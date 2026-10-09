using LastOrders.UI.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.Livestock
{
    /// <summary>
    /// The card over a farm building when it is tapped: what it gives, in a round badge on the left -
    /// an egg, a bottle of milk - and beside it how the next one is coming on: a bar filling, the
    /// time left on it, how many are waiting out of how many it holds, and the feed still in the
    /// trough. Hungry, it shows the feed it is waiting for instead, and the bar stops; asleep, it
    /// greys over until the morning.
    ///
    /// Takes no raycasts, like the work timer: it floats over the building, and taps meant for the
    /// building must go through it.
    /// </summary>
    public sealed class ProduceProgressView : WorldUiElement
    {
        public enum State { Working, Hungry, Asleep, Waiting, Full }

        [SerializeField] Image icon;

        [Tooltip("The badge behind the icon; it shakes while the animals are hungry.")]
        [SerializeField] RectTransform badge;

        [Tooltip("An Image set to Filled. Its fill amount is the progress.")]
        [SerializeField] Image fill;

        [SerializeField] TMP_Text caption;
        [SerializeField] TMP_Text time;
        [SerializeField] TMP_Text stock;

        [Tooltip("The feed left in the trough: its icon and how many.")]
        [SerializeField] Image feedIcon;

        [SerializeField] TMP_Text feedCount;

        [SerializeField] Color workingColour = new Color(0.45f, 0.82f, 0.35f);
        [SerializeField] Color hungryColour = new Color(0.9f, 0.42f, 0.3f);
        [SerializeField] Color asleepColour = new Color(0.55f, 0.6f, 0.72f);
        [SerializeField] Color fullColour = new Color(0.98f, 0.72f, 0.25f);

        State state;

        public void Show(State now, Sprite produce, Sprite feed, string captionText, float progress, float secondsLeft, int stored, int capacity,
                         int feedLeft)
        {
            state = now;

            if (icon != null)
            {
                Sprite shown = now == State.Hungry && feed != null ? feed : produce;
                if (icon.sprite != shown) icon.sprite = shown;
                icon.enabled = shown != null;
            }

            if (fill != null)
            {
                fill.fillAmount = now == State.Full ? 1f : Mathf.Clamp01(progress);
                fill.color = now == State.Working ? workingColour
                    : now == State.Hungry ? hungryColour
                    : now == State.Full ? fullColour
                    : asleepColour;
            }

            if (caption != null) caption.text = captionText;

            if (time != null)
            {
                time.text = now == State.Full ? "FULL"
                    : now == State.Hungry ? "FEED ME"
                    : now == State.Asleep ? "Zzz"
                    : FormatSeconds(secondsLeft);
            }

            if (stock != null) stock.text = $"{stored}/{capacity}";

            bool fed = feed != null;
            if (feedIcon != null)
            {
                if (feedIcon.sprite != feed) feedIcon.sprite = feed;
                feedIcon.enabled = fed;
            }

            if (feedCount != null)
            {
                feedCount.enabled = fed;
                feedCount.text = $"x{feedLeft}";
                feedCount.color = feedLeft > 0 ? Color.white : hungryColour;
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (badge == null) return;

            // Hungry, the badge shakes for attention; working, it rocks gently.
            float t = Time.unscaledTime;
            float angle = state == State.Hungry ? Mathf.Sin(t * 22f) * 9f * Mathf.Max(0f, Mathf.Sin(t * 2.4f))
                : state == State.Working ? Mathf.Sin(t * 2.2f) * 4f
                : 0f;
            badge.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        public override void OnReturnedToPool()
        {
            if (badge != null) badge.localRotation = Quaternion.identity;
            base.OnReturnedToPool();
        }

        /// <summary>"8s" under a minute, "1:05" from a minute up, as the work timer has it.</summary>
        internal static string FormatSeconds(float seconds)
        {
            int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return total >= 60 ? $"{total / 60}:{total % 60:00}" : $"{total}s";
        }

        /// <summary>Editor wiring, for the prefab builder.</summary>
        public void Configure(RectTransform animated, Image produceIcon, RectTransform iconBadge, Image bar, TMP_Text captionLabel,
                              TMP_Text timeLabel, TMP_Text stockLabel, Image feedImage, TMP_Text feedLabel)
        {
            body = animated;
            icon = produceIcon;
            badge = iconBadge;
            fill = bar;
            caption = captionLabel;
            time = timeLabel;
            stock = stockLabel;
            feedIcon = feedImage;
            feedCount = feedLabel;
        }
    }
}
