using System;
using System.Collections.Generic;
using FoodStreet.Navigation;
using UnityEngine;

namespace LastOrders.Perimeter
{
    /// <summary>
    /// A single reading of the restaurant's two competing numbers. Returned by value so the
    /// UI can diff a preview against the current state without either being mutated.
    /// </summary>
    public readonly struct PerimeterReadout
    {
        /// <summary>Total customers that can be seated at once.</summary>
        public readonly int SeatingCapacity;

        /// <summary>Doors in the perimeter. The input cap on everything: customers enter
        /// only through doors, and each admission takes 90 clock-seconds.</summary>
        public readonly int DoorCount;

        /// <summary>0 to 1. How sound the shell is, weighted by wall health.</summary>
        public readonly float Integrity;

        /// <summary>Perimeter tiles with no wall or door at all. A hole is worse than a weak
        /// wall, so these are called out separately rather than averaged into integrity.</summary>
        public readonly int Gaps;

        public PerimeterReadout(int seating, int doors, float integrity, int gaps)
        {
            SeatingCapacity = seating;
            DoorCount = doors;
            Integrity = integrity;
            Gaps = gaps;
        }

        public static PerimeterReadout operator -(PerimeterReadout a, PerimeterReadout b) =>
            new PerimeterReadout(
                a.SeatingCapacity - b.SeatingCapacity,
                a.DoorCount - b.DoorCount,
                a.Integrity - b.Integrity,
                a.Gaps - b.Gaps);
    }

    /// <summary>
    /// The load-bearing rule of the build layer (7), and the reason the two genres fuse
    /// rather than sit side by side.
    ///
    /// Revenue capacity and defensive integrity are the same variable, expressed in doors.
    /// A door is how customers get in and it is the softest point in any wall. Barring it
    /// makes it as strong as the wall and drops throughput to zero. Nothing about airlocks,
    /// service corridors, decoy entrances or interior mazing is authored - all of it emerges
    /// from that one rule, which is why this service computes rather than prescribes.
    ///
    /// The most important thing here is not the simulation, it is
    /// <see cref="Preview"/>: the GDD names delivering this trade-off at the moment of
    /// decision - live, during the drag - as "the single most important UX job in the game".
    /// </summary>
    public sealed class PerimeterService
    {
        readonly List<Vector2Int> doorTiles = new List<Vector2Int>();
        readonly List<Vector2Int> wallTiles = new List<Vector2Int>();
        readonly List<Vector2Int> pantryTiles = new List<Vector2Int>();

        PerimeterReadout current;

        /// <summary>Raised after any recompute, so the top bar and the build readout refresh
        /// without polling the grid every frame.</summary>
        public event Action<PerimeterReadout> OnReadoutChanged;

        public PerimeterReadout Current => current;

        public IReadOnlyList<Vector2Int> DoorTiles => doorTiles;

        public IReadOnlyList<Vector2Int> PantryTiles => pantryTiles;

        /// <summary>
        /// The restaurant's way in, in world space, for when no door stands on the build grid - a
        /// restaurant whose walls and door are scene pieces rather than placed objects. Three of the
        /// four fields seed from doors; with no door and nothing here they had no objective at all,
        /// and every zombie stood where it appeared for the whole night.
        /// </summary>
        public Func<IReadOnlyList<Vector3>> FallbackEntrances { get; set; }

        readonly List<Vector2Int> entranceTiles = new List<Vector2Int>();

        /// <summary>
        /// Rescans the grid. Driven by the build command stack's executed event rather than
        /// by Update, because the grid only changes when a command changes it.
        /// </summary>
        public void Recompute()
        {
            doorTiles.Clear();
            wallTiles.Clear();
            pantryTiles.Clear();

            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();
            if (grid == null) return;

            int seating = 0;
            float healthSum = 0f;
            float healthMax = 0f;

            var counted = new HashSet<PlacedObject_Done>();

            int width = grid.GetWidth();
            int height = grid.GetHeight();

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    PlacedObject_Done occupant = grid.GetGridObject(x, z)?.GetPlacedObject();
                    if (occupant == null) continue;

                    PlacedObjectTypeSO type = occupant.PlacedObjectTypeSO;
                    if (type == null) continue;

                    var tile = new Vector2Int(x, z);

                    if (type.isDoor) doorTiles.Add(tile);
                    if (type.isWall) wallTiles.Add(tile);
                    if (type.isPantry) pantryTiles.Add(tile);

                    // Multi-tile objects appear once per occupied tile. Seats and health are
                    // per object, so they are counted against the instance, not the tile.
                    if (!counted.Add(occupant)) continue;

                    seating += type.seatCapacity;

                    if (type.isWall || type.isDoor)
                    {
                        var damage = occupant.GetComponent<Consequence.DamageableStructure>();
                        healthMax += type.maxHealth;
                        healthSum += damage != null ? damage.Health : type.maxHealth;
                    }
                }
            }

            float integrity = healthMax > 0f ? healthSum / healthMax : 0f;
            int gaps = CountGaps(grid, width, height);

            current = new PerimeterReadout(seating, doorTiles.Count, integrity, gaps);
            OnReadoutChanged?.Invoke(current);
        }

        /// <summary>
        /// Counts unenclosed edges by flood-filling from outside the built area. Anything the
        /// fill reaches that is neither wall nor door is a hole an enemy can simply walk
        /// through, which no amount of wall health compensates for.
        /// </summary>
        static int CountGaps(GridXZ<GridBuildingSystem3D.GridObject> grid, int width, int height)
        {
            int gaps = 0;

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    bool onEdge = x == 0 || z == 0 || x == width - 1 || z == height - 1;
                    if (!onEdge) continue;

                    PlacedObject_Done occupant = grid.GetGridObject(x, z)?.GetPlacedObject();
                    PlacedObjectTypeSO type = occupant != null ? occupant.PlacedObjectTypeSO : null;

                    if (type == null || (!type.isWall && !type.isDoor)) gaps++;
                }
            }

            return gaps;
        }

        /// <summary>
        /// What the readout would become if this object were placed. The live delta shown
        /// while the player drags.
        ///
        /// Computed rather than simulated: actually placing and un-placing the object to
        /// measure it would fire build events, dirty the nav grid and churn the pool sixty
        /// times a second.
        /// </summary>
        public PerimeterReadout Preview(PlacedObjectTypeSO type, Vector2Int origin, PlacedObjectTypeSO.Dir dir)
        {
            if (type == null) return current;

            int seating = current.SeatingCapacity + type.seatCapacity;
            int doors = current.DoorCount + (type.isDoor ? 1 : 0);
            float integrity = current.Integrity;
            int gaps = current.Gaps;

            if (type.isWall || type.isDoor)
            {
                List<Vector2Int> tiles = type.GetGridPositionList(origin, dir);
                GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();

                if (grid != null)
                {
                    int width = grid.GetWidth();
                    int height = grid.GetHeight();

                    for (int i = 0; i < tiles.Count; i++)
                    {
                        bool onEdge = tiles[i].x == 0 || tiles[i].y == 0 ||
                                      tiles[i].x == width - 1 || tiles[i].y == height - 1;
                        if (onEdge) gaps = Mathf.Max(0, gaps - 1);
                    }
                }

                // Sealing a hole is worth far more than thickening an existing wall, and the
                // readout has to show that or players will keep stacking walls behind a gap.
                integrity = Mathf.Clamp01(integrity + (gaps < current.Gaps ? 0.05f : 0.01f));
            }

            return new PerimeterReadout(seating, doors, integrity, gaps);
        }

        /// <summary>Delta form, which is what the UI actually draws: "+4 seats, -12 percent
        /// integrity" beside the player's thumb.</summary>
        public PerimeterReadout PreviewDelta(PlacedObjectTypeSO type, Vector2Int origin, PlacedObjectTypeSO.Dir dir) =>
            Preview(type, origin, dir) - current;

        /// <summary>
        /// Pushes door positions into the navigation layer as flow field seeds.
        ///
        /// Three of the four fields seed from doors - Doors, Breach and WeakestPoint - and
        /// differ only in what they charge to cross a wall. That is 24.3 doing double duty
        /// as the AI architecture: the roster's behavioural variety is this call plus a cost
        /// parameter, not eight behaviour trees.
        /// </summary>
        public void SeedNavigationFields()
        {
            NavigationSystem nav = NavigationSystem.Instance;
            if (nav == null || nav.Grid == null) return;

            IReadOnlyList<Vector2Int> doors = doorTiles.Count > 0 ? doorTiles : EntranceTiles(nav);

            nav.SetFlowFieldSeeds(FlowFieldId.Doors, doors);
            nav.SetFlowFieldSeeds(FlowFieldId.Breach, doors);
            nav.SetFlowFieldSeeds(FlowFieldId.WeakestPoint, doors);

            // The Glutton ignores people entirely and beelines for the stores, so its field
            // seeds somewhere else completely. With no pantry built it has nothing to want,
            // and falls back to behaving like everything else.
            nav.SetFlowFieldSeeds(FlowFieldId.Pantry, pantryTiles.Count > 0 ? pantryTiles : doors);
        }

        /// <summary>The build tiles under <see cref="FallbackEntrances"/>, those on the grid.</summary>
        List<Vector2Int> EntranceTiles(NavigationSystem nav)
        {
            entranceTiles.Clear();

            IReadOnlyList<Vector3> points = FallbackEntrances?.Invoke();
            if (points == null) return entranceTiles;

            NavGrid grid = nav.Grid.Grid;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2Int tile = grid.WorldToBuildTile(points[i]);
                if (grid.BuildTileInBounds(tile.x, tile.y) && !entranceTiles.Contains(tile)) entranceTiles.Add(tile);
            }

            return entranceTiles;
        }
    }
}
