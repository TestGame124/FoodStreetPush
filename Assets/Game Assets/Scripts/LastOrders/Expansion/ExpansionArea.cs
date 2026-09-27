using System.Collections.Generic;
using DG.Tweening;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Effects;
using LastOrders.Interaction;
using LastOrders.UI.Popups;
using LastOrders.UI.World;
using UnityEngine;

namespace LastOrders.Expansion
{
    /// <summary>
    /// A piece of land in the scene that the restaurant can reclaim: the debris standing on
    /// it, the way out to it, and everything the player sees as it changes hands.
    ///
    /// Everything about the area is authored here and nothing about the rules is. The debris
    /// under it is found automatically. Once it is cleared an expand button stands over the
    /// middle of the land; it opens a popup saying what expanding needs. Paying starts the
    /// build: a timer over the land and the new boundary rising a section at a time, with dust
    /// where each goes up. When it is done the old wall in front comes down and whatever the
    /// land is now for appears. Adding an area is placing one of these and filling it in.
    /// </summary>
    public sealed class ExpansionArea : MonoBehaviour
    {
        [SerializeField] GameLifetimeScope scope;

        [SerializeField] ExpansionAreaDefinition definition;

        [Tooltip("Points walked through, in order, to get from inside the compound out to this " +
                 "area - the gap in the wall. Walked backwards on the way home.")]
        [SerializeField] Transform[] approachRoute;

        [Tooltip("The middle of the land, where the expand button and the build timer float.")]
        [SerializeField] Transform centre;

        [Tooltip("Shown only while the area is unclaimed: the wall in front of it, warning signs.")]
        [SerializeField] GameObject[] lockedVisuals;

        [Tooltip("The new boundary, raised section by section while the area expands.")]
        [SerializeField] ExpansionBuildPlan buildPlan;

        [Tooltip("Played at the foot of each boundary section as it goes up.")]
        [SerializeField] PooledEffect buildEffect;

        [Tooltip("Shown once the area is claimed: what the land is now for.")]
        [SerializeField] GameObject[] unlockedContent;

        readonly List<string> debrisIds = new List<string>();

        GameComposition composition;
        Vector3[] route;
        ActionPromptView expandButton;
        WorkProgressView timer;
        bool registered;

        public ExpansionAreaDefinition Definition => definition;
        public string AreaId => definition != null ? definition.id : null;

        /// <summary>
        /// The approach as world points. One shared instance for the whole area, which is how a
        /// worker recognises that the next job is behind the same wall and skips the walk
        /// back inside between them.
        /// </summary>
        public IReadOnlyList<Vector3> Route => route;

        Transform Centre => centre != null ? centre : transform;

        void Awake()
        {
            route = BuildRoute();

            // Collected here, before anything registers, so the area never registers as an
            // empty one that would count as already cleared.
            foreach (Debris debris in GetComponentsInChildren<Debris>(true))
            {
                if (!string.IsNullOrEmpty(debris.DebrisId)) debrisIds.Add(debris.DebrisId);
            }
        }

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                Debug.LogError($"[ExpansionArea] {name} found no GameLifetimeScope.", this);
                return;
            }

            if (definition == null)
            {
                Debug.LogError($"[ExpansionArea] {name} has no area definition.", this);
                return;
            }

            composition.Expansion.OnAreaStateChanged += HandleStateChanged;
            composition.Expansion.OnAreaClaimed += HandleClaimed;
            composition.Expansion.RegisterArea(definition, debrisIds);
            registered = true;

            // Whatever state it came back in - claimed, part built, ready - show it as it is,
            // with no fanfare.
            Show(composition.Expansion.StateOf(AreaId), restoring: true);
        }

        void OnDestroy()
        {
            if (!registered) return;

            composition.Expansion.OnAreaStateChanged -= HandleStateChanged;
            composition.Expansion.OnAreaClaimed -= HandleClaimed;
        }

        void Update()
        {
            if (timer == null) return;

            float progress = composition.Expansion.ExpansionProgress(AreaId);
            timer.SetProgress(progress, composition.Expansion.ExpansionSecondsLeft(AreaId));

            if (buildPlan != null) buildPlan.SetProgress(progress, PlayBuildEffect);
        }

        void HandleStateChanged(string areaId, AreaState state)
        {
            if (areaId == AreaId && registered) Show(state, restoring: false);
        }

        void HandleClaimed(string areaId)
        {
            if (areaId != AreaId) return;

            RewardBundle reward = definition.claimReward;
            if (!reward.IsEmpty) composition.WorldUi.ShowReward(Centre.position, reward.Describe());

            string benefit = string.IsNullOrEmpty(definition.benefit) ? "" : $" {definition.benefit}.";
            TapFeedback.Report($"{definition.displayName} is part of the restaurant now!{benefit}");
        }

        // --- What the player sees at each stage ----------------------------------------------

        void Show(AreaState state, bool restoring)
        {
            switch (state)
            {
                case AreaState.ReadyToExpand:
                    HideTimer();
                    ShowExpandButton();
                    if (buildPlan != null) buildPlan.HideAll();
                    SetClaimed(false, animate: false);
                    break;

                case AreaState.Expanding:
                    HideExpandButton();
                    ClosePopup();
                    if (buildPlan != null) buildPlan.HideAll();
                    ShowTimer();
                    SetClaimed(false, animate: false);
                    break;

                case AreaState.Claimed:
                    HideExpandButton();
                    ClosePopup();
                    HideTimer();
                    if (buildPlan != null) buildPlan.Complete(!restoring, PlayBuildEffect);
                    SetClaimed(true, animate: !restoring);
                    break;

                default:
                    HideExpandButton();
                    HideTimer();
                    if (buildPlan != null) buildPlan.HideAll();
                    SetClaimed(false, animate: false);
                    break;
            }
        }

        void ShowExpandButton()
        {
            if (expandButton != null && expandButton.IsLive) return;
            expandButton = composition.WorldUi.ShowExpandPrompt(Centre, OpenPopup);
        }

        void HideExpandButton()
        {
            if (expandButton != null && expandButton.IsLive && expandButton.Anchor == Centre) expandButton.Release();
            expandButton = null;
        }

        void ShowTimer()
        {
            if (timer != null && timer.IsLive) return;

            timer = composition.WorldUi.ShowExpansionProgress(Centre);
            if (timer != null) timer.SetProgress(0f, composition.Expansion.ExpansionSecondsLeft(AreaId));
        }

        void HideTimer()
        {
            if (timer != null && timer.IsLive && timer.Anchor == Centre) timer.Release();
            timer = null;
        }

        /// <summary>The expand button was pressed: show what expanding needs.</summary>
        void OpenPopup()
        {
            WorldUiCatalog catalog = composition.WorldUi.Catalog;
            ExpansionPopupView prefab = catalog != null ? catalog.expansionPopup : null;

            ExpansionPopupView popup = composition.Popups.Show(prefab);
            if (popup == null) return;

            popup.Bind(() => composition.Expansion.GetOffer(AreaId), StartExpansion);
        }

        void StartExpansion()
        {
            bool started = composition.Expansion.StartExpansion(AreaId, out string message);
            TapFeedback.Report(message);

            // The state change closes the popup; a refusal leaves it open with the reason on it.
            if (started) ClosePopup();
        }

        void ClosePopup()
        {
            if (composition.Popups.Current is ExpansionPopupView) composition.Popups.Hide();
        }

        void PlayBuildEffect(Vector3 foot)
        {
            // At ground level: a section's foot is below ground while it is sunk to rise.
            composition.Effects.Play(buildEffect, new Vector3(foot.x, transform.position.y, foot.z));
        }

        void SetClaimed(bool claimed, bool animate)
        {
            SetAll(lockedVisuals, !claimed);
            SetAll(unlockedContent, claimed);

            if (!claimed || !animate || unlockedContent == null) return;

            // New land rises into place rather than blinking in, so the player sees what changed.
            foreach (GameObject item in unlockedContent)
            {
                if (item == null) continue;

                Transform t = item.transform;
                Vector3 scale = t.localScale;

                t.DOKill(true);
                t.localScale = new Vector3(scale.x, scale.y * 0.05f, scale.z);
                t.DOScale(scale, 0.5f).SetEase(Ease.OutBack);
            }
        }

        static void SetAll(GameObject[] objects, bool active)
        {
            if (objects == null) return;

            foreach (GameObject item in objects)
                if (item != null && item.activeSelf != active) item.SetActive(active);
        }

        Vector3[] BuildRoute()
        {
            if (approachRoute == null || approachRoute.Length == 0) return null;

            var points = new List<Vector3>(approachRoute.Length);
            foreach (Transform point in approachRoute)
                if (point != null) points.Add(point.position);

            return points.Count > 0 ? points.ToArray() : null;
        }

        /// <summary>Editor wiring, for the setup tool. Keeps the fields private to everything else.</summary>
        public void Configure(GameLifetimeScope lifetimeScope, ExpansionAreaDefinition area, Transform[] route,
                              Transform middle, GameObject[] locked, ExpansionBuildPlan boundary,
                              PooledEffect dust, GameObject[] unlocked)
        {
            scope = lifetimeScope;
            definition = area;
            approachRoute = route;
            centre = middle;
            lockedVisuals = locked;
            buildPlan = boundary;
            buildEffect = dust;
            unlockedContent = unlocked;
        }

        void OnDrawGizmosSelected()
        {
            if (approachRoute == null) return;

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            for (int i = 0; i < approachRoute.Length; i++)
            {
                if (approachRoute[i] == null) continue;

                Gizmos.DrawWireSphere(approachRoute[i].position, 0.35f);
                if (i > 0 && approachRoute[i - 1] != null)
                    Gizmos.DrawLine(approachRoute[i - 1].position, approachRoute[i].position);
            }
        }
    }
}
