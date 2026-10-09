using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Everything about bringing a crop in, built in code:
    ///
    /// - The shimmer over a ripe pot, replacing the art pack's yellow flare: a few gold twinkles
    ///   among the crop, warm motes drifting up off it and a soft pool of gold light breathing on
    ///   the soil under the plants. Calm enough that a whole garden of ripe pots stays pleasant.
    /// - The burst as a crop is cut: a ring of gold, leaves and golden chaff thrown up, sparkles.
    /// - The sickle the player sweeps over ripe pots: a smooth low-poly sickle - steel blade bright
    ///   along its edge, brass ferrule, wooden handle - rendered like the crops and outlined, on a
    ///   cream badge, and a <see cref="HarvestTool"/> on the game's UI canvas to drive it.
    ///
    /// The particles share the planting burst's textures and materials (see
    /// <see cref="PlantingEffects"/>, which must have been built first).
    /// </summary>
    public static class HarvestingEffects
    {
        const string UiFolder = "Assets/Game Assets/Content/LastOrders/Farming/UI";
        const string ShimmerPath = PlantingEffects.Folder + "/RipeShimmer.prefab";
        const string BurstPath = PlantingEffects.Folder + "/HarvestBurst.prefab";

        const string OldFlareName = "Shine_ellow";
        const string ShimmerName = "RipeShimmer";
        const string BurstName = "HarvestBurst";
        const string ToolName = "HarvestSickle";

        const int IconSize = 256;

        [MenuItem("Last Orders/Farming/Build Harvest Effects & Sickle", priority = 93)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This builds the ripe shimmer, the harvest burst and the sickle, and puts them into:\n\n" +
                "- pfPlantPot: the old yellow flare out, the shimmer in as its finished effect, the burst on the soil\n" +
                $"- {UiFolder}: the sickle and its badge\n" +
                "- the open scene: a HarvestSickle on the game's UI, which taps on ripe pots bring up\n\n" +
                "The scene is left unsaved.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        /// <summary>Builds it all and puts it into the pot and the open scene, with no dialogs.</summary>
        public static string Apply()
        {
            ContentGenerator.EnsureFolder(UiFolder);

            Material sparkle = Existing("Burst_Sparkle");
            Material leaf = Existing("Burst_Leaf");
            Material glow = PlantingEffects.SpriteMaterial("Ripe_Glow", PlantingEffects.Paint("Particle_Glow", Glow), true, 0f, 0.3f);
            Material ring = PlantingEffects.SpriteMaterial("Ripe_Ring", AssetDatabase.LoadAssetAtPath<Texture2D>(PlantingEffects.Folder + "/Particle_Ring.png"),
                                                           true, 0f, 0.06f);
            Material pool = PlantingEffects.SpriteMaterial("Ripe_Pool", AssetDatabase.LoadAssetAtPath<Texture2D>(PlantingEffects.Folder + "/Particle_Glow.png"),
                                                           true, 0f, 0.04f);
            UnderCrops(ring);
            UnderCrops(pool);

            BuildShimmer(sparkle, glow, pool);
            BuildBurst(sparkle, leaf, glow, ring);

            Sprite sickle = SaveSprite("Icon_Sickle", SickleIcon());
            Sprite badge = SaveSprite("Icon_ToolBadge", Badge());

            var report = new StringBuilder("[Last Orders] Harvest effects built.\n");
            report.Append($"  {ShimmerPath}: twinkles, motes, glow. {BurstPath}: ring, leaves, chaff, sparkles.\n");
            report.Append($"  {UiFolder}: Icon_Sickle, Icon_ToolBadge.\n");
            report.Append($"  {DressPot()}\n");
            report.Append($"  {SetUpScene(sickle, badge)}\n");
            return report.ToString();
        }

        /// <summary>
        /// Drawn before the crop sprites, with their shadows, so light on the soil glows under the
        /// plants rather than over them.
        /// </summary>
        static void UnderCrops(Material material)
        {
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 10;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }

        static Material Existing(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{PlantingEffects.Folder}/{name}.mat");
            if (material == null)
                throw new InvalidOperationException($"{name} is missing: run Last Orders / Farming / Build Planting Effects first.");
            return material;
        }

        // --- The ripe shimmer ------------------------------------------------------------------------

        static void BuildShimmer(Material sparkle, Material glow, Material pool)
        {
            var root = new GameObject(ShimmerName);
            try
            {
                // Twinkles among the crop, popping in and out.
                ParticleSystem twinkles = Looping(root.transform, "Twinkles", sparkle, ParticleSystemRenderMode.Billboard, 2.4f, 2);
                var main = twinkles.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
                main.startRotation = new ParticleSystem.MinMaxCurve(-20f * Mathf.Deg2Rad, 20f * Mathf.Deg2Rad);
                main.startColor = new ParticleSystem.MinMaxGradient(Hex("FFE07A"), Hex("FFF5CF"));
                main.maxParticles = 8;
                Box(twinkles, new Vector3(0.8f, 0.45f, 0.8f), new Vector3(0f, 0.32f, 0f));
                PlantingEffects.Rise(twinkles, 0.03f, 0.1f);
                PlantingEffects.Grow(twinkles, PlantingEffects.Curve((0f, 0f), (0.25f, 1f), (0.55f, 0.8f), (1f, 0f)));
                PlantingEffects.Spin(twinkles, 30f, 90f);
                twinkles.GetComponent<ParticleSystemRenderer>().sortingFudge = -20f;

                // Warm motes drifting up off the ripe crop.
                ParticleSystem motes = Looping(root.transform, "Motes", glow, ParticleSystemRenderMode.Billboard, 3.5f, 2);
                main = motes.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.06f);
                main.startColor = new ParticleSystem.MinMaxGradient(Hex("FFE9A0"), Hex("FFF4C8"));
                main.maxParticles = 12;
                Box(motes, new Vector3(0.85f, 0.05f, 0.85f), new Vector3(0f, 0.1f, 0f));
                PlantingEffects.Rise(motes, 0.16f, 0.3f);
                var noise = motes.noise;
                noise.enabled = true;
                noise.strength = 0.12f;
                noise.frequency = 1.2f;
                noise.scrollSpeed = 0.4f;
                FadeInOut(motes, 0.2f, 0.7f);

                // A soft pool of gold light on the soil under the plants, slowly breathing.
                ParticleSystem pulse = Looping(root.transform, "Glow", pool, ParticleSystemRenderMode.HorizontalBillboard, 0.75f, 1);
                main = pulse.main;
                main.startLifetime = 2f;
                main.startSize = 1.25f;
                main.startColor = Fade(Hex("FFC94A"), 0.4f);
                main.maxParticles = 3;
                var shape = pulse.shape;
                shape.enabled = false;
                pulse.transform.localPosition = new Vector3(0f, 0.015f, 0f);
                PlantingEffects.Grow(pulse, PlantingEffects.Curve((0f, 0.8f), (0.5f, 1f), (1f, 0.9f)));
                FadeInOut(pulse, 0.4f, 0.45f);

                PrefabUtility.SaveAsPrefabAsset(root, ShimmerPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static ParticleSystem Looping(Transform root, string name, Material material, ParticleSystemRenderMode mode, float rate, short opening)
        {
            ParticleSystem ps = PlantingEffects.NewSystem(root, name, material, mode);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = false;
            main.duration = 2f;
            main.startSpeed = 0f;

            // A few at once as it switches on, so the shimmer is there the moment the crop ripens.
            PlantingEffects.Burst(ps, 0f, opening);
            var emission = ps.emission;
            emission.rateOverTime = rate;
            return ps;
        }

        static void Box(ParticleSystem ps, Vector3 size, Vector3 centre)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            shape.position = centre;
        }

        /// <summary>Fades in over the first <paramref name="fadeIn"/> of its life and out over the last <paramref name="fadeOut"/>.</summary>
        static void FadeInOut(ParticleSystem ps, float fadeIn, float fadeOut)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(1f, 1f - fadeOut), new GradientAlphaKey(0f, 1f) });

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        // --- The harvest burst -----------------------------------------------------------------------

        static void BuildBurst(Material sparkle, Material leaf, Material glow, Material ring)
        {
            var root = new GameObject(BurstName);
            try
            {
                root.AddComponent<PotBurst>().PlayOn = PotMoment.Harvested;

                ParticleSystem flash = PlantingEffects.NewSystem(root.transform, "Ring", ring, ParticleSystemRenderMode.HorizontalBillboard);
                var main = flash.main;
                main.startLifetime = 0.45f;
                main.startSpeed = 0f;
                main.startSize = 1.15f;
                main.startColor = Fade(Hex("FFE28A"), 0.85f);
                main.maxParticles = 1;
                PlantingEffects.Burst(flash, 0f, 1);
                var shape = flash.shape;
                shape.enabled = false;
                flash.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                PlantingEffects.Grow(flash, PlantingEffects.Curve((0f, 0.3f), (0.4f, 0.85f), (1f, 1f)));
                PlantingEffects.FadeOut(flash, 0f);

                ParticleSystem leaves = PlantingEffects.NewSystem(root.transform, "Leaves", leaf, ParticleSystemRenderMode.Billboard);
                main = leaves.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.1f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.12f);
                main.startRotation = PlantingEffects.Turn();
                main.startColor = new ParticleSystem.MinMaxGradient(Hex("7CC24A"), Hex("B3DB5E"));
                main.gravityModifier = 0.4f;
                main.maxParticles = 10;
                PlantingEffects.Burst(leaves, 0f, 7);
                Cone(leaves, 40f, 0.12f, 0.15f);
                var noise = leaves.noise;
                noise.enabled = true;
                noise.strength = 0.4f;
                noise.frequency = 1.8f;
                noise.scrollSpeed = 0.8f;
                PlantingEffects.Drag(leaves, 0.3f, 0.15f);
                PlantingEffects.Grow(leaves, PlantingEffects.Curve((0f, 0.4f), (0.12f, 1f), (0.75f, 1f), (1f, 0f)));
                PlantingEffects.FadeOut(leaves, 0.7f);
                PlantingEffects.Spin(leaves, -500f, 500f);

                ParticleSystem chaff = PlantingEffects.NewSystem(root.transform, "Chaff", glow, ParticleSystemRenderMode.Billboard);
                main = chaff.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.06f);
                main.startColor = new ParticleSystem.MinMaxGradient(Hex("FFD966"), Hex("FFF0B0"));
                main.gravityModifier = 0.5f;
                main.maxParticles = 14;
                PlantingEffects.Burst(chaff, 0f, 10);
                Cone(chaff, 50f, 0.15f, 0.12f);
                PlantingEffects.FadeOut(chaff, 0.6f);

                ParticleSystem sparkles = PlantingEffects.NewSystem(root.transform, "Sparkles", sparkle, ParticleSystemRenderMode.Billboard);
                main = sparkles.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.13f, 0.22f);
                main.startRotation = new ParticleSystem.MinMaxCurve(-20f * Mathf.Deg2Rad, 20f * Mathf.Deg2Rad);
                main.startColor = new ParticleSystem.MinMaxGradient(Hex("FFE58A"), Hex("FFF8DA"));
                main.maxParticles = 12;
                var emission = sparkles.emission;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6), new ParticleSystem.Burst(0.1f, 3) });
                shape = sparkles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.35f;
                shape.position = new Vector3(0f, 0.3f, 0f);
                PlantingEffects.Rise(sparkles, 0.25f, 0.4f);
                PlantingEffects.Grow(sparkles, PlantingEffects.Curve((0f, 0f), (0.18f, 1f), (0.6f, 0.75f), (1f, 0f)));
                PlantingEffects.Spin(sparkles, 60f, 140f);
                sparkles.GetComponent<ParticleSystemRenderer>().sortingFudge = -20f;

                PrefabUtility.SaveAsPrefabAsset(root, BurstPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void Cone(ParticleSystem ps, float angle, float radius, float height)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
            shape.position = new Vector3(0f, height, 0f);
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        /// <summary>A soft round glow with a brighter core: a mote of light.</summary>
        static Color Glow(float x, float y)
        {
            float r2 = x * x + y * y;
            float alpha = Mathf.Exp(-r2 * 5f) * 0.55f + Mathf.Exp(-r2 * 28f) * 0.6f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(alpha) * (1f - Mathf.Clamp01((Mathf.Sqrt(r2) - 0.9f) / 0.1f)));
        }

        // --- The sickle ------------------------------------------------------------------------------

        const int Blade = 0, Spine = 1, Ferrule = 2, Handle = 3;

        /// <summary>
        /// A sickle in the crops' smooth low-poly style, rendered like them and outlined, centred in a
        /// square icon with a soft shadow.
        /// </summary>
        static Texture2D SickleIcon()
        {
            var shape = new SmoothMesh(4);

            // Modelled in the XY plane, facing -Z (the bake looks along +Z), lifted clear of the
            // bake's soil line at zero.
            var centre = new Vector3(0.02f, 0.52f, 0f);
            const float Radius = 0.165f, From = 202f, To = -18f;
            Vector3 Arc(float degrees, float r) =>
                centre + new Vector3(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad), 0f) * r;

            // The blade: a crescent sweeping up over the top and down to its point, bellied a little.
            shape.Surface(Blade, 30, 6, (u, v) =>
            {
                float degrees = Mathf.Lerp(From, To, u);
                // Broad through the belly of the curve, narrowing to a point.
                float width = 0.088f * Mathf.Pow(1f - u, 0.55f) * (0.72f + 0.28f * Mathf.Sin(Mathf.PI * u)) + 0.003f;
                float belly = 0.008f * Mathf.Sin(Mathf.PI * v) * (1f - 0.6f * u);
                return Arc(degrees, Radius - v * width) + Vector3.back * belly;
            });

            // A rolled spine along the back of the blade.
            shape.Surface(Spine, 8, 30, (u, v) =>
            {
                float degrees = Mathf.Lerp(From, To, v);
                float r = Mathf.Lerp(0.01f, 0.002f, v);
                Vector3 p = Arc(degrees, Radius + r * 0.2f);
                Vector3 outward = (p - centre).normalized;
                float a = u * Mathf.PI * 2f;
                return p + (outward * Mathf.Cos(a) + Vector3.forward * Mathf.Sin(a)) * r;
            }, wrapU: true);

            // The brass ferrule where the blade meets the handle, and the handle running down from it.
            Vector3 foot = Arc(From, Radius - 0.04f);
            Vector3 down = new Vector3(Mathf.Cos(242f * Mathf.Deg2Rad), Mathf.Sin(242f * Mathf.Deg2Rad), 0f);
            shape.Tube(Ferrule, foot - down * 0.012f, foot + down * 0.04f, 0.027f, 0.027f, 14);
            shape.Tube(Handle, foot + down * 0.035f, foot + down * 0.18f, 0.025f, 0.028f, 14);
            shape.RoundedBox(Handle, foot + down * 0.185f, new Vector3(0.06f, 0.06f, 0.06f), 0.028f, Quaternion.identity, 3);

            Mesh mesh = shape.Build("Sickle", out List<int> used);
            Material[] palette = SicklePaint();

            try
            {
                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++) vertices[i] *= 1.6f;
                mesh.vertices = vertices;
                mesh.RecalculateBounds();

                Texture2D render = CropSprites.Render(mesh, used, palette, Quaternion.Euler(10f, -16f, 0f), out Vector2 _);
                try
                {
                    return Outlined(render);
                }
                finally
                {
                    Object.DestroyImmediate(render);
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                foreach (Material material in palette) Object.DestroyImmediate(material);
            }
        }

        /// <summary>The bake shader in the sickle's four paints: bright steel, dark spine, brass, warm wood.</summary>
        static Material[] SicklePaint()
        {
            Shader shader = Shader.Find("Hidden/LastOrders/CropBake");

            Material Paint(Color a, Color b, float power, float gloss)
            {
                var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                material.SetColor("_ColorA", a);
                material.SetColor("_ColorB", b);
                material.SetFloat("_GradientPower", power);
                material.SetFloat("_Gloss", gloss);
                material.SetFloat("_Vein", 0f);
                material.SetFloat("_EdgeShade", 0f);
                material.SetVector("_Kernels", Vector4.zero);
                material.SetFloat("_GroundShade", 1f);
                material.SetFloat("_GroundHeight", 0.001f);
                material.SetFloat("_Rim", 0.3f);
                return material;
            }

            // The blade runs from its spine (dark) to its honed edge (bright).
            return new[]
            {
                Paint(new Color(0.55f, 0.61f, 0.66f), new Color(0.94f, 0.97f, 0.99f), 1.6f, 0.7f),
                Paint(new Color(0.36f, 0.4f, 0.45f), new Color(0.5f, 0.55f, 0.6f), 1f, 0.4f),
                Paint(new Color(0.72f, 0.53f, 0.24f), new Color(0.95f, 0.8f, 0.45f), 1f, 0.6f),
                Paint(new Color(0.5f, 0.29f, 0.15f), new Color(0.76f, 0.5f, 0.28f), 1f, 0.15f),
            };
        }

        /// <summary><paramref name="render"/> fitted into a square icon with a dark outline round it and a soft shadow under it.</summary>
        static Texture2D Outlined(Texture2D render)
        {
            const int Box = 200, OutlinePixels = 6;
            var outlineColour = new Color(0.23f, 0.15f, 0.09f);

            // Trimmed to what was drawn: the crop renderer frames from the origin, which the sickle is well clear of.
            Color32[] source = render.GetPixels32();
            int minX = render.width, minY = render.height, maxX = -1, maxY = -1;
            for (int y = 0; y < render.height; y++)
                for (int x = 0; x < render.width; x++)
                {
                    if (source[y * render.width + x].a < 3) continue;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }

            float drawnW = maxX - minX + 1, drawnH = maxY - minY + 1;
            float scale = Mathf.Min(Box / drawnW, Box / drawnH);
            int w = Mathf.RoundToInt(drawnW * scale), h = Mathf.RoundToInt(drawnH * scale);
            int ox = (IconSize - w) / 2, oy = (IconSize - h) / 2 + 4;

            var art = new Color[IconSize * IconSize];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    art[(y + oy) * IconSize + x + ox] = render.GetPixelBilinear((minX + (x + 0.5f) / scale) / render.width,
                                                                                (minY + (y + 0.5f) / scale) / render.height);

            // The outline: the art's alpha spread out a few pixels in every direction.
            var ring = new float[IconSize * IconSize];
            for (int y = 0; y < IconSize; y++)
                for (int x = 0; x < IconSize; x++)
                {
                    float best = 0f;
                    for (int dy = -OutlinePixels; dy <= OutlinePixels && best < 1f; dy++)
                        for (int dx = -OutlinePixels; dx <= OutlinePixels; dx++)
                        {
                            int sx = x + dx, sy = y + dy;
                            if (sx < 0 || sy < 0 || sx >= IconSize || sy >= IconSize) continue;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            if (d > OutlinePixels + 0.5f) continue;
                            float a = art[sy * IconSize + sx].a * Mathf.Clamp01(OutlinePixels + 0.5f - d);
                            if (a > best) best = a;
                        }
                    ring[y * IconSize + x] = best;
                }

            var pixels = new Color[IconSize * IconSize];
            for (int y = 0; y < IconSize; y++)
                for (int x = 0; x < IconSize; x++)
                {
                    int i = y * IconSize + x;

                    // A soft shadow, down and to the right of the outline.
                    float shadow = 0f;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int sx = x - 4 + dx, sy = y + 6 + dy;
                            if (sx < 0 || sy < 0 || sx >= IconSize || sy >= IconSize) continue;
                            shadow += ring[sy * IconSize + sx];
                        }
                    shadow = shadow / 25f * 0.35f;

                    Color c = new Color(0f, 0f, 0f, shadow);
                    c = Over(new Color(outlineColour.r, outlineColour.g, outlineColour.b, ring[i]), c);
                    c = Over(art[i], c);
                    pixels[i] = c;
                }

            var icon = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            icon.SetPixels(pixels);
            icon.Apply(false);
            return icon;
        }

        /// <summary>A round cream badge with a warm brown rim, lit from the top, on a soft shadow.</summary>
        static Texture2D Badge()
        {
            const int Samples = 3;
            const float Radius = 0.82f, Rim = 0.06f;
            var top = new Color(1f, 0.98f, 0.92f);
            var bottom = new Color(0.95f, 0.87f, 0.7f);
            var rim = new Color(0.43f, 0.29f, 0.18f);

            var pixels = new Color[IconSize * IconSize];
            for (int j = 0; j < IconSize; j++)
                for (int i = 0; i < IconSize; i++)
                {
                    Color sum = Color.clear;
                    for (int sj = 0; sj < Samples; sj++)
                        for (int si = 0; si < Samples; si++)
                        {
                            float x = (i + (si + 0.5f) / Samples) / IconSize * 2f - 1f;
                            float y = (j + (sj + 0.5f) / Samples) / IconSize * 2f - 1f;
                            float r = Mathf.Sqrt(x * x + y * y);

                            float shadowR = Mathf.Sqrt(x * x + (y + 0.07f) * (y + 0.07f));
                            var c = new Color(0f, 0f, 0f, 0.3f * (1f - Smooth(Mathf.InverseLerp(Radius - 0.04f, Radius + 0.1f, shadowR))));

                            float inside = 1f - Smooth(Mathf.InverseLerp(Radius - 0.012f, Radius + 0.012f, r));
                            float face = 1f - Smooth(Mathf.InverseLerp(Radius - Rim - 0.012f, Radius - Rim + 0.012f, r));
                            Color fill = Color.Lerp(bottom, top, Smooth(y * 0.5f + 0.5f));
                            // A soft sheen across the top of the face.
                            float sheen = Smooth(Mathf.InverseLerp(0.25f, 0.65f, y)) * (1f - Smooth(Mathf.InverseLerp(Radius - Rim - 0.12f, Radius - Rim, r)));
                            fill = Color.Lerp(fill, Color.white, sheen * 0.45f);
                            Color body = Color.Lerp(rim, fill, face);
                            body.a = inside;

                            sum += Over(body, c);
                        }

                    pixels[j * IconSize + i] = sum / (Samples * Samples);
                }

            var badge = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            badge.SetPixels(pixels);
            badge.Apply(false);
            return badge;
        }

        static Color Over(Color top, Color under)
        {
            float a = top.a + under.a * (1f - top.a);
            if (a <= 0f) return Color.clear;
            Color c = (top * top.a + under * under.a * (1f - top.a)) / a;
            c.a = a;
            return c;
        }

        static Sprite SaveSprite(string name, Texture2D texture)
        {
            string path = $"{UiFolder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // --- The pot and the scene -------------------------------------------------------------------

        /// <summary>
        /// pfPlantPot: the art pack's yellow flare out, the shimmer in its place as the pot's finished
        /// effect (switched on as the crop ripens, off as it is picked), and the harvest burst on the soil.
        /// </summary>
        static string DressPot()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlantingEffects.PotPrefabPath);

            try
            {
                var pot = root.GetComponent<PlantPot>();
                if (pot == null) return "WARNING: pfPlantPot has no PlantPot.";

                Transform flare = root.transform.Find(OldFlareName);
                bool hadFlare = flare != null;
                if (hadFlare) Object.DestroyImmediate(flare.gameObject);

                GameObject shimmer = Nest(root.transform, ShimmerPath, ShimmerName);
                shimmer.SetActive(false);
                Nest(root.transform, BurstPath, BurstName);

                var serialized = new SerializedObject(pot);
                serialized.FindProperty("finishedEffect").objectReferenceValue = shimmer;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlantingEffects.PotPrefabPath);
                return $"pfPlantPot: {(hadFlare ? "yellow flare removed; " : "")}ripe shimmer as the finished effect, harvest burst on the soil.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The prefab at <paramref name="path"/> nested under <paramref name="parent"/> on the soil, reusing one already there.</summary>
        static GameObject Nest(Transform parent, string path, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Transform existing = parent.Find(name);
            if (existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) != prefab)
            {
                Object.DestroyImmediate(existing.gameObject);
                existing = null;
            }

            if (existing == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.name = name;
                existing = instance.transform;
            }

            existing.SetLocalPositionAndRotation(new Vector3(0.5f, SoilBed.Crown, 0.5f), Quaternion.identity);
            existing.localScale = Vector3.one;
            return existing.gameObject;
        }

        /// <summary>The sickle on the game's UI canvas, hidden until a ripe pot is tapped.</summary>
        static string SetUpScene(Sprite sickle, Sprite badge)
        {
            Scene scene = SceneManager.GetActiveScene();
            var ui = Object.FindFirstObjectByType<UIGame>(FindObjectsInactive.Include);
            if (ui == null) return "Scene: no UIGame here; ripe pots will harvest on a tap, as before.";

            Transform tool = ui.transform.Find(ToolName);
            bool created = tool == null;
            if (created)
            {
                var go = new GameObject(ToolName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(HarvestTool));
                Undo.RegisterCreatedObjectUndo(go, "Harvest sickle");
                tool = go.transform;
                tool.SetParent(ui.transform, false);
            }

            Undo.RecordObject(tool, "Harvest sickle");
            var rect = (RectTransform)tool;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(150f, 150f);
            rect.anchoredPosition = Vector2.zero;
            rect.SetAsLastSibling();

            var badgeImage = tool.GetComponent<Image>();
            badgeImage.sprite = badge;
            badgeImage.preserveAspect = true;
            badgeImage.raycastTarget = true;

            // Out of sight in the editor; the tool hides itself in play too, until it is offered.
            tool.GetComponent<CanvasGroup>().alpha = 0f;

            Transform blade = tool.Find("Sickle");
            if (blade == null)
            {
                var go = new GameObject("Sickle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, "Harvest sickle");
                blade = go.transform;
                blade.SetParent(tool, false);
            }

            var bladeRect = (RectTransform)blade;
            bladeRect.anchorMin = bladeRect.anchorMax = bladeRect.pivot = new Vector2(0.5f, 0.5f);
            bladeRect.sizeDelta = new Vector2(112f, 112f);
            bladeRect.anchoredPosition = new Vector2(0f, 4f);
            var bladeImage = blade.GetComponent<Image>();
            bladeImage.sprite = sickle;
            bladeImage.preserveAspect = true;
            bladeImage.raycastTarget = false;

            var harvest = new SerializedObject(tool.GetComponent<HarvestTool>());
            harvest.FindProperty("badge").objectReferenceValue = badgeImage;
            harvest.FindProperty("sickle").objectReferenceValue = bladeRect;
            harvest.FindProperty("hover").floatValue = 85f;
            harvest.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            return $"Scene: {(created ? "HarvestSickle added" : "HarvestSickle updated")} under {ui.name} (the scene is not saved).";
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static Color Hex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out Color c);
            return c;
        }

        static Color Fade(Color c, float alpha)
        {
            c.a = alpha;
            return c;
        }
    }
}
