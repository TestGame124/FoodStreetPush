using System;

namespace LastOrders.Audio
{
    /// <summary>
    /// The volume sliders. Every cue answers to exactly one, so a player who turns the music off
    /// still hears the zombies, and one who mutes the effects still hears the alarm.
    ///
    /// Explicit values because they are saved: in each cue asset, in the library's mix and in the
    /// player's preferences. Add to the end; never renumber.
    /// </summary>
    public enum SoundBus
    {
        /// <summary>Everything that happens in the world: cooking, zombies, building, the guns.</summary>
        Sfx = 0,

        /// <summary>Buttons, popups, coins landing, the level-up fanfare. Never spatial.</summary>
        Ui = 1,

        /// <summary>The bed under everything: the countryside by day, crickets by night.</summary>
        Ambience = 2,

        /// <summary>Score. The pack has none yet; the bus is here so adding it is content only.</summary>
        Music = 3,

        /// <summary>People: the customers' and the cooks' cartoon voices.</summary>
        Voice = 4,
    }

    /// <summary>The <see cref="LastOrders.DayCycle.DayPhase"/>s a looping emitter sounds in.</summary>
    [Flags]
    public enum SoundPhases
    {
        None = 0,
        Dawn = 1 << 0,
        Day = 1 << 1,
        Dusk = 1 << 2,
        Night = 1 << 3,
        Resolution = 1 << 4,
        All = Dawn | Day | Dusk | Night | Resolution,
    }

    public static class SoundPhasesExtensions
    {
        public static bool Includes(this SoundPhases phases, DayCycle.DayPhase phase) =>
            (phases & (SoundPhases)(1 << (int)phase)) != 0;
    }
}
