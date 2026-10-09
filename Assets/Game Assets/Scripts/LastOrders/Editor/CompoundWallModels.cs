using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using Panel = LastOrders.EditorTools.TextureGenerator.WallPanel;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The compound's wall, its gates and the bastions that thicken it, modelled in code in the game's
    /// smooth low-poly style and painted in their vertices on LastOrders/Stylized, as
    /// <see cref="BuildingKit"/> builds are.
    ///
    /// The wall is precast concrete: square posts on wider plinths every couple of metres, three heavy
    /// slabs stacked between each pair, a coping along the top carrying a rail of anti-climb spikes,
    /// all on a footing, every edge rounded so it catches the light. It is a wall people have held for
    /// a year: here a slab has gone and the gap is boarded over, there a bay is sheathed in tin on its
    /// outside, a lighter slab has been put in, and inside, timber shores brace it and sandbags are
    /// heaped at its foot. Broken, its top course is down and the spike rail hangs; fallen, it is a heap
    /// of slabs between stumps of post.
    ///
    /// A section is put together from parts - a bay between each pair of posts, and the posts - each a
    /// mesh shared with every part like it along the whole wall: bays are built to the nearest
    /// <see cref="Quantum"/> and stretched the last centimetre or so to fit. Everything is built in the
    /// frame the wall setup lays it in - X along the wall, +Z out of the compound (a gate's +Z is in) -
    /// and saved as mesh assets named for what they are; a rebuild writes into the meshes already there.
    ///
    /// Nothing stands higher than <see cref="CompoundWallSceneSetup.BastionHeight"/> or further than
    /// <see cref="Reach"/> from the wall's line, so a bastion raised over any stretch of it - at a wall
    /// site, or under a shooter bought from the shop - hides it whole.
    /// </summary>
    public static partial class CompoundWallModels
    {
        const string Folder = "Assets/Game Assets/Content/LastOrders/Walls/" + EnvKit.Style;
        const string SectionFolder = Folder + "/Sections";

        // --- Measurements (metres; X along the wall, +Z out of the compound, Y up) ------------------

        const float FootingTop = 0.2f, FootingDepth = 0.64f;
        const float SlabTop = 1.95f, SlabDepth = 0.36f, SlabRound = 0.035f;
        const int SlabRows = 3;
        const float SlabHeight = (SlabTop - FootingTop) / SlabRows;
        const float CopingTop = 2.05f, CopingDepth = 0.46f;
        const float PostSize = 0.5f, PostTop = 2.2f, PostCap = 0.07f, PlinthSize = 0.58f, PlinthTop = 0.28f;

        /// <summary>About how far apart the posts stand.</summary>
        const float Bay = 2.1f;

        /// <summary>
        /// Bays are built to the nearest this many metres long and stretched to fit - a few percent at
        /// most, which nothing on them shows - so most share a mesh.
        /// </summary>
        const float Quantum = 0.2f;

        const float SpikeRailTop = CopingTop + 0.03f, SpikeStep = 0.22f, SpikeLength = 0.13f;

        /// <summary>
        /// The furthest anything at the wall's foot reaches from its line. A bastion over the wall reaches
        /// 0.9 m in from it (1 m for the shop's), so whatever stands within this is inside one.
        /// </summary>
        const float Reach = 0.85f;

        // --- The palette -----------------------------------------------------------------------------

        /// <summary>Weathered concrete, a shade apart slab to slab. Mid grey: a lit face any lighter reads white.</summary>
        static readonly Color[] SlabGrey = { B.Hex("9C9A94"), B.Hex("938F88"), B.Hex("A3A099"), B.Hex("8F8E89"), B.Hex("999790") };

        /// <summary>A slab put in since, not yet weathered to match the rest.</summary>
        static readonly Color SlabNew = B.Hex("A8A69F");

        static readonly Color PostGrey = B.Hex("8B8984");
        static readonly Color CapGrey = B.Hex("929089");
        static readonly Color FootingGrey = B.Hex("7D7B76");

        /// <summary>Broken faces, and the inside of a bastion where it shows.</summary>
        static readonly Color CoreGrey = B.Hex("6C6A66");

        static readonly Color Iron = B.Iron;
        static readonly Color IronDark = B.Hex("2F3236");
        static readonly Color Steel = B.Hex("6E7479");
        static readonly Color SteelLight = B.Hex("8D9398");
        static readonly Color Rust = B.Hex("7D5E4A");
        static readonly Color RebarRust = B.Hex("6A5446");
        static readonly Color[] Burlap = { B.Hex("A9946F"), B.Hex("9F8A66"), B.Hex("B19C78"), B.Hex("958260") };

        // --- Sections --------------------------------------------------------------------------------

        /// <summary>What stands at one end of a section.</summary>
        public enum End
        {
            /// <summary>The next section's post, which it puts up: this one's slabs stop at its face.</summary>
            Open,

            /// <summary>A post of its own: every section's start, and the end of a run.</summary>
            Post,

            /// <summary>A gate's pillar, which the gate brings, broader than a post: it hides the slabs' ends.</summary>
            Gate,
        }

        public enum State { Intact, Damaged, Rubble }

        /// <summary>How one bay has been patched.</summary>
        public enum Patch { None, NewSlab, Boarded, Sheathed }

        /// <summary>One section of wall, its ends in the frame its parts are built in.</summary>
        public struct Section
        {
            public float Length;

            /// <summary>What stands at its -X and +X ends.</summary>
            public End Lo, Hi;

            /// <summary>How it has been patched, if at all, and in which bay.</summary>
            public Patch Patch;
            public int PatchBay;

            /// <summary>0 nothing inside, 1 a timber shore, 2 sandbags at its foot.</summary>
            public int Dressing;

            /// <summary>Which way round its bays' slabs are shaded.</summary>
            public int Shading;

            public int Bays => Mathf.Max(1, Mathf.RoundToInt(Length / Bay));
        }

        /// <summary>
        /// A section <paramref name="length"/> long, with <paramref name="start"/> and <paramref name="end"/>
        /// at its ends along the run. Its make is picked by <paramref name="key"/> - where it stands - so a
        /// rebuild makes it the same again. <paramref name="turned"/> says its parts go on turned half
        /// round - its outside is its local -Z - which swaps its ends.
        /// </summary>
        public static Section SectionFor(string key, float length, End start, End end, bool turned)
        {
            // FNV-1a, as the wall's panels are picked: string.GetHashCode is not the same run to run. Its
            // high bits hardly change between keys that differ in their last letter, so it is stirred
            // before anything else is picked from it.
            uint hash = 2166136261;
            foreach (char c in key) hash = (hash ^ c) * 16777619;
            uint stirred = (uint)B.Hash((int)hash);

            Panel panel = CompoundWallSceneSetup.PanelFor(key);
            var section = new Section
            {
                Length = length,
                Lo = turned ? end : start,
                Hi = turned ? start : end,
                Patch = panel == Panel.Planks ? Patch.Boarded
                      : panel == Panel.Tin ? Patch.Sheathed
                      : (stirred >> 4) % 3 == 0 ? Patch.NewSlab
                      : Patch.None,
            };

            section.PatchBay = (int)(stirred % (uint)section.Bays);
            uint dressing = (stirred >> 9) % 7;
            section.Dressing = dressing == 0 ? 1 : dressing == 1 ? 2 : 0;
            section.Shading = (int)((stirred >> 14) % 2);
            return section;
        }

        /// <summary>
        /// A section's look in one state, under <paramref name="group"/> (turned already if it needs to be):
        /// a part for each bay and for each post the section puts up.
        /// </summary>
        public static void Build(Transform group, Section s, State state, Material[] materials)
        {
            float[] cuts = Cuts(s);

            for (int i = 0; i < cuts.Length; i++)
            {
                if (!OwnsPost(s, cuts, i)) continue;

                // Broken, the posts between bays snap; the ends hold until the section falls.
                bool between = i > 0 && i < cuts.Length - 1;
                int variant = (i + s.PatchBay + s.Shading) % 2;
                Mesh mesh = state == State.Rubble ? StumpMesh(variant)
                          : state == State.Damaged && between ? BrokenPostMesh(variant)
                          : PostMesh();

                Part(group, $"Post_{i}", mesh, materials).transform.localPosition = new Vector3(cuts[i], 0f, 0f);
            }

            for (int bay = 0; bay < s.Bays; bay++)
            {
                float length = cuts[bay + 1] - cuts[bay];
                var key = new BayKey
                {
                    Length = Mathf.Max(Quantum * 4f, Mathf.Round(length / Quantum) * Quantum),
                    Patch = bay == s.PatchBay ? s.Patch : Patch.None,
                    Shading = state == State.Intact ? (s.Shading + bay) % 2 : 0,
                };

                // A new slab is only new while it stands.
                if (state != State.Intact && key.Patch == Patch.NewSlab) key.Patch = Patch.None;

                float middle = (cuts[bay] + cuts[bay + 1]) * 0.5f;
                GameObject part = Part(group, $"Bay_{bay}", BayMesh(key, state), materials);
                part.transform.localPosition = new Vector3(middle, 0f, 0f);
                part.transform.localScale = new Vector3(length / key.Length, 1f, 1f);
            }

            // Inside, where the wall has needed holding, while it stands: the bay after the patched one when there is another.
            if (state != State.Intact || s.Dressing == 0) return;

            int dressed = (s.PatchBay + 1) % s.Bays;
            float a = cuts[dressed] + PostSize * 0.5f, b = cuts[dressed + 1] - PostSize * 0.5f;

            if (s.Dressing == 1)
                Part(group, "Shore", ShoreMesh(), materials).transform.localPosition = new Vector3(Mathf.Lerp(a, b, 0.62f), 0f, 0f);
            else
                Part(group, "Sandbags", SandbagsMesh(b - a > 1.6f ? 3 : 2), materials).transform.localPosition = new Vector3((a + b) * 0.5f, 0f, 0f);
        }

        /// <summary>Where the posts stand: the section's ends and a post every <see cref="Bay"/> or so between.</summary>
        static float[] Cuts(Section s)
        {
            int bays = s.Bays;
            var cuts = new float[bays + 1];
            for (int i = 0; i <= bays; i++) cuts[i] = -s.Length * 0.5f + s.Length * i / bays;
            return cuts;
        }

        /// <summary>The section puts up the posts between its bays, and at an end only one of its own.</summary>
        static bool OwnsPost(Section s, float[] cuts, int i) =>
            (i > 0 && i < cuts.Length - 1) || (i == 0 && s.Lo == End.Post) || (i == cuts.Length - 1 && s.Hi == End.Post);

        // --- Meshes and parts ------------------------------------------------------------------------

        static readonly Dictionary<string, Mesh> made = new Dictionary<string, Mesh>();

        /// <summary>For tests: meshes are built in memory instead of saved as assets, and thrown away by <see cref="Begin"/>.</summary>
        internal static bool InMemory;

        /// <summary>Starts a build: each mesh asked for from here on is built fresh, once, however many share it.</summary>
        public static void Begin()
        {
            foreach (Mesh mesh in made.Values)
                if (mesh != null && !AssetDatabase.Contains(mesh)) UnityEngine.Object.DestroyImmediate(mesh);

            made.Clear();
        }

        static Mesh Get(string folder, string name, Func<B.Model> build)
        {
            if (made.TryGetValue(name, out Mesh mesh) && mesh != null) return mesh;

            mesh = InMemory ? build().Build(name) : build().Save($"{folder}/{name}.asset", name);
            made[name] = mesh;
            return mesh;
        }

        /// <summary>A child of <paramref name="parent"/> drawing <paramref name="mesh"/>, turned half round when <paramref name="turned"/>.</summary>
        public static GameObject Part(Transform parent, string name, Mesh mesh, Material[] materials, bool turned = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (turned) go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            B.Show(go, mesh, materials);
            return go;
        }

        /// <summary>Darker toward the ground, where rain splashes dirt up it, and on anything's underside.</summary>
        static float Shade(Vector3 p, Vector3 n) => Mathf.Lerp(0.76f, 1f, Smooth(p.y / 0.9f)) * (n.y < -0.6f ? 0.8f : 1f);

        // --- Posts -----------------------------------------------------------------------------------------

        /// <summary>
        /// A square post on a wider plinth with a cap. One mesh for every post on the wall: two runs
        /// meeting at a corner each put one on the same spot, and they match exactly.
        /// </summary>
        static Mesh PostMesh() => Get(SectionFolder, "Wall_Post", () =>
        {
            var m = new B.Model { Shade = Shade };
            m.Box(FootingGrey, new Vector3(0f, PlinthTop * 0.5f, 0f), new Vector3(PlinthSize, PlinthTop, PlinthSize), 0.035f);
            m.Box(PostGrey, new Vector3(0f, PostTop * 0.5f, 0f), new Vector3(PostSize, PostTop, PostSize), 0.045f);
            m.Box(CapGrey, new Vector3(0f, PostTop + PostCap * 0.5f, 0f), new Vector3(PostSize + 0.1f, PostCap, PostSize + 0.1f), 0.025f);
            return m;
        });

        static Mesh BrokenPostMesh(int variant) => Get(SectionFolder, $"Wall_PostBroken_{variant}", () =>
        {
            var m = new B.Model { Shade = Shade };
            Stump(m, 0f, variant == 0 ? 1.35f : 1.62f, 40 + variant);
            return m;
        });

        static Mesh StumpMesh(int variant) => Get(SectionFolder, $"Wall_PostStump_{variant}", () =>
        {
            var m = new B.Model { Shade = Shade };
            Stump(m, 0f, variant == 0 ? 0.5f : 0.82f, 60 + variant);
            return m;
        });

        /// <summary>A post snapped at <paramref name="height"/>: a lump of it tipped over on the break, its bars bent out.</summary>
        static void Stump(B.Model m, float x, float height, int key)
        {
            m.Box(FootingGrey, new Vector3(x, PlinthTop * 0.5f, 0f), new Vector3(PlinthSize, PlinthTop, PlinthSize), 0.035f);
            m.Box(PostGrey, new Vector3(x, height * 0.5f, 0f), new Vector3(PostSize, height, PostSize), 0.045f);
            m.Box(PostGrey * 0.95f, new Vector3(x + 0.06f * B.Jitter(key), height + 0.03f, 0.04f * B.Jitter(key + 1)),
                  new Vector3(PostSize * 0.8f, 0.16f, PostSize * 0.7f), 0.03f,
                  Quaternion.Euler(14f * B.Jitter(key + 2), 30f * B.Jitter(key + 3), 16f * B.Jitter(key + 4)));
            Rebar(m, new Vector3(x - 0.13f, height - 0.05f, 0.13f), new Vector3(-0.3f, 1f, 0.2f), 0.38f, key);
            Rebar(m, new Vector3(x + 0.13f, height - 0.05f, -0.11f), new Vector3(0.4f, 1f, -0.3f), 0.32f, key + 9);
        }

        // --- Bays ------------------------------------------------------------------------------------------

        /// <summary>
        /// One bay as its mesh is made and named: centred on its own origin, a post at each end. Beside a
        /// gate, the gate's pillar stands where the post would and hides the slabs' ends the same.
        /// </summary>
        struct BayKey
        {
            public float Length;
            public Patch Patch;
            public int Shading;

            public int Seed => Mathf.RoundToInt(Length * 100f) * 31 + (int)Patch * 101 + Shading * 211;

            public string Name(State state) => $"Wall_Bay_{state}_{Mathf.RoundToInt(Length * 100f)}_{Patch}_{Shading}";
        }

        static Mesh BayMesh(BayKey key, State state) =>
            Get(SectionFolder, key.Name(state), () => state == State.Intact ? BayIntact(key)
                                                     : state == State.Damaged ? BayDamaged(key)
                                                     : BayRubble(key));

        /// <summary>Where a bay's slabs run: tucked into the posts either side.</summary>
        static (float a, float b) Span(BayKey k)
        {
            const float Tuck = 0.03f;
            float half = k.Length * 0.5f;
            return (-half + PostSize * 0.5f - Tuck, half - PostSize * 0.5f + Tuck);
        }

        static Mesh ShoreMesh() => Get(SectionFolder, "Wall_Shore", () =>
        {
            var m = new B.Model { Shade = Shade };
            Shore(m, 0f, 5);
            return m;
        });

        static Mesh SandbagsMesh(int count) => Get(SectionFolder, $"Wall_Sandbags_{count}", () =>
        {
            var m = new B.Model { Shade = Shade };
            Sandbags(m, count, 11 + count);
            return m;
        });

        static Color SlabColour(BayKey k, int row) =>
            SlabGrey[B.Hash(k.Shading * 977 + Mathf.RoundToInt(k.Length * 100f) * 7 + row) % SlabGrey.Length] *
            (1f + 0.025f * B.Jitter(k.Seed + row));

        static B.Model BayIntact(BayKey k)
        {
            var m = new B.Model { Shade = Shade };
            float half = k.Length * 0.5f;
            (float a, float b) = Span(k);

            Footing(m, -half, half, FootingTop);

            for (int r = 0; r < SlabRows; r++)
            {
                float y = FootingTop + r * SlabHeight;

                if (k.Patch == Patch.Boarded && r == SlabRows - 1)
                {
                    Boarded(m, a, b, y, k.Seed);
                    continue;
                }

                Slab(m, k.Patch == Patch.NewSlab && r == 1 ? SlabNew : SlabColour(k, r), a, b, y, SlabHeight);
            }

            Coping(m, a, b);
            Spikes(m, a, b, k.Seed);

            if (k.Patch == Patch.Sheathed) Sheathed(m, a, b, k.Seed);
            return m;
        }

        static void Footing(B.Model m, float from, float to, float top) =>
            m.Box(FootingGrey, new Vector3((from + to) * 0.5f, (top - 0.03f) * 0.5f, 0f), new Vector3(to - from, top + 0.03f, FootingDepth), 0.035f);

        static void Slab(B.Model m, Color c, float a, float b, float y, float height) =>
            m.Box(c, new Vector3((a + b) * 0.5f, y + height * 0.5f, 0f), new Vector3(b - a, height - 0.006f, SlabDepth), SlabRound);

        static void Coping(B.Model m, float a, float b) =>
            m.Box(CapGrey, new Vector3((a + b) * 0.5f, (SlabTop + CopingTop) * 0.5f, 0f), new Vector3(b - a, CopingTop - SlabTop, CopingDepth), 0.03f);

        /// <summary>A rail of short spikes along the coping, leaning out and in by turns as anti-climb spikes are set; a few bent.</summary>
        static void Spikes(B.Model m, float a, float b, int key)
        {
            float from = a + 0.08f, to = b - 0.08f;
            if (to - from < 0.2f) return;

            m.Box(Iron, new Vector3((from + to) * 0.5f, SpikeRailTop - 0.015f, 0f), new Vector3(to - from, 0.03f, 0.05f), 0.008f);

            int count = Mathf.Max(2, Mathf.RoundToInt((to - from) / SpikeStep) + 1);
            for (int k = 0; k < count; k++)
            {
                float x = Mathf.Lerp(from + 0.04f, to - 0.04f, k / (float)(count - 1));
                float lean = (k % 2 == 0 ? 16f : -16f) + 8f * B.Jitter(key * 13 + k);
                var foot = new Vector3(x, SpikeRailTop - 0.005f, 0f);
                Vector3 tip = foot + Quaternion.Euler(lean, 0f, 6f * B.Jitter(key * 7 + k)) * Vector3.up * SpikeLength;
                m.Tube(SpikePaint, foot, tip, 0.017f, 0.002f, 3);
            }
        }

        static Color SpikePaint(Vector3 p, Vector3 n) => Color.Lerp(IronDark, Rust, Smooth((p.y - SpikeRailTop - 0.03f) / 0.08f) * 0.7f);

        // --- Patches and dressing ----------------------------------------------------------------------

        /// <summary>
        /// The top slab gone and the gap boarded over both sides, the stub of the slab dark behind the
        /// boards, a batten nailed across outside.
        /// </summary>
        static void Boarded(B.Model m, float a, float b, float y, int key)
        {
            float mid = (a + b) * 0.5f;
            m.Box(B.WalnutDark, new Vector3(mid, y + SlabHeight * 0.5f, 0f), new Vector3(b - a, SlabHeight - 0.01f, 0.2f), 0.02f);

            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = 0; k < 3; k++)
                {
                    int id = key * 7 + k * 3 + side + 1;
                    float by = y + SlabHeight * (k + 0.5f) / 3f;
                    Color c = B.Plank(id) * (1f + 0.04f * B.Jitter(id + 5));
                    m.Box(c, new Vector3(mid + 0.04f * B.Jitter(id + 1), by, side * 0.125f), new Vector3(b - a + 0.1f, SlabHeight / 3f - 0.02f, 0.05f),
                          0.012f, Quaternion.Euler(0f, 0f, 1.8f * B.Jitter(id)));
                }
            }

            var from = new Vector3(a + 0.14f, y + 0.08f, 0.172f);
            var to = new Vector3(b - 0.14f, y + SlabHeight - 0.08f, 0.172f);
            Beam(m, B.Plank(key + 3) * 0.9f, from, to, new Vector2(0.1f, 0.035f), Vector3.forward);

            // Nail heads at each end of the batten.
            foreach (Vector3 end in new[] { from, to })
                m.Box(IronDark, end + new Vector3(0f, 0f, 0.02f), new Vector3(0.025f, 0.025f, 0.012f), 0.005f);
        }

        /// <summary>The outside of a bay sheathed in sheets of tin bolted over the slabs, each overlapping the last, rusting at the foot.</summary>
        static void Sheathed(B.Model m, float a, float b, int key)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt((b - a) / 0.85f));
            float w = (b - a) / count;

            for (int k = 0; k < count; k++)
            {
                int id = key * 11 + k;
                float top = 1.66f + 0.16f * B.Jitter(id), bottom = 0.3f + 0.05f * B.Jitter(id + 1);
                float length = top - bottom, width = w + 0.07f;
                Color[] tins = B.Hash(id) % 3 == 0 ? B.RoofTin : B.Tin;
                Color sheet = tins[B.Hash(id + 2) % tins.Length];

                var centre = new Vector3(a + w * (k + 0.5f), (top + bottom) * 0.5f, SlabDepth * 0.5f + 0.008f + 0.007f * k);
                m.Transform = Matrix4x4.TRS(centre, Quaternion.Euler(0f, 0f, 1.6f * B.Jitter(id + 3)), Vector3.one);

                m.Corrugated(new Vector3(-width * 0.5f, length * 0.5f, 0f), Vector3.right, Vector3.down, width, length, 0.11f, 0.026f, 0.006f,
                             (u, v) => TinPaint(sheet, u, v, length, id), 0.34f);

                for (int c = 0; c < 4; c++)
                {
                    float bx = ((c & 1) == 0 ? -1f : 1f) * (width * 0.5f - 0.05f);
                    float bolt = ((c & 2) == 0 ? -1f : 1f) * (length * 0.5f - 0.07f);
                    m.Box(Iron, new Vector3(bx, bolt, 0.03f), new Vector3(0.04f, 0.04f, 0.025f), 0.008f);
                }

                m.Transform = Matrix4x4.identity;
            }
        }

        /// <summary>Rust up from a sheet's foot, and in runs down from a fixing here and there.</summary>
        static Color TinPaint(Color sheet, float u, float v, float length, int key)
        {
            float rust = Smooth((v - (length - 0.36f)) / 0.3f) * 0.75f;
            if (Mathf.Sin(u * 9.3f + key * 1.7f) > 0.93f) rust = Mathf.Max(rust, 0.55f * Smooth(v / length));
            return Color.Lerp(sheet, Rust, rust);
        }

        /// <summary>A raking shore inside: a timber prop from a sole board on the ground up to a cleat spiked to the wall.</summary>
        static void Shore(B.Model m, float x, int key)
        {
            Color wood = B.Plank(key) * 0.92f;
            var foot = new Vector3(x, 0.07f, -Reach + 0.14f);
            var head = new Vector3(x, 1.45f, -SlabDepth * 0.5f - 0.07f);

            Beam(m, wood, foot, head, new Vector2(0.12f, 0.12f), Vector3.right);
            m.Box(B.Walnut, new Vector3(x, 1.5f, -SlabDepth * 0.5f - 0.035f), new Vector3(0.34f, 0.16f, 0.07f), 0.012f);
            m.Box(B.WalnutDark, new Vector3(x, 0.03f, foot.z - 0.02f), new Vector3(0.36f, 0.06f, 0.2f), 0.012f);
            m.Tube(B.WalnutDark, new Vector3(x + 0.13f, -0.05f, foot.z - 0.1f), new Vector3(x + 0.12f, 0.15f, foot.z - 0.09f), 0.025f, 6);
        }

        /// <summary>Sandbags heaped against the inside of the wall: a course of <paramref name="count"/>, and one fewer on top.</summary>
        static void Sandbags(B.Model m, int count, int key)
        {
            float z = -FootingDepth * 0.5f - 0.17f;

            for (int k = 0; k < count; k++)
                Bag(m, new Vector3((k - (count - 1) * 0.5f) * 0.5f, 0.085f, z + 0.02f * B.Jitter(key + k)), 4f * B.Jitter(key * 3 + k), key + k);

            for (int k = 0; k < count - 1; k++)
                Bag(m, new Vector3((k - (count - 2) * 0.5f) * 0.5f, 0.25f, z + 0.03f), 6f * B.Jitter(key * 5 + k), key + 10 + k);
        }

        static void Bag(B.Model m, Vector3 at, float yaw, int key, float length = 0.5f)
        {
            Quaternion lie = Quaternion.Euler(3f * B.Jitter(key), yaw, 4f * B.Jitter(key + 9));
            m.Box(Burlap[B.Hash(key) % Burlap.Length], at, new Vector3(length, 0.17f, 0.3f), 0.07f, lie, 2);
        }

        // --- The wall broken -----------------------------------------------------------------------------

        /// <summary>
        /// Below half: the bottom course whole, the middle one broken through with half of it knocked
        /// down to a stump, a stub of the top course left at one end with its bars showing, the rest down
        /// inside, the spike rail torn off and hanging.
        /// </summary>
        static B.Model BayDamaged(BayKey k)
        {
            var m = new B.Model { Shade = Shade };
            int key = k.Seed;
            var rng = new System.Random(key);
            float half = k.Length * 0.5f;
            (float a, float b) = Span(k);
            float w = b - a;
            bool lowAtStart = rng.Next(2) == 0;

            Footing(m, -half, half, FootingTop);
            Slab(m, SlabColour(k, 0), a, b, FootingTop, SlabHeight);

            // The middle course: broken through, one piece standing, the other a stump with a jagged top.
            float split = Mathf.Lerp(a, b, 0.4f + 0.2f * Next(rng));
            float y1 = FootingTop + SlabHeight;
            float standA = lowAtStart ? split : a, standB = lowAtStart ? b : split;
            float lowA = lowAtStart ? a : split, lowB = lowAtStart ? split : b;

            Slab(m, SlabColour(k, 1), standA, standB, y1, SlabHeight);
            Slab(m, SlabColour(k, 1), lowA, lowB, y1, SlabHeight * 0.45f);
            Jag(m, SlabColour(k, 1) * 0.96f, (lowA + lowB) * 0.5f, y1 + SlabHeight * 0.45f, lowB - lowA, key);

            for (int i = 0; i < 3; i++)
            {
                var from = new Vector3(Mathf.Lerp(lowA + 0.1f, lowB - 0.1f, (i + 0.5f) / 3f), y1 + SlabHeight * 0.4f, 0.08f * (i - 1));
                Rebar(m, from, new Vector3(0.3f * B.Jitter(key + i), 1f, 0.3f * B.Jitter(key + i + 4)), 0.3f + 0.15f * Next(rng), key + i);
            }

            // A stub of the top course at the standing end.
            float stub = Mathf.Min(0.55f, (standB - standA) * 0.45f);
            float stubA = lowAtStart ? standB - stub : standA;
            Slab(m, SlabColour(k, 2), stubA, stubA + stub, y1 + SlabHeight, SlabHeight * 0.6f);
            Jag(m, SlabColour(k, 2) * 0.96f, stubA + stub * 0.5f, y1 + SlabHeight * 1.6f, stub, key + 3);

            // What came down: a slab broken in two inside, chips both sides.
            float jitter = B.Jitter(key + 7);
            SlabPiece(m, SlabColour(k, 2), new Vector3(0.2f * jitter, 0.11f, -0.5f), new Vector3(Mathf.Min(1.1f, w * 0.55f), 0.2f, 0.4f),
                      Quaternion.Euler(4f * jitter, 8f * jitter, 7f));
            SlabPiece(m, SlabColour(k, 2) * 0.97f, new Vector3((lowA + lowB) * 0.5f, 0.3f, -0.5f), new Vector3(Mathf.Min(0.8f, w * 0.4f), 0.2f, 0.38f),
                      Quaternion.Euler(-38f, -10f * jitter, 4f));
            Chunks(m, a, b, 0.42f, 3, rng);
            Chunks(m, a, b, -0.45f, 2, rng);

            // The spike rail torn off one end and hanging down outside.
            var hangFrom = new Vector3(lowAtStart ? b - 0.1f : a + 0.1f, SlabTop - 0.02f, 0.05f);
            Beam(m, Iron, hangFrom, new Vector3(0f, 0.18f, 0.5f), new Vector2(0.05f, 0.03f), Vector3.up);

            if (k.Patch == Patch.Boarded) HangingBoards(m, standA, standB, y1, key);
            if (k.Patch == Patch.Sheathed) HangingSheet(m, standA, standB, key);
            return m;
        }

        /// <summary>Fallen: the footing with slabs down in a heap across it, chips, bent bars and the spike rail on the ground.</summary>
        static B.Model BayRubble(BayKey k)
        {
            var m = new B.Model { Shade = Shade };
            int key = k.Seed;
            var rng = new System.Random(key + 7);
            float half = k.Length * 0.5f;
            (float a, float b) = Span(k);
            float w = b - a, mid = (a + b) * 0.5f, j = B.Jitter(key);

            Footing(m, -half, half, 0.16f);

            SlabPiece(m, SlabColour(k, 0), new Vector3(mid - w * 0.12f, 0.17f, -0.12f), new Vector3(w * 0.7f, 0.22f, 0.5f), Quaternion.Euler(4f, 8f * j, 3f));
            SlabPiece(m, SlabColour(k, 1), new Vector3(mid + w * 0.16f, 0.36f, 0.1f), new Vector3(w * 0.55f, 0.2f, 0.46f), Quaternion.Euler(-14f, -12f * j, 9f));
            SlabPiece(m, SlabColour(k, 2), new Vector3(mid - w * 0.2f, 0.12f, 0.48f), new Vector3(Mathf.Min(0.9f, w * 0.4f), 0.18f, 0.4f),
                      Quaternion.Euler(6f, 15f * j, -5f));
            SlabPiece(m, SlabColour(k, 2) * 0.97f, new Vector3(mid + w * 0.25f, 0.1f, -0.48f), new Vector3(Mathf.Min(0.8f, w * 0.35f), 0.18f, 0.4f),
                      Quaternion.Euler(-5f, -12f * j, 4f));

            Chunks(m, a, b, 0.4f, 3, rng);
            Chunks(m, a, b, -0.4f, 3, rng);
            Rebar(m, new Vector3(mid - w * 0.1f, 0.3f, 0.05f), new Vector3(0.6f, 1f, 0.2f), 0.45f, key + 1);
            Rebar(m, new Vector3(mid + w * 0.2f, 0.42f, 0.15f), new Vector3(-0.4f, 1f, -0.5f), 0.4f, key + 5);

            Beam(m, Iron, new Vector3(a + 0.2f, 0.04f, -0.66f), new Vector3(b - 0.3f, 0.06f, -0.52f - 0.1f * j), new Vector2(0.05f, 0.03f), Vector3.up);

            if (k.Patch == Patch.Boarded)
            {
                for (int i = 0; i < 2; i++)
                    m.Box(B.Plank(key + i), new Vector3(mid + 0.2f * i, 0.03f + 0.03f * i, 0.56f - 0.12f * i), new Vector3(w * 0.8f, 0.04f, 0.18f), 0.012f,
                          Quaternion.Euler(0f, 4f * j + 3f * i, 0f));
            }
            else if (k.Patch == Patch.Sheathed)
            {
                FlatSheet(m, new Vector3(mid, 0.05f, 0.55f), Mathf.Min(1.1f, w * 0.6f), 0.4f, 6f * j, key);
            }

            return m;
        }

        /// <summary>A jagged top on a broken slab: a lump of it left tipped on the break.</summary>
        static void Jag(B.Model m, Color c, float x, float y, float width, int key)
        {
            float tilt = (B.Hash(key) % 2 == 0 ? 1f : -1f) * (10f + 8f * Mathf.Abs(B.Jitter(key + 1)));
            m.Box(c, new Vector3(x + width * 0.12f * B.Jitter(key + 2), y, 0f), new Vector3(width * 0.55f, 0.16f, SlabDepth * 0.84f), 0.025f,
                  Quaternion.Euler(0f, 0f, tilt));
        }

        static void SlabPiece(B.Model m, Color c, Vector3 centre, Vector3 size, Quaternion turn) => m.Box(c, centre, size, SlabRound, turn);

        /// <summary>A few chips of concrete along the wall's foot, <paramref name="z"/> out from its line (in when negative).</summary>
        static void Chunks(B.Model m, float a, float b, float z, int count, System.Random rng)
        {
            for (int k = 0; k < count; k++)
            {
                float size = 0.13f + 0.13f * Next(rng);
                var at = new Vector3(Mathf.Lerp(a, b, Next(rng)), size * 0.3f, z + Mathf.Sign(z) * 0.16f * Next(rng));
                Color c = SlabGrey[rng.Next(SlabGrey.Length)] * (0.92f + 0.08f * Next(rng));
                m.Box(c, at, new Vector3(size * 1.4f, size * 0.8f, size), 0.03f, Quaternion.Euler(rng.Next(-20, 20), rng.Next(0, 180), rng.Next(-20, 20)));
            }
        }

        /// <summary>Bent reinforcing bar out of broken concrete: two straight lengths with a kink.</summary>
        static void Rebar(B.Model m, Vector3 from, Vector3 heading, float length, int key)
        {
            Vector3 dir = heading.normalized;
            Vector3 kink = from + dir * (length * 0.55f);
            Vector3 bend = Quaternion.Euler(30f * B.Jitter(key), 50f * B.Jitter(key + 1), 25f * B.Jitter(key + 2)) * dir;
            m.Tube(RebarRust, from, kink, 0.016f, 5);
            m.Tube(RebarRust * 0.9f, kink, kink + bend * (length * 0.45f), 0.016f, 5);
        }

        /// <summary>The boards come away from one end: one hanging by a nail, one on the ground outside.</summary>
        static void HangingBoards(B.Model m, float a, float b, float y, int key)
        {
            float w = b - a;
            Beam(m, B.Plank(key), new Vector3(a + 0.1f, y + SlabHeight * 0.85f, 0.2f), new Vector3(a + w * 0.55f, y - 0.25f, 0.24f),
                 new Vector2(0.16f, 0.045f), Vector3.forward);
            m.Box(B.Plank(key + 1), new Vector3(a + w * 0.5f, 0.03f, 0.52f), new Vector3(w * 0.75f, 0.045f, 0.17f), 0.012f, Quaternion.Euler(0f, 6f * B.Jitter(key), 0f));
        }

        /// <summary>A sheet of the tin torn half off, hanging askew from its top bolts.</summary>
        static void HangingSheet(B.Model m, float a, float b, int key)
        {
            float width = Mathf.Min(0.9f, b - a), length = 1.1f;
            Color sheet = B.Tin[B.Hash(key) % B.Tin.Length];

            m.Transform = Matrix4x4.TRS(new Vector3((a + b) * 0.5f, 1.25f, SlabDepth * 0.5f + 0.06f),
                                        Quaternion.Euler(-14f, 0f, 24f * (B.Hash(key + 1) % 2 == 0 ? 1f : -1f)), Vector3.one);
            m.Corrugated(new Vector3(-width * 0.5f, 0f, 0f), Vector3.right, Vector3.down, width, length, 0.11f, 0.026f, 0.006f,
                         (u, v) => TinPaint(sheet, u, v, length, key), 0.34f);
            m.Transform = Matrix4x4.identity;
        }

        /// <summary>A sheet of tin lying flat on the ground, face up.</summary>
        static void FlatSheet(B.Model m, Vector3 centre, float width, float length, float yaw, int key)
        {
            Color sheet = B.Tin[B.Hash(key) % B.Tin.Length];
            m.Transform = Matrix4x4.TRS(centre, Quaternion.Euler(0f, yaw, 0f), Vector3.one);

            // Down along +Z and across +X: its face is cross(+Z, +X), straight up.
            m.Corrugated(new Vector3(-width * 0.5f, 0f, -length * 0.5f), Vector3.right, Vector3.forward, width, length, 0.11f, 0.026f, 0.006f,
                         (u, v) => TinPaint(sheet, u, v, length, key), 0.34f);
            m.Transform = Matrix4x4.identity;
        }

        // --- Shapes ------------------------------------------------------------------------------------------

        /// <summary>
        /// A squared timber or bar from <paramref name="from"/> to <paramref name="to"/>, <paramref name="section"/>
        /// across: x across it, y along <paramref name="up"/> (which must not lie along it).
        /// </summary>
        static void Beam(B.Model m, Color c, Vector3 from, Vector3 to, Vector2 section, Vector3 up)
        {
            Vector3 d = to - from;
            m.Box(c, (from + to) * 0.5f, new Vector3(section.x, section.y, d.magnitude), Mathf.Min(section.x, section.y) * 0.2f,
                  Quaternion.LookRotation(d, up));
        }

        /// <summary>A rounded box painted by <paramref name="paint"/>, which sees each point where the model has it.</summary>
        static void Rounded(B.Model m, Func<Vector3, Vector3, Color> paint, Vector3 centre, Vector3 size, float radius)
        {
            var shape = new SmoothMesh(1);
            shape.RoundedBox(0, centre, size, radius, Quaternion.identity, 1);
            m.Add(shape, paint);
        }

        static float Next(System.Random rng) => (float)rng.NextDouble();

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
