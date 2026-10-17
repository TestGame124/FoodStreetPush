using System.Collections.Generic;
using UnityEngine;

namespace FoodStreet.Navigation
{
    /// <summary>
    /// A walk between two points that goes round whatever stands on the nav grid - tables and their
    /// chairs, counters, stations, the diner's walls - rather than through it.
    ///
    /// For walkers that are given authored points (the visitors' doorway and chair, a worker's job)
    /// rather than steered by <see cref="PathFollower"/>: each leg between two points is asked of the
    /// grid's A* once, and walked as its turns. A leg that crosses nothing is left straight, and so is
    /// one the grid cannot help with - an end off the grid, or no way round at all - as walkers always
    /// did before.
    ///
    /// Stepping into one's own furniture is allowed: a chair's side lies inside the cells its table
    /// stands on, and getting there means stepping in from the edge. Stepping through anybody else's,
    /// or a wall, is not.
    /// </summary>
    public static class GridRoute
    {
        static readonly Path scratch = new Path();

        /// <summary>Metres between the samples a straight step is checked at.</summary>
        const float SampleSpacing = 0.1f;

        /// <summary>The grid's version, so a walker can tell its turns were planned on an older world.</summary>
        public static int Version
        {
            get
            {
                NavigationSystem nav = NavigationSystem.Instance;
                return nav != null && nav.Grid != null ? nav.Grid.Version : -1;
            }
        }

        /// <summary>
        /// The turns of a walk from <paramref name="from"/> to <paramref name="to"/> round what stands on
        /// the grid, ending with <paramref name="to"/> itself, into <paramref name="into"/>. False - walk it
        /// straight - when it crosses nothing anyway, or the grid cannot help.
        /// </summary>
        public static bool Plan(Vector3 from, Vector3 to, float radius, List<Vector3> into)
        {
            into.Clear();

            NavigationSystem nav = NavigationSystem.Instance;
            GridService grid = nav != null ? nav.Grid : null;
            if (grid == null || nav.Pathfinder == null) return false;
            if (!grid.Grid.InBounds(grid.Grid.WorldToCell(from)) || !grid.Grid.InBounds(grid.Grid.WorldToCell(to))) return false;
            if (StepIsClear(from, to)) return false;

            nav.Pathfinder.FindPath(from, to, radius, scratch);
            if (!scratch.IsUsable) return false;

            List<Vector3> turns = scratch.Waypoints;
            for (int i = 0; i < turns.Count; i++)
            {
                Vector3 p = turns[i];
                p.y = to.y;

                // The cell under their feet, and the cell of the point itself, are not turns.
                if (i == 0 && Flat(p - from) < 0.3f) continue;
                if (scratch.Status == PathStatus.Complete && i == turns.Count - 1) continue;
                into.Add(p);
            }

            into.Add(to);
            return true;
        }

        /// <summary>
        /// True if a straight step from <paramref name="from"/> to <paramref name="to"/> crosses only open
        /// floor, or the cells of what stands at either end - out of one's own table's footprint, into the
        /// one being stepped up to. Never through a wall. True off the grid, where there is nothing to ask.
        /// </summary>
        public static bool StepIsClear(Vector3 from, Vector3 to)
        {
            NavigationSystem nav = NavigationSystem.Instance;
            GridService grid = nav != null ? nav.Grid : null;
            if (grid == null) return true;

            NavGrid cells = grid.Grid;
            PlacedObject_Done atStart = grid.GetOccupantAt(from);
            PlacedObject_Done atEnd = grid.GetOccupantAt(to);

            Vector3 run = to - from;
            run.y = 0f;
            float length = run.magnitude;
            int samples = Mathf.Max(1, Mathf.CeilToInt(length / SampleSpacing));

            for (int s = 1; s <= samples; s++)
            {
                Vector3 p = from + run * (s / (float)samples);
                Vector2Int cell = cells.WorldToCell(p);
                if (!cells.InBounds(cell)) continue;

                int index = cells.Index(cell);
                if (grid.IsWalkable(index)) continue;
                if (grid.IsWall(index)) return false;

                PlacedObject_Done occupant = grid.GetOccupant(cell);
                if (occupant == null || (occupant != atStart && occupant != atEnd)) return false;
            }

            return true;
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
