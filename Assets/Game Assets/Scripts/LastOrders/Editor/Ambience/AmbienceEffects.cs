using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using B = LastOrders.EditorTools.BuildingKit;
using FX = LastOrders.EditorTools.PlantingEffects;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The level's weather and drift, painted and set up in code: the tiling cloud-shade texture the sun
    /// wears as its cookie; seed fluff and pollen floating in the light; dead leaves for the gusts to
    /// throw; fireflies; and smoke rising off the fires still burning in the suburbs, with embers at
    /// their feet that glow at night.
    ///
    /// Particles are on the planting bursts' soft-particle shader, in this folder's own materials.
    /// The systems are only built here; <see cref="LastOrders.Ambience.AmbientDrift"/> and
    /// <see cref="LastOrders.Ambience.AmbientInsects"/> steer them with the wind and the dark.
    /// </summary>
    internal static class AmbienceEffects
    {
        const string Folder = AmbienceKit.EffectsFolder;
        const int CloudSize = 256;

        // --- The clouds ------------------------------------------------------------------------------

        /// <summary>
        /// Soft cloud shade that tiles: white where the sun is clear, down to <c>1 - depth</c> under the
        /// thickest of a cloud. Several octaves of noise wrapped on the tile, cut off into separate
        /// clouds with soft edges so most of the ground is in sun.
        /// </summary>
        internal static Texture2D Clouds(float cover = 0.42f, float depth = 0.34f)
        {
            ContentGenerator.EnsureFolder(Folder);
            string path = $"{Folder}/CloudShadows.png";

            var texture = new Texture2D(CloudSize, CloudSize, TextureFormat.RGB24, false);
            var pixels = new Color[CloudSize * CloudSize];
            var rng = new System.Random(20261010);
            var lattice = new float[5][];
            int[] cells = { 4, 8, 16, 32, 64 };
            for (int o = 0; o < cells.Length; o++)
            {
                lattice[o] = new float[cells[o] * cells[o]];
                for (int i = 0; i < lattice[o].Length; i++) lattice[o][i] = (float)rng.NextDouble();
            }

            float[] weights = { 0.5f, 0.26f, 0.13f, 0.07f, 0.04f };
            for (int y = 0; y < CloudSize; y++)
            {
                for (int x = 0; x < CloudSize; x++)
                {
                    float u = x / (float)CloudSize, v = y / (float)CloudSize, n = 0f;
                    for (int o = 0; o < cells.Length; o++) n += weights[o] * Wrapped(lattice[o], cells[o], u, v);

                    // Thin edges, full middles: a cloud, not a smudge.
                    float shade = FX.Smooth(Mathf.InverseLerp(1f - cover, 1f - cover + 0.16f, n));
                    float value = 1f - depth * shade;
                    pixels[y * CloudSize + x] = new Color(value, value, value, 1f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Smooth value noise on a lattice that wraps round the tile.</summary>
        static float Wrapped(float[] lattice, int cells, float u, float v)
        {
            float fx = u * cells, fy = v * cells;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = FX.Smooth(fx - x0), ty = FX.Smooth(fy - y0);
            float At(int x, int y) => lattice[((y % cells + cells) % cells) * cells + (x % cells + cells) % cells];
            return Mathf.Lerp(Mathf.Lerp(At(x0, y0), At(x0 + 1, y0), tx), Mathf.Lerp(At(x0, y0 + 1), At(x0 + 1, y0 + 1), tx), ty);
        }

        // --- Particles -------------------------------------------------------------------------------

        /// <summary>Seed fluff and pollen: tiny soft motes adrift in the light. Kept playing in world space.</summary>
        internal static ParticleSystem Pollen(Transform parent)
        {
            ParticleSystem ps = Fresh(parent, "Pollen", Sprite("Ambience_Mote", Dot(), false, 0.55f, 0f));
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 10f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 14f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.94f, 0.88f, 0.75f), new Color(0.93f, 0.9f, 0.76f, 0.6f));
            main.maxParticles = 220;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            // About the view at the game's usual zoom, not the whole level: thin air is wasted motes.
            shape.scale = new Vector3(34f, 4f, 28f);
            shape.position = new Vector3(0f, 2.2f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.15f;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            FadeInOut(ps, 0.2f, 0.75f);
            return ps;
        }

        /// <summary>Dead leaves, thrown by the gusts and left to tumble down and skid to a stop. Emitted into by hand.</summary>
        internal static ParticleSystem Leaves(Transform parent, Transform ground)
        {
            ParticleSystem ps = Fresh(parent, "GustLeaves", Solid(), ParticleSystemRenderMode.Mesh, LeafMesh());
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 10f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.11f, 0.17f);
            main.startRotation3D = true;
            main.startRotationX = FX.Turn();
            main.startRotationY = FX.Turn();
            main.startRotationZ = FX.Turn();
            main.startColor = RandomOf(B.Hex("8A6E45"), B.Hex("7A5A3B"), B.Hex("9B8650"), B.Hex("6E6A3A"), B.Hex("A0763F"));
            main.gravityModifier = 0.16f;
            main.maxParticles = 120;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = false;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.9f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.5f;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            FX.Spin3D(ps, -420f, 420f);

            // Down onto the ground and skidding along it, slowing with every touch.
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.Planes;
            collision.SetPlane(0, ground);
            collision.dampen = new ParticleSystem.MinMaxCurve(0.18f);
            collision.bounce = new ParticleSystem.MinMaxCurve(0.08f);
            collision.radiusScale = 0.2f;

            FadeInOut(ps, 0.03f, 0.8f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            return ps;
        }

        /// <summary>Fireflies: soft green-gold glows blinking low over the grass. Kept playing, emitted into by hand over open ground.</summary>
        internal static ParticleSystem Fireflies(Transform parent)
        {
            ParticleSystem ps = Fresh(parent, "Fireflies", Sprite("Ambience_Firefly", Glow(), true, 0f, 0.1f));
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 10f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.46f);
            main.startColor = new ParticleSystem.MinMaxGradient(B.Hex("E4E88E"), B.Hex("F0DA7E"));
            main.maxParticles = 90;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = false;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.45f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.2f;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            // Blinking: a slow glow up, a hold, a fade, dark a while, again.
            var blink = new Gradient();
            blink.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.9f, 0.24f), new GradientAlphaKey(0f, 0.36f),
                    new GradientAlphaKey(0f, 0.55f), new GradientAlphaKey(1f, 0.68f), new GradientAlphaKey(0f, 0.85f), new GradientAlphaKey(0f, 1f),
                });
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(blink);

            ps.GetComponent<ParticleSystemRenderer>().sortingFudge = -10f;
            return ps;
        }

        /// <summary>
        /// A fire still smouldering: a column of grey smoke climbing and spreading, leaning with the wind,
        /// and a few embers lifting off its foot.
        /// </summary>
        internal static ParticleSystem Smoke(Transform parent, string name, Vector3 at)
        {
            Transform old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            ParticleSystem ps = FX.NewSystem(parent, name, Sprite("Ambience_Smoke", Puff(), false, 0.85f, 0.3f), ParticleSystemRenderMode.Billboard);
            ps.transform.position = at;
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 10f;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 2.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(1f, 1.5f);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(B.Hex("7C7873"), B.Hex("8E8A85"));
            main.maxParticles = 40;

            var emission = ps.emission;
            emission.rateOverTime = 2.6f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 7f;
            shape.radius = 0.5f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.15f;
            noise.scrollSpeed = 0.1f;

            FX.Grow(ps, FX.Curve((0f, 0.5f), (0.3f, 1.5f), (1f, 3.4f)));
            FX.Spin(ps, -12f, 12f);

            // Darker at the foot, thinning and paling as it climbs.
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(B.Hex("8E8A85"), 0f), new GradientColorKey(B.Hex("E2DFD8"), 0.35f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.07f), new GradientAlphaKey(0.28f, 0.5f), new GradientAlphaKey(0f, 1f) });
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(fade);

            Embers(ps.transform);
            FireGlow(ps.transform);
            return ps;
        }

        /// <summary>The fire's warm glow on the ground round it: soft, flickering, added light that tells at night.</summary>
        static void FireGlow(Transform plume)
        {
            ParticleSystem ps = FX.NewSystem(plume, "Glow", Sprite("Ambience_Ember", Glow(), true, 0f, 0.4f), ParticleSystemRenderMode.Billboard);
            ps.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(2.2f, 3.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.88f, 0.48f, 0.22f, 0.5f), new Color(0.94f, 0.6f, 0.3f, 0.65f));
            main.maxParticles = 8;

            var emission = ps.emission;
            emission.rateOverTime = 6f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            FadeInOut(ps, 0.3f, 0.6f);
            ps.GetComponent<ParticleSystemRenderer>().sortingFudge = 20f;
        }

        static void Embers(Transform plume)
        {
            ParticleSystem ps = FX.NewSystem(plume, "Embers", Sprite("Ambience_Ember", Glow(), true, 0f, 0.15f), ParticleSystemRenderMode.Billboard);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(B.Hex("E3884A"), B.Hex("F0B060"));
            main.maxParticles = 20;

            var emission = ps.emission;
            emission.rateOverTime = 4f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 0.45f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 1.2f;

            FadeInOut(ps, 0.05f, 0.5f);
        }

        // --- Plumbing --------------------------------------------------------------------------------

        static ParticleSystem Fresh(Transform parent, string name, Material material,
                                    ParticleSystemRenderMode mode = ParticleSystemRenderMode.Billboard, Mesh mesh = null)
        {
            Transform old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            return FX.NewSystem(parent, name, material, mode, mesh);
        }

        static void FadeInOut(ParticleSystem ps, float inBy, float holdUntil)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inBy), new GradientAlphaKey(1f, holdUntil), new GradientAlphaKey(0f, 1f) });
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>One of the given colours for each particle, not a blend of them.</summary>
        static ParticleSystem.MinMaxGradient RandomOf(params Color[] colours)
        {
            var keys = new GradientColorKey[colours.Length];
            for (int i = 0; i < colours.Length; i++) keys[i] = new GradientColorKey(colours[i], (i + 0.5f) / colours.Length);
            var gradient = new Gradient { mode = GradientMode.Fixed };
            gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
        }

        static Material Sprite(string name, Texture2D texture, bool additive, float lit, float pull)
        {
            Material material = Saved(name, "LastOrders/SoftParticle");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Additive", additive ? 1f : 0f);
            material.SetFloat("_SrcBlend", (float)(additive ? BlendMode.One : BlendMode.SrcAlpha));
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_Lit", lit);
            material.SetFloat("_DepthPull", pull);
            material.enableInstancing = true;
            Save(material);
            return material;
        }

        /// <summary>Lit like the props, coloured by each particle: the leaves.</summary>
        static Material Solid()
        {
            Material material = Saved("Ambience_Solid", "LastOrders/Stylized");
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Wrap", 0.7f);
            material.SetFloat("_LightStrength", 0.85f);
            material.SetFloat("_AmbientStrength", 0.7f);
            material.SetFloat("_ShadowStrength", 0.5f);
            material.SetFloat("_RimStrength", 0.08f);
            Save(material);
            return material;
        }

        static Material Saved(string name, string shaderName)
        {
            ContentGenerator.EnsureFolder(Folder);
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

        static void Save(Material material)
        {
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }

        /// <summary>A leaf a metre long, for the particles to scale down: a pointed oval, cupped, a little curled at the tip.</summary>
        static Mesh LeafMesh()
        {
            string path = $"{Folder}/Ambience_Leaf.asset";
            var m = new B.Model();
            m.Surface((p, n) =>
            {
                float rib = 1f - FX.Smooth(Mathf.Abs(p.x) / 0.05f);
                float v = Mathf.Lerp(0.82f, 1f, rib * 0.6f + 0.4f * Mathf.InverseLerp(-0.5f, 0.5f, p.z));
                return new Color(v, v, v);
            }, 6, 10, (u, v) =>
            {
                float z = Mathf.Lerp(-0.5f, 0.5f, v);
                float half = 0.3f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(v, 0.85f))), 0.8f);
                float x = Mathf.Lerp(-half, half, u);
                float y = 0.12f * (x * x / 0.09f) + 0.1f * v * v;
                return new Vector3(x, y, z);
            });

            // Both faces: a leaf is seen from either side as it tumbles.
            m.Surface((p, n) => new Color(0.78f, 0.78f, 0.78f), 6, 10, (u, v) =>
            {
                float z = Mathf.Lerp(-0.5f, 0.5f, v);
                float half = 0.3f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(v, 0.85f))), 0.8f);
                float x = Mathf.Lerp(half, -half, u);
                float y = 0.12f * (x * x / 0.09f) + 0.1f * v * v - 0.01f;
                return new Vector3(x, y, z);
            });

            return m.Save(path, "Ambience_Leaf");
        }

        // --- Textures ------------------------------------------------------------------------------

        static Texture2D Dot() => Paint("Particle_Mote", (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return new Color(1f, 1f, 1f, Mathf.Exp(-r * r * 9f) * (1f - FX.Smooth((r - 0.85f) / 0.15f)));
        });

        static Texture2D Glow() => Paint("Particle_Glow", (x, y) =>
        {
            float r2 = x * x + y * y;
            float core = Mathf.Exp(-r2 * 30f);
            float halo = Mathf.Exp(-r2 * 6f) * 0.45f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(core + halo) * (1f - FX.Smooth((Mathf.Sqrt(r2) - 0.85f) / 0.15f)));
        });

        static Texture2D Puff()
        {
            const string shared = FX.Folder + "/Particle_Puff.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(shared);
            return existing != null ? existing : Paint("Particle_Puff", FX.Puff);
        }

        /// <summary>Paints a 128 px sprite from <paramref name="pixel"/>(x, y), each in -1..1, into this folder.</summary>
        static Texture2D Paint(string name, Func<float, float, Color> pixel)
        {
            ContentGenerator.EnsureFolder(Folder);
            string path = $"{Folder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int size = 128, samples = 3;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                {
                    Color sum = Color.clear;
                    for (int sj = 0; sj < samples; sj++)
                        for (int si = 0; si < samples; si++)
                            sum += pixel(((i + (si + 0.5f) / samples) / size) * 2f - 1f, ((j + (sj + 0.5f) / samples) / size) * 2f - 1f);
                    pixels[j * size + i] = sum / (samples * samples);
                }

            texture.SetPixels(pixels);
            texture.Apply(false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
