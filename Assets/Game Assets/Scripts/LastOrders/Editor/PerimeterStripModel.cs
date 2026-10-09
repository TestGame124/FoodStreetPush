using System.Collections.Generic;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using L = LastOrders.EditorTools.PerimeterStripLayout;
using S = LastOrders.EditorTools.SoupKitchenModel;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The ground of the strip outside the walls, modelled in code: what tells it apart from the grass
    /// and the uncleared land beyond at a glance.
    ///
    /// Old concrete paving laid in a running bond, worn: slabs cracked across, a few gone to the
    /// gravel bed under them, one or two oil-stained, all settled a little out of true, with weeds in
    /// the joints. A broken kerb runs along its outer edge, the line between the safe ground and the
    /// land still to clear. In front of every gate the paving gives way to a gravel apron worn into
    /// two tracks by feet and barrows. And under every build slot a reinforced patch - a steel road
    /// plate bolted down with its outer edge in worn hazard stripes, or a fresh concrete pad with its
    /// anchor bolts and rebar showing - the ground a barricade or a trap is fixed to.
    ///
    /// One mesh per side, in world space, painted in its vertices on LastOrders/Stylized like every
    /// other prop. Deterministic: the same layout always lays the same slabs.
    /// </summary>
    public static class PerimeterStripModel
    {
        // --- Measurements (metres; y is up from the grass) --------------------------------------------

        const float BedTop = 0.04f;
        const float SlabTop = 0.075f, SlabThick = 0.05f, SlabLength = 0.9f, Joint = 0.035f;
        const int Rows = 3;

        /// <summary>The patch under a slot, a little bigger than the slot.</summary>
        static readonly Vector2 Patch = new Vector2(L.SlotLength + 0.1f, L.SlotDepth + 0.1f);

        // --- The palette --------------------------------------------------------------------------------

        static readonly Color[] Paving = { B.Hex("7D7C78"), B.Hex("85847F"), B.Hex("787772"), B.Hex("82807A"), B.Hex("7A7975") };
        static readonly Color Stained = B.Hex("64625E");
        static readonly Color Bleached = B.Hex("8E8C86");
        static readonly Color Kerb = B.Hex("8A8984");
        static readonly Color Gravel = B.Hex("857D72");
        static readonly Color GravelDark = B.Hex("736B60");
        static readonly Color Moss = B.Hex("6F7556");
        static readonly Color Track = B.Hex("6A5F52");
        static readonly Color PlateSteel = B.Hex("5D6266");
        static readonly Color PlateEdge = B.Hex("4D5155");
        static readonly Color Rust = B.Hex("7D5E4A");
        static readonly Color Bolt = B.Hex("8D9398");
        static readonly Color Ochre = B.Hex("8F7A4A");
        static readonly Color Charcoal = B.Hex("34312C");
        static readonly Color FreshConcrete = B.Hex("94928C");
        static readonly Color FreshEdge = B.Hex("85837D");
        static readonly Color Weed = B.Leaf;
        static readonly Color WeedLight = B.LeafLight;

        /// <summary>
        /// The strip along <paramref name="side"/>, with its gates' aprons (spans along it) and a patch
        /// under each of <paramref name="slots"/>.
        /// </summary>
        public static B.Model Build(L.Side side, IReadOnlyList<Vector2> gates, IReadOnlyList<L.Slot> slots)
        {
            // Darker at the foot of the wall, where it is always in its shade.
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.8f, 1f, S.Smooth((side.OutOf(p) - L.WallHalf) / 0.7f)) };
            var rng = new System.Random(side.Key * 7919);

            Bed(m, side);

            var aprons = new List<Vector2>();
            foreach (Vector2 gate in gates) aprons.Add(new Vector2(gate.x - L.ApronMargin, gate.y + L.ApronMargin));

            var patches = new List<Rect>();
            foreach (L.Slot slot in slots)
                patches.Add(new Rect(slot.Along - Patch.x * 0.5f, slot.Out - Patch.y * 0.5f, Patch.x, Patch.y));

            Slabs(m, side, aprons, patches, rng);
            Kerbs(m, side, aprons, rng);
            foreach (Vector2 apron in aprons) Apron(m, side, apron, rng);
            foreach (L.Slot slot in slots)
            {
                if (slot.Plate) SteelPlate(m, side, slot, rng);
                else ConcretePad(m, side, slot, rng);
            }

            return m;
        }

        // --- The bed --------------------------------------------------------------------------------------

        /// <summary>Packed gravel under it all, mottled, mossier toward the outer edge: what shows through gaps and aprons.</summary>
        static void Bed(B.Model m, L.Side side)
        {
            float length = side.To - side.From;
            float depth = L.Depth - L.Inner;
            int along = Mathf.Max(2, Mathf.CeilToInt(length / 0.5f));

            m.Surface((p, n) =>
            {
                float a = side.AlongOf(p), o = side.OutOf(p);
                float mottle = Mathf.Sin(a * 3.1f + o * 1.7f) * Mathf.Sin(a * 1.3f - o * 4.1f);
                Color c = Color.Lerp(Gravel, GravelDark, 0.5f + 0.5f * mottle);
                return Color.Lerp(c, Moss, S.Smooth((o - 2.2f) / 0.6f) * 0.5f);
            }, along, 4, (u, v) =>
            {
                float a = side.From + u * length, o = L.Inner + v * depth;
                float y = BedTop + 0.005f * Mathf.Sin(a * 2.3f) * Mathf.Sin(o * 3.7f + a);
                return side.World(a, o, y);
            });
        }

        // --- The paving ------------------------------------------------------------------------------------

        static void Slabs(B.Model m, L.Side side, List<Vector2> aprons, List<Rect> patches, System.Random rng)
        {
            float rowDepth = (L.Depth - L.KerbWidth - (L.WallHalf + 0.02f)) / Rows;

            for (int row = 0; row < Rows; row++)
            {
                float o0 = L.WallHalf + 0.02f + row * rowDepth;
                float o1 = o0 + rowDepth;

                // Running bond: every other row starts half a slab along.
                float offset = row % 2 == 0 ? 0f : SlabLength * 0.5f;

                var runs = new List<Vector2> { new Vector2(side.From, side.To) };
                foreach (Vector2 apron in aprons) runs = L.Cut(runs, apron.x, apron.y);

                foreach (Vector2 run in runs)
                {
                    float first = Mathf.Floor((run.x - offset) / SlabLength) * SlabLength + offset;
                    for (float a = first; a < run.y; a += SlabLength)
                    {
                        float a0 = Mathf.Max(a, run.x), a1 = Mathf.Min(a + SlabLength, run.y);
                        if (a1 - a0 < 0.12f) continue;

                        double roll = rng.NextDouble();
                        if (Covered(patches, a0, a1, o0, o1)) continue;

                        // A few gone, down to the gravel.
                        if (roll < 0.06) continue;

                        Slab(m, side, a0, a1, o0, o1, roll, rng);
                    }
                }
            }
        }

        /// <summary>True when the slab would be wholly under a slot's patch, and so never seen.</summary>
        static bool Covered(List<Rect> patches, float a0, float a1, float o0, float o1)
        {
            foreach (Rect r in patches)
                if (a0 >= r.xMin - 0.05f && a1 <= r.xMax + 0.05f && o0 >= r.yMin - 0.05f && o1 <= r.yMax + 0.05f) return true;

            return false;
        }

        static void Slab(B.Model m, L.Side side, float a0, float a1, float o0, float o1, double roll, System.Random rng)
        {
            Color colour = Paving[rng.Next(Paving.Length)] * (1f + 0.035f * Signed(rng));
            if (roll > 0.92) colour = Stained * (1f + 0.04f * Signed(rng));
            else if (roll > 0.86) colour = Bleached;

            // Cracked across, the two halves settled differently.
            if (roll > 0.06 && roll < 0.19 && a1 - a0 > 0.5f)
            {
                float cut = Mathf.Lerp(a0 + 0.25f, a1 - 0.25f, (float)rng.NextDouble());
                Piece(m, side, a0, cut - 0.012f, o0, o1, colour, rng);
                Piece(m, side, cut + 0.012f, a1, o0, o1, colour * 0.96f, rng);
                if (rng.NextDouble() < 0.6) Weeds(m, side, cut, Mathf.Lerp(o0, o1, (float)rng.NextDouble()), rng);
                return;
            }

            Piece(m, side, a0, a1, o0, o1, colour, rng);

            // Weeds in the odd joint.
            if (rng.NextDouble() < 0.12) Weeds(m, side, a1, Mathf.Lerp(o0, o1, (float)rng.NextDouble()), rng);
        }

        static void Piece(B.Model m, L.Side side, float a0, float a1, float o0, float o1, Color colour, System.Random rng)
        {
            float length = a1 - a0 - Joint, depth = o1 - o0 - Joint;
            float sink = 0.008f * Signed(rng);
            Vector3 centre = side.World((a0 + a1) * 0.5f, (o0 + o1) * 0.5f, SlabTop - SlabThick * 0.5f + sink);

            // Settled a little out of true: tipped a degree or so either way.
            Quaternion lie = Quaternion.LookRotation(side.Out) * Quaternion.Euler(1.1f * Signed(rng), 0f, 1.1f * Signed(rng));
            m.Box(colour, centre, new Vector3(length, SlabThick, depth), 0.012f, lie);
        }

        static void Weeds(B.Model m, L.Side side, float along, float outward, System.Random rng)
        {
            Vector3 root = side.World(along, outward, SlabTop - 0.02f);
            int blades = 4 + rng.Next(3);
            for (int i = 0; i < blades; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float h = 0.08f + 0.07f * (float)rng.NextDouble();
                var lean = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.04f + 0.03f * (float)rng.NextDouble());
                m.Tube((p, n) => Color.Lerp(Weed, WeedLight, S.Smooth((p.y - root.y) / h)), root, root + Vector3.up * h + lean, 0.012f, 0.002f, 4);
            }
        }

        // --- The kerb -------------------------------------------------------------------------------------

        /// <summary>Kerb stones along the outer edge, some tipped out, a few gone; none across an apron.</summary>
        static void Kerbs(B.Model m, L.Side side, List<Vector2> aprons, System.Random rng)
        {
            const float Stone = 0.9f, Height = 0.15f;
            float o = L.Depth - L.KerbWidth * 0.5f;

            var runs = new List<Vector2> { new Vector2(side.From, side.To) };
            foreach (Vector2 apron in aprons) runs = L.Cut(runs, apron.x, apron.y);

            foreach (Vector2 run in runs)
            {
                for (float a = run.x; a < run.y - 0.1f; a += Stone)
                {
                    float a1 = Mathf.Min(a + Stone, run.y);
                    if (rng.NextDouble() < 0.07) continue;

                    float tip = rng.NextDouble() < 0.18 ? 4f + 5f * (float)rng.NextDouble() : 1f * Signed(rng);
                    float sink = rng.NextDouble() < 0.15 ? -0.04f : 0.008f * Signed(rng);
                    Color c = Kerb * (1f + 0.04f * Signed(rng));

                    Quaternion lie = Quaternion.LookRotation(side.Out) * Quaternion.Euler(tip, 1.5f * Signed(rng), 0f);
                    Vector3 centre = side.World((a + a1) * 0.5f, o, 0.02f + Height * 0.5f + sink);
                    m.Box(c, centre, new Vector3(a1 - a - 0.025f, Height, L.KerbWidth), 0.02f, lie);
                }
            }
        }

        // --- An apron -------------------------------------------------------------------------------------

        /// <summary>Two tracks worn across the gravel in front of a gate, and a few loose stones.</summary>
        static void Apron(B.Model m, L.Side side, Vector2 apron, System.Random rng)
        {
            float middle = (apron.x + apron.y) * 0.5f;

            for (int k = -1; k <= 1; k += 2)
            {
                float a = middle + k * 0.45f;
                m.Surface((p, n) => Track * (1f + 0.05f * Mathf.Sin(side.OutOf(p) * 7f)), 2, 10, (u, v) =>
                {
                    float o = L.Inner + 0.2f + v * (L.Depth - L.Inner + 0.4f);
                    float wobble = 0.06f * Mathf.Sin(o * 2.1f + k);
                    return side.World(a + wobble + (u - 0.5f) * 0.32f, o, BedTop + 0.006f);
                });
            }

            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(apron.x, apron.y, (float)rng.NextDouble());
                float o = Mathf.Lerp(L.WallHalf + 0.3f, L.Depth, (float)rng.NextDouble());
                float r = 0.03f + 0.04f * (float)rng.NextDouble();
                S.Ball(m, (p, n) => GravelDark * 0.95f, side.World(a, o, BedTop + r * 0.3f), new Vector3(r, r * 0.6f, r * 0.85f),
                       Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), 7, 4);
            }
        }

        // --- The patches under slots ------------------------------------------------------------------------

        /// <summary>
        /// A steel road plate laid over the paving and bolted down round its edge: worn smooth and
        /// darker in the middle, rust at its corners, its outer edge painted in hazard stripes.
        /// </summary>
        static void SteelPlate(B.Model m, L.Side side, L.Slot slot, System.Random rng)
        {
            const float Thick = 0.028f;
            Quaternion face = Quaternion.LookRotation(side.Out);
            Vector3 centre = side.World(slot.Along, slot.Out, L.PadTop - Thick * 0.5f);

            var shape = new SmoothMesh(1);
            shape.RoundedBox(0, centre, new Vector3(Patch.x, Thick, Patch.y), 0.01f, face, 1);
            m.Add(shape, (p, n) =>
            {
                if (n.y < 0.7f) return PlateEdge;
                float a = side.AlongOf(p) - slot.Along, o = side.OutOf(p) - slot.Out;
                // Rust only right at the corners, where water stands.
                float corner = S.Smooth((Mathf.Abs(a) - Patch.x * 0.45f) / 0.06f) * S.Smooth((Mathf.Abs(o) - Patch.y * 0.4f) / 0.06f);
                return Color.Lerp(PlateSteel * (1f + 0.04f * Mathf.Sin(a * 4f + o * 7f)), Rust, corner * 0.45f);
            });

            // A weld seam across the middle, where two plates were joined.
            m.Box(PlateEdge, side.World(slot.Along + 0.05f, slot.Out, L.PadTop + 0.003f), new Vector3(0.025f, 0.008f, Patch.y - 0.1f), 0.003f, face);

            // Hazard stripes along the outer edge, in blocks: paint changes only at a vertex.
            const float Block = 0.2f;
            int count = Mathf.RoundToInt((Patch.x - 0.1f) / Block);
            float o1 = slot.Out + Patch.y * 0.5f - 0.1f;
            for (int i = 0; i < count; i++)
            {
                float a = slot.Along - (count * Block) * 0.5f + (i + 0.5f) * Block;
                Color c = (i % 2 == 0 ? Ochre : Charcoal) * (1f + 0.05f * Signed(rng));
                if (rng.NextDouble() < 0.15) continue;
                m.Box(c, side.World(a, o1, L.PadTop + 0.002f), new Vector3(Block - 0.01f, 0.006f, 0.1f), 0.002f, face);
            }

            Bolts(m, side, slot, 0.07f, rng);
        }

        /// <summary>A fresh concrete pad: cleaner and lighter than the old slabs, its anchor bolts and rebar ends showing.</summary>
        static void ConcretePad(B.Model m, L.Side side, L.Slot slot, System.Random rng)
        {
            const float Thick = 0.07f;
            Quaternion face = Quaternion.LookRotation(side.Out);
            Vector3 centre = side.World(slot.Along, slot.Out, L.PadTop - Thick * 0.5f);

            var shape = new SmoothMesh(1);
            shape.RoundedBox(0, centre, new Vector3(Patch.x, Thick, Patch.y), 0.02f, face, 1);
            m.Add(shape, (p, n) =>
            {
                float a = side.AlongOf(p), o = side.OutOf(p);
                float mottle = 0.03f * Mathf.Sin(a * 5.3f + o * 2.1f) * Mathf.Sin(o * 6.7f - a);
                return (n.y > 0.7f ? FreshConcrete : FreshEdge) * (1f + mottle);
            });

            // Rebar ends left sticking up at two corners, rusted.
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 at = side.World(slot.Along + k * (Patch.x * 0.5f - 0.08f), slot.Out - k * (Patch.y * 0.5f - 0.08f), L.PadTop);
                m.Tube(Rust, at, at + Vector3.up * 0.06f + side.Along * 0.01f * k, 0.011f, 6);
                m.Tube(Rust, at + side.Along * 0.04f, at + side.Along * 0.04f + Vector3.up * 0.045f, 0.011f, 6);
            }

            Bolts(m, side, slot, 0.12f, rng);
        }

        /// <summary>Anchor bolts round a patch, <paramref name="inset"/> in from its edge.</summary>
        static void Bolts(B.Model m, L.Side side, L.Slot slot, float inset, System.Random rng)
        {
            float ha = Patch.x * 0.5f - inset, ho = Patch.y * 0.5f - inset;
            float[] along = { -ha, -ha * 0.33f, ha * 0.33f, ha };

            foreach (float a in along)
                foreach (float o in new[] { -ho, ho })
                {
                    Vector3 at = side.World(slot.Along + a, slot.Out + o, L.PadTop);
                    m.Tube(Bolt * (1f + 0.04f * Signed(rng)), at, at + Vector3.up * 0.016f, 0.02f, 6);
                }
        }

        static float Signed(System.Random rng) => (float)rng.NextDouble() * 2f - 1f;
    }
}
