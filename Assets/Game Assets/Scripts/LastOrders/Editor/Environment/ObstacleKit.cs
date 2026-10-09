using LastOrders.Core;
using UnityEngine;
using E = LastOrders.EditorTools.EnvKit;
using K = LastOrders.EditorTools.PrimitiveKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The things lying about after the end of the world, each one put together on the spot.
    ///
    /// None of these is a prop placed a hundred times. A junk pile is four to nine pieces
    /// stacked however they fell; a car is one of four body shapes, in one of three states, with
    /// whichever wheels it still has; a fence is a run of panels that bends, leans and has gaps.
    /// The same seed builds the same pile, and the next seed builds a different one.
    ///
    /// Every builder places its parts under one group, in that group's local space, and returns
    /// the group already positioned and turned.
    /// </summary>
    public static class ObstacleKit
    {
        public enum Kind
        {
            Junk,
            Car,
            Rocks,
            Fence,
            DeadTree,
            Log,
            Bushes,
            Crater,
            Sandbags,
            Barriers,
            Barrels,
            Tyres,
            Crates,
        }

        public static Transform Build(Kind kind, Transform parent, string name, Vector2 at, ref DeterministicRandom rng)
        {
            float yaw = rng.Range(0f, 360f);

            switch (kind)
            {
                case Kind.Car: return Car(parent, name, at, yaw, ref rng);
                case Kind.Rocks: return Rocks(parent, name, at, ref rng);
                case Kind.Fence: return FenceRun(parent, name, at, yaw, ref rng);
                case Kind.DeadTree: return DeadTree(parent, name, at, ref rng);
                case Kind.Log: return FallenLog(parent, name, at, yaw, ref rng);
                case Kind.Bushes: return Bushes(parent, name, at, ref rng);
                case Kind.Crater: return Crater(parent, name, at, ref rng);
                case Kind.Sandbags: return Sandbags(parent, name, at, yaw, ref rng);
                case Kind.Barriers: return Barriers(parent, name, at, yaw, ref rng);
                case Kind.Barrels: return Barrels(parent, name, at, ref rng);
                case Kind.Tyres: return Tyres(parent, name, at, ref rng);
                case Kind.Crates: return Crates(parent, name, at, ref rng);
                default: return JunkPile(parent, name, at, ref rng);
            }
        }

        static Transform Root(Transform parent, string name, Vector2 at, float yaw)
        {
            Transform root = K.Group(name, parent);
            root.localPosition = new Vector3(at.x, 0f, at.y);
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return root;
        }

        // --- A heap of whatever ---------------------------------------------------------------------

        public static Transform JunkPile(Transform parent, string name, Vector2 at, ref DeterministicRandom rng)
        {
            Transform root = Root(parent, name, at, rng.Range(0f, 360f));

            float spread = rng.Range(0.7f, 1.5f);
            int count = rng.Range(4, 10);

            for (int i = 0; i < count; i++)
            {
                float angle = rng.Range(0f, Mathf.PI * 2f);
                float r = rng.Range(0f, spread) * (i == 0 ? 0.2f : 1f);
                var p = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);

                // Later pieces land on earlier ones, so the middle of the heap stands highest.
                float lift = (1f - r / Mathf.Max(0.01f, spread)) * rng.Range(0f, 0.45f);
                var tilt = new Vector3(rng.Range(-35f, 35f), rng.Range(0f, 360f), rng.Range(-35f, 35f));

                switch (rng.Range(0, 6))
                {
                    case 0:
                        {
                            var size = new Vector3(rng.Range(0.4f, 0.9f), rng.Range(0.3f, 0.6f), rng.Range(0.4f, 0.8f));
                            E.Block($"Box_{i}", root, p + Vector3.up * (size.y * 0.5f + lift), size, tilt, E.Pick(E.Weathered, ref rng));
                            break;
                        }
                    case 1:
                        {
                            var size = new Vector3(rng.Range(1.4f, 2.6f), 0.08f, rng.Range(0.18f, 0.3f));
                            E.Block($"Plank_{i}", root, p + Vector3.up * (0.1f + lift), size,
                                    new Vector3(rng.Range(-12f, 12f), rng.Range(0f, 360f), rng.Range(-20f, 20f)), E.Pick(E.Weathered, ref rng));
                            break;
                        }
                    case 2:
                        {
                            GameObject tyre = K.Cylinder($"Tyre_{i}", root, p + Vector3.up * lift, 0.72f, 0.24f, K.Dark);
                            tyre.transform.localRotation = Quaternion.Euler(rng.Range(-25f, 25f), 0f, rng.Range(-25f, 25f));
                            break;
                        }
                    case 3:
                        {
                            GameObject drum = K.Cylinder($"Drum_{i}", root, p + Vector3.up * lift, 0.55f, 0.85f, E.Pick(E.Rust, ref rng));
                            if (rng.Chance(0.5f))
                            {
                                drum.transform.localPosition = p + Vector3.up * (0.28f + lift);
                                drum.transform.localRotation = Quaternion.Euler(90f, rng.Range(0f, 360f), 0f);
                            }
                            break;
                        }
                    case 4:
                        {
                            var size = new Vector3(rng.Range(0.5f, 1.1f), rng.Range(0.15f, 0.35f), rng.Range(0.4f, 0.9f));
                            E.Block($"Slab_{i}", root, p + Vector3.up * (size.y * 0.5f + lift), size, tilt, E.Pick(E.Concrete, ref rng));
                            break;
                        }
                    default:
                        {
                            var size = new Vector3(rng.Range(0.9f, 1.6f), 0.04f, rng.Range(0.6f, 1.1f));
                            E.Block($"Sheet_{i}", root, p + Vector3.up * (0.15f + lift), size,
                                    new Vector3(rng.Range(-30f, 30f), rng.Range(0f, 360f), rng.Range(-30f, 30f)), E.Pick(E.Scrap, ref rng));
                            break;
                        }
                }
            }

            if (rng.Chance(0.35f))
            {
                E.PackOf(E.Rubbish, root, new Vector3(rng.Range(-0.8f, 0.8f), 0f, rng.Range(-0.8f, 0.8f)),
                         rng.Range(0f, 360f), rng.Range(0.7f, 1f), ref rng);
            }

            return root;
        }

        // --- Cars ---------------------------------------------------------------------------------------

        public static Transform Car(Transform parent, string name, Vector2 at, float yaw, ref DeterministicRandom rng,
                                    bool allowPack = true)
        {
            // Now and then the pack's own wreck, for a silhouette the boxes cannot make.
            if (allowPack && rng.Chance(0.12f))
            {
                Transform packRoot = Root(parent, name, at, yaw);
                if (E.PackOf(E.Wrecks, packRoot, Vector3.zero, 0f, 0.9f, ref rng) != null) return packRoot;
                Object.DestroyImmediate(packRoot.gameObject);
            }

            Transform root = Root(parent, name, at, yaw);

            int shape = rng.Range(0, 4); // sedan, hatchback, van, pickup
            float length = shape switch { 0 => 4.3f, 1 => 3.7f, 2 => 4.8f, _ => 4.9f };
            float width = shape == 2 ? 2.0f : 1.85f;

            float state = rng.NextFloat();
            bool burnt = state < 0.25f;
            bool rusted = !burnt && state < 0.55f;

            Color paint = burnt ? E.Pick(E.Burnt, ref rng) : rusted ? E.Pick(E.Rust, ref rng) : E.Pick(E.CarPaint, ref rng);
            Color glass = burnt ? E.Scorch : K.Hex("3E4A52");

            int wheelsLeft = 0;
            var wheelAt = new[]
            {
                new Vector2(length * 0.32f, width * 0.5f), new Vector2(length * 0.32f, -width * 0.5f),
                new Vector2(-length * 0.32f, width * 0.5f), new Vector2(-length * 0.32f, -width * 0.5f),
            };

            bool[] hasWheel = new bool[4];
            for (int i = 0; i < 4; i++)
            {
                hasWheel[i] = !burnt && rng.Chance(0.78f);
                if (hasWheel[i]) wheelsLeft++;
            }

            float clearance = wheelsLeft >= 3 ? 0.28f : 0.12f;

            K.Box("Body", root, new Vector3(0f, clearance, 0f), new Vector3(length, 0.62f, width), paint);

            float bodyTop = clearance + 0.62f;
            float cabinLength = shape switch { 0 => 2.2f, 1 => 2.0f, 2 => 3.5f, _ => 1.7f };
            float cabinX = shape switch { 0 => -0.2f, 1 => -0.4f, 2 => -0.55f, _ => 0.85f };
            float cabinHeight = shape == 2 ? 0.95f : 0.55f;

            K.Box("Glass", root, new Vector3(cabinX, bodyTop, 0f), new Vector3(cabinLength, cabinHeight, width - 0.12f), glass);
            K.Box("Roof", root, new Vector3(cabinX, bodyTop + cabinHeight, 0f),
                  new Vector3(cabinLength - 0.25f, 0.08f, width - 0.2f), paint);

            if (shape == 3)
            {
                // The pickup's bed, open at the top and usually not empty.
                K.Box("Bed", root, new Vector3(-length * 0.28f, bodyTop, 0f), new Vector3(length * 0.4f, 0.05f, width - 0.2f), K.Dark);
                if (rng.Chance(0.6f))
                    E.Block("Load", root, new Vector3(-length * 0.28f, bodyTop + 0.2f, rng.Range(-0.3f, 0.3f)),
                            new Vector3(0.7f, 0.35f, 0.6f), new Vector3(0f, rng.Range(0f, 60f), 0f), E.Pick(E.Weathered, ref rng));
            }

            for (int i = 0; i < 4; i++)
            {
                if (!hasWheel[i]) continue;
                K.Log($"Wheel_{i}", root, new Vector3(wheelAt[i].x, 0.34f, wheelAt[i].y), 0.68f, 0.26f, K.Dark, 0f);
            }

            // A door hanging open or a bonnet up.
            if (rng.Chance(0.3f))
            {
                float side = rng.Chance(0.5f) ? 1f : -1f;
                E.Block("Door", root, new Vector3(cabinX + 0.4f, clearance + 0.45f, side * (width * 0.5f + 0.45f)),
                        new Vector3(1.0f, 0.62f, 0.06f), new Vector3(0f, side * rng.Range(35f, 70f), 0f), paint);
            }

            if (rng.Chance(0.2f))
            {
                E.Block("Bonnet", root, new Vector3(length * 0.36f, bodyTop + 0.35f, 0f),
                        new Vector3(0.9f, 0.05f, width - 0.2f), new Vector3(0f, 0f, rng.Range(40f, 65f)), paint);
            }

            // On the ground beside the car, not under it: a car on its side would stand its own shadow up.
            if (burnt || rng.Chance(0.2f))
                E.Blob($"{name}_Scorch", parent, at, length * 1.1f, E.Scorch, 2, ref rng, burnt ? 0.65f : 0.4f);

            // One in ten lies on its side.
            if (rng.Chance(0.1f))
            {
                root.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f);
                root.localPosition += Vector3.up * (width * 0.5f);
            }

            return root;
        }

        // --- Ground and growth ------------------------------------------------------------------------------

        public static Transform Rocks(Transform parent, string name, Vector2 at, ref DeterministicRandom rng)
        {
            Transform root = Root(parent, name, at, rng.Range(0f, 360f));

            int boulders = rng.Range(1, 4);
            for (int i = 0; i < boulders; i++)
            {
                float s = rng.Range(0.5f, 1.4f) * (i == 0 ? 1.3f : 1f);
                var size = new Vector3(s * rng.Range(0.9f, 1.4f), s * rng.Range(0.45f, 0.8f), s * rng.Range(0.8f, 1.2f));
                var p = new Vector3(rng.Range(-0.9f, 0.9f), size.y * 0.35f, rng.Range(-0.9f, 0.9f));
                GameObject rock = K.Sphere($"Boulder_{i}", root, p, size, E.Pick(E.Concrete, ref rng));
                rock.transform.localRotation = Quaternion.Euler(rng.Range(-15f, 15f), rng.Range(0f, 360f), rng.Range(-15f, 15f));
            }

            int pebbles = rng.Range(1, 3);
            for (int i = 0; i < pebbles; i++)
            {
                E.PackOf(E.Rocks, root, new Vector3(rng.Range(-1.5f, 1.5f), 0f, rng.Range(-1.5f, 1.5f)),
                         rng.Range(0f, 360f), rng.Range(0.8f, 1.6f), ref rng);
            }

            if (rng.Chance(0.5f))
                E.PackOf(E.Grass, root, new Vector3(rng.Range(-1f, 1f), 0f, rng.Range(-1f, 1f)), rng.Range(0f, 360f),
                         rng.Range(1f, 1.5f), ref rng);

            return root;
        }

        public static Transform DeadTree(Transform parent, string name, Vector2 at, ref DeterministicRandom rng,
                                         Material material = null)
        {
            Transform root = Root(parent, name, at, rng.Range(0f, 360f));

            var lean = rng.Chance(0.3f) ? new Vector3(rng.Range(-10f, 10f), 0f, rng.Range(-10f, 10f)) : Vector3.zero;
            GameObject tree = E.Pack(E.DeadTree, root, Vector3.zero, 0f, rng.Range(0.8f, 1.35f), material, lean);

            if (tree == null)
            {
                float height = rng.Range(3.6f, 5.6f);
                K.Cylinder("Trunk", root, Vector3.zero, 0.36f, height, E.Bark);

                int branches = rng.Range(3, 6);
                for (int i = 0; i < branches; i++)
                {
                    K.Log($"Branch_{i}", root, new Vector3(0f, height * (0.5f + i * 0.1f), 0f), 0.14f,
                          rng.Range(1.2f, 2.4f), E.Bark, rng.Range(0f, 360f), rng.Range(-34f, -14f));
                }
            }

            if (rng.Chance(0.4f))
                E.PackOf(E.Grass, root, new Vector3(rng.Range(-0.8f, 0.8f), 0f, rng.Range(-0.8f, 0.8f)), 0f,
                         rng.Range(1f, 1.5f), ref rng);

            return root;
        }

        public static Transform FallenLog(Transform parent, string name, Vector2 at, float yaw, ref DeterministicRandom rng)
        {
            Transform root = Root(parent, name, at, yaw);

            float diameter = rng.Range(0.35f, 0.6f);
            float length = rng.Range(2.4f, 5f);
            K.Log("Log", root, new Vector3(0f, diameter * 0.45f, 0f), diameter, length, E.Bark, 90f, rng.Range(-4f, 4f));

            if (rng.Chance(0.5f))
                E.PackOf(E.Stumps, root, new Vector3(-length * 0.5f - 0.6f, 0f, 0f), rng.Range(0f, 360f), rng.Range(0.8f, 1.2f), ref rng);

            if (rng.Chance(0.5f))
                E.PackOf(E.Bushes, root, new Vector3(rng.Range(-1f, 1f), 0f, rng.Range(0.6f, 1.2f)), rng.Range(0f, 360f),
                         rng.Range(1.2f, 2f), ref rng);

            return root;
        }

        public static Transform Bushes(Transform parent, string name, Vector2 at, ref DeterministicRandom rng,
                                       Material material = null)
        {
            Transform root = Root(parent, name, at, rng.Range(0f, 360f));

            int count = rng.Range(2, 6);
            for (int i = 0; i < count; i++)
            {
                var p = new Vector3(rng.Range(-1.4f, 1.4f), 0f, rng.Range(-1.4f, 1.4f));
                string[] set = rng.Chance(0.7f) ? E.Bushes : E.Grass;
                float scale = set == E.Bushes ? rng.Range(1.3f, 2.2f) : rng.Range(1f, 1.6f);
                E.PackOf(set, root, p, rng.Range(0f, 360f), scale, ref rng, material);
            }

            return root;
        }

        public static Transform Crater(Transform parent, string name, Vector2 at, ref DeterministicRandom rng)
        {
            Transform root = Root(parent, name, at, rng.Range(0f, 360f));

            float size = rng.Range(2.2f, 4.2f);
            E.Blob("Scorch", root, Vector2.zero, size * 1.9f, E.Scorch, 2, ref rng, 0.55f);
            GroundSplats.Splat("Pit", root, Vector2.zero, size * 0.9f, size * 0.75f, rng.Range(0f, 360f), E.Mud[1], 0.85f, 3, ref rng);

            int chunks = rng.Range(5, 10);
            for (int i = 0; i < chunks; i++)
            {
                float angle = i / (float)chunks * Mathf.PI * 2f + rng.Range(-0.3f, 0.3f);
                float r = size * rng.Range(0.42f, 0.62f);
                var s = new Vector3(rng.Range(0.25f, 0.6f), rng.Range(0.12f, 0.3f), rng.Range(0.2f, 0.5f));

                E.Block($"Chunk_{i}", root, new Vector3(Mathf.Cos(angle) * r, s.y * 0.4f, Mathf.Sin(angle) * r), s,
                        new Vector3(rng.Range(-25f, 25f), rng.Range(0f, 360f), rng.Range(-25f, 25f)),
                        rng.Chance(0.5f) ? K.Soil : E.Pick(E.Concrete, ref rng));
            }

            return root;
        }

        // --- What people built to keep things out --------------------------------------------------------------

        public static Transform FenceRun(Transform parent, string name, Vector2 at, float yaw, ref DeterministicRandom rng)
        {
            Transform root = Root(parent, name, at, yaw);

            int panels = rng.Range(2, 6);
            Vector2 cursor = Vector2.zero;
            float heading = 0f;
            string style = E.WoodFences[rng.Range(0, E.WoodFences.Length)];

            for (int i = 0; i < panels; i++)
            {
                heading += rng.Range(-22f, 22f);
                var dir = new Vector2(Mathf.Cos(heading * Mathf.Deg2Rad), -Mathf.Sin(heading * Mathf.Deg2Rad));
                Vector2 end = cursor + dir * 2.5f;

                if (!rng.Chance(0.18f))
                {
                    // Standing, leaning, or flat on the ground.
                    Vector3 tilt = rng.Chance(0.12f)
                        ? new Vector3(rng.Chance(0.5f) ? 84f : -84f, 0f, 0f)
                        : new Vector3(rng.Range(-18f, 18f), 0f, rng.Range(-4f, 4f));

                    GameObject panel = E.Pack(style, root, new Vector3(end.x, 0f, end.y), heading, 1f, null, tilt);
                    if (panel == null)
                    {
                        E.Block($"Panel_{i}", root, new Vector3((cursor.x + end.x) * 0.5f, 0.6f, (cursor.y + end.y) * 0.5f),
                                new Vector3(2.5f, 0.9f, 0.06f), new Vector3(tilt.x, heading, tilt.z), E.Pick(E.Weathered, ref rng));
                    }
                }

                K.Box($"Post_{i}", root, new Vector3(cursor.x, 0f, cursor.y), new Vector3(0.14f, rng.Range(0.9f, 1.4f), 0.14f), K.WoodDark);
                cursor = end;
            }

            return root;
        }

        public static Transform Sandbags(Transform parent, string name, Vector2 at, float yaw, ref DeterministicRandom rng)
        {
            Transform root = Root(parent, name, at, yaw);

            float radius = rng.Range(1.6f, 2.8f);
            float arc = rng.Range(70f, 150f);
            int bags = Mathf.RoundToInt(arc * Mathf.Deg2Rad * radius / 0.54f);
            int rows = rng.Range(2, 4);

            // Flat and packed tight, courses staggered like brickwork: a line of loose round bags
            // reads as a clutch of eggs.
            for (int row = 0; row < rows; row++)
            {
                for (int i = 0; i < bags - row; i++)
                {
                    if (rng.Chance(row == rows - 1 ? 0.25f : 0.05f)) continue;

                    float t = (i + row * 0.5f) / Mathf.Max(1, bags - 1);
                    float a = (-arc * 0.5f + t * arc) * Mathf.Deg2Rad;
                    var p = new Vector3(Mathf.Sin(a) * radius, 0.12f + row * 0.2f, Mathf.Cos(a) * radius);

                    E.Block($"Bag_{row}_{i}", root, p, new Vector3(0.58f, 0.2f, 0.36f),
                            new Vector3(rng.Range(-5f, 5f), a * Mathf.Rad2Deg + 90f + rng.Range(-7f, 7f), rng.Range(-4f, 4f)),
                            E.Pick(E.Sandbag, ref rng));
                }
            }

            return root;
        }

        public static Transform Barriers(Transform parent, string name, Vector2 at, float yaw, ref DeterministicRandom rng)
        {
            Transform root = Root(parent, name, at, yaw);

            int count = rng.Range(2, 5);
            float x = 0f;
            for (int i = 0; i < count; i++)
            {
                x += rng.Range(1.9f, 2.6f);
                if (rng.Chance(0.15f)) continue;

                bool toppled = rng.Chance(0.1f);

                Transform block = K.Group($"Barrier_{i}", root);
                block.localPosition = new Vector3(x, toppled ? 0.31f : 0f, rng.Range(-0.35f, 0.35f));
                block.localRotation = Quaternion.Euler(toppled ? 90f : 0f, rng.Range(-14f, 14f), 0f);

                Color c = E.Pick(E.Concrete, ref rng);
                K.Box("Base", block, Vector3.zero, new Vector3(1.9f, 0.4f, 0.62f), c);
                K.Box("Top", block, new Vector3(0f, 0.4f, 0f), new Vector3(1.9f, 0.42f, 0.28f), c);
            }

            return root;
        }

        public static Transform Barrels(Transform parent, string name, Vector2 at, ref DeterministicRandom rng)
        {
            Transform root = Root(parent, name, at, rng.Range(0f, 360f));

            int count = rng.Range(2, 6);
            for (int i = 0; i < count; i++)
            {
                var p = new Vector3(rng.Range(-1f, 1f), 0f, rng.Range(-1f, 1f));
                bool tipped = rng.Chance(0.35f);
                Vector3 tilt = tipped ? new Vector3(90f, 0f, 0f) : Vector3.zero;
                if (tipped) p.y = 0.45f;

                if (E.Pack(E.Drums[rng.Range(0, E.Drums.Length)], root, p, rng.Range(0f, 360f), rng.Range(0.8f, 1f), null, tilt) == null)
                {
                    GameObject drum = K.Cylinder($"Drum_{i}", root, p, 0.6f, 0.9f, E.Pick(E.Rust, ref rng));
                    drum.transform.localRotation = Quaternion.Euler(tilt);
                }
            }

            return root;
        }

        public static Transform Tyres(Transform parent, string name, Vector2 at, ref DeterministicRandom rng)
        {
            Transform root = Root(parent, name, at, rng.Range(0f, 360f));

            int stacks = rng.Range(1, 3);
            for (int s = 0; s < stacks; s++)
            {
                var basePoint = new Vector3(rng.Range(-0.9f, 0.9f), 0f, rng.Range(-0.9f, 0.9f));
                int height = rng.Range(1, 5);

                for (int i = 0; i < height; i++)
                {
                    var p = basePoint + new Vector3(rng.Range(-0.08f, 0.08f), i * 0.22f, rng.Range(-0.08f, 0.08f));
                    if (E.Pack(E.Tyre, root, p, rng.Range(0f, 360f), 0.45f) == null)
                        K.Cylinder($"Tyre_{s}_{i}", root, p, 0.75f, 0.22f, K.Dark);
                }
            }

            return root;
        }

        public static Transform Crates(Transform parent, string name, Vector2 at, ref DeterministicRandom rng)
        {
            Transform root = Root(parent, name, at, rng.Range(0f, 360f));

            int count = rng.Range(2, 5);
            for (int i = 0; i < count; i++)
            {
                var p = new Vector3(rng.Range(-1.1f, 1.1f), 0f, rng.Range(-1.1f, 1.1f));
                string pick = rng.Chance(0.5f) ? E.Crate : rng.Chance(0.5f) ? E.PalletCrate : E.WoodStack;
                Vector3 tilt = rng.Chance(0.2f) ? new Vector3(0f, 0f, rng.Range(15f, 30f)) : Vector3.zero;

                if (E.Pack(pick, root, p, rng.Range(0f, 360f), rng.Range(0.9f, 1.2f), null, tilt) == null)
                    K.Box($"Crate_{i}", root, p, Vector3.one * 0.8f, K.Wood, rng.Range(0f, 90f));
            }

            return root;
        }

        // --- Street furniture ---------------------------------------------------------------------------------

        /// <summary>A street lamp: standing dark, leaning, or down across the pavement.</summary>
        public static Transform Streetlight(Transform parent, string name, Vector2 at, float facing, ref DeterministicRandom rng)
        {
            Transform root = Root(parent, name, at, facing);

            float state = rng.NextFloat();
            Transform pole = K.Group("Pole", root);

            K.Cylinder("Shaft", pole, Vector3.zero, 0.14f, 5.2f, K.MetalDark);
            K.Log("Arm", pole, new Vector3(0f, 5.1f, 0.55f), 0.08f, 1.2f, K.MetalDark, 0f);
            K.Box("Head", pole, new Vector3(0f, 4.95f, 1.1f), new Vector3(0.32f, 0.14f, 0.55f), K.Dark);

            if (state < 0.25f)
            {
                // Down: lying on the ground toward the street, broken off at the foot.
                pole.localRotation = Quaternion.Euler(88f, 0f, rng.Range(-8f, 8f));
                pole.localPosition = new Vector3(0f, 0.1f, 0f);
                K.Cylinder("Stub", root, Vector3.zero, 0.16f, 0.35f, K.MetalDark);
            }
            else if (state < 0.45f)
            {
                pole.localRotation = Quaternion.Euler(rng.Range(8f, 18f), 0f, rng.Range(-6f, 6f));
            }

            return root;
        }

        /// <summary>A few black bags and a tipped bin, the way a kerb looks when nobody comes any more.</summary>
        public static Transform Trash(Transform parent, string name, Vector2 at, ref DeterministicRandom rng)
        {
            Transform root = Root(parent, name, at, rng.Range(0f, 360f));

            int bags = rng.Range(2, 6);
            for (int i = 0; i < bags; i++)
            {
                float s = rng.Range(0.4f, 0.65f);
                K.Sphere($"Bag_{i}", root, new Vector3(rng.Range(-0.6f, 0.6f), s * 0.35f, rng.Range(-0.6f, 0.6f)),
                         new Vector3(s, s * 0.75f, s * 0.9f), rng.Chance(0.6f) ? K.Dark : K.Hex("3E4A3A"));
            }

            if (rng.Chance(0.6f))
            {
                Vector3 tilt = rng.Chance(0.6f) ? new Vector3(0f, 0f, 90f) : Vector3.zero;
                E.Pack(E.Bin, root, new Vector3(rng.Range(-0.6f, 0.6f), tilt.z > 0f ? 0.3f : 0f, rng.Range(-0.6f, 0.6f)),
                       rng.Range(0f, 360f), 1.3f, null, tilt);
            }

            return root;
        }
    }
}
