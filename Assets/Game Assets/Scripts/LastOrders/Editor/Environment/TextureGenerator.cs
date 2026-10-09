using System;
using System.IO;
using LastOrders.Core;
using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The level's generated textures, painted in code so they tile perfectly and can be changed
    /// by changing a number: grass, roof shingles, clapboard siding, corrugated metal, and the
    /// restaurant's floor tiles.
    ///
    /// Every one wraps at its edges - noise on a repeating lattice, strokes and shapes drawn modulo
    /// the size - so a tile never shows a seam. Each is stamped with a version in its importer, and
    /// is painted again only when that version changes.
    ///
    /// The game is smooth low-poly and stylised, so these are painted the way that style paints a
    /// surface: big readable shapes - clumps, tiles, stones - with soft shading across them, darker
    /// in the gaps and lighter on the crowns, and never photographic grain. Grain that fine turns to
    /// noise at the camera's distance, and noise next to smooth models reads as a photo pasted under
    /// them. The ground and the floors are painted with height too (see the relief), and carry a
    /// normal map, so the light rounds them as it rounds the models.
    ///
    /// The palette is the game's clean low-poly look: fresh green grass, deeper in the shade,
    /// plain brown earth, light grey concrete, weathered timber, slate tarmac, a diner in off-white
    /// and teal. Neutral colours under plain daylight - nothing tinted warm, so each thing keeps its
    /// own colour (see PaintedLookSetup).
    /// </summary>
    public static partial class TextureGenerator
    {
        /// <summary>The current style's folder: textures painted in an earlier style stay where they were.</summary>
        const string Folder = EnvKit.TextureFolder;

        public const string GrassPath = Folder + "/Grass_Albedo.png";
        public const string GrassMacroPath = Folder + "/Grass_Macro.png";
        public const string ShinglesPath = Folder + "/Roof_Shingles.png";
        public const string SidingPath = Folder + "/Wall_Siding.png";
        public const string MetalPath = Folder + "/Metal_Corrugated.png";
        /// <summary>The restaurant's tiles. Named afresh for each repaint of the floor's look, so the
        /// look before stays on disk as it was (Floor_DinerChecker / Floor_KitchenTile).</summary>
        public const string DinerFloorPath = Folder + "/Floor_DinerTiles.png";
        public const string KitchenFloorPath = Folder + "/Floor_KitchenTiles.png";

        const string Version = "tex-2";

        /// <summary>The grass and the floors have their own version, so repainting them leaves the
        /// houses' textures alone.</summary>
        const string GrassVersion = "grass-lowpoly-2";

        const string FloorVersion = "floor-lowpoly-2";

        // --- Grass ----------------------------------------------------------------------------------

        /// <summary>
        /// Up the screen, in the ground plane's UVs - which run west along U and south along V, the
        /// plane being turned half round. The tufts' blades point this way, away from the camera,
        /// so they read as standing up rather than lying flat.
        /// </summary>
        static readonly Vector2 GrassAwayFromCamera = new Vector2(0.6f, -0.8f);

        /// <summary>
        /// Turf seen from above, as the smooth low-poly style models it: a lush lawn in rich greens
        /// shading softly into one another, low swells of thicker grass - some catching the sun,
        /// lime-gold - and tufts of broad blades each folded down the middle into two flat faces,
        /// one turned to the light, one away, deep green at the foot and lime at the tip. Little
        /// clusters of flowers - buttercup yellow, marigold orange, daisy white - and the odd pebble.
        /// Its normal map lets the level's light catch the tufts' faces.
        ///
        /// The broad lighter and darker patches across the lawn are the second texture, tiled eight
        /// times more coarsely: painted into this one they repeated every tile and read as a grid.
        /// They shade softly into one another, as light over a field does, not in bands.
        /// </summary>
        public static void EnsureGrass(out Texture2D albedo, out Texture2D macro)
        {
            albedo = CurrentRelief(GrassPath, GrassVersion);
            macro = Current(GrassMacroPath, GrassVersion);
            if (albedo != null && macro != null) return;

            ContentGenerator.EnsureFolder(Folder);

            var rng = new DeterministicRandom(20261002);
            albedo = SaveRelief(GrassPath, PaintGrass(ref rng), 1f, TextureWrapMode.Repeat, TextureWrapMode.Repeat, GrassVersion);
            macro = Save(GrassMacroPath, PaintGrassMacro(1024, ref rng), 1024, TextureImporterType.Default, true, TextureWrapMode.Repeat,
                         GrassVersion);
        }

        static Relief PaintGrass(ref DeterministicRandom rng)
        {
            const int size = 1024;
            const float metres = 8f;
            var r = new Relief(size, size, metres, metres);

            // A natural leaf green, neither lime nor emerald: under plain daylight these come out the
            // green of a lawn. Yellower read as dry grass, and more saturated as neon.
            Color[] greens = { Hex("5D7A3E"), Hex("66843F"), Hex("58743A"), Hex("6C8C47"), Hex("516C37") };
            Color low = Hex("2C4424");
            Color tip = Hex("92AE5C");
            Color sunny = Hex("7B9A4C");

            // The lawn itself: three greens shading softly into one another over a metre or so,
            // rising and falling a couple of centimetres with them.
            var patches = new Periodic(size, new[] { 4, 8, 16, 32 }, new[] { 0.45f, 0.3f, 0.15f, 0.1f }, ref rng);
            var hue = new Periodic(size, new[] { 6, 12 }, new[] { 0.6f, 0.4f }, ref rng);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = patches.Sample(x, y);
                    Color c = Color.Lerp(greens[2], greens[3], Band(n, 0.5f, 0.18f));
                    c = Color.Lerp(c, greens[1], Band(hue.Sample(x, y), 0.55f, 0.12f) * 0.6f);
                    r.Albedo[y * size + x] = new Color(c.r, c.g, c.b, 1f);
                    r.Height[y * size + x] = n * 0.015f;
                }
            }

            // Low swells of thicker grass, a shade lighter, rising out of the lawn; one in four
            // high enough to catch the sun and go lime-gold.
            for (int i = 0; i < 200; i++)
            {
                float radius = rng.Range(0.25f, 0.5f);
                Color c = rng.Chance(0.25f) ? sunny * rng.Range(0.97f, 1.04f) : greens[rng.Range(0, greens.Length)] * rng.Range(1f, 1.06f);
                r.Swell(rng.Range(0f, metres), rng.Range(0f, metres), radius, radius * rng.Range(0.65f, 0.9f), 0.012f, c, 0.55f,
                        rng.Range(0f, Mathf.PI));
            }

            // The turf: tufts of broad blades folded down the middle, pointing away from the camera,
            // each taking the colour of the patch it grows in - darker at the foot, lighter at the
            // tip. Thick in some places and thin in others, as a lawn left to itself grows.
            var thickness = new Periodic(size, new[] { 4, 8, 16 }, new[] { 0.5f, 0.3f, 0.2f }, ref rng);
            for (int i = 0; i < 2600; i++)
            {
                var foot = new Vector2(rng.Range(0f, metres), rng.Range(0f, metres));
                float lush = thickness.Sample(Mathf.FloorToInt(foot.x / metres * size), Mathf.FloorToInt(foot.y / metres * size));
                if (rng.NextFloat() > Band(lush, 0.5f, 0.15f) * 0.85f + 0.1f) continue;

                float ground = r.HeightAt(foot.x, foot.y);
                float scale = rng.Range(0.7f, 1.3f) * Mathf.Lerp(0.85f, 1.15f, lush);
                Color colour = r.ColourAt(foot.x, foot.y) * rng.Range(0.97f, 1.06f);
                Color top = Color.Lerp(colour, tip, rng.Range(0.35f, 0.65f));
                int blades = rng.Range(5, 9);

                for (int k = 0; k < blades; k++)
                {
                    float spread = k / (float)(blades - 1) * 2f - 1f;
                    float lean = spread * rng.Range(0.6f, 0.95f) + rng.Range(-0.1f, 0.1f);
                    Vector2 dir = Rotate(GrassAwayFromCamera, lean);
                    float length = rng.Range(0.12f, 0.22f) * scale * (1f - Mathf.Abs(spread) * 0.35f);

                    r.Blade(foot + new Vector2(dir.y, -dir.x) * spread * 0.03f * scale, dir, length, rng.Range(0.04f, 0.06f) * scale,
                            0.025f, Color.Lerp(colour, low, 0.25f), top, ground);
                }
            }

            // Flowers: five round petals and a centre, in little clusters of one colour - buttercup
            // yellow, daisy white, a lilac - the way wild flowers come up together.
            Color[] petals = { Hex("E6C93C"), Hex("ECEEE4"), Hex("B9A3E0"), Hex("E6C93C"), Hex("ECEEE4") };
            Color[] hearts = { Hex("C99A2A"), Hex("D9B12E"), Hex("8E7BC4"), Hex("C99A2A"), Hex("D9B12E") };
            for (int i = 0; i < 22; i++)
            {
                float u = rng.Range(0f, metres), v = rng.Range(0f, metres);
                int kind = rng.Range(0, petals.Length);

                for (int k = rng.Range(2, 6); k > 0; k--)
                {
                    float fu = u + rng.Range(-0.18f, 0.18f), fv = v + rng.Range(-0.18f, 0.18f);
                    float ground = r.HeightAt(fu, fv) + 0.01f;
                    float bloom = rng.Range(0.85f, 1.2f);

                    for (int f = 0; f < 5; f++)
                    {
                        float a = f / 5f * Mathf.PI * 2f;
                        r.Mound(fu + Mathf.Cos(a) * 0.021f * bloom, fv + Mathf.Sin(a) * 0.021f * bloom, 0.018f * bloom, 0.018f * bloom,
                                0.006f, petals[kind], ground);
                    }

                    r.Mound(fu, fv, 0.011f * bloom, 0.011f * bloom, 0.009f, hearts[kind], ground);
                }
            }

            // Pebbles, half sunk.
            for (int i = 0; i < 16; i++)
            {
                float u = rng.Range(0f, metres), v = rng.Range(0f, metres);
                float radius = rng.Range(0.025f, 0.045f);
                Color stone = Color.Lerp(Hex("8A8780"), Hex("A09D96"), rng.NextFloat());
                r.Mound(u, v, radius, radius * rng.Range(0.65f, 0.9f), radius * 0.5f, stone, r.HeightAt(u, v), rng.Range(0f, Mathf.PI));
            }

            // A little cooler and darker down between the tufts, a little warmer on top of them.
            r.Occlude(0.04f, 0.015f, low, 0.35f, tip, 0.15f);
            r.Occlude(0.25f, 0.03f, low, 0.2f, tip, 0.1f);
            return r;
        }

        /// <summary>
        /// The lawn's broad patches, for the grass material's detail map, where grey 0.5 changes
        /// nothing: cool and deep in some, warm and golden in others, shading softly into each other
        /// over metres, with brighter sunlit clearings here and there - light falling unevenly over a
        /// field, as a painter would lay it in.
        /// </summary>
        static Color[] PaintGrassMacro(int size, ref DeterministicRandom rng)
        {
            var light = new Periodic(size, new[] { 2, 4, 8, 16 }, new[] { 0.5f, 0.28f, 0.14f, 0.08f }, ref rng);
            var sun = new Periodic(size, new[] { 3, 6, 12 }, new[] { 0.55f, 0.3f, 0.15f }, ref rng);
            var pixels = new Color[size * size];

            // Close to grey 0.5 throughout, so the patches change how light the lawn is far more than
            // its hue: a gold here turned the whole field yellow, a blue turned it cyan.
            var shade = new Color(0.4f, 0.46f, 0.42f);
            var lit = new Color(0.56f, 0.58f, 0.53f);
            var golden = new Color(0.6f, 0.64f, 0.54f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Band(light.Sample(x, y), 0.5f, 0.2f);
                    Color c = Color.Lerp(shade, lit, n);
                    c = Color.Lerp(c, golden, Band(sun.Sample(x, y), 0.64f, 0.07f) * 0.7f);
                    pixels[y * size + x] = new Color(c.r, c.g, c.b, 1f);
                }
            }

            return pixels;
        }

        static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // --- The restaurant's floor --------------------------------------------------------------------

        /// <summary>
        /// The dining room: a diner's checkerboard in off-white and teal, the teal of the walls'
        /// skirting. Painted the way the low-poly style paints a floor: clean squares
        /// each catching the light a little differently, brighter toward one corner as if a brush
        /// had laid the light across it, meeting at a hairline seam rather than dark grout, with a
        /// soft sheen of polish here and there and only the faintest scuffs. Four squares by four;
        /// one tile covers 2.4 m, squares of 0.6 m.
        /// </summary>
        public static Texture2D EnsureDinerFloor()
        {
            Texture2D existing = CurrentRelief(DinerFloorPath, FloorVersion);
            if (existing != null) return existing;

            ContentGenerator.EnsureFolder(Folder);

            var rng = new DeterministicRandom(5151);
            return SaveRelief(DinerFloorPath, PaintDinerFloor(ref rng), 1f, TextureWrapMode.Repeat, TextureWrapMode.Repeat, FloorVersion);
        }

        static Relief PaintDinerFloor(ref DeterministicRandom rng)
        {
            const float metres = 2.4f;
            const int count = 4;

            // An off-white painted grey: under the sky's light a floor facing straight up shows as white.
            Color cream = Hex("ABAAA1"), sage = Hex("3B6966");
            // Between the two in value, so it reads as a seam against either rather than a dark grid.
            Color seam = Hex("7E8079");
            Relief r = TileFloor(1024, metres, count, (i, j) => (i + j) % 2 == 0 ? cream : sage, seam, joint: 0.005f, bevel: 0.018f,
                                 height: 0.004f, variation: 0.025f, tilt: 0.008f, ref rng);

            TileLight(r, metres / count, count, 0.07f, ref rng);
            Polish(r, 9, Color.Lerp(cream, Color.white, 0.45f), ref rng);
            Scuffs(r, 10, Hex("6E6F69"), ref rng);
            r.Occlude(0.02f, 0.004f, seam * 0.85f, 0.25f, Color.Lerp(cream, Color.white, 0.3f), 0.08f);
            return r;
        }

        /// <summary>
        /// The kitchen strip along the back wall: square tiles in a cool blue-grey, each a shade
        /// apart and lit across like the dining room's, a light seam between - a working floor that
        /// sets the stoves and counters off without taking the eye. Three by three; one tile covers
        /// 1.2 m, squares of 0.4 m.
        /// </summary>
        public static Texture2D EnsureKitchenFloor()
        {
            Texture2D existing = CurrentRelief(KitchenFloorPath, FloorVersion);
            if (existing != null) return existing;

            ContentGenerator.EnsureFolder(Folder);

            var rng = new DeterministicRandom(6263);
            return SaveRelief(KitchenFloorPath, PaintKitchenFloor(ref rng), 1f, TextureWrapMode.Repeat, TextureWrapMode.Repeat,
                              FloorVersion);
        }

        static Relief PaintKitchenFloor(ref DeterministicRandom rng)
        {
            const float metres = 1.2f;
            const int count = 3;

            Color[] fired = { Hex("858C90"), Hex("80878B"), Hex("8A9195"), Hex("7C8387"), Hex("8E9599") };
            var pick = new int[count * count];
            for (int k = 0; k < pick.Length; k++) pick[k] = (k * 2 + rng.Range(0, 2)) % fired.Length;

            Color seam = Hex("666C70");
            Relief r = TileFloor(512, metres, count, (i, j) => fired[pick[j * count + i]], seam, joint: 0.006f, bevel: 0.016f,
                                 height: 0.004f, variation: 0.03f, tilt: 0.01f, ref rng);

            TileLight(r, metres / count, count, 0.08f, ref rng);
            Polish(r, 4, Hex("A4AAAE"), ref rng);
            Scuffs(r, 6, Hex("5C6266"), ref rng);
            r.Occlude(0.015f, 0.004f, seam * 0.8f, 0.3f, Hex("9CA2A6"), 0.06f);
            return r;
        }

        /// <summary>
        /// The light a painter lays across each tile: brighter toward one corner and a shade darker
        /// toward the other, by up to <paramref name="amount"/>, each tile turned its own way.
        /// </summary>
        static void TileLight(Relief r, float cell, int count, float amount, ref DeterministicRandom rng)
        {
            var toward = new Vector2[count * count];
            var strength = new float[count * count];
            for (int k = 0; k < toward.Length; k++)
            {
                toward[k] = Rotate(Vector2.right, rng.Range(0f, Mathf.PI * 2f));
                strength[k] = rng.Range(0.5f, 1f) * amount;
            }

            float half = cell * 0.5f;
            for (int y = 0; y < r.H; y++)
            {
                float v = (y + 0.5f) / r.PpmV;
                int j = Mathf.Min(count - 1, Mathf.FloorToInt(v / cell));

                for (int x = 0; x < r.W; x++)
                {
                    float u = (x + 0.5f) / r.PpmU;
                    int i = Mathf.Min(count - 1, Mathf.FloorToInt(u / cell));
                    int k = j * count + i;

                    var local = new Vector2(u - (i + 0.5f) * cell, v - (j + 0.5f) * cell) / half;
                    float light = 1f + strength[k] * 0.5f * Vector2.Dot(local, toward[k]);

                    int p = y * r.W + x;
                    Color c = r.Albedo[p] * light;
                    r.Albedo[p] = new Color(c.r, c.g, c.b, 1f);
                }
            }
        }

        /// <summary>Long soft streaks of sheen, where a mop has left the floor brighter.</summary>
        static void Polish(Relief r, int count, Color sheen, ref DeterministicRandom rng)
        {
            for (int s = 0; s < count; s++)
            {
                float length = rng.Range(0.18f, 0.4f);
                r.Stain(rng.Range(0f, r.MetresU), rng.Range(0f, r.MetresV), length, length * rng.Range(0.2f, 0.35f),
                        rng.Range(0f, Mathf.PI), sheen, rng.Range(0.05f, 0.1f), 1f);
            }
        }

        /// <summary>
        /// A square grid of tiles, <paramref name="count"/> by <paramref name="count"/> over
        /// <paramref name="metres"/>: each its own shade of <paramref name="colourOf"/>, rising
        /// <paramref name="height"/> above the grout over a rounded edge <paramref name="bevel"/>
        /// wide, its top tipped by up to <paramref name="tilt"/> (a slope) so no two catch the light
        /// alike. The grout straddles the tile's edges, so it wraps.
        /// </summary>
        static Relief TileFloor(int size, float metres, int count, Func<int, int, Color> colourOf, Color grout, float joint, float bevel,
                                float height, float variation, float tilt, ref DeterministicRandom rng)
        {
            var r = new Relief(size, size, metres, metres);
            float cell = metres / count;
            float half = cell * 0.5f - joint * 0.5f;
            // Corners only just rounded: rounder, and the grout pools into a dot where four tiles meet.
            float corner = bevel * 0.35f;
            float ppm = r.PpmU;

            var shade = new float[count * count];
            var lean = new Vector2[count * count];
            for (int k = 0; k < shade.Length; k++)
            {
                shade[k] = 1f + rng.Range(-variation, variation);
                lean[k] = Rotate(Vector2.right, rng.Range(0f, Mathf.PI * 2f)) * rng.Range(0.3f, 1f) * tilt;
            }

            // Lifts every tile so its lowest corner still stands clear of the grout.
            float lift = tilt * half * 1.42f;

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / ppm;
                int j = Mathf.Min(count - 1, Mathf.FloorToInt(v / cell));

                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / ppm;
                    int i = Mathf.Min(count - 1, Mathf.FloorToInt(u / cell));
                    int k = j * count + i;

                    // Distance in from the tile's rounded-square edge.
                    var local = new Vector2(u - (i + 0.5f) * cell, v - (j + 0.5f) * cell);
                    var q = new Vector2(Mathf.Abs(local.x) - (half - corner), Mathf.Abs(local.y) - (half - corner));
                    float outside = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - corner;
                    float inside = -outside;

                    float coverage = Mathf.Clamp01(inside * ppm + 0.5f);
                    float t = Mathf.Clamp01(inside / bevel);
                    float profile = Mathf.Sqrt(1f - (1f - t) * (1f - t));
                    float top = height + lift + Vector2.Dot(local, lean[k]);

                    Color c = colourOf(i, j) * shade[k];
                    c = Color.Lerp(grout, c, coverage);

                    int p = y * size + x;
                    r.Albedo[p] = new Color(c.r, c.g, c.b, 1f);
                    r.Height[p] = top * profile * coverage;
                }
            }

            return r;
        }

        /// <summary>Knocks a piece off one corner of <paramref name="chips"/> tiles, down to the bed beneath.</summary>
        static void Chips(Relief r, float cell, int count, int chips, Color bed, ref DeterministicRandom rng)
        {
            for (int c = 0; c < chips; c++)
            {
                int i = rng.Range(0, count), j = rng.Range(0, count);
                float cu = rng.Chance(0.5f) ? 0f : 1f, cv = rng.Chance(0.5f) ? 0f : 1f;
                float radius = cell * rng.Range(0.05f, 0.08f);

                // Centred just inside the tile's own corner, so the break is its corner and not the
                // grout's crossing.
                float inset = radius * 0.75f;
                float u = (i + cu) * cell + (cu < 0.5f ? inset : -inset);
                float v = (j + cv) * cell + (cv < 0.5f ? inset : -inset);
                r.Dent(u, v, radius, 0.3f, bed, 1f);
            }
        }

        /// <summary>A jagged crack of <paramref name="steps"/> runs from <paramref name="start"/>.</summary>
        static void Crack(Relief r, Vector2 start, int steps, float step, float width, Color colour, ref DeterministicRandom rng)
        {
            Vector2 p = start;
            float angle = rng.Range(0f, Mathf.PI * 2f);
            for (int s = 0; s < steps; s++)
            {
                angle += rng.Range(-0.8f, 0.8f);
                Vector2 next = p + Rotate(Vector2.right, angle) * step * rng.Range(0.6f, 1.3f);
                r.Groove(p, next, width, 0.002f, colour, 0.7f);
                p = next;
            }
        }

        /// <summary>Soft streaks of grime, the marks of chair legs and shoes.</summary>
        static void Scuffs(Relief r, int count, Color colour, ref DeterministicRandom rng)
        {
            for (int s = 0; s < count; s++)
            {
                float length = rng.Range(0.03f, 0.1f);
                r.Stain(rng.Range(0f, r.MetresU), rng.Range(0f, r.MetresV), length, length * rng.Range(0.12f, 0.3f),
                        rng.Range(0f, Mathf.PI), colour, rng.Range(0.06f, 0.14f), 0.8f);
            }
        }

        // --- Roofs, walls, metal ----------------------------------------------------------------------

        /// <summary>
        /// Rows of staggered shingles, light grey so a material's colour tints them: a shadow line
        /// under each row, a hairline between shingles, every shingle a slightly different shade.
        /// V runs up the slope. One tile is meant to cover 1.8 m.
        /// </summary>
        public static Texture2D EnsureShingles()
        {
            Texture2D existing = Current(ShinglesPath);
            if (existing != null) return existing;

            ContentGenerator.EnsureFolder(Folder);

            const int size = 512;
            const int rows = 10;
            int rowHeight = size / rows;

            var rng = new DeterministicRandom(7771);
            var grain = new Periodic(size, new[] { 16, 64 }, new[] { 0.6f, 0.4f }, ref rng);
            var pixels = new Color[size * size];

            for (int row = 0; row < rows; row++)
            {
                // Shingle edges for this row, wrapping round the tile.
                // The last shingle runs off the right edge and finishes before the first one on the left.
                var edges = new System.Collections.Generic.List<int>();
                int x = rng.Range(0, 40);
                while (x < size)
                {
                    edges.Add(x);
                    x += rng.Range(34, 72);
                }

                var shades = new float[edges.Count];
                for (int i = 0; i < shades.Length; i++) shades[i] = rng.Range(0.72f, 0.96f);

                for (int y = row * rowHeight; y < (row + 1) * rowHeight; y++)
                {
                    float inRow = (y - row * rowHeight) / (float)rowHeight;

                    for (int px = 0; px < size; px++)
                    {
                        int shingle = edges.Count - 1;
                        for (int i = 0; i < edges.Count; i++)
                            if (px >= edges[i]) shingle = i;

                        float shade = shades[shingle];

                        // Lighter at the exposed lower edge, darker up under the row above; a dark
                        // line along the bottom where the row overhangs the one below.
                        shade *= Mathf.Lerp(1.06f, 0.86f, inRow);
                        if (inRow < 0.07f) shade *= 0.55f;

                        bool gap = false;
                        foreach (int e in edges)
                            if (Mathf.Abs(px - e) <= 1 || Mathf.Abs(px - e - size) <= 1) gap = true;
                        if (gap) shade *= 0.5f;

                        shade *= 0.92f + grain.Sample(px, y) * 0.14f;
                        pixels[y * size + px] = new Color(shade, shade, shade, 1f);
                    }
                }
            }

            return Save(ShinglesPath, pixels, size, TextureImporterType.Default, true, TextureWrapMode.Repeat);
        }

        /// <summary>
        /// Clapboard: level boards, each shading from light at its top to dark at its lower lip, a
        /// shadow line beneath, faint grain, and the odd streak of dirt run down from the eaves. One
        /// tile is meant to cover 1.2 m, eight boards.
        /// </summary>
        public static Texture2D EnsureSiding()
        {
            Texture2D existing = Current(SidingPath);
            if (existing != null) return existing;

            ContentGenerator.EnsureFolder(Folder);

            const int size = 512;
            const int boards = 8;
            int boardHeight = size / boards;

            var rng = new DeterministicRandom(4413);
            var grain = new Periodic(size, new[] { 64, 8 }, new[] { 0.7f, 0.3f }, ref rng);
            var streaks = new Periodic(size, new[] { 24, 6 }, new[] { 0.7f, 0.3f }, ref rng);
            var pixels = new Color[size * size];

            var boardShade = new float[boards];
            for (int i = 0; i < boards; i++) boardShade[i] = rng.Range(0.88f, 1f);

            for (int y = 0; y < size; y++)
            {
                int board = y / boardHeight;
                float inBoard = (y % boardHeight) / (float)boardHeight;

                for (int x = 0; x < size; x++)
                {
                    float shade = boardShade[board] * Mathf.Lerp(0.8f, 1f, inBoard);
                    if (inBoard < 0.08f) shade *= 0.6f;

                    // Grain runs along the board: sample the noise squashed flat.
                    shade *= 0.94f + grain.Sample(x, (y / 8) * 8) * 0.08f;

                    float s = streaks.Sample(x, 0);
                    if (s > 0.62f) shade *= Mathf.Lerp(1f, 0.9f, (s - 0.62f) / 0.38f);

                    pixels[y * size + x] = new Color(shade, shade, shade, 1f);
                }
            }

            return Save(SidingPath, pixels, size, TextureImporterType.Default, true, TextureWrapMode.Repeat);
        }

        /// <summary>Corrugated sheet, ridges running up it, grey going to rust in patches. One tile is 0.8 m.</summary>
        public static Texture2D EnsureMetal()
        {
            Texture2D existing = Current(MetalPath);
            if (existing != null) return existing;

            ContentGenerator.EnsureFolder(Folder);

            const int size = 256;
            var rng = new DeterministicRandom(311);
            var rust = new Periodic(size, new[] { 4, 8, 16 }, new[] { 0.5f, 0.3f, 0.2f }, ref rng);
            var pixels = new Color[size * size];

            Color metal = Hex("9AA0A4");
            Color rusted = Hex("8B5B3E");
            const float ridges = 12f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float wave = 0.78f + 0.22f * Mathf.Sin(x / (float)size * ridges * Mathf.PI * 2f);
                    float r = Mathf.Clamp01((rust.Sample(x, y) - 0.45f) * 3f);
                    Color c = Color.Lerp(metal, rusted, r) * wave;
                    pixels[y * size + x] = new Color(c.r, c.g, c.b, 1f);
                }
            }

            return Save(MetalPath, pixels, size, TextureImporterType.Default, true, TextureWrapMode.Repeat);
        }

        // --- Painting ------------------------------------------------------------------------------------

        /// <summary>0 below the threshold, 1 above it, with an eased edge <paramref name="softness"/> either side:
        /// the shader smoothstep, which <c>Mathf.SmoothStep</c> is not.</summary>
        static float Band(float value, float threshold, float softness)
        {
            float t = Mathf.Clamp01((value - (threshold - softness)) / (2f * softness));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Blends one pixel toward a colour, wrapping round the tile's edges.</summary>
        static void Blend(Color[] pixels, int size, int x, int y, Color c, float a)
        {
            if (a <= 0f) return;

            int i = Wrap(y, size) * size + Wrap(x, size);
            Color o = pixels[i];
            pixels[i] = new Color(o.r + (c.r - o.r) * a, o.g + (c.g - o.g) * a, o.b + (c.b - o.b) * a, 1f);
        }

        /// <summary>A flat round shape with a crisp, anti-aliased edge.</summary>
        static void Disc(Color[] pixels, int size, Vector2 centre, float radius, Color c, float opacity)
        {
            int r = Mathf.CeilToInt(radius + 1f);
            int x0 = Mathf.FloorToInt(centre.x), y0 = Mathf.FloorToInt(centre.y);

            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    float d = Vector2.Distance(new Vector2(x0 + dx + 0.5f, y0 + dy + 0.5f), centre);
                    Blend(pixels, size, x0 + dx, y0 + dy, c, Mathf.Clamp01(radius + 0.5f - d) * opacity);
                }
            }
        }

        /// <summary>A soft oval, for a shadow on the ground: solid in the middle, fading over its outer third.</summary>
        static void Ellipse(Color[] pixels, int size, Vector2 centre, float rx, float ry, Color c, float opacity)
        {
            int ix = Mathf.CeilToInt(rx + 1f), iy = Mathf.CeilToInt(ry + 1f);
            int x0 = Mathf.FloorToInt(centre.x), y0 = Mathf.FloorToInt(centre.y);

            for (int dy = -iy; dy <= iy; dy++)
            {
                for (int dx = -ix; dx <= ix; dx++)
                {
                    float nx = (x0 + dx + 0.5f - centre.x) / rx;
                    float ny = (y0 + dy + 0.5f - centre.y) / ry;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    Blend(pixels, size, x0 + dx, y0 + dy, c, Mathf.Clamp01((1f - d) / 0.35f) * opacity);
                }
            }
        }

        /// <summary>
        /// One grass blade: a pointed, gently bent wedge from its root along <paramref name="dir"/>,
        /// base colour at the root going to the tip colour, the left half a shade darker - the lit and
        /// shaded faces a low-poly blade would have.
        /// </summary>
        static void Blade(Color[] pixels, int size, Vector2 root, Vector2 dir, float length, float width, Color baseColour,
                          Color tipColour)
        {
            var normal = new Vector2(dir.y, -dir.x);
            float bend = length * 0.12f * Mathf.Sign(dir.x == 0f ? 1f : dir.x);

            float reach = length + width;
            int x0 = Mathf.FloorToInt(root.x - reach), x1 = Mathf.CeilToInt(root.x + reach);
            int y0 = Mathf.FloorToInt(root.y - reach), y1 = Mathf.CeilToInt(root.y + reach);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - root;
                    float along = Vector2.Dot(p, dir);
                    float t = along / length;
                    if (t < -0.05f || t > 1.02f) continue;

                    float tc = Mathf.Clamp01(t);
                    float side = Vector2.Dot(p, normal) - bend * tc * tc;
                    float half = width * 0.5f * Mathf.Pow(1f - tc, 0.85f);

                    // Crisp sides, a crisp root, and the point itself left to the taper.
                    float coverage = Mathf.Clamp01(half - Mathf.Abs(side) + 0.5f) * Mathf.Clamp01(along + 0.5f);
                    if (coverage <= 0f) continue;

                    Color c = Color.Lerp(baseColour, tipColour, Mathf.Pow(tc, 1.2f));
                    if (side < 0f) c *= 0.88f;

                    Blend(pixels, size, x, y, c, coverage);
                }
            }
        }

        /// <summary>A straight stroke with round ends.</summary>
        static void Line(Color[] pixels, int size, Vector2 a, Vector2 b, float width, Color c, float opacity)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - width - 1f), x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + width + 1f);
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - width - 1f), y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + width + 1f);

            Vector2 ab = b - a;
            float len2 = Mathf.Max(1e-4f, ab.sqrMagnitude);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float d = Vector2.Distance(p, a + ab * t);
                    Blend(pixels, size, x, y, c, Mathf.Clamp01(width * 0.5f + 0.5f - d) * opacity);
                }
            }
        }

        static Texture2D Save(string path, Color[] pixels, int size, TextureImporterType type, bool srgb, TextureWrapMode wrap,
                              string version = Version) =>
            Save(path, pixels, size, size, type, srgb, wrap, wrap, version);

        /// <summary>
        /// Any shape of texture, wrapping differently along U and V: a road repeats along its length
        /// and must not across it, or one kerb's edge line bleeds onto the other side.
        /// </summary>
        static Texture2D Save(string path, Color[] pixels, int width, int height, TextureImporterType type, bool srgb,
                              TextureWrapMode wrapU, TextureWrapMode wrapV, string version)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.wrapModeU = wrapU;
            importer.wrapModeV = wrapV;
            if (width > 2048 || height > 2048) importer.maxTextureSize = 4096;
            importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.anisoLevel = 4;
            importer.userData = version;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>The texture at <paramref name="path"/> if it was painted by this version, else null.</summary>
        static Texture2D Current(string path, string version = Version)
        {
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null || importer.userData != version) return null;
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static int Wrap(int v, int size) => ((v % size) + size) % size;

        static Color Hex(string rgb) => PrimitiveKit.Hex(rgb);

        // --- Noise that tiles -----------------------------------------------------------------------------

        /// <summary>Fractal value noise on lattices that repeat exactly once across the tile.</summary>
        sealed class Periodic
        {
            readonly int size;
            readonly int[] periods;
            readonly float[] weights;
            readonly float[][] lattices;

            public Periodic(int size, int[] periods, float[] weights, ref DeterministicRandom rng)
            {
                this.size = size;
                this.periods = periods;
                this.weights = weights;
                lattices = new float[periods.Length][];

                for (int o = 0; o < periods.Length; o++)
                {
                    int p = periods[o];
                    lattices[o] = new float[p * p];
                    for (int i = 0; i < p * p; i++) lattices[o][i] = rng.NextFloat();
                }
            }

            public float Sample(int x, int y)
            {
                float value = 0f;
                for (int o = 0; o < periods.Length; o++)
                {
                    int p = periods[o];
                    float fx = Wrap(x, size) / (float)size * p;
                    float fy = Wrap(y, size) / (float)size * p;
                    int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                    float tx = Smooth(fx - x0), ty = Smooth(fy - y0);

                    float[] l = lattices[o];
                    float a = l[(y0 % p) * p + x0 % p];
                    float b = l[(y0 % p) * p + (x0 + 1) % p];
                    float c = l[((y0 + 1) % p) * p + x0 % p];
                    float d = l[((y0 + 1) % p) * p + (x0 + 1) % p];

                    value += Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty) * weights[o];
                }

                return value;
            }

            static float Smooth(float t) => t * t * (3f - 2f * t);
        }
    }
}
