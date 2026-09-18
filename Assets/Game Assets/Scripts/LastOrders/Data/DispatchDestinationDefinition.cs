using System;
using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>Risk band of a Dispatch destination. Drives outcome weighting, and the
    /// guarantee in 12.3 that Stranded is never possible on Low - so a cautious player is
    /// never blindsided.</summary>
    public enum DispatchRisk
    {
        Low = 0,
        Moderate = 1,
        High = 2,
    }

    /// <summary>The 12.3 outcome tiers. No tier removes anything permanently.</summary>
    public enum DispatchOutcomeTier
    {
        Windfall = 0,
        Success = 1,
        Partial = 2,
        Setback = 3,
        Stranded = 4,
    }

    /// <summary>
    /// A place beyond the walls. The world outside exists, matters, and supplies what the
    /// player cannot build, but it is narrated rather than played.
    ///
    /// 12.2 is the argument for this being the right shape: a destination is a data row
    /// plus two to four vignettes, so fifty destinations is a week of writing rather than
    /// six months of level design, and it scales in LiveOps as pure content patches.
    ///
    /// The real cost of a dispatch is never the risk. It is that the Survivor is gone:
    /// their labour absent from the kitchen, their body absent from the night watch.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Dispatch Destination", fileName = "Dispatch_")]
    public sealed class DispatchDestinationDefinition : ScriptableObject
    {
        [Serializable]
        public struct Outcome
        {
            [TextArea(2, 5)]
            [Tooltip("The vignette. 27 names writing quality as the entire mitigation for " +
                     "Dispatch reading as a menu - budget real time for these.")]
            public string narrative;

            public DispatchOutcomeTier tier;

            public ResourceAmount[] haul;

            public int reputationDelta;

            [Tooltip("Extra days the Survivor is away. Setback is 1-3, Stranded 3-5.")]
            public int extraDaysAway;

            [Tooltip("Relative likelihood within this destination.")]
            [Min(0f)] public float weight;
        }

        [Serializable]
        public struct Choice
        {
            [TextArea(2, 4)]
            [Tooltip("The situation. Exactly one meaningful choice per resolution.")]
            public string setup;

            public string optionALabel;
            public Outcome optionA;

            public string optionBLabel;
            public Outcome optionB;
        }

        [Header("Board entry")]
        public string title = "The Flooded Pharmacy";

        [TextArea(2, 4)]
        [Tooltip("The one-line hook shown on the Dispatch board card.")]
        public string teaser;

        public DispatchRisk risk = DispatchRisk.Moderate;

        [Tooltip("Days the assigned Survivor is away before resolving.")]
        [Min(1)] public int durationDays = 2;

        [Header("Expectations shown to the player")]
        public ResourceAmount[] likelyRewards;

        [Tooltip("Named on the card as a possibility, e.g. a Chem Blueprint.")]
        public string[] possibleRareRewards;

        [Header("Resolution")]
        [Tooltip("Outcomes rolled when there is no choice to present.")]
        public Outcome[] outcomes;

        [Tooltip("Vignettes with one meaningful choice. When any exist, one is drawn in " +
                 "preference to a plain outcome roll.")]
        public Choice[] choices;

        [Header("Availability")]
        public int earliestDay = 4;

        public int requiredReputation;

        [Tooltip("Can appear more than once per campaign.")]
        public bool repeatable = true;

        /// <summary>12.3: Stranded is never possible on Low risk. Enforced here rather than at
        /// the roll site so no future caller can forget it.</summary>
        public bool AllowsStranded => risk == DispatchRisk.High;

        void OnValidate()
        {
            if (risk == DispatchRisk.Low && outcomes != null)
            {
                for (int i = 0; i < outcomes.Length; i++)
                {
                    if (outcomes[i].tier == DispatchOutcomeTier.Stranded)
                    {
                        Debug.LogWarning(
                            $"[Dispatch] '{title}' is Low risk but outcome {i} is Stranded. " +
                            "12.3 guarantees a cautious player is never blindsided.", this);
                    }
                }
            }
        }
    }
}
