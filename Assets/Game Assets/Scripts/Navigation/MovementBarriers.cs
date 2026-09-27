using System.Collections.Generic;
using UnityEngine;

namespace FoodStreet.Navigation
{
    /// <summary>Who is trying to get past a barrier. A gate treats the two very differently.</summary>
    public enum MoverKind
    {
        /// <summary>Staff, visitors and their cars. Walk authored routes and wait at a shut gate.</summary>
        Friendly,

        /// <summary>The night's zombies. Stopped by anything standing, and try to break it.</summary>
        Hostile,
    }

    /// <summary>
    /// Something solid that is not on the build grid - the compound's outer wall and its gates.
    ///
    /// The grid only knows about placed objects, and the outer wall is not one: it runs along
    /// the edge of the plot and partly off it. So walls and gates answer for themselves, in
    /// world space, and both movers ask them: the steering layer for the zombies, the waypoint
    /// walker for everyone else.
    /// </summary>
    public interface IMovementBarrier
    {
        /// <summary>
        /// True when a step from <paramref name="from"/> to <paramref name="to"/> would take a
        /// body of <paramref name="radius"/> into this barrier. A step that leaves it, or keeps
        /// the same depth, is never refused, so nothing is ever trapped inside one.
        /// </summary>
        bool Blocks(Vector3 from, Vector3 to, float radius, MoverKind mover);
    }

    /// <summary>A barrier that opens: told when somebody's route runs through it.</summary>
    public interface IPassageGate : IMovementBarrier
    {
        /// <summary>A friendly mover at <paramref name="from"/> is walking toward
        /// <paramref name="to"/>. The gate opens when that leg runs through it within
        /// <paramref name="reach"/> metres of <paramref name="from"/>.</summary>
        void RequestPassage(Vector3 from, Vector3 to, float reach);
    }

    /// <summary>
    /// Every barrier currently standing in the world. Walls and gates add themselves while they
    /// are active; movers ask here rather than each finding them.
    /// </summary>
    public static class MovementBarriers
    {
        static readonly List<IMovementBarrier> barriers = new List<IMovementBarrier>();
        static readonly List<IPassageGate> gates = new List<IPassageGate>();

        public static int Count => barriers.Count;

        public static void Register(IMovementBarrier barrier)
        {
            if (barrier == null || barriers.Contains(barrier)) return;

            barriers.Add(barrier);
            if (barrier is IPassageGate gate) gates.Add(gate);
        }

        public static void Unregister(IMovementBarrier barrier)
        {
            if (barrier == null) return;

            barriers.Remove(barrier);
            if (barrier is IPassageGate gate) gates.Remove(gate);
        }

        /// <summary>The first barrier refusing this step, or null when it is clear.</summary>
        public static IMovementBarrier FindBlocking(Vector3 from, Vector3 to, float radius, MoverKind mover)
        {
            for (int i = 0; i < barriers.Count; i++)
            {
                IMovementBarrier barrier = barriers[i];
                if (barrier is Object o && o == null) continue;

                if (barrier.Blocks(from, to, radius, mover)) return barrier;
            }

            return null;
        }

        /// <summary>Tells every gate a friendly mover is heading from one point to another, so the
        /// one on that line opens before they reach it. Only a crossing within
        /// <paramref name="reach"/> metres counts: a gate forty metres down a long leg has no
        /// business opening yet.</summary>
        public static void RequestPassage(Vector3 from, Vector3 to, float reach)
        {
            for (int i = 0; i < gates.Count; i++)
            {
                IPassageGate gate = gates[i];
                if (gate is Object o && o == null) continue;

                gate.RequestPassage(from, to, reach);
            }
        }

        /// <summary>Forgets everything. For tests, and for a scene torn down with domain reload off.</summary>
        public static void Clear()
        {
            barriers.Clear();
            gates.Clear();
        }

        // --- Geometry shared by the barriers ------------------------------------------------------

        /// <summary>
        /// How far <paramref name="world"/> is inside a flat box, grown by <paramref name="radius"/>.
        /// Positive inside, negative outside. The box is <paramref name="centre"/>, turned by
        /// <paramref name="yaw"/> degrees, with half-sizes along its own X and Z.
        /// </summary>
        public static float Penetration(Vector3 world, Vector3 centre, float yaw, Vector2 halfSize, float radius)
        {
            Vector3 local = Quaternion.Euler(0f, -yaw, 0f) * (world - centre);

            float x = halfSize.x + radius - Mathf.Abs(local.x);
            float z = halfSize.y + radius - Mathf.Abs(local.z);

            return Mathf.Min(x, z);
        }

        /// <summary>
        /// True when the flat line from <paramref name="from"/> to <paramref name="to"/> passes
        /// through an opening - the local X axis of a frame at <paramref name="centre"/> turned by
        /// <paramref name="yaw"/>, <paramref name="halfWidth"/> either side - no further than
        /// <paramref name="reach"/> metres from <paramref name="from"/>.
        /// </summary>
        public static bool CrossesOpening(Vector3 from, Vector3 to, Vector3 centre, float yaw, float halfWidth, float reach)
        {
            Quaternion inverse = Quaternion.Euler(0f, -yaw, 0f);
            Vector3 a = inverse * (from - centre);
            Vector3 b = inverse * (to - centre);

            // Both on the same side of the line: it never crosses.
            if (a.z * b.z > 0f) return false;

            float dz = a.z - b.z;
            if (Mathf.Abs(dz) < 1e-5f) return false;

            float t = a.z / dz;
            float x = a.x + (b.x - a.x) * t;
            if (Mathf.Abs(x) > halfWidth) return false;

            float distance = t * new Vector2(b.x - a.x, b.z - a.z).magnitude;
            return distance <= reach;
        }

        /// <summary>
        /// The rule every solid barrier uses: a step is refused when it ends inside the grown box
        /// and deeper than it started. Walking out, or along, is always allowed.
        /// </summary>
        public static bool StepEnters(Vector3 from, Vector3 to, Vector3 centre, float yaw, Vector2 halfSize, float radius)
        {
            float after = Penetration(to, centre, yaw, halfSize, radius);
            if (after <= 0f) return false;

            float before = Penetration(from, centre, yaw, halfSize, radius);
            return after > before + 1e-4f;
        }
    }
}
