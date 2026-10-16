using System.Text;
using LastOrders.Core;
using LastOrders.Effects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FX = LastOrders.EditorTools.PlantingEffects;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// What plays when something is put down on the build grid: the landing burst, built in code on
    /// the planting bursts' textures - a ring of dust puffs rolling out from the foot of the thing, a
    /// ring spreading across the ground, a twinkle of stars going up and a scatter of confetti flecks
    /// in the diner's colours - sized for a one-metre footprint and scaled up to each object's cells
    /// when it plays. And the <see cref="PlacementJuice"/> in the scene that drops every placed
    /// object in (<see cref="PlacementPop"/>) and plays the burst where it lands.
    /// </summary>
    public static class PlacementEffects
    {
        public const string BurstPath = EffectPrefabGenerator.Folder + "/Fx_PlaceLanding.prefab";
        const string RootName = "PlacementJuice";

        [MenuItem("Last Orders/Effects/Set Up Placement Animation (Current Scene)", priority = 160)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This builds the landing burst for placed objects and adds the placement animation to the open scene:\n\n" +
                $"- {BurstPath} (rebuilt in place; its materials and meshes beside the planting burst's)\n" +
                $"- '{RootName}' in the scene: everything placed or moved on the build grid drops in, squashes, wobbles and kicks up dust\n\n" +
                "The scene is left unsaved.",
                "Set Up", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        /// <summary>Builds the burst and sets up the scene, with no dialogs.</summary>
        public static string Apply()
        {
            PooledEffect burst = BuildBurst();
            string scene = SetUpScene(burst);

            var report = new StringBuilder("[Last Orders] Placement animation set up.\n");
            report.Append($"  {BurstPath}: puffs, ring, sparkles, confetti.\n");
            report.Append($"  {scene}\n");
            return report.ToString();
        }

        // --- The scene --------------------------------------------------------------------------------

        static string SetUpScene(PooledEffect burst)
        {
            Scene scene = SceneManager.GetActiveScene();

            PlacementJuice juice = Object.FindFirstObjectByType<PlacementJuice>(FindObjectsInactive.Include);
            bool created = juice == null;
            if (created)
            {
                var go = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(go, scene);
                juice = go.AddComponent<PlacementJuice>();
                Undo.RegisterCreatedObjectUndo(go, "Set Up Placement Animation");
            }
            else
            {
                Undo.RecordObject(juice, "Set Up Placement Animation");
            }

            var scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            juice.Configure(scope, burst);
            EditorUtility.SetDirty(juice);
            EditorSceneManager.MarkSceneDirty(juice.gameObject.scene);

            return $"'{juice.name}' {(created ? "added to" : "updated in")} {scene.name}" +
                   (scope == null ? " - no GameLifetimeScope found, so the dust will not play until one is assigned." : ".");
        }

        // --- The burst --------------------------------------------------------------------------------

        static PooledEffect BuildBurst()
        {
            ContentGenerator.EnsureFolder(EffectPrefabGenerator.Folder);

            Material puff = FX.SpriteMaterial("Place_Puff", Texture("Particle_Puff", FX.Puff), false, 0.55f, 0.25f);
            Material ring = FX.SpriteMaterial("Place_Ring", Texture("Particle_Ring", FX.Ring), false, 0.7f, 0.08f);
            Material sparkle = FX.SpriteMaterial("Place_Sparkle", Texture("Particle_Sparkle", FX.Sparkle), true, 0f, 0.3f);
            Material solid = FX.SolidMaterial();
            Mesh fleck = AssetDatabase.LoadAssetAtPath<Mesh>($"{FX.Folder}/Particle_Confetti.asset") ??
                         FX.SaveMesh("Particle_Confetti", new Vector3(1f, 0.14f, 0.62f), 0.06f, 1);

            var root = new GameObject("Fx_PlaceLanding");
            try
            {
                Puffs(root.transform, puff);
                GroundRing(root.transform, ring);
                Sparkles(root.transform, sparkle);
                Confetti(root.transform, solid, fleck);
                root.AddComponent<PooledEffect>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BurstPath);
                return prefab.GetComponent<PooledEffect>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static Texture2D Texture(string name, System.Func<float, float, Color> paint)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>($"{FX.Folder}/{name}.png");
            return existing != null ? existing : FX.Paint(name, paint);
        }

        /// <summary>Dust rolled out low from all round the foot, swelling and slowing as it fades.</summary>
        static void Puffs(Transform root, Material material)
        {
            ParticleSystem ps = FX.NewSystem(root, "Puffs", material, ParticleSystemRenderMode.Billboard);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 1.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.36f);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(FX.Fade(FX.Hex("EFE3CB"), 0.95f), FX.Fade(FX.Hex("D6C2A0"), 0.9f));
            main.maxParticles = 24;

            FX.Burst(ps, 0f, 18);

            // Round the edge of the footprint, flat on the ground, thrown outward.
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
            shape.radiusThickness = 0.15f;
            shape.position = new Vector3(0f, 0.06f, 0f);
            shape.rotation = new Vector3(90f, 0f, 0f);

            FX.Rise(ps, 0.15f, 0.35f);
            FX.Drag(ps, 0.15f, 0.28f);
            FX.Grow(ps, FX.Curve((0f, 0.5f), (0.3f, 1.1f), (1f, 1.45f)));
            FX.FadeOut(ps, 0.4f);
            FX.Spin(ps, -60f, 60f);
        }

        /// <summary>A ring spreading out across the ground from the foot.</summary>
        static void GroundRing(Transform root, Material material)
        {
            ParticleSystem ps = FX.NewSystem(root, "Ring", material, ParticleSystemRenderMode.HorizontalBillboard);
            var main = ps.main;
            main.startLifetime = 0.45f;
            main.startSpeed = 0f;
            main.startSize = 1.9f;
            main.startColor = FX.Fade(FX.Hex("FFF4DC"), 0.6f);
            main.maxParticles = 1;

            FX.Burst(ps, 0f, 1);

            // Over the diner's floor slab (its top is 0.035 m up) as well as the grass.
            var shape = ps.shape;
            shape.enabled = false;
            ps.transform.localPosition = new Vector3(0f, 0.06f, 0f);

            FX.Grow(ps, FX.Curve((0f, 0.45f), (0.35f, 0.85f), (1f, 1f)));
            FX.FadeOut(ps, 0f);
        }

        /// <summary>Four-point stars popping up over it, glowing.</summary>
        static void Sparkles(Transform root, Material material)
        {
            ParticleSystem ps = FX.NewSystem(root, "Sparkles", material, ParticleSystemRenderMode.Billboard);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.27f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-20f * Mathf.Deg2Rad, 20f * Mathf.Deg2Rad);
            main.startColor = new ParticleSystem.MinMaxGradient(FX.Hex("FFE58A"), FX.Hex("FFF8DA"));
            main.maxParticles = 14;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.02f, 6), new ParticleSystem.Burst(0.14f, 4) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.45f;
            shape.position = new Vector3(0f, 0.55f, 0f);

            FX.Rise(ps, 0.3f, 0.5f);
            FX.Grow(ps, FX.Curve((0f, 0f), (0.18f, 1f), (0.6f, 0.75f), (1f, 0f)));
            FX.Spin(ps, 60f, 140f);

            ps.GetComponent<ParticleSystemRenderer>().sortingFudge = -20f;
        }

        /// <summary>Flecks in the diner's colours thrown up and tumbling back down round it.</summary>
        static void Confetti(Transform root, Material material, Mesh mesh)
        {
            ParticleSystem ps = FX.NewSystem(root, "Confetti", material, ParticleSystemRenderMode.Mesh, mesh);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 3.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.065f, 0.095f);
            main.startRotation3D = true;
            main.startRotationX = FX.Turn();
            main.startRotationY = FX.Turn();
            main.startRotationZ = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(Livery()) { mode = ParticleSystemGradientMode.RandomColor };
            main.gravityModifier = 0.9f;
            main.maxParticles = 24;

            FX.Burst(ps, 0f, 18);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.35f;
            shape.position = new Vector3(0f, 0.3f, 0f);
            shape.rotation = new Vector3(-90f, 0f, 0f);

            FX.Drag(ps, 1.2f, 0.12f);
            FX.Grow(ps, FX.Curve((0f, 1f), (0.75f, 1f), (1f, 0f)));
            FX.Spin3D(ps, 240f, 560f);
        }

        /// <summary>Honey, teal, tomato red and cream - the dining set's colours - one picked per fleck.</summary>
        static Gradient Livery()
        {
            var gradient = new Gradient { mode = GradientMode.Fixed };
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(FX.Hex("E8B64C"), 0.25f), new GradientColorKey(FX.Hex("3FA39A"), 0.5f),
                    new GradientColorKey(FX.Hex("D9534A"), 0.75f), new GradientColorKey(FX.Hex("F4E9D0"), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }
    }
}
