using LastOrders.Core;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.DayCycle.States;
using LastOrders.Interaction;
using LastOrders.Night;
using LastOrders.Reputation;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI
{
    /// <summary>
    /// The attack cycle on screen.
    ///
    /// Always there: the Safety Reputation meter, 0 to 100, and a line under it saying when the
    /// next attack comes. During the preparation warning that line becomes the countdown; during
    /// the attack it says which wave it is and where from, with the restaurant's health beside it.
    /// In the morning a card says how it went - VICTORY or DEFENSE FAILED, what it paid, and how
    /// far the meter moved - and while anything in the yard is wrecked, one button mends it all.
    ///
    /// Reads state and never decides any. Built by 'Last Orders / Encounters / Set Up Zombie
    /// Attacks (Current Scene)', which wires every field.
    /// </summary>
    [DefaultExecutionOrder(1210)]
    public sealed class EncounterHud : MonoBehaviour
    {
        [SerializeField] GameLifetimeScope scope;

        [Header("Safety Reputation")]
        [SerializeField] Image safetyFill;
        [SerializeField] Text safetyValue;
        [SerializeField] Color lowSafetyColour = new Color(0.90f, 0.38f, 0.28f);
        [SerializeField] Color midSafetyColour = new Color(0.96f, 0.76f, 0.28f);
        [SerializeField] Color highSafetyColour = new Color(0.42f, 0.80f, 0.40f);

        [Header("Next attack, countdown, wave")]
        [SerializeField] Image statusPanel;
        [SerializeField] Text statusText;
        [SerializeField] Color calmPanelColour = new Color(0.16f, 0.15f, 0.17f, 0.82f);
        [SerializeField] Color alarmPanelColour = new Color(0.62f, 0.16f, 0.12f, 0.92f);

        [Header("Restaurant health")]
        [SerializeField] GameObject healthGroup;
        [SerializeField] Image healthFill;
        [SerializeField] Text healthText;

        [Header("Morning result")]
        [SerializeField] GameObject resultPanel;
        [SerializeField] Image resultHeader;
        [SerializeField] Text resultTitle;
        [SerializeField] Text resultBody;
        [SerializeField] Button resultClose;
        [SerializeField] Color victoryColour = new Color(0.30f, 0.62f, 0.32f, 1f);
        [SerializeField] Color defeatColour = new Color(0.70f, 0.20f, 0.16f, 1f);

        [Tooltip("Seconds the morning card stays up before it tucks itself away.")]
        [Min(1f)] [SerializeField] float resultSeconds = 9f;

        [Header("Repair")]
        [SerializeField] Button repairButton;
        [SerializeField] Text repairLabel;

        GameComposition composition;
        float refreshIn;
        float resultHideAt;
        string threatName = "Low";

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                Debug.LogError("[EncounterHud] No GameLifetimeScope, so the attack cycle cannot be shown.", this);
                enabled = false;
                return;
            }

            composition.Reputation.OnReputationChanged += HandleReputationChanged;
            composition.Machine.OnPhaseChanged += HandlePhaseChanged;
            composition.Encounters.OnResolved += HandleResolved;

            if (repairButton != null)
            {
                repairButton.onClick.RemoveListener(Repair);
                repairButton.onClick.AddListener(Repair);
            }

            if (resultClose != null)
            {
                resultClose.onClick.RemoveListener(HideResult);
                resultClose.onClick.AddListener(HideResult);
            }

            HideResult();
            HandleReputationChanged(composition.Reputation.Current);
            Refresh();
        }

        void OnDestroy()
        {
            if (composition == null) return;

            composition.Reputation.OnReputationChanged -= HandleReputationChanged;
            composition.Machine.OnPhaseChanged -= HandlePhaseChanged;
            composition.Encounters.OnResolved -= HandleResolved;
        }

        void Update()
        {
            if (composition == null) return;

            if (resultPanel != null && resultPanel.activeSelf && Time.unscaledTime >= resultHideAt) HideResult();

            refreshIn -= Time.unscaledDeltaTime;
            if (refreshIn > 0f) return;

            refreshIn = 0.25f;
            Refresh();
        }

        void Refresh()
        {
            DrawStatus();
            DrawHealth();
            DrawRepair();
        }

        // --- The meter ----------------------------------------------------------------------------

        void HandleReputationChanged(float value)
        {
            float t = Mathf.Clamp01(value / ReputationService.Max);

            if (safetyFill != null)
            {
                safetyFill.fillAmount = t;
                safetyFill.color = t < 0.5f
                    ? Color.Lerp(lowSafetyColour, midSafetyColour, t * 2f)
                    : Color.Lerp(midSafetyColour, highSafetyColour, (t - 0.5f) * 2f);
            }

            if (safetyValue != null) safetyValue.text = Mathf.RoundToInt(value).ToString();

            RefreshThreat();
        }

        /// <summary>The band the next attack would come from, as it stands.</summary>
        void RefreshThreat() => threatName = Hud.HudNarrator.ThreatName(composition);

        // --- The line under it -------------------------------------------------------------------

        void HandlePhaseChanged(DayPhase previous, DayPhase current)
        {
            RefreshThreat();

            // The card is the morning's news; once the restaurant opens it is old.
            if (current == DayPhase.Day || current == DayPhase.Dusk) HideResult();

            refreshIn = 0f;
        }

        void DrawStatus()
        {
            if (statusText == null) return;

            DayPhase phase = composition.Machine.CurrentPhase;
            bool alarm = false;
            string line;

            switch (phase)
            {
                case DayPhase.Dusk:
                    alarm = true;
                    line = Countdown();
                    break;

                case DayPhase.Night:
                    alarm = true;
                    line = Wave();
                    break;

                case DayPhase.Resolution:
                    line = Hud.HudNarrator.Aftermath(composition);
                    break;

                default:
                    line = NextAttack();
                    break;
            }

            statusText.text = line;
            if (statusPanel != null) statusPanel.color = alarm ? alarmPanelColour : calmPanelColour;
        }

        string NextAttack() => $"{Hud.HudNarrator.NextAttack(composition)}  ·  {threatName} threat";

        string Countdown() => Hud.HudNarrator.Countdown(composition);

        string Wave() => Hud.HudNarrator.Wave(composition);

        // --- The restaurant's health -------------------------------------------------------------

        void DrawHealth()
        {
            if (healthGroup == null) return;

            DayPhase phase = composition.Machine.CurrentPhase;
            var restaurant = composition.Restaurant;

            bool shown = phase == DayPhase.Dusk || phase == DayPhase.Night ||
                         (phase == DayPhase.Resolution && restaurant.IsDamaged);

            if (healthGroup.activeSelf != shown) healthGroup.SetActive(shown);
            if (!shown) return;

            float fraction = restaurant.HealthFraction;

            if (healthFill != null)
            {
                healthFill.fillAmount = fraction;
                healthFill.color = fraction < 0.3f ? lowSafetyColour : Color.Lerp(midSafetyColour, highSafetyColour, (fraction - 0.3f) / 0.7f);
            }

            if (healthText != null)
                healthText.text = $"RESTAURANT  {Mathf.CeilToInt(restaurant.Health)}/{Mathf.RoundToInt(restaurant.MaxHealth)}";
        }

        // --- The morning card ----------------------------------------------------------------------

        void HandleResolved(EncounterResult result)
        {
            if (result == null || resultPanel == null) return;

            if (resultTitle != null) resultTitle.text = result.Headline;
            if (resultHeader != null) resultHeader.color = result.IsVictory ? victoryColour : defeatColour;

            if (resultBody != null)
            {
                resultBody.text = $"{result.Summary}\n{result.RewardLine}\n{result.ReputationLine}";
            }

            resultPanel.SetActive(true);
            resultHideAt = Time.unscaledTime + resultSeconds;
        }

        void HideResult()
        {
            if (resultPanel != null && resultPanel.activeSelf) resultPanel.SetActive(false);
        }

        // --- The one repair ------------------------------------------------------------------------

        void DrawRepair()
        {
            if (repairButton == null) return;

            var outdoor = composition.Outdoor;
            int wrecked = outdoor.WreckedCount;
            bool shown = wrecked > 0 && outdoor.IsRepairTime(out _);

            if (repairButton.gameObject.activeSelf != shown) repairButton.gameObject.SetActive(shown);
            if (!shown) return;

            repairButton.interactable = outdoor.CanRepairAll(out _);

            if (repairLabel != null)
            {
                string what = wrecked == 1 ? "Repair wreck" : $"Repair {wrecked} wrecks";
                repairLabel.text = $"{what}\n<size=22>{outdoor.DescribeCost()}  ·  {outdoor.RepairKits} kit{(outdoor.RepairKits == 1 ? "" : "s")} held</size>";
            }
        }

        void Repair()
        {
            if (composition == null) return;

            // Either way the reply says what happened: mended, or why not - no kit and not
            // enough coins - rather than a button that silently does nothing.
            composition.Outdoor.TryRepairAll(out string message);
            TapFeedback.Report(message);

            refreshIn = 0f;
        }

        public void Configure(GameLifetimeScope lifetimeScope, Image meterFill, Text meterValue, Image panel, Text status,
                              GameObject health, Image healthBar, Text healthLabel, GameObject result, Image header,
                              Text title, Text body, Button close, Button repair, Text repairText)
        {
            scope = lifetimeScope;
            safetyFill = meterFill;
            safetyValue = meterValue;
            statusPanel = panel;
            statusText = status;
            healthGroup = health;
            healthFill = healthBar;
            healthText = healthLabel;
            resultPanel = result;
            resultHeader = header;
            resultTitle = title;
            resultBody = body;
            resultClose = close;
            repairButton = repair;
            repairLabel = repairText;
        }
    }
}
