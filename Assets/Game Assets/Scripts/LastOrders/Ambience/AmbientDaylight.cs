using UnityEngine;

namespace LastOrders.Ambience
{
    /// <summary>
    /// How dark it is, for the level's wildlife: birds go to roost as the light goes, fireflies come out.
    ///
    /// Read off the global DayNightLighting already sets for the world-edge fog and light pools, rather
    /// than off the day-cycle machine: it eases between phases instead of jumping, so a flock leaves as
    /// dusk falls, and none of this needs the game's composition to run. Zero - full day - in a level
    /// without day/night lighting, and in edit mode.
    /// </summary>
    public static class AmbientDaylight
    {
        static readonly int NightGlowId = Shader.PropertyToID("_EnvNightGlow");

        /// <summary>0 is full day, 1 full night. Dawn is about 0.25, dusk 0.6.</summary>
        public static float Darkness => Mathf.Clamp01(Shader.GetGlobalFloat(NightGlowId));

        /// <summary>1 by day, easing to 0 as dusk falls.</summary>
        public static float Day => 1f - AmbientWind.Smoothstep(0.3f, 0.55f, Darkness);

        /// <summary>0 by day, rising to 1 as night falls.</summary>
        public static float Night => AmbientWind.Smoothstep(0.5f, 0.85f, Darkness);
    }
}
