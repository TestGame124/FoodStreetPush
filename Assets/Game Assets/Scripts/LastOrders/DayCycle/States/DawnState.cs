using LastOrders.Data;
using UnityEngine;

namespace LastOrders.DayCycle.States
{
    /// <summary>
    /// Dawn. Paused, and the only phase where the player has all the time they want.
    ///
    /// Everything here is a decision made with full information and no clock pressure: read
    /// last night's damage, hear the Threat Profile, send someone on Dispatch, trade, and
    /// rebuild. The clock does not start until they say so, which is what makes every
    /// subsequent minute feel spent rather than lost.
    ///
    /// This is also where the day's arrivals are planned and where Heat is reset - both
    /// deliberately at the start of the cycle rather than the end, so a save written at any
    /// point during a day restores into a world whose day is already fully decided.
    /// </summary>
    public sealed class DawnState : IDayPhaseState
    {
        readonly DayCycleContext context;

        public DawnState(DayCycleContext context)
        {
            this.context = context;
        }

        public DayPhase Phase => DayPhase.Dawn;

        /// <summary>Raised once the dawn screen has everything it needs to draw.</summary>
        public event System.Action<DayReport> OnDawnReady;

        public void OnEnter(DayPhase previous)
        {
            // Paused by design. Nothing in the world moves until BeginDay is called.
            context.Time.IsPaused = true;

            float heatCarriedIn = context.Heat.CurrentHeat;

            context.Heat.ResetForNewDay();
            context.Time.BeginWorkingDay();
            context.ClosedForRepairs = false;

            // Recalled here rather than at the end of Night, so anything the siege left
            // checked out is reclaimed even if the night ended abnormally.
            context.Pools.ReturnAll();

            context.Perimeter.Recompute();
            context.Perimeter.SeedNavigationFields();

            // Success itself is dangerous. Applied once, at the start of the day, so the
            // player can see the floor they are starting from.
            context.Heat.AddReputationTier(context.Reputation.Tier);
            context.Heat.AddFootprint(CountOccupiedTiles());

            int day = context.Time.CurrentDay;

            // The forecast reads tonight's plan rather than predicting it. Yesterday's Heat
            // is the best available estimate of tonight's before the day has been traded.
            context.ThreatProfile.Forecast(day, heatCarriedIn);

            context.Dispatch.RefreshBoard(day);
            context.Customers.PlanDay(day, MenuVariety(), Comfort(), DamageState(), FactionEvent());

            OnDawnReady?.Invoke(context.Report);
        }

        public void OnTick(float deltaSeconds)
        {
            // Nothing. Dawn advances only when the player commits to opening.
        }

        public void OnExit(DayPhase next)
        {
            context.Time.IsPaused = false;
            context.Report.Reset(context.Time.CurrentDay);
        }

        /// <summary>
        /// 15.10. Skip serving entirely for a faster build day and a Reputation cost.
        ///
        /// A deliberate recovery lever rather than a miserable half-day: after a disaster the
        /// player should be able to choose to spend a day rebuilding instead of limping
        /// through a service they cannot staff.
        /// </summary>
        public void ChooseClosedForRepairs()
        {
            context.ClosedForRepairs = true;
            context.Reputation.Add(-context.Balance.closedForRepairsReputationCost);
        }

        static int CountOccupiedTiles()
        {
            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();
            if (grid == null) return 0;

            int count = 0;
            int width = grid.GetWidth();
            int height = grid.GetHeight();

            for (int x = 0; x < width; x++)
                for (int z = 0; z < height; z++)
                    if (grid.GetGridObject(x, z)?.GetPlacedObject() != null) count++;

            return count;
        }

        /// <summary>Menu variety multiplier for the daily pool (9.2). More distinct recipes
        /// available means a broader draw of customers.</summary>
        float MenuVariety()
        {
            int recipes = context.Balance.recipes != null ? context.Balance.recipes.Length : 0;
            return Mathf.Lerp(0.8f, 1.3f, Mathf.Clamp01(recipes / 12f));
        }

        float Comfort()
        {
            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();
            if (grid == null) return 1f;

            float total = 1f;
            var counted = new System.Collections.Generic.HashSet<PlacedObject_Done>();

            int width = grid.GetWidth();
            int height = grid.GetHeight();

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    PlacedObject_Done occupant = grid.GetGridObject(x, z)?.GetPlacedObject();
                    if (occupant == null || !counted.Add(occupant)) continue;

                    PlacedObjectTypeSO type = occupant.PlacedObjectTypeSO;
                    if (type != null && type.buildCategory == BuildCategory.Comfort)
                        total += type.comfortMultiplier - 1f;
                }
            }

            return Mathf.Clamp(total, 0.9f, 1.2f);
        }

        /// <summary>A damaged restaurant draws fewer people. The 0.5 floor is what makes a
        /// bad night self-limiting rather than a spiral: fewer customers means less Heat,
        /// which means a survivable night tomorrow.</summary>
        float DamageState() => Mathf.Clamp(context.Perimeter.Current.Integrity, 0.5f, 1f);

        float FactionEvent() => 1f;
    }
}
