using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FoodStreet.Visitors.EditorTools;
using LastOrders.Crew;
using LastOrders.EditorTools.Screens;
using LastOrders.Jobs;
using LastOrders.Kitchen;
using LastOrders.Restaurant;
using LastOrders.UI.Hud;
using LastOrders.UI.Kit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.EditorTools.Crew
{
    /// <summary>
    /// The diner's starting line-up: two main characters and no hired hands.
    ///
    /// The male main character is left as he is. The female one - added to the level as a member of
    /// staff - becomes the second main character: her own prefab (<c>MainCharacterFemale</c>, made from
    /// <c>Staff 1</c>, which is left as it was) with the crew's duties - serving tables
    /// (<see cref="Waiter"/>), helping at a kitchen station (<see cref="StationAssist"/>), keeping the
    /// diner clean between orders (<see cref="ChoreRoutine"/>) and being picked up and dropped onto a
    /// station by the player (<see cref="CrewHandle"/>) - on the workers' animator, which gains the chore
    /// loops and a plate-carrying layer.
    ///
    /// The hired hands - the Chef, the Helper, the waiting staff - are switched off, not deleted, and
    /// noted in a <see cref="StartingCrew"/> so 'Restore' brings them back. The main character covers
    /// their jobs (repairs) while nobody of their role is on duty.
    ///
    /// Also builds the clips, gives the visitors their after-meal reactions, and puts the "help here"
    /// chip on the kitchen screen. The level is left unsaved.
    /// </summary>
    public static class CrewSetup
    {
        const string SourcePrefab = "Assets/Game Assets/Prefabs/Characters/Staff 1.prefab";
        const string CrewPrefab = "Assets/Game Assets/Prefabs/Characters/MainCharacterFemale.prefab";
        const string PaintPath = "Assets/Game Assets/Content/LastOrders/Decor/Decor_Toon.mat";
        const string RecordName = "StartingCrew";
        const string CrewName = "MainCharacterFemale";
        const string ChipName = "HelperChip";
        const string DefaultName = "Mia";

        [MenuItem("Last Orders/Crew/Set Up Main Characters & Crew (Current Scene)", priority = 140)]
        static void RunSetUp() => Debug.Log(SetUp());

        [MenuItem("Last Orders/Crew/Restore Previous Staff (Current Scene)", priority = 141)]
        static void RunRestore() => Debug.Log(Restore());

        public static string SetUp()
        {
            var report = new StringBuilder("[Last Orders] Main characters & crew:\n");

            report.Append("  ").Append(WorkerAnimations.Apply()).Append('\n');
            report.Append("  ").Append(VisitorReactionSetup.Apply()).Append('\n');
            report.Append("  ").Append(PaceLocomotion()).Append('\n');
            report.Append("  ").Append(BuildPrefab()).Append('\n');
            report.Append("  ").Append(PlaceCrew()).Append('\n');
            report.Append("  ").Append(KitchenChip()).Append('\n');

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            report.Append("  The level is left unsaved.");
            return report.ToString();
        }

        /// <summary>The visitors' and the old waiters' walks paced to real speed, as the workers' are.</summary>
        static string PaceLocomotion()
        {
            var paced = new List<string>();
            foreach (string path in new[]
                     {
                         "Assets/Game Assets/Prefabs/Characters/VisitorAnimator.controller",
                         "Assets/Game Assets/Content/LastOrders/Characters/WaiterAnimator.controller",
                     })
            {
                var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);
                if (WorkerAnimations.PaceMovement(controller)) paced.Add(controller.name);
            }

            return $"walk paced to speed (idle / walk / jog) on {string.Join(", ", paced)}";
        }

        // --- The prefab ------------------------------------------------------------------------------

        /// <summary>
        /// MainCharacterFemale: made from Staff 1 the first time, then kept and only topped up, so
        /// changes made to it by hand survive another run.
        /// </summary>
        static string BuildPrefab()
        {
            bool exists = File.Exists(CrewPrefab);
            if (!exists && !File.Exists(SourcePrefab)) return $"WARNING: neither {CrewPrefab} nor {SourcePrefab} exists.";

            GameObject root = PrefabUtility.LoadPrefabContents(exists ? CrewPrefab : SourcePrefab);
            try
            {
                root.name = CrewName;

                // She is not staff: the old staff brain goes.
                foreach (StaffBehaviour staff in root.GetComponents<StaffBehaviour>()) Object.DestroyImmediate(staff, true);

                Animator animator = root.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.avatar != null && a.avatar.isHuman);
                if (animator == null) return $"WARNING: no humanoid animator in {SourcePrefab}.";

                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(WorkerAnimations.ControllerPath);
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                Ensure<CharacterLookAt>(animator.gameObject);
                Ensure<CharacterReach>(animator.gameObject);

                var legs = Ensure<NavWalker>(root);
                UiKit.Set(legs, "animator", animator);

                var brain = Ensure<CrewBrain>(root);
                if (!exists) UiKit.Set(brain, "displayName", DefaultName);

                var props = Ensure<CrewProps>(root);
                var paint = AssetDatabase.LoadAssetAtPath<Material>(PaintPath);
                if (paint != null) UiKit.Set(props, "paint", paint);

                Ensure<Waiter>(root);
                Ensure<StationAssist>(root);
                Ensure<ChoreRoutine>(root);
                Ensure<CrewHandle>(root);

                // Something for a finger to land on, that bumps into nothing.
                var capsule = root.GetComponent<CapsuleCollider>();
                if (capsule == null) capsule = root.AddComponent<CapsuleCollider>();
                capsule.isTrigger = true;
                capsule.center = new Vector3(0f, 0.85f, 0f);
                capsule.height = 1.7f;
                capsule.radius = 0.32f;

                PrefabUtility.SaveAsPrefabAsset(root, CrewPrefab);
                return $"{(exists ? "topped up" : "made")} {CrewPrefab} (worker animator, crew duties, '{brain.DisplayName}')";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // --- The level -------------------------------------------------------------------------------

        static string PlaceCrew()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrewPrefab);
            if (prefab == null) return "WARNING: the crew prefab was not made.";

            StartingCrew record = Object.FindFirstObjectByType<StartingCrew>(FindObjectsInactive.Include);
            if (record == null)
            {
                record = new GameObject(RecordName).AddComponent<StartingCrew>();
                Undo.RegisterCreatedObjectUndo(record.gameObject, "Set Up Crew");
            }

            var switchedOff = new List<GameObject>(record.switchedOff.Where(g => g != null));
            var names = new List<string>();

            void SwitchOff(GameObject go)
            {
                if (go == null || !go.activeSelf) return;
                Undo.RecordObject(go, "Set Up Crew");
                go.SetActive(false);
                if (!switchedOff.Contains(go)) switchedOff.Add(go);
                names.Add(go.name);
            }

            // Where she stands: the female staff member's place, or the pass.
            GameObject staffOne = Object.FindObjectsByType<StaffBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                        .Select(s => s.gameObject)
                                        .FirstOrDefault(g => g.scene.IsValid() && Source(g) == SourcePrefab);
            Vector3 from = staffOne != null ? staffOne.transform.position : new Vector3(22.5f, 0f, 23.7f);
            Pose home = Home(from);

            GameObject crew = record.crew;
            if (crew == null)
            {
                crew = (GameObject)PrefabUtility.InstantiatePrefab(prefab, EditorSceneManager.GetActiveScene());
                Undo.RegisterCreatedObjectUndo(crew, "Set Up Crew");
                crew.name = CrewName;

                WorkerAgent main = Object.FindObjectsByType<WorkerAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                         .FirstOrDefault(w => w.Role == WorkerRole.MainCharacter);
                if (main != null && main.transform.parent != null) crew.transform.SetParent(main.transform.parent, true);
            }

            Undo.RecordObject(crew.transform, "Set Up Crew");
            crew.transform.SetPositionAndRotation(home.position, home.rotation);
            crew.SetActive(true);
            record.crew = crew;

            // No hired hands at the start: the workers other than the main character, and the waiting staff.
            SwitchOff(staffOne);
            foreach (WorkerAgent worker in Object.FindObjectsByType<WorkerAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (worker.Role != WorkerRole.MainCharacter) SwitchOff(worker.gameObject);
            foreach (Waiter waiter in Object.FindObjectsByType<Waiter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (waiter.gameObject != crew) SwitchOff(waiter.gameObject);
            foreach (StaffBehaviour staff in Object.FindObjectsByType<StaffBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                SwitchOff(staff.gameObject);

            Undo.RecordObject(record, "Set Up Crew");
            record.switchedOff = switchedOff.ToArray();
            EditorUtility.SetDirty(record);

            string off = names.Count > 0 ? string.Join(", ", names) : "nothing more";
            return $"'{crew.name}' at ({home.position.x:0.00}, {home.position.z:0.00}); switched off: {off}";
        }

        /// <summary>The waiting spot at the pass nearest <paramref name="near"/>, facing the dining room.</summary>
        static Pose Home(Vector3 near)
        {
            Transform best = null;
            float nearest = float.MaxValue;
            foreach (ServingPass pass in Object.FindObjectsByType<ServingPass>(FindObjectsSortMode.None))
            {
                if (pass.ServicePoints == null) continue;
                foreach (Transform point in pass.ServicePoints)
                {
                    if (point == null) continue;
                    float d = (point.position - near).sqrMagnitude;
                    if (d < nearest)
                    {
                        nearest = d;
                        best = point;
                    }
                }
            }

            Vector3 at = best != null ? best.position : near;
            at.y = near.y;
            return new Pose(at, Quaternion.LookRotation(Vector3.back));
        }

        // --- The kitchen screen ------------------------------------------------------------------------

        /// <summary>The "help here" chip, over the cooking slots that hang above the station.</summary>
        static string KitchenChip()
        {
            KitchenStationView view = Object.FindObjectsByType<KitchenStationView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                            .FirstOrDefault(v => v.gameObject.scene.IsValid());
            if (view == null) return "no kitchen screen in the level (run 'Build Diner Screens'); no helper chip";

            var rack = new SerializedObject(view).FindProperty("rack").objectReferenceValue as RectTransform;
            if (rack == null) return "the kitchen screen has no rack; no helper chip";

            Transform old = rack.Find(ChipName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            HudSkin skin = UiKit.Skin;
            UiKit.ButtonParts parts = UiKit.TextButton(rack, ChipName, HudSprites.ButtonCream, $"{DefaultName}: help here", new Vector2(560f, 76f),
                                                       Vector2.zero, new Vector2(0.5f, 1f), HudSprites.IconServe, 28f);
            RectTransform chip = (RectTransform)parts.button.transform;
            chip.pivot = new Vector2(0.5f, 0f);
            chip.anchoredPosition = new Vector2(0f, 18f);
            chip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var group = chip.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            RectTransform crossRect = UiKit.Node("Cross", chip, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), UiKit.Middle,
                                                 new Vector2(-34f, 2f), new Vector2(30f, 30f));
            Image cross = UiKit.Paint(crossRect, HudSprites.GlyphCross, Color.white);
            cross.enabled = false;

            var helper = chip.gameObject.AddComponent<StationHelperChip>();
            UiKit.Set(helper, "view", view);
            UiKit.Set(helper, "button", parts.button);
            UiKit.Set(helper, "face", parts.face);
            UiKit.Set(helper, "label", parts.label);
            UiKit.Set(helper, "cross", cross);
            UiKit.Set(helper, "motion", parts.motion);
            UiKit.Set(helper, "group", group);
            UiKit.Set(helper, "offerFace", skin[HudSprites.ButtonCream]);
            UiKit.Set(helper, "helpingFace", skin[HudSprites.ButtonTeal]);
            UiKit.Set(helper, "sparkle", skin[HudSprites.IconStar]);

            Undo.RegisterCreatedObjectUndo(chip.gameObject, "Set Up Crew");
            return $"helper chip over the kitchen's cooking slots ({view.name})";
        }

        // --- Restore ---------------------------------------------------------------------------------

        public static string Restore()
        {
            var report = new StringBuilder("[Last Orders] Previous staff restored: ");

            StartingCrew record = Object.FindFirstObjectByType<StartingCrew>(FindObjectsInactive.Include);
            if (record == null) return report.Append("nothing to restore (no StartingCrew in the level).").ToString();

            int back = 0;
            foreach (GameObject go in record.switchedOff)
            {
                if (go == null) continue;
                Undo.RecordObject(go, "Restore Staff");
                go.SetActive(true);
                back++;
            }

            if (record.crew != null) Undo.DestroyObjectImmediate(record.crew);
            Undo.DestroyObjectImmediate(record.gameObject);

            foreach (KitchenStationView view in Object.FindObjectsByType<KitchenStationView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var rack = new SerializedObject(view).FindProperty("rack").objectReferenceValue as RectTransform;
                Transform chip = rack != null ? rack.Find(ChipName) : null;
                if (chip != null) Undo.DestroyObjectImmediate(chip.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return report.Append($"{back} switched back on, the second main character and the helper chip taken out. " +
                                 "The animators keep their new clips. The level is left unsaved.").ToString();
        }

        // --- Helpers ---------------------------------------------------------------------------------

        static T Ensure<T>(GameObject on) where T : Component
        {
            T found = on.GetComponent<T>();
            return found != null ? found : on.AddComponent<T>();
        }

        static string Source(GameObject instance)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            return source != null ? AssetDatabase.GetAssetPath(source) : null;
        }
    }
}
