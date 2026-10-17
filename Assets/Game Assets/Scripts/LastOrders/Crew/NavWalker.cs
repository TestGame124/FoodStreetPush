using FoodStreet.Navigation;
using LastOrders.Jobs;
using UnityEngine;

namespace LastOrders.Crew
{
    /// <summary>
    /// Legs for the crew: walks the nav grid round the tables and counters to a spot, turns to face
    /// the way asked, and drives the animator's walk from the speed actually covered.
    ///
    /// The nav grid stops a walker at the edge of the cells a table stands on, a stride short of a
    /// chair's side: that last stride is stepped straight in - only across open floor or the very
    /// furniture being stepped up to, never through a wall or anything else (<see cref="GridRoute"/>)
    /// - and remembered so the next walk steps back out the same way first. A spot that cannot be
    /// reached that way is asked for again, and then given up on (<see cref="Stuck"/>), never walked
    /// to straight through whatever is in the way.
    ///
    /// Every duty walks through this, so serving, sweeping and going to help in the kitchen all move
    /// alike.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NavWalker : MonoBehaviour
    {
        /// <summary>The furthest a walker stopped short is stepped the rest of the way, ignoring the grid.</summary>
        const float MaxStepIn = 1.6f;

        /// <summary>Close enough to the spot walked to.</summary>
        const float ArriveRadius = 0.12f;

        [SerializeField] Animator animator;

        [SerializeField] float walkSpeed = 2.2f;
        [SerializeField] float bodyRadius = 0.28f;
        [SerializeField] float turnSpeed = 600f;

        [Tooltip("Seconds the walk/idle blend takes to settle, so a stop is not a snap.")]
        [SerializeField] float moveDamping = 0.1f;

        PathFollower mover;
        SteeringAgent body;

        Vector3 target, facing;
        Vector3? steppedInFrom, stepOutTo;
        int retries;
        float stepping, pace = 1f;

        public Animator Animator => animator;
        public float WalkSpeed => walkSpeed;

        /// <summary>The spot last walked to.</summary>
        public Vector3 Target => target;

        /// <summary>Metres a second covered this frame, by the grid or by a stride stepped in.</summary>
        public float Speed => Mathf.Max(body != null ? body.Velocity.magnitude : 0f, stepping);

        public bool IsMoving => Speed > 0.05f;

        void Awake()
        {
            mover = NavAgentSetup.Ensure(gameObject);
            body = GetComponent<SteeringAgent>();
            mover.StoppingDistance = 0.12f;
            SetPace(1f);

            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        /// <summary>
        /// Sets off for <paramref name="point"/>, to face <paramref name="thenFace"/> there, at
        /// <paramref name="speedShare"/> of walking speed. Stepped in beside a table, it first steps back
        /// out the way it came.
        /// </summary>
        public void WalkTo(Vector3 point, Vector3 thenFace, float speedShare = 1f)
        {
            target = point;
            facing = thenFace;
            retries = 0;
            Stuck = false;
            SetPace(speedShare);

            if (!mover.enabled) mover.enabled = true;

            if (steppedInFrom.HasValue)
            {
                stepOutTo = steppedInFrom;
                steppedInFrom = null;
                mover.Stop();
                return;
            }

            mover.SetDestination(point);
        }

        /// <summary>
        /// True once at the spot walked to and turned to face the way asked - or once the spot has been
        /// given up on as out of reach, which <see cref="Stuck"/> then says.
        /// </summary>
        public bool Arrive() => Reached() && Turn(facing);

        /// <summary>The last spot walked to could not be reached without going through something: they
        /// stopped as close as they could get.</summary>
        public bool Stuck { get; private set; }

        /// <summary>Stands still where it is. A stride stepped in is remembered, so the next walk steps out first.</summary>
        public void Stop()
        {
            if (mover != null && mover.enabled) mover.Stop();
            stepOutTo = null;
        }

        /// <summary>Off the grid altogether - picked up and carried - until <see cref="Land"/>.</summary>
        public void Lift()
        {
            Stop();
            steppedInFrom = null;
            if (mover != null) mover.enabled = false;
        }

        /// <summary>Put down at <paramref name="position"/>, back on the grid.</summary>
        public void Land(Vector3 position)
        {
            steppedInFrom = stepOutTo = null;
            transform.position = position;
            if (mover == null) return;

            mover.enabled = true;
            mover.Warp(position);
            mover.Stop();
        }

        /// <summary>Turns toward <paramref name="direction"/> on the spot; true once it is facing it.</summary>
        public bool Turn(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) return true;

            Quaternion want = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
            return Quaternion.Angle(transform.rotation, want) < 4f;
        }

        bool Reached()
        {
            if (stepOutTo.HasValue)
            {
                if (StepToward(stepOutTo.Value))
                {
                    stepOutTo = null;
                    mover.SetDestination(target);
                }

                return false;
            }

            if (Stuck) return true;
            if (!mover.HasArrived) return false;

            Vector3 to = target - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance <= ArriveRadius) return true;

            // The last stride in, beside a chair or up to a counter: only if it goes through nothing.
            // Checked once, at its start; after that they are on their way along a clear line.
            if (distance <= MaxStepIn && (steppedInFrom.HasValue || GridRoute.StepIsClear(transform.position, target)))
            {
                if (!steppedInFrom.HasValue) steppedInFrom = transform.position;
                StepToward(target);
                return false;
            }

            // The nav grid says "arrived" when it finds no route at all, so callers are never left
            // waiting. Here that is a failure: ask again, then give up where they stand.
            if (retries < 2)
            {
                retries++;
                mover.SetDestination(target);
                return false;
            }

            Stuck = true;
            return true;
        }

        /// <summary>A stride straight toward <paramref name="point"/>, ignoring the grid; true once there.</summary>
        bool StepToward(Vector3 point, float speed = 0f)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance <= 0.04f) return true;

            if (speed <= 0f) speed = walkSpeed * 0.6f;
            transform.position += to / distance * Mathf.Min(distance, speed * Time.deltaTime);
            Turn(to);
            stepping = speed;
            return false;
        }

        void SetPace(float share)
        {
            pace = Mathf.Clamp(share, 0.2f, 1.5f);
            if (body != null) body.ConfigureBody(walkSpeed * pace, bodyRadius);
        }

        void LateUpdate()
        {
            CharacterLocomotion.Drive(animator, Speed, Time.deltaTime, moveDamping);
            stepping = 0f;
        }
    }
}
