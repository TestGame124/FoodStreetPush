namespace LastOrders.DayCycle
{
    /// <summary>
    /// The five phases of §4. Ordered as they run, so <c>&lt;</c> and <c>&gt;</c> are
    /// meaningful within a single cycle.
    /// </summary>
    public enum DayPhase
    {
        /// <summary>Paused. Damage report, Threat Profile, Dispatch board, trader, Rebuild All.</summary>
        Dawn = 0,

        /// <summary>The working day. Clock 0 to 60. Build, cook, serve. Heat climbs.</summary>
        Day = 1,

        /// <summary>45 real seconds. Last call, shutters, approach arrows. Building locked.</summary>
        Dusk = 2,

        /// <summary>2 to 4 real minutes. The siege. Crisis Bar only.</summary>
        Night = 3,

        /// <summary>Tallies damage, injuries and Reputation, then hands to the next Dawn.</summary>
        Resolution = 4,
    }

    public static class DayPhaseExtensions
    {
        /// <summary>True while the build layer accepts placements. §4: building locks at Dusk.</summary>
        public static bool AllowsBuilding(this DayPhase phase) =>
            phase == DayPhase.Dawn || phase == DayPhase.Day;

        /// <summary>True while customers may arrive, be seated and be served.</summary>
        public static bool AllowsService(this DayPhase phase) => phase == DayPhase.Day;

        /// <summary>True while the Crisis Bar replaces the build UI (§23).</summary>
        public static bool IsSiege(this DayPhase phase) =>
            phase == DayPhase.Dusk || phase == DayPhase.Night;

        /// <summary>True when the phase is a safe point to write a full save. Night is
        /// excluded — it saves through a separate mid-night snapshot (§24.5).</summary>
        public static bool IsSaveBoundary(this DayPhase phase) =>
            phase == DayPhase.Dawn || phase == DayPhase.Resolution;
    }
}
