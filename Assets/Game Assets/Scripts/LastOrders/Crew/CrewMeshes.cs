using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.Crew
{
    /// <summary>
    /// The little things the crew handle - a cloth, a broom, a feather duster - and the mess a meal
    /// leaves on a table, made in code as flat-shaded, vertex-coloured low-poly meshes in the diner's
    /// palette (honey wood, teal, cream, red). Built once and shared.
    ///
    /// Drawn with the toon shader's vertex paint (<see cref="Paint"/>), like the decorations.
    /// </summary>
    public static class CrewMeshes
    {
        static readonly Color Wood = new Color(0.66f, 0.45f, 0.25f);
        static readonly Color WoodDark = new Color(0.45f, 0.29f, 0.16f);
        static readonly Color Teal = new Color(0.22f, 0.6f, 0.6f);
        static readonly Color TealLight = new Color(0.45f, 0.78f, 0.76f);
        static readonly Color Cream = new Color(0.97f, 0.93f, 0.82f);
        static readonly Color Red = new Color(0.8f, 0.25f, 0.2f);
        static readonly Color Straw = new Color(0.88f, 0.74f, 0.42f);
        static readonly Color StrawDark = new Color(0.72f, 0.56f, 0.28f);
        static readonly Color Pink = new Color(0.96f, 0.72f, 0.74f);
        static readonly Color Crumb = new Color(0.78f, 0.56f, 0.3f);
        static readonly Color Sauce = new Color(0.72f, 0.3f, 0.18f);

        static Mesh cloth, broom, duster, ringGood, ringPlain;
        static Mesh[] mess;

        /// <summary>A flat ring on the floor, 0.55 m across: teal where a drop would do something, cream where it would not.</summary>
        public static Mesh Ring(bool good)
        {
            Mesh cached = good ? ringGood : ringPlain;
            if (cached != null) return cached;

            var b = new Builder();
            Color colour = good ? TealLight : Cream;
            const int sides = 28;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                var o0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var o1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                b.Flat(o0 * 0.42f, o1 * 0.42f, o1 * 0.55f, o0 * 0.55f, colour);
            }

            Mesh mesh = b.Mesh(good ? "Crew_RingGood" : "Crew_Ring");
            if (good) ringGood = mesh;
            else ringPlain = mesh;
            return mesh;
        }
        static Material paint;

        /// <summary>Draws the props and the mess with <paramref name="material"/> from now on - the setup's
        /// vertex-painted toon material, rather than a runtime copy of the shader.</summary>
        public static void UsePaint(Material material)
        {
            if (material != null) paint = material;
        }

        /// <summary>The toon shader with vertex paint on: the material given to <see cref="UsePaint"/>, or a
        /// runtime copy of the shader when none was.</summary>
        public static Material Paint
        {
            get
            {
                if (paint != null) return paint;

                Shader shader = Shader.Find("LastOrders/Toon");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                paint = new Material(shader) { name = "CrewPaint (runtime)" };
                if (paint.HasProperty("_VertexPaint")) paint.SetFloat("_VertexPaint", 1f);
                return paint;
            }
        }

        /// <summary>A folded rag, 22 x 17 cm - big enough to show past a cartoon hand - held flat under the palm.</summary>
        public static Mesh Cloth
        {
            get
            {
                if (cloth != null) return cloth;

                var b = new Builder();
                b.Box(new Vector3(0f, 0f, 0f), new Vector3(0.22f, 0.024f, 0.17f), Quaternion.Euler(0f, 0f, 3f), Teal);
                b.Box(new Vector3(0.03f, -0.018f, -0.01f), new Vector3(0.16f, 0.016f, 0.13f), Quaternion.Euler(2f, 12f, -4f), TealLight);
                b.Box(new Vector3(-0.08f, -0.014f, 0f), new Vector3(0.026f, 0.014f, 0.172f), Quaternion.Euler(0f, 0f, 3f), Cream);
                return cloth = b.Mesh("Crew_Cloth");
            }
        }

        /// <summary>A broom standing on its head at the origin, its handle up +Y to 1.25 m.</summary>
        public static Mesh Broom
        {
            get
            {
                if (broom != null) return broom;

                var b = new Builder();
                b.Cylinder(new Vector3(0f, 0.11f, 0f), new Vector3(0f, 1.25f, 0f), 0.016f, 7, Wood);
                b.Cylinder(new Vector3(0f, 1.2f, 0f), new Vector3(0f, 1.26f, 0f), 0.02f, 7, Red);
                b.Box(new Vector3(0f, 0.1f, 0f), new Vector3(0.3f, 0.05f, 0.07f), Quaternion.identity, Red);
                b.Box(new Vector3(0f, 0.135f, 0f), new Vector3(0.22f, 0.025f, 0.05f), Quaternion.identity, WoodDark);
                // Bristles: a fan, wider at the floor.
                b.Taper(new Vector3(0f, 0.075f, 0f), new Vector3(0.3f, 0.065f), new Vector3(0.36f, 0.08f), 0.075f, Straw, StrawDark);
                return broom = b.Mesh("Crew_Broom");
            }
        }

        /// <summary>A feather duster held at the origin, its plume along +Y.</summary>
        public static Mesh Duster
        {
            get
            {
                if (duster != null) return duster;

                var b = new Builder();
                b.Cylinder(new Vector3(0f, -0.06f, 0f), new Vector3(0f, 0.2f, 0f), 0.013f, 6, WoodDark);
                b.Cylinder(new Vector3(0f, 0.19f, 0f), new Vector3(0f, 0.22f, 0f), 0.02f, 6, Red);
                b.Blob(new Vector3(0f, 0.3f, 0f), new Vector3(0.09f, 0.11f, 0.09f), Pink, 3);
                b.Blob(new Vector3(0.03f, 0.37f, 0.01f), new Vector3(0.06f, 0.07f, 0.06f), Cream, 7);
                b.Blob(new Vector3(-0.03f, 0.25f, -0.02f), new Vector3(0.06f, 0.06f, 0.06f), Cream, 11);
                return duster = b.Mesh("Crew_Duster");
            }
        }

        /// <summary>What a meal leaves behind, on the table top at the origin: crumbs, a screwed-up napkin, a smear.</summary>
        public static Mesh Mess(int variant)
        {
            if (mess == null) mess = new Mesh[3];
            variant = Mathf.Abs(variant) % mess.Length;
            if (mess[variant] != null) return mess[variant];

            var random = new System.Random(17 + variant * 31);
            float R(float min, float max) => min + (float)random.NextDouble() * (max - min);

            var b = new Builder();

            // A smear where the plate was.
            b.Disc(new Vector3(R(-0.03f, 0.03f), 0.002f, R(-0.03f, 0.03f)), R(0.07f, 0.1f), 9, variant == 1 ? Sauce : Crumb, 0.003f);

            // Crumbs scattered round it.
            int crumbs = 7 + variant * 2;
            for (int i = 0; i < crumbs; i++)
            {
                float angle = R(0f, Mathf.PI * 2f);
                float reach = R(0.03f, 0.15f);
                var at = new Vector3(Mathf.Cos(angle) * reach, 0.008f, Mathf.Sin(angle) * reach);
                float size = R(0.012f, 0.022f);
                b.Box(at, new Vector3(size, size * 0.7f, size * R(0.7f, 1.2f)), Quaternion.Euler(R(0, 40), R(0, 360), R(0, 40)),
                      Color.Lerp(Crumb, WoodDark, R(0f, 0.5f)));
            }

            // The napkin, screwed up and dropped.
            b.Blob(new Vector3(R(0.08f, 0.13f), 0.03f, R(-0.06f, 0.06f)), new Vector3(0.045f, 0.032f, 0.04f), Cream, 3 + variant);
            if (variant == 2) b.Blob(new Vector3(-0.1f, 0.022f, 0.05f), new Vector3(0.03f, 0.022f, 0.03f), Cream, 13);

            return mess[variant] = b.Mesh("Crew_Mess_" + variant);
        }

        /// <summary>Flat-shaded triangles with a colour per face.</summary>
        sealed class Builder
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Color> colours = new List<Color>();
            readonly List<int> triangles = new List<int>();

            void Tri(Vector3 a, Vector3 b, Vector3 c, Color colour)
            {
                int i = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);

                // A little light from above baked in, so faces read apart even in flat light.
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                Color shaded = colour * (0.86f + 0.14f * Mathf.Clamp01(normal.y * 0.5f + 0.5f));
                shaded.a = 1f;
                colours.Add(shaded);
                colours.Add(shaded);
                colours.Add(shaded);

                triangles.Add(i);
                triangles.Add(i + 1);
                triangles.Add(i + 2);
            }

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colour)
            {
                Tri(a, b, c, colour);
                Tri(a, c, d, colour);
            }

            /// <summary>A quad on the ground plane, facing up whichever way its corners run.</summary>
            public void Flat(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colour)
            {
                if (Vector3.Cross(b - a, c - a).y >= 0f) Quad(a, b, c, d, colour);
                else Quad(d, c, b, a, colour);
            }

            public void Box(Vector3 centre, Vector3 size, Quaternion turn, Color colour)
            {
                Vector3 h = size * 0.5f;
                Vector3 P(float x, float y, float z) => centre + turn * new Vector3(x * h.x, y * h.y, z * h.z);

                Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), colour);
                Quad(P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), colour);
                Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), colour);
                Quad(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1), colour);
                Quad(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), colour);
                Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), colour);
            }

            public void Cylinder(Vector3 from, Vector3 to, float radius, int sides, Color colour)
            {
                Vector3 axis = (to - from).normalized;
                Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 other = Vector3.Cross(axis, side);

                for (int i = 0; i < sides; i++)
                {
                    float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                    Vector3 r0 = (side * Mathf.Cos(a0) + other * Mathf.Sin(a0)) * radius;
                    Vector3 r1 = (side * Mathf.Cos(a1) + other * Mathf.Sin(a1)) * radius;

                    Quad(from + r0, from + r1, to + r1, to + r0, colour);
                    Tri(to, to + r0, to + r1, colour);
                    Tri(from, from + r1, from + r0, colour);
                }
            }

            /// <summary>A block wider at the bottom than the top: <paramref name="top"/> and <paramref name="bottom"/> are (width, depth).</summary>
            public void Taper(Vector3 topCentre, Vector2 top, Vector2 bottom, float height, Color colour, Color tips)
            {
                Vector3 T(float x, float z) => topCentre + new Vector3(x * top.x * 0.5f, 0f, z * top.y * 0.5f);
                Vector3 B(float x, float z) => topCentre + new Vector3(x * bottom.x * 0.5f, -height, z * bottom.y * 0.5f);

                Quad(T(1, -1), B(1, -1), B(-1, -1), T(-1, -1), colour);
                Quad(T(-1, 1), B(-1, 1), B(1, 1), T(1, 1), colour);
                Quad(T(-1, -1), B(-1, -1), B(-1, 1), T(-1, 1), colour);
                Quad(T(1, 1), B(1, 1), B(1, -1), T(1, -1), colour);
                Quad(B(1, -1), B(1, 1), B(-1, 1), B(-1, -1), tips);
            }

            /// <summary>A lumpy ball - a screwed-up napkin, a puff of feathers.</summary>
            public void Blob(Vector3 centre, Vector3 radii, Color colour, int seed)
            {
                var random = new System.Random(seed);
                const int rings = 4, segments = 6;
                var points = new Vector3[rings + 1, segments];

                for (int r = 0; r <= rings; r++)
                {
                    float v = r / (float)rings * Mathf.PI;
                    for (int s = 0; s < segments; s++)
                    {
                        float u = s / (float)segments * Mathf.PI * 2f + (r % 2) * 0.4f;
                        float lump = 0.8f + 0.4f * (float)random.NextDouble();
                        var p = new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u)) * lump;
                        points[r, s] = centre + Vector3.Scale(p, radii);
                    }
                }

                for (int r = 0; r < rings; r++)
                {
                    for (int s = 0; s < segments; s++)
                    {
                        int n = (s + 1) % segments;
                        Color shade = Color.Lerp(colour, Color.white, (r + s) % 3 == 0 ? 0.08f : 0f);
                        Tri(points[r, s], points[r, n], points[r + 1, n], shade);
                        Tri(points[r, s], points[r + 1, n], points[r + 1, s], shade);
                    }
                }
            }

            /// <summary>A thin flat disc lying on the ground plane.</summary>
            public void Disc(Vector3 centre, float radius, int sides, Color colour, float thickness)
            {
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                    float w0 = 0.8f + 0.2f * Mathf.Sin(a0 * 3f), w1 = 0.8f + 0.2f * Mathf.Sin(a1 * 3f);
                    Vector3 p0 = centre + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius * w0;
                    Vector3 p1 = centre + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius * w1;
                    Vector3 up = Vector3.up * thickness;
                    Tri(centre + up, p1 + up, p0 + up, colour);
                }
            }

            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetColors(colours);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                return mesh;
            }
        }
    }
}
