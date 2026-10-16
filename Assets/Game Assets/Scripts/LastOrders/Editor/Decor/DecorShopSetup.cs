using System;
using System.Collections.Generic;
using System.Text;
using LastOrders.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Model = LastOrders.EditorTools.BuildingKit.Model;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The diner's decorations in the shop's Decoration tab, bought and set down on the build grid
    /// like the tables: a potted plant, a menu board, a flower planter, a standard lamp, a gumball
    /// machine, a display shelf, a jukebox and an arcade cabinet (<see cref="DecorModels"/>).
    ///
    /// Each is a placed object with its origin on its footprint's near left corner and the model at
    /// the footprint's middle, facing -Z; a ghost of its meshes over the grid's footprint marker; an
    /// icon rendered from it; and a shop item named as the button shows it. Prices, limits and unlocks
    /// are set the first time an item is made, so tuning done in the assets afterwards is kept.
    ///
    /// Rebuilding writes into the meshes and prefabs already there, so the level's copies follow.
    /// </summary>
    public static class DecorShopSetup
    {
        const string ShopFolder = DecorKit.Folder + "/Shop";
        const string PrefabFolder = "Assets/GridBuildingSystem/Prefabs/Furniture/Decor";
        const string WellPath = "Assets/GridBuildingSystem/Scripts/BuildingTypes/Props/WellProp.asset";
        const int IconLayer = 31;

        sealed class Decor
        {
            public string Title, Key;
            public ObjectType Type;
            public int Width = 1, Depth = 1, Price, Limit;
            public float Comfort;
            public Func<Model> Build;
        }

        /// <summary>In the shop's order, cheapest first.</summary>
        static readonly Decor[] Decorations =
        {
            new Decor { Title = "Potted Plant", Key = "PottedPlant", Type = ObjectType.PottedPlant, Price = 40, Limit = 8, Comfort = 1.02f, Build = DecorModels.PottedPlant },
            new Decor { Title = "Menu Board", Key = "MenuBoard", Type = ObjectType.MenuBoard, Price = 50, Limit = 4, Comfort = 1.02f, Build = DecorModels.MenuBoard },
            new Decor { Title = "Floor Lamp", Key = "FloorLamp", Type = ObjectType.FloorLamp, Price = 60, Limit = 6, Comfort = 1.02f, Build = DecorModels.FloorLamp },
            new Decor { Title = "Flower Planter", Key = "FlowerPlanter", Type = ObjectType.FlowerPlanter, Width = 2, Price = 70, Limit = 6, Comfort = 1.03f, Build = DecorModels.FlowerPlanter },
            new Decor { Title = "Gumball Machine", Key = "GumballMachine", Type = ObjectType.GumballMachine, Price = 90, Limit = 3, Comfort = 1.03f, Build = DecorModels.GumballMachine },
            new Decor { Title = "Display Shelf", Key = "DisplayShelf", Type = ObjectType.DisplayShelf, Width = 2, Price = 120, Limit = 4, Comfort = 1.04f, Build = DecorModels.DisplayShelf },
            new Decor { Title = "Jukebox", Key = "Jukebox", Type = ObjectType.Jukebox, Price = 200, Limit = 2, Comfort = 1.06f, Build = DecorModels.Jukebox },
            new Decor { Title = "Arcade Cabinet", Key = "ArcadeCabinet", Type = ObjectType.ArcadeCabinet, Price = 250, Limit = 2, Comfort = 1.06f, Build = DecorModels.ArcadeCabinet },
        };

        [MenuItem("Last Orders/Decor/Build Decorations (Models, Shop, Scene)", priority = 120)]
        static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This builds the diner's decorations - potted plant, menu board, floor lamp, flower planter, gumball machine, display " +
                "shelf, jukebox and arcade cabinet - as placed objects with ghosts, icons and shop items, and lists them in the open " +
                "level's Decoration tab.\n\nThe level is left unsaved.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        [MenuItem("Last Orders/Decor/Dress the Diner with Decorations (Current Scene)", priority = 121)]
        static void RunDress()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This sets one of each decoration out in the diner's dining room, on free cells along the walls and by the door, " +
                "registered with the zone so build mode can move or sell them.\n\nBuild the decorations first. The level is left unsaved.",
                "Dress", "Cancel");

            if (proceed) Debug.Log(Dress());
        }

        [MenuItem("Last Orders/Decor/Remove Decorations from the Diner (Current Scene)", priority = 122)]
        static void RunUndress() => Debug.Log(Undress());

        /// <summary>Builds every decoration, its prefab, ghost, icon and shop item, and lists them in the open level's shop.</summary>
        public static string Apply()
        {
            ContentGenerator.EnsureFolder(ShopFolder);
            ContentGenerator.EnsureFolder(DecorKit.MeshFolder);
            ContentGenerator.EnsureFolder(PrefabFolder);
            var report = new StringBuilder("[Last Orders] Decorations:\n");

            var items = new List<PlacedObjectTypeSO>();
            foreach (Decor decor in Decorations)
            {
                Model model = decor.Build();
                Mesh mesh = model.Save($"{DecorKit.MeshFolder}/Decor_{decor.Key}.asset", $"Decor_{decor.Key}");

                GameObject prefab = Prefab(decor, mesh);
                PlacedObjectTypeSO item = Item(decor, out string note);
                GameObject ghost = Ghost(decor, prefab);
                Sprite icon = Icon(decor, prefab);

                Undo.RecordObject(item, "Decorations");
                item.prefab = prefab.transform;
                item.visual = ghost.transform;
                item.shopIcon = icon;
                EditorUtility.SetDirty(item);
                AssetDatabase.SaveAssetIfDirty(item);

                PointPrefabAt(prefab, decor, item);
                items.Add(item);
                report.Append($"  {item.name}: {note}, {decor.Width}x{decor.Depth}, {item.PriceAmount} coins, up to {item.maxItems}; " +
                              $"{mesh.vertexCount} verts{(mesh.subMeshCount > 1 ? " + glow" : "")}, {icon.name}\n");
            }

            report.Append(ListInShop(items));
            return report.ToString();
        }

        static string PrefabPath(Decor decor) => $"{PrefabFolder}/pfDecor_{decor.Key}.prefab";

        // --- The prefab -----------------------------------------------------------------------------

        /// <summary>
        /// The placed object: the identity and a collider on the root at the footprint's corner, the
        /// model as a child at its middle. An existing prefab is edited in place, so the level's copies
        /// keep their overrides.
        /// </summary>
        static GameObject Prefab(Decor decor, Mesh mesh)
        {
            string path = PrefabPath(decor);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                using var editing = new PrefabUtility.EditPrefabContentsScope(path);
                Fill(editing.prefabContentsRoot, decor, mesh);
            }
            else
            {
                var root = new GameObject($"pfDecor_{decor.Key}");
                try
                {
                    Fill(root, decor, mesh);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static void Fill(GameObject root, Decor decor, Mesh mesh)
        {
            if (root.GetComponent<PlacedObject_Done>() == null) root.AddComponent<PlacedObject_Done>();

            var middle = new Vector3(decor.Width * 0.5f, 0f, decor.Depth * 0.5f);
            Transform model = root.transform.Find("Model");
            if (model == null)
            {
                model = new GameObject("Model").transform;
                model.SetParent(root.transform, false);
            }

            model.localPosition = middle;
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one;
            BuildingKit.Show(model.gameObject, mesh, DecorKit.MaterialsFor(mesh));
            model.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;

            // Taps in build mode find the object by this.
            var collider = root.GetComponent<BoxCollider>();
            if (collider == null) collider = root.AddComponent<BoxCollider>();
            collider.center = middle + mesh.bounds.center;
            collider.size = mesh.bounds.size;
        }

        /// <summary>The placed-object identity pointed at its item, so a copy the shop places and the ones in the level both know what they are.</summary>
        static void PointPrefabAt(GameObject prefab, Decor decor, PlacedObjectTypeSO item)
        {
            using var editing = new PrefabUtility.EditPrefabContentsScope(AssetDatabase.GetAssetPath(prefab));
            var so = new SerializedObject(editing.prefabContentsRoot.GetComponent<PlacedObject_Done>());
            so.FindProperty("placedObjectTypeSO").objectReferenceValue = item;
            so.FindProperty("shopItemType").intValue = (int)decor.Type;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- The shop item --------------------------------------------------------------------------

        static PlacedObjectTypeSO Item(Decor decor, out string note)
        {
            string path = $"{ShopFolder}/{decor.Title}.asset";
            var item = AssetDatabase.LoadAssetAtPath<PlacedObjectTypeSO>(path);
            bool made = item == null;
            if (made)
            {
                item = ScriptableObject.CreateInstance<PlacedObjectTypeSO>();
                AssetDatabase.CreateAsset(item, path);
            }

            bool first = made || item.width != decor.Width || item.height != decor.Depth;

            Undo.RecordObject(item, "Decorations");
            item.nameString = decor.Title;
            item.categoryType = ObjectCategory.Decoration;
            item.type = decor.Type;
            item.width = decor.Width;
            item.height = decor.Depth;
            item.buildCategory = BuildCategory.Comfort;
            if (first)
            {
                item.PriceAmount = decor.Price;
                item.maxItems = decor.Limit;
                item.requiredLevel = 0;
                item.isUnlocked = true;
                item.Sellable = true;
                item.comfortMultiplier = decor.Comfort;
                item.buildClockMinutes = 1f;
                item.maxHealth = 40f;
            }

            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssetIfDirty(item);
            note = made ? "made" : first ? "refitted" : "updated";
            return item;
        }

        // --- The ghost and the icon -----------------------------------------------------------------

        /// <summary>The ghost: the grid's footprint marker sized to the decoration's cells and its meshes, with no scripts or colliders.</summary>
        static GameObject Ghost(Decor decor, GameObject prefab)
        {
            string path = $"{PrefabFolder}/pfDecor_{decor.Key}Ghost.prefab";
            var root = new GameObject($"pfDecor_{decor.Key}Ghost");

            try
            {
                var well = AssetDatabase.LoadAssetAtPath<PlacedObjectTypeSO>(WellPath);
                Transform marker = well != null && well.visual != null ? well.visual : null;
                foreach (string part in new[] { "Area", "Anchor" })
                {
                    Transform source = marker != null ? marker.Find(part) : null;
                    if (source == null) continue;

                    GameObject copy = Object.Instantiate(source.gameObject, root.transform);
                    copy.name = part;
                    copy.transform.localRotation = source.localRotation;
                    foreach (Collider c in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

                    if (part == "Area")
                    {
                        copy.transform.localPosition = new Vector3(decor.Width * 0.5f, 0f, decor.Depth * 0.5f);
                        copy.transform.localScale = new Vector3(decor.Width, source.localScale.y, decor.Depth);
                    }
                    else
                    {
                        copy.transform.localPosition = source.localPosition;
                        copy.transform.localScale = source.localScale;
                    }
                }

                GameObject model = Object.Instantiate(prefab, root.transform);
                model.name = "Decor";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                MeshesOnly(model);

                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void MeshesOnly(GameObject model)
        {
            foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        static Sprite Icon(Decor decor, GameObject prefab)
        {
            Texture2D render = Render(prefab);
            try
            {
                return CropIcons.SaveIcon(ShopFolder, $"Icon_{decor.Key}", CropIcons.Compose(render));
            }
            finally
            {
                Object.DestroyImmediate(render);
            }
        }

        /// <summary>The decoration on its own, seen from in front, a little to the right and above, as the game's camera sees it: square, transparent round it.</summary>
        static Texture2D Render(GameObject prefab, int size = 1024)
        {
            GameObject model = Object.Instantiate(prefab);
            var camera = new GameObject("__DecorIconCamera").AddComponent<Camera>();
            RenderTexture target = null;

            try
            {
                model.transform.SetPositionAndRotation(new Vector3(0f, -400f, 0f), Quaternion.identity);
                MeshesOnly(model);
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = IconLayer;

                var bounds = new Bounds(model.transform.position, Vector3.zero);
                bool first = true;
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                {
                    if (first) bounds = r.bounds;
                    else bounds.Encapsulate(r.bounds);
                    first = false;
                }

                Vector3 toCamera = new Vector3(0.45f, 0.75f, -1f).normalized;
                const float Fov = 16f;
                float distance = bounds.extents.magnitude / Mathf.Sin(Fov * 0.5f * Mathf.Deg2Rad);

                camera.transform.position = bounds.center + toCamera * distance;
                camera.transform.LookAt(bounds.center);
                camera.fieldOfView = Fov;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = distance * 3f;
                camera.cullingMask = 1 << IconLayer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.allowHDR = false;

                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                data.renderShadows = true;

                target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                texture.Apply(false);
                RenderTexture.active = previous;
                return texture;
            }
            finally
            {
                camera.targetTexture = null;
                if (target != null) Object.DestroyImmediate(target);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(model);
            }
        }

        // --- The open level -------------------------------------------------------------------------

        /// <summary>
        /// The Decoration entry of the open level's shop: the decorations right after the table, ahead
        /// of the storehouse, walls and door already listed there.
        /// </summary>
        static string ListInShop(List<PlacedObjectTypeSO> items)
        {
            var database = Object.FindFirstObjectByType<ObjectDatabaseHandler>(FindObjectsInactive.Include);
            if (database == null) return "  WARNING: the open level has no ObjectDatabaseHandler (no shop) - nothing listed.\n";

            Undo.RecordObject(database, "Decorations");
            var so = new SerializedObject(database);
            SerializedProperty databases = so.FindProperty("objectsDatabases");
            SerializedProperty entry = null;
            for (int i = 0; i < databases.arraySize; i++)
            {
                SerializedProperty candidate = databases.GetArrayElementAtIndex(i);
                if (candidate.FindPropertyRelative("type").intValue == (int)ObjectCategory.Decoration) entry = candidate;
            }

            if (entry == null)
            {
                databases.arraySize++;
                entry = databases.GetArrayElementAtIndex(databases.arraySize - 1);
                entry.FindPropertyRelative("type").intValue = (int)ObjectCategory.Decoration;
                entry.FindPropertyRelative("placedObjectTypeSOList").arraySize = 0;
            }

            SerializedProperty list = entry.FindPropertyRelative("placedObjectTypeSOList");
            var listed = new List<PlacedObjectTypeSO>();
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is PlacedObjectTypeSO existing && !items.Contains(existing))
                    listed.Add(existing);

            int at = listed.FindIndex(it => it.type == ObjectType.Table1) + 1;
            listed.InsertRange(at, items);

            list.arraySize = listed.Count;
            for (int i = 0; i < listed.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = listed[i];
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return $"  Shop: Decoration lists {string.Join(", ", listed.ConvertAll(it => it.name))}. The level is unsaved.\n";
        }

        /// <summary>Where <see cref="Dress"/> sets each decoration: its near left cell, turned by <see cref="PlacedObjectTypeSO.Dir"/>.</summary>
        sealed class Spot
        {
            public ObjectType Type;
            public Vector2Int Cell;
            public PlacedObjectTypeSO.Dir Dir;
        }

        /// <summary>
        /// Main Level_Apocalypse's dining room (floor x 9.75-29.85, z 15.25-24.2): a menu board and a
        /// plant either side of the way in from the door, set into the room (right against the front
        /// wall the door's arch and the wall hide them from the camera), 2.2 m apart and clear of the
        /// nearest tables' standing spots; along the back, where the floor
        /// meets the kitchen tiles, a lamp, the shelf and the planter to the left of the serving pass and
        /// the gumball machine, arcade cabinet and jukebox to its right. The tables, the aisle up from
        /// the door and the opening between the pass's two modules - the staff's way through - stay clear.
        /// </summary>
        static readonly Spot[] Spots =
        {
            new Spot { Type = ObjectType.MenuBoard, Cell = new Vector2Int(18, 17) },
            new Spot { Type = ObjectType.PottedPlant, Cell = new Vector2Int(21, 16) },
            new Spot { Type = ObjectType.FloorLamp, Cell = new Vector2Int(10, 23) },
            new Spot { Type = ObjectType.DisplayShelf, Cell = new Vector2Int(11, 23) },
            new Spot { Type = ObjectType.FlowerPlanter, Cell = new Vector2Int(13, 23) },
            new Spot { Type = ObjectType.GumballMachine, Cell = new Vector2Int(25, 23) },
            new Spot { Type = ObjectType.ArcadeCabinet, Cell = new Vector2Int(26, 23) },
            new Spot { Type = ObjectType.Jukebox, Cell = new Vector2Int(28, 23) },
        };

        /// <summary>One of each decoration on its <see cref="Spots"/> cell, registered with the zone; any already there is moved.</summary>
        public static string Dress()
        {
            var report = new StringBuilder("[Last Orders] Dressing the diner:\n");
            var zone = Object.FindFirstObjectByType<Zone>(FindObjectsInactive.Include);
            SerializedObject zoneSo = zone != null ? new SerializedObject(zone) : null;
            SerializedProperty placedList = zoneSo?.FindProperty("placedObject");
            Transform parent = Parent(true);

            foreach (Spot spot in Spots)
            {
                Decor decor = Array.Find(Decorations, d => d.Type == spot.Type);
                var item = AssetDatabase.LoadAssetAtPath<PlacedObjectTypeSO>($"{ShopFolder}/{decor.Title}.asset");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(decor));
                if (item == null || prefab == null)
                {
                    report.Append($"  WARNING: {decor.Title} is not built - build the decorations first.\n");
                    continue;
                }

                GameObject instance = null;
                foreach (Transform child in parent)
                    if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == prefab) instance = child.gameObject;

                bool added = instance == null;
                if (added)
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    Undo.RegisterCreatedObjectUndo(instance, "Dress the diner");
                }

                // The grid turns an object about its footprint: the pivot moves to keep the cells.
                Vector2Int offset = item.GetRotationOffset(spot.Dir);
                var at = new Vector3(spot.Cell.x + offset.x, 0f, spot.Cell.y + offset.y);
                Undo.RecordObject(instance.transform, "Dress the diner");
                instance.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, item.GetRotationAngle(spot.Dir), 0f));

                var so = new SerializedObject(instance.GetComponent<PlacedObject_Done>());
                so.FindProperty("placedObjectTypeSO").objectReferenceValue = item;
                so.FindProperty("shopItemType").intValue = (int)item.type;
                so.FindProperty("dir").intValue = (int)spot.Dir;
                so.FindProperty("IsPlaced").boolValue = false;
                so.ApplyModifiedProperties();

                string registered = "";
                if (placedList != null)
                {
                    var placed = instance.GetComponent<PlacedObject_Done>();
                    bool listed = false;
                    for (int k = 0; k < placedList.arraySize; k++)
                        if (placedList.GetArrayElementAtIndex(k).objectReferenceValue == placed) listed = true;

                    if (!listed)
                    {
                        placedList.arraySize++;
                        placedList.GetArrayElementAtIndex(placedList.arraySize - 1).objectReferenceValue = placed;
                        registered = ", registered with the zone";
                    }
                }

                report.Append($"  {decor.Title} {(added ? "added" : "moved")} at cell {spot.Cell} facing {spot.Dir}{registered}\n");
            }

            zoneSo?.ApplyModifiedProperties();
            if (zone == null) report.Append("  WARNING: no Zone in the open level - the decorations are not registered as placed objects.\n");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            report.Append("  The level is unsaved.\n");
            return report.ToString();
        }

        /// <summary>Takes the decorations <see cref="Dress"/> set out back out of the level and off the zone's list.</summary>
        public static string Undress()
        {
            Transform parent = Parent(false);
            if (parent == null) return "[Last Orders] No dressed decorations in the open level.";

            var zone = Object.FindFirstObjectByType<Zone>(FindObjectsInactive.Include);
            if (zone != null)
            {
                var zoneSo = new SerializedObject(zone);
                SerializedProperty placedList = zoneSo.FindProperty("placedObject");
                for (int k = placedList.arraySize - 1; k >= 0; k--)
                    if (placedList.GetArrayElementAtIndex(k).objectReferenceValue is PlacedObject_Done placed && placed.transform.IsChildOf(parent))
                        placedList.DeleteArrayElementAtIndex(k);
                zoneSo.ApplyModifiedProperties();
            }

            int count = parent.childCount;
            Undo.DestroyObjectImmediate(parent.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return $"[Last Orders] Removed {count} decorations from the diner. The level is unsaved.";
        }

        /// <summary>The level's root for the dressed decorations, beside the restaurant.</summary>
        static Transform Parent(bool create)
        {
            const string Name = "DinerDecorations";
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == Name) return root.transform;

            if (!create) return null;
            var made = new GameObject(Name);
            Undo.RegisterCreatedObjectUndo(made, "Dress the diner");
            return made.transform;
        }
    }
}
