using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Smooth low-poly shapes, put together in code: turned profiles, rounded boxes and tapered
    /// tubes, each with smooth normals so a dozen segments read as a round thing rather than a
    /// faceted one - the soft, chunky look of the game's props. A part can have hard creases where
    /// its profile says so (the edge where a table top meets its rim); everywhere else it curves.
    ///
    /// Several submeshes, one per material, so a whole prop is one renderer.
    /// </summary>
    public sealed class SmoothMesh
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int>[] triangles;

        public SmoothMesh(int submeshes)
        {
            triangles = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++) triangles[i] = new List<int>();
        }

        /// <summary>
        /// A profile turned round the Y axis through <paramref name="centre"/>. Each point is
        /// (radius, height), bottom to top; <paramref name="subs"/> gives each segment's submesh.
        /// A point given twice in a row is a crease: the surface turns sharply there instead of
        /// curving through it.
        /// </summary>
        public void Lathe(Vector3 centre, IList<Vector2> profile, IList<int> subs, int segments)
        {
            int count = profile.Count;
            var normals2 = new Vector2[count];

            for (int i = 0; i < count; i++)
            {
                Vector2 tangent;
                bool creaseBefore = i > 0 && profile[i] == profile[i - 1];
                bool creaseAfter = i < count - 1 && profile[i + 1] == profile[i];

                if (creaseBefore) tangent = profile[Mathf.Min(i + 1, count - 1)] - profile[i];
                else if (creaseAfter) tangent = profile[i] - profile[Mathf.Max(i - 1, 0)];
                else tangent = profile[Mathf.Min(i + 1, count - 1)] - profile[Mathf.Max(i - 1, 0)];

                // Outward: a profile running out along the bottom, up the side and in along the top
                // has its outside on the right of the direction of travel.
                normals2[i] = new Vector2(tangent.y, -tangent.x).normalized;
            }

            int start = vertices.Count;
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);

                for (int i = 0; i < count; i++)
                {
                    Vector2 p = profile[i];
                    vertices.Add(centre + new Vector3(p.x * cos, p.y, p.x * sin));
                    normals.Add(new Vector3(normals2[i].x * cos, normals2[i].y, normals2[i].x * sin).normalized);
                    uvs.Add(new Vector2(s / (float)segments, i / (float)(count - 1)));
                }
            }

            for (int s = 0; s < segments; s++)
            {
                for (int i = 0; i < count - 1; i++)
                {
                    if (profile[i] == profile[i + 1]) continue;

                    int a = start + s * count + i;
                    int b = start + (s + 1) * count + i;
                    List<int> t = triangles[subs[Mathf.Min(i, subs.Count - 1)]];

                    // Wound to face outward for a profile running bottom to top.
                    t.Add(a); t.Add(a + 1); t.Add(b);
                    t.Add(b); t.Add(a + 1); t.Add(b + 1);
                }
            }
        }

        /// <summary>
        /// A box with every edge and corner rounded to <paramref name="radius"/>, smooth all over: a
        /// cushion, a padded backrest. Each face is a grid packed toward its edges, pushed out onto
        /// the rounding, so the curve has <paramref name="roundSegments"/> steps and the flat middle
        /// costs nothing.
        /// </summary>
        public void RoundedBox(int sub, Vector3 centre, Vector3 size, float radius, Quaternion rotation, int roundSegments = 3)
        {
            Vector3 h = size * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) - 1e-4f);

            float[] Axis(float half)
            {
                var samples = new List<float>();
                for (int k = 0; k <= roundSegments; k++) samples.Add(-half + radius * k / roundSegments);
                for (int k = roundSegments; k >= 0; k--) samples.Add(half - radius * k / roundSegments);
                return samples.ToArray();
            }

            float[] xs = Axis(h.x), ys = Axis(h.y), zs = Axis(h.z);

            // Each face: its normal and the two axes across it, with the arrays those axes sample.
            (Vector3 n, Vector3 u, Vector3 v, float[] su, float[] sv, float depth)[] faces =
            {
                (Vector3.right, Vector3.forward, Vector3.up, zs, ys, h.x),
                (Vector3.left, Vector3.back, Vector3.up, zs, ys, h.x),
                (Vector3.up, Vector3.right, Vector3.forward, xs, zs, h.y),
                (Vector3.down, Vector3.right, Vector3.back, xs, zs, h.y),
                (Vector3.forward, Vector3.left, Vector3.up, xs, ys, h.z),
                (Vector3.back, Vector3.right, Vector3.up, xs, ys, h.z),
            };

            Vector3 inner = h - Vector3.one * radius;

            foreach (var (n, u, v, su, sv, depth) in faces)
            {
                int start = vertices.Count;
                int cu = su.Length, cv = sv.Length;

                for (int j = 0; j < cv; j++)
                {
                    for (int i = 0; i < cu; i++)
                    {
                        Vector3 p = n * depth + u * su[i] + v * sv[j];
                        Vector3 core = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y),
                                                   Mathf.Clamp(p.z, -inner.z, inner.z));
                        Vector3 offset = p - core;
                        Vector3 normal = offset.sqrMagnitude > 1e-10f ? offset.normalized : n;

                        vertices.Add(centre + rotation * (core + normal * radius));
                        normals.Add(rotation * normal);
                        uvs.Add(new Vector2(i / (float)(cu - 1), j / (float)(cv - 1)));
                    }
                }

                for (int j = 0; j < cv - 1; j++)
                {
                    for (int i = 0; i < cu - 1; i++)
                    {
                        int a = start + j * cu + i;
                        int b = a + cu;
                        List<int> t = triangles[sub];

                        // v x u = n for every face above, so this winding faces out.
                        t.Add(a); t.Add(b); t.Add(a + 1);
                        t.Add(a + 1); t.Add(b); t.Add(b + 1);
                    }
                }
            }
        }

        /// <summary>
        /// A surface over the rectangle <paramref name="area"/> (x and y of the rect are X and Z),
        /// raised to <paramref name="height"/>(x, z) at every point of a grid <paramref name="steps"/>
        /// squares across, with smooth normals from the surface's own slope: heaped soil, furrows.
        /// </summary>
        public void Heightfield(int sub, Rect area, int steps, System.Func<float, float, float> height)
        {
            int start = vertices.Count;
            int row = steps + 1;
            float dx = area.width / steps, dz = area.height / steps;

            for (int j = 0; j <= steps; j++)
            {
                for (int i = 0; i <= steps; i++)
                {
                    float x = area.xMin + i * dx, z = area.yMin + j * dz;
                    vertices.Add(new Vector3(x, height(x, z), z));

                    // The slope, from a half-step either side.
                    float sx = (height(x + dx * 0.5f, z) - height(x - dx * 0.5f, z)) / dx;
                    float sz = (height(x, z + dz * 0.5f) - height(x, z - dz * 0.5f)) / dz;
                    normals.Add(new Vector3(-sx, 1f, -sz).normalized);
                    uvs.Add(new Vector2(i / (float)steps, j / (float)steps));
                }
            }

            List<int> t = triangles[sub];
            for (int j = 0; j < steps; j++)
            {
                for (int i = 0; i < steps; i++)
                {
                    int a = start + j * row + i;
                    int b = a + row;

                    // Facing up: +Z then +X is clockwise seen from above.
                    t.Add(a); t.Add(b); t.Add(a + 1);
                    t.Add(a + 1); t.Add(b); t.Add(b + 1);
                }
            }
        }

        /// <summary>
        /// A surface swept over (u, v) in [0, 1] x [0, 1] by <paramref name="at"/>, in a grid
        /// <paramref name="stepsU"/> by <paramref name="stepsV"/> squares, with smooth normals from the
        /// surface's own tangents: a leaf, a bending stem, a fruit. Its uv is (u, v), so a material can
        /// shade along it. The front is the side cross(dv, du) points to, the same way round as
        /// <see cref="Heightfield"/>. <paramref name="wrapU"/> says u = 0 and u = 1 are the same line
        /// (the seam round a stem or a fruit), so the normals there match across it.
        /// </summary>
        public void Surface(int sub, int stepsU, int stepsV, System.Func<float, float, Vector3> at, bool wrapU = false)
        {
            int start = vertices.Count;
            int row = stepsU + 1;

            for (int j = 0; j <= stepsV; j++)
            {
                float v = j / (float)stepsV;
                for (int i = 0; i <= stepsU; i++)
                {
                    float u = i / (float)stepsU;
                    vertices.Add(at(u, v));
                    normals.Add(SurfaceNormal(at, u, v, wrapU));
                    uvs.Add(new Vector2(u, v));
                }
            }

            List<int> t = triangles[sub];
            for (int j = 0; j < stepsV; j++)
            {
                for (int i = 0; i < stepsU; i++)
                {
                    int a = start + j * row + i;
                    int b = a + row;
                    t.Add(a); t.Add(b); t.Add(a + 1);
                    t.Add(a + 1); t.Add(b); t.Add(b + 1);
                }
            }
        }

        static Vector3 SurfaceNormal(System.Func<float, float, Vector3> at, float u, float v, bool wrapU)
        {
            const float e = 1e-3f;

            // At a pole or a pointed tip one tangent vanishes; the normal is taken a little way in instead.
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float u0 = wrapU ? Mathf.Repeat(u - e, 1f) : Mathf.Max(u - e, 0f);
                float u1 = wrapU ? Mathf.Repeat(u + e, 1f) : Mathf.Min(u + e, 1f);
                Vector3 du = at(u1, v) - at(u0, v);
                Vector3 dv = at(u, Mathf.Min(v + e, 1f)) - at(u, Mathf.Max(v - e, 0f));
                Vector3 n = Vector3.Cross(dv, du);

                // A tangent next to nothing beside the other counts as vanished: at a pole where
                // sin(pi) comes out a hair below zero it is not quite zero, and points the wrong way.
                float a = du.sqrMagnitude, b = dv.sqrMagnitude;
                bool sound = a > b * 1e-6f && b > a * 1e-6f && n.sqrMagnitude > a * b * 1e-6f;

                // Divided out by hand: Vector3.normalized gives zero for anything under 1e-5 long,
                // and a cross of two steps this small is far shorter than that.
                if (sound) return n / Mathf.Sqrt(n.sqrMagnitude);

                v = Mathf.MoveTowards(v, 0.5f, e * 4f);
            }

            return Vector3.up;
        }

        /// <summary>A round tube from <paramref name="from"/> to <paramref name="to"/>, tapering from one
        /// radius to the other, smooth round its sides, with a flat cap at each end: a chair leg.</summary>
        public void Tube(int sub, Vector3 from, Vector3 to, float radiusFrom, float radiusTo, int segments)
        {
            Vector3 axis = to - from;
            float length = axis.magnitude;
            if (length < 1e-5f) return;

            Quaternion turn = Quaternion.FromToRotation(Vector3.up, axis / length);
            var mesh = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(radiusFrom, 0f), new Vector2(radiusFrom, 0f),
                new Vector2(radiusTo, length), new Vector2(radiusTo, length), new Vector2(0f, length),
            };

            int before = vertices.Count;
            Lathe(Vector3.zero, mesh, new[] { sub }, segments);

            for (int i = before; i < vertices.Count; i++)
            {
                vertices[i] = from + turn * vertices[i];
                normals[i] = turn * normals[i];
            }
        }

        /// <summary>
        /// The finished mesh saved as an asset at <paramref name="path"/> - into the asset already
        /// there when there is one, so every prefab pointing at it keeps pointing at it.
        /// </summary>
        public Mesh BuildAsset(string path, string name, out List<int> used)
        {
            Mesh built = Build(name, out used);

            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                UnityEditor.AssetDatabase.CreateAsset(built, path);
                UnityEditor.AssetDatabase.SaveAssetIfDirty(built);
                return built;
            }

            // Through the mesh API, not CopySerialized: that copies the data but leaves the GPU
            // drawing the old vertices with the new triangles until the editor restarts.
            existing.Clear();
            existing.name = name;
            existing.indexFormat = built.indexFormat;
            existing.SetVertices(built.vertices);
            existing.SetNormals(built.normals);
            existing.SetTangents(built.tangents);
            existing.SetUVs(0, built.uv);
            existing.subMeshCount = built.subMeshCount;
            for (int s = 0; s < built.subMeshCount; s++) existing.SetTriangles(built.GetTriangles(s), s);
            existing.RecalculateBounds();

            Object.DestroyImmediate(built);
            UnityEditor.EditorUtility.SetDirty(existing);

            // Only this mesh: SaveAssets would also write whatever else is dirty, packages included.
            UnityEditor.AssetDatabase.SaveAssetIfDirty(existing);
            return existing;
        }

        /// <summary>The finished mesh, keeping only the submeshes that have faces; <paramref name="used"/> says which.</summary>
        public Mesh Build(string name, out List<int> used)
        {
            used = new List<int>();
            for (int i = 0; i < triangles.Length; i++)
                if (triangles[i].Count > 0) used.Add(i);

            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = used.Count;
            for (int i = 0; i < used.Count; i++) mesh.SetTriangles(triangles[used[i]], i);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
