using System.Collections.Generic;
using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Build
{
    /// <summary>
    /// Removes a buildable. Used by the player selling, and by the consequence model when a
    /// perimeter object is Destroyed.
    ///
    /// Undo re-creates the object rather than resurrecting the original instance, because
    /// the original went back to its pool on destruction. Everything that identifies it -
    /// type, origin, facing - is captured up front so the reconstruction is exact.
    /// </summary>
    public sealed class DeleteBuildableCommand : IBuildCommand
    {
        readonly PlacedObjectTypeSO type;
        readonly Vector2Int origin;
        readonly PlacedObjectTypeSO.Dir dir;

        PlacedObject_Done target;
        bool executed;

        public DeleteBuildableCommand(PlacedObject_Done target)
        {
            this.target = target;
            type = target != null ? target.PlacedObjectTypeSO : null;
            dir = target != null ? target.Dir : PlacedObjectTypeSO.Dir.Down;

            if (target != null)
            {
                List<Vector2Int> tiles = target.GetGridPositionList();
                origin = tiles.Count > 0 ? tiles[0] : Vector2Int.zero;
            }
        }

        public string Description => type != null ? $"Remove {type.nameString}" : "Remove";

        /// <summary>The Hearth is indestructible (15.1). It can be Damaged but never disabled,
        /// and one flag solving the softlock problem is only true if nothing can delete it.</summary>
        public bool IsUndoable => true;

        public bool CanExecute(BuildContext context) =>
            target != null && type != null && !type.isHearth;

        public bool Execute(BuildContext context)
        {
            if (!CanExecute(context)) return false;

            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();

            List<Vector2Int> tiles = target.GetGridPositionList();
            for (int i = 0; i < tiles.Count; i++)
                grid.GetGridObject(tiles[i].x, tiles[i].y)?.ClearPlacedObject();

            target.DestroySelf();
            target = null;
            executed = true;
            return true;
        }

        public void Undo(BuildContext context)
        {
            if (!executed || type == null) return;

            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();
            Vector3 world = grid.GetWorldPosition(origin.x, origin.y);

            target = PlacedObject_Done.Create(world, origin, dir, type);

            List<Vector2Int> tiles = type.GetGridPositionList(origin, dir);
            for (int i = 0; i < tiles.Count; i++)
                grid.GetGridObject(tiles[i].x, tiles[i].y)?.SetPlacedObject(target);

            executed = false;
        }
    }

    /// <summary>
    /// Moves an existing object to a new origin for a small clock cost (6.3).
    ///
    /// Implemented as clear-then-place against the same instance rather than as a delete
    /// plus a place, so the object keeps its identity: its save entry, its ID, and anything
    /// mid-flight on it (a cooking timer, a seated customer) survive the move.
    /// </summary>
    public sealed class MoveBuildableCommand : IBuildCommand
    {
        const float MoveClockMinutes = 0.5f;

        readonly PlacedObject_Done target;
        readonly Vector2Int newOrigin;
        readonly Vector2Int oldOrigin;
        readonly PlacedObjectTypeSO.Dir dir;

        public MoveBuildableCommand(PlacedObject_Done target, Vector2Int newOrigin)
        {
            this.target = target;
            this.newOrigin = newOrigin;
            dir = target != null ? target.Dir : PlacedObjectTypeSO.Dir.Down;

            if (target != null)
            {
                List<Vector2Int> tiles = target.GetGridPositionList();
                oldOrigin = tiles.Count > 0 ? tiles[0] : Vector2Int.zero;
            }
        }

        public string Description => "Move";

        public bool IsUndoable => true;

        public bool CanExecute(BuildContext context)
        {
            if (target == null) return false;
            if (!context.IgnoreClockCost && !context.Time.CanAfford(MoveClockMinutes)) return false;

            // Passing the target as the ignored occupant, so a one-tile nudge that overlaps
            // the object's own footprint is still legal.
            return PlaceBuildableCommand.AreTilesFree(
                context, target.PlacedObjectTypeSO, newOrigin, dir, target);
        }

        public bool Execute(BuildContext context)
        {
            if (!CanExecute(context)) return false;

            if (!context.IgnoreClockCost && !context.Time.TrySpendClockMinutes(MoveClockMinutes))
                return false;

            Relocate(oldOrigin, newOrigin);
            return true;
        }

        public void Undo(BuildContext context)
        {
            Relocate(newOrigin, oldOrigin);
            if (!context.IgnoreClockCost) context.Time.RefundClockMinutes(MoveClockMinutes);
        }

        void Relocate(Vector2Int from, Vector2Int to)
        {
            if (target == null) return;

            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();
            PlacedObjectTypeSO type = target.PlacedObjectTypeSO;

            List<Vector2Int> before = type.GetGridPositionList(from, dir);
            for (int i = 0; i < before.Count; i++)
                grid.GetGridObject(before[i].x, before[i].y)?.ClearPlacedObject();

            target.PlaceSelectedObject(grid.GetWorldPosition(to.x, to.y), to, dir);

            List<Vector2Int> after = type.GetGridPositionList(to, dir);
            for (int i = 0; i < after.Count; i++)
                grid.GetGridObject(after[i].x, after[i].y)?.SetPlacedObject(target);
        }
    }
}
