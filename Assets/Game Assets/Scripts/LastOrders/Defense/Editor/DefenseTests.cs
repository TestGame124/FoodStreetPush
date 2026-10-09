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

    // --- The starting scrap turret ---------------------------------------------------------

    /// <summary>The turret's content as generated, or the test is skipped when it has not been made yet.</summary>
    static DefenseDefinition ScrapTurretAsset()
    {
        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<DefenseDefinition>(LastOrders.EditorTools.DefenseContentGenerator.ScrapTurretPath);
        if (asset == null) Assert.Inconclusive("The scrap turret content has not been generated in this project.");
        return asset;
    }

    [Test]
    public void StartingTurretShootsWithNobodyPostedAndNoAmmoInTheStores()
    {
        DefenseDefinition turret = ScrapTurretAsset();
        DefenseUnit unit = defense.Register(turret, "turret", Vector3.zero, startsBuilt: true);

        WeaponDefinition gun = unit.Weapon;
        Assert.IsNotNull(gun, "Level 1 has a gun.");
        Assert.AreEqual(0, ledger.Get(ResourceType.Ammo));

        FakeTarget inRange = Enemy(gun.rangeMetres - 0.5f);
        defense.Tick(0.25f);

        Assert.IsTrue(unit.CanFire, "Nobody needs to be posted to it.");
        Assert.IsNull(unit.Crew);
        Assert.AreEqual(1, inRange.Hits, "The first shot at something already in range is immediate.");
        Assert.IsFalse(unit.IsOutOfAmmo);
    }

    [Test]
    public void StartingTurretTracksBeforeItCanShootAndIgnoresWhatItCannotSee()
    {
        DefenseDefinition turret = ScrapTurretAsset();
        DefenseUnit unit = defense.Register(turret, "turret", Vector3.zero, startsBuilt: true);
        DefenseLevel level = unit.CurrentLevel;

        Assert.Greater(level.SpotRange, unit.Weapon.rangeMetres, "It sees further than it shoots.");

        FakeTarget beyondSight = Enemy(level.SpotRange + 1f);
        Run(2f);
        Assert.IsNull(unit.Target);
        Assert.AreEqual(0, beyondSight.Hits);

        FakeTarget spotted = Enemy((level.SpotRange + unit.Weapon.rangeMetres) * 0.5f);
        Run(2f);
        Assert.AreSame(spotted, unit.Target, "Tracked from the moment it is seen.");
        Assert.AreEqual(0, spotted.Hits, "But not shot until it is in range.");
    }

    [Test]
    public void TurretBarrelsAreHomeBeforeEveryLevelsNextShot()
    {
        DefenseDefinition turret = ScrapTurretAsset();
        var motion = UnityEditor.AssetDatabase.LoadAssetAtPath<DefenseMotionProfile>(
            LastOrders.EditorTools.DefenseContentGenerator.ScrapTurretMotionPath);
        Assert.IsNotNull(motion, "The turret has a motion profile.");

        for (int i = 0; i < turret.LevelCount; i++)
        {
            WeaponDefinition gun = turret.levels[i].weapon;
            Assert.Less(motion.recoilReturnSeconds, gun.secondsBetweenShots,
                        $"Level {i + 1}'s {gun.displayName} fires again before the barrels are home.");
            Assert.Less(motion.flashSeconds, gun.secondsBetweenShots, $"Level {i + 1}'s flash never goes out.");
        }
    }

    // --- Thrown shots ------------------------------------------------------------------------

    DefenseUnit Thrower(float speed, float damage = 10f, float range = 8f)
    {
        var lobber = Make<DefenseDefinition>();
        lobber.displayName = "Lobber";
        lobber.requiresCrew = false;
        lobber.levels = new[]
        {
            new DefenseLevel { weapon = Weapon("Lob", damage, 2f, range), spotRangeMetres = 10f },
        };
        lobber.levels[0].weapon.projectileSpeed = speed;
        return defense.Register(lobber, "lobber", Vector3.zero, startsBuilt: true);
    }

    [Test]
    public void ThrownShotLandsWhenItArrivesNotWhenItIsFired()
    {
        DefenseUnit unit = Thrower(speed: 4f);
        FakeTarget enemy = Enemy(4f);

        defense.Tick(0.25f);
        Assert.AreEqual(1, unit.ShotsFired);
        Assert.AreEqual(1, unit.ShotsInFlight, "4 m at 4 m/s: a second in the air.");
        Assert.AreEqual(0, enemy.Hits);

        Run(0.5f);
        Assert.AreEqual(0, enemy.Hits, "Still on its way three quarters of a second in.");

        Run(0.5f);
        Assert.AreEqual(1, enemy.Hits);
        Assert.AreEqual(1000f - 10f, enemy.Health, 0.001f);
        Assert.AreEqual(0, unit.ShotsInFlight);
    }

    [Test]
    public void ThrownShotAtSomethingThatDiesOnTheWayIsWasted()
    {
        DefenseUnit unit = Thrower(speed: 2f);
        FakeTarget enemy = Enemy(4f);

        defense.Tick(0.25f);
        Assert.AreEqual(1, unit.ShotsInFlight);

        // Killed by something else while the shot is in the air.
        enemy.Health = 0f;
        Run(2.5f);

        Assert.AreEqual(0, enemy.Hits);
        Assert.AreEqual(0, unit.ShotsInFlight);
    }

    [Test]
    public void ThrownShotStillLandsIfTheThrowerCannotFireAnyMore()
    {
        var lobber = Make<DefenseDefinition>();
        lobber.requiresCrew = true;
        lobber.crewRole = SurvivorRole.Guard;
        lobber.levels = new[] { new DefenseLevel { weapon = Weapon("Lob", 10f, 2f, 8f), spotRangeMetres = 10f } };
        lobber.levels[0].weapon.projectileSpeed = 4f;

        Survivor guard = Recruit(guardRole);
        DefenseUnit unit = defense.Register(lobber, "lobber", Vector3.zero, startsBuilt: true);
        FakeTarget enemy = Enemy(4f);

        defense.Tick(0.25f);
        Assert.AreEqual(1, unit.ShotsInFlight);

        // The guard is sent away with the shot in the air, and nobody else can take the post.
        guard.AwayDaysRemaining = 2;
        Run(1f);

        Assert.IsFalse(unit.CanFire);
        Assert.AreEqual(1, enemy.Hits, "What is already thrown lands.");
    }

    [Test]
    public void LobbedWeaponSaysSoAndHitscanLandsAtOnce()
    {
        WeaponDefinition lob = Weapon("Lob", 10f, 2f, 8f);
        lob.projectileSpeed = 5f;

        StringAssert.Contains("lobbed", lob.Describe());
        Assert.AreEqual(1f, lob.FlightSeconds(5f), 1e-5f);
        Assert.AreEqual(0f, pistol.FlightSeconds(5f), "Hitscan has no flight.");
        StringAssert.DoesNotContain("lobbed", pistol.Describe());
    }

    [Test]
    public void ToastapultLobsToastWithNobodyPosted()
    {
        var toaster = UnityEditor.AssetDatabase.LoadAssetAtPath<DefenseDefinition>(LastOrders.EditorTools.DefenseContentGenerator.ToastapultPath);
        if (toaster == null) Assert.Inconclusive("The Toastapult content has not been generated in this project.");

        DefenseUnit unit = defense.Register(toaster, "toaster", Vector3.zero, startsBuilt: true);
        WeaponDefinition toast = unit.Weapon;
        Assert.IsTrue(toast.IsProjectile, "It throws its toast.");

        FakeTarget enemy = Enemy(toast.rangeMetres - 1f);
        defense.Tick(0.25f);

        Assert.IsTrue(unit.CanFire);
        Assert.IsNull(unit.Crew);
        Assert.AreEqual(1, unit.ShotsFired);
        Assert.AreEqual(0, enemy.Hits, "Not landed yet.");

        Run(toast.FlightSeconds(toast.rangeMetres) + 0.5f);
        Assert.GreaterOrEqual(enemy.Hits, 1);
    }

    // --- Where a shot lands: splash and slow -----------------------------------------------------

    sealed class SlowTarget : IDefenseTarget, ISlowable
    {
        public Vector3 Position { get; set; }
        public float Health = 1000f;
        public float SlowedTo = 1f;
        public float SlowedFor;
        public bool IsAlive => Health > 0f;
        public string DisplayName => "Zombie";
        public void ApplyDamage(float amount) => Health -= amount;

        public void Slow(float multiplier, float seconds)
        {
            SlowedTo = Mathf.Min(SlowedTo, multiplier);
            SlowedFor = Mathf.Max(SlowedFor, seconds);
        }
    }

    [Test]
    public void SplashHitsWhatStandsCloseToWhereTheShotLandsAndNothingFurther()
    {
        DefenseUnit unit = Thrower(speed: 4f);
        unit.Weapon.splashRadius = 1.5f;
        unit.Weapon.splashFraction = 0.5f;

        FakeTarget aimed = Enemy(4f);
        FakeTarget beside = Enemy(4f, 1.2f);
        FakeTarget clear = Enemy(7.5f);

        defense.Tick(0.25f);
        Run(1f);

        Assert.AreEqual(1000f - 10f, aimed.Health, 0.001f, "The full shot on what it was thrown at.");
        Assert.AreEqual(1000f - 5f, beside.Health, 0.001f, "Half of it on what stood 1.2 m away.");
        Assert.AreEqual(0, clear.Hits, "3.5 m off is out of the splash.");
    }

    [Test]
    public void HitscanSplashLandsAtOnceAndNeverHitsTheDead()
    {
        var gun = Weapon("Blast", 10f, 2f, 8f);
        gun.splashRadius = 2f;
        gun.splashFraction = 0.4f;

        var blaster = Make<DefenseDefinition>();
        blaster.requiresCrew = false;
        blaster.levels = new[] { new DefenseLevel { weapon = gun, spotRangeMetres = 10f } };
        defense.Register(blaster, "blaster", Vector3.zero, startsBuilt: true);

        FakeTarget aimed = Enemy(3f);
        FakeTarget beside = Enemy(3.5f, 0.5f);
        FakeTarget dead = Enemy(3f, 1f, health: 0f);

        defense.Tick(0.25f);

        Assert.AreEqual(1, aimed.Hits);
        Assert.AreEqual(1000f - 4f, beside.Health, 0.001f);
        Assert.AreEqual(0, dead.Hits);
        StringAssert.Contains("splashes 2 m", gun.Describe());
    }

    [Test]
    public void SlowingShotHoldsBackWhatItHitsAndAPlainOneDoesNot()
    {
        DefenseUnit unit = Thrower(speed: 4f);
        unit.Weapon.slowMultiplier = 0.6f;
        unit.Weapon.slowSeconds = 1.5f;

        var target = new SlowTarget { Position = new Vector3(4f, 0f, 0f) };
        hostiles.Add(target);

        defense.Tick(0.25f);
        Assert.AreEqual(1f, target.SlowedTo, "Nothing until the shot lands.");

        Run(1f);
        Assert.AreEqual(0.6f, target.SlowedTo, 1e-5f);
        Assert.AreEqual(1.5f, target.SlowedFor, 1e-5f);
        Assert.AreEqual(1000f - 10f, target.Health, 0.001f);
        StringAssert.Contains("slows", unit.Weapon.Describe());

        StringAssert.DoesNotContain("slows", pistol.Describe());
        Assert.IsFalse(pistol.Slows);
        Assert.IsFalse(pistol.Splashes);
    }

    [Test]
    public void KitchenGardenShootersEachHaveTheirOwnTrick()
    {
        DefenseDefinition tomato = UnityEditor.AssetDatabase.LoadAssetAtPath<DefenseDefinition>(LastOrders.EditorTools.DefenseContentGenerator.TomatoShooterPath);
        DefenseDefinition cabbage = UnityEditor.AssetDatabase.LoadAssetAtPath<DefenseDefinition>(LastOrders.EditorTools.DefenseContentGenerator.CabbageShooterPath);
        DefenseDefinition plate = UnityEditor.AssetDatabase.LoadAssetAtPath<DefenseDefinition>(LastOrders.EditorTools.DefenseContentGenerator.PlateShooterPath);
        if (tomato == null || cabbage == null || plate == null) Assert.Inconclusive("The shooters' content has not been generated in this project.");

        foreach (DefenseDefinition shooter in new[] { tomato, cabbage, plate })
        {
            Assert.IsFalse(shooter.requiresCrew, $"{shooter.displayName} needs nobody posted.");
            Assert.AreEqual(2, shooter.LevelCount, $"{shooter.displayName} has an upgrade.");
            Assert.IsNotNull(shooter.prefab, $"{shooter.displayName} has its model.");
            for (int i = 0; i < shooter.LevelCount; i++)
                Assert.IsTrue(shooter.Level(i).weapon.IsProjectile, $"{shooter.displayName} Level {i + 1} throws what it shoots.");
        }

        Assert.IsTrue(tomato.Level(0).weapon.Slows, "Tomatoes leave pulp to wade through.");
        Assert.IsTrue(cabbage.Level(0).weapon.Splashes, "A cabbage bursts on everything round it.");
        Assert.IsTrue(plate.Level(0).weapon.Splashes, "A plate shatters into its neighbours.");
        Assert.Greater(cabbage.Level(0).weapon.splashRadius, plate.Level(0).weapon.splashRadius, "The cabbage is the heavy one.");
        Assert.Less(tomato.Level(0).weapon.secondsBetweenShots, plate.Level(0).weapon.secondsBetweenShots, "The tomato is the quick one.");
        Assert.Less(plate.Level(0).weapon.secondsBetweenShots, cabbage.Level(0).weapon.secondsBetweenShots);
    }

    [Test]
    public void ShopSellsEveryShooterAsSomethingToSetDownOnTheGrid()
    {
        var items = new List<PlacedObjectTypeSO>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:PlacedObjectTypeSO", new[] { LastOrders.EditorTools.DefenseShopSetup.Folder }))
            items.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<PlacedObjectTypeSO>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)));
        if (items.Count == 0) Assert.Inconclusive("The defense shop has not been set up in this project.");

        var types = new HashSet<ObjectType>();
        foreach (PlacedObjectTypeSO item in items)
        {
            Assert.AreEqual(ObjectCategory.Defense, item.categoryType, item.name);
            Assert.IsTrue(types.Add(item.type), $"{item.name}: two shop items share {item.type}, and the shop keys them by it.");
            Assert.Greater(item.PriceAmount, 0, $"{item.name} costs Coins.");
            Assert.IsNotNull(item.shopIcon, $"{item.name} has its icon.");
            Assert.IsNotNull(item.visual, $"{item.name} has a ghost to drag.");
            Assert.IsNull(item.visual.GetComponentInChildren<DefensePost>(true), $"{item.name}'s ghost is only its look.");

            Assert.IsNotNull(item.prefab, item.name);
            var placed = item.prefab.GetComponent<PlacedObject_Done>();
            Assert.IsNotNull(placed, $"{item.name} is placed by the grid.");

            var post = item.prefab.GetComponentInChildren<DefensePost>(true);
            Assert.IsNotNull(post, $"{item.name} is a defense once it is down.");
            Assert.IsNotNull(post.Definition, item.name);

            var so = new UnityEditor.SerializedObject(post);
            Assert.IsTrue(so.FindProperty("startsBuilt").boolValue, $"{item.name} is built as it is bought.");
            Assert.IsTrue(so.FindProperty("placedFromShop").boolValue, $"{item.name} takes its id from where it is set down.");

            Vector3 middle = post.transform.localPosition;
            Assert.AreEqual(item.width * 0.5f, middle.x, 1e-4f, $"{item.name} stands in the middle of its footprint.");
            Assert.AreEqual(item.height * 0.5f, middle.z, 1e-4f, $"{item.name} stands in the middle of its footprint.");

            // Over the compound wall it goes up on a bastion, and the ghost shows it there.
            Assert.IsNotNull(item.prefab.GetComponent<WallMount>(), $"{item.name} can be mounted on the wall.");
            Transform bastion = item.prefab.Find("Bastion");
            Assert.IsNotNull(bastion, $"{item.name} carries its bastion.");
            Assert.IsFalse(bastion.gameObject.activeSelf, $"{item.name}'s bastion is off until it is set down on a wall.");
            Assert.IsNotNull(bastion.GetComponent<LastOrders.Perimeter.CompoundWall>(), $"{item.name}'s bastion is a wall piece zombies can batter.");
            Assert.IsNotNull(item.visual.GetComponent<WallMount>(), $"{item.name}'s ghost shows it up on the wall.");
            Assert.IsNull(item.visual.GetComponentInChildren<LastOrders.Perimeter.CompoundWall>(true), $"{item.name}'s ghost bastion is only its look.");

            Assert.IsNotNull(item.placementProfile, $"{item.name} has the defenses' placement rules.");
            Assert.IsTrue(System.Array.Exists(item.placementProfile.rules, r => r is DefenseFootingRule),
                          $"{item.name} is kept out of gateways and off wall sites.");
        }

        Assert.IsTrue(types.SetEquals(new[] { ObjectType.TomatoShooter, ObjectType.CornShooter, ObjectType.PlateShooter, ObjectType.CabbageShooter }));
    }

    // --- How a defense moves -----------------------------------------------------------------

    [Test]
    public void IdleSweepStaysInsideItsArcAndSwingsBothWays()
    {
        var motion = Make<DefenseMotionProfile>();
        motion.idleScanDegrees = 40f;
        motion.idleScanSeconds = 8f;

        float least = 0f, most = 0f;
        for (float t = 0f; t < 8f; t += 0.05f)
        {
            float yaw = motion.ScanYaw(t);
            Assert.LessOrEqual(Mathf.Abs(yaw), 40f + 1e-3f);
            least = Mathf.Min(least, yaw);
            most = Mathf.Max(most, yaw);
        }

        Assert.AreEqual(40f, most, 0.1f);
        Assert.AreEqual(-40f, least, 0.1f);
        Assert.AreEqual(0f, motion.ScanYaw(0f), 1e-4f, "Starts facing its rest direction.");
        Assert.Greater(Mathf.Abs(motion.ScanYaw(1f) - motion.ScanYaw(1f, 0.1f)), 1f, "An offset puts two turrets out of step.");
    }

    [Test]
    public void NoSweepWhenTheArcIsZero()
    {
        var motion = Make<DefenseMotionProfile>();
        motion.idleScanDegrees = 0f;

        Assert.AreEqual(0f, motion.ScanYaw(1.3f));
        Assert.AreEqual(0f, motion.ScanYaw(5.7f, 0.4f));
    }

    [Test]
    public void RecoilKicksFullyBackThenRunsHome()
    {
        var motion = Make<DefenseMotionProfile>();
        motion.recoilDistance = 0.1f;
        motion.recoilReturnSeconds = 0.2f;

        Assert.AreEqual(0.1f, motion.RecoilOffset(0f), 1e-5f, "Thrown all the way back on the shot.");
        Assert.Less(motion.RecoilOffset(0.1f), motion.RecoilOffset(0.05f), "Runs forward from there.");
        Assert.Greater(motion.RecoilOffset(0.1f), 0f);
        Assert.AreEqual(0f, motion.RecoilOffset(0.2f), "Home when the return time is up.");
        Assert.AreEqual(0f, motion.RecoilOffset(5f));
    }
}
#endif
