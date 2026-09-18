using UnityEngine;

namespace FoodStreet.Navigation
{
    /// <summary>
    /// Walks an A* path. Ask for a destination, get steered there.
    ///
    /// This is the replacement for NavMeshAgent, and deliberately answers to the same
    /// handful of members the game was already using — <see cref="SetDestination"/>,
    /// <see cref="Velocity"/>, <see cref="Speed"/>, <see cref="StoppingDistance"/>,
    /// and <c>enabled</c>. Disabling it parks the agent: the steering component goes down
    /// with it, so a tween can own the transform without being fought over.
    ///
    /// It holds no copy of the world. When the grid changes underneath it, it repaths.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SteeringAgent))]
    public sealed class PathFollower : MonoBehaviour
    {
        [Tooltip("How close to the final waypoint counts as arrived.")]
        [SerializeField] float stoppingDistance = 0.35f;

        [Tooltip("How close to an intermediate waypoint counts as passed. Wants to be " +
                 "comfortably under stoppingDistance or agents corner too tightly.")]
        [SerializeField] float waypointTolerance = 0.18f;

        [Tooltip("Slow into the last waypoint over this distance. Zero for a hard stop.")]
        [SerializeField] float arrivalSlowdown = 0.8f;

        [Tooltip("Seconds between retries when a destination could not be reached at all. " +
                 "Usually means something was placed in the way mid-walk.")]
        [SerializeField] float retryInterval = 0.5f;

        [Tooltip("Retries before the destination is abandoned. Arrival is reported on the " +
                 "first failure regardless, so callers are never blocked; this only bounds " +
                 "how long an abandoned agent keeps hoping.")]
        [SerializeField] int maxRetries = 3;

        public float StoppingDistance
        {
            get => stoppingDistance;
            set => stoppingDistance = value;
        }

        public Vector3 Velocity => steering != null ? steering.Velocity : Vector3.zero;

        public float Speed => steering != null ? steering.Speed : 1f;

        /// <summary>True once there is nothing left to walk to. Also true after a failed
        /// request, so callers waiting on arrival cannot be left hanging by an
        /// unreachable destination.</summary>
        public bool HasArrived { get; private set; } = true;

        /// <summary>The point last asked for, which is not always the point reached — a
        /// destination inside a table resolves to the nearest spot an agent fits.</summary>
        public Vector3 Destination { get; private set; }

        public bool HasPath => !HasArrived && waypointIndex < path.Waypoints.Count;

        SteeringAgent steering;
        readonly Path path = new Path();
        int waypointIndex;
        bool hasDestination;
        float retryTimer;
        int retriesLeft;

        void Awake()
        {
            steering = GetComponent<SteeringAgent>();
        }

        void OnEnable()
        {
            if (steering != null) steering.enabled = true;
        }

        void OnDisable()
        {
            if (steering != null)
            {
                steering.Halt();
                steering.enabled = false;
            }

            ClearPath();
        }

        /// <summary>Requests a route to <paramref name="world"/>. Recomputed immediately;
        /// there is no request queue to wait on.</summary>
        public void SetDestination(Vector3 world)
        {
            Destination = world;
            hasDestination = true;
            retryTimer = 0f;
            retriesLeft = maxRetries;
            RequestPath();
        }

        /// <summary>Drops the current route and stands still.</summary>
        public void Stop()
        {
            ClearPath();
            if (steering != null) steering.Halt();
        }

        public void Warp(Vector3 position)
        {
            if (steering != null) steering.Warp(position);
            else transform.position = position;

            if (hasDestination) RequestPath();
        }

        void ClearPath()
        {
            path.Reset();
            waypointIndex = 0;
            hasDestination = false;
            HasArrived = true;
        }

        void RequestPath()
        {
            NavigationSystem nav = NavigationSystem.Instance;
            if (nav == null || nav.Pathfinder == null)
            {
                // No navigation yet — head straight there rather than refusing to move.
                path.Reset();
                path.Waypoints.Add(Destination);
                path.Status = PathStatus.Partial;
                waypointIndex = 0;
                HasArrived = false;
                return;
            }

            float radius = steering != null ? steering.Radius : 0.3f;
            nav.Pathfinder.FindPath(transform.position, Destination, radius, path);

            waypointIndex = 0;
            HasArrived = !path.IsUsable;

            if (HasArrived)
            {
                // Nowhere to go. Retry on a timer in case the obstruction is temporary,
                // but report arrival now so callers are not blocked on it.
                retryTimer = retriesLeft > 0 ? retryInterval : 0f;
            }
        }

        void Update()
        {
            if (!hasDestination) return;

            // Someone built or sold something. The route being walked was computed against
            // the old world, so throw it away rather than trust it. Polled rather than
            // event-driven because agents are pooled and outlive their subscriptions.
            NavigationSystem nav = NavigationSystem.Instance;
            if (nav != null && nav.Grid != null && path.GridVersion != nav.Grid.Version)
            {
                RequestPath();
            }

            if (HasArrived)
            {
                if (retryTimer <= 0f)
                {
                    // Out of retries. Let go of the destination so an agent that has been
                    // written off does not suddenly set off again minutes later.
                    if (retriesLeft <= 0) hasDestination = false;
                    return;
                }

                retryTimer -= Time.deltaTime;
                if (retryTimer <= 0f)
                {
                    retriesLeft--;
                    RequestPath();
                }
                return;
            }

            if (waypointIndex >= path.Waypoints.Count)
            {
                Arrive();
                return;
            }

            Vector3 position = transform.position;
            Vector3 waypoint = path.Waypoints[waypointIndex];

            Vector3 offset = waypoint - position;
            offset.y = 0f;
            float distance = offset.magnitude;

            bool isLast = waypointIndex == path.Waypoints.Count - 1;
            float tolerance = isLast ? stoppingDistance : waypointTolerance;

            if (distance <= tolerance)
            {
                waypointIndex++;
                if (waypointIndex >= path.Waypoints.Count)
                {
                    Arrive();
                    return;
                }

                waypoint = path.Waypoints[waypointIndex];
                offset = waypoint - position;
                offset.y = 0f;
                distance = offset.magnitude;
                isLast = waypointIndex == path.Waypoints.Count - 1;
            }

            if (distance < 0.0001f) return;

            float scale = 1f;
            if (isLast && arrivalSlowdown > 0f)
            {
                scale = Mathf.Clamp(distance / arrivalSlowdown, 0.25f, 1f);
            }

            steering.SetDesiredDirection(offset / distance, scale);
        }

        void Arrive()
        {
            HasArrived = true;
            hasDestination = false;
            retryTimer = 0f;
            steering.Halt();
        }
    }
}
