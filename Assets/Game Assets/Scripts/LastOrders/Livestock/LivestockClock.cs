using UnityEngine;

namespace LastOrders.Livestock
{
    /// <summary>
    /// What a pen has laid by and when the next one comes: one unit every
    /// <see cref="SecondsPerUnit"/> of working time, kept until collected, never more than
    /// <see cref="Capacity"/>. A full nest box lays nothing more - the hens wait for it to be
    /// emptied - so the clock stops at full rather than counting on toward an egg with nowhere to go.
    ///
    /// Plain C#, so the rule can be tested without a scene; <see cref="LivestockPen"/> decides when
    /// time counts (awake, in the working day, the clock running) and only feeds it seconds.
    /// </summary>
    public sealed class LivestockClock
    {
        float elapsed;

        public LivestockClock(float secondsPerUnit, int capacity)
        {
            SecondsPerUnit = Mathf.Max(0.01f, secondsPerUnit);
            Capacity = Mathf.Max(1, capacity);
        }

        public float SecondsPerUnit { get; }
        public int Capacity { get; }

        /// <summary>Units laid by and waiting to be collected.</summary>
        public int Stored { get; private set; }

        public bool Full => Stored >= Capacity;

        /// <summary>How far through the next unit, 0..1; 1 when full.</summary>
        public float Progress => Full ? 1f : elapsed / SecondsPerUnit;

        /// <summary>Working seconds until the next unit; 0 when full.</summary>
        public float SecondsToNext => Full ? 0f : Mathf.Max(0f, SecondsPerUnit - elapsed);

        /// <summary>
        /// Counts <paramref name="seconds"/> of working time. Returns how many units that made - never
        /// more than <paramref name="most"/>, what the animals have been fed for; the time toward one
        /// more waits, finished, for the next feed.
        /// </summary>
        public int Tick(float seconds, int most = int.MaxValue)
        {
            if (seconds <= 0f || Full || most <= 0) return 0;

            elapsed += seconds;
            int made = 0;
            while (elapsed >= SecondsPerUnit && !Full && made < most)
            {
                elapsed -= SecondsPerUnit;
                Stored++;
                made++;
            }

            // Full stops the clock where it is: the next one starts from nothing once there is room.
            if (Full) elapsed = 0f;
            else elapsed = Mathf.Min(elapsed, SecondsPerUnit);
            return made;
        }

        /// <summary>Empties the store. Returns how many were taken.</summary>
        public int TakeAll()
        {
            int taken = Stored;
            Stored = 0;
            return taken;
        }

        /// <summary>Starts over with <paramref name="stored"/> already laid by and the next one not begun.</summary>
        public void Reset(int stored = 0)
        {
            Stored = Mathf.Clamp(stored, 0, Capacity);
            elapsed = 0f;
        }
    }
}
