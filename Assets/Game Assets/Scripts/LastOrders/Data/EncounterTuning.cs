using System;
using UnityEngine;
using UnityEngine.Serialization;

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
    /// The small attack that comes on the nights between the big waves: a share of what a big wave
    /// would send, in one short wave from one side, with a shorter warning and a smaller reward.
    /// </summary>
    [Serializable]
    public sealed class NightlyAttackTuning
    {
        [Tooltip("Share of a big wave's zombies a nightly attack sends.")]
        [Range(0.1f, 1f)] public float enemyShare = 0.45f;

        [Tooltip("Never fewer zombies than this on a nightly attack.")]
        [Min(1)] public int minEnemies = 2;

        [Tooltip("Waves a nightly attack comes in, at most.")]
        [Range(1, 3)] public int maxWaves = 1;

        [Tooltip("Kinds of zombie in a nightly wave, at most.")]
        [Range(1, 3)] public int maxTypesPerWave = 2;

        [Tooltip("Real seconds of warning before a nightly attack.")]
        [Min(5f)] public float preparationSeconds = 30f;

        [Tooltip("Share of the band's victory coins and XP a nightly attack pays. It pays no repair kits.")]
        [Range(0f, 1f)] public float rewardShare = 0.4f;

        [Tooltip("Share of a big wave's Safety gain or loss a nightly attack moves the meter by.")]
        [Range(0f, 1f)] public float reputationShare = 0.5f;

        [Tooltip("Outdoor objects a nightly attack may wreck, at most.")]
        [Min(0)] public int maxOutdoorWrecks = 1;
    }

    /// <summary>
    /// What the next attack is measured from besides the Safety Reputation: the player's level makes
    /// it harder, and a compound still showing the last attack's damage makes it lighter, so a
    /// restaurant on its knees is given room to get back up.
    /// </summary>
    [Serializable]
    public sealed class ThreatScalingTuning
    {
        [Header("Player level")]
        [Tooltip("Points added to the Safety Reputation, when picking the threat band, for each level above 1.")]
        [Min(0f)] public float bandPointsPerLevel = 3f;

        [Tooltip("Never more band points than this from the level.")]
        [Min(0f)] public float maxBandPointsFromLevel = 30f;

        [Tooltip("Zombies added for each level above 1, on a big wave.")]
        [Min(0f)] public float enemiesPerLevel = 0.5f;

        [Tooltip("Never more zombies than this from the level. The band's own cap rises by the same amount.")]
        [Min(0)] public int maxEnemiesFromLevel = 8;

        [Header("Zone damage (0 none, 1 everything broken)")]
        [Tooltip("Band points taken off at full damage.")]
        [Min(0f)] public float bandPointsOffAtFullDamage = 25f;

        [Tooltip("Share of the zombies taken off at full damage.")]
        [Range(0f, 0.9f)] public float enemyReliefAtFullDamage = 0.4f;

        [Tooltip("How much each kind of damage counts toward the zone's damage. Walls and gates down or hurt.")]
        [Min(0f)] public float wallWeight = 0.5f;

        [Tooltip("Wall or gate sections knocked down that count as the wall fully damaged.")]
        [Min(1)] public int fallenSectionsForFullWallDamage = 3;

        [Tooltip("Beds, pens and turrets in the yard still wrecked.")]
        [Min(0f)] public float yardWeight = 0.3f;

        [Tooltip("Wrecks standing in the yard that count as the yard fully damaged.")]
        [Min(1)] public int wrecksForFullYardDamage = 4;

        [Tooltip("Defenses knocked out and not yet repaired.")]
        [Min(0f)] public float defenseWeight = 0.2f;

        /// <summary>Band points from <paramref name="level"/>.</summary>
        public float LevelBandPoints(int level) =>
            Mathf.Min(Mathf.Max(0, level - 1) * bandPointsPerLevel, maxBandPointsFromLevel);

        /// <summary>Zombies added by <paramref name="level"/>.</summary>
        public int LevelEnemies(int level) =>
            Mathf.Min(Mathf.RoundToInt(Mathf.Max(0, level - 1) * enemiesPerLevel), maxEnemiesFromLevel);

        public float DamageBandPoints(float damage) => Mathf.Clamp01(damage) * bandPointsOffAtFullDamage;

        /// <summary>Share of the zombies that still come with the zone this damaged.</summary>
        public float DamageRelief(float damage) => 1f - Mathf.Clamp01(damage) * enemyReliefAtFullDamage;

        /// <summary>One number from 0 to 1 for the three kinds of damage, each 0 to 1.</summary>
        public float ZoneDamage(float walls, float yard, float defenses)
        {
            float weights = wallWeight + yardWeight + defenseWeight;
            if (weights <= 0f) return 0f;

            return Mathf.Clamp01((Mathf.Clamp01(walls) * wallWeight + Mathf.Clamp01(yard) * yardWeight +
                                  Mathf.Clamp01(defenses) * defenseWeight) / weights);
        }
    }

    /// <summary>
    /// Who in an attack goes for the defenses instead of the restaurant, when, which defense, and how
    /// many at once.
    ///
    /// Each wave sends a share of its zombies - the toughest first - as hunters: they look for the
    /// nearest defense they can reach and set about it. Any other zombie that comes close to a gun
    /// shooting at it turns on that gun. The rest, always at least one, keep on for the restaurant.
    /// Caps keep it casual: only a few zombies on one defense, never most of the horde on the
    /// defenses at once, and only so many defenses knocked out in one attack before every zombie left
    /// turns on the restaurant.
    /// </summary>
    [Serializable]
    public sealed class ZombieTargetingTuning
    {
        [Header("Who goes for the defenses")]
        [Tooltip("Share of a big wave's zombies sent as hunters - the toughest first.")]
        [Range(0f, 1f)] public float hunterShareBigWave = 0.4f;

        [Tooltip("Share of a nightly pack's zombies sent as hunters.")]
        [Range(0f, 1f)] public float hunterShareNightly = 0.3f;

        [Tooltip("Hunters in any wave of two or more zombies, at least. One always goes for the restaurant.")]
        [Min(0)] public int minHuntersPerWave = 1;

        [Tooltip("Never more than this share of the zombies out at once on the defenses - hunters and " +
                 "provoked together - so the restaurant is always under pressure.")]
        [Range(0.1f, 1f)] public float maxShareOnDefenses = 0.6f;

        [Header("When")]
        [Tooltip("Metres a hunter can see a defense from and set off for it.")]
        [Min(1f)] public float hunterSightMetres = 30f;

        [Tooltip("Metres from a gun shooting at it that any other zombie turns on that gun. Zero: only hunters go for defenses.")]
        [Min(0f)] public float provokedWithinMetres = 9f;

        [Tooltip("Metres from the restaurant inside which a zombie ignores the defenses: it is nearly at the door.")]
        [Min(0f)] public float restaurantCommitMetres = 10f;

        [Header("Which defense")]
        [Tooltip("Metres added to a defense's distance for every zombie already on it, so a pack spreads out.")]
        [Min(0f)] public float crowdPenaltyMetres = 4f;

        [Tooltip("Metres taken off for the gun that is shooting at this zombie.")]
        [Min(0f)] public float shootingAtItBonusMetres = 6f;

        [Tooltip("Metres taken off for a defense standing on the ground rather than up on a bastion: the soft target.")]
        [Min(0f)] public float exposedBonusMetres = 4f;

        [Tooltip("Metres taken off for a defense already battered, at its last legs.")]
        [Min(0f)] public float damagedBonusMetres = 6f;

        [Header("How many")]
        [Tooltip("Zombies on one defense at once, at most, in a big wave.")]
        [Min(1)] public int maxAttackersPerDefense = 3;

        [Tooltip("Zombies on one defense at once, at most, in a nightly attack.")]
        [Min(1)] public int maxAttackersPerDefenseNightly = 2;

        [Tooltip("Defenses one big wave may knock out. After that every zombie goes for the restaurant.")]
        [Min(0)] public int maxKnockedOutBigWave = 2;

        [Tooltip("Defenses one nightly attack may knock out.")]
        [Min(0)] public int maxKnockedOutNightly = 1;

        [Header("The defenses")]
        [Tooltip("Health of a defense standing on the ground - on a ring pad or in the yard. One up on the " +
                 "wall is reached through its bastion, which has health of its own.")]
        [Min(1f)] public float groundDefenseHealth = 90f;

        [Tooltip("Seconds a zombie keeps trying to reach its defense before it gives up and goes for the restaurant.")]
        [Min(1f)] public float giveUpSeconds = 25f;

        public int AttackersPerDefense(bool bigWave) =>
            Mathf.Max(1, bigWave ? maxAttackersPerDefense : maxAttackersPerDefenseNightly);

        public int KnockOutCap(bool bigWave) => Mathf.Max(0, bigWave ? maxKnockedOutBigWave : maxKnockedOutNightly);

        /// <summary>Hunters in a wave of <paramref name="fighters"/> zombies that fight: the share,
        /// at least the minimum once there are two, and never all of them.</summary>
        public int HuntersIn(int fighters, bool bigWave)
        {
            if (fighters <= 1) return 0;

            float share = bigWave ? hunterShareBigWave : hunterShareNightly;
            int hunters = Mathf.Max(Mathf.RoundToInt(fighters * share), minHuntersPerWave);
            return Mathf.Clamp(hunters, 0, fighters - 1);
        }
    }

    /// <summary>
    /// The zombie attack cycle: when attacks come, how long the warning is, how the waves run,
    /// how much the restaurant and the yard can take, and how the difficulty is held in check.
    ///
    /// Zombies come every night. Most nights it is a small pack; every few days it is a big wave.
    /// Tuned to be casual: never a run-ender - the worst an attack does is wreck a capped handful of
    /// things in the yard, and a wreck keeps its place and its upgrades until one repair kit brings
    /// everything back.
    /// </summary>
    [Serializable]
    public sealed class EncounterTuning
    {
        [Header("Schedule")]
        [Tooltip("The first day that ends in a zombie attack. From then on every night has one.")]
        [Min(1)] public int firstNightlyAttackDay = 1;

        [Tooltip("Days between big waves. Counted in in-game days, which only pass while the game is " +
                 "being played: the working day stops when the game does.")]
        [FormerlySerializedAs("attackEveryDays")]
        [Min(1)] public int bigWaveEveryDays = 3;

        [Tooltip("The first day that ends in a big wave.")]
        [FormerlySerializedAs("firstAttackDay")]
        [Min(1)] public int firstBigWaveDay = 3;

        [Tooltip("Real seconds of warning before a big wave. Night starts when it runs out, " +
                 "whether or not the defenses are ready.")]
        [Min(5f)] public float preparationSeconds = 60f;

        [Tooltip("The smaller attack on the nights between big waves.")]
        public NightlyAttackTuning nightly = new NightlyAttackTuning();

        [Tooltip("How the player's level and the compound's damage move the difficulty.")]
        public ThreatScalingTuning scaling = new ThreatScalingTuning();

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

        [Tooltip("Who goes for the defenses instead of the restaurant, when, and how many.")]
        public ZombieTargetingTuning targeting = new ZombieTargetingTuning();

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

        public NightlyAttackTuning Nightly => nightly ??= new NightlyAttackTuning();

        public ThreatScalingTuning Scaling => scaling ??= new ThreatScalingTuning();

        public ZombieTargetingTuning Targeting => targeting ??= new ZombieTargetingTuning();

        /// <summary>True when <paramref name="day"/> ends in an attack, big or small.</summary>
        public bool IsAttackDay(int day) => day >= Mathf.Max(1, firstNightlyAttackDay) || IsBigWaveDay(day);

        /// <summary>True when <paramref name="day"/> ends in a big wave.</summary>
        public bool IsBigWaveDay(int day)
        {
            int every = Mathf.Max(1, bigWaveEveryDays);
            int first = Mathf.Max(1, firstBigWaveDay);
            return day >= first && (day - first) % every == 0;
        }

        /// <summary>The first day on or after <paramref name="day"/> that ends in an attack.</summary>
        public int NextAttackDay(int day)
        {
            day = Mathf.Max(1, day);
            if (day >= Mathf.Max(1, firstNightlyAttackDay)) return day;

            return Mathf.Min(Mathf.Max(1, firstNightlyAttackDay), NextBigWaveDay(day));
        }

        /// <summary>The first big-wave day on or after <paramref name="day"/>.</summary>
        public int NextBigWaveDay(int day)
        {
            int every = Mathf.Max(1, bigWaveEveryDays);
            int first = Mathf.Max(1, firstBigWaveDay);

            if (day <= first) return first;

            int since = day - first;
            int rest = since % every;
            return rest == 0 ? day : day + (every - rest);
        }

        /// <summary>Seconds of warning before the attack that ends <paramref name="day"/>.</summary>
        public float PreparationSeconds(int day) =>
            IsBigWaveDay(day) ? preparationSeconds : Mathf.Min(preparationSeconds, Nightly.preparationSeconds);

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

        [Header("Customers and the player's level")]
        [Tooltip("Share taken off the time between cars for each level above 1: a restaurant that has grown is better known on the road.")]
        [Range(0f, 0.25f)] public float arrivalFasterPerLevel = 0.06f;

        [Tooltip("Never less than this share of the wait, however high the level.")]
        [Range(0.1f, 1f)] public float minLevelArrivalScale = 0.55f;

        [Tooltip("Levels above 1 for each extra customer the biggest party may bring.")]
        [Min(1)] public int levelsPerExtraGuest = 3;

        [Tooltip("Never more extra customers in a party than this from the level.")]
        [Min(0)] public int maxExtraGuestsFromLevel = 2;
    }
}
