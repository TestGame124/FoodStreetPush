using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using B = LastOrders.EditorTools.BuildingKit;
using K = LastOrders.EditorTools.SoupKitchenModel;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The salad station (pfSaladStation) as a salad corner in the game's smooth low-poly style, in
    /// place of the prototype salad bar: a cold well along the back - a sage cabinet with a steel top
    /// and eight pans of what the salads are made from (lettuce, tomatoes, sweetcorn, carrot, cheese,
    /// beans, chicken) - with a shelf over it of potted herbs, dressings and bowls and utensils hung
    /// under it; an oak worktop round the right with a carrot being chopped on a board, a big wooden
    /// bowl of salad being tossed, a dressing cruet, a colander of washed leaves, a stack of plates
    /// ready, and crates of lettuces and carrots underneath; a chalkboard menu on the wall; a cool box
    /// and a pyramid of bean tins on the floor, a slatted mat where the cook stands - all on a pad of
    /// sage tiles that marks the corner out from the kitchen floor.
    ///
    /// What moves while the station works - the knife and the carrot rounds it cuts, the salad and
    /// the servers tossing it with the leaves they throw up, the cruet and its stream of dressing - is
    /// its own object, driven by a <see cref="SaladStationAnimation"/>. Everything else is one mesh,
    /// painted in its vertices on LastOrders/Stylized, as <see cref="BuildingKit"/> builds.
    ///
    /// Its front faces -Z. It is set into the station turned round, since the station's front is its
    /// +Z. The prototype model and the painted floor shadows that went with it are switched off, not
    /// deleted; the collider is fitted to the corner. The station's components and centre point are
    /// left alone. The salad station has no build-mode entry, so there is no build ghost to dress.
    /// </summary>
    public static class SaladStationModel
    {
        const string PrefabPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Stoves/SaladStation/pfSaladStation.prefab";
        const string Folder = "Assets/Game Assets/Content/LastOrders/Kitchen/" + EnvKit.Style + "/SaladStation";

        const string OldModel = "SaladStation";
        static readonly string[] OldShadows = { "Square (2)", "Square (3)" };
        const string ModelName = "SaladCorner";

        /// <summary>
        /// Where the corner's middle stands in the station's space. The station is placed on the build
        /// grid like everything the shop sells, its origin the near left corner of a 3 x 2 cell
        /// footprint at the grid's height: the pad is centred across the footprint, its back a hand
        /// short of the footprint's back edge, and stands on the diner's floor, 0.035 up. Its front
        /// faces -Z as the station's does, so it is not turned.
        /// </summary>
        internal static readonly Vector3 Place = new Vector3(1.5f, 0.035f, 0.8f);

        // --- Measurements (metres; front toward -Z) ------------------------------------------------

        const float PadLeft = -1.25f, PadRight = 1.25f, PadFront = -0.6f, PadBack = 1.15f, PadTop = 0.047f;

        /// <summary>The cold well cabinet along the back: plinth, painted body, a steel top round the well.</summary>
        const float CabLeft = -1.2f, CabRight = 0.58f, CabFront = 0.42f, CabBack = 1.1f, CabTop = 0.86f, SteelTop = 0.91f;
        const float PlinthTop = PadTop + 0.09f;
        const float WellLeft = CabLeft + 0.07f, WellRight = CabRight - 0.07f, WellFront = CabFront + 0.07f, WellBack = CabBack - 0.09f;

        /// <summary>The oak worktop round the right, the length of the corner.</summary>
        const float WorkLeft = 0.62f, WorkRight = 1.22f, WorkFront = -0.52f, WorkBack = 1.1f, WorkTop = 0.88f, WorkShelf = 0.2f;

        const float BoardX = 0.9f, BoardZ = -0.27f, BoardWidth = 0.3f, BoardDepth = 0.4f, BoardTop = WorkTop + 0.025f;

        /// <summary>The knife's tip, on the board, and the carrot it is cutting, lying across the board.</summary>
        const float KnifeX = BoardX - 0.01f, KnifeZ = BoardZ + 0.13f;
        const float CarrotZ = BoardZ + 0.05f;

        const float BowlX = 0.9f, BowlZ = 0.23f, BowlRadius = 0.22f, BowlHeight = 0.14f;

        /// <summary>The middle of the heap of salad, a little below the bowl's rim.</summary>
        const float SaladY = WorkTop + BowlHeight - 0.03f;

        const float CruetX = 1.1f, CruetZ = 0.58f, CruetHeight = 0.2f;

        /// <summary>The overshelf above the cold well, on two steel posts standing up from the back of its top.</summary>
        const float ShelfLeft = CabLeft + 0.02f, ShelfRight = CabRight - 0.02f, ShelfY = 1.45f, ShelfFront = 0.9f, ShelfBack = 1.1f;
        const float PostZ = 1.05f;

        // --- The palette ---------------------------------------------------------------------------

        /// <summary>Sage quarry tiles, muted, so the corner reads apart from the soup kitchen's brick red and the blue-grey floor.</summary>
        static readonly Color[] Tiles = { B.Hex("6C8675"), B.Hex("728D7B"), B.Hex("67806F"), B.Hex("6F8B78") };
        static readonly Color Grout = B.Hex("5D6560");

        static readonly Color Sage = B.Hex("5D7D66");
        static readonly Color SageLight = B.Hex("698A72");
        static readonly Color Frame = B.Hex("ABA595");
        static readonly Color Steel = B.Hex("9EA4A9");
        static readonly Color SteelLight = B.Hex("AEB4B8");
        static readonly Color SteelDark = B.Hex("6E7479");
        static readonly Color WellDark = B.Hex("3C4044");
        static readonly Color IronDark = B.Hex("2A2C2F");

        static readonly Color[] Oak = { B.Hex("B39B7A"), B.Hex("AA9171"), B.Hex("BBA383"), B.Hex("A58C6C") };
        static readonly Color BoardOak = B.Hex("C2AB88");
        static readonly Color BowlWood = B.Hex("8A6A4C");
        static readonly Color BowlInside = B.Hex("A6865F");
        static readonly Color ServerWood = B.Hex("B59B76");

        static readonly Color Cream = B.Hex("AFA898");
        static readonly Color Teal = B.Hex("3D6762");
        static readonly Color TealLight = B.Hex("47756F");

        static readonly Color Leaf = B.Hex("5C8732");
        static readonly Color LeafLight = B.Hex("78A043");
        static readonly Color LeafPale = B.Hex("9DB463");
        static readonly Color LeafDark = B.Hex("456F2A");
        static readonly Color TomatoRed = B.Hex("B5302A");
        static readonly Color Carrot = B.Hex("C0601F");
        static readonly Color CarrotLight = B.Hex("CE772D");
        static readonly Color CarrotCore = B.Hex("D08A3E");
        static readonly Color CarrotTop = B.Hex("4E7A2C");
        static readonly Color Corn = B.Hex("C8922A");
        static readonly Color CornLight = B.Hex("D4A236");
        static readonly Color Bean = B.Hex("6E2621");
        static readonly Color BeanWhite = B.Hex("BDB6A2");

        /// <summary>A pale farmhouse cheese: kept well off the sweetcorn's gold so the two pans read apart.</summary>
        static readonly Color Cheese = B.Hex("C2AF78");
        static readonly Color CheeseLight = B.Hex("CDBC88");
        static readonly Color Chicken = B.Hex("A9763F");
        static readonly Color ChickenPale = B.Hex("C7B394");
        static readonly Color Grill = B.Hex("4A2E1C");
        static readonly Color Soil = B.Hex("4A3A2E");

        static readonly Color TinLabel = B.Hex("A33A32");
        static readonly Color TinBand = B.Hex("BDB4A0");
        static readonly Color GlassOil = B.Hex("8C8A3A");
        static readonly Color GlassGreen = B.Hex("3E5E3A");
        static readonly Color Vinegar = B.Hex("6A2A2C");
        static readonly Color Dressing = B.Hex("B8A04A");
        static readonly Color Slate = B.Hex("2F3533");
        static readonly Color Chalk = B.Hex("C9C6B8");
        static readonly Color ChalkGreen = B.Hex("8FB07A");

        [MenuItem("Last Orders/Kitchen/Build Salad Station (pfSaladStation)", priority = 101)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This builds the salad corner - cold well of ingredient pans, oak worktop, herb shelf, chalkboard menu, cool box, " +
                "tiled pad - and puts it into pfSaladStation in place of the prototype salad bar, which is switched off with its " +
                "painted floor shadows, not deleted.\n\n" +
                "While the station is making something, the knife chops a carrot, the servers toss the salad and the dressing " +
                "cruet pours now and then.\n\n" +
                "The station's components and centre point are kept; its collider is fitted to the corner.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        /// <summary>Builds the corner and puts it into the prefab, with no dialogs.</summary>
        public static string Apply()
        {
            ContentGenerator.EnsureFolder(Folder);

            Material painted = B.PaintedMaterial();
            Mesh corner = SaveMesh(Corner(), "SaladStation");
            Mesh knife = SaveMesh(KnifeModel(), "SaladStation_Knife");
            Mesh bit = SaveMesh(Round(), "SaladStation_CarrotRound");
            Mesh salad = SaveMesh(Salad(), "SaladStation_Salad");
            Mesh servers = SaveMesh(Servers(), "SaladStation_Servers");
            Mesh leaf = SaveMesh(TossedLeaf(), "SaladStation_Leaf");
            Mesh cruet = SaveMesh(Cruet(), "SaladStation_Cruet");
            Mesh stream = SaveMesh(Stream(), "SaladStation_Stream");

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
                go.transform.localRotation = Quaternion.identity;
                B.Show(go, corner, corner.subMeshCount > 1 ? new[] { painted, B.GlowMaterial() } : new[] { painted });

                Transform model = go.transform;

                // The knife stands on its edge, tip on the board, handle toward the front; it chops by lifting the handle.
                Transform knifePart = Part(model, "Knife", knife, new Vector3(KnifeX, BoardTop, KnifeZ), painted, ShadowCastingMode.On);

                var rounds = new Transform[6];
                for (int i = 0; i < rounds.Length; i++)
                {
                    rounds[i] = Part(model, $"CarrotRound_{i}", bit, new Vector3(KnifeX, BoardTop, CarrotZ), painted, ShadowCastingMode.Off);
                    rounds[i].gameObject.SetActive(false);
                }

                Transform saladPart = Part(model, "Salad", salad, new Vector3(BowlX, SaladY, BowlZ), painted, ShadowCastingMode.On);
                Transform serversPart = Part(model, "Servers", servers, new Vector3(BowlX, SaladY + 0.04f, BowlZ), painted, ShadowCastingMode.On);

                var leaves = new Transform[6];
                for (int i = 0; i < leaves.Length; i++)
                {
                    leaves[i] = Part(model, $"Leaf_{i}", leaf, new Vector3(BowlX, SaladY, BowlZ), painted, ShadowCastingMode.Off);
                    leaves[i].gameObject.SetActive(false);
                }

                Transform cruetPart = Part(model, "Cruet", cruet, new Vector3(CruetX, WorkTop, CruetZ), painted, ShadowCastingMode.On);

                // Held over the bowl's back edge and tipped toward it (-Z), its mouth a hand above the salad.
                var pour = new GameObject("CruetPour").transform;
                pour.SetParent(model, false);
                pour.localPosition = new Vector3(BowlX + 0.06f, WorkTop + 0.37f, BowlZ + 0.3f);
                pour.localRotation = Quaternion.Euler(-105f, 0f, 0f);

                Vector3 mouth = pour.localPosition + pour.localRotation * (Vector3.up * CruetHeight);
                Transform streamPart = Part(model, "Stream", stream, mouth, painted, ShadowCastingMode.Off);
                streamPart.localScale = new Vector3(1f, mouth.y - (SaladY + 0.05f), 1f);
                streamPart.gameObject.SetActive(false);

                go.AddComponent<SaladStationAnimation>().Configure(knifePart, rounds, new Vector3(KnifeX - 0.02f, BoardTop + 0.01f, CarrotZ),
                                                                   new Vector3(BoardX - 0.08f, BoardTop + 0.004f, BoardZ - 0.09f), saladPart, serversPart,
                                                                   leaves, cruetPart, pour, streamPart);

                Transform centre = root.transform.Find("CenterPoint");
                if (centre != null) centre.localPosition = new Vector3(Place.x, 0.5f, Place.z + PadFront);

                string colliderNote = FitCollider(root, model);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return $"[Last Orders] Salad corner built: {corner.vertexCount} vertices in the corner, knife, salad, servers, cruet and " +
                       $"{rounds.Length + leaves.Length} flying pieces; in pfSaladStation" +
                       $"{(old != null ? $"; the prototype '{OldModel}' model is switched off" : "")}" +
                       $"{(shadowsOff > 0 ? $", {shadowsOff} painted floor shadows switched off" : "")}{colliderNote}.";
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

            collider.size = new Vector3(PadRight - PadLeft, 1.2f, PadBack - PadFront);
            return $"; collider {before} x {beforeSize} -> {collider.center} x {collider.size}";
        }

        // --- The corner ---------------------------------------------------------------------------

        static B.Model Corner()
        {
            // A shade darker toward the floor, on everything standing on the pad but not on the tiles themselves.
            var m = new B.Model { Shade = (p, n) => p.y < PadTop + 0.01f ? 1f : Mathf.Lerp(0.84f, 1f, K.Smooth((p.y - PadTop) / 0.35f)) };

            Pad(m);
            Duckboard(m);
            ColdWell(m);
            Shelf(m);
            Chalkboard(m);
            Worktop(m);
            OnTheWorktop(m);
            CoolBox(m);
            BeanTins(m);

            return m;
        }

        /// <summary>Sage tiles on a grout bed, a shade apart from each other; none under the cabinet or the worktop.</summary>
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
                    bool underCabinet = x - tw * 0.5f > CabLeft && x + tw * 0.5f < CabRight && z - td * 0.5f > CabFront;
                    bool underWorktop = x - tw * 0.5f > WorkLeft && x + tw * 0.5f < WorkRight && z - td * 0.5f > WorkFront + 0.1f;
                    if (underCabinet || underWorktop) continue;

                    int key = j * across + i;
                    Color c = Tiles[B.Hash(key + 700) % Tiles.Length] * (1f + 0.035f * B.Jitter(key + 1300));
                    m.Box(c, new Vector3(x, PadTop - 0.007f, z), new Vector3(tw - 0.018f, 0.014f, td - 0.018f), 0.005f);
                }
            }
        }

        /// <summary>The slatted mat in front of the cold well, where the cook stands.</summary>
        static void Duckboard(B.Model m)
        {
            const float left = -0.55f, right = 0.5f, front = -0.5f, back = 0.32f;

            foreach (float x in new[] { left + 0.06f, (left + right) * 0.5f, right - 0.06f })
                m.Box(B.Walnut, new Vector3(x, PadTop + 0.014f, (front + back) * 0.5f), new Vector3(0.05f, 0.028f, back - front - 0.04f), 0.008f);

            const int slats = 6;
            float pitch = (back - front) / slats;
            for (int k = 0; k < slats; k++)
            {
                float z = front + (k + 0.5f) * pitch;
                Color c = Oak[B.Hash(k + 140) % Oak.Length] * (1f + 0.04f * B.Jitter(k + 141));
                m.Box(c, new Vector3((left + right) * 0.5f, PadTop + 0.04f, z), new Vector3(right - left, 0.024f, pitch - 0.03f), 0.008f);
            }
        }

        // --- The cold well ------------------------------------------------------------------------

        /// <summary>
        /// The cold well: a dark plinth, a cream-framed cabinet with three sage doors and steel bar
        /// handles, a steel tray rail along its front, and a steel top round a well of eight pans.
        /// </summary>
        static void ColdWell(B.Model m)
        {
            float mid = (CabLeft + CabRight) * 0.5f, w = CabRight - CabLeft;
            float zMid = (CabFront + CabBack) * 0.5f, d = CabBack - CabFront;

            m.Box(IronDark, new Vector3(mid, (PadTop + PlinthTop) * 0.5f, zMid + 0.03f), new Vector3(w - 0.06f, PlinthTop - PadTop, d - 0.06f), 0.01f);
            m.Box(Frame, new Vector3(mid, (PlinthTop + CabTop) * 0.5f, zMid), new Vector3(w, CabTop - PlinthTop, d), 0.025f, 2);

            const int doors = 3;
            float dw = (w - 0.08f) / doors;
            float doorBottom = PlinthTop + 0.05f, doorTop = CabTop - 0.11f;
            float dy = (doorBottom + doorTop) * 0.5f, dh = doorTop - doorBottom;
            for (int i = 0; i < doors; i++)
            {
                float x = CabLeft + 0.04f + (i + 0.5f) * dw;
                m.Box(Sage, new Vector3(x, dy, CabFront - 0.008f), new Vector3(dw - 0.03f, dh, 0.03f), 0.012f, 2);
                m.Box(SageLight, new Vector3(x, dy - 0.01f, CabFront - 0.024f), new Vector3(dw - 0.13f, dh - 0.14f, 0.012f), 0.008f, 2);

                // A bar handle across the top of the door, on two stand-offs.
                float hy = doorTop - 0.045f, hz = CabFront - 0.055f;
                m.Tube(Steel, new Vector3(x - 0.13f, hy, hz), new Vector3(x + 0.13f, hy, hz), 0.011f, 10);
                foreach (float sx in new[] { x - 0.1f, x + 0.1f })
                    m.Box(Steel, new Vector3(sx, hy, (hz + CabFront - 0.02f) * 0.5f), new Vector3(0.02f, 0.02f, CabFront - 0.02f - hz), 0.005f);

                // The cold air vent along its foot.
                for (int k = 0; k < 3; k++)
                    m.Box(IronDark, new Vector3(x, doorBottom + 0.035f + k * 0.022f, CabFront - 0.026f), new Vector3(dw - 0.2f, 0.01f, 0.01f), 0.003f);
            }

            // The tray rail: three steel tubes on brackets along the cabinet's front, under the lip of its top.
            const float railY = CabTop - 0.04f;
            float railZ = CabFront - 0.12f;
            for (int k = 0; k < 3; k++)
                m.Tube(SteelLight, new Vector3(CabLeft + 0.05f, railY, railZ + k * 0.035f), new Vector3(CabRight - 0.03f, railY, railZ + k * 0.035f), 0.009f, 8);
            foreach (float x in new[] { CabLeft + 0.12f, mid, CabRight - 0.1f })
                m.Box(Steel, new Vector3(x, railY - 0.012f, (railZ + CabFront) * 0.5f), new Vector3(0.025f, 0.018f, CabFront - railZ + 0.02f), 0.005f);

            // The steel top round the well, a lip over the front.
            float lip = CabFront - 0.025f;
            float topY = (CabTop + SteelTop) * 0.5f, topH = SteelTop - CabTop;
            m.Box(Steel, new Vector3(mid, topY, (lip + WellFront) * 0.5f), new Vector3(w + 0.02f, topH, WellFront - lip), 0.01f, 2);
            m.Box(Steel, new Vector3(mid, topY, (WellBack + CabBack) * 0.5f), new Vector3(w + 0.02f, topH, CabBack - WellBack), 0.01f, 2);
            m.Box(Steel, new Vector3((CabLeft - 0.01f + WellLeft) * 0.5f, topY, (WellFront + WellBack) * 0.5f),
                  new Vector3(WellLeft - CabLeft + 0.01f, topH, WellBack - WellFront + 0.01f), 0.01f, 2);
            m.Box(Steel, new Vector3((WellRight + CabRight + 0.01f) * 0.5f, topY, (WellFront + WellBack) * 0.5f),
                  new Vector3(CabRight + 0.01f - WellRight, topH, WellBack - WellFront + 0.01f), 0.01f, 2);

            m.Box(WellDark, new Vector3((WellLeft + WellRight) * 0.5f, CabTop + 0.002f, (WellFront + WellBack) * 0.5f),
                  new Vector3(WellRight - WellLeft, 0.01f, WellBack - WellFront), 0.003f);

            Pans(m);
        }

        enum Pan { Lettuce, Tomatoes, Sweetcorn, Carrot, Romaine, Cheese, Beans, Chicken }

        /// <summary>Eight pans in the well, two rows of four, each heaped with one thing the salads are made from.</summary>
        static void Pans(B.Model m)
        {
            Pan[] front = { Pan.Lettuce, Pan.Tomatoes, Pan.Sweetcorn, Pan.Carrot };
            Pan[] back = { Pan.Romaine, Pan.Cheese, Pan.Beans, Pan.Chicken };

            float pw = (WellRight - WellLeft) / 4f, pd = (WellBack - WellFront) / 2f;
            for (int row = 0; row < 2; row++)
            {
                for (int i = 0; i < 4; i++)
                {
                    var centre = new Vector3(WellLeft + (i + 0.5f) * pw, SteelTop, WellFront + (row == 0 ? 0.5f : 1.5f) * pd);
                    var half = new Vector2(pw * 0.5f - 0.008f, pd * 0.5f - 0.008f);
                    PanRim(m, centre, half);
                    Heap(m, row == 0 ? front[i] : back[i], centre, half - new Vector2(0.014f, 0.014f), row * 4 + i);
                }
            }
        }

        /// <summary>A pan's rolled steel rim, sitting on the well's edge.</summary>
        static void PanRim(B.Model m, Vector3 c, Vector2 half)
        {
            const float t = 0.012f;
            float y = c.y - 0.002f;
            m.Box(SteelLight, new Vector3(c.x, y, c.z - half.y + t * 0.5f), new Vector3(half.x * 2f, t, t), 0.004f);
            m.Box(SteelLight, new Vector3(c.x, y, c.z + half.y - t * 0.5f), new Vector3(half.x * 2f, t, t), 0.004f);
            m.Box(SteelLight, new Vector3(c.x - half.x + t * 0.5f, y, c.z), new Vector3(t, t, half.y * 2f - 2f * t), 0.004f);
            m.Box(SteelLight, new Vector3(c.x + half.x - t * 0.5f, y, c.z), new Vector3(t, t, half.y * 2f - 2f * t), 0.004f);
        }

        /// <summary>
        /// What a pan holds, heaped over its rim: a low mound in the food's own colour, so nothing shows
        /// through, with pieces of it scattered over the mound.
        /// </summary>
        static void Heap(B.Model m, Pan pan, Vector3 c, Vector2 half, int key)
        {
            const float rise = 0.03f;
            Color mound;
            switch (pan)
            {
                case Pan.Lettuce: mound = LeafLight; break;
                case Pan.Romaine: mound = Leaf; break;
                case Pan.Tomatoes: mound = TomatoRed * 0.9f; break;
                case Pan.Sweetcorn: mound = Corn; break;
                case Pan.Carrot: mound = Carrot; break;
                case Pan.Cheese: mound = Cheese; break;
                case Pan.Beans: mound = Bean; break;
                default: mound = Chicken; break;
            }

            // A pillow filling the pan to its rim, domed a little in the middle.
            m.Box(mound * 0.92f, c + Vector3.down * 0.01f, new Vector3(half.x * 2f, 0.03f, half.y * 2f), 0.012f, 2);
            K.Ball(m, (p, n) => mound, c + Vector3.down * 0.004f, new Vector3(half.x * 0.86f, rise * 0.75f, half.y * 0.8f), Quaternion.identity, 16, 6);

            // A point on the heap, and how high it stands there.
            Vector3 On(float u, float v)
            {
                float k = Mathf.Max(0f, 1f - u * u * 0.8f - v * v * 0.8f);
                return c + new Vector3(u * half.x * 0.9f, 0.004f + rise * 0.6f * Mathf.Sqrt(k), v * half.y * 0.86f);
            }

            var spots = new List<Vector3>();
            int count;
            switch (pan)
            {
                case Pan.Sweetcorn: count = 70; break;
                case Pan.Beans: count = 46; break;
                case Pan.Carrot: count = 48; break;
                case Pan.Tomatoes: count = 11; break;
                case Pan.Cheese: count = 28; break;
                case Pan.Chicken: count = 13; break;
                default: count = 26; break;
            }

            for (int i = 0; i < count; i++)
            {
                // Spread evenly over the pan, a little shaken up.
                float u = Mathf.Repeat(i * 0.618034f + 0.31f * ((B.Hash(key * 131 + i) % 1000) / 1000f), 1f) * 2f - 1f;
                float v = Mathf.Repeat(i / (float)count + 0.5f / count + 0.08f * B.Jitter(key * 173 + i), 1f) * 2f - 1f;
                spots.Add(On(u, v));
            }

            for (int i = 0; i < spots.Count; i++)
            {
                Vector3 at = spots[i];
                int k = key * 97 + i;
                Quaternion turn = Quaternion.Euler(20f * B.Jitter(k + 1), 180f * B.Jitter(k + 2), 20f * B.Jitter(k + 3));

                switch (pan)
                {
                    case Pan.Lettuce:
                    case Pan.Romaine:
                        Color leaf = pan == Pan.Romaine ? (i % 2 == 0 ? LeafDark : Leaf) : (i % 3 == 0 ? LeafPale : LeafLight);
                        K.Ball(m, (p, n) => leaf, at + Vector3.up * 0.006f, new Vector3(0.042f, 0.01f, 0.026f), turn, 10, 5);
                        break;
                    case Pan.Tomatoes:
                        K.Tomato(m, at - Vector3.up * 0.016f, 0.03f, k);
                        break;
                    case Pan.Sweetcorn:
                        Color kernel = i % 3 == 0 ? CornLight : Corn;
                        K.Ball(m, (p, n) => kernel, at + Vector3.up * 0.004f, new Vector3(0.012f, 0.009f, 0.011f), turn, 8, 4);
                        break;
                    case Pan.Carrot:
                        Vector3 along = turn * Vector3.right * 0.03f;
                        m.Tube(i % 2 == 0 ? Carrot : CarrotLight, at - along + Vector3.up * 0.005f, at + along + Vector3.up * 0.005f, 0.0065f, 5);
                        break;
                    case Pan.Cheese:
                        m.Box(i % 2 == 0 ? CheeseLight : Cheese, at + Vector3.up * 0.01f, Vector3.one * 0.028f, 0.005f, turn);
                        break;
                    case Pan.Beans:
                        Color bean = i % 3 == 0 ? BeanWhite : Bean;
                        K.Ball(m, (p, n) => bean, at + Vector3.up * 0.005f, new Vector3(0.018f, 0.01f, 0.011f), turn, 8, 4);
                        break;
                    case Pan.Chicken:
                        ChickenPiece(m, at + Vector3.up * 0.008f, turn);
                        break;
                }
            }
        }

        /// <summary>A strip of grilled chicken: golden, the grill's bars across its top, pale at its cut end.</summary>
        static void ChickenPiece(B.Model m, Vector3 at, Quaternion turn)
        {
            m.Box(Chicken, at, new Vector3(0.07f, 0.02f, 0.03f), 0.008f, turn);
            foreach (float x in new[] { -0.02f, 0f, 0.02f })
                m.Box(Grill, at + turn * new Vector3(x, 0.0095f, 0f), new Vector3(0.006f, 0.003f, 0.02f), 0.0012f, turn * Quaternion.Euler(0f, 25f, 0f));
            m.Box(ChickenPale, at + turn * new Vector3(0.034f, 0f, 0f), new Vector3(0.004f, 0.015f, 0.024f), 0.002f, turn);
        }

        // --- Over the cold well -------------------------------------------------------------------

        /// <summary>
        /// The overshelf above the cold well, on two steel posts standing up from the back of its top:
        /// two pots of herbs, a jar of pickles, oil and vinegar, a stack of cream bowls and tins; on a
        /// rail between the posts, tongs, a whisk, a grater and a ladle.
        /// </summary>
        static void Shelf(B.Model m)
        {
            float mid = (ShelfLeft + ShelfRight) * 0.5f, zMid = (ShelfFront + ShelfBack) * 0.5f;

            foreach (float x in new[] { ShelfLeft + 0.04f, ShelfRight - 0.04f })
            {
                m.Box(SteelDark, new Vector3(x, SteelTop + 0.006f, PostZ), new Vector3(0.07f, 0.012f, 0.07f), 0.004f);
                m.Tube(Steel, new Vector3(x, SteelTop, PostZ), new Vector3(x, ShelfY - 0.024f, PostZ), 0.016f, 10);
                m.Box(Steel, new Vector3(x, ShelfY - 0.034f, zMid), new Vector3(0.03f, 0.02f, ShelfBack - ShelfFront - 0.02f), 0.006f);
            }

            for (int i = 0; i < 2; i++)
            {
                float z = ShelfFront + (i + 0.5f) * (ShelfBack - ShelfFront) * 0.5f;
                m.Box(Oak[i + 1] * (1f + 0.03f * B.Jitter(i + 300)), new Vector3(mid, ShelfY - 0.012f, z),
                      new Vector3(ShelfRight - ShelfLeft, 0.024f, (ShelfBack - ShelfFront) * 0.5f - 0.004f), 0.006f);
            }

            float top = ShelfY;
            HerbPot(m, new Vector3(-1.02f, top, 1.03f), 0);
            HerbPot(m, new Vector3(-0.86f, top, 1.02f), 1);
            Jar(m, new Vector3(-0.66f, top, 1.03f));
            K.Bottle(m, new Vector3(-0.5f, top, 1.04f), 0.035f, 0.22f, GlassOil);
            K.Bottle(m, new Vector3(-0.41f, top, 1.03f), 0.032f, 0.2f, Vinegar);
            for (int k = 0; k < 4; k++) K.Bowl(m, new Vector3(-0.16f, top + k * 0.026f, 1.02f), 0.09f);
            K.Tin(m, new Vector3(0.08f, top, 1.04f), 0.036f, 0.09f, TinLabel);
            K.Tin(m, new Vector3(0.17f, top, 1.04f), 0.036f, 0.09f, TinLabel);
            K.Tin(m, new Vector3(0.125f, top + 0.09f, 1.04f), 0.036f, 0.09f, TinLabel);
            HerbPot(m, new Vector3(0.36f, top, 1.03f), 2);

            float railY = ShelfY - 0.09f, railZ = PostZ;
            m.Tube(Steel, new Vector3(ShelfLeft + 0.04f, railY, railZ), new Vector3(ShelfRight - 0.04f, railY, railZ), 0.008f, 8);
            Tongs(m, new Vector3(-0.8f, railY, railZ));
            Whisk(m, new Vector3(-0.55f, railY, railZ));
            Grater(m, new Vector3(-0.3f, railY, railZ));
            K.Ladle(m, new Vector3(0.0f, railY, railZ));
        }

        /// <summary>A cream enamel pot with a navy rim, dark soil and a bushy herb: leaves on stems round a dome.</summary>
        static void HerbPot(B.Model m, Vector3 foot, int key)
        {
            const float r = 0.055f, h = 0.085f;
            m.Lathe((p, n) => p.y - foot.y > h - 0.016f ? Teal : Cream, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r * 0.75f, 0f), new Vector2(r * 0.75f, 0f), new Vector2(r, h - 0.016f), new Vector2(r + 0.006f, h - 0.012f),
                new Vector2(r + 0.006f, h), new Vector2(r - 0.004f, h), new Vector2(r - 0.006f, h - 0.02f),
            }, 16);
            K.Disc(m, (p, n) => Soil, foot + Vector3.up * (h - 0.016f), Vector3.up, r - 0.006f, 14);

            for (int k = 0; k < 14; k++)
            {
                float a = (k * 137.5f + key * 40f) * Mathf.Deg2Rad;
                float ring = k < 5 ? 0.012f : k < 10 ? 0.034f : 0.05f;
                float y = h + (k < 5 ? 0.075f : k < 10 ? 0.055f : 0.03f) + 0.01f * B.Jitter(k + key * 20);
                Vector3 at = foot + new Vector3(Mathf.Cos(a) * ring, y, Mathf.Sin(a) * ring);
                m.Tube(LeafDark, foot + new Vector3(Mathf.Cos(a) * ring * 0.3f, h - 0.016f, Mathf.Sin(a) * ring * 0.3f), at, 0.003f, 5);
                Color leaf = k % 3 == 0 ? LeafLight : Leaf;
                K.Ball(m, (p, n) => leaf, at, new Vector3(0.026f, 0.01f, 0.017f),
                       Quaternion.Euler(-25f, -a * Mathf.Rad2Deg + 90f, 0f), 10, 6);
            }
        }

        /// <summary>A glass jar of pickles: green glass, a steel lid.</summary>
        static void Jar(B.Model m, Vector3 foot)
        {
            const float r = 0.045f, h = 0.13f;
            m.Lathe((p, n) => p.y - foot.y > h - 0.022f ? SteelDark : GlassGreen, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r * 0.9f, 0f), new Vector2(r, 0.01f), new Vector2(r, h - 0.03f), new Vector2(r * 0.86f, h - 0.022f),
                new Vector2(r * 0.9f, h - 0.022f), new Vector2(r * 0.9f, h), new Vector2(r * 0.9f, h), new Vector2(0f, h),
            }, 16);
        }

        static void Tongs(B.Model m, Vector3 hook)
        {
            K.Hook(m, hook);
            Vector3 top = hook - Vector3.up * 0.032f;
            foreach (float s in new[] { -1f, 1f })
                m.Tube(Steel, top, top + new Vector3(s * 0.022f, -0.2f, 0f), 0.006f, 6);
            m.Box(Steel, top - Vector3.up * 0.01f, new Vector3(0.022f, 0.018f, 0.012f), 0.005f);
        }

        static void Whisk(B.Model m, Vector3 hook)
        {
            K.Hook(m, hook);
            Vector3 top = hook - Vector3.up * 0.032f, neck = top - Vector3.up * 0.1f;
            m.Tube(B.Walnut, top, neck, 0.011f, 8);
            // Wire loops from the neck down and back, each turned round from the last.
            for (int k = 0; k < 4; k++)
            {
                float a = k * 45f * Mathf.Deg2Rad;
                var across = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                const int steps = 10;
                Vector3 Loop(float t) => neck + across * (0.032f * Mathf.Sin(t * Mathf.PI * 2f)) - Vector3.up * (0.055f * (1f - Mathf.Cos(t * Mathf.PI * 2f)));
                for (int s = 0; s < steps; s++) m.Tube(SteelLight, Loop(s / (float)steps), Loop((s + 1) / (float)steps), 0.0025f, 4);
            }
        }

        static void Grater(B.Model m, Vector3 hook)
        {
            K.Hook(m, hook);
            Vector3 top = hook - Vector3.up * 0.032f;
            m.Tube(B.Walnut, top, top - Vector3.up * 0.04f, 0.01f, 8);
            Vector3 c = top - Vector3.up * 0.13f;
            m.Box(Steel, c, new Vector3(0.075f, 0.16f, 0.012f), 0.004f);
            for (int r = 0; r < 5; r++)
                for (int q = 0; q < 3; q++)
                    m.Box(SteelDark, c + new Vector3(-0.022f + q * 0.022f, -0.06f + r * 0.03f, -0.0065f), new Vector3(0.012f, 0.005f, 0.002f), 0.001f);
        }

        /// <summary>
        /// The chalkboard menu, stood on the back of the worktop and leant against the wall: a walnut
        /// frame, slate, a chalked heading, six chalked lines with their prices and a leaf drawn in green.
        /// </summary>
        static void Chalkboard(B.Model m)
        {
            const float width = 0.42f, height = 0.46f, cx = 0.92f, footZ = 1.0f;
            Matrix4x4 was = m.Transform;

            // Built flat in its own space - x across, y up its face, its face toward -Z - then stood up and leant back.
            m.Transform = was * Matrix4x4.TRS(new Vector3(cx, WorkTop, footZ), Quaternion.Euler(12f, 0f, 0f), Vector3.one);

            m.Box(Slate, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, 0.02f), 0.004f);
            const float f = 0.03f;
            m.Box(B.Walnut, new Vector3(0f, height - f * 0.5f, -0.006f), new Vector3(width + 0.01f, f, 0.032f), 0.006f);
            m.Box(B.Walnut, new Vector3(0f, f * 0.5f, -0.006f), new Vector3(width + 0.01f, f, 0.032f), 0.006f);
            m.Box(B.Walnut, new Vector3(-width * 0.5f + f * 0.5f, height * 0.5f, -0.006f), new Vector3(f, height, 0.032f), 0.006f);
            m.Box(B.Walnut, new Vector3(width * 0.5f - f * 0.5f, height * 0.5f, -0.006f), new Vector3(f, height, 0.032f), 0.006f);

            const float face = -0.012f;
            m.Box(Chalk, new Vector3(-0.03f, height - 0.075f, face), new Vector3(0.2f, 0.02f, 0.003f), 0.002f);
            for (int k = 0; k < 6; k++)
            {
                float y = height - 0.13f - k * 0.045f;
                float length = 0.13f + 0.08f * ((B.Hash(k + 60) % 100) / 100f);
                m.Box(Chalk, new Vector3(-width * 0.5f + 0.06f, y, face), new Vector3(0.012f, 0.012f, 0.003f), 0.003f);
                m.Box(Chalk * 0.92f, new Vector3(-width * 0.5f + 0.085f + length * 0.5f, y, face), new Vector3(length, 0.009f, 0.003f), 0.002f);
                m.Box(Chalk * 0.92f, new Vector3(width * 0.5f - 0.07f, y, face), new Vector3(0.032f, 0.009f, 0.003f), 0.002f);
            }

            K.Ball(m, (p, n) => ChalkGreen, new Vector3(width * 0.5f - 0.08f, height - 0.075f, face), new Vector3(0.032f, 0.017f, 0.003f),
                   Quaternion.Euler(0f, 0f, 30f), 10, 4);

            m.Transform = was;
        }

        // --- The worktop --------------------------------------------------------------------------

        /// <summary>Oak boards on walnut legs and aprons, a slatted shelf underneath with a crate of lettuces and a crate of carrots on it.</summary>
        static void Worktop(B.Model m)
        {
            float cx = (WorkLeft + WorkRight) * 0.5f, cz = (WorkFront + WorkBack) * 0.5f;
            float w = WorkRight - WorkLeft, d = WorkBack - WorkFront;
            float under = WorkTop - 0.05f;

            foreach (float x in new[] { WorkLeft + 0.04f, WorkRight - 0.04f })
                foreach (float z in new[] { WorkFront + 0.04f, cz, WorkBack - 0.04f })
                    m.Box(B.Walnut, new Vector3(x, (PadTop + under) * 0.5f, z), new Vector3(0.056f, under - PadTop, 0.056f), 0.012f);

            foreach (float x in new[] { WorkLeft + 0.035f, WorkRight - 0.035f })
                m.Box(B.Walnut, new Vector3(x, under - 0.045f, cz), new Vector3(0.026f, 0.09f, d - 0.08f), 0.008f);
            foreach (float z in new[] { WorkFront + 0.035f, WorkBack - 0.035f })
                m.Box(B.Walnut, new Vector3(cx, under - 0.045f, z), new Vector3(w - 0.08f, 0.09f, 0.026f), 0.008f);

            const int boards = 4;
            for (int i = 0; i < boards; i++)
            {
                float x = WorkLeft + (i + 0.5f) * w / boards;
                Color c = Oak[B.Hash(i + 210) % Oak.Length] * (1f + 0.04f * B.Jitter(i + 211));
                m.Box(c, new Vector3(x, WorkTop - 0.025f, cz), new Vector3(w / boards - 0.004f, 0.05f, d + 0.02f), 0.01f);
            }

            for (int i = 0; i < 4; i++)
            {
                float x = WorkLeft + 0.07f + i * (w - 0.14f) / 3f;
                m.Box(B.Plank(i + 260), new Vector3(x, WorkShelf, cz), new Vector3(0.11f, 0.024f, d - 0.06f), 0.008f);
            }

            float shelfTop = WorkShelf + 0.012f;
            LettuceCrate(m, new Vector3(cx, shelfTop, WorkFront + 0.36f));
            CarrotCrate(m, new Vector3(cx, shelfTop, WorkBack - 0.4f));
        }

        static void LettuceCrate(B.Model m, Vector3 foot)
        {
            var size = new Vector3(0.44f, 0.17f, 0.5f);
            K.OpenCrate(m, foot, size, 3f, 31);
            int key = 0;
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 3; j++)
                    LettuceHead(m, foot + new Vector3(-0.1f + i * 0.2f + 0.01f * B.Jitter(++key), size.y - 0.06f, -0.15f + j * 0.15f), 0.07f, key);
        }

        /// <summary>A round lettuce: a pale heart and a ring of leaves cupped round it.</summary>
        static void LettuceHead(B.Model m, Vector3 centre, float r, int key)
        {
            K.Ball(m, (p, n) => LeafPale, centre, new Vector3(r * 0.8f, r * 0.7f, r * 0.8f), Quaternion.identity, 12, 8);
            for (int k = 0; k < 6; k++)
            {
                float a = (k * 60f + key * 23f) * Mathf.Deg2Rad;
                var out1 = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Color leaf = k % 2 == 0 ? LeafLight : Leaf;
                K.Ball(m, (p, n) => leaf, centre + out1 * (r * 0.62f) + Vector3.up * (r * 0.05f), new Vector3(r * 0.62f, r * 0.62f, r * 0.14f),
                       Quaternion.LookRotation(out1) * Quaternion.Euler(-20f, 0f, 0f), 10, 6);
            }
        }

        static void CarrotCrate(B.Model m, Vector3 foot)
        {
            var size = new Vector3(0.44f, 0.15f, 0.42f);
            K.OpenCrate(m, foot, size, -4f, 37);
            Quaternion turn = Quaternion.Euler(0f, -4f, 0f);
            for (int k = 0; k < 9; k++)
            {
                float x = -0.16f + (k % 5) * 0.08f + 0.01f * B.Jitter(k + 400);
                float z = k < 5 ? -0.08f : 0.06f;
                Vector3 crown = foot + turn * new Vector3(x, size.y - 0.02f + (k < 5 ? 0f : 0.025f), z + 0.12f);
                Vector3 tip = foot + turn * new Vector3(x + 0.02f * B.Jitter(k + 410), size.y - 0.03f, z - 0.12f);
                m.Tube((p, n) => Carrot, crown, tip, 0.024f, 0.006f, 10);
                for (int f = 0; f < 3; f++)
                    m.Tube(CarrotTop, crown, crown + new Vector3(0.03f * (f - 1), 0.07f + 0.02f * B.Jitter(k * 3 + f), 0.06f + 0.02f * B.Jitter(k * 5 + f)), 0.005f, 5);
            }
        }

        /// <summary>
        /// On the worktop, front to back: the board with a carrot being cut and the rounds piled
        /// beside it (the knife is its own part), the big wooden salad bowl (its salad and servers
        /// are their own parts), the colander of washed leaves, salt and pepper, and a stack of
        /// plates ready at the back. The cruet stands beside the bowl, its own part.
        /// </summary>
        static void OnTheWorktop(B.Model m)
        {
            Quaternion boardTurn = Quaternion.Euler(0f, -4f, 0f);
            m.Box(BoardOak, new Vector3(BoardX, WorkTop + 0.0125f, BoardZ), new Vector3(BoardWidth, 0.025f, BoardDepth), 0.008f, boardTurn, 2);

            // The carrot lies across the board from the left, cut off square where the knife stands.
            var cut = new Vector3(KnifeX - 0.012f, BoardTop + 0.02f, CarrotZ);
            var crown = new Vector3(BoardX - 0.14f, BoardTop + 0.022f, CarrotZ + 0.01f);
            m.Tube((p, n) => n.x > 0.8f ? CarrotCore : Carrot, crown, cut, 0.022f, 0.018f, 12);
            K.Disc(m, (p, n) => CarrotCore, cut + Vector3.right * 0.0005f, Vector3.right, 0.017f, 12);
            for (int f = 0; f < 3; f++)
                m.Tube(CarrotTop, crown, crown + new Vector3(-0.08f, 0.02f + 0.01f * f, -0.03f + f * 0.03f), 0.004f, 5);

            // Rounds already cut, in a pile in front of the knife.
            for (int k = 0; k < 9; k++)
            {
                var at = new Vector3(BoardX - 0.08f + 0.035f * B.Jitter(k + 500), BoardTop + 0.004f + (k > 5 ? 0.006f : 0f), BoardZ - 0.09f + 0.035f * B.Jitter(k + 510));
                RoundOn(m, at, Quaternion.Euler(8f * B.Jitter(k + 520), 0f, 8f * B.Jitter(k + 530)));
            }

            // Shredded lettuce at the board's front end.
            for (int k = 0; k < 7; k++)
            {
                Color leaf = k % 2 == 0 ? LeafLight : LeafPale;
                var at = new Vector3(BoardX + 0.08f + 0.03f * B.Jitter(k + 540), BoardTop + 0.006f, BoardZ - 0.12f + 0.03f * B.Jitter(k + 550));
                K.Ball(m, (p, n) => leaf, at, new Vector3(0.03f, 0.007f, 0.015f), Quaternion.Euler(0f, 60f * B.Jitter(k + 560), 0f), 10, 5);
            }

            SaladBowl(m);
            Colander(m, new Vector3(0.8f, WorkTop, 0.74f));
            for (int k = 0; k < 4; k++) Plate(m, new Vector3(1.09f, WorkTop + k * 0.018f, 0.79f), 0.11f);
            K.PepperMill(m, new Vector3(0.7f, WorkTop, 0.93f));
            K.Crock(m, new Vector3(0.8f, WorkTop, 0.93f), 0.045f, 0.09f);
        }

        /// <summary>A round of carrot lying flat: orange rim, paler face.</summary>
        static void RoundOn(B.Model m, Vector3 foot, Quaternion turn)
        {
            var shape = new SmoothMesh(1);
            shape.Lathe(Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.016f, 0f), new Vector2(0.016f, 0f), new Vector2(0.016f, 0.007f), new Vector2(0.016f, 0.007f),
                new Vector2(0f, 0.007f),
            }, new[] { 0 }, 12);
            Matrix4x4 was = m.Transform;
            m.Transform = was * Matrix4x4.TRS(foot, turn, Vector3.one);
            m.Add(shape, (p, n) => n.y > 0.8f ? (new Vector2(p.x, p.z).magnitude < 0.008f ? CarrotCore : CarrotLight) : Carrot);
            m.Transform = was;
        }

        /// <summary>The big wooden salad bowl: turned walnut outside, paler inside, on a low foot.</summary>
        static void SaladBowl(B.Model m)
        {
            var foot = new Vector3(BowlX, WorkTop, BowlZ);
            const float r = BowlRadius, h = BowlHeight;
            m.Lathe((p, n) =>
            {
                float rr = new Vector2(p.x - foot.x, p.z - foot.z).magnitude;
                float y = p.y - foot.y;
                bool inside = y > 0.03f && rr < r - 0.012f && n.y > -0.2f && (n.x * (p.x - foot.x) + n.z * (p.z - foot.z)) < 0f;
                return inside ? BowlInside : y > h - 0.008f ? BowlInside : BowlWood;
            }, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r * 0.45f, 0f), new Vector2(r * 0.47f, 0.012f), new Vector2(r * 0.55f, 0.016f),
                new Vector2(r * 0.85f, h * 0.35f), new Vector2(r * 0.98f, h * 0.75f), new Vector2(r, h - 0.004f), new Vector2(r - 0.006f, h),
                new Vector2(r - 0.014f, h - 0.006f), new Vector2(r - 0.016f, h - 0.03f), new Vector2(r * 0.8f, h * 0.35f), new Vector2(r * 0.45f, 0.03f),
                new Vector2(0f, 0.028f),
            }, 32);
        }

        /// <summary>A steel colander of washed leaves on three little feet.</summary>
        static void Colander(B.Model m, Vector3 foot)
        {
            const float r = 0.13f, h = 0.1f;
            for (int k = 0; k < 3; k++)
            {
                float a = k * 120f * Mathf.Deg2Rad;
                m.Box(SteelDark, foot + new Vector3(Mathf.Cos(a) * 0.06f, 0.008f, Mathf.Sin(a) * 0.06f), new Vector3(0.02f, 0.016f, 0.02f), 0.005f);
            }

            Vector3 b = foot + Vector3.up * 0.014f;
            m.Lathe((p, n) =>
            {
                // Rows of holes round the sides, as dark dots.
                float y = p.y - b.y;
                float a = Mathf.Atan2(p.z - b.z, p.x - b.x);
                bool hole = y > 0.02f && y < h - 0.02f && Mathf.Repeat(a * 9f / Mathf.PI, 1f) < 0.3f && Mathf.Repeat(y / 0.022f, 1f) < 0.35f;
                return hole ? SteelDark : Steel;
            }, b, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r * 0.55f, 0f), new Vector2(r * 0.85f, h * 0.4f), new Vector2(r, h), new Vector2(r + 0.006f, h + 0.004f),
                new Vector2(r - 0.004f, h + 0.002f), new Vector2(r - 0.006f, h - 0.02f),
            }, 24);

            foreach (float s in new[] { -1f, 1f })
                m.Box(Steel, b + new Vector3(s * (r + 0.02f), h - 0.01f, 0f), new Vector3(0.04f, 0.01f, 0.03f), 0.004f);

            K.Ball(m, (p, n) => LeafLight, b + Vector3.up * (h - 0.012f), new Vector3(r - 0.012f, 0.03f, r - 0.012f), Quaternion.identity, 16, 8);
            for (int k = 0; k < 9; k++)
            {
                float a = k * 40f * Mathf.Deg2Rad;
                float ring = k == 0 ? 0f : 0.07f;
                Color leaf = k % 3 == 0 ? LeafPale : Leaf;
                K.Ball(m, (p, n) => leaf, b + new Vector3(Mathf.Cos(a) * ring, h + 0.012f, Mathf.Sin(a) * ring), new Vector3(0.05f, 0.012f, 0.032f),
                       Quaternion.Euler(-15f, -a * Mathf.Rad2Deg, 0f), 10, 5);
            }
        }

        /// <summary>A diner plate: cream glaze, the teal band round its rim.</summary>
        static void Plate(B.Model m, Vector3 foot, float r)
        {
            m.Lathe((p, n) => new Vector2(p.x - foot.x, p.z - foot.z).magnitude > r * 0.82f ? Teal : Cream, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r * 0.5f, 0f), new Vector2(r * 0.9f, 0.01f), new Vector2(r, 0.016f), new Vector2(r * 0.96f, 0.018f),
                new Vector2(r * 0.6f, 0.008f), new Vector2(0f, 0.007f),
            }, 24);
        }

        // --- The floor ----------------------------------------------------------------------------

        /// <summary>A cream enamel cool box with a teal lid, steel handles at its ends and a latch at the front.</summary>
        static void CoolBox(B.Model m)
        {
            var foot = new Vector3(-0.9f, PadTop, -0.28f);
            const float w = 0.52f, h = 0.3f, d = 0.36f;
            Quaternion turn = Quaternion.Euler(0f, 6f, 0f);

            m.Box(Cream, foot + turn * new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), 0.03f, turn, 2);
            m.Box(TealLight, foot + turn * new Vector3(0f, 0.025f, 0f), new Vector3(w + 0.008f, 0.05f, d + 0.008f), 0.02f, turn, 2);
            m.Box(Teal, foot + turn * new Vector3(0f, h + 0.03f, 0f), new Vector3(w + 0.02f, 0.06f, d + 0.02f), 0.03f, turn, 2);

            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 side = foot + turn * new Vector3(s * (w * 0.5f + 0.02f), h - 0.06f, 0f);
                m.Tube(Steel, side + turn * new Vector3(0f, 0f, -0.08f), side + turn * new Vector3(0f, 0f, 0.08f), 0.011f, 8);
                foreach (float z in new[] { -0.08f, 0.08f })
                    m.Box(Steel, foot + turn * new Vector3(s * (w * 0.5f + 0.01f), h - 0.06f, z), new Vector3(0.025f, 0.03f, 0.02f), 0.005f, turn);
            }

            m.Box(SteelLight, foot + turn * new Vector3(0f, h + 0.005f, -d * 0.5f - 0.012f), new Vector3(0.07f, 0.06f, 0.014f), 0.005f, turn);
        }

        /// <summary>Tins of beans stacked three, two, one beside the cool box.</summary>
        static void BeanTins(B.Model m)
        {
            const float r = 0.045f, h = 0.11f, gap = 0.094f;
            var foot = new Vector3(-0.95f, PadTop, 0.14f);
            for (int layer = 0; layer < 3; layer++)
            {
                int count = 3 - layer;
                for (int i = 0; i < count; i++)
                {
                    float x = foot.x + (i - (count - 1) * 0.5f) * gap;
                    BeanTin(m, new Vector3(x, foot.y + layer * (h + 0.002f), foot.z + 0.004f * B.Jitter(layer * 5 + i)), r, h);
                }
            }
        }

        /// <summary>A tin with steel rims and a red label, a cream band round its middle.</summary>
        static void BeanTin(B.Model m, Vector3 foot, float r, float h)
        {
            m.Lathe((p, n) =>
            {
                float y = p.y - foot.y;
                if (y < 0.012f || y > h - 0.012f || n.y > 0.8f) return Steel;
                return y > h * 0.42f && y < h * 0.62f ? TinBand : TinLabel;
            }, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r - 0.003f, 0f), new Vector2(r, 0.004f), new Vector2(r + 0.002f, 0.01f), new Vector2(r, 0.014f),
                new Vector2(r, h - 0.014f), new Vector2(r + 0.002f, h - 0.01f), new Vector2(r, h - 0.004f), new Vector2(r - 0.003f, h), new Vector2(0f, h),
            }, 16);
        }

        // --- The moving parts ---------------------------------------------------------------------

        /// <summary>
        /// The cook's knife, in a space whose origin is the tip of its blade on the board: the blade
        /// standing on its edge back along -Z to a walnut handle, so lifting the handle (turning about
        /// X) rocks it on its tip.
        /// </summary>
        static B.Model KnifeModel()
        {
            var m = new B.Model();
            const float blade = 0.2f, deep = 0.045f;

            // The blade, deepest at the heel and stepping down to the tip; its edge a lighter line.
            m.Box(Steel, new Vector3(0f, deep * 0.5f, -blade * 0.62f), new Vector3(0.004f, deep * 0.92f, blade * 0.76f), 0.0018f);
            m.Box(Steel, new Vector3(0f, deep * 0.32f, -blade * 0.16f), new Vector3(0.0035f, deep * 0.6f, blade * 0.3f), 0.0015f,
                  Quaternion.Euler(-8f, 0f, 0f));
            m.Box(SteelLight, new Vector3(0f, 0.003f, -blade * 0.5f), new Vector3(0.0045f, 0.005f, blade * 0.96f), 0.0015f);
            m.Box(B.Walnut, new Vector3(0f, deep * 0.62f, -blade - 0.06f), new Vector3(0.022f, 0.026f, 0.12f), 0.009f);
            foreach (float z in new[] { -blade - 0.03f, -blade - 0.09f })
                m.Box(SteelLight, new Vector3(0f, deep * 0.62f, z), new Vector3(0.024f, 0.006f, 0.006f), 0.002f);
            return m;
        }

        /// <summary>A round of carrot, its origin under its middle.</summary>
        static B.Model Round()
        {
            var m = new B.Model();
            RoundOn(m, Vector3.zero, Quaternion.identity);
            return m;
        }

        /// <summary>
        /// The salad in the big bowl, its origin in the middle of the heap: a mound of leaves with
        /// cherry tomatoes, carrot and sweetcorn over it - so that turning it shows it being tossed.
        /// </summary>
        static B.Model Salad()
        {
            var m = new B.Model();
            K.Ball(m, (p, n) => LeafLight, Vector3.zero, new Vector3(0.19f, 0.06f, 0.19f), Quaternion.identity, 22, 9);

            for (int k = 0; k < 26; k++)
            {
                float a = k * 137.5f * Mathf.Deg2Rad;
                float r = 0.17f * Mathf.Sqrt((k + 0.5f) / 26f);
                float y = 0.06f * Mathf.Sqrt(Mathf.Max(0f, 1f - (r / 0.19f) * (r / 0.19f)));
                Color leaf = k % 4 == 0 ? LeafPale : k % 3 == 0 ? Leaf : LeafLight;
                var at = new Vector3(Mathf.Cos(a) * r, y + 0.006f, Mathf.Sin(a) * r);
                K.Ball(m, (p, n) => leaf, at, new Vector3(0.05f, 0.011f, 0.032f), Quaternion.Euler(-20f * r / 0.17f, -a * Mathf.Rad2Deg, 12f * B.Jitter(k)), 10, 5);
            }

            foreach (float a in new[] { 20f, 110f, 200f, 290f, 160f })
            {
                float rad = a * Mathf.Deg2Rad, r = a == 160f ? 0.03f : 0.1f;
                K.Tomato(m, new Vector3(Mathf.Cos(rad) * r, 0.05f - r * 0.2f, Mathf.Sin(rad) * r), 0.024f, (int)a);
            }

            for (int k = 0; k < 14; k++)
            {
                float a = (k * 77f + 13f) * Mathf.Deg2Rad, r = 0.04f + 0.1f * ((B.Hash(k + 600) % 100) / 100f);
                var at = new Vector3(Mathf.Cos(a) * r, 0.062f - r * 0.18f, Mathf.Sin(a) * r);
                if (k % 2 == 0)
                {
                    Vector3 along = Quaternion.Euler(0f, k * 47f, 0f) * Vector3.right * 0.024f;
                    m.Tube(Carrot, at - along, at + along, 0.005f, 5);
                }
                else K.Ball(m, (p, n) => Corn, at, new Vector3(0.011f, 0.008f, 0.01f), Quaternion.identity, 8, 5);
            }

            return m;
        }

        /// <summary>
        /// The salad servers, a wooden spoon and fork standing in the salad, in a space whose origin is
        /// the middle of the bowl, their handles leaning out over the rim toward the front.
        /// </summary>
        static B.Model Servers()
        {
            var m = new B.Model();
            foreach (float s in new[] { -1f, 1f })
            {
                var head = new Vector3(s * 0.06f, -0.02f, -0.03f);
                var end = new Vector3(s * 0.11f, 0.24f, -0.17f);
                Vector3 dir = (end - head).normalized;
                m.Tube((p, n) => ServerWood, head + dir * 0.05f, end, 0.011f, 0.013f, 8);
                K.Ball(m, (p, n) => ServerWood * 0.96f, end + dir * 0.006f, Vector3.one * 0.014f, Quaternion.identity, 8, 6);

                Quaternion lay = Quaternion.FromToRotation(Vector3.up, dir);
                if (s < 0f) K.Ball(m, (p, n) => ServerWood * 0.94f, head + dir * 0.02f, new Vector3(0.036f, 0.05f, 0.014f), lay, 12, 8);
                else
                {
                    m.Box(ServerWood * 0.94f, head + dir * 0.04f, new Vector3(0.06f, 0.03f, 0.012f), 0.005f, lay);
                    for (int t = -1; t <= 1; t++)
                        m.Box(ServerWood * 0.94f, head + dir * 0.0f + lay * new Vector3(t * 0.02f, -0.01f, 0f), new Vector3(0.012f, 0.05f, 0.01f), 0.004f, lay);
                }
            }

            return m;
        }

        /// <summary>A loose leaf, tossed up out of the bowl: pale on its face, greener underneath.</summary>
        static B.Model TossedLeaf()
        {
            var m = new B.Model();
            K.Ball(m, (p, n) => n.y > 0f ? LeafLight : Leaf, Vector3.zero, new Vector3(0.042f, 0.009f, 0.028f), Quaternion.identity, 10, 6);
            return m;
        }

        /// <summary>The dressing cruet, its origin under its middle: a glass bottle of golden dressing with a cork.</summary>
        static B.Model Cruet()
        {
            var m = new B.Model();
            K.Bottle(m, Vector3.zero, 0.038f, CruetHeight, GlassOil);
            return m;
        }

        /// <summary>The stream of dressing, a metre long down -Y from the cruet's mouth; the animation scales it to the drop.</summary>
        static B.Model Stream()
        {
            var m = new B.Model();
            m.Tube((p, n) => Dressing, Vector3.zero, Vector3.down, 0.0045f, 0.0035f, 8);
            return m;
        }
    }
}
