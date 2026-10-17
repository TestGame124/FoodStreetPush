#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using LastOrders.Consequence;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Economy;
using LastOrders.Night;
using LastOrders.Reputation;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The horde's brain, without a scene: who in a wave hunts the defenses (the toughest first, never
/// all of them), when a zombie goes for one (a hunter in sight of it, anyone else shot at from close
/// by, nobody nearly at the door, a gun on the wall only from outside), which one (nearest, the gun
/// shooting at it, soft and battered ones first, spreading out), and how many (a few per defense, a
/// share of the horde, a cap on knock-outs before everyone turns on the restaurant).
/// </summary>
public class ZombieDirectorTests
{
    readonly List<Object> owned = new List<Object>();

    EncounterTuning tuning;
    EnemyDefinition shambler;
    EnemyDefinition brute;
    EnemyDefinition screecher;

    int live;
    OutdoorDamageService yard;

    // The compound: inside is z between 0 and 30. The restaurant's front is at z 20.
    static bool Inside(Vector3 at) => at.z > 0f && at.z < 30f;

    static float ToRestaurant(Vector3 at) => Mathf.Max(0f, 20f - at.z) + Mathf.Max(0f, at.z - 28f);

    [SetUp]
    public void SetUp()
    {
        tuning = new EncounterTuning();
        live = 10;

        shambler = Enemy("Zombie", health: 30f, dps: 4f);
        brute = Enemy("Brute", health: 120f, dps: 12f);
        screecher = Enemy("Screecher", health: 30f, dps: 0f);
        screecher.isNonCombatant = true;

        var machine = new DayCycleMachine(new TimeService(6f));
        yard = new OutdoorDamageService(tuning, new ResourceLedger(), null, machine, Inside, null)
        {
            DefensesTargetedElsewhere = true,
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

    EnemyDefinition Enemy(string name, float health, float dps)
    {
        var enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
        owned.Add(enemy);
        enemy.displayName = name;
        enemy.maxHealth = health;
        enemy.structureDps = dps;
        return enemy;
    }

    /// <summary>A defense as the horde sees it, with no scene behind it.</summary>
    sealed class FakeDefense : IDefenseObjective
    {
        public string DisplayName { get; set; } = "Corn Shooter";
        public Vector3 Position { get; set; }
        public object AimingAt { get; set; }
        public bool OnTheWall { get; set; }
        public Vector3 Outward { get; set; }
        public float Health { get; set; } = 100f;
        public float MaxHealth { get; set; } = 100f;
        public bool IsStanding => Health > 0f;
        public float HealthFraction => Health / MaxHealth;
        public bool WorthBreakingThroughFor => true;

        public Vector3 ClosestPoint(Vector3 from) => Position;

        public float FlatDistance(Vector3 from) => new Vector2(Position.x - from.x, Position.z - from.z).magnitude;

        public bool TakeHit(float damage, Vector3 from)
        {
            if (Health <= 0f) return true;
            Health = Mathf.Max(0f, Health - damage);
            return Health <= 0f;
        }
    }

    ZombieDirector Director(params FakeDefense[] defenses)
    {
        var list = new List<IDefenseObjective>(defenses);
        return new ZombieDirector(() => tuning, yard, Inside, ToRestaurant, () => live)
        {
            Scanner = () => list,
        };
    }

    static EncounterWave Wave(params EnemyDefinition[] kinds)
    {
        var wave = new EncounterWave();
        for (int i = 0; i < kinds.Length; i++) wave.Spawns.Add(new WaveSpawn(kinds[i], i, 0));
        return wave;
    }

    /// <summary>A zombie made a hunter: a wave with a place for one brute, and this one is it.</summary>
    object Hunter(ZombieDirector director)
    {
        director.PlanWave(Wave(brute, shambler, shambler));
        var zombie = new object();
        director.Enlist(zombie, brute);
        Assert.IsTrue(director.IsHunter(zombie));
        return zombie;
    }

    // --- Who -----------------------------------------------------------------------------------

    [Test]
    public void AShareOfEachWaveHuntsAndOneAlwaysGoesForTheRestaurant()
    {
        ZombieTargetingTuning rules = tuning.Targeting;

        Assert.AreEqual(0, rules.HuntersIn(1, bigWave: true), "A lone zombie goes for the restaurant.");
        Assert.AreEqual(1, rules.HuntersIn(2, bigWave: false), "A pair splits: one hunts, one for the restaurant.");
        Assert.AreEqual(2, rules.HuntersIn(5, bigWave: true));
        Assert.AreEqual(4, rules.HuntersIn(10, bigWave: true));
        Assert.AreEqual(3, rules.HuntersIn(10, bigWave: false), "A nightly pack sends fewer.");

        rules.hunterShareBigWave = 1f;
        Assert.AreEqual(4, rules.HuntersIn(5, bigWave: true), "Never all of them.");
    }

    [Test]
    public void TheToughestAreMadeHuntersFirst()
    {
        ZombieDirector director = Director(new FakeDefense { Position = new Vector3(0f, 0f, -5f), OnTheWall = true });
        director.BeginAttack(bigWave: true);

        // Five fighters and a screecher: two hunters, the brute and one shambler.
        director.PlanWave(Wave(shambler, shambler, brute, screecher, shambler, shambler));

        object big = new object(), first = new object(), second = new object(), third = new object(), quiet = new object();
        director.Enlist(first, shambler);
        director.Enlist(big, brute);
        director.Enlist(second, shambler);
        director.Enlist(quiet, screecher);
        director.Enlist(third, shambler);

        Assert.IsTrue(director.IsHunter(big), "The brute hunts.");
        Assert.IsTrue(director.IsHunter(first), "So does the first shambler out.");
        Assert.IsFalse(director.IsHunter(second));
        Assert.IsFalse(director.IsHunter(third));
        Assert.IsFalse(director.IsHunter(quiet), "A screecher never fights.");
    }

    [Test]
    public void WithNoDefenseStandingNobodyHunts()
    {
        ZombieDirector director = Director();
        director.BeginAttack(bigWave: true);
        director.PlanWave(Wave(brute, shambler, shambler, shambler));

        var zombie = new object();
        director.Enlist(zombie, brute);

        Assert.IsFalse(director.IsHunter(zombie));
    }

    // --- When ----------------------------------------------------------------------------------

    [Test]
    public void AHunterGoesForADefenseOnlyOnceItIsInSight()
    {
        var gun = new FakeDefense { Position = new Vector3(0f, 0f, 0f), OnTheWall = true };
        ZombieDirector director = Director(gun);
        director.BeginAttack(bigWave: true);
        object hunter = Hunter(director);

        Assert.IsFalse(director.TryAssign(hunter, new Vector3(0f, 0f, -45f), out _), "Too far to see it.");

        Assert.IsTrue(director.TryAssign(hunter, new Vector3(0f, 0f, -20f), out IRaidTarget target));
        Assert.AreSame(gun, target);
    }

    [Test]
    public void AnyOtherZombieOnlyTurnsOnTheGunShootingAtItFromCloseBy()
    {
        var gun = new FakeDefense { Position = new Vector3(0f, 0f, 0f), OnTheWall = true };
        ZombieDirector director = Director(gun);
        director.BeginAttack(bigWave: true);

        var walker = new object();
        director.Enlist(walker, shambler);
        Assert.IsFalse(director.IsHunter(walker));

        Assert.IsFalse(director.TryAssign(walker, new Vector3(0f, 0f, -5f), out _), "Not shot at: it walks on.");

        gun.AimingAt = walker;
        Assert.IsFalse(director.TryAssign(walker, new Vector3(0f, 0f, -15f), out _), "Shot at, but from far off.");
        Assert.IsTrue(director.TryAssign(walker, new Vector3(0f, 0f, -5f), out IRaidTarget target), "Provoked.");
        Assert.AreSame(gun, target);
    }

    [Test]
    public void NearlyAtTheDoorAZombieIgnoresTheDefenses()
    {
        var turret = new FakeDefense { Position = new Vector3(3f, 0f, 16f) };
        ZombieDirector director = Director(turret);
        director.BeginAttack(bigWave: true);
        object hunter = Hunter(director);

        Assert.IsFalse(director.TryAssign(hunter, new Vector3(0f, 0f, 15f), out _), "Five metres from the restaurant.");
        Assert.IsTrue(director.TryAssign(hunter, new Vector3(0f, 0f, 5f), out _));
    }

    [Test]
    public void AGunOnTheWallIsOnlyGotAtFromOutsideAndAGroundDefenseFromItsOwnSide()
    {
        var wallGun = new FakeDefense { Position = new Vector3(0f, 0f, 0f), OnTheWall = true };
        var ringPad = new FakeDefense { Position = new Vector3(20f, 0f, -6f) };
        var yardTurret = new FakeDefense { Position = new Vector3(-20f, 0f, 6f) };

        ZombieDirector director = Director(wallGun, ringPad, yardTurret);
        director.BeginAttack(bigWave: true);
        object hunter = Hunter(director);

        Assert.IsTrue(director.TryAssign(hunter, new Vector3(-20f, 0f, 3f), out IRaidTarget inside));
        Assert.AreSame(yardTurret, inside, "Over the wall: only what is inside it, the wall gun is behind it.");

        Assert.IsTrue(director.TryAssign(hunter, new Vector3(-2f, 0f, -3f), out IRaidTarget outside));
        Assert.AreSame(wallGun, outside, "Outside: the wall gun, nearer than the pad.");

        Assert.IsTrue(director.TryAssign(hunter, new Vector3(18f, 0f, -8f), out IRaidTarget pad));
        Assert.AreSame(ringPad, pad);
        Assert.IsFalse(director.StillWanted(hunter, pad, new Vector3(18f, 0f, 2f)), "Got inside: the pad is out of reach now.");
    }

    [Test]
    public void AGunOnTheWallIsOnlyGoneForFromInFrontOfItsStretchOfWall()
    {
        // A gun on the south wall, facing south, and one on an east-facing stretch at x 20.
        var southGun = new FakeDefense { Position = new Vector3(0f, 0f, 0f), OnTheWall = true, Outward = Vector3.back };
        var eastGun = new FakeDefense { Position = new Vector3(20f, 0f, 0f), OnTheWall = true, Outward = Vector3.right };

        ZombieDirector director = Director(southGun, eastGun);
        director.BeginAttack(bigWave: true);
        object hunter = Hunter(director);

        // South of the wall and west of the east stretch: nearer the east gun, but round its corner.
        Vector3 from = new Vector3(12f, 0f, -4f);
        Assert.IsTrue(director.TryAssign(hunter, from, out IRaidTarget target));
        Assert.AreSame(southGun, target, "The east gun faces the other way: the wall beside it is in the way.");

        Assert.IsTrue(director.TryAssign(hunter, new Vector3(26f, 0f, -2f), out IRaidTarget inFront));
        Assert.AreSame(eastGun, inFront);
        Assert.IsFalse(director.StillWanted(hunter, inFront, new Vector3(15f, 0f, -2f)), "Walked round behind it: let it go.");
    }

    // --- Which ---------------------------------------------------------------------------------

    [Test]
    public void APackSpreadsOverTheDefensesRatherThanQueueingAtOne()
    {
        var near = new FakeDefense { Position = new Vector3(0f, 0f, 0f), OnTheWall = true };
        var far = new FakeDefense { Position = new Vector3(3f, 0f, 0f), OnTheWall = true };
        ZombieDirector director = Director(near, far);
        director.BeginAttack(bigWave: true);

        director.PlanWave(Wave(brute, brute, brute, shambler, shambler));
        object a = new object(), b = new object();
        director.Enlist(a, brute);
        director.Enlist(b, brute);

        Vector3 from = new Vector3(0f, 0f, -10f);
        Assert.IsTrue(director.TryAssign(a, from, out IRaidTarget first));
        Assert.IsTrue(director.TryAssign(b, from, out IRaidTarget second));

        Assert.AreSame(near, first);
        Assert.AreSame(far, second, "Ten metres to one already taken counts as fourteen: the other is nearer.");
    }

    [Test]
    public void TheGunShootingAtItAndASoftOrBatteredDefenseArePreferred()
    {
        var wallGun = new FakeDefense { Position = new Vector3(0f, 0f, 0f), OnTheWall = true };
        var padGun = new FakeDefense { Position = new Vector3(0f, 0f, -14f) };
        ZombieDirector director = Director(wallGun, padGun);
        director.BeginAttack(bigWave: true);
        object hunter = Hunter(director);

        Vector3 from = new Vector3(0f, 0f, -7f);

        // Seven metres each way: the one on the ground is the soft target.
        Assert.IsTrue(director.TryAssign(hunter, from, out IRaidTarget soft));
        Assert.AreSame(padGun, soft);

        // The wall gun shooting at it outweighs that.
        wallGun.AimingAt = hunter;
        Assert.IsTrue(director.TryAssign(hunter, from, out IRaidTarget shooting));
        Assert.AreSame(wallGun, shooting);

        // And a pad gun on its last legs outweighs both.
        padGun.Health = 5f;
        Assert.IsTrue(director.TryAssign(hunter, from, out IRaidTarget battered));
        Assert.AreSame(padGun, battered);
    }

    // --- How many ------------------------------------------------------------------------------

    [Test]
    public void OnlyAFewGoForOneDefenseAndNeverMostOfTheHorde()
    {
        var gun = new FakeDefense { Position = new Vector3(0f, 0f, 0f), OnTheWall = true };
        ZombieDirector director = Director(gun);
        director.BeginAttack(bigWave: false);

        director.PlanWave(Wave(brute, brute, brute, brute, brute, brute, brute, brute, brute, brute));
        var zombies = new List<object>();
        for (int i = 0; i < 4; i++)
        {
            var zombie = new object();
            director.Enlist(zombie, brute);
            zombies.Add(zombie);
        }

        Vector3 from = new Vector3(0f, 0f, -8f);
        Assert.IsTrue(director.TryAssign(zombies[0], from, out _));
        Assert.IsTrue(director.TryAssign(zombies[1], from, out _));
        Assert.IsFalse(director.TryAssign(zombies[2], from, out _), "Two at most on one defense in a nightly attack.");
        Assert.AreEqual(2, director.AttackersOf(gun));

        director.Release(zombies[0]);
        director.Release(zombies[1]);

        live = 2;
        Assert.IsTrue(director.TryAssign(zombies[0], from, out _));
        Assert.IsFalse(director.TryAssign(zombies[1], from, out _), "With two out, one stays on the restaurant.");
    }

    [Test]
    public void OnceTheNightsKnockOutsAreUsedUpEveryoneTurnsOnTheRestaurant()
    {
        var gun = new FakeDefense { Position = new Vector3(0f, 0f, 0f), OnTheWall = true, Health = 20f, MaxHealth = 20f };
        var other = new FakeDefense { Position = new Vector3(4f, 0f, 0f), OnTheWall = true };
        ZombieDirector director = Director(gun, other);
        director.BeginAttack(bigWave: false);

        director.PlanWave(Wave(brute, brute, brute, brute, brute, brute, brute, brute, brute, brute));
        object a = new object(), b = new object(), c = new object();
        director.Enlist(a, brute);
        director.Enlist(b, brute);
        director.Enlist(c, brute);

        // Six metres to the gun, seven to the other: the first takes the gun, the second the other
        // (the gun with one on it counts as ten), the third the gun again.
        Vector3 from = new Vector3(0f, 0f, -6f);
        Assert.IsTrue(director.TryAssign(a, from, out IRaidTarget onGun));
        Assert.IsTrue(director.TryAssign(b, from, out IRaidTarget onOther));
        Assert.IsTrue(director.TryAssign(c, from, out IRaidTarget alsoOnGun));
        Assert.AreSame(gun, onGun);
        Assert.AreSame(other, onOther);
        Assert.AreSame(gun, alsoOnGun);

        IDefenseObjective downed = null;
        bool capped = false;
        director.OnDefenseKnockedOut += d => downed = d;
        director.OnKnockOutCapReached += () => capped = true;

        Assert.IsFalse(director.Strike(a, onGun, 12f, from));
        Assert.IsTrue(director.Strike(a, onGun, 12f, from), "The blow that knocks it out.");

        Assert.AreSame(gun, downed);
        Assert.IsTrue(capped, "A nightly attack knocks out one defense at most.");
        Assert.AreEqual(1, director.KnockedOut);
        Assert.IsFalse(director.StillWanted(c, alsoOnGun, from), "Nobody goes on hitting a defense that is down.");
        Assert.IsFalse(director.StillWanted(b, onOther, from), "The one on the other gun lets go too.");
        Assert.IsFalse(director.TryAssign(b, from, out _), "And nobody is sent at a defense again tonight.");
    }

    [Test]
    public void AZombieThatGaveUpIsNotSentBackToTheSameDefense()
    {
        var gun = new FakeDefense { Position = new Vector3(0f, 0f, 0f), OnTheWall = true };
        var other = new FakeDefense { Position = new Vector3(10f, 0f, 0f), OnTheWall = true };
        ZombieDirector director = Director(gun, other);
        director.BeginAttack(bigWave: true);
        object hunter = Hunter(director);

        Vector3 from = new Vector3(0f, 0f, -5f);
        Assert.IsTrue(director.TryAssign(hunter, from, out IRaidTarget first));
        Assert.AreSame(gun, first);

        director.GaveUp(hunter, first);

        Assert.IsTrue(director.TryAssign(hunter, from, out IRaidTarget next));
        Assert.AreSame(other, next);
    }

    // --- The yard ------------------------------------------------------------------------------

    [Test]
    public void OverTheWallWithNoDefenseWantingItAZombieStillRaidsTheYard()
    {
        var bedObject = new GameObject("Bed");
        owned.Add(bedObject);
        bedObject.transform.position = new Vector3(5f, 0f, 8f);
        bedObject.AddComponent<BoxCollider>();
        OutdoorWreck bed = bedObject.AddComponent<OutdoorWreck>();
        bed.Configure(30f, "Bed");
        yard.Register(bed);
        yard.BeginEncounter(2);

        ZombieDirector director = Director();
        director.BeginAttack(bigWave: true);

        var zombie = new object();
        director.Enlist(zombie, shambler);

        Assert.IsTrue(director.TryAssign(zombie, new Vector3(6f, 0f, 9f), out IRaidTarget target));
        Assert.AreSame(bed, target);
        Assert.IsFalse(target.WorthBreakingThroughFor, "A bed is not worth smashing a wall for.");
    }

    [Test]
    public void TheYardsRaidersLeaveDefensesToTheHorde()
    {
        var turretObject = new GameObject("Turret");
        owned.Add(turretObject);
        turretObject.transform.position = new Vector3(5f, 0f, 8f);
        turretObject.AddComponent<BoxCollider>();
        OutdoorWreck turret = turretObject.AddComponent<OutdoorWreck>();
        turret.Configure(90f, "Turret", isDefense: true);
        yard.Register(turret);
        yard.BeginEncounter(2);

        Assert.IsFalse(yard.TryAssign(new object(), new Vector3(6f, 0f, 9f), out _));

        bool reported = false;
        yard.OnWrecked += _ => reported = true;
        turret.Wreck();
        yard.ReportDefenseWrecked(turret);

        Assert.IsTrue(reported, "Told to the HUD and the wrench like any wreck.");
        Assert.AreEqual(0, yard.WrecksThisEncounter, "But it does not use up the yard's cap.");
    }

    [Test]
    public void ADefenseKnockedOutCountsAgainstTheAttacksResult()
    {
        Assert.AreEqual(EncounterResultKind.WreckedVictory, ReputationService.Classify(false, false, 0 + 1));

        var result = new EncounterResult { Kind = EncounterResultKind.WreckedVictory, Wrecks = 0, DefensesKnockedOut = 2 };
        StringAssert.Contains("knocked out 2 defenses", result.Summary);
    }
}
#endif
