#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using FoodStreet.Navigation;
using LastOrders.DayCycle;
using LastOrders.Data;
using LastOrders.Defense;
using LastOrders.Economy;
using LastOrders.Progression;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The rules of the strip outside the walls, without a scene: a barricade stops the night's zombies
/// and nobody else until it is battered down; a trap springs on everything standing on it at once,
/// then resets, and jams after so many strikes; wire holds back whatever wades into it, cuts it once
/// a second and is torn by it; any slot takes any kind, paid from the stores in daylight, and is
/// remembered; and a worker's repair puts each kind right.
/// </summary>
public class GroundDefenseTests
{
    readonly List<Object> owned = new List<Object>();
    readonly List<IDefenseTarget> hostiles = new List<IDefenseTarget>();

    ProgressionService progression;
    ResourceLedger ledger;
    DayCycleMachine machine;
    GroundDefenseService service;

    GroundDefenseDefinition barricade;
    GroundDefenseDefinition spikes;
    GroundDefenseDefinition wire;
    GroundDefenseDefinition[] kinds;

    [SetUp]
    public void SetUp()
    {
        hostiles.Clear();

        progression = new ProgressionService();
        ledger = new ResourceLedger();
        machine = new DayCycleMachine(new TimeService(6f));
        service = new GroundDefenseService(ledger, progression, machine, () => hostiles);

        barricade = Kind("barricade", GroundDefenseKind.Barricade, new Vector2(2.4f, 0.6f), 120f,
                         new ResourceAmount(ResourceType.Wood, 10), new ResourceAmount(ResourceType.Scrap, 4));

        spikes = Kind("spike_plate", GroundDefenseKind.Trap, new Vector2(1.5f, 1.2f), 3f, new ResourceAmount(ResourceType.Scrap, 12));
        spikes.strikeDamage = 8f;
        spikes.strikeSeconds = 1.2f;
        spikes.wearPerStrike = 1f;
        spikes.slowMultiplier = 0.25f;
        spikes.slowSeconds = 0.6f;

        wire = Kind("razor_wire", GroundDefenseKind.Snare, new Vector2(2.4f, 1f), 4f, new ResourceAmount(ResourceType.Scrap, 6));
        wire.slowMultiplier = 0.4f;
        wire.slowSeconds = 0.6f;
        wire.damagePerSecond = 2f;
        wire.wearPerSecond = 1f;

        kinds = new[] { barricade, spikes, wire };
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);

        owned.Clear();
    }

    // --- Helpers ---------------------------------------------------------------------------

    GroundDefenseDefinition Kind(string id, GroundDefenseKind kind, Vector2 footprint, float health, params ResourceAmount[] cost)
    {
        var asset = ScriptableObject.CreateInstance<GroundDefenseDefinition>();
        owned.Add(asset);
        asset.id = id;
        asset.displayName = id;
        asset.kind = kind;
        asset.footprint = footprint;
        asset.health = health;
        asset.buildCost = cost;
        return asset;
    }

    /// <summary>A slot at the origin facing +Z, built with <paramref name="kind"/> from the start.</summary>
    GroundDefenseUnit Slot(GroundDefenseDefinition kind, string id = "slot") =>
        service.Register(id, Vector3.zero, 0f, kinds, kind);

    FakeZombie Zombie(Vector3 at, float health = 30f)
    {
        var zombie = new FakeZombie { Position = at, Health = health };
        hostiles.Add(zombie);
        return zombie;
    }

    sealed class FakeZombie : IDefenseTarget, ISlowable
    {
        public Vector3 Position { get; set; }
        public float Health;
        public int Hits;
        public float SlowedTo = 1f;
        public bool IsAlive => Health > 0f;
        public string DisplayName => "Zombie";

        public void ApplyDamage(float amount)
        {
            Health -= amount;
            Hits++;
        }

        public void Slow(float multiplier, float seconds) => SlowedTo = Mathf.Min(SlowedTo, multiplier);
    }

    sealed class IdlePhase : IDayPhaseState
    {
        public IdlePhase(DayPhase phase) => Phase = phase;
        public DayPhase Phase { get; }
        public void OnEnter(DayPhase previous) { }
        public void OnTick(float deltaSeconds) { }
        public void OnExit(DayPhase next) { }
    }

    // --- Barricades -----------------------------------------------------------------------

    [Test]
    public void BarricadeStopsZombiesButNotWorkers()
    {
        GroundDefenseUnit unit = Slot(barricade);

        var from = new Vector3(0f, 0f, 1.5f);
        var into = new Vector3(0f, 0f, 0.4f);

        Assert.IsTrue(unit.Blocks(from, into, 0.35f, MoverKind.Hostile), "A zombie walking into it is stopped.");
        Assert.IsFalse(unit.Blocks(from, into, 0.35f, MoverKind.Friendly), "A worker steps over it.");
        Assert.IsFalse(unit.Blocks(from, new Vector3(3f, 0f, 1.5f), 0.35f, MoverKind.Hostile), "Walking past it is not blocked.");
    }

    [Test]
    public void BarricadeFallsToBlowsAndThenLetsThemThrough()
    {
        GroundDefenseUnit unit = Slot(barricade);
        bool worn = false;
        unit.OnWorn += () => worn = true;

        for (int i = 0; i < 19; i++) Assert.IsFalse(unit.TakeHit(6f), $"Blow {i + 1} of a Zombie's 6 should not bring it down yet.");
        Assert.IsTrue(unit.TakeHit(6f), "The twentieth blow brings 120 health down.");

        Assert.IsTrue(worn);
        Assert.IsTrue(unit.IsWorn);
        Assert.IsFalse(unit.Blocks(new Vector3(0f, 0f, 1.5f), new Vector3(0f, 0f, 0.4f), 0.35f, MoverKind.Hostile),
                       "Battered down, it is a gap.");
        Assert.IsFalse(unit.TakeHit(6f), "Nothing left to hit.");
    }

    [Test]
    public void BarricadeIgnoresTheTick()
    {
        GroundDefenseUnit unit = Slot(barricade);
        FakeZombie zombie = Zombie(Vector3.zero);

        service.Tick(1f);

        Assert.AreEqual(0, zombie.Hits, "A barricade only stands in the way.");
        Assert.AreEqual(120f, unit.Health);
    }

    // --- Traps ----------------------------------------------------------------------------------

    [Test]
    public void TrapSpringsOnEverythingOnItThenResets()
    {
        GroundDefenseUnit unit = Slot(spikes);
        FakeZombie a = Zombie(new Vector3(0.3f, 0f, 0f));
        FakeZombie b = Zombie(new Vector3(-0.3f, 0f, 0.2f));
        FakeZombie off = Zombie(new Vector3(0f, 0f, 3f));
        int strikes = 0;
        unit.OnStrike += () => strikes++;

        service.Tick(0.1f);

        Assert.AreEqual(1, strikes);
        Assert.AreEqual(22f, a.Health, "8 to everything on it.");
        Assert.AreEqual(22f, b.Health);
        Assert.AreEqual(30f, off.Health, "Nothing off the plate is touched.");
        Assert.AreEqual(0.25f, a.SlowedTo, "Pinned for a moment.");

        service.Tick(1f);
        Assert.AreEqual(1, strikes, "Still resetting 1.1 s later.");

        service.Tick(0.25f);
        Assert.AreEqual(2, strikes, "Springs again once 1.2 s have passed.");
        Assert.AreEqual(14f, a.Health);
    }

    [Test]
    public void TrapJamsAfterItsStrikesUntilRepaired()
    {
        GroundDefenseUnit unit = Slot(spikes);
        FakeZombie zombie = Zombie(Vector3.zero, 1000f);

        for (int i = 0; i < 3; i++) service.Tick(1.3f);

        Assert.AreEqual(3, unit.Strikes);
        Assert.IsTrue(unit.IsWorn, "Three strikes on a 3-strike plate jams it.");

        float before = zombie.Health;
        service.Tick(1.3f);
        Assert.AreEqual(before, zombie.Health, "A jammed trap does nothing.");

        unit.Repair();
        Assert.IsTrue(unit.IsWorking);

        service.Tick(0.1f);
        Assert.Less(zombie.Health, before, "Repaired, it springs again.");
    }

    [Test]
    public void TrapIgnoresTheDead()
    {
        Slot(spikes);
        FakeZombie dead = Zombie(Vector3.zero, 0f);

        service.Tick(0.1f);

        Assert.AreEqual(0, dead.Hits);
    }

    // --- Wire ----------------------------------------------------------------------------------

    [Test]
    public void WireHoldsBackAndCutsOnceASecond()
    {
        GroundDefenseUnit unit = Slot(wire);
        FakeZombie zombie = Zombie(new Vector3(0.8f, 0f, 0.2f));

        service.Tick(0.1f);
        Assert.AreEqual(0.4f, zombie.SlowedTo, "Held to 40% while it is in the wire.");
        Assert.AreEqual(1, zombie.Hits, "Cut as it wades in.");
        Assert.AreEqual(1, unit.Caught);

        for (int i = 0; i < 5; i++) service.Tick(0.1f);
        Assert.AreEqual(1, zombie.Hits, "Not cut again within the second.");

        for (int i = 0; i < 6; i++) service.Tick(0.1f);
        Assert.AreEqual(2, zombie.Hits, "Cut again a second later.");
        Assert.AreEqual(26f, zombie.Health, "2 a cut.");
    }

    [Test]
    public void WireIsTornByWhatPushesThroughIt()
    {
        GroundDefenseUnit unit = Slot(wire);
        Zombie(new Vector3(-0.5f, 0f, 0f), 1000f);
        Zombie(new Vector3(0.5f, 0f, 0f), 1000f);

        // Two zombies in it wear 2 a second: 4 health lasts two seconds.
        service.Tick(1f);
        Assert.IsTrue(unit.IsWorking);
        service.Tick(1.05f);
        Assert.IsTrue(unit.IsWorn, "Torn after 4 zombie-seconds.");

        var fresh = Zombie(new Vector3(0f, 0f, 0f));
        service.Tick(0.5f);
        Assert.AreEqual(1f, fresh.SlowedTo, "Torn wire holds nothing.");
    }

    // --- Building -------------------------------------------------------------------------------

    [Test]
    public void AnEmptySlotOffersEveryKind()
    {
        service.Register("slot", Vector3.zero, 0f, kinds, null);

        BuildChoiceOffer offer = service.GetBuildOffer("slot");

        Assert.AreEqual(3, offer.Options.Count);
        StringAssert.Contains("10 Wood + 4 Scrap", offer.Options[0].Label);
        Assert.IsFalse(offer.Options[0].Affordable, "Nothing in the stores.");
        Assert.IsNull(offer.Blocked, "Daylight, and nothing built here: it can be built.");
    }

    [Test]
    public void BuildingPaysFromTheStoresAndIsRemembered()
    {
        GroundDefenseUnit unit = service.Register("slot", Vector3.zero, 0f, kinds, null);
        ledger.Add(ResourceType.Scrap, 15);

        Assert.IsFalse(service.TryBuild("slot", 0, out string refused), "Short of Wood for a barricade.");
        StringAssert.Contains("Wood", refused);

        Assert.IsTrue(service.TryBuild("slot", 1, out _), "A spike plate is 12 Scrap.");
        Assert.AreEqual(3, ledger.Get(ResourceType.Scrap));
        Assert.AreSame(spikes, unit.Definition);

        Assert.IsFalse(service.TryBuild("slot", 2, out string taken), "One thing to a slot.");
        StringAssert.Contains("already", taken);

        // The slot registers again - a scene reload - and comes back with what was bought for it.
        service.Unregister("slot");
        GroundDefenseUnit again = service.Register("slot", Vector3.zero, 0f, kinds, null);
        Assert.AreSame(spikes, again.Definition);
    }

    [Test]
    public void NothingIsBuiltAfterDark()
    {
        service.Register("slot", Vector3.zero, 0f, kinds, null);
        ledger.Add(ResourceType.Scrap, 50);
        machine.AddState(new IdlePhase(DayPhase.Night));
        machine.ForceTo(DayPhase.Night);

        Assert.IsFalse(service.TryBuild("slot", 1, out string message));
        StringAssert.Contains("dark", message);
    }

    [Test]
    public void WhatASlotStartsWithIsFree()
    {
        GroundDefenseUnit unit = Slot(barricade);

        Assert.IsTrue(unit.IsBuilt);
        Assert.AreEqual(0, ledger.Get(ResourceType.Wood));
        Assert.AreEqual(1, service.BuiltCount);
    }
}
#endif
