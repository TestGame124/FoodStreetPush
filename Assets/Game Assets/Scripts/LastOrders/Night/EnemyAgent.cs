using System;
using FoodStreet.Navigation;
using LastOrders.Consequence;
using LastOrders.Core;
using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Night
{
    /// <summary>
    /// Every enemy in the game.
    ///
    /// Three states - Moving, Attacking, Dying - and no archetype-specific code anywhere.
    /// The roster's variety comes entirely from which flow field the definition points at:
    /// a Brute samples Breach so walls are cheap to it, a Flanker samples WeakestPoint so it
    /// pools wherever the shell is thinnest, a Glutton samples Pantry and ignores people
    /// altogether. The Shambler and the Brute run the identical code path.
    ///
    /// This is 24.3 doing double duty as an AI architecture, and it is why there are no
    /// behaviour trees, no utility AI and no per-enemy state machines here. Attacking is not
    /// a decision either: it is what happens when the next cell on the path is a wall.
    /// Walls are expensive rather than impassable, so the field is always solvable and there
    /// is no fallback logic to get wrong.
    /// </summary>
    [RequireComponent(typeof(SteeringAgent))]
    public sealed class EnemyAgent : MonoBehaviour, IPoolable
    {
        enum State { Moving, Attacking, Dying }

        [SerializeField] FlowFieldFollower follower;
        [SerializeField] SteeringAgent steering;

        EnemyDefinition definition;
        State state;
        float health;
        float attackAccumulator;
        DamageableStructure target;

        /// <summary>Raised when this agent dies, with itself and its definition, so the night
        /// can tally kills and drops without walking a list of live agents.</summary>
        public event Action<EnemyAgent, EnemyDefinition> OnDied;

        public EnemyDefinition Definition => definition;

        public bool IsAlive => state != State.Dying;

        void Awake()
        {
            if (steering == null) steering = GetComponent<SteeringAgent>();
            if (follower == null) follower = GetComponent<FlowFieldFollower>();
        }

        /// <summary>
        /// Applies an archetype to a pooled instance. Everything that distinguishes one
        /// enemy from another happens in these few lines.
        /// </summary>
        public void Configure(EnemyDefinition def, Vector3 spawnPosition)
        {
            definition = def;
            health = def.maxHealth;
            state = State.Moving;
            attackAccumulator = 0f;
            target = null;

            transform.position = spawnPosition;

            if (steering != null)
            {
                steering.Warp(spawnPosition);
                steering.ConfigureBody(def.moveSpeed, def.agentRadius);
            }

            if (follower != null)
            {
                follower.Field = def.FlowFieldId;
                follower.active = true;
            }
        }

        void Update()
        {
            if (state == State.Dying || definition == null) return;

            switch (state)
            {
                case State.Moving: TickMoving(); break;
                case State.Attacking: TickAttacking(Time.deltaTime); break;
            }

            if (definition.heatPerSecond > 0f)
                HeatEmitted += definition.heatPerSecond * Time.deltaTime;
        }

        /// <summary>Heat this agent has emitted since spawning. The Screecher's entire role,
        /// drained by the night each frame rather than pushed, so a pooled agent cannot
        /// double-report after being released.</summary>
        public float HeatEmitted { get; private set; }

        public float ConsumeEmittedHeat()
        {
            float value = HeatEmitted;
            HeatEmitted = 0f;
            return value;
        }

        void TickMoving()
        {
            // The only question asked each frame: is the thing in front of me something I
            // have to go through? The flow field has already decided the direction.
            DamageableStructure blocker = FindBlockingStructure();

            if (blocker != null && !definition.isNonCombatant)
            {
                target = blocker;
                state = State.Attacking;
                if (follower != null) follower.active = false;
                if (steering != null) steering.Halt();
            }
        }

        void TickAttacking(float deltaTime)
        {
            if (target == null || target.State == DamageState.Destroyed || !target.IsFunctional)
            {
                ResumeMoving();
                return;
            }

            // Accumulated rather than applied per-frame so that damage is frame-rate
            // independent. At 120 agents the difference between 30fps and 60fps would
            // otherwise be a factor of two on how long a wall survives.
            attackAccumulator += definition.structureDps * deltaTime;

            if (attackAccumulator < 1f) return;

            float damage = Mathf.Floor(attackAccumulator);
            attackAccumulator -= damage;

            if (definition.ignitesStructures) Ignite(target);

            if (target.ApplyDamage(damage)) ResumeMoving();
        }

        void ResumeMoving()
        {
            target = null;
            attackAccumulator = 0f;
            state = State.Moving;
            if (follower != null) follower.active = true;
        }

        static void Ignite(DamageableStructure structure)
        {
            var fire = structure.GetComponent<StructureFire>();
            if (fire != null) fire.Ignite();
        }

        /// <summary>
        /// Looks one cell ahead along the flow direction. A wall there means "attack it"
        /// rather than "find another way", which is the whole of the breach behaviour.
        /// </summary>
        DamageableStructure FindBlockingStructure()
        {
            NavigationSystem nav = NavigationSystem.Instance;
            if (nav == null || nav.Grid == null) return null;

            FlowField field = nav.GetFlowField(definition.FlowFieldId);
            if (field == null || !field.IsValid) return null;

            Vector3 direction = field.GetDirection(transform.position);
            if (direction.sqrMagnitude < 0.0001f) return null;

            Vector3 probe = transform.position + direction.normalized * definition.attackRange;

            PlacedObject_Done occupant = nav.Grid.GetOccupantAt(probe);
            if (occupant == null) return null;

            PlacedObjectTypeSO type = occupant.PlacedObjectTypeSO;
            if (type == null) return null;

            // Only solid things are worth stopping for. Walking over a rug is not a breach.
            if (!type.isWall && !type.isDoor && type.seatCapacity == 0 && !type.isPantry)
                return null;

            var damageable = occupant.GetComponent<DamageableStructure>();
            return damageable != null && damageable.IsFunctional ? damageable : null;
        }

        public void ApplyDamage(float amount)
        {
            if (state == State.Dying) return;

            health -= amount;
            if (health > 0f) return;

            state = State.Dying;
            if (follower != null) follower.active = false;
            if (steering != null) steering.Halt();

            OnDied?.Invoke(this, definition);
        }

        // --- Pooling ----------------------------------------------------------
        //
        // The reset path, written before the spawn path. An enemy released dirty does not
        // break at the point of release; it breaks three nights later as something that
        // spawns already-dead or walks to a wall that was demolished ten minutes ago.

        public void OnTakenFromPool()
        {
            state = State.Moving;
            HeatEmitted = 0f;
        }

        public void OnReturnedToPool()
        {
            // Every reference to another pooled or scene object is dropped here. A retained
            // target is the classic leak: it keeps a destroyed structure alive and makes the
            // next occupant of this instance attack a wall that no longer exists.
            target = null;
            definition = null;

            OnDied = null;

            health = 0f;
            attackAccumulator = 0f;
            HeatEmitted = 0f;
            state = State.Moving;

            if (follower != null) follower.active = false;
            if (steering != null) steering.Halt();
        }
    }
}
