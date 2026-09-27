using System.Text;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Housing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Puts the shelters into the open scene: the one the restaurant starts with, and the empty
    /// sites where more can be bought.
    ///
    /// The sites are this level's - the compound of Main Level_Apocalypse. They stand in a row in
    /// the open yard west of the restaurant, doors facing it, clear of the walk from the gate to
    /// the diner door, the car park, the hospital tent and the kitchen garden. The built one is
    /// the north end of the row, in the default camera's view, so the first thing the player
    /// taps to hire someone is on screen.
    ///
    /// Safe to re-run. Shelters already placed are found by id, moved back to their site and
    /// rewired; nothing is duplicated. A site moved by hand is put back, so move the site here
    /// rather than in the scene.
    /// </summary>
    public static class ShelterSceneSetup
    {
        const string RootName = "Shelters";

        readonly struct Site
        {
            public readonly string Id;
            public readonly Vector3 Position;
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

        /// <summary>
        /// Facing east, into the yard, with their backs 3.5 m in from the compound's west wall
        /// (x = -9.5 since the starting-area layout widened it). 4.4 m apart, which leaves a
        /// walkable gap between the roofs.
        /// </summary>
        static readonly Site[] Sites =
        {
            new Site("shelter_1", new Vector3(-6f, 0f, 20.2f), 90f, startsBuilt: true),
            new Site("shelter_2", new Vector3(-6f, 0f, 15.8f), 90f, startsBuilt: false),
            new Site("shelter_3", new Vector3(-6f, 0f, 11.4f), 90f, startsBuilt: false),
        };

        [MenuItem("Last Orders/Shelters/Set Up Shelters (Current Scene)", priority = 70)]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();

            if (scope == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "This scene has no GameLifetimeScope, so nothing would run the shelters.\n\n" +
                    "Open the level that has the Last Orders systems in it.", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                $"This edits '{scene.name}' in place:\n\n" +
                "- Adds the shelter the restaurant starts with, west of the diner\n" +
                $"- Adds {Sites.Length - 1} empty sites where more shelters can be bought\n" +
                "- Creates the shelter content, prefab and popup if they do not exist\n\n" +
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

            GameBalanceConfig balance = new SerializedObject(scope).FindProperty("balance")
                .objectReferenceValue as GameBalanceConfig;

            // The popup and the build button come from the world UI catalog.
            WorldUiPrefabGenerator.Ensure(balance);

            ShelterDefinition shelter = ShelterContentGenerator.EnsureDefinition(balance);
            GameObject prefab = ShelterContentGenerator.EnsurePrefab(shelter, rebuild: false);

            Transform root = FindOrCreateRoot(scene, RootName);
            var report = new StringBuilder("[Last Orders] Shelters set up.\n");

            foreach (Site site in Sites)
            {
                ShelterHut hut = FindHut(root, site.Id) ?? Place(prefab, root, site.Id);

                Undo.RecordObject(hut.transform, "Place shelter");
                hut.transform.SetPositionAndRotation(site.Position, Quaternion.Euler(0f, site.Yaw, 0f));

                // Through SerializedObject so the values are recorded as overrides on the prefab
                // instance, rather than set on the object and lost the next time it reloads.
                var so = new SerializedObject(hut);
                so.FindProperty("scope").objectReferenceValue = scope;
                so.FindProperty("definition").objectReferenceValue = shelter;
                so.FindProperty("shelterId").stringValue = site.Id;
                so.FindProperty("startsBuilt").boolValue = site.StartsBuilt;
                so.ApplyModifiedProperties();

                // The hut sorts this out itself at runtime; set here as well so an empty site
                // looks empty in the editor rather than like a hut standing on its own pad.
                ShowInEditor(so.FindProperty("builtVisuals").objectReferenceValue as GameObject, site.StartsBuilt);
                ShowInEditor(so.FindProperty("siteVisuals").objectReferenceValue as GameObject, !site.StartsBuilt);

                report.Append($"  {site.Id}: {(site.StartsBuilt ? "built" : "empty site")} at {site.Position}\n");
            }

            int hires = shelter.HireCount;
            report.Append($"  {shelter.beds} beds each; {hires} role{(hires == 1 ? "" : "s")} to hire from " +
                          $"{ShelterContentGenerator.DefinitionPath}.\n");

            if (hires == 0)
                report.Append("  WARNING: the shelter lists nobody to hire. Add hires to its definition.\n");

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);

            return report.ToString();
        }

        static ShelterHut FindHut(Transform root, string id)
        {
            foreach (ShelterHut hut in root.GetComponentsInChildren<ShelterHut>(true))
                if (hut.ShelterId == id) return hut;

            return null;
        }

        static ShelterHut Place(GameObject prefab, Transform root, string id)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            instance.name = $"Shelter_{id}";
            Undo.RegisterCreatedObjectUndo(instance, "Place shelter");
            return instance.GetComponent<ShelterHut>();
        }

        static void ShowInEditor(GameObject part, bool active)
        {
            if (part == null || part.activeSelf == active) return;

            Undo.RecordObject(part, "Place shelter");
            part.SetActive(active);
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
