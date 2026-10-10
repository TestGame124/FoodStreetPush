using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using B = LastOrders.EditorTools.BuildingKit;
using FX = LastOrders.EditorTools.PlantingEffects;
using G = LastOrders.EditorTools.GriddleStationModel;
using K = LastOrders.EditorTools.SoupKitchenModel;
using T = LastOrders.EditorTools.KitchenTools;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The grill station (pfGrillStation): a char-grill corner inside the diner's kitchen, in the
    /// game's smooth low-poly style. A steel-clad firebox on an open stand - a bed of coals glowing
    /// under a grate of iron bars, vent and knobs on its front, a rail along it - with a bag of
    /// charcoal and a crate of corn on the shelf underneath; a stainless extraction hood over the back
    /// of it on two posts, its duct going up; a maple prep bench on the left with fish on a tray,
    /// chicken and onion being threaded onto skewers and a bowl of tomatoes, more corn underneath; a
    /// steel landing table on the right with grilled food resting on a pan, plates, and a bin of
    /// charcoal underneath; a fire extinguisher and a sack on the floor - all on a pad of charcoal
    /// slate, with a slatted mat where the cook stands.
    ///
    /// What moves while the station cooks - two cobs, three skewers, a fish and tomatoes on the grate,
    /// the tongs that turn them, the flames, the glow of the coals, the smoke and sparks - is its own
    /// object, driven by a <see cref="GrillStationAnimation"/>. Everything else is one mesh, painted in
    /// its vertices on LastOrders/Stylized as <see cref="BuildingKit"/> builds.
    ///
    /// Made and set up as <see cref="GriddleStationModel"/> makes the griddle: a hand-placed station
    /// with its front toward -Z and its origin in the middle of its pad, on the floor.
    /// </summary>
    public static class GrillStationModel
    {
        internal const string PrefabPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Stoves/GrillStation/pfGrillStation.prefab";
        const string Folder = "Assets/Game Assets/Content/LastOrders/Kitchen/" + EnvKit.Style + "/GrillStation";
        const string ModelName = "GrillCorner";
        const string DisplayName = "Grill Station";

        // --- Measurements (metres; front toward -Z) ------------------------------------------------

        const float PadTop = G.PadTop;

        /// <summary>The steel-clad firebox on its open stand, the grate across its top and the coals down inside it.</summary>
        const float GrillLeft = -0.62f, GrillRight = 0.52f, GrillFront = 0.4f, GrillBack = 1.1f;
        const float StandTop = 0.46f, StandShelf = 0.14f, BoxTop = 0.84f, Wall = 0.045f;
        const float GrateLeft = -0.56f, GrateRight = 0.46f, GrateFront = 0.46f, GrateBack = 1.04f, GrateTop = 0.865f;
        const float CoalY = 0.74f;
        const int Bars = 16;

        /// <summary>The hood over the back of the grill, kept back so it never hides the grate from the game's camera.</summary>
        const float HoodLeft = -0.66f, HoodRight = 0.56f, HoodFront = 0.74f, HoodBack = 1.15f, HoodBottom = 1.5f, HoodTop = 1.74f, DuctTop = 2.0f;
        const float PostZ = 1.125f;

        const float BenchLeft = -1.17f, BenchRight = -0.7f, BenchFront = -0.5f, BenchBack = 1.08f, BenchTop = 0.86f, BenchShelf = 0.22f;
        const float TableLeft = 0.6f, TableRight = 1.16f, TableFront = 0.42f, TableBack = 1.1f, TableTop = 0.86f, TableShelf = 0.2f;

        /// <summary>Where the food goes on the grate: cobs, skewers and the fish lying along X, tomato halves at the right.</summary>
        static readonly Vector2[] CobSpots = { new Vector2(-0.4f, 0.58f), new Vector2(-0.4f, 0.74f) };
        static readonly Vector2[] SkewerSpots = { new Vector2(0.05f, 0.58f), new Vector2(0.05f, 0.72f), new Vector2(0.05f, 0.86f) };
        static readonly Vector2 FishSpot = new Vector2(-0.33f, 0.92f);
        static readonly Vector2[] TomatoSpots = { new Vector2(0.34f, 0.6f), new Vector2(0.37f, 0.75f), new Vector2(0.33f, 0.9f) };
        const float CobRadius = 0.036f, CobLength = 0.2f, Chunk = 0.046f, FishThick = 0.026f;

        static readonly Vector3 TongsRest = new Vector3(0.7f, TableTop + 0.008f, 0.62f);

        // --- The palette ---------------------------------------------------------------------------

        /// <summary>Charcoal slate, a shade apart tile to tile, with a paler grout so the tiles read: sooty, as a grill corner is.</summary>
        static readonly Color[] Tiles = { B.Hex("55524F"), B.Hex("5C5955"), B.Hex("514E4B"), B.Hex("595652") };
        static readonly Color Grout = B.Hex("6E6A64");

        static readonly Color Iron = B.Hex("2E2E30");
        static readonly Color IronEdge = B.Hex("4F5154");
        static readonly Color Soot = B.Hex("1F1C1B");
        static readonly Color Coal = B.Hex("2B2522");
        static readonly Color Ash = B.Hex("6E6862");
        static readonly Color DinerRed = B.Hex("86463E");

        static readonly Color Kernel = B.Hex("C8922A");
        static readonly Color KernelLight = B.Hex("D2A23A");
        static readonly Color Char = B.Hex("3E2618");
        static readonly Color Husk = B.Hex("8C9A55");
        static readonly Color HuskDry = B.Hex("A99C6A");
        static readonly Color Chicken = B.Hex("A9763F");
        static readonly Color ChickenRaw = B.Hex("B89A84");
        static readonly Color Onion = B.Hex("B4A884");
        static readonly Color OnionGrilled = B.Hex("9C7A4C");
        static readonly Color SkewerWood = B.Hex("B59B76");
        static readonly Color FishBack = B.Hex("56646E");
        static readonly Color FishBelly = B.Hex("A6A8A3");
        static readonly Color FishGrilledBack = B.Hex("8A6436");
        static readonly Color FishGrilledBelly = B.Hex("AE8E5E");
        static readonly Color Fin = B.Hex("5E6466");
        static readonly Color TomatoSkin = B.Hex("A9332A");
        static readonly Color GlassOil = B.Hex("8C8A3A");
        static readonly Color Charcoal = B.Hex("3A3735");

        [MenuItem("Last Orders/Kitchen/Build Grill Station (pfGrillStation)", priority = 103)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This builds the grill corner - char-grill on its stand, extraction hood, prep bench, landing table, tiled pad - " +
                "into pfGrillStation, making the station prefab if it does not exist yet.\n\n" +
                "While the station cooks, the coals glow up, flames lick through the bars, the tongs turn cobs, skewers and a fish " +
                "with a flare-up each time, and smoke and sparks rise into the hood.\n\n" +
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
            Material flame = G.UnlitMaterial(Folder, "Grill_Flame", new Color(1f, 0.5f, 0.15f));
            Material flameCore = G.UnlitMaterial(Folder, "Grill_FlameCore", new Color(1f, 0.85f, 0.42f));
            Material coalGlow = G.UnlitMaterial(Folder, "Grill_Coals", new Color(0.95f, 0.42f, 0.12f));
            Material smokeMaterial = K.SteamMaterial();
            Material sparkMaterial = G.GlowSpriteMaterial(Folder, "Grill_Sparks");

            Mesh corner = SaveMesh(Corner(), "GrillStation");
            Mesh cob = SaveMesh(GrilledCob(), "GrillStation_Cob");
            Mesh skewer = SaveMesh(Skewer(), "GrillStation_Skewer");
            Mesh fish = SaveMesh(GrilledFish(), "GrillStation_Fish");
            Mesh tomato = SaveMesh(TomatoHalf(), "GrillStation_Tomato");
            Mesh tongs = SaveMesh(Tongs(), "GrillStation_Tongs");
            Mesh coals = SaveMesh(CoalBed(), "GrillStation_Coals");
            Mesh flamesA = SaveMesh(Flames(0), "GrillStation_Flames_A");
            Mesh flamesB = SaveMesh(Flames(1), "GrillStation_Flames_B");

            bool created = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            GameObject root = G.OpenStation(PrefabPath, DisplayName);

            try
            {
                Transform previous = root.transform.Find(ModelName);
                if (previous != null) Object.DestroyImmediate(previous.gameObject);

                var go = new GameObject(ModelName);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = G.Place;
                B.Show(go, corner, corner.subMeshCount > 1 ? new[] { painted, B.GlowMaterial() } : new[] { painted });
                Transform model = go.transform;

                Transform coalPart = G.Part(model, "Coals", coals, new Vector3(0f, CoalY, 0f), coalGlow, ShadowCastingMode.Off);
                Transform[] flameRows =
                {
                    G.Part(model, "Flames_A", flamesA, new Vector3(0f, CoalY, 0f), flame, ShadowCastingMode.Off),
                    G.Part(model, "Flames_B", flamesB, new Vector3(0f, CoalY, 0f), flameCore, ShadowCastingMode.Off),
                };

                var cobs = new Transform[CobSpots.Length];
                for (int i = 0; i < cobs.Length; i++)
                {
                    cobs[i] = G.Part(model, $"Cob_{i}", cob, new Vector3(CobSpots[i].x, GrateTop + CobRadius, CobSpots[i].y), painted, ShadowCastingMode.On);
                    cobs[i].localRotation = Quaternion.Euler(i * 70f, i == 0 ? 3f : -4f, 0f);
                }

                var skewers = new Transform[SkewerSpots.Length];
                for (int i = 0; i < skewers.Length; i++)
                {
                    skewers[i] = G.Part(model, $"Skewer_{i}", skewer, new Vector3(SkewerSpots[i].x, GrateTop + Chunk * 0.5f, SkewerSpots[i].y), painted,
                                        ShadowCastingMode.On);
                    skewers[i].localRotation = Quaternion.Euler(i * 120f, 2f * (i - 1), 0f);
                }

                Transform fishPart = G.Part(model, "Fish", fish, new Vector3(FishSpot.x, GrateTop + FishThick * 0.5f, FishSpot.y), painted, ShadowCastingMode.On);

                var tomatoes = new Transform[TomatoSpots.Length];
                for (int i = 0; i < tomatoes.Length; i++)
                {
                    tomatoes[i] = G.Part(model, $"Tomato_{i}", tomato, new Vector3(TomatoSpots[i].x, GrateTop, TomatoSpots[i].y), painted, ShadowCastingMode.On);
                    tomatoes[i].localRotation = Quaternion.Euler(0f, i * 77f, 0f);
                }

                var food = new List<Transform> { cobs[0], cobs[1], skewers[0], skewers[1], skewers[2], fishPart };
                food.AddRange(tomatoes);
                foreach (Transform part in food) part.gameObject.SetActive(false);

                Transform[] turns = { skewers[0], cobs[0], skewers[1], fishPart, skewers[2], cobs[1] };
                float[] degrees = { 180f, 90f, 180f, 180f, 180f, 90f };

                Transform tongsPart = G.Part(model, "Tongs", tongs, TongsRest, painted, ShadowCastingMode.On);
                Chopping(model, painted);

                var grateMiddle = new Vector3((GrateLeft + GrateRight) * 0.5f, GrateTop + 0.05f, (GrateFront + GrateBack) * 0.5f);
                var grateSize = new Vector3(GrateRight - GrateLeft - 0.06f, 0.01f, GrateBack - GrateFront - 0.06f);
                ParticleSystem smoke = G.Steam(model, "Smoke", smokeMaterial, grateMiddle, grateSize, new Vector2(0.22f, 0.34f), 50, new Color(0.8f, 0.8f, 0.8f), 0.42f);
                var drift = smoke.velocityOverLifetime;
                drift.z = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
                ParticleSystem sparks = Sparks(model, sparkMaterial, grateMiddle, grateSize);

                go.AddComponent<GrillStationAnimation>().Configure(food.ToArray(), turns, degrees, tongsPart, flameRows, coalPart.GetComponent<MeshRenderer>(),
                                                                   smoke, sparks);

                string colliderNote = G.FitCollider(root, G.PadRight - G.PadLeft, G.PadBack - G.PadFront);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return $"[Last Orders] Grill corner built: {corner.vertexCount} vertices in the corner, {food.Count} pieces on the grate " +
                       $"({turns.Length} turned by the tongs), coals, flames, smoke and sparks; {(created ? "made" : "rebuilt")} {PrefabPath}{colliderNote}.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Mesh SaveMesh(B.Model model, string name) => model.Save($"{Folder}/{name}.asset", name);

        /// <summary>Sparks off the coals: orange specks drifting up and wandering, a spray more when fat flares.</summary>
        static ParticleSystem Sparks(Transform parent, Material material, Vector3 at, Vector3 size)
        {
            ParticleSystem ps = FX.NewSystem(parent, "Sparks", material, ParticleSystemRenderMode.Billboard);
            ps.transform.localPosition = at;

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.042f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.5f, 0.16f, 1f), new Color(1f, 0.78f, 0.38f, 1f));
            main.gravityModifier = -0.04f;
            main.maxParticles = 50;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;

            // Turned to throw upward, so its depth across the grate is its Y and its thickness its Z.
            shape.scale = new Vector3(size.x, size.z, 0.01f);
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.randomDirectionAmount = 0.3f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 1.4f;
            noise.scrollSpeed = 0.6f;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            FX.Grow(ps, FX.Curve((0f, 1f), (0.7f, 0.7f), (1f, 0f)));
            FX.FadeOut(ps, 0.6f);
            return ps;
        }

        // --- The corner ---------------------------------------------------------------------------

        static B.Model Corner()
        {
            // A shade darker toward the floor, on everything standing on the pad but not on the tiles themselves.
            var m = new B.Model { Shade = (p, n) => p.y < PadTop + 0.01f ? 1f : Mathf.Lerp(0.84f, 1f, K.Smooth((p.y - PadTop) / 0.35f)) };

            G.Pad(m, 8, 6, (i, j) => Tiles[B.Hash(j * 8 + i + 1500) % Tiles.Length] * (1f + 0.035f * B.Jitter(j * 8 + i + 1600)), Grout);
            G.Duckboard(m, -0.6f, 0.5f, -0.5f, 0.32f, 340);
            Firebox(m);
            Grate(m);
            Hood(m);
            PrepBench(m);
            LandingTable(m);
            OnTheFloor(m);

            return m;
        }

        // --- The grill ----------------------------------------------------------------------------

        /// <summary>
        /// The firebox: steel walls round a sooty inside, the coals heaped in it (their glow is its own
        /// part, under them), a vent with its slider and two knobs on the front, an ash drawer, a rail
        /// along it; on an open stand of square legs with a shelf carrying a bag of charcoal and a
        /// crate of corn.
        /// </summary>
        static void Firebox(B.Model m)
        {
            float mx = (GrillLeft + GrillRight) * 0.5f, mz = (GrillFront + GrillBack) * 0.5f;
            float w = GrillRight - GrillLeft, d = GrillBack - GrillFront;

            // The stand.
            foreach (float x in new[] { GrillLeft + 0.03f, GrillRight - 0.03f })
            {
                foreach (float z in new[] { GrillFront + 0.03f, GrillBack - 0.03f })
                {
                    m.Box(G.Steel, new Vector3(x, (PadTop + StandTop) * 0.5f, z), new Vector3(0.04f, StandTop - PadTop, 0.04f), 0.008f);
                    m.Tube(G.Rubber, new Vector3(x, PadTop, z), new Vector3(x, PadTop + 0.02f, z), 0.028f, 10);
                }
            }

            m.Box(G.SteelDark, new Vector3(mx, StandShelf, mz), new Vector3(w - 0.02f, 0.016f, d - 0.02f), 0.005f);
            float shelfTop = StandShelf + 0.008f;
            CharcoalBag(m, new Vector3(-0.36f, shelfTop, 0.76f), 10f, 0.26f);
            CornCrate(m, new Vector3(0.18f, shelfTop, 0.76f), -5f, 5);

            // The box: a floor, four walls, the inside painted with soot down to the coals.
            float boxMid = (StandTop + BoxTop) * 0.5f, boxHeight = BoxTop - StandTop;
            m.Box(G.SteelDark, new Vector3(mx, StandTop + 0.02f, mz), new Vector3(w, 0.04f, d), 0.008f);
            m.Box(G.Steel, new Vector3(mx, boxMid, GrillFront + Wall * 0.5f), new Vector3(w, boxHeight, Wall), 0.01f, 2);
            m.Box(G.Steel, new Vector3(mx, boxMid, GrillBack - Wall * 0.5f), new Vector3(w, boxHeight, Wall), 0.01f, 2);
            m.Box(G.Steel, new Vector3(GrillLeft + Wall * 0.5f, boxMid, mz), new Vector3(Wall, boxHeight, d - 2f * Wall + 0.004f), 0.01f, 2);
            m.Box(G.Steel, new Vector3(GrillRight - Wall * 0.5f, boxMid, mz), new Vector3(Wall, boxHeight, d - 2f * Wall + 0.004f), 0.01f, 2);

            float inLeft = GrillLeft + Wall, inRight = GrillRight - Wall, inFront = GrillFront + Wall, inBack = GrillBack - Wall;
            float sootBottom = CoalY - 0.04f, sootMid = (sootBottom + BoxTop - 0.012f) * 0.5f, sootHeight = BoxTop - 0.012f - sootBottom;
            m.Box(Soot, new Vector3(mx, sootBottom, mz), new Vector3(inRight - inLeft, 0.02f, inBack - inFront), 0.004f);
            m.Box(Soot, new Vector3(mx, sootMid, inFront + 0.003f), new Vector3(inRight - inLeft, sootHeight, 0.006f), 0.002f);
            m.Box(Soot, new Vector3(mx, sootMid, inBack - 0.003f), new Vector3(inRight - inLeft, sootHeight, 0.006f), 0.002f);
            m.Box(Soot, new Vector3(inLeft + 0.003f, sootMid, mz), new Vector3(0.006f, sootHeight, inBack - inFront), 0.002f);
            m.Box(Soot, new Vector3(inRight - 0.003f, sootMid, mz), new Vector3(0.006f, sootHeight, inBack - inFront), 0.002f);

            // The coals: lumps heaped thick over the embers, so the glow shows only between them; ash greying a few.
            for (int k = 0; k < 260; k++)
            {
                float x = Mathf.Lerp(inLeft + 0.02f, inRight - 0.02f, (B.Hash(k * 3 + 1) % 1000) / 1000f);
                float z = Mathf.Lerp(inFront + 0.02f, inBack - 0.02f, (B.Hash(k * 3 + 2) % 1000) / 1000f);
                float s = 0.034f + 0.012f * B.Jitter(k * 3 + 3);
                Color c = (B.Hash(k * 7) % 6 == 0 ? Ash : Coal) * (1f + 0.12f * B.Jitter(k * 5));
                m.Box(c, new Vector3(x, CoalY + s * 0.2f + 0.008f * B.Jitter(k + 11), z), new Vector3(s * 1.2f, s * 0.75f, s), s * 0.3f,
                      Quaternion.Euler(20f * B.Jitter(k + 12), 180f * B.Jitter(k + 13), 20f * B.Jitter(k + 14)));
            }

            // The front: a vent with its slider, two knobs, a red name plate, the ash drawer and the rail.
            float face = GrillFront - 0.002f;
            m.Box(G.SteelDark, new Vector3(-0.3f, 0.7f, face), new Vector3(0.3f, 0.07f, 0.008f), 0.004f);
            for (int k = 0; k < 7; k++) m.Box(Soot, new Vector3(-0.42f + k * 0.04f, 0.7f, face - 0.004f), new Vector3(0.018f, 0.05f, 0.002f), 0.001f);
            m.Box(G.Chrome, new Vector3(-0.2f, 0.7f, face - 0.008f), new Vector3(0.03f, 0.024f, 0.012f), 0.004f);

            foreach (float x in new[] { 0.1f, 0.26f })
                m.Tube((p, n) => n.z < -0.8f ? G.Chrome : G.Bakelite, new Vector3(x, 0.7f, face), new Vector3(x, 0.7f, face - 0.03f), 0.024f, 0.02f, 12);

            m.Box(G.Chrome, new Vector3(0.4f, 0.71f, face - 0.002f), new Vector3(0.1f, 0.04f, 0.004f), 0.003f);
            m.Box(DinerRed, new Vector3(0.4f, 0.71f, face - 0.0045f), new Vector3(0.08f, 0.022f, 0.002f), 0.001f);

            m.Box(G.SteelDark, new Vector3(mx, StandTop + 0.07f, face - 0.006f), new Vector3(w - 0.12f, 0.09f, 0.014f), 0.006f);
            m.Tube(G.Chrome, new Vector3(mx - 0.12f, StandTop + 0.07f, face - 0.03f), new Vector3(mx + 0.12f, StandTop + 0.07f, face - 0.03f), 0.008f, 8);
            foreach (float x in new[] { mx - 0.12f, mx + 0.12f })
                m.Tube(G.Chrome, new Vector3(x, StandTop + 0.07f, face - 0.012f), new Vector3(x, StandTop + 0.07f, face - 0.03f), 0.006f, 6);

            const float railY = 0.79f;
            float railZ = GrillFront - 0.05f;
            m.Tube(G.Chrome, new Vector3(GrillLeft + 0.01f, railY, railZ), new Vector3(GrillRight - 0.01f, railY, railZ), 0.011f, 10);
            foreach (float x in new[] { GrillLeft + 0.04f, GrillRight - 0.04f })
                m.Box(G.Steel, new Vector3(x, railY, (railZ + GrillFront) * 0.5f), new Vector3(0.018f, 0.02f, GrillFront - railZ), 0.005f);
        }

        /// <summary>The grate: iron bars running front to back on two cross rails, set in a steel flange round the firebox's top.</summary>
        static void Grate(B.Model m)
        {
            float mx = (GrillLeft + GrillRight) * 0.5f, mz = (GrillFront + GrillBack) * 0.5f;

            // The flange: a lip round the top of the walls the grate rests inside.
            m.Box(G.SteelLight, new Vector3(mx, BoxTop + 0.006f, GrillFront + 0.03f), new Vector3(GrillRight - GrillLeft + 0.01f, 0.012f, 0.07f), 0.004f);
            m.Box(G.SteelLight, new Vector3(mx, BoxTop + 0.006f, GrillBack - 0.03f), new Vector3(GrillRight - GrillLeft + 0.01f, 0.012f, 0.07f), 0.004f);
            m.Box(G.SteelLight, new Vector3(GrillLeft + 0.03f, BoxTop + 0.006f, mz), new Vector3(0.07f, 0.012f, GrillBack - GrillFront - 0.12f), 0.004f);
            m.Box(G.SteelLight, new Vector3(GrillRight - 0.03f, BoxTop + 0.006f, mz), new Vector3(0.07f, 0.012f, GrillBack - GrillFront - 0.12f), 0.004f);

            float gz = (GrateFront + GrateBack) * 0.5f;
            foreach (float z in new[] { GrateFront + 0.03f, GrateBack - 0.03f })
                m.Box(Iron, new Vector3((GrateLeft + GrateRight) * 0.5f, GrateTop - 0.03f, z), new Vector3(GrateRight - GrateLeft, 0.016f, 0.016f), 0.004f);

            float pitch = (GrateRight - GrateLeft) / Bars;
            for (int i = 0; i <= Bars; i++)
            {
                float x = GrateLeft + i * pitch;
                m.Box(Iron, new Vector3(x, GrateTop - 0.011f, gz), new Vector3(0.016f, 0.022f, GrateBack - GrateFront), 0.004f);
                m.Box(IronEdge, new Vector3(x, GrateTop - 0.0015f, gz), new Vector3(0.01f, 0.003f, GrateBack - GrateFront - 0.01f), 0.0012f);
            }
        }

        /// <summary>
        /// The extraction hood over the back of the grill: a stainless canopy with a darker lip, a
        /// collar and the duct going up, banded; on two posts from the floor behind the stand, a grill
        /// brush and a fork hung from the right one.
        /// </summary>
        static void Hood(B.Model m)
        {
            float mx = (HoodLeft + HoodRight) * 0.5f;
            foreach (float x in new[] { HoodLeft + 0.03f, HoodRight - 0.03f })
                m.Tube(G.Steel, new Vector3(x, PadTop, PostZ), new Vector3(x, HoodBottom + 0.02f, PostZ), 0.018f, 10);

            m.Box(G.Steel, new Vector3(mx, (HoodBottom + HoodTop) * 0.5f, (HoodFront + HoodBack) * 0.5f),
                  new Vector3(HoodRight - HoodLeft, HoodTop - HoodBottom, HoodBack - HoodFront), 0.025f, 2);
            m.Box(G.SteelDark, new Vector3(mx, HoodBottom + 0.02f, HoodFront - 0.003f), new Vector3(HoodRight - HoodLeft - 0.03f, 0.04f, 0.008f), 0.004f);
            m.Box(G.SteelLight, new Vector3(mx, HoodTop - 0.01f, (HoodFront + HoodBack) * 0.5f), new Vector3(HoodRight - HoodLeft - 0.04f, 0.012f, HoodBack - HoodFront - 0.04f),
                  0.004f);

            // A name plate on its front in the diner's red.
            m.Box(G.Chrome, new Vector3(mx, (HoodBottom + HoodTop) * 0.5f + 0.02f, HoodFront - 0.004f), new Vector3(0.22f, 0.06f, 0.006f), 0.004f);
            m.Box(DinerRed, new Vector3(mx, (HoodBottom + HoodTop) * 0.5f + 0.02f, HoodFront - 0.0075f), new Vector3(0.18f, 0.03f, 0.002f), 0.001f);

            // The collar and the duct.
            const float ductX = -0.05f, ductZ = 0.99f;
            m.Box(G.SteelDark, new Vector3(ductX, HoodTop + 0.03f, ductZ), new Vector3(0.4f, 0.06f, 0.26f), 0.01f);
            m.Box(G.Steel, new Vector3(ductX, (HoodTop + 0.06f + DuctTop) * 0.5f, ductZ), new Vector3(0.3f, DuctTop - HoodTop - 0.06f, 0.2f), 0.012f, 2);
            foreach (float y in new[] { 1.86f, 1.96f })
                m.Box(G.SteelDark, new Vector3(ductX, y, ductZ), new Vector3(0.31f, 0.018f, 0.21f), 0.005f);

            // Tools hung on the right post.
            float px = HoodRight - 0.03f, hookY = 1.3f;
            K.Hook(m, new Vector3(px - 0.02f, hookY, PostZ - 0.02f));
            Vector3 top = new Vector3(px - 0.02f, hookY - 0.032f, PostZ - 0.02f);
            m.Tube(B.Walnut, top, top - Vector3.up * 0.16f, 0.01f, 8);
            m.Box(G.SteelDark, top - Vector3.up * 0.22f, new Vector3(0.012f, 0.1f, 0.05f), 0.004f);
            for (int k = 0; k < 5; k++) m.Box(G.Chrome, top - Vector3.up * (0.18f + k * 0.018f) + Vector3.forward * -0.03f, new Vector3(0.01f, 0.006f, 0.012f), 0.002f);

            K.Hook(m, new Vector3(px - 0.02f, hookY - 0.06f, PostZ + 0.03f));
            Vector3 fork = new Vector3(px - 0.02f, hookY - 0.092f, PostZ + 0.03f);
            m.Tube(B.Walnut, fork, fork - Vector3.up * 0.12f, 0.009f, 8);
            m.Tube(G.Steel, fork - Vector3.up * 0.12f, fork - Vector3.up * 0.2f, 0.004f, 6);
            foreach (float s in new[] { -1f, 1f })
                m.Tube(G.Steel, fork - Vector3.up * 0.2f, fork - Vector3.up * 0.3f + Vector3.forward * (s * 0.012f), 0.003f, 5);
        }

        /// <summary>On the prep board, from the bench's middle across and front to back: the onion half the knife cuts, and the pile it throws the pieces onto.</summary>
        static readonly Vector2 OnionCut = new Vector2(0.06f, 0.425f);
        static readonly Vector2 OnionPile = new Vector2(0.1f, 0.33f);

        /// <summary>
        /// The knife on the prep board, its own part: while the grill cooks it rocks through the onion
        /// half, its blade across the board and its handle out to the left, and the pieces hop onto
        /// the pile (a <see cref="KitchenToolMotion"/>).
        /// </summary>
        static void Chopping(Transform model, Material painted)
        {
            float cx = (BenchLeft + BenchRight) * 0.5f, on = BenchTop + 0.025f;
            Mesh knife = SaveMesh(T.ChopKnife(), "GrillStation_Knife");
            Mesh piece = SaveMesh(T.Piece(OnionGrilled * 1.15f, Onion, new Vector3(0.024f, 0.016f, 0.02f)), "GrillStation_OnionPiece");

            // Turned so the blade runs back toward -X from its tip, right of the onion.
            var tip = new Vector3(cx + OnionCut.x + 0.07f, on, OnionCut.y);
            Transform blade = T.Tool(model, "Knife", knife, tip, Quaternion.Euler(0f, 90f, 0f), painted);
            Transform[] pieces = T.Pieces(model, "OnionBit", piece, 6, tip, painted);
            blade.gameObject.AddComponent<KitchenToolMotion>().Configure(KitchenToolMotion.Motion.Chop, 3f, 24f, Vector3.back)
                 .Cuts(pieces, new Vector3(cx + OnionCut.x, on + 0.01f, OnionCut.y - 0.01f), new Vector3(cx + OnionPile.x, on, OnionPile.y));
        }

        /// <summary>Half an onion lying cut face down on the board: its golden skin domed over it.</summary>
        static void OnionHalf(B.Model m, Vector3 foot, float r)
        {
            m.Lathe((p, n) => n.y < 0.2f ? Onion : B.Hex("9C6A3A"), foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r, 0f), new Vector2(r, 0.003f), new Vector2(r * 0.95f, r * 0.45f), new Vector2(r * 0.7f, r * 0.78f),
                new Vector2(r * 0.3f, r * 0.95f), new Vector2(0f, r),
            }, 16);
        }

        // --- The bench, the table, the floor ------------------------------------------------------

        /// <summary>
        /// The prep bench on the left: a tray of fish at the front; chicken and onion on a board in the
        /// middle, two skewers threaded and a bundle of bare ones beside them; a bowl of tomatoes, a
        /// crock of skewers and a bottle of oil at the back; corn in a crate and a sack underneath.
        /// </summary>
        static void PrepBench(B.Model m)
        {
            G.Bench(m, BenchLeft, BenchRight, BenchFront, BenchBack, BenchTop, BenchShelf, 410);
            float cx = (BenchLeft + BenchRight) * 0.5f, top = BenchTop;

            // The fish on their tray.
            var tray = new Vector3(cx, top, -0.28f);
            m.Box(G.Steel, tray + Vector3.up * 0.006f, new Vector3(0.34f, 0.012f, 0.28f), 0.004f);
            foreach (float s in new[] { -1f, 1f })
            {
                m.Box(G.SteelLight, tray + new Vector3(0f, 0.014f, s * 0.135f), new Vector3(0.34f, 0.018f, 0.01f), 0.004f);
                m.Box(G.SteelLight, tray + new Vector3(s * 0.165f, 0.014f, 0f), new Vector3(0.01f, 0.018f, 0.28f), 0.004f);
            }

            Fish(m, tray + new Vector3(0f, 0.012f + FishThick * 0.5f, -0.055f), Quaternion.Euler(0f, 4f, 0f), 0.28f, FishBack, FishBelly, false);
            Fish(m, tray + new Vector3(0.01f, 0.012f + FishThick * 0.5f, 0.06f), Quaternion.Euler(0f, 176f, 0f), 0.27f, FishBack, FishBelly, false);

            // The board: chicken, onion, skewers.
            Quaternion boardTurn = Quaternion.Euler(0f, -3f, 0f);
            var board = new Vector3(cx, top, 0.24f);
            m.Box(B.Hex("C2AB88"), board + Vector3.up * 0.0125f, new Vector3(0.36f, 0.025f, 0.44f), 0.008f, boardTurn, 2);
            float on = top + 0.025f;
            for (int k = 0; k < 9; k++)
            {
                var at = new Vector3(cx - 0.08f + 0.04f * B.Jitter(k + 700), on + 0.014f + (k > 5 ? 0.012f : 0f), 0.36f + 0.04f * B.Jitter(k + 710));
                m.Box(ChickenRaw * (1f + 0.05f * B.Jitter(k + 720)), at, Vector3.one * 0.036f, 0.01f, Quaternion.Euler(10f * B.Jitter(k + 730), 45f * B.Jitter(k + 740), 0f));
            }

            // Onion cut so far, heaped where the knife throws it, and the half it is cutting.
            for (int k = 0; k < 4; k++)
            {
                var at = new Vector3(cx + OnionPile.x + 0.025f * B.Jitter(k + 750), on + 0.012f, OnionPile.y + 0.025f * B.Jitter(k + 760));
                m.Box(Onion, at, new Vector3(0.036f, 0.024f, 0.028f), 0.006f, Quaternion.Euler(0f, 30f * B.Jitter(k + 770), 0f));
            }

            OnionHalf(m, new Vector3(cx + OnionCut.x, on, OnionCut.y), 0.036f);

            foreach (float z in new[] { 0.12f, 0.2f })
                RawSkewer(m, new Vector3(cx, on + Chunk * 0.4f, z));

            for (int k = 0; k < 7; k++)
            {
                float z = 0.03f + k * 0.008f;
                m.Tube(SkewerWood, new Vector3(cx - 0.17f, on + 0.004f + (k % 2) * 0.006f, z), new Vector3(cx + 0.15f, on + 0.004f + (k % 2) * 0.006f, z + 0.01f), 0.0035f, 5);
            }

            // At the back.
            var bowl = new Vector3(cx - 0.08f, top, 0.78f);
            K.Bowl(m, bowl, 0.1f);
            K.Tomato(m, bowl + new Vector3(-0.03f, 0.03f, -0.02f), 0.038f, 3);
            K.Tomato(m, bowl + new Vector3(0.035f, 0.03f, 0.005f), 0.037f, 4);
            K.Tomato(m, bowl + new Vector3(0f, 0.055f, 0.03f), 0.036f, 5);

            var crock = new Vector3(cx + 0.12f, top, 0.95f);
            K.Crock(m, crock, 0.045f, 0.1f);
            for (int k = 0; k < 6; k++)
            {
                Vector3 lean = new Vector3(0.03f * B.Jitter(k + 780), 0.24f, 0.03f * B.Jitter(k + 790));
                m.Tube(SkewerWood, crock + Vector3.up * 0.04f, crock + lean, 0.003f, 5);
            }

            K.Bottle(m, new Vector3(cx + 0.14f, top, 0.74f), 0.036f, 0.22f, GlassOil);

            // Underneath.
            float shelfTop = BenchShelf + 0.012f;
            CornCrate(m, new Vector3(cx, shelfTop, -0.15f), 3f, 51);
            B.Sack(m, new Vector3(cx, shelfTop, 0.62f), 0.13f, 0.34f, 0.03f, 23);
        }

        /// <summary>
        /// The landing table on the right: a steel top on square legs, a sheet pan of grilled corn and
        /// skewers resting on it, plates and salt and pepper at the back; a bin of charcoal underneath.
        /// The tongs rest on it too, as their own part.
        /// </summary>
        static void LandingTable(B.Model m)
        {
            float mx = (TableLeft + TableRight) * 0.5f, mz = (TableFront + TableBack) * 0.5f;
            float w = TableRight - TableLeft, d = TableBack - TableFront;

            foreach (float x in new[] { TableLeft + 0.025f, TableRight - 0.025f })
                foreach (float z in new[] { TableFront + 0.025f, TableBack - 0.025f })
                {
                    m.Box(G.Steel, new Vector3(x, (PadTop + TableTop) * 0.5f, z), new Vector3(0.036f, TableTop - PadTop, 0.036f), 0.007f);
                    m.Tube(G.Rubber, new Vector3(x, PadTop, z), new Vector3(x, PadTop + 0.02f, z), 0.026f, 10);
                }

            m.Box(G.Steel, new Vector3(mx, TableTop - 0.015f, mz), new Vector3(w, 0.03f, d), 0.006f, 2);
            m.Box(G.SteelLight, new Vector3(mx, TableTop + 0.006f, TableBack - 0.008f), new Vector3(w, 0.03f, 0.016f), 0.004f);
            m.Box(G.SteelDark, new Vector3(mx, TableShelf, mz), new Vector3(w - 0.02f, 0.016f, d - 0.02f), 0.005f);

            // The pan of grilled food resting.
            var pan = new Vector3(0.96f, TableTop, 0.66f);
            m.Box(G.Steel, pan + Vector3.up * 0.005f, new Vector3(0.3f, 0.01f, 0.36f), 0.003f);
            foreach (float s in new[] { -1f, 1f })
            {
                m.Box(G.SteelLight, pan + new Vector3(0f, 0.012f, s * 0.175f), new Vector3(0.3f, 0.016f, 0.01f), 0.004f);
                m.Box(G.SteelLight, pan + new Vector3(s * 0.145f, 0.012f, 0f), new Vector3(0.01f, 0.016f, 0.36f), 0.004f);
            }

            float onPan = pan.y + 0.01f;
            for (int k = 0; k < 2; k++)
            {
                Vector3 c = new Vector3(pan.x - 0.06f + k * 0.09f, onPan + CobRadius * 0.9f, pan.z - 0.08f);
                StillCob(m, c, Quaternion.Euler(0f, 90f + 6f * B.Jitter(k + 800), 0f), CobRadius * 0.9f, CobLength * 0.9f);
            }

            for (int k = 0; k < 2; k++)
                StillSkewer(m, new Vector3(pan.x, onPan + Chunk * 0.4f, pan.z + 0.06f + k * 0.07f), Quaternion.Euler(0f, 4f * B.Jitter(k + 810), 0f));

            for (int k = 0; k < 5; k++) G.DinerPlate(m, new Vector3(0.78f, TableTop + k * 0.018f, 0.98f), 0.1f);
            K.PepperMill(m, new Vector3(0.94f, TableTop, 1.0f));
            K.Tin(m, new Vector3(1.03f, TableTop, 1.0f), 0.028f, 0.08f, G.Cream);

            // The charcoal bin under it.
            var bin = new Vector3(mx, TableShelf + 0.008f, 0.78f);
            m.Box(G.SteelDark, bin + Vector3.up * 0.09f, new Vector3(0.4f, 0.18f, 0.4f), 0.01f);
            for (int k = 0; k < 30; k++)
            {
                float x = 0.16f * B.Jitter(k + 820), z = 0.16f * B.Jitter(k + 830);
                m.Box(Coal * (1f + 0.15f * B.Jitter(k + 840)), bin + new Vector3(x, 0.185f, z), new Vector3(0.04f, 0.024f, 0.034f), 0.008f,
                      Quaternion.Euler(0f, 90f * B.Jitter(k + 850), 15f * B.Jitter(k + 860)));
            }
        }

        /// <summary>A fire extinguisher on the floor at the front right, in the diner's red, and a sack of charcoal by it.</summary>
        static void OnTheFloor(B.Model m)
        {
            var foot = new Vector3(1.03f, PadTop, -0.4f);
            const float r = 0.065f, h = 0.4f;
            m.Lathe((p, n) =>
            {
                float y = p.y - foot.y;
                return y > h * 0.42f && y < h * 0.62f ? G.Cream : DinerRed;
            }, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r - 0.008f, 0f), new Vector2(r, 0.012f), new Vector2(r, h - 0.04f), new Vector2(r * 0.7f, h - 0.005f),
                new Vector2(0.02f, h), new Vector2(0f, h),
            }, 18);
            m.Box(G.Bakelite, foot + Vector3.up * (h + 0.025f), new Vector3(0.05f, 0.05f, 0.045f), 0.01f);
            m.Box(G.Chrome, foot + new Vector3(0f, h + 0.06f, -0.03f), new Vector3(0.016f, 0.012f, 0.1f), 0.004f, Quaternion.Euler(-15f, 0f, 0f));
            m.Tube(G.Bakelite, foot + new Vector3(0.03f, h + 0.02f, 0f), foot + new Vector3(0.075f, h - 0.1f, -0.02f), 0.008f, 6);
            m.Tube(G.Bakelite, foot + new Vector3(0.075f, h - 0.1f, -0.02f), foot + new Vector3(0.07f, h - 0.2f, -0.04f), 0.008f, 6);

            CharcoalBag(m, new Vector3(0.74f, PadTop, -0.3f), -14f, 0.36f);
        }

        /// <summary>A paper sack of charcoal, folded over at the top, a red band printed round it.</summary>
        static void CharcoalBag(B.Model m, Vector3 foot, float yaw, float height)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            var size = new Vector3(0.3f, height, 0.16f);
            m.Box(Charcoal, foot + Vector3.up * (size.y * 0.5f), size, 0.04f, q, 2);
            m.Box(DinerRed * 0.9f, foot + Vector3.up * (size.y * 0.55f), new Vector3(size.x + 0.004f, 0.06f, size.z + 0.004f), 0.03f, q, 2);
            m.Box(Charcoal * 1.15f, foot + Vector3.up * (size.y + 0.015f) + q * new Vector3(0f, 0f, 0.02f), new Vector3(size.x - 0.02f, 0.04f, 0.1f), 0.015f,
                  q * Quaternion.Euler(-25f, 0f, 0f), 2);
        }

        /// <summary>An open crate of corn still in its husks, the ears lying across it.</summary>
        static void CornCrate(B.Model m, Vector3 foot, float yaw, int key)
        {
            var size = new Vector3(0.42f, 0.15f, 0.34f);
            K.OpenCrate(m, foot, size, yaw, key);
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            for (int k = 0; k < 6; k++)
            {
                float z = -0.11f + (k % 3) * 0.11f;
                float y = size.y - 0.02f + (k / 3) * 0.045f;
                Vector3 c = foot + q * new Vector3(0.02f * B.Jitter(k + key), y, z + (k / 3) * 0.05f);
                Quaternion along = q * Quaternion.Euler(0f, 90f + 8f * B.Jitter(k + key + 5), 0f);
                K.Ball(m, (p, n) => Color.Lerp(Husk * 0.9f, HuskDry, 0.4f + 0.3f * Mathf.Sin(p.x * 80f + k)), c, new Vector3(0.042f, 0.042f, 0.17f), along, 12, 8);
                m.Tube(HuskDry, c + along * new Vector3(0f, 0f, 0.16f), c + along * new Vector3(0f, 0.01f, 0.23f), 0.012f, 6);
            }
        }

        // --- Food, still and moving ---------------------------------------------------------------

        /// <summary>
        /// A fish lying on its side at <paramref name="centre"/> (the middle of its body, half way up its
        /// thickness), its snout toward -X of <paramref name="turn"/>: the body round in section, deep
        /// at the shoulder and narrowing to the tail, darker along its back (+Z) than its belly; a
        /// forked tail, fins along its back and under its belly, an eye on each side. Grilled, bars of
        /// char cross both its flanks.
        /// </summary>
        static void Fish(B.Model m, Vector3 centre, Quaternion turn, float length, Color back, Color belly, bool grilled)
        {
            float body = length * 0.78f, tail = length * 0.22f;
            float half = FishThick * 0.5f, depth = length * 0.15f;
            float X(float s) => (s - 0.5f) * body - tail * 0.5f;
            float D(float s) => depth * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Lerp(0f, 0.88f, Mathf.Pow(s, 0.8f)))), 0.6f) * (1f - 0.55f * s * s * s);
            float T(float s) => half * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Lerp(0f, 0.9f, Mathf.Pow(s, 0.85f)))), 0.5f) * (1f - 0.5f * s * s);
            Quaternion back3 = Quaternion.Inverse(turn);

            m.Surface((p, n) =>
            {
                Vector3 l = back3 * (p - centre);
                float across = Mathf.InverseLerp(-depth, depth, l.z);
                Color c = Color.Lerp(belly, back, K.Smooth(across));
                if (!grilled) return c;

                float bar = Mathf.Repeat(l.x * 0.7f + l.z * 0.7f, 0.055f) / 0.055f;
                float onFlank = K.Smooth((Mathf.Abs(l.y) - half * 0.3f) / (half * 0.5f));
                return Color.Lerp(c, Char, onFlank * K.Smooth(1f - Mathf.Abs(bar - 0.5f) * 5f) * 0.9f);
            }, 20, 36, (u, v) =>
            {
                float a = u * Mathf.PI * 2f;
                return centre + turn * new Vector3(X(v), Mathf.Sin(a) * T(v), Mathf.Cos(a) * D(v));
            }, true);

            // The open end at the tail's wrist, closed.
            K.Ball(m, (p, n) => Color.Lerp(belly, back, 0.5f), centre + turn * new Vector3(X(1f), 0f, 0f), new Vector3(0.008f, T(1f), D(1f)), turn, 10, 6);

            // The tail's two lobes, the fins, the eyes.
            Vector3 wrist = centre + turn * new Vector3(X(1f) - 0.004f, 0f, 0f);
            foreach (float s in new[] { -1f, 1f })
            {
                Quaternion lobe = turn * Quaternion.Euler(0f, -s * 30f, 0f);
                m.Box(Fin, wrist + lobe * new Vector3(tail * 0.45f, 0f, 0f), new Vector3(tail * 0.95f, 0.004f, depth * 0.5f), 0.002f, lobe);
            }

            m.Box(Fin, centre + turn * new Vector3(X(0.55f), 0f, D(0.55f) + depth * 0.15f), new Vector3(body * 0.38f, 0.004f, depth * 0.4f), 0.002f,
                  turn * Quaternion.Euler(0f, -8f, 0f));
            m.Box(Fin, centre + turn * new Vector3(X(0.7f), 0f, -D(0.7f) - depth * 0.08f), new Vector3(body * 0.18f, 0.004f, depth * 0.28f), 0.002f,
                  turn * Quaternion.Euler(0f, 10f, 0f));

            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 eye = centre + turn * new Vector3(X(0.08f), s * T(0.08f) * 0.95f, depth * 0.15f);
                K.Disc(m, (p, n) => FishBelly * 1.1f, eye, turn * (Vector3.up * s), 0.0095f, 10);
                K.Disc(m, (p, n) => Soot, eye + turn * (Vector3.up * (s * 0.0008f)), turn * (Vector3.up * s), 0.0055f, 10);
            }
        }

        /// <summary>
        /// A husked cob, its origin on its axis in its middle, lying along X: rows of kernels bumping
        /// its surface, the grooves between them darker, charred in rings where it has lain on the bars.
        /// </summary>
        static B.Model GrilledCob()
        {
            var m = new B.Model();
            StillCob(m, Vector3.zero, Quaternion.Euler(0f, 90f, 0f), CobRadius, CobLength);
            return m;
        }

        /// <summary>A grilled cob at <paramref name="centre"/> lying along <paramref name="turn"/>'s forward.</summary>
        static void StillCob(B.Model m, Vector3 centre, Quaternion turn, float radius, float length)
        {
            Quaternion back = Quaternion.Inverse(turn);
            float Kernels(float u, float v) => Mathf.Abs(Mathf.Sin(u * Mathf.PI * 14f)) * Mathf.Abs(Mathf.Sin(v * Mathf.PI * 16f + (Mathf.Floor(u * 14f) % 2f) * 1.5f));
            float Girth(float v) => radius * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Lerp(0.03f, 0.97f, v))), 0.3f) * (1f - 0.18f * v);

            m.Surface((p, n) =>
            {
                Vector3 l = back * (p - centre);
                float v = l.z / length + 0.5f;
                float a = Mathf.Atan2(l.y, l.x) / (Mathf.PI * 2f);
                Color c = Color.Lerp(Kernel * 0.78f, KernelLight, Kernels(a, v));
                float ring = Mathf.Repeat(v * 5f + 0.2f, 1f);
                float charred = K.Smooth(1f - Mathf.Abs(ring - 0.5f) * 6f) * (0.55f + 0.45f * Mathf.Max(0f, Mathf.Sin(a * Mathf.PI * 2f + 0.6f)));
                return Color.Lerp(c, Char, charred * 0.85f);
            }, 36, 40, (u, v) =>
            {
                // Round the other way from the angle's sense, so the surface faces out.
                float a = -u * Mathf.PI * 2f;
                float r = Girth(v) * (1f + 0.07f * Kernels(u, v));
                return centre + turn * new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, (v - 0.5f) * length);
            }, true);

            m.Tube(Husk * 0.8f, centre + turn * new Vector3(0f, 0f, -length * 0.5f - 0.025f), centre + turn * new Vector3(0f, 0f, -length * 0.45f), radius * 0.45f, 8);
        }

        /// <summary>A skewer, its origin on the stick in its middle, lying along X: chicken and onion threaded on in turn, charred where the bars were.</summary>
        static B.Model Skewer()
        {
            var m = new B.Model();
            StillSkewer(m, Vector3.zero, Quaternion.identity);
            return m;
        }

        static void StillSkewer(B.Model m, Vector3 centre, Quaternion turn)
        {
            m.Tube(SkewerWood, centre + turn * new Vector3(-0.17f, 0f, 0f), centre + turn * new Vector3(0.17f, 0f, 0f), 0.004f, 6);
            m.Tube((p, n) => SkewerWood, centre + turn * new Vector3(0.17f, 0f, 0f), centre + turn * new Vector3(0.19f, 0f, 0f), 0.004f, 0.0008f, 6);

            for (int i = 0; i < 5; i++)
            {
                Vector3 at = centre + turn * new Vector3(-0.115f + i * 0.058f, 0f, 0f);
                Quaternion q = turn * Quaternion.Euler(30f * B.Jitter(i + 900), 0f, 0f);
                if (i % 2 == 0)
                {
                    m.Box(Chicken * (1f + 0.05f * B.Jitter(i + 910)), at, new Vector3(Chunk, Chunk * 0.92f, Chunk), 0.013f, q, 2);
                    foreach (float s in new[] { -1f, 1f })
                        foreach (float o in new[] { -0.012f, 0.008f })
                            m.Box(Char, at + q * new Vector3(o, s * Chunk * 0.46f, 0f), new Vector3(0.006f, 0.004f, Chunk * 0.85f), 0.0015f, q * Quaternion.Euler(0f, 20f, 0f));
                }
                else
                {
                    for (int l = -1; l <= 1; l++)
                        m.Box(l == 0 ? Onion : OnionGrilled, at + q * new Vector3(l * 0.008f, 0f, 0f), new Vector3(0.007f, Chunk * 0.85f, Chunk * (0.95f - 0.12f * Mathf.Abs(l))),
                              0.003f, q);
                    foreach (float s in new[] { -1f, 1f })
                        m.Box(Char, at + q * new Vector3(0f, s * Chunk * 0.43f, 0f), new Vector3(0.02f, 0.004f, Chunk * 0.6f), 0.0015f, q);
                }
            }
        }

        /// <summary>A skewer threaded and waiting to go on the grill: raw chicken and onion.</summary>
        static void RawSkewer(B.Model m, Vector3 centre)
        {
            m.Tube(SkewerWood, centre + new Vector3(-0.17f, 0f, 0f), centre + new Vector3(0.17f, 0f, 0f), 0.004f, 6);
            for (int i = 0; i < 5; i++)
            {
                Vector3 at = centre + new Vector3(-0.115f + i * 0.058f, 0f, 0f);
                if (i % 2 == 0) m.Box(ChickenRaw, at, new Vector3(Chunk * 0.85f, Chunk * 0.75f, Chunk * 0.85f), 0.012f, Quaternion.Euler(20f * B.Jitter(i + 950), 0f, 0f));
                else m.Box(Onion, at, new Vector3(0.02f, Chunk * 0.7f, Chunk * 0.8f), 0.004f);
            }
        }

        /// <summary>The fish on the grill, its origin in its middle: both flanks grilled golden with bars of char, so it looks the same whichever way up.</summary>
        static B.Model GrilledFish()
        {
            var m = new B.Model();
            Fish(m, Vector3.zero, Quaternion.identity, 0.3f, FishGrilledBack, FishGrilledBelly, true);
            return m;
        }

        /// <summary>Half a tomato grilling cut face down, its origin on the grate: a red dome, blistered and blackened in patches.</summary>
        static B.Model TomatoHalf()
        {
            var m = new B.Model();
            const float r = 0.04f;
            m.Lathe((p, n) =>
            {
                float blister = Mathf.Sin(p.x * 140f) * Mathf.Sin(p.z * 120f + p.y * 60f);
                return Color.Lerp(TomatoSkin, Char, K.Smooth((blister - 0.55f) / 0.3f) * 0.8f);
            }, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r, 0f), new Vector2(r, 0.004f), new Vector2(r * 0.96f, r * 0.45f), new Vector2(r * 0.75f, r * 0.75f),
                new Vector2(r * 0.4f, r * 0.9f), new Vector2(0f, r * 0.93f),
            }, 18);
            return m;
        }

        /// <summary>
        /// The tongs, their origin between the tips of the jaws, lying flat with the handles back
        /// toward -Z: two steel arms spreading from the hinge, scalloped jaws, black grips.
        /// </summary>
        static B.Model Tongs()
        {
            // Chunkier than life, with grips in the diner's red: thin steel was lost against the grate.
            var m = new B.Model();
            var hinge = new Vector3(0f, 0.022f, -0.32f);
            foreach (float s in new[] { -1f, 1f })
            {
                var tip = new Vector3(s * 0.02f, 0f, 0f);
                var bend = new Vector3(s * 0.032f, 0.01f, -0.11f);
                m.Tube(G.Chrome, tip, bend, 0.009f, 8);
                m.Tube(G.Chrome, bend, hinge + new Vector3(s * 0.011f, 0f, 0f), 0.009f, 8);
                m.Box(G.SteelLight, tip + new Vector3(-s * 0.004f, 0.003f, 0.01f), new Vector3(0.018f, 0.026f, 0.045f), 0.006f);
                m.Tube(DinerRed, hinge + new Vector3(s * 0.016f, 0.003f, 0.12f), hinge + new Vector3(s * 0.012f, 0.003f, 0.012f), 0.014f, 10);
            }

            m.Tube(G.Steel, hinge + new Vector3(-0.016f, 0f, 0f), hinge + new Vector3(0.016f, 0f, 0f), 0.012f, 8);
            m.Tube(G.Chrome, hinge + new Vector3(0f, 0f, -0.008f), hinge + new Vector3(0f, 0f, -0.04f), 0.005f, 6);
            return m;
        }

        /// <summary>
        /// The embers among the coals, their origin at the coals' foot in the middle of the firebox: bits
        /// glowing in the gaps between the lumps (and on top of a few), the colour coming into them. A
        /// whole glowing sheet under the coals read as a tray of orange.
        /// </summary>
        static B.Model CoalBed()
        {
            var m = new B.Model();
            float left = GrillLeft + Wall + 0.02f, right = GrillRight - Wall - 0.02f, front = GrillFront + Wall + 0.02f, back = GrillBack - Wall - 0.02f;
            for (int k = 0; k < 170; k++)
            {
                float x = Mathf.Lerp(left, right, (B.Hash(k * 5 + 4001) % 1000) / 1000f);
                float z = Mathf.Lerp(front, back, (B.Hash(k * 5 + 4002) % 1000) / 1000f);
                float s = 0.024f + 0.008f * B.Jitter(k + 4003);
                var shape = new SmoothMesh(1);
                shape.RoundedBox(0, new Vector3(x, 0.006f + 0.01f * B.Jitter(k + 4004), z), new Vector3(s * 1.3f, s * 0.6f, s), s * 0.25f,
                                 Quaternion.Euler(0f, 180f * B.Jitter(k + 4005), 0f), 1);
                m.Add(shape, Color.white);
            }

            return m;
        }

        /// <summary>
        /// The flames, in a space on the coals: tongues standing in the slots between the bars, thin
        /// across a slot and long along it, so they lick up through the gaps - <paramref name="set"/>
        /// 0 the tall orange ones, 1 the shorter yellow ones among them.
        /// </summary>
        static B.Model Flames(int set)
        {
            var m = new B.Model();
            float pitch = (GrateRight - GrateLeft) / Bars;
            float[] rows = set == 0 ? new[] { 0.56f, 0.73f, 0.9f } : new[] { 0.64f, 0.82f, 0.98f };
            Quaternion across = Quaternion.Euler(0f, 90f, 0f);

            for (int i = 0; i < Bars; i++)
            {
                float x = GrateLeft + (i + 0.5f) * pitch;
                for (int r = 0; r < rows.Length; r++)
                {
                    int key = set * 300 + i * 7 + r;
                    if (B.Hash(key) % 2 == 0) continue;

                    float z = rows[r] + 0.03f * B.Jitter(key + 1);
                    float h = (set == 0 ? 0.13f : 0.09f) * (1f + 0.25f * B.Jitter(key + 2));
                    K.Tongue(m, new Vector3(x, 0f, z), h, set == 0 ? 0.028f : 0.02f, new Vector3(0f, 0f, 0.01f * B.Jitter(key + 3)), 0.4f, across);
                }
            }

            return m;
        }
    }
}
