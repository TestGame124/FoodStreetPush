using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>What a ground defense does to what reaches it.</summary>
    public enum GroundDefenseKind
    {
        /// <summary>Stands in the way. A zombie that walks into it stops and batters it until it falls.</summary>
        Barricade,

        /// <summary>Springs on whatever steps on it, hurting everything on it at once, then resets.</summary>
        Trap,

        /// <summary>Holds back whatever wades through it - slower while it is caught, and cut a little.</summary>
        Snare,
    }

    /// <summary>
    /// One kind of ground defense the strip outside the walls can hold: a barricade, a trap or
    /// something that slows the dead down.
    ///
    /// The strip is a row of build slots, and any slot takes any of these - the slot only says
    /// where. Everything that tells one kind from another is here: what it does to a zombie,
    /// how much ground it covers, how long it lasts and what building one costs. How it looks is
    /// its prefab's.
    ///
    /// Every kind wears out - a barricade is battered down, a trap jams after so many strikes, wire
    /// is torn by what pushes through it - and a worn one is put right by a worker, the same way a
    /// wall section is.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Defense/Ground Defense", fileName = "GroundDefense_")]
    public sealed class GroundDefenseDefinition : ScriptableObject
    {
        [Tooltip("Stable identity. Recorded in progression flags, so renaming it forgets every one built.")]
        public string id = "barricade";

        public string displayName = "Barricade";

        [TextArea(2, 4)] public string description;

        public GroundDefenseKind kind;

        [Tooltip("The model. Instantiated on the slot, its +Z facing out from the wall.")]
        public GameObject prefab;

        [Header("Building")]
        [Tooltip("Taken from the stores. Empty: free.")]
        public ResourceAmount[] buildCost;

        [Header("Ground")]
        [Tooltip("Metres along the strip (X) and across it (Z): what a barricade blocks, or what a " +
                 "trap or snare catches. Keep it inside the slot.")]
        public Vector2 footprint = new Vector2(2.4f, 0.6f);

        [Header("Wear")]
        [Tooltip("A barricade's health against blows; a trap's strikes before it jams; a snare's " +
                 "zombie-seconds before it is torn.")]
        [Min(1f)] public float health = 120f;

        [Tooltip("Below this share of its health it shows as damaged.")]
        [Range(0.05f, 0.95f)] public float damagedBelow = 0.5f;

        [Header("Trap")]
        [Tooltip("Damage to everything on it, each time it springs.")]
        [Min(0f)] public float strikeDamage = 8f;

        [Tooltip("Seconds from one strike to the next while something stays on it.")]
        [Min(0.1f)] public float strikeSeconds = 1.2f;

        [Tooltip("Health a strike costs it.")]
        [Min(0f)] public float wearPerStrike = 1f;

        [Header("Holding back")]
        [Tooltip("Speed, as a share of its own, of whatever it holds. One: it does not slow anything.")]
        [Range(0.05f, 1f)] public float slowMultiplier = 1f;

        [Tooltip("Seconds the slow lasts after whatever it caught is free of it.")]
        [Min(0f)] public float slowSeconds = 0.6f;

        [Tooltip("A snare's: damage a second to each thing caught in it, dealt once a second.")]
        [Min(0f)] public float damagePerSecond;

        [Tooltip("A snare's: health lost for each second each thing spends caught in it.")]
        [Min(0f)] public float wearPerSecond = 1f;

        /// <summary>"Barricade: stops zombies until they smash through it" - one line, for the build popup.</summary>
        public string Summary()
        {
            switch (kind)
            {
                case GroundDefenseKind.Barricade:
                    return $"{displayName}: stops zombies until they smash through ({health:0} health)";

                case GroundDefenseKind.Trap:
                    return $"{displayName}: {strikeDamage:0.#} damage to everything on it every {strikeSeconds:0.#}s, " +
                           $"{health:0} strikes before it jams";

                default:
                    return $"{displayName}: holds anything in it to {slowMultiplier * 100f:0}% speed" +
                           (damagePerSecond > 0f ? $", {damagePerSecond:0.#} damage a second" : "");
            }
        }
    }
}
