using System.Collections.Generic;
using LastOrders.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using FX = LastOrders.EditorTools.PlantingEffects;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The Toastapult's shot as particles: the fire, smoke and sparks a slice of flaming toast trails
    /// through the air, the pop as it leaves the slot, and the burst where it lands.
    ///
    /// The launch and the impact are pooled one-shot effects (<see cref="PooledEffect"/>), played by
    /// the defense through the game's effects pool, so two shots landing together each get their
    /// own. The trail is part of the slice itself - systems that emit as it moves and are left to
    /// die away after it lands (<see cref="LastOrders.Defense.DefenseProjectiles"/>).
    ///
    /// Soft round sprites on the project's soft-particle shader - the fire and sparks additive so
    /// they glow at night as by day, the smoke alpha-blended and partly lit - and the crumbs solid
    /// little lumps of toast on the lit vertex-colour material, bouncing off the ground.
    /// </summary>
    public static class ToastapultEffects
    {
        public const string LaunchPath = EffectPrefabGenerator.Folder + "/Fx_ToastLaunch.prefab";
        public const string ImpactPath = EffectPrefabGenerator.Folder + "/Fx_ToastImpact.prefab";

        const string Folder = "Assets/Game Assets/Content/LastOrders/Defense/" + EnvKit.Style + "/Toastapult/Fx";

        static readonly Color Toast = PrimitiveKit.Hex("C69A5B");
        static readonly Color Crust = PrimitiveKit.Hex("5E3E24");

        static readonly Color FireCore = new Color(1f, 0.92f, 0.62f);
        static readonly Color FireMid = new Color(1f, 0.58f, 0.18f);
        static readonly Color FireEdge = new Color(0.85f, 0.22f, 0.08f);
        static readonly Color Smoke = new Color(0.36f, 0.34f, 0.33f);
        static readonly Color SmokeLight = new Color(0.62f, 0.6f, 0.58f);

        sealed class Materials
        {
            public Material Fire, Spark, Smoke, Ring, Crumb;
        }

        static Materials materials;

        static Materials Mats()
        {
            ContentGenerator.EnsureFolder(Folder);

            Texture2D puff = Texture("Particle_Puff", FX.Puff);
            Texture2D glow = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FX.Folder}/Particle_Glow.png") ?? puff;
            Texture2D ring = Texture("Particle_Ring", FX.Ring);

            materials = new Materials
            {
                Fire = Soft("ToastFx_Fire", glow, additive: true, lit: 0f, pull: 0.3f),
                Spark = Soft("ToastFx_Spark", glow, additive: true, lit: 0f, pull: 0.15f),
                Smoke = Soft("ToastFx_Smoke", puff, additive: false, lit: 0.55f, pull: 0.25f),
                Ring = Soft("ToastFx_Ring", ring, additive: false, lit: 0.4f, pull: 0.05f),
                Crumb = FX.SolidMaterial(),
            };
            return materials;
        }

        // --- The trail, on the slice ----------------------------------------------------------------

        /// <summary>
        /// The trail systems under <paramref name="projectile"/>: a tongue of fire streaming off it, a
        /// line of smoke, and sparks - all emitted by distance travelled, in the world, so they stay
        /// where the slice has been. Off until it is thrown.
        /// </summary>
        public static ParticleSystem[] Trail(Transform projectile)
        {
            Materials m = Mats();

            // Short and warm: a tongue of flame off the slice, not a rocket's exhaust - the toast has to stay the thing you see.
            ParticleSystem fire = System(projectile, "Trail_Fire", m.Fire);
            {
                var main = fire.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.22f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.42f);
                main.startRotation = FX.Turn();
                main.startColor = new Color(1f, 0.9f, 0.8f);
                main.maxParticles = 60;

                var emission = fire.emission;
                emission.rateOverDistance = 20f;

                var shape = fire.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.06f;

                ColourOverLife(fire, Fade((FireMid, 0f), (FireMid, 0.3f), (FireEdge, 1f)), (0.85f, 0f), (0.6f, 0.4f), (0f, 1f));
                FX.Grow(fire, FX.Curve((0f, 1f), (1f, 0.25f)));
            }

            ParticleSystem smoke = System(projectile, "Trail_Smoke", m.Smoke);
            {
                var main = smoke.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.32f);
                main.startRotation = FX.Turn();
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(Smoke.r, Smoke.g, Smoke.b, 0.55f), new Color(SmokeLight.r, SmokeLight.g, SmokeLight.b, 0.45f));
                main.maxParticles = 50;

                var emission = smoke.emission;
                emission.rateOverDistance = 7f;

                FX.Rise(smoke, 0.15f, 0.35f);
                FX.Grow(smoke, FX.Curve((0f, 0.6f), (1f, 2.2f)));
                FX.FadeOut(smoke, 0.3f);
                FX.Spin(smoke, -40f, 40f);
            }

            ParticleSystem sparks = System(projectile, "Trail_Sparks", m.Spark);
            {
                var main = sparks.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.1f);
                main.startColor = new ParticleSystem.MinMaxGradient(FireCore, FireMid);
                main.gravityModifier = 0.35f;
                main.maxParticles = 60;

                var emission = sparks.emission;
                emission.rateOverDistance = 12f;

                var shape = sparks.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.08f;

                ColourOverLife(sparks, Fade((Color.white, 0f), (FireMid, 0.5f), (FireEdge, 1f)), (1f, 0f), (1f, 0.6f), (0f, 1f));
                FX.Grow(sparks, FX.Curve((0f, 1f), (1f, 0.3f)));
            }

            return new[] { fire, smoke, sparks };
        }

        /// <summary>A thin wisp of smoke off the exhaust pipe, all the time: it is on, and it is hot.</summary>
        public static ParticleSystem Exhaust(Transform parent, Vector3 at)
        {
            Materials m = Mats();
            ParticleSystem ps = System(parent, "ExhaustSmoke", m.Smoke);
            ps.transform.localPosition = at;

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.24f);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(SmokeLight.r, SmokeLight.g, SmokeLight.b, 0.5f), new Color(Smoke.r, Smoke.g, Smoke.b, 0.45f));
            main.maxParticles = 20;

            var emission = ps.emission;
            emission.rateOverTime = 2.5f;

            FX.Rise(ps, 0.35f, 0.55f);
            FX.Grow(ps, FX.Curve((0f, 0.5f), (1f, 2f)));
            FX.FadeOut(ps, 0.35f);
            FX.Spin(ps, -30f, 30f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.3f;
            return ps;
        }

        // --- The one-shots ------------------------------------------------------------------------------

        /// <summary>The pop out of the slots: a puff of smoke, a spit of sparks, a flash and a few crumbs.</summary>
        public static PooledEffect Launch(Mesh crumb)
        {
            Materials m = Mats();
            var root = new GameObject("Fx_ToastLaunch");

            try
            {
                Flash(root.transform, m.Fire, size: 0.7f, lifetime: 0.14f);
                Burst(root.transform, "Smoke", m.Smoke, count: 8, lifetime: (0.6f, 1f), speed: (0.7f, 1.5f), size: (0.24f, 0.38f), cone: 28f,
                      colours: new ParticleSystem.MinMaxGradient(new Color(SmokeLight.r, SmokeLight.g, SmokeLight.b, 0.6f), new Color(Smoke.r, Smoke.g, Smoke.b, 0.5f)),
                      grow: FX.Curve((0f, 0.6f), (1f, 1.9f)), gravity: -0.05f, fade: true);
                Burst(root.transform, "Sparks", m.Spark, count: 14, lifetime: (0.3f, 0.6f), speed: (2f, 4f), size: (0.06f, 0.1f), cone: 35f,
                      colours: new ParticleSystem.MinMaxGradient(FireCore, FireMid), grow: FX.Curve((0f, 1f), (1f, 0.2f)), gravity: 1f, fade: true);
                Crumbs(root.transform, crumb, m.Crumb, count: 6, speed: (1f, 2f), cone: 30f);

                root.AddComponent<PooledEffect>();
                return Save(root, LaunchPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Where the toast lands: a flash, a fireball, a ring of dust blown out along the ground, a
        /// cloud of smoke, a spray of embers and the slice going to crumbs.
        /// </summary>
        public static PooledEffect Impact(Mesh crumb)
        {
            Materials m = Mats();
            var root = new GameObject("Fx_ToastImpact");

            try
            {
                Flash(root.transform, m.Fire, size: 1.5f, lifetime: 0.13f);

                ParticleSystem fireball = Burst(root.transform, "Fireball", m.Fire, count: 7, lifetime: (0.25f, 0.42f), speed: (0.5f, 1.4f),
                                                size: (0.4f, 0.7f), cone: 0f, colours: new ParticleSystem.MinMaxGradient(Color.white), grow: FX.Curve((0f, 0.7f), (1f, 1.3f)),
                                                gravity: -0.1f, fade: false);
                ColourOverLife(fireball, Fade((FireCore, 0f), (FireMid, 0.3f), (FireEdge, 1f)), (1f, 0f), (0.8f, 0.5f), (0f, 1f));

                Burst(root.transform, "Smoke", m.Smoke, count: 10, lifetime: (0.9f, 1.4f), speed: (0.8f, 1.8f), size: (0.35f, 0.55f), cone: 0f,
                      colours: new ParticleSystem.MinMaxGradient(new Color(Smoke.r, Smoke.g, Smoke.b, 0.6f), new Color(SmokeLight.r, SmokeLight.g, SmokeLight.b, 0.5f)),
                      grow: FX.Curve((0f, 0.6f), (1f, 2.1f)), gravity: -0.06f, fade: true, drag: true);

                Burst(root.transform, "Embers", m.Spark, count: 22, lifetime: (0.4f, 0.8f), speed: (2.5f, 4.5f), size: (0.06f, 0.11f), cone: 0f,
                      colours: new ParticleSystem.MinMaxGradient(FireCore, FireMid), grow: FX.Curve((0f, 1f), (1f, 0.25f)), gravity: 1.2f, fade: true);

                ShockRing(root.transform, m.Ring);
                Crumbs(root.transform, crumb, m.Crumb, count: 14, speed: (2f, 3.5f), cone: 0f);

                root.AddComponent<PooledEffect>();
                return Save(root, ImpactPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // --- Parts --------------------------------------------------------------------------------------

        /// <summary>One bright soft glow that swells and is gone in a blink.</summary>
        static void Flash(Transform parent, Material material, float size, float lifetime)
        {
            ParticleSystem ps = System(parent, "Flash", material);
            var main = ps.main;
            main.loop = false;
            main.duration = lifetime;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = FireCore;
            main.maxParticles = 2;

            FX.Burst(ps, 0f, 1);
            FX.Grow(ps, FX.Curve((0f, 0.6f), (0.3f, 1f), (1f, 0.4f)));
            ColourOverLife(ps, Fade((Color.white, 0f), (FireMid, 1f)), (1f, 0f), (0f, 1f));
        }

        /// <summary>
        /// A burst of <paramref name="count"/> soft sprites thrown out up a cone of <paramref name="cone"/>
        /// degrees (0: every way over a hemisphere).
        /// </summary>
        internal static ParticleSystem Burst(Transform parent, string name, Material material, short count, (float, float) lifetime, (float, float) speed,
                                    (float, float) size, float cone, ParticleSystem.MinMaxGradient colours, AnimationCurve grow, float gravity, bool fade,
                                    bool drag = false)
        {
            ParticleSystem ps = System(parent, name, material);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.Item1, lifetime.Item2);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
            main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
            main.startRotation = FX.Turn();
            main.startColor = colours;
            main.gravityModifier = gravity;
            main.maxParticles = count * 2;

            FX.Burst(ps, 0f, count);
            Spread(ps, cone, 0.08f);
            FX.Grow(ps, grow);
            if (fade) FX.FadeOut(ps, 0.35f);
            if (drag) FX.Drag(ps, 0.5f, 0.35f);
            return ps;
        }

        /// <summary>Dust blown out flat along the ground from where it hit.</summary>
        internal static void ShockRing(Transform parent, Material material)
        {
            ParticleSystem ps = System(parent, "Ring", material, ParticleSystemRenderMode.HorizontalBillboard);
            ps.transform.localPosition = Vector3.down * 0.85f;

            var main = ps.main;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = 0.35f;
            main.startSpeed = 0f;
            main.startSize = 0.4f;
            main.startColor = new Color(0.8f, 0.74f, 0.64f, 0.75f);
            main.maxParticles = 2;

            FX.Burst(ps, 0f, 1);
            FX.Grow(ps, FX.Curve((0f, 0.4f), (1f, 4.5f)));
            FX.FadeOut(ps, 0.2f);
        }

        /// <summary>Lumps of toast thrown up and bouncing on the ground before they shrink away.</summary>
        static void Crumbs(Transform parent, Mesh crumb, Material material, short count, (float, float) speed, float cone)
        {
            ParticleSystem ps = System(parent, "Crumbs", material, ParticleSystemRenderMode.Mesh, crumb);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
            main.startRotation3D = true;
            main.startRotationX = FX.Turn();
            main.startRotationY = FX.Turn();
            main.startRotationZ = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(Toast, Crust);
            main.gravityModifier = 1.4f;
            main.maxParticles = count * 2;

            FX.Burst(ps, 0f, count);
            Spread(ps, cone, 0.1f);
            FX.Spin3D(ps, 300f, 800f);
            FX.Grow(ps, FX.Curve((0f, 1f), (0.75f, 1f), (1f, 0f)));

            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.bounce = 0.3f;
            collision.dampen = 0.55f;
            collision.lifetimeLoss = 0f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.alignment = ParticleSystemRenderSpace.World;
        }

        /// <summary>Up a cone of <paramref name="degrees"/>, or every way over the top half of a sphere when it is zero.</summary>
        internal static void Spread(ParticleSystem ps, float degrees, float radius)
        {
            var shape = ps.shape;
            shape.enabled = true;

            if (degrees <= 0f)
            {
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = radius;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                return;
            }

            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = degrees;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        // --- Plumbing ---------------------------------------------------------------------------------

        /// <summary>The particle system called <paramref name="name"/> under <paramref name="parent"/>, made if missing and reset to a quiet start: world space, nothing on its own.</summary>
        internal static ParticleSystem System(Transform parent, string name, Material material,
                                     ParticleSystemRenderMode mode = ParticleSystemRenderMode.Billboard, Mesh mesh = null)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
            }

            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;

            var ps = t.GetComponent<ParticleSystem>();
            if (ps == null) ps = t.gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new ParticleSystem.Burst[0]);

            var shape = ps.shape;
            shape.enabled = false;

            var renderer = t.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.mesh = mesh;
            renderer.sharedMaterial = material;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        internal static Gradient Fade(params (Color colour, float time)[] keys)
        {
            var colours = new GradientColorKey[keys.Length];
            for (int i = 0; i < keys.Length; i++) colours[i] = new GradientColorKey(keys[i].colour, keys[i].time);

            var gradient = new Gradient();
            gradient.SetKeys(colours, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        /// <summary>Colour over its life from <paramref name="colours"/>, alpha from (alpha, time) <paramref name="alphas"/>.</summary>
        internal static void ColourOverLife(ParticleSystem ps, Gradient colours, params (float alpha, float time)[] alphas)
        {
            var keys = new GradientAlphaKey[alphas.Length];
            for (int i = 0; i < alphas.Length; i++) keys[i] = new GradientAlphaKey(alphas[i].alpha, alphas[i].time);

            var gradient = new Gradient();
            gradient.SetKeys(colours.colorKeys, keys);

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        internal static Texture2D Texture(string name, System.Func<float, float, Color> paint)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FX.Folder}/{name}.png");
            if (existing != null) return existing;

            ContentGenerator.EnsureFolder(FX.Folder);
            return FX.Paint(name, paint);
        }

        /// <summary>The project's soft-particle shader on <paramref name="texture"/>: additive to glow, or alpha-blended and partly lit by the scene.</summary>
        static Material Soft(string name, Texture2D texture, bool additive, float lit, float pull) =>
            Soft(Folder, name, texture, additive, lit, pull);

        /// <summary>The same, saved in <paramref name="folder"/>: for another defense's effects.</summary>
        internal static Material Soft(string folder, string name, Texture2D texture, bool additive, float lit, float pull)
        {
            Shader shader = Shader.Find("LastOrders/SoftParticle");
            if (shader == null) throw new System.InvalidOperationException("Shader LastOrders/SoftParticle is missing.");

            ContentGenerator.EnsureFolder(folder);
            string path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Additive", additive ? 1f : 0f);
            material.SetFloat("_SrcBlend", (float)(additive ? BlendMode.One : BlendMode.SrcAlpha));
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_Lit", lit);
            material.SetFloat("_DepthPull", pull);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        static PooledEffect Save(GameObject root, string path)
        {
            ContentGenerator.EnsureFolder(EffectPrefabGenerator.Folder);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            return prefab.GetComponent<PooledEffect>();
        }
    }
}
