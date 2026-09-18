using System;
using System.Collections.Generic;
using LastOrders.Consequence;
using LastOrders.Customers;
using LastOrders.Night;
using LastOrders.Perimeter;
using UnityEngine;

namespace LastOrders.DayCycle.States
{
    /// <summary>
    /// Forty-five real seconds between the restaurant and the siege.
    ///
    /// This is the most important stretch of the game and by far the most dangerous state to
    /// get wrong, because it is where every in-flight thing has to be resolved at once:
    /// meals half-eaten, doors mid-admission, parties still queuing outside the walls. Those
    /// are exactly the transition bugs an explicit state machine exists to give one home to,
    /// and they all live in <see cref="OnEnter"/> here rather than scattered across the
    /// systems that own the objects.
    ///
    /// Three things happen and their order matters:
    ///
    /// Last Call resolves every customer, because a party still eating when the wave spawns
    /// is a civilian the player has to physically manage (9.8).
    ///
    /// Heat locks, so the bar the player spent all day watching is the bar the wave is
    /// built from.
    ///
    /// The wave is generated and its lanes telegraphed, so the player gets the whole 45
    /// seconds to reposition against information rather than against a surprise.
    /// </summary>
    public sealed class DuskState : IDayPhaseState
    {
        readonly DayCycleContext context;
        readonly DayCycleMachine machine;

        float remaining;

        public DuskState(DayCycleContext context, DayCycleMachine machine)
        {
            this.context = context;
            this.machine = machine;
        }

        public DayPhase Phase => DayPhase.Dusk;

        /// <summary>Seconds left in the warning, for the countdown and the shutter animation.</summary>
        public float SecondsRemaining => Mathf.Max(0f, remaining);

        /// <summary>Raised with tonight's plan as soon as it exists, so approach arrows can
        /// be drawn while the player can still act on them.</summary>
        public event Action<WavePlan> OnWaveTelegraphed;

        /// <summary>Raised per customer resolved at Last Call, so the UI can show where each
        /// one went rather than having them silently vanish.</summary>
        public event Action<CustomerIdentity, VisitOutcome> OnCustomerResolved;

        public void OnEnter(DayPhase previous)
        {
            remaining = context.Balance.duskWarningSeconds;

            // 1. Last Call. Everything in flight is resolved before anything else reads the
            //    world, so no later step can see a half-finished meal.
            ResolveLastCall();
            CloseAllDoors();

            // 2. Any customer still inside is a civilian during the siege. Lights still
            //    burning are Heat, and they are burning because the player chose to keep
            //    serving - so this charge lands before the lock, not after.
            context.Heat.AddPoweredLights(CountPoweredLights());

            float locked = context.Heat.LockForNight();
            context.Report.HeatAtDusk = locked;

            // 3. Tonight is now decided and cannot be influenced further.
            context.TonightsWave = context.Waves.BuildPlan(context.Time.CurrentDay, locked);
            OnWaveTelegraphed?.Invoke(context.TonightsWave);

            context.CrisisBar.BeginNight();
            BeginNightOnStructures();

            // Building locks. Repositioning mobile assets and placing pre-purchased
            // consumables is all that is left.
            context.Commands.ExpireUndoWindow();
        }

        public void OnTick(float deltaSeconds)
        {
            remaining -= deltaSeconds;
            if (remaining <= 0f) machine.RequestTransition(DayPhase.Night);
        }

        public void OnExit(DayPhase next) { }

        /// <summary>Lets the player skip the remaining warning once they are satisfied with
        /// their positioning. Confident players should not be made to wait.</summary>
        public void SkipWarning() => remaining = 0f;

        /// <summary>
        /// 9.8. Resolves every customer still present.
        ///
        /// The cellar is the whole design here: with one, a caught party shelters safely and
        /// pays in full at dawn. Without one they panic and become an Evacuate problem. This
        /// is what makes the cellar one of the most valuable buildables in the game, and what
        /// turns the player's greed at minute 50 into their problem at minute 62.
        /// </summary>
        void ResolveLastCall()
        {
            bool hasCellar = HasCellar();

            var customers = UnityEngine.Object.FindObjectsByType<CustomerIdentity>(
                FindObjectsSortMode.None);

            for (int i = 0; i < customers.Length; i++)
            {
                CustomerIdentity customer = customers[i];
                if (customer == null) continue;

                bool finished = customer.HasBeenServed;
                VisitOutcome outcome = customer.ResolveAtDusk(hasCellar, finished);

                int payment = customer.PaymentFor(outcome);
                if (payment > 0)
                {
                    context.Ledger.Add(Data.ResourceType.Coin, payment);
                    context.Report.CoinEarned += payment;
                }

                float reputationDelta = customer.ReputationFor(outcome);
                if (!Mathf.Approximately(reputationDelta, 0f))
                {
                    context.Reputation.Add(reputationDelta);
                    context.Report.ReputationDelta += reputationDelta;
                }

                OnCustomerResolved?.Invoke(customer, outcome);
            }
        }

        void CloseAllDoors()
        {
            var doors = UnityEngine.Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None);
            for (int i = 0; i < doors.Length; i++) doors[i].ForceClose();
        }

        /// <summary>Resets the per-night near-miss measurement on every structure, so 15.7's
        /// margin is measured for tonight rather than accumulated across the campaign.</summary>
        void BeginNightOnStructures()
        {
            var structures = UnityEngine.Object.FindObjectsByType<DamageableStructure>(
                FindObjectsSortMode.None);

            for (int i = 0; i < structures.Length; i++) structures[i].BeginNight();
        }

        static bool HasCellar() => AnyBuildableWhere(type => type.isCellar);

        static int CountPoweredLights()
        {
            int count = 0;
            ForEachBuildable(type =>
            {
                if (type.isPoweredLight) count++;
            });
            return count;
        }

        static bool AnyBuildableWhere(Func<PlacedObjectTypeSO, bool> predicate)
        {
            bool found = false;
            ForEachBuildable(type =>
            {
                if (!found && predicate(type)) found = true;
            });
            return found;
        }

        static void ForEachBuildable(Action<PlacedObjectTypeSO> action)
        {
            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();
            if (grid == null) return;

            var counted = new HashSet<PlacedObject_Done>();
            int width = grid.GetWidth();
            int height = grid.GetHeight();

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    PlacedObject_Done occupant = grid.GetGridObject(x, z)?.GetPlacedObject();
                    if (occupant == null || !counted.Add(occupant)) continue;

                    PlacedObjectTypeSO type = occupant.PlacedObjectTypeSO;
                    if (type != null) action(type);
                }
            }
        }
    }
}
