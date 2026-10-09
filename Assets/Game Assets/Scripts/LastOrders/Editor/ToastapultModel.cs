using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.Defense;
using LastOrders.Effects;
using UnityEditor;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using S = LastOrders.EditorTools.SoupKitchenModel;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The Toastapult: an old diner toaster left on a hot plate too long, until it came alive. It
    /// stands on the plate on a pallet like the kitchen stations' duckboards, a crate of loaves
    /// beside it - its coil glowing round its feet, stubby chrome legs underneath, the flex trailing
    /// off to a plug - and its front has become a face: two amber lamp eyes in chrome bezels with
    /// slit pupils, the chrome trims bent down over them into a glower, the crumb tray opened into a
    /// grille mouth with the elements glowing in it. The rest is what the heat did to it: a third lamp
    /// eye up on a coiled spring, vents burnt through its sides, an exhaust pipe that never stops
    /// smoking, a riveted scrap patch, a weld seam, the enamel chipped and sooted black round the
    /// slots. It turns to face what is coming and lobs slices of flaming toast at it, squashing down
    /// to fire and springing back up.
    ///
    /// Built to sit with the compound's other things rather than as a cartoon: the diner's muted
    /// enamel red with a cream band, worn the way the turret and the sheds are worn, in the game's
    /// smooth low-poly style, painted in its vertices on LastOrders/Stylized like
    /// <see cref="BuildingKit"/> builds. Everything hot - the coil, the elements, the embers on the
    /// toast - is an unlit orange and the eyes the lanterns' warm glow, so they read by day and by
    /// night. No part of it is a plant. And its prefab.
    ///
    /// Separate meshes for whatever moves on its own: the stand stays put; the toaster turns (the
    /// post's aim pivot) and squashes and stretches (<see cref="LivingDefenseAnimation"/>); the eyes
    /// blink, the stalk sways, the loaded toast disappears and comes back, the lever pumps; the slice
    /// in flight is its own, with its fire-and-smoke trail on it (<see cref="DefenseProjectiles"/>).
    /// The pop out of the slots and the burst where it lands are pooled effects
    /// (<see cref="ToastapultEffects"/>). The empty site is the hot plate, cold, with a loaf on it.
    ///
    /// Re-running rebuilds the meshes into the assets already there and re-dresses the prefab's own
    /// objects by name. Local +Z is the way its face looks at rest.
    /// </summary>
    public static class ToastapultModel
    {
        public const string PrefabPath = DefensePrefabGenerator.Folder + "/Defense_Toastapult.prefab";

        const string Folder = "Assets/Game Assets/Content/LastOrders/Defense/" + EnvKit.Style + "/Toastapult";

        /// <summary>What an earlier build had that this one does not, removed from the prefab when it is re-dressed.</summary>
        static readonly string[] Retired = { "Plant", "CrumbSplat" };

        // --- Measurements (metres; its face looks along +Z) -----------------------------------------

        /// <summary>How much bigger than a kitchen toaster it has grown. The whole appliance is built life size and scaled by this.</summary>
        const float Grown = 1.15f;

        /// <summary>The pallet it all stands on, as the kitchen stations stand on their duckboards.</summary>
        const float PalletHalfX = 0.74f, PalletHalfZ = 0.56f, PalletTop = 0.11f;

        /// <summary>The hot plate: the underside on its legs, the top it stands on - which is the aim pivot.</summary>
        const float PlateRadius = 0.44f, PlateBottom = PalletTop + 0.15f, PlateTop = PalletTop + 0.25f;

        // Toaster space: from the plate under its feet.
        const float BodyBottom = 0.1f, BodyTop = 0.7f, BodyHalfWidth = 0.43f, BodyHalfDepth = 0.27f, BodyRound = 0.12f;
        const float SlotZ = 0.07f, SlotHalfLength = 0.25f;

        /// <summary>The lamp eyes: chrome bezels set in the front panel, the big one on its right.</summary>
        const float EyeY = 0.47f, EyeX = 0.17f, EyeRadius = 0.085f, SmallEye = 0.85f;
        const float EyeZ = BodyHalfDepth;

        /// <summary>The grille mouth across the bottom of the front, where the crumb tray was.</summary>
        const float MouthY = 0.25f, MouthHalfWidth = 0.23f, MouthHalfHeight = 0.05f;
        const float LeverX = BodyHalfWidth + 0.01f, LeverY = 0.56f, LeverZ = 0.1f, LeverTravel = 0.15f;

        /// <summary>The stalk eye's spring: where it grows from on the top back corner, and how tall it stands.</summary>
        static readonly Vector3 StalkRoot = new Vector3(-0.27f, BodyTop - 0.02f, -0.13f);
        const float StalkHeight = 0.3f;

        /// <summary>The exhaust pipe, out of the other back corner and up.</summary>
        static readonly Vector3[] Pipe =
        {
            new Vector3(0.27f, BodyTop - 0.03f, -0.15f), new Vector3(0.29f, BodyTop + 0.09f, -0.17f), new Vector3(0.33f, BodyTop + 0.19f, -0.22f),
        };

        /// <summary>Where the toast leaves from: just over the slots.</summary>
        const float MuzzleY = BodyTop + 0.12f;

        // --- The palette -------------------------------------------------------------------------

        /// <summary>
        /// Enamel in the diner's muted red - the scrap turret's fuel drum - with a cream band, as the
        /// compound's painted things are: mid-tone, the colour in the object, nothing brighter than the
        /// buildings round it.
        /// </summary>
        static readonly Color EnamelRed = B.Hex("8E3B33");
        static readonly Color EnamelRedDark = B.Hex("74302A");
        static readonly Color CreamBand = B.Hex("AFA898");
        /// <summary>The steel under the enamel, dull: lighter than this and a chip reads as a white fleck.</summary>
        static readonly Color BareMetal = B.Hex("6A6F73");
        static readonly Color Soot = B.Hex("2A2524");
        static readonly Color Chrome = B.Hex("A5ABB0");
        static readonly Color ChromeDark = B.Hex("7E858A");
        static readonly Color SlotDark = B.Hex("1F1C1B");
        static readonly Color Bakelite = B.Hex("2B2D30");
        static readonly Color Enamel = B.Hex("2E3033");
        static readonly Color Rubber = B.Hex("232323");
        static readonly Color CoilCold = B.Hex("4A4C4F");
        static readonly Color Patch = B.Hex("8F969B");
        static readonly Color Pupil = B.Hex("1A1A1C");
        static readonly Color[] Bread = { B.Hex("B58A55"), B.Hex("A97E4A"), B.Hex("BC9460") };

        static readonly Color Toast = B.Hex("C69A5B");
        static readonly Color ToastDark = B.Hex("B0834A");
        static readonly Color Crust = B.Hex("7E5430");
        static readonly Color Charred = B.Hex("3E2A1E");
        static readonly Color Scorched = B.Hex("8A5E34");

        /// <summary>Everything hot: unlit, so it glows the same by day and by night.</summary>
        static readonly Color Hot = new Color(1f, 0.56f, 0.26f);

        [MenuItem("Last Orders/Defense/Rebuild Toastapult (Model & Prefab)", priority = 45)]
        static void Run()
        {
            DefenseDefinition toaster = DefenseContentGenerator.GenerateToastapult();
            Debug.Log(Apply(toaster, DefenseContentGenerator.LoadToastapultMotion()));
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
                ["Stand"] = Save(Stand(hot: true), "Toastapult_Stand"),
                ["Body"] = Save(Body(), "Toastapult_Body"),
                ["Eye"] = Save(Lamp(), "Toastapult_Eye"),
                ["Stalk"] = Save(Stalk(), "Toastapult_Stalk"),
                ["Loaded"] = Save(Loaded(), "Toastapult_Loaded"),
                ["Slice"] = Save(FlyingSlice(), "Toastapult_Slice"),
                ["Lever"] = Save(Lever(), "Toastapult_Lever"),
                ["Site"] = Save(Site(), "Toastapult_Site"),
            };

            Mesh crumb = Crumb();
            Material hot = HotMaterial();
            PooledEffect launch = ToastapultEffects.Launch(crumb);
            PooledEffect impact = ToastapultEffects.Impact(crumb);

            bool created = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            GameObject root = created ? new GameObject("Defense_Toastapult") : PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                Dress(root, definition, motion, meshes, hot, launch, impact);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (created) Object.DestroyImmediate(root);
                else PrefabUtility.UnloadPrefabContents(root);
            }

            int vertices = 0;
            foreach (KeyValuePair<string, Mesh> pair in meshes)
                if (pair.Key != "Site" && pair.Key != "Slice") vertices += pair.Value.vertexCount;

            return $"[Last Orders] Toastapult {(created ? "built" : "rebuilt")}: {vertices} vertices in the toaster and its stand, " +
                   $"{meshes["Site"].vertexCount} in the empty site; prefab at {PrefabPath}; effects {ToastapultEffects.LaunchPath}, {ToastapultEffects.ImpactPath}.";
        }

        // --- The prefab --------------------------------------------------------------------------

        static void Dress(GameObject root, DefenseDefinition definition, DefenseMotionProfile motion, Dictionary<string, Mesh> meshes, Material hot,
                          PooledEffect launch, PooledEffect impact)
        {
            foreach (string name in Retired)
            {
                Transform old = root.transform.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            // The crumbs out of the slots were the recoil add-on's "spent cases"; the launch effect does that now.
            var recoil = root.GetComponent<DefenseRecoil>();
            if (recoil != null) Object.DestroyImmediate(recoil);

            Material painted = B.PaintedMaterial();
            Material[] withHeat = { painted, hot };
            Material[] withLamp = { painted, B.GlowMaterial() };

            Transform appliance = Child(root.transform, "Appliance", Vector3.zero);
            appliance.localScale = Vector3.one * Grown;
            Show(Child(appliance, "Stand", Vector3.zero), meshes["Stand"], withHeat);

            // The aim pivot, on the plate: the whole toaster turns to face what it throws at.
            Transform head = Child(appliance, "Head", new Vector3(0f, PlateTop, 0f));

            // Squashes and stretches from its feet; everything on the toaster sits under it.
            Transform body = Child(head, "Body", Vector3.zero);
            Show(body, meshes["Body"], withHeat);

            // The lamp eyes, the big one on its right, set a little askew: not quite a matched pair any more.
            Transform eyeBig = Child(body, "Eye_Big", new Vector3(EyeX, EyeY, EyeZ));
            eyeBig.localRotation = Quaternion.Euler(0f, 0f, -8f);
            Show(eyeBig, meshes["Eye"], withLamp);

            Transform eyeSmall = Child(body, "Eye_Small", new Vector3(-EyeX, EyeY + 0.012f, EyeZ));
            eyeSmall.localRotation = Quaternion.Euler(0f, 0f, 10f);
            eyeSmall.localScale = Vector3.one * SmallEye;
            Show(eyeSmall, meshes["Eye"], withLamp);

            // The third eye, a lamp up on its spring, looking out over the top.
            Transform stalk = Child(body, "Stalk", StalkRoot);
            Show(stalk, meshes["Stalk"]);
            Transform stalkEye = Child(stalk, "Stalk_Eye", new Vector3(0f, StalkHeight + 0.045f, 0.01f));
            stalkEye.localRotation = Quaternion.Euler(10f, 12f, 0f);
            stalkEye.localScale = Vector3.one * 0.6f;
            Show(stalkEye, meshes["Eye"], withLamp);

            Transform loaded = Child(body, "Loaded", Vector3.zero);
            Show(loaded, meshes["Loaded"]);

            Transform lever = Child(body, "Lever", new Vector3(LeverX, LeverY, LeverZ));
            Show(lever, meshes["Lever"]);

            ToastapultEffects.Exhaust(body, Pipe[Pipe.Length - 1] + Vector3.up * 0.04f);

            Transform muzzle = Child(head, "Muzzle", new Vector3(0f, MuzzleY, 0f));

            Transform slice = Child(root.transform, "Projectile_Toast", Vector3.zero);
            Show(slice, meshes["Slice"], withHeat);
            ToastapultEffects.Trail(slice);
            slice.gameObject.SetActive(false);

            Transform site = Child(root.transform, "Site", Vector3.zero);
            site.localScale = Vector3.one * Grown;
            Show(site, meshes["Site"]);

            var tap = root.GetComponent<BoxCollider>();
            if (tap == null) tap = root.AddComponent<BoxCollider>();

            var post = root.GetComponent<DefensePost>();
            if (post == null) post = root.AddComponent<DefensePost>();

            post.Configure(null, definition, "toastapult", false);
            post.ConfigureParts(appliance.gameObject, site.gameObject, head, muzzle, null, null, null, tap);
            post.ConfigureMotion(motion, builtSize: new Vector3(1.5f, 1.8f, 1.3f), siteSize: new Vector3(1.5f, 0.7f, 1.3f),
                                 calloutAt: new Vector3(0f, 2.5f, 0f), buildButtonAt: new Vector3(0f, 1.1f, 0f));
            post.ConfigureMuzzleEffect(launch);

            var thrown = root.GetComponent<DefenseProjectiles>();
            if (thrown == null) thrown = root.AddComponent<DefenseProjectiles>();
            thrown.Configure(post, slice.gameObject, impact);

            var living = root.GetComponent<LivingDefenseAnimation>();
            if (living == null) living = root.AddComponent<LivingDefenseAnimation>();
            living.Configure(post, body, new[] { eyeBig, eyeSmall, stalkEye }, loaded, lever, LeverTravel, new[] { stalk });

            appliance.gameObject.SetActive(true);
            site.gameObject.SetActive(false);
        }

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

        /// <summary>The mesh on <paramref name="t"/>, in its painted material - and the hot one for its glowing submesh, when it has one.</summary>
        static void Show(Transform t, Mesh mesh, Material[] withHeat = null)
        {
            Material[] materials = mesh.subMeshCount > 1 && withHeat != null ? withHeat : new[] { B.PaintedMaterial() };
            B.Show(t.gameObject, mesh, materials);
        }

        // --- The hot plate ------------------------------------------------------------------------------

        /// <summary>
        /// The electric hot plate it stands on: black enamel with a chrome band, the coil on top
        /// (glowing, or cold on an empty site), a knob and a pilot light on the front, four stubby chrome
        /// legs, and the flex out of the back trailing off across the ground to a plug.
        /// </summary>
        static B.Model Stand(bool hot)
        {
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.8f, 1f, S.Smooth(p.y / 0.3f)) };

            m.Lathe((p, n) => Enamel, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, PlateBottom), new Vector2(PlateRadius - 0.03f, PlateBottom), new Vector2(PlateRadius, PlateBottom + 0.03f),
                new Vector2(PlateRadius, PlateTop - 0.02f), new Vector2(PlateRadius - 0.02f, PlateTop), new Vector2(PlateRadius - 0.02f, PlateTop),
                new Vector2(0f, PlateTop),
            }, 32);

            m.Lathe((p, n) => Chrome, Vector3.zero, new List<Vector2>
            {
                new Vector2(PlateRadius + 0.004f, PlateBottom + 0.035f), new Vector2(PlateRadius + 0.012f, PlateBottom + 0.045f),
                new Vector2(PlateRadius + 0.012f, PlateBottom + 0.06f), new Vector2(PlateRadius + 0.004f, PlateBottom + 0.07f),
            }, 32);

            // The coil: a spiral of element on the top, round under its feet.
            const int Steps = 90;
            const float Turns = 2.6f, Inner = 0.09f, Outer = PlateRadius - 0.05f;
            for (int i = 0; i < Steps; i++)
            {
                Vector3 a = Spiral(i / (float)Steps), b = Spiral((i + 1) / (float)Steps);
                if (hot) m.Tube((p, n) => Color.white, a, b, 0.013f, 0.013f, 8, B.Model.Glow);
                else m.Tube(CoilCold, a, b, 0.013f, 8);
            }

            Vector3 Spiral(float s)
            {
                float angle = s * Turns * Mathf.PI * 2f, radius = Mathf.Lerp(Inner, Outer, s);
                return new Vector3(Mathf.Cos(angle) * radius, PlateTop + 0.012f, Mathf.Sin(angle) * radius);
            }

            // The knob and the pilot light on the front.
            var front = new Vector3(0f, PlateBottom + 0.05f, PlateRadius);
            S.Disc(m, (p, n) => Chrome, front + Vector3.forward * 0.004f, Vector3.forward, 0.045f, 18);
            m.Tube(Bakelite, front + Vector3.forward * 0.005f, front + Vector3.forward * 0.045f, 0.03f, 14);
            m.Box(Chrome, front + new Vector3(0f, 0f, 0.047f), new Vector3(0.008f, 0.04f, 0.006f), 0.003f);
            var pilot = new Vector3(0.11f, PlateBottom + 0.05f, PlateRadius - 0.01f);
            Glow(m, hot, pilot, 0.017f);

            // Four stubby legs, splayed a little, on rubber feet on the pallet.
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                var out_ = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 top = out_ * 0.3f + Vector3.up * (PlateBottom + 0.01f), foot = out_ * 0.34f + Vector3.up * (PalletTop + 0.035f);
                m.Tube((p, n) => Chrome, top, foot, 0.03f, 0.026f, 12);
                S.Ball(m, (p, n) => Rubber, foot + Vector3.down * 0.005f, new Vector3(0.045f, 0.035f, 0.045f), Quaternion.identity, 12, 8);
            }

            Pallet(m);

            // A crate of loaves at the back corner: what it fires.
            var crate = new Vector3(-0.56f, PalletTop, -0.34f);
            S.OpenCrate(m, crate, new Vector3(0.3f, 0.22f, 0.26f), 14f, 5);
            for (int k = 0; k < 3; k++)
            {
                Quaternion lie = Quaternion.Euler(4f * B.Jitter(k + 70), 14f + 8f * B.Jitter(k + 71), 0f);
                Loaf(m, crate + lie * new Vector3(0f, 0.16f + 0.02f * k, (k - 1) * 0.075f), lie, 0.24f, Bread[k % Bread.Length]);
            }

            // The flex out of the back, down onto the pallet and off its edge across the ground to the plug.
            var flex = new[]
            {
                new Vector3(0f, PlateBottom + 0.04f, -PlateRadius + 0.01f), new Vector3(0.01f, PalletTop + 0.06f, -0.49f),
                new Vector3(0.05f, PalletTop + 0.016f, -0.53f), new Vector3(0.1f, PalletTop + 0.012f, -PalletHalfZ + 0.01f),
                new Vector3(0.15f, 0.05f, -PalletHalfZ - 0.04f), new Vector3(0.22f, 0.02f, -0.7f), new Vector3(0.3f, 0.02f, -0.82f),
            };
            Strand(m, flex, 0.016f, 0.016f, Bakelite);
            Plug(m, flex[flex.Length - 1], (flex[flex.Length - 1] - flex[flex.Length - 2]).normalized);

            return m;
        }

        /// <summary>A pallet of weathered boards on three bearers, like the duckboards the kitchen stations stand on.</summary>
        static void Pallet(B.Model m)
        {
            for (int k = -1; k <= 1; k++)
                m.Box(B.WalnutDark, new Vector3(0f, 0.035f, k * (PalletHalfZ - 0.07f)), new Vector3(PalletHalfX * 2f - 0.04f, 0.07f, 0.09f), 0.012f);

            const int Boards = 8;
            float pitch = PalletHalfX * 2f / Boards;
            for (int i = 0; i < Boards; i++)
            {
                float x = -PalletHalfX + pitch * (i + 0.5f);
                Color c = B.Plank(i + 90) * (1f + 0.04f * B.Jitter(i + 95));
                m.Box(c, new Vector3(x, PalletTop - 0.017f, 0.006f * B.Jitter(i + 99)), new Vector3(pitch - 0.024f, 0.034f, PalletHalfZ * 2f), 0.01f,
                      Quaternion.Euler(0f, 0.8f * B.Jitter(i + 97), 0f));
            }
        }

        /// <summary>A loaf lying along <paramref name="lie"/>: a rounded block of bread with a domed, darker top crust.</summary>
        static void Loaf(B.Model m, Vector3 centre, Quaternion lie, float length, Color colour)
        {
            m.Box(colour, centre, new Vector3(length, 0.09f, 0.1f), 0.035f, lie, 3);
            S.Ball(m, (p, n) => colour * 0.86f, centre + lie * new Vector3(0f, 0.04f, 0f), new Vector3(length * 0.46f, 0.035f, 0.05f), lie, 14, 8);
        }

        /// <summary>A plug on the end of the flex, its prongs pointing on along <paramref name="along"/>.</summary>
        static void Plug(B.Model m, Vector3 at, Vector3 along)
        {
            Quaternion turn = Quaternion.LookRotation(new Vector3(along.x, 0f, along.z));
            Vector3 centre = at + turn * new Vector3(0f, 0.012f, 0.05f);
            m.Box(Bakelite, centre, new Vector3(0.07f, 0.05f, 0.09f), 0.015f, turn, 2);
            for (int side = -1; side <= 1; side += 2)
                m.Box(Chrome, centre + turn * new Vector3(side * 0.016f, 0f, 0.07f), new Vector3(0.008f, 0.02f, 0.05f), 0.002f, turn);
        }

        // --- The toaster ---------------------------------------------------------------------------

        static B.Model Body()
        {
            // A touch darker toward its feet and on anything facing down.
            var m = new B.Model { Shade = (p, n) => (n.y < -0.4f ? 0.82f : 1f) * Mathf.Lerp(0.86f, 1f, S.Smooth((p.y - BodyBottom) / 0.25f)) };

            float bodyMid = (BodyBottom + BodyTop) * 0.5f;
            var bodySize = new Vector3(BodyHalfWidth * 2f, BodyTop - BodyBottom, BodyHalfDepth * 2f);

            // The enamel shell. Its vertices are all round its rounded edges - rows across the flat faces
            // pick up the lighting square by square, in blotches - so the wear is painted on the edges,
            // which is where enamel wears.
            var shell = new SmoothMesh(1);
            shell.RoundedBox(0, new Vector3(0f, bodyMid, 0f), bodySize, BodyRound, Quaternion.identity, 4);
            m.Add(shell, ShellPaint);

            // Chrome: a base plate and bun feet under it, a rim round each slot.
            m.Box(Chrome, new Vector3(0f, 0.07f, 0f), new Vector3(0.8f, 0.06f, 0.46f), 0.028f, 2);
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -0.33f : 0.33f, z = (i & 2) == 0 ? -0.16f : 0.16f;
                S.Ball(m, (p, n) => ChromeDark, new Vector3(x, 0.03f, z), new Vector3(0.05f, 0.035f, 0.05f), Quaternion.identity, 10, 6);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * SlotZ;
                m.Box(Chrome, new Vector3(0f, BodyTop - 0.004f, z), new Vector3(SlotHalfLength * 2f + 0.05f, 0.02f, 0.085f), 0.008f, 2);
                m.Box(SlotDark, new Vector3(0f, BodyTop + 0.004f, z), new Vector3(SlotHalfLength * 2f, 0.012f, 0.052f), 0.005f);

                // The elements down in the slot, glowing.
                m.Box(Color.white, new Vector3(0f, BodyTop + 0.0105f, z), new Vector3(SlotHalfLength * 2f - 0.04f, 0.003f, 0.026f), 0.001f,
                      Quaternion.identity, 1, B.Model.Glow);
            }

            Face(m);
            Mutations(m);

            // The lever's channel on its right, the browning dial on its left.
            m.Box(SlotDark, new Vector3(BodyHalfWidth + 0.002f, LeverY - LeverTravel * 0.5f, LeverZ), new Vector3(0.014f, LeverTravel + 0.06f, 0.036f), 0.006f);
            S.Disc(m, (p, n) => Chrome, new Vector3(-BodyHalfWidth - 0.003f, 0.36f, 0.06f), Vector3.left, 0.065f, 18);
            m.Tube(Bakelite, new Vector3(-BodyHalfWidth - 0.004f, 0.36f, 0.06f), new Vector3(-BodyHalfWidth - 0.04f, 0.36f, 0.06f), 0.03f, 12);
            m.Box(Chrome, new Vector3(-BodyHalfWidth - 0.042f, 0.36f, 0.06f), new Vector3(0.006f, 0.05f, 0.012f), 0.003f);

            return m;
        }

        /// <summary>
        /// The enamel, worn the way the compound's painted things are worn: a cream band low round it,
        /// chipped through to bare metal along its edges where it has been knocked about, grimy
        /// underneath, and sooted along the top edges where the heat comes off it.
        /// </summary>
        static Color ShellPaint(Vector3 p, Vector3 n)
        {
            Color c = p.y > 0.125f && p.y < 0.16f ? CreamBand : EnamelRed;

            // A shade darker underneath, where the enamel is always in shadow and grime collects.
            if (n.y < -0.5f) c = EnamelRedDark;

            // Chipped along its edges and corners, where enamel gets knocked.
            int edges = (Mathf.Abs(p.x) > BodyHalfWidth - 0.07f ? 1 : 0) + (Mathf.Abs(p.z) > BodyHalfDepth - 0.07f ? 1 : 0) +
                        (p.y > BodyTop - 0.07f || p.y < BodyBottom + 0.07f ? 1 : 0);
            float chip = Mathf.Sin(p.x * 53f + p.y * 17f + p.z * 7f) * Mathf.Sin(p.z * 47f - p.y * 31f + p.x * 11f);
            if (edges >= 2 && chip > 0.45f) c = BareMetal;

            // Soot along the top edges, darkest at the very top.
            c = Color.Lerp(c, Soot, 0.55f * S.Smooth((p.y - (BodyTop - 0.06f)) / 0.06f));
            return c;
        }

        /// <summary>
        /// Chrome trims bent down over the lamp eyes into a glower, and a grille mouth where the crumb
        /// tray was: a dark opening with the elements glowing across it and bent metal teeth top and
        /// bottom. The eyes are their own objects, to blink.
        /// </summary>
        static void Face(B.Model m)
        {
            const float BrowY = 0.6f, BrowZ = BodyHalfDepth + 0.006f;
            m.Box(Chrome, new Vector3(EyeX + 0.005f, BrowY, BrowZ), new Vector3(0.19f, 0.028f, 0.03f), 0.01f, Quaternion.Euler(0f, 0f, 16f), 2);
            m.Box(Chrome, new Vector3(-EyeX - 0.005f, BrowY - 0.004f, BrowZ), new Vector3(0.17f, 0.028f, 0.03f), 0.01f, Quaternion.Euler(0f, 0f, -12f), 2);

            // The opening, framed in chrome.
            float z = BodyHalfDepth;
            m.Box(ChromeDark, new Vector3(0f, MouthY, z - 0.002f), new Vector3(MouthHalfWidth * 2f + 0.03f, MouthHalfHeight * 2f + 0.03f, 0.016f), 0.012f, 2);
            m.Box(SlotDark, new Vector3(0f, MouthY, z + 0.004f), new Vector3(MouthHalfWidth * 2f, MouthHalfHeight * 2f, 0.012f), 0.008f, 2);

            // The elements across it, glowing.
            for (int k = -1; k <= 1; k += 2)
                m.Box(Color.white, new Vector3(0f, MouthY + k * 0.016f, z + 0.011f), new Vector3(MouthHalfWidth * 2f - 0.04f, 0.007f, 0.004f), 0.002f,
                      Quaternion.identity, 1, B.Model.Glow);

            // Bent teeth, top and bottom, out of step and not all the same size.
            for (int i = 0; i < 7; i++)
            {
                float x = -MouthHalfWidth + 0.03f + i * (MouthHalfWidth * 2f - 0.06f) / 6f;
                float s = 0.034f * (1f + 0.25f * B.Jitter(i + 120));
                m.Box(Chrome, new Vector3(x, MouthY + MouthHalfHeight - 0.004f, z + 0.012f), new Vector3(s, s, 0.008f), 0.003f,
                      Quaternion.Euler(0f, 0f, 45f + 8f * B.Jitter(i + 130)));
                if (i < 6)
                {
                    float xb = x + (MouthHalfWidth * 2f - 0.06f) / 12f;
                    m.Box(Chrome, new Vector3(xb, MouthY - MouthHalfHeight + 0.004f, z + 0.012f), new Vector3(s * 0.85f, s * 0.85f, 0.008f), 0.003f,
                          Quaternion.Euler(0f, 0f, 45f + 8f * B.Jitter(i + 140)));
                }
            }
        }

        /// <summary>
        /// What the heat did to it: vents burnt through both sides with the glow showing, a riveted
        /// scrap patch over a hole on its right, a weld seam over the front corner, and an exhaust
        /// pipe out of the back. The third eye on its spring is its own object, to sway.
        /// </summary>
        static void Mutations(B.Model m)
        {
            // Three vents low on each side, the glow behind each grille.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (BodyHalfWidth + 0.002f);
                for (int k = 0; k < 3; k++)
                {
                    var at = new Vector3(x, 0.27f + k * 0.05f, -0.08f);
                    m.Box(SlotDark, at, new Vector3(0.012f, 0.03f, 0.14f), 0.008f);
                    m.Box(Color.white, at + Vector3.right * (side * 0.004f), new Vector3(0.008f, 0.012f, 0.11f), 0.004f, Quaternion.identity, 1, B.Model.Glow);
                }
            }

            // The patch, bolted on at the corners.
            var patch = new Vector3(BodyHalfWidth + 0.006f, 0.5f, -0.13f);
            m.Box(Patch, patch, new Vector3(0.012f, 0.13f, 0.15f), 0.004f, Quaternion.Euler(4f, 0f, 0f));
            for (int k = 0; k < 4; k++)
            {
                var rivet = patch + new Vector3(0.007f, (k & 1) == 0 ? -0.048f : 0.048f, (k & 2) == 0 ? -0.058f : 0.058f);
                S.Ball(m, (p, n) => ChromeDark, rivet, Vector3.one * 0.011f, Quaternion.identity, 8, 5);
            }

            // A weld seam over the top front corner, where it split and was mended.
            for (int k = 0; k < 6; k++)
            {
                float t = k / 5f;
                var bead = Vector3.Lerp(new Vector3(-0.37f, 0.67f, 0.2f), new Vector3(-0.31f, 0.56f, 0.27f), t) + new Vector3(0f, 0f, 0.012f);
                S.Ball(m, (p, n) => ChromeDark, bead, new Vector3(0.016f, 0.012f, 0.012f), Quaternion.Euler(0f, 0f, 30f), 8, 5);
            }

            // The exhaust pipe, with a collar where it comes out and a dark mouth on the end.
            Strand(m, Pipe, 0.034f, 0.03f, Chrome);
            m.Tube(ChromeDark, Pipe[0], Pipe[0] + (Pipe[1] - Pipe[0]).normalized * 0.03f, 0.044f, 12);
            Vector3 mouth = Pipe[Pipe.Length - 1];
            Vector3 up = (Pipe[Pipe.Length - 1] - Pipe[Pipe.Length - 2]).normalized;
            S.Disc(m, (p, n) => SlotDark, mouth + up * 0.001f, up, 0.026f, 14);
        }

        /// <summary>
        /// A lamp eye, facing +Z from where it sits in the panel: a chrome bezel round a domed amber
        /// lens - lit, in the warm glow the lanterns use - with a dark slit of a pupil across it.
        /// Squashed flat in its Y, it blinks.
        /// </summary>
        static B.Model Lamp()
        {
            var m = new B.Model();
            const float R = EyeRadius;

            // Built turned about Y, then laid over to face forward.
            Matrix4x4 was = m.Transform;
            m.Transform = Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.up, Vector3.forward));

            m.Lathe((p, n) => Chrome, Vector3.zero, new List<Vector2>
            {
                new Vector2(R * 0.8f, -0.02f), new Vector2(R + 0.014f, -0.02f), new Vector2(R + 0.02f, 0.008f), new Vector2(R + 0.012f, 0.024f),
                new Vector2(R * 0.86f, 0.028f), new Vector2(R * 0.8f, 0.016f),
            }, 24);

            m.Lathe((p, n) => Color.white, Vector3.zero, new List<Vector2>
            {
                new Vector2(R * 0.82f, 0.012f), new Vector2(R * 0.62f, 0.032f), new Vector2(R * 0.3f, 0.042f), new Vector2(0f, 0.045f),
            }, 24, B.Model.Glow);

            m.Transform = was;

            m.Box(Pupil, new Vector3(0f, 0f, 0.046f), new Vector3(0.02f, R * 1.3f, 0.006f), 0.004f);
            return m;
        }

        /// <summary>The third eye's stalk: a coiled chrome spring up from the corner, a little cup on top for the eye. About its root.</summary>
        static B.Model Stalk()
        {
            var m = new B.Model();

            m.Lathe((p, n) => ChromeDark, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, -0.02f), new Vector2(0.05f, -0.02f), new Vector2(0.05f, -0.02f), new Vector2(0.05f, 0.012f),
                new Vector2(0.05f, 0.012f), new Vector2(0f, 0.012f),
            }, 16);

            const int Steps = 70;
            const float Turns = 6f, Radius = 0.034f;
            var coil = new Vector3[Steps + 1];
            for (int i = 0; i <= Steps; i++)
            {
                float s = i / (float)Steps, a = s * Turns * Mathf.PI * 2f;
                coil[i] = new Vector3(Mathf.Cos(a) * Radius, 0.012f + s * (StalkHeight - 0.02f), Mathf.Sin(a) * Radius);
            }

            for (int i = 0; i < Steps; i++) m.Tube((p, n) => Chrome, coil[i], coil[i + 1], 0.009f, 0.009f, 6);

            m.Lathe((p, n) => Chrome, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, StalkHeight - 0.01f), new Vector2(0.025f, StalkHeight - 0.01f), new Vector2(0.05f, StalkHeight + 0.03f),
                new Vector2(0.05f, StalkHeight + 0.03f), new Vector2(0.044f, StalkHeight + 0.035f), new Vector2(0f, StalkHeight + 0.02f),
            }, 16);
            return m;
        }

        /// <summary>The two slices sitting in the slots, their tops showing: what the next shot throws.</summary>
        static B.Model Loaded()
        {
            var m = new B.Model();
            for (int side = -1; side <= 1; side += 2)
                Slice(m, new Vector3(0.02f * side, BodyTop - 0.18f, side * SlotZ), 1f, Quaternion.Euler(0f, 0f, 3f * side), flaming: false);
            return m;
        }

        /// <summary>One slice in flight, about its middle, a size up so it reads from the camera: scorched, embers glowing in it.</summary>
        static B.Model FlyingSlice()
        {
            var m = new B.Model();
            Slice(m, new Vector3(0f, -0.2f, 0f), 1.2f, Quaternion.identity, flaming: true);
            return m;
        }

        /// <summary>
        /// A slice of toast standing on <paramref name="foot"/>, face toward its Z: a square of bread
        /// with the domed top a slice of a loaf has, golden on the faces and dark at the crust - or,
        /// <paramref name="flaming"/>, scorched black round the edges with embers glowing in both faces.
        /// </summary>
        static void Slice(B.Model m, Vector3 foot, float scale, Quaternion turn, bool flaming)
        {
            float s = scale;
            Vector3 face = turn * Vector3.forward;
            Vector3 middle = foot + turn * new Vector3(0f, 0.16f * s, 0f);

            Color Paint(Vector3 p, Vector3 n)
            {
                if (Mathf.Abs(Vector3.Dot(n, face)) < 0.7f) return flaming ? Charred : Crust;

                // Toasted darker in the middle than at the edges, as a grill leaves it - or burnt in from the edges.
                float fromMiddle = Mathf.Clamp01((p - middle).magnitude / (0.2f * s));
                return flaming ? Color.Lerp(Scorched, Charred, fromMiddle * fromMiddle) : Color.Lerp(ToastDark, Toast, fromMiddle);
            }

            var box = new SmoothMesh(1);
            box.RoundedBox(0, foot + turn * new Vector3(0f, 0.11f * s, 0f), new Vector3(0.38f, 0.22f, 0.045f) * s, 0.018f * s, turn, 2);
            m.Add(box, Paint);

            for (int side = -1; side <= 1; side += 2)
            {
                var lobe = new SmoothMesh(1);
                Vector3 centre = foot + turn * new Vector3(side * 0.085f * s, 0.22f * s, 0f);
                lobe.Tube(0, centre - face * (0.0225f * s), centre + face * (0.0225f * s), 0.115f * s, 0.115f * s, 18);
                m.Add(lobe, Paint);
            }

            if (!flaming) return;

            // Embers burning in both faces.
            Vector2[] spots = { new Vector2(-0.08f, 0.1f), new Vector2(0.07f, 0.16f), new Vector2(0.01f, 0.05f), new Vector2(-0.03f, 0.24f), new Vector2(0.11f, 0.06f) };
            float[] sizes = { 0.035f, 0.028f, 0.03f, 0.022f, 0.02f };
            for (int sideOfSlice = -1; sideOfSlice <= 1; sideOfSlice += 2)
            {
                for (int k = 0; k < spots.Length; k++)
                {
                    Vector3 at = foot + turn * new Vector3(spots[k].x * s, spots[k].y * s, sideOfSlice * 0.0235f * s);
                    Ember(m, at, face * sideOfSlice, sizes[k] * s);
                }
            }
        }

        /// <summary>A glowing ember on a surface facing <paramref name="normal"/>: a ragged disc a hair proud of it.</summary>
        static void Ember(B.Model m, Vector3 centre, Vector3 normal, float radius)
        {
            normal.Normalize();
            Vector3 right = Vector3.Cross(Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up, normal).normalized;
            Vector3 up = Vector3.Cross(normal, right);
            float seed = centre.x * 13f + centre.y * 7f;

            m.Surface((p, n) => Color.white, 12, 2, (u, v) =>
            {
                float a = u * Mathf.PI * 2f;
                float ragged = 1f + 0.3f * Mathf.Sin(a * 3f + seed) + 0.15f * Mathf.Sin(a * 5f - seed);
                return centre + normal * 0.002f + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * (radius * ragged * v);
            }, true, B.Model.Glow);
        }

        /// <summary>The lever on its side: a stem out of the channel and a bakelite knob, about the lever's pivot.</summary>
        static B.Model Lever()
        {
            var m = new B.Model();
            m.Box(Chrome, new Vector3(0.03f, 0f, 0f), new Vector3(0.06f, 0.024f, 0.03f), 0.008f);
            m.Box(Bakelite, new Vector3(0.075f, 0f, 0f), new Vector3(0.05f, 0.05f, 0.11f), 0.018f, 2);
            return m;
        }

        // --- The empty site ------------------------------------------------------------------------

        /// <summary>The hot plate before anything is on it: switched off, its coil cold, a loaf left on it waiting.</summary>
        static B.Model Site()
        {
            B.Model m = Stand(hot: false);

            var loaf = new Vector3(0f, PlateTop, 0.02f);
            m.Box(Toast, loaf + Vector3.up * 0.08f, new Vector3(0.42f, 0.16f, 0.24f), 0.06f, Quaternion.Euler(0f, 18f, 0f), 3);
            S.Ball(m, (p, n) => ToastDark, loaf + Vector3.up * 0.15f, new Vector3(0.2f, 0.07f, 0.115f), Quaternion.Euler(0f, 18f, 0f), 18, 10);

            // Scores across the top of the crust.
            for (int k = -1; k <= 1; k++)
            {
                Quaternion turn = Quaternion.Euler(0f, 18f, 0f);
                m.Box(Crust, loaf + turn * new Vector3(k * 0.11f, 0.215f, 0f), new Vector3(0.025f, 0.012f, 0.16f), 0.005f, turn * Quaternion.Euler(0f, 25f, 0f));
            }

            return m;
        }

        // --- Shapes ---------------------------------------------------------------------------------

        /// <summary>A strand through <paramref name="points"/> - a flex, a pipe - tapering from <paramref name="from"/> to <paramref name="to"/>, a ball at each joint so it bends smoothly.</summary>
        static void Strand(B.Model m, IList<Vector3> points, float from, float to, Color colour)
        {
            int last = points.Count - 1;
            for (int i = 0; i < last; i++)
            {
                float r0 = Mathf.Lerp(from, to, i / (float)last), r1 = Mathf.Lerp(from, to, (i + 1) / (float)last);
                m.Tube((p, n) => colour, points[i], points[i + 1], r0, r1, 12);
                if (i > 0) S.Ball(m, (p, n) => colour, points[i], Vector3.one * r0, Quaternion.identity, 12, 6);
            }
        }

        /// <summary>A small light: glowing when <paramref name="lit"/>, a dark lens when not.</summary>
        static void Glow(B.Model m, bool lit, Vector3 at, float radius)
        {
            var ball = new SmoothMesh(1);
            ball.RoundedBox(0, at, Vector3.one * radius * 2f, radius * 0.99f, Quaternion.identity, 3);
            if (lit) m.Add(ball, Color.white, B.Model.Glow);
            else m.Add(ball, SlotDark);
        }

        // --- Assets ---------------------------------------------------------------------------------

        static Mesh Save(B.Model model, string name) => model.Save($"{Folder}/{name}.asset", name);

        /// <summary>A crumb about a metre across, for a particle to scale down.</summary>
        static Mesh Crumb()
        {
            var shape = new SmoothMesh(1);
            shape.RoundedBox(0, Vector3.zero, new Vector3(1f, 0.7f, 0.85f), 0.25f, Quaternion.Euler(10f, 20f, 5f), 2);
            return shape.BuildAsset($"{Folder}/Toastapult_Crumb.asset", "Toastapult_Crumb", out List<int> _);
        }

        /// <summary>Unlit orange, for everything red-hot.</summary>
        static Material HotMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new System.InvalidOperationException("Shader Universal Render Pipeline/Unlit is missing.");

            string path = $"{Folder}/Toastapult_Hot.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "Toastapult_Hot" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", Hot);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }
    }
}
