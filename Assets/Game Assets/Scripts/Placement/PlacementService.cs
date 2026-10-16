using System.Collections.Generic;
using UnityEngine;

namespace FoodStreet.Placement
{
    /// <summary>
    /// Authoritative placement operations. Validation is O(footprint area), without temporary lists.
    /// GridXZ is retained only as the existing navigation/event adapter; each cell owns two layers.
    /// All validation completes before a move releases any cells.
    /// </summary>
    public sealed class PlacementService
    {
        /// <summary>
        /// Fittings that stand on the grid without being placed objects - the diner's serving pass -
        /// keyed by whoever set them. Static: they belong to the scene, not to one grid's lifetime,
        /// and a fitting can come up before the grid does.
        /// </summary>
        static readonly Dictionary<object, Rect> fixtures = new Dictionary<object, Rect>();

        /// <summary>
        /// Something was put down on the grid by the player's hand or a build command: a new object
        /// (false) or one picked up and put down somewhere else (true). Not raised for what the scene
        /// registers at start, nor for <see cref="TryShift"/>, which carries objects rather than drops them.
        /// </summary>
        public static event System.Action<PlacedObject_Done, bool> Placed;

        readonly GridXZ<GridBuildingSystem3D.GridObject> cells;
        public GridLayout Layout { get; }

        /// <summary>
        /// Keeps anything solid from being built over <paramref name="area"/>: a flat world rectangle,
        /// x across and y along the world's Z. Setting the same owner again moves it; null clears it.
        /// Floors still go under it.
        /// </summary>
        public static void SetFixture(object owner, Rect? area)
        {
            if (owner == null) return;

            if (area.HasValue) fixtures[owner] = area.Value;
            else fixtures.Remove(owner);
        }

        public PlacementService(GridXZ<GridBuildingSystem3D.GridObject> cells)
        {
            this.cells = cells;
            Layout = new GridLayout(cells.GetWidth(), cells.GetHeight(), cells.GetCellSize(), cells.GetOriginPosition());
        }

        public PlacementFailure Validate(PlacedObjectTypeSO type, Vector2Int origin,
            PlacedObjectTypeSO.Dir direction, PlacedObject_Done moving = null)
        {
            if (type == null || type.WidthInCells < 1 || type.HeightInCells < 1 || (int)direction < 0 || (int)direction > 3)
                return PlacementFailure.InvalidDefinition;
            var profile = type.placementProfile;
            if (profile != null && !profile.allowRotation && direction != (moving != null ? moving.Dir : PlacedObjectTypeSO.Dir.Down))
                return PlacementFailure.RotationNotAllowed;
            Vector2Int size = type.GetFootprintSize(direction);
            if (!Layout.Contains(origin) || size.x > Layout.Width - origin.x || size.y > Layout.Height - origin.y)
                return PlacementFailure.OutsideGrid;
            bool floor = type.categoryType == ObjectCategory.Floor;
            if (!floor && OverlapsFixture(origin, size)) return PlacementFailure.Occupied;
            for (int x = origin.x; x < origin.x + size.x; x++)
                for (int z = origin.y; z < origin.y + size.y; z++)
                {
                    var cell = cells.GetGridObject(x, z);
                    var occupant = floor ? cell.Floor : cell.Solid;
                    if (occupant != null && occupant != moving) return PlacementFailure.Occupied;
                    if (!floor && profile != null && profile.requiresFloor && cell.Floor == null)
                        return PlacementFailure.FloorRequired;
                }
            if (profile != null && profile.rules != null)
                foreach (var rule in profile.rules)
                {
                    if (rule == null) continue;
                    var failure = rule.Validate(this, type, origin, direction, moving);
                    if (failure != PlacementFailure.None) return failure;
                }
            return PlacementFailure.None;
        }

        /// <summary>True when the footprint's cells overlap any fitting. A hair inside the cells' edges,
        /// so a fitting that only touches a cell's side leaves it free.</summary>
        bool OverlapsFixture(Vector2Int origin, Vector2Int size)
        {
            if (fixtures.Count == 0) return false;

            const float Inset = 0.02f;
            Vector3 corner = Layout.CellToWorld(origin);
            var footprint = new Rect(corner.x + Inset, corner.z + Inset, size.x * Layout.CellSize - Inset * 2f, size.y * Layout.CellSize - Inset * 2f);

            foreach (KeyValuePair<object, Rect> fixture in fixtures)
            {
                // A fitting destroyed without clearing itself (a scene unloaded) stops counting.
                if (fixture.Key is Object owner && owner == null) continue;
                if (fixture.Value.Overlaps(footprint)) return true;
            }

            return false;
        }

        /// <summary>The ground an object's cells cover, flat: x across, y along the world's Z.</summary>
        public Rect Footprint(PlacedObject_Done target)
        {
            if (target == null || target.PlacedObjectTypeSO == null) return default;

            Vector2Int size = target.PlacedObjectTypeSO.GetFootprintSize(target.Dir);
            Vector3 corner = Layout.CellToWorld(target.Origin);
            return new Rect(corner.x, corner.z, size.x * Layout.CellSize, size.y * Layout.CellSize);
        }

        public PlacedObject_Done GetAt(Vector2Int cell, bool floor = false)
        {
            var value = cells.GetGridObject(cell.x, cell.y);
            return value == null ? null : floor ? value.Floor : value.Solid;
        }

        public bool TryPlace(PlacedObjectTypeSO type, Vector2Int origin, PlacedObjectTypeSO.Dir direction, out PlacedObject_Done placed)
        {
            placed = null;
            if (Validate(type, origin, direction) != PlacementFailure.None || type.prefab == null ||
                type.prefab.GetComponent<PlacedObject_Done>() == null) return false;
            placed = PlacedObject_Done.Create(Layout.Pivot(type, origin, direction), origin, direction, type);
            Register(placed);
            Placed?.Invoke(placed, false);
            return true;
        }

        public bool CanMove(PlacedObject_Done target, Vector2Int origin, PlacedObjectTypeSO.Dir direction) =>
            target != null && !target.isRemoved && Validate(target.PlacedObjectTypeSO, origin, direction, target) == PlacementFailure.None &&
            (!target.IsFloor() || (target.Origin == origin && target.Dir == direction) || CanRemove(target));

        public bool TryMove(PlacedObject_Done target, Vector2Int origin, PlacedObjectTypeSO.Dir direction)
        {
            if (!CanMove(target, origin, direction)) return false;
            Unregister(target);
            target.PlaceSelectedObject(Layout.Pivot(target.PlacedObjectTypeSO, origin, direction), origin, direction);
            Register(target);
            Placed?.Invoke(target, true);
            return true;
        }

        /// <summary>
        /// True when every one of <paramref name="movers"/> fits <paramref name="offset"/> cells
        /// along: still on the grid, and onto cells that are free or held by another of them. The
        /// first object standing in the way, when there is one, as <paramref name="blocker"/> - null
        /// with false means the grid itself ends there. Rules and fittings are not asked: the group
        /// moves together, as it already stands.
        /// </summary>
        public bool CanShift(IReadOnlyList<PlacedObject_Done> movers, Vector2Int offset, out PlacedObject_Done blocker)
        {
            blocker = null;
            if (movers == null) return true;

            foreach (PlacedObject_Done target in movers)
            {
                if (target == null || target.isRemoved || target.PlacedObjectTypeSO == null) continue;

                Vector2Int origin = target.Origin + offset;
                Vector2Int size = target.PlacedObjectTypeSO.GetFootprintSize(target.Dir);
                if (!Layout.Contains(origin) || size.x > Layout.Width - origin.x || size.y > Layout.Height - origin.y)
                    return false;

                bool floor = target.IsFloor();
                for (int x = origin.x; x < origin.x + size.x; x++)
                    for (int z = origin.y; z < origin.y + size.y; z++)
                    {
                        var cell = cells.GetGridObject(x, z);
                        var occupant = floor ? cell.Floor : cell.Solid;
                        if (occupant == null || Contains(movers, occupant)) continue;

                        blocker = occupant;
                        return false;
                    }
            }

            return true;
        }

        /// <summary>
        /// Moves a group of objects the same number of cells together - the kitchen going back with
        /// the restaurant's back wall - as one step, so none of them is refused for a cell another one
        /// is only just leaving. Their poses jump to the new cells; how they get there is the caller's.
        /// </summary>
        public bool TryShift(IReadOnlyList<PlacedObject_Done> movers, Vector2Int offset)
        {
            if (movers == null || movers.Count == 0 || offset == Vector2Int.zero) return true;
            if (!CanShift(movers, offset, out _)) return false;

            foreach (PlacedObject_Done target in movers)
                if (target != null && !target.isRemoved) Unregister(target);

            foreach (PlacedObject_Done target in movers)
            {
                if (target == null || target.isRemoved || target.PlacedObjectTypeSO == null) continue;

                Vector2Int origin = target.Origin + offset;
                target.PlaceSelectedObject(Layout.Pivot(target.PlacedObjectTypeSO, origin, target.Dir), origin, target.Dir);
                Register(target);
            }

            return true;
        }

        static bool Contains(IReadOnlyList<PlacedObject_Done> list, PlacedObject_Done item)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == item) return true;
            return false;
        }

        public bool CanRemove(PlacedObject_Done target)
        {
            if (target == null || target.isRemoved || target.PlacedObjectTypeSO == null || target.PlacedObjectTypeSO.isHearth) return false;
            if (!target.IsFloor()) return true;
            var size = target.PlacedObjectTypeSO.GetFootprintSize(target.Dir);
            for (int x = target.Origin.x; x < target.Origin.x + size.x; x++)
                for (int z = target.Origin.y; z < target.Origin.y + size.y; z++)
                {
                    var solid = GetAt(new Vector2Int(x, z));
                    if (solid != null && solid.PlacedObjectTypeSO.placementProfile != null && solid.PlacedObjectTypeSO.placementProfile.requiresFloor) return false;
                }
            return true;
        }

        public void Register(PlacedObject_Done target)
        {
            if (target != null) target.placedPosition = target.transform.position;
            Write(target, false);
        }
        public void Unregister(PlacedObject_Done target) => Write(target, true);

        void Write(PlacedObject_Done target, bool remove)
        {
            if (target == null || target.PlacedObjectTypeSO == null) return;
            Vector2Int size = target.PlacedObjectTypeSO.GetFootprintSize(target.Dir);
            for (int x = target.Origin.x; x < target.Origin.x + size.x; x++)
                for (int z = target.Origin.y; z < target.Origin.y + size.y; z++)
                {
                    var cell = cells.GetGridObject(x, z);
                    if (cell == null) continue;
                    if (remove) cell.Remove(target); else cell.SetPlacedObject(target);
                }
        }
    }
}
