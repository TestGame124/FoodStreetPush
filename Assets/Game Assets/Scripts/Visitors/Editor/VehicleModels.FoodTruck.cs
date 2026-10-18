using System;
using System.Collections.Generic;
using LastOrders.EditorTools;
using UnityEditor;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using K = FoodStreet.Visitors.EditorTools.VehicleKit;
using S = LastOrders.EditorTools.SoupKitchenModel;
using Object = UnityEngine.Object;

namespace FoodStreet.Visitors.EditorTools
{
    /// <summary>
    /// The stranded food truck from the roadside lot: a tall rounded step van in diner teal and
    /// cream, its serving hatch down the kerb side with a striped awning hinged over it, a counter
    /// under the hatch, a chimney and a gas rack, a ladder up the back, and a giant hot dog on a
    /// sign board along the roof. Built the way the visitors' van is - lofted bodywork with the
    /// glass, doors and stripes laid onto it - so it drives with the same springs and wheels.
    ///
    /// Three extra parts ride on the sprung body: the awning on its hinge, which the depot props
    /// open on the pad and shuts for the road; how it was found - rust, a tarp over the sign, the
    /// hatch boarded up; and how it looks back at work - bulbs, an OPEN sign, sauces on the counter.
    /// </summary>
    public static partial class VehicleModels
    {
        internal const string FoodTruckId = "FoodTruck";

        static readonly Color TruckTeal = B.Hex("4E7D79");
        static readonly Color TruckCream = B.Hex("B6AD97");
        static readonly Color TruckRed = B.Hex("8E3B33");
        static readonly Color TruckMustard = B.Hex("B08A3E");
        static readonly Color Canvas = B.Hex("B9AF98");
        static readonly Color Steel = B.Hex("8F969B");
        static readonly Color Bun = B.Hex("B98A4E");
        static readonly Color BunTop = B.Hex("9C6C38");
        static readonly Color Sausage = B.Hex("9A4632");
        static readonly Color Mustard = B.Hex("D3A93A");
        static readonly Color Tarp = B.Hex("5C6E5A");
        static readonly Color Plank = B.Hex("8F7660");

        /// <summary>The truck as built, with the parts that move or come and go besides the rig's own.</summary>
        internal sealed class FoodTruckParts
        {
            public Rig Rig;

            /// <summary>The awning, modelled about its hinge: X along the hinge, hanging down -Y, out of the truck -Z.</summary>
            public B.Model Awning;

            /// <summary>The hinge, in vehicle space, and its turn: local X runs along the truck.</summary>
            public Vector3 Hinge;

            public Quaternion HingeTurn;

            public B.Model Derelict;
            public B.Model Restored;
        }

        // Measurements, vehicle space: +Z forward, +X the kerb side with the hatch.
        const float TruckR = 0.44f, TruckTrack = 0.86f, TruckZF = 1.52f, TruckZR = -1.58f, TruckBelt = 1.04f, TruckRoof = 2.5f;
        const float HatchZ0 = -1.75f, HatchZ1 = 0.12f, HatchY0 = 1.22f, HatchY1 = 2.0f;

        internal static FoodTruckParts FoodTruck()
        {
            const float ArchR = 0.5f, Clear = 0.32f, G0 = 1.2f, G1 = 1.8f;

            var rig = new Rig(TruckR);
            B.Model m = rig.Body;
            Func<Vector3, Vector3, Color> tubPaint = K.Worn(TruckTeal, 71, 0.8f);
            Func<Vector3, Vector3, Color> boxPaint = K.Worn(TruckCream, 72, 0.5f);

            var tub = new Loft
            {
                Back = -2.48f, Front = 2.32f, CapBack = 0.22f, CapFront = 0.4f, Round = 3.4f, CapRound = 2.6f,
                Section = z => new Vector3(Arch(z, Clear, TruckR, ArchR, TruckZF, TruckZR), TruckBelt - 0.14f * K.Smooth((z - 1.25f) / 0.75f),
                                           1.0f + 0.03f * Fenders(z, 0.68f, TruckZF, TruckZR)),
                Tuck = y => 1f - 0.08f * Sq(Mathf.Max(0f, y)) - 0.06f * Sq(Mathf.Max(0f, -y)),
            };
            tub.Build(m, tubPaint, 56, 76);

            // The kitchen box: taller than the van's and nearly square, the cab glass raked back at the front.
            var box = new Loft
            {
                Back = -2.45f, Front = 1.58f, CapBack = 0.16f, CapFront = 0.5f, Round = 4.6f, CapRound = 2.8f,
                Section = z => new Vector3(0.8f, TruckRoof - 0.1f * K.Smooth((z - 0.5f) / 0.7f), 0.98f),
                Tuck = y => 1f - 0.06f * Sq(Mathf.Max(0f, y)),
            };
            box.Warp = Rake(TruckBelt, TruckRoof, 0.4f, box.Front, box.Back, 0.32f, 0f);
            box.Build(m, boxPaint, 60, 72);

            // Cab glass, and the cab doors either side.
            Pane(box, m, box.EndArea(true, G0 - 0.02f, 1.95f, 0.86f));
            for (int s = -1; s <= 1; s += 2)
            {
                const float Z0 = 0.4f, Z1 = 1.12f;
                FixedDoor(tub, m, s, Z0, Z1, 0.46f, 0.98f, tubPaint, handleAtFront: false);
                box.Patch(m, boxPaint, box.SideArea(s, Z0, Z1, 1.06f, 1.96f), 0.008f, 18, 10, 9f, 0.02f, K.Flat(K.Trim));
                DoorGlass(box, m, box.SideArea(s, Z0 + 0.08f, Z1 - 0.1f, G0, G1), 0.014f);
                K.Mirror(m, OnSide(box, 1.12f, 1.34f, s), s, TruckCream);
            }

            // Livery: a red band at the belt line and a mustard pinstripe over it, both sides.
            for (int s = -1; s <= 1; s += 2)
            {
                Decal(box, m, box.SideArea(s, -2.22f, 0.32f, 1.06f, 1.16f), TruckRed, 0.004f, 0f, 44, 2);
                Decal(box, m, box.SideArea(s, -2.22f, 0.32f, 1.19f, 1.215f), TruckMustard, 0.005f, 0f, 44, 2);
            }

            // The far side: a round sign with a raised hot dog on it, so neither side is bare.
            {
                const float Z = -0.85f, Y = 1.62f;
                Decal(box, m, box.SideArea(-1f, Z - 0.42f, Z + 0.42f, Y - 0.36f, Y + 0.36f), TruckRed, 0.005f, 2f, 20, 20);
                Decal(box, m, box.SideArea(-1f, Z - 0.37f, Z + 0.37f, Y - 0.31f, Y + 0.31f), K.Cream, 0.009f, 2f, 20, 20);
                Vector3 at = OnSide(box, Z, Y, -1f, 0.04f);
                HotDog(m, at, Quaternion.LookRotation(Vector3.forward, Vector3.right * -1f) * Quaternion.Euler(0f, 0f, 0f), 0.32f, sideways: true);
            }

            Hatch(rig, box, m, boxPaint);

            // The back: twin doors with a seam, chrome handles, a ladder up the kerb-side corner.
            Decal(box, m, box.EndArea(false, 1.0f, 2.2f, 0f, 0f, 0.03f), K.Trim, 0.005f, 0f, 2, 12);
            for (int s = -1; s <= 1; s += 2)
                m.Box(K.Chrome, OnEnd(box, s * 0.08f, 1.4f, false, 0.025f), new Vector3(0.04f, 0.14f, 0.035f), 0.014f);
            Ladder(m, box);

            // Face, bumpers and lamps.
            for (int s = -1; s <= 1; s += 2)
            {
                EndLamp(m, tub, true, s * 0.63f, 0.78f, 0.15f, K.Lens, K.Chrome);
                EndLamp(m, tub, true, s * 0.86f, 0.66f, 0.045f, K.Indicator, K.Chrome);
            }

            Grille(m, tub, 0.58f, 0.7f, 0.32f, 0.035f, 3);
            Bumper(m, tub, true, 0.58f, 0.085f, K.BumperGrey);
            Bumper(m, tub, false, 0.6f, 0.085f, K.BumperGrey);
            TailLamps(m, tub, 0.8f, 0.8f, 0.98f, 0.14f);
            K.NumberPlate(m, OnEnd(tub, 0f, 0.76f, false, 0.01f), Vector3.back, 0.38f);

            Roof(m, box);
            GasRack(m, box);

            K.Tailpipe(m, new Vector3(-0.6f, 0.34f, -2.3f));
            rig.Exhaust = new Vector3(-0.6f, 0.34f, -2.34f);
            rig.FourWheels(TruckTrack, TruckZF, TruckZR, TruckR, 0.32f, K.Cream, K.Chrome, false);
            rig.IdleWhileParked = false;

            var parts = new FoodTruckParts { Rig = rig };
            parts.Awning = Awning(box, out parts.Hinge, out parts.HingeTurn);
            parts.Derelict = Derelict(tub, box);
            parts.Restored = Restored(box);
            return parts;
        }

        // --- The serving hatch ------------------------------------------------------------------------

        /// <summary>
        /// The hatch down the kerb side: the dark of the kitchen behind it, a chrome frame round it
        /// with a mullion, a steel counter on brackets under it, and a menu board hung beside it.
        /// </summary>
        static void Hatch(Rig rig, Loft box, B.Model m, Func<Vector3, Vector3, Color> paint)
        {
            Opening(box, m, box.SideArea(1f, HatchZ0, HatchZ1, HatchY0, HatchY1));

            // The kitchen inside, as far as the dark shows it: a hood over the back wall and the
            // glow-less grill top, laid just proud of the opening.
            Vector3 hood = OnSide(box, (HatchZ0 + HatchZ1) * 0.5f, HatchY1 - 0.12f, 1f, 0.02f);
            m.Box(Steel * 0.8f, hood, new Vector3(0.05f, 0.16f, HatchZ1 - HatchZ0 - 0.4f), 0.02f, 2);

            // The frame: chrome all round, and a mullion splitting the hatch in two.
            var frame = new List<Vector3>();
            float zc = (HatchZ0 + HatchZ1) * 0.5f, yc = (HatchY0 + HatchY1) * 0.5f;
            float hz = (HatchZ1 - HatchZ0) * 0.5f + 0.03f, hy = (HatchY1 - HatchY0) * 0.5f + 0.03f;
            for (int k = 0; k < 20; k++)
            {
                float a = k * Mathf.PI * 2f / 20f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float z = zc + Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 0.25f) * hz;
                float y = yc + Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.25f) * hy;
                frame.Add(OnSide(box, z, y, 1f, 0.02f));
            }
            K.Sweep(m, Shiny(K.Chrome), frame, 0.028f, 8, 3, closed: true);
            K.Sweep(m, Shiny(K.Chrome), new[] { OnSide(box, zc, HatchY0, 1f, 0.02f), OnSide(box, zc, HatchY1, 1f, 0.02f) }, 0.02f, 8, 3);

            // The counter: a steel shelf out over the kerb on two brackets, its front edge rolled.
            Vector3 sill = OnSide(box, zc, HatchY0 - 0.02f, 1f, 0f);
            float length = HatchZ1 - HatchZ0 + 0.14f;
            m.Box(Steel, new Vector3(sill.x + 0.15f, sill.y, zc), new Vector3(0.34f, 0.045f, length), 0.016f, 2);
            K.Sweep(m, Shiny(Steel), new[] { new Vector3(sill.x + 0.32f, sill.y + 0.01f, HatchZ0 - 0.06f), new Vector3(sill.x + 0.32f, sill.y + 0.01f, HatchZ1 + 0.06f) },
                    0.022f, 8, 3);
            foreach (float z in new[] { HatchZ0 + 0.2f, HatchZ1 - 0.2f })
            {
                m.Box(K.Trim, new Vector3(sill.x + 0.1f, sill.y - 0.12f, z), new Vector3(0.2f, 0.04f, 0.04f), 0.01f,
                      Quaternion.Euler(0f, 0f, -38f), 2);
            }

            // A menu board hung on the body ahead of the hatch: a framed slate with three raised lines.
            Vector3 board = OnSide(box, HatchZ1 + 0.36f, 1.56f, 1f, 0.035f);
            Quaternion face = Quaternion.LookRotation(Vector3.right, Vector3.up);
            m.Box(B.Walnut, board, new Vector3(0.48f, 0.66f, 0.05f), 0.02f, face, 2);
            m.Box(B.Hex("2E3532"), board + Vector3.right * 0.02f, new Vector3(0.4f, 0.57f, 0.03f), 0.01f, face, 2);
            for (int k = 0; k < 3; k++)
                m.Box(K.Cream, board + new Vector3(0.04f, 0.17f - k * 0.15f, 0f), new Vector3(0.008f, 0.035f, 0.26f - k * 0.04f), 0.004f);

            // A step to the cab door, under the hatch's front end.
            m.Box(K.Trim, new Vector3(0.98f, 0.36f, 0.75f), new Vector3(0.18f, 0.05f, 0.5f), 0.015f, 2);
        }

        /// <summary>
        /// The striped awning over the hatch, about its hinge: alternate red and cream panels with a
        /// scalloped valance along the free edge, a hem bar and two struts that prop it when it is up.
        /// Thick canvas on a frame, so it reads as a lid, not a sheet.
        /// </summary>
        static B.Model Awning(Loft box, out Vector3 hinge, out Quaternion turn)
        {
            float zc = (HatchZ0 + HatchZ1) * 0.5f;
            Vector3 top = OnSide(box, zc, HatchY1 + 0.08f, 1f, 0.03f);
            hinge = top;
            turn = Quaternion.Euler(0f, -90f, 0f);

            var m = new B.Model { Shade = (p, n) => n.y < -0.4f ? 0.82f : 1f };
            float width = HatchZ1 - HatchZ0 + 0.2f;
            const float drop = 0.86f, thick = 0.045f;
            const int stripes = 8;
            float stripe = width / stripes;

            // Local space: X along the hinge, the flap hanging down -Y, the outside facing -Z.
            for (int i = 0; i < stripes; i++)
            {
                float x = -width * 0.5f + stripe * (i + 0.5f);
                Color c = (i % 2 == 0 ? TruckRed : Canvas) * (1f + 0.03f * B.Jitter(i + 7));
                m.Box(c, new Vector3(x, -drop * 0.5f, -thick * 0.5f), new Vector3(stripe + 0.004f, drop, thick), 0.012f, 2);

                // A scallop at the hem, one per stripe, the colour of the stripe above it.
                m.Box(c, new Vector3(x, -drop - 0.05f, -thick * 0.5f), new Vector3(stripe * 0.82f, 0.12f, thick * 0.8f), 0.05f, 3);
            }

            // The frame: a rail along the hinge, a hem bar, and the two struts lying folded along it.
            K.Sweep(m, Shiny(K.Chrome), new[] { new Vector3(-width * 0.5f, 0f, -0.01f), new Vector3(width * 0.5f, 0f, -0.01f) }, 0.025f, 8, 3);
            K.Sweep(m, Shiny(K.Chrome), new[] { new Vector3(-width * 0.5f, -drop, -thick), new Vector3(width * 0.5f, -drop, -thick) }, 0.018f, 8, 3);
            foreach (float x in new[] { -width * 0.5f + 0.06f, width * 0.5f - 0.06f })
                K.Sweep(m, Shiny(K.Chrome), new[] { new Vector3(x, -0.04f, 0.01f), new Vector3(x, -drop + 0.04f, 0.01f) }, 0.014f, 6, 2);

            return m;
        }

        // --- The roof -----------------------------------------------------------------------------------

        /// <summary>
        /// Along the roof: a sign board on two stout posts with a giant hot dog lying on top of it -
        /// the bun two fat halves, the sausage poking out of both ends, mustard zigzagged along it -
        /// a chimney stack with a rain cap at the back, and a vent hood.
        /// </summary>
        static void Roof(B.Model m, Loft box)
        {
            float roof = box.Warp(box.Top(0f, -0.8f)).y;

            // The sign board, along the truck, seen from either side.
            const float Z = -0.75f, Length = 2.1f, Height = 0.5f;
            float y = roof + 0.28f + Height * 0.5f;
            foreach (float z in new[] { Z - 0.7f, Z + 0.7f })
            {
                m.Tube(K.Trim, new Vector3(0f, roof - 0.02f, z), new Vector3(0f, y, z), 0.045f, 10);
                m.Box(K.Trim, new Vector3(0f, roof + 0.02f, z), new Vector3(0.22f, 0.05f, 0.22f), 0.015f, 2);
            }

            m.Box(TruckRed, new Vector3(0f, y, Z), new Vector3(0.14f, Height, Length), 0.04f, 3);
            for (int s = -1; s <= 1; s += 2)
            {
                // A cream panel inset in the red frame, each side, with three raised dots of bulb sockets.
                m.Box(K.Cream, new Vector3(s * 0.06f, y, Z), new Vector3(0.04f, Height - 0.12f, Length - 0.14f), 0.015f, 2);
                for (int k = -3; k <= 3; k++)
                    m.Lathe((p, n) => K.Trim, new Vector3(s * 0.075f, y + Height * 0.5f - 0.03f, Z + k * 0.28f),
                            new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.03f, 0f), new Vector2(0.03f, 0.02f), new Vector2(0f, 0.025f) }, 8);
            }

            HotDog(m, new Vector3(0f, y + Height * 0.5f + 0.02f, Z), Quaternion.identity, 1f, sideways: false);

            // Lettering on both faces: a row of raised blocks of different heights, a word at a distance.
            for (int s = -1; s <= 1; s += 2)
            {
                float[] heights = { 0.2f, 0.16f, 0.2f, 0.12f, 0.2f, 0.18f, 0.2f, 0.14f };
                for (int k = 0; k < heights.Length; k++)
                {
                    float z = Z - 0.74f + k * 0.21f;
                    m.Box(TruckRed * 0.9f, new Vector3(s * 0.088f, y - 0.02f + (heights[k] - 0.2f) * 0.5f, z), new Vector3(0.02f, heights[k], 0.13f), 0.008f, 2);
                }
            }

            // The chimney: a stack with a ring of rivets and a conical rain cap on legs.
            var foot = new Vector3(-0.55f, roof - 0.03f, -2.0f);
            m.Lathe((p, n) => Steel * (p.y - foot.y > 0.5f ? 0.85f : 1f), foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.16f, 0f), new Vector2(0.16f, 0.06f), new Vector2(0.09f, 0.1f), new Vector2(0.09f, 0.62f),
                new Vector2(0.1f, 0.62f), new Vector2(0.1f, 0.66f), new Vector2(0.075f, 0.66f), new Vector2(0f, 0.65f),
            }, 14);
            m.Lathe((p, n) => K.Trim, foot + Vector3.up * 0.78f, new List<Vector2>
            {
                new Vector2(0f, -0.02f), new Vector2(0.2f, -0.02f), new Vector2(0.2f, 0f), new Vector2(0f, 0.12f),
            }, 14);
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI * 2f / 3f;
                Vector3 off = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.085f;
                m.Tube(K.Trim, foot + Vector3.up * 0.64f + off, foot + Vector3.up * 0.77f + off * 1.6f, 0.008f, 5);
            }

            // A vent hood over the kitchen: a box with a lip and slats.
            var vent = new Vector3(0.45f, roof + 0.07f, -1.85f);
            m.Box(Steel, vent, new Vector3(0.42f, 0.14f, 0.5f), 0.03f, 2);
            m.Box(Steel * 0.9f, vent + Vector3.up * 0.09f, new Vector3(0.48f, 0.04f, 0.56f), 0.015f, 2);
            for (int k = -2; k <= 2; k++)
                m.Box(K.Interior, vent + new Vector3(0.215f, 0f, k * 0.085f), new Vector3(0.01f, 0.07f, 0.04f), 0.004f);
        }

        /// <summary>
        /// A hot dog: a bun of two fat halves, the sausage longer than it poking out at both ends, and a
        /// mustard zigzag along the top. <paramref name="sideways"/> stands it on its side, for a sign.
        /// </summary>
        static void HotDog(B.Model m, Vector3 at, Quaternion turn, float scale, bool sideways)
        {
            Quaternion lay = sideways ? turn : Quaternion.identity;
            Func<Vector3, Vector3, Color> bun = (p, n) => Color.Lerp(Bun, BunTop, Mathf.Clamp01(Vector3.Dot(n, lay * Vector3.up) * 1.4f));

            // Two rolls of bun side by side with a cleft between them for the sausage to lie in.
            float r = 0.13f * scale, length = 0.62f * scale;
            for (int s = -1; s <= 1; s += 2)
                S.Ball(m, bun, at + lay * new Vector3(s * r * 0.98f, r * 0.85f, 0f), new Vector3(r * 0.92f, r * 0.85f, length), lay, 18, 12);

            Vector3 axis = lay * Vector3.forward;
            Vector3 middle = at + lay * new Vector3(0f, r * 1.45f, 0f);
            float sausage = length * 1.18f, thick = r * 0.62f;
            m.Tube((p, n) => Sausage * (1f + 0.06f * Mathf.Sin(Vector3.Dot(p - middle, axis) * 40f)), middle - axis * sausage, middle + axis * sausage,
                   thick, thick, 16);
            S.Ball(m, (p, n) => Sausage, middle - axis * sausage, Vector3.one * thick, lay, 12, 8);
            S.Ball(m, (p, n) => Sausage, middle + axis * sausage, Vector3.one * thick, lay, 12, 8);

            var squiggle = new List<Vector3>();
            for (int k = 0; k <= 12; k++)
            {
                float t = k / 12f;
                float side = (k % 2 == 0 ? -1f : 1f) * thick * 0.55f;
                squiggle.Add(middle + lay * new Vector3(side, thick * 0.92f, Mathf.Lerp(-sausage * 0.85f, sausage * 0.85f, t)));
            }
            K.Sweep(m, (p, n) => Mustard, squiggle, thick * 0.22f, 8, 4);
        }

        /// <summary>A ladder up the back corner on the kerb side, standing off the body on short brackets.</summary>
        static void Ladder(B.Model m, Loft box)
        {
            Vector3 low = OnEnd(box, 0.66f, 0.95f, false, 0.07f), high = OnEnd(box, 0.66f, 2.4f, false, 0.07f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 off = Vector3.right * s * 0.16f;
                K.Sweep(m, Shiny(K.Chrome), new[] { low + off, high + off, high + off + new Vector3(0f, 0.18f, 0.12f) }, 0.016f, 6, 3);
                foreach (float t in new[] { 0.08f, 0.92f })
                    m.Tube(K.Chrome, Vector3.Lerp(low, high, t) + off, Vector3.Lerp(low, high, t) + off + Vector3.forward * 0.07f, 0.012f, 6);
            }

            for (int k = 0; k < 6; k++)
            {
                Vector3 rung = Vector3.Lerp(low, high, (k + 0.5f) / 6f);
                m.Tube(K.Chrome, rung - Vector3.right * 0.16f, rung + Vector3.right * 0.16f, 0.012f, 6);
            }
        }

        /// <summary>Two gas bottles strapped in a cage at the back on the road side.</summary>
        static void GasRack(B.Model m, Loft box)
        {
            Vector3 back = OnEnd(box, -0.55f, 1.0f, false, 0.0f);
            var floor = new Vector3(back.x, 0.62f, back.z - 0.2f);
            m.Box(K.Trim, floor, new Vector3(0.62f, 0.04f, 0.34f), 0.012f, 2);

            for (int k = 0; k < 2; k++)
            {
                Vector3 foot = floor + new Vector3(-0.15f + k * 0.3f, 0.02f, 0f);
                Color c = k == 0 ? TruckRed : B.Hex("50606E");
                m.Lathe((p, n) => n.y > 0.8f ? c * 1.1f : c, foot, new List<Vector2>
                {
                    new Vector2(0f, 0f), new Vector2(0.11f, 0f), new Vector2(0.125f, 0.03f), new Vector2(0.125f, 0.42f), new Vector2(0.1f, 0.5f),
                    new Vector2(0.04f, 0.53f), new Vector2(0.04f, 0.58f), new Vector2(0f, 0.58f),
                }, 14);
                m.Tube(K.Chrome, foot + Vector3.up * 0.58f, foot + Vector3.up * 0.62f, 0.03f, 8);
            }

            foreach (float y in new[] { 0.25f, 0.45f })
                K.Sweep(m, (p, n) => K.Strap, new[] { floor + new Vector3(-0.31f, y, 0.16f), floor + new Vector3(0.31f, y, 0.16f) }, 0.012f, 6, 2);
        }

        // --- How it was found, and how it looks back at work -------------------------------------------

        /// <summary>
        /// The truck as it was found: rust through the paint in patches, a tarp lashed over the roof
        /// sign, planks nailed across the hatch, and a flat cardboard CLOSED sign in the cab window.
        /// </summary>
        static B.Model Derelict(Loft tub, Loft box)
        {
            var m = new B.Model { Transform = Matrix4x4.Translate(Vector3.down * TruckR) };

            var rust = new (float side, float z, float y, float w, float h)[]
            {
                (1f, -2.1f, 0.62f, 0.4f, 0.22f), (1f, 0.9f, 0.55f, 0.3f, 0.18f), (-1f, -1.4f, 0.6f, 0.5f, 0.2f),
                (-1f, 0.2f, 0.85f, 0.28f, 0.16f), (1f, -0.4f, 2.25f, 0.36f, 0.16f), (-1f, -2.0f, 1.9f, 0.3f, 0.26f),
            };
            for (int i = 0; i < rust.Length; i++)
            {
                var r = rust[i];
                Loft on = r.y < TruckBelt ? tub : box;
                on.Patch(m, K.Worn(K.RustDark, 80 + i, 0f), on.SideArea(r.side, r.z - r.w, r.z + r.w, r.y - r.h, r.y + r.h), 0.01f, 10, 6, 2f);
            }

            // Planks over the hatch, nailed at slightly different angles.
            float zc = (HatchZ0 + HatchZ1) * 0.5f;
            for (int k = 0; k < 3; k++)
            {
                float y = Mathf.Lerp(HatchY0 + 0.14f, HatchY1 - 0.14f, k / 2f);
                Vector3 at = OnSide(box, zc + 0.05f * B.Jitter(k), y, 1f, 0.06f);
                Quaternion tilt = Quaternion.Euler(4f * B.Jitter(k + 3), 0f, 0f);
                m.Box(Plank * (1f + 0.05f * B.Jitter(k + 9)), at, new Vector3(0.04f, 0.16f, HatchZ1 - HatchZ0 + 0.3f), 0.012f,
                      Quaternion.Euler(6f * B.Jitter(k + 11), 0f, 0f) * tilt);
                foreach (float z in new[] { HatchZ0 - 0.06f, HatchZ1 + 0.06f })
                    m.Tube(K.Trim, new Vector3(at.x + 0.02f, y, z), new Vector3(at.x + 0.035f, y, z), 0.012f, 6);
            }

            // A tarp roped over the roof sign, sagging between its corners.
            float roof = box.Warp(box.Top(0f, -0.8f)).y;
            float signTop = roof + 0.28f + 0.5f + 0.2f;
            m.Surface((p, n) => Tarp * (0.9f + 0.1f * K.Noise(p * 6f)) * (n.y < -0.3f ? 0.7f : 1f), 18, 14, (u, v) =>
            {
                float x = (u - 0.5f) * 0.8f;
                float z = -0.75f + (v - 0.5f) * 2.4f;
                float drape = Mathf.Pow(Mathf.Abs(x) / 0.4f, 1.8f);
                float y = signTop - drape * 0.62f + 0.04f * Mathf.Sin(v * 17f) * (1f - drape) - 0.08f * Mathf.Pow(Mathf.Abs(v - 0.5f) * 2f, 4f);
                return new Vector3(x * (1f + drape * 0.25f), y, z);
            });
            for (int s = -1; s <= 1; s += 2)
                K.Sweep(m, (p, n) => B.Twine * 0.8f, new[] { new Vector3(s * 0.42f, signTop - 0.6f, -1.4f), new Vector3(s * 0.62f, roof + 0.01f, -1.6f) }, 0.01f, 5, 2);

            // CLOSED, on cardboard, taped in the cab's side window.
            Vector3 card = OnSide(box, 0.75f, 1.5f, 1f, 0.03f);
            Quaternion face = Quaternion.LookRotation(Vector3.right, Vector3.up) * Quaternion.Euler(0f, 0f, 6f);
            m.Box(K.Cardboard, card, new Vector3(0.36f, 0.22f, 0.012f), 0.004f, face);
            m.Box(TruckRed * 0.8f, card + Vector3.right * 0.008f, new Vector3(0.26f, 0.06f, 0.004f), 0.002f, face);
            return m;
        }

        /// <summary>
        /// Back at work: a string of bulbs looped along over the hatch and round the sign, a glowing
        /// OPEN plaque in the hatch, sauce bottles and a napkin box on the counter.
        /// </summary>
        static B.Model Restored(Loft box)
        {
            var m = new B.Model { Transform = Matrix4x4.Translate(Vector3.down * TruckR) };

            // Bulbs swagged along the top of the hatch.
            var wire = new List<Vector3>();
            const int bulbs = 9;
            for (int k = 0; k <= bulbs * 2; k++)
            {
                float t = k / (float)(bulbs * 2);
                float z = Mathf.Lerp(HatchZ0 - 0.1f, HatchZ1 + 0.1f, t);
                float sag = Mathf.Abs(Mathf.Sin(t * Mathf.PI * bulbs)) * 0.06f;
                wire.Add(OnSide(box, z, HatchY1 + 0.32f - sag, 1f, 0.05f));
            }
            K.Sweep(m, (p, n) => K.Trim, wire, 0.006f, 5, 2);
            for (int k = 0; k < bulbs; k++)
            {
                Vector3 at = wire[k * 2 + 1] + Vector3.down * 0.05f;
                m.Tube(K.Trim, at + Vector3.up * 0.04f, at + Vector3.up * 0.015f, 0.012f, 6);
                GlowBall(m, at - Vector3.up * 0.02f, 0.03f);
            }

            // Bulbs along the top edge of the roof sign, both sides.
            float roof = box.Warp(box.Top(0f, -0.8f)).y;
            float y = roof + 0.28f + 0.5f - 0.03f;
            for (int s = -1; s <= 1; s += 2)
                for (int k = -3; k <= 3; k++)
                    GlowBall(m, new Vector3(s * 0.095f, y, -0.75f + k * 0.28f), 0.026f);

            // OPEN, lit, in the corner of the hatch.
            Vector3 plaque = OnSide(box, HatchZ1 - 0.28f, HatchY1 - 0.18f, 1f, 0.045f);
            Quaternion face = Quaternion.LookRotation(Vector3.right, Vector3.up);
            m.Box(K.Trim, plaque, new Vector3(0.4f, 0.16f, 0.03f), 0.02f, face, 2);
            var lit = new SmoothMesh(1);
            lit.RoundedBox(0, plaque + Vector3.right * 0.018f, new Vector3(0.012f, 0.1f, 0.32f), 0.005f, Quaternion.identity, 2);
            m.Add(lit, Color.white, B.Model.Glow);

            // Sauces and a napkin box on the counter.
            Vector3 sill = OnSide(box, (HatchZ0 + HatchZ1) * 0.5f, HatchY0 + 0.005f, 1f, 0.15f);
            var bottles = new (float z, Color c)[] { (-0.5f, TruckRed), (-0.38f, Mustard), (0.42f, TruckRed) };
            foreach (var b in bottles)
            {
                Vector3 foot = new Vector3(sill.x + 0.05f, sill.y, sill.z + b.z);
                m.Lathe((p, n) => b.c, foot, new List<Vector2>
                {
                    new Vector2(0f, 0f), new Vector2(0.035f, 0f), new Vector2(0.038f, 0.02f), new Vector2(0.038f, 0.13f), new Vector2(0.022f, 0.16f),
                    new Vector2(0.008f, 0.2f), new Vector2(0f, 0.205f),
                }, 10);
            }

            Vector3 napkins = new Vector3(sill.x + 0.06f, sill.y + 0.06f, sill.z + 0.15f);
            m.Box(Steel, napkins, new Vector3(0.1f, 0.12f, 0.14f), 0.015f, 2);
            m.Box(B.Hex("D8D2C2"), napkins + Vector3.up * 0.065f, new Vector3(0.06f, 0.03f, 0.1f), 0.008f, 2);
            return m;
        }

        /// <summary>A little glowing ball - a bulb - in the glow submesh.</summary>
        static void GlowBall(B.Model m, Vector3 centre, float r)
        {
            var profile = new List<Vector2>();
            for (int i = 0; i <= 6; i++)
            {
                float a = -Mathf.PI * 0.5f + i * Mathf.PI / 6f;
                profile.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
            }

            m.Lathe((p, n) => Color.white, centre, profile, 10, B.Model.Glow);
        }

        // --- The prefab -------------------------------------------------------------------------------

        /// <summary>
        /// Builds the food truck's meshes and its prefab - a visitor's vehicle the depot drives - with
        /// the awning on its hinge and the found and restored dressing as children of the sprung body.
        /// Rebuilt in place, so the scene's copy keeps pointing at it.
        /// </summary>
        internal static GameObject BuildFoodTruck(VehicleDefinition definition, string meshFolder, string prefabPath)
        {
            FoodTruckParts parts = FoodTruck();
            Rig rig = parts.Rig;

            ContentGenerator.EnsureFolder(meshFolder);
            ContentGenerator.EnsureFolder(System.IO.Path.GetDirectoryName(prefabPath).Replace('\\', '/'));

            Mesh body = rig.Body.Save($"{meshFolder}/FoodTruck_Body.asset", "FoodTruck_Body");
            Mesh wheel = rig.Wheel.Save($"{meshFolder}/FoodTruck_Wheel.asset", "FoodTruck_Wheel");
            Mesh awning = parts.Awning.Save($"{meshFolder}/FoodTruck_Awning.asset", "FoodTruck_Awning");
            Mesh derelict = parts.Derelict.Save($"{meshFolder}/FoodTruck_Derelict.asset", "FoodTruck_Derelict");
            Mesh restored = parts.Restored.Save($"{meshFolder}/FoodTruck_Restored.asset", "FoodTruck_Restored");

            bool created = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null;
            GameObject root = created ? new GameObject("FoodTruck") : PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                Dress(root, definition, rig, body, wheel, new Mesh[0]);

                Transform sprung = root.transform.Find("Body");
                Vector3 pivot = new Vector3(0f, rig.PivotY, 0f);

                Transform hinge = Child(sprung, "Awning", parts.Hinge - pivot);
                hinge.localRotation = parts.HingeTurn;
                B.Show(hinge.gameObject, awning, new[] { B.PaintedMaterial() });

                Transform found = Child(sprung, "Derelict", Vector3.zero);
                B.Show(found.gameObject, derelict, B.MaterialsFor(derelict));

                Transform working = Child(sprung, "Restored", Vector3.zero);
                B.Show(working.gameObject, restored, B.MaterialsFor(restored));

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                if (created) Object.DestroyImmediate(root);
                else PrefabUtility.UnloadPrefabContents(root);
            }

            definition.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition.prefab;
        }
    }
}
