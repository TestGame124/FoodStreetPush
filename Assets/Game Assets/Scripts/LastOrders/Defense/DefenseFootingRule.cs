using FoodStreet.Placement;
using UnityEngine;

namespace LastOrders.Defense
{
    /// <summary>
    /// Where a defense bought from the shop may be set down: on open ground, or over the compound
    /// wall - where it goes up on a bastion (<see cref="WallMount"/>) - but not across a gateway, on
    /// a wall site or defense pad, on top of a defense already standing, or half on a wall.
    /// <see cref="WallFooting"/> decides; this only turns its refusal into a red ghost.
    /// </summary>
    [CreateAssetMenu(menuName = "Food Street/Placement/Rules/Defense Footing")]
    public sealed class DefenseFootingRule : PlacementRule
    {
        public override PlacementFailure Validate(PlacementService service, PlacedObjectTypeSO type, Vector2Int origin,
                                                  PlacedObjectTypeSO.Dir direction, PlacedObject_Done moving)
        {
            Vector2Int size = type.GetFootprintSize(direction);
            float cell = service.Layout.CellSize;
            Vector3 middle = service.Layout.CellToWorld(origin) + new Vector3(size.x * cell * 0.5f, 0f, size.y * cell * 0.5f);

            WallFooting.Result footing = WallFooting.Find(middle, new Vector2(size.x * cell, size.y * cell), moving != null ? moving.transform : null);
            return footing.Kind == WallFooting.Kind.Blocked ? PlacementFailure.RuleRejected : PlacementFailure.None;
        }
    }
}
