using LastOrders.UI.Hud;
using TMPro;
using UnityEngine;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// The line of help along the shop drawer's top. It normally says how to build; a tapped card
    /// borrows it for a moment to answer - drag it, it is still locked, it needs more coins - then
    /// it goes back to the usual line.
    /// </summary>
    public sealed class ShopDrawerHint : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] HudMotion motion;
        [SerializeField] string usual = "Drag an item up onto your land to build it";
        [SerializeField] float holdSeconds = 3f;

        [SerializeField] Color usualColour = new Color(0.47f, 0.39f, 0.32f);
        [SerializeField] Color warningColour = new Color(0.72f, 0.42f, 0.10f);
        [SerializeField] Color dangerColour = new Color(0.78f, 0.31f, 0.25f);
        [SerializeField] Color infoColour = new Color(0.13f, 0.39f, 0.37f);

        float revertAt = -1f;

        void OnEnable() => Revert();

        void Update()
        {
            if (revertAt > 0f && Time.unscaledTime >= revertAt) Revert();
        }

        public void Say(string message, HudTone tone)
        {
            if (label == null) return;

            label.text = message;
            label.color = tone switch
            {
                HudTone.Warning => warningColour,
                HudTone.Danger => dangerColour,
                _ => infoColour,
            };

            revertAt = Time.unscaledTime + holdSeconds;
            if (motion != null) motion.Punch(0.1f);
        }

        void Revert()
        {
            revertAt = -1f;
            if (label == null) return;
            label.text = usual;
            label.color = usualColour;
        }
    }
}
