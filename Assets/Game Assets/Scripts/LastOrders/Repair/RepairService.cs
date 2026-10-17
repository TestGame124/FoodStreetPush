using System;
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Economy;
using LastOrders.Jobs;
using UnityEngine;

namespace LastOrders.Repair
{
    /// <summary>
    /// What is broken, who has been sent to fix it, and what fixing it costs.
    ///
    /// A plain class with no scene in it. Walls, gates and placed objects register themselves
    /// while they are in the world; the overlay in the scene asks what needs a button and shows
    /// the answer. A repair is an ordinary job on the queue, for the role the tuning names - the
    /// Helper by default - so the worker walks over, works for a few seconds and the thing comes
    /// back whole. Nothing mends by itself: a wall knocked down tonight is still down tomorrow
    /// night unless somebody went out and rebuilt it in between.
    ///
    /// Repairs are asked for in daylight only. Anything already queued at dusk goes back on the
    /// queue with its progress kept when the worker takes cover, and is finished in the morning.
    /// </summary>
    public sealed class RepairService
    {
        /// <summary>Every repair job's target ID starts with this, so the queue can say what it is.</summary>
        public const string JobPrefix = "repair:";

        readonly RepairTuning tuning;
        readonly JobService jobs;
        readonly ResourceLedger ledger;
        readonly IWallet coins;
        readonly DayCycleMachine machine;
        readonly Func<bool> breached;

        readonly List<IRepairable> repairables = new List<IRepairable>();
        readonly Dictionary<IRepairable, Job> active = new Dictionary<IRepairable, Job>();

        public RepairService(RepairTuning tuning, JobService jobs, ResourceLedger ledger, IWallet coins,
                             DayCycleMachine machine, Func<bool> breached)
        {
            this.tuning = tuning ?? new RepairTuning();
            this.jobs = jobs;
            this.ledger = ledger;
            this.coins = coins;
            this.machine = machine;
            this.breached = breached;

            if (machine != null) machine.OnPhaseChanged += HandlePhaseChanged;
        }

        public RepairTuning Tuning => tuning;

        public IReadOnlyList<IRepairable> All => repairables;

        /// <summary>Who does the work, in the player's words.</summary>
        /// <summary>Who will come: the repair role, or the main character while nobody of that role is on duty.</summary>
        public string WorkerName
        {
            get
            {
                WorkerRole who = jobs != null && jobs.Covers(WorkerRole.MainCharacter, tuning.role) ? WorkerRole.MainCharacter : tuning.role;
                return who == WorkerRole.MainCharacter ? "Main Character" : who.ToString();
            }
        }

        /// <summary>A repair has been paid for and a worker asked to go.</summary>
        public event Action<IRepairable> OnRepairQueued;

        /// <summary>The worker finished: it is whole again.</summary>
        public event Action<IRepairable> OnRepaired;

        /// <summary>Morning, with how many things the night left broken. Not raised on a morning
        /// with nothing to fix.</summary>
        public event Action<int> OnDamageFound;

        // --- The scene registers --------------------------------------------------------------

        public void Register(IRepairable repairable)
        {
            if (repairable == null || repairables.Contains(repairable)) return;

            repairables.Add(repairable);

            // A job raised before this piece went away and came back is still its job.
            Job existing = jobs?.FindActiveFor(JobPrefix + repairable.RepairId);
            if (existing != null) Track(repairable, existing);
        }

        /// <summary>Gone from the world: nothing left to walk to, so any job for it is called off.</summary>
        public void Unregister(IRepairable repairable)
        {
            if (repairable == null) return;

            repairables.Remove(repairable);

            if (!active.TryGetValue(repairable, out Job job)) return;

            active.Remove(repairable);
            job.OnCompleted -= HandleCompleted;
            job.Cancel();
        }

        // --- Questions ------------------------------------------------------------------------

        /// <summary>Hit, or knocked down.</summary>
        public static bool NeedsRepair(IRepairable repairable)
        {
            if (repairable == null || IsGone(repairable)) return false;
            return repairable.IsDestroyed || repairable.Health < repairable.MaxHealth - 0.01f;
        }

        /// <summary>The unfinished repair job for it, or null.</summary>
        public Job JobOf(IRepairable repairable)
        {
            if (repairable == null || !active.TryGetValue(repairable, out Job job)) return null;

            if (!job.IsFinished) return job;

            active.Remove(repairable);
            return null;
        }

        public bool IsQueued(IRepairable repairable) => JobOf(repairable) != null;

        /// <summary>How much of it is missing: 1 for something knocked down, 0 for something whole.</summary>
        public static float DamageFraction(IRepairable repairable)
        {
            if (repairable == null || repairable.IsDestroyed) return 1f;
            if (repairable.MaxHealth <= 0f) return 0f;

            return Mathf.Clamp01(1f - repairable.Health / repairable.MaxHealth);
        }

        /// <summary>What repairing it costs now: the full price for something knocked down, its
        /// share of it for something only hit.</summary>
        public Price PriceOf(IRepairable repairable) =>
            tuning.fullRepairCost.IsFree ? default : tuning.fullRepairCost.Scaled(DamageFraction(repairable));

        /// <summary>Seconds of work once the worker is standing at it.</summary>
        public float WorkSecondsFor(IRepairable repairable)
        {
            float seconds = tuning.baseSeconds + tuning.secondsForFullDamage * DamageFraction(repairable);
            if (repairable != null && repairable.IsDestroyed) seconds += tuning.extraSecondsWhenDestroyed;

            return Mathf.Max(0.1f, seconds);
        }

        /// <summary>True when a daylight tap would send somebody: the phase is right and nothing is
        /// inside the walls. The overlay shows no buttons otherwise.</summary>
        public bool IsRepairTime(out string reason)
        {
            if (machine != null && !machine.CurrentPhase.AllowsBuilding())
            {
                reason = "Nobody goes out to fix things after dark. It will have to wait for the morning.";
                return false;
            }

            if (breached != null && breached())
            {
                reason = "Not with zombies inside the walls. Clear them out first.";
                return false;
            }

            reason = null;
            return true;
        }

        public bool CanRepair(IRepairable repairable, out string reason)
        {
            if (repairable == null || IsGone(repairable) || !repairables.Contains(repairable))
            {
                reason = "There is nothing there to repair.";
                return false;
            }

            string name = repairable.DisplayName;

            if (!repairable.IsRepairableNow)
            {
                reason = $"The {Lower(name)} cannot be reached right now.";
                return false;
            }

            if (!NeedsRepair(repairable))
            {
                reason = $"The {Lower(name)} is in one piece.";
                return false;
            }

            Job job = JobOf(repairable);
            if (job != null)
            {
                reason = job.State == JobState.Working
                    ? $"Repairing the {Lower(name)}... {job.Progress:P0}"
                    : $"The {WorkerName} is on the way to repair the {Lower(name)}.";
                return false;
            }

            if (!IsRepairTime(out reason)) return false;

            reason = Till.Shortfall(PriceOf(repairable), ledger, coins);
            return reason == null;
        }

        // --- Doing it -------------------------------------------------------------------------

        /// <summary>Pays for the repair and puts it on the queue. The message is the tap's reply
        /// either way: who is going, or why nobody is.</summary>
        public bool RequestRepair(IRepairable repairable, out string message)
        {
            if (!CanRepair(repairable, out message)) return false;

            Price price = PriceOf(repairable);
            if (!Till.TryPay(price, ledger, coins, out message)) return false;

            Job job = jobs.Queue(JobPrefix + repairable.RepairId, repairable.RepairTarget, tuning.role,
                                 repairable.RepairPoint, WorkSecondsFor(repairable),
                                 $"Repair {repairable.DisplayName}");
            Track(repairable, job);

            string paid = price.IsFree ? string.Empty : $" ({price.Describe(Till.CoinName(coins))})";
            message = $"The {WorkerName} is on the way to repair the {Lower(repairable.DisplayName)}{paid}.";

            OnRepairQueued?.Invoke(repairable);
            return true;
        }

        void Track(IRepairable repairable, Job job)
        {
            if (active.TryGetValue(repairable, out Job previous) && ReferenceEquals(previous, job)) return;

            if (previous != null) previous.OnCompleted -= HandleCompleted;

            active[repairable] = job;
            job.OnCompleted -= HandleCompleted;
            job.OnCompleted += HandleCompleted;
        }

        void HandleCompleted(Job job)
        {
            job.OnCompleted -= HandleCompleted;

            IRepairable done = null;
            foreach (KeyValuePair<IRepairable, Job> entry in active)
            {
                if (!ReferenceEquals(entry.Value, job)) continue;

                done = entry.Key;
                break;
            }

            if (done == null) return;

            active.Remove(done);
            if (IsGone(done)) return;

            done.Repair();

            Debug.Log($"[Last Orders] The {WorkerName} repaired the {Lower(done.DisplayName)}.");
            OnRepaired?.Invoke(done);
        }

        // --- Morning --------------------------------------------------------------------------

        void HandlePhaseChanged(DayPhase previous, DayPhase current)
        {
            if (current != DayPhase.Dawn || previous == current) return;

            int broken = CountNeedingRepair();
            if (broken > 0) OnDamageFound?.Invoke(broken);
        }

        public int CountNeedingRepair()
        {
            int broken = 0;
            for (int i = 0; i < repairables.Count; i++)
                if (repairables[i].IsRepairableNow && NeedsRepair(repairables[i])) broken++;

            return broken;
        }

        static bool IsGone(IRepairable repairable) => repairable is UnityEngine.Object o && o == null;

        static string Lower(string name) => string.IsNullOrEmpty(name) ? "thing" : name.ToLowerInvariant();
    }
}
