#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Defense;
using LastOrders.Economy;
using LastOrders.Housing;
using LastOrders.Progression;
using LastOrders.Survivors;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The rules of the shelters, without a scene: each one has two beds, everyone hired needs one,
/// so the number of people who can be hired is two a shelter; building another raises the limit;
/// a price is paid in full or not at all; nothing is built or hired after dark; the starting crew
/// take no bed; and a hired Guard or Medic goes to work with no further wiring.
/// </summary>
public class ShelterTests
{
    readonly List<Object> owned = new List<Object>();

    GameBalanceConfig balance;
    ProgressionService progression;
    ResourceLedger ledger;
    DayCycleMachine machine;
    SurvivorService survivors;
    FakeWallet coins;
    ShelterService shelters;

    SurvivorRoleDefinition guardRole;
    SurvivorRoleDefinition medicRole;
    ShelterDefinition hut;

    [SetUp]
    public void SetUp()
    {
        balance = Make<GameBalanceConfig>();
        balance.survivorNames = new[] { "Kira", "Mahmoud", "Tess", "Osei" };

        progression = new ProgressionService();
        ledger = new ResourceLedger();
        machine = new DayCycleMachine(new TimeService(6f));
        survivors = new SurvivorService(balance, ledger);
        coins = new FakeWallet();

        shelters = new ShelterService(balance, survivors, ledger, coins, progression, machine);

        guardRole = Role(SurvivorRole.Guard);
        medicRole = Role(SurvivorRole.Medic);

        hut = Make<ShelterDefinition>();
        hut.id = "shelter";
        hut.displayName = "Shelter";
        hut.beds = 2;
        hut.buildPrice = new ShelterPrice(50, new ResourceAmount(ResourceType.Wood, 12));
        hut.hires = new[]
        {
            new ShelterHire { role = guardRole, pitch = "stands watch", price = new ShelterPrice(40) },
            new ShelterHire { role = medicRole, pitch = "looks after the tent", price = new ShelterPrice(50) },
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

    const int Guard = 0;
    const int Medic = 1;

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

    ShelterUnit Built(string id = "home") => shelters.Register(hut, id, Vector3.zero, startsBuilt: true);

    ShelterUnit Site(string id = "site") => shelters.Register(hut, id, Vector3.zero, startsBuilt: false);

    Survivor Hire(string id = "home", int option = Guard)
    {
        Assert.IsTrue(shelters.TryHire(id, option, out Survivor hired, out string message), message);
        return hired;
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

    sealed class FakeWallet : IPremiumWallet
    {
        public int Balance { get; set; }
        public string DisplayName => "Coins";

        public bool TrySpend(int amount)
        {
            if (amount > Balance) return false;
            Balance -= amount;
            return true;
        }
    }

    // --- Beds are the limit ------------------------------------------------------------------

    [Test]
    public void StartingShelterHasTwoFreeBeds()
    {
        Built();

        Assert.AreEqual(2, shelters.Capacity);
        Assert.AreEqual(0, shelters.Housed);
        Assert.AreEqual(2, shelters.FreeBeds);
    }

    [Test]
    public void EmptySiteHousesNobody()
    {
        Site();

        Assert.AreEqual(0, shelters.Capacity);
        Assert.AreEqual(0, shelters.FreeBeds);
        Assert.IsFalse(shelters.TryHire("site", Guard, out string reason));
        StringAssert.Contains("Build the Shelter first", reason);
    }

    [Test]
    public void HiringPutsASurvivorOnTheRosterAndInABed()
    {
        ShelterUnit home = Built();
        coins.Balance = 100;

        Survivor hired = Hire();

        Assert.AreEqual(1, survivors.Roster.Count);
        Assert.AreSame(hired, survivors.Roster[0]);
        Assert.AreSame(guardRole, hired.Role);
        Assert.IsTrue(hired.IsAvailable);
        Assert.AreSame(home, shelters.HomeOf(hired));
        Assert.AreEqual(1, home.Occupied);
        Assert.AreEqual(60, coins.Balance, "A Guard costs 40 Coins.");
    }

    [Test]
    public void ThirdHireIsRefusedUntilAnotherShelterIsBuilt()
    {
        Built();
        Site();
        coins.Balance = 1000;

        Hire(option: Guard);
        Hire(option: Medic);

        Assert.IsFalse(shelters.TryHire("home", Guard, out string reason));
        StringAssert.Contains("Every bed is taken", reason);
        StringAssert.Contains("Build another shelter", reason);
        Assert.AreEqual(2, survivors.Roster.Count);
        Assert.AreEqual(910, coins.Balance, "A refused hire charges nothing.");
    }

    [Test]
    public void WithEverySiteBuiltAFullCompoundSaysSoWithoutSendingThePlayerToBuild()
    {
        Built();
        coins.Balance = 1000;

        Hire();
        Hire();

        Assert.IsFalse(shelters.TryHire("home", Guard, out string reason));
        Assert.AreEqual("Every bed is taken.", reason);
    }

    [Test]
    public void BuildingAShelterRaisesTheLimitByItsBeds()
    {
        Built();
        ShelterUnit extra = Site();
        coins.Balance = 1000;
        ledger.Add(ResourceType.Wood, 12);

        Hire();
        Hire();

        Assert.IsTrue(shelters.TryBuild("site", out string message), message);

        Assert.IsTrue(extra.IsBuilt);
        Assert.AreEqual(4, shelters.Capacity);
        Assert.AreEqual(2, shelters.FreeBeds);
        Assert.AreEqual(0, ledger.Get(ResourceType.Wood));
        Assert.AreEqual(1000 - 80 - 50, coins.Balance);

        Hire();
        Hire();
        Assert.AreEqual(4, survivors.Roster.Count);
        Assert.AreEqual(0, shelters.FreeBeds);
    }

    [Test]
    public void HiringFromAFullShelterUsesAFreeBedInAnother()
    {
        ShelterUnit first = Built("first");
        ShelterUnit second = Built("second");
        coins.Balance = 1000;

        Hire("first");
        Hire("first");
        Survivor third = Hire("first");

        Assert.AreEqual(2, first.Occupied);
        Assert.AreSame(second, shelters.HomeOf(third));
    }

    [Test]
    public void StartingCrewTakeNoBed()
    {
        Built();
        survivors.Add(new Survivor { Id = "survivor_0", Name = "Kira", Role = guardRole });

        Assert.AreEqual(2, shelters.FreeBeds);
        Assert.IsNull(shelters.HomeOf(survivors.Roster[0]), "The starting crew sleep in the diner.");
    }

    // --- Paying ------------------------------------------------------------------------------

    [Test]
    public void HireShortOfCoinsSaysByHowMuchAndChangesNothing()
    {
        Built();
        coins.Balance = 30;

        Assert.IsFalse(shelters.TryHire("home", Guard, out string reason));

        StringAssert.Contains("Need 10 more Coins", reason);
        Assert.AreEqual(0, survivors.Roster.Count);
        Assert.AreEqual(30, coins.Balance);
        Assert.AreEqual(0, shelters.Housed);
    }

    [Test]
    public void BuildIsPaidInFullOrNotAtAll()
    {
        Site();
        coins.Balance = 100;
        ledger.Add(ResourceType.Wood, 5);

        Assert.IsFalse(shelters.TryBuild("site", out string reason));

        StringAssert.Contains("Need 7 more Wood", reason);
        Assert.AreEqual(100, coins.Balance, "No Coins are taken when the Wood is short.");
        Assert.AreEqual(5, ledger.Get(ResourceType.Wood));
        Assert.AreEqual(0, shelters.Capacity);
    }

    [Test]
    public void OfferGreysOutWhatCannotBeAfforded()
    {
        Built();
        coins.Balance = 45;

        ShelterOffer offer = shelters.GetHireOffer("home");

        Assert.IsNull(offer.Blocked);
        Assert.AreEqual(2, offer.Options.Count);
        Assert.IsTrue(offer.CanTake(Guard), "40 Coins for a Guard is affordable.");
        Assert.IsFalse(offer.CanTake(Medic), "50 Coins for a Medic is not.");
        StringAssert.Contains("Hire a Guard", offer.Options[Guard].Label);
        StringAssert.Contains("40 Coins", offer.Options[Guard].Label);
        StringAssert.Contains("0 of 2 beds taken", offer.Status);
    }

    [Test]
    public void OptionIndexStaysTheHireIndexPastABrokenEntry()
    {
        hut.hires = new[]
        {
            new ShelterHire { role = null, price = new ShelterPrice(1) },
            new ShelterHire { role = medicRole, price = new ShelterPrice(50) },
        };

        Built();
        coins.Balance = 100;

        ShelterOffer offer = shelters.GetHireOffer("home");
        Assert.AreEqual(2, offer.Options.Count);
        Assert.IsFalse(offer.CanTake(0));
        Assert.IsTrue(offer.CanTake(1));

        Survivor hired = Hire(option: 1);
        Assert.AreSame(medicRole, hired.Role);
    }

    // --- After dark ----------------------------------------------------------------------------

    [Test]
    public void NothingIsHiredOrBuiltAfterDark()
    {
        Built();
        Site();
        coins.Balance = 1000;
        ledger.Add(ResourceType.Wood, 100);

        Phase(DayPhase.Night);

        Assert.IsFalse(shelters.TryHire("home", Guard, out string hireReason));
        StringAssert.Contains("after dark", hireReason);

        Assert.IsFalse(shelters.TryBuild("site", out string buildReason));
        StringAssert.Contains("after dark", buildReason);

        Assert.AreEqual(1000, coins.Balance);
        Assert.AreEqual(0, survivors.Roster.Count);
    }

    // --- Records ---------------------------------------------------------------------------------

    [Test]
    public void BuiltShelterIsRecordedAndComesBackBuilt()
    {
        Site();
        coins.Balance = 100;
        ledger.Add(ResourceType.Wood, 12);

        Assert.IsTrue(shelters.TryBuild("site", out string message), message);
        Assert.IsTrue(progression.Has(ShelterIds.Built("site")));

        // A reload: a fresh service over the same progression.
        var reloaded = new ShelterService(balance, survivors, ledger, coins, progression, machine);
        ShelterUnit again = reloaded.Register(hut, "site", Vector3.zero, startsBuilt: false);

        Assert.IsTrue(again.IsBuilt);
        Assert.IsFalse(reloaded.TryBuild("site", out string reason), "It is never charged for twice.");
        StringAssert.Contains("already", reason);
    }

    [Test]
    public void NewHiresTakeNamesNobodyHas()
    {
        Built();
        Built("second");
        coins.Balance = 1000;
        survivors.Add(new Survivor { Id = "survivor_0", Name = "Kira", Role = guardRole });

        Survivor first = Hire();
        Survivor second = Hire();
        Survivor third = Hire();

        Assert.AreEqual("Mahmoud", first.Name);
        Assert.AreEqual("Tess", second.Name);
        Assert.AreEqual("Osei", third.Name);
        Assert.AreNotEqual(first.Id, second.Id);
        Assert.AreNotEqual(second.Id, third.Id);
    }

    // --- Going to work -------------------------------------------------------------------------

    [Test]
    public void HiredGuardIsPostedToAnEmptyTower()
    {
        var pistol = Make<WeaponDefinition>();
        pistol.rangeMetres = 6f;

        var tower = Make<DefenseDefinition>();
        tower.requiresCrew = true;
        tower.crewRole = SurvivorRole.Guard;
        tower.levels = new[] { new DefenseLevel { weapon = pistol } };

        var defense = new DefenseService(survivors, ledger, coins, progression, machine,
                                         () => new List<IDefenseTarget>(), () => false);
        DefenseUnit post = defense.Register(tower, "gate", Vector3.zero, startsBuilt: true);

        Built();
        coins.Balance = 100;
        Survivor guard = Hire(option: Guard);

        defense.Tick(0.25f);

        Assert.AreSame(guard, post.Crew);
        Assert.IsTrue(post.CanFire);
    }
}
#endif
