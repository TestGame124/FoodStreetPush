using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// An authored setpiece on a fixed day. 16.2 calls these "the game's only levels, and
    /// they are pure data" - a wave manifest, a modifier, and three lines of text.
    ///
    /// Seven of them carry the campaign's structure, and each one costs an afternoon rather
    /// than the weeks a hand-built level would.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Named Night", fileName = "Night_")]
    public sealed class NamedNightDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string title = "First Frost";

        [Tooltip("The campaign day this fires on. 16.2 fixes these at 7, 12, 18, 25, 33, 41, 50.")]
        public int day = 7;

        [TextArea(3, 6)]
        [Tooltip("The dawn radio broadcast. This is the whole of the setpiece's writing budget " +
                 "and it does most of the work.")]
        public string broadcast;

        [Header("Content")]
        [Tooltip("Overrides the manifest the director would otherwise have drawn.")]
        public WaveManifest waveOverride;

        [Header("Modifier")]
        [Tooltip("Stations freeze without a running generator. First Frost.")]
        public bool requiresGenerator;

        [Tooltip("No powered defenses at all for the night. Blackout.")]
        public bool disablesPoweredDefenses;

        [Tooltip("Multiplies night visibility. Rain of Ash halves it.")]
        [Range(0.1f, 1f)] public float visibilityMultiplier = 1f;

        [Tooltip("All wooden structures are at fire risk regardless of Burners. Rain of Ash.")]
        public bool woodFireRisk;

        [Tooltip("Runs two full waves back to back. The Long Night.")]
        public bool doubleLength;

        [Tooltip("The player may pay the attackers off instead of fighting. The Marauders. " +
                 "Paying makes a lasting enemy, which Act III's adaptive antagonist reads.")]
        public bool offersBribe;

        public ResourceAmount[] bribeCost;

        [Tooltip("Traders shelter with the player and their trucks must survive. The Convoy.")]
        public bool hasProtectTarget;

        [Header("Reward")]
        [Tooltip("Paid at Resolution if the night's condition was met.")]
        public ResourceAmount[] completionReward;

        public int reputationReward;
    }
}
