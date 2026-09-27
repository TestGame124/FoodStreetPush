using LastOrders.Core;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Kitchen;
using LastOrders.Progression;
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
        FoodStreet.Visitors.VisitorManager visitors;

        /// <summary>Seconds until the dusk and night lines are redrawn: the countdown and the
        /// zombie count change by the second, and nothing raises an event for either.</summary>
        float refreshIn;

        /// <summary>
        /// Whether this level has a blocked entrance at all.
        ///
        /// Read from the scene rather than assumed. The opening chain starts with clearing the
        /// doorway in the level that has a pile of debris in it, and asking for that in a level
        /// that does not would leave the objective stuck on step one forever.
        /// </summary>
        bool waitsOnEntrance;

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

            composition.Ledger.OnBalanceChanged += HandleBalanceChanged;
            composition.Machine.OnPhaseChanged += HandlePhaseChanged;
            composition.Meals.OnPassChanged += HandlePassChanged;
            composition.Progression.OnFlagSet += HandleFlagSet;
            composition.Night.OnGroupIncoming += HandleGroupIncoming;
            composition.Danger.OnDoorReached += HandleDoorReached;
            composition.Compound.OnUnderAttack += HandleWallUnderAttack;
            composition.Compound.OnStructureDown += HandleWallDown;
            composition.Compound.OnBreached += HandleBreached;
            composition.Compound.OnCleared += HandleCleared;
            composition.Compound.OnRepaired += HandleWallsRepaired;

            if (phaseButton != null) phaseButton.onClick.AddListener(AdvancePhase);

            // Excludes inactive objects, and clearing the rubble deactivates it - so this reads
            // false both in a level that never had any and in one where it is already done.
            waitsOnEntrance = FindFirstObjectByType<Interaction.ClearableRubble>() != null;
            visitors = FindFirstObjectByType<FoodStreet.Visitors.VisitorManager>();

            Refresh(composition.Ledger.Get(ResourceType.Coin));
            HandlePhaseChanged(composition.Machine.CurrentPhase, composition.Machine.CurrentPhase);
            RestoreObjective();
        }

        void OnDestroy()
        {
            if (composition == null) return;

            composition.Ledger.OnBalanceChanged -= HandleBalanceChanged;
            composition.Machine.OnPhaseChanged -= HandlePhaseChanged;
            composition.Meals.OnPassChanged -= HandlePassChanged;
            composition.Progression.OnFlagSet -= HandleFlagSet;
            composition.Night.OnGroupIncoming -= HandleGroupIncoming;
            composition.Danger.OnDoorReached -= HandleDoorReached;
            composition.Compound.OnUnderAttack -= HandleWallUnderAttack;
            composition.Compound.OnStructureDown -= HandleWallDown;
            composition.Compound.OnBreached -= HandleBreached;
            composition.Compound.OnCleared -= HandleCleared;
            composition.Compound.OnRepaired -= HandleWallsRepaired;

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

        /// <summary>The warning before each group. Said here as well as over the road, because
        /// the player may well be looking at the kitchen.</summary>
        void HandleGroupIncoming(Night.WaveGroup group) => ShowMessage($"{group.Label} coming up the road!");

        void HandleDoorReached() => ShowMessage("A zombie is at the diner door!");

        void HandleWallUnderAttack(Perimeter.ICompoundStructure hit) =>
            ShowMessage($"Zombies are smashing the {hit.DisplayName.ToLowerInvariant()}!");

        void HandleWallDown(Perimeter.ICompoundStructure fallen) =>
            ShowMessage($"The {fallen.DisplayName.ToLowerInvariant()} is down - they can get in!");

        void HandleBreached() =>
            ShowMessage("Zombies are inside the walls! The staff are taking cover in the shelters and nobody is eating.");

        void HandleCleared() => ShowMessage("The compound is clear. Back to work.");

        void HandleWallsRepaired(int count) =>
            ShowMessage($"Overnight the walls were patched up ({count} section{(count == 1 ? "" : "s")} repaired).");

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
        static string Describe(DayPhase phase) => phase switch
        {
            DayPhase.Dawn => "MORNING",
            DayPhase.Day => "OPEN",
            DayPhase.Dusk => "PREPARE",
            DayPhase.Night => "NIGHT",
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
        /// The next link in the opening chain, read off the progression flags rather than
        /// authored, so the line can never ask for something the player has already done.
        /// </summary>
        void RestoreObjective()
        {
            if (composition == null)
            {
                SetObjective("Day 1", "Prepare the compound for nightfall");
                return;
            }

            ProgressionService progression = composition.Progression;
            int day = Mathf.Max(1, composition.Time.CurrentDay);

            // The night outranks the opening chain. "Cook something" is the wrong thing to be
            // told while zombies are coming up the road, and a rough morning is news.
            switch (composition.Machine.CurrentPhase)
            {
                case DayPhase.Dusk:
                    ShowDusk();
                    return;

                case DayPhase.Night:
                    ShowNight(day);
                    return;

                case DayPhase.Resolution:
                    SetObjective($"Day {day}", "The sun is coming up");
                    return;

                case DayPhase.Dawn:
                    if (ShowRoughMorning(day)) return;
                    break;
            }

            if (waitsOnEntrance && !progression.Has(ProgressionFlags.EntranceCleared))
            {
                SetObjective("A Fresh Start", "Clear the debris from the diner entrance");
                return;
            }

            if (!progression.Has(ProgressionFlags.FirstMealCooked))
            {
                SetObjective("Something To Sell",
                             "Cook something. Visitors sit down and wait until they are fed.");
                return;
            }

            if (composition.Machine.CurrentPhase == DayPhase.Dawn)
            {
                SetObjective($"Day {day}", "Start the day when you are ready");
                return;
            }

            // The visitor manager knows exactly why the forecourt is empty, and an empty
            // forecourt with no explanation is the single most confusing state this system can
            // be in.
            string blocked = visitors != null ? visitors.StatusReason : null;
            if (!string.IsNullOrEmpty(blocked))
            {
                SetObjective("No Visitors", char.ToUpperInvariant(blocked[0]) + blocked.Substring(1));
                return;
            }

            // Somebody is sat there waiting. That is the most actionable thing on screen, so it
            // outranks the meal count.
            int waiting = visitors != null ? visitors.WaitingToBeServed : 0;
            if (waiting > 0)
            {
                SetObjective($"Day {day}",
                             waiting == 1
                                 ? "A visitor is waiting. Tap them to serve what they asked for."
                                 : $"{waiting} visitors are waiting. Tap one to serve them.");
                return;
            }

            int ready = composition.Meals.TotalReady;
            string pass = ready == 1 ? "1 meal on the pass" : $"{ready} meals on the pass";

            SetObjective($"Day {day}", $"Serve the road. {pass}");
        }

        // --- The night, in the player's words ------------------------------------------------

        /// <summary>The warning: what is coming tonight, and whether anyone is up there to meet it.</summary>
        void ShowDusk()
        {
            Night.WavePlan plan = composition.Context.TonightsWave;
            int coming = plan != null ? plan.Spawns.Count : 0;
            int seconds = Mathf.CeilToInt(composition.Dusk.SecondsRemaining);

            string forecast = coming switch
            {
                0 => "Nothing on the road tonight, by the look of it.",
                1 => "One zombie is drawn in by the lights.",
                _ => $"About {coming} zombies are drawn in by the lights.",
            };

            SetObjective($"Dark in {seconds}s", $"{forecast} {WatchLine()}");
        }

        void ShowNight(int day)
        {
            int live = composition.Night.LiveEnemyCount;

            string zombies = live switch
            {
                0 => "Quiet for now.",
                1 => "1 zombie out there.",
                _ => $"{live} zombies out there.",
            };

            SetObjective($"Night {day}", $"{zombies} {WatchLine()} Everyone else is inside.");
        }

        /// <summary>Last night, if it left a mark: who is in the hospital tent, or how close it
        /// came. False on a morning after a quiet night, which has nothing to report.</summary>
        bool ShowRoughMorning(int day)
        {
            DayReport report = composition.Context.Report;

            // Survivors by name; visitors by what they are, since that is all the player knows
            // them as - "a trucker", not "Trucker".
            var hurt = new System.Collections.Generic.List<string>(report.InjuredSurvivors);
            foreach (string visitor in report.InjuredVisitors) hurt.Add(Indefinite(visitor));

            if (hurt.Count > 0)
            {
                string who = string.Join(" and ", hurt);
                who = char.ToUpperInvariant(who[0]) + who.Substring(1);

                SetObjective("A Rough Night",
                             $"{who} got hurt and {(hurt.Count == 1 ? "is" : "are")} recovering in the hospital tent.");
                return true;
            }

            // Day 1's dawn is the start of the game, not the morning after anything.
            if (report.ZombiesAtTheDoor > 0 && day > 1)
            {
                SetObjective("A Close Call",
                             report.ZombiesAtTheDoor == 1
                                 ? "A zombie reached the diner door last night. Nobody was hurt."
                                 : $"{report.ZombiesAtTheDoor} zombies reached the diner door last night. Nobody was hurt.");
                return true;
            }

            return false;
        }

        /// <summary>"a trucker", "an old farmer".</summary>
        static string Indefinite(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return "a visitor";

            string lower = kind.ToLowerInvariant();
            return "aeiou".IndexOf(lower[0]) >= 0 ? $"an {lower}" : $"a {lower}";
        }

        /// <summary>
        /// Who is on watch, or the problem if nobody is. The line that matters most at dusk: a
        /// tower with nobody in it is the difference between a quiet night and one at the door.
        /// </summary>
        string WatchLine()
        {
            System.Collections.Generic.IReadOnlyList<Defense.DefenseUnit> units = composition.Defense.All;

            string onWatch = null;
            int watching = 0;
            bool anyTower = false;

            for (int i = 0; i < units.Count; i++)
            {
                Defense.DefenseUnit unit = units[i];
                if (!unit.IsBuilt || !unit.NeedsCrew) continue;

                anyTower = true;
                if (unit.Crew == null || !unit.CrewOnDuty) continue;

                watching++;
                onWatch ??= unit.Crew.Name;
            }

            if (!anyTower) return string.Empty;
            if (watching == 0) return "Nobody is in the guard tower!";

            return watching == 1 ? $"{onWatch} is in the tower." : $"{watching} guards are on watch.";
        }
    }
}
