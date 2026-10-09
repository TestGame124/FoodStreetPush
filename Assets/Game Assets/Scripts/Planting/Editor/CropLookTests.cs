#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public class CropLookTests
{
    CropLook look;
    Sprite[][] stages;
    Texture2D texture;

    [SetUp] public void SetUp()
    {
        texture = new Texture2D(4, 4);
        stages = new Sprite[4][];
        for (int s = 0; s < stages.Length; s++)
        {
            stages[s] = new Sprite[3];
            for (int v = 0; v < 3; v++)
            {
                stages[s][v] = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero);
                stages[s][v].name = $"stage {s} variant {v}";
            }
        }

        look = ScriptableObject.CreateInstance<CropLook>();
        look.Configure(stages, new[] { new Vector2(-0.25f, 0.3f), Vector2.zero, new Vector2(0.25f, -0.3f) }, 0.1f, 0.02f);
    }

    [TearDown] public void TearDown()
    {
        foreach (Sprite[] stage in stages)
            foreach (Sprite sprite in stage) Object.DestroyImmediate(sprite);
        Object.DestroyImmediate(look);
        Object.DestroyImmediate(texture);
    }

    static int VariantOf(Sprite sprite) => sprite.name[sprite.name.Length - 1] - '0';

    [Test] public void EachPlantKeepsItsVariantAsItGrows()
    {
        for (int seed = 0; seed < 50; seed++)
            for (int spot = 0; spot < look.SpotCount; spot++)
            {
                int first = VariantOf(look.SpriteFor(0, spot, seed));
                for (int stage = 1; stage < look.StageCount; stage++)
                    Assert.AreEqual(first, VariantOf(look.SpriteFor(stage, spot, seed)), $"seed {seed} spot {spot} stage {stage}");
            }
    }

    [Test] public void APotLooksTheSameEveryTimeItIsPlanted()
    {
        for (int spot = 0; spot < look.SpotCount; spot++)
        {
            Assert.AreSame(look.SpriteFor(2, spot, 1234), look.SpriteFor(2, spot, 1234));
            Assert.AreEqual(look.SizeFor(spot, 1234), look.SizeFor(spot, 1234));
            Assert.AreEqual(look.PositionFor(spot, 1234), look.PositionFor(spot, 1234));
        }
    }

    [Test] public void SizesAndPositionsStayWithinTheirJitter()
    {
        for (int seed = 0; seed < 200; seed++)
            for (int spot = 0; spot < look.SpotCount; spot++)
            {
                float size = look.SizeFor(spot, seed);
                Assert.That(size, Is.InRange(0.9f, 1.1f));

                Vector2 offset = look.PositionFor(spot, seed) - look.Spots[spot];
                Assert.That(Mathf.Abs(offset.x), Is.LessThanOrEqualTo(0.02f + 1e-5f));
                Assert.That(Mathf.Abs(offset.y), Is.LessThanOrEqualTo(0.02f + 1e-5f));
            }
    }

    [Test] public void NeighbouringPotsDoNotAllLookAlike()
    {
        var seen = new System.Collections.Generic.HashSet<int>();
        for (int seed = 0; seed < 20; seed++) seen.Add(VariantOf(look.SpriteFor(0, 0, seed)));
        Assert.AreEqual(3, seen.Count, "every variant turns up");
    }

    [Test] public void StagesBeyondTheLastShowTheRipeCrop()
    {
        Assert.AreEqual(3, look.SpriteFor(99, 0, 7).name[6] - '0');
        Assert.AreEqual(0, look.SpriteFor(-5, 0, 7).name[6] - '0');
    }
}
#endif
