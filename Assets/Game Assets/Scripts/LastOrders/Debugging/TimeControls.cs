using System;
using LastOrders.Core;
using LastOrders.DayCycle;
using UnityEngine;

namespace LastOrders.Debugging
{
    /// <summary>How the day cycle is being driven. Normal is the game; the rest are for testing.</summary>
    public enum TimeMode
    {
        /// <summary>The cycle runs itself: dawn, day, dusk, night, resolution, repeat.</summary>
        Normal,

        /// <summary>Held at Day. The working clock is wound back before it can run out, so the
        /// restaurant never closes.</summary>
        AlwaysDay,

        /// <summary>Held at Night. Each siege that ends is followed straight into another.</summary>
        AlwaysNight,
    }

    /// <summary>
    /// Test controls for the day cycle: jump between day and night, or pin it to one of them.
    ///
    /// Worth having as a real component rather than as an inspector fiddle, because almost
    /// everything in this game is gated on the phase. Customers only arrive during the day,
    /// the siege only happens at night, and waiting six real minutes to look at either of
    /// them is how a bug that takes a minute to fix takes an afternoon to see.
    ///
    /// Two rules keep it from being a source of bugs itself:
    ///
    /// It only ever asks, through <see cref="DayCycleMachine.RequestTransition"/>. The machine
    /// defers and serialises transitions, so a request made from a phase event cannot unwind
    /// the event that made it - which is exactly the class of bug a "force it to night" button
    /// would otherwise introduce.
    ///
    /// And night is always entered through dusk. Dusk is what locks Heat and builds the wave,
    /// so a night entered directly is a night with nothing in it - the state handles that
    /// safely and ends immediately, which in AlwaysNight would spin. Going through dusk and
    /// skipping its warning gets a real night in about a frame.
    /// </summary>
    [DefaultExecutionOrder(1190)]
    public sealed class TimeControls : MonoBehaviour
    {
        [SerializeField] GameLifetimeScope scope;

        [Header("Buttons")]
        [Tooltip("Wired in Start rather than by the scene tool. A listener added with " +
                 "AddListener from an editor script is a RUNTIME listener: it is never " +
                 "serialised, so it is gone the moment the scene is saved and reloaded - the " +
                 "buttons look right and do nothing.")]
        [SerializeField] UnityEngine.UI.Button toggleButton;

        [SerializeField] UnityEngine.UI.Button normalButton;
        [SerializeField] UnityEngine.UI.Button alwaysDayButton;
        [SerializeField] UnityEngine.UI.Button alwaysNightButton;

        [Tooltip("Seconds between corrections while a mode is holding the cycle. Throttled so " +
                 "a mode that cannot reach its target phase asks once a second rather than " +
                 "every frame.")]
        [SerializeField] float correctionSeconds = 1f;

        [Tooltip("Clock-minutes wound back when Always Day is about to run out of daylight. " +
                 "Large enough that the rewind is rare, small enough that the clock still " +
                 "visibly moves.")]
        [SerializeField] float alwaysDayRewind = 30f;

        GameComposition composition;
        float nextCorrectionIn;
        bool skipDuskWhenEntered;

        public TimeMode Mode { get; private set; } = TimeMode.Normal;

        /// <summary>Raised when the mode changes, so the buttons can redraw.</summary>
        public event Action<TimeMode> OnModeChanged;

        public DayPhase CurrentPhase =>
            composition != null ? composition.Machine.CurrentPhase : DayPhase.Dawn;

        public bool IsNight => CurrentPhase == DayPhase.Night || CurrentPhase == DayPhase.Dusk;

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                Debug.LogError("[TimeControls] No GameLifetimeScope, so the clock cannot be " +
                               "driven from here.", this);
                enabled = false;
                return;
            }

            composition.Machine.OnPhaseChanged += HandlePhaseChanged;

            Bind(toggleButton, ToggleDayNight);
            Bind(normalButton, SetNormal);
            Bind(alwaysDayButton, SetAlwaysDay);
            Bind(alwaysNightButton, SetAlwaysNight);
        }

        static void Bind(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        void OnDestroy()
        {
            if (composition != null) composition.Machine.OnPhaseChanged -= HandlePhaseChanged;
        }

        // --- The controls -------------------------------------------------------------------

        public void SetMode(TimeMode mode)
        {
            if (Mode == mode) return;

            Mode = mode;
            nextCorrectionIn = 0f;

            OnModeChanged?.Invoke(mode);
            Debug.Log($"[TimeControls] Time mode: {mode}.", this);

            // Apply immediately rather than waiting for the next correction, so pressing the
            // button does something the same frame.
            Correct();
        }

        public void SetNormal() => SetMode(TimeMode.Normal);

        public void SetAlwaysDay() => SetMode(TimeMode.AlwaysDay);

        public void SetAlwaysNight() => SetMode(TimeMode.AlwaysNight);

        /// <summary>
        /// Jumps to whichever of day or night we are not currently in.
        ///
        /// Drops the mode back to Normal first. A toggle that fought a held mode would look
        /// broken - press it, watch the world flip back a second later - and silently losing
        /// the press is worse than changing a setting the player can see.
        /// </summary>
        public void ToggleDayNight()
        {
            if (composition == null) return;

            if (Mode != TimeMode.Normal) SetMode(TimeMode.Normal);

            if (IsNight) GoToDay();
            else GoToNight();
        }

        public void GoToDay()
        {
            if (composition == null) return;

            skipDuskWhenEntered = false;
            composition.Machine.RequestTransition(DayPhase.Day);
        }

        /// <summary>
        /// Goes to night the long way round, through dusk.
        ///
        /// Dusk is what locks Heat and builds the wave. Entering night without it gives an
        /// empty siege that ends on the first tick, which is not night - it is a loading
        /// screen. The warning itself is then skipped, so this costs about a frame.
        /// </summary>
        public void GoToNight()
        {
            if (composition == null) return;

            if (CurrentPhase == DayPhase.Night) return;

            skipDuskWhenEntered = true;
            composition.Machine.RequestTransition(DayPhase.Dusk);
        }

        // --- Holding a mode -------------------------------------------------------------------

        void Update()
        {
            if (composition == null || Mode == TimeMode.Normal) return;

            nextCorrectionIn -= Time.deltaTime;
            if (nextCorrectionIn > 0f) return;

            nextCorrectionIn = correctionSeconds;
            Correct();
        }

        void Correct()
        {
            switch (Mode)
            {
                case TimeMode.AlwaysDay:
                    HoldDay();
                    break;

                case TimeMode.AlwaysNight:
                    HoldNight();
                    break;
            }
        }

        /// <summary>
        /// Keeps it daytime.
        ///
        /// The clock is wound back rather than stopped, because a stopped clock is a different
        /// kind of test: build costs are priced in clock-minutes, and freezing it would make
        /// everything free. Refunding minutes deliberately does not re-fire the per-minute
        /// event, so nothing downstream sees the day happen twice.
        /// </summary>
        void HoldDay()
        {
            if (CurrentPhase != DayPhase.Day)
            {
                GoToDay();
                return;
            }

            if (composition.Time.ClockMinutesRemaining > alwaysDayRewind * 0.2f) return;

            composition.Time.RefundClockMinutes(alwaysDayRewind);
        }

        void HoldNight()
        {
            if (CurrentPhase == DayPhase.Night) return;

            GoToNight();
        }

        void HandlePhaseChanged(DayPhase previous, DayPhase current)
        {
            // Skipping is done on the phase event rather than inside GoToNight, because dusk
            // has to have actually been entered - and its own OnEnter is what built the wave
            // the skip is hurrying along.
            if (current != DayPhase.Dusk || !skipDuskWhenEntered) return;

            skipDuskWhenEntered = false;
            composition.Dusk.SkipWarning();
        }

        public void Configure(GameLifetimeScope lifetimeScope, UnityEngine.UI.Button toggle,
                              UnityEngine.UI.Button normal, UnityEngine.UI.Button day,
                              UnityEngine.UI.Button night)
        {
            scope = lifetimeScope;
            toggleButton = toggle;
            normalButton = normal;
            alwaysDayButton = day;
            alwaysNightButton = night;
        }
    }
}
