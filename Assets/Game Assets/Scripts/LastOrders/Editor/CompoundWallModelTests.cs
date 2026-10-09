#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using LastOrders.EditorTools;
using NUnit.Framework;
using UnityEngine;
using End = LastOrders.EditorTools.CompoundWallModels.End;
using Patch = LastOrders.EditorTools.CompoundWallModels.Patch;
using State = LastOrders.EditorTools.CompoundWallModels.State;

/// <summary>
/// The modelled compound wall and its gates, built in memory: nothing on a stretch of wall that a
/// bastion raised over it would not hide, gate leaves that disappear into the wall as they slide
/// open, patches and dressing spread along a wall rather than bunched on one stretch, and a section
/// turned round to face out with its ends swapped.
/// </summary>
public class CompoundWallModelTests
{
    /// <summary>How far the smallest bastion reaches either side of the wall's line: a wall site's, 0.9 m in.</summary>
    const float BastionReach = 0.9f;

    /// <summary>The wall's slabs: a leaf slid in among them is out of sight below their top and inside their faces.</summary>
    const float SlabTop = 1.95f, SlabHalfDepth = 0.18f;

    GameObject root;

    [SetUp]
    public void SetUp()
    {
        CompoundWallModels.InMemory = true;
        CompoundWallModels.Begin();
        root = new GameObject("WallModelTest");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        CompoundWallModels.Begin();
        CompoundWallModels.InMemory = false;
    }

    static IEnumerable<CompoundWallModels.Section> Sections()
    {
        foreach (float length in new[] { 1.5f, 2.46f, 4.21f, 5f })
        foreach (Patch patch in new[] { Patch.None, Patch.NewSlab, Patch.Boarded, Patch.Sheathed })
        foreach (int dressing in new[] { 0, 1, 2 })
            yield return new CompoundWallModels.Section { Length = length, Lo = End.Post, Hi = End.Post, Patch = patch, Dressing = dressing };
    }

    [Test]
    public void EveryPartOfTheWallFitsUnderABastion()
    {
        foreach (State state in new[] { State.Intact, State.Damaged, State.Rubble })
        {
            foreach (CompoundWallModels.Section section in Sections())
            {
                var group = new GameObject($"{state}").transform;
                group.SetParent(root.transform, false);
                CompoundWallModels.Build(group, section, state, new Material[0]);

                foreach (MeshFilter filter in group.GetComponentsInChildren<MeshFilter>())
                {
                    Bounds b = filter.sharedMesh.bounds;
                    for (int k = 0; k < 8; k++)
                    {
                        var corner = new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z);
                        Vector3 p = filter.transform.localToWorldMatrix.MultiplyPoint3x4(corner);
                        string what = $"{state} {filter.sharedMesh.name} in a {section.Length} m section";

                        Assert.That(p.y, Is.LessThanOrEqualTo(CompoundWallSceneSetup.BastionHeight), $"{what} stands up through a bastion's deck");
                        Assert.That(Mathf.Abs(p.z), Is.LessThanOrEqualTo(BastionReach), $"{what} reaches out past a bastion's face");
                    }
                }
            }
        }
    }

    [Test]
    public void GateLeavesDisappearIntoTheWall()
    {
        foreach (float width in new[] { 1.49f, 4.99f })
        {
            foreach (bool left in new[] { true, false })
            {
                Bounds b = CompoundWallModels.GateLeaf(width, left).bounds;
                Assert.That(b.max.y, Is.LessThan(SlabTop), $"a {width} m leaf shows over the wall it slides into");
                Assert.That(Mathf.Max(b.max.z, -b.min.z), Is.LessThan(SlabHalfDepth), $"a {width} m leaf shows through the faces of the wall it slides into");
                Assert.That(b.size.x, Is.EqualTo(width).Within(0.01f), $"a {width} m leaf is not as wide as its half of the gate");
            }
        }
    }

    [Test]
    public void PatchesAndDressingAreSpreadAlongAWall()
    {
        var dressings = new int[3];
        var patches = new int[4];

        // Keyed as the wall setup keys them: by where each section stands, in decimetres - here forty
        // sections 4.2 m apart along a wall at z 35.2.
        for (int i = 0; i < 40; i++)
        {
            string name = $"{Mathf.RoundToInt((i * 4.2f - 9.5f) * 10f)},352";
            CompoundWallModels.Section s = CompoundWallModels.SectionFor(name, 4.2f, End.Post, End.Open, false);
            CompoundWallModels.Section again = CompoundWallModels.SectionFor(name, 4.2f, End.Post, End.Open, false);

            Assert.That(again.Patch, Is.EqualTo(s.Patch), $"{name} is patched differently from one build to the next");
            Assert.That(again.Dressing, Is.EqualTo(s.Dressing), $"{name} is dressed differently from one build to the next");
            Assert.That(again.PatchBay, Is.EqualTo(s.PatchBay));

            dressings[s.Dressing]++;
            patches[(int)s.Patch]++;
        }

        Assert.That(dressings[1], Is.GreaterThan(0), "no shore anywhere along forty sections");
        Assert.That(dressings[2], Is.GreaterThan(0), "no sandbags anywhere along forty sections");
        Assert.That(dressings[0], Is.GreaterThan(20), "most of the wall should stand on its own");

        foreach (Patch patch in new[] { Patch.NewSlab, Patch.Boarded, Patch.Sheathed })
            Assert.That(patches[(int)patch], Is.GreaterThan(0), $"no {patch} section anywhere along forty sections");
        Assert.That(patches[(int)Patch.None], Is.GreaterThan(10), "the wall should be mostly unpatched");
    }

    [Test]
    public void ATurnedSectionHasItsEndsSwapped()
    {
        CompoundWallModels.Section facing = CompoundWallModels.SectionFor("126,-26", 4.2f, End.Gate, End.Open, false);
        CompoundWallModels.Section turned = CompoundWallModels.SectionFor("126,-26", 4.2f, End.Gate, End.Open, true);

        Assert.That(facing.Lo, Is.EqualTo(End.Gate));
        Assert.That(facing.Hi, Is.EqualTo(End.Open));
        Assert.That(turned.Lo, Is.EqualTo(End.Open), "turned half round, the section's start is at its mesh's +X end");
        Assert.That(turned.Hi, Is.EqualTo(End.Gate));
        Assert.That(turned.Patch, Is.EqualTo(facing.Patch), "turning a section round must not change how it is made");
    }
}
#endif
