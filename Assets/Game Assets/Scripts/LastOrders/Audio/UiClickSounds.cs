using System.Collections.Generic;
using LastOrders.UI.Hud;
using UnityEngine;

namespace LastOrders.Audio
{
    /// <summary>
    /// The click under every button: one listener for every pressable <see cref="HudMotion"/> in
    /// the game, including popups made from prefabs long after the scene loaded.
    ///
    /// One place rather than a component on each of a hundred buttons, which a rebuilt screen would
    /// lose. What a button says is read from its name - a close or back button steps back, a buy or
    /// confirm steps forward, anything else taps - unless a <see cref="UiButtonSound"/> on it says
    /// otherwise. Worked out once per button and remembered.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiClickSounds : MonoBehaviour
    {
        static readonly string[] Backward = { "close", "back", "cancel", "dismiss", "exit", "skip" };

        static readonly string[] Forward =
        {
            "confirm", "continue", "claim", "buy", "build", "hire", "upgrade", "start", "accept", "collect",
            "send", "expand", "repair", "place", "done", "cook", "serve", "unlock",
        };

        readonly Dictionary<HudMotion, SoundId> chosen = new Dictionary<HudMotion, SoundId>();

        void OnEnable() => HudMotion.Pressed += HandlePressed;

        void OnDisable() => HudMotion.Pressed -= HandlePressed;

        void HandlePressed(HudMotion element)
        {
            if (element == null) return;

            if (!chosen.TryGetValue(element, out SoundId id))
                chosen[element] = id = Choose(element);

            SoundController.Play(id);
        }

        /// <summary>The sound a button makes, from its <see cref="UiButtonSound"/> or else its name. Public for the tests.</summary>
        public static SoundId Choose(Component element)
        {
            if (element.TryGetComponent(out UiButtonSound custom)) return custom.Sound;
            return ForName(element.name);
        }

        public static SoundId ForName(string name)
        {
            string lower = name != null ? name.ToLowerInvariant() : string.Empty;

            if (ContainsAny(lower, Backward)) return SoundId.UiBack;
            if (ContainsAny(lower, Forward)) return SoundId.UiConfirm;
            return SoundId.UiTap;
        }

        static bool ContainsAny(string text, string[] words)
        {
            for (int i = 0; i < words.Length; i++)
                if (text.Contains(words[i])) return true;
            return false;
        }
    }
}
