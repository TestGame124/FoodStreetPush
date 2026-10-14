#if UNITY_INCLUDE_TESTS
using LastOrders.Restaurant;
using NUnit.Framework;

/// <summary>
/// The diner's swing doors: which way they open for someone at the doorway - away from them, into
/// the room for whoever comes from the yard and out for whoever leaves - and that they stay shut for
/// anyone off to the side, too far off, or walking away.
/// </summary>
public class SwingDoorsTests
{
    const float Span = 1.4f;
    const float Reach = 1.8f;
    const float InDoorway = 0.8f;

    static int Side(float across, float through, float speedIn) =>
        SwingDoors.SideToOpen(across, through, speedIn, Span, Reach, InDoorway);

    [Test]
    public void SomeoneComingInFromTheYardOpensThemIntoTheRoom()
    {
        Assert.AreEqual(1, Side(0.1f, -1.5f, 1f));
    }

    [Test]
    public void SomeoneLeavingOpensThemOutToTheYard()
    {
        Assert.AreEqual(-1, Side(-0.2f, 1.2f, -1f));
    }

    [Test]
    public void SomeoneRightOnTheLineGoesTheWayTheyAreWalking()
    {
        Assert.AreEqual(1, Side(0f, 0.05f, 0.9f));
        Assert.AreEqual(-1, Side(0f, -0.05f, -0.9f));
    }

    [Test]
    public void SomeoneInTheDoorwayKeepsThemOpenEvenStandingStill()
    {
        Assert.AreEqual(1, Side(0.3f, -0.5f, 0f));
        Assert.AreEqual(-1, Side(0.3f, 0.5f, 0f));
    }

    [Test]
    public void WalkingAwayStandingAsideOrFarOffLeavesThemShut()
    {
        Assert.AreEqual(0, Side(0f, -1.5f, -0.8f), "Walking away into the yard.");
        Assert.AreEqual(0, Side(0f, 1.5f, 0.8f), "Walking away into the room.");
        Assert.AreEqual(0, Side(2f, -0.5f, 1f), "Off to the side of the doorway.");
        Assert.AreEqual(0, Side(0f, -3f, 1f), "Too far off yet.");
        Assert.AreEqual(0, Side(0f, -1.5f, 0f), "Standing about outside.");
    }
}
#endif
