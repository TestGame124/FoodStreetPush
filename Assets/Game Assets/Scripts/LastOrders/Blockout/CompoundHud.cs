using LastOrders.Core;
using LastOrders.Data;
using LastOrders.DayCycle;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.Blockout
{
    /// <summary>
    /// Drives the base HUD of section 20: coins, day and phase, the one primary objective, and
    /// the single action that moves the day on.
    ///
    /// Deliberately thin. 21 puts the HUD outside every service's ownership, so this reads
    /// state and never decides any - it subscribes to the ledger, the phase machine, the pass
    /// and the progression flags, and writes text.
    ///
    /// The objective line is the exception worth explaining. It is derived rather than
    /// authored, because the opening of the game is a chain - clear the entrance, cook
    /// something, turn the sign, serve the people who notice - and each link is already a
    /// progression flag. Reading those flags means the line cannot say "cook something" after
    /// the player already has, which is the failure mode of an authored objective.
    /// </summary>
    [DefaultExecutionOrder(1200)]
    public sealed class CompoundHud : MonoBehaviour
    {
        [SerializeField] GameLifetimeScope scope;

        [Header("Wallet")]
        public Text coinsText;
        public Text levelText;

        [Header("Day")]
        public Text dayText;

        [Header("Objective")]
        public Text objectiveTitle;
        public Text objectiveBody;

        [Header("Actions")]
        public Button buildButton;
        public Button worldMapButton;

        [Tooltip("Opens the working day at dawn, and skips the rest of the warning at dusk. " +
                 "Without it the compound sits in a paused dawn forever and nobody ever walks " +
                 "the road.")]
        public Button phaseButton;

        public Text phaseButtonLabel;

        GameComposition composition;
        UI.Hud.HudNarrator narrator;

        /// <summary>Seconds until the dusk and night lines are redrawn: the countdown and the
        /// zombie count change by the second, and nothing raises an event for either.</summary>
        float refreshIn;

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                // Not an error: the HUD is authored into the scene and can be inspected without
                // the composition root, which is useful while dressing the layout.
                Refresh(0);
                if (phaseButton != null) phaseButton.gameObject.SetActive(false);
                return;
            }

            // Excludes inactive objects, and clearing the rubble deactivates it - so this reads
            // false both in a level that never had any and in one where it is already done.
            bool waitsOnEntrance = FindFirstObjectByType<Interaction.ClearableRubble>() != null;
            var visitors = FindFirstObjectByType<FoodStreet.Visitors.VisitorManager>();

            narrator = new UI.Hud.HudNarrator(composition, visitors, waitsOnEntrance);
            narrator.OnMessage += HandleNews;

            composition.Ledger.OnBalanceChanged += HandleBalanceChanged;
            composition.Machine.OnPhaseChanged += HandlePhaseChanged;
            composition.Meals.OnPassChanged += HandlePassChanged;
            composition.Progression.OnFlagSet += HandleFlagSet;

            if (phaseButton != null) phaseButton.onClick.AddListener(AdvancePhase);

            Refresh(composition.Ledger.Get(ResourceType.Coin));
            HandlePhaseChanged(composition.Machine.CurrentPhase, composition.Machine.CurrentPhase);
            RestoreObjective();
        }

        void OnDestroy()
        {
            if (composition == null) return;

            narrator.OnMessage -= HandleNews;
            narrator.Dispose();

            composition.Ledger.OnBalanceChanged -= HandleBalanceChanged;
            composition.Machine.OnPhaseChanged -= HandlePhaseChanged;
            composition.Meals.OnPassChanged -= HandlePassChanged;
            composition.Progression.OnFlagSet -= HandleFlagSet;

            if (phaseButton != null) phaseButton.onClick.RemoveListener(AdvancePhase);
        }

        void Update()
        {
            if (composition == null) return;

            DayPhase phase = composition.Machine.CurrentPhase;
            if (phase != DayPhase.Dusk && phase != DayPhase.Night) return;

            refreshIn -= Time.unscaledDeltaTime;
            if (refreshIn > 0f) return;

            refreshIn = 0.5f;

            // A tap reply or a warning is on the line: it restores the objective itself.
            if (!IsInvoking(nameof(RestoreObjective))) RestoreObjective();
        }

        void HandleNews(string message, UI.Hud.HudTone tone) => ShowMessage(message);

        void HandleBalanceChanged(ResourceType type, int amount)
        {
            if (type == ResourceType.Coin) Refresh(amount);
        }

        void Refresh(int coins)
        {
            if (coinsText != null) coinsText.text = coins.ToString();
        }

        void HandlePassChanged() => RestoreObjective();

        void HandleFlagSet(string flag) => RestoreObjective();

        void HandlePhaseChanged(DayPhase previous, DayPhase current)
        {
            if (dayText != null)
            {
                int day = Mathf.Max(1, composition.Time.CurrentDay);
                dayText.text = $"DAY {day}  •  {Describe(current)}";
            }

            RefreshPhaseButton(current);
            RestoreObjective();
        }

        /// <summary>
        /// 3's four phases in the player's words. The machine's own names come from the other
        /// design, so the mapping lives here rather than being baked into the enum.
        /// </summary>
        public static string Describe(DayPhase phase) => phase switch
        {
            DayPhase.Dawn => "MORNING",
            DayPhase.Day => "OPEN",
            DayPhase.Dusk => "PREPARE",
            DayPhase.Night => "ATTACK",
            DayPhase.Resolution => "RECOVERY",
            _ => phase.ToString().ToUpperInvariant(),
        };

        // --- The one action that moves the day on ------------------------------------------

        void RefreshPhaseButton(DayPhase phase)
        {
            if (phaseButton == null) return;

            bool shown = phase == DayPhase.Dawn || phase == DayPhase.Dusk;
            phaseButton.gameObject.SetActive(shown);

            if (!shown || phaseButtonLabel == null) return;

            // Dawn is paused by design and waits for the player; dusk runs on its own and this
            // only lets a confident player stop waiting for it.
            phaseButtonLabel.text = phase == DayPhase.Dawn ? "Start the day" : "Ready";
        }

        void AdvancePhase()
        {
            if (composition == null) return;

            switch (composition.Machine.CurrentPhase)
            {
                case DayPhase.Dawn:
                    composition.Machine.RequestTransition(DayPhase.Day);
                    break;

                case DayPhase.Dusk:
                    composition.Dusk.SkipWarning();
                    break;
            }
        }

        // --- Objective ----------------------------------------------------------------------

        /// <summary>Sets the single objective line. 20: one primary objective with progress.</summary>
        public void SetObjective(string title, string body)
        {
            if (objectiveTitle != null) objectiveTitle.text = title;
            if (objectiveBody != null) objectiveBody.text = body;
        }

        /// <summary>
        /// Shows the result of a tap, briefly.
        ///
        /// 4: never accept a tap and leave the player guessing whether it worked. Reusing the
        /// objective card's second line rather than adding a toast keeps 20's rule that the
        /// centre of the screen stays clear around the entrance and the dining activity.
        /// </summary>
        public void ShowMessage(string message)
        {
            if (objectiveBody == null || string.IsNullOrEmpty(message)) return;

            objectiveBody.text = message;
            CancelInvoke(nameof(RestoreObjective));
            Invoke(nameof(RestoreObjective), 2.5f);
        }

        /// <summary>
        /// The next link in the opening chain, read off the progression flags by the narrator. This
        /// HUD has no countdown or wave display of its own, so at dusk the title carries the
        /// countdown, and at night the title carries the wave and the line the restaurant's health.
        /// </summary>
        void RestoreObjective()
        {
            if (composition == null)
            {
                SetObjective("Day 1", "Prepare the compound for nightfall");
                return;
            }

            UI.Hud.HudObjective objective = narrator.Objective();

            switch (composition.Machine.CurrentPhase)
            {
                case DayPhase.Dusk:
                    SetObjective($"Zombies in {Mathf.CeilToInt(composition.Dusk.SecondsRemaining)}s", objective.Body);
                    return;

                case DayPhase.Night:
                    var night = composition.Night;
                    string title = night.WaveCount > 0
                        ? $"Wave {Mathf.Clamp(night.WaveIndex + 1, 1, night.WaveCount)} of {night.WaveCount}"
                        : $"Night {Mathf.Max(1, composition.Time.CurrentDay)}";
                    int health = Mathf.RoundToInt(composition.Restaurant.HealthFraction * 100f);
                    SetObjective(title, $"{objective.Body} Restaurant at {health}%.");
                    return;
            }

            SetObjective(objective.Title, objective.Body);
        }
    }
}
