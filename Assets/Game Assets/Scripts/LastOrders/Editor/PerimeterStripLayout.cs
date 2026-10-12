using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Where the safe zone's outer defenses go, as numbers: the defense ring outside the walls, the
    /// strip of ground along their foot and its build slots, the pads further out where a gun can
    /// be set on the ground, the sites on the walls where any defense can be mounted, and what
    /// stands where from the start.
    ///
    /// The ring is open ground <see cref="Ring"/> metres deep all the way round the walls. On the
    /// west, north and east it lies between the safe zone and the land to claim: the plots and the
    /// courtyard start beyond it, and stay beyond it once claimed - each walled in on its own, with a
    /// gate facing the safe zone's. Nothing claimed ever takes the ring, the wall or what the player
    /// built in them. On the south it runs to the road's verge, and there is no land to claim beyond
    /// it: the drive crosses it from the road to the main gate.
    ///
    /// The strip runs round the outside of all four walls - the inner edge of the ring. In front of
    /// every gate the strip is left open, a gravel apron workers, customers and cars cross, with no
    /// slot near it.
    ///
    /// The slots are laid along each run of wall between the gates, evenly spaced, each set a
    /// little nearer the wall or the plot than its neighbour, so what is built there makes a
    /// staggered line zombies have to weave through - not lanes. Where the night's own route
    /// crosses a run (<see cref="Approaches"/>) the row is slid along so a slot sits squarely on it
    /// rather than leaving the gap between two there.
    ///
    /// The wall sites stand ten to fifteen metres apart all the way round, south wall included. A
    /// defense built on one thickens that stretch of wall into a bastion, which reaches out over the
    /// strip, so the slots leave room for it. Every slot built from the start is within reach of a
    /// defense mounted from the start.
    ///
    /// Measured against Main Level_Apocalypse's compound walls (centrelines x -9.46 and 45.95, z
    /// -2.62 and 35.2, 0.5 m thick, 2.1 m high). The gates are read from the scene.
    /// </summary>
    public static class PerimeterStripLayout
    {
        // --- The compound ---------------------------------------------------------------------------

        public const float WestLine = -9.46f, EastLine = 45.95f, NorthLine = 35.2f, SouthLine = -2.62f;

        /// <summary>Half the wall's thickness: its outer face is this far out from its centreline.</summary>
        public const float WallHalf = 0.25f;

        // --- The ring -------------------------------------------------------------------------------

        /// <summary>
        /// Metres from the wall's centreline to the land to claim - or, on the south, to the road's
        /// verge: the strip, then the ring pads with room to walk round them.
        /// </summary>
        public const float Ring = 8f;

        /// <summary>The walls and the ring round them: ground nothing but the defenses stands on.</summary>
        public static Rect RingBounds => Rect.MinMaxRect(WestLine - Ring, SouthLine - Ring, EastLine + Ring, NorthLine + Ring);

        // --- The strip ------------------------------------------------------------------------------

        /// <summary>Metres from the wall's centreline to the strip's outer edge: 2.6 m of ground beyond the wall's face.</summary>
        public const float Depth = 2.85f;

        /// <summary>Where the paving starts: tucked under the wall, so no seam shows at its foot.</summary>
        public const float Inner = 0.1f;

        /// <summary>The kerb along the outer edge, between the strip and the land beyond.</summary>
        public const float KerbWidth = 0.16f;

        /// <summary>The top of a slot's reinforced patch: what a ground defense stands on.</summary>
        public const float PadTop = 0.1f;

        // --- Slots ------------------------------------------------------------------------------------

        public const float SlotLength = 2.4f, SlotDepth = 1.5f, SlotGap = 1f;

        /// <summary>A slot's middle, out from the wall's centreline, and how far either way neighbours are staggered.</summary>
        public const float SlotOut = 1.55f, SlotStagger = 0.25f;

        /// <summary>Metres kept clear of slots either side of a gate: past the gravel apron, with room to walk round it.</summary>
        public const float GateClear = 0.8f;

        /// <summary>Metres the gravel apron in front of a gate runs past each of its posts.</summary>
        public const float ApronMargin = 0.6f;

        /// <summary>Metres plot obstacles are kept back beyond the strip's outer edge.</summary>
        public const float DebrisClear = 0.4f;

        // --- Ring pads ----------------------------------------------------------------------------------

        /// <summary>A ring pad's middle, out from the wall's centreline: past the strip, short of the plots.</summary>
        public const float RingPadOut = 5.4f;

        /// <summary>A ring pad's side: the bastion's top, room for any of the guns a wall site takes.</summary>
        public const float RingPadSize = 2.8f;

        /// <summary>The top of a ring pad's slab, what the gun stands on.</summary>
        public const float RingPadTop = 0.08f;

        // --- The sides ---------------------------------------------------------------------------------

        /// <summary>
        /// One run of wall and the strip in front of it, in its own frame: along the wall, and out from
        /// it. Along is the slot's +X and out its +Z, chosen round each wall so up is always
        /// cross(out, along) - the ground faces up whichever side it is.
        /// </summary>
        public readonly struct Side
        {
            public readonly string Name;
            public readonly char Key;

            /// <summary>A point on the wall's centreline, where along is zero.</summary>
            public readonly Vector3 Origin;

            public readonly Vector3 Along;
            public readonly Vector3 Out;

            /// <summary>The run the strip covers, in metres along.</summary>
            public readonly float From, To;

            public readonly bool HasStrip;

            public Side(string name, char key, Vector3 origin, Vector3 along, Vector3 outward, float from, float to, bool hasStrip)
            {
                Name = name;
                Key = key;
                Origin = origin;
                Along = along;
                Out = outward;
                From = from;
                To = to;
                HasStrip = hasStrip;
            }

            public Vector3 World(float along, float outward, float y = 0f) => Origin + Along * along + Out * outward + Vector3.up * y;

            /// <summary>How far along this side a world point is.</summary>
            public float AlongOf(Vector3 world) => Vector3.Dot(world - Origin, Along);

            /// <summary>How far out from the wall's centreline a world point is.</summary>
            public float OutOf(Vector3 world) => Vector3.Dot(world - Origin, Out);

            /// <summary>Degrees about Y that turn +Z to face out from this wall.</summary>
            public float Yaw => Quaternion.LookRotation(Out).eulerAngles.y;
        }

        /// <summary>
        /// The west and east strips stop where the north and south ones begin (just past those walls'
        /// faces), and the north and south ones run on over the corners, so the four meet without
        /// overlapping.
        /// </summary>
        public static readonly Side West = new Side("West", 'w', new Vector3(WestLine, 0f, 0f), Vector3.forward, Vector3.left,
                                                    SouthLine - Inner, NorthLine + Inner, true);

        public static readonly Side North = new Side("North", 'n', new Vector3(0f, 0f, NorthLine), Vector3.right, Vector3.forward,
                                                     WestLine - Depth, EastLine + Depth, true);

        public static readonly Side East = new Side("East", 'e', new Vector3(EastLine, 0f, 0f), Vector3.back, Vector3.right,
                                                    -(NorthLine + Inner), -(SouthLine - Inner), true);

        /// <summary>The road side: along runs west, so it is the north strip turned round.</summary>
        public static readonly Side South = new Side("South", 's', new Vector3(0f, 0f, SouthLine), Vector3.left, Vector3.back,
                                                     -(EastLine + Depth), -(WestLine - Depth), true);

        public static readonly Side[] Strips = { West, North, East, South };

        /// <summary>
        /// Where the night's zombies actually cross each strip, in metres along it. They walk the
        /// Doors flow field, which knows nothing of the compound wall, so from wherever they come in
        /// they gather onto a few lines and hit the wall there: level with the diner door on the west
        /// and east (z 14.1), and either side of the diner on the north - down its west side (x 8.6)
        /// and onto the yard gate (x 30.9, an apron, so the guns hold that one). Traced from the
        /// field in play on 2026-10-05; re-trace them if the diner moves or grows a lot. On the south
        /// they come straight up their trail, across the road, onto the main gate - an apron, held by
        /// the guns either side of it.
        /// </summary>
        public static float[] Approaches(Side side)
        {
            switch (side.Key)
            {
                case 'w': return new[] { 14.1f };
                case 'n': return new[] { 8.6f, 30.9f };
                case 'e': return new[] { -14.1f };
                default: return new float[0];
            }
        }

        // --- Wall sites --------------------------------------------------------------------------------

        /// <summary>
        /// A bastion's footprint in its wall site's frame: <see cref="Length"/> along the wall, from
        /// <see cref="Inner"/> metres inside the wall's centreline to <see cref="Outer"/> outside it.
        /// </summary>
        public readonly struct Bastion
        {
            public readonly float Length, Inner, Outer;

            public Bastion(float length, float inner, float outer)
            {
                Length = length;
                Inner = inner;
                Outer = outer;
            }

            public float Thickness => Inner + Outer;

            /// <summary>Out from the wall's centreline to the middle of its footprint.</summary>
            public float Middle => (Outer - Inner) * 0.5f;
        }

        /// <summary>
        /// Over the strip: 1.15 m out beyond the wall's face and 0.65 m in, about 2.8 x 2.3 m on top -
        /// room for the scrap turret's pad, the Toastapult's pallet or the corn shooter's.
        /// </summary>
        public static readonly Bastion StripBastion = new Bastion(2.8f, 0.9f, 1.4f);

        /// <summary>
        /// On a wall with no strip in front of it - its foot at a kerb - so it reaches into the
        /// compound instead. The south wall's were, while the road ran along its foot.
        /// </summary>
        public static readonly Bastion RoadBastion = new Bastion(2.8f, 1.65f, 0.65f);

        /// <summary>Twice an ordinary section's: it was built to carry a gun.</summary>
        public const float BastionHealth = 700f;

        /// <summary>Metres kept clear of strip slots past each end of a bastion.</summary>
        public const float BastionClear = 0.25f;

        public readonly struct WallMount
        {
            public readonly string Id;
            public readonly Side Side;
            public readonly float Along;
            public readonly bool StartsBuilt;

            public WallMount(string id, Side side, float along, bool startsBuilt)
            {
                Id = id;
                Side = side;
                Along = along;
                StartsBuilt = startsBuilt;
            }

            /// <summary>On the wall's centreline: the site's own frame, +Z out of the compound.</summary>
            public Vector3 Position => Side.World(Along, 0f);

            public Bastion Bastion => Side.HasStrip ? StripBastion : RoadBastion;
        }

        /// <summary>
        /// Every place on the wall a defense can be mounted: none on a gate or its pillars, each bastion
        /// clear of what stands inside the wall behind it (the towers, the tent, the shelters, the car
        /// park). Six carry a corn shooter from the start - one on each side where the night comes in,
        /// two either side of the main gate - and four are empty, for whatever the player picks.
        /// </summary>
        public static readonly WallMount[] Mounts =
        {
            // Between the south-west tower and the hospital tent; and level with the shelters.
            new WallMount("wall_west_south", West, 2.5f, false),
            new WallMount("wall_west_north", West, 19f, true),

            // Past the north-west gate's pillar; over the storehouse; either side of the field gate; short of the north-east tower.
            // The yard's clear of x 21.5, where the north field's and the courtyard's walls come across the ring once claimed.
            new WallMount("wall_north_west", North, 3.5f, false),
            new WallMount("wall_north_mid", North, 13f, true),
            new WallMount("wall_north_yard", North, 23.6f, false),
            new WallMount("wall_north_east", North, 38.5f, true),

            // East: along runs south, so these are at z 2.6 and z 19.
            new WallMount("wall_east_south", East, -2.6f, false),
            new WallMount("wall_east_north", East, -19f, true),

            // Both sides of the main gate, facing the road; the east one past the end of the car park.
            new WallMount("wall_south_west", South, -4.8f, true),
            new WallMount("wall_south_east", South, -39f, true),
        };

        // --- Ring pads ----------------------------------------------------------------------------------

        public readonly struct RingPad
        {
            public readonly string Id;
            public readonly Side Side;
            public readonly float Along;

            public RingPad(string id, Side side, float along)
            {
                Id = id;
                Side = side;
                Along = along;
            }

            /// <summary>On the slab's top: the site's own frame, +Z out from the wall.</summary>
            public Vector3 Position => Side.World(Along, RingPadOut);
        }

        /// <summary>
        /// Out in the ring, where a gun the wall sites take can be set on the ground instead: one
        /// beside each line the night comes in by (z 14.1 west and east, x 8.6 north), just off the
        /// worn trail it walks in on, the rest between the gates. Each keeps 2.5 m clear of the way
        /// from a gate out to the plot facing it, so workers walk straight across - and on the south,
        /// of the main gate and the drive, flanking the trail that comes up to them. All empty: the
        /// player picks what goes on them.
        /// </summary>
        public static readonly RingPad[] RingPads =
        {
            // Gates at z 11.5 and 30.5; the trail at z 18.
            new RingPad("ring_west_south", West, 5f),
            new RingPad("ring_west_mid", West, 16f),
            new RingPad("ring_west_north", West, 24f),

            // Gates at x -1.5, 18.5 and the yard's at 30.5; the trail at x 7, the north-west lane along the ring from x -12.
            new RingPad("ring_north_west", North, -6f),
            new RingPad("ring_north_mid", North, 13.5f),
            new RingPad("ring_north_field", North, 24.5f),
            new RingPad("ring_north_yard", North, 38f),

            // East: along runs south, so these are at z 3, 12 and 22; gates at z 8 and 27.5, the trail at z 16.
            new RingPad("ring_east_south", East, -3f),
            new RingPad("ring_east_mid", East, -12f),
            new RingPad("ring_east_north", East, -22f),

            // South: along runs west, so these are at x -3, 10.5, 29.5 and 40.5; the main gate and the
            // drive at x 15-25, the night's trail up the middle of them.
            new RingPad("ring_south_west", South, 3f),
            new RingPad("ring_south_gate_west", South, -10.5f),
            new RingPad("ring_south_gate_east", South, -29.5f),
            new RingPad("ring_south_east", South, -40.5f),
        };

        // --- What the strip starts with -----------------------------------------------------------------

        public enum Start { Barricade, SpikePlate, RazorWire }

        public readonly struct Starter
        {
            public readonly Side Side;
            public readonly float Near;
            public readonly Start Kind;

            public Starter(Side side, float near, Start kind)
            {
                Side = side;
                Near = near;
                Kind = kind;
            }
        }

        /// <summary>
        /// The slots built from the start, each the slot nearest <see cref="Starter.Near"/> along its
        /// side: a mix of all three on every strip, in front of the guns mounted from the start and
        /// where the night comes in, so each of them delays something the guns can reach.
        /// </summary>
        public static readonly Starter[] Starters =
        {
            // A barricade across each approach, wire and spikes along from it, all in reach of the
            // corn shooters at z 19 (west and east), x 13 and x 38.5 (north).
            new Starter(West, 15f, Start.Barricade),
            new Starter(West, 22.7f, Start.RazorWire),
            new Starter(West, 26.1f, Start.SpikePlate),

            new Starter(North, 6.35f, Start.RazorWire),
            new Starter(North, 9.75f, Start.Barricade),
            new Starter(North, 34.8f, Start.SpikePlate),
            new Starter(North, 42.6f, Start.Barricade),

            new Starter(East, -14.9f, Start.Barricade),
            new Starter(East, -11.5f, Start.RazorWire),
            new Starter(East, -22.9f, Start.SpikePlate),

            // Either side of the main gate's apron, in reach of the corn shooters at x 4.8 and x 39.
            new Starter(South, -11f, Start.Barricade),
            new Starter(South, -7f, Start.SpikePlate),
            new Starter(South, -32f, Start.RazorWire),
        };

        // --- Slots -------------------------------------------------------------------------------------

        public readonly struct Slot
        {
            public readonly string Id;
            public readonly Side Side;
            public readonly float Along;
            public readonly float Out;

            /// <summary>Its patch: a steel plate, or a fresh concrete pad.</summary>
            public readonly bool Plate;

            public Slot(string id, Side side, float along, float outward, bool plate)
            {
                Id = id;
                Side = side;
                Along = along;
                Out = outward;
                Plate = plate;
            }

            public Vector3 Position => Side.World(Along, Out, PadTop);
        }

        /// <summary>
        /// The slots along <paramref name="side"/>: as many as fit, evenly spaced, in each run of it
        /// between <paramref name="gates"/> (spans along it, in metres) and the wall sites' bastions -
        /// slid within the run's spare
        /// room so a slot is as near centred on any of its <see cref="Approaches"/> as it can be, and
        /// centred in the run otherwise. Staggered nearer the wall and further out by turns, the
        /// patches alternating plate and concrete.
        /// </summary>
        public static List<Slot> Slots(Side side, IReadOnlyList<Vector2> gates)
        {
            float[] approaches = Approaches(side);
            var slots = new List<Slot>();
            var runs = new List<Vector2> { new Vector2(side.From + 0.3f, side.To - 0.3f) };

            foreach (Vector2 gate in gates) runs = Cut(runs, gate.x - GateClear, gate.y + GateClear);

            foreach (WallMount mount in Mounts)
            {
                if (mount.Side.Key != side.Key) continue;
                float half = mount.Bastion.Length * 0.5f + BastionClear;
                runs = Cut(runs, mount.Along - half, mount.Along + half);
            }

            int n = 0;
            foreach (Vector2 run in runs)
            {
                float length = run.y - run.x;
                int count = Mathf.FloorToInt((length + SlotGap) / (SlotLength + SlotGap));
                if (count <= 0) continue;

                float used = count * SlotLength + (count - 1) * SlotGap;
                float spare = length - used;
                float start = run.x + Shift(run, count, spare, approaches) + SlotLength * 0.5f;

                for (int i = 0; i < count; i++)
                {
                    float along = start + i * (SlotLength + SlotGap);
                    float outward = SlotOut + (n % 2 == 0 ? -SlotStagger : SlotStagger);
                    slots.Add(new Slot($"strip_{side.Key}_{n + 1:00}", side, along, outward, n % 2 == 0));
                    n++;
                }
            }

            return slots;
        }

        /// <summary>
        /// How far into its spare room a run's row of slots starts: half of it, centring the row, unless
        /// an approach crosses the run - then whatever puts a slot's middle nearest that approach.
        /// </summary>
        static float Shift(Vector2 run, int count, float spare, float[] approaches)
        {
            float shift = spare * 0.5f;
            float best = float.MaxValue;

            foreach (float approach in approaches)
            {
                if (approach < run.x || approach > run.y) continue;

                for (int k = 0; k < count; k++)
                {
                    float first = run.x + SlotLength * 0.5f + k * (SlotLength + SlotGap);
                    float s = Mathf.Clamp(approach - first, 0f, spare);
                    float miss = Mathf.Abs(first + s - approach);
                    if (miss >= best) continue;

                    best = miss;
                    shift = s;
                }
            }

            return shift;
        }

        /// <summary>The runs left once [<paramref name="from"/>, <paramref name="to"/>] is taken out of them.</summary>
        public static List<Vector2> Cut(List<Vector2> runs, float from, float to)
        {
            var left = new List<Vector2>();
            foreach (Vector2 run in runs)
            {
                if (to <= run.x || from >= run.y)
                {
                    left.Add(run);
                    continue;
                }

                if (from > run.x) left.Add(new Vector2(run.x, from));
                if (to < run.y) left.Add(new Vector2(to, run.y));
            }

            return left;
        }
    }
}
