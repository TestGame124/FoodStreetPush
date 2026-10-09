using System;
using DG.Tweening;
using FoodStreet.Navigation;
using LastOrders.Consequence;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Defense;
using LastOrders.Perimeter;
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
    ///
    /// The compound's outer wall and its gates are not on the grid, so they cannot be the next
    /// cell. They are what the steering refuses a step into instead, and the rule is the same:
    /// whatever stopped the zombie is what it starts hitting - a blow a second, arms coming down
    /// on it - until it falls or opens. The restaurant's walls never do: a zombie at them keeps
    /// hitting them until something kills it.
    ///
    /// Dying is seen, not a blink: the body topples, lies there a moment and sinks away, and
    /// only then is the instance free to go back to its pool (<see cref="OnFallen"/>).
    /// </summary>
    [RequireComponent(typeof(SteeringAgent))]
    public sealed class EnemyAgent : MonoBehaviour, IPoolable, IDefenseTarget, ISlowable
    {
        enum State { Moving, Attacking, Dying }

        /// <summary>Seconds between blows on the compound wall. Slow enough that each one reads.</summary>
        const float BlowInterval = 1f;

        /// <summary>Seconds a body takes to topple, then lies still, then takes to sink away.</summary>
        const float FallSeconds = 0.45f;
        const float LieSeconds = 0.9f;
        const float SinkSeconds = 1.2f;

        /// <summary>Metres a body sinks: far enough to be gone below the ground.</summary>
        const float SinkDepth = 0.8f;

        [SerializeField] FlowFieldFollower follower;
        [SerializeField] SteeringAgent steering;

        EnemyDefinition definition;
        State state;
        float health;
        float attackAccumulator;
        DamageableStructure target;

        /// <summary>The wall section or gate being battered, when it is one of those rather than
        /// something on the grid.</summary>
        IBreakable breaking;

        float blowCooldown;
        IEnemyBody body;

        /// <summary>The share of its speed it is held to by wire or a trap, and until when.</summary>
        float slowMultiplier = 1f;
        float slowUntil;

        /// <summary>Raised when this agent dies, with itself and its definition, so the night
        /// can tally kills and drops without walking a list of live agents.</summary>
        public event Action<EnemyAgent, EnemyDefinition> OnDied;

        /// <summary>Raised once the body has fallen and sunk out of sight, after <see cref="OnDied"/>:
        /// the moment the instance can go back to its pool.</summary>
        public event Action<EnemyAgent> OnFallen;

        public EnemyDefinition Definition => definition;

        public bool IsAlive => state != State.Dying;

        public Vector3 Position => transform.position;

        public string DisplayName => definition != null ? definition.displayName : name;

        void Awake()
        {
            if (steering == null) steering = GetComponent<SteeringAgent>();
            if (follower == null) follower = GetComponent<FlowFieldFollower>();

            // Stopped by every wall and let through no gate.
            if (steering != null) steering.Mover = MoverKind.Hostile;

            // TryGetComponent, not GetComponent: a missing interface can come back as Unity's fake
            // null, which an interface-typed null check does not see.
            body = TryGetComponent(out IEnemyBody found) ? found : null;
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
            breaking = null;

            transform.position = spawnPosition;
            SetAnimated(true);
            EndSlow();

            if (steering != null)
            {
                steering.enabled = true;
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

            if (slowMultiplier < 1f && Time.time >= slowUntil) EndSlow();

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
            if (definition.isNonCombatant) return;

            DamageableStructure blocker = FindBlockingStructure();

            if (blocker != null)
            {
                target = blocker;
                StartAttacking();
                return;
            }

            // Walked into the compound wall or a shut gate.
            if (steering != null && steering.BlockedBy is IBreakable wall && wall.IsInTheWay)
            {
                breaking = wall;

                // Out of step with the rest of the crowd at it, so the blows land one by one.
                blowCooldown = UnityEngine.Random.Range(0.15f, 0.6f);
                StartAttacking();
            }
        }

        void StartAttacking()
        {
            state = State.Attacking;
            if (follower != null) follower.active = false;
            if (steering != null) steering.Halt();
        }

        void TickAttacking(float deltaTime)
        {
            if (breaking != null)
            {
                TickBreaking(deltaTime);
                return;
            }

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

        /// <summary>
        /// Batters the wall section or gate in front of it: a blow every second, each worth a
        /// second of the archetype's structure damage, so a Brute still goes through a wall four
        /// times faster than a Shambler. Stops the moment it falls, or a gate in it opens.
        /// </summary>
        void TickBreaking(float deltaTime)
        {
            if (breaking == null || (breaking is UnityEngine.Object o && o == null) || !breaking.IsInTheWay)
            {
                ResumeMoving();
                return;
            }

            Vector3 toward = breaking.ClosestPoint(transform.position) - transform.position;
            toward.y = 0f;
            if (toward.sqrMagnitude > 1e-4f)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, Quaternion.LookRotation(toward), 360f * deltaTime);
            }

            blowCooldown -= deltaTime;
            if (blowCooldown > 0f) return;

            blowCooldown = BlowInterval;

            if (body != null) body.Pound();

            if (breaking.TakeHit(definition.structureDps * BlowInterval, transform.position)) ResumeMoving();
        }

        void ResumeMoving()
        {
            target = null;
            breaking = null;
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

        /// <summary>
        /// Held back by wire or pinned by a trap: walks at <paramref name="multiplier"/> of its pace
        /// until <paramref name="seconds"/> after the last time it was caught. The strongest hold
        /// wins while more than one has it.
        /// </summary>
        public void Slow(float multiplier, float seconds)
        {
            if (state == State.Dying) return;

            float now = Time.time;
            if (now >= slowUntil) slowMultiplier = 1f;

            slowMultiplier = Mathf.Min(slowMultiplier, Mathf.Clamp(multiplier, 0.05f, 1f));
            slowUntil = Mathf.Max(slowUntil, now + Mathf.Max(0f, seconds));

            if (steering != null) steering.SpeedScale = slowMultiplier;
        }

        /// <summary>Back to its own pace.</summary>
        void EndSlow()
        {
            slowMultiplier = 1f;
            slowUntil = 0f;
            if (steering != null) steering.SpeedScale = 1f;
        }

        public void ApplyDamage(float amount)
        {
            if (state == State.Dying) return;

            health -= amount;
            if (health > 0f)
            {
                body?.Hurt(amount);
                return;
            }

            state = State.Dying;
            if (follower != null) follower.active = false;

            // A body now: nothing walks round it and nothing jostles it.
            if (steering != null)
            {
                steering.Halt();
                steering.enabled = false;
            }

            OnDied?.Invoke(this, definition);

            // Unless whoever heard it sent this straight back to its pool.
            if (state == State.Dying) Fall();
        }

        /// <summary>Topples onto its back, lies there a moment, then sinks out of sight.</summary>
        void Fall()
        {
            // Lies as the walk left it: no reaching arms pointing at the sky.
            SetAnimated(false);

            transform.DOKill();

            Vector3 fallen = transform.eulerAngles + new Vector3(-90f, 0f, 0f);

            Sequence fall = DOTween.Sequence().SetTarget(transform);
            fall.Append(transform.DORotate(fallen, FallSeconds).SetEase(Ease.InQuad));
            fall.AppendInterval(LieSeconds);
            fall.Append(transform.DOMove(transform.position + Vector3.down * SinkDepth, SinkSeconds).SetEase(Ease.InQuad));
            fall.OnComplete(() => OnFallen?.Invoke(this));
        }

        /// <summary>Turns the body's own posing - the reach, the stoop, the waddle - on or off.</summary>
        void SetAnimated(bool on)
        {
            if (body is Behaviour behaviour && behaviour.enabled != on) behaviour.enabled = on;
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
            breaking = null;
            definition = null;

            OnDied = null;
            OnFallen = null;

            // Recalled mid-fall at dawn: the fall stops where it is, and everything it moved is
            // put back below.
            transform.DOKill();
            SetAnimated(true);

            health = 0f;
            attackAccumulator = 0f;
            blowCooldown = 0f;
            HeatEmitted = 0f;
            state = State.Moving;
            EndSlow();

            // Steering turns the agent to face where it walks. Nothing reset that until
            // something could kill an enemy mid-walk - the guard towers - and every pooled
            // death then failed the integrity check.
            transform.localRotation = Quaternion.identity;

            if (follower != null) follower.active = false;
            if (steering != null)
            {
                steering.enabled = true;
                steering.Halt();
            }
        }
    }
}
