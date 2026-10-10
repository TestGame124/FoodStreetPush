using System.Collections.Generic;
using LastOrders.Consequence;
using LastOrders.Core;
using LastOrders.DayCycle;
using LastOrders.Effects;
using LastOrders.Interaction;
using LastOrders.Jobs;
using LastOrders.Night;
using LastOrders.UI.World;
using UnityEngine;

namespace LastOrders.Restaurant
{
    /// <summary>
    /// The attack, out in the world: the restaurant's health over its door while an attack is on,
    /// the crew coming out in the morning to patch it back to full, and a wrench over every wreck
    /// in the yard that puts them all back together with one repair kit.
    ///
    /// The crew's patch-up is a real job on the queue, for the hands who do repairs - they walk to
    /// the front wall and swing at it, and the health climbs as they work. With nobody to send,
    /// the patch-up runs on its own over the same few seconds, so the restaurant is always whole
    /// before the next day's service.
    /// </summary>
    [DefaultExecutionOrder(1180)]
    public sealed class EncounterWorldView : MonoBehaviour
    {
        const string JobPrefix = "crew-repair:";

        [SerializeField] GameLifetimeScope scope;

        [Header("Effects")]
        [Tooltip("Played where something in the yard is wrecked, and as it smoulders afterwards.")]
        [SerializeField] PooledEffect wreckEffect;

        [Tooltip("Played on every blow of the crew's patch-up, and over each wreck as it is repaired.")]
        [SerializeField] PooledEffect repairEffect;

        [Header("Restaurant health")]
        [Tooltip("Metres above the front wall that the restaurant's health bar floats.")]
        [SerializeField] float healthBarHeight = 3.6f;

        [Header("Wrecks")]
        [Tooltip("Seconds between the puffs of dust that rise from a wreck.")]
        [Min(0.5f)] [SerializeField] float smoulderSeconds = 3f;

        [Tooltip("Who patches the restaurant up in the morning. Whoever of them is in the scene goes.")]
        [SerializeField] WorkerRole[] crewRoles = { WorkerRole.Helper, WorkerRole.MainCharacter };

        GameComposition composition;
        RestaurantService restaurant;

        Transform healthAnchor;
        HealthBarView healthBar;

        readonly Dictionary<OutdoorWreck, ActionPromptView> wrenches = new Dictionary<OutdoorWreck, ActionPromptView>();
        readonly List<OutdoorWreck> scratch = new List<OutdoorWreck>();

        // The crew's patch-up.
        readonly List<Job> crewJobs = new List<Job>();
        readonly List<Transform> crewSpots = new List<Transform>();
        bool patching;
        float patchFrom;
        float patchTimer;
        float patchTotal;

        float refreshIn;
        float smoulderIn;

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                Debug.LogError("[EncounterWorldView] No GameLifetimeScope, so the attack cannot be shown in the world.", this);
                enabled = false;
                return;
            }

            restaurant = composition.Restaurant;

            composition.Machine.OnPhaseChanged += HandlePhaseChanged;
            composition.Outdoor.OnWrecked += HandleWrecked;
            composition.Outdoor.OnRepaired += HandleRepaired;
            composition.Encounters.OnResolved += HandleResolved;
            restaurant.OnHealthChanged += HandleHealthChanged;
        }

        void OnDestroy()
        {
            if (composition == null) return;

            composition.Machine.OnPhaseChanged -= HandlePhaseChanged;
            composition.Outdoor.OnWrecked -= HandleWrecked;
            composition.Outdoor.OnRepaired -= HandleRepaired;
            composition.Encounters.OnResolved -= HandleResolved;
            restaurant.OnHealthChanged -= HandleHealthChanged;
        }

        void OnDisable()
        {
            if (composition == null || !gameObject.scene.isLoaded) return;

            HideHealthBar();
            foreach (KeyValuePair<OutdoorWreck, ActionPromptView> entry in wrenches) ReleaseIfOurs(entry.Value, entry.Key.transform);
            wrenches.Clear();
        }

        void Update()
        {
            if (composition == null) return;

            if (patching) TickPatch(Time.deltaTime);

            Smoulder(Time.deltaTime);

            refreshIn -= Time.unscaledDeltaTime;
            if (refreshIn > 0f) return;

            refreshIn = 0.4f;
            RefreshHealthBar();
            RefreshWrenches();
        }

        // --- The restaurant's health ------------------------------------------------------------

        bool HealthBarWanted
        {
            get
            {
                if (!restaurant.HasFootprint) return false;

                DayPhase phase = composition.Machine.CurrentPhase;
                return phase == DayPhase.Dusk || phase == DayPhase.Night || patching ||
                       (phase == DayPhase.Resolution && restaurant.IsDamaged);
            }
        }

        void RefreshHealthBar()
        {
            if (!HealthBarWanted)
            {
                HideHealthBar();
                return;
            }

            PlaceHealthAnchor();

            if (healthBar == null || !healthBar.IsLive || healthBar.Anchor != healthAnchor)
            {
                healthBar = composition.WorldUi.ShowHealthBar(healthAnchor);
            }

            DrawHealth();
        }

        void HandleHealthChanged(float health, float max)
        {
            if (healthBar != null && healthBar.IsLive) DrawHealth();
        }

        void DrawHealth()
        {
            if (healthBar == null) return;

            float fraction = restaurant.HealthFraction;
            healthBar.Set(fraction, restaurant.Health, restaurant.MaxHealth, fraction < 0.3f);
        }

        void HideHealthBar()
        {
            if (healthBar != null && healthBar.IsLive && healthBar.Anchor == healthAnchor) healthBar.Release();
            healthBar = null;
        }

        /// <summary>Over the middle of the front wall, where the door is and the zombies end up.</summary>
        void PlaceHealthAnchor()
        {
            if (healthAnchor == null)
            {
                healthAnchor = new GameObject("RestaurantHealthAnchor").transform;
                healthAnchor.SetParent(transform, false);
            }

            healthAnchor.position = FrontCentre() + Vector3.up * healthBarHeight;
        }

        Vector3 FrontCentre()
        {
            Rect area = restaurant.Footprint;
            Vector2 front = restaurant.Front;
            Vector2 centre = area.center + new Vector2(front.x * area.width, front.y * area.height) * 0.5f;
            return new Vector3(centre.x, 0f, centre.y);
        }

        /// <summary>Along the front wall, <paramref name="across"/> metres from its middle, and
        /// <paramref name="outward"/> metres out from it.</summary>
        Vector3 AlongFront(float across, float outward)
        {
            Vector2 front = restaurant.Front;
            var side = new Vector3(-front.y, 0f, front.x);
            var outwards = new Vector3(front.x, 0f, front.y);
            return FrontCentre() + side * across + outwards * outward;
        }

        // --- The morning patch-up ----------------------------------------------------------------

        void HandleResolved(EncounterResult result)
        {
            if (restaurant.IsDamaged) BeginPatch();
        }

        void BeginPatch()
        {
            CancelCrewJobs();

            patching = true;
            patchFrom = restaurant.Health;
            patchTimer = 0f;
            patchTotal = Mathf.Max(0.5f, composition.Encounters.Tuning.crewRepairSeconds);

            if (!restaurant.HasFootprint) return;

            // Spread along the front wall, either side of the door.
            int index = 0;
            foreach (WorkerAgent worker in FindObjectsByType<WorkerAgent>(FindObjectsSortMode.None))
            {
                if (!worker.isActiveAndEnabled || System.Array.IndexOf(crewRoles, worker.Role) < 0) continue;

                float across = (index % 2 == 0 ? -1f : 1f) * (2.4f + 1.6f * (index / 2));
                Transform spot = CrewSpot(index, AlongFront(across, 0f));

                Job job = composition.Jobs.Queue($"{JobPrefix}{index}", spot, worker.Role, AlongFront(across, 0.9f),
                                                 patchTotal, "Patch up the restaurant");
                crewJobs.Add(job);
                index++;
            }

            Debug.Log($"[Last Orders] The crew comes out to patch up the restaurant ({crewJobs.Count} " +
                      $"worker{(crewJobs.Count == 1 ? "" : "s")}, {restaurant.HealthFraction:P0} to full).");
        }

        Transform CrewSpot(int index, Vector3 at)
        {
            while (crewSpots.Count <= index)
            {
                var spot = new GameObject($"CrewRepairSpot{crewSpots.Count}").transform;
                spot.SetParent(transform, false);
                spot.gameObject.AddComponent<CrewRepairStrike>().Bind(this);
                crewSpots.Add(spot);
            }

            Transform chosen = crewSpots[index];
            chosen.position = at + Vector3.up * 1.2f;
            return chosen;
        }

        /// <summary>
        /// The health climbs with the crew's work: as far along as the furthest of them, or - with
        /// nobody working yet, or nobody to send - on its own clock, which also makes sure it never
        /// takes much longer than it should. Full at the end, always.
        /// </summary>
        void TickPatch(float deltaTime)
        {
            patchTimer += deltaTime;

            float worked = 0f;
            for (int i = 0; i < crewJobs.Count; i++)
                if (crewJobs[i] != null) worked = Mathf.Max(worked, crewJobs[i].State == JobState.Completed ? 1f : crewJobs[i].Progress);

            // Without a crew it runs on the clock alone; with one, the clock is only the backstop
            // for a crew that never makes it out - twice as long as the work.
            float clock = crewJobs.Count == 0
                ? patchTimer / patchTotal
                : Mathf.Clamp01((patchTimer - patchTotal * 2f) / patchTotal);

            float progress = Mathf.Clamp01(Mathf.Max(worked, clock));
            float wanted = Mathf.Lerp(patchFrom, restaurant.MaxHealth, progress);

            if (wanted > restaurant.Health) restaurant.Restore(wanted - restaurant.Health);

            if (crewJobs.Count == 0 && Random.value < deltaTime * 2.5f) Puff(FrontCentre() + Vector3.up * 1.2f, repairEffect);

            if (progress < 1f) return;

            restaurant.RestoreFully();
            patching = false;
            CancelCrewJobs();

            composition.WorldUi.ShowReward(FrontCentre() + Vector3.up * 2.4f, "Restaurant repaired");
        }

        void CancelCrewJobs()
        {
            for (int i = 0; i < crewJobs.Count; i++)
                if (crewJobs[i] != null && !crewJobs[i].IsFinished) crewJobs[i].Cancel();

            crewJobs.Clear();
        }

        /// <summary>A blow of the crew's tools landing on the wall.</summary>
        internal void OnCrewStrike(Transform spot) => Puff(spot.position, repairEffect);

        void HandlePhaseChanged(DayPhase previous, DayPhase current)
        {
            // The working day never opens on a damaged restaurant: whatever the crew had left to
            // do is done by the time the sign turns.
            if (current == DayPhase.Day && patching)
            {
                restaurant.RestoreFully();
                patching = false;
                CancelCrewJobs();
            }

            refreshIn = 0f;
        }

        // --- Wrecks -----------------------------------------------------------------------------

        void HandleWrecked(OutdoorWreck wreck)
        {
            Vector3 at = wreck.Centre + Vector3.up * 0.4f;
            Puff(at, wreckEffect);
            composition.WorldUi.ShowWarning(at + Vector3.up * 1.2f, $"{wreck.DisplayName} wrecked!");
        }

        void HandleRepaired(int count)
        {
            IReadOnlyList<OutdoorWreck> all = composition.Outdoor.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && wrenches.ContainsKey(all[i])) Puff(all[i].Centre + Vector3.up * 0.5f, repairEffect);

            refreshIn = 0f;
        }

        /// <summary>A wrench over every wreck while it can be mended. Any of them mends them all.</summary>
        void RefreshWrenches()
        {
            bool canMend = composition.Outdoor.IsRepairTime(out _);

            IReadOnlyList<OutdoorWreck> all = composition.Outdoor.All;
            for (int i = 0; i < all.Count; i++)
            {
                OutdoorWreck wreck = all[i];
                if (wreck == null) continue;

                bool wanted = canMend && wreck.IsWrecked && wreck.gameObject.activeInHierarchy;
                wrenches.TryGetValue(wreck, out ActionPromptView shown);
                bool showing = shown != null && shown.IsLive && shown.Anchor == wreck.Anchor;

                if (wanted && !showing)
                {
                    ActionPromptView button = composition.WorldUi.ShowRepairPrompt(wreck.Anchor, RepairAll);
                    if (button != null) wrenches[wreck] = button;
                }
                else if (!wanted && shown != null)
                {
                    ReleaseIfOurs(shown, wreck.Anchor);
                    wrenches.Remove(wreck);
                }
            }

            // Wrecks that have left the world - sold, or destroyed - take their wrench with them.
            scratch.Clear();
            foreach (KeyValuePair<OutdoorWreck, ActionPromptView> entry in wrenches)
                if (entry.Key == null) scratch.Add(entry.Key);

            for (int i = 0; i < scratch.Count; i++)
            {
                if (wrenches.TryGetValue(scratch[i], out ActionPromptView orphan) && orphan != null && orphan.IsLive) orphan.Release();
                wrenches.Remove(scratch[i]);
            }
        }

        void RepairAll()
        {
            composition.Outdoor.TryRepairAll(out string message);
            TapFeedback.Report(message);
        }

        void Smoulder(float deltaTime)
        {
            if (wreckEffect == null) return;

            smoulderIn -= deltaTime;
            if (smoulderIn > 0f) return;

            smoulderIn = smoulderSeconds;

            IReadOnlyList<OutdoorWreck> all = composition.Outdoor.All;
            for (int i = 0; i < all.Count; i++)
            {
                OutdoorWreck wreck = all[i];
                if (wreck != null && wreck.IsWrecked && wreck.gameObject.activeInHierarchy && Random.value < 0.6f)
                    Puff(wreck.Centre + Vector3.up * 0.3f, wreckEffect);
            }
        }

        void Puff(Vector3 at, PooledEffect effect)
        {
            if (effect != null) composition.Effects.Play(effect, at);
        }

        static void ReleaseIfOurs(WorldUiElement element, Transform anchor)
        {
            if (element != null && element.IsLive && element.Anchor == anchor) element.Release();
        }

        public void Configure(GameLifetimeScope lifetimeScope, PooledEffect wrecked, PooledEffect repaired)
        {
            scope = lifetimeScope;
            wreckEffect = wrecked;
            repairEffect = repaired;
        }
    }
}
