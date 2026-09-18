using System.Collections.Generic;
using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Build
{
    /// <summary>
    /// Places one buildable on the grid.
    ///
    /// Deliberately built on the low-level placement primitives rather than on
    /// <see cref="GridBuildingSystem3D.PlaceObject"/>, which reads the mouse, the tutorial
    /// state and the UI. A command has to be replayable from a save or from Rebuild All,
    /// where none of those exist.
    /// </summary>
    public sealed class PlaceBuildableCommand : IBuildCommand
    {
        readonly PlacedObjectTypeSO type;
        readonly Vector2Int origin;
        readonly PlacedObjectTypeSO.Dir dir;

        PlacedObject_Done placed;
        ResourceAmount[] paidCost;
        float paidClockMinutes;

        public PlaceBuildableCommand(PlacedObjectTypeSO type, Vector2Int origin, PlacedObjectTypeSO.Dir dir)
        {
            this.type = type;
            this.origin = origin;
            this.dir = dir;
        }

        public string Description => $"Place {type.nameString}";

        public bool IsUndoable => true;

        /// <summary>The object this command created, once executed. Move and Upgrade read it
        /// so a batch can chain without going back through the grid.</summary>
        public PlacedObject_Done Placed => placed;

        public bool CanExecute(BuildContext context)
        {
            if (type == null || context.Grid == null) return false;
            if (!AreTilesFree(context, type, origin, dir)) return false;

            ResourceAmount[] cost = type.buildCost.Scaled(context.CostMultiplier);
            if (!context.Ledger.CanAfford(cost)) return false;

            if (!context.IgnoreClockCost && !context.Time.CanAfford(type.buildClockMinutes))
                return false;

            return true;
        }

        public bool Execute(BuildContext context)
        {
            if (!CanExecute(context)) return false;

            ResourceAmount[] cost = type.buildCost.Scaled(context.CostMultiplier);

            // Materials first, then time. Both are checked above, but spending in a fixed
            // order means a future failure mode always leaves the same partial state to
            // reason about - and the refund path below mirrors it exactly.
            if (!context.Ledger.TrySpend(cost)) return false;

            float minutes = context.IgnoreClockCost ? 0f : type.buildClockMinutes;
            if (minutes > 0f && !context.Time.TrySpendClockMinutes(minutes))
            {
                context.Ledger.Add(cost);
                return false;
            }

            paidCost = cost;
            paidClockMinutes = minutes;

            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();
            Vector3 world = grid.GetWorldPosition(origin.x, origin.y);

            placed = PlacedObject_Done.Create(world, origin, dir, type);

            List<Vector2Int> tiles = type.GetGridPositionList(origin, dir);
            for (int i = 0; i < tiles.Count; i++)
                grid.GetGridObject(tiles[i].x, tiles[i].y)?.SetPlacedObject(placed);

            return true;
        }

        public void Undo(BuildContext context)
        {
            if (placed == null) return;

            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();

            List<Vector2Int> tiles = placed.GetGridPositionList();
            for (int i = 0; i < tiles.Count; i++)
                grid.GetGridObject(tiles[i].x, tiles[i].y)?.ClearPlacedObject();

            placed.DestroySelf();
            placed = null;

            // Refund both currencies. An undo that returned materials but not minutes would
            // quietly make the undo button a way to burn the day.
            if (paidCost != null) context.Ledger.Add(paidCost);
            if (paidClockMinutes > 0f) context.Time.RefundClockMinutes(paidClockMinutes);

            paidCost = null;
            paidClockMinutes = 0f;
        }

        /// <summary>Shared by every command that needs to know whether a footprint will fit.</summary>
        internal static bool AreTilesFree(BuildContext context, PlacedObjectTypeSO type,
                                          Vector2Int origin, PlacedObjectTypeSO.Dir dir,
                                          PlacedObject_Done ignore = null)
        {
            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();
            if (grid == null) return false;

            List<Vector2Int> tiles = type.GetGridPositionList(origin, dir);
            for (int i = 0; i < tiles.Count; i++)
            {
                GridBuildingSystem3D.GridObject cell = grid.GetGridObject(tiles[i].x, tiles[i].y);

                // Null means off-grid, which is a legitimate placement failure rather than
                // an error - the player dragged past the edge of the plot.
                if (cell == null) return false;

                PlacedObject_Done occupant = cell.GetPlacedObject();
                if (occupant != null && occupant != ignore) return false;
            }

            return true;
        }
    }
}
