using System;
using LastOrders.Restaurant;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Popups
{
    /// <summary>
    /// "Expand the restaurant?" - what the next expansion costs, with what the player has towards
    /// it, and yes or no.
    ///
    /// Yes does not expand anything yet. It closes the popup and lights up the ground each side
    /// could grow into, and the player taps the one they want; whether that ground is clear and
    /// whether they can pay is asked again then, because both can change in between.
    /// </summary>
    public sealed class RestaurantExpansionPopupView : PopupView
    {
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text body;

        [Tooltip("Pre-built rows, shown as many as the price has parts.")]
        [SerializeField] RequirementRowView[] rows;

        [Tooltip("Why yes is not available, when it is not.")]
        [SerializeField] TMP_Text reason;

        [SerializeField] Button yesButton;
        [SerializeField] Image yesButtonImage;
        [SerializeField] Button noButton;

        [SerializeField] Color readyColour = new Color(0.32f, 0.7f, 0.3f);
        [SerializeField] Color blockedColour = new Color(0.6f, 0.6f, 0.6f);

        [Tooltip("Seconds between refreshes while open.")]
        [SerializeField] float refreshInterval = 0.25f;

        Func<RestaurantOffer> source;
        Action onYes;
        float nextRefresh;

        /// <summary>Whoever bound it, so a caller only ever closes its own popup.</summary>
        public object Owner { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (yesButton != null) yesButton.onClick.AddListener(HandleYes);
            if (noButton != null) noButton.onClick.AddListener(Close);
        }

        public void Bind(Func<RestaurantOffer> offer, Action yes, object owner)
        {
            source = offer;
            onYes = yes;
            Owner = owner;
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

            RestaurantOffer offer = source();

            if (title != null) title.text = offer.Title;
            if (body != null) body.text = offer.Body;

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) continue;

                if (i < offer.Requirements.Count) rows[i].Show(offer.Requirements[i]);
                else rows[i].Hide();
            }

            if (reason != null) reason.text = offer.CanChoose ? string.Empty : offer.Reason;

            if (yesButton != null) yesButton.interactable = offer.CanChoose;
            if (yesButtonImage != null) yesButtonImage.color = offer.CanChoose ? readyColour : blockedColour;
        }

        void HandleYes()
        {
            // Closed first: what yes does next is in the world, behind where the popup was.
            Action yes = onYes;
            Close();
            yes?.Invoke();
        }

        protected override void OnClosed()
        {
            source = null;
            onYes = null;
            Owner = null;
        }
    }
}
