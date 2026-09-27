using System;
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Jobs;
using LastOrders.Progression;
using UnityEngine;

namespace LastOrders.Expansion
{
    /// <summary>Where one piece of debris stands.</summary>
    public enum DebrisState
    {
        /// <summary>Standing, and nobody has been asked to clear it.</summary>
        Idle,

        /// <summary>A clearing job exists and a worker is on the way, or will be.</summary>
        Queued,

        /// <summary>A worker is at it.</summary>
        Working,

        /// <summary>Gone, permanently.</summary>
        Cleared,
    }

    /// <summary>
    /// Turns "clear this" into a job, and a finished job into a cleared piece of debris.
    ///
    /// The rules of section 4 live here rather than on the debris object: a request is either
    /// accepted or refused with a reason, a second request for the same pile is "already in
    /// hand" rather than a second job, and the payout goes through an ID so it can only happen
    /// once. The debris object only asks and shows.
    ///
    /// Listens to the job queue once, for every piece, and matches finished jobs by target ID.
    /// Subscribing per request would subscribe twice whenever the queue hands back a job it
    /// already had.
    ///
    /// Work done on a piece is kept here, not on its job. A worker recalled halfway through a
    /// pile, or knocked down beside it, leaves the pile half cleared: the job is cancelled, the
    /// seconds left are written back to the piece, and whoever is sent next starts from there.
    /// </summary>
    public sealed class DebrisClearingService
    {
        /// <summary>Prefix on the job queue's target IDs, so debris jobs can never collide with
        /// a stove or a table that happens to share a name.</summary>
        public const string JobPrefix = "debris:";

        sealed class Record
        {
            public string Id;
            public string AreaId;
            public DebrisDefinition Definition;
            public Transform Target;
            public Vector3 WorkPoint;
            public IReadOnlyList<Vector3> Route;
            public DebrisState State;
            public Job Job;

            /// <summary>Seconds of work still to do, kept from a job that was cancelled part
            /// way. Negative while nobody has touched the piece.</summary>
            public float WorkLeft = -1f;
        }

        readonly JobService jobs;
        readonly ExpansionService expansion;
        readonly RewardService rewards;
        readonly ProgressionService progression;
        readonly DayCycleMachine machine;
        readonly Dictionary<string, Record> records = new Dictionary<string, Record>();

        public event Action<string, DebrisState> OnDebrisStateChanged;

        /// <summary>Raised once per piece, with what it actually paid - empty if its reward
        /// had somehow already been granted.</summary>
        public event Action<string, RewardBundle> OnDebrisCleared;

        public DebrisClearingService(JobService jobs, ExpansionService expansion, RewardService rewards,
                                     ProgressionService progression, DayCycleMachine machine)
        {
            this.jobs = jobs;
            this.expansion = expansion;
            this.rewards = rewards;
            this.progression = progression;
            this.machine = machine;

            jobs.OnJobStateChanged += HandleJobStateChanged;
            jobs.OnJobCompleted += HandleJobCompleted;
        }

        /// <summary>
        /// Adds a piece of debris. <paramref name="areaId"/> may be empty, for debris that is
        /// not part of any expansion and can always be worked.
        ///
        /// Returns the state it starts in, so the object can hide itself at once when it was
        /// cleared before.
        /// </summary>
        public DebrisState Register(string id, string areaId, DebrisDefinition definition,
                                    Transform target, Vector3 workPoint, IReadOnlyList<Vector3> route)
        {
            if (string.IsNullOrEmpty(id) || definition == null)
            {
                Debug.LogError("[Expansion] Debris registered with no id or no definition.", target);
                return DebrisState.Idle;
            }

            if (records.TryGetValue(id, out Record existing))
            {
                // The same object re-registering after being toggled is fine; two objects
                // sharing an id is a content bug that would clear one when the other finishes.
                if (existing.Target != target)
                {
                    Debug.LogError($"[Expansion] Two debris objects share the id '{id}'. Give each " +
                                   "its own; the second was ignored.", target);
                }

                return existing.State;
            }

            var record = new Record
            {
                Id = id,
                AreaId = areaId,
                Definition = definition,
                Target = target,
                WorkPoint = workPoint,
                Route = route,
                State = progression.Has(ExpansionIds.DebrisCleared(id)) ? DebrisState.Cleared : DebrisState.Idle,
            };

            // A job may already be running for it - the object was disabled and re-enabled
            // while its clearing was in hand. Adopt it rather than raising a second.
            if (record.State != DebrisState.Cleared)
            {
                Job running = jobs.FindActiveFor(JobPrefix + id);
                if (running != null)
                {
                    record.Job = running;
                    record.State = running.State == JobState.Working ? DebrisState.Working : DebrisState.Queued;
                }
            }

            records.Add(id, record);
            return record.State;
        }

        public bool IsRegistered(string id) => !string.IsNullOrEmpty(id) && records.ContainsKey(id);

        public DebrisState StateOf(string id) =>
            TryGet(id, out Record record) ? record.State : DebrisState.Idle;

        public string AreaOf(string id) => TryGet(id, out Record record) ? record.AreaId : null;

        /// <summary>The job clearing this piece, while there is one. For progress displays.</summary>
        public Job JobOf(string id) => TryGet(id, out Record record) ? record.Job : null;

        public string DisplayNameOf(string id) =>
            TryGet(id, out Record record) ? record.Definition.displayName : null;

        public Vector3 WorkPointOf(string id) => TryGet(id, out Record record) ? record.WorkPoint : Vector3.zero;

        /// <summary>
        /// How much of the piece is cleared, 0 to 1 - from the job while one is running, from
        /// the work kept on the piece while none is.
        /// </summary>
        public float ProgressOf(string id)
        {
            if (!TryGet(id, out Record record)) return 0f;
            if (record.State == DebrisState.Cleared) return 1f;
            if (record.Job != null && !record.Job.IsFinished) return record.Job.Progress;
            if (record.WorkLeft < 0f) return 0f;

            return 1f - Mathf.Clamp01(record.WorkLeft / Mathf.Max(0.01f, record.Definition.workSeconds));
        }

        /// <summary>Every piece in the area, cleared or not. For "3 of 6 cleared".</summary>
        public int CountIn(string areaId)
        {
            int count = 0;
            foreach (Record record in records.Values)
                if (record.AreaId == areaId) count++;

            return count;
        }

        /// <summary>Adds the pieces in the area still standing to <paramref name="into"/>.</summary>
        public void CollectStanding(string areaId, List<string> into)
        {
            foreach (Record record in records.Values)
                if (record.AreaId == areaId && record.State != DebrisState.Cleared) into.Add(record.Id);
        }

        /// <summary>The way out to an area, as its debris registered it. Null when it has none.</summary>
        public IReadOnlyList<Vector3> RouteOf(string areaId)
        {
            foreach (Record record in records.Values)
                if (record.AreaId == areaId && record.Route != null) return record.Route;

            return null;
        }

        /// <summary>
        /// Whether a clearing request would be accepted, and if not, why - in words the player
        /// can act on.
        /// </summary>
        public bool CanClear(string id, out string reason)
        {
            if (!TryGet(id, out Record record))
            {
                reason = "There is nothing here to clear.";
                return false;
            }

            string name = record.Definition.displayName;

            switch (record.State)
            {
                case DebrisState.Cleared:
                    reason = $"The {name} is already cleared.";
                    return false;

                case DebrisState.Queued:
                    reason = $"Already heading over to clear the {name}.";
                    return false;

                case DebrisState.Working:
                    float progress = record.Job != null ? record.Job.Progress : 0f;
                    reason = $"Clearing the {name}... {progress:P0}";
                    return false;
            }

            if (!string.IsNullOrEmpty(record.AreaId) && expansion.StateOf(record.AreaId) == AreaState.Locked)
            {
                reason = expansion.WhyLocked(record.AreaId) ?? $"The {name} is out of reach for now.";
                return false;
            }

            // The yard is outside the walls. Work already under way finishes; nobody new is sent
            // out once the light goes.
            if (machine != null && !machine.CurrentPhase.AllowsBuilding())
            {
                reason = "Too dangerous to work outside after dark. Wait for dawn.";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// Sends a worker to clear the piece. Returns false, with the reason in
        /// <paramref name="message"/>, when the request is refused.
        /// </summary>
        public bool RequestClear(string id, out string message)
        {
            if (!CanClear(id, out message)) return false;

            Record record = records[id];
            DebrisDefinition definition = record.Definition;

            record.Job = QueueJob(record);

            SetState(record, record.Job.State == JobState.Working ? DebrisState.Working : DebrisState.Queued);

            message = $"Heading out to clear the {definition.displayName}.";
            return true;
        }

        /// <summary>
        /// Raises the clearing job for one particular worker and claims it for them: the
        /// survivor assigned to the area the piece stands in. Hands back the job they already
        /// hold on it rather than a second one, and takes the piece off anyone else who had it,
        /// keeping their work. Null, with the reason, when the piece cannot be worked.
        /// </summary>
        public Job RequestClearBy(string id, string workerId, out string message)
        {
            if (!TryGet(id, out Record record))
            {
                message = "There is nothing here to clear.";
                return null;
            }

            Job held = record.Job;
            if (held != null && !held.IsFinished)
            {
                if (held.OwnerId == workerId || held.ReservedFor == workerId || held.ReservedFor == null && held.IsOpen)
                {
                    if (held.TryReserveFor(workerId))
                    {
                        message = null;
                        return held;
                    }
                }

                // Somebody else's: they stop, and what they did stays on the piece.
                held.Cancel();
            }

            if (!CanClear(id, out message)) return null;

            record.Job = QueueJob(record);
            record.Job.TryReserveFor(workerId);

            SetState(record, DebrisState.Queued);
            return record.Job;
        }

        /// <summary>
        /// Stops the work on a piece and keeps what was done. The recall and the knock-down both
        /// end here: the pile stays part cleared, ready for whoever comes next.
        /// </summary>
        public void CancelClear(string id)
        {
            if (TryGet(id, out Record record) && record.Job != null) record.Job.Cancel();
        }

        Job QueueJob(Record record)
        {
            DebrisDefinition definition = record.Definition;

            Job job = jobs.Queue(JobPrefix + record.Id, record.Target, definition.role, record.WorkPoint,
                                 definition.workSeconds, $"Clear {definition.displayName}", record.Route);

            // A fresh job starts where the last one on this piece stopped.
            if (record.WorkLeft > 0f && job.WorkRemaining >= job.WorkTotal)
                job.WorkRemaining = Mathf.Min(record.WorkLeft, job.WorkTotal);

            return job;
        }

        void HandleJobStateChanged(Job job, JobState state)
        {
            if (!TryGetForJob(job, out Record record)) return;

            switch (state)
            {
                case JobState.Working:
                    SetState(record, DebrisState.Working);
                    break;

                case JobState.Queued:
                case JobState.Reserved:
                case JobState.Travelling:
                case JobState.Blocked:
                    SetState(record, DebrisState.Queued);
                    break;

                case JobState.Cancelled:
                    if (record.Job == job) record.WorkLeft = job.WorkRemaining;
                    record.Job = null;
                    SetState(record, DebrisState.Idle);
                    break;
            }
        }

        void HandleJobCompleted(Job job)
        {
            if (!TryGetForJob(job, out Record record) || record.State == DebrisState.Cleared) return;

            record.Job = null;
            progression.Set(ExpansionIds.DebrisCleared(record.Id));

            bool paid = rewards.Grant(ExpansionIds.DebrisReward(record.Id), record.Definition.reward);

            SetState(record, DebrisState.Cleared);
            OnDebrisCleared?.Invoke(record.Id, paid ? record.Definition.reward : default);
        }

        bool TryGetForJob(Job job, out Record record)
        {
            record = null;
            if (job == null || job.TargetId == null || !job.TargetId.StartsWith(JobPrefix)) return false;

            return records.TryGetValue(job.TargetId.Substring(JobPrefix.Length), out record);
        }

        bool TryGet(string id, out Record record)
        {
            record = null;
            return !string.IsNullOrEmpty(id) && records.TryGetValue(id, out record);
        }

        void SetState(Record record, DebrisState next)
        {
            if (record.State == next) return;

            record.State = next;
            OnDebrisStateChanged?.Invoke(record.Id, next);
        }
    }
}
