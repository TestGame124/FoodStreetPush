using System;
using System.Collections.Generic;
using LastOrders.Jobs;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The workers' own animations, made in code: the project has five humanoid clips - an idle that
    /// barely moves, a walk, two sits and a box carry - and a worker needs to fidget, gesture, talk
    /// and work.
    ///
    /// Each clip is a humanoid one, written as muscle curves, so it plays on any of the rigs the
    /// workers wear and Unity retargets it. They are built as functions of time on top of the
    /// stock idle's first frame - its stance, its feet, its fingers - and sampled at 30 frames a
    /// second: a breathing idle that shifts its weight from foot to foot, gestures that rise into a
    /// pose and move within it, and two work loops (hands busy at a counter; a tool swung overhead
    /// in time with <see cref="WorkerToolSwing"/>).
    ///
    /// The worker controller plays the breathing idle and the walk on the base layer, with an IK
    /// pass for <see cref="CharacterLookAt"/>, and the gestures and work on an upper-body layer
    /// above it (no head - the look-at owns the head). Its parameters are
    /// <see cref="WorkerGestures"/>'s. The visitors' controller is left as it is.
    /// </summary>
    internal static class WorkerAnimations
    {
        internal const string Folder = "Assets/Game Assets/Content/LastOrders/Characters";
        internal const string ClipFolder = Folder + "/Animations";
        internal const string ControllerPath = Folder + "/WorkerAnimator.controller";
        internal const string MaskPath = Folder + "/WorkerUpperBody.mask";

        const string Source = "Assets/Game Assets/Prefabs/Characters/Animations/";
        const string IdleSource = Source + "Yaku J Ignite@Idle.fbx";
        const string WalkSource = Source + "Yaku J Ignite@Walking.fbx";
        const string SitSource = Source + "Yaku J Ignite@Sitting.fbx";

        const float Fps = 30f;

        // --- Poses ---------------------------------------------------------------------------------

        /// <summary>Muscle and goal values by property name, as the clips name them.</summary>
        internal sealed class Pose : Dictionary<string, float>
        {
            public Pose() { }
            public Pose(Pose other) : base(other) { }

            /// <summary>Moves the given values <paramref name="weight"/> of the way to <paramref name="target"/>'s.</summary>
            public Pose Toward(Pose target, float weight)
            {
                if (weight <= 0f) return this;
                foreach (KeyValuePair<string, float> pair in target)
                    this[pair.Key] = TryGetValue(pair.Key, out float from) ? Mathf.LerpUnclamped(from, pair.Value, weight) : pair.Value;
                return this;
            }

            public Pose Add(string name, float amount)
            {
                this[name] = (TryGetValue(name, out float v) ? v : 0f) + amount;
                return this;
            }
        }

        static Pose baseline;

        /// <summary>The stock idle's first frame: stance, feet, fingers. Every clip starts from it.</summary>
        internal static Pose Base
        {
            get
            {
                if (baseline != null) return baseline;

                AnimationClip idle = Clip(IdleSource);
                baseline = new Pose();
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(idle))
                    baseline[binding.propertyName] = AnimationUtility.GetEditorCurve(idle, binding).Evaluate(0f);
                return baseline;
            }
        }

        static Pose P(params (string name, float value)[] values)
        {
            var pose = new Pose();
            foreach ((string name, float value) in values) pose[name] = value;
            return pose;
        }

        /// <summary>Both sides at once: "{0}" in the name becomes Left and Right.</summary>
        static Pose Both(params (string name, float value)[] values)
        {
            var pose = new Pose();
            foreach ((string name, float value) in values)
            {
                pose[string.Format(name, "Left")] = value;
                pose[string.Format(name, "Right")] = value;
            }

            return pose;
        }

        static Pose Join(params Pose[] parts)
        {
            var pose = new Pose();
            foreach (Pose part in parts)
                foreach (KeyValuePair<string, float> pair in part) pose[pair.Key] = pair.Value;
            return pose;
        }

        // Sign conventions measured on these rigs: Arm Down-Up + raises the arm out to the side;
        // Arm Front-Back - swings it forward; Arm Twist - turns a bent forearm in and back across
        // the body, + turns it up; Forearm Stretch 1 is straight, - bends the elbow;
        // Spine/Chest Front-Back + bends forward.

        static Pose Arm(string side, float downUp, float frontBack, float twist, float stretch, float forearmTwist = 0f, float hand = 0f) =>
            P(($"{side} Arm Down-Up", downUp), ($"{side} Arm Front-Back", frontBack), ($"{side} Arm Twist In-Out", twist),
              ($"{side} Forearm Stretch", stretch), ($"{side} Forearm Twist In-Out", forearmTwist), ($"{side} Hand Down-Up", hand));

        static Pose Arms(float downUp, float frontBack, float twist, float stretch, float forearmTwist = 0f, float hand = 0f) =>
            Join(Arm("Left", downUp, frontBack, twist, stretch, forearmTwist, hand), Arm("Right", downUp, frontBack, twist, stretch, forearmTwist, hand));

        /// <summary>Hands resting on the hips, elbows out.</summary>
        internal static readonly Pose Hips = Join(
            Arms(-0.12f, 0.2f, -0.85f, -0.3f, 0f, 0.35f),
            P(("Chest Front-Back", -0.04f)));

        /// <summary>Arms folded across the middle, one forearm over the other.</summary>
        internal static readonly Pose Folded = Join(
            Arm("Left", -0.45f, -0.12f, -0.55f, -0.4f, 0f, 0.1f),
            Arm("Right", -0.48f, -0.1f, -0.62f, -0.46f, 0f, 0.1f),
            Both(("{0} Shoulder Front-Back", -0.1f)),
            P(("Chest Front-Back", -0.03f)));

        /// <summary>Right hand up at the back of the head.</summary>
        internal static readonly Pose Scratch = Join(
            Arm("Right", 0.45f, -0.3f, 0.6f, -0.85f, 0f, 0.1f),
            P(("Right Shoulder Down-Up", 0.15f), ("Spine Left-Right", 0.05f), ("Chest Front-Back", 0.04f)));

        /// <summary>Both arms up over the head, back arched a little.</summary>
        internal static readonly Pose Reach = Join(
            Arms(0.95f, -0.1f, 0f, 0.55f, 0f, 0.3f),
            Both(("{0} Shoulder Down-Up", 0.5f)),
            P(("Spine Front-Back", -0.05f), ("Chest Front-Back", -0.08f), ("UpperChest Front-Back", -0.12f)));

        /// <summary>The back of the right wrist to the forehead.</summary>
        internal static readonly Pose Brow = Join(
            Arm("Right", 0.25f, -1f, 0.2f, -1f, 0f, -0.1f),
            P(("Chest Front-Back", -0.04f)));

        /// <summary>Right hand up beside the head, palm out.</summary>
        internal static readonly Pose WaveUp = Join(
            Arm("Right", 0.2f, 0f, 0.7f, -0.25f, 0f, 0.2f),
            P(("Right Shoulder Down-Up", 0.15f), ("Spine Left-Right", -0.04f)));

        /// <summary>Hands up in front of the chest, as when explaining.</summary>
        internal static readonly Pose Explain = Arms(-0.55f, 0.05f, 0.15f, -0.3f, 0.4f, 0.2f);

        /// <summary>Shoulders up, forearms out, palms turned up.</summary>
        internal static readonly Pose ShrugUp = Join(
            Arms(-0.42f, 0.1f, 0.25f, -0.1f, 0.9f, 0.3f),
            Both(("{0} Shoulder Down-Up", 0.75f)),
            P(("Spine Left-Right", 0.04f)));

        /// <summary>Right arm out straight, index finger pointing.</summary>
        internal static readonly Pose Pointing = Join(
            Arm("Right", -0.15f, -0.85f, 0f, 0.95f, 0.2f, 0f),
            P(("RightHand.Index.1 Stretched", 1f), ("RightHand.Index.2 Stretched", 1f), ("RightHand.Index.3 Stretched", 1f),
              ("RightHand.Middle.1 Stretched", -0.9f), ("RightHand.Middle.2 Stretched", -0.9f), ("RightHand.Middle.3 Stretched", -0.6f),
              ("RightHand.Ring.1 Stretched", -0.9f), ("RightHand.Ring.2 Stretched", -0.9f), ("RightHand.Ring.3 Stretched", -0.6f),
              ("RightHand.Little.1 Stretched", -0.9f), ("RightHand.Little.2 Stretched", -0.9f), ("RightHand.Little.3 Stretched", -0.6f)),
            P(("Chest Twist Left-Right", -0.06f)));

        /// <summary>Bent a little, hands to the belly.</summary>
        internal static readonly Pose LaughPose = Join(
            Arms(-0.5f, 0f, -0.45f, -0.2f),
            P(("Spine Front-Back", 0.12f), ("Chest Front-Back", 0.1f)));

        /// <summary>Left forearm up in front, palm up, looked at.</summary>
        internal static readonly Pose Palm = Join(
            Arm("Left", -0.55f, -0.1f, -0.15f, -0.35f, 0.8f, 0.1f),
            P(("Spine Front-Back", 0.06f), ("Chest Front-Back", 0.05f)));

        /// <summary>Both hands busy in front at waist height.</summary>
        internal static readonly Pose Counter = Join(
            Arms(-0.45f, 0.15f, 0f, -0.3f, 0.2f, 0.1f),
            P(("Spine Front-Back", 0.12f), ("Chest Front-Back", 0.06f)));

        /// <summary>The tool up over the head in both hands, leaning back into it.</summary>
        internal static readonly Pose SwingUp = Join(
            Arm("Right", 0.85f, -0.3f, 0f, 0.35f, 0f, 0.2f),
            Arm("Left", 0.7f, -0.45f, 0f, 0.25f, 0f, 0.2f),
            Both(("{0} Shoulder Down-Up", 0.45f)),
            P(("Spine Front-Back", -0.08f), ("Chest Front-Back", -0.1f), ("Chest Twist Left-Right", 0.06f)));

        /// <summary>The blow landed: both arms forward and down, bent over it.</summary>
        internal static readonly Pose SwingDown = Join(
            Arm("Right", -0.55f, -0.55f, 0f, 0.9f, 0f, -0.1f),
            Arm("Left", -0.6f, -0.5f, 0f, 0.8f, 0f, -0.1f),
            P(("Spine Front-Back", 0.3f), ("Chest Front-Back", 0.15f), ("Chest Twist Left-Right", -0.04f)));

        // --- Envelopes ------------------------------------------------------------------------------

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>0 to 1 over [<paramref name="start"/>, start + <paramref name="rise"/>], back to 0 over the last <paramref name="fall"/> seconds of <paramref name="length"/>.</summary>
        static float Envelope(float t, float length, float start, float rise, float fall) =>
            Ease((t - start) / rise) * (1f - Ease((t - (length - fall)) / fall));

        static float Wave(float t, float hz, float phase = 0f) => Mathf.Sin((t * hz + phase) * Mathf.PI * 2f);

        // --- The clips --------------------------------------------------------------------------------

        /// <summary>Breathing, a weight shift from one foot to the other and back, the head and arms never quite still. 12 s.</summary>
        static Pose AliveIdle(float t)
        {
            const float length = 12f;
            var pose = new Pose(Base);

            float breath = Wave(t, 3f / length);
            pose.Add("Chest Front-Back", 0.025f * breath).Add("UpperChest Front-Back", 0.03f * breath);
            pose.Add("Left Shoulder Down-Up", 0.04f * breath).Add("Right Shoulder Down-Up", 0.04f * breath);

            // Weight onto one hip, hold, across to the other: hips out, spine back over the feet.
            float shift = Mathf.Sin(t / length * Mathf.PI * 2f);
            shift = Mathf.Sign(shift) * Mathf.Pow(Mathf.Abs(shift), 0.6f);
            pose.Add("RootT.x", 0.022f * shift);
            pose.Add("Spine Left-Right", -0.06f * shift).Add("Chest Left-Right", -0.03f * shift);
            pose.Add("Left Upper Leg In-Out", 0.03f * shift).Add("Right Upper Leg In-Out", -0.03f * shift);

            // The head and arms drift, slowly and out of step.
            pose.Add("Head Turn Left-Right", 0.06f * Wave(t, 2f / length, 0.2f)).Add("Head Nod Down-Up", 0.03f * Wave(t, 3f / length, 0.6f));
            pose.Add("Left Arm Front-Back", 0.03f * Wave(t, 2f / length, 0.1f)).Add("Right Arm Front-Back", 0.03f * Wave(t, 2f / length, 0.55f));
            pose.Add("Left Forearm Stretch", -0.04f * (0.5f + 0.5f * Wave(t, 1f / length, 0.3f)));
            pose.Add("Right Forearm Stretch", -0.04f * (0.5f + 0.5f * Wave(t, 1f / length, 0.8f)));
            return pose;
        }

        static Pose Held(Pose target, float t, float length)
        {
            var pose = new Pose(Base).Toward(target, 1f);
            float breath = Wave(t, 2f / length);
            pose.Add("Chest Front-Back", 0.025f * breath).Add("UpperChest Front-Back", 0.025f * breath);
            pose.Add("Left Shoulder Down-Up", 0.035f * breath).Add("Right Shoulder Down-Up", 0.035f * breath);
            return pose;
        }

        static Pose ScratchHead(float t, float length)
        {
            var pose = new Pose(Base).Toward(Scratch, Envelope(t, length, 0f, 0.55f, 0.55f));
            float rub = Envelope(t, length, 0.55f, 0.15f, 0.7f);
            pose.Add("Right Forearm Twist In-Out", 0.18f * rub * Wave(t, 3.2f)).Add("Right Hand Down-Up", 0.15f * rub * Wave(t, 3.2f, 0.25f));
            return pose;
        }

        static Pose Stretch(float t, float length)
        {
            float up = Envelope(t, length, 0.1f, 0.9f, 1f);
            var pose = new Pose(Base).Toward(Reach, up);
            // A lean to one side and the other at the top of it.
            float lean = Envelope(t, length, 1f, 0.4f, 1.3f) * Wave(t, 0.6f);
            pose.Add("Spine Left-Right", 0.12f * lean).Add("Chest Left-Right", 0.1f * lean);
            return pose;
        }

        static Pose WipeBrow(float t, float length)
        {
            var pose = new Pose(Base).Toward(Brow, Envelope(t, length, 0f, 0.5f, 0.6f));
            // Across the forehead, side to side.
            float sweep = Ease((t - 0.55f) / 0.8f);
            pose.Add("Right Arm Down-Up", 0.3f * sweep * Envelope(t, length, 0.4f, 0.2f, 0.6f));
            return pose;
        }

        static Pose WaveHand(float t, float length)
        {
            var pose = new Pose(Base).Toward(WaveUp, Envelope(t, length, 0f, 0.4f, 0.5f));
            float swing = Envelope(t, length, 0.35f, 0.15f, 0.55f) * Wave(t, 2.6f);
            pose.Add("Right Arm Twist In-Out", 0.28f * swing).Add("Right Hand In-Out", 0.25f * swing);
            return pose;
        }

        static Pose Talk(float t, float length, bool lively)
        {
            var pose = new Pose(Base).Toward(Explain, Envelope(t, length, 0f, 0.35f, 0.45f) * (lively ? 1f : 0.8f));
            float on = Envelope(t, length, 0.2f, 0.2f, 0.5f);

            // Beats with the hands as they talk, the two out of step; a sweep out on the lively one.
            pose.Add("Right Forearm Stretch", 0.18f * on * Wave(t, 1.7f)).Add("Left Forearm Stretch", 0.14f * on * Wave(t, 1.3f, 0.35f));
            pose.Add("Right Forearm Twist In-Out", 0.2f * on * Wave(t, 0.9f, 0.1f)).Add("Left Forearm Twist In-Out", 0.15f * on * Wave(t, 0.8f, 0.6f));
            if (lively) pose.Add("Right Arm Down-Up", 0.22f * on * Mathf.Max(0f, Wave(t, 0.6f))).Add("Right Arm Front-Back", -0.12f * on * Wave(t, 0.6f));
            pose.Add("Chest Front-Back", 0.03f * on * Wave(t, 1.7f)).Add("Chest Twist Left-Right", 0.05f * on * Wave(t, 0.6f, 0.2f));
            return pose;
        }

        static Pose Shrug(float t, float length)
        {
            float up = Envelope(t, length, 0f, 0.35f, 0.7f);
            return new Pose(Base).Toward(ShrugUp, up);
        }

        static Pose Point(float t, float length)
        {
            var pose = new Pose(Base).Toward(Pointing, Envelope(t, length, 0f, 0.45f, 0.55f));
            float jab = Envelope(t, length, 0.45f, 0.1f, 0.9f) * Mathf.Max(0f, Wave(t, 1.5f));
            pose.Add("Right Arm Front-Back", -0.05f * jab);
            return pose;
        }

        static Pose Laugh(float t, float length)
        {
            float on = Envelope(t, length, 0f, 0.25f, 0.6f);
            var pose = new Pose(Base).Toward(LaughPose, on);
            // Shoulders shaking, the chest bouncing with it.
            float shake = on * Mathf.Abs(Wave(t, 3.5f));
            pose.Add("Chest Front-Back", 0.08f * shake).Add("UpperChest Front-Back", 0.06f * shake);
            pose.Add("Left Shoulder Down-Up", 0.25f * shake).Add("Right Shoulder Down-Up", 0.25f * shake);
            return pose;
        }

        static Pose CheckHand(float t, float length)
        {
            var pose = new Pose(Base).Toward(Palm, Envelope(t, length, 0f, 0.5f, 0.55f));
            // Turning the hand over to look at it.
            float turn = Envelope(t, length, 0.5f, 0.3f, 0.7f) * Wave(t, 0.7f);
            pose.Add("Left Forearm Twist In-Out", 0.25f * turn);
            return pose;
        }

        static Pose WorkHands(float t, float length)
        {
            var pose = new Pose(Base).Toward(Counter, 1f);
            float beat = Wave(t, 2f / length);
            // The right hand chops, the left steadies and turns things over.
            pose.Add("Right Forearm Stretch", 0.2f * Mathf.Max(0f, beat)).Add("Right Hand Down-Up", -0.25f * beat);
            pose.Add("Left Forearm Twist In-Out", 0.2f * Wave(t, 1f / length, 0.25f)).Add("Left Forearm Stretch", 0.06f * Wave(t, 1f / length));
            pose.Add("Spine Front-Back", 0.03f * beat).Add("Chest Twist Left-Right", 0.04f * Wave(t, 1f / length, 0.1f));
            return pose;
        }

        /// <summary>A slow lift and a fast drop, as <see cref="WorkerToolSwing"/> swings the tool: up for 70% of the swing, down in the last 30%.</summary>
        static Pose WorkSwing(float t, float length)
        {
            float u = t / length;
            float lift = u < 0.7f ? Ease(u / 0.7f) : 1f - Ease((u - 0.7f) / 0.3f);
            return new Pose(Base).Toward(SwingDown, 1f).Toward(SwingUp, lift);
        }

        // --- Building ---------------------------------------------------------------------------------

        internal static AnimationClip Clip(string path)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) return clip;
            throw new InvalidOperationException($"No animation clip in {path}.");
        }

        /// <summary>Samples <paramref name="pose"/> over <paramref name="length"/> seconds into a humanoid clip, made or rewritten in place.</summary>
        static AnimationClip Bake(string name, float length, bool loop, Func<float, Pose> pose)
        {
            ContentGenerator.EnsureFolder(ClipFolder);
            string path = $"{ClipFolder}/Worker_{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool created = clip == null;
            if (created) clip = new AnimationClip { name = "Worker_" + name };
            else clip.ClearCurves();

            int frames = Mathf.Max(2, Mathf.RoundToInt(length * Fps) + 1);
            var curves = new Dictionary<string, Keyframe[]>();
            foreach (string property in Base.Keys) curves[property] = new Keyframe[frames];

            for (int f = 0; f < frames; f++)
            {
                float t = f == frames - 1 ? length : f / Fps;
                Pose sample = pose(t);
                foreach (string property in Base.Keys)
                    curves[property][f] = new Keyframe(t, sample.TryGetValue(property, out float v) ? v : Base[property]);
            }

            foreach (KeyValuePair<string, Keyframe[]> pair in curves)
            {
                var curve = new AnimationCurve(pair.Value);
                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                }

                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), pair.Key), curve);
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (created) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
            return clip;
        }

        internal static Dictionary<Gesture, AnimationClip> BuildGestures()
        {
            var clips = new Dictionary<Gesture, AnimationClip>();
            float hold = WorkerGestures.HoldSeconds;

            clips[Gesture.HandsOnHips] = Bake("HandsOnHips", hold, true, t => Held(Hips, t, hold));
            clips[Gesture.ArmsCrossed] = Bake("ArmsCrossed", hold, true, t => Held(Folded, t, hold));
            Add(Gesture.ScratchHead, ScratchHead);
            Add(Gesture.Stretch, Stretch);
            Add(Gesture.WipeBrow, WipeBrow);
            Add(Gesture.Wave, WaveHand);
            Add(Gesture.TalkA, (t, l) => Talk(t, l, false));
            Add(Gesture.TalkB, (t, l) => Talk(t, l, true));
            Add(Gesture.Shrug, Shrug);
            Add(Gesture.Point, Point);
            Add(Gesture.Laugh, Laugh);
            Add(Gesture.CheckHand, CheckHand);
            return clips;

            void Add(Gesture gesture, Func<float, float, Pose> pose)
            {
                float length = WorkerGestures.Seconds(gesture);
                clips[gesture] = Bake(gesture.ToString(), length, false, t => pose(t, length));
            }
        }

        // --- The controller ---------------------------------------------------------------------------

        internal static string Apply()
        {
            baseline = null;
            AnimationClip idle = Bake("AliveIdle", 12f, true, AliveIdle);
            AnimationClip hands = Bake("WorkHands", WorkerGestures.WorkHandsSeconds, true, t => WorkHands(t, WorkerGestures.WorkHandsSeconds));
            AnimationClip swing = Bake("WorkSwing", WorkerGestures.SwingSeconds, true, t => WorkSwing(t, WorkerGestures.SwingSeconds));
            Dictionary<Gesture, AnimationClip> gestures = BuildGestures();

            AvatarMask mask = UpperBodyMask();
            AnimatorController controller = Controller();

            // Parameters: the visitors' three, so nothing that sets them warns, and the worker's own.
            controller.AddParameter("Move", AnimatorControllerParameterType.Float);
            controller.AddParameter("Sit", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Eat", AnimatorControllerParameterType.Bool);
            controller.AddParameter(WorkerGestures.WorkingParam, AnimatorControllerParameterType.Bool);
            controller.AddParameter(WorkerGestures.WorkStyleParam, AnimatorControllerParameterType.Int);
            controller.AddParameter(WorkerGestures.GestureParam, AnimatorControllerParameterType.Int);
            controller.AddParameter(WorkerGestures.ActParam, AnimatorControllerParameterType.Trigger);

            // Base: the breathing idle and the walk, blended by speed; sitting kept from the visitors'.
            controller.AddLayer("Base");
            AnimatorStateMachine body = controller.layers[0].stateMachine;
            AnimatorState movement = controller.CreateBlendTreeInController("Movement", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Move";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(Clip(WalkSource), 1f);
            movement.iKOnFeet = true;
            body.defaultState = movement;

            AnimatorState sitting = body.AddState("Sitting");
            sitting.motion = Clip(SitSource);
            AnimatorStateTransition sit = Transition(body.AddAnyStateTransition(sitting), 0.25f);
            sit.canTransitionToSelf = false;
            sit.AddCondition(AnimatorConditionMode.If, 0f, "Sit");
            Transition(sitting.AddTransition(movement), 0.25f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Sit");

            // Upper body: gestures and work over whatever the legs are doing.
            controller.AddLayer("Gestures");
            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = true;
            layers[1].defaultWeight = 1f;
            layers[1].avatarMask = mask;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            controller.layers = layers;

            AnimatorStateMachine upper = controller.layers[1].stateMachine;
            AnimatorState empty = upper.AddState("Empty");
            upper.defaultState = empty;

            AnimatorState workHands = Looping(upper, "Work Hands", hands);
            AnimatorState workSwing = Looping(upper, "Work Swing", swing);
            foreach ((AnimatorState state, int style) in new[] { (workHands, WorkerGestures.WorkHands), (workSwing, WorkerGestures.WorkSwing) })
            {
                AnimatorStateTransition into = Transition(upper.AddAnyStateTransition(state), 0.2f);
                into.canTransitionToSelf = false;
                into.AddCondition(AnimatorConditionMode.If, 0f, WorkerGestures.WorkingParam);
                into.AddCondition(AnimatorConditionMode.Equals, style, WorkerGestures.WorkStyleParam);
                Transition(state.AddTransition(empty), 0.25f).AddCondition(AnimatorConditionMode.IfNot, 0f, WorkerGestures.WorkingParam);
            }

            foreach (KeyValuePair<Gesture, AnimationClip> pair in gestures)
            {
                int id = (int)pair.Key;
                AnimatorState state = upper.AddState(pair.Key.ToString());
                state.motion = pair.Value;

                AnimatorStateTransition into = Transition(empty.AddTransition(state), 0.3f);
                into.AddCondition(AnimatorConditionMode.If, 0f, WorkerGestures.ActParam);
                into.AddCondition(AnimatorConditionMode.Equals, id, WorkerGestures.GestureParam);

                // Let go when asked; a one-shot also when it has played out.
                Transition(state.AddTransition(empty), 0.35f).AddCondition(AnimatorConditionMode.NotEqual, id, WorkerGestures.GestureParam);
                if (!WorkerGestures.IsHeld(pair.Key))
                {
                    AnimatorStateTransition done = Transition(state.AddTransition(empty), 0.3f);
                    done.hasExitTime = true;
                    done.exitTime = 0.9f;
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return $"Worker animator: breathing idle, {gestures.Count} gestures, 2 work loops; {ControllerPath}.";
        }

        static AnimatorState Looping(AnimatorStateMachine machine, string name, AnimationClip clip)
        {
            AnimatorState state = machine.AddState(name);
            state.motion = clip;
            return state;
        }

        static AnimatorStateTransition Transition(AnimatorStateTransition transition, float seconds)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = seconds;
            return transition;
        }

        /// <summary>The worker controller, emptied to be filled again: kept in place so whatever uses it keeps it.</summary>
        static AnimatorController Controller()
        {
            ContentGenerator.EnsureFolder(Folder);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) return AnimatorController.CreateAnimatorControllerAtPath(ControllerPath).Clear();

            return controller.Clear();
        }

        static AnimatorController Clear(this AnimatorController controller)
        {
            while (controller.layers.Length > 0) controller.RemoveLayer(0);
            while (controller.parameters.Length > 0) controller.RemoveParameter(0);
            return controller;
        }

        /// <summary>Spine, chest, arms and fingers: everything a gesture moves. Not the head (the look-at's) or the legs (the walk's).</summary>
        static AvatarMask UpperBodyMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, MaskPath);
            }

            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm ||
                             part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, upper);
            }

            EditorUtility.SetDirty(mask);
            AssetDatabase.SaveAssetIfDirty(mask);
            return mask;
        }

        [MenuItem("Last Orders/Workers/Build Worker Animations", priority = 130)]
        static void Run() => Debug.Log("[Last Orders] " + Apply());
    }
}
