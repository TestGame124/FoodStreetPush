using System.Collections.Generic;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using D = LastOrders.EditorTools.DiscoveryKit;
using K = FoodStreet.Visitors.EditorTools.VehicleKit;
using S = LastOrders.EditorTools.SoupKitchenModel;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The abandoned security hut out on the east field: a gatehouse of concrete blocks laid in
    /// courses up to the sill, a band of big windows all round in a steel frame, a thick flat roof
    /// with a gutter and a downpipe, a siren with two horns and a beacon on the roof, a floodlight
    /// mast and a camera on a bracket. A sandbagged post in front, a barrier post beside it for the
    /// old access road. Origin on the ground in the middle; the windows' front faces -Z, the door is
    /// on the +X side.
    ///
    /// The noise maker found in it is its own model: a site siren on a tripod with a car battery
    /// to run it, made to be set down anywhere out past the walls.
    /// </summary>
    public static class SecurityHutModel
    {
        public const float Width = 2.6f, Depth = 2.2f;
        const float Plinth = 0.18f, Sill = 1.0f, Head = 2.05f, Top = 2.22f;

        static float HX => Width * 0.5f;
        static float HZ => Depth * 0.5f;

        /// <summary>Where the buttons float.</summary>
        public static readonly Vector3 Marker = new Vector3(0f, 4.1f, 0f);

        /// <summary>The barrier's pivot, and the length of its arm.</summary>
        public static readonly Vector3 BarrierPivot = new Vector3(-HX - 0.75f, 0.95f, -HZ - 0.9f);
        const float ArmLength = 3.4f;

        static readonly Color Block = B.Hex("8A8780");
        static readonly Color Frame = B.Hex("4D5458");
        static readonly Color Header = D.TealDark;
        static readonly Color RoofTop = B.Hex("7A7C7F");
        static readonly Color Beacon = B.Hex("C2662F");

        static float Shade(Vector3 p, Vector3 n)
        {
            float k = Mathf.Lerp(0.74f, 1f, S.Smooth(Mathf.Clamp01(p.y / 0.5f)));
            bool under = Mathf.Abs(n.y) < 0.5f && Mathf.Abs(p.x) < HX + 0.1f && Mathf.Abs(p.z) < HZ + 0.1f && p.y < Top;
            if (under) k *= Mathf.Lerp(1f, 0.84f, S.Smooth(Mathf.Clamp01((p.y - (Top - 0.35f)) / 0.35f)));
            if (n.y < -0.5f) k *= 0.82f;
            return k;
        }

        // --- The hut ----------------------------------------------------------------------------------

        public static B.Model Hut()
        {
            var m = new B.Model { Shade = Shade };

            m.Box(D.Concrete, new Vector3(0f, Plinth * 0.5f, 0f), new Vector3(Width + 0.4f, Plinth, Depth + 0.4f), 0.04f, 2);
            m.Box(D.Concrete, new Vector3(HX + 0.45f, Plinth * 0.4f, 0.35f), new Vector3(0.5f, Plinth * 0.8f, 0.85f), 0.03f, 2);

            Blocks(m);
            Windows(m);
            Door(m);
            Roof(m);
            Siren(m, new Vector3(-0.35f, Top + 0.2f, 0.15f));
            Floodlight(m, new Vector3(HX + 0.25f, 0f, -HZ - 0.15f));
            Camera(m, new Vector3(-HX - 0.05f, Head + 0.02f, -HZ - 0.05f));
            SandbagPost(m);
            BarrierPost(m);

            // Conduit from the roof down the back corner into a fuse box.
            K.Sweep(m, (p, n) => D.SteelDark, new[]
            {
                new Vector3(HX - 0.2f, Top + 0.05f, HZ + 0.12f), new Vector3(HX - 0.2f, Top - 0.1f, HZ + 0.06f), new Vector3(HX - 0.2f, 1.45f, HZ + 0.06f),
            }, 0.022f, 6, 4);
            m.Box(D.Steel, new Vector3(HX - 0.2f, 1.25f, HZ + 0.1f), new Vector3(0.32f, 0.42f, 0.14f), 0.025f, 2);
            m.Box(D.Mustard, new Vector3(HX - 0.2f, 1.3f, HZ + 0.175f), new Vector3(0.1f, 0.08f, 0.01f), 0.004f);
            return m;
        }

        /// <summary>The block wall to the sill: courses of blocks with the joints staggered, each block its own shade.</summary>
        static void Blocks(B.Model m)
        {
            const int Courses = 4;
            const float BlockLength = 0.42f;
            float course = (Sill - Plinth) / Courses;

            void Run(Vector3 origin, Vector3 along, Vector3 outward, float length, int key)
            {
                Quaternion turn = Quaternion.LookRotation(outward, Vector3.up);
                for (int c = 0; c < Courses; c++)
                {
                    float offset = c % 2 == 0 ? 0f : BlockLength * 0.5f;
                    for (float x = -offset; x < length; x += BlockLength)
                    {
                        float a = Mathf.Max(0f, x), b = Mathf.Min(length, x + BlockLength);
                        if (b - a < 0.05f) continue;

                        Color shade = Block * (1f + 0.06f * B.Jitter(key * 97 + c * 13 + (int)(x * 10f)));
                        Vector3 centre = origin + along * ((a + b) * 0.5f) + Vector3.up * (Plinth + course * (c + 0.5f)) + outward * 0.005f;
                        m.Box(shade, centre, new Vector3(b - a - 0.012f, course - 0.012f, 0.2f), 0.02f, turn, 2);
                    }
                }
            }

            Run(new Vector3(-HX, 0f, -HZ + 0.1f), Vector3.right, Vector3.back, Width, 1);
            Run(new Vector3(-HX, 0f, HZ - 0.1f), Vector3.right, Vector3.forward, Width, 2);
            Run(new Vector3(-HX + 0.1f, 0f, -HZ), Vector3.forward, Vector3.left, Depth, 3);
            Run(new Vector3(HX - 0.1f, 0f, -HZ), Vector3.forward, Vector3.right, 0.95f, 4);
            Run(new Vector3(HX - 0.1f, 0f, -HZ + 1.75f), Vector3.forward, Vector3.right, Depth - 1.75f, 5);

            // A concrete sill along the top of the blocks, all round.
            m.Box(D.Concrete, new Vector3(0f, Sill + 0.03f, -HZ + 0.02f), new Vector3(Width + 0.14f, 0.06f, 0.3f), 0.02f, 2);
            m.Box(D.Concrete, new Vector3(0f, Sill + 0.03f, HZ - 0.02f), new Vector3(Width + 0.14f, 0.06f, 0.3f), 0.02f, 2);
            m.Box(D.Concrete, new Vector3(-HX + 0.02f, Sill + 0.03f, 0f), new Vector3(0.3f, 0.06f, Depth), 0.02f, 2);
            m.Box(D.Concrete, new Vector3(HX - 0.02f, Sill + 0.03f, -HZ + 0.48f), new Vector3(0.3f, 0.06f, 0.96f), 0.02f, 2);
        }

        /// <summary>The window band: steel corner posts and mullions, a pane in each bay, the teal header over it all.</summary>
        static void Windows(B.Model m)
        {
            float y0 = Sill + 0.06f, y1 = Head;

            // Front and back: three bays; the left end: two; the door side: one bay ahead of the door.
            Bays(m, new Vector3(-HX, 0f, -HZ), Vector3.right, Vector3.back, Width, 3, y0, y1);
            Bays(m, new Vector3(-HX, 0f, HZ), Vector3.right, Vector3.forward, Width, 3, y0, y1);
            Bays(m, new Vector3(-HX, 0f, -HZ), Vector3.forward, Vector3.left, Depth, 2, y0, y1);
            Bays(m, new Vector3(HX, 0f, -HZ), Vector3.forward, Vector3.right, 0.95f, 1, y0, y1);

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                m.Box(Frame, new Vector3(sx * HX, (Plinth + Top) * 0.5f, sz * HZ), new Vector3(0.12f, Top - Plinth, 0.12f), 0.025f, 2);

            // The header band, proud, all the way round.
            m.Box(Header, new Vector3(0f, (Head + Top) * 0.5f, -HZ), new Vector3(Width + 0.06f, Top - Head, 0.16f), 0.025f, 2);
            m.Box(Header, new Vector3(0f, (Head + Top) * 0.5f, HZ), new Vector3(Width + 0.06f, Top - Head, 0.16f), 0.025f, 2);
            m.Box(Header, new Vector3(-HX, (Head + Top) * 0.5f, 0f), new Vector3(0.16f, Top - Head, Depth + 0.06f), 0.025f, 2);
            m.Box(Header, new Vector3(HX, (Head + Top) * 0.5f, 0f), new Vector3(0.16f, Top - Head, Depth + 0.06f), 0.025f, 2);

            // The dark inside, behind the glass: a desk with an old monitor on it against the back window.
            m.Box(B.Hex("2A2725"), new Vector3(0f, (Sill + Head) * 0.5f, 0f), new Vector3(Width - 0.3f, Head - Sill, Depth - 0.3f), 0.02f);
            m.Box(B.Walnut, new Vector3(-0.3f, Sill + 0.02f, HZ - 0.45f), new Vector3(1.4f, 0.05f, 0.55f), 0.012f, 2);
            m.Box(B.Hex("C9C2AE"), new Vector3(-0.5f, Sill + 0.22f, HZ - 0.42f), new Vector3(0.42f, 0.34f, 0.36f), 0.04f, 2);
        }

        /// <summary>
        /// Panes in a frame along one side: <paramref name="bays"/> of them between mullions, each pane
        /// set back in its frame with a sill bar under it, so the band reads as glass in steel, not paint.
        /// </summary>
        static void Bays(B.Model m, Vector3 origin, Vector3 along, Vector3 outward, float length, int bays, float y0, float y1)
        {
            float bay = length / bays;
            Quaternion turn = Quaternion.LookRotation(outward, Vector3.up);

            for (int i = 0; i < bays; i++)
            {
                float a = i * bay, b = a + bay;
                Vector3 centre = origin + along * ((a + b) * 0.5f) + Vector3.up * ((y0 + y1) * 0.5f) + outward * 0.01f;
                Vector3 across = along * (b - a - 0.1f), up = Vector3.up * (y1 - y0 - 0.08f);

                m.Surface(K.Glazing(centre, across, up), 6, 6, (u, v) => centre + across * (u - 0.5f) + up * (v - 0.5f) + outward * 0.002f);

                if (i > 0)
                    m.Box(Frame, origin + along * a + Vector3.up * ((y0 + y1) * 0.5f) + outward * 0.03f, new Vector3(0.07f, y1 - y0, 0.08f), 0.015f, turn, 2);
            }

            m.Box(Frame, origin + along * (length * 0.5f) + Vector3.up * (y0 + 0.02f) + outward * 0.04f, new Vector3(length, 0.05f, 0.08f), 0.015f, turn, 2);
            m.Box(Frame, origin + along * (length * 0.5f) + Vector3.up * (y1 - 0.02f) + outward * 0.03f, new Vector3(length, 0.05f, 0.07f), 0.015f, turn, 2);
        }

        /// <summary>The door on the +X side: a steel door with a wired-glass window, a lever handle, a kick plate.</summary>
        static void Door(B.Model m)
        {
            float Z0 = -HZ + 0.95f, Z1 = -HZ + 1.75f;
            float z = (Z0 + Z1) * 0.5f, x = HX + 0.02f;
            Quaternion turn = Quaternion.LookRotation(Vector3.right, Vector3.up);

            m.Box(Frame, new Vector3(x, (Plinth + Head) * 0.5f, z), new Vector3(0.06f, Head - Plinth, Z1 - Z0 + 0.04f), 0.015f, 2);
            m.Box(D.Teal, new Vector3(x + 0.04f, (Plinth + Head) * 0.5f, z), new Vector3(0.05f, Head - Plinth - 0.08f, Z1 - Z0 - 0.08f), 0.02f, 2);

            var glass = new Vector3(x + 0.07f, 1.55f, z);
            m.Surface(K.Glazing(glass, Vector3.forward * 0.4f, Vector3.up * 0.42f), 4, 4,
                      (u, v) => glass + new Vector3(0f, (v - 0.5f) * 0.42f, (0.5f - u) * 0.4f));
            m.Box(Frame, glass + new Vector3(0.005f, 0f, 0f), new Vector3(0.02f, 0.5f, 0.48f), 0.008f);
            m.Box(D.Steel, new Vector3(x + 0.07f, 0.35f, z), new Vector3(0.012f, 0.3f, Z1 - Z0 - 0.16f), 0.006f);

            m.Box(K.Chrome, new Vector3(x + 0.1f, 1.05f, Z0 + 0.14f), new Vector3(0.04f, 0.04f, 0.18f), 0.015f, turn, 2);
            m.Tube(K.Chrome, new Vector3(x + 0.07f, 1.05f, Z0 + 0.06f), new Vector3(x + 0.1f, 1.05f, Z0 + 0.06f), 0.025f, 8);
        }

        /// <summary>The roof: a thick slab with a deep overhang, a gutter along the front and a downpipe at its corner.</summary>
        static void Roof(B.Model m)
        {
            const float Over = 0.32f, Thick = 0.18f;
            float y = Top + Thick * 0.5f;
            m.Box(RoofTop, new Vector3(0f, y, 0f), new Vector3(Width + Over * 2f, Thick, Depth + Over * 2f), 0.05f, 3);
            m.Box(Header, new Vector3(0f, y - 0.02f, 0f), new Vector3(Width + Over * 2f + 0.04f, 0.1f, Depth + Over * 2f + 0.04f), 0.03f, 2);

            // The gutter: a half-round trough along the front edge, and a downpipe from its end.
            float gz = -HZ - Over - 0.08f, gy = Top + 0.02f;
            m.Surface((p, n) => D.SteelDark * (n.y > 0.3f ? 0.7f : 1f), 18, 6, (u, v) =>
            {
                float a = Mathf.PI * (0.05f + 0.9f * v);
                return new Vector3(Mathf.Lerp(-HX - Over, HX + Over, u), gy - Mathf.Sin(a) * 0.07f, gz + Mathf.Cos(a) * 0.07f);
            });
            m.Surface((p, n) => D.SteelDark, 6, 18, (v, u) =>
            {
                float a = Mathf.PI * (0.05f + 0.9f * v);
                return new Vector3(Mathf.Lerp(-HX - Over, HX + Over, u), gy - Mathf.Sin(a) * 0.075f, gz + Mathf.Cos(a) * 0.075f);
            });
            K.Sweep(m, (p, n) => D.SteelDark, new[]
            {
                new Vector3(-HX - Over + 0.1f, gy - 0.07f, gz), new Vector3(-HX - Over + 0.1f, gy - 0.2f, gz + 0.1f),
                new Vector3(-HX - 0.05f, gy - 0.35f, -HZ - 0.1f), new Vector3(-HX - 0.05f, Plinth + 0.1f, -HZ - 0.1f),
                new Vector3(-HX - 0.15f, Plinth, -HZ - 0.2f),
            }, 0.035f, 8, 4);
        }

        /// <summary>
        /// The roof siren: a mast with two horn speakers pointing opposite ways off a motor drum, and a
        /// beacon dome on top of it in a cage.
        /// </summary>
        static void Siren(B.Model m, Vector3 foot)
        {
            m.Box(D.SteelDark, foot + Vector3.up * 0.02f, new Vector3(0.36f, 0.04f, 0.36f), 0.015f, 2);
            m.Tube(D.SteelDark, foot, foot + Vector3.up * 0.75f, 0.04f, 10);

            Vector3 drum = foot + Vector3.up * 0.75f;
            m.Lathe((p, n) => n.y > 0.8f ? D.Steel * 1.05f : D.Steel, drum, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.12f, 0f), new Vector2(0.13f, 0.02f), new Vector2(0.13f, 0.16f), new Vector2(0.11f, 0.18f),
                new Vector2(0f, 0.18f),
            }, 16);

            for (int s = -1; s <= 1; s += 2)
                Horn(m, drum + Vector3.up * 0.09f, new Vector3(s, 0f, 0.25f * s).normalized, 0.42f, 0.17f);

            // The beacon: a dome in a little wire cage.
            Vector3 beacon = drum + Vector3.up * 0.18f;
            m.Lathe((p, n) => B.Iron, beacon, new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.1f, 0f), new Vector2(0.1f, 0.03f), new Vector2(0f, 0.03f) }, 14);
            BeaconDome(m, beacon + Vector3.up * 0.03f, 0.085f, sub: B.Model.Painted, Beacon);
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f + 0.4f;
                var off = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.1f;
                K.Sweep(m, (p, n) => B.Iron, new[] { beacon + off + Vector3.up * 0.03f, beacon + off * 1.05f + Vector3.up * 0.12f, beacon + Vector3.up * 0.2f },
                        0.006f, 5, 3);
            }
        }

        /// <summary>A horn speaker: a throat flaring out to a wide mouth, with a rolled rim and a dark inside.</summary>
        public static void Horn(B.Model m, Vector3 throat, Vector3 direction, float length, float mouth)
        {
            Quaternion turn = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            Matrix4x4 was = m.Transform;
            m.Transform = was * Matrix4x4.TRS(throat, turn, Vector3.one);
            try
            {
                var profile = new List<Vector2> { new Vector2(0f, -0.02f), new Vector2(0.05f, -0.02f), new Vector2(0.05f, 0.04f) };
                for (int i = 1; i <= 8; i++)
                {
                    float t = i / 8f;
                    profile.Add(new Vector2(Mathf.Lerp(0.05f, mouth, t * t), 0.04f + length * t));
                }

                profile.Add(new Vector2(mouth + 0.02f, length + 0.04f));
                profile.Add(new Vector2(mouth + 0.02f, length + 0.06f));
                profile.Add(new Vector2(mouth - 0.01f, length + 0.06f));
                m.Lathe((p, n) => D.CreamLight * (n.y < -0.3f ? 0.8f : 1f), Vector3.zero, profile, 16);

                // The dark of the mouth, and the driver's dust cap in it.
                m.Lathe((p, n) => B.Hex("1F1F21"), Vector3.zero, new List<Vector2>
                {
                    new Vector2(mouth - 0.012f, length + 0.04f), new Vector2(0.05f, length * 0.3f), new Vector2(0f, length * 0.25f),
                }, 16);
            }
            finally
            {
                m.Transform = was;
            }
        }

        /// <summary>A beacon dome on a short collar.</summary>
        public static void BeaconDome(B.Model m, Vector3 foot, float radius, int sub, Color colour)
        {
            var profile = new List<Vector2> { new Vector2(0f, 0f), new Vector2(radius, 0f), new Vector2(radius, 0.04f) };
            for (int i = 1; i <= 6; i++)
            {
                float a = i / 6f * Mathf.PI * 0.5f;
                profile.Add(new Vector2(radius * Mathf.Cos(a), 0.04f + radius * 1.1f * Mathf.Sin(a)));
            }

            m.Lathe((p, n) => colour * (1f + 0.15f * Mathf.Max(0f, n.y)), foot, profile, 14, sub);
        }

        /// <summary>A floodlight on a tall pole: a box lamp tipped to throw its light down on the road.</summary>
        static void Floodlight(B.Model m, Vector3 foot)
        {
            m.Box(D.Concrete, foot + Vector3.up * 0.1f, new Vector3(0.36f, 0.2f, 0.36f), 0.04f, 2);
            m.Tube(D.SteelDark, foot, foot + Vector3.up * 3.6f, 0.055f, 10);

            Vector3 head = foot + new Vector3(-0.2f, 3.55f, -0.15f);
            Quaternion tip = Quaternion.Euler(30f, -35f, 0f);
            m.Box(D.SteelDark, head, new Vector3(0.42f, 0.3f, 0.22f), 0.04f, tip, 2);
            m.Box(D.Steel, head + tip * new Vector3(0f, 0f, -0.12f), new Vector3(0.36f, 0.24f, 0.03f), 0.015f, tip, 2);
            K.Sweep(m, (p, n) => D.SteelDark, new[] { foot + Vector3.up * 3.4f, head + Vector3.up * 0.05f }, 0.02f, 6, 2);
        }

        /// <summary>A security camera on a bracket under the roof's corner.</summary>
        static void Camera(B.Model m, Vector3 mount)
        {
            m.Box(D.SteelDark, mount, new Vector3(0.1f, 0.12f, 0.06f), 0.015f, 2);
            Vector3 body = mount + new Vector3(-0.2f, -0.08f, -0.12f);
            Quaternion look = Quaternion.Euler(18f, 215f, 0f);
            K.Sweep(m, (p, n) => D.SteelDark, new[] { mount, mount + new Vector3(-0.12f, -0.02f, -0.05f), body }, 0.018f, 6, 3);
            m.Box(D.CreamLight, body, new Vector3(0.14f, 0.13f, 0.32f), 0.04f, look, 2);
            m.Box(D.CreamLight * 0.9f, body + look * new Vector3(0f, 0.08f, -0.02f), new Vector3(0.18f, 0.02f, 0.38f), 0.008f, look);
            m.Tube(B.Hex("1F1F21"), body + look * new Vector3(0f, 0f, 0.15f), body + look * new Vector3(0f, 0f, 0.19f), 0.04f, 10);
        }

        /// <summary>A curved wall of sandbags in front of the windows, two high.</summary>
        static void SandbagPost(B.Model m)
        {
            for (int layer = 0; layer < 2; layer++)
            {
                for (int k = 0; k < 6; k++)
                {
                    float a = Mathf.Lerp(-0.85f, 0.85f, (k + (layer == 1 ? 0.5f : 0f)) / 5f);
                    if (layer == 1 && k == 5) continue;
                    var at = new Vector3(Mathf.Sin(a) * 1.7f - 0.2f, 0.12f + layer * 0.21f, -HZ - 1.15f + (1f - Mathf.Cos(a)) * 1.2f);
                    D.Sandbag(m, at, -a * Mathf.Rad2Deg, 1f, layer * 10 + k);
                }
            }
        }

        /// <summary>The barrier's post and counterweight box. Its arm comes and goes with how the hut is found.</summary>
        static void BarrierPost(B.Model m)
        {
            Vector3 foot = new Vector3(BarrierPivot.x, 0f, BarrierPivot.z);
            m.Box(D.Concrete, foot + Vector3.up * 0.06f, new Vector3(0.5f, 0.12f, 0.5f), 0.04f, 2);
            m.Box(D.Red, foot + Vector3.up * 0.5f, new Vector3(0.28f, 0.8f, 0.28f), 0.05f, 2);
            m.Box(D.CreamLight, foot + Vector3.up * 0.62f, new Vector3(0.3f, 0.12f, 0.3f), 0.04f, 2);
            m.Tube(D.SteelDark, BarrierPivot + Vector3.back * 0.17f, BarrierPivot + Vector3.forward * 0.17f, 0.05f, 10);
        }

        /// <summary>
        /// The barrier's arm from <paramref name="pivot"/>, raised <paramref name="raise"/> degrees: a striped
        /// boom, a counterweight on its short end, a fork post where its tip rests when down.
        /// </summary>
        static void BarrierArm(B.Model m, Vector3 pivot, float raise, bool broken)
        {
            Quaternion turn = Quaternion.Euler(0f, 0f, raise);
            float length = broken ? ArmLength * 0.55f : ArmLength;
            const int Stripes = 7;
            float stripe = ArmLength / Stripes;

            for (int i = 0; i < Stripes; i++)
            {
                float a = i * stripe, b = Mathf.Min(length, a + stripe);
                if (b - a < 0.05f) break;
                Color c = i % 2 == 0 ? D.Red : D.CreamLight;
                m.Box(c, pivot + turn * new Vector3((a + b) * 0.5f + 0.15f, 0f, 0f), new Vector3(b - a + 0.004f, 0.11f, 0.08f), 0.035f, turn, 2);
            }

            m.Box(D.SteelDark, pivot + turn * new Vector3(-0.3f, 0f, 0f), new Vector3(0.36f, 0.2f, 0.14f), 0.04f, turn, 2);
        }

        // --- How it was found ---------------------------------------------------------------------------

        public static B.Model Derelict()
        {
            var m = new B.Model { Shade = Shade };

            // Smashed panes: a dark hole with shards round its edge, in two of the front bays and one at the end.
            Smashed(m, new Vector3(-HX + Width / 6f, (Sill + Head) * 0.5f + 0.05f, -HZ - 0.02f), Vector3.right, Vector3.back, 0.3f, 1);
            Smashed(m, new Vector3(HX - Width / 6f, (Sill + Head) * 0.5f - 0.1f, -HZ - 0.02f), Vector3.right, Vector3.back, 0.24f, 2);
            Smashed(m, new Vector3(-HX - 0.02f, (Sill + Head) * 0.5f, -HZ + Depth * 0.25f), Vector3.forward, Vector3.left, 0.27f, 3);

            // The middle front bay boarded up.
            float z = -HZ - 0.05f;
            D.NailedPlank(m, new Vector3(-0.42f, Sill + 0.2f, z), new Vector3(0.42f, Sill + 0.32f, z), Vector3.back, 0.16f, 11);
            D.NailedPlank(m, new Vector3(-0.42f, Sill + 0.6f, z), new Vector3(0.42f, Sill + 0.55f, z), Vector3.back, 0.16f, 12);
            D.NailedPlank(m, new Vector3(-0.4f, Head - 0.15f, z), new Vector3(0.4f, Head - 0.22f, z), Vector3.back, 0.15f, 13);

            D.Graffiti(m, new Vector3(-0.3f, 0.6f, -HZ - 0.11f), Vector3.right, Vector3.up, Vector3.back, 0.9f, D.Graffiti2, 21);
            D.Graffiti(m, new Vector3(-HX - 0.11f, 0.55f, 0.2f), Vector3.forward, Vector3.up, Vector3.left, 0.7f, D.Graffiti1, 22);

            // The barrier arm snapped and lying in the grass, the sign hanging off one post.
            BarrierArm(m, new Vector3(BarrierPivot.x + 0.4f, 0.06f, BarrierPivot.z - 0.6f), 2f, broken: true);
            Vector3 tip = new Vector3(BarrierPivot.x + 2.6f, 0.06f, BarrierPivot.z - 0.2f);
            m.Box(D.CreamLight, tip, new Vector3(1.1f, 0.11f, 0.08f), 0.035f, Quaternion.Euler(0f, 24f, 90f + 2f) * Quaternion.Euler(0f, 0f, -90f), 2);

            Sign(m, tilt: 24f);

            // Litter: an oil drum, papers, a chair on its back; weeds all round.
            Vector3 drum = new Vector3(HX + 0.7f, 0f, HZ + 0.4f);
            m.Lathe((p, n) => B.Rust * (0.85f + 0.15f * K.Noise(p * 6f)), drum, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.27f, 0f), new Vector2(0.28f, 0.03f), new Vector2(0.28f, 0.28f), new Vector2(0.3f, 0.3f),
                new Vector2(0.28f, 0.32f), new Vector2(0.28f, 0.6f), new Vector2(0.3f, 0.62f), new Vector2(0.28f, 0.64f), new Vector2(0.28f, 0.84f),
                new Vector2(0.26f, 0.86f), new Vector2(0f, 0.86f),
            }, 16);
            for (int k = 0; k < 5; k++)
                m.Box(B.Hex("CFC9B8"), new Vector3(-1.2f + k * 0.35f, 0.012f, -HZ - 0.5f - 0.2f * B.Jitter(k)), new Vector3(0.21f, 0.008f, 0.29f), 0.002f,
                      Quaternion.Euler(0f, 40f * B.Jitter(k + 9), 0f));

            for (int k = 0; k < 10; k++)
            {
                float a = k * 0.63f;
                D.WeedClump(m, new Vector3(Mathf.Cos(a) * (HX + 0.32f), 0f, Mathf.Sin(a) * (HZ + 0.3f)), 0.22f, 9, 90 + k);
            }

            D.Tyre(m, new Vector3(-HX - 0.9f, 0.11f, HZ + 0.3f), Vector3.up, 0.36f, 0.22f);
            D.Tyre(m, new Vector3(-HX - 0.9f, 0.33f, HZ + 0.3f), Vector3.up, 0.36f, 0.22f);
            return m;
        }

        /// <summary>A smashed pane: the dark behind it showing through a jagged hole, splinters of glass round the edge.</summary>
        static void Smashed(B.Model m, Vector3 centre, Vector3 along, Vector3 outward, float radius, int key)
        {
            Quaternion turn = Quaternion.LookRotation(outward, Vector3.up);
            const int Points = 11;

            m.Surface((p, n) => B.Hex("191817"), Points, 1, (u, f) =>
            {
                float a = u * Mathf.PI * 2f;
                float r = radius * (0.65f + 0.35f * Mathf.Abs(B.Jitter(key * 31 + Mathf.RoundToInt(u * Points))));
                Vector3 rim = centre + along * (Mathf.Cos(a) * r) + Vector3.up * (Mathf.Sin(a) * r * 0.85f) + outward * 0.006f;
                return Vector3.Lerp(rim, centre + outward * 0.006f, f);
            }, true);

            for (int k = 0; k < 7; k++)
            {
                float a = k * Mathf.PI * 2f / 7f + B.Jitter(key + k);
                Vector3 at = centre + along * (Mathf.Cos(a) * radius * 0.85f) + Vector3.up * (Mathf.Sin(a) * radius * 0.75f) + outward * 0.012f;
                m.Box(B.GlassSky, at, new Vector3(0.05f, 0.11f, 0.008f), 0.003f, turn * Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f));
            }
        }

        /// <summary>The hut's sign over the front windows: a red board with a raised gold shield, on two short posts on the roof edge.</summary>
        static void Sign(B.Model m, float tilt)
        {
            Vector3 left = new Vector3(-0.7f, Top + 0.18f, -HZ - 0.15f), right = new Vector3(0.7f, Top + 0.18f, -HZ - 0.15f);
            m.Tube(D.SteelDark, left, left + Vector3.up * 0.35f, 0.03f, 8);
            m.Tube(D.SteelDark, right, right + Vector3.up * (tilt > 0f ? 0.12f : 0.35f), 0.03f, 8);

            // Hung from its left post: the right end dropped when it has come off the right one.
            Vector3 hang = left + Vector3.up * 0.55f;
            Quaternion turn = Quaternion.Euler(0f, 0f, -tilt);
            Vector3 centre = hang + turn * new Vector3(0.7f, -0.02f, 0f);
            m.Box(D.Red, centre, new Vector3(1.6f, 0.42f, 0.08f), 0.04f, turn, 2);
            m.Box(D.CreamLight, centre + turn * new Vector3(0f, 0f, -0.035f), new Vector3(1.48f, 0.3f, 0.03f), 0.012f, turn, 2);

            // The shield: a rounded block with its point down, a stripe across it.
            Vector3 shield = centre + turn * new Vector3(-0.5f, 0f, -0.07f);
            m.Box(D.Mustard, shield + turn * new Vector3(0f, 0.04f, 0f), new Vector3(0.22f, 0.16f, 0.05f), 0.04f, turn, 2);
            m.Box(D.Mustard, shield + turn * new Vector3(0f, -0.05f, 0f), new Vector3(0.15f, 0.15f, 0.05f), 0.03f, turn * Quaternion.Euler(0f, 0f, 45f), 2);
            m.Box(D.Red * 0.8f, shield + turn * new Vector3(0f, 0.03f, -0.028f), new Vector3(0.05f, 0.18f, 0.01f), 0.004f, turn);

            // Lettering after it: raised blocks of different heights.
            float[] heights = { 0.16f, 0.13f, 0.16f, 0.16f, 0.12f, 0.16f, 0.14f, 0.16f };
            for (int k = 0; k < heights.Length; k++)
                m.Box(D.Red * 0.85f, centre + turn * new Vector3(-0.28f + k * 0.12f, (heights[k] - 0.16f) * 0.5f, -0.055f), new Vector3(0.08f, heights[k], 0.02f),
                      0.008f, turn);
        }

        // --- Back at work ---------------------------------------------------------------------------------

        public static B.Model Restored()
        {
            var m = new B.Model { Shade = Shade };

            Sign(m, tilt: 0f);
            BarrierArm(m, BarrierPivot, 72f, broken: false);

            // The floodlight lit, and a warm light inside two of the windows.
            Vector3 flood = new Vector3(HX + 0.25f, 0f, -HZ - 0.15f) + new Vector3(-0.2f, 3.55f, -0.15f);
            D.GlowBox(m, flood + Quaternion.Euler(30f, -35f, 0f) * new Vector3(0f, 0f, -0.14f), new Vector3(0.32f, 0.2f, 0.012f), Quaternion.Euler(30f, -35f, 0f));
            D.GlowBox(m, new Vector3(0f, (Sill + Head) * 0.5f + 0.03f, HZ + 0.015f), new Vector3(0.7f, 0.85f, 0.008f), Quaternion.identity);
            D.GlowBox(m, new Vector3(-HX - 0.015f, (Sill + Head) * 0.5f + 0.03f, HZ * 0.5f), new Vector3(0.008f, 0.85f, 0.85f), Quaternion.identity);

            // A radio aerial on the roof, and a pot of something green by the door.
            Vector3 aerial = new Vector3(HX - 0.2f, Top + 0.18f, HZ - 0.2f);
            m.Box(D.SteelDark, aerial + Vector3.up * 0.03f, new Vector3(0.14f, 0.06f, 0.14f), 0.02f, 2);
            m.Tube(D.SteelDark, aerial, aerial + Vector3.up * 1.4f, 0.012f, 6);
            S.Ball(m, (p, n) => D.Red, aerial + Vector3.up * 1.42f, Vector3.one * 0.03f, Quaternion.identity, 8, 6);

            Vector3 pot = new Vector3(HX + 0.55f, Plinth * 0.8f, -HZ + 0.65f);
            m.Lathe((p, n) => B.Hex("9A5A3E"), pot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.13f, 0f), new Vector2(0.17f, 0.28f), new Vector2(0.19f, 0.3f), new Vector2(0f, 0.3f),
            }, 14);
            for (int k = 0; k < 4; k++)
                S.Ball(m, (p, n) => k % 2 == 0 ? B.Leaf : B.LeafLight, pot + new Vector3(0.06f * Mathf.Cos(k * 1.6f), 0.4f + 0.04f * k, 0.06f * Mathf.Sin(k * 1.6f)),
                       new Vector3(0.12f, 0.1f, 0.12f), Quaternion.identity, 8, 6);

            // Fresh sandbags topping up the post.
            for (int k = 0; k < 3; k++)
            {
                float a = Mathf.Lerp(-0.5f, 0.5f, k / 2f);
                D.Sandbag(m, new Vector3(Mathf.Sin(a) * 1.7f - 0.2f, 0.54f, -HZ - 1.15f + (1f - Mathf.Cos(a)) * 1.2f), -a * Mathf.Rad2Deg, 1f, 40 + k);
            }

            return m;
        }

        /// <summary>The roof siren's beacon lit: a glowing dome over the painted one, shown while the noise maker sounds.</summary>
        public static B.Model AlarmBeacon()
        {
            var m = new B.Model();
            BeaconDome(m, new Vector3(-0.35f, Top + 0.2f + 0.75f + 0.18f + 0.03f, 0.15f), 0.094f, B.Model.Glow, Color.white);
            return m;
        }

        // --- The noise maker ---------------------------------------------------------------------------------

        /// <summary>The noise maker's top - drum, horns - at this height over its feet.</summary>
        public const float DeviceHead = 1.55f;

        /// <summary>
        /// The noise maker: a tripod of steel legs with rubber feet, two of them weighed down with
        /// sandbags; a mast up from its hub; a motor drum with two horn speakers on it, pointing opposite
        /// ways; a car battery at the foot with a cable up to it; hazard tape wrapped round the legs. The
        /// beacon on top is its own part, to spin. Origin on the ground under the mast.
        /// </summary>
        public static B.Model Device()
        {
            var m = new B.Model { Shade = D.GroundShade(0.3f) };
            Color hazard = B.Hex("C7A132");

            Vector3 hub = new Vector3(0f, 0.95f, 0f);
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI * 2f / 3f + 0.5f;
                var foot = new Vector3(Mathf.Cos(a) * 0.62f, 0.05f, Mathf.Sin(a) * 0.62f);
                Vector3 dir = (foot - hub).normalized;
                m.Tube((p, n) =>
                {
                    float along = Vector3.Dot(p - hub, dir);
                    bool tape = along > 0.45f && along < 0.7f && Mathf.Repeat(along * 18f, 1f) < 0.5f;
                    return tape ? hazard : D.SteelDark;
                }, hub, foot, 0.028f, 0.024f, 8);
                S.Ball(m, (p, n) => D.Rubber, foot, new Vector3(0.05f, 0.04f, 0.05f), Quaternion.identity, 8, 5);
                if (k < 2) D.Sandbag(m, foot + Vector3.up * 0.1f, -a * Mathf.Rad2Deg, 0.8f, 60 + k);
            }

            m.Lathe((p, n) => D.SteelDark, hub + Vector3.down * 0.06f, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.07f, 0f), new Vector2(0.08f, 0.06f), new Vector2(0.07f, 0.12f), new Vector2(0f, 0.12f),
            }, 12);
            m.Tube(D.Steel, hub, new Vector3(0f, DeviceHead, 0f), 0.032f, 10);

            Vector3 drum = new Vector3(0f, DeviceHead, 0f);
            m.Lathe((p, n) => n.y > 0.8f ? D.Red * 1.1f : D.Red, drum, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.13f, 0f), new Vector2(0.15f, 0.025f), new Vector2(0.15f, 0.2f), new Vector2(0.13f, 0.22f),
                new Vector2(0.05f, 0.23f), new Vector2(0f, 0.23f),
            }, 16);
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f;
                m.Box(D.SteelDark, drum + new Vector3(Mathf.Cos(a) * 0.15f, 0.11f, Mathf.Sin(a) * 0.15f), new Vector3(0.025f, 0.14f, 0.025f), 0.008f,
                      Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f));
            }

            Horn(m, drum + Vector3.up * 0.11f + Vector3.right * 0.12f, Vector3.right, 0.4f, 0.17f);
            Horn(m, drum + Vector3.up * 0.11f + Vector3.left * 0.12f, Vector3.left, 0.4f, 0.17f);

            // The battery at the foot, and the cable looping up to the drum.
            var battery = new Vector3(0.2f, 0.12f, -0.35f);
            m.Box(B.Hex("2D2F31"), battery, new Vector3(0.34f, 0.22f, 0.2f), 0.025f, Quaternion.Euler(0f, 15f, 0f), 2);
            m.Box(B.Hex("3E4246"), battery + Vector3.up * 0.115f, new Vector3(0.3f, 0.02f, 0.16f), 0.008f, Quaternion.Euler(0f, 15f, 0f));
            Vector3 plus = battery + Quaternion.Euler(0f, 15f, 0f) * new Vector3(0.1f, 0.13f, 0f);
            Vector3 minus = battery + Quaternion.Euler(0f, 15f, 0f) * new Vector3(-0.1f, 0.13f, 0f);
            m.Tube(D.Red, plus, plus + Vector3.up * 0.04f, 0.02f, 8);
            m.Tube(B.Hex("1F1F21"), minus, minus + Vector3.up * 0.04f, 0.02f, 8);
            K.Sweep(m, (p, n) => D.Red * 0.9f, new[]
            {
                plus + Vector3.up * 0.04f, plus + new Vector3(-0.05f, 0.12f, 0.1f), new Vector3(0.05f, 0.4f, -0.08f), new Vector3(0.04f, 1.0f, -0.04f),
                new Vector3(0.035f, DeviceHead + 0.02f, -0.03f),
            }, 0.012f, 6, 4);
            return m;
        }

        /// <summary>The beacon on top of the noise maker: an amber dome round a mirrored reflector, which shows it turning.</summary>
        public static B.Model DeviceBeacon()
        {
            var m = new B.Model();
            m.Lathe((p, n) => B.Iron, Vector3.zero, new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.09f, 0f), new Vector2(0.09f, 0.03f), new Vector2(0f, 0.03f) }, 14);
            BeaconDome(m, new Vector3(0f, 0.03f, 0f), 0.08f, B.Model.Painted, Beacon);
            m.Box(D.Steel * 1.2f, new Vector3(0f, 0.09f, 0f), new Vector3(0.11f, 0.08f, 0.012f), 0.004f, 2);
            D.GlowBox(m, new Vector3(0f, 0.09f, 0.01f), new Vector3(0.07f, 0.05f, 0.004f), Quaternion.identity);
            return m;
        }

        /// <summary>A ring of sound rolling out from the horns: a fat torus, glowing.</summary>
        public static B.Model SoundRing()
        {
            var m = new B.Model();
            var profile = new List<Vector2>();
            for (int i = 0; i <= 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f;
                profile.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.025f, Mathf.Sin(a) * 0.04f));
            }

            m.Lathe((p, n) => Color.white, Vector3.zero, profile, 28, B.Model.Glow);
            return m;
        }

        /// <summary>The ring on the ground showing how far it is heard: radius one, thin, for scaling to the earshot.</summary>
        public static B.Model Earshot()
        {
            var m = new B.Model();
            var profile = new List<Vector2>();
            for (int i = 0; i <= 8; i++)
            {
                float a = i / 8f * Mathf.PI;
                profile.Add(new Vector2(1f + Mathf.Cos(a) * 0.012f, 0.03f + Mathf.Sin(a) * 0.02f));
            }

            m.Lathe((p, n) => Color.white, Vector3.zero, profile, 64, B.Model.Glow);
            return m;
        }
    }
}
