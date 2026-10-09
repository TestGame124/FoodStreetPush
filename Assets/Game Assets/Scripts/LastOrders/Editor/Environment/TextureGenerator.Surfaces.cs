using System.Collections.Generic;
using LastOrders.Core;
using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The built surfaces' textures: the road with its markings painted in, the suburbs' streets,
    /// kerbs, pavements, the car park with its bays, the dirt path, the compound's block wall, the
    /// diner's walls, and the leaf pattern the toon foliage shader lays over the trees.
    ///
    /// Painted in metres, on a canvas that knows how many pixels a metre is along each axis, so a
    /// line 12 cm wide is 12 cm wide whatever the texture's shape. Markings that were separate
    /// meshes - every dash, every bay line, the wall's course line - are painted in here instead:
    /// one draw where there were dozens, and no strips floating a centimetre above the surface.
    /// </summary>
    public static partial class TextureGenerator
    {
        public const string RoadPath = Folder + "/Road_Asphalt.png";
        public const string AsphaltPath = Folder + "/Asphalt_Plain.png";
        public const string StreetPath = Folder + "/Street_Faded.png";
        public const string KerbPath = Folder + "/Kerb_Concrete.png";
        public const string PavementPath = Folder + "/Pavement_Slabs.png";
        public const string DirtPathPath = Folder + "/Path_Dirt.png";
        public const string ParkingPath = Folder + "/Parking_Apron.png";
        public const string CompoundWallPath = Folder + "/Wall_ConcreteBlock.png";
        public const string PlankWallPath = Folder + "/Wall_Planks.png";
        public const string TinWallPath = Folder + "/Wall_Tin.png";
        public const string DinerWallPath = Folder + "/Wall_Diner.png";
        public const string LeavesPath = Folder + "/Foliage_Leaves.png";

        /// <summary>Metres of road one copy of its texture covers: four dashes.</summary>
        public const float RoadTile = 12.8f;

        /// <summary>Metres of street one copy covers, along and across (the street is 6 m wide).</summary>
        public const float StreetTile = 6f;

        public const float AsphaltTile = 4f;
        public const float KerbTile = 1f;
        public const float PavementTile = 2.6f;
        public const float DirtPathTile = 3f;

        /// <summary>The compound wall's texture: 2.4 m along, the wall's whole 2.1 m height up.</summary>
        public const float WallTileLength = 2.4f;

        public const float WallHeight = 2.1f;

        /// <summary>Where the wall's bands change, in metres up: the footing's top, the cap's bottom.</summary>
        public const float WallFootingTop = 0.22f;

        public const float WallCapBottom = 1.98f;

        const string SurfaceVersion = "surface-lowpoly-2";

        // The level's light brightens a lit surface by about half again, so these are darker than
        // they show - see the grass. The tarmac leans violet, so the warm sun and the cool grade in
        // the shadows turn it the soft grey-mauve of painted asphalt, neither brown nor blue.
        // A cool neutral slate: the painted look's mauve read as purple under plain daylight.
        static readonly Color AsphaltBase = new Color(0.3f, 0.31f, 0.33f);
        static readonly Color PaintWhite = new Color(0.88f, 0.87f, 0.82f);

        // --- Roads -------------------------------------------------------------------------------------

        /// <summary>
        /// The visitors' road: 12.8 m of it along U, the 5 m carriageway across V. Asphalt with
        /// broad soft patches and a scatter of rounded stones in it, darker wheel tracks in each
        /// lane, a solid edge line along both sides, four centre dashes to the tile (the 3.2 m
        /// spacing the old dash meshes had), one repair patch and a few cracks.
        /// </summary>
        public static Texture2D EnsureRoad()
        {
            Texture2D existing = CurrentRelief(RoadPath, SurfaceVersion);
            if (existing != null) return existing;

            ContentGenerator.EnsureFolder(Folder);

            const float across = 5f;
            var rng = new DeterministicRandom(8101);
            Relief r = PaintAsphalt(2048, 1024, RoadTile, across, false, AsphaltBase, ref rng);
            Canvas c = Canvas.Over(r, wrapV: false);

            // Wheel tracks: where the tyres run, a shade darker.
            foreach (float lane in new[] { across * 0.25f, across * 0.75f })
                foreach (float track in new[] { -0.75f, 0.75f })
                    c.SoftBand(lane + track, 0.2f, 0.12f, AsphaltBase * 0.9f, 0.55f);

            // A patched trench across one lane, darker and edged.
            c.Rect(7.4f, 3.0f, 8.5f, 3.75f, AsphaltBase * 0.91f, 1f);
            c.RectOutline(7.4f, 3.0f, 8.5f, 3.75f, 0.022f, AsphaltBase * 0.76f, 0.8f);

            PaintCracks(r, 3, AsphaltBase * 0.72f, ref rng);

            // Edge lines, chipped here and there.
            var wear = new Periodic(256, new[] { 16, 32 }, new[] { 0.6f, 0.4f }, ref rng);
            foreach (float v in new[] { 0.21f, across - 0.21f })
                c.WornStrip(0f, RoadTile, v - 0.06f, v + 0.06f, PaintWhite, 0.95f, wear, 0.9f);

            // Centre dashes: 1.4 m of paint, 1.8 m of gap.
            for (float u = 0.2f; u < RoadTile; u += 3.2f)
                c.WornStrip(u, u + 1.4f, across * 0.5f - 0.07f, across * 0.5f + 0.07f, PaintWhite, 0.95f, wear, 0.9f);

            return SaveRelief(RoadPath, r, 1f, TextureWrapMode.Repeat, TextureWrapMode.Clamp, SurfaceVersion);
        }

        /// <summary>A side street, 6 m square: asphalt, one faded centre dash, cracks. Nobody has painted it in a year.</summary>
        public static Texture2D EnsureStreet()
        {
            Texture2D existing = CurrentRelief(StreetPath, SurfaceVersion);
            if (existing != null) return existing;

            ContentGenerator.EnsureFolder(Folder);

            var rng = new DeterministicRandom(8202);
            Color street = AsphaltBase * 1.04f;
            Relief r = PaintAsphalt(1024, 1024, StreetTile, StreetTile, false, street, ref rng);
            Canvas c = Canvas.Over(r, wrapV: false);

            var wear = new Periodic(256, new[] { 8, 16 }, new[] { 0.5f, 0.5f }, ref rng);
            c.WornStrip(1.0f, 2.6f, 2.93f, 3.07f, new Color(0.66f, 0.63f, 0.55f), 0.75f, wear, 0.62f);

            PaintCracks(r, 4, street * 0.72f, ref rng);

            return SaveRelief(StreetPath, r, 1f, TextureWrapMode.Repeat, TextureWrapMode.Clamp, SurfaceVersion);
        }

        /// <summary>Plain asphalt, 4 m square: the drive, and anything else tarmac without markings.</summary>
        public static Texture2D EnsureAsphalt()
        {
            Texture2D existing = CurrentRelief(AsphaltPath, SurfaceVersion);
            if (existing != null) return existing;

            ContentGenerator.EnsureFolder(Folder);

            var rng = new DeterministicRandom(8303);
            Relief r = PaintAsphalt(512, 512, AsphaltTile, AsphaltTile, true, AsphaltBase, ref rng);
            PaintCracks(r, 2, AsphaltBase * 0.72f, ref rng);

            return SaveRelief(AsphaltPath, r, 1f, TextureWrapMode.Repeat, TextureWrapMode.Repeat, SurfaceVersion);
        }

        /// <summary>
        /// The car park: its asphalt with the bays painted on, covering exactly
        /// <paramref name="apron"/> (world metres, x and y are X and Z). Painted from the markings'
        /// own rectangles, so the paint is where the bays are. Always repainted: the layout decides
        /// it. The rectangles are kept with the texture, so <see cref="RepaintParking"/> can paint
        /// it again in a new style after the bay meshes they came from are gone.
        /// </summary>
        public static Texture2D PaintParking(Rect apron, IReadOnlyList<Rect> markings)
        {
            ContentGenerator.EnsureFolder(Folder);

            var rng = new DeterministicRandom(8404);
            Relief r = PaintAsphalt(1024, 1024, apron.width, apron.height, false, AsphaltBase, ref rng);
            Canvas c = Canvas.Over(r, wrapV: false);
            PaintCracks(r, 3, AsphaltBase * 0.72f, ref rng);

            // Oil where cars stand.
            foreach (Rect m in markings)
            {
                if (m.width > m.height) continue;
                // Only the side lines: a stain beside each, toward the bay's middle, most bays.
                if (rng.Chance(0.5f))
                    r.Stain(m.center.x - apron.xMin + rng.Range(-1.4f, 1.4f), m.center.y - apron.yMin + rng.Range(-0.8f, 0.8f),
                            rng.Range(0.25f, 0.45f), rng.Range(0.2f, 0.35f), rng.Range(0f, Mathf.PI), AsphaltBase * 0.8f, 0.6f, 0.4f);
            }

            var wear = new Periodic(256, new[] { 16, 32 }, new[] { 0.6f, 0.4f }, ref rng);
            foreach (Rect m in markings)
                c.WornStrip(m.xMin - apron.xMin, m.xMax - apron.xMin, m.yMin - apron.yMin, m.yMax - apron.yMin,
                            PaintWhite, 0.95f, wear, 0.9f);

            Texture2D painted = SaveRelief(ParkingPath, r, 1f, TextureWrapMode.Clamp, TextureWrapMode.Clamp, SurfaceVersion);

            var importer = AssetImporter.GetAtPath(ParkingPath);
            importer.userData = SurfaceVersion + "\n" + string.Join(";", Rects(apron, markings));
            importer.SaveAndReimport();
            return painted;
        }

        /// <summary>
        /// Paints the car park again from the rectangles <see cref="PaintParking"/> kept with it -
        /// or, the first time in a new style, with the previous style's car park. Null if there are
        /// none - a car park painted before they were kept.
        /// </summary>
        public static Texture2D RepaintParking()
        {
            // Not ??: an importer Unity has let go of reads as a "fake null", which ?? does not see.
            var importer = AssetImporter.GetAtPath(ParkingPath);
            if (importer == null) importer = AssetImporter.GetAtPath(EnvKit.PreviousTextureFolder + "/Parking_Apron.png");
            if (importer == null) return null;

            string[] lines = importer.userData.Split('\n');
            if (lines.Length < 2 || string.IsNullOrEmpty(lines[1])) return null;

            var rects = new List<Rect>();
            var invariant = System.Globalization.CultureInfo.InvariantCulture;
            foreach (string rect in lines[1].Split(';'))
            {
                string[] n = rect.Split(',');
                rects.Add(new Rect(float.Parse(n[0], invariant), float.Parse(n[1], invariant), float.Parse(n[2], invariant),
                                   float.Parse(n[3], invariant)));
            }

            return PaintParking(rects[0], rects.GetRange(1, rects.Count - 1));
        }

        static IEnumerable<string> Rects(Rect apron, IReadOnlyList<Rect> markings)
        {
            var invariant = System.Globalization.CultureInfo.InvariantCulture;
            string Format(Rect r) => string.Format(invariant, "{0:R},{1:R},{2:R},{3:R}", r.x, r.y, r.width, r.height);

            yield return Format(apron);
            foreach (Rect m in markings) yield return Format(m);
        }

        /// <summary>Kerbstones, a metre each: pale concrete with a rounded top edge and a joint at each end.</summary>
        public static Texture2D EnsureKerb()
        {
            Texture2D existing = CurrentRelief(KerbPath, SurfaceVersion);
            if (existing != null) return existing;

            ContentGenerator.EnsureFolder(Folder);

            var rng = new DeterministicRandom(8505);
            Color stone = Hex("A39E92");
            Relief r = TileFloor(256, KerbTile, 1, (i, j) => stone, Hex("66625A"), joint: 0.016f, bevel: 0.04f, height: 0.01f,
                                 variation: 0f, tilt: 0f, ref rng);
            PaintSpecks(r, 30, 0.008f, 0.016f, stone, ref rng);
            r.Occlude(0.03f, 0.008f, Hex("66625A"), 0.4f, Hex("BDB8AC"), 0.08f);

            return SaveRelief(KerbPath, r, 1f, TextureWrapMode.Repeat, TextureWrapMode.Repeat, SurfaceVersion);
        }

        /// <summary>Pavement: 1.3 m slabs, two by two, grouted, rounded at the edges, laid a little unevenly, the odd one cracked.</summary>
        public static Texture2D EnsurePavement()
        {
            Texture2D existing = CurrentRelief(PavementPath, SurfaceVersion);
            if (existing != null) return existing;

            ContentGenerator.EnsureFolder(Folder);

            var rng = new DeterministicRandom(8606);
            Color slab = Hex("A09A8D"), joint = Hex("69645B");
            Relief r = TileFloor(1024, PavementTile, 2, (i, j) => slab, joint, joint: 0.014f, bevel: 0.035f, height: 0.008f,
                                 variation: 0.05f, tilt: 0.012f, ref rng);

            Chips(r, PavementTile / 2f, 2, 2, Hex("6E6960"), ref rng);
            PaintSpecks(r, 90, 0.01f, 0.022f, slab, ref rng);
            for (int i = 0; i < 2; i++)
                Crack(r, new Vector2(rng.Range(0.3f, PavementTile - 0.3f), rng.Range(0.3f, PavementTile - 0.3f)), 6, 0.12f, 0.012f,
                      slab * 0.6f, ref rng);

            r.Occlude(0.03f, 0.008f, joint * 0.8f, 0.45f, Hex("ABA59A"), 0.08f);
            return SaveRelief(PavementPath, r, 1f, TextureWrapMode.Repeat, TextureWrapMode.Repeat, SurfaceVersion);
        }

        /// <summary>
        /// The path to the diner's door, 3 m square: brown earth packed in low humps, worn
        /// smooth, with rounded stones trodden into it - small pebbles everywhere, a few flat
        /// cobbles - each showing only its top.
        /// </summary>
        public static Texture2D EnsureDirtPath()
        {
            Texture2D existing = CurrentRelief(DirtPathPath, SurfaceVersion);
            if (existing != null) return existing;

            ContentGenerator.EnsureFolder(Folder);

            var rng = new DeterministicRandom(8707);
            var r = new Relief(1024, 1024, DirtPathTile, DirtPathTile);

            Color earth = Hex("7B6A55");
            Color[] earths = { Hex("836F58"), Hex("8C775E"), Hex("76644F"), Hex("937E63") };
            Color[] stones = { Hex("8E8B84"), Hex("99958D"), Hex("85827B"), Hex("A19D95"), Hex("8A877F") };
            r.Fill(earth);

            for (int i = 0; i < 70; i++)
            {
                float radius = rng.Range(0.18f, 0.4f);
                r.Swell(rng.Range(0f, DirtPathTile), rng.Range(0f, DirtPathTile), radius, radius * rng.Range(0.6f, 0.9f), 0.012f,
                        earths[rng.Range(0, earths.Length)], 0.8f, rng.Range(0f, Mathf.PI));
            }

            for (int i = 0; i < 9; i++)
            {
                float u = rng.Range(0f, DirtPathTile), v = rng.Range(0f, DirtPathTile);
                float radius = rng.Range(0.06f, 0.1f);
                r.Mound(u, v, radius, radius * rng.Range(0.65f, 0.85f), 0.014f, stones[rng.Range(0, stones.Length)] * 0.95f,
                        r.HeightAt(u, v) * 0.7f, rng.Range(0f, Mathf.PI));
            }

            for (int i = 0; i < 150; i++)
            {
                float u = rng.Range(0f, DirtPathTile), v = rng.Range(0f, DirtPathTile);
                float radius = rng.Range(0.015f, 0.04f);
                r.Mound(u, v, radius, radius * rng.Range(0.65f, 1f), radius * 0.55f, stones[rng.Range(0, stones.Length)],
                        r.HeightAt(u, v) * 0.8f, rng.Range(0f, Mathf.PI));
            }

            r.Occlude(0.025f, 0.008f, earth * 0.68f, 0.5f, Hex("ABA08C"), 0.1f);
            r.Occlude(0.15f, 0.012f, earth * 0.8f, 0.3f, Hex("9C8A6E"), 0.2f);
            return SaveRelief(DirtPathPath, r, 1f, TextureWrapMode.Repeat, TextureWrapMode.Repeat, SurfaceVersion);
        }

        // --- Walls ---------------------------------------------------------------------------------------

        /// <summary>What fills a compound wall section between its footing and its cap.</summary>
        public enum WallPanel
        {
            /// <summary>Tall light grey concrete slabs, two to the tile.</summary>
            Concrete,

            /// <summary>Weathered boards nailed to two battens: a stretch someone patched with timber.</summary>
            Planks,

            /// <summary>Grey corrugated sheet riveted on, rusting at the foot: a stretch patched with tin.</summary>
            Tin,
        }

        public static string WallPath(WallPanel panel) => panel switch
        {
            WallPanel.Planks => PlankWallPath,
            WallPanel.Tin => TinWallPath,
            _ => CompoundWallPath,
        };

        /// <summary>
        /// The compound wall, 2.4 m of it along U and its whole 2.1 m height up V: a darker concrete
        /// footing, the panel, and a pale cap - the same footing and cap whatever the panel, so
        /// sections of different kinds line up. The panel is tall light grey concrete slabs with rounded
        /// edges and a dark joint between, grimier toward the ground, or weathered boards, or
        /// corrugated tin. Painted with height, so the slabs' edges, the boards and the ribs catch
        /// the light. U repeats; V does not, so a face UV'd by height shows each band where it is.
        /// </summary>
        public static Texture2D EnsureCompoundWall(WallPanel panel = WallPanel.Concrete)
        {
            string path = WallPath(panel);
            Texture2D existing = CurrentRelief(path, SurfaceVersion);
            if (existing != null) return existing;

            ContentGenerator.EnsureFolder(Folder);

            var rng = new DeterministicRandom(8808 + (int)panel * 101);
            var r = new Relief(1024, 1024, WallTileLength, WallHeight, wrapV: false);
            r.Fill(Hex("5A5955"));

            if (panel == WallPanel.Planks) PaintPlanks(r, ref rng);
            else if (panel == WallPanel.Tin) PaintTin(r, ref rng);
            else PaintConcreteSlabs(r, ref rng);

            // The footing and the cap: plain concrete, rounded along the edges they show (the
            // footing meets the ground, the cap's top is its top face), the cap paler.
            r.Course(-0.1f, WallFootingTop, Hex("908E89"), 0.035f, 0.03f);
            r.Course(WallCapBottom, WallHeight + 0.1f, Hex("B8B7B1"), 0.03f, 0.025f);

            // A shade under the cap's overhang, and splashes of earth up the footing.
            r.ShadeBand(WallCapBottom - 0.06f, WallCapBottom, Hex("4A4946"), 0.35f);
            for (int i = 0; i < 6; i++)
            {
                float u = rng.Range(0f, WallTileLength);
                r.Stain(u, rng.Range(0.02f, 0.1f), rng.Range(0.15f, 0.35f), rng.Range(0.05f, 0.09f), 0f, Hex("6A5E4E"),
                        rng.Range(0.15f, 0.3f), 0.8f);
            }

            r.Occlude(0.03f, 0.008f, Hex("474643"), 0.4f, Color.white, 0.06f);
            return SaveRelief(path, r, 1f, TextureWrapMode.Repeat, TextureWrapMode.Clamp, SurfaceVersion);
        }

        /// <summary>Two slabs to the tile, each its own light grey, darker toward the ground, with rain streaks under the cap and a chipped corner or two.</summary>
        static void PaintConcreteSlabs(Relief r, ref DeterministicRandom rng)
        {
            // A plain light grey, a touch warm; any lighter went white where the sun hit it.
            Color cream = Hex("A3A29C");
            const float slab = WallTileLength / 2f;
            const float joint = 0.022f;

            for (int k = 0; k < 2; k++)
            {
                Color colour = cream * rng.Range(0.96f, 1.03f);
                r.Slab(k * slab + joint * 0.5f, WallFootingTop + joint, (k + 1) * slab - joint * 0.5f, WallCapBottom - joint * 0.5f,
                       colour, 0.045f, 0.02f, 0.02f);
            }

            // Grime rising off the ground, fading out by a metre up; a little lighter near the top.
            r.ShadeBand(WallFootingTop, 0.9f, Hex("85837C"), 0.32f, fadeUp: true);
            r.ShadeBand(1.4f, WallCapBottom, Hex("D6D5D0"), 0.18f, fadeUp: false);

            // Streaks run down from the cap.
            for (int i = 0; i < 7; i++)
            {
                float length = rng.Range(0.25f, 0.7f);
                r.Stain(rng.Range(0f, WallTileLength), WallCapBottom - length * 0.5f, rng.Range(0.03f, 0.07f), length * 0.5f, 0f,
                        Hex("96948D"), rng.Range(0.12f, 0.25f), 0.7f);
            }

            // Knocked corners.
            for (int i = 0; i < 3; i++)
            {
                float u = (rng.Range(0, 2) + (rng.Chance(0.5f) ? 0.03f : 0.97f)) * slab;
                float v = rng.Chance(0.5f) ? WallFootingTop + 0.06f : WallCapBottom - 0.06f;
                r.Dent(u, v, rng.Range(0.035f, 0.06f), 0.3f, Hex("8B8984"), 1f);
            }
        }

        /// <summary>Upright boards a hand wide, each its own weathered brown, grained along its length, on two cross battens with nail heads.</summary>
        static void PaintPlanks(Relief r, ref DeterministicRandom rng)
        {
            Color[] woods = { Hex("8A6B55"), Hex("946F58"), Hex("7F624E"), Hex("98765F"), Hex("876854") };
            const int boards = 12;
            const float board = WallTileLength / boards;
            const float gap = 0.012f;
            float top = WallCapBottom - 0.005f, bottom = WallFootingTop + 0.005f;

            for (int k = 0; k < boards; k++)
            {
                Color colour = woods[rng.Range(0, woods.Length)] * rng.Range(0.95f, 1.04f);
                float u0 = k * board + gap * 0.5f, u1 = (k + 1) * board - gap * 0.5f;
                r.Slab(u0, bottom, u1, top, colour, 0.014f, 0.012f, 0.004f);

                // Grain: long faint streaks, a knot or two.
                for (int g = 0; g < 4; g++)
                {
                    float length = rng.Range(0.3f, 0.9f);
                    r.Stain(rng.Range(u0 + 0.02f, u1 - 0.02f), rng.Range(bottom + length * 0.5f, top - length * 0.5f), 0.006f, length * 0.5f,
                            0f, colour * 0.8f, rng.Range(0.3f, 0.5f), 0.6f);
                }

                if (rng.Chance(0.4f)) r.Dent(rng.Range(u0 + 0.05f, u1 - 0.05f), rng.Range(bottom + 0.3f, top - 0.3f), 0.016f, 0.6f,
                                             colour * 0.62f, 1f);
            }

            // The battens across them, standing proud, and a nail where each crosses a board.
            Color batten = Hex("6B5141");
            foreach (float v in new[] { 0.42f, 1.72f })
            {
                r.Course(v - 0.055f, v + 0.055f, batten, 0.015f, 0.03f, standOn: 0.012f);
                for (int k = 0; k < boards; k++)
                    r.Mound((k + 0.5f) * board, v, 0.009f, 0.009f, 0.004f, Hex("5D5852"), 0.04f);
            }

            r.ShadeBand(WallFootingTop, 0.7f, Hex("4C3A2D"), 0.3f, fadeUp: true);
        }

        /// <summary>Corrugated sheets, three to the tile, ribs running up them, riveted top and bottom, rust creeping up from the foot.</summary>
        static void PaintTin(Relief r, ref DeterministicRandom rng)
        {
            Color metal = Hex("8E989D"), rust = Hex("8E5A3A");
            const int sheets = 3;
            const float sheet = WallTileLength / sheets;
            const float rib = sheet / 10f;
            var rot = new Periodic(256, new[] { 4, 8, 16 }, new[] { 0.5f, 0.3f, 0.2f }, ref rng);
            float bottom = WallFootingTop, top = WallCapBottom;

            var shade = new float[sheets];
            for (int k = 0; k < sheets; k++) shade[k] = rng.Range(0.93f, 1.05f);

            for (int y = 0; y < r.H; y++)
            {
                float v = (y + 0.5f) / r.PpmV;
                if (v < bottom || v > top) continue;

                for (int x = 0; x < r.W; x++)
                {
                    float u = (x + 0.5f) / r.PpmU;
                    int k = Mathf.Min(sheets - 1, Mathf.FloorToInt(u / sheet));
                    float wave = 0.5f + 0.5f * Mathf.Cos((u - k * sheet) / rib * Mathf.PI * 2f);

                    // Rust: patches of it, thicker the lower down.
                    float low = 1f - Mathf.Clamp01((v - bottom) / 1.1f);
                    float rusty = Band(rot.Sample(x * 256 / r.W, y * 256 / r.H) + low * 0.35f, 0.72f, 0.08f);

                    Color c = Color.Lerp(metal * shade[k], rust, rusty * 0.85f);
                    int p = y * r.W + x;
                    r.Albedo[p] = new Color(c.r, c.g, c.b, 1f);
                    r.Height[p] = 0.02f + wave * 0.009f;
                }
            }

            // Where one sheet laps the next, a dark seam; rivets along the top and bottom.
            for (int k = 0; k < sheets; k++)
            {
                float u = k * sheet;
                r.Groove(new Vector2(u, bottom), new Vector2(u, top), 0.01f, 0.006f, Hex("4E5357"), 0.9f);

                for (int j = 0; j < 10; j += 2)
                    foreach (float v in new[] { bottom + 0.12f, top - 0.12f })
                        r.Mound(u + (j + 0.5f) * rib, v, 0.008f, 0.008f, 0.004f, Hex("6E767A"), 0.03f);
            }

            // Rust run down from a few rivets.
            for (int i = 0; i < 4; i++)
            {
                float length = rng.Range(0.2f, 0.5f);
                r.Stain(rng.Range(0f, WallTileLength), top - 0.12f - length * 0.5f, 0.02f, length * 0.5f, 0f, rust, 0.4f, 0.6f);
            }
        }

        /// <summary>
        /// The diner's own walls, in bands up V only - so the walls can be stretched along their
        /// length as the building grows without the texture stretching with them. The walls are unit
        /// cubes half in the ground: V 0.5 is ground level, 1 their top. From the ground: a teal
        /// skirting with a lit lip, the teal of the floor's checks, an off-white wall, a red rail, a pale cap.
        /// </summary>
        public static Texture2D EnsureDinerWall()
        {
            Texture2D existing = Current(DinerWallPath, SurfaceVersion);
            if (existing != null) return existing;

            ContentGenerator.EnsureFolder(Folder);

            var c = new Canvas(64, 256, 1f, 1f, wrapV: false);
            Color terracotta = Hex("3E6D69");
            Color cream = Hex("C9C8BF");
            Color rail = Hex("A6463C");
            Color cap = Hex("CFCEC7");

            c.Fill(terracotta * 0.8f);
            c.Rect(0f, 0.5f, 1f, 0.7f, terracotta, 1f);
            c.Rect(0f, 0.5f, 1f, 0.53f, terracotta * 0.85f, 1f);
            c.Rect(0f, 0.69f, 1f, 0.705f, Hex("5A8C87"), 1f);
            c.Rect(0f, 0.705f, 1f, 0.92f, cream, 1f);
            c.Rect(0f, 0.705f, 1f, 0.725f, cream * 0.88f, 1f);
            c.Rect(0f, 0.92f, 1f, 0.945f, rail, 1f);
            c.Rect(0f, 0.945f, 1f, 1.01f, cap, 1f);

            return Save(DinerWallPath, c.Pixels, 64, 256, TextureImporterType.Default, true, TextureWrapMode.Repeat,
                        TextureWrapMode.Clamp, SurfaceVersion);
        }

        // --- Foliage -------------------------------------------------------------------------------------

        /// <summary>
        /// A tileable bed of stylised leaves for the toon foliage shader, grey around 0.5 so it
        /// multiplies the tree's own colour lighter and darker: dark gaps between clumps, and on top
        /// hundreds of rounded leaf shapes, each lit on its upper half and shaded on its lower, the
        /// later ones overlapping the earlier - the painted-clump look of a toon canopy.
        /// </summary>
        public static Texture2D EnsureLeaves()
        {
            Texture2D existing = Current(LeavesPath, SurfaceVersion);
            if (existing != null) return existing;

            ContentGenerator.EnsureFolder(Folder);

            var rng = new DeterministicRandom(8909);
            var c = new Canvas(512, 512, 1f, 1f, wrapV: true);
            c.Fill(new Color(0.32f, 0.32f, 0.32f));

            for (int i = 0; i < 520; i++)
            {
                float u = rng.Range(0f, 1f), v = rng.Range(0f, 1f);
                float length = rng.Range(0.05f, 0.085f);
                float width = length * rng.Range(0.55f, 0.75f);
                float angle = rng.Range(-0.9f, 0.9f) + Mathf.PI * 0.5f;
                float tone = rng.Range(0.46f, 0.56f);

                c.Leaf(u, v, length, width, angle, new Color(tone, tone, tone), new Color(tone * 1.24f, tone * 1.24f, tone * 1.24f));
            }

            return Save(LeavesPath, c.Pixels, 512, TextureImporterType.Default, true, TextureWrapMode.Repeat, SurfaceVersion);
        }

        // --- Shared painting -------------------------------------------------------------------------------

        /// <summary>
        /// Asphalt: two close tones shading softly into each other in broad patches, and rounded
        /// stones of aggregate set into it, low and only a little lighter or darker than the
        /// surface round them, so they read as texture rather than as noise.
        /// </summary>
        static Relief PaintAsphalt(int w, int h, float metresU, float metresV, bool wrapV, Color baseColour,
                                   ref DeterministicRandom rng)
        {
            var r = new Relief(w, h, metresU, metresV, wrapV);
            var patches = new Periodic(w, new[] { 3, 6, 12 }, new[] { 0.55f, 0.3f, 0.15f }, ref rng);
            Color light = baseColour * 1.05f;
            Color dark = baseColour * 0.95f;

            for (int y = 0; y < h; y++)
            {
                int sy = y * w / h;
                for (int x = 0; x < w; x++)
                {
                    Color col = Color.Lerp(dark, light, Band(patches.Sample(x, sy), 0.5f, 0.14f));
                    r.Albedo[y * w + x] = new Color(col.r, col.g, col.b, 1f);
                }
            }

            int stones = Mathf.RoundToInt(metresU * metresV * 6f);
            for (int i = 0; i < stones; i++)
            {
                float radius = rng.Range(0.025f, 0.05f);
                Color stone = rng.Chance(0.6f) ? baseColour * rng.Range(1.05f, 1.1f) : baseColour * rng.Range(0.92f, 0.95f);
                r.Mound(rng.Range(0f, metresU), rng.Range(0f, metresV), radius, radius * rng.Range(0.65f, 1f), radius * 0.2f, stone,
                        angle: rng.Range(0f, Mathf.PI), soft: radius * 0.05f);
            }

            r.Occlude(0.03f, 0.004f, baseColour * 0.8f, 0.35f, baseColour * 1.15f, 0.15f);
            return r;
        }

        /// <summary>Little rounded stones on a surface, a shade lighter or darker than it.</summary>
        static void PaintSpecks(Relief r, int count, float minRadius, float maxRadius, Color baseColour, ref DeterministicRandom rng)
        {
            for (int i = 0; i < count; i++)
            {
                float u = rng.Range(0f, r.MetresU), v = rng.Range(0f, r.MetresV);
                float radius = rng.Range(minRadius, maxRadius);
                Color speck = rng.Chance(0.55f) ? baseColour * 1.12f : baseColour * 0.88f;
                r.Mound(u, v, radius, radius, radius * 0.2f, speck, r.HeightAt(u, v));
            }
        }

        /// <summary>Cracks: short jagged runs pressed into the surface, each forking once.</summary>
        static void PaintCracks(Relief r, int count, Color colour, ref DeterministicRandom rng)
        {
            const float width = 0.024f;
            for (int i = 0; i < count; i++)
            {
                var p = new Vector2(rng.Range(0f, r.MetresU), rng.Range(0.3f, r.MetresV - 0.3f));
                float angle = rng.Range(0f, Mathf.PI * 2f);
                int steps = rng.Range(5, 9);

                for (int s = 0; s < steps; s++)
                {
                    angle += rng.Range(-0.7f, 0.7f);
                    Vector2 next = p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rng.Range(0.08f, 0.18f);
                    r.Groove(p, next, width, 0.003f, colour, 0.5f);

                    if (s == steps / 2)
                    {
                        float fork = angle + rng.Range(0.6f, 1.1f) * (rng.Chance(0.5f) ? 1f : -1f);
                        r.Groove(p, p + new Vector2(Mathf.Cos(fork), Mathf.Sin(fork)) * 0.14f, width * 0.8f, 0.002f, colour, 0.5f);
                    }

                    p = next;
                }
            }
        }

        // --- The canvas --------------------------------------------------------------------------------------

        /// <summary>A texture being painted, addressed in metres: U always wraps, V wraps when asked.</summary>
        sealed class Canvas
        {
            public readonly int W, H;
            public readonly float MetresU, MetresV;
            public readonly Color[] Pixels;
            readonly bool wrapV;

            float PpmU => W / MetresU;
            float PpmV => H / MetresV;

            public Canvas(int w, int h, float metresU, float metresV, bool wrapV)
                : this(new Color[w * h], w, h, metresU, metresV, wrapV)
            {
            }

            Canvas(Color[] pixels, int w, int h, float metresU, float metresV, bool wrapV)
            {
                W = w;
                H = h;
                MetresU = metresU;
                MetresV = metresV;
                Pixels = pixels;
                this.wrapV = wrapV;
            }

            /// <summary>Paints flat markings in metres onto a relief's colours.</summary>
            public static Canvas Over(Relief relief, bool wrapV) =>
                new Canvas(relief.Albedo, relief.W, relief.H, relief.MetresU, relief.MetresV, wrapV);

            public void Fill(Color c)
            {
                for (int i = 0; i < Pixels.Length; i++) Pixels[i] = new Color(c.r, c.g, c.b, 1f);
            }

            void Blend(int x, int y, Color c, float a)
            {
                if (a <= 0f) return;
                if (!wrapV && (y < 0 || y >= H)) return;

                int i = (wrapV ? ((y % H) + H) % H : y) * W + ((x % W) + W) % W;
                Color o = Pixels[i];
                Pixels[i] = new Color(o.r + (c.r - o.r) * a, o.g + (c.g - o.g) * a, o.b + (c.b - o.b) * a, 1f);
            }

            /// <summary>A rectangle in metres with anti-aliased edges.</summary>
            public void Rect(float u0, float v0, float u1, float v1, Color c, float a)
            {
                float x0 = u0 * PpmU, x1 = u1 * PpmU, y0 = v0 * PpmV, y1 = v1 * PpmV;

                for (int y = Mathf.FloorToInt(y0); y <= Mathf.CeilToInt(y1); y++)
                {
                    float cy = Mathf.Clamp01(Mathf.Min(y + 1f, y1) - Mathf.Max(y, y0));
                    if (cy <= 0f) continue;

                    for (int x = Mathf.FloorToInt(x0); x <= Mathf.CeilToInt(x1); x++)
                    {
                        float cx = Mathf.Clamp01(Mathf.Min(x + 1f, x1) - Mathf.Max(x, x0));
                        Blend(x, y, c, cx * cy * a);
                    }
                }
            }

            public void RectOutline(float u0, float v0, float u1, float v1, float width, Color c, float a)
            {
                Rect(u0, v0, u1, v0 + width, c, a);
                Rect(u0, v1 - width, u1, v1, c, a);
                Rect(u0, v0 + width, u0 + width, v1 - width, c, a);
                Rect(u1 - width, v0 + width, u1, v1 - width, c, a);
            }

            /// <summary>Paint worn thin in places: a rectangle whose opacity drops where the noise is low.</summary>
            public void WornStrip(float u0, float u1, float v0, float v1, Color c, float a, Periodic wear, float keep)
            {
                float x0 = u0 * PpmU, x1 = u1 * PpmU, y0 = v0 * PpmV, y1 = v1 * PpmV;

                for (int y = Mathf.FloorToInt(y0); y <= Mathf.CeilToInt(y1); y++)
                {
                    float cy = Mathf.Clamp01(Mathf.Min(y + 1f, y1) - Mathf.Max(y, y0));
                    if (cy <= 0f) continue;

                    for (int x = Mathf.FloorToInt(x0); x <= Mathf.CeilToInt(x1); x++)
                    {
                        float cx = Mathf.Clamp01(Mathf.Min(x + 1f, x1) - Mathf.Max(x, x0));
                        float n = wear.Sample(x * 256 / W, y * 256 / H);
                        float worn = Band(n, 1f - keep, 0.03f);
                        Blend(x, y, c, cx * cy * a * Mathf.Lerp(0.35f, 1f, worn));
                    }
                }
            }

            /// <summary>A band across the whole width at height <paramref name="v"/>: solid for
            /// <paramref name="half"/> either side, easing out over <paramref name="soft"/>.</summary>
            public void SoftBand(float v, float half, float soft, Color c, float a)
            {
                int y0 = Mathf.FloorToInt((v - half - soft) * PpmV), y1 = Mathf.CeilToInt((v + half + soft) * PpmV);
                for (int y = y0; y <= y1; y++)
                {
                    float d = Mathf.Abs((y + 0.5f) / PpmV - v);
                    float w = 1f - Band(d, half + soft * 0.5f, soft * 0.5f);
                    if (w <= 0f) continue;
                    for (int x = 0; x < W; x++) Blend(x, y, c, w * a);
                }
            }

            public void Disc(float u, float v, float radius, Color c, float a)
            {
                float cx = u * PpmU, cy = v * PpmV, rx = radius * PpmU, ry = radius * PpmV;
                for (int y = Mathf.FloorToInt(cy - ry - 1f); y <= Mathf.CeilToInt(cy + ry + 1f); y++)
                {
                    for (int x = Mathf.FloorToInt(cx - rx - 1f); x <= Mathf.CeilToInt(cx + rx + 1f); x++)
                    {
                        float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry;
                        float d = Mathf.Sqrt(nx * nx + ny * ny);
                        Blend(x, y, c, Mathf.Clamp01((1f - d) * Mathf.Min(rx, ry) + 0.5f) * a);
                    }
                }
            }

            public void Line(Vector2 a, Vector2 b, float width, Color c, float opacity)
            {
                Vector2 pa = new Vector2(a.x * PpmU, a.y * PpmV), pb = new Vector2(b.x * PpmU, b.y * PpmV);
                float w = width * 0.5f * (PpmU + PpmV) * 0.5f;
                Vector2 ab = pb - pa;
                float len2 = Mathf.Max(1e-4f, ab.sqrMagnitude);

                for (int y = Mathf.FloorToInt(Mathf.Min(pa.y, pb.y) - w - 1f); y <= Mathf.CeilToInt(Mathf.Max(pa.y, pb.y) + w + 1f); y++)
                {
                    for (int x = Mathf.FloorToInt(Mathf.Min(pa.x, pb.x) - w - 1f); x <= Mathf.CeilToInt(Mathf.Max(pa.x, pb.x) + w + 1f); x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float t = Mathf.Clamp01(Vector2.Dot(p - pa, ab) / len2);
                        float d = Vector2.Distance(p, pa + ab * t);
                        Blend(x, y, c, Mathf.Clamp01(w + 0.5f - d) * opacity);
                    }
                }
            }

            /// <summary>A rounded leaf: a pointed oval along <paramref name="angle"/>, its upper side
            /// lit, a darker rim round it so overlapping leaves stay separate.</summary>
            public void Leaf(float u, float v, float length, float width, float angle, Color shade, Color lit)
            {
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var side = new Vector2(-dir.y, dir.x);
                float reach = length * PpmU + 2f;
                float cx = u * PpmU, cy = v * PpmV;

                for (int y = Mathf.FloorToInt(cy - reach); y <= Mathf.CeilToInt(cy + reach); y++)
                {
                    for (int x = Mathf.FloorToInt(cx - reach); x <= Mathf.CeilToInt(cx + reach); x++)
                    {
                        var p = new Vector2((x + 0.5f) / PpmU - u, (y + 0.5f) / PpmV - v);
                        float along = Vector2.Dot(p, dir) / length;
                        float across = Vector2.Dot(p, side) / width;

                        // Pointed at the tip: the half-width narrows toward along = 1.
                        float half = Mathf.Sqrt(Mathf.Max(0f, 1f - along * along)) * (along > 0f ? 1f - along * 0.35f : 1f);
                        float d = Mathf.Abs(across) - half;
                        float px = width * PpmU;
                        float coverage = Mathf.Clamp01(-d * px + 0.5f);
                        if (coverage <= 0f) continue;

                        float rim = Mathf.Clamp01(-d * px / 2.2f);
                        Color col = across > 0.05f ? lit : shade;
                        col = Color.Lerp(shade * 0.72f, col, rim);
                        Blend(x, y, col, coverage);
                    }
                }
            }
        }
    }
}
