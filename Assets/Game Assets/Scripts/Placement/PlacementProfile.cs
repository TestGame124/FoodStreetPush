using UnityEngine;

namespace FoodStreet.Placement
{
    /// <summary>Shared, immutable-at-runtime placement policy; occupancy belongs to the scene.</summary>
    [CreateAssetMenu(menuName = "Food Street/Placement/Placement Profile")]
    public sealed class PlacementProfile : ScriptableObject
    {
        public bool allowRotation = true;
        public bool requiresFloor;
        public PlacementRule[] rules;
    }
}
