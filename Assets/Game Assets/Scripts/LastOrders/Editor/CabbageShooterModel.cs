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
    /// The Cabbage Shooter, modelled in code in the game's smooth low-poly style, and its prefab: a
    /// spring catapult on a garden cart.
    ///
    /// A plank cart on two chunky spoked wheels and a pair of legs, a pull handle out the back. On a
    /// turntable on its bed, two timber A-frames carry the axle of a stout throwing arm, two fat coil
    /// springs in the diner's red wound round it either side. The arm's bucket is a kitchen colander,
    /// cream enamel with a red rim; it lies cocked back over a padded rest, and a padded crossbar up
    /// front stops it at the top of its throw. A winch at the back, a big red handwheel on its side,
    /// winds it down again. A crate of cabbages on the ground, two more rolled under the cart. Toon
    /// proportions - wheels, springs, bucket and handwheel all too big - in the props' muted paint.
    ///
    /// What moves: the cart's top turns to track (the aim pivot); the arm whips forward on every
    /// throw and is wound back down (<see cref="DefenseSwingArm"/>), the handwheel turning while it
    /// has something in sight (<see cref="DefenseBarrelSpin"/>); the cabbage in the bucket is gone as
    /// it is thrown and back once the arm is down (<see cref="DefenseReload"/>). What it throws is a
    /// whole cabbage, tumbling slowly along a high arc (<see cref="DefenseProjectiles"/>).
    ///
    /// Local +Z is the way it faces at rest. Re-running rebuilds the meshes in place and re-dresses the
    /// prefab's own objects by name.
    /// </summary>
    public static class CabbageShooterModel
    {
        public const string PrefabPath = DefensePrefabGenerator.Folder + "/Defense_CabbageShooter.prefab";

        internal const string Folder = "Assets/Game Assets/Content/LastOrders/Defense/" + EnvKit.Style + "/CabbageShooter";

        // --- Measurements (metres, model space; it faces +Z) ----------------------------------------

        const float WheelRadius = 0.25f, WheelX = 0.5f, WheelWidth = 0.08f;
        const float BedBottom = 0.3f, BedTop = 0.38f, BedHalfX = 0.42f, BedHalfZ = 0.56f;

        /// <summary>The turntable on the cart's bed.</summary>
        const float PivotY = BedTop + 0.05f;

        // Head space, from the pivot.
        const float DeckTop = 0.06f;
        const float FrameX = 0.23f;
        const float AxleY = 0.28f, AxleZ = 0f;
        const float ApexY = 0.56f, ApexZ = 0.19f;
        const float ArmLength = 0.5f, ArmWidth = 0.085f, ArmDepth = 0.075f;

        /// <summary>Degrees about X: lying back at rest, and against the crossbar at the top of the throw.</summary>
        internal const float Cocked = -60f, Thrown = 17f;

        const float BucketRadius = 0.135f, BucketDepth = 0.085f;

        /// <summary>Which way the bucket opens in the arm's space: up, when the arm lies cocked.</summary>
        static Vector3 Opening => Quaternion.Euler(-Cocked, 0f, 0f) * Vector3.up;

        static readonly Vector3 WinchAt = new Vector3(0f, 0.27f, -0.36f);

        /// <summary>The padded rest the cocked arm lies on, across the winch posts' tops: just under the arm at that depth.</summary>
        const float RestY = 0.405f;

        // --- The palette -------------------------------------------------------------------------------

        static readonly Color Red = B.Hex("8E3B33");
        static readonly Color RedDark = B.Hex("6E2D27");
        static readonly Color Cream = B.Hex("AFA898");
        static readonly Color CreamInside = B.Hex("968F80");
        static readonly Color IronDark = B.Hex("2F3236");
        static readonly Color Iron = B.Iron;
        static readonly Color Steel = B.Hex("8D9398");
        static readonly Color SteelLight = B.Hex("9EA4A9");
        static readonly Color Burlap = B.Hex("A9946F");
        static readonly Color BurlapDark = B.Hex("8A7655");
        static readonly Color Rope = B.Hex("B5A27E");
        static readonly Color Bore = B.Hex("1A1A1A");

        /// <summary>Timber a little warmer than the weathered planks, as a working tool's oiled wood is.</summary>
        static readonly Color Beam = B.Hex("7F6650");
        static readonly Color BeamDark = B.Hex("65513F");

        internal static readonly Color Heart = B.Hex("B4BC8C");
        internal static readonly Color Head = B.Hex("9CAE78");
        internal static readonly Color Inner = B.Hex("839B5E");
        internal static readonly Color Outer = B.Hex("67824A");
        internal static readonly Color Rib = B.Hex("C2C6A0");

        [MenuItem("Last Orders/Defense/Rebuild Cabbage Shooter (Model & Prefab)", priority = 47)]
        static void Run()
        {
            DefenseDefinition shooter = DefenseContentGenerator.GenerateCabbageShooter();
            Debug.Log(Apply(shooter, DefenseContentGenerator.LoadCabbageShooterMotion()));
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
                ["Base"] = Save(Base(), "CabbageShooter_Base"),
                ["Head"] = Save(HeadFrame(), "CabbageShooter_Head"),
                ["Arm"] = Save(Arm(), "CabbageShooter_Arm"),
                ["Winch"] = Save(Handwheel(), "CabbageShooter_Handwheel"),
                ["Loaded"] = Save(LoadedCabbage(), "CabbageShooter_Loaded"),
                ["Shot"] = Save(ThrownCabbage(), "CabbageShooter_Cabbage"),
            };

            PooledEffect launch = FoodShooterEffects.CabbageLaunch();
            PooledEffect impact = FoodShooterEffects.CabbageImpact();

            bool created = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            GameObject root = created ? new GameObject("Defense_CabbageShooter") : PrefabUtility.LoadPrefabContents(PrefabPath);

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

            int vertices = meshes["Base"].vertexCount + meshes["Head"].vertexCount + meshes["Arm"].vertexCount +
                           meshes["Winch"].vertexCount + meshes["Loaded"].vertexCount;
            return $"[Last Orders] Cabbage Shooter (cart catapult) {(created ? "built" : "rebuilt")}: {vertices} vertices, " +
                   $"the thrown cabbage {meshes["Shot"].vertexCount}; prefab at {PrefabPath}.";
        }

        // --- The prefab --------------------------------------------------------------------------------

        static void Dress(GameObject root, DefenseDefinition definition, DefenseMotionProfile motion, Dictionary<string, Mesh> meshes,
                          PooledEffect launch, PooledEffect impact)
        {
            Material painted = B.PaintedMaterial();

            Transform built = C.Child(root.transform, "Catapult", Vector3.zero);
            B.Show(C.Child(built, "Base", Vector3.zero).gameObject, meshes["Base"], new[] { painted });

            // The aim pivot: the frame, the arm and the winch turn together on the cart's bed.
            Transform head = C.Child(built, "Head", new Vector3(0f, PivotY, 0f));
            B.Show(head.gameObject, meshes["Head"], new[] { painted });

            // The arm's pivot on the axle: upright at zero, posed cocked by the swing add-on.
            Transform arm = C.Child(head, "Arm", new Vector3(0f, AxleY, AxleZ));
            arm.localRotation = Quaternion.Euler(Cocked, 0f, 0f);
            B.Show(arm.gameObject, meshes["Arm"], new[] { painted });

            Transform loaded = C.Child(arm, "Loaded", BucketCentre + Opening * 0.02f);
            B.Show(loaded.gameObject, meshes["Loaded"], new[] { painted });

            // The handwheel on the winch's right end, its axle across: it spins about its own Z, turned along X.
            Transform wheel = C.Child(head, "Handwheel", WinchAt + new Vector3(FrameX - 0.02f, 0f, 0f));
            wheel.localRotation = Quaternion.Euler(0f, 90f, 0f);
            B.Show(wheel.gameObject, meshes["Winch"], new[] { painted });

            // Where the bucket is at the top of its throw.
            Vector3 release = new Vector3(0f, AxleY, AxleZ) + Quaternion.Euler(Thrown, 0f, 0f) * (BucketCentre + Opening * 0.08f);
            Transform muzzle = C.Child(head, "Muzzle", release);

            Transform site = C.Child(root.transform, "Site", Vector3.zero);
            B.Show(site.gameObject, meshes["Base"], new[] { painted });

            Transform shot = C.Child(root.transform, "Projectile_Cabbage", Vector3.zero);
            B.Show(shot.gameObject, meshes["Shot"], new[] { painted });
            C.Quiet(shot.gameObject);
            shot.gameObject.SetActive(false);

            var tap = root.GetComponent<BoxCollider>();
            if (tap == null) tap = root.AddComponent<BoxCollider>();

            var post = root.GetComponent<DefensePost>();
            if (post == null) post = root.AddComponent<DefensePost>();

            post.Configure(null, definition, "cabbage_shooter", false);
            post.ConfigureParts(built.gameObject, site.gameObject, head, muzzle, null, null, null, tap);
            post.ConfigureMotion(motion, builtSize: new Vector3(1.8f, 1.6f, 1.9f), siteSize: new Vector3(1.6f, 0.7f, 1.8f),
                                 calloutAt: new Vector3(0f, 2.4f, 0f), buildButtonAt: new Vector3(0f, 1.1f, 0f));
            post.ConfigureMuzzleEffect(launch);

            var swing = root.GetComponent<DefenseSwingArm>();
            if (swing == null) swing = root.AddComponent<DefenseSwingArm>();
            swing.Configure(post, arm, Cocked, Thrown, 0.13f);

            var spinners = new List<DefenseBarrelSpin>(root.GetComponents<DefenseBarrelSpin>());
            while (spinners.Count < 1) spinners.Add(root.AddComponent<DefenseBarrelSpin>());
            for (int i = 1; i < spinners.Count; i++) Object.DestroyImmediate(spinners[i]);
            spinners[0].Configure(post, wheel, 6, 140f, -1f);

            var reload = root.GetComponent<DefenseReload>();
            if (reload == null) reload = root.AddComponent<DefenseReload>();
            reload.Configure(post, loaded.gameObject);

            var thrown = root.GetComponent<DefenseProjectiles>();
            if (thrown == null) thrown = root.AddComponent<DefenseProjectiles>();
            thrown.Configure(post, shot.gameObject, impact);

            // Toon life: the whole frame bucks down as the arm lets go and springs back past its size.
            var living = root.GetComponent<LivingDefenseAnimation>();
            if (living == null) living = root.AddComponent<LivingDefenseAnimation>();
            living.Configure(post, head, new Transform[0], null, null, 0f, new Transform[0]);

            built.gameObject.SetActive(true);
            site.gameObject.SetActive(false);
        }

        static Mesh Save(B.Model model, string name) => model.Save($"{Folder}/{name}.asset", name);

        // --- The base: the cart --------------------------------------------------------------------------

        static B.Model Base()
        {
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.78f, 1f, S.Smooth(p.y / 0.5f)) };

            // The bed: planks along it on two bearers, low side boards round it.
            for (int side = -1; side <= 1; side += 2)
                m.Box(BeamDark, new Vector3(side * 0.3f, BedBottom - 0.03f, 0f), new Vector3(0.08f, 0.07f, BedHalfZ * 2f - 0.04f), 0.012f);

            const int Boards = 6;
            for (int i = 0; i < Boards; i++)
            {
                float x = -BedHalfX + (i + 0.5f) * (BedHalfX * 2f / Boards);
                Color c = B.Plank(i * 3 + 1) * (1f + 0.04f * B.Jitter(i + 5));
                m.Box(c, new Vector3(x, (BedBottom + BedTop) * 0.5f, 0.01f * B.Jitter(i)), new Vector3(BedHalfX * 2f / Boards - 0.01f, BedTop - BedBottom, BedHalfZ * 2f),
                      0.012f);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                m.Box(B.Plank(side + 9), new Vector3(side * (BedHalfX + 0.015f), BedTop + 0.02f, 0f), new Vector3(0.03f, 0.13f, BedHalfZ * 2f), 0.01f);
                m.Box(B.Plank(side + 12), new Vector3(0f, BedTop + 0.02f, side * (BedHalfZ + 0.015f)), new Vector3(BedHalfX * 2f + 0.06f, 0.13f, 0.03f), 0.01f);
            }

            // Iron corner straps.
            for (int k = 0; k < 4; k++)
            {
                float x = (k & 1) == 0 ? -1f : 1f, z = (k & 2) == 0 ? -1f : 1f;
                m.Box(IronDark, new Vector3(x * (BedHalfX + 0.032f), BedTop + 0.02f, z * (BedHalfZ - 0.04f)), new Vector3(0.006f, 0.14f, 0.07f), 0.002f);
            }

            // The axle, the two wheels, and two legs at the front and the back to stand it level.
            m.Tube(IronDark, new Vector3(-WheelX, WheelRadius, 0f), new Vector3(WheelX, WheelRadius, 0f), 0.028f, 10);
            for (int side = -1; side <= 1; side += 2) Wheel(m, new Vector3(side * WheelX, WheelRadius, 0f), side);

            for (int end = -1; end <= 1; end += 2)
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 top = new Vector3(side * 0.3f, BedBottom - 0.05f, end * (BedHalfZ - 0.08f));
                    Vector3 foot = new Vector3(side * 0.33f, 0f, end * (BedHalfZ - 0.02f));
                    m.Box(BeamDark, (top + foot) * 0.5f, new Vector3(0.06f, top.y - foot.y + 0.02f, 0.06f), 0.012f,
                          Quaternion.FromToRotation(Vector3.up, top - foot));
                }

            // The pull handle out of the back: two rails to a wooden grip.
            for (int side = -1; side <= 1; side += 2)
            {
                var from = new Vector3(side * 0.22f, BedTop - 0.02f, -BedHalfZ - 0.02f);
                var to = new Vector3(side * 0.18f, 0.62f, -BedHalfZ - 0.3f);
                m.Tube(IronDark, from, to, 0.018f, 8);
            }

            m.Tube(B.HoneyLight, new Vector3(-0.22f, 0.62f, -BedHalfZ - 0.3f), new Vector3(0.22f, 0.62f, -BedHalfZ - 0.3f), 0.028f, 10);

            // Ammunition: a crate of cabbages by the front wheel, two loose under the bed.
            Vector3 crate = new Vector3(0.66f, 0f, 0.58f);
            S.OpenCrate(m, crate, new Vector3(0.34f, 0.2f, 0.3f), -10f, 7);
            for (int k = 0; k < 3; k++)
            {
                float a = k * 2.1f + 0.5f;
                Cabbage(m, crate + new Vector3(Mathf.Cos(a) * 0.07f, 0.2f, Mathf.Sin(a) * 0.06f), 0.085f, 40 + k, Quaternion.Euler(10f * k, k * 70f, 0f));
            }

            Cabbage(m, new Vector3(-0.1f, 0.075f, 0.32f), 0.08f, 50, Quaternion.Euler(20f, 30f, 0f));
            Cabbage(m, new Vector3(-0.66f, 0.08f, -0.42f), 0.085f, 51, Quaternion.Euler(-15f, 110f, 0f));
            return m;
        }

        /// <summary>A chunky spoked wooden wheel with an iron tyre, on the axle at <paramref name="hub"/>, <paramref name="side"/> -1 or 1.</summary>
        static void Wheel(B.Model m, Vector3 hub, int side)
        {
            Matrix4x4 was = m.Transform;
            m.Transform = was * Matrix4x4.TRS(hub, Quaternion.FromToRotation(Vector3.up, Vector3.right * side), Vector3.one);

            float w = WheelWidth * 0.5f, r = WheelRadius;
            m.Lathe((p, n) => new Vector2(p.x, p.z).magnitude > r - 0.025f ? IronDark : Beam * (1f + 0.05f * Mathf.Sin(Mathf.Atan2(p.z, p.x) * 8f)),
                    Vector3.zero, new List<Vector2>
                    {
                        new Vector2(r - 0.07f, -w), new Vector2(r - 0.025f, -w), new Vector2(r - 0.025f, -w - 0.004f), new Vector2(r, -w * 0.7f),
                        new Vector2(r, w * 0.7f), new Vector2(r - 0.025f, w + 0.004f), new Vector2(r - 0.025f, w), new Vector2(r - 0.07f, w),
                        new Vector2(r - 0.07f, -w),
                    }, 28);

            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                m.Tube((p, n) => BeamDark, dir * 0.06f, dir * (r - 0.065f), 0.022f, 0.016f, 8);
            }

            // The hub: a fat turned boss, its iron cap and pin.
            m.Lathe((p, n) => p.y > w + 0.01f ? IronDark : Beam, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, -w - 0.02f), new Vector2(0.07f, -w - 0.02f), new Vector2(0.075f, 0f), new Vector2(0.07f, w + 0.015f),
                new Vector2(0.045f, w + 0.02f), new Vector2(0.04f, w + 0.045f), new Vector2(0f, w + 0.05f),
            }, 16);

            m.Transform = was;
        }

        // --- The head: deck, frames, springs, winch --------------------------------------------------------

        static B.Model HeadFrame()
        {
            var m = new B.Model { Shade = (p, n) => n.y < -0.4f ? 0.8f : Mathf.Lerp(0.86f, 1f, S.Smooth(p.y / 0.5f)) };

            // The turntable and the deck boards on it.
            m.Lathe((p, n) => n.y > 0.8f ? Iron : IronDark, new Vector3(0f, -0.05f, 0f), new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.28f, 0f), new Vector2(0.28f, 0.03f), new Vector2(0.26f, 0.05f), new Vector2(0f, 0.05f),
            }, 24);
            for (int i = 0; i < 4; i++)
            {
                float z = -0.42f + i * 0.28f;
                m.Box(B.Plank(i * 7 + 4), new Vector3(0f, DeckTop * 0.5f, z), new Vector3(0.66f, DeckTop, 0.26f), 0.012f, Quaternion.Euler(0f, 1.5f * B.Jitter(i), 0f));
            }

            Frames(m);
            Springs(m);
            Winch(m);
            return m;
        }

        /// <summary>
        /// The two A-frames, one each side of the arm, joined at the top by the padded crossbar it stops
        /// against, and braced at the axle; the axle itself through them, big nuts on its ends.
        /// </summary>
        static void Frames(B.Model m)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * FrameX;
                var apex = new Vector3(x, ApexY, ApexZ);
                var back = new Vector3(x, DeckTop, -0.3f);
                var front = new Vector3(x, DeckTop, 0.4f);

                Beamed(m, back, apex, 0.075f, 21 + side);
                Beamed(m, front, apex, 0.075f, 23 + side);

                // The brace across at the axle's height, a bearing block on it.
                float t = (AxleY - DeckTop) / (ApexY - DeckTop);
                Vector3 b0 = Vector3.Lerp(back, apex, t), b1 = Vector3.Lerp(front, apex, t);
                Beamed(m, b0, b1, 0.06f, 25 + side);
                m.Box(IronDark, new Vector3(x, AxleY, AxleZ), new Vector3(0.09f, 0.11f, 0.11f), 0.02f, 2);

                // Iron gussets where the beams meet the deck.
                m.Box(IronDark, back + new Vector3(0f, 0.04f, 0.03f), new Vector3(0.085f, 0.06f, 0.1f), 0.006f);
                m.Box(IronDark, front + new Vector3(0f, 0.04f, -0.03f), new Vector3(0.085f, 0.06f, 0.1f), 0.006f);
            }

            // The crossbar at the top, wrapped in a burlap pad tied on with cord.
            var left = new Vector3(-FrameX - 0.05f, ApexY, ApexZ);
            var right = new Vector3(FrameX + 0.05f, ApexY, ApexZ);
            m.Tube(BeamDark, left, right, 0.04f, 10);
            m.Tube((p, n) => Burlap * (1f + 0.06f * Mathf.Sin(p.x * 60f)), new Vector3(-0.12f, ApexY, ApexZ), new Vector3(0.12f, ApexY, ApexZ), 0.06f, 0.06f, 12);
            foreach (float x in new[] { -0.1f, 0f, 0.1f })
                m.Tube(Rope, new Vector3(x - 0.006f, ApexY, ApexZ), new Vector3(x + 0.006f, ApexY, ApexZ), 0.064f, 12);

            // The axle through both frames, with a big hex nut on each end.
            m.Tube(SteelLight, new Vector3(-FrameX - 0.07f, AxleY, AxleZ), new Vector3(FrameX + 0.07f, AxleY, AxleZ), 0.026f, 10);
            for (int side = -1; side <= 1; side += 2)
                m.Tube(IronDark, new Vector3(side * (FrameX + 0.055f), AxleY, AxleZ), new Vector3(side * (FrameX + 0.085f), AxleY, AxleZ), 0.045f, 6);
        }

        /// <summary>Two fat coil springs round the axle either side of the arm, in the diner's red, their ends hooked under the deck.</summary>
        static void Springs(B.Model m)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                const int Turns = 4, Steps = 64;
                const float R = 0.075f, Wire = 0.016f;
                float from = side * 0.065f, to = side * (FrameX - 0.06f);

                Vector3 previous = Vector3.zero;
                for (int i = 0; i <= Steps; i++)
                {
                    float t = i / (float)Steps;
                    float a = t * Turns * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Lerp(from, to, t), AxleY + Mathf.Cos(a) * R, AxleZ + Mathf.Sin(a) * R);
                    if (i > 0)
                    {
                        int key = i;
                        m.Tube((q, n) => C.Chipped(q, key, Red) * (i % 2 == 0 ? 1f : 0.96f), previous, p, Wire, Wire, 6);
                    }

                    previous = p;
                }

                // The spring's leg down to the deck, and its hook.
                var leg = new Vector3(to, AxleY - R, AxleZ - 0.02f);
                m.Tube(RedDark, leg, new Vector3(to, DeckTop + 0.01f, AxleZ + 0.16f), Wire, 6);
            }
        }

        /// <summary>The winch at the back: a drum wound with rope between two posts, which also carry the padded rest the arm lies on.</summary>
        static void Winch(B.Model m)
        {
            for (int side = -1; side <= 1; side += 2)
                Beamed(m, new Vector3(side * 0.15f, DeckTop, WinchAt.z), new Vector3(side * 0.15f, RestY, WinchAt.z), 0.06f, 30 + side);

            m.Tube(BeamDark, new Vector3(-0.2f, WinchAt.y, WinchAt.z), new Vector3(FrameX, WinchAt.y, WinchAt.z), 0.02f, 8);
            Matrix4x4 was = m.Transform;
            m.Transform = was * Matrix4x4.TRS(WinchAt, Quaternion.FromToRotation(Vector3.up, Vector3.right), Vector3.one);
            m.Lathe((p, n) => Mathf.Abs(p.y) > 0.085f ? Beam : Rope * (1f + 0.08f * Mathf.Sin(p.y * 140f)), Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, -0.11f), new Vector2(0.075f, -0.11f), new Vector2(0.075f, -0.09f), new Vector2(0.055f, -0.088f),
                new Vector2(0.058f, 0f), new Vector2(0.055f, 0.088f), new Vector2(0.075f, 0.09f), new Vector2(0.075f, 0.11f), new Vector2(0f, 0.11f),
            }, 16);
            m.Transform = was;

            // The rest: a bar across the posts' tops, padded, under where the cocked arm lies.
            m.Tube(BeamDark, new Vector3(-0.19f, RestY, WinchAt.z), new Vector3(0.19f, RestY, WinchAt.z), 0.025f, 8);
            m.Tube(BurlapDark, new Vector3(-0.08f, RestY + 0.004f, WinchAt.z), new Vector3(0.08f, RestY + 0.004f, WinchAt.z), 0.036f, 10);
        }

        /// <summary>A squared timber from <paramref name="from"/> to <paramref name="to"/>, <paramref name="thickness"/> across.</summary>
        static void Beamed(B.Model m, Vector3 from, Vector3 to, float thickness, int key)
        {
            Vector3 along = to - from;
            Quaternion turn = Quaternion.LookRotation(along.normalized, Mathf.Abs(along.normalized.y) > 0.95f ? Vector3.forward : Vector3.up);
            Color c = Beam * (1f + 0.05f * B.Jitter(key));
            m.Box(c, (from + to) * 0.5f, new Vector3(thickness, thickness, along.magnitude + thickness * 0.4f), 0.014f, turn);
        }

        // --- What moves -------------------------------------------------------------------------------------

        /// <summary>Where the bucket's middle is, in the arm's space.</summary>
        static Vector3 BucketCentre => Vector3.up * (ArmLength + 0.02f) + Opening * 0.01f;

        /// <summary>
        /// The throwing arm in its own space, standing up from its axle along +Y: a stout timber with
        /// iron bands, a hub round the axle, a stub below it, and the colander bucket at its top.
        /// </summary>
        static B.Model Arm()
        {
            var m = new B.Model();

            // The beam, a little thicker at the root.
            var shape = new SmoothMesh(1);
            shape.RoundedBox(0, new Vector3(0f, (ArmLength - 0.1f) * 0.5f, 0f), new Vector3(ArmWidth, ArmLength + 0.1f, ArmDepth), 0.018f, Quaternion.identity, 2);
            m.Add(shape, (p, n) => Beam * (1f + 0.06f * Mathf.Sin(p.y * 31f)) * (Mathf.Abs(n.x) > 0.7f ? 0.9f : 1f));

            foreach (float y in new[] { 0.12f, 0.3f, ArmLength - 0.04f })
                m.Box(IronDark, Vector3.up * y, new Vector3(ArmWidth + 0.012f, 0.03f, ArmDepth + 0.012f), 0.006f);

            // The hub round the axle: iron cheeks either side.
            m.Tube(IronDark, Vector3.right * -0.06f, Vector3.right * 0.06f, 0.05f, 14);

            Bucket(m, BucketCentre - Opening * 0.01f);
            return m;
        }

        /// <summary>
        /// A kitchen colander as the bucket: cream enamel inside and out with a red rim, rows of dark
        /// holes, two little handles, bolted to the arm's top by its foot ring.
        /// </summary>
        static void Bucket(B.Model m, Vector3 foot)
        {
            Matrix4x4 was = m.Transform;
            m.Transform = was * Matrix4x4.TRS(foot, Quaternion.FromToRotation(Vector3.up, Opening), Vector3.one);

            float R = BucketRadius, D = BucketDepth;
            m.Lathe((p, n) =>
            {
                if (p.y > D - 0.006f) return Red;
                bool inside = n.y > 0.2f;
                return inside ? CreamInside : C.Chipped(p, 8, Cream);
            }, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(R * 0.45f, 0f), new Vector2(R * 0.75f, D * 0.25f), new Vector2(R * 0.95f, D * 0.7f),
                new Vector2(R, D), new Vector2(R + 0.014f, D + 0.006f), new Vector2(R + 0.012f, D + 0.012f), new Vector2(R - 0.008f, D + 0.004f),
                new Vector2(R * 0.88f, D * 0.72f), new Vector2(R * 0.68f, D * 0.3f), new Vector2(R * 0.4f, 0.012f), new Vector2(0f, 0.012f),
            }, 24);

            // The foot ring, and the holes in rings round its sides.
            m.Lathe((p, n) => Cream * 0.9f, Vector3.down * 0.022f, new List<Vector2>
            {
                new Vector2(R * 0.32f, 0f), new Vector2(R * 0.38f, 0f), new Vector2(R * 0.42f, 0.024f), new Vector2(R * 0.34f, 0.024f),
            }, 16);
            for (int ring = 0; ring < 2; ring++)
            {
                float y = D * (0.3f + 0.3f * ring), r = Mathf.Lerp(R * 0.78f, R * 0.95f, ring) + 0.002f;
                int count = 10 + ring * 2;
                for (int k = 0; k < count; k++)
                {
                    float a = (k + 0.5f * ring) * Mathf.PI * 2f / count;
                    var outward = new Vector3(Mathf.Cos(a), 0.3f, Mathf.Sin(a)).normalized;
                    S.Disc(m, (p, n) => Bore, new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), outward, 0.009f, 6);
                }
            }

            for (int side = -1; side <= 1; side += 2)
                m.Box(Red, new Vector3(side * (R + 0.03f), D, 0f), new Vector3(0.05f, 0.012f, 0.03f), 0.005f);

            m.Transform = was;

            // The bolt plate under the foot, onto the beam's end.
            m.Box(IronDark, foot - Opening * 0.03f, new Vector3(ArmWidth + 0.02f, 0.02f, ArmDepth + 0.02f), 0.006f,
                  Quaternion.FromToRotation(Vector3.up, Opening));
        }

        /// <summary>The winch's handwheel in its own space, axle along Z: a red rim on six spokes, a hub and a wooden knob to crank it by.</summary>
        static B.Model Handwheel()
        {
            var m = new B.Model();
            const int Steps = 24;
            const float R = 0.12f;

            for (int i = 0; i < Steps; i++)
            {
                float a0 = i / (float)Steps * Mathf.PI * 2f, a1 = (i + 1) / (float)Steps * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * R;
                var p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * R;
                int key = i;
                m.Tube((p, n) => C.Chipped(p, key + 40, Red), p0, p1, 0.02f, 0.02f, 8);
            }

            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f;
                m.Tube(RedDark, Vector3.zero, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (R - 0.01f), 0.011f, 6);
            }

            m.Tube(IronDark, new Vector3(0f, 0f, -0.03f), new Vector3(0f, 0f, 0.035f), 0.032f, 12);
            m.Tube(B.HoneyLight, new Vector3(R, 0f, 0.01f), new Vector3(R, 0f, 0.08f), 0.016f, 8);
            return m;
        }

        /// <summary>The cabbage waiting in the bucket.</summary>
        static B.Model LoadedCabbage()
        {
            var m = new B.Model();
            Cabbage(m, Vector3.zero, 0.115f, 60, Quaternion.FromToRotation(Vector3.up, Opening));
            return m;
        }

        /// <summary>The cabbage it throws, centred: a big firm head, tight pale leaves wrapped over it, darker outer leaves cupping it. Twice life size.</summary>
        static B.Model ThrownCabbage()
        {
            var m = new B.Model();
            Cabbage(m, Vector3.down * 0.15f, 0.17f, 70, Quaternion.identity);
            return m;
        }

        // --- Cabbages ---------------------------------------------------------------------------------------

        /// <summary>
        /// A cabbage of radius <paramref name="r"/> sitting on <paramref name="foot"/>, turned by
        /// <paramref name="turn"/>: a round pale heart, leaves wrapped tight over it, broad darker outer
        /// leaves cupping it from below with their tops flaring out.
        /// </summary>
        internal static void Cabbage(B.Model m, Vector3 foot, float r, int key, Quaternion turn)
        {
            Matrix4x4 was = m.Transform;
            Vector3 centre = Vector3.up * (r * 0.92f);
            m.Transform = was * Matrix4x4.TRS(foot, turn, Vector3.one);

            var radii = new Vector3(r, r * 0.92f, r);
            Vector3 On(float th, float ph, float s) =>
                centre + Vector3.Scale(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), -Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)), radii) * s;

            S.Ball(m, (p, n) => Heart, centre, radii * 0.98f, Quaternion.identity, 16, 10);

            // The heart's leaves wrapped over it, each a little proud of the one before.
            for (int k = 0; k < 4; k++)
            {
                float middle = (k * 137.5f + 40f + key * 11f) * Mathf.Deg2Rad;
                float half = 1f + 0.1f * B.Jitter(key * 5 + k);
                float lift = 1.02f + 0.03f * k;
                m.Surface((p, n) => Color.Lerp(Head, Rib, Mathf.Exp(-Mathf.Pow(Mathf.Repeat(Mathf.Atan2(p.z - centre.z, p.x - centre.x) - middle + Mathf.PI, Mathf.PI * 2f) - Mathf.PI, 2f) / 0.01f) * 0.6f),
                          8, 10, (u, v) =>
                          {
                              float x = u * 2f - 1f;
                              float th = Mathf.Lerp(0.25f * Mathf.PI, 0.9f * Mathf.PI, v);
                              float taper = Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Lerp(0.15f, 1f, v))));
                              return On(th, middle + x * half * taper, lift + 0.02f * x * x * v);
                          });
            }

            // Broad outer leaves cupping it from below, their tops flaring out in a rippled edge.
            for (int k = 0; k < 5; k++)
            {
                float middle = (k * 72f + 20f + key * 7f) * Mathf.Deg2Rad;
                float half = 0.86f;
                float top = (0.55f + 0.06f * B.Jitter(key * 3 + k)) * Mathf.PI;
                float lift = 1.1f + 0.016f * k;
                Color leaf = k % 2 == 0 ? Outer : Inner;
                m.Surface((p, n) => leaf, 10, 10, (u, v) =>
                {
                    float x = u * 2f - 1f;
                    float th = Mathf.Lerp(0.05f * Mathf.PI, top, v);
                    float taper = Mathf.Lerp(0.5f, 1f, Mathf.SmoothStep(0f, 1f, v / 0.5f));
                    float s = lift + 0.22f * Mathf.Pow(Mathf.SmoothStep(0f, 1f, (v - 0.45f) / 0.55f), 1.6f) + 0.06f * x * x * v
                              + 0.05f * Mathf.Sin(x * Mathf.PI * 3f + k) * v * v * v;
                    return On(th, middle + x * half * taper, s);
                });
                m.Surface((p, n) => leaf * 0.85f, 10, 10, (u, v) =>
                {
                    float x = (1f - u) * 2f - 1f;
                    float th = Mathf.Lerp(0.05f * Mathf.PI, top, v);
                    float taper = Mathf.Lerp(0.5f, 1f, Mathf.SmoothStep(0f, 1f, v / 0.5f));
                    float s = lift - 0.012f + 0.22f * Mathf.Pow(Mathf.SmoothStep(0f, 1f, (v - 0.45f) / 0.55f), 1.6f) + 0.06f * x * x * v
                              + 0.05f * Mathf.Sin(x * Mathf.PI * 3f + k) * v * v * v;
                    return On(th, middle + x * half * taper, s);
                });
            }

            m.Transform = was;
        }
    }
}
