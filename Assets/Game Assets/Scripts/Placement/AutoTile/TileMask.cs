using UnityEngine;

namespace FoodStreet.Placement
{
    /// <summary>
    /// Which of a cell's eight neighbours belong to the same tiled piece, and what each quarter of
    /// the cell should therefore look like.
    ///
    /// A tile is drawn as four quarters round its centre. Each quarter only cares about the two
    /// sides it touches and the corner between them, so it needs five shapes and no more: a closed
    /// corner, a straight border on one side or the other, the small notch of an inside corner, or
    /// nothing at all. Every one of the 256 neighbourhoods is a choice of four of those.
    /// </summary>
    public static class TileMask
    {
        // One bit per neighbour, clockwise from north.
        public const int N = 1 << 0, NE = 1 << 1, E = 1 << 2, SE = 1 << 3, S = 1 << 4, SW = 1 << 5, W = 1 << 6, NW = 1 << 7;

        /// <summary>The neighbour each bit stands for, as a cell offset (x east, y north).</summary>
        public static readonly Vector2Int[] Offsets =
        {
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(1, -1),
            new Vector2Int(0, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1),
        };

        /// <summary>The quarters, in the order a tile set stores them.</summary>
        public const int QuarterNE = 0, QuarterSE = 1, QuarterSW = 2, QuarterNW = 3;
        public const int Quarters = 4;

        /// <summary>Which way each quarter lies from the centre: x east, y north.</summary>
        public static readonly Vector2Int[] QuarterSigns =
        {
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1),
        };

        // The east/west side, north/south side and corner each quarter touches.
        static readonly int[] QuarterSideX = { E, E, W, W };
        static readonly int[] QuarterSideZ = { N, S, S, N };
        static readonly int[] QuarterCorner = { NE, SE, SW, NW };

        /// <summary>
        /// The mask with every corner bit cleared that cannot change the drawing, so neighbourhoods
        /// that look the same share one mesh. A corner neighbour shapes a quarter only when both
        /// sides next to it are joined; it also decides whether a border running out along the
        /// north or east edge carries on straight (<see cref="Joints"/>), so north-east counts when
        /// either side next to it is joined, south-east when the east is, north-west when the north is.
        /// </summary>
        public static int Canonical(int mask)
        {
            mask &= 0xFF;
            if ((mask & (N | E)) == 0) mask &= ~NE;
            if ((mask & E) == 0) mask &= ~SE;
            if ((mask & (S | W)) != (S | W)) mask &= ~SW;
            if ((mask & N) == 0) mask &= ~NW;
            return mask;
        }

        /// <summary>What <paramref name="quarter"/> of a cell with neighbours <paramref name="mask"/> looks like.</summary>
        public static QuarterShape Shape(int mask, int quarter)
        {
            bool x = (mask & QuarterSideX[quarter]) != 0;
            bool z = (mask & QuarterSideZ[quarter]) != 0;

            if (!x && !z) return QuarterShape.Corner;
            if (x && !z) return QuarterShape.BorderZ;
            if (!x) return QuarterShape.BorderX;
            return (mask & QuarterCorner[quarter]) != 0 ? QuarterShape.Open : QuarterShape.InsideCorner;
        }

        /// <summary>
        /// The seams where a straight border runs on from this cell into the next. A post marks
        /// each one, so a long bed reads as modules joined end to end. Writes them into
        /// <paramref name="into"/> (at least four long) and returns how many there are.
        ///
        /// A seam is shared by two cells, so only one of them may claim it: this cell claims the
        /// seams on its east and north edges, its neighbours the other two. A border only runs on
        /// straight when the corner past the seam is empty; otherwise the neighbour turns an inside
        /// corner there, which has a post of its own a few centimetres away.
        /// </summary>
        public static int Joints(int mask, TileJoint[] into)
        {
            int count = 0;
            bool n = (mask & N) != 0, e = (mask & E) != 0, s = (mask & S) != 0, w = (mask & W) != 0;

            if (e && !n && (mask & NE) == 0) into[count++] = new TileJoint(true, 1);
            if (e && !s && (mask & SE) == 0) into[count++] = new TileJoint(true, -1);
            if (n && !e && (mask & NE) == 0) into[count++] = new TileJoint(false, 1);
            if (n && !w && (mask & NW) == 0) into[count++] = new TileJoint(false, -1);
            return count;
        }
    }

    /// <summary>A seam a straight border crosses: on the cell's east edge or its north edge, and on which border.</summary>
    public readonly struct TileJoint
    {
        /// <summary>On the east edge (the border runs east-west); otherwise the north edge.</summary>
        public readonly bool East;

        /// <summary>Which border: +1 the north (or east) one, -1 the south (or west) one.</summary>
        public readonly int Side;

        public TileJoint(bool east, int side)
        {
            East = east;
            Side = side;
        }

        /// <summary>Where the post stands, from the cell's centre, for a cell <paramref name="half"/> across from centre to edge and borders <paramref name="inset"/> from the centre.</summary>
        public Vector3 Position(float half, float inset) =>
            East ? new Vector3(half, 0f, Side * inset) : new Vector3(Side * inset, 0f, half);
    }

    /// <summary>The five ways a quarter of a tile can look.</summary>
    public enum QuarterShape
    {
        /// <summary>No neighbour on either side: a border down both, a post where they meet.</summary>
        Corner = 0,

        /// <summary>Joined east or west only: one straight border, along the north or south edge.</summary>
        BorderZ = 1,

        /// <summary>Joined north or south only: one straight border, along the east or west edge.</summary>
        BorderX = 2,

        /// <summary>Joined on both sides but not across the corner: a short L of border round the gap.</summary>
        InsideCorner = 3,

        /// <summary>Joined all round: nothing but the inside.</summary>
        Open = 4,
    }
}
