#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using LastOrders.Consequence;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.DayCycle.States;
using LastOrders.Economy;
using LastOrders.Night;
using LastOrders.Reputation;
using LastOrders.Restaurant;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The zombie attack cycle and the Safety Reputation, without a scene: an attack every third day;
/// difficulty from the reputation, locked when the warning appears, climbing a step at a time and
/// lightened after defeats in a row; up to three short waves from different sides, using only what
/// progression has unlocked; the restaurant's health ending an attack; a cap on what the yard can
/// lose; one repair kit mending every wreck; one reputation result per attack; and customers
/// arriving more often, in bigger parties, the safer the restaurant is believed to be.
/// </summary>
public class EncounterTests
{
    readonly List<Object> owned = new List<Object>();

    GameBalanceConfig balance;
    EnemyDefinition shambler;
    EnemyDefinition runner;
    EnemyDefinition brute;
    EnemyDefinition screecher;

    [SetUp]
    public void SetUp()
    {
        balance = Make<GameBalanceConfig>();
        balance.heatWeights = Make<HeatWeightsConfig>();
        balance.encounter = new EncounterTuning();
        balance.safety = new SafetyReputationTuning();

        shambler = Enemy("Zombie", cost: 1f, day: 1);
        runner = Enemy("Runner", cost: 1.5f, day: 3);
        brute = Enemy("Brute", cost: 6f, day: 8);
        screecher = Enemy("Screecher", cost: 4f, day: 1);
        screecher.isNonCombatant = true;

        balance.enemies = new[] { shambler, runner, brute, screecher };
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);

        owned.Clear();
    }

    // --- Helpers ---------------------------------------------------------------------------

    T Make<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        owned.Add(asset);
        return asset;
    }

    EnemyDefinition Enemy(string name, float cost, int day)
    {
        EnemyDefinition enemy = Make<EnemyDefinition>();
        enemy.displayName = name;
        enemy.threatCost = cost;
        enemy.earliestDay = day;
        return enemy;
    }

    OutdoorWreck Wreckable(string name, Vector3 at)
    {
        var go = new GameObject(name);
        owned.Add(go);
        go.transform.position = at;
        go.AddComponent<BoxCollider>();

        OutdoorWreck wreck = go.AddComponent<OutdoorWreck>();
        wreck.Configure(30f, name);
        return wreck;
    }

    sealed class FakeWallet : IWallet
    {
        public string DisplayName => "Coins";
        public int Balance { get; set; }

        public bool TrySpend(int amount)
        {
            if (amount > Balance) return false;
            Balance -= amount;
            return true;
        }
    }

    sealed class EmptyState : IDayPhaseState
    {
        public EmptyState(DayPhase phase) => Phase = phase;
        public DayPhase Phase { get; }
        public void OnEnter(DayPhase previous) { }
        public void OnTick(float deltaSeconds) { }
        public void OnExit(DayPhase next) { }
    }

    static DayCycleMachine Machine(DayPhase phase)
    {
        var machine = new DayCycleMachine(new TimeService(6f));
        foreach (DayPhase each in System.Enum.GetValues(typeof(DayPhase))) machine.AddState(new EmptyState(each));
        machine.Start(phase);
        return machine;
    }

    sealed class Game
    {
        public ReputationService Reputation;
        public ResourceLedger Ledger;
        public RestaurantService Restaurant;
        public OutdoorDamageService Outdoor;
        public EncounterService Encounters;
        public FakeWallet Coins;
        public DayCycleMachine Machine;
        public int CoinsPaid;
        public int ExperiencePaid;
    }

    Game NewGame(float reputation = 30f, DayPhase phase = DayPhase.Day)
    {
        balance.safety.startingReputation = reputation;

        var game = new Game
        {
            Reputation = new ReputationService(balance),
            Ledger = new ResourceLedger(),
            Coins = new FakeWallet(),
            Machine = Machine(phase),
        };

        game.Restaurant = new RestaurantService(new RestaurantTuning(), game.Ledger, game.Coins, game.Machine, () => false);
        game.Restaurant.SetFootprint(Rect.MinMaxRect(10f, 15f, 30f, 29f), Vector2.down);

        game.Outdoor = new OutdoorDamageService(balance.encounter, game.Ledger, game.Coins, game.Machine,
                                                at => at.z < 15f && at.z > -3f, null);

        game.Encounters = new EncounterService(balance, game.Reputation, game.Ledger, game.Restaurant, game.Outdoor,
                                               coins => game.CoinsPaid += coins, xp => game.ExperiencePaid += xp);
        return game;
    }

    // --- The schedule --------------------------------------------------------------------------

    [Test]
    public void AnAttackEndsEveryThirdDayStartingOnTheThird()
    {
        EncounterTuning tuning = balance.encounter;

        Assert.IsFalse(tuning.IsAttackDay(1));
        Assert.IsFalse(tuning.IsAttackDay(2));
        Assert.IsTrue(tuning.IsAttackDay(3));
        Assert.IsFalse(tuning.IsAttackDay(4));
        Assert.IsFalse(tuning.IsAttackDay(5));
        Assert.IsTrue(tuning.IsAttackDay(6));
        Assert.IsTrue(tuning.IsAttackDay(9));

        Assert.AreEqual(3, tuning.NextAttackDay(1));
        Assert.AreEqual(3, tuning.NextAttackDay(3));
        Assert.AreEqual(6, tuning.NextAttackDay(4));
        Assert.AreEqual(9, tuning.NextAttackDay(7));
    }

    [Test]
    public void TheDaysUntilTheNextAttackCountDownToTonight()
    {
        Game game = NewGame();

        Assert.AreEqual(2, game.Encounters.DaysUntilAttack(1));
        Assert.AreEqual(1, game.Encounters.DaysUntilAttack(2));
        Assert.AreEqual(0, game.Encounters.DaysUntilAttack(3), "Zero means tonight.");
        Assert.AreEqual(2, game.Encounters.DaysUntilAttack(4));
    }

    // --- Difficulty -----------------------------------------------------------------------------

    [TestCase(0f, 0)]
    [TestCase(34f, 0)]
    [TestCase(35f, 1)]
    [TestCase(69f, 1)]
    [TestCase(70f, 2)]
    [TestCase(100f, 2)]
    public void ReputationSelectsTheThreatBand(float reputation, int band)
    {
        Assert.AreEqual(band, balance.encounter.TierFor(reputation));
        Assert.AreEqual(band, NewGame(reputation).Encounters.RequestFor(3).TierIndex,
                        "The first attack has nothing before it to limit the climb.");
    }

    [Test]
    public void HigherBandsSendMoreZombiesInMoreWavesOfMoreKinds()
    {
        WavePlan low = NewGame(10f).Encounters.Preview(9);
        WavePlan high = NewGame(90f).Encounters.Preview(9);

        Assert.Greater(high.EnemyCount, low.EnemyCount);
        Assert.Greater(high.Waves.Count, low.Waves.Count);
        Assert.Greater(high.Composition.Count, low.Composition.Count, "More varied attack combinations.");
        Assert.AreEqual("Low", low.ThreatName);
        Assert.AreEqual("High", high.ThreatName);
    }

    [Test]
    public void OnlyKindsProgressionHasUnlockedEverCome()
    {
        WavePlan early = NewGame(95f).Encounters.Preview(3);

        CollectionAssert.DoesNotContain(early.Composition, brute, "The Brute is not unlocked until day 8.");
        CollectionAssert.DoesNotContain(early.Composition, screecher, "Non-combatants never come: they would only hold a wave open.");

        foreach (WaveSpawn spawn in early.Spawns)
            Assert.LessOrEqual(spawn.Enemy.earliestDay, 3);
    }

    [Test]
    public void TheLowBandSendsOnlyTheBasics()
    {
        WavePlan low = NewGame(5f).Encounters.Preview(30);

        foreach (EnemyDefinition kind in low.Composition)
            Assert.LessOrEqual(kind.threatCost, balance.encounter.Tier(0).maxThreatCost);
    }

    [Test]
    public void UpToThreeShortWavesEachFromAnotherSide()
    {
        WavePlan plan = NewGame(90f).Encounters.Preview(12);

        Assert.LessOrEqual(plan.Waves.Count, 3);
        Assert.Greater(plan.Waves.Count, 1);

        int total = 0;
        for (int w = 0; w < plan.Waves.Count; w++)
        {
            total += plan.Waves[w].Count;
            if (w > 0) Assert.AreNotEqual(plan.Waves[w - 1].Lanes[0], plan.Waves[w].Lanes[0], "A different side every wave.");

            foreach (WaveSpawn spawn in plan.Waves[w].Spawns)
                Assert.LessOrEqual(spawn.TimeSeconds, balance.encounter.waveArrivalSeconds + 5f, "A short wave: everyone sets off within seconds.");
        }

        Assert.AreEqual(plan.EnemyCount, total, "Every zombie belongs to one wave.");
    }

    [Test]
    public void ThePreviewIsTheAttackThatComes()
    {
        Game game = NewGame(50f);

        WavePlan preview = game.Encounters.Preview(6);
        WavePlan locked = game.Encounters.Lock(6);

        Assert.AreEqual(preview.EnemyCount, locked.EnemyCount);
        for (int i = 0; i < preview.Spawns.Count; i++)
        {
            Assert.AreSame(preview.Spawns[i].Enemy, locked.Spawns[i].Enemy);
            Assert.AreEqual(preview.Spawns[i].Lane, locked.Spawns[i].Lane);
        }
    }

    [Test]
    public void DifficultyIsLockedWhenTheWarningAppears()
    {
        Game game = NewGame(10f);

        WavePlan locked = game.Encounters.Lock(3);
        int enemies = locked.EnemyCount;

        game.Reputation.Set(100f);

        Assert.AreSame(locked, game.Encounters.Current);
        Assert.AreEqual(enemies, game.Encounters.Current.EnemyCount, "Nothing done after the warning changes tonight.");
        Assert.AreEqual(0, game.Encounters.Current.Tier);
    }

    [Test]
    public void TheBandClimbsOnlyOneStepFromOneAttackToTheNext()
    {
        Game game = NewGame(5f);

        game.Encounters.Lock(3);
        game.Encounters.Resolve(3);
        Assert.AreEqual(0, game.Encounters.LastTier);

        game.Reputation.Set(100f);

        EncounterRequest next = game.Encounters.RequestFor(6);
        Assert.AreEqual(1, next.TierIndex, "From Low it can only rise to Medium, however safe the restaurant now looks.");
    }

    [Test]
    public void TheZombieCountGrowsOnlyALittleFromOneAttackToTheNext()
    {
        Game game = NewGame(5f);

        WavePlan first = game.Encounters.Lock(3);
        game.Encounters.Resolve(3);

        game.Reputation.Set(69f);
        EncounterRequest next = game.Encounters.RequestFor(6);

        Assert.LessOrEqual(next.Enemies, first.EnemyCount + balance.encounter.maxEnemyRisePerEncounter);
    }

    [Test]
    public void DefeatsInARowLightenTheNextAttacksUntilAVictory()
    {
        Game game = NewGame(50f);
        EncounterTuning tuning = balance.encounter;

        Assert.AreEqual(1f, tuning.ReliefAfter(0));
        Assert.AreEqual(1f, tuning.ReliefAfter(1), "One defeat is not a run of them.");
        Assert.AreEqual(0.75f, tuning.ReliefAfter(2), 1e-4f);
        Assert.AreEqual(0.5f, tuning.ReliefAfter(3), 1e-4f);
        Assert.AreEqual(0.5f, tuning.ReliefAfter(9), 1e-4f, "Never below the floor.");

        int before = game.Encounters.RequestFor(3).Enemies;

        for (int night = 0; night < 2; night++)
        {
            game.Encounters.Lock(3 + night * 3);
            game.Encounters.Fail();
            game.Encounters.Resolve(3 + night * 3);
        }

        Assert.AreEqual(2, game.Encounters.ConsecutiveDefeats);
        EncounterRequest lightened = game.Encounters.RequestFor(9);
        Assert.Less(lightened.Relief, 1f);
        Assert.Less(lightened.Enemies, before + balance.encounter.maxEnemyRisePerEncounter);

        game.Encounters.Lock(9);
        game.Encounters.Resolve(9);

        Assert.AreEqual(0, game.Encounters.ConsecutiveDefeats, "A victory ends the run.");
        Assert.AreEqual(1f, game.Encounters.RequestFor(12).Relief);
    }

    // --- The restaurant -------------------------------------------------------------------------

    [Test]
    public void TheRestaurantStartsEveryAttackAtFullHealthAndCarriesIt()
    {
        Game game = NewGame();
        RestaurantService restaurant = game.Restaurant;

        game.Encounters.Lock(3);
        Assert.AreEqual(balance.encounter.restaurantHealth, restaurant.Health);

        restaurant.TakeHit(100f, new Vector3(20f, 0f, 14f));
        Assert.AreEqual(balance.encounter.restaurantHealth - 100f, restaurant.Health, "Carried, not reset, from wave to wave.");

        game.Encounters.Resolve(3);
        game.Encounters.Lock(6);
        Assert.AreEqual(restaurant.MaxHealth, restaurant.Health, "Full again for the next attack.");
    }

    [Test]
    public void OutsideAnAttackABlowCostsTheRestaurantNothing()
    {
        Game game = NewGame();
        float before = game.Restaurant.Health;

        Assert.IsFalse(game.Restaurant.TakeHit(50f, new Vector3(20f, 0f, 14f)));
        Assert.AreEqual(before, game.Restaurant.Health);
    }

    [Test]
    public void TheRestaurantFallingEndsTheAttackAsADefeat()
    {
        Game game = NewGame();
        game.Encounters.Lock(3);

        bool defeated = false;
        game.Encounters.OnDefeated += () => defeated = true;

        game.Restaurant.TakeHit(balance.encounter.restaurantHealth + 10f, new Vector3(20f, 0f, 14f));

        Assert.IsTrue(defeated);
        Assert.IsTrue(game.Encounters.IsDefeated);
        Assert.AreEqual(0f, game.Restaurant.Health);
    }

    [Test]
    public void TheNightEndsTheMomentTheRestaurantFalls()
    {
        Game game = NewGame();
        var time = new TimeService(6f);
        var context = new DayCycleContext(balance, time, null, null, new ResourceLedger(), null, null, null,
                                          null, null, null, null, null, null, null)
        {
            Encounters = game.Encounters,
            TonightsWave = game.Encounters.Lock(3),
        };

        var machine = new DayCycleMachine(time);
        var night = new NightState(context, machine);
        machine.AddState(night);
        machine.AddState(new EmptyState(DayPhase.Resolution));
        machine.Start(DayPhase.Night);

        machine.Tick(0.25f);
        Assert.AreEqual(DayPhase.Night, machine.CurrentPhase);
        Assert.AreEqual(0, night.WaveIndex, "The first wave sets off at nightfall.");

        game.Restaurant.TakeHit(10000f, Vector3.zero);
        machine.Tick(0.25f);

        Assert.AreEqual(DayPhase.Resolution, machine.CurrentPhase, "Ended there and then, with waves still to come.");
    }

    [Test]
    public void WavesComeOneAfterAnotherAndTheLastBeatenEndsTheNight()
    {
        Game game = NewGame(10f);
        var time = new TimeService(6f);
        WavePlan plan = game.Encounters.Lock(3);
        var context = new DayCycleContext(balance, time, null, null, new ResourceLedger(), null, null, null,
                                          null, null, null, null, null, null, null)
        {
            Encounters = game.Encounters,
            TonightsWave = plan,
        };

        var machine = new DayCycleMachine(time);
        var night = new NightState(context, machine);
        machine.AddState(night);
        machine.AddState(new EmptyState(DayPhase.Resolution));

        var started = new List<int>();
        game.Encounters.OnWaveStarted += (index, total, wave) => started.Add(index);

        int spawned = 0;
        night.OnSpawnRequested += (_, __) => spawned++;

        machine.Start(DayPhase.Night);

        // Nothing registers, so every wave counts as beaten once it is all out.
        for (int i = 0; i < 400 && machine.CurrentPhase == DayPhase.Night; i++) machine.Tick(0.25f);

        Assert.AreEqual(DayPhase.Resolution, machine.CurrentPhase);
        Assert.AreEqual(plan.Waves.Count, started.Count);
        Assert.AreEqual(plan.EnemyCount, spawned);
        Assert.AreEqual(plan.Waves.Count, game.Encounters.WavesBeaten);
    }

    // --- The yard ------------------------------------------------------------------------------

    [Test]
    public void OnlyZombiesInsideTheWallsAreSentAtTheYard()
    {
        Game game = NewGame();
        OutdoorWreck coop = Wreckable("Coop", new Vector3(5f, 0f, 2f));
        game.Outdoor.Register(coop);
        game.Outdoor.BeginEncounter(2);

        object outside = new object(), inside = new object();

        Assert.IsFalse(game.Outdoor.TryAssign(outside, new Vector3(5f, 0f, -10f), out _), "Still beyond the wall.");
        Assert.IsTrue(game.Outdoor.TryAssign(inside, new Vector3(6f, 0f, 4f), out OutdoorWreck target));
        Assert.AreSame(coop, target);
    }

    [Test]
    public void TheYardCanLoseNoMoreThanTheCapAndThenItIsTheRestaurantsTurn()
    {
        Game game = NewGame();
        OutdoorWreck a = Wreckable("Coop", new Vector3(2f, 0f, 2f));
        OutdoorWreck b = Wreckable("Bed", new Vector3(6f, 0f, 2f));
        OutdoorWreck c = Wreckable("Turret", new Vector3(10f, 0f, 2f));
        foreach (OutdoorWreck wreck in new[] { a, b, c }) game.Outdoor.Register(wreck);

        game.Outdoor.BeginEncounter(cap: 1);

        bool capReached = false;
        game.Outdoor.OnCapReached += () => capReached = true;

        object first = new object(), second = new object();
        Assert.IsTrue(game.Outdoor.TryAssign(first, new Vector3(2f, 0f, 3f), out OutdoorWreck one));
        Assert.IsTrue(game.Outdoor.TryAssign(second, new Vector3(10f, 0f, 3f), out OutdoorWreck two));
        Assert.AreSame(one, two, "With room for one more wreck, everyone is sent at the same thing.");

        for (int i = 0; i < 10 && !one.IsWrecked; i++) game.Outdoor.Strike(first, one, 10f, Vector3.zero);

        Assert.IsTrue(one.IsWrecked);
        Assert.IsTrue(capReached);
        Assert.AreEqual(1, game.Outdoor.WrecksThisEncounter);
        Assert.IsFalse(game.Outdoor.StillWanted(second, two));
        Assert.IsFalse(game.Outdoor.TryAssign(new object(), new Vector3(10f, 0f, 3f), out _), "Nothing more is handed out.");
    }

    [Test]
    public void AWreckKeepsItsPlaceAndOneKitMendsEveryWreck()
    {
        Game game = NewGame();
        OutdoorWreck a = Wreckable("Coop", new Vector3(2f, 0f, 2f));
        OutdoorWreck b = Wreckable("Bed", new Vector3(6f, 0f, 2f));
        game.Outdoor.Register(a);
        game.Outdoor.Register(b);

        Vector3 placed = a.transform.position;
        a.Wreck();
        b.Wreck();

        Assert.AreEqual(2, game.Outdoor.WreckedCount);
        Assert.Less(Vector3.Distance(placed, a.transform.position), 0.5f, "Still where it was put.");

        game.Ledger.Add(ResourceType.RepairKit, 1);

        Assert.IsTrue(game.Outdoor.TryRepairAll(out string message), message);
        Assert.AreEqual(0, game.Outdoor.WreckedCount);
        Assert.AreEqual(0, game.Ledger.Get(ResourceType.RepairKit), "One kit, for all of them.");
        Assert.IsFalse(a.IsWrecked);
        Assert.AreEqual(a.MaxHealth, a.Health);
    }

    [Test]
    public void WithNoKitTheRepairBuysOneOrSaysWhatIsMissing()
    {
        Game game = NewGame();
        OutdoorWreck a = Wreckable("Coop", new Vector3(2f, 0f, 2f));
        game.Outdoor.Register(a);
        a.Wreck();

        game.Coins.Balance = 5;
        Assert.IsFalse(game.Outdoor.TryRepairAll(out string refused));
        StringAssert.Contains("Repair Kit", refused);
        Assert.IsTrue(a.IsWrecked);

        game.Coins.Balance = balance.encounter.repairKitCoinPrice;
        Assert.IsTrue(game.Outdoor.TryRepairAll(out _));
        Assert.AreEqual(0, game.Coins.Balance);
        Assert.IsFalse(a.IsWrecked);
    }

    [Test]
    public void NothingIsMendedWhileTheAttackIsOn()
    {
        Game game = NewGame(phase: DayPhase.Night);
        OutdoorWreck a = Wreckable("Coop", new Vector3(2f, 0f, 2f));
        game.Outdoor.Register(a);
        a.Wreck();
        game.Ledger.Add(ResourceType.RepairKit, 1);

        Assert.IsFalse(game.Outdoor.TryRepairAll(out _));
        Assert.IsTrue(a.IsWrecked);
    }

    // --- The Safety Reputation ------------------------------------------------------------------

    [Test]
    public void EachAttackLeavesOneResultOnTheMeter()
    {
        SafetyReputationTuning safety = balance.safety;

        Assert.AreEqual(EncounterResultKind.CleanVictory, ReputationService.Classify(false, false, 0));
        Assert.AreEqual(EncounterResultKind.BreachedVictory, ReputationService.Classify(false, true, 0));
        Assert.AreEqual(EncounterResultKind.WreckedVictory, ReputationService.Classify(false, true, 2));
        Assert.AreEqual(EncounterResultKind.Defeat, ReputationService.Classify(true, true, 2));

        var reputation = new ReputationService(balance);
        Assert.AreEqual(safety.cleanVictory, reputation.ResultDelta(EncounterResultKind.CleanVictory, 0f));
        Assert.AreEqual(safety.breachedVictory, reputation.ResultDelta(EncounterResultKind.BreachedVictory, 0f));
        Assert.AreEqual(safety.wreckedVictory, reputation.ResultDelta(EncounterResultKind.WreckedVictory, 0f));
        Assert.AreEqual(-safety.defeatPenalty, reputation.ResultDelta(EncounterResultKind.Defeat, 1f));

        Assert.Greater(safety.cleanVictory, safety.breachedVictory);
        Assert.Greater(safety.breachedVictory, safety.wreckedVictory);
    }

    [Test]
    public void AVictoryEarnsLessTheHarderTheRestaurantWasHit()
    {
        var reputation = new ReputationService(balance);

        float untouched = reputation.ResultDelta(EncounterResultKind.BreachedVictory, 0f);
        float battered = reputation.ResultDelta(EncounterResultKind.BreachedVictory, 0.9f);

        Assert.Less(battered, untouched);
        Assert.Greater(battered, 0f, "Still a gain: it was held.");
    }

    [Test]
    public void ADefeatCostsOneCappedPenaltyAndNoReward()
    {
        Game game = NewGame(40f);
        OutdoorWreck a = Wreckable("Coop", new Vector3(2f, 0f, 2f));
        game.Outdoor.Register(a);

        game.Encounters.Lock(3);
        game.Encounters.NoteBreach();
        game.Restaurant.TakeHit(100000f, Vector3.zero);

        EncounterResult result = game.Encounters.Resolve(3);

        Assert.AreEqual(EncounterOutcome.Defeat, result.Outcome);
        Assert.AreEqual("DEFENSE FAILED", result.Headline);
        Assert.AreEqual(-balance.safety.defeatPenalty, result.ReputationDelta, 1e-4f);
        Assert.AreEqual(40f - balance.safety.defeatPenalty, game.Reputation.Current, 1e-4f);
        Assert.AreEqual(0, game.CoinsPaid);
        Assert.AreEqual(0, game.ExperiencePaid);
        Assert.AreEqual(0, game.Ledger.Get(ResourceType.RepairKit));
    }

    [Test]
    public void AVictoryPaysItsBandsRewardAndAKit()
    {
        Game game = NewGame(10f);

        game.Encounters.Lock(3);
        EncounterResult result = game.Encounters.Resolve(3);

        ThreatTierTuning low = balance.encounter.Tier(0);
        Assert.AreEqual("VICTORY", result.Headline);
        Assert.AreEqual(EncounterResultKind.CleanVictory, result.Kind);
        Assert.AreEqual(low.victoryCoins, game.CoinsPaid);
        Assert.AreEqual(low.victoryExperience, game.ExperiencePaid);
        Assert.AreEqual(low.victoryRepairKits, game.Ledger.Get(ResourceType.RepairKit));
        Assert.AreEqual(10f + balance.safety.cleanVictory, game.Reputation.Current, 1e-4f);
    }

    [Test]
    public void TheMeterCanFallToZeroAndStaysWithinZeroToAHundred()
    {
        var reputation = new ReputationService(balance);

        reputation.Add(-1000f);
        Assert.AreEqual(0f, reputation.Current, "No permanent floor: a run of defeats can empty it.");

        reputation.Add(1000f);
        Assert.AreEqual(100f, reputation.Current);
    }

    [Test]
    public void ASaferRestaurantSeesCarsMoreOftenWithMorePeopleInThem()
    {
        var reputation = new ReputationService(balance);

        reputation.Set(0f);
        float atZero = reputation.ArrivalIntervalScale;
        Vector2Int partyAtZero = reputation.PartySizeRange;

        reputation.Set(100f);
        float atFull = reputation.ArrivalIntervalScale;
        Vector2Int partyAtFull = reputation.PartySizeRange;

        Assert.Greater(atZero, atFull, "Longer between cars at zero.");
        Assert.AreEqual(new Vector2Int(1, 2), partyAtZero, "Even at zero a car brings one or two.");
        Assert.Greater(partyAtFull.y, partyAtZero.y);
    }

    [Test]
    public void APartyNeverOutnumbersTheFreeChairs()
    {
        var reputation = new ReputationService(balance);
        reputation.Set(100f);

        for (float roll = 0f; roll <= 1f; roll += 0.1f)
        {
            int size = reputation.RollPartySize(room: 2, roll01: roll);
            Assert.GreaterOrEqual(size, 1);
            Assert.LessOrEqual(size, 2);
        }

        reputation.Set(0f);
        for (float roll = 0f; roll <= 1f; roll += 0.1f)
        {
            int size = reputation.RollPartySize(room: 8, roll01: roll);
            Assert.GreaterOrEqual(size, 1);
            Assert.LessOrEqual(size, 2);
        }
    }
}
#endif
