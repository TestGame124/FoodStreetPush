using System;
using System.Collections.Generic;
using DG.Tweening;
using LastOrders.Core;
using LastOrders.DayCycle;
using LastOrders.Interaction;
using LastOrders.UI.Popups;
using LastOrders.UI.World;
using UnityEngine;

namespace LastOrders.Livestock
{
    /// <summary>
    /// A farm building that gives something while its animals are fed and about: the chicken coop
    /// lays eggs into its nest boxes, the cow shelter fills churns of milk. Neither gives anything
    /// on an empty trough - the hens eat corn, the cow wheat - so the first thing either needs is
    /// feeding, from the storehouse, with a tap. One item of feed is good for
    /// <see cref="unitsPerFeed"/> eggs or churns. Then one every <see cref="secondsPerUnit"/> of the
    /// working day, kept until collected, up to what the nest boxes or the churn stand hold - and
    /// nothing at night, when the animals are asleep, or while the morning screen has the clock stopped.
    ///
    /// What is waiting shows on the building - one egg or churn per unit, the harvest shimmer, and a
    /// bubble over it with the item and how many - as does the feed: corn by the feeder, hay in the
    /// rack. A hungry building's bubble shows what it wants instead.
    ///
    /// A tap collects whatever is waiting (thrown out to fly into the storehouse, as crops are).
    /// With nothing to collect, a hungry building - or one with room for more and feed to give -
    /// opens the feed popup, where the player picks how many eggs or churns to make; the feed that
    /// costs leaves the storehouse (never its last corn or wheat, kept back for sowing) and is seen
    /// flying from the popup's FEED button into the feeder, each item landing with a burst and the
    /// feed shown filling up. Otherwise a tap shows a card: the item, a bar filling toward the next
    /// one with the time left, how many are waiting and how much feed is left.
    ///
    /// Sold in the shop and pooled like everything else set down on the grid, so a copy that comes
    /// back out of the pool starts empty and hungry rather than with its last owner's eggs and corn.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LivestockPen : MonoBehaviour, IInteractableObject, ITappable
    {
        [SerializeField] GameLifetimeScope scope;

        [Tooltip("Shown on a tap and in its messages.")]
        [SerializeField] string displayName = "Chicken Coop";

        [Tooltip("What it gives: the Egg or Milk item.")]
        [SerializeField] ItemDataSO produce;

        [Tooltip("One of what it gives, in a sentence: \"egg\", \"churn of milk\".")]
        [SerializeField] string unitName = "egg";

        [Tooltip("More than one, in a sentence: \"eggs\", \"churns of milk\".")]
        [SerializeField] string unitsName = "eggs";

        [Tooltip("Who lives here, in a sentence: \"the hens\", \"the cow\".")]
        [SerializeField] string animalsName = "the hens";

        [Tooltip("Who is asleep at night, for the message a tap gets then.")]
        [SerializeField] string animalsAsleep = "The hens are roosting for the night.";

        [Tooltip("Seconds of the working day for each one.")]
        [SerializeField, Min(1f)] float secondsPerUnit = 30f;

        [Tooltip("The most it keeps before it stops: the nest boxes, the churn stand.")]
        [SerializeField, Min(1)] int capacity = 6;

        [Tooltip("Already waiting when it is set down or the level starts.")]
        [SerializeField, Min(0)] int startingStock;

        [Header("Feed")]
        [Tooltip("What the animals eat: Corn for the hens, the wheat crop (the Flour item) for the cow.")]
        [SerializeField] ItemDataSO feed;

        [Tooltip("The feed in a sentence: \"corn\", \"wheat\".")]
        [SerializeField] string feedName = "corn";

        [Tooltip("The most items of feed the trough holds.")]
        [SerializeField, Min(1)] int feedCapacity = 3;

        [Tooltip("Eggs or churns each item of feed is good for.")]
        [SerializeField, Min(1)] int unitsPerFeed = 2;

        [Tooltip("Feed already in the trough when it is set down or the level starts.")]
        [SerializeField, Min(0)] int startingFeed;

        [Header("Parts")]
        [Tooltip("Where what is collected is thrown out from: in front of the nest box, the churn stand.")]
        [SerializeField] Transform collectPoint;

        [Tooltip("What shows how much is waiting, one per unit in order: the eggs in the nest, the churns by the gate.")]
        [SerializeField] GameObject[] stock;

        [Tooltip("What shows the feed left, one per item in order: corn by the feeder, hay in the rack.")]
        [SerializeField] GameObject[] feedShown;

        [Tooltip("The harvest shimmer, on while anything is waiting.")]
        [SerializeField] GameObject readyEffect;

        [Tooltip("Played as the lot is collected: straw and sparkles out of the nest, a splash of milk.")]
        [SerializeField] ParticleSystem[] collectBurst;

        [Tooltip("Played as each one is laid or filled: a pop of sparkle at the nest or the churns.")]
        [SerializeField] ParticleSystem[] producedBurst;

        [Tooltip("Played as each item of feed lands: kernels scattered at the feeder, hay at the rack, and the feed taken in.")]
        [SerializeField] ParticleSystem[] feedBurst;

        [Tooltip("Where fed items drop in: the feeder, the front of the hay rack.")]
        [SerializeField] Transform feedPoint;

        [Tooltip("The icon thrown in for each item fed.")]
        [SerializeField] FeedDrop dropPrefab;

        [Tooltip("Seconds between one item of feed setting off and the next.")]
        [SerializeField, Min(0f)] float dropStagger = 0.16f;

        [Tooltip("The \"-2 Corn\" that rises from the feeder as the feed goes in.")]
        [SerializeField] Color usedColour = new Color(1f, 0.66f, 0.36f);

        [Header("Over the building")]
        [Tooltip("Where the card and the bubble float: above the roof.")]
        [SerializeField] Transform uiAnchor;

        [SerializeField] ProduceProgressView cardPrefab;
        [SerializeField] ProduceBubbleView bubblePrefab;

        [Tooltip("Seconds the card stays up after a tap.")]
        [SerializeField, Min(0.5f)] float cardSeconds = 4.5f;

        [SerializeField] Color readyRing = new Color(0.45f, 0.82f, 0.35f);
        [SerializeField] Color fullRing = new Color(0.98f, 0.62f, 0.2f);
        [SerializeField] Color hungryRing = new Color(0.9f, 0.32f, 0.26f);

        LivestockClock clock;
        FeedTrough trough;
        GameComposition composition;
        Vector3[] stockScale, feedScale;
        bool subscribed;

        ProduceProgressView card;
        ProduceBubbleView bubble;
        float cardUntil = -1f;

        readonly List<FeedDrop> drops = new List<FeedDrop>();
        int inFlight, fedUnannounced;
        bool warnedNoPopup, cardOnLanding;

        /// <summary>One more laid by, and how many are waiting now.</summary>
        public event Action<LivestockPen, int> Produced;

        /// <summary>Just collected: how many.</summary>
        public event Action<LivestockPen, int> Collected;

        /// <summary>Just fed - as the first of the feed lands: how many items went in the trough.</summary>
        public event Action<LivestockPen, int> Fed;

        /// <summary>The animals are going to bed (true) or getting up (false).</summary>
        public event Action<bool> RestChanged;

        public string DisplayName => displayName;
        public ItemDataSO Produce => produce;
        public ItemDataSO Feed => feed;
        public int Stored => Clock.Stored;
        public int Capacity => Clock.Capacity;
        public bool Full => Clock.Full;
        public float Progress => Clock.Progress;
        public float SecondsToNext => Clock.SecondsToNext;
        public int FeedLeft => Trough.ItemsLeft;

        /// <summary>Nothing in the trough: nothing more will come until they are fed.</summary>
        public bool Hungry => feed != null && Trough.Hungry;

        /// <summary>Dusk to the end of the night: the animals are in bed and nothing is laid.</summary>
        public bool Resting { get; private set; }

        /// <summary>The morning screen has the clock stopped: nothing moves, nothing is laid.</summary>
        public bool Paused => composition != null && composition.Time.IsPaused;

        /// <summary>Fed, awake, room left: an egg or a churn is on its way.</summary>
        public bool Producing => !Resting && !Paused && !Full && !Hungry;

        public Vector3 CollectPoint => collectPoint != null ? collectPoint.position : transform.position + Vector3.up * 0.4f;

        /// <summary>Where fed items drop in.</summary>
        public Vector3 FeedPoint => feedPoint != null ? feedPoint.position : transform.position + Vector3.up * 0.4f;

        /// <summary>Items of feed still on their way into the feeder.</summary>
        public int FeedInFlight => inFlight;

        Transform Anchor => uiAnchor != null ? uiAnchor : transform;

        LivestockClock Clock => clock ??= new LivestockClock(secondsPerUnit, capacity);
        FeedTrough Trough => trough ??= new FeedTrough(feedCapacity, unitsPerFeed);

        void Awake()
        {
            stockScale = Scales(stock);
            feedScale = Scales(feedShown);
        }

        static Vector3[] Scales(GameObject[] parts)
        {
            if (parts == null) return null;
            var scales = new Vector3[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null) scales[i] = parts[i].transform.localScale;
            return scales;
        }

        void OnEnable()
        {
            // Out of the pool or into the level: whatever it held before is gone.
            Clock.Reset(startingStock);
            Trough.Reset(startingFeed);
            cardUntil = -1f;
            inFlight = 0;
            fedUnannounced = 0;
            Show(stock, stockScale, Clock.Stored, false);
            ShowFeed(false);
            ShowShimmer();
        }

        void OnDisable()
        {
            foreach (FeedDrop drop in drops)
                if (drop != null) drop.Cancel();
            inFlight = 0;
            fedUnannounced = 0;
            cardOnLanding = false;

            CloseFeedPopup();
            ReleaseUi();
        }

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition != null && !subscribed)
            {
                composition.Machine.OnPhaseChanged += HandlePhaseChanged;
                subscribed = true;
                Apply(composition.Machine.CurrentPhase);
            }
        }

        void OnDestroy()
        {
            if (subscribed && composition != null) composition.Machine.OnPhaseChanged -= HandlePhaseChanged;
            subscribed = false;
            ReleaseUi();
            Kill(stock);
            Kill(feedShown);
        }

        static void Kill(GameObject[] parts)
        {
            if (parts == null) return;
            foreach (GameObject part in parts)
                if (part != null) part.transform.DOKill();
        }

        void HandlePhaseChanged(DayPhase previous, DayPhase current) => Apply(current);

        void Apply(DayPhase phase)
        {
            // Bed at dusk, up again with the morning: dawn wakes them even while its screen holds the clock.
            bool rest = phase == DayPhase.Dusk || phase == DayPhase.Night || phase == DayPhase.Resolution;
            if (rest == Resting) return;

            Resting = rest;
            RestChanged?.Invoke(rest);
        }

        void Update()
        {
            if (!Resting && !Paused && !Hungry)
            {
                int made = Clock.Tick(Time.deltaTime, feed != null ? Trough.Portions : int.MaxValue);
                if (made > 0)
                {
                    if (feed != null)
                        for (int i = 0; i < made; i++) Trough.Eat();

                    Show(stock, stockScale, Clock.Stored, true);
                    ShowFeed(false);
                    ShowShimmer();
                    Play(producedBurst);
                    Produced?.Invoke(this, Clock.Stored);
                }
            }

            ShowUi();
        }

        // --- Tapping ---------------------------------------------------------------------------------

        public bool OnTouch() => TapFeedback.Report(OnTapped());

        /// <summary>
        /// Collects what is waiting; with nothing waiting, opens the feed popup when there is feeding
        /// to decide, or shows the card. Returns what to tell the player.
        /// </summary>
        public string OnTapped()
        {
            if (Clock.Stored > 0)
            {
                cardUntil = Time.time + cardSeconds;
                return Collect();
            }

            if (WantsFeeding)
            {
                if (OpenFeedPopup())
                {
                    cardUntil = -1f;
                    return null;
                }

                // No popup in this scene's UI catalog: feed what fits rather than leave them hungry.
                if (!warnedNoPopup)
                {
                    warnedNoPopup = true;
                    Debug.LogWarning($"[Livestock] No feed popup in the UI catalog; the {displayName.ToLowerInvariant()} is fed all it holds instead. " +
                                     "Run Last Orders/Farming/Rebuild Farm Animals & Buildings.", this);
                }

                cardUntil = Time.time + cardSeconds;
                return Refill() ?? Status();
            }

            cardUntil = Time.time + cardSeconds;
            return Status();
        }

        /// <summary>Hungry, or room in the feeder and feed to put in it: the popup has something to offer.</summary>
        public bool WantsFeeding => feed != null && Trough.Room > 0 && (Trough.Hungry || SpareFeed(FeedInStore) > 0);

        string Status()
        {
            if (Resting) return $"{displayName}: {animalsAsleep}";
            if (Hungry) return $"{displayName}: {Capitalised(animalsName)} {Need} {feedName}.";
            int seconds = Mathf.CeilToInt(Clock.SecondsToNext);
            return $"{displayName}: the next {unitName} in {seconds / 60}:{seconds % 60:00}.";
        }

        // --- The feed popup ----------------------------------------------------------------------------

        bool OpenFeedPopup()
        {
            WorldUiCatalog catalog = composition != null && composition.WorldUi != null ? composition.WorldUi.Catalog : null;
            FeedPopupView prefab = catalog != null ? catalog.feedPopup : null;
            if (prefab == null || composition.Popups == null) return false;

            FeedPopupView popup = composition.Popups.Show(prefab);
            if (popup == null) return false;

            popup.Bind(this, Offer, HandleFeedChosen);
            return true;
        }

        void HandleFeedChosen(int items, Vector2 fromScreen)
        {
            bool done = GiveFeed(items, fromScreen, out string message);
            TapFeedback.Report(message);
            if (!done) return;

            // A refusal leaves the popup open with the reason on it. Done, the card shows the new
            // timer - once the feed is in, so it does not cover the feed going in.
            CloseFeedPopup();
            if (inFlight > 0) cardOnLanding = true;
            else cardUntil = Time.time + cardSeconds;
        }

        void CloseFeedPopup()
        {
            if (composition != null && composition.Popups != null &&
                composition.Popups.Current is FeedPopupView open && open.Owner == (object)this)
                composition.Popups.Hide();
        }

        /// <summary>Everything the feed popup shows, as it stands now.</summary>
        public FeedOffer Offer()
        {
            int have = FeedInStore;
            ProduceProgressView.State now = Now();
            string status = now switch
            {
                ProduceProgressView.State.Full => $"Full - collect the {unitsName}!",
                ProduceProgressView.State.Asleep => $"{Capitalised(animalsName)} {(Plural ? "are" : "is")} asleep until morning",
                ProduceProgressView.State.Hungry => $"Hungry - feed {animalsName} to start",
                ProduceProgressView.State.Waiting => "Waiting for the day to start",
                _ => $"Next {unitName} in",
            };

            string each = ProduceProgressView.FormatSeconds(secondsPerUnit);
            string description = $"Each {feedName} makes {unitsPerFeed} {(unitsPerFeed == 1 ? unitName : unitsName)}, one every {each} of daytime.";

            return new FeedOffer(displayName, description, produce != null ? produce.itemImage : null, feed != null ? feed.itemImage : null,
                                 unitName, unitsName, feedName, unitsPerFeed, secondsPerUnit, have, SpareFeed(have),
                                 feed != null && feed.canBePlanted, Trough.ItemsLeft, Trough.Capacity, Clock.Stored, Clock.Capacity,
                                 now, status, Clock.Progress, Clock.SecondsToNext);
        }

        // --- Feeding -----------------------------------------------------------------------------------

        int FeedInStore => feed != null && InventorySystem.instance != null ? InventorySystem.instance.ItemCount(feed.Type) : 0;

        /// <summary>Of <paramref name="have"/>, what can be fed: the last one of a crop is its seed, kept to grow more from.</summary>
        int SpareFeed(int have) => Mathf.Max(0, feed != null && feed.canBePlanted ? have - 1 : have);

        /// <summary>
        /// Feeds up to <paramref name="items"/> from the storehouse - as many as fit and can be spared -
        /// and throws them into the feeder from <paramref name="fromScreen"/> (a point on screen, the
        /// FEED button's), or from above when there is none. False, with why, when nothing went in.
        /// </summary>
        public bool GiveFeed(int items, Vector2? fromScreen, out string message)
        {
            message = null;
            if (feed == null) return false;

            InventorySystem inventory = InventorySystem.instance;
            int have = FeedInStore;
            int give = Mathf.Min(items, Trough.Room, SpareFeed(have));

            if (give <= 0)
            {
                message = Trough.Room <= 0 ? $"{displayName}: the feeder is full."
                    : have > 0 ? $"{displayName}: {Capitalised(animalsName)} {Need} {feedName} - grow more, the last one is kept for sowing."
                    : $"{displayName}: {Capitalised(animalsName)} {Need} {feedName} - grow some in the plant beds.";
                return false;
            }

            if (inventory == null || !inventory.TryConsume(feed.Type, give))
            {
                message = $"{displayName}: the {feedName} could not be taken from the storehouse.";
                return false;
            }

            Trough.Fill(give);
            Deliver(give, fromScreen);

            int makes = give * unitsPerFeed;
            message = $"Fed {animalsName} {give} {feedName}: {makes} {(makes == 1 ? unitName : unitsName)} to come.";
            return true;
        }

        /// <summary>
        /// Tops the trough up from the storehouse with all that fits and can be spared. Returns what
        /// to tell the player, or null when there was nothing to say (the trough was already full).
        /// </summary>
        public string Refill()
        {
            if (feed == null || Trough.Room <= 0) return null;
            if (GiveFeed(Trough.Room, null, out string message)) return message;
            return Trough.Hungry ? message : null;
        }

        /// <summary>
        /// Shows <paramref name="count"/> items of feed going in: their icons thrown into the feeder,
        /// each landing with a burst and one more shown in it. All at once where nothing can be seen.
        /// </summary>
        void Deliver(int count, Vector2? fromScreen)
        {
            Camera view = composition != null && composition.WorldUi != null ? composition.WorldUi.Camera : Camera.main;
            fedUnannounced += count;

            if (!Application.isPlaying || dropPrefab == null || view == null || feed == null || !isActiveAndEnabled)
            {
                for (int i = 0; i < count; i++) Land();
                return;
            }

            Vector3 target = FeedPoint;
            Vector3 start = fromScreen.HasValue ? OnScreenAt(view, fromScreen.Value, target) : target + Vector3.up * 3f;

            for (int i = 0; i < count; i++)
            {
                inFlight++;
                Drop().Fly(feed.itemImage, start, target, i * dropStagger, HandleLanded);
            }
        }

        /// <summary>The point under <paramref name="screen"/> as far from the camera as <paramref name="depthOf"/> is.</summary>
        static Vector3 OnScreenAt(Camera view, Vector2 screen, Vector3 depthOf)
        {
            Ray ray = view.ScreenPointToRay(screen);
            float depth = Vector3.Dot(depthOf - view.transform.position, view.transform.forward);
            float along = depth / Mathf.Max(0.05f, Vector3.Dot(ray.direction, view.transform.forward));
            return ray.GetPoint(Mathf.Max(0.5f, along));
        }

        FeedDrop Drop()
        {
            foreach (FeedDrop drop in drops)
                if (drop != null && !drop.Flying) return drop;

            FeedDrop made = Instantiate(dropPrefab, transform);
            made.name = $"{dropPrefab.name}_{drops.Count}";
            drops.Add(made);
            return made;
        }

        void HandleLanded(FeedDrop drop)
        {
            inFlight = Mathf.Max(0, inFlight - 1);
            Land();

            if (inFlight > 0 || !cardOnLanding) return;
            cardOnLanding = false;
            cardUntil = Time.time + cardSeconds;
        }

        /// <summary>One item of feed in: shown in the feeder, a burst where it went in, and the first says how much was used.</summary>
        void Land()
        {
            // Out of play (the tests) nothing animates: no tweens, no particles.
            bool playing = Application.isPlaying;
            ShowFeed(playing);
            if (playing) Burst(feedBurst);

            if (fedUnannounced <= 0) return;

            int count = fedUnannounced;
            fedUnannounced = 0;

            if (composition != null && composition.WorldUi != null)
                composition.WorldUi.ShowFloatingText(FeedPoint + Vector3.up * 0.9f, $"-{count} {Capitalised(feedName)}", usedColour);

            Fed?.Invoke(this, count);
        }

        /// <summary>The feed shown in the feeder: what is in the trough, less what is still on its way.</summary>
        void ShowFeed(bool pop) => Show(feedShown, feedScale, Mathf.Max(0, Trough.ItemsLeft - inFlight), pop);

        /// <summary>Collects everything waiting into the storehouse. Returns what to tell the player.</summary>
        public string Collect()
        {
            InventorySystem inventory = InventorySystem.instance;
            ItemController items = ItemController.instance;

            if (produce == null || inventory == null || items == null || items.itemReceiver == null)
                return $"{displayName}: there is nowhere to put the {unitsName}.";

            if (!inventory.CanAdd(produce.Type))
                return $"{displayName}: the storehouse is full - use something up first.";

            int count = Clock.TakeAll();
            if (count <= 0) return null;

            // A fresh item: the first pickup to land becomes the inventory's entry and the rest add to it.
            produce.GenerateItem(items.itemReceiver, new Item(produce, 1), CollectPoint, inventory, count);

            Show(stock, stockScale, 0, false);
            ShowShimmer();
            Play(collectBurst);

            if (composition != null && composition.WorldUi != null)
                composition.WorldUi.ShowReward(CollectPoint + Vector3.up * 0.8f, $"+{count} {produce.Name}");

            Collected?.Invoke(this, count);
            return $"Collected {count} {(count == 1 ? unitName : unitsName)} from the {displayName.ToLowerInvariant()}.";
        }

        /// <summary>"the hens" or "the cow".</summary>
        bool Plural => animalsName.EndsWith("s");

        /// <summary>"need" for the hens, "needs" for the cow.</summary>
        string Need => Plural ? "need" : "needs";

        static string Capitalised(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // --- Showing it --------------------------------------------------------------------------------

        static void Play(ParticleSystem[] systems)
        {
            if (systems == null) return;
            foreach (ParticleSystem system in systems)
            {
                if (system == null) continue;
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.Play(true);
            }
        }

        /// <summary>
        /// Another shot of each of <paramref name="systems"/>, without cutting short one still going:
        /// feed landing an item at a time throws a fresh handful in with each.
        /// </summary>
        static void Burst(ParticleSystem[] systems)
        {
            if (systems == null) return;
            foreach (ParticleSystem system in systems)
            {
                if (system == null) continue;
                if (!system.isPlaying)
                {
                    system.Play(false);
                    continue;
                }

                ParticleSystem.EmissionModule emission = system.emission;
                int count = emission.burstCount > 0 ? Mathf.RoundToInt(emission.GetBurst(0).count.constantMax) : 4;
                system.Emit(Mathf.Max(1, count));
            }
        }

        void ShowShimmer()
        {
            bool on = Clock.Stored > 0;
            if (readyEffect != null && readyEffect.activeSelf != on) readyEffect.SetActive(on);
        }

        /// <summary>The first <paramref name="count"/> of <paramref name="parts"/> on, the rest off; a new one pops in.</summary>
        static void Show(GameObject[] parts, Vector3[] scales, int count, bool pop)
        {
            if (parts == null) return;

            for (int i = 0; i < parts.Length; i++)
            {
                GameObject part = parts[i];
                if (part == null) continue;

                bool on = i < count;
                if (part.activeSelf == on) continue;

                part.SetActive(on);
                if (!on || !pop || scales == null) continue;

                Transform t = part.transform;
                t.DOKill();
                t.localScale = scales[i] * 0.2f;
                t.DOScale(scales[i], 0.4f).SetEase(Ease.OutBack);
            }
        }

        void ShowUi()
        {
            WorldUiService ui = composition != null ? composition.WorldUi : null;
            if (ui == null) return;

            bool showCard = cardPrefab != null && Time.time < cardUntil;
            if (showCard)
            {
                if (card == null || !card.IsLive) card = ui.Spawn(cardPrefab, Anchor, Vector3.zero);
                if (card != null) ShowCard(card);
            }
            else Release(ref card);

            // The bubble: what is waiting, or what they want - unless the card is saying it already.
            Sprite icon = null;
            string label = null;
            Color ring = readyRing;
            float pulse = 1f;

            if (!showCard && produce != null && Clock.Stored > 0)
            {
                icon = produce.itemImage;
                label = Clock.Stored.ToString();
                ring = Clock.Full ? fullRing : readyRing;
                pulse = Clock.Full ? 2.2f : 1f;
            }
            else if (!showCard && Hungry && feed != null)
            {
                icon = feed.itemImage;
                label = "!";
                ring = hungryRing;
                pulse = 1.6f;
            }

            if (icon != null && bubblePrefab != null)
            {
                if (bubble == null || !bubble.IsLive) bubble = ui.Spawn(bubblePrefab, Anchor, Vector3.zero);
                if (bubble != null) bubble.Show(icon, label, ring, pulse);
            }
            else Release(ref bubble);
        }

        /// <summary>What it is doing now, as the card and the popup show it.</summary>
        ProduceProgressView.State Now()
        {
            if (Clock.Full) return ProduceProgressView.State.Full;
            if (Resting) return ProduceProgressView.State.Asleep;
            if (Hungry) return ProduceProgressView.State.Hungry;
            if (Paused) return ProduceProgressView.State.Waiting;
            return ProduceProgressView.State.Working;
        }

        void ShowCard(ProduceProgressView view)
        {
            ProduceProgressView.State state = Now();
            string caption = state switch
            {
                ProduceProgressView.State.Full => "Full - tap!",
                ProduceProgressView.State.Asleep => "Asleep",
                ProduceProgressView.State.Hungry => $"Needs {feedName}",
                ProduceProgressView.State.Waiting => "Waiting",
                // The item's own short name - "Next egg", "Next milk" - so it fits beside the feed.
                _ => produce != null ? $"Next {produce.Name.ToLowerInvariant()}" : $"Next {unitName}",
            };

            view.Show(state, produce != null ? produce.itemImage : null, feed != null ? feed.itemImage : null, caption, Clock.Progress,
                      Clock.SecondsToNext, Clock.Stored, Clock.Capacity, Mathf.Max(0, Trough.ItemsLeft - inFlight));
        }

        static void Release<T>(ref T element) where T : WorldUiElement
        {
            if (element != null && element.IsLive) element.Release();
            element = null;
        }

        void ReleaseUi()
        {
            Release(ref card);
            Release(ref bubble);
        }

        /// <summary>Editor wiring, for the builder that makes the building.</summary>
        public void Configure(string title, ItemDataSO item, string one, string many, string animals, string asleep, float seconds, int most,
                              Transform throwFrom, GameObject[] shownStock, GameObject shimmer, ParticleSystem[] burst)
        {
            displayName = title;
            produce = item;
            unitName = one;
            unitsName = many;
            animalsName = animals;
            animalsAsleep = asleep;
            secondsPerUnit = seconds;
            capacity = most;
            collectPoint = throwFrom;
            stock = shownStock;
            readyEffect = shimmer;
            collectBurst = burst;
            clock = null;
        }

        /// <summary>Editor wiring: what the animals eat and how far it goes, and where it is seen going in.</summary>
        public void ConfigureFeed(ItemDataSO food, string foodName, int most, int unitsEach, GameObject[] shown, ParticleSystem[] burst,
                                  Transform dropInto, FeedDrop drop)
        {
            feed = food;
            feedName = foodName;
            feedCapacity = most;
            unitsPerFeed = unitsEach;
            feedShown = shown;
            feedBurst = burst;
            feedPoint = dropInto;
            dropPrefab = drop;
            trough = null;
        }

        /// <summary>Editor wiring: the card and bubble over it, and the pop when one is made.</summary>
        public void ConfigureUi(Transform anchor, ProduceProgressView cardView, ProduceBubbleView bubbleView, ParticleSystem[] made)
        {
            uiAnchor = anchor;
            cardPrefab = cardView;
            bubblePrefab = bubbleView;
            producedBurst = made;
        }
    }
}
