using LastOrders.DayCycle;
using LastOrders.Economy;

namespace LastOrders.Build
{
    /// <summary>Optional preflight so a blocked inverse operation stays on the undo stack.</summary>
    public interface IBuildUndoValidation
    {
        bool CanUndo(BuildContext context);
    }

    /// <summary>
    /// Everything a command needs to act on the world. Passed in rather than captured so a
    /// recorded command can be replayed later against a different context, which is what
    /// makes Rebuild All work.
    /// </summary>
    public sealed class BuildContext
    {
        public readonly ResourceLedger Ledger;
        public readonly TimeService Time;
        public readonly GridBuildingSystem3D Grid;

        /// <summary>When true, commands skip their clock-minute cost. Dawn is paused and its
        /// rebuilds are priced in materials only; the working day is where minutes are
        /// currency.</summary>
        public bool IgnoreClockCost;

        /// <summary>Scales material cost. Rebuilding from a ghost outline is 40 percent
        /// (15.2), repairing a Damaged object is 15 percent (15.4).</summary>
        public float CostMultiplier = 1f;

        public BuildContext(ResourceLedger ledger, TimeService time, GridBuildingSystem3D grid)
        {
            Ledger = ledger;
            Time = time;
            Grid = grid;
        }
    }

    /// <summary>
    /// One reversible change to the build layer: place, move, rotate, delete, upgrade.
    ///
    /// A small pattern with a disproportionate payoff here, because three separate features
    /// in the GDD fall out of it rather than being built separately. The free five-second
    /// undo of 6.3 is <see cref="Undo"/>. The one-tap Rebuild All of 15.3 is a replay of
    /// recorded commands at a reduced cost multiplier. And the layout's serialization story
    /// becomes "write down the commands" rather than a bespoke format.
    ///
    /// The contract that makes all three safe: <see cref="Execute"/> either fully succeeds
    /// and returns true, or changes nothing and returns false. There is no partial state for
    /// <see cref="Undo"/> to have to reason about.
    /// </summary>
    public interface IBuildCommand
    {
        /// <summary>Shown in the undo toast and written to the command log.</summary>
        string Description { get; }

        /// <summary>False for anything that cannot be meaningfully reversed. Such commands
        /// still execute; they simply are not pushed onto the undo stack.</summary>
        bool IsUndoable { get; }

        /// <summary>Checks affordability and legality without mutating anything.</summary>
        bool CanExecute(BuildContext context);

        /// <summary>All-or-nothing. Returns false having changed nothing if it cannot complete.</summary>
        bool Execute(BuildContext context);

        /// <summary>Restores the world to exactly its pre-execute state, including refunding
        /// materials and clock-minutes.</summary>
        void Undo(BuildContext context);
    }
}
