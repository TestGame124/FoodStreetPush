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
        readonly GridXZ<GridBuildingSystem3D.GridObject> cells;
        public GridLayout Layout { get; }

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
            return true;
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
