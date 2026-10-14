using System;
using System.Collections.Generic;
using System.IO;
using LastOrders.Night;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Pose = LastOrders.EditorTools.WorkerAnimations.Pose;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The zombies' own animations, made in code. Until now a zombie was the visitors' walk with its
    /// arms bent forward by hand every frame: no idle of its own, no swing at a wall, no flinch, and
    /// a death that tipped the whole body over like a plank.
    ///
    /// The clips are humanoid ones, written as muscle curves on top of the stock idle's stance (see
    /// <see cref="WorkerAnimations"/>), so they play on every rig a zombie wears - both zombie models
    /// and the farm boy - and Unity retargets them:
    /// - two idles: a hunched sway with twitches, and a drooping head that snaps up to look round;
    /// - two shambles - arms reaching out in front, or hanging and swinging - both the stock walk re-timed
    ///   into a limp, one leg stiff and dragging, the body lurching over the good one, a stumble every
    ///   other step; and a lurching lope for the fast ones. Every walk loop is two cycles long so they
    ///   blend foot for foot;
    /// - two battering loops, a two-fisted slam and a raking claw, the blow landing halfway through;
    /// - three flinches, added over whatever else is playing;
    /// - two deaths: thrown onto its back, or down on its knees and over onto its face.
    ///
    /// The controller blends idle, shamble and lope by speed and the two styles by a number picked per
    /// life; the battering is a layer over the whole body and the flinches an additive one over the
    /// upper body. Its parameters, states and timings are <see cref="ZombieMotion"/>'s, and
    /// <see cref="ZombieBody"/> plays it.
    /// Both zombie prefabs get it; the visitors' controller is left as it was, and 'Restore Zombies'
    /// Visitor Animator' puts it back on them.
    /// </summary>
    public static class ZombieAnimations
    {
        public const string ClipFolder = WorkerAnimations.Folder + "/Animations/Zombies";
        public const string ControllerPath = WorkerAnimations.Folder + "/ZombieAnimator.controller";
        const string MaskPath = WorkerAnimations.Folder + "/ZombieUpperBody.mask";

        const string WalkSource = "Assets/Game Assets/Prefabs/Characters/Animations/Yaku J Ignite@Walking.fbx";
        const string VisitorControllerPath = "Assets/Game Assets/Prefabs/Characters/VisitorAnimator.controller";

        const float Fps = 30f;

        static readonly string[] Prefabs = { ZombieContentGenerator.ZombiePath, ZombieContentGenerator.WandererPath };

        // --- Pose helpers ----------------------------------------------------------------------------
        //
        // Measured on these rigs (the stock stance; both zombie models and the farm boy alike):
        // - Upper Leg Front-Back is 0.6 standing and NEGATIVE swings the thigh forward, about 48 degrees
        //   a unit (sitting is about -0.6). Lower Leg Stretch is 1 straight and 0 at a right angle.
        // - Spine, Chest and UpperChest Front-Back NEGATIVE bend forward. The body's orientation is held
        //   while they do, so the pelvis tips the other way, legs and all: (-0.2, -0.35, -0.3) turns the
        //   chest 15 degrees forward and the pelvis 13 back. Turning the body 13 degrees forward with it
        //   keeps the pelvis where it was and puts the chest 28 forward (Bend). Left-Right is the same
        //   about the roll (Lean), + to the right.
        // - Arm Down-Up + raises the arm; Arm Front-Back - swings it forward - but its zero is not the
        //   side: about +0.3 holds the arm out square to the side, 0 is already 30 degrees forward, and
        //   -0.9 swings it across the chest. Straight ahead is about -0.6. Forearm Stretch 1 is
        //   straight. Neck Nod - bends the neck forward; Head Nod + looks up.

        /// <summary>Degrees the chest turns for one unit of the spine bend in <see cref="Bend"/>, the pelvis held.</summary>
        const float BendPerUnit = 28f;

        /// <summary>Degrees the pelvis tips back for each unit of that bend, which the body's turn gives back.</summary>
        const float PelvisPerUnit = 13f;

        /// <summary>Degrees of thigh swing for one unit of Upper Leg Front-Back.</summary>
        const float ThighDegrees = 48f;

        static Pose Start() => new Pose(WorkerAnimations.Base);

        static Pose Both(this Pose pose, string name, float amount) =>
            pose.Add(string.Format(name, "Left"), amount).Add(string.Format(name, "Right"), amount);

        static Pose Set(this Pose pose, string name, float value)
        {
            pose[name] = value;
            return pose;
        }

        static Pose Arm(this Pose pose, string side, float downUp, float frontBack, float stretch, float hand = 0f, float twist = 0f) =>
            pose.Set($"{side} Arm Down-Up", downUp).Set($"{side} Arm Front-Back", frontBack).Set($"{side} Forearm Stretch", stretch)
                .Set($"{side} Hand Down-Up", hand).Set($"{side} Arm Twist In-Out", twist);

        static readonly string[] Fingers = { "Index", "Middle", "Ring", "Little" };

        /// <summary>Fingers hooked into a claw: <paramref name="amount"/> 0 as they are, 1 hooked.</summary>
        static Pose Claw(this Pose pose, string side, float amount)
        {
            foreach (string finger in Fingers)
            {
                pose.Set($"{side}Hand.{finger}.1 Stretched", Mathf.Lerp(Get(pose, $"{side}Hand.{finger}.1 Stretched"), -0.25f, amount));
                pose.Set($"{side}Hand.{finger}.2 Stretched", Mathf.Lerp(Get(pose, $"{side}Hand.{finger}.2 Stretched"), -0.75f, amount));
                pose.Set($"{side}Hand.{finger}.3 Stretched", Mathf.Lerp(Get(pose, $"{side}Hand.{finger}.3 Stretched"), -0.6f, amount));
            }

            return pose;
        }

        static float Get(Pose pose, string name) => pose.TryGetValue(name, out float v) ? v : 0f;

        /// <summary>Only the shoulders, arms, hands and fingers of <paramref name="pose"/>: to bend a walk's arms toward, leaving its legs alone.</summary>
        static Pose ArmsOf(Pose pose)
        {
            var arms = new Pose();
            foreach (KeyValuePair<string, float> pair in pose)
                if (pair.Key.Contains("Arm") || pair.Key.Contains("Hand") || pair.Key.Contains("Shoulder")) arms[pair.Key] = pair.Value;
            return arms;
        }

        static Quaternion BodyTurn(Pose pose)
        {
            var q = new Quaternion(Get(pose, "RootQ.x"), Get(pose, "RootQ.y"), Get(pose, "RootQ.z"), Get(pose, "RootQ.w"));
            float length = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return length > 1e-6f ? new Quaternion(q.x / length, q.y / length, q.z / length, q.w / length) : Quaternion.identity;
        }

        static Pose SetBodyTurn(this Pose pose, Quaternion q)
        {
            if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            return pose.Set("RootQ.x", q.x).Set("RootQ.y", q.y).Set("RootQ.z", q.z).Set("RootQ.w", q.w);
        }

        /// <summary>The whole body turned by <paramref name="euler"/> degrees about its middle: + X tips it forward, + Z rolls it to its left.</summary>
        static Pose Turn(this Pose pose, Vector3 euler) => pose.SetBodyTurn(Quaternion.Euler(euler) * BodyTurn(pose));

        /// <summary>The chest bent forward by <paramref name="degrees"/> (negative: arched back) over a pelvis that stays as it was, legs and all.</summary>
        static Pose Bend(this Pose pose, float degrees)
        {
            float s = degrees / BendPerUnit;
            pose.Add("Spine Front-Back", -0.2f * s).Add("Chest Front-Back", -0.35f * s).Add("UpperChest Front-Back", -0.3f * s);
            return pose.Turn(new Vector3(PelvisPerUnit * s, 0f, 0f));
        }

        /// <summary>The chest leant over to its right by <paramref name="degrees"/> (negative: its left), the pelvis kept.</summary>
        static Pose Lean(this Pose pose, float degrees)
        {
            float s = degrees / BendPerUnit;
            pose.Add("Spine Left-Right", 0.2f * s).Add("Chest Left-Right", 0.35f * s).Add("UpperChest Left-Right", 0.3f * s);
            return pose.Turn(new Vector3(0f, 0f, -PelvisPerUnit * s));
        }

        /// <summary>The chest twisted round to its right by about <paramref name="degrees"/> (negative: its left). Keep it small: the pelvis turns back against it.</summary>
        static Pose Twist(this Pose pose, float degrees)
        {
            float s = degrees / BendPerUnit;
            return pose.Add("Spine Twist Left-Right", 0.2f * s).Add("Chest Twist Left-Right", 0.35f * s).Add("UpperChest Twist Left-Right", 0.3f * s);
        }

        /// <summary>Tipped forward at the hips by <paramref name="degrees"/>, pelvis and all, the thighs swung forward to keep the feet under it.</summary>
        static Pose Tip(this Pose pose, float degrees)
        {
            pose.Turn(new Vector3(degrees, 0f, 0f));
            return pose.Both("{0} Upper Leg Front-Back", -degrees / ThighDegrees);
        }

        /// <summary>Stooped: the back curved over and tipped at the hips, the neck thrust forward and the head tipped back up to look ahead.</summary>
        static Pose Hunch(this Pose pose, float amount)
        {
            pose.Bend(22f * amount).Tip(9f * amount);
            pose.Add("Neck Nod Down-Up", -0.15f * amount).Add("Head Nod Down-Up", 0.5f * amount);
            return pose.Both("{0} Shoulder Down-Up", -0.08f * amount);
        }

        /// <summary>Knees bent a little under the stoop, the hips down to suit.</summary>
        static Pose Sag(this Pose pose, float amount)
        {
            pose.Both("{0} Upper Leg Front-Back", -0.1f * amount).Both("{0} Lower Leg Stretch", -0.18f * amount);
            pose.Both("{0} Foot Up-Down", 0.06f * amount);
            return pose.Add("RootT.y", -0.022f * amount);
        }

        /// <summary>Both arms straight out in front, shoulder width apart, raised against the stoop so they reach level; the hands hanging off the wrists, clawed.</summary>
        static Pose Reach(this Pose pose)
        {
            pose.Arm("Left", 0.45f, -0.68f, 0.88f, -0.35f).Arm("Right", 0.45f, -0.7f, 0.86f, -0.35f);
            return pose.Claw("Left", 0.6f).Claw("Right", 0.6f);
        }

        /// <summary>Both arms hanging dead from a stooped body - swung forward, so they hang straight down - the left shoulder slumped lower.</summary>
        static Pose Hang(this Pose pose)
        {
            pose.Arm("Left", -0.5f, -0.1f, 0.92f, -0.2f).Arm("Right", -0.46f, -0.12f, 0.9f, -0.2f);
            pose.Add("Left Shoulder Down-Up", -0.25f);
            return pose.Claw("Left", 0.35f).Claw("Right", 0.45f);
        }

        // --- Envelopes ------------------------------------------------------------------------------

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static float EaseIn(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t;
        }

        /// <summary>A sine that repeats a whole number of times over <paramref name="length"/>, so a loop closes.</summary>
        static float Cycle(float t, float length, int times, float phase = 0f) =>
            Mathf.Sin((t / length * times + phase) * Mathf.PI * 2f);

        /// <summary>A twitch at <paramref name="at"/>: snapping to full in <paramref name="rise"/> seconds, dying away over about <paramref name="fall"/>.</summary>
        static float Twitch(float t, float at, float rise, float fall)
        {
            float d = t - at;
            if (d <= 0f) return 0f;
            if (d < rise) return Ease(d / rise);
            return Mathf.Exp(-(d - rise) / fall);
        }

        /// <summary>Up over <paramref name="rise"/>, held, down over <paramref name="fall"/>: an event from <paramref name="from"/> to <paramref name="to"/>.</summary>
        static float Window(float t, float from, float to, float rise, float fall) =>
            Ease((t - from) / rise) * (1f - Ease((t - (to - fall)) / fall));

        // --- The idles -----------------------------------------------------------------------------

        /// <summary>Hunched, arms half raised and drooping, swaying from foot to foot; the head lolls after
        /// the sway, the chest heaves, a jerk of the head and shoulder, and later of the left arm.</summary>
        static Pose IdleSwayer(float t)
        {
            float length = ZombieMotion.IdleSeconds;
            var pose = Start().Hunch(1f).Sag(1f).Hang();
            pose.Toward(ArmsOf(Start().Reach()), 0.55f);

            float sway = Cycle(t, length, 2);
            pose.Add("RootT.x", 0.03f * sway).Lean(-5f * sway);
            pose.Add("Left Upper Leg In-Out", 0.03f * sway).Add("Right Upper Leg In-Out", -0.03f * sway);

            float lag = Cycle(t, length, 2, -0.15f);
            pose.Add("Head Tilt Left-Right", 0.2f * lag).Add("Neck Tilt Left-Right", 0.08f * lag);
            pose.Add("Head Turn Left-Right", 0.1f * Cycle(t, length, 1, 0.3f));

            float heave = 0.6f * Cycle(t, length, 3) + 0.4f * Cycle(t, length, 7, 0.3f);
            pose.Bend(2.5f * heave).Both("{0} Shoulder Down-Up", 0.04f * heave);

            // The arms swing after the body like weights.
            float swing = Cycle(t, length, 2, -0.22f);
            pose.Add("Left Arm Front-Back", 0.07f * swing).Add("Right Arm Front-Back", -0.05f * swing);
            pose.Add("Left Arm Down-Up", 0.04f * Cycle(t, length, 3, 0.1f)).Add("Right Arm Down-Up", 0.04f * Cycle(t, length, 3, 0.6f));
            pose.Add("Jaw Close", -0.25f - 0.15f * Cycle(t, length, 3, 0.2f));

            float jerk = Twitch(t, 2.1f, 0.06f, 0.25f);
            pose.Add("Head Turn Left-Right", 0.45f * jerk).Add("Head Tilt Left-Right", -0.22f * jerk);
            pose.Add("Right Shoulder Down-Up", 0.35f * jerk).Add("Right Arm Down-Up", 0.12f * jerk);

            float arm = Twitch(t, 4.4f, 0.05f, 0.18f);
            pose.Add("Left Arm Down-Up", 0.25f * arm).Add("Left Forearm Stretch", -0.35f * arm).Twist(5f * arm);
            pose.Claw("Left", 0.5f * arm);
            return pose;
        }

        /// <summary>Head hanging to one side, arms dead at its sides, rocking; halfway through the head
        /// snaps up and looks left and right as if it heard something, then sinks again.</summary>
        static Pose IdleDrooper(float t)
        {
            float length = ZombieMotion.IdleSeconds;
            var pose = Start().Hunch(1.3f).Sag(1.2f).Hang();
            pose.Add("Neck Nod Down-Up", -0.2f).Add("Head Nod Down-Up", -0.75f).Add("Head Tilt Left-Right", 0.32f).Add("Neck Tilt Left-Right", 0.1f);

            float rock = Cycle(t, length, 1);
            pose.Bend(4f * rock);
            pose.Add("RootT.x", -0.015f * Cycle(t, length, 1, 0.25f));

            float swing = Cycle(t, length, 1, -0.15f);
            pose.Add("Left Arm Front-Back", -0.1f * swing).Add("Right Arm Front-Back", -0.08f * swing);
            pose.Add("Jaw Close", -0.4f);

            // Something heard: up, a look one way and the other, and down again.
            float up = Window(t, 2.8f, 4.9f, 0.14f, 0.7f);
            pose.Add("Neck Nod Down-Up", 0.3f * up).Add("Head Nod Down-Up", 0.7f * up).Add("Head Tilt Left-Right", -0.3f * up);
            pose.Bend(-8f * up).Add("Jaw Close", 0.25f * up);
            pose.Add("Head Turn Left-Right", 0.45f * up * Mathf.Sin(Mathf.Clamp01((t - 2.95f) / 1.6f) * Mathf.PI * 2f));

            float twitch = Twitch(t, 1.2f, 0.05f, 0.2f);
            pose.Add("Right Arm Down-Up", 0.18f * twitch).Add("Right Forearm Stretch", -0.3f * twitch).Claw("Right", 0.6f * twitch);
            return pose;
        }

        // --- The walks -----------------------------------------------------------------------------

        /// <summary>The stock walk's curves, to be read at any moment.</summary>
        sealed class Walk
        {
            readonly List<(string name, AnimationCurve curve)> curves = new List<(string, AnimationCurve)>();
            public readonly float Length;

            public Walk(AnimationClip clip)
            {
                Length = clip.length;
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                    curves.Add((binding.propertyName, AnimationUtility.GetEditorCurve(clip, binding)));
            }

            /// <summary>The walk at <paramref name="cycle"/> (0 to 1) of its cycle.</summary>
            public Pose At(float cycle)
            {
                var pose = Start();
                float t = Mathf.Repeat(cycle, 1f) * Length;
                foreach ((string name, AnimationCurve curve) in curves) pose[name] = curve.Evaluate(t);
                return pose;
            }
        }

        /// <summary>Scales <paramref name="name"/>'s swing about <paramref name="middle"/>.</summary>
        static void Scale(Pose pose, string name, float middle, float by)
        {
            if (pose.TryGetValue(name, out float v)) pose[name] = middle + (v - middle) * by;
        }

        /// <summary>
        /// The stock walk re-timed into a limp: it lingers on the good (right) leg and hurries over the
        /// bad (left) one, whose knee hardly bends so its toe drags through, swung out round the hip.
        /// <paramref name="limp"/> is how uneven the steps are.
        /// </summary>
        static Pose Limp(Walk walk, float u, float limp, float stride)
        {
            float warped = u - limp / (Mathf.PI * 2f) * Mathf.Sin(u * Mathf.PI * 2f);
            Pose pose = walk.At(warped);

            Scale(pose, "Left Upper Leg Front-Back", 0.45f, stride);
            Scale(pose, "Right Upper Leg Front-Back", 0.45f, stride);

            // The bad leg swings forward round the hip, knee stiff, toe down: a drag, not a step.
            float swingBad = Mathf.Max(0f, Mathf.Cos(u * Mathf.PI * 2f));
            if (pose.TryGetValue("Left Lower Leg Stretch", out float knee)) pose["Left Lower Leg Stretch"] = Mathf.Lerp(knee, 0.95f, 0.55f);
            pose.Add("Left Upper Leg In-Out", 0.07f * swingBad).Add("Left Foot Up-Down", -0.25f * swingBad);
            pose.Add("Left Upper Leg Twist In-Out", -0.08f * swingBad);

            // Over the good leg, then a quick hitch down as the weight goes onto the bad one.
            float over = Mathf.Cos(u * Mathf.PI * 2f);
            pose.Add("RootT.x", 0.022f * over).Lean(7f * over);
            float hitch = Mathf.Max(0f, -over);
            pose.Add("RootT.y", -0.03f * hitch * hitch);
            pose.Twist(4f * Mathf.Sin(u * Mathf.PI * 2f));
            return pose;
        }

        /// <summary>
        /// The shamble, two cycles long: the limp, stooped, the head bobbing a beat behind the steps and
        /// lolling after the lurch; a stumble partway through the second cycle. <paramref name="reacher"/>
        /// holds the arms out in front, clawing; otherwise they hang and swing like dead weights.
        /// </summary>
        static Pose Shamble(Walk walk, float t, bool reacher)
        {
            float cycle = ZombieMotion.ShambleCycleSeconds;
            float length = cycle * ZombieMotion.CyclesPerLoop;
            float c = t / cycle;
            float u = Mathf.Repeat(c, 1f);
            bool second = c >= 1f && c < 2f;

            Pose pose = Limp(walk, u, reacher ? 0.32f : 0.42f, ZombieMotion.ShambleStride);
            pose.Hunch(reacher ? 1f : 1.3f);

            float wave = u * Mathf.PI * 2f;
            pose.Add("Head Nod Down-Up", 0.06f * Mathf.Cos(wave * 2f + 0.6f));
            pose.Add("Head Tilt Left-Right", 0.16f * Mathf.Cos(wave - 0.8f) + (reacher ? 0f : 0.26f));
            pose.Add("Neck Nod Down-Up", reacher ? 0f : -0.16f).Add("Head Nod Down-Up", reacher ? 0f : -0.35f);
            pose.Add("Head Turn Left-Right", 0.08f * Cycle(t, length, 1, 0.2f));
            pose.Add("Jaw Close", -0.3f - 0.15f * Cycle(t, length, 2, 0.1f));

            if (reacher)
            {
                pose.Toward(ArmsOf(Start().Reach()), 0.9f);

                // Out of step with each other and with the feet; one hand a little higher, then the other.
                pose.Add("Left Arm Down-Up", 0.06f * Mathf.Sin(wave * 2f + 0.3f) + 0.05f * Cycle(t, length, 1));
                pose.Add("Right Arm Down-Up", 0.06f * Mathf.Sin(wave * 2f + 1.9f) - 0.05f * Cycle(t, length, 1));
                pose.Add("Left Arm Front-Back", 0.05f * Mathf.Sin(wave)).Add("Right Arm Front-Back", -0.05f * Mathf.Sin(wave));
                pose.Add("Left Hand Down-Up", 0.12f * Mathf.Sin(wave * 2f + 1f)).Add("Right Hand Down-Up", 0.12f * Mathf.Sin(wave * 2f + 2.4f));
            }
            else
            {
                // Some of the walk's own arm swing, lagging; the rest hanging.
                pose.Toward(ArmsOf(Start().Hang()), 0.75f);
                float swing = Mathf.Sin(wave - 1.2f);
                pose.Add("Left Arm Front-Back", 0.14f * swing).Add("Right Arm Front-Back", -0.12f * swing);
                pose.Add("Left Forearm Stretch", -0.06f * Mathf.Abs(swing)).Add("Right Forearm Stretch", -0.05f * Mathf.Abs(swing));
            }

            // The stumble: pitched forward, head dropping, arms jerked down, caught again.
            if (second)
            {
                float trip = Window(u, 0.3f, 0.75f, 0.1f, 0.3f);
                pose.Bend(10f * trip).Add("Head Nod Down-Up", -0.2f * trip);
                pose.Add("RootT.y", -0.02f * trip).Both("{0} Arm Down-Up", -0.12f * trip);
            }

            return pose;
        }

        /// <summary>The fast ones: the walk at a run's pace and length of stride, thrown forward over the
        /// feet, knees high, head up and arms clawing forward and flailing; a slight limp still.</summary>
        static Pose Lope(Walk walk, float t)
        {
            float cycle = ZombieMotion.LopeCycleSeconds;
            float length = cycle * ZombieMotion.CyclesPerLoop;
            float u = Mathf.Repeat(t / cycle, 1f);
            float wave = u * Mathf.PI * 2f;

            Pose pose = Limp(walk, u, 0.14f, ZombieMotion.LopeStride * 1.1f);
            foreach (string side in new[] { "Left", "Right" })
                if (pose.TryGetValue($"{side} Lower Leg Stretch", out float knee)) pose[$"{side} Lower Leg Stretch"] = knee - 0.25f * (1f - knee);

            pose.Hunch(1.25f);
            pose.Add("Head Nod Down-Up", 0.15f).Add("RootT.y", -0.035f + 0.022f * Mathf.Cos(wave * 2f));

            pose.Toward(ArmsOf(Start().Reach()), 0.8f);
            pose.Add("Left Arm Front-Back", 0.26f * Mathf.Sin(wave)).Add("Right Arm Front-Back", -0.26f * Mathf.Sin(wave));
            pose.Add("Left Arm Down-Up", 0.1f * Mathf.Sin(wave * 2f + 0.5f)).Add("Right Arm Down-Up", 0.1f * Mathf.Sin(wave * 2f + 2.1f));
            pose.Add("Left Forearm Stretch", -0.15f * Mathf.Max(0f, Mathf.Sin(wave))).Add("Right Forearm Stretch", -0.15f * Mathf.Max(0f, -Mathf.Sin(wave)));
            pose.Add("Head Tilt Left-Right", 0.1f * Mathf.Cos(wave - 0.6f)).Add("Jaw Close", -0.5f);

            // The last loop's twitch still dying away as this one starts, so the loop closes.
            float jerk = Twitch(t, cycle * 1.35f, 0.05f, 0.15f) + Twitch(t + length, cycle * 1.35f, 0.05f, 0.15f);
            pose.Add("Head Turn Left-Right", 0.3f * jerk).Add("Head Tilt Left-Right", -0.2f * jerk);
            return pose;
        }

        // --- Battering ------------------------------------------------------------------------------
        //
        // The whole body, not just the arms: a blow is thrown from the hips, and any bend of the spine
        // moves the pelvis too, so the legs have to be the clip's own to stay planted.

        /// <summary>
        /// A battering loop from its three poses: rising from <paramref name="low"/> to <paramref name="high"/>
        /// for the first 40%, dropping onto <paramref name="struck"/> at the impact - accelerating, as a blow
        /// does - a shudder through the body there, and back down toward low by the end.
        /// </summary>
        static Pose Batter(float t, Pose low, Pose high, Pose struck)
        {
            float u = Mathf.Repeat(t / ZombieMotion.BatterSeconds, 1f);
            float impact = ZombieMotion.ImpactPhase;
            float lift = impact - 0.1f;

            Pose pose;
            if (u < lift) pose = new Pose(low).Toward(high, Ease(u / lift));
            else if (u < impact) pose = new Pose(high).Toward(struck, EaseIn((u - lift) / 0.1f));
            else
            {
                pose = new Pose(struck).Toward(low, Ease((u - impact - 0.12f) / (1f - impact - 0.12f)));
                float shudder = Mathf.Sin(Mathf.Clamp01((u - impact) / 0.12f) * Mathf.PI);
                pose.Bend(4f * shudder).Add("Head Nod Down-Up", -0.15f * shudder);
            }

            return pose;
        }

        static Pose Slam(float t)
        {
            Pose high = Start().Sag(1.1f).Hunch(0.5f).Bend(-16f)
                .Arm("Left", 0.95f, -0.72f, 0.55f, 0.25f).Arm("Right", 0.92f, -0.75f, 0.6f, 0.25f)
                .Both("{0} Shoulder Down-Up", 0.5f).Add("Head Nod Down-Up", 0.3f).Add("Jaw Close", -0.6f);
            Pose struck = Start().Sag(1.6f).Hunch(0.5f).Bend(20f).Tip(6f)
                .Arm("Left", 0.45f, -0.8f, 0.88f, -0.4f).Arm("Right", 0.42f, -0.82f, 0.9f, -0.4f)
                .Add("Head Nod Down-Up", -0.2f).Claw("Left", 0.8f).Claw("Right", 0.8f);
            Pose low = new Pose(struck).Toward(high, 0.2f);
            return Batter(t, low, high, struck);
        }

        static Pose Rake(float t)
        {
            Pose high = Start().Sag(1.1f).Hunch(0.6f).Bend(-6f).Twist(14f)
                .Arm("Right", 0.75f, -0.3f, 0.5f, 0.3f, 0.35f).Arm("Left", 0.2f, -0.6f, 0.8f, -0.35f)
                .Add("Right Shoulder Down-Up", 0.45f).Add("Head Nod Down-Up", 0.2f).Add("Head Turn Left-Right", -0.15f)
                .Claw("Right", 1f).Claw("Left", 0.6f).Add("Jaw Close", -0.6f);
            Pose struck = Start().Sag(1.6f).Hunch(0.6f).Bend(16f).Tip(5f).Twist(-16f).Lean(-5f)
                .Arm("Right", 0.25f, -0.98f, 0.92f, -0.4f, -0.35f).Arm("Left", -0.35f, -0.25f, 0.75f, -0.3f)
                .Add("Head Nod Down-Up", -0.1f).Add("Head Turn Left-Right", 0.12f).Claw("Right", 1f).Claw("Left", 0.6f);
            Pose low = new Pose(struck).Toward(high, 0.2f);
            return Batter(t, low, high, struck);
        }

        // --- Flinches -------------------------------------------------------------------------------

        /// <summary>0 at the start, snapped to 1 in 0.06 s, swinging back past rest and settling to 0 by the end.</summary>
        static float Jolt(float t)
        {
            float length = ZombieMotion.HitSeconds;
            const float snap = 0.06f;
            float k = t < snap ? Ease(t / snap) : Mathf.Exp(-(t - snap) * 7f) * Mathf.Cos((t - snap) * 13f);
            return k * (1f - Ease((t - (length - 0.12f)) / 0.12f));
        }

        /// <summary>
        /// A flinch, added over whatever is playing: the first frame is the stance itself, so only the
        /// difference counts. <paramref name="side"/> 0 is a hit square on, -1 from its left, 1 from its right.
        /// </summary>
        static Pose Hit(float t, int side)
        {
            float k = Jolt(t);
            var pose = Start();

            if (side == 0)
            {
                pose.Bend(-18f * k).Add("Neck Nod Down-Up", 0.25f * k).Add("Head Nod Down-Up", 0.3f * k);
                pose.Both("{0} Arm Down-Up", 0.22f * k).Both("{0} Forearm Stretch", -0.28f * k).Both("{0} Shoulder Down-Up", 0.2f * k);
                return pose.Add("Jaw Close", -0.5f * k);
            }

            // Knocked round and over, away from the side it was struck on.
            float s = -side;
            pose.Lean(12f * s * k).Twist(10f * s * k).Bend(-6f * k);
            pose.Add("Head Turn Left-Right", 0.32f * s * k).Add("Head Tilt Left-Right", 0.22f * s * k);
            string struck = side < 0 ? "Left" : "Right", other = side < 0 ? "Right" : "Left";
            pose.Add($"{struck} Arm Down-Up", 0.32f * k).Add($"{struck} Forearm Stretch", -0.3f * k).Add($"{struck} Shoulder Down-Up", 0.25f * k);
            pose.Add($"{other} Arm Front-Back", -0.15f * k);
            return pose.Add("Jaw Close", -0.4f * k);
        }

        // --- Deaths ---------------------------------------------------------------------------------

        /// <summary>One moment of a fall: a pose, where the body is (added to the stance) and how it is turned on top of the pose's own turn (degrees).</summary>
        sealed class Key
        {
            public float Time;
            public Pose Pose;
            public Vector3 Offset;
            public Vector3 Turn;

            /// <summary>How the way from the moment before to this one is taken.</summary>
            public Func<float, float> Ease = ZombieAnimations.Ease;
        }

        static Key K(float time, Pose pose, Vector3 offset, Vector3 turn, Func<float, float> ease = null) =>
            new Key { Time = time, Pose = pose, Offset = offset, Turn = turn, Ease = ease ?? (Func<float, float>)Ease };

        /// <summary>The fall at <paramref name="t"/>, between its keys; the body placed and turned from the stance.</summary>
        static Pose Through(Key[] keys, float t)
        {
            int i = 0;
            while (i < keys.Length - 2 && t >= keys[i + 1].Time) i++;

            Key a = keys[i], b = keys[i + 1];
            float s = b.Ease(Mathf.Clamp01((t - a.Time) / Mathf.Max(1e-4f, b.Time - a.Time)));

            var pose = new Pose(a.Pose).Toward(b.Pose, s);
            Vector3 offset = Vector3.LerpUnclamped(a.Offset, b.Offset, s);
            Vector3 turn = Vector3.LerpUnclamped(a.Turn, b.Turn, s);
            return Place(pose, offset, turn);
        }

        /// <summary>The body moved by <paramref name="offset"/> from the stance and turned by <paramref name="turn"/> on top of its own turn (degrees: + X tips it forward).</summary>
        static Pose Place(Pose pose, Vector3 offset, Vector3 turn)
        {
            Pose stance = WorkerAnimations.Base;
            pose["RootT.x"] = stance["RootT.x"] + offset.x;
            pose["RootT.y"] = Get(pose, "RootT.y") + offset.y;
            pose["RootT.z"] = stance["RootT.z"] + offset.z;
            return pose.Turn(turn);
        }

        /// <summary>How far the body's middle comes down to lie on the ground, from where it stands.</summary>
        const float LyingDrop = -0.83f;

        static Pose Standing() => Start().Hunch(1f).Sag(1f).Hang().Toward(ArmsOf(Start().Reach()), 0.45f);

        /// <summary>
        /// Thrown onto its back: a jolt back, the knees giving, a fall that gathers speed, a bounce off
        /// the ground with the limbs flung out, and still - one knee up, head rolled to the side.
        /// </summary>
        static Pose DeathBack(float t)
        {
            Pose stand = Standing();
            Pose jolt = Start().Bend(-20f).Add("Neck Nod Down-Up", 0.25f).Add("Head Nod Down-Up", 0.35f).Add("Jaw Close", -0.7f)
                .Arm("Left", -0.1f, -0.35f, 0.3f, 0.2f).Arm("Right", 0.0f, -0.25f, 0.25f, 0.2f).Claw("Left", 0.3f).Claw("Right", 0.3f);
            Pose buckle = Start().Bend(8f).Add("Head Nod Down-Up", -0.15f)
                .Both("{0} Upper Leg Front-Back", -0.55f).Both("{0} Lower Leg Stretch", -0.95f).Both("{0} Foot Up-Down", 0.2f)
                .Arm("Left", -0.2f, -0.45f, 0.5f, -0.2f).Arm("Right", -0.1f, -0.4f, 0.45f, -0.2f);
            Pose ground = Start().Add("Head Turn Left-Right", 0.4f).Add("Head Nod Down-Up", 0.05f).Add("Jaw Close", -0.5f)
                .Add("Left Upper Leg Front-Back", -0.55f).Add("Left Lower Leg Stretch", -0.75f)
                .Add("Right Upper Leg Front-Back", -0.05f).Add("Right Lower Leg Stretch", -0.15f).Add("Right Upper Leg In-Out", 0.12f)
                .Arm("Left", 0.45f, 0.36f, 0.75f, 0.1f).Arm("Right", 0.05f, 0.38f, 0.65f, 0.1f).Claw("Left", 0.4f).Claw("Right", 0.5f);
            Pose flung = new Pose(ground).Add("Left Arm Down-Up", 0.2f).Add("Right Arm Down-Up", 0.25f)
                .Add("Left Lower Leg Stretch", -0.15f).Add("Head Nod Down-Up", 0.15f);
            Pose still = new Pose(ground).Add("Left Lower Leg Stretch", 0.35f).Add("Left Upper Leg Front-Back", 0.3f)
                .Add("Head Turn Left-Right", 0.15f).Add("Left Arm Down-Up", -0.08f).Add("Jaw Close", -0.1f)
                .Claw("Left", 0f).Claw("Right", 0.2f);

            Key[] keys =
            {
                K(0f, stand, Vector3.zero, Vector3.zero),
                K(0.14f, jolt, new Vector3(0f, 0f, -0.06f), new Vector3(-12f, 0f, 0f)),
                K(0.48f, buckle, new Vector3(0.02f, -0.32f, -0.16f), new Vector3(-26f, 4f, 3f)),
                K(0.8f, ground, new Vector3(0.04f, LyingDrop, -0.62f), new Vector3(-88f, 8f, 6f), EaseIn),
                K(0.9f, flung, new Vector3(0.04f, LyingDrop + 0.06f, -0.64f), new Vector3(-82f, 8f, 5f), x => Mathf.Sin(Mathf.Clamp01(x) * Mathf.PI * 0.5f)),
                K(1.02f, ground, new Vector3(0.04f, LyingDrop, -0.65f), new Vector3(-88f, 8f, 6f), EaseIn),
                K(ZombieMotion.DeathBackSeconds, still, new Vector3(0.04f, LyingDrop, -0.65f), new Vector3(-89f, 8f, 6f)),
            };

            return Through(keys, t);
        }

        /// <summary>
        /// Down on its knees and over onto its face: a jolt, the legs folding under it, a slump, a fall
        /// forward that gathers speed, a bounce, and still - arms out past its head, face turned aside.
        /// </summary>
        static Pose DeathForward(float t)
        {
            Pose stand = Standing();
            Pose jolt = Start().Bend(-12f).Add("Head Nod Down-Up", 0.32f).Add("Jaw Close", -0.7f)
                .Arm("Left", -0.35f, -0.45f, 0.45f, 0.1f).Arm("Right", -0.3f, -0.5f, 0.4f, 0.1f);
            Pose kneel = Start().Bend(22f).Add("Head Nod Down-Up", -0.35f)
                .Both("{0} Upper Leg Front-Back", -0.12f).Both("{0} Lower Leg Stretch", -0.92f).Both("{0} Foot Up-Down", -0.4f)
                .Arm("Left", -0.55f, -0.12f, 0.9f, -0.3f).Arm("Right", -0.5f, -0.15f, 0.9f, -0.3f).Add("Jaw Close", -0.4f);
            Pose ground = Start().Add("Head Turn Left-Right", -0.6f).Add("Head Nod Down-Up", 0.1f).Add("Jaw Close", -0.5f)
                .Add("Left Upper Leg Front-Back", 0.05f).Add("Left Lower Leg Stretch", -0.1f)
                .Add("Right Upper Leg Front-Back", -0.1f).Add("Right Lower Leg Stretch", -0.3f).Add("Right Upper Leg In-Out", 0.12f)
                .Both("{0} Foot Up-Down", -0.5f)
                .Arm("Left", 0.85f, 0.42f, 0.75f, 0.1f).Arm("Right", 0.2f, 0.45f, 0.6f, 0.1f).Claw("Left", 0.5f).Claw("Right", 0.4f);
            Pose bounce = new Pose(ground).Add("Left Arm Down-Up", 0.15f).Add("Right Lower Leg Stretch", -0.2f).Add("Head Nod Down-Up", 0.15f);
            Pose still = new Pose(ground).Add("Right Arm Down-Up", -0.1f).Add("Head Turn Left-Right", -0.1f).Claw("Left", 0.2f).Add("Jaw Close", -0.1f);

            Key[] keys =
            {
                K(0f, stand, Vector3.zero, Vector3.zero),
                K(0.15f, jolt, new Vector3(0f, 0.01f, -0.04f), new Vector3(-7f, 0f, 0f)),
                K(0.55f, kneel, new Vector3(0f, -0.4f, 0.04f), new Vector3(10f, -3f, -2f)),
                K(0.9f, ground, new Vector3(-0.02f, LyingDrop, 0.6f), new Vector3(87f, -6f, -4f), EaseIn),
                K(1.0f, bounce, new Vector3(-0.02f, LyingDrop + 0.05f, 0.62f), new Vector3(82f, -6f, -4f), x => Mathf.Sin(Mathf.Clamp01(x) * Mathf.PI * 0.5f)),
                K(1.12f, ground, new Vector3(-0.02f, LyingDrop, 0.63f), new Vector3(87f, -6f, -4f), EaseIn),
                K(ZombieMotion.DeathForwardSeconds, still, new Vector3(-0.02f, LyingDrop, 0.63f), new Vector3(88f, -6f, -4f)),
            };

            return Through(keys, t);
        }

        // --- Building ---------------------------------------------------------------------------------

        /// <summary>Samples <paramref name="pose"/> over <paramref name="length"/> seconds into a humanoid clip, made or rewritten in place.
        /// Everything the body does stays in the pose: the agent moves the zombie, not the clip.</summary>
        static AnimationClip Bake(string name, float length, bool loop, Func<float, Pose> pose)
        {
            ContentGenerator.EnsureFolder(ClipFolder);
            string path = $"{ClipFolder}/Zombie_{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool created = clip == null;
            if (created) clip = new AnimationClip { name = "Zombie_" + name };
            else clip.ClearCurves();

            Pose stance = WorkerAnimations.Base;
            int frames = Mathf.Max(2, Mathf.RoundToInt(length * Fps) + 1);
            var curves = new Dictionary<string, Keyframe[]>();
            foreach (string property in stance.Keys) curves[property] = new Keyframe[frames];

            for (int f = 0; f < frames; f++)
            {
                float t = f == frames - 1 ? length : f / Fps;
                Pose sample = pose(t);
                foreach (string property in stance.Keys)
                    curves[property][f] = new Keyframe(t, sample.TryGetValue(property, out float v) ? v : stance[property]);
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
            settings.loopBlendOrientation = true;
            settings.loopBlendPositionY = true;
            settings.loopBlendPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (created) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
            return clip;
        }

        /// <summary>Every clip, made or remade, by name without the "Zombie_" prefix.</summary>
        static Dictionary<string, AnimationClip> BakeAll()
        {
            var walk = new Walk(WorkerAnimations.Clip(WalkSource));
            float idle = ZombieMotion.IdleSeconds;
            float shamble = ZombieMotion.ShambleCycleSeconds * ZombieMotion.CyclesPerLoop;
            float lope = ZombieMotion.LopeCycleSeconds * ZombieMotion.CyclesPerLoop;
            float hit = ZombieMotion.HitSeconds;

            return new Dictionary<string, AnimationClip>
            {
                ["Idle_Swayer"] = Bake("Idle_Swayer", idle, true, IdleSwayer),
                ["Idle_Drooper"] = Bake("Idle_Drooper", idle, true, IdleDrooper),
                ["Shamble_Reacher"] = Bake("Shamble_Reacher", shamble, true, t => Shamble(walk, t, true)),
                ["Shamble_Dragger"] = Bake("Shamble_Dragger", shamble, true, t => Shamble(walk, t, false)),
                ["Lope"] = Bake("Lope", lope, true, t => Lope(walk, t)),
                ["Batter_Slam"] = Bake("Batter_Slam", ZombieMotion.BatterSeconds, true, Slam),
                ["Batter_Claw"] = Bake("Batter_Claw", ZombieMotion.BatterSeconds, true, Rake),
                ["Hit_Front"] = Bake("Hit_Front", hit, false, t => Hit(t, 0)),
                ["Hit_Left"] = Bake("Hit_Left", hit, false, t => Hit(t, -1)),
                ["Hit_Right"] = Bake("Hit_Right", hit, false, t => Hit(t, 1)),
                ["Death_Back"] = Bake("Death_Back", ZombieMotion.DeathBackSeconds, false, DeathBack),
                ["Death_Forward"] = Bake("Death_Forward", ZombieMotion.DeathForwardSeconds, false, DeathForward),
            };
        }

        // --- The controller ---------------------------------------------------------------------------

        /// <summary>Makes every clip and the controller, and puts the controller on both zombie prefabs.</summary>
        public static string Apply()
        {
            Dictionary<string, AnimationClip> clips = BakeAll();
            AvatarMask mask = UpperBodyMask();
            AnimatorController controller = Controller();

            AddFloat(controller, ZombieMotion.MoveParam, 0f);
            AddFloat(controller, ZombieMotion.StyleParam, 0f);
            AddFloat(controller, ZombieMotion.WalkRateParam, 1f);
            AddFloat(controller, ZombieMotion.BatterRateParam, 1f);

            // Base: idle, shamble and lope by speed, in two styles; the deaths, cued from code.
            controller.AddLayer("Base");
            AnimatorStateMachine body = controller.layers[ZombieMotion.BaseLayer].stateMachine;
            AnimatorState locomotion = controller.CreateBlendTreeInController(ZombieMotion.LocomotionState, out BlendTree styles, 0);
            styles.blendType = BlendTreeType.Simple1D;
            styles.blendParameter = ZombieMotion.StyleParam;
            styles.useAutomaticThresholds = false;
            Speeds(styles.CreateBlendTreeChild(0f), clips["Idle_Swayer"], clips["Shamble_Reacher"], clips["Lope"]);
            Speeds(styles.CreateBlendTreeChild(1f), clips["Idle_Drooper"], clips["Shamble_Dragger"], clips["Lope"]);
            locomotion.speedParameterActive = true;
            locomotion.speedParameter = ZombieMotion.WalkRateParam;
            locomotion.iKOnFeet = false;
            body.defaultState = locomotion;

            State(body, ZombieMotion.DeathBackState, clips["Death_Back"]);
            State(body, ZombieMotion.DeathForwardState, clips["Death_Forward"]);

            // Battering: the whole body, as a blow is thrown from the hips; only while it stands at something.
            controller.AddLayer("Action");
            AnimatorStateMachine action = controller.layers[ZombieMotion.ActionLayer].stateMachine;
            action.defaultState = action.AddState(ZombieMotion.EmptyState);
            foreach ((string state, string clip) in new[] { (ZombieMotion.SlamState, "Batter_Slam"), (ZombieMotion.ClawState, "Batter_Claw") })
            {
                AnimatorState batter = State(action, state, clips[clip]);
                batter.speedParameterActive = true;
                batter.speedParameter = ZombieMotion.BatterRateParam;
            }

            // Flinches, added over everything.
            controller.AddLayer("Flinch");
            AnimatorStateMachine flinch = controller.layers[ZombieMotion.FlinchLayer].stateMachine;
            flinch.defaultState = flinch.AddState(ZombieMotion.EmptyState);
            State(flinch, ZombieMotion.HitFrontState, clips["Hit_Front"]);
            State(flinch, ZombieMotion.HitLeftState, clips["Hit_Left"]);
            State(flinch, ZombieMotion.HitRightState, clips["Hit_Right"]);

            AnimatorControllerLayer[] layers = controller.layers;
            layers[ZombieMotion.ActionLayer].defaultWeight = 1f;
            layers[ZombieMotion.ActionLayer].avatarMask = null;
            layers[ZombieMotion.ActionLayer].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[ZombieMotion.FlinchLayer].defaultWeight = 1f;
            layers[ZombieMotion.FlinchLayer].avatarMask = mask;
            layers[ZombieMotion.FlinchLayer].blendingMode = AnimatorLayerBlendingMode.Additive;
            controller.layers = layers;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);

            int rigs = Put(controller);
            return $"[Last Orders] Zombie animator: {clips.Count} clips (2 idles, 2 shambles, a lope, 2 battering loops, 3 flinches, " +
                   $"2 deaths) in {ClipFolder}; {ControllerPath} on {rigs} zombie rigs. Shamble {ZombieMotion.ShambleSpeed:0.00} m/s, " +
                   $"lope {ZombieMotion.LopeSpeed:0.00} m/s.";
        }

        static void Speeds(BlendTree tree, Motion idle, Motion shamble, Motion lope)
        {
            tree.name = "Speed";
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = ZombieMotion.MoveParam;
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(shamble, ZombieMotion.ShambleSpeed);
            tree.AddChild(lope, ZombieMotion.LopeSpeed);
        }

        static AnimatorState State(AnimatorStateMachine machine, string name, Motion motion)
        {
            AnimatorState state = machine.AddState(name);
            state.motion = motion;
            state.iKOnFeet = false;
            state.writeDefaultValues = true;
            return state;
        }

        static void AddFloat(AnimatorController controller, string name, float value)
        {
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = name,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = value,
            });
        }

        /// <summary>The zombie controller, emptied to be filled again: kept in place so whatever uses it keeps it.</summary>
        static AnimatorController Controller()
        {
            ContentGenerator.EnsureFolder(WorkerAnimations.Folder);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            while (controller.layers.Length > 0) controller.RemoveLayer(0);
            while (controller.parameters.Length > 0) controller.RemoveParameter(0);
            return controller;
        }

        /// <summary>The spine, head, arms and fingers: what a flinch moves. Not the legs, which keep walking or standing.</summary>
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
                bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head ||
                             part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm ||
                             part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, upper);
            }

            EditorUtility.SetDirty(mask);
            AssetDatabase.SaveAssetIfDirty(mask);
            return mask;
        }

        /// <summary>Puts <paramref name="controller"/> on every animator in both zombie prefabs. Returns how many.</summary>
        static int Put(RuntimeAnimatorController controller)
        {
            int count = 0;
            foreach (string path in Prefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
                    {
                        count++;
                        if (animator.runtimeAnimatorController == controller) continue;
                        animator.runtimeAnimatorController = controller;
                        changed = true;
                    }

                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return count;
        }

        [MenuItem("Last Orders/Hospital & Zombies/Build Zombie Animations", priority = 62)]
        static void Run() => Debug.Log(Apply());

        [MenuItem("Last Orders/Hospital & Zombies/Restore Zombies' Visitor Animator", priority = 63)]
        static void Restore()
        {
            var visitor = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(VisitorControllerPath);
            if (visitor == null)
            {
                Debug.LogError($"[Last Orders] No visitor controller at {VisitorControllerPath}.");
                return;
            }

            Debug.Log($"[Last Orders] The visitors' walk is back on {Put(visitor)} zombie rigs; ZombieBody bends it into a shamble again.");
        }

        // --- Contact sheets -----------------------------------------------------------------------------

        static readonly Vector3 Stage = new Vector3(0f, 0f, -700f);

        static readonly string[] Rigs =
        {
            "Assets/Game Assets/Models/Characters/Zombie 1.fbx",
            "Assets/Game Assets/Models/Characters/Zombie 2.fbx",
            "Assets/PolygonFarm/Prefabs/Characters/SM_Chr_FarmBoy_01.prefab",
        };

        /// <summary>
        /// A contact sheet of <paramref name="clips"/> (names without "Zombie_") at <paramref name="shares"/>
        /// of their length: a row per clip, a column per moment per rig, a strip of ground under each row,
        /// shot from <paramref name="yaw"/> (180 is face on, 90 from the side). Written to a PNG; nothing is left in the scene.
        /// </summary>
        public static string Sheet(string file, string[] clips, float[] shares, float yaw = 180f, int rig = -1)
        {
            var loaded = new AnimationClip[clips.Length];
            for (int i = 0; i < clips.Length; i++)
                loaded[i] = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/Zombie_{clips[i]}.anim");
            return Sheet(file, loaded, shares, yaw, rig);
        }

        /// <summary>
        /// Test poses for working out which way a muscle turns on these rigs: a row per change, each the
        /// stock stance with only those muscles moved (name=value, several joined by ';').
        /// </summary>
        public static string Probe(string file, string[] changes, float yaw = 90f, int rig = -1)
        {
            var clips = new AnimationClip[changes.Length];
            for (int i = 0; i < changes.Length; i++)
            {
                var pose = Start();
                foreach (string change in changes[i].Split(';'))
                {
                    // name=amount adds, name:value sets; hunch=, sag=, bend=, tip= are the helpers.
                    bool set = change.Contains(":");
                    string[] pair = change.Split(set ? ':' : '=');
                    if (pair.Length != 2) continue;

                    string name = pair[0].Trim();
                    float v = float.Parse(pair[1], System.Globalization.CultureInfo.InvariantCulture);
                    if (name == "hunch") pose.Hunch(v);
                    else if (name == "sag") pose.Sag(v);
                    else if (name == "bend") pose.Bend(v);
                    else if (name == "tip") pose.Tip(v);
                    else if (name == "turnx") pose.Turn(new Vector3(v, 0f, 0f));
                    else if (name == "turnz") pose.Turn(new Vector3(0f, 0f, v));
                    else if (set) pose.Set(name, v);
                    else pose.Add(name, v);
                }

                var clip = new AnimationClip { name = changes[i] };
                foreach (KeyValuePair<string, float> pair in pose)
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), pair.Key), AnimationCurve.Constant(0f, 1f, pair.Value));
                clips[i] = clip;
            }

            return Sheet(file, clips, new[] { 0.5f }, yaw, rig);
        }

        static string Sheet(string file, AnimationClip[] clips, float[] shares, float yaw, int rig)
        {
            var made = new List<GameObject>();
            int columns = shares.Length * (rig < 0 ? Rigs.Length : 1);
            const float across = 1.5f, down = 2.6f;

            try
            {
                for (int row = 0; row < clips.Length; row++)
                {
                    AnimationClip clip = clips[row];
                    if (clip == null) continue;

                    var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ground.hideFlags = HideFlags.DontSave;
                    ground.transform.position = Stage + new Vector3((columns - 1) * across * 0.5f, -row * down - 0.02f, 0f);
                    ground.transform.localScale = new Vector3(columns * across + 1f, 0.04f, 3f);
                    made.Add(ground);

                    int column = 0;
                    for (int r = 0; r < Rigs.Length; r++)
                    {
                        if (rig >= 0 && r != rig) continue;

                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Rigs[r]);
                        foreach (float share in shares)
                        {
                            var go = (GameObject)Object.Instantiate(prefab);
                            go.hideFlags = HideFlags.DontSave;
                            foreach (MonoBehaviour behaviour in go.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;

                            Animator animator = go.GetComponentInChildren<Animator>();
                            if (animator == null)
                            {
                                Object.DestroyImmediate(go);
                                continue;
                            }

                            Vector3 at = Stage + new Vector3(column * across, -row * down, 0f);
                            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
                            clip.SampleAnimation(animator.gameObject, share * clip.length);
                            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));

                            foreach (SkinnedMeshRenderer skin in go.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
                            made.Add(go);
                            column++;
                        }
                    }
                }

                float width = columns * across, height = clips.Length * down;
                var centre = Stage + new Vector3((columns - 1) * across * 0.5f, 1.1f - (clips.Length - 1) * down * 0.5f, 0f);
                return Shoot(file, centre, width, height);
            }
            finally
            {
                foreach (GameObject go in made) Object.DestroyImmediate(go);
            }
        }

        static string Shoot(string file, Vector3 centre, float width, float height)
        {
            var cameraObject = new GameObject("ZombiePreviewCamera") { hideFlags = HideFlags.DontSave };
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.6f, 0.62f);

            int w = 2200, h = Mathf.Clamp(Mathf.RoundToInt(w * height / Mathf.Max(width, 0.1f)), 300, 2400);
            var texture = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            camera.aspect = w / (float)h;
            camera.orthographicSize = Mathf.Max(height * 0.55f, width * 0.55f / camera.aspect);
            camera.transform.SetPositionAndRotation(centre + new Vector3(0f, 2.2f, -20f), Quaternion.Euler(6f, 0f, 0f));
            camera.Render();

            RenderTexture.active = texture;
            var image = new Texture2D(w, h, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            image.Apply();
            RenderTexture.active = null;

            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllBytes(file, image.EncodeToPNG());

            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(cameraObject);
            return file;
        }
    }
}
