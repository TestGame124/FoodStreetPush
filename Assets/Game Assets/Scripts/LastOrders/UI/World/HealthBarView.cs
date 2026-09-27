using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.World
{
    /// <summary>
    /// The bar over a worker in a fight: how much health they have left, green going to red.
    ///
    /// Shown while they are fighting and for a few seconds after, not all day - a bar over
    /// every head in the compound is noise, and one that appears only when something is hitting
    /// them is a warning. Below the danger line it pulses, which is the moment to recall them.
    ///
    /// Takes no raycasts, like the work timer: it floats over a character the player may want
    /// to tap.
    /// </summary>
    public sealed class HealthBarView : WorldUiElement
    {
        [Tooltip("An Image set to Filled. Its fill amount is the health left.")]
        [SerializeField] Image fill;

        [SerializeField] TMP_Text label;

        [Tooltip("Pulsed while badly hurt. The body's own group, so the root is untouched.")]
        [SerializeField] CanvasGroup group;

        [SerializeField] Color healthyColour = new Color(0.4f, 0.85f, 0.3f);
        [SerializeField] Color hurtColour = new Color(0.95f, 0.75f, 0.2f);
        [SerializeField] Color dangerColour = new Color(0.9f, 0.25f, 0.2f);

        bool pulsing;

        /// <summary>Health as a share of the maximum, the numbers to print, and whether they
        /// are past the danger line.</summary>
        public void Set(float fraction, float current, float max, bool low)
        {
            fraction = Mathf.Clamp01(fraction);

            if (fill != null)
            {
                fill.fillAmount = fraction;
                fill.color = low ? dangerColour : Color.Lerp(hurtColour, healthyColour, (fraction - 0.35f) / 0.65f);
            }

            if (label != null) label.text = $"{Mathf.CeilToInt(current)}/{Mathf.RoundToInt(max)}";

            SetPulse(low);
        }

        void SetPulse(bool on)
        {
            if (group == null || on == pulsing) return;

            pulsing = on;
            DOTween.Kill(group);
            group.alpha = 1f;

            if (on)
            {
                DOTween.To(() => group.alpha, a => group.alpha = a, 0.45f, 0.35f)
                       .SetEase(Ease.InOutSine)
                       .SetLoops(-1, LoopType.Yoyo)
                       .SetTarget(group);
            }
        }

        public override void OnReturnedToPool()
        {
            SetPulse(false);
            if (fill != null) fill.fillAmount = 1f;
            if (label != null) label.text = string.Empty;

            base.OnReturnedToPool();
        }
    }
}
