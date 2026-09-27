using LastOrders.Effects;
using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Builds the pooled particle effects, each as its own prefab.
    ///
    /// Created only when missing, like the UI prefabs: an effect is exactly the kind of thing
    /// an artist will want to retune by hand, and a generator that overwrote it would be run
    /// once and then avoided.
    /// </summary>
    public static class EffectPrefabGenerator
    {
        public const string Folder = "Assets/Game Assets/Content/LastOrders/Prefabs/Effects";

        const string BuildDustPath = Folder + "/Fx_BuildDust.prefab";
        const string ChipsMaterialPath = Folder + "/Fx_Chips.mat";

        /// <summary>URP's own particle material: soft round puffs, unlit, alpha blended.</summary>
        const string ParticlesUnlitPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

        /// <summary>The puff of dust and flying chips where a wall section goes up.</summary>
        public static PooledEffect EnsureBuildDust()
        {
            ContentGenerator.EnsureFolder(Folder);

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BuildDustPath);
            if (existing != null) return existing.GetComponent<PooledEffect>();

            var root = new GameObject("Fx_BuildDust");
            Dust(root.AddComponent<ParticleSystem>());

            var chips = new GameObject("Chips");
            chips.transform.SetParent(root.transform, false);
            Chips(chips.AddComponent<ParticleSystem>());

            root.AddComponent<PooledEffect>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BuildDustPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[Last Orders] Created effect prefab {BuildDustPath}.");
            return prefab.GetComponent<PooledEffect>();
        }

        /// <summary>A low, spreading cloud: slow, rising a little, growing as it fades.</summary>
        static void Dust(ParticleSystem system)
        {
            ParticleSystem.MainModule main = system.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.64f, 0.58f, 0.5f, 0.85f),
                                                                new Color(0.84f, 0.8f, 0.72f, 0.85f));
            main.gravityModifier = -0.08f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });

            UpwardCone(system.shape, angle: 70f, radius: 0.8f);

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));

            FadeOut(system.colorOverLifetime);

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticlesUnlitPath);
        }

        /// <summary>A handful of small tumbling blocks thrown up and falling back.</summary>
        static void Chips(ParticleSystem system)
        {
            ParticleSystem.MainModule main = system.main;
            main.duration = 0.3f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.48f, 0.48f, 0.5f),
                                                                new Color(0.7f, 0.66f, 0.6f));
            main.gravityModifier = 1.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 20;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

            UpwardCone(system.shape, angle: 35f, radius: 0.4f);

            ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            renderer.sharedMaterial = ChipsMaterial();
        }

        /// <summary>Cones point along +Z; turned to point up out of the ground.</summary>
        static void UpwardCone(ParticleSystem.ShapeModule shape, float angle, float radius)
        {
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        static void FadeOut(ParticleSystem.ColorOverLifetimeModule colour)
        {
            colour.enabled = true;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });

            colour.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>The particle material without its soft round texture, so a chip reads as a solid block.</summary>
        static Material ChipsMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ChipsMaterialPath);
            if (material != null) return material;

            var source = AssetDatabase.LoadAssetAtPath<Material>(ParticlesUnlitPath);
            if (source == null) return null;

            material = new Material(source);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);

            AssetDatabase.CreateAsset(material, ChipsMaterialPath);
            return material;
        }
    }
}
