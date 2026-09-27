#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.DayCycle.States;
using LastOrders.Defense;
using LastOrders.Economy;
using LastOrders.Medical;
using LastOrders.Night;
using LastOrders.Progression;
using LastOrders.Survivors;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The starting threat, without a scene: the night arrives in small groups up one lane each,
/// the same night every time; each group is announced before it arrives; a zombie nothing can
/// reach does not hold the night open past dawn; only zombies at the diner door make a night
/// difficult; a difficult night hurts one of the people who were exposed and sends them to the
/// hospital, and a quiet one hurts nobody; and the walk into the diner never goes through a wall.
/// </summary>
public class NightThreatTests
{
    readonly List<Object> owned = new List<Object>();
    readonly List<IDefenseTarget> hostiles = new List<IDefenseTarget>();

    GameBalanceConfig balance;
    EnemyDefinition zombie;

    [SetUp]
    public void SetUp()
    {
        hostiles.Clear();

        balance = Make<GameBalanceConfig>();
        balance.heatWeights = Make<HeatWeightsConfig>();

        zombie = Make<EnemyDefinition>();
        zombie.displayName = "Zombie";
        zombie.threatCost = 1f;
        zombie.earliestDay = 1;
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

    GameObject MakeObject(string name)
    {
        var go = new GameObject(name);
        owned.Add(go);
        return go;
    }

    WaveManifest Manifest(float budget)
    {
        var manifest = Make<WaveManifest>();
        manifest.minTier = 0;
        manifest.maxTier = 5;
        manifest.earliestDay = 1;
        manifest.entries = new[] { new WaveManifest.Entry { enemy = zombie, weight = 1f } };
        manifest.nightDurationSeconds = 120f;
        manifest.approachLanes = 2;
        manifest.budgetPerHeat = 1f;
        manifest.minimumBudget = budget;
        manifest.groupSize = new Vector2Int(2, 4);
        manifest.groupStaggerSeconds = 1.2f;
        return manifest;
    }

    sealed class FakeTarget : IDefenseTarget
    {
        public Vector3 Position { get; set; }
        public bool Alive = true;
        public bool IsAlive => Alive;
        public string DisplayName => "Zombie";
        public void ApplyDamage(float amount) { }
    }

    sealed class FakeVisitor : IPatient
    {
        public FakeVisitor(string id) => PatientId = id;
        public string PatientId { get; }
        public string DisplayName => "Trucker";
        public PatientKind Kind => PatientKind.Visitor;
    }

    sealed class IdlePhase : IDayPhaseState
    {
        public IdlePhase(DayPhase phase) => Phase = phase;
        public DayPhase Phase { get; }
        public void OnEnter(DayPhase previous) { }
        public void OnTick(float deltaSeconds) { }
        public void OnExit(DayPhase next) { }
    }

    /// <summary>A night running a hand-written plan, with just enough context to tick.</summary>
    (DayCycleMachine machine, NightState night) Night(WavePlan plan)
    {
        var time = new TimeService(6f);
        var context = new DayCycleContext(balance, time, null, null, new ResourceLedger(), null, null, null,
                                          new CrisisBarService(balance, new ResourceLedger()), null, null, null,
                                          null, null, null)
        {
            TonightsWave = plan,
        };

        var machine = new DayCycleMachine(time);
        var night = new NightState(context, machine);

        machine.AddState(night);
        machine.AddState(new IdlePhase(DayPhase.Resolution));
        machine.Start(DayPhase.Night);

        return (machine, night);
    }

    static void Run(DayCycleMachine machine, float seconds)
    {
        int steps = Mathf.RoundToInt(seconds / 0.25f);
        for (int i = 0; i < steps; i++) machine.Tick(0.25f);
    }

    // --- Small groups ------------------------------------------------------------------------

    [Test]
    public void NightArrivesInSmallGroups()
    {
        balance.waveManifests = new[] { Manifest(budget: 13f) };

        WavePlan plan = new WaveDirector(balance, balance.heatWeights).BuildPlan(day: 3, heat: 0f);

        Assert.AreEqual(13, plan.Spawns.Count);

        int total = 0;
        int undersized = 0;

        foreach (WaveGroup group in plan.Groups)
        {
            Assert.LessOrEqual(group.Count, 4);
            Assert.GreaterOrEqual(group.Count, 1);
            if (group.Count < 2) undersized++;
            total += group.Count;
        }

        Assert.AreEqual(plan.Spawns.Count, total, "Every arrival belongs to exactly one group.");
        Assert.LessOrEqual(undersized, 1, "Only the last group can be short, when too few are left.");
    }

    [Test]
    public void GroupComesUpOneLaneAFewSecondsApart()
    {
        balance.waveManifests = new[] { Manifest(budget: 13f) };

        WavePlan plan = new WaveDirector(balance, balance.heatWeights).BuildPlan(day: 3, heat: 0f);

        foreach (WaveGroup group in plan.Groups)
        {
            for (int k = 0; k < group.Count; k++)
            {
                float due = group.TimeSeconds + 1.2f * k;
                bool found = plan.Spawns.Exists(s => s.Lane == group.Lane && Mathf.Abs(s.TimeSeconds - due) < 1e-3f);
                Assert.IsTrue(found, $"Member {k} of the group at {group.TimeSeconds:0.0}s should arrive at {due:0.0}s on lane {group.Lane}.");
            }
        }

        foreach (WaveSpawn spawn in plan.Spawns)
            Assert.LessOrEqual(spawn.TimeSeconds, plan.DurationSeconds + 1e-3f, "Nobody is scheduled after the night ends.");
    }

    [Test]
    public void SameNightIsTheSameNightEveryTime()
    {
        balance.waveManifests = new[] { Manifest(budget: 13f) };
        var director = new WaveDirector(balance, balance.heatWeights);

        WavePlan first = director.BuildPlan(day: 3, heat: 0f);
        WavePlan second = director.BuildPlan(day: 3, heat: 0f);

        Assert.AreEqual(first.Spawns.Count, second.Spawns.Count);
        for (int i = 0; i < first.Spawns.Count; i++)
        {
            Assert.AreEqual(first.Spawns[i].TimeSeconds, second.Spawns[i].TimeSeconds);
            Assert.AreEqual(first.Spawns[i].Lane, second.Spawns[i].Lane);
        }

        Assert.AreEqual(first.Groups.Count, second.Groups.Count);
    }

    // --- The warning -------------------------------------------------------------------------

    [Test]
    public void GroupIsAnnouncedBeforeItArrives()
    {
        balance.incomingWarningSeconds = 6f;

        var plan = new WavePlan { DurationSeconds = 60f, LaneCount = 1 };
        plan.Groups.Add(new WaveGroup(10f, 0, 3, zombie));
        for (int k = 0; k < 3; k++) plan.Spawns.Add(new WaveSpawn(zombie, 10f + k, 0));

        var (machine, night) = Night(plan);

        var warnings = new List<WaveGroup>();
        int spawned = 0;
        night.OnGroupIncoming += warnings.Add;
        night.OnSpawnRequested += (_, __) => spawned++;

        Run(machine, 3.75f);
        Assert.AreEqual(0, warnings.Count, "Not yet: the warning is six seconds ahead, not ten.");

        Run(machine, 0.5f);
        Assert.AreEqual(1, warnings.Count, "Announced at four seconds, six before the group.");
        Assert.AreEqual(3, warnings[0].Count);
        Assert.AreEqual(0, spawned, "Announced before anything is on the road.");

        Run(machine, 10f);
        Assert.AreEqual(1, warnings.Count, "Once per group, not once per zombie.");
        Assert.AreEqual(3, spawned);
    }

    [Test]
    public void LabelNamesTheGroupTheWayThePlayerWould()
    {
        Assert.AreEqual("A zombie", new WaveGroup(0f, 0, 1, zombie).Label);
        Assert.AreEqual("3 zombies", new WaveGroup(0f, 0, 3, zombie).Label);
    }

    // --- Dawn --------------------------------------------------------------------------------

    [Test]
    public void ZombieNobodyCanReachDoesNotHoldTheNightOpen()
    {
        balance.dawnGraceSeconds = 20f;

        var plan = new WavePlan { DurationSeconds = 20f, LaneCount = 1 };
        plan.Groups.Add(new WaveGroup(0f, 0, 1, zombie));
        plan.Spawns.Add(new WaveSpawn(zombie, 0f, 0));

        var (machine, night) = Night(plan);

        // Standing at a door no tower can see: alive until something ends the night.
        var agent = MakeObject("Zombie").AddComponent<EnemyAgent>();
        night.OnSpawnRequested += (_, __) => night.Register(agent);

        Run(machine, 30f);
        Assert.AreEqual(DayPhase.Night, machine.CurrentPhase, "Inside the grace the guards still get their chance.");

        Run(machine, 11f);
        Assert.AreEqual(DayPhase.Resolution, machine.CurrentPhase, "Past the grace, dawn comes anyway.");
    }

    [Test]
    public void ClearNightEndsOnTime()
    {
        var plan = new WavePlan { DurationSeconds = 20f, LaneCount = 1 };
        var (machine, _) = Night(plan);

        Run(machine, 20.25f);

        Assert.AreEqual(DayPhase.Resolution, machine.CurrentPhase);
    }

    // --- How bad the night was -----------------------------------------------------------------

    [Test]
    public void OnlyZombiesAtTheDoorCount()
    {
        var danger = new NightDangerService(balance, null, null, () => hostiles);
        danger.SetShelterPoint(Vector3.zero);

        hostiles.Add(new FakeTarget { Position = new Vector3(2f, 0f, 0f) });
        hostiles.Add(new FakeTarget { Position = new Vector3(10f, 0f, 0f) });
        hostiles.Add(new FakeTarget { Position = new Vector3(1f, 0f, 1f), Alive = false });

        int reached = 0;
        danger.OnDoorReached += () => reached++;

        for (int i = 0; i < 12; i++) danger.Tick(0.25f);

        Assert.AreEqual(3f, danger.Pressure, 1e-4f, "One living zombie at the door for three seconds.");
        Assert.AreEqual(1, danger.ReachedDoorCount);
        Assert.AreEqual(1, reached, "Said once, the first time, not every frame.");

        danger.BeginNight();
        Assert.AreEqual(0f, danger.Pressure);
        Assert.AreEqual(0, danger.ReachedDoorCount);
    }

    [Test]
    public void InjuryChanceStartsAtTheThresholdAndClimbs()
    {
        balance.injuryPressureThreshold = 5f;
        balance.injuryChanceAtThreshold = 0.35f;
        balance.injuryChancePerPressureSecond = 0.03f;

        Assert.AreEqual(0f, NightDangerService.InjuryChance(0f, balance));
        Assert.AreEqual(0f, NightDangerService.InjuryChance(4.9f, balance), "Touched the door and was dealt with.");
        Assert.AreEqual(0.35f, NightDangerService.InjuryChance(5f, balance), 1e-4f);
        Assert.AreEqual(0.65f, NightDangerService.InjuryChance(15f, balance), 1e-4f);
        Assert.AreEqual(1f, NightDangerService.InjuryChance(200f, balance));
    }

    /// <summary>The danger service with a guard on watch in a built tower, a hospital, and
    /// <paramref name="pressure"/> zombie-seconds at the door already.</summary>
    (NightDangerService danger, HospitalService hospital, Survivor guard) Exposed(float pressure, bool guardOnWatch)
    {
        var survivors = new SurvivorService(balance, new ResourceLedger());
        var hospital = new HospitalService(null, survivors, () => false);

        var guardRole = Make<SurvivorRoleDefinition>();
        guardRole.role = SurvivorRole.Guard;

        var weapon = Make<WeaponDefinition>();
        var tower = Make<DefenseDefinition>();
        tower.requiresCrew = true;
        tower.crewRole = SurvivorRole.Guard;
        tower.levels = new[] { new DefenseLevel { weapon = weapon } };

        var defense = new DefenseService(survivors, new ResourceLedger(), null, new ProgressionService(),
                                         new DayCycleMachine(new TimeService(6f)), () => hostiles, () => false);
        defense.Register(tower, "gate", new Vector3(0f, 0f, -20f), startsBuilt: true);

        Survivor guard = null;
        if (guardOnWatch)
        {
            guard = new Survivor { Id = "guard", Name = "Mahmoud", Role = guardRole };
            survivors.Add(guard);
            defense.Tick(0.25f);
        }

        var danger = new NightDangerService(balance, hospital, defense, () => hostiles);
        danger.SetShelterPoint(Vector3.zero);

        hostiles.Clear();
        hostiles.Add(new FakeTarget { Position = Vector3.zero });

        int steps = Mathf.RoundToInt(pressure / 0.25f);
        for (int i = 0; i < steps; i++) danger.Tick(0.25f);

        return (danger, hospital, guard);
    }

    [Test]
    public void QuietNightHurtsNobody()
    {
        var (danger, hospital, _) = Exposed(pressure: 0f, guardOnWatch: true);

        NightInjury injury = danger.Resolve(day: 1);

        Assert.IsFalse(injury.Happened);
        Assert.AreEqual(0, hospital.OccupiedCount);
    }

    [Test]
    public void DifficultNightSendsSomeoneExposedToTheHospital()
    {
        var (danger, hospital, guard) = Exposed(pressure: 60f, guardOnWatch: true);

        var visitor = new FakeVisitor("visitor:1");
        danger.RegisterCivilian(visitor);

        NightInjury injury = danger.Resolve(day: 1);

        Assert.IsTrue(injury.Happened, "Sixty zombie-seconds at the door is a certain injury.");
        Assert.IsTrue(injury.Patient.PatientId == visitor.PatientId || injury.Patient.PatientId == "survivor:guard",
                      "Only the guard on watch and the visitor sheltering inside were exposed.");
        Assert.IsTrue(hospital.Contains(injury.Patient));
        Assert.AreEqual(1, hospital.OccupiedCount, "One injury a night, at most.");

        if (injury.Survivor != null)
        {
            Assert.AreSame(guard, injury.Survivor);
            Assert.IsTrue(guard.InHospital);
        }
        else
        {
            CollectionAssert.DoesNotContain(danger.Civilians, visitor, "A patient is no longer sheltering.");
        }
    }

    [Test]
    public void SameNightHurtsTheSamePerson()
    {
        string Victim()
        {
            var (danger, _, _) = Exposed(pressure: 60f, guardOnWatch: true);
            danger.RegisterCivilian(new FakeVisitor("visitor:1"));
            danger.RegisterCivilian(new FakeVisitor("visitor:2"));
            return danger.Resolve(day: 4).Patient.PatientId;
        }

        Assert.AreEqual(Victim(), Victim());
    }

    [Test]
    public void NobodyOutsideAndNobodyShelteringMeansNobodyHurt()
    {
        // The workers are inside and not at risk; with no guard posted and no customers left,
        // there is nobody for even a terrible night to hurt.
        var (danger, hospital, _) = Exposed(pressure: 60f, guardOnWatch: false);

        Assert.IsFalse(danger.Resolve(day: 1).Happened);
        Assert.AreEqual(0, hospital.OccupiedCount);
    }

    // --- The shelter -------------------------------------------------------------------------

    static readonly Bounds Restaurant = new Bounds(new Vector3(19.85f, 1.5f, 21.95f), new Vector3(11.7f, 3f, 10.5f));

    NightShelter Shelter()
    {
        GameObject root = MakeObject("Shelter");

        Transform Point(string name, Vector3 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root.transform, false);
            t.position = at;
            return t;
        }

        var shelter = root.AddComponent<NightShelter>();
        shelter.Configure(null, Point("Out", new Vector3(19.82f, 0f, 14.6f)), Point("In", new Vector3(19.82f, 0f, 18.1f)),
                          new[] { Point("A", new Vector3(17.8f, 0f, 20.2f)), Point("B", new Vector3(19.8f, 0f, 20.5f)) },
                          Restaurant);
        return shelter;
    }

    /// <summary>True when any leg before the door crosses the building.</summary>
    static bool CrossesTheWalls(Vector3 from, List<Vector3> route, Vector3 door)
    {
        Vector3 previous = from;

        foreach (Vector3 next in route)
        {
            if (next == door) return false;

            Vector3 a = new Vector3(previous.x, Restaurant.center.y, previous.z);
            Vector3 b = new Vector3(next.x, Restaurant.center.y, next.z);
            Vector3 delta = b - a;

            if (delta.sqrMagnitude > 1e-6f &&
                Restaurant.IntersectRay(new Ray(a, delta.normalized), out float hit) && hit <= delta.magnitude)
                return true;

            previous = next;
        }

        return false;
    }

    [Test]
    public void FromInFrontTheWalkInIsStraightThroughTheDoor()
    {
        NightShelter shelter = Shelter();

        List<Vector3> route = shelter.RouteIn(new Vector3(20f, 0f, 5f), new Vector3(17.8f, 0f, 20.2f));

        Assert.AreEqual(3, route.Count, "No corners: nothing is in the way.");
        Assert.AreEqual(shelter.DoorOutside, route[0]);
        Assert.AreEqual(shelter.DoorInside, route[1]);
    }

    [TestCase(33f, 22f)]  // beside the building, where the Chef waits by day
    [TestCase(20f, 32f)]  // behind it
    [TestCase(8f, 24f)]   // the other side
    public void FromAnywhereElseTheWalkGoesRoundTheBuilding(float x, float z)
    {
        NightShelter shelter = Shelter();
        var from = new Vector3(x, 0f, z);

        List<Vector3> route = shelter.RouteIn(from, new Vector3(17.8f, 0f, 20.2f));

        Assert.Greater(route.Count, 3, "At least one corner.");
        Assert.IsFalse(CrossesTheWalls(from, route, shelter.DoorOutside), "Never through a wall.");
    }

    [Test]
    public void WalkOutIsTheWalkInBackwards()
    {
        NightShelter shelter = Shelter();
        var home = new Vector3(33f, 0f, 22f);
        var spot = new Vector3(17.8f, 0f, 20.2f);

        List<Vector3> into = shelter.RouteIn(home, spot);
        List<Vector3> outOf = shelter.RouteOut(spot, home);

        Assert.AreEqual(shelter.DoorInside, outOf[0]);
        Assert.AreEqual(shelter.DoorOutside, outOf[1]);
        Assert.AreEqual(home, outOf[outOf.Count - 1]);
        Assert.AreEqual(into.Count, outOf.Count, "The same corners, the other way round.");
    }

    [Test]
    public void EveryoneGetsTheirOwnSpotUntilTheyRunOut()
    {
        NightShelter shelter = Shelter();
        object chef = new object(), helper = new object(), third = new object();

        Vector3 a = shelter.Claim(chef);
        Vector3 b = shelter.Claim(helper);

        Assert.AreNotEqual(a, b);
        Assert.AreEqual(a, shelter.Claim(chef), "Asking again is the same spot.");
        Assert.AreEqual(shelter.DoorInside, shelter.Claim(third), "Past the last spot, the doorway.");

        shelter.Release(chef);
        Assert.AreEqual(a, shelter.Claim(third), "A released spot is free again.");
    }
}
#endif
