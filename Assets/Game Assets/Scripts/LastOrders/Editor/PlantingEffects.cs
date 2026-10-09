using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The burst that plays when a seed goes into a plant pot, built in code: a handful of seeds
    /// dropping into the soil, then a soft puff of earth and a few clods kicked up where they land, a
    /// ring spreading across the soil, little leaves fluttering out and a twinkle of sparkles over
    /// the top - about a second, start to finish.
    ///
    /// The puffs, ring, leaves and sparkles are camera-facing sprites on textures painted here (a
    /// lumpy cartoon cloud, a ring, a leaf, a four-point star) on LastOrders/SoftParticle; the seeds
    /// and clods are small smooth low-poly meshes on LastOrders/Stylized, lit like the bed they fall
    /// into. Saved as a prefab, and set into pfPlantPot on the middle of the soil, where a
    /// <see cref="PotBurst"/> plays it each time the pot is planted.
    /// </summary>
    public static class PlantingEffects
    {
        internal const string Folder = "Assets/Game Assets/Content/LastOrders/Farming/Effects";
        const string PrefabPath = Folder + "/PlantBurst.prefab";
        internal const string PotPrefabPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Farming/PlantPot/pfPlantPot.prefab";
        const string BurstName = "PlantBurst";

        const int TextureSize = 128;

        [MenuItem("Last Orders/Farming/Build Planting Effects (Plant Pots)", priority = 92)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This builds the burst that plays when a seed is planted, and puts it into:\n\n" +
                $"- {Folder} (its textures, meshes, materials and the PlantBurst prefab)\n" +
                "- pfPlantPot (on the middle of the soil, played by a PotBurst each planting)\n\n" +
                "The scene is not changed.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        /// <summary>Builds the burst and puts it into the pot, with no dialogs.</summary>
        public static string Apply()
        {
            ContentGenerator.EnsureFolder(Folder);

            // The dust takes only part of the scene's light, so it stays a warm, light puff against the dark soil.
            Material puff = SpriteMaterial("Burst_Puff", Paint("Particle_Puff", Puff), false, 0.6f, 0.25f);
            Material ring = SpriteMaterial("Burst_Ring", Paint("Particle_Ring", Ring), false, 0.7f, 0.08f);
            Material leaf = SpriteMaterial("Burst_Leaf", Paint("Particle_Leaf", Leaf), false, 1f, 0.25f);
            Material sparkle = SpriteMaterial("Burst_Sparkle", Paint("Particle_Sparkle", Sparkle), true, 0f, 0.3f);
            Material solid = SolidMaterial();

            Mesh seed = SaveMesh("Particle_Seed", new Vector3(0.55f, 0.38f, 1f), 0.18f, 2);
            Mesh clod = SaveMesh("Particle_Clod", new Vector3(1f, 0.72f, 0.85f), 0.28f, 1);

            var root = new GameObject(BurstName);
            try
            {
                root.AddComponent<PotBurst>().PlayOn = PotMoment.Planted;
                Seeds(root.transform, solid, seed);
                Dust(root.transform, puff);
                Clods(root.transform, solid, clod);
                GroundRing(root.transform, ring);
                Leaves(root.transform, leaf);
                Sparkles(root.transform, sparkle);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            var report = new StringBuilder("[Last Orders] Planting effects built.\n");
            report.Append($"  {PrefabPath}: seeds, dust, clods, ring, leaves, sparkles.\n");
            report.Append($"  {DressPot()}\n");
            return report.ToString();
        }

        // --- The burst, part by part -----------------------------------------------------------------

        /// <summary>Seeds dropping from a hand's height into the soil, landing as the rest goes off.</summary>
        internal static void Seeds(Transform root, Material material, Mesh mesh)
        {
            ParticleSystem ps = NewSystem(root, "Seeds", material, ParticleSystemRenderMode.Mesh, mesh);
            var main = ps.main;
            main.startLifetime = 0.2f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.055f);
            main.startRotation3D = true;
            main.startRotationX = Turn();
            main.startRotationY = Turn();
            main.startRotationZ = Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(Hex("D9B77A"), Hex("B98E55"));
            main.maxParticles = 8;

            Burst(ps, 0f, 4);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.11f;
            shape.position = new Vector3(0f, 0.44f, 0f);
            shape.rotation = new Vector3(90f, 0f, 0f);

            Rise(ps, -2.3f, -2.3f);
            Spin3D(ps, 240f, 520f);
        }

        /// <summary>A soft puff of earth thrown out low across the soil, swelling and settling as it fades.</summary>
        internal static void Dust(Transform root, Material material)
        {
            ParticleSystem ps = NewSystem(root, "Dust", material, ParticleSystemRenderMode.Billboard);
            var main = ps.main;
            main.startDelay = 0.17f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 1.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
            main.startRotation = Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(Fade(Hex("D8B98F"), 1f), Fade(Hex("B8946C"), 0.95f));
            main.maxParticles = 18;

            Burst(ps, 0f, 12);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.06f;
            shape.position = new Vector3(0f, 0.03f, 0f);
            shape.rotation = new Vector3(90f, 0f, 0f);

            Rise(ps, 0.25f, 0.45f);
            Drag(ps, 0.12f, 0.3f);
            Grow(ps, Curve((0f, 0.55f), (0.35f, 1.2f), (1f, 1.5f)));
            FadeOut(ps, 0.45f);
            Spin(ps, -50f, 50f);
        }

        /// <summary>Clods of earth kicked up in little arcs and falling back, shrinking into the soil.</summary>
        internal static void Clods(Transform root, Material material, Mesh mesh)
        {
            ParticleSystem ps = NewSystem(root, "Clods", material, ParticleSystemRenderMode.Mesh, mesh);
            var main = ps.main;
            main.startDelay = 0.17f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.055f);
            main.startRotation3D = true;
            main.startRotationX = Turn();
            main.startRotationY = Turn();
            main.startRotationZ = Turn();
            // Dry earth off the top, lighter than the damp soil they fall back onto, or they vanish into it.
            main.startColor = new ParticleSystem.MinMaxGradient(Hex("A07A55"), Hex("C29A6E"));
            main.gravityModifier = 0.6f;
            main.maxParticles = 12;

            Burst(ps, 0f, 8);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 38f;
            shape.radius = 0.06f;
            shape.position = new Vector3(0f, 0.02f, 0f);
            shape.rotation = new Vector3(-90f, 0f, 0f);

            Grow(ps, Curve((0f, 1f), (0.7f, 1f), (1f, 0f)));
            Spin3D(ps, 200f, 420f);
        }

        /// <summary>A ring spreading out across the soil from where the seeds went in.</summary>
        internal static void GroundRing(Transform root, Material material)
        {
            ParticleSystem ps = NewSystem(root, "Ring", material, ParticleSystemRenderMode.HorizontalBillboard);
            var main = ps.main;
            main.startDelay = 0.16f;
            main.startLifetime = 0.42f;
            main.startSpeed = 0f;
            main.startSize = 0.8f;
            main.startColor = Fade(Hex("FFF1D2"), 0.45f);
            main.maxParticles = 1;

            Burst(ps, 0f, 1);

            var shape = ps.shape;
            shape.enabled = false;
            ps.transform.localPosition = new Vector3(0f, 0.018f, 0f);

            Grow(ps, Curve((0f, 0.22f), (0.4f, 0.8f), (1f, 1f)));
            FadeOut(ps, 0f);
        }

        /// <summary>A few small leaves fluttering up and out, turning over as they go.</summary>
        internal static void Leaves(Transform root, Material material)
        {
            ParticleSystem ps = NewSystem(root, "Leaves", material, ParticleSystemRenderMode.Billboard);
            var main = ps.main;
            main.startDelay = 0.19f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 1.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.1f);
            main.startRotation = Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(Hex("7CC24A"), Hex("A6D65A"));
            main.gravityModifier = 0.22f;
            main.maxParticles = 8;

            Burst(ps, 0f, 4);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 42f;
            shape.radius = 0.05f;
            shape.position = new Vector3(0f, 0.06f, 0f);
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.45f;
            noise.frequency = 1.8f;
            noise.scrollSpeed = 0.8f;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            Drag(ps, 0.25f, 0.18f);
            Grow(ps, Curve((0f, 0.3f), (0.15f, 1f), (0.75f, 1f), (1f, 0f)));
            FadeOut(ps, 0.7f);
            Spin(ps, -420f, 420f);
        }

        /// <summary>A twinkle over the top: four-point stars popping up and away, glowing.</summary>
        internal static void Sparkles(Transform root, Material material)
        {
            ParticleSystem ps = NewSystem(root, "Sparkles", material, ParticleSystemRenderMode.Billboard);
            var main = ps.main;
            main.startDelay = 0.22f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.11f, 0.19f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-20f * Mathf.Deg2Rad, 20f * Mathf.Deg2Rad);
            main.startColor = new ParticleSystem.MinMaxGradient(Hex("FFE58A"), Hex("FFF8DA"));
            main.maxParticles = 10;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 4), new ParticleSystem.Burst(0.12f, 2) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.28f;
            shape.position = new Vector3(0f, 0.2f, 0f);

            Rise(ps, 0.2f, 0.32f);
            Grow(ps, Curve((0f, 0f), (0.18f, 1f), (0.6f, 0.75f), (1f, 0f)));
            Spin(ps, 60f, 140f);

            // In front of the dust and leaves it shines through.
            ps.GetComponent<ParticleSystemRenderer>().sortingFudge = -20f;
        }

        // --- Particle system plumbing --------------------------------------------------------------

        internal static ParticleSystem NewSystem(Transform root, string name, Material material, ParticleSystemRenderMode mode, Mesh mesh = null)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(root, false);

            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.sharedMaterial = material;
            renderer.mesh = mesh;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 1f;
            return ps;
        }

        internal static void Burst(ParticleSystem ps, float time, short count)
        {
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(time, count) });
        }

        internal static ParticleSystem.MinMaxCurve Turn() => new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        /// <summary>
        /// A steady climb (or fall) of <paramref name="min"/> to <paramref name="max"/> metres a second.
        /// Every axis is given as a range, even the still ones: Unity refuses velocity curves of mixed kinds.
        /// </summary>
        internal static void Rise(ParticleSystem ps, float min, float max)
        {
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(min, max);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }

        internal static void Grow(ParticleSystem ps, AnimationCurve curve)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        /// <summary>Full until <paramref name="holdUntil"/> of the way through, then faded to nothing.</summary>
        internal static void FadeOut(ParticleSystem ps, float holdUntil)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, holdUntil), new GradientAlphaKey(0f, 1f) });

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        internal static void Spin(ParticleSystem ps, float minDegrees, float maxDegrees)
        {
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(minDegrees * Mathf.Deg2Rad, maxDegrees * Mathf.Deg2Rad);
        }

        internal static void Spin3D(ParticleSystem ps, float minDegrees, float maxDegrees)
        {
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            var speed = new ParticleSystem.MinMaxCurve(minDegrees * Mathf.Deg2Rad, maxDegrees * Mathf.Deg2Rad);
            rotation.x = speed;
            rotation.y = speed;
            rotation.z = speed;
        }

        /// <summary>Slows particles toward <paramref name="limit"/> metres a second, losing <paramref name="dampen"/> of the excess each step.</summary>
        internal static void Drag(ParticleSystem ps, float limit, float dampen)
        {
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = new ParticleSystem.MinMaxCurve(limit);
            drag.dampen = dampen;
        }

        internal static AnimationCurve Curve(params (float time, float value)[] keys)
        {
            var curve = new AnimationCurve();
            foreach (var (time, value) in keys) curve.AddKey(new Keyframe(time, value));
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }

        // --- Textures ------------------------------------------------------------------------------

        /// <summary>
        /// A puffy cartoon cloud: a round middle with four lumps round it, white so the particle's colour
        /// tints it, a little lighter toward the top left and soft at the rim.
        /// </summary>
        internal static Color Puff(float x, float y)
        {
            (Vector2 centre, float radius)[] lumps =
            {
                (new Vector2(0f, 0f), 0.5f),
                (new Vector2(-0.34f, 0.16f), 0.34f), (new Vector2(0.3f, 0.24f), 0.36f),
                (new Vector2(0.3f, -0.26f), 0.32f), (new Vector2(-0.26f, -0.3f), 0.3f),
            };

            var p = new Vector2(x, y);
            float inside = float.MaxValue, shade = 0f;
            foreach (var (centre, radius) in lumps)
            {
                float d = (p - centre).magnitude - radius;
                if (d < inside)
                {
                    inside = d;
                    // Each lump lit as a little dome from the top left.
                    Vector2 n = (p - centre) / radius;
                    shade = Mathf.Clamp01(0.55f + 0.45f * Vector2.Dot(n, new Vector2(-0.6f, 0.8f)));
                }
            }

            float alpha = 1f - Smooth(Mathf.InverseLerp(-0.1f, 0.02f, inside));
            float v = Mathf.Lerp(0.8f, 1f, shade);
            return new Color(v, v, v, alpha);
        }

        internal static Color Ring(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float band = Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.09f, 2f));
            float inner = Mathf.Exp(-Mathf.Pow((r - 0.62f) / 0.18f, 2f)) * 0.25f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(band + inner) * (1f - Smooth(Mathf.InverseLerp(0.9f, 1f, r))));
        }

        /// <summary>A pointed oval leaf with a paler midrib, its base at the bottom.</summary>
        internal static Color Leaf(float x, float y)
        {
            float v = (y + 0.92f) / 1.84f;
            if (v < 0f || v > 1f) return Color.clear;

            float halfWidth = 0.5f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(v, 0.85f))), 0.8f);
            float edge = Mathf.Abs(x) - halfWidth;
            float alpha = 1f - Smooth(Mathf.InverseLerp(-0.05f, 0.01f, edge));

            float rib = 1f - Smooth(Mathf.InverseLerp(0.015f, 0.05f, Mathf.Abs(x)));
            float tone = Mathf.Lerp(0.78f, 1f, v) * Mathf.Lerp(1f, 0.85f, Mathf.Abs(x) / Mathf.Max(halfWidth, 0.01f));
            float value = Mathf.Lerp(tone, 1f, rib * 0.7f);
            return new Color(value, value, value, alpha);
        }

        /// <summary>A four-point star, its arms thinning to points, round a bright soft core.</summary>
        internal static Color Sparkle(float x, float y)
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            float arms = Mathf.Max(Mathf.Exp(-ay * 18f) * Mathf.Pow(Mathf.Max(0f, 1f - ax), 2.2f),
                                   Mathf.Exp(-ax * 18f) * Mathf.Pow(Mathf.Max(0f, 1f - ay), 2.2f));
            float r2 = x * x + y * y;
            float core = Mathf.Exp(-r2 * 26f);
            float glow = Mathf.Exp(-r2 * 5f) * 0.3f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(arms + core + glow));
        }

        /// <summary>Paints a square texture from <paramref name="pixel"/>(x, y), each in -1..1, and saves it as a PNG beside the burst.</summary>
        internal static Texture2D Paint(string name, Func<float, float, Color> pixel)
        {
            const int Samples = 3;
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            var pixels = new Color[TextureSize * TextureSize];

            for (int j = 0; j < TextureSize; j++)
            {
                for (int i = 0; i < TextureSize; i++)
                {
                    // A few samples a pixel, so the shapes' edges come out smooth.
                    Color sum = Color.clear;
                    for (int sj = 0; sj < Samples; sj++)
                        for (int si = 0; si < Samples; si++)
                        {
                            float x = ((i + (si + 0.5f) / Samples) / TextureSize) * 2f - 1f;
                            float y = ((j + (sj + 0.5f) / Samples) / TextureSize) * 2f - 1f;
                            sum += pixel(x, y);
                        }

                    pixels[j * TextureSize + i] = sum / (Samples * Samples);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false);

            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // --- Materials and meshes ------------------------------------------------------------------

        internal static Material SpriteMaterial(string name, Texture2D texture, bool additive, float lit, float pull)
        {
            Material material = SavedMaterial(name, "LastOrders/SoftParticle");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Additive", additive ? 1f : 0f);
            material.SetFloat("_SrcBlend", (float)(additive ? BlendMode.One : BlendMode.SrcAlpha));
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_Lit", lit);
            material.SetFloat("_DepthPull", pull);
            Save(material);
            return material;
        }

        /// <summary>The seeds and clods: lit like the bed, coloured by each particle.</summary>
        internal static Material SolidMaterial()
        {
            Material material = SavedMaterial("Burst_Solid", "LastOrders/Stylized");
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Wrap", 0.55f);
            material.SetFloat("_LightStrength", 0.88f);
            material.SetFloat("_AmbientStrength", 0.62f);
            material.SetFloat("_ShadowStrength", 0.5f);
            material.SetFloat("_RimStrength", 0.1f);
            Save(material);
            return material;
        }

        internal static Material SavedMaterial(string name, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException($"Shader {shaderName} is missing.");

            string path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            return material;
        }

        internal static void Save(Material material)
        {
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }

        /// <summary>A rounded lump about a metre across, for a particle to scale down: a seed, a clod.</summary>
        internal static Mesh SaveMesh(string name, Vector3 size, float radius, int roundSegments)
        {
            var shape = new SmoothMesh(1);
            shape.RoundedBox(0, Vector3.zero, size, radius, Quaternion.identity, roundSegments);
            return shape.BuildAsset($"{Folder}/{name}.asset", name, out List<int> _);
        }

        // --- The pot ---------------------------------------------------------------------------------

        /// <summary>pfPlantPot: the burst on the middle of the soil, the prefab nested so rebuilding it updates every pot.</summary>
        internal static string DressPot()
        {
            var burst = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject root = PrefabUtility.LoadPrefabContents(PotPrefabPath);

            try
            {
                Transform existing = root.transform.Find(BurstName);
                bool nested = existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == burst;
                if (existing != null && !nested)
                {
                    Object.DestroyImmediate(existing.gameObject);
                    existing = null;
                }

                if (existing == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(burst, root.transform);
                    instance.name = BurstName;
                    existing = instance.transform;
                }

                existing.SetLocalPositionAndRotation(new Vector3(0.5f, SoilBed.Crown, 0.5f), Quaternion.identity);
                existing.localScale = Vector3.one;

                PrefabUtility.SaveAsPrefabAsset(root, PotPrefabPath);
                return $"pfPlantPot: {(nested ? "burst already nested; it updates with the prefab" : "burst nested on the soil")}.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        internal static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        internal static Color Hex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out Color c);
            return c;
        }

        internal static Color Fade(Color c, float alpha)
        {
            c.a = alpha;
            return c;
        }
    }
}
