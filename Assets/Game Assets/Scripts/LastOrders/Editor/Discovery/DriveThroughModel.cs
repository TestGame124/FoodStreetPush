using System;
using System.Collections.Generic;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using D = LastOrders.EditorTools.DiscoveryKit;
using K = FoodStreet.Visitors.EditorTools.VehicleKit;
using S = LastOrders.EditorTools.SoupKitchenModel;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The abandoned drive-through booth by the road: a little two-tone kiosk on a concrete plinth -
    /// teal below a steel trim line, cream panels above with battens between them, chunky pillars
    /// at the corners - under a thick flat roof with a mustard fascia. Its front is the serving
    /// window: a steel counter on brackets, a fixed pane and a sliding hatch, a striped canopy curving
    /// out over it with scallops along its lip, a lamp either side. A porthole in the side the cars
    /// come from, a door at the back. Put back to work, a giant burger stands on the roof.
    ///
    /// Origin on the ground in the middle; the window faces -Z, onto the lane.
    /// </summary>
    public static class DriveThroughModel
    {
        // --- Measurements -------------------------------------------------------------------------

        public const float Width = 3.2f, Depth = 2.4f;
        const float Plinth = 0.16f, WallTop = 2.35f, Wall = 0.12f, Trim = 0.9f;
        const float WinX0 = -1.25f, WinX1 = 0.25f, WinY0 = 1.0f, WinY1 = 1.95f;

        static float HX => Width * 0.5f;
        static float HZ => Depth * 0.5f;
        static float Front => -HZ;

        /// <summary>Where the sliding hatch sits shut, over the left half of the window, and how far it slides open.</summary>
        public static readonly Vector3 HatchClosed = new Vector3((WinX0 + (WinX0 + WinX1) * 0.5f) * 0.5f, (WinY0 + WinY1) * 0.5f, -HZ - 0.035f);
        public static float HatchSlide => (WinX1 - WinX0) * 0.5f;

        /// <summary>Where a bag is passed out of the window from.</summary>
        public static readonly Vector3 ServePoint = new Vector3((WinX0 + WinX1) * 0.5f, WinY0 + 0.3f, -HZ - 0.3f);

        /// <summary>Where the buttons float, over the roof.</summary>
        public static readonly Vector3 Marker = new Vector3(0f, 4.3f, 0f);

        public static readonly Vector3 BurgerAt = new Vector3(-0.25f, WallTop + 0.18f, 0.05f);

        static readonly Color Panel = D.Cream;
        static readonly Color Wainscot = D.Teal;
        static readonly Color Roof = B.Hex("7E8083");
        static readonly Color Interior = B.Hex("2A2725");

        // --- The booth ------------------------------------------------------------------------------

        public static B.Model Booth()
        {
            var m = new B.Model { Shade = Shade };

            PlinthAndStep(m);
            Walls(m);
            Pillars(m);
            Inside(m);
            Window(m);
            Canopy(m);
            RoofSlab(m);
            Porthole(m);
            BackDoor(m);
            SideUnits(m);

            m.Lantern(new Vector3(WinX0 - 0.2f, 2.02f, Front - 0.02f), Vector3.back);
            m.Lantern(new Vector3(WinX1 + 0.2f, 2.02f, Front - 0.02f), Vector3.back);

            // Bollards guarding the corners from the lane, yellow and black.
            foreach (float x in new[] { -HX - 0.35f, HX + 0.35f })
                Bollard(m, new Vector3(x, 0f, Front - 0.45f));

            Bin(m, new Vector3(WinX1 + 0.7f, 0f, Front - 0.5f));
            return m;
        }

        /// <summary>Darker at the foot of the walls and up under the roof's overhang.</summary>
        static float Shade(Vector3 p, Vector3 n)
        {
            float k = Mathf.Lerp(0.76f, 1f, S.Smooth(Mathf.Clamp01(p.y / 0.5f)));
            bool underRoof = Mathf.Abs(n.y) < 0.5f && Mathf.Abs(p.x) < HX + 0.1f && Mathf.Abs(p.z) < HZ + 0.1f && p.y < WallTop;
            if (underRoof) k *= Mathf.Lerp(1f, 0.84f, S.Smooth(Mathf.Clamp01((p.y - (WallTop - 0.4f)) / 0.4f)));
            if (n.y < -0.5f) k *= 0.82f;
            return k;
        }

        static void PlinthAndStep(B.Model m)
        {
            m.Box(D.Concrete, new Vector3(0f, Plinth * 0.5f, 0f), new Vector3(Width + 0.34f, Plinth, Depth + 0.34f), 0.04f, 2);
            m.Box(D.ConcreteDark, new Vector3(0f, 0.03f, 0f), new Vector3(Width + 0.42f, 0.06f, Depth + 0.42f), 0.025f, 2);

            // The back step, up to the door.
            m.Box(D.Concrete, new Vector3(1.0f, Plinth * 0.4f, HZ + 0.4f), new Vector3(0.95f, Plinth * 0.8f, 0.42f), 0.035f, 2);
        }

        /// <summary>
        /// The walls, each two-tone and panelled: a slab for every run between openings, teal to the
        /// trim line and cream above, battens over the joints, and the steel trim all the way round.
        /// </summary>
        static void Walls(B.Model m)
        {
            // Front: round the window.
            WallRun(m, Vector3.back, -HX, WinX0, Plinth, WallTop, 1);
            WallRun(m, Vector3.back, WinX1, HX, Plinth, WallTop, 2);
            WallRun(m, Vector3.back, WinX0, WinX1, Plinth, WinY0, 3);
            WallRun(m, Vector3.back, WinX0, WinX1, WinY1, WallTop, 4);

            // Back, with the door, and the two sides.
            const float DoorX0 = 0.6f, DoorX1 = 1.4f, DoorTop = 2.05f;
            WallRun(m, Vector3.forward, -HX, DoorX0, Plinth, WallTop, 5);
            WallRun(m, Vector3.forward, DoorX1, HX, Plinth, WallTop, 6);
            WallRun(m, Vector3.forward, DoorX0, DoorX1, DoorTop, WallTop, 7);
            WallRun(m, Vector3.right, -HZ, HZ, Plinth, WallTop, 8);
            WallRun(m, Vector3.left, -HZ, HZ, Plinth, WallTop, 9);

            // The trim line, proud all round, broken only by the door.
            float y = Trim;
            m.Box(D.Steel, new Vector3(0f, y, Front - 0.02f), new Vector3(Width + 0.04f, 0.05f, 0.05f), 0.018f, 2);
            m.Box(D.Steel, new Vector3(-HX - 0.02f, y, 0f), new Vector3(0.05f, 0.05f, Depth + 0.04f), 0.018f, 2);
            m.Box(D.Steel, new Vector3(HX + 0.02f, y, 0f), new Vector3(0.05f, 0.05f, Depth + 0.04f), 0.018f, 2);
            m.Box(D.Steel, new Vector3((-HX + DoorX0) * 0.5f, y, HZ + 0.02f), new Vector3(DoorX0 + HX, 0.05f, 0.05f), 0.018f, 2);
            m.Box(D.Steel, new Vector3((DoorX1 + HX) * 0.5f, y, HZ + 0.02f), new Vector3(HX - DoorX1, 0.05f, 0.05f), 0.018f, 2);
        }

        /// <summary>
        /// One run of wall on the face toward <paramref name="outward"/>, from <paramref name="from"/> to
        /// <paramref name="to"/> along it and <paramref name="bottom"/> to <paramref name="top"/> high:
        /// teal below the trim line, cream above, a batten every panel's width.
        /// </summary>
        static void WallRun(B.Model m, Vector3 outward, float from, float to, float bottom, float top, int key)
        {
            if (to - from < 0.02f || top - bottom < 0.02f) return;

            Vector3 along = Vector3.Cross(Vector3.up, outward).normalized * -1f;
            float face = Mathf.Abs(outward.x) > 0.5f ? HX : HZ;
            Vector3 plane = outward * (face - Wall * 0.5f);
            Quaternion turn = Quaternion.LookRotation(outward, Vector3.up);

            void Slab(float y0, float y1, Color colour)
            {
                if (y1 - y0 < 0.01f) return;
                Vector3 c = plane + along * ((from + to) * 0.5f) + Vector3.up * ((y0 + y1) * 0.5f);
                m.Box(colour * (1f + 0.03f * B.Jitter(key)), c, new Vector3(to - from, y1 - y0, Wall), 0.02f, turn, 2);
            }

            Slab(bottom, Mathf.Min(top, Trim), Wainscot);
            Slab(Mathf.Max(bottom, Trim), top, Panel);

            // Battens over the panel joints: a little proud, a little darker, so the wall reads as panels.
            const float panel = 0.62f;
            for (float x = Mathf.Ceil((from + 0.15f) / panel) * panel; x < to - 0.15f; x += panel)
            {
                float y0 = Mathf.Max(bottom, Trim + 0.04f), y1 = top - 0.02f;
                if (y1 - y0 < 0.1f) continue;
                Vector3 c = outward * (face + 0.012f) + along * x + Vector3.up * ((y0 + y1) * 0.5f);
                m.Box(Panel * 0.9f, c, new Vector3(0.05f, y1 - y0, 0.03f), 0.01f, turn);
            }
        }

        static void Pillars(B.Model m)
        {
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var foot = new Vector3(sx * (HX + 0.01f), Plinth, sz * (HZ + 0.01f));
                m.Tube((p, n) => D.TealDark * (1f + 0.05f * Mathf.Sin(Mathf.Atan2(p.z - foot.z, p.x - foot.x) * 6f)), foot,
                       foot + Vector3.up * (WallTop - Plinth), 0.085f, 0.085f, 14);
                m.Lathe((p, n) => D.Steel, foot, new List<Vector2>
                {
                    new Vector2(0f, 0f), new Vector2(0.11f, 0f), new Vector2(0.11f, 0.06f), new Vector2(0.09f, 0.09f), new Vector2(0f, 0.09f),
                }, 14);
            }
        }

        /// <summary>The dark inside, seen through the window: a counter, a till and a coffee urn on it, a hood over them.</summary>
        static void Inside(B.Model m)
        {
            m.Box(Interior, new Vector3(0f, (Plinth + WallTop) * 0.5f, 0.05f), new Vector3(Width - Wall * 2f - 0.02f, WallTop - Plinth - 0.02f, Depth - Wall * 2f - 0.02f),
                  0.02f);

            float z = Front + Wall + 0.25f;
            m.Box(D.Steel * 0.8f, new Vector3((WinX0 + WinX1) * 0.5f, WinY0 - 0.03f, z), new Vector3(WinX1 - WinX0 + 0.3f, 0.05f, 0.5f), 0.015f, 2);
            m.Box(B.Hex("3E4246"), new Vector3(WinX0 + 0.35f, WinY0 + 0.1f, z + 0.05f), new Vector3(0.32f, 0.2f, 0.26f), 0.03f, Quaternion.Euler(-12f, 8f, 0f), 2);
            m.Box(B.Hex("2E3134"), new Vector3(WinX0 + 0.35f, WinY0 + 0.16f, z - 0.04f), new Vector3(0.22f, 0.06f, 0.08f), 0.01f, 2);

            Vector3 urn = new Vector3(WinX1 - 0.3f, WinY0 - 0.01f, z + 0.08f);
            m.Lathe((p, n) => D.Steel, urn, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.11f, 0f), new Vector2(0.12f, 0.04f), new Vector2(0.12f, 0.3f), new Vector2(0.1f, 0.34f),
                new Vector2(0.04f, 0.36f), new Vector2(0.04f, 0.4f), new Vector2(0f, 0.41f),
            }, 14);
            m.Tube(B.Iron, urn + new Vector3(0f, 0.08f, -0.11f), urn + new Vector3(0f, 0.08f, -0.17f), 0.015f, 6);

            m.Box(D.Steel * 0.7f, new Vector3((WinX0 + WinX1) * 0.5f, WinY1 - 0.08f, z + 0.1f), new Vector3(WinX1 - WinX0, 0.12f, 0.36f), 0.02f, 2);
        }

        /// <summary>The serving window: steel jambs proud of the wall, the fixed pane, and the counter out over the lane.</summary>
        static void Window(B.Model m)
        {
            float z = Front - 0.02f;
            float mid = (WinX0 + WinX1) * 0.5f;

            m.Box(D.Steel, new Vector3(mid, WinY1 + 0.04f, z), new Vector3(WinX1 - WinX0 + 0.16f, 0.08f, 0.08f), 0.02f, 2);
            m.Box(D.Steel, new Vector3(WinX0 - 0.04f, (WinY0 + WinY1) * 0.5f, z), new Vector3(0.08f, WinY1 - WinY0, 0.08f), 0.02f, 2);
            m.Box(D.Steel, new Vector3(WinX1 + 0.04f, (WinY0 + WinY1) * 0.5f, z), new Vector3(0.08f, WinY1 - WinY0, 0.08f), 0.02f, 2);

            // The fixed half of the glazing, on the right, set back so the hatch slides over it.
            Pane(m, new Vector3((mid + WinX1) * 0.5f, (WinY0 + WinY1) * 0.5f, Front + 0.01f), (WinX1 - mid) * 0.5f, (WinY1 - WinY0) * 0.5f);

            // The counter: a steel shelf out over the lane with a rolled front edge, on two brackets.
            float depth = 0.36f;
            var counter = new Vector3(mid, WinY0 - 0.02f, Front - depth * 0.5f);
            m.Box(D.Steel, counter, new Vector3(WinX1 - WinX0 + 0.34f, 0.05f, depth), 0.018f, 2);
            K.Sweep(m, (p, n) => D.Steel * 1.08f, new[] { new Vector3(WinX0 - 0.17f, WinY0 - 0.01f, Front - depth), new Vector3(WinX1 + 0.17f, WinY0 - 0.01f, Front - depth) },
                    0.026f, 8, 3);
            foreach (float x in new[] { WinX0 + 0.12f, WinX1 - 0.12f })
                m.Box(D.SteelDark, new Vector3(x, WinY0 - 0.16f, Front - 0.14f), new Vector3(0.04f, 0.05f, 0.3f), 0.012f, Quaternion.Euler(-38f, 0f, 0f), 2);
        }

        static void Pane(B.Model m, Vector3 centre, float halfX, float halfY)
        {
            m.Surface(K.Glazing(centre, Vector3.right * halfX * 2f, Vector3.up * halfY * 2f), 8, 8,
                      (u, v) => centre + new Vector3((u - 0.5f) * halfX * 2f, (v - 0.5f) * halfY * 2f, -0.004f));
        }

        /// <summary>
        /// The sliding hatch, about its own middle: a steel frame round a pane, a bar handle on stand-offs.
        /// It is drawn a hair in front of the fixed pane and slides across it.
        /// </summary>
        public static B.Model Hatch()
        {
            var m = new B.Model();
            float hx = (WinX1 - WinX0) * 0.25f, hy = (WinY1 - WinY0) * 0.5f;

            Pane(m, Vector3.zero, hx - 0.03f, hy - 0.03f);
            m.Box(D.Steel, new Vector3(0f, hy - 0.02f, 0f), new Vector3(hx * 2f, 0.04f, 0.035f), 0.012f, 2);
            m.Box(D.Steel, new Vector3(0f, -hy + 0.02f, 0f), new Vector3(hx * 2f, 0.04f, 0.035f), 0.012f, 2);
            m.Box(D.Steel, new Vector3(-hx + 0.02f, 0f, 0f), new Vector3(0.04f, hy * 2f, 0.035f), 0.012f, 2);
            m.Box(D.Steel, new Vector3(hx - 0.02f, 0f, 0f), new Vector3(0.04f, hy * 2f, 0.035f), 0.012f, 2);

            m.Tube(K.Chrome, new Vector3(hx - 0.1f, -0.12f, -0.06f), new Vector3(hx - 0.1f, 0.12f, -0.06f), 0.014f, 8);
            foreach (float y in new[] { -0.1f, 0.1f })
                m.Tube(K.Chrome, new Vector3(hx - 0.1f, y, -0.02f), new Vector3(hx - 0.1f, y, -0.06f), 0.01f, 6);
            return m;
        }

        /// <summary>
        /// The canopy over the window: a Dutch awning curving out and down from the wall, a stripe at a
        /// time - red and cream, each with a top, an underside and a front edge - cheeks closing its
        /// ends, and a scalloped valance hanging from its lip.
        /// </summary>
        static void Canopy(B.Model m)
        {
            const float X0 = WinX0 - 0.25f, X1 = WinX1 + 0.25f, Out = 0.62f, Rise = 0.32f, Thick = 0.045f;
            const float Top = WallTop - 0.05f;
            const int Stripes = 10;
            float stripe = (X1 - X0) / Stripes;

            Vector3 Curve(float x, float t, float lift)
            {
                float a = t * Mathf.PI * 0.5f;
                var p = new Vector3(x, Top - Rise + Rise * Mathf.Cos(a), Front - Out * Mathf.Sin(a));
                var n = new Vector3(0f, Mathf.Cos(a) * Out, -Mathf.Sin(a) * Rise).normalized;
                return p + n * lift;
            }

            for (int i = 0; i < Stripes; i++)
            {
                float x0 = X0 + i * stripe, x1 = x0 + stripe + 0.002f;
                Color c = (i % 2 == 0 ? D.Red : D.CreamLight) * (1f + 0.03f * B.Jitter(i + 40));

                // Top: u runs out along the curve and v across, so it faces up; the underside the other way round.
                m.Surface((p, n) => c * (n.y < -0.3f ? 0.72f : 1f), 14, 2, (u, v) => Curve(Mathf.Lerp(x0, x1, v), u, Thick));
                m.Surface((p, n) => c * 0.72f, 2, 14, (u, v) => Curve(Mathf.Lerp(x0, x1, u), v, 0f));
                m.Surface((p, n) => c, 2, 1, (u, t) => Vector3.Lerp(Curve(Mathf.Lerp(x0, x1, u), 1f, 0f), Curve(Mathf.Lerp(x0, x1, u), 1f, Thick), t));

                // The scallop under this stripe's end of the lip.
                Vector3 lip = Curve((x0 + x1) * 0.5f, 1f, Thick * 0.5f);
                m.Box(c, lip + Vector3.down * 0.07f, new Vector3(stripe * 0.86f, 0.15f, 0.035f), 0.06f, 3);
            }

            // Cheeks closing both ends, from the wall out to the curve.
            for (int s = 0; s < 2; s++)
            {
                float x = s == 0 ? X0 : X1;
                bool flip = s == 1;
                m.Surface((p, n) => D.Red * 0.9f, 12, 1, (t, f) =>
                {
                    Vector3 rim = Curve(x, flip ? 1f - t : t, Thick * 0.5f);
                    var wall = new Vector3(x, rim.y, Front);
                    return Vector3.Lerp(wall, rim, f);
                });
            }

            // A steel rail along the wall where it is fixed on.
            m.Tube(D.SteelDark, new Vector3(X0 - 0.04f, Top + 0.02f, Front - 0.03f), new Vector3(X1 + 0.04f, Top + 0.02f, Front - 0.03f), 0.025f, 8);
        }

        /// <summary>The roof: a thick slab over everything with an overhang, a mustard fascia round it, a vent and an extractor on top.</summary>
        static void RoofSlab(B.Model m)
        {
            const float Over = 0.26f, Thick = 0.18f;
            float y = WallTop + Thick * 0.5f;
            m.Box(Roof, new Vector3(0f, y, 0f), new Vector3(Width + Over * 2f, Thick, Depth + Over * 2f), 0.04f, 2);

            // The fascia: a band proud of the slab's edge all round, with a lip along its top.
            float fy = y - 0.01f;
            foreach (int s in new[] { -1, 1 })
            {
                m.Box(D.Mustard, new Vector3(0f, fy, s * (HZ + Over + 0.02f)), new Vector3(Width + Over * 2f + 0.08f, 0.24f, 0.05f), 0.02f, 2);
                m.Box(D.Mustard, new Vector3(s * (HX + Over + 0.02f), fy, 0f), new Vector3(0.05f, 0.24f, Depth + Over * 2f + 0.08f), 0.02f, 2);
                m.Box(D.Mustard * 0.85f, new Vector3(0f, fy + 0.13f, s * (HZ + Over + 0.03f)), new Vector3(Width + Over * 2f + 0.1f, 0.03f, 0.07f), 0.012f);
                m.Box(D.Mustard * 0.85f, new Vector3(s * (HX + Over + 0.03f), fy + 0.13f, 0f), new Vector3(0.07f, 0.03f, Depth + Over * 2f + 0.1f), 0.012f);
            }

            // A vent stack with a cap, and a boxy extractor with a round fan in its lid.
            float top = WallTop + Thick;
            var vent = new Vector3(1.05f, top, 0.65f);
            m.Tube(D.Steel, vent, vent + Vector3.up * 0.42f, 0.07f, 12);
            m.Lathe((p, n) => D.SteelDark, vent + Vector3.up * 0.42f, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.13f, 0f), new Vector2(0.13f, 0.02f), new Vector2(0f, 0.1f),
            }, 12);

            var fan = new Vector3(0.9f, top + 0.14f, -0.35f);
            m.Box(D.Steel, fan, new Vector3(0.62f, 0.28f, 0.52f), 0.04f, 2);
            m.Lathe((p, n) => D.SteelDark, fan + Vector3.up * 0.14f, new List<Vector2>
            {
                new Vector2(0f, 0.02f), new Vector2(0.17f, 0.02f), new Vector2(0.19f, 0f), new Vector2(0.21f, 0.03f), new Vector2(0.19f, 0.05f),
            }, 16);
            for (int k = 0; k < 4; k++)
                m.Box(B.Iron, fan + Vector3.up * 0.165f, new Vector3(0.32f, 0.01f, 0.04f), 0.004f, Quaternion.Euler(0f, k * 45f, 0f));
        }

        /// <summary>A round porthole in the side the cars come from: a chunky steel ring, a domed lens, a bolt every so often.</summary>
        static void Porthole(B.Model m)
        {
            var centre = new Vector3(HX + 0.01f, 1.55f, 0f);
            Matrix4x4 was = m.Transform;
            m.Transform = was * Matrix4x4.TRS(centre, Quaternion.Euler(0f, 0f, -90f), Vector3.one);
            try
            {
                m.Lathe((p, n) => D.Steel, Vector3.zero, new List<Vector2>
                {
                    new Vector2(0.24f, 0f), new Vector2(0.34f, 0f), new Vector2(0.36f, 0.03f), new Vector2(0.33f, 0.07f), new Vector2(0.25f, 0.07f),
                    new Vector2(0.23f, 0.03f), new Vector2(0.24f, 0f),
                }, 24);
                m.Lathe((p, n) => Color.Lerp(B.Glass, B.GlassSky, Mathf.Clamp01(n.x * 0.5f + 0.5f)), Vector3.zero, new List<Vector2>
                {
                    new Vector2(0f, 0.065f), new Vector2(0.12f, 0.06f), new Vector2(0.24f, 0.035f), new Vector2(0.245f, 0.02f),
                }, 24);
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    var bolt = new Vector3(Mathf.Cos(a) * 0.3f, 0.07f, Mathf.Sin(a) * 0.3f);
                    m.Tube(B.Iron, bolt, bolt + Vector3.up * 0.015f, 0.016f, 6);
                }
            }
            finally
            {
                m.Transform = was;
            }
        }

        /// <summary>The staff door at the back: inset panels, a push bar, a little steel hood over it.</summary>
        static void BackDoor(B.Model m)
        {
            const float X0 = 0.6f, X1 = 1.4f, Top = 2.05f;
            float z = HZ + 0.01f;
            float mid = (X0 + X1) * 0.5f;

            m.Box(D.SteelDark, new Vector3(mid, (Plinth + Top) * 0.5f, z - 0.02f), new Vector3(X1 - X0, Top - Plinth, 0.06f), 0.015f, 2);
            m.Box(D.TealDark, new Vector3(mid, (Plinth + Top) * 0.5f, z + 0.02f), new Vector3(X1 - X0 - 0.08f, Top - Plinth - 0.06f, 0.05f), 0.02f, 2);
            foreach (float y in new[] { 0.65f, 1.55f })
                m.Box(D.TealDark * 0.85f, new Vector3(mid, y, z + 0.05f), new Vector3(X1 - X0 - 0.26f, 0.6f, 0.02f), 0.012f);
            m.Tube(K.Chrome, new Vector3(X0 + 0.12f, 1.05f, z + 0.1f), new Vector3(X1 - 0.12f, 1.05f, z + 0.1f), 0.016f, 8);

            m.Box(D.Steel, new Vector3(mid, Top + 0.12f, z + 0.18f), new Vector3(X1 - X0 + 0.3f, 0.04f, 0.38f), 0.015f, Quaternion.Euler(-10f, 0f, 0f), 2);
        }

        /// <summary>An air conditioner on the far side with a slatted grille and a fan, and a gas bottle in a cage.</summary>
        static void SideUnits(B.Model m)
        {
            var ac = new Vector3(-HX - 0.26f, 1.35f, 0.35f);
            m.Box(D.CreamLight, ac, new Vector3(0.4f, 0.48f, 0.72f), 0.04f, 2);
            for (int k = -3; k <= 3; k++)
                m.Box(D.SteelDark, ac + new Vector3(-0.205f, 0.06f, k * 0.085f), new Vector3(0.02f, 0.28f, 0.035f), 0.006f);
            m.Box(B.Iron, ac + new Vector3(0.1f, -0.3f, 0f), new Vector3(0.06f, 0.12f, 0.5f), 0.015f, Quaternion.Euler(0f, 0f, 30f));

            var gas = new Vector3(-HX - 0.3f, Plinth * 0.5f, -0.55f);
            m.Lathe((p, n) => n.y > 0.8f ? D.Red * 1.08f : D.Red, gas, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.15f, 0f), new Vector2(0.17f, 0.04f), new Vector2(0.17f, 0.62f), new Vector2(0.13f, 0.72f),
                new Vector2(0.05f, 0.75f), new Vector2(0.05f, 0.82f), new Vector2(0f, 0.82f),
            }, 16);
            m.Tube(K.Chrome, gas + Vector3.up * 0.82f, gas + Vector3.up * 0.88f, 0.035f, 8);
        }

        static void Bollard(B.Model m, Vector3 foot)
        {
            Color yellow = B.Hex("B8962F"), black = B.Hex("2E2E30");
            m.Lathe((p, n) =>
            {
                float y = p.y - foot.y;
                return n.y > 0.7f ? yellow * 1.05f : ((int)(y / 0.16f) % 2 == 0 ? yellow : black);
            }, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.11f, 0f), new Vector2(0.1f, 0.02f), new Vector2(0.1f, 0.16f), new Vector2(0.1f, 0.16f),
                new Vector2(0.1f, 0.32f), new Vector2(0.1f, 0.32f), new Vector2(0.1f, 0.48f), new Vector2(0.1f, 0.48f), new Vector2(0.1f, 0.68f),
                new Vector2(0.09f, 0.74f), new Vector2(0f, 0.78f),
            }, 14);
        }

        static void Bin(B.Model m, Vector3 foot)
        {
            m.Lathe((p, n) => n.y > 0.8f ? D.TealDark * 1.1f : D.TealDark * (1f + 0.04f * Mathf.Sin(Mathf.Atan2(p.z - foot.z, p.x - foot.x) * 12f)),
                    foot, new List<Vector2>
                    {
                        new Vector2(0f, 0f), new Vector2(0.2f, 0f), new Vector2(0.23f, 0.7f), new Vector2(0.25f, 0.72f), new Vector2(0.25f, 0.76f),
                        new Vector2(0.18f, 0.78f), new Vector2(0.17f, 0.86f), new Vector2(0f, 0.9f),
                    }, 16);
        }

        // --- The burger --------------------------------------------------------------------------------

        /// <summary>
        /// A giant burger on a pole: a bun with sesame seeds, a frilly lettuce leaf, tomato, cheese
        /// drooping over the edge of a thick patty, the bottom bun, a sign plate under it all.
        /// </summary>
        public static void Burger(B.Model m, Vector3 foot, bool withPole, float lean = 0f, float scale = 1f)
        {
            Color bun = B.Hex("B98A4E"), bunTop = B.Hex("A0713B"), patty = B.Hex("5B3A2A"), lettuce = B.Hex("6E9637"),
                  tomato = B.Hex("A23B2E"), cheese = B.Hex("D3A23A"), seed = B.Hex("D9CDA8");

            if (withPole)
            {
                m.Tube(D.SteelDark, foot, foot + Vector3.up * 0.55f, 0.06f, 12);
                m.Box(D.SteelDark, foot + Vector3.up * 0.03f, new Vector3(0.36f, 0.06f, 0.36f), 0.02f, 2);
            }

            Vector3 plate = foot + Vector3.up * (withPole ? 0.55f : 0f);
            Matrix4x4 was = m.Transform;
            m.Transform = was * Matrix4x4.TRS(plate, Quaternion.Euler(lean, 0f, lean * 0.6f), Vector3.one * scale);
            try
            {
                if (withPole)
                    m.Lathe((p, n) => D.Red, Vector3.zero, new List<Vector2>
                    {
                        new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0.52f, 0.03f), new Vector2(0.5f, 0.06f), new Vector2(0f, 0.06f),
                    }, 24);

                float y = withPole ? 0.06f : 0f;

                // Bottom bun.
                m.Lathe((p, n) => n.y < -0.5f ? bun * 0.8f : bun, new Vector3(0f, y, 0f), new List<Vector2>
                {
                    new Vector2(0f, 0f), new Vector2(0.38f, 0f), new Vector2(0.43f, 0.04f), new Vector2(0.44f, 0.1f), new Vector2(0.42f, 0.14f),
                    new Vector2(0f, 0.14f),
                }, 24);
                y += 0.14f;

                // The patty, knobbly round its edge.
                m.Lathe((p, n) => patty * (0.85f + 0.15f * Mathf.Sin(Mathf.Atan2(p.z, p.x) * 11f + p.y * 30f)), new Vector3(0f, y, 0f), new List<Vector2>
                {
                    new Vector2(0f, 0f), new Vector2(0.44f, 0f), new Vector2(0.47f, 0.04f), new Vector2(0.46f, 0.09f), new Vector2(0.43f, 0.12f),
                    new Vector2(0f, 0.12f),
                }, 24);
                y += 0.12f;

                // A slice of cheese, its corners drooping over the patty's edge.
                var slice = new SmoothMesh(1);
                slice.RoundedBox(0, new Vector3(0f, y + 0.012f, 0f), new Vector3(0.8f, 0.03f, 0.8f), 0.012f, Quaternion.Euler(0f, 45f, 0f), 2);
                Func<Vector3, Vector3> droop = p =>
                {
                    float r = new Vector2(p.x, p.z).magnitude;
                    return p + Vector3.down * Mathf.Max(0f, r - 0.42f) * 0.9f;
                };
                m.Warp = droop;
                m.Add(slice, cheese);
                m.Warp = null;
                y += 0.03f;

                // Tomato, two slices side by side.
                for (int s = -1; s <= 1; s += 2)
                    m.Lathe((p, n) => n.y > 0.5f ? tomato * 1.15f : tomato, new Vector3(s * 0.17f, y, 0.02f * s), new List<Vector2>
                    {
                        new Vector2(0f, 0f), new Vector2(0.2f, 0f), new Vector2(0.21f, 0.025f), new Vector2(0.2f, 0.05f), new Vector2(0f, 0.05f),
                    }, 16);
                y += 0.04f;

                // Lettuce: a ring frilled up and down round its edge - its top facing up, its underside down.
                m.Surface((p, n) => lettuce * (n.y < -0.2f ? 0.75f : 1f), 3, 48, (u, v) =>
                {
                    float a = v * Mathf.PI * 2f;
                    float r = Mathf.Lerp(0.1f, 0.5f, u);
                    float frill = Mathf.Sin(a * 9f) * 0.04f * u * u;
                    return new Vector3(Mathf.Cos(a) * r, y + 0.01f + frill, Mathf.Sin(a) * r);
                });
                m.Surface((p, n) => lettuce * 0.7f, 48, 3, (u, v) =>
                {
                    float a = u * Mathf.PI * 2f;
                    float r = Mathf.Lerp(0.1f, 0.5f, v);
                    float frill = Mathf.Sin(a * 9f) * 0.04f * v * v;
                    return new Vector3(Mathf.Cos(a) * r, y - 0.005f + frill, Mathf.Sin(a) * r);
                }, true);
                y += 0.03f;

                // The top bun: a fat dome, toasted darker on top, seeds scattered over it.
                var crown = new Vector3(0f, y, 0f);
                var profile = new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.42f, 0f), new Vector2(0.45f, 0.04f) };
                for (int i = 1; i <= 8; i++)
                {
                    float a = i / 8f * Mathf.PI * 0.5f;
                    profile.Add(new Vector2(0.45f * Mathf.Cos(a), 0.04f + 0.3f * Mathf.Sin(a)));
                }
                m.Lathe((p, n) => Color.Lerp(bun, bunTop, Mathf.Clamp01((p.y - crown.y) / 0.3f)), crown, profile, 24);

                for (int k = 0; k < 14; k++)
                {
                    float a = k * 2.399f, d = 0.08f + 0.28f * ((k * 37 % 100) / 100f);
                    float h = 0.04f + 0.3f * Mathf.Sqrt(Mathf.Max(0f, 1f - (d / 0.45f) * (d / 0.45f)));
                    var at = crown + new Vector3(Mathf.Cos(a) * d, h + 0.005f, Mathf.Sin(a) * d);
                    S.Ball(m, (p, n) => seed, at, new Vector3(0.025f, 0.012f, 0.014f), Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f), 6, 4);
                }
            }
            finally
            {
                m.Transform = was;
            }
        }

        // --- How it was found ---------------------------------------------------------------------------

        public static B.Model Derelict()
        {
            var m = new B.Model { Shade = Shade };

            // The window boarded over, and a CLOSED board nailed across the boards.
            Vector3 face = Vector3.back;
            float z = Front - 0.06f;
            D.NailedPlank(m, new Vector3(WinX0 - 0.15f, WinY0 + 0.18f, z), new Vector3(WinX1 + 0.15f, WinY0 + 0.3f, z), face, 0.17f, 1);
            D.NailedPlank(m, new Vector3(WinX0 - 0.12f, WinY0 + 0.55f, z - 0.01f), new Vector3(WinX1 + 0.18f, WinY0 + 0.47f, z - 0.01f), face, 0.18f, 2);
            D.NailedPlank(m, new Vector3(WinX0 - 0.1f, WinY1 - 0.12f, z), new Vector3(WinX1 + 0.12f, WinY1 - 0.18f, z), face, 0.16f, 3);
            D.NailedPlank(m, new Vector3(WinX0, WinY0 + 0.05f, z - 0.05f), new Vector3(WinX1, WinY1 - 0.05f, z - 0.05f), face, 0.15f, 4);

            var sign = new Vector3((WinX0 + WinX1) * 0.5f + 0.1f, WinY0 + 0.42f, z - 0.1f);
            Quaternion tilt = Quaternion.Euler(0f, 0f, -7f);
            m.Box(D.Cardboard, sign, new Vector3(0.6f, 0.26f, 0.025f), 0.01f, tilt, 2);
            for (int k = 0; k < 6; k++)
                m.Box(D.Red * 0.85f, sign + tilt * new Vector3(-0.22f + k * 0.088f, 0f, -0.016f), new Vector3(0.05f, 0.12f, 0.006f), 0.003f, tilt);

            // The porthole boarded with a cross.
            Vector3 port = new Vector3(HX + 0.09f, 1.55f, 0f);
            D.NailedPlank(m, port + new Vector3(0f, -0.3f, -0.3f), port + new Vector3(0f, 0.3f, 0.3f), Vector3.right, 0.12f, 5);
            D.NailedPlank(m, port + new Vector3(0.02f, 0.3f, -0.3f), port + new Vector3(0.02f, -0.3f, 0.3f), Vector3.right, 0.12f, 6);

            // Spray paint, and rust run down from the roof.
            D.Graffiti(m, new Vector3(HX + 0.005f, 0.62f, 0.45f), Vector3.forward, Vector3.up, Vector3.right, 0.75f, D.Graffiti1, 3);
            D.Graffiti(m, new Vector3(1.0f, 1.45f, Front - 0.005f), Vector3.left, Vector3.up, Vector3.back, 0.6f, D.Graffiti2, 8);
            for (int k = 0; k < 6; k++)
            {
                float x = -HX + 0.3f + k * 0.52f + 0.1f * B.Jitter(k);
                float length = 0.3f + 0.35f * Mathf.Abs(B.Jitter(k + 4));
                m.Box(B.Rust * 0.8f, new Vector3(x, WallTop - length * 0.5f, Front - 0.004f), new Vector3(0.035f, length, 0.008f), 0.003f);
            }

            // Weeds up round the plinth and in the gutter.
            for (int k = 0; k < 9; k++)
            {
                float a = k * 0.7f;
                var at = new Vector3(Mathf.Cos(a) * (HX + 0.28f), 0f, Mathf.Sin(a) * (HZ + 0.25f));
                D.WeedClump(m, at, 0.22f, 9, 30 + k);
            }
            D.WeedClump(m, new Vector3(-HX - 0.1f, WallTop + 0.18f, HZ), 0.12f, 6, 50);
            D.WeedClump(m, new Vector3(HX, WallTop + 0.18f, -HZ - 0.1f), 0.1f, 5, 51);

            // The burger's top bun, fallen off, lying upside down on the roof; the pole snapped short.
            m.Tube(D.SteelDark, BurgerAt, BurgerAt + Vector3.up * 0.22f, 0.06f, 12);
            m.Box(D.SteelDark, BurgerAt + Vector3.up * 0.03f, new Vector3(0.36f, 0.06f, 0.36f), 0.02f, 2);
            Matrix4x4 was = m.Transform;
            m.Transform = was * Matrix4x4.TRS(new Vector3(0.65f, WallTop + 0.53f, 0.35f), Quaternion.Euler(178f, 30f, 8f), Vector3.one * 0.85f);
            var profile = new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.42f, 0f), new Vector2(0.45f, 0.04f) };
            for (int i = 1; i <= 8; i++)
            {
                float a = i / 8f * Mathf.PI * 0.5f;
                profile.Add(new Vector2(0.45f * Mathf.Cos(a), 0.04f + 0.3f * Mathf.Sin(a)));
            }
            m.Lathe((p, n) => B.Hex("8C6A44"), Vector3.zero, profile, 20);
            m.Transform = was;

            // Rubbish: a tyre against the wall, a cone knocked over, sacks.
            D.Tyre(m, new Vector3(-HX - 0.2f, 0.38f, -0.7f), new Vector3(1f, 0.15f, 0.2f), 0.38f, 0.22f);
            D.Cone(m, new Vector3(WinX1 + 1.1f, 0f, Front - 1.0f), 30f, true);
            D.RubbishSack(m, new Vector3(HX + 0.3f, 0f, HZ + 0.1f), 1f, 3);
            D.RubbishSack(m, new Vector3(HX + 0.55f, 0f, HZ - 0.3f), 0.8f, 4);
            return m;
        }

        // --- Back at work -------------------------------------------------------------------------------

        public static B.Model Restored()
        {
            var m = new B.Model { Shade = Shade };

            Burger(m, BurgerAt, true);

            // A ring of bulbs round the burger's plate.
            Vector3 plate = BurgerAt + Vector3.up * 0.58f;
            for (int k = 0; k < 10; k++)
            {
                float a = k * Mathf.PI * 2f / 10f;
                D.GlowBall(m, plate + new Vector3(Mathf.Cos(a) * 0.5f, 0.02f, Mathf.Sin(a) * 0.5f), 0.035f);
            }

            // OPEN, lit, in the top corner of the fixed pane; the menu over the counter inside lit too.
            Vector3 open = new Vector3(WinX1 - 0.2f, WinY1 - 0.15f, Front - 0.01f);
            m.Box(B.Iron, open, new Vector3(0.36f, 0.15f, 0.03f), 0.02f, 2);
            D.GlowBox(m, open + Vector3.back * 0.018f, new Vector3(0.28f, 0.08f, 0.012f), Quaternion.identity);
            D.GlowBox(m, new Vector3((WinX0 + WinX1) * 0.5f, WinY1 - 0.2f, Front + Wall + 0.42f), new Vector3(1.1f, 0.18f, 0.02f), Quaternion.identity);

            // Bulbs swagged along the canopy's lip.
            D.BulbString(m, new Vector3(WinX0 - 0.25f, WallTop - 0.4f, Front - 0.66f), new Vector3(WinX1 + 0.25f, WallTop - 0.4f, Front - 0.66f), 7, 0.06f);

            // A flower box under the porthole and a pot by the back door.
            Vector3 box = new Vector3(HX + 0.14f, 1.12f, 0f);
            m.Box(D.TealDark, box, new Vector3(0.2f, 0.18f, 0.7f), 0.03f, 2);
            m.Box(B.Hex("4A3628"), box + Vector3.up * 0.08f, new Vector3(0.15f, 0.04f, 0.64f), 0.015f);
            for (int k = 0; k < 7; k++)
            {
                var at = box + new Vector3(0.02f * B.Jitter(k), 0.15f + 0.03f * B.Jitter(k + 3), -0.28f + k * 0.093f);
                S.Ball(m, (p, n) => B.Leaf, at, new Vector3(0.07f, 0.06f, 0.07f), Quaternion.identity, 8, 6);
                S.Ball(m, (p, n) => B.Flowers[k % B.Flowers.Length], at + Vector3.up * 0.06f, Vector3.one * 0.03f, Quaternion.identity, 6, 4);
            }

            Vector3 pot = new Vector3(0.3f, Plinth, HZ + 0.32f);
            m.Lathe((p, n) => B.Hex("9A5A3E"), pot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.14f, 0f), new Vector2(0.18f, 0.3f), new Vector2(0.2f, 0.32f), new Vector2(0.2f, 0.35f),
                new Vector2(0.16f, 0.35f), new Vector2(0f, 0.33f),
            }, 14);
            for (int k = 0; k < 5; k++)
            {
                float a = k * 1.3f;
                S.Ball(m, (p, n) => k % 2 == 0 ? B.Leaf : B.LeafLight, pot + new Vector3(Mathf.Cos(a) * 0.08f, 0.45f + 0.06f * k % 3, Mathf.Sin(a) * 0.08f),
                       new Vector3(0.13f, 0.11f, 0.13f), Quaternion.identity, 8, 6);
            }

            return m;
        }

        // --- The menu board and the speaker -----------------------------------------------------------------

        /// <summary>
        /// The drive-through's menu board and speaker post, beside the lane where the cars queue: a
        /// framed board under a little roof, three things on the menu each with a small model of the
        /// dish standing proud of it, and the order speaker on its own post with a grille and a button.
        /// Origin on the ground, the board facing -Z onto the lane.
        /// </summary>
        public static B.Model MenuBoard()
        {
            var m = new B.Model { Shade = D.GroundShade() };
            const float W = 1.5f, H = 1.05f, Lift = 0.75f;
            D.PostSign(m, Vector3.zero, 0f, new Vector2(W, H), Lift, D.TealDark, B.Hex("2E3532"), D.Mustard);

            float top = Lift + H;
            m.Box(D.Red, new Vector3(0f, top + 0.08f, -0.05f), new Vector3(W + 0.2f, 0.06f, 0.42f), 0.025f, Quaternion.Euler(-12f, 0f, 0f), 2);
            m.Box(D.Red * 0.85f, new Vector3(0f, top + 0.02f, -0.24f), new Vector3(W + 0.2f, 0.1f, 0.04f), 0.015f, 2);

            // Three rows: a model of the dish at the left of each, a raised price strip after it.
            for (int k = 0; k < 3; k++)
            {
                float y = Lift + H - 0.36f - k * 0.27f;
                var icon = new Vector3(-W * 0.5f + 0.3f, y - 0.06f, -0.12f);
                if (k == 0) Burger(m, icon + Vector3.down * 0.02f, false, 0f, 0.17f);
                else if (k == 1) Cup(m, icon);
                else Fries(m, icon);

                m.Box(D.CreamLight, new Vector3(0.1f, y, -0.065f), new Vector3(0.62f, 0.05f, 0.012f), 0.005f);
                m.Box(D.Mustard, new Vector3(0.55f, y, -0.065f), new Vector3(0.14f, 0.07f, 0.014f), 0.006f);
            }

            // The speaker, on its own short post ahead of the board.
            var post = new Vector3(W * 0.5f + 0.75f, 0f, -0.25f);
            m.Box(D.Concrete, post + Vector3.up * 0.05f, new Vector3(0.24f, 0.1f, 0.24f), 0.03f, 2);
            m.Tube(D.SteelDark, post, post + Vector3.up * 1.0f, 0.05f, 10);
            var head = post + new Vector3(0f, 1.18f, 0f);
            m.Box(D.Steel, head, new Vector3(0.36f, 0.42f, 0.24f), 0.07f, 3);
            m.Lathe((p, n) => B.Iron, head + new Vector3(0f, 0.05f, -0.12f), new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.11f, 0f), new Vector2(0.12f, 0.01f), new Vector2(0.11f, 0.02f), new Vector2(0f, 0.015f),
            }, 16);
            for (int k = 0; k < 9; k++)
            {
                var hole = head + new Vector3(-0.06f + (k % 3) * 0.06f, -0.01f + (k / 3) * 0.06f, -0.145f);
                m.Tube(B.Hex("1F1F21"), hole, hole + Vector3.back * 0.006f, 0.012f, 6);
            }
            m.Tube(D.Red, head + new Vector3(0f, -0.13f, -0.12f), head + new Vector3(0f, -0.13f, -0.15f), 0.03f, 10);
            return m;
        }

        static void Cup(B.Model m, Vector3 foot)
        {
            m.Lathe((p, n) => (p.y - foot.y) > 0.07f && (p.y - foot.y) < 0.12f ? D.Red : D.CreamLight, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.05f, 0f), new Vector2(0.07f, 0.18f), new Vector2(0.075f, 0.19f), new Vector2(0.06f, 0.2f),
                new Vector2(0f, 0.21f),
            }, 12);
            m.Tube(D.Red, foot + new Vector3(0.01f, 0.2f, 0f), foot + new Vector3(0.04f, 0.3f, 0f), 0.008f, 6);
        }

        static void Fries(B.Model m, Vector3 foot)
        {
            m.Box(D.Red, foot + Vector3.up * 0.07f, new Vector3(0.13f, 0.14f, 0.06f), 0.015f, 2);
            for (int k = 0; k < 7; k++)
            {
                float x = -0.05f + k * 0.017f;
                m.Box(D.Mustard * 1.15f, foot + new Vector3(x, 0.17f + 0.02f * B.Jitter(k), 0f), new Vector3(0.014f, 0.12f, 0.014f), 0.004f,
                      Quaternion.Euler(0f, 0f, 8f * B.Jitter(k + 2)));
            }
        }

        /// <summary>The board as found: its face cracked off and propped against a post, weeds round the feet.</summary>
        public static B.Model MenuBoardDerelict()
        {
            var m = new B.Model { Shade = D.GroundShade() };
            m.Box(B.Hex("2E3532"), new Vector3(-0.3f, 0.3f, -0.4f), new Vector3(0.7f, 0.5f, 0.03f), 0.012f, Quaternion.Euler(-62f, 14f, 4f), 2);
            for (int k = 0; k < 4; k++)
                D.WeedClump(m, new Vector3(-0.8f + k * 0.55f, 0f, 0.05f), 0.2f, 8, 70 + k);
            D.Cone(m, new Vector3(1.6f, 0f, -0.6f), 0f, false);
            return m;
        }

        /// <summary>The board lit: its header glowing, and a lamp over it.</summary>
        public static B.Model MenuBoardRestored()
        {
            var m = new B.Model();
            D.GlowBox(m, new Vector3(0f, 0.75f + 1.05f - 0.08f, -0.06f), new Vector3(1.3f, 0.07f, 0.012f), Quaternion.identity);
            m.Lantern(new Vector3(0.6f, 0.75f + 1.05f + 0.05f, -0.06f), Vector3.back);
            return m;
        }

        // --- The bag, and the lane ---------------------------------------------------------------------------

        /// <summary>A brown paper takeaway bag, its top folded over, a red sticker on the front.</summary>
        public static B.Model Bag()
        {
            var m = new B.Model();
            Color kraft = B.Hex("A8875A");
            var body = new SmoothMesh(1);
            body.RoundedBox(0, new Vector3(0f, 0.13f, 0f), new Vector3(0.22f, 0.26f, 0.14f), 0.02f, Quaternion.identity, 2);
            m.Warp = p => new Vector3(p.x * (1f - 0.08f * p.y / 0.26f), p.y, p.z);
            m.Add(body, (p, n) => kraft * (n.y < -0.5f ? 0.8f : 1f));
            m.Warp = null;
            m.Box(kraft * 0.92f, new Vector3(0f, 0.27f, -0.02f), new Vector3(0.21f, 0.06f, 0.1f), 0.012f, Quaternion.Euler(-20f, 0f, 0f), 2);
            m.Lathe((p, n) => D.Red, new Vector3(0f, 0.13f, -0.072f), new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.045f, 0f), new Vector2(0.045f, 0.006f), new Vector2(0f, 0.006f),
            }, 14);
            return m;
        }

        /// <summary>
        /// The lane the cars queue in, along <paramref name="line"/> (local points, on the ground): a
        /// strip of tarmac <paramref name="width"/> across standing a few centimetres proud, kerb
        /// stones along the side away from the road between <paramref name="kerbFrom"/> and
        /// <paramref name="kerbTo"/> (distances along the line), raised arrows painted down it the way
        /// the cars go, and a stop line at <paramref name="stopAt"/>.
        /// </summary>
        public static B.Model Lane(IList<Vector3> line, float width, float kerbFrom, float kerbTo, float stopAt)
        {
            var m = new B.Model();
            var path = Smoothed(line, 8);

            // Distances along it, and the way across at each point (to the left of travel).
            var along = new float[path.Count];
            for (int i = 1; i < path.Count; i++) along[i] = along[i - 1] + Vector3.Distance(path[i - 1], path[i]);

            Vector3 Across(int i)
            {
                Vector3 d = path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)];
                d.y = 0f;
                return Vector3.Cross(Vector3.up, d.normalized);
            }

            Vector3 At(float u, int i, float y) => path[i] + Across(i) * ((u - 0.5f) * width) + Vector3.up * y;

            const float Top = 0.035f, Bottom = -0.03f;
            int n = path.Count - 1;
            Func<Vector3, Vector3, Color> tarmac = (p, nn) => D.Asphalt * (0.92f + 0.08f * K.Noise(p * 1.3f)) * (nn.y < 0.5f ? 0.8f : 1f);

            m.Surface(tarmac, 6, n, (u, v) =>
            {
                float f = v * n;
                int i = Mathf.Min(n - 1, Mathf.FloorToInt(f));
                return Vector3.Lerp(At(u, i, Top), At(u, i + 1, Top), f - i);
            });

            foreach (float u in new[] { 0f, 1f })
            {
                m.Surface(tarmac, n, 1, (v, t) =>
                {
                    float f = v * n;
                    int i = Mathf.Min(n - 1, Mathf.FloorToInt(f));
                    Vector3 hi = Vector3.Lerp(At(u, i, Top), At(u, i + 1, Top), f - i);
                    Vector3 lo = Vector3.Lerp(At(u, i, Bottom), At(u, i + 1, Bottom), f - i);
                    return u == 0f ? Vector3.Lerp(hi, lo, t) : Vector3.Lerp(lo, hi, t);
                });
            }

            Vector3 PointAt(float distance, out Vector3 across, out Vector3 forward)
            {
                int i = 1;
                while (i < path.Count - 1 && along[i] < distance) i++;
                float t = Mathf.InverseLerp(along[i - 1], along[i], distance);
                forward = (path[i] - path[i - 1]).normalized;
                across = Vector3.Cross(Vector3.up, forward);
                return Vector3.Lerp(path[i - 1], path[i], t);
            }

            // Kerb stones along the far side.
            const float Stone = 0.62f;
            int k = 0;
            for (float d = kerbFrom; d + Stone <= kerbTo; d += Stone + 0.02f, k++)
            {
                Vector3 c = PointAt(d + Stone * 0.5f, out Vector3 across, out Vector3 forward);
                Quaternion turn = Quaternion.LookRotation(forward, Vector3.up);
                m.Box(D.Concrete * (k % 2 == 0 ? 1f : 0.93f), c + across * (width * 0.5f + 0.1f) + Vector3.up * 0.07f, new Vector3(0.18f, 0.14f, Stone),
                      0.035f, turn, 2);
            }

            // Arrows down the lane the way the cars go, and the stop line at the window.
            for (float d = kerbFrom + 2.5f; d < kerbTo - 1.5f; d += 6f)
            {
                Vector3 c = PointAt(d, out _, out Vector3 forward);
                Quaternion turn = Quaternion.LookRotation(forward, Vector3.up);
                m.Box(D.RoadPaint, c + Vector3.up * (Top + 0.004f), new Vector3(0.14f, 0.008f, 1.0f), 0.003f, turn);
                foreach (int s in new[] { -1, 1 })
                    m.Box(D.RoadPaint, c + forward * 0.42f + turn * new Vector3(s * 0.13f, Top + 0.004f, 0f), new Vector3(0.12f, 0.008f, 0.45f), 0.003f,
                          turn * Quaternion.Euler(0f, s * -38f, 0f));
            }

            {
                Vector3 c = PointAt(stopAt, out _, out Vector3 forward);
                m.Box(D.RoadPaint, c + Vector3.up * (Top + 0.004f), new Vector3(width - 0.4f, 0.008f, 0.18f), 0.003f, Quaternion.LookRotation(forward, Vector3.up));
            }

            return m;
        }

        /// <summary><paramref name="points"/> joined by a Catmull-Rom curve, <paramref name="steps"/> points to a span.</summary>
        static List<Vector3> Smoothed(IList<Vector3> points, int steps)
        {
            var result = new List<Vector3>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 p0 = points[Mathf.Max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(i + 2, points.Count - 1)];
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps;
                    float t2 = t * t, t3 = t2 * t;
                    result.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }

            result.Add(points[points.Count - 1]);
            return result;
        }
    }
}
