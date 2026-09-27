using UnityEngine;

namespace FoodStreet.Visitors.EditorTools
{
    /// <summary>
    /// Every number the visitor scene setup uses, in one table.
    ///
    /// Written in the spirit of the blockout schedule this project already has: the road, the
    /// boundary and the car park have to agree with one another, and three of them holding
    /// three copies of "where the gate is" is three chances to be right in two places and wrong
    /// in the third.
    ///
    /// Almost nothing here is absolute. The boundary is read off the walls the level already
    /// has and moved by the deltas below, so a wall nudged by hand in the editor is not
    /// silently undone the next time the command runs. What IS absolute is the road and the
    /// apron, because neither exists yet and something has to say where they go.
    ///
    /// Conventions, matching the rest of the project: north is +Z, east is +X, everything sits
    /// at Y = 0, and a rectangle is stated as min/max rather than corner-plus-size so a number
    /// cannot be transposed on the way in.
    /// </summary>
    public static class ZoneLayout
    {
        // --- The road -----------------------------------------------------------------
        //
        // Measured off the one surviving road tile at (22.2, -7.1), which is what is left of
        // the street after the apocalypse dressing pass. The road runs east to west along the
        // south side of the compound.
        //
        // Both numbers are pinned by what is already standing either side of it, and there is
        // less room than there looks. The town's southern terrace fronts onto Z -9.2, and the
        // compound's south wall lands on Z -3.1 once it has been moved out for the car park.
        // Six metres, for a road and two verges - so a five metre carriageway centred between
        // them, which leaves about half a metre of verge on each side.
        //
        // A wider road here would not read as a wider road. It would read as tarmac running
        // through somebody's front room.

        public const float RoadCentreZ = -6.2f;

        public const float RoadWidth = 5f;

        /// <summary>
        /// Where the road leaves the map, west and east.
        ///
        /// Well past anything the camera can reach. The requirement is not "spawn off screen" -
        /// it is that nobody is ever seen arriving out of nothing, and forty metres of road
        /// between the spawn and the gate is what turns a spawn into a journey.
        /// </summary>
        public const float RoadWestEnd = -45f;

        public const float RoadEastEnd = 95f;

        /// <summary>Metres between the dashed lane markings.</summary>
        public const float LaneMarkSpacing = 3.2f;

        // --- Clearing -----------------------------------------------------------------

        /// <summary>
        /// The driving corridor: the road plus a shoulder either side.
        ///
        /// Anything whose renderer overlaps this is removed. Wider than the tarmac on purpose -
        /// a wrecked car half on the verge still blocks the lane it is leaning into, and a
        /// corridor measured to the kerb would leave exactly those.
        /// </summary>
        public static Rect RoadCorridor => FromBounds(
            RoadWestEnd, RoadCentreZ - RoadWidth * 0.5f - 1f,
            RoadEastEnd, RoadCentreZ + RoadWidth * 0.5f + 1f);

        /// <summary>
        /// The drive: from the road, through the gate, to the apron.
        ///
        /// Cleared as well as the road itself. A gateway with a rubble pile in it is a car park
        /// nobody can reach, and it fails in the least obvious way - the cars drive straight
        /// through the pile, because none of this collides.
        /// </summary>
        public static Rect GateApproach => FromBounds(
            GateCentreX - GateWidth * 0.5f - 1f, RoadCentreZ,
            GateCentreX + GateWidth * 0.5f + 1f, ApronMinZ);

        // --- The boundary -------------------------------------------------------------
        //
        // Deltas, applied to whatever the walls currently measure. Enough room for a car park
        // and the drive to reach it, and no more: the compound should read as having been
        // extended, not replaced.

        /// <summary>
        /// Metres the south wall moves out, toward the road.
        ///
        /// The only large one, because this is the band the car park goes in. Two rows of bays
        /// and a lane between them need about fifteen metres, and the compound only had twelve
        /// between its gate and the restaurant.
        /// </summary>
        public const float SouthExpansion = 7f;

        /// <summary>
        /// Metres the east and west walls move out. Large, because the restaurant and the yard
        /// round it outgrew the compound: 2.5 and 2.1 made room for the car park, and the
        /// starting-area layout (Last Orders / Layout) added 8 m east and 12 m west on top. West
        /// is the bigger one because the shelters, the garden and the stores stand along that
        /// wall and move out with it.
        ///
        /// The layout tool reads these, so change them here and re-run that rather than
        /// dragging the walls: a boundary moved by hand is put back by the next run of either.
        /// </summary>
        public const float EastExpansion = 10.5f;

        public const float WestExpansion = 14.1f;

        /// <summary>The north wall does not move. There is nothing behind the restaurant that
        /// needs the space, and moving it would push the compound off the terrain the level
        /// already dressed.</summary>
        public const float NorthExpansion = 0f;

        /// <summary>
        /// The gate, widened from the 8.2 m opening the level has.
        ///
        /// Wide enough for a car to turn in without clipping the post, which at the level's
        /// camera angle is the difference between a gate and a hole in a wall.
        /// </summary>
        public const float GateWidth = 10f;

        public const float GateCentreX = 20f;

        // --- The car park ---------------------------------------------------------------
        //
        // East of the gate, so it never sits in the line people walk from the gate to the door.
        // That lane runs straight up X 20 and is the busiest path on the site.

        public const float ApronMinX = 25.5f;
        public const float ApronMaxX = 36.5f;
        public const float ApronMinZ = -1f;
        public const float ApronMaxZ = 13.5f;

        public static Rect Apron => FromBounds(ApronMinX, ApronMinZ, ApronMaxX, ApronMaxZ);

        /// <summary>Bays per row. Two rows either side of one lane, which is the densest
        /// honest layout and the one every real car park uses.</summary>
        public const int BaysPerRow = 4;

        /// <summary>
        /// How deep one row is, once the lane has taken its share.
        ///
        /// A bay longer than this would overhang into the driving lane, which is invisible in
        /// the editor and obvious the first time two cars are parked opposite each other. The
        /// parking builder clamps to it and says so rather than letting it happen.
        /// </summary>
        public static float RowDepth => (ApronMaxZ - ApronMinZ - LaneWidth) * 0.5f;

        /// <summary>Metres of driving lane between the two rows.</summary>
        public const float LaneWidth = 4f;

        /// <summary>Where a car turns off the drive onto the apron, and where it rejoins.
        /// Offset from one another so arriving and leaving cars do not aim at one point.</summary>
        public static Vector3 LaneEntry => new Vector3(ApronMinX - 1.5f, 0f, LaneCentreZ + 1f);

        public static Vector3 LaneExit => new Vector3(ApronMinX - 1.5f, 0f, LaneCentreZ - 1f);

        /// <summary>The middle of the driving lane, with a row of bays either side of it.</summary>
        public static float LaneCentreZ => (ApronMinZ + ApronMaxZ) * 0.5f;

        // --- The drive and the path ---------------------------------------------------------
        //
        // Two surfaces that exist only so the cars and the people are not driving and walking
        // across grass. They carry no logic: the route nodes decide where anybody goes, and
        // these are laid to match so that what the player sees agrees with it.

        /// <summary>Metres across the drive from the gate to the apron. Wider than the gate is
        /// pointless and narrower than a car is worse than nothing.</summary>
        public const float DriveWidth = 6f;

        /// <summary>Metres across the footpath from the apron to the restaurant door.</summary>
        public const float PathWidth = 3f;

        public static Rect Drive => FromBounds(
            GateCentreX - DriveWidth * 0.5f, RoadCentreZ + RoadWidth * 0.5f,
            GateCentreX + DriveWidth * 0.5f, LaneCentreZ + LaneWidth * 0.5f);

        /// <summary>The elbow joining the drive to the apron, across the top of the lane.</summary>
        public static Rect DriveToApron => FromBounds(
            GateCentreX - DriveWidth * 0.5f, LaneCentreZ - LaneWidth * 0.5f,
            ApronMinX + 0.5f, LaneCentreZ + LaneWidth * 0.5f);

        /// <summary>The footpath from the apron up to the restaurant door. Its north end is
        /// filled in from the measured door, which is the only part of it that moves.</summary>
        public static Rect PathToDoor(float doorX, float doorZ) => FromBounds(
            doorX - PathWidth * 0.5f, LaneCentreZ,
            doorX + PathWidth * 0.5f, doorZ + 0.5f);

        // --- Route nodes -----------------------------------------------------------------

        public static Vector3 RoadStart => new Vector3(RoadWestEnd + 4f, 0f, RoadCentreZ);

        public static Vector3 RoadEnd => new Vector3(RoadEastEnd - 4f, 0f, RoadCentreZ);

        /// <summary>On the road, square with the gate. A car turns here rather than at the
        /// gate itself, so the turn is on tarmac.</summary>
        public static Vector3 GateOutside => new Vector3(GateCentreX, 0f, RoadCentreZ + 1.4f);

        /// <summary>Just inside the boundary. Filled in against the actual south wall by the
        /// setup command, which knows where the wall ended up.</summary>
        public static Vector3 GateInside(float southWallZ) =>
            new Vector3(GateCentreX, 0f, southWallZ + 2.5f);

        /// <summary>
        /// How far outside and inside the restaurant door its two route nodes sit.
        ///
        /// The door itself is measured from the building rather than stated here, because the
        /// building is art and may be moved. These are only the standoffs.
        ///
        /// The inside one is the shorter of the two: a node deeper into the room than the
        /// nearest table would send somebody past their own chair and back.
        /// </summary>
        public const float DoorClearance = 2.1f;

        public const float DoorInsideClearance = 1.4f;

        // --- Helpers ----------------------------------------------------------------------

        public static Rect FromBounds(float minX, float minZ, float maxX, float maxZ) =>
            new Rect(minX, minZ, maxX - minX, maxZ - minZ);

        /// <summary>The compound once widened, from the boundary as it originally stood.</summary>
        public static Rect Expanded(Rect original) => FromBounds(
            original.xMin - WestExpansion, original.yMin - SouthExpansion,
            original.xMax + EastExpansion, original.yMax + NorthExpansion);

        public static bool Overlaps(Rect rect, Bounds bounds)
        {
            return bounds.max.x >= rect.xMin && bounds.min.x <= rect.xMax &&
                   bounds.max.z >= rect.yMin && bounds.min.z <= rect.yMax;
        }
    }
}
