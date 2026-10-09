using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using B = LastOrders.EditorTools.BuildingKit;
using FX = LastOrders.EditorTools.PlantingEffects;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The soup station (pfSoupStove) as a little soup kitchen corner in the game's smooth low-poly
    /// style, in place of the prototype tray oven: a teal enamel range with the fire showing behind
    /// its firebox grate, a cream enamel stockpot of tomato soup on the hob and a saucepan beside it,
    /// a stovepipe and a warming shelf of spice tins with a ladle, a sieve and a skillet hung under it;
    /// an oak prep counter round the left with tomatoes being chopped on a board, a stack of bowls, oil
    /// and salt, a sack and a crate of potatoes underneath; logs stacked on the right, a bucket, a crate
    /// of tomatoes on the floor, a slatted mat where the cook stands - all on a pad of terracotta tiles
    /// that marks the corner out from the kitchen floor.
    ///
    /// What moves while the station cooks - the soup and its bubbles, the spoon, the saucepan's lid,
    /// the flames round the pot and behind the grate, the steam - is its own object, driven by a
    /// <see cref="SoupKitchenAnimation"/>. Everything else is one mesh, painted in its vertices on
    /// LastOrders/Stylized with the embers in an unlit glow, as <see cref="BuildingKit"/> builds.
    ///
    /// Its front faces -Z. It is set into the station turned round, since the station's front is its
    /// +Z, where the prototype's oven door faced. The prototype model and the painted floor shadows
    /// that went with it are switched off, not deleted; the collider is fitted to the corner, so a tap
    /// anywhere on it reaches the station. The station's components and centre point are left alone.
    /// The build ghost (SoupStoveVisuals) is given the corner's still mesh in the same place.
    /// </summary>
    public static class SoupKitchenModel
    {
        const string PrefabPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Stoves/SoupStove/pfSoupStove.prefab";

        /// <summary>What build mode shows, tinted, while a soup station is being placed.</summary>
        const string GhostPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Stoves/SoupStove/SoupStoveVisuals.prefab";

        const string OldGhostModel = "Stove_Prefab";
        const string Folder = "Assets/Game Assets/Content/LastOrders/Kitchen/" + EnvKit.Style + "/SoupKitchen";
        const string PuffTexturePath = FX.Folder + "/Particle_Puff.png";

        const string OldModel = "Bread oven";
        static readonly string[] OldShadows = { "Square (2)", "Square (3)" };
        const string ModelName = "SoupKitchen";

        /// <summary>
        /// Where the corner's middle stands in the station's space, on the floor (the station's origin
        /// is a hair above it). The prototype stood about here, so the corner fills the same gap between
        /// the bread oven and the salad station along the kitchen's back wall.
        /// </summary>
        static readonly Vector3 Place = new Vector3(0.32f, -0.005f, 0.61f);

        // --- Measurements (metres; front toward -Z) ------------------------------------------------

        /// <summary>The tiled pad the corner stands on.</summary>
        const float PadLeft = -1.2f, PadRight = 1.2f, PadFront = -0.6f, PadBack = 1.15f, PadTop = 0.047f;

        /// <summary>The range: bun feet, the enamel body, the iron hob on top.</summary>
        const float RangeLeft = -0.42f, RangeRight = 0.82f, RangeFront = 0.4f, RangeBack = 1.1f;
        const float FeetTop = PadTop + 0.07f, BodyTop = 0.76f, HobTop = 0.82f;

        /// <summary>The enamel body's face, under the hob's overhang.</summary>
        const float BodyFront = RangeFront + 0.02f;

        /// <summary>The top of the ring plates the pans stand on.</summary>
        const float RingTop = HobTop + 0.014f;

        const float PotX = -0.08f, PotZ = 0.74f, PotRadius = 0.26f, PotHeight = 0.35f, PotInner = 0.246f;
        const float SoupY = RingTop + 0.29f;
        const float PanX = 0.5f, PanZ = 0.8f, PanRadius = 0.14f, PanHeight = 0.13f;
        const float PanTop = RingTop + PanHeight;

        /// <summary>The firebox door under the stockpot, and the window in it the fire shows through.</summary>
        const float FireX = PotX, DoorHalf = 0.21f, WindowHalf = 0.13f, WindowBottom = 0.46f, WindowTop = 0.6f;
        const float DoorFront = BodyFront - 0.05f;
        const float EmberY = WindowBottom + 0.018f;
        const float FlameZ = BodyFront - 0.03f;

        const float ShelfLeft = RangeLeft, ShelfRight = 0.64f, ShelfY = 1.34f, ShelfFront = 0.95f, ShelfBack = 1.12f;
        const float PipeX = 0.72f, PipeZ = 1.0f, PipeTop = 1.82f;

        const float CounterLeft = -1.17f, CounterRight = -0.57f, CounterFront = -0.5f, CounterBack = 1.08f, CounterTop = 0.86f;
        const float CounterShelf = 0.22f;

        // --- The palette ---------------------------------------------------------------------------

        /// <summary>Brick-red quarry tiles, muted: a warmer, more orange terracotta read as salmon across a pad this size.</summary>
        static readonly Color[] Tiles = { B.Hex("8A5650"), B.Hex("925C55"), B.Hex("84524B"), B.Hex("8E5A52") };
        static readonly Color Grout = B.Hex("6B6560");

        static readonly Color Enamel = B.Hex("3D6762");
        static readonly Color EnamelLight = B.Hex("47756F");
        static readonly Color IronTop = B.Hex("33363A");
        static readonly Color IronTrim = B.Hex("484C51");
        static readonly Color IronDark = B.Hex("2A2C2F");
        static readonly Color Soot = B.Hex("1F1C1B");
        static readonly Color Coal = B.Hex("2B2522");
        static readonly Color Steel = B.Hex("9EA4A9");
        static readonly Color SteelDark = B.Hex("6E7479");
        static readonly Color PanSteel = B.Hex("8D9398");
        static readonly Color PanSteelLight = B.Hex("A5ABB0");
        static readonly Color SieveMesh = B.Hex("5F656A");
        static readonly Color DialFace = B.Hex("B9B2A2");
        static readonly Color Bakelite = B.Hex("2B2D30");

        /// <summary>Cream enamel with a navy rim, the pot and the bowls alike. Mid cream: any lighter and its lit side clips.</summary>
        static readonly Color Cream = B.Hex("AFA898");
        static readonly Color CreamInside = B.Hex("968F80");
        static readonly Color Navy = B.Hex("2E4560");

        /// <summary>Tomato soup. Painted this deep, it shows a full red without its lit side clipping.</summary>
        static readonly Color SoupRed = B.Hex("9E3628");
        static readonly Color SoupDeep = B.Hex("7E2A1F");
        static readonly Color SoupCream = B.Hex("BFA48C");
        static readonly Color SoupBubbleTop = B.Hex("BE5A44");
        static readonly Color Herb = B.Hex("4F7A2E");
        static readonly Color HerbLight = B.Hex("6E9238");

        static readonly Color TomatoRed = B.Hex("B5302A");
        static readonly Color TomatoFlesh = B.Hex("C24A36");
        static readonly Color TomatoCore = B.Hex("D58360");
        static readonly Color Calyx = B.Hex("4E6E2A");
        static readonly Color Potato = B.Hex("8E7150");

        static readonly Color[] Oak = { B.Hex("B39B7A"), B.Hex("AA9171"), B.Hex("BBA383"), B.Hex("A58C6C") };
        static readonly Color BoardOak = B.Hex("C2AB88");
        static readonly Color SpoonWood = B.Hex("B59B76");
        static readonly Color SpoonWet = B.Hex("8A4A33");
        static readonly Color Bark = B.Hex("584536");
        static readonly Color LogEnd = B.Hex("C9AE86");
        static readonly Color LogRing = B.Hex("8F7558");

        static readonly Color TowelCream = B.Hex("BDB4A0");
        static readonly Color TowelRed = B.Hex("A33A32");
        static readonly Color GlassGreen = B.Hex("3E5E3A");
        static readonly Color GlassOil = B.Hex("8C8A3A");
        static readonly Color Cork = B.Hex("9C7F5C");
        static readonly Color CrockGlaze = B.Hex("A9A291");
        static readonly Color SpiceRed = B.Hex("A33A32");
        static readonly Color SpiceMustard = B.Hex("B8962E");
        static readonly Color SpiceTeal = B.Hex("3E6B66");
        static readonly Color Galvanised = B.Hex("8F969B");
        static readonly Color GalvanisedDark = B.Hex("6B7176");
        static readonly Color Water = B.Hex("5D7480");

        [MenuItem("Last Orders/Kitchen/Build Soup Kitchen (pfSoupStove)", priority = 99)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This builds the soup kitchen corner - range, stockpot, prep counter, woodpile, tiled pad - and puts it into " +
                "pfSoupStove in place of the prototype tray oven, which is switched off with its painted floor shadows, not deleted.\n\n" +
                "While the station cooks, the fire roars, the soup bubbles, the spoon stirs, steam rises and the saucepan lid rattles.\n\n" +
                "The station's components and centre point are kept; its collider is fitted to the corner. The build ghost " +
                "(SoupStoveVisuals) shows the corner too, its generic stove switched off.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        /// <summary>Builds the corner and puts it into the prefab, with no dialogs.</summary>
        public static string Apply()
        {
            ContentGenerator.EnsureFolder(Folder);

            Material painted = B.PaintedMaterial();
            Material ember = UnlitMaterial("SoupKitchen_Ember", new Color(0.95f, 0.4f, 0.13f));
            Material flame = UnlitMaterial("SoupKitchen_Flame", new Color(1f, 0.5f, 0.15f));
            Material flameCore = UnlitMaterial("SoupKitchen_FlameCore", new Color(1f, 0.85f, 0.42f));
            Material steam = SteamMaterial();

            Mesh corner = SaveMesh(Corner(), "SoupKitchen");
            Mesh soup = SaveMesh(Soup(), "SoupKitchen_Soup");
            Mesh bubble = SaveMesh(Bubble(), "SoupKitchen_Bubble");
            Mesh spoon = SaveMesh(Spoon(), "SoupKitchen_Spoon");
            Mesh lid = SaveMesh(Lid(), "SoupKitchen_Lid");
            Mesh potFlamesA = SaveMesh(PotFlames(0), "SoupKitchen_PotFlames_A");
            Mesh potFlamesB = SaveMesh(PotFlames(1), "SoupKitchen_PotFlames_B");
            Mesh fireFlamesA = SaveMesh(FireboxFlames(0), "SoupKitchen_FireboxFlames_A");
            Mesh fireFlamesB = SaveMesh(FireboxFlames(1), "SoupKitchen_FireboxFlames_B");

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                Transform old = root.transform.Find(OldModel);
                if (old != null) old.gameObject.SetActive(false);

                int shadowsOff = 0;
                foreach (string name in OldShadows)
                {
                    Transform shadow = root.transform.Find(name);
                    if (shadow == null || !shadow.gameObject.activeSelf) continue;
                    shadow.gameObject.SetActive(false);
                    shadowsOff++;
                }

                Transform previous = root.transform.Find(ModelName);
                if (previous != null) Object.DestroyImmediate(previous.gameObject);

                var go = new GameObject(ModelName);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Place;
                go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                B.Show(go, corner, corner.subMeshCount > 1 ? new[] { painted, ember } : new[] { painted });

                Transform model = go.transform;
                Transform soupPart = Part(model, "Soup", soup, new Vector3(PotX, SoupY, PotZ), painted, ShadowCastingMode.Off);

                var bubbles = new Transform[7];
                for (int i = 0; i < bubbles.Length; i++)
                {
                    bubbles[i] = Part(soupPart, $"Bubble_{i}", bubble, Vector3.zero, painted, ShadowCastingMode.Off);
                    bubbles[i].gameObject.SetActive(false);
                }

                // Resting with its handle to the back right, clear of the soup as the camera sees it.
                Transform spoonPart = Part(model, "Spoon", spoon, new Vector3(PotX, SoupY, PotZ), painted, ShadowCastingMode.On);
                spoonPart.localRotation = Quaternion.Euler(0f, -40f, 0f);

                Transform lidPart = Part(model, "Lid", lid, new Vector3(PanX, PanTop, PanZ), painted, ShadowCastingMode.On);

                // Out until the station cooks, so the corner stands idle in the editor as it does in the game.
                var potFlames = new[]
                {
                    Part(model, "PotFlames_A", potFlamesA, new Vector3(PotX, RingTop, PotZ), flame, ShadowCastingMode.Off),
                    Part(model, "PotFlames_B", potFlamesB, new Vector3(PotX, RingTop, PotZ), flameCore, ShadowCastingMode.Off),
                };
                foreach (Transform set in potFlames) set.gameObject.SetActive(false);

                var fireboxFlames = new[]
                {
                    Part(model, "FireboxFlames_A", fireFlamesA, new Vector3(FireX, EmberY, FlameZ), flame, ShadowCastingMode.Off),
                    Part(model, "FireboxFlames_B", fireFlamesB, new Vector3(FireX, EmberY, FlameZ), flameCore, ShadowCastingMode.Off),
                };

                ParticleSystem potSteam = Steam(model, steam);
                ParticleSystem lidSteam = LidSteam(model, steam);

                go.AddComponent<SoupKitchenAnimation>().Configure(soupPart, spoonPart, lidPart, bubbles, potFlames, fireboxFlames, potSteam,
                                                                  lidSteam);

                string colliderNote = FitCollider(root, model);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return $"[Last Orders] Soup kitchen built: {corner.vertexCount} vertices in the corner, 6 moving parts, steam; in pfSoupStove" +
                       $"{(old != null ? $"; the prototype '{OldModel}' model is switched off" : "")}" +
                       $"{(shadowsOff > 0 ? $", {shadowsOff} painted floor shadows switched off" : "")}{colliderNote}. {DressGhost(corner, soup, spoon, lid, painted, ember)}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The build ghost shows the corner too, standing where the station will put it: the corner's
        /// still mesh in place of the generic stove model, which is switched off, with the soup, the
        /// spoon and the lid at rest so the pots are not empty. Nothing moves on a ghost (build mode
        /// switches its behaviours off), so it gets no fire, steam or animation; its footprint and
        /// anchor markers are left as they are.
        /// </summary>
        static string DressGhost(Mesh corner, Mesh soup, Mesh spoon, Mesh lid, Material painted, Material ember)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(GhostPath);

            try
            {
                Transform old = root.transform.Find(OldGhostModel);
                if (old != null) old.gameObject.SetActive(false);

                Transform previous = root.transform.Find(ModelName);
                if (previous != null) Object.DestroyImmediate(previous.gameObject);

                var go = new GameObject(ModelName);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Place;
                go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                B.Show(go, corner, corner.subMeshCount > 1 ? new[] { painted, ember } : new[] { painted });
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

                Part(go.transform, "Soup", soup, new Vector3(PotX, SoupY, PotZ), painted, ShadowCastingMode.Off);
                Part(go.transform, "Spoon", spoon, new Vector3(PotX, SoupY, PotZ), painted, ShadowCastingMode.Off).localRotation = Quaternion.Euler(0f, -40f, 0f);
                Part(go.transform, "Lid", lid, new Vector3(PanX, PanTop, PanZ), painted, ShadowCastingMode.Off);

                PrefabUtility.SaveAsPrefabAsset(root, GhostPath);
                return $"Build ghost (SoupStoveVisuals) shows the corner{(old != null ? $"; its '{OldGhostModel}' is switched off" : "")}.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Saves a model as a mesh asset of the same name in the corner's folder.</summary>
        static Mesh SaveMesh(B.Model model, string name) => model.Save($"{Folder}/{name}.asset", name);

        /// <summary>A moving part: its own object under the corner, showing <paramref name="mesh"/> in <paramref name="material"/>.</summary>
        static Transform Part(Transform parent, string name, Mesh mesh, Vector3 at, Material material, ShadowCastingMode shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            B.Show(go, mesh, new[] { material });
            go.GetComponent<MeshRenderer>().shadowCastingMode = shadows;
            return go.transform;
        }

        /// <summary>The station's collider made to cover the pad and what stands on it, a little over a metre high.</summary>
        static string FitCollider(GameObject root, Transform model)
        {
            var collider = root.GetComponent<BoxCollider>();
            if (collider == null) return "";

            Vector3 before = collider.center, beforeSize = collider.size;
            var middle = new Vector3((PadLeft + PadRight) * 0.5f, 0.6f, (PadFront + PadBack) * 0.5f);
            collider.center = root.transform.InverseTransformPoint(model.TransformPoint(middle));

            // Turned half round, so the box's sides still run along the station's axes.
            collider.size = new Vector3(PadRight - PadLeft, 1.2f, PadBack - PadFront);
            return $"; collider {before} x {beforeSize} -> {collider.center} x {collider.size}";
        }

        // --- The corner ---------------------------------------------------------------------------

        static B.Model Corner()
        {
            // A shade darker toward the floor, on everything standing on the pad but not on the tiles themselves.
            var m = new B.Model { Shade = (p, n) => p.y < PadTop + 0.01f ? 1f : Mathf.Lerp(0.84f, 1f, Smooth((p.y - PadTop) / 0.35f)) };

            Pad(m);
            Duckboard(m);
            Range(m);
            Stockpot(m);
            Saucepan(m);
            WarmingShelf(m);
            Stovepipe(m);
            Counter(m);
            Woodpile(m);
            Bucket(m);
            TomatoCrate(m);

            return m;
        }

        /// <summary>Terracotta tiles on a grout bed, a shade apart from each other; none under the range, where nobody sees them.</summary>
        static void Pad(B.Model m)
        {
            float width = PadRight - PadLeft, depth = PadBack - PadFront;
            float bed = PadTop - 0.012f;
            m.Box(Grout, new Vector3((PadLeft + PadRight) * 0.5f, bed * 0.5f, (PadFront + PadBack) * 0.5f), new Vector3(width, bed, depth), 0.012f, 2);

            const int across = 8, down = 6;
            float tw = width / across, td = depth / down;

            for (int j = 0; j < down; j++)
            {
                for (int i = 0; i < across; i++)
                {
                    float x = PadLeft + (i + 0.5f) * tw, z = PadFront + (j + 0.5f) * td;
                    if (x - tw * 0.5f > RangeLeft && x + tw * 0.5f < RangeRight && z - td * 0.5f > RangeFront) continue;

                    int key = j * across + i;
                    Color c = Tiles[B.Hash(key + 300) % Tiles.Length] * (1f + 0.035f * B.Jitter(key + 900));
                    m.Box(c, new Vector3(x, PadTop - 0.007f, z), new Vector3(tw - 0.018f, 0.014f, td - 0.018f), 0.005f);
                }
            }
        }

        /// <summary>The slatted mat in front of the range, where the cook stands.</summary>
        static void Duckboard(B.Model m)
        {
            const float left = -0.5f, right = 0.7f, front = -0.5f, back = 0.3f;

            foreach (float x in new[] { left + 0.06f, (left + right) * 0.5f, right - 0.06f })
                m.Box(B.Walnut, new Vector3(x, PadTop + 0.014f, (front + back) * 0.5f), new Vector3(0.05f, 0.028f, back - front - 0.04f), 0.008f);

            const int slats = 6;
            float pitch = (back - front) / slats;
            for (int k = 0; k < slats; k++)
            {
                float z = front + (k + 0.5f) * pitch;
                Color c = Oak[B.Hash(k + 40) % Oak.Length] * (1f + 0.04f * B.Jitter(k + 41));
                m.Box(c, new Vector3((left + right) * 0.5f, PadTop + 0.04f, z), new Vector3(right - left, 0.024f, pitch - 0.03f), 0.008f);
            }
        }

        // --- The range ----------------------------------------------------------------------------

        /// <summary>Teal enamel on iron bun feet under an iron hob edged in steel, with its firebox door, oven door and rail.</summary>
        static void Range(B.Model m)
        {
            float mid = (RangeLeft + RangeRight) * 0.5f, width = RangeRight - RangeLeft;
            float bodyZ = (BodyFront + RangeBack) * 0.5f, bodyDepth = RangeBack - BodyFront;

            foreach (float x in new[] { RangeLeft + 0.07f, RangeRight - 0.07f })
            {
                foreach (float z in new[] { BodyFront + 0.06f, RangeBack - 0.08f })
                {
                    m.Lathe((p, n) => IronDark, new Vector3(x, PadTop, z), new List<Vector2>
                    {
                        new Vector2(0f, 0f), new Vector2(0.034f, 0f), new Vector2(0.042f, 0.028f), new Vector2(0.034f, FeetTop - PadTop + 0.01f),
                        new Vector2(0f, FeetTop - PadTop + 0.01f),
                    }, 10);
                }
            }

            m.Box(IronDark, new Vector3(mid, FeetTop + 0.025f, bodyZ), new Vector3(width - 0.02f, 0.05f, bodyDepth - 0.01f), 0.012f);
            m.Box(Enamel, new Vector3(mid, (FeetTop + 0.05f + BodyTop) * 0.5f, bodyZ), new Vector3(width - 0.04f, BodyTop - FeetTop - 0.05f, bodyDepth),
                  0.03f, 2);

            m.Box(IronTop, new Vector3(mid, (BodyTop + HobTop) * 0.5f, (RangeFront + RangeBack) * 0.5f),
                  new Vector3(width, HobTop - BodyTop, RangeBack - RangeFront), 0.02f, 2);
            m.Box(Steel, new Vector3(mid, HobTop - 0.03f, RangeFront + 0.005f), new Vector3(width + 0.006f, 0.034f, 0.014f), 0.006f);

            RingPlate(m, PotX, PotZ, 0.3f);
            RingPlate(m, PanX, PanZ, 0.17f);

            Firebox(m);
            OvenDoor(m);
            Rail(m);
        }

        /// <summary>A round iron plate set in the hob for a pan to stand on, its edge a lighter ring.</summary>
        static void RingPlate(B.Model m, float x, float z, float radius)
        {
            m.Lathe((p, n) => new Vector2(p.x - x, p.z - z).magnitude > radius - 0.03f ? IronTrim : IronDark, new Vector3(x, HobTop - 0.002f, z),
                    new List<Vector2>
                    {
                        new Vector2(0f, 0f), new Vector2(radius, 0f), new Vector2(radius, 0f), new Vector2(radius, RingTop - HobTop + 0.002f),
                        new Vector2(radius, RingTop - HobTop + 0.002f), new Vector2(radius - 0.026f, RingTop - HobTop + 0.002f),
                        new Vector2(radius - 0.034f, RingTop - HobTop + 0.002f), new Vector2(0f, RingTop - HobTop + 0.002f),
                    }, 28);
        }

        /// <summary>
        /// The firebox door under the stockpot: an iron frame round a window, standing out from the
        /// body, and behind it the dark of the firebox, a bed of glowing embers with coals on it and
        /// the grate's bars. The flames are their own parts, so they can burn up and die down.
        /// </summary>
        static void Firebox(B.Model m)
        {
            float z = (DoorFront + BodyFront) * 0.5f, depth = BodyFront - DoorFront + 0.004f;
            const float doorBottom = 0.3f, doorTop = 0.68f;

            m.Box(IronTrim, new Vector3(FireX, (WindowTop + doorTop) * 0.5f, z), new Vector3(DoorHalf * 2f, doorTop - WindowTop, depth), 0.012f, 2);
            m.Box(IronTrim, new Vector3(FireX, (doorBottom + WindowBottom) * 0.5f, z), new Vector3(DoorHalf * 2f, WindowBottom - doorBottom, depth),
                  0.012f, 2);
            for (int side = -1; side <= 1; side += 2)
            {
                m.Box(IronTrim, new Vector3(FireX + side * (WindowHalf + DoorHalf) * 0.5f, (WindowBottom + WindowTop) * 0.5f, z),
                      new Vector3(DoorHalf - WindowHalf, WindowTop - WindowBottom + 0.02f, depth), 0.012f, 2);
            }

            m.Box(Soot, new Vector3(FireX, (WindowBottom + WindowTop) * 0.5f, BodyFront - 0.01f),
                  new Vector3(WindowHalf * 2f + 0.02f, WindowTop - WindowBottom + 0.02f, 0.02f), 0.004f);

            var embers = new SmoothMesh(1);
            embers.RoundedBox(0, new Vector3(FireX, WindowBottom + 0.012f, BodyFront - 0.022f), new Vector3(WindowHalf * 2f - 0.01f, 0.026f, 0.016f),
                              0.006f, Quaternion.identity, 2);
            m.Add(embers, Color.white, B.Model.Glow);

            for (int k = 0; k < 5; k++)
            {
                float x = FireX - WindowHalf + 0.03f + k * 0.05f;
                m.Box(Coal, new Vector3(x, WindowBottom + 0.02f + 0.004f * B.Jitter(k), BodyFront - 0.032f), new Vector3(0.034f, 0.022f, 0.018f), 0.007f,
                      Quaternion.Euler(0f, 0f, 20f * B.Jitter(k + 3)));
            }

            for (int k = -1; k <= 1; k++)
            {
                m.Tube(IronDark, new Vector3(FireX + k * 0.065f, WindowBottom - 0.005f, DoorFront + 0.004f),
                       new Vector3(FireX + k * 0.065f, WindowTop + 0.005f, DoorFront + 0.004f), 0.008f, 6);
            }

            // Hinges down the left, the latch on the right.
            foreach (float y in new[] { 0.36f, 0.62f })
                m.Box(IronDark, new Vector3(FireX - DoorHalf - 0.008f, y, DoorFront + 0.02f), new Vector3(0.024f, 0.05f, 0.03f), 0.006f);
            float latch = FireX + (WindowHalf + DoorHalf) * 0.5f;
            m.Tube(Steel, new Vector3(latch, 0.53f, DoorFront + 0.002f), new Vector3(latch, 0.53f, DoorFront - 0.026f), 0.015f, 10);

            // The ash drawer under it, with its pull.
            m.Box(IronTrim, new Vector3(FireX, 0.22f, z), new Vector3(DoorHalf * 2f, 0.085f, depth), 0.01f, 2);
            m.Tube(Steel, new Vector3(FireX - 0.07f, 0.22f, DoorFront - 0.018f), new Vector3(FireX + 0.07f, 0.22f, DoorFront - 0.018f), 0.008f, 8);
            foreach (float x in new[] { FireX - 0.07f, FireX + 0.07f })
                m.Tube(Steel, new Vector3(x, 0.22f, DoorFront + 0.002f), new Vector3(x, 0.22f, DoorFront - 0.02f), 0.006f, 6);
        }

        /// <summary>The oven door on the right: a raised panel in lighter enamel, a steel bar handle and a thermometer dial.</summary>
        static void OvenDoor(B.Model m)
        {
            float left = FireX + DoorHalf + 0.04f, right = RangeRight - 0.06f;
            float x = (left + right) * 0.5f, w = right - left;
            const float bottom = 0.2f, top = 0.68f;
            float y = (bottom + top) * 0.5f, h = top - bottom;

            m.Box(EnamelLight, new Vector3(x, y, BodyFront - 0.014f), new Vector3(w, h, 0.03f), 0.022f, 2);
            m.Box(Enamel, new Vector3(x, y - 0.02f, BodyFront - 0.032f), new Vector3(w - 0.09f, h - 0.16f, 0.012f), 0.008f, 2);

            float hy = top - 0.045f, hz = BodyFront - 0.06f;
            m.Tube(Steel, new Vector3(left + 0.04f, hy, hz), new Vector3(right - 0.04f, hy, hz), 0.012f, 10);
            foreach (float sx in new[] { left + 0.06f, right - 0.06f })
                m.Box(Steel, new Vector3(sx, hy, (hz + BodyFront - 0.03f) * 0.5f), new Vector3(0.022f, 0.022f, BodyFront - 0.03f - hz), 0.006f);

            var dial = new Vector3(x, y + 0.05f, BodyFront - 0.04f);
            Disc(m, (p, n) => IronDark, dial + Vector3.forward * 0.002f, Vector3.back, 0.05f);
            Disc(m, (p, n) => DialFace, dial, Vector3.back, 0.04f);
            m.Box(IronDark, dial + new Vector3(0.012f, 0.008f, -0.002f), new Vector3(0.03f, 0.006f, 0.003f), 0.001f, Quaternion.Euler(0f, 0f, 30f));

            // A tea towel hung over the handle.
            const float towelX = 0.62f, towelWidth = 0.15f;
            m.Tube(TowelCream, new Vector3(towelX - towelWidth * 0.5f, hy + 0.016f, hz), new Vector3(towelX + towelWidth * 0.5f, hy + 0.016f, hz), 0.02f, 10);
            m.Box(TowelCream, new Vector3(towelX, hy - 0.1f, hz - 0.02f), new Vector3(towelWidth, 0.22f, 0.01f), 0.004f);
            m.Box(TowelCream, new Vector3(towelX, hy - 0.06f, hz + 0.02f), new Vector3(towelWidth, 0.14f, 0.01f), 0.004f);
            foreach (float sy in new[] { hy - 0.17f, hy - 0.145f })
                m.Box(TowelRed, new Vector3(towelX, sy, hz - 0.026f), new Vector3(towelWidth + 0.002f, 0.014f, 0.004f), 0.0015f);
        }

        /// <summary>The steel rail along the front of the range, under the hob's edge.</summary>
        static void Rail(B.Model m)
        {
            const float y = 0.725f, z = RangeFront - 0.055f;
            m.Tube(Steel, new Vector3(RangeLeft + 0.01f, y, z), new Vector3(RangeRight - 0.01f, y, z), 0.011f, 10);
            foreach (float x in new[] { RangeLeft + 0.035f, RangeRight - 0.035f })
                m.Box(Steel, new Vector3(x, y, (z + BodyFront) * 0.5f), new Vector3(0.018f, 0.02f, BodyFront - z), 0.005f);
        }

        /// <summary>
        /// The stockpot: cream enamel, a navy pinstripe, a rolled navy rim and squared navy handles,
        /// its inside wall down to below where the soup stands. The soup is its own part.
        /// </summary>
        static void Stockpot(B.Model m)
        {
            float b = RingTop;
            var profile = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(PotRadius - 0.035f, 0f), new Vector2(PotRadius - 0.012f, 0.01f), new Vector2(PotRadius, 0.04f),
                new Vector2(PotRadius, 0.255f), new Vector2(PotRadius, 0.258f), new Vector2(PotRadius, 0.276f), new Vector2(PotRadius, 0.279f),
                new Vector2(PotRadius, PotHeight - 0.034f), new Vector2(PotRadius, PotHeight - 0.03f), new Vector2(PotRadius + 0.008f, PotHeight - 0.016f),
                new Vector2(PotRadius + 0.006f, PotHeight - 0.002f), new Vector2(PotRadius - 0.006f, PotHeight), new Vector2(PotInner, PotHeight - 0.012f),
                new Vector2(PotInner, PotHeight - 0.02f), new Vector2(PotInner, PotHeight - 0.1f),
            };

            m.Lathe((p, n) =>
            {
                float y = p.y - b;
                float r = new Vector2(p.x - PotX, p.z - PotZ).magnitude;
                if (r < PotRadius - 0.007f && y > 0.2f) return y > PotHeight - 0.016f ? Navy : CreamInside;
                if (y > PotHeight - 0.032f) return Navy;
                if (y > 0.2565f && y < 0.2775f) return Navy;
                return y < 0.015f ? Navy * 0.8f : Cream;
            }, new Vector3(PotX, b, PotZ), profile, 32);

            float hy = b + PotHeight - 0.07f;
            for (int s = -1; s <= 1; s += 2)
            {
                float x = PotX + s * PotRadius;
                foreach (float dz in new[] { -0.05f, 0.05f })
                    m.Box(Navy, new Vector3(x + s * 0.018f, hy, PotZ + dz), new Vector3(0.04f, 0.024f, 0.018f), 0.007f);
                m.Tube(Navy, new Vector3(x + s * 0.036f, hy, PotZ - 0.065f), new Vector3(x + s * 0.036f, hy, PotZ + 0.065f), 0.012f, 8);
            }
        }

        /// <summary>The steel saucepan on the right ring, a black handle out to the front; its lid is its own part, with soup under it.</summary>
        static void Saucepan(B.Model m)
        {
            float b = RingTop;
            m.Lathe((p, n) => p.y - b < 0.02f ? SteelDark : PanSteel, new Vector3(PanX, b, PanZ), new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(PanRadius - 0.02f, 0f), new Vector2(PanRadius - 0.006f, 0.006f), new Vector2(PanRadius, 0.022f),
                new Vector2(PanRadius, PanHeight - 0.01f), new Vector2(PanRadius + 0.004f, PanHeight - 0.002f), new Vector2(PanRadius - 0.002f, PanHeight),
                new Vector2(PanRadius - 0.01f, PanHeight - 0.006f), new Vector2(PanRadius - 0.01f, PanHeight - 0.05f),
            }, 22);

            Disc(m, (p, n) => SoupRed, new Vector3(PanX, b + PanHeight - 0.04f, PanZ), Vector3.up, PanRadius - 0.008f, 16);

            Vector3 dir = new Vector3(0.45f, 0f, -0.89f).normalized;
            Vector3 root = new Vector3(PanX, b + PanHeight - 0.03f, PanZ) + dir * PanRadius;
            Vector3 tip = root + dir * 0.21f + Vector3.up * 0.03f;
            m.Box(Steel, root + dir * 0.012f, new Vector3(0.03f, 0.022f, 0.03f), 0.006f, Quaternion.LookRotation(dir));
            m.Tube((p, n) => Bakelite, root + dir * 0.02f, tip, 0.014f, 0.016f, 8);
        }

        /// <summary>
        /// The warming shelf on two iron brackets at the back of the hob: spice tins, a salt crock, a
        /// crock of wooden spoons and a bottle on it, and on a rail under it a ladle, a sieve and a skillet.
        /// </summary>
        static void WarmingShelf(B.Model m)
        {
            float mid = (ShelfLeft + ShelfRight) * 0.5f, zMid = (ShelfFront + ShelfBack) * 0.5f;

            foreach (float x in new[] { ShelfLeft + 0.04f, ShelfRight - 0.04f })
                m.Box(IronDark, new Vector3(x, (HobTop + ShelfY) * 0.5f, ShelfBack - 0.03f), new Vector3(0.03f, ShelfY - HobTop, 0.022f), 0.006f);

            m.Box(IronTop, new Vector3(mid, ShelfY, zMid), new Vector3(ShelfRight - ShelfLeft, 0.024f, ShelfBack - ShelfFront), 0.008f, 2);
            m.Tube(Steel, new Vector3(ShelfLeft + 0.01f, ShelfY + 0.035f, ShelfFront + 0.008f), new Vector3(ShelfRight - 0.01f, ShelfY + 0.035f, ShelfFront + 0.008f),
                   0.006f, 6);
            foreach (float x in new[] { ShelfLeft + 0.02f, ShelfRight - 0.02f })
                m.Tube(Steel, new Vector3(x, ShelfY, ShelfFront + 0.008f), new Vector3(x, ShelfY + 0.038f, ShelfFront + 0.008f), 0.005f, 6);

            float top = ShelfY + 0.012f;
            Tin(m, new Vector3(-0.34f, top, 1.04f), 0.034f, 0.085f, SpiceRed);
            Tin(m, new Vector3(-0.255f, top, 1.05f), 0.032f, 0.075f, SpiceMustard);
            Tin(m, new Vector3(-0.17f, top, 1.04f), 0.034f, 0.09f, SpiceTeal);
            Crock(m, new Vector3(0f, top, 1.04f), 0.05f, 0.1f);
            UtensilCrock(m, new Vector3(0.2f, top, 1.04f));
            Bottle(m, new Vector3(0.44f, top, 1.04f), 0.035f, 0.2f, GlassGreen);

            float railY = ShelfY - 0.07f, railZ = ShelfBack - 0.05f;
            m.Tube(Steel, new Vector3(ShelfLeft + 0.04f, railY, railZ), new Vector3(ShelfRight - 0.04f, railY, railZ), 0.008f, 8);
            Ladle(m, new Vector3(-0.3f, railY, railZ));
            Sieve(m, new Vector3(-0.08f, railY, railZ));
            Skillet(m, new Vector3(0.36f, railY, railZ));
        }

        /// <summary>The stovepipe up from the back of the hob, banded, with its damper handle and a china-hat cap on three legs.</summary>
        static void Stovepipe(B.Model m)
        {
            var foot = new Vector3(PipeX, HobTop, PipeZ);
            m.Box(IronDark, foot + Vector3.up * 0.02f, new Vector3(0.19f, 0.04f, 0.19f), 0.012f, 2);
            m.Tube(IronDark, foot, new Vector3(PipeX, PipeTop, PipeZ), 0.065f, 14);
            foreach (float y in new[] { 1.05f, 1.5f })
                m.Tube(IronTrim, new Vector3(PipeX, y - 0.015f, PipeZ), new Vector3(PipeX, y + 0.015f, PipeZ), 0.073f, 14);

            m.Box(Steel, new Vector3(PipeX, 1.25f, PipeZ - 0.09f), new Vector3(0.018f, 0.018f, 0.07f), 0.005f);
            m.Box(Steel, new Vector3(PipeX, 1.25f, PipeZ - 0.125f), new Vector3(0.06f, 0.014f, 0.016f), 0.005f);

            for (int k = 0; k < 3; k++)
            {
                float a = k * 120f * Mathf.Deg2Rad;
                var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.055f;
                m.Tube(IronDark, new Vector3(PipeX, PipeTop, PipeZ) + o, new Vector3(PipeX, PipeTop + 0.06f, PipeZ) + o, 0.006f, 5);
            }

            m.Lathe((p, n) => IronTrim, new Vector3(PipeX, PipeTop + 0.05f, PipeZ), new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.13f, 0f), new Vector2(0.13f, 0f), new Vector2(0.13f, 0.012f), new Vector2(0.13f, 0.012f),
                new Vector2(0f, 0.08f),
            }, 16);
        }

        // --- The counter, the woodpile, the floor -------------------------------------------------

        /// <summary>
        /// The oak prep counter round the left: walnut legs and aprons under a board top, tomatoes being
        /// chopped on a board with the knife beside them, whole ones waiting, a bunch of herbs, a stack
        /// of bowls at the front end and pepper, salt and oil at the back; a sack and a crate of
        /// potatoes on the shelf underneath.
        /// </summary>
        static void Counter(B.Model m)
        {
            float cx = (CounterLeft + CounterRight) * 0.5f, cz = (CounterFront + CounterBack) * 0.5f;
            float w = CounterRight - CounterLeft, d = CounterBack - CounterFront;
            float under = CounterTop - 0.05f;

            foreach (float x in new[] { CounterLeft + 0.04f, CounterRight - 0.04f })
                foreach (float z in new[] { CounterFront + 0.04f, cz, CounterBack - 0.04f })
                    m.Box(B.Walnut, new Vector3(x, (PadTop + under) * 0.5f, z), new Vector3(0.056f, under - PadTop, 0.056f), 0.012f);

            foreach (float x in new[] { CounterLeft + 0.035f, CounterRight - 0.035f })
                m.Box(B.Walnut, new Vector3(x, under - 0.045f, cz), new Vector3(0.026f, 0.09f, d - 0.08f), 0.008f);
            foreach (float z in new[] { CounterFront + 0.035f, CounterBack - 0.035f })
                m.Box(B.Walnut, new Vector3(cx, under - 0.045f, z), new Vector3(w - 0.08f, 0.09f, 0.026f), 0.008f);

            const int boards = 5;
            for (int i = 0; i < boards; i++)
            {
                float x = CounterLeft + (i + 0.5f) * w / boards;
                Color c = Oak[B.Hash(i + 10) % Oak.Length] * (1f + 0.04f * B.Jitter(i + 11));
                m.Box(c, new Vector3(x, CounterTop - 0.025f, cz), new Vector3(w / boards - 0.004f, 0.05f, d + 0.02f), 0.01f);
            }

            for (int i = 0; i < 4; i++)
            {
                float x = CounterLeft + 0.07f + i * (w - 0.14f) / 3f;
                m.Box(B.Plank(i + 60), new Vector3(x, CounterShelf, cz), new Vector3(0.11f, 0.024f, d - 0.06f), 0.008f);
            }

            // Under the counter.
            float shelfTop = CounterShelf + 0.012f;
            B.Sack(m, new Vector3(cx, shelfTop, 0.74f), 0.13f, 0.34f, 0.03f, 12);
            var crateFoot = new Vector3(cx, shelfTop, 0.08f);
            var crate = new Vector3(0.42f, 0.16f, 0.36f);
            OpenCrate(m, crateFoot, crate, 4f, 21);
            Quaternion crateTurn = Quaternion.Euler(0f, 4f, 0f);
            int key = 0;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    key++;
                    var local = new Vector3(-0.12f + i * 0.12f + 0.012f * B.Jitter(key + 1), crate.y - 0.012f, -0.1f + j * 0.1f + 0.012f * B.Jitter(key + 2));
                    Ball(m, (p, n) => Potato * (1f + 0.07f * B.Jitter(key + 3)), crateFoot + crateTurn * local, new Vector3(0.05f, 0.034f, 0.038f),
                         Quaternion.Euler(0f, 60f * B.Jitter(key + 4), 0f), 10, 7);
                }
            }

            // On top.
            float top = CounterTop;
            for (int k = 0; k < 3; k++) Bowl(m, new Vector3(cx + 0.01f, top + k * 0.028f, -0.32f), 0.1f);

            Quaternion boardTurn = Quaternion.Euler(0f, 6f, 0f);
            m.Box(BoardOak, new Vector3(cx, top + 0.0125f, 0.2f), new Vector3(0.34f, 0.025f, 0.42f), 0.008f, boardTurn, 2);
            float onBoard = top + 0.025f;
            TomatoHalf(m, new Vector3(cx - 0.06f, onBoard, 0.12f), 0.045f, 1);
            TomatoHalf(m, new Vector3(cx + 0.05f, onBoard, 0.2f), 0.043f, 2);
            for (int k = 0; k < 8; k++)
            {
                var at = new Vector3(cx + 0.07f + 0.03f * B.Jitter(k + 200), onBoard + 0.011f, 0.04f + 0.035f * B.Jitter(k + 210));
                m.Box(k % 3 == 0 ? TomatoFlesh : TomatoRed, at, Vector3.one * 0.022f, 0.005f, Quaternion.Euler(0f, 40f * B.Jitter(k + 220), 0f));
            }

            Knife(m, new Vector3(cx - 0.04f, onBoard, 0.33f), 35f);

            Tomato(m, new Vector3(cx - 0.12f, top, 0.56f), 0.046f, 3);
            Tomato(m, new Vector3(cx - 0.035f, top, 0.62f), 0.044f, 4);
            Tomato(m, new Vector3(cx - 0.13f, top, 0.66f), 0.042f, 5);
            Herbs(m, new Vector3(cx + 0.1f, top, 0.6f));

            PepperMill(m, new Vector3(cx - 0.17f, top, 0.92f));
            Crock(m, new Vector3(cx - 0.02f, top, 0.96f), 0.06f, 0.12f);
            Bottle(m, new Vector3(cx + 0.15f, top, 0.92f), 0.04f, 0.26f, GlassOil);
        }

        /// <summary>Logs stacked three, two, one on a pair of iron rails beside the range, their cut ends out.</summary>
        static void Woodpile(B.Model m)
        {
            const float front = 0.45f, back = 1.04f;
            foreach (float z in new[] { front + 0.08f, back - 0.08f })
                m.Box(IronDark, new Vector3(1.03f, PadTop + 0.015f, z), new Vector3(0.3f, 0.03f, 0.03f), 0.006f);

            const float r = 0.045f;
            float y0 = PadTop + 0.03f + r;
            float[][] layers = { new[] { 0.94f, 1.03f, 1.12f }, new[] { 0.985f, 1.075f }, new[] { 1.03f } };
            int key = 0;
            for (int l = 0; l < layers.Length; l++)
            {
                foreach (float x in layers[l])
                {
                    key++;
                    float y = y0 + l * r * 1.7f;
                    Log(m, new Vector3(x, y, front + 0.02f * B.Jitter(key + 510)), new Vector3(x, y, back + 0.02f * B.Jitter(key + 520)),
                        r * (1f + 0.1f * B.Jitter(key + 500)), key);
                }
            }
        }

        /// <summary>A galvanised bucket of water, its handle laid back.</summary>
        static void Bucket(B.Model m)
        {
            var foot = new Vector3(1.02f, PadTop, 0.13f);
            const float r0 = 0.1f, r1 = 0.125f, h = 0.24f;

            m.Lathe((p, n) =>
            {
                float r = new Vector2(p.x - foot.x, p.z - foot.z).magnitude;
                float y = p.y - foot.y;
                return y > 0.12f && r < Mathf.Lerp(r0, r1, y / h) - 0.004f ? GalvanisedDark : Galvanised;
            }, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r0, 0f), new Vector2(r0, 0f), new Vector2(r1, h), new Vector2(r1 + 0.006f, h + 0.006f),
                new Vector2(r1 - 0.004f, h + 0.004f), new Vector2(r1 - 0.008f, h - 0.01f), new Vector2(r1 - 0.012f, h - 0.07f),
            }, 18);

            Disc(m, (p, n) => Water, foot + Vector3.up * (h - 0.05f), Vector3.up, r1 - 0.015f, 18);

            for (int s = -1; s <= 1; s += 2)
                m.Box(GalvanisedDark, foot + new Vector3(s * (r1 + 0.002f), h - 0.025f, 0f), new Vector3(0.012f, 0.03f, 0.024f), 0.004f);

            Quaternion back = Quaternion.Euler(35f, 0f, 0f);
            Vector3 Arc(float t)
            {
                float a = t * Mathf.PI;
                return foot + Vector3.up * (h - 0.025f) + back * new Vector3(-Mathf.Cos(a) * (r1 + 0.008f), Mathf.Sin(a) * 0.13f, 0f);
            }

            const int steps = 7;
            for (int k = 0; k < steps; k++)
                m.Tube(GalvanisedDark, Arc(k / (float)steps), Arc((k + 1) / (float)steps), 0.005f, 5);
        }

        /// <summary>An open crate of tomatoes on the floor at the front corner, heaped above its rim.</summary>
        static void TomatoCrate(B.Model m)
        {
            var foot = new Vector3(0.95f, PadTop, -0.33f);
            var size = new Vector3(0.4f, 0.19f, 0.32f);
            const float yaw = -8f;
            OpenCrate(m, foot, size, yaw, 11);

            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            int key = 0;
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    key++;
                    var local = new Vector3(-0.135f + i * 0.09f + 0.01f * B.Jitter(key + 1), size.y - 0.072f, -0.09f + j * 0.09f + 0.01f * B.Jitter(key + 3));
                    Tomato(m, foot + turn * local, 0.046f, key + 20);
                }
            }

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    key++;
                    var local = new Vector3(-0.09f + i * 0.09f + 0.012f * B.Jitter(key + 5), size.y - 0.01f, -0.045f + j * 0.09f + 0.012f * B.Jitter(key + 7));
                    Tomato(m, foot + turn * local, 0.045f, key + 20);
                }
            }
        }

        // --- Props --------------------------------------------------------------------------------

        /// <summary>A tomato sitting on <paramref name="foot"/>: a glossy red ball a little flattened, darker underneath, a green star of calyx on top.</summary>
        internal static void Tomato(B.Model m, Vector3 foot, float r, int key)
        {
            Color skin = TomatoRed * (1f + 0.06f * B.Jitter(key));
            Vector3 centre = foot + Vector3.up * (r * 0.86f);
            Ball(m, (p, n) => Color.Lerp(skin * 0.86f, skin * 1.06f, Smooth((p.y - foot.y) / (r * 1.7f))), centre, new Vector3(r, r * 0.86f, r),
                 Quaternion.Euler(0f, key * 37f, 0f), 14, 9);

            Vector3 top = centre + Vector3.up * (r * 0.86f - 0.002f);
            for (int k = 0; k < 3; k++)
            {
                Quaternion q = Quaternion.Euler(0f, k * 60f + key * 17f, 0f);
                m.Box(Calyx, top, new Vector3(r * 0.9f, r * 0.1f, r * 0.16f), r * 0.04f, q);
            }
        }

        /// <summary>Half a tomato, cut face up: red skin round pale flesh and a paler core.</summary>
        static void TomatoHalf(B.Model m, Vector3 foot, float r, int key)
        {
            float h = r * 0.8f;
            Color skin = TomatoRed * (1f + 0.05f * B.Jitter(key));
            m.Lathe((p, n) =>
            {
                if (n.y < 0.8f) return skin;
                float d = new Vector2(p.x - foot.x, p.z - foot.z).magnitude / r;
                return d > 0.86f ? skin : d > 0.42f ? TomatoFlesh : TomatoCore;
            }, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r * 0.55f, 0.004f), new Vector2(r * 0.9f, h * 0.4f), new Vector2(r, h), new Vector2(r, h),
                new Vector2(r * 0.88f, h), new Vector2(r * 0.84f, h), new Vector2(r * 0.44f, h), new Vector2(r * 0.4f, h), new Vector2(0f, h),
            }, 16);
        }

        static void Knife(B.Model m, Vector3 at, float yaw)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            m.Box(Steel, at + q * new Vector3(0.05f, 0.004f, 0f), new Vector3(0.16f, 0.006f, 0.032f), 0.0025f, q);
            m.Box(B.Walnut, at + q * new Vector3(-0.08f, 0.009f, 0f), new Vector3(0.1f, 0.018f, 0.022f), 0.006f, q);
        }

        /// <summary>A bunch of herbs: stems tied with twine at one end, the leaves fanning out at the other.</summary>
        static void Herbs(B.Model m, Vector3 foot)
        {
            for (int k = 0; k < 4; k++)
            {
                Vector3 a = foot + new Vector3(-0.06f + 0.006f * k, 0.008f, -0.06f + 0.004f * k);
                Vector3 b = foot + new Vector3(0.02f + 0.015f * B.Jitter(k + 70), 0.012f, 0.04f + 0.02f * B.Jitter(k + 71));
                m.Tube(HerbLight, a, b, 0.004f, 5);
            }

            m.Tube(B.Twine, foot + new Vector3(-0.042f, 0.009f, -0.047f), foot + new Vector3(-0.03f, 0.009f, -0.035f), 0.011f, 8);

            for (int k = 0; k < 7; k++)
            {
                Color leaf = k % 2 == 0 ? Herb : HerbLight;
                var at = foot + new Vector3(0.03f + 0.04f * B.Jitter(k + 80), 0.016f, 0.06f + 0.035f * B.Jitter(k + 81));
                Ball(m, (p, n) => leaf, at, new Vector3(0.026f, 0.01f, 0.017f), Quaternion.Euler(0f, 50f * B.Jitter(k + 82), 0f), 8, 5);
            }
        }

        /// <summary>An enamel bowl, cream with a navy rim, standing on <paramref name="foot"/>.</summary>
        internal static void Bowl(B.Model m, Vector3 foot, float r)
        {
            m.Lathe((p, n) => p.y - foot.y > r * 0.5f ? Navy : Cream, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r * 0.45f, 0f), new Vector2(r * 0.45f, 0f), new Vector2(r * 0.5f, 0.008f), new Vector2(r * 0.82f, r * 0.3f),
                new Vector2(r * 0.98f, r * 0.56f), new Vector2(r, r * 0.62f), new Vector2(r * 0.93f, r * 0.64f), new Vector2(r * 0.86f, r * 0.55f),
                new Vector2(r * 0.6f, r * 0.28f), new Vector2(0f, r * 0.2f),
            }, 20);
        }

        /// <summary>A glazed crock with a wooden lid.</summary>
        internal static void Crock(B.Model m, Vector3 foot, float r, float h)
        {
            m.Lathe((p, n) => p.y - foot.y > h * 0.89f ? Oak[0] : CrockGlaze, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r * 0.8f, 0f), new Vector2(r * 0.8f, 0f), new Vector2(r, h * 0.25f), new Vector2(r * 0.98f, h * 0.75f),
                new Vector2(r * 0.82f, h * 0.88f), new Vector2(r * 0.86f, h * 0.9f), new Vector2(r * 0.86f, h), new Vector2(r * 0.86f, h), new Vector2(0f, h),
            }, 16);
        }

        /// <summary>A crock with three wooden spoons standing in it, heads up.</summary>
        static void UtensilCrock(B.Model m, Vector3 foot)
        {
            const float r = 0.045f, h = 0.09f;
            m.Lathe((p, n) => n.y > 0.8f && p.y - foot.y > h * 0.9f ? Soot : CrockGlaze, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r * 0.85f, 0f), new Vector2(r * 0.85f, 0f), new Vector2(r, h * 0.3f), new Vector2(r, h * 0.9f),
                new Vector2(r * 1.06f, h), new Vector2(r * 1.06f, h), new Vector2(r * 0.85f, h), new Vector2(r * 0.85f, h), new Vector2(0f, h * 0.92f),
            }, 14);

            Vector3[] leans = { new Vector3(-0.035f, 0.22f, -0.01f), new Vector3(0.01f, 0.25f, 0.025f), new Vector3(0.04f, 0.21f, -0.02f) };
            for (int k = 0; k < leans.Length; k++)
            {
                Vector3 a = foot + new Vector3(leans[k].x * 0.2f, h * 0.5f, leans[k].z * 0.2f);
                Vector3 b = foot + leans[k];
                m.Tube(SpoonWood, a, b, 0.006f, 6);
                Ball(m, (p, n) => SpoonWood * 0.94f, b + (b - a).normalized * 0.025f, new Vector3(0.018f, 0.03f, 0.008f),
                     Quaternion.FromToRotation(Vector3.up, (b - a).normalized), 10, 6);
            }
        }

        /// <summary>A tin with a dark lid and a coloured body.</summary>
        internal static void Tin(B.Model m, Vector3 foot, float r, float h, Color label)
        {
            m.Lathe((p, n) =>
            {
                float y = p.y - foot.y;
                return y > h - 0.013f || y < 0.009f ? SteelDark : label;
            }, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r, 0f), new Vector2(r, 0f), new Vector2(r, 0.008f), new Vector2(r, 0.01f), new Vector2(r, h - 0.014f),
                new Vector2(r, h - 0.012f), new Vector2(r + 0.003f, h - 0.01f), new Vector2(r + 0.003f, h), new Vector2(r + 0.003f, h), new Vector2(0f, h),
            }, 14);
        }

        internal static void Bottle(B.Model m, Vector3 foot, float r, float h, Color glass)
        {
            m.Lathe((p, n) => p.y - foot.y > h * 0.93f ? Cork : glass, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r, 0f), new Vector2(r, 0f), new Vector2(r, h * 0.58f), new Vector2(r * 0.7f, h * 0.72f),
                new Vector2(r * 0.34f, h * 0.8f), new Vector2(r * 0.32f, h * 0.92f), new Vector2(r * 0.36f, h * 0.94f), new Vector2(r * 0.34f, h),
                new Vector2(0f, h),
            }, 14);
        }

        internal static void PepperMill(B.Model m, Vector3 foot)
        {
            const float r = 0.032f, h = 0.16f;
            m.Lathe((p, n) => B.Walnut, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r, 0f), new Vector2(r, 0f), new Vector2(r * 0.8f, h * 0.4f), new Vector2(r * 0.92f, h * 0.55f),
                new Vector2(r * 0.76f, h * 0.75f), new Vector2(r * 0.5f, h * 0.9f), new Vector2(r * 0.3f, h), new Vector2(0f, h * 1.02f),
            }, 12);
        }

        /// <summary>An open crate: two slats a side round a dark inside, corner posts, no lid.</summary>
        internal static void OpenCrate(B.Model m, Vector3 foot, Vector3 size, float yaw, int key)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 h = size * 0.5f;
            Color wood = B.HoneyLight * (1f + 0.05f * B.Jitter(key));

            m.Box(B.WalnutDark, foot + turn * new Vector3(0f, size.y * 0.35f, 0f), new Vector3(size.x - 0.03f, size.y * 0.7f, size.z - 0.03f), 0.01f, turn);

            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = 0; k < 2; k++)
                {
                    float y = size.y * (k == 0 ? 0.25f : 0.75f);
                    Color c = wood * (1f + 0.05f * B.Jitter(key * 7 + k + side * 3));
                    m.Box(c, foot + turn * new Vector3(0f, y, side * h.z), new Vector3(size.x, size.y * 0.38f, 0.022f), 0.007f, turn);
                    m.Box(c, foot + turn * new Vector3(side * h.x, y, 0f), new Vector3(0.022f, size.y * 0.38f, size.z - 0.02f), 0.007f, turn);
                }
            }

            for (int k = 0; k < 4; k++)
            {
                float x = (k & 1) == 0 ? -h.x : h.x, z = (k & 2) == 0 ? -h.z : h.z;
                m.Box(B.Walnut, foot + turn * new Vector3(x, h.y, z), new Vector3(0.034f, size.y, 0.034f), 0.008f, turn);
            }
        }

        /// <summary>A log from <paramref name="from"/> to <paramref name="to"/>: bark round it, pale cut ends darkening to the bark.</summary>
        static void Log(B.Model m, Vector3 from, Vector3 to, float r, int key)
        {
            Vector3 axis = (to - from).normalized;
            Color bark = Bark * (1f + 0.07f * B.Jitter(key));
            m.Tube((p, n) =>
            {
                if (Mathf.Abs(Vector3.Dot(n, axis)) < 0.8f) return bark;
                Vector3 d = p - from;
                float off = (d - axis * Vector3.Dot(d, axis)).magnitude;
                return Color.Lerp(LogEnd, LogRing, off / r);
            }, from, to, r, r, 10);
        }

        internal static void Hook(B.Model m, Vector3 at) => m.Tube(Steel, at, at - Vector3.up * 0.032f, 0.004f, 5);

        internal static void Ladle(B.Model m, Vector3 hook)
        {
            Hook(m, hook);
            Vector3 top = hook - Vector3.up * 0.03f, bottom = hook - Vector3.up * 0.3f;
            m.Tube(Steel, top, bottom, 0.007f, 6);
            Ball(m, (p, n) => n.z < -0.3f ? SteelDark : Steel, bottom + new Vector3(0f, -0.032f, -0.012f), new Vector3(0.042f, 0.036f, 0.028f),
                 Quaternion.identity, 12, 8);
        }

        static void Sieve(B.Model m, Vector3 hook)
        {
            Hook(m, hook);
            Vector3 centre = hook - Vector3.up * 0.2f;
            m.Tube(Steel, hook - Vector3.up * 0.03f, centre + Vector3.up * 0.07f, 0.006f, 6);
            m.Tube((p, n) => Mathf.Abs(n.z) > 0.8f ? SieveMesh : Steel, centre + Vector3.forward * 0.012f, centre - Vector3.forward * 0.012f, 0.07f, 0.07f,
                   18);
        }

        static void Skillet(B.Model m, Vector3 hook)
        {
            Hook(m, hook);
            Vector3 centre = hook - Vector3.up * 0.255f;
            m.Tube(IronDark, hook - Vector3.up * 0.03f, centre + Vector3.up * 0.08f, 0.011f, 8);
            m.Tube((p, n) => n.z < -0.8f ? IronTop : IronDark, centre + Vector3.forward * 0.015f, centre - Vector3.forward * 0.015f, 0.085f, 0.08f, 18);
        }

        // --- The moving parts ---------------------------------------------------------------------

        /// <summary>
        /// Tomato soup, in its own space with the middle of its surface at the origin: a shallow dome
        /// of red, darker at the edge, with a spiral of cream in it and herbs floating on it - so that
        /// turning it shows the soup being stirred.
        /// </summary>
        static B.Model Soup()
        {
            var m = new B.Model();
            const float R = PotInner + 0.003f;
            const float Dome = 0.003f;

            m.Surface((p, n) =>
            {
                float rr = new Vector2(p.x, p.z).magnitude / R;
                float a = Mathf.Atan2(p.z, p.x) / (Mathf.PI * 2f);
                float swirl = Mathf.Cos((a + rr * 1.35f) * Mathf.PI * 2f);
                float cream = Smooth(Mathf.InverseLerp(0.8f, 0.97f, swirl)) * Mathf.Clamp01(Mathf.Sin(Mathf.Clamp01((rr - 0.08f) / 0.8f) * Mathf.PI) * 1.4f);
                Color c = Color.Lerp(SoupRed, SoupDeep, Smooth((rr - 0.7f) / 0.3f));
                return Color.Lerp(c, SoupCream, cream * 0.85f);
            }, 64, 14, (u, v) =>
            {
                float a = -u * Mathf.PI * 2f;
                float r = v * R;
                return new Vector3(Mathf.Cos(a) * r, Dome * (1f - v * v), Mathf.Sin(a) * r);
            }, true);

            for (int k = 0; k < 8; k++)
            {
                float a = k * 2.4f + 0.3f, d = 0.05f + 0.15f * (B.Hash(k + 900) % 100 / 100f);
                float v = d / R;
                var at = new Vector3(Mathf.Cos(a) * d, Dome * (1f - v * v) + 0.0005f, Mathf.Sin(a) * d);
                m.Box(k % 3 == 0 ? HerbLight : Herb, at, new Vector3(0.028f, 0.004f, 0.016f), 0.0018f, Quaternion.Euler(0f, k * 47f, 0f));
            }

            return m;
        }

        /// <summary>A bubble swelling out of the soup: a low dome, lighter on top, its foot below the surface.</summary>
        static B.Model Bubble()
        {
            var m = new B.Model();
            m.Lathe((p, n) => Color.Lerp(SoupRed * 1.06f, SoupBubbleTop, Smooth(p.y / 0.016f)), Vector3.zero, new List<Vector2>
            {
                new Vector2(0.024f, -0.006f), new Vector2(0.0235f, 0.001f), new Vector2(0.02f, 0.008f), new Vector2(0.013f, 0.0135f),
                new Vector2(0.006f, 0.016f), new Vector2(0f, 0.0168f),
            }, 12);
            return m;
        }

        /// <summary>
        /// The wooden spoon standing in the soup, in a space whose origin is the middle of the pot at
        /// the soup's surface, so turning it about its up axis stirs. Stained red where it is in the soup.
        /// </summary>
        static B.Model Spoon()
        {
            var m = new B.Model();
            var bottom = new Vector3(0.11f, -0.07f, 0f);
            var top = new Vector3(0.27f, 0.42f, 0.03f);
            m.Tube((p, n) => Color.Lerp(SpoonWet, SpoonWood, Smooth((p.y - 0.005f) / 0.06f)), bottom, top, 0.013f, 0.016f, 10);
            Ball(m, (p, n) => SpoonWood, top + (top - bottom).normalized * 0.004f, new Vector3(0.018f, 0.018f, 0.018f), Quaternion.identity, 8, 6);
            return m;
        }

        /// <summary>The saucepan's lid, its origin the middle of the pan's rim: a shallow steel dome with a black knob.</summary>
        static B.Model Lid()
        {
            var m = new B.Model();
            m.Lathe((p, n) => Color.Lerp(PanSteel, PanSteelLight, Smooth(p.y / 0.035f)), Vector3.zero, new List<Vector2>
            {
                new Vector2(0.142f, -0.008f), new Vector2(0.15f, -0.002f), new Vector2(0.15f, 0.002f), new Vector2(0.135f, 0.012f), new Vector2(0.1f, 0.026f),
                new Vector2(0.05f, 0.036f), new Vector2(0f, 0.039f),
            }, 24);
            m.Lathe((p, n) => Bakelite, new Vector3(0f, 0.034f, 0f), new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.014f, 0f), new Vector2(0.014f, 0f), new Vector2(0.018f, 0.014f), new Vector2(0.016f, 0.024f),
                new Vector2(0f, 0.027f),
            }, 12);
            return m;
        }

        /// <summary>
        /// Half the tongues of flame licking up round the foot of the stockpot, in the pot's own space
        /// on the ring plate: <paramref name="set"/> 0 the taller ones, 1 the shorter ones between them
        /// and a little further out, drawn in the fire's yellow. Each is flattened against the pot and
        /// curls in toward it at the tip: short round ones, seen from above, read as beads round the pot.
        /// </summary>
        static B.Model PotFlames(int set)
        {
            var m = new B.Model();
            const int tongues = 16;
            for (int i = set; i < tongues; i += 2)
            {
                float a = (i + 0.3f * B.Jitter(i + 40)) / tongues * Mathf.PI * 2f;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float h = (set == 0 ? 0.13f : 0.085f) * (1f + 0.2f * B.Jitter(i + 50));
                float at = set == 0 ? PotRadius + 0.015f : PotRadius + 0.026f;
                Tongue(m, radial * at, h, h * 0.3f, -radial * 0.014f, 0.55f, Quaternion.LookRotation(radial));
            }

            return m;
        }

        /// <summary>The fire behind the grate, in a space on the ember bed: <paramref name="set"/> 0 the tall orange tongues, 1 the yellow ones in front.</summary>
        static B.Model FireboxFlames(int set)
        {
            var m = new B.Model();
            float[] xs = set == 0 ? new[] { -0.09f, -0.045f, 0f, 0.045f, 0.09f } : new[] { -0.068f, -0.022f, 0.024f, 0.07f };
            float[] hs = set == 0 ? new[] { 0.07f, 0.095f, 0.11f, 0.09f, 0.065f } : new[] { 0.05f, 0.062f, 0.058f, 0.045f };
            float z = set == 0 ? 0.004f : -0.006f;
            for (int k = 0; k < xs.Length; k++)
                Tongue(m, new Vector3(xs[k], 0f, z), hs[k], hs[k] * 0.32f, new Vector3(xs[k] * 0.25f, 0f, 0f), 0.35f);
            return m;
        }

        /// <summary>
        /// A tongue of flame standing on <paramref name="foot"/>: round at the foot, drawn up to a point
        /// <paramref name="h"/> above it, its tip carried over by <paramref name="lean"/>; <paramref name="flat"/>
        /// squashes it along <paramref name="turn"/>'s forward (front to back when left unturned).
        /// </summary>
        static void Tongue(B.Model m, Vector3 foot, float h, float r, Vector3 lean, float flat, Quaternion? turn = null)
        {
            Quaternion q = turn ?? Quaternion.identity;
            var flame = new SmoothMesh(1);
            flame.Surface(0, 10, 8, (u, v) =>
            {
                float a = u * Mathf.PI * 2f;

                // Floored at zero: sin(pi) comes out a hair below it at the tip, and a fractional power of that is NaN.
                float width = r * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Lerp(0.08f, 1f, v))), 0.8f) * (1f - 0.55f * v);
                return foot + q * new Vector3(Mathf.Cos(a) * width, v * h, Mathf.Sin(a) * width * flat) + lean * (v * v);
            }, wrapU: true);
            m.Add(flame, Color.white);
        }

        /// <summary>
        /// Steam rolling off the stockpot: soft puffs rising, swelling and drifting as they fade, a
        /// plume about a metre tall. Puffs any smaller were lost from the game camera's usual distance.
        /// The animation sets how much.
        /// </summary>
        static ParticleSystem Steam(Transform parent, Material material)
        {
            ParticleSystem ps = FX.NewSystem(parent, "Steam", material, ParticleSystemRenderMode.Billboard);
            ps.transform.localPosition = new Vector3(PotX, SoupY + 0.03f, PotZ);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.9f, 2.6f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.24f, 0.36f);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.62f), new Color(0.96f, 0.96f, 0.96f, 0.8f));
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.15f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            FX.Rise(ps, 0.32f, 0.5f);
            FX.Grow(ps, FX.Curve((0f, 0.5f), (0.4f, 1.1f), (1f, 1.9f)));
            FadeInOut(ps, 0.15f, 0.55f);
            FX.Spin(ps, -25f, 25f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.1f;
            noise.frequency = 0.7f;
            noise.scrollSpeed = 0.25f;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            return ps;
        }

        /// <summary>Little puffs escaping round the saucepan's rim, given out one or two at a time as the lid rattles.</summary>
        static ParticleSystem LidSteam(Transform parent, Material material)
        {
            ParticleSystem ps = FX.NewSystem(parent, "LidSteam", material, ParticleSystemRenderMode.Billboard);
            ps.transform.localPosition = new Vector3(PanX, PanTop + 0.005f, PanZ);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.13f);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.5f), new Color(1f, 1f, 1f, 0.7f));
            main.maxParticles = 16;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = PanRadius;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            FX.Rise(ps, 0.3f, 0.5f);
            FX.Grow(ps, FX.Curve((0f, 0.5f), (1f, 1.6f)));
            FadeInOut(ps, 0.1f, 0.4f);
            return ps;
        }

        static void FadeInOut(ParticleSystem ps, float inUntil, float outFrom)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inUntil), new GradientAlphaKey(0.8f, outFrom), new GradientAlphaKey(0f, 1f) });

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        // --- Shapes and materials -----------------------------------------------------------------

        /// <summary>A ball (or an egg, by <paramref name="radii"/>): a tomato, a potato, a leaf.</summary>
        internal static void Ball(B.Model m, Func<Vector3, Vector3, Color> paint, Vector3 centre, Vector3 radii, Quaternion turn, int around, int rings)
        {
            m.Surface(paint, around, rings, (u, v) =>
            {
                float th = v * Mathf.PI, ph = u * Mathf.PI * 2f;
                float s = Mathf.Sin(th);
                return centre + turn * new Vector3(s * Mathf.Cos(ph) * radii.x, -Mathf.Cos(th) * radii.y, s * Mathf.Sin(ph) * radii.z);
            }, true);
        }

        /// <summary>A flat disc of <paramref name="radius"/> facing <paramref name="normal"/>.</summary>
        internal static void Disc(B.Model m, Func<Vector3, Vector3, Color> paint, Vector3 centre, Vector3 normal, float radius, int steps = 20)
        {
            normal.Normalize();
            Vector3 right = Vector3.Cross(Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up, normal).normalized;
            Vector3 up = Vector3.Cross(normal, right);
            m.Surface(paint, steps, 2, (u, v) =>
            {
                float a = u * Mathf.PI * 2f;
                return centre + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * (radius * v);
            }, true);
        }

        /// <summary>Unlit in one colour, for fire and embers: they glow the same however dark the kitchen is.</summary>
        static Material UnlitMaterial(string name, Color colour)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("Shader Universal Render Pipeline/Unlit is missing.");

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

        /// <summary>Soft white puffs on the planting burst's cloud texture, taking part of the kitchen's light.</summary>
        static Material SteamMaterial()
        {
            Shader shader = Shader.Find("LastOrders/SoftParticle");
            if (shader == null) throw new InvalidOperationException("Shader LastOrders/SoftParticle is missing.");

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PuffTexturePath);
            if (texture == null)
            {
                ContentGenerator.EnsureFolder(FX.Folder);
                texture = FX.Paint("Particle_Puff", FX.Puff);
            }

            string path = $"{Folder}/SoupKitchen_Steam.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "SoupKitchen_Steam" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Additive", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_Lit", 0.55f);
            material.SetFloat("_DepthPull", 0.2f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        internal static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
