using System.IO;
using LastOrders.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using E = LastOrders.EditorTools.EnvKit;
using K = LastOrders.EditorTools.PrimitiveKit;
using L = LastOrders.EditorTools.EnvironmentLayout;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The fog the world ends in: a stack of transparent layers over the edge bands, and a few slow
    /// puffs of mist where the trails come out.
    ///
    /// Each layer is one ring-shaped mesh with a hole over the clear ground, so the play area costs
    /// nothing to draw; the EdgeFog shader works out how thick the fog is from where each pixel
    /// lies. The mesh, the noise the fog is broken up by, and each layer's material are assets,
    /// made here, so the scene only holds references.
    /// </summary>
    public static class EdgeFogBuilder
    {
        const string MeshPath = E.GeneratedFolder + "/EdgeFog_Ring.asset";
        const string NoisePath = E.GeneratedFolder + "/EdgeFog_Noise.png";

        /// <summary>Above the ground stains (0-9) and the light pools, below the drifting mist.</summary>
        public const int FogSortingOrder = 100;

        /// <summary>How many lanes the EdgeFog shader lays a tongue down (_Tongue0 to _Tongue7).</summary>
        public const int MaxTongues = 8;

        /// <summary>Half the width of the strip of fog mesh laid down a corridor for its tongue: the tongue and a margin.</summary>
        const float StripHalfWidth = 6f;

        public static int Build(Transform root, ref DeterministicRandom rng)
        {
            ContentGenerator.EnsureFolder(E.GeneratedFolder);

            Mesh ring = RingMesh();
            Texture2D noise = NoiseTexture();
            Shader shader = Shader.Find("LastOrders/EdgeFog");

            if (shader == null)
            {
                Debug.LogError("[Last Orders] The LastOrders/EdgeFog shader is missing, so the world has no fog.");
                return 0;
            }

            for (int i = 0; i < L.FogLayers.Length; i++)
            {
                L.FogLayer layer = L.FogLayers[i];
                Material material = E.CachedMaterial($"EdgeFog_Layer{i}", () => new Material(shader), m => Configure(m, layer, i, noise));

                var go = new GameObject($"FogLayer_{i}");
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(0f, layer.Height, 0f);

                go.AddComponent<MeshFilter>().sharedMesh = ring;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                // Sorting order outranks the render queue for transparent things, and the ground
                // stains use orders to stack; the fog has to sort above all of them.
                renderer.sortingOrder = FogSortingOrder + i;
            }

            int puffs = 0;
            Material puff = E.SoftSpot("FogPuff", new Color(L.FogColour.r, L.FogColour.g, L.FogColour.b, 0.34f),
                                       additive: false, nightOnly: false, fogTint: true);

            foreach (L.Lane lane in L.Lanes)
            {
                Vector2 inward = lane.Inward;
                var spawn = new Vector2(lane.Spawn.x, lane.Spawn.z);
                Vector2 at = spawn - inward * 5f;
                Puffs(root, $"Mist_{lane.Name}", new Vector3(at.x, 0.7f, at.y), new Vector3(9f, 0.2f, 9f), puff, ref rng);
                puffs++;
            }

            return puffs;
        }

        static void Configure(Material m, L.FogLayer layer, int index, Texture2D noise)
        {
            m.SetColor("_Color", L.FogColour);
            m.SetColor("_ShadeColor", L.FogShade);
            m.SetTexture("_NoiseTex", noise);
            m.SetFloat("_NoiseScale", layer.NoiseScale);
            m.SetVector("_Scroll", layer.Scroll);
            m.SetFloat("_Opacity", layer.Opacity);
            m.SetFloat("_NoiseStrength", 0.55f + index * 0.04f);
            m.SetVector("_ClearRect", new Vector4(L.ClearMinX, L.ClearMinZ, L.ClearMaxX, L.ClearMaxZ));
            m.SetFloat("_FogStart", layer.Start);
            m.SetFloat("_FogEnd", layer.End);
            m.SetFloat("_Solid", layer.Solid);
            m.SetFloat("_TongueWidth", 4.5f);
            m.SetFloat("_TongueStrength", layer.TongueStrength);

            for (int i = 0; i < MaxTongues; i++) m.SetVector($"_Tongue{i}", i < L.Lanes.Length ? Tongue(L.Lanes[i]) : Vector4.zero);

            Vector3 c = L.Checkpoint;
            m.SetVector("_Pocket", new Vector4(c.x, c.z, L.CheckpointFogRadius, L.CheckpointFogKept));

            // Low layers first, so a high one is always drawn over what is under it.
            m.renderQueue = (int)RenderQueue.Transparent + index * 2;
        }

        /// <summary>
        /// A tongue of ground mist from well inside the band, down the lane, to just past where the
        /// zombies appear - so they appear in it.
        /// </summary>
        static Vector4 Tongue(L.Lane lane)
        {
            Vector2 inward = lane.Inward;
            var spawn = new Vector2(lane.Spawn.x, lane.Spawn.z);

            // Where the lane crosses into the clear ground, then ten metres further out.
            float inside = -L.Outside(spawn);
            Vector2 from = spawn - inward * (inside + 10f);
            Vector2 to = spawn + inward * 2.5f;

            return new Vector4(from.x, from.y, to.x, to.y);
        }

        static void Puffs(Transform parent, string name, Vector3 at, Vector3 box, Material material, ref DeterministicRandom rng)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 12f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(18f, 26f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.55f), new Color(1f, 1f, 1f, 0.9f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 14;
            main.gravityModifier = 0f;
            main.flipRotation = 0.5f;

            var emission = ps.emission;
            emission.rateOverTime = 0.6f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            float drift = rng.Range(0.08f, 0.2f) * (rng.Chance(0.5f) ? 1f : -1f);
            velocity.x = new ParticleSystem.MinMaxCurve(drift * 0.6f, drift);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(fade);

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 4f;
            renderer.sortingOrder = FogSortingOrder + 20;

            ps.Play(true);
        }

        // --- Assets ------------------------------------------------------------------------------------

        /// <summary>
        /// A flat frame: the world plus its overhang outside, the clear ground (less an inset) as the
        /// hole - and a strip from the hole's edge down each lane that spawns deeper in than that, so
        /// its tongue carries on to where the zombies appear. The play area between costs nothing.
        /// </summary>
        static Mesh RingMesh()
        {
            Rect outer = NeighborhoodBuilder.Inflate(L.World, L.FogOverhang);
            Rect inner = NeighborhoodBuilder.Inflate(L.Clear, -L.FogHoleInset);

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            bool created = mesh == null;
            if (created) mesh = new Mesh { name = "EdgeFog_Ring" };

            var vertices = new System.Collections.Generic.List<Vector3>
            {
                new Vector3(outer.xMin, 0f, outer.yMin), new Vector3(outer.xMax, 0f, outer.yMin),
                new Vector3(outer.xMax, 0f, outer.yMax), new Vector3(outer.xMin, 0f, outer.yMax),
                new Vector3(inner.xMin, 0f, inner.yMin), new Vector3(inner.xMax, 0f, inner.yMin),
                new Vector3(inner.xMax, 0f, inner.yMax), new Vector3(inner.xMin, 0f, inner.yMax),
            };

            // Four quads between the outer and inner rectangles, wound to face up.
            var triangles = new System.Collections.Generic.List<int>
            {
                0, 4, 1, 1, 4, 5,
                1, 5, 2, 2, 5, 6,
                2, 6, 3, 3, 6, 7,
                3, 7, 0, 0, 7, 4,
            };

            foreach (L.Lane lane in L.Lanes)
            {
                Vector4 tongue = Tongue(lane);
                var tip = new Vector2(tongue.z, tongue.w);
                if (inner.Contains(tip) == false) continue;

                // From where the lane crosses into the hole to a little past the tongue's tip.
                Vector2 inward = lane.Inward;
                var spawn = new Vector2(lane.Spawn.x, lane.Spawn.z);
                Vector2 from = Entry(inner, spawn, inward), to = tip + inward * 2f;
                Vector2 across = new Vector2(-inward.y, inward.x) * StripHalfWidth;

                int b = vertices.Count;
                vertices.Add(new Vector3(from.x - across.x, 0f, from.y - across.y));
                vertices.Add(new Vector3(from.x + across.x, 0f, from.y + across.y));
                vertices.Add(new Vector3(to.x + across.x, 0f, to.y + across.y));
                vertices.Add(new Vector3(to.x - across.x, 0f, to.y - across.y));

                // Either winding: Cull Off, and the normal is set by hand.
                triangles.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals;
            mesh.RecalculateBounds();

            if (created) AssetDatabase.CreateAsset(mesh, MeshPath);
            else EditorUtility.SetDirty(mesh);

            return mesh;
        }

        /// <summary>Where a line walked from outside the rectangle along <paramref name="inward"/> to <paramref name="p"/> crosses into it.</summary>
        static Vector2 Entry(Rect r, Vector2 p, Vector2 inward)
        {
            if (Mathf.Abs(inward.x) > Mathf.Abs(inward.y)) return new Vector2(inward.x > 0f ? r.xMin : r.xMax, p.y);
            return new Vector2(p.x, inward.y > 0f ? r.yMin : r.yMax);
        }

        /// <summary>
        /// Tileable fractal noise, so the fog scrolls forever without a seam. Value noise on a lattice
        /// that wraps, four octaves.
        /// </summary>
        static Texture2D NoiseTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath);
            if (existing != null) return existing;

            const int size = 256;
            var pixels = new Color32[size * size];
            var rng = new DeterministicRandom(4242);

            int[] periods = { 4, 8, 16, 32 };
            float[] weights = { 0.5f, 0.28f, 0.14f, 0.08f };
            var lattices = new float[periods.Length][];

            for (int o = 0; o < periods.Length; o++)
            {
                int p = periods[o];
                lattices[o] = new float[p * p];
                for (int i = 0; i < p * p; i++) lattices[o][i] = rng.NextFloat();
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float value = 0f;
                    for (int o = 0; o < periods.Length; o++)
                    {
                        int p = periods[o];
                        float fx = x / (float)size * p;
                        float fy = y / (float)size * p;
                        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                        float tx = Smooth(fx - x0), ty = Smooth(fy - y0);

                        float a = lattices[o][(y0 % p) * p + x0 % p];
                        float b = lattices[o][(y0 % p) * p + (x0 + 1) % p];
                        float c = lattices[o][((y0 + 1) % p) * p + x0 % p];
                        float d = lattices[o][((y0 + 1) % p) * p + (x0 + 1) % p];

                        value += Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty) * weights[o];
                    }

                    byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(v, v, v, 255);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(NoisePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(NoisePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(NoisePath);
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath);
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
