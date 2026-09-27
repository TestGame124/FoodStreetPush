using System;
using System.Collections.Generic;
using LastOrders.Interaction;
using LastOrders.Medical;
using UnityEngine;

namespace FoodStreet.Visitors
{
    public enum VisitorStage
    {
        /// <summary>In the car, on the way in. Not drawn.</summary>
        Riding,

        /// <summary>Out of the car, walking to the chair.</summary>
        Walking,

        /// <summary>At the table, showing an order, waiting. Indefinitely.</summary>
        Seated,

        /// <summary>Served, eating. The only stage on a timer.</summary>
        Eating,

        /// <summary>Walking back to the car.</summary>
        Returning,

        /// <summary>Back in the car. Not drawn.</summary>
        Boarding,

        /// <summary>Fed, but it is dark outside: still at the table, waiting for morning.</summary>
        Sheltering,

        /// <summary>Hurt, and walking to a free cot in the hospital tent.</summary>
        ToHospital,

        /// <summary>In a cot in the hospital tent.</summary>
        InHospital,

        /// <summary>Well again, walking to where the shuttle collects them.</summary>
        ToShuttle,

        /// <summary>Waiting beside the tent for the shuttle.</summary>
        WaitingForShuttle,

        /// <summary>Walking out to the shuttle - or, with none, off down the road.</summary>
        LeavingOnFoot,

        /// <summary>Finished. Waiting to be pooled.</summary>
        Gone,
    }

    /// <summary>
    /// One person, from the car door to the chair and back.
    ///
    /// The whole loop is four beats - walk in, sit and ask, be served, walk out - and each one
    /// is a method below. Nothing here is on a patience timer: a visitor who cannot be served
    /// sits and waits, because their bay and their chair are the two things holding the next
    /// arrival back, and a player who is short of bread should see the queue stop rather than
    /// watch people quietly leave while they cook.
    ///
    /// The tap is the entire interaction, and the rule it follows is section 4's: a tap is
    /// never silently accepted and never silently refused. Tapping a visitor who cannot be
    /// served pulses their bubble and says what is missing; tapping one who is not waiting
    /// declines the tap outright so it passes through to whatever is behind them.
    ///
    /// Nobody travels after dark. A visitor still at a table when night falls stays in the diner
    /// until morning, and one hurt on a bad night goes to the hospital tent and leaves on a
    /// later shuttle rather than in their own car. The manager decides when; each of those is
    /// one method below.
    ///
    /// Nobody eats with zombies inside the walls, either. While the compound is breached a
    /// visitor stays frozen at their table: the meal half eaten stops being eaten, and an order
    /// still waiting cannot be served. It all picks up again once the compound is clear.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class Visitor : MonoBehaviour, IInteractableObject, ITappable, IPatient
    {
        static readonly int MoveHash = Animator.StringToHash("Move");
        static readonly int SitHash = Animator.StringToHash("Sit");
        static readonly int EatHash = Animator.StringToHash("Eat");

        [SerializeField] VisitorDefinition definition;
        [SerializeField] OrderBubble bubble;
        [SerializeField] Animator animator;

        readonly WaypointWalker walker = new WaypointWalker();

        VisitorRoads roads;
        ParkingLot lot;
        VisitorVehicle car;
        SeatRegistry seats;
        SeatRef seat;

        VisitorOrder order;
        GameObject plate;
        float eatRemaining;
        bool lastAffordable;

        /// <summary>Set for the night: a visitor who finishes eating stays at the table rather
        /// than walking out into the dark.</summary>
        bool holdDeparture;

        /// <summary>Zombies are inside the walls: not eating, and not being served.</summary>
        bool alarmed;

        Pose bedPose;
        Action<Visitor> onInBed;

        public VisitorStage Stage { get; private set; } = VisitorStage.Gone;

        /// <summary>At a table in the diner - waiting, eating, or waiting out the night. The
        /// people who are inside when the zombies come.</summary>
        public bool IsInside =>
            Stage == VisitorStage.Seated || Stage == VisitorStage.Eating || Stage == VisitorStage.Sheltering;

        public string PatientId => $"visitor:{GetInstanceID()}";

        public PatientKind Kind => PatientKind.Visitor;

        public VisitorDefinition Definition => definition;

        public VisitorOrder Order => order;

        /// <summary>The car they came in. The manager matches a parked car back to its own
        /// passenger with this rather than guessing at whoever is still riding.</summary>
        public VisitorVehicle Car => car;

        /// <summary>True while they are at the table with an unfilled order. The one state the
        /// player can act on, and the one the HUD counts.</summary>
        public bool IsWaitingToBeServed => Stage == VisitorStage.Seated && !order.IsEmpty;

        /// <summary>Frozen at the table because zombies are inside the walls.</summary>
        public bool IsAlarmed => alarmed;

        /// <summary>Raised once they are back in the car and this instance can be pooled.</summary>
        public event Action<Visitor> OnJourneyEnded;

        /// <summary>Raised when an order is filled, with what it paid. The manager turns this
        /// into coins; the visitor does not know what a wallet is.</summary>
        public event Action<Visitor, int> OnServed;

        // --- The journey -----------------------------------------------------------------

        /// <summary>
        /// Starts a visit.
        ///
        /// Called with the chair already reserved. Reserving it here would leave a window
        /// between the manager deciding somebody may come and this claiming a seat, and one
        /// frame is enough for two visitors to be sent to the same chair.
        /// </summary>
        public void Begin(VisitorDefinition who, VisitorRoads route, ParkingLot parking,
                          SeatRegistry registry, VisitorVehicle vehicle, in SeatRef chair)
        {
            definition = who;
            roads = route;
            lot = parking;
            seats = registry;
            car = vehicle;
            seat = chair;

            order = definition != null ? definition.RollOrder() : default;

            if (order.IsEmpty)
            {
                Debug.LogError(
                    $"[Visitors] '{DisplayName}' has no orders it can ask for, so it can never " +
                    "be served. Give the definition at least one item request.", definition);
            }

            eatRemaining = 0f;
            plate = null;

            Stage = VisitorStage.Riding;

            // Out of sight until the car stops. A person sliding along the road inside their own
            // bodywork is the kind of thing nobody reports and everybody sees.
            SetVisible(false);
            bubble?.Hide();
        }

        /// <summary>Called by the manager when the car has parked. The visitor gets out.</summary>
        public void GetOutOfCar()
        {
            if (Stage != VisitorStage.Riding) return;

            ParkingSlot bay = car != null ? car.Bay : null;

            transform.position = bay != null ? bay.DoorPoint : transform.position;
            SetVisible(true);

            walker.Follow(roads != null ? roads.WalkToSeat(lot, seat) : null);
            Stage = VisitorStage.Walking;
        }

        void Update()
        {
            switch (Stage)
            {
                case VisitorStage.Walking:
                    if (walker.Advance(transform, WalkSpeed, TurnSpeed, ArriveRadius, Time.deltaTime))
                        SitDown();
                    break;

                case VisitorStage.Seated:
                    RefreshAffordable();
                    break;

                case VisitorStage.Eating:
                    Eat();
                    break;

                case VisitorStage.Returning:
                    if (walker.Advance(transform, WalkSpeed, TurnSpeed, ArriveRadius, Time.deltaTime))
                        BoardCar();
                    break;

                case VisitorStage.ToHospital:
                    // A limp, not a walk: this is somebody who got hurt.
                    if (walker.Advance(transform, WalkSpeed * 0.55f, TurnSpeed, ArriveRadius, Time.deltaTime))
                        LieDown(bedPose);
                    break;

                case VisitorStage.ToShuttle:
                    if (walker.Advance(transform, WalkSpeed, TurnSpeed, ArriveRadius, Time.deltaTime))
                        Stage = VisitorStage.WaitingForShuttle;
                    break;

                case VisitorStage.LeavingOnFoot:
                    if (walker.Advance(transform, WalkSpeed, TurnSpeed, ArriveRadius, Time.deltaTime))
                        Depart();
                    break;
            }

            Animate();
        }

        void SitDown()
        {
            transform.position = seat.SitPoint;
            WaypointWalker.Face(transform, seat.SitForward);

            TableBehaviour.Chair chair = seat.Chair;
            if (chair != null) chair.isSitted = true;

            Stage = VisitorStage.Seated;

            ShowOrder();
        }

        void ShowOrder()
        {
            // Nobody orders with zombies in the yard. The bubble comes back when it is clear.
            if (bubble == null || order.IsEmpty || alarmed) return;

            ItemDataSO data = order.Data;
            lastAffordable = CanAfford();

            bubble.Show(data != null ? data.itemImage : null, order.Amount, lastAffordable);
        }

        /// <summary>
        /// Keeps the disc honest while they sit there.
        ///
        /// Polled rather than driven off InventorySystem's change event, because the event is
        /// static and a pooled visitor that forgot to unsubscribe would keep a dead instance
        /// alive for the session. A comparison against the last answer means the bubble is only
        /// touched when it actually changes.
        /// </summary>
        void RefreshAffordable()
        {
            bool affordable = CanAfford();
            if (affordable == lastAffordable) return;

            lastAffordable = affordable;
            bubble?.SetAffordable(affordable);
        }

        void Eat()
        {
            // The fork goes down until the compound is clear; the meal waits on the table.
            if (alarmed) return;

            eatRemaining -= Time.deltaTime;
            if (eatRemaining > 0f) return;

            ReturnPlate();

            // Finished after dark: they stay where it is safe until morning.
            if (holdDeparture)
            {
                Stage = VisitorStage.Sheltering;
                return;
            }

            LeaveTable();
        }

        void LeaveTable()
        {
            ParkingSlot bay = car != null ? car.Bay : null;
            Vector3 doorPoint = bay != null ? bay.DoorPoint : transform.position;

            ReleaseSeat();

            walker.Follow(roads != null ? roads.WalkToCar(lot, doorPoint) : null);
            Stage = VisitorStage.Returning;
        }

        void BoardCar()
        {
            SetVisible(false);
            Stage = VisitorStage.Boarding;

            car?.DriveOut();

            Stage = VisitorStage.Gone;
            OnJourneyEnded?.Invoke(this);
        }

        // --- Closing time and the night --------------------------------------------------

        /// <summary>
        /// The restaurant closed while they were still in the car. The visit ends here; the car
        /// is turned round by whoever owns it, with nobody getting out.
        /// </summary>
        public void CancelRide()
        {
            if (Stage != VisitorStage.Riding) return;

            ReleaseSeat();

            Stage = VisitorStage.Gone;
            OnJourneyEnded?.Invoke(this);
        }

        /// <summary>
        /// The restaurant closed while they were walking in. Back to the car and home before it
        /// gets dark - nobody pays anything for a place that shut on time.
        /// </summary>
        public void TurnBack()
        {
            if (Stage != VisitorStage.Walking) return;

            ParkingSlot bay = car != null ? car.Bay : null;
            Vector3 doorPoint = bay != null ? bay.DoorPoint : transform.position;

            // The way in is the apron, the door, the doorway, the chair. Past the door they are
            // at the building and go back out through it; before it, straight back to the car.
            bool atTheDoor = walker.Remaining <= 2;

            ReleaseSeat();

            var path = new List<Vector3>(4);
            if (atTheDoor && roads != null) path.AddRange(roads.WalkToCar(lot, doorPoint));
            else
            {
                if (walker.Remaining <= 3 && lot != null) path.Add(lot.LaneEntry);
                path.Add(doorPoint);
            }

            walker.Follow(path);
            Stage = VisitorStage.Returning;
        }

        /// <summary>Night has fallen with them at a table. Once fed, they stay until morning.</summary>
        public void Shelter() => holdDeparture = true;

        /// <summary>
        /// Zombies are inside the walls, or have gone. While alarmed they stop eating and cannot
        /// be served; the order bubble goes, and comes back with the all-clear. A visitor still
        /// walking to their table carries on to it - the diner is the safe place to be.
        /// </summary>
        public void SetAlarmed(bool on)
        {
            if (alarmed == on) return;

            alarmed = on;

            if (on)
            {
                bubble?.Hide();
                return;
            }

            if (Stage == VisitorStage.Seated) ShowOrder();
        }

        /// <summary>Morning. Anyone who was only waiting for daylight gets up and goes home;
        /// anyone still waiting for their order carries on waiting for it.</summary>
        public void EndShelter()
        {
            holdDeparture = false;

            if (Stage == VisitorStage.Sheltering) LeaveTable();
        }

        // --- The hospital tent -----------------------------------------------------------

        /// <summary>
        /// Hurt: out of the chair and over to a cot, limping. Their car goes home without them
        /// - they will leave on a later shuttle - so the car is dropped here.
        /// </summary>
        public void GoToHospital(IReadOnlyList<Vector3> path, Pose bed, Action<Visitor> arrived)
        {
            LeaveEverything();

            bedPose = bed;
            onInBed = arrived;

            walker.Follow(path);
            Stage = VisitorStage.ToHospital;

            if (walker.Arrived) LieDown(bed);
        }

        /// <summary>Straight into the cot - they were carried over in the night, while nobody
        /// could be out there to see it.</summary>
        public void CarriedToHospital(Pose bed, Action<Visitor> arrived)
        {
            LeaveEverything();

            onInBed = arrived;
            LieDown(bed);
        }

        void LeaveEverything()
        {
            ReturnPlate();
            ReleaseSeat();
            bubble?.Hide();
            walker.Clear();

            holdDeparture = false;
            order = default;
            car = null;

            SetVisible(true);
        }

        void LieDown(Pose bed)
        {
            walker.Clear();
            transform.SetPositionAndRotation(bed.position, bed.rotation);

            Stage = VisitorStage.InHospital;

            Action<Visitor> arrived = onInBed;
            onInBed = null;
            arrived?.Invoke(this);
        }

        /// <summary>Well again: up out of the cot and over to wait for the shuttle.</summary>
        public void GetUp(IReadOnlyList<Vector3> pathToWait)
        {
            if (Stage != VisitorStage.InHospital && Stage != VisitorStage.ToHospital) return;

            Stand(pathToWait);
            Stage = VisitorStage.ToShuttle;
        }

        /// <summary>
        /// Walks the path and leaves the map at the end of it: out to the shuttle's door, or with
        /// no shuttle in the level, off down the road on foot.
        /// </summary>
        public void LeaveOnFoot(IReadOnlyList<Vector3> path)
        {
            Stand(path);
            Stage = VisitorStage.LeavingOnFoot;
        }

        void Stand(IReadOnlyList<Vector3> path)
        {
            // Upright again, feet on the ground, facing where they are off to. Lying down is the
            // only time the body is anything but upright - and the walker keeps whatever height
            // it starts at, so leaving them at mattress height would float them all the way home.
            Vector3 here = transform.position;
            if (path != null && path.Count > 0) here.y = path[path.Count - 1].y;

            Vector3 toward = path != null && path.Count > 0 ? path[0] - here : transform.forward;
            toward.y = 0f;

            transform.SetPositionAndRotation(here,
                toward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toward) : Quaternion.identity);

            walker.Follow(path);
        }

        void Depart()
        {
            SetVisible(false);

            Stage = VisitorStage.Gone;
            OnJourneyEnded?.Invoke(this);
        }

        // --- The tap ---------------------------------------------------------------------

        public string DisplayName =>
            definition != null && !string.IsNullOrEmpty(definition.displayName)
                ? definition.displayName
                : "Visitor";

        /// <summary>Mouse3D's path. Declining the tap lets it fall through to whatever is
        /// behind, which is how tapping past a walking visitor still reaches the ground.</summary>
        public bool OnTouch()
        {
            if (!IsWaitingToBeServed) return false;

            return TapFeedback.Report(OnTapped());
        }

        /// <summary>The blockout TapSelector's path. Same decision, different caller.</summary>
        public string OnTapped()
        {
            if (!IsWaitingToBeServed) return null;

            if (alarmed) return $"{DisplayName} won't eat with zombies inside the walls.";

            ItemDataSO data = order.Data;
            string label = data != null && !string.IsNullOrEmpty(data.Name) ? data.Name : order.Item.ToString();

            InventorySystem inventory = InventorySystem.instance;
            if (inventory == null) return "No inventory in this scene, so nobody can be served.";

            if (!inventory.TryConsume(order.Item, order.Amount))
            {
                int held = inventory.ItemCount(order.Item);

                bubble?.FlashShort();
                return $"{DisplayName} wants {order.Amount} {label}. You have {held}.";
            }

            Serve();
            return $"Served {DisplayName}: {order.Amount} {label}.";
        }

        void Serve()
        {
            bubble?.Hide();
            ShowPlate();

            TableBehaviour.Chair chair = seat.Chair;
            if (chair != null) chair.foodServed = true;

            eatRemaining = definition != null ? definition.RollEatSeconds() : 4f;
            Stage = VisitorStage.Eating;

            int payment = definition != null ? definition.payment : 0;
            if (payment > 0) OnServed?.Invoke(this, payment);
        }

        // --- The plate -------------------------------------------------------------------

        /// <summary>
        /// Borrows a plate from the level's own pool and puts it in front of them.
        ///
        /// Borrowed rather than built: these are the same plates the waiting staff carry, and a
        /// second set of food props beside them would be two kinds of dinner on one table.
        /// </summary>
        void ShowPlate()
        {
            Transform at = seat.PlatePoint;
            InventoryObject pool = PlatePool();

            if (at == null || pool == null) return;

            plate = pool.GetFoodPlate(at.position);
            if (plate != null) plate.transform.SetParent(at, worldPositionStays: true);
        }

        void ReturnPlate()
        {
            if (plate == null) return;

            InventoryObject pool = PlatePool();
            if (pool != null) pool.ReturnFoodPlate(plate);

            plate = null;
        }

        static InventoryObject PlatePool()
        {
            Zone zone = LevelController.Instance != null ? LevelController.Instance.zone : null;
            return zone != null ? zone.InventoryPosition : null;
        }

        // --- Housekeeping ----------------------------------------------------------------

        void ReleaseSeat()
        {
            if (seats != null) seats.Release(seat);

            seat = default;
        }

        bool CanAfford()
        {
            if (order.IsEmpty) return false;

            InventorySystem inventory = InventorySystem.instance;
            return inventory != null && inventory.ItemCount(order.Item) >= order.Amount;
        }

        void SetVisible(bool visible)
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                // The bubble hides and shows itself with the order, so leaving it out of this
                // stops a bubble reappearing on a visitor who has already got back in the car.
                if (bubble != null && renderer.transform.IsChildOf(bubble.transform)) continue;

                renderer.enabled = visible;
            }

            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
                collider.enabled = visible;
        }

        void Animate()
        {
            if (animator == null) return;

            bool moving = Stage == VisitorStage.Walking || Stage == VisitorStage.Returning ||
                          Stage == VisitorStage.ToHospital || Stage == VisitorStage.ToShuttle ||
                          Stage == VisitorStage.LeavingOnFoot;

            animator.SetFloat(MoveHash, moving ? 1f : 0f);
            animator.SetBool(SitHash, Stage == VisitorStage.Seated || Stage == VisitorStage.Eating ||
                                      Stage == VisitorStage.Sheltering);
            animator.SetBool(EatHash, Stage == VisitorStage.Eating && !alarmed);
        }

        /// <summary>Puts the visitor back in its starting state, for the pool. Releases the
        /// chair and the plate, so a visitor removed abnormally cannot hold either forever.</summary>
        public void ResetForPool()
        {
            ReturnPlate();
            ReleaseSeat();

            walker.Clear();
            bubble?.Hide();

            roads = null;
            lot = null;
            car = null;
            seats = null;
            order = default;
            eatRemaining = 0f;
            lastAffordable = false;
            holdDeparture = false;
            alarmed = false;
            onInBed = null;

            // A visitor pooled out of a cot would otherwise arrive tomorrow lying on the road.
            transform.rotation = Quaternion.identity;

            Stage = VisitorStage.Gone;

            OnJourneyEnded = null;
            OnServed = null;
        }

        public void Configure(OrderBubble orderBubble, Animator characterAnimator)
        {
            bubble = orderBubble;
            animator = characterAnimator;
        }

        float WalkSpeed => definition != null ? definition.walkSpeed : 1.6f;
        float TurnSpeed => definition != null ? definition.turnSpeed : 540f;
        float ArriveRadius => definition != null ? definition.arriveRadius : 0.25f;
    }
}
