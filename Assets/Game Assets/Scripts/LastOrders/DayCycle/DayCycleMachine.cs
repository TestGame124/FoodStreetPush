using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.DayCycle
{
    /// <summary>
    /// The spine. Dawn to Day to Dusk to Night to Resolution and back to Dawn, as an
    /// explicit machine with enter and exit hooks.
    ///
    /// Why this rather than a phase enum and scattered branching: the bugs in a day/night
    /// game do not live inside the phases, they live on the boundaries. A meal in progress
    /// when Last Call fires. A build half-paid-for at dusk. A save written while a wave is
    /// mid-spawn. Each of those is one system's assumption meeting another system's timing,
    /// and the only durable fix is for there to be exactly one place where "the phase
    /// changed" is expressed. That place is here.
    ///
    /// Two rules make it trustworthy:
    ///
    /// Transitions are deferred, never re-entrant. A state that requests a transition from
    /// inside OnEnter, or a subscriber that requests one from inside a phase-changed event,
    /// does not get a nested transition. The request is queued and runs after the current
    /// one completes. Without this, Dusk entering Night from inside its own OnEnter would
    /// unwind into OnExit being called for a phase that had not finished being entered.
    ///
    /// Exit always precedes enter, and both always run. Even a forced restore from a save
    /// pays the exit of whatever it was in, so no system is left believing the restaurant
    /// is still open.
    /// </summary>
    public sealed class DayCycleMachine
    {
        readonly Dictionary<DayPhase, IDayPhaseState> states = new Dictionary<DayPhase, IDayPhaseState>();
        readonly TimeService time;

        DayPhase pending;
        bool hasPending;
        bool transitioning;

        public DayPhase CurrentPhase { get; private set; } = DayPhase.Dawn;

        public IDayPhaseState CurrentState { get; private set; }

        public bool IsRunning { get; private set; }

        /// <summary>Raised after the new state's OnEnter. Subscribers see a fully entered
        /// phase, never a half-built one. Arguments are (previous, current).</summary>
        public event Action<DayPhase, DayPhase> OnPhaseChanged;

        /// <summary>Raised before the outgoing state's OnExit, so a subscriber can still read
        /// the world as it was during that phase. The Resolution tally and the Last Call
        /// sweep both need that.</summary>
        public event Action<DayPhase, DayPhase> OnPhaseExiting;

        public DayCycleMachine(TimeService time)
        {
            this.time = time ?? throw new ArgumentNullException(nameof(time));
        }

        public void AddState(IDayPhaseState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            states[state.Phase] = state;
        }

        /// <summary>
        /// Enters the first phase. Separate from construction so every system can subscribe
        /// to <see cref="OnPhaseChanged"/> before the first Dawn fires. Otherwise the whole
        /// game misses the event that starts it.
        /// </summary>
        public void Start(DayPhase startPhase = DayPhase.Dawn)
        {
            if (IsRunning) return;
            IsRunning = true;

            CurrentPhase = startPhase;
            CurrentState = Find(startPhase);
            CurrentState?.OnEnter(startPhase);
            time.ResetPhaseTimer();

            OnPhaseChanged?.Invoke(startPhase, startPhase);
            DrainPending();
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsRunning) return;

            time.TickPhase(deltaSeconds);
            CurrentState?.OnTick(deltaSeconds);

            // Drained after the tick rather than before, so a state that requests a
            // transition partway through its own OnTick still finishes that tick.
            DrainPending();
        }

        /// <summary>
        /// Requests a move to <paramref name="next"/>. Safe to call from anywhere, including
        /// from inside a phase event. The request is queued and applied at the next safe
        /// point rather than executed underneath the caller.
        /// </summary>
        public void RequestTransition(DayPhase next)
        {
            if (!IsRunning)
            {
                Debug.LogWarning($"[DayCycleMachine] Transition to {next} requested before Start().");
                return;
            }

            if (hasPending && pending != next)
            {
                // Two different transitions queued in one frame means two systems each
                // believe they own the phase change. Last writer wins, but it is worth
                // saying so out loud: this is a design bug, not a timing quirk.
                Debug.LogWarning(
                    $"[DayCycleMachine] {pending} was already queued when {next} was requested. Using {next}.");
            }

            pending = next;
            hasPending = true;

            if (!transitioning) DrainPending();
        }

        /// <summary>Convenience for the common case, where each phase hands to the next.</summary>
        public void RequestNext() => RequestTransition(NextInCycle(CurrentPhase));

        public static DayPhase NextInCycle(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Dawn: return DayPhase.Day;
                case DayPhase.Day: return DayPhase.Dusk;
                case DayPhase.Dusk: return DayPhase.Night;
                case DayPhase.Night: return DayPhase.Resolution;
                case DayPhase.Resolution: return DayPhase.Dawn;
                default: return DayPhase.Dawn;
            }
        }

        void DrainPending()
        {
            if (transitioning) return;

            transitioning = true;
            try
            {
                // A loop, because a state's OnEnter may legitimately decide it is already
                // finished. Resolution with nothing to report goes straight on to Dawn.
                int guard = 0;
                while (hasPending)
                {
                    DayPhase next = pending;
                    hasPending = false;

                    if (++guard > 8)
                    {
                        Debug.LogError(
                            "[DayCycleMachine] Transition loop did not settle after 8 hops. " +
                            $"Stopping at {CurrentPhase}. A state is re-requesting from OnEnter.");
                        break;
                    }

                    Apply(next);
                }
            }
            finally
            {
                transitioning = false;
            }
        }

        void Apply(DayPhase next)
        {
            DayPhase previous = CurrentPhase;

            OnPhaseExiting?.Invoke(previous, next);
            CurrentState?.OnExit(next);

            CurrentPhase = next;
            CurrentState = Find(next);
            time.ResetPhaseTimer();

            CurrentState?.OnEnter(previous);
            OnPhaseChanged?.Invoke(previous, next);
        }

        /// <summary>
        /// Drops straight into a phase, paying exit and enter. For restoring a save: a file
        /// written mid-night must come back up in Night, not walk forward through Dawn and
        /// Day to get there.
        /// </summary>
        public void ForceTo(DayPhase phase)
        {
            if (!IsRunning)
            {
                Start(phase);
                return;
            }

            hasPending = false;
            Apply(phase);
        }

        public void Stop()
        {
            if (!IsRunning) return;

            CurrentState?.OnExit(CurrentPhase);
            IsRunning = false;
            hasPending = false;
        }

        IDayPhaseState Find(DayPhase phase)
        {
            if (states.TryGetValue(phase, out IDayPhaseState state)) return state;

            Debug.LogError($"[DayCycleMachine] No state registered for {phase}.");
            return null;
        }
    }
}
