using System.Collections.Generic;
using UnityEngine;

namespace FoodStreet.Navigation
{
    /// <summary>
    /// The four fields the game asks for. They are the same algorithm four times over;
    /// only the seeds and <see cref="FlowFieldSettings.WallCost"/> differ.
    /// </summary>
    public enum FlowFieldId
    {
        /// <summary>Route to the objective, walls solid. What anything law-abiding uses.</summary>
        Doors = 0,

        /// <summary>Walls passable but expensive: use a door if there is one, otherwise
        /// come through the shell.</summary>
        Breach = 1,

        /// <summary>Walls barely more expensive than open ground, so the field pools at
        /// whatever stretch of wall is thinnest or least defended.</summary>
        WeakestPoint = 2,

        /// <summary>Route to the stores, walls solid. Used by staff, and by anything that
        /// has already got inside.</summary>
        Pantry = 3,
    }

    public struct FlowFieldSettings
    {
        /// <summary>Cost of stepping into a wall cell, in the same units as
        /// <see cref="GridService.GetCost"/>. Negative means walls are impassable, which is
        /// the only thing separating <see cref="FlowFieldId.Doors"/> from
        /// <see cref="FlowFieldId.Breach"/>.</summary>
        public int WallCost;

        /// <summary>Body radius the field is generated for. Cells too tight are excluded.</summary>
        public float AgentRadius;

        public static FlowFieldSettings For(FlowFieldId id)
        {
            switch (id)
            {
                case FlowFieldId.Breach:
                    // Roughly twenty plain steps to chew through a wall: worth it only when
                    // the way round is genuinely long.
                    return new FlowFieldSettings { WallCost = 200, AgentRadius = 0.3f };

                case FlowFieldId.WeakestPoint:
                    // Cheap enough that thickness dominates, so the field naturally finds
                    // the shortest run of wall rather than the nearest one.
                    return new FlowFieldSettings { WallCost = 40, AgentRadius = 0.3f };

                case FlowFieldId.Pantry:
                case FlowFieldId.Doors:
                default:
                    return new FlowFieldSettings { WallCost = -1, AgentRadius = 0.3f };
            }
        }
    }

    /// <summary>
    /// Multi-source flow field: BFS out from every seed at once into an integration field,
    /// then one pass to collapse that into a direction per cell.
    ///
    /// The integration pass is a Dijkstra relaxation run off a plain queue rather than a
    /// heap. Cell costs sit in a narrow band (10 to a few hundred), so the re-enqueue count
    /// stays low and the queue beats a priority queue on a grid this size.
    ///
    /// One field serves any number of agents, which is the point: a hundred Survivors
    /// converging on the same objective cost one generation between them, not a hundred
    /// A* searches.
    /// </summary>
    public sealed class FlowField
    {
        public const int Unreachable = int.MaxValue;
        const byte NoDirection = 255;

        public FlowFieldId Id { get; }
        public FlowFieldSettings Settings;

        /// <summary><see cref="GridService.Version"/> this field was generated against.</summary>
        public int GridVersion { get; private set; } = -1;

        /// <summary>False until the first generation with at least one valid seed.</summary>
        public bool IsValid { get; private set; }

        readonly GridService svc;
        readonly NavGrid grid;

        readonly int[] integration;
        readonly byte[] direction;
        readonly bool[] queued;
        readonly Queue<int> frontier = new Queue<int>(1024);

        public FlowField(FlowFieldId id, GridService service)
        {
            Id = id;
            Settings = FlowFieldSettings.For(id);

            svc = service;
            grid = service.Grid;

            integration = new int[grid.CellCount];
            direction = new byte[grid.CellCount];
            queued = new bool[grid.CellCount];
        }

        /// <summary>Integrated cost from this cell to the nearest seed, or
        /// <see cref="Unreachable"/>. Useful on its own — it is a distance-to-objective
        /// readout, which is what a "how exposed is this tile" query wants.</summary>
        public int GetIntegratedCost(Vector2Int cell) =>
            grid.InBounds(cell) ? integration[grid.Index(cell)] : Unreachable;

        /// <summary>Unit direction to follow from this cell, or zero at a seed, an
        /// unreachable cell, or off-grid.</summary>
        public Vector3 GetDirection(Vector2Int cell)
        {
            if (!grid.InBounds(cell)) return Vector3.zero;

            byte d = direction[grid.Index(cell)];
            if (d == NoDirection) return Vector3.zero;

            Vector2Int step = NavGrid.Neighbours[d];
            Vector3 dir = new Vector3(step.x, 0f, step.y);
            return dir.normalized;
        }

        public Vector3 GetDirection(Vector3 world) => GetDirection(grid.WorldToCell(world));

        /// <summary>
        /// Regenerates from scratch against the current state of <see cref="GridService"/>.
        /// Seeds are build-tile coordinates, because that is the granularity the game
        /// thinks in — every nav cell inside a seed tile becomes a source.
        /// </summary>
        public void Generate(IReadOnlyList<Vector2Int> seedTiles)
        {
            GridVersion = svc.Version;
            IsValid = false;

            for (int i = 0; i < integration.Length; i++)
            {
                integration[i] = Unreachable;
                direction[i] = NoDirection;
                queued[i] = false;
            }

            frontier.Clear();

            if (seedTiles == null || seedTiles.Count == 0) return;

            int minClearance = svc.ClearanceForRadius(Settings.AgentRadius);

            for (int s = 0; s < seedTiles.Count; s++)
            {
                Vector2Int tile = seedTiles[s];
                if (!grid.BuildTileInBounds(tile.x, tile.y)) continue;

                Vector2Int origin = grid.BuildTileToCellOrigin(tile.x, tile.y);
                for (int dz = 0; dz < NavGrid.Subdivision; dz++)
                {
                    for (int dx = 0; dx < NavGrid.Subdivision; dx++)
                    {
                        int index = grid.Index(origin.x + dx, origin.y + dz);
                        if (!Passable(index, minClearance)) continue;

                        integration[index] = 0;
                        if (queued[index]) continue;

                        queued[index] = true;
                        frontier.Enqueue(index);
                        IsValid = true;
                    }
                }
            }

            if (!IsValid) return;

            Integrate(minClearance);
            BuildDirections(minClearance);
        }

        void Integrate(int minClearance)
        {
            while (frontier.Count > 0)
            {
                int current = frontier.Dequeue();
                queued[current] = false;

                int currentCost = integration[current];
                grid.FromIndex(current, out int cx, out int cz);

                for (int n = 0; n < NavGrid.Neighbours.Length; n++)
                {
                    Vector2Int step = NavGrid.Neighbours[n];
                    int nx = cx + step.x;
                    int nz = cz + step.y;

                    if (!grid.InBounds(nx, nz)) continue;

                    int neighbour = grid.Index(nx, nz);
                    if (!Passable(neighbour, minClearance)) continue;

                    // Same corner rule as A*. Without it the field points agents diagonally
                    // through the join between two walls and they grind against it.
                    if (n >= NavGrid.StraightNeighbourCount)
                    {
                        if (!Passable(cx + step.x, cz, minClearance)) continue;
                        if (!Passable(cx, cz + step.y, minClearance)) continue;
                    }

                    int candidate = currentCost +
                        NavGrid.NeighbourCosts[n] * EnterCost(neighbour) / NavGrid.StraightCost;

                    if (candidate >= integration[neighbour]) continue;

                    integration[neighbour] = candidate;
                    if (queued[neighbour]) continue;

                    queued[neighbour] = true;
                    frontier.Enqueue(neighbour);
                }
            }
        }

        void BuildDirections(int minClearance)
        {
            for (int z = 0; z < grid.Height; z++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    int index = grid.Index(x, z);
                    if (integration[index] == Unreachable) continue;
                    if (integration[index] == 0) continue; // A seed. Already there.

                    int best = integration[index];
                    byte bestDir = NoDirection;

                    for (int n = 0; n < NavGrid.Neighbours.Length; n++)
                    {
                        Vector2Int step = NavGrid.Neighbours[n];
                        int nx = x + step.x;
                        int nz = z + step.y;

                        if (!grid.InBounds(nx, nz)) continue;

                        int neighbour = grid.Index(nx, nz);
                        if (integration[neighbour] >= best) continue;
                        if (!Passable(neighbour, minClearance)) continue;

                        if (n >= NavGrid.StraightNeighbourCount)
                        {
                            if (!Passable(x + step.x, z, minClearance)) continue;
                            if (!Passable(x, z + step.y, minClearance)) continue;
                        }

                        best = integration[neighbour];
                        bestDir = (byte)n;
                    }

                    direction[index] = bestDir;
                }
            }
        }

        bool Passable(int x, int z, int minClearance) =>
            grid.InBounds(x, z) && Passable(grid.Index(x, z), minClearance);

        bool Passable(int index, int minClearance)
        {
            if (svc.IsWalkable(index)) return svc.GetClearance(index) >= minClearance;

            // Walls are the only blocker the wall-cost knob applies to. Furniture stays
            // solid in every field — a Survivor smashing through a stove is not the
            // behaviour anyone is asking for.
            return Settings.WallCost >= 0 && svc.IsWall(index);
        }

        int EnterCost(int index) =>
            svc.IsWalkable(index) ? svc.GetCost(index) : Settings.WallCost;
    }
}
