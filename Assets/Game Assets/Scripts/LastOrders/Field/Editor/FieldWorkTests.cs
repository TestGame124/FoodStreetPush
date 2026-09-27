#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Economy;
using LastOrders.Expansion;
using LastOrders.Field;
using LastOrders.Jobs;
using LastOrders.Medical;
using LastOrders.Progression;
using LastOrders.Survivors;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The rules of sending workers out, without a scene: zombies before debris, one worker to an
/// area, nothing lost on a recall, the warning raised once, a downed worker pausing their area
/// and holding a cot, the Helper carrying before the Chef, and the area picking up again once
/// its worker is well.
/// </summary>
public class FieldWorkTests
{
    sealed class FakeWorker : IFieldWorker
    {
        public FakeWorker(string id, WorkerRole role, bool canClear, bool canCarry)
        {
            WorkerId = id;
            Role = role;
            CanClearAreas = canClear;
            CanCarry = canCarry;
            Health = new WorkerHealth(100f, 0.35f);
        }

        public string WorkerId { get; }
        public string DisplayName => WorkerId;
        public string RoleName => Role.ToString();
        public WorkerRole Role { get; }
        public WorkerHealth Health { get; }
        public Vector3 Position { get; set; }
        public bool CanClearAreas { get; }
        public bool CanCarry { get; }
        public bool IsSheltering { get; set; }
        public string Activity => "Idle";
    }

    sealed class FakeZombie : IAreaZombie
    {
        public FakeZombie(string id, string area, Vector3 at)
        {
            ZombieId = id;
            AreaId = area;
            Position = at;
            Health = MaxHealth;
        }

        public string ZombieId { get; }
        public string AreaId { get; }
        public Vector3 Position { get; }
        public bool IsAlive => Health > 0f;
        public float Health { get; private set; }
        public float MaxHealth => 40f;

        public void ApplyDamage(float amount) => Health = Mathf.Max(0f, Health - amount);
    }

    sealed class IdlePhase : IDayPhaseState
    {
        public IdlePhase(DayPhase phase) => Phase = phase;
        public DayPhase Phase { get; }
        public void OnEnter(DayPhase previous) { }
        public void OnTick(float deltaSeconds) { }
        public void OnExit(DayPhase next) { }
    }

    readonly List<Object> owned = new List<Object>();

    ProgressionService progression;
    JobService jobs;
    DayCycleMachine machine;
    ExpansionService expansion;
    DebrisClearingService clearing;
    HospitalService hospital;
    FieldWorkService field;

    FakeWorker main;
    FakeWorker helper;
    FakeWorker chef;

    [SetUp]
    public void SetUp()
    {
        progression = new ProgressionService();
        var ledger = new ResourceLedger();
        jobs = new JobService();
        machine = new DayCycleMachine(new TimeService(6f));
        var rewards = new RewardService(progression, ledger, _ => { });
        expansion = new ExpansionService(progression, rewards, ledger, machine, () => 1);
        clearing = new DebrisClearingService(jobs, expansion, rewards, progression, machine);
        hospital = new HospitalService(null, null, () => false);
        field = new FieldWorkService(clearing, expansion, hospital, progression, machine);

        // The same wiring GameComposition does.
        clearing.OnDebrisCleared += (id, _) => expansion.HandleDebrisCleared(clearing.AreaOf(id), id);

        main = new FakeWorker("main", WorkerRole.MainCharacter, canClear: true, canCarry: false);
        helper = new FakeWorker("helper", WorkerRole.Helper, canClear: true, canCarry: true);
        chef = new FakeWorker("chef", WorkerRole.Chef, canClear: false, canCarry: true);

        field.Register(main);
        field.Register(helper);
        field.Register(chef);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);

        owned.Clear();
    }

    // --- Helpers ---------------------------------------------------------------------------

    ExpansionAreaDefinition AreaType(string id, params ExpansionAreaDefinition[] unlockedBy)
    {
        var area = ScriptableObject.CreateInstance<ExpansionAreaDefinition>();
        owned.Add(area);

        area.id = id;
        area.displayName = id;
        area.unlockedBy = unlockedBy;
        return area;
    }

    DebrisDefinition DebrisType()
    {
        var type = ScriptableObject.CreateInstance<DebrisDefinition>();
        owned.Add(type);

        type.displayName = "rubble";
        type.workSeconds = 5f;
        type.reward = new RewardBundle(1);
        type.role = WorkerRole.MainCharacter;
        return type;
    }

    /// <summary>An open area with debris at the given work points, on the way out through one route.</summary>
    void OpenArea(string areaId, params Vector3[] piles)
    {
        var ids = new string[piles.Length];
        var route = new[] { new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, 3f) };
        DebrisDefinition type = DebrisType();

        for (int i = 0; i < piles.Length; i++)
        {
            ids[i] = $"{areaId}/{i}";
            clearing.Register(ids[i], areaId, type, null, piles[i], route);
        }

        expansion.RegisterArea(AreaType(areaId), ids);
    }

    FakeZombie Zombie(string id, string areaId, Vector3 at)
    {
        var zombie = new FakeZombie(id, areaId, at);
        field.RegisterZombie(zombie);
        return zombie;
    }

    void Kill(FakeWorker worker, IAreaZombie zombie)
    {
        while (zombie.IsAlive) field.StrikeZombie(worker, zombie, 10f);
    }

    void GoToNight()
    {
        machine.AddState(new IdlePhase(DayPhase.Night));
        machine.ForceTo(DayPhase.Night);
    }

    void Assign(FakeWorker worker, string areaId)
    {
        Assert.IsTrue(field.Assign(areaId, worker.WorkerId, out string message), message);
    }

    // --- Sending people out ----------------------------------------------------------------

    [Test]
    public void AWorkerFightsEveryZombieBeforeAnyDebris()
    {
        OpenArea("yard", new Vector3(0f, 0f, 5f), new Vector3(4f, 0f, 9f));
        FakeZombie near = Zombie("yard/z1", "yard", new Vector3(0f, 0f, 6f));
        FakeZombie far = Zombie("yard/z2", "yard", new Vector3(3f, 0f, 12f));

        Assign(main, "yard");

        FieldTask task = field.NextTask(main);
        Assert.AreEqual(FieldTaskKind.Fight, task.Kind);
        Assert.AreSame(near, task.Zombie, "The nearest zombie goes first.");
        Assert.NotNull(task.Route, "The task carries the way out through the wall.");

        Kill(main, near);
        task = field.NextTask(main);
        Assert.AreEqual(FieldTaskKind.Fight, task.Kind);
        Assert.AreSame(far, task.Zombie);

        Kill(main, far);
        task = field.NextTask(main);
        Assert.AreEqual(FieldTaskKind.Clear, task.Kind, "Only with the land quiet does the clearing start.");
        Assert.AreEqual("yard/0", task.DebrisId, "The nearest pile first.");
    }

    [Test]
    public void TheChefIsNeverSentOut()
    {
        OpenArea("yard", Vector3.zero);

        Assert.IsFalse(field.CanAssign("yard", chef, out string reason));
        StringAssert.Contains("cannot be sent", reason);
    }

    [Test]
    public void LockedLandAndNightRefuseWorkers()
    {
        ExpansionAreaDefinition first = AreaType("first");
        expansion.RegisterArea(first, new[] { "first/a" });
        clearing.Register("first/a", "first", DebrisType(), null, Vector3.zero, null);

        expansion.RegisterArea(AreaType("second", first), new[] { "second/a" });
        clearing.Register("second/a", "second", DebrisType(), null, Vector3.zero, null);

        Assert.IsFalse(field.CanAssign("second", main, out string locked));
        StringAssert.Contains("first", locked, "The reason names the land to claim first.");

        GoToNight();
        Assert.IsFalse(field.CanAssign("first", main, out string night));
        StringAssert.Contains("dark", night);
    }

    [Test]
    public void OneWorkerClearsAnAreaAtATime()
    {
        OpenArea("yard", Vector3.zero);
        Assign(main, "yard");

        Assert.IsFalse(field.CanAssign("yard", helper, out string reason));
        StringAssert.Contains("Recall", reason);
    }

    // --- Nothing is lost --------------------------------------------------------------------

    [Test]
    public void RecallKeepsTheHalfClearedPileAndTheDeadZombies()
    {
        OpenArea("yard", new Vector3(0f, 0f, 5f));
        FakeZombie zombie = Zombie("yard/z1", "yard", new Vector3(0f, 0f, 6f));

        Assign(main, "yard");
        Kill(main, zombie);
        Assert.IsTrue(field.IsKilled("yard/z1"), "A kill is recorded, so the zombie stays dead.");

        FieldTask task = field.NextTask(main);
        Job job = field.ClaimDebrisJob(main, task.DebrisId, out _);
        Assert.NotNull(job);
        job.BeginTravel();
        job.BeginWork();
        job.WorkRemaining = 2f;

        Assert.IsTrue(field.Recall(main.WorkerId, out _));
        Assert.AreEqual(JobState.Cancelled, job.State);
        Assert.IsNull(field.AreaOf(main));
        Assert.AreEqual(0.6f, clearing.ProgressOf(task.DebrisId), 1e-4f, "Three of five seconds done stays done.");

        Assign(helper, "yard");
        FieldTask again = field.NextTask(helper);
        Assert.AreEqual(FieldTaskKind.Clear, again.Kind, "The dead zombie is not fought twice.");

        Job resumed = field.ClaimDebrisJob(helper, again.DebrisId, out _);
        Assert.AreEqual(2f, resumed.WorkRemaining, 1e-4f, "Whoever goes next starts where it stopped.");
    }

    [Test]
    public void AJobHeldForAWorkerIsNotTakenByAnother()
    {
        OpenArea("yard", new Vector3(0f, 0f, 5f));
        Assign(helper, "yard");

        FieldTask task = field.NextTask(helper);
        Job job = field.ClaimDebrisJob(helper, task.DebrisId, out _);

        // Dusk: the worker goes in and the job goes back on the queue.
        jobs.ReleaseAllFor(helper.WorkerId);
        Assert.AreEqual(JobState.Queued, job.State);

        Assert.IsFalse(jobs.TryClaim(WorkerRole.MainCharacter, main.WorkerId, out _),
                       "The main character's queue matches the job's role, but it is held for the Helper.");

        Assert.AreSame(job, field.ClaimDebrisJob(helper, task.DebrisId, out _), "In the morning it is theirs again.");
        Assert.AreEqual(helper.WorkerId, job.OwnerId);
    }

    // --- Getting hurt ----------------------------------------------------------------------

    [Test]
    public void ZombiesOnlyHurtWorkersOutThere()
    {
        OpenArea("yard", Vector3.zero);

        Assert.IsFalse(field.DamageWorker(helper, 10f), "Nobody at the restaurant can be hit.");
        Assert.AreEqual(100f, helper.Health.Current);

        Assign(helper, "yard");
        Assert.IsTrue(field.DamageWorker(helper, 10f));
        Assert.AreEqual(90f, helper.Health.Current);
    }

    [Test]
    public void TheLowHealthWarningIsRaisedOnceOnTheWayDown()
    {
        var health = new WorkerHealth(100f, 0.35f);
        int warnings = 0;
        health.OnLow += () => warnings++;

        health.Damage(60f);
        Assert.AreEqual(0, warnings);

        health.Damage(10f);
        health.Damage(5f);
        Assert.AreEqual(1, warnings, "Once per trip below the line, not once per hit.");
        Assert.IsTrue(health.IsLow);

        health.Heal(25f);
        health.Damage(20f);
        Assert.AreEqual(2, warnings, "Healed clear of the line, the next drop warns again.");
    }

    [Test]
    public void ADownedWorkerPausesTheirAreaAndHoldsACot()
    {
        OpenArea("yard", new Vector3(0f, 0f, 5f));
        Assign(helper, "yard");

        FieldTask task = field.NextTask(helper);
        Job job = field.ClaimDebrisJob(helper, task.DebrisId, out _);
        job.BeginTravel();
        job.BeginWork();
        job.WorkRemaining = 1f;

        field.DamageWorker(helper, 100f);

        Assert.IsTrue(helper.Health.IsDowned);
        Assert.IsTrue(field.IsCasualty(helper));
        Assert.AreEqual(CasualtyStage.AwaitingPickup, field.StageOf(helper));

        Assert.IsTrue(field.IsPaused("yard"), "The area waits for them.");
        Assert.AreEqual("yard", field.AreaOf(helper));
        Assert.IsFalse(field.IsOnActiveAssignment(helper));
        Assert.AreEqual(FieldTaskKind.None, field.NextTask(helper).Kind, "They stop working.");

        Assert.AreEqual(JobState.Cancelled, job.State);
        Assert.AreEqual(0.8f, clearing.ProgressOf(task.DebrisId), 1e-4f, "The pile keeps its progress.");

        HospitalCot cot = field.CotOf(helper);
        Assert.NotNull(cot, "A cot is held for them from the moment they fall.");
        Assert.IsFalse(cot.InBed, "Treatment waits until they are carried in.");
    }

    // --- Carried in -------------------------------------------------------------------------

    [Test]
    public void TheHelperCarriesBeforeTheChef()
    {
        OpenArea("yard", Vector3.zero);
        Assign(main, "yard");
        field.DamageWorker(main, 100f);

        Assert.IsFalse(field.TryTakeRescue(chef, out _), "The Chef stays at the stove while the Helper is free.");
        Assert.IsTrue(field.TryTakeRescue(helper, out IFieldWorker casualty));
        Assert.AreSame(main, casualty);
        Assert.AreSame(helper, field.RescuerOf(main));
        Assert.IsFalse(field.TryTakeRescue(chef, out _), "One carrier per casualty.");
    }

    [Test]
    public void TheChefCarriesWhenTheHelperIsTheOneDown()
    {
        OpenArea("yard", Vector3.zero);
        Assign(helper, "yard");
        field.DamageWorker(helper, 100f);

        Assert.IsFalse(field.TryTakeRescue(main, out _), "The main character is not restaurant staff.");
        Assert.IsTrue(field.TryTakeRescue(chef, out IFieldWorker casualty));
        Assert.AreSame(helper, casualty);
    }

    [Test]
    public void AHelperOutClearingIsNotSentToCarry()
    {
        OpenArea("yard", Vector3.zero);
        OpenArea("field", Vector3.zero);

        Assign(helper, "yard");
        Assign(main, "field");
        field.DamageWorker(main, 100f);

        Assert.IsFalse(field.TryTakeRescue(helper, out _));
        Assert.IsTrue(field.TryTakeRescue(chef, out _), "With the Helper out there, the Chef goes.");
    }

    [Test]
    public void DeliveredWorkersRecoverAndGoBackToTheirArea()
    {
        OpenArea("yard", new Vector3(0f, 0f, 5f));
        Assign(main, "yard");
        field.DamageWorker(main, 100f);

        int recovered = 0;
        field.OnRecovered += w => recovered += w == main ? 1 : 0;

        Assert.IsTrue(field.TryTakeRescue(helper, out _));
        field.PickedUp(main);
        Assert.AreEqual(CasualtyStage.Carried, field.StageOf(main));

        HospitalCot cot = field.Delivered(main);
        Assert.NotNull(cot);
        Assert.IsTrue(cot.InBed, "Laid in the cot, treatment starts.");
        Assert.IsFalse(field.IsRescuing(helper));

        hospital.Tick(200f);

        Assert.AreEqual(1, recovered);
        Assert.IsFalse(field.IsCasualty(main));
        Assert.IsTrue(main.Health.IsFull);
        Assert.IsTrue(field.IsOnActiveAssignment(main), "The paused area picks up again.");
        Assert.AreEqual(FieldTaskKind.Clear, field.NextTask(main).Kind);
    }

    [Test]
    public void WithEveryCotTakenTheyWaitBesideTheTent()
    {
        OpenArea("yard", Vector3.zero);

        hospital.AdmitSurvivor(new Survivor { Id = "a", Name = "A" });
        hospital.AdmitSurvivor(new Survivor { Id = "b", Name = "B" });
        hospital.Tick(50f);

        Assign(main, "yard");
        field.DamageWorker(main, 100f);

        HospitalCot ready = null;
        field.OnCotReady += (w, c) => ready = w == main ? c : ready;

        Assert.IsTrue(field.TryTakeRescue(helper, out _));
        field.PickedUp(main);
        Assert.IsNull(field.Delivered(main), "No cot yet.");
        Assert.AreEqual(0, field.WaitingIndexOf(main));

        hospital.Tick(150f);

        Assert.NotNull(ready, "The first cot to come free is theirs.");
        Assert.IsTrue(ready.InBed);
        Assert.AreSame(ready, field.CotOf(main));
    }

    [Test]
    public void RecallingADownedWorkerLetsTheAreaGo()
    {
        OpenArea("yard", Vector3.zero);
        Assign(main, "yard");
        field.DamageWorker(main, 100f);

        Assert.IsTrue(field.Recall(main.WorkerId, out _));
        Assert.IsNull(field.AreaOf(main));

        field.TryTakeRescue(helper, out _);
        field.Delivered(main);
        hospital.Tick(200f);

        Assert.IsFalse(field.IsOnActiveAssignment(main), "Recalled, they stay at the restaurant once well.");
    }

    [Test]
    public void SomeoneElseCanTakeOverAPausedArea()
    {
        OpenArea("yard", Vector3.zero);
        Assign(main, "yard");
        field.DamageWorker(main, 100f);

        Assign(helper, "yard");

        Assert.IsNull(field.AreaOf(main));
        Assert.AreSame(helper, field.WorkerOn("yard"));
        Assert.IsFalse(field.IsPaused("yard"));
    }

    [Test]
    public void ClearingTheLastPileEndsTheAssignment()
    {
        OpenArea("yard", new Vector3(0f, 0f, 5f));
        Assign(main, "yard");

        FieldEnd? ended = null;
        field.OnAssignmentEnded += (w, area, reason) => ended = reason;

        FieldTask task = field.NextTask(main);
        Job job = field.ClaimDebrisJob(main, task.DebrisId, out _);
        job.BeginTravel();
        job.BeginWork();
        job.Complete();

        Assert.AreEqual(AreaState.ReadyToExpand, expansion.StateOf("yard"));
        Assert.AreEqual(FieldEnd.Finished, ended);
        Assert.IsNull(field.AreaOf(main));
    }

    [Test]
    public void TheWorkerPanelOffersRecallOnlyWhileOut()
    {
        OpenArea("yard", Vector3.zero);

        FieldPanel idle = field.GetWorkerPanel(helper.WorkerId);
        Assert.IsFalse(idle.ShowRecall);
        Assert.AreEqual(1, idle.Options.Count);
        Assert.IsTrue(idle.Options[0].Enabled);

        Assert.IsTrue(field.TakeWorkerOption(helper.WorkerId, 0, out _));

        FieldPanel busy = field.GetWorkerPanel(helper.WorkerId);
        Assert.IsTrue(busy.ShowRecall);
        Assert.IsTrue(busy.ShowHealth);
        Assert.AreEqual(1f, busy.Health, 1e-4f);
    }
}
#endif
