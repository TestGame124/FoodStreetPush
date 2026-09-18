namespace LastOrders.DayCycle
{
    /// <summary>
    /// One phase of the cycle. Deliberately a real object with enter and exit hooks rather
    /// than a case in a switch, because the interesting code in this game is not what each
    /// phase does — it is what happens on the boundaries between them.
    ///
    /// The contract: <see cref="OnEnter"/> and <see cref="OnExit"/> are called exactly once
    /// per visit and always in pairs, even when the machine is forced to a phase out of
    /// order by a save restore. Neither may request another transition; see
    /// <see cref="DayCycleMachine"/> for why.
    /// </summary>
    public interface IDayPhaseState
    {
        DayPhase Phase { get; }

        /// <param name="previous">The phase being left. Equals <see cref="Phase"/> only on
        /// the very first entry, where there is no previous phase.</param>
        void OnEnter(DayPhase previous);

        /// <summary>Per-frame while this phase is current. <paramref name="deltaSeconds"/> is
        /// real time; a phase that wants clock-minutes asks <see cref="TimeService"/>.</summary>
        void OnTick(float deltaSeconds);

        /// <param name="next">The phase being entered. Lets a state vary its teardown — Day
        /// closes the kitchen differently going to Dusk than going to a forced Dawn.</param>
        void OnExit(DayPhase next);
    }
}
