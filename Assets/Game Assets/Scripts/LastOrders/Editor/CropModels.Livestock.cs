using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The farmyard's produce and what the kitchen makes with it, modelled for their icons
    /// (<see cref="LivestockContent"/>) in the style and paints of the crops and the other dishes:
    /// three eggs in a nest of straw, a bottle of milk with a glass poured beside it, a custard tart
    /// on a plate with a slice cut, and a bowl of corn chowder.
    ///
    /// Each stands on the origin, +Y up, its best side toward -Z, where the icon camera looks from.
    /// </summary>
    public static partial class CropModels
    {
        // --- Eggs -------------------------------------------------------------------------------------

        /// <summary>An egg lying on its side at <paramref name="centre"/>, pointing <paramref name="yaw"/>, its narrow end a little up.</summary>
        static void LyingEgg(SmoothMesh m, Paint paint, Vector3 centre, float yaw, float tilt, float r)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f - tilt, 0f, 0f);
            float h = r * 2.6f;
            m.Surface((int)paint, 22, 16, (u, v) =>
            {
                float t = v;
                float radius = r * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), 0.85f) * (1.1f - 0.22f * t);
                float a = u * Mathf.PI * 2f;
                return centre + turn * new Vector3(Mathf.Cos(a) * radius, (t - 0.45f) * h, Mathf.Sin(a) * radius);
            }, wrapU: true);
        }

        /// <summary>Three eggs, two brown and a cream one, in a round nest of straw.</summary>
        static void EggNest(SmoothMesh m, Dice d)
        {
            // The nest: straws curled round in rings, built up into a rim.
            for (int ring = 0; ring < 5; ring++)
            {
                float radius = 0.11f + ring * 0.011f;
                float y = 0.01f + ring * 0.012f;
                for (int k = 0; k < 10; k++)
                {
                    float a = k / 10f * 360f + ring * 17f + d.Range(-8f, 8f);
                    Vector3 at = Horizontal(a) * radius + Vector3.up * y;
                    Paint straw = (k + ring) % 3 == 0 ? Paint.WheatGold : Paint.WheatStraw;
                    Stem(m, straw, Spine.Arc(at, a + 90f, d.Range(0.11f, 0.14f), d.Range(-6f, 8f), d.Range(-8f, 4f), 52f), 0.0085f, 0.006f, 6, 10);
                }
            }

            Blob(m, Paint.WheatStraw, new Vector3(0f, 0.012f, 0f), new Vector3(0.12f, 0.012f, 0.12f), Quaternion.identity, 24, 6);

            LyingEgg(m, Paint.EggShell, new Vector3(-0.05f, 0.06f, 0.024f), 30f, 12f, 0.044f);
            LyingEgg(m, Paint.EggShellCream, new Vector3(0.052f, 0.06f, 0.034f), -50f, 8f, 0.043f);
            LyingEgg(m, Paint.EggShell, new Vector3(0.004f, 0.058f, -0.05f), 95f, 15f, 0.044f);
        }

        // --- Milk -------------------------------------------------------------------------------------

        /// <summary>A glass bottle of milk with a teal foil cap, and a tumbler of it poured beside it.</summary>
        static void MilkBottle(SmoothMesh m, Dice d)
        {
            // The bottle: milk to the shoulder, the glass of its neck above, the cap.
            var bottle = new Vector3(0.025f, 0f, 0.02f);
            var milk = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.046f, 0f), new Vector2(0.052f, 0.006f), new Vector2(0.054f, 0.02f),
                new Vector2(0.054f, 0.12f), new Vector2(0.05f, 0.145f), new Vector2(0.036f, 0.17f), new Vector2(0.026f, 0.185f),
                new Vector2(0f, 0.186f),
            };
            m.Lathe(bottle, milk, Subs(milk, Paint.Milk), 32);

            var glass = new List<Vector2>
            {
                new Vector2(0.0275f, 0.183f), new Vector2(0.024f, 0.205f), new Vector2(0.025f, 0.222f), new Vector2(0.029f, 0.228f),
                new Vector2(0.029f, 0.236f),
            };
            m.Lathe(bottle, glass, Subs(glass, Paint.MilkGlass), 32);

            var cap = new List<Vector2>
            {
                new Vector2(0.0305f, 0.226f), new Vector2(0.031f, 0.243f), new Vector2(0.026f, 0.248f), new Vector2(0f, 0.249f),
            };
            m.Lathe(bottle, cap, Subs(cap, Paint.GlazeBand), 32);

            // The tumbler beside it, milk to near the top, its glass rim above.
            var tumbler = new Vector3(-0.07f, 0f, -0.045f);
            var poured = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.034f, 0f), new Vector2(0.038f, 0.004f), new Vector2(0.042f, 0.085f), new Vector2(0f, 0.086f),
            };
            m.Lathe(tumbler, poured, Subs(poured, Paint.Milk), 28);
            var rim = new List<Vector2> { new Vector2(0.0425f, 0.084f), new Vector2(0.044f, 0.1f), new Vector2(0.04f, 0.1f), new Vector2(0.0395f, 0.088f) };
            m.Lathe(tumbler, rim, Subs(rim, Paint.MilkGlass), 28);
        }

        static List<int> Subs(List<Vector2> profile, Paint paint)
        {
            var subs = new List<int>();
            for (int i = 0; i < profile.Count - 1; i++) subs.Add((int)paint);
            return subs;
        }

        // --- Custard tart -----------------------------------------------------------------------------

        /// <summary>A custard tart on a plate: a fluted case of short pastry, the custard baked brown in the middle, nutmeg over it.</summary>
        static void CustardTart(SmoothMesh m, Dice d)
        {
            float well = Plate(m, 0.17f);
            const float R = 0.115f, H = 0.042f;
            var foot = new Vector3(0f, well, 0f);

            // The case: fluted round its side, a rolled rim.
            m.Surface((int)Paint.Pastry, 72, 8, (u, v) =>
            {
                float a = u * Mathf.PI * 2f;
                float flute = 1f + 0.035f * Mathf.Cos(a * 24f);
                float y = v * H;
                float r = R * flute * Mathf.Lerp(0.92f, 1f, v);
                return foot + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            }, wrapU: true);
            m.Surface((int)Paint.Pastry, 72, 6, (u, v) =>
            {
                float a = u * Mathf.PI * 2f;
                float b = v * Mathf.PI;
                float flute = 1f + 0.035f * Mathf.Cos(a * 24f);
                float r = R * flute - 0.006f + Mathf.Cos(b) * 0.008f;
                return foot + new Vector3(Mathf.Cos(a) * r, H + Mathf.Sin(b) * 0.007f, Mathf.Sin(a) * r);
            }, wrapU: true);

            // The custard, domed a little, from its edge into a browned middle.
            float top = well + H - 0.004f;
            m.Lathe(Vector3.zero, new List<Vector2>
            {
                new Vector2(R - 0.01f, top), new Vector2(R * 0.6f, top + 0.005f), new Vector2(R * 0.35f, top + 0.007f),
            }, new[] { (int)Paint.Custard, (int)Paint.Custard }, 48);
            m.Lathe(Vector3.zero, new List<Vector2>
            {
                new Vector2(R * 0.36f, top + 0.0075f), new Vector2(R * 0.15f, top + 0.0085f), new Vector2(0f, top + 0.009f),
            }, new[] { (int)Paint.CustardTop, (int)Paint.CustardTop }, 48);

            // Nutmeg grated over it.
            for (int i = 0; i < 18; i++)
            {
                float r = R * 0.8f * Mathf.Sqrt(d.Value);
                float a = d.Range(0f, 360f);
                Vector3 at = Horizontal(a) * r + Vector3.up * (top + 0.008f);
                Blob(m, Paint.GrillMark, at, new Vector3(0.0035f, 0.0015f, 0.0035f), Quaternion.identity, 6, 4);
            }
        }

        // --- Corn chowder -----------------------------------------------------------------------------

        /// <summary>A bowl of corn chowder on its saucer: creamy, kernels of corn and cubes of potato in it, herbs on top.</summary>
        static void CornChowder(SmoothMesh m, Dice d)
        {
            float well = Plate(m, 0.17f);
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

            float surface = well + 0.074f;
            m.Lathe(Vector3.zero, new List<Vector2>
            {
                new Vector2(0.1155f, surface), new Vector2(0.07f, surface + 0.002f), new Vector2(0f, surface + 0.003f),
            }, new[] { (int)Paint.Chowder, (int)Paint.Chowder }, 40);

            // Kernels and potato breaking the surface.
            for (int i = 0; i < 22; i++)
            {
                float r = 0.095f * Mathf.Sqrt(d.Value);
                float a = d.Range(0f, 360f);
                float k = d.Range(0.0065f, 0.0085f);
                Blob(m, Paint.Kernel, Horizontal(a) * r + Vector3.up * (surface + 0.002f), new Vector3(k, k * 0.75f, k * 0.9f),
                     Quaternion.Euler(0f, d.Range(0f, 360f), d.Range(-20f, 20f)), 10, 7);
            }

            foreach (float a in new[] { 40f, 160f, 280f, 215f })
            {
                float rad = a * Mathf.Deg2Rad;
                var at = new Vector3(Mathf.Cos(rad) * 0.06f, surface + 0.003f, Mathf.Sin(rad) * 0.06f);
                Rounded(m, Paint.BeanWhite, at, new Vector3(0.011f, 0.008f, 0.011f), d.Range(0f, 90f), 0.35f);
            }

            // A few herb leaves in the middle.
            for (int k = 0; k < 3; k++)
            {
                Vector3 at = new Vector3(0.005f, surface + 0.006f, -0.005f);
                Leaf(m, Paint.Basil, Spine.Arc(at, 60f + k * 120f + d.Range(-10f, 10f), 0.03f, 12f, -8f), 0.011f, Narrow, 0.3f);
            }
        }
    }
}
