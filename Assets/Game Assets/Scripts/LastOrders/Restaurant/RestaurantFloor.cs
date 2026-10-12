using UnityEngine;
using UnityEngine.Rendering;

namespace LastOrders.Restaurant
{
    /// <summary>
    /// The restaurant's floor, laid wall to wall inside it: a checkerboard for the dining room and
    /// plain tiles along the kitchen wall.
    ///
    /// A thin slab rather than a flat sheet, so where it shows in the doorway it has an edge, the
    /// step up into the building. It has no collider: every tap and the build grid raycast against
    /// the ground plane a few centimetres under it, and something to hit here would come first.
    ///
    /// The tiles are mapped in metres from a fixed point - the middle of the doorway on the
    /// kitchen wall's line - rather than stretched over the slab. The kitchen wall never moves and
    /// the door only moves toward the room's front, so when the building grows the new floor
    /// carries the pattern on and the old floor keeps every tile where it was.
    ///
    /// <see cref="RestaurantShell"/> lays it again whenever the walls move.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RestaurantFloor : MonoBehaviour
    {
        [Tooltip("The dining room's tiles.")]
        [SerializeField] Material dining;

        [Tooltip("The kitchen strip's tiles, along the back wall.")]
        [SerializeField] Material kitchen;

        [Tooltip("Metres one copy of the dining room's texture covers.")]
        [SerializeField] float diningTile = 2.4f;

        [Tooltip("Metres one copy of the kitchen's texture covers.")]
        [SerializeField] float kitchenTile = 1.2f;

        [Tooltip("How deep the kitchen strip along the back wall is.")]
        [SerializeField] float kitchenDepth = 3f;

        [Tooltip("Height of the floor's top above the ground. Kept under the furniture's feet.")]
        [SerializeField] float top = 0.035f;

        [Tooltip("Optional. A threshold strip between the kitchen tiles and the dining room's floor.")]
        [SerializeField] Material trim;

        [Tooltip("How wide the threshold strip is, taken off the dining room's side.")]
        [SerializeField] float trimWidth = 0.2f;

        Mesh mesh;

        /// <summary>How deep the kitchen strip along the back wall is: where its tiles meet the dining room's floor.</summary>
        public float KitchenDepth => kitchenDepth;

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(Material diningMaterial, Material kitchenMaterial, float diningMetres, float kitchenMetres,
                              float kitchenStripDepth)
        {
            dining = diningMaterial;
            kitchen = kitchenMaterial;
            diningTile = diningMetres;
            kitchenTile = kitchenMetres;
            kitchenDepth = kitchenStripDepth;

            GetComponent<MeshRenderer>().sharedMaterials = Materials();
        }

        /// <summary>Editor wiring, for the setup tool: the threshold strip, or none for null.</summary>
        public void ConfigureTrim(Material trimMaterial, float width)
        {
            trim = trimMaterial;
            trimWidth = width;

            GetComponent<MeshRenderer>().sharedMaterials = Materials();
        }

        /// <summary>One per submesh: dining, kitchen, and the threshold if there is one.</summary>
        Material[] Materials()
        {
            Material k = kitchen != null ? kitchen : dining;
            return trim != null ? new[] { dining, k, trim } : new[] { dining, k };
        }

        /// <summary>
        /// Lays the floor over <paramref name="inner"/>, the room between the walls' inner faces, in
        /// world metres (x and y of the rect are world X and Z). <paramref name="back"/> points from
        /// the room toward the kitchen wall, along X or Z; <paramref name="anchor"/> is where the
        /// tile pattern is measured from.
        /// </summary>
        public void Fit(Rect inner, Vector2 back, Vector2 anchor)
        {
            if (inner.width < 0.1f || inner.height < 0.1f) return;

            if (mesh == null || GetComponent<MeshFilter>().sharedMesh != mesh)
            {
                mesh = new Mesh { name = "RestaurantFloor" };
                mesh.MarkDynamic();
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }

            // The kitchen strip, cut off the side the back points to. None if the room is shallower
            // than two strips: a kitchen floor that fills the whole room is not a kitchen floor.
            Rect diningRect = inner, kitchenRect = default;
            bool alongX = Mathf.Abs(back.x) > Mathf.Abs(back.y);
            float roomDepth = alongX ? inner.width : inner.height;
            bool hasKitchen = kitchen != null && kitchenDepth > 0.01f && roomDepth > kitchenDepth * 2f;

            if (hasKitchen)
            {
                if (alongX && back.x > 0f)
                {
                    kitchenRect = Rect.MinMaxRect(inner.xMax - kitchenDepth, inner.yMin, inner.xMax, inner.yMax);
                    diningRect = Rect.MinMaxRect(inner.xMin, inner.yMin, kitchenRect.xMin, inner.yMax);
                }
                else if (alongX)
                {
                    kitchenRect = Rect.MinMaxRect(inner.xMin, inner.yMin, inner.xMin + kitchenDepth, inner.yMax);
                    diningRect = Rect.MinMaxRect(kitchenRect.xMax, inner.yMin, inner.xMax, inner.yMax);
                }
                else if (back.y > 0f)
                {
                    kitchenRect = Rect.MinMaxRect(inner.xMin, inner.yMax - kitchenDepth, inner.xMax, inner.yMax);
                    diningRect = Rect.MinMaxRect(inner.xMin, inner.yMin, inner.xMax, kitchenRect.yMin);
                }
                else
                {
                    kitchenRect = Rect.MinMaxRect(inner.xMin, inner.yMin, inner.xMax, inner.yMin + kitchenDepth);
                    diningRect = Rect.MinMaxRect(inner.xMin, kitchenRect.yMax, inner.xMax, inner.yMax);
                }
            }

            // The threshold: a strip off the dining room's side of the kitchen line, in the dining
            // room's mapping, so a strip as wide as a board shows as one darker board.
            Rect trimRect = default;
            bool hasTrim = hasKitchen && trim != null && trimWidth > 0.01f;
            if (hasTrim)
            {
                float w = trimWidth;
                if (alongX && back.x > 0f)
                {
                    trimRect = Rect.MinMaxRect(diningRect.xMax - w, diningRect.yMin, diningRect.xMax, diningRect.yMax);
                    diningRect.xMax -= w;
                }
                else if (alongX)
                {
                    trimRect = Rect.MinMaxRect(diningRect.xMin, diningRect.yMin, diningRect.xMin + w, diningRect.yMax);
                    diningRect.xMin += w;
                }
                else if (back.y > 0f)
                {
                    trimRect = Rect.MinMaxRect(diningRect.xMin, diningRect.yMax - w, diningRect.xMax, diningRect.yMax);
                    diningRect.yMax -= w;
                }
                else
                {
                    trimRect = Rect.MinMaxRect(diningRect.xMin, diningRect.yMin, diningRect.xMax, diningRect.yMin + w);
                    diningRect.yMin += w;
                }
            }

            var vertices = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var diningTriangles = new System.Collections.Generic.List<int>();
            var kitchenTriangles = new System.Collections.Generic.List<int>();
            var trimTriangles = new System.Collections.Generic.List<int>();

            Top(diningRect, diningTile, anchor, vertices, normals, uvs, diningTriangles);
            if (hasKitchen) Top(kitchenRect, kitchenTile, anchor, vertices, normals, uvs, kitchenTriangles);
            if (hasTrim) Top(trimRect, diningTile, anchor, vertices, normals, uvs, trimTriangles);
            Sides(inner, diningTile, vertices, normals, uvs, diningTriangles);

            Material[] materials = Materials();

            mesh.Clear();
            mesh.subMeshCount = materials.Length;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(diningTriangles, 0);
            mesh.SetTriangles(kitchenTriangles, 1);
            if (materials.Length > 2) mesh.SetTriangles(trimTriangles, 2);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            var renderer = GetComponent<MeshRenderer>();
            Material[] current = renderer.sharedMaterials;
            bool same = current.Length == materials.Length;
            for (int i = 0; same && i < materials.Length; i++) same = current[i] == materials[i];
            if (!same) renderer.sharedMaterials = materials;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        /// <summary>The top face of one area, its UVs in metres from the anchor.</summary>
        void Top(Rect r, float tile, Vector2 anchor, System.Collections.Generic.List<Vector3> vertices,
                 System.Collections.Generic.List<Vector3> normals, System.Collections.Generic.List<Vector2> uvs,
                 System.Collections.Generic.List<int> triangles)
        {
            int start = vertices.Count;
            float metres = Mathf.Max(0.01f, tile);

            foreach (Vector2 corner in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax),
                                               new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin) })
            {
                vertices.Add(Local(corner, top));
                normals.Add(transform.InverseTransformDirection(Vector3.up));
                uvs.Add((corner - anchor) / metres);
            }

            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        /// <summary>The slab's four edges, from the top down to the ground, facing out.</summary>
        void Sides(Rect r, float tile, System.Collections.Generic.List<Vector3> vertices,
                   System.Collections.Generic.List<Vector3> normals, System.Collections.Generic.List<Vector2> uvs,
                   System.Collections.Generic.List<int> triangles)
        {
            var corners = new[]
            {
                new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin),
                new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax),
            };

            for (int i = 0; i < 4; i++)
            {
                Vector2 a = corners[i], b = corners[(i + 1) % 4];
                Vector2 edge = (b - a).normalized;
                var outward = new Vector3(edge.y, 0f, -edge.x);

                int start = vertices.Count;
                vertices.Add(Local(a, 0f));
                vertices.Add(Local(a, top));
                vertices.Add(Local(b, top));
                vertices.Add(Local(b, 0f));

                Vector3 n = transform.InverseTransformDirection(outward);
                for (int k = 0; k < 4; k++) normals.Add(n);

                // The grout line's colour, which runs along every tile edge: a strip of the texture's
                // edge, squeezed.
                float length = Vector2.Distance(a, b) / Mathf.Max(0.01f, tile);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(0f, 0.004f));
                uvs.Add(new Vector2(length, 0.004f));
                uvs.Add(new Vector2(length, 0f));

                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
        }

        Vector3 Local(Vector2 world, float height) =>
            transform.InverseTransformPoint(new Vector3(world.x, transform.position.y + height, world.y));
    }
}
