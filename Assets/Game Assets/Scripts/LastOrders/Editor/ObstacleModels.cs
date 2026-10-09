using System;
using System.Collections.Generic;
using LastOrders.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using B = LastOrders.EditorTools.BuildingKit;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The obstacles that cover the land round the compound until it is cleared - boulders, fallen
    /// trees, root stumps, thickets, car wrecks, scrap heaps, rubble and old barricades - modelled in
    /// code in the game's smooth low-poly style, like the shelter hut and the storehouse (see
    /// <see cref="BuildingKit"/>): rounded chunky shapes with smooth normals, colour painted into
    /// the vertices and lit by LastOrders/Stylized, one renderer each.
    ///
    /// Three variants of each kind, built from a fixed key, so a rebuild gives the same models
    /// and every prefab and scene object pointing at one keeps pointing at it. Colour comes from the
    /// objects - grey stone with fresh moss, brown bark, green leaves, faded car paint - and never
    /// from a tint over everything, so nothing reads orange. Painted a little darker than it shows,
    /// as the buildings are: the sun and the sky lift a lit face by nearly half again.
    ///
    /// Each model stands on the ground at its origin, roughly centred, and is grounded by a darker
    /// band at its foot. The land setup (<see cref="LandPlotSetup"/>) puts them out as debris.
    /// </summary>
    public static partial class ObstacleModels
    {
        public const string Folder = "Assets/Game Assets/Content/LastOrders/Obstacles/" + EnvKit.Style;

        public enum Kind
        {
            Boulders,
            FallenTree,
            Stump,
            Thicket,
            CarWreck,
            ScrapHeap,
            Rubble,
            Barricade,
        }

        public const int Variants = 3;

        public static readonly Kind[] Kinds = (Kind[])Enum.GetValues(typeof(Kind));

        public static string Name(Kind kind, int variant) => $"Obstacle_{kind}_{(char)('A' + variant)}";

        public static string MeshPath(Kind kind, int variant) => $"{Folder}/{Name(kind, variant)}.asset";

        public static string PrefabPath(Kind kind, int variant) => $"{Folder}/{Name(kind, variant)}.prefab";

        public static GameObject LoadPrefab(Kind kind, int variant) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(kind, variant));

        /// <summary>True when every model's prefab is on disk.</summary>
        public static bool AllBuilt()
        {
            foreach (Kind kind in Kinds)
                for (int v = 0; v < Variants; v++)
                    if (LoadPrefab(kind, v) == null) return false;

            return true;
        }

        [MenuItem("Last Orders/Expansion/Build Obstacle Models", priority = 31)]
        public static void Run() => Debug.Log(Apply());

        /// <summary>Builds every model into its mesh and prefab, rewriting those already there in place.</summary>
        public static string Apply()
        {
            ContentGenerator.EnsureFolder(Folder);

            int built = 0, vertices = 0;
            foreach (Kind kind in Kinds)
            {
                for (int v = 0; v < Variants; v++)
                {
                    B.Model model = Build(kind, v);
                    vertices += model.VertexCount;

                    Mesh mesh = model.Save(MeshPath(kind, v), Name(kind, v));
                    SavePrefab(kind, v, mesh);
                    built++;
                }
            }

            return $"[Last Orders] {built} obstacle models built into {Folder} ({vertices} vertices in all).";
        }

        static void SavePrefab(Kind kind, int variant, Mesh mesh)
        {
            string path = PrefabPath(kind, variant);
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(Name(kind, variant));

            try
            {
                B.Show(root, mesh);
                var renderer = root.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        /// <summary>One model, from its kind and variant alone.</summary>
        public static B.Model Build(Kind kind, int variant)
        {
            var m = new B.Model { Shade = GroundShade };
            int key = 1000 + (int)kind * 97 + variant * 31;
            var rng = new DeterministicRandom(key);

            switch (kind)
            {
                case Kind.Boulders: Boulders(m, variant, key, ref rng); break;
                case Kind.FallenTree: FallenTree(m, variant, key, ref rng); break;
                case Kind.Stump: Stumps(m, variant, key, ref rng); break;
                case Kind.Thicket: Thicket(m, variant, key, ref rng); break;
                case Kind.CarWreck: CarWreck(m, variant, key, ref rng); break;
                case Kind.ScrapHeap: ScrapHeap(m, variant, key, ref rng); break;
                case Kind.Rubble: Rubble(m, variant, key, ref rng); break;
                default: Barricade(m, variant, key, ref rng); break;
            }

            return m;
        }

        // --- The palette ---------------------------------------------------------------------------

        /// <summary>Mid grey: any lighter and a face turned to the sun clips white.</summary>
        static readonly Color[] Stone = { B.Hex("807E79"), B.Hex("7A7974"), B.Hex("85837E"), B.Hex("7C8081") };
        static readonly Color[] Moss = { B.Hex("5D7B2F"), B.Hex("678833"), B.Hex("55722B") };
        static readonly Color[] Bark = { B.Hex("5E4630"), B.Hex("664C35"), B.Hex("57422E") };
        static readonly Color BarkDark = B.Hex("47372A");
        static readonly Color HeartDark = B.Hex("8C7352");
        static readonly Color HeartMid = B.Hex("A58A63");
        static readonly Color HeartLight = B.Hex("B9A078");
        static readonly Color SapWood = B.Hex("C2AB84");
        static readonly Color BrokenWood = B.Hex("A88E68");
        /// <summary>A shade deeper and cooler than the lawn, so a thicket stands out of the grass it grows in.</summary>
        static readonly Color[] Leaves = { B.Hex("3F6B2A"), B.Hex("4A7530"), B.Hex("416628"), B.Hex("4F7C33") };
        static readonly Color LeafLight = B.Hex("6E9438");
        static readonly Color[] Grass = { B.Hex("5F8C33"), B.Hex("6B9A3A"), B.Hex("66923A") };
        static readonly Color GrassRoot = B.Hex("3F5F25");
        static readonly Color[] Blossom = { B.Hex("E8E6D6"), B.Hex("E6C23C"), B.Hex("D9899B") };
        static readonly Color Earth = B.Hex("6E5A45");
        static readonly Color EarthDark = B.Hex("594838");
        static readonly Color MushroomCap = B.Hex("B5483A");
        static readonly Color MushroomGill = B.Hex("D6CBB2");

        // --- Shared shaping ----------------------------------------------------------------------------

        /// <summary>Darker at the foot of everything, where it meets the grass, so nothing floats.</summary>
        static float GroundShade(Vector3 p, Vector3 n)
        {
            float k = Mathf.Lerp(0.72f, 1f, Smooth(p.y / 0.32f));
            if (n.y < -0.3f) k *= 0.86f;
            return k;
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>0 below <paramref name="from"/>, 1 above <paramref name="to"/>, eased between.</summary>
        static float Band(float x, float from, float to) => Smooth((x - from) / (to - from));

        /// <summary>Smooth 0..1 noise through space, the same for the same point every build.</summary>
        static float Noise(Vector3 p) =>
            (Mathf.PerlinNoise(p.x + 31.7f, p.y + 11.3f) + Mathf.PerlinNoise(p.y + 5.1f, p.z + 47.9f) +
             Mathf.PerlinNoise(p.z + 71.3f, p.x + 23.9f)) / 3f;

        static Vector3 Seed(int key) => new Vector3(key * 0.137f % 50f, key * 0.291f % 50f, key * 0.173f % 50f);

        static float Hash01(int key) => B.Hash(key) % 10007 / 10007f;

        static Color Pick(Color[] palette, int key) => palette[B.Hash(key) % palette.Length];

        /// <summary>A direction on the sphere, stable for its key.</summary>
        static Vector3 Dir(int key)
        {
            float a = Hash01(key * 3 + 1) * Mathf.PI * 2f;
            float y = Hash01(key * 3 + 2) * 2f - 1f;
            float r = Mathf.Sqrt(1f - y * y);
            return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        }

        /// <summary>The unit sphere's point at (u round, v up from the bottom pole).</summary>
        static Vector3 Sphere(float u, float v, out float around, out float polar)
        {
            around = u * Mathf.PI * 2f;
            polar = Mathf.PI * (1f - v);
            return new Vector3(Mathf.Sin(polar) * Mathf.Cos(around), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(around));
        }

        /// <summary>Adds everything <paramref name="build"/> adds as if it stood at <paramref name="at"/>, turned by <paramref name="turn"/>.</summary>
        static void Placed(B.Model m, Vector3 at, Quaternion turn, Action build) => Placed(m, Matrix4x4.TRS(at, turn, Vector3.one), build);

        static void Placed(B.Model m, Matrix4x4 local, Action build)
        {
            Matrix4x4 old = m.Transform;
            m.Transform = old * local;
            try { build(); }
            finally { m.Transform = old; }
        }

        // --- Stone -----------------------------------------------------------------------------------------

        /// <summary>
        /// A boulder: a rounded lump cut by a few broad faces, so it reads as rock rather than a pebble -
        /// flat-ish facets meeting at soft edges - sitting a little into the ground on a flat underside.
        /// Moss on the faces that look at the sky.
        /// </summary>
        static void Rock(B.Model m, Vector3 foot, Vector3 size, float yaw, int key, float moss)
        {
            const int Planes = 10;
            const double Softness = 0.1;

            var normals = new Vector3[Planes];
            var distances = new float[Planes];

            normals[0] = (Vector3.up + Dir(key) * 0.35f).normalized;
            distances[0] = 0.8f + 0.1f * Hash01(key + 1);
            normals[1] = Vector3.down;
            distances[1] = 0.5f;

            for (int k = 2; k < Planes; k++)
            {
                Vector3 d = Dir(key * 13 + k);
                d.y *= 0.55f;
                normals[k] = d.normalized;
                distances[k] = 0.84f + 0.15f * Hash01(key * 7 + k);
            }

            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 half = size * 0.5f;
            Vector3 centre = foot + Vector3.up * (half.y * 0.5f - 0.05f);
            Color stone = Pick(Stone, key);
            Vector3 seed = Seed(key);

            Vector3 At(float u, float v)
            {
                Vector3 dir = Sphere(u, v, out _, out _);

                // The nearest face wins, smoothly, so the faces meet at rounded edges.
                double sum = Math.Exp(-1.3 / Softness);
                for (int k = 0; k < Planes; k++)
                {
                    float c = Vector3.Dot(dir, normals[k]);
                    if (c > 0.02f) sum += Math.Exp(-(distances[k] / c) / Softness);
                }

                float r = (float)(-Softness * Math.Log(sum));
                r *= 1f + 0.07f * (Noise(dir * 2.3f + seed) - 0.5f);
                return centre + turn * Vector3.Scale(dir * r, half);
            }

            Color Paint(Vector3 p, Vector3 n)
            {
                float grain = Noise(p * 3.1f + seed);
                Color c = stone * (0.9f + 0.2f * grain) * Mathf.Lerp(0.88f, 1.04f, n.y * 0.5f + 0.5f);

                // In patches on the faces turned to the sky, never a cap over the whole top.
                float w = Band(n.y, 0.45f, 0.8f) * Band(Noise(p * 1.7f + seed * 1.3f), 0.47f, 0.6f) * moss;
                return Color.Lerp(c, Pick(Moss, key + (int)(grain * 7f)), w);
            }

            m.Surface(Paint, 36, 20, At, wrapU: true);
        }

        static void Boulders(B.Model m, int variant, int key, ref DeterministicRandom rng)
        {
            switch (variant)
            {
                case 0:
                    Rock(m, Vector3.zero, new Vector3(1.9f, 1.25f, 1.6f), 20f, key, 0.6f);
                    Rock(m, new Vector3(1.15f, 0f, -0.55f), new Vector3(0.95f, 0.65f, 0.85f), 75f, key + 1, 0.4f);
                    Rock(m, new Vector3(-0.85f, 0f, 0.75f), new Vector3(0.6f, 0.4f, 0.55f), 140f, key + 2, 0.3f);
                    break;

                case 1:
                    Rock(m, new Vector3(-0.55f, 0f, 0.1f), new Vector3(1.45f, 1.05f, 1.3f), 60f, key, 0.5f);
                    Rock(m, new Vector3(0.75f, 0f, -0.2f), new Vector3(1.25f, 0.85f, 1.15f), 200f, key + 1, 0.55f);
                    Rock(m, new Vector3(0.1f, 0f, -0.95f), new Vector3(0.45f, 0.3f, 0.4f), 10f, key + 2, 0.2f);
                    break;

                default:
                    Rock(m, new Vector3(0.1f, 0f, 0f), new Vector3(2.3f, 1.5f, 1.85f), 110f, key, 0.85f);
                    Rock(m, new Vector3(-1.3f, 0f, -0.5f), new Vector3(0.75f, 0.55f, 0.7f), 30f, key + 1, 0.5f);
                    Rock(m, new Vector3(1.35f, 0f, 0.65f), new Vector3(0.7f, 0.45f, 0.6f), 300f, key + 2, 0.4f);
                    Rock(m, new Vector3(0.6f, 0f, -1.15f), new Vector3(0.42f, 0.3f, 0.4f), 80f, key + 3, 0.3f);
                    break;
            }

            Tufts(m, 1.3f, 4 + variant, key + 40, ref rng);
        }

        // --- Growth ----------------------------------------------------------------------------------------

        /// <summary>A clump of grass blades fanning out of one spot.</summary>
        static void Tuft(B.Model m, Vector3 foot, float height, int key)
        {
            int blades = 6 + B.Hash(key) % 4;
            Color tip = Pick(Grass, key);

            for (int i = 0; i < blades; i++)
            {
                float a = (i + Hash01(key * 11 + i) * 0.7f) / blades * Mathf.PI * 2f;
                float lean = 0.25f + 0.4f * Hash01(key * 13 + i);
                float h = height * (0.6f + 0.5f * Hash01(key * 17 + i));
                var out2 = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 from = foot + out2 * 0.04f - Vector3.up * 0.02f;
                Vector3 to = from + (out2 * lean + Vector3.up).normalized * h;

                m.Tube((p, n) => Color.Lerp(GrassRoot, tip, Smooth((p.y - foot.y) / h)), from, to, 0.028f, 0.003f, 4);
            }
        }

        /// <summary>A few tufts round the foot of an obstacle, <paramref name="reach"/> metres out.</summary>
        static void Tufts(B.Model m, float reach, int count, int key, ref DeterministicRandom rng)
        {
            for (int i = 0; i < count; i++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f);
                float r = reach * rng.Range(0.7f, 1.1f);
                Tuft(m, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), rng.Range(0.28f, 0.45f), key + i);
            }
        }

        /// <summary>
        /// A leafy mound: a lumpy ball of leaves squashed onto the ground, lighter where the sky
        /// reaches it, darker underneath, flecked with brighter leaves.
        /// </summary>
        static Func<float, float, Vector3> Bush(B.Model m, Vector3 foot, float radius, int key, Color leaf, float height = 1f,
                                                Color? fleck = null)
        {
            Vector3 centre = foot + Vector3.up * radius * 0.6f * height;
            Vector3 seed = Seed(key);
            Color flecks = fleck ?? LeafLight;

            Vector3 At(float u, float v)
            {
                // Lumps from noise through space only: anything that runs round the ball pinches
                // into a star where it meets itself at the top.
                Vector3 dir = Sphere(u, v, out _, out _);
                float r = 1f + 0.26f * (Noise(dir * 1.9f + seed) - 0.5f) + 0.12f * (Noise(dir * 4.6f + seed * 1.7f) - 0.5f);
                Vector3 q = dir * (r * radius);
                q.y *= height;

                float floor = -0.6f * radius * height + 0.02f;
                if (q.y < floor) q.y = floor + (q.y - floor) * 0.08f;
                return centre + q;
            }

            Color Paint(Vector3 p, Vector3 n)
            {
                Color c = leaf * Mathf.Lerp(0.7f, 1.04f, n.y * 0.5f + 0.5f);
                float spot = Band(Noise(p * 5f + seed), 0.56f, 0.66f);
                return Color.Lerp(c, flecks, spot * 0.45f * Mathf.Clamp01(n.y + 0.4f));
            }

            m.Surface(Paint, 24, 14, At, wrapU: true);
            return At;
        }

        /// <summary>Small round flowers dotted over the top of a bush.</summary>
        static void Flowers(B.Model m, Func<float, float, Vector3> bush, int count, int key)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 at = bush(Hash01(key * 5 + i), 0.6f + 0.35f * Hash01(key * 7 + i));
                Color c = Pick(Blossom, key + i);
                var head = new SmoothMesh(1);
                head.RoundedBox(0, at, new Vector3(0.09f, 0.06f, 0.09f), 0.028f, Quaternion.Euler(0f, i * 37f, 0f), 2);
                m.Add(head, c);
            }
        }

        /// <summary>A thorny stem arching out of a thicket, in three bends.</summary>
        static void Bramble(B.Model m, Vector3 from, Vector3 heading, float length, int key)
        {
            heading.y = 0f;
            heading.Normalize();
            Vector3 a = from;
            for (int i = 0; i < 3; i++)
            {
                float t = (i + 1) / 3f;
                Vector3 b = from + heading * (length * t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * length * 0.35f + 0.05f) +
                            Vector3.Cross(heading, Vector3.up) * (0.12f * (Hash01(key + i) - 0.5f));
                m.Tube(Color.Lerp(BarkDark, B.Hex("5B4A2E"), t), a, b, 0.022f * (1.1f - t * 0.4f), 6);
                a = b;
            }
        }

        static void Thicket(B.Model m, int variant, int key, ref DeterministicRandom rng)
        {
            var lobes = new List<(Vector3 at, float r)>();

            switch (variant)
            {
                case 0:
                    lobes.Add((new Vector3(0f, 0f, 0f), 0.95f));
                    lobes.Add((new Vector3(0.95f, 0f, 0.35f), 0.75f));
                    lobes.Add((new Vector3(-0.85f, 0f, 0.4f), 0.7f));
                    lobes.Add((new Vector3(0.3f, 0f, -0.8f), 0.68f));
                    lobes.Add((new Vector3(-0.55f, 0f, -0.65f), 0.55f));
                    break;

                case 1:
                    lobes.Add((new Vector3(-0.45f, 0f, 0.1f), 1.05f));
                    lobes.Add((new Vector3(0.7f, 0f, -0.15f), 0.9f));
                    lobes.Add((new Vector3(0.15f, 0f, 0.85f), 0.62f));
                    break;

                default:
                    // Round a dead sapling: bare stick and branches standing out of the growth.
                    Vector3 top = new Vector3(0.15f, 2.1f, -0.1f);
                    m.Tube((p, n) => Pick(Bark, key), Vector3.zero - Vector3.up * 0.05f, top, 0.09f, 0.035f, 8);
                    for (int b = 0; b < 4; b++)
                    {
                        float a = b * 1.7f + 0.4f;
                        Vector3 from = Vector3.Lerp(Vector3.zero, top, 0.45f + b * 0.13f);
                        Vector3 to = from + new Vector3(Mathf.Cos(a) * 0.65f, 0.45f, Mathf.Sin(a) * 0.65f);
                        m.Tube((p, n) => BarkDark, from, to, 0.035f, 0.012f, 6);
                    }

                    lobes.Add((new Vector3(0.7f, 0f, 0.2f), 0.8f));
                    lobes.Add((new Vector3(-0.65f, 0f, 0.35f), 0.75f));
                    lobes.Add((new Vector3(0f, 0f, -0.75f), 0.7f));
                    break;
            }

            for (int i = 0; i < lobes.Count; i++)
            {
                Func<float, float, Vector3> bush = Bush(m, lobes[i].at, lobes[i].r, key + i * 7, Pick(Leaves, key + i), rng.Range(0.85f, 1.05f));
                if (variant == 1) Flowers(m, bush, 5, key + i * 3);
            }

            for (int i = 0; i < 3 + variant; i++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Bramble(m, dir * 0.6f + Vector3.up * 0.25f, dir, rng.Range(0.8f, 1.3f), key + 60 + i * 5);
            }

            Tufts(m, 1.4f, 4, key + 80, ref rng);
        }

        // --- Wood --------------------------------------------------------------------------------------

        /// <summary>
        /// A log from <paramref name="a"/> to <paramref name="b"/>: grooved bark with moss along its top,
        /// and at each end either the rings of a saw cut or the pale splintered wood of a break.
        /// </summary>
        static void Log(B.Model m, Vector3 a, Vector3 b, float radiusA, float radiusB, int key, bool cutA = true, bool cutB = true)
        {
            float length = Vector3.Distance(a, b);
            if (length < 0.05f) return;

            var profile = new List<Vector2>();
            AddCap(profile, radiusA, 0f, outward: true);

            int rings = Mathf.Max(3, Mathf.CeilToInt(length / 0.35f));
            for (int i = 1; i < rings; i++)
            {
                float t = i / (float)rings;
                profile.Add(new Vector2(Mathf.Lerp(radiusA, radiusB, t) * (1f + 0.05f * Mathf.Sin(t * 9f + key)), t * length));
            }

            AddCap(profile, radiusB, length, outward: false);

            Quaternion turn = Quaternion.FromToRotation(Vector3.up, (b - a) / length);
            Matrix4x4 world = m.Transform * Matrix4x4.TRS(a, turn, Vector3.one);
            Vector3 seed = Seed(key);
            Color bark = Pick(Bark, key);

            Color Paint(Vector3 p, Vector3 n)
            {
                bool atA = p.y < 0.01f, atB = p.y > length - 0.01f;
                if (Mathf.Abs(n.y) > 0.7f && (atA || atB))
                {
                    if (!(atA ? cutA : cutB)) return BrokenWood * (0.9f + 0.2f * Noise(p * 6f + seed));
                    return Rings(new Vector2(p.x, p.z).magnitude / Mathf.Max(0.01f, atA ? radiusA : radiusB));
                }

                float around = Mathf.Atan2(p.z, p.x);
                float groove = Mathf.Sin(around * 11f + Mathf.Sin(p.y * 2.1f + key) * 1.6f);
                Color c = bark * (0.86f + 0.12f * groove);
                float up = world.MultiplyVector(n).normalized.y;
                float moss = Band(up, 0.55f, 0.9f) * Band(Noise(p * 1.5f + seed), 0.52f, 0.64f);
                return Color.Lerp(c, Moss[0], moss * 0.8f);
            }

            Placed(m, Matrix4x4.TRS(a, turn, Vector3.one), () => m.Lathe(Paint, Vector3.zero, profile, 14));

            // A break is jagged: splinters standing out of the end.
            if (!cutA) Splinters(m, a, (a - b) / length, radiusA, key + 3);
            if (!cutB) Splinters(m, b, (b - a) / length, radiusB, key + 5);
        }

        /// <summary>
        /// The flat end of a turned shape, as rings out to the rim, so the end can be painted with
        /// growth rings. <paramref name="outward"/>: at the bottom, running out to the rim.
        /// </summary>
        static void AddCap(List<Vector2> profile, float radius, float y, bool outward)
        {
            float[] rings = { 0f, 0.2f, 0.4f, 0.55f, 0.7f, 0.84f, 0.93f, 1f };
            if (outward)
            {
                foreach (float r in rings) profile.Add(new Vector2(r * radius, y));
                profile.Add(new Vector2(radius, y));
            }
            else
            {
                profile.Add(new Vector2(radius, y));
                for (int i = rings.Length - 1; i >= 0; i--) profile.Add(new Vector2(rings[i] * radius, y));
            }
        }

        /// <summary>The colour of a saw cut <paramref name="d"/> of the way out from the heart.</summary>
        static Color Rings(float d)
        {
            if (d > 0.95f) return BarkDark;
            if (d > 0.88f) return SapWood * 0.92f;
            if (d > 0.78f) return SapWood;
            int band = Mathf.FloorToInt(d / 0.18f);
            return band % 2 == 0 ? Color.Lerp(HeartDark, HeartMid, d) : HeartLight;
        }

        static void Splinters(B.Model m, Vector3 end, Vector3 outward, float radius, int key)
        {
            Vector3 side = Vector3.Cross(outward, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side.Normalize();
            Vector3 up = Vector3.Cross(side, outward).normalized;

            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f + Hash01(key + i);
                Vector3 root = end + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * (radius * 0.55f) - outward * 0.02f;
                Vector3 tip = root + outward * (0.12f + 0.22f * Hash01(key * 3 + i)) + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * 0.04f;
                m.Tube((p, n) => BrokenWood, root, tip, radius * 0.2f, 0.01f, 5);
            }
        }

        /// <summary>A sawn-off stump on a flaring foot, with roots running out into the ground.</summary>
        static void Stump(B.Model m, Vector3 foot, float radius, float height, int key, int roots)
        {
            var profile = new List<Vector2>
            {
                new Vector2(0f, -0.06f), new Vector2(radius * 1.45f, -0.06f), new Vector2(radius * 1.45f, -0.06f),
                new Vector2(radius * 1.28f, 0.04f), new Vector2(radius * 1.1f, 0.13f), new Vector2(radius * 1.02f, 0.26f),
                new Vector2(radius * 1f, height * 0.62f),
            };
            AddCap(profile, radius * 0.98f, height, outward: false);

            Vector3 seed = Seed(key);
            Color bark = Pick(Bark, key);

            Color Paint(Vector3 p, Vector3 n)
            {
                if (n.y > 0.7f && p.y > foot.y + height - 0.01f)
                    return Rings(new Vector2(p.x - foot.x, p.z - foot.z).magnitude / (radius * 0.98f));

                float around = Mathf.Atan2(p.z - foot.z, p.x - foot.x);
                Color c = bark * (0.86f + 0.12f * Mathf.Sin(around * 13f + Mathf.Sin((p.y - foot.y) * 4f) * 1.4f));
                float moss = Band(Noise(p * 2.2f + seed), 0.55f, 0.68f) * Band(p.y - foot.y, 0.02f, 0.18f);
                return Color.Lerp(c, Pick(Moss, key), moss * 0.8f);
            }

            m.Lathe(Paint, foot, profile, 16);

            // Roots: thick where they leave the trunk, arching over and diving back into the ground
            // close by - a root that runs out straight reads as a spike.
            for (int i = 0; i < roots; i++)
            {
                float a = (i + 0.35f * Hash01(key + i)) / roots * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float reach = radius * (0.75f + 0.45f * Hash01(key * 3 + i));
                Vector3 from = foot + dir * (radius * 0.7f) + Vector3.up * 0.2f;
                Vector3 bend = foot + dir * (radius * 1.15f + reach * 0.35f) + Vector3.up * 0.13f;
                Vector3 to = foot + dir * (radius * 1.1f + reach) - Vector3.up * 0.1f;
                m.Tube((p, n) => bark * 0.92f, from, bend, radius * 0.36f, radius * 0.26f, 9);
                m.Tube((p, n) => bark * 0.86f, bend, to, radius * 0.26f, radius * 0.1f, 9);
            }
        }

        static void Mushroom(B.Model m, Vector3 foot, float size, int key)
        {
            m.Tube(MushroomGill, foot - Vector3.up * 0.01f, foot + Vector3.up * size * 0.55f, size * 0.12f, 6);

            var cap = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(size * 0.42f, 0f), new Vector2(size * 0.42f, 0f),
                new Vector2(size * 0.38f, size * 0.13f), new Vector2(size * 0.22f, size * 0.25f), new Vector2(0f, size * 0.29f),
            };

            m.Lathe((p, n) => n.y < -0.3f ? MushroomGill : MushroomCap * (0.92f + 0.12f * Hash01(key)), foot + Vector3.up * size * 0.5f, cap, 10);
        }

        static void FallenTree(B.Model m, int variant, int key, ref DeterministicRandom rng)
        {
            switch (variant)
            {
                case 0:
                {
                    // Uprooted: the root plate stood on edge, earth still in it, the trunk lying away
                    // from it, broken at the top, a few branches still carrying leaves.
                    Vector3 basePoint = new Vector3(-1.85f, 0.42f, 0f);
                    Vector3 tip = new Vector3(2.35f, 0.24f, 0.25f);
                    Log(m, basePoint, tip, 0.34f, 0.2f, key, cutA: true, cutB: false);

                    // The plate: a disc of earth on edge, thin across the trunk's line.
                    Placed(m, Matrix4x4.TRS(new Vector3(-2.1f, 0f, 0f), Quaternion.Euler(0f, 0f, 8f), new Vector3(0.32f, 1f, 1f)),
                           () => Bush(m, Vector3.zero, 0.9f, key + 9, Earth, 1.05f));

                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.PI * 2f + 0.3f;
                        var dir = new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a));
                        Vector3 from = new Vector3(-2.1f, 0.6f, 0f) + dir * 0.62f;
                        Vector3 to = from + dir * rng.Range(0.15f, 0.3f) + Vector3.left * rng.Range(0.15f, 0.3f);
                        if (to.y < 0.02f) to.y = 0.02f;
                        m.Tube((p, n) => BarkDark, from, to, 0.06f, 0.025f, 6);
                    }

                    for (int i = 0; i < 3; i++)
                    {
                        float t = 0.45f + i * 0.18f;
                        Vector3 at = Vector3.Lerp(basePoint, tip, t);
                        float side = i % 2 == 0 ? 1f : -1f;
                        Vector3 end = at + new Vector3(0.45f, 0.55f + 0.2f * i, side * 0.75f);
                        Log(m, at, end, 0.09f, 0.05f, key + 20 + i, cutA: true, cutB: false);
                        Bush(m, end + Vector3.down * 0.25f, 0.42f, key + 30 + i, Pick(Leaves, key + i), 0.8f);
                    }

                    Tufts(m, 1.2f, 4, key + 50, ref rng);
                    break;
                }

                case 1:
                {
                    // A woodpile left where the trees were felled: three logs, sawn at both ends.
                    Log(m, new Vector3(-1.3f, 0.26f, -0.29f), new Vector3(1.3f, 0.26f, -0.29f), 0.27f, 0.25f, key);
                    Log(m, new Vector3(-1.25f, 0.25f, 0.29f), new Vector3(1.35f, 0.25f, 0.29f), 0.26f, 0.24f, key + 1);
                    Log(m, new Vector3(-1.15f, 0.7f, 0.02f), new Vector3(1.2f, 0.7f, -0.02f), 0.25f, 0.23f, key + 2);

                    // Chocks to stop it rolling.
                    m.Box(Pick(Bark, key) * 0.9f, new Vector3(-0.9f, 0.08f, -0.68f), new Vector3(0.18f, 0.16f, 0.22f), 0.04f);
                    m.Box(Pick(Bark, key) * 0.9f, new Vector3(0.95f, 0.08f, 0.68f), new Vector3(0.18f, 0.16f, 0.22f), 0.04f);

                    Mushroom(m, new Vector3(1.55f, 0f, 0.2f), 0.2f, key + 4);
                    Tufts(m, 1.5f, 5, key + 50, ref rng);
                    break;
                }

                default:
                {
                    // A long trunk felled and left, the stump it came off still standing.
                    Stump(m, new Vector3(-2.3f, 0f, 0.35f), 0.36f, 0.5f, key + 7, 5);
                    Log(m, new Vector3(-1.55f, 0.3f, -0.2f), new Vector3(2.45f, 0.23f, -0.45f), 0.31f, 0.21f, key);
                    Log(m, new Vector3(0.6f, 0.4f, -0.3f), new Vector3(1.1f, 0.85f, -1.05f), 0.08f, 0.05f, key + 3, cutB: false);

                    Mushroom(m, new Vector3(-2.75f, 0f, 0.65f), 0.22f, key + 4);
                    Mushroom(m, new Vector3(-2.55f, 0f, 0.82f), 0.15f, key + 5);
                    Tufts(m, 1.6f, 5, key + 50, ref rng);
                    break;
                }
            }
        }

        static void Stumps(B.Model m, int variant, int key, ref DeterministicRandom rng)
        {
            switch (variant)
            {
                case 0:
                    Stump(m, Vector3.zero, 0.46f, 0.6f, key, 6);
                    Mushroom(m, new Vector3(0.6f, 0f, -0.35f), 0.24f, key + 1);
                    Mushroom(m, new Vector3(0.75f, 0f, -0.15f), 0.16f, key + 2);
                    break;

                case 1:
                    Stump(m, new Vector3(-0.45f, 0f, 0.15f), 0.4f, 0.52f, key, 5);
                    Stump(m, new Vector3(0.65f, 0f, -0.35f), 0.3f, 0.38f, key + 1, 4);
                    Mushroom(m, new Vector3(0.1f, 0f, 0.65f), 0.2f, key + 3);
                    break;

                default:
                {
                    Stump(m, Vector3.zero, 0.44f, 0.55f, key, 6);

                    // An axe left in the top, and a split piece by it.
                    Vector3 bite = new Vector3(0.08f, 0.58f, 0f);
                    Vector3 handleEnd = bite + new Vector3(0.38f, 0.55f, 0.12f);
                    m.Tube((p, n) => B.HoneyLight * 0.9f, bite, handleEnd, 0.03f, 0.028f, 8);
                    m.Box(B.Iron, bite + new Vector3(-0.05f, -0.02f, 0f), new Vector3(0.2f, 0.12f, 0.035f), 0.012f,
                          Quaternion.LookRotation(handleEnd - bite) * Quaternion.Euler(90f, 0f, 0f));

                    Log(m, new Vector3(0.65f, 0.12f, 0.5f), new Vector3(1.05f, 0.12f, 0.85f), 0.13f, 0.13f, key + 4);
                    break;
                }
            }

            Tufts(m, 0.95f, 4, key + 50, ref rng);
        }
    }
}
