using System.Collections.Generic;
using FoodStreet.Visitors;
using LastOrders.Audio;
using LastOrders.Core;
using LastOrders.DayCycle;
using LastOrders.Expansion;
using LastOrders.Field;
using LastOrders.Housing;
using LastOrders.Interaction;
using LastOrders.Medical;
using LastOrders.Night;
using LastOrders.UI.World;
using UnityEngine;

namespace LastOrders.Jobs
{
    /// <summary>
    /// A survivor doing what the queue asks. Section 6: players choose roles and priorities,
    /// not movement destinations.
    ///
    /// The whole behaviour is claim, travel, work, complete. Movement is a straight walk rather
    /// than a path query, because the compound is open ground and the navigation layer is a
    /// separate piece of work - keeping it out of here means the job loop can be seen to work
    /// before pathfinding is wired, and the swap is confined to the walker.
    ///
    /// A job on the far side of a wall carries a route through the way out. The worker walks
    /// it on the way there and walks it backwards on the way home - but not between two jobs
    /// behind the same wall, where going back inside just to come out again reads as a bug.
    ///
    /// After dark nobody works, and nobody works while zombies are inside the walls either. The
    /// job goes back on the queue with its progress kept, and the worker walks to the nearest
    /// built <see cref="ShelterHut"/>, in through its door and out of sight, and stays there until
    /// morning or until the compound is clear; the guards in the towers are the only ones left
    /// outside. A level with no shelter built yet falls back to the diner's
    /// <see cref="NightShelter"/>.
    ///
    /// Sent to clear an area, a worker stops drawing from the queue and does what the
    /// <see cref="FieldWorkService"/> says instead: out through the wall, every zombie on the
    /// land, then the debris. Zombies hit back, and the health bar over their head shows it.
    /// At zero they go down where they stand, and restaurant staff - a worker with nothing in
    /// hand - walk out, pick them up and carry them to the hospital tent. Tapping a worker opens
    /// their info panel, which is where Recall Worker is.
    /// </summary>
    [DefaultExecutionOrder(1150)]
    public sealed class WorkerAgent : MonoBehaviour, IFieldWorker, ITappable, IInteractableObject
    {
        enum CarryStage
        {
            None,
            Going,
            PickingUp,
            Carrying,
        }

        [SerializeField] GameLifetimeScope scope;

        [Tooltip("Which queue this survivor draws from. 6 gives the Helper the clearing, " +
                 "building, farming and carrying work.")]
        [SerializeField] WorkerRole role = WorkerRole.Helper;

        [Tooltip("What the panels call them. Their role when left empty.")]
        [SerializeField] string displayName;

        [Tooltip("Metres per second. Slow enough that travel reads as travel.")]
        [SerializeField] float moveSpeed = 2.6f;

        [SerializeField] float turnSpeed = 540f;

        [Tooltip("How close counts as arrived. Below the agent radius of 18 and above the " +
                 "distance a frame of movement covers.")]
        [SerializeField] float arriveRadius = 0.55f;

        [Tooltip("Where this survivor waits when there is nothing to do. Set to their start " +
                 "position when left empty.")]
        [SerializeField] Transform idlePoint;

        [Header("Health")]
        [SerializeField] float maxHealth = 100f;

        [Tooltip("At or below this share of their health they are badly hurt: the bar pulses red " +
                 "and the player is told to recall them. High enough that two zombies still on them " +
                 "take several seconds to finish the job - time to tap them and recall.")]
        [Range(0.05f, 0.9f)] [SerializeField] float lowHealthFraction = 0.4f;

        [Tooltip("Health back per second while nothing is hitting them. A cot is what brings " +
                 "someone back from being downed; this only closes scrapes.")]
        [SerializeField] float restHealPerSecond = 2f;

        [Tooltip("Seconds after the last blow, given or taken, before resting starts.")]
        [SerializeField] float restDelay = 6f;

        [Tooltip("Seconds the health bar stays up after the last blow.")]
        [SerializeField] float healthBarLinger = 3f;

        [Header("Fighting")]
        [SerializeField] float attackDamage = 10f;

        [Tooltip("Seconds between blows. Close to the tool swing, so the hits land with the swings.")]
        [SerializeField] float attackInterval = 0.65f;

        [Tooltip("How close to a zombie they stand to hit it.")]
        [SerializeField] float attackRange = 1.25f;

        [Header("Carrying")]
        [Tooltip("Seconds spent lifting someone off the ground.")]
        [SerializeField] float pickUpSeconds = 0.8f;

        [Tooltip("Where a carried body's feet sit, relative to the carrier: right, up, forward. " +
                 "The body lies across their arms, head to the right.")]
        [SerializeField] Vector3 carryFeet = new Vector3(-0.85f, 1.05f, 0.45f);

        /// <summary>Optional. A controller with a work clip gets it; one without is left alone
        /// rather than logging a missing-parameter warning every frame.</summary>
        static readonly int WorkingParam = Animator.StringToHash("Working");

        /// <summary>How far above the ground a body lies, so it rests on the ground rather than
        /// half in it.</summary>
        const float LyingHeight = 0.15f;

        readonly WaypointWalker walker = new WaypointWalker { GridRadius = 0.3f };
        readonly List<Vector3> path = new List<Vector3>();
        readonly List<Vector3> returnPath = new List<Vector3>();

        GameComposition composition;
        FieldWorkService field;
        HospitalTent tent;
        Animator animator;
        bool hasWorkingParam;
        Job job;
        Vector3 home;
        string workerId;
        WorkerHealth health;

        /// <summary>The job in hand came from the field service - a pile on an assigned area -
        /// rather than from the ordinary queue.</summary>
        bool jobIsField;

        /// <summary>The route this worker last went out through, and so has to come back
        /// through. Null while they are on the home side of every wall.</summary>
        IReadOnlyList<Vector3> beyondRoute;

        bool returning;

        /// <summary>Points left on the path once its walk back in is done, or -1 when the path
        /// does not start with one.</summary>
        int pointsAfterReturn = -1;

        NightShelter shelter;

        /// <summary>Taking cover, or on the way in or back out.</summary>
        bool sheltering;

        bool inside;
        bool leavingShelter;
        Vector3 shelterSpot;

        /// <summary>The hut taking cover in. Null when it is the diner, or nowhere.</summary>
        ShelterHut coverHut;

        /// <summary>Seconds still to wait inside for a turn at the hut's door on the way out.</summary>
        float exitWait;

        /// <summary>Renderers switched off while out of sight in a hut, to switch back on after.</summary>
        readonly List<Renderer> hiddenRenderers = new List<Renderer>();

        readonly List<Collider> hiddenColliders = new List<Collider>();

        /// <summary>The way out being walked to reach an area, while it is walked.</summary>
        IReadOnlyList<Vector3> headingOut;

        bool inField;
        float attackCooldown;
        float lastCombat = float.NegativeInfinity;
        HealthBarView healthBar;

        /// <summary>Up out of a cot and walking back from the tent.</summary>
        bool walkingHome;

        WorkerAgent casualty;
        CarryStage carryStage;
        float pickUpLeft;
        IReadOnlyList<Vector3> carryRoute;

        public WorkerRole Role => role;
        public Job Current => job;

        /// <summary>Nothing to do this frame: no job, not out in the field, not taking cover,
        /// not carrying anyone. Walking home to wait counts. <see cref="WorkerLife"/> fills the time.</summary>
        public bool IsIdle => idleFrame >= Time.frameCount - 1;

        /// <summary>Where this survivor waits when there is nothing to do.</summary>
        public Vector3 HomePoint => idlePoint != null ? idlePoint.position : home;

        /// <summary>
        /// Optional, while idle: somewhere else to stand for now - a stroll, a chat with someone -
        /// walked to at <see cref="IdlePace"/> round the diner as home is. Null waits at home.
        /// </summary>
        public Vector3? IdleSpot { get; set; }

        /// <summary>A share of walking speed for getting to <see cref="IdleSpot"/>: a stroll, not a job.</summary>
        public float IdlePace { get; set; } = 1f;

        public float ArriveRadius => arriveRadius;

        /// <summary>True while standing at a job and doing it, or swinging at a zombie, as
        /// opposed to walking.</summary>
        public bool IsWorking { get; private set; }

        /// <summary>Inside a shelter. Nobody works outside after dark or while zombies are in: the
        /// guards in the towers are the only ones out there.</summary>
        public bool IsSheltering => sheltering && inside;

        /// <summary>After dark, or with zombies inside the walls: everybody but the guards goes in.</summary>
        bool MustTakeCover => composition.Machine.CurrentPhase.IsSiege() || composition.Compound.IsBreached;

        /// <summary>What the tool swing lands on while fighting: the zombie in front of them.
        /// Null otherwise, when a blow lands on whatever the job is for.</summary>
        public Transform StrikeTarget { get; private set; }

        public WorkerHealth Health => health;

        // --- IFieldWorker ------------------------------------------------------------------

        public string WorkerId => workerId;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? RoleName : displayName;

        public string RoleName
        {
            get
            {
                switch (role)
                {
                    case WorkerRole.MainCharacter: return "Main Character";
                    default: return role.ToString();
                }
            }
        }

        public Vector3 Position => transform.position;

        /// <summary>The Chef never leaves the kitchen; everyone else can be sent out.</summary>
        public bool CanClearAreas => role != WorkerRole.Chef;

        /// <summary>Restaurant staff carry the hurt in. The main character is out clearing.</summary>
        public bool CanCarry => role != WorkerRole.MainCharacter;

        bool IFieldWorker.IsSheltering => sheltering;

        public string Activity
        {
            get
            {
                if (walkingHome) return "Walking back from the tent";
                if (job != null && !job.IsFinished) return job.Description;
                return "Idle";
            }
        }

        void Awake()
        {
            home = transform.position;
            workerId = $"{role}_{GetInstanceID()}";

            health = new WorkerHealth(maxHealth, lowHealthFraction);
            health.OnDamaged += HandleDamaged;
            health.OnLow += HandleLowHealth;
            health.OnDowned += GoDown;

            // Optional: a capsule has nothing to animate, a character has a full controller.
            animator = GetComponentInChildren<Animator>();
            hasWorkingParam = HasBool(animator, WorkingParam);

            EnsureTapCollider();
        }

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                Debug.LogError($"[WorkerAgent] {name} found no GameLifetimeScope, so it will " +
                               "never be given work.", this);
                enabled = false;
                return;
            }

            shelter = FindFirstObjectByType<NightShelter>();
            tent = FindFirstObjectByType<HospitalTent>();

            // On duty: their role's work is theirs, not the main character's to cover.
            composition.Jobs.ReportOnDuty(role, workerId, true);

            field = composition.Field;
            field.Register(this);
            field.OnCotReady += HandleCotReady;
            field.OnRecovered += HandleRecovered;
            field.OnAssignmentEnded += HandleAssignmentEnded;
        }

        void OnDestroy()
        {
            if (field == null) return;

            field.OnCotReady -= HandleCotReady;
            field.OnRecovered -= HandleRecovered;
            field.OnAssignmentEnded -= HandleAssignmentEnded;
            field.Unregister(this);
        }

        void OnEnable()
        {
            composition?.Jobs?.ReportOnDuty(role, workerId, true);
        }

        void OnDisable()
        {
            // 6: a phase transition returns unused reservations rather than losing the work.
            composition?.Jobs?.ReleaseAllFor(workerId);
            composition?.Jobs?.ReportOnDuty(role, workerId, false);
            job = null;
            jobIsField = false;

            // beyondRoute is kept: where the worker is standing has not changed, and forgetting
            // that they are outside is how they walk home through a wall.
            returning = false;
            pointsAfterReturn = -1;
            walker.Clear();
            SetWorking(false);
            StrikeTarget = null;
            headingOut = null;

            if (shelter != null) shelter.Release(this);
            sheltering = false;
            inside = false;
            leavingShelter = false;
            coverHut = null;
            exitWait = 0f;
            ShowBody();

            // Not while the scene is coming down: the pool would re-parent the bar under a root
            // that is being destroyed in the same pass, and whoever is carried may already be gone.
            if (!gameObject.scene.isLoaded) return;

            if (casualty != null) PutDown();
            ReleaseHealthBar();
        }

        void Update()
        {
            if (composition == null) return;

            // Down, carried or in a cot: the body is posed where it lies, or by whoever holds it.
            if (field.IsCasualty(this))
            {
                AsCasualty();
                return;
            }

            Rest();
            DrawHealthBar();

            Vector3 before = transform.position;

            // Dusk and night, or zombies inside the walls: everybody but the guards goes in.
            // Checked every frame rather than on the events, so a worker that wakes up mid-siege
            // still goes in.
            if (MustTakeCover)
            {
                StopFieldWork();
                Shelter(before);
                return;
            }

            if (sheltering && LeaveShelter(before)) return;

            if (walkingHome && !WalkHome(before)) return;

            if (casualty != null)
            {
                Carry(before);
                return;
            }

            if (field.IsOnActiveAssignment(this))
            {
                Field(before);
                return;
            }

            LeaveField();

            // Staff pick up the hurt before taking anything new off the queue - but they finish
            // what is in their hands first: a table half wiped is seconds from done.
            if ((job == null || job.IsFinished) && field.TryTakeRescue(this, out IFieldWorker hurt))
            {
                if (hurt is WorkerAgent body)
                {
                    BeginCarry(body);
                    Carry(before);
                    return;
                }

                field.DropRescue(this);
            }

            Work(before);
        }

        // --- The night -------------------------------------------------------------------------

        void Shelter(Vector3 before)
        {
            if (!sheltering || leavingShelter) BeginShelter();

            if (!inside && FollowPath())
            {
                inside = true;

                // Whatever wall they came back in through, they are on the home side of it now.
                beyondRoute = null;

                // In through the hut's door and out of sight until they come back out.
                if (coverHut != null) HideBody();
            }

            Animate(before);
        }

        /// <summary>
        /// Puts the tools down and heads for cover: the nearest built shelter, or the diner in a
        /// level with none built yet.
        ///
        /// The job goes back on the queue with its progress kept - section 6's phase rule - so
        /// work picks up where it left off. A worker outside a wall comes back in through it
        /// first, then round the diner to the shelter's door.
        /// </summary>
        void BeginShelter()
        {
            bool wasLeaving = leavingShelter;

            sheltering = true;
            leavingShelter = false;
            walkingHome = false;

            composition.Jobs.ReleaseAllFor(workerId);
            job = null;
            jobIsField = false;
            SetWorking(false);

            if (shelter == null) shelter = FindFirstObjectByType<NightShelter>();

            // Still waiting for a turn at the door when the danger came back: they never left.
            bool neverLeft = wasLeaving && exitWait > 0f;
            exitWait = 0f;

            if (neverLeft)
            {
                walker.Clear();
                inside = true;
                return;
            }

            // Turned round on the way out - the danger came back. Straight back in.
            if (wasLeaving && (coverHut != null ? coverHut.IsBuilt : shelter != null))
            {
                path.Clear();

                if (coverHut != null)
                {
                    path.Add(coverHut.DoorOutside);
                    path.Add(coverHut.DoorInside);
                }
                else
                {
                    path.Add(shelter.DoorOutside);
                    path.Add(shelter.DoorInside);
                    path.Add(shelterSpot);
                }

                inside = false;
                walker.Follow(path);
                return;
            }

            path.Clear();
            if (beyondRoute != null) AppendReturn();

            Vector3 from = path.Count > 0 ? path[path.Count - 1] : transform.position;

            coverHut = ShelterHut.NearestBuilt(from);

            if (coverHut != null)
            {
                AppendDetour(from, coverHut.DoorOutside);
                path.Add(coverHut.DoorOutside);
                path.Add(coverHut.DoorInside);
            }
            else if (shelter != null)
            {
                shelterSpot = shelter.Claim(this);
                path.AddRange(shelter.RouteIn(from, shelterSpot));
            }
            else
            {
                // A level with no shelter at all: home is the safest place there is.
                path.Add(idlePoint != null ? idlePoint.position : home);
            }

            returning = false;
            pointsAfterReturn = -1;
            inside = false;
            walker.Follow(path);
        }

        /// <summary>Walks back out once it is safe. True until they are out and ready for work.</summary>
        bool LeaveShelter(Vector3 before)
        {
            if (!leavingShelter)
            {
                leavingShelter = true;

                Vector3 to = idlePoint != null ? idlePoint.position : home;

                if (coverHut != null && inside)
                {
                    // Out of sight until it is their turn at the door.
                    exitWait = coverHut.ReserveDoorway();

                    path.Clear();
                    path.Add(coverHut.DoorInside);
                    path.Add(coverHut.DoorOutside);
                    AppendDetour(coverHut.DoorOutside, to);
                    path.Add(to);
                    walker.Follow(path);
                }
                else if (coverHut == null && shelter != null && inside)
                {
                    walker.Follow(shelter.RouteOut(shelterSpot, to));
                }
                else
                {
                    // It was safe again before they got in, so they are still outside, and home
                    // is a straight walk round the diner.
                    path.Clear();
                    AppendDetour(transform.position, to);
                    path.Add(to);
                    walker.Follow(path);
                }

                if (shelter != null) shelter.Release(this);
            }

            if (exitWait > 0f)
            {
                exitWait -= Time.deltaTime;
                if (exitWait > 0f) return true;
            }

            ShowBody();

            bool arrived = FollowPath();
            Animate(before);

            if (!arrived) return true;

            sheltering = false;
            inside = false;
            leavingShelter = false;
            coverHut = null;
            return true;
        }

        /// <summary>Out of sight inside a hut: nothing drawn, nothing to tap.</summary>
        void HideBody()
        {
            if (hiddenRenderers.Count > 0 || hiddenColliders.Count > 0) return;

            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;

                r.enabled = false;
                hiddenRenderers.Add(r);
            }

            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                if (!c.enabled) continue;

                c.enabled = false;
                hiddenColliders.Add(c);
            }

            ReleaseHealthBar();
        }

        /// <summary>Back in sight: only what <see cref="HideBody"/> switched off comes back on, so a
        /// tool put away meanwhile stays put away.</summary>
        void ShowBody()
        {
            for (int i = 0; i < hiddenRenderers.Count; i++)
                if (hiddenRenderers[i] != null) hiddenRenderers[i].enabled = true;

            for (int i = 0; i < hiddenColliders.Count; i++)
                if (hiddenColliders[i] != null) hiddenColliders[i].enabled = true;

            hiddenRenderers.Clear();
            hiddenColliders.Clear();
        }

        /// <summary>
        /// Drives the walk cycle from the distance actually covered this frame, so a worker
        /// standing at a station is not still playing a walk - which is the artefact that makes
        /// staff read as sliding rather than walking.
        /// </summary>
        void Animate(Vector3 before)
        {
            if (animator == null) return;

            // The legs keep pace with the ground covered: idle, walk or jog by real speed.
            Vector3 moved = transform.position - before;
            moved.y = 0f;
            float speed = Time.deltaTime > 0f ? Mathf.Min(moved.magnitude / Time.deltaTime, moveSpeed * 1.5f) : 0f;
            CharacterLocomotion.Drive(animator, speed, Time.deltaTime);
        }

        // --- The queue ---------------------------------------------------------------------------

        void Work(Vector3 before)
        {
            if (job == null || job.IsFinished)
            {
                job = null;
                jobIsField = false;

                if (composition.Jobs.TryClaim(role, workerId, out Job claimed))
                {
                    job = claimed;
                    job.BeginTravel();
                    PlanPathTo(job);
                }
                else
                {
                    SetWorking(false);
                    Idle();
                    Animate(before);
                    return;
                }
            }

            Progress();
            Animate(before);
        }

        /// <summary>Walks to the job in hand and does it. Shared by the queue and the field.</summary>
        void Progress()
        {
            switch (job.State)
            {
                case JobState.Reserved:
                case JobState.Travelling:
                    SetWorking(false);
                    bool arrived = FollowPath();

                    // Back in through the old way out: on the home side until the next route.
                    if (pointsAfterReturn >= 0 && walker.Remaining <= pointsAfterReturn)
                    {
                        beyondRoute = null;
                        pointsAfterReturn = -1;
                    }

                    // Through the way out, with only the spot to stand on left: from here the
                    // way home is back through the same gap.
                    if (job.Route != null && walker.Remaining <= 1) beyondRoute = job.Route;

                    if (arrived) job.BeginWork();
                    break;

                case JobState.Working:
                    SetWorking(true);
                    if (job.Target != null) Face(job.Target.position - transform.position);
                    Labour();
                    break;

                default:
                    // Blocked or cancelled by something else: drop it and look for other work.
                    SetWorking(false);
                    job = null;
                    jobIsField = false;
                    break;
            }
        }

        void Labour()
        {
            job.WorkRemaining -= Time.deltaTime;

            if (job.WorkRemaining > 0f) return;

            job.Complete();
            job = null;
            jobIsField = false;
            SetWorking(false);
        }

        /// <summary>
        /// Lays out the walk to a job: back in through the last way out if this job is not
        /// behind the same wall, out through its own route if it has one, then the spot to
        /// stand on.
        /// </summary>
        void PlanPathTo(Job target)
        {
            path.Clear();
            pointsAfterReturn = -1;

            bool sameSide = beyondRoute != null && ReferenceEquals(beyondRoute, target.Route);
            bool comingBack = beyondRoute != null && !sameSide;

            if (comingBack) AppendReturn();

            int tailStart = path.Count;

            // Round the diner rather than through it, to the way out or straight to the job: a
            // repair on the far side of the compound is as far as the restaurant is wide.
            bool walksRoute = target.Route != null && !sameSide;
            Vector3 from = path.Count > 0 ? path[path.Count - 1] : transform.position;
            AppendDetour(from, walksRoute ? target.Route[0] : target.InteractionPoint);

            if (walksRoute)
            {
                for (int i = 0; i < target.Route.Count; i++) path.Add(target.Route[i]);
            }

            path.Add(target.InteractionPoint);

            if (comingBack) pointsAfterReturn = path.Count - tailStart;

            // beyondRoute is set once the route has actually been walked, not here: a job
            // released before the gap would otherwise leave the worker inside believing they
            // were out, and the next job behind the same wall would walk them through it.
            returning = false;

            walker.Follow(path);
        }

        /// <summary>
        /// Adds the way back in to the path. Only the part not yet walked when the worker was
        /// already on their way home, so a job arriving mid-return does not send them back to
        /// the outer end of the gap first.
        /// </summary>
        void AppendReturn()
        {
            if (!returning) BuildReturnPath();

            int start = returning ? Mathf.Max(0, returnPath.Count - walker.Remaining) : 0;
            for (int i = start; i < returnPath.Count; i++) path.Add(returnPath[i]);
        }

        void BuildReturnPath()
        {
            returnPath.Clear();
            for (int i = beyondRoute.Count - 1; i >= 0; i--) returnPath.Add(beyondRoute[i]);
        }

        /// <summary>Walks the current path. Returns true once its last point is reached.</summary>
        bool FollowPath()
        {
            if (walker.Arrived) return true;
            return walker.Advance(transform, moveSpeed, turnSpeed, arriveRadius, Time.deltaTime);
        }

        /// <summary>Walks toward a point until within <paramref name="stopAt"/>, at <paramref name="pace"/>
        /// of walking speed. Returns true on arrival.</summary>
        bool TravelTowards(Vector3 target, float stopAt, float pace = 1f)
        {
            Vector3 flat = new Vector3(target.x, transform.position.y, target.z);
            Vector3 delta = flat - transform.position;
            float distance = delta.magnitude;

            if (distance <= stopAt) return true;

            Vector3 step = delta / distance * moveSpeed * pace * Time.deltaTime;
            transform.position += Vector3.ClampMagnitude(step, distance - stopAt);

            Face(delta);
            return false;
        }

        int idleFrame = -10;

        /// <summary>Walks them home, or to a stroll or a chat, round whatever stands in the way: a walker of its
        /// own, so the work queue's path is left as it is.</summary>
        readonly WaypointWalker idleWalker = new WaypointWalker { GridRadius = 0.3f };

        Vector3 idleGoal;
        bool idleRouted;

        void Idle()
        {
            bool resumed = idleFrame < Time.frameCount - 1;
            idleFrame = Time.frameCount;

            // Outside a wall with nothing left to do there: back in the way they came first.
            if (beyondRoute != null)
            {
                if (!returning)
                {
                    BuildReturnPath();
                    walker.Follow(returnPath);
                    returning = true;
                }

                if (!FollowPath()) return;

                beyondRoute = null;
                returning = false;
            }

            Vector3 target = IdleSpot ?? (idlePoint != null ? idlePoint.position : home);
            float pace = IdleSpot.HasValue ? Mathf.Clamp(IdlePace, 0.2f, 1f) : 1f;

            // Round the diner, and round anything on the way. Routed again whenever where they are
            // headed moves, or they come back to idling from something else.
            var goal = new Vector3(target.x, transform.position.y, target.z);
            if (resumed || !idleRouted || (goal - idleGoal).sqrMagnitude > 0.04f)
            {
                idleGoal = goal;
                idleRouted = true;

                idleDetour.Clear();
                if (shelter == null) shelter = FindFirstObjectByType<NightShelter>();
                if (shelter != null) shelter.AppendDetour(transform.position, target, idleDetour);
                idleDetour.Add(target);
                idleWalker.Follow(idleDetour);
            }

            if (!idleWalker.Arrived) idleWalker.Advance(transform, moveSpeed * pace, turnSpeed, arriveRadius, Time.deltaTime);
        }

        readonly List<Vector3> idleDetour = new List<Vector3>(2);

        /// <summary>
        /// Moves where this worker waits out of <paramref name="area"/> - the restaurant has grown
        /// over it - to just past its nearest edge, <paramref name="clearance"/> metres out.
        /// </summary>
        public void KeepHomeOutOf(Rect area, float clearance)
        {
            Rect wide = Rect.MinMaxRect(area.xMin - clearance, area.yMin - clearance,
                                        area.xMax + clearance, area.yMax + clearance);

            Vector3 spot = idlePoint != null ? idlePoint.position : home;
            if (!wide.Contains(new Vector2(spot.x, spot.z))) return;

            float west = spot.x - wide.xMin;
            float east = wide.xMax - spot.x;
            float south = spot.z - wide.yMin;
            float north = wide.yMax - spot.z;
            float nearest = Mathf.Min(Mathf.Min(west, east), Mathf.Min(south, north));

            if (nearest == west) spot.x = wide.xMin;
            else if (nearest == east) spot.x = wide.xMax;
            else if (nearest == south) spot.z = wide.yMin;
            else spot.z = wide.yMax;

            if (idlePoint != null) idlePoint.position = spot;
            else home = spot;
        }

        // --- Out in the field --------------------------------------------------------------------

        /// <summary>Assigned to an area: fight whatever is on it, then clear it.</summary>
        void Field(Vector3 before)
        {
            // Whatever the ordinary queue had given them goes back on it, progress kept.
            if (job != null && !jobIsField)
            {
                job.Release();
                job = null;
            }

            inField = true;

            FieldTask task = field.NextTask(this);

            switch (task.Kind)
            {
                case FieldTaskKind.Fight:
                    Fight(task);
                    break;

                case FieldTaskKind.Clear:
                    StrikeTarget = null;
                    ClearDebris(task);
                    break;

                default:
                    SetWorking(false);
                    StrikeTarget = null;
                    Idle();
                    break;
            }

            Animate(before);
        }

        void Fight(FieldTask task)
        {
            // Any pile they were on was put down by the service, its progress kept.
            if (jobIsField)
            {
                job = null;
                jobIsField = false;
            }

            IAreaZombie zombie = task.Zombie;

            if (!HeadOut(task.Route))
            {
                SetWorking(false);
                StrikeTarget = null;
                return;
            }

            if (!TravelTowards(zombie.Position, attackRange))
            {
                SetWorking(false);
                StrikeTarget = null;
                return;
            }

            Face(zombie.Position - transform.position);
            SetWorking(true);
            StrikeTarget = zombie is Component body ? body.transform : null;
            lastCombat = Time.time;

            attackCooldown -= Time.deltaTime;
            if (attackCooldown > 0f) return;

            attackCooldown = attackInterval;
            field.StrikeZombie(this, zombie, attackDamage);
        }

        void ClearDebris(FieldTask task)
        {
            string target = DebrisClearingService.JobPrefix + task.DebrisId;

            if (job == null || job.IsFinished || !jobIsField || job.TargetId != target)
            {
                job = field.ClaimDebrisJob(this, task.DebrisId, out _);
                jobIsField = job != null;
                headingOut = null;

                if (job == null)
                {
                    SetWorking(false);
                    Idle();
                    return;
                }

                job.BeginTravel();
                PlanPathTo(job);
            }

            Progress();
        }

        /// <summary>
        /// Walks out through the way to an area. True once through it - straight away when they
        /// are already on that side of the wall, or the area needs no route.
        /// </summary>
        bool HeadOut(IReadOnlyList<Vector3> route)
        {
            if (route == null || ReferenceEquals(beyondRoute, route))
            {
                headingOut = null;
                return true;
            }

            if (!ReferenceEquals(headingOut, route))
            {
                headingOut = route;

                path.Clear();
                pointsAfterReturn = -1;

                if (beyondRoute != null) AppendReturn();
                for (int i = 0; i < route.Count; i++) path.Add(route[i]);

                returning = false;
                walker.Follow(path);
            }

            if (!FollowPath()) return false;

            beyondRoute = route;
            headingOut = null;
            return true;
        }

        /// <summary>Called on the way out of field mode: nothing left pointing at the area.</summary>
        void LeaveField()
        {
            if (!inField) return;

            inField = false;
            headingOut = null;
            StrikeTarget = null;
            SetWorking(false);

            if (jobIsField)
            {
                job = null;
                jobIsField = false;
            }
        }

        /// <summary>The night came, or the worker was stopped: tools down, keeping the work.</summary>
        void StopFieldWork()
        {
            if (inField || jobIsField) field.DropTask(this);
            LeaveField();

            walkingHome = false;
            if (casualty != null) PutDown();
        }

        // --- Hurt ----------------------------------------------------------------------------

        void HandleDamaged(float amount)
        {
            lastCombat = Time.time;
            SoundController.Play(SoundId.WorkerHurt, transform.position);

            if (composition != null)
                composition.WorldUi.ShowWarning(transform.position + Vector3.up * 2f, $"-{Mathf.CeilToInt(amount)}");
        }

        /// <summary>The warning: once, as they drop past the line, while there is still time to
        /// call them back.</summary>
        void HandleLowHealth()
        {
            if (composition == null) return;

            composition.WorldUi.ShowWarning(transform.position + Vector3.up * 2.6f, "Badly hurt!");
            TapFeedback.Report($"{DisplayName} is badly hurt! Tap them and Recall Worker before they go down.");
        }

        /// <summary>Out of health: down where they stand, face up, until somebody comes.</summary>
        void GoDown()
        {
            walker.Clear();
            job = null;
            jobIsField = false;
            headingOut = null;
            inField = false;
            walkingHome = false;
            StrikeTarget = null;
            SetWorking(false);
            ReleaseHealthBar();

            if (casualty != null) PutDown();
            CharacterLocomotion.Set(animator, 0f);

            // Knocked backwards: the head goes the way their back was facing.
            LieAt(transform.position, -transform.forward);

            if (composition == null) return;

            string tentName = tent != null ? tent.DisplayName : "hospital tent";
            composition.WorldUi.ShowWarning(transform.position + Vector3.up * 1.2f, "Downed!");
            TapFeedback.Report($"{DisplayName} is down! The staff will carry them to the {tentName}. " +
                               "Their clearing is paused and nothing is lost.");
        }

        void AsCasualty()
        {
            if (field.StageOf(this) != CasualtyStage.AwaitingPickup || field.RescuerOf(this) != null) return;

            // Nobody is out after dark, and with nobody at all to come for them the wait would
            // never end: either way they are carried in while nobody is watching, the way the
            // visitors are after a night.
            if (composition.Machine.CurrentPhase.IsSiege() || !AnyoneCouldCarry()) ArriveAtTent();
        }

        bool AnyoneCouldCarry()
        {
            IReadOnlyList<IFieldWorker> all = field.Workers;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != (IFieldWorker)this && all[i].CanCarry && !field.IsCasualty(all[i])) return true;

            return false;
        }

        /// <summary>Delivered to the tent: into the cot held for them, or beside it until one frees.</summary>
        void ArriveAtTent()
        {
            HospitalCot cot = field.Delivered(this);
            LieInTent(cot);
        }

        void LieInTent(HospitalCot cot)
        {
            if (tent == null)
            {
                LieAt(idlePoint != null ? idlePoint.position : home, transform.forward);
                return;
            }

            Pose pose = cot != null ? tent.CotPose(cot.Index) : tent.WaitingPose(field.WaitingIndexOf(this));
            transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        void HandleCotReady(IFieldWorker worker, HospitalCot cot)
        {
            if (worker == (IFieldWorker)this) LieInTent(cot);
        }

        /// <summary>Well again: up at the tent's way in, and back round the diner to where they wait.</summary>
        void HandleRecovered(IFieldWorker worker)
        {
            if (worker != (IFieldWorker)this) return;

            Vector3 start = tent != null ? tent.Entrance : transform.position;
            start.y = home.y;

            Vector3 to = idlePoint != null ? idlePoint.position : home;
            Vector3 facing = to - start;
            facing.y = 0f;

            transform.SetPositionAndRotation(start, facing.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(facing) : Quaternion.identity);

            WalkHomeFrom(start);
        }

        /// <summary>From the tent, which is inside the walls, back round the diner to where they wait.</summary>
        void WalkHomeFrom(Vector3 start)
        {
            Vector3 to = idlePoint != null ? idlePoint.position : home;

            beyondRoute = null;
            returning = false;
            pointsAfterReturn = -1;

            path.Clear();
            AppendDetour(start, to);
            path.Add(to);
            walker.Follow(path);
            walkingHome = true;
        }

        /// <summary>Walks back from the tent. True once home.</summary>
        bool WalkHome(Vector3 before)
        {
            bool arrived = FollowPath();
            Animate(before);

            if (arrived) walkingHome = false;
            return arrived;
        }

        void HandleAssignmentEnded(IFieldWorker worker, string areaId, FieldEnd reason)
        {
            if (worker != (IFieldWorker)this || composition == null) return;

            if (reason == FieldEnd.Finished)
            {
                string area = composition.Expansion.DefinitionOf(areaId)?.displayName ?? "area";
                TapFeedback.Report($"{DisplayName} finished clearing the {area}.");
            }
        }

        /// <summary>Health comes back slowly once nothing has been hit, either way, for a while.</summary>
        void Rest()
        {
            if (health.IsFull || Time.time - lastCombat < restDelay) return;

            health.Heal(restHealPerSecond * Time.deltaTime);
        }

        /// <summary>The bar over their head: up in a fight and a little after, and all the
        /// while they are badly hurt out there.</summary>
        void DrawHealthBar()
        {
            // Nobody can see them inside a hut, so nor can their bar be seen.
            bool hidden = hiddenRenderers.Count > 0;

            bool show = !health.IsDowned && !hidden &&
                        (Time.time - lastCombat < healthBarLinger || health.IsLow && field.IsExposed(this));

            if (!show)
            {
                ReleaseHealthBar();
                return;
            }

            if (healthBar == null || !healthBar.IsLive || healthBar.Anchor != transform)
                healthBar = composition.WorldUi.ShowHealthBar(transform);

            if (healthBar != null) healthBar.Set(health.Fraction, health.Current, health.Max, health.IsLow);
        }

        void ReleaseHealthBar()
        {
            if (healthBar == null) return;

            if (healthBar.IsLive && healthBar.Anchor == transform) healthBar.Release();
            healthBar = null;
        }

        /// <summary>
        /// Lies flat at a point, face up, head toward <paramref name="headward"/>. The project's
        /// characters have their pivot at the feet, so this turns about the feet.
        /// </summary>
        void LieAt(Vector3 at, Vector3 headward)
        {
            headward.y = 0f;
            if (headward.sqrMagnitude < 1e-4f) headward = Vector3.forward;

            // Face to the sky, and the body's own up - the way the head points - along the ground.
            transform.SetPositionAndRotation(new Vector3(at.x, home.y + LyingHeight, at.z),
                                             Quaternion.LookRotation(Vector3.up, headward.normalized));
        }

        // --- Carrying ------------------------------------------------------------------------

        void BeginCarry(WorkerAgent hurt)
        {
            casualty = hurt;
            carryStage = CarryStage.Going;
            carryRoute = field.RouteToCasualty(hurt);

            path.Clear();
            pointsAfterReturn = -1;

            if (beyondRoute != null && !ReferenceEquals(beyondRoute, carryRoute)) AppendReturn();

            Vector3 from = path.Count > 0 ? path[path.Count - 1] : transform.position;
            Vector3 to = hurt.transform.position;

            if (carryRoute != null && !ReferenceEquals(beyondRoute, carryRoute))
            {
                AppendDetour(from, carryRoute[0]);
                for (int i = 0; i < carryRoute.Count; i++) path.Add(carryRoute[i]);
            }
            else if (beyondRoute == null)
            {
                AppendDetour(from, to);
            }

            path.Add(to);

            returning = false;
            walker.Follow(path);

            TapFeedback.Report($"{DisplayName} is going to carry {hurt.DisplayName} to the hospital tent.");
        }

        void Carry(Vector3 before)
        {
            // The casualty got up, or was taken off them: nothing left to carry.
            if (field.RescuerOf(casualty) != (IFieldWorker)this)
            {
                EndCarry();
                Animate(before);
                return;
            }

            switch (carryStage)
            {
                case CarryStage.Going:
                    SetWorking(false);

                    if (carryRoute != null && walker.Remaining <= 1) beyondRoute = carryRoute;

                    if (FollowPath())
                    {
                        carryStage = CarryStage.PickingUp;
                        pickUpLeft = pickUpSeconds;
                    }
                    break;

                case CarryStage.PickingUp:
                    Face(casualty.transform.position - transform.position);

                    pickUpLeft -= Time.deltaTime;
                    if (pickUpLeft > 0f) break;

                    field.PickedUp(casualty);
                    carryStage = CarryStage.Carrying;
                    PlanCarryToTent();
                    PoseCarried();
                    break;

                case CarryStage.Carrying:
                    bool arrived = FollowPath();

                    if (pointsAfterReturn >= 0 && walker.Remaining <= pointsAfterReturn)
                    {
                        beyondRoute = null;
                        pointsAfterReturn = -1;
                    }

                    PoseCarried();

                    if (arrived)
                    {
                        WorkerAgent delivered = casualty;
                        EndCarry();
                        delivered.ArriveAtTent();
                        WalkHomeFrom(transform.position);
                    }
                    break;
            }

            Animate(before);
        }

        /// <summary>Back in through the way out if they came through one, round the diner, to the tent.</summary>
        void PlanCarryToTent()
        {
            path.Clear();
            pointsAfterReturn = -1;

            int returnLegs = 0;
            if (beyondRoute != null)
            {
                for (int i = beyondRoute.Count - 1; i >= 0; i--) path.Add(beyondRoute[i]);
                returnLegs = path.Count;
            }

            Vector3 from = path.Count > 0 ? path[path.Count - 1] : transform.position;
            Vector3 door = tent != null ? tent.Entrance : (idlePoint != null ? idlePoint.position : home);

            AppendDetour(from, door);
            path.Add(door);

            if (returnLegs > 0) pointsAfterReturn = path.Count - returnLegs;

            returning = false;
            walker.Follow(path);
        }

        /// <summary>The casualty across their arms, face up, head to the carrier's right.</summary>
        void PoseCarried()
        {
            Vector3 feet = transform.position + transform.right * carryFeet.x + Vector3.up * carryFeet.y +
                           transform.forward * carryFeet.z;

            casualty.transform.SetPositionAndRotation(feet, Quaternion.LookRotation(Vector3.up, transform.right));
        }

        /// <summary>Stopped mid-rescue: whoever they held is laid down in front of them for the
        /// next person to come for.</summary>
        void PutDown()
        {
            WorkerAgent held = casualty;
            bool carrying = carryStage == CarryStage.Carrying;

            EndCarry();

            if (held != null && carrying) held.LieAt(transform.position + transform.forward * 0.8f, transform.right);
        }

        void EndCarry()
        {
            if (casualty != null && field.RescuerOf(casualty) == (IFieldWorker)this) field.DropRescue(this);

            casualty = null;
            carryStage = CarryStage.None;
            carryRoute = null;
            walker.Clear();
        }

        /// <summary>Adds the corners round the diner a straight walk between two points would
        /// otherwise cut through.</summary>
        void AppendDetour(Vector3 from, Vector3 to)
        {
            if (shelter == null) shelter = FindFirstObjectByType<NightShelter>();
            if (shelter != null) shelter.AppendDetour(from, to, path);
        }

        // --- Tapping -------------------------------------------------------------------------

        public bool OnTouch() => TapFeedback.Report(OnTapped());

        public string OnTapped()
        {
            if (composition == null) return $"{DisplayName} is not connected to the game.";

            return FieldPanels.OpenWorker(composition, this) ? null : field.StatusOf(this);
        }

        /// <summary>
        /// Something for a tap to land on. A trigger, so nothing bumps into it; the characters
        /// the setup tools make have no collider of their own.
        /// </summary>
        void EnsureTapCollider()
        {
            if (GetComponentInChildren<Collider>() != null) return;

            var capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.4f;
        }

        // --- Plumbing ------------------------------------------------------------------------

        void SetWorking(bool working)
        {
            if (IsWorking == working) return;

            IsWorking = working;
            if (hasWorkingParam) animator.SetBool(WorkingParam, working);
        }

        static bool HasBool(Animator target, int hash)
        {
            if (target == null || target.runtimeAnimatorController == null) return false;

            foreach (AnimatorControllerParameter parameter in target.parameters)
            {
                if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Bool)
                    return true;
            }

            return false;
        }

        void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) return;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
        }

        void OnDrawGizmosSelected()
        {
            if (job == null) return;

            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.9f);
            Gizmos.DrawLine(transform.position, job.InteractionPoint);
            Gizmos.DrawWireSphere(job.InteractionPoint, arriveRadius);
        }
    }
}
