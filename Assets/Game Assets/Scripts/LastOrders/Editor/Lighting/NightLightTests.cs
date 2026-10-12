#if UNITY_INCLUDE_TESTS
using LastOrders.EditorTools;
using LastOrders.Lighting;
using LastOrders.Perimeter;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The night lights' workings: coming on with the dark, lamps divided by the moon so they come out
/// as themselves, the cookie covering the ground the lights are on whatever way the sun is turned,
/// footprints that hold all of their light, ceiling lights that fit the room, and a claimed plot's
/// yard light that only shines once the plot is claimed.
/// </summary>
public class NightLightTests
{
    [Test]
    public void LightsComeOnWithTheDarkAndNotAtDawn()
    {
        var range = new Vector2(0.3f, 0.75f);
        Assert.That(NightLights.Ramp(range, 0f), Is.EqualTo(0f), "off by day");
        Assert.That(NightLights.Ramp(range, 0.25f), Is.EqualTo(0f), "off at dawn");
        Assert.That(NightLights.Ramp(range, 0.6f), Is.InRange(0.6f, 0.9f), "mostly on at dusk");
        Assert.That(NightLights.Ramp(range, 1f), Is.EqualTo(1f), "full at night");
        Assert.That(NightLights.Ramp(range, 0.525f), Is.EqualTo(0.5f).Within(1e-4f), "a smoothstep, half way at the middle");
    }

    [Test]
    public void ALampDividedByTheMoonComesOutAsItself()
    {
        var moon = new Color(0.44f * 0.13f, 0.54f * 0.13f, 0.78f * 0.13f);
        var lamp = new Color(1f, 0.76f, 0.44f);
        Vector3 gain = NightLights.Gain(moon, 0.5f);

        // Every shader multiplies the sun's light by the cookie, so cookie = lamp * gain gives back lamp * on.
        Assert.That(lamp.r * gain.x * moon.r, Is.EqualTo(0.5f).Within(1e-4f));
        Assert.That(lamp.g * gain.y * moon.g, Is.EqualTo(0.38f).Within(1e-4f));
        Assert.That(lamp.b * gain.z * moon.b, Is.EqualTo(0.22f).Within(1e-4f));

        Vector3 dark = NightLights.Gain(Color.black, 1f);
        Assert.That(dark.x, Is.LessThan(100f), "a sun turned right down doesn't blow the cookie up");
        Assert.That(float.IsInfinity(dark.x), Is.False);
    }

    [Test]
    public void TheCookieCoversTheGroundForAnyTurnOfTheSun()
    {
        var ground = Rect.MinMaxRect(-10f, 0f, 46f, 36f);
        var light = new GameObject("TestSun").transform;
        try
        {
            foreach (Vector3 angles in new[] { new Vector3(60f, 0f, 0f), new Vector3(52.6f, 49.9f, 32.4f), new Vector3(90f, 0f, 0f), new Vector3(30f, 200f, 10f) })
            {
                light.rotation = Quaternion.Euler(angles);
                Matrix4x4 worldToLight = NightLights.WorldToLight(light);
                Rect cookie = NightLights.LightSpaceRect(worldToLight, ground, 0f);

                // URP's lookup: uv = (light-space xy - offset) / size + 0.5, with offset = the rect's centre.
                for (float x = ground.xMin; x <= ground.xMax; x += 7f)
                for (float z = ground.yMin; z <= ground.yMax; z += 6f)
                {
                    Vector3 p = worldToLight.MultiplyPoint3x4(new Vector3(x, 0f, z));
                    Vector2 uv = (new Vector2(p.x, p.y) - cookie.center) / cookie.size + new Vector2(0.5f, 0.5f);
                    Assert.That(uv.x, Is.InRange(-1e-4f, 1.0001f), $"{angles}: ground ({x}, {z}) inside the cookie across");
                    Assert.That(uv.y, Is.InRange(-1e-4f, 1.0001f), $"{angles}: ground ({x}, {z}) inside the cookie up");
                }
            }

            // Straight down, the cookie is the ground's own size.
            light.rotation = Quaternion.Euler(90f, 0f, 0f);
            Rect straight = NightLights.LightSpaceRect(NightLights.WorldToLight(light), ground, 0f);
            Assert.That(straight.width, Is.EqualTo(56f).Within(1e-3f));
            Assert.That(straight.height, Is.EqualTo(36f).Within(1e-3f));
        }
        finally
        {
            Object.DestroyImmediate(light.gameObject);
        }
    }

    [Test]
    public void AFootprintsBoundsHoldAllOfItsLight()
    {
        Footprint lamp = Footprint.Round(new Vector2(5f, -3f), 6f, Color.white);
        Assert.That(lamp.Bounds, Is.EqualTo(Rect.MinMaxRect(-1f, -9f, 11f, 3f)));

        float half = Mathf.Sqrt(0.5f);
        Footprint turned = Footprint.Area(Vector2.zero, new Vector2(half, half), new Vector2(1f, 1f), 1f, Color.white, true, 0f, 0f);
        Rect bounds = turned.Bounds;
        Assert.That(bounds.xMax, Is.EqualTo(2f * Mathf.Sqrt(2f)).Within(1e-4f), "a square turned 45 degrees, fade included");
        Assert.That(bounds.yMin, Is.EqualTo(-2f * Mathf.Sqrt(2f)).Within(1e-4f));
    }

    [Test]
    public void CeilingLightsFitTheRoomWallToWall()
    {
        Footprint room = Footprint.Area(Vector2.zero, Vector2.right, new Vector2(10f, 6.5f), 1.6f, Color.white, true, 3.4f, 0.5f);
        Vector2 spacing = room.FittedPoolSpacing;

        Assert.That(20f / spacing.x, Is.EqualTo(Mathf.Round(20f / spacing.x)).Within(1e-4f), "a whole number across");
        Assert.That(13f / spacing.y, Is.EqualTo(Mathf.Round(13f / spacing.y)).Within(1e-4f), "a whole number along");
        Assert.That(spacing.x, Is.InRange(3f, 4f));

        Footprint even = Footprint.Area(Vector2.zero, Vector2.right, Vector2.one, 1f, Color.white, true, 0f, 0f);
        Assert.That(even.FittedPoolSpacing, Is.EqualTo(Vector2.zero), "no ceiling lights asked for, none fitted");
    }

    [Test]
    public void FootprintsEqualWhenUnmovedAndNotWhenMoved()
    {
        Footprint a = Footprint.Round(new Vector2(1f, 2f), 6f, Color.white);
        Footprint nudged = Footprint.Round(new Vector2(1.001f, 2f), 6f, Color.white);
        Footprint moved = Footprint.Round(new Vector2(1.5f, 2f), 6f, Color.white);
        Footprint dimmed = Footprint.Round(new Vector2(1f, 2f), 6f, Color.white * 0.5f);

        Assert.That(a.Equals(nudged), Is.True, "float noise is not a move");
        Assert.That(a.Equals(moved), Is.False);
        Assert.That(a.Equals(dimmed), Is.False);
    }

    [Test]
    public void APlotsYardLightShinesOnlyOnceItIsClaimed()
    {
        var plot = new GameObject("TestPlot");
        var claimed = new GameObject("ShownWhenClaimed");
        try
        {
            claimed.SetActive(false);
            var area = new Rect(5000f, 5000f, 16f, 18f);
            plot.AddComponent<CompoundZone>().Configure(null, area, claimed);
            NightLight light = NightLightsSetup.Yard(plot);

            Assert.That(light.TryGetFootprint(out _), Is.False, "unclaimed land stays dark");

            claimed.SetActive(true);
            Assert.That(light.TryGetFootprint(out Footprint footprint), Is.True, "claimed land is lit");
            Assert.That(footprint.IsFill, Is.True, "lit as one with the compound, not added to it");
            Assert.That(footprint.Centre, Is.EqualTo(area.center));
            Assert.That(footprint.Half, Is.EqualTo(area.size * 0.5f));
        }
        finally
        {
            Object.DestroyImmediate(plot);
            Object.DestroyImmediate(claimed);
        }
    }

    [Test]
    public void ALampsPoolIsUnderIt()
    {
        var lamp = new GameObject("TestLamp");
        try
        {
            lamp.transform.position = new Vector3(25f, 2.8f, -2.6f);
            NightLight light = NightLightsSetup.GateLamp(lamp);

            Assert.That(light.TryGetFootprint(out Footprint footprint), Is.True);
            Assert.That(footprint.IsRound, Is.True);
            Assert.That(footprint.IsFill, Is.False, "lamps add to the yard light");
            Assert.That(footprint.Centre, Is.EqualTo(new Vector2(25f, -2.6f)));
        }
        finally
        {
            Object.DestroyImmediate(lamp);
        }
    }
}
#endif
