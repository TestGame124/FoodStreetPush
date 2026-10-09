using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// One entry of the 14 roster. The whole of an archetype's behaviour lives in this
    /// asset, and the agent that reads it has no per-archetype code at all.
    ///
    /// That is the point of 24.3 doing double duty as an AI architecture. A Brute is not a
    /// Shambler with extra branching; it is a Shambler sampling the Breach field instead of
    /// the Doors field. A Glutton samples Pantry. A Flanker samples WeakestPoint. Nothing in
    /// EnemyAgent asks what kind of enemy it is.
    ///
    /// The design rule from 14 that constrains the art: every archetype must be
    /// identifiable by silhouette and hue alone at roughly 40px. Animation and surface
    /// detail carry nothing at that size, which is why the colour is authored here as data
    /// and validated, rather than left to a material.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Enemy", fileName = "Enemy_")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Shambler";

        [Tooltip("Short line for the bestiary and the Threat Profile broadcast.")]
        [TextArea(2, 3)] public string description;

        [Tooltip("Hard colour-coding per archetype. 21: never two archetypes in the same hue " +
                 "family, and the coding must be colourblind-safe because the design leans " +
                 "heavily on hue.")]
        public Color silhouetteColour = Color.grey;

        [Tooltip("Tint the whole body in the silhouette colour as it spawns - the hue doing the " +
                 "identifying for archetypes that share a model. Off for one with a model of its " +
                 "own, painted in its own colours, which the tint would wash out.")]
        public bool tintBody = true;

        [Header("Behaviour - this single field IS the AI (24.3)")]
        [Tooltip("Which flow field this archetype samples. Doors routes to a door with walls " +
                 "solid. Breach makes walls passable but expensive. WeakestPoint pools at the " +
                 "thinnest defended stretch. Pantry routes to the stores.")]
        public ThreatNavigation navigation = ThreatNavigation.Doors;

        [Tooltip("Never attacks anything. The Screecher's whole role: it raises Heat and calls " +
                 "reinforcements, and exists to punish passive play.")]
        public bool isNonCombatant;

        [Tooltip("Heat added per second while alive. Screecher only.")]
        public float heatPerSecond;

        [Tooltip("Ignites wooden structures on contact. Burner only. Punishes cheap wood spam.")]
        public bool ignitesStructures;

        [Tooltip("Steals resources rather than only destroying. Raider only. Theft is capped " +
                 "at 50 percent of any single resource per night by 15.6 regardless.")]
        public bool stealsResources;

        [Tooltip("Ignores slowing traps. Runner only, and the reason slow-only defenses fail.")]
        public bool immuneToSlow;

        [Header("Combat (14.1)")]
        [Tooltip("Damage per second dealt to structures.")]
        public float structureDps = 6f;

        public float maxHealth = 30f;

        [Tooltip("World units per second. Shambler is the 1.0 baseline.")]
        public float moveSpeed = 1f;

        [Tooltip("Physical radius, for steering separation and for fitting through doorways.")]
        public float agentRadius = 0.35f;

        [Tooltip("How close the agent must be to a structure to damage it.")]
        public float attackRange = 0.6f;

        [Header("Spawning")]
        [Tooltip("The pooled prefab. Pools are keyed by prefab, so two archetypes sharing a " +
                 "component never share a pool.")]
        public GameObject prefab;

        [Tooltip("Instances pre-warmed at boot. Set near the typical peak count for this " +
                 "archetype so a wave does not instantiate mid-siege.")]
        public int poolPrewarm = 8;

        [Tooltip("Relative cost when the WaveDirector fills a wave against a Heat budget. " +
                 "A Brute costing 6 and a Shambler costing 1 is what stops high Heat from " +
                 "simply meaning more Shamblers.")]
        public float threatCost = 1f;

        [Tooltip("Earliest campaign day this may appear. Act pacing from 18 is expressed here " +
                 "rather than in the WaveDirector.")]
        public int earliestDay = 1;

        [Header("Rewards")]
        [Tooltip("Dropped on death, before any night-total caps.")]
        public ResourceAmount[] drops;

        /// <summary>Maps the authored field id onto the navigation layer's enum. Kept as an
        /// explicit switch so the data enum and the nav enum can diverge without silently
        /// mismatching if either gains a member.</summary>
        public FoodStreet.Navigation.FlowFieldId FlowFieldId
        {
            get
            {
                switch (navigation)
                {
                    case ThreatNavigation.Breach: return FoodStreet.Navigation.FlowFieldId.Breach;
                    case ThreatNavigation.WeakestPoint: return FoodStreet.Navigation.FlowFieldId.WeakestPoint;
                    case ThreatNavigation.Pantry: return FoodStreet.Navigation.FlowFieldId.Pantry;
                    default: return FoodStreet.Navigation.FlowFieldId.Doors;
                }
            }
        }

        /// <summary>Seconds for this enemy alone to breach a structure of the given HP.
        /// The 14.1 time-to-breach table is this function, and having it here means the
        /// table in the design doc can be regenerated from the assets rather than
        /// maintained alongside them.</summary>
        public float SecondsToBreach(float structureHealth) =>
            structureDps <= 0f ? float.PositiveInfinity : structureHealth / structureDps;

        void OnValidate()
        {
            if (isNonCombatant) structureDps = 0f;
            maxHealth = Mathf.Max(1f, maxHealth);
            moveSpeed = Mathf.Max(0.05f, moveSpeed);
            agentRadius = Mathf.Max(0.05f, agentRadius);
            threatCost = Mathf.Max(0.01f, threatCost);
        }
    }
}
