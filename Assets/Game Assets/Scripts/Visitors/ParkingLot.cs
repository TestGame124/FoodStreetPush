using System.Collections.Generic;
using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// Every bay in the car park, and the one place they are handed out.
    ///
    /// The second of the two gates on arrivals, and the one the brief puts first: more visitors
    /// only come if there is somewhere to put the car. That makes the car park a visible,
    /// buildable limit on trade - the player can stand in it and count what they have - which a
    /// hidden spawn cap never is.
    ///
    /// Every car enters through <see cref="laneEntry"/> and leaves through <see cref="laneExit"/>
    /// rather than driving the straight line from the gate to its bay. One shared mouth is what
    /// stops a car cutting the corner across three parked ones, and it costs a single waypoint.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class ParkingLot : MonoBehaviour
    {
        [Tooltip("The apron mouth every arriving car drives to before turning into its bay.")]
        [SerializeField] Transform laneEntry;

        [Tooltip("Where a leaving car rejoins the drive. Usually beside the entry; a separate " +
                 "node so a one-way apron can be laid out later without code.")]
        [SerializeField] Transform laneExit;

        readonly List<ParkingSlot> slots = new List<ParkingSlot>();

        public IReadOnlyList<ParkingSlot> Slots => slots;

        public int Capacity => slots.Count;

        public int FreeSlotCount
        {
            get
            {
                int free = 0;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i] != null && slots[i].IsFree) free++;
                }

                return free;
            }
        }

        public bool HasFreeSlot => FreeSlotCount > 0;

        /// <summary>The apron mouth. Falls back to the lot itself, so a half-wired lot sends
        /// cars somewhere visible rather than to the world origin.</summary>
        public Vector3 LaneEntry => laneEntry != null ? laneEntry.position : transform.position;

        public Vector3 LaneExit => laneExit != null ? laneExit.position : LaneEntry;

        /// <summary>
        /// Adds a bay, keeping the list in hierarchy order.
        ///
        /// Sorted on the way in rather than left in arrival order, because bays register
        /// themselves from OnEnable and Unity does not promise to enable them in the order they
        /// sit in the scene. Without this the lot hands out whichever bay happened to wake up
        /// first, and a half-full car park comes out scattered one space apart - which reads as
        /// a bug where a car park filling from one end reads as a business with regulars.
        /// </summary>
        public void Register(ParkingSlot slot)
        {
            if (slot == null || slots.Contains(slot)) return;

            int order = slot.transform.GetSiblingIndex();
            int at = slots.Count;

            while (at > 0 && slots[at - 1].transform.GetSiblingIndex() > order) at--;

            slots.Insert(at, slot);
        }

        public void Unregister(ParkingSlot slot)
        {
            if (slot == null) return;

            slot.Release();
            slots.Remove(slot);
        }

        /// <summary>
        /// Claims a bay for an arriving car.
        ///
        /// Bays are handed out in order rather than at random, so the car park fills from one
        /// end. A half-full lot with the cars clustered reads as a business with regulars; the
        /// same cars scattered one bay apart reads as a bug.
        /// </summary>
        public bool TryReserve(VisitorVehicle vehicle, out ParkingSlot slot)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null || !slots[i].IsFree) continue;
                if (!slots[i].TryReserve(vehicle)) continue;

                slot = slots[i];
                return true;
            }

            slot = null;
            return false;
        }

        /// <summary>Empties every bay. For a hard reset, where a car removed abnormally would
        /// otherwise hold its bay for the rest of the session.</summary>
        public void ReleaseAll()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null) slots[i].Release();
            }
        }

        public void Configure(Transform entry, Transform exit)
        {
            laneEntry = entry;
            laneExit = exit;
        }

        void OnDrawGizmosSelected()
        {
            if (laneEntry == null) return;

            Gizmos.color = new Color(0.3f, 0.9f, 1f);
            Gizmos.DrawWireSphere(laneEntry.position, 0.4f);

            if (laneExit != null && laneExit != laneEntry)
            {
                Gizmos.color = new Color(1f, 0.8f, 0.4f);
                Gizmos.DrawWireSphere(laneExit.position, 0.4f);
            }

            foreach (ParkingSlot slot in GetComponentsInChildren<ParkingSlot>())
            {
                if (slot == null) continue;

                Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.4f);
                Gizmos.DrawLine(laneEntry.position, slot.ParkPosition);
            }
        }
    }
}
