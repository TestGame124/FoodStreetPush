using FoodStreet.Visitors.EditorTools;
using LastOrders.Night;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Every number the world-edge builder uses, in one table.
    ///
    /// The level is a compound by a road with open ground round it, and past that ground the
    /// world ends in fog: a band of trees along the back, abandoned suburbs on the other three
    /// sides. The bands are where the night comes from, so the spawn lanes, the trails the
    /// zombies walk in on and the daytime wanderers all live here too - a lane and the trail it
    /// walks down are one decision, and two tables would be two chances to disagree.
    ///
    /// Same conventions as <see cref="ZoneLayout"/>: north is +Z (the back of the screen), east is
    /// +X, everything sits at Y = 0, rectangles are min/max.
    /// </summary>
    public static class EnvironmentLayout
    {
        public const int DefaultSeed = 20260930;

        // --- The clear ground ---------------------------------------------------------------
        //
        // The open land round the compound, which the fog does not reach: the land the restaurant
        // grows into. Past the defense ring it is three rings of plots to clear and claim on the
        // west, north and east (LandPlotSetup), each touching the next, so the land claimed grows
        // out from the restaurant as one piece; then a strip of no man's land, then the old
        // perimeter fence, broken open where each of the night's lanes comes through. South, past
        // the road, it stays open: the customers' side.
        //
        // The plot field reaches x -63.5..100 and z 89.5; the fence stands FenceInset inside these
        // edges, about 8 m beyond the outermost plots. The camera frames about 28 m across at its
        // widest, so none of the edges is ever on screen together with the restaurant.
        //
        // Everything else out here - the avenues, the lanes, the checkpoint, the camera's limits,
        // the road's ends - is measured from these four, so the ground is resized by changing them
        // (and the plot rings in LandPlotSetup to fill it). Grown on 2026-10-12 from X -50..86,
        // Z -45..80 (and on 2026-10-01 from X -36..72, Z -31..66).

        public const float ClearMinX = -76f;
        public const float ClearMaxX = 112f;
        public const float ClearMinZ = -50f;
        public const float ClearMaxZ = 102f;

        public static Rect Clear => ZoneLayout.FromBounds(ClearMinX, ClearMinZ, ClearMaxX, ClearMaxZ);

        /// <summary>How deep each band is, from the clear ground to where the fog is total.</summary>
        public const float BandDepth = 34f;

        /// <summary>Nothing is built deeper into a band than this: past it the fog is solid.</summary>
        public const float ContentDepth = 28f;

        public static Rect World => ZoneLayout.FromBounds(
            ClearMinX - BandDepth, ClearMinZ - BandDepth, ClearMaxX + BandDepth, ClearMaxZ + BandDepth);

        /// <summary>The whole back edge, corners included: the trees wrap round both of them.</summary>
        public static Rect NorthBand => ZoneLayout.FromBounds(World.xMin, ClearMaxZ, World.xMax, World.yMax);

        public static Rect WestBand => ZoneLayout.FromBounds(World.xMin, World.yMin, ClearMinX, ClearMaxZ);

        public static Rect EastBand => ZoneLayout.FromBounds(ClearMaxX, World.yMin, World.xMax, ClearMaxZ);

        public static Rect SouthBand => ZoneLayout.FromBounds(ClearMinX, World.yMin, ClearMaxX, ClearMinZ);

        /// <summary>Metres outside the clear ground; negative inside it. What the fog is made of.</summary>
        public static float Outside(Vector2 p)
        {
            Rect r = Clear;
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dz = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);

            if (dx > 0f || dz > 0f) return Mathf.Sqrt(dx * dx + dz * dz);

            return -Mathf.Min(Mathf.Min(p.x - r.xMin, r.xMax - p.x), Mathf.Min(p.y - r.yMin, r.yMax - p.y));
        }

        // --- The compound, as the builder keeps clear of it ---------------------------------------
        //
        // Stated here as a fallback. The builder measures the compound's own roots too and keeps
        // clear of whichever is larger, so a wall moved by hand is never buried in junk.

        public static Rect Compound => ZoneLayout.FromBounds(-9.5f, -2.6f, 46f, 35.2f);

        public static Rect Courtyard => ZoneLayout.FromBounds(21f, 34.7f, 46.5f, 48.9f);

        /// <summary>The nav grid the night's zombies path on (StartingArea_Grid: 62 x 50 from -12, 0).
        /// Outside the walls it only gets flat ground dressing: a zombie crossing it walks through
        /// whatever is there, and at this camera distance that is the one thing that shows.</summary>
        public static Rect NavGrid => ZoneLayout.FromBounds(-12f, 0f, 50f, 50f);

        // --- The approaches -------------------------------------------------------------------------

        public readonly struct Lane
        {
            public readonly string Name;

            /// <summary>Where the spawner puts them, a few metres inside the fog.</summary>
            public readonly Vector3 Spawn;

            /// <summary>Deep in its band, where the approach begins. The fog tongue starts here.</summary>
            public readonly Vector3 Mouth;

            /// <summary>Where the worn dirt across the open ground starts: the end of the street that leads
            /// out of the suburbs, or where the forest's own winding trail comes out of the trees.</summary>
            public readonly Vector3 TrailStart;

            /// <summary>Where the trail meets the compound. The spawn walks straight here: an enemy off the nav
            /// grid heads for its nearest edge, so spawn and wall end share an X or a Z.</summary>
            public readonly Vector3 WallEnd;

            public Lane(string name, Vector3 spawn, Vector3 mouth, Vector3 trailStart, Vector3 wallEnd)
            {
                Name = name;
                Spawn = spawn;
                Mouth = mouth;
                TrailStart = trailStart;
                WallEnd = wallEnd;
            }

            /// <summary>Unit direction the night walks in along this lane, on the ground.</summary>
            public Vector2 Inward
            {
                get
                {
                    Vector3 d = WallEnd - Spawn;
                    return new Vector2(d.x, d.z).normalized;
                }
            }

        }

        /// <summary>
        /// Where the west and east lanes cross the bands, and so their lane streets: either side of
        /// the middle of the compound, and past its north corners. The land plots split along them,
        /// so each trail runs down the line between two plots rather than across one - and clear of
        /// the wall's gun sites and the ring's pads, so a claimed plot's wall across the ring never
        /// lands on one.
        /// </summary>
        public const float WestLaneZ = 21.5f;

        public const float EastLaneZ = 16f;

        public const float NorthWestLaneZ = 39f;

        public const float NorthEastLaneZ = 39f;

        /// <summary>The north lane's line down to the wall, west of the courtyard: straight onto the
        /// diner's west side, where the night's flow field takes them anyway.</summary>
        public const float NorthLaneX = 7f;

        /// <summary>
        /// How far inside the old fence a lane spawns: just through its breach, in the first of the
        /// fog, on the no man's land between the fence and the outermost plots - never on land the
        /// player can claim, or they would appear inside the walls.
        /// </summary>
        public const float SpawnInsideFence = 4.5f;

        /// <summary>
        /// The night's six approaches, in lane order: the front across the road and through the
        /// gate the starting tower watches, the west and east, out of the trees, and the two that
        /// come in past the compound's north corners. The attack cycle takes a different one for
        /// each wave.
        ///
        /// Each comes through a breach in the old perimeter fence (<see cref="Breaches"/>) and walks
        /// down its trail across the land to claim, along the line between two plots, to the
        /// compound - or to whatever claimed land's wall is in the way first. Every spawn shares an
        /// X or a Z with its wall end and lies off the nav grid, so it walks straight in down its trail.
        /// </summary>
        public static readonly Lane[] Lanes =
        {
            new Lane("Lane0_South", new Vector3(ZoneLayout.GateCentreX, 0f, Fence.yMin + SpawnInsideFence),
                     new Vector3(ZoneLayout.GateCentreX, 0f, ClearMinZ - 21f),
                     new Vector3(ZoneLayout.GateCentreX, 0f, ClearMinZ - 0.6f),
                     new Vector3(ZoneLayout.GateCentreX, 0f, ZoneLayout.RoadCentreZ - ZoneLayout.RoadWidth * 0.5f)),
            WestLane("Lane1_West", WestLaneZ),
            EastLane("Lane2_East", EastLaneZ),
            new Lane("Lane3_North", new Vector3(NorthLaneX, 0f, Fence.yMax - SpawnInsideFence), new Vector3(NorthLaneX + 6f, 0f, ClearMaxZ + 24f),
                     new Vector3(NorthLaneX, 0f, ClearMaxZ + 2f), new Vector3(NorthLaneX, 0f, Compound.yMax)),
            WestLane("Lane4_NorthWest", NorthWestLaneZ),
            EastLane("Lane5_NorthEast", NorthEastLaneZ),
        };

        static Lane WestLane(string name, float z) =>
            new Lane(name, new Vector3(Fence.xMin + SpawnInsideFence, 0f, z), new Vector3(ClearMinX - 22f, 0f, z),
                     new Vector3(ClearMinX - 0.6f, 0f, z), new Vector3(Compound.xMin, 0f, z));

        static Lane EastLane(string name, float z) =>
            new Lane(name, new Vector3(Fence.xMax - SpawnInsideFence, 0f, z), new Vector3(ClearMaxX + 22f, 0f, z),
                     new Vector3(ClearMaxX + 0.6f, 0f, z), new Vector3(Compound.xMax, 0f, z));

        /// <summary>The worn path's half width. The clear corridor either side of it is wider.</summary>
        public const float TrailHalfWidth = 2.5f;

        /// <summary>Keep-out half width along a trail: nothing that stands up is put in it.</summary>
        public const float TrailClearance = 4f;

        /// <summary>Metres either side of its lane a zombie may set off from: a group keeps to its trail.</summary>
        public const float LaneSpread = 3f;

        // --- The old perimeter fence ----------------------------------------------------------------

        /// <summary>
        /// How far inside the clear ground's edge the fence stands: in the first of the haze, so
        /// the suburbs and the trees beyond it fade while the fence itself still reads.
        /// </summary>
        public const float FenceInset = 4f;

        /// <summary>
        /// The line of the old perimeter - somebody's attempt, long before the diner, to fence the
        /// whole stretch off. It runs all the way round the clear ground and is broken open where
        /// each lane comes through (<see cref="Breaches"/>) and where the road does.
        /// </summary>
        public static Rect Fence => NeighborhoodBuilder.Inflate(Clear, -FenceInset);

        /// <summary>Half the width of the hole in the fence where a lane comes through it: its trail, a group's spread, and the fence it flattened.</summary>
        public const float BreachHalfWidth = 6.5f;

        /// <summary>Half the width of the opening the road runs through: the carriageway and a verge either side.</summary>
        public const float RoadGapHalfWidth = ZoneLayout.RoadWidth * 0.5f + 3f;

        public readonly struct Breach
        {
            public readonly string Name;

            /// <summary>The middle of the opening, on the fence's line.</summary>
            public readonly Vector2 At;

            /// <summary>Along the fence.</summary>
            public readonly Vector2 Along;

            public readonly float HalfWidth;

            /// <summary>Where the night comes through, rather than the customers' road.</summary>
            public readonly bool Lane;

            public Breach(string name, Vector2 at, Vector2 along, float halfWidth, bool lane)
            {
                Name = name;
                At = at;
                Along = along;
                HalfWidth = halfWidth;
                Lane = lane;
            }
        }

        /// <summary>Every opening in the fence: one per lane, where its street meets the clear ground,
        /// and one at each end of the road.</summary>
        public static Breach[] Breaches
        {
            get
            {
                Rect f = Fence;
                var list = new System.Collections.Generic.List<Breach>();

                foreach (Lane lane in Lanes)
                {
                    Vector2 inward = lane.Inward;
                    bool alongZ = Mathf.Abs(inward.x) > 0.5f;
                    Vector2 at = alongZ
                        ? new Vector2(inward.x > 0f ? f.xMin : f.xMax, lane.WallEnd.z)
                        : new Vector2(lane.WallEnd.x, inward.y > 0f ? f.yMin : f.yMax);

                    list.Add(new Breach(lane.Name, at, alongZ ? Vector2.up : Vector2.right, BreachHalfWidth, lane: true));
                }

                list.Add(new Breach("Road_West", new Vector2(f.xMin, ZoneLayout.RoadCentreZ), Vector2.up, RoadGapHalfWidth, lane: false));
                list.Add(new Breach("Road_East", new Vector2(f.xMax, ZoneLayout.RoadCentreZ), Vector2.up, RoadGapHalfWidth, lane: false));
                return list.ToArray();
            }
        }

        // --- The daytime wanderers ------------------------------------------------------------------

        /// <summary>
        /// Patches of the suburbs the town's zombies amble about in by day.
        ///
        /// On the avenues that wrap the clear ground, a little way into the fog, where a player
        /// panning out can see them without them ever being near the walls. Streets rather than lots: they
        /// walk straight lines through whatever is in the way, and a street has nothing in it. None
        /// to the north - the trees are the night's, and the courtyard is the workers'.
        /// </summary>
        public static readonly TownZombies.Area[] TownPatches =
        {
            new TownZombies.Area("SouthWest", new Vector3(-10f, 0f, SouthAvenueZ), new Vector2(16f, 3.5f), 3),
            new TownZombies.Area("SouthEast", new Vector3(50f, 0f, SouthAvenueZ), new Vector2(16f, 3.5f), 3),
            new TownZombies.Area("West", new Vector3(WestAvenueX, 0f, 36f), new Vector2(3.5f, 16f), 2),
            new TownZombies.Area("East", new Vector3(EastAvenueX, 0f, 34f), new Vector2(3.5f, 16f), 2),
        };

        // --- The suburbs' streets --------------------------------------------------------------------

        public readonly struct Street
        {
            public readonly string Name;
            public readonly Vector2 From;
            public readonly Vector2 To;
            public readonly float Width;

            /// <summary>Already laid by the visitor setup; the suburbs only line it with sidewalks and houses.</summary>
            public readonly bool IsMainRoad;

            public Street(string name, Vector2 from, Vector2 to, float width = 6f, bool isMainRoad = false)
            {
                Name = name;
                From = from;
                To = to;
                Width = width;
                IsMainRoad = isMainRoad;
            }

            public bool AlongX => Mathf.Abs(To.y - From.y) < 0.01f;

            public Rect Surface => AlongX
                ? ZoneLayout.FromBounds(Mathf.Min(From.x, To.x), From.y - Width * 0.5f, Mathf.Max(From.x, To.x), From.y + Width * 0.5f)
                : ZoneLayout.FromBounds(From.x - Width * 0.5f, Mathf.Min(From.y, To.y), From.x + Width * 0.5f, Mathf.Max(From.y, To.y));
        }

        public const float SidewalkWidth = 1.3f;

        /// <summary>How far into its band each avenue runs: room for a row of lots between it and
        /// the clear ground, and another behind it.</summary>
        public const float AvenueDepth = 16f;

        public const float WestAvenueX = ClearMinX - AvenueDepth;
        public const float EastAvenueX = ClearMaxX + AvenueDepth;
        public const float SouthAvenueZ = ClearMinZ - AvenueDepth;

        /// <summary>How far past the south avenue the side avenues carry on into the fog.</summary>
        const float AvenueTail = 14f;

        /// <summary>How far into its band a cross street runs from the clear ground.</summary>
        const float CrossStreetDepth = 32f;

        /// <summary>The west main road stops a little short: the checkpoint stands across it.</summary>
        const float WestRoadDepth = 30f;

        /// <summary>
        /// An avenue a little way into the fog on three sides, wrapping the clear ground like the
        /// ring road of a suburb, with houses along both sides of it: the near row backs onto the
        /// open ground, so a player looking out sees back gardens and fences, and the far row fades
        /// into the mist. The avenues meet at the two front corners.
        ///
        /// The cross streets are the lanes and the main road. Each lane street runs from deep in
        /// the fog, over the avenue and between the near houses to the edge of the clear ground,
        /// where the lane's trail carries straight on through the breach in the fence - so the night
        /// walks out of the suburbs down a road rather than through somebody's house.
        /// </summary>
        public static readonly Street[] Streets =
        {
            // West.
            new Street("West_Avenue", new Vector2(WestAvenueX, SouthAvenueZ - AvenueTail), new Vector2(WestAvenueX, ClearMaxZ)),
            new Street("West_MainRoad", new Vector2(ClearMinX - WestRoadDepth, ZoneLayout.RoadCentreZ),
                       new Vector2(ClearMinX, ZoneLayout.RoadCentreZ), ZoneLayout.RoadWidth, isMainRoad: true),
            new Street("West_LaneStreet", new Vector2(ClearMinX - CrossStreetDepth, WestLaneZ), new Vector2(ClearMinX, WestLaneZ)),
            new Street("West_LaneStreet_North", new Vector2(ClearMinX - CrossStreetDepth, NorthWestLaneZ), new Vector2(ClearMinX, NorthWestLaneZ)),

            // South, past the road.
            new Street("South_Avenue", new Vector2(WestAvenueX, SouthAvenueZ), new Vector2(EastAvenueX, SouthAvenueZ)),
            new Street("South_LaneStreet", new Vector2(ZoneLayout.GateCentreX, ClearMinZ - CrossStreetDepth),
                       new Vector2(ZoneLayout.GateCentreX, ClearMinZ)),

            // East.
            new Street("East_Avenue", new Vector2(EastAvenueX, SouthAvenueZ - AvenueTail), new Vector2(EastAvenueX, ClearMaxZ)),
            new Street("East_MainRoad", new Vector2(ClearMaxX, ZoneLayout.RoadCentreZ),
                       new Vector2(ClearMaxX + CrossStreetDepth, ZoneLayout.RoadCentreZ), ZoneLayout.RoadWidth, isMainRoad: true),
            new Street("East_LaneStreet", new Vector2(ClearMaxX, EastLaneZ), new Vector2(ClearMaxX + CrossStreetDepth, EastLaneZ)),
            new Street("East_LaneStreet_North", new Vector2(ClearMaxX, NorthEastLaneZ), new Vector2(ClearMaxX + CrossStreetDepth, NorthEastLaneZ)),
        };

        // --- The survivors' checkpoint -----------------------------------------------------------------

        /// <summary>
        /// Where the road west leaves the suburbs for the settlement the customers live in.
        ///
        /// Far enough into the fog that the settlement behind it is only lights, near enough that
        /// a player panned fully west sees the gate. The fog is thinned over it so the lights read.
        /// </summary>
        public static Vector3 Checkpoint => new Vector3(WestAvenueX - 7.5f, 0f, ZoneLayout.RoadCentreZ);

        public const float CheckpointFogRadius = 16f;

        /// <summary>How much of the fog is left over the checkpoint.</summary>
        public const float CheckpointFogKept = 0.3f;

        /// <summary>What the suburbs keep clear for it: the walls, the tower and the yard behind.
        /// West of the avenue, so the crossroads in front of the gate stays a crossroads.</summary>
        public static Rect CheckpointZone => ZoneLayout.FromBounds(Checkpoint.x - 13f, -26f, Checkpoint.x + 2.6f, 9f);

        /// <summary>The roadside sign pointing the way in, on the verge just inside where the road comes through the old fence.</summary>
        public static Vector3 DinerSign => new Vector3(Fence.xMin + 5f, 0f, ZoneLayout.RoadCentreZ + ZoneLayout.RoadWidth * 0.5f + 2.2f);

        // --- The camera ---------------------------------------------------------------------------------

        /// <summary>
        /// Where the camera's pivot may go. Just past the clear ground on every side, so a player
        /// can look into the suburbs and the trees but never pan far enough to see them end. The
        /// camera sits south-east of its pivot and looks north-north-west, so the north and west
        /// limits are the tighter ones: the view already reaches further that way.
        /// </summary>
        public static Rect CameraBounds => ZoneLayout.FromBounds(ClearMinX - 12f, ClearMinZ - 11f, ClearMaxX + 12f, ClearMaxZ + 4f);

        // --- Fog ------------------------------------------------------------------------------------------

        public readonly struct FogLayer
        {
            public readonly float Height;
            public readonly float Opacity;
            public readonly float Start;
            public readonly float End;
            public readonly float TongueStrength;
            public readonly float NoiseScale;
            public readonly Vector4 Scroll;
            public readonly float Solid;

            public FogLayer(float height, float opacity, float start, float end, float tongue,
                            float noiseScale, Vector4 scroll, float solid = 0f)
            {
                Height = height;
                Opacity = opacity;
                Start = start;
                End = end;
                TongueStrength = tongue;
                NoiseScale = noiseScale;
                Scroll = scroll;
                Solid = solid;
            }
        }

        /// <summary>
        /// Low layers start early and thin, so the suburbs' first houses stand in a haze; high ones
        /// start deep and thick, so the tree crowns and roofs-that-were further in are swallowed.
        /// Only the low ones reach down the trails: a zombie should come out of ground mist, not out
        /// of a cloud at head height.
        /// </summary>
        public static readonly FogLayer[] FogLayers =
        {
            new FogLayer(0.35f, 0.5f, -6f, 22f, 0.9f, 0.045f, new Vector4(0.30f, 0.10f, -0.18f, 0.22f)),
            new FogLayer(1.4f, 0.45f, -4f, 24f, 0.7f, 0.035f, new Vector4(-0.25f, 0.14f, 0.20f, 0.12f)),
            new FogLayer(3.0f, 0.45f, 0f, 26f, 0.35f, 0.028f, new Vector4(0.22f, -0.10f, -0.14f, -0.20f)),
            new FogLayer(5.5f, 0.5f, 4f, 28f, 0f, 0.022f, new Vector4(0.16f, 0.08f, 0.12f, -0.10f)),
            new FogLayer(8.5f, 0.6f, 10f, 30f, 0f, 0.018f, new Vector4(-0.12f, 0.1f, 0.1f, 0.06f)),
            new FogLayer(12f, 0.95f, 16f, 34f, 0f, 0.015f, new Vector4(-0.10f, 0.06f, 0.08f, 0.10f), solid: 1f),
        };

        public static readonly Color FogColour = new Color(0.74f, 0.78f, 0.78f, 1f);
        public static readonly Color FogShade = new Color(0.60f, 0.65f, 0.66f, 1f);

        /// <summary>How far past the world's edge the fog carries on, so no camera angle finds its end.</summary>
        public const float FogOverhang = 90f;

        /// <summary>How far inside the clear ground the fog mesh starts. Enough to hold the tongues; a lane
        /// spawning deeper in gets a strip of mesh down to it (<see cref="EdgeFogBuilder"/>).</summary>
        public const float FogHoleInset = 12f;
    }
}
