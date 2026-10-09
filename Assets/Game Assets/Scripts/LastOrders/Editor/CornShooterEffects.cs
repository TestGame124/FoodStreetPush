using System.Collections.Generic;
using LastOrders.Effects;
using UnityEditor;
using UnityEngine;
using C = LastOrders.EditorTools.CornShooterModel;
using FX = LastOrders.EditorTools.PlantingEffects;
using T = LastOrders.EditorTools.ToastapultEffects;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The Corn Shooter's throws as particles: the cough of chaff and husk out of the launch tube as a
    /// cob leaves it, and the cob bursting where it lands - kernels sprayed every way and bouncing,
    /// chunks of cob, husk leaves fluttering down, a ring of dust.
    ///
    /// Sized to the thrown cob, which is twice life size so it reads from the game camera: its kernels
    /// and chunks are scaled up with it.
    ///
    /// Pooled one-shot effects (<see cref="PooledEffect"/>) played through the game's effects pool, on
    /// the Toastapult's particle plumbing: soft sprites on the project's soft-particle shader, partly
    /// lit, and solid lumps on the lit vertex-colour material. Nothing glows - it is corn.
    /// </summary>
    public static class CornShooterEffects
    {
        public const string LaunchPath = EffectPrefabGenerator.Folder + "/Fx_CornLaunch.prefab";
        public const string ImpactPath = EffectPrefabGenerator.Folder + "/Fx_CornImpact.prefab";

        const string Folder = C.Folder + "/Fx";

        static readonly Color Chaff = new Color(0.74f, 0.68f, 0.52f);
        static readonly Color Dust = new Color(0.62f, 0.57f, 0.5f);

        sealed class Materials
        {
            public Material Puff, Leaf, Ring, Lump;
            public Mesh Kernel, Chunk;
        }

        static Materials Mats()
        {
            ContentGenerator.EnsureFolder(Folder);

            return new Materials
            {
                Puff = T.Soft(Folder, "CornFx_Puff", T.Texture("Particle_Puff", FX.Puff), additive: false, lit: 0.6f, pull: 0.2f),
                Leaf = T.Soft(Folder, "CornFx_Husk", T.Texture("Particle_Leaf", FX.Leaf), additive: false, lit: 0.7f, pull: 0.1f),
                Ring = T.Soft(Folder, "CornFx_Ring", T.Texture("Particle_Ring", FX.Ring), additive: false, lit: 0.4f, pull: 0.05f),
                Lump = FX.SolidMaterial(),
                Kernel = LumpMesh("CornFx_Kernel", new Vector3(1f, 0.85f, 0.6f), 0.3f),
                Chunk = LumpMesh("CornFx_CobChunk", new Vector3(1f, 0.9f, 1.5f), 0.35f),
            };
        }

        /// <summary>The cob leaving the tube: a cough of chaff, a few shreds of husk and a kernel or two knocked loose.</summary>
        public static PooledEffect Launch()
        {
            Materials m = Mats();
            var root = new GameObject("Fx_CornLaunch");

            try
            {
                T.Burst(root.transform, "Chaff", m.Puff, count: 6, lifetime: (0.4f, 0.7f), speed: (0.8f, 1.6f), size: (0.16f, 0.26f), cone: 25f,
                        colours: new ParticleSystem.MinMaxGradient(Fade(Chaff, 0.55f), Fade(Chaff * 0.9f, 0.4f)), grow: FX.Curve((0f, 0.5f), (1f, 1.8f)),
                        gravity: -0.02f, fade: true, drag: true);
                Husks(root.transform, m.Leaf, count: 3, speed: (0.6f, 1.3f), cone: 40f, size: (0.12f, 0.2f));
                Lumps(root.transform, "Kernels", m.Kernel, m.Lump, count: 3, speed: (1.4f, 2.4f), cone: 30f, size: (0.07f, 0.09f),
                      C.Kernel, C.KernelDark, bounce: false);

                root.AddComponent<PooledEffect>();
                return Save(root, LaunchPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Where a cob lands: it bursts into kernels and chunks, its husk flutters off, and dust rings out along the ground.</summary>
        public static PooledEffect Impact()
        {
            Materials m = Mats();
            var root = new GameObject("Fx_CornImpact");

            try
            {
                Lumps(root.transform, "Kernels", m.Kernel, m.Lump, count: 26, speed: (1.8f, 3.6f), cone: 0f, size: (0.08f, 0.11f),
                      C.Kernel, C.KernelDark, bounce: true);
                Lumps(root.transform, "Chunks", m.Chunk, m.Lump, count: 5, speed: (1.2f, 2.2f), cone: 0f, size: (0.15f, 0.2f),
                      C.Kernel, C.KernelPale, bounce: true);
                Husks(root.transform, m.Leaf, count: 6, speed: (1.2f, 2.4f), cone: 0f, size: (0.18f, 0.28f));
                T.Burst(root.transform, "Dust", m.Puff, count: 8, lifetime: (0.5f, 0.9f), speed: (0.6f, 1.3f), size: (0.3f, 0.45f), cone: 0f,
                        colours: new ParticleSystem.MinMaxGradient(Fade(Dust, 0.5f), Fade(Chaff, 0.4f)), grow: FX.Curve((0f, 0.6f), (1f, 1.9f)),
                        gravity: -0.03f, fade: true, drag: true);
                T.ShockRing(root.transform, m.Ring);

                root.AddComponent<PooledEffect>();
                return Save(root, ImpactPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // --- Parts -----------------------------------------------------------------------------------------

        /// <summary>Solid bits thrown out - kernels, chunks of cob - falling and, on a landing, bouncing before they shrink away.</summary>
        static void Lumps(Transform parent, string name, Mesh mesh, Material material, short count, (float, float) speed, float cone,
                          (float, float) size, Color a, Color b, bool bounce)
        {
            ParticleSystem ps = T.System(parent, name, material, ParticleSystemRenderMode.Mesh, mesh);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(bounce ? 0.7f : 0.3f, bounce ? 1.05f : 0.5f);
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

            // Effects play unturned: a launch throws them up a cone, a landing every way over the top half.
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

        /// <summary>Husk leaves thrown up, drifting and turning as they fall.</summary>
        static void Husks(Transform parent, Material material, short count, (float, float) speed, float cone, (float, float) size)
        {
            ParticleSystem ps = T.System(parent, "Husk", material);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
            main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(C.Husk, C.HuskPale);
            main.gravityModifier = 0.45f;
            main.maxParticles = count * 2;

            FX.Burst(ps, 0f, count);
            T.Spread(ps, cone, 0.05f);
            FX.Spin(ps, -360f, 360f);
            FX.Drag(ps, 0.8f, 0.3f);
            FX.FadeOut(ps, 0.6f);
        }

        // --- Plumbing --------------------------------------------------------------------------------------

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

        static PooledEffect Save(GameObject root, string path)
        {
            ContentGenerator.EnsureFolder(EffectPrefabGenerator.Folder);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            return prefab.GetComponent<PooledEffect>();
        }
    }
}
