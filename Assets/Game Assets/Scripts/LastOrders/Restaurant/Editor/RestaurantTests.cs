#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using FoodStreet.Navigation;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Economy;
using LastOrders.Restaurant;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The restaurant building, without a scene: each side grows into the strip in front of its own
/// wall, left and right are the player's looking at the front, every expansion costs more than
/// the last and is refused whole when it cannot be paid, nothing grows after dark - and a zombie
/// is stopped at the walls, which take its blows and never fall.
/// </summary>
public class RestaurantTests
{
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

    static readonly Rect Diner = Rect.MinMaxRect(10f, 15f, 30f, 29f);

    ResourceLedger ledger;
    FakeWallet coins;
    DayCycleMachine machine;
    bool breached;

    [SetUp]
    public void SetUp()
    {
        ledger = new ResourceLedger();
        coins = new FakeWallet();
        breached = false;

        machine = new DayCycleMachine(new TimeService(6f));
        machine.AddState(new EmptyState(DayPhase.Day));
        machine.AddState(new EmptyState(DayPhase.Night));
        machine.Start(DayPhase.Day);
    }

    RestaurantService Service(RestaurantTuning tuning = null)
    {
        var restaurant = new RestaurantService(tuning ?? Tuning(), ledger, coins, machine, () => breached);
        restaurant.SetFootprint(Diner, Vector2.down);
        return restaurant;
    }

    /// <summary>Three metres a side, two steps each, 10 coins and 5 wood to start.</summary>
    static RestaurantTuning Tuning() => new RestaurantTuning
    {
        back = new RestaurantSideTuning(RestaurantSide.Back, 3f, 2),
        left = new RestaurantSideTuning(RestaurantSide.Left, 3f, 2),
        right = new RestaurantSideTuning(RestaurantSide.Right, 3f, 1),
        firstExpansionCost = new Price(10, new ResourceAmount(ResourceType.Wood, 5)),
        costGrowth = 2f,
    };

    void Fund(int coinsHeld, int wood)
    {
        coins.Balance = coinsHeld;
        ledger.Add(ResourceType.Wood, wood);
    }

    // --- Where each side grows ----------------------------------------------------------------

    [Test]
    public void EachSideGrowsIntoTheStripInFrontOfItsOwnWall()
    {
        RestaurantService restaurant = Service();

        Assert.AreEqual(Rect.MinMaxRect(10f, 29f, 30f, 32f), restaurant.StripFor(RestaurantSide.Back));
        Assert.AreEqual(Rect.MinMaxRect(7f, 15f, 10f, 29f), restaurant.StripFor(RestaurantSide.Left));
        Assert.AreEqual(Rect.MinMaxRect(30f, 15f, 33f, 29f), restaurant.StripFor(RestaurantSide.Right));
    }

    [Test]
    public void LeftAndRightAreThePlayersLookingAtTheFront()
    {
        RestaurantService restaurant = Service();

        // The door faces south: stood outside it looking north, west is on the left.
        Assert.AreEqual(Vector2.left, restaurant.DirectionOf(RestaurantSide.Left));
        Assert.AreEqual(Vector2.right, restaurant.DirectionOf(RestaurantSide.Right));

        // Turned round, so are they.
        restaurant.SetFootprint(Diner, Vector2.up);
        Assert.AreEqual(Vector2.right, restaurant.DirectionOf(RestaurantSide.Left));
        Assert.AreEqual(Vector2.down, restaurant.DirectionOf(RestaurantSide.Back));
    }

    [Test]
    public void AFrontThatIsNotSquareIsSnappedToTheNearestAxis()
    {
        RestaurantService restaurant = Service();
        restaurant.SetFootprint(Diner, new Vector2(0.2f, -3f));

        Assert.AreEqual(Vector2.down, restaurant.Front);
    }

    // --- Growing ------------------------------------------------------------------------------

    [Test]
    public void ExpandingTakesInTheStripAndCountsTheStep()
    {
        RestaurantService restaurant = Service();
        Fund(10, 5);

        RestaurantSide grown = RestaurantSide.Right;
        Rect taken = default;
        restaurant.OnExpanded += (side, strip) => { grown = side; taken = strip; };

        Assert.IsTrue(restaurant.TryExpand(RestaurantSide.Back, out string message), message);

        Assert.AreEqual(Rect.MinMaxRect(10f, 15f, 30f, 32f), restaurant.Footprint);
        Assert.AreEqual(1, restaurant.StepsTaken(RestaurantSide.Back));
        Assert.AreEqual(RestaurantSide.Back, grown);
        Assert.AreEqual(Rect.MinMaxRect(10f, 29f, 30f, 32f), taken);

        Assert.AreEqual(0, coins.Balance);
        Assert.AreEqual(0, ledger.Get(ResourceType.Wood));
    }

    [Test]
    public void TheNextStripStartsFromTheNewWall()
    {
        RestaurantService restaurant = Service();
        Fund(100, 100);

        restaurant.TryExpand(RestaurantSide.Left, out _);

        Assert.AreEqual(Rect.MinMaxRect(4f, 15f, 7f, 29f), restaurant.StripFor(RestaurantSide.Left));
        Assert.AreEqual(Rect.MinMaxRect(7f, 29f, 30f, 32f), restaurant.StripFor(RestaurantSide.Back),
                        "The back strip is as wide as the back has become.");
    }

    [Test]
    public void EachSideStopsAtItsLastStep()
    {
        RestaurantService restaurant = Service();
        Fund(1000, 1000);

        Assert.IsTrue(restaurant.TryExpand(RestaurantSide.Right, out _));
        Assert.IsFalse(restaurant.TryExpand(RestaurantSide.Right, out string reason));

        StringAssert.Contains("any further", reason);
        Assert.AreEqual(0, restaurant.StepsLeft(RestaurantSide.Right));
        Assert.IsTrue(restaurant.AnyStepsLeft);
    }

    [Test]
    public void EachExpansionCostsMoreThanTheLast()
    {
        RestaurantService restaurant = Service();
        Fund(1000, 1000);

        Assert.AreEqual(10, restaurant.NextPrice.coins);
        restaurant.TryExpand(RestaurantSide.Left, out _);

        Assert.AreEqual(20, restaurant.NextPrice.coins);
        Assert.AreEqual(10, restaurant.NextPrice.resources.AmountOf(ResourceType.Wood));
    }

    [Test]
    public void ShortOfThePriceNothingGrowsAndNothingIsTaken()
    {
        RestaurantService restaurant = Service();
        Fund(10, 2);

        Assert.IsFalse(restaurant.TryExpand(RestaurantSide.Left, out string reason));

        StringAssert.Contains("Wood", reason);
        Assert.AreEqual(Diner, restaurant.Footprint);
        Assert.AreEqual(10, coins.Balance);
        Assert.AreEqual(2, ledger.Get(ResourceType.Wood));
    }

    [Test]
    public void NothingGrowsAfterDark()
    {
        RestaurantService restaurant = Service();
        Fund(100, 100);
        machine.ForceTo(DayPhase.Night);

        Assert.IsFalse(restaurant.TryExpand(RestaurantSide.Left, out string reason));
        StringAssert.Contains("daylight", reason);
    }

    [Test]
    public void NothingGrowsWithZombiesInside()
    {
        RestaurantService restaurant = Service();
        Fund(100, 100);
        breached = true;

        Assert.IsFalse(restaurant.TryExpand(RestaurantSide.Left, out _));
    }

    [Test]
    public void TheOfferListsWhatThePlayerHasTowardsThePrice()
    {
        RestaurantService restaurant = Service();
        Fund(4, 5);

        RestaurantOffer offer = restaurant.GetOffer();

        Assert.IsTrue(offer.CanChoose, "Whether they can pay is asked again when they pick the side.");
        Assert.AreEqual(2, offer.Requirements.Count);
        Assert.AreEqual(4, offer.Requirements[0].Have);
        Assert.AreEqual(10, offer.Requirements[0].Need);
        Assert.IsFalse(offer.Requirements[0].Met);
        Assert.IsTrue(offer.Requirements[1].Met);
    }

    [Test]
    public void TheOfferSaysNoOnceEverySideIsAsBigAsItGets()
    {
        var tuning = Tuning();
        tuning.back.maxSteps = 0;
        tuning.left.maxSteps = 0;
        tuning.right.maxSteps = 0;

        RestaurantOffer offer = Service(tuning).GetOffer();

        Assert.IsFalse(offer.CanChoose);
        StringAssert.Contains("as big", offer.Reason);
    }

    // --- The line no zombie crosses -----------------------------------------------------------

    const float Body = 0.3f;

    [Test]
    public void AZombieIsStoppedAtTheWallsAndNobodyElseIs()
    {
        RestaurantService restaurant = Service();

        var outside = new Vector3(20f, 0f, 14.5f);
        var against = new Vector3(20f, 0f, 14.8f);

        Assert.IsTrue(restaurant.Blocks(outside, against, Body, MoverKind.Hostile), "Up against the front wall.");
        Assert.IsFalse(restaurant.Blocks(outside, against, Body, MoverKind.Friendly), "Staff and visitors use the door.");

        Assert.IsFalse(restaurant.Blocks(against, new Vector3(20.2f, 0f, 14.8f), Body, MoverKind.Hostile),
                       "Along the wall is allowed.");
        Assert.IsFalse(restaurant.Blocks(against, outside, Body, MoverKind.Hostile), "So is backing off.");
        Assert.IsFalse(restaurant.Blocks(new Vector3(20f, 0f, 8f), new Vector3(20f, 0f, 8.2f), Body, MoverKind.Hostile),
                       "Nothing stops it out in the yard.");
    }

    [Test]
    public void TheWallsTakeEveryBlowAndNeverFall()
    {
        RestaurantService restaurant = Service();

        var struck = new List<Vector3>();
        restaurant.OnHit += at => struck.Add(at);

        for (int i = 0; i < 3; i++)
            Assert.IsFalse(restaurant.TakeHit(1000f, new Vector3(20f, 0f, 14.5f)), "No blow brings them down.");

        Assert.IsTrue(restaurant.IsInTheWay);
        Assert.AreEqual(3, restaurant.BlowsTaken);
        Assert.AreEqual(new Vector3(20f, 0f, 15f), struck[0], "Struck where the zombie stands against them.");
    }

    [Test]
    public void TheWallsGrowAndSoDoesWhatTheyStop()
    {
        RestaurantService restaurant = Service();
        Fund(100, 100);

        var from = new Vector3(6.4f, 0f, 20f);
        var to = new Vector3(6.8f, 0f, 20f);
        Assert.IsFalse(restaurant.Blocks(from, to, Body, MoverKind.Hostile));

        restaurant.TryExpand(RestaurantSide.Left, out _);
        Assert.IsTrue(restaurant.Blocks(from, to, Body, MoverKind.Hostile));
    }

    [Test]
    public void WithoutWallsNothingIsStopped()
    {
        var restaurant = new RestaurantService(Tuning(), ledger, coins, machine, () => false);

        Assert.IsFalse(restaurant.Blocks(new Vector3(20f, 0f, 14.5f), new Vector3(20f, 0f, 20f), Body, MoverKind.Hostile));
        Assert.IsFalse(restaurant.IsInTheWay);
        Assert.IsFalse(restaurant.TakeHit(10f, Vector3.zero));
        Assert.AreEqual(0, restaurant.BlowsTaken);
    }

    // --- Geometry -----------------------------------------------------------------------------

    [Test]
    public void DistanceToTheWallsIsFlatAndZeroInside()
    {
        Assert.AreEqual(0f, RestaurantService.DistanceTo(Diner, new Vector3(20f, 5f, 20f)));
        Assert.AreEqual(2f, RestaurantService.DistanceTo(Diner, new Vector3(20f, 0f, 13f)), 1e-4f);
        Assert.AreEqual(5f, RestaurantService.DistanceTo(Diner, new Vector3(33f, 0f, 33f)), 1e-4f);
    }

    [Test]
    public void ATurnedBoxOverlapsOnlyWhenItReachesTheArea()
    {
        Rect area = Rect.MinMaxRect(0f, 0f, 4f, 4f);

        // A 4 m wall section lying along X, just south of the area and just touching it.
        Assert.IsTrue(RestaurantService.Overlaps(area, new Vector3(2f, 0f, -0.1f), 0f, new Vector2(2f, 0.2f)));
        Assert.IsFalse(RestaurantService.Overlaps(area, new Vector3(2f, 0f, -0.5f), 0f, new Vector2(2f, 0.2f)));

        // Turned to run along Z beside the area's east edge: clear of it, then over it.
        Assert.IsFalse(RestaurantService.Overlaps(area, new Vector3(4.5f, 0f, 2f), 90f, new Vector2(2f, 0.2f)));
        Assert.IsTrue(RestaurantService.Overlaps(area, new Vector3(4.1f, 0f, 2f), 90f, new Vector2(2f, 0.2f)));

        // At 45 degrees off the corner: the bounding squares overlap, the box does not.
        Assert.IsFalse(RestaurantService.Overlaps(area, new Vector3(5.2f, 0f, 5.2f), 45f, new Vector2(1.5f, 0.2f)));
    }
}
#endif
