using System.Collections.Generic;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using S = LastOrders.EditorTools.SoupKitchenModel;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The gates and the bastions.
    ///
    /// A gate: two concrete pillars banded in hazard paint at the foot, each with a caged beacon on
    /// top, a concrete threshold with the rail the leaves run on, and the motor's box on the inside of
    /// one pillar. The leaves are in the diner's livery, as the scrap turret is: teal steel frames
    /// rusting low down, a cream corrugated panel below a red rail, bars with spear tips above, braced
    /// across, a latch where they meet - and on the wide gate the diner's red badge. Knocked down, the
    /// leaves lie flat inside among torn-off bars and chips of the pillars.
    ///
    /// A gate's frame has local +Z into the compound. The leaves run in the wall's middle and slide
    /// into it, so nothing on them stands further than <see cref="SlabDepth"/> / 2 from it or higher
    /// than the slabs.
    ///
    /// A bastion: the same concrete as the wall, broader - slabs on all four faces between square
    /// posts at its corners, a deck on top, a parapet of sandbags along its outer edge, steel straps
    /// bolted up its outer face and a ladder up its inner one.
    /// </summary>
    public static partial class CompoundWallModels
    {
        const string GateFolder = Folder + "/Gates";
        const string BastionFolder = Folder + "/Bastions";

        // --- Gate measurements (metres; X along the opening, +Z in) --------------------------------------

        const float PillarSize = 0.86f, PillarTop = 2.55f;

        /// <summary>Where a beacon's lens sits on its pillar.</summary>
        public const float LampHeight = PillarTop + 0.24f;

        /// <summary>The leaves' top, spear tips and all: under the slabs' top, so a leaf slid into the wall is out of sight.</summary>
        const float LeafTop = 1.88f;

        const float LeafDepth = 0.09f, LeafBottom = 0.17f, LeafRail = 0.09f, MidRail = 0.88f, Stile = 0.09f;

        /// <summary>A leaf wider than this is the main gate's: braced in a zig-zag, a middle stile, a drop bolt and the badge.</summary>
        const float WideLeaf = 3f;

        static readonly Color GateGrey = B.Hex("8F8D87");

        /// <summary>The diner's livery, as on the scrap turret. Mid cream: any lighter and its lit side clips.</summary>
        static readonly Color Teal = B.Hex("3D6762");
        static readonly Color TealDark = B.Hex("33554F");
        static readonly Color TealLight = B.Hex("47756F");
        static readonly Color Cream = B.Hex("AFA898");
        static readonly Color Red = B.Hex("8E3B33");

        /// <summary>Hazard paint, worn and muted: ochre and near-black.</summary>
        static readonly Color Hazard = B.Hex("A88C45");
        static readonly Color HazardDark = B.Hex("34312C");

        static int Cm(float metres) => Mathf.RoundToInt(metres * 100f);

        public static Mesh GatePillars(float width) => Get(GateFolder, $"Gate_Pillars_{Cm(width)}", () => Pillars(width));

        public static Mesh GateLeaf(float width, bool left) =>
            Get(GateFolder, $"Gate_Leaf{(left ? "L" : "R")}_{Cm(width)}", () => Leaf(width, left));

        /// <summary>A beacon's glass: the gate lights it amber, green or red.</summary>
        public static Mesh GateLens() => Get(GateFolder, "Gate_BeaconLens", Lens);

        public static Mesh GateWreck(float width) => Get(GateFolder, $"Gate_Wreck_{Cm(width)}", () => Wreck(width));

        // --- Pillars ---------------------------------------------------------------------------------------

        static B.Model Pillars(float width)
        {
            var m = new B.Model { Shade = Shade };
            float half = width * 0.5f;

            // The threshold, and the rail the leaves run on, on into the wall either side.
            m.Box(FootingGrey, new Vector3(0f, 0.01f, 0f), new Vector3(width, 0.06f, 0.95f), 0.02f);
            m.Box(Steel, new Vector3(0f, 0.055f, 0f), new Vector3(width + PillarSize, 0.03f, 0.07f), 0.008f);

            for (int side = -1; side <= 1; side += 2) Pillar(m, side * half, side);

            // The motor that drives the leaves, boxed on the inside of the left pillar, its cable down into the ground.
            float face = PillarSize * 0.5f;
            m.Box(Steel, new Vector3(-half, 1.08f, face + 0.08f), new Vector3(0.36f, 0.46f, 0.16f), 0.03f);
            m.Box(SteelLight, new Vector3(-half, 1.22f, face + 0.165f), new Vector3(0.3f, 0.06f, 0.012f), 0.004f);
            m.Box(HazardDark, new Vector3(-half + 0.1f, 1.0f, face + 0.165f), new Vector3(0.06f, 0.06f, 0.012f), 0.004f);
            m.Tube(IronDark, new Vector3(-half + 0.1f, 0.85f, face + 0.07f), new Vector3(-half + 0.1f, -0.02f, face + 0.07f), 0.022f, 6);
            return m;
        }

        /// <summary>A pillar on a plinth, hazard bands round its foot, a string course, a stepped cap, a beacon, and guides where the leaf runs in.</summary>
        static void Pillar(B.Model m, float x, int side)
        {
            m.Box(FootingGrey, new Vector3(x, 0.16f, 0f), new Vector3(PillarSize + 0.12f, 0.32f, PillarSize + 0.12f), 0.04f);
            m.Box(GateGrey, new Vector3(x, PillarTop * 0.5f, 0f), new Vector3(PillarSize, PillarTop, PillarSize), 0.055f);

            for (int k = 0; k < 4; k++)
                m.Box(k % 2 == 0 ? Hazard : HazardDark, new Vector3(x, 0.36f + 0.17f * k + 0.085f, 0f),
                      new Vector3(PillarSize + 0.025f, 0.17f, PillarSize + 0.025f), 0.05f);

            m.Box(CapGrey, new Vector3(x, PillarTop - 0.32f, 0f), new Vector3(PillarSize + 0.06f, 0.07f, PillarSize + 0.06f), 0.02f);
            m.Box(CapGrey, new Vector3(x, PillarTop + 0.05f, 0f), new Vector3(PillarSize + 0.16f, 0.1f, PillarSize + 0.16f), 0.03f);
            m.Box(CapGrey, new Vector3(x, PillarTop + 0.13f, 0f), new Vector3(PillarSize - 0.2f, 0.06f, PillarSize - 0.2f), 0.02f);
            Beacon(m, new Vector3(x, PillarTop + 0.16f, 0f));

            // Steel guides on the face toward the opening, either side of where the leaf runs into the wall.
            float jamb = x - side * (PillarSize * 0.5f + 0.012f);
            for (int z = -1; z <= 1; z += 2)
                m.Box(Steel, new Vector3(jamb, 1.0f, z * 0.09f), new Vector3(0.03f, 1.5f, 0.06f), 0.008f);
        }

        /// <summary>The beacon's drum, and a wire cage over where the lens goes.</summary>
        static void Beacon(B.Model m, Vector3 foot)
        {
            const float Drum = 0.17f, Cage = 0.16f;
            m.Lathe((p, n) => IronDark, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(Drum, 0f), new Vector2(Drum, 0f), new Vector2(Drum, 0.08f), new Vector2(Drum, 0.08f),
                new Vector2(0f, 0.08f),
            }, 14);

            Vector3 top = foot + Vector3.up * 0.36f;
            for (int k = 0; k < 4; k++)
            {
                float a = (k + 0.5f) * Mathf.PI * 0.5f;
                var round = new Vector3(Mathf.Cos(a) * Cage, 0f, Mathf.Sin(a) * Cage);
                Vector3 rim = foot + round + Vector3.up * 0.08f;
                Vector3 bow = foot + round + Vector3.up * 0.24f;
                m.Tube(IronDark, rim, bow, 0.01f, 5);
                m.Tube(IronDark, bow, top, 0.01f, 5);
            }

            m.Lathe((p, n) => IronDark, top - Vector3.up * 0.015f, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.05f, 0f), new Vector2(0.05f, 0f), new Vector2(0.045f, 0.025f), new Vector2(0f, 0.03f),
            }, 10);
        }

        static B.Model Lens()
        {
            var m = new B.Model();
            m.Lathe((p, n) => Color.white, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.13f, 0f), new Vector2(0.13f, 0f), new Vector2(0.13f, 0.065f), new Vector2(0.12f, 0.13f),
                new Vector2(0.09f, 0.175f), new Vector2(0.05f, 0.2f), new Vector2(0f, 0.21f),
            }, 16);
            return m;
        }

        // --- Leaves ----------------------------------------------------------------------------------------

        /// <summary>
        /// One leaf, <paramref name="width"/> across, standing on its rollers at its own origin.
        /// <paramref name="left"/>: its +X edge meets the other leaf, where the latch is.
        /// </summary>
        static B.Model Leaf(float width, bool left)
        {
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.84f, 1f, Smooth(p.y / 0.7f)) * (n.y < -0.6f ? 0.85f : 1f) };
            bool wide = width > WideLeaf;
            float half = width * 0.5f;
            float topRail = LeafTop - 0.1f;
            float meet = left ? half : -half, inward = -Mathf.Sign(meet);

            // Hangers and the rollers it runs on.
            foreach (float x in new[] { -half + 0.32f, half - 0.32f })
            {
                m.Tube(IronDark, new Vector3(x, 0.115f, -0.035f), new Vector3(x, 0.115f, 0.035f), 0.045f, 12);
                m.Box(Iron, new Vector3(x, LeafBottom - 0.01f, 0f), new Vector3(0.16f, 0.05f, 0.1f), 0.01f);
            }

            // The frame: rails and stiles in teal enamel, rusting low down; the middle rail the diner's red.
            Rounded(m, FramePaint, new Vector3(0f, LeafBottom + LeafRail * 0.5f, 0f), new Vector3(width, LeafRail, LeafDepth), 0.02f);
            Rounded(m, (p, n) => Red, new Vector3(0f, MidRail + LeafRail * 0.5f, 0f), new Vector3(width - 0.02f, LeafRail, LeafDepth - 0.006f), 0.02f);
            Rounded(m, FramePaint, new Vector3(0f, topRail - LeafRail * 0.5f, 0f), new Vector3(width, LeafRail, LeafDepth), 0.02f);
            for (int side = -1; side <= 1; side += 2)
                Rounded(m, FramePaint, new Vector3(side * (half - Stile * 0.5f), (LeafBottom + topRail) * 0.5f, 0f),
                        new Vector3(Stile, topRail - LeafBottom, LeafDepth + 0.006f), 0.02f);

            if (wide)
                Rounded(m, FramePaint, new Vector3(0f, (LeafBottom + topRail) * 0.5f, 0f), new Vector3(0.07f, topRail - LeafBottom, LeafDepth + 0.003f), 0.018f);

            // The panel below the red rail: cream corrugated sheet, ribbed on both faces.
            float x0 = -half + Stile, x1 = half - Stile, y0 = LeafBottom + LeafRail, y1 = MidRail;
            for (int face = -1; face <= 1; face += 2)
            {
                var corner = new Vector3(face > 0 ? x0 : x1, y1, face * 0.003f);
                m.Corrugated(corner, face > 0 ? Vector3.right : Vector3.left, Vector3.down, x1 - x0, y1 - y0, 0.12f, 0.024f, 0.003f,
                             (u, v) => PanelPaint(u, v, y1 - y0), 0.31f);
            }

            // Bars above it, each with a spear tip standing on the top rail.
            float barFoot = MidRail + LeafRail;
            int bars = Mathf.Max(3, Mathf.RoundToInt((x1 - x0) / 0.13f));
            for (int i = 0; i < bars; i++)
            {
                float x = Mathf.Lerp(x0, x1, (i + 0.5f) / bars);
                if (wide && Mathf.Abs(x) < 0.06f) continue;

                m.Tube(Teal, new Vector3(x, barFoot - 0.01f, 0f), new Vector3(x, topRail + 0.005f, 0f), 0.016f, 5);
                m.Tube((p, n) => TealLight, new Vector3(x, topRail, 0f), new Vector3(x, LeafTop, 0f), 0.028f, 0.002f, 3);
            }

            // Bracing across the bars, on the inside face: one diagonal on a small leaf, a zig-zag along a wide one.
            int panels = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / 1.3f));
            for (int k = 0; k < panels; k++)
            {
                float a = Mathf.Lerp(x0, x1, k / (float)panels) + 0.04f, b = Mathf.Lerp(x0, x1, (k + 1f) / panels) - 0.04f;
                bool rising = (k % 2 == 0) == left;
                float low = barFoot + 0.03f, high = topRail - LeafRail - 0.03f;
                var from = new Vector3(a, rising ? low : high, 0.03f);
                var to = new Vector3(b, rising ? high : low, 0.03f);
                Beam(m, TealDark, from, to, new Vector2(0.06f, 0.022f), Vector3.forward);
            }

            // A latch where the leaves meet, on both faces; a drop bolt down the inside of a wide leaf's meeting stile.
            float lx = meet + inward * (Stile + 0.07f);
            for (int face = -1; face <= 1; face += 2)
            {
                float z = face * (LeafDepth * 0.5f + 0.018f);
                m.Box(Iron, new Vector3(lx, 1.12f, z), new Vector3(0.12f, 0.2f, 0.036f), 0.01f);
                m.Tube(SteelLight, new Vector3(lx + inward * 0.03f, 1.06f, z + face * 0.02f), new Vector3(lx + inward * 0.03f, 1.18f, z + face * 0.02f),
                       0.012f, 6);
            }

            if (wide)
            {
                float bx = meet + inward * (Stile + 0.16f);
                m.Tube(Iron, new Vector3(bx, LeafBottom - 0.06f, LeafDepth * 0.5f + 0.02f), new Vector3(bx, 0.84f, LeafDepth * 0.5f + 0.02f), 0.017f, 6);
                m.Box(Iron, new Vector3(bx, 0.62f, LeafDepth * 0.5f + 0.012f), new Vector3(0.08f, 0.05f, 0.02f), 0.006f);

                // The diner's badge on the panel, both faces: a red disc in a cream ring, the meeting edge's side of the leaf.
                var badge = new Vector3(meet + inward * half * 0.5f, (y0 + y1) * 0.5f, 0f);
                for (int face = -1; face <= 1; face += 2)
                {
                    var normal = new Vector3(0f, 0f, face);
                    S.Disc(m, (p, n) => Cream, badge + normal * 0.03f, normal, 0.22f);
                    S.Disc(m, (p, n) => Red, badge + normal * 0.034f, normal, 0.17f);
                    S.Disc(m, (p, n) => Cream, badge + normal * 0.038f, normal, 0.05f, 12);
                }
            }

            return m;
        }

        static Color FramePaint(Vector3 p, Vector3 n) => Color.Lerp(Teal, Rust, Smooth((0.42f - p.y) / 0.25f) * 0.55f);

        /// <summary>The panel's cream, rusting up from its foot and down from a rivet here and there.</summary>
        static Color PanelPaint(float u, float v, float length)
        {
            float rust = Smooth((v - (length - 0.2f)) / 0.18f) * 0.6f;
            if (Mathf.Sin(u * 7.1f) > 0.95f) rust = Mathf.Max(rust, 0.35f * Smooth(v / length));
            return Color.Lerp(Cream, Rust, rust);
        }

        /// <summary>What lies about a gate knocked down, besides its leaves: bars torn out and bent, a roller, chips off the pillars.</summary>
        static B.Model Wreck(float width)
        {
            var m = new B.Model { Shade = Shade };
            var rng = new System.Random(Cm(width) * 31 + 5);
            float half = width * 0.5f;

            int bars = 4 + Mathf.RoundToInt(width / 2f);
            for (int k = 0; k < bars; k++)
            {
                var from = new Vector3(Mathf.Lerp(-half + 0.4f, half - 0.4f, Next(rng)), 0.025f, 0.5f + 1.8f * Next(rng));
                Vector3 dir = Quaternion.Euler(0f, rng.Next(0, 180), 0f) * Vector3.right;
                Vector3 kink = from + dir * (0.3f + 0.3f * Next(rng));
                Vector3 end = kink + Quaternion.Euler(-10f - 25f * Next(rng), rng.Next(-60, 60), 0f) * dir * 0.35f;
                m.Tube(Teal, from, kink, 0.016f, 6);
                m.Tube(Teal, kink, end, 0.016f, 6);
            }

            // A roller off its hanger, on its side.
            var roller = new Vector3(half * 0.35f, 0f, 1.1f);
            m.Tube(IronDark, roller, roller + Vector3.up * 0.07f, 0.045f, 12);

            // Chips off the pillars, round their feet.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = 0; k < 3; k++)
                {
                    float size = 0.1f + 0.1f * Next(rng);
                    var at = new Vector3(side * (half - 0.2f - 0.5f * Next(rng)), size * 0.3f, (Next(rng) - 0.3f) * 1.4f);
                    m.Box(GateGrey * (0.92f + 0.08f * Next(rng)), at, new Vector3(size * 1.3f, size * 0.8f, size), 0.025f,
                          Quaternion.Euler(rng.Next(-20, 20), rng.Next(0, 180), rng.Next(-20, 20)));
                }
            }

            return m;
        }

        /// <summary>Where the two leaves of a knocked-down gate <paramref name="width"/> wide lie: flat inside, one half over the other.</summary>
        public static void FallenLeaves(Transform left, Transform right, float width)
        {
            left.localPosition = new Vector3(-width * 0.26f, 0.07f, 0.35f);
            left.localRotation = Quaternion.Euler(86f, 9f, 0f);
            right.localPosition = new Vector3(width * 0.2f, 0.16f, 0.22f);
            right.localRotation = Quaternion.Euler(80f, -13f, 3f);
        }

        // --- Bastions ----------------------------------------------------------------------------------------

        const float BastionDeck = 0.12f, BastionInset = 0.12f, ParapetRise = 0.34f;

        /// <summary>A bastion <paramref name="length"/> along the wall and <paramref name="thickness"/> through it, +Z out.</summary>
        public static Mesh BastionMesh(float length, float thickness, State state, int variant) =>
            Get(BastionFolder, $"Bastion_{state}_{Cm(length)}x{Cm(thickness)}_{variant}", () =>
                state == State.Intact ? BastionIntact(length, thickness, variant)
                : state == State.Damaged ? BastionDamaged(length, thickness, variant)
                : BastionRubble(length, thickness, variant));

        static B.Model BastionIntact(float length, float thickness, int variant)
        {
            float top = CompoundWallSceneSetup.BastionHeight;
            var m = new B.Model { Shade = Shade };
            float hx = length * 0.5f, hz = thickness * 0.5f;
            float px = hx - BastionInset, pz = hz - BastionInset;

            m.Box(FootingGrey, new Vector3(0f, 0.095f, 0f), new Vector3(length + 0.16f, 0.25f, thickness + 0.16f), 0.035f);
            m.Box(CoreGrey, new Vector3(0f, (FootingTop + top) * 0.5f, 0f), new Vector3(length - 0.3f, top - FootingTop, thickness - 0.3f), 0.03f);
            BastionSlabs(m, length, thickness, top, variant, 3);

            // The deck, a little proud of the slabs all round.
            m.Box(CapGrey, new Vector3(0f, top - BastionDeck * 0.5f, 0f), new Vector3(length + 0.06f, BastionDeck, thickness + 0.06f), 0.03f);

            for (int k = 0; k < 4; k++)
            {
                float x = ((k & 1) == 0 ? -1f : 1f) * px, z = ((k & 2) == 0 ? -1f : 1f) * pz;
                float height = top + ParapetRise;
                m.Box(FootingGrey, new Vector3(x, PlinthTop * 0.5f, z), new Vector3(PlinthSize, PlinthTop, PlinthSize), 0.035f);
                m.Box(PostGrey, new Vector3(x, height * 0.5f, z), new Vector3(PostSize, height, PostSize), 0.045f);
                m.Box(CapGrey, new Vector3(x, height + PostCap * 0.5f, z), new Vector3(PostSize + 0.1f, PostCap, PostSize + 0.1f), 0.025f);
            }

            // A parapet of sandbags along the outer edge, two courses, between the posts.
            float from = -px + PostSize * 0.5f + 0.02f, to = px - PostSize * 0.5f - 0.02f;
            int bags = Mathf.Max(1, Mathf.RoundToInt((to - from) / 0.5f));
            float w = (to - from) / bags, zBag = hz - 0.17f;
            for (int k = 0; k < bags; k++)
                Bag(m, new Vector3(from + w * (k + 0.5f), top + 0.085f, zBag), 3f * B.Jitter(variant * 7 + k), variant * 31 + k, w);
            for (int k = 0; k < bags - 1; k++)
                Bag(m, new Vector3(from + w * (k + 1f), top + 0.25f, zBag + 0.01f), 4f * B.Jitter(variant * 11 + k), variant * 37 + k, w);

            // Steel straps bolted up the outer face: it was built to carry weight.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * length * 0.2f;
                m.Box(Iron, new Vector3(x, (FootingTop + 0.1f + top - 0.2f) * 0.5f, hz + 0.018f), new Vector3(0.13f, top - FootingTop - 0.3f, 0.035f), 0.01f);
                for (int b = 0; b < 4; b++)
                    m.Box(SteelLight, new Vector3(x, FootingTop + 0.3f + b * 0.5f, hz + 0.04f), new Vector3(0.05f, 0.05f, 0.02f), 0.008f);
            }

            // A ladder up the inside.
            float lx = length * 0.18f, lz = -hz - 0.1f;
            for (int side = -1; side <= 1; side += 2)
            {
                m.Tube(Steel, new Vector3(lx + side * 0.2f, 0f, lz), new Vector3(lx + side * 0.2f, top + 0.45f, lz), 0.022f, 8);
                for (int k = 0; k < 2; k++)
                    m.Box(Iron, new Vector3(lx + side * 0.2f, 0.7f + k * 1.2f, lz + 0.06f), new Vector3(0.04f, 0.04f, 0.12f), 0.008f);
            }

            for (int k = 0; k < 8; k++)
                m.Tube(Steel, new Vector3(lx - 0.2f, 0.28f + k * 0.3f, lz), new Vector3(lx + 0.2f, 0.28f + k * 0.3f, lz), 0.016f, 6);

            return m;
        }

        /// <summary>
        /// Slab courses on all four faces between the corner posts, up to <paramref name="rows"/> of the three;
        /// broken ones stop there.
        /// </summary>
        static void BastionSlabs(B.Model m, float length, float thickness, float top, int variant, int rows)
        {
            float hx = length * 0.5f, hz = thickness * 0.5f;
            float spanX = 2f * (hx - BastionInset) - PostSize + 0.06f, spanZ = 2f * (hz - BastionInset) - PostSize + 0.06f;
            float h = (top - BastionDeck - FootingTop) / 3f;

            for (int r = 0; r < rows; r++)
            {
                float y = FootingTop + r * h + h * 0.5f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Color c = SlabGrey[B.Hash(variant * 17 + r * 5 + side + 3) % SlabGrey.Length];
                    m.Box(c, new Vector3(0f, y, side * (hz - 0.13f)), new Vector3(spanX, h - 0.006f, 0.26f), SlabRound);

                    Color e = SlabGrey[B.Hash(variant * 23 + r * 7 + side + 11) % SlabGrey.Length];
                    m.Box(e, new Vector3(side * (hx - 0.13f), y, 0f), new Vector3(0.26f, h - 0.006f, spanZ), SlabRound);
                }
            }
        }

        /// <summary>Below half: the top course and the deck gone, posts snapped, sandbags spilled down the outside, bars out of the broken core.</summary>
        static B.Model BastionDamaged(float length, float thickness, int variant)
        {
            float top = CompoundWallSceneSetup.BastionHeight;
            var m = new B.Model { Shade = Shade };
            var rng = new System.Random(variant * 7919 + Cm(length) + Cm(thickness) * 3);
            float hx = length * 0.5f, hz = thickness * 0.5f;
            float px = hx - BastionInset, pz = hz - BastionInset;
            float h = (top - BastionDeck - FootingTop) / 3f;

            m.Box(FootingGrey, new Vector3(0f, 0.095f, 0f), new Vector3(length + 0.16f, 0.25f, thickness + 0.16f), 0.035f);
            float coreTop = FootingTop + h * 2f + 0.1f;
            m.Box(CoreGrey, new Vector3(0f, (FootingTop + coreTop) * 0.5f, 0f), new Vector3(length - 0.3f, coreTop - FootingTop, thickness - 0.3f), 0.03f);
            BastionSlabs(m, length, thickness, top, variant, 2);

            // Stubs of the top course at two corners, jagged.
            float y2 = FootingTop + h * 2f;
            m.Box(SlabGrey[1], new Vector3(-hx * 0.5f, y2 + h * 0.3f, hz - 0.13f), new Vector3(hx * 0.8f, h * 0.6f, 0.26f), SlabRound);
            Jag(m, SlabGrey[1], -hx * 0.5f, y2 + h * 0.6f, hx * 0.8f, variant + 3);
            m.Box(SlabGrey[3], new Vector3(hx - 0.13f, y2 + h * 0.25f, -hz * 0.4f), new Vector3(0.26f, h * 0.5f, hz * 0.9f), SlabRound);

            for (int k = 0; k < 4; k++)
            {
                float x = ((k & 1) == 0 ? -1f : 1f) * px, z = ((k & 2) == 0 ? -1f : 1f) * pz;
                float height = top * (0.62f + 0.12f * k / 3f) + 0.2f * Next(rng);
                StumpAt(m, x, z, height, variant * 13 + k);
            }

            for (int k = 0; k < 4; k++)
            {
                var from = new Vector3(Mathf.Lerp(-hx + 0.4f, hx - 0.4f, (k + 0.5f) / 4f), coreTop - 0.05f, (Next(rng) - 0.5f) * hz);
                Rebar(m, from, new Vector3(B.Jitter(variant + k) * 0.5f, 1f, B.Jitter(variant + k + 9) * 0.5f), 0.35f + 0.2f * Next(rng), variant * 5 + k);
            }

            // The parapet's bags spilled down the outside, one still up on the broken top.
            for (int k = 0; k < 4; k++)
                Bag(m, new Vector3(Mathf.Lerp(-hx + 0.3f, hx - 0.3f, (k + Next(rng) * 0.5f) / 4f), 0.09f + (k == 2 ? 0.17f : 0f), hz + 0.32f + 0.1f * Next(rng)),
                    rng.Next(-40, 40), variant * 41 + k);
            Bag(m, new Vector3(hx * 0.3f, coreTop + 0.08f, hz * 0.3f), 25f, variant * 43);

            for (int side = -1; side <= 1; side += 2)
                Chunks(m, -hx, hx, side * (hz + 0.2f), 3, rng);

            // The ladder still up the inside.
            float lx = length * 0.18f, lz = -hz - 0.1f;
            for (int side = -1; side <= 1; side += 2)
                m.Tube(Steel, new Vector3(lx + side * 0.2f, 0f, lz), new Vector3(lx + side * 0.2f, coreTop + 0.2f, lz), 0.022f, 8);
            for (int k = 0; k < 5; k++)
                m.Tube(Steel, new Vector3(lx - 0.2f, 0.28f + k * 0.3f, lz), new Vector3(lx + 0.2f, 0.28f + k * 0.3f, lz), 0.016f, 6);

            return m;
        }

        /// <summary>Fallen: the footing, four stumps of post, slabs and bags heaped over the footprint.</summary>
        static B.Model BastionRubble(float length, float thickness, int variant)
        {
            var m = new B.Model { Shade = Shade };
            var rng = new System.Random(variant * 7919 + Cm(length) * 5 + Cm(thickness));
            float hx = length * 0.5f, hz = thickness * 0.5f;
            float px = hx - BastionInset, pz = hz - BastionInset;

            m.Box(FootingGrey, new Vector3(0f, 0.07f, 0f), new Vector3(length + 0.16f, 0.2f, thickness + 0.16f), 0.035f);
            m.Box(CoreGrey, new Vector3(0f, 0.3f, 0f), new Vector3(length - 0.5f, 0.3f, thickness - 0.5f), 0.04f);

            for (int k = 0; k < 4; k++)
            {
                float x = ((k & 1) == 0 ? -1f : 1f) * px, z = ((k & 2) == 0 ? -1f : 1f) * pz;
                StumpAt(m, x, z, 0.5f + 0.35f * Next(rng), variant * 17 + k);
            }

            for (int k = 0; k < 7; k++)
            {
                var at = new Vector3((Next(rng) - 0.5f) * (length - 0.6f), 0.3f + 0.25f * Next(rng), (Next(rng) - 0.5f) * (thickness - 0.6f));
                var size = new Vector3(0.6f + 0.5f * Next(rng), 0.2f, 0.4f + 0.25f * Next(rng));
                SlabPiece(m, SlabGrey[rng.Next(SlabGrey.Length)], at, size,
                          Quaternion.Euler((Next(rng) - 0.5f) * 40f, rng.Next(0, 180), (Next(rng) - 0.5f) * 30f));
            }

            for (int k = 0; k < 4; k++)
                Bag(m, new Vector3((Next(rng) - 0.5f) * length, 0.09f, hz + 0.25f + 0.2f * Next(rng)), rng.Next(-60, 60), variant * 47 + k);

            for (int side = -1; side <= 1; side += 2)
                Chunks(m, -hx, hx, side * (hz + 0.15f), 3, rng);

            Rebar(m, new Vector3(-hx * 0.3f, 0.45f, 0f), new Vector3(0.5f, 1f, 0.3f), 0.5f, variant + 1);
            Rebar(m, new Vector3(hx * 0.35f, 0.45f, hz * 0.2f), new Vector3(-0.4f, 1f, -0.4f), 0.45f, variant + 7);
            return m;
        }

        /// <summary>A post snapped at <paramref name="height"/>, standing at (<paramref name="x"/>, <paramref name="z"/>).</summary>
        static void StumpAt(B.Model m, float x, float z, float height, int key)
        {
            m.Transform = Matrix4x4.Translate(new Vector3(0f, 0f, z));
            Stump(m, x, height, key);
            m.Transform = Matrix4x4.identity;
        }
    }
}
