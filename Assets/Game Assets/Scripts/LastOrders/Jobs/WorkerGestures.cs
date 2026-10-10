using UnityEngine;

namespace LastOrders.Jobs
{
    /// <summary>What a worker can do with their hands while there is nothing else to do with them.</summary>
    public enum Gesture
    {
        None = 0,

        /// <summary>Held until told otherwise.</summary>
        HandsOnHips = 1,

        /// <summary>Held until told otherwise.</summary>
        ArmsCrossed = 2,

        ScratchHead = 3,
        Stretch = 4,
        WipeBrow = 5,
        Wave = 6,
        TalkA = 7,
        TalkB = 8,
        Shrug = 9,
        Point = 10,
        Laugh = 11,
        CheckHand = 12,
    }

    /// <summary>
    /// The worker animator's contract: its parameter names, and how long each gesture runs. The clip
    /// builder makes the clips these lengths and <see cref="WorkerLife"/> waits them out, so the two
    /// cannot drift apart.
    /// </summary>
    public static class WorkerGestures
    {
        public const string GestureParam = "Gesture";
        public const string ActParam = "Act";
        public const string WorkingParam = "Working";
        public const string WorkStyleParam = "WorkStyle";

        public static readonly int GestureId = Animator.StringToHash(GestureParam);
        public static readonly int ActId = Animator.StringToHash(ActParam);
        public static readonly int WorkStyleId = Animator.StringToHash(WorkStyleParam);

        /// <summary>Hands busy in front at a counter: chopping, stirring, sorting.</summary>
        public const int WorkHands = 0;

        /// <summary>A tool swung overhead and down: clearing, breaking, building.</summary>
        public const int WorkSwing = 1;

        /// <summary>One swing of <see cref="WorkSwing"/>: the tool swing's default rate, 1.6 a second.</summary>
        public const float SwingSeconds = 0.625f;

        public const float WorkHandsSeconds = 1.2f;

        /// <summary>Held gestures loop on this, breathing, until let go.</summary>
        public const float HoldSeconds = 4f;

        public static bool IsHeld(Gesture gesture) => gesture == Gesture.HandsOnHips || gesture == Gesture.ArmsCrossed;

        /// <summary>Seconds a one-shot gesture takes; a held one's loop.</summary>
        public static float Seconds(Gesture gesture)
        {
            switch (gesture)
            {
                case Gesture.ScratchHead: return 2.6f;
                case Gesture.Stretch: return 3.4f;
                case Gesture.WipeBrow: return 2f;
                case Gesture.Wave: return 2.2f;
                case Gesture.TalkA: return 2.4f;
                case Gesture.TalkB: return 2.4f;
                case Gesture.Shrug: return 1.6f;
                case Gesture.Point: return 2.2f;
                case Gesture.Laugh: return 2f;
                case Gesture.CheckHand: return 2.4f;
                case Gesture.None: return 0f;
                default: return HoldSeconds;
            }
        }

        /// <summary>True if <paramref name="animator"/> has the gesture parameters: the worker controller.</summary>
        public static bool CanGesture(Animator animator)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter.nameHash == GestureId) return true;
            return false;
        }
    }
}
