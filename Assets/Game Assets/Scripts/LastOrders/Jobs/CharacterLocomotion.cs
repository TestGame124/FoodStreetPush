using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.Jobs
{
    /// <summary>
    /// Feet that keep pace with the ground. Everyone who walks - the crew, the workers, the diners -
    /// tells this how fast they are really going, and it drives their animator's locomotion from that:
    /// the idle, the stock walk and a jog made from it, blended by speed so the stride and the cadence
    /// match the ground covered, rather than one walk cycle played at whatever speed the body slides at.
    ///
    /// Speeds are given to the animator per unit of the rig's human scale, so a short cartoon and a
    /// tall farmer take steps that fit their legs. Controllers built before the "Speed" parameter
    /// get their old 0-1 "Move" blend instead, now at a full walk from the walk's own pace up.
    ///
    /// The paces are measured, not guessed: the stock walk's planted foot moves back at about 1.55 m/s
    /// on a rig of human scale 1; the jog's at <see cref="JogPace"/>.
    /// </summary>
    public static class CharacterLocomotion
    {
        public const string SpeedParam = "Speed";
        public static readonly int SpeedId = Animator.StringToHash(SpeedParam);

        /// <summary>The stock walk's pace, metres a second at human scale 1.</summary>
        public const float WalkPace = 1.55f;

        /// <summary>The jog's pace, metres a second at human scale 1 (measured 3.3-3.5 across the rigs).</summary>
        public const float JogPace = 3.4f;

        /// <summary>Seconds of one jog cycle (two strides).</summary>
        public const float JogCycleSeconds = 0.72f;

        static readonly Dictionary<RuntimeAnimatorController, int> kinds = new Dictionary<RuntimeAnimatorController, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => kinds.Clear();

        /// <summary>
        /// Drives <paramref name="animator"/>'s locomotion for moving at <paramref name="metresPerSecond"/>,
        /// eased over <paramref name="damping"/> seconds so a stop is not a snap.
        /// </summary>
        public static void Drive(Animator animator, float metresPerSecond, float deltaTime, float damping = 0.1f)
        {
            if (animator == null || deltaTime <= 0f) return;

            int kind = Kind(animator);
            if (kind == 0) return;

            float scale = animator.isHuman && animator.humanScale > 0.01f ? animator.humanScale : 1f;
            float pace = Mathf.Max(0f, metresPerSecond) / scale;

            if (kind == 2) animator.SetFloat(SpeedId, pace, damping, deltaTime);
            else animator.SetFloat(WorkerGestures.MoveId, Mathf.Clamp01(pace / WalkPace), damping, deltaTime);
        }

        /// <summary>Puts the locomotion straight at <paramref name="metresPerSecond"/>, with no easing: for a body put down, or pooled.</summary>
        public static void Set(Animator animator, float metresPerSecond)
        {
            if (animator == null) return;

            int kind = Kind(animator);
            float scale = animator.isHuman && animator.humanScale > 0.01f ? animator.humanScale : 1f;
            float pace = Mathf.Max(0f, metresPerSecond) / scale;

            if (kind == 2) animator.SetFloat(SpeedId, pace);
            else if (kind == 1) animator.SetFloat(WorkerGestures.MoveId, Mathf.Clamp01(pace / WalkPace));
        }

        /// <summary>0 nothing to drive, 1 the old 0-1 "Move" blend, 2 the "Speed" blend.</summary>
        static int Kind(Animator animator)
        {
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller == null) return 0;
            if (kinds.TryGetValue(controller, out int kind)) return kind;

            // An animator not running yet reports no parameters: ask again later rather than remember that.
            if (animator.parameterCount == 0) return 0;

            kind = 0;
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == SpeedId && p.type == AnimatorControllerParameterType.Float) kind = 2;
                else if (kind == 0 && p.nameHash == WorkerGestures.MoveId && p.type == AnimatorControllerParameterType.Float) kind = 1;
            }

            kinds[controller] = kind;
            return kind;
        }
    }
}
