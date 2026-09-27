using UnityEngine;
namespace LastOrders.Build
{
    public sealed class DeleteBuildableCommand : IBuildCommand, IBuildUndoValidation
    {
        readonly PlacedObjectTypeSO type;
        readonly Vector2Int origin;
        readonly PlacedObjectTypeSO.Dir dir;
        readonly int identity;
        PlacedObject_Done target;
        bool executed;
        public DeleteBuildableCommand(PlacedObject_Done target)
        {
            this.target = target;
            if (target == null) return;
            type = target.PlacedObjectTypeSO; dir = target.Dir; origin = target.Origin; identity = target.ID;
        }
        public string Description => type != null ? "Remove " + type.nameString : "Remove";
        public bool IsUndoable => true;
        public bool CanExecute(BuildContext context) => !executed && target != null && target.ID == identity && target.Origin == origin &&
            target.Dir == dir && context.Grid != null && context.Grid.Placement != null && context.Grid.Placement.CanRemove(target);
        public bool Execute(BuildContext context)
        {
            if (!CanExecute(context)) return false;
            target.DestroySelf(); target = null; executed = true; return true;
        }
        public void Undo(BuildContext context)
        {
            if (!CanUndo(context) || !context.Grid.Placement.TryPlace(type, origin, dir, out target)) return;
            target.RestoreIdentity(identity);
            executed = false;
        }
        public bool CanUndo(BuildContext context) => executed && type != null && type.prefab != null &&
            PlaceBuildableCommand.AreTilesFree(context, type, origin, dir);
    }
    public sealed class MoveBuildableCommand : IBuildCommand, IBuildUndoValidation
    {
        const float MoveClockMinutes = .5f;
        readonly PlacedObject_Done target;
        readonly Vector2Int newOrigin, oldOrigin;
        readonly PlacedObjectTypeSO.Dir dir;
        bool executed;
        float paidMinutes;
        readonly int identity;
        public MoveBuildableCommand(PlacedObject_Done target, Vector2Int newOrigin)
        {
            this.target = target; this.newOrigin = newOrigin;
            if (target == null) return;
            dir = target.Dir; oldOrigin = target.Origin;
            identity = target.ID;
        }
        public string Description => "Move";
        public bool IsUndoable => true;
        public bool CanExecute(BuildContext context) => !executed && target != null && !target.isRemoved && target.ID == identity && target.Origin == oldOrigin && target.Dir == dir &&
            (context.IgnoreClockCost || context.Time.CanAfford(MoveClockMinutes)) &&
            context.Grid != null && context.Grid.Placement != null && context.Grid.Placement.CanMove(target, newOrigin, dir);
        public bool Execute(BuildContext context)
        {
            if (!CanExecute(context)) return false;
            paidMinutes = context.IgnoreClockCost ? 0 : MoveClockMinutes;
            if (paidMinutes > 0 && !context.Time.TrySpendClockMinutes(paidMinutes)) return false;
            if (!context.Grid.Placement.TryMove(target, newOrigin, dir))
            {
                if (paidMinutes > 0) context.Time.RefundClockMinutes(paidMinutes);
                paidMinutes = 0; return false;
            }
            executed = true; return true;
        }
        public void Undo(BuildContext context)
        {
            if (!CanUndo(context) || !context.Grid.Placement.TryMove(target, oldOrigin, dir)) return;
            if (paidMinutes > 0) context.Time.RefundClockMinutes(paidMinutes);
            paidMinutes = 0; executed = false;
        }
        public bool CanUndo(BuildContext context) => executed && target != null && !target.isRemoved && target.ID == identity &&
            target.Origin == newOrigin && target.Dir == dir && context.Grid.Placement.CanMove(target, oldOrigin, dir);
    }
}
