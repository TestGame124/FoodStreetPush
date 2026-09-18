using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// The single asset the composition root is handed. Everything tunable that is not
    /// already on a per-content definition lives here.
    ///
    /// 18 and 19 are flagged in the GDD as placeholders pending Phase A data, so expect
    /// most of this to move. That is the argument for it being one asset rather than
    /// constants spread across ten services.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Config/Game Balance", fileName = "GameBalance")]
    public sealed class GameBalanceConfig : ScriptableObject
    {
        [Header("Clock (4)")]
        [Tooltip("Real seconds per in-game clock-minute. 6 gives a 6-minute working day and " +
                 "an 8-12 minute cycle overall.")]
        public float realSecondsPerClockMinute = 6f;

        [Tooltip("Real seconds of the dusk warning. 45 is the single most important stretch of " +
                 "audio in the game (22) and the moment the store-page screenshot comes from.")]
        public float duskWarningSeconds = 45f;

        [Header("Customers (9.2)")]
        [Tooltip("BasePool = basePartyCount + floor(Reputation x reputationToParties).")]
        public int basePartyCount = 4;

        public float reputationToParties = 0.24f;

        [Tooltip("Arrival share per 10-clock-minute block. 9.3: 5, 15, 30, 15, 20, 15 percent. " +
                 "Two rushes where doors, tables and servers bind simultaneously, with " +
                 "deliberate breathing room between.")]
        public float[] arrivalCurveByBlock = { 0.05f, 0.15f, 0.30f, 0.15f, 0.20f, 0.15f };

        [Tooltip("Party size weights for 1, 2, 3 and 4. 9.5: 40, 30, 20, 10 percent.")]
        public float[] partySizeWeights = { 0.40f, 0.30f, 0.20f, 0.10f };

        [Tooltip("Clock-minutes a queued party waits before leaving. They stand OUTSIDE the " +
                 "perimeter while they wait, which is the point.")]
        public float queuePatienceClockMinutes = 8f;

        [Tooltip("Reputation lost per party that gives up in the queue.")]
        public float queueAbandonReputationPenalty = 2f;

        [Header("The Last Party (9.4)")]
        [Tooltip("Guarantee one high-value party arrives in this clock-minute window. A family " +
                 "of four, a big payout, and no chance they finish before dusk. This is a " +
                 "designed daily beat, not an emergent maybe.")]
        public Vector2 lastPartyWindow = new Vector2(48f, 52f);

        public float lastPartyHeat = 8f;

        public float lastPartyTurnAwayReputation = -5f;

        [Tooltip("Heat from feeding them at the door instead. Requires a takeaway window.")]
        public float lastPartyDoorServiceHeat = 2f;

        [Header("Doors (7)")]
        [Tooltip("Clock-seconds to admit one party through one door. A door being processed is " +
                 "an OPEN door - for this whole duration it is a hole in the perimeter.")]
        public float doorAdmissionClockSeconds = 90f;

        [Header("Reputation (10)")]
        [Tooltip("Tier boundaries. Reputation can dip but never below the highest boundary " +
                 "reached (15.1), so tiers are permanent unlocks.")]
        public float[] reputationTiers = { 0f, 25f, 50f, 75f };

        [Tooltip("Lost per day while closed, badly damaged, or serving poorly. This is what " +
                 "makes turtling a losing strategy, which is correct for a game whose thesis " +
                 "is that safety is unaffordable.")]
        public float reputationDecayPerDay = 2f;

        [Header("Consequences (15.8)")]
        [Tooltip("Hard ceiling on structure loss in one night, as a fraction. Even a disaster " +
                 "caps here.")]
        [Range(0f, 1f)] public float maxStructureLossFraction = 0.65f;

        [Tooltip("Ceiling on theft of any SINGLE resource in one night, as a fraction, so a " +
                 "bad night never zeroes the player's ability to act (15.6).")]
        [Range(0f, 1f)] public float maxTheftFractionPerResource = 0.5f;

        [Tooltip("Fraction of original cost to rebuild from a ghost outline (15.2).")]
        [Range(0f, 1f)] public float rebuildCostFraction = 0.4f;

        [Tooltip("Fraction of original cost to repair a Damaged object (15.4).")]
        [Range(0f, 1f)] public float repairCostFraction = 0.15f;

        [Tooltip("Days an injured Survivor is unavailable. A Medic halves it. Permadeath is " +
                 "cut (15.5) and lives only behind an off-by-default Harsh Mode.")]
        public Vector2Int injuryDaysRange = new Vector2Int(1, 3);

        [Header("Closed for Repairs (15.10)")]
        [Tooltip("Build speed bonus when the player skips serving entirely for a day.")]
        public float closedForRepairsBuildBonus = 0.5f;

        public float closedForRepairsReputationCost = 15f;

        [Header("Forecast (16.1)")]
        [Tooltip("Chance the dawn Threat Profile is incomplete. Roughly one night in six: " +
                 "enough to prevent autopilot, rare enough never to feel cheated. Wrong " +
                 "forecasts skew toward ADDITIONAL threat, never entirely different threat.")]
        [Range(0f, 0.5f)] public float incompleteForecastChance = 1f / 6f;

        [Header("Undo (6.3)")]
        [Tooltip("Seconds the last placement can be undone for free.")]
        public float undoWindowSeconds = 5f;

        [Header("Determinism")]
        [Tooltip("Campaign seed. Fixed per save so (day, Heat, seed) reproduces a night exactly, " +
                 "which is what lets the Threat Profile be honest.")]
        public int campaignSeed = 20260918;

        [Header("New game")]
        [Tooltip("What the player starts day 1 with. Enough to build a perimeter and cook, " +
                 "not enough to be comfortable.")]
        public ResourceAmount[] startingResources =
        {
            new ResourceAmount(ResourceType.Coin, 120),
            new ResourceAmount(ResourceType.Scrap, 60),
            new ResourceAmount(ResourceType.Parts, 10),
            new ResourceAmount(ResourceType.Ammo, 20),
            new ResourceAmount(ResourceType.Medicine, 2),
            new ResourceAmount(ResourceType.Veg, 12),
            new ResourceAmount(ResourceType.Grain, 12),
            new ResourceAmount(ResourceType.Meat, 6),
            new ResourceAmount(ResourceType.Spice, 4),
        };

        [Tooltip("Survivors on the roster at the start. 17 makes staffing the progression " +
                 "system that unlocks the rest of the game, so this is deliberately small: " +
                 "with no staff the player does everything and the clock destroys them, and " +
                 "feeling that is what makes the first recruit matter.")]
        public int startingSurvivorCount = 1;

        [Tooltip("Names drawn for starting and recruited Survivors.")]
        public string[] survivorNames =
        {
            "Kira", "Mahmoud", "Tess", "Osei", "Bex", "Ruiz", "Halvard", "Nnamdi",
            "Sana", "Petra", "Yusuf", "Lowell",
        };

        [Header("Content registries")]
        public HeatWeightsConfig heatWeights;
        public PlotTierDefinition[] plotTiers;
        public EnemyDefinition[] enemies;
        public WaveManifest[] waveManifests;
        public NamedNightDefinition[] namedNights;
        public CustomerArchetypeDefinition[] customerArchetypes;
        public RecipeDefinition[] recipes;
        public WallMaterialDefinition[] wallMaterials;
        public SurvivorRoleDefinition[] survivorRoles;
        public SurvivorTraitDefinition[] survivorTraits;
        public DispatchDestinationDefinition[] dispatchDestinations;
        public CrisisAbilityDefinition[] crisisAbilities;

        /// <summary>Which Reputation tier a value falls in. Shared by the archetype weighting,
        /// the Heat contribution and the expansion gates so they cannot disagree.</summary>
        public int ReputationTier(float reputation)
        {
            if (reputationTiers == null || reputationTiers.Length == 0) return 0;

            int tier = 0;
            for (int i = 0; i < reputationTiers.Length; i++)
                if (reputation >= reputationTiers[i]) tier = i;

            return tier;
        }

        void OnValidate()
        {
            realSecondsPerClockMinute = Mathf.Max(0.1f, realSecondsPerClockMinute);

            // The arrival curve is a distribution; if it does not sum to 1 the day quietly
            // serves the wrong number of customers and every downstream number is off.
            if (arrivalCurveByBlock != null && arrivalCurveByBlock.Length > 0)
            {
                float sum = 0f;
                for (int i = 0; i < arrivalCurveByBlock.Length; i++) sum += arrivalCurveByBlock[i];
                if (Mathf.Abs(sum - 1f) > 0.01f)
                    Debug.LogWarning($"[GameBalance] arrivalCurveByBlock sums to {sum:0.###}, not 1.", this);
            }
        }
    }
}
