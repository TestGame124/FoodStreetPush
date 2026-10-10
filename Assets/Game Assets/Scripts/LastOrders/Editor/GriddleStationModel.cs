using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using B = LastOrders.EditorTools.BuildingKit;
using FX = LastOrders.EditorTools.PlantingEffects;
using K = LastOrders.EditorTools.SoupKitchenModel;
using T = LastOrders.EditorTools.KitchenTools;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The griddle station (pfGriddleStation): a breakfast corner inside the diner's kitchen, in the
    /// game's smooth low-poly style. A flat-top griddle on a steel stand - its seasoned black plate
    /// walled in by splash guards, a grease trough along the front, knobs and a pilot lamp on its
    /// panel - with trays of eggs, a crate of milk and a sack of potatoes on the shelf underneath and a
    /// shelf over it of squeeze bottles, plates and order tickets; a maple prep bench on the left with
    /// a bowl of beaten egg, a loaf being sliced and a box of eggs, a crate of potatoes underneath; a
    /// cream undercounter fridge on the right with plates and a breakfast ready on it; boxes of eggs
    /// and a pedal bin on the floor - all on a pad of charcoal and cream diner tiles, with a slatted
    /// mat where the cook stands.
    ///
    /// What moves while the station cooks - two eggs frying, the hash browns and French toast the
    /// spatula turns, the spatula, the pilot lamp, the steam and the spitting fat - is its own object,
    /// driven by a <see cref="GriddleStationAnimation"/>. Everything else is one mesh, painted in its
    /// vertices on LastOrders/Stylized as <see cref="BuildingKit"/> builds.
    ///
    /// The station prefab is made here the first time, set up as the soup and salad stations are:
    /// placed by hand in the level, with no placed-object type and no build ghost. Its front faces -Z,
    /// out into the dining room, and its origin is the middle of its pad, on the floor.
    /// </summary>
    public static class GriddleStationModel
    {
        internal const string PrefabPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Stoves/GriddleStation/pfGriddleStation.prefab";
        const string Folder = "Assets/Game Assets/Content/LastOrders/Kitchen/" + EnvKit.Style + "/GriddleStation";
        const string ModelName = "GriddleCorner";
        const string DisplayName = "Griddle Station";

        // --- Measurements (metres; front toward -Z) ------------------------------------------------

        internal const float PadLeft = -1.2f, PadRight = 1.2f, PadFront = -0.6f, PadBack = 1.15f, PadTop = 0.047f;

        /// <summary>
        /// Where the corner stands in the station's space. The station is placed on the build grid like
        /// everything the shop sells, its origin the near left corner of a 3 x 2 cell footprint at the
        /// grid's height: the pad is centred across the footprint, its back a hand short of the
        /// footprint's back edge, and stands on the diner's floor, 0.035 up.
        /// </summary>
        internal static readonly Vector3 Place = new Vector3(1.5f, 0.035f, 0.8f);

        /// <summary>The footprint on the build grid, in cells: 3 across, 2 deep.</summary>
        internal const int FootprintWidth = 3, FootprintDepth = 2;

        /// <summary>The steel stand the griddle sits on, its shelf low down.</summary>
        const float StandLeft = -0.6f, StandRight = 0.5f, StandFront = 0.38f, StandBack = 1.1f, StandTop = 0.62f, StandShelf = 0.16f;

        /// <summary>The griddle's body on its feet, and the plate set into its top.</summary>
        const float BodyLeft = -0.58f, BodyRight = 0.48f, BodyFront = 0.42f, BodyBack = 1.08f, BodyBottom = 0.66f, BodyTop = 0.875f;
        const float PlateLeft = -0.54f, PlateRight = 0.44f, PlateFront = 0.47f, PlateBack = 1.0f, PlateTop = 0.9f;

        const float PanelY = 0.77f, LampX = 0.38f;

        const float ShelfLeft = StandLeft, ShelfRight = StandRight, ShelfY = 1.42f, ShelfFront = 0.95f, ShelfBack = 1.14f, PostZ = 1.125f;

        const float BenchLeft = -1.17f, BenchRight = -0.66f, BenchFront = -0.5f, BenchBack = 1.08f, BenchTop = 0.86f, BenchShelf = 0.22f;

        const float FridgeLeft = 0.58f, FridgeRight = 1.16f, FridgeFront = 0.42f, FridgeBack = 1.1f, FridgeTop = 0.86f;

        /// <summary>Where the food goes on the plate, and how big it is: chunkier than life, so it reads at the game's zoom.</summary>
        static readonly Vector2[] EggSpots = { new Vector2(-0.4f, 0.6f), new Vector2(-0.26f, 0.83f) };
        static readonly Vector2[] PattySpots = { new Vector2(-0.08f, 0.6f), new Vector2(-0.05f, 0.83f) };
        static readonly Vector2[] ToastSpots = { new Vector2(0.21f, 0.6f), new Vector2(0.24f, 0.83f) };
        const float EggRadius = 0.075f, PattyThick = 0.026f, ToastHalf = 0.068f, ToastThick = 0.026f;

        /// <summary>The spatula's corner of the plate, its blade flat and handle out over the front.</summary>
        static readonly Vector3 SpatulaRest = new Vector3(0.39f, PlateTop + 0.003f, 0.54f);

        // --- The palette ---------------------------------------------------------------------------

        /// <summary>The diner's charcoal and cream, checked: apart from the soup corner's brick, the salad bar's sage and the bakery's crust.</summary>
        static readonly Color TileDark = B.Hex("4B4E52");
        static readonly Color TileLight = B.Hex("A09B8E");
        static readonly Color Grout = B.Hex("6B6560");

        /// <summary>Stainless, as the bakery's: broad faces painted darker than they look, or they go white.</summary>
        internal static readonly Color Steel = B.Hex("7E8489");
        internal static readonly Color SteelLight = B.Hex("8B9196");
        internal static readonly Color SteelDark = B.Hex("5E6469");
        internal static readonly Color Chrome = B.Hex("A3A9AD");
        internal static readonly Color Bakelite = B.Hex("2B2D30");
        internal static readonly Color Rubber = B.Hex("2A2C2F");

        static readonly Color PlateIron = B.Hex("2F3134");
        static readonly Color PlateWorn = B.Hex("494C4F");
        static readonly Color Grease = B.Hex("3B3530");

        internal static readonly Color Cream = B.Hex("AFA898");
        internal static readonly Color Teal = B.Hex("3D6762");
        internal static readonly Color TealLight = B.Hex("47756F");
        static readonly Color Navy = B.Hex("2E4560");

        internal static readonly Color[] Maple = { B.Hex("A48A68"), B.Hex("9C8262"), B.Hex("AA9070"), B.Hex("977D5E") };
        internal static readonly Color[] Slats = { B.Hex("B39B7A"), B.Hex("AA9171"), B.Hex("BBA383"), B.Hex("A58C6C") };
        static readonly Color BoardOak = B.Hex("C2AB88");

        /// <summary>
        /// The food, painted well short of how pale it looks: lying flat at the plate's height facing
        /// the sun it comes out about 1.4 times as bright, and a whiter egg white clipped.
        /// </summary>
        static readonly Color EggWhite = B.Hex("A9A598");
        static readonly Color EggEdge = B.Hex("8C7552");
        static readonly Color Yolk = B.Hex("C28D2E");
        static readonly Color YolkTop = B.Hex("CC9B3A");
        static readonly Color HashBrown = B.Hex("A07A42");
        static readonly Color HashBrownTop = B.Hex("AE8A50");
        static readonly Color HashCrisp = B.Hex("7A5530");
        static readonly Color Toast = B.Hex("A47A46");
        static readonly Color ToastSpot = B.Hex("85602F");
        static readonly Color Crust = B.Hex("7E5A34");
        static readonly Color Crumb = B.Hex("A89A7E");
        static readonly Color ShellBrown = B.Hex("A88B68");
        static readonly Color ShellCream = B.Hex("B4AA94");
        static readonly Color Milk = B.Hex("ABA9A2");
        static readonly Color Butter = B.Hex("BFA65E");
        static readonly Color Potato = B.Hex("8E7150");

        static readonly Color Ketchup = B.Hex("9E3628");
        static readonly Color Mustard = B.Hex("B08D2C");
        static readonly Color Ticket = B.Hex("B4AC9A");
        static readonly Color Ink = B.Hex("5A5550");
        static readonly Color TicketRed = B.Hex("A33A32");
        static readonly Color Cardboard = B.Hex("9C8462");
        static readonly Color CardboardDark = B.Hex("7F6A4D");

        [MenuItem("Last Orders/Kitchen/Build Griddle Station (pfGriddleStation)", priority = 102)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This builds the griddle corner - flat-top griddle on its stand, shelf of bottles and tickets, prep bench, " +
                "undercounter fridge, tiled pad - into pfGriddleStation, making the station prefab if it does not exist yet.\n\n" +
                "While the station cooks, eggs fry, the spatula flips hash browns and French toast, steam rises and fat spits.\n\n" +
                "Run 'Build Griddle & Grill Recipes + Icons' to give it its dishes, and 'Set Up Kitchen Shop & Line (Current Scene)' " +
                "to put it in the diner.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        /// <summary>Builds the corner and puts it into the station prefab, with no dialogs.</summary>
        public static string Apply()
        {
            ContentGenerator.EnsureFolder(Folder);

            Material painted = B.PaintedMaterial();
            Material lampGlow = UnlitMaterial(Folder, "Griddle_Lamp", new Color(1f, 0.52f, 0.16f));
            Material steamMaterial = K.SteamMaterial();
            Material spatterMaterial = GlowSpriteMaterial(Folder, "Griddle_Spatter");

            Mesh corner = SaveMesh(Corner(), "GriddleStation");
            Mesh egg = SaveMesh(FriedEgg(), "GriddleStation_Egg");
            Mesh patty = SaveMesh(Patty(), "GriddleStation_HashBrown");
            Mesh toast = SaveMesh(ToastSlice(), "GriddleStation_Toast");
            Mesh spatula = SaveMesh(Spatula(), "GriddleStation_Spatula");
            Mesh lamp = SaveMesh(Lamp(), "GriddleStation_Lamp");

            bool created = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            GameObject root = OpenStation(PrefabPath, DisplayName);

            try
            {
                Transform previous = root.transform.Find(ModelName);
                if (previous != null) Object.DestroyImmediate(previous.gameObject);

                var go = new GameObject(ModelName);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Place;
                B.Show(go, corner, corner.subMeshCount > 1 ? new[] { painted, B.GlowMaterial() } : new[] { painted });
                Transform model = go.transform;

                var eggs = new Transform[EggSpots.Length];
                for (int i = 0; i < eggs.Length; i++)
                {
                    eggs[i] = Part(model, $"Egg_{i}", egg, new Vector3(EggSpots[i].x, PlateTop, EggSpots[i].y), painted, ShadowCastingMode.Off);
                    eggs[i].localRotation = Quaternion.Euler(0f, i * 140f + 20f, 0f);
                }

                var flips = new List<Transform>();
                for (int i = 0; i < PattySpots.Length; i++)
                {
                    Transform p = Part(model, $"HashBrown_{i}", patty, new Vector3(PattySpots[i].x, PlateTop + PattyThick * 0.5f, PattySpots[i].y), painted,
                                       ShadowCastingMode.On);
                    p.localRotation = Quaternion.Euler(0f, i == 0 ? 6f : -8f, 0f);
                    flips.Add(p);

                    Transform t = Part(model, $"Toast_{i}", toast, new Vector3(ToastSpots[i].x, PlateTop + ToastThick * 0.5f, ToastSpots[i].y), painted,
                                       ShadowCastingMode.On);
                    t.localRotation = Quaternion.Euler(0f, i == 0 ? -10f : 12f, 0f);
                    flips.Add(t);
                }

                // Taken in turn along the plate: hash brown, toast, hash brown, toast.
                foreach (Transform part in eggs) part.gameObject.SetActive(false);
                foreach (Transform part in flips) part.gameObject.SetActive(false);

                Transform turner = Part(model, "Spatula", spatula, SpatulaRest, painted, ShadowCastingMode.On);

                Transform lampPart = Part(model, "PilotLamp", lamp, new Vector3(LampX, PanelY + 0.04f, BodyFront), lampGlow, ShadowCastingMode.Off);
                lampPart.gameObject.SetActive(false);

                Prep(model, painted);

                var plateMiddle = new Vector3((PlateLeft + PlateRight) * 0.5f, PlateTop + 0.02f, (PlateFront + PlateBack) * 0.5f);
                var plateSize = new Vector3(PlateRight - PlateLeft - 0.08f, 0.01f, PlateBack - PlateFront - 0.08f);
                ParticleSystem steam = Steam(model, "Steam", steamMaterial, plateMiddle, plateSize, new Vector2(0.16f, 0.26f), 30);
                ParticleSystem spatter = Spatter(model, "Spatter", spatterMaterial, plateMiddle, plateSize);

                go.AddComponent<GriddleStationAnimation>().Configure(eggs, flips.ToArray(), turner, lampPart.gameObject, steam, spatter);

                string colliderNote = FitCollider(root, PadRight - PadLeft, PadBack - PadFront);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return $"[Last Orders] Griddle corner built: {corner.vertexCount} vertices in the corner, {eggs.Length} eggs, {flips.Count} " +
                       $"pieces to flip, spatula, lamp, steam and spatter; {(created ? "made" : "rebuilt")} {PrefabPath}{colliderNote}.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Mesh SaveMesh(B.Model model, string name) => model.Save($"{Folder}/{name}.asset", name);

        // --- The station, shared with the grill ------------------------------------------------------

        /// <summary>
        /// The station prefab at <paramref name="path"/>, opened for editing (the caller saves and
        /// unloads it). Made the first time as the hand-placed stoves are: a CookeryStation with its
        /// centre point at the front of the pad and its name, a collider, and a PlacedObject_Done
        /// beside it for the code that looks for one - with no placed-object type.
        /// </summary>
        internal static GameObject OpenStation(string path, string displayName)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                ContentGenerator.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                var made = new GameObject(Path.GetFileNameWithoutExtension(path));
                try
                {
                    made.AddComponent<PlacedObject_Done>();
                    var station = made.AddComponent<CookeryStation>();
                    made.AddComponent<BoxCollider>();

                    var centre = new GameObject("CenterPoint").transform;
                    centre.SetParent(made.transform, false);
                    centre.localPosition = new Vector3(0f, 0.5f, -(PadBack - PadFront) * 0.5f);
                    station.centerPoint = centre;

                    PrefabUtility.SaveAsPrefabAsset(made, path);
                }
                finally
                {
                    Object.DestroyImmediate(made);
                }
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            var cookery = root.GetComponent<CookeryStation>();
            if (cookery != null)
            {
                var serialized = new SerializedObject(cookery);
                serialized.FindProperty("displayName").stringValue = displayName;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            // At the front of the pad, where the cook stands.
            Transform centrePoint = root.transform.Find("CenterPoint");
            if (centrePoint != null) centrePoint.localPosition = new Vector3(Place.x, 0.5f, Place.z + PadFront);

            return root;
        }

        /// <summary>A moving part: its own object under the corner, showing <paramref name="mesh"/> in <paramref name="material"/>.</summary>
        internal static Transform Part(Transform parent, string name, Mesh mesh, Vector3 at, Material material, ShadowCastingMode shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            B.Show(go, mesh, new[] { material });
            go.GetComponent<MeshRenderer>().shadowCastingMode = shadows;
            return go.transform;
        }

        /// <summary>The station's collider made to cover the pad and what stands on it, a little over a metre high.</summary>
        internal static string FitCollider(GameObject root, float width, float depth)
        {
            var collider = root.GetComponent<BoxCollider>();
            if (collider == null) return "";

            Vector3 before = collider.center, beforeSize = collider.size;
            collider.center = new Vector3(Place.x, Place.y + 0.6f, Place.z + (PadFront + PadBack) * 0.5f);
            collider.size = new Vector3(width, 1.2f, depth);
            return $"; collider {before} x {beforeSize} -> {collider.center} x {collider.size}";
        }

        /// <summary>Unlit in one colour, for a lamp or a flame: it glows the same however dark the kitchen is.</summary>
        internal static Material UnlitMaterial(string folder, string name, Color colour)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("Shader Universal Render Pipeline/Unlit is missing.");

            string path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", colour);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        /// <summary>Soft round specks of light, added on: spitting fat, sparks. The pickups' glow texture, coloured by each particle.</summary>
        internal static Material GlowSpriteMaterial(string folder, string name)
        {
            Shader shader = Shader.Find("LastOrders/SoftParticle");
            if (shader == null) throw new InvalidOperationException("Shader LastOrders/SoftParticle is missing.");

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FX.Folder + "/Particle_Glow.png");
            if (texture == null)
            {
                ContentGenerator.EnsureFolder(FX.Folder);
                texture = FX.Paint("Particle_Sparkle", FX.Sparkle);
            }

            string path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Additive", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_Lit", 0f);
            material.SetFloat("_DepthPull", 0.2f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        /// <summary>
        /// Soft puffs lifting off a hot surface <paramref name="size"/> across round <paramref name="at"/>,
        /// swelling and drifting as they fade. Left running at no rate: the animation sets how much, and
        /// emits a few more where something lands.
        /// </summary>
        internal static ParticleSystem Steam(Transform parent, string name, Material material, Vector3 at, Vector3 size, Vector2 puff, int max,
                                             Color? tint = null, float rise = 0.3f)
        {
            ParticleSystem ps = FX.NewSystem(parent, name, material, ParticleSystemRenderMode.Billboard);
            ps.transform.localPosition = at;

            Color c = tint ?? Color.white;
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(puff.x, puff.y);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(c.r, c.g, c.b, 0.5f), new Color(c.r * 0.96f, c.g * 0.96f, c.b * 0.96f, 0.72f));
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;

            FX.Rise(ps, rise, rise * 1.6f);
            FX.Grow(ps, FX.Curve((0f, 0.45f), (0.4f, 1.05f), (1f, 1.8f)));
            K.FadeInOut(ps, 0.15f, 0.5f);
            FX.Spin(ps, -25f, 25f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.8f;
            noise.scrollSpeed = 0.25f;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            return ps;
        }

        /// <summary>Hot fat spitting off the plate: bright specks hopping up off it and falling back, quick and small.</summary>
        static ParticleSystem Spatter(Transform parent, string name, Material material, Vector3 at, Vector3 size)
        {
            ParticleSystem ps = FX.NewSystem(parent, name, material, ParticleSystemRenderMode.Billboard);
            ps.transform.localPosition = at;

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.95f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.86f, 0.5f, 0.9f), new Color(1f, 0.95f, 0.75f, 1f));
            main.gravityModifier = 0.45f;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;

            // Turned to throw upward, so its depth across the plate is its Y and its thickness its Z.
            shape.scale = new Vector3(size.x, size.z, 0.01f);
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.randomDirectionAmount = 0.35f;

            FX.Grow(ps, FX.Curve((0f, 1f), (0.7f, 0.8f), (1f, 0f)));
            return ps;
        }

        // --- Shared shapes ---------------------------------------------------------------------------

        /// <summary>
        /// Tiles <paramref name="across"/> by <paramref name="down"/> over the pad on a grout bed,
        /// coloured by <paramref name="tile"/>(column, row); none where <paramref name="skip"/> says
        /// something stands on them that hides them.
        /// </summary>
        internal static void Pad(B.Model m, int across, int down, Func<int, int, Color> tile, Color grout, Func<float, float, bool> skip = null)
        {
            float width = PadRight - PadLeft, depth = PadBack - PadFront;
            float bed = PadTop - 0.012f;
            m.Box(grout, new Vector3((PadLeft + PadRight) * 0.5f, bed * 0.5f, (PadFront + PadBack) * 0.5f), new Vector3(width, bed, depth), 0.012f, 2);

            float tw = width / across, td = depth / down;
            for (int j = 0; j < down; j++)
            {
                for (int i = 0; i < across; i++)
                {
                    float x = PadLeft + (i + 0.5f) * tw, z = PadFront + (j + 0.5f) * td;
                    if (skip != null && skip(x, z)) continue;
                    m.Box(tile(i, j), new Vector3(x, PadTop - 0.007f, z), new Vector3(tw - 0.016f, 0.014f, td - 0.016f), 0.005f);
                }
            }
        }

        /// <summary>The slatted mat where the cook stands, from <paramref name="left"/> to <paramref name="right"/>, <paramref name="front"/> to <paramref name="back"/>.</summary>
        internal static void Duckboard(B.Model m, float left, float right, float front, float back, int key)
        {
            foreach (float x in new[] { left + 0.06f, (left + right) * 0.5f, right - 0.06f })
                m.Box(B.Walnut, new Vector3(x, PadTop + 0.014f, (front + back) * 0.5f), new Vector3(0.05f, 0.028f, back - front - 0.04f), 0.008f);

            const int slats = 6;
            float pitch = (back - front) / slats;
            for (int k = 0; k < slats; k++)
            {
                float z = front + (k + 0.5f) * pitch;
                Color c = Slats[B.Hash(k + key) % Slats.Length] * (1f + 0.04f * B.Jitter(k + key + 1));
                m.Box(c, new Vector3((left + right) * 0.5f, PadTop + 0.04f, z), new Vector3(right - left, 0.024f, pitch - 0.03f), 0.008f);
            }
        }

        /// <summary>A maple-topped bench on walnut legs and aprons, a slatted shelf underneath at <paramref name="shelf"/>.</summary>
        internal static void Bench(B.Model m, float left, float right, float front, float back, float top, float shelf, int key)
        {
            float cx = (left + right) * 0.5f, cz = (front + back) * 0.5f;
            float w = right - left, d = back - front;
            float under = top - 0.05f;

            foreach (float x in new[] { left + 0.04f, right - 0.04f })
                foreach (float z in new[] { front + 0.04f, cz, back - 0.04f })
                    m.Box(B.Walnut, new Vector3(x, (PadTop + under) * 0.5f, z), new Vector3(0.056f, under - PadTop, 0.056f), 0.012f);

            foreach (float x in new[] { left + 0.035f, right - 0.035f })
                m.Box(B.Walnut, new Vector3(x, under - 0.045f, cz), new Vector3(0.026f, 0.09f, d - 0.08f), 0.008f);
            foreach (float z in new[] { front + 0.035f, back - 0.035f })
                m.Box(B.Walnut, new Vector3(cx, under - 0.045f, z), new Vector3(w - 0.08f, 0.09f, 0.026f), 0.008f);

            const int boards = 4;
            for (int i = 0; i < boards; i++)
            {
                float x = left + (i + 0.5f) * w / boards;
                Color c = Maple[B.Hash(i + key) % Maple.Length] * (1f + 0.04f * B.Jitter(i + key + 1));
                m.Box(c, new Vector3(x, top - 0.025f, cz), new Vector3(w / boards - 0.004f, 0.05f, d + 0.02f), 0.01f);
            }

            for (int i = 0; i < 4; i++)
            {
                float x = left + 0.07f + i * (w - 0.14f) / 3f;
                m.Box(B.Plank(i + key + 50), new Vector3(x, shelf, cz), new Vector3(0.11f, 0.024f, d - 0.06f), 0.008f);
            }
        }

        /// <summary>A diner plate: cream glaze, the teal band round its rim.</summary>
        internal static void DinerPlate(B.Model m, Vector3 foot, float r)
        {
            m.Lathe((p, n) => new Vector2(p.x - foot.x, p.z - foot.z).magnitude > r * 0.82f ? Teal : Cream, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r * 0.5f, 0f), new Vector2(r * 0.9f, 0.01f), new Vector2(r, 0.016f), new Vector2(r * 0.96f, 0.018f),
                new Vector2(r * 0.6f, 0.008f), new Vector2(0f, 0.007f),
            }, 24);
        }

        /// <summary>A squeeze bottle: a soft body of <paramref name="colour"/>, a white cap and a pointed nozzle.</summary>
        internal static void SqueezeBottle(B.Model m, Vector3 foot, Color colour)
        {
            const float r = 0.032f, h = 0.15f;
            m.Lathe((p, n) => colour, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r * 0.9f, 0f), new Vector2(r, 0.012f), new Vector2(r, h * 0.8f), new Vector2(r * 0.8f, h * 0.9f),
                new Vector2(r * 0.55f, h), new Vector2(0f, h),
            }, 14);
            m.Tube(Cream, foot + Vector3.up * h, foot + Vector3.up * (h + 0.022f), 0.018f, 12);
            m.Tube((p, n) => Cream, foot + Vector3.up * (h + 0.022f), foot + Vector3.up * (h + 0.06f), 0.008f, 0.002f, 8);
        }

        /// <summary>An egg standing in a tray or a box: brown or cream, a little paler at the top.</summary>
        static void ShellEgg(B.Model m, Vector3 foot, float r, int key)
        {
            Color shell = (key % 3 == 0 ? ShellCream : ShellBrown) * (1f + 0.04f * B.Jitter(key));
            K.Ball(m, (p, n) => Color.Lerp(shell * 0.9f, shell * 1.04f, K.Smooth((p.y - foot.y) / (r * 2.6f))), foot + Vector3.up * (r * 1.25f),
                   new Vector3(r, r * 1.3f, r), Quaternion.Euler(0f, key * 31f, 0f), 12, 9);
        }

        // --- The corner ---------------------------------------------------------------------------

        static B.Model Corner()
        {
            // A shade darker toward the floor, on everything standing on the pad but not on the tiles themselves.
            var m = new B.Model { Shade = (p, n) => p.y < PadTop + 0.01f ? 1f : Mathf.Lerp(0.84f, 1f, K.Smooth((p.y - PadTop) / 0.35f)) };

            Pad(m, 10, 7, (i, j) => ((i + j) % 2 == 0 ? TileLight : TileDark) * (1f + 0.03f * B.Jitter(j * 10 + i + 800)), Grout,
                (x, z) => x > FridgeLeft + 0.05f && z > FridgeFront + 0.1f);
            Duckboard(m, -0.55f, 0.5f, -0.5f, 0.32f, 140);
            Stand(m);
            Griddle(m);
            Overshelf(m);
            PrepBench(m);
            Fridge(m);
            OnTheFloor(m);

            return m;
        }

        // --- The griddle --------------------------------------------------------------------------

        /// <summary>
        /// The steel equipment stand: four square legs on bullet feet, a top the griddle stands on and a
        /// shelf low down with trays of eggs, a crate of milk bottles and a sack of potatoes.
        /// </summary>
        static void Stand(B.Model m)
        {
            float mx = (StandLeft + StandRight) * 0.5f, mz = (StandFront + StandBack) * 0.5f;
            foreach (float x in new[] { StandLeft + 0.025f, StandRight - 0.025f })
            {
                foreach (float z in new[] { StandFront + 0.025f, StandBack - 0.025f })
                {
                    m.Box(Steel, new Vector3(x, (PadTop + StandTop) * 0.5f, z), new Vector3(0.036f, StandTop - PadTop, 0.036f), 0.007f);
                    m.Lathe((p, n) => Chrome, new Vector3(x, PadTop, z), new List<Vector2>
                    {
                        new Vector2(0f, 0f), new Vector2(0.008f, 0f), new Vector2(0.026f, 0.03f), new Vector2(0.026f, 0.034f), new Vector2(0f, 0.034f),
                    }, 10);
                }
            }

            m.Box(Steel, new Vector3(mx, StandTop - 0.015f, mz), new Vector3(StandRight - StandLeft, 0.03f, StandBack - StandFront), 0.006f, 2);
            m.Box(SteelDark, new Vector3(mx, StandShelf, mz), new Vector3(StandRight - StandLeft - 0.02f, 0.016f, StandBack - StandFront - 0.02f), 0.005f);
            float shelfTop = StandShelf + 0.008f;

            // Two trays of eggs, stacked crosswise.
            for (int k = 0; k < 2; k++) EggTray(m, new Vector3(-0.4f, shelfTop + k * 0.068f, 0.74f), k * 90f, k * 30);

            MilkCrate(m, new Vector3(-0.04f, shelfTop, 0.74f));
            B.Sack(m, new Vector3(0.32f, shelfTop, 0.76f), 0.12f, 0.3f, -0.04f, 31);
        }

        /// <summary>A pulp tray of eggs, five by five, a few gone.</summary>
        static void EggTray(B.Model m, Vector3 foot, float yaw, int key)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            m.Box(CardboardDark, foot + Vector3.up * 0.012f, new Vector3(0.28f, 0.024f, 0.28f), 0.008f, q);
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 5; j++)
                {
                    if (B.Hash(key + i * 5 + j) % 7 == 0) continue;
                    ShellEgg(m, foot + q * new Vector3(-0.112f + i * 0.056f, 0.012f, -0.112f + j * 0.056f), 0.021f, key + i * 5 + j);
                }
        }

        /// <summary>A teal wire crate of milk bottles with their cream caps.</summary>
        static void MilkCrate(B.Model m, Vector3 foot)
        {
            const float w = 0.28f, d = 0.28f, h = 0.13f;
            m.Box(Teal, foot + Vector3.up * 0.008f, new Vector3(w, 0.016f, d), 0.006f);
            foreach (float y in new[] { 0.05f, h })
            {
                m.Box(TealLight, foot + new Vector3(0f, y, -d * 0.5f), new Vector3(w, 0.014f, 0.012f), 0.004f);
                m.Box(TealLight, foot + new Vector3(0f, y, d * 0.5f), new Vector3(w, 0.014f, 0.012f), 0.004f);
                m.Box(TealLight, foot + new Vector3(-w * 0.5f, y, 0f), new Vector3(0.012f, 0.014f, d), 0.004f);
                m.Box(TealLight, foot + new Vector3(w * 0.5f, y, 0f), new Vector3(0.012f, 0.014f, d), 0.004f);
            }

            for (int k = 0; k < 4; k++)
            {
                float x = (k & 1) == 0 ? -w * 0.5f : w * 0.5f, z = (k & 2) == 0 ? -d * 0.5f : d * 0.5f;
                m.Box(Teal, foot + new Vector3(x, h * 0.5f, z), new Vector3(0.016f, h, 0.016f), 0.004f);
            }

            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    MilkBottle(m, foot + new Vector3(-0.085f + i * 0.085f, 0.016f, -0.085f + j * 0.085f), 0.032f, 0.17f);
        }

        internal static void MilkBottle(B.Model m, Vector3 foot, float r, float h)
        {
            m.Lathe((p, n) => p.y - foot.y > h * 0.93f ? Cream : Milk, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r, 0f), new Vector2(r, 0f), new Vector2(r, h * 0.6f), new Vector2(r * 0.6f, h * 0.8f),
                new Vector2(r * 0.5f, h * 0.93f), new Vector2(r * 0.55f, h), new Vector2(0f, h),
            }, 14);
        }

        /// <summary>
        /// The griddle on its four short feet: a stainless body, its front a dark panel with four knobs
        /// and the pilot lamp's bezel (the lamp is its own part); the plate set into its top, seasoned
        /// black, worn paler where the food goes; splash guards round the back and sides; the grease
        /// trough along the front edge draining into a drawer on the right.
        /// </summary>
        static void Griddle(B.Model m)
        {
            float mx = (BodyLeft + BodyRight) * 0.5f, mz = (BodyFront + BodyBack) * 0.5f;

            foreach (float x in new[] { BodyLeft + 0.05f, BodyRight - 0.05f })
                foreach (float z in new[] { BodyFront + 0.05f, BodyBack - 0.05f })
                    m.Tube((p, n) => Chrome, new Vector3(x, StandTop, z), new Vector3(x, BodyBottom + 0.01f, z), 0.018f, 0.014f, 10);

            m.Box(Steel, new Vector3(mx, (BodyBottom + BodyTop) * 0.5f, mz), new Vector3(BodyRight - BodyLeft, BodyTop - BodyBottom, BodyBack - BodyFront), 0.012f, 2);

            // The panel across the front.
            m.Box(SteelDark, new Vector3(mx, PanelY, BodyFront - 0.004f), new Vector3(BodyRight - BodyLeft - 0.04f, 0.14f, 0.01f), 0.004f);
            foreach (float x in new[] { -0.44f, -0.22f, 0f, 0.2f })
            {
                m.Tube((p, n) => n.z < -0.8f ? Chrome : Bakelite, new Vector3(x, PanelY - 0.01f, BodyFront - 0.008f), new Vector3(x, PanelY - 0.01f, BodyFront - 0.036f),
                       0.024f, 0.02f, 12);
                m.Box(Chrome, new Vector3(x, PanelY - 0.01f, BodyFront - 0.038f), new Vector3(0.006f, 0.026f, 0.004f), 0.002f, Quaternion.Euler(0f, 0f, -30f + x * 60f));
                foreach (float a in new[] { -50f, 0f, 50f })
                {
                    Vector3 tick = Quaternion.Euler(0f, 0f, a) * Vector3.up * 0.036f;
                    m.Box(Cream, new Vector3(x, PanelY - 0.01f, BodyFront - 0.01f) + tick, new Vector3(0.004f, 0.008f, 0.003f), 0.001f);
                }
            }

            // The lamp's chrome bezel and a name plate under it.
            K.Disc(m, (p, n) => Chrome, new Vector3(LampX, PanelY + 0.04f, BodyFront - 0.01f), Vector3.back, 0.018f, 12);
            m.Box(Chrome, new Vector3(LampX, PanelY - 0.035f, BodyFront - 0.011f), new Vector3(0.12f, 0.03f, 0.004f), 0.003f);
            m.Box(TicketRed, new Vector3(LampX, PanelY - 0.035f, BodyFront - 0.0135f), new Vector3(0.09f, 0.01f, 0.002f), 0.001f);

            // The plate: a slab, its top worn paler where the food is cooked, blotchy with seasoning.
            m.Box(PlateIron, new Vector3((PlateLeft + PlateRight) * 0.5f, PlateTop - 0.014f, (PlateFront + PlateBack) * 0.5f),
                  new Vector3(PlateRight - PlateLeft, 0.024f, PlateBack - PlateFront), 0.004f);
            m.Heightfield((p, n) =>
            {
                float wear = Mathf.Clamp01(0.5f + 0.35f * Mathf.Sin(p.x * 23f + p.z * 9f) * Mathf.Sin(p.z * 31f - p.x * 7f));
                float middle = 1f - Mathf.Clamp01(Mathf.Abs(p.z - 0.72f) / 0.3f);
                return Color.Lerp(PlateIron, PlateWorn, wear * middle * 0.8f);
            }, new Rect(PlateLeft + 0.01f, PlateFront + 0.01f, PlateRight - PlateLeft - 0.02f, PlateBack - PlateFront - 0.02f), 40, (x, z) => PlateTop);

            // The splash guards: the back one straight, the sides sloping down to the front.
            m.Box(Steel, new Vector3((PlateLeft + PlateRight) * 0.5f, PlateTop + 0.07f, PlateBack + 0.012f), new Vector3(PlateRight - PlateLeft + 0.05f, 0.16f, 0.016f),
                  0.004f);
            foreach (float x in new[] { PlateLeft - 0.013f, PlateRight + 0.013f })
            {
                float z = (PlateFront + PlateBack) * 0.5f + 0.02f;
                m.Box(Steel, new Vector3(x, PlateTop + 0.04f, z), new Vector3(0.016f, 0.11f, PlateBack - PlateFront - 0.02f), 0.004f, Quaternion.Euler(-11f, 0f, 0f));
            }

            // The grease trough and its drawer.
            m.Box(SteelDark, new Vector3((PlateLeft + PlateRight) * 0.5f, PlateTop - 0.012f, BodyFront + 0.025f), new Vector3(PlateRight - PlateLeft + 0.06f, 0.022f, 0.04f),
                  0.006f);
            m.Box(Grease, new Vector3((PlateLeft + PlateRight) * 0.5f, PlateTop - 0.004f, BodyFront + 0.025f), new Vector3(PlateRight - PlateLeft + 0.03f, 0.008f, 0.024f),
                  0.003f);
            m.Box(Steel, new Vector3(PlateRight + 0.005f, PlateTop - 0.05f, BodyFront - 0.02f), new Vector3(0.09f, 0.05f, 0.04f), 0.008f);
        }

        /// <summary>
        /// The shelf over the griddle on two posts from the floor behind the stand: squeeze bottles of
        /// ketchup and mustard, salt and pepper, a stack of plates and a jar of utensils; under its
        /// front edge, a rail of order tickets.
        /// </summary>
        static void Overshelf(B.Model m)
        {
            foreach (float x in new[] { ShelfLeft + 0.02f, ShelfRight - 0.02f })
            {
                m.Tube(Steel, new Vector3(x, PadTop, PostZ), new Vector3(x, ShelfY + 0.01f, PostZ), 0.016f, 10);
                m.Box(SteelDark, new Vector3(x, ShelfY - 0.034f, (ShelfFront + ShelfBack) * 0.5f), new Vector3(0.03f, 0.02f, ShelfBack - ShelfFront - 0.02f), 0.006f);
            }

            m.Box(Steel, new Vector3((ShelfLeft + ShelfRight) * 0.5f, ShelfY - 0.012f, (ShelfFront + ShelfBack) * 0.5f),
                  new Vector3(ShelfRight - ShelfLeft, 0.024f, ShelfBack - ShelfFront), 0.006f, 2);

            float top = ShelfY;
            SqueezeBottle(m, new Vector3(-0.5f, top, 1.04f), Ketchup);
            SqueezeBottle(m, new Vector3(-0.42f, top, 1.06f), Mustard);
            SqueezeBottle(m, new Vector3(-0.34f, top, 1.03f), Ketchup);
            K.Tin(m, new Vector3(-0.2f, top, 1.05f), 0.03f, 0.09f, Cream);
            K.PepperMill(m, new Vector3(-0.12f, top, 1.05f));
            for (int k = 0; k < 5; k++) DinerPlate(m, new Vector3(0.06f, top + k * 0.018f, 1.04f), 0.1f);
            K.Crock(m, new Vector3(0.26f, top, 1.05f), 0.045f, 0.1f);
            for (int k = 0; k < 3; k++)
            {
                Vector3 a = new Vector3(0.26f, top + 0.05f, 1.05f), b = a + new Vector3(-0.03f + k * 0.03f, 0.16f, 0.01f * (k - 1));
                m.Tube(k == 1 ? SteelLight : B.Walnut, a, b, 0.006f, 6);
                m.Box(SteelLight, b + Vector3.up * 0.02f, new Vector3(0.04f, 0.04f, 0.004f), 0.003f, Quaternion.Euler(0f, 20f * (k - 1), 0f));
            }

            K.Tin(m, new Vector3(0.4f, top, 1.05f), 0.034f, 0.1f, TicketRed);

            // The ticket rail, its tickets hung along it.
            float railY = ShelfY - 0.06f, railZ = ShelfFront - 0.004f;
            m.Box(Chrome, new Vector3((ShelfLeft + ShelfRight) * 0.5f, railY, railZ), new Vector3(ShelfRight - ShelfLeft - 0.06f, 0.026f, 0.012f), 0.005f);
            for (int k = 0; k < 6; k++)
            {
                float x = -0.47f + k * 0.17f + 0.02f * B.Jitter(k + 70);
                float tilt = 4f * B.Jitter(k + 71);
                Quaternion q = Quaternion.Euler(0f, 0f, tilt);
                Vector3 c = new Vector3(x, railY - 0.055f, railZ - 0.008f);
                m.Box(Ticket, c, new Vector3(0.07f, 0.1f, 0.002f), 0.001f, q);
                if (k % 3 == 1) m.Box(TicketRed, c + q * new Vector3(0f, 0.04f, -0.0015f), new Vector3(0.07f, 0.012f, 0.001f), 0.0005f, q);
                for (int line = 0; line < 4; line++)
                {
                    float length = 0.03f + 0.025f * ((B.Hash(k * 7 + line) % 100) / 100f);
                    m.Box(Ink, c + q * new Vector3(-0.025f + length * 0.5f, 0.02f - line * 0.017f, -0.0015f), new Vector3(length, 0.004f, 0.001f), 0.0005f, q);
                }
            }
        }

        /// <summary>Where the bowl of egg stands on the bench, front to back.</summary>
        const float BowlZ = -0.3f;

        /// <summary>
        /// The prep bench at work while the griddle cooks, each its own part: the whisk going round
        /// the bowl, the beaten egg swirling after it; the bread knife sawing the next slice off the
        /// loaf (<see cref="KitchenToolMotion"/>s).
        /// </summary>
        static void Prep(Transform model, Material painted)
        {
            float cx = (BenchLeft + BenchRight) * 0.5f, onBoard = BenchTop + 0.025f;
            Mesh egg = SaveMesh(BeatenEgg(), "GriddleStation_BeatenEgg");
            Mesh whisk = SaveMesh(T.Whisk(), "GriddleStation_Whisk");
            Mesh saw = SaveMesh(T.BreadKnife(), "GriddleStation_BreadKnife");

            var surface = new Vector3(cx, BenchTop + 0.045f, BowlZ);
            Transform beaten = Part(model, "BeatenEgg", egg, surface, painted, ShadowCastingMode.Off);
            Transform beater = T.Tool(model, "Whisk", whisk, surface, Quaternion.Euler(0f, 30f, 0f), painted);
            beater.gameObject.AddComponent<KitchenToolMotion>().Configure(KitchenToolMotion.Motion.Whisk, 1.4f, 0f, Vector3.up).Works(beaten);

            // Upright in the loaf just behind its cut face, its handle out to the right.
            Transform knife = T.Tool(model, "BreadKnife", saw, new Vector3(cx + 0.02f, onBoard + 0.098f, 0.252f), Quaternion.identity, painted);
            knife.gameObject.AddComponent<KitchenToolMotion>().Configure(KitchenToolMotion.Motion.Saw, 2.2f, 0.035f, Vector3.right);
        }

        /// <summary>Beaten egg in the bowl, its origin in the middle of its surface: yolk and white streaked together, so it shows turning.</summary>
        static B.Model BeatenEgg()
        {
            var m = new B.Model();
            K.Disc(m, (p, n) =>
            {
                float a = Mathf.Atan2(p.z, p.x), r = new Vector2(p.x, p.z).magnitude / 0.082f;
                float streak = Mathf.Sin(a * 3f + r * 6f);
                return Color.Lerp(Yolk, YolkTop * 1.05f, 0.5f + 0.5f * streak);
            }, Vector3.zero, Vector3.up, 0.082f, 28);
            return m;
        }

        // --- The bench, the fridge, the floor -----------------------------------------------------

        /// <summary>
        /// The prep bench on the left: a bowl of egg beaten for the French toast with a whisk in it at
        /// the front, a loaf on a board with slices cut in the middle, a box of eggs, a bottle of milk
        /// and a dish of butter at the back; a crate of potatoes and a sack of flour underneath.
        /// </summary>
        static void PrepBench(B.Model m)
        {
            Bench(m, BenchLeft, BenchRight, BenchFront, BenchBack, BenchTop, BenchShelf, 210);
            float cx = (BenchLeft + BenchRight) * 0.5f, top = BenchTop;

            // The bowl of beaten egg and its whisk.
            // The egg in it and the whisk are their own parts (Prep).
            var bowl = new Vector3(cx, top, BowlZ);
            K.Bowl(m, bowl, 0.1f);

            // The loaf on its board, three slices cut and laid out, the bread knife beside them.
            Quaternion boardTurn = Quaternion.Euler(0f, 4f, 0f);
            m.Box(BoardOak, new Vector3(cx, top + 0.0125f, 0.2f), new Vector3(0.36f, 0.025f, 0.46f), 0.008f, boardTurn, 2);
            float onBoard = top + 0.025f;
            m.Box(Crust, new Vector3(cx + 0.02f, onBoard + 0.05f, 0.32f), new Vector3(0.12f, 0.1f, 0.18f), 0.035f, 3);
            m.Box(Crumb, new Vector3(cx + 0.02f, onBoard + 0.048f, 0.229f), new Vector3(0.1f, 0.08f, 0.004f), 0.003f);
            for (int k = 0; k < 3; k++)
            {
                var at = new Vector3(cx - 0.06f + k * 0.055f, onBoard + 0.006f + k * 0.004f, 0.11f - k * 0.05f);
                Quaternion q = Quaternion.Euler(0f, -20f + k * 12f, 0f);
                m.Box(Crust, at, new Vector3(0.11f, 0.012f, 0.1f), 0.004f, q);
                m.Box(Crumb, at + Vector3.up * 0.0015f, new Vector3(0.096f, 0.012f, 0.086f), 0.003f, q);
            }

            // The bread knife, sawing into the loaf, is its own part (Prep).

            // The box of eggs, its lid up at the back; milk; butter in its dish.
            var box = new Vector3(cx - 0.05f, top, 0.74f);
            m.Box(Cardboard, box + Vector3.up * 0.02f, new Vector3(0.14f, 0.04f, 0.22f), 0.008f);
            m.Box(CardboardDark, box + new Vector3(0f, 0.08f, 0.115f), new Vector3(0.14f, 0.12f, 0.008f), 0.004f, Quaternion.Euler(-12f, 0f, 0f));
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 3; j++)
                    ShellEgg(m, box + new Vector3(-0.034f + i * 0.068f, 0.03f, -0.07f + j * 0.07f), 0.024f, 90 + i * 3 + j);

            MilkBottle(m, new Vector3(cx + 0.12f, top, 0.72f), 0.036f, 0.2f);
            var dish = new Vector3(cx + 0.09f, top, 0.94f);
            m.Box(Cream, dish + Vector3.up * 0.008f, new Vector3(0.13f, 0.016f, 0.09f), 0.006f);
            m.Box(Butter, dish + Vector3.up * 0.03f, new Vector3(0.08f, 0.03f, 0.05f), 0.008f);

            // Underneath.
            float shelfTop = BenchShelf + 0.012f;
            var crateFoot = new Vector3(cx, shelfTop, -0.2f);
            var crate = new Vector3(0.4f, 0.15f, 0.36f);
            K.OpenCrate(m, crateFoot, crate, 3f, 41);
            int key = 0;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    key++;
                    var at = crateFoot + new Vector3(-0.12f + i * 0.12f + 0.012f * B.Jitter(key + 1), crate.y - 0.012f, -0.1f + j * 0.1f + 0.012f * B.Jitter(key + 2));
                    K.Ball(m, (p, n) => Potato * (1f + 0.07f * B.Jitter(key + 3)), at, new Vector3(0.05f, 0.034f, 0.038f), Quaternion.Euler(0f, 60f * B.Jitter(key + 4), 0f),
                           10, 7);
                }
            }

            B.Sack(m, new Vector3(cx, shelfTop, 0.6f), 0.13f, 0.34f, 0.03f, 17);
        }

        /// <summary>
        /// A cream enamel undercounter fridge on the right: a teal door with a chrome bar handle, a kick
        /// plate and vent; on its steel top a stack of plates, a breakfast plated up ready to go - two
        /// eggs, a hash brown, a slice of toast - and salt and pepper.
        /// </summary>
        static void Fridge(B.Model m)
        {
            float mx = (FridgeLeft + FridgeRight) * 0.5f, mz = (FridgeFront + FridgeBack) * 0.5f;
            float w = FridgeRight - FridgeLeft, d = FridgeBack - FridgeFront;

            m.Box(Rubber, new Vector3(mx, PadTop + 0.04f, mz + 0.02f), new Vector3(w - 0.04f, 0.08f, d - 0.04f), 0.01f);
            m.Box(Cream, new Vector3(mx, (PadTop + 0.08f + FridgeTop - 0.03f) * 0.5f, mz), new Vector3(w, FridgeTop - 0.03f - PadTop - 0.08f, d), 0.025f, 2);
            m.Box(Steel, new Vector3(mx, FridgeTop - 0.015f, mz - 0.01f), new Vector3(w + 0.02f, 0.03f, d + 0.02f), 0.006f, 2);

            float doorBottom = PadTop + 0.12f, doorTop = FridgeTop - 0.08f;
            m.Box(Teal, new Vector3(mx, (doorBottom + doorTop) * 0.5f, FridgeFront - 0.01f), new Vector3(w - 0.06f, doorTop - doorBottom, 0.025f), 0.014f, 2);
            m.Box(TealLight, new Vector3(mx, (doorBottom + doorTop) * 0.5f - 0.01f, FridgeFront - 0.025f), new Vector3(w - 0.16f, doorTop - doorBottom - 0.12f, 0.01f),
                  0.008f, 2);

            float hy = doorTop - 0.05f, hz = FridgeFront - 0.06f;
            m.Tube(Chrome, new Vector3(mx - 0.18f, hy, hz), new Vector3(mx + 0.18f, hy, hz), 0.011f, 10);
            foreach (float x in new[] { mx - 0.15f, mx + 0.15f })
                m.Box(Chrome, new Vector3(x, hy, (hz + FridgeFront - 0.02f) * 0.5f), new Vector3(0.02f, 0.02f, FridgeFront - 0.02f - hz), 0.005f);

            for (int k = 0; k < 3; k++)
                m.Box(Rubber, new Vector3(mx, PadTop + 0.03f + k * 0.022f, FridgeFront - 0.012f), new Vector3(w - 0.16f, 0.01f, 0.01f), 0.003f);

            // On top.
            float top = FridgeTop;
            for (int k = 0; k < 6; k++) DinerPlate(m, new Vector3(mx - 0.12f, top + k * 0.018f, 0.95f), 0.11f);

            var plate = new Vector3(mx + 0.02f, top, 0.64f);
            DinerPlate(m, plate, 0.13f);
            float on = top + 0.012f;
            foreach (var (x, z) in new[] { (-0.04f, 0.03f), (0.035f, 0.045f) })
            {
                K.Disc(m, (p, n) => EggWhite, plate + new Vector3(x, 0.013f, z), Vector3.up, 0.042f, 16);
                K.Ball(m, (p, n) => Yolk, plate + new Vector3(x + 0.005f, 0.016f, z), new Vector3(0.017f, 0.01f, 0.017f), Quaternion.identity, 10, 6);
            }

            m.Box(HashBrown, new Vector3(plate.x - 0.03f, on + 0.008f, plate.z - 0.05f), new Vector3(0.08f, 0.016f, 0.05f), 0.007f, Quaternion.Euler(0f, 12f, 0f));
            m.Box(Toast, new Vector3(plate.x + 0.05f, on + 0.008f, plate.z - 0.04f), new Vector3(0.06f, 0.014f, 0.06f), 0.005f, Quaternion.Euler(0f, -25f, 0f));

            K.PepperMill(m, new Vector3(mx + 0.2f, top, 0.95f));
            K.Tin(m, new Vector3(mx + 0.11f, top, 0.97f), 0.026f, 0.08f, Cream);
        }

        /// <summary>Two boxes of eggs stacked on the floor at the front right, and a steel pedal bin.</summary>
        static void OnTheFloor(B.Model m)
        {
            var foot = new Vector3(0.84f, PadTop, -0.32f);
            for (int k = 0; k < 2; k++)
            {
                Quaternion q = Quaternion.Euler(0f, k * 9f - 4f, 0f);
                var size = new Vector3(0.34f, 0.18f, 0.26f);
                Vector3 c = foot + Vector3.up * (size.y * (k + 0.5f));
                m.Box(Cardboard * (1f + 0.04f * B.Jitter(k + 3)), c, size, 0.01f, q);
                m.Box(CardboardDark, c + q * new Vector3(0f, size.y * 0.5f - 0.002f, 0f), new Vector3(0.02f, 0.004f, size.z + 0.002f), 0.002f, q);
                m.Box(Ticket, c + q * new Vector3(0f, 0.02f, -size.z * 0.5f - 0.002f), new Vector3(0.16f, 0.07f, 0.002f), 0.001f, q);
                for (int e = 0; e < 3; e++) K.Disc(m, (p, n) => ShellBrown, c + q * new Vector3(-0.045f + e * 0.045f, 0.02f, -size.z * 0.5f - 0.0035f), q * Vector3.back, 0.016f, 10);
            }

            var bin = new Vector3(0.84f, PadTop, 0.15f);
            const float r = 0.13f, h = 0.4f;
            m.Lathe((p, n) => p.y - bin.y > h - 0.05f ? SteelLight : Steel, bin, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r - 0.01f, 0f), new Vector2(r, 0.02f), new Vector2(r, h - 0.05f), new Vector2(r + 0.008f, h - 0.045f),
                new Vector2(r + 0.006f, h - 0.02f), new Vector2(r * 0.7f, h + 0.01f), new Vector2(0f, h + 0.015f),
            }, 22);
            m.Box(Rubber, bin + new Vector3(0f, 0.02f, -r - 0.03f), new Vector3(0.09f, 0.018f, 0.07f), 0.006f);
        }

        // --- The moving parts ---------------------------------------------------------------------

        /// <summary>
        /// An egg frying sunny side up, its origin on the plate under the middle of its white: the
        /// white spread in a ragged round, browned at its lacy edge, domed toward the yolk.
        /// </summary>
        static B.Model FriedEgg()
        {
            var m = new B.Model();
            const float r = EggRadius;
            var yolk = new Vector3(0.012f, 0f, 0.008f);
            float Edge(float a) => r * (1f + 0.1f * Mathf.Sin(a * 3f + 0.4f) + 0.06f * Mathf.Sin(a * 5f + 1.3f));
            Vector3 At(float a, float t, float scale) =>
                Vector3.Lerp(yolk, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Edge(a) * scale, t) + Vector3.up * (0.003f + 0.01f * (1f - t * t));

            m.Surface((p, n) => EggEdge, 48, 2, (u, v) =>
            {
                Vector3 p = At(-u * Mathf.PI * 2f, v, 1.07f);
                return new Vector3(p.x, 0.0015f, p.z);
            }, true);
            m.Surface((p, n) =>
            {
                float t = new Vector2(p.x - yolk.x, p.z - yolk.z).magnitude / r;
                return Color.Lerp(EggWhite, EggWhite * 0.93f, K.Smooth((t - 0.75f) / 0.3f));
            }, 48, 8, (u, v) => At(-u * Mathf.PI * 2f, v, 1f), true);

            K.Ball(m, (p, n) => Color.Lerp(Yolk, YolkTop, K.Smooth((p.y - 0.008f) / 0.02f)), yolk + Vector3.up * 0.011f, new Vector3(0.026f, 0.017f, 0.026f),
                   Quaternion.identity, 16, 10);
            return m;
        }

        /// <summary>A hash brown, its origin in its middle: an oval patty, golden both sides, crisp shreds over both faces and darker round its edge.</summary>
        static B.Model Patty()
        {
            var m = new B.Model();
            var size = new Vector3(0.13f, PattyThick, 0.09f);
            m.Box(HashBrown, Vector3.zero, size, 0.011f, 3);
            m.Box(HashCrisp, Vector3.zero, new Vector3(size.x + 0.004f, size.y * 0.5f, size.z + 0.004f), 0.007f, 3);

            for (int side = -1; side <= 1; side += 2)
            {
                for (int s = 0; s < 18; s++)
                {
                    int key = s * 7 + side * 3 + 11;
                    float x = 0.05f * B.Jitter(key), z = 0.032f * B.Jitter(key + 1);
                    Vector3 at = new Vector3(x, side * (size.y * 0.5f - 0.001f), z);
                    Vector3 along = Quaternion.Euler(0f, 90f * B.Jitter(key + 2), 0f) * Vector3.right * 0.014f;
                    m.Tube(s % 3 == 0 ? HashCrisp : HashBrownTop, at - along, at + along, 0.0032f, 6);
                }
            }

            return m;
        }

        /// <summary>A slice of French toast, its origin in its middle: golden both faces with browner spots, the crust round its edge.</summary>
        static B.Model ToastSlice()
        {
            var m = new B.Model();
            const float h = ToastThick * 0.5f;
            Vector3 At(float a, float r, float y)
            {
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float x = Mathf.Sign(c) * Mathf.Sqrt(Mathf.Abs(c)), z = Mathf.Sign(s) * Mathf.Sqrt(Mathf.Abs(s));
                if (z > 0f) z *= 1f + 0.18f * (1f - x * x);
                return new Vector3(x * ToastHalf * r, y, z * ToastHalf * r);
            }

            Color Face(Vector3 p, Vector3 n)
            {
                float spot = Mathf.Sin(p.x * 90f + 1.1f) * Mathf.Sin(p.z * 70f - 0.7f);
                return Color.Lerp(Toast, ToastSpot, K.Smooth((spot - 0.35f) / 0.4f));
            }

            m.Surface(Face, 40, 5, (u, v) => At(-u * Mathf.PI * 2f, v, h + 0.002f * (1f - v * v)), true);
            m.Surface(Face, 40, 5, (u, v) => At(u * Mathf.PI * 2f, v, -h - 0.002f * (1f - v * v)), true);
            m.Surface((p, n) => Crust, 40, 3, (u, v) => At(u * Mathf.PI * 2f, 1f + 0.05f * Mathf.Sin(Mathf.PI * v), Mathf.Lerp(-h, h, v)), true);
            return m;
        }

        /// <summary>
        /// The spatula, its origin in the middle of its blade lying flat: a broad steel blade, the neck
        /// cranked up from its back edge, and a walnut handle riveted on, back toward -Z.
        /// </summary>
        static B.Model Spatula()
        {
            var m = new B.Model();
            m.Box(SteelLight, new Vector3(0f, 0.002f, 0f), new Vector3(0.1f, 0.004f, 0.1f), 0.0015f);
            m.Box(Steel, new Vector3(0f, 0.003f, -0.05f), new Vector3(0.1f, 0.006f, 0.012f), 0.002f);

            var neck = new Vector3(0f, 0.004f, -0.055f);
            var bend = new Vector3(0f, 0.045f, -0.1f);
            var end = new Vector3(0f, 0.095f, -0.24f);
            m.Tube(Steel, neck, bend, 0.006f, 8);
            m.Tube(Steel, bend, bend + (end - bend).normalized * 0.03f, 0.006f, 8);
            m.Tube((p, n) => B.Walnut, bend + (end - bend).normalized * 0.02f, end, 0.012f, 0.014f, 10);
            foreach (float t in new[] { 0.3f, 0.7f })
                K.Ball(m, (p, n) => Chrome, Vector3.Lerp(bend, end, t) + Vector3.up * 0.012f, Vector3.one * 0.004f, Quaternion.identity, 6, 4);
            return m;
        }

        /// <summary>The pilot lamp's lit lens, its origin on the panel.</summary>
        static B.Model Lamp()
        {
            var m = new B.Model();
            m.Box(Color.white, new Vector3(0f, 0f, -0.016f), new Vector3(0.022f, 0.022f, 0.014f), 0.007f, 2);
            return m;
        }
    }
}
