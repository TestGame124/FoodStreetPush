using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Merges a group of primitive shapes into one mesh on the group itself: one renderer where
    /// there were dozens, a submesh per material.
    ///
    /// The shapes are built as they always were - boxes placed by the kit - and baked afterwards,
    /// so the code that decides what a wall looks like stays readable. Surfaces going onto a
    /// texture get their UVs projected rather than kept: a cube's own UVs stretch one copy of the
    /// texture over each face whatever its size. Projected ones run along the wall and up it in
    /// real units, so blocks are the same size on every piece and carry on from one section to the
    /// next, and the texture's height bands - footing, blocks, cap - land at the heights they are.
    /// </summary>
    public static class MeshBaker
    {
        /// <summary>What a source material becomes, and whether its faces take projected UVs.</summary>
        public delegate Material Remap(Material source, out bool project);

        /// <param name="along">Where the group's local X = 0 falls along the whole wall, in metres, so the
        /// texture carries on across groups laid end to end.</param>
        /// <param name="tileLength">Metres along the wall one copy of the texture covers.</param>
        /// <param name="height">Metres up the wall the texture's V runs over, from the group's local Y = 0.</param>
        public static int Bake(Transform group, Remap remap, float along, float tileLength, float height)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var bySubmesh = new Dictionary<Material, List<int>>();
            var order = new List<Material>();
            var doomed = new List<GameObject>();

            Matrix4x4 toGroup = group.worldToLocalMatrix;

            foreach (MeshFilter filter in group.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.transform == group || filter.sharedMesh == null) continue;

                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                Mesh mesh = filter.sharedMesh;
                Matrix4x4 m = toGroup * filter.transform.localToWorldMatrix;

                Vector3[] v = mesh.vertices;
                Vector3[] n = mesh.normals;
                Vector2[] uv = mesh.uv;

                Material[] materials = renderer.sharedMaterials;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    Material source = materials[Mathf.Min(s, materials.Length - 1)];
                    Material target = remap(source, out bool project);
                    if (target == null) continue;

                    if (!bySubmesh.TryGetValue(target, out List<int> triangles))
                    {
                        triangles = new List<int>();
                        bySubmesh[target] = triangles;
                        order.Add(target);
                    }

                    // Vertices copied per submesh: a vertex shared by two submeshes would need two UVs.
                    var remapped = new Dictionary<int, int>();
                    foreach (int index in mesh.GetTriangles(s))
                    {
                        if (!remapped.TryGetValue(index, out int copy))
                        {
                            Vector3 p = m.MultiplyPoint3x4(v[index]);
                            Vector3 normal = m.MultiplyVector(n.Length > index ? n[index] : Vector3.up).normalized;

                            copy = vertices.Count;
                            vertices.Add(p);
                            normals.Add(normal);
                            uvs.Add(project ? Project(p, normal, along, tileLength, height) : (uv.Length > index ? uv[index] : Vector2.zero));
                            remapped[index] = copy;
                        }

                        triangles.Add(copy);
                    }
                }

                doomed.Add(filter.gameObject);
            }

            if (vertices.Count == 0) return 0;

            var baked = new Mesh { name = group.name + "_Baked" };
            if (vertices.Count > 65000) baked.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            baked.SetVertices(vertices);
            baked.SetNormals(normals);
            baked.SetUVs(0, uvs);
            baked.subMeshCount = order.Count;
            for (int i = 0; i < order.Count; i++) baked.SetTriangles(bySubmesh[order[i]], i);
            baked.RecalculateBounds();

            // Children first, then the group becomes the renderer.
            foreach (GameObject go in doomed)
                if (go != null) Object.DestroyImmediate(go);

            // Not ??: a missing component reads as a Unity "fake null", which ?? does not see.
            var groupFilter = group.GetComponent<MeshFilter>();
            if (groupFilter == null) groupFilter = group.gameObject.AddComponent<MeshFilter>();
            groupFilter.sharedMesh = baked;

            var groupRenderer = group.GetComponent<MeshRenderer>();
            if (groupRenderer == null) groupRenderer = group.gameObject.AddComponent<MeshRenderer>();
            groupRenderer.sharedMaterials = order.ToArray();

            return doomed.Count;
        }

        /// <summary>How far under its own height a face looking up samples the texture: below the lit
        /// edge painted along the top of each band, into the band's body, so a ledge's top is the
        /// band's colour rather than a strip of highlight.</summary>
        const float TopFaceDrop = 0.06f;

        /// <summary>
        /// Box projection in the group's space. Faces looking along the wall or across it take U
        /// along the wall (or across it, for the ends) and V up it; faces looking up take U along
        /// and a V a little under their own height, so a ledge's top shows the band it is the top of.
        /// </summary>
        static Vector2 Project(Vector3 p, Vector3 n, float along, float tileLength, float height)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);

            if (ay >= ax && ay >= az)
                return new Vector2((p.x + along) / tileLength, (p.y - TopFaceDrop * Mathf.Sign(n.y)) / height);

            if (ax >= az)
                return new Vector2((p.z + along) / tileLength, p.y / height);

            return new Vector2((p.x + along) / tileLength, p.y / height);
        }
    }
}
