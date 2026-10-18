using LastOrders.Audio;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Jobs;
using LastOrders.Progression;
using UnityEngine;

namespace LastOrders.Interaction
{
    /// <summary>
    /// The entrance debris of section 13, step 1: tap it, the Helper clears it, the diner
    /// entrance opens and 8 wood arrives.
    ///
    /// It is the first thing the player touches, so it is also the first place every rule in
    /// section 4 has to hold: the tap reports what happened, a second tap says the work is
    /// already queued rather than queueing it twice, and the reward is granted through an ID so
    /// that closing the app mid-clear cannot pay it out again.
    /// </summary>
    public sealed class ClearableRubble : MonoBehaviour, ITappable, IInteractableObject
    {
        [SerializeField] GameLifetimeScope scope;

        [Tooltip("Stable identity for the job queue. Survives the object being pooled or moved.")]
        [SerializeField] string targetId = "B1_entrance_debris";

        [Tooltip("13 step 1 and the 14 ledger both put the entrance debris at 8 wood.")]
        [SerializeField] int woodReward = 8;

        [Tooltip("5: tutorial repairs take 5-10 seconds of work AFTER travel.")]
        [SerializeField] float workSeconds = 5f;

        [Tooltip("Where the Helper stands to work. The object's own position when left empty.")]
        [SerializeField] Transform interactionPoint;

        [SerializeField] string displayName = "Entrance debris";

        GameComposition composition;
        Job job;

        public string DisplayName => displayName;
        public bool IsCleared { get; private set; }

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
                Debug.LogError($"[ClearableRubble] {name} found no GameLifetimeScope.", this);
        }


        /// <summary>
        /// The same tap, arriving from the project's own Mouse3D instead of the grey-box
        /// selector. Both input layers end up in the same method; only the plumbing differs.
        /// </summary>
        public bool OnTouch() => TapFeedback.Report(OnTapped());

        public string OnTapped()
        {
            if (IsCleared) return $"{displayName} is already clear.";

            if (composition == null) return "The game systems are not running.";

            if (job != null && !job.IsFinished)
            {
                // 4: a tap is never silently accepted. The honest answer to the second tap is
                // that the work is already in hand, with how far along it is.
                return job.State == JobState.Working
                    ? $"Clearing the entrance... {job.Progress:P0}"
                    : "Helper is on the way to clear the entrance.";
            }

            Vector3 point = interactionPoint != null ? interactionPoint.position : WorkPoint();

            job = composition.Jobs.Queue(targetId, transform, WorkerRole.Helper, point,
                                         workSeconds, $"Clear {displayName}");

            job.OnCompleted += HandleCleared;
            return "Helper sent to clear the entrance.";
        }

        /// <summary>
        /// Stand on the approach side rather than inside the pile.
        ///
        /// 18 puts the door throat at X 19-21 and the gate-to-porch route south of it, so the
        /// Helper works from the south - which is also the side the player is looking from.
        /// </summary>
        Vector3 WorkPoint() => transform.position + new Vector3(0f, 0f, -1.4f);

        void HandleCleared(Job completed)
        {
            completed.OnCompleted -= HandleCleared;

            IsCleared = true;

            // Granted through an ID, so a relaunch mid-clear cannot pay it twice (13, 22).
            composition.Progression.GrantOnce(ProgressionRewards.ClearEntranceWood,
                () => composition.Ledger.Add(ResourceType.Wood, woodReward));

            composition.Progression.Set(ProgressionFlags.EntranceCleared);

            SoundController.Play(SoundId.DebrisCleared, transform.position);
            gameObject.SetActive(false);
        }
    }
}
