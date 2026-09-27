using UnityEngine;

namespace FoodStreet.Placement
{
    /// <summary>Extend with terrain, room, adjacency or unlock rules without changing the grid.</summary>
    public abstract class PlacementRule : ScriptableObject
    {
        public abstract PlacementFailure Validate(PlacementService service, PlacedObjectTypeSO type,
            Vector2Int origin, PlacedObjectTypeSO.Dir direction, PlacedObject_Done moving);
    }

    public enum PlacementFailure { None, InvalidDefinition, OutsideGrid, Occupied, FloorRequired, RotationNotAllowed, RuleRejected }
}
