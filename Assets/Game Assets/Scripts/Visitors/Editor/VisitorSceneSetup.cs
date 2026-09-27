using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using K = LastOrders.EditorTools.PrimitiveKit;

namespace FoodStreet.Visitors.EditorTools
{
    /// <summary>
    /// Lays the road, widens the compound, builds the car park and wires the visitor system
    /// into whichever scene is open.
    ///
    /// Everything it creates lives under one root, and that root is destroyed before anything
    /// is built - so running this twice replaces the previous pass rather than doubling it.
    /// That is not a theoretical nicety: the command this replaces left three copies of its
    /// route in Main Level, each one eight nodes deep, and the scene gave no sign of it.
    ///
    /// The boundary is the one thing it edits rather than creates. Those walls are the level's
    /// own, so they are measured and moved rather than rebuilt: a wall nudged by hand keeps its
    /// height, its thickness, its material and its place in the hierarchy, and only the two
    /// numbers this command is entitled to change actually change.
    /// </summary>
    public static class VisitorSceneSetup
    {
        /// <summary>Everything this command builds hangs off here.</summary>
        const string SystemRoot = "VisitorSystem";

        const string ProfilePath = "Assets/Game Assets/Content/Visitors/VisitorTraffic.asset";

        /// <summary>
        /// Roots left by the visitor system this replaces. Destroyed rather than deactivated:
        /// they are a second, contradictory answer to where the road is, and a deactivated one
        /// is the kind of thing somebody re-enables in six months to find out what it did.
        /// </summary>
        static readonly string[] LegacyRoots = { "VisitorRoute", "VisitorSpawners" };

        /// <summary>Children of the old Visitors host that belonged to the customer layer. The
        /// Chef and the Helper stand beside them and are left alone.</summary>
        static readonly string[] LegacyVisitorChildren = { "OpenSign", "PorchLight" };

        /// <summary>
        /// The dressing groups the corridor sweep is allowed to delete out of.
        ///
        /// A list rather than the whole scene. The sweep deletes things, and a rule broad
        /// enough to reach the restaurant or the terrain is a rule that eventually does.
        /// </summary>
        static readonly string[] DressingGroups =
        {
            "Wrecks", "Rubble", "NearWallDebris", "StreetFurniture", "DeadFlora",
            "MarketRuins", "Barricades", "Scrub",
        };

        // Craters and GroundScars are deliberately NOT in that list. A road pitted with
        // scorches, cracks and the odd shell hole reads as a road that has seen things, and a
        // suspiciously clean strip through a ruined town is worse than an obstacle.
        //
        // An earlier version tried to express that as a height threshold - anything under a
        // quarter of a metre is a mark rather than an obstacle - and it was wrong in exactly
        // the case that mattered: a toppled lamp post lying flat across the carriageway is
        // half a metre of nothing, and it stayed.

        [MenuItem("Food Street/Visitors/Set Up Visitor System", priority = 1)]
        public static void Run()
        {
            if (!Preflight(out VisitorTrafficProfile profile, out Zone zone, dialogs: true)) return;

            bool proceed = EditorUtility.DisplayDialog(
                "Food Street",
                $"This edits '{SceneManager.GetActiveScene().name}' in place:\n\n" +
                "- Clears wrecks, rubble and junk off the road and the gateway\n" +
                "- Lays a road running off both edges of the map\n" +
                "- Widens the compound boundary and the gate\n" +
                "- Builds a car park inside the south-east corner\n" +
                "- Wires the visitor system to the level's own chairs\n\n" +
                "The restaurant, the terrain and the town are not touched.",
                "Set up", "Cancel");

            if (!proceed) return;

            Apply(profile, zone);
        }

        /// <summary>
        /// The same work with no dialogs, for a script or a build step.
        ///
        /// Split out rather than guarded by a flag inside Run, because a confirmation dialog is
        /// not a detail of the work - it is the whole difference between a command somebody
        /// chose and a command something ran.
        /// </summary>
        public static bool ApplyUnattended()
        {
            if (!Preflight(out VisitorTrafficProfile profile, out Zone zone, dialogs: false))
                return false;

            Apply(profile, zone);
            return true;
        }

        /// <summary>
        /// The two things that have to exist before any of this means anything: content to send
        /// and a compound to send it to.
        /// </summary>
        static bool Preflight(out VisitorTrafficProfile profile, out Zone zone, bool dialogs)
        {
            profile = AssetDatabase.LoadAssetAtPath<VisitorTrafficProfile>(ProfilePath);
            zone = null;

            if (profile == null)
            {
                Complain(dialogs, "No visitor traffic profile found. Run " +
                                  "'Food Street / Visitors / Generate Visitor Content' first.");
                return false;
            }

            zone = Object.FindFirstObjectByType<Zone>();

            if (zone == null)
            {
                Complain(dialogs, $"'{SceneManager.GetActiveScene().name}' has no Zone, so there " +
                                  "is no compound to widen and no door to walk to.");
                return false;
            }

            return true;
        }

        static void Complain(bool dialogs, string message)
        {
            if (dialogs) EditorUtility.DisplayDialog("Food Street", message, "OK");
            else Debug.LogError($"[Visitors] {message}");
        }

        static void Apply(VisitorTrafficProfile profile, Zone zone)
        {
            Scene scene = SceneManager.GetActiveScene();

            K.Reset();

            int removedLegacy = RemoveLegacy();
            int cleared = ClearCorridor();

            Transform root = new GameObject(SystemRoot).transform;

            BuildRoad(K.Group("Road", root));

            Rect widened = ExpandBoundary(zone, out float gateCentreX, out Rect original);
            float southWallZ = widened.yMin;

            Vector3 doorOutside = MeasureDoor(zone, out Vector3 doorInside);

            BuildYard(K.Group("Yard", root), original, widened);
            BuildDrive(K.Group("Drive", root), doorOutside);

            ParkingLot parking = BuildParking(K.Group("Parking", root), profile);
            Transform nodes = BuildRouteNodes(root, southWallZ, gateCentreX, doorOutside, doorInside);

            var roads = root.gameObject.AddComponent<VisitorRoads>();
            roads.Configure(Node(nodes, "RoadStart"), Node(nodes, "RoadEnd"),
                            Node(nodes, "GateOutside"), Node(nodes, "GateInside"),
                            Node(nodes, "DoorOutside"), Node(nodes, "DoorInside"));

            var seats = root.gameObject.AddComponent<SeatRegistry>();

            Transform pool = K.Group("Pool", root);

            var manager = root.gameObject.AddComponent<VisitorManager>();
            manager.Configure(profile, roads, parking, seats, pool);

            EditorSceneManager.MarkSceneDirty(scene);

            int chairs = CountChairs();
            int tables = Object.FindObjectsByType<TableBehaviour>(FindObjectsSortMode.None).Length;

            Debug.Log($"[Visitors] Set up in '{scene.name}'.\n" +
                      $"{removedLegacy} objects of the old visitor system removed, " +
                      $"{cleared} obstacles cleared off the road and the gateway.\n" +
                      $"{parking.Slots.Count} parking bays built, {chairs} chairs found on " +
                      $"{tables} tables.\n" +
                      $"Door at {doorOutside.x:0.0}, south wall at {southWallZ:0.0}.\n" +
                      "Save the scene, then press play: a car arrives once a chair and a bay are " +
                      "both free.");

            if (chairs == 0)
            {
                Debug.LogWarning("[Visitors] No chairs in the level, so nobody will ever arrive. " +
                                 "Place at least one table with a TableBehaviour on it.");
            }

            Selection.activeGameObject = root.gameObject;
        }

        // --- Taking the old system out ------------------------------------------------------

        static int RemoveLegacy()
        {
            int removed = 0;

            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == SystemRoot || System.Array.IndexOf(LegacyRoots, go.name) >= 0)
                {
                    removed += go.GetComponentsInChildren<Transform>(true).Length;
                    Object.DestroyImmediate(go);
                    continue;
                }

                if (go.name != "Visitors") continue;

                foreach (string child in LegacyVisitorChildren)
                {
                    Transform found = go.transform.Find(child);
                    if (found == null) continue;

                    removed += found.GetComponentsInChildren<Transform>(true).Length;
                    Object.DestroyImmediate(found.gameObject);
                }
            }

            // Fixtures the old command welded onto every table: a dirty-plate visual, a clear
            // point and a seat per chair. The chairs themselves are the level's own and stay.
            foreach (TableBehaviour table in
                     Object.FindObjectsByType<TableBehaviour>(FindObjectsSortMode.None))
            {
                foreach (string stale in new[] { "LastOrders_Fixtures", "Seats" })
                {
                    Transform previous = table.transform.Find(stale);
                    if (previous == null) continue;

                    removed += previous.GetComponentsInChildren<Transform>(true).Length;
                    Object.DestroyImmediate(previous.gameObject);
                }
            }

            return removed;
        }

        // --- Clearing the road ---------------------------------------------------------------

        /// <summary>
        /// Deletes anything a car would hit on the road or in the gateway.
        ///
        /// Renderer bounds rather than pivots. Half of this dressing is a cluster of pieces
        /// under one parent whose pivot sits several metres from the mass of it, and a pivot
        /// test leaves exactly the wrecks that are lying across the lane.
        ///
        /// The old Roads root goes too, whatever it contains. One surviving tile of it sits in
        /// the middle of the new road and would z-fight with it, which reads as a flickering
        /// stripe nobody can find the cause of.
        /// </summary>
        static int ClearCorridor()
        {
            Rect road = ZoneLayout.RoadCorridor;
            Rect gate = ZoneLayout.GateApproach;

            int cleared = 0;

            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == "Roads")
                {
                    cleared += go.GetComponentsInChildren<Transform>(true).Length;
                    Object.DestroyImmediate(go);
                    continue;
                }

                foreach (string group in DressingGroups)
                {
                    Transform host = FindDeep(go.transform, group);
                    if (host == null) continue;

                    cleared += ClearGroup(host, road, gate);
                }
            }

            return cleared;
        }

        static int ClearGroup(Transform group, Rect road, Rect gate)
        {
            var doomed = new List<GameObject>();

            for (int i = 0; i < group.childCount; i++)
            {
                Transform child = group.GetChild(i);
                if (!TryMeasure(child, out Bounds bounds)) continue;

                if (!ZoneLayout.Overlaps(road, bounds) && !ZoneLayout.Overlaps(gate, bounds))
                    continue;

                doomed.Add(child.gameObject);
            }

            foreach (GameObject go in doomed) Object.DestroyImmediate(go);

            return doomed.Count;
        }

        static bool TryMeasure(Transform target, out Bounds bounds)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            return true;
        }

        // --- The road --------------------------------------------------------------------------

        /// <summary>
        /// One tarmac slab and a dashed line, running off both edges of the map.
        ///
        /// Off BOTH edges, because a road that stops at the world rectangle is a corridor, and
        /// the whole point of arriving by car is that these people are coming from somewhere.
        /// </summary>
        static void BuildRoad(Transform parent)
        {
            float west = ZoneLayout.RoadWestEnd;
            float east = ZoneLayout.RoadEastEnd;
            float z = ZoneLayout.RoadCentreZ;

            K.Cube("Surface", parent, new Vector3((west + east) * 0.5f, 0.02f, z),
                   new Vector3(east - west, 0.04f, ZoneLayout.RoadWidth), K.Asphalt);

            for (float x = west + 2f; x < east; x += ZoneLayout.LaneMarkSpacing)
            {
                K.Cube("Line", parent, new Vector3(x, 0.05f, z),
                       new Vector3(1.4f, 0.02f, 0.16f), K.RoadLine);
            }

            // A strip of kerb on the compound side, so the road has an edge rather than fading
            // into the terrain.
            K.Cube("Kerb", parent,
                   new Vector3((west + east) * 0.5f, 0.06f, z + ZoneLayout.RoadWidth * 0.5f + 0.2f),
                   new Vector3(east - west, 0.12f, 0.4f), K.Stone);
        }

        /// <summary>
        /// The drive from the gate to the apron, and the footpath from the apron to the door.
        ///
        /// Cosmetic, and worth it. Cars and people already go this way - the route nodes say so
        /// - but on bare grass the drive reads as vehicles cutting across a lawn, and the first
        /// thing anybody watching would report is that the car park has no entrance.
        ///
        /// Laid from the same rectangles the nodes are placed against, so the surface cannot
        /// disagree with the path taken over it.
        /// </summary>
        static void BuildDrive(Transform parent, Vector3 doorOutside)
        {
            Slab(parent, "Drive", ZoneLayout.Drive, K.Asphalt, 0.025f);
            Slab(parent, "DriveToApron", ZoneLayout.DriveToApron, K.Asphalt, 0.025f);

            Slab(parent, "PathToDoor", ZoneLayout.PathToDoor(doorOutside.x, doorOutside.z),
                 K.Soil, 0.03f);
        }

        /// <summary>
        /// Surfaces the ground the compound has just gained.
        ///
        /// The level's own floor mesh stops at the old boundary, so widening the walls leaves an
        /// L of bare terrain inside them - and a compound whose floor does not reach its own
        /// fence reads as a wall that was dragged rather than a yard that was extended.
        ///
        /// Three strips rather than one big slab over everything: laying a new floor across the
        /// whole compound would draw over the authored one, and the two are not the same green.
        ///
        /// Only a fallback, though. When the level's wasteland is laid as aprons over one grass
        /// plane, pulling them back shows the level's own grass right up to the new wall, and no
        /// strip is needed at all.
        /// </summary>
        static void BuildYard(Transform parent, Rect original, Rect widened)
        {
            if (widened.height <= 0f || original.height <= 0f) return;

            if (LastOrders.EditorTools.CompoundGround.OpenWindow(widened)) return;

            Strip(parent, "Yard_South", widened.xMin, widened.yMin, widened.xMax, original.yMin);
            Strip(parent, "Yard_West", widened.xMin, original.yMin, original.xMin, widened.yMax);
            Strip(parent, "Yard_East", original.xMax, original.yMin, widened.xMax, widened.yMax);
            Strip(parent, "Yard_North", widened.xMin, original.yMax, widened.xMax, widened.yMax);
        }

        /// <summary>One band of new ground, skipped when the boundary did not move on that
        /// side. A zero-width cube is invisible, but it is also a lie in the hierarchy.</summary>
        static void Strip(Transform parent, string name, float minX, float minZ, float maxX,
                          float maxZ)
        {
            if (maxX - minX < 0.05f || maxZ - minZ < 0.05f) return;

            Slab(parent, name, ZoneLayout.FromBounds(minX, minZ, maxX, maxZ), K.Grass, 0.01f);
        }

        static void Slab(Transform parent, string name, Rect area, Color colour, float height)
        {
            K.Cube(name, parent, new Vector3(area.center.x, height, area.center.y),
                   new Vector3(area.width, 0.04f, area.height), colour);
        }

        // --- The boundary -----------------------------------------------------------------------

        /// <summary>
        /// Moves the compound's own walls out, and widens the gate.
        ///
        /// Each wall is classified by the direction it runs rather than by its name, so the
        /// numbering the level happens to use - Cube (5) through Cube (9) - does not matter and
        /// cannot be relied on to keep meaning what it means today.
        ///
        /// The expansion is measured from the rectangle recorded by <see cref="ZoneBoundary"/>,
        /// not from wherever the walls currently stand. That is what makes running this twice
        /// harmless: deltas applied to the current positions would expand an already-expanded
        /// compound, and the only sign of it would be a boundary slowly walking off the map.
        ///
        /// Returns the widened rectangle, and reports the original through
        /// <paramref name="original"/> so the new ground can be surfaced to match.
        /// </summary>
        static Rect ExpandBoundary(Zone zone, out float gateCentreX, out Rect original)
        {
            gateCentreX = ZoneLayout.GateCentreX;
            original = ZoneLayout.Apron;

            Transform walls = FindDeep(zone.transform, "OuterBoundries");
            if (walls == null)
            {
                Debug.LogWarning("[Visitors] No OuterBoundries under the Zone, so the compound " +
                                 "was left at its current size. The car park may overlap it.");
                return ZoneLayout.Apron;
            }

            var alongX = new List<Transform>();
            var alongZ = new List<Transform>();

            for (int i = 0; i < walls.childCount; i++)
            {
                Transform wall = walls.GetChild(i);
                if (!wall.gameObject.activeSelf) wall.gameObject.SetActive(true);

                Vector3 run = wall.forward;

                if (Mathf.Abs(run.x) > Mathf.Abs(run.z)) alongX.Add(wall);
                else alongZ.Add(wall);
            }

            if (alongX.Count < 2 || alongZ.Count < 2)
            {
                Debug.LogWarning("[Visitors] The boundary is not the four-sided run this expects, " +
                                 "so it was left alone.");
                return ZoneLayout.Apron;
            }

            // North and south are the extremes of the runs that travel along X; east and west,
            // of those that travel along Z.
            alongX.Sort((a, b) => a.position.z.CompareTo(b.position.z));
            alongZ.Sort((a, b) => a.position.x.CompareTo(b.position.x));

            Transform north = alongX[alongX.Count - 1];
            Transform west = alongZ[0];
            Transform east = alongZ[alongZ.Count - 1];

            // Everything else running along X at the low end is the south face, which the level
            // splits into two runs either side of the gate.
            var south = new List<Transform>();
            for (int i = 0; i < alongX.Count; i++)
            {
                if (alongX[i] != north) south.Add(alongX[i]);
            }

            Rect start = Remember(walls, north, south[0], west, east);
            original = start;

            Rect target = ZoneLayout.Expanded(start);
            float newNorthZ = target.yMax;
            float newSouthZ = target.yMin;
            float newWestX = target.xMin;
            float newEastX = target.xMax;

            SetRun(north, alongX: true, line: newNorthZ, from: newWestX, to: newEastX);
            SetRun(west, alongX: false, line: newWestX, from: newSouthZ, to: newNorthZ);
            SetRun(east, alongX: false, line: newEastX, from: newSouthZ, to: newNorthZ);

            float gateMin = gateCentreX - ZoneLayout.GateWidth * 0.5f;
            float gateMax = gateCentreX + ZoneLayout.GateWidth * 0.5f;

            // Two runs either side of the opening. Sorted first, so the west half of the wall
            // stays the west half rather than jumping across the gate.
            south.Sort((a, b) => a.position.x.CompareTo(b.position.x));

            SetRun(south[0], alongX: true, line: newSouthZ, from: newWestX, to: gateMin);

            if (south.Count > 1)
                SetRun(south[south.Count - 1], alongX: true, line: newSouthZ, from: gateMax, to: newEastX);

            // Any extra south runs would now be duplicates lying across the gate.
            for (int i = 1; i < south.Count - 1; i++) south[i].gameObject.SetActive(false);

            return ZoneLayout.FromBounds(newWestX, newSouthZ, newEastX, newNorthZ);
        }

        /// <summary>
        /// The rectangle the boundary started at, measured once and kept.
        ///
        /// Measured from the wall lines rather than from their bounds, because a wall is a long
        /// thin cube and its bounds include its own thickness - which would grow the recorded
        /// rectangle by half a metre a side for no reason anybody could later explain.
        /// </summary>
        static Rect Remember(Transform walls, Transform north, Transform south, Transform west,
                             Transform east)
        {
            var memory = walls.GetComponent<ZoneBoundary>() ?? walls.gameObject.AddComponent<ZoneBoundary>();

            if (memory.HasOriginal) return memory.Original;

            Rect measured = ZoneLayout.FromBounds(west.position.x, south.position.z,
                                                  east.position.x, north.position.z);

            memory.Record(measured);

            Debug.Log($"[Visitors] Recorded the compound's original boundary: " +
                      $"x {measured.xMin:0.0} to {measured.xMax:0.0}, " +
                      $"z {measured.yMin:0.0} to {measured.yMax:0.0}. " +
                      "Every future expansion is measured from this.");

            return measured;
        }

        /// <summary>
        /// Puts a wall on a line and stretches it between two points.
        ///
        /// The wall's own thickness, height and rotation are left exactly as they were. These
        /// are the level's objects: this command is entitled to say where a wall starts and
        /// stops, and to nothing else about it.
        /// </summary>
        static void SetRun(Transform wall, bool alongX, float line, float from, float to)
        {
            float length = Mathf.Max(0.5f, to - from);
            float centre = (from + to) * 0.5f;

            Vector3 position = wall.position;

            wall.position = alongX
                ? new Vector3(centre, position.y, line)
                : new Vector3(line, position.y, centre);

            // Length lives on local Z for every one of these walls: they are unit cubes turned
            // to face along their run, which is why the run direction is read off `forward`.
            Vector3 scale = wall.localScale;
            wall.localScale = new Vector3(scale.x, scale.y, length);
        }

        // --- The door ---------------------------------------------------------------------------

        /// <summary>
        /// Finds the restaurant doorway: the gap in the south face of its walls.
        ///
        /// Measured rather than stated, because the building is art and may be moved. The gap is
        /// the whole reason this is worth the trouble - the straight line from the car park to a
        /// chair does not go through the door, and customers walking through the wall is the
        /// kind of thing nobody reports and everybody sees.
        /// </summary>
        static Vector3 MeasureDoor(Zone zone, out Vector3 inside)
        {
            Transform walls = FindDeep(zone.transform, "RestaurantWalls");

            if (walls == null)
            {
                Debug.LogWarning("[Visitors] No RestaurantWalls under the Zone, so the door was " +
                                 "guessed from the tables. Check the DoorOutside node.");

                Vector3 guess = DiningRoomCentre(zone);
                inside = guess;
                return guess + Vector3.back * 3f;
            }

            var face = new List<Transform>();
            float lowest = float.PositiveInfinity;

            for (int i = 0; i < walls.childCount; i++)
            {
                Transform wall = walls.GetChild(i);
                if (Mathf.Abs(wall.forward.x) <= Mathf.Abs(wall.forward.z)) continue;

                lowest = Mathf.Min(lowest, wall.position.z);
                face.Add(wall);
            }

            face.RemoveAll(w => w.position.z > lowest + 0.5f);
            face.Sort((a, b) => a.position.x.CompareTo(b.position.x));

            float doorX = ZoneLayout.GateCentreX;

            for (int i = 0; i + 1 < face.Count; i++)
            {
                float leftEdge = face[i].position.x + face[i].localScale.z * 0.5f;
                float rightEdge = face[i + 1].position.x - face[i + 1].localScale.z * 0.5f;

                if (rightEdge - leftEdge < 0.5f) continue;

                doorX = (leftEdge + rightEdge) * 0.5f;
                break;
            }

            inside = new Vector3(doorX, 0f, lowest + ZoneLayout.DoorInsideClearance);
            return new Vector3(doorX, 0f, lowest - ZoneLayout.DoorClearance);
        }

        static Vector3 DiningRoomCentre(Zone zone)
        {
            var tables = Object.FindObjectsByType<TableBehaviour>(FindObjectsSortMode.None);
            if (tables.Length == 0) return zone.transform.position;

            Vector3 total = Vector3.zero;
            for (int i = 0; i < tables.Length; i++) total += tables[i].transform.position;

            return total / tables.Length;
        }

        // --- The car park -------------------------------------------------------------------------

        /// <summary>
        /// A paved apron with two rows of bays either side of one lane.
        ///
        /// Every bay is sized to the largest vehicle in the traffic profile rather than to the
        /// car that happens to use it, so a long van cannot be handed a bay it overhangs by
        /// half a metre into the one behind.
        ///
        /// Cars park facing the lane. Nose-in looks marginally better on arrival and much worse
        /// on departure, where the car would have to turn on the spot against a wall; parked
        /// facing out, leaving is a straight line.
        /// </summary>
        static ParkingLot BuildParking(Transform parent, VisitorTrafficProfile profile)
        {
            var lot = parent.gameObject.AddComponent<ParkingLot>();

            Rect apron = ZoneLayout.Apron;
            Vector2 bay = profile.LargestBay();

            // Clamped rather than trusted. A bay drawn longer than its row overhangs the
            // driving lane, which looks fine in the editor and wrong the first time two cars
            // are parked opposite one another.
            float rowDepth = ZoneLayout.RowDepth;
            float maxWidth = apron.width / ZoneLayout.BaysPerRow;

            if (bay.y > rowDepth || bay.x > maxWidth)
            {
                Debug.LogWarning(
                    $"[Visitors] The largest vehicle needs a {bay.x:0.0} x {bay.y:0.0} m bay, " +
                    $"but the apron only allows {maxWidth:0.0} x {rowDepth:0.0} m. The markings " +
                    "were clamped. Shorten the vehicle or widen the apron in ZoneLayout.");

                bay = new Vector2(Mathf.Min(bay.x, maxWidth), Mathf.Min(bay.y, rowDepth));
            }

            K.Cube("Apron", parent,
                   new Vector3(apron.center.x, 0.02f, apron.center.y),
                   new Vector3(apron.width, 0.04f, apron.height), K.Asphalt);

            float laneZ = ZoneLayout.LaneCentreZ;
            float laneHalf = ZoneLayout.LaneWidth * 0.5f;

            float southDepth = (laneZ - laneHalf) - apron.yMin;
            float northDepth = apron.yMax - (laneZ + laneHalf);

            float southCentre = apron.yMin + southDepth * 0.5f;
            float northCentre = apron.yMax - northDepth * 0.5f;

            float step = apron.width / ZoneLayout.BaysPerRow;

            var slots = new List<ParkingSlot>();

            for (int i = 0; i < ZoneLayout.BaysPerRow; i++)
            {
                float x = apron.xMin + step * (i + 0.5f);

                slots.Add(Bay(parent, lot, $"Bay_S{i}", new Vector3(x, 0f, southCentre),
                              facing: Vector3.forward, bay));

                slots.Add(Bay(parent, lot, $"Bay_N{i}", new Vector3(x, 0f, northCentre),
                              facing: Vector3.back, bay));
            }

            Transform entry = Marker(parent, "LaneEntry", ZoneLayout.LaneEntry);
            Transform exit = Marker(parent, "LaneExit", ZoneLayout.LaneExit);
            lot.Configure(entry, exit);

            // Registered here as well as in OnEnable. The bays this command just built are not
            // enabled again before the scene is saved, so without this the lot would come out
            // of the builder empty and only fill itself on the next play.
            foreach (ParkingSlot slot in slots) lot.Register(slot);

            return lot;
        }

        static ParkingSlot Bay(Transform parent, ParkingLot lot, string name, Vector3 at,
                               Vector3 facing, Vector2 size)
        {
            Transform root = K.Group(name, parent);
            root.position = at;
            root.rotation = Quaternion.LookRotation(facing, Vector3.up);

            // Painted markings rather than kerbs: a bay a car can drive over is a bay a slightly
            // wrong waypoint does not park on top of.
            foreach (float dx in new[] { -size.x * 0.5f, size.x * 0.5f })
            {
                K.Cube("Line", root, new Vector3(dx, 0.045f, 0f),
                       new Vector3(0.12f, 0.02f, size.y), K.RoadLine);
            }

            K.Cube("Stop", root, new Vector3(0f, 0.045f, -size.y * 0.5f),
                   new Vector3(size.x, 0.02f, 0.12f), K.RoadLine);

            var slot = root.gameObject.AddComponent<ParkingSlot>();
            slot.Configure(lot, new Vector3(size.x * 0.52f, 0f, 0f));

            return slot;
        }

        // --- Route nodes ----------------------------------------------------------------------------

        static Transform BuildRouteNodes(Transform parent, float southWallZ, float gateCentreX,
                                         Vector3 doorOutside, Vector3 doorInside)
        {
            Transform root = K.Group("Route", parent);

            Marker(root, "RoadStart", ZoneLayout.RoadStart);
            Marker(root, "RoadEnd", ZoneLayout.RoadEnd);
            Marker(root, "GateOutside", new Vector3(gateCentreX, 0f, ZoneLayout.GateOutside.z));
            Marker(root, "GateInside", ZoneLayout.GateInside(southWallZ));
            Marker(root, "DoorOutside", doorOutside);
            Marker(root, "DoorInside", doorInside);

            return root;
        }

        static Transform Marker(Transform parent, string name, Vector3 at)
        {
            Transform node = K.Group(name, parent);
            node.position = at;

            return node;
        }

        static Transform Node(Transform parent, string id)
        {
            Transform node = parent != null ? parent.Find(id) : null;

            if (node == null)
                Debug.LogError($"[Visitors] No route node '{id}'. Cars will not drive.");

            return node;
        }

        // --- Helpers ---------------------------------------------------------------------------------

        static int CountChairs()
        {
            int chairs = 0;

            foreach (TableBehaviour table in
                     Object.FindObjectsByType<TableBehaviour>(FindObjectsSortMode.None))
            {
                if (table.Chairs != null) chairs += table.Chairs.Length;
            }

            return chairs;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
