using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.Consequence
{
    /// <summary>
    /// What a destroyed structure leaves behind (15.2).
    ///
    /// Not rubble. Rubble blocks the tile and turns recovery into cleanup. A ghost is an
    /// outline on a tile that is otherwise completely free: the player can build something
    /// else there, or tap once to restore what was lost at 40 percent of its original cost.
    ///
    /// This is the mechanism behind the claim that failure costs time and never progress.
    /// The layout is never destroyed, only unpaid-for. Nobody minds paying; everybody minds
    /// re-placing twelve tables.
    /// </summary>
    public sealed class GhostRegistry
    {
        public readonly struct Ghost
        {
            public readonly PlacedObjectTypeSO Type;
            public readonly Vector2Int Origin;
            public readonly PlacedObjectTypeSO.Dir Dir;

            /// <summary>The day it was lost. Lets the dawn report say "last night" versus
            /// "still unrepaired from three nights ago", which reads very differently.</summary>
            public readonly int DayLost;

            public Ghost(PlacedObjectTypeSO type, Vector2Int origin, PlacedObjectTypeSO.Dir dir, int dayLost)
            {
                Type = type;
                Origin = origin;
                Dir = dir;
                DayLost = dayLost;
            }
        }

        readonly List<Ghost> ghosts = new List<Ghost>();

        public IReadOnlyList<Ghost> Ghosts => ghosts;

        public int Count => ghosts.Count;

        /// <summary>Raised when a structure is lost, so the outline visual can be spawned.</summary>
        public event Action<Ghost> OnGhostAdded;

        /// <summary>Raised when one is rebuilt or built over, so the outline can be removed.</summary>
        public event Action<Ghost> OnGhostCleared;

        public void Add(PlacedObjectTypeSO type, Vector2Int origin, PlacedObjectTypeSO.Dir dir, int dayLost)
        {
            if (type == null) return;

            var ghost = new Ghost(type, origin, dir, dayLost);
            ghosts.Add(ghost);
            OnGhostAdded?.Invoke(ghost);
        }

        /// <summary>
        /// Drops the ghost occupying a tile, whether because it was rebuilt or because the
        /// player chose to build something else there. A ghost never prevents a placement -
        /// it is a suggestion, not an obstacle.
        /// </summary>
        public void ClearAt(Vector2Int origin)
        {
            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                if (ghosts[i].Origin != origin) continue;

                Ghost ghost = ghosts[i];
                ghosts.RemoveAt(i);
                OnGhostCleared?.Invoke(ghost);
                return;
            }
        }

        public bool TryGetAt(Vector2Int origin, out Ghost ghost)
        {
            for (int i = 0; i < ghosts.Count; i++)
            {
                if (ghosts[i].Origin == origin)
                {
                    ghost = ghosts[i];
                    return true;
                }
            }

            ghost = default;
            return false;
        }

        /// <summary>
        /// Ghosts ordered for a partial rebuild, most important first.
        ///
        /// 15.3 promises that a player who cannot afford Rebuild All is offered a partial
        /// rebuild by priority rather than being told no. Perimeter comes first because an
        /// unsealed wall is what makes tomorrow night worse; production next because it is
        /// what earns the money to finish the job; comfort last because it only multiplies
        /// a number that is already small after a bad night.
        /// </summary>
        public List<Ghost> ByRebuildPriority()
        {
            var ordered = new List<Ghost>(ghosts);
            ordered.Sort((a, b) => Priority(a).CompareTo(Priority(b)));
            return ordered;
        }

        static int Priority(Ghost ghost)
        {
            PlacedObjectTypeSO type = ghost.Type;
            if (type == null) return 99;

            if (type.isHearth) return 0;
            if (type.isWall || type.isDoor || type.isPerimeter) return 1;

            switch (type.buildCategory)
            {
                case Data.BuildCategory.Defense: return 2;
                case Data.BuildCategory.Production: return 3;
                case Data.BuildCategory.Utility: return 4;
                case Data.BuildCategory.Service: return 5;
                case Data.BuildCategory.Structure: return 6;
                default: return 7;
            }
        }

        public void Clear() => ghosts.Clear();
    }
}
