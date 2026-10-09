using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The compound's buildings - the shelter hut, the storehouse - modelled in code in the game's
    /// smooth low-poly style: chunky timber boards and posts rounded on every edge, corrugated tin
    /// roofs whose ridges catch the light, like the diner furniture and the plant beds.
    ///
    /// Colour is painted into the vertices, as on the plant beds, and lit by LastOrders/Stylized, so
    /// a whole building is one material: each board its own shade of weathered timber, darker where the walls
    /// meet the ground and up under the eaves, rust running down a tin roof from its fixings. A second
    /// submesh holds what glows - a lantern's glass - drawn with the warm unlit material the shelter's
    /// windows light up with.
    ///
    /// The palette: weathered grey-brown timber, darker trim, painted doors, blue and grey tin, grey
    /// stone - neutral wood with the colour in the paintwork, so no building reads orange. Painted a
    /// little darker than it shows, as the level's other surfaces are: the sun and the sky lift a lit
    /// face by nearly half again.
    /// </summary>
    public static class BuildingKit
    {
        /// <summary>The buildings' meshes and their material, in the art style's own folder.</summary>
        public const string Folder = "Assets/Game Assets/Content/LastOrders/Buildings/" + EnvKit.Style;

        const string MaterialPath = Folder + "/Building_" + EnvKit.Style + ".mat";

        /// <summary>Warm and unlit: the shelter's windows at night and the lanterns' glass.</summary>
        public const string GlowMaterialPath = "Assets/Game Assets/Content/LastOrders/Materials/Shelter_WindowLit.mat";

        static readonly Color GlowColour = new Color(1f, 0.84f, 0.48f);

        // --- The palette ---------------------------------------------------------------------------

        /// <summary>Planks, each picked from these by where it is, so a wall reads as boards of one timber weathered unevenly.</summary>
        public static readonly Color[] Planks = { Hex("8F7660"), Hex("85705B"), Hex("977E67"), Hex("7F6A56"), Hex("9C846C"), Hex("7A6452") };

        public static readonly Color Walnut = Hex("5A4434");
        public static readonly Color WalnutDark = Hex("42332A");
        public static readonly Color HoneyLight = Hex("AE9A80");
        public static readonly Color DoorWood = Hex("6E5A4A");
        public static readonly Color Iron = Hex("3A3A3C");
        public static readonly Color Stone = Hex("949390");
        public static readonly Color Glass = Hex("2C3846");
        public static readonly Color GlassSky = Hex("566B7E");
        public static readonly Color Rust = Hex("85614C");
        public static readonly Color Twine = Hex("CCC4AE");
        public static readonly Color Leaf = Hex("5D7B2F");
        public static readonly Color LeafLight = Hex("7F9A3A");
        public static readonly Color[] Flowers = { Hex("E6C23C"), Hex("E8E6D6"), Hex("E2802F") };

        /// <summary>The shelter's roof: painted blue tin, sheets of it weathered a little differently.</summary>
        public static readonly Color[] RoofTin = { Hex("5C7A93"), Hex("557189"), Hex("63829B") };

        /// <summary>The storehouse's roof: galvanised tin. Painted grey: facing the sun, any lighter clips.</summary>
        public static readonly Color[] Tin = { Hex("A2A7AA"), Hex("9AA0A4"), Hex("A9AEB1") };

        public static readonly Color TinPatch = Hex("7C8083");

        /// <summary>A plank's colour, picked by <paramref name="key"/> so the same board is the same shade every build.</summary>
        public static Color Plank(int key) => Planks[Hash(key) % Planks.Length];

        /// <summary>A stable hash of a small integer, for picking shades.</summary>
        public static int Hash(int key)
        {
            unchecked
            {
                uint h = (uint)key * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (int)(h & 0x7fffffff);
            }
        }

        /// <summary>-1..1, stable for <paramref name="key"/>.</summary>
        public static float Jitter(int key) => (Hash(key) % 2001) / 1000f - 1f;

        public static Color Hex(string rgb) => PrimitiveKit.Hex(rgb);

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // --- Materials and assets ------------------------------------------------------------------

        /// <summary>LastOrders/Stylized in white, so the vertices' paint is the colour; made if missing and set every time.</summary>
        public static Material PaintedMaterial()
        {
            ContentGenerator.EnsureFolder(Folder);
            Shader shader = Shader.Find("LastOrders/Stylized");
            if (shader == null) throw new InvalidOperationException("Shader LastOrders/Stylized is missing.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Building_" + EnvKit.Style };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_ShadeColor", new Color(0.55f, 0.6f, 0.74f));
            material.SetFloat("_Wrap", 0.55f);
            material.SetFloat("_LightStrength", 0.88f);
            material.SetFloat("_AmbientStrength", 0.62f);
            material.SetFloat("_ShadowStrength", 0.8f);
            material.SetFloat("_RimStrength", 0.08f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        /// <summary>Unlit, so a lit window or a lantern reads at night however dark the scene is.</summary>
        public static Material GlowMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
            if (material != null) return material;

            ContentGenerator.EnsureFolder(System.IO.Path.GetDirectoryName(GlowMaterialPath).Replace('\\', '/'));

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", GlowColour);
            material.color = GlowColour;

            AssetDatabase.CreateAsset(material, GlowMaterialPath);
            return material;
        }

        /// <summary>The materials for a model's submeshes: painted, then glowing if it has a glow.</summary>
        public static Material[] MaterialsFor(Mesh mesh) =>
            mesh.subMeshCount > 1 ? new[] { PaintedMaterial(), GlowMaterial() } : new[] { PaintedMaterial() };

        /// <summary>
        /// Puts a mesh on <paramref name="go"/>, adding the filter and renderer if it has none, with
        /// the materials its submeshes need.
        /// </summary>
        public static void Show(GameObject go, Mesh mesh, Material[] materials = null)
        {
            // Not ??: a missing component comes back as Unity's fake null, which ?? takes for an object.
            var filter = go.GetComponent<MeshFilter>();
            if (filter == null) filter = go.AddComponent<MeshFilter>();
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = go.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            renderer.sharedMaterials = materials ?? MaterialsFor(mesh);
        }

        // --- The model -----------------------------------------------------------------------------

        /// <summary>
        /// A building being put together: shapes from <see cref="SmoothMesh"/>, each painted as it is
        /// added, in two submeshes - <see cref="Painted"/> and <see cref="Glow"/>.
        /// </summary>
        public sealed class Model
        {
            public const int Painted = 0, Glow = 1;

            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Color32> colours = new List<Color32>();
            readonly List<int>[] triangles = { new List<int>(), new List<int>() };

            /// <summary>
            /// The painter's shading over the whole building, multiplying every painted colour at a
            /// point with a normal: darker at the foot of a wall, up under the eaves, in a corner.
            /// </summary>
            public Func<Vector3, Vector3, float> Shade = (p, n) => 1f;

            /// <summary>Applied to everything added, for building a part off to one side of where it is drawn.</summary>
            public Matrix4x4 Transform = Matrix4x4.identity;

            /// <summary>
            /// Optional. Bends every point added, before <see cref="Transform"/>: a rounded box tapered
            /// at the top into a car's cabin. Normals are bent with it. Paint still sees the part as built.
            /// </summary>
            public Func<Vector3, Vector3> Warp;

            public int VertexCount => vertices.Count;

            public void Add(SmoothMesh shape, Func<Vector3, Vector3, Color> paint, int sub = Painted)
            {
                Mesh part = shape.Build("part", out List<int> used);
                try
                {
                    if (used.Count == 0) return;

                    Matrix4x4 normalMatrix = Transform.inverse.transpose;
                    int start = vertices.Count;
                    Vector3[] v = part.vertices, n = part.normals;
                    for (int i = 0; i < v.Length; i++)
                    {
                        Vector3 bent = Warp != null ? Warp(v[i]) : v[i];
                        Vector3 bentNormal = Warp != null ? WarpNormal(v[i], n[i]) : n[i];
                        Vector3 p = Transform.MultiplyPoint3x4(bent);
                        Vector3 normal = normalMatrix.MultiplyVector(bentNormal).normalized;
                        vertices.Add(p);
                        normals.Add(normal);

                        // Painted in the part's own space, so a colour pattern moves with the part.
                        Color c = paint(v[i], n[i]);
                        if (sub == Painted) c *= Shade(p, normal);
                        c.a = 1f;
                        colours.Add(c);
                    }

                    for (int s = 0; s < part.subMeshCount; s++)
                        foreach (int index in part.GetTriangles(s)) triangles[sub].Add(start + index);
                }
                finally
                {
                    Object.DestroyImmediate(part);
                }
            }

            /// <summary>A normal carried through <see cref="Warp"/>: by the inverse transpose of its slope at the point.</summary>
            Vector3 WarpNormal(Vector3 at, Vector3 normal)
            {
                const float e = 1e-3f;
                Vector3 dx = (Warp(at + Vector3.right * e) - Warp(at - Vector3.right * e)) / (2f * e);
                Vector3 dy = (Warp(at + Vector3.up * e) - Warp(at - Vector3.up * e)) / (2f * e);
                Vector3 dz = (Warp(at + Vector3.forward * e) - Warp(at - Vector3.forward * e)) / (2f * e);

                var slope = Matrix4x4.identity;
                slope.SetColumn(0, dx);
                slope.SetColumn(1, dy);
                slope.SetColumn(2, dz);
                return slope.inverse.transpose.MultiplyVector(normal).normalized;
            }

            public void Add(SmoothMesh shape, Color colour, int sub = Painted) => Add(shape, (p, n) => colour, sub);

            /// <summary>A board, a post, a block: a box with every edge rounded.</summary>
            public void Box(Color colour, Vector3 centre, Vector3 size, float radius, Quaternion rotation, int segments = 1,
                            int sub = Painted)
            {
                var shape = new SmoothMesh(1);
                shape.RoundedBox(0, centre, size, radius, rotation, segments);
                Add(shape, colour, sub);
            }

            public void Box(Color colour, Vector3 centre, Vector3 size, float radius, int segments = 1) =>
                Box(colour, centre, size, radius, Quaternion.identity, segments);

            /// <summary>A round post, pipe or log from <paramref name="from"/> to <paramref name="to"/>.</summary>
            public void Tube(Func<Vector3, Vector3, Color> paint, Vector3 from, Vector3 to, float radiusFrom, float radiusTo,
                             int segments = 12, int sub = Painted)
            {
                var shape = new SmoothMesh(1);
                shape.Tube(0, from, to, radiusFrom, radiusTo, segments);
                Add(shape, paint, sub);
            }

            public void Tube(Color colour, Vector3 from, Vector3 to, float radius, int segments = 12) =>
                Tube((p, n) => colour, from, to, radius, radius, segments);

            /// <summary>A turned shape round the Y axis through <paramref name="centre"/>: a barrel, a lantern's cap.</summary>
            public void Lathe(Func<Vector3, Vector3, Color> paint, Vector3 centre, IList<Vector2> profile, int segments,
                              int sub = Painted)
            {
                var shape = new SmoothMesh(1);
                var subs = new int[Mathf.Max(1, profile.Count - 1)];
                shape.Lathe(centre, profile, subs, segments);
                Add(shape, paint, sub);
            }

            /// <summary>Any swept surface, see <see cref="SmoothMesh.Surface"/>.</summary>
            public void Surface(Func<Vector3, Vector3, Color> paint, int stepsU, int stepsV, Func<float, float, Vector3> at,
                                bool wrapU = false, int sub = Painted)
            {
                var shape = new SmoothMesh(1);
                shape.Surface(0, stepsU, stepsV, at, wrapU);
                Add(shape, paint, sub);
            }

            /// <summary>A surface raised over a rectangle, see <see cref="SmoothMesh.Heightfield"/>.</summary>
            public void Heightfield(Func<Vector3, Vector3, Color> paint, Rect area, int steps, Func<float, float, float> height)
            {
                var shape = new SmoothMesh(1);
                shape.Heightfield(0, area, steps, height);
                Add(shape, paint);
            }

            /// <summary>
            /// A sheet of corrugated tin with its ridges running down it: <paramref name="width"/>
            /// across from <paramref name="corner"/> along <paramref name="across"/>, <paramref name="length"/>
            /// down along <paramref name="down"/>, waves <paramref name="pitch"/> apart and
            /// <paramref name="depth"/> deep on top of a sheet <paramref name="thickness"/> thick. Its top
            /// faces cross(down, across). <paramref name="paint"/> is given how far across and how far down
            /// a point is, in metres. Rows of vertices run across it every <paramref name="rowStep"/> down
            /// it: paint only changes at a row, so a stripe painted across a sheet needs them closer than
            /// the stripe is wide.
            /// </summary>
            public void Corrugated(Vector3 corner, Vector3 across, Vector3 down, float width, float length, float pitch, float depth,
                                   float thickness, Func<float, float, Color> paint, float rowStep = 0.2f)
            {
                across = across.normalized;
                down = down.normalized;
                Vector3 up = Vector3.Cross(down, across).normalized;

                int waves = Mathf.Max(1, Mathf.RoundToInt(width / pitch));
                int stepsU = waves * 8;
                int stepsV = Mathf.Max(2, Mathf.CeilToInt(length / rowStep));

                // Troughs at both edges, so a sheet laid beside another meets it trough to trough.
                float Wave(float u) => depth * 0.5f * (1f - Mathf.Cos(u * waves * Mathf.PI * 2f));
                Vector3 Top(float u, float v) => corner + across * (u * width) + down * (v * length) + up * Wave(u);
                Vector3 Bottom(float u, float v) => corner + across * (u * width) + down * (v * length) - up * thickness;

                Color Paint(Vector3 p, Vector3 n)
                {
                    Vector3 d = p - corner;
                    return paint(Mathf.Clamp(Vector3.Dot(d, across), 0f, width), Mathf.Clamp(Vector3.Dot(d, down), 0f, length));
                }

                Color Under(Vector3 p, Vector3 n) => Paint(p, n) * 0.72f;

                Surface(Paint, stepsU, stepsV, Top);
                Surface(Under, stepsV, 1, (a, b) => Bottom(b, a));
                Surface(Paint, stepsU, 1, (u, t) => Vector3.Lerp(Top(u, 1f), Bottom(u, 1f), t));
                Surface(Paint, stepsU, 1, (u, t) => Vector3.Lerp(Bottom(u, 0f), Top(u, 0f), t));
                Surface(Paint, stepsV, 1, (v, t) => Vector3.Lerp(Top(0f, v), Bottom(0f, v), t));
                Surface(Paint, stepsV, 1, (v, t) => Vector3.Lerp(Bottom(1f, v), Top(1f, v), t));
            }

            /// <summary>
            /// A wall of planks laid level and lapped like clapboard, each lower edge kicked out over
            /// the board below. The wall's face is at <paramref name="origin"/> + <paramref name="outward"/>
            /// * 0, running <paramref name="from"/> to <paramref name="to"/> metres along <paramref name="along"/>,
            /// <paramref name="bottom"/> to <paramref name="top"/> high. <paramref name="gaps"/> leaves
            /// openings (x0, x1 along it, up to height y) - a doorway. <paramref name="key"/> seeds the shades.
            /// </summary>
            public void LapWall(Vector3 origin, Vector3 along, Vector3 outward, float from, float to, float bottom, float top,
                                int rows, int key, IList<Vector3> gaps = null)
            {
                Quaternion face = Quaternion.LookRotation(outward, Vector3.up);
                Quaternion lap = face * Quaternion.Euler(-4f, 0f, 0f);
                float rowHeight = (top - bottom) / rows;

                for (int r = 0; r < rows; r++)
                {
                    float y0 = bottom + r * rowHeight;
                    var spans = new List<Vector2> { new Vector2(from, to) };

                    if (gaps != null)
                    {
                        foreach (Vector3 gap in gaps)
                        {
                            if (y0 >= gap.z) continue;
                            var cut = new List<Vector2>();
                            foreach (Vector2 s in spans)
                            {
                                if (gap.y <= s.x || gap.x >= s.y) { cut.Add(s); continue; }
                                if (gap.x > s.x) cut.Add(new Vector2(s.x, gap.x));
                                if (gap.y < s.y) cut.Add(new Vector2(gap.y, s.y));
                            }

                            spans = cut;
                        }
                    }

                    for (int s = 0; s < spans.Count; s++)
                    {
                        float a = spans[s].x, b = spans[s].y;
                        if (b - a < 0.05f) continue;

                        Vector3 centre = origin + along * ((a + b) * 0.5f) + Vector3.up * (y0 + rowHeight * 0.5f) + outward * 0.022f;
                        Color c = Plank(key * 131 + r * 7 + s) * (1f + 0.03f * Jitter(key * 17 + r));
                        Box(c, centre, new Vector3(b - a, rowHeight + 0.018f, 0.045f), 0.014f, lap, 1);
                    }
                }
            }

            /// <summary>
            /// Upright planks side by side, as on a shed or a gable: <paramref name="from"/> to
            /// <paramref name="to"/> along <paramref name="along"/>, each from <paramref name="bottom"/>
            /// up to <paramref name="topAt"/>(its middle along the wall), a hairline apart.
            /// </summary>
            public void BoardWall(Vector3 origin, Vector3 along, Vector3 outward, float from, float to, float bottom,
                                  Func<float, float> topAt, float boardWidth, int key, IList<Vector3> gaps = null)
            {
                Quaternion face = Quaternion.LookRotation(outward, Vector3.up);
                int count = Mathf.Max(1, Mathf.RoundToInt((to - from) / boardWidth));
                float w = (to - from) / count;

                for (int i = 0; i < count; i++)
                {
                    float a = from + i * w, b = a + w;
                    float mid = (a + b) * 0.5f;
                    float top = topAt(mid);
                    float low = bottom;

                    if (gaps != null)
                        foreach (Vector3 gap in gaps)
                            if (mid > gap.x && mid < gap.y) low = Mathf.Max(low, gap.z);

                    if (top - low < 0.04f) continue;

                    // Every board a little proud of or behind its neighbours, as boards nailed up by hand are.
                    float proud = 0.006f * Jitter(key * 37 + i);
                    Vector3 centre = origin + along * mid + Vector3.up * ((low + top) * 0.5f) + outward * (0.022f + proud);
                    Color c = Plank(key * 113 + i) * (1f + 0.04f * Jitter(key * 59 + i));
                    Box(c, centre, new Vector3(w - 0.012f, top - low, 0.045f), 0.012f, face, 1);
                }
            }

            /// <summary>A wall lantern: an iron arm out from the wall at <paramref name="at"/>, a cap, a frame and glowing glass.</summary>
            public void Lantern(Vector3 at, Vector3 outward)
            {
                Quaternion face = Quaternion.LookRotation(outward, Vector3.up);
                Vector3 hang = at + outward * 0.14f;

                Box(Iron, at + outward * 0.012f, new Vector3(0.08f, 0.14f, 0.024f), 0.008f, face);
                Box(Iron, at + outward * 0.07f + Vector3.up * 0.08f, new Vector3(0.02f, 0.02f, 0.13f), 0.006f, face);
                Tube(Iron, hang + Vector3.up * 0.08f, hang + Vector3.up * 0.045f, 0.006f, 6);

                Lathe((p, n) => Iron, hang + Vector3.up * 0.035f, new List<Vector2>
                {
                    new Vector2(0f, -0.005f), new Vector2(0.058f, -0.005f), new Vector2(0.058f, -0.005f), new Vector2(0.03f, 0.022f),
                    new Vector2(0.01f, 0.03f), new Vector2(0f, 0.032f),
                }, 10);

                Lathe((p, n) => Iron, hang - Vector3.up * 0.1f, new List<Vector2>
                {
                    new Vector2(0f, -0.012f), new Vector2(0.045f, -0.012f), new Vector2(0.045f, -0.012f), new Vector2(0.045f, 0.004f),
                    new Vector2(0.045f, 0.004f), new Vector2(0f, 0.004f),
                }, 10);

                var glass = new SmoothMesh(1);
                glass.RoundedBox(0, hang - Vector3.up * 0.035f, new Vector3(0.07f, 0.12f, 0.07f), 0.016f, face, 2);
                Add(glass, Color.white, Glow);

                for (int k = 0; k < 4; k++)
                {
                    float a = (k + 0.5f) * Mathf.PI * 0.5f;
                    Vector3 corner = hang + face * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.05f;
                    Tube(Iron, corner - Vector3.up * 0.1f, corner + Vector3.up * 0.03f, 0.005f, 5);
                }
            }

            /// <summary>The model as a mesh, two submeshes if anything glows.</summary>
            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetColors(colours);

                bool glows = triangles[Glow].Count > 0;
                mesh.subMeshCount = glows ? 2 : 1;
                mesh.SetTriangles(triangles[Painted], 0);
                if (glows) mesh.SetTriangles(triangles[Glow], 1);
                mesh.RecalculateBounds();
                return mesh;
            }

            /// <summary>
            /// Saves the model at <paramref name="path"/> - into the mesh already there when there is
            /// one, so everything pointing at it keeps pointing at it.
            /// </summary>
            public Mesh Save(string path, string name)
            {
                ContentGenerator.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
                Mesh built = Build(name);

                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(built, path);
                    AssetDatabase.SaveAssetIfDirty(built);
                    return built;
                }

                // Through the mesh API, not CopySerialized: that copies the data but leaves the GPU
                // drawing the old vertices with the new triangles until the editor restarts.
                existing.Clear();
                existing.name = name;
                existing.indexFormat = built.indexFormat;
                existing.SetVertices(built.vertices);
                existing.SetNormals(built.normals);
                existing.SetColors(built.colors32);
                existing.subMeshCount = built.subMeshCount;
                for (int s = 0; s < built.subMeshCount; s++) existing.SetTriangles(built.GetTriangles(s), s);
                existing.RecalculateBounds();

                Object.DestroyImmediate(built);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
                return existing;
            }
        }

        // --- Shared painting -----------------------------------------------------------------------

        /// <summary>
        /// The shade a painter lays on a small timber building: darker toward the ground over
        /// <paramref name="footReach"/>, and darker up under the eaves at <paramref name="eave"/> on
        /// the walls inside <paramref name="footprint"/> (half sizes, x and z), where the roof's
        /// overhang keeps the light off.
        /// </summary>
        public static Func<Vector3, Vector3, float> TimberShade(Vector2 footprint, float eave, float footReach = 0.5f)
        {
            return (p, n) =>
            {
                float k = Mathf.Lerp(0.74f, 1f, Smooth(p.y / footReach));

                // The walls only: a roof sheet out over the eave is in the light.
                bool underRoof = Mathf.Abs(n.y) < 0.5f && Mathf.Abs(p.x) < footprint.x + 0.12f && Mathf.Abs(p.z) < footprint.y + 0.12f &&
                                 p.y < eave + 0.05f;
                if (underRoof) k *= Mathf.Lerp(1f, 0.82f, Smooth((p.y - (eave - 0.45f)) / 0.45f));

                return k;
            };
        }

        /// <summary>A small wooden barrel, its staves banded with iron, standing at <paramref name="foot"/>.</summary>
        public static void Barrel(Model m, Vector3 foot, float radius, float height, Color staves, Color top)
        {
            var profile = new List<Vector2>();
            const int rings = 10;
            profile.Add(new Vector2(0f, 0f));
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings;
                profile.Add(new Vector2(radius * (0.86f + 0.14f * Mathf.Sin(t * Mathf.PI)), t * height));
            }

            profile.Add(new Vector2(radius * 0.86f, height));
            profile.Add(new Vector2(radius * 0.8f, height));
            profile.Add(new Vector2(radius * 0.8f, height));
            profile.Add(new Vector2(0f, height - 0.02f));

            m.Lathe((p, n) =>
            {
                float y = p.y - foot.y;
                float across = new Vector2(p.x - foot.x, p.z - foot.z).magnitude;
                if (n.y > 0.7f && y > height * 0.5f && across < radius * 0.8f + 0.002f) return top;
                float t = y / height;
                bool hoop = Mathf.Abs(t - 0.18f) < 0.045f || Mathf.Abs(t - 0.82f) < 0.045f;
                // Staves: a shade apart round the barrel.
                float a = Mathf.Atan2(p.z - foot.z, p.x - foot.x);
                float stave = 1f + 0.05f * Mathf.Sign(Mathf.Sin(a * 9f));
                return hoop ? Iron : staves * stave;
            }, foot, profile, 18);
        }

        /// <summary>A crate of slats round a dark inside, its lid on: <paramref name="size"/>, standing at <paramref name="foot"/>.</summary>
        public static void Crate(Model m, Vector3 foot, Vector3 size, float yaw, int key)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 h = size * 0.5f;
            Color wood = HoneyLight * (1f + 0.05f * Jitter(key));
            Color dark = Walnut;

            m.Box(dark, foot + turn * new Vector3(0f, h.y, 0f), size - new Vector3(0.03f, 0.03f, 0.03f), 0.01f, turn);

            // Three slats a side, front and back.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = 0; k < 3; k++)
                {
                    float y = size.y * (k + 0.5f) / 3f;
                    Color c = wood * (1f + 0.04f * Jitter(key * 7 + k + side * 3));
                    m.Box(c, foot + turn * new Vector3(0f, y, side * h.z), new Vector3(size.x, size.y / 3f - 0.02f, 0.03f), 0.008f, turn);
                    m.Box(c, foot + turn * new Vector3(side * h.x, y, 0f), new Vector3(0.03f, size.y / 3f - 0.02f, size.z - 0.03f), 0.008f,
                          turn);
                }
            }

            // Lid boards.
            for (int k = 0; k < 3; k++)
            {
                float z = -h.z + size.z * (k + 0.5f) / 3f;
                m.Box(wood * (1f + 0.04f * Jitter(key * 11 + k)), foot + turn * new Vector3(0f, size.y + 0.012f, z),
                      new Vector3(size.x + 0.02f, 0.024f, size.z / 3f - 0.012f), 0.008f, turn);
            }

            // Corner posts, darker.
            for (int k = 0; k < 4; k++)
            {
                float x = (k & 1) == 0 ? -h.x : h.x, z = (k & 2) == 0 ? -h.z : h.z;
                m.Box(Walnut, foot + turn * new Vector3(x, h.y, z), new Vector3(0.04f, size.y, 0.04f), 0.01f, turn);
            }
        }

        /// <summary>
        /// A tied sack of grain, standing at <paramref name="foot"/>: a heavy belly slumped low and a
        /// little to one side, a neck gathered in by a cord, and the sacking above it flopping open.
        /// </summary>
        public static void Sack(Model m, Vector3 foot, float radius, float height, float lean, int key)
        {
            Color burlap = Hex("A9946F") * (1f + 0.04f * Jitter(key));
            Color cord = Hex("6A4E30");

            // (height, radius) up the sack, as fractions: flat foot, low heavy belly, neck, flare, closed top.
            Vector2[] profile =
            {
                new Vector2(0f, 0f), new Vector2(0.02f, 0.82f), new Vector2(0.1f, 1f), new Vector2(0.3f, 1.04f), new Vector2(0.5f, 0.92f),
                new Vector2(0.66f, 0.62f), new Vector2(0.75f, 0.36f), new Vector2(0.8f, 0.33f), new Vector2(0.87f, 0.5f),
                new Vector2(0.94f, 0.58f), new Vector2(0.985f, 0.36f), new Vector2(1f, 0f),
            };

            m.Surface((p, n) =>
            {
                float t = (p.y - foot.y) / height;
                if (t > 0.74f && t < 0.81f) return cord;
                // Darker in the creases toward the neck, lighter on the belly.
                return burlap * Mathf.Lerp(1.02f, 0.86f, Mathf.Clamp01((t - 0.45f) / 0.3f)) * Mathf.Lerp(0.9f, 1f, Mathf.Clamp01(t * 6f));
            }, 24, 22, (u, v) =>
            {
                float a = u * Mathf.PI * 2f;
                float f = Mathf.Clamp01(v) * (profile.Length - 1);
                int i = Mathf.Min((int)f, profile.Length - 2);
                Vector2 q = Vector2.Lerp(profile[i], profile[i + 1], Smooth(f - i));

                float r = radius * q.y;
                // Lumpy where the grain sits, creased at the neck, and the flap above it uneven.
                r *= 1f + 0.06f * Mathf.Sin(a * 3f + key) * Mathf.Sin(Mathf.Clamp01(v / 0.6f) * Mathf.PI)
                        + 0.08f * Mathf.Sin(a * 5f + key * 2f) * Mathf.Clamp01((v - 0.8f) / 0.15f);
                float y = q.x * height;
                return foot + new Vector3(Mathf.Cos(a) * r + lean * q.x * q.x * height, y, Mathf.Sin(a) * r * 0.86f);
            }, true);
        }
    }
}
