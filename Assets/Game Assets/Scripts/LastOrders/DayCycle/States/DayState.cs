using LastOrders.Customers;
using UnityEngine;

namespace LastOrders.DayCycle.States
{
    /// <summary>
    /// The working day. Sixty clock-minutes that have to cover building, repairing, prepping
    /// and serving, and that do not pause for indecision.
    ///
    /// This state owns the clock - it is the only phase that advances
    /// <see cref="TimeService.TickWorkingDay"/> - and it releases arrivals from the schedule
    /// the director built at dawn. Everything else during the day is other systems reacting
    /// to the minute tick, not to this class.
    /// </summary>
    public sealed class DayState : IDayPhaseState
    {
        readonly DayCycleContext context;
        readonly DayCycleMachine machine;

        public DayState(DayCycleContext context, DayCycleMachine machine)
        {
            this.context = context;
            this.machine = machine;
        }

        public DayPhase Phase => DayPhase.Day;

        /// <summary>Raised when a party reaches the road tile, for the spawner to build them.</summary>
        public event System.Action<ScheduledParty> OnPartyArrived;

        public void OnEnter(DayPhase previous)
        {
            context.Time.OnClockMinutePassed += HandleMinutePassed;
            context.Time.OnWorkingDayEnded += HandleWorkingDayEnded;
            context.Customers.OnPartyArrived += HandlePartyArrived;
        }

        public void OnTick(float deltaSeconds)
        {
            // The one line that makes the day a day. Everything else in the game is
            // downstream of this call.
            context.Time.TickWorkingDay(deltaSeconds);
        }

        public void OnExit(DayPhase next)
        {
            // Unsubscribed in OnExit rather than relying on the object being discarded,
            // because the state instance is reused every single day. A missed unsubscribe
            // here would have the day fifty state handling forty-nine duplicate minute ticks.
            context.Time.OnClockMinutePassed -= HandleMinutePassed;
            context.Time.OnWorkingDayEnded -= HandleWorkingDayEnded;
            context.Customers.OnPartyArrived -= HandlePartyArrived;

            // A placement made during the day cannot be undone after the shutters drop: by
            // then the player has seen what the layout was worth.
            context.Commands.ExpireUndoWindow();
        }

        void HandleMinutePassed(int minute)
        {
            // Closed for Repairs means no service at all, but the clock still runs and the
            // build bonus still applies - the player traded the day, they did not skip it.
            if (!context.ClosedForRepairs)
                context.Customers.ReleaseDue(minute);
        }

        void HandlePartyArrived(ScheduledParty party) => OnPartyArrived?.Invoke(party);

        void HandleWorkingDayEnded()
        {
            // Requested rather than applied. The machine defers it to a safe point, so this
            // can fire from inside a minute tick without unwinding the tick.
            machine.RequestTransition(DayPhase.Dusk);
        }

        /// <summary>
        /// 11.2. Closing in the last fifteen clock-minutes cuts Heat at the cost of the
        /// day's peak revenue - the dinner rush and the dangerous tail are both in that
        /// window, so this is never a free choice.
        /// </summary>
        public bool TryCloseEarly()
        {
            if (context.Time.ClockMinutesRemaining > 15f) return false;

            context.Heat.ApplyCloseEarly();
            machine.RequestTransition(DayPhase.Dusk);
            return true;
        }

        /// <summary>
        /// 11.2 and open question 28.3. Refusing a party at the door is the cleanest Heat
        /// control in the game and the most morally interesting decision in it. Worth
        /// instrumenting carefully - if it becomes dominant, the Heat economy is wrong.
        /// </summary>
        public void RefuseAtDoor(CustomerIdentity customer)
        {
            context.Heat.ApplyRefusedCustomer();
            context.Reputation.Add(customer != null
                ? customer.ReputationFor(VisitOutcome.RefusedAtDoor)
                : -1f);

            context.Report.CustomersTurnedAway++;
        }
    }
}
