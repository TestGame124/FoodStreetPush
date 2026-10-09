#if UNITY_INCLUDE_TESTS
using FoodStreet.Placement;
using NUnit.Framework;
using UnityEngine;

public class TileMaskTests
{
    readonly TileJoint[] joints = new TileJoint[4];

    [Test] public void EveryNeighbourhoodDrawsLikeItsCanonicalForm()
    {
        var expected = new TileJoint[4];
        for (int mask = 0; mask < 256; mask++)
        {
            int canonical = TileMask.Canonical(mask);
            for (int quarter = 0; quarter < TileMask.Quarters; quarter++)
                Assert.AreEqual(TileMask.Shape(mask, quarter), TileMask.Shape(canonical, quarter), $"mask {mask:X2} quarter {quarter}");

            int count = TileMask.Joints(mask, expected);
            Assert.AreEqual(count, TileMask.Joints(canonical, joints), $"mask {mask:X2} joints");
            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(expected[i].East, joints[i].East, $"mask {mask:X2} joint {i}");
                Assert.AreEqual(expected[i].Side, joints[i].Side, $"mask {mask:X2} joint {i}");
            }
        }
    }

    [Test] public void LoneCellIsFourCornersWithNoSeams()
    {
        for (int quarter = 0; quarter < TileMask.Quarters; quarter++)
            Assert.AreEqual(QuarterShape.Corner, TileMask.Shape(0, quarter));
        Assert.AreEqual(0, TileMask.Joints(0, joints));
    }

    [Test] public void SurroundedCellIsOpenSoil()
    {
        for (int quarter = 0; quarter < TileMask.Quarters; quarter++)
            Assert.AreEqual(QuarterShape.Open, TileMask.Shape(0xFF, quarter));
        Assert.AreEqual(0, TileMask.Joints(0xFF, joints));
    }

    [Test] public void BordersRunOnIntoAnEastNeighbourWithAPostAtEachSeam()
    {
        int mask = TileMask.E;
        Assert.AreEqual(QuarterShape.BorderZ, TileMask.Shape(mask, TileMask.QuarterNE));
        Assert.AreEqual(QuarterShape.BorderZ, TileMask.Shape(mask, TileMask.QuarterSE));
        Assert.AreEqual(QuarterShape.Corner, TileMask.Shape(mask, TileMask.QuarterSW));
        Assert.AreEqual(QuarterShape.Corner, TileMask.Shape(mask, TileMask.QuarterNW));

        Assert.AreEqual(2, TileMask.Joints(mask, joints));
        Assert.IsTrue(joints[0].East && joints[1].East);
        Assert.AreEqual(new Vector3(0.5f, 0f, 0.4f), joints[0].Position(0.5f, 0.4f));
        Assert.AreEqual(new Vector3(0.5f, 0f, -0.4f), joints[1].Position(0.5f, 0.4f));
    }

    [Test] public void WestAndSouthSeamsAreLeftToTheNeighbourThere()
    {
        Assert.AreEqual(0, TileMask.Joints(TileMask.W, joints));
        Assert.AreEqual(0, TileMask.Joints(TileMask.S, joints));
        Assert.AreEqual(2, TileMask.Joints(TileMask.N, joints));
    }

    [Test] public void NoSeamPostWhereTheNeighbourTurnsAnInsideCorner()
    {
        // East and north-east joined, north not: the east pot turns an inside corner round the
        // empty north cell, so the north border does not run on straight and has no seam post.
        int mask = TileMask.E | TileMask.NE;
        Assert.AreEqual(1, TileMask.Joints(mask, joints));
        Assert.IsTrue(joints[0].East);
        Assert.AreEqual(-1, joints[0].Side);
    }

    [Test] public void MissingCornerNeighbourMakesAnInsideCorner()
    {
        int mask = TileMask.N | TileMask.E;
        Assert.AreEqual(QuarterShape.InsideCorner, TileMask.Shape(mask, TileMask.QuarterNE));
        Assert.AreEqual(QuarterShape.Open, TileMask.Shape(mask | TileMask.NE, TileMask.QuarterNE));
        Assert.AreEqual(QuarterShape.BorderX, TileMask.Shape(mask, TileMask.QuarterNW));
        Assert.AreEqual(QuarterShape.BorderZ, TileMask.Shape(mask, TileMask.QuarterSE));
    }

    [Test] public void CombinedMeshIsOnePiecePerQuarterPlusItsSeamPosts()
    {
        var set = ScriptableObject.CreateInstance<AutoTileSet>();
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh piece = cube.GetComponent<MeshFilter>().sharedMesh;

        try
        {
            var pieces = new Mesh[AutoTileSet.PieceCount];
            for (int i = 0; i < pieces.Length; i++) pieces[i] = piece;
            set.Configure(pieces, piece, 0.5f, 0.4f);

            Assert.AreEqual(piece.vertexCount * 4, set.MeshFor(0).vertexCount);
            Assert.AreEqual(piece.vertexCount * 6, set.MeshFor(TileMask.E).vertexCount);
            Assert.AreSame(set.MeshFor(TileMask.E), set.MeshFor(TileMask.E | TileMask.SW), "SW cannot change this cell's drawing");
            Assert.AreNotSame(set.MeshFor(TileMask.E), set.MeshFor(TileMask.E | TileMask.NE), "NE removes a seam post");
        }
        finally
        {
            set.Forget();
            Object.DestroyImmediate(set);
            Object.DestroyImmediate(cube);
        }
    }
}
#endif
