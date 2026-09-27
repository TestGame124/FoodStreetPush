using FoodStreet.Navigation;
using LastOrders.Data;
using LastOrders.Night;
using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Builds the grey-box prefabs the systems need in order to be visible.
    ///
    /// GDD Phase A is explicitly a grey-box milestone: Gate 1 asks whether "one more table
    /// before dusk" creates tension, and that question is answerable with capsules. What it
    /// is not answerable without is anything on screen at all - so rather than leave the
    /// enemy definitions with null prefabs and a night where the wave director reports
    /// spawns that never appear, this generates a single agent prefab they all share.
    ///
    /// One prefab for all eight archetypes is correct here, not a shortcut. The whole point
    /// of the flow-field architecture is that the archetypes differ by data, and the colour
    /// is applied per spawn from the definition's silhouette hue.
    /// </summary>
    public static class PrefabGenerator
    {
        const string Folder = "Assets/Game Assets/Content/LastOrders/Prefabs";
        const string EnemyPath = Folder + "/Enemy_Greybox.prefab";

        /// <summary>Creates the enemy prefab if missing and assigns it to every definition
        /// that has none. Never overwrites a prefab someone has authored by hand.</summary>
        public static GameObject EnsureEnemyPrefab(EnemyDefinition[] enemies)
        {
            ContentGenerator.EnsureFolder(Folder);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath);
            if (prefab == null) prefab = CreateEnemyPrefab();

            if (enemies != null)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    if (enemies[i] == null || enemies[i].prefab != null) continue;

                    enemies[i].prefab = prefab;
                    EditorUtility.SetDirty(enemies[i]);
                }
            }

            return prefab;
        }

        static GameObject CreateEnemyPrefab()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "Enemy_Greybox";

            // 24.6: kinematic and custom overlap queries only, no rigidbody stacking. The
            // agent navigates on the grid and never touches physics, so the primitive's
            // collider is pure cost at 120 concurrent enemies.
            Object.DestroyImmediate(root.GetComponent<Collider>());

            root.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);

            // Order matters: SteeringAgent first, because the other two require it and
            // AddComponent would otherwise add a second one implicitly.
            root.AddComponent<SteeringAgent>();
            root.AddComponent<FlowFieldFollower>();
            root.AddComponent<EnemyAgent>();

            ApplyGreyboxMaterial(root);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, EnemyPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[Last Orders] Created grey-box enemy prefab at {EnemyPath}.");
            return prefab;
        }

        /// <summary>
        /// Gives the capsule a URP-lit material it can be tinted through.
        ///
        /// The project is URP, so the primitive's default built-in material would render
        /// magenta. Creating one explicitly is the difference between a visible enemy and an
        /// error-shader blob.
        /// </summary>
        static void ApplyGreyboxMaterial(GameObject target)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                            ?? Shader.Find("Standard");

            if (shader == null) return;

            string path = Folder + "/Enemy_Greybox.mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.color = Color.white;
            EditorUtility.SetDirty(material);

            var renderer = target.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }
    }
}
