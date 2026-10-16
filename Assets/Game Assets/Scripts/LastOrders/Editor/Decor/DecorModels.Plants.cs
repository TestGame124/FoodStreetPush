using System.Collections.Generic;
using UnityEngine;
using Model = LastOrders.EditorTools.BuildingKit.Model;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The decorations' models, each standing on the floor at its footprint's middle and facing -Z
    /// (toward the game's camera), painted in <see cref="DecorKit"/>'s palette. This part: the greenery.
    /// </summary>
    public static partial class DecorModels
    {
        const float F = DecorKit.Floor;

        static Vector2 V(float r, float y) => new Vector2(r, y);

        static float Jitter(int key) => BuildingKit.Jitter(key);

        /// <summary>
        /// A big leafy house plant in a glazed teal pot: a darker foot, a cream band and a rolled cream
        /// lip like the chairs' cream shells, dark soil, and a fountain of broad glossy leaves on their
        /// own stalks - long and drooping low down, smaller and more upright at the crown.
        /// </summary>
        public static Model PottedPlant()
        {
            Model m = DecorKit.New();
            var foot = new Vector3(0f, F, 0f);

            // (radius, height): foot, body flaring out, cream band, neck, rolled lip, then down inside.
            var pot = new List<Vector2>
            {
                V(0f, 0f), V(0.15f, 0f), V(0.162f, 0.012f), V(0.16f, 0.045f), V(0.172f, 0.056f),
                V(0.195f, 0.15f), V(0.217f, 0.25f), V(0.229f, 0.318f), V(0.2295f, 0.322f),
                V(0.236f, 0.37f), V(0.2365f, 0.374f), V(0.243f, 0.41f),
                V(0.258f, 0.42f), V(0.27f, 0.437f), V(0.268f, 0.455f), V(0.252f, 0.463f), V(0.236f, 0.452f), V(0.229f, 0.43f),
            };

            DecorKit.Turned(m, y => y < 0.05f ? DecorKit.TealDark
                                  : y > 0.32f && y < 0.372f ? DecorKit.Cream
                                  : y > 0.412f ? DecorKit.Cream
                                  : DecorKit.Teal, foot, pot, 26);

            const float SoilTop = 0.445f;
            DecorKit.Turned(m, DecorKit.Soil, foot, new List<Vector2>
            {
                V(0.232f, 0.425f), V(0.2f, 0.438f), V(0.12f, SoilTop), V(0f, SoilTop + 0.006f),
            }, 20);

            // Leaves round the stalk by the golden angle, low ones first.
            const int Leaves = 15;
            var crown = new Vector3(0f, F + SoilTop, 0f);
            for (int i = 0; i < Leaves; i++)
            {
                float t = i / (float)(Leaves - 1);
                Vector3 heading = Quaternion.Euler(0f, 25f + i * 137.5f, 0f) * Vector3.forward;
                float height = Mathf.Lerp(0.12f, 0.6f, t) + 0.03f * Jitter(i * 3 + 1);

                Vector3 root = crown + heading * Mathf.Lerp(0.07f, 0.04f, t) + Vector3.up * height;
                DecorKit.Rod(m, DecorKit.Stem, crown + heading * 0.02f - Vector3.up * 0.01f, root, 0.013f, 0.009f, 6);

                float length = Mathf.Lerp(0.42f, 0.3f, t) * (1f + 0.06f * Jitter(i * 5 + 2));
                float rise = Mathf.Lerp(0.3f, 1.55f, t);
                float droop = Mathf.Lerp(0.8f, 1.05f, t);
                Color colour = DecorKit.Tone(Color.Lerp(DecorKit.Leaf, DecorKit.LeafLight, 0.15f + t * 0.5f), 1f + 0.05f * Jitter(i * 7 + 3));
                DecorKit.LeafBlade(m, root, heading, rise, droop, length, length * 0.56f, 0.26f, colour);
            }

            return m;
        }

        /// <summary>
        /// A long planter of honey-wood planks with teal corner posts and feet, like the dining chairs'
        /// teal frames, capped with a lighter rim; a mound of soil, clumps of leaves and flowers in the
        /// diner's colours - cushion red, cream, butter and coral. Two cells long, one deep.
        /// </summary>
        public static Model FlowerPlanter()
        {
            Model m = DecorKit.New();
            const float L = 1.72f, D = 0.5f, H = 0.44f, Lift = 0.06f;
            float y0 = F + Lift;

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                DecorKit.Box(m, DecorKit.TealDark, new Vector3(sx * (L * 0.5f - 0.07f), F + Lift * 0.5f + 0.004f, sz * (D * 0.5f - 0.07f)),
                             new Vector3(0.1f, Lift + 0.012f, 0.1f), 0.025f);
                DecorKit.Box(m, DecorKit.Teal, new Vector3(sx * (L * 0.5f - 0.01f), y0 + H * 0.5f, sz * (D * 0.5f - 0.01f)),
                             new Vector3(0.075f, H + 0.02f, 0.075f), 0.025f);
            }

            // A dark body behind the planks, so the gaps between them read as grooves.
            DecorKit.Box(m, DecorKit.Groove, new Vector3(0f, y0 + (H - 0.07f) * 0.5f, 0f), new Vector3(L - 0.07f, H - 0.07f, D - 0.07f), 0.01f, 1);

            // Three courses of planks a side, each its own shade of the honey wood.
            const int Courses = 3;
            float course = H / Courses;
            for (int r = 0; r < Courses; r++)
            {
                float cy = y0 + course * (r + 0.5f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Color c = DecorKit.Tone(DecorKit.Honey, 1f + 0.05f * Jitter(r * 7 + side * 3 + 11));
                    DecorKit.Box(m, c, new Vector3(0f, cy, side * (D * 0.5f - 0.025f)), new Vector3(L - 0.07f, course - 0.014f, 0.05f), 0.02f);
                    Color e = DecorKit.Tone(DecorKit.Honey, 0.97f + 0.05f * Jitter(r * 13 + side * 5 + 17));
                    DecorKit.Box(m, e, new Vector3(side * (L * 0.5f - 0.025f), cy, 0f), new Vector3(0.05f, course - 0.014f, D - 0.07f), 0.02f);
                }
            }

            // The rim: four lighter capping boards.
            Color cap = DecorKit.Tone(DecorKit.Honey, 1.04f);
            for (int side = -1; side <= 1; side += 2)
            {
                DecorKit.Box(m, cap, new Vector3(0f, y0 + H + 0.016f, side * (D * 0.5f - 0.02f)), new Vector3(L + 0.04f, 0.034f, 0.085f), 0.014f);
                DecorKit.Box(m, cap, new Vector3(side * (L * 0.5f - 0.02f), y0 + H + 0.015f, 0f), new Vector3(0.085f, 0.032f, D - 0.04f), 0.014f);
            }

            // Soil heaped a little in the middle.
            float soil = y0 + H - 0.045f;
            var bed = new Rect(-L * 0.5f + 0.05f, -D * 0.5f + 0.05f, L - 0.1f, D - 0.1f);
            m.Heightfield((p, n) => DecorKit.Tone(DecorKit.Soil, 0.94f + 0.08f * Mathf.Sin(p.x * 23f + p.z * 31f)), bed, 18,
                          (x, z) => soil + 0.018f * Mathf.Cos(z / (D * 0.5f) * Mathf.PI * 0.5f) * Mathf.Cos(x / (L * 0.5f) * Mathf.PI * 0.5f));

            // Clumps of leaves along the bed, each with flowers standing out of it.
            Color[] blooms = { DecorKit.Red, DecorKit.CreamLight, DecorKit.Butter, DecorKit.Coral };
            const int Clumps = 6;
            int flower = 0;
            for (int c = 0; c < Clumps; c++)
            {
                var at = new Vector3(Mathf.Lerp(-0.66f, 0.66f, c / (float)(Clumps - 1)) + 0.04f * Jitter(c * 3 + 5), soil + 0.012f,
                                     0.05f * Jitter(c * 7 + 2));
                for (int k = 0; k < 6; k++)
                {
                    Vector3 heading = Quaternion.Euler(0f, k * 60f + 30f * Jitter(c * 11 + k), 0f) * Vector3.forward;
                    float length = 0.17f + 0.03f * Jitter(c * 17 + k * 3);
                    Color leaf = DecorKit.Tone(DecorKit.Leaf, 1.02f + 0.06f * Jitter(c * 19 + k));
                    DecorKit.LeafBlade(m, at + heading * 0.015f, heading, 0.95f, 1.15f, length, length * 0.48f, 0.22f, leaf, 6);
                }

                for (int k = 0; k < 3; k++)
                {
                    Vector3 offset = new Vector3(0.07f * Jitter(c * 23 + k * 7 + 1), 0f, 0.06f * Jitter(c * 29 + k * 5 + 3));
                    float height = 0.11f + 0.1f * ((k + 1) % 3) * 0.5f + 0.02f * Jitter(c * 31 + k);
                    Vector3 head = at + offset + Vector3.up * height;
                    DecorKit.Rod(m, DecorKit.Stem, at + offset * 0.5f, head, 0.007f, 0.005f, 5);
                    Flower(m, head, blooms[flower % blooms.Length], 0.1f, c * 47f + k * 23f, c * 7 + k);
                    flower++;
                }
            }

            return m;
        }

        /// <summary>
        /// A five-petalled flower at <paramref name="at"/> about <paramref name="size"/> across, its
        /// petals cupped up a little, paler down the middle and deeper at the eye, which is butter
        /// yellow (red on a butter flower).
        /// </summary>
        static void Flower(Model m, Vector3 at, Color petals, float size, float spin, int key)
        {
            for (int k = 0; k < 5; k++)
            {
                Vector3 heading = Quaternion.Euler(0f, spin + k * 72f, 0f) * Vector3.forward;
                Color c = DecorKit.Tone(petals, 1f + 0.05f * Jitter(key * 13 + k));
                DecorKit.LeafBlade(m, at + heading * (size * 0.04f), heading, 0.35f, 0.25f, size * 0.52f, size * 0.42f, 0.18f, c, 3);
            }

            Color eye = petals == DecorKit.Butter ? DecorKit.RedLight : DecorKit.Butter;
            DecorKit.Ball(m, eye, at + Vector3.up * (size * 0.05f), size * 0.15f, 8, 4);
        }
    }
}
