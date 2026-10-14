#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using LastOrders.EditorTools;
using LastOrders.Night;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// The zombies' own animations: the walk played at the pace the body really moves, the battering
/// kept on the beat the agent strikes on, the controller holding every state the body cues by name,
/// both zombie prefabs wearing it, the deaths ending flat on the ground and the loops closing.
/// </summary>
public class ZombieAnimationTests
{
    const string ZombieModel = "Assets/Game Assets/Models/Characters/Zombie 1.fbx";

    // --- Pace ----------------------------------------------------------------------------------

    [Test]
    public void StandingStillPlaysTheIdleAtItsOwnRate()
    {
        Assert.AreEqual(0f, ZombieMotion.Move(0f, 1f));
        Assert.AreEqual(1f, ZombieMotion.WalkRate(0f, 0f, 1f));
    }

    [Test]
    public void AtTheShamblesOwnSpeedTheFeetKeepPace()
    {
        const float scale = 1.06f;
        float speed = ZombieMotion.ShambleSpeed * scale;
        float move = ZombieMotion.Move(speed, scale);

        Assert.AreEqual(ZombieMotion.ShambleSpeed, move, 1e-4f);
        Assert.AreEqual(1f, ZombieMotion.WalkRate(speed, move, scale), 1e-4f);
    }

    [Test]
    public void FastOnesBlendTowardTheLopeAndSlowOnesStillStep()
    {
        float fast = ZombieMotion.Move(2.2f, 1f);
        Assert.Greater(fast, ZombieMotion.ShambleSpeed);
        Assert.LessOrEqual(fast, ZombieMotion.LopeSpeed);

        float slow = ZombieMotion.Move(0.3f, 1f);
        Assert.AreEqual(ZombieMotion.ShambleSpeed, slow, 1e-4f, "A crawl still plays the shamble, slowed.");
        Assert.AreEqual(ZombieMotion.MinWalkRate, ZombieMotion.WalkRate(0.3f, slow, 1f), 1e-4f);
    }

    [TestCase(0.5f, 1f)]
    [TestCase(0.38f, 1f)]
    [TestCase(0.64f, 1f)]
    [TestCase(0.45f, 1.6f)]
    public void TheNextBlowComesDownOnTheNextBeat(float phase, float interval)
    {
        float rate = ZombieMotion.BatterRate(phase, interval);
        float landed = Mathf.Repeat(phase + rate * interval / ZombieMotion.BatterSeconds, 1f);

        Assert.AreEqual(ZombieMotion.ImpactPhase, landed, 1e-3f);
    }

    // --- The controller ------------------------------------------------------------------------

    static AnimatorController Controller()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ZombieAnimations.ControllerPath);
        if (controller == null) Assert.Ignore("Run 'Last Orders / Hospital & Zombies / Build Zombie Animations' first.");
        return controller;
    }

    static HashSet<string> States(AnimatorController controller, int layer)
    {
        var names = new HashSet<string>();
        foreach (ChildAnimatorState child in controller.layers[layer].stateMachine.states) names.Add(child.state.name);
        return names;
    }

    [Test]
    public void TheControllerHasEveryStateTheBodyCuesByName()
    {
        AnimatorController controller = Controller();
        Assert.AreEqual(3, controller.layers.Length);

        CollectionAssert.IsSupersetOf(States(controller, ZombieMotion.BaseLayer),
            new[] { ZombieMotion.LocomotionState, ZombieMotion.DeathBackState, ZombieMotion.DeathForwardState });
        CollectionAssert.IsSupersetOf(States(controller, ZombieMotion.ActionLayer),
            new[] { ZombieMotion.EmptyState, ZombieMotion.SlamState, ZombieMotion.ClawState });
        CollectionAssert.IsSupersetOf(States(controller, ZombieMotion.FlinchLayer),
            new[] { ZombieMotion.EmptyState, ZombieMotion.HitFrontState, ZombieMotion.HitLeftState, ZombieMotion.HitRightState });

        var parameters = new HashSet<string>();
        foreach (AnimatorControllerParameter p in controller.parameters) parameters.Add(p.name);
        CollectionAssert.IsSupersetOf(parameters,
            new[] { ZombieMotion.MoveParam, ZombieMotion.StyleParam, ZombieMotion.WalkRateParam, ZombieMotion.BatterRateParam });

        Assert.AreEqual(AnimatorLayerBlendingMode.Additive, controller.layers[ZombieMotion.FlinchLayer].blendingMode);
    }

    [Test]
    public void BothZombiePrefabsWearTheZombieController()
    {
        AnimatorController controller = Controller();

        foreach (string path in new[] { ZombieContentGenerator.ZombiePath, ZombieContentGenerator.WandererPath })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);

            Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
            Assert.IsNotEmpty(animators, path);
            foreach (Animator animator in animators)
                Assert.AreSame(controller, animator.runtimeAnimatorController, $"{path}: {animator.name}");
        }
    }

    // --- The clips -----------------------------------------------------------------------------

    static AnimationClip Clip(string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ZombieAnimations.ClipFolder}/Zombie_{name}.anim");
        if (clip == null) Assert.Ignore("Run 'Last Orders / Hospital & Zombies / Build Zombie Animations' first.");
        return clip;
    }

    [TestCase("Death_Back")]
    [TestCase("Death_Forward")]
    public void ADeathEndsLyingFlatOnTheGround(string name)
    {
        AnimationClip clip = Clip(name);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ZombieModel);
        if (model == null) Assert.Ignore("No zombie model.");

        var body = (GameObject)Object.Instantiate(model);
        try
        {
            body.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Animator animator = body.GetComponent<Animator>();
            clip.SampleAnimation(body, clip.length);
            body.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            float hips = animator.GetBoneTransform(HumanBodyBones.Hips).position.y;
            float head = animator.GetBoneTransform(HumanBodyBones.Head).position.y;
            Assert.Less(hips, 0.4f, "The hips are down.");
            Assert.Less(head, 0.45f, "The head is down.");
            Assert.Greater(hips, -0.05f, "Not sunk into the ground.");
            Assert.Greater(head, -0.05f, "Not sunk into the ground.");
        }
        finally
        {
            Object.DestroyImmediate(body);
        }
    }

    [TestCase("Idle_Swayer")]
    [TestCase("Idle_Drooper")]
    [TestCase("Shamble_Reacher")]
    [TestCase("Shamble_Dragger")]
    [TestCase("Lope")]
    [TestCase("Batter_Slam")]
    [TestCase("Batter_Claw")]
    public void ALoopEndsWhereItStarts(string name)
    {
        AnimationClip clip = Clip(name);
        Assert.IsTrue(AnimationUtility.GetAnimationClipSettings(clip).loopTime, "Set to loop.");

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            Assert.AreEqual(curve.Evaluate(0f), curve.Evaluate(clip.length), 0.03f, binding.propertyName);
        }
    }
}
#endif
