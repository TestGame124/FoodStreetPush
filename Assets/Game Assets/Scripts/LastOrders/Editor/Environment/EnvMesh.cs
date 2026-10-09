using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// A mesh put together face by face: boxes for walls, sloped quads and triangles for roofs.
    ///
    /// Primitives cannot make a roof - Unity has no triangle, and a gable end is one - and a cube's
    /// UVs stretch one copy of a texture over each face whatever its size, so siding on a long wall
    /// comes out as fat planks and on a short one as thin ones. Faces built here are UV'd in metres,
    /// so a material's tiling is a real size and every wall's boards are the same width. Several
    /// submeshes let one house be one renderer with a material per part.
    ///
    /// Faces are flat-shaded (no shared vertices), which is the look the rest of the level has.
    /// </summary>
    public sealed class EnvMesh
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int>[] triangles;

        public EnvMesh(int submeshes)
        {
            triangles = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++) triangles[i] = new List<int>();
        }

        public int Submeshes => triangles.Length;

        public bool IsEmpty
        {
            get
            {
                foreach (List<int> t in triangles)
                    if (t.Count > 0) return false;
                return true;
            }
        }

        /// <summary>
        /// A four-cornered face. The corners go round the edge in either direction; the face is
        /// turned so it looks along <paramref name="outward"/>, which is the side that gets drawn.
        /// </summary>
        public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                         Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 outward)
        {
            // From the diagonals rather than two edges: a hipped roof over a square plan has two
            // corners of each "quad" at the same point, and an edge of zero length gives no normal.
            Vector3 n = Vector3.Cross(c - a, d - b);
            if (Vector3.Dot(n, outward) < 0f)
            {
                (b, d) = (d, b);
                (ub, ud) = (ud, ub);
                n = -n;
            }

            n = n.normalized;
            int i = vertices.Count;

            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
            for (int k = 0; k < 4; k++) normals.Add(n);

            triangles[sub].Add(i); triangles[sub].Add(i + 1); triangles[sub].Add(i + 2);
            triangles[sub].Add(i); triangles[sub].Add(i + 2); triangles[sub].Add(i + 3);
        }

        public void Triangle(int sub, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 outward)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, outward) < 0f)
            {
                (b, c) = (c, b);
                (ub, uc) = (uc, ub);
                n = -n;
            }

            n = n.normalized;
            int i = vertices.Count;

            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc);
            for (int k = 0; k < 3; k++) normals.Add(n);

            triangles[sub].Add(i); triangles[sub].Add(i + 1); triangles[sub].Add(i + 2);
        }

        /// <summary>
        /// A box, turned by <paramref name="rotation"/>, UV'd in metres on every face: along the
        /// face's width and up its height, so siding runs level on every wall.
        /// </summary>
        public void Box(int sub, Vector3 centre, Vector3 size, Quaternion rotation, bool bottom = false)
        {
            Vector3 h = size * 0.5f;

            // Each face: its normal, and the axis its V runs up. U is whatever completes the pair.
            Face(sub, centre, rotation, Vector3.up, Vector3.forward, h);
            if (bottom) Face(sub, centre, rotation, Vector3.down, Vector3.forward, h);
            Face(sub, centre, rotation, Vector3.right, Vector3.up, h);
            Face(sub, centre, rotation, Vector3.left, Vector3.up, h);
            Face(sub, centre, rotation, Vector3.forward, Vector3.up, h);
            Face(sub, centre, rotation, Vector3.back, Vector3.up, h);
        }

        void Face(int sub, Vector3 centre, Quaternion rotation, Vector3 n, Vector3 v, Vector3 h)
        {
            Vector3 u = Vector3.Cross(n, v);

            float hn = Mathf.Abs(Vector3.Dot(n, h));
            float hu = Mathf.Abs(Vector3.Dot(u, h));
            float hv = Mathf.Abs(Vector3.Dot(v, h));

            Vector3 Corner(float su, float sv) => centre + rotation * (n * hn + u * (su * hu) + v * (sv * hv));

            // Offset by the box's own position so neighbouring walls do not all start their
            // boards at the same seam.
            float ou = Vector3.Dot(rotation * u, centre);
            float ov = Vector3.Dot(rotation * v, centre);

            Quad(sub,
                 Corner(-1f, -1f), Corner(-1f, 1f), Corner(1f, 1f), Corner(1f, -1f),
                 new Vector2(ou - hu, ov - hv), new Vector2(ou - hu, ov + hv), new Vector2(ou + hu, ov + hv), new Vector2(ou + hu, ov - hv),
                 rotation * n);
        }

        /// <summary>The finished mesh, with only the submeshes that have faces, and which of them they were.</summary>
        public Mesh Build(string name, out List<int> usedSubmeshes)
        {
            usedSubmeshes = new List<int>();
            for (int i = 0; i < triangles.Length; i++)
                if (triangles[i].Count > 0) usedSubmeshes.Add(i);

            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = usedSubmeshes.Count;
            for (int i = 0; i < usedSubmeshes.Count; i++) mesh.SetTriangles(triangles[usedSubmeshes[i]], i);

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        // --- Roofs --------------------------------------------------------------------------------------
        //
        // Every roof is built in its own frame: origin at the middle of the wall tops, the ridge
        // along local X, local Z across it. The caller supplies where that frame sits.

        public struct RoofParts
        {
            public int Covering;
            public int Gable;
            public int Trim;
        }

        /// <summary>
        /// A pitched roof, gabled at both ends. Slopes overhang the walls; the gable triangles sit
        /// flush with the end walls and take the wall's own material.
        /// </summary>
        public void GableRoof(RoofParts parts, Vector3 origin, Quaternion rotation, float length, float width,
                              float rise, float overhang, float endOverhang = 0.3f)
        {
            float slope = rise / (width * 0.5f);
            float x = length * 0.5f + endOverhang;
            float ze = width * 0.5f + overhang;
            float ye = -overhang * slope;

            Vector3 P(float px, float py, float pz) => origin + rotation * new Vector3(px, py, pz);

            float run = Mathf.Sqrt(ze * ze + (rise - ye) * (rise - ye));

            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 outward = rotation * new Vector3(0f, width * 0.5f, side * rise).normalized;

                Quad(parts.Covering,
                     P(-x, ye, side * ze), P(-x, rise, 0f), P(x, rise, 0f), P(x, ye, side * ze),
                     new Vector2(-x, 0f), new Vector2(-x, run), new Vector2(x, run), new Vector2(x, 0f),
                     outward);

                // Fascia: the board along the eave that gives the roof an edge.
                Quad(parts.Trim,
                     P(-x, ye, side * ze), P(x, ye, side * ze), P(x, ye - 0.18f, side * ze), P(-x, ye - 0.18f, side * ze),
                     Vector2.zero, Vector2.right, Vector2.one, Vector2.up,
                     rotation * new Vector3(0f, 0f, side));

                // The gable triangle under the slopes, at this end.
                float gx = side * length * 0.5f;
                Triangle(parts.Gable,
                         P(gx, 0f, -width * 0.5f), P(gx, rise, 0f), P(gx, 0f, width * 0.5f),
                         new Vector2(-width * 0.5f, 0f), new Vector2(0f, rise), new Vector2(width * 0.5f, 0f),
                         rotation * new Vector3(side, 0f, 0f));
            }

            // Ridge cap.
            Box(parts.Trim, P(0f, rise + 0.03f, 0f), new Vector3(x * 2f, 0.08f, 0.22f), rotation);
        }

        /// <summary>A hipped roof: sloped on all four sides, or a pyramid when the plan is square.</summary>
        public void HipRoof(RoofParts parts, Vector3 origin, Quaternion rotation, float length, float width,
                            float rise, float overhang)
        {
            float slope = rise / (width * 0.5f);
            float ridge = Mathf.Max(0f, (length - width) * 0.5f);
            float xe = length * 0.5f + overhang;
            float ze = width * 0.5f + overhang;
            float ye = -overhang * slope;

            Vector3 P(float px, float py, float pz) => origin + rotation * new Vector3(px, py, pz);

            float run = Mathf.Sqrt(ze * ze + (rise - ye) * (rise - ye));

            foreach (float side in new[] { -1f, 1f })
            {
                // The long sides: trapezoids up to the ridge.
                Quad(parts.Covering,
                     P(-xe, ye, side * ze), P(-ridge, rise, 0f), P(ridge, rise, 0f), P(xe, ye, side * ze),
                     new Vector2(-xe, 0f), new Vector2(-ridge, run), new Vector2(ridge, run), new Vector2(xe, 0f),
                     rotation * new Vector3(0f, 1f, side * slope).normalized);

                // The ends: triangles up to the ridge's end.
                Triangle(parts.Covering,
                         P(side * xe, ye, -ze), P(side * ridge, rise, 0f), P(side * xe, ye, ze),
                         new Vector2(-ze, 0f), new Vector2(0f, run), new Vector2(ze, 0f),
                         rotation * new Vector3(side * slope, 1f, 0f).normalized);

                Quad(parts.Trim,
                     P(-xe, ye, side * ze), P(xe, ye, side * ze), P(xe, ye - 0.16f, side * ze), P(-xe, ye - 0.16f, side * ze),
                     Vector2.zero, Vector2.right, Vector2.one, Vector2.up, rotation * new Vector3(0f, 0f, side));
                Quad(parts.Trim,
                     P(side * xe, ye, -ze), P(side * xe, ye, ze), P(side * xe, ye - 0.16f, ze), P(side * xe, ye - 0.16f, -ze),
                     Vector2.zero, Vector2.right, Vector2.one, Vector2.up, rotation * new Vector3(side, 0f, 0f));
            }

            if (ridge > 0.05f) Box(parts.Trim, P(0f, rise + 0.03f, 0f), new Vector3(ridge * 2f + 0.2f, 0.08f, 0.2f), rotation);
        }

        /// <summary>A single slope, high at local +Z: a lean-to, a garage, a shack.</summary>
        public void ShedRoof(RoofParts parts, Vector3 origin, Quaternion rotation, float length, float width,
                             float rise, float overhang)
        {
            float slope = rise / width;
            float x = length * 0.5f + overhang * 0.6f;
            float zLow = -width * 0.5f - overhang;
            float yLow = -overhang * slope;
            float zHigh = width * 0.5f + overhang * 0.4f;
            float yHigh = rise + overhang * 0.4f * slope;

            Vector3 P(float px, float py, float pz) => origin + rotation * new Vector3(px, py, pz);

            float run = Mathf.Sqrt((zHigh - zLow) * (zHigh - zLow) + (yHigh - yLow) * (yHigh - yLow));

            Quad(parts.Covering,
                 P(-x, yLow, zLow), P(-x, yHigh, zHigh), P(x, yHigh, zHigh), P(x, yLow, zLow),
                 new Vector2(-x, 0f), new Vector2(-x, run), new Vector2(x, run), new Vector2(x, 0f),
                 rotation * new Vector3(0f, 1f, -slope).normalized);

            // The wall filling under the slope: two side triangles and the high back strip.
            foreach (float side in new[] { -1f, 1f })
            {
                float sx = side * length * 0.5f;
                Triangle(parts.Gable,
                         P(sx, 0f, -width * 0.5f), P(sx, rise, width * 0.5f), P(sx, 0f, width * 0.5f),
                         new Vector2(-width * 0.5f, 0f), new Vector2(width * 0.5f, rise), new Vector2(width * 0.5f, 0f),
                         rotation * new Vector3(side, 0f, 0f));
            }

            Quad(parts.Gable,
                 P(-length * 0.5f, 0f, width * 0.5f), P(-length * 0.5f, rise, width * 0.5f),
                 P(length * 0.5f, rise, width * 0.5f), P(length * 0.5f, 0f, width * 0.5f),
                 new Vector2(-length * 0.5f, 0f), new Vector2(-length * 0.5f, rise), new Vector2(length * 0.5f, rise), new Vector2(length * 0.5f, 0f),
                 rotation * Vector3.forward);

            Quad(parts.Trim,
                 P(-x, yLow, zLow), P(x, yLow, zLow), P(x, yLow - 0.15f, zLow), P(-x, yLow - 0.15f, zLow),
                 Vector2.zero, Vector2.right, Vector2.one, Vector2.up, rotation * Vector3.back);
        }

        /// <summary>
        /// A flat patch lying on a roof slope - a hole, a tarp, a bare board, moss - given in the
        /// slope's own terms: how far along it, how far up it, how big, turned how far.
        /// </summary>
        public void SlopePatch(int sub, Vector3 eaveStart, Vector3 along, Vector3 up, Vector3 normal,
                               Vector2 at, Vector2 size, float angle)
        {
            float c = Mathf.Cos(angle * Mathf.Deg2Rad);
            float s = Mathf.Sin(angle * Mathf.Deg2Rad);

            Vector3 Corner(float du, float dv)
            {
                float ru = du * c - dv * s;
                float rv = du * s + dv * c;
                return eaveStart + along * (at.x + ru) + up * (at.y + rv) + normal * 0.03f;
            }

            float hx = size.x * 0.5f;
            float hy = size.y * 0.5f;

            Quad(sub, Corner(-hx, -hy), Corner(-hx, hy), Corner(hx, hy), Corner(hx, -hy),
                 new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), normal);
        }
    }
}
