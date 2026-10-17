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
        public bool IsBigWave;
        public EncounterOutcome Outcome;
        public EncounterResultKind Kind;
        public string ThreatName;

        public int Waves;
        public int WavesBeaten;
        public int Enemies;

        public bool Breached;
        public int Wrecks;

        /// <summary>Defenses the zombies knocked out - a gun's bastion brought down, a turret on the ground smashed.</summary>
        public int DefensesKnockedOut;

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
                        if (Wrecks == 0)
                            return DefensesKnockedOut == 1 ? "The restaurant held. The zombies knocked out a defense."
                                                           : $"The restaurant held. The zombies knocked out {DefensesKnockedOut} defenses.";

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
    /// Zombies come every night: a small pack most nights, and every few days a big wave.
    ///
    /// Difficulty is decided by three things - the Safety Reputation, the player's level, and how
    /// damaged the compound still is - and decided once: when the warning appears, the threat band
    /// and the number of zombies are locked, and nothing the player does during the warning changes
    /// them. Safety and level push it up; damage left standing pulls it down, so a compound on its
    /// knees is given room to recover. Three rules keep it fair. The band may climb only one step
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

        /// <summary>The player's level. Level 1 when nothing is wired.</summary>
        public Func<int> PlayerLevel { get; set; }

        /// <summary>How damaged the compound is right now, 0 to 1. None when nothing is wired.</summary>
        public Func<float> ZoneDamage { get; set; }

        /// <summary>Defenses the zombies have knocked out in the attack under way. None when nothing is wired.</summary>
        public Func<int> DefensesKnockedOut { get; set; }

        // --- History, which the next attack is measured against ----------------------------------

        public int EncountersFought { get; private set; }

        /// <summary>Big waves fought: what a band's per-attack growth counts.</summary>
        public int BigWavesFought { get; private set; }

        public int ConsecutiveDefeats { get; private set; }

        /// <summary>The band the last attack came from, or -1 before the first.</summary>
        public int LastTier { get; private set; } = -1;

        /// <summary>How many zombies a big wave would have sent at the last attack, or -1 before the
        /// first. A nightly attack sends a share of it.</summary>
        public int LastEnemies { get; private set; } = -1;

        public EncounterResult LastResult { get; private set; }

        // --- The attack in hand ------------------------------------------------------------------

        /// <summary>True from the moment the warning appears until the morning settles it.</summary>
        public bool IsActive { get; private set; }

        /// <summary>The locked attack, while one is on.</summary>
        public WavePlan Current { get; private set; }

        /// <summary>What the locked attack was decided from.</summary>
        public EncounterRequest CurrentRequest { get; private set; }

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

        public bool IsBigWaveDay(int day) => Tuning.IsBigWaveDay(day);

        public int NextAttackDay(int day) => Tuning.NextAttackDay(day);

        public int NextBigWaveDay(int day) => Tuning.NextBigWaveDay(day);

        /// <summary>Whole days until the next attack: zero means tonight.</summary>
        public int DaysUntilAttack(int day) => Mathf.Max(0, NextAttackDay(day) - day);

        /// <summary>Whole days until the next big wave: zero means tonight.</summary>
        public int DaysUntilBigWave(int day) => Mathf.Max(0, NextBigWaveDay(day) - day);

        /// <summary>Seconds of warning before tonight's attack: shorter for a nightly one.</summary>
        public float PreparationSeconds(int day) => Tuning.PreparationSeconds(day);

        // --- Difficulty -----------------------------------------------------------------------------

        /// <summary>The level, as the attack is measured by it: 1 when nothing says otherwise.</summary>
        public int CurrentLevel => Mathf.Max(1, PlayerLevel != null ? PlayerLevel() : 1);

        /// <summary>How damaged the compound is, 0 to 1, as the attack is measured by it.</summary>
        public float CurrentZoneDamage => Mathf.Clamp01(ZoneDamage != null ? ZoneDamage() : 0f);

        /// <summary>The attack <paramref name="day"/> would bring, were it locked now. Pure: the
        /// morning's preview and the evening's lock agree unless the reputation, the level or the
        /// damage moved between them.</summary>
        public EncounterRequest RequestFor(int day)
        {
            EncounterTuning tuning = Tuning;
            ThreatScalingTuning scaling = tuning.Scaling;
            NightlyAttackTuning nightly = tuning.Nightly;

            bool big = tuning.IsBigWaveDay(day);
            float safety = reputation != null ? reputation.Current : 0f;
            int level = CurrentLevel;
            float damage = CurrentZoneDamage;

            // Safety and the level pick the band; damage still standing takes it down.
            float score = Mathf.Max(0f, safety + scaling.LevelBandPoints(level) - scaling.DamageBandPoints(damage));
            int tierIndex = tuning.TierFor(score);
            if (LastTier >= 0) tierIndex = Mathf.Min(tierIndex, LastTier + Mathf.Max(0, tuning.maxTierRisePerEncounter));

            ThreatTierTuning tier = tuning.Tier(tierIndex);

            // What a big wave would send: the band's number, more for every level, held to a small
            // rise on the last attack, lightened after defeats and with the compound still broken.
            int full = tier.EnemiesAfter(BigWavesFought) + scaling.LevelEnemies(level);
            if (LastEnemies > 0) full = Mathf.Min(full, LastEnemies + Mathf.Max(0, tuning.maxEnemyRisePerEncounter));

            float relief = tuning.ReliefAfter(ConsecutiveDefeats);
            if (relief < 1f) full = Mathf.Max(1, Mathf.RoundToInt(full * relief));

            float damageRelief = scaling.DamageRelief(damage);
            if (damageRelief < 1f) full = Mathf.Max(1, Mathf.RoundToInt(full * damageRelief));

            // A nightly attack is a share of it.
            int enemies = big
                ? full
                : Mathf.Max(Mathf.Min(nightly.minEnemies, full), Mathf.RoundToInt(full * nightly.enemyShare));

            return new EncounterRequest
            {
                Day = day,
                EncounterNumber = EncountersFought + 1,
                TierIndex = tierIndex,
                Tier = tier,
                Enemies = enemies,
                LaneCount = Mathf.Max(1, LaneCount),
                Relief = relief * damageRelief,
                Seed = balance.campaignSeed,
                IsBigWave = big,
                MaxWaves = big ? 0 : nightly.maxWaves,
                MaxTypesPerWave = big ? 0 : nightly.maxTypesPerWave,
                Safety = safety,
                PlayerLevel = level,
                ZoneDamage = damage,
                BandScore = score,
                FullEnemies = full,
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
            // Measured before the yard is made whole below: the damage the last attack left is part
            // of what tonight is decided from.
            CurrentRequest = RequestFor(day);
            Current = EncounterPlanner.Build(CurrentRequest, Tuning, Roster());

            IsActive = true;
            IsDefeated = false;
            Breached = false;
            WaveIndex = -1;
            WavesBeaten = 0;

            restaurant?.BeginEncounter(Tuning.restaurantHealth);
            outdoor?.BeginEncounter(CurrentRequest.IsBigWave ? Tuning.maxOutdoorWrecks : Tuning.Nightly.maxOutdoorWrecks);

            EncounterRequest r = CurrentRequest;
            Debug.Log($"[Last Orders] Attack {Current.EncounterNumber} locked for day {day} ({(r.IsBigWave ? "big wave" : "nightly")}): " +
                      $"{Current.ThreatName} threat, {Current.EnemyCount} zombies in {Current.Waves.Count} wave{(Current.Waves.Count == 1 ? "" : "s")}. " +
                      $"Safety {r.Safety:0}, level {r.PlayerLevel}, zone damage {r.ZoneDamage:P0} -> band score {r.BandScore:0}" +
                      (Current.Relief < 1f ? $"; lightened to {Current.Relief:P0} ({ConsecutiveDefeats} defeats in a row, damage)." : "."));

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
            int knockedOut = DefensesKnockedOut != null ? Mathf.Max(0, DefensesKnockedOut()) : 0;
            float health = restaurant != null ? restaurant.HealthFraction : 1f;

            // A defense knocked out is damage done, the same as a wreck in the yard.
            bool big = CurrentRequest.IsBigWave;
            EncounterResultKind kind = ReputationService.Classify(defeated, Breached, wrecks + knockedOut);
            float share = big ? 1f : Tuning.Nightly.reputationShare;
            float delta = reputation != null ? reputation.ApplyEncounterResult(kind, 1f - health, share) : 0f;

            var result = new EncounterResult
            {
                Day = day,
                EncounterNumber = Current.EncounterNumber,
                IsBigWave = big,
                Outcome = defeated ? EncounterOutcome.Defeat : EncounterOutcome.Victory,
                Kind = kind,
                ThreatName = Current.ThreatName,
                Waves = Current.Waves.Count,
                WavesBeaten = defeated ? WavesBeaten : Current.Waves.Count,
                Enemies = Current.EnemyCount,
                Breached = Breached,
                Wrecks = wrecks,
                DefensesKnockedOut = knockedOut,
                RestaurantHealthFraction = health,
                ReputationDelta = delta,
                ReputationAfter = reputation != null ? reputation.Current : 0f,
            };

            if (!defeated) PayVictory(result);

            EncountersFought++;
            if (big) BigWavesFought++;
            ConsecutiveDefeats = defeated ? ConsecutiveDefeats + 1 : 0;
            LastTier = Current.Tier;
            LastEnemies = Mathf.Max(Current.EnemyCount, CurrentRequest.FullEnemies);

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

            // A nightly attack pays a share of the band's reward and no kits: those are the big wave's.
            float share = result.IsBigWave ? 1f : Tuning.Nightly.rewardShare;
            result.Coins = Mathf.RoundToInt(tier.victoryCoins * share);
            result.Experience = Mathf.RoundToInt(tier.victoryExperience * share);
            result.RepairKits = result.IsBigWave ? tier.victoryRepairKits : 0;

            if (result.Coins > 0) payCoins?.Invoke(result.Coins);
            if (result.Experience > 0) addExperience?.Invoke(result.Experience);
            if (result.RepairKits > 0) ledger?.Add(ResourceType.RepairKit, result.RepairKits);
        }
    }
}
