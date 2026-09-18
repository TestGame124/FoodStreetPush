using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// One row of the 6.4 wall table.
    ///
    /// The 14.1 time-to-breach analysis concludes that the answer is never "thicker wall"
    /// alone. It is turrets killing attackers before they finish. These numbers exist to
    /// make that true: wood is a speed bump, brick buys real time, and massed Brutes go
    /// through anything.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Wall Material", fileName = "Wall_")]
    public sealed class WallMaterialDefinition : ScriptableObject
    {
        public string displayName = "Scrap wood";

        [Tooltip("Hit points per segment. GDD: wood 100, corrugated 180, brick 350, reinforced 600.")]
        public float health = 100f;

        public ResourceAmount[] cost;

        [Tooltip("Clock-minutes to build one segment. The real cost of a wall is the day it eats.")]
        public float buildClockMinutes = 2f;

        [Tooltip("Burns. Burners exist specifically to punish cheap wood spam.")]
        public bool flammable = true;

        public bool fireproof;

        [Tooltip("Heat added per segment. Corrugated adds 1, which is the cost of its HP.")]
        public float heatPerSegment;

        [Tooltip("Plot tier required before this material can be built. Reinforced is tier 4+.")]
        public int requiredPlotTier = 1;

        public Material visual;
    }
}
