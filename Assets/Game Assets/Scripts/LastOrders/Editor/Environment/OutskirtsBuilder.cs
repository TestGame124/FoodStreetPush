using System.Collections.Generic;
using FoodStreet.Visitors.EditorTools;
using LastOrders.Core;
using UnityEngine;
using E = LastOrders.EditorTools.EnvKit;
using K = LastOrders.EditorTools.PrimitiveKit;
using L = LastOrders.EditorTools.EnvironmentLayout;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The open ground between the walls and the fog: the land the compound would grow into.
    ///
    /// Kept mostly open, with small things lying about in it - a heap of junk, a car, a run of
    /// broken fence, rocks, scrub - scattered unevenly, thinner near the walls and thicker toward
    /// the edges, and never in the way of a trail, the road or the gate. Each one goes under the
    /// side of the compound it is on, so a later expansion can take one side's obstacles over as
    /// debris to clear, the way the courtyard's were.
    ///
    /// The ground gets drier and more worn toward the edges, and the night's approach trails are
    /// worn into it: the paths it walks in on, visible by day. Nothing stands across the old fence's line.
    /// </summary>
    public static class OutskirtsBuilder
    {
        public sealed class Result
        {
            public int Obstacles;
            public int Patches;
            public readonly Dictionary<string, int> PerSector = new Dictionary<string, int>();
        }

        public enum Sector { North, West, East, South }

        static readonly (ObstacleKit.Kind kind, float weight)[] SouthMix =
        {
            (ObstacleKit.Kind.Car, 3f), (ObstacleKit.Kind.Barriers, 2f), (ObstacleKit.Kind.Junk, 2f), (ObstacleKit.Kind.Tyres, 1.5f),
            (ObstacleKit.Kind.Sandbags, 1f), (ObstacleKit.Kind.Crater, 1f), (ObstacleKit.Kind.Barrels, 1f),
            (ObstacleKit.Kind.Rocks, 1f), (ObstacleKit.Kind.Bushes, 1f), (ObstacleKit.Kind.Fence, 1f),
        };

        static readonly (ObstacleKit.Kind kind, float weight)[] SideMix =
        {
            (ObstacleKit.Kind.Junk, 2f), (ObstacleKit.Kind.Car, 1.5f), (ObstacleKit.Kind.Fence, 2f), (ObstacleKit.Kind.Bushes, 2f),
            (ObstacleKit.Kind.Rocks, 1.5f), (ObstacleKit.Kind.DeadTree, 1.5f), (ObstacleKit.Kind.Crates, 1f),
            (ObstacleKit.Kind.Tyres, 1f), (ObstacleKit.Kind.Barrels, 1f), (ObstacleKit.Kind.Crater, 0.7f),
            (ObstacleKit.Kind.Log, 0.5f), (ObstacleKit.Kind.Sandbags, 0.5f),
        };

        static readonly (ObstacleKit.Kind kind, float weight)[] NorthMix =
        {
            (ObstacleKit.Kind.Rocks, 2f), (ObstacleKit.Kind.DeadTree, 2.5f), (ObstacleKit.Kind.Log, 2f), (ObstacleKit.Kind.Bushes, 2.5f),
            (ObstacleKit.Kind.Junk, 1f), (ObstacleKit.Kind.Fence, 1f), (ObstacleKit.Kind.Crater, 0.5f), (ObstacleKit.Kind.Car, 0.5f),
        };

        /// <param name="keepOut">Rectangles nothing may stand in: the compound, the courtyard, the car park.</param>
        /// <param name="noStanding">Where only flat ground dressing may go: the nav grid outside the walls.</param>
        public static Result Build(Transform root, IReadOnlyList<Rect> keepOut, IReadOnlyList<Rect> noStanding,
                                   ref DeterministicRandom rng)
        {
            var result = new Result();
            Rect clear = L.Clear;

            var sectors = new Dictionary<Sector, Transform>();
            foreach (Sector s in System.Enum.GetValues(typeof(Sector))) sectors[s] = K.Group(s.ToString(), root);

            Transform ground = K.Group("Ground", root);
            Transform trails = K.Group("Trails", root);

            var road = ZoneLayout.RoadCorridor;
            var gate = ZoneLayout.GateApproach;

            bool OnRoad(Vector2 p, float margin) =>
                NeighborhoodBuilder.Inflate(road, margin).Contains(p) || NeighborhoodBuilder.Inflate(gate, margin).Contains(p);

            bool InAny(IReadOnlyList<Rect> rects, Vector2 p, float margin)
            {
                foreach (Rect r in rects)
                    if (NeighborhoodBuilder.Inflate(r, margin).Contains(p)) return true;
                return false;
            }

            // --- The trails -----------------------------------------------------------------------------

            var trailSegments = new List<(Vector2 a, Vector2 b)>();
            foreach (L.Lane lane in L.Lanes)
            {
                // In the trees the forest lays its own winding part; out here it is straight.
                var a = new Vector2(lane.TrailStart.x, lane.TrailStart.z);
                var b = new Vector2(lane.WallEnd.x, lane.WallEnd.z);
                trailSegments.Add((a, b));

                Trail(trails, lane.Name, a, b, ref rng);
            }

            bool OnTrail(Vector2 p, float clearance)
            {
                foreach (var (a, b) in trailSegments)
                    if (DistanceToSegment(p, a, b) < clearance) return true;
                return false;
            }

            // The old fence runs round inside the edge; nothing stands across its line.
            Rect fence = L.Fence;
            bool OnFenceLine(Vector2 p, float margin) =>
                NeighborhoodBuilder.Inflate(fence, margin).Contains(p) && !NeighborhoodBuilder.Inflate(fence, -margin).Contains(p);

            // --- Worn ground ----------------------------------------------------------------------------------

            var patches = PoissonScatter.Sample(clear, 7f, 13f,
                p => Mathf.Lerp(13f, 7f, EdgeCloseness(p)),
                p => !InAny(keepOut, p, 0.5f) && !OnRoad(p, -0.5f) && PoissonScatter.Noise(p, 0.05f, 7) > 0.34f,
                ref rng, maxPoints: 220);

            foreach (Vector2 p in patches)
            {
                // Sunlit clumps of grass everywhere, bare earth more often toward the edges, the odd wallow of mud.
                float edge = EdgeCloseness(p);
                float roll = rng.NextFloat();
                Color colour = roll < 0.12f ? E.Pick(E.Mud, ref rng)
                             : roll < 0.3f + edge * 0.35f ? E.Pick(E.Dirt, ref rng)
                             : E.Pick(E.SunGrass, ref rng);

                float size = rng.Range(4f, 8.5f) * (0.85f + edge * 0.4f);
                E.Blob("Patch", ground, p, size, colour, rng.Range(0, 2), ref rng, rng.Range(0.35f, 0.55f));
                result.Patches++;
            }

            // --- The obstacles ---------------------------------------------------------------------------------

            var spots = PoissonScatter.Sample(clear, 5.5f, 10f,
                p => Mathf.Lerp(10f, 5.5f, EdgeCloseness(p)),
                p =>
                {
                    if (InAny(keepOut, p, 3f) || InAny(noStanding, p, 1.5f)) return false;
                    if (OnRoad(p, 2.5f) || OnTrail(p, L.TrailClearance) || OnFenceLine(p, 3f)) return false;
                    if (Vector2.Distance(p, new Vector2(L.DinerSign.x, L.DinerSign.z)) < 4f) return false;

                    // The first few metres off the walls stay open, and the rest has clearings in it.
                    return PoissonScatter.Noise(p, 0.07f, 3) > 0.32f;
                }, ref rng, maxPoints: 200);

            var counters = new Dictionary<Sector, int>();
            foreach (Vector2 p in spots)
            {
                Sector sector = SectorOf(p);
                var mix = sector == Sector.South ? SouthMix : sector == Sector.North ? NorthMix : SideMix;
                ObstacleKit.Kind kind = Pick(mix, ref rng);

                counters.TryGetValue(sector, out int n);
                counters[sector] = n + 1;

                ObstacleKit.Build(kind, sectors[sector], $"Obstacle_{kind}_{n:000}", p, ref rng);
                result.Obstacles++;
            }

            foreach (var kv in counters) result.PerSector[kv.Key.ToString()] = kv.Value;

            DinerSign(root, ref rng);
            return result;
        }

        /// <summary>Which side of the compound a point is on, by the clear ground's own proportions.</summary>
        public static Sector SectorOf(Vector2 p)
        {
            Rect r = L.Clear;
            float dx = (p.x - r.center.x) / (r.width * 0.5f);
            float dz = (p.y - r.center.y) / (r.height * 0.5f);

            if (Mathf.Abs(dx) > Mathf.Abs(dz)) return dx < 0f ? Sector.West : Sector.East;
            return dz < 0f ? Sector.South : Sector.North;
        }

        /// <summary>0 well inside the clear ground, 1 at the fog.</summary>
        static float EdgeCloseness(Vector2 p) => Mathf.Clamp01(1f - -L.Outside(p) / 22f);

        static ObstacleKit.Kind Pick((ObstacleKit.Kind kind, float weight)[] mix, ref DeterministicRandom rng)
        {
            float total = 0f;
            foreach (var entry in mix) total += entry.weight;

            float roll = rng.Range(0f, total);
            foreach (var entry in mix)
            {
                roll -= entry.weight;
                if (roll <= 0f) return entry.kind;
            }

            return mix[mix.Length - 1].kind;
        }

        /// <summary>
        /// A path worn into the ground by a lot of feet: soft stains overlapping along a line, never
        /// quite straight at the edges, with a darker, barer strip down the middle where most walk.
        /// </summary>
        public static void Trail(Transform parent, string name, IReadOnlyList<Vector2> line, ref DeterministicRandom rng)
        {
            Transform root = K.Group($"Trail_{name}", parent);

            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 a = line[i];
                Vector2 b = line[i + 1];
                float length = Vector2.Distance(a, b);
                if (length < 0.01f) continue;

                Vector2 dir = (b - a) / length;
                var normal = new Vector2(-dir.y, dir.x);
                float yaw = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;

                for (float t = 0f; t < length; t += 1.7f)
                {
                    Vector2 at = a + dir * t + normal * rng.Range(-0.4f, 0.4f);
                    GroundSplats.Splat("Worn", root, at, rng.Range(3.4f, 4.6f), L.TrailHalfWidth * 2f * rng.Range(0.85f, 1.1f),
                                       yaw + rng.Range(-15f, 15f), E.Pick(E.Dirt, ref rng), rng.Range(0.5f, 0.68f), 4, ref rng);

                    if (rng.Chance(0.7f))
                        GroundSplats.Splat("Bare", root, at + normal * rng.Range(-0.5f, 0.5f), rng.Range(2f, 3f), rng.Range(1.2f, 1.9f),
                                           yaw + rng.Range(-10f, 10f), E.Pick(E.Mud, ref rng), rng.Range(0.25f, 0.4f), 5, ref rng);
                }
            }
        }

        static void Trail(Transform parent, string name, Vector2 a, Vector2 b, ref DeterministicRandom rng) =>
            Trail(parent, name, new[] { a, b }, ref rng);

        /// <summary>Hand-painted, pointing the way in: the one piece of the outskirts that is not junk.</summary>
        static void DinerSign(Transform parent, ref DeterministicRandom rng)
        {
            Transform sign = K.Group("DinerSign", parent);
            sign.localPosition = L.DinerSign;
            sign.localRotation = Quaternion.Euler(0f, -12f, 0f);

            K.Cylinder("PostL", sign, new Vector3(-1.1f, 0f, 0f), 0.14f, 2.3f, K.WoodDark);
            K.Cylinder("PostR", sign, new Vector3(1.1f, 0f, 0f), 0.14f, 2.3f, K.WoodDark);
            K.Cube("Board", sign, new Vector3(0f, 1.75f, 0f), new Vector3(2.8f, 0.9f, 0.1f), K.WoodLight);
            K.Cube("Arrow", sign, new Vector3(1.55f, 1.75f, 0f), new Vector3(0.5f, 0.5f, 0.1f), K.DinerAccent, 45f);

            SignText.Add(sign, "LAST ORDERS\nDINER", new Vector3(0f, 1.75f, -0.07f), 0.34f, K.DinerAccent);

            ObstacleKit.Tyres(parent, "SignTyres", new Vector2(L.DinerSign.x - 2.4f, L.DinerSign.z + 0.6f), ref rng);
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
