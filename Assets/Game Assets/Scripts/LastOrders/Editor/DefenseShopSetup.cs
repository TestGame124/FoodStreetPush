using System;
using System.Collections.Generic;
using System.Text;
using LastOrders.Data;
using FoodStreet.Placement;
using LastOrders.Defense;
using LastOrders.Perimeter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Puts the shooters in the shop, beside the plant pot, the tables and the stoves: a Defense tab
    /// listing the Tomato, Corn, Plate and Cabbage Shooters, each bought with Coins and dragged onto
    /// the build grid like any other buildable.
    ///
    /// For each shooter it builds what the grid building system needs - all of it in
    /// <see cref="Folder"/>:
    ///
    /// - The shop item, a <see cref="PlacedObjectTypeSO"/> named as the shop shows it (the button's
    ///   title is the asset's name), 2 x 2 cells, in the new <see cref="ObjectCategory.Defense"/>.
    ///   Its price, its limit and the level it unlocks at are filled in only when it is created, to
    ///   be tuned by hand afterwards.
    /// - What is placed: a corner-pivoted root with a <see cref="PlacedObject_Done"/>, the shooter's
    ///   own prefab nested at the middle of its footprint as a post the shop sells
    ///   (<see cref="DefensePost.ConfigureForShop"/>) - built as it is bought, its id taken from where
    ///   it is set down, standing down while the shop has it put away. Rebuilding a shooter's model
    ///   rebuilds every shop copy with it.
    /// - The ghost dragged over the grid: the shooter's meshes only, on the grid's own footprint
    ///   marker, so the placement tint shows on it.
    /// - A bastion for the wall (<see cref="BastionPath"/>, one prefab nested in every shooter): set
    ///   down over the compound wall, a shooter goes up on it instead of standing in the wall
    ///   (<see cref="WallMount"/>), and the ghost shows it there before it is bought. A placement
    ///   profile with <see cref="DefenseFootingRule"/> keeps it out of gateways, off wall sites and
    ///   pads, off the standing defenses, and off a wall it would only half sit on.
    /// - Its icon, rendered from the model and finished like the item icons (outline, soft shadow),
    ///   on the button through <see cref="PlacedObjectTypeSO.shopIcon"/>.
    ///
    /// In the open scene it then lists them under a Defense entry of the shop's database and adds the
    /// tab: a copy of the Props tab button and its content panel, wired to each other. Idempotent;
    /// the scene is left unsaved.
    ///
    /// Bought here, a shooter is paid for in the wallet's Coins, as everything in the shop is; its
    /// upgrades are still bought by tapping it, in Coins or premium, from its definition.
    /// </summary>
    public static class DefenseShopSetup
    {
        public const string Folder = "Assets/Game Assets/Content/LastOrders/Defense/Shop";

        const string WellPath = "Assets/GridBuildingSystem/Scripts/BuildingTypes/Props/WellProp.asset";

        /// <summary>The bastion a shooter stands on when it is put on the wall, built once for all of them.</summary>
        public const string BastionPath = Folder + "/pfShopBastion.prefab";

        const string BastionMeshFolder = Folder + "/Bastion";
        const string ProfilePath = Folder + "/Placement_Defense.asset";
        const string RulePath = Folder + "/Rule_DefenseFooting.asset";
        const string TabName = "Defense";
        const string ContentName = "DefenseContent";

        /// <summary>A layer nothing else draws on, for rendering an icon on its own.</summary>
        const int IconLayer = 31;

        const int Cells = 2;

        sealed class Shooter
        {
            public string Title, Key;
            public ObjectType Type;
            public int Price, Limit, Level;
            public Func<DefenseDefinition> Definition;
            public Func<DefenseDefinition, GameObject> Prefab;
        }

        /// <summary>
        /// In the order the tab lists them. Placeholder prices in wallet Coins, pitched against a table
        /// (100): the quick, light Tomato Shooter is the cheapest, the Cabbage Shooter's splash the dearest.
        /// </summary>
        static Shooter[] Shooters() => new[]
        {
            new Shooter
            {
                Title = "Tomato Shooter", Key = "TomatoShooter", Type = ObjectType.TomatoShooter, Price = 60, Limit = 4, Level = 0,
                Definition = DefenseContentGenerator.GenerateTomatoShooter,
                Prefab = d => TomatoShooterModel.Ensure(d, DefenseContentGenerator.LoadTomatoShooterMotion()),
            },
            new Shooter
            {
                Title = "Corn Shooter", Key = "CornShooter", Type = ObjectType.CornShooter, Price = 80, Limit = 4, Level = 0,
                Definition = DefenseContentGenerator.GenerateCornShooter,
                Prefab = d => CornShooterModel.Ensure(d, DefenseContentGenerator.LoadCornShooterMotion()),
            },
            new Shooter
            {
                Title = "Plate Shooter", Key = "PlateShooter", Type = ObjectType.PlateShooter, Price = 100, Limit = 4, Level = 0,
                Definition = DefenseContentGenerator.GeneratePlateShooter,
                Prefab = d => PlateShooterModel.Ensure(d, DefenseContentGenerator.LoadPlateShooterMotion()),
            },
            new Shooter
            {
                Title = "Cabbage Shooter", Key = "CabbageShooter", Type = ObjectType.CabbageShooter, Price = 120, Limit = 4, Level = 0,
                Definition = DefenseContentGenerator.GenerateCabbageShooter,
                Prefab = d => CabbageShooterModel.Ensure(d, DefenseContentGenerator.LoadCabbageShooterMotion()),
            },
        };

        [MenuItem("Last Orders/Defense/Set Up Defense Shop (Current Scene)", priority = 60)]
        static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Last Orders] Leave Play mode before setting up the defense shop.");
                return;
            }

            Debug.Log(Apply());
        }

        /// <summary>Builds every shop item and wires the open scene's shop to them. No dialogs; the scene is left unsaved.</summary>
        public static string Apply()
        {
            var report = new StringBuilder("[Last Orders] Defense shop:\n");
            PlacedObjectTypeSO[] items = BuildItems(report);
            report.Append(ApplyToScene(items));
            return report.ToString();
        }

        /// <summary>Every shooter's shop item, built or brought up to date.</summary>
        public static PlacedObjectTypeSO[] BuildItems(StringBuilder report = null)
        {
            ContentGenerator.EnsureFolder(Folder);
            var items = new List<PlacedObjectTypeSO>();

            GameObject bastion = Bastion();
            PlacementProfile profile = Profile();

            foreach (Shooter shooter in Shooters())
            {
                DefenseDefinition definition = shooter.Definition();
                GameObject prefab = shooter.Prefab(definition);
                if (prefab == null)
                {
                    report?.Append($"  {shooter.Title}: WARNING - no prefab, skipped.\n");
                    continue;
                }

                PlacedObjectTypeSO item = Item(shooter, out bool made);
                item.placementProfile = profile;
                item.shopIcon = Icon(shooter, prefab);
                item.visual = Ghost(shooter, prefab, bastion).transform;
                item.prefab = Placed(shooter, prefab, definition, item, bastion).transform;
                EditorUtility.SetDirty(item);
                AssetDatabase.SaveAssetIfDirty(item);

                items.Add(item);
                WeaponDefinition weapon = definition.Level(0)?.weapon;
                report?.Append($"  {shooter.Title}: {(made ? "created" : "rebuilt")}, {item.PriceAmount} Coins, up to {item.maxItems}, " +
                               $"{item.width}x{item.height} cells{(weapon != null ? $" - {weapon.Describe()}" : "")}.\n");
            }

            report?.Append($"  Over the wall each goes up on a bastion ({BastionPath}, {CompoundWallSceneSetup.BastionHeight} m).\n");
            return items.ToArray();
        }

        // --- On the wall --------------------------------------------------------------------------------

        /// <summary>
        /// The bastion: the wall-site bastion, square to the 2 x 2 footprint, its baked meshes saved
        /// beside it so the prefab keeps them. A real wall piece with its wall-site health. Rebuilt
        /// every run, in place.
        /// </summary>
        static GameObject Bastion()
        {
            ContentGenerator.EnsureFolder(BastionMeshFolder);
            var root = new GameObject("__ShopBastion");

            try
            {
                CompoundWall wall = CompoundWallSceneSetup.BuildBastion(root.transform, "pfShopBastion", Vector3.zero, 0f, Cells, Cells,
                                                                        PerimeterStripLayout.BastionHealth, null,
                                                                        EffectPrefabGenerator.EnsureBuildDust(), 4242);
                GameObject bastion = wall.gameObject;
                bastion.transform.SetParent(null, true);

                // The baker leaves its meshes in memory: a prefab only keeps meshes that are assets.
                int n = 0;
                foreach (MeshFilter filter in bastion.GetComponentsInChildren<MeshFilter>(true))
                {
                    Mesh mesh = filter.sharedMesh;
                    if (mesh == null || AssetDatabase.Contains(mesh)) continue;

                    string path = $"{BastionMeshFolder}/ShopBastion_{filter.transform.parent.name}_{filter.name}_{n++}.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (existing != null)
                    {
                        EditorUtility.CopySerialized(mesh, existing);
                        filter.sharedMesh = existing;
                    }
                    else AssetDatabase.CreateAsset(mesh, path);
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(bastion, BastionPath);
                Object.DestroyImmediate(bastion);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The shooters' placement profile: rotation allowed, and the footing rule.</summary>
        static PlacementProfile Profile()
        {
            var rule = AssetDatabase.LoadAssetAtPath<DefenseFootingRule>(RulePath);
            if (rule == null)
            {
                rule = ScriptableObject.CreateInstance<DefenseFootingRule>();
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

        // --- The shop item ------------------------------------------------------------------------------

        static PlacedObjectTypeSO Item(Shooter shooter, out bool made)
        {
            string path = $"{Folder}/{shooter.Title}.asset";
            var item = AssetDatabase.LoadAssetAtPath<PlacedObjectTypeSO>(path);
            made = item == null;

            if (made)
            {
                item = ScriptableObject.CreateInstance<PlacedObjectTypeSO>();
                item.PriceAmount = shooter.Price;
                item.maxItems = shooter.Limit;
                item.requiredLevel = shooter.Level;
                item.isUnlocked = shooter.Level <= 0;
                item.Sellable = true;
                AssetDatabase.CreateAsset(item, path);
            }

            item.nameString = shooter.Title;
            item.categoryType = ObjectCategory.Defense;
            item.type = shooter.Type;
            item.width = Cells;
            item.height = Cells;
            item.buildCategory = BuildCategory.Defense;
            return item;
        }

        /// <summary>
        /// What the grid places: the corner-pivoted root the placement system moves about, the shooter
        /// nested at the middle of its footprint.
        /// </summary>
        static GameObject Placed(Shooter shooter, GameObject defensePrefab, DefenseDefinition definition, PlacedObjectTypeSO item,
                                 GameObject bastionPrefab)
        {
            string path = $"{Folder}/pf{shooter.Key}.prefab";
            var root = new GameObject($"pf{shooter.Key}");

            try
            {
                var placed = root.AddComponent<PlacedObject_Done>();

                var nested = (GameObject)PrefabUtility.InstantiatePrefab(defensePrefab, root.transform);
                nested.name = "Defense";
                nested.transform.localPosition = new Vector3(Cells * 0.5f, 0f, Cells * 0.5f);
                nested.transform.localRotation = Quaternion.identity;

                var post = nested.GetComponent<DefensePost>();
                post.ConfigureForShop(definition, definition.id);
                PrefabUtility.RecordPrefabInstancePropertyModifications(post);

                // Off until the shooter is set down over a wall.
                var bastion = (GameObject)PrefabUtility.InstantiatePrefab(bastionPrefab, root.transform);
                bastion.name = "Bastion";
                bastion.transform.localPosition = nested.transform.localPosition;
                bastion.SetActive(false);

                root.AddComponent<WallMount>().Configure(nested.transform, bastion, bastion.GetComponent<CompoundWall>(), post,
                                                         new Vector2(Cells, Cells), CompoundWallSceneSetup.BastionHeight);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);

                // Pointed at its shop item once both exist: the placed copy and the item refer to each other.
                using (var editing = new PrefabUtility.EditPrefabContentsScope(path))
                {
                    var so = new SerializedObject(editing.prefabContentsRoot.GetComponent<PlacedObject_Done>());
                    so.FindProperty("placedObjectTypeSO").objectReferenceValue = item;
                    so.FindProperty("shopItemType").intValue = (int)shooter.Type;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                return AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The ghost: the shooter's meshes and nothing else - no post, no effects, no colliders - over
        /// the grid's own footprint marker, the one the 2 x 2 well uses.
        /// </summary>
        static GameObject Ghost(Shooter shooter, GameObject defensePrefab, GameObject bastionPrefab)
        {
            string path = $"{Folder}/pf{shooter.Key}Visual.prefab";
            var root = new GameObject($"pf{shooter.Key}Visual");

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
                    copy.transform.localPosition = source.localPosition;
                    copy.transform.localRotation = source.localRotation;
                    copy.transform.localScale = source.localScale;
                    foreach (Collider c in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                }

                GameObject model = Object.Instantiate(defensePrefab, root.transform);
                model.name = "Model";
                model.transform.localPosition = new Vector3(Cells * 0.5f, 0f, Cells * 0.5f);
                model.transform.localRotation = Quaternion.identity;
                MeshesOnly(model);

                // The bastion as it looks when sound, its renderers off until the ghost is over a wall.
                GameObject bastion = Object.Instantiate(bastionPrefab, root.transform);
                bastion.name = "Bastion";
                bastion.transform.localPosition = model.transform.localPosition;
                foreach (string broken in new[] { "Visual/Damaged", "Visual/Rubble", "HealthBarAnchor" })
                {
                    Transform part = bastion.transform.Find(broken);
                    if (part != null) Object.DestroyImmediate(part.gameObject);
                }

                foreach (CompoundWall wall in bastion.GetComponentsInChildren<CompoundWall>(true)) Object.DestroyImmediate(wall);
                foreach (Collider c in bastion.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (Renderer r in bastion.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                bastion.transform.Find("Visual")?.gameObject.SetActive(true);
                bastion.transform.Find("Visual/Intact")?.gameObject.SetActive(true);

                root.AddComponent<WallMount>().Configure(model.transform, bastion, null, null, new Vector2(Cells, Cells),
                                                         CompoundWallSceneSetup.BastionHeight);

                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Strips a copy of a shooter down to what it looks like: no site, no thrown shot, no effects, no scripts, no colliders.</summary>
        static void MeshesOnly(GameObject model)
        {
            var doomed = new List<GameObject>();
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t == model.transform) continue;
                if (!t.gameObject.activeSelf || t.GetComponent<ParticleSystem>() != null || t.name.StartsWith("Projectile_")) doomed.Add(t.gameObject);
            }

            foreach (GameObject go in doomed)
                if (go != null) Object.DestroyImmediate(go);

            // Add-ons before the post they listen to, then everything else that is not drawing.
            foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(behaviour is DefensePost)) Object.DestroyImmediate(behaviour);
            foreach (DefensePost post in model.GetComponentsInChildren<DefensePost>(true)) Object.DestroyImmediate(post);
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        // --- The icon -----------------------------------------------------------------------------------

        static Sprite Icon(Shooter shooter, GameObject defensePrefab)
        {
            Texture2D render = Render(defensePrefab);
            try
            {
                return CropIcons.SaveIcon(Folder, $"Icon_{shooter.Key}", CropIcons.Compose(render));
            }
            finally
            {
                Object.DestroyImmediate(render);
            }
        }

        /// <summary>
        /// The shooter drawn on its own against nothing, seen from in front and above as the shop shows
        /// things, lit by the level's own sun and sky: square, transparent round it.
        /// </summary>
        public static Texture2D Render(GameObject defensePrefab, int size = 1024)
        {
            GameObject model = Object.Instantiate(defensePrefab);
            var camera = new GameObject("__ShooterIconCamera").AddComponent<Camera>();
            RenderTexture target = null;

            try
            {
                model.transform.SetPositionAndRotation(new Vector3(0f, -400f, 0f), Quaternion.identity);
                MeshesOnly(model);
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = IconLayer;

                var bounds = new Bounds(model.transform.position, Vector3.zero);
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);

                Vector3 toCamera = new Vector3(0.62f, 0.62f, 1f).normalized;
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

        // --- The scene's shop ---------------------------------------------------------------------------

        /// <summary>Lists the items under the shop's Defense entry and gives the shop window a Defense tab for them.</summary>
        public static string ApplyToScene(PlacedObjectTypeSO[] items)
        {
            var report = new StringBuilder();

            var database = Object.FindFirstObjectByType<ObjectDatabaseHandler>(FindObjectsInactive.Include);
            if (database == null) return "  WARNING: the open scene has no ObjectDatabaseHandler (no shop) - nothing wired.\n";

            Undo.RecordObject(database, "Defense shop");
            var so = new SerializedObject(database);
            SerializedProperty databases = so.FindProperty("objectsDatabases");
            SerializedProperty entry = null;
            for (int i = 0; i < databases.arraySize; i++)
            {
                SerializedProperty candidate = databases.GetArrayElementAtIndex(i);
                if (candidate.FindPropertyRelative("type").intValue == (int)ObjectCategory.Defense) entry = candidate;
            }

            if (entry == null)
            {
                databases.arraySize++;
                entry = databases.GetArrayElementAtIndex(databases.arraySize - 1);
                entry.FindPropertyRelative("type").intValue = (int)ObjectCategory.Defense;
            }

            SerializedProperty list = entry.FindPropertyRelative("placedObjectTypeSOList");
            list.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            so.ApplyModifiedProperties();
            report.Append($"  Shop database '{database.name}': Defense lists {string.Join(", ", Array.ConvertAll(items, it => it.name))}.\n");

            report.Append(Tab());

            EditorSceneManager.MarkSceneDirty(database.gameObject.scene);
            report.Append("  Scene left unsaved.\n");
            return report.ToString();
        }

        /// <summary>The Defense tab: a copy of the Props tab and its content panel, wired to each other and to the window.</summary>
        static string Tab()
        {
            var window = Object.FindFirstObjectByType<UiItemsWindow>(FindObjectsInactive.Include);
            if (window == null) return "  WARNING: no shop window (UiItemsWindow) in the scene - no tab added.\n";

            Transform buttons = window.transform.Find("AllPanelButtons");
            Transform viewport = window.transform.Find("Scroll View/Viewport");
            Transform propsButton = buttons != null ? buttons.Find("Props") : null;
            Transform propsContent = viewport != null ? viewport.Find("PropsContent") : null;
            if (propsButton == null || propsContent == null)
                return "  WARNING: the shop window has no Props tab to copy - no tab added.\n";

            Transform content = viewport.Find(ContentName);
            if (content == null)
            {
                GameObject copy = Object.Instantiate(propsContent.gameObject, viewport);
                copy.name = ContentName;
                Undo.RegisterCreatedObjectUndo(copy, "Defense shop");
                content = copy.transform;
                content.SetSiblingIndex(propsContent.GetSiblingIndex() + 1);
            }

            // The buttons are made when the game starts; anything left in the copy is stale.
            for (int i = content.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(content.GetChild(i).gameObject);
            content.gameObject.SetActive(false);

            Transform tab = buttons.Find(TabName);
            if (tab == null)
            {
                GameObject copy = Object.Instantiate(propsButton.gameObject, buttons);
                copy.name = TabName;
                Undo.RegisterCreatedObjectUndo(copy, "Defense shop");
                tab = copy.transform;
            }

            // Last of the tabs, before the close button.
            Transform close = buttons.Find("CloseBtn");
            tab.SetSiblingIndex(close != null ? close.GetSiblingIndex() : buttons.childCount - 1);
            if (close != null && tab.GetSiblingIndex() > close.GetSiblingIndex()) tab.SetSiblingIndex(close.GetSiblingIndex());

            var label = tab.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                Undo.RecordObject(label, "Defense shop");
                label.text = TabName;
            }

            var button = tab.GetComponent<Button>();
            var buttonSo = new SerializedObject(button);
            SerializedProperty calls = buttonSo.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            for (int i = 0; i < calls.arraySize; i++)
            {
                SerializedProperty call = calls.GetArrayElementAtIndex(i);
                if (call.FindPropertyRelative("m_MethodName").stringValue != nameof(UiItemsWindow.ActivatePanel)) continue;
                call.FindPropertyRelative("m_Target").objectReferenceValue = window;
                call.FindPropertyRelative("m_Arguments.m_ObjectArgument").objectReferenceValue = content.gameObject;
            }

            buttonSo.ApplyModifiedProperties();

            var windowSo = new SerializedObject(window);
            SerializedProperty containers = windowSo.FindProperty("itemContainers");
            SerializedProperty slot = null;
            for (int i = 0; i < containers.arraySize; i++)
            {
                SerializedProperty candidate = containers.GetArrayElementAtIndex(i);
                if (candidate.FindPropertyRelative("type").intValue == (int)ObjectCategory.Defense) slot = candidate;
            }

            if (slot == null)
            {
                containers.arraySize++;
                slot = containers.GetArrayElementAtIndex(containers.arraySize - 1);
            }

            slot.FindPropertyRelative("type").intValue = (int)ObjectCategory.Defense;
            slot.FindPropertyRelative("isActive").boolValue = false;
            slot.FindPropertyRelative("container").objectReferenceValue = content;
            windowSo.ApplyModifiedProperties();

            return $"  Shop window: '{TabName}' tab after Props, opening '{ContentName}'.\n";
        }
    }
}
