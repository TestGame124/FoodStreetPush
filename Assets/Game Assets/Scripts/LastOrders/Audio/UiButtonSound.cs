using LastOrders.UI.Hud;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastOrders.Audio
{
    /// <summary>
    /// Which sound a button makes, where the default is not right - and the click itself, on a
    /// button that has no <see cref="HudMotion"/> to give under the finger.
    ///
    /// Most buttons need nothing: every pressable HudMotion already clicks through
    /// <see cref="UiClickSounds"/>, which reads this when it is there to know which sound. A
    /// button without one - a prompt floating over the world, the approach alert's badge - sounds
    /// from here instead, once per click, and only while it can be clicked.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiButtonSound : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] SoundId sound = SoundId.UiTap;

        public SoundId Sound => sound;

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(SoundId id) => sound = id;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            // The press has already been sounded, by this button's own sound.
            if (TryGetComponent(out HudMotion motion) && motion.Pressable) return;

            if (TryGetComponent(out Selectable selectable) && !selectable.IsInteractable()) return;

            SoundController.Play(sound);
        }
    }
}
