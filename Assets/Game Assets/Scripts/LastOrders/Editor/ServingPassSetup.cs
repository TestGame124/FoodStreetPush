using System.Collections.Generic;
using System.Linq;
using System.Text;
using LastOrders.Kitchen;
using LastOrders.Restaurant;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Puts the serving pass into the open level and the waiters on the floor.
    ///
    /// The pass is two pfServingPass modules on the kitchen side of the line where the kitchen tiles
    /// meet the dining room's boards - their fronts on that line, so the cooks work behind them on the
    /// tiles and the waiters wait in front of them on the boards - centred on the kitchen line with a
    /// 3 m opening between them, in front of the middle station and in line with the door, and the
    /// ends left open. Nothing new stands in the dining room, and the way into the kitchen stays clear.
    ///
    /// The kitchen's depth is the floor's own (<see cref="RestaurantFloor.KitchenDepth"/>), and the
    /// floor is laid again first, as play lays it, so what the editor shows is where the tiles end.
    ///
    /// The waiters are the level's two staff, switched off since the order board went: their old
    /// brain (<c>StaffBehaviour</c>) is switched off rather than removed, a <see cref="Waiter"/> put
    /// beside it, and each stands at the inner end of a module, facing the tables.
    ///
    /// Idempotent: run again, it puts the same things back in place. Undoable, and leaves the scene
    /// unsaved.
    /// </summary>
    public static class ServingPassSetup
    {
        const string RootName = "ServingPass";
        const string StaffControllerPath = "Assets/Game Assets/Prefabs/Characters/StaffAnimator.controller";

        /// <summary>The opening between the two modules, in front of the middle station.</summary>
        const float Opening = 3f;

        /// <summary>How far the counter's front stands over the tiles' edge, onto the threshold strip.</summary>
        const float FrontOverEdge = 0.03f;

        [MenuItem("Last Orders/Kitchen/Set Up Serving Pass & Waiters (Current Scene)", priority = 107)]
        public static void Run() => Debug.Log(Apply());

        [MenuItem("Last Orders/Kitchen/Remove Serving Pass & Waiters (Current Scene)", priority = 108)]
        public static void RunRemove() => Debug.Log(Remove());

        public static string Apply()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ServingPassModel.PrefabPath);
            var report = new StringBuilder("[Last Orders] Serving pass: ");
            if (prefab == null)
            {
                report.Append(ServingPassModel.Apply()).Append(" ");
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ServingPassModel.PrefabPath);
            }

            var floor = Object.FindFirstObjectByType<RestaurantFloor>();
            Renderer floorView = floor != null ? floor.GetComponent<Renderer>() : null;
            if (floorView == null) return report.Append("no RestaurantFloor in this scene - nothing done.").ToString();

            // Laid as play lays it, so the tiles' edge measured here is the one the player sees.
            var shell = Object.FindFirstObjectByType<RestaurantShell>();
            if (shell != null)
            {
                var floorMesh = floor.GetComponent<MeshFilter>();
                if (floorMesh != null) Undo.RecordObject(floorMesh, "Set Up Serving Pass");
                shell.FitFloor();
            }

            Bounds inside = floorView.bounds;
            float depth = floor.KitchenDepth > 0.01f ? floor.KitchenDepth : RestaurantSceneSetup.KitchenDepth;
            float tiles = inside.max.z - depth;
            float lineMiddle = KitchenLineMiddle(inside);
            float z = tiles - FrontOverEdge - ServingPassModel.TopFront;
            float halfStep = (Opening + ServingPassModel.TopHalf * 2f) * 0.5f;

            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Set Up Serving Pass");
            }

            Undo.RecordObject(root.transform, "Set Up Serving Pass");
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var board = root.GetComponent<ServingPassBoard>();
            if (board == null) board = Undo.AddComponent<ServingPassBoard>(root);
            var dishes = AssetDatabase.LoadAssetAtPath<PassDishSet>(ServingPassModel.DishSetPath);
            var boardSo = new SerializedObject(board);
            boardSo.FindProperty("dishes").objectReferenceValue = dishes;
            boardSo.ApplyModifiedProperties();

            var modules = new List<ServingPass>();
            foreach ((string name, float x) in new[] { ("pfServingPass (Left)", lineMiddle - halfStep), ("pfServingPass (Right)", lineMiddle + halfStep) })
            {
                Transform existing = root.transform.Find(name);
                GameObject module = existing != null ? existing.gameObject : null;
                if (module == null)
                {
                    module = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                    module.name = name;
                    Undo.RegisterCreatedObjectUndo(module, "Set Up Serving Pass");
                }

                Undo.RecordObject(module.transform, "Set Up Serving Pass");
                module.transform.SetPositionAndRotation(new Vector3(x, 0f, z), Quaternion.identity);
                modules.Add(module.GetComponent<ServingPass>());
            }

            report.Append($"2 modules at x {lineMiddle - halfStep:0.00} / {lineMiddle + halfStep:0.00}, z {z:0.00} " +
                          $"(kitchen {depth:0.#} m deep, tiles from z {tiles:0.00}, opening {Opening} m); ");

            report.Append(PutWaitersOn(modules));

            EditorSceneManager.MarkSceneDirty(root.scene);
            return report.Append(". Scene left unsaved.").ToString();
        }

        /// <summary>The middle of the stations along the back wall, or of the room when there are none.</summary>
        static float KitchenLineMiddle(Bounds inside)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (CookeryStation station in Object.FindObjectsByType<CookeryStation>(FindObjectsSortMode.None))
            {
                Vector3 p = station.transform.position;
                if (p.x < inside.min.x || p.x > inside.max.x || p.z < inside.center.z || p.z > inside.max.z) continue;

                // Station roots are the near-left corner of a 3 m footprint.
                min = Mathf.Min(min, p.x);
                max = Mathf.Max(max, p.x + 3f);
            }

            return min <= max ? (min + max) * 0.5f : inside.center.x;
        }

        static string PutWaitersOn(List<ServingPass> modules)
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ServingPassModel.WaiterControllerPath);
            StaffBehaviour[] staff = Object.FindObjectsByType<StaffBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                           .Where(s => s.gameObject.scene.IsValid())
                                           .OrderBy(s => s.name)
                                           .ToArray();
            if (staff.Length == 0) return "no staff in the scene to wait tables";

            // The inner end of each module, beside the opening: the left module's right stand, the right one's left.
            var homes = new List<Transform>();
            if (modules.Count > 0 && modules[0] != null) homes.Add(modules[0].ServicePoints[modules[0].ServicePoints.Count - 1]);
            if (modules.Count > 1 && modules[1] != null) homes.Add(modules[1].ServicePoints[0]);

            var names = new List<string>();
            for (int i = 0; i < staff.Length && i < homes.Count; i++)
            {
                StaffBehaviour member = staff[i];
                GameObject go = member.gameObject;

                Undo.RecordObject(member, "Set Up Waiters");
                member.enabled = false;

                if (go.GetComponent<Waiter>() == null) Undo.AddComponent<Waiter>(go);

                var animator = go.GetComponentInChildren<Animator>(true);
                if (animator != null && controller != null)
                {
                    Undo.RecordObject(animator, "Set Up Waiters");
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                }

                Undo.RecordObject(go.transform, "Set Up Waiters");
                go.transform.SetPositionAndRotation(homes[i].position, Quaternion.LookRotation(Vector3.back));

                Undo.RecordObject(go, "Set Up Waiters");
                go.SetActive(true);
                names.Add($"{go.name} at ({homes[i].position.x:0.00}, {homes[i].position.z:0.00})");
            }

            return $"waiters {string.Join(", ", names)}";
        }

        /// <summary>Takes the pass out and sends the staff back off duty, as they were.</summary>
        public static string Remove()
        {
            var report = new StringBuilder("[Last Orders] Serving pass removed: ");

            GameObject root = GameObject.Find(RootName);
            if (root != null)
            {
                Undo.DestroyObjectImmediate(root);
                report.Append("pass gone; ");
            }

            var staffController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(StaffControllerPath);
            int restored = 0;
            foreach (Waiter waiter in Object.FindObjectsByType<Waiter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                GameObject go = waiter.gameObject;
                var staff = go.GetComponent<StaffBehaviour>();
                if (staff != null)
                {
                    Undo.RecordObject(staff, "Remove Waiters");
                    staff.enabled = true;
                }

                var animator = go.GetComponentInChildren<Animator>(true);
                if (animator != null && staffController != null)
                {
                    Undo.RecordObject(animator, "Remove Waiters");
                    animator.runtimeAnimatorController = staffController;
                }

                Undo.DestroyObjectImmediate(waiter);
                Undo.RecordObject(go, "Remove Waiters");
                go.SetActive(false);
                restored++;
                EditorSceneManager.MarkSceneDirty(go.scene);
            }

            return report.Append($"{restored} waiters back off duty. Scene left unsaved.").ToString();
        }
    }
}
