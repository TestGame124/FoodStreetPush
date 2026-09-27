using System.Text;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Defense;
using LastOrders.Night;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using K = LastOrders.EditorTools.PrimitiveKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Puts the guard towers into the open scene: the one the restaurant starts with, beside
    /// the gate, and the empty sites where more can be bought.
    ///
    /// The sites are this level's - the compound of Main Level_Apocalypse, whose south wall
    /// runs along z = -2.6 with the gate at x = 20 and the road beyond it. The starting tower
    /// stands just inside the wall to the west of the gate, where its guard can see the road
    /// and the gate itself is within the pistol's reach, so the one place every enemy comes
    /// through is covered from the first night. The other two sites cover the south-west
    /// corner and the east wall.
    ///
    /// A tower watching the road is only any use if the night comes up the road, so this also
    /// points the enemy spawner's lanes there when the level has none of its own.
    ///
    /// Safe to re-run. Posts already placed are found by id, moved back to their site and
    /// rewired; nothing is duplicated. A site moved by hand is put back, so move the site
    /// here rather than in the scene.
    /// </summary>
    public static class DefenseSceneSetup
    {
        const string RootName = "Defenses";

        readonly struct Site
        {
            public readonly string Id;
            public readonly Vector3 Position;

            /// <summary>Which way the guard faces at rest: toward the road, or out over the wall.</summary>
            public readonly float Yaw;

            public readonly bool StartsBuilt;

            public Site(string id, Vector3 position, float yaw, bool startsBuilt)
            {
                Id = id;
                Position = position;
                Yaw = yaw;
                StartsBuilt = startsBuilt;
            }
        }

        static readonly Site[] Sites =
        {
            // 5.9 m from the middle of the gate, so the gate is inside the pistol's 6 m.
            new Site("tower_gate", new Vector3(15.2f, 0f, 0.9f), 180f, startsBuilt: true),
            // The other two stand 2 m inside the west and east walls, which the starting-area
            // layout moved out to x = -9.5 and x = 46.
            new Site("tower_southwest", new Vector3(-7.4f, 0f, 0.9f), 180f, startsBuilt: false),
            new Site("tower_east", new Vector3(44f, 0f, 21f), 90f, startsBuilt: false),
        };

        /// <summary>
        /// Where this level's night comes from: the road, in front of the gate.
        ///
        /// The towers are sited to watch the road, but the spawner's own placement assumes the
        /// road is to the north. Here it is to the south, so left to itself the night arrives
        /// from the far side of the compound, out of every tower's sight. Both points are
        /// inside the width of the gate, because an enemy off the nav grid walks straight in
        /// to its nearest edge - and from here, that line runs through the gate.
        /// </summary>
        static readonly Vector3[] RoadLanes =
        {
            new Vector3(17.5f, 0f, -9f),
            new Vector3(22.5f, 0f, -9f),
        };

        [MenuItem("Last Orders/Defense/Set Up Guard Towers (Current Scene)", priority = 40)]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();

            if (scope == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "This scene has no GameLifetimeScope, so nothing would run the towers.\n\n" +
                    "Open the level that has the Last Orders systems in it.", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                $"This edits '{scene.name}' in place:\n\n" +
                "- Adds the guard tower the restaurant starts with, beside the gate\n" +
                $"- Adds {Sites.Length - 1} empty sites where more towers can be bought\n" +
                "- Points the enemy lanes at the road, if none are authored\n" +
                "- Creates the guard tower content and prefab if they do not exist\n\n" +
                "Back the scene up first if it is not under version control.",
                "Set up", "Cancel");

            if (!proceed) return;

            Debug.Log(Apply(scope, save: true));
        }

        /// <summary>The setup against the open scene's scope, with no dialogs - for automation.</summary>
        public static string ApplyToActiveScene(bool save)
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            return scope != null
                ? Apply(scope, save)
                : "[Last Orders] No GameLifetimeScope in the open scene; nothing was changed.";
        }

        /// <summary>The whole setup, with no dialogs, so it can also be driven from a script.</summary>
        public static string Apply(GameLifetimeScope scope, bool save)
        {
            Scene scene = scope.gameObject.scene;
            K.Reset();

            GameBalanceConfig balance = new SerializedObject(scope).FindProperty("balance")
                .objectReferenceValue as GameBalanceConfig;

            // The popup and the build button come from the world UI catalog.
            WorldUiPrefabGenerator.Ensure(balance);

            DefenseDefinition tower = DefenseContentGenerator.Generate();
            GameObject prefab = DefensePrefabGenerator.EnsureGuardTower(tower, rebuild: false);

            Transform root = FindOrCreateRoot(scene, RootName);
            var report = new StringBuilder("[Last Orders] Guard towers set up.\n");

            foreach (Site site in Sites)
            {
                DefensePost post = FindPost(root, site.Id) ?? Place(prefab, root, site.Id);

                Undo.RecordObject(post.transform, "Place defense");
                post.transform.SetPositionAndRotation(site.Position, Quaternion.Euler(0f, site.Yaw, 0f));

                // Through SerializedObject so the values are recorded as overrides on the prefab
                // instance, rather than set on the object and lost the next time it reloads.
                var so = new SerializedObject(post);
                so.FindProperty("scope").objectReferenceValue = scope;
                so.FindProperty("definition").objectReferenceValue = tower;
                so.FindProperty("postId").stringValue = site.Id;
                so.FindProperty("startsBuilt").boolValue = site.StartsBuilt;
                so.ApplyModifiedProperties();

                // The post sorts this out itself at runtime; set here as well so an empty site
                // looks empty in the editor rather than like a tower standing on its own pad.
                ShowInEditor(so.FindProperty("builtVisuals").objectReferenceValue as GameObject, site.StartsBuilt);
                ShowInEditor(so.FindProperty("siteVisuals").objectReferenceValue as GameObject, !site.StartsBuilt);

                report.Append($"  {site.Id}: {(site.StartsBuilt ? "built" : "empty site")} at {site.Position}\n");
            }

            report.Append($"  {EnsureRoadLanes()}\n");

            if (!HasGuardRole(balance))
            {
                report.Append("  WARNING: GameBalance has no Guard role, so no starter guard will be " +
                              "seeded and the tower cannot fire. Run 'Last Orders / Generate Starting Content'.\n");
            }
            else if (balance.startingGuardCount <= 0)
            {
                report.Append("  WARNING: GameBalance starts with no guards, so the starting tower " +
                              "has nobody to stand watch until one is recruited.\n");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);

            return report.ToString();
        }

        static DefensePost FindPost(Transform root, string id)
        {
            foreach (DefensePost post in root.GetComponentsInChildren<DefensePost>(true))
                if (post.PostId == id) return post;

            return null;
        }

        static DefensePost Place(GameObject prefab, Transform root, string id)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            instance.name = $"GuardTower_{id}";
            Undo.RegisterCreatedObjectUndo(instance, "Place defense");
            return instance.GetComponent<DefensePost>();
        }

        /// <summary>
        /// Points the spawner's lanes at the road. Only when it has none of its own, so this
        /// never replaces an approach a designer has placed - but note that 'Set Up Main Level'
        /// clears them, and this has to be run again after it.
        /// </summary>
        static string EnsureRoadLanes()
        {
            EnemySpawner spawner = Object.FindFirstObjectByType<EnemySpawner>();
            if (spawner == null) return "No EnemySpawner in the scene, so no approach lanes were set.";

            var so = new SerializedObject(spawner);
            SerializedProperty lanes = so.FindProperty("laneOverrides");

            for (int i = 0; i < lanes.arraySize; i++)
            {
                if (lanes.GetArrayElementAtIndex(i).objectReferenceValue != null)
                    return "Approach lanes already authored on the EnemySpawner; left as they are.";
            }

            lanes.arraySize = RoadLanes.Length;

            for (int i = 0; i < RoadLanes.Length; i++)
            {
                string name = $"Lane{i}_Road";
                Transform point = spawner.transform.Find(name);

                if (point == null)
                {
                    point = K.Group(name, spawner.transform);
                    Undo.RegisterCreatedObjectUndo(point.gameObject, "Place approach lane");
                }

                point.position = RoadLanes[i];
                lanes.GetArrayElementAtIndex(i).objectReferenceValue = point;
            }

            so.ApplyModifiedProperties();
            return $"Approach lanes set to the road in front of the gate ({RoadLanes.Length}).";
        }

        static void ShowInEditor(GameObject part, bool active)
        {
            if (part == null || part.activeSelf == active) return;

            Undo.RecordObject(part, "Place defense");
            part.SetActive(active);
        }

        static bool HasGuardRole(GameBalanceConfig balance)
        {
            if (balance == null || balance.survivorRoles == null) return false;

            foreach (SurvivorRoleDefinition role in balance.survivorRoles)
                if (role != null && role.role == SurvivorRole.Guard) return true;

            return false;
        }

        static Transform FindOrCreateRoot(Scene scene, string name)
        {
            foreach (GameObject existing in scene.GetRootGameObjects())
                if (existing.name == name) return existing.transform;

            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, name);
            return go.transform;
        }
    }
}
