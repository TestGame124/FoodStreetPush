using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// One parking bay.
    ///
    /// Registers itself with the lot rather than being listed on it, so a bay built, moved or
    /// destroyed mid-game joins and leaves the pool with no wiring - which is what lets the car
    /// park grow with the restaurant later without touching the director.
    ///
    /// The bay's own transform IS the parked pose: a car sits at this position facing this
    /// forward. Keeping the two together means a bay dragged in the editor takes its parked
    /// cars with it, where a separate "park here" child is one more thing to remember to move.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class ParkingSlot : MonoBehaviour
    {
        [SerializeField] ParkingLot lot;

        [Tooltip("Where somebody getting out of the car stands, as an offset in the bay's own " +
                 "space. Beside the car rather than inside it.")]
        [SerializeField] Vector3 doorOffset = new Vector3(1.5f, 0f, 0f);

        VisitorVehicle occupant;

        public bool IsFree => occupant == null;

        public VisitorVehicle Occupant => occupant;

        /// <summary>The parked pose: where a car stops and which way it points.</summary>
        public Vector3 ParkPosition => transform.position;

        public Vector3 ParkForward => transform.forward;

        /// <summary>Where the driver stands once they are out. Every walk to and from the
        /// restaurant starts and ends here rather than at the car itself, so nobody is ever
        /// seen standing inside their own bodywork.</summary>
        public Vector3 DoorPoint => transform.TransformPoint(doorOffset);

        void OnEnable()
        {
            if (lot == null) lot = GetComponentInParent<ParkingLot>();

            if (lot == null)
            {
                Debug.LogError($"[Parking] Bay '{name}' has no ParkingLot above it, so nobody " +
                               "will ever be sent to it.", this);
                return;
            }

            lot.Register(this);
        }

        void OnDisable()
        {
            if (lot != null) lot.Unregister(this);
        }

        /// <summary>
        /// Claims the bay.
        ///
        /// Claimed the moment a car is dispatched rather than when it arrives, because the
        /// drive in takes several seconds and a bay that is free for the whole of it is a bay
        /// two cars are sent to.
        /// </summary>
        public bool TryReserve(VisitorVehicle vehicle)
        {
            if (vehicle == null || occupant != null) return false;

            occupant = vehicle;
            return true;
        }

        public void Release() => occupant = null;

        /// <summary>Assigns the bay to a lot at build time.</summary>
        public void Configure(ParkingLot owner, Vector3 door)
        {
            lot = owner;
            doorOffset = door;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = IsFree ? new Color(0.4f, 0.9f, 0.5f, 0.7f) : new Color(0.95f, 0.5f, 0.3f, 0.7f);

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 0.2f, 0f), new Vector3(2.6f, 0.4f, 5f));
            Gizmos.matrix = Matrix4x4.identity;

            Gizmos.DrawWireSphere(DoorPoint, 0.2f);
        }
    }
}
