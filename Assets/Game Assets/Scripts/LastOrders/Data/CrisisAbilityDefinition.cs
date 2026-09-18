using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>The seven verbs of 13.3. One grammar throughout: tap ability, tap tile.</summary>
    public enum CrisisVerb
    {
        Patch = 0,
        Reload = 1,
        Bait = 2,
        Reroute = 3,
        Evacuate = 4,
        Rally = 5,
        Douse = 6,
    }

    /// <summary>
    /// A Crisis Bar ability.
    ///
    /// These exist because automated defense alone would make the night a cutscene players
    /// learn to skip. The build is the strategy - that is what makes the day matter - but
    /// it cannot be the entire night.
    ///
    /// The skill ceiling here is triage: recognising which of three simultaneous breaches
    /// actually threatens the pantry, and which the turrets will handle unaided. Tune
    /// cooldowns so that a player can always act on one crisis and never on all three.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Crisis Ability", fileName = "Crisis_")]
    public sealed class CrisisAbilityDefinition : ScriptableObject
    {
        public CrisisVerb verb = CrisisVerb.Patch;

        public string displayName = "Patch";

        [TextArea(2, 3)] public string description;

        public Sprite icon;

        [Tooltip("Real seconds before reuse. GDD: Patch 15, Reload 20, Bait 30, Reroute 25, " +
                 "Evacuate 12, Rally 18, Douse 20.")]
        public float cooldownSeconds = 15f;

        [Tooltip("Tiles from the tapped point the effect covers. Zero means a single tile.")]
        public float radiusTiles;

        [Tooltip("Primary magnitude. Patch restores this fraction of max HP (0.4 for 40 percent); " +
                 "Bait holds enemies for this many seconds.")]
        public float magnitude = 0.4f;

        [Tooltip("Seconds the effect persists. Bait is 8.")]
        public float durationSeconds;

        [Header("Requirements")]
        [Tooltip("Buildable that must exist before this verb is offered. Douse needs a water " +
                 "tank; Reroute needs a generator.")]
        public string requiredBuildableTag;

        [Tooltip("Uses per night. Zero means unlimited, gated only by cooldown.")]
        public int usesPerNight;

        public ResourceAmount[] costPerUse;

        [Tooltip("Unlocked from the campaign's start. Later verbs arrive with their buildings.")]
        public bool unlockedFromStart = true;
    }
}
