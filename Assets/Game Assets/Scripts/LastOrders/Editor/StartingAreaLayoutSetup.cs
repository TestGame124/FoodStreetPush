using System.Collections.Generic;
using System.Text;
using FoodStreet.Placement;
using FoodStreet.Visitors;
using FoodStreet.Visitors.EditorTools;
using LastOrders.Core;
using LastOrders.Expansion;
using LastOrders.Jobs;
using LastOrders.Night;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Widens the starting area, and the restaurant in the middle of it, in the open scene.
    ///
    /// Both had run out of room. The restaurant was 11.7 by 10.5 m with the stove, the sink, the
    /// fridge, the counter and two tables already in it, and there was no floor left for another
    /// cooking station; the yard round it had nowhere for a bigger building to go.
    ///
    /// The compound: the west and east walls move out to where <see cref="ZoneLayout"/> puts them.
    /// The visitor setup widens from the same numbers, so running either tool again agrees with
    /// the other. The south wall stays on the road and the north wall stays where the courtyard
    /// meets it; the courtyard is widened so its new wall still meets the east one. Everything
    /// standing against the west or east wall - the shelters, the hospital tent, the garden, the
    /// stores, the tower sites, the junk propped against the brickwork - moves out with its wall,
    /// so the ring of facilities keeps its shape and the new ground opens up in the middle. Town
    /// dressing inside the new line is cleared, the level's grass is shown up to it, and the
    /// build grid - which the navigation mirrors - is widened to cover it.
    ///
    /// The restaurant: stretched to <see cref="RestaurantTarget"/> from its south face, which does
    /// not move, so the door, the footpath, the shelter's door points and every route node that
    /// meets them stay where they are. The room stretches round its contents: the kitchen along
    /// the back wall keeps its distance from the back wall and the nearer side wall, the counter
    /// moves back a metre, and the tables spread toward the side walls. Workers standing just
    /// outside a side wall move out with it.
    ///
    /// Safe to re-run. Every move is measured from where the walls stand now to where they
    /// should be, so a second run has nothing left to move. The compound walls and the courtyard
    /// are rebuilt from the boundary on every run, as their own tools do.
    /// </summary>
    public static class StartingAreaLayoutSetup
    {
        const string BoundaryName = "OuterBoundries";
        const string RestaurantWallsName = "RestaurantWalls";

        const string GridSettingsPath = "Assets/Game Assets/Content/LastOrders/Grid/StartingArea_Grid.asset";

        /// <summary>
        /// The restaurant's outer wall faces once widened, world metres (x and y of the rect are
        /// world X and Z). About twice the old floor: 20.6 by 13.5 m against 11.7 by 10.5. The door
        /// stays near the middle of the south face, and the north-east corner stops short of the
        /// main character's post by the gap out to the courtyard. The south edge is for reading
        /// only: the south face is measured and never moved.
        /// </summary>
        static readonly Rect RestaurantTarget = Rect.MinMaxRect(9.5f, 16.2f, 30.1f, 29.7f);

        /// <summary>How far in from the west or east wall something can stand and still move
        /// with it. Reaches the garden beds but stops well short of the restaurant.</summary>
        const float WallsideDepth = 9.5f;

        /// <summary>How deep the kitchen line along the restaurant's back wall is.</summary>
        const float KitchenDepth = 3f;

        /// <summary>How much of the room's growth the counter moves back by.</summary>
        const float CounterShare = 0.4f;

        /// <summary>How much of the room's growth the tables spread out by.</summary>
        const float TableShare = 0.7f;

        /// <summary>Workers standing this close outside a restaurant side wall move out with it.</summary>
        const float BesideWallDepth = 8f;

        /// <summary>
        /// How far dressing has to reach inside the new wall line to be cleared. A heap leaning on
        /// the outside of the wall reads as junk piled against it; one standing a metre inside
        /// reads as a mistake.
        /// </summary>
        const float DressingIntrusion = 0.4f;

        /// <summary>The same for flat ground dressing, which shows through the grass at any depth.</summary>
        const float GroundIntrusion = 0.2f;

        /// <summary>Metres of grid kept beyond the walls, so the navigation's clearance from its
        /// own edge never reaches the ground inside them.</summary>
        const float GridMargin = 2f;

        /// <summary>
        /// Groups of the town's dressing that can be cleared from inside the new wall line. Not
        /// the town's buildings, and not the wasteland, whose flat pieces are handled apart.
        /// </summary>
        static readonly string[] DressingGroups =
        {
            "Wrecks", "Rubble", "NearWallDebris", "StreetFurniture", "DeadFlora", "MarketRuins",
            "Barricades", "GroundScars", "Craters",
        };

        /// <summary>The wasteland's flat patches and scrub, under ApocalypseEnvironment/Wasteland.</summary>
        static readonly string[] GroundGroups = { "Patches", "Scrub" };

        /// <summary>Roots whose children are facilities sited against the compound walls.</summary>
        static readonly string[] WallsideRoots = { "Shelters", "Facilities", "Defenses" };

        /// <summary>One straight-sided rectangle of wall runs: the compound, or the restaurant.</summary>
        sealed class WallSet
        {
            public Transform North;
            public Transform West;
            public Transform East;

            /// <summary>The south face, west to east. More than one piece when it has an opening.</summary>
            public readonly List<Transform> South = new List<Transform>();

            /// <summary>The wall lines themselves, not their faces.</summary>
            public Rect Lines => Rect.MinMaxRect(West.position.x, South[0].position.z, East.position.x, North.position.z);
        }

        [MenuItem("Last Orders/Layout/Expand Starting Area & Restaurant (Current Scene)", priority = 20)]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();

            if (scope == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "This scene has no GameLifetimeScope, so there are no compound walls to rebuild.\n\n" +
                    "Open the level that has the Last Orders systems in it.", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                $"This edits '{scene.name}' in place:\n\n" +
                "- Moves the compound's west and east walls out, and widens the courtyard to match\n" +
                "- Moves the shelters, tent, garden, stores and tower sites out with their walls\n" +
                "- Clears the town's junk from inside the new walls and shows the grass up to them\n" +
                "- Widens the build and navigation grid to cover the new ground\n" +
                "- Makes the restaurant about twice the size, keeping its door where it is\n" +
                "- Rebuilds the compound walls and gates\n\n" +
                "Back the scene up first if it is not under version control.",
                "Expand", "Cancel");

            if (!proceed) return;

            Debug.Log(Apply(scope, save: true));
        }

        /// <summary>The setup against the open scene's scope, with no dialogs - for automation.</summary>
        public static string ApplyToActiveScene(bool save)
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            return scope != null
                ? Apply(scope, save)
                : "[Last Orders] No GameLifetimeScope in the open scene; nothing was changed.";
        }

        /// <summary>The whole layout, with no dialogs, so it can also be driven from a script.</summary>
        public static string Apply(GameLifetimeScope scope, bool save)
        {
            Scene scene = scope.gameObject.scene;

            Transform boundary = FindInScene(scene, BoundaryName);
            if (boundary == null)
                return $"[Last Orders] No '{BoundaryName}' in '{scene.name}', so there is no compound to widen. Nothing was changed.";

            var memory = boundary.GetComponent<ZoneBoundary>();
            if (memory == null || !memory.HasOriginal)
            {
                return "[Last Orders] The compound boundary has no recorded original to widen from. Run " +
                       "'Food Street / Visitors / Set Up Visitor System' first. Nothing was changed.";
            }

            if (!ReadWalls(boundary, out WallSet compound))
                return "[Last Orders] The compound boundary is not the four-sided run this expects. Nothing was changed.";

            Undo.SetCurrentGroupName("Expand starting area");
            int undoGroup = Undo.GetCurrentGroup();

            var report = new StringBuilder("[Last Orders] Starting area laid out.\n");

            Rect current = compound.Lines;
            Rect target = ZoneLayout.Expanded(memory.Original);
            float westShift = target.xMin - current.xMin;
            float eastShift = target.xMax - current.xMax;

            report.Append($"  Compound: X {target.xMin:0.0} to {target.xMax:0.0}, Z {target.yMin:0.0} to {target.yMax:0.0} " +
                          $"({target.width:0} x {target.height:0} m; west wall moved {westShift:0.0} m, east {eastShift:0.0} m).\n");

            // Cleared before anything moves out: the facilities are about to stand where the junk was.
            int cleared = ClearDressing(target, current);
            int moved = ShiftWallside(scene, current, westShift, eastShift);
            int runs = MoveBoundary(compound, current, target);

            report.Append($"  {cleared} pieces of town dressing cleared from inside the new walls, " +
                          $"{moved} things moved out with their wall, {runs} boundary runs re-laid.\n");

            if (CompoundGround.OpenWindow(target))
            {
                int strips = RemoveYardStrips(scene);
                report.Append($"  Grass shown up to the walls{(strips > 0 ? $"; {strips} old yard strips removed" : "")}.\n");
            }
            else
            {
                report.Append("  WARNING: no wasteland aprons to pull back, so the new ground is bare terrain.\n");
            }

            int patches = ShiftTownPatches(current, westShift, eastShift);
            if (patches > 0) report.Append($"  {patches} town zombie patches moved out beyond the new walls.\n");

            report.Append($"  {EnsureGrid(target)}\n");
            report.Append($"  {ExpandRestaurant(scene)}\n");

            if (Object.FindFirstObjectByType<ExpansionArea>(FindObjectsInactive.Include) != null)
            {
                ExpansionSceneSetup.Apply(scope, save: false);
                report.Append("  Courtyard re-laid to meet the new east wall.\n");
            }

            report.Append(CompoundWallSceneSetup.Apply(scope, save: false));

            Undo.CollapseUndoOperations(undoGroup);

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);
            else report.Append("The scene is not saved.\n");

            return report.ToString();
        }

        // --- Reading walls -----------------------------------------------------------------------

        /// <summary>
        /// Sorts a set of boundary cubes into north, south, west and east by the way each one
        /// runs, not by its name. Every piece is a unit cube turned to face along its run, with
        /// its length on local Z, which is why the run is read off <c>forward</c>.
        /// </summary>
        static bool ReadWalls(Transform root, out WallSet walls)
        {
            walls = new WallSet();

            var alongX = new List<Transform>();
            var alongZ = new List<Transform>();

            foreach (Transform wall in root)
            {
                if (!wall.gameObject.activeSelf) continue;

                if (RunsAlongX(wall)) alongX.Add(wall);
                else alongZ.Add(wall);
            }

            if (alongX.Count < 2 || alongZ.Count < 2) return false;

            alongX.Sort((a, b) => a.position.z.CompareTo(b.position.z));
            alongZ.Sort((a, b) => a.position.x.CompareTo(b.position.x));

            walls.North = alongX[alongX.Count - 1];
            walls.West = alongZ[0];
            walls.East = alongZ[alongZ.Count - 1];

            for (int i = 0; i < alongX.Count - 1; i++) walls.South.Add(alongX[i]);
            walls.South.Sort((a, b) => a.position.x.CompareTo(b.position.x));

            return true;
        }

        static bool RunsAlongX(Transform wall) => Mathf.Abs(wall.forward.x) > Mathf.Abs(wall.forward.z);

        /// <summary>Where a run starts and ends along the axis it travels.</summary>
        static void Extent(Transform wall, out float from, out float to)
        {
            float centre = RunsAlongX(wall) ? wall.position.x : wall.position.z;
            float half = wall.localScale.z * 0.5f;
            from = centre - half;
            to = centre + half;
        }

        /// <summary>
        /// Puts a wall on a line and stretches it between two points, keeping its height,
        /// thickness and turn. Returns 1 when it moved, for the report.
        /// </summary>
        static int Lay(Transform wall, float line, float from, float to)
        {
            bool alongX = RunsAlongX(wall);
            float centre = (from + to) * 0.5f;
            float length = Mathf.Max(0.1f, to - from);

            Vector3 position = alongX
                ? new Vector3(centre, wall.position.y, line)
                : new Vector3(line, wall.position.y, centre);

            if ((position - wall.position).sqrMagnitude < 1e-6f && Mathf.Abs(wall.localScale.z - length) < 1e-3f)
                return 0;

            Undo.RecordObject(wall, "Lay wall");
            wall.position = position;

            Vector3 scale = wall.localScale;
            wall.localScale = new Vector3(scale.x, scale.y, length);
            return 1;
        }

        // --- The compound boundary ---------------------------------------------------------------

        /// <summary>
        /// Moves the compound's walls from <paramref name="from"/> to <paramref name="to"/>.
        ///
        /// The west and east walls are laid whole. The north and south faces are split - by the
        /// gate, and by the courtyard's opening - so each piece only has the end that stood at
        /// the old corner moved to the new one; an end at a gate or at the courtyard stays put.
        /// The courtyard's own stretch of the north wall is treated the same way, which is what
        /// carries the north wall out to the new east corner.
        /// </summary>
        static int MoveBoundary(WallSet walls, Rect from, Rect to)
        {
            int moved = 0;

            moved += Lay(walls.West, to.xMin, to.yMin, to.yMax);
            moved += Lay(walls.East, to.xMax, to.yMin, to.yMax);

            foreach (Transform piece in walls.South) moved += FollowCorners(piece, to.yMin, from, to);
            moved += FollowCorners(walls.North, to.yMax, from, to);

            foreach (Transform piece in ExpansionWallPieces())
            {
                if (!RunsAlongX(piece) || Mathf.Abs(piece.position.z - from.yMax) > 0.6f) continue;
                moved += FollowCorners(piece, to.yMax, from, to);
            }

            return moved;
        }

        static int FollowCorners(Transform piece, float line, Rect from, Rect to)
        {
            Extent(piece, out float start, out float end);

            if (Mathf.Abs(start - from.xMin) < 0.6f) start = to.xMin;
            if (Mathf.Abs(end - from.xMax) < 0.6f) end = to.xMax;

            return Lay(piece, line, start, end);
        }

        /// <summary>The wall pieces an expansion shows across its opening until it is claimed.</summary>
        static IEnumerable<Transform> ExpansionWallPieces()
        {
            foreach (ExpansionArea area in Object.FindObjectsByType<ExpansionArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SerializedProperty locked = new SerializedObject(area).FindProperty("lockedVisuals");

                for (int i = 0; locked != null && i < locked.arraySize; i++)
                {
                    var piece = locked.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                    if (piece != null && piece.name.Contains("Wall")) yield return piece.transform;
                }
            }
        }

        // --- Clearing the new ground -------------------------------------------------------------

        /// <summary>
        /// Deletes the town's dressing that would stand inside the new walls.
        ///
        /// Dressing already inside the old walls is left alone - that is the level's own, and it
        /// moves out with its wall instead. Anything the courtyard shows until it is claimed is
        /// never touched, wherever it stands.
        /// </summary>
        static int ClearDressing(Rect target, Rect current)
        {
            GameObject environment = GameObject.Find("ApocalypseEnvironment");
            if (environment == null) return 0;

            HashSet<Transform> keep = CourtyardArt();
            var doomed = new List<GameObject>();

            Rect inside = Shrink(target, DressingIntrusion);
            foreach (string group in DressingGroups)
            {
                Transform host = environment.transform.Find(group);
                if (host == null) continue;

                foreach (Transform art in host)
                {
                    if (Keeps(art, keep) || !TryMeasure(art, out Bounds bounds)) continue;
                    if (current.Contains(new Vector2(bounds.center.x, bounds.center.z))) continue;

                    if (Reaches(art, inside)) doomed.Add(art.gameObject);
                }
            }

            Rect ground = Shrink(target, GroundIntrusion);
            foreach (string group in GroundGroups)
            {
                Transform host = environment.transform.Find($"Wasteland/{group}");
                if (host == null) continue;

                foreach (Transform art in host)
                {
                    if (!Keeps(art, keep) && Reaches(art, ground)) doomed.Add(art.gameObject);
                }
            }

            foreach (GameObject go in doomed)
            {
                // A piece inside a prefab instance cannot be deleted from the scene, only hidden.
                if (PrefabUtility.IsPartOfPrefabInstance(go) && PrefabUtility.GetOutermostPrefabInstanceRoot(go) != go)
                {
                    Undo.RecordObject(go, "Clear dressing");
                    go.SetActive(false);
                }
                else
                {
                    Undo.DestroyObjectImmediate(go);
                }
            }

            return doomed.Count;
        }

        /// <summary>Everything an expansion shows until it is claimed, and so must keep.</summary>
        static HashSet<Transform> CourtyardArt()
        {
            var art = new HashSet<Transform>();

            foreach (ExpansionArea area in Object.FindObjectsByType<ExpansionArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SerializedProperty locked = new SerializedObject(area).FindProperty("lockedVisuals");

                for (int i = 0; locked != null && i < locked.arraySize; i++)
                {
                    if (locked.GetArrayElementAtIndex(i).objectReferenceValue is GameObject piece) art.Add(piece.transform);
                }
            }

            return art;
        }

        /// <summary>True when <paramref name="candidate"/> is, or holds, something that must be kept.</summary>
        static bool Keeps(Transform candidate, HashSet<Transform> keep)
        {
            foreach (Transform kept in keep)
            {
                if (kept != null && (kept == candidate || kept.IsChildOf(candidate))) return true;
            }

            return false;
        }

        static Rect Shrink(Rect rect, float by) =>
            Rect.MinMaxRect(rect.xMin + by, rect.yMin + by, rect.xMax - by, rect.yMax - by);

        /// <summary>True when any part of <paramref name="art"/> stands over <paramref name="area"/>.</summary>
        static bool Reaches(Transform art, Rect area)
        {
            foreach (Renderer renderer in art.GetComponentsInChildren<Renderer>(true))
            {
                if (Overlaps(renderer, area)) return true;
            }

            return false;
        }

        static readonly Vector2[] Corners = new Vector2[8];

        /// <summary>
        /// Whether a renderer's box, seen from above, overlaps a rectangle on the ground.
        ///
        /// Tested on the box itself rather than its world bounds. The wasteland's patches are
        /// long thin slabs laid at angles, and their bounds are squares several times their
        /// size: a bounds test clears patches that lie wholly outside the wall.
        /// </summary>
        static bool Overlaps(Renderer renderer, Rect area)
        {
            // The world bounds are the box's extent along X and Z: the rectangle's own two axes.
            Bounds world = renderer.bounds;
            if (world.max.x < area.xMin || world.min.x > area.xMax || world.max.z < area.yMin || world.min.z > area.yMax)
                return false;

            Bounds local = renderer.localBounds;
            Matrix4x4 toWorld = renderer.localToWorldMatrix;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));

                Vector3 point = toWorld.MultiplyPoint3x4(corner);
                Corners[i] = new Vector2(point.x, point.z);
            }

            // Then the box's own axes, as they fall on the ground. Any one that separates the two
            // shapes proves they do not meet.
            for (int column = 0; column < 3; column++)
            {
                Vector4 axis = toWorld.GetColumn(column);
                if (Separates(new Vector2(axis.x, axis.z), area)) return false;
            }

            return true;
        }

        static bool Separates(Vector2 axis, Rect area)
        {
            if (axis.sqrMagnitude < 1e-6f) return false;
            axis.Normalize();

            float boxMin = float.PositiveInfinity, boxMax = float.NegativeInfinity;
            foreach (Vector2 corner in Corners)
            {
                float d = Vector2.Dot(corner, axis);
                boxMin = Mathf.Min(boxMin, d);
                boxMax = Mathf.Max(boxMax, d);
            }

            float rectMin = float.PositiveInfinity, rectMax = float.NegativeInfinity;
            foreach (Vector2 corner in new[] { area.min, area.max, new Vector2(area.xMin, area.yMax), new Vector2(area.xMax, area.yMin) })
            {
                float d = Vector2.Dot(corner, axis);
                rectMin = Mathf.Min(rectMin, d);
                rectMax = Mathf.Max(rectMax, d);
            }

            return boxMax < rectMin || boxMin > rectMax;
        }

        /// <summary>
        /// The pale strips the visitor setup laid over the ground the compound first gained.
        /// With the grass showing through they are a second, lighter green on top of it.
        /// </summary>
        static int RemoveYardStrips(Scene scene)
        {
            Transform yard = FindInScene(scene, "VisitorSystem")?.Find("Yard");
            if (yard == null) return 0;

            var strips = new List<GameObject>();
            foreach (Transform strip in yard)
            {
                if (strip.name.StartsWith("Yard_")) strips.Add(strip.gameObject);
            }

            foreach (GameObject strip in strips) Undo.DestroyObjectImmediate(strip);
            return strips.Count;
        }

        // --- Moving out with the walls -----------------------------------------------------------

        /// <summary>
        /// Moves what stands against the old west and east walls out by as far as those walls
        /// moved: the facilities sited along them, the stores and the garden, and the junk propped
        /// against the inside of the brickwork.
        /// </summary>
        static int ShiftWallside(Scene scene, Rect current, float westShift, float eastShift)
        {
            bool west = Mathf.Abs(westShift) > 0.01f;
            bool east = Mathf.Abs(eastShift) > 0.01f;
            if (!west && !east) return 0;

            int moved = 0;

            foreach (Transform unit in WallsideUnits(scene, current))
            {
                Vector3 p = unit.position;
                if (p.z < current.yMin - 0.5f || p.z > current.yMax + 0.5f) continue;

                float shift = 0f;
                if (west && p.x >= current.xMin - 0.5f && p.x <= current.xMin + WallsideDepth) shift = westShift;
                else if (east && p.x <= current.xMax + 0.5f && p.x >= current.xMax - WallsideDepth) shift = eastShift;

                if (shift == 0f) continue;

                Undo.RecordObject(unit, "Move with the wall");
                unit.position = p + new Vector3(shift, 0f, 0f);
                moved++;
            }

            return moved;
        }

        static List<Transform> WallsideUnits(Scene scene, Rect current)
        {
            var units = new List<Transform>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (System.Array.IndexOf(WallsideRoots, root.name) < 0) continue;
                foreach (Transform child in root.transform) units.Add(child);
            }

            // The stores and the garden beds are placed objects under the restaurant's zone,
            // standing out in the yard.
            Zone zone = Object.FindFirstObjectByType<Zone>();
            if (zone != null)
            {
                foreach (Transform child in zone.transform)
                {
                    if (child.GetComponent<PlacedObject_Done>() != null) units.Add(child);
                }
            }

            // Junk propped against the inside of the walls: the level's own dressing, inside them.
            GameObject environment = GameObject.Find("ApocalypseEnvironment");
            if (environment != null)
            {
                foreach (string group in DressingGroups)
                {
                    Transform host = environment.transform.Find(group);
                    if (host == null) continue;

                    foreach (Transform art in host)
                    {
                        if (TryMeasure(art, out Bounds bounds) && current.Contains(new Vector2(bounds.center.x, bounds.center.z)))
                            units.Add(art);
                    }
                }
            }

            return units;
        }

        /// <summary>
        /// Moves the town's zombie patches beside the west and east walls out with them, so none
        /// ends up inside. Patches across the road to the south are left where they are.
        /// </summary>
        static int ShiftTownPatches(Rect current, float westShift, float eastShift)
        {
            TownZombies town = Object.FindFirstObjectByType<TownZombies>();
            if (town == null) return 0;

            var so = new SerializedObject(town);
            SerializedProperty areas = so.FindProperty("areas");

            int moved = 0;
            for (int i = 0; areas != null && i < areas.arraySize; i++)
            {
                SerializedProperty centre = areas.GetArrayElementAtIndex(i).FindPropertyRelative("centre");
                Vector2 size = areas.GetArrayElementAtIndex(i).FindPropertyRelative("size").vector2Value;
                Vector3 c = centre.vector3Value;

                bool beside = c.z + size.y * 0.5f >= current.yMin && c.z - size.y * 0.5f <= current.yMax;
                if (!beside) continue;

                float shift = c.x < current.xMin ? westShift : c.x > current.xMax ? eastShift : 0f;
                if (Mathf.Abs(shift) < 0.01f) continue;

                centre.vector3Value = c + new Vector3(shift, 0f, 0f);
                moved++;
            }

            so.ApplyModifiedProperties();
            return moved;
        }

        // --- The grid ----------------------------------------------------------------------------

        /// <summary>
        /// Widens the build grid to cover the compound from wall to wall. Navigation mirrors the
        /// build grid, so this is also what lets anyone walk on the new ground.
        ///
        /// Only X, and the far Z edge, are widened. The south edge stays at z = 0 on purpose: the
        /// gate and the road are off the grid by design, and the night's enemies walk in from off
        /// it. The origin moves by whole cells, so everything already placed snaps exactly where
        /// it did.
        /// </summary>
        static string EnsureGrid(Rect compound)
        {
            GridBuildingSystem3D build = Object.FindFirstObjectByType<GridBuildingSystem3D>();
            if (build == null) return "WARNING: no GridBuildingSystem3D in the scene, so the grid was not widened.";

            var so = new SerializedObject(build);
            SerializedProperty property = so.FindProperty("settings");
            var settings = property.objectReferenceValue as GridSettings;

            if (settings == null)
            {
                settings = AssetDatabase.LoadAssetAtPath<GridSettings>(GridSettingsPath);

                if (settings == null)
                {
                    // Created with the grid the scene was already getting without one: 50 x 50 at
                    // the world origin. Widened below.
                    ContentGenerator.EnsureFolder(System.IO.Path.GetDirectoryName(GridSettingsPath).Replace('\\', '/'));
                    settings = ScriptableObject.CreateInstance<GridSettings>();
                    AssetDatabase.CreateAsset(settings, GridSettingsPath);
                }

                property.objectReferenceValue = settings;
                so.ApplyModifiedProperties();
            }

            float cell = settings.cellSize > 0f ? settings.cellSize : 1f;
            Vector3 origin = settings.origin;

            float minX = Mathf.Min(origin.x, compound.xMin - GridMargin);
            float maxX = Mathf.Max(origin.x + settings.width * cell, compound.xMax + GridMargin);
            float maxZ = Mathf.Max(origin.z + settings.height * cell, compound.yMax + GridMargin);

            // Whole cells west of the old origin, so the cell boundaries do not move.
            float originX = origin.x - Mathf.Ceil((origin.x - minX) / cell - 1e-4f) * cell;
            int width = Mathf.CeilToInt((maxX - originX) / cell - 1e-4f);
            int height = Mathf.CeilToInt((maxZ - origin.z) / cell - 1e-4f);

            if (Mathf.Abs(originX - origin.x) > 1e-4f || width != settings.width || height != settings.height)
            {
                Undo.RecordObject(settings, "Widen grid");
                settings.origin = new Vector3(originX, origin.y, origin.z);
                settings.width = width;
                settings.height = height;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssetIfDirty(settings);
            }

            return $"Grid: {settings.width} x {settings.height} cells of {cell:0.##} m from ({settings.origin.x:0}, {settings.origin.z:0}), " +
                   $"covering X {settings.origin.x:0} to {settings.origin.x + settings.width * cell:0}.";
        }

        // --- The restaurant ----------------------------------------------------------------------

        /// <summary>
        /// The restaurant's outer walls as a box, 3 m tall - what the night shelter keeps routes
        /// outside of. False when the scene has no restaurant walls to measure.
        /// </summary>
        public static bool TryMeasureRestaurant(out Bounds building)
        {
            building = default;

            Transform walls = FindInScene(SceneManager.GetActiveScene(), RestaurantWallsName);
            if (walls == null || !ReadWalls(walls, out WallSet set)) return false;

            building = ToBounds(Outer(set));
            return true;
        }

        static Rect Outer(WallSet set)
        {
            float half = set.West.localScale.x * 0.5f;
            Rect lines = set.Lines;
            return Rect.MinMaxRect(lines.xMin - half, lines.yMin - half, lines.xMax + half, lines.yMax + half);
        }

        static Bounds ToBounds(Rect outer) =>
            new Bounds(new Vector3(outer.center.x, 1.5f, outer.center.y), new Vector3(outer.width, 3f, outer.height));

        static string ExpandRestaurant(Scene scene)
        {
            Transform wallsRoot = FindInScene(scene, RestaurantWallsName);
            if (wallsRoot == null || !ReadWalls(wallsRoot, out WallSet walls) || walls.South.Count < 2)
                return "WARNING: no restaurant walls with a door in the south face, so the restaurant was left as it is.";

            float thickness = walls.West.localScale.x;
            float half = thickness * 0.5f;

            Rect outer = Outer(walls);
            Rect inner = Shrink(outer, thickness);

            Extent(walls.South[0], out _, out float doorWest);
            Extent(walls.South[walls.South.Count - 1], out float doorEast, out _);
            float doorX = (doorWest + doorEast) * 0.5f;

            Rect targetOuter = Rect.MinMaxRect(RestaurantTarget.xMin, outer.yMin, RestaurantTarget.xMax, RestaurantTarget.yMax);
            Rect targetInner = Shrink(targetOuter, thickness);

            if (doorWest < targetOuter.xMin + 0.5f || doorEast > targetOuter.xMax - 0.5f)
                return "WARNING: the restaurant's door is outside the widened south face, so the restaurant was left as it is.";

            float westGrowth = targetInner.xMin - inner.xMin;
            float eastGrowth = targetInner.xMax - inner.xMax;
            float northGrowth = targetInner.yMax - inner.yMax;

            string done;

            if (Mathf.Abs(westGrowth) < 0.01f && Mathf.Abs(eastGrowth) < 0.01f && Mathf.Abs(northGrowth) < 0.01f)
            {
                done = "Restaurant already at its widened size";
            }
            else
            {
                int furniture = StretchRoom(wallsRoot, inner, doorX, westGrowth, eastGrowth, northGrowth);
                int workers = MoveWorkersBeside(outer, targetOuter);

                // Side walls between the inner faces, the back wall across the full width, and
                // the south face out to the corners from either side of the door - as the level
                // built them.
                Lay(walls.West, targetOuter.xMin + half, targetInner.yMin, targetInner.yMax);
                Lay(walls.East, targetOuter.xMax - half, targetInner.yMin, targetInner.yMax);
                Lay(walls.North, targetOuter.yMax - half, targetOuter.xMin, targetOuter.xMax);

                float southLine = walls.South[0].position.z;
                Lay(walls.South[0], southLine, targetOuter.xMin, doorWest);
                Lay(walls.South[walls.South.Count - 1], southLine, doorEast, targetOuter.xMax);

                done = $"Restaurant widened from {outer.width:0.0} x {outer.height:0.0} m to " +
                       $"{targetOuter.width:0.0} x {targetOuter.height:0.0} m; {furniture} pieces rearranged, " +
                       $"{workers} workers moved out with the walls";
            }

            NightShelter shelter = Object.FindFirstObjectByType<NightShelter>();
            if (shelter != null)
            {
                var so = new SerializedObject(shelter);
                so.FindProperty("footprint").boundsValue = ToBounds(targetOuter);
                so.ApplyModifiedProperties();
            }

            return $"{done}. Floor inside: X {targetInner.xMin:0.0} to {targetInner.xMax:0.0}, " +
                   $"Z {targetInner.yMin:0.0} to {targetInner.yMax:0.0}; door at X {doorX:0.0}.";
        }

        /// <summary>
        /// Moves what is inside the restaurant as the room stretches round it.
        ///
        /// The kitchen line along the back wall keeps its distance from the back wall and from the
        /// nearer side wall, so the stove stays in its corner and the fridge at its end of the
        /// line, and the stretch opens up the middle of the back wall for new stations. The counter
        /// moves back a little, widening the dining room without crowding the kitchen. The tables
        /// spread toward the side walls, leaving the walk in from the door clear.
        /// </summary>
        static int StretchRoom(Transform wallsRoot, Rect inner, float doorX, float westGrowth, float eastGrowth,
                               float northGrowth)
        {
            float kitchenLine = inner.yMax - KitchenDepth;
            float middle = inner.center.y;

            int moved = 0;

            foreach (Transform unit in RoomContents(wallsRoot))
            {
                Vector3 p = unit.position;
                if (!inner.Contains(new Vector2(p.x, p.z))) continue;

                // The door's own pieces stand in the doorway, not in the room.
                if (Mathf.Abs(p.x - doorX) < 1.5f && p.z < inner.yMin + 1.5f) continue;

                bool west = p.x < doorX;
                float sideGrowth = west ? westGrowth : eastGrowth;

                Vector3 shift;
                if (p.z >= kitchenLine) shift = new Vector3(sideGrowth, 0f, northGrowth);
                else if (p.z >= middle) shift = new Vector3(0f, 0f, Mathf.Round(northGrowth * CounterShare));
                else shift = new Vector3(Mathf.Round(sideGrowth * TableShare), 0f, 0f);

                if (shift.sqrMagnitude < 1e-6f) continue;

                Undo.RecordObject(unit, "Rearrange restaurant");
                unit.position = p + shift;
                moved++;
            }

            return moved;
        }

        /// <summary>
        /// What the room holds: the restaurant root's own pieces - the counter, the fridge, the
        /// sink - and the placed objects under its zone - the stove, the tables. Only what is
        /// switched on; a spare door left off in the hierarchy stays where it is.
        /// </summary>
        static List<Transform> RoomContents(Transform wallsRoot)
        {
            var contents = new List<Transform>();

            Transform zone = wallsRoot.parent;
            Transform restaurant = wallsRoot.root;

            foreach (Transform child in restaurant)
            {
                if (child == zone || wallsRoot.IsChildOf(child) || !child.gameObject.activeInHierarchy) continue;
                contents.Add(child);
            }

            if (zone != null && zone != restaurant)
            {
                foreach (Transform child in zone)
                {
                    if (child.gameObject.activeInHierarchy && child.GetComponent<PlacedObject_Done>() != null)
                        contents.Add(child);
                }
            }

            return contents;
        }

        /// <summary>
        /// Workers standing just outside a side wall of the restaurant move out with it, keeping
        /// their distance from it, rather than ending up inside the widened room.
        /// </summary>
        static int MoveWorkersBeside(Rect outer, Rect targetOuter)
        {
            int moved = 0;

            foreach (WorkerAgent worker in Object.FindObjectsByType<WorkerAgent>(FindObjectsSortMode.None))
            {
                Vector3 p = worker.transform.position;
                if (p.z < outer.yMin || p.z > outer.yMax) continue;

                float shift = 0f;
                if (p.x >= outer.xMax && p.x <= outer.xMax + BesideWallDepth) shift = targetOuter.xMax - outer.xMax;
                else if (p.x <= outer.xMin && p.x >= outer.xMin - BesideWallDepth) shift = targetOuter.xMin - outer.xMin;

                if (Mathf.Abs(shift) < 0.01f) continue;

                Undo.RecordObject(worker.transform, "Move worker");
                worker.transform.position = p + new Vector3(shift, 0f, 0f);
                moved++;
            }

            return moved;
        }

        // --- Plumbing ----------------------------------------------------------------------------

        static bool TryMeasure(Transform target, out Bounds bounds)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        static Transform FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            }

            return null;
        }
    }
}
