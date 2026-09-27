#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Defense;
using LastOrders.Economy;
using LastOrders.Medical;
using LastOrders.Progression;
using LastOrders.Survivors;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The hospital tent, without a scene: two cots; the injured take the first free one and
/// recover on their own; a third waits for a cot to free; nothing heals while the clock is
/// stopped or before a visitor is lying down; a medic doubles the pace; and a guard in a cot is
/// off the tower until they are well, then back on watch without being told.
/// </summary>
public class HospitalTests
{
    readonly List<Object> owned = new List<Object>();

    SurvivorService survivors;
    HospitalService hospital;
    HospitalDefinition tent;
    bool paused;

    SurvivorRoleDefinition guardRole;
    SurvivorRoleDefinition medicRole;

    int nextSurvivor;

    [SetUp]
    public void SetUp()
    {
        paused = false;
        nextSurvivor = 0;

        var balance = Make<GameBalanceConfig>();
        survivors = new SurvivorService(balance, new ResourceLedger());

        tent = Make<HospitalDefinition>();
        tent.cots = 2;
        tent.visitorRecoverySeconds = 90f;
        tent.survivorRecoverySeconds = 150f;
        tent.medicRole = SurvivorRole.Medic;

        hospital = new HospitalService(tent, survivors, () => paused);

        guardRole = Role(SurvivorRole.Guard);
        medicRole = Role(SurvivorRole.Medic);
        medicRole.injuryRecoveryMultiplier = 0.5f;
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

    Survivor Recruit(SurvivorRoleDefinition role)
    {
        var survivor = new Survivor { Id = $"s{nextSurvivor}", Name = $"Survivor {nextSurvivor}", Role = role };
        nextSurvivor++;
        survivors.Add(survivor);
        return survivor;
    }

    /// <summary>Quarter-second steps: exact in binary, so 600 of them are exactly 150 seconds.</summary>
    void Run(float seconds, DefenseService defense = null)
    {
        int steps = Mathf.RoundToInt(seconds / 0.25f);
        for (int i = 0; i < steps; i++)
        {
            hospital.Tick(0.25f);
            defense?.Tick(0.25f);
        }
    }

    sealed class FakeVisitor : IPatient
    {
        public FakeVisitor(string id) => PatientId = id;
        public string PatientId { get; }
        public string DisplayName => "Trucker";
        public PatientKind Kind => PatientKind.Visitor;
    }

    // --- Admission ---------------------------------------------------------------------------

    [Test]
    public void InjuredGuardTakesACotAndIsOffDuty()
    {
        Survivor guard = Recruit(guardRole);

        Assert.IsTrue(hospital.AdmitSurvivor(guard));

        HospitalCot cot = hospital.Cots[0];
        Assert.AreEqual("survivor:s0", cot.Patient.PatientId);
        Assert.IsTrue(cot.InBed, "A survivor has no body to walk over, so they are in bed at once.");
        Assert.IsTrue(guard.IsInjured);
        Assert.IsFalse(guard.IsAvailable);
    }

    [Test]
    public void AdmittingTheSamePersonTwiceIsOneStay()
    {
        Survivor guard = Recruit(guardRole);

        hospital.AdmitSurvivor(guard);
        Assert.IsFalse(hospital.AdmitSurvivor(guard));

        Assert.AreEqual(1, hospital.OccupiedCount);
        Assert.AreEqual(0, hospital.Waiting.Count);
    }

    [Test]
    public void ThirdPatientWaitsForTheFirstCotToFree()
    {
        Survivor first = Recruit(guardRole);
        Survivor second = Recruit(guardRole);
        var visitor = new FakeVisitor("visitor:1");

        var queued = new List<IPatient>();
        hospital.OnQueued += queued.Add;

        hospital.AdmitSurvivor(first);
        hospital.AdmitSurvivor(second);
        hospital.Admit(visitor);

        Assert.AreEqual(2, hospital.OccupiedCount);
        Assert.AreEqual(1, hospital.Waiting.Count);
        CollectionAssert.AreEqual(new[] { visitor }, queued);
        Assert.IsTrue(hospital.Contains(visitor));
        Assert.IsNull(hospital.CotOf(visitor));

        Run(150f);

        Assert.AreSame(visitor, hospital.Cots[0].Patient, "The first cot to free goes to whoever waited.");
        Assert.AreEqual(0, hospital.Waiting.Count);
    }

    // --- Recovery ----------------------------------------------------------------------------

    [Test]
    public void GuardIsBackAtWorkOnceTreated()
    {
        Survivor guard = Recruit(guardRole);
        var discharged = new List<IPatient>();
        hospital.OnDischarged += discharged.Add;

        hospital.AdmitSurvivor(guard);

        Run(149.75f);
        Assert.IsTrue(guard.InHospital, "A quarter-second short of the stay is still in the cot.");

        Run(0.25f);

        Assert.IsFalse(guard.InHospital);
        Assert.IsTrue(guard.IsAvailable);
        Assert.IsTrue(hospital.Cots[0].IsFree);
        Assert.AreEqual(1, discharged.Count);
    }

    [Test]
    public void NobodyRecoversWhileTheClockIsStopped()
    {
        Survivor guard = Recruit(guardRole);
        hospital.AdmitSurvivor(guard);

        paused = true;
        Run(500f);

        Assert.AreEqual(0f, hospital.Cots[0].Progress);
        Assert.IsTrue(guard.InHospital);
    }

    [Test]
    public void VisitorIsNotTreatedUntilTheyAreLyingDown()
    {
        var visitor = new FakeVisitor("visitor:1");
        hospital.Admit(visitor);

        Assert.IsFalse(hospital.Cots[0].InBed);

        Run(200f);
        Assert.AreEqual(0f, hospital.Cots[0].Progress, "The walk over is not rest.");

        hospital.MarkInBed(visitor);
        Run(90f);

        Assert.IsFalse(hospital.Contains(visitor));
    }

    // --- The medic ---------------------------------------------------------------------------

    [Test]
    public void WithNoMedicRecoveryRunsAtTheNormalPace()
    {
        Recruit(guardRole);
        hospital.Tick(0f);

        Assert.IsNull(hospital.Medic);
        Assert.AreEqual(1f, hospital.TreatmentSpeed);
        StringAssert.Contains("No medic", hospital.Describe());
    }

    [Test]
    public void MedicOnTheRosterMakesRecoveryTwiceAsFast()
    {
        Survivor guard = Recruit(guardRole);
        Survivor medic = Recruit(medicRole);

        hospital.AdmitSurvivor(guard);
        hospital.Tick(0f);

        Assert.AreSame(medic, hospital.Medic);
        Assert.AreEqual(2f, hospital.TreatmentSpeed);

        Run(75f);

        Assert.IsFalse(guard.InHospital, "A 150-second stay at twice the pace is 75 seconds.");
    }

    [Test]
    public void InjuredMedicLooksAfterNobody()
    {
        Survivor medic = Recruit(medicRole);

        hospital.AdmitSurvivor(medic);
        hospital.Tick(0f);

        Assert.IsNull(hospital.Medic);
        Assert.AreEqual(1f, hospital.TreatmentSpeed);
    }

    [Test]
    public void OnlyAMedicCanBeAssigned()
    {
        Survivor guard = Recruit(guardRole);

        Assert.IsFalse(hospital.AssignMedic(guard, out string reason));
        StringAssert.Contains("Only a Medic", reason);
    }

    [Test]
    public void AssignedMedicIsTheOneOnDuty()
    {
        Recruit(medicRole);
        Survivor chosen = Recruit(medicRole);

        Assert.IsTrue(hospital.AssignMedic(chosen, out _));
        hospital.Tick(0f);

        Assert.AreSame(chosen, hospital.Medic);
    }

    // --- The guard tower ---------------------------------------------------------------------

    [Test]
    public void GuardInACotLeavesTheTowerAndIsBackOnWatchWhenWell()
    {
        var weapon = Make<WeaponDefinition>();
        weapon.damagePerShot = 16f;
        weapon.secondsBetweenShots = 1.5f;
        weapon.rangeMetres = 6f;

        var tower = Make<DefenseDefinition>();
        tower.requiresCrew = true;
        tower.crewRole = SurvivorRole.Guard;
        tower.levels = new[] { new DefenseLevel { weapon = weapon, spotRangeMetres = 14f } };

        var defense = new DefenseService(survivors, new ResourceLedger(), null, new ProgressionService(),
                                         new DayCycleMachine(new TimeService(6f)),
                                         () => new List<IDefenseTarget>(), () => false);

        DefenseUnit unit = defense.Register(tower, "gate", Vector3.zero, startsBuilt: true);
        Survivor guard = Recruit(guardRole);

        defense.Tick(0.25f);
        Assert.AreSame(guard, unit.Crew);

        hospital.AdmitSurvivor(guard);
        defense.Tick(0.25f);

        Assert.IsNull(unit.Crew, "A guard in a cot is not in the tower.");
        Assert.IsFalse(unit.CanFire);

        Run(150f, defense);

        Assert.AreSame(guard, unit.Crew, "Well again, and back on watch without being told.");
        Assert.IsTrue(unit.CanFire);
    }
}
#endif
