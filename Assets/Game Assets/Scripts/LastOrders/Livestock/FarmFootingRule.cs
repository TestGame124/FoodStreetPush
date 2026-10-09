using FoodStreet.Navigation;
using FoodStreet.Placement;
using LastOrders.Defense;
using LastOrders.Housing;
using LastOrders.Medical;
using LastOrders.Perimeter;
using LastOrders.Restaurant;
using UnityEngine;

namespace LastOrders.Livestock
{
    /// <summary>
    /// Where a chicken coop or a cow shelter bought from the shop may be set down: on open ground
    /// only. Not across the compound wall or a gateway - where a shooter would go up on a bastion, a
    /// hen house has no business - nor on a wall site, a defense pad or a defense; and not on top of
    /// the things standing in the compound that the grid does not know about: the diner, the
    /// storehouse, a shelter, the hospital tent, a table, a plant bed, a stove, another pen.
    ///
    /// <see cref="WallFooting"/> is asked first, as the shooters ask it. Its check of the wall samples
    /// a footprint at a few points - enough for a 2 x 2 shooter, but the gaps between them on a pen
    /// several metres deep are wider than the wall is thick - so the wall and the gates are walked
    /// along here as well, and nothing on them slips through. Runs every frame while the ghost is
    /// dragged: nothing here allocates.
    /// </summary>
    [CreateAssetMenu(menuName = "Food Street/Placement/Rules/Farm Footing")]
    public sealed class FarmFootingRule : PlacementRule
    {
        /// <summary>Metres the footprint is shrunk by, so grazing the face of a wall or a hut is still open ground.</summary>
        const float Margin = 0.12f;

        /// <summary>How finely the wall is walked along, metres.</summary>
        const float Step = 0.1f;

        static readonly Collider[] Hits = new Collider[64];

        public override PlacementFailure Validate(PlacementService service, PlacedObjectTypeSO type, Vector2Int origin,
                                                  PlacedObjectTypeSO.Dir direction, PlacedObject_Done moving)
        {
            Vector2Int size = type.GetFootprintSize(direction);
            float cell = service.Layout.CellSize;
            Vector3 middle = service.Layout.CellToWorld(origin) + new Vector3(size.x * cell * 0.5f, 0f, size.y * cell * 0.5f);
            Transform self = moving != null ? moving.transform : null;

            WallFooting.Result footing = WallFooting.Find(middle, new Vector2(size.x * cell, size.y * cell), self);
            if (footing.Kind != WallFooting.Kind.Ground) return PlacementFailure.RuleRejected;

            var half = new Vector2(size.x * cell * 0.5f - Margin, size.y * cell * 0.5f - Margin);
            if (CrossesWall(middle, half, self) || OnSomething(middle, half, self)) return PlacementFailure.RuleRejected;
            return PlacementFailure.None;
        }

        /// <summary>Whether any stretch of the compound wall or any gate runs through the footprint.</summary>
        public static bool CrossesWall(Vector3 centre, Vector2 half, Transform self)
        {
            foreach (IMovementBarrier barrier in MovementBarriers.Standing)
            {
                if (!(barrier is CompoundStructure piece) || piece == null || !piece.IsPresent) continue;
                if (self != null && piece.transform.IsChildOf(self)) continue;

                Vector3 middle = piece.transform.position;
                float length = piece.Length * 0.5f, thickness = piece.Thickness * 0.5f;
                float reach = new Vector2(length, thickness).magnitude + half.magnitude;
                float dx = middle.x - centre.x, dz = middle.z - centre.z;
                if (dx * dx + dz * dz > reach * reach) continue;

                // Its length runs along its own X: walk its middle and both faces.
                Quaternion turn = Quaternion.Euler(0f, piece.transform.eulerAngles.y, 0f);
                Vector3 along = turn * Vector3.right, across = turn * Vector3.forward;
                int steps = Mathf.Max(1, Mathf.CeilToInt(piece.Length / Step));

                for (int i = 0; i <= steps; i++)
                {
                    Vector3 p = middle + along * Mathf.Lerp(-length, length, i / (float)steps);
                    for (int k = -1; k <= 1; k++)
                    {
                        Vector3 q = p + across * (k * thickness);
                        if (Mathf.Abs(q.x - centre.x) < half.x && Mathf.Abs(q.z - centre.z) < half.y) return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Whether the footprint is over one of the compound's buildings or fittings the grid does not hold.</summary>
        public static bool OnSomething(Vector3 centre, Vector2 half, Transform self)
        {
            int count = Physics.OverlapBoxNonAlloc(centre + Vector3.up * 1f, new Vector3(half.x, 1f, half.y), Hits, Quaternion.identity, ~0,
                                                   QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider hit = Hits[i];
                if (hit == null || (self != null && hit.transform.IsChildOf(self))) continue;

                if (hit.GetComponentInParent<RestaurantShell>() != null || hit.GetComponentInParent<InventoryObject>() != null ||
                    hit.GetComponentInParent<ShelterHut>() != null || hit.GetComponentInParent<HospitalTent>() != null ||
                    hit.GetComponentInParent<TableBehaviour>() != null || hit.GetComponentInParent<PlantPot>() != null ||
                    hit.GetComponentInParent<CookeryStation>() != null || hit.GetComponentInParent<LivestockPen>() != null)
                    return true;
            }

            return false;
        }
    }
}
