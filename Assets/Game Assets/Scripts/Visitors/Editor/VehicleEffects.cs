using LastOrders.EditorTools;
using UnityEditor;
using UnityEngine;
using FX = LastOrders.EditorTools.PlantingEffects;
using T = LastOrders.EditorTools.ToastapultEffects;

namespace FoodStreet.Visitors.EditorTools
{
    /// <summary>
    /// The vehicles' particles, on the Toastapult's plumbing: soft cartoon puffs on the project's
    /// soft-particle shader, partly lit by the scene.
    ///
    /// - Exhaust: a puff of grey smoke out of the tailpipe on every chug of the engine, emitted one
    ///   at a time by <see cref="VehicleAnimation"/> so the smoke keeps time with the body's hop.
    /// - Backfire: the cough as the engine catches - a burst of darker smoke, a blink of flame and
    ///   a few sparks.
    /// - Wheel dust: kicked up behind the rear wheels by distance driven, switched on above a
    ///   walking pace.
    /// - Stop dust: a ring of dust puffed out from under the car as it pulls up.
    /// </summary>
    internal static class VehicleEffects
    {
        const string Folder = VehicleModels.Folder + "/Fx";

        static readonly Color Smoke = new Color(0.66f, 0.65f, 0.63f);
        static readonly Color SmokeDark = new Color(0.36f, 0.35f, 0.34f);
        static readonly Color Dust = new Color(0.64f, 0.59f, 0.51f);
        static readonly Color DustLight = new Color(0.72f, 0.68f, 0.6f);
        static readonly Color FireCore = new Color(1f, 0.9f, 0.6f);
        static readonly Color FireMid = new Color(1f, 0.55f, 0.18f);

        static Material Puff() => T.Soft(Folder, "VehicleFx_Puff", T.Texture("Particle_Puff", FX.Puff), additive: false, lit: 0.6f, pull: 0.2f);

        static Material Glow()
        {
            Texture2D glow = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FX.Folder}/Particle_Glow.png") ?? T.Texture("Particle_Puff", FX.Puff);
            return T.Soft(Folder, "VehicleFx_Glow", glow, additive: true, lit: 0f, pull: 0.25f);
        }

        static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>The tailpipe's puffs, under <paramref name="pipe"/> (whose +Z points out of the pipe). Nothing emitted on its own.</summary>
        public static ParticleSystem Exhaust(Transform pipe)
        {
            ParticleSystem ps = T.System(pipe, "ExhaustSmoke", Puff());

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.32f);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(Alpha(Smoke, 0.62f), Alpha(Smoke * 0.85f, 0.5f));
            main.maxParticles = 40;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.02f;

            FX.Rise(ps, 0.35f, 0.6f);
            FX.Grow(ps, FX.Curve((0f, 0.5f), (0.3f, 1.4f), (1f, 2.6f)));
            FX.FadeOut(ps, 0.3f);
            FX.Spin(ps, -45f, 45f);
            FX.Drag(ps, 0.25f, 0.3f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.1f;
            noise.frequency = 0.8f;
            noise.scrollSpeed = 0.4f;
            return ps;
        }

        /// <summary>The cough as it starts: dark smoke out of the pipe, a blink of flame, a spit of sparks.</summary>
        public static ParticleSystem Backfire(Transform pipe)
        {
            ParticleSystem smoke = T.Burst(pipe, "Backfire", Puff(), count: 9, lifetime: (0.75f, 1.15f), speed: (1.3f, 2.4f), size: (0.3f, 0.46f),
                                           cone: 26f, colours: new ParticleSystem.MinMaxGradient(Alpha(SmokeDark, 0.85f), Alpha(SmokeDark * 1.25f, 0.7f)),
                                           grow: FX.Curve((0f, 0.5f), (0.25f, 1.4f), (1f, 2.4f)), gravity: -0.12f, fade: true, drag: true);
            Along(smoke);
            FX.Rise(smoke, 0.3f, 0.55f);

            ParticleSystem flash = T.Burst(smoke.transform, "Flash", Glow(), count: 1, lifetime: (0.09f, 0.13f), speed: (0f, 0f), size: (0.42f, 0.52f),
                                           cone: 0f, colours: new ParticleSystem.MinMaxGradient(FireCore), grow: FX.Curve((0f, 0.6f), (0.3f, 1f), (1f, 0.5f)),
                                           gravity: 0f, fade: true);
            flash.transform.localPosition = Vector3.forward * 0.06f;

            ParticleSystem sparks = T.Burst(smoke.transform, "Sparks", Glow(), count: 5, lifetime: (0.25f, 0.45f), speed: (1.8f, 3.2f), size: (0.04f, 0.07f),
                                            cone: 30f, colours: new ParticleSystem.MinMaxGradient(FireCore, FireMid), grow: FX.Curve((0f, 1f), (1f, 0.2f)),
                                            gravity: 0.8f, fade: true);
            Along(sparks);
            return smoke;
        }

        /// <summary>Points a burst along its transform's +Z - out of the pipe - instead of straight up.</summary>
        static void Along(ParticleSystem ps)
        {
            var shape = ps.shape;
            shape.rotation = Vector3.zero;
        }

        /// <summary>Dust thrown up behind a rear wheel by distance driven. Off until the car is moving.</summary>
        public static ParticleSystem WheelDust(Transform at)
        {
            ParticleSystem ps = T.System(at, "WheelDust", Puff());

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.42f);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(Alpha(Dust, 0.4f), Alpha(DustLight, 0.3f));
            main.maxParticles = 40;

            var emission = ps.emission;
            emission.rateOverDistance = 1.6f;
            emission.enabled = false;

            T.Spread(ps, 0f, 0.12f);
            FX.Rise(ps, 0.08f, 0.22f);
            FX.Grow(ps, FX.Curve((0f, 0.5f), (1f, 1.9f)));
            FX.FadeOut(ps, 0.2f);
            FX.Spin(ps, -30f, 30f);
            FX.Drag(ps, 0.2f, 0.25f);
            return ps;
        }

        /// <summary>A ring of dust puffed out flat from under the car as it pulls up, <paramref name="radius"/> across.</summary>
        public static ParticleSystem StopDust(Transform at, float radius)
        {
            ParticleSystem ps = T.Burst(at, "PullUpDust", Puff(), count: 11, lifetime: (0.6f, 0.95f), speed: (0.8f, 1.6f), size: (0.3f, 0.46f), cone: 0f,
                                        colours: new ParticleSystem.MinMaxGradient(Alpha(Dust, 0.55f), Alpha(DustLight, 0.42f)),
                                        grow: FX.Curve((0f, 0.5f), (1f, 1.8f)), gravity: -0.02f, fade: true, drag: true);

            // Out flat along the ground from a ring under the wheels, not up.
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 0.2f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            return ps;
        }
    }
}
