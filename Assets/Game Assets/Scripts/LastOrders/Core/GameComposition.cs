using LastOrders.Build;
using LastOrders.Consequence;
using LastOrders.Customers;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.DayCycle.States;
using LastOrders.Dispatch;
using LastOrders.Economy;
using LastOrders.Heat;
using LastOrders.Night;
using LastOrders.Perimeter;
using LastOrders.Reputation;
using LastOrders.Survivors;
using UnityEngine;

namespace LastOrders.Core
{
    /// <summary>
    /// Builds the whole object graph, in dependency order, in one readable place.
    ///
    /// Kept as a plain class with no DI-framework types in it. VContainer is the registered
    /// container per 24.1, but the actual construction order - which service needs which -
    /// is game knowledge rather than framework knowledge, and it is far easier to read and
    /// to debug as a sequence of constructor calls than as a set of registrations whose
    /// order the container decides.
    ///
    /// It also means a package resolution problem with VContainer costs one thin adapter
    /// file rather than the ability to start the game.
    /// </summary>
    public sealed class GameComposition
    {
        public readonly GameBalanceConfig Balance;

        public readonly TimeService Time;
        public readonly HeatService Heat;
        public readonly ReputationService Reputation;
        public readonly ResourceLedger Ledger;
        public readonly PerimeterService Perimeter;
        public readonly CustomerDirector Customers;
        public readonly WaveDirector Waves;
        public readonly ThreatProfileService ThreatProfile;
        public readonly CrisisBarService CrisisBar;
        public readonly SurvivorService Survivors;
        public readonly DispatchService Dispatch;
        public readonly GhostRegistry Ghosts;
        public readonly PoolService Pools;
        public readonly BuildContext BuildContext;
        public readonly BuildCommandStack Commands;

        public readonly DayCycleMachine Machine;
        public readonly DayCycleContext Context;

        public readonly DawnState Dawn;
        public readonly DayState Day;
        public readonly DuskState Dusk;
        public readonly NightState Night;
        public readonly ResolutionState Resolution;

        public GameComposition(GameBalanceConfig balance, Transform poolRoot, GridBuildingSystem3D grid)
        {
            Balance = balance;

            // Leaf services first: nothing here depends on anything else in the graph.
            Time = new TimeService(balance.realSecondsPerClockMinute);
            Heat = new HeatService(balance.heatWeights);
            Reputation = new ReputationService(balance);
            Ledger = new ResourceLedger();
            Ghosts = new GhostRegistry();
            Perimeter = new PerimeterService();
            Pools = new PoolService(poolRoot);

            // Then the ones that compose them.
            Customers = new CustomerDirector(balance, Reputation);
            Waves = new WaveDirector(balance, balance.heatWeights);
            ThreatProfile = new ThreatProfileService(Waves, balance);
            CrisisBar = new CrisisBarService(balance, Ledger);
            Survivors = new SurvivorService(balance, Ledger);
            Dispatch = new DispatchService(balance, Ledger, Reputation, Survivors);

            BuildContext = new BuildContext(Ledger, Time, grid);
            Commands = new BuildCommandStack(BuildContext, balance.undoWindowSeconds);

            Machine = new DayCycleMachine(Time);

            Context = new DayCycleContext(
                balance, Time, Heat, Reputation, Ledger, Perimeter, Customers, Waves,
                ThreatProfile, CrisisBar, Survivors, Dispatch, Ghosts, Commands,
                BuildContext, Pools);

            // States last: they need the context, which needs everything else.
            Dawn = new DawnState(Context);
            Day = new DayState(Context, Machine);
            Dusk = new DuskState(Context, Machine);
            Night = new NightState(Context, Machine);
            Resolution = new ResolutionState(Context, Machine);

            Machine.AddState(Dawn);
            Machine.AddState(Day);
            Machine.AddState(Dusk);
            Machine.AddState(Night);
            Machine.AddState(Resolution);

            WireCrossCuttingEvents();
        }

        /// <summary>
        /// The subscriptions that would otherwise be scattered across the systems they
        /// connect. Gathered here so that "what reacts to what" is answerable by reading one
        /// method rather than by grepping for every subscription in the project.
        /// </summary>
        void WireCrossCuttingEvents()
        {
            // The build layer is the only thing that changes the shape of the restaurant, so
            // it is the only thing that needs to invalidate the perimeter and the nav seeds.
            Commands.OnCommandExecuted += _ => RefreshSpatialState();
            Commands.OnCommandUndone += _ => RefreshSpatialState();
        }

        void RefreshSpatialState()
        {
            Perimeter.Recompute();
            Perimeter.SeedNavigationFields();
        }

        /// <summary>Starts the cycle. Separate from construction so every listener can
        /// subscribe before the first Dawn fires - otherwise the game misses the event that
        /// starts it.</summary>
        public void Start(DayPhase phase = DayPhase.Dawn) => Machine.Start(phase);

        public void Tick(float deltaSeconds) => Machine.Tick(deltaSeconds);
    }
}
