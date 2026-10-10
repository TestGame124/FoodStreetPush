using FoodStreet.Placement;
using UnityEngine;

namespace LastOrders.Restaurant
{
    /// <summary>
    /// Where a kitchen station bought from the shop may be set down: inside the diner, its whole
    /// footprint on the diner's floor - which is laid exactly between the walls' inner faces and
    /// grows with them - and not on top of the things in there the grid does not hold: a wall, a
    /// table, the stores, a plant bed, a station placed by hand. A kitchen is indoors; the diner is
    /// roofless only because the game is seen from above.
    ///
    /// Runs every frame while the ghost is dragged: nothing here allocates.
    /// </summary>
    [CreateAssetMenu(menuName = "Food Street/Placement/Rules/Kitchen Footing")]
    public sealed class KitchenFootingRule : PlacementRule
    {
        /// <summary>Metres the footprint is shrunk by when looking for things in the way, so standing against a neighbour is still clear.</summary>
        const float Margin = 0.15f;

        /// <summary>How far past the floor's edge a footprint may reach and still count as on it: float slack, not a gap.</summary>
        const float Slack = 0.01f;

        static readonly Collider[] Hits = new Collider[64];
        static Renderer floor;

        public override PlacementFailure Validate(PlacementService service, PlacedObjectTypeSO type, Vector2Int origin,
                                                  PlacedObjectTypeSO.Dir direction, PlacedObject_Done moving)
        {
            Vector2Int size = type.GetFootprintSize(direction);
            float cell = service.Layout.CellSize;
            Vector3 corner = service.Layout.CellToWorld(origin);
            var footprint = new Rect(corner.x, corner.z, size.x * cell, size.y * cell);

            if (!OnDinerFloor(footprint)) return PlacementFailure.RuleRejected;

            var middle = new Vector3(footprint.center.x, corner.y, footprint.center.y);
            var half = new Vector2(footprint.width * 0.5f - Margin, footprint.height * 0.5f - Margin);
            return InTheWay(middle, half, moving != null ? moving.transform : null) ? PlacementFailure.RuleRejected : PlacementFailure.None;
        }

        /// <summary>Whether the footprint lies on the diner's floor. A level with no diner has no kitchen to keep things in.</summary>
        static bool OnDinerFloor(Rect footprint)
        {
            if (floor == null)
            {
                var found = Object.FindFirstObjectByType<RestaurantFloor>();
                floor = found != null ? found.GetComponent<Renderer>() : null;
                if (floor == null) return true;
            }

            Bounds b = floor.bounds;
            return footprint.xMin >= b.min.x - Slack && footprint.xMax <= b.max.x + Slack &&
                   footprint.yMin >= b.min.z - Slack && footprint.yMax <= b.max.z + Slack;
        }

        /// <summary>Whether a wall or a fitting the grid does not hold stands in the footprint.</summary>
        static bool InTheWay(Vector3 centre, Vector2 half, Transform self)
        {
            int count = Physics.OverlapBoxNonAlloc(centre + Vector3.up * 1f, new Vector3(half.x, 1f, half.y), Hits, Quaternion.identity, ~0,
                                                   QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider hit = Hits[i];
                if (hit == null || !hit.enabled || (self != null && hit.transform.IsChildOf(self))) continue;

                if (hit.GetComponentInParent<RestaurantShell>() != null || hit.GetComponentInParent<CookeryStation>() != null ||
                    hit.GetComponentInParent<TableBehaviour>() != null || hit.GetComponentInParent<InventoryObject>() != null ||
                    hit.GetComponentInParent<PlantPot>() != null)
                    return true;
            }

            return false;
        }
    }
}
