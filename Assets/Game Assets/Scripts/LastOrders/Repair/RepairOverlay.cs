using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.Interaction;
using LastOrders.Jobs;
using LastOrders.UI.World;
using UnityEngine;

namespace LastOrders.Repair
{
    /// <summary>
    /// What the player sees of the repair list: a wrench over everything broken, and a timer
    /// over everything somebody has been sent to fix.
    ///
    /// One component for the whole level rather than one per wall, because the walls, the gates
    /// and the placed objects are three different kinds of thing and the list already has all of
    /// them. The wrenches are up in daylight only - nobody is sent out after dark - and pressing
    /// one is the whole of asking for a repair: the worker named in the tuning walks over, works
    /// for a few seconds, and "Repaired!" rises off it.
    /// </summary>
    [DefaultExecutionOrder(1140)]
    public sealed class RepairOverlay : MonoBehaviour
    {
        [SerializeField] GameLifetimeScope scope;

        [Tooltip("Seconds between checks of what needs a wrench.")]
        [SerializeField] float refreshInterval = 0.25f;

        readonly Dictionary<IRepairable, ActionPromptView> buttons = new Dictionary<IRepairable, ActionPromptView>();
        readonly Dictionary<IRepairable, WorkProgressView> bars = new Dictionary<IRepairable, WorkProgressView>();
        readonly List<IRepairable> scratch = new List<IRepairable>();

        GameComposition composition;
        RepairService repairs;
        float nextRefresh;
        float reportIn = -1f;
        int brokenToReport;

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                Debug.LogError("[RepairOverlay] No GameLifetimeScope, so nothing broken can be seen or repaired.", this);
                enabled = false;
                return;
            }

            repairs = composition.Repairs;
            repairs.OnRepaired += HandleRepaired;
            repairs.OnDamageFound += HandleDamageFound;
        }

        void OnDestroy()
        {
            if (repairs == null) return;

            repairs.OnRepaired -= HandleRepaired;
            repairs.OnDamageFound -= HandleDamageFound;
        }

        void OnDisable()
        {
            if (composition == null || !gameObject.scene.isLoaded) return;

            foreach (KeyValuePair<IRepairable, ActionPromptView> entry in buttons) ReleaseIfOurs(entry.Value, entry.Key);
            foreach (KeyValuePair<IRepairable, WorkProgressView> entry in bars) ReleaseIfOurs(entry.Value, entry.Key);

            buttons.Clear();
            bars.Clear();
        }

        void Update()
        {
            if (repairs == null) return;

            DrawBars();
            ReportDamage();

            nextRefresh -= Time.unscaledDeltaTime;
            if (nextRefresh > 0f) return;

            nextRefresh = refreshInterval;
            Refresh();
        }

        /// <summary>Puts a wrench over everything broken and a timer over everything in hand, and
        /// takes down whatever is no longer either.</summary>
        void Refresh()
        {
            bool daylight = repairs.IsRepairTime(out _);
            IReadOnlyList<IRepairable> all = repairs.All;

            for (int i = 0; i < all.Count; i++)
            {
                IRepairable repairable = all[i];
                if (IsGone(repairable)) continue;

                bool reachable = repairable.IsRepairableNow;
                Job job = repairs.JobOf(repairable);

                bool wantButton = daylight && reachable && job == null && RepairService.NeedsRepair(repairable);
                bool wantBar = daylight && reachable && job != null;

                SyncButton(repairable, wantButton);
                SyncBar(repairable, wantBar);
            }

            // Anything that has left the list since - knocked out of the world, pooled - loses both.
            scratch.Clear();
            foreach (IRepairable repairable in buttons.Keys) if (!Listed(repairable)) scratch.Add(repairable);
            foreach (IRepairable repairable in bars.Keys) if (!Listed(repairable) && !scratch.Contains(repairable)) scratch.Add(repairable);

            for (int i = 0; i < scratch.Count; i++)
            {
                SyncButton(scratch[i], false);
                SyncBar(scratch[i], false);
            }

            scratch.Clear();
        }

        bool Listed(IRepairable repairable)
        {
            if (IsGone(repairable)) return false;

            IReadOnlyList<IRepairable> all = repairs.All;
            for (int i = 0; i < all.Count; i++)
                if (ReferenceEquals(all[i], repairable)) return true;

            return false;
        }

        void SyncButton(IRepairable repairable, bool want)
        {
            buttons.TryGetValue(repairable, out ActionPromptView button);
            bool live = Owns(button, repairable);

            if (want == live) return;

            if (!want)
            {
                ReleaseIfOurs(button, repairable);
                buttons.Remove(repairable);
                return;
            }

            button = composition.WorldUi.ShowRepairPrompt(repairable.RepairAnchor, () => Press(repairable));
            if (button != null) buttons[repairable] = button;
        }

        void SyncBar(IRepairable repairable, bool want)
        {
            bars.TryGetValue(repairable, out WorkProgressView bar);
            bool live = Owns(bar, repairable);

            if (want == live) return;

            if (!want)
            {
                ReleaseIfOurs(bar, repairable);
                bars.Remove(repairable);
                return;
            }

            bar = composition.WorldUi.ShowProgress(repairable.RepairAnchor);
            if (bar == null) return;

            bar.SetWaiting();
            bars[repairable] = bar;
        }

        /// <summary>The timers, every frame: counting down while the worker is at it, waiting while they walk.</summary>
        void DrawBars()
        {
            foreach (KeyValuePair<IRepairable, WorkProgressView> entry in bars)
            {
                WorkProgressView bar = entry.Value;
                if (!Owns(bar, entry.Key)) continue;

                Job job = repairs.JobOf(entry.Key);

                if (job != null && job.State == JobState.Working) bar.SetProgress(job.Progress, job.WorkRemaining);
                else bar.SetWaiting();
            }
        }

        /// <summary>The wrench was pressed: pay and send the worker, or say why not.</summary>
        void Press(IRepairable repairable)
        {
            repairs.RequestRepair(repairable, out string message);
            TapFeedback.Report(message);

            // A refusal took the wrench down with the press; put it straight back.
            nextRefresh = 0f;
        }

        void HandleRepaired(IRepairable repairable)
        {
            SyncBar(repairable, false);
            if (IsGone(repairable)) return;

            Transform anchor = repairable.RepairAnchor;
            composition.WorldUi.ShowReward(anchor.position + Vector3.up * 0.6f, "Repaired!");
        }

        /// <summary>Said a moment after the morning comes in, so the HUD's own morning line does not
        /// overwrite it in the same frame.</summary>
        void HandleDamageFound(int broken)
        {
            brokenToReport = broken;
            reportIn = 0.5f;
        }

        void ReportDamage()
        {
            if (reportIn < 0f) return;

            reportIn -= Time.unscaledDeltaTime;
            if (reportIn > 0f) return;

            reportIn = -1f;

            string things = brokenToReport == 1 ? "1 thing" : $"{brokenToReport} things";
            TapFeedback.Report($"The night left {things} broken. Tap the wrench over each one to send the " +
                               $"{repairs.WorkerName} to fix it.");
        }

        /// <summary>
        /// Still showing, and still over this one. A pressed wrench goes back to the pool by
        /// itself and may already be over something else by the time this asks, and releasing it
        /// then would take down another wall's button.
        /// </summary>
        static bool Owns(WorldUiElement element, IRepairable repairable) =>
            element != null && element.IsLive && !IsGone(repairable) && element.Anchor == repairable.RepairAnchor;

        static void ReleaseIfOurs(WorldUiElement element, IRepairable repairable)
        {
            if (element == null || !element.IsLive) return;

            // Over something that has since been destroyed: nobody else can be using it.
            if (Owns(element, repairable) || element.Anchor == null) element.Release();
        }

        static bool IsGone(IRepairable repairable) =>
            repairable == null || repairable is Object o && o == null;
    }
}
