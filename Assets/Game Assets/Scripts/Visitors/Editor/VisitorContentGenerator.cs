using System.IO;
using LastOrders.EditorTools;
using UnityEditor;
using UnityEngine;
using K = LastOrders.EditorTools.PrimitiveKit;

namespace FoodStreet.Visitors.EditorTools
{
    /// <summary>
    /// Builds the visitor definitions, the vehicles and the prefabs for both.
    ///
    /// Assets are generated from the readable C# tables below rather than authored by hand,
    /// which is how the rest of this project's content works. A change to who turns up is then
    /// a one-line diff somebody can review, and the assets on disk cannot drift away from the
    /// intent they were supposed to express.
    ///
    /// Idempotent throughout. Everything goes through <see cref="CreateOrLoad{T}"/>, which
    /// loads an existing asset and edits it in place, so running this twice updates rather than
    /// duplicates - and a prefab or a colour somebody changed by hand survives, because the
    /// prefab field is only filled when it is empty.
    /// </summary>
    public static class VisitorContentGenerator
    {
        const string Root = "Assets/Game Assets/Content/Visitors";
        const string ProfilePath = Root + "/VisitorTraffic.asset";

        [MenuItem("Food Street/Visitors/Generate Visitor Content", priority = 0)]
        public static void Generate()
        {
            EnsureFolders();
            K.Reset();

            VehicleDefinition[] vehicles = GenerateVehicles();
            VisitorDefinition[] visitors = GenerateVisitors(vehicles);

            VisitorTrafficProfile profile = CreateOrLoad<VisitorTrafficProfile>(ProfilePath);
            profile.visitors = visitors;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Visitors] Generated {vehicles.Length} vehicles and {visitors.Length} " +
                      $"visitors into {Root}.\n" +
                      "Run 'Food Street / Visitors / Set Up Visitor System' to lay the road, " +
                      "the parking and the traffic into the open scene.");

            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        // --- Vehicles ---------------------------------------------------------------------
        //
        // Four cars, differing in size and colour rather than in shape. The silhouette is what
        // reads at this camera distance, and four lengths of the same greybox car is already
        // more difference than a player will consciously notice.

        static VehicleDefinition[] GenerateVehicles()
        {
            return new[]
            {
                Vehicle("Hatchback", "Hatchback", new Vector3(1.75f, 0.8f, 3.7f),
                        body: "C4694B", cabin: "3B4A63", drive: 6.5f),

                Vehicle("Pickup", "Pickup", new Vector3(1.95f, 0.9f, 4.6f),
                        body: "8B5B3E", cabin: "46587A", drive: 6f),

                Vehicle("Van", "Delivery van", new Vector3(2.05f, 1.15f, 4.6f),
                        body: "D8CBA6", cabin: "9AA3A9", drive: 5.2f),

                Vehicle("Saloon", "Saloon", new Vector3(1.85f, 0.82f, 4.3f),
                        body: "4E86B0", cabin: "34302B", drive: 6.2f),
            };
        }

        /// <summary>A vehicle asset and its greybox prefab. Also builds the hospital's shuttle,
        /// which is not in the traffic profile - nobody arrives by it - but is driven the same way.</summary>
        internal static VehicleDefinition Vehicle(string id, string label, Vector3 size, string body,
                                                  string cabin, float drive)
        {
            var asset = CreateOrLoad<VehicleDefinition>($"{Root}/Vehicles/Vehicle_{id}.asset");

            asset.displayName = label;
            asset.bodySize = size;
            asset.bodyColour = K.Hex(body);
            asset.cabinColour = K.Hex(cabin);
            asset.wheelRadius = Mathf.Clamp(size.y * 0.45f, 0.28f, 0.45f);
            asset.driveSpeed = drive;
            asset.manoeuvreSpeed = 2.2f;
            asset.turnSpeed = 150f;
            asset.arriveRadius = 0.6f;

            // Sized off the body rather than stated, so a longer car cannot be given a bay it
            // overhangs by a quiet half metre. The margins are tight on purpose: the car park
            // has a fixed footprint, and generous bays there means fewer of them.
            asset.bayFootprint = new Vector2(size.x + 0.7f, size.z + 0.6f);

            if (asset.prefab == null)
                asset.prefab = BuildVehiclePrefab(asset, $"{Root}/Prefabs/Vehicle_{id}.prefab");

            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// A greybox car: body, cabin, glass and four wheels.
        ///
        /// The same assembly the compound builder used for its scenery car, saved as a prefab
        /// with a behaviour on it. Local +Z is the front, which is what the parking bays and
        /// the waypoint follower both assume.
        /// </summary>
        static GameObject BuildVehiclePrefab(VehicleDefinition definition, string path)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));

            Vector3 size = definition.bodySize;
            float wheel = definition.wheelRadius;
            float floor = wheel * 1.2f;

            K.Box("Body", root.transform, Vector3.up * floor,
                  new Vector3(size.x, size.y, size.z), definition.bodyColour);

            K.Box("Cabin", root.transform, new Vector3(0f, floor + size.y, -size.z * 0.08f),
                  new Vector3(size.x * 0.88f, size.y * 0.85f, size.z * 0.46f), definition.cabinColour);

            K.Cube("Glass", root.transform,
                   new Vector3(0f, floor + size.y + size.y * 0.55f, -size.z * 0.08f),
                   new Vector3(size.x * 0.9f, size.y * 0.34f, size.z * 0.47f), K.Glass);

            // A darker strip at the front, so which way the car is pointing is readable from
            // above - which is the only angle this game is played from.
            K.Cube("Grille", root.transform, new Vector3(0f, floor + size.y * 0.4f, size.z * 0.5f),
                   new Vector3(size.x * 0.8f, size.y * 0.4f, 0.12f), K.Dark);

            foreach (float dz in new[] { size.z * 0.32f, -size.z * 0.32f })
            {
                foreach (float dx in new[] { -size.x * 0.5f, size.x * 0.5f })
                {
                    K.Log("Wheel", root.transform, new Vector3(dx, wheel, dz),
                          wheel * 2f, 0.3f, K.Dark, yaw: 90f);
                }
            }

            var hit = root.AddComponent<BoxCollider>();
            hit.center = new Vector3(0f, floor + size.y * 0.6f, 0f);
            hit.size = new Vector3(size.x, size.y * 2f, size.z);

            root.AddComponent<VisitorVehicle>().Configure(definition);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            return prefab;
        }

        // --- Visitors ---------------------------------------------------------------------
        //
        // Orders are drawn from the dishes the level's own kitchen can actually cook. An order
        // for something no station produces is an order that can never be filled, and a visitor
        // who can never be served holds a chair and a bay for the rest of the session.
        //
        // Amounts climb with the payment. A single bread is the tutorial case and a visitor
        // asking for three of something is the one who makes the player check the storeroom
        // before they tap.

        static VisitorDefinition[] GenerateVisitors(VehicleDefinition[] vehicles)
        {
            return new[]
            {
                Visitor("Regular", "Regular", model: 0, vehicle: Find(vehicles, "Hatchback"),
                        weight: 4f, pay: 22, hat: null,
                        orders: new[]
                        {
                            Order(ItemType.Bread, 1, 1, 3f),
                            Order(ItemType.TomatoSoup, 1, 1, 2f),
                        }),

                Visitor("Trucker", "Trucker", model: 2, vehicle: Find(vehicles, "Pickup"),
                        weight: 3f, pay: 38, hat: "SM_Chr_Attach_CowboyHat_01",
                        orders: new[]
                        {
                            Order(ItemType.Bread, 2, 3, 3f),
                            Order(ItemType.CornBread, 1, 2, 2f),
                        }),

                Visitor("Courier", "Courier", model: 1, vehicle: Find(vehicles, "Van"),
                        weight: 2f, pay: 45, hat: "SM_Chr_Attach_Cap_01",
                        orders: new[]
                        {
                            Order(ItemType.CornBread, 2, 3, 3f),
                            Order(ItemType.TomatoSoup, 2, 2, 2f),
                        }),

                Visitor("Family", "Family", model: 3, vehicle: Find(vehicles, "Saloon"),
                        weight: 2f, pay: 60, hat: null,
                        orders: new[]
                        {
                            Order(ItemType.TomatoSoup, 2, 3, 3f),
                            Order(ItemType.Bread, 3, 3, 2f),
                        }),

                Visitor("Farmhand", "Farmhand", model: 4, vehicle: Find(vehicles, "Pickup"),
                        weight: 2.5f, pay: 30, hat: "SM_Chr_Attach_Cap_02",
                        orders: new[]
                        {
                            Order(ItemType.CornBread, 1, 2, 3f),
                            Order(ItemType.Bread, 1, 2, 2f),
                        }),
            };
        }

        static ItemRequest Order(ItemType item, int low, int high, float weight) =>
            new ItemRequest { item = item, amountRange = new Vector2Int(low, high), weight = weight };

        static VisitorDefinition Visitor(string id, string label, int model,
                                         VehicleDefinition vehicle, float weight, int pay,
                                         string hat, ItemRequest[] orders)
        {
            var asset = CreateOrLoad<VisitorDefinition>($"{Root}/Visitors/Visitor_{id}.asset");

            asset.displayName = label;
            asset.characterModel = model;
            asset.headwear = hat;
            asset.vehicle = vehicle;
            asset.spawnWeight = weight;
            asset.payment = pay;
            asset.orders = orders;
            asset.eatSeconds = new Vector2(3f, 5f);
            asset.walkSpeed = 1.6f;

            if (asset.prefab == null)
                asset.prefab = BuildVisitorPrefab(asset, $"{Root}/Prefabs/Visitor_{id}.prefab");

            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// One visitor prefab: a person, a tap target and an order bubble.
        ///
        /// Built from the project's own character art where it is available and from primitives
        /// where it is not, rather than failing. A greybox figure in a dressed level looks wrong
        /// and is obvious; a definition with a null prefab looks like the spawner being broken.
        /// </summary>
        static GameObject BuildVisitorPrefab(VisitorDefinition definition, string path)
        {
            string prefabName = Path.GetFileNameWithoutExtension(path);

            GameObject root = VisitorCharacterGenerator.IsAvailable
                ? VisitorCharacterGenerator.CreateCharacter(definition.characterModel, prefabName,
                                                            Vector3.zero, null)
                : null;

            if (root == null)
            {
                root = new GameObject(prefabName);
                K.Capsule("Body", root.transform, Vector3.zero, 0.5f, 1.25f, K.Denim);
                K.Sphere("Head", root.transform, Vector3.up * 1.45f, 0.42f, K.Skin);
            }
            else if (!string.IsNullOrEmpty(definition.headwear))
            {
                VisitorCharacterGenerator.AddHat(root, definition.headwear);
            }

            // 4 asks for a touch target of at least 48 logical pixels, and a skinned mesh has no
            // collider of its own. One capsule over the whole figure is that target.
            var hit = root.AddComponent<CapsuleCollider>();
            hit.center = new Vector3(0f, 0.85f, 0f);
            hit.height = 1.7f;
            hit.radius = 0.32f;

            OrderBubble bubble = BuildBubble(root.transform);

            var visitor = root.AddComponent<Visitor>();
            visitor.Configure(bubble, root.GetComponentInChildren<Animator>());

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            return prefab;
        }

        /// <summary>
        /// The order bubble: a disc, an item icon and a count, floating over the head.
        ///
        /// Sprite renderers rather than a world-space canvas, matching every other piece of
        /// floating UI in this project. The panel carries its own box collider so the tap
        /// target is the bubble as well as the person - the bubble is the thing the player is
        /// actually looking at, and it is twice the size.
        /// </summary>
        static OrderBubble BuildBubble(Transform parent)
        {
            var go = new GameObject("OrderBubble");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 2.1f, 0f);

            var bubble = go.AddComponent<OrderBubble>();

            var panel = new GameObject("Panel");
            panel.transform.SetParent(go.transform, false);

            SpriteRenderer disc = Sprite(panel.transform, "Disc", "UI/Skin/Knob.psd", 0);
            SpriteRenderer icon = Sprite(panel.transform, "Icon", null, 1);

            icon.enabled = false;

            var hit = panel.AddComponent<BoxCollider>();
            hit.size = new Vector3(0.8f, 0.8f, 0.1f);
            hit.isTrigger = true;

            var countGo = new GameObject("Count");
            countGo.transform.SetParent(go.transform, false);
            // Toward the camera by a hair, so it draws in front of the disc now that the bubble
            // takes the camera's rotation; sized to sit in the disc's corner, not over it.
            countGo.transform.localPosition = new Vector3(0.3f, -0.27f, -0.02f);

            var count = countGo.AddComponent<TextMesh>();
            count.text = "x1";
            count.characterSize = 0.05f;
            count.fontSize = 64;
            count.anchor = TextAnchor.MiddleCenter;
            count.alignment = TextAlignment.Center;
            count.color = new Color(0.16f, 0.14f, 0.12f);
            count.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // A TextMesh draws with the font's own material, and the font asset carries it.
            var renderer = countGo.GetComponent<MeshRenderer>();
            if (renderer != null && count.font != null) renderer.sharedMaterial = count.font.material;

            bubble.Configure(panel, disc, icon, count);

            panel.SetActive(false);
            return bubble;
        }

        static SpriteRenderer Sprite(Transform parent, string name, string builtin, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();

            // Sorted explicitly rather than left to depth order: the icon and the disc sit in
            // the same plane, and whichever drew last would otherwise win by accident.
            renderer.sortingOrder = order;

            if (!string.IsNullOrEmpty(builtin))
                renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(builtin);

            return renderer;
        }

        // --- Asset plumbing -----------------------------------------------------------------

        static VehicleDefinition Find(VehicleDefinition[] set, string label)
        {
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] != null && set[i].name.EndsWith(label)) return set[i];
            }

            Debug.LogError($"[Visitors] No vehicle named '{label}' was generated.");
            return set.Length > 0 ? set[0] : null;
        }

        internal static void EnsureFolders()
        {
            string[] folders = { Root, $"{Root}/Vehicles", $"{Root}/Visitors", $"{Root}/Prefabs" };

            foreach (string folder in folders) EnsureFolder(folder);

            AssetDatabase.Refresh();
        }

        /// <summary>Creates a folder and every missing ancestor of it.</summary>
        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        /// <summary>
        /// The asset at a path, made if it is not there yet.
        ///
        /// Load-then-create rather than delete-then-create, so every reference already pointing
        /// at the asset survives a regeneration. Deleting and recreating would give it a new
        /// GUID and quietly empty the traffic profile of every scene that used it.
        /// </summary>
        static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);

            return created;
        }
    }
}
