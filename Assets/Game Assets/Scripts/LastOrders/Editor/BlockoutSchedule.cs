using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The measured starting area of The Last Restaurant v0.2, sections 15 to 18.
    ///
    /// Every coordinate in the blockout appears here exactly once. The scene builder and the
    /// validator both read these arrays, so a number cannot be right in one and wrong in the
    /// other - the validator would otherwise be checking the builder against a second copy of
    /// the same typo.
    ///
    /// Deliberately free of UnityEditor types so the validator can run against a built player
    /// if that is ever wanted, and so this file stays readable as what it is: a table.
    ///
    /// Conventions from 16, preserved exactly:
    ///   - Coordinates are the SOUTHWEST CORNER of a world-aligned rectangle.
    ///   - Size is X width by Z depth AFTER rotation. The table is already world-aligned, so
    ///     nothing here may be swapped again when a facing is applied.
    ///   - North is +Z, east is +X, Unity Y is elevation. All objects sit at Y = 0.
    /// </summary>
    public static class BlockoutSchedule
    {
        // --- World (15) -------------------------------------------------------------

        public const float WorldSizeX = 48f;
        public const float WorldSizeZ = 40f;

        public const float CompoundMinX = 4f;
        public const float CompoundMaxX = 36f;
        public const float CompoundMinZ = 8f;
        public const float CompoundMaxZ = 32f;

        /// <summary>
        /// Build-grid cell size. 0.5 m rather than 16's 1 m outdoor grid, because navigation
        /// resolution equals build-cell size in this project - GridService.RebuildTile writes
        /// one classification across every nav cell of a build tile, so the 4x subdivision
        /// carries no independent information.
        ///
        /// At 1 m the schedule is not representable: C1 sits at X 5.5, the kitchen stations
        /// are 1.5 m deep, the service markers are at 19.5, and 17 asks for a 0.5 m interior
        /// grid outright. At 0.5 m every coordinate below lands on a cell boundary except the
        /// three that derive from the 0.25 m wall thickness, and those are visual only.
        /// </summary>
        public const float CellSize = 0.5f;

        public static int GridWidth => Mathf.RoundToInt(WorldSizeX / CellSize);   // 96
        public static int GridHeight => Mathf.RoundToInt(WorldSizeZ / CellSize);  // 80

        // --- Dimensions (16, 17) ----------------------------------------------------

        public const float FenceThickness = 0.25f;
        public const float FenceSectionLength = 2f;
        public const float FenceHeight = 1.6f;

        public const float DinerWallThickness = 0.25f;
        public const float DinerWallHeight = 3f;

        /// <summary>17: the front door spans X 19-21 on Z 17. A visual opening, not a barrier.</summary>
        public const float DinerDoorMinX = 19f;
        public const float DinerDoorMaxX = 21f;

        // --- Camera (18) ------------------------------------------------------------

        public const float CameraPitch = 60f;
        public const float CameraYaw = 0f;

        /// <summary>
        /// 18 asks for "around 21 m". That is the depth-fitting number: an orthographic camera
        /// pitched 60 degrees sees 2 * size / sin(60) = 48.5 m of ground, against a 40 m world.
        /// The 8.5 m surplus is the "visible border of future land" the same section requires.
        ///
        /// Width is the loose axis in landscape - at 16:9 the view spans 74.7 m against a 48 m
        /// world - which is a consequence of fixing the pitch, not an error in the number.
        /// </summary>
        public const float DefaultOrthoSize = 21f;

        public const float MinOrthoSize = 8f;
        public const float MaxOrthoSize = 24f;

        /// <summary>Metres of visible ground permitted outside the world bounds when panning.</summary>
        public const float ViewBorder = 6f;

        // --- Types ------------------------------------------------------------------

        public enum Facing { North, East, South, West, Any }

        /// <summary>
        /// How an object participates in the grid, which is what decides whether navigation
        /// can see it. Driven by GridService.ClassifyTile, which switches on
        /// PlacedObjectTypeSO.type.
        /// </summary>
        public enum Kind
        {
            /// <summary>Registered, unwalkable, not breachable. Beds, coop, tent, crates, post, workshop.</summary>
            Solid,

            /// <summary>Registered as ObjectType.Wall. Perimeter fence and diner walls.</summary>
            Wall,

            /// <summary>Registered as ObjectType.Door. The gate, and the diner doorway.</summary>
            Door,

            /// <summary>Not registered. Visual props and the clearing target.</summary>
            Scenery,

            /// <summary>Not registered, no collider. Route strips and table envelopes.</summary>
            Ground,
        }

        public readonly struct Footprint
        {
            public readonly string Id;
            public readonly string Label;
            public readonly float CornerX;
            public readonly float CornerZ;
            public readonly float SizeX;
            public readonly float SizeZ;
            public readonly float Height;
            public readonly Facing Face;
            public readonly Kind Role;
            public readonly string Note;

            public Footprint(string id, string label, float cornerX, float cornerZ,
                             float sizeX, float sizeZ, float height, Facing face, Kind role,
                             string note = "")
            {
                Id = id; Label = label;
                CornerX = cornerX; CornerZ = cornerZ;
                SizeX = sizeX; SizeZ = sizeZ; Height = height;
                Face = face; Role = role; Note = note;
            }

            public float MaxX => CornerX + SizeX;
            public float MaxZ => CornerZ + SizeZ;
        }

        public readonly struct Marker
        {
            public readonly string Id;
            public readonly float X;
            public readonly float Z;
            public readonly string Note;

            public Marker(string id, float x, float z, string note = "")
            {
                Id = id; X = x; Z = z; Note = note;
            }

            public Vector3 Position => new Vector3(X, 0f, Z);
        }

        public readonly struct Strip
        {
            public readonly string Id;
            public readonly float MinX;
            public readonly float MinZ;
            public readonly float MaxX;
            public readonly float MaxZ;
            public readonly float MinClear;

            public Strip(string id, float minX, float minZ, float maxX, float maxZ, float minClear)
            {
                Id = id; MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ; MinClear = minClear;
            }

            public float SizeX => MaxX - MinX;
            public float SizeZ => MaxZ - MinZ;
        }

        public readonly struct FenceRun
        {
            public readonly string Id;

            /// <summary>True when the run travels along X at a fixed Z.</summary>
            public readonly bool AlongX;

            /// <summary>The fixed coordinate: Z for an along-X run, X for an along-Z run.</summary>
            public readonly float Line;

            public readonly float From;
            public readonly float To;

            public FenceRun(string id, bool alongX, float line, float from, float to)
            {
                Id = id; AlongX = alongX; Line = line; From = from; To = to;
            }

            public float Length => To - From;
            public int SectionCount => Mathf.RoundToInt(Length / FenceSectionLength);
        }

        // --- Outdoor placement schedule (16) ----------------------------------------

        public static readonly Footprint[] Outdoor =
        {
            new Footprint("R1", "Diner",            14f,   17f, 12f, 10f, DinerWallHeight, Facing.South, Kind.Wall,
                          "Door blocked; roof cutaway"),
            new Footprint("F1", "Crop bed",          7f,   17f,  3f,  4f, 0.4f,  Facing.East,  Kind.Solid,   "Empty"),
            new Footprint("F2", "Crop bed",          7f,   23f,  3f,  4f, 0.4f,  Facing.East,  Kind.Solid,   "Empty"),
            new Footprint("W1", "Water barrel pad",  6f,   13f,  2f,  2f, 1.0f,  Facing.East,  Kind.Scenery, "Active visual prop"),
            new Footprint("C1", "Chicken coop",      5.5f, 28f,  5f,  3f, 2.0f,  Facing.South, Kind.Solid,   "Locked"),
            new Footprint("S1", "Survivor tent",    29f,   27f,  5f,  3f, 2.2f,  Facing.South, Kind.Solid,   "Active"),
            new Footprint("S2", "Supply crates",    31f,   23f,  2f,  2f, 1.0f,  Facing.West,  Kind.Solid,   "Starter supply visual"),
            new Footprint("D1", "Guard post",       24f,   10f,  3f,  3f, 2.8f,  Facing.South, Kind.Solid,   "Unfinished"),
            new Footprint("G1", "Gate",             18f,    7.75f, 4f, 0.5f, 2.0f, Facing.South, Kind.Door,  "Opening until installed"),
            new Footprint("B1", "Entrance debris",  19f,   16f,  2f,  1f, 0.8f,  Facing.Any,   Kind.Scenery, "First clearing target"),
            new Footprint("X1", "Workshop",         39f,   21f,  6f,  6f, 3.5f,  Facing.West,  Kind.Solid,   "Locked outside boundary"),
        };

        // --- Restaurant interior (17) -----------------------------------------------

        /// <summary>
        /// 18: table-set footprints include chairs and seating animation space, with a 1x1 m
        /// table centred in each 3x3 m envelope. Only the table is solid - see TableCores.
        /// These entries are the envelopes, emitted as visible reservations, not collision.
        /// </summary>
        public static readonly Footprint[] Interior =
        {
            new Footprint("K1", "Stove",        15f, 25f, 2f,   1.5f, 1.0f, Facing.South, Kind.Solid),
            new Footprint("K2", "Sink",         18f, 25f, 2f,   1.5f, 1.0f, Facing.South, Kind.Solid),
            new Footprint("K3", "Pantry",       21f, 25f, 2f,   1.5f, 1.8f, Facing.South, Kind.Solid),
            new Footprint("K4", "Supply crate", 24f, 25f, 1.5f, 1.5f, 1.0f, Facing.South, Kind.Solid),
            new Footprint("K5", "Counter",      15f, 22f, 8f,   1f,   1.1f, Facing.North, Kind.Solid),
            new Footprint("T1", "Table set",    16f, 18f, 3f,   3f,   0f,   Facing.East,  Kind.Ground, "Usable"),
            new Footprint("T2", "Table set",    22f, 18f, 3f,   3f,   0f,   Facing.West,  Kind.Ground, "Damaged"),
        };

        /// <summary>
        /// The solid 1x1 m tables inside the T1 and T2 envelopes.
        ///
        /// Registering the full 3x3 instead would leave a 1 m band at Z 21-22 as the only
        /// connection from the central aisle to the kitchen opening, against 18's own "main
        /// two-way routes remain at least 2 m" - and on the most-travelled path in the
        /// building. Treating the envelope as reservation and the table as collision widens
        /// that band to 2 m and restores the east and west service edges 18 reserves.
        /// </summary>
        public static readonly Footprint[] TableCores =
        {
            new Footprint("T1c", "Table",       17f, 19f, 1f, 1f, 0.8f, Facing.East, Kind.Solid, "T1 core"),
            new Footprint("T2c", "Table",       23f, 19f, 1f, 1f, 0.8f, Facing.West, Kind.Solid, "T2 core"),
        };

        /// <summary>Chairs sit on the north and south sides of each table (18). Visual only.</summary>
        public static readonly Footprint[] Chairs =
        {
            new Footprint("T1n", "Chair", 17.25f, 20.25f, 0.5f, 0.5f, 0.6f, Facing.South, Kind.Scenery),
            new Footprint("T1s", "Chair", 17.25f, 18.25f, 0.5f, 0.5f, 0.6f, Facing.North, Kind.Scenery),
            new Footprint("T2n", "Chair", 23.25f, 20.25f, 0.5f, 0.5f, 0.6f, Facing.South, Kind.Scenery),
            new Footprint("T2s", "Chair", 23.25f, 18.25f, 0.5f, 0.5f, 0.6f, Facing.North, Kind.Scenery),
        };

        // --- Fence (16) --------------------------------------------------------------

        /// <summary>
        /// 16: west X=4 and east X=36 from Z 8-32; north Z=32 from X 4-36; south Z=8 from
        /// X 4-18 and X 22-36. The X 18-22 omission is the gate opening, and it matches G1's
        /// 4 m width exactly. 12 + 12 + 16 + 7 + 7 = 54 sections, as the document states.
        /// </summary>
        public static readonly FenceRun[] Fence =
        {
            new FenceRun("West",      alongX: false, line: 4f,  from: 8f,  to: 32f),
            new FenceRun("East",      alongX: false, line: 36f, from: 8f,  to: 32f),
            new FenceRun("North",     alongX: true,  line: 32f, from: 4f,  to: 36f),
            new FenceRun("SouthWest", alongX: true,  line: 8f,  from: 4f,  to: 18f),
            new FenceRun("SouthEast", alongX: true,  line: 8f,  from: 22f, to: 36f),
        };

        public const int ExpectedFenceSections = 54;

        /// <summary>The four compound corners, where posts are emitted once and runs are shortened.</summary>
        public static readonly Vector2[] FenceCorners =
        {
            new Vector2(CompoundMinX, CompoundMinZ),
            new Vector2(CompoundMaxX, CompoundMinZ),
            new Vector2(CompoundMinX, CompoundMaxZ),
            new Vector2(CompoundMaxX, CompoundMaxZ),
        };

        // --- Nodes (16, 18) -----------------------------------------------------------

        /// <summary>
        /// The public road's centreline and width. Customers walk it end to end, so it is a
        /// route as much as a surface and the number belongs here rather than in the builder.
        /// </summary>
        public const float RoadCentreZ = 2f;

        public const float RoadWidth = 4.2f;

        /// <summary>
        /// Where the road leaves the map to the west, toward the survivor settlement.
        ///
        /// Far outside the world rectangle, and the number is not arbitrary. At a 21 m
        /// orthographic size the view is wider than the world plus its border, so the camera
        /// clamps to the world centre and shows roughly X -13 to 61 at 16:9. There is
        /// therefore no such thing as "just off the west edge": -17 is genuinely outside the
        /// default frame at that ratio, and at wider ratios the settlement buildings and the
        /// gate stand between the camera and this point.
        ///
        /// Both defences matter, because the requirement is not "spawn off screen" - it is
        /// that nobody is ever seen arriving out of nothing. Sixteen metres of road between
        /// here and the arrival marker is what turns a spawn into a journey.
        /// </summary>
        public const float SettlementRoadX = -17f;

        /// <summary>The settlement's own gate across the road. Travellers come through it,
        /// which is a cheaper way to say "they came from somewhere" than any amount of
        /// building behind it.</summary>
        public const float SettlementGateX = -14f;

        /// <summary>Where the road leaves the map to the east. Travellers who pass the diner
        /// without stopping walk off this end, which is what makes the road read as a route
        /// rather than as a customer delivery pipe.</summary>
        public const float RoadEastExitX = 52f;

        public static readonly Marker[] CustomerNodes =
        {
            new Marker("SettlementRoad", SettlementRoadX, RoadCentreZ,
                       "Spawn and despawn, inside the western settlement and behind its gate. " +
                       "Travellers walk into view rather than appearing in it."),
            new Marker("RoadWest",       2f,    2f,
                       "16's arrival point. Kept exactly, but as a ROUTE marker: it is well " +
                       "inside the visible frame, so spawning here would pop people into " +
                       "existence in full view."),
            new Marker("RoadTurn",      20f,    2f),
            new Marker("Driveway",      20f,    4f,
                       "Where a traveller stops and looks at the diner before committing. The " +
                       "pause is the whole point of the first arrival."),
            new Marker("OutsideGate",   20f,    6f),
            new Marker("InsideGate",    20f,   10f),
            new Marker("DinerEntrance", 20f,   17f),
            new Marker("RoadEast",      RoadEastExitX, RoadCentreZ,
                       "Exit for background travellers who never turn off the road."),
            new Marker("Waiting",       22.5f, 16.5f,
                       "16 places this at Z 16, which is exactly the north edge of the east " +
                       "connection route - waiting customers would stand in east-bound " +
                       "traffic. Moved 0.5 m north, which clears the route and still keeps it " +
                       "off the main aisle as the document intends."),
            new Marker("DinerShelter",  19.5f, 21f,
                       "Where customers already inside move during a daytime incident: deep " +
                       "in the dining room, off the door throat and away from the windows."),
        };

        // --- The road west (visitor system) ----------------------------------------------

        /// <summary>
        /// The survivor settlement customers travel from, and the screen that hides the point
        /// they walk in from.
        ///
        /// Both sit west of X 0, outside the world rectangle, because neither is playable
        /// ground - they are the answer to "where do these people come from", built so that
        /// answer is on screen rather than in a design document.
        /// </summary>
        public static readonly Footprint[] West =
        {
            new Footprint("WS1", "Settlement shack",      -22f,   5f,   4.5f, 3.5f, 2.8f, Facing.East,  Kind.Scenery,
                          "Nearest building of the western settlement"),
            new Footprint("WS2", "Settlement shack",      -25f,  -2.5f, 4f,   3f,   2.4f, Facing.East,  Kind.Scenery),
            new Footprint("WS3", "Settlement watchtower", -20f,   9.5f, 2.5f, 2.5f, 6f,   Facing.East,  Kind.Scenery,
                          "Reads as a place that watches its own road"),
            new Footprint("WS4", "Settlement fence",      -27f,  -4f,   1f,   16f,  1.8f, Facing.East,  Kind.Scenery),

            new Footprint("SC1", "Road screen: hoarding", -10f,  -2.6f, 8f,   0.4f, 5f,   Facing.North, Kind.Scenery,
                          "Roadside hoarding on the south verge. Tall because a 60 degree " +
                          "pitch puts a lot of ground between the camera and anything low."),
            new Footprint("SC2", "Road screen: coach",     -9f,   4.5f, 9f,   2.6f, 3.2f, Facing.East,  Kind.Scenery,
                          "Burnt-out coach on the north verge, closing the gap above the hoarding"),
        };

        /// <summary>
        /// Dead trees along both verges west of the map, thickening the screen.
        ///
        /// Listed rather than scattered randomly: the screen has a job to do, and a random
        /// pass that happens to leave a two-metre gap in it fails silently and only on some
        /// seeds.
        /// </summary>
        public static readonly Vector2[] WestTreeline =
        {
            new Vector2(-1.8f, -2.2f), new Vector2(-12.5f, -2.4f), new Vector2(-15.5f, -1.8f),
            new Vector2(-18.5f, -2.4f), new Vector2(-21.5f, -1.9f), new Vector2(-24.5f, -2.3f),
            new Vector2(-1.5f, 5.6f), new Vector2(-12.5f, 5.4f), new Vector2(-16f, 5.9f),
            new Vector2(-24f, 6.2f),
        };

        public static readonly Marker[] SurvivorNodes =
        {
            new Marker("SurvivorA", 19f, 10f, "16: move promptly so they do not block arrivals"),
            new Marker("SurvivorB", 21f, 10f, "16: move promptly so they do not block arrivals"),
        };

        /// <summary>
        /// 11's Night 1 route, in order. Index 0 is the spawn point; the rest are waypoints.
        /// Three walkers at 0, 4 and 8 seconds travel this lane to reach the gate.
        /// </summary>
        public static readonly Marker[] EnemyRoute =
        {
            new Marker("EnemySpawn",  42f, 2f, "11: spawn point for the Night 1 walkers"),
            new Marker("EnemyTurn",   20f, 2f),
            new Marker("EnemyApproach", 20f, 6f),
            new Marker("EnemyTarget", 20f, 8f, "The gate"),
        };

        /// <summary>
        /// 18: service markers sit in the aisle beside each table so staff do not stand inside
        /// table collision. Station markers sit near Z 24, in the kitchen circulation band.
        /// </summary>
        public static readonly Marker[] InteractionNodes =
        {
            new Marker("ServiceT1", 19.5f, 19.5f),
            new Marker("ServiceT2", 21.5f, 19.5f),
            new Marker("StationK1", 16f,   24.5f),
            new Marker("StationK2", 19f,   24.5f),
            new Marker("StationK3", 22f,   24.5f),
            new Marker("StationK4", 24.75f, 24.5f),
        };

        // --- Reserved routes (18) ------------------------------------------------------

        /// <summary>
        /// Ground surfaces, not colliders. Stored as min/max rather than corner+size because
        /// 18 states them as spans and converting them here would just be another chance to
        /// transpose a number.
        /// </summary>
        public static readonly Strip[] Routes =
        {
            new Strip("GateToPorch",  18f, 4f,    22f,    17f, 4f),
            new Strip("FarmConn",     10f, 14f,   18f,    16f, 2f),
            new Strip("CropStrip",    10f, 16f,   12f,    28f, 2f),
            new Strip("EastConn",     22f, 14f,   34f,    16f, 2f),
            new Strip("TentAccessA",  27f, 14f,   29f,    27f, 2f),
            new Strip("TentAccessB",  29f, 25f,   32f,    27f, 2f),
            new Strip("DoorThroat",   19f, 16.75f, 21f,   17.25f, 2f),
            new Strip("KitchenOpen",  23f, 22f,   25.75f, 25f, 2.75f),
        };

        // --- Collision and navigation (18) ---------------------------------------------

        /// <summary>18: approximately 0.3 m agent radius for people.</summary>
        public const float AgentRadius = 0.3f;

        /// <summary>18: minimum practical single-person clearance after collision margins.</summary>
        public const float MinSinglePersonClearance = 1f;

        // --- Helpers --------------------------------------------------------------------

        /// <summary>
        /// Corner to centre. The schedule is authored in corners because that is how footprints
        /// tile and how 16 states them; Unity positions a scaled primitive by its centre.
        /// Doing the conversion in exactly one place is the difference between an off-by-half-
        /// a-metre nobody notices and one everybody does.
        /// </summary>
        public static Vector3 CentreOf(in Footprint f, float y = 0f) =>
            new Vector3(f.CornerX + f.SizeX * 0.5f, y, f.CornerZ + f.SizeZ * 0.5f);

        public static Vector3 CentreOf(float cornerX, float cornerZ, float sizeX, float sizeZ, float y = 0f) =>
            new Vector3(cornerX + sizeX * 0.5f, y, cornerZ + sizeZ * 0.5f);

        public static Vector3 CentreOf(in Strip s, float y = 0f) =>
            new Vector3((s.MinX + s.MaxX) * 0.5f, y, (s.MinZ + s.MaxZ) * 0.5f);

        /// <summary>
        /// Facing to Unity yaw. 16: prefab front is local +Z, yaw 0 faces north, 90 east,
        /// 180 south, 270 west.
        ///
        /// This is applied to a CHILD transform only, never to the object carrying the
        /// footprint scale. The table is already world-aligned, so rotating the scaled parent
        /// would swap width and depth a second time - which is precisely what 16 warns
        /// against. Keeping the yaw on a child makes that mistake impossible rather than
        /// merely discouraged.
        /// </summary>
        public static float YawOf(Facing f) => f switch
        {
            Facing.North => 0f,
            Facing.East => 90f,
            Facing.South => 180f,
            Facing.West => 270f,
            _ => 0f,
        };

        /// <summary>World-space AABB of a footprint on the ground plane, for overlap tests.</summary>
        public static Rect RectOf(in Footprint f) => new Rect(f.CornerX, f.CornerZ, f.SizeX, f.SizeZ);

        public static Rect RectOf(in Strip s) => new Rect(s.MinX, s.MinZ, s.SizeX, s.SizeZ);

        /// <summary>
        /// True when a fence run lies on the compound's low edge (west or south) rather than
        /// its high edge (east or north).
        /// </summary>
        public static bool IsMinEdge(in FenceRun run) => run.AlongX
            ? Mathf.Abs(run.Line - CompoundMinZ) < 1e-3f
            : Mathf.Abs(run.Line - CompoundMinX) < 1e-3f;

        /// <summary>
        /// The grid tile a perimeter wall occupies, given its centreline.
        ///
        /// A 0.25 m wall straddling a centreline is thinner than a 0.5 m cell and its
        /// centreline falls exactly on a cell boundary, so which of the two adjoining cells it
        /// claims has to be decided once and applied uniformly - otherwise the west and south
        /// walls land outside the compound while the east and north walls land inside it, and
        /// the playable area silently comes out half a metre short on two sides.
        ///
        /// The rule is OUTWARD: a perimeter wall takes the cell on the outside of its line.
        /// That leaves the walkable interior at cols 8-71 and rows 16-63, which is exactly the
        /// 32 x 24 m of compound that 15 specifies.
        /// </summary>
        public static int WallTileOutward(float centreline, bool isMinEdge)
        {
            int boundary = Mathf.RoundToInt(centreline / CellSize);
            return isMinEdge ? boundary - 1 : boundary;
        }

        /// <summary>The tile index a fence run occupies across its thickness.</summary>
        public static int FenceTile(in FenceRun run) => WallTileOutward(run.Line, IsMinEdge(run));

        /// <summary>
        /// The outward tile for a compound corner post, as (column, row). Corners belong to two
        /// edges at once, so each axis is resolved independently.
        /// </summary>
        public static Vector2Int CornerTile(Vector2 corner)
        {
            bool minX = Mathf.Abs(corner.x - CompoundMinX) < 1e-3f;
            bool minZ = Mathf.Abs(corner.y - CompoundMinZ) < 1e-3f;

            return new Vector2Int(WallTileOutward(corner.x, minX), WallTileOutward(corner.y, minZ));
        }

        /// <summary>Grid column containing a world X. Floors, so a point on a boundary belongs to the upper cell.</summary>
        public static int ColumnOf(float worldX) => Mathf.FloorToInt(worldX / CellSize);

        public static int RowOf(float worldZ) => Mathf.FloorToInt(worldZ / CellSize);
    }
}
