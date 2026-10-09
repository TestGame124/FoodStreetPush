using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The salad station's dishes and the ingredients it keeps, modelled for their icons
    /// (<see cref="SaladContent"/>) in the style and paints of the produce and the other dishes.
    ///
    /// The salads come in the diner's crockery - a wide salad bowl, or the dinner plate the cornbread
    /// is served on - and each shows what goes into it: a garden salad of leaves and tomato wedges,
    /// a sweetcorn salad heaped with kernels, an orange carrot slaw, tomato and cheese slices fanned
    /// round a plate, a bowl of beans with corn and tomato, sliced grilled chicken on a bed of leaves.
    /// The ingredients the farm does not grow - lettuce, carrots, cheese, a tin of beans, chicken -
    /// get icons of their own the same way.
    ///
    /// Each stands on the origin, +Y up, its best side toward -Z, where the icon camera looks from.
    /// </summary>
    public static partial class CropModels
    {
        public enum Ingredient { Lettuce, Carrot, Cheese, CannedBeans, Chicken, Egg, Milk }

        /// <summary>The ingredient, as its item icon shows it.</summary>
        public static SmoothMesh Stocked(Ingredient ingredient)
        {
            var mesh = new SmoothMesh((int)Paint.Count);
            var dice = new Dice(48611 * ((int)ingredient + 7));

            switch (ingredient)
            {
                case Ingredient.Lettuce: LettuceHead(mesh, dice); break;
                case Ingredient.Carrot: Carrots(mesh, dice); break;
                case Ingredient.Cheese: CheeseWedge(mesh, dice); break;
                case Ingredient.CannedBeans: BeanTin(mesh, dice); break;
                case Ingredient.Chicken: Drumsticks(mesh, dice); break;
                case Ingredient.Egg: EggNest(mesh, dice); break;
                case Ingredient.Milk: MilkBottle(mesh, dice); break;
            }

            return mesh;
        }

        // --- The salads -----------------------------------------------------------------------------

        /// <summary>The salad bowl's rim height and the radius inside it, where the heap of salad starts.</summary>
        const float SaladRim = 0.08f, SaladInside = 0.122f;

        /// <summary>Leaves heaped up out of the bowl, tomato wedges tucked in on top.</summary>
        static void GardenSalad(SmoothMesh m, Dice d)
        {
            SaladBowl(m);
            LeafBed(m, d, 9, 4);

            foreach (float a in new[] { 20f, 95f, 165f, 245f, 310f })
            {
                float r = d.Range(0.045f, 0.065f);
                Wedge(m, Horizontal(a) * r + Vector3.up * (SaladRim + 0.045f), 0.034f, a + 90f + d.Range(-25f, 25f), d.Range(-35f, 35f));
            }
        }

        /// <summary>A ring of leaves round the bowl and a heap of sweetcorn in the middle, three tomato wedges against it.</summary>
        static void SweetcornSalad(SmoothMesh m, Dice d)
        {
            SaladBowl(m);
            LeafBed(m, d, 9, 0);

            Blob(m, Paint.Kernel, new Vector3(0f, SaladRim + 0.012f, 0f), new Vector3(0.08f, 0.03f, 0.08f), Quaternion.identity, 20, 8);
            Kernels(m, d, 110, 0.08f, 0.032f);

            foreach (float a in new[] { 140f, 225f, 320f })
            {
                Vector3 at = Horizontal(a) * 0.078f + Vector3.up * (SaladRim + 0.032f);
                Wedge(m, at, 0.032f, a + 90f + d.Range(-20f, 20f), d.Range(-35f, 35f));
            }
        }

        /// <summary>Shredded carrot heaped in the bowl, a few leaves standing up behind it and a sprig of herbs on top.</summary>
        static void CarrotSlaw(SmoothMesh m, Dice d)
        {
            SaladBowl(m);

            foreach (float a in new[] { -50f, 0f, 50f })
            {
                Vector3 at = Horizontal(a) * 0.03f + Vector3.up * (SaladRim + 0.004f);
                SaladLeaf(m, d, a == 0f ? Paint.LettuceHeart : Paint.Lettuce, at, a + d.Range(-8f, 8f), 0.13f, 48f, -10f, 0.05f);
            }

            Blob(m, Paint.Carrot, new Vector3(0f, SaladRim - 0.004f, 0f), new Vector3(0.112f, 0.036f, 0.112f), Quaternion.identity, 24, 10);

            for (int i = 0; i < 190; i++)
            {
                float r = 0.105f * Mathf.Sqrt(d.Value);
                float a = d.Range(0f, 360f);
                Vector3 at = Horizontal(a) * r + Vector3.up * (Heap(r, 0.036f) + d.Range(0.001f, 0.006f));
                Vector3 along = Direction(d.Range(0f, 360f), d.Range(-15f, 25f)) * d.Range(0.022f, 0.032f);
                m.Tube((int)Paint.Carrot, at - along, at + along, 0.0034f, 0.003f, 5);
            }

            for (int k = 0; k < 3; k++)
            {
                Vector3 at = new Vector3(-0.012f, Heap(0f, 0.036f) + 0.008f, -0.01f);
                Leaf(m, Paint.Basil, Spine.Arc(at, 150f + k * 100f + d.Range(-10f, 10f), 0.04f, 20f, -10f), 0.014f, Ovate, 0.35f);
            }
        }

        /// <summary>Tomato slices and slices of cheese laid round a plate in turn, each leaning on the last, basil in the middle.</summary>
        static void TomatoCheeseSalad(SmoothMesh m, Dice d)
        {
            float well = Plate(m, 0.17f);
            const int slices = 10;
            const float ring = 0.088f;

            for (int i = 0; i < slices; i++)
            {
                float a = i * 360f / slices + 10f;
                Vector3 radial = Horizontal(a);
                Quaternion turn = Quaternion.AngleAxis(-15f, radial) * Quaternion.Euler(0f, a, 0f);
                Vector3 at = radial * ring + Vector3.up * (well + 0.012f);

                if (i % 2 == 0) TomatoSlice(m, at, 0.042f, 0.011f, turn);
                else m.RoundedBox((int)Paint.Cheese, at + turn * (Vector3.up * 0.005f), new Vector3(0.062f, 0.009f, 0.074f), 0.0035f, turn, 2);
            }

            for (int k = 0; k < 4; k++)
            {
                Vector3 at = new Vector3(0.004f, well + 0.006f, -0.004f);
                Leaf(m, Paint.Basil, Spine.Arc(at, 30f + k * 90f + d.Range(-15f, 15f), 0.05f, 22f, -6f), 0.019f, Ovate, 0.3f);
            }
        }

        /// <summary>Kidney beans and white beans with sweetcorn and diced tomato, heaped in the bowl, a few herb leaves over them.</summary>
        static void BeanSalad(SmoothMesh m, Dice d)
        {
            SaladBowl(m);
            LeafBed(m, d, 8, 0);

            const float radius = 0.088f, peak = 0.032f;
            Blob(m, Paint.BeanWhite, new Vector3(0f, SaladRim + 0.012f, 0f), new Vector3(radius, peak, radius), Quaternion.identity, 24, 10);

            for (int i = 0; i < 190; i++)
            {
                float r = (radius - 0.006f) * Mathf.Sqrt(d.Value);
                float a = d.Range(0f, 360f);
                Vector3 at = Horizontal(a) * r + Vector3.up * (Mound(r, radius, peak) + 0.004f);
                var turn = Quaternion.Euler(d.Range(-20f, 20f), d.Range(0f, 360f), d.Range(-20f, 20f));
                float pick = d.Value;

                if (pick < 0.42f) Blob(m, Paint.Bean, at, new Vector3(0.0135f, 0.0075f, 0.0085f), turn, 10, 7);
                else if (pick < 0.68f) Blob(m, Paint.BeanWhite, at, new Vector3(0.0115f, 0.007f, 0.0075f), turn, 10, 7);
                else if (pick < 0.88f) Blob(m, Paint.Kernel, at, new Vector3(0.0075f, 0.0056f, 0.0068f), turn, 9, 6);
                else m.RoundedBox((int)Paint.TomatoFlesh, at, Vector3.one * 0.017f, 0.004f, turn, 2);
            }

            for (int k = 0; k < 6; k++)
            {
                float a = k * 60f + d.Range(-15f, 15f);
                float r = d.Range(0.02f, 0.06f);
                Vector3 at = Horizontal(a) * r + Vector3.up * (Mound(r, radius, peak) + 0.01f);
                Leaf(m, Paint.Basil, Spine.Arc(at, d.Range(0f, 360f), 0.026f, 15f, -10f), 0.01f, Ovate, 0.3f);
            }
        }

        /// <summary>A bed of leaves on the plate, grilled chicken sliced and fanned across it, tomato wedges at the side.</summary>
        static void ChickenSalad(SmoothMesh m, Dice d)
        {
            float well = Plate(m, 0.17f);

            Blob(m, Paint.LettuceHeart, new Vector3(0f, well, 0f), new Vector3(0.08f, 0.02f, 0.08f), Quaternion.identity, 20, 8);
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f + d.Range(-10f, 10f);
                Vector3 at = Horizontal(a) * 0.015f + Vector3.up * (well + 0.012f);
                SaladLeaf(m, d, k % 3 == 0 ? Paint.LettuceHeart : Paint.Lettuce, at, a, d.Range(0.11f, 0.13f), 16f, -12f, 0.048f);
            }

            // One breast sliced and fanned out over the leaves from the front, each slice on its pale
            // cut face with the browned outside of the breast along one edge.
            var pivot = new Vector3(0.0f, well + 0.03f, -0.075f);
            const int slices = 5;
            for (int i = 0; i < slices; i++)
            {
                float yaw = -44f + i * 22f + d.Range(-3f, 3f);
                Vector3 at = pivot + Horizontal(yaw) * 0.082f + Vector3.up * (0.0025f * i);
                ChickenSlice(m, at, Quaternion.Euler(0f, yaw - 90f, 0f) * Quaternion.Euler(0f, 0f, -4f), 0.12f - Mathf.Abs(i - 2) * 0.008f);
            }

            foreach (float a in new[] { 200f, 235f, 270f })
            {
                Vector3 at = Horizontal(a) * 0.1f + Vector3.up * (well + 0.02f);
                Wedge(m, at, 0.029f, a + 90f + d.Range(-15f, 15f), d.Range(-35f, 35f));
            }
        }

        // --- Salad parts ----------------------------------------------------------------------------

        /// <summary>
        /// A salad bowl in the diner's glaze, wider and shallower than the soup bowl: a foot, a round
        /// belly, the teal band under the rim, the rim, and down inside to its floor.
        /// </summary>
        static void SaladBowl(SmoothMesh m)
        {
            var profile = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.05f, 0f), new Vector2(0.055f, 0.004f), new Vector2(0.058f, 0.008f),
                new Vector2(0.1f, 0.026f), new Vector2(0.126f, 0.05f), new Vector2(0.137f, 0.07f), new Vector2(0.141f, SaladRim - 0.002f),
                new Vector2(0.137f, SaladRim + 0.002f), new Vector2(0.131f, SaladRim - 0.003f), new Vector2(0.12f, 0.058f),
                new Vector2(0.095f, 0.032f), new Vector2(0.05f, 0.016f), new Vector2(0f, 0.013f),
            };
            var subs = new List<int>();
            for (int i = 0; i < profile.Count - 1; i++) subs.Add(i == 5 ? (int)Paint.GlazeBand : (int)Paint.Glaze);
            m.Lathe(Vector3.zero, profile, subs, 44);
        }

        /// <summary>How high a heap <paramref name="peak"/> tall over the bowl's rim stands <paramref name="r"/> from the middle.</summary>
        static float Heap(float r, float peak) => SaladRim - 0.004f + peak * Mathf.Sqrt(Mathf.Max(0f, 1f - (r / 0.114f) * (r / 0.114f)));

        /// <summary>How high a mound <paramref name="radius"/> across and <paramref name="peak"/> tall, heaped on the leaves, stands <paramref name="r"/> from the middle.</summary>
        static float Mound(float r, float radius, float peak) =>
            SaladRim + 0.012f + peak * Mathf.Sqrt(Mathf.Max(0f, 1f - (r / radius) * (r / radius)));

        /// <summary>
        /// Leaves heaped in the bowl: a pale mound so nothing shows through between them,
        /// <paramref name="outer"/> leaves spreading out and down over the rim and
        /// <paramref name="inner"/> standing up in the middle.
        /// </summary>
        static void LeafBed(SmoothMesh m, Dice d, int outer, int inner)
        {
            Blob(m, Paint.LettuceHeart, new Vector3(0f, SaladRim - 0.012f, 0f), new Vector3(0.118f, 0.042f, 0.118f), Quaternion.identity, 24, 10);

            for (int k = 0; k < outer; k++)
            {
                float a = k * 360f / outer + d.Range(-10f, 10f);
                Vector3 at = Horizontal(a) * 0.025f + Vector3.up * (SaladRim + 0.008f);
                SaladLeaf(m, d, k % 3 == 1 ? Paint.LettuceHeart : Paint.Lettuce, at, a, d.Range(0.125f, 0.15f), d.Range(26f, 34f), d.Range(-34f, -22f),
                          d.Range(0.046f, 0.056f));
            }

            for (int k = 0; k < inner; k++)
            {
                float a = k * 360f / inner + 25f + d.Range(-12f, 12f);
                Vector3 at = Horizontal(a) * 0.008f + Vector3.up * (SaladRim + 0.03f);
                SaladLeaf(m, d, k % 2 == 0 ? Paint.Lettuce : Paint.LettuceHeart, at, a, d.Range(0.08f, 0.095f), 52f, -4f, 0.04f);
            }
        }

        /// <summary>A loose lettuce leaf: broad and round, its edge frilled, its sides cupped up.</summary>
        static void SaladLeaf(SmoothMesh m, Dice d, Paint paint, Vector3 at, float yaw, float length, float rise, float tip, float half) =>
            Leaf(m, paint, Spine.Arc(at, yaw, length, rise, tip, d.Range(-15f, 15f)), half, Round, 0.35f, 0.14f, 3.5f, 18, 8);

        /// <summary>Sweetcorn kernels scattered over a heap <paramref name="peak"/> tall and <paramref name="radius"/> across.</summary>
        static void Kernels(SmoothMesh m, Dice d, int count, float radius, float peak)
        {
            for (int i = 0; i < count; i++)
            {
                float r = radius * Mathf.Sqrt(d.Value);
                float a = d.Range(0f, 360f);
                float y = Mound(r, radius, peak);
                float size = d.Range(0.0068f, 0.0082f);
                Blob(m, Paint.Kernel, Horizontal(a) * r + Vector3.up * (y + size * 0.4f), new Vector3(size, size * 0.75f, size * 0.9f),
                     Quaternion.Euler(d.Range(-25f, 25f), d.Range(0f, 360f), d.Range(-25f, 25f)), 9, 6);
            }
        }

        /// <summary>
        /// A wedge of tomato <paramref name="r"/> in radius, lying skin down on <paramref name="foot"/> with
        /// its core along <paramref name="yaw"/>, rolled <paramref name="roll"/> degrees onto one cut face:
        /// red skin, the wet flesh of its two cut faces, a pocket of seeds in each.
        /// </summary>
        static void Wedge(SmoothMesh m, Vector3 foot, float r, float yaw, float roll)
        {
            const float half = 0.6f;
            Quaternion q = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, roll);

            // th runs along the core from one end to the other, ph round it from straight down.
            Vector3 At(float rho, float th, float ph) =>
                foot + q * new Vector3(rho * Mathf.Sin(th) * Mathf.Sin(ph), r - rho * Mathf.Sin(th) * Mathf.Cos(ph), -rho * Mathf.Cos(th));

            float Th(float v) => Mathf.Lerp(0.03f, 0.97f, v) * Mathf.PI;

            m.Surface((int)Paint.TomatoRed, 10, 14, (u, v) => At(r, Th(v), Mathf.Lerp(-half, half, u)));

            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                m.Surface((int)Paint.TomatoFlesh, 5, 14, (u, v) => At(r * u, Th(v), s * half));
                m.Surface((int)Paint.TomatoSeeds, 10, 3, (u, v) =>
                {
                    float a = u * Mathf.PI * 2f;
                    float rho = r * (0.55f + 0.2f * v * Mathf.Cos(a));
                    float th = Mathf.PI * (0.5f + 0.24f * v * Mathf.Sin(a));
                    return At(rho, th, s * (half + 0.05f * r / Mathf.Max(1e-4f, rho * Mathf.Sin(th))));
                }, wrapU: true);
            }
        }

        /// <summary>
        /// A round slice of tomato <paramref name="thick"/> thick on <paramref name="foot"/>, turned by
        /// <paramref name="turn"/>: the skin round its edge and the red wall inside it, five pockets of
        /// paler jelly between the walls, the seeds in them.
        /// </summary>
        static void TomatoSlice(SmoothMesh m, Vector3 foot, float r, float thick, Quaternion turn)
        {
            Vector3 At(float radius, float a, float y) => foot + turn * new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);

            m.Surface((int)Paint.TomatoRed, 28, 2, (u, v) => At(r, u * Mathf.PI * 2f, v * thick), wrapU: true);
            m.Surface((int)Paint.TomatoFlesh, 28, 3, (u, v) => At(r * v, -u * Mathf.PI * 2f, thick), wrapU: true);
            m.Surface((int)Paint.TomatoFlesh, 28, 2, (u, v) => At(r * v, u * Mathf.PI * 2f, 0f), wrapU: true);
            m.Surface((int)Paint.TomatoRed, 28, 1, (u, v) => At(r * Mathf.Lerp(0.88f, 1f, v), -u * Mathf.PI * 2f, thick + 0.0003f), wrapU: true);

            // The pockets: wedges between the walls, narrow toward the core and round at the outer end.
            for (int k = 0; k < 5; k++)
            {
                float centre = k * Mathf.PI * 2f / 5f + 0.3f;
                m.Surface((int)Paint.TomatoSeeds, 8, 8, (u, v) =>
                {
                    float spread = 0.5f * Mathf.Pow(Arch(Mathf.PI * Mathf.Lerp(0.12f, 1f, v)), 0.5f) * Mathf.Lerp(0.45f, 1f, v);
                    return At(r * Mathf.Lerp(0.2f, 0.8f, v), centre + (u * 2f - 1f) * spread, thick + 0.0007f);
                });

                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 seed = At(r * 0.52f, centre + s * 0.17f, thick + 0.0012f);
                    Blob(m, Paint.BeanWhite, seed, new Vector3(r * 0.075f, r * 0.025f, r * 0.045f), turn * Quaternion.Euler(0f, -centre * Mathf.Rad2Deg + 90f, 0f), 8, 5);
                }
            }
        }

        /// <summary>
        /// A slice of grilled chicken breast <paramref name="length"/> long lying on its pale cut face,
        /// the browned outside of the breast along its back edge with the grill's bars across it.
        /// </summary>
        static void ChickenSlice(SmoothMesh m, Vector3 centre, Quaternion turn, float length)
        {
            const float Thick = 0.013f, Width = 0.035f;
            m.RoundedBox((int)Paint.ChickenCut, centre, new Vector3(length, Thick, Width), 0.006f, turn, 3);

            Vector3 crust = centre + turn * new Vector3(0f, 0.001f, Width * 0.5f - 0.0075f);
            m.RoundedBox((int)Paint.Chicken, crust, new Vector3(length + 0.002f, Thick + 0.002f, 0.017f), 0.0065f, turn, 3);

            for (int k = -2; k <= 2; k++)
            {
                Vector3 at = crust + turn * new Vector3(k * length * 0.19f, (Thick + 0.002f) * 0.5f - 0.0004f, 0.0005f);
                m.RoundedBox((int)Paint.GrillMark, at, new Vector3(0.004f, 0.0024f, 0.0095f), 0.001f, turn * Quaternion.Euler(0f, 28f, 0f), 2);
            }
        }

        // --- The ingredients -------------------------------------------------------------------------

        /// <summary>A loose head of lettuce: a pale heart, cupped leaves standing round it, frilled outer leaves opening out.</summary>
        static void LettuceHead(SmoothMesh m, Dice d)
        {
            Blob(m, Paint.LettuceHeart, new Vector3(0f, 0.075f, 0f), new Vector3(0.058f, 0.07f, 0.058f), Quaternion.identity, 18, 12);

            for (int k = 0; k < 6; k++)
            {
                float a = k * 60f + d.Range(-10f, 10f);
                Vector3 at = Horizontal(a) * 0.026f + Vector3.up * 0.015f;
                Leaf(m, Paint.LettuceHeart, Spine.Arc(at, a, d.Range(0.12f, 0.135f), 84f, 62f), 0.05f, Round, 0.6f, 0.07f, 4f, 18, 8);
            }

            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f + 22f + d.Range(-8f, 8f);
                Vector3 at = Horizontal(a) * 0.035f;
                Leaf(m, Paint.Lettuce, Spine.Arc(at, a, d.Range(0.16f, 0.18f), 62f, 16f, d.Range(-10f, 10f)), 0.07f, Round, 0.45f, 0.1f, 4.5f, 18, 8);
            }
        }

        /// <summary>Three carrots side by side, their feathery tops lying back behind them.</summary>
        static void Carrots(SmoothMesh m, Dice d)
        {
            for (int i = 0; i < 3; i++)
            {
                const float R = 0.024f;
                var crown = new Vector3(-0.07f + i * 0.045f, R * 0.95f, 0.06f - i * 0.02f);
                float yaw = 155f - i * 12f + d.Range(-4f, 4f);
                Spine root = Spine.Arc(crown, yaw, d.Range(0.2f, 0.23f), -4f, -6f, d.Range(-10f, 10f));

                m.Surface((int)Paint.Carrot, 16, 24, (u, v) =>
                {
                    Vector3 tangent = root.Tangent(v), side = root.Side(v);
                    Vector3 other = Vector3.Cross(side, tangent).normalized;
                    float a = u * Mathf.PI * 2f;
                    float shoulder = Mathf.Sqrt(Mathf.Clamp01(v / 0.05f));
                    float rings = 1f + 0.035f * Mathf.Sin(v * 70f + i);
                    float r = R * shoulder * Mathf.Lerp(1f, 0.08f, Mathf.Pow(v, 1.15f)) * rings;
                    return root.At(v) + (side * Mathf.Cos(a) + other * Mathf.Sin(a)) * r;
                }, wrapU: true);

                // The tops: stalks back from the crown, a frond at the end of each.
                for (int k = 0; k < 4; k++)
                {
                    float back = yaw + 180f + (k - 1.5f) * 16f + d.Range(-5f, 5f);
                    Spine stalk = Spine.Arc(crown, back, d.Range(0.08f, 0.11f), 30f, -4f);
                    Stem(m, Paint.CarrotTop, stalk, 0.0035f, 0.0022f, 6, 8);
                    Leaf(m, Paint.CarrotTop, Spine.Arc(stalk.End, back, d.Range(0.07f, 0.09f), 8f, -18f), 0.022f, Narrow, 0.2f, 0.3f, 6f);
                }
            }
        }

        /// <summary>A wedge cut from a round of cheese, its waxed rind round the back, and a cube cut from it in front.</summary>
        static void CheeseWedge(SmoothMesh m, Dice d)
        {
            const float R = 0.17f, H = 0.085f, Spread = 24f, Facing = -20f;
            Vector3 At(float rho, float a, float y) => Horizontal(Facing + a) * rho + Vector3.up * y;

            m.Surface((int)Paint.Cheese, 10, 8, (u, v) => At(v * R, Mathf.Lerp(Spread, -Spread, u), H));
            m.Surface((int)Paint.Cheese, 10, 8, (u, v) => At(v * R, Mathf.Lerp(-Spread, Spread, u), 0f));
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                m.Surface((int)Paint.Cheese, 8, 6, (u, v) => At(u * R, s * Spread, v * H));
            }

            m.Surface((int)Paint.CheeseRind, 14, 4, (u, v) => At(R, Mathf.Lerp(-Spread, Spread, u), v * H));

            m.RoundedBox((int)Paint.Cheese, new Vector3(0.07f, 0.022f, -0.07f), Vector3.one * 0.044f, 0.006f, Quaternion.Euler(0f, 28f, 0f), 2);
        }

        /// <summary>An opened tin of kidney beans, its lid curled back, a few beans spilled in front.</summary>
        static void BeanTin(SmoothMesh m, Dice d)
        {
            const float R = 0.065f, H = 0.13f;
            var profile = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(R - 0.004f, 0f), new Vector2(R, 0.005f), new Vector2(R + 0.002f, 0.012f), new Vector2(R, 0.018f),
                new Vector2(R, 0.05f), new Vector2(R, 0.08f), new Vector2(R, 0.112f), new Vector2(R + 0.002f, 0.12f), new Vector2(R + 0.001f, H),
                new Vector2(R - 0.004f, H), new Vector2(R - 0.005f, 0.118f),
            };
            int[] subs =
            {
                (int)Paint.Can, (int)Paint.Can, (int)Paint.Can, (int)Paint.Can, (int)Paint.CanLabel, (int)Paint.CanBand, (int)Paint.CanLabel,
                (int)Paint.Can, (int)Paint.Can, (int)Paint.Can, (int)Paint.Can,
            };
            m.Lathe(Vector3.zero, profile, subs, 36);

            Blob(m, Paint.Bean, new Vector3(0f, 0.114f, 0f), new Vector3(R - 0.006f, 0.012f, R - 0.006f), Quaternion.identity, 24, 8);
            for (int i = 0; i < 26; i++)
            {
                float r = (R - 0.016f) * Mathf.Sqrt(d.Value);
                float a = d.Range(0f, 360f);
                float y = 0.114f + 0.012f * Mathf.Sqrt(Mathf.Max(0f, 1f - (r / (R - 0.006f)) * (r / (R - 0.006f)))) + 0.003f;
                Blob(m, Paint.Bean, Horizontal(a) * r + Vector3.up * y, new Vector3(0.0125f, 0.007f, 0.008f),
                     Quaternion.Euler(d.Range(-15f, 15f), d.Range(0f, 360f), d.Range(-15f, 15f)), 10, 7);
            }

            // The lid, still hinged at the back of the rim, bent up and back.
            var hinge = new Vector3(0f, H, R - 0.004f);
            Quaternion lift = Quaternion.Euler(-115f, 0f, 0f);
            m.Surface((int)Paint.Can, 28, 3, (u, v) =>
            {
                float a = u * Mathf.PI * 2f;
                var flat = new Vector3(Mathf.Cos(a) * (R - 0.004f) * v, 0f, Mathf.Sin(a) * (R - 0.004f) * v - (R - 0.004f));
                return hinge + lift * flat;
            }, wrapU: true);

            foreach (Vector3 at in new[] { new Vector3(0.05f, 0.006f, -0.08f), new Vector3(0.075f, 0.006f, -0.06f), new Vector3(0.03f, 0.006f, -0.095f) })
                Blob(m, Paint.Bean, at, new Vector3(0.0125f, 0.007f, 0.008f), Quaternion.Euler(0f, d.Range(0f, 360f), 0f), 10, 7);
        }

        /// <summary>Two grilled drumsticks, one lying across the other, their bones out to the right.</summary>
        static void Drumsticks(SmoothMesh m, Dice d)
        {
            Drumstick(m, new Vector3(-0.05f, 0.032f, 0.03f), Direction(100f, 6f), 0.036f);
            Drumstick(m, new Vector3(-0.07f, 0.034f, -0.04f), Direction(70f, 14f), 0.038f);
        }

        /// <summary>A drumstick: the plump meat from its round end at <paramref name="fat"/> along <paramref name="axis"/>, narrowing to the bone and its knuckle.</summary>
        static void Drumstick(SmoothMesh m, Vector3 fat, Vector3 axis, float R)
        {
            const float Meat = 0.15f;
            Vector3 side = Vector3.Cross(axis, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(side, axis).normalized;

            m.Surface((int)Paint.Chicken, 20, 24, (u, v) =>
            {
                float th = v * Mathf.PI;
                float along = Meat * (1f - Mathf.Cos(th)) * 0.5f;
                // Round and full at the fat end, drawn in to the bone.
                float r = R * Mathf.Pow(Arch(th), 0.65f) * Mathf.Lerp(1.15f, 0.32f, Mathf.SmoothStep(0f, 1f, v));
                float a = u * Mathf.PI * 2f;
                return fat - axis * (R * 0.6f) + axis * along + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * r;
            }, wrapU: true);

            Vector3 boneFrom = fat + axis * (Meat - R * 0.9f);
            Vector3 boneTo = boneFrom + axis * 0.06f;
            m.Tube((int)Paint.Bone, boneFrom, boneTo, 0.0075f, 0.0075f, 10);
            foreach (float s in new[] { -1f, 1f })
                Blob(m, Paint.Bone, boneTo + side * (s * 0.0075f), Vector3.one * 0.0105f, Quaternion.identity, 12, 8);
        }
    }
}
