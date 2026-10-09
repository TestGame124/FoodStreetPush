using System;
using System.Collections.Generic;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using S = LastOrders.EditorTools.SoupKitchenModel;

namespace FoodStreet.Visitors.EditorTools
{
    /// <summary>
    /// What the visitors' vehicles are made of, in the game's smooth low-poly style: bodywork lofted
    /// as one soft rounded shape, glass and doors laid onto it, fat tyres, big round lamps, chunky
    /// bumpers and whatever is tied on the roof.
    ///
    /// Everything is built in vehicle space - origin on the ground under the middle of the car, +Z
    /// the front, +X the right - and painted into the vertices, lit by LastOrders/Stylized like the
    /// compound's props. Paint is muted and worn: grime toward the ground, rust coming through low
    /// down, chips of primer on the tops.
    /// </summary>
    internal static class VehicleKit
    {
        // --- The palette ---------------------------------------------------------------------------

        /// <summary>Chrome as the props paint it: mid grey, or a bumper facing the sun reads white.</summary>
        internal static readonly Color Chrome = B.Hex("8C9297");
        internal static readonly Color ChromeLight = B.Hex("A0A5A9");
        internal static readonly Color ChromeDark = B.Hex("5E6367");
        internal static readonly Color Rubber = B.Hex("2C2C2E");
        internal static readonly Color RubberSide = B.Hex("37373A");
        internal static readonly Color Whitewall = B.Hex("B3AE9E");
        internal static readonly Color Trim = B.Hex("3A3B3E");
        internal static readonly Color BumperGrey = B.Hex("4B4D51");
        internal static readonly Color Interior = B.Hex("222326");
        internal static readonly Color Glass = B.Glass;
        internal static readonly Color GlassSky = B.GlassSky;
        internal static readonly Color Lens = B.Hex("C9C1A3");
        internal static readonly Color Indicator = B.Hex("B07A3E");
        internal static readonly Color TailLens = B.Hex("8E3A33");
        internal static readonly Color Cream = B.Hex("B5AE9A");
        internal static readonly Color Rust = B.Hex("85614C");
        internal static readonly Color RustDark = B.Hex("6E4A38");
        internal static readonly Color Plate = B.Hex("B0A994");
        internal static readonly Color PlateInk = B.Hex("33363A");
        internal static readonly Color Cardboard = B.Hex("9F8461");
        internal static readonly Color Straw = B.Hex("A79562");
        internal static readonly Color Leather = B.Hex("6E4A38");
        internal static readonly Color Strap = B.Hex("3F352C");

        // --- Noise and paint -----------------------------------------------------------------------

        internal static float Smooth(float t) => S.Smooth(t);

        internal static float Band(float x, float from, float to) => Smooth((x - from) / (to - from));

        static float Lattice(int x, int y, int z) => B.Hash(x * 73856093 ^ y * 19349663 ^ z * 83492791) % 10007 / 10007f;

        /// <summary>Smooth value noise in 0..1.</summary>
        internal static float Noise(Vector3 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
            float fx = Smooth(p.x - x), fy = Smooth(p.y - y), fz = Smooth(p.z - z);

            float a = Mathf.Lerp(Lattice(x, y, z), Lattice(x + 1, y, z), fx);
            float b = Mathf.Lerp(Lattice(x, y + 1, z), Lattice(x + 1, y + 1, z), fx);
            float c = Mathf.Lerp(Lattice(x, y, z + 1), Lattice(x + 1, y, z + 1), fx);
            float d = Mathf.Lerp(Lattice(x, y + 1, z + 1), Lattice(x + 1, y + 1, z + 1), fx);
            return Mathf.Lerp(Mathf.Lerp(a, b, fy), Mathf.Lerp(c, d, fy), fz);
        }

        internal static Vector3 Seed(int key) => new Vector3(key * 1.37f % 31f, key * 2.91f % 29f, key * 1.73f % 23f);

        /// <summary>
        /// Paintwork that has done some miles: a little uneven, dustier toward the ground, rust coming
        /// through along the bottom, chips of primer on the tops, dark underneath.
        /// </summary>
        internal static Func<Vector3, Vector3, Color> Worn(Color paint, int key, float wear = 1f)
        {
            Vector3 seed = Seed(key);
            return (p, n) => Weather(paint, p, n, seed, wear);
        }

        internal static Color Weather(Color paint, Vector3 p, Vector3 n, Vector3 seed, float wear)
        {
            Color c = paint * (0.96f + 0.06f * Noise(p * 1.7f + seed));
            c *= Mathf.Lerp(0.82f, 1f, Smooth((p.y - 0.22f) / 0.5f));

            // Rust only low down - sills, arches, the bottoms of the doors - and only here and there.
            float rust = Band(Noise(p * 2.6f + seed * 2f), 0.66f, 0.74f) * (1f - Smooth((p.y - 0.25f) / 0.35f)) * wear;
            c = Color.Lerp(c, Color.Lerp(Rust, RustDark, Noise(p * 7f)), rust * 0.75f);

            if (n.y < -0.45f) c *= 0.6f;
            return c;
        }

        /// <summary>
        /// Paint for a pane of glass spanning <paramref name="centre"/> ± half of <paramref name="across"/>
        /// and <paramref name="up"/>: dark low down, the sky caught toward its top, and one broad soft
        /// sheen slanting across it. Broad on purpose: vertex paint draws a thin streak as a zigzag.
        /// </summary>
        internal static Func<Vector3, Vector3, Color> Glazing(Vector3 centre, Vector3 across, Vector3 up)
        {
            float aa = Mathf.Max(1e-6f, across.sqrMagnitude), uu = Mathf.Max(1e-6f, up.sqrMagnitude);
            return (p, n) =>
            {
                float a = 2f * Vector3.Dot(p - centre, across) / aa;
                float b = 2f * Vector3.Dot(p - centre, up) / uu;

                Color c = Color.Lerp(Glass, GlassSky, Smooth((b + 1f) * 0.5f) * 0.6f + Mathf.Max(0f, n.y) * 0.25f);
                float d = (a * 0.5f + b - 0.15f) / 0.42f;
                return Color.Lerp(c, GlassSky * 1.25f, Mathf.Exp(-d * d) * 0.4f);
            };
        }

        internal static Func<Vector3, Vector3, Color> Flat(Color c) => (p, n) => n.y < -0.45f ? c * 0.65f : c;

        // --- Placing parts -------------------------------------------------------------------------

        /// <summary>Builds whatever <paramref name="build"/> adds at <paramref name="at"/> turned by <paramref name="turn"/>.</summary>
        internal static void Placed(B.Model m, Vector3 at, Quaternion turn, Action build)
        {
            Matrix4x4 was = m.Transform;
            m.Transform = was * Matrix4x4.TRS(at, turn, Vector3.one);
            try { build(); }
            finally { m.Transform = was; }
        }

        // --- Tubes, bars and rails -----------------------------------------------------------------

        /// <summary>
        /// A round bar swept smoothly through <paramref name="points"/>: a bumper, a roof rail, a strap,
        /// a handle. An open bar has rounded ends; a <paramref name="closed"/> one is a loop. <paramref name="squash"/>
        /// flattens its section (x across the bend, y up from it).
        /// </summary>
        internal static void Sweep(B.Model m, Func<Vector3, Vector3, Color> paint, IList<Vector3> points, float radius, int around = 10,
                                   int perSpan = 6, bool closed = false, Vector2 squash = default)
        {
            if (points.Count < 2) return;
            if (squash == default) squash = Vector2.one;

            // The centre line, Catmull-Rom through the points.
            var path = new List<Vector3>();
            int spans = closed ? points.Count : points.Count - 1;
            Vector3 P(int i) => closed ? points[(i % points.Count + points.Count) % points.Count]
                                       : points[Mathf.Clamp(i, 0, points.Count - 1)];

            for (int s = 0; s < spans; s++)
            {
                Vector3 p0 = P(s - 1), p1 = P(s), p2 = P(s + 1), p3 = P(s + 2);
                if (!closed && s == 0) p0 = p1 * 2f - p2;
                if (!closed && s == spans - 1) p3 = p2 * 2f - p1;

                for (int k = 0; k < perSpan; k++)
                {
                    float t = k / (float)perSpan;
                    path.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
                                     (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t));
                }
            }

            path.Add(closed ? path[0] : points[points.Count - 1]);

            // Tangents and frames carried along it.
            int count = path.Count;
            var tangent = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 a = path[closed ? (i - 1 + count - 1) % (count - 1) : Mathf.Max(i - 1, 0)];
                Vector3 b = path[closed ? (i + 1) % (count - 1) : Mathf.Min(i + 1, count - 1)];
                tangent[i] = (b - a).normalized;
            }

            var normal = new Vector3[count];
            Vector3 reference = Mathf.Abs(Vector3.Dot(tangent[0], Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            normal[0] = (reference - tangent[0] * Vector3.Dot(reference, tangent[0])).normalized;
            for (int i = 1; i < count; i++)
            {
                Vector3 n = normal[i - 1] - tangent[i] * Vector3.Dot(normal[i - 1], tangent[i]);
                normal[i] = n.sqrMagnitude > 1e-8f ? n.normalized : normal[i - 1];
            }

            // Rings: rounded caps added past each end of an open bar.
            var centres = new List<Vector3>();
            var radii = new List<float>();
            var frames = new List<(Vector3 t, Vector3 n)>();
            const int capRings = 4;

            if (!closed)
            {
                for (int k = capRings; k >= 1; k--)
                {
                    float a = k / (float)capRings * Mathf.PI * 0.5f;
                    centres.Add(path[0] - tangent[0] * (radius * Mathf.Sin(a) * 0.8f));
                    radii.Add(radius * Mathf.Cos(a));
                    frames.Add((tangent[0], normal[0]));
                }
            }

            for (int i = 0; i < count; i++)
            {
                centres.Add(path[i]);
                radii.Add(radius);
                frames.Add((tangent[i], normal[i]));
            }

            if (!closed)
            {
                for (int k = 1; k <= capRings; k++)
                {
                    float a = k / (float)capRings * Mathf.PI * 0.5f;
                    centres.Add(path[count - 1] + tangent[count - 1] * (radius * Mathf.Sin(a) * 0.8f));
                    radii.Add(radius * Mathf.Cos(a));
                    frames.Add((tangent[count - 1], normal[count - 1]));
                }
            }

            int rings = centres.Count;
            m.Surface(paint, around, rings - 1, (u, v) =>
            {
                int i = Mathf.Clamp(Mathf.RoundToInt(v * (rings - 1)), 0, rings - 1);
                var (t, n) = frames[i];
                Vector3 b = Vector3.Cross(t, n);
                float a = u * Mathf.PI * 2f;
                return centres[i] + (n * (Mathf.Cos(a) * squash.y) + b * (Mathf.Sin(a) * squash.x)) * radii[i];
            }, true);
        }

        internal static void Sweep(B.Model m, Color colour, IList<Vector3> points, float radius, int around = 10, int perSpan = 6,
                                   bool closed = false) =>
            Sweep(m, Flat(colour), points, radius, around, perSpan, closed);

        // --- Wheels --------------------------------------------------------------------------------

        /// <summary>
        /// A wheel round the Y axis through the origin, its outside facing +Y: a fat round-shouldered
        /// tyre with blocks of tread, a painted steel rim dished in, a chrome hubcap and five nuts.
        /// Toy-sized in the tyre: the rim is small inside a lot of rubber.
        /// </summary>
        internal static void Wheel(B.Model m, float radius, float width, Color rim, Color hubcap, bool whitewall, int blocks = 14)
        {
            float h = width * 0.5f;
            float ri = radius * 0.58f;
            float corner = Mathf.Min(h * 0.9f, (radius - ri) * 0.7f);
            float wallIn = ri + (radius - ri) * 0.28f, wallOut = ri + (radius - ri) * 0.62f;

            var profile = new List<Vector2>();
            profile.Add(new Vector2(ri, -h * 0.82f));
            profile.Add(new Vector2(ri, -h * 0.82f));
            profile.Add(new Vector2(ri + 0.02f, -h * 0.94f));
            profile.Add(new Vector2(radius - corner, -h));
            for (int k = 1; k <= 4; k++)
            {
                float a = -Mathf.PI * 0.5f + k / 4f * Mathf.PI * 0.5f;
                profile.Add(new Vector2(radius - corner + Mathf.Cos(a) * corner, -h + corner + Mathf.Sin(a) * corner));
            }

            profile.Add(new Vector2(radius, h - corner));
            for (int k = 1; k <= 4; k++)
            {
                float a = k / 4f * Mathf.PI * 0.5f;
                profile.Add(new Vector2(radius - corner + Mathf.Cos(a) * corner, h - corner + Mathf.Sin(a) * corner));
            }

            // The outer sidewall, with rings either side of the whitewall so its edges are sharp.
            profile.Add(new Vector2(wallOut + 0.003f, h * 0.99f));
            profile.Add(new Vector2(wallOut - 0.003f, h * 0.99f));
            profile.Add(new Vector2(wallIn + 0.003f, h * 0.97f));
            profile.Add(new Vector2(wallIn - 0.003f, h * 0.97f));
            profile.Add(new Vector2(ri + 0.02f, h * 0.94f));
            profile.Add(new Vector2(ri, h * 0.82f));
            profile.Add(new Vector2(ri, h * 0.82f));

            // And shut along the inside, round to where it began.
            profile.Add(new Vector2(ri, -h * 0.82f));

            m.Lathe((p, n) =>
            {
                float across = new Vector2(p.x, p.z).magnitude;
                if (Mathf.Abs(n.y) < 0.6f && across > radius - corner * 0.8f)
                {
                    float a = Mathf.Atan2(p.z, p.x) / (Mathf.PI * 2f) + 0.5f;
                    return ((int)(a * blocks * 2f)) % 2 == 0 ? Rubber : Rubber * 0.78f;
                }

                if (whitewall && p.y > 0f && across > wallIn && across < wallOut) return Whitewall;
                return n.y < -0.5f ? RubberSide * 0.85f : RubberSide;
            }, Vector3.zero, profile, 28);

            // The steel rim, dished, and the hubcap domed out of its middle.
            var disc = new List<Vector2>
            {
                new Vector2(0f, -h * 0.5f), new Vector2(ri + 0.004f, -h * 0.5f), new Vector2(ri + 0.004f, -h * 0.5f),
                new Vector2(ri + 0.004f, h * 0.6f), new Vector2(ri * 0.9f, h * 0.68f), new Vector2(ri * 0.78f, h * 0.55f),
                new Vector2(ri * 0.6f, h * 0.52f), new Vector2(ri * 0.47f, h * 0.62f), new Vector2(ri * 0.43f, h * 0.64f),
                new Vector2(ri * 0.43f, h * 0.64f), new Vector2(ri * 0.38f, h * 0.84f), new Vector2(ri * 0.24f, h * 0.98f),
                new Vector2(0f, h * 1.02f),
            };

            m.Lathe((p, n) =>
            {
                float across = new Vector2(p.x, p.z).magnitude;
                if (across < ri * 0.435f && p.y > h * 0.6f) return Color.Lerp(hubcap, ChromeLight, Smooth((p.y - h * 0.85f) / (h * 0.2f)) * 0.5f);
                return across < ri * 0.8f && across > ri * 0.55f ? rim * 0.82f : rim;
            }, Vector3.zero, disc, 24);

            for (int k = 0; k < 5; k++)
            {
                float a = k * Mathf.PI * 2f / 5f + 0.3f;
                S.Ball(m, (p, n) => ChromeDark, new Vector3(Mathf.Cos(a) * ri * 0.6f, h * 0.55f, Mathf.Sin(a) * ri * 0.6f),
                       new Vector3(0.018f, 0.014f, 0.018f), Quaternion.identity, 6, 4);
            }
        }

        /// <summary>A spare tyre on its own, lying flat at <paramref name="foot"/>.</summary>
        internal static void SpareTyre(B.Model m, Vector3 foot, float radius, float width, Color rim) =>
            Placed(m, foot + Vector3.up * (width * 0.5f), Quaternion.identity, () => Wheel(m, radius, width, rim, Chrome, false));

        // --- Lamps, plates, mirrors ------------------------------------------------------------------

        /// <summary>
        /// A big round lamp at <paramref name="at"/> looking along <paramref name="facing"/>: a chrome
        /// bezel standing proud and a domed lens a shade lighter in its middle. The car's eyes.
        /// </summary>
        internal static void Lamp(B.Model m, Vector3 at, Vector3 facing, float r, Color lens, Color bezel, float depth = 0.07f)
        {
            Placed(m, at, Quaternion.FromToRotation(Vector3.up, facing.normalized), () =>
            {
                m.Lathe((p, n) =>
                {
                    float across = new Vector2(p.x, p.z).magnitude;
                    if (across > r * 0.95f || p.y < -0.004f) return bezel;
                    return lens * Mathf.Lerp(1.06f, 0.92f, across / r);
                }, Vector3.zero, new List<Vector2>
                {
                    new Vector2(0f, -depth), new Vector2(r * 1.02f, -depth), new Vector2(r * 1.12f, -depth * 0.4f),
                    new Vector2(r * 1.15f, 0f), new Vector2(r * 1.1f, 0.017f), new Vector2(r * 0.98f, 0.019f),
                    new Vector2(r * 0.96f, 0.012f), new Vector2(r * 0.96f, 0.012f), new Vector2(r * 0.72f, 0.03f),
                    new Vector2(r * 0.38f, 0.041f), new Vector2(0f, 0.045f),
                }, 22);
            });
        }

        /// <summary>A number plate standing on the surface at <paramref name="at"/>, facing <paramref name="facing"/>.</summary>
        internal static void NumberPlate(B.Model m, Vector3 at, Vector3 facing, float width = 0.42f)
        {
            Quaternion turn = Quaternion.LookRotation(facing.normalized, Vector3.up);
            m.Box(Trim, at, new Vector3(width + 0.03f, 0.13f, 0.02f), 0.008f, turn);
            m.Box(Plate, at + turn * new Vector3(0f, 0f, 0.012f), new Vector3(width, 0.11f, 0.012f), 0.006f, turn);
            for (int k = 0; k < 5; k++)
            {
                float x = (k - 2f) * width * 0.17f;
                m.Box(PlateInk, at + turn * new Vector3(x, 0f, 0.02f), new Vector3(width * 0.1f, 0.06f, 0.006f), 0.003f, turn);
            }
        }

        /// <summary>A wing mirror out from <paramref name="root"/> on the <paramref name="side"/> (+1 right): a chrome stalk and a body-coloured head.</summary>
        internal static void Mirror(B.Model m, Vector3 root, float side, Color body)
        {
            Vector3 tip = root + new Vector3(side * 0.15f, 0.06f, -0.03f);
            m.Tube(Chrome, root, tip, 0.016f, 8);
            Quaternion turn = Quaternion.Euler(0f, -side * 10f, 0f);
            Vector3 head = tip + new Vector3(side * 0.05f, 0.05f, 0f);
            m.Box(body, head, new Vector3(0.12f, 0.12f, 0.07f), 0.03f, turn, 2);
            m.Box(GlassSky, head + turn * new Vector3(0f, 0f, -0.036f), new Vector3(0.095f, 0.095f, 0.01f), 0.012f, turn);
        }

        /// <summary>A short chrome exhaust pipe at <paramref name="at"/>, pointing back.</summary>
        internal static void Tailpipe(B.Model m, Vector3 at)
        {
            m.Tube((p, n) => n.z < -0.9f ? Interior : ChromeDark, at + Vector3.forward * 0.35f, at, 0.04f, 0.045f, 10);
        }

        // --- What gets tied on the roof ----------------------------------------------------------------

        /// <summary>A hard suitcase standing on <paramref name="foot"/>: rounded, two straps round it, a handle on top.</summary>
        internal static void Suitcase(B.Model m, Vector3 foot, Vector3 size, float yaw, Color colour, int key)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 centre = foot + Vector3.up * (size.y * 0.5f);
            var shape = new LastOrders.EditorTools.SmoothMesh(1);
            shape.RoundedBox(0, centre, size, Mathf.Min(size.x, size.y, size.z) * 0.22f, turn, 2);
            m.Add(shape, Worn(colour, key, 0.3f));

            // A darker seam round its middle, the straps over it, and the handle.
            m.Box(colour * 0.7f, centre, new Vector3(size.x + 0.01f, 0.022f, size.z + 0.01f), 0.01f, turn);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 at = centre + turn * new Vector3(s * size.x * 0.27f, 0f, 0f);
                m.Box(Strap, at, new Vector3(0.05f, size.y + 0.018f, size.z + 0.018f), 0.012f, turn);
            }

            Vector3 top = foot + Vector3.up * size.y;
            Sweep(m, Strap, new[]
            {
                top + turn * new Vector3(-0.08f, -0.005f, 0f), top + turn * new Vector3(-0.06f, 0.045f, 0f),
                top + turn * new Vector3(0.06f, 0.045f, 0f), top + turn * new Vector3(0.08f, -0.005f, 0f),
            }, 0.014f, 6, 3);
        }

        /// <summary>A rolled-up bedroll or sleeping bag lying along <paramref name="yaw"/>, two straps round it.</summary>
        internal static void Bedroll(B.Model m, Vector3 centre, float length, float radius, float yaw, Color colour, int key)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 along = turn * Vector3.right * (length * 0.5f - radius * 0.6f);
            Vector3 seed = Seed(key);

            Sweep(m, (p, n) =>
            {
                // The roll's layers show as a darker spiral at the ends.
                float end = Mathf.Abs(Vector3.Dot(n, turn * Vector3.right));
                Color c = colour * (0.95f + 0.08f * Noise(p * 6f + seed));
                return end > 0.7f ? c * (0.8f + 0.15f * Mathf.Sin(Vector3.Distance(p, centre) * 90f)) : c;
            }, new[] { centre - along, centre + along }, radius, 14, 4);

            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 at = centre + turn * Vector3.right * (s * length * 0.24f);
                var loop = new List<Vector3>();
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI * 0.25f;
                    loop.Add(at + turn * new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a)) * (radius + 0.008f));
                }

                Sweep(m, Strap, loop, 0.012f, 6, 3, closed: true);
            }
        }

        /// <summary>A jerry can on <paramref name="foot"/>: pressed steel with its X, three-bar handle and spout.</summary>
        internal static void JerryCan(B.Model m, Vector3 foot, float yaw, Color colour, int key)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 size = new Vector3(0.17f, 0.34f, 0.27f);
            Vector3 centre = foot + Vector3.up * (size.y * 0.5f);

            var shape = new LastOrders.EditorTools.SmoothMesh(1);
            shape.RoundedBox(0, centre, size, 0.025f, turn, 2);
            m.Add(shape, Worn(colour, key, 0.6f));

            for (int s = -1; s <= 1; s += 2)
            {
                for (int d = -1; d <= 1; d += 2)
                {
                    m.Box(colour * 0.9f, centre + turn * new Vector3(s * (size.x * 0.5f + 0.004f), 0f, 0f),
                          new Vector3(0.012f, 0.3f, 0.022f), 0.006f, turn * Quaternion.Euler(d * 35f, 0f, 0f));
                }
            }

            Vector3 top = foot + Vector3.up * size.y;
            for (int k = -1; k <= 1; k++)
                m.Tube(colour * 0.85f, top + turn * new Vector3(0f, 0f, k * 0.045f - 0.03f), top + turn * new Vector3(0f, 0.05f, k * 0.045f - 0.03f), 0.01f, 6);
            m.Tube(colour * 0.85f, top + turn * new Vector3(0f, 0.05f, -0.085f), top + turn * new Vector3(0f, 0.05f, 0.03f), 0.013f, 6);
            m.Tube(ChromeDark, top + turn * new Vector3(0f, -0.01f, 0.09f), top + turn * new Vector3(0f, 0.04f, 0.1f), 0.024f, 8);
        }

        /// <summary>A cardboard parcel taped across its top, a label on its side.</summary>
        internal static void Parcel(B.Model m, Vector3 foot, Vector3 size, float yaw, int key)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 centre = foot + Vector3.up * (size.y * 0.5f);
            Color card = Cardboard * (0.93f + 0.1f * B.Jitter(key) * 0.5f + 0.05f);

            var shape = new LastOrders.EditorTools.SmoothMesh(1);
            shape.RoundedBox(0, centre, size, 0.018f, turn, 1);
            m.Add(shape, (p, n) => n.y < -0.45f ? card * 0.6f : card * (0.94f + 0.08f * Noise(p * 9f + Seed(key))));

            m.Box(Cream * 0.95f, centre + Vector3.up * (size.y * 0.5f), new Vector3(0.07f, 0.008f, size.z + 0.006f), 0.003f, turn);
            m.Box(Cream, centre + turn * new Vector3(size.x * 0.18f, 0f, size.z * 0.5f + 0.003f), new Vector3(size.x * 0.32f, size.y * 0.3f, 0.006f), 0.003f, turn);
        }

        /// <summary>A bale of straw bound with two lengths of twine.</summary>
        internal static void Bale(B.Model m, Vector3 foot, Vector3 size, float yaw, int key)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 centre = foot + Vector3.up * (size.y * 0.5f);
            Vector3 seed = Seed(key);

            var shape = new LastOrders.EditorTools.SmoothMesh(1);
            shape.RoundedBox(0, centre, size, 0.06f, turn, 2);
            m.Add(shape, (p, n) =>
            {
                Color c = Straw * (0.88f + 0.18f * Noise(p * 14f + seed));
                return n.y < -0.45f ? c * 0.65f : c * Mathf.Lerp(0.9f, 1.04f, Mathf.Max(0f, n.y));
            });

            for (int s = -1; s <= 1; s += 2)
                m.Box(B.Twine * 0.85f, centre + turn * new Vector3(s * size.x * 0.25f, 0f, 0f),
                      new Vector3(0.025f, size.y + 0.01f, size.z + 0.01f), 0.01f, turn);
        }

        /// <summary>A galvanised milk churn standing on <paramref name="foot"/>.</summary>
        internal static void Churn(B.Model m, Vector3 foot, float scale = 1f)
        {
            float r = 0.15f * scale, h = 0.5f * scale;
            m.Lathe((p, n) => B.Hex("8C9296") * (0.92f + 0.08f * Noise(p * 10f)) * (n.y < -0.4f ? 0.65f : 1f), foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(r, 0f), new Vector2(r * 1.04f, h * 0.06f), new Vector2(r, h * 0.12f),
                new Vector2(r, h * 0.55f), new Vector2(r * 0.62f, h * 0.78f), new Vector2(r * 0.58f, h * 0.9f),
                new Vector2(r * 0.66f, h * 0.93f), new Vector2(r * 0.6f, h), new Vector2(0f, h * 1.02f),
            }, 18);

            for (int s = -1; s <= 1; s += 2)
                Sweep(m, ChromeDark, new[]
                {
                    foot + new Vector3(s * r * 0.75f, h * 0.68f, -0.04f), foot + new Vector3(s * r * 1.05f, h * 0.74f, 0f),
                    foot + new Vector3(s * r * 0.75f, h * 0.68f, 0.04f),
                }, 0.01f, 6, 3);
        }

        /// <summary>A soft holdall, slumped: an egg of canvas with a strap over it.</summary>
        internal static void Holdall(B.Model m, Vector3 foot, Vector3 size, float yaw, Color colour, int key)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 centre = foot + Vector3.up * (size.y * 0.5f);
            S.Ball(m, Worn(colour, key, 0.2f), centre, size * 0.5f, turn, 16, 10);
            Sweep(m, Strap, new[]
            {
                centre + turn * new Vector3(-size.x * 0.3f, size.y * 0.3f, 0f), centre + turn * new Vector3(0f, size.y * 0.75f, 0f),
                centre + turn * new Vector3(size.x * 0.3f, size.y * 0.3f, 0f),
            }, 0.014f, 6, 4);
        }
    }

    /// <summary>
    /// A shape lofted along Z: rounded sections from <see cref="Back"/> to <see cref="Front"/>, shut at
    /// each end over <see cref="CapBack"/> and <see cref="CapFront"/> metres - a car's tub, its cabin,
    /// a fender. Its sections are superellipses (<see cref="Round"/> 2 is an ellipse, higher is
    /// boxier) between a bottom and a top that may change along its length, as may its width, and
    /// the whole shape can be bent afterwards by <see cref="Warp"/>: a windscreen raked back.
    ///
    /// The surface is also known in closed form, which is what lets parts be laid onto it: a point
    /// on either side at a (z, height), on either end at an (x, height), on the top at an (x, z).
    /// Windows, doors and stripes are patches of the surface lifted off it by a few millimetres, so
    /// their edges are as sharp as their own vertices rather than as soft as the body's.
    /// </summary>
    internal sealed class Loft
    {
        public float Back, Front;
        public float CapBack = 0.3f, CapFront = 0.3f;
        public float Round = 3.2f;
        public float CapRound = 2.8f;

        /// <summary>At a z: (bottom, top, half width). Held at its value where each end begins.</summary>
        public Func<float, Vector3> Section;

        /// <summary>The width at a height, from -1 at the bottom to 1 at the top: the sides tucking in.</summary>
        public Func<float, float> Tuck = y => 1f;

        public Func<Vector3, Vector3> Warp = p => p;

        public float ZBack => Back + CapBack;
        public float ZFront => Front - CapFront;

        float[] perimeter;
        float perimeterRound = -1f;
        const int Steps = 512;

        // --- The section's outline, spaced evenly along its length ---------------------------------

        static Vector2 Squircle(float theta, float p)
        {
            float c = Mathf.Cos(theta), s = Mathf.Sin(theta);
            return new Vector2(Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / p), -Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 2f / p));
        }

        Vector2 Perimeter(float u)
        {
            if (perimeter == null || perimeterRound != Round) Measure();

            float f = Mathf.Repeat(u, 1f) * Steps;
            int i = Mathf.Min((int)f, Steps - 1);
            return Squircle(Mathf.Lerp(perimeter[i], perimeter[i + 1], f - i), Round);
        }

        void Measure()
        {
            const int fine = 4096;
            var lengths = new float[fine + 1];
            Vector2 last = Squircle(0f, Round);
            for (int k = 1; k <= fine; k++)
            {
                Vector2 p = Squircle(k * Mathf.PI * 2f / fine, Round);
                lengths[k] = lengths[k - 1] + (p - last).magnitude;
                last = p;
            }

            perimeter = new float[Steps + 1];
            int j = 0;
            for (int i = 0; i <= Steps; i++)
            {
                float target = lengths[fine] * i / Steps;
                while (j < fine - 1 && lengths[j + 1] < target) j++;
                float t = Mathf.InverseLerp(lengths[j], lengths[j + 1], target);
                perimeter[i] = (j + t) * Mathf.PI * 2f / fine;
            }

            perimeterRound = Round;
        }

        // --- Along it ---------------------------------------------------------------------------------

        (float z, float s) Along(float v)
        {
            float lb = CapBack * 1.6f, lf = CapFront * 1.6f, lm = Mathf.Max(1e-4f, ZFront - ZBack);
            float total = lb + lm + lf;
            float vb = lb / total, vf = lf / total;
            float e = 2f / CapRound;

            if (v < vb)
            {
                float a = v / vb * Mathf.PI * 0.5f;
                return (ZBack - CapBack * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(a)), e), Mathf.Pow(Mathf.Max(0f, Mathf.Sin(a)), e));
            }

            if (v > 1f - vf)
            {
                float a = (v - (1f - vf)) / vf * Mathf.PI * 0.5f;
                return (ZFront + CapFront * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(a)), e), Mathf.Pow(Mathf.Max(0f, Mathf.Cos(a)), e));
            }

            return (Mathf.Lerp(ZBack, ZFront, (v - vb) / (1f - vb - vf)), 1f);
        }

        /// <summary>How much of the full section is left at <paramref name="z"/>: 1 along the middle, 0 at the very ends.</summary>
        public float ScaleAt(float z)
        {
            float t;
            if (z > ZFront) t = (z - ZFront) / Mathf.Max(1e-4f, CapFront);
            else if (z < ZBack) t = (ZBack - z) / Mathf.Max(1e-4f, CapBack);
            else return 1f;

            t = Mathf.Clamp01(t);
            return Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(t, CapRound)), 1f / CapRound);
        }

        Vector3 SectionAt(float z) => Section(Mathf.Clamp(z, ZBack, ZFront));

        public float Mid(float z)
        {
            Vector3 s = SectionAt(z);
            return (s.x + s.y) * 0.5f;
        }

        public float Half(float z)
        {
            Vector3 s = SectionAt(z);
            return (s.y - s.x) * 0.5f;
        }

        // --- The surface ------------------------------------------------------------------------------

        public Vector3 Point(float u, float v)
        {
            var (z, s) = Along(v);
            Vector3 sec = SectionAt(z);
            Vector2 q = Perimeter(u);
            float y = q.y * s;
            float half = (sec.y - sec.x) * 0.5f, mid = (sec.y + sec.x) * 0.5f;
            return Warp(new Vector3(q.x * s * sec.z * Tuck(y), mid + y * half, z));
        }

        public void Build(B.Model m, Func<Vector3, Vector3, Color> paint, int around = 56, int along = 64) =>
            m.Surface(paint, around, along, Point, true);

        /// <summary>The point on the <paramref name="side"/> (+1 right, -1 left) at <paramref name="z"/>, <paramref name="y"/> high. Before the warp.</summary>
        public Vector3 Side(float z, float y, float side)
        {
            float s = ScaleAt(z);
            Vector3 sec = SectionAt(z);
            float half = (sec.y - sec.x) * 0.5f, mid = (sec.y + sec.x) * 0.5f;
            float Y = Mathf.Clamp((y - mid) / half, -s * 0.999f, s * 0.999f);
            float across = s * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(Y) / Mathf.Max(s, 1e-4f), Round)), 1f / Round);
            return new Vector3(side * across * sec.z * Tuck(Y), mid + Y * half, z);
        }

        /// <summary>The point on the <paramref name="front"/> (or back) end at <paramref name="x"/>, <paramref name="y"/> high. Before the warp.</summary>
        public Vector3 End(float x, float y, bool front)
        {
            Vector3 sec = Section(front ? ZFront : ZBack);
            float half = (sec.y - sec.x) * 0.5f, mid = (sec.y + sec.x) * 0.5f;
            float Y = Mathf.Clamp((y - mid) / half, -0.999f, 0.999f);
            float X = x / (sec.z * Tuck(Y));
            float r = Mathf.Min(1f, Mathf.Pow(Mathf.Pow(Mathf.Abs(X), Round) + Mathf.Pow(Mathf.Abs(Y), Round), 1f / Round));
            float d = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(r, CapRound)), 1f / CapRound);
            return new Vector3(x, mid + Y * half, front ? ZFront + CapFront * d : ZBack - CapBack * d);
        }

        /// <summary>How far out across the end the surface reaches at <paramref name="y"/> high.</summary>
        public float EndHalfWidth(float y, bool front)
        {
            Vector3 sec = Section(front ? ZFront : ZBack);
            float Y = Mathf.Clamp((y - (sec.x + sec.y) * 0.5f) / ((sec.y - sec.x) * 0.5f), -1f, 1f);
            return sec.z * Tuck(Y) * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(Y), Round)), 1f / Round);
        }

        /// <summary>The point on top at <paramref name="x"/> across and <paramref name="z"/> along. Before the warp.</summary>
        public Vector3 Top(float x, float z)
        {
            float s = ScaleAt(z);
            Vector3 sec = SectionAt(z);
            float half = (sec.y - sec.x) * 0.5f, mid = (sec.y + sec.x) * 0.5f;
            float lo = 0f, hi = s;

            for (int i = 0; i < 32; i++)
            {
                float Y = (lo + hi) * 0.5f;
                float X = x / (sec.z * Tuck(Y));
                float f = Mathf.Pow(Mathf.Abs(X), Round) + Mathf.Pow(Y, Round) - Mathf.Pow(s, Round);
                if (f < 0f) lo = Y;
                else hi = Y;
            }

            return new Vector3(x, mid + lo * half, z);
        }

        /// <summary>The bent surface's outward normal at a point of a patch function.</summary>
        public Vector3 NormalOf(Func<float, float, Vector3> at, float a, float b)
        {
            const float e = 2e-3f;
            Vector3 da = Warp(at(a + e, b)) - Warp(at(a - e, b));
            Vector3 db = Warp(at(a, b + e)) - Warp(at(a, b - e));
            Vector3 n = Vector3.Cross(da, db);

            // Divided out by hand: Vector3.normalized gives zero for anything under 1e-5 long, and
            // a cross of two steps this small is far shorter than that.
            float length = n.magnitude;
            n = length > 1e-14f ? n / length : Vector3.up;

            Vector3 pre = at(a, b);
            float z = Mathf.Clamp(pre.z, ZBack, ZFront);
            Vector3 axis = Warp(new Vector3(0f, Mid(z), z));
            return Vector3.Dot(n, Warp(pre) - axis) < 0f ? -n : n;
        }

        // --- Patches laid on it ----------------------------------------------------------------------

        /// <summary>
        /// A patch of the surface over <paramref name="at"/>(a, b) for a and b in -1..1, its corners
        /// rounded (<paramref name="corner"/> is the superellipse power of its outline: 2 a circle,
        /// higher squarer, 0 square), lifted <paramref name="lift"/> off the body. With a
        /// <paramref name="thickness"/> it is a solid panel - a door - closed round its edge and
        /// painted <paramref name="inside"/> behind.
        /// </summary>
        public void Patch(B.Model m, Func<Vector3, Vector3, Color> paint, Func<float, float, Vector3> at, float lift, int stepsA = 16,
                          int stepsB = 8, float corner = 6f, float thickness = 0f, Func<Vector3, Vector3, Color> inside = null)
        {
            Vector3 Lifted(float s, float t, float by)
            {
                Vector2 d = RoundRect(s, t, corner);
                return Warp(at(d.x, d.y)) + NormalOf(at, d.x, d.y) * by;
            }

            Vector3 Outer(float s, float t) => Lifted(s, t, lift);
            Vector3 Inner(float s, float t) => Lifted(s, t, lift - thickness);

            Vector2 c = RoundRect(0.6f, 0.55f, corner);
            Vector3 outward = NormalOf(at, c.x, c.y);
            bool flip = FacesAway(Outer, outward);

            m.Surface(paint, stepsA, stepsB, (s, t) => Outer(flip ? 1f - s : s, t));
            if (thickness <= 0f) return;

            m.Surface(inside ?? paint, stepsA, stepsB, (s, t) => Inner(flip ? s : 1f - s, t));

            // The edge all round, outer face to inner.
            Vector3 Rim(float w, float k)
            {
                Vector2 b = Border(w);
                return Vector3.Lerp(Outer(b.x, b.y), Inner(b.x, b.y), k);
            }

            Vector3 middle = Outer(0.5f, 0.5f);
            Vector3 p0 = Rim(0.1f, 0.5f);
            Vector3 dw = Rim(0.11f, 0.5f) - p0, dk = Rim(0.1f, 0.6f) - p0;
            bool flipRim = Vector3.Dot(Vector3.Cross(dk, dw), p0 - middle) < 0f;

            m.Surface(inside ?? paint, 4 * Mathf.Max(stepsA, stepsB), 1, (w, k) => Rim(flipRim ? 1f - w : w, k), true);
        }

        static bool FacesAway(Func<float, float, Vector3> f, Vector3 outward)
        {
            Vector3 p = f(0.6f, 0.55f);
            Vector3 du = f(0.61f, 0.55f) - p, dv = f(0.6f, 0.56f) - p;
            return Vector3.Dot(Vector3.Cross(dv, du), outward) < 0f;
        }

        /// <summary>The unit square mapped onto a rounded square of superellipse power <paramref name="power"/>, in -1..1.</summary>
        static Vector2 RoundRect(float s, float t, float power)
        {
            var c = new Vector2(2f * s - 1f, 2f * t - 1f);
            if (power <= 0f) return c;

            float m = Mathf.Max(Mathf.Abs(c.x), Mathf.Abs(c.y));
            if (m < 1e-6f) return c;

            Vector2 d = c / m;
            float r = Mathf.Pow(Mathf.Pow(Mathf.Abs(d.x), power) + Mathf.Pow(Mathf.Abs(d.y), power), 1f / power);
            return c / r;
        }

        /// <summary>Round the edge of the unit square, w from 0 to 1.</summary>
        static Vector2 Border(float w)
        {
            w = Mathf.Repeat(w, 1f) * 4f;
            int side = Mathf.Min((int)w, 3);
            float f = w - side;
            switch (side)
            {
                case 0: return new Vector2(f, 0f);
                case 1: return new Vector2(1f, f);
                case 2: return new Vector2(1f - f, 1f);
                default: return new Vector2(0f, 1f - f);
            }
        }

        // --- Patch shapes -----------------------------------------------------------------------------

        /// <summary>A patch on a side over z0..z1 and y0..y1.</summary>
        public Func<float, float, Vector3> SideArea(float side, float z0, float z1, float y0, float y1) =>
            (a, b) => Side(Mathf.Lerp(z0, z1, (a + 1f) * 0.5f), Mathf.Lerp(y0, y1, (b + 1f) * 0.5f), side);

        /// <summary>A patch on an end, across <paramref name="fill"/> of its width at each height, from y0 to y1, its middle shifted by <paramref name="x"/>.</summary>
        public Func<float, float, Vector3> EndArea(bool front, float y0, float y1, float fill, float x = 0f, float width = -1f) =>
            (a, b) =>
            {
                float y = Mathf.Lerp(y0, y1, (b + 1f) * 0.5f);
                float half = width > 0f ? width * 0.5f : EndHalfWidth(y, front) * fill;
                return End(x + a * half, y, front);
            };

        /// <summary>A patch on top over x0..x1 and z0..z1.</summary>
        public Func<float, float, Vector3> TopArea(float x0, float x1, float z0, float z1) =>
            (a, b) => Top(Mathf.Lerp(x0, x1, (a + 1f) * 0.5f), Mathf.Lerp(z0, z1, (b + 1f) * 0.5f));

        /// <summary>
        /// The whole roof as a patch: down each side to <paramref name="edge"/> high, from where the
        /// back end is <paramref name="back"/> high to where the front end is <paramref name="front"/>
        /// high, narrowing with the shape into its rounded ends - a two-tone roof with a crisp edge.
        /// </summary>
        public Func<float, float, Vector3> RoofArea(float edge, float front, float back, float gap = 0.03f)
        {
            float zMid = (ZBack + ZFront) * 0.5f;
            Vector3 sec = SectionAt(zMid);
            float half = (sec.y - sec.x) * 0.5f, mid = (sec.y + sec.x) * 0.5f;
            float Y = Mathf.Clamp((edge - mid) / half, 0f, 0.999f);
            float fill = Tuck(Y) * Mathf.Pow(1f - Mathf.Pow(Y, Round), 1f / Round) / Mathf.Max(1e-4f, Tuck(0f));

            float z1 = End(0f, front, true).z - gap, z0 = End(0f, back, false).z + gap;
            return (a, b) =>
            {
                float z = Mathf.Lerp(z0, z1, (b + 1f) * 0.5f);
                return Top(a * fill * SectionAt(z).z * Tuck(0f) * ScaleAt(z), z);
            };
        }
    }
}
