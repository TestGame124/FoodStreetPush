using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using B = LastOrders.EditorTools.BuildingKit;
using G = LastOrders.EditorTools.GriddleStationModel;
using K = LastOrders.EditorTools.SoupKitchenModel;
using T = LastOrders.EditorTools.KitchenTools;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The bread station (pfBreadStove) as a bakery corner inside the diner's kitchen, in the game's
    /// smooth low-poly style: a two-deck bakery oven in diner-red enamel with steel doors, loaves
    /// browning behind its lit windows, on a steel stand with sheet pans and proving baskets on the
    /// shelf underneath and a flue up from the back; a baker's bench on the left with a ball of dough
    /// on a dusting of flour, a rolling pin, a tray of shaped rolls waiting to go in and a basket of
    /// finished loaves at the front end; a rack of baking trays on the right with rolls and baguettes
    /// cooling at the top and dough proving lower down, a peel hung on its side; an open flour bin
    /// and a basket of baguettes in front of it - all on a pad of crust-and-crumb tiles that
    /// marks the corner out from the kitchen floor, with a slatted mat where the baker stands.
    ///
    /// It takes the place of the wood-fired dome oven, which read as something built out of doors:
    /// the diner is roofless only because the game is seen from above, and its kitchen is indoors.
    ///
    /// One mesh, painted in its vertices on LastOrders/Stylized as <see cref="BuildingKit"/> builds.
    /// What moves while the station bakes is its own part: the heat behind the oven windows and the
    /// panel lamps (unlit, lit up by a <see cref="StationHeatGlow"/> with heat off the flue), the
    /// rolling pin and the sheet of dough it rolls out, and the ball of dough being kneaded
    /// (<see cref="KitchenToolMotion"/>s). The station's earlier models and the painted floor shadows
    /// that went with them are switched off, not deleted; the collider is fitted to the corner, so a
    /// tap anywhere on it reaches the station, and the centre point is set at the front of the pad.
    /// The old build ghost (BreadStoveVisuals) shows the corner too; the shop uses its own
    /// (<see cref="KitchenShopSetup"/>).
    ///
    /// Its front faces -Z, out into the dining room, as the station's does.
    /// </summary>
    public static class BakeryCornerModel
    {
        const string PrefabPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Stoves/BreadStove/pfBreadStove.prefab";

        /// <summary>What build mode shows, tinted, while a bread station is being placed.</summary>
        const string GhostPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Stoves/BreadStove/BreadStoveVisuals.prefab";

        const string Folder = "Assets/Game Assets/Content/LastOrders/Kitchen/" + EnvKit.Style + "/BakeryCorner";

        internal const string ModelName = "BakeryCorner";

        /// <summary>What the corner replaces, switched off: the wood-fired oven, the prototype stove before it, and the painted floor shadows under them.</summary>
        static readonly string[] OldParts = { "BreadOven", "Stove", "Square (2)", "Square (3)" };

        const string OldGhostModel = "Stove_Prefab";

        /// <summary>
        /// Where the corner's middle stands in the station's space. The station is placed on the build
        /// grid like everything the shop sells, its origin the near left corner of a 3 x 2 cell
        /// footprint at the grid's height: the pad is centred across the footprint, its back a hand
        /// short of the footprint's back edge, and stands on the diner's floor, 0.035 up.
        /// </summary>
        internal static readonly Vector3 Place = new Vector3(1.5f, 0.035f, 0.8f);

        // --- Measurements (metres; front toward -Z) ------------------------------------------------

        /// <summary>The tiled pad the corner stands on. Its back edge is 0.1 short of the wall.</summary>
        const float PadLeft = -1.2f, PadRight = 1.2f, PadFront = -0.6f, PadBack = 1.15f, PadTop = 0.047f;

        /// <summary>The oven's body, the face its doors stand out from, and the stand it sits on.</summary>
        const float OvenLeft = -0.48f, OvenRight = 0.72f, OvenBack = 1.12f, OvenFace = 0.4f;
        const float StandTop = 0.3f, StandShelf = 0.1f, OvenTop = 1.02f;

        /// <summary>The two decks, bottom up: where the first starts, how far apart they are, how tall each is.</summary>
        const float DeckBottom = 0.33f, DeckPitch = 0.32f, DeckHeight = 0.3f;

        /// <summary>A deck's door, and the window in it: its sill and head measured up from the deck's bottom.</summary>
        const float DoorLeft = -0.4f, DoorRight = 0.4f, DoorFront = 0.32f;
        const float WindowLeft = -0.3f, WindowRight = 0.3f, WindowSill = 0.06f, WindowHead = 0.19f;

        /// <summary>The steel face behind the doors, and the glow of the hot deck seen through a window just in front of it.</summary>
        const float SteelFace = OvenFace - 0.024f;
        const float GlowZ = SteelFace - 0.002f;

        /// <summary>The control panel right of the doors, one block of it a deck.</summary>
        const float PanelLeft = 0.47f, PanelRight = 0.68f, PanelFront = OvenFace - 0.03f;

        const float FlueX = 0.5f, FlueZ = 0.92f, FlueTop = 1.6f;

        const float BenchLeft = -1.17f, BenchRight = -0.56f, BenchFront = -0.5f, BenchBack = 1.08f, BenchTop = 0.86f, BenchShelf = 0.22f;

        const float RackLeft = 0.8f, RackRight = 1.16f, RackFront = 0.42f, RackBack = 1.1f, RackTop = 1.42f;

        /// <summary>The rack's trays, bottom up.</summary>
        static readonly float[] TrayHeights = { 0.2f, 0.42f, 0.64f, 0.86f, 1.08f, 1.3f };

        const float BinX = 0.98f, BinZ = 0.06f, BinWidth = 0.34f, BinDepth = 0.36f, BinHeight = 0.48f;

        /// <summary>The rolling pin's barrel, and the sheet of dough it rolls out on the bench.</summary>
        const float PinRadius = 0.031f, SheetThick = 0.012f;

        // --- The palette ---------------------------------------------------------------------------

        /// <summary>Crumb and crust: an oatmeal and a toasted brown, checked, apart from the soup corner's brick red and the salad bar's sage.</summary>
        static readonly Color Crumb = B.Hex("968B78");
        static readonly Color CrustTile = B.Hex("6F5D4C");
        static readonly Color Grout = B.Hex("6B6560");

        /// <summary>
        /// The diner's red, toned down for a surface this big: in the turret's and the toaster's 8E3B33
        /// the oven's lit top and front came out a saturated pillar-box red.
        /// </summary>
        static readonly Color Enamel = B.Hex("86463E");

        /// <summary>Stainless, painted darker than the soup range's small steel parts: broad doors facing the light in those went white.</summary>
        static readonly Color Steel = B.Hex("7E8489");
        static readonly Color SteelLight = B.Hex("8B9196");
        static readonly Color SteelDark = B.Hex("5E6469");

        /// <summary>For the bright small parts: a handle, a bead, a dial's rim.</summary>
        static readonly Color Chrome = B.Hex("A3A9AD");
        static readonly Color Panel = B.Hex("2B2D30");
        static readonly Color Rubber = B.Hex("2A2C2F");
        static readonly Color Soot = B.Hex("1F1C1B");
        static readonly Color DeckStone = B.Hex("4A403A");
        static readonly Color DialFace = B.Hex("B9B2A2");
        static readonly Color Cream = B.Hex("AFA898");

        /// <summary>The bench's maple top, a shade darker than the furniture's so a dusting of flour shows on it.</summary>
        static readonly Color[] Maple = { B.Hex("A48A68"), B.Hex("9C8262"), B.Hex("AA9070"), B.Hex("977D5E") };
        static readonly Color[] Slats = { B.Hex("B39B7A"), B.Hex("AA9171"), B.Hex("BBA383"), B.Hex("A58C6C") };
        static readonly Color PinWood = B.Hex("B59B76");
        static readonly Color Wicker = B.Hex("A1855F");
        static readonly Color WickerDark = B.Hex("7E6446");

        /// <summary>
        /// Flour, dough and crust are painted well short of how pale they look: lying at the bench's
        /// height facing the sun they come out about 1.4 times as bright, and paler than this (BAB5AA
        /// flour, C1B193 dough) clipped to white.
        /// </summary>
        static readonly Color Flour = B.Hex("A19D93");
        static readonly Color Dough = B.Hex("A29276");
        static readonly Color DoughTop = B.Hex("AFA083");
        static readonly Color Crust = B.Hex("8E643A");
        static readonly Color CrustTop = B.Hex("A0784A");
        static readonly Color Score = B.Hex("D3BB8F");

        [MenuItem("Last Orders/Kitchen/Build Bakery Corner (pfBreadStove)", priority = 97)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This builds the bakery corner - deck oven, baker's bench, tray rack, flour bin, tiled pad - and puts it into " +
                "pfBreadStove in place of the wood-fired oven, which is switched off with the prototype stove and its painted " +
                "floor shadows, not deleted.\n\nWhile the station bakes, the oven heats up, its lamps light, the pin rolls out dough " +
                "and the dough is kneaded.\n\nThe station's components are kept; its collider and centre point are fitted to the corner.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        /// <summary>Builds the corner and puts it into the prefab and the build ghost, with no dialogs.</summary>
        public static string Apply()
        {
            ContentGenerator.EnsureFolder(Folder);

            Material painted = B.PaintedMaterial();
            Material glow = GlowMaterial();
            var heat = new B.Model();
            var lamps = new B.Model();
            Mesh corner = Corner(heat, lamps).Save($"{Folder}/BakeryCorner.asset", "BakeryCorner");
            Material[] materials = corner.subMeshCount > 1 ? new[] { painted, glow } : new[] { painted };
            Mesh heatMesh = heat.Save($"{Folder}/BakeryCorner_OvenHeat.asset", "BakeryCorner_OvenHeat");
            Mesh lampMesh = lamps.Save($"{Folder}/BakeryCorner_Lamps.asset", "BakeryCorner_Lamps");
            Mesh pinMesh = T.RollingPin(PinRadius).Save($"{Folder}/BakeryCorner_RollingPin.asset", "BakeryCorner_RollingPin");
            Mesh sheetMesh = T.DoughSheet(0.12f, 0.08f, SheetThick).Save($"{Folder}/BakeryCorner_DoughSheet.asset", "BakeryCorner_DoughSheet");
            Mesh ballMesh = T.DoughBall(new Vector3(0.085f, 0.036f, 0.075f)).Save($"{Folder}/BakeryCorner_Dough.asset", "BakeryCorner_Dough");

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                var off = new List<string>();
                foreach (string name in OldParts)
                {
                    Transform old = root.transform.Find(name);
                    if (old == null || !old.gameObject.activeSelf) continue;
                    old.gameObject.SetActive(false);
                    off.Add(name);
                }

                Transform previous = root.transform.Find(ModelName);
                if (previous != null) Object.DestroyImmediate(previous.gameObject);

                var go = new GameObject(ModelName);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Place;
                B.Show(go, corner, materials);
                Working(go.transform, painted, glow, heatMesh, lampMesh, pinMesh, sheetMesh, ballMesh);

                Transform centre = root.transform.Find("CenterPoint");
                if (centre != null) centre.localPosition = new Vector3(Place.x, 0.5f, Place.z + PadFront);

                string colliderNote = FitCollider(root);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return $"[Last Orders] Bakery corner built: {corner.vertexCount} vertices, one renderer in pfBreadStove" +
                       $"{(off.Count > 0 ? $"; switched off: {string.Join(", ", off)}" : "")}{colliderNote}. {DressGhost(corner, materials)}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The build ghost shows the corner too, standing where the station will put it, in place of the
        /// generic stove model, which is switched off. Its footprint and anchor markers are left as they are.
        /// </summary>
        static string DressGhost(Mesh corner, Material[] materials)
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
                B.Show(go, corner, materials);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

                PrefabUtility.SaveAsPrefabAsset(root, GhostPath);
                return $"Build ghost (BreadStoveVisuals) shows the corner{(old != null ? $"; its '{OldGhostModel}' is switched off" : "")}.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The station's collider made to cover the pad and what stands on it, a little over a metre high.</summary>
        static string FitCollider(GameObject root)
        {
            var collider = root.GetComponent<BoxCollider>();
            if (collider == null) return "";

            Vector3 before = collider.center, beforeSize = collider.size;
            collider.center = Place + new Vector3((PadLeft + PadRight) * 0.5f, 0.6f, (PadFront + PadBack) * 0.5f);
            collider.size = new Vector3(PadRight - PadLeft, 1.2f, PadBack - PadFront);
            return $"; collider {before} x {beforeSize} -> {collider.center} x {collider.size}";
        }

        /// <summary>
        /// What moves while the station bakes, each its own part of the corner: the heat behind the
        /// oven windows coming up from a banked glow, the panel lamps lighting and heat rising off the
        /// flue (a <see cref="StationHeatGlow"/>); on the bench the pin rolling out a sheet of dough with
        /// a puff of flour at each end of a pass, and the big ball of dough being kneaded
        /// (<see cref="KitchenToolMotion"/>s).
        /// </summary>
        static void Working(Transform corner, Material painted, Material glow, Mesh heatMesh, Mesh lampMesh, Mesh pinMesh, Mesh sheetMesh, Mesh ballMesh)
        {
            Transform heat = G.Part(corner, "OvenHeat", heatMesh, Vector3.zero, glow, ShadowCastingMode.Off);
            Transform lamps = G.Part(corner, "Lamps", lampMesh, Vector3.zero, glow, ShadowCastingMode.Off);
            lamps.gameObject.SetActive(false);

            ParticleSystem flue = G.Steam(corner, "FlueHeat", K.SteamMaterial(), new Vector3(FlueX, FlueTop + 0.12f, FlueZ), new Vector3(0.08f, 0.01f, 0.08f),
                                          new Vector2(0.14f, 0.22f), 20, new Color(0.92f, 0.92f, 0.92f), 0.35f);
            corner.gameObject.AddComponent<StationHeatGlow>().Configure(new[] { heat.GetComponent<Renderer>() }, new Color(0.3f, 0.13f, 0.06f),
                                                                       new Color(1f, 0.6f, 0.28f), new[] { lamps.gameObject }, new[] { flue }, 2.5f);

            float cx = (BenchLeft + BenchRight) * 0.5f, onFlour = BenchTop + 0.004f;
            var sheetAt = new Vector3(cx + 0.01f, onFlour, -0.03f);
            Transform sheet = G.Part(corner, "DoughSheet", sheetMesh, sheetAt, painted, ShadowCastingMode.On);

            Quaternion pinTurn = Quaternion.Euler(0f, 10f, 0f);
            Transform pin = T.Tool(corner, "RollingPin", pinMesh, sheetAt + Vector3.up * (SheetThick + PinRadius), pinTurn, painted);
            ParticleSystem flour = T.Puff(corner, "FlourPuff", K.SteamMaterial(), sheetAt + Vector3.up * 0.04f, new Color(0.95f, 0.93f, 0.88f), new Vector2(0.06f, 0.1f));
            pin.gameObject.AddComponent<KitchenToolMotion>().Configure(KitchenToolMotion.Motion.Roll, 0.9f, 0.05f, pinTurn * Vector3.forward)
               .Rolls(PinRadius).Works(sheet, 0.25f).Puffs(flour);

            Transform ball = T.Tool(corner, "Dough", ballMesh, new Vector3(cx + 0.03f, onFlour, 0.13f), Quaternion.Euler(0f, 53f, 0f), painted);
            ball.gameObject.AddComponent<KitchenToolMotion>().Configure(KitchenToolMotion.Motion.Knead, 0.8f, 0.22f, Vector3.up);
        }

        // --- The corner ---------------------------------------------------------------------------

        /// <summary>The corner's still mesh; the heat behind the oven windows goes into <paramref name="heat"/> and the panel lamps into <paramref name="lamps"/>, to be lit while it bakes.</summary>
        static B.Model Corner(B.Model heat, B.Model lamps)
        {
            // A shade darker toward the floor, on everything standing on the pad but not on the tiles themselves.
            var m = new B.Model { Shade = (p, n) => p.y < PadTop + 0.01f ? 1f : Mathf.Lerp(0.84f, 1f, K.Smooth((p.y - PadTop) / 0.35f)) };

            Pad(m);
            Duckboard(m);
            Oven(m, heat, lamps);
            Bench(m);
            Rack(m);
            FlourBin(m);
            BaguetteBasket(m);

            return m;
        }

        /// <summary>Crumb and crust tiles, checked, on a grout bed; the floor shows under the oven's stand, so it is tiled there too.</summary>
        static void Pad(B.Model m)
        {
            float width = PadRight - PadLeft, depth = PadBack - PadFront;
            float bed = PadTop - 0.012f;
            m.Box(Grout, new Vector3((PadLeft + PadRight) * 0.5f, bed * 0.5f, (PadFront + PadBack) * 0.5f), new Vector3(width, bed, depth), 0.012f, 2);

            const int across = 10, down = 7;
            float tw = width / across, td = depth / down;

            for (int j = 0; j < down; j++)
            {
                for (int i = 0; i < across; i++)
                {
                    int key = j * across + i;
                    Color c = ((i + j) % 2 == 0 ? Crumb : CrustTile) * (1f + 0.03f * B.Jitter(key + 300));
                    m.Box(c, new Vector3(PadLeft + (i + 0.5f) * tw, PadTop - 0.007f, PadFront + (j + 0.5f) * td),
                          new Vector3(tw - 0.016f, 0.014f, td - 0.016f), 0.005f);
                }
            }
        }

        /// <summary>The slatted mat in front of the oven, where the baker stands.</summary>
        static void Duckboard(B.Model m)
        {
            const float left = -0.46f, right = 0.72f, front = -0.5f, back = 0.26f;

            foreach (float x in new[] { left + 0.06f, (left + right) * 0.5f, right - 0.06f })
                m.Box(B.Walnut, new Vector3(x, PadTop + 0.014f, (front + back) * 0.5f), new Vector3(0.05f, 0.028f, back - front - 0.04f), 0.008f);

            const int slats = 6;
            float pitch = (back - front) / slats;
            for (int k = 0; k < slats; k++)
            {
                float z = front + (k + 0.5f) * pitch;
                Color c = Slats[B.Hash(k + 40) % Slats.Length] * (1f + 0.04f * B.Jitter(k + 41));
                m.Box(c, new Vector3((left + right) * 0.5f, PadTop + 0.04f, z), new Vector3(right - left, 0.024f, pitch - 0.03f), 0.008f);
            }
        }

        // --- The oven -----------------------------------------------------------------------------

        /// <summary>Red enamel round two decks, each behind a steel door with a lit window, on a steel stand, a flue up from the back.</summary>
        static void Oven(B.Model m, B.Model heat, B.Model lamps)
        {
            Stand(m);

            float mid = (OvenLeft + OvenRight) * 0.5f;
            m.Box(Enamel, new Vector3(mid, (StandTop + OvenTop) * 0.5f, (OvenFace + OvenBack) * 0.5f),
                  new Vector3(OvenRight - OvenLeft, OvenTop - StandTop, OvenBack - OvenFace), 0.035f, 2);

            Deck(m, DeckBottom, 0, heat, lamps);
            Deck(m, DeckBottom + DeckPitch, 1, heat, lamps);
            Crown(m);
            Flue(m);
        }

        /// <summary>Four square steel legs on rubber feet, rails under the oven, and a shelf low down with sheet pans and proving baskets on it.</summary>
        static void Stand(B.Model m)
        {
            float left = OvenLeft + 0.04f, right = OvenRight - 0.04f, front = OvenFace + 0.03f, back = OvenBack - 0.04f;
            float mx = (left + right) * 0.5f, mz = (front + back) * 0.5f;

            foreach (float x in new[] { left, right })
            {
                foreach (float z in new[] { front, back })
                {
                    m.Box(Steel, new Vector3(x, (PadTop + StandTop) * 0.5f, z), new Vector3(0.04f, StandTop - PadTop, 0.04f), 0.008f);
                    m.Tube(Rubber, new Vector3(x, PadTop, z), new Vector3(x, PadTop + 0.02f, z), 0.026f, 10);
                }
            }

            foreach (float z in new[] { front, back })
                m.Box(SteelDark, new Vector3(mx, StandTop - 0.02f, z), new Vector3(right - left + 0.04f, 0.04f, 0.03f), 0.006f);
            foreach (float x in new[] { left, right })
                m.Box(SteelDark, new Vector3(x, StandTop - 0.02f, mz), new Vector3(0.03f, 0.04f, back - front), 0.006f);

            m.Box(SteelDark, new Vector3(mx, StandShelf, mz), new Vector3(right - left + 0.02f, 0.016f, back - front + 0.02f), 0.005f);
            float shelfTop = StandShelf + 0.008f;

            for (int k = 0; k < 3; k++)
                Tray(m, new Vector3(-0.17f, shelfTop + k * 0.018f, 0.76f), 0.48f, 0.6f, k % 2 == 0 ? Steel : SteelLight);

            Banneton(m, new Vector3(0.36f, shelfTop, 0.6f), 0.1f, 0.075f);
            Banneton(m, new Vector3(0.36f, shelfTop + 0.06f, 0.6f), 0.1f, 0.075f);
            Banneton(m, new Vector3(0.38f, shelfTop, 0.88f), 0.1f, 0.075f);
        }

        /// <summary>
        /// One deck: a steel face across its front, its door standing out from that - rails and stiles
        /// round a window with a chrome bead, a chrome bar handle along the top - and through the
        /// window the deck's stone floor, the dark of its roof, the glow of the heat and loaves baking;
        /// its block of the control panel beside it, a gauge, two knobs and a lamp.
        /// </summary>
        static void Deck(B.Model m, float bottom, int deck, B.Model glowing, B.Model lamps)
        {
            float top = bottom + DeckHeight;
            float sill = bottom + WindowSill, head = bottom + WindowHead;
            float doorBottom = bottom + 0.015f, doorTop = top - 0.015f;
            float doorMid = (DoorLeft + DoorRight) * 0.5f, doorWidth = DoorRight - DoorLeft;
            float doorZ = (DoorFront + SteelFace) * 0.5f, doorDepth = SteelFace - DoorFront;
            float windowMid = (WindowLeft + WindowRight) * 0.5f, windowWidth = WindowRight - WindowLeft;

            m.Box(Steel, new Vector3(doorMid, (bottom + top) * 0.5f, (SteelFace + OvenFace) * 0.5f), new Vector3(doorWidth + 0.04f, DeckHeight, OvenFace - SteelFace),
                  0.01f, 2);

            m.Box(SteelLight, new Vector3(doorMid, (head + doorTop) * 0.5f, doorZ), new Vector3(doorWidth, doorTop - head, doorDepth), 0.012f, 2);
            m.Box(SteelLight, new Vector3(doorMid, (doorBottom + sill) * 0.5f, doorZ), new Vector3(doorWidth, sill - doorBottom, doorDepth), 0.012f, 2);
            m.Box(SteelLight, new Vector3((DoorLeft + WindowLeft) * 0.5f, (sill + head) * 0.5f, doorZ), new Vector3(WindowLeft - DoorLeft, head - sill + 0.02f, doorDepth),
                  0.012f, 2);
            m.Box(SteelLight, new Vector3((WindowRight + DoorRight) * 0.5f, (sill + head) * 0.5f, doorZ), new Vector3(DoorRight - WindowRight, head - sill + 0.02f, doorDepth),
                  0.012f, 2);

            // The chrome bead round the window.
            float bead = DoorFront - 0.002f;
            foreach (float y in new[] { sill, head })
                m.Box(Chrome, new Vector3(windowMid, y, bead), new Vector3(windowWidth + 0.024f, 0.012f, 0.008f), 0.004f);
            foreach (float x in new[] { WindowLeft, WindowRight })
                m.Box(Chrome, new Vector3(x, (sill + head) * 0.5f, bead), new Vector3(0.012f, head - sill + 0.012f, 0.008f), 0.004f);

            // Inside: the stone floor of the deck, the dark of its roof, and the heat glowing at the back.
            float insideZ = (DoorFront + GlowZ) * 0.5f, insideDepth = GlowZ - DoorFront;
            m.Box(DeckStone, new Vector3(windowMid, sill + 0.006f, insideZ), new Vector3(windowWidth + 0.01f, 0.012f, insideDepth), 0.003f);
            m.Box(Soot, new Vector3(windowMid, head - 0.009f, insideZ + 0.004f), new Vector3(windowWidth + 0.01f, 0.018f, insideDepth - 0.008f), 0.003f);

            var heat = new SmoothMesh(1);
            heat.RoundedBox(0, new Vector3(windowMid, (sill + head) * 0.5f, GlowZ), new Vector3(windowWidth + 0.01f, head - sill + 0.01f, 0.004f), 0.001f,
                            Quaternion.identity, 1);
            glowing.Add(heat, Color.white);

            // Loaves baking: long ones on the lower deck, rolls on the upper, flattened front to back to fit the deck as the window shows it.
            var floor = new Vector3(0f, sill + 0.012f, insideZ);
            int key = 10 + deck * 10;
            if (deck == 0)
            {
                foreach (float x in new[] { -0.19f, 0f, 0.19f })
                    LongLoaf(m, floor + Vector3.right * x, Quaternion.LookRotation(Vector3.right), 0.15f, 0.04f, 0.06f, 3, true, ++key);
            }
            else
            {
                foreach (float x in new[] { -0.215f, -0.072f, 0.072f, 0.215f })
                    Bun(m, floor + Vector3.right * x, 0.045f, 0.02f, 0.055f, 0f, true, ++key, 1);
            }

            // The handle: a chrome bar on two standoffs along the door's top rail.
            float hy = (head + doorTop) * 0.5f, hz = DoorFront - 0.034f;
            m.Tube(Chrome, new Vector3(DoorLeft + 0.07f, hy, hz), new Vector3(DoorRight - 0.07f, hy, hz), 0.012f, 10);
            foreach (float x in new[] { DoorLeft + 0.1f, DoorRight - 0.1f })
                m.Box(Steel, new Vector3(x, hy, (hz + DoorFront) * 0.5f), new Vector3(0.022f, 0.022f, DoorFront - hz), 0.006f);

            // The panel: a gauge up top, two knobs and the lamp that shows the deck is on.
            float px = (PanelLeft + PanelRight) * 0.5f;
            m.Box(Panel, new Vector3(px, (bottom + top) * 0.5f, (PanelFront + OvenFace) * 0.5f), new Vector3(PanelRight - PanelLeft, DeckHeight - 0.02f, OvenFace - PanelFront),
                  0.012f, 2);

            var gauge = new Vector3(px, bottom + 0.195f, PanelFront - 0.003f);
            K.Disc(m, (p, n) => Chrome, gauge, Vector3.back, 0.052f);
            K.Disc(m, (p, n) => DialFace, gauge + Vector3.back * 0.002f, Vector3.back, 0.043f);
            m.Box(Enamel, gauge + new Vector3(0.01f, 0.012f, -0.004f), new Vector3(0.036f, 0.006f, 0.003f), 0.001f, Quaternion.Euler(0f, 0f, 35f + deck * 40f));

            foreach (float x in new[] { PanelLeft + 0.05f, PanelRight - 0.05f })
            {
                m.Tube((p, n) => n.z < -0.8f ? Chrome : Rubber, new Vector3(x, bottom + 0.075f, PanelFront), new Vector3(x, bottom + 0.075f, PanelFront - 0.026f), 0.021f,
                       0.018f, 12);
            }

            var lamp = new Vector3(px, bottom + 0.075f, PanelFront - 0.002f);
            K.Disc(m, (p, n) => Chrome, lamp, Vector3.back, 0.017f, 12);
            var light = new SmoothMesh(1);
            light.RoundedBox(0, lamp + Vector3.back * 0.006f, new Vector3(0.02f, 0.02f, 0.012f), 0.006f, Quaternion.identity, 2);
            lamps.Add(light, Color.white);
        }

        /// <summary>
        /// The top of the oven above the decks: a cream pinstripe and a chrome name plate across the
        /// front, two steam damper knobs, a vent grille on top and two proving baskets set there to warm.
        /// </summary>
        static void Crown(B.Model m)
        {
            float mid = (OvenLeft + OvenRight) * 0.5f;
            float band = (DeckBottom + DeckPitch + DeckHeight + OvenTop) * 0.5f;

            m.Box(Cream, new Vector3(mid, band, OvenFace - 0.002f), new Vector3(OvenRight - OvenLeft - 0.08f, 0.01f, 0.006f), 0.002f);
            m.Box(Chrome, new Vector3(-0.05f, band, OvenFace - 0.006f), new Vector3(0.22f, 0.036f, 0.008f), 0.004f, 2);
            m.Box(SteelDark, new Vector3(-0.05f, band, OvenFace - 0.0105f), new Vector3(0.17f, 0.008f, 0.002f), 0.001f);

            foreach (float x in new[] { 0.52f, 0.62f })
                m.Tube((p, n) => n.z < -0.8f ? Chrome : Rubber, new Vector3(x, band, OvenFace), new Vector3(x, band, OvenFace - 0.022f), 0.013f, 0.011f, 10);

            m.Box(SteelDark, new Vector3(0.12f, OvenTop + 0.004f, 0.96f), new Vector3(0.26f, 0.012f, 0.12f), 0.004f);
            for (int k = 0; k < 4; k++)
                m.Box(Soot, new Vector3(0.03f + k * 0.06f, OvenTop + 0.0105f, 0.96f), new Vector3(0.026f, 0.002f, 0.08f), 0.001f);

            Banneton(m, new Vector3(-0.26f, OvenTop, 0.66f), 0.11f, 0.08f);
            Banneton(m, new Vector3(-0.26f, OvenTop + 0.065f, 0.66f), 0.11f, 0.08f);
        }

        /// <summary>A stainless flue up from the back of the oven top, banded, with its damper handle and a china-hat cap on three legs.</summary>
        static void Flue(B.Model m)
        {
            var foot = new Vector3(FlueX, OvenTop, FlueZ);
            m.Box(SteelDark, foot + Vector3.up * 0.02f, new Vector3(0.17f, 0.04f, 0.17f), 0.01f, 2);
            m.Tube(Steel, foot, new Vector3(FlueX, FlueTop, FlueZ), 0.058f, 14);
            foreach (float y in new[] { 1.24f, 1.46f })
                m.Tube(SteelDark, new Vector3(FlueX, y - 0.012f, FlueZ), new Vector3(FlueX, y + 0.012f, FlueZ), 0.065f, 14);

            m.Box(SteelDark, new Vector3(FlueX, 1.14f, FlueZ - 0.08f), new Vector3(0.016f, 0.016f, 0.06f), 0.004f);
            m.Box(Rubber, new Vector3(FlueX, 1.14f, FlueZ - 0.114f), new Vector3(0.06f, 0.014f, 0.016f), 0.005f);

            for (int k = 0; k < 3; k++)
            {
                float a = k * 120f * Mathf.Deg2Rad;
                var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.048f;
                m.Tube(SteelDark, new Vector3(FlueX, FlueTop, FlueZ) + o, new Vector3(FlueX, FlueTop + 0.055f, FlueZ) + o, 0.005f, 5);
            }

            m.Lathe((p, n) => SteelDark, new Vector3(FlueX, FlueTop + 0.045f, FlueZ), new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.12f, 0f), new Vector2(0.12f, 0f), new Vector2(0.12f, 0.012f), new Vector2(0.12f, 0.012f),
                new Vector2(0f, 0.075f),
            }, 16);
        }

        // --- The bench ----------------------------------------------------------------------------

        /// <summary>
        /// The baker's bench down the left: walnut legs and aprons under a maple top. A basket of the
        /// day's loaves at the front end; in the middle a dusting of flour with a ball of dough on it,
        /// two pieces cut from it, the rolling pin and a scraper; a tray of shaped rolls waiting at the
        /// back, and a dredger, a crock and a bowl of flour against the wall. Underneath, a crate of
        /// loaves, a stack of proving baskets and two sacks of flour.
        /// </summary>
        static void Bench(B.Model m)
        {
            float cx = (BenchLeft + BenchRight) * 0.5f, cz = (BenchFront + BenchBack) * 0.5f;
            float w = BenchRight - BenchLeft, d = BenchBack - BenchFront;
            float under = BenchTop - 0.05f;

            foreach (float x in new[] { BenchLeft + 0.04f, BenchRight - 0.04f })
                foreach (float z in new[] { BenchFront + 0.04f, cz, BenchBack - 0.04f })
                    m.Box(B.Walnut, new Vector3(x, (PadTop + under) * 0.5f, z), new Vector3(0.056f, under - PadTop, 0.056f), 0.012f);

            foreach (float x in new[] { BenchLeft + 0.035f, BenchRight - 0.035f })
                m.Box(B.Walnut, new Vector3(x, under - 0.045f, cz), new Vector3(0.026f, 0.09f, d - 0.08f), 0.008f);
            foreach (float z in new[] { BenchFront + 0.035f, BenchBack - 0.035f })
                m.Box(B.Walnut, new Vector3(cx, under - 0.045f, z), new Vector3(w - 0.08f, 0.09f, 0.026f), 0.008f);

            const int boards = 5;
            for (int i = 0; i < boards; i++)
            {
                float x = BenchLeft + (i + 0.5f) * w / boards;
                Color c = Maple[B.Hash(i + 10) % Maple.Length] * (1f + 0.04f * B.Jitter(i + 11));
                m.Box(c, new Vector3(x, BenchTop - 0.025f, cz), new Vector3(w / boards - 0.004f, 0.05f, d + 0.02f), 0.01f);
            }

            for (int i = 0; i < 4; i++)
            {
                float x = BenchLeft + 0.07f + i * (w - 0.14f) / 3f;
                m.Box(B.Plank(i + 60), new Vector3(x, BenchShelf, cz), new Vector3(0.11f, 0.024f, d - 0.06f), 0.008f);
            }

            float top = BenchTop;

            // The front end: the day's loaves in a basket.
            Basket(m, new Vector3(cx, top, -0.27f), 0.17f, 0.2f, 0.07f);
            float inBasket = top + 0.014f;
            Bun(m, new Vector3(cx - 0.06f, inBasket, -0.33f), 0.068f, 0.068f, 0.085f, 20f, true, 1, 2);
            LongLoaf(m, new Vector3(cx + 0.07f, inBasket, -0.25f), Quaternion.Euler(0f, 8f, 0f), 0.26f, 0.09f, 0.075f, 3, true, 2);
            Bun(m, new Vector3(cx - 0.06f, inBasket, -0.17f), 0.058f, 0.058f, 0.075f, 70f, true, 3, 1);

            // The middle: flour, the dough, the pin and the scraper.
            FlourDust(m, new Vector3(cx + 0.02f, top + 0.002f, 0.17f), 0.22f, 0.27f, 5);
            float onFlour = top + 0.004f;
            // The big ball of dough, the sheet rolled out and the pin are their own parts (Working).
            DoughBall(m, new Vector3(cx - 0.12f, onFlour, 0.29f), new Vector3(0.036f, 0.022f, 0.033f), 2);
            DoughBall(m, new Vector3(cx - 0.035f, onFlour, 0.33f), new Vector3(0.034f, 0.021f, 0.032f), 3);
            Scraper(m, new Vector3(cx + 0.19f, top, 0.34f), -24f);

            // The back: a tray of shaped rolls waiting to go in, and the dredger, a crock and a bowl of flour at the wall.
            var tray = new Vector3(cx, top, 0.72f);
            Tray(m, tray, 0.38f, 0.44f, Steel);
            int key = 30;
            foreach (float x in new[] { -0.09f, 0.09f })
                foreach (float z in new[] { -0.13f, 0f, 0.13f })
                    Bun(m, tray + new Vector3(x, 0.008f, z), 0.05f, 0.05f, 0.05f, 0f, false, ++key, 0);

            K.Tin(m, new Vector3(cx - 0.19f, top, 0.99f), 0.034f, 0.1f, Cream);
            K.Crock(m, new Vector3(cx - 0.03f, top, 1.0f), 0.05f, 0.1f);
            var bowl = new Vector3(cx + 0.17f, top, 0.98f);
            K.Bowl(m, bowl, 0.075f);
            K.Disc(m, (p, n) => Flour, bowl + Vector3.up * (0.075f * 0.45f), Vector3.up, 0.075f * 0.8f, 16);

            // Underneath.
            float shelfTop = BenchShelf + 0.012f;
            var crateFoot = new Vector3(cx, shelfTop, -0.3f);
            var crate = new Vector3(0.42f, 0.15f, 0.3f);
            K.OpenCrate(m, crateFoot, crate, 3f, 23);
            key = 40;
            foreach (float x in new[] { -0.1f, 0.1f })
                LongLoaf(m, crateFoot + new Vector3(x, crate.y - 0.05f, 0f), Quaternion.Euler(0f, 4f + 6f * B.Jitter(key), 0f), 0.24f, 0.09f, 0.07f, 3, true, ++key);

            for (int k = 0; k < 3; k++) Banneton(m, new Vector3(cx, shelfTop + k * 0.05f, 0.06f), 0.1f, 0.07f);

            B.Sack(m, new Vector3(cx + 0.02f, shelfTop, 0.42f), 0.12f, 0.3f, -0.03f, 14);
            B.Sack(m, new Vector3(cx, shelfTop, 0.76f), 0.13f, 0.34f, 0.03f, 12);
        }

        /// <summary>A dusting of flour on the bench, a ragged oval lying flat.</summary>
        static void FlourDust(B.Model m, Vector3 centre, float rx, float rz, int key)
        {
            m.Surface((p, n) => Flour, 36, 3, (u, v) =>
            {
                float a = u * Mathf.PI * 2f;
                float ragged = 1f + 0.12f * Mathf.Sin(a * 3f + key) + 0.07f * Mathf.Sin(a * 7f + key * 2f);
                // Round the other way from a lathe, so the flat side faces up.
                return centre + new Vector3(-Mathf.Cos(a) * rx * ragged * v, 0f, Mathf.Sin(a) * rz * ragged * v);
            }, true);
        }

        /// <summary>A ball of dough slumped on <paramref name="foot"/>, paler on top where the flour caught it.</summary>
        static void DoughBall(B.Model m, Vector3 foot, Vector3 radii, int key)
        {
            Vector3 centre = foot + Vector3.up * radii.y;
            K.Ball(m, (p, n) => Color.Lerp(Dough * 0.92f, DoughTop, K.Smooth((p.y - foot.y) / (radii.y * 2f))), centre, radii,
                   Quaternion.Euler(0f, key * 53f, 0f), 18, 9);
        }


        /// <summary>A bench scraper lying flat: a steel blade, a walnut grip along its back edge.</summary>
        static void Scraper(B.Model m, Vector3 foot, float yaw)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            m.Box(Steel, foot + q * new Vector3(0f, 0.003f, 0.005f), new Vector3(0.12f, 0.004f, 0.08f), 0.002f, q);
            m.Box(B.Walnut, foot + q * new Vector3(0f, 0.013f, -0.042f), new Vector3(0.124f, 0.024f, 0.026f), 0.008f, q);
        }

        // --- The rack -----------------------------------------------------------------------------

        /// <summary>
        /// A steel rack of baking trays on casters: rolls cooling on the top tray, baguettes under them,
        /// rolls and loaves proving lower down, loaves baked below that and an empty tray at the
        /// bottom; a bread peel hung from its right side.
        /// </summary>
        static void Rack(B.Model m)
        {
            float mx = (RackLeft + RackRight) * 0.5f, mz = (RackFront + RackBack) * 0.5f;
            float postBottom = PadTop + 0.075f;
            float[] xs = { RackLeft + 0.0125f, RackRight - 0.0125f }, zs = { RackFront + 0.0125f, RackBack - 0.0125f };

            foreach (float x in xs)
            {
                foreach (float z in zs)
                {
                    m.Box(Steel, new Vector3(x, (postBottom + RackTop) * 0.5f, z), new Vector3(0.025f, RackTop - postBottom, 0.025f), 0.005f);
                    m.Box(SteelDark, new Vector3(x, postBottom - 0.008f, z), new Vector3(0.03f, 0.02f, 0.03f), 0.004f);
                    m.Tube(Rubber, new Vector3(x - 0.011f, PadTop + 0.028f, z), new Vector3(x + 0.011f, PadTop + 0.028f, z), 0.028f, 12);
                }
            }

            foreach (float y in new[] { postBottom + 0.012f, RackTop - 0.012f })
            {
                foreach (float x in xs)
                    m.Box(Steel, new Vector3(x, y, mz), new Vector3(0.022f, 0.022f, RackBack - RackFront), 0.004f);
                foreach (float z in zs)
                    m.Box(Steel, new Vector3(mx, y, z), new Vector3(RackRight - RackLeft, 0.022f, 0.022f), 0.004f);
            }

            float trayWidth = RackRight - RackLeft - 0.05f, trayDepth = RackBack - RackFront - 0.04f;
            foreach (float y in TrayHeights)
            {
                foreach (float x in new[] { RackLeft + 0.02f, RackRight - 0.02f })
                    m.Box(SteelDark, new Vector3(x, y - 0.01f, mz), new Vector3(0.016f, 0.008f, RackBack - RackFront - 0.03f), 0.002f);
                Tray(m, new Vector3(mx, y - 0.006f, mz), trayWidth, trayDepth, Steel);
            }

            float On(int tray) => TrayHeights[tray] + 0.002f;
            float[] four = { -0.225f, -0.075f, 0.075f, 0.225f };
            int key = 60;

            // Top: rolls cooling, scored across.
            foreach (float x in new[] { -0.072f, 0.072f })
                foreach (float z in four)
                    Bun(m, new Vector3(mx + x, On(5), mz + z), 0.056f, 0.056f, 0.06f, B.Jitter(key) * 30f, true, ++key, key % 3 == 0 ? 2 : 1);

            foreach (float x in new[] { -0.095f, 0f, 0.095f })
                LongLoaf(m, new Vector3(mx + x, On(4), mz), Quaternion.identity, 0.56f, 0.06f, 0.05f, 5, true, ++key);

            foreach (float x in new[] { -0.072f, 0.072f })
                foreach (float z in four)
                    Bun(m, new Vector3(mx + x, On(3), mz + z), 0.05f, 0.05f, 0.048f, 0f, false, ++key, 0);

            foreach (float x in new[] { -0.072f, 0.072f })
            {
                foreach (float z in new[] { -0.15f, 0.15f })
                {
                    LongLoaf(m, new Vector3(mx + x, On(2), mz + z), Quaternion.identity, 0.26f, 0.09f, 0.06f, 0, false, ++key);
                    LongLoaf(m, new Vector3(mx + x, On(1), mz + z), Quaternion.identity, 0.26f, 0.09f, 0.075f, 3, true, ++key);
                }
            }

            Peel(m);
        }

        /// <summary>The bread peel hung blade down on a hook from the rack's right side.</summary>
        static void Peel(B.Model m)
        {
            float x = RackRight + 0.02f, z = 0.76f;
            m.Tube(SteelDark, new Vector3(RackRight - 0.005f, 1.36f, z), new Vector3(x, 1.36f, z), 0.005f, 6);
            m.Tube(PinWood * 0.95f, new Vector3(x, 1.37f, z), new Vector3(x, 0.86f, z), 0.015f, 8);
            m.Box(PinWood, new Vector3(x, 0.7f, z), new Vector3(0.014f, 0.32f, 0.25f), 0.007f, 2);
            m.Box(PinWood * 0.95f, new Vector3(x, 0.87f, z), new Vector3(0.018f, 0.04f, 0.12f), 0.007f);
        }

        // --- The floor ----------------------------------------------------------------------------

        /// <summary>
        /// An open boarded flour bin at the front of the rack, heaped with flour, a steel scoop in it.
        /// (A lid propped up at the back of it read as the back of a chair.)
        /// </summary>
        static void FlourBin(B.Model m)
        {
            var foot = new Vector3(BinX, PadTop, BinZ);
            float hx = BinWidth * 0.5f, hz = BinDepth * 0.5f;

            m.Box(B.WalnutDark, foot + Vector3.up * (BinHeight * 0.45f), new Vector3(BinWidth - 0.03f, BinHeight * 0.9f, BinDepth - 0.03f), 0.01f);

            for (int k = 0; k < 3; k++)
            {
                float y = (k + 0.5f) * BinHeight / 3f, h = BinHeight / 3f - 0.012f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Color c = B.HoneyLight * (1f + 0.05f * B.Jitter(k * 5 + side + 80));
                    m.Box(c, foot + new Vector3(0f, y, side * hz), new Vector3(BinWidth, h, 0.024f), 0.007f);
                    m.Box(c * 0.97f, foot + new Vector3(side * hx, y, 0f), new Vector3(0.024f, h, BinDepth - 0.02f), 0.007f);
                }
            }

            for (int k = 0; k < 4; k++)
            {
                float x = (k & 1) == 0 ? -hx : hx, z = (k & 2) == 0 ? -hz : hz;
                m.Box(B.Walnut, foot + new Vector3(x, BinHeight * 0.5f + 0.006f, z), new Vector3(0.036f, BinHeight + 0.012f, 0.036f), 0.008f);
            }

            m.Box(Flour, foot + new Vector3(0f, BinHeight - 0.055f, 0f), new Vector3(BinWidth - 0.04f, 0.02f, BinDepth - 0.04f), 0.008f);
            K.Ball(m, (p, n) => Flour, foot + new Vector3(-0.02f, BinHeight - 0.05f, 0.02f), new Vector3(0.11f, 0.03f, 0.1f), Quaternion.identity, 16, 8);

            // The scoop, half buried, its handle up toward the front.
            var cup = foot + new Vector3(0.03f, BinHeight - 0.03f, -0.05f);
            var lip = cup + new Vector3(-0.05f, 0.02f, 0.03f);
            m.Tube((p, n) => Chrome, cup, lip, 0.03f, 0.034f, 12);
            m.Tube(B.Walnut, cup, cup + new Vector3(0.07f, 0.035f, -0.04f), 0.01f, 8);
        }

        /// <summary>A tall wicker basket in front of the bin with a bunch of baguettes standing in it, leaning out.</summary>
        static void BaguetteBasket(B.Model m)
        {
            var foot = new Vector3(BinX, PadTop, -0.37f);
            const float r0 = 0.09f, r1 = 0.12f, h = 0.26f;

            var profile = new List<Vector2> { new Vector2(0f, 0f), new Vector2(r0, 0f), new Vector2(r0, 0f) };
            const int rows = 9;
            for (int i = 1; i <= rows; i++)
            {
                float t = i / (float)rows;
                profile.Add(new Vector2(Mathf.Lerp(r0, r1, t), t * h));
            }

            profile.Add(new Vector2(r1 + 0.008f, h + 0.008f));
            profile.Add(new Vector2(r1 - 0.01f, h + 0.004f));
            profile.Add(new Vector2(r0 - 0.008f, 0.02f));
            profile.Add(new Vector2(0f, 0.02f));

            m.Lathe((p, n) =>
            {
                float y = p.y - foot.y;
                if (y > h - 0.005f) return WickerDark;
                // Coils: a shade apart row by row.
                return Mathf.RoundToInt(y / (h / rows)) % 2 == 0 ? Wicker : Wicker * 0.86f;
            }, foot, profile, 22);

            for (int k = 0; k < 6; k++)
            {
                float a = (k / 6f + 0.07f) * Mathf.PI * 2f;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float lean = (10f + 5f * B.Jitter(k + 90)) * Mathf.Deg2Rad;
                Vector3 axis = Vector3.up * Mathf.Cos(lean) + radial * Mathf.Sin(lean);
                float length = 0.52f + 0.05f * B.Jitter(k + 95);
                Vector3 bottom = foot + radial * 0.035f + Vector3.up * 0.03f;
                LongLoaf(m, bottom + axis * (length * 0.5f), Quaternion.LookRotation(axis, radial), length, 0.058f, 0.05f, 5, true, k + 100);
            }
        }

        // --- Bread, baskets, trays ----------------------------------------------------------------

        /// <summary>
        /// A long loaf - a baguette or a bâtard - lying along its own forward through <paramref name="centre"/>,
        /// the middle of its flat underside: round in section, its ends drawn in, browner low down and
        /// golden on top, and when baked <paramref name="slashes"/> pale cuts slanting across its top.
        /// Raw dough is pale and smooth.
        /// </summary>
        static void LongLoaf(B.Model m, Vector3 centre, Quaternion turn, float length, float width, float height, int slashes, bool baked, int key)
        {
            Quaternion back = Quaternion.Inverse(turn);
            Color low = baked ? Crust * 0.86f : Dough * 0.9f, high = baked ? CrustTop : DoughTop;
            float tone = 1f + 0.04f * B.Jitter(key);
            float spacing = length * 0.72f / Mathf.Max(1, slashes);

            m.Surface((p, n) =>
            {
                Vector3 l = back * (p - centre);
                Color c = Color.Lerp(low, high, K.Smooth(l.y / height)) * tone;
                if (!baked || slashes == 0) return c;

                float cut = 0f;
                for (int k = 0; k < slashes; k++)
                {
                    float d = l.z - (k - (slashes - 1) * 0.5f) * spacing - l.x * 1.1f;
                    cut = Mathf.Max(cut, Mathf.Exp(-d * d / (spacing * spacing * 0.03f)));
                }

                cut *= K.Smooth((l.y - height * 0.55f) / (height * 0.3f)) * K.Smooth(1f - Mathf.Abs(l.x) / (width * 0.42f));
                return Color.Lerp(c, Score, cut * 0.85f);
            }, 14, 32, (u, v) =>
            {
                float a = u * Mathf.PI * 2f;
                float end = Mathf.Abs(2f * v - 1f);
                float k = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(end, 2.5f)));
                float s = Mathf.Sin(a);

                // Flat underneath: the section's lower part squashed toward the board.
                float y = (0.42f + (s >= 0f ? s * 0.58f : s * 0.42f)) * height * k;
                return centre + turn * new Vector3(-Mathf.Cos(a) * width * 0.5f * k, y, (v - 0.5f) * length);
            }, true);
        }

        /// <summary>
        /// A round loaf or roll sitting on <paramref name="foot"/>: flattened underneath, domed on top,
        /// <paramref name="cuts"/> pale slashes across it when baked (one, or two crossed); raw dough
        /// pale and smooth.
        /// </summary>
        static void Bun(B.Model m, Vector3 foot, float rx, float rz, float height, float yaw, bool baked, int key, int cuts)
        {
            Quaternion spin = Quaternion.Euler(0f, yaw, 0f), back = Quaternion.Inverse(spin);
            Color low = baked ? Crust * 0.86f : Dough * 0.9f, high = baked ? CrustTop : DoughTop;
            float tone = 1f + 0.04f * B.Jitter(key);
            float w = rx * rx * 0.05f;

            m.Surface((p, n) =>
            {
                Vector3 l = back * (p - foot);
                Color c = Color.Lerp(low, high, K.Smooth(l.y / height)) * tone;
                if (!baked || cuts == 0) return c;

                float cut = Mathf.Exp(-l.x * l.x / w);
                if (cuts > 1) cut = Mathf.Max(cut, Mathf.Exp(-l.z * l.z / w));
                cut *= K.Smooth((l.y - height * 0.6f) / (height * 0.25f));
                return Color.Lerp(c, Score, cut * 0.85f);
            }, 20, 12, (u, v) =>
            {
                float th = v * Mathf.PI, ph = u * Mathf.PI * 2f;
                float s = Mathf.Sin(th), y = -Mathf.Cos(th);
                y = y < 0f ? y * 0.45f : y;
                return foot + spin * new Vector3(s * Mathf.Cos(ph) * rx, (y + 0.45f) / 1.45f * height, s * Mathf.Sin(ph) * rz);
            }, true);
        }

        /// <summary>A shallow oval wicker basket standing on <paramref name="foot"/>, its rim a darker plait.</summary>
        static void Basket(B.Model m, Vector3 foot, float rx, float rz, float h)
        {
            Vector2[] profile =
            {
                new Vector2(0f, 0f), new Vector2(0.84f, 0f), new Vector2(0.92f, h * 0.35f), new Vector2(0.97f, h * 0.7f), new Vector2(1f, h),
                new Vector2(1.03f, h + 0.01f), new Vector2(0.95f, h + 0.006f), new Vector2(0.9f, h * 0.5f), new Vector2(0.8f, 0.012f), new Vector2(0f, 0.012f),
            };

            m.Surface((p, n) =>
            {
                float y = p.y - foot.y;
                if (y > h - 0.004f) return WickerDark;
                float a = Mathf.Atan2(p.z - foot.z, p.x - foot.x);
                return Wicker * (1f + 0.06f * Mathf.Sign(Mathf.Sin(a * 14f)));
            }, 28, profile.Length - 1, (u, v) =>
            {
                float a = u * Mathf.PI * 2f;
                float f = v * (profile.Length - 1);
                int i = Mathf.Min((int)f, profile.Length - 2);
                Vector2 q = Vector2.Lerp(profile[i], profile[i + 1], f - i);
                return foot + new Vector3(Mathf.Cos(a) * rx * q.x, q.y, Mathf.Sin(a) * rz * q.x);
            }, true);
        }

        /// <summary>A round cane proving basket standing on <paramref name="foot"/>: coiled outside, floured inside.</summary>
        static void Banneton(B.Model m, Vector3 foot, float r, float h)
        {
            var profile = new List<Vector2> { new Vector2(0f, 0f), new Vector2(r * 0.72f, 0f), new Vector2(r * 0.72f, 0f) };
            const int rows = 6;
            for (int i = 1; i <= rows; i++)
            {
                float t = i / (float)rows;
                profile.Add(new Vector2(Mathf.Lerp(r * 0.72f, r, Mathf.Sqrt(t)), t * h));
            }

            profile.Add(new Vector2(r * 0.97f, h + 0.005f));
            profile.Add(new Vector2(r * 0.93f, h));
            profile.Add(new Vector2(r * 0.68f, 0.012f));
            profile.Add(new Vector2(0f, 0.012f));

            m.Lathe((p, n) =>
            {
                float y = p.y - foot.y;
                float across = new Vector2(p.x - foot.x, p.z - foot.z).magnitude;
                if (n.y > 0.2f && across < r * 0.95f) return Flour * 0.95f;
                return Mathf.RoundToInt(y / (h / rows)) % 2 == 0 ? Wicker : WickerDark;
            }, foot, profile, 20);
        }

        /// <summary>A sheet pan standing on <paramref name="foot"/>, <paramref name="sx"/> by <paramref name="sz"/>, with its rolled rim.</summary>
        static void Tray(B.Model m, Vector3 foot, float sx, float sz, Color steel)
        {
            m.Box(steel, foot + Vector3.up * 0.004f, new Vector3(sx, 0.008f, sz), 0.003f);
            foreach (float s in new[] { -1f, 1f })
            {
                m.Box(SteelLight, foot + new Vector3(0f, 0.009f, s * (sz * 0.5f - 0.005f)), new Vector3(sx, 0.016f, 0.01f), 0.004f);
                m.Box(SteelLight, foot + new Vector3(s * (sx * 0.5f - 0.005f), 0.009f, 0f), new Vector3(0.01f, 0.016f, sz), 0.004f);
            }
        }

        // --- Materials ----------------------------------------------------------------------------

        /// <summary>The heat glowing through the oven windows and the panel lamps: unlit, so they read however dark the kitchen is.</summary>
        static Material GlowMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("Shader Universal Render Pipeline/Unlit is missing.");

            const string name = "BakeryCorner_OvenGlow";
            string path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", new Color(1f, 0.6f, 0.28f));
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }
    }
}
