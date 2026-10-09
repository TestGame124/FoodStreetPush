using System;
using System.Collections.Generic;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using S = LastOrders.EditorTools.SoupKitchenModel;
using V = FoodStreet.Visitors.EditorTools.VehicleKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The pieces the coop and the cow shelter share, in the same hand as the shelter hut and the
    /// storehouse: corrugated tin weathered from its fixings, a trodden yard of dirt and straw,
    /// timber posts and rails, a run of wire mesh, galvanised fittings.
    /// </summary>
    internal static class FarmBuildingKit
    {
        internal static readonly Color Dirt = B.Hex("8C7A62");
        internal static readonly Color DirtDark = B.Hex("6F604C");
        internal static readonly Color Straw = B.Hex("A79562");
        internal static readonly Color StrawLight = B.Hex("B5A372");
        internal static readonly Color Galvanised = B.Hex("8C9296");
        internal static readonly Color GalvanisedTop = B.Hex("7F858A");
        internal static readonly Color GalvanisedDark = B.Hex("6B7176");
        internal static readonly Color Wire = B.Hex("6E7377");
        internal static readonly Color Water = B.Hex("4E646C");
        internal static readonly Color DoorPaint = B.Hex("47766E");
        internal static readonly Color BarnRed = B.Hex("8A4A40");
        internal static readonly Color Cream = B.Hex("AFA898");

        // --- Tin ---------------------------------------------------------------------------------------

        /// <summary>
        /// Tin as a painter shows it: a shade lighter toward the top, rust run down in streaks from
        /// the fixings, heavier toward the eave. <paramref name="v"/> is 0 at the top to 1 at the eave.
        /// </summary>
        internal static Color Weathered(Color tin, float u, float v, int key, float rust)
        {
            float phase = B.Jitter(key) * 10f;
            float streak = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u * 7.3f + phase)), 10f) +
                           0.6f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u * 13.1f + phase * 2f)), 14f);
            float amount = Mathf.Clamp01(streak) * Mathf.Clamp01((v - 0.3f) / 0.7f) * rust;
            Color c = tin * Mathf.Lerp(1.05f, 0.95f, v) * (1f + 0.03f * Mathf.Sin(u * 2.1f + phase));
            return Color.Lerp(c, B.Rust, amount * 0.55f);
        }

        /// <summary>
        /// One slope of corrugated tin: its top edge from <paramref name="topStart"/> to
        /// <paramref name="topEnd"/>, running <paramref name="length"/> down <paramref name="down"/>.
        /// Several sheets painted on one surface with a shadow line at each lap, so no crest of the
        /// sheet below comes up through the one on top.
        /// </summary>
        internal static void TinSlope(B.Model m, Vector3 topStart, Vector3 topEnd, Vector3 down, float length, Color[] tins, int sheets,
                                      int key, float rust)
        {
            down = down.normalized;
            Vector3 along = topEnd - topStart;
            float span = along.magnitude;
            Vector3 across = along / span;
            Vector3 corner = topStart;

            // Corrugated's top faces cross(down, across): turned round if that points down.
            if (Vector3.Cross(down, across).y < 0f)
            {
                across = -across;
                corner = topEnd;
            }

            float sheet = span / Mathf.Max(1, sheets);
            m.Corrugated(corner, across, down, span, length, 0.15f, 0.035f, 0.03f, (u, v) =>
            {
                int k = Mathf.Min(sheets - 1, Mathf.FloorToInt(u / sheet));
                Color c = Weathered(tins[(k + key) % tins.Length], u, v / length, k + key * 10, rust);
                float seam = Mathf.Abs(u - Mathf.Round(u / sheet) * sheet);
                return u > 0.1f && u < span - 0.1f && seam < 0.035f ? c * 0.78f : c;
            });
        }

        // --- Ground ------------------------------------------------------------------------------------

        /// <summary>
        /// Trodden ground over <paramref name="area"/>: dirt scuffed by feet, a little lumpy, straw
        /// strewn where <paramref name="strawAt"/>(x, z) says (0 none, 1 thick), and darker where
        /// <paramref name="shadeAt"/> says it is sheltered. Its edge runs down into the grass.
        /// </summary>
        internal static void Yard(B.Model m, Rect area, int key, Func<float, float, float> strawAt, Func<float, float, float> shadeAt = null)
        {
            Vector3 seed = V.Seed(key);
            int steps = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(area.width, area.height) * 9f), 16, 48);

            m.Heightfield((p, n) =>
            {
                float mottle = V.Noise(p * 2.3f + seed);
                Color c = Color.Lerp(DirtDark, Dirt, mottle);
                float straw = Mathf.Clamp01(strawAt(p.x, p.z)) * V.Band(V.Noise(p * 5.5f + seed * 2f), 0.3f, 0.6f);
                c = Color.Lerp(c, Color.Lerp(Straw, StrawLight, V.Noise(p * 13f)), straw);
                if (shadeAt != null) c *= Mathf.Lerp(1f, 0.78f, Mathf.Clamp01(shadeAt(p.x, p.z)));
                return c * (0.96f + 0.06f * V.Noise(p * 17f + seed));
            }, area, steps, (x, z) =>
            {
                float edge = Mathf.Min(Mathf.Min(x - area.xMin, area.xMax - x), Mathf.Min(z - area.yMin, area.yMax - z));
                // Up off the grass even at its edge, so the two never fight over the same depth.
                float lift = 0.012f + 0.012f * V.Noise(new Vector3(x * 3.1f, 0f, z * 3.1f) + seed);
                return 0.012f + lift * LivestockKit.Smooth(edge / 0.25f);
            });
        }

        /// <summary>A tuft of straw lying on the ground: a few flattened wisps.</summary>
        internal static void StrawTuft(B.Model m, Vector3 foot, float size, int key)
        {
            for (int i = 0; i < 3; i++)
            {
                float yaw = B.Jitter(key * 5 + i) * 180f;
                Vector3 at = foot + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0.04f * i, 0.012f, 0f);
                Color c = Color.Lerp(Straw, StrawLight, 0.5f + 0.5f * B.Jitter(key * 3 + i));
                S.Ball(m, (p, n) => n.y < -0.3f ? c * 0.7f : c, at, new Vector3(size * 0.5f, 0.014f, size * 0.18f), Quaternion.Euler(0f, yaw, 0f), 8, 5);
            }
        }

        // --- Timber ------------------------------------------------------------------------------------

        /// <summary>A square fence post, a little out of true, its top cut on a slant to shed the rain.</summary>
        internal static void Post(B.Model m, Vector3 foot, float height, float size, int key)
        {
            Color c = B.Plank(key * 7 + 3) * 0.92f;
            Quaternion lean = Quaternion.Euler(1.2f * B.Jitter(key), 0f, 1.2f * B.Jitter(key + 40));
            m.Box(c, foot + lean * Vector3.up * (height * 0.5f), new Vector3(size, height, size), size * 0.18f, lean, 2);
            m.Box(c * 0.82f, foot + lean * Vector3.up * (height + 0.004f), new Vector3(size * 0.9f, 0.02f, size * 0.9f), 0.006f,
                  lean * Quaternion.Euler(8f, 0f, 0f), 1);
        }

        /// <summary>A rail from <paramref name="a"/> to <paramref name="b"/>: a sawn board on edge, one shade of timber.</summary>
        internal static void Rail(B.Model m, Vector3 a, Vector3 b, float height, float thickness, int key)
        {
            Vector3 d = b - a;
            float length = d.magnitude;
            if (length < 0.02f) return;
            Quaternion turn = Quaternion.LookRotation(d / length, Vector3.up) * Quaternion.Euler(0f, 0f, 1.2f * B.Jitter(key));
            Color c = B.Plank(key * 13 + 1) * (1f + 0.04f * B.Jitter(key * 3));
            m.Box(c, (a + b) * 0.5f, new Vector3(thickness, height, length), thickness * 0.3f, turn, 1);
        }

        /// <summary>
        /// Wire mesh stretched between two posts from <paramref name="a"/> to <paramref name="b"/>
        /// (their feet), <paramref name="bottom"/> to <paramref name="top"/> high, a wire every
        /// <paramref name="spacing"/> each way: the chicken run.
        /// </summary>
        internal static void WireMesh(B.Model m, Vector3 a, Vector3 b, float bottom, float top, float spacing)
        {
            Vector3 d = b - a;
            float length = d.magnitude;
            if (length < 0.05f) return;
            Vector3 along = d / length;
            const float r = 0.0055f;

            int uprights = Mathf.Max(1, Mathf.RoundToInt(length / spacing));
            for (int i = 1; i < uprights; i++)
            {
                Vector3 x = a + along * (length * i / uprights);
                m.Tube(Wire, x + Vector3.up * bottom, x + Vector3.up * top, r, 4);
            }

            int rows = Mathf.Max(1, Mathf.RoundToInt((top - bottom) / spacing));
            for (int j = 0; j <= rows; j++)
            {
                float y = Mathf.Lerp(bottom, top, j / (float)rows);
                // A little slack in each strand.
                Vector3 sag = Vector3.down * 0.008f * (j == 0 || j == rows ? 0f : 1f);
                m.Tube(Wire, a + Vector3.up * y, a + d * 0.5f + Vector3.up * y + sag, r, 4);
                m.Tube(Wire, a + d * 0.5f + Vector3.up * y + sag, b + Vector3.up * y, r, 4);
            }
        }

        // --- Galvanised fittings ----------------------------------------------------------------------

        /// <summary>A hanging-style feeder stood on the ground: a tray round the foot, the hopper, a coolie-hat lid.</summary>
        internal static void Feeder(B.Model m, Vector3 foot, float scale)
        {
            Func<Vector3, Vector3, Color> tin = (p, n) =>
                (n.y > 0.6f ? GalvanisedTop : Galvanised) * (0.93f + 0.08f * V.Noise(p * 11f)) * (n.y < -0.4f ? 0.65f : 1f);

            float s = scale;
            m.Lathe(tin, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.17f * s, 0f), new Vector2(0.18f * s, 0.05f * s), new Vector2(0.18f * s, 0.05f * s),
                new Vector2(0.16f * s, 0.05f * s), new Vector2(0.16f * s, 0.03f * s), new Vector2(0.11f * s, 0.03f * s),
                new Vector2(0.11f * s, 0.03f * s), new Vector2(0.105f * s, 0.32f * s), new Vector2(0.105f * s, 0.32f * s),
                new Vector2(0.15f * s, 0.32f * s), new Vector2(0.03f * s, 0.4f * s), new Vector2(0f, 0.41f * s),
            }, 16);

            // Grain showing in the tray.
            S.Disc(m, (p, n) => StrawLight * 0.95f, foot + Vector3.up * 0.032f * s, Vector3.up, 0.155f * s, 16);
            V.Sweep(m, GalvanisedDark, new[]
            {
                foot + new Vector3(-0.06f * s, 0.4f * s, 0f), foot + new Vector3(0f, 0.48f * s, 0f), foot + new Vector3(0.06f * s, 0.4f * s, 0f),
            }, 0.008f, 6, 3);
        }

        /// <summary>A galvanised drinking fount: a dome on a dished tray of water.</summary>
        internal static void Fount(B.Model m, Vector3 foot, float scale)
        {
            float s = scale;
            m.Lathe((p, n) => (n.y > 0.6f ? GalvanisedTop : Galvanised) * (0.93f + 0.08f * V.Noise(p * 11f)) * (n.y < -0.4f ? 0.65f : 1f),
                    foot, new List<Vector2>
                    {
                        new Vector2(0f, 0f), new Vector2(0.15f * s, 0f), new Vector2(0.155f * s, 0.045f * s), new Vector2(0.155f * s, 0.045f * s),
                        new Vector2(0.14f * s, 0.045f * s), new Vector2(0.14f * s, 0.03f * s), new Vector2(0.1f * s, 0.03f * s),
                        new Vector2(0.1f * s, 0.03f * s), new Vector2(0.1f * s, 0.2f * s), new Vector2(0.07f * s, 0.27f * s),
                        new Vector2(0.02f * s, 0.3f * s), new Vector2(0f, 0.3f * s),
                    }, 16);
            S.Disc(m, (p, n) => Water, foot + Vector3.up * 0.036f * s, Vector3.up, 0.138f * s, 16);
            m.Tube(GalvanisedDark, foot + Vector3.up * 0.3f * s, foot + Vector3.up * 0.34f * s, 0.012f * s, 6);
        }
    }
}
