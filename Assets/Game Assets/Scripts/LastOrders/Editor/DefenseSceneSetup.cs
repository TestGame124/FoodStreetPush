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
        /// The scrap turret the restaurant starts with: on the grass just west of the drive, in
        /// front of the diner, resting toward the gate.
        ///
        /// Every zombie that gets inside makes for the diner door, whichever wall it came over,
        /// so a gun here covers the one place they all end up - 5.6 m from the door, inside the
        /// Level 1 gun's 7 m - and sees up the drive toward the gate. It stands clear of the
        /// drive (x 17-23) and the visitors' line to the door (x 20), and its pad stops short of
        /// z 11.5, so the diner can still grow its full 3 m forward (to z 12).
        /// </summary>
        static readonly Site TurretSite = new Site("turret_diner", new Vector3(15.4f, 0f, 10.4f), 180f, startsBuilt: true);

        /// <summary>
        /// The Toastapult: the turret's mirror across the drive, on the grass between the top of the
        /// drive and the car park (x 25.4), 6.1 m from the diner door. Its leaves and roots reach
        /// 0.7 m from the middle, clear of the drive, the bays and the diner's 3 m to grow forward.
        /// </summary>
        static readonly Site ToastapultSite = new Site("toastapult_diner", new Vector3(24.2f, 0f, 10.4f), 180f, startsBuilt: true);

        /// <summary>
        /// Where this level's night comes from, one point per lane.
        ///
        /// The spawner's own placement assumes the road is to the north. Here it is to the south,
        /// so left to itself the night arrives from wherever the grid's bounding box happens to
        /// fall. The world-edge layout owns the real approaches - lane 0 across the road to the
        /// gate the starting tower watches, then west, east and out of the trees - and each
        /// point sits square with its wall, because an enemy off the nav grid walks straight in
        /// to its nearest edge.
        /// </summary>
        static EnvironmentLayout.Lane[] RoadLanes => EnvironmentLayout.Lanes;

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
                "- Points the enemy lanes at the world edge's approaches, if none are authored\n" +
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

        [MenuItem("Last Orders/Defense/Set Up Starting Turret (Current Scene)", priority = 42)]
        public static void RunTurret()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();

            if (scope == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "This scene has no GameLifetimeScope, so nothing would run the turret.\n\n" +
                    "Open the level that has the Last Orders systems in it.", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                $"This edits '{scene.name}' in place:\n\n" +
                $"- Adds the scrap turret the restaurant starts with, in front of the diner at {TurretSite.Position}\n" +
                "- Creates the turret's content (definition, guns, motion profile) and prefab if they do not exist\n\n" +
                "A turret already in the scene is rewired where it stands, not moved. The guard towers are left alone.",
                "Set up", "Cancel");

            if (!proceed) return;

            Debug.Log(ApplyTurret(scope, save: true));
        }

        /// <summary>The turret setup against the open scene's scope, with no dialogs - for automation.</summary>
        public static string ApplyTurretToActiveScene(bool save)
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            return scope != null
                ? ApplyTurret(scope, save)
                : "[Last Orders] No GameLifetimeScope in the open scene; nothing was changed.";
        }

        /// <summary>
        /// Places the starting turret, with no dialogs. Found by its id if it is already there, and
        /// then only rewired - never moved, so a turret dragged somewhere better by hand stays put.
        /// </summary>
        public static string ApplyTurret(GameLifetimeScope scope, bool save)
        {
            DefenseDefinition turret = DefenseContentGenerator.GenerateScrapTurret();
            GameObject prefab = ScrapTurretModel.Ensure(turret, DefenseContentGenerator.LoadScrapTurretMotion());
            return PlaceStartingDefense(scope, TurretSite, turret, prefab, "ScrapTurret", save);
        }

        [MenuItem("Last Orders/Defense/Set Up Toastapult (Current Scene)", priority = 44)]
        public static void RunToastapult()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();

            if (scope == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "This scene has no GameLifetimeScope, so nothing would run the Toastapult.\n\n" +
                    "Open the level that has the Last Orders systems in it.", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                $"This edits '{scene.name}' in place:\n\n" +
                $"- Plants the Toastapult in front of the diner at {ToastapultSite.Position}, across the drive from the scrap turret\n" +
                "- Creates its content (definition, toast, motion profile) and prefab if they do not exist\n\n" +
                "One already in the scene is rewired where it stands, not moved. Nothing else is touched.",
                "Set up", "Cancel");

            if (!proceed) return;

            Debug.Log(ApplyToastapult(scope, save: true));
        }

        /// <summary>The Toastapult setup against the open scene's scope, with no dialogs - for automation.</summary>
        public static string ApplyToastapultToActiveScene(bool save)
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            return scope != null
                ? ApplyToastapult(scope, save)
                : "[Last Orders] No GameLifetimeScope in the open scene; nothing was changed.";
        }

        /// <summary>Plants the Toastapult, with no dialogs: placed if missing, otherwise rewired where it stands.</summary>
        public static string ApplyToastapult(GameLifetimeScope scope, bool save)
        {
            DefenseDefinition toaster = DefenseContentGenerator.GenerateToastapult();
            GameObject prefab = ToastapultModel.Ensure(toaster, DefenseContentGenerator.LoadToastapultMotion());
            return PlaceStartingDefense(scope, ToastapultSite, toaster, prefab, "Toastapult", save);
        }

        /// <summary>
        /// A defense placed at <paramref name="site"/> if no post there has its id yet, then wired to
        /// the scope and its definition. One already placed is never moved.
        /// </summary>
        static string PlaceStartingDefense(GameLifetimeScope scope, Site site, DefenseDefinition definition, GameObject prefab,
                                           string namePrefix, bool save)
        {
            Scene scene = scope.gameObject.scene;

            GameBalanceConfig balance = new SerializedObject(scope).FindProperty("balance")
                .objectReferenceValue as GameBalanceConfig;
            WorldUiPrefabGenerator.Ensure(balance);

            Transform root = FindOrCreateRoot(scene, RootName);

            DefensePost post = FindPost(root, site.Id);
            bool placed = post == null;

            if (placed)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                instance.name = $"{namePrefix}_{site.Id}";
                Undo.RegisterCreatedObjectUndo(instance, "Place defense");
                instance.transform.SetPositionAndRotation(site.Position, Quaternion.Euler(0f, site.Yaw, 0f));
                post = instance.GetComponent<DefensePost>();
            }

            var so = new SerializedObject(post);
            so.FindProperty("scope").objectReferenceValue = scope;
            so.FindProperty("definition").objectReferenceValue = definition;
            so.FindProperty("postId").stringValue = site.Id;
            so.FindProperty("startsBuilt").boolValue = site.StartsBuilt;
            so.ApplyModifiedProperties();

            ShowInEditor(so.FindProperty("builtVisuals").objectReferenceValue as GameObject, site.StartsBuilt);
            ShowInEditor(so.FindProperty("siteVisuals").objectReferenceValue as GameObject, !site.StartsBuilt);

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);

            WeaponDefinition gun = definition.Level(0)?.weapon;
            return $"[Last Orders] {definition.displayName} {(placed ? "placed" : "rewired where it stands")} at {post.transform.position}: " +
                   $"{(gun != null ? $"{gun.displayName} {gun.Describe()}" : "no weapon")}, " +
                   $"spots {definition.Level(0)?.SpotRange:0.#} m out, {(definition.requiresCrew ? "needs crew" : "needs no crew")}.";
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
                string name = RoadLanes[i].Name;
                Transform point = spawner.transform.Find(name);

                if (point == null)
                {
                    point = K.Group(name, spawner.transform);
                    Undo.RegisterCreatedObjectUndo(point.gameObject, "Place approach lane");
                }

                point.position = RoadLanes[i].Spawn;
                lanes.GetArrayElementAtIndex(i).objectReferenceValue = point;
            }

            so.ApplyModifiedProperties();
            return $"Approach lanes set to the world edge's approaches ({RoadLanes.Length}).";
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
