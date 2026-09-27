using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// The shuttle that takes visitors home from the hospital tent.
    ///
    /// A visitor hurt on a bad night does not go home in their own car - that went home the
    /// next morning - so once they are well they wait beside the tent for this. It runs by day
    /// only, like every other vehicle on the road, and it comes a while after somebody starts
    /// waiting rather than the instant they do: "a later shuttle", not a taxi they summoned.
    ///
    /// It stops on the road by the gate rather than turning in. That keeps it off the drive and
    /// out of the car park, whose bays belong to paying customers, and it makes the leaving
    /// something the player sees: out through the gate, up the step, gone.
    /// </summary>
    [DefaultExecutionOrder(45)]
    public sealed class VisitorShuttle : MonoBehaviour
    {
        enum State { Idle, Due, Arriving, Boarding, Leaving }

        [SerializeField] VehicleDefinition vehicle;
        [SerializeField] VisitorRoads roads;

        [Tooltip("Where the shuttle stops, on the road by the gate. Forward is the way it drives.")]
        [SerializeField] Transform stop;

        [Tooltip("Points to walk round on the way from the waiting spot to the gate - a guard " +
                 "tower in the way, say. The gate itself comes from the road and is added after these.")]
        [SerializeField] Transform[] walkToStop;

        [Tooltip("Seconds between somebody starting to wait and the shuttle arriving, shortest to longest.")]
        [SerializeField] Vector2 delaySeconds = new Vector2(12f, 20f);

        [Tooltip("Longest the shuttle waits at the stop before it leaves with whoever has boarded.")]
        [SerializeField] float boardingTimeout = 30f;

        [Tooltip("Metres from its side where passengers step aboard.")]
        [SerializeField] float doorGap = 0.6f;

        readonly List<Visitor> waiting = new List<Visitor>();
        readonly List<Visitor> boarding = new List<Visitor>();

        VisitorVehicle bus;
        State state = State.Idle;
        float timer;
        Func<bool> running;

        /// <summary>True when the scene gives the shuttle a vehicle, a road and a stop.</summary>
        public bool IsConfigured =>
            vehicle != null && vehicle.prefab != null && roads != null && roads.IsComplete && stop != null;

        /// <summary>Everyone waiting or on their way to the bus. What the next waiting spot is counted from.</summary>
        public int WaitingCount => waiting.Count + boarding.Count;

        /// <summary>Whether the shuttle may run right now. By day only; the visitor manager says when that is.</summary>
        public void SetSchedule(Func<bool> isRunning) => running = isRunning;

        bool CanRun => running == null || running();

        /// <summary>Somebody is well and waiting to go home.</summary>
        public void Enqueue(Visitor visitor)
        {
            if (visitor != null && !waiting.Contains(visitor) && !boarding.Contains(visitor)) waiting.Add(visitor);
        }

        public void Remove(Visitor visitor)
        {
            waiting.Remove(visitor);
            boarding.Remove(visitor);
        }

        void Update()
        {
            switch (state)
            {
                case State.Idle:
                    if (waiting.Count > 0 && CanRun)
                    {
                        timer = Random.Range(delaySeconds.x, delaySeconds.y);
                        state = State.Due;
                    }
                    break;

                case State.Due:
                    // The clock only runs while the road is open. A shuttle due at dusk comes
                    // in the morning, not through the night.
                    if (!CanRun) break;

                    timer -= Time.deltaTime;
                    if (timer <= 0f) Dispatch();
                    break;

                case State.Boarding:
                    SendWhoeverIsReady();

                    timer -= Time.deltaTime;

                    bool everyoneAboard = boarding.Count == 0 && !AnyoneStillComing();
                    if (everyoneAboard || timer <= 0f) Depart();
                    break;
            }
        }

        // --- The run -------------------------------------------------------------------------

        void Dispatch()
        {
            if (!IsConfigured)
            {
                Debug.LogError("[Visitors] The shuttle has no vehicle, road or stop, so nobody can leave the " +
                               "hospital tent. Run 'Last Orders / Hospital & Zombies / Set Up (Current Scene)'.", this);
                state = State.Idle;
                enabled = false;
                return;
            }

            if (bus == null)
            {
                GameObject instance = Instantiate(vehicle.prefab, transform);
                instance.name = "Shuttle";
                bus = instance.GetComponent<VisitorVehicle>();
            }

            bus.gameObject.SetActive(true);
            bus.ResetForPool();
            bus.Configure(vehicle);
            bus.OnParked += HandleArrived;

            Vector3 start = OnLane(roads.RoadStart);
            Vector3 approach = stop.position - stop.forward * 12f;

            bus.DriveAlong(new[] { start, approach, stop.position }, stop.forward, slowLegsAtEnd: 1);
            state = State.Arriving;
        }

        void HandleArrived(VisitorVehicle vehicle)
        {
            vehicle.OnParked -= HandleArrived;

            state = State.Boarding;
            timer = boardingTimeout;

            SendWhoeverIsReady();
        }

        /// <summary>Everyone at the waiting spot walks out to the bus. Anyone still on their way
        /// to the spot gets the same once they are there, while the bus is still waiting.</summary>
        void SendWhoeverIsReady()
        {
            for (int i = waiting.Count - 1; i >= 0; i--)
            {
                Visitor visitor = waiting[i];

                if (visitor == null || visitor.Stage == VisitorStage.Gone)
                {
                    waiting.RemoveAt(i);
                    continue;
                }

                if (visitor.Stage != VisitorStage.WaitingForShuttle) continue;

                waiting.RemoveAt(i);
                boarding.Add(visitor);
                visitor.LeaveOnFoot(WalkToDoor());
            }

            // Stepped aboard: they are off the list.
            boarding.RemoveAll(v => v == null || v.Stage == VisitorStage.Gone);
        }

        bool AnyoneStillComing()
        {
            for (int i = 0; i < waiting.Count; i++)
                if (waiting[i] != null && waiting[i].Stage == VisitorStage.ToShuttle) return true;

            return false;
        }

        void Depart()
        {
            state = State.Leaving;

            bus.OnDeparted += HandleGone;
            bus.DriveAway(new[] { OnLane(roads.RoadEnd) });
        }

        void HandleGone(VisitorVehicle vehicle)
        {
            vehicle.OnDeparted -= HandleGone;

            vehicle.ResetForPool();
            vehicle.gameObject.SetActive(false);

            // Anyone who missed it waits for the next.
            state = State.Idle;
        }

        // --- Where ---------------------------------------------------------------------------

        /// <summary>A point of the road moved sideways onto the shuttle's own lane.</summary>
        Vector3 OnLane(Vector3 point) => stop.position + stop.forward * Vector3.Dot(point - stop.position, stop.forward);

        /// <summary>Out round the tower, through the gate, to the step on the side facing the gate.</summary>
        List<Vector3> WalkToDoor()
        {
            var path = new List<Vector3>(6);

            if (walkToStop != null)
            {
                for (int i = 0; i < walkToStop.Length; i++)
                    if (walkToStop[i] != null) path.Add(walkToStop[i].position);
            }

            // The gate is the road's, not a copy of it: a gate moved on the road moves the walk too.
            path.Add(roads.GateInside);
            path.Add(roads.GateOutside);

            float side = Mathf.Sign(Vector3.Dot(roads.GateOutside - stop.position, stop.right));
            float halfWidth = vehicle != null ? vehicle.bodySize.x * 0.5f : 1f;

            path.Add(stop.position + stop.right * side * (halfWidth + doorGap));
            return path;
        }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(VehicleDefinition shuttle, VisitorRoads route, Transform stopPoint, Transform[] walk)
        {
            vehicle = shuttle;
            roads = route;
            stop = stopPoint;
            walkToStop = walk;
        }

        void OnDrawGizmosSelected()
        {
            if (stop == null) return;

            Gizmos.color = new Color(1f, 0.8f, 0.2f);
            Gizmos.DrawWireCube(stop.position + Vector3.up * 0.8f, new Vector3(2.2f, 1.6f, 2.2f));
            Gizmos.DrawLine(stop.position, stop.position + stop.forward * 3f);
        }
    }
}
