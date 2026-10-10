using System;
using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// One band of the threat the Safety Reputation selects: how many zombies come, in how many
    /// waves, how varied they are, and what holding them off pays.
    ///
    /// The bands are ordered by <see cref="minReputation"/>. A restaurant survivors believe is safe
    /// is a bright light on the road, and the dead are drawn to it in greater numbers and in
    /// stranger company - but never with anything progression has not yet introduced.
    /// </summary>
    [Serializable]
    public sealed class ThreatTierTuning
    {
        [Tooltip("What the player is told the threat is: Low, Medium, High.")]
        public string name = "Low";

        [Tooltip("The Safety Reputation at which this band starts.")]
        [Range(0f, 100f)] public float minReputation;

        [Tooltip("How many short waves the attack comes in.")]
        [Range(1, 3)] public int waves = 2;

        [Tooltip("Zombies across the whole attack, at the first attack fought in this band.")]
        [Min(1)] public int enemies = 5;

        [Tooltip("Zombies added for every attack the restaurant has already fought.")]
        [Min(0f)] public float enemiesPerEncounter = 0.5f;

        [Tooltip("Never more zombies than this in one attack in this band.")]
        [Min(1)] public int maxEnemies = 8;

        [Tooltip("Different kinds of zombie in one wave, at most. One is a plain shamble; three is a " +
                 "mixed attack the defenses have to answer in different ways.")]
        [Range(1, 3)] public int typesPerWave = 1;

        [Tooltip("Sides each wave comes from. Every wave comes from a different side to the one before.")]
        [Range(1, 2)] public int sidesPerWave = 1;

        [Tooltip("The dearest kind of zombie this band may send, by its threat cost. Cheap ones only " +
                 "keeps a low band to the basic shamblers, whatever has been unlocked.")]
        [Min(0.1f)] public float maxThreatCost = 1.5f;

        [Header("Victory reward")]
        [Tooltip("The level's own Coins - what visitors pay and the HUD counts.")]
        [Min(0)] public int victoryCoins = 40;

        [Min(0)] public int victoryExperience = 20;

        [Tooltip("Repair kits handed over for holding the line. One kit restores every wreck at once.")]
        [Min(0)] public int victoryRepairKits = 1;

        public ThreatTierTuning() { }

        public ThreatTierTuning(string name, float minReputation, int waves, int enemies, float enemiesPerEncounter,
                                int maxEnemies, int typesPerWave, int sidesPerWave, float maxThreatCost,
                                int victoryCoins, int victoryExperience, int victoryRepairKits)
        {
            this.name = name;
            this.minReputation = minReputation;
            this.waves = waves;
            this.enemies = enemies;
            this.enemiesPerEncounter = enemiesPerEncounter;
            this.maxEnemies = maxEnemies;
            this.typesPerWave = typesPerWave;
            this.sidesPerWave = sidesPerWave;
            this.maxThreatCost = maxThreatCost;
            this.victoryCoins = victoryCoins;
            this.victoryExperience = victoryExperience;
            this.victoryRepairKits = victoryRepairKits;
        }

        /// <summary>How many zombies this band sends once the restaurant has fought
        /// <paramref name="encountersFought"/> attacks.</summary>
        public int EnemiesAfter(int encountersFought) =>
            Mathf.Clamp(Mathf.RoundToInt(enemies + enemiesPerEncounter * Mathf.Max(0, encountersFought)), 1,
                        Mathf.Max(1, maxEnemies));
    }

    /// <summary>
    /// The zombie attack cycle: when attacks come, how long the warning is, how the waves run,
    /// how much the restaurant and the yard can take, and how the difficulty is held in check.
    ///
    /// Tuned to be casual. An attack is a few minutes every few days, never a run-ender: the worst
    /// it does is wreck a capped handful of things in the yard, and a wreck keeps its place and its
    /// upgrades until one repair kit brings everything back.
    /// </summary>
    [Serializable]
    public sealed class EncounterTuning
    {
        [Header("Schedule")]
        [Tooltip("Days between attacks. Counted in in-game days, which only pass while the game is " +
                 "being played: the working day stops when the game does.")]
        [Min(1)] public int attackEveryDays = 3;

        [Tooltip("The first day that ends in an attack. Days before it end quietly.")]
        [Min(1)] public int firstAttackDay = 3;

        [Tooltip("Real seconds of warning before the attack. Night starts when it runs out, " +
                 "whether or not the defenses are ready.")]
        [Min(5f)] public float preparationSeconds = 60f;

        [Header("Waves")]
        [Tooltip("Seconds over which one wave's zombies set off, in their small groups.")]
        [Min(0f)] public float waveArrivalSeconds = 8f;

        [Tooltip("Smallest and largest knot of zombies that comes up a side together.")]
        public Vector2Int groupSize = new Vector2Int(2, 3);

        [Tooltip("Seconds between members of one group setting off.")]
        [Min(0f)] public float groupStaggerSeconds = 1.2f;

        [Tooltip("Breathing room once a wave is beaten, before the next one sets off.")]
        [Min(0f)] public float breakBetweenWavesSeconds = 6f;

        [Tooltip("Seconds a wave is given before the next one comes anyway. Keeps a straggler the " +
                 "defenses cannot see from holding the night open.")]
        [Min(10f)] public float waveTimeoutSeconds = 75f;

        [Tooltip("Seconds past the last wave's timeout before morning comes and whatever is left " +
                 "slinks off. The attack still counts as held.")]
        [Min(0f)] public float stragglerGraceSeconds = 30f;

        [Header("The restaurant")]
        [Tooltip("The restaurant's health. Full at the start of every attack, carried from wave to " +
                 "wave, and the attack is lost the moment it reaches zero.")]
        [Min(1f)] public float restaurantHealth = 400f;

        [Tooltip("Seconds the crew takes to patch the restaurant back to full in the morning.")]
        [Min(0.5f)] public float crewRepairSeconds = 4f;

        [Header("The yard")]
        [Tooltip("Outdoor objects one attack may wreck, at most. Past it, every zombie still standing " +
                 "goes for the restaurant.")]
        [Min(0)] public int maxOutdoorWrecks = 2;

        [Tooltip("Health of a bed, a pen, a turret in the yard. A wreck keeps its place and upgrades.")]
        [Min(1f)] public float outdoorObjectHealth = 45f;

        [Tooltip("Metres a zombie inside the walls looks for something in the yard to smash.")]
        [Min(1f)] public float raidSearchRadius = 16f;

        [Tooltip("Zombies that may gang up on one object at once.")]
        [Min(1)] public int maxRaidersPerObject = 3;

        [Tooltip("Seconds a zombie keeps trying to reach its target before it gives up and heads for " +
                 "the restaurant.")]
        [Min(1f)] public float raidGiveUpSeconds = 20f;

        [Header("Holding difficulty in check")]
        [Tooltip("Threat bands the attack may climb from one attack to the next.")]
        [Min(0)] public int maxTierRisePerEncounter = 1;

        [Tooltip("Zombies the attack may grow by from one attack to the next.")]
        [Min(0)] public int maxEnemyRisePerEncounter = 3;

        [Tooltip("Defeats in a row before the next attacks are lightened.")]
        [Min(1)] public int defeatsBeforeRelief = 2;

        [Tooltip("Share of the zombies taken off for each defeat in a row, from the first that counts.")]
        [Range(0f, 1f)] public float reliefPerDefeat = 0.25f;

        [Tooltip("Never fewer than this share of the zombies, however many defeats in a row.")]
        [Range(0.1f, 1f)] public float minimumRelief = 0.5f;

        [Header("Repairs")]
        [Tooltip("Repair kits a new game starts with.")]
        [Min(0)] public int startingRepairKits = 1;

        [Tooltip("Coins a repair kit costs when there is none in the stores. Zero: kits can only be won.")]
        [Min(0)] public int repairKitCoinPrice = 40;

        [Header("Morning")]
        [Tooltip("Seconds the zombies left standing take to slink off before morning.")]
        [Min(0f)] public float retreatSeconds = 4f;

        [Header("Threat bands, lowest first")]
        public ThreatTierTuning[] tiers =
        {
            new ThreatTierTuning("Low", 0f, waves: 2, enemies: 5, enemiesPerEncounter: 0.5f, maxEnemies: 8,
                                 typesPerWave: 1, sidesPerWave: 1, maxThreatCost: 1.5f,
                                 victoryCoins: 40, victoryExperience: 20, victoryRepairKits: 1),
            new ThreatTierTuning("Medium", 35f, waves: 3, enemies: 8, enemiesPerEncounter: 0.5f, maxEnemies: 12,
                                 typesPerWave: 2, sidesPerWave: 1, maxThreatCost: 4f,
                                 victoryCoins: 70, victoryExperience: 35, victoryRepairKits: 1),
            new ThreatTierTuning("High", 70f, waves: 3, enemies: 11, enemiesPerEncounter: 0.5f, maxEnemies: 16,
                                 typesPerWave: 3, sidesPerWave: 2, maxThreatCost: 99f,
                                 victoryCoins: 110, victoryExperience: 50, victoryRepairKits: 1),
        };

        /// <summary>True when <paramref name="day"/> ends in an attack.</summary>
        public bool IsAttackDay(int day)
        {
            int every = Mathf.Max(1, attackEveryDays);
            int first = Mathf.Max(1, firstAttackDay);
            return day >= first && (day - first) % every == 0;
        }

        /// <summary>The first attack day on or after <paramref name="day"/>.</summary>
        public int NextAttackDay(int day)
        {
            int every = Mathf.Max(1, attackEveryDays);
            int first = Mathf.Max(1, firstAttackDay);

            if (day <= first) return first;

            int since = day - first;
            int rest = since % every;
            return rest == 0 ? day : day + (every - rest);
        }

        /// <summary>The band a reputation falls in, before any limit on how fast it may climb.</summary>
        public int TierFor(float reputation)
        {
            if (tiers == null || tiers.Length == 0) return 0;

            int tier = 0;
            for (int i = 0; i < tiers.Length; i++)
                if (tiers[i] != null && reputation >= tiers[i].minReputation) tier = i;

            return tier;
        }

        public ThreatTierTuning Tier(int index)
        {
            if (tiers == null || tiers.Length == 0) return new ThreatTierTuning();
            return tiers[Mathf.Clamp(index, 0, tiers.Length - 1)] ?? new ThreatTierTuning();
        }

        /// <summary>Share of the zombies that still come after <paramref name="defeatsInARow"/>
        /// defeats in a row: one until the relief starts, then less for each, never below the floor.</summary>
        public float ReliefAfter(int defeatsInARow)
        {
            int counted = defeatsInARow - Mathf.Max(1, defeatsBeforeRelief) + 1;
            if (counted <= 0) return 1f;

            return Mathf.Clamp(1f - reliefPerDefeat * counted, minimumRelief, 1f);
        }
    }

    /// <summary>
    /// The Safety Reputation: one meter from 0 to 100 for how safe survivors believe the
    /// restaurant is. It moves once per attack, by how the attack went, and it decides two things
    /// in return - how often customers turn up, and how hard the next attack is.
    /// </summary>
    [Serializable]
    public sealed class SafetyReputationTuning
    {
        [Tooltip("Where a new game starts.")]
        [Range(0f, 100f)] public float startingReputation = 30f;

        [Header("One result per attack")]
        [Tooltip("Held, and no zombie ever got inside the walls.")]
        public float cleanVictory = 8f;

        [Tooltip("Held, but zombies got inside the walls. Nothing in the yard was wrecked.")]
        public float breachedVictory = 4f;

        [Tooltip("Held, but something in the yard was wrecked.")]
        public float wreckedVictory = 1f;

        [Tooltip("The restaurant fell. One penalty, never more than this, however much else broke.")]
        [Min(0f)] public float defeatPenalty = 10f;

        [Tooltip("Share of a victory's gain lost as the restaurant's own health ran down: at 0.5 a " +
                 "victory with the restaurant nearly gone earns half.")]
        [Range(0f, 1f)] public float restaurantDamageWeight = 0.5f;

        [Header("Customers")]
        [Tooltip("Multiplier on the time between customer cars at a reputation of 0. Above 1 is slower.")]
        [Min(0.1f)] public float arrivalIntervalAtZero = 2.6f;

        [Tooltip("Multiplier on the time between customer cars at 50.")]
        [Min(0.1f)] public float arrivalIntervalAtHalf = 1f;

        [Tooltip("Multiplier on the time between customer cars at 100.")]
        [Min(0.1f)] public float arrivalIntervalAtFull = 0.55f;

        [Tooltip("Customers in one car at a reputation of 0. Even an empty-seeming road brings " +
                 "somebody now and then, so the restaurant can always earn its way back.")]
        public Vector2Int partySizeAtZero = new Vector2Int(1, 2);

        [Tooltip("Customers in one car at 100.")]
        public Vector2Int partySizeAtFull = new Vector2Int(1, 4);
    }
}
