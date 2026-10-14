using UnityEngine;

namespace LastOrders.Night
{
    /// <summary>
    /// The zombies' own animations, as the body and the clip builder both need them: the
    /// controller's parameters, states and layers, how long each clip is, and the speed each walk
    /// was made for. Single source - the builder makes the clips from these numbers, and
    /// <see cref="ZombieBody"/> plays them by the same ones.
    ///
    /// The walks are the stock walk re-timed and bent into a limp, so their stride is a share of
    /// the stock walk's; the walk is played faster or slower to match how fast the zombie really
    /// goes, which keeps the feet from sliding across the ground.
    /// </summary>
    public static class ZombieMotion
    {
        // --- Parameters --------------------------------------------------------------------------------

        /// <summary>Metres per second, damped: 0 stood still, then the speed of the walk to play.</summary>
        public const string MoveParam = "Move";

        /// <summary>0 to 1, picked once per life: the arms-out reacher at 0, the hanging dragger at 1.</summary>
        public const string StyleParam = "Style";

        /// <summary>Playback rate of the walk, so the feet keep pace with the ground.</summary>
        public const string WalkRateParam = "WalkRate";

        /// <summary>Playback rate of the battering, so every blow lands on the beat the agent hits on.</summary>
        public const string BatterRateParam = "BatterRate";

        // --- Layers and states -----------------------------------------------------------------------

        public const int BaseLayer = 0;

        /// <summary>Upper body over the legs: battering.</summary>
        public const int ActionLayer = 1;

        /// <summary>Upper body, added on top of everything: flinching from hits.</summary>
        public const int FlinchLayer = 2;

        public const string LocomotionState = "Locomotion";
        public const string DeathBackState = "Death Back";
        public const string DeathForwardState = "Death Forward";
        public const string EmptyState = "Empty";
        public const string SlamState = "Batter Slam";
        public const string ClawState = "Batter Claw";
        public const string HitFrontState = "Hit Front";
        public const string HitLeftState = "Hit Left";
        public const string HitRightState = "Hit Right";

        // --- The walks ----------------------------------------------------------------------------------

        /// <summary>Metres the stock walk covers in one cycle (two steps), on a rig of human scale 1.</summary>
        public const float StockStrideMetres = 1.75f;

        /// <summary>Seconds for one cycle of the shamble - two steps, the bad leg quicker than the good.</summary>
        public const float ShambleCycleSeconds = 1.4f;

        /// <summary>The shamble's stride as a share of the stock walk's.</summary>
        public const float ShambleStride = 0.78f;

        /// <summary>Seconds for one cycle of the lope, the fast ones' lurching run.</summary>
        public const float LopeCycleSeconds = 0.78f;

        public const float LopeStride = 1.05f;

        /// <summary>Every walk loop holds two cycles, so they blend foot for foot and the second can stumble.</summary>
        public const int CyclesPerLoop = 2;

        /// <summary>Seconds each idle loop runs before it repeats.</summary>
        public const float IdleSeconds = 6f;

        public static float ShambleSpeed => StockStrideMetres * ShambleStride / ShambleCycleSeconds;

        public static float LopeSpeed => StockStrideMetres * LopeStride / LopeCycleSeconds;

        /// <summary>Slowest and fastest the walks are played, so a crawl still steps and a sprint still reads.</summary>
        public const float MinWalkRate = 0.42f;
        public const float MaxWalkRate = 1.6f;

        /// <summary>
        /// The blend parameter for a zombie moving at <paramref name="speed"/> m/s on a rig of
        /// <paramref name="humanScale"/>: 0 stood still, otherwise the speed in the walks' own
        /// metres, held inside the range between the shamble and the lope.
        /// </summary>
        public static float Move(float speed, float humanScale)
        {
            if (speed <= 0.08f) return 0f;
            return Mathf.Clamp(speed / Mathf.Max(0.1f, humanScale), ShambleSpeed, LopeSpeed);
        }

        /// <summary>How fast to play the walk blended at <paramref name="move"/> for the feet to keep pace.</summary>
        public static float WalkRate(float speed, float move, float humanScale)
        {
            if (move <= 0f || speed <= 0.08f) return 1f;
            return Mathf.Clamp(speed / Mathf.Max(0.1f, move * humanScale), MinWalkRate, MaxWalkRate);
        }

        // --- Battering ----------------------------------------------------------------------------------

        /// <summary>Seconds of one blow in the battering loop: the arms up, down on it, and back up.</summary>
        public const float BatterSeconds = 1f;

        /// <summary>Where in the loop the blow lands, 0 to 1.</summary>
        public const float ImpactPhase = 0.5f;

        /// <summary>
        /// The rate that brings the next blow down on the next beat: <paramref name="phase"/> is
        /// where the loop is now (0 to 1) as a blow is struck, <paramref name="interval"/> the
        /// seconds until the next one. A loop ahead of the beat slows down, one behind speeds up.
        /// </summary>
        public static float BatterRate(float phase, float interval)
        {
            float behind = Mathf.Repeat(ImpactPhase - phase + 0.5f, 1f) - 0.5f;
            float cycles = 1f + Mathf.Clamp(behind, -0.3f, 0.3f);
            return Mathf.Clamp(cycles * BatterSeconds / Mathf.Max(0.3f, interval), 0.4f, 2.5f);
        }

        // --- Hits and deaths ----------------------------------------------------------------------------

        /// <summary>Seconds of a flinch.</summary>
        public const float HitSeconds = 0.55f;

        public const float DeathBackSeconds = 1.5f;
        public const float DeathForwardSeconds = 1.6f;

        /// <summary>Seconds from the killing blow until the body lies still on the ground.</summary>
        public const float LyingAfterSeconds = 1.2f;
    }
}
