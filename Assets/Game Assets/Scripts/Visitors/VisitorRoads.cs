using System.Collections.Generic;
using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// The route, as the cars and the people on it understand it.
    ///
    /// Six nodes, named by their ROLE rather than by a compass direction, so the same code
    /// drives a street running north to south and one running east to west. Every leg below is
    /// assembled from them, which means a re-dressed street - a wider gate, a drive on the
    /// other side - is a matter of dragging transforms and nothing else.
    ///
    /// The two ends matter most and are easiest to get wrong. Nobody may be seen appearing, so
    /// <see cref="roadStart"/> and <see cref="roadEnd"/> belong well outside the frame, behind
    /// whatever the level has between them and the camera. The metres of road between there and
    /// the gate are what turn a spawn into an arrival.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class VisitorRoads : MonoBehaviour
    {
        [Header("The road")]
        [Tooltip("Where cars appear. Off camera, or behind scenery, or both.")]
        [SerializeField] Transform roadStart;

        [Tooltip("Where cars leave. Off camera, at the other end.")]
        [SerializeField] Transform roadEnd;

        [Header("The gate")]
        [Tooltip("On the road, square with the gate opening. Turning in starts here.")]
        [SerializeField] Transform gateOutside;

        [Tooltip("Just inside the boundary, on the drive.")]
        [SerializeField] Transform gateInside;

        [Header("The door")]
        [Tooltip("Outside the restaurant door, where the path from the car park meets it.")]
        [SerializeField] Transform doorOutside;

        [Tooltip("Inside the doorway. A building's door is usually the only gap in its wall, " +
                 "and the straight line from a bay to a chair does not go through it.")]
        [SerializeField] Transform doorInside;

        /// <summary>True once every node is present. Checked rather than assumed: a half-wired
        /// route produces cars driving to the world origin and no error at all.</summary>
        public bool IsComplete =>
            roadStart != null && roadEnd != null && gateOutside != null &&
            gateInside != null && doorOutside != null && doorInside != null;

        public Vector3 RoadStart => Point(roadStart);
        public Vector3 RoadEnd => Point(roadEnd);
        public Vector3 GateOutside => Point(gateOutside);
        public Vector3 GateInside => Point(gateInside);
        public Vector3 DoorOutside => Point(doorOutside);
        public Vector3 DoorInside => Point(doorInside);

        /// <summary>Off the map, in through the gate, onto the apron, into the bay.</summary>
        public IReadOnlyList<Vector3> DriveIn(ParkingLot lot, ParkingSlot bay)
        {
            var path = new List<Vector3>(5) { RoadStart, GateOutside, GateInside };

            if (lot != null) path.Add(lot.LaneEntry);
            if (bay != null) path.Add(bay.ParkPosition);

            return path;
        }

        /// <summary>Out of the bay, off the apron, through the gate, away down the road.</summary>
        public IReadOnlyList<Vector3> DriveOut(ParkingLot lot)
        {
            var path = new List<Vector3>(4);
            if (lot != null) path.Add(lot.LaneExit);

            path.Add(GateInside);
            path.Add(GateOutside);
            path.Add(RoadEnd);

            return path;
        }

        /// <summary>How many points <see cref="WalkToSeat"/> has past the inside of the doorway: beside
        /// the chair, then in front of it.</summary>
        public const int SeatApproachPoints = 2;

        /// <summary>
        /// From the car door to the chair, through the restaurant doorway. Ends standing in front of
        /// the chair, having stepped in from beside it - the visitor sits down from there.
        /// </summary>
        public IReadOnlyList<Vector3> WalkToSeat(ParkingLot lot, in SeatRef seat)
        {
            var path = new List<Vector3>(5);
            if (lot != null) path.Add(lot.LaneEntry);

            path.Add(DoorOutside);
            path.Add(DoorInside);
            path.Add(seat.SidePoint(DoorInside));
            path.Add(seat.StandPoint);

            return path;
        }

        /// <summary>Back out the same way. Built rather than reversed so the car-door point is
        /// the caller's to supply: a visitor leaves for whichever bay their own car is in.</summary>
        public IReadOnlyList<Vector3> WalkToCar(ParkingLot lot, Vector3 doorPoint)
        {
            var path = new List<Vector3>(4) { DoorInside, DoorOutside };

            if (lot != null) path.Add(lot.LaneEntry);

            path.Add(doorPoint);
            return path;
        }

        /// <summary>Assigns every node at once. Called by the scene tool.</summary>
        public void Configure(Transform start, Transform end, Transform outsideGate,
                              Transform insideGate, Transform outsideDoor, Transform insideDoor)
        {
            roadStart = start;
            roadEnd = end;
            gateOutside = outsideGate;
            gateInside = insideGate;
            doorOutside = outsideDoor;
            doorInside = insideDoor;
        }

        static Vector3 Point(Transform t) => t != null ? t.position : Vector3.zero;

        void OnDrawGizmosSelected()
        {
            DrawLeg(new[] { roadStart, gateOutside, gateInside }, new Color(0.3f, 0.9f, 1f));
            DrawLeg(new[] { gateInside, gateOutside, roadEnd }, new Color(0.7f, 0.7f, 0.75f));
            DrawLeg(new[] { gateInside, doorOutside, doorInside }, new Color(0.4f, 1f, 0.6f));
        }

        /// <summary>Draws a leg, skipping nodes the level does not have. A missing node should
        /// draw a shorter line, not a line to the origin.</summary>
        static void DrawLeg(Transform[] nodes, Color colour)
        {
            Gizmos.color = colour;

            Transform previous = null;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] == null) continue;

                if (previous != null)
                {
                    Gizmos.DrawLine(previous.position + Vector3.up * 0.1f,
                                    nodes[i].position + Vector3.up * 0.1f);
                }

                previous = nodes[i];
            }
        }
    }
}
