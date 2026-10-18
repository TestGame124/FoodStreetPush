using System.Collections.Generic;
using DG.Tweening;
using LastOrders.Audio;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Effects;
using LastOrders.Interaction;
using LastOrders.Perimeter;
using LastOrders.UI.World;
using UnityEngine;

namespace LastOrders.Defense
{
    /// <summary>
    /// A place on the compound wall where any defense can be mounted: a corn shooter, the scrap
    /// turret, the Toastapult - whichever the player picks.
    ///
    /// Empty, it is a stretch of ordinary wall marked out with pegs and twine and a build button over
    /// it. Building on it thickens that stretch of wall into a bastion - a wall piece of its own,
    /// broader and stronger than the wall it encloses - and the defense stands on top of it. The
    /// bastion is a real <see cref="CompoundWall"/>: zombies batter it, it shows damage, falls to
    /// rubble and is repaired by a worker like any other section, and the thin wall inside it is still
    /// there once it is down. While it is down, whatever stood on it is knocked over and out of action
    /// until the bastion is rebuilt.
    ///
    /// What was built is recorded under the site's id, which is also the defense's own, so its
    /// upgrades are kept with it. A site the scene starts with something on is never charged for it.
    ///
    /// A site with no bastion is a pad out in the defense ring, on the ground: the defense is set
    /// straight on it, and nothing under it can be knocked down.
    /// </summary>
    public sealed class WallSite : MonoBehaviour, ITappable, IInteractableObject
    {
        [SerializeField] GameLifetimeScope scope;

        [Tooltip("Stable identity. Recorded in progression, and the id of the defense built here.")]
        [SerializeField] string siteId = "wall";

        [Tooltip("What can be mounted here, in the order the popup lists them. Each needs its prefab set.")]
        [SerializeField] DefenseDefinition[] options;

        [Tooltip("Optional. Stands here from the start, never charged for.")]
        [SerializeField] DefenseDefinition startsWith;

        [Header("Parts")]
        [Tooltip("The thickened wall. Active while a defense stands here; its wall piece comes and goes with it.")]
        [SerializeField] GameObject bastion;

        [SerializeField] CompoundWall bastionWall;

        [Tooltip("Where the defense stands: on top of the bastion.")]
        [SerializeField] Transform mount;

        [Tooltip("What an empty site shows: where the bastion will go, pegged out.")]
        [SerializeField] GameObject emptyMarker;

        [Tooltip("Takes the tap over an empty site. Off once something is built: the defense takes its own.")]
        [SerializeField] BoxCollider tapCollider;

        [SerializeField] Vector3 buildButtonOffset = new Vector3(0f, 3.1f, 0f);

        [Header("Knocked over")]
        [Tooltip("Degrees the defense tips while the bastion under it is down.")]
        [SerializeField] float toppleDegrees = 24f;

        [Tooltip("Metres it drops, onto the rubble.")]
        [SerializeField] float toppleDrop = 1.7f;

        [Header("Standing here in the scene")]
        [SerializeField] DefensePost post;

        [SerializeField] DefenseDefinition postOf;

        GameComposition composition;
        ActionPromptView buildButton;
        DefenseDefinition built;
        bool toppled;

        static readonly List<WallSite> standing = new List<WallSite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStanding() => standing.Clear();

        /// <summary>Every site in the level right now, built on or not.</summary>
        public static IReadOnlyList<WallSite> Standing => standing;

        public string SiteId => siteId;

        /// <summary>What stands here. Null on an empty site.</summary>
        public DefenseDefinition Built => built;

        public DefensePost Post => post;

        public CompoundWall Bastion => bastionWall;

        /// <summary>A pad on the ground out in the defense ring, rather than a site on the wall.</summary>
        public bool OnGround => bastion == null;

        public string DisplayName => built != null ? built.displayName : OnGround ? "Defense pad" : "Wall site";

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                Debug.LogError($"[WallSite] {name} found no GameLifetimeScope.", this);
                return;
            }

            built = composition.Defense.BuiltAt(siteId, options) ?? startsWith;
            Show(animate: false);
        }

        void OnEnable()
        {
            if (!standing.Contains(this)) standing.Add(this);
        }

        void OnDisable() => standing.Remove(this);

        void OnDestroy()
        {
            if (gameObject.scene.isLoaded) HideBuildButton();
        }

        void Update()
        {
            if (composition == null || built == null || bastionWall == null || post == null || post.Unit == null) return;

            bool down = bastionWall.IsDestroyed;
            if (down != toppled) Topple(down);
        }

        // --- Drawing it ---------------------------------------------------------------------------------

        void Show(bool animate)
        {
            bool standing = built != null;

            SetActive(bastion, standing);
            SetActive(emptyMarker, !standing);
            if (tapCollider != null) tapCollider.enabled = !standing;

            if (standing)
            {
                HideBuildButton();
                EnsurePost(built);
            }
            else
            {
                ClearPost();
                ShowBuildButton();
            }

            if (!animate || !standing) return;

            // Dust round its foot as it goes up, like anything put down on the build grid.
            PlacementJuice.Burst(transform.position, 2.4f);

            // The wall swells into the bastion first, then the defense rises on top of it - or, on a
            // pad, straight away.
            if (bastion != null)
            {
                Transform wall = bastion.transform;
                wall.DOKill(true);
                wall.localScale = new Vector3(1f, 0.05f, 1f);
                wall.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
            }

            if (post == null) return;

            Transform gun = post.transform;
            gun.DOKill(true);
            gun.localScale = new Vector3(1f, 0.05f, 1f);
            gun.DOScale(Vector3.one, 0.45f).SetEase(Ease.OutBack).SetDelay(bastion != null ? 0.4f : 0f);
        }

        void EnsurePost(DefenseDefinition definition)
        {
            if (post != null && postOf == definition) return;

            ClearPost();

            if (definition.prefab == null)
            {
                Debug.LogWarning($"[WallSite] '{definition.displayName}' has no prefab, so nothing stands on {name}.", this);
                return;
            }

            GameObject instance = Instantiate(definition.prefab, mount != null ? mount : transform);
            instance.name = $"{definition.displayName}_{siteId}";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // Before its Start: it registers as already built, under this site's id.
            post = instance.GetComponent<DefensePost>();
            if (post != null) post.Configure(scope, definition, siteId, true);
            postOf = definition;
        }

        void ClearPost()
        {
            if (post != null) Destroy(post.gameObject);
            post = null;
            postOf = null;
            toppled = false;
        }

        /// <summary>The bastion under it fell, or was rebuilt: the defense goes over with it, and is set up again with it.</summary>
        void Topple(bool down)
        {
            toppled = down;
            composition.Defense.SetOutOfAction(post.PostId, down);

            Transform gun = post.transform;
            gun.DOKill(true);

            if (down)
            {
                gun.DOLocalMove(Vector3.down * toppleDrop, 0.35f).SetEase(Ease.InQuad);
                gun.DOLocalRotate(new Vector3(0f, 0f, toppleDegrees), 0.35f).SetEase(Ease.InQuad);
                composition.WorldUi.ShowWarning(transform.position + Vector3.up * 3f, $"{built.displayName} knocked over!");
                SoundController.Play(SoundId.StructureBreak, gun.position);
                return;
            }

            gun.DOLocalMove(Vector3.zero, 0.4f).SetEase(Ease.OutBack);
            gun.DOLocalRotate(Vector3.zero, 0.4f).SetEase(Ease.OutBack);
        }

        // --- Building ----------------------------------------------------------------------------------

        public bool OnTouch() => TapFeedback.Report(OnTapped());

        public string OnTapped()
        {
            if (composition == null) return "This wall site is not connected to the game.";
            if (built != null) return post != null ? composition.Defense.Describe(post.PostId) : null;

            return BuildChoicePopup.Open(composition, this, Offer, Take) ? null : "Nothing can be built here.";
        }

        BuildChoiceOffer Offer() => composition.Defense.GetSiteOffer(siteId, options, OnGround);

        void Take(int index)
        {
            bool done = composition.Defense.TryBuildAt(siteId, options, index, out string message, out DefenseDefinition chosen, OnGround);
            TapFeedback.Report(message);

            // A refusal leaves the popup open with the reason on it.
            if (!done) return;

            BuildChoicePopup.Close(composition, this);
            built = chosen;
            Show(animate: true);
        }

        void ShowBuildButton()
        {
            if (buildButton != null && buildButton.IsLive) return;

            WorldUiCatalog catalog = composition.WorldUi.Catalog;
            ActionPromptView prefab = catalog != null ? catalog.actionPrompt : null;
            buildButton = composition.WorldUi.ShowButton(prefab, transform, buildButtonOffset, null,
                                                         () => BuildChoicePopup.Open(composition, this, Offer, Take));
        }

        void HideBuildButton()
        {
            if (buildButton != null && buildButton.IsLive && buildButton.Anchor == transform) buildButton.Release();
            buildButton = null;
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(GameLifetimeScope lifetimeScope, string id, DefenseDefinition[] choices, DefenseDefinition starting,
                              GameObject raised, CompoundWall raisedWall, Transform top, GameObject marker, BoxCollider tap,
                              Vector3 buttonAt)
        {
            scope = lifetimeScope;
            siteId = id;
            options = choices;
            startsWith = starting;
            bastion = raised;
            bastionWall = raisedWall;
            mount = top;
            emptyMarker = marker;
            tapCollider = tap;
            buildButtonOffset = buttonAt;
        }

        /// <summary>Editor wiring for the defense shown in the scene before the game runs.</summary>
        public void ConfigurePost(DefensePost standing, DefenseDefinition of)
        {
            post = standing;
            postOf = of;
        }
    }
}
