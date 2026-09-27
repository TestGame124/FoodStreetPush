using System;
using System.Text;
using LastOrders.Field;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Popups
{
    /// <summary>
    /// A worker's info panel: who they are, their health, what they are doing, where they
    /// could be sent - and the Recall Worker button while they are out. The same panel answers
    /// a tap on land that needs clearing, listing who could be sent there.
    ///
    /// Reads a fresh <see cref="FieldPanel"/> a few times a second while open rather than once
    /// on opening. The health bar has to fall while the player watches a fight through it, and
    /// the recall button has to appear the moment it is needed.
    /// </summary>
    public sealed class WorkerPanelView : PopupView
    {
        [SerializeField] TMP_Text title;

        [Tooltip("Their role, or what the land becomes.")]
        [SerializeField] TMP_Text subtitle;

        [Header("Health")]
        [Tooltip("Hidden when the panel has no worker to show.")]
        [SerializeField] GameObject healthRoot;

        [Tooltip("An Image set to Filled.")]
        [SerializeField] Image healthFill;

        [SerializeField] TMP_Text healthLabel;

        [SerializeField] Color healthyColour = new Color(0.4f, 0.8f, 0.3f);
        [SerializeField] Color lowColour = new Color(0.88f, 0.28f, 0.22f);

        [Header("Text")]
        [SerializeField] TMP_Text status;

        [SerializeField] TMP_Text details;

        [Tooltip("Why nothing can be pressed, or the low-health warning.")]
        [SerializeField] TMP_Text reason;

        [Header("Options, left to right")]
        [SerializeField] Button[] optionButtons;
        [SerializeField] Image[] optionImages;
        [SerializeField] TMP_Text[] optionLabels;

        [Tooltip("Distance between the centres of neighbouring buttons.")]
        [SerializeField] float optionSpacing = 280f;

        [SerializeField] Color readyColour = new Color(0.32f, 0.7f, 0.3f);
        [SerializeField] Color blockedColour = new Color(0.6f, 0.6f, 0.6f);

        [Header("Recall")]
        [SerializeField] Button recallButton;
        [SerializeField] TMP_Text recallLabel;

        [Tooltip("Seconds between refreshes while open.")]
        [SerializeField] float refreshInterval = 0.2f;

        readonly StringBuilder lines = new StringBuilder(256);

        Func<FieldPanel> source;
        Action<int> onOption;
        Action onRecall;
        float nextRefresh;

        /// <summary>Whoever opened it, so a worker or an area only ever closes its own panel.</summary>
        public object Owner { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            if (recallButton != null) recallButton.onClick.AddListener(HandleRecall);

            if (optionButtons == null) return;

            for (int i = 0; i < optionButtons.Length; i++)
            {
                int index = i;
                if (optionButtons[i] != null) optionButtons[i].onClick.AddListener(() => HandleOption(index));
            }
        }

        public void Bind(object owner, Func<FieldPanel> panel, Action<int> option, Action recall)
        {
            Owner = owner;
            source = panel;
            onOption = option;
            onRecall = recall;
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

            FieldPanel panel = source();

            SetText(title, panel.Title);
            SetText(subtitle, panel.Subtitle);

            if (healthRoot != null && healthRoot.activeSelf != panel.ShowHealth) healthRoot.SetActive(panel.ShowHealth);

            if (panel.ShowHealth)
            {
                if (healthFill != null)
                {
                    healthFill.fillAmount = Mathf.Clamp01(panel.Health);
                    healthFill.color = panel.HealthLow ? lowColour : healthyColour;
                }

                SetText(healthLabel, panel.HealthText);
            }

            SetText(status, panel.Status);

            lines.Clear();
            for (int i = 0; i < panel.Details.Count; i++)
            {
                if (i > 0) lines.Append('\n');
                lines.Append("- ").Append(panel.Details[i]);
            }
            SetText(details, lines.ToString());

            int slots = optionButtons != null ? optionButtons.Length : 0;
            int shown = Mathf.Min(panel.Options.Count, slots);

            for (int i = 0; i < slots; i++)
            {
                bool visible = i < shown;
                FieldOption option = visible ? panel.Options[i] : default;
                ShowButton(i, visible, option.Label, option.Enabled, (i - (shown - 1) * 0.5f) * optionSpacing);
            }

            if (recallButton != null && recallButton.gameObject.activeSelf != panel.ShowRecall)
                recallButton.gameObject.SetActive(panel.ShowRecall);

            SetText(recallLabel, panel.RecallLabel);
            SetText(reason, panel.Reason);
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

        void HandleOption(int index)
        {
            onOption?.Invoke(index);

            // Still open means it was refused; show why straight away.
            if (IsOpen) Refresh();
        }

        void HandleRecall()
        {
            onRecall?.Invoke();
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
            onOption = null;
            onRecall = null;
        }
    }
}
