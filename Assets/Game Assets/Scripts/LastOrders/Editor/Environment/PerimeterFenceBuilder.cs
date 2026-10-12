using System.Collections.Generic;
using LastOrders.Core;
using UnityEngine;
using UnityEngine.Rendering;
using B = LastOrders.EditorTools.BuildingKit;
using E = LastOrders.EditorTools.EnvKit;
using K = LastOrders.EditorTools.PrimitiveKit;
using L = LastOrders.EditorTools.EnvironmentLayout;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The old perimeter fence round the edge of the clear ground: somebody's attempt, long before
    /// the diner, to fence the whole stretch off. It never held. Built from whatever was to hand -
    /// tin sheets nailed to timber, precast road barriers, sandbags, a few strands of wire - in
    /// stretches, as different people put up different bits of it.
    ///
    /// It is broken open where each of the night's lanes comes through
    /// (<see cref="EnvironmentLayout.Breaches"/>): sheets trampled flat, a barrier shoved over, and a
    /// hazard post either side of the gap with an amber lamp on it, lit at night, so a player
    /// panning out sees where the zombies come in. The road comes through between a pair of posts
    /// in the diner's colours.
    ///
    /// Every piece is one of a few shared meshes (<see cref="Folder"/>), laid in four-metre modules
    /// stretched a little to fit each run exactly; the scene holds only references. Smooth low-poly,
    /// vertex-painted in the compound wall's palette.
    /// </summary>
    public static class PerimeterFenceBuilder
    {
        public const string Folder = ObstacleModels.Folder + "/PerimeterFence";

        /// <summary>Every run is built in modules this long, stretched or squeezed a little to fit it.</summary>
        const float ModuleLength = 4f;

        /// <summary>Kept clear either side of a corner post.</summary>
        const float CornerClear = 0.35f;

        public sealed class Result
        {
            public int Modules;
            public int Breaches;
        }

        enum Kind { Tin, Jersey, Sandbags, Wire }

        const int TinVariants = 4, JerseyVariants = 3, SandbagVariants = 2, WireVariants = 3, BreachVariants = 2;

        static readonly Color[] Timber = { B.Hex("7C6B58"), B.Hex("85735F"), B.Hex("6F604F"), B.Hex("8C7A65") };
        static readonly Color[] TinGalvanised = { B.Hex("A2A7AA"), B.Hex("9AA0A4"), B.Hex("A9AEB1"), B.Hex("959B9F") };
        static readonly Color[] TinRust = { B.Hex("85614C"), B.Hex("7D5E4A"), B.Hex("8C6A52") };
        static readonly Color TinTeal = B.Hex("5E7D78");
        static readonly Color[] Concrete = { B.Hex("9C9A94"), B.Hex("938F88"), B.Hex("A3A099") };
        static readonly Color ConcreteDark = B.Hex("7D7B76");
        static readonly Color[] Burlap = { B.Hex("A9946F"), B.Hex("9F8A66"), B.Hex("B19C78"), B.Hex("958260") };
        static readonly Color WireGrey = B.Hex("55585B");
        static readonly Color Steel = B.Hex("6E7479");
        static readonly Color Hazard = B.Hex("A88C45");
        static readonly Color HazardDark = B.Hex("34312C");
        static readonly Color DinerRed = B.Hex("8E3B33");
        static readonly Color Cream = B.Hex("AFA898");
        static readonly Color Lamp = new Color(1f, 0.72f, 0.36f);

        /// <summary>How each side is mostly built: wire and timber toward the trees, tin and road barriers toward the suburbs.</summary>
        static readonly (Kind kind, float weight)[] TreesMix = { (Kind.Wire, 3f), (Kind.Tin, 3f), (Kind.Jersey, 1.5f), (Kind.Sandbags, 1f) };

        static readonly (Kind kind, float weight)[] SuburbMix = { (Kind.Tin, 4f), (Kind.Jersey, 3f), (Kind.Sandbags, 2f), (Kind.Wire, 1f) };

        static readonly (Kind kind, float weight)[] RoadMix = { (Kind.Jersey, 4f), (Kind.Tin, 4f), (Kind.Sandbags, 2f) };

        public static Result Build(Transform root, ref DeterministicRandom rng)
        {
            var result = new Result();
            Meshes meshes = BuildMeshes();

            Rect f = L.Fence;
            var sides = new[]
            {
                ("West", new Vector2(f.xMin, f.yMin), new Vector2(f.xMin, f.yMax), Vector2.left, SuburbMix),
                ("North", new Vector2(f.xMin, f.yMax), new Vector2(f.xMax, f.yMax), Vector2.up, TreesMix),
                ("East", new Vector2(f.xMax, f.yMax), new Vector2(f.xMax, f.yMin), Vector2.right, SuburbMix),
                ("South", new Vector2(f.xMax, f.yMin), new Vector2(f.xMin, f.yMin), Vector2.down, RoadMix),
            };

            L.Breach[] breaches = L.Breaches;
            Material pool = E.SoftSpot("BreachLamp", new Color(Lamp.r, Lamp.g, Lamp.b, 0.5f), additive: true, nightOnly: true, fogTint: false);

            foreach (var (name, from, to, outward, mix) in sides)
            {
                Transform group = K.Group(name, root);
                Vector2 dir = (to - from).normalized;
                float length = Vector2.Distance(from, to);

                // The openings along this side, in order along it.
                var gaps = new List<(float a, float b, L.Breach breach)>();
                foreach (L.Breach breach in breaches)
                {
                    Vector2 rel = breach.At - from;
                    if (Mathf.Abs(rel.x * dir.y - rel.y * dir.x) > 0.5f) continue;

                    float t = Vector2.Dot(rel, dir);
                    if (t <= 0f || t >= length) continue;
                    gaps.Add((t - breach.HalfWidth, t + breach.HalfWidth, breach));
                }

                gaps.Sort((x, y) => x.a.CompareTo(y.a));

                Place(group, "Corner", meshes.Corner, from, dir, outward, 0f, 1f, 0f);

                float cursor = CornerClear;
                var stretch = new Stretch();
                foreach (var gap in gaps)
                {
                    result.Modules += Run(group, meshes, from, dir, outward, cursor, gap.a, mix, ref stretch, ref rng);
                    Opening(group, meshes, pool, from, dir, outward, gap.breach, (gap.a + gap.b) * 0.5f, ref rng);
                    if (gap.breach.Lane) result.Breaches++;
                    cursor = gap.b;
                }

                result.Modules += Run(group, meshes, from, dir, outward, cursor, length - CornerClear, mix, ref stretch, ref rng);
            }

            return result;
        }

        // --- Laying it out ------------------------------------------------------------------------

        /// <summary>The kind being built and how many more modules of it: a stretch put up by one pair of hands.</summary>
        struct Stretch
        {
            public Kind Kind;
            public int Left;
        }

        /// <summary>Modules from <paramref name="a"/> to <paramref name="b"/> metres along a side, the same length each, filling it exactly.</summary>
        static int Run(Transform group, Meshes meshes, Vector2 from, Vector2 dir, Vector2 outward, float a, float b,
                       (Kind kind, float weight)[] mix, ref Stretch stretch, ref DeterministicRandom rng)
        {
            float length = b - a;
            if (length < 1.2f) return 0;

            int count = Mathf.Max(1, Mathf.RoundToInt(length / ModuleLength));
            float each = length / count;

            for (int i = 0; i < count; i++)
            {
                if (stretch.Left <= 0)
                {
                    stretch.Kind = Pick(mix, ref rng);
                    stretch.Left = rng.Range(2, 6);
                }

                stretch.Left--;
                Mesh mesh = meshes.Of(stretch.Kind, ref rng);

                // Never quite straight: each module set a hand's width in or out, and turned a touch.
                float wobble = rng.Range(-0.12f, 0.12f);
                float yaw = rng.Range(-1.2f, 1.2f);
                Place(group, $"{stretch.Kind}_{group.childCount:000}", mesh, from + dir * (a + each * i), dir, outward, wobble,
                      each / ModuleLength, yaw);
            }

            return count;
        }

        /// <summary>A break in the fence: posts either side and, where the night comes through, the fence it flattened.</summary>
        static void Opening(Transform group, Meshes meshes, Material pool, Vector2 from, Vector2 dir, Vector2 outward, L.Breach breach,
                            float centre, ref DeterministicRandom rng)
        {
            Transform root = K.Group($"Breach_{breach.Name}", group);
            Vector2 at = from + dir * centre;

            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 post = at + dir * side * (breach.HalfWidth - 0.25f);
                Place(root, side < 0f ? "PostA" : "PostB", breach.Lane ? meshes.BreachPost : meshes.RoadPost, post, dir, outward, 0f, 1f, 0f);

                // Its lamp's light on the ground, inside the line: only at night.
                Vector2 glow = post - outward * 1.2f;
                E.GroundSpot("LampPool", root, new Vector3(glow.x, 0.06f, glow.y), breach.Lane ? 6.5f : 5f, pool);
            }

            if (!breach.Lane) return;

            // What it was: sheets and a barrier knocked down over the opening, off the trail down its middle.
            Mesh debris = meshes.BreachDebris[rng.Range(0, meshes.BreachDebris.Length)];
            bool flip = rng.Chance(0.5f);
            Transform t = Place(root, "Flattened", debris, at, flip ? -dir : dir, flip ? -outward : outward, 0f, 1f, 0f);
            t.localScale = new Vector3(breach.HalfWidth / L.BreachHalfWidth, 1f, 1f);
        }

        /// <summary>
        /// One piece at <paramref name="at"/> on the line, its local +X along <paramref name="dir"/> and
        /// +Z out of the clear ground, stretched along X by <paramref name="stretch"/>.
        /// </summary>
        static Transform Place(Transform parent, string name, Mesh mesh, Vector2 at, Vector2 dir, Vector2 outward, float wobble, float stretch,
                               float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            Vector2 p = at + outward * wobble;
            go.transform.localPosition = new Vector3(p.x, 0f, p.y);
            go.transform.localRotation = Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.y)) * Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = new Vector3(stretch, 1f, 1f);

            B.Show(go, mesh);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            return go.transform;
        }

        static Kind Pick((Kind kind, float weight)[] mix, ref DeterministicRandom rng)
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

        // --- The meshes -----------------------------------------------------------------------------

        sealed class Meshes
        {
            public Mesh[] Tin, Jersey, Sandbags, Wire, BreachDebris;
            public Mesh Corner, BreachPost, RoadPost;

            public Mesh Of(Kind kind, ref DeterministicRandom rng)
            {
                Mesh[] set = kind == Kind.Tin ? Tin : kind == Kind.Jersey ? Jersey : kind == Kind.Sandbags ? Sandbags : Wire;
                return set[rng.Range(0, set.Length)];
            }
        }

        /// <summary>Every piece, rewritten in place so the scene's references hold.</summary>
        static Meshes BuildMeshes()
        {
            ContentGenerator.EnsureFolder(Folder);

            Mesh[] Set(string kind, int count, System.Func<int, B.Model> make)
            {
                var set = new Mesh[count];
                for (int v = 0; v < count; v++) set[v] = make(v).Save($"{Folder}/Fence_{kind}_{(char)('A' + v)}.asset", $"Fence_{kind}_{(char)('A' + v)}");
                return set;
            }

            return new Meshes
            {
                Tin = Set("Tin", TinVariants, TinRun),
                Jersey = Set("Jersey", JerseyVariants, JerseyRun),
                Sandbags = Set("Sandbags", SandbagVariants, SandbagRun),
                Wire = Set("Wire", WireVariants, WireRun),
                BreachDebris = Set("BreachDebris", BreachVariants, BreachDebris),
                Corner = CornerPost().Save($"{Folder}/Fence_Corner.asset", "Fence_Corner"),
                BreachPost = BreachPost().Save($"{Folder}/Fence_BreachPost.asset", "Fence_BreachPost"),
                RoadPost = RoadPost().Save($"{Folder}/Fence_RoadPost.asset", "Fence_RoadPost"),
            };
        }

        /// <summary>Darker at the foot, where the ground's damp and the grass grows up it.</summary>
        static float FootShade(Vector3 p, Vector3 n)
        {
            float t = Mathf.Clamp01(p.y / 0.35f);
            return Mathf.Lerp(0.74f, 1f, t * t * (3f - 2f * t));
        }

        static Color Pick(Color[] palette, int key) => palette[B.Hash(key) % palette.Length];

        static Color Shaded(Color c, int key, float amount = 0.05f) => c * (1f + amount * B.Jitter(key));

        /// <summary>A timber post standing on the ground at <paramref name="foot"/>, leaning a little.</summary>
        static void Post(B.Model m, Vector3 foot, float height, float thick, int key)
        {
            Quaternion lean = Quaternion.Euler(3f * B.Jitter(key * 7 + 1), 0f, 2.5f * B.Jitter(key * 11 + 3));
            m.Box(Shaded(Pick(Timber, key), key), foot + lean * new Vector3(0f, height * 0.5f, 0f), new Vector3(thick, height, thick), 0.025f, lean);
        }

        // --- Tin sheets on timber ------------------------------------------------------------------

        /// <summary>
        /// Four sheets of corrugated tin nailed to two rails on three posts, the tin on the inside face
        /// where the camera sees it: galvanised going to rust at the foot, now and then a rusted-through
        /// sheet or one painted teal from some shed. Variant D has a sheet torn off and one sagging in.
        /// </summary>
        static B.Model TinRun(int v)
        {
            var m = new B.Model { Shade = FootShade };
            int key = 100 + v * 37;

            for (int i = 0; i <= 2; i++) Post(m, new Vector3(i * 2f, 0f, 0.07f), 1.95f + 0.12f * B.Jitter(key + i), 0.13f, key + i);

            foreach (float y in new[] { 0.42f, 1.5f })
                m.Box(Shaded(Pick(Timber, key + (int)(y * 10f)), key), new Vector3(2f, y, -0.04f), new Vector3(4.06f, 0.09f, 0.06f), 0.02f);

            for (int s = 0; s < 4; s++)
            {
                int k = key * 13 + s;
                if (v == 3 && s == 2) continue;

                float width = 0.95f;
                float x0 = 0.03f + s * 0.985f;
                float height = 1.72f + 0.14f * B.Jitter(k);
                float bottom = 0.05f + 0.06f * (B.Jitter(k + 5) * 0.5f + 0.5f);

                // Leaning in at the top, more on the sagging one.
                float lean = v == 3 && s == 1 ? 16f : 3f * B.Jitter(k + 9);
                float twist = 2f * B.Jitter(k + 13);

                int roll = B.Hash(k + 21) % 10;
                Color baseColour = roll < 2 ? Pick(TinRust, k) : roll < 3 ? TinTeal : Pick(TinGalvanised, k);
                Color rust = Pick(TinRust, k + 1);
                bool rusted = roll < 2;

                Sheet(m, new Vector3(x0 + width * 0.5f, bottom, -0.085f), width, height, lean, twist, (u, d) =>
                {
                    float down = d / height;
                    float streak = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u * 11f + k)), 10f) * 0.5f * down;
                    float foot = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, down)) * (rusted ? 0.25f : 0.6f);
                    return Color.Lerp(baseColour, rust, Mathf.Clamp01(foot + streak));
                });
            }

            // The torn-off sheet, lying at the foot inside.
            if (v == 3)
            {
                Quaternion flat = Quaternion.Euler(-84f, 18f, 0f);
                m.Box(Pick(TinGalvanised, key) * 0.92f, new Vector3(2.55f, 0.07f, -0.75f), new Vector3(0.95f, 1.6f, 0.03f), 0.01f, flat);
            }

            return m;
        }

        /// <summary>
        /// One sheet, its foot centred at <paramref name="foot"/>, facing -Z (into the clear ground),
        /// leaning <paramref name="lean"/> degrees in at the top about its foot.
        /// </summary>
        static void Sheet(B.Model m, Vector3 foot, float width, float height, float lean, float twist, System.Func<float, float, Color> paint)
        {
            Quaternion q = Quaternion.Euler(-lean, twist, 0f);
            Vector3 across = q * Vector3.left;
            Vector3 down = q * Vector3.down;
            Vector3 corner = foot + q * new Vector3(width * 0.5f, height, 0f);

            m.Corrugated(corner, across, down, width, height, 0.16f, 0.035f, 0.012f, paint, rowStep: 0.6f);
        }

        // --- Road barriers -----------------------------------------------------------------------------

        /// <summary>Two precast road barriers end to end; in C one has been shoved askew.</summary>
        static B.Model JerseyRun(int v)
        {
            var m = new B.Model { Shade = FootShade };
            int key = 300 + v * 41;

            Barrier(m, new Vector3(1.0f, 0f, 0.02f * B.Jitter(key)), 1.5f * B.Jitter(key + 1), key);

            if (v == 2) Barrier(m, new Vector3(3.05f, 0f, 0.45f), 17f, key + 7);
            else Barrier(m, new Vector3(3.02f, 0f, 0.03f * B.Jitter(key + 2)), 1.5f * B.Jitter(key + 3), key + 7);

            // A pallet of sandbags heaped against the join in B.
            if (v == 1)
                for (int i = 0; i < 4; i++)
                    m.Box(Pick(Burlap, key + i), new Vector3(1.75f + i * 0.5f, 0.1f + (i % 2) * 0.16f, -0.52f), new Vector3(0.52f, 0.17f, 0.32f), 0.07f,
                          Quaternion.Euler(0f, 8f * B.Jitter(key + i), 4f * B.Jitter(key + i * 3)));

            return m;
        }

        /// <summary>A jersey barrier: wide foot, narrower body, narrow top; a faded hazard band on some.</summary>
        static void Barrier(B.Model m, Vector3 foot, float yaw, int key)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Color body = Shaded(Pick(Concrete, key), key);

            m.Box(body * 0.97f, foot + turn * new Vector3(0f, 0.14f, 0f), new Vector3(1.96f, 0.28f, 0.62f), 0.05f, turn, 2);
            m.Box(body, foot + turn * new Vector3(0f, 0.46f, 0f), new Vector3(1.96f, 0.4f, 0.38f), 0.06f, turn, 2);
            m.Box(body * 1.03f, foot + turn * new Vector3(0f, 0.75f, 0f), new Vector3(1.96f, 0.18f, 0.24f), 0.05f, turn, 2);

            if (B.Hash(key) % 3 == 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    Color band = i % 2 == 0 ? Hazard * 0.92f : HazardDark;
                    m.Box(band, foot + turn * new Vector3(-0.72f + i * 0.48f, 0.6f, -0.2f), new Vector3(0.46f, 0.12f, 0.02f), 0.005f, turn);
                }
            }

            // The lifting slot through its foot.
            m.Box(ConcreteDark, foot + turn * new Vector3(0f, 0.12f, -0.31f), new Vector3(0.5f, 0.1f, 0.02f), 0.01f, turn);
        }

        // --- Sandbags ------------------------------------------------------------------------------------

        /// <summary>Three courses of sandbags laid like brickwork in front of a row of pickets with a plank along their tops.</summary>
        static B.Model SandbagRun(int v)
        {
            var m = new B.Model { Shade = FootShade };
            int key = 500 + v * 53;

            for (int i = 0; i < 4; i++) Post(m, new Vector3(0.25f + i * 1.17f, 0f, 0.28f), 1.25f + 0.1f * B.Jitter(key + i), 0.1f, key + i);
            m.Box(Pick(Timber, key), new Vector3(2f, 1.2f, 0.22f), new Vector3(3.9f, 0.08f, 0.05f), 0.02f, Quaternion.Euler(0f, 0f, 1.2f * B.Jitter(key)));

            int courses = v == 0 ? 3 : 4;
            for (int row = 0; row < courses; row++)
            {
                float offset = row % 2 == 0 ? 0.04f : 0.31f;
                for (int i = 0; i < 7; i++)
                {
                    int k = key * 7 + row * 17 + i;

                    // The top course has gaps where bags split or were taken.
                    if (row == courses - 1 && B.Hash(k) % 4 == 0) continue;

                    float x = offset + i * 0.56f;
                    if (x > 3.75f) continue;

                    var at = new Vector3(x + 0.26f, 0.1f + row * 0.19f, 0.02f - row * 0.03f);
                    m.Box(Shaded(Pick(Burlap, k), k), at, new Vector3(0.54f, 0.18f, 0.34f), 0.075f,
                          Quaternion.Euler(3f * B.Jitter(k + 1), 5f * B.Jitter(k + 2), 3f * B.Jitter(k + 3)));
                }
            }

            return m;
        }

        // --- Wire --------------------------------------------------------------------------------------------

        /// <summary>Timber posts with four strands of wire sagging between them; one snapped and hanging in C.</summary>
        static B.Model WireRun(int v)
        {
            var m = new B.Model { Shade = FootShade };
            int key = 700 + v * 61;

            var tops = new Vector3[3];
            for (int i = 0; i <= 2; i++)
            {
                float height = 1.4f + 0.1f * B.Jitter(key + i);
                Post(m, new Vector3(i * 2f, 0f, 0f), height, 0.12f, key + i);
                tops[i] = new Vector3(i * 2f, height, 0f);
            }

            // A stay braced against the middle post in B.
            if (v == 1) m.Tube(Pick(Timber, key + 9), new Vector3(1f, 0f, -0.6f), new Vector3(2f, 1.05f, -0.06f), 0.045f, 6);

            for (int strand = 0; strand < 4; strand++)
            {
                float y = 0.3f + strand * 0.32f;
                for (int span = 0; span < 2; span++)
                {
                    var a = new Vector3(span * 2f, y, -0.07f);
                    var b = new Vector3(span * 2f + 2f, y, -0.07f);

                    if (v == 2 && strand == 2 && span == 1)
                    {
                        // Snapped: hanging off the post down into the grass.
                        m.Tube(WireGrey, a, a + new Vector3(0.45f, -0.35f, -0.05f), 0.009f, 4);
                        m.Tube(WireGrey, a + new Vector3(0.45f, -0.35f, -0.05f), a + new Vector3(0.75f, -y + 0.04f, -0.12f), 0.009f, 4);
                        continue;
                    }

                    Vector3 sag = (a + b) * 0.5f + Vector3.down * (0.05f + 0.03f * B.Jitter(key + strand * 5 + span));
                    m.Tube(WireGrey, a, sag, 0.008f, 4);
                    m.Tube(WireGrey, sag, b, 0.008f, 4);
                }
            }

            return m;
        }

        // --- Posts ---------------------------------------------------------------------------------------------

        /// <summary>A precast post where two sides meet, on a footing, with a cap.</summary>
        static B.Model CornerPost()
        {
            var m = new B.Model { Shade = FootShade };
            m.Box(ConcreteDark, new Vector3(0f, 0.1f, 0f), new Vector3(0.62f, 0.2f, 0.62f), 0.04f);
            m.Box(Concrete[0], new Vector3(0f, 1.1f, 0f), new Vector3(0.36f, 1.9f, 0.36f), 0.04f, 2);
            m.Box(Concrete[2], new Vector3(0f, 2.08f, 0f), new Vector3(0.46f, 0.1f, 0.46f), 0.03f);
            return m;
        }

        /// <summary>
        /// A steel post either side of a breach, banded in hazard paint, a striped board across it and
        /// a caged amber lamp on top looking into the clear ground - somebody marked the hole rather
        /// than mend it.
        /// </summary>
        static B.Model BreachPost()
        {
            var m = new B.Model { Shade = FootShade };

            m.Box(ConcreteDark, new Vector3(0f, 0.12f, 0f), new Vector3(0.55f, 0.24f, 0.55f), 0.04f);
            for (int i = 0; i < 8; i++)
            {
                Color band = i % 2 == 0 ? Hazard : HazardDark;
                m.Box(band, new Vector3(0f, 0.24f + i * 0.28f + 0.14f, 0f), new Vector3(0.18f, 0.28f, 0.18f), 0.02f);
            }

            // The board, bolted across the post at chest height, facing in.
            for (int i = 0; i < 5; i++)
            {
                Color band = i % 2 == 0 ? Hazard * 0.95f : HazardDark;
                m.Box(band, new Vector3(-0.48f + i * 0.24f, 1.55f, -0.12f), new Vector3(0.24f, 0.34f, 0.03f), 0.006f,
                      Quaternion.Euler(0f, 0f, 4f));
            }

            // The lamp: a housing, its lens glowing toward the clear ground, a wire cage over it.
            m.Box(Steel, new Vector3(0f, 2.62f, 0f), new Vector3(0.26f, 0.2f, 0.26f), 0.04f);
            m.Box(Lamp, new Vector3(0f, 2.62f, -0.13f), new Vector3(0.18f, 0.13f, 0.03f), 0.02f, Quaternion.identity, 1, B.Model.Glow);
            for (int i = 0; i < 3; i++)
                m.Tube(HazardDark, new Vector3(-0.12f + i * 0.12f, 2.5f, -0.17f), new Vector3(-0.12f + i * 0.12f, 2.74f, -0.17f), 0.01f, 4);
            m.Box(Steel, new Vector3(0f, 2.77f, 0f), new Vector3(0.32f, 0.04f, 0.32f), 0.015f);

            return m;
        }

        /// <summary>Where the road comes through: a concrete post in the diner's red and cream with a small lamp on top.</summary>
        static B.Model RoadPost()
        {
            var m = new B.Model { Shade = FootShade };

            m.Box(ConcreteDark, new Vector3(0f, 0.1f, 0f), new Vector3(0.6f, 0.2f, 0.6f), 0.04f);
            m.Box(Concrete[1], new Vector3(0f, 0.7f, 0f), new Vector3(0.4f, 1.0f, 0.4f), 0.05f, 2);
            m.Box(DinerRed, new Vector3(0f, 1.32f, 0f), new Vector3(0.42f, 0.24f, 0.42f), 0.04f);
            m.Box(Cream, new Vector3(0f, 1.56f, 0f), new Vector3(0.42f, 0.24f, 0.42f), 0.04f);
            m.Box(Steel, new Vector3(0f, 1.74f, 0f), new Vector3(0.22f, 0.12f, 0.22f), 0.03f);
            m.Box(Lamp, new Vector3(0f, 1.87f, 0f), new Vector3(0.16f, 0.14f, 0.16f), 0.05f, Quaternion.identity, 1, B.Model.Glow);
            return m;
        }

        // --- The breach ------------------------------------------------------------------------------------------

        /// <summary>
        /// What is left lying in a breach, centred on it: sheets trampled flat both sides, a barrier
        /// shoved over on its side, a post snapped off - all clear of the trail down the middle.
        /// </summary>
        static B.Model BreachDebris(int v)
        {
            var m = new B.Model { Shade = FootShade };
            int key = 900 + v * 71;
            float edge = L.BreachHalfWidth;

            foreach (float side in new[] { -1f, 1f })
            {
                int k = key + (side > 0f ? 11 : 0);

                // Two sheets, flat and overlapping, one bent up at its end.
                for (int i = 0; i < 2; i++)
                {
                    float x = side * (edge - 1.3f - i * 0.9f);
                    float z = 0.6f * B.Jitter(k + i * 3) + (i == 0 ? 0.6f : -0.5f);
                    var foot = new Vector3(x, 0.03f + i * 0.02f, z);
                    Quaternion flat = Quaternion.Euler(-88f + (i == 1 ? 9f : 0f), 70f * B.Jitter(k + i * 5) + side * 20f, 0f);

                    Color c = i == 0 ? Pick(TinGalvanised, k + i) : Pick(TinRust, k + i);
                    Vector3 across = flat * Vector3.left, down = flat * Vector3.down;
                    Vector3 corner = foot + flat * new Vector3(0.475f, 0.85f, 0f);
                    m.Corrugated(corner, across, down, 0.95f, 1.7f, 0.16f, 0.035f, 0.012f, (u, d) => Color.Lerp(c, TinRust[0], d / 1.7f * 0.4f),
                                 rowStep: 0.6f);
                }

                // The snapped stump of a post at the opening's edge.
                Quaternion lean = Quaternion.Euler(14f * B.Jitter(k + 7), 0f, side * -18f);
                m.Box(Pick(Timber, k), new Vector3(side * (edge - 0.55f), 0.32f, 0.1f), new Vector3(0.13f, 0.64f, 0.13f), 0.02f, lean);
            }

            // A barrier rolled over on its side, outside the line, on one flank.
            float bx = (v == 0 ? 1f : -1f) * (edge - 1.6f);
            // Tipped about its long axis onto its side.
            Quaternion over = Quaternion.Euler(0f, 24f * (v == 0 ? 1f : -1f), 0f) * Quaternion.Euler(88f, 0f, 0f);
            Color body = Shaded(Pick(Concrete, key), key);
            Vector3 at = new Vector3(bx, 0.31f, 1.4f);
            m.Box(body * 0.97f, at + over * new Vector3(0f, 0.14f, 0f), new Vector3(1.96f, 0.28f, 0.62f), 0.05f, over, 2);
            m.Box(body, at + over * new Vector3(0f, 0.46f, 0f), new Vector3(1.96f, 0.4f, 0.38f), 0.06f, over, 2);
            m.Box(body * 1.03f, at + over * new Vector3(0f, 0.75f, 0f), new Vector3(1.96f, 0.18f, 0.24f), 0.05f, over, 2);

            return m;
        }
    }
}
