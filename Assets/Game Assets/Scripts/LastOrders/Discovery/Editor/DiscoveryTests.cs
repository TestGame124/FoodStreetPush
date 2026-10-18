#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using LastOrders.Consequence;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Defense;
using LastOrders.Discovery;
using LastOrders.Economy;
using LastOrders.Expansion;
using LastOrders.Kitchen;
using LastOrders.Night;
using LastOrders.Progression;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The discovery sites' rules, without a scene: a site found only by claiming its plot, the
/// drive-through's bags packed from the pass and paid for at the window, the food truck's load and
/// what each place pays for it, a round on the range and its medal, and the noise maker drawing the
/// zombies in earshot off the horde's orders and letting them go again.
/// </summary>
public class DiscoveryTests
{
    readonly List<Object> owned = new List<Object>();

    int coins;
    int experience;

    [SetUp]
    public void SetUp()
    {
        coins = 0;
        experience = 0;
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

    void Pay(int amount) => coins += amount;
    void Learn(int amount) => experience += amount;

    /// <summary>A pass with counts per dish, and a menu of what the stations make.</summary>
    sealed class FakeKitchen : IDishStore
    {
        public readonly Dictionary<ItemType, int> Counts = new Dictionary<ItemType, int>();
        public readonly List<ItemType> Dishes = new List<ItemType>();

        public int Spare(ItemType dish) => Counts.TryGetValue(dish, out int n) ? n : 0;

        public bool TryTake(ItemType dish)
        {
            if (Spare(dish) <= 0) return false;
            Counts[dish]--;
            return true;
        }

        public void PutBack(ItemType dish) => Counts[dish] = Spare(dish) + 1;

        public IReadOnlyCollection<ItemType> Menu => Dishes;
    }

    static DishCatalogue Prices() => new DishCatalogue { Price = dish => dish == ItemType.TomatoSoup ? 14 : 12, Name = dish => dish.ToString() };

    // --- Finding a site ----------------------------------------------------------------------------

    ExpansionAreaDefinition Plot(string id)
    {
        var plot = Make<ExpansionAreaDefinition>();
        plot.id = id;
        plot.displayName = "East lot";
        plot.unlockedBy = new ExpansionAreaDefinition[0];
        plot.expandCost = new ResourceAmount[0];
        plot.expandSeconds = 1f;
        plot.claimReward = new RewardBundle(0);
        return plot;
    }

    DiscoverySiteDefinition Site(string id, ExpansionAreaDefinition plot, DiscoveryFeature feature)
    {
        var site = Make<DiscoverySiteDefinition>();
        site.id = id;
        site.displayName = "Abandoned security hut";
        site.featureName = "Noise maker";
        site.feature = feature;
        site.area = plot;
        return site;
    }

    [Test]
    public void SiteIsFoundOnlyWhenItsPlotIsClaimed()
    {
        var progression = new ProgressionService();
        var ledger = new ResourceLedger();
        var expansion = new ExpansionService(progression, new RewardService(progression, ledger, null), ledger,
                                             new DayCycleMachine(new TimeService(6f)), () => 1);
        var discovery = new DiscoveryService(progression, expansion);

        ExpansionAreaDefinition plot = Plot("plot_east");
        DiscoverySiteDefinition hut = Site("security_hut", plot, DiscoveryFeature.NoiseMaker);

        var found = new List<(string, bool)>();
        discovery.OnDiscovered += (site, announce) => found.Add((site.id, announce));

        discovery.Register(hut);
        expansion.RegisterArea(plot, new[] { "plot_east/rubble_1" });

        Assert.IsFalse(discovery.IsDiscovered("security_hut"));
        Assert.IsFalse(discovery.HasFeature(DiscoveryFeature.NoiseMaker));
        StringAssert.Contains("East lot", discovery.WhyUndiscovered("security_hut"));

        expansion.HandleDebrisCleared("plot_east", "plot_east/rubble_1");
        Assert.IsTrue(expansion.StartExpansion("plot_east", out _));
        Assert.IsFalse(discovery.IsDiscovered("security_hut"), "Paying for the land does not find what is on it.");

        expansion.Tick(2f);

        Assert.IsTrue(discovery.IsDiscovered("security_hut"));
        Assert.IsTrue(discovery.HasFeature(DiscoveryFeature.NoiseMaker));
        Assert.AreEqual(1, found.Count);
        Assert.AreEqual(("security_hut", true), found[0], "Found just now, by claiming: worth announcing.");
        Assert.IsNull(discovery.WhyUndiscovered("security_hut"));
    }

    [Test]
    public void SiteOnLandAlreadyClaimedIsFoundQuietly()
    {
        var progression = new ProgressionService();
        var ledger = new ResourceLedger();
        var expansion = new ExpansionService(progression, new RewardService(progression, ledger, null), ledger,
                                             new DayCycleMachine(new TimeService(6f)), () => 1);
        var discovery = new DiscoveryService(progression, expansion);

        ExpansionAreaDefinition plot = Plot("plot_west");
        progression.Set(ExpansionIds.AreaClaimed("plot_west"));

        var found = new List<bool>();
        discovery.OnDiscovered += (_, announce) => found.Add(announce);

        expansion.RegisterArea(plot, new[] { "plot_west/stump_1" });
        discovery.Register(Site("shooting_range", plot, DiscoveryFeature.ShootingRange));

        Assert.IsTrue(discovery.HasFeature(DiscoveryFeature.ShootingRange));
        CollectionAssert.AreEqual(new[] { false }, found);
    }

    // --- The drive-through ---------------------------------------------------------------------------

    TakeawayService Window(FakeKitchen kitchen, TakeawayTuning tuning, System.Func<bool> open = null)
    {
        return new TakeawayService(tuning, kitchen, Prices(), open ?? (() => true), () => true, Pay, Learn, 7) { HasWindow = true };
    }

    static TakeawayTuning OneDishBags() => new TakeawayTuning
    {
        laneLength = 3, arrivalSeconds = new Vector2(10f, 10f), patienceSeconds = 30f, dishesPerBag = new Vector2Int(1, 1),
        priceMultiplier = 1f, quickTip = 0.5f, experiencePerBag = 2, cookToOrderChance = 0f,
    };

    [Test]
    public void NoCarsWithoutTheWindowOrWhileClosed()
    {
        var kitchen = new FakeKitchen();
        kitchen.Dishes.Add(ItemType.TomatoSoup);
        kitchen.Counts[ItemType.TomatoSoup] = 3;

        TakeawayService noWindow = Window(kitchen, OneDishBags());
        noWindow.HasWindow = false;
        noWindow.Tick(60f);
        Assert.AreEqual(0, noWindow.Lane.Count);

        TakeawayService closed = Window(kitchen, OneDishBags(), () => false);
        closed.Tick(60f);
        Assert.AreEqual(0, closed.Lane.Count);
    }

    [Test]
    public void BagIsPackedFromThePassAndPaidForAtTheWindow()
    {
        var kitchen = new FakeKitchen();
        kitchen.Dishes.Add(ItemType.TomatoSoup);
        kitchen.Counts[ItemType.TomatoSoup] = 2;

        TakeawayService takeaway = Window(kitchen, OneDishBags());
        takeaway.Tick(9f);
        Assert.AreEqual(1, takeaway.Lane.Count, "The first car comes soon after opening.");

        TakeawayOrder order = takeaway.Lane[0];
        Assert.AreEqual(ItemType.TomatoSoup, order.Dishes[0]);
        Assert.AreEqual(TakeawayCarState.Arriving, order.State);

        Assert.IsFalse(takeaway.TryHandOver(order, out _), "Not served while still pulling in.");

        takeaway.MarkArrived(order);
        Assert.AreEqual(TakeawayCarState.Waiting, order.State);
        Assert.IsFalse(takeaway.TryHandOver(order, out string why));
        StringAssert.Contains("still to pack", why);

        Assert.IsTrue(takeaway.TryPackDish(ItemType.TomatoSoup, null, out _));
        Assert.AreEqual(1, kitchen.Spare(ItemType.TomatoSoup), "Packing takes the dish off the pass.");
        Assert.IsTrue(order.IsComplete);

        Assert.IsTrue(takeaway.TryHandOver(order, out _));
        Assert.AreEqual(14 + 7, coins, "Fourteen for the soup and half again for a quick handover.");
        Assert.AreEqual(2, experience);
        Assert.AreEqual(0, takeaway.Lane.Count);
        Assert.AreEqual(TakeawayCarState.Leaving, order.State);
    }

    [Test]
    public void OnlyTheCarAtTheWindowIsServed()
    {
        var kitchen = new FakeKitchen();
        kitchen.Dishes.Add(ItemType.Bread);
        kitchen.Counts[ItemType.Bread] = 5;

        TakeawayService takeaway = Window(kitchen, OneDishBags());
        takeaway.Tick(9f);
        takeaway.Tick(10.5f);
        Assert.AreEqual(2, takeaway.Lane.Count);

        TakeawayOrder front = takeaway.Lane[0], back = takeaway.Lane[1];
        takeaway.MarkArrived(front);
        takeaway.MarkArrived(back);
        takeaway.TryPack(back, 0, out _);

        Assert.IsFalse(takeaway.TryHandOver(back, out string why));
        StringAssert.Contains("window first", why);

        int moved = 0;
        takeaway.OnLaneMoved += () => moved++;
        takeaway.TryPack(front, 0, out _);
        Assert.IsTrue(takeaway.TryHandOver(front, out _));
        Assert.AreEqual(1, moved, "The car behind moves up.");
        Assert.AreSame(back, takeaway.AtWindow);
    }

    [Test]
    public void CarThatGivesUpPutsItsPackedDishesBack()
    {
        var kitchen = new FakeKitchen();
        kitchen.Dishes.Add(ItemType.Bread);
        kitchen.Counts[ItemType.Bread] = 1;

        TakeawayService takeaway = Window(kitchen, OneDishBags());
        takeaway.Tick(9f);
        TakeawayOrder order = takeaway.Lane[0];
        takeaway.MarkArrived(order);
        takeaway.TryPack(order, 0, out _);
        Assert.AreEqual(0, kitchen.Spare(ItemType.Bread));

        bool? served = null;
        takeaway.OnLeaving += (_, s) => served = s;
        takeaway.Tick(31f);

        Assert.AreEqual(false, served);
        Assert.AreEqual(1, kitchen.Spare(ItemType.Bread), "The bread comes back to the pass.");
        Assert.AreEqual(0, coins);
    }

    [Test]
    public void ClosingSendsEveryCarAway()
    {
        var kitchen = new FakeKitchen();
        kitchen.Dishes.Add(ItemType.Bread);
        kitchen.Counts[ItemType.Bread] = 4;

        bool open = true;
        TakeawayService takeaway = Window(kitchen, OneDishBags(), () => open);
        takeaway.Tick(9f);
        takeaway.Tick(10.5f);
        takeaway.TryPack(takeaway.Lane[0], 0, out _);

        open = false;
        takeaway.Tick(0.1f);
        Assert.AreEqual(0, takeaway.Lane.Count);
        Assert.AreEqual(4, kitchen.Spare(ItemType.Bread));
    }

    [Test]
    public void PackingADishThereIsNoneOfSaysSo()
    {
        var kitchen = new FakeKitchen();
        kitchen.Dishes.Add(ItemType.TomatoSoup);

        TakeawayTuning tuning = OneDishBags();
        tuning.cookToOrderChance = 1f;
        TakeawayService takeaway = Window(kitchen, tuning);
        takeaway.Tick(9f);

        Assert.IsFalse(takeaway.TryPack(takeaway.Lane[0], 0, out string why));
        StringAssert.Contains("Cook some", why);
    }

    // --- The food truck ----------------------------------------------------------------------------------

    static FoodTruckTuning Truck() => new FoodTruckTuning
    {
        capacity = 3,
        experiencePerTrip = 4,
        destinations = new[]
        {
            new FoodTruckDestination("farm", "Hillside farm", "", 30f, 2f, 0.5f, true, Vector2.zero, ItemType.Bread),
        },
    };

    [Test]
    public void TruckLoadsFromThePassUpToItsCapacity()
    {
        var kitchen = new FakeKitchen();
        kitchen.Counts[ItemType.Bread] = 5;

        var truck = new FoodTruckService(Truck(), kitchen, Prices(), () => true, () => true, Pay, Learn);
        for (int i = 0; i < 3; i++) Assert.IsTrue(truck.TryLoad(ItemType.Bread, out _));
        Assert.IsFalse(truck.TryLoad(ItemType.Bread, out string why));
        StringAssert.Contains("full", why);
        Assert.AreEqual(2, kitchen.Spare(ItemType.Bread));

        Assert.IsTrue(truck.TryUnload(ItemType.Bread, out _));
        Assert.AreEqual(3, kitchen.Spare(ItemType.Bread));
        Assert.AreEqual(2, truck.Loaded);
    }

    [Test]
    public void TripPaysWhatThePlaceThinksTheLoadIsWorth()
    {
        var kitchen = new FakeKitchen();
        kitchen.Counts[ItemType.Bread] = 2;
        kitchen.Counts[ItemType.TomatoSoup] = 1;

        var truck = new FoodTruckService(Truck(), kitchen, Prices(), () => true, () => true, Pay, Learn);
        FoodTruckDestination farm = truck.Find("farm");

        Assert.IsFalse(truck.CanSend(farm, out string empty));
        StringAssert.Contains("Load", empty);

        truck.TryLoad(ItemType.Bread, out _);
        truck.TryLoad(ItemType.Bread, out _);
        truck.TryLoad(ItemType.TomatoSoup, out _);

        // Bread is the farm's favourite at double; soup sells for half.
        Assert.AreEqual(12 * 2 * 2 + 7, truck.Estimate(farm));

        Assert.IsTrue(truck.TrySend(farm, out _));
        Assert.AreEqual(FoodTruckState.Out, truck.State);
        Assert.IsFalse(truck.TryLoad(ItemType.Bread, out _), "Nothing loads while it is out.");

        truck.Tick(29f);
        Assert.AreEqual(0, coins);

        truck.Tick(2f);
        Assert.AreEqual(FoodTruckState.Parked, truck.State);
        Assert.AreEqual(55, coins);
        Assert.AreEqual(4, experience);
        Assert.AreEqual(0, truck.Loaded);
    }

    [Test]
    public void TruckStaysHomeAfterDark()
    {
        var kitchen = new FakeKitchen();
        kitchen.Counts[ItemType.Bread] = 1;

        var truck = new FoodTruckService(Truck(), kitchen, Prices(), () => false, () => true, Pay, Learn);
        truck.TryLoad(ItemType.Bread, out _);
        Assert.IsFalse(truck.TrySend(truck.Find("farm"), out string why));
        StringAssert.Contains("dark", why);
    }

    // --- The range -----------------------------------------------------------------------------------------

    static ShootingRangeTuning Range() => new ShootingRangeTuning
    {
        roundSeconds = 20f, cooldownSeconds = 30f, medals = new[] { 2, 4, 6 }, coins = new[] { 5, 10, 20 }, experience = new[] { 1, 2, 3 },
    };

    [Test]
    public void RoundPaysForItsMedalAndThenRests()
    {
        bool day = true;
        var range = new ShootingRangeService(Range(), () => day, () => true, Pay, Learn);

        Assert.IsTrue(range.TryStart(out _));
        for (int i = 0; i < 5; i++) range.RecordKnockDown();

        int medal = -2;
        range.OnFinished += (_, m) => medal = m;
        range.Tick(21f);

        Assert.AreEqual(1, medal, "Five down is silver.");
        Assert.AreEqual(10, coins);
        Assert.AreEqual(2, experience);
        Assert.AreEqual(5, range.BestScore);

        Assert.IsFalse(range.TryStart(out string resting));
        StringAssert.Contains("Resetting", resting);

        range.Tick(31f);
        Assert.IsTrue(range.CanStart(out _));

        day = false;
        Assert.IsFalse(range.CanStart(out string night));
        StringAssert.Contains("after dark", night);
    }

    [Test]
    public void KnockDownsCountOnlyDuringARound()
    {
        var range = new ShootingRangeService(Range(), () => true, () => true, Pay, Learn);
        range.RecordKnockDown();
        Assert.AreEqual(0, range.Score);
        Assert.AreEqual(-1, range.MedalFor(1));
        Assert.AreEqual(2, range.MedalFor(9));
        Assert.AreEqual(1, range.ToNextMedal(1));
    }

    // --- The noise maker -----------------------------------------------------------------------------------

    /// <summary>The horde, as far as the noise maker can tell: one target it hands out, and what it was told.</summary>
    sealed class FakeHorde : IZombieOrders
    {
        public readonly IRaidTarget Defense = new Spot();
        public readonly List<object> Released = new List<object>();

        public bool TryAssign(object zombie, Vector3 from, out IRaidTarget target)
        {
            target = Defense;
            return true;
        }

        public bool StillWanted(object zombie, IRaidTarget target, Vector3 from) => true;
        public bool Strike(object zombie, IRaidTarget target, float damage, Vector3 from) => false;
        public void GaveUp(object zombie, IRaidTarget target) { }
        public void Release(object zombie) => Released.Add(zombie);

        sealed class Spot : IRaidTarget
        {
            public Vector3 ClosestPoint(Vector3 from) => Vector3.zero;
            public float FlatDistance(Vector3 from) => from.magnitude;
            public bool WorthBreakingThroughFor => true;
        }
    }

    [Test]
    public void NoiseMakerIsSetDownOnlyWhereAndWhenItMayBe()
    {
        bool night = false;
        var noise = new NoiseMakerService(new FakeHorde(), () => new NoiseMakerTuning(), () => true, () => night, () => !night,
                                          at => at.x < 0f ? "Set it outside the walls." : null);

        Assert.IsFalse(noise.TryPlace(new Vector3(-5f, 0f, 0f), out string inside));
        StringAssert.Contains("outside", inside);

        Assert.IsTrue(noise.TryPlace(new Vector3(5f, 0f, 3f), out _));
        Assert.IsTrue(noise.IsPlaced);
        Assert.AreEqual(new Vector3(5f, 0f, 3f), noise.Position);

        night = true;
        Assert.IsFalse(noise.TryPlace(new Vector3(6f, 0f, 3f), out string dark));
        StringAssert.Contains("daylight", dark);
    }

    [Test]
    public void SoundingDrawsZombiesInEarshotAndLetsThemGoAfter()
    {
        var horde = new FakeHorde();
        var tuning = new NoiseMakerTuning { usesPerAttack = 1, soundSeconds = 5f, radius = 10f, health = 100f };
        bool attack = false;
        var noise = new NoiseMakerService(horde, () => tuning, () => true, () => attack, () => !attack, _ => null);
        noise.TryPlace(Vector3.zero, out _);

        Assert.IsFalse(noise.TrySound(out string early));
        StringAssert.Contains("zombies come", early);

        attack = true;
        noise.BeginAttack();
        Assert.IsTrue(noise.TrySound(out _));
        Assert.IsFalse(noise.TrySound(out _), "One use an attack.");

        object near = new object(), far = new object();

        Assert.IsTrue(noise.TryAssign(near, new Vector3(6f, 0f, 0f), out IRaidTarget lure));
        Assert.AreNotSame(horde.Defense, lure, "In earshot: it goes for the noise maker.");
        CollectionAssert.Contains(horde.Released, near, "Whatever the horde had it doing is let go.");

        Assert.IsTrue(noise.TryAssign(far, new Vector3(30f, 0f, 0f), out IRaidTarget other));
        Assert.AreSame(horde.Defense, other, "Out of earshot, the horde decides as before.");

        Assert.IsFalse(noise.StillWanted(far, horde.Defense, new Vector3(6f, 0f, 0f)), "Wandering into earshot drops its errand.");
        Assert.IsTrue(noise.StillWanted(near, lure, new Vector3(1f, 0f, 0f)));

        noise.Tick(6f);
        Assert.IsFalse(noise.IsSounding);
        Assert.IsFalse(noise.StillWanted(near, lure, new Vector3(1f, 0f, 0f)), "Silent, it lets them go.");
    }

    [Test]
    public void CrowdBattersTheNoiseMakerQuiet()
    {
        var tuning = new NoiseMakerTuning { usesPerAttack = 1, soundSeconds = 30f, radius = 10f, health = 10f };
        var noise = new NoiseMakerService(new FakeHorde(), () => tuning, () => true, () => true, () => true, _ => null);
        noise.TryPlace(Vector3.zero, out _);
        noise.BeginAttack();
        noise.TrySound(out _);

        object zombie = new object();
        noise.TryAssign(zombie, Vector3.one, out IRaidTarget lure);

        bool? broken = null;
        noise.OnSilenced += b => broken = b;

        Assert.IsFalse(noise.Strike(zombie, lure, 6f, Vector3.one));
        Assert.IsTrue(noise.Strike(zombie, lure, 6f, Vector3.one), "The blow that breaks it ends the battering.");
        Assert.AreEqual(true, broken);
        Assert.IsFalse(noise.IsSounding);
    }

    // --- A practice machine ---------------------------------------------------------------------------------

    sealed class Cardboard : IDefenseTarget
    {
        public Vector3 Position { get; set; }
        public bool Up = true;
        public int Hits;
        public bool IsAlive => Up;
        public string DisplayName => "Cardboard zombie";

        public void ApplyDamage(float amount) => Hits++;
    }

    [Test]
    public void PracticeMachineShootsOnlyWhatItIsToldToAtItsOwnPace()
    {
        var weapon = Make<WeaponDefinition>();
        weapon.damagePerShot = 5f;
        weapon.secondsBetweenShots = 1f;
        weapon.rangeMetres = 2f;
        weapon.ammoPerShot = 3;

        var kind = Make<DefenseDefinition>();
        kind.id = "scrap_turret";
        kind.levels = new[] { new DefenseLevel { weapon = weapon, spotRangeMetres = 2f } };

        DefenseUnit unit = DefenseUnit.ForPractice("range:scrap_turret", kind, Vector3.zero);

        var near = new Cardboard { Position = new Vector3(1f, 0f, 0f) };
        var called = new Cardboard { Position = new Vector3(9f, 0f, 0f) };
        var around = new List<IDefenseTarget> { near, called };

        unit.TickCalled(0.1f, null, around);
        Assert.AreEqual(0, near.Hits, "Nothing called: nothing shot, however near.");

        unit.TickCalled(0.1f, called, around);
        Assert.AreEqual(1, called.Hits, "Out of the weapon's range and with no ammo in the stores, a practice shot still goes.");

        unit.TickCalled(0.5f, called, around);
        Assert.AreEqual(1, called.Hits, "The weapon keeps its own pace.");

        unit.TickCalled(0.6f, called, around);
        Assert.AreEqual(2, called.Hits);

        called.Up = false;
        unit.TickCalled(1.5f, called, around);
        Assert.AreEqual(2, called.Hits, "A target that has dropped is not shot.");
    }

    // --- The pass --------------------------------------------------------------------------------------------

    [Test]
    public void DishesTakenOffThePassCanBePutBack()
    {
        var meals = new MealStock();
        meals.Add(ItemType.TomatoSoup, 2);

        Assert.AreEqual(2, meals.Spare(ItemType.TomatoSoup));
        Assert.IsTrue(meals.TryTake(ItemType.TomatoSoup));
        Assert.AreEqual(1, meals.Spare(ItemType.TomatoSoup));

        Assert.IsTrue(meals.TryReserve(ItemType.TomatoSoup));
        Assert.IsFalse(meals.TryTake(ItemType.TomatoSoup), "One promised to a customer is not spare.");

        meals.PutBack(ItemType.TomatoSoup);
        Assert.AreEqual(1, meals.Spare(ItemType.TomatoSoup));
        Assert.AreEqual(2, meals.Cooked(ItemType.TomatoSoup));
    }
}
#endif
