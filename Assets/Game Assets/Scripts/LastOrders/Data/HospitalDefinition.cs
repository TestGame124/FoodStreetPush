using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// The hospital tent the restaurant opens with: how many cots it has and how long a stay
    /// in one takes.
    ///
    /// Recovery is measured in seconds of play rather than in days. A day-long injury is a
    /// number on a report; a bar filling over a cot beside the gate is something the player
    /// watches, and watching the guard's bar crawl while dusk gets closer is the whole cost of
    /// a bad night made visible.
    ///
    /// The clock stops at dawn with everything else, so a stay is never spent on the morning
    /// screen.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Medical/Hospital", fileName = "Hospital_")]
    public sealed class HospitalDefinition : ScriptableObject
    {
        public string displayName = "Hospital Tent";

        [TextArea(2, 4)] public string description;

        [Tooltip("Beds. Anyone injured while every one is taken waits for the next to come free.")]
        [Min(1)] public int cots = 2;

        [Tooltip("Seconds of treatment an injured visitor needs before they can leave.")]
        [Min(1f)] public float visitorRecoverySeconds = 90f;

        [Tooltip("Seconds of treatment an injured survivor - a guard, most nights - needs before " +
                 "they are back at work. Short enough that a guard hurt tonight is back on watch " +
                 "before tomorrow's dusk, so one bad night does not become every night.")]
        [Min(1f)] public float survivorRecoverySeconds = 150f;

        [Tooltip("The role that can be posted to the tent to speed up recovery.")]
        public SurvivorRole medicRole = SurvivorRole.Medic;

        [Tooltip("How much faster everyone recovers with a medic posted, when the medic's role " +
                 "asset does not say. A role's own injury recovery multiplier wins: 0.5 is twice " +
                 "as fast.")]
        [Min(1f)] public float medicSpeedFallback = 2f;

        /// <summary>How much faster treatment runs with this role posted to the tent.</summary>
        public float MedicSpeed(SurvivorRoleDefinition role)
        {
            float multiplier = role != null ? role.injuryRecoveryMultiplier : 1f;

            // The role asset stores a duration multiplier - 0.5 halves the stay - and a speed
            // is its inverse. Anything that would not speed recovery up falls back rather than
            // making a medic a reason to heal slower.
            return multiplier > 0f && multiplier < 1f ? 1f / multiplier : medicSpeedFallback;
        }
    }
}
