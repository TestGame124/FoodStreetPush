using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Expansion;
using LastOrders.Field;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Readies the open scene for sending workers out to clear land: zombies standing on every
    /// expansion area, and the health bar and worker panel prefabs in the UI catalog.
    ///
    /// The workers need nothing from here - health, fighting, being carried and the tap
    /// collider are all set up on them when the game starts.
    ///
    /// Safe to re-run. An area that already has its zombies keeps its numbers; only the prefab
    /// and the scope are re-pointed.
    /// </summary>
    public static class FieldWorkSceneSetup
    {
        const string ZombiesName = "Zombies";

        /// <summary>How many zombies a freshly set up area gets.</summary>
        const int DefaultCount = 4;

        /// <summary>Kept clear between the patch and the end of the way out, so nothing is
        /// waiting in the gap in the wall.</summary>
        const float GapClearance = 3f;

        [MenuItem("Last Orders/Expansion/Set Up Area Clearing (Current Scene)", priority = 31)]
        public static void Run()
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();

            if (scope == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "This scene has no GameLifetimeScope, so nothing would drive the workers.\n\n" +
                    "Open the level that has the Last Orders systems in it.", "OK");
                return;
            }

            EditorUtility.DisplayDialog("Last Orders", Apply(scope, save: false), "OK");
        }

        /// <summary>The setup against the open scene's scope, with no dialogs - for automation,
        /// which may not be able to reference the scope's type directly.</summary>
        public static string ApplyToActiveScene(bool save)
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            return scope != null
                ? Apply(scope, save)
                : "[Last Orders] No GameLifetimeScope in the open scene; nothing was changed.";
        }

        public static string Apply(GameLifetimeScope scope, bool save)
        {
            Scene scene = scope.gameObject.scene;

            GameBalanceConfig balance = new SerializedObject(scope).FindProperty("balance")
                .objectReferenceValue as GameBalanceConfig;

            WorldUiPrefabGenerator.Ensure(balance);

            if (ZombieContentGenerator.LoadWanderer() == null) ZombieContentGenerator.Generate(rebuild: false);
            GameObject prefab = ZombieContentGenerator.LoadWanderer();

            int areas = 0;
            foreach (ExpansionArea area in Object.FindObjectsByType<ExpansionArea>(FindObjectsSortMode.None))
            {
                if (area.gameObject.scene != scene) continue;

                EnsureZombies(scope, area, prefab);
                areas++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);

            string summary = $"[Last Orders] Area clearing set up in '{scene.name}': zombies on {areas} " +
                             $"area{(areas == 1 ? "" : "s")}, health bar and worker panel in the UI catalog." +
                             (prefab == null ? " No zombie prefab could be made - run 'Generate Starting Content'." : "") +
                             (save ? "" : " The scene is not saved.");

            Debug.Log(summary);
            return summary;
        }

        /// <summary>Gives an area its zombies, or re-points the ones it has.</summary>
        public static AreaZombies EnsureZombies(GameLifetimeScope scope, ExpansionArea area, GameObject prefab)
        {
            Transform root = area.transform.Find(ZombiesName);
            if (root == null)
            {
                root = new GameObject(ZombiesName).transform;
                root.SetParent(area.transform, false);
                Undo.RegisterCreatedObjectUndo(root.gameObject, ZombiesName);
            }

            AreaZombies zombies = root.GetComponent<AreaZombies>();

            if (zombies != null)
            {
                var so = new SerializedObject(zombies);
                so.FindProperty("scope").objectReferenceValue = scope;
                so.FindProperty("area").objectReferenceValue = area;
                if (prefab != null) so.FindProperty("zombiePrefab").objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                return zombies;
            }

            zombies = Undo.AddComponent<AreaZombies>(root.gameObject);

            Rect patch = PatchFor(area);
            zombies.Configure(scope, area, prefab, new Vector3(patch.center.x, area.transform.position.y, patch.center.y),
                              patch.size, DefaultCount);

            EditorUtility.SetDirty(zombies);
            return zombies;
        }

        /// <summary>
        /// Where the zombies amble: over the area's debris, pulled back from the end of the way
        /// out so none of them stands in the gap, and never smaller than a patch worth walking.
        /// </summary>
        static Rect PatchFor(ExpansionArea area)
        {
            Debris[] debris = area.GetComponentsInChildren<Debris>(true);

            Rect patch;
            if (debris.Length == 0)
            {
                Vector3 c = area.transform.position;
                patch = new Rect(c.x - 5f, c.z - 4f, 10f, 8f);
            }
            else
            {
                Vector3 first = debris[0].transform.position;
                float xMin = first.x, xMax = first.x, zMin = first.z, zMax = first.z;

                foreach (Debris piece in debris)
                {
                    Vector3 p = piece.transform.position;
                    xMin = Mathf.Min(xMin, p.x);
                    xMax = Mathf.Max(xMax, p.x);
                    zMin = Mathf.Min(zMin, p.z);
                    zMax = Mathf.Max(zMax, p.z);
                }

                patch = Rect.MinMaxRect(xMin, zMin, xMax, zMax);
            }

            // Pull back whichever edge faces the way in.
            Transform routeEnd = RouteEnd(area);
            if (routeEnd != null)
            {
                Vector2 gap = new Vector2(routeEnd.position.x, routeEnd.position.z);

                if (gap.y <= patch.yMin + GapClearance && gap.x >= patch.xMin && gap.x <= patch.xMax)
                    patch.yMin = Mathf.Max(patch.yMin, gap.y + GapClearance);
                else if (gap.y >= patch.yMax - GapClearance && gap.x >= patch.xMin && gap.x <= patch.xMax)
                    patch.yMax = Mathf.Min(patch.yMax, gap.y - GapClearance);
                else if (gap.x <= patch.xMin + GapClearance)
                    patch.xMin = Mathf.Max(patch.xMin, gap.x + GapClearance);
                else if (gap.x >= patch.xMax - GapClearance)
                    patch.xMax = Mathf.Min(patch.xMax, gap.x - GapClearance);
            }

            // At least 6 by 4, grown about its centre.
            Vector2 centre = patch.center;
            Vector2 size = new Vector2(Mathf.Max(6f, patch.width), Mathf.Max(4f, patch.height));
            return new Rect(centre - size * 0.5f, size);
        }

        static Transform RouteEnd(ExpansionArea area)
        {
            SerializedProperty route = new SerializedObject(area).FindProperty("approachRoute");
            if (route == null || route.arraySize == 0) return null;

            return route.GetArrayElementAtIndex(route.arraySize - 1).objectReferenceValue as Transform;
        }
    }
}
