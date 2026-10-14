using System;
using System.Collections.Generic;
using System.Text;
using LastOrders.Restaurant;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using B = LastOrders.EditorTools.BuildingKit;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The diner's shell in wood and cream: a floor of warm varnished planks with big cream tiles
    /// along the kitchen wall and a dark board across the threshold between them; low cream walls,
    /// rounded along the top, on a brown skirting; and a pair of red swing doors with portholes in a
    /// chunky cream arch.
    ///
    /// The walls keep their transforms, colliders and bounds - the expansion stretches them, the nav
    /// grid and the night read them - only their mesh and material change. Each gets a mesh made for
    /// its own thickness and for how deep it is sunk into the ground (the walls on the camera's side
    /// stand half in it), so the skirting sits on the floor on every wall. The profile only changes
    /// across the wall and up it, never along it, so the walls stretch as the building grows without
    /// anything stretching with them.
    ///
    /// The doors are a new frame and two leaves put into pfDoor beside the old door, which is switched
    /// off. The doorway is widened for them - the two front pieces end further from its middle - and
    /// a leaf hangs on each jamb. <see cref="SwingDoors"/> swings them, either way, for whoever walks
    /// through; the old DoorBehaviour is switched off. (The single leaf this used to fit is kept,
    /// switched off.)
    ///
    /// Everything is written as new assets; the checkerboard, the banded wall texture and the old
    /// door stay where they were, and 'Restore Previous Diner Look' puts them back.
    /// </summary>
    public static class DinerShellModel
    {
        const string WallFolder = B.Folder + "/DinerWalls";
        const string DoubleFrameMeshPath = B.Folder + "/DinerDoor_DoubleFrame.asset";
        const string SwingLeafMeshPath = B.Folder + "/DinerDoor_SwingLeaf.asset";
        const string PaintedMaterialPath = B.Folder + "/Building_" + EnvKit.Style + ".mat";

        const string MaterialFolder = "Assets/Game Assets/Content/LastOrders/Materials/" + EnvKit.Style;
        public const string PlanksMaterialPath = MaterialFolder + "/RestaurantFloor_Planks.mat";
        public const string CreamMaterialPath = MaterialFolder + "/RestaurantFloor_KitchenCream.mat";
        public const string ThresholdMaterialPath = MaterialFolder + "/RestaurantFloor_Threshold.mat";

        const string WallsName = "RestaurantWalls";
        const string DoorName = "DinerDoor";
        const string OldDoorName = "Door2";

        /// <summary>The old door's swinging part, under <see cref="OldDoorName"/>: what DoorBehaviour turned before.</summary>
        const string OldLeafName = "GameObject (1)";

        // --- The palette -----------------------------------------------------------------------------

        /// <summary>Warm cream, painted a little darker than it shows: a face toward the sun comes out nearly half again as bright.</summary>
        static readonly Color Cream = B.Hex("BFA68D");

        static readonly Color CreamCap = B.Hex("C7AF95");

        /// <summary>The door's frame: a shade under the walls' cap, as its top faces the sun square on and would clip.</summary>
        static readonly Color FrameCream = B.Hex("BDA68E");
        static readonly Color Skirting = B.Hex("74503C");
        static readonly Color DoorRed = B.Hex("7A443B");
        static readonly Color Chrome = B.Hex("ADABA4");

        /// <summary>The kick plates: brushed steel, a shade under the chrome so the two read apart.</summary>
        static readonly Color Steel = B.Hex("8F8E89");
        static readonly Color Glass = B.Hex("7FAAB6");
        static readonly Color GlassGlint = B.Hex("CFE3E6");
        static readonly Color Sill = B.Hex("5A3F2E");

        /// <summary>The threshold board: the planks, a shade darker.</summary>
        static readonly Color ThresholdTint = new Color(0.72f, 0.64f, 0.6f);

        const float PlankSmoothness = 0.32f;
        const float CreamSmoothness = 0.28f;
        /// <summary>Half a board: with the kitchen strip 3 m deep, the dining room's first board starts on a joint.</summary>
        const float ThresholdWidth = 0.12f;

        // --- The walls' measurements (metres) ---------------------------------------------------------

        /// <summary>How high the skirting reaches above the ground: just under a decimetre above the floor.</summary>
        const float SkirtingHeight = 0.13f;

        /// <summary>How far the skirting stands proud of the wall above it, and the bevel up onto the wall.</summary>
        const float SkirtingProud = 0.012f;

        const float TopRadius = 0.06f;
        const int TopSteps = 7;

        // --- The door's measurements (metres; the door's own space: X across the doorway, Y up, Z through the wall, inward) ---

        const float FrameBand = 0.18f;

        /// <summary>How far the jambs reach into the doorway past the walls' ends, so the ends are buried in them rather than flush with them.</summary>
        const float JambOverlap = 0.015f;
        const float FrameProud = 0.04f;
        const float OpeningHeight = 2f;

        /// <summary>The arch's corners, at the middle of the frame's band: 0.21 inside, 0.39 outside.</summary>
        const float ArchRadius = 0.3f;

        const float LeafClearance = 0.012f;
        const float LeafThickness = 0.056f;
        const float LeafFoot = 0.045f;
        const float LeafEdge = 0.014f;

        /// <summary>Steps round the leaf's outline, a multiple of six (see <see cref="RoundTopRect"/>): the faces and the edge share them, so they meet without a crack.</summary>
        const int LeafSteps = 96;

        /// <summary>The hinge's axis, in from the jamb's face, so the open leaf clears it.</summary>
        const float HingeInset = 0.03f;

        /// <summary>The doorway's width for the pair of doors: wide enough that each leaf is a door, not a shutter.</summary>
        const float DoubleDoorway = 1.7f;

        /// <summary>The gap between the two leaves where they meet in the middle.</summary>
        const float MeetGap = 0.012f;

        /// <summary>The meeting edge's top corner: all but square.</summary>
        const float MeetCorner = 0.04f;

        const float PortholeHeight = 1.42f;
        const float PortholeRadius = 0.125f;
        const float BarHeight = 0.98f;

        // --- Menus ------------------------------------------------------------------------------------

        [MenuItem("Last Orders/Restaurant/Apply Wood & Cream Diner (Floor, Walls, Door)", priority = 76)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This dresses the restaurant in the open scene:\n\n" +
                "- Floor: wooden planks in the dining room, cream tiles along the kitchen wall, a dark threshold board\n" +
                "- Walls: cream, rounded along the top, on a brown skirting\n" +
                "- Door: a pair of red swing doors with portholes in a cream arch, the doorway widened for them (the old door is switched off)\n\n" +
                "Nothing is deleted - 'Restore Previous Diner Look' puts the old look back. The scene is left unsaved.",
                "Apply", "Cancel");

            if (proceed) Debug.Log(Apply(save: false));
        }

        [MenuItem("Last Orders/Restaurant/Restore Previous Diner Look (Checker Floor, Banded Walls, Old Door)", priority = 77)]
        public static void RunRestore()
        {
            if (EditorUtility.DisplayDialog("Last Orders", "Put the checkerboard floor, the banded walls and the old door back? " +
                                            "The scene is left unsaved.", "Restore", "Cancel"))
                Debug.Log(Restore(save: false));
        }

        [MenuItem("Last Orders/Restaurant/Fit Diner Double Doors (Current Scene)", priority = 78)]
        public static void RunDoors()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This fits a pair of red swing doors in the restaurant's doorway, widening it to " +
                $"{DoubleDoorway:0.#} m. They open either way for visitors, workers and waiters, and rattle when " +
                "zombies hit the door.\n\nThe old door and the single leaf are switched off, not deleted. " +
                "The scene is left unsaved.",
                "Fit Doors", "Cancel");

            if (proceed) Debug.Log(FitDoors(save: false));
        }

        /// <summary>Only the doors, on the open scene, with no dialogs.</summary>
        public static string FitDoors(bool save)
        {
            Scene scene = SceneManager.GetActiveScene();
            var report = new StringBuilder("[Last Orders] Diner double doors.\n");

            Transform walls = Find(scene, WallsName);
            if (walls == null) return $"[Last Orders] No '{WallsName}' in '{scene.name}'; nothing was changed.";

            if (!RestaurantSceneSetup.ReadWalls(walls, out _, out _, out _, out Transform frontLeft, out Transform frontRight, out string problem))
                return $"[Last Orders] {problem} Nothing was changed.";

            PlaceDoor(frontLeft, frontRight, report);

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);
            return report.ToString();
        }

        /// <summary>The whole look on the open scene, with no dialogs.</summary>
        public static string Apply(bool save)
        {
            Scene scene = SceneManager.GetActiveScene();
            var report = new StringBuilder("[Last Orders] Wood & cream diner applied.\n");

            Transform walls = Find(scene, WallsName);
            if (walls == null) return $"[Last Orders] No '{WallsName}' in '{scene.name}'; nothing was changed.";

            if (!RestaurantSceneSetup.ReadWalls(walls, out Transform back, out Transform left, out Transform right,
                                                out Transform frontLeft, out Transform frontRight, out string problem))
                return $"[Last Orders] {problem} Nothing was changed.";

            DressWalls(report, back, left, right, frontLeft, frontRight);

            RestaurantFloor floor = walls.root.GetComponentInChildren<RestaurantFloor>(true);
            if (floor != null)
            {
                Undo.RecordObject(floor, "Diner floor");
                Undo.RecordObject(floor.GetComponent<MeshRenderer>(), "Diner floor");
                DressFloor(floor);
                EditorUtility.SetDirty(floor);
                RefitFloor(walls);
                report.Append("  Floor: planks in the dining room, cream tiles along the kitchen wall, a threshold board between.\n");
            }
            else
            {
                report.Append("  WARNING: no RestaurantFloor - run 'Set Up Repairs & Expansion' first.\n");
            }

            PlaceDoor(frontLeft, frontRight, report);

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);
            return report.ToString();
        }

        /// <summary>The checkerboard, the banded walls and the old door, back on the open scene.</summary>
        public static string Restore(bool save)
        {
            Scene scene = SceneManager.GetActiveScene();
            var report = new StringBuilder("[Last Orders] Previous diner look restored.\n");

            Transform walls = Find(scene, WallsName);
            if (walls == null || !RestaurantSceneSetup.ReadWalls(walls, out Transform back, out Transform left, out Transform right,
                                                                 out Transform frontLeft, out Transform frontRight, out _))
                return "[Last Orders] No restaurant walls in the open scene; nothing was changed.";

            RestaurantSceneSetup.DressWallsPrevious(back, left, right, frontLeft, frontRight);
            report.Append("  Walls: the banded diner texture.\n");

            RestaurantFloor floor = walls.root.GetComponentInChildren<RestaurantFloor>(true);
            if (floor != null)
            {
                Undo.RecordObject(floor, "Diner floor");
                Undo.RecordObject(floor.GetComponent<MeshRenderer>(), "Diner floor");
                RestaurantSceneSetup.DressFloorPrevious(floor);
                EditorUtility.SetDirty(floor);
                RefitFloor(walls);
                report.Append("  Floor: the checkerboard and the kitchen tiles.\n");
            }

            DoorBehaviour door = FindDoor(frontLeft, frontRight);
            if (door != null)
            {
                var swing = door.GetComponent<SwingDoors>();
                if (swing != null)
                {
                    if (swing.PreviousDoorway > 0.05f)
                    {
                        SetDoorway(frontLeft, frontRight, swing.PreviousDoorway);
                        report.Append($"  Doorway: back to {swing.PreviousDoorway:0.##} m.\n");
                    }

                    Undo.RecordObject(swing, "Diner door");
                    swing.enabled = false;
                }

                Undo.RecordObject(door, "Diner door");
                door.enabled = true;

                Transform visual = VisualRoot(door);
                Transform ours = visual.Find(DoorName);
                Transform old = visual.Find(OldDoorName);

                if (ours != null)
                {
                    Undo.RecordObject(ours.gameObject, "Diner door");
                    ours.gameObject.SetActive(false);
                }

                if (old != null)
                {
                    Undo.RecordObject(old.gameObject, "Diner door");
                    old.gameObject.SetActive(true);

                    Transform leaf = old.Find(OldLeafName);
                    var so = new SerializedObject(door);
                    so.FindProperty("door").objectReferenceValue = leaf != null ? leaf : old;
                    so.ApplyModifiedProperties();
                    report.Append($"  Door: '{OldDoorName}' back on, and swinging again.\n");
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);
            return report.ToString();
        }

        /// <summary>Paints the floor's textures again if their version changed and sets their materials up afresh.</summary>
        public static void RestyleFloor()
        {
            PlanksMaterial();
            CreamMaterial();
            ThresholdMaterial();
        }

        static void RefitFloor(Transform walls)
        {
            RestaurantShell shell = walls.GetComponent<RestaurantShell>();
            if (shell != null) shell.FitFloor();
        }

        // --- The floor --------------------------------------------------------------------------------

        /// <summary>Puts the planks, the cream tiles and the threshold on the floor. Lay it again afterwards (<see cref="RestaurantShell.FitFloor"/>).</summary>
        public static void DressFloor(RestaurantFloor floor)
        {
            floor.Configure(PlanksMaterial(), CreamMaterial(), TextureGenerator.PlankTile, TextureGenerator.CreamTile,
                            RestaurantSceneSetup.KitchenDepth);
            floor.ConfigureTrim(ThresholdMaterial(), ThresholdWidth);
        }

        static Material PlanksMaterial() =>
            RestaurantSceneSetup.FloorMaterial(PlanksMaterialPath, TextureGenerator.EnsureDinerPlanks(), PlankSmoothness);

        static Material CreamMaterial() =>
            RestaurantSceneSetup.FloorMaterial(CreamMaterialPath, TextureGenerator.EnsureKitchenCream(), CreamSmoothness);

        static Material ThresholdMaterial()
        {
            Material material = RestaurantSceneSetup.FloorMaterial(ThresholdMaterialPath, TextureGenerator.EnsureDinerPlanks(),
                                                                   PlankSmoothness);
            material.SetColor("_BaseColor", ThresholdTint);
            EditorUtility.SetDirty(material);
            return material;
        }

        // --- The walls --------------------------------------------------------------------------------

        /// <summary>Gives each wall the cream mesh made for its shape, and the buildings' painted material.</summary>
        public static void DressWalls(StringBuilder report, params Transform[] walls)
        {
            Material paint = AssetDatabase.LoadAssetAtPath<Material>(PaintedMaterialPath);
            if (paint == null) paint = B.PaintedMaterial();

            var names = new List<string>();
            foreach (Transform wall in walls)
            {
                if (wall == null) continue;

                var filter = wall.GetComponent<MeshFilter>();
                var renderer = wall.GetComponent<MeshRenderer>();
                if (filter == null || renderer == null) continue;

                Mesh mesh = WallMesh(wall);
                Undo.RecordObject(filter, "Diner walls");
                Undo.RecordObject(renderer, "Diner walls");
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = paint;
                names.Add($"{wall.name} ({mesh.name})");
            }

            report?.Append($"  Walls: cream on a brown skirting, rounded along the top - {string.Join(", ", names)}.\n");
        }

        /// <summary>
        /// The mesh for one wall: a unit box like Unity's cube, bounds and all, whose profile across
        /// the wall is the skirting, the bevel up off it, the wall and its rounded top. Built in
        /// metres for the wall's thickness and height and divided back into the unit box, so on the
        /// wall the top is round and the skirting the same height everywhere. Walls of the same shape
        /// share one.
        /// </summary>
        public static Mesh WallMesh(Transform wall)
        {
            Vector3 scale = wall.lossyScale;
            float sx = Mathf.Abs(scale.x), sy = Mathf.Abs(scale.y), sz = Mathf.Abs(scale.z);
            bool alongZ = sz >= sx;
            float across = alongZ ? sx : sz;

            // The ground, in metres from the wall's middle.
            float ground = wall.InverseTransformPoint(new Vector3(wall.position.x, 0f, wall.position.z)).y * sy;
            float sunk = Mathf.Max(0f, ground + sy * 0.5f);

            string name = $"DinerWall_{Mathf.RoundToInt(across * 1000f)}mm_{Mathf.RoundToInt(sy * 100f)}cm_sunk{Mathf.RoundToInt(sunk * 100f)}";
            Mesh built = BuildWall(name, across, sy, ground, alongZ);
            return SaveMesh(built, WallFolder + "/" + name + ".asset");
        }

        struct Point
        {
            public Vector2 P, N;
            public Color C;

            public Point(float x, float y, Vector2 n, Color c)
            {
                P = new Vector2(x, y);
                N = n;
                C = c;
            }
        }

        static Mesh BuildWall(string name, float across, float height, float ground, bool alongZ)
        {
            float w = across * 0.5f, wb = w - SkirtingProud;
            float yb = -height * 0.5f, yt = height * 0.5f;
            float r = Mathf.Min(TopRadius, wb * 0.8f);
            float ys = Mathf.Clamp(ground + SkirtingHeight, yb + 0.001f, yt - r - 0.03f);
            float yc = ys + SkirtingProud;

            // The wall's face a shade darker low down, where the light reaches less, and the top lighter.
            Color Face(float y) => Color.Lerp(Cream * 0.93f, Cream, Mathf.SmoothStep(0f, 1f, (y - yc) / 0.6f));
            Color Foot(float y) => Skirting * Mathf.Lerp(0.9f, 1f, Mathf.Clamp01((y - ground) / SkirtingHeight));

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colours = new List<Color>();
            var triangles = new List<int>();

            Vector3 Unit(float a, float y, float along) =>
                alongZ ? new Vector3(a / across, y / height, along) : new Vector3(along, y / height, a / across);

            Vector3 UnitNormal(Vector2 n) =>
                (alongZ ? new Vector3(n.x * across, n.y * height, 0f) : new Vector3(0f, n.y * height, n.x * across)).normalized;

            void Quad(int a, int b, int c, int d, Vector3 facing)
            {
                bool front = Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), facing) >= 0f;
                if (front) triangles.AddRange(new[] { a, b, c, a, c, d });
                else triangles.AddRange(new[] { a, c, b, a, d, c });
            }

            // A strip of the profile run the wall's length, smooth across its points.
            void Strip(IList<Point> points)
            {
                int start = vertices.Count;
                foreach (Point p in points)
                {
                    foreach (float along in new[] { -0.5f, 0.5f })
                    {
                        vertices.Add(Unit(p.P.x, p.P.y, along));
                        normals.Add(UnitNormal(p.N));
                        colours.Add(p.C);
                    }
                }

                for (int i = 0; i < points.Count - 1; i++)
                {
                    int a = start + i * 2;
                    Quad(a, a + 1, a + 3, a + 2, UnitNormal(points[i].N + points[i + 1].N));
                }
            }

            Vector2 bevel = new Vector2(1f, 1f).normalized;

            // Skirting faces and their bevels, each side.
            foreach (float side in new[] { 1f, -1f })
            {
                var n = new Vector2(side, 0f);
                Strip(new[] { new Point(side * w, yb, n, Foot(yb)), new Point(side * w, ys, n, Foot(ys)) });

                var nb = new Vector2(side * bevel.x, bevel.y);
                Strip(new[] { new Point(side * w, ys, nb, Foot(ys)), new Point(side * wb, yc, nb, Foot(ys)) });
            }

            // The wall's faces and its rounded top, one smooth run from one side's skirting to the other's.
            var run = new List<Point> { new Point(wb, yc, Vector2.right, Face(yc)), new Point(wb, yt - r, Vector2.right, Face(yt - r)) };
            for (int k = 1; k <= TopSteps; k++)
            {
                float a = k / (float)TopSteps * Mathf.PI * 0.5f;
                var n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                run.Add(new Point(wb - r + r * n.x, yt - r + r * n.y, n, Color.Lerp(Face(yt - r), CreamCap, n.y)));
            }

            for (int k = run.Count - 1; k >= 0; k--)
            {
                Point p = run[k];
                run.Add(new Point(-p.P.x, p.P.y, new Vector2(-p.N.x, p.N.y), p.C));
            }

            Strip(run);

            // The ends: the profile, level by level up it, each band its own quad.
            var levels = new List<Point>
            {
                new Point(w, yb, Vector2.zero, Foot(yb)), new Point(w, ys, Vector2.zero, Foot(ys)),
                new Point(wb, yc, Vector2.zero, Face(yc)), new Point(wb, yt - r, Vector2.zero, Face(yt - r)),
            };
            for (int k = 1; k <= TopSteps; k++)
            {
                float a = k / (float)TopSteps * Mathf.PI * 0.5f;
                levels.Add(new Point(wb - r + r * Mathf.Cos(a), yt - r + r * Mathf.Sin(a), Vector2.zero, Color.Lerp(Face(yt - r), CreamCap, Mathf.Sin(a))));
            }

            foreach (float along in new[] { -0.5f, 0.5f })
            {
                Vector3 facing = alongZ ? new Vector3(0f, 0f, along) : new Vector3(along, 0f, 0f);
                for (int i = 0; i < levels.Count - 1; i++)
                {
                    Point lo = levels[i], hi = levels[i + 1];
                    Color c = i < 2 ? Foot(lo.P.y) : lo.C;
                    Color ch = i < 2 ? Foot(lo.P.y) : hi.C;

                    int start = vertices.Count;
                    vertices.Add(Unit(lo.P.x, lo.P.y, along));
                    vertices.Add(Unit(hi.P.x, hi.P.y, along));
                    vertices.Add(Unit(-hi.P.x, hi.P.y, along));
                    vertices.Add(Unit(-lo.P.x, lo.P.y, along));
                    colours.Add(c);
                    colours.Add(ch);
                    colours.Add(ch);
                    colours.Add(c);
                    for (int k = 0; k < 4; k++) normals.Add(facing.normalized);

                    Quad(start, start + 1, start + 2, start + 3, facing);
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Saves into the mesh already at <paramref name="path"/> if there is one, through the mesh API, so whatever uses it keeps it.</summary>
        static Mesh SaveMesh(Mesh built, string path)
        {
            ContentGenerator.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, path);
                AssetDatabase.SaveAssetIfDirty(built);
                return built;
            }

            existing.Clear();
            existing.name = built.name;
            existing.SetVertices(built.vertices);
            existing.SetNormals(built.normals);
            existing.SetColors(built.colors);
            existing.subMeshCount = built.subMeshCount;
            for (int s = 0; s < built.subMeshCount; s++) existing.SetTriangles(built.GetTriangles(s), s);
            existing.RecalculateBounds();

            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssetIfDirty(existing);
            return existing;
        }

        // --- The door ---------------------------------------------------------------------------------

        /// <summary>
        /// Widens the doorway between the two front pieces for a pair of doors, builds the frame and
        /// the two leaves for it and puts them into the door there, in place of its old model, with
        /// <see cref="SwingDoors"/> to swing them.
        /// </summary>
        static void PlaceDoor(Transform frontLeft, Transform frontRight, StringBuilder report)
        {
            if (frontLeft == null || frontRight == null)
            {
                report.Append("  Door: the front wall is one piece, so there is no doorway to fit a door to.\n");
                return;
            }

            Bounds l = BoundsOf(frontLeft), r = BoundsOf(frontRight);
            if (l.size.x < l.size.z)
            {
                report.Append("  Door: the front wall does not run east-west, so the door was left as it was.\n");
                return;
            }

            if (l.center.x > r.center.x)
            {
                (l, r) = (r, l);
                (frontLeft, frontRight) = (frontRight, frontLeft);
            }

            DoorBehaviour door = FindDoor(frontLeft, frontRight);
            if (door == null)
            {
                report.Append("  Door: no DoorBehaviour in the doorway, so no door was fitted.\n");
                return;
            }

            // Wide enough for two doors: the front pieces end further from the middle.
            float before = r.min.x - l.max.x;
            bool widened = before < DoubleDoorway - 0.02f;
            if (widened)
            {
                SetDoorway(frontLeft, frontRight, DoubleDoorway);
                l = BoundsOf(frontLeft);
                r = BoundsOf(frontRight);
                report.Append($"  Doorway: widened from {before:0.##} m to {r.min.x - l.max.x:0.##} m about its middle.\n");
            }

            float gap = r.min.x - l.max.x;
            float depth = Mathf.Max(l.size.z, r.size.z);
            var centre = new Vector3((l.max.x + r.min.x) * 0.5f, 0f, (l.center.z + r.center.z) * 0.5f);

            float half = gap * 0.5f - JambOverlap;
            Mesh frame = BuildFrame(half, depth + FrameProud * 2f).Save(DoubleFrameMeshPath, "DinerDoor_DoubleFrame");
            Mesh leafMesh = BuildSwingLeaf(half).Save(SwingLeafMeshPath, "DinerDoor_SwingLeaf");
            Material paint = AssetDatabase.LoadAssetAtPath<Material>(PaintedMaterialPath);
            if (paint == null) paint = B.PaintedMaterial();

            Transform visual = VisualRoot(door);
            Transform root = visual.Find(DoorName);
            if (root == null)
            {
                root = new GameObject(DoorName).transform;
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Diner door");
                root.SetParent(visual, false);
            }

            Undo.RecordObject(root, "Diner door");
            Undo.RecordObject(root.gameObject, "Diner door");
            root.gameObject.SetActive(true);
            root.SetPositionAndRotation(centre, Quaternion.identity);
            root.localScale = Vector3.one;
            ScaleFree(root);

            Transform frameT = Child(root, "Frame");
            frameT.localPosition = Vector3.zero;
            frameT.localRotation = Quaternion.identity;
            B.Show(frameT.gameObject, frame, new[] { paint });

            // The single leaf this used to hang, kept but off.
            Transform single = root.Find("Leaf");
            if (single != null)
            {
                Undo.RecordObject(single.gameObject, "Diner door");
                single.gameObject.SetActive(false);
            }

            // A leaf on each jamb, shut across the doorway and meeting in the middle. The leaf runs
            // along its own +Z from the hinge, so the left one is turned to 90 and the right to -90.
            Transform leafLeft = Hang("LeafLeft", root, centre + Vector3.right * (-half + HingeInset), 90f, leafMesh, paint);
            Transform leafRight = Hang("LeafRight", root, centre + Vector3.right * (half - HingeInset), -90f, leafMesh, paint);

            Transform old = visual.Find(OldDoorName);
            if (old != null)
            {
                Undo.RecordObject(old.gameObject, "Diner door");
                old.gameObject.SetActive(false);
            }

            // The doors swing themselves now; the old behaviour would turn a leaf that is switched off.
            Undo.RecordObject(door, "Diner door");
            door.enabled = false;

            var swing = door.GetComponent<SwingDoors>();
            if (swing == null) swing = Undo.AddComponent<SwingDoors>(door.gameObject);
            Undo.RecordObject(swing, "Diner door");
            swing.enabled = true;
            swing.Configure(root, leafLeft, leafRight, gap * 0.5f, widened ? before : 0f);
            EditorUtility.SetDirty(swing);

            report.Append($"  Door: frame and a pair of swing doors in '{Path(root)}' at ({centre.x:0.##}, {centre.z:0.##}), " +
                          $"doorway {gap:0.##} m, each leaf {half - HingeInset - MeetGap * 0.5f:0.##} m; '{OldDoorName}' and the " +
                          "single leaf switched off; SwingDoors swings them, DoorBehaviour is off.\n");
        }

        /// <summary>A leaf on its hinge at <paramref name="hinge"/>, shut at <paramref name="yaw"/>.</summary>
        static Transform Hang(string name, Transform root, Vector3 hinge, float yaw, Mesh mesh, Material paint)
        {
            Transform leaf = Child(root, name);
            Undo.RecordObject(leaf.gameObject, "Diner door");
            leaf.gameObject.SetActive(true);
            leaf.SetPositionAndRotation(hinge, Quaternion.Euler(0f, yaw, 0f));
            leaf.localScale = Vector3.one;
            B.Show(leaf.gameObject, mesh, new[] { paint });
            return leaf;
        }

        /// <summary>
        /// Moves the two front pieces' facing ends so the doorway between them is <paramref name="width"/>
        /// across, about the same middle. The far end of each piece stays where it is.
        /// </summary>
        static void SetDoorway(Transform frontLeft, Transform frontRight, float width)
        {
            Bounds l = BoundsOf(frontLeft), r = BoundsOf(frontRight);
            if (l.center.x > r.center.x)
            {
                (l, r) = (r, l);
                (frontLeft, frontRight) = (frontRight, frontLeft);
            }

            float each = (width - (r.min.x - l.max.x)) * 0.5f;
            Shorten(frontLeft, Vector3.right, each);
            Shorten(frontRight, Vector3.left, each);
        }

        /// <summary>Takes <paramref name="metres"/> off the end of a wall facing <paramref name="end"/> (a negative amount lengthens it).</summary>
        static void Shorten(Transform wall, Vector3 end, float metres)
        {
            Undo.RecordObject(wall, "Diner doorway");

            Vector3 scale = wall.localScale;
            bool alongZ = Mathf.Abs(Vector3.Dot(wall.forward, end)) > Mathf.Abs(Vector3.Dot(wall.right, end));
            float perUnit = alongZ
                ? (Mathf.Abs(scale.z) > 1e-4f ? wall.lossyScale.z / scale.z : 1f)
                : (Mathf.Abs(scale.x) > 1e-4f ? wall.lossyScale.x / scale.x : 1f);
            float change = metres / Mathf.Max(1e-4f, Mathf.Abs(perUnit));

            if (alongZ) scale.z -= change * Mathf.Sign(scale.z);
            else scale.x -= change * Mathf.Sign(scale.x);

            wall.localScale = scale;
            wall.position -= end * (metres * 0.5f);
        }

        /// <summary>Keeps the door's parts at their true size under a parent that might be scaled.</summary>
        static void ScaleFree(Transform t)
        {
            Vector3 s = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(1f / Mathf.Max(1e-4f, s.x), 1f / Mathf.Max(1e-4f, s.y), 1f / Mathf.Max(1e-4f, s.z));
        }

        static Transform Child(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null)
            {
                Undo.RecordObject(t, "Diner door");
                return t;
            }

            t = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(t.gameObject, "Diner door");
            t.SetParent(parent, false);
            return t;
        }

        static DoorBehaviour FindDoor(Transform frontLeft, Transform frontRight)
        {
            Bounds l = BoundsOf(frontLeft), r = frontRight != null ? BoundsOf(frontRight) : l;
            if (l.center.x > r.center.x) (l, r) = (r, l);
            float x = (l.max.x + r.min.x) * 0.5f, z = (l.center.z + r.center.z) * 0.5f;

            DoorBehaviour best = null;
            float bestDistance = 3f;
            foreach (DoorBehaviour door in Object.FindObjectsByType<DoorBehaviour>(FindObjectsSortMode.None))
            {
                Vector3 p = door.transform.position;
                float d = new Vector2(p.x - x, p.z - z).magnitude;
                if (d < bestDistance)
                {
                    best = door;
                    bestDistance = d;
                }
            }

            return best;
        }

        /// <summary>Where the door's models live: its DoorVisual group, or the door itself.</summary>
        static Transform VisualRoot(DoorBehaviour door)
        {
            Transform visual = door.transform.Find("DoorVisual");
            return visual != null ? visual : door.transform;
        }

        /// <summary>
        /// The frame: a band of cream round a rounded arch, chunkier than the wall and standing a
        /// little proud of both its faces, its edges rounded, with a dark sill across the doorway.
        /// <paramref name="half"/> is half the doorway; the jambs stand on the walls' ends either side.
        /// </summary>
        static B.Model BuildFrame(float half, float depth)
        {
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.86f, 1f, Mathf.SmoothStep(0f, 1f, p.y / 0.7f)) };

            float centreHalf = half + FrameBand * 0.5f;
            float centreTop = OpeningHeight + FrameBand * 0.5f;
            const float foot = -0.03f;
            const float edge = 0.045f;

            Vector3 At(float u, float v)
            {
                Arch(v, centreHalf, centreTop, ArchRadius, foot, out Vector2 c, out Vector2 outward);
                Vector2 q = RoundRect(u, FrameBand, depth, edge);
                Vector2 p = c + outward * q.x;
                return new Vector3(p.x, p.y, q.y);
            }

            Vector3 Out(float u, float v)
            {
                Arch(v, centreHalf, centreTop, ArchRadius, foot, out Vector2 c, out _);
                return At(u, v) - new Vector3(c.x, c.y, 0f);
            }

            // Inside the arch - the reveal round the doorway - a shade darker than the faces.
            Color Paint(Vector3 p, Vector3 n)
            {
                if (Mathf.Abs(n.z) > 0.5f) return FrameCream;
                var toward = new Vector2(-p.x, Mathf.Min(p.y, OpeningHeight - ArchRadius) - p.y);
                bool reveal = toward.sqrMagnitude > 1e-6f && Vector2.Dot(new Vector2(n.x, n.y), toward.normalized) > 0.5f;
                return reveal ? FrameCream * 0.9f : FrameCream;
            }

            m.Surface(Paint, 48, 160, Facing(At, Out), true);

            // The sill across the doorway, between the jambs, a hair above the floor.
            m.Box(Sill, new Vector3(0f, 0.022f, 0f), new Vector3(half * 2f, 0.044f, depth - 0.04f), 0.01f, 2);
            return m;
        }

        /// <summary>
        /// One of the pair of swing doors, in its own space: the hinge's axis on the origin, the leaf
        /// running out along +Z to the meeting edge in the doorway's middle - so turned to 90 on the
        /// left jamb, or to -90 on the right, it closes its half of the doorway. <paramref name="half"/>
        /// is half the doorway. Both faces alike, as a door that swings both ways is, so the one leaf
        /// hangs on either jamb: red, the top corner by the hinge rounded to the arch and the meeting
        /// corner all but square, its edges rounded; a porthole, a steel kick plate along the foot and
        /// a chrome push plate by the meeting edge on each face, three hinges.
        /// </summary>
        static B.Model BuildSwingLeaf(float half)
        {
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.85f, 1f, Mathf.SmoothStep(0f, 1f, p.y / 0.8f)) };

            float h = LeafThickness * 0.5f;
            float a0 = LeafClearance - HingeInset;
            float a1 = half - HingeInset - MeetGap * 0.5f;
            float y0 = LeafFoot, y1 = OpeningHeight - LeafClearance;
            float corner = ArchRadius - FrameBand * 0.5f - LeafClearance;
            var middle = new Vector2((a0 + a1) * 0.5f, (y0 + y1) * 0.5f);

            // The outline inset by the edge's rounding: the faces fill it, the edge rolls round it.
            void Outline(float t, out Vector2 at, out Vector2 normal) =>
                RoundTopRect(t, a0 + LeafEdge, a1 - LeafEdge, y0 + LeafEdge, y1 - LeafEdge,
                             corner - LeafEdge, MeetCorner - LeafEdge, out at, out normal);

            Vector3 Leaf(Vector2 q, float x) => new Vector3(x, q.y, q.x);

            Color Red(Vector3 p, Vector3 n) => DoorRed;

            foreach (float side in new[] { 1f, -1f })
            {
                Vector3 Face(float u, float v)
                {
                    Outline(u, out Vector2 at, out _);
                    return Leaf(Vector2.Lerp(middle, at, v), side * h);
                }

                m.Surface(Red, LeafSteps, 6, Facing(Face, (u, v) => Vector3.right * side), true);
            }

            Vector3 Edge(float u, float v)
            {
                Outline(u, out Vector2 at, out Vector2 normal);
                float theta = v * Mathf.PI;
                return Leaf(at + normal * (LeafEdge * Mathf.Sin(theta)), h * Mathf.Cos(theta));
            }

            Vector3 EdgeOut(float u, float v)
            {
                Outline(u, out _, out Vector2 normal);
                return Leaf(normal, 0f);
            }

            m.Surface(Red, LeafSteps, 8, Facing(Edge, EdgeOut), true);

            // The porthole, through both faces: a chrome ring round light blue glass with a glint.
            var port = new Vector2(middle.x, PortholeHeight);
            foreach (float side in new[] { 1f, -1f })
            {
                float x = side * (h + 0.003f);
                Vector3 Disc(float u, float v)
                {
                    float a = u * Mathf.PI * 2f;
                    return Leaf(port + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (PortholeRadius * v), x);
                }

                Color GlassPaint(Vector3 p, Vector3 n)
                {
                    var d = new Vector2(p.z - port.x, p.y - port.y) / PortholeRadius;
                    float angle = Mathf.Atan2(d.y, d.x * side) * Mathf.Rad2Deg;
                    bool glint = d.magnitude > 0.45f && d.magnitude < 0.85f && angle > 100f && angle < 165f;
                    return glint ? GlassGlint : Color.Lerp(Glass * 1.08f, Glass * 0.9f, Mathf.Clamp01(0.5f - d.y * 0.5f));
                }

                m.Surface(GlassPaint, 40, 8, Facing(Disc, (u, v) => Vector3.right * side), true);

                const float tube = 0.024f, flat = 0.014f;
                float ring = PortholeRadius + tube * 0.6f;
                Vector3 Rim(float u, float v)
                {
                    float a = u * Mathf.PI * 2f, b = v * Mathf.PI * 2f;
                    var radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    return Leaf(port + radial * (ring + tube * Mathf.Cos(b)), side * h + side * flat * Mathf.Sin(b));
                }

                Vector3 RimOut(float u, float v)
                {
                    float a = u * Mathf.PI * 2f;
                    var radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    return Rim(u, v) - Leaf(port + radial * ring, side * h);
                }

                m.Surface((p, n) => Chrome, 40, 10, Facing(Rim, RimOut), true);
            }

            foreach (float side in new[] { 1f, -1f })
            {
                // A kick plate scuffed by every boot that pushed through; a push plate at hand height by the meeting edge.
                m.Box(Steel, new Vector3(side * (h + 0.003f), LeafFoot + 0.13f, middle.x), new Vector3(0.006f, 0.22f, a1 - a0 - 0.1f), 0.003f);
                m.Box(Chrome, new Vector3(side * (h + 0.004f), BarHeight + 0.12f, a1 - 0.13f), new Vector3(0.008f, 0.3f, 0.1f), 0.004f);

                // Three hinge straps on each face.
                foreach (float y in new[] { 0.32f, 1.02f, 1.72f })
                    m.Box(B.Iron, new Vector3(side * (h + 0.004f), y, a0 + 0.09f), new Vector3(0.01f, 0.07f, 0.15f), 0.004f);
            }

            // And their barrels on the hinge edge.
            foreach (float y in new[] { 0.32f, 1.02f, 1.72f })
                m.Tube(B.Iron, new Vector3(0f, y - 0.06f, 0f), new Vector3(0f, y + 0.06f, 0f), 0.032f, 10);

            return m;
        }

        /// <summary>
        /// The middle of the frame's band, by <paramref name="v"/> from the left foot up, over the
        /// arch and down to the right foot, and the way out from the doorway there.
        /// </summary>
        static void Arch(float v, float half, float top, float radius, float foot, out Vector2 at, out Vector2 outward)
        {
            float leg = top - radius - foot;
            float arc = radius * Mathf.PI * 0.5f;
            float head = 2f * (half - radius);
            float s = Mathf.Clamp01(v) * (2f * leg + 2f * arc + head);

            if (s <= leg)
            {
                at = new Vector2(-half, foot + s);
                outward = Vector2.left;
                return;
            }

            s -= leg;
            if (s <= arc)
            {
                float a = Mathf.PI - s / radius;
                outward = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                at = new Vector2(-half + radius, top - radius) + outward * radius;
                return;
            }

            s -= arc;
            if (s <= head)
            {
                at = new Vector2(-half + radius + s, top);
                outward = Vector2.up;
                return;
            }

            s -= head;
            if (s <= arc)
            {
                float a = Mathf.PI * 0.5f - s / radius;
                outward = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                at = new Vector2(half - radius, top - radius) + outward * radius;
                return;
            }

            s -= arc;
            at = new Vector2(half, top - radius - s);
            outward = Vector2.right;
        }

        /// <summary>A point round a <paramref name="w"/> by <paramref name="h"/> rectangle about the origin, its corners rounded to <paramref name="r"/>, by <paramref name="t"/> anticlockwise from the middle of its right side.</summary>
        static Vector2 RoundRect(float t, float w, float h, float r)
        {
            float hx = w * 0.5f - r, hy = h * 0.5f - r;
            float arc = r * Mathf.PI * 0.5f;
            float s = Mathf.Repeat(t, 1f) * (4f * arc + 4f * hx + 4f * hy);

            if (s <= hy) return new Vector2(w * 0.5f, s);
            s -= hy;

            for (int corner = 0; corner < 4; corner++)
            {
                var c = new Vector2(corner == 0 || corner == 3 ? hx : -hx, corner < 2 ? hy : -hy);
                float start = corner * Mathf.PI * 0.5f;
                if (s <= arc)
                {
                    float a = start + s / r;
                    return c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                }

                s -= arc;
                float straight = corner % 2 == 0 ? 2f * hx : 2f * hy;
                if (corner == 3) straight = hy;
                if (s <= straight)
                {
                    switch (corner)
                    {
                        case 0: return new Vector2(hx - s, h * 0.5f);
                        case 1: return new Vector2(-w * 0.5f, hy - s);
                        case 2: return new Vector2(-hx + s, -h * 0.5f);
                        default: return new Vector2(w * 0.5f, -hy + s);
                    }
                }

                s -= straight;
            }

            return new Vector2(w * 0.5f, 0f);
        }

        /// <summary>
        /// A point round a rectangle with its top left corner rounded to <paramref name="rLeft"/> and its
        /// top right to <paramref name="rRight"/>, by <paramref name="t"/> anticlockwise from its bottom
        /// left corner, and the outward normal there. Each of its six runs - the bottom, the right side,
        /// a corner, the top, a corner, the left side - takes an equal share of t, so a surface run round
        /// it in a multiple of six steps lands on both its square corners.
        /// </summary>
        static void RoundTopRect(float t, float x0, float x1, float y0, float y1, float rLeft, float rRight, out Vector2 at, out Vector2 normal)
        {
            float f = Mathf.Repeat(t, 1f) * 6f;
            int run = Mathf.Min(5, Mathf.FloorToInt(f));
            float s = f - run;

            switch (run)
            {
                case 0:
                    at = new Vector2(Mathf.Lerp(x0, x1, s), y0);
                    normal = Vector2.down;
                    return;
                case 1:
                    at = new Vector2(x1, Mathf.Lerp(y0, y1 - rRight, s));
                    normal = Vector2.right;
                    return;
                case 2:
                    normal = new Vector2(Mathf.Cos(s * Mathf.PI * 0.5f), Mathf.Sin(s * Mathf.PI * 0.5f));
                    at = new Vector2(x1 - rRight, y1 - rRight) + normal * rRight;
                    return;
                case 3:
                    at = new Vector2(Mathf.Lerp(x1 - rRight, x0 + rLeft, s), y1);
                    normal = Vector2.up;
                    return;
                case 4:
                    normal = new Vector2(Mathf.Cos((1f + s) * Mathf.PI * 0.5f), Mathf.Sin((1f + s) * Mathf.PI * 0.5f));
                    at = new Vector2(x0 + rLeft, y1 - rLeft) + normal * rLeft;
                    return;
                default:
                    at = new Vector2(x0, Mathf.Lerp(y1 - rLeft, y0, s));
                    normal = Vector2.left;
                    return;
            }
        }

        /// <summary>
        /// The surface, or the same surface run the other way round if that is what it takes for
        /// its front (see <see cref="SmoothMesh.Surface"/>) to face <paramref name="outward"/>.
        /// </summary>
        static Func<float, float, Vector3> Facing(Func<float, float, Vector3> at, Func<float, float, Vector3> outward)
        {
            const float u = 0.37f, v = 0.53f, e = 1e-3f;
            Vector3 du = at(u + e, v) - at(u - e, v);
            Vector3 dv = at(u, v + e) - at(u, v - e);
            return Vector3.Dot(Vector3.Cross(dv, du), outward(u, v)) >= 0f ? at : (a, b) => at(1f - a, b);
        }

        // --- Plumbing ---------------------------------------------------------------------------------

        static Bounds BoundsOf(Transform piece)
        {
            Renderer r = piece != null ? piece.GetComponentInChildren<Renderer>() : null;
            return r != null ? r.bounds : new Bounds(piece != null ? piece.position : Vector3.zero, Vector3.zero);
        }

        static Transform Find(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;

            return null;
        }

        static string Path(Transform t) => t.parent != null ? $"{Path(t.parent)}/{t.name}" : t.name;
    }
}
