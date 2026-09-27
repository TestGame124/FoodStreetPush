using System.Collections.Generic;
using LastOrders.Blockout;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Jobs;
using LastOrders.Kitchen;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using K = LastOrders.EditorTools.PrimitiveKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Puts the Last Orders systems into the project's own working level, and takes the
    /// grey-box out of it.
    ///
    /// The grey-box compound was built to answer a layout question and it answered it. It is
    /// the wrong thing to keep playing in: Main Level already has the terrain, the props, the
    /// lighting, the camera, the build grid, the shop and the cooking UI, and none of that is
    /// worth rebuilding out of cubes. So this goes the other way round - the systems move to
    /// the level rather than the level moving to the systems.
    ///
    /// Everything it needs is derived from what is already in the scene: the food on the pass
    /// comes from the level's own cooking, and the build grid is the level's own. The only
    /// things this adds that did not exist are the day/phase HUD and the two workers the job
    /// queue cannot run without.
    ///
    /// Customers are NOT set up here. They arrive by road in vehicles and park inside the
    /// zone, which needs the road, the boundary and the parking to agree with one another -
    /// so that is one command of its own, 'Food Street / Visitors / Set Up Visitor System'.
    ///
    /// It edits Main Level in place, on purpose. A copy would be a second scene to keep in
    /// step with every art change, which is exactly the trap the grey-box fell into.
    /// </summary>
    public static class MainLevelSetup
    {
        const string ScenePath = "Assets/GridBuildingSystem/Main Level.unity";
        const string BalancePath = "Assets/Game Assets/Content/LastOrders/GameBalance.asset";

        /// <summary>
        /// Roots the grey-box pass left behind. Destroyed, not parked: they are a duplicate
        /// compound sitting on top of a real level, and every one of them is regenerable from
        /// 'Build Compound Scene'.
        /// </summary>
        static readonly string[] GreyboxRoots = { "LastOrders_Compound", "Visitors" };

        [MenuItem("Last Orders/Set Up Main Level", priority = 1)]
        public static void Run()
        {
            GameBalanceConfig balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(BalancePath);
            if (balance == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "No GameBalance asset found.\n\nRun 'Last Orders / Generate Starting Content' first.",
                    "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This edits Main Level in place:\n\n" +
                "- Removes the grey-box compound and its camera override\n" +
                "- Adds the Last Orders systems and the day/phase HUD\n" +
                "- Adds the Chef and Helper the job queue needs\n\n" +
                "The level's art, camera, build grid and UI are not touched. " +
                "Any unsaved changes in the current scene will be lost.",
                "Set up", "Cancel");

            if (!proceed) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            K.Reset();

            int removed = RemoveGreybox(scene);

            Zone zone = Object.FindFirstObjectByType<Zone>();
            if (zone == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "Main Level has no Zone, so there is no road and no restaurant to bind to.",
                    "OK");
                return;
            }

            GridBuildingSystem3D grid = Object.FindFirstObjectByType<GridBuildingSystem3D>();
            GameLifetimeScope scope = EnsureScope(balance, grid);

            var host = new GameObject("LevelSystems").transform;

            // Test controls and a sun that follows the phase, so the cycle can be seen and
            // driven without waiting six real minutes for it.
            // The HUD builds its own TimeControls with the buttons attached; this one is the
            // fallback for a scene whose HUD was not built, so it has no buttons to bind.
            host.gameObject.AddComponent<Debugging.TimeControls>()
                .Configure(scope, null, null, null, null);
            host.gameObject.AddComponent<DayNightLighting>().Configure(scope, Sun());

            int workers = EnsureWorkers(scope, zone, host);

            // Minimal: this level already has a wallet, a shop and a build button. All the
            // day cycle adds is the phase, the button that starts it, and a line for tap
            // replies.
            int hud = CompoundHudBuilder.Build(scope, minimal: true);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            int dishes = Object.FindObjectsByType<CookeryStation>(FindObjectsSortMode.None).Length;

            Debug.Log($"[Last Orders] Main Level is set up.\n" +
                      $"{removed} grey-box objects removed, {workers} workers on the job " +
                      $"queue, {hud} HUD elements added.\n" +
                      $"{dishes} cooking stations found - the menu is built from these, and with " +
                      "none of them nobody can order anything.\n" +
                      "Customers are a separate command: run 'Food Street / Visitors / Set Up " +
                      "Visitor System' to lay the road, the parking and the traffic.");

            if (dishes == 0)
            {
                Debug.LogWarning("[Last Orders] No CookeryStation in the level, so the menu will " +
                                 "be empty and no visitor order can ever be filled. Place a " +
                                 "cooking station.");
            }
        }

        // --- Taking the grey-box back out -----------------------------------------------------

        /// <summary>
        /// Destroys the compound overlay and undoes what it did to the level around it.
        ///
        /// The camera matters more than the geometry. The grey-box framed an orthographic
        /// top-down view of a 48 x 40 metre plan; Main Level is a perspective camera on a
        /// Cinemachine brain looking at a street. Leaving a BlockoutCameraRig behind would
        /// fight the brain for the transform every single frame.
        /// </summary>
        static int RemoveGreybox(Scene scene)
        {
            // Gathered first, destroyed after. Destroying while walking the hierarchy hands
            // the loop transforms whose GameObject has already gone with its parent.
            var doomed = new List<GameObject>();

            // Every transform in the scene, not just the roots, and including deactivated
            // ones. A grey-box that was switched off rather than deleted is still a duplicate
            // compound sitting on the level, and switched off is exactly the state somebody
            // leaves it in when they are trying to see their own scene again.
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null) continue;

                GameObject go = t.gameObject;
                if (go.scene != scene) continue;
                if ((go.hideFlags & HideFlags.HideAndDontSave) != 0) continue;
                if (System.Array.IndexOf(GreyboxRoots, go.name) < 0) continue;

                doomed.Add(go);
            }

            int removed = 0;
            for (int i = 0; i < doomed.Count; i++)
            {
                if (doomed[i] == null) continue;

                Object.DestroyImmediate(doomed[i]);
                removed++;
            }

            foreach (BlockoutCameraRig rig in
                     Object.FindObjectsByType<BlockoutCameraRig>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(rig);
            }

            // The grey-box selector is redundant here: Main Level has Mouse3D, which is a
            // better tap layer and already dispatches to everything Last Orders puts down.
            foreach (Interaction.TapSelector selector in
                     Object.FindObjectsByType<Interaction.TapSelector>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(selector);
            }

            // The compound pass switched this off to stop its floors fighting the terrain.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Floors" && !root.activeSelf) root.SetActive(true);
            }

            return removed;
        }

        // --- The systems layer ------------------------------------------------------------------

        static GameLifetimeScope EnsureScope(GameBalanceConfig balance, GridBuildingSystem3D grid)
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();

            if (scope == null)
            {
                var root = new GameObject("LastOrders");
                scope = root.AddComponent<GameLifetimeScope>();
            }

            Transform pools = scope.transform.Find("Pools");
            if (pools == null)
            {
                var host = new GameObject("Pools");
                host.transform.SetParent(scope.transform, false);
                pools = host.transform;
            }

            var so = new SerializedObject(scope);
            so.FindProperty("balance").objectReferenceValue = balance;
            so.FindProperty("buildGrid").objectReferenceValue = grid;
            so.FindProperty("poolRoot").objectReferenceValue = pools;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Lane overrides pointed at compound markers that no longer exist. Cleared rather
            // than re-pointed, so the spawner falls back to placing lanes around the grid -
            // which is the right behaviour on a level whose approaches were never authored.
            foreach (Night.EnemySpawner spawner in
                     Object.FindObjectsByType<Night.EnemySpawner>(FindObjectsSortMode.None))
            {
                var sso = new SerializedObject(spawner);
                sso.FindProperty("scope").objectReferenceValue = scope;

                SerializedProperty lanes = sso.FindProperty("laneOverrides");
                if (lanes != null) lanes.arraySize = 0;

                sso.ApplyModifiedPropertiesWithoutUndo();
            }

            return scope;
        }

        // --- Somebody to do the work -----------------------------------------------------------

        /// <summary>
        /// Makes sure there is a Chef and a Helper on the job queue.
        ///
        /// Not optional, and the reason this method exists at all: every Last Orders
        /// interaction is a JOB, and a job with nobody to claim it never completes. Turning the
        /// sign round is a Chef job, so with no Chef the restaurant can never open and not one
        /// customer ever arrives - and from the outside that looks exactly like the spawner
        /// being broken. Clearing tables is a Helper job, so with no Helper every table stays
        /// dirty after its first customer and the dining room closes itself.
        ///
        /// The level's own StaffBehaviour waiters are a different system entirely: they carry
        /// plates to seated customers and know nothing about the queue. These two stand beside
        /// them rather than replacing them.
        /// </summary>
        static int EnsureWorkers(GameLifetimeScope scope, Zone zone, Transform parent)
        {
            Vector3 door = zone.EntryPosition != null
                ? zone.EntryPosition.position
                : zone.transform.position;

            int made = 0;

            if (!HasWorker(WorkerRole.Chef))
            {
                MakeWorker(scope, WorkerRole.Chef, "Chef", 2, door + new Vector3(1.6f, 0f, 1.6f), parent);
                made++;
            }

            if (!HasWorker(WorkerRole.Helper))
            {
                MakeWorker(scope, WorkerRole.Helper, "Helper", 1, door + new Vector3(-1.6f, 0f, 1.6f), parent);
                made++;
            }

            return made;
        }

        /// <summary>
        /// True when an ACTIVE worker of this role exists.
        ///
        /// Active matters. The grey-box put its Chef and Helper inside the compound root, and a
        /// deactivated worker is a worker that never runs its Start, never claims a job, and
        /// never tells anybody why.
        /// </summary>
        internal static bool HasWorker(WorkerRole role)
        {
            foreach (WorkerAgent agent in
                     Object.FindObjectsByType<WorkerAgent>(FindObjectsSortMode.None))
            {
                if (agent.Role == role && agent.gameObject.activeInHierarchy) return true;
            }

            return false;
        }

        internal static WorkerAgent MakeWorker(GameLifetimeScope scope, WorkerRole role, string name,
                                               int model, Vector3 at, Transform parent)
        {
            GameObject go = VisitorCharacterGenerator.CreateCharacter(model, name, at, parent);

            if (go == null)
            {
                // No character art. A capsule is still a worker, and a worker that looks wrong
                // is better than a restaurant that cannot open.
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.position = at;

                K.Capsule("Body", go.transform, Vector3.zero, 0.5f, 1.25f,
                          role == WorkerRole.Chef ? K.ChefWhite : K.Denim);
            }

            var agent = go.AddComponent<WorkerAgent>();

            // intValue, not enumValueIndex: the index is a position in the enum's name list and
            // only happens to match the value while the enum has no gaps.
            var so = new SerializedObject(agent);
            so.FindProperty("role").intValue = (int)role;
            so.FindProperty("scope").objectReferenceValue = scope;
            so.ApplyModifiedPropertiesWithoutUndo();

            return agent;
        }

        /// <summary>The scene's brightest directional light. What the phase lighting drives,
        /// captured as its own DAY setting so the level looks unchanged in daylight.</summary>
        static Light Sun()
        {
            Light best = null;

            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                if (best == null || light.intensity > best.intensity) best = light;
            }

            return best;
        }

    }
}
