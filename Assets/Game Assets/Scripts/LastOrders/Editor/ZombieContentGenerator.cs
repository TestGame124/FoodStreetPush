using FoodStreet.Navigation;
using LastOrders.Data;
using LastOrders.Night;
using UnityEditor;
using UnityEngine;
using K = LastOrders.EditorTools.PrimitiveKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Builds the zombies: the one the night sends up the road, and the ones that wander the
    /// ruined town by day.
    ///
    /// Both are the project's own characters under a <see cref="ZombieBody"/>, which does the
    /// shambling, the stoop and the outstretched arms. Three different people sit under each
    /// prefab and one is picked per life, so a pool of one prefab is still a crowd.
    ///
    /// The night's zombie replaces the grey capsule on every archetype that was still using it.
    /// Raiders are the exception: they are people, and the design says so. A prefab somebody
    /// has authored by hand is never replaced.
    ///
    /// Safe to re-run: prefabs are only built when missing, unless a rebuild is asked for.
    /// </summary>
    public static class ZombieContentGenerator
    {
        public const string Folder = "Assets/Game Assets/Content/LastOrders/Prefabs/Enemies";
        public const string ZombiePath = Folder + "/Enemy_Zombie.prefab";
        public const string WandererPath = Folder + "/Zombie_Wanderer.prefab";

        const string GreyboxPath = "Assets/Game Assets/Content/LastOrders/Prefabs/Enemy_Greybox.prefab";

        /// <summary>Which of the project's characters the zombies are made from. Not the guard's
        /// model, so a zombie at the foot of the tower never looks like the one up in it.</summary>
        static readonly int[] Bodies = { 2, 4, 0 };

        /// <summary>The archetypes that stay as they are: people, not the dead.</summary>
        static readonly string[] NotZombies = { "Raider" };

        [MenuItem("Last Orders/Hospital & Zombies/Rebuild Zombie Prefabs", priority = 61)]
        static void Rebuild() => Generate(rebuild: true);

        /// <summary>Builds whatever is missing and puts the zombie on the archetypes. Returns a
        /// line for the setup report.</summary>
        public static string Generate(bool rebuild)
        {
            ContentGenerator.EnsureFolder(Folder);
            K.Reset();

            GameObject zombie = Ensure(ZombiePath, rebuild, BuildZombie);
            Ensure(WandererPath, rebuild, BuildWanderer);

            int assigned = AssignToArchetypes(zombie);

            AssetDatabase.SaveAssets();
            return $"Zombie prefabs ready; {assigned} archetypes use the zombie.";
        }

        public static GameObject LoadWanderer() => AssetDatabase.LoadAssetAtPath<GameObject>(WandererPath);

        static GameObject Ensure(string path, bool rebuild, System.Func<GameObject> build)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !rebuild) return existing;

            GameObject root = build();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            Debug.Log($"[Last Orders] {(existing != null ? "Rebuilt" : "Created")} {path}.");
            return prefab;
        }

        // --- The prefabs ----------------------------------------------------------------------

        /// <summary>The night's zombie. Components in the order the grey-box capsule had them:
        /// the steering first, because the other two require it.</summary>
        static GameObject BuildZombie()
        {
            var root = new GameObject("Enemy_Zombie");

            root.AddComponent<SteeringAgent>();
            root.AddComponent<FlowFieldFollower>();
            root.AddComponent<EnemyAgent>();

            AddBodies(root);
            return root;
        }

        /// <summary>A town zombie: the body and nothing else. Nothing navigates it and nothing
        /// shoots at it.</summary>
        static GameObject BuildWanderer()
        {
            var root = new GameObject("Zombie_Wanderer");
            AddBodies(root);
            return root;
        }

        static void AddBodies(GameObject root)
        {
            var variants = new GameObject[Bodies.Length];

            // The zombies' own clips once they are built; the visitors' walk, bent by the body, until then.
            var zombieController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ZombieAnimations.ControllerPath);

            for (int i = 0; i < Bodies.Length; i++)
            {
                GameObject body = VisitorCharacterGenerator.CreateCharacter(Bodies[i], $"Body_{i}", Vector3.zero, root.transform);

                if (body == null)
                {
                    // No character art: a hunched capsule still reads as something coming.
                    Transform fallback = K.Group($"Body_{i}", root.transform);
                    K.Capsule("Torso", fallback, Vector3.zero, 0.5f, 1.25f, K.Hex("6F7F57"));
                    K.Sphere("Head", fallback, new Vector3(0f, 1.38f, 0.12f), 0.4f, K.Hex("8D9A73"));
                    body = fallback.gameObject;
                }

                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = Quaternion.identity;
                body.SetActive(i == 0);

                if (zombieController != null)
                    foreach (Animator animator in body.GetComponentsInChildren<Animator>(true))
                        animator.runtimeAnimatorController = zombieController;

                variants[i] = body;
            }

            root.AddComponent<ZombieBody>().Configure(variants);
        }

        // --- The archetypes -------------------------------------------------------------------

        /// <summary>
        /// Puts the zombie on every archetype still using the grey-box capsule, and names the
        /// first night's archetype what the player calls it.
        /// </summary>
        static int AssignToArchetypes(GameObject zombie)
        {
            string[] guids = AssetDatabase.FindAssets("t:EnemyDefinition");
            var greybox = AssetDatabase.LoadAssetAtPath<GameObject>(GreyboxPath);
            int assigned = 0;

            foreach (string guid in guids)
            {
                var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (enemy == null) continue;

                bool changed = false;

                if (enemy.name == "Enemy_Shambler" && enemy.displayName != ContentGenerator.ZombieName)
                {
                    enemy.displayName = ContentGenerator.ZombieName;
                    enemy.description = ContentGenerator.ZombieDescription;
                    changed = true;
                }

                bool person = System.Array.IndexOf(NotZombies, enemy.displayName) >= 0;
                bool placeholder = enemy.prefab == null || enemy.prefab == greybox;

                if (!person && placeholder && zombie != null)
                {
                    enemy.prefab = zombie;
                    changed = true;
                }

                if (enemy.prefab == zombie) assigned++;
                if (changed) EditorUtility.SetDirty(enemy);
            }

            return assigned;
        }
    }
}
