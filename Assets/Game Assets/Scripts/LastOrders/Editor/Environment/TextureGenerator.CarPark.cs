using System;
using System.Collections.Generic;
using LastOrders.Core;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The car park's tarmac, painted for the one shape it is: the drive in from the gate, the lot
    /// and the corner between them as a single surface, with everything a well-kept diner car park
    /// has on it - bays marked out in hairpin pairs, arrows in the lane for the way in and the way
    /// out, a hatched box at the lane's dead end, an arrow up the drive - and the wear a car park in
    /// use picks up: tyre tracks along the line the cars actually drive, oil under each engine,
    /// patched holes, sealed and open cracks, a grate in the lane and a cover on the drive, grime
    /// along the kerbs and weeds in the gutter.
    ///
    /// Painted in world metres from the layout <see cref="CarParkModel"/> measures, so the paint is
    /// where the bays, the lane and the route nodes are. Always repainted: the layout decides it.
    /// </summary>
    public static partial class TextureGenerator
    {
        const string CarParkVersion = "carpark-1";

        /// <summary>
        /// What the car park is painted from. World metres throughout, x and y being X and Z; the
        /// texture covers exactly <see cref="Area"/>.
        /// </summary>
        public sealed class CarParkLayout
        {
            public Rect Area;

            /// <summary>The paved edge, counter-clockwise seen from above.</summary>
            public readonly List<Vector2> Outline = new List<Vector2>();

            /// <summary>Per outline segment (point i to point i + 1): whether a kerb stands on it.</summary>
            public readonly List<bool> Kerbed = new List<bool>();

            /// <summary>How far in from the edge the kerb reaches.</summary>
            public float KerbWidth = 0.2f;

            public readonly List<Vector2> BayCentres = new List<Vector2>();

            /// <summary>Each bay's forward: the way a parked car faces, toward the lane.</summary>
            public readonly List<Vector2> BayForwards = new List<Vector2>();

            /// <summary>Across, then deep.</summary>
            public Vector2 BaySize;

            public Rect Lane;
            public Vector2 GateInside, LaneEntry, LaneExit;
            public float DriveCentreX, GateZ;
        }

        // Paint a shade under white, and a muted road-marking yellow: the level's light lifts a
        // lit surface by half again, and pure white lines read as stark.
        static readonly Color LinePaint = new Color(0.8f, 0.78f, 0.71f);
        static readonly Color HatchPaint = new Color(0.75f, 0.65f, 0.35f);
        static readonly Color Tar = new Color(0.17f, 0.175f, 0.185f);

        /// <summary>Paints the car park at <paramref name="path"/> (and its normal map beside it) and returns it.</summary>
        public static Texture2D PaintCarPark(string path, CarParkLayout layout)
        {
            ContentGenerator.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));

            Rect area = layout.Area;
            const int w = 2048;
            int h = Mathf.Max(4, Mathf.RoundToInt(w * area.height / area.width / 4f) * 4);

            var rng = new DeterministicRandom(8909);
            Relief r = PaintAsphalt(w, h, area.width, area.height, false, AsphaltBase, ref rng);
            var wear = new Periodic(256, new[] { 16, 32 }, new[] { 0.6f, 0.4f }, ref rng);

            // Everything below is in the texture's own metres, from its corner.
            Vector2 O(Vector2 world) => world - area.min;
            var outline = new List<Vector2>(layout.Outline.Count);
            foreach (Vector2 p in layout.Outline) outline.Add(O(p));

            Rect lane = new Rect(layout.Lane.position - area.min, layout.Lane.size);
            Vector2 gateIn = O(layout.GateInside), laneIn = O(layout.LaneEntry), laneOut = O(layout.LaneExit);
            float driveX = layout.DriveCentreX - area.xMin, gateZ = layout.GateZ - area.yMin;

            // --- Repairs and wear ---------------------------------------------------------------

            Patch(r, new Rect(lane.xMin + lane.width * 0.42f, lane.center.y - 1.05f, 2.1f, 1.5f), ref rng);
            Patch(r, new Rect(driveX - 1.9f, gateZ + 0.7f, 1.6f, 2.2f), ref rng);

            int northEast = -1;
            for (int i = 0; i < layout.BayCentres.Count; i++)
            {
                if (layout.BayForwards[i].y >= 0f) continue;
                if (northEast < 0 || layout.BayCentres[i].x > layout.BayCentres[northEast].x) northEast = i;
            }

            if (northEast >= 0)
            {
                Vector2 c = O(layout.BayCentres[northEast]);
                Patch(r, new Rect(c.x - 0.4f, c.y - 0.3f, 1.3f, 1.8f), ref rng);
            }

            // Tyre tracks: the line the cars drive, a wheel's width either side of it.
            var inPath = new List<Vector2> { new Vector2(driveX, gateZ), gateIn, laneIn, new Vector2(lane.xMin + 3.5f, laneIn.y - 0.2f) };
            var outPath = new List<Vector2> { new Vector2(lane.xMin + 3.5f, laneOut.y + 0.2f), laneOut, gateIn, new Vector2(driveX, gateZ) };
            Tracks(r, inPath, 0.78f, 0.07f);
            Tracks(r, outPath, 0.78f, 0.06f);
            Tracks(r, new List<Vector2> { new Vector2(lane.xMin + 0.5f, lane.center.y), new Vector2(lane.xMax - 1.8f, lane.center.y) }, 0.9f, 0.035f);

            // Oil under the engines: bays face the lane, so the engine is at the lane end.
            for (int i = 0; i < layout.BayCentres.Count; i++)
            {
                if (BuildingKit.Hash(i * 31 + 7) % 4 == 0) continue;

                Vector2 c = O(layout.BayCentres[i]) + layout.BayForwards[i] * (layout.BaySize.y * 0.5f - 1.2f);
                c += new Vector2(rng.Range(-0.25f, 0.25f), rng.Range(-0.2f, 0.2f));
                r.Stain(c.x, c.y, rng.Range(0.38f, 0.5f), rng.Range(0.26f, 0.34f), rng.Range(0f, Mathf.PI), AsphaltBase * 0.72f, 0.45f, 0.8f);
                r.Stain(c.x + rng.Range(-0.08f, 0.08f), c.y + rng.Range(-0.06f, 0.06f), rng.Range(0.14f, 0.2f), rng.Range(0.1f, 0.15f),
                        rng.Range(0f, Mathf.PI), AsphaltBase * 0.56f, 0.5f, 0.7f);
            }

            Grate(r, new Vector2(lane.xMin + lane.width * 0.63f, lane.center.y));
            Manhole(r, new Vector2(driveX + 1.35f, gateZ + 4.7f));

            // Cracks the owner has sealed with tar, wandering lines a shade darker; then a few they have not.
            for (int i = 0; i < 8; i++)
            {
                Vector2 p = Vector2.zero;
                bool found = false;
                for (int attempt = 0; attempt < 30 && !found; attempt++)
                {
                    p = new Vector2(rng.Range(0.5f, area.width - 0.5f), rng.Range(0.5f, area.height - 0.5f));
                    found = Inside(outline, p) && DistanceToEdge(outline, p) > 0.6f;
                }

                if (found) Sealant(r, p, rng.Range(0f, Mathf.PI * 2f), rng.Range(9, 16), ref rng);
            }

            PaintCracks(r, 4, AsphaltBase * 0.72f, ref rng);

            // --- Markings ----------------------------------------------------------------------------

            Bays(r, layout, outline, wear);

            Arrow(r, new Vector2(lane.xMin + 1.0f, laneIn.y), new Vector2(lane.xMin + 3.3f, laneIn.y), LinePaint, 0.88f, wear);
            Arrow(r, new Vector2(lane.xMin + 3.3f, laneOut.y), new Vector2(lane.xMin + 1.0f, laneOut.y), LinePaint, 0.88f, wear);
            Arrow(r, new Vector2(driveX, gateZ + 1.3f), new Vector2(driveX, gateZ + 3.6f), LinePaint, 0.85f, wear);

            Hatch(r, Rect.MinMaxRect(lane.xMax - 1.65f, lane.yMin + 0.3f, lane.xMax - layout.KerbWidth - 0.18f, lane.yMax - 0.3f), wear);

            // --- The edges -----------------------------------------------------------------------------

            KerbGrime(r, outline, layout.Kerbed, layout.KerbWidth);
            Weeds(r, outline, layout.Kerbed, layout.KerbWidth, ref rng);

            return SaveRelief(path, r, 1f, TextureWrapMode.Clamp, TextureWrapMode.Clamp, CarParkVersion);
        }

        // --- Wear -----------------------------------------------------------------------------------------

        /// <summary>A patched hole: fresher, darker tarmac in a rectangle, its seam sealed in a groove round it.</summary>
        static void Patch(Relief r, Rect patch, ref DeterministicRandom rng)
        {
            Color fresh = AsphaltBase * rng.Range(0.93f, 0.96f);
            PaintWhere(r, patch, p => RectDistance(p, patch, 0.04f), fresh, 0.85f, null, 1f);

            for (int i = 0; i < 30; i++)
            {
                float radius = rng.Range(0.02f, 0.04f);
                r.Mound(rng.Range(patch.xMin + 0.05f, patch.xMax - 0.05f), rng.Range(patch.yMin + 0.05f, patch.yMax - 0.05f), radius,
                        radius * rng.Range(0.7f, 1f), radius * 0.2f, fresh * rng.Range(1.04f, 1.1f), soft: radius * 0.05f);
            }

            Color seam = AsphaltBase * 0.68f;
            Vector2 a = patch.min, b = new Vector2(patch.xMax, patch.yMin), c = patch.max, d = new Vector2(patch.xMin, patch.yMax);
            r.Groove(a, b, 0.026f, 0.002f, seam, 0.38f);
            r.Groove(b, c, 0.026f, 0.002f, seam, 0.38f);
            r.Groove(c, d, 0.026f, 0.002f, seam, 0.38f);
            r.Groove(d, a, 0.026f, 0.002f, seam, 0.38f);
        }

        /// <summary>Two soft darker bands, a wheel's width either side of <paramref name="path"/>.</summary>
        static void Tracks(Relief r, IList<Vector2> path, float halfTrack, float opacity)
        {
            for (int i = 0; i + 1 < path.Count; i++)
            {
                Vector2 a = path[i], b = path[i + 1];
                float length = Vector2.Distance(a, b);
                if (length < 1e-3f) continue;

                Vector2 dir = (b - a) / length;
                Vector2 side = new Vector2(-dir.y, dir.x) * halfTrack;
                float angle = Mathf.Atan2(dir.y, dir.x);

                for (float s = 0f; s <= length; s += 0.12f)
                {
                    Vector2 p = a + dir * s;
                    foreach (Vector2 wheel in new[] { p + side, p - side })
                        r.Stain(wheel.x, wheel.y, 0.34f, 0.2f, angle, AsphaltBase * 0.78f, opacity, 1f);
                }
            }
        }

        /// <summary>A drain grate set in the lane: an iron frame, its slots dark, the tarmac round it damp.</summary>
        static void Grate(Relief r, Vector2 at)
        {
            r.Stain(at.x, at.y, 0.75f, 0.6f, 0f, AsphaltBase * 0.82f, 0.35f, 1f);

            var frame = new Rect(at.x - 0.32f, at.y - 0.22f, 0.64f, 0.44f);
            PaintWhere(r, frame, p => RectDistance(p, frame, 0.02f), Hex("3E4042"), 1f, null, 1f);

            for (int k = -3; k <= 3; k++)
            {
                float x = at.x + k * 0.075f;
                r.Groove(new Vector2(x, at.y - 0.15f), new Vector2(x, at.y + 0.15f), 0.032f, 0.006f, Hex("1B1C1E"), 0.9f);
            }
        }

        /// <summary>A round iron cover on the drive, a ring groove round its rim and a hatch of grooves across it.</summary>
        static void Manhole(Relief r, Vector2 at)
        {
            const float radius = 0.34f;
            PaintWhere(r, new Rect(at.x - radius, at.y - radius, radius * 2f, radius * 2f), p => Vector2.Distance(p, at) - radius,
                       Hex("4A4B4D"), 1f, null, 1f);

            const int steps = 24;
            for (int i = 0; i < steps; i++)
            {
                float a0 = i * Mathf.PI * 2f / steps, a1 = (i + 1) * Mathf.PI * 2f / steps;
                r.Groove(at + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * (radius - 0.04f),
                         at + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * (radius - 0.04f), 0.02f, 0.004f, Hex("2A2B2D"), 0.8f);
            }

            for (int k = -2; k <= 2; k++)
            {
                float o = k * 0.09f;
                float half = Mathf.Sqrt(Mathf.Max(0f, (radius - 0.08f) * (radius - 0.08f) - o * o));
                r.Groove(at + new Vector2(o, -half), at + new Vector2(o, half), 0.016f, 0.003f, Hex("2E2F31"), 0.7f);
                r.Groove(at + new Vector2(-half, o), at + new Vector2(half, o), 0.016f, 0.003f, Hex("2E2F31"), 0.7f);
            }
        }

        /// <summary>A crack run over with tar: a wandering line a shade darker than the tarmac, forking once.</summary>
        static void Sealant(Relief r, Vector2 start, float angle, int steps, ref DeterministicRandom rng)
        {
            Vector2 p = start;
            for (int s = 0; s < steps; s++)
            {
                angle += rng.Range(-0.5f, 0.5f);
                Vector2 next = p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rng.Range(0.12f, 0.24f);
                Vector2 a = p, b = next;
                PaintWhere(r, Bounds(a, b, 0.05f), q => SegmentDistance(q, a, b) - 0.022f, Tar, 0.62f, null, 1f);

                if (s == steps / 2)
                {
                    float fork = angle + rng.Range(0.6f, 1.1f) * (rng.Chance(0.5f) ? 1f : -1f);
                    Vector2 end = p + new Vector2(Mathf.Cos(fork), Mathf.Sin(fork)) * rng.Range(0.3f, 0.5f);
                    PaintWhere(r, Bounds(p, end, 0.05f), q => SegmentDistance(q, p, end) - 0.018f, Tar, 0.55f, null, 1f);
                }

                p = next;
            }
        }

        // --- Markings ----------------------------------------------------------------------------------

        /// <summary>
        /// The bays' side lines as hairpins - two lines joined in a round end at the lane - one pair
        /// on every line two bays share and on each open side, none where a kerb already is. They
        /// start a little in from the kerb at the back of the bay, where the wheel stops stand.
        /// </summary>
        static void Bays(Relief r, CarParkLayout layout, List<Vector2> outline, Periodic wear)
        {
            var drawn = new List<Vector2>();
            float half = layout.BaySize.x * 0.5f, depth = layout.BaySize.y * 0.5f;
            const float gap = 0.13f, width = 0.1f;

            for (int i = 0; i < layout.BayCentres.Count; i++)
            {
                Vector2 c = layout.BayCentres[i] - layout.Area.min;
                Vector2 f = layout.BayForwards[i];
                Vector2 right = new Vector2(f.y, -f.x);

                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2 mid = c + right * (side * half);
                    if (drawn.Exists(d => Vector2.Distance(d, mid) < 0.2f)) continue;
                    drawn.Add(mid);

                    if (DistanceToEdge(outline, mid) < 0.5f) continue;

                    Vector2 rear = mid - f * depth, front = mid + f * depth;
                    float clear = DistanceToEdge(outline, rear);
                    rear += f * Mathf.Max(0f, layout.KerbWidth + 0.15f - clear);

                    Vector2 cap = front - f * gap;
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Vector2 a = rear + right * (s * gap), b = cap + right * (s * gap);
                        PaintWhere(r, Bounds(a, b, 0.1f), p => SegmentDistance(p, a, b) - width * 0.5f, LinePaint, 0.92f, wear, 0.85f);
                    }

                    // The round end: a half ring beyond the cap's centre, toward the lane.
                    PaintWhere(r, Bounds(cap, cap, gap + 0.1f), p =>
                    {
                        Vector2 d = p - cap;
                        float ring = Mathf.Abs(d.magnitude - gap) - width * 0.5f;
                        float beyond = -Vector2.Dot(d, f);
                        return Mathf.Max(ring, beyond);
                    }, LinePaint, 0.92f, wear, 0.85f);
                }
            }
        }

        /// <summary>A lane arrow from <paramref name="tail"/> to <paramref name="tip"/>: a round-ended shaft and a broad head.</summary>
        static void Arrow(Relief r, Vector2 tail, Vector2 tip, Color colour, float opacity, Periodic wear)
        {
            Vector2 dir = (tip - tail).normalized;
            Vector2 side = new Vector2(-dir.y, dir.x);
            const float headLength = 0.72f, headHalf = 0.4f, shaftHalf = 0.11f;

            Vector2 neck = tip - dir * headLength;
            PaintWhere(r, Bounds(tail, neck, 0.2f), p => SegmentDistance(p, tail, neck + dir * 0.05f) - shaftHalf, colour, opacity, wear, 0.85f);

            Vector2 a = tip, b = neck + side * headHalf, c = neck - side * headHalf;
            PaintWhere(r, Bounds(b, c, 0.1f, a), p => TriangleDistance(p, a, b, c), colour, opacity, wear, 0.85f);
        }

        /// <summary>A keep-clear box: a yellow outline with diagonal stripes across it.</summary>
        static void Hatch(Relief r, Rect box, Periodic wear)
        {
            const float line = 0.08f, stripe = 0.13f, spacing = 0.4f;
            Rect outer = box;
            Rect inner = Rect.MinMaxRect(box.xMin + line, box.yMin + line, box.xMax - line, box.yMax - line);

            PaintWhere(r, outer, p => Mathf.Max(RectDistance(p, outer, 0f), -RectDistance(p, inner, 0f)), HatchPaint, 0.85f, wear, 0.8f);
            PaintWhere(r, inner, p =>
            {
                float s = (p.x + p.y) * 0.70710678f;
                float d = Mathf.Abs(s - Mathf.Round(s / spacing) * spacing) - stripe * 0.5f;
                return Mathf.Max(d, RectDistance(p, inner, 0f));
            }, HatchPaint, 0.85f, wear, 0.8f);
        }

        // --- The edges ---------------------------------------------------------------------------------

        /// <summary>The dark line of grit that collects along the foot of a kerb, fading out onto the tarmac.</summary>
        static void KerbGrime(Relief r, List<Vector2> outline, List<bool> kerbed, float kerbWidth)
        {
            const float reach = 0.3f;
            var amount = new float[r.W * r.H];

            for (int i = 0; i < outline.Count; i++)
            {
                if (i >= kerbed.Count || !kerbed[i]) continue;

                Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
                float length = Vector2.Distance(a, b);
                if (length < 1e-4f) continue;

                Vector2 dir = (b - a) / length, inward = new Vector2(-dir.y, dir.x);
                Rect box = Bounds(a, b, kerbWidth + reach + 0.05f);

                ForEachPixel(r, box, (x, y, p) =>
                {
                    float t = Vector2.Dot(p - a, dir);
                    if (t < -0.1f || t > length + 0.1f) return;

                    float d = Vector2.Dot(p - a, inward) - kerbWidth;
                    if (d < -0.03f || d > reach) return;

                    float k = 1f - Mathf.Clamp01(d / reach);
                    int index = y * r.W + x;
                    amount[index] = Mathf.Max(amount[index], k * k * 0.32f);
                });
            }

            Color grime = AsphaltBase * 0.6f;
            for (int i = 0; i < amount.Length; i++)
            {
                if (amount[i] <= 0f) continue;
                Color c = Color.Lerp(r.Albedo[i], grime, amount[i]);
                r.Albedo[i] = new Color(c.r, c.g, c.b, 1f);
            }
        }

        /// <summary>Little tufts of green in the gutter along the kerbs, in clumps here and there.</summary>
        static void Weeds(Relief r, List<Vector2> outline, List<bool> kerbed, float kerbWidth, ref DeterministicRandom rng)
        {
            Color[] greens = { Hex("5B7034"), Hex("6A8040"), Hex("4E6230"), Hex("74883F") };

            for (int i = 0; i < outline.Count; i++)
            {
                if (i >= kerbed.Count || !kerbed[i]) continue;

                Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
                float length = Vector2.Distance(a, b);
                if (length < 1e-4f) continue;

                Vector2 dir = (b - a) / length, inward = new Vector2(-dir.y, dir.x);
                for (float s = 0.15f; s < length - 0.15f; s += 0.32f)
                {
                    if (!rng.Chance(0.28f)) continue;

                    int tufts = rng.Range(2, 6);
                    for (int k = 0; k < tufts; k++)
                    {
                        Vector2 p = a + dir * (s + rng.Range(-0.1f, 0.1f)) + inward * (kerbWidth + rng.Range(0.01f, 0.07f));
                        float radius = rng.Range(0.018f, 0.04f);
                        r.Mound(p.x, p.y, radius, radius * rng.Range(0.6f, 1f), 0.006f, greens[rng.Range(0, greens.Length)],
                                r.HeightAt(p.x, p.y), rng.Range(0f, Mathf.PI));
                    }
                }
            }
        }

        // --- Shapes ----------------------------------------------------------------------------------------

        /// <summary>
        /// Paints <paramref name="colour"/> wherever <paramref name="distance"/> (a signed distance in
        /// metres, negative inside) is below zero inside <paramref name="box"/>, anti-aliased at the
        /// edge, worn thin in places by <paramref name="wear"/> when given - <paramref name="keep"/> is
        /// how much of the paint survives.
        /// </summary>
        static void PaintWhere(Relief r, Rect box, Func<Vector2, float> distance, Color colour, float opacity, Periodic wear, float keep)
        {
            float ppm = (r.PpmU + r.PpmV) * 0.5f;

            ForEachPixel(r, box, (x, y, p) =>
            {
                float cover = Mathf.Clamp01(0.5f - distance(p) * ppm);
                if (cover <= 0f) return;

                float a = cover * opacity;
                if (wear != null)
                {
                    float n = wear.Sample(x * 256 / r.W, y * 256 / r.H);
                    a *= Mathf.Lerp(0.35f, 1f, Band(n, 1f - keep, 0.03f));
                }

                int i = y * r.W + x;
                Color o = r.Albedo[i];
                r.Albedo[i] = new Color(o.r + (colour.r - o.r) * a, o.g + (colour.g - o.g) * a, o.b + (colour.b - o.b) * a, 1f);
            });
        }

        /// <summary>Calls <paramref name="paint"/> for every pixel over <paramref name="box"/> (metres), with the pixel's centre in metres.</summary>
        static void ForEachPixel(Relief r, Rect box, Action<int, int, Vector2> paint)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(box.xMin * r.PpmU) - 1), x1 = Mathf.Min(r.W - 1, Mathf.CeilToInt(box.xMax * r.PpmU) + 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(box.yMin * r.PpmV) - 1), y1 = Mathf.Min(r.H - 1, Mathf.CeilToInt(box.yMax * r.PpmV) + 1);

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    paint(x, y, new Vector2((x + 0.5f) / r.PpmU, (y + 0.5f) / r.PpmV));
        }

        static Rect Bounds(Vector2 a, Vector2 b, float pad, Vector2? c = null)
        {
            Vector2 min = Vector2.Min(a, b), max = Vector2.Max(a, b);
            if (c.HasValue)
            {
                min = Vector2.Min(min, c.Value);
                max = Vector2.Max(max, c.Value);
            }

            return Rect.MinMaxRect(min.x - pad, min.y - pad, max.x + pad, max.y + pad);
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Signed distance to a rectangle with corners rounded by <paramref name="corner"/>.</summary>
        static float RectDistance(Vector2 p, Rect rect, float corner)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - rect.center.x) - (rect.width * 0.5f - corner),
                                    Mathf.Abs(p.y - rect.center.y) - (rect.height * 0.5f - corner));
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - corner;
        }

        /// <summary>Signed distance to a triangle, near enough at its edges for anti-aliasing.</summary>
        static float TriangleDistance(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float winding = Mathf.Sign((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x));
            float Edge(Vector2 from, Vector2 to)
            {
                Vector2 e = (to - from).normalized;
                Vector2 outward = new Vector2(e.y, -e.x) * winding;
                return Vector2.Dot(p - from, outward);
            }

            return Mathf.Max(Edge(a, b), Mathf.Max(Edge(b, c), Edge(c, a)));
        }

        static float DistanceToEdge(List<Vector2> outline, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < outline.Count; i++)
                best = Mathf.Min(best, SegmentDistance(p, outline[i], outline[(i + 1) % outline.Count]));
            return best;
        }

        static bool Inside(List<Vector2> outline, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
            {
                Vector2 a = outline[i], b = outline[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }

            return inside;
        }
    }
}
