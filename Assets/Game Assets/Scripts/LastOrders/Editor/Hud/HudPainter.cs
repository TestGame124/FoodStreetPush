using System;
using UnityEngine;

namespace LastOrders.EditorTools.Hud
{
    /// <summary>A signed distance in design units: negative inside the shape, positive outside.</summary>
    public delegate float Sdf(Vector2 p);

    /// <summary>
    /// How a filled part is lit: a soft bevel worked out from the shape's own distance field, so a
    /// flat outline comes out looking like the pillowy, top-left-lit produce in the item icons
    /// (<see cref="CropIcons"/>) rather than like clip art.
    /// </summary>
    public struct Shading
    {
        public Color Base;

        /// <summary>Design units in from the edge over which the surface rolls over.</summary>
        public float Bevel;

        /// <summary>How strongly the rolled edge catches and loses the light.</summary>
        public float Relief;

        /// <summary>Brightness multipliers at the part's top and bottom, for a gentle vertical fall-off.</summary>
        public float Top, Bottom;

        public float Spec;

        public static Shading Of(Color colour, float bevel, float relief = 0.85f, float top = 1.08f,
                                 float bottom = 0.9f, float spec = 0.22f) =>
            new Shading { Base = colour, Bevel = bevel, Relief = relief, Top = top, Bottom = bottom, Spec = spec };
    }

    /// <summary>
    /// A small raster canvas for the HUD art: shapes are distance fields in design units, and every
    /// edge is anti-aliased analytically from the distance, so one definition bakes cleanly at any
    /// size.
    ///
    /// Design space runs top-down like SVG - (0,0) is the top left - because that is how the icons
    /// are drawn out on paper; the flip to Unity's bottom-up rows happens once, in <see cref="Blit"/>.
    /// </summary>
    public sealed class HudPainter
    {
        public readonly int Width, Height;

        readonly Color[] pixels;

        /// <summary>Pixels per design unit.</summary>
        readonly float scale;

        /// <summary>The light, in design space (y down): from the top left and in front.</summary>
        static readonly Vector3 Light = new Vector3(-0.42f, -0.72f, 0.56f).normalized;

        public HudPainter(int width, int height, float unitsAcross)
        {
            Width = width;
            Height = height;
            scale = width / unitsAcross;
            pixels = new Color[width * height];
        }

        /// <summary>The painter's design units across one pixel.</summary>
        public float Pixel => 1f / scale;

        Vector2 Point(int x, int row) => new Vector2((x + 0.5f) / scale, (row + 0.5f) / scale);

        float Coverage(float distance) => Mathf.Clamp01(0.5f - distance * scale);

        // --- Fills ----------------------------------------------------------------------------------

        public void Fill(Sdf shape, Color colour, Sdf clip = null) =>
            Fill(shape, _ => colour, clip);

        public void Fill(Sdf shape, Func<Vector2, Color> paint, Sdf clip = null)
        {
            for (int row = 0; row < Height; row++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Vector2 p = Point(x, row);
                    float a = Coverage(shape(p));
                    if (clip != null && a > 0f) a *= Coverage(clip(p));
                    if (a <= 0f) continue;

                    Color c = paint(p);
                    Over(row * Width + x, c, a * c.a);
                }
            }
        }

        /// <summary>A vertical gradient between two design-space heights.</summary>
        public void Gradient(Sdf shape, float y0, Color top, float y1, Color bottom, Sdf clip = null) =>
            Fill(shape, p => Color.Lerp(top, bottom, Mathf.InverseLerp(y0, y1, p.y)), clip);

        /// <summary>
        /// A lit part. The surface normal is tipped outwards near the edge in proportion to how close
        /// the edge is, which reads as a rounded, moulded object under a top-left key light.
        /// </summary>
        public void Shade(Sdf shape, Shading s, float y0, float y1, Sdf clip = null)
        {
            float e = 0.35f / scale;

            for (int row = 0; row < Height; row++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Vector2 p = Point(x, row);
                    float d = shape(p);
                    float a = Coverage(d);
                    if (clip != null && a > 0f) a *= Coverage(clip(p));
                    if (a <= 0f) continue;

                    var g = new Vector2(shape(p + new Vector2(e, 0f)) - shape(p - new Vector2(e, 0f)),
                                        shape(p + new Vector2(0f, e)) - shape(p - new Vector2(0f, e)));
                    if (g.sqrMagnitude > 1e-12f) g.Normalize();

                    float depth = s.Bevel <= 0f ? 1f : Mathf.Clamp01(-d / s.Bevel);
                    float tilt = 1f - depth;
                    tilt = tilt * tilt * (3f - 2f * tilt);

                    var n = new Vector3(g.x * tilt * 1.6f, g.y * tilt * 1.6f, 1f).normalized;
                    float lit = Vector3.Dot(n, Light);
                    float shade = 1f + (lit - Light.z) * s.Relief;
                    float fall = Mathf.Lerp(s.Top, s.Bottom, Mathf.InverseLerp(y0, y1, p.y));

                    Color c = s.Base * (shade * fall);

                    Vector3 h = (Light + Vector3.forward).normalized;
                    float spec = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(n, h)), 28f) * s.Spec * tilt;
                    c += new Color(spec, spec, spec, 0f);

                    c.a = s.Base.a;
                    Over(row * Width + x, c, a * c.a);
                }
            }
        }

        /// <summary>A soft shadow: the shape offset and feathered over <paramref name="blur"/> units.</summary>
        public void Shadow(Sdf shape, Color colour, float blur, Vector2 offset)
        {
            for (int row = 0; row < Height; row++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float d = shape(Point(x, row) - offset);
                    float a = 1f - Smooth(-blur, blur, d);
                    if (a <= 0f) continue;

                    Over(row * Width + x, colour, a * colour.a);
                }
            }
        }

        /// <summary>A glow or vignette: any per-point colour with its own alpha, no shape edge.</summary>
        public void Paint(Func<Vector2, Color> paint)
        {
            for (int row = 0; row < Height; row++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Color c = paint(Point(x, row));
                    if (c.a <= 0f) continue;
                    Over(row * Width + x, c, c.a);
                }
            }
        }

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Straight-alpha "over".</summary>
        void Over(int i, Color c, float a)
        {
            if (a <= 0f) return;
            a = Mathf.Min(1f, a);

            Color d = pixels[i];
            float outA = a + d.a * (1f - a);
            if (outA <= 0f) return;

            float k = d.a * (1f - a);
            pixels[i] = new Color((c.r * a + d.r * k) / outA,
                                  (c.g * a + d.g * k) / outA,
                                  (c.b * a + d.b * k) / outA,
                                  outA);
        }

        /// <summary>Copies the canvas into an atlas at a pixel offset, flipping to bottom-up rows.</summary>
        public void Blit(Color[] atlas, int atlasWidth, int ox, int oy)
        {
            for (int row = 0; row < Height; row++)
            {
                int ay = oy + (Height - 1 - row);
                for (int x = 0; x < Width; x++)
                {
                    Color c = pixels[row * Width + x];
                    c.r = Mathf.Clamp01(c.r);
                    c.g = Mathf.Clamp01(c.g);
                    c.b = Mathf.Clamp01(c.b);
                    atlas[ay * atlasWidth + ox + x] = c;
                }
            }
        }
    }

    /// <summary>Distance-field primitives and operators, after Inigo Quilez's 2D catalogue.</summary>
    public static class Shape
    {
        public static Sdf Circle(Vector2 c, float r) => p => (p - c).magnitude - r;

        public static Sdf Circle(float x, float y, float r) => Circle(new Vector2(x, y), r);

        /// <summary>A rounded box from its corners, in design units.</summary>
        public static Sdf Box(float x0, float y0, float x1, float y1, float radius = 0f)
        {
            var c = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
            var b = new Vector2((x1 - x0) * 0.5f, (y1 - y0) * 0.5f);
            radius = Mathf.Min(radius, Mathf.Min(b.x, b.y));
            return p => RoundBox(p - c, b, radius);
        }

        /// <summary>A rounded box about a centre, turned by <paramref name="degrees"/>.</summary>
        public static Sdf Box(Vector2 c, Vector2 half, float radius, float degrees)
        {
            float rad = -degrees * Mathf.Deg2Rad;
            float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
            radius = Mathf.Min(radius, Mathf.Min(half.x, half.y));
            return p =>
            {
                Vector2 q = p - c;
                q = new Vector2(q.x * cs - q.y * sn, q.x * sn + q.y * cs);
                return RoundBox(q, half, radius);
            };
        }

        static float RoundBox(Vector2 p, Vector2 b, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x) - b.x + r, Mathf.Abs(p.y) - b.y + r);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        public static Sdf Capsule(Vector2 a, Vector2 b, float r) => p =>
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        };

        public static Sdf Capsule(float ax, float ay, float bx, float by, float r) =>
            Capsule(new Vector2(ax, ay), new Vector2(bx, by), r);

        /// <summary>An approximate ellipse distance; exact enough for edges a pixel or two wide.</summary>
        public static Sdf Ellipse(Vector2 c, Vector2 radii, float degrees = 0f)
        {
            float rad = -degrees * Mathf.Deg2Rad;
            float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
            return p =>
            {
                Vector2 q = p - c;
                q = new Vector2(q.x * cs - q.y * sn, q.x * sn + q.y * cs);
                float k0 = new Vector2(q.x / radii.x, q.y / radii.y).magnitude;
                float k1 = new Vector2(q.x / (radii.x * radii.x), q.y / (radii.y * radii.y)).magnitude;
                return k1 <= 1e-6f ? -Mathf.Min(radii.x, radii.y) : k0 * (k0 - 1f) / k1;
            };
        }

        public static Sdf Polygon(params Vector2[] v) => p =>
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i];
                Vector2 w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));

                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }

            return s * Mathf.Sqrt(d);
        };

        /// <summary>A polygon from flat (x, y) pairs.</summary>
        public static Sdf Poly(params float[] xy)
        {
            var v = new Vector2[xy.Length / 2];
            for (int i = 0; i < v.Length; i++) v[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return Polygon(v);
        }

        /// <summary>A star with <paramref name="points"/> points, the first pointing straight up.</summary>
        public static Sdf Star(Vector2 c, float outer, float inner, int points = 5, float degrees = 0f)
        {
            var v = new Vector2[points * 2];
            for (int i = 0; i < v.Length; i++)
            {
                float r = i % 2 == 0 ? outer : inner;
                float a = (degrees - 90f + i * 180f / points) * Mathf.Deg2Rad;
                v[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }

            return Polygon(v);
        }

        /// <summary>A regular polygon, the first corner at <paramref name="degrees"/> from straight up.</summary>
        public static Sdf Regular(Vector2 c, float r, int sides, float degrees = 0f)
        {
            var v = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = (degrees - 90f + i * 360f / sides) * Mathf.Deg2Rad;
                v[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }

            return Polygon(v);
        }

        public static Sdf Union(params Sdf[] parts) => p =>
        {
            float d = float.MaxValue;
            for (int i = 0; i < parts.Length; i++) d = Mathf.Min(d, parts[i](p));
            return d;
        };

        public static Sdf Intersect(Sdf a, Sdf b) => p => Mathf.Max(a(p), b(p));

        public static Sdf Subtract(Sdf a, Sdf b) => p => Mathf.Max(a(p), -b(p));

        /// <summary>Grows a shape outwards (positive) or shrinks it (negative); also rounds corners.</summary>
        public static Sdf Grow(Sdf a, float amount) => p => a(p) - amount;

        /// <summary>A line of width <paramref name="width"/> running along the shape's edge.</summary>
        public static Sdf Edge(Sdf a, float width) => p => Mathf.Abs(a(p)) - width * 0.5f;

        public static Sdf Move(Sdf a, float dx, float dy) => p => a(p - new Vector2(dx, dy));

        /// <summary>Everything above (smaller y than) a design-space height.</summary>
        public static Sdf Above(float y) => p => p.y - y;

        public static Sdf Below(float y) => p => y - p.y;

        public static Sdf LeftOf(float x) => p => p.x - x;

        public static Sdf RightOf(float x) => p => x - p.x;
    }
}
