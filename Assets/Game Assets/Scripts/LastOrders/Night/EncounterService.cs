using System;
using System.Collections.Generic;
using System.Text;
using LastOrders.Consequence;
using LastOrders.Data;
using LastOrders.Economy;
using LastOrders.Reputation;
using LastOrders.Restaurant;
using UnityEngine;

namespace LastOrders.Night
{
    public enum EncounterOutcome
    {
        Victory = 0,
        Defeat = 1,
    }

    /// <summary>How one attack went, and what it paid or cost. What the morning banner shows.</summary>
    public sealed class EncounterResult
    {
        public int Day;
        public int EncounterNumber;
        public EncounterOutcome Outcome;
        public EncounterResultKind Kind;
        public string ThreatName;

        public int Waves;
        public int WavesBeaten;
        public int Enemies;

        public bool Breached;
        public int Wrecks;
        public float RestaurantHealthFraction = 1f;

        public float ReputationDelta;
        public float ReputationAfter;

        public int Coins;
        public int Experience;
        public int RepairKits;

        public bool IsVictory => Outcome == EncounterOutcome.Victory;

        public string Headline => IsVictory ? "VICTORY" : "DEFENSE FAILED";

        /// <summary>One line on how it went, in the player's words.</summary>
        public string Summary
        {
            get
            {
                switch (Kind)
                {
                    case EncounterResultKind.CleanVictory: return "Not one zombie got inside the walls.";
                    case EncounterResultKind.BreachedVictory: return "Zombies got inside the walls, but nothing was wrecked.";
                    case EncounterResultKind.WreckedVictory:
                        return Wrecks == 1 ? "The restaurant held. One thing in the yard was wrecked."
                                           : $"The restaurant held. {Wrecks} things in the yard were wrecked.";
                    default: return "The zombies broke into the restaurant.";
                }
            }
        }

        /// <summary>"+40 Coins  +20 XP  +1 Repair Kit", or "No reward".</summary>
        public string RewardLine
        {
            get
            {
                if (!IsVictory) return "No reward";

                var parts = new List<string>();
                if (Coins > 0) parts.Add($"+{Coins} Coins");
                if (Experience > 0) parts.Add($"+{Experience} XP");
                if (RepairKits > 0) parts.Add(RepairKits == 1 ? "+1 Repair Kit" : $"+{RepairKits} Repair Kits");

                return parts.Count > 0 ? string.Join("   ", parts) : "No reward";
            }
        }

        /// <summary>"Safety +6", "Safety -10".</summary>
        public string ReputationLine
        {
            get
            {
                float rounded = Mathf.Round(ReputationDelta * 10f) / 10f;
                string sign = rounded > 0f ? "+" : rounded < 0f ? "-" : "±";
                return $"Safety {sign}{Mathf.Abs(rounded):0.#}  ({Mathf.RoundToInt(ReputationAfter)}/100)";
            }
        }
    }

    /// <summary>
    /// The zombie attack cycle as the rules see it: which days end in an attack, how hard the next
    /// one is, what happened during it, and what it was worth.
    ///
    /// Difficulty is decided by the Safety Reputation, and decided once: when the warning appears,
    /// the threat band and the number of zombies are locked, and nothing the player does in the
    /// next sixty seconds changes them. Three rules keep it fair. The band may climb only one step
    /// from one attack to the next; the number of zombies may grow only a little; and after defeats
    /// in a row the next attacks are lightened until the restaurant wins one again.
    ///
    /// A plain class with no scene in it. The night tells it about waves and breaches, the
    /// restaurant tells it when its health runs out, and it settles the result in the morning.
    /// </summary>
    public sealed class EncounterService
    {
        readonly GameBalanceConfig balance;
        readonly ReputationService reputation;
        readonly ResourceLedger ledger;
        readonly RestaurantService restaurant;
        readonly OutdoorDamageService outdoor;
        readonly Action<int> payCoins;
        readonly Action<int> addExperience;

        public EncounterService(GameBalanceConfig balance, ReputationService reputation, ResourceLedger ledger,
                                RestaurantService restaurant, OutdoorDamageService outdoor,
                                Action<int> payCoins, Action<int> addExperience)
        {
            this.balance = balance ?? throw new ArgumentNullException(nameof(balance));
            this.reputation = reputation;
            this.ledger = ledger;
            this.restaurant = restaurant;
            this.outdoor = outdoor;
            this.payCoins = payCoins;
            this.addExperience = addExperience;

            if (restaurant != null) restaurant.OnHealthDepleted += Fail;
        }

        public EncounterTuning Tuning => balance.encounter ?? (balance.encounter = new EncounterTuning());

        /// <summary>Sides of the compound the attacks can come from. The spawner says how many it has.</summary>
        public int LaneCount { get; set; } = 4;

        /// <summary>Each side in the player's words - "the west" - as the spawner names them.</summary>
        public string[] LaneNames { get; set; }

        // --- History, which the next attack is measured against ----------------------------------

        public int EncountersFought { get; private set; }

        public int ConsecutiveDefeats { get; private set; }

        /// <summary>The band the last attack came from, or -1 before the first.</summary>
        public int LastTier { get; private set; } = -1;

        /// <summary>How many zombies the last attack sent, or -1 before the first.</summary>
        public int LastEnemies { get; private set; } = -1;

        public EncounterResult LastResult { get; private set; }

        // --- The attack in hand ------------------------------------------------------------------

        /// <summary>True from the moment the warning appears until the morning settles it.</summary>
        public bool IsActive { get; private set; }

        /// <summary>The locked attack, while one is on.</summary>
        public WavePlan Current { get; private set; }

        /// <summary>The restaurant's health ran out: the attack is lost and over.</summary>
        public bool IsDefeated { get; private set; }

        /// <summary>A zombie got inside the walls at some point tonight.</summary>
        public bool Breached { get; private set; }

        /// <summary>The wave under way, from 0, or -1 before the first sets off.</summary>
        public int WaveIndex { get; private set; } = -1;

        public int WavesBeaten { get; private set; }

        /// <summary>The attack is locked in and its warning has appeared.</summary>
        public event Action<WavePlan> OnLocked;

        /// <summary>A wave has set off: its index, how many waves there are, and the wave.</summary>
        public event Action<int, int, EncounterWave> OnWaveStarted;

        /// <summary>The restaurant fell.</summary>
        public event Action OnDefeated;

        /// <summary>The morning has settled the attack.</summary>
        public event Action<EncounterResult> OnResolved;

        // --- Schedule ----------------------------------------------------------------------------

        public bool IsAttackDay(int day) => Tuning.IsAttackDay(day);

        public int NextAttackDay(int day) => Tuning.NextAttackDay(day);

        /// <summary>Whole days until the next attack: zero means tonight.</summary>
        public int DaysUntilAttack(int day) => Mathf.Max(0, NextAttackDay(day) - day);

        // --- Difficulty -----------------------------------------------------------------------------

        /// <summary>The attack <paramref name="day"/> would bring, were it locked now. Pure: the
        /// morning's preview and the evening's lock agree unless the reputation moved between them.</summary>
        public EncounterRequest RequestFor(int day)
        {
            EncounterTuning tuning = Tuning;
            float safety = reputation != null ? reputation.Current : 0f;

            int tierIndex = tuning.TierFor(safety);
            if (LastTier >= 0) tierIndex = Mathf.Min(tierIndex, LastTier + Mathf.Max(0, tuning.maxTierRisePerEncounter));

            ThreatTierTuning tier = tuning.Tier(tierIndex);

            int enemies = tier.EnemiesAfter(EncountersFought);
            if (LastEnemies > 0) enemies = Mathf.Min(enemies, LastEnemies + Mathf.Max(0, tuning.maxEnemyRisePerEncounter));

            float relief = tuning.ReliefAfter(ConsecutiveDefeats);
            if (relief < 1f) enemies = Mathf.Max(1, Mathf.RoundToInt(enemies * relief));

            return new EncounterRequest
            {
                Day = day,
                EncounterNumber = EncountersFought + 1,
                TierIndex = tierIndex,
                Tier = tier,
                Enemies = enemies,
                LaneCount = Mathf.Max(1, LaneCount),
                Relief = relief,
                Seed = balance.campaignSeed,
            };
        }

        public WavePlan Preview(int day) => EncounterPlanner.Build(RequestFor(day), Tuning, Roster());

        IReadOnlyList<EnemyDefinition> Roster() => balance.enemies ?? Array.Empty<EnemyDefinition>();

        /// <summary>
        /// The warning has appeared: the attack is decided and cannot change. The restaurant goes
        /// into it at full health, and the yard is found and counted.
        /// </summary>
        public WavePlan Lock(int day)
        {
            Current = Preview(day);

            IsActive = true;
            IsDefeated = false;
            Breached = false;
            WaveIndex = -1;
            WavesBeaten = 0;

            restaurant?.BeginEncounter(Tuning.restaurantHealth);
            outdoor?.BeginEncounter(Tuning.maxOutdoorWrecks);

            Debug.Log($"[Last Orders] Attack {Current.EncounterNumber} locked for day {day}: {Current.ThreatName} threat, " +
                      $"{Current.EnemyCount} zombies in {Current.Waves.Count} wave{(Current.Waves.Count == 1 ? "" : "s")}" +
                      (Current.Relief < 1f ? $" (lightened to {Current.Relief:P0} after {ConsecutiveDefeats} defeats)." : "."));

            OnLocked?.Invoke(Current);
            return Current;
        }

        // --- During the attack ----------------------------------------------------------------------

        public void NoteWaveStarted(int index)
        {
            if (!IsActive || Current == null || index < 0 || index >= Current.Waves.Count) return;

            WaveIndex = index;
            OnWaveStarted?.Invoke(index, Current.Waves.Count, Current.Waves[index]);
        }

        public void NoteWaveBeaten(int index)
        {
            if (IsActive) WavesBeaten = Mathf.Max(WavesBeaten, index + 1);
        }

        public void NoteBreach()
        {
            if (IsActive && !IsDefeated) Breached = true;
        }

        /// <summary>The restaurant fell. The attack ends there and then.</summary>
        public void Fail()
        {
            if (!IsActive || IsDefeated) return;

            IsDefeated = true;
            Debug.Log("[Last Orders] The restaurant fell. Defense failed.");
            OnDefeated?.Invoke();
        }

        /// <summary>The side a lane comes from, in the player's words: "the west".</summary>
        public string SideName(int lane)
        {
            string[] names = LaneNames;
            if (names != null && lane >= 0 && lane < names.Length && !string.IsNullOrEmpty(names[lane]))
                return $"the {names[lane].ToLowerInvariant()}";

            return "the road";
        }

        /// <summary>"from the west and the north".</summary>
        public string SidesOf(EncounterWave wave)
        {
            if (wave == null || wave.Lanes.Count == 0) return "the road";

            var text = new StringBuilder(SideName(wave.Lanes[0]));
            for (int i = 1; i < wave.Lanes.Count; i++) text.Append(" and ").Append(SideName(wave.Lanes[i]));
            return text.ToString();
        }

        // --- The morning -----------------------------------------------------------------------

        /// <summary>
        /// Settles the attack: one result on the Safety Reputation, the victory reward or nothing,
        /// and the history the next attack is measured against. Null when no attack was on.
        /// </summary>
        public EncounterResult Resolve(int day)
        {
            if (!IsActive || Current == null) return null;

            bool defeated = IsDefeated;
            int wrecks = outdoor != null ? outdoor.WrecksThisEncounter : 0;
            float health = restaurant != null ? restaurant.HealthFraction : 1f;

            EncounterResultKind kind = ReputationService.Classify(defeated, Breached, wrecks);
            float delta = reputation != null ? reputation.ApplyEncounterResult(kind, 1f - health) : 0f;

            var result = new EncounterResult
            {
                Day = day,
                EncounterNumber = Current.EncounterNumber,
                Outcome = defeated ? EncounterOutcome.Defeat : EncounterOutcome.Victory,
                Kind = kind,
                ThreatName = Current.ThreatName,
                Waves = Current.Waves.Count,
                WavesBeaten = defeated ? WavesBeaten : Current.Waves.Count,
                Enemies = Current.EnemyCount,
                Breached = Breached,
                Wrecks = wrecks,
                RestaurantHealthFraction = health,
                ReputationDelta = delta,
                ReputationAfter = reputation != null ? reputation.Current : 0f,
            };

            if (!defeated) PayVictory(result);

            EncountersFought++;
            ConsecutiveDefeats = defeated ? ConsecutiveDefeats + 1 : 0;
            LastTier = Current.Tier;
            LastEnemies = Current.EnemyCount;

            IsActive = false;
            outdoor?.EndEncounter();
            restaurant?.EndEncounter();

            // Dents are knocked out overnight; only wrecks wait for a repair kit.
            outdoor?.MendDents();

            Debug.Log($"[Last Orders] Attack {result.EncounterNumber}: {result.Headline}. {result.Summary} " +
                      $"{result.RewardLine}. {result.ReputationLine}.");

            LastResult = result;
            OnResolved?.Invoke(result);
            return result;
        }

        void PayVictory(EncounterResult result)
        {
            ThreatTierTuning tier = Tuning.Tier(Current.Tier);

            result.Coins = tier.victoryCoins;
            result.Experience = tier.victoryExperience;
            result.RepairKits = tier.victoryRepairKits;

            if (result.Coins > 0) payCoins?.Invoke(result.Coins);
            if (result.Experience > 0) addExperience?.Invoke(result.Experience);
            if (result.RepairKits > 0) ledger?.Add(ResourceType.RepairKit, result.RepairKits);
        }
    }
}
