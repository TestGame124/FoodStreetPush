using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using E = LastOrders.EditorTools.EnvKit;
using L = LastOrders.EditorTools.EnvironmentLayout;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The grass the whole level stands on.
    ///
    /// The level's own ground plane keeps its mesh and collider - the build grid and every tap
    /// raycast against it - and gets a new material: the generated turf, tiled at a real size,
    /// with a much coarser variation laid over it, so the tile's repeat is broken up by shading that
    /// repeats eight times less often. Matte, like the low-poly models standing on it, with the
    /// turf's normal map so the light rounds its clumps.
    ///
    /// Past the plane's edge a sheet of the same grass carries on under the fog, UV'd by the plane's
    /// own mapping extended outward, so the texture runs straight across the join.
    /// </summary>
    public static class GroundBuilder
    {
        const string PlanePath = "Geometry/Ground";

        /// <summary>Metres one copy of the grass texture covers. Sized so a tuft is a hand's width
        /// across, big enough to read at the game's camera distance.</summary>
        public const float GrassTile = 8f;

        /// <summary>Metres one copy of the coarse variation - the lit and shaded patches - covers.</summary>
        public const float VariationTile = 64f;

        public static string Build(Scene scene, Transform parent)
        {
            MeshRenderer plane = FindPlane(scene);
            Material grass = GrassFor(plane, out Vector3 uFit, out Vector3 vFit);

            string report;
            if (plane != null)
            {
                Undo.RecordObject(plane, "Grass");
                plane.sharedMaterial = grass;
                report = $"Ground: generated grass on '{PlanePath}', one tile per {GrassTile:0} m, variation per {VariationTile:0} m";
            }
            else
            {
                report = $"WARNING: no '{PlanePath}' in the scene; only the far ground has the new grass";
            }

            FarGround(parent, grass, uFit, vFit);
            return report + ", continued under the fog.";
        }

        /// <summary>
        /// Paints the grass again if its version has changed and sets its material up afresh,
        /// leaving the scene alone: the plane and the far ground already use the material.
        /// </summary>
        public static string Restyle(Scene scene)
        {
            MeshRenderer plane = FindPlane(scene);
            GrassFor(plane, out _, out _);
            return plane != null
                ? $"Ground: grass up to date and its material set up again, tiled to '{PlanePath}'."
                : $"Ground: grass up to date; WARNING: no '{PlanePath}' to tile it to, so it was tiled one UV unit per 100 m.";
        }

        /// <summary>The grass material, tiled to <paramref name="plane"/>'s UVs, and those UVs as functions of world X and Z.</summary>
        static Material GrassFor(MeshRenderer plane, out Vector3 uFit, out Vector3 vFit)
        {
            TextureGenerator.EnsureGrass(out Texture2D albedo, out Texture2D macro);

            // The plane's UVs as a function of where you stand: u = a.x*X + a.y*Z + a.z, same for v.
            if (plane == null || !FitUVs(plane, out uFit, out vFit))
            {
                // No plane to match: a mapping of our own, one UV unit per 100 m.
                uFit = new Vector3(0.01f, 0f, 0f);
                vFit = new Vector3(0f, 0.01f, 0f);
            }

            // Metres per UV unit along each, which is what the tiling has to divide.
            float metresU = 1f / new Vector2(uFit.x, uFit.y).magnitude;
            float metresV = 1f / new Vector2(vFit.x, vFit.y).magnitude;

            return E.CachedMaterial("Ground_Grass", () => new Material(Shader.Find("Universal Render Pipeline/Lit")), m =>
            {
                m.SetTexture("_BaseMap", albedo);
                m.SetTextureScale("_BaseMap", new Vector2(metresU / GrassTile, metresV / GrassTile));
                m.SetColor("_BaseColor", Color.white);

                TextureGenerator.BindNormal(m, albedo);

                // URP Lit tiles the detail map over the base map's already-tiled UVs, not the mesh's,
                // so its scale is the ratio of the two tiles. Scaled from the mesh's UVs like the base
                // map, the variation came out repeating every three metres instead of every sixty-four.
                m.SetTexture("_DetailAlbedoMap", macro);
                m.SetTextureScale("_DetailAlbedoMap", Vector2.one * (GrassTile / VariationTile));
                m.SetFloat("_DetailAlbedoMapScale", 1f);
                m.EnableKeyword("_DETAIL_MULX2");

                // Grass does not shine.
                m.SetFloat("_Smoothness", 0f);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_SpecularHighlights", 0f);
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.SetFloat("_EnvironmentReflections", 0f);
                m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            });
        }

        static MeshRenderer FindPlane(Scene scene)
        {
            string[] parts = PlanePath.Split('/');
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != parts[0]) continue;
                Transform t = root.transform.Find(parts[1]);
                if (t != null && t.gameObject.activeInHierarchy) return t.GetComponent<MeshRenderer>();
            }

            return null;
        }

        /// <summary>A flat sheet beneath everything, out to where the fog is solid, with the plane's UVs carried on.</summary>
        static void FarGround(Transform parent, Material grass, Vector3 uFit, Vector3 vFit)
        {
            Rect r = NeighborhoodBuilder.Inflate(L.World, L.FogOverhang);

            Vector2 UV(float x, float z) => new Vector2(uFit.x * x + uFit.y * z + uFit.z, vFit.x * x + vFit.y * z + vFit.z);

            var mesh = new Mesh
            {
                name = "FarGround",
                vertices = new[]
                {
                    new Vector3(r.xMin, 0f, r.yMin), new Vector3(r.xMin, 0f, r.yMax),
                    new Vector3(r.xMax, 0f, r.yMax), new Vector3(r.xMax, 0f, r.yMin),
                },
                uv = new[] { UV(r.xMin, r.yMin), UV(r.xMin, r.yMax), UV(r.xMax, r.yMax), UV(r.xMax, r.yMin) },
                normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            // Just under the plane, so wherever both are there the plane wins.
            var go = new GameObject("FarGround");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, -0.03f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = grass;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>
        /// Fits u and v as planar functions of world X and Z over the mesh's vertices - exact for a
        /// flat plane, however it is scaled, turned or mirrored.
        /// </summary>
        static bool FitUVs(MeshRenderer renderer, out Vector3 uFit, out Vector3 vFit)
        {
            uFit = vFit = default;

            var filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || mesh.uv == null || mesh.uv.Length != mesh.vertexCount) return false;

            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = mesh.uv;

            // Normal equations for [x z 1] . [a b c] = u, summed over the vertices.
            double sxx = 0, sxz = 0, sx = 0, szz = 0, sz = 0, n = 0;
            double sxu = 0, szu = 0, su = 0, sxv = 0, szv = 0, sv = 0;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 w = renderer.transform.TransformPoint(vertices[i]);
                double x = w.x, z = w.z;

                sxx += x * x; sxz += x * z; sx += x; szz += z * z; sz += z; n += 1;
                sxu += x * uv[i].x; szu += z * uv[i].x; su += uv[i].x;
                sxv += x * uv[i].y; szv += z * uv[i].y; sv += uv[i].y;
            }

            double[,] m = { { sxx, sxz, sx }, { sxz, szz, sz }, { sx, sz, n } };
            if (!Solve(m, sxu, szu, su, out Vector3 u) || !Solve(m, sxv, szv, sv, out Vector3 v)) return false;

            uFit = u;
            vFit = v;
            return new Vector2(u.x, u.y).sqrMagnitude > 1e-12f && new Vector2(v.x, v.y).sqrMagnitude > 1e-12f;
        }

        static bool Solve(double[,] m, double b0, double b1, double b2, out Vector3 result)
        {
            double Det(double a, double b, double c, double d, double e, double f, double g, double h, double i) =>
                a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);

            double det = Det(m[0, 0], m[0, 1], m[0, 2], m[1, 0], m[1, 1], m[1, 2], m[2, 0], m[2, 1], m[2, 2]);
            if (System.Math.Abs(det) < 1e-9)
            {
                result = default;
                return false;
            }

            double x = Det(b0, m[0, 1], m[0, 2], b1, m[1, 1], m[1, 2], b2, m[2, 1], m[2, 2]) / det;
            double y = Det(m[0, 0], b0, m[0, 2], m[1, 0], b1, m[1, 2], m[2, 0], b2, m[2, 2]) / det;
            double z = Det(m[0, 0], m[0, 1], b0, m[1, 0], m[1, 1], b1, m[2, 0], m[2, 1], b2) / det;

            result = new Vector3((float)x, (float)y, (float)z);
            return true;
        }
    }
}
