#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using FoodStreet.Visitors;
using LastOrders.Crew;
using LastOrders.Economy;
using LastOrders.EditorTools;
using LastOrders.Jobs;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// The crew and what goes with them: chores handed out one to a crew member, a mess first, nothing
/// done twice in a minute; a helper speeding a station up and no more; the main character covering
/// for roles nobody is on duty for; how diners judge their meal; rewards flying only from where they
/// were earned; and the animators carrying every clip the code asks for.
/// </summary>
public class CrewTests
{
    readonly List<Object> made = new List<Object>();

    sealed class FakeSource : IChoreSource
    {
        public readonly List<Chore> Chores = new List<Chore>();
        public void Offer(Vector3 from, List<Chore> into) => into.AddRange(Chores);
    }

    static Chore At(string key, Vector3 stand, float weight = 1f, bool urgent = false) => new Chore
    {
        Label = key, Key = key, Stand = stand, Work = stand + Vector3.forward, Seconds = 2f, Weight = weight, Urgent = urgent,
    };

    CrewBrain Brain(string name)
    {
        var go = new GameObject(name);
        made.Add(go);
        return go.AddComponent<CrewBrain>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in made) if (o != null) Object.DestroyImmediate(o);
        made.Clear();
        ChoreBoard.Reset();
        RewardSource.Clear();
    }

    // --- The chore board -------------------------------------------------------------------------------

    [Test]
    public void AMessIsCleanedBeforeAnythingElseTheNearestFirst()
    {
        var source = new FakeSource();
        source.Chores.Add(At("sweep", new Vector3(0.5f, 0f, 0f), weight: 100f));
        source.Chores.Add(At("far mess", new Vector3(9f, 0f, 0f), urgent: true));
        source.Chores.Add(At("near mess", new Vector3(3f, 0f, 0f), urgent: true));
        ChoreBoard.UseOnly(source);

        Assert.IsTrue(ChoreBoard.TryTake(Brain("a"), Vector3.zero, 0.5f, 0f, out Chore chore));
        Assert.AreEqual("near mess", chore.Key);
    }

    [Test]
    public void AChoreHeldByOneIsNotGivenToAnother()
    {
        var source = new FakeSource();
        source.Chores.Add(At("table", Vector3.one));
        ChoreBoard.UseOnly(source);

        CrewBrain a = Brain("a"), b = Brain("b");
        Assert.IsTrue(ChoreBoard.TryTake(a, Vector3.zero, 0.5f, 0f, out Chore chore));
        Assert.IsFalse(ChoreBoard.TryTake(b, Vector3.zero, 0.5f, 0f, out _), "held by a");
        Assert.IsTrue(ChoreBoard.StillOn(chore, a));
        Assert.IsFalse(ChoreBoard.StillOn(chore, b));

        ChoreBoard.Drop(chore, a);
        Assert.IsTrue(ChoreBoard.TryTake(b, Vector3.zero, 0.5f, 0f, out _), "dropped, so b may have it");
    }

    [Test]
    public void AChoreDoneIsLeftAloneForAWhileAndFinishingItDoesIt()
    {
        var source = new FakeSource();
        bool done = false;
        Chore table = At("table", Vector3.one);
        table.Done = () => done = true;
        source.Chores.Add(table);
        ChoreBoard.UseOnly(source);

        CrewBrain a = Brain("a");
        Assert.IsTrue(ChoreBoard.TryTake(a, Vector3.zero, 0.5f, 100f, out Chore chore));
        ChoreBoard.Finish(chore, a, 100f);
        Assert.IsTrue(done);

        Assert.IsFalse(ChoreBoard.TryTake(a, Vector3.zero, 0.5f, 110f, out _), "just done");
        Assert.IsTrue(ChoreBoard.TryTake(a, Vector3.zero, 0.5f, 100f + ChoreBoard.AgainAfter + 1f, out _), "due again");
    }

    [Test]
    public void AChoreNoLongerWantedIsNotStillOn()
    {
        var source = new FakeSource();
        bool wanted = true;
        Chore table = At("table", Vector3.one);
        table.StillWanted = () => wanted;
        source.Chores.Add(table);
        ChoreBoard.UseOnly(source);

        CrewBrain a = Brain("a");
        Assert.IsTrue(ChoreBoard.TryTake(a, Vector3.zero, 0.5f, 0f, out Chore chore));
        Assert.IsTrue(ChoreBoard.StillOn(chore, a));
        wanted = false;
        Assert.IsFalse(ChoreBoard.StillOn(chore, a), "somebody sat down at the table");
    }

    [Test]
    public void TheCloserOfTwoEqualChoresIsTheLikelier()
    {
        var source = new FakeSource();
        source.Chores.Add(At("near", new Vector3(1f, 0f, 0f)));
        source.Chores.Add(At("far", new Vector3(30f, 0f, 0f)));
        ChoreBoard.UseOnly(source);

        int near = 0;
        for (int i = 0; i < 20; i++)
        {
            Assert.IsTrue(ChoreBoard.TryTake(null, Vector3.zero, i / 20f, 0f, out Chore chore));
            if ((string)chore.Key == "near") near++;
        }

        Assert.Greater(near, 12, "the near one wins most rolls");
    }

    // --- A helper at a station --------------------------------------------------------------------------

    [Test]
    public void AHelperSpeedsAStationUpOnlyWhileTheyStay()
    {
        var go = new GameObject("Boost test station");
        made.Add(go);
        go.SetActive(false);
        var station = go.AddComponent<CookeryStation>();
        object mia = new object(), bob = new object();

        Assert.AreEqual(1f, station.CookSpeed, 1e-5f);
        station.AddBoost(mia, 0.15f);
        Assert.AreEqual(1.15f, station.CookSpeed, 1e-5f);
        station.AddBoost(mia, 0.15f);
        Assert.AreEqual(1.15f, station.CookSpeed, 1e-5f, "the same helper twice is still one");
        station.AddBoost(bob, 0.1f);
        Assert.AreEqual(1.25f, station.CookSpeed, 1e-5f);
        station.RemoveBoost(mia);
        Assert.AreEqual(1.1f, station.CookSpeed, 1e-5f);
        station.RemoveBoost(bob);
        Assert.IsFalse(station.IsBoosted);
    }

    // --- Covering for roles nobody is on duty for -----------------------------------------------------

    [Test]
    public void TheMainCharacterCoversForARoleNobodyIsOnDutyFor()
    {
        var jobs = new JobService();
        var target = new GameObject("repair target");
        made.Add(target);
        jobs.Queue("repair:wall", target.transform, WorkerRole.Helper, Vector3.zero, 2f, "Repair the wall");

        Assert.IsFalse(jobs.TryClaim(WorkerRole.MainCharacter, "main", out _), "nobody has reported in: roles are kept strictly");

        jobs.ReportOnDuty(WorkerRole.MainCharacter, "main", true);
        jobs.ReportOnDuty(WorkerRole.Helper, "helper", true);
        Assert.IsFalse(jobs.TryClaim(WorkerRole.MainCharacter, "main", out _), "the Helper is on duty: it is theirs");

        jobs.ReportOnDuty(WorkerRole.Helper, "helper", false);
        Assert.IsTrue(jobs.Covers(WorkerRole.MainCharacter, WorkerRole.Helper));
        Assert.IsTrue(jobs.TryClaim(WorkerRole.MainCharacter, "main", out Job claimed), "no Helper on duty: the main character goes");
        Assert.AreEqual(WorkerRole.Helper, claimed.RequiredRole);
        Assert.IsFalse(jobs.Covers(WorkerRole.Helper, WorkerRole.MainCharacter), "only the main character covers");
    }

    // --- Diners after their meal ------------------------------------------------------------------------

    [Test]
    public void ADinerServedQuicklyIsDelightedAndOneKeptWaitingIsContent()
    {
        Assert.AreEqual(MealVerdict.Delighted, VisitorReactions.Judge(5f));
        Assert.AreEqual(MealVerdict.Happy, VisitorReactions.Judge(VisitorReactions.DelightedWithin + 1f));
        Assert.AreEqual(MealVerdict.Content, VisitorReactions.Judge(VisitorReactions.HappyWithin + 1f));
    }

    [Test]
    public void EveryReactionIsAOneShotGestureThatTakesTime()
    {
        foreach (MealVerdict verdict in System.Enum.GetValues(typeof(MealVerdict)))
        {
            for (float roll = 0f; roll < 1f; roll += 0.05f)
            {
                Gesture g = VisitorReactions.Pick(verdict, roll);
                Assert.AreNotEqual(Gesture.None, g);
                Assert.IsFalse(WorkerGestures.IsHeld(g), $"{g} lets go by itself");
                Assert.Greater(WorkerGestures.Seconds(g), 0.5f, g.ToString());
            }
        }
    }

    // --- Rewards ------------------------------------------------------------------------------------

    [Test]
    public void ARewardFliesOnlyFromASourceGivenThisFrame()
    {
        RewardSource.Clear();
        Assert.IsFalse(RewardSource.TryGetScreen(out _), "no source: it just counts up");

        RewardSource.AtScreen(new Vector2(120f, 340f));
        Assert.IsTrue(RewardSource.TryGetScreen(out Vector2 from));
        Assert.AreEqual(new Vector2(120f, 340f), from);

        RewardSource.Clear();
        Assert.IsFalse(RewardSource.TryGetScreen(out _));
    }

    [Test]
    public void AGestureDriverWithoutTheContractDoesNothing()
    {
        var driver = new GestureDriver(null);
        Assert.IsFalse(driver.CanGesture);
        Assert.IsFalse(driver.Play(Gesture.Wave));
        Assert.IsFalse(driver.Busy);
        driver.Tick(1f);
        driver.Reset();
    }

    // --- The animators ----------------------------------------------------------------------------------

    static AnimationClip ClipFor(string name) =>
        AssetDatabase.LoadAssetAtPath<AnimationClip>($"{WorkerAnimations.ClipFolder}/Worker_{name}.anim");

    [Test]
    public void EveryChoreLoopHasItsClipAndKeepsItsTime()
    {
        if (ClipFor("WorkWipe") == null) Assert.Ignore("Build the worker animations first (Last Orders / Workers).");

        foreach ((string name, int style) in new[] { ("WorkWipe", WorkerGestures.WorkWipe), ("WorkSweep", WorkerGestures.WorkSweep), ("WorkDust", WorkerGestures.WorkDust) })
        {
            AnimationClip clip = ClipFor(name);
            Assert.That(clip, Is.Not.Null, name);
            Assert.That(clip.isLooping, Is.True, name);
            Assert.That(clip.length, Is.EqualTo(WorkerGestures.WorkSeconds(style)).Within(0.05f), name);
        }
    }

    [Test]
    public void TheWorkerControllerCanCarryAPlateAndWorkWithIK()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(WorkerAnimations.ControllerPath);
        if (controller == null) Assert.Ignore("Build the worker animations first (Last Orders / Workers).");

        AnimatorControllerLayer gestures = null, carry = null;
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            if (layer.name == WorkerGestures.LayerName) gestures = layer;
            if (layer.name == WorkerGestures.CarryLayerName) carry = layer;
        }

        Assert.That(gestures, Is.Not.Null);
        Assert.That(gestures.iKPass, Is.True, "the crew's hands are put on their work by IK on this layer");
        Assert.That(carry, Is.Not.Null, "the crew wait tables on the workers' controller");
        Assert.That(carry.defaultWeight, Is.EqualTo(0f));
    }

    [Test]
    public void TheWorkersWalkAndJogAtTheirRealSpeed()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(WorkerAnimations.ControllerPath);
        AnimationClip jog = ClipFor("Jog");
        if (controller == null || jog == null) Assert.Ignore("Build the worker animations first (Last Orders / Workers).");

        Assert.That(jog.isLooping, Is.True);
        Assert.That(jog.length, Is.EqualTo(CharacterLocomotion.JogCycleSeconds).Within(0.05f));

        BlendTree tree = null;
        foreach (ChildAnimatorState s in controller.layers[0].stateMachine.states)
            if (s.state.name == "Movement") tree = s.state.motion as BlendTree;

        Assert.That(tree, Is.Not.Null);
        Assert.That(tree.blendParameter, Is.EqualTo(CharacterLocomotion.SpeedParam), "blended by real speed, not a 0-1 guess");
        Assert.That(tree.children.Length, Is.EqualTo(3));
        Assert.That(tree.children[0].threshold, Is.EqualTo(0f));
        Assert.That(tree.children[1].threshold, Is.EqualTo(CharacterLocomotion.WalkPace), "the walk at its own pace");
        Assert.That(tree.children[2].threshold, Is.EqualTo(CharacterLocomotion.JogPace), "the jog at its own pace");
    }

    [Test]
    public void APlateIsCarriedInBothHands()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(WorkerAnimations.ControllerPath);
        if (controller == null) Assert.Ignore("Build the worker animations first (Last Orders / Workers).");

        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            if (layer.name != WorkerGestures.CarryLayerName) continue;

            Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm), Is.True);
            Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm), Is.True, "both arms come up round the plate");
            Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Body), Is.False, "the walk keeps the back");
            return;
        }

        Assert.Fail("No carry layer.");
    }

    [Test]
    public void AWalkerRoutingRoundThingsStillCountsItsOwnPoints()
    {
        var go = new GameObject("walker");
        made.Add(go);
        var walker = new WaypointWalker { GridRadius = 0.3f };
        walker.Follow(new[] { new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, 2f) });
        Assert.AreEqual(2, walker.Remaining);

        // No nav grid here: each leg is walked straight, and the count is of the points given.
        for (int i = 0; i < 200 && walker.Remaining == 2; i++) walker.Advance(go.transform, 2f, 0f, 0.05f, 0.05f);
        Assert.AreEqual(1, walker.Remaining);
        for (int i = 0; i < 200 && !walker.Arrived; i++) walker.Advance(go.transform, 2f, 0f, 0.05f, 0.05f);
        Assert.IsTrue(walker.Arrived);
        Assert.Less(Vector3.Distance(go.transform.position, new Vector3(1f, 0f, 2f)), 0.06f);
    }

    [Test]
    public void TheVisitorControllerReactsToTheMealOnItsArmsOnly()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Game Assets/Prefabs/Characters/VisitorAnimator.controller");
        if (controller == null) Assert.Ignore("No visitor animator.");

        AnimatorControllerLayer gestures = null;
        foreach (AnimatorControllerLayer layer in controller.layers)
            if (layer.name == WorkerGestures.LayerName) gestures = layer;
        if (gestures == null) Assert.Ignore("Run 'Add After-Meal Reactions' first.");

        Assert.That(gestures.defaultWeight, Is.EqualTo(0f), "at rest the seated IK keeps the hands");
        Assert.That(gestures.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Body), Is.False, "the chair keeps the back and hips");
        Assert.That(gestures.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm), Is.True);

        var states = new HashSet<string>();
        foreach (ChildAnimatorState s in gestures.stateMachine.states) states.Add(s.state.name);
        foreach (MealVerdict verdict in System.Enum.GetValues(typeof(MealVerdict)))
            for (float roll = 0f; roll < 1f; roll += 0.05f)
                Assert.That(states, Does.Contain(VisitorReactions.Pick(verdict, roll).ToString()));
    }
}
#endif
