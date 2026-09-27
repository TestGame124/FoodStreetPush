using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.Jobs
{
    /// <summary>The three workers of section 6. Players choose roles, never destinations.</summary>
    public enum WorkerRole
    {
        /// <summary>Serves ready meals, cooks queued meals, then cleans.</summary>
        Chef = 0,

        /// <summary>Explicit repair, build, harvest and carry jobs. Guard post at night.</summary>
        Helper = 1,

        /// <summary>Workshop output and repairs, after the first expedition rescues them.</summary>
        Mechanic = 2,

        /// <summary>The player's own character. Clears the debris standing between the
        /// restaurant and the land around it, until there are hired hands to do it instead.</summary>
        MainCharacter = 3,
    }

    /// <summary>
    /// The job lifecycle of section 6, in order.
    ///
    /// Delivering is kept even though nothing in the opening step uses it: the inventory
    /// abstraction says workers visibly carry things while the inventory itself stays shared,
    /// and that distinction only survives if the carrying leg is a state rather than an
    /// animation someone remembers to play.
    /// </summary>
    public enum JobState
    {
        Queued,
        Reserved,
        Travelling,
        Working,
        Delivering,
        Completed,
        Blocked,
        Cancelled,
    }

    /// <summary>
    /// One unit of work, as section 6 specifies it: an ID, a target, a required role, an
    /// interaction point, the work remaining, and where the output goes.
    ///
    /// A class rather than a struct because a job is referenced by the queue, by the worker
    /// that claimed it and by whatever raised it, and all three have to see the same state
    /// change. Its fields are deliberately not a general-purpose task system - the contract is
    /// the small one the document describes, and it grows when a step needs it to.
    /// </summary>
    public sealed class Job
    {
        static int nextId = 1;

        public readonly int Id;

        /// <summary>Stable identity of the thing being worked on, so a relocated target rebinds
        /// by ID rather than by a transform reference that may have been pooled away (6).</summary>
        public readonly string TargetId;

        public readonly Transform Target;
        public readonly WorkerRole RequiredRole;
        public readonly Vector3 InteractionPoint;

        /// <summary>
        /// Points to walk through, in order, before the interaction point. Null for a job that
        /// is a straight walk away.
        ///
        /// Exists because the walls are scene cubes the build grid cannot see, so nothing can
        /// path around them: work on the far side of one has to say where the way through is.
        /// </summary>
        public readonly IReadOnlyList<Vector3> Route;

        /// <summary>Seconds of work after travel. 5 puts tutorial repairs at 5-10.</summary>
        public float WorkRemaining;

        public readonly float WorkTotal;

        /// <summary>Creation order. 6: priority ties resolve by creation time, so an old task
        /// cannot starve behind a stream of new ones.</summary>
        public readonly int Sequence;

        public JobState State { get; private set; } = JobState.Queued;

        /// <summary>The single worker that owns this job. 6: a worker owns one job at a time,
        /// and a station or seat reservation has one owner.</summary>
        public string OwnerId { get; private set; }

        public string Description { get; }

        /// <summary>
        /// The one worker this job is held for, whatever role the queue would otherwise match.
        /// Null for an ordinary job anyone of the right role can take.
        ///
        /// Work a survivor was sent out to do stays theirs through a night indoors: released at
        /// dusk, it goes back on the queue for the morning, and without this the first idle
        /// worker of the job's role would walk out and take it off them.
        /// </summary>
        public string ReservedFor { get; private set; }

        /// <summary>Raised once, when the job reaches Completed.</summary>
        public event Action<Job> OnCompleted;

        /// <summary>Raised on every transition, for the HUD and the tutorial.</summary>
        public event Action<Job, JobState> OnStateChanged;

        public Job(string targetId, Transform target, WorkerRole role, Vector3 interactionPoint,
                   float workSeconds, string description, int sequence,
                   IReadOnlyList<Vector3> route = null)
        {
            Id = nextId++;
            TargetId = targetId;
            Target = target;
            RequiredRole = role;
            InteractionPoint = interactionPoint;
            Route = route != null && route.Count > 0 ? route : null;
            WorkTotal = Mathf.Max(0.01f, workSeconds);
            WorkRemaining = WorkTotal;
            Description = description;
            Sequence = sequence;
        }

        public bool IsOpen => State == JobState.Queued;
        public bool IsFinished => State == JobState.Completed || State == JobState.Cancelled;
        public float Progress => 1f - Mathf.Clamp01(WorkRemaining / WorkTotal);

        /// <summary>Claims the job for one worker. Returns false if someone already owns it.</summary>
        public bool TryReserve(string workerId)
        {
            if (State != JobState.Queued && State != JobState.Blocked) return false;

            OwnerId = workerId;
            Set(JobState.Reserved);
            return true;
        }

        /// <summary>
        /// Holds the job for one worker and claims it for them. True when it is theirs -
        /// including when they already had it - and false when it is held for someone else.
        /// </summary>
        public bool TryReserveFor(string workerId)
        {
            if (IsFinished || string.IsNullOrEmpty(workerId)) return false;
            if (ReservedFor != null && ReservedFor != workerId) return false;

            ReservedFor = workerId;
            return OwnerId == workerId || TryReserve(workerId);
        }

        public void BeginTravel() => Set(JobState.Travelling);
        public void BeginWork() => Set(JobState.Working);

        /// <summary>
        /// 6: if a path becomes invalid, retry after a navigation update and then mark the job
        /// Blocked with an icon. Blocked returns it to the pool rather than destroying it, so
        /// the work is not lost when the obstruction clears.
        /// </summary>
        public void Block()
        {
            OwnerId = null;
            Set(JobState.Blocked);
        }

        public void Complete()
        {
            if (State == JobState.Completed) return;

            WorkRemaining = 0f;
            Set(JobState.Completed);
            OnCompleted?.Invoke(this);
        }

        public void Cancel()
        {
            if (IsFinished) return;

            OwnerId = null;
            Set(JobState.Cancelled);
        }

        /// <summary>
        /// Releases a claim without cancelling the work. Used by the phase transition rule in 6:
        /// finish the atomic operation, retain partial work, return unused reservations.
        /// </summary>
        public void Release()
        {
            if (IsFinished) return;

            OwnerId = null;
            Set(JobState.Queued);
        }

        void Set(JobState next)
        {
            if (State == next) return;

            State = next;
            OnStateChanged?.Invoke(this, next);
        }

        public override string ToString() => $"#{Id} {Description} ({State})";
    }
}
