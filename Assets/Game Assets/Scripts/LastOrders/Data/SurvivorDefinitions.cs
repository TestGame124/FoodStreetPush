using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>The six roles of 17. A Survivor has exactly one.</summary>
    public enum SurvivorRole
    {
        Cook = 0,
        Server = 1,
        Builder = 2,
        Guard = 3,
        Scout = 4,
        Medic = 5,
    }

    /// <summary>
    /// What a role does, as numbers rather than as code.
    ///
    /// 17 makes the point that Survivors are what make the game affordable to play: with no
    /// staff the player does everything and the clock destroys them. Staffing is therefore
    /// the progression system that unlocks the rest of the game, and these multipliers are
    /// the shape of that curve.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Survivor Role", fileName = "Role_")]
    public sealed class SurvivorRoleDefinition : ScriptableObject
    {
        public SurvivorRole role = SurvivorRole.Cook;

        public string displayName = "Cook";

        [TextArea(2, 3)] public string description;

        [Tooltip("Multiplies cooking station speed. Cook.")]
        public float stationSpeedMultiplier = 1f;

        [Tooltip("Multiplies table service throughput. Server.")]
        public float serviceThroughputMultiplier = 1f;

        [Tooltip("Multiplies build speed, and enables automatic repair during the night. Builder.")]
        public float buildSpeedMultiplier = 1f;

        public bool repairsDuringNight;

        [Tooltip("Damage per second when posted to a defense position. Guard.")]
        public float guardDps;

        [Tooltip("Subtracted from Dispatch risk. Scout.")]
        [Range(0f, 1f)] public float dispatchRiskReduction;

        [Tooltip("Multiplies injury recovery time. Medic halves it, so 0.5.")]
        public float injuryRecoveryMultiplier = 1f;

        [Tooltip("Coin paid daily. 19: wages are the pressure valve. A large roster forces the " +
                 "restaurant to stay open and busy, which raises Heat, which raises threat. " +
                 "The economy must push players toward danger rather than permit a safe idle " +
                 "equilibrium - preserve this through every rebalance.")]
        public int dailyWage = 5;
    }

    /// <summary>
    /// A small modifier with personality. 17 lists Nervous, Ex-Soldier, Regular, Light
    /// Sleeper and Superstitious - the value is character, not power, so keep the numbers
    /// small and the downsides real.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Survivor Trait", fileName = "Trait_")]
    public sealed class SurvivorTraitDefinition : ScriptableObject
    {
        public string displayName = "Nervous";

        [TextArea(2, 3)] public string description;

        [Tooltip("Multiplies this Survivor's role output. Nervous is +20 percent prep, so 1.2.")]
        public float roleEffectivenessMultiplier = 1f;

        [Tooltip("Abandons their post when the siege starts. Nervous.")]
        public bool fleesDuringSiege;

        [Tooltip("Multiplies daily wage. Ex-Soldier eats double.")]
        public float wageMultiplier = 1f;

        [Tooltip("Adds Reputation simply by being on the roster. Regular.")]
        public float passiveReputationPerDay;

        [Tooltip("Clock-minutes of delay before this Survivor starts their first task. " +
                 "Light Sleeper is 5.")]
        public float firstTaskDelayClockMinutes;

        [Tooltip("Refuses to work on Named Nights. Superstitious - a trait that is free most " +
                 "days and expensive on exactly the seven days it matters.")]
        public bool refusesNamedNights;

        [Tooltip("Multiplies injury duration for this Survivor.")]
        public float injuryDurationMultiplier = 1f;
    }
}
