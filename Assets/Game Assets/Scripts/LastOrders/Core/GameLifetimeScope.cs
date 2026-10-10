using LastOrders.Build;
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
    ///
    /// Runs late on purpose. In a scene that still has GameHandler, that component's Start
    /// initialises the build grid, loads the placed objects and bootstraps navigation, and
    /// the Last Orders layer reads all three. Default order would leave which Start runs
    /// first up to Unity, and the symptom of losing that race is silent: a perimeter with no
    /// walls in it and a Heat footprint of zero.
    /// </summary>
    [DefaultExecutionOrder(1000)]
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

        [Tooltip("Parent for pooled world-space UI. Created automatically when left empty.")]
        [SerializeField] Transform worldUiRoot;

        [Tooltip("Parent for pooled particle effects. Created automatically when left empty.")]
        [SerializeField] Transform effectsRoot;

        [Tooltip("Parent for the popup canvas. Created automatically when left empty.")]
        [SerializeField] Transform popupRoot;

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

            // Separate from Pools on purpose: dawn recalls everything under Pools, and the world
            // UI and the effects each have their own pool that must survive it.
            worldUiRoot = EnsureRoot(worldUiRoot, "WorldUI");
            effectsRoot = EnsureRoot(effectsRoot, "Effects");
            popupRoot = EnsureRoot(popupRoot, "Popups");

            if (balance.worldUi == null)
            {
                Debug.LogWarning("[GameLifetimeScope] GameBalance has no World UI catalog, so debris " +
                                 "prompts and work timers cannot be shown. Run 'Last Orders / " +
                                 "Generate Starting Content'.", this);
            }

            composition = new GameComposition(balance, poolRoot, worldUiRoot, effectsRoot, popupRoot, buildGrid);

            builder.RegisterInstance(composition);
            builder.RegisterInstance(balance);

            builder.RegisterInstance(composition.Time);
            builder.RegisterInstance(composition.Heat);
            builder.RegisterInstance(composition.Reputation);
            builder.RegisterInstance(composition.Ledger);
            builder.RegisterInstance(composition.Perimeter);
            builder.RegisterInstance(composition.Waves);
            builder.RegisterInstance(composition.ThreatProfile);
            builder.RegisterInstance(composition.CrisisBar);
            builder.RegisterInstance(composition.Survivors);
            builder.RegisterInstance(composition.Dispatch);
            builder.RegisterInstance(composition.Meals);
            builder.RegisterInstance(composition.Incidents);
            builder.RegisterInstance(composition.Pools);
            builder.RegisterInstance(composition.Commands);
            builder.RegisterInstance(composition.Jobs);
            builder.RegisterInstance(composition.Progression);
            builder.RegisterInstance(composition.Rewards);
            builder.RegisterInstance(composition.Expansion);
            builder.RegisterInstance(composition.Clearing);
            builder.RegisterInstance(composition.WorldUi);
            builder.RegisterInstance(composition.Effects);
            builder.RegisterInstance(composition.Popups);
            builder.RegisterInstance(composition.Defense);
            builder.RegisterInstance(composition.Hospital);
            builder.RegisterInstance(composition.Danger);
            builder.RegisterInstance(composition.Field);
            builder.RegisterInstance(composition.Compound);
            builder.RegisterInstance(composition.Repairs);
            builder.RegisterInstance(composition.Restaurant);
            builder.RegisterInstance(composition.Outdoor);
            builder.RegisterInstance(composition.Encounters);
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

        Transform EnsureRoot(Transform existing, string name)
        {
            if (existing != null) return existing;

            var host = new GameObject(name);
            host.transform.SetParent(transform, false);
            return host.transform;
        }
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
            // Idempotent, and in a scene that still has GameHandler it will already have been
            // done. Calling it anyway means the Last Orders layer works in a scene with the
            // old bootstrap and in one without it, rather than silently depending on which
            // scene it was dropped into.
            if (GridBuildingSystem3D.GetGrid() != null)
                FoodStreet.Navigation.NavigationSystem.Bootstrap();

            // Nothing is persisted, so every launch is a new campaign.
            composition.SeedNewGame();
            composition.Start();
        }

        public void Tick() => composition.Tick(Time.deltaTime);
    }
}
