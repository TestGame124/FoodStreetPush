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

        /// <summary>The stock walk's curves, to be read at any moment of its cycle. Shared with the zombies' builder.</summary>
        internal sealed class WalkCycle
        {
            readonly List<(string name, AnimationCurve curve)> curves = new List<(string, AnimationCurve)>();
            readonly Dictionary<string, float> means = new Dictionary<string, float>();
            public readonly float Length;

            public WalkCycle(AnimationClip clip)
            {
                Length = clip.length;
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                    curves.Add((binding.propertyName, AnimationUtility.GetEditorCurve(clip, binding)));
            }

            /// <summary>The walk at <paramref name="cycle"/> (0 to 1) of its cycle.</summary>
            public Pose At(float cycle)
            {
                var pose = new Pose(Base);
                float t = Mathf.Repeat(cycle, 1f) * Length;
                foreach ((string name, AnimationCurve curve) in curves) pose[name] = curve.Evaluate(t);
                return pose;
            }

            /// <summary>The middle of <paramref name="name"/>'s swing over the cycle.</summary>
            public float Mean(string name)
            {
                if (means.TryGetValue(name, out float mean)) return mean;

                AnimationCurve found = null;
                foreach ((string n, AnimationCurve curve) in curves)
                    if (n == name) found = curve;

                mean = 0f;
                if (found != null)
                {
                    const int samples = 60;
                    for (int i = 0; i < samples; i++) mean += found.Evaluate(Length * i / samples);
                    mean /= samples;
                }

                return means[name] = mean;
            }

            /// <summary>Scales <paramref name="name"/>'s swing in <paramref name="pose"/> about its middle by <paramref name="by"/>.</summary>
            public void Swing(Pose pose, string name, float by)
            {
                if (!pose.TryGetValue(name, out float v)) return;
                float middle = Mean(name);
                pose[name] = middle + (v - middle) * by;
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
            // Both fists closed round the handle.
            return new Pose(Base).Toward(SwingDown, 1f).Toward(SwingUp, lift).Toward(Join(Fist("Right", 0.9f), Fist("Left", 0.9f)), 1f);
        }

        // --- Chores ------------------------------------------------------------------------------------
        //
        // The crew's chore loops. The hands are put exactly on the work by CharacterReach (IK), so these
        // give the body its lean and rhythm and get the arms near where the IK takes them.
        //
        // Measured on these rigs (see the zombie builder): Spine/Chest Front-Back NEGATIVE bends forward,
        // and the body's orientation is held, so a bend tips the pelvis back unless the body is turned
        // forward with it - which Bent does.

        static Quaternion BodyTurn(Pose pose)
        {
            float Get(string n) => pose.TryGetValue(n, out float v) ? v : 0f;
            var q = new Quaternion(Get("RootQ.x"), Get("RootQ.y"), Get("RootQ.z"), Get("RootQ.w"));
            float length = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return length > 1e-6f ? new Quaternion(q.x / length, q.y / length, q.z / length, q.w / length) : Quaternion.identity;
        }

        /// <summary>The chest bent forward by about <paramref name="degrees"/> over a pelvis that stays put.</summary>
        static Pose Bent(Pose pose, float degrees)
        {
            float s = degrees / 28f;
            pose.Add("Spine Front-Back", -0.2f * s).Add("Chest Front-Back", -0.35f * s).Add("UpperChest Front-Back", -0.3f * s);

            Quaternion q = Quaternion.Euler(13f * s, 0f, 0f) * BodyTurn(pose);
            if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            pose["RootQ.x"] = q.x;
            pose["RootQ.y"] = q.y;
            pose["RootQ.z"] = q.z;
            pose["RootQ.w"] = q.w;
            return pose;
        }

        static readonly string[] FingerNames = { "Index", "Middle", "Ring", "Little" };

        /// <summary>The four fingers curled into the palm by <paramref name="amount"/> (0 open, 1 a fist); the thumb along them.</summary>
        static Pose Fist(string side, float amount, float thumbUp = 0f)
        {
            var pose = new Pose();
            foreach (string finger in FingerNames)
            {
                pose[$"{side}Hand.{finger}.1 Stretched"] = Mathf.Lerp(0.3f, -0.9f, amount);
                pose[$"{side}Hand.{finger}.2 Stretched"] = Mathf.Lerp(0.3f, -1f, amount);
                pose[$"{side}Hand.{finger}.3 Stretched"] = Mathf.Lerp(0.3f, -0.8f, amount);
            }

            pose[$"{side}Hand.Thumb.1 Stretched"] = Mathf.Lerp(-0.4f * amount, 0.8f, thumbUp);
            pose[$"{side}Hand.Thumb.2 Stretched"] = Mathf.Lerp(-0.6f * amount, 0.9f, thumbUp);
            pose[$"{side}Hand.Thumb.3 Stretched"] = Mathf.Lerp(-0.6f * amount, 0.9f, thumbUp);
            pose[$"{side}Hand.Thumb.Spread"] = Mathf.Lerp(0f, 0.4f, thumbUp);
            return pose;
        }

        /// <summary>Fingers spread wide open.</summary>
        static Pose Spread(string side)
        {
            var pose = new Pose();
            foreach (string finger in FingerNames)
            {
                pose[$"{side}Hand.{finger}.1 Stretched"] = 0.6f;
                pose[$"{side}Hand.{finger}.2 Stretched"] = 0.7f;
                pose[$"{side}Hand.{finger}.3 Stretched"] = 0.6f;
                pose[$"{side}Hand.{finger}.Spread"] = 0.6f;
            }

            return pose;
        }

        /// <summary>Leaning over a surface, the right hand going round with the cloth, the left braced beside it.</summary>
        static Pose WorkWipe(float t, float length)
        {
            var pose = Bent(new Pose(Base), 22f);
            pose.Toward(Join(Arm("Right", -0.42f, -0.56f, 0f, 0.55f, 0f, -0.1f), Arm("Left", -0.46f, -0.46f, 0f, 0.5f, 0f, 0.2f)), 1f);

            float a = t / length * Mathf.PI * 2f;
            pose.Add("Right Arm Front-Back", -0.08f * Mathf.Sin(a)).Add("Right Arm Down-Up", 0.06f * Mathf.Cos(a));
            pose.Add("Chest Twist Left-Right", 0.05f * Mathf.Sin(a)).Add("Spine Left-Right", 0.025f * Mathf.Cos(a));
            pose.Add("Left Shoulder Down-Up", 0.05f).Add("Right Shoulder Down-Up", 0.04f * Mathf.Sin(a));
            return pose;
        }

        /// <summary>A broom in both hands, swept side to side: the chest swinging with it, a little stooped.</summary>
        static Pose WorkSweep(float t, float length)
        {
            var pose = Bent(new Pose(Base), 12f);
            pose.Toward(Join(Arm("Right", -0.5f, -0.4f, 0f, 0.5f), Arm("Left", -0.3f, -0.1f, -0.2f, -0.1f)), 1f);

            float s = Mathf.Sin(t / length * Mathf.PI * 2f);
            pose.Add("Spine Twist Left-Right", 0.06f * s).Add("Chest Twist Left-Right", 0.12f * s).Add("UpperChest Twist Left-Right", 0.06f * s);
            pose.Add("Right Arm Front-Back", -0.06f * s).Add("Left Arm Front-Back", 0.05f * s);

            // Both hands closed round the broom handle.
            pose.Toward(Join(Fist("Right", 0.85f), Fist("Left", 0.85f)), 1f);
            return pose;
        }

        /// <summary>A duster flicked side to side at chest height, the other hand on the hip.</summary>
        static Pose WorkDust(float t, float length)
        {
            var pose = Bent(new Pose(Base), 6f);
            pose.Toward(Join(Arm("Right", -0.1f, -0.6f, 0.1f, 0.4f), Arm("Left", -0.12f, 0.2f, -0.85f, -0.3f, 0f, 0.35f)), 1f);

            float flick = Mathf.Sin(t / length * Mathf.PI * 4f);
            pose.Add("Right Hand In-Out", 0.4f * flick).Add("Right Forearm Twist In-Out", 0.2f * flick);
            pose.Add("Chest Twist Left-Right", 0.03f * flick);

            // The duster held in the fist.
            pose.Toward(Fist("Right", 0.85f), 1f);
            return pose;
        }

        // --- On the move ---------------------------------------------------------------------------------

        /// <summary>
        /// A jog, made from the stock walk: the same steps quicker, longer and springier - thighs and
        /// knees swinging further, a bounce, a lean into it, the arms bent and pumping. For anyone going
        /// faster than the walk's own pace, so their feet keep up with the ground.
        /// </summary>
        static Pose Jog(WalkCycle walk, float t)
        {
            float u = t / CharacterLocomotion.JogCycleSeconds;
            Pose pose = walk.At(u);

            foreach (string side in new[] { "Left", "Right" })
            {
                walk.Swing(pose, $"{side} Upper Leg Front-Back", 1.35f);
                walk.Swing(pose, $"{side} Lower Leg Stretch", 1.4f);
                pose.Add($"{side} Lower Leg Stretch", -0.08f);
                walk.Swing(pose, $"{side} Arm Front-Back", 1.8f);
                walk.Swing(pose, $"{side} Foot Up-Down", 1.25f);

                // Forearms up and pumping with the arms.
                if (pose.TryGetValue($"{side} Forearm Stretch", out float elbow)) pose[$"{side} Forearm Stretch"] = Mathf.Lerp(elbow, -0.25f, 0.8f);
                pose.Add($"{side} Arm Down-Up", -0.06f);
            }

            walk.Swing(pose, "RootT.y", 1.7f);
            return Bent(pose, 7f);
        }

        // --- After a meal -------------------------------------------------------------------------------
        //
        // Played sat at the table on the visitors' arms-only layer (and available to anyone with the
        // worker controller): only the arms and fingers of these count there. The arm values were
        // found by searching the muscles for where the wrist and elbow land on the rigs (wrist at the
        // belly, at the mouth, a fist up in front of the shoulder...), not by eye.

        /// <summary>Both hands to the belly, the right one patting it.</summary>
        static Pose PatBelly(float t, float length)
        {
            var target = Arms(-0.6f, 0.1f, -0.2f, -0.1f, 0f, 0.15f);
            var pose = new Pose(Base).Toward(target, Envelope(t, length, 0f, 0.4f, 0.45f));

            float pat = Envelope(t, length, 0.4f, 0.1f, 0.6f) * Mathf.Max(0f, Wave(t, 2.8f));
            pose.Add("Right Forearm Stretch", 0.16f * pat).Add("Right Hand Down-Up", -0.35f * pat);
            pose.Add("Left Shoulder Down-Up", 0.06f * pat).Add("Right Shoulder Down-Up", 0.06f * pat);
            return pose;
        }

        /// <summary>The right fist up in front of the shoulder, thumb up, pumped once or twice.</summary>
        static Pose ThumbsUp(float t, float length)
        {
            float up = Envelope(t, length, 0f, 0.3f, 0.4f);
            var pose = new Pose(Base).Toward(Join(Arm("Right", -0.4f, 0f, 0.4f, -0.2f, 0.3f, 0.1f), Fist("Right", 1f, 1f)), up);

            float pump = Envelope(t, length, 0.3f, 0.1f, 0.5f) * Mathf.Max(0f, Wave(t, 2.2f));
            pose.Add("Right Arm Down-Up", 0.07f * pump).Add("Right Forearm Stretch", -0.08f * pump);
            return pose;
        }

        /// <summary>The back of the right hand at the mouth, side to side, as with a napkin.</summary>
        static Pose WipeMouth(float t, float length)
        {
            var pose = new Pose(Base).Toward(Join(Arm("Right", -0.1f, -0.5f, 0.2f, -0.4f, 0f, -0.15f), Fist("Right", 0.4f)),
                                              Envelope(t, length, 0f, 0.45f, 0.5f));

            float wipe = Envelope(t, length, 0.4f, 0.15f, 0.6f) * Wave(t, 1.8f);
            pose.Add("Right Arm Front-Back", 0.1f * wipe).Add("Right Hand In-Out", 0.25f * wipe);
            return pose;
        }

        /// <summary>Fingertips kissed at the lips, then flung out, up and open: a chef's kiss.</summary>
        static Pose ChefKiss(float t, float length)
        {
            Pose atLips = Join(Arm("Right", -0.1f, -0.5f, 0.2f, -0.4f, 0f, 0.1f), Fist("Right", 0.55f, 0.3f));
            Pose flung = Join(Arm("Right", 0.6f, -0.4f, 0.4f, 0.9f, 0.3f, 0.2f), Spread("Right"));

            var pose = new Pose(Base).Toward(atLips, Envelope(t, length, 0f, 0.4f, 0.5f));
            float fling = Ease((t - 0.65f) / 0.22f) * (1f - Ease((t - (length - 0.5f)) / 0.5f));
            pose.Toward(flung, fling);
            return pose;
        }

        /// <summary>Hands up in front of the chest, clapping three times a second.</summary>
        static Pose Clap(float t, float length)
        {
            Pose apart = Join(Arms(-0.25f, 0.1f, 0f, -0.3f, 0.5f, 0.1f), Spread("Left"), Spread("Right"));
            Pose together = Arms(-0.3f, -0.1f, -0.2f, -0.1f, 0.5f, 0.1f);

            var pose = new Pose(Base).Toward(apart, Envelope(t, length, 0f, 0.3f, 0.4f));
            float clap = Envelope(t, length, 0.3f, 0.1f, 0.45f) * (0.5f + 0.5f * Wave(t, 3f));
            pose.Toward(together, clap);
            return pose;
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
            Add(Gesture.PatBelly, PatBelly);
            Add(Gesture.ThumbsUp, ThumbsUp);
            Add(Gesture.WipeMouth, WipeMouth);
            Add(Gesture.ChefKiss, ChefKiss);
            Add(Gesture.Clap, Clap);
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
            AnimationClip wipe = Bake("WorkWipe", WorkerGestures.WipeSeconds, true, t => WorkWipe(t, WorkerGestures.WipeSeconds));
            AnimationClip sweep = Bake("WorkSweep", WorkerGestures.SweepSeconds, true, t => WorkSweep(t, WorkerGestures.SweepSeconds));
            AnimationClip dust = Bake("WorkDust", WorkerGestures.DustSeconds, true, t => WorkDust(t, WorkerGestures.DustSeconds));
            Dictionary<Gesture, AnimationClip> gestures = BuildGestures();
            AnimationClip jog = BuildJog();

            AvatarMask mask = UpperBodyMask();
            AnimatorController controller = Controller();

            // Parameters: the visitors' three, so nothing that sets them warns, and the worker's own.
            controller.AddParameter("Move", AnimatorControllerParameterType.Float);
            controller.AddParameter(CharacterLocomotion.SpeedParam, AnimatorControllerParameterType.Float);
            controller.AddParameter("Sit", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Eat", AnimatorControllerParameterType.Bool);
            controller.AddParameter(WorkerGestures.WorkingParam, AnimatorControllerParameterType.Bool);
            controller.AddParameter(WorkerGestures.WorkStyleParam, AnimatorControllerParameterType.Int);
            controller.AddParameter(WorkerGestures.GestureParam, AnimatorControllerParameterType.Int);
            controller.AddParameter(WorkerGestures.ActParam, AnimatorControllerParameterType.Trigger);

            // Base: the breathing idle, the walk and the jog, blended by real speed; sitting kept from the visitors'.
            controller.AddLayer("Base");
            AnimatorStateMachine body = controller.layers[0].stateMachine;
            AnimatorState movement = controller.CreateBlendTreeInController("Movement", out BlendTree tree, 0);
            PaceTree(tree, idle, jog);
            movement.iKOnFeet = true;
            body.defaultState = movement;

            AnimatorState sitting = body.AddState("Sitting");
            sitting.motion = Clip(SitSource);
            AnimatorStateTransition sit = Transition(body.AddAnyStateTransition(sitting), 0.25f);
            sit.canTransitionToSelf = false;
            sit.AddCondition(AnimatorConditionMode.If, 0f, "Sit");
            Transition(sitting.AddTransition(movement), 0.25f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Sit");

            // Upper body: gestures and work over whatever the legs are doing. Its IK pass puts the
            // crew's hands on what they work on, over the work loop.
            controller.AddLayer(WorkerGestures.LayerName);
            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = true;
            layers[1].defaultWeight = 1f;
            layers[1].avatarMask = mask;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[1].iKPass = true;
            controller.layers = layers;

            AnimatorStateMachine upper = controller.layers[1].stateMachine;
            AnimatorState empty = upper.AddState("Empty");
            upper.defaultState = empty;

            AnimatorState workHands = Looping(upper, "Work Hands", hands);
            AnimatorState workSwing = Looping(upper, "Work Swing", swing);
            AnimatorState workWipe = Looping(upper, "Work Wipe", wipe);
            AnimatorState workSweep = Looping(upper, "Work Sweep", sweep);
            AnimatorState workDust = Looping(upper, "Work Dust", dust);
            foreach ((AnimatorState state, int style) in new[]
                     {
                         (workHands, WorkerGestures.WorkHands), (workSwing, WorkerGestures.WorkSwing), (workWipe, WorkerGestures.WorkWipe),
                         (workSweep, WorkerGestures.WorkSweep), (workDust, WorkerGestures.WorkDust),
                     })
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

            string carried = AddCarryLayer(controller);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return $"Worker animator: breathing idle, {gestures.Count} gestures, 5 work loops, {carried}; {ControllerPath}.";
        }

        /// <summary>The jog, made from the stock walk (<see cref="Jog"/>).</summary>
        internal static AnimationClip BuildJog()
        {
            var walk = new WalkCycle(Clip(WalkSource));
            return Bake("Jog", CharacterLocomotion.JogCycleSeconds, true, t => Jog(walk, t));
        }

        /// <summary>
        /// Locomotion by real speed (<see cref="CharacterLocomotion"/>): idle at 0, the stock walk at its own
        /// pace, the jog at its own - so between and at them the feet keep up with the ground.
        /// </summary>
        internal static void PaceTree(BlendTree tree, Motion idle, Motion jog)
        {
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = CharacterLocomotion.SpeedParam;
            tree.useAutomaticThresholds = false;
            while (tree.children.Length > 0) tree.RemoveChild(0);
            tree.AddChild(idle, 0f);
            tree.AddChild(Clip(WalkSource), CharacterLocomotion.WalkPace);
            if (jog != null) tree.AddChild(jog, CharacterLocomotion.JogPace);
        }

        /// <summary>
        /// Puts another controller's "Movement" on <see cref="PaceTree"/> - the visitors', the old waiters' -
        /// keeping its own idle. Adds the Speed parameter. False when it has no Movement blend tree.
        /// </summary>
        internal static bool PaceMovement(AnimatorController controller)
        {
            if (controller == null) return false;

            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            {
                if (child.state.name != "Movement" || !(child.state.motion is BlendTree tree)) continue;

                Motion idle = tree.children.Length > 0 ? tree.children[0].motion : null;
                if (idle == null) return false;

                bool has = false;
                foreach (AnimatorControllerParameter p in controller.parameters)
                    if (p.name == CharacterLocomotion.SpeedParam) has = true;
                if (!has) controller.AddParameter(CharacterLocomotion.SpeedParam, AnimatorControllerParameterType.Float);

                var jog = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/Worker_Jog.anim");
                PaceTree(tree, idle, jog != null ? jog : BuildJog());
                EditorUtility.SetDirty(tree);
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssetIfDirty(controller);
                return true;
            }

            return false;
        }

        /// <summary>The carrying pose's loop: seconds of breathing over the held arms.</summary>
        const float CarryPlateSeconds = 4f;

        /// <summary>
        /// A plate held up in front in both hands: forearms forward at the waist, elbows in at the sides,
        /// palms up under it. Found by searching the arm muscles for the wrists and elbows of a tray hold,
        /// as the reactions were.
        /// </summary>
        static Pose CarryPlate(float t, float length)
        {
            Pose held = Join(Arms(-0.65f, 0f, 0.2f, 0f, 0.6f, -0.15f), Fist("Left", 0.15f), Fist("Right", 0.15f));
            return Held(held, t, length);
        }

        /// <summary>
        /// Both arms held up round a plate, on the arms only, at weight 0 until a waiter lifts one. So anybody
        /// on this controller can wait tables, and the crew wear the same controller as the workers.
        /// (The staff's own carry clip held a box on one arm, and a plate set between the hands hung beside
        /// the other.)
        /// </summary>
        static string AddCarryLayer(AnimatorController controller)
        {
            AnimationClip carry = Bake("CarryPlate", CarryPlateSeconds, true, t => CarryPlate(t, CarryPlateSeconds));

            controller.AddLayer(WorkerGestures.CarryLayerName);
            AnimatorControllerLayer[] layers = controller.layers;
            int index = layers.Length - 1;
            layers[index].avatarMask = ArmsMask();
            layers[index].defaultWeight = 0f;
            layers[index].blendingMode = AnimatorLayerBlendingMode.Override;
            controller.layers = layers;

            AnimatorStateMachine machine = controller.layers[index].stateMachine;
            AnimatorState holding = machine.AddState(WorkerGestures.CarryLayerName);
            holding.motion = carry;
            machine.defaultState = holding;
            return $"carry layer ({carry.name})";
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

        internal const string ArmsMaskPath = Folder + "/ArmsOnly.mask";

        /// <summary>
        /// Both arms and their fingers, nothing else: a plate held up in front while the legs walk, a
        /// diner's gesture while the chair keeps their back and hips. One mask for both.
        /// </summary>
        internal static AvatarMask ArmsMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(ArmsMaskPath);
            if (mask == null)
            {
                // It was the visitors' own once: renamed in place, so what used it keeps it.
                const string old = Folder + "/VisitorArms.mask";
                if (AssetDatabase.LoadAssetAtPath<AvatarMask>(old) != null && string.IsNullOrEmpty(AssetDatabase.MoveAsset(old, ArmsMaskPath)))
                    mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(ArmsMaskPath);
            }

            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, ArmsMaskPath);
            }

            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool arms = part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm ||
                            part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, arms);
            }

            EditorUtility.SetDirty(mask);
            AssetDatabase.SaveAssetIfDirty(mask);
            return mask;
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
