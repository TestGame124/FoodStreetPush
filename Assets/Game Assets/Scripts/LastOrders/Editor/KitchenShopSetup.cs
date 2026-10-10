using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FoodStreet.Placement;
using LastOrders.Restaurant;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Every kitchen station in the shop's Stoves tab - the bakery, the soup kitchen, the salad
    /// station, the griddle and the grill - bought and set down on the build grid like the tables and
    /// the shooters, and the diner's own starting kitchen standing on that grid too.
    ///
    /// Each station is a 3 x 2 cell placed object with its origin on the footprint's near left corner
    /// (the corner builders put each corner there); a <see cref="KitchenFootingRule"/> keeps them
    /// inside the diner. Each gets a shop item named as the button and the cooking panel show it -
    /// the bread and soup stoves' existing items are renamed and refitted rather than replaced - a
    /// ghost that is the station's meshes over the grid's footprint marker, and an icon rendered from
    /// the station at work.
    ///
    /// In the open level the Stoves entry of the shop lists all five, and the five stations along the
    /// kitchen's back wall are laid out on whole cells - griddle, bakery, soup, salad, grill - and
    /// registered with the level's zone as placed objects, so the grid knows where they stand, the
    /// shop counts them against each item's limit and build mode can move them. The level is left
    /// unsaved.
    /// </summary>
    public static class KitchenShopSetup
    {
        const string Folder = "Assets/Game Assets/Content/LastOrders/Kitchen/Shop";
        const string ProfilePath = Folder + "/Placement_Kitchen.asset";
        const string RulePath = Folder + "/Rule_KitchenFooting.asset";
        const string WellPath = "Assets/GridBuildingSystem/Scripts/BuildingTypes/Props/WellProp.asset";
        const string StovesFolder = "Assets/GridBuildingSystem/Prefabs/Furniture/Stoves";
        const int IconLayer = 31;

        const int Width = GriddleStationModel.FootprintWidth, Depth = GriddleStationModel.FootprintDepth;

        /// <summary>The build grid's cells are a metre square on whole-metre lines.</summary>
        const float Cell = 1f;

        sealed class Station
        {
            public string Title, Key, Prefab, ExistingItem;
            public ObjectType Type;
            public int Price, Limit;
        }

        /// <summary>In the shop's order, cheapest first.</summary>
        static readonly Station[] Stations =
        {
            new Station { Title = "Bakery", Key = "Bakery", Type = ObjectType.BreadStove, Price = 150, Limit = 2,
                          Prefab = StovesFolder + "/BreadStove/pfBreadStove.prefab",
                          ExistingItem = "Assets/GridBuildingSystem/Scripts/BuildingTypes/Stoves/BreadStove.asset" },
            new Station { Title = "Soup Kitchen", Key = "SoupKitchen", Type = ObjectType.SoupStove, Price = 150, Limit = 2,
                          Prefab = StovesFolder + "/SoupStove/pfSoupStove.prefab",
                          ExistingItem = "Assets/GridBuildingSystem/Scripts/BuildingTypes/Stoves/SoupStove.asset" },
            new Station { Title = "Salad Station", Key = "SaladStation", Type = ObjectType.SaladStation, Price = 200, Limit = 2,
                          Prefab = StovesFolder + "/SaladStation/pfSaladStation.prefab" },
            new Station { Title = "Griddle Station", Key = "GriddleStation", Type = ObjectType.GriddleStation, Price = 250, Limit = 2,
                          Prefab = GriddleStationModel.PrefabPath },
            new Station { Title = "Grill Station", Key = "GrillStation", Type = ObjectType.GrillStation, Price = 300, Limit = 2,
                          Prefab = GrillStationModel.PrefabPath },
        };

        /// <summary>Left to right along the kitchen's back wall.</summary>
        static readonly ObjectType[] Line = { ObjectType.GriddleStation, ObjectType.BreadStove, ObjectType.SoupStove, ObjectType.SaladStation, ObjectType.GrillStation };

        /// <summary>The cooking parts switched on for the icon, so it shows the station at work.</summary>
        static readonly string[] AtWork = { "Egg_", "HashBrown_", "Toast_", "PilotLamp", "Cob_", "Skewer_", "Fish", "Tomato_", "Lamps" };

        [MenuItem("Last Orders/Kitchen/Set Up Kitchen Shop & Line (Current Scene)", priority = 106)]
        static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This puts all five kitchen stations in the shop's Stoves tab (Bakery, Soup Kitchen, Salad Station, Griddle " +
                "Station, Grill Station) as 3 x 2 placed objects kept inside the diner, with ghosts and icons, and lays the diner's " +
                "own five stations out on the build grid along the back wall, registered with the zone.\n\nBuild the corners first. " +
                "The level is left unsaved.",
                "Set Up", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        [MenuItem("Last Orders/Kitchen/Build All Kitchen Stations (Models, Recipes, Shop, Scene)", priority = 107)]
        static void BuildAll()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This rebuilds all five kitchen corners (bakery, soup, salad, griddle, grill), the griddle's and grill's dishes and " +
                "icons, then the shop items and the kitchen line in the open level.\n\nThe level is left unsaved.",
                "Build", "Cancel");

            if (!proceed) return;

            var report = new StringBuilder();
            report.Append(BakeryCornerModel.Apply()).Append('\n');
            report.Append(SoupKitchenModel.Apply()).Append('\n');
            report.Append(SaladStationModel.Apply()).Append('\n');
            report.Append(GriddleStationModel.Apply()).Append('\n');
            report.Append(GrillStationModel.Apply()).Append('\n');
            report.Append(GriddleGrillContent.Apply()).Append('\n');
            report.Append(Apply());
            Debug.Log(report.ToString());
        }

        /// <summary>Makes the shop items, ghosts and icons and sets up the open level, with no dialogs.</summary>
        public static string Apply()
        {
            ContentGenerator.EnsureFolder(Folder);
            var report = new StringBuilder("[Last Orders] Kitchen shop & line:\n");

            PlacementProfile profile = Profile();
            var items = new List<PlacedObjectTypeSO>();
            foreach (Station station in Stations)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(station.Prefab);
                if (prefab == null)
                {
                    report.Append($"  WARNING: no {station.Prefab} - build it first; {station.Title} left out.\n");
                    continue;
                }

                PlacedObjectTypeSO item = Item(station, out string itemNote);
                GameObject ghost = Ghost(station, prefab);
                Sprite icon = Icon(station, prefab);

                Undo.RecordObject(item, "Kitchen shop");
                item.prefab = prefab.transform;
                item.visual = ghost.transform;
                item.placementProfile = profile;
                item.shopIcon = icon;
                EditorUtility.SetDirty(item);
                AssetDatabase.SaveAssetIfDirty(item);

                PointPrefabAt(station, item);
                items.Add(item);
                report.Append($"  {item.name}: {itemNote}, {Width}x{Depth}, {item.PriceAmount} coins, up to {item.maxItems}; ghost {ghost.name}, {icon.name}\n");
            }

            report.Append(ApplyToScene(items));
            return report.ToString();
        }

        // --- The shop items -----------------------------------------------------------------------------

        static PlacementProfile Profile()
        {
            var rule = AssetDatabase.LoadAssetAtPath<KitchenFootingRule>(RulePath);
            if (rule == null)
            {
                rule = ScriptableObject.CreateInstance<KitchenFootingRule>();
                AssetDatabase.CreateAsset(rule, RulePath);
            }

            var profile = AssetDatabase.LoadAssetAtPath<PlacementProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PlacementProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            profile.allowRotation = true;
            profile.requiresFloor = false;
            profile.rules = new PlacementRule[] { rule };
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }

        /// <summary>
        /// The station's shop item: the bread and soup stoves' own items renamed to the station's title
        /// (the button and the cooking panel show an item's name) and refitted, the others made here.
        /// Price, limit and unlock are set the first time this sees an item, so tuning made in the
        /// assets afterwards is kept.
        /// </summary>
        static PlacedObjectTypeSO Item(Station station, out string note)
        {
            // Under its title, beside the old stove item it was renamed from, or still under the old name.
            string folder = station.ExistingItem != null ? Path.GetDirectoryName(station.ExistingItem).Replace('\\', '/') : Folder;
            string path = $"{folder}/{station.Title}.asset";
            var item = AssetDatabase.LoadAssetAtPath<PlacedObjectTypeSO>(path);
            if (item == null && station.ExistingItem != null)
            {
                item = AssetDatabase.LoadAssetAtPath<PlacedObjectTypeSO>(station.ExistingItem);
                if (item != null)
                {
                    string error = AssetDatabase.RenameAsset(station.ExistingItem, station.Title);
                    if (!string.IsNullOrEmpty(error)) Debug.LogWarning($"[Last Orders] Could not rename {station.ExistingItem} to {station.Title}: {error}");
                }
            }

            bool made = item == null;
            if (made)
            {
                item = ScriptableObject.CreateInstance<PlacedObjectTypeSO>();
                AssetDatabase.CreateAsset(item, path);
            }

            bool first = made || item.width != Width || item.height != Depth;

            Undo.RecordObject(item, "Kitchen shop");
            item.nameString = station.Title;
            item.categoryType = ObjectCategory.Stoves;
            item.type = station.Type;
            item.width = Width;
            item.height = Depth;
            if (first)
            {
                item.PriceAmount = station.Price;
                item.maxItems = station.Limit;
                item.requiredLevel = 0;
                item.isUnlocked = true;
                item.Sellable = true;
            }

            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssetIfDirty(item);
            note = made ? "made" : first ? "refitted from the old stove item" : "updated";
            return item;
        }

        /// <summary>The station's placed-object identity pointed at its item, so a copy the shop places and the ones in the level both know what they are.</summary>
        static void PointPrefabAt(Station station, PlacedObjectTypeSO item)
        {
            using var editing = new PrefabUtility.EditPrefabContentsScope(station.Prefab);
            var placed = editing.prefabContentsRoot.GetComponent<PlacedObject_Done>();
            if (placed == null) placed = editing.prefabContentsRoot.AddComponent<PlacedObject_Done>();

            var so = new SerializedObject(placed);
            so.FindProperty("placedObjectTypeSO").objectReferenceValue = item;
            so.FindProperty("shopItemType").intValue = (int)station.Type;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- The ghost and the icon ---------------------------------------------------------------------

        /// <summary>
        /// The ghost: the grid's footprint marker sized to 3 x 2 cells and the station's meshes as they
        /// stand idle - no station, no animation, no effects, no colliders. A live station would
        /// register itself with the kitchen as the ghost was made.
        /// </summary>
        static GameObject Ghost(Station station, GameObject prefab)
        {
            string path = $"{Folder}/pf{station.Key}Ghost.prefab";
            var root = new GameObject($"pf{station.Key}Ghost");

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
                        copy.transform.localPosition = new Vector3(Width * Cell * 0.5f, 0f, Depth * Cell * 0.5f);
                        copy.transform.localScale = new Vector3(Width * Cell, source.localScale.y, Depth * Cell);
                    }
                    else
                    {
                        copy.transform.localPosition = source.localPosition;
                        copy.transform.localScale = source.localScale;
                    }
                }

                GameObject model = Object.Instantiate(prefab, root.transform);
                model.name = "Station";
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

        /// <summary>A copy of a station stripped to what it looks like idle: what is switched off, the effects, every script and collider go.</summary>
        static void MeshesOnly(GameObject model)
        {
            var doomed = new List<GameObject>();
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t == model.transform) continue;
                if (!t.gameObject.activeSelf || t.GetComponent<ParticleSystem>() != null) doomed.Add(t.gameObject);
            }

            foreach (GameObject go in doomed)
                if (go != null) Object.DestroyImmediate(go);

            foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        static Sprite Icon(Station station, GameObject prefab)
        {
            Texture2D render = Render(prefab);
            try
            {
                return CropIcons.SaveIcon(Folder, $"Icon_{station.Key}", CropIcons.Compose(render));
            }
            finally
            {
                Object.DestroyImmediate(render);
            }
        }

        /// <summary>
        /// The station drawn on its own with its cooking switched on, seen from in front, a little to the
        /// right and above, as the game's camera sees it: square, transparent round it.
        /// </summary>
        static Texture2D Render(GameObject prefab, int size = 1024)
        {
            GameObject model = Object.Instantiate(prefab);
            var camera = new GameObject("__StationIconCamera").AddComponent<Camera>();
            RenderTexture target = null;

            try
            {
                model.transform.SetPositionAndRotation(new Vector3(0f, -400f, 0f), Quaternion.identity);
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                    foreach (string part in AtWork)
                        if (t.name.StartsWith(part)) t.gameObject.SetActive(true);

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

                Vector3 toCamera = new Vector3(0.5f, 0.85f, -1f).normalized;
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

        // --- The open level -----------------------------------------------------------------------------

        static string ApplyToScene(List<PlacedObjectTypeSO> items)
        {
            var report = new StringBuilder();
            Scene scene = SceneManager.GetActiveScene();

            var database = Object.FindFirstObjectByType<ObjectDatabaseHandler>(FindObjectsInactive.Include);
            if (database == null) report.Append("  WARNING: the open level has no ObjectDatabaseHandler (no shop) - nothing listed.\n");
            else
            {
                Undo.RecordObject(database, "Kitchen shop");
                var so = new SerializedObject(database);
                SerializedProperty databases = so.FindProperty("objectsDatabases");
                SerializedProperty entry = null;
                for (int i = 0; i < databases.arraySize; i++)
                {
                    SerializedProperty candidate = databases.GetArrayElementAtIndex(i);
                    if (candidate.FindPropertyRelative("type").intValue == (int)ObjectCategory.Stoves) entry = candidate;
                }

                if (entry == null)
                {
                    databases.InsertArrayElementAtIndex(0);
                    entry = databases.GetArrayElementAtIndex(0);
                    entry.FindPropertyRelative("type").intValue = (int)ObjectCategory.Stoves;
                }

                SerializedProperty list = entry.FindPropertyRelative("placedObjectTypeSOList");
                list.arraySize = items.Count;
                for (int i = 0; i < items.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
                so.ApplyModifiedProperties();
                report.Append($"  Shop: Stoves lists {string.Join(", ", items.ConvertAll(it => it.name))}.\n");
            }

            report.Append(LayLine(items));
            EditorSceneManager.MarkSceneDirty(scene);
            report.Append("  The level is unsaved.\n");
            return report.ToString();
        }

        /// <summary>
        /// The five stations along the back wall on whole cells, the line centred on the diner, each
        /// footprint's back on the last whole cell line before the wall; any station missing from the
        /// level is added. Each is registered with the zone as a placed object not yet placed, so the
        /// zone hands it to the grid where it stands when the game starts.
        /// </summary>
        static string LayLine(List<PlacedObjectTypeSO> items)
        {
            var report = new StringBuilder();
            var floorObject = Object.FindFirstObjectByType<RestaurantFloor>();
            var floor = floorObject != null ? floorObject.GetComponent<Renderer>() : null;
            if (floor == null) return "  WARNING: no diner floor in the open level - the kitchen line is not laid out.\n";

            Bounds b = floor.bounds;
            float back = Mathf.Floor(b.max.z / Cell + 0.0001f) * Cell - Depth * Cell;
            float start = Mathf.Round((b.center.x - Line.Length * Width * Cell * 0.5f) / Cell) * Cell;
            if (start < b.min.x) start = Mathf.Ceil(b.min.x / Cell) * Cell;

            var zone = Object.FindFirstObjectByType<Zone>(FindObjectsInactive.Include);
            SerializedObject zoneSo = zone != null ? new SerializedObject(zone) : null;
            SerializedProperty placedList = zoneSo?.FindProperty("placedObject");

            for (int i = 0; i < Line.Length; i++)
            {
                PlacedObjectTypeSO item = items.Find(it => it.type == Line[i]);
                if (item == null) continue;

                var at = new Vector3(start + i * Width * Cell, 0f, back);
                GameObject instance = Instance(item.prefab.gameObject, out bool added);
                Undo.RecordObject(instance.transform, "Kitchen line");
                instance.transform.SetPositionAndRotation(at, Quaternion.identity);

                var placed = instance.GetComponent<PlacedObject_Done>();
                var so = new SerializedObject(placed);
                so.FindProperty("placedObjectTypeSO").objectReferenceValue = item;
                so.FindProperty("shopItemType").intValue = (int)item.type;
                so.FindProperty("dir").intValue = (int)PlacedObjectTypeSO.Dir.Down;
                so.FindProperty("IsPlaced").boolValue = false;
                so.ApplyModifiedProperties();

                string registered = "";
                if (placedList != null)
                {
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

                report.Append($"  {instance.name} {(added ? "added" : "moved")} to cells x {at.x:0}-{at.x + Width * Cell:0}, z {at.z:0}-{at.z + Depth * Cell:0}{registered}\n");
            }

            zoneSo?.ApplyModifiedProperties();
            if (zone == null) report.Append("  WARNING: no Zone in the open level - the stations are not registered as placed objects.\n");
            return report.ToString();
        }

        /// <summary>The level's copy of <paramref name="prefab"/>, or a new one if it has none.</summary>
        static GameObject Instance(GameObject prefab, out bool added)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            foreach (var station in Object.FindObjectsByType<CookeryStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(station.gameObject);
                if (root == null) continue;
                var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
                if (source != null && AssetDatabase.GetAssetPath(source) == path)
                {
                    added = false;
                    return root;
                }
            }

            added = true;
            var made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
            Undo.RegisterCreatedObjectUndo(made, "Kitchen line");
            return made;
        }
    }
}
