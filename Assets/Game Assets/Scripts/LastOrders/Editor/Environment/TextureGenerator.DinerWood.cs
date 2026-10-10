using LastOrders.Core;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The restaurant's wood-and-cream floor: varnished planks in the dining room and big cream
    /// tiles along the kitchen wall. New files beside the checkerboard's, so that look stays on disk
    /// as it was (see <see cref="DinerShellModel"/>, which swaps between them).
    /// </summary>
    public static partial class TextureGenerator
    {
        public const string DinerPlanksPath = Folder + "/Floor_DinerPlanks.png";
        public const string KitchenCreamPath = Folder + "/Floor_KitchenCream.png";

        const string WoodFloorVersion = "floor-wood-3";

        /// <summary>Metres one copy of the planks covers, and how many rows of boards run across it: boards 0.24 m wide,
        /// long enough that a row reads as boards rather than bricks.</summary>
        public const float PlankTile = 4.8f;
        public const int PlankRows = 20;

        /// <summary>Metres one copy of the kitchen's tiles covers, two by two: tiles of 0.6 m.</summary>
        public const float CreamTile = 1.2f;

        /// <summary>
        /// The dining room: planks running along U - across the room, parallel to the front wall -
        /// each row's boards of their own lengths, the rows' joints staggered. A warm mid brown,
        /// every board a shade apart, with a soft grain along it, a hairline seam round it and a
        /// little sheen of varnish here and there. One tile covers 4.8 m.
        /// </summary>
        public static Texture2D EnsureDinerPlanks()
        {
            Texture2D existing = CurrentRelief(DinerPlanksPath, WoodFloorVersion);
            if (existing != null) return existing;

            ContentGenerator.EnsureFolder(Folder);

            var rng = new DeterministicRandom(7311);
            return SaveRelief(DinerPlanksPath, PaintDinerPlanks(ref rng), 1f, TextureWrapMode.Repeat, TextureWrapMode.Repeat,
                              WoodFloorVersion);
        }

        static Relief PaintDinerPlanks(ref DeterministicRandom rng)
        {
            const int size = 2048;
            const float metres = PlankTile;
            float row = metres / PlankRows;
            const float gap = 0.004f;

            // Painted darker than it shows: a floor facing the sky comes out nearly half again as bright.
            // Close together, so the floor reads as one timber rather than a patchwork.
            Color[] woods = { Hex("735334"), Hex("6F5032"), Hex("775637"), Hex("6D4E31"), Hex("795838"), Hex("715234") };
            Color seam = Hex("43301F");
            Color grain = Hex("62472E");
            Color sheen = Hex("8E6D4C");

            var r = new Relief(size, size, metres, metres);
            r.Fill(seam);

            for (int j = 0; j < PlankRows; j++)
            {
                float v0 = j * row, v1 = v0 + row;

                // Round the tile from wherever this row's first joint falls, so the rows' joints never line up.
                float start = rng.Range(0f, metres);
                float end = start + metres;
                float u = start;
                int previous = -1;

                while (u < end - 0.05f)
                {
                    float length = rng.Range(1.3f, 2.5f);
                    if (end - (u + length) < 0.8f) length = end - u;

                    int pick = rng.Range(0, woods.Length);
                    if (pick == previous) pick = (pick + 1) % woods.Length;
                    previous = pick;

                    Color c = woods[pick] * (1f + rng.Range(-0.015f, 0.015f));
                    r.Slab(u + gap * 0.5f, v0 + gap * 0.5f, u + length - gap * 0.5f, v1 - gap * 0.5f, c, 0.01f, 0.0035f, 0.003f);

                    // The grain: a few long soft streaks along the board, darker and lighter, kept inside its ends.
                    int streaks = rng.Range(2, 5);
                    for (int s = 0; s < streaks; s++)
                    {
                        float reach = length * rng.Range(0.18f, 0.4f);
                        float cu = u + rng.Range(reach, Mathf.Max(reach, length - reach));
                        float cv = rng.Range(v0 + row * 0.2f, v1 - row * 0.2f);
                        bool dark = rng.Chance(0.65f);
                        r.Stain(cu, cv, reach, rng.Range(0.004f, 0.01f), 0f, dark ? grain : sheen, rng.Range(0.08f, 0.14f), 0.9f);
                    }

                    // Now and then a knot: a small dark oval with the grain swept round it.
                    if (rng.Chance(0.18f))
                    {
                        float cu = u + rng.Range(0.15f, Mathf.Max(0.16f, length - 0.15f));
                        float cv = rng.Range(v0 + row * 0.3f, v1 - row * 0.3f);
                        r.Stain(cu, cv, 0.05f, 0.012f, 0f, grain, 0.18f, 0.8f);
                        r.Stain(cu, cv, 0.014f, 0.008f, 0f, seam, 0.35f, 0.7f);
                    }

                    u += length;
                }
            }

            Polish(r, 30, sheen, ref rng);
            Scuffs(r, 20, Hex("553D29"), ref rng);
            r.Occlude(0.02f, 0.003f, seam * 0.9f, 0.3f, sheen, 0.06f);
            return r;
        }

        /// <summary>
        /// The kitchen strip: big square tiles in a warm cream, each a shade apart and lit across
        /// like the old ones, a soft beige seam between - a clean working floor that sets the stoves
        /// off against the wood. Two by two; one tile covers 1.2 m, tiles of 0.6 m.
        /// </summary>
        public static Texture2D EnsureKitchenCream()
        {
            Texture2D existing = CurrentRelief(KitchenCreamPath, WoodFloorVersion);
            if (existing != null) return existing;

            ContentGenerator.EnsureFolder(Folder);

            var rng = new DeterministicRandom(8317);
            return SaveRelief(KitchenCreamPath, PaintKitchenCream(ref rng), 1f, TextureWrapMode.Repeat, TextureWrapMode.Repeat,
                              WoodFloorVersion);
        }

        static Relief PaintKitchenCream(ref DeterministicRandom rng)
        {
            const float metres = CreamTile;
            const int count = 2;

            // Painted well under how it shows: a floor facing the sky comes out nearly half again as bright.
            Color[] cream = { Hex("A69682"), Hex("A2927E"), Hex("AA9A86"), Hex("9F8F7B") };
            var pick = new int[count * count];
            for (int k = 0; k < pick.Length; k++) pick[k] = (k + rng.Range(0, 3)) % cream.Length;

            Color seam = Hex("7F6E5C");
            Relief r = TileFloor(512, metres, count, (i, j) => cream[pick[j * count + i]], seam, joint: 0.007f, bevel: 0.018f,
                                 height: 0.004f, variation: 0.02f, tilt: 0.008f, ref rng);

            TileLight(r, metres / count, count, 0.07f, ref rng);
            Polish(r, 5, Hex("B6A28B"), ref rng);
            Scuffs(r, 5, Hex("857462"), ref rng);
            r.Occlude(0.015f, 0.004f, seam * 0.9f, 0.25f, Hex("B19D86"), 0.06f);
            return r;
        }
    }
}
