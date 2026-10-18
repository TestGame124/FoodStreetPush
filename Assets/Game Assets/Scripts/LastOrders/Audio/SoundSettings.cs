using System;
using UnityEngine;

namespace LastOrders.Audio
{
    /// <summary>
    /// The player's own volume sliders: one master, one per bus, kept between sessions.
    ///
    /// Apart from the controller on purpose. A settings screen reads and writes these and needs no
    /// controller in its scene to do it; the controller listens for <see cref="Changed"/> and turns
    /// what is already sounding up or down with the slider. And the player's preference is a
    /// different thing from the mix in <see cref="SoundLibrary"/> - the designer sets the ambience
    /// under the effects, the player decides how loud the effects are.
    /// </summary>
    public static class SoundSettings
    {
        const string Prefix = "LastOrders.Audio.";

        static readonly SoundBus[] AllBuses = (SoundBus[])Enum.GetValues(typeof(SoundBus));

        /// <summary>Raised whenever a slider moves. The bus is null for the master slider.</summary>
        public static event Action<SoundBus?> Changed;

        public static float Master
        {
            get => Read("Master");
            set => Write("Master", value, null);
        }

        public static float Get(SoundBus bus) => Read(bus.ToString());

        public static void Set(SoundBus bus, float volume) => Write(bus.ToString(), volume, bus);

        /// <summary>Mutes or unmutes a bus without losing where its slider was.</summary>
        public static bool IsMuted(SoundBus bus) => PlayerPrefs.GetInt(Prefix + bus + ".Muted", 0) != 0;

        public static void SetMuted(SoundBus bus, bool muted)
        {
            PlayerPrefs.SetInt(Prefix + bus + ".Muted", muted ? 1 : 0);
            Changed?.Invoke(bus);
        }

        /// <summary>What the slider and the mute leave of a bus: 0 to 1.</summary>
        public static float Effective(SoundBus bus) => IsMuted(bus) ? 0f : Get(bus);

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(Prefix + "Master");
            foreach (SoundBus bus in AllBuses)
            {
                PlayerPrefs.DeleteKey(Prefix + bus);
                PlayerPrefs.DeleteKey(Prefix + bus + ".Muted");
            }

            Changed?.Invoke(null);
        }

        static float Read(string key) => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + key, 1f));

        static void Write(string key, float value, SoundBus? bus)
        {
            PlayerPrefs.SetFloat(Prefix + key, Mathf.Clamp01(value));
            Changed?.Invoke(bus);
        }
    }
}
