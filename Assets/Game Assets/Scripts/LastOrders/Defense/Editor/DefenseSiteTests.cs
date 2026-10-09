#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using LastOrders.DayCycle;
using LastOrders.Data;
using LastOrders.Defense;
using LastOrders.Economy;
using LastOrders.Progression;
using LastOrders.Survivors;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The rules of a wall site, without a scene: it offers every defense it can take, each at its own
/// price; buying one there is paying for that one and nothing else, in daylight; what was bought is
/// remembered under the site's id; and a defense knocked over with the bastion under it neither
/// watches nor shoots until it is set up again.
/// </summary>
public class DefenseSiteTests
{
    readonly List<Object> owned = new List<Object>();
    readonly List<IDefenseTarget> hostiles = new List<IDefenseTarget>();

    ProgressionService progression;
    ResourceLedger ledger;
    DayCycleMachine machine;
    Wallet gems;
    DefenseService defense;

    DefenseDefinition corn;
    DefenseDefinition turret;
    DefenseDefinition toaster;
    DefenseDefinition[] options;

    [SetUp]
    public void SetUp()
    {
        hostiles.Clear();

        var balance = Make<GameBalanceConfig>();
        progression = new ProgressionService();
        ledger = new ResourceLedger();
        machine = new DayCycleMachine(new TimeService(6f));
        gems = new Wallet();

        defense = new DefenseService(new SurvivorService(balance, ledger), ledger, gems, progression, machine,
                                     () => hostiles, () => false);

        corn = Defense("corn_shooter", 4f, new DefensePrice(15, new ResourceAmount(ResourceType.TradeToken, 40)));
        turret = Defense("scrap_turret", 5f, new DefensePrice(15, new ResourceAmount(ResourceType.TradeToken, 45)));
        toaster = Defense("toastapult", 12f, new DefensePrice(20));
        options = new[] { corn, turret, toaster };
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);

        owned.Clear();
    }

    T Make<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        owned.Add(asset);
        return asset;
    }

    DefenseDefinition Defense(string id, float damage, DefensePrice price)
    {
        var weapon = Make<WeaponDefinition>();
        weapon.displayName = id + " gun";
        weapon.damagePerShot = damage;
        weapon.secondsBetweenShots = 1f;
        weapon.rangeMetres = 8f;

        var asset = Make<DefenseDefinition>();
        asset.id = id;
        asset.displayName = id;
        asset.requiresCrew = false;
        asset.buildPrice = price;
        asset.levels = new[] { new DefenseLevel { weapon = weapon, spotRangeMetres = 10f } };
        return asset;
    }

    sealed class Wallet : IPremiumWallet
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

    sealed class Target : IDefenseTarget
    {
        public Vector3 Position { get; set; }
        public float Health = 100f;
        public bool IsAlive => Health > 0f;
        public string DisplayName => "Zombie";
        public void ApplyDamage(float amount) => Health -= amount;
    }

    sealed class IdlePhase : IDayPhaseState
    {
        public IdlePhase(DayPhase phase) => Phase = phase;
        public DayPhase Phase { get; }
        public void OnEnter(DayPhase previous) { }
        public void OnTick(float deltaSeconds) { }
        public void OnExit(DayPhase next) { }
    }

    [Test]
    public void AnEmptySiteOffersEveryDefenseAtItsOwnPrice()
    {
        BuildChoiceOffer offer = defense.GetSiteOffer("wall_1", options);

        Assert.AreEqual(3, offer.Options.Count);
        StringAssert.Contains("40 Trade Tokens", offer.Options[0].Label);
        StringAssert.Contains("20 Gems", offer.Options[2].Label, "A price with no materials is paid in premium currency.");
        Assert.IsNull(offer.Blocked);
    }

    [Test]
    public void BuildingPaysForTheChosenOneAndIsRemembered()
    {
        ledger.Add(ResourceType.TradeToken, 50);

        Assert.IsTrue(defense.TryBuildAt("wall_1", options, 1, out string message, out DefenseDefinition built), message);
        Assert.AreSame(turret, built);
        Assert.AreEqual(5, ledger.Get(ResourceType.TradeToken), "The turret's 45, not the corn shooter's 40.");
        Assert.AreSame(turret, defense.BuiltAt("wall_1", options));
        Assert.IsNull(defense.BuiltAt("wall_2", options), "Another site is still empty.");

        Assert.IsFalse(defense.TryBuildAt("wall_1", options, 0, out string again, out _));
        StringAssert.Contains("already", again);
    }

    [Test]
    public void APremiumOnlyDefenseIsPaidInGems()
    {
        gems.Balance = 25;

        Assert.IsTrue(defense.TryBuildAt("wall_1", options, 2, out _, out DefenseDefinition built));
        Assert.AreSame(toaster, built);
        Assert.AreEqual(5, gems.Balance);
    }

    [Test]
    public void ShortOfTheStoresNothingIsBuilt()
    {
        ledger.Add(ResourceType.TradeToken, 30);

        Assert.IsFalse(defense.TryBuildAt("wall_1", options, 0, out string message, out DefenseDefinition built));
        Assert.IsNull(built);
        StringAssert.Contains("Trade Tokens", message);
        Assert.AreEqual(30, ledger.Get(ResourceType.TradeToken));
        Assert.IsNull(defense.BuiltAt("wall_1", options));
    }

    [Test]
    public void NothingIsBuiltOnTheWallAfterDark()
    {
        ledger.Add(ResourceType.TradeToken, 100);
        machine.AddState(new IdlePhase(DayPhase.Night));
        machine.ForceTo(DayPhase.Night);

        Assert.IsFalse(defense.TryBuildAt("wall_1", options, 0, out string message, out _));
        StringAssert.Contains("dark", message);
    }

    [Test]
    public void AKnockedOverDefenseNeitherWatchesNorShoots()
    {
        DefenseUnit unit = defense.Register(corn, "wall_1", Vector3.zero, startsBuilt: true);
        var zombie = new Target { Position = new Vector3(3f, 0f, 0f) };
        hostiles.Add(zombie);

        defense.SetOutOfAction("wall_1", true);
        defense.Tick(2f);

        Assert.IsFalse(unit.CanFire);
        Assert.IsNull(unit.Target);
        Assert.AreEqual(100f, zombie.Health);
        StringAssert.Contains("knocked over", defense.Describe("wall_1"));

        defense.SetOutOfAction("wall_1", false);
        defense.Tick(0.1f);

        Assert.AreEqual(96f, zombie.Health, "Set up again, it shoots.");
    }
}
#endif
