using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.Build
{
    /// <summary>
    /// Runs build commands and remembers them.
    ///
    /// Two separate records, because they answer different questions:
    ///
    /// The undo stack is short and time-limited. 6.3 gives the player a free undo on the
    /// last placement within five seconds, which exists to forgive a misplaced tap, not to
    /// let them rewind the day. Anything older has been paid for.
    ///
    /// The history is the full ordered log of what the restaurant is made of. That is what
    /// Rebuild All replays after a bad night, and what the save file can store instead of a
    /// bespoke layout format.
    /// </summary>
    public sealed class BuildCommandStack
    {
        readonly struct Entry
        {
            public readonly IBuildCommand Command;
            public readonly float RealTime;

            public Entry(IBuildCommand command, float realTime)
            {
                Command = command;
                RealTime = realTime;
            }
        }

        readonly BuildContext context;
        readonly float undoWindowSeconds;

        readonly List<Entry> undoStack = new List<Entry>();
        readonly List<IBuildCommand> history = new List<IBuildCommand>();

        /// <summary>Raised after any successful command, so the perimeter readout and the
        /// navigation grid refresh without polling.</summary>
        public event Action<IBuildCommand> OnCommandExecuted;

        public event Action<IBuildCommand> OnCommandUndone;

        public BuildCommandStack(BuildContext context, float undoWindowSeconds)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.undoWindowSeconds = undoWindowSeconds;
        }

        public IReadOnlyList<IBuildCommand> History => history;

        /// <summary>True while the last command is still inside its free-undo window. The
        /// undo button binds to this rather than to a timer of its own.</summary>
        public bool CanUndo
        {
            get
            {
                if (undoStack.Count == 0) return false;
                return Time.unscaledTime - undoStack[undoStack.Count - 1].RealTime <= undoWindowSeconds;
            }
        }

        public bool Execute(IBuildCommand command)
        {
            if (command == null) return false;

            if (!command.Execute(context))
                return false;

            history.Add(command);

            if (command.IsUndoable)
                undoStack.Add(new Entry(command, Time.unscaledTime));

            OnCommandExecuted?.Invoke(command);
            return true;
        }

        /// <summary>
        /// Reverses the last undoable command if it is still inside the window. Returns false
        /// rather than reversing something old, because a stale undo would be worse than no
        /// undo: the player would have moved on and the change would look like a bug.
        /// </summary>
        public bool UndoLast()
        {
            if (!CanUndo) return false;

            Entry entry = undoStack[undoStack.Count - 1];
            undoStack.RemoveAt(undoStack.Count - 1);

            entry.Command.Undo(context);

            // Removed from the tail of the history rather than by identity: the same command
            // instance could legitimately appear twice after a Rebuild All replay.
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(history[i], entry.Command))
                {
                    history.RemoveAt(i);
                    break;
                }
            }

            OnCommandUndone?.Invoke(entry.Command);
            return true;
        }

        /// <summary>
        /// Runs a batch as one unit. If any command fails, everything already applied is
        /// rolled back, so a partially affordable Rebuild All does not leave the restaurant
        /// half-restored with the materials gone.
        /// </summary>
        public bool ExecuteBatch(IReadOnlyList<IBuildCommand> commands)
        {
            if (commands == null || commands.Count == 0) return true;

            var applied = new List<IBuildCommand>(commands.Count);

            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i].Execute(context))
                {
                    applied.Add(commands[i]);
                    history.Add(commands[i]);
                    continue;
                }

                for (int j = applied.Count - 1; j >= 0; j--)
                {
                    applied[j].Undo(context);
                    history.Remove(applied[j]);
                }
                return false;
            }

            for (int i = 0; i < applied.Count; i++)
                OnCommandExecuted?.Invoke(applied[i]);

            return true;
        }

        /// <summary>Clears the free-undo window without touching history. Called on phase
        /// change: a placement made before dusk cannot be undone after the shutters drop,
        /// because the player has already seen the consequences of the layout.</summary>
        public void ExpireUndoWindow() => undoStack.Clear();

        public void ClearHistory()
        {
            history.Clear();
            undoStack.Clear();
        }
    }
}
