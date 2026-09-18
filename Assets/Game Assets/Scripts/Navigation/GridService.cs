using System;
using UnityEngine;

namespace FoodStreet.Navigation
{
    [Flags]
    public enum CellFlags : byte
    {
        None = 0,

        /// <summary>An agent may stand here.</summary>
        Walkable = 1 << 0,

        /// <summary>A built floor tile, as opposed to bare ground.</summary>
        Floor = 1 << 1,

        /// <summary>A doorway. Walkable, and the reason nav cells are finer than build tiles.</summary>
        Door = 1 << 2,

        /// <summary>A wall segment. Blocked for pathing, but breachable by flow fields
        /// that pass a non-negative wall cost.</summary>
        Wall = 1 << 3,
    }

    /// <summary>
    /// The single source of truth for "what does the world look like to something that
    /// walks on it". Given a nav cell it answers three questions and nothing else:
    /// is it walkable, what object is on it, and what does it cost to enter.
    ///
    /// Everything downstream — A*, the flow fields, the steering layer — reads through
    /// here every time it needs an answer. None of them keep their own copy of the
    /// world, so there is exactly one place a stale grid can come from.
    ///
    /// Structural edits arrive through <see cref="MarkTileDirty"/> (wired to the build
    /// grid's change event). They are accumulated, not applied: <see cref="Rebuild"/>
    /// folds them in, and <see cref="NavigationSystem"/> decides how often that happens.
    /// </summary>
    public sealed class GridService
    {
        /// <summary>Cost of entering a built floor cell. Equals <see cref="NavGrid.StraightCost"/>
        /// so that a straight step across a floor costs exactly the A* heuristic's unit —
        /// the heuristic stays admissible only while this is the cheapest cell in the grid.</summary>
        public const byte FloorCost = NavGrid.StraightCost;

        /// <summary>Bare ground. Passable, but pricier than a floor, so paths drift onto
        /// the player's floors without ever being forced to.</summary>
        public const byte GroundCost = 16;

        public const byte MaxCost = 255;

        /// <summary>Clearance is capped here; past ~2 build tiles of space the exact
        /// number stops mattering to anything that reads it.</summary>
        public const byte MaxClearance = 15;

        /// <summary>Extra cost applied to cells with less than <see cref="ComfortClearance"/>
        /// free space, scaled by how tight they are. Keeps agents off the walls when
        /// there is room to spare, without ever making a gap impassable.</summary>
        public const int TightnessPenalty = 8;

        /// <summary>Clearance (in nav cells) at and above which a cell is considered open.</summary>
        public const int ComfortClearance = 3;

        public NavGrid Grid { get; }

        /// <summary>Bumped on every rebuild that changed something. Consumers compare
        /// against a stored value to know their last answer is stale.</summary>
        public int Version { get; private set; }

        readonly GridXZ<GridBuildingSystem3D.GridObject> source;

        // Per nav cell.
        readonly CellFlags[] flags;

        /// <summary>Terrain cost alone. Kept apart from <see cref="cost"/> because the
        /// clearance penalty is recomputed from scratch on every rebuild — folding it into
        /// one array would compound it against tiles that were not themselves dirty.</summary>
        readonly byte[] baseCost;

        readonly byte[] cost;
        readonly byte[] clearance;

        // Per build tile.
        readonly PlacedObject_Done[] occupant;
        readonly bool[] tileDirty;

        int dirtyTileCount;

        public GridService(NavGrid grid, GridXZ<GridBuildingSystem3D.GridObject> source)
        {
            Grid = grid;
            this.source = source;

            flags = new CellFlags[grid.CellCount];
            baseCost = new byte[grid.CellCount];
            cost = new byte[grid.CellCount];
            clearance = new byte[grid.CellCount];

            int tileCount = grid.BuildWidth * grid.BuildHeight;
            occupant = new PlacedObject_Done[tileCount];
            tileDirty = new bool[tileCount];

            MarkAllDirty();
            Rebuild();
        }

        #region The three questions

        public bool IsWalkable(int x, int z) =>
            Grid.InBounds(x, z) && (flags[Grid.Index(x, z)] & CellFlags.Walkable) != 0;

        public bool IsWalkable(Vector2Int cell) => IsWalkable(cell.x, cell.y);

        public bool IsWalkable(int index) => (flags[index] & CellFlags.Walkable) != 0;

        /// <summary>What is standing on this cell, or null for open ground.</summary>
        public PlacedObject_Done GetOccupant(int x, int z)
        {
            if (!Grid.InBounds(x, z)) return null;
            Vector2Int tile = Grid.CellToBuildTile(x, z);
            return occupant[tile.y * Grid.BuildWidth + tile.x];
        }

        public PlacedObject_Done GetOccupant(Vector2Int cell) => GetOccupant(cell.x, cell.y);

        public PlacedObject_Done GetOccupantAt(Vector3 world)
        {
            Vector2Int cell = Grid.WorldToCell(world);
            return GetOccupant(cell.x, cell.y);
        }

        /// <summary>Cost of entering this cell, where <see cref="NavGrid.StraightCost"/> is
        /// "one plain step". Blocked cells report <see cref="MaxCost"/> rather than throwing,
        /// so callers that only care about relative cost need no special case.</summary>
        public byte GetCost(int x, int z)
        {
            if (!Grid.InBounds(x, z)) return MaxCost;
            return cost[Grid.Index(x, z)];
        }

        public byte GetCost(Vector2Int cell) => GetCost(cell.x, cell.y);

        public byte GetCost(int index) => cost[index];

        #endregion

        #region Derived queries

        public CellFlags GetFlags(int index) => flags[index];

        public bool IsWall(int index) => (flags[index] & CellFlags.Wall) != 0;

        public bool IsDoor(int index) => (flags[index] & CellFlags.Door) != 0;

        /// <summary>Nav cells between this one and the nearest blocked cell, capped at
        /// <see cref="MaxClearance"/>. Zero on a blocked cell. Lets a caller ask for a
        /// path an agent of a given radius actually fits down.</summary>
        public byte GetClearance(int index) => clearance[index];

        public byte GetClearance(int x, int z) =>
            Grid.InBounds(x, z) ? clearance[Grid.Index(x, z)] : (byte)0;

        /// <summary>Clearance in nav cells needed to fit a body of the given world radius.</summary>
        public int ClearanceForRadius(float radius) =>
            Mathf.Max(1, Mathf.CeilToInt(radius / Grid.CellSize));

        /// <summary>
        /// Nearest walkable cell to <paramref name="cell"/> with at least the requested
        /// clearance, searched outwards in square rings. Used to make a destination
        /// inside a table or a wall mean "get as close as you can" instead of "no path".
        /// </summary>
        public bool TryFindNearestWalkable(Vector2Int cell, int minClearance, int maxRadius, out Vector2Int result)
        {
            cell = Grid.ClampCell(cell);
            if (IsWalkable(cell) && GetClearance(cell.x, cell.y) >= minClearance)
            {
                result = cell;
                return true;
            }

            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        // Ring only: skip the interior, already covered by a smaller r.
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;

                        int x = cell.x + dx;
                        int z = cell.y + dz;
                        if (!Grid.InBounds(x, z)) continue;
                        if (!IsWalkable(x, z)) continue;
                        if (GetClearance(x, z) < minClearance) continue;

                        result = new Vector2Int(x, z);
                        return true;
                    }
                }
            }

            result = cell;
            return false;
        }

        #endregion

        #region Dirty tracking

        public bool HasDirtyTiles => dirtyTileCount > 0;

        public void MarkTileDirty(int tx, int tz)
        {
            if (!Grid.BuildTileInBounds(tx, tz)) return;

            int i = tz * Grid.BuildWidth + tx;
            if (tileDirty[i]) return;

            tileDirty[i] = true;
            dirtyTileCount++;
        }

        public void MarkAllDirty()
        {
            for (int i = 0; i < tileDirty.Length; i++)
            {
                if (!tileDirty[i])
                {
                    tileDirty[i] = true;
                    dirtyTileCount++;
                }
            }
        }

        /// <summary>
        /// Folds accumulated tile edits into the cell data. Returns true if anything moved,
        /// in which case <see cref="Version"/> has advanced.
        /// </summary>
        public bool Rebuild()
        {
            if (dirtyTileCount == 0) return false;

            for (int tz = 0; tz < Grid.BuildHeight; tz++)
            {
                for (int tx = 0; tx < Grid.BuildWidth; tx++)
                {
                    int t = tz * Grid.BuildWidth + tx;
                    if (!tileDirty[t]) continue;

                    tileDirty[t] = false;
                    RebuildTile(tx, tz, t);
                }
            }

            dirtyTileCount = 0;

            // Clearance is a distance transform, so a single changed tile can shift values
            // several cells away. Cheaper to redo the whole field than to bound the ripple.
            RebuildClearance();
            ApplyTightnessCost();

            Version++;
            return true;
        }

        void RebuildTile(int tx, int tz, int tileIndex)
        {
            GridBuildingSystem3D.GridObject gridObject = source.GetGridObject(tx, tz);
            PlacedObject_Done placed = gridObject?.GetPlacedObject();
            occupant[tileIndex] = placed;

            CellFlags tileFlags;
            byte tileCost;
            ClassifyTile(placed, out tileFlags, out tileCost);

            Vector2Int origin = Grid.BuildTileToCellOrigin(tx, tz);
            for (int dz = 0; dz < NavGrid.Subdivision; dz++)
            {
                int rowStart = Grid.Index(origin.x, origin.y + dz);
                for (int dx = 0; dx < NavGrid.Subdivision; dx++)
                {
                    flags[rowStart + dx] = tileFlags;
                    baseCost[rowStart + dx] = tileCost;
                }
            }
        }

        static void ClassifyTile(PlacedObject_Done placed, out CellFlags tileFlags, out byte tileCost)
        {
            if (placed == null)
            {
                tileFlags = CellFlags.Walkable;
                tileCost = GroundCost;
                return;
            }

            PlacedObjectTypeSO type = placed.PlacedObjectTypeSO;
            ObjectType objectType = type != null ? type.type : placed.shopItemType;

            switch (objectType)
            {
                case ObjectType.Door:
                    tileFlags = CellFlags.Walkable | CellFlags.Floor | CellFlags.Door;
                    tileCost = FloorCost;
                    return;

                case ObjectType.Wall:
                case ObjectType.BigWall:
                    tileFlags = CellFlags.Wall;
                    tileCost = MaxCost;
                    return;
            }

            bool isFloor = type != null
                ? type.categoryType == ObjectCategory.Floor
                : objectType == ObjectType.Floor1 || objectType == ObjectType.WoodenFloor;

            if (isFloor)
            {
                tileFlags = CellFlags.Walkable | CellFlags.Floor;
                tileCost = FloorCost;
                return;
            }

            // Furniture, stoves, pots, props: solid, and not something you can break through.
            tileFlags = CellFlags.None;
            tileCost = MaxCost;
        }

        /// <summary>
        /// Two-pass chamfer distance transform over the whole grid. Blocked cells are 0;
        /// every walkable cell ends up holding its Chebyshev distance to the nearest
        /// blocked cell, saturated at <see cref="MaxClearance"/>.
        /// </summary>
        void RebuildClearance()
        {
            int w = Grid.Width;
            int h = Grid.Height;

            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = z * w + x;
                    if (!IsWalkable(i))
                    {
                        clearance[i] = 0;
                        continue;
                    }

                    // Off-grid counts as blocked so agents do not hug the world edge.
                    int best = MaxClearance;
                    if (x == 0 || z == 0) best = 1;
                    else
                    {
                        best = Mathf.Min(best, clearance[i - 1] + 1);
                        best = Mathf.Min(best, clearance[i - w] + 1);
                        best = Mathf.Min(best, clearance[i - w - 1] + 1);
                        if (x + 1 < w) best = Mathf.Min(best, clearance[i - w + 1] + 1);
                    }

                    clearance[i] = (byte)Mathf.Min(best, MaxClearance);
                }
            }

            for (int z = h - 1; z >= 0; z--)
            {
                for (int x = w - 1; x >= 0; x--)
                {
                    int i = z * w + x;
                    if (clearance[i] == 0) continue;

                    int best = clearance[i];
                    if (x == w - 1 || z == h - 1) best = 1;
                    else
                    {
                        best = Mathf.Min(best, clearance[i + 1] + 1);
                        best = Mathf.Min(best, clearance[i + w] + 1);
                        best = Mathf.Min(best, clearance[i + w + 1] + 1);
                        if (x > 0) best = Mathf.Min(best, clearance[i + w - 1] + 1);
                    }

                    clearance[i] = (byte)Mathf.Min(best, MaxClearance);
                }
            }
        }

        /// <summary>Rebuilds the final cost as terrain plus a tightness penalty, once
        /// clearance is known. Never penalises a doorway — a door is meant to be squeezed
        /// through, and taxing it would send agents around the building.</summary>
        void ApplyTightnessCost()
        {
            for (int i = 0; i < cost.Length; i++)
            {
                byte terrain = baseCost[i];

                if (!IsWalkable(i) || IsDoor(i))
                {
                    cost[i] = terrain;
                    continue;
                }

                int room = clearance[i];
                if (room >= ComfortClearance)
                {
                    cost[i] = terrain;
                    continue;
                }

                int penalty = TightnessPenalty * (ComfortClearance - room);
                cost[i] = (byte)Mathf.Min(terrain + penalty, MaxCost - 1);
            }
        }

        #endregion
    }
}
