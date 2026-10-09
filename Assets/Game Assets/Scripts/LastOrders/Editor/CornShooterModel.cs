using System;
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.Defense;
using LastOrders.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using B = LastOrders.EditorTools.BuildingKit;
using S = LastOrders.EditorTools.SoupKitchenModel;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The Corn Shooter, modelled in code in the game's smooth low-poly style, and its prefab: a
    /// machine that throws whole corncobs.
    ///
    /// An old pitching machine re-geared for corn. A little single-cylinder engine at the back - farm
    /// green, finned head, a red flywheel on its side, a red fuel tank, an exhaust pipe that never stops
    /// smoking - drives two tyres on their sides through a pair of belts. They spin against each
    /// other; a cob drops out of the cage magazine above the breech, is pushed between them and leaves
    /// down the launch tube nose first. All of it on a deck plate on a turntable, on a cast pedestal on a
    /// pallet, a bushel basket of cobs and a sack beside it, a lantern on the engine for the night.
    /// Painted in its vertices on LastOrders/Stylized like every <see cref="BuildingKit"/> prop.
    ///
    /// What moves: the head turns to track (the aim pivot); the two tyres spin against each other and
    /// the flywheel turns while it has something in its sights, faster on each throw
    /// (<see cref="DefenseBarrelSpin"/>); the launch tube jolts back (<see cref="DefenseRecoil"/>); the
    /// cob waiting in the breech is gone as it is thrown and back once the next drops
    /// (<see cref="DefenseReload"/>). What it throws is a cob of its own - kernels in rows, a tuft of
    /// silk at its nose, the husk peeled back into four leaves that trail behind it like a dart's
    /// flights - spiralling nose first along its arc (<see cref="DefenseProjectiles"/>).
    ///
    /// It fits on the top of a wall bastion; local +Z is the way it faces at rest. Re-running rebuilds
    /// the meshes in place and re-dresses the prefab's own objects by name.
    /// </summary>
    public static class CornShooterModel
    {
        public const string PrefabPath = DefensePrefabGenerator.Folder + "/Defense_CornShooter.prefab";

        internal const string Folder = "Assets/Game Assets/Content/LastOrders/Defense/" + EnvKit.Style + "/CornShooter";

        /// <summary>Children of the first design - the corn sheller that sprayed kernels - removed on a rebuild.</summary>
        static readonly string[] Retired = { "Sheller/Head/Flywheel", "Sheller/Head/Spout", "Sheller/Head/SpentCobs", "Projectile_Kernels" };

        // --- Measurements (metres, model space; it faces +Z) ----------------------------------------

        const float PalletTop = 0.16f;

        /// <summary>The bearing at the top of the pedestal; the head turns on it.</summary>
        const float PivotY = 0.68f;

        // Head space, from the pivot.
        const float DeckTop = 0.25f;

        /// <summary>The line a cob travels along: through the breech, between the tyres, down the tube.</summary>
        const float LineY = 0.38f;

        const float BreechBack = -0.24f, BreechFront = 0.1f;
        const float WheelZ = 0.22f, WheelGap = 0.05f, WheelRadius = 0.17f, WheelWidth = 0.1f;
        const float TubeFrom = 0.4f, TubeLength = 0.5f, TubeRadius = 0.085f;

        static readonly Vector3 EngineAt = new Vector3(0f, DeckTop + 0.17f, -0.46f);
        static readonly Vector3 EngineSize = new Vector3(0.36f, 0.34f, 0.28f);

        static float WheelX => WheelRadius + WheelGap;

        // --- The palette -------------------------------------------------------------------------------

        /// <summary>Faded farm-machine green, muted the way the props' paint is.</summary>
        static readonly Color Green = B.Hex("56613F");
        static readonly Color GreenDark = B.Hex("47512F");
        static readonly Color Cream = B.Hex("AFA898");
        static readonly Color Red = B.Hex("8E3B33");
        static readonly Color IronDark = B.Hex("2F3236");
        static readonly Color Iron = B.Iron;
        static readonly Color Steel = B.Hex("6E7479");
        static readonly Color SteelLight = B.Hex("8D9398");
        static readonly Color BareIron = B.Hex("5F6468");
        static readonly Color Rubber = B.Hex("2A2A2B");
        static readonly Color RubberWorn = B.Hex("3B3A39");

        /// <summary>Dulled galvanised tin: mid grey, or its lit side clips white.</summary>
        static readonly Color Galvanised = B.Hex("8C9296");

        static readonly Color Rust = B.Hex("7D5E4A");
        static readonly Color Bore = B.Hex("141414");
        static readonly Color Belt = B.Hex("3A2E26");
        static readonly Color Wicker = B.Hex("A88F63");
        static readonly Color WickerBand = B.Hex("7E6A4A");

        internal static readonly Color Kernel = B.Hex("CDAA4C");
        internal static readonly Color KernelDark = B.Hex("B08E3A");
        internal static readonly Color KernelPale = B.Hex("D6BE76");
        internal static readonly Color Husk = B.Hex("9C9C64");
        internal static readonly Color HuskDark = B.Hex("7D7F4C");
        internal static readonly Color HuskPale = B.Hex("B3AE7C");
        internal static readonly Color Silk = B.Hex("7A5A3A");
        internal static readonly Color Stalk = B.Hex("8C8459");

        [MenuItem("Last Orders/Defense/Rebuild Corn Shooter (Model & Prefab)", priority = 45)]
        static void Run()
        {
            DefenseDefinition shooter = DefenseContentGenerator.GenerateCornShooter();
            Debug.Log(Apply(shooter, DefenseContentGenerator.LoadCornShooterMotion()));
        }

        /// <summary>The prefab, built with its meshes if it does not exist yet; left as it is if it does.</summary>
        public static GameObject Ensure(DefenseDefinition definition, DefenseMotionProfile motion)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;

            Debug.Log(Apply(definition, motion));
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        /// <summary>Builds every mesh and effect and dresses the prefab with them, creating it if it is missing. No dialogs.</summary>
        public static string Apply(DefenseDefinition definition, DefenseMotionProfile motion)
        {
            ContentGenerator.EnsureFolder(Folder);
            ContentGenerator.EnsureFolder(DefensePrefabGenerator.Folder);

            var meshes = new Dictionary<string, Mesh>
            {
                ["Base"] = Save(Base(), "CornShooter_Base"),
                ["Head"] = Save(Head(), "CornShooter_Head"),
                ["Wheel"] = Save(Tyre(), "CornShooter_Tyre"),
                ["Flywheel"] = Save(Flywheel(), "CornShooter_Flywheel"),
                ["Tube"] = Save(Tube(), "CornShooter_Tube"),
                ["Loaded"] = Save(LoadedCob(), "CornShooter_LoadedCob"),
                ["Site"] = Save(Site(), "CornShooter_Site"),
                ["Cob"] = Save(ThrownCob(), "CornShooter_Cob"),
            };

            PooledEffect launch = CornShooterEffects.Launch();
            PooledEffect impact = CornShooterEffects.Impact();

            bool created = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            GameObject root = created ? new GameObject("Defense_CornShooter") : PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                Dress(root, definition, motion, meshes, launch, impact);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (created) Object.DestroyImmediate(root);
                else PrefabUtility.UnloadPrefabContents(root);
            }

            definition.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);

            int vertices = meshes["Base"].vertexCount + meshes["Head"].vertexCount + meshes["Wheel"].vertexCount * 2 +
                           meshes["Flywheel"].vertexCount + meshes["Tube"].vertexCount + meshes["Loaded"].vertexCount;
            return $"[Last Orders] Corn Shooter (cob pitcher) {(created ? "built" : "rebuilt")}: {vertices} vertices, the thrown cob " +
                   $"{meshes["Cob"].vertexCount}, {meshes["Site"].vertexCount} in the empty site; prefab at {PrefabPath}.";
        }

        // --- The prefab --------------------------------------------------------------------------------

        static void Dress(GameObject root, DefenseDefinition definition, DefenseMotionProfile motion, Dictionary<string, Mesh> meshes,
                          PooledEffect launch, PooledEffect impact)
        {
            foreach (string path in Retired)
            {
                Transform old = root.transform.Find(path);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            Material painted = B.PaintedMaterial();

            Transform built = Child(root.transform, "Sheller", Vector3.zero);
            B.Show(Child(built, "Base", Vector3.zero).gameObject, meshes["Base"], new[] { painted });

            // The aim pivot: everything on the deck turns with it.
            Transform head = Child(built, "Head", new Vector3(0f, PivotY, 0f));
            B.Show(head.gameObject, meshes["Head"], new[] { painted, B.GlowMaterial() });

            // The two tyres on their sides, their axles upright: each spins about its own Z, turned to point up.
            var wheels = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                wheels[i] = Child(head, i == 0 ? "Wheel_L" : "Wheel_R", new Vector3(side * WheelX, LineY, WheelZ));
                wheels[i].localRotation = Quaternion.Euler(-90f, 0f, 0f);
                B.Show(wheels[i].gameObject, meshes["Wheel"], new[] { painted });
            }

            // The engine's flywheel, on its left side: its axle runs across, along X.
            Transform flywheel = Child(head, "EngineFlywheel", EngineAt + new Vector3(-EngineSize.x * 0.5f - 0.05f, 0f, 0f));
            flywheel.localRotation = Quaternion.Euler(0f, 90f, 0f);
            B.Show(flywheel.gameObject, meshes["Flywheel"], new[] { painted });

            Transform tube = Child(head, "Tube", new Vector3(0f, LineY, TubeFrom));
            B.Show(tube.gameObject, meshes["Tube"], new[] { painted });

            Transform loaded = Child(head, "Loaded", new Vector3(0f, LineY, 0f));
            B.Show(loaded.gameObject, meshes["Loaded"], new[] { painted });

            Transform muzzle = Child(head, "Muzzle", new Vector3(0f, LineY, TubeFrom + TubeLength + 0.06f));

            // Smoke off the exhaust, all the time: the engine is running.
            ToastapultEffects.Exhaust(head, ExhaustTop + Vector3.up * 0.04f);

            Transform site = Child(root.transform, "Site", Vector3.zero);
            B.Show(site.gameObject, meshes["Site"], new[] { painted });

            Transform shot = Child(root.transform, "Projectile_Cob", Vector3.zero);
            B.Show(shot.gameObject, meshes["Cob"], new[] { painted });
            Quiet(shot.gameObject);
            shot.gameObject.SetActive(false);

            var tap = root.GetComponent<BoxCollider>();
            if (tap == null) tap = root.AddComponent<BoxCollider>();

            var post = root.GetComponent<DefensePost>();
            if (post == null) post = root.AddComponent<DefensePost>();

            post.Configure(null, definition, "corn_shooter", false);
            post.ConfigureParts(built.gameObject, site.gameObject, head, muzzle, null, null, null, tap);
            post.ConfigureMotion(motion, builtSize: new Vector3(1.8f, 1.8f, 1.8f), siteSize: new Vector3(1.6f, 0.6f, 1.6f),
                                 calloutAt: new Vector3(0f, 2.4f, 0f), buildButtonAt: new Vector3(0f, 1.1f, 0f));
            post.ConfigureMuzzleEffect(launch);

            var recoil = root.GetComponent<DefenseRecoil>();
            if (recoil == null) recoil = root.AddComponent<DefenseRecoil>();
            recoil.Configure(post, new[] { tube }, new Transform[0], null);

            // The tyres against each other, fast; the flywheel more slowly. A throw kicks each on a turn.
            var spinners = new List<DefenseBarrelSpin>(root.GetComponents<DefenseBarrelSpin>());
            while (spinners.Count < 3) spinners.Add(root.AddComponent<DefenseBarrelSpin>());
            for (int i = 3; i < spinners.Count; i++) Object.DestroyImmediate(spinners[i]);
            spinners[0].Configure(post, wheels[0], 1, 720f, 1f);
            spinners[1].Configure(post, wheels[1], 1, 720f, -1f);
            spinners[2].Configure(post, flywheel, 2, 300f, 1f);

            var reload = root.GetComponent<DefenseReload>();
            if (reload == null) reload = root.AddComponent<DefenseReload>();
            reload.Configure(post, loaded.gameObject);

            var thrown = root.GetComponent<DefenseProjectiles>();
            if (thrown == null) thrown = root.AddComponent<DefenseProjectiles>();
            thrown.Configure(post, shot.gameObject, impact);

            built.gameObject.SetActive(true);
            site.gameObject.SetActive(false);
        }

        internal static Transform Child(Transform parent, string name, Vector3 at)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
            }

            t.localPosition = at;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            return t;
        }

        internal static void Quiet(GameObject go)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // --- The base: pallet, pedestal, cobs ------------------------------------------------------------

        static B.Model Base()
        {
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.8f, 1f, S.Smooth(p.y / 0.5f)) };

            Pallet(m);

            // The pedestal: a cast-iron column flaring to its foot, bolted through the pallet.
            m.Lathe((p, n) => p.y > PivotY - 0.03f ? Iron : IronDark, new Vector3(0f, PalletTop, 0f), new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.28f, 0f), new Vector2(0.28f, 0.035f), new Vector2(0.21f, 0.08f),
                new Vector2(0.12f, 0.18f), new Vector2(0.09f, 0.38f), new Vector2(0.12f, 0.44f), new Vector2(0.15f, 0.5f),
                new Vector2(0.15f, PivotY - PalletTop), new Vector2(0f, PivotY - PalletTop),
            }, 20);

            for (int i = 0; i < 4; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 0.5f;
                var at = new Vector3(Mathf.Cos(a) * 0.22f, PalletTop + 0.035f, Mathf.Sin(a) * 0.22f);
                m.Tube(SteelLight, at, at + Vector3.up * 0.025f, 0.022f, 6);
            }

            // Ammunition: a bushel basket of cobs at the back, a sack of them beside it, two dropped on the boards.
            Basket(m, new Vector3(-0.58f, PalletTop, -0.5f));
            B.Sack(m, new Vector3(0.58f, PalletTop, -0.52f), 0.14f, 0.36f, -0.03f, 4);
            Cob(m, new Vector3(-0.66f, PalletTop + 0.04f, 0.05f), Quaternion.Euler(90f, 20f, 0f), 0.26f, 0.04f, 31);
            Cob(m, new Vector3(-0.6f, PalletTop + 0.04f, 0.32f), Quaternion.Euler(88f, -55f, 0f), 0.24f, 0.038f, 32);
            return m;
        }

        /// <summary>A pallet of weathered planks on two runners, the size of the top of a bastion.</summary>
        static void Pallet(B.Model m)
        {
            for (int side = -1; side <= 1; side += 2)
                m.Box(B.Walnut, new Vector3(side * 0.42f, 0.05f, 0f), new Vector3(0.1f, 0.1f, 1.36f), 0.012f);

            const int Boards = 7;
            for (int i = 0; i < Boards; i++)
            {
                float z = -0.62f + i * (1.24f / (Boards - 1));
                Color c = B.Plank(i * 5 + 2) * (1f + 0.04f * B.Jitter(i + 11));
                m.Box(c, new Vector3(0.02f * B.Jitter(i), PalletTop - 0.03f, z), new Vector3(1.12f, 0.06f, 0.16f), 0.012f,
                      Quaternion.Euler(0f, 1.2f * B.Jitter(i + 4), 0f));
            }
        }

        /// <summary>A bushel basket of woven splints, banded, heaped with cobs.</summary>
        static void Basket(B.Model m, Vector3 foot)
        {
            const float H = 0.3f;
            m.Lathe((p, n) =>
            {
                float y = (p.y - foot.y) / H;
                if (n.y > 0.8f && y > 0.5f) return B.WalnutDark;
                bool band = Mathf.Abs(y - 0.12f) < 0.05f || Mathf.Abs(y - 0.92f) < 0.05f;
                float weave = Mathf.Sign(Mathf.Sin(Mathf.Atan2(p.z - foot.z, p.x - foot.x) * 14f + y * 18f));
                return band ? WickerBand : Wicker * (1f + 0.05f * weave);
            }, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.17f, 0f), new Vector2(0.18f, 0.02f), new Vector2(0.18f, H * 0.07f), new Vector2(0.185f, H * 0.17f),
                new Vector2(0.21f, H * 0.5f), new Vector2(0.23f, H * 0.87f), new Vector2(0.235f, H * 0.97f), new Vector2(0.24f, H),
                new Vector2(0.22f, H), new Vector2(0.2f, H * 0.82f), new Vector2(0f, H * 0.82f),
            }, 20);

            Vector3 top = foot + Vector3.up * (H * 0.82f);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.3f;
                var at = top + new Vector3(Mathf.Cos(a) * 0.08f, 0.03f + 0.03f * (i % 2), Mathf.Sin(a) * 0.08f);
                Cob(m, at, Quaternion.Euler(78f + 10f * B.Jitter(i), i * 47f, 0f), 0.2f, 0.034f, i + 20);
            }
        }

        // --- The head: deck, engine, breech, magazine ------------------------------------------------------

        static Vector3 ExhaustTop => EngineAt + new Vector3(0.22f, 0.5f, -0.06f);

        static B.Model Head()
        {
            var m = new B.Model { Shade = (p, n) => n.y < -0.4f ? 0.82f : 1f };

            // The bearing, the bracket, and the deck plate everything is bolted to.
            m.Lathe((p, n) => n.y > 0.9f ? Iron : IronDark, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.2f, 0f), new Vector2(0.2f, 0.05f), new Vector2(0.185f, 0.06f), new Vector2(0f, 0.06f),
            }, 20);
            m.Box(IronDark, new Vector3(0f, 0.13f, 0f), new Vector3(0.3f, 0.14f, 0.34f), 0.025f, 2);
            PaintedBox(m, (p, n) => n.y > 0.7f ? Chipped(p, 1, Green) : GreenDark, new Vector3(0f, DeckTop - 0.03f, -0.08f),
                       new Vector3(0.9f, 0.06f, 1.18f), 0.02f, 2);
            for (int k = 0; k < 6; k++)
            {
                float x = (k % 2 == 0 ? -1f : 1f) * 0.4f, z = -0.6f + (k / 2) * 0.52f;
                S.Ball(m, (p, n) => SteelLight, new Vector3(x, DeckTop, z), new Vector3(0.016f, 0.01f, 0.016f), Quaternion.identity, 6, 4);
            }

            Engine(m);
            Belts(m);
            Breech(m);
            Magazine(m);

            // The tyres' axles up through the deck, and the brackets they turn in.
            for (int side = -1; side <= 1; side += 2)
            {
                var axle = new Vector3(side * WheelX, DeckTop, WheelZ);
                m.Tube(Steel, axle, axle + Vector3.up * (LineY - DeckTop + WheelWidth * 0.5f + 0.03f), 0.022f, 8);
                m.Box(IronDark, axle + Vector3.up * 0.02f, new Vector3(0.1f, 0.04f, 0.1f), 0.01f);
            }

            return m;
        }

        /// <summary>
        /// The engine at the back: a finned single cylinder on a green crankcase, a red fuel tank on its back,
        /// a pull-start on its right, the exhaust up and back, and a lantern hung on its back.
        /// </summary>
        static void Engine(B.Model m)
        {
            PaintedBox(m, (p, n) => Chipped(p, 3, Green), EngineAt, EngineSize, 0.06f, 3);
            m.Box(GreenDark, EngineAt + new Vector3(0f, -EngineSize.y * 0.5f + 0.03f, 0f), EngineSize + new Vector3(0.04f, -EngineSize.y + 0.06f, 0.04f), 0.02f);

            // The cylinder and its finned head, leaning back.
            Vector3 cylinder = EngineAt + new Vector3(0f, EngineSize.y * 0.5f, 0.02f);
            Quaternion lean = Quaternion.Euler(-15f, 0f, 0f);
            for (int k = 0; k < 6; k++)
                m.Box(k % 2 == 0 ? IronDark : Iron, cylinder + lean * new Vector3(0f, 0.025f + k * 0.032f, 0f), new Vector3(0.24f, 0.016f, 0.2f), 0.006f, lean);
            m.Tube(IronDark, cylinder, cylinder + lean * Vector3.up * 0.2f, 0.06f, 12);
            m.Tube(Bore, cylinder + lean * Vector3.up * 0.2f, cylinder + lean * Vector3.up * 0.26f, 0.014f, 6);

            // The fuel tank, in diner red with a cream band and a cap.
            Vector3 tank = EngineAt + new Vector3(-0.1f, 0.12f, -EngineSize.z * 0.5f - 0.06f);
            PaintedBox(m, (p, n) => Chipped(p, 5, Red), tank, new Vector3(0.16f, 0.1f, 0.2f), 0.04f, 3);
            m.Box(Cream, tank + new Vector3(0.081f, 0f, 0f), new Vector3(0.004f, 0.03f, 0.16f), 0.002f);
            m.Tube(SteelLight, tank + Vector3.up * 0.05f, tank + Vector3.up * 0.075f, 0.022f, 8);

            // The exhaust, out of the right side, up and back, rusted.
            Vector3 port = EngineAt + new Vector3(EngineSize.x * 0.5f, 0.06f, -0.04f);
            Vector3 bend = port + new Vector3(0.06f, 0.1f, -0.02f);
            m.Tube(Rust, port, bend, 0.024f, 8);
            m.Tube(Rust, bend, ExhaustTop, 0.024f, 8);
            m.Tube(IronDark, ExhaustTop, ExhaustTop + Vector3.up * 0.03f, 0.03f, 8);

            // The pull-start: a cord out of its right side to a wooden T-handle.
            Vector3 cord = EngineAt + new Vector3(EngineSize.x * 0.5f + 0.005f, -0.04f, 0.06f);
            m.Tube(IronDark, cord, cord + Vector3.right * 0.04f, 0.035f, 10);
            m.Tube(B.HoneyLight, cord + new Vector3(0.05f, 0f, -0.05f), cord + new Vector3(0.05f, 0f, 0.05f), 0.016f, 8);

            m.Lantern(EngineAt + new Vector3(0.06f, 0.06f, -EngineSize.z * 0.5f), Vector3.back);
        }

        /// <summary>A belt from the engine's pulley round to each tyre's axle, crossed to the far one so they turn against each other.</summary>
        static void Belts(B.Model m)
        {
            var pulley = new Vector3(0f, DeckTop + 0.04f, EngineAt.z + EngineSize.z * 0.5f + 0.06f);
            m.Tube(IronDark, pulley - Vector3.up * 0.04f, pulley + Vector3.up * 0.03f, 0.06f, 14);

            for (int side = -1; side <= 1; side += 2)
            {
                var hub = new Vector3(side * WheelX, DeckTop + 0.04f, WheelZ);
                Vector3 along = (hub - pulley).normalized;
                Vector3 across = Vector3.Cross(Vector3.up, along) * 0.05f;
                Quaternion lie = Quaternion.LookRotation(along);
                float length = Vector3.Distance(hub, pulley);
                m.Box(Belt, (pulley + hub) * 0.5f + across * side, new Vector3(0.015f, 0.04f, length), 0.006f, lie);
                m.Box(Belt, (pulley + hub) * 0.5f - across * side * 0.6f + Vector3.up * 0.005f, new Vector3(0.015f, 0.04f, length), 0.006f, lie);
                m.Tube(IronDark, hub, hub + Vector3.up * 0.04f, 0.045f, 12);
            }
        }

        /// <summary>The breech: a galvanised trough the cob lies in, on two brackets, ahead of the engine.</summary>
        static void Breech(B.Model m)
        {
            float length = BreechFront - BreechBack;
            float middle = (BreechFront + BreechBack) * 0.5f;

            m.Surface((p, n) => Galvanised * (n.y > 0f ? 0.88f : 1f), 10, 4, (u, v) =>
            {
                float a = Mathf.Lerp(-Mathf.PI * 0.85f, -Mathf.PI * 0.15f, u);
                return new Vector3(Mathf.Cos(a) * 0.075f, LineY + Mathf.Sin(a) * 0.075f, BreechBack + v * length);
            });
            m.Surface((p, n) => Galvanised * 0.8f, 10, 4, (u, v) =>
            {
                float a = Mathf.Lerp(-Mathf.PI * 0.15f, -Mathf.PI * 0.85f, u);
                return new Vector3(Mathf.Cos(a) * 0.082f, LineY + Mathf.Sin(a) * 0.082f, BreechBack + v * length);
            });

            for (int k = 0; k < 2; k++)
            {
                float z = BreechBack + 0.05f + k * (length - 0.1f);
                m.Box(IronDark, new Vector3(0f, (DeckTop + LineY - 0.075f) * 0.5f, z), new Vector3(0.05f, LineY - 0.075f - DeckTop, 0.04f), 0.01f);
            }

            // A pusher plate at its back end, on a rod from the engine.
            m.Box(IronDark, new Vector3(0f, LineY, BreechBack - 0.02f), new Vector3(0.12f, 0.12f, 0.02f), 0.01f);
            m.Tube(Steel, new Vector3(0f, LineY, BreechBack - 0.03f), new Vector3(0f, LineY, EngineAt.z + EngineSize.z * 0.5f), 0.012f, 6);
        }

        /// <summary>The magazine over the breech: an open cage of steel bars, three cobs stacked in it waiting to drop.</summary>
        static void Magazine(B.Model m)
        {
            const float Half = 0.085f, Top = 0.98f;
            float bottom = LineY + 0.1f;
            float back = BreechBack + 0.02f, front = BreechFront - 0.02f;

            foreach (float x in new[] { -Half, Half })
                foreach (float z in new[] { back, front })
                    m.Tube(IronDark, new Vector3(x, bottom, z), new Vector3(x, Top, z), 0.012f, 6);

            foreach (float y in new[] { bottom + 0.02f, (bottom + Top) * 0.5f, Top })
            {
                m.Tube(Galvanised, new Vector3(-Half, y, back), new Vector3(Half, y, back), 0.01f, 6);
                m.Tube(Galvanised, new Vector3(-Half, y, front), new Vector3(Half, y, front), 0.01f, 6);
                m.Tube(Galvanised, new Vector3(-Half, y, back), new Vector3(-Half, y, front), 0.01f, 6);
                m.Tube(Galvanised, new Vector3(Half, y, back), new Vector3(Half, y, front), 0.01f, 6);
            }

            // A painted band round the top, the diner's red.
            m.Box(Red, new Vector3(0f, Top - 0.03f, back - 0.004f), new Vector3(Half * 2f + 0.02f, 0.05f, 0.008f), 0.003f);

            for (int k = 0; k < 3; k++)
            {
                float y = bottom + 0.07f + k * 0.13f;
                Cob(m, new Vector3(0.006f * B.Jitter(k), y, back + 0.01f), Quaternion.Euler(90f, 4f * B.Jitter(k + 3), 0f), front - back - 0.02f, 0.052f,
                    40 + k, husk: false);
            }
        }

        // --- What moves -------------------------------------------------------------------------------------

        /// <summary>
        /// A pitching tyre in its own space, axle along Z: black rubber worn grey across its tread, a red
        /// hub with four holes and a steel cap.
        /// </summary>
        static B.Model Tyre()
        {
            var m = new B.Model();
            m.Transform = Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.up, Vector3.forward));

            float w = WheelWidth * 0.5f, r = WheelRadius;
            m.Lathe((p, n) =>
            {
                // Tread blocks round the outside: paint only changes at a vertex, so they are the lathe's own segments.
                float a = Mathf.Atan2(p.z, p.x);
                bool block = Mathf.Sin(a * 14f) > 0f;
                bool tread = new Vector2(p.x, p.z).magnitude > r - 0.012f;
                return tread ? (block ? RubberWorn : Rubber) : Rubber;
            }, Vector3.zero, new List<Vector2>
            {
                new Vector2(r * 0.66f, -w), new Vector2(r * 0.9f, -w), new Vector2(r * 0.97f, -w * 0.8f), new Vector2(r, -w * 0.4f),
                new Vector2(r, w * 0.4f), new Vector2(r * 0.97f, w * 0.8f), new Vector2(r * 0.9f, w), new Vector2(r * 0.66f, w),
            }, 28);

            m.Lathe((p, n) => Chipped(p, 7, Red), Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, -w * 0.7f), new Vector2(r * 0.68f, -w * 0.7f), new Vector2(r * 0.68f, w * 0.7f), new Vector2(0f, w * 0.7f),
            }, 24);

            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f + 0.4f;
                var at = new Vector3(Mathf.Cos(a) * r * 0.42f, w * 0.7f + 0.001f, Mathf.Sin(a) * r * 0.42f);
                S.Disc(m, (p, n) => Bore, at, Vector3.up, 0.02f, 10);
            }

            m.Lathe((p, n) => SteelLight, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, w * 0.7f), new Vector2(0.04f, w * 0.7f), new Vector2(0.035f, w * 0.7f + 0.02f), new Vector2(0f, w * 0.7f + 0.025f),
            }, 12);

            m.Transform = Matrix4x4.identity;
            return m;
        }

        /// <summary>The engine's flywheel in its own space, axle along Z: a red rim on four spokes, a hub.</summary>
        static B.Model Flywheel()
        {
            var m = new B.Model();
            const int Steps = 24;
            const float R = 0.15f;

            for (int i = 0; i < Steps; i++)
            {
                float a0 = i / (float)Steps * Mathf.PI * 2f, a1 = (i + 1) / (float)Steps * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * R;
                var p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * R;
                int key = i;
                m.Tube((p, n) => Chipped(p, key + 30, Red), p0, p1, 0.024f, 0.024f, 8);
            }

            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f;
                m.Tube(IronDark, Vector3.zero, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (R - 0.01f), 0.014f, 6);
            }

            m.Tube(IronDark, new Vector3(0f, 0f, -0.03f), new Vector3(0f, 0f, 0.04f), 0.04f, 12);
            return m;
        }

        /// <summary>
        /// The launch tube, from just past the tyres along +Z: a galvanised pipe on a collar, a band of the
        /// diner's red near its mouth, a reinforced lip, dark inside.
        /// </summary>
        static B.Model Tube()
        {
            var m = new B.Model();

            m.Tube(IronDark, Vector3.zero, Vector3.forward * 0.05f, TubeRadius + 0.02f, 16);
            m.Tube((p, n) => Galvanised * (1f + 0.05f * Mathf.Sin(p.z * 23f)), Vector3.forward * 0.05f, Vector3.forward * TubeLength,
                   TubeRadius, TubeRadius, 16);
            m.Tube(Red, Vector3.forward * (TubeLength - 0.14f), Vector3.forward * (TubeLength - 0.09f), TubeRadius + 0.006f, 16);
            m.Tube(IronDark, Vector3.forward * (TubeLength - 0.02f), Vector3.forward * (TubeLength + 0.02f), TubeRadius + 0.016f, 16);
            S.Disc(m, (p, n) => Bore, Vector3.forward * (TubeLength + 0.021f), Vector3.forward, TubeRadius - 0.012f, 16);

            // A sight rail along its top, and a strap down to the deck.
            m.Box(IronDark, new Vector3(0f, TubeRadius + 0.012f, TubeLength * 0.5f), new Vector3(0.016f, 0.02f, TubeLength * 0.8f), 0.005f);
            m.Tube(IronDark, new Vector3(0f, -TubeRadius, 0.12f), new Vector3(0f, DeckTop - LineY + 0.02f, 0.05f), 0.014f, 6);
            return m;
        }

        /// <summary>The cob waiting in the breech, nose toward the tyres.</summary>
        static B.Model LoadedCob()
        {
            var m = new B.Model();
            Cob(m, new Vector3(0f, 0f, BreechBack + 0.02f), Quaternion.Euler(90f, 0f, 0f), BreechFront - BreechBack + 0.08f, 0.055f, 50, husk: false);
            return m;
        }

        // --- The empty site and what it throws ---------------------------------------------------------------

        /// <summary>The pallet and the pedestal waiting, the basket of cobs beside them, the machine still under a tarp.</summary>
        static B.Model Site()
        {
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.8f, 1f, S.Smooth(p.y / 0.5f)) };
            Pallet(m);
            Basket(m, new Vector3(-0.58f, PalletTop, -0.5f));

            var shape = new SmoothMesh(1);
            shape.RoundedBox(0, new Vector3(0.15f, PalletTop + 0.2f, 0.1f), new Vector3(0.6f, 0.4f, 0.7f), 0.12f, Quaternion.Euler(0f, 8f, 0f), 3);
            m.Add(shape, (p, n) => (n.y > 0.2f ? B.Hex("8A8268") : B.Hex("736C55")) * (1f + 0.05f * Mathf.Sin(p.x * 21f + p.z * 7f)));
            return m;
        }

        /// <summary>
        /// The cob it throws, nose along +Z, centred: kernels in rows, a few dark and a few pale, a tuft of
        /// silk at its nose, the stub of its stalk at the back and the husk peeled back from there into four
        /// long leaves that trail behind it like a dart's flights. Twice life size: at the game camera's
        /// distance a real cob is a few pixels.
        /// </summary>
        static B.Model ThrownCob()
        {
            var m = new B.Model();
            const float Length = 0.46f, Radius = 0.07f;
            float back = -Length * 0.5f;

            m.Transform = Matrix4x4.TRS(new Vector3(0f, 0f, back), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            m.Lathe((p, n) =>
            {
                float a = Mathf.Atan2(p.z, p.x);
                int row = Mathf.FloorToInt((a + Mathf.PI) / (Mathf.PI * 2f) * 12f);
                int ring = Mathf.FloorToInt(p.y / Length * 18f);
                int key = row * 31 + ring * 7;
                Color c = B.Hash(key) % 11 == 0 ? KernelDark : B.Hash(key) % 7 == 0 ? KernelPale : Kernel;
                bool groove = Mathf.Sin(a * 12f) < -0.6f;
                return (groove ? c * 0.86f : c) * Mathf.Lerp(1.05f, 0.92f, p.y / Length);
            }, Vector3.zero, CobProfile(Length, Radius, 16), 24);

            // The stalk stub at the back.
            m.Tube((p, n) => Stalk, Vector3.zero, Vector3.down * 0.05f, Radius * 0.4f, Radius * 0.34f, 10);
            m.Transform = Matrix4x4.identity;

            // Silk at the nose: a few fine dark threads curling forward.
            Vector3 nose = new Vector3(0f, 0f, -back);
            for (int k = 0; k < 5; k++)
            {
                float a = k * 1.25f;
                var tip = nose + new Vector3(Mathf.Cos(a) * 0.03f, Mathf.Sin(a) * 0.03f, 0.05f + 0.02f * B.Jitter(k));
                m.Tube((p, n) => Silk, nose - Vector3.forward * 0.01f, tip, 0.006f, 0.002f, 4);
            }

            // The husk: four leaves from round the stalk, swept back and out, curving, a pale midrib down each.
            for (int k = 0; k < 4; k++)
            {
                float spin = k * 90f + 45f;
                int key = k;
                m.Surface((p, n) =>
                {
                    Color leaf = key % 2 == 0 ? Husk : HuskPale;
                    return Color.Lerp(leaf, HuskDark, Mathf.Clamp01((-p.z + back) / 0.3f) * 0.5f);
                }, 4, 10, (u, v) => HuskLeaf(spin, u, v, back), wrapU: false);
                m.Surface((p, n) => HuskDark, 4, 10, (u, v) => HuskLeaf(spin, 1f - u, v, back), wrapU: false);
            }

            return m;
        }

        /// <summary>
        /// A point on a husk leaf: <paramref name="u"/> across it, <paramref name="v"/> from where it leaves
        /// the cob near the stalk to its tip, trailing back and splaying out from the axis at
        /// <paramref name="spin"/> degrees round it.
        /// </summary>
        static Vector3 HuskLeaf(float spin, float u, float v, float back)
        {
            Quaternion round = Quaternion.Euler(0f, 0f, spin);
            float z = back + 0.12f - v * 0.34f;
            float outward = 0.065f + v * v * 0.09f;
            float width = 0.07f * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.15f, 1f, v)) * (1f - 0.5f * v);
            float curl = (u - 0.5f) * width;
            float cup = -Mathf.Abs(u - 0.5f) * width * 0.4f;
            return round * new Vector3(curl, outward + cup, z);
        }

        // --- Corn -------------------------------------------------------------------------------------------

        /// <summary>
        /// A cob from <paramref name="foot"/> along <paramref name="turn"/>'s up, <paramref name="length"/> long:
        /// kernels in rows round it (paint changes only at a vertex, so the rows are rings of the lathe),
        /// with husk leaves peeled back from its foot unless it has been stripped of them.
        /// </summary>
        internal static void Cob(B.Model m, Vector3 foot, Quaternion turn, float length, float radius, int key, bool husk = true)
        {
            Matrix4x4 was = m.Transform;
            m.Transform = was * Matrix4x4.TRS(foot, turn, Vector3.one);

            m.Lathe((p, n) => (Mathf.Sin(Mathf.Atan2(p.z, p.x) * 9f) < -0.55f ? KernelDark : Kernel) * (1f + 0.05f * B.Jitter(key)),
                    Vector3.zero, CobProfile(length, radius, 14), 18);

            if (husk)
            {
                for (int k = 0; k < 3; k++)
                {
                    Quaternion leaf = Quaternion.Euler(0f, k * 120f + key * 13f, 0f) * Quaternion.Euler(-28f, 0f, 0f);
                    m.Box(k == 1 ? HuskDark : Husk, leaf * new Vector3(0f, -0.03f, radius * 0.9f), new Vector3(radius * 1.3f, length * 0.42f, 0.012f),
                          0.006f, leaf);
                }

                m.Tube(HuskDark, new Vector3(0f, 0.01f, 0f), new Vector3(0f, -0.05f, 0f), radius * 0.35f, 8);
            }

            m.Transform = was;
        }

        /// <summary>A cob's outline up its length: kernel rows as bumps, thinning toward the tip.</summary>
        internal static List<Vector2> CobProfile(float length, float radius, int rows)
        {
            var profile = new List<Vector2> { new Vector2(0f, 0f) };
            for (int i = 0; i <= rows; i++)
            {
                float t = i / (float)rows;
                float r = radius * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Lerp(0.06f, 1f, t))), 0.3f) * (1f - 0.3f * t * t);
                profile.Add(new Vector2(r, t * length));
                if (i < rows) profile.Add(new Vector2(r * 0.94f, (t + 0.5f / rows) * length));
            }

            profile.Add(new Vector2(0f, length));
            return profile;
        }

        // --- Helpers -----------------------------------------------------------------------------------------

        internal static void PaintedBox(B.Model m, Func<Vector3, Vector3, Color> paint, Vector3 centre, Vector3 size, float radius, int segments)
        {
            var shape = new SmoothMesh(1);
            shape.RoundedBox(0, centre, size, radius, Quaternion.identity, segments);
            m.Add(shape, paint);
        }

        /// <summary>Paint worn back to bare iron in patches, as paint chips on a working machine.</summary>
        internal static Color Chipped(Vector3 p, int key, Color paint)
        {
            float chip = Mathf.Sin(p.x * 31f + key * 1.7f) * Mathf.Sin(p.y * 27f - key * 2.3f) * Mathf.Sin(p.z * 23f + key);
            if (chip > 0.6f) return BareIron;
            return paint * (1f + 0.035f * Mathf.Sin(p.x * 9f + p.y * 5f + p.z * 7f + key));
        }

        static Mesh Save(B.Model model, string name) => model.Save($"{Folder}/{name}.asset", name);
    }
}
