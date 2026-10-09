using System;
using LastOrders.Livestock;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Popups
{
    /// <summary>
    /// Feeding a farm building: what it is doing now (the next egg's bar and time, how many are
    /// waiting), and how many eggs or churns to make next - picked with minus, plus and MAX, the
    /// feed that costs shown beside what it makes, with how long it takes, what the feeder holds
    /// and what the storehouse has. FEED hands the choice back to the building, with where the
    /// button is on screen so the feed can be seen flying from it into the feeder.
    ///
    /// Reads a fresh <see cref="FeedOffer"/> several times a second while open, so the bar moves
    /// and a crop harvested meanwhile shows up in what can be given.
    /// </summary>
    public sealed class FeedPopupView : PopupView
    {
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text description;

        [Header("Now")]
        [SerializeField] Image nowIcon;
        [SerializeField] TMP_Text status;
        [SerializeField] Image bar;
        [SerializeField] TMP_Text barTime;
        [SerializeField] TMP_Text stock;

        [Header("How many")]
        [SerializeField] Image feedIcon;
        [SerializeField] TMP_Text feedCount;
        [SerializeField] Image produceIcon;
        [SerializeField] TMP_Text produceCount;

        [Tooltip("What the choice makes, under the number: \"4 eggs\".")]
        [SerializeField] TMP_Text amount;

        [Tooltip("How long it takes, the feeder and the storehouse.")]
        [SerializeField] TMP_Text summary;

        [SerializeField] Button less;
        [SerializeField] Button more;
        [SerializeField] Button most;

        [Tooltip("Why nothing can be fed, when nothing can.")]
        [SerializeField] TMP_Text reason;

        [SerializeField] Button feedButton;
        [SerializeField] Image feedButtonImage;
        [SerializeField] TMP_Text feedButtonLabel;

        [Header("Colours")]
        [SerializeField] Color readyColour = new Color(0.32f, 0.7f, 0.3f);
        [SerializeField] Color blockedColour = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] Color workingColour = new Color(0.45f, 0.82f, 0.35f);
        [SerializeField] Color hungryColour = new Color(0.9f, 0.42f, 0.3f);
        [SerializeField] Color asleepColour = new Color(0.55f, 0.6f, 0.72f);
        [SerializeField] Color fullColour = new Color(0.98f, 0.72f, 0.25f);

        [Tooltip("Seconds between refreshes while open.")]
        [SerializeField] float refreshInterval = 0.1f;

        Func<FeedOffer> source;
        Action<int, Vector2> onFeed;
        int chosen;
        float nextRefresh;

        /// <summary>Whoever opened it, so a building only ever closes its own popup.</summary>
        public object Owner { get; private set; }

        /// <summary>Items of feed picked.</summary>
        public int Chosen => chosen;

        protected override void Awake()
        {
            base.Awake();
            if (less != null) less.onClick.AddListener(() => Choose(chosen - 1));
            if (more != null) more.onClick.AddListener(() => Choose(chosen + 1));
            if (most != null) most.onClick.AddListener(() => Choose(int.MaxValue));
            if (feedButton != null) feedButton.onClick.AddListener(HandleFeed);
        }

        /// <summary>Binds the popup to a building: where its offer comes from, and what feeding
        /// <c>n</c> items does, given where the FEED button is on screen.</summary>
        public void Bind(object owner, Func<FeedOffer> offer, Action<int, Vector2> feed)
        {
            Owner = owner;
            source = offer;
            onFeed = feed;
            chosen = 1;
            Refresh();
        }

        /// <summary>Picks <paramref name="items"/> of feed, held to what can be given.</summary>
        public void Choose(int items)
        {
            chosen = items;
            Refresh();
        }

        void Update()
        {
            if (source == null) return;

            nextRefresh -= Time.unscaledDeltaTime;
            if (nextRefresh > 0f) return;

            Refresh();
        }

        void Refresh()
        {
            nextRefresh = refreshInterval;
            if (source == null) return;

            FeedOffer offer = source();
            int top = offer.Most;
            chosen = top <= 0 ? 0 : Mathf.Clamp(chosen, 1, top);

            SetText(title, offer.Title);
            SetText(description, offer.Description);

            ShowNow(offer);

            SetIcon(feedIcon, offer.FeedIcon);
            SetIcon(produceIcon, offer.ProduceIcon);
            int makes = offer.Makes(chosen);
            SetText(feedCount, $"x{chosen}");
            SetText(produceCount, $"x{makes}");
            SetText(amount, $"{makes} {(makes == 1 ? offer.UnitName : offer.UnitsName)}");

            string kept = offer.KeepsSeed && offer.Have > 0 ? " (1 kept for sowing)" : string.Empty;
            SetText(summary, (chosen > 0 ? $"Takes {ProduceProgressView.FormatSeconds(offer.Takes(chosen))} of daytime\n" : "\n") +
                             $"Feeder {offer.InTrough}/{offer.TroughCapacity}  -  Storehouse {offer.Have} {offer.FeedName}{kept}");

            SetText(reason, offer.Blocked ?? string.Empty);

            if (less != null) less.interactable = chosen > 1;
            if (more != null) more.interactable = chosen < top;
            if (most != null) most.interactable = chosen < top;

            bool ready = chosen > 0;
            if (feedButton != null) feedButton.interactable = ready;
            if (feedButtonImage != null) feedButtonImage.color = ready ? readyColour : blockedColour;
            SetText(feedButtonLabel, ready ? $"FEED {chosen} {offer.FeedName.ToUpperInvariant()}" : "FEED");
        }

        void ShowNow(FeedOffer offer)
        {
            ProduceProgressView.State now = offer.State;
            SetIcon(nowIcon, now == ProduceProgressView.State.Hungry && offer.FeedIcon != null ? offer.FeedIcon : offer.ProduceIcon);
            SetText(status, offer.Status);
            SetText(stock, $"{offer.Stored}/{offer.Capacity}");

            if (bar != null)
            {
                bar.fillAmount = now == ProduceProgressView.State.Full ? 1f : Mathf.Clamp01(offer.Progress);
                bar.color = now == ProduceProgressView.State.Working ? workingColour
                    : now == ProduceProgressView.State.Hungry ? hungryColour
                    : now == ProduceProgressView.State.Full ? fullColour
                    : asleepColour;
            }

            SetText(barTime, now == ProduceProgressView.State.Full ? "FULL"
                : now == ProduceProgressView.State.Hungry ? "FEED ME"
                : now == ProduceProgressView.State.Asleep ? "Zzz"
                : ProduceProgressView.FormatSeconds(offer.SecondsToNext));
        }

        void HandleFeed()
        {
            if (chosen <= 0) return;

            // Where the button is on screen, for the feed to set off from. The popup canvas is an overlay.
            Vector2 from = feedButton != null
                ? RectTransformUtility.WorldToScreenPoint(null, feedButton.transform.position)
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.3f);

            onFeed?.Invoke(chosen, from);

            // Still open means it was refused; show why straight away.
            if (IsOpen) Refresh();
        }

        static void SetIcon(Image image, Sprite sprite)
        {
            if (image == null) return;
            if (image.sprite != sprite) image.sprite = sprite;
            image.enabled = sprite != null;
        }

        static void SetText(TMP_Text target, string text)
        {
            if (target != null) target.text = text ?? string.Empty;
        }

        protected override void OnClosed()
        {
            Owner = null;
            source = null;
            onFeed = null;
            chosen = 0;
        }
    }
}
