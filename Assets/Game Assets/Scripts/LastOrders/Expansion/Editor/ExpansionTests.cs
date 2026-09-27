#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Text.RegularExpressions;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Economy;
using LastOrders.Expansion;
using LastOrders.Jobs;
using LastOrders.Progression;
using LastOrders.UI.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The rules of the expansion loop, without a scene: one job per pile however often it is
/// asked for, every payout exactly once, locked land refusing work, land made ready only by
/// the clearance that empties it, and claimed only by a paid-for build running to the end.
/// </summary>
public class ExpansionTests
{
    readonly List<Object> owned = new List<Object>();

    ProgressionService progression;
    ResourceLedger ledger;
    JobService jobs;
    DayCycleMachine machine;
    RewardService rewards;
    ExpansionService expansion;
    DebrisClearingService clearing;

    int experience;
    int clearedEvents;
    int level;

    [SetUp]
    public void SetUp()
    {
        experience = 0;
        clearedEvents = 0;
        level = 1;

        progression = new ProgressionService();
        ledger = new ResourceLedger();
        jobs = new JobService();
        machine = new DayCycleMachine(new TimeService(6f));
        rewards = new RewardService(progression, ledger, amount => experience += amount);
        expansion = new ExpansionService(progression, rewards, ledger, machine, () => level);
        clearing = new DebrisClearingService(jobs, expansion, rewards, progression, machine);

        // The same wiring GameComposition does.
        clearing.OnDebrisCleared += (id, _) =>
        {
            clearedEvents++;
            expansion.HandleDebrisCleared(clearing.AreaOf(id), id);
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

    DebrisDefinition DebrisType(int xp = 2, int wood = 3)
    {
        var type = ScriptableObject.CreateInstance<DebrisDefinition>();
        owned.Add(type);

        type.displayName = "rubble";
        type.workSeconds = 5f;
        type.reward = new RewardBundle(xp, new ResourceAmount(ResourceType.Wood, wood));
        type.role = WorkerRole.MainCharacter;
        return type;
    }

    ExpansionAreaDefinition AreaType(string id, int claimXp = 5, params ExpansionAreaDefinition[] unlockedBy)
    {
        var area = ScriptableObject.CreateInstance<ExpansionAreaDefinition>();
        owned.Add(area);

        area.id = id;
        area.displayName = id;
        area.unlockedBy = unlockedBy;
        area.claimReward = new RewardBundle(claimXp);
        area.expandSeconds = 10f;
        return area;
    }

    void RegisterDebris(string id, string areaId, DebrisDefinition type = null)
    {
        clearing.Register(id, areaId, type ?? DebrisType(), null, Vector3.zero, null);
    }

    /// <summary>Plays a worker's part: claim, walk, work, finish.</summary>
    void FinishJobFor(string debrisId)
    {
        Job job = jobs.FindActiveFor(DebrisClearingService.JobPrefix + debrisId);
        Assert.NotNull(job, $"No job was queued for {debrisId}.");

        Assert.IsTrue(job.TryReserve("worker"));
        job.BeginTravel();
        job.BeginWork();
        job.Complete();
    }

    /// <summary>Pays for the expansion and runs its build to the end.</summary>
    void Expand(string areaId)
    {
        Assert.IsTrue(expansion.StartExpansion(areaId, out string message), message);
        expansion.Tick(1000f);
    }

    void Clear(string debrisId)
    {
        Assert.IsTrue(clearing.RequestClear(debrisId, out string message), message);
        FinishJobFor(debrisId);
    }

    // --- Requests --------------------------------------------------------------------------

    [Test]
    public void SecondRequestForTheSamePileRaisesNoSecondJob()
    {
        RegisterOpenArea("yard", "yard/a");

        Assert.IsTrue(clearing.RequestClear("yard/a", out _));
        Assert.IsFalse(clearing.RequestClear("yard/a", out string reason));

        StringAssert.Contains("Already", reason);
        Assert.AreEqual(1, jobs.All.Count);
        Assert.AreEqual(DebrisState.Queued, clearing.StateOf("yard/a"));
    }

    [Test]
    public void JobStateIsMirroredOntoTheDebris()
    {
        RegisterOpenArea("yard", "yard/a");
        clearing.RequestClear("yard/a", out _);

        Job job = jobs.FindActiveFor(DebrisClearingService.JobPrefix + "yard/a");
        job.TryReserve("worker");
        job.BeginTravel();
        Assert.AreEqual(DebrisState.Queued, clearing.StateOf("yard/a"));

        job.BeginWork();
        Assert.AreEqual(DebrisState.Working, clearing.StateOf("yard/a"));

        job.Cancel();
        Assert.AreEqual(DebrisState.Idle, clearing.StateOf("yard/a"));
        Assert.IsNull(clearing.JobOf("yard/a"));
    }

    /// <summary>A phase that does nothing, so the machine can be put in it without complaint.</summary>
    sealed class IdlePhase : IDayPhaseState
    {
        public IdlePhase(DayPhase phase) => Phase = phase;
        public DayPhase Phase { get; }
        public void OnEnter(DayPhase previous) { }
        public void OnTick(float deltaSeconds) { }
        public void OnExit(DayPhase next) { }
    }

    [Test]
    public void NoNewWorkIsStartedAtNight()
    {
        RegisterOpenArea("yard", "yard/a");
        machine.AddState(new IdlePhase(DayPhase.Night));
        machine.ForceTo(DayPhase.Night);

        Assert.IsFalse(clearing.CanClear("yard/a", out string reason));
        StringAssert.Contains("dark", reason);
        Assert.AreEqual(0, jobs.All.Count);
    }

    // --- Payouts ---------------------------------------------------------------------------

    [Test]
    public void ClearingPaysExperienceAndResourcesExactlyOnce()
    {
        // Two pieces, so clearing the first cannot also claim the area and muddy the totals.
        var area = AreaType("yard", claimXp: 0);
        RegisterDebris("yard/a", "yard", DebrisType(xp: 2, wood: 3));
        RegisterDebris("yard/b", "yard");
        expansion.RegisterArea(area, new[] { "yard/a", "yard/b" });

        Clear("yard/a");

        Assert.AreEqual(2, experience);
        Assert.AreEqual(3, ledger.Get(ResourceType.Wood));
        Assert.AreEqual(1, clearedEvents);
        Assert.IsTrue(progression.Has(ExpansionIds.DebrisCleared("yard/a")));
        Assert.AreEqual(DebrisState.Cleared, clearing.StateOf("yard/a"));

        // A second completion of the same job, and a second request, change nothing.
        jobs.All[0].Complete();
        Assert.IsFalse(clearing.RequestClear("yard/a", out _));

        Assert.AreEqual(2, experience);
        Assert.AreEqual(3, ledger.Get(ResourceType.Wood));
        Assert.AreEqual(1, clearedEvents);
    }

    [Test]
    public void DebrisClearedBeforeRegistersAsCleared()
    {
        progression.Set(ExpansionIds.DebrisCleared("yard/a"));

        DebrisState state = clearing.Register("yard/a", "yard", DebrisType(), null, Vector3.zero, null);

        Assert.AreEqual(DebrisState.Cleared, state);
        Assert.IsFalse(clearing.CanClear("yard/a", out _));
    }

    // --- Land ------------------------------------------------------------------------------

    [Test]
    public void LastClearanceReadiesTheLandButDoesNotClaimIt()
    {
        var area = AreaType("yard", claimXp: 5);
        RegisterDebris("yard/a", "yard", DebrisType(xp: 1, wood: 0));
        RegisterDebris("yard/b", "yard", DebrisType(xp: 1, wood: 0));
        expansion.RegisterArea(area, new[] { "yard/a", "yard/b" });

        Assert.AreEqual(AreaState.Available, expansion.StateOf("yard"));

        Clear("yard/a");
        Assert.AreEqual(AreaState.Available, expansion.StateOf("yard"));
        Assert.AreEqual(1, expansion.RemainingIn("yard"));

        Clear("yard/b");

        Assert.AreEqual(AreaState.ReadyToExpand, expansion.StateOf("yard"));
        Assert.IsFalse(progression.Has(ExpansionIds.AreaClaimed("yard")));
        Assert.AreEqual(1 + 1, experience);
    }

    [Test]
    public void ExpandingChargesOnceBuildsThenClaims()
    {
        var area = AreaType("yard", claimXp: 5);
        area.expandCost = new[] { new ResourceAmount(ResourceType.Wood, 10), new ResourceAmount(ResourceType.Scrap, 4) };
        RegisterDebris("yard/a", "yard", DebrisType(xp: 1, wood: 0));
        expansion.RegisterArea(area, new[] { "yard/a" });
        Clear("yard/a");

        ledger.Add(ResourceType.Wood, 12);
        ledger.Add(ResourceType.Scrap, 4);

        string claimed = null;
        expansion.OnAreaClaimed += id => claimed = id;

        Assert.IsTrue(expansion.StartExpansion("yard", out _));
        Assert.AreEqual(AreaState.Expanding, expansion.StateOf("yard"));
        Assert.AreEqual(2, ledger.Get(ResourceType.Wood));
        Assert.AreEqual(0, ledger.Get(ResourceType.Scrap));

        // Asked twice, paid once.
        Assert.IsFalse(expansion.StartExpansion("yard", out string again));
        StringAssert.Contains("already", again);
        Assert.AreEqual(2, ledger.Get(ResourceType.Wood));

        expansion.Tick(4f);
        Assert.AreEqual(0.4f, expansion.ExpansionProgress("yard"), 0.001f);
        Assert.AreEqual(6f, expansion.ExpansionSecondsLeft("yard"), 0.001f);
        Assert.IsNull(claimed);

        expansion.Tick(6.5f);

        Assert.AreEqual(AreaState.Claimed, expansion.StateOf("yard"));
        Assert.AreEqual("yard", claimed);
        Assert.IsTrue(progression.Has(ExpansionIds.AreaClaimed("yard")));
        Assert.AreEqual(1 + 5, experience);

        expansion.Tick(10f);
        Assert.AreEqual(1 + 5, experience);
    }

    [Test]
    public void OfferListsWhatIsNeededAndWhyItIsRefused()
    {
        var area = AreaType("yard");
        area.requiredLevel = 3;
        area.expandCost = new[] { new ResourceAmount(ResourceType.Wood, 15) };
        RegisterDebris("yard/a", "yard", DebrisType(xp: 0, wood: 5));
        expansion.RegisterArea(area, new[] { "yard/a" });
        Clear("yard/a");

        ExpansionOffer offer = expansion.GetOffer("yard");

        Assert.AreEqual(2, offer.Requirements.Count);
        Assert.AreEqual("Level", offer.Requirements[0].Label);
        Assert.IsFalse(offer.Requirements[0].Met);
        Assert.AreEqual(5, offer.Requirements[1].Have);
        Assert.AreEqual(15, offer.Requirements[1].Need);
        Assert.IsFalse(offer.CanExpand);
        StringAssert.Contains("level 3", offer.Reason);

        level = 3;
        offer = expansion.GetOffer("yard");
        Assert.IsTrue(offer.Requirements[0].Met);
        StringAssert.Contains("10 more Wood", offer.Reason);

        ledger.Add(ResourceType.Wood, 10);
        offer = expansion.GetOffer("yard");
        Assert.IsTrue(offer.CanExpand, offer.Reason);
        Assert.IsTrue(offer.Requirements[1].Met);
    }

    [Test]
    public void NoExpansionIsStartedAtNight()
    {
        var area = AreaType("yard");
        RegisterDebris("yard/a", "yard");
        expansion.RegisterArea(area, new[] { "yard/a" });
        Clear("yard/a");

        machine.AddState(new IdlePhase(DayPhase.Night));
        machine.ForceTo(DayPhase.Night);

        Assert.IsFalse(expansion.StartExpansion("yard", out string reason));
        StringAssert.Contains("dark", reason);
        Assert.AreEqual(AreaState.ReadyToExpand, expansion.StateOf("yard"));
    }

    [Test]
    public void AreaWithNoDebrisIsNeverReady()
    {
        LogAssert.Expect(LogType.Warning, new Regex("has no debris"));

        expansion.RegisterArea(AreaType("empty"), new string[0]);

        Assert.AreEqual(AreaState.Available, expansion.StateOf("empty"));
        Assert.IsFalse(expansion.CanExpand("empty", out _));
    }

    [Test]
    public void LockedLandRefusesWorkUntilTheLandBeforeItIsClaimed()
    {
        var first = AreaType("courtyard", claimXp: 0);
        var second = AreaType("grocery", claimXp: 0, unlockedBy: first);

        RegisterDebris("courtyard/a", "courtyard");
        RegisterDebris("grocery/a", "grocery");
        expansion.RegisterArea(first, new[] { "courtyard/a" });
        expansion.RegisterArea(second, new[] { "grocery/a" });

        Assert.AreEqual(AreaState.Locked, expansion.StateOf("grocery"));
        Assert.IsFalse(clearing.CanClear("grocery/a", out string reason));
        StringAssert.Contains("courtyard", reason);

        // Cleared is not enough; the land before it has to be built into and claimed.
        Clear("courtyard/a");
        Assert.AreEqual(AreaState.Locked, expansion.StateOf("grocery"));

        Expand("courtyard");

        Assert.AreEqual(AreaState.Claimed, expansion.StateOf("courtyard"));
        Assert.AreEqual(AreaState.Available, expansion.StateOf("grocery"));
        Assert.IsTrue(clearing.CanClear("grocery/a", out _));
    }

    [Test]
    public void ClearedLandRestoresReadyAndPaidLandRestoresBuilding()
    {
        progression.Set(ExpansionIds.DebrisCleared("yard/a"));
        progression.Set(ExpansionIds.DebrisCleared("field/a"));
        progression.Set(ExpansionIds.ExpansionStarted("field"));

        RegisterDebris("yard/a", "yard");
        RegisterDebris("field/a", "field");
        expansion.RegisterArea(AreaType("yard"), new[] { "yard/a" });
        expansion.RegisterArea(AreaType("field"), new[] { "field/a" });

        Assert.AreEqual(AreaState.ReadyToExpand, expansion.StateOf("yard"));

        // Paid for before: building again, with the timer restarted and nothing charged.
        Assert.AreEqual(AreaState.Expanding, expansion.StateOf("field"));
        Assert.AreEqual(10f, expansion.ExpansionSecondsLeft("field"), 0.001f);
        Assert.AreEqual(0, experience);
    }

    [Test]
    public void ClaimRecordedBeforeRestoresWithoutPayingAgain()
    {
        var area = AreaType("yard", claimXp: 5);
        progression.Set(ExpansionIds.DebrisCleared("yard/a"));
        progression.Set(ExpansionIds.AreaClaimed("yard"));
        progression.GrantOnce(ExpansionIds.AreaReward("yard"), null);

        RegisterDebris("yard/a", "yard");
        expansion.RegisterArea(area, new[] { "yard/a" });

        Assert.AreEqual(AreaState.Claimed, expansion.StateOf("yard"));
        Assert.AreEqual(0, experience);
    }

    // --- Jobs and routes -------------------------------------------------------------------

    [Test]
    public void RouteSurvivesTheQueueHandingBackAnExistingJob()
    {
        var route = new[] { new Vector3(30f, 0f, 33f), new Vector3(30f, 0f, 37f) };

        Job first = jobs.Queue("target", null, WorkerRole.MainCharacter, Vector3.one, 5f, "clear", route);
        Job second = jobs.Queue("target", null, WorkerRole.MainCharacter, Vector3.one, 5f, "clear");

        Assert.AreSame(first, second);
        Assert.AreSame(route, second.Route);
    }

    [Test]
    public void DebrisJobsGoToTheMainCharacter()
    {
        RegisterOpenArea("yard", "yard/a");
        clearing.RequestClear("yard/a", out _);

        Assert.IsFalse(jobs.TryClaim(WorkerRole.Helper, "helper", out _));
        Assert.IsTrue(jobs.TryClaim(WorkerRole.MainCharacter, "main", out Job claimed));
        Assert.AreEqual(DebrisClearingService.JobPrefix + "yard/a", claimed.TargetId);
    }

    // --- World UI ---------------------------------------------------------------------------

    [Test]
    public void ReleasingWorldUiTwiceKeepsTheInstance()
    {
        var root = new GameObject("WorldUI test root");
        owned.Add(root);

        var template = new GameObject("Progress template", typeof(RectTransform));
        owned.Add(template);
        template.AddComponent<Canvas>();
        WorkProgressView prefab = template.AddComponent<WorkProgressView>();

        var catalog = ScriptableObject.CreateInstance<WorldUiCatalog>();
        owned.Add(catalog);
        catalog.workProgress = prefab;

        var ui = new WorldUiService(catalog, root.transform);

        WorkProgressView shown = ui.ShowProgress(null);
        Assert.NotNull(shown);
        Assert.AreEqual(1, ui.LiveCount);

        shown.Release();
        ui.Release(shown);

        Assert.AreEqual(0, ui.LiveCount);
        Assert.IsTrue(shown != null, "A second release destroyed a pooled instance.");

        // And the pool hands the same instance straight back.
        Assert.AreSame(shown, ui.ShowProgress(null));
    }

    AreaState RegisterOpenArea(string areaId, string debrisId)
    {
        RegisterDebris(debrisId, areaId);
        expansion.RegisterArea(AreaType(areaId), new[] { debrisId });
        return expansion.StateOf(areaId);
    }
}
#endif
