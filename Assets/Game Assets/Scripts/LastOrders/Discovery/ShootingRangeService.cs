using System;
using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Discovery
{
    /// <summary>
    /// The shooting range's rules: a short round against the clock, a knock-down counted for each
    /// cardboard zombie hit while it is up, a medal and a small reward at the end, and a rest
    /// before the next. Daylight only - after dark the machines are wanted on the walls.
    ///
    /// The range in the scene runs the targets and the machine; this only keeps the score and
    /// decides what it was worth.
    /// </summary>
    public sealed class ShootingRangeService
    {
        public static readonly string[] MedalNames = { "Bronze", "Silver", "Gold" };

        readonly ShootingRangeTuning tuning;
        readonly Func<bool> isDaytime;
        readonly Func<bool> isUnlocked;
        readonly Action<int> pay;
        readonly Action<int> addExperience;

        float secondsLeft;
        float restLeft;

        /// <summary>A round started.</summary>
        public event Action OnStarted;

        /// <summary>A round ended: the score, and the medal (-1 for none).</summary>
        public event Action<int, int> OnFinished;

        public ShootingRangeService(ShootingRangeTuning tuning, Func<bool> isDaytime, Func<bool> isUnlocked, Action<int> pay,
                                    Action<int> addExperience)
        {
            this.tuning = tuning ?? new ShootingRangeTuning();
            this.isDaytime = isDaytime;
            this.isUnlocked = isUnlocked;
            this.pay = pay;
            this.addExperience = addExperience;
        }

        public ShootingRangeTuning Tuning => tuning;

        public bool IsRunning { get; private set; }

        public int Score { get; private set; }

        public int BestScore { get; private set; }

        public float SecondsLeft => IsRunning ? Mathf.Max(0f, secondsLeft) : 0f;

        public float RestLeft => Mathf.Max(0f, restLeft);

        public float RoundSeconds => Mathf.Max(1f, tuning.roundSeconds);

        /// <summary>The medal a score earns: 0 bronze, 1 silver, 2 gold, -1 none.</summary>
        public int MedalFor(int score)
        {
            int[] medals = tuning.medals;
            if (medals == null) return -1;

            int best = -1;
            for (int i = 0; i < medals.Length && i < MedalNames.Length; i++)
                if (score >= medals[i]) best = i;
            return best;
        }

        /// <summary>Knock-downs still needed for the next medal, or 0 at gold.</summary>
        public int ToNextMedal(int score)
        {
            int[] medals = tuning.medals;
            if (medals == null) return 0;

            for (int i = 0; i < medals.Length; i++)
                if (score < medals[i]) return medals[i] - score;
            return 0;
        }

        public int CoinsFor(int medal) => medal >= 0 && tuning.coins != null && medal < tuning.coins.Length ? tuning.coins[medal] : 0;

        public int ExperienceFor(int medal) =>
            medal >= 0 && tuning.experience != null && medal < tuning.experience.Length ? tuning.experience[medal] : 0;

        public bool CanStart(out string reason)
        {
            if (isUnlocked != null && !isUnlocked())
            {
                reason = "The shooting range has not been found yet.";
                return false;
            }

            if (IsRunning)
            {
                reason = "A round is already on.";
                return false;
            }

            if (isDaytime != null && !isDaytime())
            {
                reason = "The machines are on the walls after dark. Practise in daylight.";
                return false;
            }

            if (restLeft > 0f)
            {
                reason = $"Resetting the targets: ready in {Mathf.CeilToInt(restLeft)}s.";
                return false;
            }

            reason = null;
            return true;
        }

        public bool TryStart(out string message)
        {
            if (!CanStart(out message)) return false;

            IsRunning = true;
            Score = 0;
            secondsLeft = RoundSeconds;
            OnStarted?.Invoke();

            message = null;
            return true;
        }

        /// <summary>A target went down to a shot. Counted only while a round is on.</summary>
        public void RecordKnockDown()
        {
            if (IsRunning) Score++;
        }

        /// <summary>Ends the round early - dusk falling, the range lost - paying for what was scored so far.</summary>
        public void Stop()
        {
            if (IsRunning) Finish();
        }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            if (!IsRunning)
            {
                if (restLeft > 0f) restLeft -= deltaSeconds;
                return;
            }

            secondsLeft -= deltaSeconds;

            if (isDaytime != null && !isDaytime())
            {
                Finish();
                return;
            }

            if (secondsLeft <= 0f) Finish();
        }

        void Finish()
        {
            IsRunning = false;
            secondsLeft = 0f;
            restLeft = Mathf.Max(0f, tuning.cooldownSeconds);

            int score = Score;
            BestScore = Mathf.Max(BestScore, score);

            int medal = MedalFor(score);
            int coins = CoinsFor(medal);
            int experience = ExperienceFor(medal);

            if (coins > 0) pay?.Invoke(coins);
            if (experience > 0) addExperience?.Invoke(experience);

            OnFinished?.Invoke(score, medal);
        }
    }
}
