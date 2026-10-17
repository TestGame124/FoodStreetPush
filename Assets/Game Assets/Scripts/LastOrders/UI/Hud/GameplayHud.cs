using System;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Economy;
using LastOrders.DayCycle;
using LastOrders.DayCycle.States;
using LastOrders.Debugging;
using LastOrders.Night;
using LastOrders.Reputation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Hud
{
    /// <summary>
    /// The gameplay screen: everything on it during play, and how it answers.
    ///
    /// Top left is who you are (level and experience) and what you hold to build with (wood, scrap,
    /// repair kits); top centre is when you are (the day, its phase and the time left in it, and the
    /// restaurant's health while it is under attack); top right is the wallet and how safe the
    /// diner is thought to be, with the next attack under it. The bottom corners are the two places
    /// you go - the shop and the orders - with the one objective above the shop and the one action
    /// that moves the day on between them. The middle of the screen is left to the diner.
    ///
    /// It reads state and decides nothing. What it says comes from <see cref="HudNarrator"/>, shared
    /// with the grey-box HUD; taps on the world answer through <see cref="Interaction.TapFeedback"/>
    /// as toasts. Built and wired by 'Last Orders / UI / Build Gameplay HUD (Current Scene)'.
    /// </summary>
    [DefaultExecutionOrder(1205)]
    public sealed class GameplayHud : MonoBehaviour
    {
        [Serializable]
        public sealed class Groups
        {
            public CanvasGroup everything, topLeft, topCenter, topRight, safety, bottomLeft, bottomCenter, bottomRight;
        }

        [Serializable]
        public sealed class ProfileView
        {
            public TMP_Text level;
            public HudMotion levelMotion;
            public HudBar xp;
            public TMP_Text xpText;
            public HudCounter wood, scrap, kits;
        }

        [Serializable]
        public sealed class WalletView
        {
            public HudCounter coins, gems;
            public Button coinsPlus, gemsPlus;
        }

        [Serializable]
        public sealed class DayView
        {
            public Button card;
            public HudMotion motion;
            public Image disc, icon, ring;
            public TMP_Text title, phase, time;
            public Image chip;
            public HudBar bar;
        }

        [Serializable]
        public sealed class HealthView
        {
            public CanvasGroup group;
            public HudBar bar;
            public TMP_Text text;
            public HudMotion motion;
            public Image vignette;
        }

        [Serializable]
        public sealed class SafetyView
        {
            public Button card;
            public HudMotion motion;
            public TMP_Text value;
            public HudBar bar;
            public Image strip, statusIcon, threatChip;
            public TMP_Text status, threat;
        }

        [Serializable]
        public sealed class ObjectiveView
        {
            public Button card;
            public HudMotion motion;
            public RectTransform panel;
            public CanvasGroup text;
            public Image disc, icon;
            public TMP_Text title, body;
            public GameObject newDot;
        }

        [Serializable]
        public sealed class ActionsView
        {
            public Button shop, orders;
            public GameObject ordersBadge;
            public TMP_Text ordersCount;
            public HudMotion ordersMotion;

            public Button phase;
            public Image phaseFace, phaseGlyph;
            public TMP_Text phaseLabel, phaseSub;
            public HudBar phaseTimer;
            public HudMotion phaseMotion;

            public Button repair;
            public Image repairFace;
            public TMP_Text repairLabel, repairSub;
            public HudMotion repairMotion;
        }

        [Serializable]
        public sealed class ResultView
        {
            public CanvasGroup group;
            public RectTransform card;
            public HudMotion cardMotion;
            public Image ribbon;
            public TMP_Text title, summary, noReward, safety;
            public GameObject coinsRow, xpRow, kitsRow;
            public HudCounter coins, xp, kits;
            public Image safetyArrow;
            public Button close;
            public HudBar timer;
        }

        [Serializable]
        public sealed class DevView
        {
            public Button toggle;
            public GameObject panel;
            public Button overlay;
            public TMP_Text overlayLabel;
        }

        [SerializeField] GameLifetimeScope scope;
        [SerializeField] HudSkin skin;

        public Groups groups = new Groups();
        public ProfileView profile = new ProfileView();
        public WalletView wallet = new WalletView();
        public DayView day = new DayView();
        public HealthView health = new HealthView();
        public SafetyView safety = new SafetyView();
        public ObjectiveView objective = new ObjectiveView();
        public ActionsView actions = new ActionsView();
        public ResultView result = new ResultView();
        public DevView dev = new DevView();

        public HudToasts toasts;
        public HudBanner banner;

        [Tooltip("The toasts' anchor. It sits under the health bar during an attack and moves up into " +
                 "its place the rest of the time.")]
        public RectTransform toastRoot;

        [SerializeField] Vector2 toastHeights = new Vector2(-122f, -192f);

        [Tooltip("Older full-screen panels (cooking, inventory, orders, level up...). While one is " +
                 "open the HUD steps out of the way rather than showing through its dimmed backdrop.")]
        public GameObject[] coveringPanels = new GameObject[0];

        [Tooltip("Older bars along the top of the screen, such as the seed tray. While one is open the " +
                 "top row of the HUD makes room for it.")]
        public GameObject[] topPanels = new GameObject[0];

        [Tooltip("Seconds the morning card stays up before it tucks itself away.")]
        [Min(2f)] [SerializeField] float resultSeconds = 12f;

        [Tooltip("Width of the objective card when open, and when tucked away to its icon.")]
        [SerializeField] Vector2 objectiveWidths = new Vector2(480f, 112f);

        GameComposition composition;
        HudNarrator narrator;
        LastOrdersDebugOverlay overlay;

        float tickIn;
        float ordersIn;
        float duskTotal = 1f;
        float lastHealth = -1f;
        float hitFlash;
        float resultShownAt = -1f;
        bool resultLeaving;
        bool objectiveCollapsed;
        bool healthShown;
        bool hasObjective;
        HudObjective shownObjective;
        int ordersShown = -1;
        DayPhase drawnPhase = (DayPhase)(-1);

        // --- Lifetime ------------------------------------------------------------------------------

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            WireButtons();
            HideResult(true);

            wallet.coins?.Set(CurrenciesController.GetCurrencyAmount(CurrencyType.Coins), false);
            wallet.gems?.Set(CurrenciesController.GetCurrencyAmount(CurrencyType.Gems), false);
            DrawXp(LevelUpSystem.GetLevel(), LevelUpSystem.GetCurrentXP(), LevelUpSystem.GetXPToNextLevel(), false);
            SetUpFlights();

            CurrenciesController.OnCurrencyAmountChanged += HandleCurrency;
            LevelUpSystem.OnXpChanged += HandleXp;
            LevelUpSystem.OnLevelUp += HandleLevelUp;

            if (composition == null)
            {
                // Not an error: the HUD can be looked at without the composition root.
                DrawPhase(DayPhase.Dawn);
                return;
            }

            var visitors = FindFirstObjectByType<FoodStreet.Visitors.VisitorManager>();
            bool waitsOnEntrance = FindFirstObjectByType<Interaction.ClearableRubble>() != null;
            // Waves and a fallen restaurant get a banner here, so the narrator need not say them too.
            narrator = new HudNarrator(composition, visitors, waitsOnEntrance) { AnnouncesWaves = false };
            narrator.OnMessage += Toast;

            composition.Ledger.OnBalanceChanged += HandleBalance;
            composition.Machine.OnPhaseChanged += HandlePhaseChanged;
            composition.Reputation.OnReputationChanged += HandleReputation;
            composition.Encounters.OnResolved += ShowResult;
            composition.Encounters.OnWaveStarted += HandleWaveStarted;
            composition.Encounters.OnDefeated += HandleDefeated;
            composition.Restaurant.OnHealthChanged += HandleHealth;

            profile.wood?.Set(composition.Ledger.Get(ResourceType.Wood), false);
            profile.scrap?.Set(composition.Ledger.Get(ResourceType.Scrap), false);
            profile.kits?.Set(composition.Ledger.Get(ResourceType.RepairKit), false);

            HandleReputation(composition.Reputation.Current);
            lastHealth = composition.Restaurant.Health;

            DayPhase phase = composition.Machine.CurrentPhase;
            DrawPhase(phase);
            Tick();

            // The first thing the player sees: which day it is.
            int dayNumber = Mathf.Max(1, composition.Time.CurrentDay);
            if (phase == DayPhase.Dawn)
                banner?.Show($"DAY {dayNumber}", dayNumber == 1 ? "Welcome to the diner" : "A new morning", HudPalette.Teal);
        }

        void OnDestroy()
        {
            CurrenciesController.OnCurrencyAmountChanged -= HandleCurrency;
            LevelUpSystem.OnXpChanged -= HandleXp;
            LevelUpSystem.OnLevelUp -= HandleLevelUp;

            if (composition == null) return;

            narrator.OnMessage -= Toast;
            narrator.Dispose();

            composition.Ledger.OnBalanceChanged -= HandleBalance;
            composition.Machine.OnPhaseChanged -= HandlePhaseChanged;
            composition.Reputation.OnReputationChanged -= HandleReputation;
            composition.Encounters.OnResolved -= ShowResult;
            composition.Encounters.OnWaveStarted -= HandleWaveStarted;
            composition.Encounters.OnDefeated -= HandleDefeated;
            composition.Restaurant.OnHealthChanged -= HandleHealth;
        }

        void WireButtons()
        {
            Bind(actions.shop, () => UIGame.GetUI()?.ShopButton());
            Bind(actions.orders, () => UIGame.GetUI()?.OrderButtonClick());
            Bind(wallet.coinsPlus, () => UIGame.GetUI()?.StoreButton());
            Bind(wallet.gemsPlus, () => UIGame.GetUI()?.StoreButton());
            Bind(actions.phase, AdvancePhase);
            Bind(actions.repair, Repair);
            Bind(result.close, () => HideResult(false));
            Bind(objective.card, ToggleObjective);
            Bind(safety.card, ExplainSafety);
            Bind(day.card, ExplainDay);

            bool devTools = Debug.isDebugBuild;
            if (dev.toggle != null) dev.toggle.gameObject.SetActive(devTools);
            if (dev.panel != null) dev.panel.SetActive(false);

            overlay = FindFirstObjectByType<LastOrdersDebugOverlay>();
            Bind(dev.toggle, () => dev.panel.SetActive(!dev.panel.activeSelf));
            Bind(dev.overlay, ToggleOverlay);
            DrawOverlayLabel();
        }

        static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        // --- Every frame ---------------------------------------------------------------------------

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            DrawCover(dt);
            DrawVignette(dt);
            DrawToastRoot(dt);
            DrawObjectiveFold(dt);
            DrawResultTimer(dt);

            if (composition == null) return;

            tickIn -= dt;
            if (tickIn <= 0f)
            {
                tickIn = 0.2f;
                Tick();
            }

            ordersIn -= dt;
            if (ordersIn <= 0f)
            {
                ordersIn = 1f;
                DrawOrders();
            }
        }

        /// <summary>The lines that change by the second without raising anything: clocks, counts, waves.</summary>
        void Tick()
        {
            DayPhase phase = composition.Machine.CurrentPhase;
            if (phase != drawnPhase) DrawPhase(phase);

            DrawDay(phase);
            DrawStatus(phase);
            DrawHealth(phase);
            DrawPhaseButton(phase);
            DrawRepair();
            DrawObjective();
        }

        // --- Covering, edit mode -------------------------------------------------------------------

        void DrawCover(float dt)
        {
            bool covered = AnyActive(coveringPanels);
            bool topCovered = AnyActive(topPanels);
            bool editing = GameHandler.currentState == GameHandler.GameState.EditState;

            Fade(groups.everything, !covered, dt);
            // The shop's category column runs down the left edge, over where the level sits.
            Fade(groups.topLeft, !topCovered && !editing, dt);
            Fade(groups.topCenter, !topCovered, dt);
            Fade(groups.topRight, !topCovered, dt);
            Fade(groups.safety, !topCovered && !editing, dt);
            Fade(groups.bottomLeft, !editing, dt);
            Fade(groups.bottomCenter, !editing, dt);
            Fade(groups.bottomRight, !editing, dt);
        }

        static bool AnyActive(GameObject[] panels)
        {
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] != null && panels[i].activeInHierarchy) return true;
            }

            return false;
        }

        static void Fade(CanvasGroup group, bool shown, float dt)
        {
            if (group == null) return;

            float goal = shown ? 1f : 0f;
            if (Mathf.Approximately(group.alpha, goal) && group.blocksRaycasts == shown) return;

            group.alpha = Mathf.MoveTowards(group.alpha, goal, dt * 7f);
            bool live = group.alpha > 0.5f;
            group.interactable = live;
            group.blocksRaycasts = live;
        }

        // --- Wallet, level, resources --------------------------------------------------------------

        // --- Rewards flying in -------------------------------------------------------------------------

        RewardFlights flights;
        int routedCoins, routedGems, xpLevel;
        float xpAt, xpToNext;

        /// <summary>Coins, gems and stars earned somewhere in the world fly to their counters (see <see cref="RewardFlights"/>).</summary>
        void SetUpFlights()
        {
            flights = GetComponent<RewardFlights>();
            if (flights == null) flights = gameObject.AddComponent<RewardFlights>();
            flights.Configure(skin, () => groups.everything == null || groups.everything.alpha > 0.5f);

            if (skin != null)
            {
                flights.SetTarget(RewardKind.Coins, Target(wallet.coins, skin[HudSprites.IconCoin], HudPalette.Gold, 5, 7));
                flights.SetTarget(RewardKind.Gems, Target(wallet.gems, skin[HudSprites.IconGem], HudPalette.TealLight, 1, 5));

                Transform badge = profile.levelMotion != null ? profile.levelMotion.transform : profile.level != null ? profile.level.transform : null;
                flights.SetTarget(RewardKind.Stars, new RewardFlights.Target
                {
                    icon = badge as RectTransform,
                    kick = profile.levelMotion,
                    sprite = skin[HudSprites.IconStar],
                    tint = HudPalette.Gold,
                    perIcon = 1,
                    maxIcons = 5,
                });
            }

            routedCoins = CurrenciesController.GetCurrencyAmount(CurrencyType.Coins);
            routedGems = CurrenciesController.GetCurrencyAmount(CurrencyType.Gems);
            xpLevel = LevelUpSystem.GetLevel();
            xpAt = LevelUpSystem.GetCurrentXP();
            xpToNext = LevelUpSystem.GetXPToNextLevel();
        }

        static RewardFlights.Target Target(HudCounter counter, Sprite sprite, Color tint, int perIcon, int maxIcons)
        {
            // The counter's own icon, so the coins land in the coin.
            Transform icon = counter != null ? counter.transform.Find("Icon") : null;
            if (icon == null && counter != null) icon = counter.transform;

            return new RewardFlights.Target
            {
                icon = icon as RectTransform,
                kick = counter != null ? counter.GetComponent<HudMotion>() : null,
                sprite = sprite,
                tint = tint,
                perIcon = perIcon,
                maxIcons = maxIcons,
            };
        }

        void HandleCurrency(CurrencyType type, int amount)
        {
            if (type == CurrencyType.Coins)
            {
                int gained = amount - routedCoins;
                routedCoins = amount;
                Route(RewardKind.Coins, gained, () => wallet.coins?.Set(CurrenciesController.GetCurrencyAmount(CurrencyType.Coins)));
            }
            else if (type == CurrencyType.Gems)
            {
                int gained = amount - routedGems;
                routedGems = amount;
                Route(RewardKind.Gems, gained, () => wallet.gems?.Set(CurrenciesController.GetCurrencyAmount(CurrencyType.Gems)));
            }
        }

        void HandleXp(int level, float xp, float toNext)
        {
            // Experience resets at each level, so what was gained is measured across the level-up.
            float gained = level == xpLevel ? xp - xpAt : level > xpLevel ? xpToNext - xpAt + xp : 0f;
            xpLevel = level;
            xpAt = xp;
            xpToNext = toNext;

            Route(RewardKind.Stars, Mathf.RoundToInt(gained),
                  () => DrawXp(LevelUpSystem.GetLevel(), LevelUpSystem.GetCurrentXP(), LevelUpSystem.GetXPToNextLevel(), true));
        }

        void Route(RewardKind kind, int gained, Action show)
        {
            if (flights != null) flights.Route(kind, gained, show);
            else show();
        }

        void DrawXp(int level, float xp, float toNext, bool animate)
        {
            if (profile.level != null) profile.level.text = Mathf.Max(1, level).ToString();
            profile.xp?.Set(toNext > 0f ? xp / toNext : 0f, !animate);
            if (profile.xpText != null) profile.xpText.text = $"{Mathf.RoundToInt(xp)} / {Mathf.RoundToInt(toNext)} XP";
        }

        void HandleLevelUp(int level)
        {
            profile.levelMotion?.Punch(0.35f);
            banner?.Show($"LEVEL {level}!", "New things to build in the shop", HudPalette.Gold);
        }

        void HandleBalance(ResourceType type, int amount)
        {
            switch (type)
            {
                case ResourceType.Wood: profile.wood?.Set(amount); break;
                case ResourceType.Scrap: profile.scrap?.Set(amount); break;
                case ResourceType.RepairKit: profile.kits?.Set(amount); break;
            }
        }

        // --- The day card --------------------------------------------------------------------------

        void HandlePhaseChanged(DayPhase previous, DayPhase current)
        {
            DrawPhase(current);
            Tick();

            if (current == DayPhase.Day || current == DayPhase.Dusk) HideResult(false);
            if (current == DayPhase.Dusk) duskTotal = Mathf.Max(1f, composition.Dusk.SecondsRemaining);

            Announce(current);
        }

        /// <summary>The look of each phase: what the chip says, its colour, the icon on the disc.</summary>
        static (string label, Color chip, string icon, Color disc) Look(DayPhase phase) => phase switch
        {
            DayPhase.Dawn => ("MORNING", HudPalette.Gold, HudSprites.IconDawn, new Color(1f, 0.93f, 0.76f)),
            DayPhase.Day => ("OPEN", HudPalette.Teal, HudSprites.IconSun, new Color(1f, 0.95f, 0.78f)),
            DayPhase.Dusk => ("PREPARE", HudPalette.Amber, HudSprites.IconMoon, new Color(0.80f, 0.86f, 0.97f)),
            DayPhase.Night => ("ATTACK", HudPalette.Red, HudSprites.IconZombie, new Color(0.98f, 0.80f, 0.76f)),
            DayPhase.Resolution => ("RECOVERY", HudPalette.Mint, HudSprites.IconDawn, new Color(0.85f, 0.95f, 0.84f)),
            _ => (phase.ToString().ToUpperInvariant(), HudPalette.Teal, HudSprites.IconSun, Color.white),
        };

        void DrawPhase(DayPhase phase)
        {
            drawnPhase = phase;
            (string label, Color chip, string icon, Color disc) = Look(phase);

            int dayNumber = composition != null ? Mathf.Max(1, composition.Time.CurrentDay) : 1;
            if (day.title != null) day.title.text = $"DAY {dayNumber}";
            if (day.phase != null) day.phase.text = label;
            if (day.chip != null) day.chip.color = chip;
            if (day.disc != null) day.disc.color = disc;
            if (day.icon != null && skin != null) day.icon.sprite = skin[icon];
            day.motion?.Punch(0.08f);
        }

        void DrawDay(DayPhase phase)
        {
            string time;
            float bar = -1f;
            Color barColour = HudPalette.Teal;
            float ring = -1f;

            switch (phase)
            {
                case DayPhase.Dawn:
                    time = "Closed - open when ready";
                    break;

                case DayPhase.Day:
                    float left = composition.Time.ClockMinutesRemaining;
                    time = left >= 1f ? $"{Mathf.CeilToInt(left)} min till closing" : "Closing time";
                    bar = 1f - composition.Time.DayProgress;
                    barColour = left <= 10f ? HudPalette.Amber : HudPalette.Teal;
                    break;

                case DayPhase.Dusk:
                    float seconds = composition.Dusk.SecondsRemaining;
                    duskTotal = Mathf.Max(duskTotal, seconds);
                    time = $"Zombies in {HudNarrator.Clock(seconds)}";
                    bar = ring = seconds / Mathf.Max(1f, duskTotal);
                    barColour = HudPalette.Red;
                    break;

                case DayPhase.Night:
                    NightState night = composition.Night;
                    if (night.WaveCount > 0)
                    {
                        int shown = Mathf.Clamp(night.WaveIndex + 1, 1, night.WaveCount);
                        time = night.BreakSecondsRemaining > 0f
                            ? $"Next wave in {Mathf.CeilToInt(night.BreakSecondsRemaining)}s"
                            : $"Wave {shown}/{night.WaveCount}  ·  {night.LiveEnemyCount} left";
                    }
                    else
                    {
                        time = night.LiveEnemyCount == 1 ? "1 zombie out there" : $"{night.LiveEnemyCount} zombies out there";
                    }

                    bar = 1f - night.Progress;
                    barColour = HudPalette.Red;
                    break;

                default:
                    time = "The zombies are leaving";
                    break;
            }

            if (day.time != null) day.time.text = time;

            if (day.bar != null)
            {
                bool shownBar = bar >= 0f;
                if (day.bar.gameObject.activeSelf != shownBar) day.bar.gameObject.SetActive(shownBar);
                if (shownBar)
                {
                    day.bar.Set(bar);
                    day.bar.SetColour(barColour);
                }
            }

            if (day.ring != null)
            {
                bool shownRing = ring >= 0f;
                if (day.ring.gameObject.activeSelf != shownRing) day.ring.gameObject.SetActive(shownRing);
                if (shownRing) day.ring.fillAmount = ring;
            }
        }

        void ExplainDay()
        {
            if (composition == null) return;

            string line = composition.Machine.CurrentPhase switch
            {
                DayPhase.Dawn => "The diner is closed. Get things ready, then tap OPEN UP to start the day.",
                DayPhase.Day => $"The diner is open. {Mathf.CeilToInt(composition.Time.ClockMinutesRemaining)} minutes until closing time.",
                DayPhase.Dusk => $"Zombies arrive in {HudNarrator.Clock(composition.Dusk.SecondsRemaining)}. Place and repair your defenses!",
                DayPhase.Night => HudNarrator.Wave(composition),
                _ => "The zombies are slinking off. A new day is coming.",
            };

            Toast(line, HudTone.Info);
        }

        // --- Announcements -------------------------------------------------------------------------

        void Announce(DayPhase phase)
        {
            if (banner == null) return;

            int dayNumber = Mathf.Max(1, composition.Time.CurrentDay);
            switch (phase)
            {
                case DayPhase.Dawn:
                    banner.Show($"DAY {dayNumber}", "A new morning", HudPalette.Teal);
                    break;

                case DayPhase.Day:
                    banner.Show("DINER OPEN", "Serve the road", HudPalette.Teal);
                    break;

                case DayPhase.Dusk:
                    WavePlan plan = composition.Encounters.Current;
                    if (plan != null)
                    {
                        string waves = plan.Waves.Count == 1 ? "1 wave" : $"{plan.Waves.Count} waves";
                        if (plan.IsBigWave)
                            banner.Show("BIG WAVE INCOMING", $"{plan.ThreatName} threat  ·  {waves}  ·  get ready!", HudPalette.Red);
                        else
                            banner.Show("ZOMBIES INCOMING", $"A small pack  ·  {plan.EnemyCount} zombies  ·  get ready!", HudPalette.Red);
                    }
                    else
                    {
                        banner.Show("NIGHTFALL", "Lock the doors", HudPalette.Night);
                    }

                    break;

                case DayPhase.Night:
                    if (composition.Night.WaveCount == 0) banner.Show($"NIGHT {dayNumber}", "Hold the diner", HudPalette.Red);
                    break;
            }
        }

        void HandleWaveStarted(int index, int total, EncounterWave wave)
        {
            string sides = composition.Encounters.SidesOf(wave);
            banner?.Show(total == 1 ? "HERE THEY COME" : $"WAVE {index + 1} OF {total}", $"{wave.Count} zombies from {sides}", HudPalette.Red);
        }

        void HandleDefeated() => banner?.Show("RESTAURANT FALLEN", "The zombies broke in", HudPalette.Red);

        // --- Safety and the next attack ------------------------------------------------------------

        void HandleReputation(float value)
        {
            float t = Mathf.Clamp01(value / ReputationService.Max);

            if (safety.value != null)
            {
                string text = Mathf.RoundToInt(value).ToString();
                if (safety.value.text != text && !string.IsNullOrEmpty(safety.value.text)) safety.motion?.Punch(0.1f);
                safety.value.text = text;
            }

            safety.bar?.Set(t);
            safety.bar?.SetColour(HudPalette.Meter(t));
        }

        static Color Threat(string name) => name switch
        {
            "Low" => HudPalette.Mint,
            "Medium" => HudPalette.Amber,
            "High" => HudPalette.Red,
            _ => HudPalette.Teal,
        };

        void DrawStatus(DayPhase phase)
        {
            if (safety.status == null) return;

            bool alarm = phase == DayPhase.Dusk || phase == DayPhase.Night;
            string line, threat;
            string icon = HudSprites.IconZombie;

            switch (phase)
            {
                // The clock and the wave are on the day card; this line says what is coming and
                // what to do about it.
                case DayPhase.Dusk:
                    WavePlan plan = composition.Encounters.Current;
                    line = plan != null && plan.Spawns.Count > 0
                        ? $"{plan.Spawns.Count} zombies in {plan.Waves.Count} wave{(plan.Waves.Count == 1 ? "" : "s")}"
                        : "Place and repair your defenses";
                    threat = plan?.ThreatName ?? HudNarrator.ThreatName(composition);
                    break;

                case DayPhase.Night:
                    line = composition.Restaurant.HealthFraction < 0.3f ? "The diner is badly hurt!" : "Hold the diner!";
                    threat = composition.Encounters.Current?.ThreatName ?? HudNarrator.ThreatName(composition);
                    break;

                case DayPhase.Resolution:
                    line = HudNarrator.Aftermath(composition);
                    threat = HudNarrator.ThreatName(composition);
                    icon = composition.Encounters.LastResult == null || composition.Encounters.LastResult.IsVictory
                        ? HudSprites.IconTrophy
                        : HudSprites.IconZombie;
                    break;

                default:
                    line = HudNarrator.NextAttack(composition);
                    threat = HudNarrator.ThreatName(composition);
                    break;
            }

            safety.status.text = line;
            safety.status.color = alarm ? Color.white : HudPalette.Ink;
            if (safety.strip != null) safety.strip.color = alarm ? HudPalette.Red : new Color(0.92f, 0.87f, 0.78f);
            if (safety.statusIcon != null && skin != null) safety.statusIcon.sprite = skin[icon];
            if (safety.threat != null) safety.threat.text = threat.ToUpperInvariant();
            if (safety.threatChip != null) safety.threatChip.color = Threat(threat);
        }

        void ExplainSafety()
        {
            if (composition == null) return;

            int value = Mathf.RoundToInt(composition.Reputation.Current);
            // Who comes and how hard the night is both ride on Safety and the level, so the tap says both.
            var visitors = FindFirstObjectByType<FoodStreet.Visitors.VisitorManager>();
            string clientele = visitors != null ? visitors.DescribeClientele() : string.Empty;

            Toast($"Safety {value}/100. Safety and your level bring more visitors - and tougher zombies. " +
                  $"{clientele} {HudNarrator.ThreatBreakdown(composition)}", HudTone.Info);
        }

        // --- The restaurant under attack -----------------------------------------------------------

        void HandleHealth(float now, float max)
        {
            if (lastHealth >= 0f && now < lastHealth - 0.01f && composition.Restaurant.IsUnderAttack)
            {
                health.motion?.Shake(9f);
                hitFlash = Mathf.Min(0.6f, hitFlash + 0.35f);
            }

            lastHealth = now;
            DrawHealth(composition.Machine.CurrentPhase);
        }

        void DrawHealth(DayPhase phase)
        {
            if (health.group == null) return;

            var restaurant = composition.Restaurant;
            bool shown = phase == DayPhase.Dusk || phase == DayPhase.Night ||
                         (phase == DayPhase.Resolution && restaurant.IsDamaged);
            healthShown = shown;

            health.group.alpha = Mathf.MoveTowards(health.group.alpha, shown ? 1f : 0f, 0.35f);
            if (!shown && health.group.alpha <= 0f) return;

            float fraction = restaurant.HealthFraction;
            health.bar?.Set(fraction);
            health.bar?.SetColour(HudPalette.Meter(fraction));

            if (health.text != null)
                health.text.text = $"{Mathf.CeilToInt(restaurant.Health)} / {Mathf.RoundToInt(restaurant.MaxHealth)}";
        }

        void DrawToastRoot(float dt)
        {
            if (toastRoot == null) return;

            Vector2 at = toastRoot.anchoredPosition;
            float goal = healthShown ? toastHeights.y : toastHeights.x;
            if (Mathf.Abs(at.y - goal) < 0.25f) return;

            at.y = Mathf.Lerp(at.y, goal, 1f - Mathf.Exp(-10f * dt));
            toastRoot.anchoredPosition = at;
        }

        /// <summary>A red edge that breathes during an attack and flares when the restaurant is hit.</summary>
        void DrawVignette(float dt)
        {
            if (health.vignette == null) return;

            float steady = 0f;
            if (composition != null)
            {
                DayPhase phase = composition.Machine.CurrentPhase;
                if (phase == DayPhase.Night) steady = 0.12f + 0.05f * Mathf.Sin(Time.unscaledTime * 2.4f);
                else if (phase == DayPhase.Dusk) steady = 0.06f;
            }

            hitFlash = Mathf.MoveTowards(hitFlash, 0f, dt * 1.4f);

            Color c = health.vignette.color;
            float a = Mathf.Max(steady, hitFlash);
            c.a = Mathf.Lerp(c.a, a, 1f - Mathf.Exp(-10f * dt));
            health.vignette.color = c;

            bool visible = c.a > 0.004f;
            if (health.vignette.enabled != visible) health.vignette.enabled = visible;
        }

        // --- The objective -------------------------------------------------------------------------

        void DrawObjective()
        {
            if (narrator == null) return;

            HudObjective next = narrator.Objective();
            if (hasObjective && next.SameAs(shownObjective)) return;

            bool headline = !hasObjective || next.Title != shownObjective.Title || next.Icon != shownObjective.Icon;
            hasObjective = true;
            shownObjective = next;

            if (objective.title != null) objective.title.text = next.Title;
            if (objective.body != null) objective.body.text = next.Body;
            if (objective.icon != null && skin != null) objective.icon.sprite = skin[next.Icon];
            if (objective.disc != null) objective.disc.color = Disc(next.Tone);

            if (!headline) return;

            objective.motion?.Punch(0.07f);
            if (objectiveCollapsed && objective.newDot != null) objective.newDot.SetActive(true);
        }

        static Color Disc(HudTone tone) => tone switch
        {
            HudTone.Good => new Color(0.82f, 0.93f, 0.80f),
            HudTone.Warning => new Color(1f, 0.88f, 0.70f),
            HudTone.Danger => new Color(1f, 0.80f, 0.76f),
            _ => new Color(0.80f, 0.92f, 0.90f),
        };

        void ToggleObjective()
        {
            objectiveCollapsed = !objectiveCollapsed;
            if (!objectiveCollapsed && objective.newDot != null) objective.newDot.SetActive(false);
        }

        /// <summary>Tucks the card away to its icon, or opens it back up, easing between the two.</summary>
        void DrawObjectiveFold(float dt)
        {
            if (objective.panel == null) return;

            float goal = objectiveCollapsed ? objectiveWidths.y : objectiveWidths.x;
            Vector2 size = objective.panel.sizeDelta;
            if (Mathf.Abs(size.x - goal) > 0.5f)
            {
                size.x = Mathf.Lerp(size.x, goal, 1f - Mathf.Exp(-14f * dt));
                objective.panel.sizeDelta = size;
            }

            if (objective.text != null)
            {
                objective.text.alpha = Mathf.MoveTowards(objective.text.alpha, objectiveCollapsed ? 0f : 1f, dt * (objectiveCollapsed ? 9f : 4f));
            }
        }

        // --- The one action that moves the day on -------------------------------------------------

        void DrawPhaseButton(DayPhase phase)
        {
            if (actions.phase == null) return;

            bool shown = phase == DayPhase.Dawn || phase == DayPhase.Dusk;
            if (actions.phase.gameObject.activeSelf != shown)
            {
                actions.phase.gameObject.SetActive(shown);
                if (shown) actions.phaseMotion?.Punch(0.22f);
            }

            if (!shown) return;

            bool dawn = phase == DayPhase.Dawn;
            if (actions.phaseFace != null && skin != null) actions.phaseFace.sprite = skin[dawn ? HudSprites.ButtonTeal : HudSprites.ButtonRed];
            if (actions.phaseGlyph != null && skin != null) actions.phaseGlyph.sprite = skin[dawn ? HudSprites.GlyphPlay : HudSprites.GlyphCheck];
            if (actions.phaseLabel != null) actions.phaseLabel.text = dawn ? "OPEN UP" : "READY!";

            if (actions.phaseSub != null)
            {
                actions.phaseSub.text = dawn
                    ? $"Start day {Mathf.Max(1, composition.Time.CurrentDay)}"
                    : $"Skip the wait  ·  {HudNarrator.Clock(composition.Dusk.SecondsRemaining)}";
            }

            if (actions.phaseTimer != null)
            {
                if (actions.phaseTimer.gameObject.activeSelf == dawn) actions.phaseTimer.gameObject.SetActive(!dawn);
                if (!dawn) actions.phaseTimer.Set(composition.Dusk.SecondsRemaining / Mathf.Max(1f, duskTotal));
            }

            if (actions.phaseMotion != null) actions.phaseMotion.Breathe = dawn;
        }

        void AdvancePhase()
        {
            if (composition == null) return;

            switch (composition.Machine.CurrentPhase)
            {
                case DayPhase.Dawn:
                    composition.Machine.RequestTransition(DayPhase.Day);
                    break;

                // Dusk runs on its own; this only lets a confident player stop waiting for it.
                case DayPhase.Dusk:
                    composition.Dusk.SkipWarning();
                    break;
            }
        }

        // --- Repairs --------------------------------------------------------------------------------

        void DrawRepair()
        {
            if (actions.repair == null) return;

            var outdoor = composition.Outdoor;
            int wrecked = outdoor.WreckedCount;
            bool shown = wrecked > 0 && outdoor.IsRepairTime(out _);

            if (actions.repair.gameObject.activeSelf != shown)
            {
                actions.repair.gameObject.SetActive(shown);
                if (shown) actions.repairMotion?.Punch(0.22f);
            }

            if (!shown) return;

            bool can = outdoor.CanRepairAll(out _);
            if (actions.repairFace != null && skin != null) actions.repairFace.sprite = skin[can ? HudSprites.ButtonRed : HudSprites.ButtonGrey];
            if (actions.repairLabel != null) actions.repairLabel.text = wrecked == 1 ? "REPAIR WRECK" : $"REPAIR {wrecked}";

            if (actions.repairSub != null)
            {
                int kits = outdoor.RepairKits;
                actions.repairSub.text = $"{outdoor.DescribeCost()}  ·  {kits} kit{(kits == 1 ? "" : "s")}";
            }

            if (actions.repairMotion != null) actions.repairMotion.Breathe = can;
        }

        /// <summary>Always answers: mended, or why not - rather than a button that silently does nothing.</summary>
        void Repair()
        {
            if (composition == null) return;

            bool done = composition.Outdoor.TryRepairAll(out string message);
            Toast(message, done ? HudTone.Good : HudTone.Warning);
            tickIn = 0f;
        }

        // --- Orders ---------------------------------------------------------------------------------

        /// <summary>The badge counts the orders that can be handed in right now, not just the open ones.</summary>
        void DrawOrders()
        {
            if (actions.ordersBadge == null) return;

            int ready = 0;
            var orders = OrderSystem.instance != null ? OrderSystem.instance.GetAllOrders() : null;
            var inventory = InventorySystem.instance;

            if (orders != null && inventory != null)
            {
                foreach (OrderSystem.Order order in orders)
                {
                    try
                    {
                        if (order != null && order.CanClaim(inventory)) ready++;
                    }
                    catch (Exception)
                    {
                        // An order the legacy board has not finished building yet. It is counted
                        // next second instead.
                    }
                }
            }

            if (ready == ordersShown) return;

            bool more = ready > ordersShown && ordersShown >= 0;
            ordersShown = ready;

            actions.ordersBadge.SetActive(ready > 0);
            if (actions.ordersCount != null) actions.ordersCount.text = ready.ToString();
            if (more) actions.ordersMotion?.Punch(0.2f);
        }

        // --- The morning card ------------------------------------------------------------------------

        void ShowResult(EncounterResult outcome)
        {
            if (outcome == null || result.group == null) return;

            result.group.gameObject.SetActive(true);
            result.group.alpha = 0f;
            resultLeaving = false;
            resultShownAt = Time.unscaledTime;

            if (result.ribbon != null) result.ribbon.color = outcome.IsVictory ? HudPalette.Mint : HudPalette.Red;
            if (result.title != null) result.title.text = outcome.Headline;
            if (result.summary != null) result.summary.text = outcome.Summary;

            bool coins = outcome.IsVictory && outcome.Coins > 0;
            bool xp = outcome.IsVictory && outcome.Experience > 0;
            bool kits = outcome.IsVictory && outcome.RepairKits > 0;

            Row(result.coinsRow, result.coins, coins, outcome.Coins);
            Row(result.xpRow, result.xp, xp, outcome.Experience);
            Row(result.kitsRow, result.kits, kits, outcome.RepairKits);
            if (result.noReward != null) result.noReward.gameObject.SetActive(!coins && !xp && !kits);

            float after = outcome.ReputationAfter;
            float before = after - outcome.ReputationDelta;
            float delta = Mathf.Round(outcome.ReputationDelta * 10f) / 10f;

            if (result.safety != null)
            {
                string sign = delta > 0f ? "+" : delta < 0f ? "-" : "±";
                result.safety.text = $"Safety  {Mathf.RoundToInt(before)} <size=130%>→</size> {Mathf.RoundToInt(after)}   <size=80%>({sign}{Mathf.Abs(delta):0.#})</size>";
            }

            if (result.safetyArrow != null && skin != null)
            {
                result.safetyArrow.sprite = skin[delta >= 0f ? HudSprites.GlyphUp : HudSprites.GlyphDown];
                result.safetyArrow.color = delta >= 0f ? HudPalette.Mint : HudPalette.Red;
            }

            result.timer?.Set(1f, true);
            if (result.card != null) result.card.localScale = Vector3.one * 0.85f;
            result.cardMotion?.Punch(0.1f);
        }

        static void Row(GameObject row, HudCounter counter, bool shown, int amount)
        {
            if (row != null) row.SetActive(shown);
            if (!shown || counter == null) return;

            counter.Set(0, false);
            counter.Set(amount);
        }

        void HideResult(bool instant)
        {
            if (result.group == null) return;

            if (instant)
            {
                result.group.alpha = 0f;
                result.group.gameObject.SetActive(false);
                resultShownAt = -1f;
                return;
            }

            if (result.group.gameObject.activeSelf) resultLeaving = true;
        }

        void DrawResultTimer(float dt)
        {
            if (result.group == null || !result.group.gameObject.activeSelf) return;

            if (resultLeaving)
            {
                result.group.alpha = Mathf.MoveTowards(result.group.alpha, 0f, dt * 5f);
                if (result.group.alpha <= 0f) HideResult(true);
                return;
            }

            result.group.alpha = Mathf.MoveTowards(result.group.alpha, 1f, dt * 5f);

            if (result.card != null)
            {
                float s = Mathf.Lerp(result.card.localScale.x, 1f, 1f - Mathf.Exp(-12f * dt));
                result.card.localScale = new Vector3(s, s, 1f);
            }

            float elapsed = Time.unscaledTime - resultShownAt;
            result.timer?.Set(1f - elapsed / resultSeconds);
            if (elapsed >= resultSeconds) resultLeaving = true;
        }

        // --- Messages --------------------------------------------------------------------------------

        /// <summary>The answer to a tap on the world, as a toast. Called by <see cref="Interaction.TapFeedback"/>.</summary>
        public void ShowMessage(string message) => Toast(message, HudTone.Info);

        public void Toast(string message, HudTone tone) => toasts?.Show(message, tone);

        // --- Dev tools -------------------------------------------------------------------------------

        void ToggleOverlay()
        {
            if (overlay == null) overlay = FindFirstObjectByType<LastOrdersDebugOverlay>();
            if (overlay == null) return;

            overlay.ShowPanel = !overlay.ShowPanel;
            DrawOverlayLabel();
        }

        void DrawOverlayLabel()
        {
            if (dev.overlayLabel == null) return;
            dev.overlayLabel.text = overlay != null && overlay.ShowPanel ? "Debug panel: ON" : "Debug panel: OFF";
        }

        public void Configure(GameLifetimeScope lifetimeScope, HudSkin hudSkin)
        {
            scope = lifetimeScope;
            skin = hudSkin;
        }
    }
}
