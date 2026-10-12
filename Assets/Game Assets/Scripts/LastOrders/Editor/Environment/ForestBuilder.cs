using System.Collections.Generic;
using LastOrders.Core;
using UnityEditor;
using UnityEngine;
using E = LastOrders.EditorTools.EnvKit;
using K = LastOrders.EditorTools.PrimitiveKit;
using L = LastOrders.EditorTools.EnvironmentLayout;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The trees along the back of the map.
    ///
    /// A forest edge is not a wall of trees. It starts as dead trunks, stumps and scrub standing
    /// apart on the open ground, thickens into a mixed wood, and ends in a dark mass nobody can see
    /// into - so the spacing tightens with depth, the kinds change with it, and the far rows are
    /// the pack's whole clumps of trees rather than one tree at a time. One trail winds out of it:
    /// the way the night comes in from the north.
    /// </summary>
    public static class ForestBuilder
    {
        public sealed class Result
        {
            public int Trees;
            public int Undergrowth;
            public readonly List<Vector2> Trail = new List<Vector2>();
        }

        const string SourceMaterialPath = "Assets/PolygonFarm/Materials/PolygonFarm_01_A.mat";

        public static Result Build(Transform root, L.Lane lane, ref DeterministicRandom rng)
        {
            var result = new Result();
            Rect band = L.NorthBand;

            Material dusk = E.Dimmed(AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath) ?? FindPackMaterial(),
                                     "Dusk", new Color(0.56f, 0.64f, 0.58f, 1f));

            // --- The trail --------------------------------------------------------------------------------

            BuildTrail(result.Trail, lane, ref rng);

            bool NearTrail(Vector2 p, float clearance)
            {
                for (int i = 0; i + 1 < result.Trail.Count; i++)
                    if (DistanceToSegment(p, result.Trail[i], result.Trail[i + 1]) < clearance) return true;
                return false;
            }

            Transform floor = K.Group("Floor", root);
            Transform trailRoot = K.Group("Trail", root);
            Transform trees = K.Group("Trees", root);
            Transform under = K.Group("Undergrowth", root);

            // --- The ground under it -----------------------------------------------------------------------

            // Dark litter under the trees, thin at the edge where it breaks up into the grass and
            // closing into one shade further in. Soft stains rather than a slab, so there is no line.
            var litter = PoissonScatter.Sample(band, 5f, 9f,
                p => Mathf.Lerp(9f, 5f, Depth01(p)),
                p => L.Outside(p) > -3f && L.Outside(p) < L.ContentDepth + 2f,
                ref rng, maxPoints: 240);

            foreach (Vector2 p in litter)
            {
                float depth = Depth01(p);
                Color colour = depth < 0.1f && rng.Chance(0.5f) ? E.Pick(E.SunGrass, ref rng) : E.Pick(E.ForestFloor, ref rng);
                float size = rng.Range(8f, 13f) * (0.8f + depth * 0.5f);
                E.Blob("Litter", floor, p, size, colour, rng.Range(0, 2), ref rng, Mathf.Lerp(0.15f, 0.6f, depth));
            }

            OutskirtsBuilder.Trail(trailRoot, lane.Name + "_Forest", result.Trail, ref rng);

            // --- The back rows: whole clumps -------------------------------------------------------------------

            for (float x = band.xMin + 4f; x < band.xMax; x += rng.Range(11f, 16f))
            {
                var at = new Vector2(x, L.ClearMaxZ + rng.Range(19f, 26f));
                if (NearTrail(at, 9f)) continue;

                E.PackOf(E.TreePatches, trees, new Vector3(at.x, 0f, at.y), rng.Chance(0.5f) ? 0f : 180f,
                         rng.Range(0.8f, 1.1f), ref rng, dusk);
                result.Trees++;
            }

            // --- The trees one at a time ------------------------------------------------------------------------

            var points = PoissonScatter.Sample(band, 2.4f, 6f,
                p => Mathf.Lerp(6f, 2.4f, Mathf.Pow(Depth01(p), 0.7f)),
                p =>
                {
                    float outside = L.Outside(p);
                    if (outside < -1f || outside > L.ContentDepth) return false;
                    if (NearTrail(p, 3.4f)) return false;

                    // Small clearings, so the wood has glades in it rather than even stipple.
                    return PoissonScatter.Noise(p, 0.07f, 91) > 0.24f;
                }, ref rng, maxPoints: 650);

            int index = 0;
            foreach (Vector2 p in points)
            {
                float depth = Depth01(p);
                float roll = rng.NextFloat();
                string name = $"Tree_{index++:000}";

                if (depth < 0.16f)
                {
                    // The edge: dead wood and scrub on open ground.
                    if (roll < 0.45f) ObstacleKit.DeadTree(trees, name, p, ref rng, dusk);
                    else if (roll < 0.65f) E.PackOf(E.Stumps, trees, new Vector3(p.x, 0f, p.y), rng.Range(0f, 360f), rng.Range(0.9f, 1.3f), ref rng, dusk);
                    else if (roll < 0.8f) ObstacleKit.FallenLog(trees, name, p, rng.Range(0f, 360f), ref rng);
                    else ObstacleKit.Bushes(under, name, p, ref rng, dusk);
                }
                else if (roll < 0.12f + depth * 0.1f)
                {
                    ObstacleKit.DeadTree(trees, name, p, ref rng, dusk);
                }
                else if (depth > 0.45f && roll > 0.94f)
                {
                    E.Pack(E.BigTree, trees, new Vector3(p.x, 0f, p.y), rng.Range(0f, 360f), rng.Range(0.6f, 0.85f), dusk);
                }
                else
                {
                    float scale = Mathf.Lerp(1.7f, 2.6f, depth) * rng.Range(0.85f, 1.15f);
                    var tilt = new Vector3(rng.Range(-4f, 4f), 0f, rng.Range(-4f, 4f));
                    E.Pack(E.Trees[rng.Range(0, E.Trees.Length)], trees, new Vector3(p.x, 0f, p.y), rng.Range(0f, 360f), scale, dusk, tilt);
                }

                result.Trees++;
            }

            // --- Undergrowth ------------------------------------------------------------------------------------

            var growth = PoissonScatter.Sample(band, 3.5f, 6f,
                p => Mathf.Lerp(6f, 3.5f, Depth01(p)),
                p => L.Outside(p) > -3f && L.Outside(p) < L.ContentDepth * 0.8f && !NearTrail(p, 1.8f),
                ref rng, maxPoints: 300);

            foreach (Vector2 p in growth)
            {
                float roll = rng.NextFloat();
                var at = new Vector3(p.x, 0f, p.y);

                if (roll < 0.45f) E.PackOf(E.Bushes, under, at, rng.Range(0f, 360f), rng.Range(1.3f, 2.4f), ref rng, dusk);
                else if (roll < 0.75f) E.PackOf(E.Grass, under, at, rng.Range(0f, 360f), rng.Range(1.1f, 1.6f), ref rng, dusk);
                else if (roll < 0.9f) E.PackOf(E.Rocks, under, at, rng.Range(0f, 360f), rng.Range(1f, 2f), ref rng, dusk);
                else ObstacleKit.FallenLog(under, "Log", p, rng.Range(0f, 360f), ref rng);

                result.Undergrowth++;
            }

            return result;
        }

        /// <summary>0 at the edge of the clear ground, 1 where the fog is total.</summary>
        static float Depth01(Vector2 p) => Mathf.Clamp01(L.Outside(p) / L.ContentDepth);

        /// <summary>
        /// A winding path from deep in the trees to where the night's zombies step out of them, then
        /// dead straight: out on the open ground they walk a straight line down their corridor, and
        /// the worn dirt there (<see cref="OutskirtsBuilder"/>) has to be where they walk.
        /// </summary>
        static void BuildTrail(List<Vector2> points, L.Lane lane, ref DeterministicRandom rng)
        {
            var mouth = new Vector2(lane.Mouth.x, lane.Mouth.z);
            var spawn = new Vector2(lane.TrailStart.x, lane.TrailStart.z);

            float length = Vector2.Distance(mouth, spawn);
            Vector2 dir = (spawn - mouth) / length;
            var normal = new Vector2(-dir.y, dir.x);

            float phase = rng.Range(0f, Mathf.PI * 2f);
            float amplitude = rng.Range(2.5f, 4f);

            for (float t = 0f; t <= length; t += 1.5f)
            {
                float u = t / length;

                // The wiggle dies away toward the spawn, so the path meets the straight part smoothly.
                float sway = Mathf.Sin(u * Mathf.PI * 2.2f + phase) * amplitude * (1f - u);
                points.Add(mouth + dir * t + normal * sway);
            }

            points.Add(spawn);
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        static Material FindPackMaterial()
        {
            foreach (string guid in AssetDatabase.FindAssets("PolygonFarm_01_A t:Material"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null && m.name == "PolygonFarm_01_A") return m;
            }

            return null;
        }
    }
}
