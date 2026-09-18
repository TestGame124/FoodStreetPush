using System;
using UnityEngine;

namespace LastOrders.DayCycle
{
    /// <summary>
    /// The game's clock. Pillar 3 — "the clock is the currency" — means every cost in the
    /// build layer is denominated in clock-minutes, so this is the one number the whole
    /// economy is priced against.
    ///
    /// Two distinct times run here and conflating them is the mistake this class exists to
    /// prevent. <see cref="ClockMinutes"/> is in-game time: it runs 0 to 60 across the Day
    /// phase only, and it is what a wall costing "2 minutes" spends. Real seconds are what
    /// Dusk's 45-second warning and Night's 2–4 minutes are measured in, because those
    /// phases are about player reaction time, not about the working day.
    ///
    /// Nothing polls this. Systems subscribe to <see cref="OnClockMinutePassed"/> and to the
    /// state machine's phase events; that is what keeps the arrival curve, Heat accrual, and
    /// build timers from each running their own drifting timer.
    /// </summary>
    public sealed class TimeService
    {
        /// <summary>In-game minutes in one Day phase. §4: "each in-game day is 60 clock-minutes".</summary>
        public const float MinutesPerDay = 60f;

        readonly float realSecondsPerClockMinute;

        /// <summary>Which campaign day this is, 1-based. Drives Named Nights (§16.2) and the
        /// act structure (§18).</summary>
        public int CurrentDay { get; private set; } = 1;

        /// <summary>0 to 60 across the Day phase. Frozen outside it — a system that reads this
        /// during Night gets 60, which is the truthful answer to "how much of the working day
        /// is left".</summary>
        public float ClockMinutes { get; private set; }

        /// <summary>Whole clock-minutes elapsed. Kept separately so the per-minute event fires
        /// exactly once per minute regardless of frame rate.</summary>
        public int ClockMinutesWhole { get; private set; }

        public float ClockMinutesRemaining => Mathf.Max(0f, MinutesPerDay - ClockMinutes);

        /// <summary>0..1 across the working day. What the top-bar arc in §23 draws.</summary>
        public float DayProgress => Mathf.Clamp01(ClockMinutes / MinutesPerDay);

        /// <summary>Real seconds since the current phase was entered. Dusk and Night are
        /// timed against this.</summary>
        public float PhaseElapsedSeconds { get; private set; }

        /// <summary>Set while a modal dawn screen is open. Dawn is paused by design (§4), and
        /// the player may also background the app mid-night.</summary>
        public bool IsPaused { get; set; }

        /// <summary>Fires once for each whole clock-minute boundary crossed, with the minute
        /// just completed. The arrival curve (§9.3) and per-minute Heat both hang off this.</summary>
        public event Action<int> OnClockMinutePassed;

        /// <summary>Fires when the working day runs out, which is what triggers Dusk.</summary>
        public event Action OnWorkingDayEnded;

        public TimeService(float realSecondsPerClockMinute = 6f)
        {
            this.realSecondsPerClockMinute = Mathf.Max(0.01f, realSecondsPerClockMinute);
        }

        /// <summary>Called by the Day state only. Advancing the working clock is that phase's
        /// job and no other phase's, which is why this is not driven from Update directly.</summary>
        public void TickWorkingDay(float deltaSeconds)
        {
            if (IsPaused) return;

            float before = ClockMinutes;
            ClockMinutes = Mathf.Min(MinutesPerDay, ClockMinutes + deltaSeconds / realSecondsPerClockMinute);

            // Loop rather than a single check: a long frame, or a resumed-from-background
            // frame, can cross several minute boundaries at once, and the arrival curve
            // must not lose the parties scheduled in the minutes it skipped.
            int reached = Mathf.FloorToInt(ClockMinutes);
            while (ClockMinutesWhole < reached)
            {
                ClockMinutesWhole++;
                OnClockMinutePassed?.Invoke(ClockMinutesWhole);
            }

            if (before < MinutesPerDay && ClockMinutes >= MinutesPerDay)
                OnWorkingDayEnded?.Invoke();
        }

        /// <summary>Advances the real-time clock the non-Day phases run on.</summary>
        public void TickPhase(float deltaSeconds)
        {
            if (IsPaused) return;
            PhaseElapsedSeconds += deltaSeconds;
        }

        public void ResetPhaseTimer() => PhaseElapsedSeconds = 0f;

        /// <summary>Rewinds the working clock for a new day. Called by Dawn, not by Day, so
        /// that a Day entered from a resumed save does not silently discard elapsed time.</summary>
        public void BeginWorkingDay()
        {
            ClockMinutes = 0f;
            ClockMinutesWhole = 0;
        }

        public void AdvanceDay() => CurrentDay++;

        /// <summary>
        /// Spends clock-minutes on a build action (§6.3). Returns false and spends nothing
        /// when the day cannot cover it — partial spends would let a player start a generator
        /// at minute 58 and get it for two minutes.
        /// </summary>
        public bool TrySpendClockMinutes(float minutes)
        {
            if (minutes <= 0f) return true;
            if (ClockMinutesRemaining < minutes) return false;

            ClockMinutes += minutes;

            int reached = Mathf.FloorToInt(ClockMinutes);
            while (ClockMinutesWhole < reached)
            {
                ClockMinutesWhole++;
                OnClockMinutePassed?.Invoke(ClockMinutesWhole);
            }

            if (ClockMinutes >= MinutesPerDay)
                OnWorkingDayEnded?.Invoke();

            return true;
        }

        public bool CanAfford(float clockMinutes) => ClockMinutesRemaining >= clockMinutes;

        /// <summary>
        /// Gives clock-minutes back. Only ever called by a command being undone (6.3), so
        /// that an undo returns both currencies the build spent.
        ///
        /// Deliberately does not rewind <see cref="ClockMinutesWhole"/> or re-fire the
        /// per-minute event. Those minutes already happened: parties were scheduled against
        /// them and Heat was accrued for them. Un-ringing that bell would spawn the day's
        /// arrivals twice. The player gets their working time back; the world does not
        /// travel backwards.
        /// </summary>
        public void RefundClockMinutes(float minutes)
        {
            if (minutes <= 0f) return;
            ClockMinutes = Mathf.Max(0f, ClockMinutes - minutes);
        }

        /// <summary>Restores the clock from a save without firing the per-minute events —
        /// replaying forty minute-ticks on load would spawn forty parties.</summary>
        public void RestoreFromSave(int day, float clockMinutes)
        {
            CurrentDay = Mathf.Max(1, day);
            ClockMinutes = Mathf.Clamp(clockMinutes, 0f, MinutesPerDay);
            ClockMinutesWhole = Mathf.FloorToInt(ClockMinutes);
            PhaseElapsedSeconds = 0f;
        }
    }
}
