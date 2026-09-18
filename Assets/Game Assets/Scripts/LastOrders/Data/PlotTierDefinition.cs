using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// One rung of the 6.2 expansion ladder. The grid itself is the progression track.
    ///
    /// The rule that makes expansion interesting: new tiles arrive OUTSIDE the current
    /// perimeter, so the player's sealed walls become interior walls with a hole in them,
    /// and the defensive plan has to be rebuilt while the restaurant keeps running.
    /// Expansion nights are among the hardest in the game, and expansion permanently raises
    /// Heat through footprint. Growth is never free.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Plot Tier", fileName = "Tier_")]
    public sealed class PlotTierDefinition : ScriptableObject
    {
        [Tooltip("1 through 5. The Shack, The Lot, The Corner, The Block, The Compound.")]
        [Min(1)] public int tier = 1;

        public string displayName = "The Shack";

        [TextArea(2, 3)]
        [Tooltip("How the ground is taken: clear the rubble east, reclaim the storefront, " +
                 "wall in the alley, take the parking lot.")]
        public string unlockNarrative;

        [Header("Grid")]
        [Tooltip("Build-tile dimensions. GDD: 8x8, 10x10, 12x12, 14x12, 16x14.")]
        [Min(4)] public int gridWidth = 8;

        [Min(4)] public int gridHeight = 8;

        [Header("Gate")]
        [Tooltip("Reputation required. GDD: -, 20, 40, 60, 80.")]
        public int requiredReputation;

        public ResourceAmount[] unlockCost;

        [Header("Direction")]
        [Tooltip("6.2 fixes expansion east and south, away from the highway, so the north wall " +
                 "stays the front of house across all five tiers. The perimeter that matters " +
                 "most never moves - which is what stops expansion from invalidating the " +
                 "player's whole mental model each time.")]
        public Vector2Int expansionOffset = new Vector2Int(0, 0);

        /// <summary>Tiles gained over the previous tier. Feeds the permanent footprint Heat
        /// that makes growth cost something forever.</summary>
        public int TileCount => gridWidth * gridHeight;
    }
}
