using System;
using UnityEngine;

namespace LastOrders.Field
{
    /// <summary>
    /// How much punishment a worker can still take out in the field.
    ///
    /// A plain class, so the rules around it - the warning, the knock-down, the recovery - can
    /// be tested without a body. The worker in the scene owns one and draws it; the field
    /// service listens to it.
    ///
    /// The low-health warning is raised once on the way down, not on every hit below the line:
    /// it is the prompt to recall somebody, and a prompt repeated every second and a half stops
    /// being read. It is armed again only once they have healed clear of the line.
    /// </summary>
    public sealed class WorkerHealth
    {
        /// <summary>How far above the warning line health has to climb before the warning can
        /// be raised again, so a worker healing and being hit on the line does not flicker it.</summary>
        const float WarningRearm = 0.1f;

        bool warned;

        public WorkerHealth(float max, float lowFraction)
        {
            Max = Mathf.Max(1f, max);
            LowFraction = Mathf.Clamp(lowFraction, 0.01f, 0.99f);
            Current = Max;
        }

        public float Max { get; }
        public float Current { get; private set; }

        /// <summary>At or below this share of the maximum, the worker is badly hurt.</summary>
        public float LowFraction { get; }

        public float Fraction => Current / Max;

        /// <summary>Knocked down: out of health, not working, waiting to be carried to a cot.</summary>
        public bool IsDowned { get; private set; }

        /// <summary>Badly hurt but still on their feet.</summary>
        public bool IsLow => !IsDowned && Fraction <= LowFraction;

        public bool IsFull => !IsDowned && Current >= Max;

        /// <summary>A hit landed, and how hard.</summary>
        public event Action<float> OnDamaged;

        /// <summary>Dropped to the warning line. Raised once per trip below it.</summary>
        public event Action OnLow;

        /// <summary>Out of health.</summary>
        public event Action OnDowned;

        /// <summary>Back to full health after being downed - out of the cot.</summary>
        public event Action OnRestored;

        /// <summary>Takes a hit. True when this is the one that knocks them down.</summary>
        public bool Damage(float amount)
        {
            if (IsDowned || amount <= 0f) return false;

            Current = Mathf.Max(0f, Current - amount);
            OnDamaged?.Invoke(amount);

            if (Current <= 0f)
            {
                IsDowned = true;
                warned = false;
                OnDowned?.Invoke();
                return true;
            }

            if (!warned && Fraction <= LowFraction)
            {
                warned = true;
                OnLow?.Invoke();
            }

            return false;
        }

        /// <summary>Recovers a little while resting. Does nothing for someone downed - that
        /// takes a cot, not a sit-down.</summary>
        public void Heal(float amount)
        {
            if (IsDowned || amount <= 0f || Current >= Max) return;

            Current = Mathf.Min(Max, Current + amount);
            if (warned && Fraction > LowFraction + WarningRearm) warned = false;
        }

        /// <summary>Well again: full health and on their feet.</summary>
        public void Restore()
        {
            bool wasDowned = IsDowned;

            IsDowned = false;
            Current = Max;
            warned = false;

            if (wasDowned) OnRestored?.Invoke();
        }
    }
}
