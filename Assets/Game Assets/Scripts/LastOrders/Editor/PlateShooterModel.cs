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
    /// The Plate Shooter, modelled in code in the game's smooth low-poly style, and its prefab: the
    /// diner's plate dispenser on a bus cart, with a clay-pigeon arm on top.
    ///
    /// A steel bus cart on four fat casters, a red rubber bumper round its top shelf, a grey dish tub
    /// of plates and a mug on the bottom one, a tea towel over its push handle. On a turntable on top,
    /// a chunky motor housing in the diner's teal with a cream band, vents and a big red mushroom
    /// button; a chrome dispenser well beside it with its spring-loaded stack of plates peeping over
    /// the rim; and on a spindle in front, a long red flinger arm that sweeps the top plate off the
    /// stack and skims it away. A desk bell on the motor, a stack of plates on the ground. Toon
    /// proportions - casters, button, bell and arm all too big - in the diner's own colours.
    ///
    /// What moves: the top turns to track (the aim pivot); the flinger arm spins while it has
    /// something in sight, faster on each throw (<see cref="DefenseBarrelSpin"/>), and kicks back on
    /// its spindle as it lets go (<see cref="DefenseRecoil"/>); the plate on top of the stack is gone
    /// as it is thrown and back as the spring lifts the next (<see cref="DefenseReload"/>). What it
    /// throws is a dinner plate, skimmed nearly flat and spinning like a frisbee
    /// (<see cref="DefenseProjectiles"/>).
    ///
    /// Local +Z is the way it faces at rest. Re-running rebuilds the meshes in place and re-dresses the
    /// prefab's own objects by name.
    /// </summary>
    public static class PlateShooterModel
    {
        public const string PrefabPath = DefensePrefabGenerator.Folder + "/Defense_PlateShooter.prefab";

        internal const string Folder = "Assets/Game Assets/Content/LastOrders/Defense/" + EnvKit.Style + "/PlateShooter";

        // --- Measurements (metres, model space; it faces +Z) ----------------------------------------

        const float CasterX = 0.4f, CasterZ = 0.32f, CasterRadius = 0.07f;
        const float PostX = 0.42f, PostZ = 0.34f;
        const float LowShelf = 0.2f, TopShelf = 0.6f;
        const float ShelfHalfX = 0.45f, ShelfHalfZ = 0.37f;

        /// <summary>The turntable on the top shelf.</summary>
        const float PivotY = TopShelf + 0.06f;

        // Head space, from the pivot.
        const float MotorTop = 0.26f;
        static readonly Vector3 MotorCentre = new Vector3(0f, MotorTop * 0.5f + 0.01f, -0.05f);
        static readonly Vector3 MotorSize = new Vector3(0.42f, MotorTop - 0.02f, 0.44f);

        static readonly Vector3 WellAt = new Vector3(0f, MotorTop, -0.1f);
        const float WellRadius = 0.17f, WellHeight = 0.24f;

        const float PlateRadius = 0.15f, PlateThickness = 0.03f;

        static readonly Vector3 SpindleAt = new Vector3(0f, MotorTop, 0.12f);
        const float RotorY = MotorTop + WellHeight + 0.085f;
        const float RotorReach = 0.27f;

        // --- The palette -------------------------------------------------------------------------------

        static readonly Color Teal = B.Hex("3B6966");
        static readonly Color TealDark = B.Hex("2F5653");
        static readonly Color Cream = B.Hex("AFA898");
        static readonly Color Red = B.Hex("8E3B33");
        static readonly Color RedDark = B.Hex("6E2D27");
        static readonly Color IronDark = B.Hex("2F3236");
        // Steel kept a shade down: a cart's worth of flat shelves and a chrome drum read white from above otherwise.
        static readonly Color Steel = B.Hex("7F858A");
        static readonly Color SteelLight = B.Hex("8E9499");
        static readonly Color SteelDark = B.Hex("666C71");
        static readonly Color TubGrey = B.Hex("6A6E70");
        static readonly Color Rubber = B.Hex("2A2A2B");
        static readonly Color RubberGrey = B.Hex("3B3A39");
        static readonly Color Bore = B.Hex("161616");

        /// <summary>The plates' glaze: off-white kept to a mid value, or its lit side clips white.</summary>
        internal static readonly Color Glaze = B.Hex("B4B0A5");
        internal static readonly Color GlazeShade = B.Hex("9C988D");
        internal static readonly Color Rim = B.Hex("3B6966");

        [MenuItem("Last Orders/Defense/Rebuild Plate Shooter (Model & Prefab)", priority = 48)]
        static void Run()
        {
            DefenseDefinition shooter = DefenseContentGenerator.GeneratePlateShooter();
            Debug.Log(Apply(shooter, DefenseContentGenerator.LoadPlateShooterMotion()));
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
                ["Base"] = Save(Base(), "PlateShooter_Base"),
                ["Head"] = Save(Head(), "PlateShooter_Head"),
                ["Rotor"] = Save(Rotor(), "PlateShooter_Rotor"),
                ["Loaded"] = Save(LoadedPlate(), "PlateShooter_Loaded"),
                ["Shot"] = Save(ThrownPlate(), "PlateShooter_Plate"),
            };

            PooledEffect launch = FoodShooterEffects.PlateLaunch();
            PooledEffect impact = FoodShooterEffects.PlateImpact();

            bool created = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            GameObject root = created ? new GameObject("Defense_PlateShooter") : PrefabUtility.LoadPrefabContents(PrefabPath);

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

            int vertices = meshes["Base"].vertexCount + meshes["Head"].vertexCount + meshes["Rotor"].vertexCount + meshes["Loaded"].vertexCount;
            return $"[Last Orders] Plate Shooter (dispenser flinger) {(created ? "built" : "rebuilt")}: {vertices} vertices, " +
                   $"the thrown plate {meshes["Shot"].vertexCount}; prefab at {PrefabPath}.";
        }

        // --- The prefab --------------------------------------------------------------------------------

        static void Dress(GameObject root, DefenseDefinition definition, DefenseMotionProfile motion, Dictionary<string, Mesh> meshes,
                          PooledEffect launch, PooledEffect impact)
        {
            Material painted = B.PaintedMaterial();

            Transform built = C.Child(root.transform, "Flinger", Vector3.zero);
            B.Show(C.Child(built, "Base", Vector3.zero).gameObject, meshes["Base"], new[] { painted });

            // The aim pivot: the motor, the well and the arm turn together on the top shelf.
            Transform head = C.Child(built, "Head", new Vector3(0f, PivotY, 0f));
            B.Show(head.gameObject, meshes["Head"], new[] { painted });

            // The arm's spindle head kicks back on a throw; the arm spins on it, its axle upright.
            Transform kick = C.Child(head, "Spindle", new Vector3(SpindleAt.x, RotorY, SpindleAt.z));
            Transform rotor = C.Child(kick, "Rotor", Vector3.zero);
            rotor.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            B.Show(rotor.gameObject, meshes["Rotor"], new[] { painted });

            Transform loaded = C.Child(head, "Loaded", new Vector3(WellAt.x, WellAt.y + WellHeight + 0.01f, WellAt.z));
            B.Show(loaded.gameObject, meshes["Loaded"], new[] { painted });

            Transform muzzle = C.Child(head, "Muzzle", new Vector3(0f, RotorY, SpindleAt.z + RotorReach + 0.08f));

            Transform site = C.Child(root.transform, "Site", Vector3.zero);
            B.Show(site.gameObject, meshes["Base"], new[] { painted });

            Transform shot = C.Child(root.transform, "Projectile_Plate", Vector3.zero);
            B.Show(shot.gameObject, meshes["Shot"], new[] { painted });
            C.Quiet(shot.gameObject);
            shot.gameObject.SetActive(false);

            var tap = root.GetComponent<BoxCollider>();
            if (tap == null) tap = root.AddComponent<BoxCollider>();

            var post = root.GetComponent<DefensePost>();
            if (post == null) post = root.AddComponent<DefensePost>();

            post.Configure(null, definition, "plate_shooter", false);
            post.ConfigureParts(built.gameObject, site.gameObject, head, muzzle, null, null, null, tap);
            post.ConfigureMotion(motion, builtSize: new Vector3(1.7f, 1.5f, 1.7f), siteSize: new Vector3(1.6f, 0.7f, 1.6f),
                                 calloutAt: new Vector3(0f, 2.2f, 0f), buildButtonAt: new Vector3(0f, 1.1f, 0f));
            post.ConfigureMuzzleEffect(launch);

            var recoil = root.GetComponent<DefenseRecoil>();
            if (recoil == null) recoil = root.AddComponent<DefenseRecoil>();
            recoil.Configure(post, new[] { kick }, new Transform[0], null);

            var spinners = new List<DefenseBarrelSpin>(root.GetComponents<DefenseBarrelSpin>());
            while (spinners.Count < 1) spinners.Add(root.AddComponent<DefenseBarrelSpin>());
            for (int i = 1; i < spinners.Count; i++) Object.DestroyImmediate(spinners[i]);
            spinners[0].Configure(post, rotor, 2, 900f, 1f);

            var reload = root.GetComponent<DefenseReload>();
            if (reload == null) reload = root.AddComponent<DefenseReload>();
            reload.Configure(post, loaded.gameObject);

            var thrown = root.GetComponent<DefenseProjectiles>();
            if (thrown == null) thrown = root.AddComponent<DefenseProjectiles>();
            thrown.Configure(post, shot.gameObject, impact);

            // Toon life: the motor hums, and the whole top jumps as the arm lets go and springs back past its size.
            var living = root.GetComponent<LivingDefenseAnimation>();
            if (living == null) living = root.AddComponent<LivingDefenseAnimation>();
            living.Configure(post, head, new Transform[0], null, null, 0f, new Transform[0]);

            built.gameObject.SetActive(true);
            site.gameObject.SetActive(false);
        }

        static Mesh Save(B.Model model, string name) => model.Save($"{Folder}/{name}.asset", name);

        // --- The base: the bus cart --------------------------------------------------------------------

        static B.Model Base()
        {
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.8f, 1f, S.Smooth(p.y / 0.6f)) };

            for (int k = 0; k < 4; k++)
            {
                float x = (k & 1) == 0 ? -1f : 1f, z = (k & 2) == 0 ? -1f : 1f;
                Caster(m, new Vector3(x * CasterX, 0f, z * CasterZ), k);
                m.Tube(SteelLight, new Vector3(x * PostX, 0.15f, z * PostZ), new Vector3(x * PostX, TopShelf + 0.02f, z * PostZ), 0.024f, 10);
                S.Ball(m, (p, n) => SteelLight, new Vector3(x * PostX, TopShelf + 0.025f, z * PostZ), Vector3.one * 0.03f, Quaternion.identity, 8, 5);
            }

            Shelf(m, LowShelf, bumper: false);
            Shelf(m, TopShelf, bumper: true);

            // The dish tub on the bottom shelf, plates stacked in it, a mug.
            Vector3 tub = new Vector3(-0.08f, LowShelf + 0.02f, 0f);
            Tub(m, tub, new Vector3(0.56f, 0.16f, 0.46f));
            for (int k = 0; k < 5; k++) Plate(m, tub + new Vector3(-0.1f, 0.02f + k * 0.022f, 0.02f), 0.12f, k);
            Mug(m, tub + new Vector3(0.15f, 0.015f, 0.1f), 20);
            Mug(m, tub + new Vector3(0.16f, 0.015f, -0.1f), 21);

            // The push handle at the back, a tea towel thrown over it.
            float handleY = TopShelf + 0.28f, handleZ = -PostZ - 0.1f;
            for (int side = -1; side <= 1; side += 2)
            {
                var a = new Vector3(side * PostX, TopShelf + 0.02f, -PostZ);
                var b = new Vector3(side * (PostX - 0.04f), handleY, handleZ);
                m.Tube(SteelLight, a, b, 0.022f, 10);
                S.Ball(m, (p, n) => SteelLight, b, Vector3.one * 0.022f, Quaternion.identity, 8, 5);
            }

            m.Tube(Rubber, new Vector3(-(PostX - 0.04f), handleY, handleZ), new Vector3(PostX - 0.04f, handleY, handleZ), 0.03f, 12);
            Towel(m, new Vector3(0.12f, handleY, handleZ));

            // A stack of plates on the ground by the cart, and one dropped beside it.
            Vector3 stack = new Vector3(0.66f, 0f, 0.5f);
            for (int k = 0; k < 7; k++) Plate(m, stack + new Vector3(0.006f * B.Jitter(k), k * 0.024f, 0.006f * B.Jitter(k + 9)), 0.13f, k + 30);
            Plate(m, new Vector3(-0.66f, 0f, 0.52f), 0.13f, 40, Quaternion.Euler(4f, 30f, -6f));
            return m;
        }

        /// <summary>A fat swivel caster: a plate under the post, a fork, a grey-treaded rubber wheel.</summary>
        static void Caster(B.Model m, Vector3 foot, int key)
        {
            m.Box(SteelDark, foot + Vector3.up * 0.145f, new Vector3(0.1f, 0.016f, 0.1f), 0.006f);
            m.Tube(IronDark, foot + Vector3.up * 0.14f, foot + Vector3.up * 0.115f, 0.03f, 10);
            float yaw = 25f * B.Jitter(key + 50);
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            for (int side = -1; side <= 1; side += 2)
                m.Box(SteelDark, foot + turn * new Vector3(side * 0.035f, 0.09f, -0.01f), new Vector3(0.012f, 0.07f, 0.06f), 0.005f, turn);

            Matrix4x4 was = m.Transform;
            m.Transform = was * Matrix4x4.TRS(foot + turn * new Vector3(0f, CasterRadius, -0.01f), turn * Quaternion.FromToRotation(Vector3.up, Vector3.right),
                                              Vector3.one);
            float r = CasterRadius, w = 0.026f;
            m.Lathe((p, n) => new Vector2(p.x, p.z).magnitude > r - 0.012f ? RubberGrey : Rubber, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, -w), new Vector2(r * 0.85f, -w), new Vector2(r, -w * 0.5f), new Vector2(r, w * 0.5f), new Vector2(r * 0.85f, w),
                new Vector2(0f, w),
            }, 18);
            m.Tube(SteelLight, Vector3.up * -(w + 0.008f), Vector3.up * (w + 0.008f), 0.018f, 8);
            m.Transform = was;
        }

        /// <summary>A steel shelf with a turned-up lip, and on the top one a red rubber bumper all round.</summary>
        static void Shelf(B.Model m, float y, bool bumper)
        {
            m.Box(Steel, new Vector3(0f, y, 0f), new Vector3(ShelfHalfX * 2f, 0.025f, ShelfHalfZ * 2f), 0.008f);
            for (int side = -1; side <= 1; side += 2)
            {
                m.Box(SteelDark, new Vector3(side * ShelfHalfX, y + 0.018f, 0f), new Vector3(0.016f, 0.04f, ShelfHalfZ * 2f), 0.005f);
                m.Box(SteelDark, new Vector3(0f, y + 0.018f, side * ShelfHalfZ), new Vector3(ShelfHalfX * 2f, 0.04f, 0.016f), 0.005f);
            }

            if (!bumper) return;
            for (int side = -1; side <= 1; side += 2)
            {
                m.Box(Red, new Vector3(side * (ShelfHalfX + 0.02f), y + 0.005f, 0f), new Vector3(0.035f, 0.05f, ShelfHalfZ * 2f + 0.07f), 0.016f);
                m.Box(Red, new Vector3(0f, y + 0.005f, side * (ShelfHalfZ + 0.02f)), new Vector3(ShelfHalfX * 2f + 0.07f, 0.05f, 0.035f), 0.016f);
            }
        }

        /// <summary>A grey plastic dish tub: a bottom and four sloped sides, open on top.</summary>
        static void Tub(B.Model m, Vector3 foot, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            m.Box(TubGrey * 0.85f, foot + Vector3.up * 0.01f, new Vector3(size.x - 0.04f, 0.02f, size.z - 0.04f), 0.008f);
            for (int side = -1; side <= 1; side += 2)
            {
                m.Box(TubGrey, foot + new Vector3(side * (h.x - 0.012f), h.y, 0f), new Vector3(0.024f, size.y, size.z), 0.01f,
                      Quaternion.Euler(0f, 0f, side * -5f));
                m.Box(TubGrey, foot + new Vector3(0f, h.y, side * (h.z - 0.012f)), new Vector3(size.x, size.y, 0.024f), 0.01f,
                      Quaternion.Euler(side * 5f, 0f, 0f));
            }
        }

        /// <summary>A diner mug: cream, a red band, a handle.</summary>
        static void Mug(B.Model m, Vector3 foot, int key)
        {
            const float R = 0.04f, H = 0.085f;
            m.Lathe((p, n) =>
            {
                float y = (p.y - foot.y) / H;
                if (n.y > 0.6f && y > 0.1f && y < 0.9f) return Bore;
                return y > 0.62f && y < 0.76f ? Red : Glaze;
            }, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(R * 0.9f, 0f), new Vector2(R, H * 0.1f), new Vector2(R, H * 0.62f), new Vector2(R, H * 0.76f),
                new Vector2(R, H), new Vector2(R - 0.006f, H), new Vector2(R - 0.006f, H * 0.2f), new Vector2(0f, H * 0.2f),
            }, 14);
            Vector3 side = Quaternion.Euler(0f, key * 53f, 0f) * Vector3.right;
            m.Tube(Glaze, foot + side * R + Vector3.up * H * 0.75f, foot + side * (R + 0.03f) + Vector3.up * H * 0.62f, 0.008f, 6);
            m.Tube(Glaze, foot + side * (R + 0.03f) + Vector3.up * H * 0.62f, foot + side * (R + 0.026f) + Vector3.up * H * 0.3f, 0.008f, 6);
            m.Tube(Glaze, foot + side * (R + 0.026f) + Vector3.up * H * 0.3f, foot + side * R + Vector3.up * H * 0.22f, 0.008f, 6);
        }

        /// <summary>A tea towel thrown over the handle: cream with two red stripes, hanging down either side of it.</summary>
        static void Towel(B.Model m, Vector3 at)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float lean = side * 8f;
                Quaternion q = Quaternion.Euler(lean, 4f, 0f);
                Vector3 centre = at + new Vector3(0f, -0.1f, side * 0.034f);
                m.Box(Cream, centre, new Vector3(0.2f, 0.2f, 0.012f), 0.005f, q);
                foreach (float y in new[] { -0.05f, -0.075f })
                    m.Box(Red, centre + q * new Vector3(0f, y, side * 0.0065f), new Vector3(0.2f, 0.012f, 0.002f), 0.001f, q);
            }

            m.Tube(Cream, at + new Vector3(-0.1f, 0.004f, 0f), at + new Vector3(0.1f, 0.004f, 0f), 0.036f, 10);
        }

        // --- The head: motor, dispenser, spindle -----------------------------------------------------------

        static B.Model Head()
        {
            var m = new B.Model { Shade = (p, n) => n.y < -0.4f ? 0.8f : Mathf.Lerp(0.88f, 1f, S.Smooth(p.y / 0.4f)) };

            m.Lathe((p, n) => n.y > 0.8f ? SteelDark : IronDark, new Vector3(0f, -0.06f, 0f), new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.2f, 0f), new Vector2(0.2f, 0.04f), new Vector2(0.18f, 0.06f), new Vector2(0f, 0.06f),
            }, 20);

            Motor(m);
            Well(m);
            Spindle(m);
            Bell(m, new Vector3(-0.13f, MotorTop, 0.06f));
            return m;
        }

        /// <summary>
        /// The motor housing: a chunky round-cornered box in teal, banded in cream, a chrome kick strip
        /// round its foot, vents down its sides and a big red mushroom button on top.
        /// </summary>
        static void Motor(B.Model m)
        {
            float bandY = MotorCentre.y + 0.01f;
            Vector3 low = new Vector3(MotorSize.x, bandY - 0.02f - 0.01f, MotorSize.z);
            Vector3 high = new Vector3(MotorSize.x, MotorTop - bandY - 0.02f, MotorSize.z);
            C.PaintedBox(m, (p, n) => C.Chipped(p, 2, Teal), new Vector3(0f, 0.01f + low.y * 0.5f, MotorCentre.z), low, 0.05f, 3);
            C.PaintedBox(m, (p, n) => C.Chipped(p, 4, Teal), new Vector3(0f, MotorTop - high.y * 0.5f, MotorCentre.z), high, 0.06f, 3);
            C.PaintedBox(m, (p, n) => Cream, new Vector3(0f, bandY, MotorCentre.z), new Vector3(MotorSize.x - 0.012f, 0.045f, MotorSize.z - 0.012f), 0.03f, 2);
            m.Box(SteelLight, new Vector3(0f, 0.018f, MotorCentre.z), new Vector3(MotorSize.x + 0.012f, 0.028f, MotorSize.z + 0.012f), 0.01f);

            // Vents: dark louvres down each side.
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 4; k++)
                {
                    float z = MotorCentre.z - 0.12f + k * 0.06f;
                    m.Box(TealDark * 0.6f, new Vector3(side * (MotorSize.x * 0.5f + 0.002f), 0.07f, z), new Vector3(0.008f, 0.06f, 0.026f), 0.004f);
                }

            // The start button, too big, on a cream collar.
            Vector3 button = new Vector3(0.12f, MotorTop, 0.07f);
            m.Lathe((p, n) => Cream, button, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.05f, 0f), new Vector2(0.045f, 0.025f), new Vector2(0f, 0.025f),
            }, 14);
            m.Lathe((p, n) => n.y > 0.5f ? Red * 1.08f : RedDark, button + Vector3.up * 0.022f, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.025f, 0f), new Vector2(0.025f, 0.02f), new Vector2(0.055f, 0.03f), new Vector2(0.058f, 0.045f),
                new Vector2(0.04f, 0.062f), new Vector2(0f, 0.068f),
            }, 16);

            // The cord out of the back, coiled once on the housing's back plate.
            Vector3 port = new Vector3(0.1f, 0.08f, MotorCentre.z - MotorSize.z * 0.5f);
            m.Tube(IronDark, port, port + Vector3.back * 0.03f, 0.02f, 8);
            Vector3 previous = port + Vector3.back * 0.03f;
            for (int i = 1; i <= 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                var p = port + new Vector3(-0.06f + Mathf.Cos(a) * 0.06f, Mathf.Sin(a) * 0.05f, -0.035f - 0.001f * i);
                m.Tube(Rubber, previous, p, 0.011f, 6);
                previous = p;
            }
        }

        /// <summary>The dispenser well: a chrome drum with a rolled rim, its stack of plates peeping over the top.</summary>
        static void Well(B.Model m)
        {
            float R = WellRadius, H = WellHeight;
            m.Lathe((p, n) =>
            {
                float y = (p.y - WellAt.y) / H;
                if (n.y > 0.5f && y < 0.95f) return Bore;
                bool band = y > 0.12f && y < 0.2f;
                return band ? Red : SteelLight * (1f + 0.05f * Mathf.Sin(Mathf.Atan2(p.z - WellAt.z, p.x - WellAt.x) * 10f));
            }, WellAt, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(R, 0f), new Vector2(R, H * 0.12f), new Vector2(R, H * 0.2f), new Vector2(R, H - 0.02f),
                new Vector2(R + 0.016f, H - 0.008f), new Vector2(R + 0.012f, H + 0.004f), new Vector2(R - 0.012f, H), new Vector2(R - 0.012f, H * 0.5f),
                new Vector2(0f, H * 0.5f),
            }, 24);

            // The stack: plates on the spring platform, rising to just under the rim.
            for (int k = 0; k < 5; k++)
                Plate(m, WellAt + Vector3.up * (H - 0.12f + k * 0.024f), PlateRadius - 0.02f, k + 10);

            // Two guide rods up its front, holding the top plate in for the arm.
            for (int side = -1; side <= 1; side += 2)
            {
                var at = WellAt + new Vector3(side * 0.09f, 0f, R * 0.9f);
                m.Tube(SteelDark, at + Vector3.up * (H - 0.02f), at + Vector3.up * (H + 0.06f), 0.01f, 6);
            }
        }

        /// <summary>The spindle the arm turns on: a gear drum on the housing, a steel shaft up to the arm.</summary>
        static void Spindle(B.Model m)
        {
            m.Lathe((p, n) => n.y > 0.6f ? SteelDark : IronDark, SpindleAt, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.065f, 0f), new Vector2(0.065f, 0.05f), new Vector2(0.045f, 0.065f), new Vector2(0f, 0.065f),
            }, 16);
            m.Tube(SteelLight, SpindleAt + Vector3.up * 0.06f, new Vector3(SpindleAt.x, RotorY - 0.02f, SpindleAt.z), 0.028f, 10);
        }

        /// <summary>A chrome desk bell on a black base: the order bell, ready for when the plate lands.</summary>
        static void Bell(B.Model m, Vector3 foot)
        {
            m.Lathe((p, n) => IronDark, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.05f, 0f), new Vector2(0.05f, 0.012f), new Vector2(0f, 0.012f),
            }, 14);
            m.Lathe((p, n) => SteelLight, foot + Vector3.up * 0.012f, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.046f, 0f), new Vector2(0.044f, 0.012f), new Vector2(0.036f, 0.03f), new Vector2(0.02f, 0.04f),
                new Vector2(0f, 0.043f),
            }, 16);
            m.Tube(SteelDark, foot + Vector3.up * 0.054f, foot + Vector3.up * 0.068f, 0.005f, 6);
            S.Ball(m, (p, n) => SteelLight, foot + Vector3.up * 0.07f, new Vector3(0.012f, 0.006f, 0.012f), Quaternion.identity, 8, 4);
        }

        // --- What moves -------------------------------------------------------------------------------------

        /// <summary>
        /// The flinger arm in its own space, axle along Z: a long arm in chipped diner red across the
        /// spindle, a black rubber paddle at its throwing end, a round counterweight at the other, a
        /// chrome hub cap.
        /// </summary>
        static B.Model Rotor()
        {
            var m = new B.Model();

            C.PaintedBox(m, (p, n) => C.Chipped(p, 6, Red), Vector3.zero, new Vector3(RotorReach * 2f, 0.07f, 0.026f), 0.012f, 2);
            m.Box(Rubber, new Vector3(RotorReach - 0.02f, 0f, 0.012f), new Vector3(0.05f, 0.09f, 0.05f), 0.014f);
            m.Tube(IronDark, new Vector3(-RotorReach + 0.03f, 0f, -0.02f), new Vector3(-RotorReach + 0.03f, 0f, 0.035f), 0.045f, 14);

            m.Transform = Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.up, Vector3.forward));
            m.Lathe((p, n) => SteelLight, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, -0.02f), new Vector2(0.045f, -0.02f), new Vector2(0.045f, 0.02f), new Vector2(0.03f, 0.035f), new Vector2(0f, 0.04f),
            }, 14);
            m.Transform = Matrix4x4.identity;
            return m;
        }

        /// <summary>The plate on top of the stack, waiting for the arm.</summary>
        static B.Model LoadedPlate()
        {
            var m = new B.Model();
            Plate(m, Vector3.zero, PlateRadius - 0.02f, 99);
            return m;
        }

        /// <summary>The plate it throws, centred: a dinner plate in off-white glaze with a teal band round its rim. Twice life size.</summary>
        static B.Model ThrownPlate()
        {
            var m = new B.Model();
            Plate(m, Vector3.down * (PlateThickness * 0.5f), PlateRadius, 0);
            return m;
        }

        // --- Plates -----------------------------------------------------------------------------------------

        /// <summary>
        /// A dinner plate of radius <paramref name="r"/> on <paramref name="foot"/>: a foot ring under it,
        /// a shallow well, a rim sloping up to a rounded edge, off-white with a teal band round the rim.
        /// </summary>
        internal static void Plate(B.Model m, Vector3 foot, float r, int key, Quaternion? turn = null)
        {
            Matrix4x4 was = m.Transform;
            m.Transform = was * Matrix4x4.TRS(foot, turn ?? Quaternion.Euler(0f, key * 41f, 0f), Vector3.one);

            float k = r / PlateRadius, t = PlateThickness * Mathf.Lerp(0.8f, 1f, k);
            Color glaze = Glaze * (1f + 0.025f * B.Jitter(key));
            m.Lathe((p, n) =>
            {
                float d = new Vector2(p.x, p.z).magnitude / r;
                if (n.y < -0.3f) return GlazeShade;
                if (n.y > 0.3f && d > 0.74f && d < 0.92f) return Rim;
                return glaze;
            }, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, 0.004f * k), new Vector2(r * 0.5f, 0.004f * k), new Vector2(r * 0.53f, 0f), new Vector2(r * 0.58f, 0f),
                new Vector2(r * 0.6f, 0.005f * k), new Vector2(r * 0.86f, t * 0.55f), new Vector2(r, t * 0.85f), new Vector2(r, t),
                new Vector2(r * 0.92f, t * 0.98f), new Vector2(r * 0.83f, t * 0.86f), new Vector2(r * 0.74f, t * 0.72f), new Vector2(r * 0.62f, t * 0.42f),
                new Vector2(r * 0.58f, t * 0.36f), new Vector2(0f, t * 0.36f),
            }, 22);

            m.Transform = was;
        }
    }
}
