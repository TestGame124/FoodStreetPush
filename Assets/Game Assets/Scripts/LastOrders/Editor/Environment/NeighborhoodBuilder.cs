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
    /// The abandoned suburbs on the west, east and front edges: the streets, the pavements, the
    /// lots along them and the houses on the lots, and the clutter a street collects when nobody
    /// has driven down it for a year.
    ///
    /// The streets come from <see cref="EnvironmentLayout.Streets"/>. Lots are cut along both sides
    /// of each one, as wide as the plan allows and as deep as the ground behind them does, so a
    /// block fills itself in rather than being drawn lot by lot - and a lot that would land in the
    /// clear ground, on another street, on a trail or over the checkpoint simply is not made.
    /// </summary>
    public static class NeighborhoodBuilder
    {
        public sealed class Result
        {
            public int Lots;
            public int Houses;
            public int EmptyLots;
            public readonly List<Rect> Occupied = new List<Rect>();
        }

        const float StreetTop = 0.035f;
        const float SidewalkTop = 0.075f;

        public static Result Build(Transform west, Transform south, Transform east, IReadOnlyList<Rect> keepOut,
                                   ref DeterministicRandom rng)
        {
            var result = new Result();

            Transform Band(Vector2 p) => p.y < L.ClearMinZ && p.x > L.ClearMinX && p.x < L.ClearMaxX ? south
                                       : p.x <= L.ClearMinX ? west : p.x >= L.ClearMaxX ? east : south;

            var streetRects = new List<Rect>();
            foreach (L.Street s in L.Streets) streetRects.Add(s.Surface);

            // --- Streets ------------------------------------------------------------------------------

            foreach (L.Street street in L.Streets)
            {
                Transform parent = K.Group(street.Name, Band(street.Surface.center));
                LayStreet(parent, street, streetRects, ref rng);
            }

            // --- Lots ---------------------------------------------------------------------------------

            var blocked = new List<Rect>(keepOut);
            foreach (Rect r in streetRects) blocked.Add(Inflate(r, L.SidewalkWidth + 0.3f));

            int index = 0;
            foreach (L.Street street in L.Streets)
            {
                foreach (float side in new[] { 1f, -1f })
                {
                    float length = street.AlongX ? Mathf.Abs(street.To.x - street.From.x) : Mathf.Abs(street.To.y - street.From.y);
                    float t = rng.Range(0.5f, 2f);

                    while (t < length - 9f)
                    {
                        float width = rng.Range(11f, 16.5f);
                        if (!TryLot(street, side, t, width, blocked, result.Occupied, out AbandonedHouseBuilder.Lot lot))
                        {
                            t += 2.5f;
                            continue;
                        }

                        Rect rect = lot.WorldRect;
                        result.Occupied.Add(rect);
                        blocked.Add(Inflate(rect, 0.15f));
                        result.Lots++;

                        Transform parent = Band(lot.Centre);
                        string name = $"Lot_{index++:000}";

                        if (rng.Chance(0.1f))
                        {
                            EmptyLot(parent, name, lot, ref rng);
                            result.EmptyLots++;
                        }
                        else
                        {
                            AbandonedHouseBuilder.Build(parent, name, lot, ref rng);
                            result.Houses++;
                        }

                        t += width + rng.Range(0.2f, 1.4f);
                    }
                }
            }

            // --- Between the lots -------------------------------------------------------------------------

            foreach (Rect band in new[] { L.WestBand, L.SouthBand, L.EastBand })
            {
                Overgrowth(Band(band.center), band, blocked, ref rng);
            }

            return result;
        }

        // --- Streets ------------------------------------------------------------------------------------

        static void LayStreet(Transform parent, L.Street street, List<Rect> all, ref DeterministicRandom rng)
        {
            Rect surface = street.Surface;
            bool alongX = street.AlongX;
            float length = alongX ? surface.width : surface.height;
            float centre = alongX ? surface.center.y : surface.center.x;
            float start = alongX ? surface.xMin : surface.yMin;

            if (!street.IsMainRoad)
            {
                // One textured slab, its faded centre dashes in its texture. The avenues lie a few
                // millimetres over the cross streets, so at a junction the avenue carries through
                // and the cross street's dashes stop at its edge.
                bool avenue = street.Name.Contains("Avenue");
                SurfaceKit.Street(parent, surface, alongX, avenue ? StreetTop : StreetTop - 0.004f, 0.03f);

                // Potholes and patching.
                for (float t = start + 3f; t < start + length - 3f; t += rng.Range(5f, 9f))
                {
                    float off = rng.Range(-street.Width * 0.35f, street.Width * 0.35f);
                    Vector2 at = Point(alongX, t, centre + off);
                    Decal(parent, "Pothole", at, rng.Range(0.6f, 1.5f), rng.Range(0.5f, 1.2f), rng.Range(0f, 180f),
                          rng.Chance(0.6f) ? E.AsphaltDark : E.AsphaltPatch, StreetTop + 0.003f);
                }
            }

            // Pavements both sides, cut where another street crosses: one mesh for the whole street.
            var pavement = new SurfaceKit.PavementRun();
            float offset = street.Width * 0.5f + L.SidewalkWidth * 0.5f;
            foreach (float side in new[] { 1f, -1f })
            {
                float line = centre + side * offset;
                float from = start;
                float end = start + length;

                var cuts = new List<Vector2>();
                foreach (Rect other in all)
                {
                    if (other == surface) continue;

                    Rect grown = Inflate(other, L.SidewalkWidth);
                    bool crosses = alongX
                        ? line >= grown.yMin && line <= grown.yMax
                        : line >= grown.xMin && line <= grown.xMax;
                    if (!crosses) continue;

                    cuts.Add(alongX ? new Vector2(grown.xMin, grown.xMax) : new Vector2(grown.yMin, grown.yMax));
                }

                cuts.Sort((a, b) => a.x.CompareTo(b.x));

                float cursor = from;
                foreach (Vector2 cut in cuts)
                {
                    Pavement(pavement, alongX, line, cursor, Mathf.Min(cut.x, end), ref rng);
                    cursor = Mathf.Max(cursor, cut.y);
                }

                Pavement(pavement, alongX, line, cursor, end, ref rng);
            }

            pavement.Build(parent);

            // What is left in the street.
            bool lane = street.Name.Contains("Lane");
            for (float t = start + rng.Range(6f, 14f); t < start + length - 4f; t += rng.Range(16f, 26f))
            {
                float side = rng.Chance(0.5f) ? 1f : -1f;
                Vector2 kerb = Point(alongX, t, centre + side * (street.Width * 0.5f + L.SidewalkWidth + 0.2f));

                float facing = alongX ? (side > 0 ? 180f : 0f) : (side > 0 ? -90f : 90f);
                ObstacleKit.Streetlight(parent, "Streetlight", kerb, facing, ref rng);

                if (!street.IsMainRoad && !lane && rng.Chance(0.55f))
                {
                    float across = rng.Range(-street.Width * 0.3f, street.Width * 0.3f);
                    Vector2 carAt = Point(alongX, t + rng.Range(3f, 8f), centre + across);
                    float yaw = (alongX ? 0f : 90f) + (rng.Chance(0.5f) ? 0f : 180f) + rng.Range(-28f, 28f);
                    ObstacleKit.Car(parent, "AbandonedCar", carAt, yaw, ref rng);
                }
                else if (street.IsMainRoad && rng.Chance(0.4f))
                {
                    // Not on the main road itself: the customers still drive it. On the verge.
                    Vector2 verge = Point(alongX, t + 5f, centre + side * (street.Width * 0.5f + L.SidewalkWidth + 2.2f));
                    ObstacleKit.Car(parent, "AbandonedCar", verge, (alongX ? 0f : 90f) + rng.Range(-40f, 40f), ref rng);
                }

                if (rng.Chance(0.35f))
                {
                    Vector2 bagAt = Point(alongX, t + rng.Range(-4f, 4f), centre - side * (street.Width * 0.5f + L.SidewalkWidth * 0.5f));
                    ObstacleKit.Trash(parent, "Trash", bagAt, ref rng);
                }
            }

            // Someone tried to close the lane streets once.
            if (lane)
            {
                float t = alongX ? (street.From.x < 0f ? start + length - 8f : start + 8f) : start + length - 8f;
                foreach (float side in new[] { 1f, -1f })
                {
                    Vector2 at = Point(alongX, t, centre + side * (street.Width * 0.5f - 0.9f));
                    ObstacleKit.Barriers(parent, "Barricade", at, alongX ? 90f : 0f, ref rng);
                }
            }

            // Now and then a pole down across it.
            if (!street.IsMainRoad && rng.Chance(0.35f))
            {
                Vector2 at = Point(alongX, start + length * rng.Range(0.3f, 0.7f), centre);
                E.Pack(E.PowerPole, parent, new Vector3(at.x, 0.25f, at.y), (alongX ? 90f : 0f) + rng.Range(-30f, 30f), 1f, null,
                       new Vector3(0f, 0f, 84f));
            }
        }

        static void Pavement(SurfaceKit.PavementRun run, bool alongX, float line, float from, float to, ref DeterministicRandom rng)
        {
            // Runs of four to seven metres, and now and then a stretch missing; the slabs and the
            // joints between them are in the texture.
            float t = from;
            while (to - t > 0.6f)
            {
                float piece = Mathf.Min(rng.Range(4f, 7f), to - t);
                if (!rng.Chance(0.07f))
                {
                    Vector2 at = Point(alongX, t + piece * 0.5f, line);
                    Vector2 size = alongX ? new Vector2(piece, L.SidewalkWidth) : new Vector2(L.SidewalkWidth, piece);
                    run.Add(at, size, SidewalkTop, 0.07f);
                }

                t += piece;
            }
        }

        static Vector2 Point(bool alongX, float along, float across) =>
            alongX ? new Vector2(along, across) : new Vector2(across, along);

        /// <summary>A flat mark at an exact height, for things that lie on a surface rather than the ground.</summary>
        static void Decal(Transform parent, string name, Vector2 at, float sx, float sz, float yaw, Color colour, float y)
        {
            GameObject go = E.Disc(name, parent, at, sx, sz, yaw, colour);
            go.transform.localPosition = new Vector3(at.x, y, at.y);
        }

        // --- Lots ------------------------------------------------------------------------------------------

        /// <summary>
        /// The deepest lot that fits at <paramref name="t"/> metres along a street, on one side of it.
        /// Deep lots first, so a block with room behind it gets a proper back garden.
        /// </summary>
        static bool TryLot(L.Street street, float side, float t, float width, List<Rect> blocked, List<Rect> occupied,
                           out AbandonedHouseBuilder.Lot lot)
        {
            lot = default;

            bool alongX = street.AlongX;
            float start = alongX ? Mathf.Min(street.From.x, street.To.x) : Mathf.Min(street.From.y, street.To.y);
            float centreLine = alongX ? street.From.y : street.From.x;
            float edge = street.Width * 0.5f + L.SidewalkWidth + 0.35f;

            for (float depth = 18f; depth >= 9f; depth -= 1f)
            {
                float across = centreLine + side * (edge + depth * 0.5f);
                float along = start + t + width * 0.5f;
                Vector2 centre = alongX ? new Vector2(along, across) : new Vector2(across, along);

                float yaw = alongX ? (side > 0 ? 180f : 0f) : (side > 0 ? -90f : 90f);

                var candidate = new AbandonedHouseBuilder.Lot
                {
                    Centre = centre,
                    Width = width,
                    Depth = depth,
                    Yaw = yaw,
                    Detailed = L.Outside(centre) < 14f,
                };

                Rect rect = candidate.WorldRect;
                if (!Fits(rect, blocked)) continue;

                lot = candidate;
                return true;
            }

            return false;
        }

        static bool Fits(Rect rect, List<Rect> blocked)
        {
            if (rect.Overlaps(L.Clear)) return false;
            if (rect.yMax > L.ClearMaxZ - 0.5f) return false;
            if (!L.World.Contains(rect.min) || !L.World.Contains(rect.max)) return false;
            if (L.Outside(rect.center) > L.ContentDepth) return false;

            foreach (Rect b in blocked)
                if (rect.Overlaps(b)) return false;

            return true;
        }

        static readonly ObstacleKit.Kind[] EmptyLotContents =
        {
            ObstacleKit.Kind.Junk, ObstacleKit.Kind.Rocks, ObstacleKit.Kind.Bushes, ObstacleKit.Kind.Bushes,
            ObstacleKit.Kind.DeadTree, ObstacleKit.Kind.Tyres,
        };

        static void EmptyLot(Transform parent, string name, AbandonedHouseBuilder.Lot lot, ref DeterministicRandom rng)
        {
            Transform root = K.Group(name, parent);
            root.localPosition = new Vector3(lot.Centre.x, 0f, lot.Centre.y);
            root.localRotation = Quaternion.Euler(0f, lot.Yaw, 0f);

            E.Blob("Overgrown", root, Vector2.zero, Mathf.Min(lot.Width, lot.Depth) * 0.9f, E.Pick(E.SunGrass, ref rng), 1, ref rng, 0.55f);

            if (rng.Chance(0.5f))
            {
                // A slab where a house was.
                E.Slab("OldSlab", root, new Vector2(rng.Range(-1f, 1f), rng.Range(-1f, 0f)),
                       new Vector2(lot.Width * 0.55f, lot.Depth * 0.45f), 0f, 0.09f, 0.08f, E.Foundation);
            }

            int things = rng.Range(2, 5);
            for (int i = 0; i < things; i++)
            {
                var at = new Vector2(rng.Range(-lot.Width * 0.35f, lot.Width * 0.35f), rng.Range(-lot.Depth * 0.35f, lot.Depth * 0.35f));
                ObstacleKit.Kind kind = EmptyLotContents[rng.Range(0, EmptyLotContents.Length)];
                ObstacleKit.Build(kind, root, $"{kind}_{i}", at, ref rng);
            }
        }

        // --- The ground between ------------------------------------------------------------------------------

        static void Overgrowth(Transform parent, Rect band, List<Rect> blocked, ref DeterministicRandom rng)
        {
            Transform root = K.Group("Overgrowth", parent);

            var points = PoissonScatter.Sample(band, 5f, 8f,
                p => Mathf.Lerp(5f, 8f, PoissonScatter.Noise(p, 0.08f, 17)),
                p =>
                {
                    if (L.Outside(p) < 1f || L.Outside(p) > L.ContentDepth) return false;
                    if (p.y > L.ClearMaxZ - 1f) return false;
                    foreach (Rect b in blocked)
                        if (Inflate(b, 1.2f).Contains(p)) return false;
                    return true;
                }, ref rng, maxPoints: 120);

            int i = 0;
            foreach (Vector2 p in points)
            {
                float roll = rng.NextFloat();
                ObstacleKit.Kind kind = roll < 0.3f ? ObstacleKit.Kind.Bushes
                                      : roll < 0.48f ? ObstacleKit.Kind.DeadTree
                                      : roll < 0.62f ? ObstacleKit.Kind.Junk
                                      : roll < 0.72f ? ObstacleKit.Kind.Fence
                                      : roll < 0.8f ? ObstacleKit.Kind.Car
                                      : roll < 0.88f ? ObstacleKit.Kind.Tyres
                                      : ObstacleKit.Kind.Rocks;

                if (kind == ObstacleKit.Kind.Bushes && rng.Chance(0.4f))
                {
                    E.PackOf(E.Trees, root, new Vector3(p.x, 0f, p.y), rng.Range(0f, 360f), rng.Range(1.5f, 2.3f), ref rng);
                    continue;
                }

                ObstacleKit.Build(kind, root, $"{kind}_{i++:000}", p, ref rng);
            }
        }

        public static Rect Inflate(Rect r, float by) => new Rect(r.xMin - by, r.yMin - by, r.width + by * 2f, r.height + by * 2f);
    }
}
