using System;
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.Economy;
using LastOrders.Survivors;
using UnityEngine;

namespace LastOrders.Defense
{
    /// <summary>One shot, as it was fired: at what, where it was aimed, and how long it takes to get there.</summary>
    public readonly struct DefenseShot
    {
        public readonly IDefenseTarget Target;

        /// <summary>The target's feet when the shot was fired. Taken before any damage, because a
        /// killing shot can hand the target back to its pool - moved and parked - before anyone
        /// draws the shot.</summary>
        public readonly Vector3 HitPoint;

        /// <summary>Seconds until it lands. Zero for hitscan: it already has.</summary>
        public readonly float FlightSeconds;

        public DefenseShot(IDefenseTarget target, Vector3 hitPoint, float flightSeconds)
        {
            Target = target;
            HitPoint = hitPoint;
            FlightSeconds = flightSeconds;
        }

        public bool InFlight => FlightSeconds > 0f;
    }

    /// <summary>
    /// One defense in the world, as the rules see it: where it stands, whether it is built,
    /// its level, who is posted to it, and what it is aiming at.
    ///
    /// A plain class with no scene in it. The post in the scene registers one of these and
    /// only draws what it says - the guard turning, the muzzle flash - so the whole of the
    /// combat can be run and tested without a tower model, and a turret that looks nothing
    /// like a tower runs the identical code.
    ///
    /// The fire loop is four questions a tick: is anyone up there, what is the nearest thing
    /// in sight, is it in range, and is the weapon ready. Nothing here asks what kind of
    /// defense it is.
    /// </summary>
    public sealed class DefenseUnit
    {
        public readonly string Id;
        public readonly DefenseDefinition Definition;

        /// <summary>A thrown shot on its way: what it will do to whom, and how long until it does.</summary>
        struct Pending
        {
            public IDefenseTarget Target;
            public WeaponDefinition Weapon;
            public float Left;
        }

        readonly List<Pending> inFlight = new List<Pending>();

        float cooldown;

        public DefenseUnit(string id, DefenseDefinition definition, Vector3 position)
        {
            Id = id;
            Definition = definition;
            Position = position;
        }

        /// <summary>The foot of the defense. Ranges are measured flat from here, not from the
        /// raised platform, so a taller tower does not quietly reach further.</summary>
        public Vector3 Position { get; set; }

        public bool IsBuilt { get; internal set; }

        /// <summary>Zero-based: 0 is Level 1.</summary>
        public int LevelIndex { get; internal set; }

        /// <summary>The Survivor posted here. Null when nobody is.</summary>
        public Survivor Crew { get; private set; }

        /// <summary>Whether the crew is at the post right now. A Nervous guard is still posted
        /// here through the siege - they have simply fled it.</summary>
        public bool CrewOnDuty { get; internal set; }

        /// <summary>The nearest enemy in sight. Tracked from the moment it is spotted, not from
        /// the moment it is in range, so the first shot is not spent turning round.</summary>
        public IDefenseTarget Target { get; private set; }

        /// <summary>Knocked over with what it stood on - a wall bastion battered down - and waiting
        /// for that to be rebuilt. It neither watches nor shoots meanwhile.</summary>
        public bool IsOutOfAction { get; internal set; }

        /// <summary>The last shot could not be paid for. Only a weapon that spends Ammo can be here.</summary>
        public bool IsOutOfAmmo { get; private set; }

        public int ShotsFired { get; private set; }

        /// <summary>Thrown shots fired that have not landed yet.</summary>
        public int ShotsInFlight => inFlight.Count;

        public DefenseLevel CurrentLevel => Definition != null ? Definition.Level(LevelIndex) : null;

        public WeaponDefinition Weapon => CurrentLevel?.weapon;

        /// <summary>Level as the player counts it, from 1.</summary>
        public int LevelNumber => LevelIndex + 1;

        public bool NeedsCrew => Definition != null && Definition.requiresCrew;

        /// <summary>True when the crew rule is satisfied: someone is up there, or nobody needs to be.</summary>
        public bool IsCrewed => !NeedsCrew || (Crew != null && CrewOnDuty);

        public bool CanFire => IsBuilt && !IsOutOfAction && Weapon != null && IsCrewed;

        public bool HasNextLevel => Definition != null && Definition.HasLevel(LevelIndex + 1);

        /// <summary>
        /// A shot was fired. A hitscan one has already landed; a thrown one is on its way and
        /// lands <see cref="DefenseShot.FlightSeconds"/> from now.
        /// </summary>
        public event Action<DefenseShot> OnFired;

        /// <summary>
        /// Something came into sight after a spell with nothing there. Raised once per
        /// approach, not once per enemy - it is the early warning, and a warning that fires for
        /// every Shambler in a wave of forty stops being one.
        /// </summary>
        public event Action<IDefenseTarget> OnSpotted;

        /// <summary>Someone new was posted here, or the post was emptied (null).</summary>
        public event Action<Survivor> OnCrewChanged;

        /// <summary>It was built, or its level changed. What the post redraws on.</summary>
        public event Action OnChanged;

        internal void SetCrew(Survivor survivor)
        {
            if (ReferenceEquals(Crew, survivor)) return;

            Crew = survivor;
            if (survivor == null) CrewOnDuty = false;

            OnCrewChanged?.Invoke(survivor);
        }

        internal void MarkBuilt()
        {
            if (IsBuilt) return;

            IsBuilt = true;
            OnChanged?.Invoke();
        }

        internal void SetLevel(int index)
        {
            if (index == LevelIndex) return;

            LevelIndex = index;
            OnChanged?.Invoke();
        }

        /// <summary>
        /// One step of the fire loop.
        ///
        /// Cooldown carries its overshoot from one shot to the next, so a pistol at 1.5 seconds
        /// fires at 0, 1.5 and 3.0 whatever the frame rate - but it never banks: a weapon that
        /// sat idle for a minute fires one shot when something arrives, not forty.
        /// </summary>
        internal void Tick(float deltaSeconds, IReadOnlyList<IDefenseTarget> hostiles, ResourceLedger stores)
        {
            // What is already in the air lands whatever happens to the defense that threw it.
            Land(deltaSeconds, hostiles);

            WeaponDefinition weapon = Weapon;

            if (!CanFire)
            {
                // Nobody up there: nothing is watched and nothing is shot. The weapon is left
                // ready, so a guard arriving mid-siege fires at the first thing in range.
                Target = null;
                cooldown = 0f;
                return;
            }

            Track(Nearest(Position, CurrentLevel.SpotRange, hostiles));

            // Ready before this step began: whatever is shot now is shot the moment it arrived,
            // and none of the step's time was spent waiting on the weapon.
            bool wasReady = cooldown <= 0f;
            cooldown -= deltaSeconds;

            float range = weapon.rangeMetres;
            bool inRange = Target != null && FlatDistanceSquared(Position, Target.Position) <= range * range;

            if (!inRange)
            {
                if (cooldown < 0f) cooldown = 0f;
                return;
            }

            if (cooldown > 0f) return;

            if (!TryPayForShot(weapon, stores))
            {
                cooldown = 0f;
                return;
            }

            IDefenseTarget shot = Target;
            Vector3 hitPoint = shot.Position;
            float flight = weapon.FlightSeconds(Mathf.Sqrt(FlatDistanceSquared(Position, hitPoint)));

            if (flight > 0f) inFlight.Add(new Pending { Target = shot, Weapon = weapon, Left = flight });
            else Hit(shot, weapon, hostiles);

            ShotsFired++;

            // Carrying the overshoot is only right while firing continuously. Carrying it off an
            // idle weapon would count waiting-for-a-target as reloading, and the second shot
            // would come a frame early.
            cooldown = wasReady ? weapon.secondsBetweenShots : cooldown + weapon.secondsBetweenShots;
            if (cooldown < 0f) cooldown = 0f;

            OnFired?.Invoke(new DefenseShot(shot, hitPoint, flight));
        }

        /// <summary>
        /// Brings every thrown shot nearer by <paramref name="deltaSeconds"/> and lands the ones
        /// that arrive. One that arrives at something already dead - killed by another defense
        /// on the way - is spent on nothing, as a lobbed shot would be. Nothing aims these in
        /// flight: a shot thrown at an enemy lands on that enemy. (A pooled enemy killed and
        /// spawned again inside one flight - about a second - would take the hit in its new life;
        /// rare enough, and small enough, to leave.)
        /// </summary>
        void Land(float deltaSeconds, IReadOnlyList<IDefenseTarget> hostiles)
        {
            for (int i = inFlight.Count - 1; i >= 0; i--)
            {
                Pending shot = inFlight[i];
                shot.Left -= deltaSeconds;

                if (shot.Left > 0f)
                {
                    inFlight[i] = shot;
                    continue;
                }

                inFlight.RemoveAt(i);

                IDefenseTarget target = shot.Target;
                if (!IsLiving(target)) continue;

                Hit(target, shot.Weapon, hostiles);
            }
        }

        /// <summary>
        /// A shot from <paramref name="weapon"/> arriving at <paramref name="target"/>: the full
        /// damage to it, a share of it to everything else living within the splash - measured flat,
        /// from where the target stands, before anyone takes damage - and every one of them slowed
        /// if the weapon slows.
        /// </summary>
        static void Hit(IDefenseTarget target, WeaponDefinition weapon, IReadOnlyList<IDefenseTarget> hostiles)
        {
            if (weapon == null) return;

            Vector3 centre = target.Position;
            Strike(target, weapon.damagePerShot, weapon);

            if (!weapon.Splashes || hostiles == null) return;

            float reach = weapon.splashRadius * weapon.splashRadius;
            float share = weapon.damagePerShot * weapon.splashFraction;

            // Gathered before any damage: a kill can hand an enemy back to its pool and reorder the list.
            List<IDefenseTarget> caught = null;
            for (int i = 0; i < hostiles.Count; i++)
            {
                IDefenseTarget other = hostiles[i];
                if (ReferenceEquals(other, target) || !IsLiving(other)) continue;
                if (FlatDistanceSquared(centre, other.Position) > reach) continue;

                (caught ??= new List<IDefenseTarget>()).Add(other);
            }

            if (caught == null) return;
            foreach (IDefenseTarget other in caught)
                if (IsLiving(other)) Strike(other, share, weapon);
        }

        static void Strike(IDefenseTarget target, float damage, WeaponDefinition weapon)
        {
            if (weapon.Slows && target is ISlowable slowable) slowable.Slow(weapon.slowMultiplier, weapon.slowSeconds);
            target.ApplyDamage(damage);
        }

        static bool IsLiving(IDefenseTarget target) =>
            target != null && !(target is UnityEngine.Object o && o == null) && target.IsAlive;

        void Track(IDefenseTarget next)
        {
            bool hadSomethingInSight = Target != null;
            Target = next;

            if (next != null && !hadSomethingInSight) OnSpotted?.Invoke(next);
        }

        bool TryPayForShot(WeaponDefinition weapon, ResourceLedger stores)
        {
            if (weapon.ammoPerShot <= 0)
            {
                IsOutOfAmmo = false;
                return true;
            }

            // Checked and taken directly rather than through TrySpend, which wants a cost array:
            // a machine gun asks this several times a second and should not allocate to do it.
            if (stores == null || stores.Get(ResourceType.Ammo) < weapon.ammoPerShot)
            {
                IsOutOfAmmo = true;
                return false;
            }

            stores.Add(ResourceType.Ammo, -weapon.ammoPerShot);
            IsOutOfAmmo = false;
            return true;
        }

        /// <summary>
        /// The closest living target within <paramref name="range"/> of <paramref name="from"/>,
        /// measured flat. Nearest is the whole targeting rule for now; a turret that should
        /// prefer the enemy furthest along its path is a second rule here, chosen by its
        /// definition.
        /// </summary>
        public static IDefenseTarget Nearest(Vector3 from, float range, IReadOnlyList<IDefenseTarget> candidates)
        {
            if (candidates == null || range <= 0f) return null;

            float best = range * range;
            IDefenseTarget found = null;

            for (int i = 0; i < candidates.Count; i++)
            {
                IDefenseTarget candidate = candidates[i];

                // The interface hides Unity's null: a destroyed enemy is not == null as an
                // IDefenseTarget, only as the Object underneath.
                if (candidate == null || (candidate is UnityEngine.Object o && o == null)) continue;
                if (!candidate.IsAlive) continue;

                float distance = FlatDistanceSquared(from, candidate.Position);
                if (distance > best) continue;

                best = distance;
                found = candidate;
            }

            return found;
        }

        public static float FlatDistanceSquared(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return x * x + z * z;
        }
    }
}
