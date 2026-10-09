using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.Defense;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using B = LastOrders.EditorTools.BuildingKit;
using FX = LastOrders.EditorTools.PlantingEffects;
using S = LastOrders.EditorTools.SoupKitchenModel;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The scrap turret the restaurant starts with, modelled in code in the game's smooth low-poly
    /// style, and its prefab.
    ///
    /// A gun bolted together from what was lying about, painted in the diner's own livery: a twin
    /// barrelled gun in teal enamel with the diner's red band, cream tin shield wings trimmed in teal,
    /// an ammo box feeding it from the side, a car battery and a motor at the back, a searchlight on
    /// the shield and a whip aerial - all on a turntable on a red fuel drum, behind a horseshoe of
    /// sandbags on a concrete pad. Painted in its vertices on LastOrders/Stylized like
    /// <see cref="BuildingKit"/> builds, with the searchlight's lens in the warm unlit glow so it reads
    /// at night.
    ///
    /// Four meshes, because four things move apart: the base stands still, the head turns to track
    /// (the post's aim pivot), the barrels kick back on each shot (<see cref="DefenseRecoil"/>), and
    /// the flash shows at the muzzle for a moment. The empty site - the pad, stakes and a pile of
    /// sandbags waiting - is a fifth, for a turret site that has not been bought yet.
    ///
    /// Re-running rebuilds the meshes into the assets already there and re-dresses the prefab's own
    /// objects by name, so the scene's turrets and their overrides keep pointing at them. Local +Z is
    /// the way the gun faces at rest.
    /// </summary>
    public static class ScrapTurretModel
    {
        public const string PrefabPath = DefensePrefabGenerator.Folder + "/Defense_ScrapTurret.prefab";

        const string Folder = "Assets/Game Assets/Content/LastOrders/Defense/" + EnvKit.Style + "/ScrapTurret";

        // --- Measurements (metres, model space; the gun faces +Z) ----------------------------------

        const float PadRadius = 1.05f, PadTop = 0.11f;

        /// <summary>The horseshoe of sandbags: the middle of the ring, a bag's size, and the gap left at the back.</summary>
        const float BagRing = 0.86f, BagLength = 0.46f, BagHeight = 0.17f, BagDepth = 0.3f, BagGapDegrees = 62f;

        const float DrumRadius = 0.27f, DrumTop = PadTop + 0.62f;

        /// <summary>The bearing on the drum; the head turns on its top face.</summary>
        const float PivotY = DrumTop + 0.05f;

        // Head space, from the pivot.
        const float TurntableRadius = 0.33f, TurntableTop = 0.05f;
        const float GunY = 0.36f, BodyFront = 0.33f, BodyBack = -0.29f, BodyHalfWidth = 0.15f, BodyHalfHeight = 0.12f;
        const float ShieldZ = 0.36f, ShieldBottom = 0.1f, ShieldTop = 0.62f, ShieldTilt = 12f;
        const float WingInner = 0.17f, WingOuter = 0.47f, ApronTop = 0.27f;

        // Barrel space, from the body's front face.
        const float BarrelSpacing = 0.065f, JacketLength = 0.42f, BarrelLength = 0.96f;

        /// <summary>Where the shots come out, just past the muzzle brakes.</summary>
        const float MuzzleZ = BarrelLength + 0.02f;

        // --- The palette -------------------------------------------------------------------------

        /// <summary>Mid grey: the pad faces straight up at the sun, and any lighter it reads white.</summary>
        static readonly Color Concrete = B.Hex("7C7B77");
        static readonly Color ConcreteEdge = B.Hex("6C6B67");
        static readonly Color[] Burlap = { B.Hex("A9946F"), B.Hex("9F8A66"), B.Hex("B19C78"), B.Hex("958260") };
        static readonly Color BurlapSeam = B.Hex("7E6D50");

        /// <summary>A fuel drum in muted red with a cream band, chipped back to grey steel in places.</summary>
        static readonly Color DrumRed = B.Hex("8A3A33");
        static readonly Color DrumBand = B.Hex("AFA898");
        static readonly Color BareSteel = B.Hex("8F969B");

        static readonly Color IronDark = B.Hex("2F3236");
        static readonly Color Iron = B.Iron;
        static readonly Color Steel = B.Hex("6E7479");
        static readonly Color SteelLight = B.Hex("8D9398");

        /// <summary>The diner's livery: teal enamel, a red band, cream panels. Mid cream: any lighter and its lit side clips.</summary>
        static readonly Color Teal = B.Hex("3D6762");
        static readonly Color TealLight = B.Hex("47756F");
        static readonly Color Cream = B.Hex("AFA898");
        static readonly Color Red = B.Hex("A33A32");

        static readonly Color Olive = B.Hex("56603F");
        static readonly Color OliveDark = B.Hex("4A5236");
        static readonly Color Stencil = B.Hex("B9B2A2");
        static readonly Color Brass = B.Hex("9C7F3E");
        static readonly Color Bakelite = B.Hex("2B2D30");
        static readonly Color Tip = B.Hex("B5302A");

        static readonly Color FlashColour = new Color(1f, 0.8f, 0.4f);

        [MenuItem("Last Orders/Defense/Rebuild Scrap Turret (Model & Prefab)", priority = 43)]
        static void Run()
        {
            DefenseDefinition turret = DefenseContentGenerator.GenerateScrapTurret();
            Debug.Log(Apply(turret, DefenseContentGenerator.LoadScrapTurretMotion()));
        }

        /// <summary>The prefab, built with its meshes if it does not exist yet; left as it is if it does.</summary>
        public static GameObject Ensure(DefenseDefinition definition, DefenseMotionProfile motion)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;

            Debug.Log(Apply(definition, motion));
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        /// <summary>Builds every mesh and dresses the prefab with them, creating it if it is missing. No dialogs.</summary>
        public static string Apply(DefenseDefinition definition, DefenseMotionProfile motion)
        {
            ContentGenerator.EnsureFolder(Folder);
            ContentGenerator.EnsureFolder(DefensePrefabGenerator.Folder);

            Mesh baseMesh = Save(Base(), "ScrapTurret_Base");
            Mesh head = Save(Head(), "ScrapTurret_Head");
            Mesh barrels = Save(Barrels(), "ScrapTurret_Barrels");
            Mesh flash = Save(Flash(), "ScrapTurret_Flash");
            Mesh site = Save(Site(), "ScrapTurret_Site");
            Mesh casing = Casing();

            Material flashMaterial = UnlitMaterial("ScrapTurret_Flash", FlashColour);

            bool created = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            GameObject root = created ? new GameObject("Defense_ScrapTurret") : PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                Dress(root, definition, motion, baseMesh, head, barrels, flash, site, casing, flashMaterial);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (created) Object.DestroyImmediate(root);
                else PrefabUtility.UnloadPrefabContents(root);
            }

            int vertices = baseMesh.vertexCount + head.vertexCount + barrels.vertexCount;
            return $"[Last Orders] Scrap turret {(created ? "built" : "rebuilt")}: {vertices} vertices in the turret, " +
                   $"{site.vertexCount} in the empty site; prefab at {PrefabPath}.";
        }

        // --- The prefab --------------------------------------------------------------------------

        static void Dress(GameObject root, DefenseDefinition definition, DefenseMotionProfile motion, Mesh baseMesh, Mesh headMesh,
                          Mesh barrelMesh, Mesh flashMesh, Mesh siteMesh, Mesh casingMesh, Material flashMaterial)
        {
            Transform turret = Child(root.transform, "Turret", Vector3.zero);
            Transform model = Child(turret, "Base", Vector3.zero);
            B.Show(model.gameObject, baseMesh);

            // The aim pivot: everything on the turntable turns with it.
            Transform head = Child(turret, "Head", new Vector3(0f, PivotY, 0f));
            B.Show(head.gameObject, headMesh);

            Transform barrels = Child(head, "Barrels", new Vector3(0f, GunY, BodyFront));
            B.Show(barrels.gameObject, barrelMesh);

            Transform muzzle = Child(barrels, "Muzzle", new Vector3(0f, 0f, MuzzleZ));
            Transform flash = Child(muzzle, "MuzzleFlash", Vector3.zero);

            // One flash per barrel, each rolled on its own axis by the recoil so no two shots look alike.
            var flashes = new Transform[2];
            for (int i = 0; i < flashes.Length; i++)
            {
                flashes[i] = Child(flash, i == 0 ? "Flash_L" : "Flash_R", new Vector3(i == 0 ? -BarrelSpacing : BarrelSpacing, 0f, 0f));
                B.Show(flashes[i].gameObject, flashMesh, new[] { flashMaterial });
                Quiet(flashes[i].gameObject);
            }

            flash.gameObject.SetActive(false);

            ParticleSystem casings = Casings(head, casingMesh);

            Transform site = Child(root.transform, "Site", Vector3.zero);
            B.Show(site.gameObject, siteMesh);

            LineRenderer tracer = DefensePrefabGenerator.BuildTracer(root.transform);

            var tap = root.GetComponent<BoxCollider>();
            if (tap == null) tap = root.AddComponent<BoxCollider>();

            var post = root.GetComponent<DefensePost>();
            if (post == null) post = root.AddComponent<DefensePost>();

            post.Configure(null, definition, "turret", false);
            post.ConfigureParts(turret.gameObject, site.gameObject, head, muzzle, null, tracer, flash.gameObject, tap);
            post.ConfigureMotion(motion, builtSize: new Vector3(2.2f, 1.6f, 2.2f), siteSize: new Vector3(2.2f, 0.5f, 2.2f),
                                 calloutAt: new Vector3(0f, 2.6f, 0f), buildButtonAt: new Vector3(0f, 1.1f, 0f));

            var recoil = root.GetComponent<DefenseRecoil>();
            if (recoil == null) recoil = root.AddComponent<DefenseRecoil>();
            recoil.Configure(post, new[] { barrels }, flashes, casings);

            // Built, as the turret a scene starts with is; the post sorts out which shows at runtime.
            turret.gameObject.SetActive(true);
            site.gameObject.SetActive(false);
        }

        /// <summary>The child called <paramref name="name"/>, made if it is missing, put at <paramref name="at"/> unturned.</summary>
        static Transform Child(Transform parent, string name, Vector3 at)
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

        static void Quiet(GameObject go)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Brass cases thrown out of the right of the gun, bouncing once off the ground and gone.</summary>
        static ParticleSystem Casings(Transform head, Mesh mesh)
        {
            Transform t = Child(head, "Casings", new Vector3(BodyHalfWidth + 0.02f, GunY + 0.02f, 0.05f));

            var ps = t.GetComponent<ParticleSystem>();
            if (ps == null) ps = t.gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.1f);
            // A little over life size: any smaller and they are lost from the game camera's distance.
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.085f);
            main.startRotation3D = true;
            main.startRotationX = FX.Turn();
            main.startRotationY = FX.Turn();
            main.startRotationZ = FX.Turn();
            main.startColor = Brass;
            main.gravityModifier = 1.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 30;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            // Out to the right and up, the way an ejector throws them.
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.01f;
            shape.rotation = new Vector3(-35f, 90f, 0f);

            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.bounce = 0.3f;
            collision.dampen = 0.6f;
            collision.lifetimeLoss = 0f;

            FX.Spin3D(ps, 400f, 900f);
            FX.Grow(ps, FX.Curve((0f, 1f), (0.75f, 1f), (1f, 0f)));

            var renderer = t.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            renderer.sharedMaterial = FX.SolidMaterial();
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        // --- The base: pad, sandbags, drum ------------------------------------------------------

        static B.Model Base()
        {
            // A shade darker down at the ground, on everything above the pad.
            var m = new B.Model { Shade = (p, n) => p.y < PadTop + 0.005f ? 1f : Mathf.Lerp(0.78f, 1f, S.Smooth((p.y - PadTop) / 0.45f)) };

            Pad(m);
            Sandbags(m);
            Drum(m);

            // A crate of spare belts at the back, an ammo can on it.
            B.Crate(m, new Vector3(-0.95f, 0f, -0.82f), new Vector3(0.42f, 0.3f, 0.32f), 24f, 7);
            AmmoCan(m, new Vector3(-0.95f, 0.33f, -0.82f), 18f);
            return m;
        }

        static void Pad(B.Model m)
        {
            m.Lathe((p, n) =>
            {
                // Mottled, a little darker on the bevel.
                float mottle = 1f + 0.035f * Mathf.Sin(p.x * 7.3f + p.z * 3.1f) * Mathf.Sin(p.z * 6.1f - p.x * 2.3f);
                return (n.y > 0.9f ? Concrete : ConcreteEdge) * mottle;
            }, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(PadRadius, 0f), new Vector2(PadRadius, 0f), new Vector2(PadRadius, PadTop - 0.04f),
                new Vector2(PadRadius - 0.04f, PadTop), new Vector2(PadRadius - 0.04f, PadTop), new Vector2(0f, PadTop),
            }, 36);
        }

        /// <summary>Two courses of bags round the front and sides, the upper one laid over the joints of the lower.</summary>
        static void Sandbags(B.Model m)
        {
            float arc = 360f - BagGapDegrees;
            float circumference = 2f * Mathf.PI * BagRing * arc / 360f;

            for (int course = 0; course < 2; course++)
            {
                int count = Mathf.RoundToInt(circumference / (BagLength * 0.97f)) - course;
                float y = PadTop + BagHeight * (0.5f + course * 0.92f);
                float ring = BagRing - course * 0.03f;

                for (int i = 0; i < count; i++)
                {
                    float t = (i + 0.5f + course * 0.5f) / (count + course);
                    float degrees = -arc * 0.5f + t * arc;
                    int key = course * 31 + i;
                    Bag(m, ring, degrees, y, key);
                }
            }
        }

        /// <summary>One bag, lying along the ring at <paramref name="degrees"/> from the front.</summary>
        static void Bag(B.Model m, float ring, float degrees, float y, int key)
        {
            float a = degrees * Mathf.Deg2Rad;
            var at = new Vector3(Mathf.Sin(a) * ring, y, Mathf.Cos(a) * ring);

            // Lying along the ring (a bag's length is its X), each a little askew, as bags thrown down by hand are.
            Quaternion lie = Quaternion.Euler(3f * B.Jitter(key), degrees + 6f * B.Jitter(key + 5), 4f * B.Jitter(key + 9));
            Color burlap = Burlap[B.Hash(key) % Burlap.Length];

            m.Box(burlap, at, new Vector3(BagLength, BagHeight, BagDepth), 0.075f, lie, 3);

            // The folded-over end, tied off: a darker band at one end.
            m.Box(BurlapSeam, at + lie * new Vector3(BagLength * 0.43f, 0f, 0f), new Vector3(0.05f, BagHeight * 0.86f, BagDepth * 0.86f),
                  0.02f, lie, 2);
        }

        /// <summary>The red fuel drum the gun stands on, its rolling hoops proud and a cream band round its middle.</summary>
        static void Drum(B.Model m)
        {
            float h = DrumTop - PadTop;

            // The band's edges are rings of their own, a hair either side of each line, so it comes out
            // crisp: paint only changes at a ring.
            const float BandFrom = 0.44f, BandTo = 0.58f, Edge = 0.008f;

            m.Lathe((p, n) =>
            {
                float y = (p.y - PadTop) / h;
                if (n.y > 0.9f) return IronDark;
                return y > BandFrom && y < BandTo ? DrumBand : DrumRed;
            }, new Vector3(0f, PadTop, 0f), new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(DrumRadius - 0.01f, 0f), new Vector2(DrumRadius, 0.012f),
                new Vector2(DrumRadius, h * 0.3f), new Vector2(DrumRadius + 0.016f, h * 0.32f), new Vector2(DrumRadius, h * 0.34f),
                new Vector2(DrumRadius, h * (BandFrom - Edge)), new Vector2(DrumRadius, h * (BandFrom + Edge)),
                new Vector2(DrumRadius, h * (BandTo - Edge)), new Vector2(DrumRadius, h * (BandTo + Edge)),
                new Vector2(DrumRadius, h * 0.66f), new Vector2(DrumRadius + 0.016f, h * 0.68f), new Vector2(DrumRadius, h * 0.7f),
                new Vector2(DrumRadius, h - 0.012f), new Vector2(DrumRadius - 0.012f, h), new Vector2(DrumRadius - 0.012f, h),
                new Vector2(0f, h),
            }, 24);

            // The bearing the head turns on.
            m.Lathe((p, n) => n.y > 0.9f ? Steel : IronDark, new Vector3(0f, DrumTop, 0f), new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.31f, 0f), new Vector2(0.31f, 0f), new Vector2(0.31f, PivotY - DrumTop),
                new Vector2(0.31f, PivotY - DrumTop), new Vector2(0f, PivotY - DrumTop),
            }, 24);
        }

        static void AmmoCan(B.Model m, Vector3 foot, float yaw)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            m.Box(Olive, foot + turn * new Vector3(0f, 0.09f, 0f), new Vector3(0.28f, 0.18f, 0.13f), 0.015f, turn, 2);
            m.Box(OliveDark, foot + turn * new Vector3(0f, 0.188f, 0f), new Vector3(0.29f, 0.025f, 0.14f), 0.008f, turn);
            m.Box(Stencil, foot + turn * new Vector3(0f, 0.1f, -0.066f), new Vector3(0.18f, 0.03f, 0.004f), 0.002f, turn);
            m.Box(IronDark, foot + turn * new Vector3(0f, 0.215f, 0f), new Vector3(0.1f, 0.02f, 0.025f), 0.008f, turn);
        }

        // --- The head: turntable, gun body, shield, ammo, battery -------------------------------

        static B.Model Head()
        {
            // A touch darker on everything facing down, under the gun and the shield.
            var m = new B.Model { Shade = (p, n) => n.y < -0.4f ? 0.82f : 1f };

            Turntable(m);
            Body(m);
            Shield(m);
            AmmoBox(m);
            Battery(m);
            Searchlight(m);

            // A whip aerial off the back, red-tipped: the thing runs itself.
            var foot = new Vector3(-0.1f, GunY + BodyHalfHeight, BodyBack + 0.07f);
            var top = foot + new Vector3(0f, 0.55f, -0.04f);
            m.Tube(IronDark, foot, top, 0.009f, 6);
            S.Ball(m, (p, n) => Tip, top, Vector3.one * 0.026f, Quaternion.identity, 10, 7);
            return m;
        }

        static void Turntable(B.Model m)
        {
            m.Lathe((p, n) => n.y > 0.9f ? Iron : IronDark, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(TurntableRadius, 0f), new Vector2(TurntableRadius, 0f),
                new Vector2(TurntableRadius, TurntableTop - 0.012f), new Vector2(TurntableRadius - 0.012f, TurntableTop),
                new Vector2(TurntableRadius - 0.012f, TurntableTop), new Vector2(0f, TurntableTop),
            }, 28);

            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f;
                S.Ball(m, (p, n) => Steel, new Vector3(Mathf.Cos(a) * 0.29f, TurntableTop, Mathf.Sin(a) * 0.29f),
                       new Vector3(0.018f, 0.012f, 0.018f), Quaternion.identity, 8, 5);
            }

            // The yoke: two cheeks up either side, an axle through the gun.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (BodyHalfWidth + 0.05f);
                m.Box(IronDark, new Vector3(x, TurntableTop + 0.17f, 0f), new Vector3(0.05f, 0.34f, 0.34f), 0.015f, 2);
                m.Tube(Steel, new Vector3(x - side * 0.03f, GunY - 0.02f, 0f), new Vector3(x + side * 0.045f, GunY - 0.02f, 0f), 0.045f, 14);
            }
        }

        /// <summary>The gun's body: teal enamel with a lighter top cover, the diner's red band round the front, a motor behind.</summary>
        static void Body(B.Model m)
        {
            float length = BodyFront - BodyBack;
            float middle = (BodyFront + BodyBack) * 0.5f;

            m.Box(Teal, new Vector3(0f, GunY, middle), new Vector3(BodyHalfWidth * 2f, BodyHalfHeight * 2f, length), 0.04f, 3);
            m.Box(TealLight, new Vector3(0f, GunY + BodyHalfHeight + 0.012f, middle - 0.03f),
                  new Vector3(BodyHalfWidth * 1.7f, 0.026f, length * 0.8f), 0.01f, 2);
            m.Box(Red, new Vector3(0f, GunY, BodyFront - 0.13f), new Vector3(BodyHalfWidth * 2f + 0.012f, BodyHalfHeight * 2f + 0.012f, 0.07f),
                  0.03f, 3);

            // The front plate the barrels come out of, and the slot on the right the cases fly from.
            m.Box(IronDark, new Vector3(0f, GunY, BodyFront + 0.008f), new Vector3(0.24f, 0.16f, 0.025f), 0.01f, 2);
            m.Box(B.Hex("1F1C1B"), new Vector3(BodyHalfWidth + 0.002f, GunY + 0.02f, 0.05f), new Vector3(0.012f, 0.05f, 0.13f), 0.004f);

            // The motor: a finned drum on the back.
            var motorFront = new Vector3(0f, GunY, BodyBack + 0.01f);
            var motorBack = new Vector3(0f, GunY, BodyBack - 0.13f);
            m.Tube(Iron, motorFront, motorBack, 0.09f, 18);
            for (int i = 0; i < 4; i++)
            {
                float z = BodyBack - 0.02f - i * 0.03f;
                m.Tube(IronDark, new Vector3(0f, GunY, z), new Vector3(0f, GunY, z - 0.012f), 0.1f, 18);
            }

            S.Disc(m, (p, n) => SteelLight, motorBack + Vector3.back * 0.001f, Vector3.back, 0.05f, 14);
        }

        /// <summary>
        /// Two wings of corrugated tin either side of the barrels and an apron under them, leaning
        /// back a little: painted cream, edged in teal, a red stripe across, chipped to the tin.
        /// </summary>
        static void Shield(B.Model m)
        {
            float tilt = ShieldTilt * Mathf.Deg2Rad;
            var down = new Vector3(0f, -Mathf.Cos(tilt), Mathf.Sin(tilt));
            float zAt(float y) => ShieldZAt(y);

            void Sheet(float x0, float x1, float yTop, float yBottom, int key)
            {
                float width = x1 - x0, length = (yTop - yBottom) / Mathf.Cos(tilt);
                var corner = new Vector3(x0, yTop, zAt(yTop));
                // Rows 1.5 cm apart, so the trim and the stripe come out crisp rather than smeared between distant rows.
                m.Corrugated(corner, Vector3.right, down, width, length, 0.075f, 0.022f, 0.012f, (u, v) => ShieldPaint(u, v, width, length, key),
                             rowStep: 0.015f);

                // Bolted to the frame at the corners.
                foreach (Vector2 c in new[] { new Vector2(0.04f, 0.04f), new Vector2(width - 0.04f, 0.04f), new Vector2(0.04f, length - 0.04f),
                                              new Vector2(width - 0.04f, length - 0.04f) })
                {
                    Vector3 at = corner + Vector3.right * c.x + down * c.y + Vector3.Cross(down, Vector3.right) * 0.026f;
                    S.Ball(m, (p, n) => IronDark, at, Vector3.one * 0.016f, Quaternion.identity, 8, 5);
                }
            }

            Sheet(-WingOuter, -WingInner, ShieldTop, ShieldBottom, 1);
            Sheet(WingInner, WingOuter, ShieldTop, ShieldBottom, 2);
            Sheet(-WingInner, WingInner, ApronTop, ShieldBottom, 3);

            // The frame behind: two struts from the cheeks out to the wings.
            for (int side = -1; side <= 1; side += 2)
            {
                var from = new Vector3(side * (BodyHalfWidth + 0.05f), GunY - 0.08f, 0.1f);
                var to = new Vector3(side * (WingInner + 0.12f), GunY - 0.04f, zAt(GunY - 0.04f) - 0.03f);
                m.Tube(IronDark, from, to, 0.02f, 8);
            }

            m.Tube(IronDark, new Vector3(-WingInner - 0.02f, ShieldBottom + 0.06f, zAt(ShieldBottom + 0.06f) - 0.03f),
                   new Vector3(WingInner + 0.02f, ShieldBottom + 0.06f, zAt(ShieldBottom + 0.06f) - 0.03f), 0.018f, 8);
        }

        /// <summary>How far forward the shield's face is at height <paramref name="y"/>: <see cref="ShieldZ"/> halfway up, leaning back above.</summary>
        static float ShieldZAt(float y) =>
            ShieldZ + ((ShieldTop + ShieldBottom) * 0.5f - y) * Mathf.Tan(ShieldTilt * Mathf.Deg2Rad);

        /// <summary>The shield's paint at <paramref name="u"/> across and <paramref name="v"/> down a sheet, in metres.</summary>
        static Color ShieldPaint(float u, float v, float width, float length, int key)
        {
            const float trim = 0.03f;
            float chip = Mathf.Sin(u * 23f + key * 1.7f) * Mathf.Sin(v * 17f - key * 2.3f) + 0.35f * Mathf.Sin((u + v) * 41f + key);
            if (chip > 1.12f) return BareSteel;

            if (u < trim || u > width - trim || v < trim || v > length - trim) return Teal;

            // The red stripe sits level with the band round the gun, on the wings; the apron is too short for it.
            float stripe = ShieldTop - GunY - 0.02f;
            if (length > 0.3f && Mathf.Abs(v - stripe) < 0.035f) return Red;

            return Cream * (1f + 0.03f * Mathf.Sin(u * 9f + v * 5f + key));
        }

        /// <summary>An olive ammo box on the left, the belt running from it into the gun.</summary>
        static void AmmoBox(B.Model m)
        {
            float x = -(BodyHalfWidth + 0.16f);
            var centre = new Vector3(x, GunY - 0.04f, 0.0f);
            m.Box(Olive, centre, new Vector3(0.15f, 0.2f, 0.28f), 0.018f, 2);
            m.Box(OliveDark, centre + new Vector3(0f, 0.11f, 0f), new Vector3(0.16f, 0.025f, 0.29f), 0.008f);
            m.Box(Stencil, centre + new Vector3(-0.076f, 0f, 0f), new Vector3(0.004f, 0.035f, 0.18f), 0.002f);
            m.Box(IronDark, centre + new Vector3(-0.078f, 0.06f, 0f), new Vector3(0.012f, 0.025f, 0.06f), 0.005f);

            // The belt: rounds side by side, curving up out of the box and down into the gun.
            for (int i = 0; i < 6; i++)
            {
                float t = i / 5f;
                float bx = Mathf.Lerp(x + 0.03f, -BodyHalfWidth + 0.005f, t);
                float by = GunY + 0.075f + Mathf.Sin(t * Mathf.PI) * 0.035f;
                var mid = new Vector3(bx, by, 0.06f);
                m.Tube(Brass, mid - Vector3.forward * 0.035f, mid + Vector3.forward * 0.025f, 0.012f, 8);
                m.Tube((p, n) => SteelLight, mid + Vector3.forward * 0.025f, mid + Vector3.forward * 0.045f, 0.012f, 0.004f, 8);
            }
        }

        /// <summary>A car battery under the back of the gun, a cable from it up to the motor.</summary>
        static void Battery(B.Model m)
        {
            // Low enough that its terminals clear the gun's belly above it.
            var centre = new Vector3(0f, TurntableTop + 0.065f, -0.22f);
            m.Box(Bakelite, centre, new Vector3(0.24f, 0.13f, 0.13f), 0.015f, 2);
            m.Box(B.Hex("3A3D41"), centre + new Vector3(0f, 0.07f, 0f), new Vector3(0.22f, 0.015f, 0.12f), 0.005f);

            var plus = centre + new Vector3(0.07f, 0.075f, 0f);
            var minus = centre + new Vector3(-0.07f, 0.075f, 0f);
            m.Tube(Tip, plus, plus + Vector3.up * 0.02f, 0.016f, 8);
            m.Tube(IronDark, minus, minus + Vector3.up * 0.02f, 0.016f, 8);

            // The cable: back out from under the gun, then up into the motor.
            Vector3 a = plus + Vector3.up * 0.02f, b = a + new Vector3(0.02f, 0f, -0.1f);
            Vector3 c = new Vector3(0.06f, GunY - 0.06f, BodyBack - 0.07f);
            m.Tube(Tip, a, b, 0.008f, 6);
            m.Tube(Tip, b, c, 0.008f, 6);
        }

        /// <summary>A searchlight on the right wing, its lens in the glow: the turret's eye at night.</summary>
        static void Searchlight(B.Model m)
        {
            // Sat on the top edge of the wing, which leans back.
            float x = (WingInner + WingOuter) * 0.5f, y = ShieldTop + 0.1f, z = ShieldZAt(ShieldTop) - 0.02f;
            var back = new Vector3(x, y, z - 0.08f);
            var front = new Vector3(x, y, z + 0.08f);

            m.Box(IronDark, new Vector3(x, ShieldTop + 0.02f, z), new Vector3(0.06f, 0.08f, 0.05f), 0.01f);
            m.Tube(Iron, back, front, 0.085f, 18);
            m.Tube(IronDark, front, front + Vector3.forward * 0.02f, 0.095f, 18);
            S.Disc(m, (p, n) => IronDark, back - Vector3.forward * 0.001f, Vector3.back, 0.085f, 16);

            // The lens: a shallow dome turned about Y, laid over to face forward.
            Matrix4x4 was = m.Transform;
            m.Transform = Matrix4x4.TRS(front + Vector3.forward * 0.012f, Quaternion.FromToRotation(Vector3.up, Vector3.forward), Vector3.one);

            var lens = new SmoothMesh(1);
            lens.Lathe(Vector3.zero, new List<Vector2> { new Vector2(0.075f, 0f), new Vector2(0.06f, 0.012f), new Vector2(0f, 0.016f) },
                       new[] { 0 }, 18);
            m.Add(lens, Color.white, B.Model.Glow);
            m.Transform = was;
        }

        // --- The barrels and the flash -----------------------------------------------------------

        /// <summary>Twin barrels in their cooling jackets, a clamp across them, muzzle brakes on the ends. From the body's face along +Z.</summary>
        static B.Model Barrels()
        {
            var m = new B.Model();

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * BarrelSpacing;

                m.Tube(IronDark, new Vector3(x, 0f, -0.02f), new Vector3(x, 0f, JacketLength), 0.05f, 14);
                for (int i = 0; i < 4; i++)
                {
                    float z = 0.07f + i * 0.09f;
                    m.Tube(Iron, new Vector3(x, 0f, z), new Vector3(x, 0f, z + 0.025f), 0.057f, 14);
                }

                m.Tube(Steel, new Vector3(x, 0f, JacketLength), new Vector3(x, 0f, BarrelLength - 0.08f), 0.028f, 12);
                m.Tube(IronDark, new Vector3(x, 0f, BarrelLength - 0.1f), new Vector3(x, 0f, BarrelLength), 0.042f, 12);
                S.Disc(m, (p, n) => B.Hex("141414"), new Vector3(x, 0f, BarrelLength + 0.001f), Vector3.forward, 0.02f, 10);
            }

            m.Box(IronDark, new Vector3(0f, 0f, 0.62f), new Vector3(BarrelSpacing * 2f + 0.08f, 0.07f, 0.06f), 0.015f, 2);
            return m;
        }

        /// <summary>One barrel's flash, from the muzzle along +Z: a long tongue out front and four short ones round it.</summary>
        static B.Model Flash()
        {
            var m = new B.Model();
            Spike(m, Vector3.forward, 0.34f, 0.07f);

            for (int i = 0; i < 4; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 0.5f;
                var out_ = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Spike(m, (out_ + Vector3.forward * 0.6f).normalized, 0.15f, 0.035f);
            }

            S.Ball(m, (p, n) => Color.white, Vector3.forward * 0.02f, Vector3.one * 0.05f, Quaternion.identity, 10, 7);
            return m;
        }

        /// <summary>A tongue of flame from the origin along <paramref name="axis"/>: round at the root, drawn to a point.</summary>
        static void Spike(B.Model m, Vector3 axis, float length, float radius)
        {
            Quaternion turn = Quaternion.FromToRotation(Vector3.up, axis);
            m.Surface((p, n) => Color.white, 10, 6, (u, v) =>
            {
                float a = u * Mathf.PI * 2f;

                // Floored at zero: sin(pi) comes out a hair below it at the tip, and a fractional power of that is NaN.
                float width = radius * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Lerp(0.12f, 1f, v))), 0.7f) * (1f - 0.6f * v);
                return turn * new Vector3(Mathf.Cos(a) * width, v * length, Mathf.Sin(a) * width);
            }, true);
        }

        // --- The empty site ------------------------------------------------------------------------

        /// <summary>The pad poured, staked out with twine, a heap of filled bags and a crate waiting for the gun.</summary>
        static B.Model Site()
        {
            var m = new B.Model { Shade = (p, n) => p.y < PadTop + 0.005f ? 1f : Mathf.Lerp(0.8f, 1f, S.Smooth((p.y - PadTop) / 0.4f)) };

            Pad(m);

            // Stakes round the edge, twine run between them.
            const int stakes = 6;
            var tops = new Vector3[stakes];
            for (int i = 0; i < stakes; i++)
            {
                float a = (i + 0.5f) / stakes * Mathf.PI * 2f;
                var foot = new Vector3(Mathf.Cos(a) * (PadRadius + 0.08f), 0f, Mathf.Sin(a) * (PadRadius + 0.08f));
                tops[i] = foot + Vector3.up * 0.42f;
                m.Box(B.WalnutDark, foot + Vector3.up * 0.2f, new Vector3(0.05f, 0.42f, 0.05f), 0.012f);
            }

            for (int i = 0; i < stakes; i++)
                m.Tube(B.Twine, tops[i] - Vector3.up * 0.05f, tops[(i + 1) % stakes] - Vector3.up * 0.05f, 0.006f, 5);

            // A heap of bags: three down, two on them.
            Vector3[] heap = { new Vector3(-0.35f, 0f, 0.15f), new Vector3(0.12f, 0f, 0.2f), new Vector3(-0.1f, 0f, -0.25f) };
            for (int i = 0; i < heap.Length; i++)
                m.Box(Burlap[i % Burlap.Length], heap[i] + Vector3.up * (PadTop + BagHeight * 0.5f), new Vector3(BagLength, BagHeight, BagDepth),
                      0.075f, Quaternion.Euler(0f, 20f + i * 47f, 2f * B.Jitter(i)), 3);

            m.Box(Burlap[3], new Vector3(-0.12f, PadTop + BagHeight * 1.45f, 0.17f), new Vector3(BagLength, BagHeight, BagDepth), 0.075f,
                  Quaternion.Euler(4f, 75f, -3f), 3);
            m.Box(Burlap[1], new Vector3(-0.2f, PadTop + BagHeight * 1.45f, -0.12f), new Vector3(BagLength, BagHeight, BagDepth), 0.075f,
                  Quaternion.Euler(-3f, 12f, 4f), 3);

            B.Crate(m, new Vector3(0.5f, PadTop, -0.35f), new Vector3(0.42f, 0.3f, 0.32f), -18f, 11);
            AmmoCan(m, new Vector3(0.5f, PadTop + 0.33f, -0.35f), -30f);
            return m;
        }

        // --- Assets ---------------------------------------------------------------------------------

        static Mesh Save(B.Model model, string name) => model.Save($"{Folder}/{name}.asset", name);

        /// <summary>A spent case about a metre long, for a particle to scale down: a tube with a rim at its base.</summary>
        static Mesh Casing()
        {
            var shape = new SmoothMesh(1);
            shape.Tube(0, new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), 0.2f, 0.17f, 10);
            shape.Tube(0, new Vector3(0f, -0.5f, 0f), new Vector3(0f, -0.42f, 0f), 0.22f, 0.22f, 10);
            return shape.BuildAsset($"{Folder}/ScrapTurret_Casing.asset", "ScrapTurret_Casing", out List<int> _);
        }

        /// <summary>Unlit in one colour, so the flash is as bright at night as by day.</summary>
        static Material UnlitMaterial(string name, Color colour)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new System.InvalidOperationException("Shader Universal Render Pipeline/Unlit is missing.");

            string path = $"{Folder}/{name}.mat";
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
    }
}
