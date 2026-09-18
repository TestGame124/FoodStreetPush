using LastOrders.Build;
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
using VContainer;
using VContainer.Unity;

namespace LastOrders.Core
{
    /// <summary>
    /// The VContainer composition root (24.1).
    ///
    /// Thin on purpose. <see cref="GameComposition"/> already builds the object graph in
    /// explicit dependency order, so this registers the finished instances rather than
    /// teaching the container how to construct them. Two reasons:
    ///
    /// The construction order is game knowledge - Heat needs the weights asset, the states
    /// need the context, the context needs everything - and that reads far better as a
    /// sequence of constructor calls than as a set of registrations.
    ///
    /// And it keeps the framework at the edge. Every system in this game is a plain class
    /// that could be newed up in a test with no container present.
    /// </summary>
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [Header("Content")]
        [Tooltip("The master balance asset. Everything tunable hangs off it.")]
        [SerializeField] GameBalanceConfig balance;

        [Header("Scene")]
        [Tooltip("The existing build grid. Must be initialised before this scope builds.")]
        [SerializeField] GridBuildingSystem3D buildGrid;

        [Tooltip("Parent for pooled instances. Created automatically when left empty.")]
        [SerializeField] Transform poolRoot;

        GameComposition composition;

        protected override void Configure(IContainerBuilder builder)
        {
            if (balance == null)
            {
                Debug.LogError("[GameLifetimeScope] No GameBalanceConfig assigned. " +
                               "The game cannot start without its content registry.", this);
                return;
            }

            if (poolRoot == null)
            {
                var host = new GameObject("Pools");
                host.transform.SetParent(transform, false);
                poolRoot = host.transform;
            }

            composition = new GameComposition(balance, poolRoot, buildGrid);

            builder.RegisterInstance(composition);
            builder.RegisterInstance(balance);

            builder.RegisterInstance(composition.Time);
            builder.RegisterInstance(composition.Heat);
            builder.RegisterInstance(composition.Reputation);
            builder.RegisterInstance(composition.Ledger);
            builder.RegisterInstance(composition.Perimeter);
            builder.RegisterInstance(composition.Customers);
            builder.RegisterInstance(composition.Waves);
            builder.RegisterInstance(composition.ThreatProfile);
            builder.RegisterInstance(composition.CrisisBar);
            builder.RegisterInstance(composition.Survivors);
            builder.RegisterInstance(composition.Dispatch);
            builder.RegisterInstance(composition.Pools);
            builder.RegisterInstance(composition.Commands);
            builder.RegisterInstance(composition.Machine);
            builder.RegisterInstance(composition.Context);

            // States are registered so UI can talk to them directly - the dusk countdown
            // binds to DuskState, the Crisis Bar to NightState - without either having to
            // reach through the machine.
            builder.RegisterInstance(composition.Dawn);
            builder.RegisterInstance(composition.Day);
            builder.RegisterInstance(composition.Dusk);
            builder.RegisterInstance(composition.Night);
            builder.RegisterInstance(composition.Resolution);

            builder.RegisterEntryPoint<GameLoop>();
        }

        public GameComposition Composition => composition;
    }

    /// <summary>
    /// Drives the machine. The only Update in the entire system layer.
    ///
    /// Everything else subscribes to phase changes and to the clock's minute tick, which is
    /// what keeps a dozen systems from each running their own drifting timer against the
    /// same day.
    /// </summary>
    public sealed class GameLoop : IStartable, ITickable
    {
        readonly GameComposition composition;

        [Inject]
        public GameLoop(GameComposition composition)
        {
            this.composition = composition;
        }

        public void Start()
        {
            // The navigation layer mirrors the build grid's dimensions and must exist before
            // anything is placed, or the first placements go unheard.
            FoodStreet.Navigation.NavigationSystem.Bootstrap();

            // Load before seeding, so a returning player is never handed a second starting
            // kit on top of what they already earned.
            FoodStreet.Saving.SaveSystem.Initialize();

            var participant = new Save.LastOrdersSaveParticipant(composition);
            FoodStreet.Saving.SaveSystem.Register(participant);

            bool isNewCampaign = FoodStreet.Saving.SaveSystem.StartedFresh;

            if (isNewCampaign)
            {
                composition.SeedNewGame();
                composition.Start();
                return;
            }

            // RestoreState drops the machine straight into the saved phase, including mid
            // night, so Start would be a second entry into a phase already entered.
            participant.RestoreState(FoodStreet.Saving.SaveSystem.Data);

            if (!composition.Machine.IsRunning)
                composition.Start();
        }

        public void Tick() => composition.Tick(Time.deltaTime);
    }
}
