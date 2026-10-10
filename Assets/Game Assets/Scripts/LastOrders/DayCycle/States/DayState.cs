using UnityEngine;

namespace LastOrders.DayCycle.States
{
    /// <summary>
    /// The working day. Sixty clock-minutes that have to cover building, repairing, prepping
    /// and serving, and that do not pause for indecision.
    ///
    /// This state owns the clock - it is the only phase that advances
    /// <see cref="TimeService.TickWorkingDay"/>. Everything else during the day is other
    /// systems reacting to the minute tick, not to this class.
    ///
    /// Arrival TIMING deliberately does not live here. The visitor director pulls the next
    /// planned party when the restaurant has food and a seat for them, rather than the clock
    /// pushing parties at a diner that may have neither; releasing on the curve into a
    /// one-table restaurant produced a queue of people who timed out at the door, which is a
    /// loss the player was given no way to avoid. What the day's schedule still decides is
    /// who arrives and how many the day holds, so Reputation remains what makes the clientele
    /// visibly improve.
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

        public void OnEnter(DayPhase previous)
        {
            context.Time.OnWorkingDayEnded += HandleWorkingDayEnded;
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
            context.Time.OnWorkingDayEnded -= HandleWorkingDayEnded;

            // A placement made during the day cannot be undone after the shutters drop: by
            // then the player has seen what the layout was worth.
            context.Commands.ExpireUndoWindow();
        }

        void HandleWorkingDayEnded()
        {
            // Requested rather than applied. The machine defers it to a safe point, so this
            // can fire from inside a minute tick without unwinding the tick.
            machine.RequestTransition(EveningAfter(context.Time.CurrentDay));
        }

        /// <summary>
        /// Where the day goes when it runs out. An attack day goes to the warning and the night
        /// after it; any other day ends quietly, straight into the morning's tally. Attacks come
        /// every few in-game days, and in-game days only pass while the game is played.
        /// </summary>
        DayPhase EveningAfter(int day) =>
            context.Encounters == null || context.Encounters.IsAttackDay(day) ? DayPhase.Dusk : DayPhase.Resolution;

        /// <summary>
        /// 11.2. Closing in the last fifteen clock-minutes cuts Heat at the cost of the
        /// day's peak revenue - the dinner rush and the dangerous tail are both in that
        /// window, so this is never a free choice.
        /// </summary>
        public bool TryCloseEarly()
        {
            if (context.Time.ClockMinutesRemaining > 15f) return false;

            context.Heat.ApplyCloseEarly();
            machine.RequestTransition(EveningAfter(context.Time.CurrentDay));
            return true;
        }

        /// <summary>
        /// 11.2 and open question 28.3. Refusing a party at the door is the cleanest Heat
        /// control in the game and the most morally interesting decision in it. Worth
        /// instrumenting carefully - if it becomes dominant, the Heat economy is wrong.
        ///
        /// Takes the Reputation cost as a number rather than a customer. Who is at the door
        /// belongs to FoodStreet.Visitors now, and this phase has no business knowing the
        /// shape of them - it only needs to know what turning one away is worth.
        /// </summary>
        public void RefuseAtDoor(float reputationCost = 1f)
        {
            context.Heat.ApplyRefusedCustomer();
            context.Reputation.Add(-Mathf.Abs(reputationCost));

            context.Report.CustomersTurnedAway++;
        }
    }
}
