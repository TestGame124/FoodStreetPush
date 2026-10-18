using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.Defense;
using LastOrders.Economy;
using LastOrders.Interaction;
using TMPro;
using UnityEngine;

namespace LastOrders.Discovery
{
    /// <summary>
    /// The old shooting range, cleaned up: a bench at the firing line where one of the compound's
    /// own defense machines is set up for practice, and cardboard zombies flipping up on runners
    /// behind a berm downrange. A round is half a minute of tapping them - each tap tells the
    /// machine what to shoot, and it fires as soon as it is ready - so a slow lobbing cabbage
    /// shooter and a quick scrap turret play differently, the way they do on the wall.
    ///
    /// The score, the medal and the rest between rounds are the range service's. The machine is a
    /// practice copy (<see cref="DefensePost.ConfigureForPractice"/>): it never joins the night's
    /// defenses and costs nothing to fire.
    /// </summary>
    [RequireComponent(typeof(DiscoverySite))]
    public sealed class ShootingRange : MonoBehaviour, IDiscoveryFeature
    {
        [Tooltip("Where the practice machine stands on the bench, facing downrange.")]
        [SerializeField] Transform mount;

        [SerializeField] RangeTarget[] targets;

        [Tooltip("The machines the range can be set up with, in the order they are offered: the ones the " +
                 "player has built on the walls come first.")]
        [SerializeField] DefenseDefinition[] machines;

        [Tooltip("The middle of the range, which the camera frames for a round.")]
        [SerializeField] Transform view;

        [Header("Scoreboard")]
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text timeText;

        [SerializeField] Sprite promptIcon;

        [SerializeField] Color hitColour = new Color(1f, 0.86f, 0.3f);

        /// <summary>The most machines offered at once: the popup has a row of three buttons.</summary>
        const int Offered = 3;

        readonly List<IDefenseTarget> standing = new List<IDefenseTarget>();
        readonly float[] downUntil = new float[16];

        DiscoverySite site;
        ShootingRangeService service;
        GameObject machine;
        DefenseDefinition machineKind;
        DefenseUnit unit;
        RangeTarget called;
        bool wired;
        float boardRefresh;
        bool panned;

        public bool WantsPrompt => service != null && !service.IsRunning && service.CanStart(out _);

        public Sprite PromptIcon => promptIcon;

        void Awake() => site = GetComponent<DiscoverySite>();

        public void OnFound(bool announce)
        {
            if (wired || site.Composition == null) return;

            service = site.Composition.Range;
            service.OnFinished += HandleFinished;
            wired = true;

            if (targets != null)
                for (int i = 0; i < targets.Length; i++)
                    if (targets[i] != null) targets[i].Attach(this, (i * 0.37f) % 1f);

            // The bench is not left bare: the first machine on offer stands on it, idle, until a round.
            List<DefenseDefinition> offer = Offer();
            if (offer.Count > 0) SetUp(offer[0]);

            DrawBoard();
        }

        void OnDestroy()
        {
            if (wired) service.OnFinished -= HandleFinished;
            if (panned) CameraPanZoomRig.InputBlocked = false;
        }

        void Update()
        {
            if (!wired) return;

            if (service.IsRunning)
            {
                RunTargets();

                if (unit != null)
                {
                    standing.Clear();
                    foreach (RangeTarget target in targets)
                        if (target != null) standing.Add(target);

                    if (called != null && !called.IsAlive) called = null;
                    unit.TickCalled(Time.deltaTime, called, standing);
                }
            }

            boardRefresh -= Time.deltaTime;
            if (boardRefresh <= 0f)
            {
                boardRefresh = 0.1f;
                DrawBoard();
            }
        }

        // --- Rounds ------------------------------------------------------------------------------

        public string OnSiteTapped()
        {
            if (service == null) return "The range is not connected to the game.";
            if (service.IsRunning) return null;

            List<DefenseDefinition> offer = Offer();
            if (offer.Count == 0) return "There is no machine to practise with.";

            bool opened = BuildChoicePopup.Open(site.Composition, this, () => Describe(offer), choice =>
            {
                if (choice < 0 || choice >= offer.Count) return;
                BuildChoicePopup.Close(site.Composition, this);
                TapFeedback.Report(StartRound(offer[choice]));
            });

            return opened ? null : StartRound(offer[0]);
        }

        BuildChoiceOffer Describe(List<DefenseDefinition> offer)
        {
            ShootingRangeTuning tuning = service.Tuning;
            var text = new BuildChoiceOffer
            {
                Title = "Shooting range",
                Description = $"Tap the cardboard zombies to fire at them. Knock down as many as you can in {Mathf.RoundToInt(service.RoundSeconds)} seconds.",
                Status = service.BestScore > 0 ? $"Best so far: {service.BestScore}" : "Pick a machine to practise with",
            };

            for (int i = 0; i < ShootingRangeService.MedalNames.Length && tuning.medals != null && i < tuning.medals.Length; i++)
            {
                text.Details.Add($"{ShootingRangeService.MedalNames[i]}: {tuning.medals[i]} down - {service.CoinsFor(i)} coins, " +
                                 $"{service.ExperienceFor(i)} XP");
            }

            if (!service.CanStart(out string reason)) text.Blocked = reason;

            foreach (DefenseDefinition kind in offer)
            {
                WeaponDefinition weapon = kind.Level(0)?.weapon;
                string pace = weapon != null ? $"\n{weapon.secondsBetweenShots:0.0}s a shot" : "";
                text.Options.Add(new BuildChoice($"{kind.displayName.ToUpperInvariant()}{pace}", true, null));
            }

            return text;
        }

        string StartRound(DefenseDefinition kind)
        {
            if (!service.CanStart(out string reason)) return reason;

            SetUp(kind);
            if (!service.TryStart(out string message)) return message;

            called = null;
            float now = Time.time;
            for (int i = 0; i < targets.Length && i < downUntil.Length; i++) downUntil[i] = now + 0.4f + i * 0.35f;

            // The camera comes round to the range and stays put while the taps are on the targets.
            CameraPanZoomRig rig = FindFirstObjectByType<CameraPanZoomRig>();
            if (rig != null && view != null)
            {
                rig.PanTo(view.position);
                CameraPanZoomRig.InputBlocked = true;
                panned = true;
            }

            return $"Go! {kind.displayName} on the bench - tap the targets.";
        }

        void HandleFinished(int score, int medal)
        {
            foreach (RangeTarget target in targets)
                if (target != null) target.Drop();

            called = null;

            if (panned)
            {
                CameraPanZoomRig.InputBlocked = false;
                panned = false;
            }

            Vector3 at = (view != null ? view.position : transform.position) + Vector3.up * 2.5f;
            int coins = service.CoinsFor(medal);
            if (coins > 0) RewardSource.At(at);

            string result = medal >= 0
                ? $"{ShootingRangeService.MedalNames[medal]} medal! {score} down - {coins} coins."
                : $"{score} down. {service.ToNextMedal(score)} more for a medal.";

            site.Composition.WorldUi.ShowReward(at, medal >= 0 ? ShootingRangeService.MedalNames[medal].ToUpperInvariant() : $"{score} DOWN");
            TapFeedback.Report(result);
        }

        /// <summary>A tap on a cardboard zombie: the machine is told to shoot it.</summary>
        public string Called(RangeTarget target)
        {
            if (service == null || !service.IsRunning) return OnSiteTapped();
            if (target != null && target.IsAlive) called = target;
            return null;
        }

        /// <summary>A shot knocked a target down.</summary>
        public void KnockedDown(RangeTarget target)
        {
            service.RecordKnockDown();
            site.Composition.WorldUi.ShowFloatingText(target.Position + Vector3.up * 1.8f, "+1", hitColour);

            int i = System.Array.IndexOf(targets, target);
            if (i >= 0 && i < downUntil.Length) downUntil[i] = Time.time + 1.2f;
        }

        /// <summary>Each target, once it has been down long enough, flips up again for a while.</summary>
        void RunTargets()
        {
            ShootingRangeTuning tuning = service.Tuning;
            float now = Time.time;

            for (int i = 0; i < targets.Length && i < downUntil.Length; i++)
            {
                RangeTarget target = targets[i];
                if (target == null || !target.IsDown || now < downUntil[i]) continue;

                float up = tuning.targetUpSeconds * Random.Range(0.8f, 1.15f);
                target.PopUp(up, Random.Range(tuning.targetSpeed.x, tuning.targetSpeed.y), Random.value < 0.5f);
                downUntil[i] = now + up + Random.Range(tuning.targetDownSeconds.x, tuning.targetDownSeconds.y);
            }
        }

        // --- The machine -------------------------------------------------------------------------

        /// <summary>
        /// What the range offers: every kind of machine the player has standing on the walls, then the
        /// rest of the range's own list to make up the row. Machines that need a guard up in them are
        /// left out - a practice copy has nobody to man it.
        /// </summary>
        List<DefenseDefinition> Offer()
        {
            var offer = new List<DefenseDefinition>();

            IReadOnlyList<DefenseUnit> built = site.Composition.Defense.All;
            for (int i = 0; i < built.Count && offer.Count < Offered; i++)
            {
                DefenseDefinition kind = built[i]?.Definition;
                if (built[i] == null || !built[i].IsBuilt || !Usable(kind) || offer.Contains(kind)) continue;
                offer.Add(kind);
            }

            if (machines != null)
                foreach (DefenseDefinition kind in machines)
                    if (offer.Count < Offered && Usable(kind) && !offer.Contains(kind)) offer.Add(kind);

            return offer;
        }

        static bool Usable(DefenseDefinition kind) => kind != null && kind.prefab != null && !kind.requiresCrew && kind.Level(0)?.weapon != null;

        void SetUp(DefenseDefinition kind)
        {
            if (kind == machineKind && machine != null) return;

            if (machine != null) Destroy(machine);

            Transform at = mount != null ? mount : transform;
            machine = Instantiate(kind.prefab, at.position, at.rotation, at);
            machine.name = $"Practice_{kind.id}";
            machineKind = kind;

            unit = DefenseUnit.ForPractice($"range:{kind.id}", kind, at.position);

            DefensePost post = machine.GetComponentInChildren<DefensePost>(true);
            if (post != null) post.ConfigureForPractice(null, unit, site);
        }

        void DrawBoard()
        {
            if (service == null) return;

            if (scoreText != null)
                scoreText.text = service.IsRunning ? $"{service.Score:00}" : service.BestScore > 0 ? $"{service.BestScore:00}" : "--";

            if (timeText == null) return;

            if (service.IsRunning)
            {
                int left = Mathf.CeilToInt(service.SecondsLeft);
                timeText.text = $"0:{left:00}";
            }
            else
            {
                timeText.text = service.RestLeft > 0f ? "WAIT" : "READY";
            }
        }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(Transform bench, RangeTarget[] cutOuts, DefenseDefinition[] kinds, Transform middle, TMP_Text score,
                              TMP_Text time, Sprite icon)
        {
            mount = bench;
            targets = cutOuts;
            machines = kinds;
            view = middle;
            scoreText = score;
            timeText = time;
            promptIcon = icon;
        }
    }
}
