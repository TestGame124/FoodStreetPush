using System;
using System.Collections.Generic;
using LastOrders.DayCycle;
using LastOrders.Expansion;
using LastOrders.Jobs;
using LastOrders.Medical;
using LastOrders.Progression;
using UnityEngine;

namespace LastOrders.Field
{
    /// <summary>What an assigned worker should be doing this moment.</summary>
    public enum FieldTaskKind
    {
        /// <summary>Nothing out there: not assigned, paused, or after dark.</summary>
        None,

        /// <summary>A zombie on the land. It goes before any of the debris does.</summary>
        Fight,

        /// <summary>A piece of debris, once nothing is left to fight.</summary>
        Clear,
    }

    public readonly struct FieldTask
    {
        public readonly FieldTaskKind Kind;
        public readonly string AreaId;

        /// <summary>The way out through the wall to the area. Null when it needs none.</summary>
        public readonly IReadOnlyList<Vector3> Route;

        public readonly IAreaZombie Zombie;
        public readonly string DebrisId;

        FieldTask(FieldTaskKind kind, string areaId, IReadOnlyList<Vector3> route, IAreaZombie zombie, string debrisId)
        {
            Kind = kind;
            AreaId = areaId;
            Route = route;
            Zombie = zombie;
            DebrisId = debrisId;
        }

        public static readonly FieldTask None = default;

        public static FieldTask Fight(string areaId, IReadOnlyList<Vector3> route, IAreaZombie zombie) =>
            new FieldTask(FieldTaskKind.Fight, areaId, route, zombie, null);

        public static FieldTask Clear(string areaId, IReadOnlyList<Vector3> route, string debrisId) =>
            new FieldTask(FieldTaskKind.Clear, areaId, route, null, debrisId);
    }

    /// <summary>Where a downed worker is on the way back to their feet.</summary>
    public enum CasualtyStage
    {
        /// <summary>Lying where they fell until somebody comes for them.</summary>
        AwaitingPickup,

        /// <summary>Over someone's shoulders on the way to the tent.</summary>
        Carried,

        /// <summary>At the tent: in a cot, or beside it until one comes free.</summary>
        AtHospital,
    }

    /// <summary>Why a worker stopped clearing an area.</summary>
    public enum FieldEnd
    {
        /// <summary>The player called them back.</summary>
        Recalled,

        /// <summary>Nothing left to clear.</summary>
        Finished,

        /// <summary>Sent somewhere else, or somebody else took the area over.</summary>
        Replaced,

        /// <summary>The land stopped being workable - claimed, or locked again.</summary>
        Unreachable,
    }

    /// <summary>A worker in a cot, as the hospital tent sees them.</summary>
    public sealed class WorkerPatient : IPatient
    {
        public readonly IFieldWorker Worker;

        public WorkerPatient(IFieldWorker worker) => Worker = worker;

        public string PatientId => $"worker:{Worker.WorkerId}";
        public string DisplayName => Worker.DisplayName;
        public PatientKind Kind => PatientKind.Worker;
    }

    /// <summary>One button on a worker or area panel: what it says, and whether it can be pressed now.</summary>
    public readonly struct FieldOption
    {
        public readonly string Label;
        public readonly bool Enabled;

        public FieldOption(string label, bool enabled)
        {
            Label = label;
            Enabled = enabled;
        }
    }

    /// <summary>
    /// Everything a worker's info panel - or an area's "who do I send" panel - shows, gathered
    /// so the popup has nothing to work out for itself.
    /// </summary>
    public sealed class FieldPanel
    {
        public string Title;
        public string Subtitle;

        public bool ShowHealth;
        public float Health;
        public string HealthText;

        /// <summary>Badly hurt or down: the bar goes red.</summary>
        public bool HealthLow;

        public string Status;
        public readonly List<string> Details = new List<string>();
        public readonly List<FieldOption> Options = new List<FieldOption>();

        public bool ShowRecall;
        public string RecallLabel = "RECALL WORKER";

        /// <summary>Why nothing on offer can be pressed, or a warning worth reading. Null otherwise.</summary>
        public string Reason;
    }

    /// <summary>
    /// The restaurant's people out past the walls: who is clearing which piece of land, what
    /// they should be doing there, and what happens when it goes wrong.
    ///
    /// The player sends a worker to an area. Out there the order never changes: every zombie on
    /// the land first, then the debris, nearest first, until the land is ready to expand into.
    /// The player can call them back at any moment, and nothing done is lost - piles cleared
    /// stay cleared, a half-cleared pile stays half cleared, and a dead zombie stays dead.
    ///
    /// A worker knocked down stops where they fell. Their area waits for them, paused; the
    /// hospital tent holds a cot for them straight away; and the first of the restaurant's
    /// staff to come free walks out, picks them up and carries them in - the Helper before
    /// anyone else, because the Chef leaving the stove is the last resort. Once they are well
    /// the paused area picks up where it stopped.
    ///
    /// Knows nothing about bodies. The workers and zombies in the scene answer its interfaces
    /// and do their own walking, fighting and carrying; this decides who and what and when.
    /// </summary>
    public sealed class FieldWorkService
    {
        sealed class Assignment
        {
            public string AreaId;
            public IFieldWorker Worker;

            /// <summary>Their worker is down. The area keeps everything and waits for them.</summary>
            public bool Paused;

            public IAreaZombie Zombie;

            /// <summary>Has landed a blow on the current zombie: fighting, not walking over.</summary>
            public bool Engaged;

            public string DebrisId;
        }

        sealed class Casualty
        {
            public IFieldWorker Worker;
            public WorkerPatient Patient;
            public CasualtyStage Stage;
            public IFieldWorker Rescuer;
            public string AreaId;
        }

        readonly DebrisClearingService clearing;
        readonly ExpansionService expansion;
        readonly HospitalService hospital;
        readonly ProgressionService progression;
        readonly DayCycleMachine machine;

        readonly List<IFieldWorker> workers = new List<IFieldWorker>();
        readonly Dictionary<string, Action> downedHandlers = new Dictionary<string, Action>();
        readonly List<IAreaZombie> zombies = new List<IAreaZombie>();
        readonly Dictionary<string, Assignment> assignments = new Dictionary<string, Assignment>();
        readonly List<Casualty> casualties = new List<Casualty>();
        readonly List<string> scratch = new List<string>();
        readonly List<Assignment> ending = new List<Assignment>();

        /// <summary>Sent out to an area.</summary>
        public event Action<IFieldWorker, string> OnAssigned;

        /// <summary>Stopped clearing an area, and why.</summary>
        public event Action<IFieldWorker, string, FieldEnd> OnAssignmentEnded;

        /// <summary>Knocked down out there. A cot is already held for them.</summary>
        public event Action<IFieldWorker> OnDowned;

        /// <summary>A cot came free for someone who was waiting beside the tent.</summary>
        public event Action<IFieldWorker, HospitalCot> OnCotReady;

        /// <summary>Out of the cot, well again.</summary>
        public event Action<IFieldWorker> OnRecovered;

        public event Action<IAreaZombie> OnZombieKilled;

        public FieldWorkService(DebrisClearingService clearing, ExpansionService expansion, HospitalService hospital,
                                ProgressionService progression, DayCycleMachine machine)
        {
            this.clearing = clearing;
            this.expansion = expansion;
            this.hospital = hospital;
            this.progression = progression;
            this.machine = machine;

            hospital.OnAdmitted += HandleAdmitted;
            hospital.OnDischarged += HandleDischarged;
            expansion.OnAreaStateChanged += HandleAreaStateChanged;
        }

        public IReadOnlyList<IFieldWorker> Workers => workers;

        public string HospitalName => hospital.DisplayName;

        // --- Registration ---------------------------------------------------------------------

        public void Register(IFieldWorker worker)
        {
            if (worker == null || worker.Health == null || Find(worker.WorkerId) != null) return;

            workers.Add(worker);

            Action handler = () => HandleDowned(worker);
            downedHandlers[worker.WorkerId] = handler;
            worker.Health.OnDowned += handler;
        }

        public void Unregister(IFieldWorker worker)
        {
            if (worker == null || !workers.Remove(worker)) return;

            if (downedHandlers.TryGetValue(worker.WorkerId, out Action handler))
            {
                worker.Health.OnDowned -= handler;
                downedHandlers.Remove(worker.WorkerId);
            }

            if (assignments.TryGetValue(worker.WorkerId, out Assignment assignment))
            {
                DropTask(assignment);
                assignments.Remove(worker.WorkerId);
            }

            DropRescue(worker);
            casualties.RemoveAll(c => c.Worker == worker);
        }

        public IFieldWorker Find(string workerId)
        {
            if (string.IsNullOrEmpty(workerId)) return null;

            for (int i = 0; i < workers.Count; i++)
                if (workers[i].WorkerId == workerId) return workers[i];

            return null;
        }

        public void RegisterZombie(IAreaZombie zombie)
        {
            if (zombie != null && !zombies.Contains(zombie)) zombies.Add(zombie);
        }

        public void UnregisterZombie(IAreaZombie zombie)
        {
            zombies.Remove(zombie);

            foreach (Assignment assignment in assignments.Values)
                if (assignment.Zombie == zombie) assignment.Zombie = null;
        }

        /// <summary>Every zombie lurking on land still to be cleared, alive or not.</summary>
        public IReadOnlyList<IAreaZombie> Zombies => zombies;

        /// <summary>Killed at some point, so it stays dead.</summary>
        public bool IsKilled(string zombieId) => progression.Has(FieldIds.ZombieKilled(zombieId));

        public int ZombiesLeft(string areaId)
        {
            int count = 0;
            for (int i = 0; i < zombies.Count; i++)
                if (zombies[i].AreaId == areaId && zombies[i].IsAlive) count++;

            return count;
        }

        // --- Sending people out ---------------------------------------------------------------

        /// <summary>Whether <paramref name="worker"/> could be sent to clear the area now, and if
        /// not, why - in words the player can act on.</summary>
        public bool CanAssign(string areaId, IFieldWorker worker, out string reason)
        {
            string area = AreaName(areaId);

            if (worker == null)
            {
                reason = "There is nobody to send.";
                return false;
            }

            if (!expansion.IsRegistered(areaId))
            {
                reason = "There is no land here to clear.";
                return false;
            }

            switch (expansion.StateOf(areaId))
            {
                case AreaState.Locked:
                    reason = expansion.WhyLocked(areaId) ?? $"The {area} is out of reach for now.";
                    return false;

                case AreaState.ReadyToExpand:
                    reason = $"The {area} is already cleared.";
                    return false;

                case AreaState.Expanding:
                case AreaState.Claimed:
                    reason = $"The {area} is already part of the restaurant.";
                    return false;
            }

            string name = worker.DisplayName;

            if (!worker.CanClearAreas)
            {
                reason = $"{name} is needed at the restaurant and cannot be sent outside the walls.";
                return false;
            }

            if (IsCasualty(worker))
            {
                reason = $"{name} is hurt and cannot work until they recover.";
                return false;
            }

            if (IsRescuing(worker))
            {
                reason = $"{name} is carrying someone to the {HospitalName}.";
                return false;
            }

            if (!AllowsWork() || worker.IsSheltering)
            {
                reason = "Too dangerous to work outside after dark. Wait for dawn.";
                return false;
            }

            Assignment mine = AssignmentOf(worker);
            if (mine != null && mine.AreaId == areaId)
            {
                reason = $"{name} is already clearing the {area}.";
                return false;
            }

            Assignment held = AssignmentOn(areaId);
            if (held != null && !held.Paused)
            {
                reason = $"{held.Worker.DisplayName} is already clearing the {area}. Recall them first.";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// Sends a worker to clear an area. Someone already clearing somewhere else moves over;
        /// an area paused because its worker is down is taken over. False, with the reason,
        /// when they cannot go.
        /// </summary>
        public bool Assign(string areaId, string workerId, out string message)
        {
            IFieldWorker worker = Find(workerId);
            if (!CanAssign(areaId, worker, out message)) return false;

            Assignment previous = AssignmentOf(worker);
            if (previous != null) End(previous, FieldEnd.Replaced);

            Assignment paused = AssignmentOn(areaId);
            if (paused != null) End(paused, FieldEnd.Replaced);

            assignments[workerId] = new Assignment { AreaId = areaId, Worker = worker };
            OnAssigned?.Invoke(worker, areaId);

            int left = ZombiesLeft(areaId);
            string threat = left > 0 ? $" {left} {Plural(left, "zombie", "zombies")} to deal with first." : string.Empty;

            message = $"{worker.DisplayName} is heading out to clear the {AreaName(areaId)}.{threat}";
            return true;
        }

        /// <summary>
        /// Calls a worker back - on their way, mid-fight, mid-pile, or down and waiting for a
        /// cot. Everything they did out there is kept.
        /// </summary>
        public bool Recall(string workerId, out string message)
        {
            IFieldWorker worker = Find(workerId);
            Assignment assignment = AssignmentOf(worker);

            if (assignment == null)
            {
                message = worker != null ? $"{worker.DisplayName} is not out clearing anything." : "There is nobody to recall.";
                return false;
            }

            string area = AreaName(assignment.AreaId);
            bool paused = assignment.Paused;

            End(assignment, FieldEnd.Recalled);

            message = paused
                ? $"{worker.DisplayName} will stay at the restaurant once they recover. The {area} keeps everything cleared so far."
                : $"{worker.DisplayName} is heading back. Everything cleared in the {area} is kept.";
            return true;
        }

        /// <summary>The area this worker is clearing, active or paused. Null when none.</summary>
        public string AreaOf(IFieldWorker worker) => AssignmentOf(worker)?.AreaId;

        /// <summary>Out there and working: assigned, and not paused.</summary>
        public bool IsOnActiveAssignment(IFieldWorker worker)
        {
            Assignment assignment = AssignmentOf(worker);
            return assignment != null && !assignment.Paused;
        }

        /// <summary>The worker clearing the area, active or paused. Null when nobody is.</summary>
        public IFieldWorker WorkerOn(string areaId) => AssignmentOn(areaId)?.Worker;

        public bool IsPaused(string areaId) => AssignmentOn(areaId)?.Paused ?? false;

        // --- Out there ------------------------------------------------------------------------

        /// <summary>
        /// What an assigned worker should do now: the nearest zombie while any is left on the
        /// land, then the nearest standing debris. Sticks with the current target until it is
        /// dealt with, so a worker does not turn away from a pile to one a step closer.
        /// </summary>
        public FieldTask NextTask(IFieldWorker worker)
        {
            Assignment assignment = AssignmentOf(worker);
            if (assignment == null || assignment.Paused || IsCasualty(worker) || !AllowsWork()) return FieldTask.None;

            IReadOnlyList<Vector3> route = clearing.RouteOf(assignment.AreaId);

            if (assignment.Zombie == null || !assignment.Zombie.IsAlive)
            {
                assignment.Zombie = NearestZombie(assignment.AreaId, worker.Position);
                assignment.Engaged = false;
            }

            if (assignment.Zombie != null)
            {
                // A pile half done can wait; something shambling up behind them cannot.
                if (assignment.DebrisId != null) CancelOwnJob(assignment);

                return FieldTask.Fight(assignment.AreaId, route, assignment.Zombie);
            }

            if (assignment.DebrisId == null || clearing.StateOf(assignment.DebrisId) == DebrisState.Cleared)
                assignment.DebrisId = NearestDebris(assignment.AreaId, worker.Position);

            if (assignment.DebrisId != null) return FieldTask.Clear(assignment.AreaId, route, assignment.DebrisId);

            // Nothing standing and nothing to fight. The area normally says so first, as it
            // turns ready to expand; this covers an area whose debris never registered.
            End(assignment, FieldEnd.Finished);
            return FieldTask.None;
        }

        /// <summary>The clearing job for the piece the worker is on, held for them. Null, with
        /// the reason, when it cannot be worked now.</summary>
        public Job ClaimDebrisJob(IFieldWorker worker, string debrisId, out string message)
        {
            Assignment assignment = AssignmentOf(worker);
            if (assignment == null || assignment.Paused)
            {
                message = $"{worker?.DisplayName ?? "Nobody"} is not clearing anything.";
                return null;
            }

            Job job = clearing.RequestClearBy(debrisId, worker.WorkerId, out message);

            // Refused - after dark, most likely. Picked again next time rather than held.
            if (job == null && assignment.DebrisId == debrisId) assignment.DebrisId = null;

            return job;
        }

        /// <summary>
        /// Puts down whatever the worker was doing out there, keeping it: the pile they were on
        /// stays part cleared. They stay assigned. The walk in at dusk ends here.
        /// </summary>
        public void DropTask(IFieldWorker worker)
        {
            Assignment assignment = AssignmentOf(worker);
            if (assignment != null) DropTask(assignment);
        }

        /// <summary>A blow landed on a zombie. True when it was the one that killed it.</summary>
        public bool StrikeZombie(IFieldWorker worker, IAreaZombie zombie, float damage)
        {
            if (zombie == null || !zombie.IsAlive || damage <= 0f || IsCasualty(worker)) return false;

            Assignment assignment = AssignmentOf(worker);
            if (assignment != null && assignment.Zombie == zombie) assignment.Engaged = true;

            zombie.ApplyDamage(damage);
            if (zombie.IsAlive) return false;

            RecordKill(zombie);
            return true;
        }

        /// <summary>
        /// A shot from a tower or turret landed on a zombie - one standing close enough to a
        /// defense to be in its sights. True when it was the one that killed it. Kept on record
        /// like a worker's kill, so it stays dead.
        /// </summary>
        public bool ShootZombie(IAreaZombie zombie, float damage)
        {
            if (zombie == null || !zombie.IsAlive || damage <= 0f) return false;

            zombie.ApplyDamage(damage);
            if (zombie.IsAlive) return false;

            RecordKill(zombie);
            return true;
        }

        void RecordKill(IAreaZombie zombie)
        {
            progression.Set(FieldIds.ZombieKilled(zombie.ZombieId));

            // Whoever was going for it has nothing left to fight.
            foreach (Assignment assignment in assignments.Values)
                if (assignment.Zombie == zombie) assignment.Zombie = null;

            OnZombieKilled?.Invoke(zombie);
        }

        /// <summary>
        /// Somebody a zombie can go for: out there on an active assignment, on their feet, in
        /// daylight. The downed are left alone, and so is whoever comes to carry them - a
        /// rescue that turns into a second casualty helps nobody.
        /// </summary>
        public bool IsExposed(IFieldWorker worker) =>
            worker != null && IsOnActiveAssignment(worker) && !IsCasualty(worker) && !worker.Health.IsDowned &&
            AllowsWork();

        /// <summary>The nearest exposed worker within <paramref name="radius"/> of a point, flat.
        /// Null when there is none.</summary>
        public IFieldWorker NearestExposed(Vector3 from, float radius)
        {
            float best = radius * radius;
            IFieldWorker found = null;

            for (int i = 0; i < workers.Count; i++)
            {
                if (!IsExposed(workers[i])) continue;

                float distance = FlatDistanceSquared(from, workers[i].Position);
                if (distance > best) continue;

                best = distance;
                found = workers[i];
            }

            return found;
        }

        /// <summary>A zombie's hit landed. Only on someone exposed; false when it did nothing.</summary>
        public bool DamageWorker(IFieldWorker worker, float damage)
        {
            if (damage <= 0f || !IsExposed(worker)) return false;

            worker.Health.Damage(damage);
            return true;
        }

        // --- Knocked down ---------------------------------------------------------------------

        void HandleDowned(IFieldWorker worker)
        {
            if (IsCasualty(worker)) return;

            Assignment assignment = AssignmentOf(worker);
            if (assignment != null)
            {
                DropTask(assignment);
                assignment.Paused = true;
            }

            // Somebody they were carrying is put down where they are.
            DropRescue(worker);

            var casualty = new Casualty
            {
                Worker = worker,
                Patient = new WorkerPatient(worker),
                Stage = CasualtyStage.AwaitingPickup,
                AreaId = assignment?.AreaId,
            };

            casualties.Add(casualty);

            // The cot is theirs from this moment; the walk over is the staff's to make.
            hospital.Admit(casualty.Patient);

            OnDowned?.Invoke(worker);
        }

        public bool IsCasualty(IFieldWorker worker) => Get(worker) != null;

        /// <summary>Where a downed worker is. Null for anyone on their feet.</summary>
        public CasualtyStage? StageOf(IFieldWorker worker) => Get(worker)?.Stage;

        /// <summary>Whoever is on the way to carry them, or carrying them. Null when nobody is yet.</summary>
        public IFieldWorker RescuerOf(IFieldWorker casualty) => Get(casualty)?.Rescuer;

        /// <summary>The casualty this worker is going for or carrying. Null when none.</summary>
        public IFieldWorker CasualtyCarriedBy(IFieldWorker carrier)
        {
            for (int i = 0; i < casualties.Count; i++)
                if (casualties[i].Rescuer == carrier && carrier != null) return casualties[i].Worker;

            return null;
        }

        public bool IsRescuing(IFieldWorker carrier) => CasualtyCarriedBy(carrier) != null;

        /// <summary>The way out through the wall to where the casualty fell. Null when they fell inside.</summary>
        public IReadOnlyList<Vector3> RouteToCasualty(IFieldWorker casualty)
        {
            Casualty record = Get(casualty);
            return record?.AreaId != null ? clearing.RouteOf(record.AreaId) : null;
        }

        /// <summary>
        /// Hands a casualty to <paramref name="carrier"/> if they are the one who should go:
        /// the oldest casualty nobody is going for yet, and only once nobody better placed is
        /// free - the Helper first, then any other staff, the Chef last.
        /// </summary>
        public bool TryTakeRescue(IFieldWorker carrier, out IFieldWorker casualty)
        {
            casualty = null;
            if (!CanCarryNow(carrier)) return false;

            Casualty next = null;
            for (int i = 0; i < casualties.Count; i++)
            {
                Casualty candidate = casualties[i];
                if (candidate.Stage != CasualtyStage.AwaitingPickup || candidate.Rescuer != null) continue;
                if (candidate.Worker == carrier) continue;

                next = candidate;
                break;
            }

            if (next == null) return false;

            int rank = CarryRank(carrier.Role);
            for (int i = 0; i < workers.Count; i++)
            {
                IFieldWorker other = workers[i];
                if (other != carrier && CarryRank(other.Role) < rank && CanCarryNow(other)) return false;
            }

            next.Rescuer = carrier;
            casualty = next.Worker;
            return true;
        }

        /// <summary>Picked up and on the way in.</summary>
        public void PickedUp(IFieldWorker casualty)
        {
            Casualty record = Get(casualty);
            if (record != null && record.Rescuer != null) record.Stage = CasualtyStage.Carried;
        }

        /// <summary>
        /// The carrier stopped - dusk, most likely. Whoever they were going for, or carrying, is
        /// left where they are for the next person to come for.
        /// </summary>
        public void DropRescue(IFieldWorker carrier)
        {
            if (carrier == null) return;

            for (int i = 0; i < casualties.Count; i++)
            {
                Casualty record = casualties[i];
                if (record.Rescuer != carrier) continue;

                record.Rescuer = null;
                if (record.Stage == CasualtyStage.Carried) record.Stage = CasualtyStage.AwaitingPickup;
            }
        }

        /// <summary>
        /// The casualty is at the tent. Into the cot held for them, which starts their
        /// treatment - or, with every cot taken, beside the tent until one frees. Returns the
        /// cot, or null while they wait.
        /// </summary>
        public HospitalCot Delivered(IFieldWorker casualty)
        {
            Casualty record = Get(casualty);
            if (record == null) return null;

            record.Stage = CasualtyStage.AtHospital;
            record.Rescuer = null;

            HospitalCot cot = hospital.CotOf(record.Patient);
            if (cot != null) hospital.MarkInBed(record.Patient);

            return cot;
        }

        /// <summary>The cot held for them. Null while they wait for one.</summary>
        public HospitalCot CotOf(IFieldWorker casualty)
        {
            Casualty record = Get(casualty);
            return record != null ? hospital.CotOf(record.Patient) : null;
        }

        /// <summary>How far back in the queue for a cot they are, from 0. -1 when not waiting.</summary>
        public int WaitingIndexOf(IFieldWorker casualty)
        {
            Casualty record = Get(casualty);
            if (record == null) return -1;

            IReadOnlyList<IPatient> waiting = hospital.Waiting;
            for (int i = 0; i < waiting.Count; i++)
                if (waiting[i].PatientId == record.Patient.PatientId) return i;

            return -1;
        }

        void HandleAdmitted(IPatient patient, HospitalCot cot)
        {
            if (!(patient is WorkerPatient worker)) return;

            // A cot given at the moment of falling is only held; one given to somebody already
            // lying beside the tent is theirs to get into.
            Casualty record = Get(worker.Worker);
            if (record == null || record.Stage != CasualtyStage.AtHospital) return;

            hospital.MarkInBed(patient);
            OnCotReady?.Invoke(worker.Worker, cot);
        }

        void HandleDischarged(IPatient patient)
        {
            if (!(patient is WorkerPatient worker)) return;

            Casualty record = Get(worker.Worker);
            if (record == null) return;

            casualties.Remove(record);
            worker.Worker.Health.Restore();

            // Their area waited for them. Back to it - unless there is nothing left to go back to.
            Assignment assignment = AssignmentOf(worker.Worker);
            if (assignment != null && assignment.Paused)
            {
                assignment.Paused = false;
                if (expansion.StateOf(assignment.AreaId) != AreaState.Available) End(assignment, FieldEnd.Finished);
            }

            OnRecovered?.Invoke(worker.Worker);
        }

        bool CanCarryNow(IFieldWorker worker) =>
            worker != null && worker.CanCarry && !worker.Health.IsDowned && !IsCasualty(worker) &&
            !IsRescuing(worker) && !IsOnActiveAssignment(worker) && !worker.IsSheltering && AllowsWork();

        /// <summary>Who goes first when somebody has to be carried in. Lower goes first.</summary>
        static int CarryRank(WorkerRole role)
        {
            switch (role)
            {
                case WorkerRole.Helper: return 0;
                case WorkerRole.Mechanic: return 1;
                case WorkerRole.MainCharacter: return 2;
                default: return 3;
            }
        }

        // --- Panels ---------------------------------------------------------------------------

        /// <summary>A worker's info panel: health, what they are doing, where they could be sent.</summary>
        public FieldPanel GetWorkerPanel(string workerId)
        {
            var panel = new FieldPanel();
            IFieldWorker worker = Find(workerId);

            if (worker == null)
            {
                panel.Title = "Worker";
                panel.Reason = "This worker is not connected to the game.";
                return panel;
            }

            panel.Title = worker.DisplayName;
            panel.Subtitle = worker.RoleName != worker.DisplayName ? worker.RoleName : null;
            FillHealth(panel, worker, prefix: null);
            panel.Status = StatusOf(worker);

            Assignment assignment = AssignmentOf(worker);
            if (assignment != null)
            {
                panel.Details.Add(ProgressLine(assignment.AreaId));

                if (assignment.Paused)
                    panel.Details.Add($"Clearing the {AreaName(assignment.AreaId)} is paused. Everything cleared so far is kept.");

                panel.ShowRecall = true;
            }
            else if (IsCasualty(worker))
            {
                panel.Details.Add("Resting until they are well. Nothing they did is lost.");
            }

            if (!worker.CanClearAreas)
                panel.Details.Add("Keeps the restaurant running - never sent outside the walls.");

            if (worker.CanCarry)
                panel.Details.Add($"Carries anyone knocked down to the {HospitalName}.");

            string refusal = null;
            List<string> areas = CandidateAreas(worker);

            for (int i = 0; i < areas.Count; i++)
            {
                bool ok = CanAssign(areas[i], worker, out string reason);
                refusal ??= ok ? null : reason;
                panel.Options.Add(new FieldOption($"Clear {AreaName(areas[i])}", ok));
            }

            panel.Reason = worker.Health.IsLow && IsExposed(worker)
                ? $"{worker.DisplayName} is badly hurt. Recall them before they go down."
                : AnyEnabled(panel) ? null : refusal;

            return panel;
        }

        /// <summary>Presses option <paramref name="index"/> on a worker's panel: sends them to that area.</summary>
        public bool TakeWorkerOption(string workerId, int index, out string message)
        {
            IFieldWorker worker = Find(workerId);
            List<string> areas = CandidateAreas(worker);

            if (worker == null || index < 0 || index >= areas.Count)
            {
                message = "There is nowhere to send them.";
                return false;
            }

            return Assign(areas[index], workerId, out message);
        }

        /// <summary>An area's panel: what is out there, and who could be sent to clear it.</summary>
        public FieldPanel GetAreaPanel(string areaId)
        {
            var panel = new FieldPanel();
            string area = AreaName(areaId);

            panel.Title = $"Clear the {area}";
            panel.Subtitle = expansion.DefinitionOf(areaId)?.benefit;

            Assignment held = AssignmentOn(areaId);
            if (held != null)
            {
                FillHealth(panel, held.Worker, prefix: held.Worker.DisplayName);
                panel.Status = held.Paused
                    ? $"Paused - {held.Worker.DisplayName} is hurt. Everything cleared so far is kept."
                    : StatusOf(held.Worker);

                panel.ShowRecall = true;
                panel.RecallLabel = $"RECALL {held.Worker.DisplayName.ToUpperInvariant()}";
            }
            else
            {
                panel.Status = expansion.StateOf(areaId) == AreaState.Available
                    ? "Send a worker to clear it. They deal with the zombies first."
                    : null;
            }

            panel.Details.Add(ProgressLine(areaId));

            string refusal = null;
            List<IFieldWorker> candidates = Candidates();

            for (int i = 0; i < candidates.Count; i++)
            {
                IFieldWorker worker = candidates[i];
                string label = worker.RoleName != worker.DisplayName ? $"{worker.DisplayName} ({worker.RoleName})" : worker.DisplayName;
                panel.Details.Add($"{label} - {HealthLine(worker)}, {ShortStatus(worker)}");

                bool ok = CanAssign(areaId, worker, out string reason);
                refusal ??= ok ? null : reason;
                panel.Options.Add(new FieldOption($"Send {worker.DisplayName}", ok));
            }

            if (candidates.Count == 0) refusal = "Nobody here can be sent outside the walls.";

            panel.Reason = AnyEnabled(panel) || held != null && !held.Paused ? null : refusal;
            return panel;
        }

        /// <summary>Presses option <paramref name="index"/> on an area's panel: sends that worker.</summary>
        public bool TakeAreaOption(string areaId, int index, out string message)
        {
            List<IFieldWorker> candidates = Candidates();

            if (index < 0 || index >= candidates.Count)
            {
                message = "There is nobody to send.";
                return false;
            }

            return Assign(areaId, candidates[index].WorkerId, out message);
        }

        /// <summary>What the worker is doing, in a sentence, for the panels.</summary>
        public string StatusOf(IFieldWorker worker)
        {
            if (worker == null) return null;

            Casualty casualty = Get(worker);
            if (casualty != null)
            {
                switch (casualty.Stage)
                {
                    case CasualtyStage.AwaitingPickup:
                        return casualty.Rescuer != null
                            ? $"Downed - {casualty.Rescuer.DisplayName} is on the way to carry them to the {HospitalName}"
                            : $"Downed - waiting to be carried to the {HospitalName}";

                    case CasualtyStage.Carried:
                        return $"Being carried to the {HospitalName} by {casualty.Rescuer?.DisplayName ?? "the staff"}";

                    default:
                        HospitalCot cot = hospital.CotOf(casualty.Patient);
                        if (cot == null || !cot.InBed) return $"Waiting for a free cot in the {HospitalName}";

                        float left = cot.SecondsLeft(hospital.TreatmentSpeed);
                        return $"Recovering in the {HospitalName}: {Mathf.RoundToInt(cot.Progress * 100f)}% ({FormatSeconds(left)} left)";
                }
            }

            IFieldWorker carrying = CasualtyCarriedBy(worker);
            if (carrying != null)
            {
                return StageOf(carrying) == CasualtyStage.Carried
                    ? $"Carrying {carrying.DisplayName} to the {HospitalName}"
                    : $"Going to carry {carrying.DisplayName} to the {HospitalName}";
            }

            Assignment assignment = AssignmentOf(worker);
            if (assignment != null)
            {
                string area = AreaName(assignment.AreaId);

                if (!AllowsWork() || worker.IsSheltering) return $"Sheltering for the night - back to the {area} at dawn";

                if (assignment.Zombie != null)
                    return assignment.Engaged ? $"Fighting zombies in the {area}" : $"Heading into the {area} - zombies there";

                if (assignment.DebrisId != null)
                {
                    string name = clearing.DisplayNameOf(assignment.DebrisId) ?? "debris";
                    return $"Clearing the {area}: {name} {Mathf.RoundToInt(clearing.ProgressOf(assignment.DebrisId) * 100f)}%";
                }

                return $"Heading to the {area}";
            }

            if (worker.IsSheltering) return "Sheltering in the diner for the night";

            return string.IsNullOrEmpty(worker.Activity) ? "Idle" : worker.Activity;
        }

        /// <summary>"Courtyard: 2 of 6 piles cleared, 1 zombie left".</summary>
        public string ProgressLine(string areaId)
        {
            int total = clearing.CountIn(areaId);
            scratch.Clear();
            clearing.CollectStanding(areaId, scratch);

            int cleared = total - scratch.Count;
            int left = ZombiesLeft(areaId);

            string line = $"{AreaName(areaId)}: {cleared} of {total} {Plural(total, "pile", "piles")} cleared";
            return left > 0 ? $"{line}, {left} {Plural(left, "zombie", "zombies")} left" : line;
        }

        void FillHealth(FieldPanel panel, IFieldWorker worker, string prefix)
        {
            WorkerHealth health = worker.Health;

            panel.ShowHealth = true;
            panel.Health = health.IsDowned ? 0f : health.Fraction;
            panel.HealthLow = health.IsDowned || health.IsLow;

            string line = health.IsDowned ? "Downed" : HealthLine(worker);
            panel.HealthText = prefix != null ? $"{prefix}: {line}" : line;
        }

        static string HealthLine(IFieldWorker worker)
        {
            WorkerHealth health = worker.Health;
            return health.IsDowned ? "downed" : $"{Mathf.CeilToInt(health.Current)}/{Mathf.RoundToInt(health.Max)} HP";
        }

        /// <summary>A few words on what they are up to, for a line in the area panel.</summary>
        string ShortStatus(IFieldWorker worker)
        {
            if (IsCasualty(worker)) return "hurt";
            if (IsRescuing(worker)) return "carrying someone";

            Assignment assignment = AssignmentOf(worker);
            if (assignment != null) return $"clearing the {AreaName(assignment.AreaId)}";

            return worker.IsSheltering ? "sheltering" : "at the restaurant";
        }

        /// <summary>Areas a worker's panel offers: workable land they are not already on.</summary>
        List<string> CandidateAreas(IFieldWorker worker)
        {
            var list = new List<string>();
            if (worker == null || !worker.CanClearAreas) return list;

            string current = AssignmentOf(worker)?.AreaId;

            foreach (string areaId in expansion.AreaIds)
            {
                if (areaId == current) continue;
                if (expansion.StateOf(areaId) == AreaState.Available) list.Add(areaId);
            }

            return list;
        }

        /// <summary>Everyone who can be sent out, in the order they joined.</summary>
        List<IFieldWorker> Candidates()
        {
            var list = new List<IFieldWorker>();
            for (int i = 0; i < workers.Count; i++)
                if (workers[i].CanClearAreas) list.Add(workers[i]);

            return list;
        }

        static bool AnyEnabled(FieldPanel panel)
        {
            for (int i = 0; i < panel.Options.Count; i++)
                if (panel.Options[i].Enabled) return true;

            return false;
        }

        // --- Plumbing -------------------------------------------------------------------------

        void HandleAreaStateChanged(string areaId, AreaState state)
        {
            if (state == AreaState.Available) return;

            ending.Clear();
            foreach (Assignment assignment in assignments.Values)
                if (assignment.AreaId == areaId) ending.Add(assignment);

            FieldEnd reason = state == AreaState.ReadyToExpand ? FieldEnd.Finished : FieldEnd.Unreachable;
            for (int i = 0; i < ending.Count; i++) End(ending[i], reason);
        }

        void End(Assignment assignment, FieldEnd reason)
        {
            DropTask(assignment);

            if (!assignments.Remove(assignment.Worker.WorkerId)) return;
            OnAssignmentEnded?.Invoke(assignment.Worker, assignment.AreaId, reason);
        }

        void DropTask(Assignment assignment)
        {
            CancelOwnJob(assignment);
            assignment.Zombie = null;
            assignment.Engaged = false;
        }

        /// <summary>Cancels the worker's job on their pile, keeping the work done - but only a
        /// job that is theirs. Somebody else on the same pile is left alone.</summary>
        void CancelOwnJob(Assignment assignment)
        {
            string debrisId = assignment.DebrisId;
            assignment.DebrisId = null;
            if (debrisId == null) return;

            Job job = clearing.JobOf(debrisId);
            string id = assignment.Worker.WorkerId;

            if (job != null && (job.OwnerId == id || job.ReservedFor == id)) clearing.CancelClear(debrisId);
        }

        IAreaZombie NearestZombie(string areaId, Vector3 from)
        {
            IAreaZombie found = null;
            float best = float.MaxValue;

            for (int i = 0; i < zombies.Count; i++)
            {
                IAreaZombie zombie = zombies[i];
                if (zombie.AreaId != areaId || !zombie.IsAlive) continue;

                float distance = FlatDistanceSquared(from, zombie.Position);
                if (distance >= best) continue;

                best = distance;
                found = zombie;
            }

            return found;
        }

        string NearestDebris(string areaId, Vector3 from)
        {
            scratch.Clear();
            clearing.CollectStanding(areaId, scratch);

            string found = null;
            float best = float.MaxValue;

            for (int i = 0; i < scratch.Count; i++)
            {
                float distance = FlatDistanceSquared(from, clearing.WorkPointOf(scratch[i]));
                if (distance >= best) continue;

                best = distance;
                found = scratch[i];
            }

            return found;
        }

        Assignment AssignmentOf(IFieldWorker worker) =>
            worker != null && assignments.TryGetValue(worker.WorkerId, out Assignment assignment) ? assignment : null;

        Assignment AssignmentOn(string areaId)
        {
            foreach (Assignment assignment in assignments.Values)
                if (assignment.AreaId == areaId) return assignment;

            return null;
        }

        Casualty Get(IFieldWorker worker)
        {
            if (worker == null) return null;

            for (int i = 0; i < casualties.Count; i++)
                if (casualties[i].Worker == worker) return casualties[i];

            return null;
        }

        string AreaName(string areaId)
        {
            string name = expansion.DefinitionOf(areaId)?.displayName;
            return string.IsNullOrEmpty(name) ? "area" : name;
        }

        bool AllowsWork() => machine == null || machine.CurrentPhase.AllowsBuilding();

        static string Plural(int count, string one, string many) => count == 1 ? one : many;

        /// <summary>"8s" under a minute, "1:05" from a minute up.</summary>
        static string FormatSeconds(float seconds)
        {
            int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return total >= 60 ? $"{total / 60}:{total % 60:00}" : $"{total}s";
        }

        static float FlatDistanceSquared(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return x * x + z * z;
        }
    }

    /// <summary>
    /// The progression flags field work is recorded under. In one place so the service that
    /// sets a flag and the one that reads it cannot spell it differently.
    /// </summary>
    public static class FieldIds
    {
        public static string ZombieKilled(string zombieId) => $"area_zombie_killed:{zombieId}";
    }
}
