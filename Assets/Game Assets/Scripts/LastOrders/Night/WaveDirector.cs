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

    /// <summary>A fully resolved night, produced at dusk and consumed by NightState.</summary>
    public sealed class WavePlan
    {
        public readonly List<WaveSpawn> Spawns = new List<WaveSpawn>();
        public float DurationSeconds;
        public int LaneCount;
        public int Tier;
        public float Heat;
        public NamedNightDefinition NamedNight;

        /// <summary>Distinct archetypes in this wave. What the Threat Profile broadcasts.</summary>
        public readonly HashSet<EnemyDefinition> Composition = new HashSet<EnemyDefinition>();
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
            FillWave(plan, manifest, day, budget, ref rng);

            plan.Spawns.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
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

        void FillWave(WavePlan plan, WaveManifest manifest, int day, float budget, ref DeterministicRandom rng)
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
                    AddSpawn(plan, eligible[i].enemy, counts, ref rng, manifest);
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

                AddSpawn(plan, enemy, counts, ref rng, manifest);
                budget -= enemy.threatCost;
            }
        }

        void AddSpawn(WavePlan plan, EnemyDefinition enemy, Dictionary<EnemyDefinition, int> counts,
                      ref DeterministicRandom rng, WaveManifest manifest)
        {
            counts.TryGetValue(enemy, out int existing);
            counts[enemy] = existing + 1;

            // Spawn time is drawn uniformly then reshaped by the manifest's curve, so a
            // manifest that wants a late surge gets one without needing bespoke scheduling.
            float t = rng.NextFloat();
            float shaped = manifest.spawnRateOverNight != null
                ? Mathf.Clamp01(1f - manifest.spawnRateOverNight.Evaluate(t))
                : t;

            float time = shaped * plan.DurationSeconds;
            int lane = rng.Range(0, Mathf.Max(1, plan.LaneCount));

            plan.Spawns.Add(new WaveSpawn(enemy, time, lane));
            plan.Composition.Add(enemy);
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
