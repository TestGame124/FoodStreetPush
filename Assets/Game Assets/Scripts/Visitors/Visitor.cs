using System;
using System.Collections.Generic;
using LastOrders.Interaction;
using LastOrders.Kitchen;
using LastOrders.Medical;
using LastOrders.Restaurant;
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

        /// <summary>Seconds from standing in front of the chair to sat on it. The animator's
        /// stand-to-sit transition is set to the same by 'Set Up Seated Visitors'.</summary>
        public const float SitSeconds = 0.55f;

        /// <summary>Seconds from sat to standing in front of the chair, likewise.</summary>
        public const float StandSeconds = 0.5f;

        /// <summary>Seconds to turn and face the table before sitting.</summary>
        const float TurnSeconds = 0.2f;

        /// <summary>Seconds after stepping out before the empty chair is pushed back in.</summary>
        const float TuckDelay = 0.45f;

        /// <summary>Metres short of the spot in front of the chair that counts as there. Tight, so
        /// nobody slides sideways into the chair while sitting down.</summary>
        const float StandArriveRadius = 0.04f;

        /// <summary>Seconds the walk/idle blend takes to settle, so a stop is not a snap.</summary>
        const float MoveDamping = 0.1f;

        /// <summary>
        /// Getting into the chair and out of it, in order. Sitting down and getting up each take a
        /// beat of their own: the chair slides out as they reach the table, they step in, turn, sit
        /// back onto it and scoot in to the table with it - and on leaving, the chair scoots out,
        /// they stand, step clear and it is pushed back in.
        /// </summary>
        enum Seating { None, SitDown, ScootIn, Settled, ScootOut, StandUp }

        [SerializeField] VisitorDefinition definition;
        [SerializeField] OrderBubble bubble;
        [SerializeField] Animator animator;

        readonly WaypointWalker walker = new WaypointWalker();

        Seating seating;
        float seatingClock;
        bool chairPulled;
        Vector3 seatFrom, seatTo;
        Quaternion turnFrom;
        List<Vector3> afterStanding;
        SeatedPose pose;

        VisitorRoads roads;
        ParkingLot lot;
        VisitorVehicle car;
        SeatRegistry seats;
        SeatRef seat;

        VisitorOrder order;
        GameObject plate;
        float eatRemaining;
        bool lastAffordable;

        /// <summary>Whoever is bringing their order, while somebody is.</summary>
        object waiter;

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
        public bool IsWaitingToBeServed => Stage == VisitorStage.Seated && seating == Seating.Settled && !order.IsEmpty;

        /// <summary>A waiter has taken their order on and is bringing it.</summary>
        public bool HasWaiter => waiter != null;

        /// <summary>The player tapped them: the next free waiter comes to them first.</summary>
        public bool ServiceRequested { get; private set; }

        /// <summary>When they sat down to order, for serving the longest-waiting first.</summary>
        public float WaitingSince { get; private set; }

        /// <summary>Which way the table is from their chair: a dish set in front of them faces back along it.</summary>
        public Vector3 TableForward => seat.SitForward;

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
            waiter = null;
            ServiceRequested = false;

            seating = Seating.None;
            chairPulled = false;
            afterStanding = null;

            Stage = VisitorStage.Riding;

            // Out of sight until the car stops. A person sliding along the road inside their own
            // bodywork is the kind of thing nobody reports and everybody sees.
            SetVisible(false);
            bubble?.Hide();
        }

        /// <summary>Called by the manager when the car has parked. The visitor gets out - the
        /// <paramref name="order"/>th of the party to, a step behind the one before.</summary>
        public void GetOutOfCar(int order = 0)
        {
            if (Stage != VisitorStage.Riding) return;

            ParkingSlot bay = car != null ? car.Bay : null;

            Vector3 door = bay != null ? bay.DoorPoint : transform.position;
            if (bay != null && order > 0) door -= bay.ParkForward * (0.75f * order);

            transform.position = door;
            SetVisible(true);

            walker.Follow(roads != null ? roads.WalkToSeat(lot, seat) : null);
            Stage = VisitorStage.Walking;
        }

        void Update()
        {
            // Sitting down, scooting in and getting up again have the body to themselves while
            // they last; what the visitor is there for picks up once they are sat, or up.
            if (StepSeating(Time.deltaTime))
            {
                Animate();
                return;
            }

            switch (Stage)
            {
                case VisitorStage.Walking:
                    // The chair comes out as they reach the table, so it is out by the time they
                    // step in front of it.
                    if (!chairPulled && walker.Remaining <= 1) PullOutChair();

                    float radius = walker.Remaining <= 1 ? StandArriveRadius : ArriveRadius;
                    if (walker.Advance(transform, WalkSpeed, TurnSpeed, radius, Time.deltaTime))
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

        /// <summary>
        /// Arrived in front of the chair: turn to the table and sit back onto the seat. They count
        /// as seated from here - inside, and staying - but order only once they are in at the table.
        /// </summary>
        void SitDown()
        {
            Stage = VisitorStage.Seated;

            if (!SeatIsMeasured)
            {
                // No chair left to get into - the table went while they walked over. Sat where the
                // seat was, as before there was any getting in at all.
                transform.position = seat.SitPoint;
                WaypointWalker.Face(transform, seat.SitForward);
                Settle();
                return;
            }

            if (!chairPulled) PullOutChair();

            seatFrom = transform.position;
            seatTo = seat.PulledPoint;
            turnFrom = transform.rotation;
            seatingClock = 0f;
            seating = Seating.SitDown;
        }

        /// <summary>In at the table: the chair is theirs as far as the staff can tell, and they ask.</summary>
        void Settle()
        {
            seating = Seating.Settled;
            WaitingSince = Time.time;

            TableBehaviour.Chair chair = seat.Chair;
            if (chair != null) chair.isSitted = true;

            ShowOrder();
        }

        void PullOutChair()
        {
            chairPulled = true;
            if (seat.Seating != null) seat.Seating.PullOut(seat.Index);
        }

        bool SeatIsMeasured => seat.IsValid && seat.Seating != null && seat.Seating.Has(seat.Index);

        /// <summary>Progress of the sit itself, after the turn to face the table.</summary>
        float SitProgress => Mathf.Clamp01((seatingClock - TurnSeconds) / SitSeconds);

        float StandProgress => Mathf.Clamp01(seatingClock / StandSeconds);

        /// <summary>
        /// One step of getting in or out of the chair. Returns true while that is all the visitor
        /// is doing.
        ///
        /// The body follows the chair while it slides rather than sliding beside it, so a visitor
        /// scooting in is carried with their seat - which is the whole of what makes it read as
        /// sitting IN a chair rather than hovering in front of one.
        /// </summary>
        bool StepSeating(float deltaSeconds)
        {
            switch (seating)
            {
                case Seating.SitDown:
                {
                    seatingClock += deltaSeconds;

                    float turn = Smooth(seatingClock / TurnSeconds);
                    transform.rotation = Quaternion.Slerp(turnFrom, Quaternion.LookRotation(seat.SitForward), turn);
                    transform.position = Vector3.Lerp(seatFrom, seatTo, Smooth(SitProgress));

                    if (SitProgress >= 1f)
                    {
                        seating = Seating.ScootIn;
                        if (seat.Seating != null) seat.Seating.ScootIn(seat.Index);
                    }

                    return true;
                }

                case Seating.ScootIn:
                    if (seat.IsValid) transform.position = seat.SeatNow;
                    if (seat.Seating == null || !seat.Seating.IsSliding(seat.Index)) Settle();
                    return true;

                case Seating.ScootOut:
                    if (seat.IsValid) transform.position = seat.SeatNow;
                    if (seat.Seating == null || !seat.Seating.IsSliding(seat.Index)) BeginStandUp(transform.position);
                    return true;

                case Seating.StandUp:
                    seatingClock += deltaSeconds;
                    transform.position = Vector3.Lerp(seatFrom, seatTo, Smooth(StandProgress));

                    if (StandProgress >= 1f) StepAwayFromTable();
                    return true;
            }

            return false;
        }

        void BeginStandUp(Vector3 from)
        {
            seatFrom = from;
            seatTo = seat.IsValid ? seat.StandPoint : from;
            seatTo.y = from.y;
            seatingClock = 0f;
            seating = Seating.StandUp;
        }

        /// <summary>Up, and off: the chair is free, and goes back under the table once they are clear of it.</summary>
        void StepAwayFromTable()
        {
            ReleaseSeat(TuckDelay);
            seating = Seating.None;

            walker.Follow(afterStanding);
            afterStanding = null;
        }

        /// <summary>
        /// Up from the table and off along <paramref name="path"/> - out past the side of the chair
        /// first, so nobody walks away through the table. Called whatever point of sitting down
        /// they had reached: caught halfway down, they go straight back up; already getting up,
        /// only where they are off to changes.
        /// </summary>
        void GetUpAndGo(IReadOnlyList<Vector3> path)
        {
            walker.Clear();

            var route = new List<Vector3>((path != null ? path.Count : 0) + 1);
            if (path != null) route.AddRange(path);

            switch (seating)
            {
                case Seating.None:
                    ReleaseSeat();
                    walker.Follow(route);
                    return;

                case Seating.SitDown:
                    BeginStandUp(transform.position);
                    break;

                case Seating.ScootIn:
                case Seating.Settled:
                    seating = Seating.ScootOut;
                    if (seat.Seating != null) seat.Seating.PullOut(seat.Index);
                    break;
            }

            if (SeatIsMeasured)
                route.Insert(0, seat.SidePoint(route.Count > 0 ? route[0] : transform.position - transform.forward));

            afterStanding = route;
        }

        /// <summary>Out of the chair on the spot, with no getting up - for being carried off, and the pool.</summary>
        void StopSeating()
        {
            ReleaseSeat();
            seating = Seating.None;
            afterStanding = null;
        }

        void ShowOrder()
        {
            // Nobody orders with zombies in the yard, nor halfway into a chair. The bubble comes
            // back when it is clear, or once they are in at the table.
            if (bubble == null || order.IsEmpty || alarmed || seating != Seating.Settled) return;

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

            GetUpAndGo(roads != null ? roads.WalkToCar(lot, doorPoint) : null);
            Stage = VisitorStage.Returning;
        }

        void BoardCar()
        {
            SetVisible(false);
            Stage = VisitorStage.Boarding;

            // The last of the party back in drives them all home.
            car?.PassengerBoarded();

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

            // The way in is the apron, the door, the doorway, then up to the chair. Past the door
            // they are at the building and go back out through it; before it, straight back to
            // the car.
            bool atTheDoor = walker.Remaining <= 1 + VisitorRoads.SeatApproachPoints;
            bool pastTheApron = walker.Remaining <= 2 + VisitorRoads.SeatApproachPoints;

            // A chair that had come out for them goes back in.
            ReleaseSeat();

            var path = new List<Vector3>(4);
            if (atTheDoor && roads != null) path.AddRange(roads.WalkToCar(lot, doorPoint));
            else
            {
                if (pastTheApron && lot != null) path.Add(lot.LaneEntry);
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

            GetUpAndGo(path);
            Stage = VisitorStage.ToHospital;

            if (seating == Seating.None && walker.Arrived) LieDown(bed);
        }

        /// <summary>Straight into the cot - they were carried over in the night, while nobody
        /// could be out there to see it.</summary>
        public void CarriedToHospital(Pose bed, Action<Visitor> arrived)
        {
            LeaveEverything();
            StopSeating();

            onInBed = arrived;
            LieDown(bed);
        }

        /// <summary>Drops the meal, the order and the car. The chair is left to whoever is taking
        /// them away: walked off, they get up out of it first; carried off, they do not.</summary>
        void LeaveEverything()
        {
            ReturnPlate();
            bubble?.Hide();
            walker.Clear();

            holdDeparture = false;
            order = default;

            // Not going back to the car: the rest of the party will drive it home without them.
            car?.PassengerLost();
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
            // Sent off before they had finished getting out of a chair: this path wins.
            if (seating != Seating.None) StopSeating();

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

            // With waiters on the floor, the tap calls one over rather than conjuring the plate: the
            // dish comes off the pass in their hands.
            ServingPassBoard pass = ServingPassBoard.Instance;
            if (Waiter.OnDuty > 0 && pass != null)
            {
                if (HasWaiter) return $"{DisplayName}'s {label} is on its way.";

                if (pass.Available(order.Item) < order.Amount)
                {
                    bubble?.FlashShort();
                    return $"{DisplayName} wants {order.Amount} {label}. The pass has {pass.Available(order.Item)} ready.";
                }

                ServiceRequested = true;
                return $"A waiter will bring {DisplayName} {order.Amount} {label}.";
            }

            if (!inventory.TryConsume(order.Item, order.Amount))
            {
                int held = inventory.ItemCount(order.Item);

                bubble?.FlashShort();
                return $"{DisplayName} wants {order.Amount} {label}. You have {held}.";
            }

            Serve();
            return $"Served {DisplayName}: {order.Amount} {label}.";
        }

        // --- The waiter ------------------------------------------------------------------

        /// <summary>
        /// A waiter takes their order on. Only one at a time, and only while they are waiting for it;
        /// false if somebody else already has it.
        /// </summary>
        public bool ClaimWaiter(object by)
        {
            if (by == null || waiter != null || !IsWaitingToBeServed) return false;

            waiter = by;
            return true;
        }

        /// <summary>True while <paramref name="by"/> is the one bringing their order.</summary>
        public bool IsServedBy(object by) => by != null && waiter == by;

        /// <summary>The waiter gives up on them - the dish went elsewhere - and another may come.</summary>
        public void ReleaseWaiter(object by)
        {
            if (waiter == by) waiter = null;
        }

        /// <summary>
        /// Where a waiter coming from <paramref name="from"/> stands to set their dish down: beside their
        /// chair, at the side of the table, on the waiter's side.
        /// </summary>
        public Vector3 ServicePoint(Vector3 from) => seat.SidePoint(from);

        /// <summary>The middle of the table's edge in front of them, at the top's height.</summary>
        public Vector3 TableEdge => SeatIsMeasured ? seat.Seating.EdgePoint(seat.Index) : seat.SitPoint + seat.SitForward * 0.4f;

        /// <summary>
        /// Where <paramref name="dish"/> will stand when it is set in front of them - on the table top,
        /// all of it on it - and which way it will face. Asked before it is put down, so the waiter can
        /// set it exactly there.
        /// </summary>
        public Pose PlateLanding(GameObject dish)
        {
            Transform at = seat.PlatePoint;
            Quaternion facing = Quaternion.LookRotation(seat.SitForward, Vector3.up);
            if (at == null || dish == null) return new Pose(dish != null ? dish.transform.position : transform.position, facing);

            Transform t = dish.transform;
            t.GetPositionAndRotation(out Vector3 wasAt, out Quaternion wasTurned);

            t.SetPositionAndRotation(at.position, facing);
            if (SeatIsMeasured) SetOnTable(dish);
            var landing = new Pose(t.position, facing);

            t.SetPositionAndRotation(wasAt, wasTurned);
            return landing;
        }

        /// <summary>
        /// A waiter sets their order in front of them: <paramref name="dish"/> is already where
        /// <see cref="PlateLanding"/> said. The meal starts, and is paid for, as a tap-served one is.
        /// </summary>
        public void ServeBrought(GameObject dish)
        {
            bubble?.Hide();

            plate = dish;
            Transform at = seat.PlatePoint;
            if (plate != null && at != null) plate.transform.SetParent(at, worldPositionStays: true);

            BeginEating();
        }

        void Serve()
        {
            bubble?.Hide();
            ShowPlate();
            BeginEating();
        }

        void BeginEating()
        {
            waiter = null;
            ServiceRequested = false;

            TableBehaviour.Chair chair = seat.Chair;
            if (chair != null) chair.foodServed = true;

            eatRemaining = definition != null ? definition.RollEatSeconds() : 4f;
            Stage = VisitorStage.Eating;

            ItemDataSO meal = order.Data;
            int payment = meal != null ? meal.coinsReward * Mathf.Max(1, order.Amount) : (definition != null ? definition.payment : 0);
            if (payment > 0) OnServed?.Invoke(this, payment);
            if (meal != null) LastOrders.Core.LastOrdersBridge.AddExperience(meal.starsReward * Mathf.Max(1, order.Amount));
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
            if (plate == null) return;

            if (SeatIsMeasured) SetOnTable(plate);
            plate.transform.SetParent(at, worldPositionStays: true);
        }

        /// <summary>
        /// Puts the plate down on the table that is actually there, all of it on the top. The
        /// plate points were placed for an older, lower table: one left half sunk into this top
        /// and hanging off its edge, the other hovering over it.
        /// </summary>
        void SetOnTable(GameObject dish)
        {
            Renderer[] renderers = dish.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            Vector3 inward = seat.SitForward;
            float inFromEdge = Vector3.Dot(bounds.center - seat.Seating.EdgePoint(seat.Index), inward);
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            float onto = Mathf.Max(0f, radius + 0.03f - inFromEdge);

            dish.transform.position += inward * onto + Vector3.up * (seat.Seating.TableTop - bounds.min.y);
        }

        void ReturnPlate()
        {
            if (plate == null) return;

            // A dish a waiter brought goes back to the pass's own plates.
            if (plate.TryGetComponent(out PassDish dish))
            {
                dish.PopOut();
                plate = null;
                return;
            }

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

        /// <summary>Frees the chair - and pushes it back under the table, after
        /// <paramref name="tuckDelay"/> seconds, if it had come out for them.</summary>
        void ReleaseSeat(float tuckDelay = 0f)
        {
            if (seat.Seating != null && (chairPulled || seating != Seating.None))
                seat.Seating.TuckIn(seat.Index, tuckDelay);

            if (seats != null) seats.Release(seat);

            seat = default;
            chairPulled = false;
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

            bool moving = seating == Seating.None &&
                          (Stage == VisitorStage.Walking || Stage == VisitorStage.Returning ||
                           Stage == VisitorStage.ToHospital || Stage == VisitorStage.ToShuttle ||
                           Stage == VisitorStage.LeavingOnFoot);

            // In the chair from the moment they start down until they start up again.
            bool sitting = (seating == Seating.SitDown && seatingClock >= TurnSeconds) ||
                           seating == Seating.ScootIn || seating == Seating.Settled || seating == Seating.ScootOut;

            bool eating = Stage == VisitorStage.Eating && !alarmed && seating == Seating.Settled;

            animator.SetFloat(MoveHash, moving ? 1f : 0f, MoveDamping, Time.deltaTime);
            animator.SetBool(SitHash, sitting);
            animator.SetBool(EatHash, eating);

            DrivePose(eating);
        }

        /// <summary>Tells the body where the table is and what to do at it: hips onto the cushion,
        /// the lean of getting in and out, hands on the top, the fork going up.</summary>
        void DrivePose(bool eating)
        {
            if (pose == null)
            {
                pose = animator.GetComponent<SeatedPose>();
                if (pose == null) pose = animator.gameObject.AddComponent<SeatedPose>();
            }

            if (seating == Seating.None || !SeatIsMeasured)
            {
                pose.Drive(default);
                return;
            }

            float seated = 1f, lean = 0f;
            if (seating == Seating.SitDown)
            {
                seated = SitProgress;
                lean = Mathf.Sin(seated * Mathf.PI);
            }
            else if (seating == Seating.StandUp)
            {
                seated = 1f - StandProgress;
                lean = Mathf.Sin(StandProgress * Mathf.PI);
            }

            Transform platePoint = seat.PlatePoint;

            pose.Drive(new SeatedPose.Frame
            {
                seated = seated,
                lean = lean,
                handsOnTable = seating == Seating.ScootIn || seating == Seating.Settled,
                eating = eating,
                tableEdge = seat.Seating.EdgePoint(seat.Index),
                inward = seat.SitForward,
                seatTop = seat.Seating.SeatTop(seat.Index),
                plate = plate != null ? plate.transform.position : platePoint != null ? platePoint.position : Vector3.zero,
                hasPlate = plate != null || platePoint != null,
            });
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Puts the visitor back in its starting state, for the pool. Releases the
        /// chair and the plate, so a visitor removed abnormally cannot hold either forever.</summary>
        public void ResetForPool()
        {
            ReturnPlate();
            StopSeating();
            if (pose != null) pose.ResetPose();

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
