using System;
using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// The shape of one night. Pure data, consumed by the WaveDirector at dusk.
    ///
    /// A manifest does not name exact counts. It names a composition and lets the director
    /// fill it against the Heat budget, so one asset covers the whole range of nights a
    /// player might bring to it. Roughly thirty of these cover the campaign.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Wave Manifest", fileName = "Wave_")]
    public sealed class WaveManifest : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public EnemyDefinition enemy;

            [Tooltip("Relative likelihood of spending budget on this archetype. Zero excludes it.")]
            [Min(0f)] public float weight;

            [Tooltip("Always spawn at least this many, budget permitting. Used to guarantee a " +
                     "Threat Profile forecast comes true - if the radio says Burners, at least " +
                     "one Burner has to arrive.")]
            [Min(0)] public int guaranteedMinimum;

            [Tooltip("Never spawn more than this many. Zero means no cap.")]
            [Min(0)] public int maximum;
        }

        [Header("Applicability")]
        [Tooltip("Lowest wave tier this manifest may be drawn for. Tiers come from the Heat " +
                 "thresholds in HeatWeightsConfig.")]
        public int minTier;

        [Tooltip("Highest wave tier this manifest may be drawn for.")]
        public int maxTier = 5;

        [Tooltip("Earliest campaign day this may be drawn.")]
        public int earliestDay = 1;

        [Header("Composition")]
        public Entry[] entries;

        [Header("Pacing")]
        [Tooltip("Night length in real seconds. 13.1: 2 to 4 minutes.")]
        [Range(60f, 300f)] public float nightDurationSeconds = 150f;

        [Tooltip("How the wave's spawns distribute across the night. Time on X (0 to 1 of the " +
                 "night), relative spawn rate on Y. A flat curve is a steady grind; a curve " +
                 "that peaks late gives the player a chance to be nearly finished and then " +
                 "tested, which is what 15.7's near-miss tuning wants.")]
        public AnimationCurve spawnRateOverNight = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Tooltip("How many distinct map-edge lanes this wave arrives from. Telegraphed during " +
                 "the 45-second dusk warning, so the player can reposition before it matters.")]
        [Range(1, 6)] public int approachLanes = 2;

        [Header("Budget")]
        [Tooltip("Threat budget per point of Heat. The director spends Heat x this on entries, " +
                 "paying each archetype's threatCost. This is the single dial that decides how " +
                 "hard a given Heat feels.")]
        public float budgetPerHeat = 0.35f;

        [Tooltip("Floor on the budget, so a near-zero-Heat night after a disaster still has a " +
                 "token wave rather than nothing at all. 11.3: a wrecked restaurant serves few " +
                 "customers, so Heat is low, so the next night is survivable. It should still " +
                 "be a night.")]
        public float minimumBudget = 3f;

        public bool AppliesTo(int tier, int day) =>
            tier >= minTier && tier <= maxTier && day >= earliestDay;
    }
}
