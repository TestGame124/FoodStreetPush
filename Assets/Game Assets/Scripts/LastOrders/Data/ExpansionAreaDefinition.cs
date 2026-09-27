using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// A piece of land the restaurant can reclaim: cleared of debris first, then expanded into
    /// for a price, which builds its new boundary.
    ///
    /// The design's rule is that only land touching the current boundary can be worked. That
    /// is expressed as <see cref="unlockedBy"/>: an area with nothing listed borders the land
    /// the game starts with, and any other becomes workable once one of the areas it lists has
    /// been claimed. One direction only, so there is no pair of lists to keep in step.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Expansion/Area", fileName = "ExpansionArea_")]
    public sealed class ExpansionAreaDefinition : ScriptableObject
    {
        [Tooltip("Stable identity. Recorded in progression flags, so renaming it forgets the claim.")]
        public string id = "area";

        public string displayName = "Area";

        [Tooltip("What the land becomes once claimed. Shown to the player.")]
        [TextArea] public string benefit;

        [Tooltip("Claiming any one of these makes this area workable. Empty: it borders the " +
                 "starting land and is workable from the start.")]
        public ExpansionAreaDefinition[] unlockedBy;

        [Header("Expanding")]
        [Tooltip("Spent when the player starts the expansion, once the debris is gone.")]
        public ResourceAmount[] expandCost;

        [Tooltip("Player level needed to start the expansion. Zero: no level needed.")]
        [Min(0)] public int requiredLevel;

        [Tooltip("Seconds the new boundary takes to build once started.")]
        [Min(0.1f)] public float expandSeconds = 12f;

        [Tooltip("Paid once, when the expansion finishes and the land is claimed.")]
        public RewardBundle claimReward = new RewardBundle(5);

        public bool BordersStartingLand => unlockedBy == null || unlockedBy.Length == 0;
    }
}
