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
    /// A handful of states - Moving, Raiding, Attacking, Retreating, Dying - and no archetype-specific
    /// code anywhere. The roster's variety comes entirely from which flow field the definition points
    /// at: a Brute samples Breach so walls are cheap to it, a Flanker samples WeakestPoint so it
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
    /// hitting them, and in an attack every blow costs the restaurant health.
    ///
    /// One decision is made, and only once a zombie is over the wall: whether there is something
    /// in the yard to smash on the way to the restaurant. The attack's yard service answers it, and
    /// stops answering once the night's cap on wrecks is reached - after that every zombie goes for
    /// the restaurant.
    ///
    /// Dying is seen, not a blink: the body falls - by its own animation when it has one, toppled
    /// whole when it does not - lies there a moment and sinks away, and only then is the instance
    /// free to go back to its pool (<see cref="OnFallen"/>). So is
    /// leaving: when morning comes, whatever is still standing turns, shambles off and sinks into
    /// the dark rather than vanishing.
    /// </summary>
    [RequireComponent(typeof(SteeringAgent))]
    public sealed class EnemyAgent : MonoBehaviour, IPoolable, IDefenseTarget, ISlowable
    {
        enum State { Moving, Raiding, Attacking, Retreating, Dying }

        /// <summary>Seconds between blows on the compound wall. Slow enough that each one reads.</summary>
        const float BlowInterval = 1f;

        /// <summary>Seconds a body takes to topple, then lies still, then takes to sink away.</summary>
        const float FallSeconds = 0.45f;
        const float LieSeconds = 0.9f;
        const float SinkSeconds = 1.2f;

        /// <summary>Metres a body sinks: far enough to be gone below the ground.</summary>
        const float SinkDepth = 0.8f;

        /// <summary>Seconds between looks for something in the yard to smash. Cheap, and a zombie
        /// that has only just stepped over the wall does not need to decide on the same frame.</summary>
        const float RaidLookSeconds = 0.5f;

        /// <summary>Seconds before a zombie that gave up on something in the yard looks again.
        /// Long enough that one blocked by a wall commits to the restaurant instead of dithering.</summary>
        const float RaidRetrySeconds = 4f;

        /// <summary>Metres from an object's edge that a raider can reach it from.</summary>
        const float RaidReach = 0.75f;

        /// <summary>Seconds a retreating zombie shambles off before it sinks out of sight.</summary>
        const float RetreatWalkSeconds = 2.6f;
        const float RetreatSinkSeconds = 0.9f;

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

        /// <summary>The yard during an attack, which hands out things to smash. Set by the spawner.</summary>
        IOutdoorRaids raids;

        /// <summary>What it is on its way to smash, or smashing, in the yard.</summary>
        OutdoorWreck raidTarget;

        PathFollower pathFollower;
        float raidLookIn;
        float raidGiveUpAt;
        float raidGiveUpSeconds = 20f;

        Vector3 retreatDirection;
        float retreatLeft;
        Vector3 baseScale = Vector3.one;

        /// <summary>Raised when this agent dies, with itself and its definition, so the night
        /// can tally kills and drops without walking a list of live agents.</summary>
        public event Action<EnemyAgent, EnemyDefinition> OnDied;

        /// <summary>Raised once the body has fallen and sunk out of sight - or, at morning, once it has
        /// slunk off and sunk away - after <see cref="OnDied"/>: the moment the instance can go back
        /// to its pool.</summary>
        public event Action<EnemyAgent> OnFallen;

        public EnemyDefinition Definition => definition;

        public bool IsAlive => state != State.Dying;

        /// <summary>Turned for home as the morning comes. Off every list; nothing shoots at it.</summary>
        public bool IsRetreating => state == State.Retreating;

        /// <summary>Going for something in the yard, or smashing it.</summary>
        public bool IsRaiding => raidTarget != null;

        public Vector3 Position => transform.position;

        public string DisplayName => definition != null ? definition.displayName : name;

        /// <summary>The attack's yard. The spawner hands it over as it takes the agent from its pool;
        /// without one the zombie goes straight for the restaurant, as it always did.</summary>
        public IOutdoorRaids Raids
        {
            get => raids;
            set => raids = value;
        }

        void Awake()
        {
            if (steering == null) steering = GetComponent<SteeringAgent>();
            if (follower == null) follower = GetComponent<FlowFieldFollower>();

            // Walks to one thing in the yard on its own route; the crowd's field cannot.
            pathFollower = GetComponent<PathFollower>();
            if (pathFollower == null) pathFollower = gameObject.AddComponent<PathFollower>();

            baseScale = transform.localScale;

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
            raidTarget = null;
            raidLookIn = UnityEngine.Random.Range(0f, RaidLookSeconds);

            transform.position = spawnPosition;
            transform.localScale = baseScale;
            SetAnimated(true);
            EndSlow();

            if (steering != null)
            {
                steering.enabled = true;
                steering.Warp(spawnPosition);
                steering.ConfigureBody(def.moveSpeed, def.agentRadius);
            }

            if (pathFollower != null) pathFollower.Stop();

            if (follower != null)
            {
                follower.Field = def.FlowFieldId;
                follower.active = true;
            }
        }

        /// <summary>How long it keeps trying to reach a raid target before giving up on it.</summary>
        public void ConfigureRaids(IOutdoorRaids yard, float giveUpSeconds)
        {
            raids = yard;
            raidGiveUpSeconds = Mathf.Max(1f, giveUpSeconds);
        }

        void Update()
        {
            if (state == State.Dying || definition == null) return;

            if (slowMultiplier < 1f && Time.time >= slowUntil) EndSlow();

            switch (state)
            {
                case State.Moving: TickMoving(Time.deltaTime); break;
                case State.Raiding: TickRaiding(); break;
                case State.Attacking: TickAttacking(Time.deltaTime); break;
                case State.Retreating: TickRetreating(Time.deltaTime); break;
            }

            if (definition.heatPerSecond > 0f && state != State.Retreating)
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

        void TickMoving(float deltaTime)
        {
            // The only question asked each frame: is the thing in front of me something I
            // have to go through? The flow field has already decided the direction.
            if (definition.isNonCombatant) return;

            // Over the wall, the yard comes first: something to smash on the way, while the
            // night still allows it.
            if (TryStartRaid(deltaTime)) return;

            DamageableStructure blocker = FindBlockingStructure();

            if (blocker != null)
            {
                target = blocker;
                blowCooldown = UnityEngine.Random.Range(0.1f, 0.5f);
                StartAttacking();
                return;
            }

            // Walked into the compound wall, a shut gate or the restaurant itself.
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
            if (pathFollower != null) pathFollower.Stop();
            if (steering != null) steering.Halt();
        }

        void TickAttacking(float deltaTime)
        {
            if (raidTarget != null)
            {
                TickSmashing(deltaTime);
                return;
            }

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

            Face(target.transform.position, deltaTime);

            // The damage runs on by the frame; the arms come down once a second so it is seen.
            blowCooldown -= deltaTime;
            if (blowCooldown <= 0f)
            {
                blowCooldown = BlowInterval;
                if (body != null) body.Pound();
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

            Face(breaking.ClosestPoint(transform.position), deltaTime);

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
            EndRaid();

            attackAccumulator = 0f;
            state = State.Moving;
            if (follower != null) follower.active = true;
        }

        void Face(Vector3 point, float deltaTime)
        {
            Vector3 toward = point - transform.position;
            toward.y = 0f;
            if (toward.sqrMagnitude <= 1e-4f) return;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(toward), 360f * deltaTime);
        }

        static void Ignite(DamageableStructure structure)
        {
            var fire = structure.GetComponent<StructureFire>();
            if (fire != null) fire.Ignite();
        }

        // --- The yard ----------------------------------------------------------------------------

        /// <summary>Asks the yard for something to smash, every so often. True when it has set off for it.</summary>
        bool TryStartRaid(float deltaTime)
        {
            if (raids == null || pathFollower == null) return false;

            raidLookIn -= deltaTime;
            if (raidLookIn > 0f) return false;

            raidLookIn = RaidLookSeconds;

            if (!raids.TryAssign(this, transform.position, out OutdoorWreck found) || found == null) return false;

            raidTarget = found;
            raidGiveUpAt = Time.time + raidGiveUpSeconds;
            state = State.Raiding;

            if (follower != null) follower.active = false;
            pathFollower.SetDestination(found.ClosestPoint(transform.position));
            return true;
        }

        /// <summary>On its way to the thing it means to smash.</summary>
        void TickRaiding()
        {
            if (raids == null || !raids.StillWanted(this, raidTarget))
            {
                ResumeMoving();
                return;
            }

            // Could not get to it in time: off to the restaurant, and not straight back again.
            if (Time.time >= raidGiveUpAt)
            {
                GiveUpRaid();
                return;
            }

            Vector3 here = transform.position;

            if (raidTarget.FlatDistance(here) <= RaidReach + (definition != null ? definition.agentRadius : 0.35f))
            {
                blowCooldown = UnityEngine.Random.Range(0.1f, 0.5f);
                StartAttacking();
                return;
            }

            // Walked into a wall on the way - the restaurant's, or the compound's from inside: the
            // yard can wait, the flow field knows the way from here.
            if (steering != null && steering.BlockedBy is IBreakable wall && wall.IsInTheWay)
            {
                GiveUpRaid();
                return;
            }

            // The route ends at the nearest spot it fits, which can be short of the thing itself
            // when it fills its cells: the last stretch is straight at it.
            if (pathFollower.HasArrived && steering != null)
                steering.SetDesiredDirection(raidTarget.ClosestPoint(here) - here, 0.6f);
        }

        /// <summary>Smashing something in the yard: a blow a second, like a wall, until it is a wreck
        /// or the night's cap says the rest is the restaurant's.</summary>
        void TickSmashing(float deltaTime)
        {
            if (raids == null || !raids.StillWanted(this, raidTarget))
            {
                ResumeMoving();
                return;
            }

            Face(raidTarget.ClosestPoint(transform.position), deltaTime);

            blowCooldown -= deltaTime;
            if (blowCooldown > 0f) return;

            blowCooldown = BlowInterval;

            if (body != null) body.Pound();

            if (raids.Strike(this, raidTarget, definition.structureDps * BlowInterval, transform.position))
                ResumeMoving();
        }

        void GiveUpRaid()
        {
            ResumeMoving();
            raidLookIn = RaidRetrySeconds;
        }

        void EndRaid()
        {
            if (raidTarget == null) return;

            raids?.Release(this);
            raidTarget = null;
            if (pathFollower != null) pathFollower.Stop();
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

        // --- Morning --------------------------------------------------------------------------------

        /// <summary>
        /// The night is over: it stops whatever it was doing, turns its back on
        /// <paramref name="awayFrom"/> - the restaurant - and shambles off, then sinks out of sight
        /// and is free to go back to its pool. Nothing hits it and it hits nothing on the way.
        /// </summary>
        public void Retreat(Vector3 awayFrom)
        {
            if (state == State.Dying || state == State.Retreating) return;

            EndRaid();
            target = null;
            breaking = null;
            attackAccumulator = 0f;

            state = State.Retreating;
            retreatLeft = RetreatWalkSeconds * UnityEngine.Random.Range(0.8f, 1.2f);

            if (follower != null) follower.active = false;

            Vector3 away = transform.position - awayFrom;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -transform.forward;

            // Not quite straight back: a crowd leaving should fan out, not file off in a line.
            retreatDirection = Quaternion.Euler(0f, UnityEngine.Random.Range(-25f, 25f), 0f) * away.normalized;
        }

        void TickRetreating(float deltaTime)
        {
            retreatLeft -= deltaTime;

            if (retreatLeft > 0f)
            {
                if (steering != null && steering.enabled) steering.SetDesiredDirection(retreatDirection, 0.8f);
                return;
            }

            SinkAway();
        }

        /// <summary>Gone into the dark: down into the ground and shrinking, then back to the pool.</summary>
        void SinkAway()
        {
            state = State.Dying;

            if (steering != null)
            {
                steering.Halt();
                steering.enabled = false;
            }

            SetAnimated(false);
            transform.DOKill();

            Sequence leave = DOTween.Sequence().SetTarget(transform);
            leave.Append(transform.DOMove(transform.position + Vector3.down * SinkDepth, RetreatSinkSeconds).SetEase(Ease.InQuad));
            leave.Join(transform.DOScale(baseScale * 0.6f, RetreatSinkSeconds).SetEase(Ease.InQuad));
            leave.OnComplete(() => OnFallen?.Invoke(this));
        }

        // --- Being held back and hurt ------------------------------------------------------------

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
            if (state == State.Dying || state == State.Retreating) return;

            health -= amount;
            if (health > 0f)
            {
                body?.Hurt(amount);
                return;
            }

            EndRaid();
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

        /// <summary>Falls down - its own fall, or toppled onto its back - lies there a moment, then sinks out of sight.</summary>
        void Fall()
        {
            transform.DOKill();

            float falling = body != null ? body.Die() : 0f;

            Sequence fall = DOTween.Sequence().SetTarget(transform);

            if (falling > 0f)
            {
                fall.AppendInterval(falling);
            }
            else
            {
                // Lies as the walk left it: no reaching arms pointing at the sky.
                SetAnimated(false);

                Vector3 fallen = transform.eulerAngles + new Vector3(-90f, 0f, 0f);
                fall.Append(transform.DORotate(fallen, FallSeconds).SetEase(Ease.InQuad));
            }

            fall.AppendInterval(LieSeconds);
            fall.Append(transform.DOMove(transform.position + Vector3.down * SinkDepth, SinkSeconds).SetEase(Ease.InQuad));
            fall.OnComplete(() => OnFallen?.Invoke(this));
        }

        /// <summary>Turns the body's own posing - the reach, the stoop, the waddle - on or off.</summary>
        void SetAnimated(bool on)
        {
            if (body is Behaviour behaviour && behaviour.enabled != on) behaviour.enabled = on;
        }

        /// <summary>The body from the start: standing, walking, a new face - off and on again, which
        /// is where every body starts clean. One that fell by itself was never switched off.</summary>
        void RestartBody()
        {
            if (!(body is Behaviour behaviour)) return;

            behaviour.enabled = false;
            behaviour.enabled = true;
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
            raids?.Release(this);
            raids = null;
            raidTarget = null;
            target = null;
            breaking = null;
            definition = null;

            OnDied = null;
            OnFallen = null;

            // Recalled mid-fall at dawn: the fall stops where it is, and everything it moved is
            // put back below.
            transform.DOKill();
            transform.localScale = baseScale;
            RestartBody();

            health = 0f;
            attackAccumulator = 0f;
            blowCooldown = 0f;
            HeatEmitted = 0f;
            retreatLeft = 0f;
            state = State.Moving;
            EndSlow();

            // Steering turns the agent to face where it walks. Nothing reset that until
            // something could kill an enemy mid-walk - the guard towers - and every pooled
            // death then failed the integrity check.
            transform.localRotation = Quaternion.identity;

            if (pathFollower != null) pathFollower.Stop();
            if (follower != null) follower.active = false;
            if (steering != null)
            {
                steering.enabled = true;
                steering.Halt();
            }
        }
    }
}
