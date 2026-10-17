using LastOrders.Jobs;
using LastOrders.Restaurant;
using UnityEngine;

namespace LastOrders.Crew
{
    /// <summary>
    /// What a crew member does when there is nobody to serve: keeps the diner nice. Takes the next
    /// chore off the <see cref="ChoreBoard"/> - a messy table first, then wiping down, sweeping,
    /// polishing the pass, dusting the decorations - picks up the cloth, broom or duster for it, walks
    /// over, does it, puts it away, and has a breather (a wipe of the brow after sweeping, a stretch)
    /// before the next. With nothing on the board at all, they wait by the pass.
    ///
    /// Any chore is done the same way, from what the chore says: the work loop and prop come from its
    /// <see cref="ChoreMotion"/>, the hands go to its work point. The least important duty, so the
    /// first customer whose dish is up takes them off it mid-wipe.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CrewBrain))]
    public sealed class ChoreRoutine : MonoBehaviour, ICrewDuty
    {
        enum Phase { Resting, Choosing, Walking, Working }

        [SerializeField] int priority = 10;

        [Tooltip("A share of walking speed between chores: unhurried.")]
        [SerializeField, Range(0.3f, 1f)] float pace = 0.72f;

        [Tooltip("Seconds of breather between one chore and the next.")]
        [SerializeField] Vector2 restSeconds = new Vector2(1.2f, 3.2f);

        [Tooltip("Seconds between looks at the board when there was nothing on it.")]
        [SerializeField] float askAgainSeconds = 2f;

        [Tooltip("Seconds to get to a chore before giving it up as out of reach.")]
        [SerializeField] float walkTimeout = 20f;

        [Header("Hands")]
        [Tooltip("Height of the wiping palm above the surface.")]
        [SerializeField] float palm = 0.035f;

        [Tooltip("Broom grips, metres up the handle: right hand, left hand.")]
        [SerializeField] Vector2 broomGrips = new Vector2(0.62f, 0.98f);

        CrewBrain brain;
        Waiter waiter;
        Phase phase;
        Chore chore;
        bool hasChore;
        float left, clock, nextAsk;
        Vector3 standBy;
        Quaternion standByTurn;
        bool standingBy;

        public int Priority => priority;

        /// <summary>Always: there is always a chore to do, or a breather to take.</summary>
        public bool HasWork => true;

        public string Activity => hasChore ? chore.Label : "Taking a breather";

        void Awake()
        {
            brain = GetComponent<CrewBrain>();
            waiter = GetComponent<Waiter>();
            standBy = transform.position;
            standByTurn = transform.rotation;
        }

        public void SetActive(bool active)
        {
            if (active)
            {
                phase = Phase.Resting;
                left = Random.Range(0.2f, 0.6f);
                return;
            }

            if (hasChore) ChoreBoard.Drop(chore, brain);
            hasChore = false;
            standingBy = false;
        }

        public void Tick(float dt)
        {
            switch (phase)
            {
                case Phase.Resting:
                    left -= dt;
                    if (standingBy) StandBy();
                    if (left <= 0f) phase = Phase.Choosing;
                    break;

                case Phase.Choosing:
                    Choose();
                    break;

                case Phase.Walking:
                    left -= dt;
                    if (!ChoreBoard.StillOn(chore, brain) || left <= 0f) Abandon();
                    else if (brain.Legs.Arrive())
                    {
                        // Out of reach without going through something: another chore, then.
                        if (brain.Legs.Stuck)
                        {
                            ChoreBoard.PutOff(chore, brain, Time.time);
                            Abandon();
                        }
                        else BeginWork();
                    }
                    break;

                case Phase.Working:
                    if (!ChoreBoard.StillOn(chore, brain))
                    {
                        Abandon();
                        break;
                    }

                    left -= dt;
                    clock += dt;
                    Hands();
                    if (left <= 0f) Finish();
                    break;
            }
        }

        // --- The round -------------------------------------------------------------------------------

        void Choose()
        {
            if (Time.time >= nextAsk && ChoreBoard.TryTake(brain, transform.position, Random.value, Time.time, out chore))
            {
                hasChore = true;
                standingBy = false;
                brain.Gestures.LetGo();
                brain.Props.Hold(PropFor(chore.Motion));
                brain.Legs.WalkTo(chore.Stand, chore.Facing, pace);
                left = walkTimeout;
                phase = Phase.Walking;
                return;
            }

            // Nothing wants doing: wait by the pass, and look again in a moment.
            nextAsk = Time.time + askAgainSeconds;
            if (!standingBy)
            {
                standingBy = true;
                Vector3 home = waiter != null ? waiter.Home : standBy;
                Quaternion turn = waiter != null ? waiter.HomeRotation : standByTurn;
                brain.Legs.WalkTo(home, turn * Vector3.forward, pace);
            }

            phase = Phase.Resting;
            left = askAgainSeconds;
        }

        void StandBy()
        {
            if (!brain.Legs.Arrive() || brain.Gestures.Busy || Random.value > Time.deltaTime * 0.25f) return;
            brain.Gestures.Play(Random.value < 0.5f ? Gesture.HandsOnHips : Gesture.ArmsCrossed, Random.Range(3f, 6f));
        }

        void BeginWork()
        {
            phase = Phase.Working;
            left = chore.Seconds;
            clock = 0f;
            brain.SetWorking(true, StyleFor(chore.Motion));
            if (brain.Look != null) brain.Look.Watch(chore.Work, 0.85f);
        }

        void Finish()
        {
            ChoreMotion motion = chore.Motion;
            ChoreBoard.Finish(chore, brain, Time.time);
            EndChore();

            phase = Phase.Resting;
            left = Random.Range(restSeconds.x, restSeconds.y);

            // A breather that fits what was just done.
            float roll = Random.value;
            if (motion == ChoreMotion.Sweep && roll < 0.35f) brain.Gestures.Play(Gesture.WipeBrow);
            else if (roll < 0.15f) brain.Gestures.Play(Gesture.Stretch);
            else if (roll < 0.3f) brain.Gestures.Play(Gesture.HandsOnHips, Random.Range(1.5f, 2.5f));

            if (brain.Gestures.Busy) left = Mathf.Max(left, brain.Gestures.Left);
        }

        void Abandon()
        {
            if (hasChore) ChoreBoard.Drop(chore, brain);
            EndChore();
            phase = Phase.Resting;
            left = 0.4f;
        }

        void EndChore()
        {
            hasChore = false;
            brain.SetWorking(false);
            brain.Props.Hold(CrewProp.None);
            if (brain.Reach != null) brain.Reach.Release();
            if (brain.Look != null) brain.Look.Release();
        }

        // --- The hands -------------------------------------------------------------------------------

        /// <summary>Where the hands go this frame for the chore's motion.</summary>
        void Hands()
        {
            CharacterReach reach = brain.Reach;
            if (reach == null) return;

            Vector3 facing = chore.Facing;
            Vector3 side = Vector3.Cross(Vector3.up, facing);
            Vector3 body = transform.position;

            switch (chore.Motion)
            {
                case ChoreMotion.Wipe:
                {
                    // Round and round, the circle itself drifting across the surface.
                    float turn = clock / WorkerGestures.WipeSeconds * Mathf.PI * 2f;
                    Vector3 drift = side * (chore.Reach * 0.7f * Mathf.Sin(clock * 0.9f)) + facing * (chore.Reach * 0.3f * Mathf.Sin(clock * 0.55f));
                    Vector3 circle = (side * Mathf.Cos(turn) + facing * Mathf.Sin(turn)) * (chore.Reach * 0.55f);
                    Vector3 hand = chore.Work + drift + circle + Vector3.up * palm;

                    reach.Hold(AvatarIKGoal.RightHand, hand, null, 1f, body + Vector3.up * 1.05f + side * 0.45f - facing * 0.1f);

                    // The other hand leant on the surface, out of the way.
                    Vector3 brace = chore.Work - side * 0.32f - facing * 0.12f + Vector3.up * palm;
                    reach.Hold(AvatarIKGoal.LeftHand, brace, null, 0.85f, body + Vector3.up * 1.05f - side * 0.45f - facing * 0.1f);
                    break;
                }

                case ChoreMotion.Dust:
                {
                    // Quick flicks side to side, a little up and down.
                    float flick = Mathf.Sin(clock / WorkerGestures.DustSeconds * Mathf.PI * 4f);
                    float bob = Mathf.Sin(clock * 3.1f);
                    Vector3 hand = chore.Work + side * (chore.Reach * flick) + Vector3.up * (0.06f * bob) - facing * 0.18f;
                    reach.Hold(AvatarIKGoal.RightHand, hand, null, 0.9f, body + Vector3.up * 1.0f + side * 0.5f);
                    reach.Release(AvatarIKGoal.LeftHand);
                    break;
                }

                case ChoreMotion.Sweep:
                {
                    // The head of the broom side to side over the floor in front; both hands on its handle.
                    float stroke = Mathf.Sin(clock / WorkerGestures.SweepSeconds * Mathf.PI * 2f);
                    Vector3 head = chore.Work + side * (chore.Reach * stroke);
                    head.y = body.y;

                    Vector3 chest = body + Vector3.up * 1.12f + facing * 0.12f + side * 0.08f;
                    Vector3 up = (chest - head).normalized;
                    brain.Props.PlaceBroom(head, head + up * 1.2f, facing);

                    reach.Hold(AvatarIKGoal.RightHand, brain.Props.BroomGrip(broomGrips.x), null, 1f, body + Vector3.up * 0.9f + side * 0.45f);
                    reach.Hold(AvatarIKGoal.LeftHand, brain.Props.BroomGrip(broomGrips.y), null, 1f, body + Vector3.up * 1.2f - side * 0.4f);
                    if (brain.Look != null) brain.Look.Watch(head, 0.7f);
                    break;
                }
            }
        }

        void LateUpdate()
        {
            if (!hasChore) return;

            // Wiping, the cloth lies flat on the surface under the palm, going round with it.
            if (chore.Motion == ChoreMotion.Wipe && phase == Phase.Working)
            {
                Vector3 palm = brain.Props.Palm;
                Quaternion turn = Quaternion.LookRotation(chore.Facing) * Quaternion.Euler(0f, 14f * Mathf.Sin(clock * 2.3f), 0f);
                brain.Props.LayCloth(new Vector3(palm.x, chore.Work.y + 0.014f, palm.z), turn);
                return;
            }

            // The broom is held while walking to the patch too: carried upright beside them.
            if (chore.Motion != ChoreMotion.Sweep || phase == Phase.Working || brain.Props.Broom == null) return;

            Vector3 side = Vector3.Cross(Vector3.up, transform.forward);
            Vector3 head = transform.position + side * 0.32f + transform.forward * 0.18f + Vector3.up * 0.05f;
            brain.Props.PlaceBroom(head, head + Vector3.up * 1.2f - transform.forward * 0.15f, transform.forward);
            if (brain.Reach != null) brain.Reach.Hold(AvatarIKGoal.RightHand, brain.Props.BroomGrip(0.85f), null, 0.9f);
        }

        static CrewProp PropFor(ChoreMotion motion)
        {
            switch (motion)
            {
                case ChoreMotion.Sweep: return CrewProp.Broom;
                case ChoreMotion.Dust: return CrewProp.Duster;
                default: return CrewProp.Cloth;
            }
        }

        static int StyleFor(ChoreMotion motion)
        {
            switch (motion)
            {
                case ChoreMotion.Sweep: return WorkerGestures.WorkSweep;
                case ChoreMotion.Dust: return WorkerGestures.WorkDust;
                default: return WorkerGestures.WorkWipe;
            }
        }
    }
}
