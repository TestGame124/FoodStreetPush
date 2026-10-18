using DG.Tweening;
using LastOrders.Audio;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Field;
using LastOrders.Interaction;
using LastOrders.Jobs;
using LastOrders.UI.World;
using UnityEngine;

namespace LastOrders.Expansion
{
    /// <summary>
    /// One piece of debris in the world: tap it, press the hammer, and the main character
    /// comes over and smashes it. Debris standing on an expansion area is the exception - it is
    /// cleared by whoever the player sends to the area, so tapping it opens that choice instead.
    ///
    /// A view and nothing more. Whether it can be cleared, who goes, what it pays and what it
    /// means for the land around it are all decided by the services; this asks, shows the
    /// answer, and plays the part the player watches - the button, the timer, the blows
    /// landing, the pile collapsing and the "+2 XP".
    ///
    /// Tapped through either input layer, like every Last Orders tappable: Mouse3D calls
    /// OnTouch, the grey-box selector calls OnTapped, and both end in the same place.
    /// </summary>
    public sealed class Debris : MonoBehaviour, ITappable, IInteractableObject, IWorkStrikeReceiver
    {
        [SerializeField] GameLifetimeScope scope;

        [Tooltip("Stable identity. Recorded when cleared, so it must not change once shipped.")]
        [SerializeField] string debrisId;

        [SerializeField] DebrisDefinition definition;

        [Tooltip("Where the worker stands to work. Worked out from the pile when left empty.")]
        [SerializeField] Transform workPoint;

        [Tooltip("The top of the pile, where the button and the timer float.")]
        [SerializeField] Transform uiAnchor;

        [Tooltip("What shakes on each blow and collapses at the end. This object when left empty.")]
        [SerializeField] Transform visual;

        GameComposition composition;
        ExpansionArea area;
        WorkProgressView progress;
        bool subscribed;
        bool collapsing;

        public string DebrisId => debrisId;
        public DebrisDefinition Definition => definition;
        public string DisplayName => definition != null ? definition.displayName : name;

        void Awake()
        {
            area = GetComponentInParent<ExpansionArea>();
            if (visual == null) visual = transform;

            if (string.IsNullOrEmpty(debrisId))
            {
                Debug.LogError($"[Debris] '{name}' has no debris id, so its clearing cannot be recorded. " +
                               "Run the expansion setup or give it one.", this);
            }
        }

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                Debug.LogError($"[Debris] '{name}' found no GameLifetimeScope.", this);
                return;
            }

            if (definition == null || string.IsNullOrEmpty(debrisId))
            {
                Debug.LogError($"[Debris] '{name}' is missing its definition or id and cannot be cleared.", this);
                return;
            }

            EnsureAnchors();

            DebrisState state = composition.Clearing.Register(
                debrisId, area != null ? area.AreaId : null, definition, transform,
                workPoint.position, area != null ? area.Route : null);

            composition.Clearing.OnDebrisStateChanged += HandleStateChanged;
            composition.Clearing.OnDebrisCleared += HandleCleared;
            subscribed = true;

            if (state == DebrisState.Cleared)
            {
                gameObject.SetActive(false);
                return;
            }

            if (state == DebrisState.Queued || state == DebrisState.Working) ShowProgress();
        }

        static bool quitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetQuitting()
        {
            quitting = false;
            Application.quitting -= MarkQuitting;
            Application.quitting += MarkQuitting;
        }

        static void MarkQuitting() => quitting = true;

        void OnDisable()
        {
            // Not while the scene is coming down: the pool would re-parent the timer under a
            // root that is being destroyed in the same pass, which Unity refuses loudly.
            if (composition == null || quitting || !gameObject.scene.isLoaded) return;

            ReleaseProgress();
            if (composition.WorldUi.IsPromptOpenFor(uiAnchor)) composition.WorldUi.HidePrompt();
        }

        void OnDestroy()
        {
            if (!subscribed || composition == null) return;

            composition.Clearing.OnDebrisStateChanged -= HandleStateChanged;
            composition.Clearing.OnDebrisCleared -= HandleCleared;
        }

        void Update()
        {
            if (progress == null) return;

            Job job = composition.Clearing.JobOf(debrisId);

            if (job != null && job.State == JobState.Working) progress.SetProgress(job.Progress, job.WorkRemaining);
            else progress.SetWaiting();
        }

        // --- Tap -------------------------------------------------------------------------

        public bool OnTouch() => TapFeedback.Report(OnTapped());

        public string OnTapped()
        {
            if (composition == null) return "The game systems are not running.";
            if (collapsing) return null;

            // Debris on a piece of land is cleared by whoever is sent to the land, not a pile at
            // a time: the tap opens the panel for choosing who goes.
            if (area != null && composition.Clearing.StateOf(debrisId) != DebrisState.Cleared &&
                FieldPanels.OpenArea(composition, area.AreaId))
            {
                return null;
            }

            // Refused, in progress or out of reach: the reason is the whole answer, and there
            // is no button to show.
            if (!composition.Clearing.CanClear(debrisId, out string reason)) return reason;

            // Tapping the same pile again leaves its button where it is.
            if (!composition.WorldUi.IsPromptOpenFor(uiAnchor))
                composition.WorldUi.ShowPrompt(uiAnchor, definition.actionIcon, Confirm);

            return $"{DisplayName}: tap the hammer to clear it.";
        }

        /// <summary>The hammer was pressed.</summary>
        void Confirm()
        {
            composition.Clearing.RequestClear(debrisId, out string message);
            TapFeedback.Report(message);
        }

        // --- What the player watches -------------------------------------------------------

        void HandleStateChanged(string id, DebrisState state)
        {
            if (id != debrisId) return;

            switch (state)
            {
                case DebrisState.Queued:
                case DebrisState.Working:
                    ShowProgress();
                    break;

                case DebrisState.Idle:
                    ReleaseProgress();
                    break;
            }
        }

        void HandleCleared(string id, RewardBundle paid)
        {
            if (id != debrisId) return;

            ReleaseProgress();
            if (composition.WorldUi.IsPromptOpenFor(uiAnchor)) composition.WorldUi.HidePrompt();

            if (!paid.IsEmpty) composition.WorldUi.ShowReward(uiAnchor.position, paid.Describe());

            SoundController.Play(SoundId.DebrisCleared, transform.position);
            Collapse();
        }

        /// <summary>One blow of the hammer landed.</summary>
        public void OnStrike()
        {
            if (collapsing || visual == null) return;

            SoundController.Play(SoundId.WorkChop, visual.position);

            // Completed, not killed, so a punch cut short by the next one snaps back to the
            // true scale instead of drifting a little larger with every blow.
            visual.DOKill(true);
            visual.DOPunchScale(visual.localScale * 0.08f, 0.18f, 8, 0.6f);
        }

        void Collapse()
        {
            collapsing = true;

            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;

            visual.DOKill(true);

            Sequence fall = DOTween.Sequence().SetTarget(this);
            fall.Append(visual.DOScale(visual.localScale * 0.05f, 0.45f).SetEase(Ease.InBack));
            fall.Join(visual.DOMove(visual.position + Vector3.down * 0.4f, 0.45f).SetEase(Ease.InQuad));
            fall.OnComplete(() => gameObject.SetActive(false));
        }

        void ShowProgress()
        {
            if (progress != null && progress.IsLive) return;

            progress = composition.WorldUi.ShowProgress(uiAnchor);
            if (progress != null) progress.SetWaiting();
        }

        void ReleaseProgress()
        {
            if (progress == null) return;

            // Checked, because the service may already have taken it back.
            if (progress.IsLive && progress.Anchor == uiAnchor) progress.Release();
            progress = null;
        }

        // --- Placement -------------------------------------------------------------------

        /// <summary>
        /// Fills in the work point and the UI anchor when they were not authored: stand at the
        /// edge of the pile on the side the worker arrives from, float the UI just over the top.
        /// </summary>
        void EnsureAnchors()
        {
            Bounds bounds = VisualBounds();

            if (uiAnchor == null)
            {
                uiAnchor = new GameObject("UiAnchor").transform;
                uiAnchor.SetParent(transform, false);
                uiAnchor.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            }

            if (workPoint == null)
            {
                Vector3 from = area != null && area.Route != null && area.Route.Count > 0
                    ? area.Route[area.Route.Count - 1]
                    : bounds.center + Vector3.back;

                Vector3 direction = from - bounds.center;
                direction.y = 0f;
                if (direction.sqrMagnitude < 1e-4f) direction = Vector3.back;
                direction.Normalize();

                float reach = Mathf.Max(bounds.extents.x, bounds.extents.z) + 0.6f;

                workPoint = new GameObject("WorkPoint").transform;
                workPoint.SetParent(transform, false);
                workPoint.position = new Vector3(bounds.center.x, transform.position.y, bounds.center.z) +
                                     direction * reach;
            }
        }

        Bounds VisualBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(transform.position + Vector3.up, Vector3.one);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(GameLifetimeScope lifetimeScope, string id, DebrisDefinition type,
                              Transform stand, Transform anchor, Transform shape)
        {
            scope = lifetimeScope;
            debrisId = id;
            definition = type;
            workPoint = stand;
            uiAnchor = anchor;
            visual = shape;
        }

        void OnDrawGizmosSelected()
        {
            if (workPoint == null) return;

            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.9f);
            Gizmos.DrawWireSphere(workPoint.position, 0.3f);
            Gizmos.DrawLine(workPoint.position, transform.position);
        }
    }
}
