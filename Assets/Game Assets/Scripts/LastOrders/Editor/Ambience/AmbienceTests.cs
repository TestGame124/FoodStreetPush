#if UNITY_INCLUDE_TESTS
using LastOrders.Ambience;
using LastOrders.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The wind's gusts staying a gust envelope, the shader's smoothstep matched, the open-ground map
/// telling a standing thing from a flat one, perches going with what they sit on, and the birds'
/// prefabs wired up as the kind they are.
/// </summary>
public class AmbienceTests
{
    [Test]
    public void GustsStayBetweenNothingAndFullAndComeAndGo()
    {
        float lowest = 1f, highest = 0f;
        for (float x = 0f; x < 600f; x += 0.25f)
        {
            float g = AmbientWind.GustAt(x);
            Assert.That(g, Is.InRange(0f, 1f));
            lowest = Mathf.Min(lowest, g);
            highest = Mathf.Max(highest, g);
        }

        Assert.That(lowest, Is.LessThan(0.05f), "there are lulls between gusts");
        Assert.That(highest, Is.GreaterThan(0.9f), "gusts reach full strength");
    }

    [Test]
    public void SmoothstepIsTheShadersNotAnEasedLerp()
    {
        Assert.That(AmbientWind.Smoothstep(0.2f, 0.8f, 0.1f), Is.EqualTo(0f));
        Assert.That(AmbientWind.Smoothstep(0.2f, 0.8f, 0.9f), Is.EqualTo(1f));
        Assert.That(AmbientWind.Smoothstep(0.2f, 0.8f, 0.5f), Is.EqualTo(0.5f).Within(1e-5f));
        Assert.That(AmbientWind.Smoothstep(0f, 1f, 0.25f), Is.EqualTo(0.15625f).Within(1e-5f));
    }

    [Test]
    public void GroundUnderAStandingThingIsTakenAndFlatThingsLeaveItOpen()
    {
        var area = new Rect(4980f, 4980f, 40f, 40f);
        var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var mat = GameObject.CreatePrimitive(PrimitiveType.Quad);
        try
        {
            crate.transform.position = new Vector3(4990f, 0.5f, 4990f);
            mat.transform.SetPositionAndRotation(new Vector3(5010f, 0.02f, 5010f), Quaternion.Euler(90f, 0f, 0f));
            mat.transform.localScale = Vector3.one * 3f;

            var map = new GroundMap(area, 1f);
            map.Rebuild(null, null);

            Assert.That(map.IsOpen(new Vector3(4990f, 0f, 4990f), 0.5f), Is.False, "the crate's ground is taken");
            Assert.That(map.IsOpen(new Vector3(5010f, 0f, 5010f), 0.5f), Is.True, "a flat mat leaves the ground open");
            Assert.That(map.IsOpen(new Vector3(5000f, 0f, 5000f), 1f), Is.True, "open grass is open");
            Assert.That(map.IsOpen(new Vector3(4900f, 0f, 4900f), 0.5f), Is.False, "outside the map is never open");
        }
        finally
        {
            Object.DestroyImmediate(crate);
            Object.DestroyImmediate(mat);
        }
    }

    [Test]
    public void IndoorsIsTakenFloorAndAll()
    {
        var area = new Rect(5980f, 5980f, 40f, 40f);
        var diner = new GameObject("TestDiner");
        try
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floor.transform.SetParent(diner.transform, false);
            floor.transform.SetPositionAndRotation(new Vector3(6000f, 0.02f, 6000f), Quaternion.Euler(90f, 0f, 0f));
            floor.transform.localScale = Vector3.one * 8f;

            var map = new GroundMap(area, 1f);
            map.Rebuild(null, new[] { "TestDiner" });
            Assert.That(map.IsOpen(new Vector3(6000f, 0f, 6000f), 0.5f), Is.False);

            map.Rebuild(null, null);
            Assert.That(map.IsOpen(new Vector3(6000f, 0f, 6000f), 0.5f), Is.True, "the same floor outdoors is open ground");
        }
        finally
        {
            Object.DestroyImmediate(diner);
        }
    }

    [Test]
    public void APerchGoesWithWhatItSitsOn()
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.transform.position = new Vector3(7000f, 1f, 7000f);
            var perch = new AmbientBirds.Perch { surface = wall.GetComponent<Renderer>(), local = new Vector3(0f, 0.5f, 0f) };

            Assert.That(perch.Usable, Is.True);
            Assert.That(perch.Point, Is.EqualTo(new Vector3(7000f, 1.5f, 7000f)));

            wall.transform.position += Vector3.right * 2f;
            Assert.That(perch.Point.x, Is.EqualTo(7002f).Within(1e-4f), "moves with the wall");

            wall.SetActive(false);
            Assert.That(perch.Usable, Is.False, "a knocked-down wall is no perch");
        }
        finally
        {
            Object.DestroyImmediate(wall);
        }
    }

    // By name: the builder's breeds are internal to the editor tools.
    [TestCase("BlueBar", AmbientBird.Kind.Pigeon)]
    [TestCase("Sparrow", AmbientBird.Kind.Sparrow)]
    [TestCase("Crow", AmbientBird.Kind.Crow)]
    [TestCase("Bat", AmbientBird.Kind.Bat)]
    public void BirdPrefabsAreWiredAsTheirKind(string breed, AmbientBird.Kind kind)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BirdModel.PrefabPath((BirdModel.Breed)System.Enum.Parse(typeof(BirdModel.Breed), breed)));
        if (prefab == null) Assert.Ignore("Build the birds first (Last Orders / Ambience).");

        var bird = prefab.GetComponent<AmbientBird>();
        Assert.That(bird, Is.Not.Null);
        Assert.That(bird.Species, Is.EqualTo(kind));
        Assert.That(prefab.GetComponent<BirdBody>(), Is.Not.Null);
        Assert.That(prefab.transform.Find("Model/Wing_L/Tip_L"), Is.Not.Null, "each wing folds at the wrist");
    }
}
#endif
