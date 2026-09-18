using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.Build
{
    /// <summary>
    /// Turns a placed object 90 degrees (6.3).
    ///
    /// Free and instant. Rotation changes no cost and no capacity, so charging for it would
    /// only punish the player for the camera having no rotation of its own (5) - they are
    /// orienting the object because they cannot orient the view.
    ///
    /// It can still fail: a non-square footprint sweeps a different set of tiles after
    /// turning, so a 1x3 counter in a tight alcove has nowhere to go.
    /// </summary>
    public sealed class RotateBuildableCommand : IBuildCommand
    {
        readonly PlacedObject_Done target;
        readonly PlacedObjectTypeSO.Dir oldDir;
        readonly PlacedObjectTypeSO.Dir newDir;
        readonly Vector2Int origin;

        public RotateBuildableCommand(PlacedObject_Done target)
        {
            this.target = target;

            if (target != null)
            {
                oldDir = target.Dir;
                newDir = PlacedObjectTypeSO.GetNextDir(oldDir);

                List<Vector2Int> tiles = target.GetGridPositionList();
                origin = tiles.Count > 0 ? tiles[0] : Vector2Int.zero;
            }
        }

        public string Description => "Rotate";

        public bool IsUndoable => true;

        public bool CanExecute(BuildContext context)
        {
            if (target == null) return false;

            return PlaceBuildableCommand.AreTilesFree(
                context, target.PlacedObjectTypeSO, origin, newDir, target);
        }

        public bool Execute(BuildContext context)
        {
            if (!CanExecute(context)) return false;

            Apply(oldDir, newDir);
            return true;
        }

        public void Undo(BuildContext context) => Apply(newDir, oldDir);

        void Apply(PlacedObjectTypeSO.Dir from, PlacedObjectTypeSO.Dir to)
        {
            if (target == null) return;

            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();
            PlacedObjectTypeSO type = target.PlacedObjectTypeSO;

            List<Vector2Int> before = type.GetGridPositionList(origin, from);
            for (int i = 0; i < before.Count; i++)
                grid.GetGridObject(before[i].x, before[i].y)?.ClearPlacedObject();

            target.PlaceSelectedObject(grid.GetWorldPosition(origin.x, origin.y), origin, to);

            List<Vector2Int> after = type.GetGridPositionList(origin, to);
            for (int i = 0; i < after.Count; i++)
                grid.GetGridObject(after[i].x, after[i].y)?.SetPlacedObject(target);
        }
    }
}
