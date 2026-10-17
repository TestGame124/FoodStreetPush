using System;
using System.Collections.Generic;
using LastOrders.Consequence;
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
    /// and they live on this state's own boundaries rather than scattered across the systems
    /// that own the objects.
    ///
    /// Three things happen and their order matters:
    ///
    /// New arrivals stop and anyone not yet seated turns round and heads home. The visitor
    /// director does this off the phase change; it is a rule about restraint rather than a
    /// resolution, and nobody pays Reputation for a restaurant that closed on time.
    ///
    /// Heat locks, so the bar the player spent all day watching is the bar the wave is
    /// built from.
    ///
    /// The wave is generated and its lanes telegraphed, so the player gets the whole 45
    /// seconds to reposition against information rather than against a surprise.
    ///
    /// Last Call runs at the END of the phase rather than the start of it. Dusk is the
    /// preparation window, and a party seated at minute 59 finishes their meal and walks out
    /// inside it - a meal is fifteen seconds against a forty-five second warning. Resolving
    /// everyone the instant the shutters start closing would mark those customers as having
    /// fled from a restaurant they were about to leave happy. Only the people still at a
    /// table when night actually falls are the civilians 9.8 is about, and Heat is locked
    /// before this runs either way, so nothing downstream sees a different world for it.
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

        public void OnEnter(DayPhase previous)
        {
            // An attack's warning is the attack cycle's preparation countdown - shorter before a
            // nightly attack than before a big wave; the old timed night keeps its own.
            remaining = context.Encounters != null
                ? context.Encounters.PreparationSeconds(context.Time.CurrentDay)
                : context.Balance.duskWarningSeconds;

            CloseAllDoors();

            // Any customer still inside is a civilian during the siege. Lights still burning
            // are Heat, and they are burning because the player chose to keep serving - so
            // this charge lands before the lock, not after.
            context.Heat.AddPoweredLights(CountPoweredLights());

            float locked = context.Heat.LockForNight();
            context.Report.HeatAtDusk = locked;

            // Tonight is now decided and cannot be influenced further: the attack's difficulty
            // is locked the moment its warning appears.
            context.TonightsWave = context.Encounters != null
                ? context.Encounters.Lock(context.Time.CurrentDay)
                : context.Waves.BuildPlan(context.Time.CurrentDay, locked);

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

        public void OnExit(DayPhase next)
        {
            // Last Call used to sweep every customer in the scene here and pay it
            // out. Visitors own their own departure now - they drive home when they are fed
            // and are never written off by the clock - so there is nothing left to resolve.
        }

        /// <summary>Lets the player skip the remaining warning once they are satisfied with
        /// their positioning. Confident players should not be made to wait.</summary>
        public void SkipWarning() => remaining = 0f;

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
