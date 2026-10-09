using System.Collections.Generic;
using LastOrders.Effects;
using UnityEditor;
using UnityEngine;
using FX = LastOrders.EditorTools.PlantingEffects;
using T = LastOrders.EditorTools.ToastapultEffects;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The shots of the three kitchen-garden shooters as particles, on the Toastapult's and the Corn
    /// Shooter's plumbing: soft sprites on the project's soft-particle shader, partly lit, and solid
    /// lumps on the lit vertex-colour material.
    ///
    /// - Tomato Shooter: a cough of steam and juice from the bell; where it lands, a splat - pulp and
    ///   seeds sprayed out, a spatter of juice and a red stain on the ground that fades.
    /// - Cabbage Shooter: a few leaves shaken loose from the bucket; where it lands, a heavy burst of
    ///   torn leaves and chunks and a wide ring of dust the size of its splash.
    /// - Plate Shooter: a puff off the flinger; where it lands, a plate shattering - shards of glaze
    ///   bouncing away and a little white dust.
    ///
    /// Pooled one-shot effects (<see cref="PooledEffect"/>), and one looping wisp of steam for the
    /// pressure cooker's valve.
    /// </summary>
    public static class FoodShooterEffects
    {
        public const string TomatoLaunchPath = EffectPrefabGenerator.Folder + "/Fx_TomatoLaunch.prefab";
        public const string TomatoImpactPath = EffectPrefabGenerator.Folder + "/Fx_TomatoImpact.prefab";
        public const string CabbageLaunchPath = EffectPrefabGenerator.Folder + "/Fx_CabbageLaunch.prefab";
        public const string CabbageImpactPath = EffectPrefabGenerator.Folder + "/Fx_CabbageImpact.prefab";
        public const string PlateLaunchPath = EffectPrefabGenerator.Folder + "/Fx_PlateLaunch.prefab";
        public const string PlateImpactPath = EffectPrefabGenerator.Folder + "/Fx_PlateImpact.prefab";

        const string Folder = "Assets/Game Assets/Content/LastOrders/Defense/" + EnvKit.Style + "/FoodShooterFx";

        static readonly Color SteamWhite = new Color(0.86f, 0.86f, 0.84f);
        static readonly Color Juice = new Color(0.62f, 0.17f, 0.13f);
        static readonly Color JuiceLight = new Color(0.72f, 0.26f, 0.18f);
        static readonly Color Dust = new Color(0.62f, 0.57f, 0.5f);
        static readonly Color DustLight = new Color(0.7f, 0.66f, 0.58f);
        static readonly Color LeafDark = new Color(0.36f, 0.48f, 0.27f);
        static readonly Color LeafPale = new Color(0.62f, 0.7f, 0.47f);
        static readonly Color Glaze = new Color(0.71f, 0.69f, 0.65f);
        static readonly Color GlazeTeal = new Color(0.23f, 0.41f, 0.4f);

        sealed class Materials
        {
            public Material Puff, Leaf, Ring, Splat, Lump;
            public Mesh Pulp, Seed, Chunk, Shard;
        }

        static Materials Mats()
        {
            ContentGenerator.EnsureFolder(Folder);

            return new Materials
            {
                Puff = T.Soft(Folder, "FoodFx_Puff", T.Texture("Particle_Puff", FX.Puff), additive: false, lit: 0.6f, pull: 0.2f),
                Leaf = T.Soft(Folder, "FoodFx_Leaf", T.Texture("Particle_Leaf", FX.Leaf), additive: false, lit: 0.7f, pull: 0.1f),
                Ring = T.Soft(Folder, "FoodFx_Ring", T.Texture("Particle_Ring", FX.Ring), additive: false, lit: 0.4f, pull: 0.05f),
                Splat = T.Soft(Folder, "FoodFx_Splat", T.Texture("Particle_Splat", SplatShape), additive: false, lit: 0.55f, pull: 0.02f),
                Lump = FX.SolidMaterial(),
                Pulp = LumpMesh("FoodFx_Pulp", new Vector3(1f, 0.7f, 1f), 0.35f),
                Seed = LumpMesh("FoodFx_Seed", new Vector3(1f, 0.35f, 0.65f), 0.15f),
                Chunk = LumpMesh("FoodFx_LeafChunk", new Vector3(1f, 0.45f, 1.3f), 0.2f),
                Shard = ShardMesh("FoodFx_Shard"),
            };
        }

        // --- Tomato -------------------------------------------------------------------------------------

        /// <summary>The spit from the bell: a cough of steam and a few drops of juice.</summary>
        public static PooledEffect TomatoLaunch() => Build("Fx_TomatoLaunch", TomatoLaunchPath, (root, m) =>
        {
            T.Burst(root, "Steam", m.Puff, count: 5, lifetime: (0.45f, 0.75f), speed: (0.7f, 1.4f), size: (0.16f, 0.26f), cone: 22f,
                    colours: new ParticleSystem.MinMaxGradient(Fade(SteamWhite, 0.6f), Fade(SteamWhite * 0.92f, 0.45f)),
                    grow: FX.Curve((0f, 0.5f), (1f, 2f)), gravity: -0.05f, fade: true, drag: true);
            Lumps(root, "Drops", m.Pulp, m.Lump, count: 3, lifetime: (0.25f, 0.4f), speed: (1.2f, 2f), cone: 25f, size: (0.03f, 0.05f), Juice, JuiceLight,
                  bounce: false);
        });

        /// <summary>Where a tomato lands: it bursts - pulp and seeds sprayed out, juice spattered, a red stain left on the ground.</summary>
        public static PooledEffect TomatoImpact() => Build("Fx_TomatoImpact", TomatoImpactPath, (root, m) =>
        {
            Lumps(root, "Pulp", m.Pulp, m.Lump, count: 10, lifetime: (0.6f, 0.9f), speed: (1.4f, 2.8f), cone: 0f, size: (0.06f, 0.1f),
                  TomatoShooterModel.Tomato, TomatoShooterModel.Pulp, bounce: true);
            Lumps(root, "Seeds", m.Seed, m.Lump, count: 14, lifetime: (0.5f, 0.8f), speed: (1.6f, 3f), cone: 0f, size: (0.025f, 0.035f),
                  TomatoShooterModel.Seed, TomatoShooterModel.Seed * 0.9f, bounce: true);
            T.Burst(root, "Juice", m.Puff, count: 7, lifetime: (0.3f, 0.5f), speed: (1.5f, 2.6f), size: (0.08f, 0.14f), cone: 0f,
                    colours: new ParticleSystem.MinMaxGradient(Fade(Juice, 0.85f), Fade(JuiceLight, 0.75f)),
                    grow: FX.Curve((0f, 1f), (1f, 0.5f)), gravity: 1.2f, fade: true);
            Stain(root, m.Splat, Juice, size: 0.75f, seconds: 1.6f);
        });

        /// <summary>A thin wisp of steam off the pressure valve, all the time.</summary>
        public static ParticleSystem Steam(Transform parent, Vector3 at)
        {
            Materials m = Mats();
            ParticleSystem ps = T.System(parent, "ValveSteam", m.Puff);
            ps.transform.localPosition = at;

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.16f);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(Fade(SteamWhite, 0.45f), Fade(SteamWhite * 0.95f, 0.35f));
            main.maxParticles = 16;

            var emission = ps.emission;
            emission.rateOverTime = 3f;

            FX.Rise(ps, 0.4f, 0.6f);
            FX.Grow(ps, FX.Curve((0f, 0.4f), (1f, 2.2f)));
            FX.FadeOut(ps, 0.3f);
            FX.Spin(ps, -40f, 40f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.1f;
            noise.frequency = 0.7f;
            noise.scrollSpeed = 0.35f;
            return ps;
        }

        // --- Cabbage ------------------------------------------------------------------------------------

        /// <summary>The fling: a couple of leaves and a puff of dust shaken loose from the bucket.</summary>
        public static PooledEffect CabbageLaunch() => Build("Fx_CabbageLaunch", CabbageLaunchPath, (root, m) =>
        {
            Leaves(root, m.Leaf, count: 3, speed: (0.6f, 1.2f), cone: 40f, size: (0.12f, 0.2f));
            T.Burst(root, "Dust", m.Puff, count: 3, lifetime: (0.4f, 0.6f), speed: (0.3f, 0.7f), size: (0.14f, 0.22f), cone: 45f,
                    colours: new ParticleSystem.MinMaxGradient(Fade(Dust, 0.45f), Fade(DustLight, 0.35f)), grow: FX.Curve((0f, 0.6f), (1f, 1.6f)),
                    gravity: -0.02f, fade: true, drag: true);
        });

        /// <summary>Where a cabbage comes down: a heavy burst of torn leaves and chunks, and dust ringing out as far as its splash reaches.</summary>
        public static PooledEffect CabbageImpact() => Build("Fx_CabbageImpact", CabbageImpactPath, (root, m) =>
        {
            Leaves(root, m.Leaf, count: 12, speed: (1.4f, 2.8f), cone: 0f, size: (0.2f, 0.34f));
            Lumps(root, "Chunks", m.Chunk, m.Lump, count: 9, lifetime: (0.7f, 1.05f), speed: (1.4f, 2.6f), cone: 0f, size: (0.1f, 0.16f),
                  CabbageShooterModel.Outer, CabbageShooterModel.Heart, bounce: true);
            T.Burst(root, "Dust", m.Puff, count: 10, lifetime: (0.6f, 1f), speed: (0.8f, 1.6f), size: (0.35f, 0.55f), cone: 0f,
                    colours: new ParticleSystem.MinMaxGradient(Fade(Dust, 0.55f), Fade(DustLight, 0.45f)), grow: FX.Curve((0f, 0.6f), (1f, 2f)),
                    gravity: -0.03f, fade: true, drag: true);
            Ring(root, m.Ring, diameter: 3.6f, seconds: 0.45f);
        });

        // --- Plate --------------------------------------------------------------------------------------

        /// <summary>The flinger letting go: a little puff off the arm.</summary>
        public static PooledEffect PlateLaunch() => Build("Fx_PlateLaunch", PlateLaunchPath, (root, m) =>
        {
            T.Burst(root, "Puff", m.Puff, count: 4, lifetime: (0.3f, 0.5f), speed: (0.6f, 1.2f), size: (0.12f, 0.2f), cone: 30f,
                    colours: new ParticleSystem.MinMaxGradient(Fade(DustLight, 0.4f), Fade(SteamWhite, 0.3f)), grow: FX.Curve((0f, 0.5f), (1f, 1.7f)),
                    gravity: 0f, fade: true, drag: true);
        });

        /// <summary>Where a plate hits: it shatters - shards of glaze and teal rim bouncing every way, a puff of white dust, a small ring.</summary>
        public static PooledEffect PlateImpact() => Build("Fx_PlateImpact", PlateImpactPath, (root, m) =>
        {
            Lumps(root, "Shards", m.Shard, m.Lump, count: 16, lifetime: (0.6f, 0.95f), speed: (1.8f, 3.4f), cone: 0f, size: (0.06f, 0.1f),
                  Glaze, Glaze * 0.92f, bounce: true);
            Lumps(root, "RimShards", m.Shard, m.Lump, count: 6, lifetime: (0.6f, 0.95f), speed: (1.6f, 3f), cone: 0f, size: (0.05f, 0.08f),
                  GlazeTeal, GlazeTeal * 1.1f, bounce: true);
            T.Burst(root, "Dust", m.Puff, count: 6, lifetime: (0.35f, 0.6f), speed: (0.8f, 1.5f), size: (0.18f, 0.3f), cone: 0f,
                    colours: new ParticleSystem.MinMaxGradient(Fade(Glaze, 0.5f), Fade(DustLight, 0.4f)), grow: FX.Curve((0f, 0.5f), (1f, 1.9f)),
                    gravity: -0.02f, fade: true, drag: true);
            Ring(root, m.Ring, diameter: 2.4f, seconds: 0.3f);
        });

        // --- Parts --------------------------------------------------------------------------------------

        static PooledEffect Build(string name, string path, System.Action<Transform, Materials> parts)
        {
            Materials m = Mats();
            var root = new GameObject(name);

            try
            {
                parts(root.transform, m);
                root.AddComponent<PooledEffect>();
                ContentGenerator.EnsureFolder(EffectPrefabGenerator.Folder);
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<PooledEffect>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Solid bits thrown out - pulp, seeds, chunks, shards - falling and, on a landing, bouncing before they shrink away.</summary>
        static void Lumps(Transform parent, string name, Mesh mesh, Material material, short count, (float, float) lifetime, (float, float) speed,
                          float cone, (float, float) size, Color a, Color b, bool bounce)
        {
            ParticleSystem ps = T.System(parent, name, material, ParticleSystemRenderMode.Mesh, mesh);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.Item1, lifetime.Item2);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
            main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
            main.startRotation3D = true;
            main.startRotationX = FX.Turn();
            main.startRotationY = FX.Turn();
            main.startRotationZ = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.gravityModifier = 1.5f;
            main.maxParticles = count * 3;

            FX.Burst(ps, 0f, count);
            T.Spread(ps, cone, 0.06f);
            FX.Spin3D(ps, 300f, 900f);
            FX.Grow(ps, FX.Curve((0f, 1f), (0.8f, 1f), (1f, 0f)));

            if (bounce)
            {
                var collision = ps.collision;
                collision.enabled = true;
                collision.type = ParticleSystemCollisionType.World;
                collision.mode = ParticleSystemCollisionMode.Collision3D;
                collision.quality = ParticleSystemCollisionQuality.Low;
                collision.bounce = 0.35f;
                collision.dampen = 0.55f;
                collision.lifetimeLoss = 0f;
            }

            ps.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.World;
        }

        /// <summary>Torn leaves thrown up, drifting and turning as they fall.</summary>
        static void Leaves(Transform parent, Material material, short count, (float, float) speed, float cone, (float, float) size)
        {
            ParticleSystem ps = T.System(parent, "Leaves", material);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
            main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(LeafDark, LeafPale);
            main.gravityModifier = 0.45f;
            main.maxParticles = count * 2;

            FX.Burst(ps, 0f, count);
            T.Spread(ps, cone, 0.08f);
            FX.Spin(ps, -360f, 360f);
            FX.Drag(ps, 0.8f, 0.3f);
            FX.FadeOut(ps, 0.6f);
        }

        /// <summary>A stain flat on the ground where it burst, there for <paramref name="seconds"/> and fading.</summary>
        static void Stain(Transform parent, Material material, Color colour, float size, float seconds)
        {
            ParticleSystem ps = T.System(parent, "Stain", material, ParticleSystemRenderMode.HorizontalBillboard);
            ps.transform.localPosition = Vector3.down * 0.78f;

            var main = ps.main;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = seconds;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.85f, size * 1.1f);
            main.startRotation = FX.Turn();
            main.startColor = Fade(colour, 0.85f);
            main.maxParticles = 2;

            FX.Burst(ps, 0f, 1);
            FX.Grow(ps, FX.Curve((0f, 0.3f), (0.08f, 1f), (1f, 1.05f)));
            FX.FadeOut(ps, 0.55f);
        }

        /// <summary>Dust blown out flat along the ground from where it hit, out to <paramref name="diameter"/> metres across.</summary>
        static void Ring(Transform parent, Material material, float diameter, float seconds)
        {
            ParticleSystem ps = T.System(parent, "Ring", material, ParticleSystemRenderMode.HorizontalBillboard);
            ps.transform.localPosition = Vector3.down * 0.75f;

            var main = ps.main;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = seconds;
            main.startSpeed = 0f;
            main.startSize = diameter * 0.2f;
            main.startColor = new Color(0.8f, 0.74f, 0.64f, 0.7f);
            main.maxParticles = 2;

            FX.Burst(ps, 0f, 1);
            FX.Grow(ps, FX.Curve((0f, 0.2f), (1f, 5f)));
            FX.FadeOut(ps, 0.2f);
        }

        // --- Plumbing -----------------------------------------------------------------------------------

        /// <summary>A splat: a lumpy round blob with a few drops thrown off round it, paler toward its middle.</summary>
        static Color SplatShape(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(y, x);
            float edge = 0.5f + 0.09f * Mathf.Sin(a * 5f + 1.1f) + 0.06f * Mathf.Sin(a * 9f + 2.3f) + 0.04f * Mathf.Sin(a * 14f + 0.5f);
            float body = 1f - FX.Smooth(Mathf.InverseLerp(edge - 0.04f, edge + 0.01f, r));

            float drops = 0f;
            for (int k = 0; k < 7; k++)
            {
                float da = k * 0.9f + 0.4f;
                float dr = 0.7f + 0.12f * Mathf.Sin(k * 2.7f);
                float size = 0.05f + 0.03f * Mathf.Sin(k * 1.9f + 1f);
                var c = new Vector2(Mathf.Cos(da), Mathf.Sin(da)) * dr;
                float d = (new Vector2(x, y) - c).magnitude;
                drops = Mathf.Max(drops, 1f - FX.Smooth(Mathf.InverseLerp(size - 0.02f, size + 0.01f, d)));
            }

            float alpha = Mathf.Max(body, drops);
            float v = Mathf.Lerp(1f, 0.85f, Mathf.Clamp01(r / 0.6f));
            return new Color(v, v, v, alpha);
        }

        static Color Fade(Color c, float alpha)
        {
            c.a = alpha;
            return c;
        }

        /// <summary>A rounded lump about a metre across, for a particle to scale down.</summary>
        static Mesh LumpMesh(string name, Vector3 size, float radius)
        {
            var shape = new SmoothMesh(1);
            shape.RoundedBox(0, Vector3.zero, size, radius, Quaternion.identity, 2);
            return shape.BuildAsset($"{Folder}/{name}.asset", name, out List<int> _);
        }

        /// <summary>A shard of plate about a metre across: a thin curved wedge, for a particle to scale down.</summary>
        static Mesh ShardMesh(string name)
        {
            var shape = new SmoothMesh(1);
            shape.Surface(0, 3, 3, (u, v) =>
            {
                float spread = Mathf.Lerp(0.15f, 1f, v);
                float x = (u - 0.5f) * spread;
                float z = v - 0.5f;
                float bow = 0.08f * Mathf.Sin(v * Mathf.PI);
                return new Vector3(x, bow, z);
            });
            shape.Surface(0, 3, 3, (u, v) =>
            {
                float spread = Mathf.Lerp(0.15f, 1f, v);
                float x = (0.5f - u) * spread;
                float z = v - 0.5f;
                float bow = 0.08f * Mathf.Sin(v * Mathf.PI) - 0.08f;
                return new Vector3(x, bow, z);
            });
            return shape.BuildAsset($"{Folder}/{name}.asset", name, out List<int> _);
        }
    }
}
