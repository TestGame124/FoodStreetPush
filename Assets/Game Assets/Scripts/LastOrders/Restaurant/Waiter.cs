using FoodStreet.Navigation;
using FoodStreet.Visitors;
using LastOrders.Kitchen;
using UnityEngine;

namespace LastOrders.Restaurant
{
    /// <summary>
    /// Waiting staff: carries dishes from the serving pass to the customers who ordered them.
    ///
    /// Waits at its place in front of the pass. When a seated customer's order is ready on the pass it
    /// takes the order on - one waiter per customer, the dish promised to them so no other waiter walks
    /// for it - goes to the pass, lifts the plate, carries it in both hands to the side of the table
    /// and sets it down in front of them; then the meal is served and paid for, as a tapped serve
    /// always was. Customers the player taps are seen to first, then whoever has waited longest.
    ///
    /// If the customer is gone by the time the plate gets there, it goes back on the pass. While a
    /// waiter is on duty a tap on a customer calls one over rather than serving on the spot
    /// (<see cref="OnDuty"/>), so the dish is always seen to come from the kitchen.
    ///
    /// Looks for work a few times a second, not every frame, and walks the nav grid round the tables
    /// and the counter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Waiter : MonoBehaviour
    {
        enum Step { AtHome, ToPass, Lifting, ToTable, SettingDown, BringingBack, GoingHome }

        static readonly int MoveId = Animator.StringToHash("Move");

        /// <summary>The furthest a walker stopped short is stepped the rest of the way, ignoring the grid.</summary>
        const float MaxStepIn = 1.6f;

        /// <summary>Close enough to the spot walked to.</summary>
        const float ArriveRadius = 0.12f;

        static int onDuty;

        [SerializeField] Animator animator;

        [Tooltip("The animator layer that holds the arms up round the plate.")]
        [SerializeField] int carryLayer = 1;

        [Header("Walking")]
        [SerializeField] float walkSpeed = 2.2f;
        [SerializeField] float bodyRadius = 0.28f;
        [SerializeField] float turnSpeed = 600f;

        [Header("Serving")]
        [SerializeField] float liftSeconds = 0.32f;
        [SerializeField] float setDownSeconds = 0.38f;

        [Tooltip("Seconds between looks round the dining room for an order to bring.")]
        [SerializeField] float lookInterval = 0.25f;

        [Tooltip("Where the plate rides: this far in front of the hands, and this far above them.")]
        [SerializeField] Vector2 carryOffset = new Vector2(0.06f, 0.05f);

        PathFollower mover;
        SteeringAgent body;
        VisitorManager visitors;
        Transform leftHand, rightHand;

        Step step;
        float nextLook, carryWeight, stepping;
        Vector3 home, facing, target;
        Vector3? steppedInFrom, stepOutTo;
        int retries;
        Quaternion homeTurn;

        Visitor guest;
        ItemType dish;
        int amount;
        bool promised, holding;
        PassDish carried;
        Transform pickUp;

        /// <summary>How many waiters are working. Taps call one over while there are any.</summary>
        public static int OnDuty => onDuty;

        /// <summary>True while it has nobody to serve.</summary>
        public bool IsFree => step == Step.AtHome || step == Step.GoingHome;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => onDuty = 0;

        void Awake()
        {
            mover = NavAgentSetup.Ensure(gameObject);
            body = GetComponent<SteeringAgent>();
            if (body != null) body.ConfigureBody(walkSpeed, bodyRadius);
            mover.StoppingDistance = 0.12f;

            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
        }

        void OnEnable()
        {
            onDuty++;
            home = transform.position;
            homeTurn = transform.rotation;
            step = Step.AtHome;
            nextLook = Time.time + Random.Range(0f, lookInterval);
        }

        void OnDisable()
        {
            onDuty--;
            Drop();
            if (mover != null) mover.Stop();
        }

        /// <summary>Its place at the pass, facing the dining room. Set by the scene setup, or where it starts.</summary>
        public void SetHome(Vector3 position, Quaternion rotation)
        {
            home = position;
            homeTurn = rotation;
        }

        // --- The round ------------------------------------------------------------------------------

        void Update()
        {
            switch (step)
            {
                case Step.AtHome:
                    Turn(homeTurn * Vector3.forward);
                    LookForWork();
                    break;

                case Step.ToPass:
                    if (!StillWanted())
                    {
                        Drop();
                        GoHome();
                    }
                    else if (Reached() && Turn(facing)) Lift();

                    break;

                case Step.Lifting:
                    if (holding) TakeToTable();
                    break;

                case Step.ToTable:
                    if (!StillWanted()) BringBack();
                    else if (Reached() && Turn(facing)) SetDown();
                    break;

                case Step.BringingBack:
                    if (Reached() && Turn(facing)) PutBack();
                    break;

                case Step.GoingHome:
                    // Taken by the next order on the way, rather than walking back to stand still.
                    if (LookForWork()) break;
                    if (Reached()) step = Step.AtHome;
                    break;
            }

            Animate();
        }

        void LateUpdate()
        {
            if (carried != null && holding)
            {
                Pose hands = CarryPose();
                carried.transform.SetPositionAndRotation(hands.position, hands.rotation);
            }
        }

        /// <summary>
        /// Picks the next customer to bring a dish to: one the player tapped, else whoever has waited
        /// longest, among those whose whole order is on the pass and no other waiter has taken on.
        /// </summary>
        bool LookForWork()
        {
            if (Time.time < nextLook) return false;
            nextLook = Time.time + lookInterval;

            ServingPassBoard board = ServingPassBoard.Instance;
            if (board == null) return false;

            if (visitors == null) visitors = FindFirstObjectByType<VisitorManager>();
            if (visitors == null) return false;

            Visitor best = null;
            var order = default(VisitorOrder);
            var everyone = visitors.Active;
            for (int i = 0; i < everyone.Count; i++)
            {
                Visitor v = everyone[i];
                if (v == null || !v.IsWaitingToBeServed || v.HasWaiter || v.IsAlarmed) continue;

                VisitorOrder wants = v.Order;
                if (board.Available(wants.Item) < wants.Amount) continue;

                if (best == null || (v.ServiceRequested && !best.ServiceRequested) ||
                    (v.ServiceRequested == best.ServiceRequested && v.WaitingSince < best.WaitingSince))
                {
                    best = v;
                    order = wants;
                }
            }

            if (best == null || !best.ClaimWaiter(this)) return false;
            if (!board.Reserve(order.Item, order.Amount))
            {
                best.ReleaseWaiter(this);
                return false;
            }

            guest = best;
            dish = order.Item;
            amount = order.Amount;
            promised = true;

            pickUp = board.PickUpPoint(dish, transform.position);
            if (pickUp == null)
            {
                Drop();
                return false;
            }

            WalkTo(pickUp.position, pickUp.forward);
            step = Step.ToPass;
            return true;
        }

        void Lift()
        {
            ServingPassBoard board = ServingPassBoard.Instance;
            carried = board != null ? board.TakeDish(dish, amount, transform.position) : null;
            promised = false;

            if (carried == null)
            {
                // The dish went elsewhere while they walked over. Somebody else may still be served.
                Drop();
                GoHome();
                return;
            }

            holding = false;
            Pose hands = CarryPose();
            carried.HopTo(hands.position, hands.rotation, liftSeconds, 0.22f, () => holding = true);
            step = Step.Lifting;
        }

        void TakeToTable()
        {
            Vector3 stand = guest.ServicePoint(transform.position);
            Vector3 toTable = guest.TableEdge - stand;
            toTable.y = 0f;
            WalkTo(stand, toTable);
            step = Step.ToTable;
        }

        void SetDown()
        {
            holding = false;
            Pose landing = guest.PlateLanding(carried.gameObject);
            Visitor servedTo = guest;
            PassDish plate = carried;

            plate.HopTo(landing.position, landing.rotation, setDownSeconds, 0.16f, () =>
            {
                carried = null;
                if (servedTo != null && servedTo.IsServedBy(this) && servedTo.IsWaitingToBeServed)
                {
                    servedTo.ServeBrought(plate.gameObject);
                }
                else
                {
                    // Up and gone in the moment it was put down: it is not left on an empty table.
                    ServingPassBoard board = ServingPassBoard.Instance;
                    if (board != null) board.PutBack(plate, amount);
                    else plate.Recycle();
                }

                guest = null;
                GoHome();
            });

            step = Step.SettingDown;
        }

        /// <summary>The customer is gone: the plate goes back on the pass.</summary>
        void BringBack()
        {
            if (guest != null) guest.ReleaseWaiter(this);
            guest = null;

            ServingPassBoard board = ServingPassBoard.Instance;
            pickUp = board != null ? board.PickUpPoint(dish, transform.position) : null;
            if (pickUp == null)
            {
                PutBack();
                return;
            }

            WalkTo(pickUp.position, pickUp.forward);
            step = Step.BringingBack;
        }

        void PutBack()
        {
            if (carried != null)
            {
                ServingPassBoard board = ServingPassBoard.Instance;
                if (board != null) board.PutBack(carried, amount);
                else carried.Recycle();
            }

            carried = null;
            holding = false;
            GoHome();
        }

        void GoHome()
        {
            WalkTo(home, homeTurn * Vector3.forward);
            step = Step.GoingHome;
        }

        /// <summary>Lets go of everything taken on: the customer, the promised dish, a plate in hand.</summary>
        void Drop()
        {
            ServingPassBoard board = ServingPassBoard.Instance;

            if (promised && board != null) board.Unreserve(dish, amount);
            promised = false;

            if (guest != null) guest.ReleaseWaiter(this);
            guest = null;

            if (carried != null)
            {
                if (board != null) board.PutBack(carried, amount);
                else carried.Recycle();
            }

            carried = null;
            holding = false;
        }

        bool StillWanted() => guest != null && guest.isActiveAndEnabled && guest.IsServedBy(this) && guest.IsWaitingToBeServed;

        // --- The body -------------------------------------------------------------------------------

        /// <summary>
        /// Sets off for <paramref name="point"/>, to face <paramref name="thenFace"/> there. Stepped in
        /// beside a table, it first steps back out the way it came: the table's cells are solid, and a
        /// route asked for from inside them goes nowhere.
        /// </summary>
        void WalkTo(Vector3 point, Vector3 thenFace)
        {
            target = point;
            facing = thenFace;
            retries = 0;

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
        /// True once at the spot walked to. The nav grid stops a walker at the edge of the cells a
        /// table stands on, a stride short of the chair's side: that last stride is stepped straight
        /// in, as the customers do, and remembered so it can be stepped out again. A route that could
        /// not be found is asked for again, and in the end walked straight.
        /// </summary>
        bool Reached()
        {
            stepping = 0f;

            if (stepOutTo.HasValue)
            {
                if (StepToward(stepOutTo.Value))
                {
                    stepOutTo = null;
                    mover.SetDestination(target);
                }

                return false;
            }

            if (!mover.HasArrived) return false;

            Vector3 to = target - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance <= ArriveRadius) return true;

            if (distance <= MaxStepIn)
            {
                if (!steppedInFrom.HasValue) steppedInFrom = transform.position;
                StepToward(target);
                return false;
            }

            // The nav grid says "arrived" when it finds no route at all, so callers are never left
            // waiting. Here that is a failure: ask again, then walk it.
            if (retries < 2)
            {
                retries++;
                mover.SetDestination(target);
                return false;
            }

            StepToward(target, walkSpeed);
            return false;
        }

        /// <summary>A stride straight toward <paramref name="point"/>, ignoring the grid; true once there.</summary>
        bool StepToward(Vector3 point, float pace = 0f)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance <= 0.04f) return true;

            if (pace <= 0f) pace = walkSpeed * 0.6f;
            transform.position += to / distance * Mathf.Min(distance, pace * Time.deltaTime);
            Turn(to);
            stepping = pace;
            return false;
        }

        /// <summary>Turns toward <paramref name="direction"/> on the spot; true once it is facing it.</summary>
        bool Turn(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) return true;

            Quaternion want = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
            return Quaternion.Angle(transform.rotation, want) < 4f;
        }

        void Animate()
        {
            if (animator == null) return;

            float speed = Mathf.Max(body != null ? body.Velocity.magnitude : 0f, stepping);
            animator.SetFloat(MoveId, Mathf.Clamp01(speed / Mathf.Max(0.1f, walkSpeed)), 0.1f, Time.deltaTime);

            // The arms come up as the plate is lifted and down as it is set on the table.
            float want = carried != null && step != Step.SettingDown ? 1f : 0f;
            carryWeight = Mathf.MoveTowards(carryWeight, want, Time.deltaTime / 0.25f);
            if (carryLayer > 0 && carryLayer < animator.layerCount) animator.SetLayerWeight(carryLayer, carryWeight);
        }

        /// <summary>Between the hands, a little in front and above them, level and facing the way they walk.</summary>
        Pose CarryPose()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            Quaternion level = Quaternion.LookRotation(forward, Vector3.up);

            Vector3 at = leftHand != null && rightHand != null
                ? (leftHand.position + rightHand.position) * 0.5f
                : transform.position + Vector3.up * 1.0f + forward * 0.3f;

            return new Pose(at + forward * carryOffset.x + Vector3.up * carryOffset.y, level);
        }
    }
}
