using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// Every number in the Heat system (GDD 11.1 and 11.2), in one asset.
    ///
    /// Heat is the design's load-bearing system: it is what binds the builder to the
    /// defense game, and without it the two halves are separate games sharing a currency.
    /// It is also the thing most likely to need fifty tuning passes. Both facts point the
    /// same way, so none of these values are constants in code.
    ///
    /// The defaults below are the GDD's tables verbatim. Treat them as a starting point
    /// that survived design review, not as balance.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Config/Heat Weights", fileName = "HeatWeights")]
    public sealed class HeatWeightsConfig : ScriptableObject
    {
        [Header("Sources (11.1)")]
        [Tooltip("Per customer served. The core tension: every customer is money and Reputation AND Heat.")]
        public float perCustomerServed = 2f;

        [Tooltip("Per dish cooked. Grilled and smoked dishes multiply this via the recipe's own field.")]
        public float perDishCooked = 1f;

        [Tooltip("Per powered light still burning after dusk.")]
        public float perPoweredLightAfterDusk = 3f;

        [Tooltip("Per running generator.")]
        public float perGeneratorRunning = 5f;

        [Tooltip("Per playing jukebox. Good music is a risk.")]
        public float perJukeboxPlaying = 4f;

        [Tooltip("Per occupied tile. This is why expansion is never free (6.2).")]
        public float perStructureTile = 0.5f;

        [Tooltip("Per corpse left unburied overnight.")]
        public float perUnburiedCorpse = 2f;

        [Tooltip("Per corrugated wall segment.")]
        public float perCorrugatedSegment = 1f;

        [Header("Reputation tier contribution (11.1)")]
        [Tooltip("Flat Heat added by the player's Reputation tier, indexed by tier 0-3 " +
                 "(Reputation 0 / 25 / 50 / 75). Success itself is dangerous.")]
        public float[] reputationTierHeat = { 5f, 15f, 30f, 50f };

        [Header("Reductions (11.2) - every one of these costs revenue")]
        [Tooltip("Blackout at dusk. Costs all turrets and floodlights for the night.")]
        public float blackoutReduction = -20f;

        [Tooltip("Smoke scrubber, applied against cooking Heat only. Costs 6 tiles, Parts and upkeep.")]
        public float smokeScrubberCookingReduction = -10f;

        [Tooltip("Closing early in the last 15 clock-minutes. Forfeits peak revenue.")]
        public float closeEarlyReduction = -10f;

        [Tooltip("Per corpse buried. Costs one clock-minute each.")]
        public float buryCorpseReduction = -2f;

        [Tooltip("Decoy fire. Consumable, one per night.")]
        public float decoyFireReduction = -15f;

        [Tooltip("Per customer refused at the door. Also costs 1 Reputation each. " +
                 "Open question 28.3: verify this does not become the dominant strategy.")]
        public float refuseCustomerReduction = -2f;

        [Header("Forecast")]
        [Tooltip("Heat thresholds separating wave tiers. The forecast marker on the Heat bar " +
                 "shows which band tonight currently lands in. Must be ascending.")]
        public float[] waveTierThresholds = { 20f, 45f, 80f, 130f, 200f };

        /// <summary>
        /// Which wave tier a given Heat lands in. The forecast marker and the WaveDirector
        /// both read this, so what the player is shown and what they get cannot diverge.
        /// </summary>
        public int TierForHeat(float heat)
        {
            if (waveTierThresholds == null || waveTierThresholds.Length == 0) return 0;

            for (int i = 0; i < waveTierThresholds.Length; i++)
                if (heat < waveTierThresholds[i]) return i;

            return waveTierThresholds.Length;
        }

        public float ReputationTierHeat(int tier)
        {
            if (reputationTierHeat == null || reputationTierHeat.Length == 0) return 0f;
            return reputationTierHeat[Mathf.Clamp(tier, 0, reputationTierHeat.Length - 1)];
        }

        void OnValidate()
        {
            // An unsorted threshold array silently breaks the forecast rather than erroring,
            // which would be a miserable bug to find by playing.
            if (waveTierThresholds == null) return;
            for (int i = 1; i < waveTierThresholds.Length; i++)
            {
                if (waveTierThresholds[i] <= waveTierThresholds[i - 1])
                {
                    Debug.LogWarning(
                        $"[HeatWeights] waveTierThresholds must ascend. Entry {i} " +
                        $"({waveTierThresholds[i]}) is not greater than entry {i - 1} " +
                        $"({waveTierThresholds[i - 1]}).", this);
                    break;
                }
            }
        }
    }
}
