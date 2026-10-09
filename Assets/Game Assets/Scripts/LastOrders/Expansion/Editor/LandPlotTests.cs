#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using FoodStreet.Visitors.EditorTools;
using LastOrders.Data;
using LastOrders.EditorTools;
using LastOrders.Expansion;
using NUnit.Framework;
using UnityEngine;
using L = LastOrders.EditorTools.EnvironmentLayout;
using Plot = LastOrders.EditorTools.LandPlotSetup.Plot;
using Side = LastOrders.EditorTools.LandPlotSetup.Side;

/// <summary>
/// The land plots round the compound and the walls between them, without a scene: plots that
/// never overlap and stay out past the defense ring, each reached from inside the compound through
/// a gate straight across the ring from its own, gates on the side they are entered by, trails
/// running between plots rather than across them, pads in the ring off the ways across it - and a
/// wall section that stands only while the land beyond it is not claimed too.
/// </summary>
public class LandPlotTests
{
    static readonly Rect Compound = Rect.MinMaxRect(-9.46f, -2.62f, 45.95f, 35.2f);
    static readonly Rect Courtyard = Rect.MinMaxRect(21.5f, 43.2f, 53.95f, 55.5f);

    const float Ring = PerimeterStripLayout.Ring;

    readonly List<Object> owned = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);

        owned.Clear();
    }

    static List<Plot> Plots() => LandPlotSetup.Layout(Compound, Courtyard);

    static float Overlap(Rect a, Rect b) =>
        Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin)) *
        Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));

    // --- The layout ------------------------------------------------------------------------

    [Test]
    public void PlotsNeverOverlapEachOtherTheCompoundOrTheCourtyard()
    {
        List<Plot> plots = Plots();

        for (int i = 0; i < plots.Count; i++)
        {
            Assert.Less(Overlap(plots[i].Land, Compound), 0.01f, $"{plots[i].Id} overlaps the compound.");
            Assert.Less(Overlap(plots[i].Land, Courtyard), 0.01f, $"{plots[i].Id} overlaps the courtyard.");

            for (int j = i + 1; j < plots.Count; j++)
                Assert.Less(Overlap(plots[i].Land, plots[j].Land), 0.01f, $"{plots[i].Id} overlaps {plots[j].Id}.");
        }
    }

    [Test]
    public void TheDefenseRingStaysClearOfEveryPieceOfLand()
    {
        // The ring all the way round the walls.
        Rect ringOuter = Rect.MinMaxRect(Compound.xMin - Ring + 0.01f, Compound.yMin - Ring + 0.01f, Compound.xMax + Ring - 0.01f,
                                         Compound.yMax + Ring - 0.01f);

        Assert.Less(Overlap(Courtyard, ringOuter), 0.01f, "The courtyard reaches into the defense ring.");
        foreach (Plot plot in Plots())
            Assert.Less(Overlap(plot.Land, ringOuter), 0.01f, $"{plot.Id} reaches into the defense ring.");
    }

    [Test]
    public void TheRoadRunsBeyondTheRingWithNoLandToClaimBetween()
    {
        float kerb = ZoneLayout.RoadCentreZ + ZoneLayout.RoadWidth * 0.5f;
        Assert.LessOrEqual(kerb, Compound.yMin - Ring, "The road runs into the defense ring.");

        // Nothing to claim on the road side: no plot reaches south of the wall's line.
        foreach (Plot plot in Plots())
            Assert.GreaterOrEqual(plot.Land.yMin, Compound.yMin - 0.01f, $"{plot.Id} reaches toward the road.");

        // The cars' drive still reaches from the road to the gate across it.
        Assert.LessOrEqual(ZoneLayout.Drive.yMin, kerb + 0.01f, "The drive stops short of the road.");
        Assert.Greater(ZoneLayout.Drive.yMax, Compound.yMin, "The drive stops short of the gate.");
    }

    [Test]
    public void EveryPlotIsEnteredFromLandAlreadyTheRestaurants()
    {
        List<Plot> plots = Plots();
        var byId = new Dictionary<string, Plot>();
        foreach (Plot plot in plots) byId[plot.Id] = plot;

        foreach (Plot plot in plots)
        {
            // Just across the gate, on the side it is entered by.
            Vector2 before = LandPlotSetup.GatePoint(plot) + LandPlotSetup.Outward(plot.Front) * 0.5f;

            if (plot.UnlockedBy == null)
            {
                // Out over the ring, and straight across it the compound's own gate.
                Vector2 compoundGate = LandPlotSetup.CompoundGatePoint(plot, Compound);
                Vector2 inside = compoundGate - LandPlotSetup.Outward(LandPlotSetup.Opposite(plot.Front)) * 0.5f;

                Assert.IsTrue(Compound.Contains(inside), $"{plot.Id}'s compound gate does not open from the compound.");
                Assert.AreEqual(Ring, Vector2.Distance(compoundGate, LandPlotSetup.GatePoint(plot)), 0.01f,
                                $"{plot.Id}'s gate is not straight across the ring from the compound's.");
                foreach (Plot other in plots)
                    Assert.IsFalse(other.Land.Contains(before), $"{plot.Id}'s gate opens onto {other.Id}, not the ring.");
                continue;
            }

            Assert.IsTrue(byId.ContainsKey(plot.UnlockedBy), $"{plot.Id} opens from a plot that does not exist.");
            Assert.IsTrue(byId[plot.UnlockedBy].Land.Contains(before), $"{plot.Id}'s gate does not open from {plot.UnlockedBy}.");
        }
    }

    [Test]
    public void EveryRouteRunsFromInsideTheCompoundToInsideItsPlot()
    {
        List<Plot> plots = Plots();
        var byId = new Dictionary<string, Plot>();
        foreach (Plot plot in plots) byId[plot.Id] = plot;

        foreach (Plot plot in plots)
        {
            List<Vector2> steps = LandPlotSetup.Route(plot, byId, Compound);

            Assert.IsTrue(Compound.Contains(steps[0]), $"{plot.Id}'s route does not start inside the compound.");
            Assert.IsTrue(plot.Land.Contains(steps[steps.Count - 1]), $"{plot.Id}'s route does not end on its land.");
            Assert.AreEqual(plot.UnlockedBy == null ? 4 : 6, steps.Count, $"{plot.Id}'s route misses a gate.");
        }
    }

    [Test]
    public void RingPadsStandInTheRingOffTheWaysAcrossIt()
    {
        Assert.Greater(PerimeterStripLayout.RingPadOut - PerimeterStripLayout.RingPadSize * 0.5f, PerimeterStripLayout.Depth,
                       "The pads reach back onto the strip.");
        Assert.Less(PerimeterStripLayout.RingPadOut + PerimeterStripLayout.RingPadSize * 0.5f, Ring, "The pads reach onto the plots.");

        // The cars' way in, and the main gate's apron, on the road side.
        float half = PerimeterStripLayout.RingPadSize * 0.5f;
        Rect drive = ZoneLayout.Drive;
        Rect gateway = Rect.MinMaxRect(ZoneLayout.GateCentreX - ZoneLayout.GateWidth * 0.5f - 1f, drive.yMin,
                                       ZoneLayout.GateCentreX + ZoneLayout.GateWidth * 0.5f + 1f, Compound.yMin);

        List<Plot> plots = Plots();
        foreach (PerimeterStripLayout.RingPad pad in PerimeterStripLayout.RingPads)
        {
            var at = new Vector2(pad.Position.x, pad.Position.z);
            var footprint = new Rect(at - Vector2.one * half, Vector2.one * half * 2f);

            Assert.IsFalse(footprint.Overlaps(drive), $"{pad.Id} stands on the drive.");
            Assert.IsFalse(footprint.Overlaps(gateway), $"{pad.Id} stands in front of the main gate.");

            foreach (Plot plot in plots)
            {
                if (plot.UnlockedBy != null) continue;

                // The way across the ring, from the compound's gate to the plot's.
                Vector2 a = LandPlotSetup.CompoundGatePoint(plot, Compound), b = LandPlotSetup.GatePoint(plot);
                Vector2 ab = b - a;
                Vector2 nearest = a + ab * Mathf.Clamp01(Vector2.Dot(at - a, ab) / ab.sqrMagnitude);

                Assert.Greater(Vector2.Distance(at, nearest), PerimeterStripLayout.RingPadSize * 0.5f + 1f,
                               $"{pad.Id} stands on the way out to {plot.Id}.");
            }
        }
    }

    [Test]
    public void GatesStandWellInsideTheSideTheyAreOn()
    {
        foreach (Plot plot in Plots())
        {
            var (_, _, from, to) = LandPlotSetup.SideLine(plot.Land, plot.Front);
            Assert.GreaterOrEqual(plot.Gate, Mathf.Min(from, to) + 2.9f, plot.Id);
            Assert.LessOrEqual(plot.Gate, Mathf.Max(from, to) - 2.9f, plot.Id);
        }
    }

    [Test]
    public void NoTrailRunsThroughTheMiddleOfAPlot()
    {
        List<Plot> plots = Plots();

        foreach (L.Lane lane in L.Lanes)
        {
            Vector3 from = lane.Name.EndsWith("North") ? new Vector3(lane.Spawn.x, 0f, L.ClearMaxZ + 2f) : lane.TrailStart;
            var a = new Vector2(from.x, from.z);
            var b = new Vector2(lane.WallEnd.x, lane.WallEnd.z);

            foreach (Plot plot in plots)
            {
                // A trail along a plot's edge is what the splits are for; one more than a couple of
                // metres in from every edge has the night walking through the obstacles.
                Rect middle = Rect.MinMaxRect(plot.Land.xMin + 2f, plot.Land.yMin + 2f, plot.Land.xMax - 2f, plot.Land.yMax - 2f);
                for (int i = 0; i <= 50; i++)
                    Assert.IsFalse(middle.Contains(Vector2.Lerp(a, b, i / 50f)), $"{lane.Name} runs through the middle of {plot.Id}.");
            }
        }
    }

    [Test]
    public void SidesAreWalkedRoundAnticlockwiseAndOutwardPointsOffTheLand()
    {
        var land = Rect.MinMaxRect(0f, 0f, 10f, 4f);

        foreach (Side side in new[] { Side.South, Side.East, Side.North, Side.West })
        {
            var (alongX, line, from, to) = LandPlotSetup.SideLine(land, side);
            Vector2 middle = alongX ? new Vector2((from + to) * 0.5f, line) : new Vector2(line, (from + to) * 0.5f);

            Assert.IsFalse(land.Contains(middle + LandPlotSetup.Outward(side) * 0.1f), $"{side} points into the land.");
            Assert.IsTrue(land.Contains(middle - LandPlotSetup.Outward(side) * 0.1f), $"{side} points away from the wrong land.");
        }

        Assert.AreEqual(Side.North, LandPlotSetup.Opposite(Side.South));
        Assert.AreEqual(Side.West, LandPlotSetup.Opposite(Side.East));
    }

    // --- The wall between two plots --------------------------------------------------------

    ExpansionAreaDefinition Area(string id)
    {
        var area = ScriptableObject.CreateInstance<ExpansionAreaDefinition>();
        area.id = id;
        owned.Add(area);
        return area;
    }

    /// <summary>A plan of three sections: open ground, the plot next door, open ground.</summary>
    ExpansionBuildPlan Plan(ExpansionAreaDefinition nextDoor, out Transform[] pieces)
    {
        var root = new GameObject("Plan");
        owned.Add(root);

        pieces = new Transform[3];
        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i] = new GameObject($"Wall_{i}").transform;
            pieces[i].SetParent(root.transform, false);
            pieces[i].gameObject.SetActive(false);
        }

        var plan = root.AddComponent<ExpansionBuildPlan>();
        plan.Configure(pieces, new[] { null, nextDoor, null });
        return plan;
    }

    [Test]
    public void ASectionFacingClaimedLandIsNeverRaised()
    {
        ExpansionAreaDefinition nextDoor = Area("next_door");
        var claimed = new HashSet<string> { nextDoor.id };

        ExpansionBuildPlan plan = Plan(nextDoor, out Transform[] pieces);
        plan.SetClaimedTest(claimed.Contains);
        plan.Complete(animate: false, placed: null);

        Assert.IsTrue(pieces[0].gameObject.activeSelf);
        Assert.IsFalse(pieces[1].gameObject.activeSelf, "The wall went up between two claimed plots.");
        Assert.IsTrue(pieces[2].gameObject.activeSelf);
    }

    [Test]
    public void ASectionComesDownWhenTheLandBeyondIsClaimed()
    {
        ExpansionAreaDefinition nextDoor = Area("next_door");
        var claimed = new HashSet<string>();

        ExpansionBuildPlan plan = Plan(nextDoor, out Transform[] pieces);
        plan.SetClaimedTest(claimed.Contains);
        plan.Complete(animate: false, placed: null);
        Assert.IsTrue(pieces[1].gameObject.activeSelf, "Facing unclaimed land, the section should stand.");

        claimed.Add(nextDoor.id);
        plan.Refresh();

        Assert.IsFalse(pieces[1].gameObject.activeSelf, "The wall between two claimed plots stayed up.");
        Assert.IsTrue(pieces[0].gameObject.activeSelf && pieces[2].gameObject.activeSelf, "Sections facing open ground came down.");
    }

    [Test]
    public void ABuildSkipsSectionsFacingClaimedLandWithoutPlacingThem()
    {
        ExpansionAreaDefinition nextDoor = Area("next_door");
        var claimed = new HashSet<string> { nextDoor.id };

        ExpansionBuildPlan plan = Plan(nextDoor, out Transform[] pieces);
        plan.SetClaimedTest(claimed.Contains);

        int placed = 0;
        plan.SetProgress(1f, _ => placed++);

        Assert.AreEqual(2, placed, "Dust was played for a section that never went up.");
        Assert.IsFalse(pieces[1].gameObject.activeSelf);
    }

    [Test]
    public void APlanWithNoClaimedTestRaisesEverything()
    {
        ExpansionBuildPlan plan = Plan(Area("next_door"), out Transform[] pieces);
        plan.Complete(animate: false, placed: null);

        foreach (Transform piece in pieces) Assert.IsTrue(piece.gameObject.activeSelf);
    }

    [Test]
    public void AnAreaWithNoShortNameIsTheYard()
    {
        ExpansionAreaDefinition area = Area("courtyard");
        Assert.AreEqual("Yard", area.WallLabel);

        area.shortName = "West lot";
        Assert.AreEqual("West lot", area.WallLabel);
    }
}
#endif
