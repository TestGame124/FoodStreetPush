using System.Collections.Generic;
using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Build
{
    /// <summary>
    /// Places one buildable on the grid.
    ///
    /// Deliberately built on the low-level placement primitives rather than on
    /// <see cref="GridBuildingSystem3D.PlaceObject"/>, which reads the mouse and the UI.
    /// A command has to be replayable from Rebuild All, where neither of those exists.
    /// </summary>
    public sealed class PlaceBuildableCommand : IBuildCommand, IBuildUndoValidation
    {
        readonly PlacedObjectTypeSO type;
        readonly Vector2Int origin;
        readonly PlacedObjectTypeSO.Dir dir;

        PlacedObject_Done placed;
        int placedIdentity;
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
            if (placed != null || type == null || type.prefab == null || type.prefab.GetComponent<PlacedObject_Done>() == null || context.Grid == null) return false;
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

            if (!context.Grid.Placement.TryPlace(type, origin, dir, out placed))
            {
                context.Ledger.Add(paidCost);
                if (paidClockMinutes > 0) context.Time.RefundClockMinutes(paidClockMinutes);
                paidCost = null; paidClockMinutes = 0;
                return false;
            }
            placedIdentity = placed.ID;
            return true;
        }

        public void Undo(BuildContext context)
        {
            if (!CanUndo(context)) return;

            placed.DestroySelf();
            placed = null;

            // Refund both currencies. An undo that returned materials but not minutes would
            // quietly make the undo button a way to burn the day.
            if (paidCost != null) context.Ledger.Add(paidCost);
            if (paidClockMinutes > 0f) context.Time.RefundClockMinutes(paidClockMinutes);

            paidCost = null;
            paidClockMinutes = 0f;
        }

        public bool CanUndo(BuildContext context) => placed != null && !placed.isRemoved && placed.ID == placedIdentity &&
            placed.Origin == origin && placed.Dir == dir && context.Grid.Placement.CanRemove(placed);

        /// <summary>Shared by every command that needs to know whether a footprint will fit.</summary>
        internal static bool AreTilesFree(BuildContext context, PlacedObjectTypeSO type,
                                          Vector2Int origin, PlacedObjectTypeSO.Dir dir,
                                          PlacedObject_Done ignore = null)
        {
            return context.Grid != null && context.Grid.Placement != null &&
                context.Grid.Placement.Validate(type, origin, dir, ignore) == FoodStreet.Placement.PlacementFailure.None;
        }
    }
}
