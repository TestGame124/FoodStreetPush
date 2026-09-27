#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Defense;
using LastOrders.Economy;
using LastOrders.Progression;
using LastOrders.Survivors;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The rules of the defenses, without a scene: a tower fires only while a guard is posted, at
/// the design's 16 damage every 1.5 seconds within 6 m; it sees further than it shoots; guards
/// are posted, replaced and lost as the roster changes; the first tower is free and the rest
/// cost 60 Trade Tokens or premium, with no worker involved; and an upgrade and an automated,
/// ammo-fed turret both run through the same code.
/// </summary>
public class DefenseTests
{
    readonly List<Object> owned = new List<Object>();
    readonly List<IDefenseTarget> hostiles = new List<IDefenseTarget>();

    ProgressionService progression;
    ResourceLedger ledger;
    DayCycleMachine machine;
    SurvivorService survivors;
    FakeWallet gems;
    DefenseService defense;
    bool namedNight;

    SurvivorRoleDefinition guardRole;
    SurvivorRoleDefinition cookRole;
    WeaponDefinition pistol;
    WeaponDefinition rifle;
    DefenseDefinition tower;

    int nextSurvivor;

    [SetUp]
    public void SetUp()
    {
        hostiles.Clear();
        namedNight = false;
        nextSurvivor = 0;

        var balance = Make<GameBalanceConfig>();

        progression = new ProgressionService();
        ledger = new ResourceLedger();
        machine = new DayCycleMachine(new TimeService(6f));
        survivors = new SurvivorService(balance, ledger);
        gems = new FakeWallet();

        defense = new DefenseService(survivors, ledger, gems, progression, machine,
                                     () => hostiles, () => namedNight);

        guardRole = Role(SurvivorRole.Guard);
        cookRole = Role(SurvivorRole.Cook);

        pistol = Weapon("Pistol", 16f, 1.5f, 6f);
        rifle = Weapon("Rifle", 24f, 1.5f, 9f);

        tower = Make<DefenseDefinition>();
        tower.id = "guard_tower";
        tower.displayName = "Guard Tower";
        tower.requiresCrew = true;
        tower.crewRole = SurvivorRole.Guard;
        tower.buildPrice = new DefensePrice(20, new ResourceAmount(ResourceType.TradeToken, 60));
        tower.levels = new[]
        {
            new DefenseLevel { weapon = pistol, spotRangeMetres = 14f },
            new DefenseLevel
            {
                weapon = rifle,
                spotRangeMetres = 18f,
                upgradePrice = new DefensePrice(30, new ResourceAmount(ResourceType.TradeToken, 90)),
            },
        };
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

    SurvivorRoleDefinition Role(SurvivorRole role)
    {
        var asset = Make<SurvivorRoleDefinition>();
        asset.role = role;
        asset.displayName = role.ToString();
        return asset;
    }

    WeaponDefinition Weapon(string name, float damage, float interval, float range, int ammo = 0)
    {
        var asset = Make<WeaponDefinition>();
        asset.displayName = name;
        asset.damagePerShot = damage;
        asset.secondsBetweenShots = interval;
        asset.rangeMetres = range;
        asset.ammoPerShot = ammo;
        return asset;
    }

    Survivor Recruit(SurvivorRoleDefinition role, SurvivorTraitDefinition trait = null)
    {
        var survivor = new Survivor { Id = $"s{nextSurvivor}", Name = $"Survivor {nextSurvivor}", Role = role, Trait = trait };
        nextSurvivor++;
        survivors.Add(survivor);
        return survivor;
    }

    DefenseUnit BuiltTower(string id = "gate", Vector3 at = default) => defense.Register(tower, id, at, startsBuilt: true);

    FakeTarget Enemy(float x, float z = 0f, float health = 1000f)
    {
        var target = new FakeTarget { Position = new Vector3(x, 0f, z), Health = health };
        hostiles.Add(target);
        return target;
    }

    /// <summary>Runs the loop for a span in quarter-second steps. A quarter is exact in binary,
    /// so six of them are exactly the pistol's 1.5 seconds.</summary>
    void Run(float seconds)
    {
        int steps = Mathf.RoundToInt(seconds / 0.25f);
        for (int i = 0; i < steps; i++) defense.Tick(0.25f);
    }

    void Phase(DayPhase phase)
    {
        machine.AddState(new IdlePhase(phase));
        machine.ForceTo(phase);
    }

    sealed class IdlePhase : IDayPhaseState
    {
        public IdlePhase(DayPhase phase) => Phase = phase;
        public DayPhase Phase { get; }
        public void OnEnter(DayPhase previous) { }
        public void OnTick(float deltaSeconds) { }
        public void OnExit(DayPhase next) { }
    }

    sealed class FakeTarget : IDefenseTarget
    {
        public Vector3 Position { get; set; }
        public float Health;
        public int Hits;
        public bool IsAlive => Health > 0f;
        public string DisplayName => "Shambler";

        public void ApplyDamage(float amount)
        {
            Health -= amount;
            Hits++;
        }
    }

    sealed class FakeWallet : IPremiumWallet
    {
        public int Balance { get; set; }
        public string DisplayName => "Gems";

        public bool TrySpend(int amount)
        {
            if (amount > Balance) return false;
            Balance -= amount;
            return true;
        }
    }

    // --- Crew ------------------------------------------------------------------------------

    [Test]
    public void TowerWithNoGuardNeverFires()
    {
        DefenseUnit unit = BuiltTower();
        FakeTarget enemy = Enemy(3f);

        Run(10f);

        Assert.AreEqual(0, enemy.Hits);
        Assert.AreEqual(0, unit.ShotsFired);
        Assert.IsNull(unit.Target, "Nobody is up there to spot anything.");
        StringAssert.Contains("cannot fire", defense.Describe("gate"));
    }

    [Test]
    public void AvailableGuardIsPostedAutomatically()
    {
        DefenseUnit unit = BuiltTower();
        Survivor guard = Recruit(guardRole);

        defense.Tick(0.25f);

        Assert.AreSame(guard, unit.Crew);
        Assert.IsTrue(unit.CanFire);
    }

    [Test]
    public void OnlyAGuardCanStandWatch()
    {
        DefenseUnit unit = BuiltTower();
        Survivor cook = Recruit(cookRole);

        defense.Tick(0.25f);
        Assert.IsNull(unit.Crew);

        Assert.IsFalse(defense.Assign("gate", cook, out string reason));
        StringAssert.Contains("Only a Guard", reason);
    }

    [Test]
    public void EachGuardMansOneTowerAtATime()
    {
        DefenseUnit first = BuiltTower("a");
        DefenseUnit second = BuiltTower("b", new Vector3(20f, 0f, 0f));
        Recruit(guardRole);

        defense.Tick(0.25f);

        Assert.IsTrue(first.CanFire);
        Assert.IsFalse(second.CanFire);
    }

    [Test]
    public void InjuredGuardLeavesThePostAndAnotherTakesOver()
    {
        DefenseUnit unit = BuiltTower();
        Survivor first = Recruit(guardRole);
        Survivor relief = Recruit(guardRole);

        defense.Tick(0.25f);
        Assert.AreSame(first, unit.Crew);

        survivors.Injure(first, 2);
        defense.Tick(0.25f);

        Assert.AreSame(relief, unit.Crew);
        Assert.IsTrue(unit.CanFire);
    }

    [Test]
    public void GuardWhoIsSentAwayLeavesTheTowerSilent()
    {
        DefenseUnit unit = BuiltTower();
        Survivor guard = Recruit(guardRole);
        FakeTarget enemy = Enemy(3f);

        defense.Tick(0.25f);
        guard.AwayDaysRemaining = 2;
        int hitsBefore = enemy.Hits;

        Run(5f);

        Assert.IsNull(unit.Crew);
        Assert.AreEqual(hitsBefore, enemy.Hits);
    }

    [Test]
    public void NervousGuardFleesTheSiegeAndIsBackAtDawn()
    {
        var nervous = Make<SurvivorTraitDefinition>();
        nervous.fleesDuringSiege = true;

        DefenseUnit unit = BuiltTower();
        Survivor guard = Recruit(guardRole, nervous);

        defense.Tick(0.25f);
        Assert.AreSame(guard, unit.Crew);

        FakeTarget enemy = Enemy(3f);
        Phase(DayPhase.Night);
        Run(5f);

        Assert.AreSame(guard, unit.Crew, "Still posted there - just not at the post.");
        Assert.IsFalse(unit.CrewOnDuty);
        Assert.AreEqual(0, enemy.Hits);
        StringAssert.Contains("left their post", defense.Describe("gate"));

        Phase(DayPhase.Dawn);
        defense.Tick(0.25f);

        Assert.IsTrue(unit.CanFire);
        Assert.AreEqual(1, enemy.Hits);
    }

    [Test]
    public void SuperstitiousGuardSitsOutNamedNightsOnly()
    {
        var superstitious = Make<SurvivorTraitDefinition>();
        superstitious.refusesNamedNights = true;

        DefenseUnit unit = BuiltTower();
        Recruit(guardRole, superstitious);

        Phase(DayPhase.Night);
        defense.Tick(0.25f);
        Assert.IsTrue(unit.CanFire);

        namedNight = true;
        defense.Tick(0.25f);
        Assert.IsFalse(unit.CanFire);
    }

    // --- Firing ----------------------------------------------------------------------------

    [Test]
    public void GuardFiresSixteenDamageEveryOneAndAHalfSeconds()
    {
        DefenseUnit unit = BuiltTower();
        Recruit(guardRole);
        FakeTarget enemy = Enemy(4f);

        // Shots on the first step, 1.5 seconds after it and 3.0 seconds after it.
        Run(3.25f);

        Assert.AreEqual(3, unit.ShotsFired);
        Assert.AreEqual(1000f - 48f, enemy.Health, 0.001f);
    }

    [Test]
    public void FirstShotAtSomethingWalkingIntoRangeIsImmediate()
    {
        BuiltTower();
        Recruit(guardRole);

        // Idle for a long time first: the weapon must be ready, not banking forty shots.
        Run(60f);
        FakeTarget enemy = Enemy(5f);
        defense.Tick(0.25f);

        Assert.AreEqual(1, enemy.Hits);

        Run(1f);
        Assert.AreEqual(1, enemy.Hits, "The second shot waits the full 1.5 seconds.");
    }

    [Test]
    public void NothingBeyondSixMetresIsShot()
    {
        BuiltTower();
        Recruit(guardRole);
        FakeTarget far = Enemy(6.5f);

        Run(5f);
        Assert.AreEqual(0, far.Hits);

        far.Position = new Vector3(6f, 0f, 0f);
        defense.Tick(0.25f);
        Assert.AreEqual(1, far.Hits, "Exactly at the edge of range counts as in range.");
    }

    [Test]
    public void RangeIsMeasuredAlongTheGroundNotFromThePlatform()
    {
        BuiltTower();
        Recruit(guardRole);

        var below = new FakeTarget { Position = new Vector3(5f, -40f, 0f), Health = 100f };
        hostiles.Add(below);

        defense.Tick(0.25f);
        Assert.AreEqual(1, below.Hits);
    }

    [Test]
    public void ApproachingEnemyIsSpottedBeforeItIsInRange()
    {
        DefenseUnit unit = BuiltTower();
        Recruit(guardRole);

        int spotted = 0;
        unit.OnSpotted += _ => spotted++;

        FakeTarget enemy = Enemy(12f);
        Run(3f);

        Assert.AreEqual(1, spotted);
        Assert.AreSame(enemy, unit.Target, "Tracked from 12 m, though the pistol reaches 6.");
        Assert.AreEqual(0, enemy.Hits);

        enemy.Position = new Vector3(5.5f, 0f, 0f);
        defense.Tick(0.25f);
        Assert.AreEqual(1, enemy.Hits);
    }

    [Test]
    public void SpottingIsCalledOncePerApproachNotOncePerEnemy()
    {
        DefenseUnit unit = BuiltTower();
        Recruit(guardRole);

        int spotted = 0;
        unit.OnSpotted += _ => spotted++;

        Enemy(10f);
        Enemy(11f);
        Enemy(12f);
        Run(1f);
        Assert.AreEqual(1, spotted);

        hostiles.Clear();
        Run(1f);
        Enemy(13f);
        Run(1f);
        Assert.AreEqual(2, spotted, "A second wave after a quiet spell is a second warning.");
    }

    [Test]
    public void NearestEnemyIsShotFirstAndTheDeadAreIgnored()
    {
        BuiltTower();
        Recruit(guardRole);

        FakeTarget dead = Enemy(1f, health: 0f);
        FakeTarget far = Enemy(5f);
        FakeTarget near = Enemy(3f);

        defense.Tick(0.25f);

        Assert.AreEqual(0, dead.Hits);
        Assert.AreEqual(0, far.Hits);
        Assert.AreEqual(1, near.Hits);
    }

    [Test]
    public void ShamblerFallsToTwoPistolShots()
    {
        BuiltTower();
        Recruit(guardRole);
        FakeTarget shambler = Enemy(4f, health: 30f);

        Run(1.75f);

        Assert.IsFalse(shambler.IsAlive);
        Assert.AreEqual(2, shambler.Hits);
    }

    // --- Building --------------------------------------------------------------------------

    [Test]
    public void StartingTowerCostsNothing()
    {
        ledger.Add(ResourceType.TradeToken, 100);

        DefenseUnit unit = BuiltTower();

        Assert.IsTrue(unit.IsBuilt);
        Assert.AreEqual(100, ledger.Get(ResourceType.TradeToken));
    }

    [Test]
    public void AdditionalTowerCostsSixtyTradeTokensAndNeedsNoWorker()
    {
        DefenseUnit unit = defense.Register(tower, "east", Vector3.zero, startsBuilt: false);
        ledger.Add(ResourceType.TradeToken, 59);

        Assert.IsFalse(defense.TryBuild("east", DefensePayment.Resources, out string reason));
        StringAssert.Contains("Need 1 more Trade Tokens", reason);
        Assert.IsFalse(unit.IsBuilt);

        ledger.Add(ResourceType.TradeToken, 1);

        // Built by paying - there is no job, and nothing has to tick for it to stand.
        Assert.IsTrue(defense.TryBuild("east", DefensePayment.Resources, out string message), message);
        Assert.IsTrue(unit.IsBuilt);
        Assert.AreEqual(0, ledger.Get(ResourceType.TradeToken));
        Assert.IsTrue(progression.Has(DefenseIds.Built("east")));
    }

    [Test]
    public void AdditionalTowerCanBeBoughtWithPremiumInstead()
    {
        DefenseUnit unit = defense.Register(tower, "east", Vector3.zero, startsBuilt: false);
        ledger.Add(ResourceType.TradeToken, 10);
        gems.Balance = 25;

        Assert.IsTrue(defense.TryBuild("east", DefensePayment.Premium, out string message), message);

        Assert.IsTrue(unit.IsBuilt);
        Assert.AreEqual(5, gems.Balance);
        Assert.AreEqual(10, ledger.Get(ResourceType.TradeToken), "Paying premium leaves the tokens alone.");
    }

    [Test]
    public void OfferListsBothWaysToPay()
    {
        defense.Register(tower, "east", Vector3.zero, startsBuilt: false);
        gems.Balance = 20;

        DefenseOffer offer = defense.GetBuildOffer("east");

        Assert.IsNull(offer.Blocked);
        Assert.AreEqual(2, offer.Payments.Count);
        Assert.AreEqual("60 Trade Tokens", offer.Payments[0].Label);
        Assert.AreEqual("20 Gems", offer.Payments[1].Label);
        Assert.IsFalse(offer.CanBuy(DefensePayment.Resources));
        Assert.IsTrue(offer.CanBuy(DefensePayment.Premium));
    }

    [Test]
    public void BuyingTheSameSiteTwiceChargesOnce()
    {
        defense.Register(tower, "east", Vector3.zero, startsBuilt: false);
        ledger.Add(ResourceType.TradeToken, 200);

        Assert.IsTrue(defense.TryBuild("east", DefensePayment.Resources, out _));
        Assert.IsFalse(defense.TryBuild("east", DefensePayment.Resources, out string reason));

        StringAssert.Contains("already", reason);
        Assert.AreEqual(140, ledger.Get(ResourceType.TradeToken));
    }

    [Test]
    public void NoTowerIsBoughtAfterDark()
    {
        defense.Register(tower, "east", Vector3.zero, startsBuilt: false);
        ledger.Add(ResourceType.TradeToken, 60);
        Phase(DayPhase.Dusk);

        Assert.IsFalse(defense.TryBuild("east", DefensePayment.Resources, out string reason));

        StringAssert.Contains("dark", reason);
        Assert.AreEqual(60, ledger.Get(ResourceType.TradeToken));
    }

    [Test]
    public void BoughtTowerIsStillThereAfterAReload()
    {
        defense.Register(tower, "east", Vector3.zero, startsBuilt: false);
        ledger.Add(ResourceType.TradeToken, 60);
        Assert.IsTrue(defense.TryBuild("east", DefensePayment.Resources, out _));

        defense.Unregister("east");
        DefenseUnit again = defense.Register(tower, "east", Vector3.zero, startsBuilt: false);

        Assert.IsTrue(again.IsBuilt);
    }

    // --- Upgrading and other kinds of defense ---------------------------------------------

    [Test]
    public void UpgradeHandsTheGuardARifle()
    {
        DefenseUnit unit = BuiltTower();
        Recruit(guardRole);
        FakeTarget enemy = Enemy(8f);

        Run(3f);
        Assert.AreEqual(0, enemy.Hits, "Out of the pistol's reach.");

        ledger.Add(ResourceType.TradeToken, 90);
        Assert.IsTrue(defense.TryUpgrade("gate", DefensePayment.Resources, out string message), message);

        Assert.AreEqual(2, unit.LevelNumber);
        Assert.AreSame(rifle, unit.Weapon);

        defense.Tick(0.25f);
        Assert.AreEqual(1, enemy.Hits);
        Assert.AreEqual(1000f - 24f, enemy.Health, 0.001f);

        Assert.IsFalse(defense.TryUpgrade("gate", DefensePayment.Resources, out string reason));
        StringAssert.Contains("highest level", reason);
    }

    [Test]
    public void AutomatedTurretFiresWithNobodyPostedAndStopsWhenOutOfAmmo()
    {
        var turret = Make<DefenseDefinition>();
        turret.displayName = "Machine Gun Turret";
        turret.requiresCrew = false;
        turret.levels = new[]
        {
            new DefenseLevel { weapon = Weapon("Machine Gun", 5f, 0.25f, 7f, ammo: 1), spotRangeMetres = 10f },
        };

        DefenseUnit unit = defense.Register(turret, "turret", Vector3.zero, startsBuilt: true);
        ledger.Add(ResourceType.Ammo, 3);
        FakeTarget enemy = Enemy(4f);

        Run(2f);

        Assert.AreEqual(3, enemy.Hits);
        Assert.AreEqual(0, ledger.Get(ResourceType.Ammo));
        Assert.IsTrue(unit.IsOutOfAmmo);
        StringAssert.Contains("out of Ammo", defense.Describe("turret"));

        ledger.Add(ResourceType.Ammo, 1);
        defense.Tick(0.25f);
        Assert.AreEqual(4, enemy.Hits);
    }
}
#endif
