#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.EditorTools;
using LastOrders.Night;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Hot Dog Zombie: how its gait moves - legs in opposition, a hop between footfalls, a jelly
/// boing when hit that settles, a slow lift and fast drop on a wall - and that the archetype is
/// wired in: its own model left untinted, in the roster, and in every wave manifest.
/// </summary>
public class HotDogZombieTests
{
    readonly List<Object> owned = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in owned)
            if (o != null) Object.DestroyImmediate(o);
        owned.Clear();
    }

    ZombieGaitProfile Gait()
    {
        var gait = ScriptableObject.CreateInstance<ZombieGaitProfile>();
        owned.Add(gait);
        return gait;
    }

    // --- The gait ------------------------------------------------------------------------------

    [Test]
    public void StandingStillNeitherStepsNorHops()
    {
        ZombieGaitProfile gait = Gait();

        Assert.AreEqual(0f, gait.LegSwing(1.1f, moving: 0f));
        Assert.AreEqual(0f, gait.Hop(1.1f, moving: 0f));
        Assert.LessOrEqual(Mathf.Abs(gait.Roll(1.1f, 0f, 0.55f)), gait.idleSwayDegrees + 1e-4f, "Only the idle sway.");
    }

    [Test]
    public void WalkingSwingsTheLegsFullyAndHopsBetweenFootfalls()
    {
        ZombieGaitProfile gait = Gait();

        Assert.AreEqual(gait.legSwingDegrees, gait.LegSwing(Mathf.PI * 0.5f, 1f), 1e-3f);
        Assert.AreEqual(gait.hopHeight, gait.Hop(0f, 1f), 1e-4f, "Highest as the legs pass each other.");
        Assert.AreEqual(0f, gait.Hop(Mathf.PI * 0.5f, 1f), 1e-4f, "Down on the footfall.");
        Assert.AreEqual(gait.waddleDegrees, gait.Roll(Mathf.PI * 0.5f, 1f, 0f), 1e-3f, "Rolled onto the foot that is down.");
    }

    [Test]
    public void HitSquashesFirstThenBoingsBackAndSettles()
    {
        ZombieGaitProfile gait = Gait();
        gait.hurtSquash = 0.2f;
        gait.hurtSeconds = 0.6f;

        Assert.AreEqual(-0.2f, gait.HurtStretch(0f), 1e-4f, "Squashed flat at once.");

        float tallest = 0f;
        for (float t = 0f; t < 0.6f; t += 0.01f) tallest = Mathf.Max(tallest, gait.HurtStretch(t));
        Assert.Greater(tallest, 0f, "Springs back up past its size.");
        Assert.Less(tallest, 0.2f);

        Assert.AreEqual(0f, gait.HurtStretch(0.6f));
        Assert.AreEqual(0f, gait.HurtWobble(0.6f));
        Assert.AreEqual(0f, gait.HurtStretch(float.MaxValue));
    }

    [Test]
    public void BlowOnAWallLiftsSlowlyAndDropsFast()
    {
        ZombieGaitProfile gait = Gait();
        gait.poundSeconds = 0.5f;

        Assert.AreEqual(0f, gait.PoundLift(0f), 1e-4f);
        Assert.AreEqual(1f, gait.PoundLift(0.33f), 1e-3f, "All the way up two-thirds of the way in.");
        Assert.Less(gait.PoundLift(0.45f), gait.PoundLift(0.36f), "Then coming down.");
        Assert.AreEqual(0f, gait.PoundLift(0.5f));
        Assert.AreEqual(0f, gait.PoundLift(float.MaxValue), "Nothing between blows.");
    }

    // --- The archetype ---------------------------------------------------------------------------

    static EnemyDefinition HotDog()
    {
        var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(HotDogZombieModel.EnemyPath);
        if (enemy == null) Assert.Inconclusive("The Hot Dog Zombie has not been built in this project.");
        return enemy;
    }

    [Test]
    public void HotDogHasItsOwnModelAndKeepsItsColours()
    {
        EnemyDefinition enemy = HotDog();

        Assert.IsFalse(enemy.tintBody, "Its own paint; the spawner's tint would wash it out.");
        Assert.IsNotNull(enemy.prefab);
        Assert.IsNotNull(enemy.prefab.GetComponent<EnemyAgent>());
        Assert.IsNotNull(enemy.prefab.GetComponent<MascotZombieBody>());
        Assert.IsNull(enemy.prefab.GetComponent<ZombieBody>());
        Assert.Greater(enemy.maxHealth, 30f, "Tougher than a Zombie for the padding.");
    }

    [Test]
    public void HotDogIsInTheRosterAndEveryWave()
    {
        EnemyDefinition enemy = HotDog();

        var balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>("Assets/Game Assets/Content/LastOrders/GameBalance.asset");
        CollectionAssert.Contains(balance.enemies, enemy);

        foreach (string guid in AssetDatabase.FindAssets("t:WaveManifest"))
        {
            var manifest = AssetDatabase.LoadAssetAtPath<WaveManifest>(AssetDatabase.GUIDToAssetPath(guid));
            bool listed = false;
            foreach (WaveManifest.Entry entry in manifest.entries)
                if (entry.enemy == enemy && entry.weight > 0f) listed = true;

            Assert.IsTrue(listed, $"{manifest.name} never sends a Hot Dog.");
        }
    }
}
#endif
