using System.Collections.Generic;
using FoodStreet.Navigation;
using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// Moves a transform along a short list of authored points.
    ///
    /// Shared by the people and the cars, so a visitor stepping out of a car that stopped
    /// somewhere else is impossible: both read the same points and arrive by the same test.
    ///
    /// Deliberately NOT PathFollower: a car steered by A* with avoidance wanders on the straight of
    /// a road, because the grid has no notion of a lane, and a handful of authored points - the
    /// gate, the door, the chair - do the job exactly and fail visibly when they are wrong.
    ///
    /// The one thing it does respect is a gate. Every step tells the gates where it is heading,
    /// so the one on its line opens in time, and a step into a gate that is still shut is not
    /// taken - the car or the person waits at it until it has opened.
    ///
    /// People also go round things: with a <see cref="GridRadius"/>, each leg between two points is
    /// walked as the turns of a route round whatever stands on the nav grid - other diners' tables
    /// and chairs, the counters, the walls - found once per leg (<see cref="GridRoute"/>). The points
    /// themselves, and <see cref="Remaining"/>, are unchanged by it. Cars leave it at 0 and keep to
    /// their lanes.
    /// </summary>
    public sealed class WaypointWalker
    {
        /// <summary>How close to a turn counts as having made it.</summary>
        const float TurnRadius = 0.15f;

        readonly List<Vector3> points = new List<Vector3>();
        readonly List<Vector3> turns = new List<Vector3>();

        int next;
        int turn, turnsFor = -1, turnsVersion = -1;

        /// <summary>Metres of body kept clear of what stands on the nav grid; 0 walks each leg straight.</summary>
        public float GridRadius { get; set; }

        /// <summary>How far the mover reaches ahead of its own position, in metres: half a car's
        /// length, nothing for a person. A shut gate holds them back by this much more, so a car's
        /// bonnet stops short of the leaves rather than through them.</summary>
        public float BodyRadius { get; set; }

        /// <summary>True once the last point has been reached. Also true before any path is
        /// set, so a caller that forgets to set one stalls rather than walking to the origin.</summary>
        public bool Arrived => next >= points.Count;

        public Vector3 Target => next < points.Count ? points[next] : Vector3.zero;

        public int Remaining => Mathf.Max(0, points.Count - next);

        /// <summary>
        /// Starts a new path.
        ///
        /// Null entries are dropped rather than walked to. A missing route node is a point at
        /// the world origin, and a visitor crossing the map to stand on it is a bug that looks
        /// like a different bug.
        /// </summary>
        public void Follow(IReadOnlyList<Vector3> path)
        {
            points.Clear();
            next = 0;
            turnsFor = -1;

            if (path == null) return;

            for (int i = 0; i < path.Count; i++)
            {
                // Consecutive duplicates would each need an arrival test to clear, which reads
                // as a pause for no reason at a point nobody placed twice on purpose.
                if (points.Count > 0 && (points[points.Count - 1] - path[i]).sqrMagnitude < 1e-4f)
                    continue;

                points.Add(path[i]);
            }
        }

        public void Clear()
        {
            points.Clear();
            next = 0;
            turnsFor = -1;
        }

        /// <summary>
        /// One step. Returns true on the frame the last point is reached.
        ///
        /// Height is carried from the mover rather than from the path, so a route node dropped
        /// slightly above the ground does not make everyone walk uphill.
        /// </summary>
        public bool Advance(Transform mover, float speed, float turnSpeed, float arriveRadius,
                            float deltaSeconds)
        {
            if (mover == null || Arrived) return false;

            Vector3 here = mover.position;
            Vector3 point = points[next];
            point.y = here.y;

            // Round what is in the way: the next turn of this leg, until only the point is left.
            Vector3 via = point;
            bool turning = GridRadius > 0f && NextTurn(here, out via);
            Vector3 goal = turning ? via : point;
            goal.y = here.y;

            Vector3 offset = goal - here;
            float distance = offset.magnitude;

            if (turning && distance <= TurnRadius)
            {
                turn++;
                return false;
            }

            if (!turning && distance <= arriveRadius)
            {
                next++;
                return Arrived;
            }

            Vector3 direction = offset / distance;
            Vector3 step = here + direction * Mathf.Min(speed * deltaSeconds, distance);

            // Asked every step rather than once per leg: a gate that is locked shut while a
            // zombie is at it opens the moment it is gone, and whoever is waiting has to be
            // asking then.
            RequestGatesAhead(here, point);

            if (MovementBarriers.FindBlocking(here, step, BodyRadius, MoverKind.Friendly) == null)
                mover.position = step;

            if (turnSpeed > 0f)
            {
                mover.rotation = Quaternion.RotateTowards(
                    mover.rotation, Quaternion.LookRotation(direction, Vector3.up),
                    turnSpeed * deltaSeconds);
            }

            return false;
        }

        /// <summary>
        /// Tells the gates about the next <see cref="GateLookAhead"/> metres of the route, not
        /// only the leg being walked: a car leaving turns onto the leg through the gate a few
        /// metres from it, and a gate asked only then would still be shut when the car arrived.
        /// </summary>
        void RequestGatesAhead(Vector3 here, Vector3 goal)
        {
            if (MovementBarriers.Count == 0) return;

            float budget = GateLookAhead;
            Vector3 from = here;
            Vector3 to = goal;
            int index = next;

            while (true)
            {
                MovementBarriers.RequestPassage(from, to, budget);

                budget -= Vector3.Distance(from, to);
                index++;
                if (budget <= 0f || index >= points.Count) return;

                from = to;
                to = points[index];
                to.y = here.y;
            }
        }

        /// <summary>Metres of route ahead a gate is told about. Far enough that a car at road speed
        /// finds it fully open, and a person just finds it open a little early.</summary>
        const float GateLookAhead = 16f;

        /// <summary>
        /// The next turn on the way to the current point, planned when the leg starts and again if the
        /// grid changes under it (something built, or sold). False once only the point itself is left,
        /// or when the leg is walked straight.
        /// </summary>
        bool NextTurn(Vector3 here, out Vector3 via)
        {
            via = default;

            int version = GridRoute.Version;
            if (turnsFor != next || turnsVersion != version)
            {
                turnsFor = next;
                turnsVersion = version;
                turn = 0;
                if (!GridRoute.Plan(here, points[next], GridRadius, turns)) turns.Clear();
            }

            // The last entry is the point itself: walked to, and arrived at, as any point is.
            if (turn >= turns.Count - 1) return false;

            via = turns[turn];
            return true;
        }

        /// <summary>Faces a direction immediately. Used on arrival, where a seat or a bay has a
        /// facing of its own that the approach does not.</summary>
        public static void Face(Transform mover, Vector3 forward)
        {
            forward.y = 0f;
            if (mover == null || forward.sqrMagnitude < 1e-4f) return;

            mover.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }
}
