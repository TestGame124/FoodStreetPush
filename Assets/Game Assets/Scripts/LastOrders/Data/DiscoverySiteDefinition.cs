using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>What a discovery site gives the player once it is found.</summary>
    public enum DiscoveryFeature
    {
        Takeaway,
        FoodTruck,
        ShootingRange,
        NoiseMaker,
    }

    /// <summary>
    /// A place left standing out on the land the restaurant can expand into - an abandoned
    /// drive-through booth, a stranded food truck - that is found, and put back to work, when the
    /// plot it stands on is claimed.
    ///
    /// It sits on land that has to be cleared and paid for first, so it is a reason to expand
    /// in one direction rather than another: the player can see the old booth by the road long
    /// before they can use it.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Expansion/Discovery Site", fileName = "Discovery_")]
    public sealed class DiscoverySiteDefinition : ScriptableObject
    {
        [Tooltip("Stable identity. Recorded in progression flags, so renaming it forgets the discovery.")]
        public string id = "site";

        [Tooltip("What it is, as found: \"Abandoned drive-through booth\".")]
        public string displayName = "Site";

        [Tooltip("What it becomes once restored: \"Takeaway service\".")]
        public string featureName = "Feature";

        [Tooltip("What the player can now do, in a sentence or two. Shown when it is found.")]
        [TextArea] public string featureBlurb;

        public DiscoveryFeature feature;

        [Tooltip("The plot whose claiming discovers it.")]
        public ExpansionAreaDefinition area;
    }
}
