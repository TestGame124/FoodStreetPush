using System;
using System.Collections.Generic;
using LastOrders.Consequence;
using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Night
{
    /// <summary>
    /// What a zombie on its way asks, every so often: is there something other than the restaurant I
    /// should go and smash? Kept narrow so the agent never needs the whole director, and a test can
    /// stand in for it.
    /// </summary>
    public interface IZombieOrders
    {
        /// <summary>Something for <paramref name="zombie"/>, standing at <paramref name="from"/>, to go
        /// and smash. False when it should keep on for the restaurant.</summary>
        bool TryAssign(object zombie, Vector3 from, out IRaidTarget target);

        /// <summary>True while it is still worth going on with.</summary>
        bool StillWanted(object zombie, IRaidTarget target, Vector3 from);

        /// <summary>Lands a blow. True when the zombie is done with it, either way.</summary>
        bool Strike(object zombie, IRaidTarget target, float damage, Vector3 from);

        /// <summary>It could not get there in time: lets it go, and does not send it back to the same one.</summary>
        void GaveUp(object zombie, IRaidTarget target);

        void Release(object zombie);
    }

    /// <summary>
    /// The horde's one brain: for every zombie in an attack, whether it goes for a defense, for
    /// something in the yard, or for the restaurant - and how many go for each.
    ///
    /// <b>Who.</b> As each wave sets off, a share of its zombies are made hunters - the toughest first,
    /// a Brute before a shambler - and the rest go for the restaurant. A wave of two or more always
    /// keeps at least one on the restaurant.
    ///
    /// <b>When.</b> A hunter sets off for a defense as soon as one it can reach is within sight. Any
    /// other zombie turns on a gun that is shooting at it from close by - it was provoked. Nobody goes
    /// for a defense once it is nearly at the restaurant's door. A gun up on the wall can only be got
    /// at from outside: a zombie already over the wall has gone past it, and goes for the yard and the
    /// restaurant instead.
    ///
    /// <b>Which.</b> The nearest, made nearer for the gun shooting at it, for a defense out on the
    /// ground rather than up on a bastion, and for one already battered - and further for every zombie
    /// already on it, so a pack spreads over the defenses rather than queueing at one.
    ///
    /// <b>How many.</b> Only a few zombies on one defense at once; never more than a share of the horde
    /// on the defenses at once; and only so many defenses knocked out in one attack. Once that many
    /// are down, every zombie still standing lets go and turns on the restaurant.
    ///
    /// A plain class with no scene in it. The scene hands it the defenses through
    /// <see cref="Scanner"/>; the yard is the <see cref="IOutdoorRaids"/> it falls back on.
    /// </summary>
    public sealed class ZombieDirector : IZombieOrders
    {
        readonly Func<EncounterTuning> tuning;
        readonly IOutdoorRaids yard;
        readonly Func<Vector3, bool> isInside;
        readonly Func<Vector3, float> distanceToRestaurant;
        readonly Func<int> liveZombies;

        readonly List<IDefenseObjective> defenses = new List<IDefenseObjective>();
        readonly Dictionary<object, IDefenseObjective> assigned = new Dictionary<object, IDefenseObjective>();
        readonly Dictionary<object, IDefenseObjective> refused = new Dictionary<object, IDefenseObjective>();
        readonly HashSet<object> hunters = new HashSet<object>();
        readonly Dictionary<EnemyDefinition, int> hunterPlaces = new Dictionary<EnemyDefinition, int>();
        readonly HashSet<IDefenseObjective> announced = new HashSet<IDefenseObjective>();
        readonly List<object> scratch = new List<object>();

        /// <param name="tuning">Read on every decision, so a change in the balance asset takes at once.</param>
        /// <param name="yard">Where a zombie inside the walls is sent when no defense wants it.</param>
        /// <param name="isInside">Whether a point is inside the compound's walls.</param>
        /// <param name="distanceToRestaurant">Flat metres from a point to the restaurant's walls.</param>
        /// <param name="liveZombies">How many zombies are out right now.</param>
        public ZombieDirector(Func<EncounterTuning> tuning, IOutdoorRaids yard, Func<Vector3, bool> isInside,
                              Func<Vector3, float> distanceToRestaurant, Func<int> liveZombies)
        {
            this.tuning = tuning ?? (() => null);
            this.yard = yard;
            this.isInside = isInside ?? (_ => false);
            this.distanceToRestaurant = distanceToRestaurant ?? (_ => float.MaxValue);
            this.liveZombies = liveZombies ?? (() => 0);
        }

        /// <summary>Every defense standing in the world, found fresh. The scene's to supply.</summary>
        public Func<IReadOnlyList<IDefenseObjective>> Scanner { get; set; }

        ZombieTargetingTuning Rules => (tuning() ?? DefaultTuning).Targeting;

        static readonly EncounterTuning DefaultTuning = new EncounterTuning();

        /// <summary>True from the moment an attack is locked in until it is settled.</summary>
        public bool IsActive { get; private set; }

        public bool IsBigWave { get; private set; }

        public int KnockedOut { get; private set; }

        public int KnockOutCap => Rules.KnockOutCap(IsBigWave);

        /// <summary>More defenses tonight's attack may still knock out.</summary>
        public bool CanKnockOutMore => KnockedOut < KnockOutCap;

        /// <summary>Zombies on their way to a defense, or at it.</summary>
        public int OnDefenses => assigned.Count;

        public IReadOnlyList<IDefenseObjective> Defenses => defenses;

        /// <summary>The first zombie of the night set off for a defense.</summary>
        public event Action<IDefenseObjective> OnDefenseTargeted;

        /// <summary>Zombies knocked a defense out.</summary>
        public event Action<IDefenseObjective> OnDefenseKnockedOut;

        /// <summary>The night's limit on defenses knocked out is reached: the rest go for the restaurant.</summary>
        public event Action OnKnockOutCapReached;

        // --- The attack -------------------------------------------------------------------------

        /// <summary>The attack is locked in: the defenses are found and nobody is on them yet.</summary>
        public void BeginAttack(bool bigWave)
        {
            IsActive = true;
            IsBigWave = bigWave;
            KnockedOut = 0;

            assigned.Clear();
            refused.Clear();
            hunters.Clear();
            hunterPlaces.Clear();
            announced.Clear();

            Rescan();
        }

        public void EndAttack()
        {
            IsActive = false;
            assigned.Clear();
            refused.Clear();
            hunters.Clear();
            hunterPlaces.Clear();
        }

        /// <summary>
        /// A wave sets off: which of its zombies are hunters. The toughest - most health and most
        /// damage to structures - are picked first. With no defense standing nobody hunts.
        /// </summary>
        public void PlanWave(EncounterWave wave)
        {
            hunterPlaces.Clear();
            if (!IsActive || wave == null) return;

            // Anything built during the warning is in play from the first wave.
            Rescan();
            if (!AnyStanding()) return;

            var fighters = new List<EnemyDefinition>();
            for (int i = 0; i < wave.Spawns.Count; i++)
            {
                EnemyDefinition kind = wave.Spawns[i].Enemy;
                if (kind != null && !kind.isNonCombatant) fighters.Add(kind);
            }

            int places = Rules.HuntersIn(fighters.Count, IsBigWave);
            if (places <= 0) return;

            fighters.Sort((a, b) => Toughness(b).CompareTo(Toughness(a)));

            for (int i = 0; i < places; i++)
            {
                hunterPlaces.TryGetValue(fighters[i], out int already);
                hunterPlaces[fighters[i]] = already + 1;
            }
        }

        /// <summary>A zombie has just come out: a hunter while the wave has a place for its kind, a raider
        /// otherwise. Called for every zombie, so one out of the pool forgets what it was last time.</summary>
        public void Enlist(object zombie, EnemyDefinition kind)
        {
            if (zombie == null) return;

            Release(zombie);
            refused.Remove(zombie);

            if (kind != null && hunterPlaces.TryGetValue(kind, out int places) && places > 0)
            {
                hunterPlaces[kind] = places - 1;
                hunters.Add(zombie);
            }
            else
            {
                hunters.Remove(zombie);
            }
        }

        public bool IsHunter(object zombie) => zombie != null && hunters.Contains(zombie);

        static float Toughness(EnemyDefinition kind) => kind.maxHealth * Mathf.Max(0.1f, kind.structureDps);

        void Rescan()
        {
            if (Scanner == null) return;

            IReadOnlyList<IDefenseObjective> found = Scanner();
            defenses.Clear();
            if (found == null) return;

            for (int i = 0; i < found.Count; i++)
                if (found[i] != null) defenses.Add(found[i]);
        }

        bool AnyStanding()
        {
            for (int i = 0; i < defenses.Count; i++)
                if (defenses[i].IsStanding) return true;

            return false;
        }

        // --- Orders --------------------------------------------------------------------------------

        public bool TryAssign(object zombie, Vector3 from, out IRaidTarget target)
        {
            target = null;
            if (zombie == null) return false;

            Release(zombie);

            if (TryAssignDefense(zombie, from, out IDefenseObjective defense))
            {
                target = defense;
                return true;
            }

            // Over the wall with no defense wanting it: the yard, while the night still allows it.
            if (yard != null && yard.TryAssign(zombie, from, out OutdoorWreck wreck) && wreck != null)
            {
                target = wreck;
                return true;
            }

            return false;
        }

        bool TryAssignDefense(object zombie, Vector3 from, out IDefenseObjective chosen)
        {
            chosen = null;
            if (!IsActive || !CanKnockOutMore || defenses.Count == 0) return false;

            ZombieTargetingTuning rules = Rules;

            // Nearly at the door: it is the restaurant's now.
            if (distanceToRestaurant(from) <= rules.restaurantCommitMetres) return false;

            // Never most of the horde on the defenses at once.
            int ceiling = Mathf.Max(1, Mathf.FloorToInt(Mathf.Max(1, liveZombies()) * rules.maxShareOnDefenses));
            if (assigned.Count >= ceiling) return false;

            bool hunter = hunters.Contains(zombie);
            bool inside = isInside(from);
            int perDefense = rules.AttackersPerDefense(IsBigWave);
            refused.TryGetValue(zombie, out IDefenseObjective gaveUpOn);

            float best = float.MaxValue;

            for (int i = 0; i < defenses.Count; i++)
            {
                IDefenseObjective defense = defenses[i];
                if (defense == gaveUpOn || !defense.IsStanding || !CanReach(defense, from, inside)) continue;

                int attackers = AttackersOf(defense);
                if (attackers >= perDefense) continue;

                float distance = defense.FlatDistance(from);
                bool shootingAtIt = ReferenceEquals(defense.AimingAt, zombie);

                // A hunter goes for anything in sight; anyone else only for the gun shooting at it from close by.
                bool wants = hunter
                    ? distance <= rules.hunterSightMetres
                    : shootingAtIt && distance <= rules.provokedWithinMetres;
                if (!wants) continue;

                float score = distance + attackers * rules.crowdPenaltyMetres
                              - (shootingAtIt ? rules.shootingAtItBonusMetres : 0f)
                              - (defense.OnTheWall ? 0f : rules.exposedBonusMetres)
                              - (1f - Mathf.Clamp01(defense.HealthFraction)) * rules.damagedBonusMetres;

                if (score >= best) continue;

                best = score;
                chosen = defense;
            }

            if (chosen == null) return false;

            assigned[zombie] = chosen;

            if (announced.Add(chosen)) OnDefenseTargeted?.Invoke(chosen);
            return true;
        }

        /// <summary>
        /// A gun up on the wall from outside only, and only from in front of the stretch of wall it
        /// stands on - from round the corner the straight way to it runs into the wall beside it. One
        /// on the ground from its own side of the wall.
        /// </summary>
        bool CanReach(IDefenseObjective defense, Vector3 from, bool zombieInside)
        {
            if (!defense.OnTheWall) return isInside(defense.Position) == zombieInside;
            if (zombieInside) return false;

            Vector3 outward = defense.Outward;
            if (outward.sqrMagnitude < 1e-4f) return true;

            Vector3 offset = from - defense.Position;
            return offset.x * outward.x + offset.z * outward.z > 0f;
        }

        public bool StillWanted(object zombie, IRaidTarget target, Vector3 from)
        {
            if (zombie == null || target == null) return false;

            if (target is IDefenseObjective defense)
            {
                return IsActive && CanKnockOutMore && defense.IsStanding &&
                       assigned.TryGetValue(zombie, out IDefenseObjective held) && held == defense &&
                       CanReach(defense, from, isInside(from));
            }

            return yard != null && target is OutdoorWreck wreck && yard.StillWanted(zombie, wreck);
        }

        public bool Strike(object zombie, IRaidTarget target, float damage, Vector3 from)
        {
            if (!(target is IDefenseObjective defense))
                return yard == null || !(target is OutdoorWreck wreck) || yard.Strike(zombie, wreck, damage, from);

            if (!StillWanted(zombie, target, from)) return true;
            if (!defense.TakeHit(damage, from)) return false;

            KnockOut(defense);
            return true;
        }

        void KnockOut(IDefenseObjective defense)
        {
            KnockedOut++;

            // Nobody goes on hitting a defense that is down.
            ReleaseAllOn(defense);

            Debug.Log($"[Last Orders] Zombies knocked out the {defense.DisplayName.ToLowerInvariant()} " +
                      $"({KnockedOut} of at most {KnockOutCap} tonight).");

            OnDefenseKnockedOut?.Invoke(defense);

            if (CanKnockOutMore) return;

            // That was the last defense the night may take: everyone on one lets go for the restaurant.
            assigned.Clear();
            OnKnockOutCapReached?.Invoke();
        }

        public void GaveUp(object zombie, IRaidTarget target)
        {
            if (zombie == null) return;

            if (target is IDefenseObjective defense) refused[zombie] = defense;
            Release(zombie);
        }

        public void Release(object zombie)
        {
            if (zombie == null) return;

            assigned.Remove(zombie);
            yard?.Release(zombie);
        }

        /// <summary>Zombies on their way to <paramref name="defense"/>, or at it.</summary>
        public int AttackersOf(IDefenseObjective defense)
        {
            int count = 0;
            foreach (IDefenseObjective held in assigned.Values)
                if (held == defense) count++;

            return count;
        }

        void ReleaseAllOn(IDefenseObjective defense)
        {
            scratch.Clear();
            foreach (KeyValuePair<object, IDefenseObjective> entry in assigned)
                if (entry.Value == defense) scratch.Add(entry.Key);

            for (int i = 0; i < scratch.Count; i++) assigned.Remove(scratch[i]);
        }
    }
}
