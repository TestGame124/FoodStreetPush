using System.Text;
using FoodStreet.Visitors;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Medical;
using LastOrders.Night;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using K = LastOrders.EditorTools.PrimitiveKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Puts the starting threat and the facility that answers it into the open scene: the
    /// hospital tent beside the gate, the shuttle stop on the road, the diner as a shelter for
    /// the night, and the zombies wandering the ruined town.
    ///
    /// The positions are this level's - the compound of Main Level_Apocalypse, with its gate in
    /// the south wall at x = 20, the gate tower just inside it at x = 15.2, the restaurant at
    /// x 9.5-30.1, z 16.2-29.7 and its door on the south face, and the west and east walls at
    /// x = -9.5 and x = 46 since the starting-area layout widened them. The tent stands in the
    /// open ground between the gate tower and the south-west site, its way in facing into the
    /// compound where the camera can see it; the shuttle stops on the far lane of the road just
    /// east of the gate, off the line the customers' cars drive.
    ///
    /// Safe to re-run. Everything is found by name and put back where it belongs; nothing is
    /// duplicated, and a tent moved by hand is moved back - move it here instead.
    /// </summary>
    public static class StartingThreatSceneSetup
    {
        const string FacilitiesRoot = "Facilities";
        const string TentName = "HospitalTent";
        const string ShelterRoot = "NightShelter";
        const string TownRoot = "TownZombies";

        static readonly Vector3 TentPosition = new Vector3(-3f, 0f, 2.4f);

        /// <summary>Facing north, into the compound: local +Z is the tent's way in. Facing the
        /// gate put the gate tower between the camera and the cots.</summary>
        const float TentYaw = 0f;

        /// <summary>North of the gate tower's legs, so the walk from the tent to the gate goes
        /// round the tower rather than through it.</summary>
        static readonly Vector3 TowerBypass = new Vector3(15.6f, 0f, 3.6f);

        /// <summary>On the road's far lane, east of the gate, facing along the road - wherever the road runs.</summary>
        public static Vector3 ShuttleStop => new Vector3(26f, 0f, FoodStreet.Visitors.EditorTools.ZoneLayout.RoadCentreZ - 1.2f);

        const float ShuttleYaw = 90f;

        static readonly Vector3 DoorOutsideFallback = new Vector3(19.82f, 0f, 14.6f);
        static readonly Vector3 DoorInsideFallback = new Vector3(19.82f, 0f, 18.1f);

        /// <summary>
        /// The restaurant's walls, as a box, for a scene whose walls cannot be measured. Routes in
        /// are kept outside it until the door.
        /// </summary>
        static readonly Bounds Restaurant = new Bounds(new Vector3(19.8f, 1.5f, 22.95f), new Vector3(20.6f, 3f, 13.5f));

        /// <summary>Clear floor inside: between the door and the long counter, and behind its east end.</summary>
        static readonly Vector3[] ShelterSpots =
        {
            new Vector3(17.8f, 0f, 20.2f),
            new Vector3(19.8f, 0f, 20.5f),
            new Vector3(23.6f, 0f, 20.2f),
            new Vector3(24.4f, 0f, 23.6f),
        };

        /// <summary>
        /// Patches of the ruined town: the streets of the suburbs at the world's edge, clear of the
        /// houses and the road. None to the north: that is the trees, and the courtyard the workers
        /// clear by day. Owned by the world-edge layout, which lays those streets.
        /// </summary>
        static TownZombies.Area[] TownAreas => EnvironmentLayout.TownPatches;

        /// <summary>Where the lights and the noise come from. What the town is drawn toward at dusk.</summary>
        static readonly Vector3 Lure = new Vector3(20f, 0f, -4.8f);

        [MenuItem("Last Orders/Hospital & Zombies/Set Up (Current Scene)", priority = 60)]
        public static void Run()
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();

            if (scope == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "This scene has no GameLifetimeScope, so nothing would run the tent or the night.\n\n" +
                    "Open the level that has the Last Orders systems in it.", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                $"This edits '{SceneManager.GetActiveScene().name}' in place:\n\n" +
                "- Adds the hospital tent beside the gate\n" +
                "- Adds the shuttle stop on the road for recovered visitors\n" +
                "- Makes the diner a shelter the workers go into at night\n" +
                "- Adds zombies wandering the town by day\n" +
                "- Creates the zombie, tent and shuttle content if it does not exist\n\n" +
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

        public static string Apply(GameLifetimeScope scope, bool save)
        {
            Scene scene = scope.gameObject.scene;
            K.Reset();

            var report = new StringBuilder("[Last Orders] Hospital, shuttle, shelter and town zombies set up.\n");

            GameBalanceConfig balance = new SerializedObject(scope).FindProperty("balance")
                .objectReferenceValue as GameBalanceConfig;

            // Floating text and the recovery bars come from the world UI catalog.
            WorldUiPrefabGenerator.Ensure(balance);

            report.Append($"  {ZombieContentGenerator.Generate(rebuild: false)}\n");

            HospitalDefinition hospital = HospitalContentGenerator.EnsureDefinition(balance);
            GameObject tentPrefab = HospitalContentGenerator.EnsureTent(rebuild: false);
            VehicleDefinition shuttle = HospitalContentGenerator.EnsureShuttle();

            report.Append($"  Hospital: {hospital.cots} cots, {hospital.survivorRecoverySeconds:0}s for a guard, " +
                          $"{hospital.visitorRecoverySeconds:0}s for a visitor.\n");

            HospitalTent tent = PlaceTent(scene, scope, tentPrefab);
            report.Append($"  {tent.name} at {tent.transform.position}.\n");

            report.Append($"  {SetUpShuttle(shuttle)}\n");
            report.Append($"  {SetUpShelter(scene, scope)}\n");
            report.Append($"  {SetUpTown(scene, scope)}\n");

            if (balance != null) EditorUtility.SetDirty(balance);
            AssetDatabase.SaveAssets();

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);

            return report.ToString();
        }

        // --- The tent ------------------------------------------------------------------------

        static HospitalTent PlaceTent(Scene scene, GameLifetimeScope scope, GameObject prefab)
        {
            Transform root = FindOrCreateRoot(scene, FacilitiesRoot);

            HospitalTent tent = root.GetComponentInChildren<HospitalTent>(true);
            if (tent == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                instance.name = TentName;
                Undo.RegisterCreatedObjectUndo(instance, "Place hospital tent");
                tent = instance.GetComponent<HospitalTent>();
            }

            Undo.RecordObject(tent.transform, "Place hospital tent");
            tent.transform.SetPositionAndRotation(TentPosition, Quaternion.Euler(0f, TentYaw, 0f));

            var so = new SerializedObject(tent);
            so.FindProperty("scope").objectReferenceValue = scope;
            so.ApplyModifiedProperties();

            return tent;
        }

        // --- The shuttle -----------------------------------------------------------------------

        static string SetUpShuttle(VehicleDefinition vehicle)
        {
            VisitorManager manager = Object.FindFirstObjectByType<VisitorManager>();
            VisitorRoads roads = Object.FindFirstObjectByType<VisitorRoads>();

            if (manager == null || roads == null)
            {
                return "WARNING: no visitor system in the scene, so there is no shuttle and recovered " +
                       "visitors will walk home. Run 'Food Street / Visitors / Set Up Visitor System' first.";
            }

            VisitorShuttle shuttle = manager.GetComponent<VisitorShuttle>();
            if (shuttle == null) shuttle = Undo.AddComponent<VisitorShuttle>(manager.gameObject);

            Transform stop = Point(manager.transform, "ShuttleStop", ShuttleStop, ShuttleYaw);
            Transform bypass = Point(manager.transform, "ShuttleWalk_TowerBypass", TowerBypass, 0f);

            var so = new SerializedObject(shuttle);
            so.FindProperty("vehicle").objectReferenceValue = vehicle;
            so.FindProperty("roads").objectReferenceValue = roads;
            so.FindProperty("stop").objectReferenceValue = stop;

            SerializedProperty walk = so.FindProperty("walkToStop");
            walk.arraySize = 1;
            walk.GetArrayElementAtIndex(0).objectReferenceValue = bypass;
            so.ApplyModifiedProperties();

            return $"Shuttle stops on the road at {stop.position}.";
        }

        // --- The shelter -------------------------------------------------------------------------

        static string SetUpShelter(Scene scene, GameLifetimeScope scope)
        {
            Transform root = FindOrCreateRoot(scene, ShelterRoot);

            NightShelter shelter = root.GetComponent<NightShelter>();
            if (shelter == null) shelter = Undo.AddComponent<NightShelter>(root.gameObject);

            // The same door the customers use, when the road knows where it is.
            VisitorRoads roads = Object.FindFirstObjectByType<VisitorRoads>();
            Vector3 outside = roads != null && roads.IsComplete ? roads.DoorOutside : DoorOutsideFallback;
            Vector3 inside = roads != null && roads.IsComplete ? roads.DoorInside : DoorInsideFallback;

            Transform doorOutside = Point(root, "DoorOutside", outside, 0f);
            Transform doorInside = Point(root, "DoorInside", inside, 0f);

            var spots = new Transform[ShelterSpots.Length];
            for (int i = 0; i < spots.Length; i++)
                spots[i] = Point(root, $"Spot_{i}", ShelterSpots[i], 180f);

            var so = new SerializedObject(shelter);
            so.FindProperty("scope").objectReferenceValue = scope;
            so.FindProperty("doorOutside").objectReferenceValue = doorOutside;
            so.FindProperty("doorInside").objectReferenceValue = doorInside;
            so.FindProperty("footprint").boundsValue =
                StartingAreaLayoutSetup.TryMeasureRestaurant(out Bounds measured) ? measured : Restaurant;

            SerializedProperty spotList = so.FindProperty("spots");
            spotList.arraySize = spots.Length;
            for (int i = 0; i < spots.Length; i++) spotList.GetArrayElementAtIndex(i).objectReferenceValue = spots[i];

            so.ApplyModifiedProperties();

            return $"The diner shelters {spots.Length} at night; the door is at {doorOutside.position}.";
        }

        // --- The town ----------------------------------------------------------------------------

        static string SetUpTown(Scene scene, GameLifetimeScope scope)
        {
            GameObject prefab = ZombieContentGenerator.LoadWanderer();
            if (prefab == null) return "WARNING: no town zombie prefab, so the town is empty by day.";

            Transform root = FindOrCreateRoot(scene, TownRoot);

            TownZombies town = root.GetComponent<TownZombies>();
            if (town == null) town = Undo.AddComponent<TownZombies>(root.gameObject);

            Transform lure = Point(root, "Lure", Lure, 0f);

            var so = new SerializedObject(town);
            so.FindProperty("scope").objectReferenceValue = scope;
            so.FindProperty("wandererPrefab").objectReferenceValue = prefab;
            so.FindProperty("lure").objectReferenceValue = lure;

            SerializedProperty areas = so.FindProperty("areas");
            areas.arraySize = TownAreas.Length;

            int total = 0;
            for (int i = 0; i < TownAreas.Length; i++)
            {
                SerializedProperty area = areas.GetArrayElementAtIndex(i);
                area.FindPropertyRelative("name").stringValue = TownAreas[i].name;
                area.FindPropertyRelative("centre").vector3Value = TownAreas[i].centre;
                area.FindPropertyRelative("size").vector2Value = TownAreas[i].size;
                area.FindPropertyRelative("count").intValue = TownAreas[i].count;
                total += TownAreas[i].count;
            }

            so.ApplyModifiedProperties();

            return $"{total} zombies wander the town in {TownAreas.Length} patches by day.";
        }

        // --- Helpers -----------------------------------------------------------------------------

        /// <summary>A named child, created if missing, put where it belongs.</summary>
        static Transform Point(Transform parent, string name, Vector3 position, float yaw)
        {
            Transform point = parent.Find(name);

            if (point == null)
            {
                point = K.Group(name, parent);
                Undo.RegisterCreatedObjectUndo(point.gameObject, "Place point");
            }
            else
            {
                Undo.RecordObject(point, "Place point");
            }

            point.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return point;
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
