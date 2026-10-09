using FoodStreet.Navigation;
using LastOrders.Perimeter;
using UnityEngine;

namespace LastOrders.Defense
{
    /// <summary>
    /// What a defense set down on the build grid would be standing on: open ground, a stretch of the
    /// compound wall it can be mounted on top of, or something it cannot go on at all.
    ///
    /// Asked by the shop's placement rule while the player drags a defense about, and by the
    /// defense itself once it is down (<see cref="WallMount"/>), so the two always agree.
    ///
    /// - A wall: some of the footprint is over a section of the compound wall, and the wall's line
    ///   runs near enough the footprint's middle that a bastion the size of the footprint encloses
    ///   it. Further off than that the defense would be half on the wall, and is refused.
    /// - Refused too: a gateway (it would block the gate), a wall site or defense pad (something
    ///   stands, or will stand, there), and any defense already standing in the level.
    ///
    /// The footprint is tested shrunk by <see cref="Margin"/>, so grazing the face of a wall is still
    /// open ground.
    /// </summary>
    public static class WallFooting
    {
        public enum Kind { Ground, Wall, Blocked }

        public readonly struct Result
        {
            public readonly Kind Kind;
            public readonly CompoundWall Wall;
            public readonly string Reason;

            public Result(Kind kind, CompoundWall wall, string reason)
            {
                Kind = kind;
                Wall = wall;
                Reason = reason;
            }
        }

        /// <summary>Metres the footprint is shrunk by before anything is tested against it.</summary>
        public const float Margin = 0.12f;

        /// <summary>The most the wall's centre line may be off the footprint's middle and still be mounted on.</summary>
        public const float MaxOffset = 0.75f;

        /// <summary>Half the size of a defense pad's slab, for keeping clear of one.</summary>
        const float PadHalf = 1.4f;

        /// <summary>Half the size of a standing defense - a guard tower is the largest - for keeping clear of one.</summary>
        const float PostHalf = 1.2f;

        const int Samples = 5;

        /// <summary>
        /// What a footprint of <paramref name="size"/> metres, centred on <paramref name="centre"/>, is on.
        /// <paramref name="self"/> is the defense being placed or moved, so it is never in its own way.
        /// </summary>
        public static Result Find(Vector3 centre, Vector2 size, Transform self)
        {
            Vector2 half = new Vector2(Mathf.Max(0.05f, size.x * 0.5f - Margin), Mathf.Max(0.05f, size.y * 0.5f - Margin));

            foreach (WallSite site in WallSite.Standing)
            {
                if (site == null || Owns(self, site.transform)) continue;
                if (Overlaps(centre, half, site.transform.position, PadHalf))
                    return new Result(Kind.Blocked, null, site.OnGround ? "on a defense pad" : "on a wall site");
            }

            foreach (DefensePost post in DefensePost.Standing)
            {
                if (post == null || post.PlacedFromShop || Owns(self, post.transform) || post.GetComponentInParent<WallSite>() != null) continue;
                if (Overlaps(centre, half, post.transform.position, PostHalf))
                    return new Result(Kind.Blocked, null, $"on top of the {post.DisplayName}");
            }

            CompoundWall best = null;
            int bestCount = 0;

            foreach (IMovementBarrier barrier in MovementBarriers.Standing)
            {
                if (!(barrier is CompoundStructure piece) || piece == null || !piece.IsPresent || Owns(self, piece.transform)) continue;

                int inside = Covered(piece, centre, half);
                if (inside == 0) continue;

                if (piece is CompoundGate) return new Result(Kind.Blocked, null, "in a gateway");
                if (!(piece is CompoundWall wall)) continue;
                if (wall.GetComponentInParent<WallSite>() != null) return new Result(Kind.Blocked, null, "on a wall site");

                if (Offset(wall, centre) > MaxOffset) return new Result(Kind.Blocked, null, "half on the wall");

                if (inside > bestCount)
                {
                    best = wall;
                    bestCount = inside;
                }
            }

            return best != null ? new Result(Kind.Wall, best, null) : new Result(Kind.Ground, null, null);
        }

        /// <summary>The wall's yaw, in degrees: its length runs along its own X.</summary>
        public static float Yaw(CompoundWall wall) => wall.transform.eulerAngles.y;

        /// <summary>How far <paramref name="point"/> is from the wall's centre line, flat, in metres.</summary>
        public static float Offset(CompoundWall wall, Vector3 point)
        {
            Vector3 local = Quaternion.Euler(0f, -Yaw(wall), 0f) * (point - wall.transform.position);
            return Mathf.Abs(local.z);
        }

        /// <summary>How many of the footprint's sample points fall inside <paramref name="piece"/>'s footprint.</summary>
        static int Covered(CompoundStructure piece, Vector3 centre, Vector2 half)
        {
            Vector3 middle = piece.transform.position;
            float yaw = piece.transform.eulerAngles.y;
            var box = new Vector2(piece.Length * 0.5f, piece.Thickness * 0.5f);

            // Too far off for any of it to touch.
            float reach = box.magnitude + half.magnitude;
            if (DefenseUnit.FlatDistanceSquared(middle, centre) > reach * reach) return 0;

            int count = 0;
            for (int i = 0; i < Samples; i++)
                for (int j = 0; j < Samples; j++)
                {
                    var at = new Vector3(centre.x + Mathf.Lerp(-half.x, half.x, i / (Samples - 1f)), centre.y,
                                         centre.z + Mathf.Lerp(-half.y, half.y, j / (Samples - 1f)));
                    if (MovementBarriers.Penetration(at, middle, yaw, box, 0f) > 0f) count++;
                }

            return count;
        }

        static bool Overlaps(Vector3 centre, Vector2 half, Vector3 other, float otherHalf) =>
            Mathf.Abs(centre.x - other.x) < half.x + otherHalf && Mathf.Abs(centre.z - other.z) < half.y + otherHalf;

        static bool Owns(Transform self, Transform other) => self != null && other != null && other.IsChildOf(self);
    }
}
