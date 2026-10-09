using System.Collections.Generic;
using System.Linq;
using System.Text;
using FoodStreet.Visitors;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Effects;
using LastOrders.Night;
using LastOrders.Repair;
using LastOrders.Restaurant;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Wires the repairs and the growing restaurant into the open scene.
    ///
    /// Repairs need one overlay for the whole level, which draws the wrench over everything
    /// broken. The restaurant needs its five walls told apart - the kitchen wall, the two sides,
    /// and the two pieces of the front either side of the door - and the things on the front
    /// wall's line that have to move out with it: the door, and the door nodes of the visitor
    /// route and of the night shelter. And a floor, which the shell lays again between the walls
    /// whenever they move.
    ///
    /// The walls are read the way the starting-area layout lays them: plain cubes under
    /// "RestaurantWalls", the kitchen wall the run furthest north, the front the runs south of
    /// it. Left and right are the player's, looking at the front from outside it.
    ///
    /// Safe to re-run: the overlay and the shell are found and rewired, never duplicated. It also
    /// brings the night shelter's footprint back into line with the walls, which the layout tools
    /// had left a metre short on the south side.
    /// </summary>
    public static class RestaurantSceneSetup
    {
        const string WallsName = "RestaurantWalls";
        const string OverlayName = "Repairs";
        const string MaterialPath = "Assets/Game Assets/Content/LastOrders/Materials/RestaurantExpansionHighlight.mat";

        const string FloorName = "RestaurantFloor";

        /// <summary>The floor's materials, in the current art style's folder: the previous style's
        /// stay one folder up (see <see cref="EnvKit.Style"/>).</summary>
        public const string DiningMaterialPath = "Assets/Game Assets/Content/LastOrders/Materials/" + EnvKit.Style + "/RestaurantFloor_Dining.mat";

        public const string KitchenMaterialPath = "Assets/Game Assets/Content/LastOrders/Materials/" + EnvKit.Style + "/RestaurantFloor_Kitchen.mat";

        /// <summary>Metres one copy of each floor texture covers: 0.6 m checks, 0.4 m kitchen tiles.</summary>
        const float DiningTile = 2.4f;
        const float KitchenTile = 1.2f;

        /// <summary>A soft sheen, as on the reference's polished diner floor.</summary>
        const float DiningSmoothness = 0.3f;
        const float KitchenSmoothness = 0.24f;

        /// <summary>The kitchen strip along the back wall: the line the stove, sink and fridge stand on.
        /// The same depth the starting-area layout keeps the kitchen line at.</summary>
        const float KitchenDepth = 3f;

        [MenuItem("Last Orders/Restaurant/Set Up Repairs & Expansion (Current Scene)", priority = 75)]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();

            if (scope == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "This scene has no GameLifetimeScope, so nothing would run the repairs or the restaurant.\n\n" +
                    "Open the level that has the Last Orders systems in it.", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                $"This edits '{scene.name}' in place:\n\n" +
                "- Adds the repair overlay: a wrench over anything broken, fixed by a worker\n" +
                "- Makes the restaurant's walls expandable forward, left and right\n" +
                "- Lays the restaurant's floor, which grows with it\n" +
                "- Creates the repair button, the expansion popup and the highlight material if missing\n\n" +
                "The scene is left unsaved.",
                "Set up", "Cancel");

            if (!proceed) return;

            Debug.Log(Apply(scope, save: false));
        }

        /// <summary>The setup against the open scene's scope, with no dialogs - for automation.</summary>
        public static string ApplyToActiveScene(bool save)
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            return scope != null
                ? Apply(scope, save)
                : "[Last Orders] No GameLifetimeScope in the open scene; nothing was changed.";
        }

        public static string Apply(GameLifetimeScope scope, bool save)
        {
            Scene scene = scope.gameObject.scene;
            var report = new StringBuilder("[Last Orders] Repairs and restaurant expansion set up.\n");

            GameBalanceConfig balance = new SerializedObject(scope).FindProperty("balance")
                .objectReferenceValue as GameBalanceConfig;

            // The wrench, the popup - and everything else the catalog is missing.
            WorldUiPrefabGenerator.Ensure(balance);

            SetUpOverlay(scene, scope, report);
            SetUpRestaurant(scene, scope, report);

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);

            return report.ToString();
        }

        // --- Repairs ------------------------------------------------------------------------------

        static void SetUpOverlay(Scene scene, GameLifetimeScope scope, StringBuilder report)
        {
            RepairOverlay overlay = Object.FindFirstObjectByType<RepairOverlay>(FindObjectsInactive.Include);

            if (overlay == null)
            {
                var go = new GameObject(OverlayName);
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "Repair overlay");
                overlay = go.AddComponent<RepairOverlay>();
            }

            var so = new SerializedObject(overlay);
            so.FindProperty("scope").objectReferenceValue = scope;
            so.ApplyModifiedProperties();

            report.Append($"  Repair overlay on '{overlay.name}'. Repairs are done by the Helper; the walls " +
                          "are no longer rebuilt by themselves at dawn (GameBalance > Repairs).\n");
        }

        // --- The restaurant -----------------------------------------------------------------------

        static void SetUpRestaurant(Scene scene, GameLifetimeScope scope, StringBuilder report)
        {
            Transform walls = Find(scene, WallsName);
            if (walls == null)
            {
                report.Append($"  WARNING: no '{WallsName}' in the scene, so the restaurant cannot grow.\n");
                return;
            }

            if (!ReadWalls(walls, out Transform back, out Transform left, out Transform right,
                           out Transform frontLeft, out Transform frontRight, out string problem))
            {
                report.Append($"  WARNING: {problem} The restaurant was not made expandable.\n");
                return;
            }

            Bounds footprint = BoundsOf(back);
            foreach (Transform wall in new[] { left, right, frontLeft, frontRight })
                if (wall != null) footprint.Encapsulate(BoundsOf(wall));

            NightShelter shelter = Object.FindFirstObjectByType<NightShelter>();
            Transform[] fixtures = FrontFixtures(frontLeft, frontRight, shelter);

            if (shelter != null)
            {
                // The routes round the building keep to its walls, not to a guess a metre short.
                var so = new SerializedObject(shelter);
                SerializedProperty prop = so.FindProperty("footprint");
                Bounds old = prop.boundsValue;
                float height = old.size.y > 0.01f ? old.size.y : 3f;

                prop.boundsValue = new Bounds(new Vector3(footprint.center.x, old.center.y, footprint.center.z),
                                              new Vector3(footprint.size.x, height, footprint.size.z));
                so.ApplyModifiedProperties();
            }

            RestaurantShell shell = walls.GetComponent<RestaurantShell>();
            if (shell == null) shell = Undo.AddComponent<RestaurantShell>(walls.gameObject);

            Undo.RecordObject(shell, "Restaurant shell");
            PooledEffect dust = EffectPrefabGenerator.EnsureBuildDust();
            shell.Configure(scope, back, left, right, frontLeft, frontRight, fixtures, shelter, HighlightMaterial(), dust, dust);
            shell.ConfigureFloor(EnsureFloor(walls));
            shell.FitFloor();
            EditorUtility.SetDirty(shell);

            DressWalls(back, left, right, frontLeft, frontRight);

            report.Append($"  Restaurant walls: back '{back.name}', left '{left.name}', right '{right.name}', " +
                          $"front '{(frontLeft != null ? frontLeft.name : "-")}' + '{(frontRight != null ? frontRight.name : "-")}'. " +
                          $"Footprint x {footprint.min.x:0.##}-{footprint.max.x:0.##}, z {footprint.min.z:0.##}-{footprint.max.z:0.##}.\n");
            report.Append($"  Moving out with the front wall: {(fixtures.Length > 0 ? string.Join(", ", fixtures.Select(Path)) : "nothing")}.\n");

            if (Object.FindObjectsByType<ParkingSlot>(FindObjectsSortMode.None).Length > 0)
                report.Append("  The car park's bays count as in the way, so the front can only grow up to them.\n");

            report.Append($"  Floor: diner checkerboard in the dining room, plain tiles {KitchenDepth:0} m deep along the " +
                          "kitchen wall; laid again whenever a wall moves.\n");
            report.Append("  Walls: terracotta skirting, cream, a wooden rail; banded only in height, so they stretch as the " +
                          "building grows without the texture stretching.\n");
        }

        /// <summary>
        /// Gives the walls the diner's paint: a cube mesh with the same bounds as Unity's, UV'd so
        /// the texture's bands run level round every side, and the wall material. The wall's own
        /// transform, collider and everything that measures it are untouched.
        /// </summary>
        static void DressWalls(params Transform[] walls)
        {
            Mesh box = LastOrders.EditorTools.SurfaceKit.DinerWallMesh();
            Material paint = LastOrders.EditorTools.SurfaceKit.DinerWall();

            foreach (Transform wall in walls)
            {
                if (wall == null) continue;

                var filter = wall.GetComponent<MeshFilter>();
                var renderer = wall.GetComponent<MeshRenderer>();
                if (filter == null || renderer == null) continue;

                Undo.RecordObject(filter, "Diner walls");
                Undo.RecordObject(renderer, "Diner walls");
                filter.sharedMesh = box;
                renderer.sharedMaterial = paint;
            }
        }

        // --- The floor ----------------------------------------------------------------------------

        /// <summary>
        /// The restaurant's floor object, made once. It lives on the restaurant's root, not among
        /// the walls - the wall readers take every child with a renderer there for a wall - and
        /// stands at the root's own origin, outside the room, so the starting-area layout does not
        /// take it for furniture and shift it as the room stretches.
        /// </summary>
        static RestaurantFloor EnsureFloor(Transform walls)
        {
            Transform restaurant = walls.root;
            Transform existing = restaurant.Find(FloorName);

            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(FloorName, typeof(MeshFilter), typeof(MeshRenderer));
                Undo.RegisterCreatedObjectUndo(go, "Restaurant floor");
                go.transform.SetParent(restaurant, false);
            }

            Undo.RecordObject(go.transform, "Restaurant floor");
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            RestaurantFloor floor = go.GetComponent<RestaurantFloor>();
            if (floor == null) floor = Undo.AddComponent<RestaurantFloor>(go);

            Undo.RecordObject(floor, "Restaurant floor");
            floor.Configure(FloorMaterial(DiningMaterialPath, TextureGenerator.EnsureDinerFloor(), DiningSmoothness),
                            FloorMaterial(KitchenMaterialPath, TextureGenerator.EnsureKitchenFloor(), KitchenSmoothness),
                            DiningTile, KitchenTile, KitchenDepth);
            EditorUtility.SetDirty(floor);

            return floor;
        }

        /// <summary>
        /// Paints the floor's tiles again if their version has changed and sets both materials up
        /// afresh. The floor already uses the materials, so the scene is not touched.
        /// </summary>
        public static string RestyleFloor()
        {
            FloorMaterial(DiningMaterialPath, TextureGenerator.EnsureDinerFloor(), DiningSmoothness);
            FloorMaterial(KitchenMaterialPath, TextureGenerator.EnsureKitchenFloor(), KitchenSmoothness);
            return "Restaurant floor: dining and kitchen tiles up to date.";
        }

        /// <summary>URP Lit with the generated tiles and their normal map: matte but with a little sheen, as a mopped floor has.</summary>
        static Material FloorMaterial(string path, Texture2D tiles, float smoothness)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                ContentGenerator.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }

            // The mesh is UV'd in texture tiles already, so the material does not scale it again.
            material.SetTexture("_BaseMap", tiles);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetColor("_BaseColor", Color.white);
            TextureGenerator.BindNormal(material, tiles);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(material);

            return material;
        }

        /// <summary>
        /// Tells the five walls apart. Runs along X and runs along Z by their bounds; the kitchen
        /// wall is the X run furthest north, the front the other X runs, split at the door.
        /// </summary>
        static bool ReadWalls(Transform root, out Transform back, out Transform left, out Transform right,
                              out Transform frontLeft, out Transform frontRight, out string problem)
        {
            back = left = right = frontLeft = frontRight = null;

            var alongX = new List<Transform>();
            var alongZ = new List<Transform>();

            foreach (Transform child in root)
            {
                if (!child.gameObject.activeSelf || child.GetComponentInChildren<Renderer>() == null) continue;

                Bounds b = BoundsOf(child);
                (b.size.x >= b.size.z ? alongX : alongZ).Add(child);
            }

            if (alongZ.Count < 2 || alongX.Count < 2)
            {
                problem = $"'{root.name}' should hold two side walls and at least two runs across (the back and the front), " +
                          $"but has {alongZ.Count} and {alongX.Count}.";
                return false;
            }

            alongZ.Sort((a, b) => BoundsOf(a).center.x.CompareTo(BoundsOf(b).center.x));
            left = alongZ[0];
            right = alongZ[alongZ.Count - 1];

            alongX.Sort((a, b) => BoundsOf(b).center.z.CompareTo(BoundsOf(a).center.z));
            back = alongX[0];

            var front = alongX.Skip(1).OrderBy(t => BoundsOf(t).center.x).ToList();
            frontLeft = front[0];
            frontRight = front.Count > 1 ? front[front.Count - 1] : null;

            problem = null;
            return true;
        }

        /// <summary>
        /// The door in the front wall's gap, and the door nodes just outside and inside it: the
        /// visitor route's and the night shelter's. Anything else on that line is the user's to add.
        /// </summary>
        static Transform[] FrontFixtures(Transform frontLeft, Transform frontRight, NightShelter shelter)
        {
            var found = new List<Transform>();

            Bounds leftBounds = BoundsOf(frontLeft);
            Bounds rightBounds = frontRight != null ? BoundsOf(frontRight) : leftBounds;
            float gapCentre = (leftBounds.max.x + rightBounds.min.x) * 0.5f;
            float frontZ = (leftBounds.center.z + rightBounds.center.z) * 0.5f;

            foreach (DoorBehaviour door in Object.FindObjectsByType<DoorBehaviour>(FindObjectsSortMode.None))
            {
                Vector3 p = door.transform.position;
                if (Mathf.Abs(p.x - gapCentre) <= 2.5f && Mathf.Abs(p.z - frontZ) <= 1.5f) Add(found, door.transform);
            }

            foreach (VisitorRoads roads in Object.FindObjectsByType<VisitorRoads>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(roads);
                Add(found, so.FindProperty("doorOutside")?.objectReferenceValue as Transform);
                Add(found, so.FindProperty("doorInside")?.objectReferenceValue as Transform);
            }

            if (shelter != null)
            {
                var so = new SerializedObject(shelter);
                Add(found, so.FindProperty("doorOutside")?.objectReferenceValue as Transform);
                Add(found, so.FindProperty("doorInside")?.objectReferenceValue as Transform);
            }

            return found.ToArray();
        }

        static void Add(List<Transform> into, Transform t)
        {
            if (t == null || into.Contains(t)) return;

            // A node that is a child of something already listed moves with it anyway.
            foreach (Transform existing in into)
                if (t.IsChildOf(existing)) return;

            into.Add(t);
        }

        /// <summary>URP unlit, transparent, white: each strip's colour comes from a property block.</summary>
        static Material HighlightMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;

            ContentGenerator.EnsureFolder(System.IO.Path.GetDirectoryName(MaterialPath).Replace('\\', '/'));

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            material = new Material(shader) { name = "RestaurantExpansionHighlight" };

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            material.color = Color.white;

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHATEST_ON");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        static Bounds BoundsOf(Transform piece)
        {
            if (piece == null) return default;

            Renderer r = piece.GetComponentInChildren<Renderer>();
            return r != null ? r.bounds : new Bounds(piece.position, Vector3.zero);
        }

        static Transform Find(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;

            return null;
        }

        static string Path(Transform t) => t.parent != null ? $"{t.parent.name}/{t.name}" : t.name;
    }
}
