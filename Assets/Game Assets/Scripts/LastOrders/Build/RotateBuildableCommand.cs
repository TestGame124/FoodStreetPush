using UnityEngine;
namespace LastOrders.Build
{
    public sealed class RotateBuildableCommand : IBuildCommand, IBuildUndoValidation
    {
        readonly PlacedObject_Done target;
        readonly PlacedObjectTypeSO.Dir oldDir, newDir;
        readonly Vector2Int origin;
        bool executed;
        readonly int identity;
        public RotateBuildableCommand(PlacedObject_Done target)
        {
            this.target = target;
            if (target == null) return;
            oldDir = target.Dir; newDir = PlacedObjectTypeSO.GetNextDir(oldDir); origin = target.Origin;
            identity = target.ID;
        }
        public string Description => "Rotate";
        public bool IsUndoable => true;
        public bool CanExecute(BuildContext context) => !executed && target != null && !target.isRemoved && target.ID == identity && target.Origin == origin && target.Dir == oldDir &&
            context.Grid != null && context.Grid.Placement != null && context.Grid.Placement.CanMove(target, origin, newDir);
        public bool Execute(BuildContext context)
        {
            if (!CanExecute(context) || !context.Grid.Placement.TryMove(target, origin, newDir)) return false;
            executed = true; return true;
        }
        public void Undo(BuildContext context)
        {
            if (!CanUndo(context) || !context.Grid.Placement.TryMove(target, origin, oldDir)) return;
            executed = false;
        }
        public bool CanUndo(BuildContext context) => executed && target != null && !target.isRemoved && target.ID == identity &&
            target.Origin == origin && target.Dir == newDir && context.Grid.Placement.CanMove(target, origin, oldDir);
    }
}
