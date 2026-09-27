using UnityEngine;

namespace FoodStreet.Placement
{
    [CreateAssetMenu(menuName = "Food Street/Placement/Rules/Require Adjacent Object")]
    public sealed class AdjacentObjectRule : PlacementRule
    {
        public PlacedObjectTypeSO requiredNeighbor;
        public override PlacementFailure Validate(PlacementService service, PlacedObjectTypeSO type,
            Vector2Int origin, PlacedObjectTypeSO.Dir direction, PlacedObject_Done moving)
        {
            if (requiredNeighbor == null) return PlacementFailure.RuleRejected;
            var size = type.GetFootprintSize(direction);
            for (int x = origin.x; x < origin.x + size.x; x++)
                if (Matches(service, new Vector2Int(x, origin.y - 1), moving) ||
                    Matches(service, new Vector2Int(x, origin.y + size.y), moving)) return PlacementFailure.None;
            for (int z = origin.y; z < origin.y + size.y; z++)
                if (Matches(service, new Vector2Int(origin.x - 1, z), moving) ||
                    Matches(service, new Vector2Int(origin.x + size.x, z), moving)) return PlacementFailure.None;
            return PlacementFailure.RuleRejected;
        }
        bool Matches(PlacementService service, Vector2Int cell, PlacedObject_Done moving)
        {
            var neighbor = service.GetAt(cell, requiredNeighbor.categoryType == ObjectCategory.Floor);
            return neighbor != null && neighbor != moving && neighbor.PlacedObjectTypeSO == requiredNeighbor;
        }
    }
}
