using DG.Tweening;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Defense;
using LastOrders.Interaction;
using LastOrders.UI.World;
using UnityEngine;

namespace LastOrders.Discovery
{
    /// <summary>What a discovery site does once it is found: the part of it the site hands taps and the find to.</summary>
    public interface IDiscoveryFeature
    {
        /// <summary>The site was found: true just now, by claiming its plot; false as the scene comes up.</summary>
        void OnFound(bool announce);

        /// <summary>A tap on the found site, or on its button. The message to show, or null when a popup opened.</summary>
        string OnSiteTapped();

        /// <summary>Whether the button over the site should stand right now.</summary>
        bool WantsPrompt { get; }

        /// <summary>The picture on the button.</summary>
        Sprite PromptIcon { get; }
    }

    /// <summary>
    /// A discovery site in the scene: an abandoned building on a plot still to be claimed, standing
    /// derelict - boarded up, overgrown, a question mark over it - until the plot is claimed, then
    /// put back to work in front of the player.
    ///
    /// Everything about how it looks is authored here and nothing about the rules is. The
    /// discovery service says when it is found; what it does then is the feature beside it on the
    /// same object (<see cref="IDiscoveryFeature"/>), which this hands every tap to once it is.
    /// </summary>
    public sealed class DiscoverySite : MonoBehaviour, ITappable, IInteractableObject
    {
        [SerializeField] GameLifetimeScope scope;

        [SerializeField] DiscoverySiteDefinition definition;

        [Tooltip("Shown until the site is found: boards over the windows, weeds, rubbish, tape.")]
        [SerializeField] GameObject[] derelict;

        [Tooltip("Shown once it is found: lights on, signs up, the place swept.")]
        [SerializeField] GameObject[] restored;

        [Tooltip("Where its buttons float.")]
        [SerializeField] Transform marker;

        [Tooltip("The question mark over a site still to be found, before the feature's own icon takes over.")]
        [SerializeField] Sprite mysteryIcon;

        [Tooltip("Seconds between checks of whether the button should stand.")]
        [SerializeField] float refreshSeconds = 0.25f;

        GameComposition composition;
        IDiscoveryFeature feature;
        ActionPromptView prompt;
        bool promptIsMystery;
        bool found;
        bool registered;
        float nextRefresh;

        public DiscoverySiteDefinition Definition => definition;
        public string SiteId => definition != null ? definition.id : null;
        public bool IsFound => found;

        public string DisplayName => definition == null ? name : found ? definition.featureName : definition.displayName;

        public GameComposition Composition => composition;

        Transform Marker => marker != null ? marker : transform;

        void Awake() => feature = GetComponent<IDiscoveryFeature>();

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                Debug.LogError($"[DiscoverySite] {name} found no GameLifetimeScope.", this);
                return;
            }

            if (definition == null)
            {
                Debug.LogError($"[DiscoverySite] {name} has no site definition.", this);
                return;
            }

            // Derelict until the service says otherwise - which it may do inside Register.
            Show(false, animate: false);

            composition.Discovery.OnDiscovered += HandleDiscovered;
            registered = true;
            composition.Discovery.Register(definition);
        }

        void OnDestroy()
        {
            if (!registered) return;

            composition.Discovery.OnDiscovered -= HandleDiscovered;
            HidePrompt();
        }

        void Update()
        {
            if (!registered) return;

            nextRefresh -= Time.unscaledDeltaTime;
            if (nextRefresh > 0f) return;
            nextRefresh = refreshSeconds;

            DrawPrompt();
        }

        void HandleDiscovered(DiscoverySiteDefinition site, bool announce)
        {
            if (site != definition) return;

            Show(true, animate: announce);
            feature?.OnFound(announce);

            if (announce) Announce();
        }

        // --- What the player sees ------------------------------------------------------------------

        void Show(bool isFound, bool animate)
        {
            found = isFound;

            SetAll(derelict, !found);
            SetAll(restored, found);
            HidePrompt();
            nextRefresh = 0f;

            if (!found || !animate || restored == null) return;

            // Put back to work in front of the player: what was boarded up springs back into shape.
            foreach (GameObject part in restored)
            {
                if (part == null) continue;

                Transform t = part.transform;
                Vector3 scale = t.localScale;

                t.DOKill(true);
                t.localScale = new Vector3(scale.x, scale.y * 0.05f, scale.z);
                t.DOScale(scale, 0.6f).SetEase(Ease.OutBack);
            }
        }

        /// <summary>Found just now: the camera comes round to it, and a card says what it is for.</summary>
        void Announce()
        {
            CameraPanZoomRig rig = FindFirstObjectByType<CameraPanZoomRig>();
            if (rig != null) rig.PanTo(transform.position);

            composition.WorldUi.ShowReward(Marker.position + Vector3.up * 1.2f, "Discovered!");

            string title = $"Discovered: {definition.displayName}";
            bool opened = BuildChoicePopup.Open(composition, this, () =>
            {
                var offer = new BuildChoiceOffer
                {
                    Title = title,
                    Description = definition.featureBlurb,
                    Status = $"{definition.featureName} unlocked!",
                };
                offer.Options.Add(new BuildChoice("TAKE A LOOK", true, null));
                offer.Options.Add(new BuildChoice("LATER", true, null));
                return offer;
            }, choice =>
            {
                BuildChoicePopup.Close(composition, this);
                if (choice == 0) TapFeedback.Report(feature?.OnSiteTapped());
            });

            if (!opened) TapFeedback.Report($"{title}! {definition.featureName} unlocked.");
        }

        void DrawPrompt()
        {
            WorldUiCatalog catalog = composition.WorldUi.Catalog;
            if (catalog == null) return;

            bool mystery = !found;
            bool wanted = mystery || (feature != null && feature.WantsPrompt);

            if (!wanted)
            {
                HidePrompt();
                return;
            }

            if (prompt != null && prompt.IsLive && promptIsMystery == mystery) return;

            HidePrompt();

            ActionPromptView prefab = mystery ? catalog.mysteryPrompt : catalog.sitePrompt;
            if (prefab == null) return;

            Sprite icon = mystery ? mysteryIcon : feature?.PromptIcon;
            prompt = composition.WorldUi.ShowButton(prefab, Marker, Vector3.zero, icon, () => TapFeedback.Report(OnTapped()));
            promptIsMystery = mystery;
        }

        void HidePrompt()
        {
            if (prompt != null && prompt.IsLive && prompt.Anchor == Marker) prompt.Release();
            prompt = null;
        }

        // --- Taps ----------------------------------------------------------------------------------

        public bool OnTouch() => TapFeedback.Report(OnTapped());

        public string OnTapped()
        {
            if (composition == null || definition == null) return $"The {name} is not connected to the game.";

            if (!found) return composition.Discovery.WhyUndiscovered(definition.id);

            return feature != null ? feature.OnSiteTapped() : $"{definition.featureName} is ready.";
        }

        [ContextMenu("Discover Now (Play Mode)")]
        void DiscoverNow()
        {
            if (composition == null || definition == null) return;
            if (!composition.Discovery.Discover(definition.id)) Debug.Log($"[DiscoverySite] {definition.id} was already found.", this);
        }

        static void SetAll(GameObject[] objects, bool active)
        {
            if (objects == null) return;

            foreach (GameObject item in objects)
                if (item != null && item.activeSelf != active) item.SetActive(active);
        }

        /// <summary>Editor wiring, for the setup tool. Keeps the fields private to everything else.</summary>
        public void Configure(GameLifetimeScope lifetimeScope, DiscoverySiteDefinition site, GameObject[] ruined, GameObject[] working,
                              Transform buttonAnchor, Sprite questionMark)
        {
            scope = lifetimeScope;
            definition = site;
            derelict = ruined;
            restored = working;
            marker = buttonAnchor;
            mysteryIcon = questionMark;
        }
    }
}
