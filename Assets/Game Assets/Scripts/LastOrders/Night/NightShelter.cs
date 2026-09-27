using System.Collections.Generic;
using LastOrders.Core;
using UnityEngine;

namespace LastOrders.Night
{
    /// <summary>
    /// The diner, as somewhere to be after dark: the way in through its door, and a spot inside
    /// for each person sheltering there.
    ///
    /// Workers walk straight lines, and the restaurant's walls are scene cubes nothing can path
    /// around. So the route in is worked out here from the building's footprint: straight to the
    /// door when nothing is in the way, otherwise round whichever corner keeps the walk outside
    /// the walls. The way back out in the morning is the same route reversed.
    ///
    /// It also tells the night's danger measurement where the people are. The door is where a
    /// zombie that gets past the guards ends up, and the door is what they are behind.
    /// </summary>
    [DefaultExecutionOrder(1120)]
    public sealed class NightShelter : MonoBehaviour
    {
        [SerializeField] GameLifetimeScope scope;

        [Tooltip("Just outside the door, where the walk in starts.")]
        [SerializeField] Transform doorOutside;

        [Tooltip("Just inside the doorway.")]
        [SerializeField] Transform doorInside;

        [Tooltip("Where people stand while they wait out the night. One person per spot; anyone " +
                 "past the last one shares the doorway.")]
        [SerializeField] Transform[] spots;

        [Tooltip("The building's footprint, world space. Routes in are kept outside it until the door.")]
        [SerializeField] Bounds footprint = new Bounds(new Vector3(19.85f, 1.5f, 21.95f), new Vector3(11.7f, 3f, 10.5f));

        [Tooltip("How far outside the footprint the corner waypoints sit.")]
        [SerializeField] float cornerMargin = 1.1f;

        readonly Dictionary<object, int> claims = new Dictionary<object, int>();

        GameComposition composition;

        public Vector3 DoorOutside => doorOutside != null ? doorOutside.position : transform.position;
        public Vector3 DoorInside => doorInside != null ? doorInside.position : DoorOutside;

        /// <summary>The building's footprint, world space, as routes round it see it.</summary>
        public Bounds Footprint => footprint;

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                Debug.LogError("[NightShelter] No GameLifetimeScope, so the night cannot tell where " +
                               "the people are.", this);
                return;
            }

            composition.Danger.SetShelterPoint(DoorOutside);
        }

        /// <summary>
        /// The building grew, or its door moved with the front wall: routes keep outside the new
        /// footprint, and the night measures from wherever the door now is.
        /// </summary>
        public void Refit(Bounds building)
        {
            footprint = building;
            if (composition != null) composition.Danger.SetShelterPoint(DoorOutside);
        }

        // --- Spots ----------------------------------------------------------------------------

        /// <summary>A spot inside for <paramref name="who"/>. The same one every time they ask,
        /// until they release it.</summary>
        public Vector3 Claim(object who)
        {
            if (who == null || spots == null || spots.Length == 0) return DoorInside;

            if (claims.TryGetValue(who, out int held)) return SpotAt(held);

            for (int i = 0; i < spots.Length; i++)
            {
                if (claims.ContainsValue(i)) continue;

                claims[who] = i;
                return SpotAt(i);
            }

            // Everyone has a spot and one more has turned up: the doorway is still inside.
            return DoorInside;
        }

        public void Release(object who)
        {
            if (who != null) claims.Remove(who);
        }

        Vector3 SpotAt(int index) => spots[index] != null ? spots[index].position : DoorInside;

        // --- Routes ---------------------------------------------------------------------------

        /// <summary>From <paramref name="from"/> to the spot, through the door, never through a wall.</summary>
        public List<Vector3> RouteIn(Vector3 from, Vector3 spot)
        {
            var route = new List<Vector3>(6);
            AppendApproach(from, route);

            route.Add(DoorOutside);
            route.Add(DoorInside);
            route.Add(spot);
            return route;
        }

        /// <summary>From the spot back out to <paramref name="to"/>: the way in, walked backwards.</summary>
        public List<Vector3> RouteOut(Vector3 spot, Vector3 to)
        {
            var route = new List<Vector3>(6) { DoorInside, DoorOutside };

            var approach = new List<Vector3>(3);
            AppendApproach(to, approach);

            for (int i = approach.Count - 1; i >= 0; i--) route.Add(approach[i]);

            route.Add(to);
            return route;
        }

        /// <summary>
        /// The corners to walk round on the way from a point to the door. None when the straight
        /// walk is clear; otherwise the shortest of going round the left or the right, taking one
        /// corner or two.
        /// </summary>
        void AppendApproach(Vector3 from, List<Vector3> into)
        {
            Vector3 door = DoorOutside;
            if (!Blocked(from, door)) return;

            Bounds wide = footprint;
            wide.Expand(new Vector3(cornerMargin * 2f, 0f, cornerMargin * 2f));

            // Corners on the door's own face first, then the far ones: the door is on one face,
            // and going round means reaching a corner of that face.
            Vector3 min = wide.min;
            Vector3 max = wide.max;
            bool doorSouth = door.z <= footprint.center.z;

            Vector3 nearLeft = new Vector3(min.x, door.y, doorSouth ? min.z : max.z);
            Vector3 nearRight = new Vector3(max.x, door.y, doorSouth ? min.z : max.z);
            Vector3 farLeft = new Vector3(min.x, door.y, doorSouth ? max.z : min.z);
            Vector3 farRight = new Vector3(max.x, door.y, doorSouth ? max.z : min.z);

            List<Vector3> best = null;
            float bestLength = float.MaxValue;

            Consider(from, door, ref best, ref bestLength, nearLeft);
            Consider(from, door, ref best, ref bestLength, nearRight);
            Consider(from, door, ref best, ref bestLength, farLeft, nearLeft);
            Consider(from, door, ref best, ref bestLength, farRight, nearRight);

            if (best != null) into.AddRange(best);
        }

        /// <summary>
        /// The corners to walk round so a straight walk between two points never crosses the
        /// building - for anyone crossing the compound past it, not only going in. None when the
        /// straight walk is clear, or when <paramref name="from"/> is inside the building already.
        /// </summary>
        public void AppendDetour(Vector3 from, Vector3 to, List<Vector3> into)
        {
            if (!Blocked(from, to)) return;

            Bounds wide = footprint;
            wide.Expand(new Vector3(cornerMargin * 2f, 0f, cornerMargin * 2f));

            Vector3 min = wide.min;
            Vector3 max = wide.max;

            // Round the footprint in order, so neighbours in the array share a side.
            Vector3[] corners =
            {
                new Vector3(min.x, from.y, min.z), new Vector3(max.x, from.y, min.z),
                new Vector3(max.x, from.y, max.z), new Vector3(min.x, from.y, max.z),
            };

            List<Vector3> best = null;
            float bestLength = float.MaxValue;

            for (int i = 0; i < corners.Length; i++)
            {
                Consider(from, to, ref best, ref bestLength, corners[i]);
                Consider(from, to, ref best, ref bestLength, corners[i], corners[(i + 1) % corners.Length]);
                Consider(from, to, ref best, ref bestLength, corners[i], corners[(i + 3) % corners.Length]);
            }

            if (best != null) into.AddRange(best);
        }

        void Consider(Vector3 from, Vector3 door, ref List<Vector3> best, ref float bestLength, params Vector3[] corners)
        {
            Vector3 previous = from;
            float length = 0f;

            for (int i = 0; i < corners.Length; i++)
            {
                if (Blocked(previous, corners[i])) return;

                length += Vector3.Distance(previous, corners[i]);
                previous = corners[i];
            }

            if (Blocked(previous, door)) return;

            length += Vector3.Distance(previous, door);
            if (length >= bestLength) return;

            bestLength = length;
            best = new List<Vector3>(corners);
        }

        /// <summary>True when the flat line between two points crosses the building.</summary>
        bool Blocked(Vector3 a, Vector3 b)
        {
            Vector3 flatA = new Vector3(a.x, footprint.center.y, a.z);
            Vector3 flatB = new Vector3(b.x, footprint.center.y, b.z);
            Vector3 delta = flatB - flatA;

            float length = delta.magnitude;
            if (length < 1e-3f) return footprint.Contains(flatA);

            return footprint.IntersectRay(new Ray(flatA, delta / length), out float distance) && distance <= length;
        }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(GameLifetimeScope lifetimeScope, Transform outside, Transform inside,
                              Transform[] standingSpots, Bounds building)
        {
            scope = lifetimeScope;
            doorOutside = outside;
            doorInside = inside;
            spots = standingSpots;
            footprint = building;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireCube(footprint.center, footprint.size);

            if (spots == null) return;

            Gizmos.color = new Color(1f, 0.85f, 0.3f);
            foreach (Transform spot in spots)
                if (spot != null) Gizmos.DrawWireSphere(spot.position, 0.3f);
        }
    }
}
