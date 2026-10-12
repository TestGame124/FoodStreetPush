#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using FoodStreet.Visitors.EditorTools;
using LastOrders.Core;
using LastOrders.EditorTools;
using LastOrders.Night;
using NUnit.Framework;
using UnityEngine;
using L = LastOrders.EditorTools.EnvironmentLayout;

/// <summary>
/// The scatter that places everything at the world edge: spacing held, the same seed giving the
/// same layout, keep-outs kept out of, and a region cut off from the rest still filled.
/// </summary>
public class PoissonScatterTests
{
    static readonly Rect Area = new Rect(0f, 0f, 60f, 40f);

    [Test]
    public void ConstantSpacingIsHeld()
    {
        var rng = new DeterministicRandom(1);
        List<Vector2> points = PoissonScatter.Sample(Area, 3f, 3f, null, null, ref rng);

        Assert.That(points.Count, Is.GreaterThan(80));
        for (int i = 0; i < points.Count; i++)
            for (int j = i + 1; j < points.Count; j++)
                Assert.That(Vector2.Distance(points[i], points[j]), Is.GreaterThanOrEqualTo(3f - 1e-3f));
    }

    [Test]
    public void VaryingSpacingKeepsTheMeanOfEachPair()
    {
        float SpacingAt(Vector2 p) => Mathf.Lerp(2f, 6f, p.x / Area.width);

        var rng = new DeterministicRandom(2);
        List<Vector2> points = PoissonScatter.Sample(Area, 2f, 6f, SpacingAt, null, ref rng);

        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                float needed = (SpacingAt(points[i]) + SpacingAt(points[j])) * 0.5f;
                Assert.That(Vector2.Distance(points[i], points[j]), Is.GreaterThanOrEqualTo(needed - 1e-3f));
            }
        }
    }

    [Test]
    public void SameSeedGivesTheSameLayout()
    {
        var a = new DeterministicRandom(42);
        var b = new DeterministicRandom(42);

        List<Vector2> first = PoissonScatter.Sample(Area, 2.5f, 5f, p => 2.5f + p.y * 0.05f, null, ref a);
        List<Vector2> second = PoissonScatter.Sample(Area, 2.5f, 5f, p => 2.5f + p.y * 0.05f, null, ref b);

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void KeepOutIsKeptOutOf()
    {
        var hole = new Rect(20f, 10f, 15f, 12f);
        var rng = new DeterministicRandom(3);

        List<Vector2> points = PoissonScatter.Sample(Area, 2f, 2f, null, p => !hole.Contains(p), ref rng);

        Assert.That(points, Is.Not.Empty);
        foreach (Vector2 p in points) Assert.That(hole.Contains(p), Is.False, $"{p} is inside the keep-out");
    }

    /// <summary>A wide strip down the middle, wider than any hop: both sides still get filled.</summary>
    [Test]
    public void BothSidesOfAWideGapAreFilled()
    {
        var strip = new Rect(25f, 0f, 10f, 40f);
        int emptySides = 0;

        for (int seed = 0; seed < 20; seed++)
        {
            var rng = new DeterministicRandom(seed);
            List<Vector2> points = PoissonScatter.Sample(Area, 3f, 3f, null, p => !strip.Contains(p), ref rng);

            bool left = points.Exists(p => p.x < strip.xMin);
            bool right = points.Exists(p => p.x > strip.xMax);
            if (!left || !right) emptySides++;
        }

        Assert.That(emptySides, Is.Zero);
    }
}

/// <summary>
/// The mesh builder the houses and roofs are made with: faces turned outward whatever order their
/// corners came in, roofs peaking where they should, and empty parts left out of the mesh.
/// </summary>
public class EnvMeshTests
{
    [Test]
    public void EveryBoxFaceLooksAwayFromTheMiddle()
    {
        var builder = new EnvMesh(1);
        var centre = new Vector3(3f, 1f, -2f);
        builder.Box(0, centre, new Vector3(2f, 1f, 4f), Quaternion.Euler(0f, 30f, 0f), bottom: true);

        Mesh mesh = builder.Build("Box", out _);
        int[] tris = mesh.triangles;
        Vector3[] v = mesh.vertices;

        Assert.That(tris.Length / 3, Is.EqualTo(12));
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
            Vector3 facing = Vector3.Cross(b - a, c - a);
            Vector3 fromMiddle = (a + b + c) / 3f - centre;
            Assert.That(Vector3.Dot(facing, fromMiddle), Is.GreaterThan(0f), $"triangle {i / 3} faces inward");
        }

        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void AGableRoofPeaksAtItsRiseAndOverhangsItsWalls()
    {
        var builder = new EnvMesh(3);
        builder.GableRoof(new EnvMesh.RoofParts { Covering = 0, Gable = 1, Trim = 2 }, Vector3.zero, Quaternion.identity,
                          8f, 6f, 2f, 0.4f);

        Mesh mesh = builder.Build("Roof", out List<int> used);
        Bounds b = mesh.bounds;

        Assert.That(used, Is.EqualTo(new List<int> { 0, 1, 2 }));
        Assert.That(b.max.y, Is.GreaterThanOrEqualTo(2f).And.LessThan(2.2f), "ridge height");
        Assert.That(b.extents.z, Is.EqualTo(3.4f).Within(0.01f), "eaves overhang the walls");

        // The slopes face up and outward, not into the house.
        Vector3[] n = mesh.normals;
        foreach (int index in mesh.GetTriangles(0))
            Assert.That(n[index].y, Is.GreaterThan(0.3f));

        Object.DestroyImmediate(mesh);
    }

    /// <summary>A pyramid roof's sides are quads with two corners on the apex; they still need a normal.</summary>
    [Test]
    public void APyramidRoofHasNoBlackFaces()
    {
        var builder = new EnvMesh(2);
        builder.HipRoof(new EnvMesh.RoofParts { Covering = 0, Gable = 1, Trim = 1 }, Vector3.zero, Quaternion.identity,
                        2.4f, 2.4f, 0.8f, 0.4f);

        Mesh mesh = builder.Build("Pyramid", out _);
        foreach (int index in mesh.GetTriangles(0))
            Assert.That(mesh.normals[index].y, Is.GreaterThan(0.3f), "a roof face with no usable normal renders black");

        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void PartsWithNothingInThemAreLeftOut()
    {
        var builder = new EnvMesh(4);
        builder.Box(2, Vector3.zero, Vector3.one, Quaternion.identity);

        Mesh mesh = builder.Build("One", out List<int> used);

        Assert.That(mesh.subMeshCount, Is.EqualTo(1));
        Assert.That(used, Is.EqualTo(new List<int> { 2 }));
        Object.DestroyImmediate(mesh);
    }
}

/// <summary>
/// The world-edge layout's promises to the rest of the level: every lane spawns off the nav grid,
/// inside the mist and square with its wall; the town's wanderers are on streets and off the road;
/// the customers set out from behind the checkpoint, deep in the fog; the camera never pans past
/// the edge.
/// </summary>
public class EnvironmentLayoutTests
{
    [Test]
    public void LanesSpawnOnTheClearGroundOutsideTheCompound()
    {
        Rect compound = NeighborhoodBuilder.Inflate(L.Compound, 5f);

        foreach (L.Lane lane in L.Lanes)
        {
            var spawn = new Vector2(lane.Spawn.x, lane.Spawn.z);
            Assert.That(compound.Contains(spawn), Is.False, $"{lane.Name} spawns next to the walls");
            Assert.That(L.Clear.Contains(spawn), Is.True, $"{lane.Name} spawns out in the band");
        }
    }

    [Test]
    public void LanesSpawnOffTheNavGridAndWalkStraightToTheirWall()
    {
        foreach (L.Lane lane in L.Lanes)
        {
            var spawn = new Vector2(lane.Spawn.x, lane.Spawn.z);
            Assert.That(L.NavGrid.Contains(spawn), Is.False, $"{lane.Name} spawns on the nav grid");

            bool square = Mathf.Abs(lane.Spawn.x - lane.WallEnd.x) < 0.01f || Mathf.Abs(lane.Spawn.z - lane.WallEnd.z) < 0.01f;
            Assert.That(square, Is.True, $"{lane.Name} would walk in on a slant, off its trail");
        }
    }

    /// <summary>
    /// Just through its breach, inside the fog's first haze: far enough that a wave arrives in time
    /// (zombies walk about a metre a second, a wave is given 75 s before the next, and 30 s more after the last).
    /// </summary>
    [Test]
    public void LanesSpawnJustInsideTheFenceNotTooFarToArrive()
    {
        foreach (L.Lane lane in L.Lanes)
        {
            var spawn = new Vector2(lane.Spawn.x, lane.Spawn.z);
            Assert.That(L.Fence.Contains(spawn), Is.True, $"{lane.Name} spawns outside the old fence");
            Assert.That(-L.Outside(spawn), Is.InRange(L.FenceInset, L.FenceInset + 8f), $"{lane.Name} spawns too far in from the fence");

            float dx = Mathf.Max(L.Compound.xMin - spawn.x, 0f, spawn.x - L.Compound.xMax);
            float dz = Mathf.Max(L.Compound.yMin - spawn.y, 0f, spawn.y - L.Compound.yMax);
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            Assert.That(distance, Is.InRange(34f, 62f), $"{lane.Name} spawns {distance:0.0} m from the walls");
        }
    }

    [Test]
    public void LanesAreInTierOrderStartingAtTheFront()
    {
        Assert.That(L.Lanes.Length, Is.EqualTo(6));
        Assert.That(L.Lanes.Length, Is.LessThanOrEqualTo(EdgeFogBuilder.MaxTongues), "a lane the fog cannot lay a tongue down");
        Assert.That(L.Lanes[0].Spawn.z, Is.LessThan(L.Compound.yMin), "lane 0 is the front, across the road");

        var sides = new[]
        {
            OutskirtsBuilder.Sector.West, OutskirtsBuilder.Sector.East, OutskirtsBuilder.Sector.North,
            OutskirtsBuilder.Sector.West, OutskirtsBuilder.Sector.East,
        };

        for (int i = 1; i < L.Lanes.Length; i++)
            Assert.That(OutskirtsBuilder.SectorOf(new Vector2(L.Lanes[i].Spawn.x, L.Lanes[i].Spawn.z)), Is.EqualTo(sides[i - 1]), L.Lanes[i].Name);

        // The two past the compound's north corners come in north of its wall, into the ring's corners.
        Assert.That(L.Lanes[4].WallEnd.z, Is.GreaterThan(L.Compound.yMax));
        Assert.That(L.Lanes[5].WallEnd.z, Is.GreaterThan(L.Compound.yMax));
        Assert.That(L.Lanes[4].WallEnd.z, Is.LessThan(PerimeterStripLayout.RingBounds.yMax));
    }

    [Test]
    public void EveryLaneComesThroughABreachInTheFenceAndTheRoadThroughAGap()
    {
        Rect fence = L.Fence;
        L.Breach[] breaches = L.Breaches;

        foreach (L.Lane lane in L.Lanes)
        {
            // Where its trail crosses the fence line.
            var a = new Vector2(lane.TrailStart.x, lane.TrailStart.z);
            var b = new Vector2(lane.WallEnd.x, lane.WallEnd.z);
            Assert.That(fence.Contains(a), Is.False, $"{lane.Name}'s trail starts inside the fence");
            Assert.That(fence.Contains(b), Is.True, $"{lane.Name}'s trail never comes in through the fence");

            Vector2 crossing = a;
            for (int i = 0; i <= 400 && !fence.Contains(crossing); i++) crossing = Vector2.Lerp(a, b, i / 400f);

            bool through = false;
            foreach (L.Breach breach in breaches)
                if (breach.Lane && Vector2.Distance(breach.At, crossing) < breach.HalfWidth - L.TrailHalfWidth) through = true;

            Assert.That(through, Is.True, $"{lane.Name}'s trail runs into the fence rather than through a breach");
        }

        foreach (float x in new[] { fence.xMin, fence.xMax })
        {
            var at = new Vector2(x, ZoneLayout.RoadCentreZ);
            bool gap = false;
            foreach (L.Breach breach in breaches)
                if (!breach.Lane && Vector2.Distance(breach.At, at) < 0.01f && breach.HalfWidth > ZoneLayout.RoadWidth * 0.5f + 1f) gap = true;

            Assert.That(gap, Is.True, $"the road runs into the fence at x {x:0}");
        }
    }


    [Test]
    public void TownPatchesLieOnStreetsAndOffTheRoad()
    {
        foreach (TownZombies.Area patch in L.TownPatches)
        {
            var rect = new Rect(new Vector2(patch.centre.x, patch.centre.z) - patch.size * 0.5f, patch.size);

            bool onStreet = false;
            foreach (L.Street street in L.Streets)
            {
                if (street.IsMainRoad) continue;
                Rect s = NeighborhoodBuilder.Inflate(street.Surface, 0.5f);
                if (s.Contains(rect.min) && s.Contains(rect.max)) onStreet = true;
            }

            Assert.That(onStreet, Is.True, $"{patch.name} is not on a street, so its zombies walk through houses");
            Assert.That(rect.Overlaps(ZoneLayout.RoadCorridor), Is.False, $"{patch.name} is on the customers' road");
            Assert.That(rect.yMax, Is.LessThan(L.ClearMaxZ), $"{patch.name} is in the trees");
        }
    }

    [Test]
    public void CustomersSetOutFromBehindTheCheckpointInsideTheFog()
    {
        Assert.That(L.Checkpoint.z, Is.EqualTo(ZoneLayout.RoadCentreZ).Within(0.01f));
        Assert.That(L.Checkpoint.x, Is.LessThan(L.ClearMinX), "the checkpoint is out in the west suburbs");
        Assert.That(ZoneLayout.RoadStart.x, Is.LessThan(L.Checkpoint.x), "cars start behind the checkpoint");

        foreach (Vector3 end in new[] { ZoneLayout.RoadStart, ZoneLayout.RoadEnd })
            Assert.That(L.Outside(new Vector2(end.x, end.z)), Is.GreaterThan(L.ContentDepth), $"{end} is where the fog is thin");
    }

    [Test]
    public void TheCheckpointLeavesTheAvenueCrossroadsOpen()
    {
        foreach (L.Street street in L.Streets)
        {
            if (street.IsMainRoad) continue;
            Assert.That(street.Surface.Overlaps(L.CheckpointZone), Is.False, $"{street.Name} runs through the checkpoint");
        }
    }

    [Test]
    public void TheCameraCanLookIntoTheBandsButNotPastThem()
    {
        Rect bounds = L.CameraBounds;
        Assert.That(bounds.xMin, Is.LessThan(L.ClearMinX));
        Assert.That(bounds.xMax, Is.GreaterThan(L.ClearMaxX));
        Assert.That(bounds.yMin, Is.LessThan(L.ClearMinZ));
        Assert.That(bounds.yMax, Is.GreaterThan(L.ClearMaxZ));

        foreach (Vector2 corner in new[] { bounds.min, bounds.max, new Vector2(bounds.xMin, bounds.yMax), new Vector2(bounds.xMax, bounds.yMin) })
            Assert.That(L.Outside(corner), Is.LessThan(L.ContentDepth * 0.6f), $"the pivot can reach {corner}, deep in the fog");
    }

    [Test]
    public void StreetsAreStraightAndFogRisesToASolidTop()
    {
        foreach (L.Street street in L.Streets)
        {
            bool straight = Mathf.Abs(street.From.x - street.To.x) < 0.01f || Mathf.Abs(street.From.y - street.To.y) < 0.01f;
            Assert.That(straight, Is.True, $"{street.Name} is not axis-aligned");
        }

        for (int i = 1; i < L.FogLayers.Length; i++)
            Assert.That(L.FogLayers[i].Height, Is.GreaterThan(L.FogLayers[i - 1].Height));

        Assert.That(L.FogLayers[L.FogLayers.Length - 1].Solid, Is.EqualTo(1f), "nothing closes off the far edge");
    }
}

/// <summary>
/// Moving a scene between the looks: every palette entry has its own colour in each look, so a
/// move can be undone, and the wall's timber and tin patches fall on the same sections every time.
/// </summary>
public class PaintedLookTests
{
    [Test]
    public void EveryColourHasItsOwnColourInEachLook()
    {
        var painted = new HashSet<string>();
        var current = new HashSet<string>();
        foreach (var (muted, before, now) in PaintedLookSetup.Palettes)
        {
            Assert.That(now.Length, Is.EqualTo(muted.Length), "a palette gained or lost an entry; its muted colours no longer line up");
            Assert.That(now.Length, Is.EqualTo(before.Length), "a palette gained or lost an entry; its painted colours no longer line up");

            foreach (string c in before)
                Assert.That(painted.Add(c), Is.True, $"two colours both become {c} in the painted look, so moving back cannot tell them apart");
            foreach (Color c in now)
                Assert.That(current.Add(ColorUtility.ToHtmlStringRGB(c)), Is.True,
                            $"two colours both become {ColorUtility.ToHtmlStringRGB(c)}, so moving back cannot tell them apart");
        }
    }

    [Test]
    public void WallPanelsAreTheSameEveryTimeAndMostlyConcrete()
    {
        int concrete = 0;
        for (int i = 0; i < 200; i++)
        {
            string name = $"North_wall_{i:00}";
            TextureGenerator.WallPanel panel = CompoundWallSceneSetup.PanelFor(name);
            Assert.That(CompoundWallSceneSetup.PanelFor(name), Is.EqualTo(panel));
            if (panel == TextureGenerator.WallPanel.Concrete) concrete++;
        }

        Assert.That(concrete, Is.InRange(110, 150), "the wall should be mostly concrete with patches, not the other way round");
    }
}
#endif
