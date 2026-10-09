using System;
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.Defense;
using LastOrders.Effects;
using UnityEditor;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using C = LastOrders.EditorTools.CornShooterModel;
using S = LastOrders.EditorTools.SoupKitchenModel;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The Tomato Shooter, modelled in code in the game's smooth low-poly style, and its prefab: a
    /// pressure cooker turned blunderbuss.
    ///
    /// A fat bulging pot in the diner's red, a cream band round its belly, its steel lid clamped down
    /// with four big wing nuts. On the lid, an oversized pressure gauge with its needle in the red, the
    /// rocking weight of the pressure valve, and a galvanised funnel heaped with tomatoes. Out of the
    /// front, a short pipe opening into a wide brass bell. It sits on a gas ring on a wonky stack of
    /// two produce crates, a gas bottle beside them with its hose up to the ring, an open crate of
    /// tomatoes on the ground. Toon proportions - the bulge, the bell, the gauge and the wing nuts are
    /// all too big - in the props' muted paint, worn at the edges.
    ///
    /// What moves: the pot turns to track (the aim pivot); the valve weight rocks round while it has
    /// something in sight and steam wisps off it (<see cref="DefenseBarrelSpin"/>); the bell kicks
    /// back on each spit (<see cref="DefenseRecoil"/>); the tomato on top of the heap is gone as it is
    /// fired and back once the next rolls up (<see cref="DefenseReload"/>). What it throws is a big
    /// ripe tomato that tumbles along a low arc and bursts in a splat (<see cref="DefenseProjectiles"/>).
    ///
    /// Local +Z is the way it faces at rest. Re-running rebuilds the meshes in place and re-dresses the
    /// prefab's own objects by name.
    /// </summary>
    public static class TomatoShooterModel
    {
        public const string PrefabPath = DefensePrefabGenerator.Folder + "/Defense_TomatoShooter.prefab";

        internal const string Folder = "Assets/Game Assets/Content/LastOrders/Defense/" + EnvKit.Style + "/TomatoShooter";

        // --- Measurements (metres, model space; it faces +Z) ----------------------------------------

        const float LowerCrateTop = 0.34f, UpperCrateHeight = 0.26f;
        const float StandFoot = LowerCrateTop + 0.024f + UpperCrateHeight + 0.024f;

        /// <summary>The gas ring the pot turns on.</summary>
        const float PivotY = StandFoot + 0.11f;

        // Head space, from the pivot.
        const float PotBulge = 0.345f, PotRim = 0.33f, PotRimY = 0.44f;
        const float BarrelY = 0.24f, BarrelFrom = 0.26f, BarrelLength = 0.58f;
        const float BellRadius = 0.19f;

        static readonly Vector3 GaugeAt = new Vector3(-0.15f, 0f, 0.13f);
        static readonly Vector3 ValveAt = new Vector3(0.15f, 0f, -0.13f);
        static readonly Vector3 FunnelAt = new Vector3(0f, 0f, -0.02f);
        const float FunnelHeight = 0.36f, FunnelTop = 0.2f;

        // --- The palette -------------------------------------------------------------------------------

        static readonly Color Red = B.Hex("8E3B33");
        static readonly Color RedDark = B.Hex("6E2D27");
        static readonly Color Cream = B.Hex("AFA898");
        static readonly Color Soot = B.Hex("2A2523");
        static readonly Color IronDark = B.Hex("2F3236");
        static readonly Color Iron = B.Iron;
        static readonly Color Steel = B.Hex("8D9398");

        /// <summary>The lid: a darker steel than the trim, or the big dome reads white from above.</summary>
        static readonly Color LidSteel = B.Hex("7C8287");
        static readonly Color SteelLight = B.Hex("9EA4A9");
        static readonly Color Galvanised = B.Hex("8C9296");
        static readonly Color Brass = B.Hex("8F8160");
        static readonly Color BrassDark = B.Hex("6F6448");
        static readonly Color Bakelite = B.Hex("2B2D30");
        static readonly Color DialFace = B.Hex("B9B2A2");
        static readonly Color Rubber = B.Hex("2A2A2B");
        static readonly Color Bore = B.Hex("141414");

        internal static readonly Color Tomato = B.Hex("A8342B");
        internal static readonly Color TomatoLight = B.Hex("B9483A");
        internal static readonly Color TomatoDark = B.Hex("7E271F");
        internal static readonly Color Calyx = B.Hex("4E6E2A");
        internal static readonly Color Pulp = B.Hex("B5503C");
        internal static readonly Color Seed = B.Hex("C9B278");

        [MenuItem("Last Orders/Defense/Rebuild Tomato Shooter (Model & Prefab)", priority = 46)]
        static void Run()
        {
            DefenseDefinition shooter = DefenseContentGenerator.GenerateTomatoShooter();
            Debug.Log(Apply(shooter, DefenseContentGenerator.LoadTomatoShooterMotion()));
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
                ["Base"] = Save(Base(), "TomatoShooter_Base"),
                ["Head"] = Save(Head(), "TomatoShooter_Head"),
                ["Valve"] = Save(ValveWeight(), "TomatoShooter_Valve"),
                ["Barrel"] = Save(Barrel(), "TomatoShooter_Barrel"),
                ["Loaded"] = Save(LoadedTomato(), "TomatoShooter_Loaded"),
                ["Shot"] = Save(ThrownTomato(), "TomatoShooter_Tomato"),
            };

            PooledEffect launch = FoodShooterEffects.TomatoLaunch();
            PooledEffect impact = FoodShooterEffects.TomatoImpact();

            bool created = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            GameObject root = created ? new GameObject("Defense_TomatoShooter") : PrefabUtility.LoadPrefabContents(PrefabPath);

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

            int vertices = meshes["Base"].vertexCount + meshes["Head"].vertexCount + meshes["Valve"].vertexCount +
                           meshes["Barrel"].vertexCount + meshes["Loaded"].vertexCount;
            return $"[Last Orders] Tomato Shooter (pressure-cooker blunderbuss) {(created ? "built" : "rebuilt")}: {vertices} vertices, " +
                   $"the thrown tomato {meshes["Shot"].vertexCount}; prefab at {PrefabPath}.";
        }

        // --- The prefab --------------------------------------------------------------------------------

        static void Dress(GameObject root, DefenseDefinition definition, DefenseMotionProfile motion, Dictionary<string, Mesh> meshes,
                          PooledEffect launch, PooledEffect impact)
        {
            Material painted = B.PaintedMaterial();
            var both = new[] { painted, B.GlowMaterial() };

            Transform built = C.Child(root.transform, "Cooker", Vector3.zero);
            B.Show(C.Child(built, "Base", Vector3.zero).gameObject, meshes["Base"], both);

            // The aim pivot: the pot and everything on it turn on the gas ring.
            Transform head = C.Child(built, "Head", new Vector3(0f, PivotY, 0f));
            B.Show(head.gameObject, meshes["Head"], both);

            // The valve's rocking weight, its axle upright: it spins about its own Z, turned to point up.
            Transform valve = C.Child(head, "ValveWeight", ValveTop);
            valve.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            B.Show(valve.gameObject, meshes["Valve"], new[] { painted });

            Transform barrel = C.Child(head, "Barrel", new Vector3(0f, BarrelY, BarrelFrom));
            B.Show(barrel.gameObject, meshes["Barrel"], new[] { painted });

            Transform loaded = C.Child(head, "Loaded", LoadedAt);
            B.Show(loaded.gameObject, meshes["Loaded"], new[] { painted });

            Transform muzzle = C.Child(head, "Muzzle", new Vector3(0f, BarrelY, BarrelFrom + BarrelLength + 0.05f));

            // Steam off the valve, all the time: it is under pressure.
            FoodShooterEffects.Steam(head, ValveTop + Vector3.up * 0.06f);

            // An empty site is the crates and the gas bottle, waiting.
            Transform site = C.Child(root.transform, "Site", Vector3.zero);
            B.Show(site.gameObject, meshes["Base"], both);

            Transform shot = C.Child(root.transform, "Projectile_Tomato", Vector3.zero);
            B.Show(shot.gameObject, meshes["Shot"], new[] { painted });
            C.Quiet(shot.gameObject);
            shot.gameObject.SetActive(false);

            var tap = root.GetComponent<BoxCollider>();
            if (tap == null) tap = root.AddComponent<BoxCollider>();

            var post = root.GetComponent<DefensePost>();
            if (post == null) post = root.AddComponent<DefensePost>();

            post.Configure(null, definition, "tomato_shooter", false);
            post.ConfigureParts(built.gameObject, site.gameObject, head, muzzle, null, null, null, tap);
            post.ConfigureMotion(motion, builtSize: new Vector3(1.7f, 1.8f, 1.7f), siteSize: new Vector3(1.6f, 0.7f, 1.6f),
                                 calloutAt: new Vector3(0f, 2.4f, 0f), buildButtonAt: new Vector3(0f, 1.1f, 0f));
            post.ConfigureMuzzleEffect(launch);

            var recoil = root.GetComponent<DefenseRecoil>();
            if (recoil == null) recoil = root.AddComponent<DefenseRecoil>();
            recoil.Configure(post, new[] { barrel }, new Transform[0], null);

            // The valve weight rocks round under the steam, harder on each spit.
            var spinners = new List<DefenseBarrelSpin>(root.GetComponents<DefenseBarrelSpin>());
            while (spinners.Count < 1) spinners.Add(root.AddComponent<DefenseBarrelSpin>());
            for (int i = 1; i < spinners.Count; i++) Object.DestroyImmediate(spinners[i]);
            spinners[0].Configure(post, valve, 3, 360f, 1f);

            var reload = root.GetComponent<DefenseReload>();
            if (reload == null) reload = root.AddComponent<DefenseReload>();
            reload.Configure(post, loaded.gameObject);

            var thrown = root.GetComponent<DefenseProjectiles>();
            if (thrown == null) thrown = root.AddComponent<DefenseProjectiles>();
            thrown.Configure(post, shot.gameObject, impact);

            // Toon life: the pot swells with the pressure, squashes down as it spits and springs back past its size.
            var living = root.GetComponent<LivingDefenseAnimation>();
            if (living == null) living = root.AddComponent<LivingDefenseAnimation>();
            living.Configure(post, head, new Transform[0], null, null, 0f, new Transform[0]);

            built.gameObject.SetActive(true);
            site.gameObject.SetActive(false);
        }

        static Mesh Save(B.Model model, string name) => model.Save($"{Folder}/{name}.asset", name);

        // --- The base: crates, gas ring, gas bottle -----------------------------------------------------

        static B.Model Base()
        {
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.8f, 1f, S.Smooth(p.y / 0.6f)) };

            // A wonky stack of two produce crates, each with a tomato stencilled on its front.
            B.Crate(m, Vector3.zero, new Vector3(1.0f, LowerCrateTop, 0.86f), 0f, 3);
            Label(m, new Vector3(0f, LowerCrateTop * 0.5f, 0.452f), Quaternion.identity, 1f);

            Vector3 upper = new Vector3(0.03f, LowerCrateTop + 0.024f, -0.01f);
            Quaternion skew = Quaternion.Euler(0f, 5f, 0f);
            B.Crate(m, upper, new Vector3(0.84f, UpperCrateHeight, 0.72f), 5f, 9);
            Label(m, upper + skew * new Vector3(-0.12f, UpperCrateHeight * 0.5f, 0.382f), skew, 0.8f);

            Stand(m);
            GasBottle(m, new Vector3(0.64f, 0f, -0.42f));

            // An open crate of tomatoes on the ground, and two that rolled off.
            Vector3 crate = new Vector3(-0.68f, 0f, 0.36f);
            S.OpenCrate(m, crate, new Vector3(0.3f, 0.17f, 0.26f), 14f, 5);
            for (int k = 0; k < 6; k++)
            {
                float a = k * 1.05f + 0.3f;
                var at = crate + Quaternion.Euler(0f, 14f, 0f) * new Vector3(Mathf.Cos(a) * 0.07f, 0.11f + 0.025f * (k % 2), Mathf.Sin(a) * 0.055f);
                Fruit(m, at, 0.05f, 60 + k);
            }

            Fruit(m, new Vector3(-0.56f, 0f, 0.64f), 0.05f, 70);
            Fruit(m, new Vector3(-0.85f, 0f, 0.06f), 0.048f, 71);
            return m;
        }

        /// <summary>A cream paper label pasted on a crate's front, a red tomato printed on it.</summary>
        static void Label(B.Model m, Vector3 centre, Quaternion turn, float scale)
        {
            m.Box(Cream * 0.95f, centre, new Vector3(0.3f, 0.16f, 0.008f) * scale, 0.004f, turn);
            Vector3 face = turn * Vector3.forward;
            S.Disc(m, (p, n) => Red, centre + face * 0.0055f + turn * new Vector3(-0.07f, 0f, 0f) * scale, face, 0.052f * scale, 16);
            m.Box(Calyx, centre + face * 0.006f + turn * new Vector3(-0.07f, 0.05f, 0f) * scale, new Vector3(0.05f, 0.014f, 0.004f) * scale,
                  0.003f, turn);
            for (int k = 0; k < 3; k++)
                m.Box(RedDark, centre + face * 0.0055f + turn * new Vector3(0.03f + 0.055f * k, 0.02f - 0.02f * (k % 2), 0f) * scale,
                      new Vector3(0.04f, 0.012f, 0.004f) * scale, 0.003f, turn);
        }

        /// <summary>A cast-iron gas ring on three bowed legs, a burner glowing in its middle.</summary>
        static void Stand(B.Model m)
        {
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI * 2f / 3f + 0.4f;
                var foot = new Vector3(Mathf.Cos(a) * 0.27f, StandFoot, Mathf.Sin(a) * 0.27f);
                var knee = new Vector3(Mathf.Cos(a) * 0.3f, StandFoot + 0.06f, Mathf.Sin(a) * 0.3f);
                var top = new Vector3(Mathf.Cos(a) * 0.21f, PivotY - 0.02f, Mathf.Sin(a) * 0.21f);
                m.Tube(IronDark, foot, knee, 0.026f, 8);
                m.Tube(IronDark, knee, top, 0.026f, 8);
                S.Ball(m, (p, n) => IronDark, knee, Vector3.one * 0.028f, Quaternion.identity, 8, 5);
                S.Ball(m, (p, n) => Iron, foot + Vector3.up * 0.008f, new Vector3(0.04f, 0.014f, 0.04f), Quaternion.identity, 8, 5);
            }

            m.Lathe((p, n) => n.y > 0.8f ? Iron : IronDark, new Vector3(0f, PivotY - 0.035f, 0f), new List<Vector2>
            {
                new Vector2(0.17f, 0f), new Vector2(0.24f, 0f), new Vector2(0.245f, 0.03f), new Vector2(0.235f, 0.035f),
                new Vector2(0.175f, 0.035f), new Vector2(0.17f, 0.03f), new Vector2(0.17f, 0f),
            }, 24);

            // The burner bowl and a ring of little flames, under the pot.
            m.Lathe((p, n) => IronDark, new Vector3(0f, PivotY - 0.09f, 0f), new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.07f, 0f), new Vector2(0.11f, 0.05f), new Vector2(0.1f, 0.055f), new Vector2(0f, 0.03f),
            }, 16);
            var glow = new SmoothMesh(1);
            for (int k = 0; k < 10; k++)
            {
                float a = k * Mathf.PI * 0.2f;
                glow.RoundedBox(0, new Vector3(Mathf.Cos(a) * 0.085f, PivotY - 0.028f, Mathf.Sin(a) * 0.085f), new Vector3(0.022f, 0.04f, 0.022f),
                                0.01f, Quaternion.identity, 2);
            }

            m.Add(glow, Color.white, B.Model.Glow);
        }

        /// <summary>A gas bottle in the diner's red with a cream band, its valve and a black hose up to the ring.</summary>
        static void GasBottle(B.Model m, Vector3 foot)
        {
            const float R = 0.13f, H = 0.42f;
            m.Lathe((p, n) =>
            {
                float y = (p.y - foot.y) / H;
                if (y < 0.08f) return IronDark;
                if (y > 0.355f && y < 0.465f) return Cream;
                return Red;
            }, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(R * 0.92f, 0f), new Vector2(R * 0.92f, H * 0.08f), new Vector2(R, H * 0.1f),
                new Vector2(R, H * 0.35f), new Vector2(R, H * 0.36f), new Vector2(R, H * 0.46f), new Vector2(R, H * 0.47f), new Vector2(R, H * 0.78f),
                new Vector2(R * 0.82f, H * 0.93f),
                new Vector2(R * 0.4f, H), new Vector2(0f, H),
            }, 20);

            // The collar and the valve.
            Vector3 top = foot + Vector3.up * H;
            m.Lathe((p, n) => Steel, top - Vector3.up * 0.01f, new List<Vector2>
            {
                new Vector2(0.075f, 0f), new Vector2(0.08f, 0.06f), new Vector2(0.07f, 0.065f), new Vector2(0.065f, 0.01f),
            }, 16);
            m.Tube(Brass, top, top + Vector3.up * 0.06f, 0.022f, 10);
            m.Tube(IronDark, top + new Vector3(-0.03f, 0.075f, 0f), top + new Vector3(0.03f, 0.075f, 0f), 0.014f, 8);

            // The hose: up over the crate's back corner to the burner.
            Vector3[] hose =
            {
                top + new Vector3(0f, 0.05f, 0.02f), foot + new Vector3(-0.06f, 0.62f, 0.08f), new Vector3(0.42f, StandFoot + 0.07f, -0.3f),
                new Vector3(0.2f, StandFoot + 0.05f, -0.16f), new Vector3(0.07f, PivotY - 0.07f, -0.04f),
            };
            for (int i = 0; i + 1 < hose.Length; i++)
            {
                m.Tube(Rubber, hose[i], hose[i + 1], 0.016f, 8);
                S.Ball(m, (p, n) => Rubber, hose[i + 1], Vector3.one * 0.016f, Quaternion.identity, 8, 5);
            }
        }

        // --- The head: pot, lid, gauge, funnel ------------------------------------------------------------

        static Vector3 ValveTop => new Vector3(ValveAt.x, LidY(new Vector2(ValveAt.x, ValveAt.z).magnitude) + 0.075f, ValveAt.z);

        static Vector3 LoadedAt => new Vector3(FunnelAt.x, FunnelBase + FunnelHeight - 0.02f, FunnelAt.z);

        static float FunnelBase => LidY(0f) - 0.03f;

        /// <summary>The lid's dome over the rim, as a height at <paramref name="r"/> from its middle.</summary>
        static float LidY(float r)
        {
            float t = Mathf.Clamp01(r / PotRim);
            return PotRimY + 0.03f + 0.14f * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
        }

        static B.Model Head()
        {
            var m = new B.Model { Shade = (p, n) => n.y < -0.4f ? 0.8f : Mathf.Lerp(0.86f, 1f, S.Smooth(p.y / 0.4f)) };

            // The bearing it turns on.
            m.Lathe((p, n) => IronDark, new Vector3(0f, -0.035f, 0f), new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.2f, 0f), new Vector2(0.2f, 0.035f), new Vector2(0f, 0.035f),
            }, 20);

            Pot(m);
            Lid(m);
            Gauge(m);
            Valve(m);
            Funnel(m);

            // The flange the barrel is bolted to, on the pot's front.
            Vector3 flange = new Vector3(0f, BarrelY, BarrelFrom + 0.01f);
            m.Tube(IronDark, flange - Vector3.forward * 0.1f, flange + Vector3.forward * 0.02f, 0.12f, 18);
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f + 0.25f;
                S.Ball(m, (p, n) => SteelLight, flange + new Vector3(Mathf.Cos(a) * 0.1f, Mathf.Sin(a) * 0.1f, 0.025f), new Vector3(0.016f, 0.016f, 0.01f),
                       Quaternion.identity, 6, 4);
            }

            return m;
        }

        /// <summary>
        /// The pot: a fat bulging barrel of a cooker in chipped diner red, sooted at its foot, a cream
        /// band round its belly, a rolled rim, and two black handles on its sides.
        /// </summary>
        static void Pot(B.Model m)
        {
            const float BandLow = 0.2f, BandHigh = 0.27f;
            // Paint changes only at a ring of vertices, so every edge between colours is two rings a hair apart.
            m.Lathe((p, n) =>
            {
                if (p.y < 0.05f) return Color.Lerp(Soot, RedDark, S.Smooth(p.y / 0.05f));
                if (p.y > BandLow - 0.001f && p.y < BandHigh + 0.001f) return Cream;
                if (p.y > PotRimY - 0.021f) return Steel;
                return Red * (1f + 0.03f * Mathf.Sin(Mathf.Atan2(p.z, p.x) * 3f + p.y * 9f));
            }, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.22f, 0f), new Vector2(0.28f, 0.025f), new Vector2(0.32f, 0.07f), new Vector2(0.34f, 0.13f),
                new Vector2(PotBulge, BandLow - 0.004f), new Vector2(PotBulge, BandLow), new Vector2(PotBulge, BandHigh),
                new Vector2(PotBulge, BandHigh + 0.004f), new Vector2(0.34f, 0.32f), new Vector2(0.325f, 0.38f),
                new Vector2(PotRim - 0.008f, PotRimY - 0.025f), new Vector2(PotRim - 0.01f, PotRimY - 0.02f),
                new Vector2(PotRim + 0.012f, PotRimY - 0.01f), new Vector2(PotRim + 0.012f, PotRimY + 0.01f),
                new Vector2(PotRim - 0.02f, PotRimY + 0.02f), new Vector2(0f, PotRimY + 0.02f),
            }, 28);

            // Two black handles, one each side, on chrome brackets.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 a = new Vector3(side * 0.335f, 0.35f, -0.1f), b = new Vector3(side * 0.44f, 0.37f, -0.07f);
                Vector3 c = new Vector3(side * 0.44f, 0.37f, 0.07f), d = new Vector3(side * 0.335f, 0.35f, 0.1f);
                m.Tube(Bakelite, a, b, 0.024f, 8);
                m.Tube(Bakelite, b, c, 0.026f, 8);
                m.Tube(Bakelite, c, d, 0.024f, 8);
                S.Ball(m, (p, n) => Bakelite, b, Vector3.one * 0.026f, Quaternion.identity, 8, 5);
                S.Ball(m, (p, n) => Bakelite, c, Vector3.one * 0.026f, Quaternion.identity, 8, 5);
                m.Box(SteelLight, a + new Vector3(side * 0.005f, 0f, 0f), new Vector3(0.02f, 0.05f, 0.04f), 0.008f);
                m.Box(SteelLight, d + new Vector3(side * 0.005f, 0f, 0f), new Vector3(0.02f, 0.05f, 0.04f), 0.008f);
            }
        }

        /// <summary>The steel lid domed over the rim, and the four big wing nuts that clamp it down.</summary>
        static void Lid(B.Model m)
        {
            var profile = new List<Vector2>();
            for (int i = 0; i <= 8; i++)
            {
                float r = PotRim + 0.01f - i * (PotRim + 0.01f) / 8f;
                profile.Add(new Vector2(r, LidY(r)));
            }

            profile.Insert(0, new Vector2(PotRim + 0.015f, PotRimY + 0.012f));
            m.Lathe((p, n) => LidSteel * (1f + 0.04f * Mathf.Sin(p.x * 17f + p.z * 11f)), Vector3.zero, profile, 28);

            for (int k = 0; k < 4; k++)
            {
                float a = (k + 0.5f) * Mathf.PI * 0.5f;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Quaternion face = Quaternion.LookRotation(outward);
                Vector3 at = outward * (PotRim + 0.02f) + Vector3.up * (PotRimY + 0.005f);

                // A clamp hooked under the rim, a bolt up through it, and a wing nut on top - too big, as a toy's would be.
                m.Box(IronDark, at, new Vector3(0.07f, 0.07f, 0.05f), 0.012f, face);
                m.Tube(SteelLight, at + Vector3.up * 0.03f, at + Vector3.up * 0.075f, 0.012f, 8);
                S.Ball(m, (p, n) => Steel, at + Vector3.up * 0.08f, new Vector3(0.026f, 0.018f, 0.026f), face, 10, 6);
                for (int wing = -1; wing <= 1; wing += 2)
                {
                    Vector3 tip = at + Vector3.up * 0.095f + face * new Vector3(wing * 0.045f, 0f, 0f);
                    m.Box(Steel, (at + Vector3.up * 0.085f + tip) * 0.5f, new Vector3(0.05f, 0.036f, 0.012f), 0.008f,
                          face * Quaternion.Euler(0f, 0f, -wing * 22f));
                }
            }
        }

        /// <summary>An oversized pressure gauge on a short pipe, leaning out from the lid: chrome bezel, cream face, its needle in the red.</summary>
        static void Gauge(B.Model m)
        {
            float lid = LidY(new Vector2(GaugeAt.x, GaugeAt.z).magnitude);
            Vector3 foot = new Vector3(GaugeAt.x, lid - 0.01f, GaugeAt.z);
            Vector3 centre = foot + new Vector3(-0.02f, 0.13f, 0.03f);
            m.Tube(Brass, foot, centre - new Vector3(0f, 0.04f, 0f), 0.018f, 8);

            Vector3 faceDir = new Vector3(-0.25f, 0.55f, 0.8f).normalized;
            Matrix4x4 was = m.Transform;
            m.Transform = was * Matrix4x4.TRS(centre, Quaternion.FromToRotation(Vector3.up, faceDir), Vector3.one);

            const float R = 0.085f;
            m.Lathe((p, n) => n.y > 0.5f && new Vector2(p.x, p.z).magnitude < R - 0.008f ? DialFace : SteelLight, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, -0.03f), new Vector2(R * 0.9f, -0.03f), new Vector2(R, -0.02f), new Vector2(R + 0.004f, 0.012f), new Vector2(R - 0.006f, 0.016f),
                new Vector2(R - 0.01f, 0.008f), new Vector2(0f, 0.008f),
            }, 24);

            // The red arc of danger along its top right, the ticks, and the needle swung into it.
            for (int k = 0; k < 9; k++)
            {
                float a = Mathf.Lerp(200f, -20f, k / 8f) * Mathf.Deg2Rad;
                Color tick = k >= 6 ? Red : Bakelite;
                var at = new Vector3(Mathf.Cos(a) * R * 0.72f, 0.0095f, Mathf.Sin(a) * R * 0.72f);
                m.Box(tick, at, new Vector3(0.012f, 0.002f, 0.004f), 0.0008f, Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f));
            }

            float needle = 10f * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(needle), 0f, Mathf.Sin(needle));
            m.Box(RedDark, dir * R * 0.32f + Vector3.up * 0.011f, new Vector3(R * 0.68f, 0.003f, 0.007f), 0.0015f,
                  Quaternion.Euler(0f, -needle * Mathf.Rad2Deg, 0f));
            S.Ball(m, (p, n) => Bakelite, Vector3.up * 0.011f, new Vector3(0.012f, 0.005f, 0.012f), Quaternion.identity, 8, 4);

            m.Transform = was;
        }

        /// <summary>The pressure valve's stem on the lid; its weight rocks on top of it.</summary>
        static void Valve(B.Model m)
        {
            float lid = LidY(new Vector2(ValveAt.x, ValveAt.z).magnitude);
            Vector3 foot = new Vector3(ValveAt.x, lid - 0.01f, ValveAt.z);
            m.Lathe((p, n) => SteelLight, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.04f, 0f), new Vector2(0.04f, 0.02f), new Vector2(0.018f, 0.03f), new Vector2(0.016f, 0.085f), new Vector2(0f, 0.085f),
            }, 12);
        }

        /// <summary>The valve's weight in its own space, axle along Z: a squat chrome bell with a knob, off to one side so it rocks as it turns.</summary>
        static B.Model ValveWeight()
        {
            var m = new B.Model();
            m.Transform = Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.up, Vector3.forward));
            m.Lathe((p, n) => n.y > 0.6f ? SteelLight : Steel, new Vector3(0.008f, 0f, 0f), new List<Vector2>
            {
                new Vector2(0f, -0.01f), new Vector2(0.038f, -0.01f), new Vector2(0.042f, 0.012f), new Vector2(0.03f, 0.04f), new Vector2(0.014f, 0.048f),
                new Vector2(0f, 0.05f),
            }, 14);
            S.Ball(m, (p, n) => Bakelite, new Vector3(0.008f, 0.062f, 0f), Vector3.one * 0.016f, Quaternion.identity, 8, 5);
            m.Box(IronDark, new Vector3(0.042f, 0.004f, 0f), new Vector3(0.022f, 0.016f, 0.016f), 0.005f);
            m.Transform = Matrix4x4.identity;
            return m;
        }

        /// <summary>The hopper: a wide galvanised funnel down into the lid, a red band under its rim, heaped with tomatoes.</summary>
        static void Funnel(B.Model m)
        {
            Vector3 foot = new Vector3(FunnelAt.x, FunnelBase, FunnelAt.z);
            float H = FunnelHeight;
            m.Lathe((p, n) =>
            {
                float y = (p.y - foot.y) / H;
                if (y > 0.82f && y < 0.92f) return Red;
                return Galvanised * (1f + 0.05f * Mathf.Sin(Mathf.Atan2(p.z - foot.z, p.x - foot.x) * 8f)) * (n.y > 0.3f ? 0.82f : 1f);
            }, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.06f, 0f), new Vector2(0.062f, H * 0.3f), new Vector2(0.09f, H * 0.42f),
                new Vector2(FunnelTop - 0.02f, H * 0.82f), new Vector2(FunnelTop - 0.012f, H * 0.92f), new Vector2(FunnelTop, H * 0.97f),
                new Vector2(FunnelTop + 0.012f, H), new Vector2(FunnelTop - 0.005f, H + 0.01f), new Vector2(FunnelTop - 0.03f, H * 0.95f),
                new Vector2(0.07f, H * 0.45f), new Vector2(0f, H * 0.42f),
            }, 24);

            // The heap: tomatoes packed into the mouth, the waiting one on top drawn on its own.
            for (int k = 0; k < 7; k++)
            {
                float a = k * Mathf.PI * 2f / 7f + 0.4f;
                var at = foot + new Vector3(Mathf.Cos(a) * 0.105f, H * 0.78f + 0.012f * (k % 3), Mathf.Sin(a) * 0.105f);
                Fruit(m, at, 0.058f, 80 + k);
            }
        }

        // --- What moves -------------------------------------------------------------------------------------

        /// <summary>
        /// The barrel from the pot's flange along +Z: a short galvanised pipe with a red band, opening
        /// into a wide dulled-brass bell with a rolled lip, dark inside. Toon-sized: the bell is twice
        /// the pipe's width.
        /// </summary>
        static B.Model Barrel()
        {
            var m = new B.Model();
            m.Transform = Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.up, Vector3.forward));

            const float Pipe = 0.075f, BellFrom = 0.3f;
            float L = BarrelLength;
            m.Lathe((p, n) =>
            {
                float z = p.y;
                if (z < BellFrom - 0.01f) return z > 0.16f && z < 0.21f ? Red : Galvanised * (1f + 0.04f * Mathf.Sin(z * 40f));
                // Inside the bell - facing the axis - in shadow.
                if (n.x * p.x + n.z * p.z < 0f) return BrassDark;
                return Brass * (1f + 0.05f * Mathf.Sin(Mathf.Atan2(p.z, p.x) * 6f));
            }, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(Pipe + 0.012f, 0f), new Vector2(Pipe + 0.012f, 0.04f), new Vector2(Pipe, 0.045f),
                new Vector2(Pipe, 0.16f), new Vector2(Pipe + 0.005f, 0.16f), new Vector2(Pipe + 0.005f, 0.21f), new Vector2(Pipe, 0.21f),
                new Vector2(Pipe, BellFrom), new Vector2(Pipe + 0.01f, BellFrom + 0.04f), new Vector2(0.105f, L - 0.16f), new Vector2(0.14f, L - 0.07f),
                new Vector2(BellRadius - 0.012f, L - 0.012f), new Vector2(BellRadius + 0.008f, L - 0.004f), new Vector2(BellRadius + 0.008f, L + 0.012f),
                new Vector2(BellRadius - 0.012f, L + 0.012f), new Vector2(BellRadius - 0.02f, L), new Vector2(0.12f, L - 0.08f),
                new Vector2(0.09f, L - 0.16f), new Vector2(0.07f, L - 0.22f), new Vector2(0f, L - 0.22f),
            }, 24);
            S.Disc(m, (p, n) => Bore, Vector3.up * (L - 0.215f), Vector3.up, 0.072f, 14);

            // A strap round the pipe.
            m.Tube(IronDark, Vector3.up * 0.1f, Vector3.up * 0.125f, Pipe + 0.01f, 16);
            m.Transform = Matrix4x4.identity;
            return m;
        }

        /// <summary>The tomato waiting on top of the heap, the next to go.</summary>
        static B.Model LoadedTomato()
        {
            var m = new B.Model();
            Fruit(m, Vector3.zero, 0.06f, 90);
            return m;
        }

        /// <summary>
        /// The tomato it throws, centred: a big ripe one, a little flattened, glossy and lighter on top,
        /// darker underneath, its green star of calyx and a stub of stalk. Twice life size, to read at
        /// the game camera's distance.
        /// </summary>
        static B.Model ThrownTomato()
        {
            var m = new B.Model();
            const float R = 0.1f;
            S.Ball(m, (p, n) => Color.Lerp(TomatoDark, TomatoLight, S.Smooth(p.y / (R * 1.7f) + 0.5f)) * (1f + 0.04f * Mathf.Sin(Mathf.Atan2(p.z, p.x) * 5f)),
                   Vector3.zero, new Vector3(R, R * 0.86f, R), Quaternion.identity, 18, 12);
            Calyces(m, Vector3.up * (R * 0.86f - 0.004f), R, 5);
            m.Tube((p, n) => Calyx, Vector3.up * (R * 0.82f), Vector3.up * (R * 0.82f + 0.035f), 0.009f, 0.006f, 6);
            return m;
        }

        // --- Tomatoes ---------------------------------------------------------------------------------------

        /// <summary>A tomato sitting on <paramref name="foot"/>: a red ball, flattened a little, its calyx on top.</summary>
        internal static void Fruit(B.Model m, Vector3 foot, float r, int key)
        {
            Color skin = Tomato * (1f + 0.06f * B.Jitter(key));
            Vector3 centre = foot + Vector3.up * (r * 0.86f);
            S.Ball(m, (p, n) => Color.Lerp(skin * 0.82f, skin * 1.08f, S.Smooth((p.y - foot.y) / (r * 1.7f))), centre, new Vector3(r, r * 0.86f, r),
                   Quaternion.Euler(0f, key * 37f, 0f), 12, 8);
            Calyces(m, centre + Vector3.up * (r * 0.86f - 0.002f), r, 5, key * 17f);
        }

        /// <summary>A star of <paramref name="leaves"/> pointed sepals, lying on the top of a tomato.</summary>
        static void Calyces(B.Model m, Vector3 top, float r, int leaves, float turn = 0f)
        {
            for (int k = 0; k < leaves; k++)
            {
                Quaternion q = Quaternion.Euler(0f, k * 360f / leaves + turn, 0f);
                m.Box(Calyx, top + q * new Vector3(r * 0.28f, -r * 0.03f, 0f), new Vector3(r * 0.55f, r * 0.08f, r * 0.16f), r * 0.035f,
                      q * Quaternion.Euler(0f, 0f, -12f));
            }
        }
    }
}
