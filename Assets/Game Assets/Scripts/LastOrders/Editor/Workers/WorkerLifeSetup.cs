using LastOrders.Jobs;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Brings the workers to life in the open level: every <see cref="WorkerAgent"/>'s rigs get the
    /// worker controller (a breathing idle, gestures, work loops), a <see cref="CharacterLookAt"/> on
    /// each rig, and a <see cref="WorkerLife"/> on the worker to fill the time between jobs.
    ///
    /// Re-runnable. 'Restore' puts the visitors' controller back and takes the two components off,
    /// for a way back to how they were.
    /// </summary>
    internal static class WorkerLifeSetup
    {
        const string VisitorControllerPath = "Assets/Game Assets/Prefabs/Characters/VisitorAnimator.controller";

        [MenuItem("Last Orders/Workers/Make Workers Feel Alive (Current Scene)", priority = 131)]
        static void Run() => Debug.Log("[Last Orders] " + Apply());

        [MenuItem("Last Orders/Workers/Restore Workers' Stock Animator (Current Scene)", priority = 132)]
        static void RunRestore() => Debug.Log("[Last Orders] " + Restore());

        internal static string Apply()
        {
            string built = WorkerAnimations.Apply();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(WorkerAnimations.ControllerPath);

            int workers = 0, rigs = 0;
            foreach (WorkerAgent worker in Object.FindObjectsByType<WorkerAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (Animator animator in worker.GetComponentsInChildren<Animator>(true))
                {
                    Undo.RecordObject(animator, "Worker animator");
                    animator.runtimeAnimatorController = controller;
                    if (animator.GetComponent<CharacterLookAt>() == null) Undo.AddComponent<CharacterLookAt>(animator.gameObject);
                    rigs++;
                }

                if (worker.GetComponent<WorkerLife>() == null) Undo.AddComponent<WorkerLife>(worker.gameObject);
                workers++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return $"{built}\nWorkers: {workers} given idle life, {rigs} rigs on the worker controller with look-at. Scene not saved.";
        }

        internal static string Restore()
        {
            var stock = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(VisitorControllerPath);
            int workers = 0;
            foreach (WorkerAgent worker in Object.FindObjectsByType<WorkerAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (Animator animator in worker.GetComponentsInChildren<Animator>(true))
                {
                    Undo.RecordObject(animator, "Stock animator");
                    animator.runtimeAnimatorController = stock;
                    var look = animator.GetComponent<CharacterLookAt>();
                    if (look != null) Undo.DestroyObjectImmediate(look);
                }

                var life = worker.GetComponent<WorkerLife>();
                if (life != null) Undo.DestroyObjectImmediate(life);
                workers++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return $"Workers: {workers} back on the visitors' animator, idle life taken off.";
        }
    }
}
