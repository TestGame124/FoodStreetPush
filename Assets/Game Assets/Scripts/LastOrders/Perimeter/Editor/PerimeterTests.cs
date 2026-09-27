#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using FoodStreet.Navigation;
using LastOrders.DayCycle;
using LastOrders.Defense;
using LastOrders.Perimeter;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The compound wall's rules, without a scene: a wall refuses a step into it but never one out of
/// it; a gate opens only for a route through it, stays shut with a zombie at it and does not slam
/// on someone half through; and a zombie standing inside the walls breaches the compound until
/// it has been clear for a moment, with everything knocked down rebuilt by dawn.
/// </summary>
public class PerimeterTests
{
    static readonly Vector2 WallHalf = new Vector2(2f, 0.25f);

    // --- A wall's footprint ------------------------------------------------------------------

    [Test]
    public void StepIntoAWallIsRefused()
    {
        var from = new Vector3(0f, 0f, -1f);
        var to = new Vector3(0f, 0f, -0.5f);

        Assert.IsTrue(MovementBarriers.StepEnters(from, to, Vector3.zero, 0f, WallHalf, 0.35f));
    }

    [Test]
    public void StepAwayFromAWallIsAllowed()
    {
        var from = new Vector3(0f, 0f, -0.5f);
        var to = new Vector3(0f, 0f, -0.7f);

        Assert.IsFalse(MovementBarriers.StepEnters(from, to, Vector3.zero, 0f, WallHalf, 0.35f),
                       "Something already pressed against a wall can always back off it.");
    }

    [Test]
    public void SlidingAlongAWallIsAllowed()
    {
        var from = new Vector3(0f, 0f, -0.55f);
        var to = new Vector3(0.1f, 0f, -0.55f);

        Assert.IsFalse(MovementBarriers.StepEnters(from, to, Vector3.zero, 0f, WallHalf, 0.35f));
    }

    [Test]
    public void TurnedWallsAreMeasuredAlongTheirOwnLength()
    {
        // Turned to run along Z, so a point 1.5 m along Z is on it and 1.5 m along X is not.
        Assert.Greater(MovementBarriers.Penetration(new Vector3(0f, 0f, 1.5f), Vector3.zero, -90f, WallHalf, 0f), 0f);
        Assert.Less(MovementBarriers.Penetration(new Vector3(1.5f, 0f, 0f), Vector3.zero, -90f, WallHalf, 0f), 0f);
    }

    // --- Who a gate is for ---------------------------------------------------------------------

    [Test]
    public void ARouteThroughTheOpeningCrossesIt()
    {
        Assert.IsTrue(MovementBarriers.CrossesOpening(new Vector3(0f, 0f, -3f), new Vector3(0f, 0f, 3f),
                                                      Vector3.zero, 0f, 5f, 12f));
    }

    [Test]
    public void ARoutePastThePostsDoesNotCrossIt()
    {
        Assert.IsFalse(MovementBarriers.CrossesOpening(new Vector3(7f, 0f, -3f), new Vector3(7f, 0f, 3f),
                                                       Vector3.zero, 0f, 5f, 12f));
    }

    [Test]
    public void ARouteAlongTheRoadDoesNotCrossIt()
    {
        Assert.IsFalse(MovementBarriers.CrossesOpening(new Vector3(-20f, 0f, -4f), new Vector3(20f, 0f, -4f),
                                                       Vector3.zero, 0f, 5f, 12f));
    }

    [Test]
    public void ACrossingTooFarAheadDoesNotCountYet()
    {
        Assert.IsFalse(MovementBarriers.CrossesOpening(new Vector3(0f, 0f, -30f), new Vector3(0f, 0f, 3f),
                                                       Vector3.zero, 0f, 5f, 12f));
    }

    // --- When a gate opens ---------------------------------------------------------------------

    static void Run(GateMechanism gate, float seconds, bool request, bool hostileNear)
    {
        const float step = 1f / 60f;
        for (float t = 0f; t < seconds; t += step)
        {
            if (request) gate.Request();
            gate.Tick(step, hostileNear);
        }
    }

    [Test]
    public void GateOpensForSomeoneComingThrough()
    {
        var gate = new GateMechanism();

        Run(gate, 1f, request: true, hostileNear: false);

        Assert.IsTrue(gate.LetsFriendliesThrough);
        Assert.IsFalse(gate.StopsHostiles);
    }

    [Test]
    public void GateStaysShutWhenNobodyAsks()
    {
        var gate = new GateMechanism();

        Run(gate, 3f, request: false, hostileNear: false);

        Assert.AreEqual(0f, gate.OpenAmount);
        Assert.IsTrue(gate.StopsHostiles);
    }

    [Test]
    public void GateShutsAgainAfterTheLastOneIsThrough()
    {
        var gate = new GateMechanism();
        Run(gate, 1f, request: true, hostileNear: false);

        Run(gate, gate.HoldOpenSeconds * 0.5f, request: false, hostileNear: false);
        Assert.IsTrue(gate.LetsFriendliesThrough, "It holds open a moment after the last request.");

        Run(gate, gate.HoldOpenSeconds + gate.CloseSeconds + 0.1f, request: false, hostileNear: false);
        Assert.AreEqual(0f, gate.OpenAmount);
    }

    [Test]
    public void GateWillNotOpenWithAZombieAtIt()
    {
        var gate = new GateMechanism();

        Run(gate, 2f, request: true, hostileNear: true);

        Assert.AreEqual(0f, gate.OpenAmount, "Whoever wants through waits.");
        Assert.IsTrue(gate.IsLocked);

        Run(gate, 1f, request: true, hostileNear: false);

        Assert.IsTrue(gate.LetsFriendliesThrough, "And it opens for them the moment the zombie is gone.");
        Assert.IsFalse(gate.IsLocked);
    }

    [Test]
    public void GateDoesNotSlamOnSomeoneHalfThrough()
    {
        var gate = new GateMechanism();
        Run(gate, 0.3f, request: true, hostileNear: false);

        Run(gate, 1f, request: true, hostileNear: true);

        Assert.IsTrue(gate.LetsFriendliesThrough);
    }

    [Test]
    public void GateShutsAtOnceWhenAZombieIsNear()
    {
        var gate = new GateMechanism();
        Run(gate, 1f, request: true, hostileNear: false);

        Run(gate, gate.HoldOpenThreatenedSeconds + gate.CloseSeconds + 0.1f, request: false, hostileNear: true);

        Assert.AreEqual(0f, gate.OpenAmount, "No lingering open with something dangerous outside.");
    }

    // --- Getting in ----------------------------------------------------------------------------

    sealed class FakeZombie : IDefenseTarget
    {
        public Vector3 Position { get; set; }
        public bool Alive = true;
        public bool IsAlive => Alive;
        public string DisplayName => "Zombie";
        public void ApplyDamage(float amount) { }
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

    readonly List<IDefenseTarget> zombies = new List<IDefenseTarget>();

    CompoundPerimeterService Compound(DayCycleMachine machine = null)
    {
        zombies.Clear();

        var compound = new CompoundPerimeterService(() => zombies, machine);
        compound.RegisterZone("inside", new Rect(0f, 0f, 30f, 30f));
        return compound;
    }

    [Test]
    public void AZombieInsideTheWallsBreachesTheCompound()
    {
        CompoundPerimeterService compound = Compound();
        int breaches = 0;
        compound.OnBreached += () => breaches++;

        var zombie = new FakeZombie { Position = new Vector3(15f, 0f, -3f) };
        zombies.Add(zombie);

        compound.Tick(0.1f);
        Assert.IsFalse(compound.IsBreached, "Outside the wall, battering it, is not in.");

        zombie.Position = new Vector3(15f, 0f, 2f);
        compound.Tick(0.1f);
        compound.Tick(0.1f);

        Assert.IsTrue(compound.IsBreached);
        Assert.AreEqual(1, breaches, "One breach, however many frames it lasts.");
    }

    [Test]
    public void ADeadZombieInsideIsNoBreach()
    {
        CompoundPerimeterService compound = Compound();
        zombies.Add(new FakeZombie { Position = new Vector3(5f, 0f, 5f), Alive = false });

        compound.Tick(0.1f);

        Assert.IsFalse(compound.IsBreached);
    }

    [Test]
    public void TheCompoundIsClearOnlyAfterAMomentWithNothingInside()
    {
        CompoundPerimeterService compound = Compound();
        var zombie = new FakeZombie { Position = new Vector3(5f, 0f, 5f) };
        zombies.Add(zombie);
        compound.Tick(0.1f);

        int cleared = 0;
        compound.OnCleared += () => cleared++;

        zombie.Alive = false;
        compound.Tick(compound.ClearDelaySeconds * 0.5f);
        Assert.IsTrue(compound.IsBreached, "A zombie stepping back over the line does not send everyone out.");

        compound.Tick(compound.ClearDelaySeconds);
        Assert.IsFalse(compound.IsBreached);
        Assert.AreEqual(1, cleared);
    }

    [Test]
    public void WithNoInteriorNothingCanBreach()
    {
        zombies.Clear();
        var compound = new CompoundPerimeterService(() => zombies, null);
        zombies.Add(new FakeZombie { Position = new Vector3(5f, 0f, 5f) });

        compound.Tick(0.1f);

        Assert.IsFalse(compound.IsBreached);
    }

    [Test]
    public void AGateIsLockedOnlyByAZombieNearIt()
    {
        CompoundPerimeterService compound = Compound();
        zombies.Add(new FakeZombie { Position = new Vector3(20f, 0f, -8f) });

        Assert.IsFalse(compound.HostileWithin(new Vector3(20f, 0f, -2.6f), 4f));
        Assert.IsTrue(compound.HostileWithin(new Vector3(20f, 0f, -2.6f), 6f));
    }

    [Test]
    public void EverythingKnockedDownIsRebuiltByDawn()
    {
        var machine = new DayCycleMachine(new TimeService(6f));
        machine.AddState(new EmptyState(DayPhase.Night));
        machine.AddState(new EmptyState(DayPhase.Dawn));

        CompoundPerimeterService compound = Compound(machine);

        var fallen = new FakeWall { Health = 0f };
        var hit = new FakeWall { Health = 120f };
        var untouched = new FakeWall();
        compound.Register(fallen);
        compound.Register(hit);
        compound.Register(untouched);

        int repaired = -1;
        compound.OnRepaired += count => repaired = count;

        machine.Start(DayPhase.Night);
        machine.ForceTo(DayPhase.Dawn);

        Assert.IsFalse(fallen.IsDestroyed);
        Assert.AreEqual(350f, hit.Health);
        Assert.AreEqual(2, repaired, "Only what needed it is counted.");
    }

    [Test]
    public void TheFirstBlowOfTheNightIsReportedOnce()
    {
        CompoundPerimeterService compound = Compound();
        var wall = new FakeWall();

        int reports = 0;
        compound.OnUnderAttack += _ => reports++;

        compound.ReportHit(wall);
        compound.ReportHit(wall);

        Assert.AreEqual(1, reports);
    }
}
#endif
