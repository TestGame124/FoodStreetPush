using UnityEngine;

namespace FoodStreet.Navigation
{
    /// <summary>
    /// Coordinate math for the navigation grid.
    ///
    /// Two resolutions live here. The <b>build tile</b> is the placement unit and
    /// matches <see cref="GridBuildingSystem3D"/> one-for-one. The <b>nav cell</b> is
    /// the pathing unit: <see cref="Subdivision"/> of them per build tile per axis.
    /// A one-tile doorway is a single build tile but four nav cells wide, which is
    /// the resolution an agent needs to actually aim through it.
    ///
    /// Pure math, no state beyond the dimensions. Everything that stores per-cell
    /// data does so in a flat array indexed by <see cref="Index"/>.
    /// </summary>
    public sealed class NavGrid
    {
        /// <summary>Nav cells per build tile, per axis. 4 => 0.25u cells on a 1u tile.</summary>
        public const int Subdivision = 4;

        /// <summary>Cost of a straight step, in the fixed-point units the fields use.</summary>
        public const int StraightCost = 10;

        /// <summary>Cost of a diagonal step (10 * sqrt2, rounded).</summary>
        public const int DiagonalCost = 14;

        public static readonly Vector2Int[] Neighbours =
        {
            new Vector2Int( 0,  1), // N
            new Vector2Int( 1,  0), // E
            new Vector2Int( 0, -1), // S
            new Vector2Int(-1,  0), // W
            new Vector2Int( 1,  1), // NE
            new Vector2Int( 1, -1), // SE
            new Vector2Int(-1, -1), // SW
            new Vector2Int(-1,  1), // NW
        };

        /// <summary>Per-entry step cost for <see cref="Neighbours"/>. First four are straight.</summary>
        public static readonly int[] NeighbourCosts =
        {
            StraightCost, StraightCost, StraightCost, StraightCost,
            DiagonalCost, DiagonalCost, DiagonalCost, DiagonalCost,
        };

        public const int StraightNeighbourCount = 4;

        public int BuildWidth { get; }
        public int BuildHeight { get; }
        public float BuildCellSize { get; }
        public Vector3 Origin { get; }

        /// <summary>Nav-cell columns.</summary>
        public int Width { get; }

        /// <summary>Nav-cell rows.</summary>
        public int Height { get; }

        /// <summary>World size of one nav cell.</summary>
        public float CellSize { get; }

        public int CellCount { get; }

        public NavGrid(int buildWidth, int buildHeight, float buildCellSize, Vector3 origin)
        {
            BuildWidth = buildWidth;
            BuildHeight = buildHeight;
            BuildCellSize = buildCellSize;
            Origin = origin;

            Width = buildWidth * Subdivision;
            Height = buildHeight * Subdivision;
            CellSize = buildCellSize / Subdivision;
            CellCount = Width * Height;
        }

        public int Index(int x, int z) => z * Width + x;

        public int Index(Vector2Int cell) => cell.y * Width + cell.x;

        public void FromIndex(int index, out int x, out int z)
        {
            z = index / Width;
            x = index - z * Width;
        }

        public bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Height;

        public bool InBounds(Vector2Int cell) => InBounds(cell.x, cell.y);

        public bool BuildTileInBounds(int tx, int tz) =>
            tx >= 0 && tz >= 0 && tx < BuildWidth && tz < BuildHeight;

        /// <summary>World position -> nav cell. Not clamped; check <see cref="InBounds"/>.</summary>
        public Vector2Int WorldToCell(Vector3 world)
        {
            Vector3 local = world - Origin;
            return new Vector2Int(
                Mathf.FloorToInt(local.x / CellSize),
                Mathf.FloorToInt(local.z / CellSize));
        }

        /// <summary>Centre of a nav cell in world space. Y comes from the grid origin.</summary>
        public Vector3 CellToWorld(int x, int z) => new Vector3(
            Origin.x + (x + 0.5f) * CellSize,
            Origin.y,
            Origin.z + (z + 0.5f) * CellSize);

        public Vector3 CellToWorld(Vector2Int cell) => CellToWorld(cell.x, cell.y);

        /// <summary>World position -> build tile, matching GridXZ.GetXZ exactly.</summary>
        public Vector2Int WorldToBuildTile(Vector3 world)
        {
            Vector3 local = world - Origin;
            return new Vector2Int(
                Mathf.FloorToInt(local.x / BuildCellSize),
                Mathf.FloorToInt(local.z / BuildCellSize));
        }

        public Vector2Int CellToBuildTile(int x, int z) =>
            new Vector2Int(x / Subdivision, z / Subdivision);

        public Vector2Int CellToBuildTile(Vector2Int cell) => CellToBuildTile(cell.x, cell.y);

        /// <summary>Lowest-indexed nav cell inside a build tile.</summary>
        public Vector2Int BuildTileToCellOrigin(int tx, int tz) =>
            new Vector2Int(tx * Subdivision, tz * Subdivision);

        public Vector2Int ClampCell(Vector2Int cell) => new Vector2Int(
            Mathf.Clamp(cell.x, 0, Width - 1),
            Mathf.Clamp(cell.y, 0, Height - 1));
    }
}
