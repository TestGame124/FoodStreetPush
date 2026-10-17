using System.Collections.Generic;
using LastOrders.EditorTools;
using LastOrders.Jobs;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FoodStreet.Visitors.EditorTools
{
    /// <summary>
    /// Gives the visitors' animator the gesture contract (<see cref="WorkerGestures"/>), so a diner can
    /// react to their meal at the table: a chef's kiss, a thumbs up, a pat of the belly, the mouth wiped,
    /// a little applause, a stretch or a shrug (see <see cref="VisitorReactions"/>).
    ///
    /// The clips are the workers' own - one set of gestures for everybody - played on an arms-and-fingers
    /// layer so the seated legs and hips stay the chair's. The layer rests at weight 0, which leaves the
    /// seated IK in charge of the hands; <see cref="GestureDriver"/> brings it up while a gesture plays.
    ///
    /// Run 'Build Worker Animations' first: it makes the clips. Safe to run again.
    /// </summary>
    public static class VisitorReactionSetup
    {
        const string ControllerPath = "Assets/Game Assets/Prefabs/Characters/VisitorAnimator.controller";

        /// <summary>Everything a diner might do at the table after eating.</summary>
        static readonly Gesture[] Reactions =
        {
            Gesture.PatBelly, Gesture.ThumbsUp, Gesture.WipeMouth, Gesture.ChefKiss, Gesture.Clap,
            Gesture.Stretch, Gesture.Shrug, Gesture.Laugh, Gesture.Wave,
        };

        [MenuItem("Food Street/Visitors/Add After-Meal Reactions (Visitor Animator)", priority = 3)]
        public static void Run() => Debug.Log(Apply());

        public static string Apply()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) return $"[Visitors] WARNING: no animator at {ControllerPath}.";

            var clips = new List<(Gesture, AnimationClip)>();
            foreach (Gesture gesture in Reactions)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{WorkerAnimations.ClipFolder}/Worker_{gesture}.anim");
                if (clip == null) return $"[Visitors] WARNING: no clip for {gesture} - run 'Last Orders/Workers/Build Worker Animations' first.";
                clips.Add((gesture, clip));
            }

            // Again from scratch: the old layer and parameters out first.
            AnimatorControllerLayer[] existing = controller.layers;
            for (int i = existing.Length - 1; i > 0; i--)
                if (existing[i].name == WorkerGestures.LayerName) controller.RemoveLayer(i);

            Parameter(controller, WorkerGestures.GestureParam, AnimatorControllerParameterType.Int);
            Parameter(controller, WorkerGestures.ActParam, AnimatorControllerParameterType.Trigger);

            controller.AddLayer(WorkerGestures.LayerName);
            AnimatorControllerLayer[] layers = controller.layers;
            int index = layers.Length - 1;
            layers[index].avatarMask = WorkerAnimations.ArmsMask();
            layers[index].defaultWeight = 0f;
            layers[index].blendingMode = AnimatorLayerBlendingMode.Override;
            controller.layers = layers;

            AnimatorStateMachine machine = controller.layers[index].stateMachine;
            AnimatorState empty = machine.AddState("Empty");
            machine.defaultState = empty;

            foreach ((Gesture gesture, AnimationClip clip) in clips)
            {
                int id = (int)gesture;
                AnimatorState state = machine.AddState(gesture.ToString());
                state.motion = clip;

                AnimatorStateTransition into = Timed(empty.AddTransition(state), 0.3f);
                into.AddCondition(AnimatorConditionMode.If, 0f, WorkerGestures.ActParam);
                into.AddCondition(AnimatorConditionMode.Equals, id, WorkerGestures.GestureParam);

                Timed(state.AddTransition(empty), 0.35f).AddCondition(AnimatorConditionMode.NotEqual, id, WorkerGestures.GestureParam);
                AnimatorStateTransition done = Timed(state.AddTransition(empty), 0.3f);
                done.hasExitTime = true;
                done.exitTime = 0.9f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return $"[Visitors] After-meal reactions: {clips.Count} gestures on an arms-only '{WorkerGestures.LayerName}' layer of {ControllerPath}.";
        }

        static void Parameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter p in controller.parameters)
                if (p.name == name) return;
            controller.AddParameter(name, type);
        }

        static AnimatorStateTransition Timed(AnimatorStateTransition transition, float seconds)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = seconds;
            return transition;
        }
    }
}
