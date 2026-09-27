using System;
using LastOrders.Expansion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Popups
{
    /// <summary>
    /// What expanding into a piece of land needs: its name and what it becomes, a line per
    /// requirement with what the player has towards it, the build time, and the button.
    ///
    /// Reads a fresh <see cref="ExpansionOffer"/> a few times a second while open rather than
    /// once on opening, so wood arriving from a cleared pile or a level gained while the popup
    /// is up turns its line green and wakes the button without closing and reopening it.
    /// </summary>
    public sealed class ExpansionPopupView : PopupView
    {
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text benefit;

        [Tooltip("Pre-built rows, shown as many as the offer has requirements.")]
        [SerializeField] RequirementRowView[] rows;

        [SerializeField] TMP_Text buildTime;

        [Tooltip("Why the button is not available, when it is not.")]
        [SerializeField] TMP_Text reason;

        [SerializeField] Button expandButton;
        [SerializeField] Image expandButtonImage;

        [SerializeField] Color readyColour = new Color(0.32f, 0.7f, 0.3f);
        [SerializeField] Color blockedColour = new Color(0.6f, 0.6f, 0.6f);

        [Tooltip("Seconds between refreshes while open.")]
        [SerializeField] float refreshInterval = 0.25f;

        Func<ExpansionOffer> source;
        Action onExpand;
        float nextRefresh;

        protected override void Awake()
        {
            base.Awake();
            if (expandButton != null) expandButton.onClick.AddListener(HandleExpand);
        }

        /// <summary>Binds the popup to an area: where its offer comes from and what the button does.</summary>
        public void Bind(Func<ExpansionOffer> offer, Action expand)
        {
            source = offer;
            onExpand = expand;
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

            ExpansionOffer offer = source();

            if (title != null) title.text = $"Expand: {offer.Title}";
            if (benefit != null) benefit.text = offer.Benefit;
            if (buildTime != null) buildTime.text = $"Build time: {FormatSeconds(offer.Seconds)}";

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) continue;

                if (i < offer.Requirements.Count) rows[i].Show(offer.Requirements[i]);
                else rows[i].Hide();
            }

            if (reason != null)
            {
                reason.text = offer.CanExpand ? string.Empty : offer.Reason;
            }

            if (expandButton != null) expandButton.interactable = offer.CanExpand;
            if (expandButtonImage != null) expandButtonImage.color = offer.CanExpand ? readyColour : blockedColour;
        }

        void HandleExpand()
        {
            onExpand?.Invoke();

            // Still open means the expansion was refused; show why straight away.
            if (IsOpen) Refresh();
        }

        protected override void OnClosed()
        {
            source = null;
            onExpand = null;
        }

        static string FormatSeconds(float seconds)
        {
            int total = Mathf.CeilToInt(seconds);
            return total >= 60 ? $"{total / 60}:{total % 60:00}" : $"{total}s";
        }
    }
}
