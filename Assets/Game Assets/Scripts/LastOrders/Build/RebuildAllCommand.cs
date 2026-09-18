using System.Collections.Generic;
using LastOrders.Consequence;
using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Build
{
    /// <summary>
    /// The dawn Rebuild All button (15.3).
    ///
    /// This is the command pattern earning its place. Restoring a wrecked restaurant is
    /// "replay these placements at 40 percent cost" - a batch of ordinary
    /// <see cref="PlaceBuildableCommand"/>s against a context with a cost multiplier, and
    /// no bespoke restore code at all.
    ///
    /// The design rule it implements: the cost is real, the busywork is not. The player pays
    /// materials and clock-minutes, and never re-designs anything. When they cannot afford
    /// everything, they are offered a partial rebuild in priority order rather than being
    /// told no.
    /// </summary>
    public sealed class RebuildAllCommand : IBuildCommand
    {
        readonly GhostRegistry ghosts;
        readonly GameBalanceConfig balance;
        readonly List<PlaceBuildableCommand> applied = new List<PlaceBuildableCommand>();
        readonly List<GhostRegistry.Ghost> restored = new List<GhostRegistry.Ghost>();

        public RebuildAllCommand(GhostRegistry ghosts, GameBalanceConfig balance)
        {
            this.ghosts = ghosts;
            this.balance = balance;
        }

        public string Description => "Rebuild All";

        /// <summary>
        /// Not undoable. A dawn rebuild is a deliberate, confirmed spend, not a stray tap,
        /// and the five-second undo window exists for misplacement rather than for regret.
        /// Leaving it off the undo stack also keeps the free-undo affordance honest: it
        /// always means "that last thing I just placed".
        /// </summary>
        public bool IsUndoable => false;

        /// <summary>How many structures this run actually restored. The dawn report shows it.</summary>
        public int RestoredCount => restored.Count;

        /// <summary>True when funds ran out and only part of the restaurant came back.</summary>
        public bool WasPartial { get; private set; }

        public bool CanExecute(BuildContext context) => ghosts != null && ghosts.Count > 0;

        public bool Execute(BuildContext context)
        {
            if (!CanExecute(context)) return false;

            applied.Clear();
            restored.Clear();
            WasPartial = false;

            float previousMultiplier = context.CostMultiplier;
            context.CostMultiplier = balance.rebuildCostFraction;

            try
            {
                List<GhostRegistry.Ghost> queue = ghosts.ByRebuildPriority();

                for (int i = 0; i < queue.Count; i++)
                {
                    GhostRegistry.Ghost ghost = queue[i];

                    var place = new PlaceBuildableCommand(ghost.Type, ghost.Origin, ghost.Dir);

                    // Stopping is wrong here, continuing is right. A cheap wall further down
                    // the queue may still be affordable after an expensive turret is not, and
                    // the player is better served by a more complete perimeter than by a
                    // rebuild that gave up at the first thing it could not pay for.
                    if (!place.Execute(context))
                    {
                        WasPartial = true;
                        continue;
                    }

                    applied.Add(place);
                    restored.Add(ghost);
                }

                for (int i = 0; i < restored.Count; i++)
                    ghosts.ClearAt(restored[i].Origin);
            }
            finally
            {
                context.CostMultiplier = previousMultiplier;
            }

            return restored.Count > 0;
        }

        public void Undo(BuildContext context)
        {
            // Present only to satisfy the interface. IsUndoable is false, so the stack never
            // calls this; implemented anyway so a future caller that ignores the flag still
            // gets correct behaviour rather than a silent no-op.
            for (int i = applied.Count - 1; i >= 0; i--)
                applied[i].Undo(context);

            for (int i = 0; i < restored.Count; i++)
            {
                GhostRegistry.Ghost ghost = restored[i];
                ghosts.Add(ghost.Type, ghost.Origin, ghost.Dir, ghost.DayLost);
            }

            applied.Clear();
            restored.Clear();
        }
    }
}
