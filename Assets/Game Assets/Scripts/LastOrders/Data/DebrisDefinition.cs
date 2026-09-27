using LastOrders.Jobs;
using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// A kind of debris: how long it takes to clear, who clears it, and what it pays.
    ///
    /// A type, not an instance. The rubble piles in one yard all share a definition, and a
    /// balance change to "rubble" is one asset rather than a hunt through the scene.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Expansion/Debris", fileName = "Debris_")]
    public sealed class DebrisDefinition : ScriptableObject
    {
        public string displayName = "Rubble";

        [Tooltip("Seconds of work once the worker is standing at it. Travel is on top.")]
        [Min(0.1f)] public float workSeconds = 6f;

        [Tooltip("Paid once, when this piece is cleared.")]
        public RewardBundle reward = new RewardBundle(2);

        [Tooltip("Who is sent. The main character until workers can be hired.")]
        public WorkerRole role = WorkerRole.MainCharacter;

        [Tooltip("Icon on the action button. Empty uses the prompt prefab's own icon - the hammer.")]
        public Sprite actionIcon;
    }
}
