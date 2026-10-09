using System;
using System.Collections.Generic;
using LastOrders.Core;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Points spread over an area the way things lie in the world: never on top of one another,
    /// never in rows, closer together in some places than others.
    ///
    /// Bridson's Poisson-disk sampling, with a spacing that may vary across the area. Two points
    /// may be no closer than the mean of their two spacings, so a dense patch blends into a sparse
    /// one rather than stopping at a line. A rejection test keeps the points off anything that
    /// has to stay clear, and several seeds are dropped in at the start so an area cut in pieces
    /// by roads is still filled in every piece.
    ///
    /// Driven by <see cref="DeterministicRandom"/>, so one seed is one layout, every time.
    /// </summary>
    public static class PoissonScatter
    {
        /// <param name="area">Where points may fall.</param>
        /// <param name="minSpacing">The smallest spacing <paramref name="spacingAt"/> will ever return.</param>
        /// <param name="maxSpacing">The largest.</param>
        /// <param name="spacingAt">The spacing wanted at a point, in metres.</param>
        /// <param name="accept">False for a point that must not be used.</param>
        public static List<Vector2> Sample(Rect area, float minSpacing, float maxSpacing,
                                           Func<Vector2, float> spacingAt, Func<Vector2, bool> accept,
                                           ref DeterministicRandom rng, int maxPoints = int.MaxValue,
                                           int attempts = 24)
        {
            var points = new List<Vector2>();
            var spacings = new List<float>();
            if (area.width <= 0f || area.height <= 0f || minSpacing <= 0f) return points;

            maxSpacing = Mathf.Max(maxSpacing, minSpacing);

            float cell = minSpacing / Mathf.Sqrt(2f);
            int cols = Mathf.Max(1, Mathf.CeilToInt(area.width / cell));
            int rows = Mathf.Max(1, Mathf.CeilToInt(area.height / cell));

            // Each cell holds at most one point, because no two points are closer than a cell's
            // diagonal. Index + 1, so zero means empty.
            var grid = new int[cols * rows];
            int reach = Mathf.CeilToInt(maxSpacing / cell);

            var active = new List<int>();

            bool Fits(Vector2 p, float spacing)
            {
                int cx = Mathf.Clamp((int)((p.x - area.xMin) / cell), 0, cols - 1);
                int cy = Mathf.Clamp((int)((p.y - area.yMin) / cell), 0, rows - 1);

                for (int y = Mathf.Max(0, cy - reach); y <= Mathf.Min(rows - 1, cy + reach); y++)
                {
                    for (int x = Mathf.Max(0, cx - reach); x <= Mathf.Min(cols - 1, cx + reach); x++)
                    {
                        int held = grid[y * cols + x] - 1;
                        if (held < 0) continue;

                        float needed = (spacing + spacings[held]) * 0.5f;
                        if ((points[held] - p).sqrMagnitude < needed * needed) return false;
                    }
                }

                return true;
            }

            void Add(Vector2 p, float spacing)
            {
                int cx = Mathf.Clamp((int)((p.x - area.xMin) / cell), 0, cols - 1);
                int cy = Mathf.Clamp((int)((p.y - area.yMin) / cell), 0, rows - 1);

                points.Add(p);
                spacings.Add(spacing);
                grid[cy * cols + cx] = points.Count;
                active.Add(points.Count - 1);
            }

            float Spacing(Vector2 p) => Mathf.Clamp(spacingAt != null ? spacingAt(p) : minSpacing, minSpacing, maxSpacing);

            // Seeds: one try per hundred-odd square metres. Growth only hops a couple of spacings,
            // so a strip the roads and trails have cut off from the rest is filled only if a seed
            // lands in it - and with too few tries, whole sides of the map came out empty.
            int seeds = Mathf.Clamp(Mathf.CeilToInt(area.width * area.height / 120f), 8, 2000);
            for (int i = 0; i < seeds && points.Count < maxPoints; i++)
            {
                var p = new Vector2(rng.Range(area.xMin, area.xMax), rng.Range(area.yMin, area.yMax));
                if (accept != null && !accept(p)) continue;

                float s = Spacing(p);
                if (Fits(p, s)) Add(p, s);
            }

            while (active.Count > 0 && points.Count < maxPoints)
            {
                int slot = rng.Range(0, active.Count);
                int index = active[slot];
                Vector2 origin = points[index];
                float spacing = spacings[index];

                bool placed = false;
                for (int k = 0; k < attempts; k++)
                {
                    float angle = rng.Range(0f, Mathf.PI * 2f);
                    float distance = rng.Range(spacing, spacing * 2f);
                    Vector2 p = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

                    if (!area.Contains(p)) continue;
                    if (accept != null && !accept(p)) continue;

                    float s = Spacing(p);
                    if (!Fits(p, s)) continue;

                    Add(p, s);
                    placed = true;
                    break;
                }

                if (!placed)
                {
                    active[slot] = active[active.Count - 1];
                    active.RemoveAt(active.Count - 1);
                }
            }

            return points;
        }

        /// <summary>Smooth noise in 0..1, offset by a seed so two layouts do not share their clumps.</summary>
        public static float Noise(Vector2 p, float scale, int seed)
        {
            float ox = (seed % 997) * 13.37f;
            float oy = (seed % 991) * 7.13f;
            return Mathf.PerlinNoise(p.x * scale + ox, p.y * scale + oy);
        }
    }
}
