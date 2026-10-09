using System;
using System.Collections.Generic;
using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// The car a visitor arrives and leaves in.
    ///
    /// Two jobs and nothing else: drive the route it is handed, and say when it has finished.
    /// It knows about a bay and a road; it does not know who is inside it, what they ordered or
    /// whether the restaurant is open. Everything that decides those things lives in
    /// <see cref="VisitorManager"/>, which is what lets a car be re-used by a different kind of
    /// visitor tomorrow.
    ///
    /// The drive in and the drive out run at different speeds. A car does not approach its bay
    /// at road speed, and the change of pace as it turns off the street is most of what makes
    /// the arrival read as parking rather than as a dolly move.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class VisitorVehicle : MonoBehaviour
    {
        public enum VehicleStage
        {
            Idle,
            DrivingIn,
            Parked,
            DrivingOut,
            Gone,
        }

        [SerializeField] VehicleDefinition definition;

        [Tooltip("Metres along the route from the bay at which the car is slow rather than " +
                 "quick. Below this it is manoeuvring; above it, driving.")]
        [SerializeField] int manoeuvreLegs = 2;

        [Tooltip("Seconds a parked car sits before it moves off once told to leave: long enough " +
                 "for the door to shut and the engine to catch.")]
        [SerializeField, Min(0f)] float pullAwaySeconds = 1.3f;

        readonly WaypointWalker walker = new WaypointWalker();

        /// <summary>Time left before a car pulling out of a bay starts to move.</summary>
        float startingFor;

        ParkingLot lot;
        ParkingSlot bay;
        VisitorRoads roads;

        /// <summary>Legs at the end of the current route driven at manoeuvring speed.</summary>
        int slowLegs;

        /// <summary>Which way to face on stopping somewhere that is not a bay.</summary>
        Vector3 stopForward;

        public VehicleStage Stage { get; private set; } = VehicleStage.Idle;

        public VehicleDefinition Definition => definition;

        /// <summary>The bay this car holds, or null once it has pulled away.</summary>
        public ParkingSlot Bay => bay;

        /// <summary>Told to leave from a standstill and not moving yet: the door is shutting and
        /// the engine starting. What the car's animation plays its start-up to.</summary>
        public bool IsStartingUp => startingFor > 0f;

        public float PullAwaySeconds => pullAwaySeconds;

        /// <summary>Raised the frame the car comes to rest in its bay. The visitor gets out on
        /// this, rather than on a timer that has to be kept in step with the drive.</summary>
        public event Action<VisitorVehicle> OnParked;

        /// <summary>Raised when the car is off the map and can be pooled.</summary>
        public event Action<VisitorVehicle> OnDeparted;

        /// <summary>
        /// Sends the car in.
        ///
        /// The bay is expected to be reserved already. Reserving it here would leave a window
        /// between the manager choosing a bay and the car claiming it, and one frame is enough
        /// for two cars to be sent to the same one.
        /// </summary>
        public void DriveIn(VisitorRoads route, ParkingLot parking, ParkingSlot slot)
        {
            roads = route;
            lot = parking;
            bay = slot;

            if (roads == null || bay == null)
            {
                Debug.LogError($"[Visitors] '{name}' was sent to drive with no route or no bay.",
                               this);
                Stage = VehicleStage.Gone;
                OnDeparted?.Invoke(this);
                return;
            }

            IReadOnlyList<Vector3> path = roads.DriveIn(lot, bay);

            Begin(path, manoeuvreLegs);
        }

        /// <summary>
        /// Drives a route that ends somewhere other than a bay - the shuttle's stop on the road -
        /// and stops there facing <paramref name="facing"/>. Raises <see cref="OnParked"/> on arrival
        /// like any other car.
        /// </summary>
        public void DriveAlong(IReadOnlyList<Vector3> path, Vector3 facing, int slowLegsAtEnd = 1)
        {
            ReleaseBay();

            if (path == null || path.Count == 0)
            {
                Stage = VehicleStage.Gone;
                OnDeparted?.Invoke(this);
                return;
            }

            stopForward = facing;
            Begin(path, slowLegsAtEnd);
        }

        void Begin(IReadOnlyList<Vector3> path, int slow)
        {
            transform.position = path[0];
            WaypointWalker.Face(transform, path.Count > 1 ? path[1] - path[0] : transform.forward);

            slowLegs = slow;
            walker.Follow(path);
            Stage = VehicleStage.DrivingIn;
        }

        /// <summary>Sends the car home. Its bay is released here rather than when it is off the
        /// map, so the space is sellable again the moment it is physically free.</summary>
        public void DriveOut()
        {
            if (roads == null)
            {
                Stage = VehicleStage.Gone;
                OnDeparted?.Invoke(this);
                return;
            }

            DriveAway(roads.DriveOut(lot), manoeuvreLegs);
        }

        /// <summary>Leaves along a route of the caller's choosing, releasing any bay first.</summary>
        public void DriveAway(IReadOnlyList<Vector3> path, int slowLegsAtEnd = 0)
        {
            ReleaseBay();

            // Only from a standstill: a car turned away on the drive is already rolling.
            startingFor = Stage == VehicleStage.Parked ? pullAwaySeconds : 0f;

            slowLegs = slowLegsAtEnd;
            walker.Follow(path);
            Stage = VehicleStage.DrivingOut;
        }

        /// <summary>
        /// Turns round before arriving: the restaurant closed while this car was on its way in.
        ///
        /// Where it goes depends on how far it got. Still on the road, it simply drives on past;
        /// turning in at the gate, it backs out onto the road; already inside, it comes round and
        /// out through the gate. Nobody is made to park just to leave again.
        /// </summary>
        public void TurnAway(VisitorRoads route)
        {
            if (Stage != VehicleStage.DrivingIn || route == null) return;

            // The way in is road start, gate outside, gate inside, the apron, the bay. What is
            // left of it says which of those legs the car is on.
            int remaining = walker.Remaining;
            var path = new List<Vector3>(3);

            if (remaining <= 2) path.Add(route.GateInside);
            if (remaining <= 3) path.Add(route.GateOutside);
            path.Add(route.RoadEnd);

            DriveAway(path);
        }

        void ReleaseBay()
        {
            if (bay == null) return;

            bay.Release();
            bay = null;
        }

        void Update()
        {
            if (Stage != VehicleStage.DrivingIn && Stage != VehicleStage.DrivingOut) return;

            if (startingFor > 0f)
            {
                startingFor -= Time.deltaTime;
                return;
            }

            float speed = walker.Remaining <= slowLegs ? ManoeuvreSpeed : DriveSpeed;

            // Half the body's length: how far the bonnet is ahead of the middle of the car, which
            // is what a shut gate has to hold back.
            walker.BodyRadius = BodyReach;

            if (!walker.Advance(transform, speed, TurnSpeed, ArriveRadius, Time.deltaTime)) return;

            if (Stage == VehicleStage.DrivingIn)
            {
                // Snapped to the bay's own pose on arrival. Waypoint following gets a car close
                // enough to look right while it is moving, and visibly crooked once it stops.
                if (bay != null)
                {
                    transform.position = bay.ParkPosition;
                    WaypointWalker.Face(transform, bay.ParkForward);
                }
                else
                {
                    WaypointWalker.Face(transform, stopForward);
                }

                Stage = VehicleStage.Parked;
                OnParked?.Invoke(this);
                return;
            }

            Stage = VehicleStage.Gone;
            OnDeparted?.Invoke(this);
        }

        /// <summary>Puts the car back in its starting state, for the pool.</summary>
        public void ResetForPool()
        {
            walker.Clear();
            ReleaseBay();

            lot = null;
            roads = null;
            slowLegs = manoeuvreLegs;
            startingFor = 0f;
            stopForward = Vector3.zero;
            Stage = VehicleStage.Idle;

            OnParked = null;
            OnDeparted = null;
        }

        public void Configure(VehicleDefinition vehicle) => definition = vehicle;

        float DriveSpeed => definition != null ? definition.driveSpeed : 6f;
        float ManoeuvreSpeed => definition != null ? definition.manoeuvreSpeed : 2.2f;
        float TurnSpeed => definition != null ? definition.turnSpeed : 180f;
        float ArriveRadius => definition != null ? definition.arriveRadius : 0.6f;
        float BodyReach => definition != null ? definition.bodySize.z * 0.5f : 2.2f;
    }
}
