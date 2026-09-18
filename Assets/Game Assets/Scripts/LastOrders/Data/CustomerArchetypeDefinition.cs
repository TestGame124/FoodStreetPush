using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// One row of the 9.6 archetype table.
    ///
    /// The clientele visibly improving as Reputation climbs is described in the GDD as "a
    /// strong, free progression signal", and it is free precisely because it is expressed
    /// as weights per Reputation tier on these assets rather than as spawn logic.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Customer Archetype", fileName = "Customer_")]
    public sealed class CustomerArchetypeDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Drifter";

        public GameObject prefab;

        public int poolPrewarm = 8;

        [Header("Party")]
        [Min(1)] public int minPartySize = 1;

        [Tooltip("Table sizes are 2, 4 and 6, so a 4-party at a 6-top wastes capacity. That " +
                 "mismatch is what makes table mix a real optimisation (9.5).")]
        [Min(1)] public int maxPartySize = 1;

        [Header("Economy")]
        public int baseCoinPayment = 18;

        [Tooltip("Tipped on top of payment. Traders pay in Scrap and Parts, Soldiers in Ammo.")]
        public ResourceAmount[] tips;

        [Tooltip("Chance of tipping a Blueprint or Recipe. Rare by design: these are a " +
                 "progression currency (18.1), not an income stream.")]
        [Range(0f, 1f)] public float rareDropChance;

        [Header("Service")]
        [Tooltip("Clock-minutes this archetype waits at each stage before giving up. Envoys " +
                 "are very low and a big swing; Regulars are very high.")]
        public float patienceClockMinutes = 6f;

        public float reputationOnSatisfied = 1f;

        [Tooltip("Lost when this party leaves unserved. Applied on queue timeout and refusal.")]
        public float reputationOnFailed = 2f;

        [Header("Progression")]
        [Tooltip("Relative spawn weight per Reputation tier (0-3, i.e. Reputation 0/25/50/75). " +
                 "Drifters weight early and fall off; Envoys are zero until tier 2.")]
        public float[] weightByReputationTier = { 1f, 1f, 1f, 1f };

        [Tooltip("Returns daily once served. The Regular's compounding value.")]
        public bool becomesRegular;

        [Tooltip("May offer to stay as a Survivor after good service. 9.7 makes service quality " +
                 "the primary staffing source, which is what ties the day loop to the night loop.")]
        [Range(0f, 1f)] public float recruitChance;

        [Tooltip("Pays nothing and costs Medicine, but gives a large Reputation gain and may " +
                 "join. The Wounded.")]
        public bool isWounded;

        public ResourceAmount[] treatmentCost;

        [Header("Heat")]
        [Tooltip("Multiplies the per-customer Heat. A six-person family is more Heat than a " +
                 "lone Drifter.")]
        public float heatMultiplier = 1f;

        public float WeightForTier(int tier)
        {
            if (weightByReputationTier == null || weightByReputationTier.Length == 0) return 1f;
            return weightByReputationTier[Mathf.Clamp(tier, 0, weightByReputationTier.Length - 1)];
        }

        void OnValidate()
        {
            maxPartySize = Mathf.Max(minPartySize, maxPartySize);
        }
    }
}
