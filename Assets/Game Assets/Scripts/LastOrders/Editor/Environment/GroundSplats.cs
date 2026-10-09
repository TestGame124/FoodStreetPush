using System.Collections.Generic;
using System.IO;
using LastOrders.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Stains on the ground: mud, dead grass, a worn path, the dark under the trees.
    ///
    /// Flat opaque ovals were tried first and read as paper cut-outs laid on the grass. These are
    /// see-through quads with a lobed, organic outline, lit like the ground under them, so a patch
    /// of mud darkens the grass rather than covering it. The outline comes from four blob shapes
    /// generated into one texture; turned, stretched and overlapped, no two patches match.
    ///
    /// The edge is crisp and the inside one flat tone, as the toon grass under them is painted: a
    /// wide feathered edge with mottling inside read as a photograph of mud.
    /// </summary>
    public static class GroundSplats
    {
        const string TexturePath = EnvKit.GeneratedFolder + "/GroundSplat_Atlas.png";
        const string MeshPath = EnvKit.GeneratedFolder + "/GroundSplat_Quads.asset";

        const int Cells = 2;
        const int CellSize = 256;

        /// <summary>Bump when <see cref="BuildAtlas"/> changes, so the texture is made again.</summary>
        const string AtlasVersion = "splat-toon-1";

        static Mesh[] meshes;
        static Texture2D atlas;
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        public static void Reset()
        {
            meshes = null;
            atlas = null;
            Materials.Clear();
        }

        /// <summary>
        /// One stain. <paramref name="order"/> decides which of two overlapping stains is on top, so
        /// a path always lies over the mud it crosses and the result does not change as the camera pans.
        /// </summary>
        public static GameObject Splat(string name, Transform parent, Vector2 at, float sizeX, float sizeZ, float yaw,
                                       Color colour, float alpha, int order, ref DeterministicRandom rng, float y = -1f)
        {
            EnsureAssets();

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(at.x, y >= 0f ? y : 0.012f + order * 0.003f, at.y);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = new Vector3(sizeX, 1f, sizeZ);

            go.AddComponent<MeshFilter>().sharedMesh = meshes[rng.Range(0, meshes.Length)];

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor(colour, alpha);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingOrder = order;

            return go;
        }

        static Material MaterialFor(Color colour, float alpha)
        {
            // Callers still ask for the faint opacities the feathered stains had. Squeezed up toward
            // solid, so where two parts of a stain or a trail overlap they merge into one shape with
            // a lobed edge instead of stacking into a darker see-through blob: the toon way.
            alpha = 0.55f + 0.45f * Mathf.Clamp01(alpha);

            // Opacity in tenths: each step is a material, and finer steps than this are not visible.
            return MaterialAt(colour, Mathf.RoundToInt(Mathf.Clamp01(alpha) * 10f) * 10);
        }

        /// <summary>
        /// The stain material for <paramref name="colour"/> at <paramref name="a"/> percent opacity,
        /// as it is named: "Splat_{colour}_{a}". For recolouring stains already laid.
        /// </summary>
        public static Material MaterialAt(Color colour, int a)
        {
            EnsureAssets();

            string key = $"Splat_{ColorUtility.ToHtmlStringRGB(colour)}_{a}";
            if (Materials.TryGetValue(key, out Material cached) && cached != null) return cached;

            Material material = EnvKit.CachedMaterial(key, () => new Material(Shader.Find("Universal Render Pipeline/Lit")), m =>
            {
                // URP Lit, switched to transparent by hand: what its inspector does when Surface Type is set.
                // URP re-derives the blend state and queue from these properties whenever it validates
                // the material, so they are what has to be right - the raw blend factors alone are undone.
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_BlendModePreserveSpecular", 0f);
                m.SetFloat("_QueueOffset", -50f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_AlphaClip", 0f);
                m.SetFloat("_ReceiveShadows", 1f);
                m.SetFloat("_Smoothness", 0.05f);
                m.SetFloat("_Metallic", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHATEST_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetShaderPassEnabled("DepthOnly", false);
                m.SetShaderPassEnabled("ShadowCaster", false);

                m.SetTexture("_BaseMap", atlas);
                m.SetColor("_BaseColor", new Color(colour.r, colour.g, colour.b, a / 100f));

                // Before the fog and the light pools, after everything solid.
                m.renderQueue = (int)RenderQueue.Transparent - 50;
                m.enableInstancing = true;
            });

            Materials[key] = material;
            return material;
        }

        // --- Assets -----------------------------------------------------------------------------------

        static void EnsureAssets()
        {
            if (meshes != null && atlas != null) return;

            ContentGenerator.EnsureFolder(EnvKit.GeneratedFolder);

            // Rebuilt when the generator below changes, not only when the file is missing.
            atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var importer = AssetImporter.GetAtPath(TexturePath);
            if (atlas == null || importer == null || importer.userData != AtlasVersion) atlas = BuildAtlas();

            meshes = LoadOrBuildMeshes();
        }

        static Mesh[] LoadOrBuildMeshes()
        {
            var existing = new List<Mesh>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(MeshPath))
                if (o is Mesh m) existing.Add(m);

            if (existing.Count == Cells * Cells)
            {
                existing.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                return existing.ToArray();
            }

            if (File.Exists(MeshPath)) AssetDatabase.DeleteAsset(MeshPath);

            var built = new Mesh[Cells * Cells];
            for (int i = 0; i < built.Length; i++)
            {
                float u0 = (i % Cells) / (float)Cells;
                float v0 = (i / Cells) / (float)Cells;
                float step = 1f / Cells;

                // Pulled in by a texel so a neighbouring cell never bleeds into the edge.
                float inset = 1.5f / (Cells * CellSize);

                built[i] = new Mesh
                {
                    name = $"GroundSplat_{i}",
                    vertices = new[]
                    {
                        new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                        new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f),
                    },
                    uv = new[]
                    {
                        new Vector2(u0 + inset, v0 + inset), new Vector2(u0 + step - inset, v0 + inset),
                        new Vector2(u0 + step - inset, v0 + step - inset), new Vector2(u0 + inset, v0 + step - inset),
                    },
                    normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
                    tangents = new[]
                    {
                        new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f),
                    },
                    triangles = new[] { 0, 2, 1, 0, 3, 2 },
                };
                built[i].RecalculateBounds();

                if (i == 0) AssetDatabase.CreateAsset(built[i], MeshPath);
                else AssetDatabase.AddObjectToAsset(built[i], MeshPath);
            }

            AssetDatabase.SaveAssets();
            return built;
        }

        /// <summary>
        /// Four lobed blobs, white, their shape in the alpha. The radius wobbles with angle - three
        /// or four lobes and a gentle waver - with a crisp edge a few texels wide and a flat inside.
        /// </summary>
        static Texture2D BuildAtlas()
        {
            int size = Cells * CellSize;
            var pixels = new Color32[size * size];
            var rng = new DeterministicRandom(9137);

            for (int cell = 0; cell < Cells * Cells; cell++)
            {
                int ox = (cell % Cells) * CellSize;
                int oy = (cell / Cells) * CellSize;

                // Lobes: a few low harmonics of the radius, each with its own phase.
                var amp = new float[5];
                var phase = new float[5];
                for (int k = 0; k < amp.Length; k++)
                {
                    amp[k] = rng.Range(0.03f, 0.16f) / (1f + k * 0.6f);
                    phase[k] = rng.Range(0f, Mathf.PI * 2f);
                }

                float seedX = rng.Range(0f, 100f);
                float seedY = rng.Range(0f, 100f);

                for (int y = 0; y < CellSize; y++)
                {
                    for (int x = 0; x < CellSize; x++)
                    {
                        float px = (x + 0.5f) / CellSize * 2f - 1f;
                        float py = (y + 0.5f) / CellSize * 2f - 1f;
                        float r = Mathf.Sqrt(px * px + py * py);
                        float angle = Mathf.Atan2(py, px);

                        // Smaller than the feathered blobs were, because the crisp edge no longer
                        // eats into the shape: the same stain covers about the same ground.
                        float edge = 0.7f;
                        for (int k = 0; k < amp.Length; k++) edge += Mathf.Sin(angle * (k + 2) + phase[k]) * amp[k];
                        edge += (Mathf.PerlinNoise(seedX + px * 2.2f, seedY + py * 2.2f) - 0.5f) * 0.1f;
                        edge = Mathf.Min(edge, 0.95f);

                        // Mathf.SmoothStep is an eased lerp, not the shader smoothstep; this is the latter.
                        float fall = Mathf.Clamp01((r - (edge - 0.03f)) / 0.03f);
                        float shape = 1f - fall * fall * (3f - 2f * fall);

                        byte alpha = (byte)Mathf.Clamp(Mathf.RoundToInt(shape * 255f), 0, 255);
                        pixels[(oy + y) * size + ox + x] = new Color32(255, 255, 255, alpha);
                    }
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.userData = AtlasVersion;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }
    }
}
