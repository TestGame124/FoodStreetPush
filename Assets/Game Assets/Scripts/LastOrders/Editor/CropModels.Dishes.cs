using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The dishes the kitchen cooks from the crops, modelled for their recipe icons (<see cref="DishIcons"/>)
    /// in the same smooth, chunky style and paints as the produce: a scored loaf on a board with two
    /// slices cut, squares of cornbread stacked on a plate with butter melting on top, and a bowl of
    /// tomato soup with a swirl of cream, basil and croutons, on its saucer.
    ///
    /// The crockery is the diner's - cream glaze with a teal band, the colours of its floor. Each dish
    /// stands on the origin, +Y up, its best side toward -Z, where the icon camera looks from; the icon
    /// renderer scales each to fill its frame.
    /// </summary>
    public static partial class CropModels
    {
        public enum Dish
        {
            Bread, CornBread, TomatoSoup, GardenSalad, SweetcornSalad, CarrotSlaw, TomatoCheeseSalad, BeanSalad, ChickenSalad, CustardTart, CornChowder,
            FriedEggs, HashBrowns, FrenchToast, GrilledCorn, ChickenSkewers, GrilledFish,
        }

        /// <summary>The dish, as its recipe icon shows it.</summary>
        public static SmoothMesh Plated(Dish dish)
        {
            var mesh = new SmoothMesh((int)Paint.Count);
            var dice = new Dice(92821 * ((int)dish + 5));

            switch (dish)
            {
                case Dish.Bread: Loaf(mesh, dice); break;
                case Dish.CornBread: Cornbread(mesh, dice); break;
                case Dish.TomatoSoup: SoupBowl(mesh, dice); break;
                case Dish.GardenSalad: GardenSalad(mesh, dice); break;
                case Dish.SweetcornSalad: SweetcornSalad(mesh, dice); break;
                case Dish.CarrotSlaw: CarrotSlaw(mesh, dice); break;
                case Dish.TomatoCheeseSalad: TomatoCheeseSalad(mesh, dice); break;
                case Dish.BeanSalad: BeanSalad(mesh, dice); break;
                case Dish.ChickenSalad: ChickenSalad(mesh, dice); break;
                case Dish.CustardTart: CustardTart(mesh, dice); break;
                case Dish.CornChowder: CornChowder(mesh, dice); break;
                case Dish.FriedEggs: FriedEggs(mesh, dice); break;
                case Dish.HashBrowns: HashBrowns(mesh, dice); break;
                case Dish.FrenchToast: FrenchToast(mesh, dice); break;
                case Dish.GrilledCorn: GrilledCorn(mesh, dice); break;
                case Dish.ChickenSkewers: ChickenSkewers(mesh, dice); break;
                case Dish.GrilledFish: GrilledFish(mesh, dice); break;
            }

            return mesh;
        }

        // --- Bread -----------------------------------------------------------------------------------

        const float LoafLength = 0.22f, LoafHalfWidth = 0.08f, LoafCentre = 0.048f;

        /// <summary>
        /// The loaf's cross-section at <paramref name="theta"/>, from -90 (the middle of its base) round
        /// its side to 90 (the top), on the side <paramref name="side"/> (-1 front, 1 back): a flat foot,
        /// sides that bulge, a domed top. (z, y).
        /// </summary>
        static Vector2 LoafSection(float theta, float side, float scale = 1f)
        {
            float t = theta * Mathf.Deg2Rad;
            float c = Mathf.Max(0f, Mathf.Cos(t)), s = Mathf.Sin(t);
            float z = side * LoafHalfWidth * (s < 0f ? Mathf.Pow(c, 0.45f) : Mathf.Pow(c, 0.8f)) * (1f + 0.06f * c);
            float y = LoafCentre + (s < 0f ? -LoafCentre * Mathf.Pow(-s, 0.3f) : 0.07f * Mathf.Pow(s, 0.85f));
            return new Vector2(z * scale, LoafCentre + (y - LoafCentre) * scale);
        }

        /// <summary>
        /// A scored bloomer on a cutting board, its right end cut off flat to show the crumb, and two
        /// slices lying crumb up against the cut, where the light from the top left catches them.
        /// </summary>
        static void Loaf(SmoothMesh m, Dice d)
        {
            const float board = 0.016f;
            Rounded(m, Paint.BoardWood, new Vector3(0.06f, board * 0.5f, -0.03f), new Vector3(0.2f, board * 0.5f, 0.125f), 4f, 0.5f);

            var lift = new Vector3(-0.04f, board, 0.02f);
            float half = LoafLength * 0.5f;

            // How far a section is scaled down toward the rounded end at -X.
            float Taper(float x)
            {
                const float dome = 0.07f;
                float k = Mathf.Clamp01((x + half) / dome);
                return Mathf.Sqrt(Mathf.Max(0f, 1f - (1f - k) * (1f - k)));
            }

            // The scoring: three slashes slanting across the top.
            float Score(float x, float z)
            {
                float a = 35f * Mathf.Deg2Rad;
                float along = x * Mathf.Cos(a) + z * Mathf.Sin(a);
                float cut = 0f;
                foreach (float at in new[] { -0.07f, 0f, 0.07f })
                    cut += Mathf.Exp(-Mathf.Pow(along - at, 2f) / (2f * 0.009f * 0.009f));
                return cut;
            }

            Vector3 Crust(float x, float theta, float side)
            {
                Vector2 q = LoafSection(theta, side, Taper(x));
                float top = Mathf.SmoothStep(0f, 1f, (theta - 25f) / 40f);
                float y = q.y - 0.015f * Score(x, q.x) * top * Taper(x);
                return lift + new Vector3(x, y, q.x);
            }

            // Two halves, each from the foot up to the top, so the crust's colour runs pale to golden.
            m.Surface((int)Paint.BreadCrust, 48, 20, (u, v) => Crust(-half + u * LoafLength, Mathf.Lerp(-90f, 90f, v), -1f));
            m.Surface((int)Paint.BreadCrust, 48, 20, (u, v) => Crust(half - u * LoafLength, Mathf.Lerp(-90f, 90f, v), 1f));

            // The dough under the crust, a little inside it: it shows only where the scoring cuts the
            // crust deeper than that, so the slashes come out pale as they bake.
            Vector3 Dough(float x, float theta, float side)
            {
                Vector2 q = LoafSection(theta, side, Taper(x));
                return lift + new Vector3(x * 0.98f, LoafCentre + (q.y - LoafCentre) * 0.9f, q.x * 0.9f);
            }

            m.Surface((int)Paint.BreadScore, 40, 10, (u, v) => Dough(-half + u * LoafLength, Mathf.Lerp(20f, 90f, v), -1f));
            m.Surface((int)Paint.BreadScore, 40, 10, (u, v) => Dough(half - u * LoafLength, Mathf.Lerp(20f, 90f, v), 1f));

            // The cut end: the crumb, paling toward the middle.
            CrumbFace(m, lift + new Vector3(half + 0.0005f, 0f, 0f), Quaternion.identity, 1f, true);

            // Two slices laid down crumb up, their tops resting against the cut end, the second a little
            // in front of the first and over it.
            Slice(m, lift + new Vector3(half + 0.115f, 0.002f, 0.005f), Quaternion.Euler(0f, 4f, 66f), 0.95f);
            Slice(m, lift + new Vector3(half + 0.17f, 0.004f, -0.11f), Quaternion.Euler(0f, -30f, 78f), 0.9f);
        }

        /// <summary>The section's outline at <paramref name="phi"/>, 0 to 2pi round it, from its foot up the front, over the top and down the back.</summary>
        static Vector2 SectionOutline(float phi, float scale)
        {
            float k = phi / Mathf.PI;
            return k <= 1f ? LoafSection(-90f + k * 180f, -1f, scale) : LoafSection(90f - (k - 1f) * 180f, 1f, scale);
        }

        /// <summary>
        /// A flat face of crumb the loaf's section in shape at <paramref name="at"/>, turned by
        /// <paramref name="turn"/>, facing +X or, with <paramref name="outward"/> false, -X.
        /// </summary>
        static void CrumbFace(SmoothMesh m, Vector3 at, Quaternion turn, float scale, bool outward)
        {
            var centre = new Vector3(0f, LoafCentre, 0f);
            m.Surface((int)Paint.BreadCrumb, 40, 6, (u, v) =>
            {
                float phi = (outward ? u : 1f - u) * Mathf.PI * 2f;
                Vector2 q = SectionOutline(phi, scale);
                Vector3 edge = new Vector3(0f, q.y, q.x);
                return at + turn * Vector3.Lerp(centre, edge, v);
            }, wrapU: true);
        }

        /// <summary>A slice of the loaf, standing on its edge at <paramref name="foot"/>: crumb both faces, a rim of crust.</summary>
        static void Slice(SmoothMesh m, Vector3 foot, Quaternion turn, float scale)
        {
            const float thickness = 0.022f;
            CrumbFace(m, foot + turn * new Vector3(thickness * 0.5f, 0f, 0f), turn, scale, true);
            CrumbFace(m, foot + turn * new Vector3(-thickness * 0.5f, 0f, 0f), turn, scale, false);

            m.Surface((int)Paint.BreadCrust, 40, 2, (u, v) =>
            {
                Vector2 q = SectionOutline(u * Mathf.PI * 2f, scale);
                return foot + turn * new Vector3(Mathf.Lerp(thickness * 0.5f, -thickness * 0.5f, v), q.y, q.x);
            }, wrapU: true);
        }

        // --- Cornbread -------------------------------------------------------------------------------

        /// <summary>
        /// Three squares of cornbread on a plate, two side by side and one across them, a pat of butter
        /// melting on the top one, kernels of corn scattered over them, and husk leaves tucked behind.
        /// </summary>
        static void Cornbread(SmoothMesh m, Dice d)
        {
            float well = Plate(m, 0.165f);
            var half = new Vector3(0.05f, 0.028f, 0.05f);

            Square(m, new Vector3(-0.052f, well + half.y, 0.012f), half, 8f);
            Square(m, new Vector3(0.05f, well + half.y, -0.02f), half, -12f);
            var top = new Vector3(0f, well + half.y * 3f + 0.004f, -0.004f);
            Square(m, top, half, 32f);

            Rounded(m, Paint.Butter, top + new Vector3(0.004f, half.y + 0.011f, 0f), new Vector3(0.026f, 0.008f, 0.021f), 50f, 0.6f);

            // Husk leaves lying on the plate behind the stack.
            for (int k = 0; k < 3; k++)
            {
                Vector3 at = new Vector3(-0.07f + k * 0.035f, well + 0.003f, 0.07f);
                Leaf(m, Paint.Husk, Spine.Arc(at, 50f + k * 28f + d.Range(-6f, 6f), d.Range(0.11f, 0.14f), 6f, -4f), 0.02f, Narrow, 0.35f);
            }

            // Kernels on the tops and a few on the plate.
            var spots = new List<Vector3>
            {
                new Vector3(-0.07f, well + half.y * 2f + 0.002f, 0.03f), new Vector3(-0.03f, well + half.y * 2f + 0.002f, 0.035f),
                new Vector3(0.07f, well + half.y * 2f + 0.002f, -0.045f), new Vector3(0.075f, well + half.y * 2f + 0.002f, 0.0f),
                new Vector3(-0.025f, top.y + half.y, -0.03f), new Vector3(0.03f, top.y + half.y, 0.025f),
                new Vector3(-0.11f, well + 0.004f, -0.06f), new Vector3(0.1f, well + 0.004f, -0.08f), new Vector3(0.0f, well + 0.004f, -0.1f),
            };
            foreach (Vector3 s in spots)
            {
                float r = d.Range(0.0065f, 0.0085f);
                Blob(m, Paint.Kernel, s + Vector3.up * r * 0.6f, new Vector3(r, r * 0.75f, r * 0.9f),
                     Quaternion.Euler(0f, d.Range(0f, 360f), d.Range(-20f, 20f)), 10, 7);
            }
        }

        // --- Tomato soup ------------------------------------------------------------------------------

        /// <summary>A bowl of tomato soup on its saucer: a swirl of cream, two basil leaves and a few croutons.</summary>
        static void SoupBowl(SmoothMesh m, Dice d)
        {
            float well = Plate(m, 0.17f);

            // The bowl: a foot ring, a round belly, the teal band under the rim, the rim, and down inside.
            var profile = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.045f, 0f), new Vector2(0.05f, 0.004f), new Vector2(0.052f, 0.008f),
                new Vector2(0.09f, 0.028f), new Vector2(0.116f, 0.056f), new Vector2(0.126f, 0.08f), new Vector2(0.13f, 0.088f),
                new Vector2(0.126f, 0.092f), new Vector2(0.12f, 0.088f), new Vector2(0.112f, 0.068f), new Vector2(0.09f, 0.038f),
                new Vector2(0.05f, 0.018f), new Vector2(0f, 0.014f),
            };
            var subs = new List<int>();
            for (int i = 0; i < profile.Count - 1; i++) subs.Add(i == 5 ? (int)Paint.GlazeBand : (int)Paint.Glaze);
            m.Lathe(new Vector3(0f, well, 0f), profile, subs, 40);

            // The soup, darker at the bowl's side, from the side in to the middle.
            float surface = well + 0.072f;
            m.Lathe(Vector3.zero, new List<Vector2>
            {
                new Vector2(0.1155f, surface), new Vector2(0.07f, surface + 0.0015f), new Vector2(0f, surface + 0.002f),
            }, new[] { (int)Paint.Soup, (int)Paint.Soup }, 40);

            // A swirl of cream, a ribbon spiralling out from near the middle.
            m.Surface((int)Paint.SoupCream, 4, 70, (u, v) =>
            {
                float a = v * 2.2f * Mathf.PI * 2f + 0.4f;
                float r = 0.012f + 0.07f * v;
                float w = 0.012f * (0.35f + 0.65f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, v * 1.15f)));
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float y = surface + 0.0025f + 0.0035f * Mathf.Sin(Mathf.PI * u);
                return radial * (r + (u - 0.5f) * w) + Vector3.up * y;
            });

            // Basil on top, a little off the middle.
            for (int k = 0; k < 2; k++)
            {
                Vector3 at = new Vector3(-0.012f, surface + 0.006f, 0.006f);
                Leaf(m, Paint.Basil, Spine.Arc(at, 120f + k * 110f + d.Range(-10f, 10f), 0.042f, 14f, -10f), 0.017f, Ovate, 0.35f);
            }

            // Croutons floating near the rim.
            foreach (float a in new[] { 200f, 250f, 320f })
            {
                float rad = a * Mathf.Deg2Rad;
                var at = new Vector3(Mathf.Cos(rad) * 0.078f, surface + 0.004f, Mathf.Sin(rad) * 0.078f);
                Rounded(m, Paint.Crouton, at, new Vector3(0.0095f, 0.008f, 0.0095f), d.Range(0f, 90f), 0.3f);
            }
        }

        // --- Shared ---------------------------------------------------------------------------------

        /// <summary>
        /// A plate in the diner's glaze, <paramref name="radius"/> across, standing on the origin: a foot,
        /// a wide well, a rim with a teal band. Returns the height of the well's floor.
        /// </summary>
        static float Plate(SmoothMesh m, float radius)
        {
            float r = radius;
            var profile = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r * 0.42f, 0f), new Vector2(r * 0.45f, 0.004f), new Vector2(r * 0.48f, 0.006f),
                new Vector2(r * 0.9f, 0.019f), new Vector2(r * 0.98f, 0.026f), new Vector2(r, 0.03f), new Vector2(r * 0.97f, 0.033f),
                new Vector2(r * 0.86f, 0.026f), new Vector2(r * 0.55f, 0.012f), new Vector2(0f, 0.011f),
            };
            var subs = new List<int>();
            for (int i = 0; i < profile.Count - 1; i++) subs.Add(i >= 4 && i <= 7 ? (int)Paint.GlazeBand : (int)Paint.Glaze);
            m.Lathe(Vector3.zero, profile, subs, 44);
            return 0.011f;
        }

        /// <summary>A square of cornbread: the yellow crumb, a golden crust baked over its top and down its edges.</summary>
        static void Square(SmoothMesh m, Vector3 centre, Vector3 half, float yaw)
        {
            Rounded(m, Paint.CornbreadCrumb, centre, half, yaw, 0.35f);
            Rounded(m, Paint.CornbreadCrust, centre + Vector3.up * (half.y - 0.004f),
                    new Vector3(half.x + 0.0015f, 0.0065f, half.z + 0.0015f), yaw, 0.9f);
        }

        /// <summary>
        /// A rounded block - a square of cornbread, a pat of butter, a crouton, a board -
        /// <paramref name="half"/> its half sizes, turned <paramref name="yaw"/> degrees, its edges rounded
        /// by <paramref name="roundness"/> of its thinnest half. A rounded box rather than a superquadric:
        /// a superquadric's sides meet its top along creases its normals cannot follow, and they show
        /// as dark stars.
        /// </summary>
        static void Rounded(SmoothMesh m, Paint paint, Vector3 centre, Vector3 half, float yaw, float roundness)
        {
            float radius = Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * Mathf.Clamp01(roundness);
            m.RoundedBox((int)paint, centre, half * 2f, radius, Quaternion.Euler(0f, yaw, 0f), 3);
        }
    }
}
