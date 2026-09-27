using System.IO;
using System.Linq;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Builds the playable Last Orders scene by deriving it from the project's working level.
    ///
    /// The first version of this built a scene from nothing - a camera, a light and a plane -
    /// and the result was exactly what that describes: flat, untextured, and with no UI at
    /// all. Everything that makes the project look and behave like a game already exists in
    /// Main Level: the terrain, the lighting, the prop art, the Canvas and all its panels,
    /// the camera rig, and a GridBuildingSystem wired to its ghost and object database.
    ///
    /// So this copies that scene and adds the Last Orders layer on top, rather than trying to
    /// recreate any of it. The old bootstrap keeps running and keeps working; the new systems
    /// attach to what it produces.
    /// </summary>
    public static class SceneBuilder
    {
        const string SourceScene = "Assets/GridBuildingSystem/Main Level.unity";
        const string ScenePath = "Assets/Scenes/LastOrders_Start.unity";
        const string BalancePath = "Assets/Game Assets/Content/LastOrders/GameBalance.asset";

        [MenuItem("Last Orders/Build Starting Scene", priority = 2)]
        public static void Build()
        {
            GameBalanceConfig balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(BalancePath);
            if (balance == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "No GameBalance asset found.\n\nRun 'Last Orders / Generate Starting Content' first.",
                    "OK");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    $"Could not find the source scene:\n{SourceScene}\n\n" +
                    "Point SourceScene at the level you want to build from.",
                    "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            // Copy rather than open-and-save-as, so the source level is never at risk of
            // being modified by this tool.
            AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(SourceScene, ScenePath))
            {
                Debug.LogError($"[Last Orders] Could not copy {SourceScene} to {ScenePath}.");
                return;
            }

            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GridBuildingSystem3D grid = Object.FindFirstObjectByType<GridBuildingSystem3D>();
            GameLifetimeScope scope = AddComposition(balance, grid);
            AddGridInitializer(grid, balance);
            AddDebugOverlay(scope);
            AddEnemySpawner(scope);

            int hudElements = AddHud(scope);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[Last Orders] Built {ScenePath} from {SourceScene}.\n" +
                      $"Composition root, grid initializer, debug overlay and a {hudElements}-element HUD added.\n" +
                      "Press Play. F1 toggles the debug overlay, F2 cycles grid layers.");

            EditorUtility.DisplayDialog("Last Orders",
                $"Scene built at:\n{ScenePath}\n\n" +
                "Derived from Main Level, so terrain, lighting, art and the existing UI are all intact.\n\n" +
                "Added: day-cycle composition root, HUD (day, clock arc, Heat bar with forecast " +
                "marker, Reputation, stores), dawn panel, Crisis Bar, and the debug overlay.\n\n" +
                "Press Play. F1 = overlay, F2 = grid layers.",
                "OK");

            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
        }

        /// <summary>
        /// Adds the Last Orders layer to the scene that is already open, in place.
        ///
        /// <see cref="Build"/> derives a fresh scene from Main Level, which is right when the
        /// generated scene is the one being played. It is wrong when Main Level itself is the
        /// scene being worked in: the copy leaves every edit made since the last build behind
        /// in a file nobody opens, and Main Level keeps coming up with no composition root -
        /// so every Last Orders component finds no scope on Start and switches itself off.
        ///
        /// Same systems, applied to the open scene, with nothing copied and nothing parked.
        /// That configuration is supported by design: GameLifetimeScope runs at execution
        /// order 1000 precisely so GameHandler.Start can initialise the build grid, the placed
        /// objects and navigation before the Last Orders layer reads all three.
        ///
        /// Marked dirty but deliberately not saved, so the change can be reviewed and undone
        /// before it reaches disk.
        /// </summary>
        [MenuItem("Last Orders/Add Systems To Current Scene", priority = 6)]
        public static void AddSystemsToCurrentScene()
        {
            GameBalanceConfig balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(BalancePath);
            if (balance == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "No GameBalance asset found.\n\nRun 'Last Orders / Generate Starting Content' first.",
                    "OK");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Last Orders", "No scene is open.", "OK");
                return;
            }

            // Include inactive on purpose. In a scene where the old bootstrap has been parked
            // the grid sits under a deactivated root, and the default search skips inactive
            // objects - which is how a scope ends up saved with buildGrid quietly null.
            GridBuildingSystem3D grid =
                Object.FindFirstObjectByType<GridBuildingSystem3D>(FindObjectsInactive.Include);

            if (grid == null)
                Debug.LogWarning("[Last Orders] No GridBuildingSystem3D in this scene, so the " +
                                 "perimeter and the Heat footprint will read as empty.");

            GameLifetimeScope scope = AddComposition(balance, grid);
            AddGridInitializer(grid, balance);
            AddDebugOverlay(scope);
            AddEnemySpawner(scope);

            int hudElements = AddHud(scope);

            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[Last Orders] Added the Last Orders layer to '{scene.name}' in place.\n" +
                      $"Composition root, grid initializer, debug overlay, enemy spawner and a " +
                      $"{hudElements}-element HUD.\n" +
                      "The scene is dirty and not yet saved - save it to keep this.");

            EditorUtility.DisplayDialog("Last Orders",
                $"Added to the open scene:\n{scene.path}\n\n" +
                "Composition root, grid initializer, debug overlay, enemy spawner and HUD.\n\n" +
                "The scene has NOT been saved. Look it over, then save to keep it.\n\n" +
                "Press Play. F1 = overlay, F2 = grid layers.",
                "OK");
        }

        // --- Systems ---------------------------------------------------------------

        static GameLifetimeScope AddComposition(GameBalanceConfig balance, GridBuildingSystem3D grid)
        {
            GameLifetimeScope existing = Object.FindFirstObjectByType<GameLifetimeScope>();
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject("LastOrders");
            var scope = root.AddComponent<GameLifetimeScope>();

            var pools = new GameObject("Pools");
            pools.transform.SetParent(root.transform, false);

            var so = new SerializedObject(scope);
            so.FindProperty("balance").objectReferenceValue = balance;
            so.FindProperty("buildGrid").objectReferenceValue = grid;
            so.FindProperty("poolRoot").objectReferenceValue = pools.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            return scope;
        }

        static void AddGridInitializer(GridBuildingSystem3D grid, GameBalanceConfig balance)
        {
            SceneGridInitializer existing = Object.FindFirstObjectByType<SceneGridInitializer>();
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var host = new GameObject("SceneGridInitializer");
            var initializer = host.AddComponent<SceneGridInitializer>();

            var so = new SerializedObject(initializer);
            so.FindProperty("buildGrid").objectReferenceValue = grid;
            so.FindProperty("plotTier").objectReferenceValue = FirstTier(balance);

            // Left null on purpose: in a scene with GameHandler the placed objects are loaded
            // from the save file at runtime rather than authored under a known root, so the
            // initializer sweeps the whole scene instead.
            so.FindProperty("restaurantRoot").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddDebugOverlay(GameLifetimeScope scope)
        {
            LastOrdersDebugOverlay existing = Object.FindFirstObjectByType<LastOrdersDebugOverlay>();
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var host = new GameObject("LastOrdersDebugOverlay");
            var overlay = host.AddComponent<LastOrdersDebugOverlay>();

            var so = new SerializedObject(overlay);
            so.FindProperty("scope").objectReferenceValue = scope;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddEnemySpawner(GameLifetimeScope scope)
        {
            Night.EnemySpawner existing = Object.FindFirstObjectByType<Night.EnemySpawner>();
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var host = new GameObject("EnemySpawner");
            var spawner = host.AddComponent<Night.EnemySpawner>();

            var so = new SerializedObject(spawner);
            so.FindProperty("scope").objectReferenceValue = scope;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- UI ---------------------------------------------------------------------

        static int AddHud(GameLifetimeScope scope)
        {
            Canvas canvas = FindMainCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("[Last Orders] No Canvas found in the scene. Creating one for the HUD.");
                canvas = CreateCanvas();
            }

            UI.LastOrdersHUD hud = HudBuilder.Build(canvas, new GameLifetimeScopeRef { Scope = scope });
            return hud.GetComponentsInChildren<Graphic>(true).Length;
        }

        /// <summary>
        /// Picks the Canvas the game's own UI lives on, so the HUD sits in the same overlay
        /// and inherits its scaler rather than fighting a second one.
        /// </summary>
        static Canvas FindMainCanvas()
        {
            Canvas[] all = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            if (all.Length == 0) return null;

            // The root overlay canvas with the most children is the game's main one; nested
            // canvases used for sorting have few.
            return all.Where(c => c.isRootCanvas)
                      .OrderByDescending(c => c.GetComponentsInChildren<Transform>(true).Length)
                      .FirstOrDefault() ?? all[0];
        }

        static Canvas CreateCanvas()
        {
            var go = new GameObject("Canvas");

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            // Portrait-locked per GDD 5, so the HUD is authored against a portrait reference.
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var events = new GameObject("EventSystem");
                events.AddComponent<UnityEngine.EventSystems.EventSystem>();
                events.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            return canvas;
        }

        static PlotTierDefinition FirstTier(GameBalanceConfig balance)
        {
            if (balance.plotTiers == null) return null;

            for (int i = 0; i < balance.plotTiers.Length; i++)
                if (balance.plotTiers[i] != null && balance.plotTiers[i].tier == 1)
                    return balance.plotTiers[i];

            return null;
        }
    }
}
