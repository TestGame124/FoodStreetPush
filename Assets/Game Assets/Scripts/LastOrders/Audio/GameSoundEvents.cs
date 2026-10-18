using System;
using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.DayCycle;
using UnityEngine;

namespace LastOrders.Audio
{
    /// <summary>
    /// The sounds of the game's own events: the dusk alarm, the horde arriving, a wall coming down,
    /// a discovery found, the truck setting off.
    ///
    /// Gathered in one place for the reason <see cref="GameComposition"/> gathers its cross-cutting
    /// subscriptions: "what makes a sound when" is then answered by reading one method, and the
    /// services stay knowing nothing about audio. Only moments a service already announces live
    /// here; a moment that belongs to one object in the scene - a zombie hit, a pot boiling - is
    /// sounded by that object, or by an add-on on it.
    ///
    /// Everything is unhooked on destroy, so a reloaded scene does not hear the last one's events.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameSoundEvents : MonoBehaviour
    {
        [SerializeField] GameLifetimeScope scope;

        [Tooltip("Seconds left of the dusk warning from which each second ticks.")]
        [SerializeField, Range(0, 15)] int duskTickFrom = 5;

        readonly List<Action> unbind = new List<Action>();

        GameComposition composition;
        SoundHandle noiseMaker;
        int lastDuskTick = -1;

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(GameLifetimeScope lifetimeScope) => scope = lifetimeScope;

        void Start()
        {
            // The legacy orders board predates the composition and runs without it.
            Bind<Action>(h => OrdersBoardUI.OnOrderClaimed += h, h => OrdersBoardUI.OnOrderClaimed -= h,
                         () => SoundController.Play(SoundId.UiOrderDelivered));

            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;
            if (composition != null) BindGame(composition);
        }

        void OnDestroy()
        {
            foreach (Action undo in unbind) undo();
            unbind.Clear();
            noiseMaker.Stop();
        }

        void Update()
        {
            if (composition == null || composition.Machine.CurrentPhase != DayPhase.Dusk)
            {
                lastDuskTick = -1;
                return;
            }

            // The last seconds of the warning count down out loud, one tick a second.
            int second = Mathf.CeilToInt(composition.Dusk.SecondsRemaining);
            if (second <= 0 || second > duskTickFrom || second == lastDuskTick) return;

            lastDuskTick = second;
            SoundController.Play(SoundId.DuskTick, second <= 2 ? 1f : 0.75f);
        }

        void BindGame(GameComposition game)
        {
            // --- The day ---------------------------------------------------------------------------

            Bind<Action<DayPhase, DayPhase>>(h => game.Machine.OnPhaseChanged += h, h => game.Machine.OnPhaseChanged -= h,
                                             HandlePhaseChanged);

            Bind<Action<LastOrders.Night.WaveGroup>>(h => game.Night.OnGroupIncoming += h, h => game.Night.OnGroupIncoming -= h,
                                                     _ => SoundController.Play(SoundId.GroupIncoming));

            Bind<Action<int, int, LastOrders.Night.EncounterWave>>(h => game.Encounters.OnWaveStarted += h,
                                                                   h => game.Encounters.OnWaveStarted -= h,
                                                                   (_, __, ___) => SoundController.Play(SoundId.WaveStart));

            Bind<Action>(h => game.Encounters.OnDefeated += h, h => game.Encounters.OnDefeated -= h,
                         () => SoundController.Play(SoundId.AttackDefeat));

            // A defeat has already been sounded as it happened; the morning only cheers a win.
            Bind<Action<LastOrders.Night.EncounterResult>>(h => game.Encounters.OnResolved += h, h => game.Encounters.OnResolved -= h,
                                                           result => { if (result != null && result.IsVictory) SoundController.Play(SoundId.AttackVictory); });

            Bind<Action>(h => game.Compound.OnBreached += h, h => game.Compound.OnBreached -= h,
                         () => SoundController.Play(SoundId.Breach));

            Bind<Action>(h => game.Danger.OnDoorReached += h, h => game.Danger.OnDoorReached -= h,
                         () => SoundController.Play(SoundId.ZombiesAtDoor));

            Bind<Action<string>>(h => game.Incidents.OnBegan += h, h => game.Incidents.OnBegan -= h,
                                 _ => SoundController.Play(SoundId.IncidentBegan));

            Bind<Action<LastOrders.Data.CrisisVerb, Vector3>>(h => game.CrisisBar.OnAbilityUsed += h, h => game.CrisisBar.OnAbilityUsed -= h,
                                                               (_, at) => SoundController.Play(SoundId.CrisisAbility, at));

            // --- What breaks, and what is put back -------------------------------------------------

            Bind<Action<LastOrders.Perimeter.ICompoundStructure>>(h => game.Compound.OnStructureDown += h,
                                                                  h => game.Compound.OnStructureDown -= h,
                                                                  part => PlayAt(SoundId.StructureBreak, part as Component));

            Bind<Action<LastOrders.Consequence.OutdoorWreck>>(h => game.Outdoor.OnWrecked += h, h => game.Outdoor.OnWrecked -= h,
                                                              wreck => PlayAt(SoundId.StructureBreak, wreck));

            Bind<Action<LastOrders.Night.IDefenseObjective>>(h => game.Horde.OnDefenseKnockedOut += h, h => game.Horde.OnDefenseKnockedOut -= h,
                                                             _ => SoundController.Play(SoundId.DefenseWrecked));

            Bind<Action<LastOrders.Repair.IRepairable>>(h => game.Repairs.OnRepaired += h, h => game.Repairs.OnRepaired -= h,
                                                        HandleRepaired);

            Bind<Action<int>>(h => game.Outdoor.OnRepaired += h, h => game.Outdoor.OnRepaired -= h,
                              count => { if (count > 0) SoundController.Play(SoundId.RepairAll); });

            Bind<Action<int>>(h => game.Compound.OnRepaired += h, h => game.Compound.OnRepaired -= h,
                              count => { if (count > 0) SoundController.Play(SoundId.RepairAll); });

            // --- What is built ---------------------------------------------------------------------

            Bind<Action<LastOrders.Defense.DefenseUnit>>(h => game.Defense.OnBuilt += h, h => game.Defense.OnBuilt -= h,
                                                         unit => SoundController.Play(SoundId.DefenseBuilt, unit.Position));

            Bind<Action<LastOrders.Defense.DefenseUnit>>(h => game.Defense.OnUpgraded += h, h => game.Defense.OnUpgraded -= h,
                                                         unit => SoundController.Play(SoundId.DefenseUpgraded, unit.Position));

            Bind<Action<LastOrders.Defense.GroundDefenseUnit>>(h => game.GroundDefenses.OnBuilt += h, h => game.GroundDefenses.OnBuilt -= h,
                                                               unit => SoundController.Play(SoundId.BuildDone, unit.Position));

            Bind<Action<LastOrders.Housing.ShelterUnit>>(h => game.Shelters.OnBuilt += h, h => game.Shelters.OnBuilt -= h,
                                                         unit => SoundController.Play(SoundId.BuildDone, unit.Position));

            Bind<Action<LastOrders.Housing.ShelterUnit, LastOrders.Survivors.Survivor>>(h => game.Shelters.OnHired += h, h => game.Shelters.OnHired -= h,
                                                                                         (unit, _) => SoundController.Play(SoundId.CrewCheer, unit.Position));

            Bind<Action<string>>(h => game.Expansion.OnAreaClaimed += h, h => game.Expansion.OnAreaClaimed -= h,
                                 _ => SoundController.Play(SoundId.ExpansionClaimed));

            // --- The people ------------------------------------------------------------------------

            Bind<Action<LastOrders.Field.IFieldWorker>>(h => game.Field.OnDowned += h, h => game.Field.OnDowned -= h,
                                                        worker => SoundController.Play(SoundId.WorkerDowned, worker.Position));

            // --- The discovery sites ---------------------------------------------------------------

            Bind<Action<LastOrders.Data.DiscoverySiteDefinition, bool>>(h => game.Discovery.OnDiscovered += h, h => game.Discovery.OnDiscovered -= h,
                                                                        (_, justFound) => { if (justFound) SoundController.Play(SoundId.Discovery); });

            Bind<Action<LastOrders.Discovery.TakeawayOrder>>(h => game.Takeaway.OnArriving += h, h => game.Takeaway.OnArriving -= h,
                                                            _ => SoundController.Play(SoundId.TakeawayArriving));

            Bind<Action<LastOrders.Discovery.TakeawayOrder, bool>>(h => game.Takeaway.OnLeaving += h, h => game.Takeaway.OnLeaving -= h,
                                                                  (_, served) => SoundController.Play(served ? SoundId.TakeawayServed : SoundId.TakeawayGaveUp));

            Bind<Action<LastOrders.Data.FoodTruckDestination>>(h => game.FoodTruck.OnSent += h, h => game.FoodTruck.OnSent -= h,
                                                               _ => SoundController.Play(SoundId.FoodTruckSent));

            Bind<Action<LastOrders.Data.FoodTruckDestination, int, int>>(h => game.FoodTruck.OnReturned += h, h => game.FoodTruck.OnReturned -= h,
                                                                         (_, __, ___) => SoundController.Play(SoundId.FoodTruckReturned));

            Bind<Action>(h => game.Range.OnStarted += h, h => game.Range.OnStarted -= h,
                         () => SoundController.Play(SoundId.RangeStart));

            Bind<Action<int, int>>(h => game.Range.OnFinished += h, h => game.Range.OnFinished -= h,
                                   (_, medal) => SoundController.Play(medal >= 0 ? SoundId.RangeMedal : SoundId.RangeNoMedal));

            Bind<Action>(h => game.NoiseMaker.OnSounded += h, h => game.NoiseMaker.OnSounded -= h, HandleNoiseMakerSounded);
            Bind<Action<bool>>(h => game.NoiseMaker.OnSilenced += h, h => game.NoiseMaker.OnSilenced -= h, HandleNoiseMakerSilenced);
        }

        void HandlePhaseChanged(DayPhase previous, DayPhase current)
        {
            // Raised once as the cycle starts, from a phase to itself: that is the scene opening, not news.
            if (previous == current) return;

            switch (current)
            {
                case DayPhase.Dawn: SoundController.Play(SoundId.DawnStinger); break;
                case DayPhase.Day: SoundController.Play(SoundId.DayStinger); break;
                case DayPhase.Dusk: SoundController.Play(SoundId.DuskWarning); break;
                case DayPhase.Night: SoundController.Play(SoundId.NightStinger); break;
            }
        }

        void HandleRepaired(LastOrders.Repair.IRepairable repaired)
        {
            if (repaired == null) return;

            Transform target = repaired.RepairTarget;
            SoundController.Play(SoundId.RepairDone, target != null ? target.position : repaired.RepairPoint);
        }

        void HandleNoiseMakerSounded()
        {
            noiseMaker.Stop();
            noiseMaker = SoundController.Play(SoundId.NoiseMakerWail, composition.NoiseMaker.Position);
        }

        void HandleNoiseMakerSilenced(bool brokenByZombies)
        {
            noiseMaker.Stop(0.6f);
            if (brokenByZombies) SoundController.Play(SoundId.StructureBreak, composition.NoiseMaker.Position);
        }

        static void PlayAt(SoundId id, Component where)
        {
            if (where != null) SoundController.Play(id, where.transform.position);
            else SoundController.Play(id);
        }

        /// <summary>Subscribes now, and remembers how to unsubscribe on destroy.</summary>
        void Bind<T>(Action<T> add, Action<T> remove, T handler) where T : Delegate
        {
            add(handler);
            unbind.Add(() => remove(handler));
        }
    }
}
