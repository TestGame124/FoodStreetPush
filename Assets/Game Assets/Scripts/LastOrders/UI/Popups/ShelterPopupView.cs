using System;
using System.Text;
using LastOrders.Housing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Popups
{
    /// <summary>
    /// Building a shelter, or hiring someone into one: how many beds are taken, who is in them,
    /// and a button for each thing on offer - one to build on an empty site, one per role to hire
    /// in a built shelter.
    ///
    /// Reads a fresh <see cref="ShelterOffer"/> a few times a second while open rather than once
    /// on opening, so coins arriving or dusk falling while the popup is up changes the right
    /// button without closing and reopening it. The buttons are laid out centred in a row, as
    /// many as the offer has.
    /// </summary>
    public sealed class ShelterPopupView : PopupView
    {
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text description;

        [Tooltip("How many beds are taken - the hiring limit.")]
        [SerializeField] TMP_Text status;

        [Tooltip("Who is in each bed, and what each hire does, a line each.")]
        [SerializeField] TMP_Text details;

        [Tooltip("Why a button is not available, when it is not.")]
        [SerializeField] TMP_Text reason;

        [Header("Options, left to right")]
        [SerializeField] Button[] optionButtons;
        [SerializeField] Image[] optionImages;
        [SerializeField] TMP_Text[] optionLabels;

        [Tooltip("Distance between the centres of neighbouring buttons.")]
        [SerializeField] float optionSpacing = 280f;

        [SerializeField] Color readyColour = new Color(0.32f, 0.7f, 0.3f);
        [SerializeField] Color blockedColour = new Color(0.6f, 0.6f, 0.6f);

        [Tooltip("Seconds between refreshes while open.")]
        [SerializeField] float refreshInterval = 0.25f;

        readonly StringBuilder lines = new StringBuilder(256);

        Func<ShelterOffer> source;
        Action<int> onTake;
        float nextRefresh;

        /// <summary>Whoever opened it, so a shelter only ever closes its own popup.</summary>
        public object Owner { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            if (optionButtons == null) return;

            for (int i = 0; i < optionButtons.Length; i++)
            {
                int index = i;
                if (optionButtons[i] != null) optionButtons[i].onClick.AddListener(() => HandleTake(index));
            }
        }

        /// <summary>Binds the popup to a shelter: where its offer comes from, and what pressing
        /// option <c>n</c> does.</summary>
        public void Bind(object owner, Func<ShelterOffer> offer, Action<int> take)
        {
            Owner = owner;
            source = offer;
            onTake = take;
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

            ShelterOffer offer = source();

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

            int slots = optionButtons != null ? optionButtons.Length : 0;
            int shown = Mathf.Min(offer.Options.Count, slots);

            for (int i = 0; i < slots; i++)
            {
                bool visible = i < shown;
                ShelterOption option = visible ? offer.Options[i] : default;
                ShowButton(i, visible, option.Label, visible && offer.CanTake(i), (i - (shown - 1) * 0.5f) * optionSpacing);
            }

            SetText(reason, offer.Blocked ?? Shortfall(offer));
        }

        /// <summary>What is missing, when nothing on offer can be afforded. Empty when something can.</summary>
        static string Shortfall(ShelterOffer offer)
        {
            string first = null;

            for (int i = 0; i < offer.Options.Count; i++)
            {
                if (offer.Options[i].Affordable) return string.Empty;
                first ??= offer.Options[i].ShortBy;
            }

            return first ?? string.Empty;
        }

        void ShowButton(int index, bool visible, string text, bool ready, float x)
        {
            Button button = optionButtons[index];
            if (button == null) return;

            if (button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
            if (!visible) return;

            var rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);

            button.interactable = ready;

            Image image = optionImages != null && index < optionImages.Length ? optionImages[index] : null;
            if (image != null) image.color = ready ? readyColour : blockedColour;

            TMP_Text label = optionLabels != null && index < optionLabels.Length ? optionLabels[index] : null;
            SetText(label, text);
        }

        void HandleTake(int index)
        {
            onTake?.Invoke(index);

            // Still open means it was refused; show why straight away.
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
            onTake = null;
        }
    }
}
