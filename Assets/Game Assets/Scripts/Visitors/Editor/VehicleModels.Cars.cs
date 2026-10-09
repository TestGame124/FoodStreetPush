using System;
using System.Collections.Generic;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using K = FoodStreet.Visitors.EditorTools.VehicleKit;
using S = LastOrders.EditorTools.SoupKitchenModel;

namespace FoodStreet.Visitors.EditorTools
{
    /// <summary>
    /// The five designs. Each is a fat tub of bodywork with a bubble of a cabin, sized to its
    /// definition's footprint so the parking bays still fit, and given its own silhouette:
    ///
    /// - Hatchback (the regular): a short round two-box hatch in sage teal with a cream roof, a
    ///   suitcase and a bedroll on its rack.
    /// - Pickup (the trucker, the farmhand): an old farm truck in diner red - a long hood between
    ///   separate bulging front fenders with the headlamps on top of them, a round cab, running
    ///   boards, and a timber-staked flatbed carrying straw bales, churns and a crate.
    /// - Van (the courier): a tall rounded bread van, cream over teal with a red pinstripe and a loaf
    ///   on a roundel on its sides, parcels piled on the roof.
    /// - Saloon (the family): a longer three-box car in slate blue with a dark vinyl roof, chrome
    ///   bumpers and side trim, whitewall tyres, and the family's luggage on top.
    /// - Shuttle (the hospital's): a rounded mustard minibus with a cream band, a row of windows,
    ///   folding glass doors on the kerb side and a red cross on its roof.
    ///
    /// The driver's door is on the right, the side the parking bays let people out on.
    /// </summary>
    public static partial class VehicleModels
    {
        // --- Shaping --------------------------------------------------------------------------------

        static float Sq(float x) => x * x;

        /// <summary>The underside at <paramref name="z"/>: <paramref name="clearance"/>, arched up over each wheel.</summary>
        static float Arch(float z, float clearance, float axle, float radius, params float[] wheels)
        {
            float bottom = clearance;
            foreach (float w in wheels)
            {
                float dz = z - w;
                if (Mathf.Abs(dz) < radius) bottom = Mathf.Max(bottom, axle + Mathf.Sqrt(radius * radius - dz * dz));
            }

            return bottom;
        }

        /// <summary>0..1, how much the bodywork bulges out over a wheel at <paramref name="z"/>.</summary>
        static float Fenders(float z, float reach, params float[] wheels)
        {
            float bulge = 0f;
            foreach (float w in wheels)
            {
                float t = Mathf.Abs(z - w) / reach;
                if (t < 1f) bulge = Mathf.Max(bulge, Sq(Mathf.Cos(t * Mathf.PI * 0.5f)));
            }

            return bulge;
        }

        /// <summary>A cabin's glass leant back: the top pushed back toward the front by <paramref name="forward"/>, forward at the back by <paramref name="rearward"/>.</summary>
        static Func<Vector3, Vector3> Rake(float belt, float roof, float middle, float front, float back, float forward, float rearward) =>
            p =>
            {
                float h = K.Smooth((p.y - belt) / (roof - belt));
                float f = K.Smooth((p.z - middle) / (front - middle));
                float r = K.Smooth((middle - p.z) / (middle - back));
                p.z -= h * (forward * f - rearward * r);
                return p;
            };

        static Func<Vector3, Vector3, Color> Shiny(Color colour) =>
            (p, n) => n.y < -0.4f ? colour * 0.6f : Color.Lerp(colour, colour * 1.14f, Mathf.Max(0f, n.y));

        // --- Laid-on parts --------------------------------------------------------------------------

        /// <summary>Glass paint for a pane over <paramref name="area"/>, its streaks laid across the pane itself.</summary>
        static Func<Vector3, Vector3, Color> Glazing(Loft loft, Func<float, float, Vector3> area)
        {
            Vector3 centre = loft.Warp(area(0f, 0f));
            return K.Glazing(centre, loft.Warp(area(1f, 0f)) - loft.Warp(area(-1f, 0f)), loft.Warp(area(0f, 1f)) - loft.Warp(area(0f, -1f)));
        }

        /// <summary>A pane of glass on <paramref name="loft"/>, single-sided.</summary>
        static void Pane(Loft loft, B.Model m, Func<float, float, Vector3> area, int a = 22, int b = 10) =>
            loft.Patch(m, Glazing(loft, area), area, 0.006f, a, b, 5f);

        /// <summary>Glass in a door: solid, so it shows from behind with the door open.</summary>
        static void DoorGlass(Loft loft, B.Model m, Func<float, float, Vector3> area, float lift = 0.01f)
        {
            Func<Vector3, Vector3, Color> glass = Glazing(loft, area);
            loft.Patch(m, glass, area, lift, 20, 10, 5f, 0.012f, (p, n) => glass(p, n) * 0.85f);
        }

        /// <summary>A door's skin: a solid panel just proud of the body, dark inside.</summary>
        static void Panel(Loft loft, B.Model m, Func<float, float, Vector3> area, Func<Vector3, Vector3, Color> paint, float lift = 0.012f) =>
            loft.Patch(m, paint, area, lift, 18, 10, 9f, 0.035f, K.Flat(K.Trim));

        /// <summary>The dark of the inside, behind a door, showing once it opens.</summary>
        static void Opening(Loft loft, B.Model m, Func<float, float, Vector3> area) =>
            loft.Patch(m, K.Flat(K.Interior), area, 0.003f, 10, 6, 8f);

        static void Decal(Loft loft, B.Model m, Func<float, float, Vector3> area, Color colour, float lift, float corner = 6f, int a = 16, int b = 6) =>
            loft.Patch(m, K.Flat(colour), area, lift, a, b, corner);

        /// <summary>A roof in a second colour, laid over the cabin down to <paramref name="edge"/> high on the sides, so its edge is crisp.</summary>
        static void RoofPanel(Loft loft, B.Model m, Color colour, float edge, float front, float back, int key, int along = 36) =>
            loft.Patch(m, K.Worn(colour, key, 0.2f), loft.RoofArea(edge, front, back), 0.004f, 24, along, 4f);

        /// <summary>A point on the side, lifted <paramref name="lift"/> off it.</summary>
        static Vector3 OnSide(Loft loft, float z, float y, float side, float lift = 0f)
        {
            Func<float, float, Vector3> at = (a, b) => loft.Side(a, b, side);
            return loft.Warp(at(z, y)) + loft.NormalOf(at, z, y) * lift;
        }

        static Vector3 OnEnd(Loft loft, float x, float y, bool front, float lift = 0f)
        {
            Func<float, float, Vector3> at = (a, b) => loft.End(a, b, front);
            return loft.Warp(at(x, y)) + loft.NormalOf(at, x, y) * lift;
        }

        /// <summary>
        /// A car's front door on <paramref name="side"/>, hinged at its front edge: the panel over
        /// <paramref name="z0"/>..<paramref name="z1"/> by <paramref name="y0"/>..<paramref name="y1"/> on
        /// the tub, its glass over <paramref name="gz0"/>..<paramref name="gz1"/> by <paramref name="g0"/>..<paramref name="g1"/>
        /// on the cabin, the dark inside behind both, and a chrome handle.
        /// </summary>
        static B.Model CarDoor(Rig rig, Loft tub, Loft cabin, float side, float z0, float z1, float y0, float y1, float gz0, float gz1, float g0,
                               float g1, Func<Vector3, Vector3, Color> paint)
        {
            Vector3 edge = OnSide(tub, z1, (y0 + y1) * 0.5f, side);
            B.Model door = rig.AddDoor(new Vector3(edge.x + side * 0.025f, (y0 + y1) * 0.5f, z1), -side * 68f);
            rig.DoorSide = side;

            Opening(tub, rig.Body, tub.SideArea(side, z0 + 0.03f, z1 - 0.03f, y0 + 0.03f, y1 - 0.03f));
            Opening(cabin, rig.Body, cabin.SideArea(side, gz0, gz1, g0, g1));

            Panel(tub, door, tub.SideArea(side, z0, z1, y0, y1), paint);
            DoorGlass(cabin, door, cabin.SideArea(side, gz0, gz1, g0, g1));
            Handle(tub, door, z0 + 0.17f, y1 - 0.1f, side);
            return door;
        }

        /// <summary>A door that does not open: the same panel and handle, fixed to the body.</summary>
        static void FixedDoor(Loft tub, B.Model m, float side, float z0, float z1, float y0, float y1, Func<Vector3, Vector3, Color> paint,
                              bool handleAtFront = false)
        {
            tub.Patch(m, paint, tub.SideArea(side, z0, z1, y0, y1), 0.008f, 18, 10, 9f, 0.02f, K.Flat(K.Trim));
            Handle(tub, m, handleAtFront ? z1 - 0.17f : z0 + 0.17f, y1 - 0.1f, side);
        }

        static void Handle(Loft tub, B.Model m, float z, float y, float side)
        {
            Vector3 at = OnSide(tub, z, y, side, 0.03f);
            m.Box(K.Chrome, at, new Vector3(0.03f, 0.035f, 0.15f), 0.014f);
        }

        /// <summary>A chrome strip along the side at <paramref name="y"/>, <paramref name="z0"/> to <paramref name="z1"/>.</summary>
        static void TrimLine(Loft tub, B.Model m, float side, float z0, float z1, float y, float lift = 0.022f)
        {
            var points = new List<Vector3>();
            int steps = Mathf.Max(2, Mathf.CeilToInt((z1 - z0) / 0.25f));
            for (int k = 0; k <= steps; k++) points.Add(OnSide(tub, Mathf.Lerp(z0, z1, k / (float)steps), y, side, lift));
            K.Sweep(m, Shiny(K.Chrome), points, 0.014f, 6, 3);
        }

        /// <summary>A round lamp on an end of <paramref name="loft"/>, looking out and a little ahead.</summary>
        static void EndLamp(B.Model m, Loft loft, bool front, float x, float y, float r, Color lens, Color bezel)
        {
            Func<float, float, Vector3> at = (a, b) => loft.End(a, b, front);
            Vector3 n = loft.NormalOf(at, x, y);
            Vector3 facing = Vector3.Lerp(n, front ? Vector3.forward : Vector3.back, 0.45f).normalized;
            K.Lamp(m, loft.Warp(at(x, y)) + n * 0.012f, facing, r, lens, bezel);
        }

        /// <summary>
        /// A grille for a mouth: a dark opening on the front end with chrome bars across it that
        /// curve up at the ends by <paramref name="smile"/>, and a chrome frame round it.
        /// </summary>
        static void Grille(B.Model m, Loft loft, float y0, float y1, float halfWidth, float smile, int bars)
        {
            Decal(loft, m, loft.EndArea(true, y0, y1, 0f, 0f, halfWidth * 2f), K.Interior, 0.006f, 4f, 14, 6);

            for (int k = 0; k < bars; k++)
            {
                float y = Mathf.Lerp(y0, y1, (k + 0.5f) / bars);
                var points = new List<Vector3>();
                for (int i = 0; i <= 6; i++)
                {
                    float t = i / 3f - 1f;
                    points.Add(OnEnd(loft, t * halfWidth * 0.86f, y + smile * t * t, true, 0.014f));
                }

                K.Sweep(m, Shiny(K.Chrome), points, 0.016f, 8, 4);
            }

            var frame = new List<Vector3>();
            float cy = (y0 + y1) * 0.5f, hy = (y1 - y0) * 0.5f + 0.022f, hx = halfWidth + 0.025f;
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI * 2f / 16f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                // A rounded rectangle: a superellipse.
                float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 0.4f) * hx;
                float y = cy + Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.4f) * hy + smile * Sq(x / hx);
                frame.Add(OnEnd(loft, x, y, true, 0.016f));
            }

            K.Sweep(m, Shiny(K.Chrome), frame, 0.02f, 8, 4, closed: true);
        }

        /// <summary>A bumper wrapped round an end of <paramref name="loft"/> at <paramref name="y"/>, back along the sides by <paramref name="wrap"/>.</summary>
        static void Bumper(B.Model m, Loft loft, bool front, float y, float radius, Color colour, float wrap = 0.26f, float fill = 0.92f)
        {
            float half = loft.EndHalfWidth(y, front);
            float zSide = front ? loft.ZFront - wrap : loft.ZBack + wrap;
            float zc = front ? loft.ZFront - half : loft.ZBack + half;

            Vector3 Out(Vector3 pre)
            {
                Vector3 p = loft.Warp(pre);
                Vector3 d = new Vector3(p.x, 0f, p.z - zc);
                return p + d.normalized * (radius * 0.55f);
            }

            var points = new List<Vector3> { Out(loft.Side(zSide, y, -1f)) };
            for (int k = 0; k <= 6; k++) points.Add(Out(loft.End(Mathf.Lerp(-fill, fill, k / 6f) * half, y, front)));
            points.Add(Out(loft.Side(zSide, y, 1f)));
            K.Sweep(m, Shiny(colour), points, radius, 12, 5);
        }

        /// <summary>Red lenses either side of the back, raised off it.</summary>
        static void TailLamps(B.Model m, Loft loft, float x, float y0, float y1, float width)
        {
            for (int s = -1; s <= 1; s += 2)
                loft.Patch(m, Shiny(K.TailLens), loft.EndArea(false, y0, y1, 0f, s * x, width), 0.012f, 8, 6, 3f, 0.02f, K.Flat(K.TailLens * 0.7f));
        }

        /// <summary>
        /// A roof rack over <paramref name="z0"/>..<paramref name="z1"/>: two rails <paramref name="halfX"/>
        /// out from the middle at one level just above the crown of the roof, on feet, with bars across.
        /// Returns the height things tied on it stand at.
        /// </summary>
        static float Rack(B.Model m, Loft loft, float halfX, float z0, float z1, int bars)
        {
            float Crown(float z) => loft.Warp(loft.Top(0f, z)).y;
            float level = Mathf.Max(Crown(z0), Mathf.Max(Crown(z1), Crown((z0 + z1) * 0.5f))) + 0.05f;

            Vector3 At(float x, float z)
            {
                Vector3 p = loft.Warp(loft.Top(x, z));
                return new Vector3(p.x, level, p.z);
            }

            for (int s = -1; s <= 1; s += 2)
            {
                var rail = new List<Vector3>();
                for (int k = 0; k <= 4; k++) rail.Add(At(s * halfX, Mathf.Lerp(z0, z1, k / 4f)));
                K.Sweep(m, Shiny(K.Trim), rail, 0.022f, 8, 4);

                foreach (float z in new[] { z0 + 0.06f, (z0 + z1) * 0.5f, z1 - 0.06f })
                {
                    Vector3 top = At(s * halfX, z);
                    Vector3 foot = loft.Warp(loft.Top(s * halfX, z));
                    float height = Mathf.Max(0.03f, top.y - foot.y) + 0.03f;
                    m.Box(K.Trim, new Vector3(top.x, (top.y + foot.y) * 0.5f, top.z), new Vector3(0.05f, height, 0.08f), 0.015f);
                }
            }

            for (int k = 0; k < bars; k++)
            {
                float z = Mathf.Lerp(z0 + 0.1f, z1 - 0.1f, bars > 1 ? k / (bars - 1f) : 0.5f);
                K.Sweep(m, Shiny(K.Trim), new[] { At(-halfX, z), At(halfX, z) }, 0.018f, 8, 3);
            }

            return level + 0.018f;
        }

        // --- Hatchback --------------------------------------------------------------------------------

        static Rig Hatchback(Color paint, Color roof)
        {
            const float R = 0.37f, Track = 0.7f, ArchR = 0.44f, Clear = 0.25f, Belt = 0.98f, Roof = 1.68f;
            const float ZF = 1.06f, ZR = -1.12f;
            const float G0 = 1.04f, G1 = 1.52f;

            var rig = new Rig(R);
            B.Model m = rig.Body;
            Func<Vector3, Vector3, Color> body = K.Worn(paint, 11);

            var tub = new Loft
            {
                Back = -1.85f, Front = 1.85f, CapBack = 0.3f, CapFront = 0.36f, Round = 3f, CapRound = 2.6f,
                Section = z => new Vector3(Arch(z, Clear, R, ArchR, ZF, ZR),
                                           Belt + 0.025f * K.Smooth((-z - 1f) / 0.5f) - 0.17f * K.Smooth((z - 0.5f) / 1.1f),
                                           0.82f + 0.035f * Fenders(z, 0.62f, ZF, ZR)),
                Tuck = y => 1f - 0.09f * Sq(Mathf.Max(0f, y)) - 0.07f * Sq(Mathf.Max(0f, -y)),
            };
            tub.Build(m, body);

            // A bubble of a cabin, the glass raked back at the front and the hatch nearly upright.
            var cabin = new Loft
            {
                Back = -1.76f, Front = 0.78f, CapBack = 0.24f, CapFront = 0.56f, Round = 3f, CapRound = 2.4f,
                Section = z => new Vector3(0.62f, Roof - 0.03f * K.Smooth((z + 0.3f) / 0.5f), 0.74f),
                Tuck = y => 1f - 0.18f * Mathf.Pow(Mathf.Max(0f, y), 1.5f),
            };
            cabin.Warp = Rake(Belt, Roof, -0.5f, cabin.Front, cabin.Back, 0.3f, 0.05f);
            cabin.Build(m, K.Worn(paint, 12, 0.5f), 48, 48);
            RoofPanel(cabin, m, roof, G1 + 0.035f, G1 + 0.03f, 1.53f, 13);

            Pane(cabin, m, cabin.EndArea(true, G0, G1, 0.8f));
            Pane(cabin, m, cabin.EndArea(false, 1.08f, 1.5f, 0.74f));
            for (int s = -1; s <= 1; s += 2) Pane(cabin, m, cabin.SideArea(s, -1.42f, -0.64f, G0, G1 - 0.02f));
            Pane(cabin, m, cabin.SideArea(-1f, -0.52f, 0.18f, G0, G1));

            CarDoor(rig, tub, cabin, 1f, -0.56f, 0.4f, 0.33f, 0.9f, -0.52f, 0.18f, G0, G1, body);
            FixedDoor(tub, m, -1f, -0.56f, 0.4f, 0.33f, 0.9f, body);

            // The face: big round lamps for eyes, a little chrome grin under them.
            for (int s = -1; s <= 1; s += 2)
            {
                EndLamp(m, tub, true, s * 0.47f, 0.69f, 0.13f, K.Lens, K.Chrome);
                EndLamp(m, tub, true, s * 0.72f, 0.58f, 0.045f, K.Indicator, K.Chrome);
                K.Mirror(m, OnSide(tub, 0.42f, Belt - 0.04f, s), s, paint);
            }

            Grille(m, tub, 0.56f, 0.63f, 0.24f, 0.025f, 2);
            Bumper(m, tub, true, 0.5f, 0.07f, K.BumperGrey);
            Bumper(m, tub, false, 0.53f, 0.07f, K.BumperGrey);
            TailLamps(m, tub, 0.6f, 0.72f, 0.88f, 0.17f);
            K.NumberPlate(m, OnEnd(tub, 0f, 0.66f, false, 0.01f), Vector3.back, 0.38f);

            // A suitcase, a bedroll and a bag tied on the rack.
            float deck = Rack(m, cabin, 0.5f, -1.28f, -0.08f, 3);
            K.Suitcase(m, new Vector3(0.04f, deck, -0.86f), new Vector3(0.66f, 0.24f, 0.46f), 4f, B.Hex("8F7A4E"), 21);
            K.Bedroll(m, new Vector3(-0.02f, deck + 0.12f, -0.3f), 0.84f, 0.12f, -3f, B.Hex("5A7477"), 22);
            K.Holdall(m, new Vector3(0.12f, deck + 0.235f, -0.9f), new Vector3(0.4f, 0.2f, 0.26f), 20f, B.Hex("767459"), 23);

            K.Tailpipe(m, new Vector3(0.42f, 0.3f, -1.8f));
            rig.Exhaust = new Vector3(0.42f, 0.3f, -1.84f);
            rig.FourWheels(Track, ZF, ZR, R, 0.27f, K.Cream, K.Chrome, false);
            return rig;
        }

        // --- Pickup -----------------------------------------------------------------------------------

        static Rig Pickup(Color paint, Color roof)
        {
            const float R = 0.43f, Track = 0.79f, ArchR = 0.47f, ZF = 1.5f, ZR = -1.42f;
            const float G0 = 1.24f, G1 = 1.78f, Deck = 1.03f;

            var rig = new Rig(R);
            B.Model m = rig.Body;
            Func<Vector3, Vector3, Color> body = K.Worn(paint, 31);

            // The cab's lower half, broad, narrowing forward into a long hood between the fenders.
            var hood = new Loft
            {
                Back = -0.54f, Front = 2.26f, CapBack = 0.12f, CapFront = 0.3f, Round = 3.4f, CapRound = 2.8f,
                Section = z => new Vector3(0.5f,
                                           1.16f - 0.05f * K.Smooth((z - 0.62f) / 0.3f) - 0.15f * K.Smooth((z - 1f) / 0.96f),
                                           Mathf.Lerp(0.86f, 0.5f, K.Smooth((z - 0.6f) / 0.45f))),
                Tuck = y => 1f - 0.06f * Sq(Mathf.Max(0f, y)) - 0.05f * Sq(Mathf.Max(0f, -y)),
            };
            hood.Build(m, body, 56, 64);

            var cab = new Loft
            {
                Back = -0.5f, Front = 0.68f, CapBack = 0.16f, CapFront = 0.38f, Round = 3f, CapRound = 2.5f,
                Section = z => new Vector3(0.82f, 1.96f, 0.8f),
                Tuck = y => 1f - 0.15f * Mathf.Pow(Mathf.Max(0f, y), 1.5f),
            };
            cab.Warp = Rake(1.16f, 1.96f, 0f, cab.Front, cab.Back, 0.16f, -0.03f);
            cab.Build(m, K.Worn(paint, 32), 44, 40);
            RoofPanel(cab, m, roof, G1 + 0.04f, G1 + 0.03f, 1.77f, 33, 24);

            Pane(cab, m, cab.EndArea(true, G0, G1, 0.8f));
            Pane(cab, m, cab.EndArea(false, 1.32f, 1.74f, 0.6f));
            Pane(cab, m, cab.SideArea(-1f, -0.28f, 0.24f, G0, G1));
            CarDoor(rig, hood, cab, 1f, -0.4f, 0.56f, 0.56f, 1.08f, -0.28f, 0.24f, G0, G1, body);
            FixedDoor(hood, m, -1f, -0.4f, 0.56f, 0.56f, 1.08f, body);

            // Separate front fenders bulging over the wheels, a headlamp up on each in a bucket.
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 0.77f;
                var fender = new Loft
                {
                    Back = 0.7f, Front = 2.24f, CapBack = 0.3f, CapFront = 0.3f, Round = 2.6f, CapRound = 2.3f,
                    Section = z =>
                    {
                        float bottom = Arch(z, 0.5f, R, ArchR, ZF);
                        return new Vector3(bottom, Mathf.Max(1.07f - 0.32f * Sq((z - ZF) / 0.8f), bottom + 0.14f), 0.2f);
                    },
                    Warp = p => p + new Vector3(x, 0f, 0f),
                };
                fender.Build(m, body, 32, 48);

                Vector3 lamp = new Vector3(x, 1.06f, 2.04f);
                m.Tube(body, lamp + Vector3.back * 0.2f, lamp, 0.12f, 0.155f, 18);
                K.Lamp(m, lamp, Vector3.forward, 0.145f, K.Lens, K.Chrome);
                m.Tube(K.Chrome, new Vector3(x, 0.94f, 1.9f), new Vector3(x, 1.0f, 1.9f), 0.03f, 8);

                // Running boards from the fender back to the bed.
                m.Box(K.Trim, new Vector3(s * 0.84f, 0.47f, 0.17f), new Vector3(0.26f, 0.05f, 1.7f), 0.02f, 2);
                K.Mirror(m, OnSide(cab, 0.22f, 1.22f, s), s, K.Chrome);
            }

            // A tall chrome grille, a chrome bumper, an ornament on the hood.
            {
                const float Gy0 = 0.57f, Gy1 = 0.9f, Half = 0.3f;
                Decal(hood, m, hood.EndArea(true, Gy0, Gy1, 0f, 0f, Half * 2f), K.Interior, 0.006f, 5f, 14, 10);
                for (int k = -3; k <= 3; k++)
                {
                    var bar = new List<Vector3>();
                    for (int i = 0; i <= 4; i++) bar.Add(OnEnd(hood, k * 0.08f, Mathf.Lerp(Gy0 + 0.02f, Gy1 - 0.02f, i / 4f), true, 0.014f));
                    K.Sweep(m, Shiny(K.Chrome), bar, 0.016f, 8, 3);
                }

                var frame = new List<Vector3>();
                for (int k = 0; k < 16; k++)
                {
                    float a = k * Mathf.PI * 2f / 16f;
                    float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                    frame.Add(OnEnd(hood, Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 0.4f) * (Half + 0.03f),
                                    (Gy0 + Gy1) * 0.5f + Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), 0.4f) * ((Gy1 - Gy0) * 0.5f + 0.03f), true, 0.018f));
                }

                K.Sweep(m, Shiny(K.Chrome), frame, 0.026f, 8, 4, closed: true);
                S.Ball(m, Shiny(K.ChromeLight), hood.Top(0f, 1.9f) + new Vector3(0f, 0.025f, 0f), new Vector3(0.035f, 0.03f, 0.07f), Quaternion.identity, 10, 6);

                K.Sweep(m, Shiny(K.Chrome), new[]
                {
                    new Vector3(-0.98f, 0.52f, 2.1f), new Vector3(-0.72f, 0.5f, 2.27f), new Vector3(0f, 0.5f, 2.33f),
                    new Vector3(0.72f, 0.5f, 2.27f), new Vector3(0.98f, 0.52f, 2.1f),
                }, 0.07f, 12, 5);
            }

            // A bent aerial on the left fender.
            m.Tube(K.Trim, new Vector3(-0.85f, 0.98f, 1.2f), new Vector3(-0.86f, 1.32f, 1.14f), 0.013f, 6);
            m.Tube(K.Trim, new Vector3(-0.86f, 1.32f, 1.14f), new Vector3(-0.82f, 1.52f, 1.02f), 0.011f, 6);
            S.Ball(m, (p, n) => K.Trim, new Vector3(-0.82f, 1.52f, 1.02f), Vector3.one * 0.026f, Quaternion.identity, 8, 5);

            // The chassis, and the flatbed on it: red sides, a timber floor, stakes and boards round it.
            m.Box(K.Trim, new Vector3(0f, 0.62f, -0.9f), new Vector3(1.1f, 0.18f, 2.9f), 0.04f, 2);
            var bed = new Loft
            {
                Back = -2.3f, Front = -0.58f, CapBack = 0.08f, CapFront = 0.08f, Round = 6f, CapRound = 5f,
                Section = z => new Vector3(0.84f, 1f, 0.95f),
            };
            bed.Build(m, body, 40, 36);

            for (int k = 0; k < 7; k++)
            {
                Color plank = B.Plank(300 + k) * (1f + 0.04f * B.Jitter(310 + k));
                m.Box(plank, new Vector3(-0.81f + k * 0.27f, 1.012f, -1.44f), new Vector3(0.25f, 0.03f, 1.64f), 0.01f);
            }

            for (int s = -1; s <= 1; s += 2)
            {
                foreach (float z in new[] { -0.66f, -1.44f, -2.22f })
                    m.Box(B.Walnut, new Vector3(s * 0.91f, 1.22f, z), new Vector3(0.07f, 0.44f, 0.07f), 0.015f);

                for (int r = 0; r < 2; r++)
                    m.Box(B.Plank(320 + r * 2 + (s + 1)), new Vector3(s * 0.925f, 1.12f + r * 0.2f, -1.44f), new Vector3(0.045f, 0.13f, 1.66f), 0.012f);

                m.Box(K.Rubber, new Vector3(s * 0.79f, 0.52f, ZR - 0.56f), new Vector3(0.3f, 0.36f, 0.03f), 0.01f);
                m.Box(K.TailLens, new Vector3(s * 0.76f, 0.92f, -2.31f), new Vector3(0.16f, 0.08f, 0.03f), 0.01f);
            }

            for (int r = 0; r < 3; r++)
                m.Box(B.Plank(330 + r), new Vector3(0f, 1.12f + r * 0.2f, -0.64f), new Vector3(1.86f, 0.13f, 0.045f), 0.012f);
            for (int r = 0; r < 2; r++)
                m.Box(B.Plank(340 + r), new Vector3(0f, 1.12f + r * 0.2f, -2.25f), new Vector3(1.86f, 0.13f, 0.045f), 0.012f);

            m.Box(K.BumperGrey, new Vector3(0f, 0.72f, -2.35f), new Vector3(1.7f, 0.11f, 0.1f), 0.04f, 2);
            K.NumberPlate(m, new Vector3(0f, 0.72f, -2.41f), Vector3.back, 0.38f);

            // The load: straw, a crate, two churns and a can of fuel.
            K.Bale(m, new Vector3(0f, Deck, -1.95f), new Vector3(0.95f, 0.4f, 0.5f), 2f, 41);
            K.Bale(m, new Vector3(0.02f, Deck, -1.42f), new Vector3(0.95f, 0.4f, 0.5f), -3f, 42);
            K.Bale(m, new Vector3(0.05f, Deck + 0.4f, -1.7f), new Vector3(0.9f, 0.38f, 0.48f), 8f, 43);
            B.Crate(m, new Vector3(-0.45f, Deck, -0.98f), new Vector3(0.52f, 0.36f, 0.44f), 6f, 44);
            K.Churn(m, new Vector3(0.44f, Deck, -0.92f));
            K.Churn(m, new Vector3(0.66f, Deck, -1.14f), 0.9f);
            K.JerryCan(m, new Vector3(0.12f, Deck, -0.86f), 90f, B.Hex("5E6B44"), 45);

            K.Tailpipe(m, new Vector3(-0.55f, 0.46f, -2.18f));
            rig.Exhaust = new Vector3(-0.55f, 0.46f, -2.22f);
            rig.FourWheels(Track, ZF, ZR, R, 0.31f, paint * 0.92f, K.Chrome, false);
            return rig;
        }

        // --- Van --------------------------------------------------------------------------------------

        static Rig Van(Color paint, Color lower)
        {
            const float R = 0.42f, Track = 0.77f, ArchR = 0.48f, Clear = 0.3f, ZF = 1.45f, ZR = -1.61f, Belt = 1.06f, Roof = 2.22f;
            const float G0 = 1.18f, G1 = 1.78f;
            Color red = B.Hex("8E3B33");

            var rig = new Rig(R);
            B.Model m = rig.Body;
            Func<Vector3, Vector3, Color> tubPaint = K.Worn(lower, 51);
            Func<Vector3, Vector3, Color> boxPaint = K.Worn(paint, 52, 0.6f);

            var tub = new Loft
            {
                Back = -2.3f, Front = 2.3f, CapBack = 0.22f, CapFront = 0.38f, Round = 3.4f, CapRound = 2.6f,
                Section = z => new Vector3(Arch(z, Clear, R, ArchR, ZF, ZR), Belt - 0.14f * K.Smooth((z - 1.2f) / 0.75f),
                                           0.98f + 0.03f * Fenders(z, 0.65f, ZF, ZR)),
                Tuck = y => 1f - 0.08f * Sq(Mathf.Max(0f, y)) - 0.06f * Sq(Mathf.Max(0f, -y)),
            };
            tub.Build(m, tubPaint, 56, 72);

            // The van's box: tall, nearly square in section, its windscreen leant well back.
            var box = new Loft
            {
                Back = -2.27f, Front = 1.64f, CapBack = 0.18f, CapFront = 0.52f, Round = 4.2f, CapRound = 2.6f,
                Section = z => new Vector3(0.8f, Roof - 0.05f * K.Smooth((z - 0.4f) / 0.7f), 0.95f),
                Tuck = y => 1f - 0.07f * Sq(Mathf.Max(0f, y)),
            };
            box.Warp = Rake(Belt, Roof, 0.3f, box.Front, box.Back, 0.34f, 0f);
            box.Build(m, boxPaint, 56, 64);

            Pane(box, m, box.EndArea(true, G0 - 0.02f, 1.9f, 0.86f));
            Pane(box, m, box.SideArea(-1f, 0.22f, 0.88f, G0, G1));
            for (int s = -1; s <= 1; s += 2) Pane(box, m, box.EndArea(false, 1.42f, 1.86f, 0f, s * 0.42f, 0.56f), 10, 8);

            // The cab door: teal below, cream above with its window.
            {
                const float Z0 = 0.14f, Z1 = 0.95f;
                Vector3 edge = OnSide(tub, Z1, 0.75f, 1f);
                B.Model door = rig.AddDoor(new Vector3(edge.x + 0.025f, 1.2f, Z1), -68f);
                Opening(tub, m, tub.SideArea(1f, Z0 + 0.03f, Z1 - 0.03f, 0.47f, 0.96f));
                Opening(box, m, box.SideArea(1f, Z0 + 0.03f, Z1 - 0.03f, 1.1f, 1.95f));
                Panel(tub, door, tub.SideArea(1f, Z0, Z1, 0.44f, 0.99f), tubPaint);
                Panel(box, door, box.SideArea(1f, Z0, Z1, 1.08f, 1.98f), boxPaint);
                DoorGlass(box, door, box.SideArea(1f, 0.22f, 0.88f, G0, G1), 0.018f);
                Handle(tub, door, Z0 + 0.17f, 0.88f, 1f);

                FixedDoor(tub, m, -1f, Z0, Z1, 0.44f, 0.99f, tubPaint);
                box.Patch(m, boxPaint, box.SideArea(-1f, Z0, Z1, 1.08f, 1.98f), 0.008f, 18, 10, 9f, 0.02f, K.Flat(K.Trim));
                DoorGlass(box, m, box.SideArea(-1f, 0.22f, 0.88f, G0, G1), 0.014f);
            }

            // Livery: a red pinstripe, and a cream roundel with a loaf of bread on each side.
            for (int s = -1; s <= 1; s += 2)
            {
                Decal(box, m, box.SideArea(s, -2.02f, s > 0 ? 0.06f : 1.06f, 1.12f, 1.16f), red, 0.004f, 0f, 40, 2);

                const float Z = -0.95f, Y = 1.52f;
                Decal(box, m, box.SideArea(s, Z - 0.39f, Z + 0.39f, Y - 0.39f, Y + 0.39f), lower, 0.004f, 2f, 20, 20);
                Decal(box, m, box.SideArea(s, Z - 0.34f, Z + 0.34f, Y - 0.34f, Y + 0.34f), K.Cream, 0.007f, 2f, 20, 20);

                Vector3 at = OnSide(box, Z, Y - 0.03f, s, 0.03f);
                Quaternion face = Quaternion.LookRotation(Vector3.right * s, Vector3.up);
                m.Box(B.Hex("9C7646"), at, new Vector3(0.44f, 0.22f, 0.06f), 0.05f, face, 2);
                m.Box(B.Hex("B08957"), at + Vector3.up * 0.07f + Vector3.right * s * 0.012f, new Vector3(0.36f, 0.12f, 0.06f), 0.05f, face, 2);
                for (int k = -1; k <= 1; k++)
                    m.Box(B.Hex("7A5A34"), at + face * new Vector3(k * 0.1f, 0.09f, 0.04f), new Vector3(0.025f, 0.08f, 0.01f), 0.004f,
                          face * Quaternion.Euler(0f, 0f, 30f));
            }

            // The back: two doors with a seam between them, chrome handles.
            Decal(box, m, box.EndArea(false, 1.0f, 2.05f, 0f, 0f, 0.03f), K.Trim, 0.005f, 0f, 2, 12);
            for (int s = -1; s <= 1; s += 2)
                m.Box(K.Chrome, OnEnd(box, s * 0.08f, 1.32f, false, 0.025f), new Vector3(0.04f, 0.14f, 0.035f), 0.014f);

            // Face, bumpers and lamps.
            for (int s = -1; s <= 1; s += 2)
            {
                EndLamp(m, tub, true, s * 0.62f, 0.78f, 0.14f, K.Lens, K.Chrome);
                EndLamp(m, tub, true, s * 0.84f, 0.66f, 0.045f, K.Indicator, K.Chrome);
                K.Mirror(m, OnSide(box, 1.0f, 1.32f, s), s, paint);
            }

            Grille(m, tub, 0.6f, 0.69f, 0.3f, 0.03f, 2);
            Bumper(m, tub, true, 0.58f, 0.08f, K.BumperGrey);
            Bumper(m, tub, false, 0.6f, 0.08f, K.BumperGrey);
            TailLamps(m, tub, 0.78f, 0.8f, 0.98f, 0.14f);
            K.NumberPlate(m, OnEnd(tub, 0f, 0.76f, false, 0.01f), Vector3.back, 0.38f);

            // Parcels piled on the rack.
            float deck = Rack(m, box, 0.72f, -1.95f, 0.75f, 4);
            K.Parcel(m, new Vector3(-0.36f, deck, -1.62f), new Vector3(0.5f, 0.34f, 0.45f), 5f, 61);
            K.Parcel(m, new Vector3(0.3f, deck, -1.55f), new Vector3(0.45f, 0.3f, 0.5f), -8f, 62);
            K.Parcel(m, new Vector3(-0.3f, deck + 0.34f, -1.6f), new Vector3(0.4f, 0.26f, 0.36f), 14f, 63);
            K.Parcel(m, new Vector3(0.25f, deck, -0.72f), new Vector3(0.6f, 0.38f, 0.5f), 3f, 64);
            K.Parcel(m, new Vector3(-0.36f, deck, -0.84f), new Vector3(0.42f, 0.3f, 0.4f), -10f, 65);
            K.Bedroll(m, new Vector3(0f, deck + 0.13f, 0.2f), 1.1f, 0.13f, 0f, B.Hex("687A63"), 66);

            K.Tailpipe(m, new Vector3(-0.58f, 0.34f, -2.12f));
            rig.Exhaust = new Vector3(-0.58f, 0.34f, -2.16f);
            rig.FourWheels(Track, ZF, ZR, R, 0.3f, K.Cream, K.Chrome, false);
            return rig;
        }

        // --- Saloon -----------------------------------------------------------------------------------

        static Rig Saloon(Color paint, Color vinyl)
        {
            const float R = 0.37f, Track = 0.74f, ArchR = 0.43f, Clear = 0.24f, Belt = 0.93f, Roof = 1.58f, ZF = 1.33f, ZR = -1.31f;
            const float G0 = 0.99f, G1 = 1.43f;

            var rig = new Rig(R);
            B.Model m = rig.Body;
            Func<Vector3, Vector3, Color> body = K.Worn(paint, 71, 0.7f);

            var tub = new Loft
            {
                Back = -2.15f, Front = 2.15f, CapBack = 0.42f, CapFront = 0.4f, Round = 3.4f, CapRound = 2.6f,
                Section = z => new Vector3(Arch(z, Clear, R, ArchR, ZF, ZR),
                                           Belt + 0.03f * K.Smooth((-z - 0.9f) / 0.5f) - 0.13f * K.Smooth((z - 0.75f) / 1.1f),
                                           0.86f + 0.03f * Fenders(z, 0.62f, ZF, ZR)),
                Tuck = y => 1f - 0.09f * Sq(Mathf.Max(0f, y)) - 0.07f * Sq(Mathf.Max(0f, -y)),
            };
            tub.Build(m, body, 56, 72);

            // The cabin, glass raked at both ends: a sloping back window over the boot.
            var cabin = new Loft
            {
                Back = -1.42f, Front = 1.02f, CapBack = 0.5f, CapFront = 0.52f, Round = 3f, CapRound = 2.4f,
                Section = z => new Vector3(0.56f, Roof, 0.76f),
                Tuck = y => 1f - 0.18f * Mathf.Pow(Mathf.Max(0f, y), 1.5f),
            };
            cabin.Warp = Rake(Belt, Roof, -0.2f, cabin.Front, cabin.Back, 0.36f, 0.34f);
            cabin.Build(m, K.Worn(paint, 72, 0.4f), 48, 48);
            RoofPanel(cabin, m, vinyl, G1 + 0.035f, G1 + 0.03f, G1 + 0.03f, 73);

            Pane(cabin, m, cabin.EndArea(true, G0, G1, 0.84f));
            Pane(cabin, m, cabin.EndArea(false, G0 + 0.02f, G1, 0.8f));
            for (int s = -1; s <= 1; s += 2) Pane(cabin, m, cabin.SideArea(s, -0.86f, -0.24f, G0, G1));
            Pane(cabin, m, cabin.SideArea(-1f, -0.12f, 0.46f, G0, G1));

            B.Model door = CarDoor(rig, tub, cabin, 1f, -0.16f, 0.84f, 0.31f, 0.85f, -0.12f, 0.46f, G0, G1, body);
            FixedDoor(tub, m, -1f, -0.16f, 0.84f, 0.31f, 0.85f, body);
            for (int s = -1; s <= 1; s += 2) FixedDoor(tub, m, s, -0.86f, -0.2f, 0.31f, 0.85f, body, handleAtFront: true);

            // Chrome along each side - the run over the opening door goes with it.
            TrimLine(tub, m, -1f, -1.95f, 1.95f, 0.7f);
            TrimLine(tub, m, 1f, -1.95f, -0.2f, 0.7f);
            TrimLine(tub, m, 1f, 0.88f, 1.95f, 0.7f);
            TrimLine(tub, door, 1f, -0.12f, 0.8f, 0.7f, 0.036f);

            for (int s = -1; s <= 1; s += 2)
            {
                EndLamp(m, tub, true, s * 0.56f, 0.68f, 0.125f, K.Lens, K.Chrome);
                EndLamp(m, tub, true, s * 0.76f, 0.57f, 0.045f, K.Indicator, K.Chrome);
                K.Mirror(m, OnSide(tub, 0.86f, Belt - 0.04f, s), s, K.Chrome);
            }

            Grille(m, tub, 0.5f, 0.6f, 0.36f, 0.015f, 3);
            Bumper(m, tub, true, 0.5f, 0.07f, K.Chrome);
            Bumper(m, tub, false, 0.56f, 0.07f, K.Chrome);
            TailLamps(m, tub, 0.62f, 0.74f, 0.88f, 0.2f);
            K.NumberPlate(m, OnEnd(tub, 0f, 0.66f, false, 0.01f), Vector3.back, 0.38f);

            // The family's luggage, piled up and tied down.
            float deck = Rack(m, cabin, 0.48f, -0.72f, 0.28f, 3);
            K.Suitcase(m, new Vector3(0f, deck, -0.42f), new Vector3(0.62f, 0.24f, 0.44f), -4f, K.Leather, 81);
            K.Suitcase(m, new Vector3(0.04f, deck + 0.24f, -0.44f), new Vector3(0.48f, 0.2f, 0.34f), 9f, B.Hex("4F6F6E"), 82);
            K.Bedroll(m, new Vector3(0f, deck + 0.12f, 0.12f), 0.8f, 0.12f, 2f, B.Hex("6F7A52"), 83);
            K.Holdall(m, new Vector3(0.3f, deck + 0.24f, -0.05f), new Vector3(0.36f, 0.2f, 0.24f), -25f, B.Hex("8A8462"), 84);

            K.Tailpipe(m, new Vector3(0.45f, 0.27f, -2.08f));
            rig.Exhaust = new Vector3(0.45f, 0.27f, -2.12f);
            rig.FourWheels(Track, ZF, ZR, R, 0.27f, K.Cream, K.Chrome, true);
            return rig;
        }

        // --- Shuttle ----------------------------------------------------------------------------------

        static Rig Shuttle(Color paint, Color cream)
        {
            const float R = 0.5f, Track = 0.84f, ArchR = 0.56f, Clear = 0.34f, Belt = 1.22f, Roof = 2.78f, ZF = 2.09f, ZR = -2.15f;
            const float G0 = 1.5f, G1 = 2.3f;
            Color red = B.Hex("8E3B33");

            var rig = new Rig(R) { DoorsOpenWhileParked = true, IdleWhileParked = true };
            B.Model m = rig.Body;
            Func<Vector3, Vector3, Color> body = K.Worn(paint, 91, 0.7f);

            var tub = new Loft
            {
                Back = -3f, Front = 3f, CapBack = 0.3f, CapFront = 0.36f, Round = 3.8f, CapRound = 2.6f,
                Section = z => new Vector3(Arch(z, Clear, R, ArchR, ZF, ZR), Belt - 0.07f * K.Smooth((z - 2.2f) / 0.44f),
                                           1.06f + 0.025f * Fenders(z, 0.75f, ZF, ZR)),
                Tuck = y => 1f - 0.06f * Sq(Mathf.Max(0f, y)) - 0.06f * Sq(Mathf.Max(0f, -y)),
            };
            tub.Build(m, body, 56, 96);

            var cabin = new Loft
            {
                Back = -2.96f, Front = 2.92f, CapBack = 0.3f, CapFront = 0.44f, Round = 4.4f, CapRound = 2.6f,
                Section = z => new Vector3(0.95f, Roof, 1.03f),
                Tuck = y => 1f - 0.07f * Sq(Mathf.Max(0f, y)),
            };
            cabin.Warp = Rake(Belt, Roof, 1.6f, cabin.Front, cabin.Back, 0.18f, 0f);
            cabin.Build(m, K.Worn(paint, 92, 0.6f), 56, 80);
            RoofPanel(cabin, m, cream, 2.42f, 2.64f, 2.36f, 93, 60);

            // A row of windows down each side; on the right the fourth is the door.
            float[][] windows = { new[] { -2.52f, -1.66f }, new[] { -1.5f, -0.64f }, new[] { -0.48f, 0.38f }, new[] { 0.54f, 1.4f }, new[] { 1.56f, 2.38f } };
            for (int s = -1; s <= 1; s += 2)
            {
                for (int w = 0; w < windows.Length; w++)
                {
                    if (s > 0 && w == 3) continue;
                    Pane(cabin, m, cabin.SideArea(s, windows[w][0], windows[w][1], G0, G1), 12, 8);
                }

                // The cream band under the windows, broken by the door on the right.
                if (s < 0) Decal(cabin, m, cabin.SideArea(s, -2.6f, 2.44f, 1.27f, 1.42f), cream, 0.004f, 0f, 48, 2);
                else
                {
                    Decal(cabin, m, cabin.SideArea(s, -2.6f, 0.48f, 1.27f, 1.42f), cream, 0.004f, 0f, 30, 2);
                    Decal(cabin, m, cabin.SideArea(s, 1.46f, 2.44f, 1.27f, 1.42f), cream, 0.004f, 0f, 10, 2);
                }
            }

            Pane(cabin, m, cabin.EndArea(true, 1.32f, 2.38f, 0.9f), 18, 10);
            Pane(cabin, m, cabin.EndArea(false, 1.62f, 2.3f, 0.7f), 14, 8);

            // Folding glass doors on the kerb side: two leaves, hinged at their outer edges, opening outward.
            {
                Opening(tub, m, tub.SideArea(1f, 0.54f, 1.4f, 0.5f, 1.17f));
                Opening(cabin, m, cabin.SideArea(1f, 0.54f, 1.4f, 1.26f, 2.34f));

                (float z0, float z1, float hinge, float angle)[] leaves = { (0.52f, 0.96f, 0.5f, 82f), (0.98f, 1.42f, 1.44f, -82f) };
                foreach (var (z0, z1, hingeZ, angle) in leaves)
                {
                    Vector3 edge = OnSide(tub, hingeZ, 1f, 1f);
                    B.Model leaf = rig.AddDoor(new Vector3(edge.x + 0.025f, 1.2f, hingeZ), angle);
                    Panel(tub, leaf, tub.SideArea(1f, z0, z1, 0.46f, 1.2f), body);
                    Panel(cabin, leaf, cabin.SideArea(1f, z0, z1, 1.24f, 2.36f), body);
                    DoorGlass(cabin, leaf, cabin.SideArea(1f, z0 + 0.05f, z1 - 0.05f, 1.34f, 2.26f), 0.02f);
                    DoorGlass(tub, leaf, tub.SideArea(1f, z0 + 0.05f, z1 - 0.05f, 0.62f, 1.1f), 0.02f);
                }

                rig.DoorSide = 1f;
                m.Box(K.Trim, new Vector3(1.0f, 0.36f, 0.97f), new Vector3(0.24f, 0.06f, 0.88f), 0.02f, 2);
            }

            // A sign over the windscreen and a red cross on the roof: it is the hospital's.
            Decal(cabin, m, cabin.EndArea(true, 2.44f, 2.62f, 0f, 0f, 1.1f), K.Interior, 0.006f, 6f, 14, 4);
            Decal(cabin, m, cabin.EndArea(true, 2.47f, 2.59f, 0f, 0f, 0.98f), K.Cream, 0.01f, 6f, 14, 4);
            Decal(cabin, m, cabin.EndArea(true, 2.485f, 2.575f, 0f, 0f, 0.035f), red, 0.014f, 0f, 2, 4);
            Decal(cabin, m, cabin.EndArea(true, 2.515f, 2.545f, 0f, 0f, 0.09f), red, 0.014f, 0f, 4, 2);

            Decal(cabin, m, cabin.TopArea(-0.13f, 0.13f, 0.2f, 1f), red, 0.014f, 6f, 6, 12);
            Decal(cabin, m, cabin.TopArea(-0.4f, 0.4f, 0.47f, 0.73f), red, 0.016f, 6f, 12, 6);

            for (int s = -1; s <= 1; s += 2)
            {
                EndLamp(m, tub, true, s * 0.72f, 0.9f, 0.16f, K.Lens, K.Chrome);
                EndLamp(m, tub, true, s * 0.92f, 0.76f, 0.05f, K.Indicator, K.Chrome);

                // Bus mirrors on long arms.
                Vector3 root = OnSide(cabin, 2.3f, 1.62f, s);
                Vector3 elbow = root + new Vector3(s * 0.22f, 0.12f, 0.12f);
                m.Tube(K.Trim, root, elbow, 0.018f, 8);
                K.Mirror(m, elbow, s, K.Trim);
            }

            Grille(m, tub, 0.7f, 0.8f, 0.42f, 0.02f, 2);
            Bumper(m, tub, true, 0.66f, 0.1f, K.BumperGrey);
            Bumper(m, tub, false, 0.68f, 0.1f, K.BumperGrey);
            TailLamps(m, tub, 0.8f, 0.95f, 1.13f, 0.14f);
            K.NumberPlate(m, OnEnd(tub, 0f, 0.86f, false, 0.01f), Vector3.back, 0.4f);

            // Bags on the rack at the back for the people going home.
            float deck = Rack(m, cabin, 0.78f, -2.55f, -0.75f, 4);
            K.Suitcase(m, new Vector3(-0.35f, deck, -2.1f), new Vector3(0.6f, 0.26f, 0.44f), 6f, K.Leather, 101);
            K.Suitcase(m, new Vector3(0.38f, deck, -2.05f), new Vector3(0.52f, 0.22f, 0.4f), -9f, B.Hex("4F6F6E"), 102);
            K.Bedroll(m, new Vector3(0f, deck + 0.13f, -1.45f), 1.2f, 0.13f, 0f, B.Hex("7A6E58"), 103);
            K.Holdall(m, new Vector3(0.32f, deck, -1.0f), new Vector3(0.5f, 0.26f, 0.3f), 12f, B.Hex("6F7A52"), 104);
            K.SpareTyre(m, new Vector3(-0.35f, deck, -1.0f), 0.3f, 0.18f, K.Cream);

            K.Tailpipe(m, new Vector3(-0.7f, 0.4f, -2.92f));
            rig.Exhaust = new Vector3(-0.7f, 0.4f, -2.96f);
            rig.FourWheels(Track, ZF, ZR, R, 0.34f, K.Cream, K.Chrome, false);
            return rig;
        }
    }
}
