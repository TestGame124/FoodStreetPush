using System.Collections.Generic;
using UnityEngine;

namespace FoodStreet.Navigation
{
    public enum PathStatus
    {
        /// <summary>No route exists even to an approximation. The waypoint list is empty.</summary>
        Failed,

        /// <summary>The path reaches the requested cell.</summary>
        Complete,

        /// <summary>The goal was unreachable or blocked; the path ends at the closest
        /// point the search got to. Customers walking to a chair get this every time,
        /// because the chair's own tile is solid.</summary>
        Partial,
    }

    public sealed class Path
    {
        public readonly List<Vector3> Waypoints = new List<Vector3>();
        public PathStatus Status;

        /// <summary><see cref="GridService.Version"/> at the time this was computed.
        /// A follower compares it to decide whether the world moved underneath it.</summary>
        public int GridVersion;

        public bool IsUsable => Status != PathStatus.Failed && Waypoints.Count > 0;

        public void Reset()
        {
            Waypoints.Clear();
            Status = PathStatus.Failed;
            GridVersion = -1;
        }
    }

    /// <summary>
    /// A* over <see cref="GridService"/>'s nav cells. Eight-directional, corner-cutting
    /// disabled, octile heuristic.
    ///
    /// One instance serves everything that walks — customers, servers, Survivors — and it
    /// is single-threaded by design: the working arrays are allocated once at construction
    /// and reused between searches, with a generation stamp standing in for clearing them.
    /// Call it from the main thread only.
    /// </summary>
    public sealed class AStarPathfinder
    {
        /// <summary>Ceiling on cells expanded per search. A 50x50 build grid is 200x200 nav
        /// cells; this lets a search sweep most of it before giving up on a walled-off goal.</summary>
        public int MaxExpansions = 20000;

        /// <summary>How far out to look for a stand-in when the requested cell is solid,
        /// in nav cells.</summary>
        public int GoalSearchRadius = NavGrid.Subdivision * 4;

        readonly GridService svc;
        readonly NavGrid grid;

        readonly int[] gScore;
        readonly int[] cameFrom;
        readonly int[] stamp;
        readonly bool[] closed;
        readonly MinHeap open = new MinHeap(512);

        int currentStamp;

        public AStarPathfinder(GridService service)
        {
            svc = service;
            grid = service.Grid;

            gScore = new int[grid.CellCount];
            cameFrom = new int[grid.CellCount];
            stamp = new int[grid.CellCount];
            closed = new bool[grid.CellCount];
        }

        /// <summary>
        /// Fills <paramref name="result"/> with world-space waypoints from
        /// <paramref name="from"/> to <paramref name="to"/>.
        /// </summary>
        /// <param name="agentRadius">Body radius. Cells too tight for it are skipped, so an
        /// agent is never routed down a gap it cannot physically enter.</param>
        public void FindPath(Vector3 from, Vector3 to, float agentRadius, Path result)
        {
            result.Reset();
            result.GridVersion = svc.Version;

            int minClearance = svc.ClearanceForRadius(agentRadius);

            if (!TryResolveEndpoint(from, minClearance, out Vector2Int startCell, out _))
            {
                // Standing inside geometry. Fall back to a body-less fit so the agent can
                // at least walk itself out rather than freezing where it is.
                if (!TryResolveEndpoint(from, 1, out startCell, out _)) return;
                minClearance = 1;
            }

            bool goalStandable = TryResolveEndpoint(to, minClearance, out Vector2Int goalCell, out bool goalUnmoved);
            if (!goalStandable && !TryResolveEndpoint(to, 1, out goalCell, out goalUnmoved)) return;

            int startIndex = grid.Index(startCell);
            int goalIndex = grid.Index(goalCell);

            if (startIndex == goalIndex)
            {
                result.Status = goalUnmoved ? PathStatus.Complete : PathStatus.Partial;
                result.Waypoints.Add(goalUnmoved
                    ? FlattenTo(grid.CellToWorld(goalCell), to)
                    : grid.CellToWorld(goalCell));
                return;
            }

            int reached = Search(startIndex, goalIndex, minClearance);
            BuildWaypoints(startIndex, reached, minClearance, result);

            if (result.Waypoints.Count == 0)
            {
                result.Status = PathStatus.Failed;
                return;
            }

            bool exact = reached == goalIndex && goalUnmoved;
            result.Status = exact ? PathStatus.Complete : PathStatus.Partial;

            // Land on the caller's actual point rather than the centre of its cell, when
            // that point is somewhere an agent can stand.
            if (exact)
            {
                result.Waypoints[result.Waypoints.Count - 1] =
                    FlattenTo(grid.CellToWorld(goalCell), to);
            }
        }

        /// <summary>Runs the search. Returns the cell the path should be reconstructed from —
        /// the goal when it was reached, otherwise the expanded cell closest to it — or -1
        /// if nothing was expanded at all.</summary>
        int Search(int startIndex, int goalIndex, int minClearance)
        {
            currentStamp++;
            open.Clear();

            grid.FromIndex(goalIndex, out int goalX, out int goalZ);

            Touch(startIndex);
            gScore[startIndex] = 0;
            cameFrom[startIndex] = -1;
            open.Push(startIndex, Heuristic(startIndex, goalX, goalZ));

            int bestCell = startIndex;
            int bestHeuristic = Heuristic(startIndex, goalX, goalZ);
            int expansions = 0;

            while (open.Count > 0)
            {
                int current = open.Pop(out _);
                if (closed[current]) continue;
                closed[current] = true;

                if (current == goalIndex) return current;

                if (++expansions > MaxExpansions) break;

                grid.FromIndex(current, out int cx, out int cz);
                int currentG = gScore[current];

                for (int n = 0; n < NavGrid.Neighbours.Length; n++)
                {
                    Vector2Int step = NavGrid.Neighbours[n];
                    int nx = cx + step.x;
                    int nz = cz + step.y;

                    if (!grid.InBounds(nx, nz)) continue;

                    int neighbour = grid.Index(nx, nz);
                    if (closed[neighbour]) continue;
                    if (!svc.IsWalkable(neighbour)) continue;
                    if (svc.GetClearance(neighbour) < minClearance) continue;

                    // Corner-cutting disabled: a diagonal is only legal when both of the
                    // straight steps that bracket it are open. Without this an agent slips
                    // through the seam where two walls meet at a corner.
                    if (n >= NavGrid.StraightNeighbourCount)
                    {
                        if (!IsOpen(cx + step.x, cz, minClearance)) continue;
                        if (!IsOpen(cx, cz + step.y, minClearance)) continue;
                    }

                    int tentative = currentG +
                        NavGrid.NeighbourCosts[n] * svc.GetCost(neighbour) / NavGrid.StraightCost;

                    if (stamp[neighbour] == currentStamp && tentative >= gScore[neighbour]) continue;

                    Touch(neighbour);
                    gScore[neighbour] = tentative;
                    cameFrom[neighbour] = current;

                    int h = Heuristic(neighbour, goalX, goalZ);
                    if (h < bestHeuristic)
                    {
                        bestHeuristic = h;
                        bestCell = neighbour;
                    }

                    open.Push(neighbour, tentative + h);
                }
            }

            return bestCell;
        }

        bool IsOpen(int x, int z, int minClearance) =>
            svc.IsWalkable(x, z) && svc.GetClearance(x, z) >= minClearance;

        /// <summary>Marks a cell as belonging to the current search, so the score arrays can
        /// carry junk from the previous one instead of being cleared every time.</summary>
        void Touch(int index)
        {
            if (stamp[index] == currentStamp) return;
            stamp[index] = currentStamp;
            closed[index] = false;
            gScore[index] = int.MaxValue;
            cameFrom[index] = -1;
        }

        static int Octile(int dx, int dz)
        {
            int min = dx < dz ? dx : dz;
            return NavGrid.StraightCost * (dx + dz) +
                   (NavGrid.DiagonalCost - 2 * NavGrid.StraightCost) * min;
        }

        int Heuristic(int index, int goalX, int goalZ)
        {
            grid.FromIndex(index, out int x, out int z);
            return Octile(Mathf.Abs(x - goalX), Mathf.Abs(z - goalZ));
        }

        #region Reconstruction and smoothing

        static readonly List<int> scratchCells = new List<int>(256);

        void BuildWaypoints(int startIndex, int endIndex, int minClearance, Path result)
        {
            scratchCells.Clear();
            for (int c = endIndex; c != -1; c = cameFrom[c])
            {
                scratchCells.Add(c);
                if (c == startIndex) break;
            }
            scratchCells.Reverse();

            // String-pulling: keep a waypoint only where the straight line finally breaks.
            // Raw grid output is one waypoint per 0.25u cell, which reads as a staircase
            // no matter how smooth the steering on top of it is.
            int anchor = 0;
            for (int i = 2; i < scratchCells.Count; i++)
            {
                if (HasLineOfSight(scratchCells[anchor], scratchCells[i], minClearance)) continue;

                result.Waypoints.Add(grid.CellToWorld(CellOf(scratchCells[i - 1])));
                anchor = i - 1;
            }

            if (scratchCells.Count > 1)
            {
                result.Waypoints.Add(grid.CellToWorld(CellOf(scratchCells[scratchCells.Count - 1])));
            }
        }

        Vector2Int CellOf(int index)
        {
            grid.FromIndex(index, out int x, out int z);
            return new Vector2Int(x, z);
        }

        /// <summary>
        /// Supercover line walk between two cells: unlike plain Bresenham it visits every
        /// cell the segment touches, so a line that clips the corner of a wall is rejected.
        /// </summary>
        bool HasLineOfSight(int fromIndex, int toIndex, int minClearance)
        {
            grid.FromIndex(fromIndex, out int x0, out int z0);
            grid.FromIndex(toIndex, out int x1, out int z1);

            int dx = Mathf.Abs(x1 - x0);
            int dz = Mathf.Abs(z1 - z0);
            int sx = x0 < x1 ? 1 : -1;
            int sz = z0 < z1 ? 1 : -1;

            int x = x0;
            int z = z0;
            int error = dx - dz;

            while (x != x1 || z != z1)
            {
                int doubled = error * 2;

                if (doubled > -dz && doubled < dx)
                {
                    // Diagonal step. Both bracketing cells must be open or the line grazes
                    // a corner the agent cannot actually pass through.
                    if (!IsOpen(x + sx, z, minClearance)) return false;
                    if (!IsOpen(x, z + sz, minClearance)) return false;
                }

                if (doubled > -dz)
                {
                    error -= dz;
                    x += sx;
                }
                if (doubled < dx)
                {
                    error += dx;
                    z += sz;
                }

                if (!IsOpen(x, z, minClearance)) return false;
            }

            return true;
        }

        /// <summary>Takes XZ from the cell centre and Y from the caller's target, so agents
        /// keep whatever ground height the scene author gave them.</summary>
        static Vector3 FlattenTo(Vector3 cellCentre, Vector3 target) =>
            new Vector3(target.x, cellCentre.y, target.z);

        /// <param name="unmoved">True when the requested cell itself was usable, i.e. the
        /// resolver did not have to substitute a neighbour.</param>
        bool TryResolveEndpoint(Vector3 world, int minClearance, out Vector2Int cell, out bool unmoved)
        {
            Vector2Int raw = grid.WorldToCell(world);
            bool found = svc.TryFindNearestWalkable(raw, minClearance, GoalSearchRadius, out cell);
            unmoved = found && cell == grid.ClampCell(raw);
            return found;
        }

        #endregion
    }
}
