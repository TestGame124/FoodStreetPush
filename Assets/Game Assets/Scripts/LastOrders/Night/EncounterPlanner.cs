using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Night
{
    /// <summary>Everything one attack is decided from. Locked when the warning appears.</summary>
    public struct EncounterRequest
    {
        public int Day;

        /// <summary>Which attack of the campaign this is, from 1.</summary>
        public int EncounterNumber;

        public int TierIndex;
        public ThreatTierTuning Tier;

        /// <summary>Zombies across the whole attack, after every limit and any relief.</summary>
        public int Enemies;

        /// <summary>Sides of the compound the night can come from.</summary>
        public int LaneCount;

        public float Relief;
        public int Seed;
    }

    /// <summary>
    /// Turns a locked request into an attack: up to three short waves, each from a different side
    /// to the one before, each made of a few kinds of zombie that progression has already
    /// introduced and the threat band allows.
    ///
    /// A pure function of the request, like the old night director - nothing here touches
    /// UnityEngine.Random - so the attack the HUD previews in the morning is the attack that comes.
    /// </summary>
    public static class EncounterPlanner
    {
        const int Salt = 3131;

        /// <summary>The kinds of zombie an attack on <paramref name="day"/> may use within a band
        /// whose dearest kind costs <paramref name="maxThreatCost"/>, cheapest first. When nothing
        /// unlocked is cheap enough, the cheapest unlocked kind on its own, so a low band always
        /// has someone to send.</summary>
        public static List<EnemyDefinition> Unlocked(IReadOnlyList<EnemyDefinition> roster, int day, float maxThreatCost)
        {
            var eligible = new List<EnemyDefinition>();
            EnemyDefinition cheapest = null;

            if (roster != null)
            {
                for (int i = 0; i < roster.Count; i++)
                {
                    EnemyDefinition enemy = roster[i];

                    // A zombie that never attacks would only hold a wave open while it wanders about.
                    if (enemy == null || enemy.isNonCombatant || enemy.earliestDay > day) continue;
                    if (eligible.Contains(enemy)) continue;

                    if (cheapest == null || Cheaper(enemy, cheapest)) cheapest = enemy;
                    if (enemy.threatCost <= maxThreatCost + 1e-4f) eligible.Add(enemy);
                }
            }

            if (eligible.Count == 0 && cheapest != null) eligible.Add(cheapest);

            eligible.Sort((a, b) => Cheaper(a, b) ? -1 : Cheaper(b, a) ? 1 : 0);
            return eligible;
        }

        static bool Cheaper(EnemyDefinition a, EnemyDefinition b)
        {
            if (!Mathf.Approximately(a.threatCost, b.threatCost)) return a.threatCost < b.threatCost;
            return string.CompareOrdinal(a.displayName ?? a.name, b.displayName ?? b.name) < 0;
        }

        public static WavePlan Build(EncounterRequest request, EncounterTuning tuning, IReadOnlyList<EnemyDefinition> roster)
        {
            tuning ??= new EncounterTuning();
            ThreatTierTuning tier = request.Tier ?? tuning.Tier(request.TierIndex);

            var plan = new WavePlan
            {
                Tier = request.TierIndex,
                ThreatName = tier.name,
                EncounterNumber = request.EncounterNumber,
                Relief = request.Relief <= 0f ? 1f : request.Relief,
                LaneCount = Mathf.Max(1, request.LaneCount),
            };

            List<EnemyDefinition> kinds = Unlocked(roster, request.Day, tier.maxThreatCost);
            if (kinds.Count == 0 || request.Enemies <= 0) return plan;

            var rng = DeterministicRandom.ForNight(request.Day, request.EncounterNumber, request.Seed, Salt);

            int waves = Mathf.Clamp(tier.waves, 1, Mathf.Min(3, request.Enemies));
            int[] sizes = Split(request.Enemies, waves);
            List<int> sides = ShuffledLanes(plan.LaneCount, ref rng);
            var used = new HashSet<EnemyDefinition>();

            for (int w = 0; w < waves; w++)
            {
                var wave = new EncounterWave { Index = w };

                // A different side every wave; a second one, opposite-ish, when the band sends two.
                wave.Lanes.Add(sides[w % sides.Count]);
                if (tier.sidesPerWave >= 2 && sides.Count >= 2)
                {
                    int second = sides[(w + (sides.Count >= 3 ? 2 : 1)) % sides.Count];
                    if (!wave.Lanes.Contains(second)) wave.Lanes.Add(second);
                }

                PickKinds(kinds, Mathf.Max(1, tier.typesPerWave), w, used, wave.Kinds, ref rng);

                List<EnemyDefinition> members = Fill(wave.Kinds, sizes[w], ref rng);
                Schedule(wave, members, tuning, ref rng);

                for (int i = 0; i < wave.Spawns.Count; i++)
                {
                    plan.Spawns.Add(wave.Spawns[i]);
                    plan.Composition.Add(wave.Spawns[i].Enemy);
                }

                plan.Groups.AddRange(wave.Groups);
                plan.Waves.Add(wave);
            }

            // An upper bound, for anything that still reads the night by its length.
            plan.DurationSeconds = waves * tuning.waveTimeoutSeconds;
            return plan;
        }

        /// <summary>Shares the zombies across the waves, the later ones a little bigger: an
        /// attack that builds, rather than one that opens with everything it has.</summary>
        public static int[] Split(int enemies, int waves)
        {
            waves = Mathf.Max(1, waves);
            var sizes = new int[waves];

            float weightSum = 0f;
            for (int w = 0; w < waves; w++) weightSum += 1f + 0.2f * w;

            int given = 0;
            for (int w = 0; w < waves; w++)
            {
                sizes[w] = Mathf.Max(1, Mathf.FloorToInt(enemies * (1f + 0.2f * w) / weightSum));
                given += sizes[w];
            }

            // Whatever rounding left over goes on the end, the biggest wave; any overshoot from
            // the one-each minimum comes off the front.
            for (int w = waves - 1; given < enemies; w = (w - 1 + waves) % waves, given++) sizes[w]++;
            for (int w = 0; given > enemies && w < waves; w++)
            {
                while (sizes[w] > 1 && given > enemies)
                {
                    sizes[w]--;
                    given--;
                }
            }

            return sizes;
        }

        static List<int> ShuffledLanes(int count, ref DeterministicRandom rng)
        {
            var lanes = new List<int>(count);
            for (int i = 0; i < count; i++) lanes.Add(i);

            for (int i = lanes.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (lanes[i], lanes[j]) = (lanes[j], lanes[i]);
            }

            return lanes;
        }

        /// <summary>
        /// The kinds in one wave. The first wave always leads with the most basic kind, so an
        /// attack opens on something familiar; after that each wave prefers kinds not yet seen
        /// tonight, which is where the variety of a higher band comes from.
        /// </summary>
        static void PickKinds(List<EnemyDefinition> kinds, int wanted, int wave, HashSet<EnemyDefinition> used,
                              List<EnemyDefinition> into, ref DeterministicRandom rng)
        {
            wanted = Mathf.Min(wanted, kinds.Count);

            if (wave == 0)
            {
                into.Add(kinds[0]);
                used.Add(kinds[0]);
            }

            int guard = 0;
            while (into.Count < wanted && guard++ < 64)
            {
                var weights = new KindWeights(kinds, into, used);
                int index = rng.WeightedIndex(weights);
                if (index < 0) break;

                into.Add(kinds[index]);
                used.Add(kinds[index]);
            }
        }

        /// <summary>One of each kind first, then the rest weighted toward the cheaper kinds, and
        /// the order shuffled so the tough ones are not always first through the gap.</summary>
        static List<EnemyDefinition> Fill(List<EnemyDefinition> kinds, int count, ref DeterministicRandom rng)
        {
            var members = new List<EnemyDefinition>(count);

            for (int i = 0; i < kinds.Count && members.Count < count; i++) members.Add(kinds[i]);

            var weights = new KindWeights(kinds, null, null);
            while (members.Count < count)
            {
                int index = rng.WeightedIndex(weights);
                members.Add(kinds[index < 0 ? 0 : index]);
            }

            for (int i = members.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (members[i], members[j]) = (members[j], members[i]);
            }

            return members;
        }

        /// <summary>Knots of two or three, spread over the wave's arrival window and across its
        /// sides, each member a second or so behind the one before.</summary>
        static void Schedule(EncounterWave wave, List<EnemyDefinition> members, EncounterTuning tuning,
                             ref DeterministicRandom rng)
        {
            int smallest = Mathf.Max(1, tuning.groupSize.x);
            int largest = Mathf.Max(smallest, tuning.groupSize.y);
            float stagger = Mathf.Max(0f, tuning.groupStaggerSeconds);

            var sizes = new List<int>();
            for (int left = members.Count; left > 0;)
            {
                int size = Mathf.Min(rng.Range(smallest, largest + 1), left);
                sizes.Add(size);
                left -= size;
            }

            float window = Mathf.Max(0f, tuning.waveArrivalSeconds);
            int next = 0;

            for (int g = 0; g < sizes.Count; g++)
            {
                float start = sizes.Count == 1 ? 0f : window * g / (sizes.Count - 1);
                int lane = wave.Lanes[g % wave.Lanes.Count];

                wave.Groups.Add(new WaveGroup(start, lane, sizes[g], members[next]));

                for (int k = 0; k < sizes[g]; k++)
                    wave.Spawns.Add(new WaveSpawn(members[next + k], start + stagger * k, lane));

                next += sizes[g];
            }

            wave.Spawns.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
        }

        /// <summary>Cheaper kinds weigh more. Kinds already in the wave weigh nothing; kinds
        /// already seen tonight weigh less, so later waves reach for something new.</summary>
        sealed class KindWeights : IReadOnlyWeights
        {
            readonly List<EnemyDefinition> kinds;
            readonly List<EnemyDefinition> taken;
            readonly HashSet<EnemyDefinition> seen;

            public KindWeights(List<EnemyDefinition> kinds, List<EnemyDefinition> taken, HashSet<EnemyDefinition> seen)
            {
                this.kinds = kinds;
                this.taken = taken;
                this.seen = seen;
            }

            public int Count => kinds.Count;

            public float WeightAt(int index)
            {
                EnemyDefinition kind = kinds[index];
                if (taken != null && taken.Contains(kind)) return 0f;

                float weight = 1f / Mathf.Max(0.25f, kind.threatCost);
                if (seen != null && seen.Contains(kind)) weight *= 0.35f;

                return weight;
            }
        }
    }
}
