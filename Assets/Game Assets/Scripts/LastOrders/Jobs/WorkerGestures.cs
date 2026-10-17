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

        // After a meal, at the table. Arms only on the visitors' controller, so they play sat down.
        PatBelly = 13,
        ThumbsUp = 14,
        WipeMouth = 15,
        ChefKiss = 16,
        Clap = 17,
    }

    /// <summary>
    /// The character animators' contract: parameter names, layer names, work styles, and how long each
    /// gesture runs. The clip builders make the clips these lengths and <see cref="GestureDriver"/>
    /// waits them out, so the two cannot drift apart. The workers' controller and the visitors' both
    /// keep it, so one gesture means the same on anyone.
    /// </summary>
    public static class WorkerGestures
    {
        public const string GestureParam = "Gesture";
        public const string ActParam = "Act";
        public const string WorkingParam = "Working";
        public const string WorkStyleParam = "WorkStyle";
        public const string MoveParam = "Move";

        /// <summary>The upper-body layer gestures and work loops play on.</summary>
        public const string LayerName = "Gestures";

        /// <summary>The arms-only layer that holds a plate up in both hands.</summary>
        public const string CarryLayerName = "Carry";

        public static readonly int GestureId = Animator.StringToHash(GestureParam);
        public static readonly int ActId = Animator.StringToHash(ActParam);
        public static readonly int WorkingId = Animator.StringToHash(WorkingParam);
        public static readonly int WorkStyleId = Animator.StringToHash(WorkStyleParam);
        public static readonly int MoveId = Animator.StringToHash(MoveParam);

        /// <summary>Hands busy in front at a counter: chopping, stirring, sorting.</summary>
        public const int WorkHands = 0;

        /// <summary>A tool swung overhead and down: clearing, breaking, building.</summary>
        public const int WorkSwing = 1;

        /// <summary>Leaning over a surface with a cloth, the hand going round in circles.</summary>
        public const int WorkWipe = 2;

        /// <summary>A broom in both hands, swept side to side across the floor.</summary>
        public const int WorkSweep = 3;

        /// <summary>A duster flicked over something at chest height.</summary>
        public const int WorkDust = 4;

        /// <summary>One swing of <see cref="WorkSwing"/>: the tool swing's default rate, 1.6 a second.</summary>
        public const float SwingSeconds = 0.625f;

        public const float WorkHandsSeconds = 1.2f;

        /// <summary>One circle of the cloth.</summary>
        public const float WipeSeconds = 1.4f;

        /// <summary>One stroke of the broom and back.</summary>
        public const float SweepSeconds = 1.3f;

        /// <summary>Two flicks of the duster.</summary>
        public const float DustSeconds = 0.9f;

        /// <summary>The seconds one loop of a work style takes.</summary>
        public static float WorkSeconds(int style)
        {
            switch (style)
            {
                case WorkSwing: return SwingSeconds;
                case WorkWipe: return WipeSeconds;
                case WorkSweep: return SweepSeconds;
                case WorkDust: return DustSeconds;
                default: return WorkHandsSeconds;
            }
        }

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
                case Gesture.PatBelly: return 2.4f;
                case Gesture.ThumbsUp: return 1.8f;
                case Gesture.WipeMouth: return 2f;
                case Gesture.ChefKiss: return 1.8f;
                case Gesture.Clap: return 2f;
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
