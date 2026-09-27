using System.Collections.Generic;
using LastOrders.Consequence;
using LastOrders.Data;
using UnityEngine;

namespace LastOrders.DayCycle.States
{
    /// <summary>
    /// The tally. Counts what the night cost, pays wages, advances everyone's counters, and
    /// hands to the next Dawn.
    ///
    /// The whole of 15 comes together here, and the rule it enforces is absolute: a bad
    /// night costs the player time and never costs them progress. Nothing in this state
    /// removes a recipe, a blueprint, a plot tier, a layout or a person. The worst it does
    /// is convert structures into ghost outlines the player pays to restore, injure someone
    /// for a few days, and take a capped slice of the stores.
    /// </summary>
    public sealed class ResolutionState : IDayPhaseState
    {
        readonly DayCycleContext context;
        readonly DayCycleMachine machine;

        public ResolutionState(DayCycleContext context, DayCycleMachine machine)
        {
            this.context = context;
            this.machine = machine;
        }

        public DayPhase Phase => DayPhase.Resolution;

        /// <summary>Raised with the completed report, for the dawn damage screen.</summary>
        public event System.Action<DayReport> OnResolved;

        public void OnEnter(DayPhase previous)
        {
            TallyStructures();
            ExtinguishFires();

            context.Survivors.PayDailyWages();

            float passive = context.Survivors.PassiveReputationPerDay();
            if (!Mathf.Approximately(passive, 0f))
            {
                context.Reputation.Add(passive);
                context.Report.ReputationDelta += passive;
            }

            ApplyDecay();

            context.Survivors.AdvanceDay();
            context.Dispatch.AdvanceDay();

            context.Time.AdvanceDay();

            OnResolved?.Invoke(context.Report);

            // Requested rather than applied directly. The machine defers it, so a subscriber
            // to OnResolved that opens a modal is not fighting a phase change underneath it.
            machine.RequestTransition(DayPhase.Dawn);
        }

        public void OnTick(float deltaSeconds) { }

        public void OnExit(DayPhase next) { }

        /// <summary>
        /// Walks every structure, records its state, and converts the destroyed ones into
        /// ghost outlines.
        ///
        /// Also captures the closest call of the night. 15.7 is the most important tuning
        /// instruction in the document - tune waves so the common outcome is <em>almost</em>,
        /// because a wall at 4 HP when dawn breaks is the night players tell their friends
        /// about, and it costs them nothing. That is only tunable if it is measured.
        /// </summary>
        void TallyStructures()
        {
            var structures = Object.FindObjectsByType<DamageableStructure>(FindObjectsSortMode.None);

            float closest = 1f;
            var destroyed = new List<DamageableStructure>();

            for (int i = 0; i < structures.Length; i++)
            {
                DamageableStructure structure = structures[i];
                if (structure == null) continue;

                var placed = structure.GetComponent<PlacedObject_Done>();
                PlacedObjectTypeSO type = placed != null ? placed.PlacedObjectTypeSO : null;

                if (type != null && (type.isWall || type.isDoor || type.isPerimeter))
                {
                    float margin = structure.LowestHealthTonight / Mathf.Max(1f, structure.MaxHealth);
                    closest = Mathf.Min(closest, margin);
                }

                switch (structure.State)
                {
                    case DamageState.Damaged:
                        context.Report.StructuresDamaged++;
                        break;
                    case DamageState.Wrecked:
                        context.Report.StructuresWrecked++;
                        break;
                    case DamageState.Destroyed:
                        context.Report.StructuresDestroyed++;
                        destroyed.Add(structure);
                        break;
                }
            }

            context.Report.ClosestCall = closest;
            context.Report.WasBadNight = BadNight();

            ConvertToGhosts(destroyed);
        }

        /// <summary>
        /// Replaces destroyed structures with ghost outlines.
        ///
        /// Outlines, not rubble. The tile is left completely free, so the player can rebuild
        /// with one tap at 40 percent or build something else entirely. They never re-design
        /// their restaurant, they pay to restore it - which eliminates the tedium that
        /// "losing progress" actually feels like in practice.
        /// </summary>
        void ConvertToGhosts(List<DamageableStructure> destroyed)
        {
            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();
            if (grid == null) return;

            for (int i = 0; i < destroyed.Count; i++)
            {
                var placed = destroyed[i].GetComponent<PlacedObject_Done>();
                if (placed == null) continue;

                List<Vector2Int> tiles = placed.GetGridPositionList();
                if (tiles.Count == 0) continue;

                context.Ghosts.Add(placed.PlacedObjectTypeSO, tiles[0], placed.Dir,
                                   context.Time.CurrentDay);

                for (int t = 0; t < tiles.Count; t++)
                    grid.GetGridObject(tiles[t].x, tiles[t].y)?.Remove(placed);

                placed.DestroySelf();
            }
        }

        static void ExtinguishFires()
        {
            var fires = Object.FindObjectsByType<StructureFire>(FindObjectsSortMode.None);
            for (int i = 0; i < fires.Length; i++) fires[i].ResetForNewDay();
        }

        /// <summary>
        /// 10. Turtling has to lose slowly, so a day spent closed, badly damaged or serving
        /// poorly costs Reputation even when nothing was destroyed. A player who bricks up
        /// every door loses by attrition, which is correct for a game whose thesis is that
        /// safety is unaffordable.
        /// </summary>
        void ApplyDecay()
        {
            bool closed = context.ClosedForRepairs;
            bool badlyDamaged = context.Perimeter.Current.Integrity < 0.6f;
            bool servedPoorly = context.Report.CustomersServed == 0 && !closed;

            float before = context.Reputation.Current;
            context.Reputation.ApplyDailyDecay(closed, badlyDamaged, servedPoorly);
            context.Report.ReputationDelta += context.Reputation.Current - before;
        }

        /// <summary>
        /// 15.8's distribution target: 15 to 20 percent of nights should land in
        /// under-prepared or worse. Below 10 percent means there are no stakes; above 35
        /// means players feel punished for playing. Flagged per night so the soft-launch
        /// telemetry can actually measure it.
        /// </summary>
        bool BadNight()
        {
            int lost = context.Report.StructuresWrecked + context.Report.StructuresDestroyed;
            int total = Mathf.Max(1, CountStructures());

            return lost / (float)total > 0.2f;
        }

        static int CountStructures() =>
            Object.FindObjectsByType<DamageableStructure>(FindObjectsSortMode.None).Length;
    }
}
