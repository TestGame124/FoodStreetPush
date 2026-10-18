using System.Collections.Generic;
using FoodStreet.Visitors;
using LastOrders.Audio;
using LastOrders.Crew;
using LastOrders.Jobs;
using LastOrders.Kitchen;
using UnityEngine;

namespace LastOrders.Restaurant
{
    /// <summary>
    /// Waiting tables: carries dishes from the serving pass to the customers who ordered them.
    ///
    /// When a seated customer's order is ready on the pass it takes the order on - one waiter per
    /// customer, the dish promised to them so no other waiter walks for it - goes to the pass, lifts the
    /// plate, carries it in both hands to the side of the table and sets it down in front of them; then
    /// the meal is served and paid for, as a tapped serve always was. Customers the player taps are seen
    /// to first, then whoever has waited longest.
    ///
    /// If the customer is gone by the time the plate gets there, it goes back on the pass. While a
    /// waiter is free to serve, a tap on a customer calls one over rather than serving on the spot
    /// (<see cref="OnDuty"/>), so the dish is always seen to come from the kitchen.
    ///
    /// One of a crew member's duties (<see cref="ICrewDuty"/>): it has the body while there is an order
    /// to bring, and lets a more important duty - helping at a station - take it. With nothing to
    /// bring, a crew member who also has chores goes off to them; one who has not walks back to their
    /// place at the pass and waits there.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavWalker))]
    public sealed class Waiter : MonoBehaviour, ICrewDuty
    {
        enum Step { AtHome, ToPass, Lifting, ToTable, SettingDown, BringingBack, GoingHome }

        static readonly List<Waiter> everyone = new List<Waiter>();

        [Tooltip("The animator layer that holds the arms up round the plate, when it has none called 'Carry'.")]
        [SerializeField] int carryLayer = 1;

        [SerializeField] int priority = 20;

        [Header("Serving")]
        [SerializeField] float liftSeconds = 0.32f;
        [SerializeField] float setDownSeconds = 0.38f;

        [Tooltip("Seconds between looks round the dining room for an order to bring.")]
        [SerializeField] float lookInterval = 0.25f;

        [Tooltip("Where the plate rides: this far in front of the hands, and this far above them.")]
        [SerializeField] Vector2 carryOffset = new Vector2(0.06f, 0.05f);

        NavWalker legs;
        CrewBrain brain;
        Animator animator;
        VisitorManager visitors;
        Transform leftHand, rightHand;

        Step step;
        float nextLook, readyAt, carryWeight;
        bool ready;
        Vector3 home;
        Quaternion homeTurn;

        Visitor guest;
        ItemType dish;
        int amount;
        bool promised, holding;
        PassDish carried;
        Transform pickUp;

        /// <summary>How many waiters are free to serve - on duty, and not taken off it to help in the
        /// kitchen. Taps call one over while there are any; with none, a tap serves on the spot.</summary>
        public static int OnDuty
        {
            get
            {
                int n = 0;
                for (int i = 0; i < everyone.Count; i++)
                    if (everyone[i] != null && everyone[i].Serving) n++;
                return n;
            }
        }

        /// <summary>True while it has nobody to serve.</summary>
        public bool IsFree => step == Step.AtHome || step == Step.GoingHome;

        /// <summary>On duty and free to serve: no more important duty has the crew member.</summary>
        public bool Serving => isActiveAndEnabled && (brain == null || brain.IsFreeFor(this));

        public int Priority => priority;

        /// <summary>An order in hand, or one up on the pass for somebody nobody is bringing it to.</summary>
        public bool HasWork => !IsFree || OrderWaiting();

        public string Activity
        {
            get
            {
                switch (step)
                {
                    case Step.ToPass:
                    case Step.Lifting: return "Fetching an order from the pass";
                    case Step.ToTable:
                    case Step.SettingDown: return guest != null ? $"Serving {guest.DisplayName}" : "Serving";
                    case Step.BringingBack: return "Taking a plate back to the pass";
                    case Step.GoingHome: return "Heading back to the pass";
                    default: return "Waiting at the pass";
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => everyone.Clear();

        void Awake()
        {
            // A waiter set up before the crew had legs of their own gets them here.
            legs = GetComponent<NavWalker>();
            if (legs == null) legs = gameObject.AddComponent<NavWalker>();
            brain = GetComponent<CrewBrain>();
            if (brain == null) brain = gameObject.AddComponent<CrewBrain>();

            animator = legs.Animator != null ? legs.Animator : GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }

            if (animator != null)
            {
                int named = animator.GetLayerIndex(WorkerGestures.CarryLayerName);
                if (named > 0) carryLayer = named;
            }

            home = transform.position;
            homeTurn = transform.rotation;
        }

        void OnEnable()
        {
            everyone.Add(this);
            step = Step.AtHome;
            nextLook = Time.time + Random.Range(0f, lookInterval);
        }

        void OnDisable()
        {
            everyone.Remove(this);
            Drop();
            step = Step.AtHome;
        }

        /// <summary>Its place at the pass, where it waits for the next dish.</summary>
        public Vector3 Home => home;

        public Quaternion HomeRotation => homeTurn;

        /// <summary>Its place at the pass, facing the dining room. Set by the scene setup, or where it
        /// starts; moved when the pass is carried back with the kitchen, and walked to if it was
        /// standing at the old one.</summary>
        public void SetHome(Vector3 position, Quaternion rotation)
        {
            home = position;
            homeTurn = rotation;

            if (Application.isPlaying && isActiveAndEnabled && step == Step.AtHome && brain != null && brain.Active == (ICrewDuty)this)
                GoHome();
        }

        // --- The duty --------------------------------------------------------------------------------

        public void SetActive(bool active)
        {
            if (active) return;

            // Taken off it - sent to the kitchen, or picked up - with an order in hand: the order goes
            // back for somebody else, the plate back on the pass.
            Drop();
            step = Step.AtHome;
        }

        public void Tick(float dt)
        {
            switch (step)
            {
                case Step.AtHome:
                    if (!LookForWork()) legs.Turn(homeTurn * Vector3.forward);
                    break;

                case Step.ToPass:
                    if (!StillWanted())
                    {
                        Drop();
                        GoHome();
                    }
                    else if (legs.Arrive()) Lift();

                    break;

                case Step.Lifting:
                    if (holding) TakeToTable();
                    break;

                case Step.ToTable:
                    if (!StillWanted()) BringBack();
                    else if (legs.Arrive()) SetDown();
                    break;

                case Step.BringingBack:
                    if (legs.Arrive()) PutBack();
                    break;

                case Step.GoingHome:
                    // Taken by the next order on the way, rather than walking back to stand still.
                    if (LookForWork()) break;
                    if (legs.Arrive()) step = Step.AtHome;
                    break;
            }
        }

        void LateUpdate()
        {
            if (carried != null && holding)
            {
                Pose hands = CarryPose();
                carried.transform.SetPositionAndRotation(hands.position, hands.rotation);
            }

            // The arms come up as the plate is lifted and down as it is set on the table.
            if (animator == null) return;
            float want = carried != null && step != Step.SettingDown ? 1f : 0f;
            carryWeight = Mathf.MoveTowards(carryWeight, want, Time.deltaTime / 0.25f);
            if (carryLayer > 0 && carryLayer < animator.layerCount) animator.SetLayerWeight(carryLayer, carryWeight);
        }

        // --- The round ------------------------------------------------------------------------------

        /// <summary>Is any order up and waiting for a waiter? Looked at a few times a second, not every frame.</summary>
        bool OrderWaiting()
        {
            if (Time.time < readyAt) return ready;

            readyAt = Time.time + lookInterval;
            ready = FindGuest(out _, out _);
            return ready;
        }

        /// <summary>
        /// The next customer to bring a dish to: one the player tapped, else whoever has waited longest,
        /// among those whose whole order is on the pass and no other waiter has taken on.
        /// </summary>
        bool FindGuest(out Visitor best, out VisitorOrder order)
        {
            best = null;
            order = default;

            ServingPassBoard board = ServingPassBoard.Instance;
            if (board == null) return false;

            if (visitors == null) visitors = FindFirstObjectByType<VisitorManager>();
            if (visitors == null) return false;

            IReadOnlyList<Visitor> everybody = visitors.Active;
            for (int i = 0; i < everybody.Count; i++)
            {
                Visitor v = everybody[i];
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

            return best != null;
        }

        /// <summary>Takes on the next customer's order, if there is one, and sets off for the pass.</summary>
        bool LookForWork()
        {
            if (Time.time < nextLook) return false;
            nextLook = Time.time + lookInterval;

            if (!FindGuest(out Visitor best, out VisitorOrder order)) return false;

            ServingPassBoard board = ServingPassBoard.Instance;
            if (!best.ClaimWaiter(this)) return false;
            if (!board.Reserve(order.Item, order.Amount))
            {
                best.ReleaseWaiter(this);
                return false;
            }

            guest = best;
            dish = order.Item;
            amount = order.Amount;
            promised = true;
            ready = false;

            pickUp = board.PickUpPoint(dish, transform.position);
            if (pickUp == null)
            {
                Drop();
                return false;
            }

            legs.WalkTo(pickUp.position, pickUp.forward);
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
            SoundController.Play(SoundId.DishLifted, carried.transform.position);
            Pose hands = CarryPose();
            carried.HopTo(hands.position, hands.rotation, liftSeconds, 0.22f, () => holding = true);
            step = Step.Lifting;
        }

        void TakeToTable()
        {
            Vector3 stand = guest.ServicePoint(transform.position);
            Vector3 toTable = guest.TableEdge - stand;
            toTable.y = 0f;
            legs.WalkTo(stand, toTable);
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

            legs.WalkTo(pickUp.position, pickUp.forward);
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
            legs.WalkTo(home, homeTurn * Vector3.forward);
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
