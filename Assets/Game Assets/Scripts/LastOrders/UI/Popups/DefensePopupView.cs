using System;
using System.Text;
using LastOrders.Defense;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Popups
{
    /// <summary>
    /// Buying or upgrading a defense: what it is, who is posted there, what it fires, and a
    /// button for each way to pay - the stores, or premium currency.
    ///
    /// Reads a fresh <see cref="DefenseOffer"/> a few times a second while open rather than
    /// once on opening, so tokens arriving or dawn breaking while the popup is up wakes the
    /// right button without closing and reopening it. The same popup serves every kind of
    /// defense, because everything that differs between them is in the offer.
    /// </summary>
    public sealed class DefensePopupView : PopupView
    {
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text description;

        [Tooltip("Who is on watch, or that nobody is.")]
        [SerializeField] TMP_Text status;

        [Tooltip("What the player gets, a line each.")]
        [SerializeField] TMP_Text details;

        [Tooltip("Why a button is not available, when it is not.")]
        [SerializeField] TMP_Text reason;

        [Header("Pay from the stores")]
        [SerializeField] Button resourcesButton;
        [SerializeField] Image resourcesButtonImage;
        [SerializeField] TMP_Text resourcesLabel;

        [Header("Pay with premium currency")]
        [SerializeField] Button premiumButton;
        [SerializeField] Image premiumButtonImage;
        [SerializeField] TMP_Text premiumLabel;

        [SerializeField] Color readyColour = new Color(0.32f, 0.7f, 0.3f);
        [SerializeField] Color premiumColour = new Color(0.55f, 0.36f, 0.85f);
        [SerializeField] Color blockedColour = new Color(0.6f, 0.6f, 0.6f);

        [Tooltip("Seconds between refreshes while open.")]
        [SerializeField] float refreshInterval = 0.25f;

        readonly StringBuilder lines = new StringBuilder(256);

        Func<DefenseOffer> source;
        Action<DefensePayment> onBuy;
        Vector2 resourcesHome;
        float nextRefresh;

        /// <summary>Whoever opened it, so a post only ever closes its own popup.</summary>
        public object Owner { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            if (resourcesButton != null)
            {
                resourcesButton.onClick.AddListener(() => HandleBuy(DefensePayment.Resources));
                resourcesHome = ((RectTransform)resourcesButton.transform).anchoredPosition;
            }

            if (premiumButton != null) premiumButton.onClick.AddListener(() => HandleBuy(DefensePayment.Premium));
        }

        /// <summary>Binds the popup to a post: where its offer comes from and what paying does.</summary>
        public void Bind(object owner, Func<DefenseOffer> offer, Action<DefensePayment> buy)
        {
            Owner = owner;
            source = offer;
            onBuy = buy;
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

            DefenseOffer offer = source();

            SetText(title, offer.Title);
            SetText(description, offer.Description);
            SetText(status, offer.Status);

            lines.Clear();
            for (int i = 0; i < offer.Details.Count; i++)
            {
                if (i > 0) lines.Append('\n');
                lines.Append("- ").Append(offer.Details[i]);
            }
            SetText(details, lines.ToString());

            bool open = offer.Blocked == null;

            if (offer.IsFree)
            {
                ShowButton(resourcesButton, resourcesButtonImage, resourcesLabel, true, "BUILD", open, readyColour);
                ShowButton(premiumButton, premiumButtonImage, premiumLabel, false, null, false, premiumColour);
                SetText(reason, offer.Blocked ?? string.Empty);
                Centre(onlyResources: true);
                return;
            }

            DefensePaymentOption? stores = Find(offer, DefensePayment.Resources);
            DefensePaymentOption? gems = Find(offer, DefensePayment.Premium);

            ShowButton(resourcesButton, resourcesButtonImage, resourcesLabel, stores.HasValue,
                       stores?.Label, open && stores.HasValue && stores.Value.Affordable, readyColour);
            ShowButton(premiumButton, premiumButtonImage, premiumLabel, gems.HasValue,
                       gems?.Label, open && gems.HasValue && gems.Value.Affordable, premiumColour);

            Centre(onlyResources: stores.HasValue && !gems.HasValue);

            SetText(reason, offer.Blocked ?? Shortfall(stores, gems));
        }

        /// <summary>What is missing, when nothing can be afforded. Empty when something can.</summary>
        static string Shortfall(DefensePaymentOption? stores, DefensePaymentOption? gems)
        {
            if (stores.HasValue && stores.Value.Affordable) return string.Empty;
            if (gems.HasValue && gems.Value.Affordable) return string.Empty;

            string a = stores?.ShortBy;
            string b = gems?.ShortBy;

            if (a != null && b != null) return $"{a}  Or: {b}";
            return a ?? b ?? string.Empty;
        }

        /// <summary>One button alone sits in the middle rather than off to one side.</summary>
        void Centre(bool onlyResources)
        {
            if (resourcesButton == null) return;

            var rect = (RectTransform)resourcesButton.transform;
            rect.anchoredPosition = onlyResources ? new Vector2(0f, resourcesHome.y) : resourcesHome;
        }

        static DefensePaymentOption? Find(DefenseOffer offer, DefensePayment method)
        {
            for (int i = 0; i < offer.Payments.Count; i++)
                if (offer.Payments[i].Method == method) return offer.Payments[i];

            return null;
        }

        void ShowButton(Button button, Image image, TMP_Text label, bool visible, string text, bool ready, Color colour)
        {
            if (button == null) return;

            if (button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
            if (!visible) return;

            button.interactable = ready;
            if (image != null) image.color = ready ? colour : blockedColour;
            SetText(label, text);
        }

        void HandleBuy(DefensePayment payment)
        {
            onBuy?.Invoke(payment);

            // Still open means the purchase was refused; show why straight away.
            if (IsOpen) Refresh();
        }

        static void SetText(TMP_Text target, string text)
        {
            if (target != null) target.text = text ?? string.Empty;
        }

        protected override void OnClosed()
        {
            Owner = null;
            source = null;
            onBuy = null;
        }
    }
}
