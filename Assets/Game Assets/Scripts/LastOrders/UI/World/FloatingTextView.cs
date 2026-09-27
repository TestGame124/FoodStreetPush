using DG.Tweening;
using TMPro;
using UnityEngine;

namespace LastOrders.UI.World
{
    /// <summary>
    /// A line of text that rises and fades - "+2 XP" over a cleared pile - then returns
    /// itself to the pool.
    ///
    /// The payout already happened by the time this plays; it is the receipt, placed where the
    /// player was looking rather than on a HUD counter they were not.
    /// </summary>
    public sealed class FloatingTextView : WorldUiElement
    {
        [SerializeField] TMP_Text label;

        [Tooltip("Fades the whole element. On the body, so the root stays untouched.")]
        [SerializeField] CanvasGroup group;

        [Tooltip("Metres risen over the lifetime.")]
        [SerializeField] float rise = 1.2f;

        [SerializeField] float lifetime = 1.3f;

        public void Play(string message, Color colour)
        {
            if (label != null)
            {
                label.text = message;
                label.color = colour;
            }

            if (group != null) group.alpha = 1f;
            extraOffset = Vector3.zero;

            DOTween.To(() => extraOffset.y, y => extraOffset.y = y, rise, lifetime)
                   .SetEase(Ease.OutCubic)
                   .SetTarget(this)
                   .OnComplete(Release);

            if (group != null)
            {
                // Holds full strength for the first half, so it can be read before it goes.
                DOTween.To(() => group.alpha, a => group.alpha = a, 0f, lifetime * 0.5f)
                       .SetDelay(lifetime * 0.5f)
                       .SetTarget(this);
            }
        }

        public override void OnReturnedToPool()
        {
            // Tweens first, so a fade cannot write alpha back after it has been reset.
            DOTween.Kill(this);

            if (group != null) group.alpha = 1f;
            if (label != null) label.text = string.Empty;

            base.OnReturnedToPool();
        }
    }
}
