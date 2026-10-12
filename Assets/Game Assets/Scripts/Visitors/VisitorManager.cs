using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.DayCycle;
using LastOrders.Medical;
using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// Who turns up, and when.
    ///
    /// Three conditions, and daylight. Somebody comes when there is a chair to sit in, a bay
    /// to leave the car in, and fewer than the cap already here. There is no opening-hours
    /// sign to tap: this system answers to the restaurant's own physical capacity, which is
    /// the thing the player builds and can therefore see.
    ///
    /// Daylight is the one rule from outside. Nobody travels after dark, so arrivals stop when
    /// the light starts to go: a car still on its way turns round, and anyone walking in from
    /// the car park goes back to it. Anyone already at a table stays, and if night falls with
    /// them still there they wait it out inside until morning. A level with no day cycle keeps
    /// the old behaviour and is open all hours.
    ///
    /// The Safety Reputation sets the pace inside those limits: the safer survivors believe the
    /// restaurant is, the more often a car turns off the road and the more people are in it. At
    /// zero a car still comes now and then with one or two, so a restaurant can always earn its
    /// way back.
    ///
    /// It also looks after the visitors a bad night leaves hurt: their car goes home without
    /// them, they recover in the hospital tent, and they leave on the shuttle.
    ///
    /// And nobody comes, or eats, while zombies are inside the walls. Cars on their way in turn
    /// round, and everyone already here freezes where they are - a meal half eaten stops being
    /// eaten, an order waiting cannot be served - until the compound is clear again.
    ///
    /// The car park is the gate the brief puts first and it is the more interesting of the two,
    /// because it is a limit the player can stand next to and count. Trade is capped by
    /// somewhere to put the cars, and the fix is to build more parking - which is a decision,
    /// where a hidden spawn interval is not.
    ///
    /// <see cref="StatusReason"/> exists because an empty forecourt looks identical whichever
    /// of the three is stopping it, and "visitors are not spawning" is a bug report nobody can
    /// answer from the outside. The HUD shows it and it is logged on the change.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class VisitorManager : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] VisitorTrafficProfile profile;

        [Header("Scene")]
        [SerializeField] VisitorRoads roads;
        [SerializeField] ParkingLot parking;
        [SerializeField] SeatRegistry seats;

        [Tooltip("Where pooled cars and people wait between visits. One is made if left empty.")]
        [SerializeField] Transform poolRoot;

        [Header("After a bad night")]
        [Tooltip("Takes healed visitors home. Found in the scene when left empty; with none, " +
                 "they walk out down the road.")]
        [SerializeField] VisitorShuttle shuttle;

        [Tooltip("Where the hurt are looked after. Found in the scene when left empty.")]
        [SerializeField] HospitalTent tent;

        readonly List<Visitor> active = new List<Visitor>();
        readonly List<Visitor> finished = new List<Visitor>();
        readonly List<SeatRef> partySeats = new List<SeatRef>(4);

        /// <summary>Cars whose driver is in the hospital tent. They go home in the morning,
        /// empty, and free their bays.</summary>
        readonly List<VisitorVehicle> carsGoingHome = new List<VisitorVehicle>();

        VisitorPool pool;
        float nextArrivalIn;
        string statusReason = "not started";

        /// <summary>The day cycle. Null in a level without one, which is then open all hours.</summary>
        GameComposition composition;

        /// <summary>Visitors on the map, riding, walking, seated or leaving.</summary>
        public int ActiveVisitors => active.Count;

        /// <summary>Everyone on the map, for the waiters to look over. Read, never kept.</summary>
        public IReadOnlyList<Visitor> Active => active;

        /// <summary>How many are sitting with an order nobody has filled. The number the player
        /// is being asked to act on.</summary>
        public int WaitingToBeServed
        {
            get
            {
                int waiting = 0;
                for (int i = 0; i < active.Count; i++)
                {
                    if (active[i] != null && active[i].IsWaitingToBeServed) waiting++;
                }

                return waiting;
            }
        }

        /// <summary>Why nobody is arriving, in the player's words. Null while they are.</summary>
        public string StatusReason => statusReason;

        public ParkingLot Parking => parking;

        public SeatRegistry Seats => seats;

        void Awake()
        {
            if (roads == null) roads = FindFirstObjectByType<VisitorRoads>();
            if (parking == null) parking = FindFirstObjectByType<ParkingLot>();
            if (seats == null) seats = FindFirstObjectByType<SeatRegistry>();

            if (poolRoot == null)
            {
                var host = new GameObject("VisitorPool");
                host.transform.SetParent(transform, false);
                poolRoot = host.transform;
            }

            pool = new VisitorPool(poolRoot);
        }

        void Start()
        {
            if (!Ready()) return;

            nextArrivalIn = profile.RollFirstDelay();

            ConnectToDayCycle();
        }

        /// <summary>Finds the day cycle and the hospital, if this level has them.</summary>
        void ConnectToDayCycle()
        {
            GameLifetimeScope scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null) return;

            if (shuttle == null) shuttle = FindFirstObjectByType<VisitorShuttle>();
            if (tent == null) tent = FindFirstObjectByType<HospitalTent>();

            if (shuttle != null) shuttle.SetSchedule(IsOpen);

            composition.Machine.OnPhaseChanged += HandlePhaseChanged;
            composition.Hospital.OnAdmitted += HandleAdmitted;
            composition.Hospital.OnDischarged += HandleDischarged;
            composition.Compound.OnBreached += HandleBreached;
            composition.Compound.OnCleared += HandleCleared;
        }

        void OnDestroy()
        {
            if (composition == null) return;

            composition.Machine.OnPhaseChanged -= HandlePhaseChanged;
            composition.Hospital.OnAdmitted -= HandleAdmitted;
            composition.Hospital.OnDischarged -= HandleDischarged;
            composition.Compound.OnBreached -= HandleBreached;
            composition.Compound.OnCleared -= HandleCleared;
        }

        void OnDisable()
        {
            // Frees every chair and bay. A scene torn down mid-service would otherwise leave
            // the level's own chair flags set, and the next play session would open with a
            // dining room that is full and empty at the same time.
            for (int i = active.Count - 1; i >= 0; i--) Retire(active[i]);

            active.Clear();
            seats?.ReleaseAll();
            parking?.ReleaseAll();
        }

        /// <summary>
        /// Checks the scene is wired before anything is allowed to run.
        ///
        /// Each miss is its own message. "The visitor system is not set up" is true of all four
        /// and useful for none of them.
        /// </summary>
        bool Ready()
        {
            if (profile == null)
            {
                Fail("No traffic profile, so there is nobody to send. Run " +
                     "'Food Street / Visitors / Generate Visitor Content'.");
                return false;
            }

            if (roads == null || !roads.IsComplete)
            {
                Fail("The road nodes are missing, so cars would drive to the world origin. " +
                     "Run 'Food Street / Visitors / Set Up Visitor System'.");
                return false;
            }

            if (parking == null)
            {
                Fail("No parking lot in the scene, so there is nowhere to leave a car.");
                return false;
            }

            if (seats == null)
            {
                Fail("No seat registry in the scene, so there is nowhere to sit.");
                return false;
            }

            return true;
        }

        void Fail(string message)
        {
            Debug.LogError($"[Visitors] {message}", this);
            enabled = false;
        }

        void Update()
        {
            Reap();

            string reason = WhyNotAccepting();
            SetStatus(reason);

            if (reason != null) return;

            nextArrivalIn -= Time.deltaTime;
            if (nextArrivalIn > 0f) return;

            nextArrivalIn = profile.RollInterval(seats.FreeChairRatio) * ReputationPace;
            Arrive();
        }

        /// <summary>
        /// How the Safety Reputation stretches or shortens the wait for the next car: survivors on
        /// the road come more often to a place they believe is safe. One in a level with no day
        /// cycle. The chairs and the car park still gate every arrival whatever this says.
        /// </summary>
        float ReputationPace => composition != null ? composition.Reputation.ArrivalIntervalScale : 1f;

        /// <summary>How many the next car brings: by the Safety Reputation, never more than there
        /// are free chairs and room on the street for.</summary>
        int RollPartySize()
        {
            int room = Mathf.Min(seats.FreeChairCount, profile.maxConcurrent - active.Count);
            if (composition == null || room <= 1) return 1;

            return composition.Reputation.RollPartySize(room, Random.value);
        }

        /// <summary>
        /// The gates, and which one is closed.
        ///
        /// Checked before the timer runs rather than after, so a full car park is not silently
        /// accruing arrivals to release in a burst the moment a space opens.
        ///
        /// Ordered the way somebody would diagnose it: the time of day, then the room, then the
        /// car park, then the cap. The first answer is the most useful one.
        /// </summary>
        string WhyNotAccepting()
        {
            string closed = ClosedReason();
            if (closed != null) return closed;

            if (seats.ChairCount == 0) return "there is nowhere to sit";
            if (!seats.HasFreeChair) return "every chair is taken";

            if (parking.Capacity == 0) return "there is nowhere to park";
            if (!parking.HasFreeSlot) return "the car park is full";

            if (active.Count >= profile.maxConcurrent) return "the street is as busy as it gets";

            return null;
        }

        /// <summary>True while travellers are on the road: the working day, unless the
        /// restaurant chose to close for repairs. Always, in a level with no day cycle.</summary>
        public bool IsOpen() => ClosedReason() == null;

        /// <summary>Why the road is empty for reasons that have nothing to do with capacity.</summary>
        string ClosedReason()
        {
            if (composition == null) return null;

            if (composition.Compound.IsBreached) return "zombies are inside the walls";

            switch (composition.Machine.CurrentPhase)
            {
                case DayPhase.Day:
                    return composition.Context.ClosedForRepairs ? "the restaurant is closed for repairs today" : null;

                case DayPhase.Dawn:
                    return "the restaurant has not opened yet";

                default:
                    return "nobody travels after dark";
            }
        }

        // --- The day cycle -------------------------------------------------------------------

        void HandlePhaseChanged(DayPhase previous, DayPhase current)
        {
            switch (current)
            {
                case DayPhase.Dusk:
                    TurnAwayArrivals();
                    break;

                case DayPhase.Night:
                    ShelterForTheNight();
                    break;

                case DayPhase.Day:
                    // The first car of the morning comes a moment after opening, not the frame
                    // the sign turns - and not in a burst for the hours the road was shut. A
                    // restaurant nobody trusts waits longer for even that one.
                    nextArrivalIn = profile.RollFirstDelay() * Mathf.Max(1f, ReputationPace);
                    EndTheNight();
                    break;
            }
        }

        /// <summary>
        /// The light is going. Nobody new is coming, and nobody who has not sat down yet stays:
        /// cars still on their way turn round, and anyone walking in heads back to their car.
        /// Anyone already at a table is left to finish.
        /// </summary>
        void TurnAwayArrivals()
        {
            for (int i = 0; i < active.Count; i++)
            {
                Visitor visitor = active[i];
                if (visitor == null) continue;

                switch (visitor.Stage)
                {
                    case VisitorStage.Riding:
                        VisitorVehicle car = visitor.Car;
                        visitor.CancelRide();
                        if (car != null) car.TurnAway(roads);
                        break;

                    case VisitorStage.Walking:
                        visitor.TurnBack();
                        break;
                }
            }
        }

        // --- Zombies inside the walls ----------------------------------------------------------

        /// <summary>
        /// Something got in. Anyone still on the road turns round, and everyone already here
        /// freezes: nobody eats and nobody can be served until it is clear. Visitors walking to
        /// their table keep going - the diner is the safest place to be - and anyone already on
        /// the way back to their car carries on out.
        /// </summary>
        void HandleBreached()
        {
            for (int i = 0; i < active.Count; i++)
            {
                Visitor visitor = active[i];
                if (visitor == null) continue;

                if (visitor.Stage == VisitorStage.Riding)
                {
                    VisitorVehicle car = visitor.Car;
                    visitor.CancelRide();
                    if (car != null) car.TurnAway(roads);
                    continue;
                }

                if (visitor.Stage == VisitorStage.Walking || visitor.IsInside) visitor.SetAlarmed(true);
            }
        }

        /// <summary>The compound is clear. Meals are picked back up and orders can be served.</summary>
        void HandleCleared()
        {
            for (int i = 0; i < active.Count; i++)
                if (active[i] != null) active[i].SetAlarmed(false);
        }

        /// <summary>Dark, with people still at the tables. They stay inside until morning, and
        /// the night's danger knows they are there.</summary>
        void ShelterForTheNight()
        {
            for (int i = 0; i < active.Count; i++)
            {
                Visitor visitor = active[i];
                if (visitor == null || !visitor.IsInside) continue;

                visitor.Shelter();
                composition.Danger.RegisterCivilian(visitor);
            }
        }

        /// <summary>
        /// Morning. Anyone who was only waiting for daylight goes home, and the cars of the
        /// people in the hospital tent go home without them.
        /// </summary>
        void EndTheNight()
        {
            for (int i = 0; i < active.Count; i++)
            {
                Visitor visitor = active[i];
                if (visitor == null) continue;

                composition.Danger.UnregisterCivilian(visitor);

                // Waiting for a cot: they stay where they are until one comes free.
                if (composition.Hospital.Contains(visitor)) continue;

                visitor.EndShelter();
            }

            SendCarsHome();
        }

        /// <summary>Drives home the parked cars of people in the tent, freeing their bays.</summary>
        void SendCarsHome()
        {
            for (int i = 0; i < carsGoingHome.Count; i++)
            {
                // A car with the rest of a party still out waits for them; the last one in drives it.
                if (carsGoingHome[i] != null && carsGoingHome[i].Stage == VisitorVehicle.VehicleStage.Parked &&
                    !carsGoingHome[i].WaitingForPassengers)
                    carsGoingHome[i].DriveOut();
            }

            carsGoingHome.Clear();
        }

        // --- The hospital tent ---------------------------------------------------------------

        /// <summary>
        /// One of ours has a cot. Overnight they are carried straight to it; during the day - a
        /// cot freed up while they waited in the diner - they limp over. Either way their car
        /// goes home without them in the morning.
        /// </summary>
        void HandleAdmitted(IPatient patient, HospitalCot cot)
        {
            if (!(patient is Visitor visitor) || !active.Contains(visitor)) return;

            composition.Danger.UnregisterCivilian(visitor);

            if (visitor.Car != null) carsGoingHome.Add(visitor.Car);

            Pose bed = tent != null ? tent.CotPose(cot.Index) : new Pose(visitor.transform.position, visitor.transform.rotation);

            if (composition.Machine.CurrentPhase == DayPhase.Night || tent == null)
            {
                visitor.CarriedToHospital(bed, InBed);
                return;
            }

            var path = new List<Vector3>(4) { roads.DoorInside, roads.DoorOutside, tent.Entrance, bed.position };
            visitor.GoToHospital(path, bed, InBed);

            // Admitted by day means the cot came free while they waited: the car can go now.
            if (composition.Machine.CurrentPhase == DayPhase.Day) SendCarsHome();
        }

        void InBed(Visitor visitor) => composition.Hospital.MarkInBed(visitor);

        /// <summary>Well again. Over to wait for the shuttle - or with no shuttle in this level,
        /// off down the road on foot.</summary>
        void HandleDischarged(IPatient patient)
        {
            if (!(patient is Visitor visitor) || !active.Contains(visitor)) return;

            Vector3 door = tent != null ? tent.Entrance : visitor.transform.position;

            if (shuttle != null && shuttle.IsConfigured)
            {
                Vector3 bench = tent != null ? tent.Bench(shuttle.WaitingCount) : door;

                visitor.GetUp(new[] { door, bench });
                shuttle.Enqueue(visitor);
                return;
            }

            visitor.LeaveOnFoot(new[] { door, roads.GateInside, roads.GateOutside, roads.RoadEnd });
        }

        void SetStatus(string reason)
        {
            if (reason == statusReason) return;

            statusReason = reason;

            // Logged on the change rather than every frame: an empty forecourt for ten minutes
            // should be one line in the console, and the line should say why.
            if (reason != null) Debug.Log($"[Visitors] No arrivals: {reason}.", this);
        }

        // --- One arrival -------------------------------------------------------------------

        /// <summary>
        /// Sends one car, with a party of one or more in it.
        ///
        /// The bay and the chairs are claimed here, before anything is taken from the pool, so
        /// the gap between "a space is spoken for" and "somebody is driving to it" is zero
        /// frames wide. Everything claimed is handed back on any failure below, because a
        /// half-built arrival that keeps a chair is a restaurant that quietly shrinks.
        /// </summary>
        void Arrive()
        {
            VisitorDefinition who = profile.PickVisitor();
            if (who == null)
            {
                Debug.LogError("[Visitors] The traffic profile has no visitors with a weight " +
                               "above zero, so nobody can ever arrive.", profile);
                enabled = false;
                return;
            }

            if (!Sendable(who))
            {
                Debug.LogError($"[Visitors] '{who.displayName}' has no visitor prefab or no " +
                               "vehicle prefab. Run 'Food Street / Visitors / Generate Visitor " +
                               "Content'.", who);
                return;
            }

            partySeats.Clear();
            if (seats.TryReserveParty(RollPartySize(), partySeats) == 0) return;

            VisitorVehicle car = pool.Take<VisitorVehicle>(who.vehicle.prefab, roads.RoadStart);
            if (car == null)
            {
                ReleaseParty();
                return;
            }

            car.ResetForPool();
            car.Configure(who.vehicle);

            if (!parking.TryReserve(car, out ParkingSlot bay))
            {
                ReleaseParty();
                pool.Return(car);
                return;
            }

            // The driver is whoever the car belongs to; anyone with them is whoever else is on
            // the road today, each with their own order.
            int boarded = 0;
            for (int i = 0; i < partySeats.Count; i++)
            {
                VisitorDefinition passenger = i == 0 ? who : profile.PickVisitor();
                if (!Sendable(passenger)) passenger = who;

                Visitor visitor = pool.Take<Visitor>(passenger.prefab, roads.RoadStart);
                if (visitor == null)
                {
                    seats.Release(partySeats[i]);
                    continue;
                }

                visitor.ResetForPool();
                visitor.Begin(passenger, roads, parking, seats, car, partySeats[i]);

                visitor.OnServed += HandleServed;
                visitor.OnJourneyEnded += HandleJourneyEnded;

                active.Add(visitor);
                boarded++;
            }

            partySeats.Clear();

            if (boarded == 0)
            {
                bay.Release();
                pool.Return(car);
                return;
            }

            car.ExpectPassengers(boarded);
            car.OnParked += HandleParked;
            car.OnDeparted += HandleDeparted;

            car.DriveIn(roads, parking, bay);
        }

        static bool Sendable(VisitorDefinition who) =>
            who != null && who.prefab != null && who.vehicle != null && who.vehicle.prefab != null;

        void ReleaseParty()
        {
            for (int i = 0; i < partySeats.Count; i++) seats.Release(partySeats[i]);
            partySeats.Clear();
        }

        /// <summary>
        /// A car has stopped, so its own passengers get out, one after another.
        ///
        /// Matched on the car rather than on "whoever is still riding". Two cars are often on
        /// the drive at once, and the one that parks first is not reliably the one that set off
        /// first - a visitor stepping out of somebody else's vehicle is the kind of thing that
        /// only ever happens once the car park is busy.
        /// </summary>
        void HandleParked(VisitorVehicle car)
        {
            car.OnParked -= HandleParked;

            int order = 0;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i] == null || active[i].Car != car) continue;

                active[i].GetOutOfCar(order++);
            }
        }

        /// <summary>The car is off the map. Pooling it is the last thing that happens in a
        /// visit, and it happens after the person, who left it several seconds earlier.</summary>
        void HandleDeparted(VisitorVehicle car)
        {
            car.OnDeparted -= HandleDeparted;

            car.ResetForPool();
            pool.Return(car);
        }

        /// <summary>
        /// The one place a visit turns into money.
        ///
        /// Paid into the level's own wallet, which is what the shop and the HUD read. There is
        /// no second ledger: a balance only this system knew about would be a balance the
        /// player cannot spend.
        /// </summary>
        void HandleServed(Visitor visitor, int payment)
        {
            if (payment <= 0) return;

            CurrenciesController.AddCurrency(CurrencyType.Coins, payment);
        }

        void HandleJourneyEnded(Visitor visitor) => finished.Add(visitor);

        /// <summary>
        /// Pools everyone who finished this frame.
        ///
        /// Deferred out of the event rather than done inside it, because the event fires from
        /// the visitor's own Update and returning the instance there would deactivate an object
        /// part way through its own frame.
        /// </summary>
        void Reap()
        {
            if (finished.Count == 0) return;

            for (int i = 0; i < finished.Count; i++) Retire(finished[i]);

            finished.Clear();
        }

        void Retire(Visitor visitor)
        {
            if (visitor == null) return;

            visitor.OnServed -= HandleServed;
            visitor.OnJourneyEnded -= HandleJourneyEnded;

            active.Remove(visitor);

            if (shuttle != null) shuttle.Remove(visitor);
            if (composition != null) composition.Danger.UnregisterCivilian(visitor);

            visitor.ResetForPool();
            pool.Return(visitor);
        }

        public void Configure(VisitorTrafficProfile traffic, VisitorRoads route, ParkingLot lot,
                              SeatRegistry registry, Transform pooled)
        {
            profile = traffic;
            roads = route;
            parking = lot;
            seats = registry;
            poolRoot = pooled;
        }
    }
}
