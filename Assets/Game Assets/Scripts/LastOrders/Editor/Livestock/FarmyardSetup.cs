using System;
using System.Collections.Generic;
using System.Text;
using FoodStreet.Placement;
using LastOrders.Data;
using LastOrders.Livestock;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using B = LastOrders.EditorTools.BuildingKit;
using F = LastOrders.EditorTools.FarmBuildingKit;
using S = LastOrders.EditorTools.SoupKitchenModel;
using V = FoodStreet.Visitors.EditorTools.VehicleKit;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The farmyard in the open scene: a chicken coop and a cow shelter, sold in the shop's Farming
    /// tab beside the plant pots and set down on the build grid like a table or a shooter, and one of
    /// each already standing in the compound's south-west corner - the open ground between the
    /// hospital tent and the gate tower, inside the south wall - on a trodden yard with a dirt path up
    /// to the kitchen garden, hay, feed sacks, a hand pump and a wheelbarrow about it.
    ///
    /// The shop items are made if missing and kept pointing at the latest prefab, ghost and icon;
    /// price, limit and level are only set when an item is first made. The scene is left unsaved.
    /// </summary>
    internal static class FarmyardSetup
    {
        const string ShopFolder = LivestockKit.ContentFolder + "/Shop";
        const string RulePath = ShopFolder + "/Rule_FarmFooting.asset";
        const string ProfilePath = ShopFolder + "/Placement_Farm.asset";
        const string AreaMaterialPath = "Assets/GridBuildingSystem/Materials/GreyTransparent.mat";
        const string AnchorMaterialPath = "Assets/GridBuildingSystem/Materials/Anchor.mat";
        const string PickupPath = "Assets/Game Assets/Content/LastOrders/Farming/Effects/ItemPickup.prefab";
        const string DressingMeshPath = LivestockKit.MeshFolder + "/Farmyard/Farmyard_Dressing.asset";
        const int IconLayer = 31;
        const string RootName = "Farmyard";

        sealed class Building
        {
            public string Title, Key, Prefab;
            public ObjectType Type;
            public int Width, Depth, Price, Limit, Level;

            /// <summary>Where the starter copy stands: the footprint's minimum corner, world metres on the 1 m grid.</summary>
            public Vector2Int Corner;

            /// <summary>Already waiting in the starter copy when the level starts.</summary>
            public int Stocked;
        }

        static readonly Building[] Buildings =
        {
            new Building
            {
                Title = "Chicken Coop", Key = "ChickenCoop", Prefab = ChickenCoopModel.PrefabPath, Type = ObjectType.ChickenCoop,
                Width = ChickenCoopModel.Width, Depth = ChickenCoopModel.Depth, Price = 120, Limit = 3, Level = 0,
                Corner = new Vector2Int(5, 2), Stocked = 0,
            },
            new Building
            {
                Title = "Cow Shelter", Key = "CowShelter", Prefab = CowShelterModel.PrefabPath, Type = ObjectType.CowShelter,
                Width = CowShelterModel.Width, Depth = CowShelterModel.Depth, Price = 200, Limit = 2, Level = 0,
                Corner = new Vector2Int(-2, 2), Stocked = 0,
            },
        };

        // --- Menus ---------------------------------------------------------------------------------------

        [MenuItem("Last Orders/Farming/Set Up Farmyard (Current Scene)", priority = 89)]
        static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Last Orders] Leave play mode before setting up the farmyard.");
                return;
            }

            Debug.Log(Apply(rebuild: false));
        }

        [MenuItem("Last Orders/Farming/Rebuild Farm Animals & Buildings (Models, Prefabs, Shop)", priority = 89)]
        static void Rebuild()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Last Orders] Leave play mode before rebuilding the farmyard.");
                return;
            }

            Debug.Log(Apply(rebuild: true));
        }

        /// <summary>Builds what is missing (or everything, <paramref name="rebuild"/>), the shop items, and the open scene's farmyard.</summary>
        internal static string Apply(bool rebuild)
        {
            var report = new StringBuilder("[Last Orders] Farmyard:\n");

            if (rebuild || AssetDatabase.LoadAssetAtPath<ItemDataSO>(LivestockContent.EggPath) == null)
                report.Append(LivestockContent.Apply()).Append('\n');

            var egg = AssetDatabase.LoadAssetAtPath<ItemDataSO>(LivestockContent.EggPath);
            var milk = AssetDatabase.LoadAssetAtPath<ItemDataSO>(LivestockContent.MilkPath);
            var corn = AssetDatabase.LoadAssetAtPath<ItemDataSO>(LivestockContent.CornPath);
            var wheat = AssetDatabase.LoadAssetAtPath<ItemDataSO>(LivestockContent.WheatPath);

            if (rebuild || AssetDatabase.LoadAssetAtPath<GameObject>(ChickenModel.PrefabPath(ChickenModel.Breed.Cream)) == null)
                report.Append("  ").Append(ChickenModel.Apply()).Append('\n');
            if (rebuild || AssetDatabase.LoadAssetAtPath<GameObject>(CowModel.PrefabPath) == null)
                report.Append("  ").Append(CowModel.Apply()).Append('\n');
            if (rebuild || AssetDatabase.LoadAssetAtPath<GameObject>(ChickenCoopModel.PrefabPath) == null)
                report.Append("  ").Append(ChickenCoopModel.Apply(egg, corn)).Append('\n');
            if (rebuild || AssetDatabase.LoadAssetAtPath<GameObject>(CowShelterModel.PrefabPath) == null)
                report.Append("  ").Append(CowShelterModel.Apply(milk, wheat)).Append('\n');

            ContentGenerator.EnsureFolder(ShopFolder);
            PlacementProfile profile = Profile();
            var items = new List<PlacedObjectTypeSO>();
            foreach (Building building in Buildings)
            {
                PlacedObjectTypeSO item = ShopItem(building, profile, out bool made);
                items.Add(item);
                report.Append($"  Shop item '{building.Title}' {(made ? "made" : "updated")}: {item.PriceAmount} coins, up to {item.maxItems}, {building.Width} x {building.Depth} cells.\n");
            }

            report.Append(ApplyToScene(items));
            return report.ToString();
        }

        // --- The shop items -----------------------------------------------------------------------------

        static PlacementProfile Profile()
        {
            var rule = AssetDatabase.LoadAssetAtPath<FarmFootingRule>(RulePath);
            if (rule == null)
            {
                rule = ScriptableObject.CreateInstance<FarmFootingRule>();
                AssetDatabase.CreateAsset(rule, RulePath);
            }

            var profile = AssetDatabase.LoadAssetAtPath<PlacementProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PlacementProfile>();
                profile.allowRotation = true;
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            profile.rules = new PlacementRule[] { rule };
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }

        static PlacedObjectTypeSO ShopItem(Building building, PlacementProfile profile, out bool made)
        {
            string path = $"{ShopFolder}/{building.Title}.asset";
            var item = AssetDatabase.LoadAssetAtPath<PlacedObjectTypeSO>(path);
            made = item == null;

            if (made)
            {
                item = ScriptableObject.CreateInstance<PlacedObjectTypeSO>();
                item.PriceAmount = building.Price;
                item.maxItems = building.Limit;
                item.requiredLevel = building.Level;
                item.isUnlocked = building.Level <= 0;
                item.Sellable = true;
                AssetDatabase.CreateAsset(item, path);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(building.Prefab);
            Sprite icon = Icon(building, prefab);

            item.nameString = building.Title;
            item.categoryType = ObjectCategory.Farming;
            item.type = building.Type;
            item.width = building.Width;
            item.height = building.Depth;
            item.buildCategory = BuildCategory.Production;
            item.placementProfile = profile;
            item.shopIcon = icon;
            item.previewImage = icon;
            item.prefab = prefab.transform;
            item.visual = Ghost(building, prefab).transform;
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssetIfDirty(item);

            // The placed copy and its item refer to each other.
            using (var editing = new PrefabUtility.EditPrefabContentsScope(building.Prefab))
            {
                var so = new SerializedObject(editing.prefabContentsRoot.GetComponent<PlacedObject_Done>());
                so.FindProperty("placedObjectTypeSO").objectReferenceValue = item;
                so.FindProperty("shopItemType").intValue = (int)building.Type;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return item;
        }

        /// <summary>The ghost: the building and its animals as meshes only, over the grid's footprint marker.</summary>
        static GameObject Ghost(Building building, GameObject prefab)
        {
            string path = $"{ShopFolder}/pf{building.Key}Visual.prefab";
            var root = new GameObject($"pf{building.Key}Visual");

            try
            {
                var area = GameObject.CreatePrimitive(PrimitiveType.Cube);
                area.name = "Area";
                area.transform.SetParent(root.transform, false);
                area.transform.localPosition = new Vector3(building.Width * 0.5f, 0f, building.Depth * 0.5f);
                area.transform.localScale = new Vector3(building.Width, 0.1f, building.Depth);
                Object.DestroyImmediate(area.GetComponent<Collider>());
                var areaRenderer = area.GetComponent<MeshRenderer>();
                areaRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(AreaMaterialPath);
                areaRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var anchor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                anchor.name = "Anchor";
                anchor.transform.SetParent(root.transform, false);
                anchor.transform.localScale = Vector3.one * 0.1f;
                Object.DestroyImmediate(anchor.GetComponent<Collider>());
                anchor.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(AnchorMaterialPath);

                GameObject model = Object.Instantiate(prefab, root.transform);
                model.name = "Building";
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

        /// <summary>What a building looks like and nothing else: no eggs or churns yet, no effects, no scripts, no colliders.</summary>
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

            foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(behaviour is PlacedObject_Done)) Object.DestroyImmediate(behaviour);
            foreach (PlacedObject_Done placed in model.GetComponentsInChildren<PlacedObject_Done>(true)) Object.DestroyImmediate(placed);
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        static Sprite Icon(Building building, GameObject prefab)
        {
            Texture2D render = Render(prefab);
            try
            {
                return CropIcons.SaveIcon(ShopFolder, $"Icon_{building.Key}", CropIcons.Compose(render));
            }
            finally
            {
                Object.DestroyImmediate(render);
            }
        }

        /// <summary>The building alone against nothing, seen from the front and above as the game camera sees it: square, transparent round it.</summary>
        static Texture2D Render(GameObject prefab, int size = 1024)
        {
            GameObject model = Object.Instantiate(prefab);
            var camera = new GameObject("__FarmIconCamera").AddComponent<Camera>();
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

                // From the front (-Z) and to the right (+X), as the game camera looks at it.
                Vector3 toCamera = new Vector3(0.5f, 0.85f, -0.75f).normalized;
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

        // --- The scene ----------------------------------------------------------------------------------

        static string ApplyToScene(List<PlacedObjectTypeSO> items)
        {
            var report = new StringBuilder();
            var database = Object.FindFirstObjectByType<ObjectDatabaseHandler>(FindObjectsInactive.Include);
            if (database == null) return "  WARNING: the open scene has no ObjectDatabaseHandler (no shop) - nothing placed.\n";

            report.Append(ListInFarmingTab(database, items));
            report.Append(Inventory());

            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Farmyard");
            }

            report.Append(Dressing(root));

            var zone = Object.FindFirstObjectByType<Zone>(FindObjectsInactive.Include);
            for (int i = 0; i < Buildings.Length; i++) report.Append(Starter(root.transform, Buildings[i], zone));

            EditorSceneManager.MarkSceneDirty(root.scene);
            report.Append("  Scene left unsaved.\n");
            return report.ToString();
        }

        /// <summary>The buildings listed after whatever the shop's Farming tab already sells.</summary>
        static string ListInFarmingTab(ObjectDatabaseHandler database, List<PlacedObjectTypeSO> items)
        {
            Undo.RecordObject(database, "Farmyard");
            var so = new SerializedObject(database);
            SerializedProperty databases = so.FindProperty("objectsDatabases");
            SerializedProperty entry = null;
            for (int i = 0; i < databases.arraySize; i++)
            {
                SerializedProperty candidate = databases.GetArrayElementAtIndex(i);
                if (candidate.FindPropertyRelative("type").intValue == (int)ObjectCategory.Farming) entry = candidate;
            }

            if (entry == null) return "  WARNING: the shop has no Farming entry - the buildings are not for sale here.\n";

            SerializedProperty list = entry.FindPropertyRelative("placedObjectTypeSOList");
            int added = 0;
            foreach (PlacedObjectTypeSO item in items)
            {
                bool listed = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == item) listed = true;
                if (listed) continue;

                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
                added++;
            }

            so.ApplyModifiedProperties();
            return $"  Shop: Farming tab {(added > 0 ? $"now sells the coop and the cow shelter ({list.arraySize} items)" : "already sells them")}.\n";
        }

        /// <summary>
        /// Room in the storehouse: the inventory's limit counts kinds of item, and the farmyard adds four
        /// (eggs, milk, two dishes) to what was already one short of it. Also the flying pickups the crops
        /// were given, if the scene still has the old ones.
        /// </summary>
        static string Inventory()
        {
            var report = new StringBuilder();
            var inventory = Object.FindFirstObjectByType<InventorySystem>(FindObjectsInactive.Include);
            if (inventory != null && inventory.inventorySize < 30)
            {
                Undo.RecordObject(inventory, "Farmyard");
                int was = inventory.inventorySize;
                inventory.inventorySize = 30;
                EditorUtility.SetDirty(inventory);
                report.Append($"  Storehouse holds 30 kinds of item (was {was}).\n");
            }

            var items = Object.FindFirstObjectByType<ItemController>(FindObjectsInactive.Include);
            var pickup = AssetDatabase.LoadAssetAtPath<ItemReceiver>(PickupPath);
            if (items != null && pickup != null && items.itemReceiver != pickup)
            {
                Undo.RecordObject(items, "Farmyard");
                items.itemReceiver = pickup;
                EditorUtility.SetDirty(items);
                report.Append("  ItemController throws the new ItemPickup (eggs, milk and crops fly into the storehouse).\n");
            }

            return report.ToString();
        }

        /// <summary>One of the building standing in the farmyard, on the grid and counted against the shop's limit.</summary>
        static string Starter(Transform root, Building building, Zone zone)
        {
            string name = $"{building.Key}_Starter";
            Transform existing = root.Find(name);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(building.Prefab);

            GameObject copy;
            if (existing != null) copy = existing.gameObject;
            else
            {
                copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                copy.name = name;
                Undo.RegisterCreatedObjectUndo(copy, "Farmyard");
            }

            copy.transform.SetPositionAndRotation(new Vector3(building.Corner.x, 0f, building.Corner.y), Quaternion.identity);

            var pen = copy.GetComponent<LivestockPen>();
            var penSo = new SerializedObject(pen);
            // Nothing waiting and nothing to eat: the first thing either needs is feeding.
            penSo.FindProperty("startingStock").intValue = building.Stocked;
            penSo.FindProperty("startingFeed").intValue = 0;
            penSo.ApplyModifiedProperties();

            string zoneNote = "";
            if (zone != null)
            {
                var placed = copy.GetComponent<PlacedObject_Done>();
                var zoneSo = new SerializedObject(zone);
                SerializedProperty list = zoneSo.FindProperty("placedObject");
                bool listed = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == placed) listed = true;
                if (!listed)
                {
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = placed;
                    zoneSo.ApplyModifiedProperties();
                    zoneNote = ", registered with the zone";
                }
            }

            return $"  {building.Title} standing at ({building.Corner.x}, {building.Corner.y})..({building.Corner.x + building.Width}, {building.Corner.y + building.Depth}){zoneNote}.\n";
        }

        // --- The yard round them ------------------------------------------------------------------------

        static string Dressing(GameObject root)
        {
            Mesh mesh = Yard().Save(DressingMeshPath, "Farmyard_Dressing");
            Transform dressing = LivestockKit.Part(root.transform, "Dressing", Vector3.zero, mesh);
            dressing.GetComponent<MeshRenderer>().receiveShadows = true;
            return $"  Farmyard ground, path and props: {mesh.vertexCount} vertices.\n";
        }

        /// <summary>Within the trodden ground: an irregular blot round both buildings, and the path up to the kitchen garden.</summary>
        static float Trodden(float x, float z)
        {
            // The yard: a rounded rectangle round the two pens, its edge wandering.
            float wobble = 0.35f * Mathf.Sin(x * 1.3f + z * 0.7f) + 0.25f * Mathf.Sin(z * 2.1f - x * 0.9f);
            float dx = Mathf.Max(0f, Mathf.Abs(x - 3.5f) - 6.2f), dz = Mathf.Max(0f, Mathf.Abs(z - 4.2f) - 2.6f);
            float yard = 1.1f + wobble - Mathf.Sqrt(dx * dx + dz * dz);

            // The path north, wandering a little, to the plant beds.
            float centre = 4.1f + 0.35f * Mathf.Sin(z * 0.45f) + 0.15f * Mathf.Sin(z * 1.3f);
            float path = z > 6f && z < 20.6f ? 0.55f + 0.1f * Mathf.Sin(z * 2.7f) - Mathf.Abs(x - centre) : -1f;
            return Mathf.Max(yard, path);
        }

        static B.Model Yard()
        {
            var m = new B.Model();
            Vector3 seed = V.Seed(77);

            // Raised just off the grass where trodden, sunk under it where not: the grass draws the edge.
            m.Heightfield((p, n) =>
            {
                Color c = Color.Lerp(F.DirtDark, F.Dirt, V.Noise(p * 1.9f + seed));
                c = Color.Lerp(c, F.Straw, V.Band(V.Noise(p * 4.5f + seed * 2f), 0.62f, 0.8f) * 0.4f);
                return c * (0.97f + 0.05f * V.Noise(p * 13f));
            }, new Rect(-3.5f, -0.4f, 14.5f, 21.2f), 150, (x, z) =>
            {
                // Height runs straight through the edge rather than jumping at it, so the grass
                // cuts the dirt along a smooth line instead of from vertex to vertex in steps.
                float t = Trodden(x, z);
                return 0.008f * Mathf.Clamp(t / 0.25f, -1.5f, 1f);
            });

            // Hay stacked by the coop's side.
            V.Bale(m, new Vector3(10.3f, 0f, 2.6f), new Vector3(0.48f, 0.4f, 0.82f), 3f, 81);
            V.Bale(m, new Vector3(10.35f, 0f, 3.5f), new Vector3(0.48f, 0.4f, 0.82f), -5f, 82);
            V.Bale(m, new Vector3(10.3f, 0.4f, 3.05f), new Vector3(0.48f, 0.4f, 0.82f), 8f, 83);

            // Feed sacks on a pallet behind it.
            Pallet(m, new Vector3(9.9f, 0f, 5.7f));
            B.Sack(m, new Vector3(9.7f, 0.12f, 5.6f), 0.2f, 0.55f, 0.04f, 91);
            B.Sack(m, new Vector3(10.15f, 0.12f, 5.8f), 0.19f, 0.5f, -0.05f, 92);
            B.Sack(m, new Vector3(9.95f, 0.12f, 6.05f), 0.18f, 0.48f, 0.06f, 93);

            // A hand pump between the pens, a bucket under its spout.
            Pump(m, new Vector3(4.1f, 0f, 6.2f));

            // A wheelbarrow of straw left out front.
            Wheelbarrow(m, new Vector3(3.9f, 0f, 1.2f), 28f);

            // Straw wisps blown about the yard.
            Vector2[] tufts = { new Vector2(3.2f, 2.4f), new Vector2(4.6f, 3.6f), new Vector2(2.9f, 0.6f), new Vector2(8.6f, 1.2f), new Vector2(-1.2f, 1.1f), new Vector2(4.4f, 9.5f) };
            for (int i = 0; i < tufts.Length; i++) F.StrawTuft(m, new Vector3(tufts[i].x, 0.012f, tufts[i].y), 0.16f, i + 30);
            return m;
        }

        static void Pallet(B.Model m, Vector3 foot)
        {
            for (int i = 0; i < 5; i++)
                m.Box(B.Plank(600 + i) * 1.05f, foot + new Vector3(-0.42f + i * 0.21f, 0.105f, 0f), new Vector3(0.17f, 0.025f, 1f), 0.005f, 1);
            for (int k = -1; k <= 1; k++)
                m.Box(B.Walnut, foot + new Vector3(0f, 0.05f, k * 0.42f), new Vector3(1f, 0.085f, 0.09f), 0.01f, 1);
        }

        /// <summary>A cast-iron hand pump on a stone slab, its long handle up, a galvanised bucket under the spout.</summary>
        static void Pump(B.Model m, Vector3 foot)
        {
            m.Box(B.Stone, foot + new Vector3(0f, 0.06f, 0f), new Vector3(0.7f, 0.12f, 0.6f), 0.04f, Quaternion.Euler(0f, 6f, 0f), 2);
            Color iron = B.Hex("3F4446");
            Vector3 body = foot + new Vector3(0f, 0.12f, 0.12f);
            m.Lathe((p, n) => iron * (n.y < -0.3f ? 0.7f : 1f) * (0.95f + 0.08f * V.Noise(p * 9f)), body, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.11f, 0f), new Vector2(0.11f, 0.06f), new Vector2(0.075f, 0.09f), new Vector2(0.07f, 0.7f),
                new Vector2(0.085f, 0.74f), new Vector2(0.085f, 0.8f), new Vector2(0.05f, 0.86f), new Vector2(0f, 0.88f),
            }, 14);

            // The spout, out over the bucket.
            m.Tube((p, n) => iron, body + new Vector3(0f, 0.6f, 0f), body + new Vector3(0f, 0.55f, -0.25f), 0.035f, 0.03f, 10);
            m.Tube(iron, body + new Vector3(0f, 0.55f, -0.25f), body + new Vector3(0f, 0.48f, -0.29f), 0.03f, 10);

            // The handle, raised.
            V.Sweep(m, iron, new[] { body + new Vector3(0f, 0.84f, 0.02f), body + new Vector3(0f, 1.0f, 0.15f), body + new Vector3(0f, 1.12f, 0.4f) }, 0.018f, 8, 5);
            S.Ball(m, (p, n) => B.Walnut, body + new Vector3(0f, 1.13f, 0.43f), new Vector3(0.03f, 0.03f, 0.06f), Quaternion.Euler(-30f, 0f, 0f), 10, 8);

            Vector3 pail = foot + new Vector3(0f, 0.12f, -0.17f);
            m.Lathe((p, n) => (n.y > 0.6f ? F.GalvanisedTop : F.Galvanised) * (0.92f + 0.08f * V.Noise(p * 12f)) * (n.y < -0.3f ? 0.7f : 1f), pail,
                    new List<Vector2>
                    {
                        new Vector2(0f, 0f), new Vector2(0.1f, 0f), new Vector2(0.13f, 0.24f), new Vector2(0.135f, 0.25f), new Vector2(0.125f, 0.25f),
                        new Vector2(0.1f, 0.03f), new Vector2(0f, 0.03f),
                    }, 14);
            S.Disc(m, (p, n) => F.Water, pail + Vector3.up * 0.2f, Vector3.up, 0.118f, 14);
        }

        /// <summary>A wheelbarrow: a tray of straw on one wheel at the front, two handles and legs at the back.</summary>
        static void Wheelbarrow(B.Model m, Vector3 foot, float yaw)
        {
            V.Placed(m, foot, Quaternion.Euler(0f, yaw, 0f), () =>
            {
                Color tray = B.Hex("5F7480");
                var shape = new SmoothMesh(1);
                shape.RoundedBox(0, new Vector3(0f, 0.42f, 0.05f), new Vector3(0.6f, 0.28f, 0.75f), 0.08f, Quaternion.Euler(-8f, 0f, 0f), 3);
                m.Add(shape, V.Worn(tray, 5, 0.6f));
                S.Ball(m, (p, n) => Color.Lerp(F.Straw, F.StrawLight, V.Noise(p * 20f)) * (n.y < 0f ? 0.75f : 1f), new Vector3(0f, 0.55f, 0.05f),
                       new Vector3(0.27f, 0.08f, 0.34f), Quaternion.Euler(-8f, 0f, 0f), 12, 8);

                // The wheel at the front, on its axle.
                V.Placed(m, new Vector3(0f, 0.17f, 0.55f), Quaternion.Euler(0f, 0f, 90f), () =>
                {
                    m.Lathe((p, n) => V.Rubber, Vector3.down * 0.035f, new List<Vector2>
                    {
                        new Vector2(0f, 0f), new Vector2(0.12f, 0f), new Vector2(0.165f, 0.015f), new Vector2(0.17f, 0.035f), new Vector2(0.165f, 0.055f),
                        new Vector2(0.12f, 0.07f), new Vector2(0f, 0.07f),
                    }, 16);
                });

                for (int side = -1; side <= 1; side += 2)
                {
                    m.Tube(B.Walnut, new Vector3(side * 0.06f, 0.17f, 0.55f), new Vector3(side * 0.25f, 0.42f, -0.65f), 0.022f, 8);
                    m.Tube(B.Walnut, new Vector3(side * 0.22f, 0.36f, -0.35f), new Vector3(side * 0.22f, 0f, -0.38f), 0.018f, 8);
                }
            });
        }
    }
}
