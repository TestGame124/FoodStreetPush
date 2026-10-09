using UnityEditor;
using UnityEngine;
using FX = LastOrders.EditorTools.PlantingEffects;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The farmyard's particles, on the planting bursts' soft-particle materials and textures:
    /// feathers loose when a hen flaps, a puff of dust where she scratches, straw and a twinkle out
    /// of the nest box when the eggs are collected, a splash of milk and a twinkle at the churns.
    ///
    /// The feathers and the dust are kept playing in world space with nothing coming out of them;
    /// each hen emits a few into them where she is (<see cref="LastOrders.Livestock.ChickenBody"/>).
    /// The collect bursts are one-shot, played by the building.
    /// </summary>
    internal static class LivestockEffects
    {
        static Texture2D Texture(string name, System.Func<float, float, Color> paint)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FX.Folder}/{name}.png");
            return existing != null ? existing : FX.Paint(name, paint);
        }

        static Material Feather() => FX.SpriteMaterial("Livestock_Feather", Texture("Particle_Leaf", FX.Leaf), false, 0.7f, 0.15f);
        static Material Dust() => FX.SpriteMaterial("Livestock_Dust", Texture("Particle_Puff", FX.Puff), false, 0.6f, 0.2f);
        static Material Milk() => FX.SpriteMaterial("Livestock_Milk", Texture("Particle_Puff", FX.Puff), false, 0.65f, 0.15f);
        static Material Sparkle() => FX.SpriteMaterial("Livestock_Sparkle", Texture("Particle_Sparkle", FX.Sparkle), true, 0f, 0.3f);

        static ParticleSystem Fresh(Transform parent, string name, Material material)
        {
            Transform old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            return FX.NewSystem(parent, name, material, ParticleSystemRenderMode.Billboard);
        }

        /// <summary>Kept playing, empty, in world space: the hens emit into it where they flap.</summary>
        internal static ParticleSystem Feathers(Transform parent)
        {
            ParticleSystem ps = Fresh(parent, "Feathers", Feather());
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.11f);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(FX.Hex("C8C1B0"), FX.Hex("9C8C78"));
            main.gravityModifier = 0.12f;
            main.maxParticles = 40;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 1.6f;
            noise.scrollSpeed = 0.7f;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            FX.Drag(ps, 0.3f, 0.12f);
            FX.Grow(ps, FX.Curve((0f, 0.4f), (0.12f, 1f), (0.8f, 1f), (1f, 0.2f)));
            FX.FadeOut(ps, 0.65f);
            FX.Spin(ps, -360f, 360f);
            return ps;
        }

        /// <summary>Kept playing, empty, in world space: a puff where a hen scratches.</summary>
        internal static ParticleSystem Scuff(Transform parent)
        {
            ParticleSystem ps = Fresh(parent, "Dust", Dust());
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.3f);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.64f, 0.59f, 0.51f, 0.8f), new Color(0.72f, 0.68f, 0.6f, 0.75f));
            main.maxParticles = 30;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.05f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            FX.Rise(ps, 0.08f, 0.22f);
            FX.Grow(ps, FX.Curve((0f, 0.45f), (0.4f, 1.3f), (1f, 1.8f)));
            FX.FadeOut(ps, 0.2f);
            FX.Spin(ps, -40f, 40f);
            return ps;
        }

        /// <summary>One shot out of the nest box: straw flicked up and out, and a twinkle over it.</summary>
        internal static ParticleSystem[] EggBurst(Transform parent)
        {
            ParticleSystem straw = Fresh(parent, "Straw", Feather());
            var main = straw.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.11f);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(FX.Hex("C2A86C"), FX.Hex("D9C48C"));
            main.gravityModifier = 0.35f;
            main.maxParticles = 12;
            FX.Burst(straw, 0f, 8);

            var shape = straw.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.12f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            FX.Drag(straw, 0.4f, 0.15f);
            FX.Grow(straw, FX.Curve((0f, 0.3f), (0.15f, 1f), (0.75f, 1f), (1f, 0f)));
            FX.FadeOut(straw, 0.7f);
            FX.Spin(straw, -480f, 480f);

            return new[] { straw, Twinkle(parent) };
        }

        /// <summary>One shot at the churns: a splash of milk thrown up and falling, and a twinkle over it.</summary>
        internal static ParticleSystem[] MilkBurst(Transform parent)
        {
            ParticleSystem splash = Fresh(parent, "Splash", Milk());
            var main = splash.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.3f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(FX.Hex("D8D4C8"), FX.Hex("EAE6DA"));
            main.gravityModifier = 1.6f;
            main.maxParticles = 14;
            FX.Burst(splash, 0f, 10);

            var shape = splash.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.08f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            FX.Grow(splash, FX.Curve((0f, 0.6f), (0.3f, 1f), (1f, 0.4f)));
            FX.FadeOut(splash, 0.75f);

            return new[] { splash, Twinkle(parent) };
        }

        /// <summary>
        /// Kept playing with nothing coming out of it until a hen settles in the nest box, when her
        /// herd turns it up: straw wisps worked out over the front of the box, a little dust.
        /// </summary>
        internal static ParticleSystem Rustle(Transform parent)
        {
            ParticleSystem ps = Fresh(parent, "Rustle", Feather());
            Looping(ps, 20);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.11f);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(FX.Hex("C2A86C"), FX.Hex("DCC793"));
            main.gravityModifier = 0.3f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45f;
            shape.radius = 0.12f;
            shape.rotation = new Vector3(-50f, 90f, 0f);

            FX.Drag(ps, 0.5f, 0.1f);
            FX.Grow(ps, FX.Curve((0f, 0.4f), (0.15f, 1f), (0.8f, 1f), (1f, 0f)));
            FX.FadeOut(ps, 0.65f);
            FX.Spin(ps, -360f, 360f);
            return ps;
        }

        /// <summary>Kept playing, turned up by the herd while the cow eats at the rack: wisps of hay dropping out of it.</summary>
        internal static ParticleSystem HayFall(Transform parent)
        {
            ParticleSystem ps = Fresh(parent, "HayFall", Feather());
            Looping(ps, 24);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.13f);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(FX.Hex("B9A16A"), FX.Hex("D2BD86"));
            main.gravityModifier = 0.35f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.6f, 0.15f, 0.2f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 1.4f;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            FX.Drag(ps, 0.6f, 0.08f);
            FX.Grow(ps, FX.Curve((0f, 0.5f), (0.15f, 1f), (0.85f, 1f), (1f, 0f)));
            FX.FadeOut(ps, 0.7f);
            FX.Spin(ps, -240f, 240f);
            return ps;
        }

        /// <summary>One shot as an egg is laid or a churn is filled: a ring of light and sparkles popping up over it.</summary>
        internal static ParticleSystem[] Pop(Transform parent)
        {
            ParticleSystem ring = Fresh(parent, "PopRing", Glow());
            var main = ring.main;
            main.startLifetime = 0.55f;
            main.startSpeed = 0f;
            main.startSize = 0.35f;
            main.startColor = FX.Hex("FFE7A0");
            main.maxParticles = 2;
            FX.Burst(ring, 0f, 1);
            ring.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            FX.Grow(ring, FX.Curve((0f, 0.3f), (1f, 2.4f)));
            FX.FadeOut(ring, 0.2f);

            ParticleSystem sparkle = Twinkle(parent);
            var sm = sparkle.main;
            sm.startDelay = 0f;
            return new[] { ring, sparkle };
        }

        /// <summary>One shot at the feeder as the hens are fed: corn kernels thrown up and scattered, a little chaff.</summary>
        internal static ParticleSystem[] CornScatter(Transform parent)
        {
            ParticleSystem kernels = Fresh(parent, "Kernels", Dust());
            var main = kernels.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(FX.Hex("D9B04A"), FX.Hex("E8C55C"));
            main.gravityModifier = 1.5f;
            main.maxParticles = 24;
            FX.Burst(kernels, 0f, 18);

            var shape = kernels.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 38f;
            shape.radius = 0.1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var collision = kernels.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.Planes;
            collision.bounce = 0.25f;
            collision.dampen = 0.6f;
            Transform old = parent.Find("KernelFloor");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var floor = new GameObject("KernelFloor").transform;
            floor.SetParent(parent, false);
            floor.localPosition = new Vector3(0f, -0.05f, 0f);
            collision.SetPlane(0, floor);

            FX.FadeOut(kernels, 0.75f);
            return new[] { kernels, Chaff(parent) };
        }

        /// <summary>One shot at the rack as the cow is fed: hay tossed in, wisps flying, a little dust.</summary>
        internal static ParticleSystem[] HayToss(Transform parent)
        {
            ParticleSystem hay = Fresh(parent, "Hay", Feather());
            var main = hay.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.14f);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(FX.Hex("B9A16A"), FX.Hex("D2BD86"));
            main.gravityModifier = 0.45f;
            main.maxParticles = 20;
            FX.Burst(hay, 0f, 14);

            var shape = hay.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 50f;
            shape.radius = 0.25f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            FX.Drag(hay, 0.4f, 0.12f);
            FX.FadeOut(hay, 0.7f);
            FX.Spin(hay, -420f, 420f);
            return new[] { hay, Chaff(parent) };
        }

        /// <summary>
        /// One shot as each item of feed goes in: a ring closing in on the feeder and sparkles drawn
        /// into it - the feed taken in - then a soft flash. <paramref name="tint"/> is the feed's colour.
        /// </summary>
        internal static ParticleSystem[] Gulp(Transform parent, Color tint)
        {
            ParticleSystem ring = Fresh(parent, "GulpRing", Glow());
            var main = ring.main;
            main.startLifetime = 0.4f;
            main.startSpeed = 0f;
            main.startSize = 0.5f;
            main.startColor = Color.Lerp(tint, Color.white, 0.35f);
            main.maxParticles = 4;
            FX.Burst(ring, 0f, 1);
            ring.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            FX.Grow(ring, FX.Curve((0f, 2.6f), (0.7f, 0.6f), (1f, 0.1f)));
            var ringFade = ring.colorOverLifetime;
            ringFade.enabled = true;
            ringFade.color = Fade((0f, 0f), (0.2f, 1f), (1f, 0f));

            // Drawn in from all round: born on a shell, pulled to its middle as they shrink.
            ParticleSystem motes = Fresh(parent, "GulpMotes", Sparkle());
            var mm = motes.main;
            mm.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.42f);
            mm.startSpeed = 0f;
            mm.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.16f);
            mm.startRotation = FX.Turn();
            mm.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(tint, Color.white, 0.25f), Color.Lerp(tint, Color.white, 0.6f));
            mm.simulationSpace = ParticleSystemSimulationSpace.Local;
            mm.maxParticles = 24;
            FX.Burst(motes, 0f, 10);

            var shape = motes.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            shape.radiusThickness = 0f;
            shape.position = new Vector3(0f, 0.15f, 0f);

            var pull = motes.velocityOverLifetime;
            pull.enabled = true;
            pull.space = ParticleSystemSimulationSpace.Local;
            pull.radial = new ParticleSystem.MinMaxCurve(-1.35f);

            FX.Grow(motes, FX.Curve((0f, 0.4f), (0.25f, 1f), (1f, 0.1f)));
            FX.Spin(motes, 90f, 220f);

            ParticleSystem flash = Fresh(parent, "GulpFlash", Glow());
            var fm = flash.main;
            fm.startDelay = 0.3f;
            fm.startLifetime = 0.3f;
            fm.startSpeed = 0f;
            fm.startSize = 0.7f;
            fm.startColor = Color.Lerp(tint, Color.white, 0.5f);
            fm.maxParticles = 2;
            FX.Burst(flash, 0f, 1);
            FX.Grow(flash, FX.Curve((0f, 0.5f), (0.3f, 1.2f), (1f, 0.8f)));
            FX.FadeOut(flash, 0.1f);

            return new[] { ring, motes, flash };
        }

        static Gradient Fade(params (float time, float alpha)[] keys)
        {
            var alphas = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++) alphas[i] = new GradientAlphaKey(keys[i].alpha, keys[i].time);
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alphas);
            return gradient;
        }

        static ParticleSystem Chaff(Transform parent)
        {
            ParticleSystem puff = Fresh(parent, "Chaff", Dust());
            var main = puff.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.35f);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.64f, 0.52f, 0.7f), new Color(0.78f, 0.72f, 0.6f, 0.6f));
            main.maxParticles = 8;
            FX.Burst(puff, 0f, 4);

            var shape = puff.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            FX.Rise(puff, 0.1f, 0.25f);
            FX.Grow(puff, FX.Curve((0f, 0.5f), (1f, 1.6f)));
            FX.FadeOut(puff, 0.2f);
            return puff;
        }

        static void Looping(ParticleSystem ps, int most)
        {
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 5f;
            main.maxParticles = most;
        }

        static Material Glow() => FX.SpriteMaterial("Livestock_Glow", Texture("Particle_Glow", FX.Sparkle), true, 0f, 0.1f);

        static ParticleSystem Twinkle(Transform parent)
        {
            ParticleSystem ps = Fresh(parent, "Sparkles", Sparkle());
            var main = ps.main;
            main.startDelay = 0.08f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.22f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-20f * Mathf.Deg2Rad, 20f * Mathf.Deg2Rad);
            main.startColor = new ParticleSystem.MinMaxGradient(FX.Hex("FFE58A"), FX.Hex("FFF8DA"));
            main.maxParticles = 10;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 5), new ParticleSystem.Burst(0.14f, 3) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            shape.position = new Vector3(0f, 0.2f, 0f);

            FX.Rise(ps, 0.2f, 0.35f);
            FX.Grow(ps, FX.Curve((0f, 0f), (0.18f, 1f), (0.6f, 0.75f), (1f, 0f)));
            FX.Spin(ps, 60f, 140f);
            ps.GetComponent<ParticleSystemRenderer>().sortingFudge = -20f;
            return ps;
        }
    }
}
