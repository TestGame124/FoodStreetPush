using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.Jobs
{
    /// <summary>
    /// The task queue of section 21: it owns the queue, the claims and the work progress, and
    /// it owns nothing about the current HUD screen.
    ///
    /// This is the piece the audit found missing outright, and everything in sections 6, 8 and
    /// 13 waits on it - clearing, repair, planting, harvesting, cooking and serving are all the
    /// same shape once a worker can be asked to go and do something.
    ///
    /// Deliberately small. It answers three questions: what work is outstanding, who owns each
    /// piece of it, and is any of it finished. It does no pathfinding and no animation; the
    /// worker does both, which is what keeps this testable without a scene.
    /// </summary>
    public sealed class JobService
    {
        readonly List<Job> jobs = new List<Job>();

        int sequence;

        public event Action<Job> OnJobQueued;
        public event Action<Job> OnJobCompleted;
        public event Action<Job, JobState> OnJobStateChanged;

        public IReadOnlyList<Job> All => jobs;

        public int OpenCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < jobs.Count; i++)
                    if (jobs[i].IsOpen) n++;

                return n;
            }
        }

        /// <summary>
        /// Raises a job and puts it on the queue.
        ///
        /// Returns the existing job when one is already outstanding for the same target, so a
        /// player tapping the same rubble four times gets one clearing job and not four. 4 is
        /// explicit that a tap must never be silently accepted, but the answer to the second
        /// tap is "already queued", not "queued again".
        /// </summary>
        public Job Queue(string targetId, Transform target, WorkerRole role,
                         Vector3 interactionPoint, float workSeconds, string description,
                         IReadOnlyList<Vector3> route = null)
        {
            Job existing = FindActiveFor(targetId);
            if (existing != null) return existing;

            var job = new Job(targetId, target, role, interactionPoint, workSeconds, description,
                              sequence++, route);

            job.OnStateChanged += HandleStateChanged;
            job.OnCompleted += HandleCompleted;

            jobs.Add(job);
            OnJobQueued?.Invoke(job);

            return job;
        }

        /// <summary>Any unfinished job already raised against this target.</summary>
        public Job FindActiveFor(string targetId)
        {
            for (int i = 0; i < jobs.Count; i++)
            {
                if (jobs[i].TargetId == targetId && !jobs[i].IsFinished) return jobs[i];
            }

            return null;
        }

        /// <summary>
        /// The oldest outstanding job this worker can take.
        ///
        /// Oldest rather than nearest, per 6: ties resolve by creation time so a long-standing
        /// task cannot starve behind newer work that happens to be closer.
        /// </summary>
        public bool TryClaim(WorkerRole role, string workerId, out Job claimed)
        {
            claimed = null;
            int best = int.MaxValue;

            for (int i = 0; i < jobs.Count; i++)
            {
                Job job = jobs[i];

                if (job.RequiredRole != role && !Covers(role, job.RequiredRole)) continue;
                if (job.ReservedFor != null && job.ReservedFor != workerId) continue;
                if (job.State != JobState.Queued && job.State != JobState.Blocked) continue;
                if (job.Sequence >= best) continue;

                best = job.Sequence;
                claimed = job;
            }

            return claimed != null && claimed.TryReserve(workerId);
        }

        // --- Who is on duty ----------------------------------------------------------------------------

        readonly Dictionary<WorkerRole, HashSet<string>> onDuty = new Dictionary<WorkerRole, HashSet<string>>();

        /// <summary>
        /// A worker reports for duty (or goes off it). Once anybody has, the main character also takes on
        /// the work of any role nobody is on duty for: a diner that starts with no hired hands still gets
        /// its walls repaired, by the player's own character, until somebody is hired to do it.
        /// </summary>
        public void ReportOnDuty(WorkerRole role, string workerId, bool on)
        {
            if (string.IsNullOrEmpty(workerId)) return;

            if (!onDuty.TryGetValue(role, out HashSet<string> who))
            {
                if (!on) return;
                onDuty[role] = who = new HashSet<string>();
            }

            if (on) who.Add(workerId);
            else who.Remove(workerId);
        }

        /// <summary>True if anybody of <paramref name="role"/> is on duty.</summary>
        public bool IsStaffed(WorkerRole role) => onDuty.TryGetValue(role, out HashSet<string> who) && who.Count > 0;

        /// <summary>
        /// True if a worker of <paramref name="role"/> may take a job meant for <paramref name="needed"/>:
        /// the main character covers for roles with nobody on duty - once workers report at all, so a queue
        /// nobody reports to keeps strictly to roles.
        /// </summary>
        public bool Covers(WorkerRole role, WorkerRole needed)
        {
            if (role != WorkerRole.MainCharacter || needed == role || onDuty.Count == 0) return false;
            return !IsStaffed(needed);
        }

        /// <summary>
        /// Returns every claim held by one worker to the queue, keeping partial work.
        ///
        /// 6's phase-transition rule: finish the atomic operation, retain partial work, return
        /// unused reservations. A worker that is despawned or reassigned must not take its job
        /// with it.
        /// </summary>
        public void ReleaseAllFor(string workerId)
        {
            for (int i = 0; i < jobs.Count; i++)
                if (jobs[i].OwnerId == workerId) jobs[i].Release();
        }

        /// <summary>Drops finished jobs. Called at a phase boundary, not every frame.</summary>
        public int PruneFinished()
        {
            int removed = jobs.RemoveAll(j => j.IsFinished);
            return removed;
        }

        public void Clear() => jobs.Clear();

        void HandleStateChanged(Job job, JobState state) => OnJobStateChanged?.Invoke(job, state);

        void HandleCompleted(Job job) => OnJobCompleted?.Invoke(job);
    }
}
