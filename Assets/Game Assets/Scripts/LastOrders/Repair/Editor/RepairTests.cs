#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Defense;
using LastOrders.Economy;
using LastOrders.Jobs;
using LastOrders.Perimeter;
using LastOrders.Repair;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Repairs, without a scene: anything hit or knocked down is a job for the Helper, the job
/// finishing is what makes it whole, a second tap is the same job, nothing is fixed after dark
/// or with zombies inside, a price is paid up front or not at all - and nothing mends by itself
/// at dawn any more.
/// </summary>
public class RepairTests
{
    sealed class FakeRepairable : IRepairable
    {
        public string RepairId { get; set; } = "wall_1";
        public string DisplayName => "South wall";
        public float Health { get; set; } = 350f;
        public float MaxHealth => 350f;
        public bool IsDestroyed => Health <= 0f;
        public bool IsRepairableNow { get; set; } = true;
        public Transform RepairTarget => null;
        public Transform RepairAnchor => null;
        public Vector3 RepairPoint => new Vector3(4f, 0f, 2f);
        public int Repairs;

        public void Repair()
        {
            Health = MaxHealth;
            Repairs++;
        }
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

    sealed class FakeWall : ICompoundStructure
    {
        public string DisplayName => "South wall";
        public float Health { get; set; } = 350f;
        public float MaxHealth => 350f;
        public bool IsDestroyed => Health <= 0f;
        public void Repair() => Health = MaxHealth;
    }

    sealed class EmptyState : IDayPhaseState
    {
        public EmptyState(DayPhase phase) => Phase = phase;
        public DayPhase Phase { get; }
        public void OnEnter(DayPhase previous) { }
        public void OnTick(float deltaSeconds) { }
        public void OnExit(DayPhase next) { }
    }

    JobService jobs;
    ResourceLedger ledger;
    FakeWallet coins;
    DayCycleMachine machine;
    bool breached;

    [SetUp]
    public void SetUp()
    {
        jobs = new JobService();
        ledger = new ResourceLedger();
        coins = new FakeWallet();
        breached = false;

        machine = new DayCycleMachine(new TimeService(6f));
        machine.AddState(new EmptyState(DayPhase.Dawn));
        machine.AddState(new EmptyState(DayPhase.Day));
        machine.AddState(new EmptyState(DayPhase.Night));
        machine.Start(DayPhase.Day);
    }

    RepairService Service(RepairTuning tuning = null) =>
        new RepairService(tuning ?? new RepairTuning(), jobs, ledger, coins, machine, () => breached);

    // --- Asking ------------------------------------------------------------------------------

    [Test]
    public void ARepairIsAJobForTheHelperStandingAtTheRepairPoint()
    {
        RepairService repairs = Service();
        var wall = new FakeRepairable { Health = 100f };
        repairs.Register(wall);

        Assert.IsTrue(repairs.RequestRepair(wall, out string message), message);

        Job job = repairs.JobOf(wall);
        Assert.IsNotNull(job);
        Assert.AreEqual(WorkerRole.Helper, job.RequiredRole);
        Assert.AreEqual(wall.RepairPoint, job.InteractionPoint);
        StringAssert.StartsWith(RepairService.JobPrefix, job.TargetId);
        StringAssert.Contains("Helper", message);
    }

    [Test]
    public void TheJobFinishingIsWhatMakesItWhole()
    {
        RepairService repairs = Service();
        var wall = new FakeRepairable { Health = 0f };
        repairs.Register(wall);

        IRepairable reported = null;
        repairs.OnRepaired += r => reported = r;

        repairs.RequestRepair(wall, out _);
        Assert.IsTrue(wall.IsDestroyed, "Nothing is fixed until somebody has done the work.");

        repairs.JobOf(wall).Complete();

        Assert.IsFalse(wall.IsDestroyed);
        Assert.AreEqual(wall.MaxHealth, wall.Health);
        Assert.AreSame(wall, reported);
        Assert.IsNull(repairs.JobOf(wall));
    }

    [Test]
    public void SomethingWholeHasNothingToRepair()
    {
        RepairService repairs = Service();
        var wall = new FakeRepairable();
        repairs.Register(wall);

        Assert.IsFalse(repairs.CanRepair(wall, out string reason));
        StringAssert.Contains("one piece", reason);
        Assert.AreEqual(0, jobs.All.Count);
    }

    [Test]
    public void AskingTwiceIsOneJob()
    {
        RepairService repairs = Service();
        var wall = new FakeRepairable { Health = 50f };
        repairs.Register(wall);

        repairs.RequestRepair(wall, out _);
        Assert.IsFalse(repairs.RequestRepair(wall, out string second));

        StringAssert.Contains("on the way", second);
        Assert.AreEqual(1, jobs.All.Count);
    }

    [Test]
    public void NobodyIsSentAfterDark()
    {
        RepairService repairs = Service();
        var wall = new FakeRepairable { Health = 50f };
        repairs.Register(wall);

        machine.ForceTo(DayPhase.Night);

        Assert.IsFalse(repairs.RequestRepair(wall, out string reason));
        StringAssert.Contains("morning", reason);
    }

    [Test]
    public void NobodyIsSentWithZombiesInside()
    {
        RepairService repairs = Service();
        var wall = new FakeRepairable { Health = 50f };
        repairs.Register(wall);

        breached = true;

        Assert.IsFalse(repairs.RequestRepair(wall, out string reason));
        StringAssert.Contains("zombies", reason);
    }

    [Test]
    public void SomethingOutOfReachCannotBeRepaired()
    {
        RepairService repairs = Service();
        var wall = new FakeRepairable { Health = 50f, IsRepairableNow = false };
        repairs.Register(wall);

        Assert.IsFalse(repairs.CanRepair(wall, out _));
    }

    [Test]
    public void KnockedDownTakesLongerThanHit()
    {
        RepairService repairs = Service();

        float scratched = repairs.WorkSecondsFor(new FakeRepairable { Health = 300f });
        float halfDown = repairs.WorkSecondsFor(new FakeRepairable { Health = 175f });
        float flattened = repairs.WorkSecondsFor(new FakeRepairable { Health = 0f });

        Assert.Less(scratched, halfDown);
        Assert.Less(halfDown, flattened);
    }

    // --- Paying ----------------------------------------------------------------------------

    [Test]
    public void APriceIsPaidUpFrontAndScalesWithTheDamage()
    {
        var tuning = new RepairTuning { fullRepairCost = new Price(0, new ResourceAmount(ResourceType.Wood, 4)) };
        RepairService repairs = Service(tuning);
        ledger.Add(ResourceType.Wood, 10);

        var fallen = new FakeRepairable { RepairId = "a", Health = 0f };
        var halfway = new FakeRepairable { RepairId = "b", Health = 175f };
        repairs.Register(fallen);
        repairs.Register(halfway);

        Assert.AreEqual(4, repairs.PriceOf(fallen).resources.AmountOf(ResourceType.Wood));
        Assert.AreEqual(2, repairs.PriceOf(halfway).resources.AmountOf(ResourceType.Wood));

        Assert.IsTrue(repairs.RequestRepair(fallen, out _));
        Assert.AreEqual(6, ledger.Get(ResourceType.Wood));
    }

    [Test]
    public void ShortOfThePriceNobodyIsSent()
    {
        var tuning = new RepairTuning { fullRepairCost = new Price(10, new ResourceAmount(ResourceType.Wood, 4)) };
        RepairService repairs = Service(tuning);
        ledger.Add(ResourceType.Wood, 10);
        coins.Balance = 3;

        var wall = new FakeRepairable { Health = 0f };
        repairs.Register(wall);

        Assert.IsFalse(repairs.RequestRepair(wall, out string reason));
        StringAssert.Contains("Coins", reason);
        Assert.AreEqual(10, ledger.Get(ResourceType.Wood), "Nothing is taken for a repair that is not happening.");
        Assert.AreEqual(3, coins.Balance);
        Assert.AreEqual(0, jobs.All.Count);
    }

    [Test]
    public void RepairsAreFreeByDefault()
    {
        RepairService repairs = Service();
        Assert.IsTrue(repairs.PriceOf(new FakeRepairable { Health = 0f }).IsFree);
    }

    // --- The world changing under it ---------------------------------------------------------

    [Test]
    public void LeavingTheWorldCallsTheJobOff()
    {
        RepairService repairs = Service();
        var wall = new FakeRepairable { Health = 0f };
        repairs.Register(wall);
        repairs.RequestRepair(wall, out _);

        Job job = repairs.JobOf(wall);
        repairs.Unregister(wall);

        Assert.AreEqual(JobState.Cancelled, job.State);
        Assert.AreEqual(0, wall.Repairs);
    }

    [Test]
    public void AJobRaisedBeforeComingBackIsStillItsJob()
    {
        RepairService repairs = Service();
        var wall = new FakeRepairable { Health = 0f };
        repairs.Register(wall);
        repairs.RequestRepair(wall, out _);

        // Pooled or toggled out and back in with the same identity.
        Job job = repairs.JobOf(wall);
        var again = new FakeRepairable { RepairId = wall.RepairId, Health = 0f };
        repairs.Register(again);

        Assert.AreSame(job, repairs.JobOf(again));
    }

    [Test]
    public void MorningCountsWhatTheNightBroke()
    {
        RepairService repairs = Service();
        repairs.Register(new FakeRepairable { RepairId = "a", Health = 0f });
        repairs.Register(new FakeRepairable { RepairId = "b", Health = 200f });
        repairs.Register(new FakeRepairable { RepairId = "c" });

        int found = -1;
        repairs.OnDamageFound += count => found = count;

        machine.ForceTo(DayPhase.Night);
        machine.ForceTo(DayPhase.Dawn);

        Assert.AreEqual(2, found);
    }

    [Test]
    public void WallsAreNotRebuiltByThemselvesAtDawnWhenRepairsAreByHand()
    {
        var compound = new CompoundPerimeterService(() => new List<IDefenseTarget>(), machine) { RepairsAtDawn = false };

        var fallen = new FakeWall { Health = 0f };
        compound.Register(fallen);

        machine.ForceTo(DayPhase.Night);
        machine.ForceTo(DayPhase.Dawn);

        Assert.IsTrue(fallen.IsDestroyed, "It stays down until a worker is sent to it.");
    }

    [Test]
    public void TheGameTurnsTheDawnRebuildOffByDefault()
    {
        Assert.IsFalse(new RepairTuning().autoRepairAtDawn);
    }
}
#endif
