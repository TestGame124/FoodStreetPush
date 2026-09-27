using System.Text;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.DayCycle.States;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI
{
    /// <summary>
    /// The HUD of GDD 23. Binds the top bar, the phase banner, the dawn report and the
    /// Crisis Bar to the services, and swaps the bottom of the screen completely at dusk.
    ///
    /// Everything here is event-driven. The Heat bar redraws when Heat changes, not every
    /// frame, and the bottom bar swaps on the phase-changed event rather than on a check.
    /// The only per-frame work is the clock arc and the dusk countdown, which genuinely do
    /// change continuously.
    ///
    /// The single most important element is the Heat bar with its forecast marker: the GDD's
    /// whole thesis is that the player watches themselves cook their way into tonight's
    /// problem, on a bar they cannot ignore. If that readout is not legible the design does
    /// not land, regardless of how well the systems underneath it are tuned.
    /// </summary>
    public sealed class LastOrdersHUD : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] GameLifetimeScope scope;

        [Header("Top bar")]
        [SerializeField] Text dayText;
        [SerializeField] Text phaseText;
        [SerializeField] Image clockFill;
        [SerializeField] Text clockText;

        [Header("Heat (11)")]
        [SerializeField] Image heatFill;
        [SerializeField] Text heatText;
        [SerializeField] RectTransform forecastMarker;
        [SerializeField] RectTransform heatBarRect;

        [Tooltip("Heat the bar is drawn full at. Beyond this the bar stays full and only the " +
                 "tier marker keeps moving.")]
        [SerializeField] float heatBarMax = 200f;

        [Header("Reputation and stores")]
        [SerializeField] Text reputationText;
        [SerializeField] Text resourcesText;

        [Header("Phase banner")]
        [SerializeField] GameObject phaseBanner;
        [SerializeField] Text bannerText;

        [Header("Dawn")]
        [SerializeField] GameObject dawnPanel;
        [SerializeField] Text dawnReportText;
        [SerializeField] Text broadcastText;

        [Header("Night")]
        [SerializeField] GameObject crisisBar;
        [SerializeField] Text nightStatusText;

        GameComposition composition;
        readonly StringBuilder builder = new StringBuilder(256);
        float bannerHideAt;

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();

            composition = scope != null ? scope.Composition : null;
            if (composition == null)
            {
                Debug.LogError("[LastOrdersHUD] No GameLifetimeScope found. HUD disabled.", this);
                enabled = false;
                return;
            }

            composition.Heat.OnHeatChanged += HandleHeatChanged;
            composition.Heat.OnForecastTierChanged += _ => RedrawHeat();
            composition.Reputation.OnReputationChanged += HandleReputationChanged;
            composition.Ledger.OnBalanceChanged += (_, __) => RedrawResources();
            composition.Machine.OnPhaseChanged += HandlePhaseChanged;

            HandlePhaseChanged(composition.Machine.CurrentPhase, composition.Machine.CurrentPhase);
            RedrawHeat();
            RedrawResources();
            HandleReputationChanged(composition.Reputation.Current);
        }

        void OnDestroy()
        {
            if (composition == null) return;

            composition.Heat.OnHeatChanged -= HandleHeatChanged;
            composition.Reputation.OnReputationChanged -= HandleReputationChanged;
            composition.Machine.OnPhaseChanged -= HandlePhaseChanged;
        }

        void Update()
        {
            if (composition == null) return;

            TimeService time = composition.Time;

            // The clock is drawn as an arc rather than digits. An arc communicates "time
            // remaining" viscerally, which is the point: the day is the currency.
            if (clockFill != null) clockFill.fillAmount = 1f - time.DayProgress;

            if (clockText != null)
            {
                DayPhase phase = composition.Machine.CurrentPhase;

                clockText.text = phase == DayPhase.Day
                    ? $"{Mathf.CeilToInt(time.ClockMinutesRemaining)}m left"
                    : PhaseSubtitle(phase);
            }

            if (phaseBanner != null && phaseBanner.activeSelf && Time.unscaledTime >= bannerHideAt)
                phaseBanner.SetActive(false);

            UpdateNightStatus();
        }

        string PhaseSubtitle(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Dawn: return "Paused";
                case DayPhase.Dusk: return $"{Mathf.CeilToInt(composition.Dusk.SecondsRemaining)}s";
                case DayPhase.Night: return $"{composition.Night.LiveEnemyCount} out there";
                default: return string.Empty;
            }
        }

        void UpdateNightStatus()
        {
            if (nightStatusText == null || !nightStatusText.gameObject.activeInHierarchy) return;

            NightState night = composition.Night;
            nightStatusText.text = $"Dawn in {Mathf.CeilToInt((1f - night.Progress) * 100f)}%   " +
                                   $"Enemies {night.LiveEnemyCount}";
        }

        // --- Phase ------------------------------------------------------------

        void HandlePhaseChanged(DayPhase previous, DayPhase current)
        {
            if (dayText != null) dayText.text = $"DAY {composition.Time.CurrentDay}";
            if (phaseText != null) phaseText.text = current.ToString().ToUpperInvariant();

            // 23: the bottom bar swaps completely at dusk. Build tabs out, Crisis Bar in.
            if (crisisBar != null) crisisBar.SetActive(current.IsSiege());
            if (dawnPanel != null) dawnPanel.SetActive(current == DayPhase.Dawn);

            if (current == DayPhase.Dawn) ShowDawn();

            ShowBanner(BannerFor(current), current == DayPhase.Dusk ? 3f : 2f);
            RedrawHeat();
        }

        static string BannerFor(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Dawn: return "DAWN";
                case DayPhase.Day: return "OPEN FOR BUSINESS";
                case DayPhase.Dusk: return "LAST CALL";
                case DayPhase.Night: return "SHUTTERS DOWN";
                case DayPhase.Resolution: return "DAWN BREAKS";
                default: return string.Empty;
            }
        }

        void ShowBanner(string message, float seconds)
        {
            if (phaseBanner == null || bannerText == null) return;

            bannerText.text = message;
            phaseBanner.SetActive(true);
            bannerHideAt = Time.unscaledTime + seconds;
        }

        void ShowDawn()
        {
            if (broadcastText != null)
            {
                Night.ThreatProfile profile = composition.ThreatProfile.Current;

                broadcastText.text = profile != null
                    ? $"\"{profile.Broadcast}\""
                    : "\"Static. Nothing on the wire tonight.\"";
            }

            if (dawnReportText == null) return;

            DayReport report = composition.Context.Report;

            builder.Clear();
            builder.AppendLine($"Served {report.CustomersServed}   Earned {report.CoinEarned}c");
            builder.AppendLine($"Heat at dusk {report.HeatAtDusk:0}   Killed {report.EnemiesKilled}");
            builder.AppendLine($"Damaged {report.StructuresDamaged}   Wrecked {report.StructuresWrecked}   Lost {report.StructuresDestroyed}");

            // 15.7: the near miss is the story worth telling, so the report says it out loud.
            if (report.ClosestCall < 0.2f && report.Day > 1)
                builder.AppendLine($"<color=#ffd070>Closest call: a wall held at {report.ClosestCall:P0}</color>");

            dawnReportText.text = builder.ToString();
        }

        // --- Bindings ----------------------------------------------------------

        void HandleHeatChanged(float heat) => RedrawHeat();

        void RedrawHeat()
        {
            if (composition == null) return;

            float heat = composition.Heat.IsLocked
                ? composition.Heat.LockedHeat
                : composition.Heat.CurrentHeat;

            if (heatFill != null) heatFill.fillAmount = Mathf.Clamp01(heat / heatBarMax);

            if (heatText != null)
                heatText.text = $"HEAT {heat:0}  ·  tier {composition.Heat.ForecastTier}";

            // The forecast marker is the bar's whole job: it shows where tonight lands if the
            // player stops trading right now, so "one more table" has a visible price.
            if (forecastMarker == null || heatBarRect == null) return;

            HeatWeightsConfig weights = composition.Balance.heatWeights;
            if (weights == null || weights.waveTierThresholds == null) return;

            int tier = Mathf.Clamp(composition.Heat.ForecastTier, 0, weights.waveTierThresholds.Length - 1);
            float threshold = weights.waveTierThresholds[tier];

            float width = heatBarRect.rect.width;
            forecastMarker.anchoredPosition = new Vector2(
                Mathf.Clamp01(threshold / heatBarMax) * width, forecastMarker.anchoredPosition.y);
        }

        void HandleReputationChanged(float value)
        {
            if (reputationText == null) return;
            reputationText.text = $"REP {value:0}  ·  tier {composition.Reputation.Tier}";
        }

        void RedrawResources()
        {
            if (resourcesText == null || composition == null) return;

            Economy.ResourceLedger ledger = composition.Ledger;

            builder.Clear();
            builder.Append($"{ledger.Get(ResourceType.Coin)}c   ");
            builder.Append($"Scrap {ledger.Get(ResourceType.Scrap)}   ");
            builder.Append($"Parts {ledger.Get(ResourceType.Parts)}   ");
            builder.Append($"Ammo {ledger.Get(ResourceType.Ammo)}   ");
            builder.Append($"Med {ledger.Get(ResourceType.Medicine)}");

            resourcesText.text = builder.ToString();
        }

        // --- Buttons (wired by the scene builder) -------------------------------

        /// <summary>Dawn is paused until the player commits to opening. That is what makes
        /// every subsequent minute feel spent rather than lost.</summary>
        public void OnOpenForBusiness()
        {
            if (composition == null) return;
            composition.Machine.RequestTransition(DayPhase.Day);
        }

        /// <summary>15.3. One tap, real cost, no fiddling.</summary>
        public void OnRebuildAll()
        {
            if (composition == null) return;

            var command = new Build.RebuildAllCommand(composition.Ghosts, composition.Balance);
            composition.Commands.Execute(command);

            ShowDawn();
        }

        /// <summary>15.10. Skip serving for a faster build day and a Reputation cost.</summary>
        public void OnClosedForRepairs()
        {
            if (composition == null) return;

            composition.Dawn.ChooseClosedForRepairs();
            composition.Machine.RequestTransition(DayPhase.Day);
        }

        /// <summary>Lets a confident player skip the rest of the dusk warning.</summary>
        public void OnSkipDusk()
        {
            if (composition == null) return;
            composition.Dusk.SkipWarning();
        }

        /// <summary>Bound to each Crisis Bar button by verb index.</summary>
        public void OnCrisisVerb(int verbIndex)
        {
            if (composition == null) return;

            // One grammar: tap ability, tap tile. Without a tile picker yet, the ability
            // resolves at the camera's focus point so the bar is testable end to end.
            Camera cam = Camera.main;
            Vector3 point = cam != null
                ? cam.transform.position + cam.transform.forward * 20f
                : Vector3.zero;

            composition.CrisisBar.Use((CrisisVerb)verbIndex, point);
        }
    }
}
