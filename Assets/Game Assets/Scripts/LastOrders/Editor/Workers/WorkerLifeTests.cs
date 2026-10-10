#if UNITY_INCLUDE_TESTS
using LastOrders.EditorTools;
using LastOrders.Jobs;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// The workers' animation contract: every gesture has its clip, as long as the idle brain waits for
/// it; held ones loop and one-shots do not; the controller has every parameter the brain sets.
/// </summary>
public class WorkerLifeTests
{
    static AnimationClip ClipFor(string name) =>
        AssetDatabase.LoadAssetAtPath<AnimationClip>($"{WorkerAnimations.ClipFolder}/Worker_{name}.anim");

    [Test]
    public void EveryGestureHasAClipAsLongAsTheBrainWaits()
    {
        if (ClipFor("AliveIdle") == null) Assert.Ignore("Build the worker animations first (Last Orders / Workers).");

        foreach (Gesture gesture in System.Enum.GetValues(typeof(Gesture)))
        {
            if (gesture == Gesture.None) continue;

            AnimationClip clip = ClipFor(gesture.ToString());
            Assert.That(clip, Is.Not.Null, gesture.ToString());
            Assert.That(clip.humanMotion, Is.True, $"{gesture} is a humanoid clip");
            Assert.That(clip.length, Is.EqualTo(WorkerGestures.Seconds(gesture)).Within(0.05f), gesture.ToString());
            Assert.That(clip.isLooping, Is.EqualTo(WorkerGestures.IsHeld(gesture)), $"{gesture}: held gestures loop, one-shots do not");
        }
    }

    [Test]
    public void TheToolSwingLoopKeepsTimeWithTheToolSwing()
    {
        AnimationClip swing = ClipFor("WorkSwing");
        if (swing == null) Assert.Ignore("Build the worker animations first (Last Orders / Workers).");

        // WorkerToolSwing's default rate is 1.6 swings a second.
        Assert.That(swing.length, Is.EqualTo(1f / 1.6f).Within(0.02f));
        Assert.That(swing.isLooping, Is.True);
    }

    [Test]
    public void TheControllerHasEveryParameterTheBrainSets()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(WorkerAnimations.ControllerPath);
        if (controller == null) Assert.Ignore("Build the worker animations first (Last Orders / Workers).");

        string[] expected = { "Move", WorkerGestures.WorkingParam, WorkerGestures.WorkStyleParam, WorkerGestures.GestureParam, WorkerGestures.ActParam };
        foreach (string name in expected)
            Assert.That(System.Array.Exists(controller.parameters, p => p.name == name), Is.True, name);

        Assert.That(controller.layers.Length, Is.EqualTo(2));
        Assert.That(controller.layers[0].iKPass, Is.True, "the look-at needs the base layer's IK pass");
        Assert.That(controller.layers[1].avatarMask, Is.Not.Null, "gestures move the upper body only");
    }

    [Test]
    public void HeldGesturesAreTheTwoThatHold()
    {
        Assert.That(WorkerGestures.IsHeld(Gesture.HandsOnHips), Is.True);
        Assert.That(WorkerGestures.IsHeld(Gesture.ArmsCrossed), Is.True);
        Assert.That(WorkerGestures.IsHeld(Gesture.Wave), Is.False);
        Assert.That(WorkerGestures.Seconds(Gesture.None), Is.EqualTo(0f));
    }
}
#endif
