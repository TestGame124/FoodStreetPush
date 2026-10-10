using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Night
{
    /// <summary>One scheduled arrival. A wave is just an ordered list of these.</summary>
    public readonly struct WaveSpawn
    {
        public readonly EnemyDefinition Enemy;

        /// <summary>Seconds into the night this one arrives.</summary>
        public readonly float TimeSeconds;

        /// <summary>Which telegraphed approach lane it uses. Lanes are shown during the
        /// 45-second dusk warning, so this must be decided before the player stops being
        /// able to reposition.</summary>
        public readonly int Lane;

        public WaveSpawn(EnemyDefinition enemy, float timeSeconds, int lane)
        {
            Enemy = enemy;
            TimeSeconds = timeSeconds;
            Lane = lane;
        }
    }

    /// <summary>
    /// A handful of arrivals that come up the same lane together. The unit the player is warned
    /// about: "three coming up the road" is something to act on, where a warning per zombie is
    /// noise.
    /// </summary>
    public readonly struct WaveGroup
    {
        /// <summary>Seconds into the night the first of them arrives.</summary>
        public readonly float TimeSeconds;

        public readonly int Lane;
        public readonly int Count;

        /// <summary>The first of them, for naming the group in a warning.</summary>
        public readonly EnemyDefinition Leader;

        public WaveGroup(float timeSeconds, int lane, int count, EnemyDefinition leader)
        {
            TimeSeconds = timeSeconds;
            Lane = lane;
            Count = count;
            Leader = leader;
        }

        /// <summary>"3 zombies", for a warning. Whatever the archetypes, the player's word for
        /// all of them is zombies.</summary>
        public string Label => Count == 1 ? "A zombie" : $"{Count} zombies";
    }

    /// <summary>A fully resolved night, produced at dusk and consumed by NightState.</summary>
    public sealed class WavePlan
    {
        public readonly List<WaveSpawn> Spawns = new List<WaveSpawn>();

        /// <summary>The same arrivals, as the groups they come in, in the order they arrive.</summary>
        public readonly List<WaveGroup> Groups = new List<WaveGroup>();
        public float DurationSeconds;
        public int LaneCount;
        public int Tier;
        public float Heat;
        public NamedNightDefinition NamedNight;

        /// <summary>Distinct archetypes in this wave. What the Threat Profile broadcasts.</summary>
        public readonly HashSet<EnemyDefinition> Composition = new HashSet<EnemyDefinition>();

        /// <summary>
        /// A zombie attack's short waves, in order. When there are any, the night runs them one
        /// after another - each sets off once the last is beaten - instead of releasing
        /// <see cref="Spawns"/> on a clock. <see cref="Spawns"/> and <see cref="Groups"/> still list
        /// every arrival, for anything that only wants to count them.
        /// </summary>
        public readonly List<EncounterWave> Waves = new List<EncounterWave>();

        /// <summary>The threat band's name: Low, Medium, High.</summary>
        public string ThreatName;

        /// <summary>Which attack of the campaign this is, from 1.</summary>
        public int EncounterNumber;

        /// <summary>Share of the zombies sent after a run of defeats. One when nothing was taken off.</summary>
        public float Relief = 1f;

        public bool IsEncounter => Waves.Count > 0;

        public int EnemyCount => Spawns.Count;
    }

    /// <summary>
    /// One short wave of an attack: who comes, from which sides, and when each sets off -
    /// counted from the moment the wave itself begins, not from nightfall.
    /// </summary>
    public sealed class EncounterWave
    {
        public int Index;

        /// <summary>The lanes - the sides of the compound - this wave comes from.</summary>
        public readonly List<int> Lanes = new List<int>();

        public readonly List<WaveSpawn> Spawns = new List<WaveSpawn>();
        public readonly List<WaveGroup> Groups = new List<WaveGroup>();

        /// <summary>The kinds of zombie in it.</summary>
        public readonly List<EnemyDefinition> Kinds = new List<EnemyDefinition>();

        public int Count => Spawns.Count;
    }

    /// <summary>
    /// Turns the Heat locked at dusk into tonight's wave.
    ///
    /// The single most important property is determinism: the plan is a pure function of
    /// (day, Heat, seed). Nothing here touches UnityEngine.Random, whose state is global and
    /// shared with every particle system in the scene.
    ///
    /// That is not a performance concern, it is a correctness one. 16.1 has the radio
    /// broadcast a Threat Profile at dawn describing tonight's composition, and the player
    /// builds against it all day. If the wave were not reproducible, the forecast would be a
    /// guess the game makes about itself, and would eventually lie. Determinism is what lets
    /// the promise be kept - and 24.4 warns that retrofitting it is painful, so it is here
    /// from the first line.
    /// </summary>
    public sealed class WaveDirector
    {
        readonly GameBalanceConfig balance;
        readonly HeatWeightsConfig heatWeights;

        public WaveDirector(GameBalanceConfig balance, HeatWeightsConfig heatWeights)
        {
            this.balance = balance;
            this.heatWeights = heatWeights;
        }

        /// <summary>
        /// Builds the plan. Called twice per day with identical arguments and expected to
        /// return identical results: once at dawn to generate the forecast, once at dusk for
        /// real. Any hidden state here would show up as the radio being wrong.
        /// </summary>
        public WavePlan BuildPlan(int day, float heat)
        {
            var plan = new WavePlan
            {
                Heat = heat,
                Tier = heatWeights.TierForHeat(heat),
            };

            plan.NamedNight = FindNamedNight(day);

            WaveManifest manifest = plan.NamedNight != null && plan.NamedNight.waveOverride != null
                ? plan.NamedNight.waveOverride
                : SelectManifest(plan.Tier, day, heat);

            if (manifest == null) return plan;

            plan.DurationSeconds = manifest.nightDurationSeconds;
            plan.LaneCount = Mathf.Max(1, manifest.approachLanes);

            if (plan.NamedNight != null && plan.NamedNight.doubleLength)
                plan.DurationSeconds *= 2f;

            var rng = DeterministicRandom.ForNight(day, Mathf.RoundToInt(heat), balance.campaignSeed);

            float budget = Mathf.Max(manifest.minimumBudget, heat * manifest.budgetPerHeat);

            var picked = new List<EnemyDefinition>();
            FillWave(manifest, day, budget, picked, ref rng);
            ScheduleGroups(plan, manifest, picked, ref rng);

            plan.Spawns.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
            plan.Groups.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
            return plan;
        }

        NamedNightDefinition FindNamedNight(int day)
        {
            if (balance.namedNights == null) return null;

            for (int i = 0; i < balance.namedNights.Length; i++)
                if (balance.namedNights[i] != null && balance.namedNights[i].day == day)
                    return balance.namedNights[i];

            return null;
        }

        WaveManifest SelectManifest(int tier, int day, float heat)
        {
            if (balance.waveManifests == null || balance.waveManifests.Length == 0) return null;

            var candidates = new List<WaveManifest>();
            for (int i = 0; i < balance.waveManifests.Length; i++)
            {
                WaveManifest manifest = balance.waveManifests[i];
                if (manifest != null && manifest.AppliesTo(tier, day)) candidates.Add(manifest);
            }

            if (candidates.Count == 0) return null;

            // Salted so that picking the manifest draws from a different stream than filling
            // it. Otherwise the choice of manifest would correlate with its contents, and
            // successive nights at similar Heat would feel like the same night twice.
            var rng = DeterministicRandom.ForNight(day, Mathf.RoundToInt(heat), balance.campaignSeed, 7717);
            return candidates[rng.Range(0, candidates.Count)];
        }

        /// <summary>Chooses who comes tonight, into <paramref name="picked"/>, spending the budget.
        /// When they come is decided afterwards, by <see cref="ScheduleGroups"/>.</summary>
        void FillWave(WaveManifest manifest, int day, float budget, List<EnemyDefinition> picked,
                      ref DeterministicRandom rng)
        {
            if (manifest.entries == null || manifest.entries.Length == 0) return;

            var eligible = new List<WaveManifest.Entry>();
            for (int i = 0; i < manifest.entries.Length; i++)
            {
                WaveManifest.Entry entry = manifest.entries[i];
                if (entry.enemy == null) continue;
                if (day < entry.enemy.earliestDay) continue;
                eligible.Add(entry);
            }

            if (eligible.Count == 0) return;

            var counts = new Dictionary<EnemyDefinition, int>();

            // Guaranteed minimums first. These are what make an honest forecast possible: if
            // the radio said Burners, a Burner has to turn up even on a low-Heat night that
            // could not otherwise afford one.
            for (int i = 0; i < eligible.Count; i++)
            {
                for (int n = 0; n < eligible[i].guaranteedMinimum; n++)
                {
                    Pick(eligible[i].enemy, picked, counts);
                    budget -= eligible[i].enemy.threatCost;
                }
            }

            var weights = new EntryWeights(eligible, counts);

            int guard = 0;
            while (budget > 0f && guard++ < 512)
            {
                int index = rng.WeightedIndex(weights);
                if (index < 0) break;

                EnemyDefinition enemy = eligible[index].enemy;
                if (enemy.threatCost > budget) break;

                Pick(enemy, picked, counts);
                budget -= enemy.threatCost;
            }
        }

        static void Pick(EnemyDefinition enemy, List<EnemyDefinition> picked, Dictionary<EnemyDefinition, int> counts)
        {
            counts.TryGetValue(enemy, out int existing);
            counts[enemy] = existing + 1;
            picked.Add(enemy);
        }

        /// <summary>
        /// Splits tonight's arrivals into small groups and gives each group one time and one lane.
        ///
        /// Zombies drawn in by the lights come in knots of two to four rather than as a steady
        /// drip, and a group is the thing the player gets warned about. Members of a group follow
        /// each other in a second or so apart, up the same road.
        ///
        /// The group's time is drawn uniformly then reshaped by the manifest's curve, so a
        /// manifest that wants a late surge gets one without needing bespoke scheduling.
        /// </summary>
        static void ScheduleGroups(WavePlan plan, WaveManifest manifest, List<EnemyDefinition> picked,
                                   ref DeterministicRandom rng)
        {
            int smallest = Mathf.Max(1, manifest.groupSize.x);
            int largest = Mathf.Max(smallest, manifest.groupSize.y);
            float stagger = Mathf.Max(0f, manifest.groupStaggerSeconds);
            int lanes = Mathf.Max(1, plan.LaneCount);

            int next = 0;
            while (next < picked.Count)
            {
                int size = Mathf.Min(rng.Range(smallest, largest + 1), picked.Count - next);

                float t = rng.NextFloat();
                float shaped = manifest.spawnRateOverNight != null
                    ? Mathf.Clamp01(1f - manifest.spawnRateOverNight.Evaluate(t))
                    : t;

                // Early enough that the whole group is on the road before the night is over.
                float latest = Mathf.Max(0f, plan.DurationSeconds - stagger * (size - 1));
                float start = shaped * latest;
                int lane = rng.Range(0, lanes);

                plan.Groups.Add(new WaveGroup(start, lane, size, picked[next]));

                for (int k = 0; k < size; k++)
                {
                    EnemyDefinition enemy = picked[next + k];
                    plan.Spawns.Add(new WaveSpawn(enemy, start + stagger * k, lane));
                    plan.Composition.Add(enemy);
                }

                next += size;
            }
        }

        /// <summary>Adapts the manifest's entries to the weighting interface, applying each
        /// entry's maximum so a cheap archetype cannot flood the whole budget.</summary>
        sealed class EntryWeights : IReadOnlyWeights
        {
            readonly List<WaveManifest.Entry> entries;
            readonly Dictionary<EnemyDefinition, int> counts;

            public EntryWeights(List<WaveManifest.Entry> entries, Dictionary<EnemyDefinition, int> counts)
            {
                this.entries = entries;
                this.counts = counts;
            }

            public int Count => entries.Count;

            public float WeightAt(int index)
            {
                WaveManifest.Entry entry = entries[index];
                if (entry.maximum <= 0) return entry.weight;

                counts.TryGetValue(entry.enemy, out int used);
                return used >= entry.maximum ? 0f : entry.weight;
            }
        }
    }
}
