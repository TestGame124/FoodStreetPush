using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// The car a visitor turns up in.
    ///
    /// Vehicles are their own asset rather than fields on the visitor, because the interesting
    /// pairing is many-to-many: the same rusted pickup should be able to bring a farmer or a
    /// scavenger, and a visitor who acquires a better car later should not need a second copy
    /// of their whole definition.
    ///
    /// The body is described in metres rather than modelled, and the prefab is generated from
    /// those numbers. That keeps the parking bays honest: a bay is sized from
    /// <see cref="bayFootprint"/>, so a longer car cannot quietly overhang the one behind it.
    /// </summary>
    [CreateAssetMenu(menuName = "Food Street/Visitors/Vehicle", fileName = "Vehicle_")]
    public sealed class VehicleDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Pickup";

        [Header("Body")]
        [Tooltip("Length along local Z, height, width along local X. The generated greybox is " +
                 "built to these numbers and the bay is sized from them.")]
        public Vector3 bodySize = new Vector3(1.9f, 0.85f, 4.4f);

        public Color bodyColour = new Color(0.62f, 0.31f, 0.22f);
        public Color cabinColour = new Color(0.28f, 0.34f, 0.45f);
        public float wheelRadius = 0.36f;

        [Header("Driving")]
        [Tooltip("Metres per second on the road. Cars are faster than people or the arrival " +
                 "reads as a queue rather than a journey.")]
        [Min(0.1f)] public float driveSpeed = 6f;

        [Tooltip("Metres per second on the parking apron, where a car is manoeuvring.")]
        [Min(0.1f)] public float manoeuvreSpeed = 2.2f;

        [Tooltip("Degrees per second the body turns to face where it is going.")]
        [Min(1f)] public float turnSpeed = 180f;

        [Tooltip("Metres from a waypoint that counts as reaching it. Larger than a person's, " +
                 "because a car that has to hit a point exactly circles it instead.")]
        [Min(0.05f)] public float arriveRadius = 0.6f;

        [Header("Parking")]
        [Tooltip("Width by length of bay this needs, in metres. The parking builder sizes its " +
                 "bays from the largest vehicle in the traffic profile.")]
        public Vector2 bayFootprint = new Vector2(2.6f, 5f);

        [Header("Prefab")]
        [Tooltip("Built by 'Food Street / Visitors / Generate Visitor Content'. A prefab " +
                 "assigned by hand is respected and never overwritten.")]
        public GameObject prefab;

        void OnValidate()
        {
            bodySize = Vector3.Max(bodySize, new Vector3(0.5f, 0.2f, 1f));
            bayFootprint = Vector2.Max(bayFootprint,
                                       new Vector2(bodySize.x + 0.6f, bodySize.z + 0.6f));
        }
    }
}
