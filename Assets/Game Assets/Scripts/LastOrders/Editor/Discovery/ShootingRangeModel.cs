using System;
using System.Collections.Generic;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using D = LastOrders.EditorTools.DiscoveryKit;
using K = FoodStreet.Visitors.EditorTools.VehicleKit;
using S = LastOrders.EditorTools.SoupKitchenModel;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The old shooting range: a firing point at one end under a lean-to of corrugated tin, a
    /// concrete pad for the machine between sandbag walls, a bench with an ammo crate; three lanes
    /// roped off downrange with their numbers raised on boards; a low earth berm the targets hide
    /// behind, the runners they ride on, and a tall backstop of earth faced with railway sleepers
    /// and stacked tyres. A scoreboard on posts beside the firing point.
    ///
    /// Origin on the ground in the middle; the firing point is at -Z and the range runs toward +Z.
    /// The cardboard zombies are their own model, built to fold on a hinge and ride a trolley.
    /// </summary>
    public static class ShootingRangeModel
    {
        public const float Length = 13f, Width = 11f;

        /// <summary>Where the practice machine stands, facing downrange.</summary>
        public static readonly Vector3 Mount = new Vector3(0f, 0.12f, -5f);

        /// <summary>The middle of each lane's runner, and how far either way along it a target rides.</summary>
        public static readonly Vector3[] Rails = { new Vector3(-3.4f, 0f, 3.6f), new Vector3(0f, 0f, 3.9f), new Vector3(3.4f, 0f, 3.6f) };
        public const float RailHalf = 1.25f;

        /// <summary>The scoreboard's face: its middle, for the score and the clock to sit on.</summary>
        public static readonly Vector3 BoardAt = new Vector3(3.6f, 0f, -5.9f);
        public const float BoardLift = 1.15f;
        public static readonly Vector2 BoardSize = new Vector2(1.7f, 1.0f);

        public static readonly Vector3 Marker = new Vector3(0f, 3.6f, -5f);

        const float ShelterBack = -6.4f, ShelterFront = -3.9f, ShelterHalf = 2.3f, BackHeight = 2.75f, FrontHeight = 2.3f;

        static float Shade(Vector3 p, Vector3 n) => Mathf.Lerp(0.78f, 1f, S.Smooth(Mathf.Clamp01(p.y / 0.4f))) * (n.y < -0.4f ? 0.84f : 1f);

        // --- The range -------------------------------------------------------------------------------

        public static B.Model Range()
        {
            var m = new B.Model { Shade = Shade };

            Shelter(m);
            Pad(m);
            Bench(m, new Vector3(-3.1f, 0f, -5.4f));
            Lanes(m);
            FrontBerm(m);
            Runners(m);
            Backstop(m);
            Scoreboard(m);

            // The flagpole by the firing point; the flag itself goes up only when the range is in use.
            var pole = new Vector3(-5.0f, 0f, -4.2f);
            m.Box(D.Concrete, pole + Vector3.up * 0.08f, new Vector3(0.36f, 0.16f, 0.36f), 0.04f, 2);
            m.Tube(D.Steel, pole, pole + Vector3.up * 4.2f, 0.045f, 10);
            S.Ball(m, (p, n) => D.Mustard, pole + Vector3.up * 4.25f, Vector3.one * 0.06f, Quaternion.identity, 8, 6);
            return m;
        }

        /// <summary>The lean-to's frame: four posts on footings, beams front and back. Its tin roof comes with how the range is found.</summary>
        static void Shelter(B.Model m)
        {
            foreach (float x in new[] { -ShelterHalf, ShelterHalf })
            {
                foreach ((float z, float h) in new[] { (ShelterBack, BackHeight), (ShelterFront, FrontHeight) })
                {
                    var foot = new Vector3(x, 0f, z);
                    m.Box(D.Concrete, foot + Vector3.up * 0.08f, new Vector3(0.3f, 0.16f, 0.3f), 0.04f, 2);
                    m.Box(B.Walnut, foot + Vector3.up * (h * 0.5f + 0.08f), new Vector3(0.16f, h - 0.04f, 0.16f), 0.03f, 2);
                }
            }

            m.Box(B.Walnut, new Vector3(0f, BackHeight + 0.02f, ShelterBack), new Vector3(ShelterHalf * 2f + 0.3f, 0.18f, 0.16f), 0.03f, 2);
            m.Box(B.Walnut, new Vector3(0f, FrontHeight + 0.02f, ShelterFront), new Vector3(ShelterHalf * 2f + 0.3f, 0.18f, 0.16f), 0.03f, 2);

            // Rafters between them, along the slope.
            float rise = BackHeight - FrontHeight, run = ShelterFront - ShelterBack;
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            for (int k = -2; k <= 2; k++)
            {
                float x = k * ShelterHalf * 0.5f;
                m.Box(B.WalnutDark, new Vector3(x, (BackHeight + FrontHeight) * 0.5f + 0.13f, (ShelterBack + ShelterFront) * 0.5f),
                      new Vector3(0.1f, 0.14f, Mathf.Sqrt(rise * rise + run * run) + 0.3f), 0.025f, Quaternion.Euler(angle, 0f, 0f), 2);
            }

            // A back wall of boards to chest height, so the firing point reads as a shed rather than a frame.
            m.BoardWall(new Vector3(0f, 0f, ShelterBack + 0.08f), Vector3.right, Vector3.forward, -ShelterHalf + 0.08f, ShelterHalf - 0.08f, 0.05f,
                        x => 1.3f, 0.22f, 41);
        }

        /// <summary>The roof: sheets of corrugated tin laid down the slope - all of them, or with one torn off and hanging.</summary>
        static void Roof(B.Model m, bool torn)
        {
            float rise = BackHeight - FrontHeight, run = ShelterFront - ShelterBack;
            float length = Mathf.Sqrt(rise * rise + run * run) + 0.6f;
            var down = new Vector3(0f, -rise, run).normalized;
            const int Sheets = 5;
            float sheet = (ShelterHalf * 2f + 0.5f) / Sheets;

            for (int i = 0; i < Sheets; i++)
            {
                if (torn && i == 3) continue;

                var corner = new Vector3(-ShelterHalf - 0.25f + i * sheet, BackHeight + 0.24f, ShelterBack - 0.3f);
                if (torn && i == 1) corner += Vector3.up * 0.03f;
                Color tin = B.Tin[i % B.Tin.Length];
                m.Corrugated(corner, Vector3.right, down, sheet + 0.04f, length, 0.15f, 0.03f, 0.02f, (u, v) =>
                {
                    float rust = torn ? Mathf.Clamp01((v / length - 0.5f) * 2f) * 0.5f : 0.1f;
                    return Color.Lerp(tin, B.Rust, rust * (0.6f + 0.4f * Mathf.Sin(u * 9f + i)));
                });
            }

            if (!torn) return;

            // The torn sheet: off its nails, slid down and hanging over the front beam, its end in the grass.
            var hanging = new Vector3(-ShelterHalf - 0.25f + 3 * sheet, FrontHeight + 0.3f, ShelterFront - 0.2f);
            m.Corrugated(hanging, Vector3.right, new Vector3(0.15f, -0.8f, 0.6f).normalized, sheet, 2.6f, 0.15f, 0.03f, 0.02f,
                         (u, v) => Color.Lerp(B.Tin[0], B.Rust, 0.45f + 0.2f * Mathf.Sin(u * 7f)));
        }

        /// <summary>The machine's pad: a concrete square with a bolted steel plate, sandbag walls either side and a low row in front.</summary>
        static void Pad(B.Model m)
        {
            var c = new Vector3(Mount.x, 0f, Mount.z);
            m.Box(D.Concrete, c + Vector3.up * 0.06f, new Vector3(2.3f, 0.12f, 2.3f), 0.04f, 2);
            m.Box(D.Steel, c + Vector3.up * 0.125f, new Vector3(1.2f, 0.02f, 1.2f), 0.01f, 2);
            for (int k = 0; k < 4; k++)
            {
                var bolt = c + new Vector3((k % 2 == 0 ? -1 : 1) * 0.5f, 0.14f, (k < 2 ? -1 : 1) * 0.5f);
                m.Tube(B.Iron, bolt, bolt + Vector3.up * 0.02f, 0.025f, 6);
            }

            for (int s = -1; s <= 1; s += 2)
            {
                for (int layer = 0; layer < 3; layer++)
                for (int k = 0; k < 3; k++)
                {
                    var at = c + new Vector3(s * 1.55f, 0.12f + layer * 0.2f, -0.75f + k * 0.62f + (layer % 2) * 0.3f);
                    if (at.z > c.z + 0.8f) continue;
                    D.Sandbag(m, at, 90f + 6f * B.Jitter(layer * 7 + k + s), 0.95f, layer * 13 + k + (s > 0 ? 50 : 0));
                }
            }

            for (int k = 0; k < 4; k++)
                D.Sandbag(m, c + new Vector3(-1.0f + k * 0.66f, 0.12f, 1.5f), 4f * B.Jitter(k), 0.95f, 80 + k);
        }

        static void Bench(B.Model m, Vector3 foot)
        {
            m.Box(B.Plank(5), foot + Vector3.up * 0.82f, new Vector3(1.4f, 0.06f, 0.6f), 0.015f, 2);
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                m.Box(B.Walnut, foot + new Vector3(sx * 0.62f, 0.4f, sz * 0.24f), new Vector3(0.07f, 0.8f, 0.07f), 0.015f, 2);
            m.Box(B.Walnut, foot + new Vector3(0f, 0.2f, 0f), new Vector3(1.3f, 0.04f, 0.5f), 0.012f);

            B.Crate(m, foot + new Vector3(-0.3f, 0.85f, 0f), new Vector3(0.42f, 0.26f, 0.32f), 8f, 7);

            // Ear defenders: two cups on a band.
            Vector3 ears = foot + new Vector3(0.35f, 0.92f, 0.05f);
            for (int s = -1; s <= 1; s += 2)
                S.Ball(m, (p, n) => D.Red, ears + Vector3.right * s * 0.09f, new Vector3(0.05f, 0.07f, 0.07f), Quaternion.identity, 10, 6);
            K.Sweep(m, (p, n) => D.SteelDark, new[] { ears + new Vector3(-0.09f, 0.04f, 0f), ears + new Vector3(0f, 0.11f, 0f), ears + new Vector3(0.09f, 0.04f, 0f) },
                    0.012f, 6, 4);
        }

        /// <summary>
        /// The lanes: lines of short posts with a rope sagging between them, from the firing point to the
        /// berm, and each lane's number standing on a board at its near end.
        /// </summary>
        static void Lanes(B.Model m)
        {
            const float From = -3.3f, To = 1.6f;
            foreach (float x in new[] { -5.1f, -1.7f, 1.7f, 5.1f })
            {
                var posts = new List<Vector3>();
                for (float z = From; z <= To + 0.01f; z += (To - From) / 3f) posts.Add(new Vector3(x, 0f, z));

                for (int i = 0; i < posts.Count; i++)
                {
                    m.Lathe((p, n) => (p.y > 0.45f && p.y < 0.55f) ? D.Red : D.CreamLight, posts[i], new List<Vector2>
                    {
                        new Vector2(0f, 0f), new Vector2(0.06f, 0f), new Vector2(0.05f, 0.03f), new Vector2(0.045f, 0.62f), new Vector2(0.055f, 0.65f),
                        new Vector2(0f, 0.69f),
                    }, 10);

                    if (i == 0) continue;
                    Vector3 a = posts[i - 1] + Vector3.up * 0.58f, b = posts[i] + Vector3.up * 0.58f;
                    K.Sweep(m, (p, n) => B.Twine * 0.85f, new[] { a, (a + b) * 0.5f + Vector3.down * 0.12f, b }, 0.014f, 6, 4);
                }
            }

            for (int lane = 0; lane < 3; lane++)
            {
                var foot = new Vector3(-3.4f + lane * 3.4f, 0f, From - 0.3f);
                m.Box(B.Walnut, foot + Vector3.up * 0.3f, new Vector3(0.06f, 0.6f, 0.06f), 0.015f, 2);
                Vector3 board = foot + new Vector3(0f, 0.68f, 0f);
                m.Box(D.CreamLight, board, new Vector3(0.42f, 0.42f, 0.06f), 0.03f, 2);
                Digit(m, board + Vector3.back * 0.035f, lane + 1, 0.26f, D.Red);
            }
        }

        /// <summary>A number in raised segments, as on an old scoreboard: <paramref name="height"/> tall, standing off the face toward -Z.</summary>
        public static void Digit(B.Model m, Vector3 centre, int digit, float height, Color colour)
        {
            // Segments a-g, clockwise from the top, then the middle.
            int[] lit = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };
            int mask = lit[Mathf.Clamp(digit, 0, 9)];
            float w = height * 0.55f, h = height * 0.5f, t = height * 0.12f;

            var segments = new (Vector3 at, bool across)[]
            {
                (new Vector3(0f, h, 0f), true), (new Vector3(w * 0.5f, h * 0.5f, 0f), false), (new Vector3(w * 0.5f, -h * 0.5f, 0f), false),
                (new Vector3(0f, -h, 0f), true), (new Vector3(-w * 0.5f, -h * 0.5f, 0f), false), (new Vector3(-w * 0.5f, h * 0.5f, 0f), false),
                (new Vector3(0f, 0f, 0f), true),
            };

            for (int i = 0; i < 7; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                Vector3 size = segments[i].across ? new Vector3(w - t * 0.6f, t, t * 0.7f) : new Vector3(t, h - t * 0.6f, t * 0.7f);
                m.Box(colour, centre + segments[i].at + Vector3.back * t * 0.3f, size, t * 0.4f, 2);
            }
        }

        /// <summary>The low berm the targets drop behind: a bank of earth faced with boards toward the firing point, grass on its crest.</summary>
        static void FrontBerm(B.Model m)
        {
            const float Z0 = 2.0f, Z1 = 3.15f, Height = 0.62f;
            m.Heightfield((p, n) => Color.Lerp(D.Earth, B.Leaf, Mathf.Clamp01((p.y - 0.35f) / 0.25f) * Mathf.Clamp01(n.y * 1.4f)) * (0.92f + 0.08f * K.Noise(p * 3f)),
                          Rect.MinMaxRect(-Width * 0.5f, Z0 - 0.2f, Width * 0.5f, Z1 + 0.3f), 36, (x, z) =>
                          {
                              float across = Mathf.Clamp01(1f - Mathf.Abs((z - (Z0 + Z1) * 0.5f) / ((Z1 - Z0) * 0.5f + 0.3f)));
                              float ends = Mathf.Clamp01((Width * 0.5f - Mathf.Abs(x)) / 0.6f);
                              return Height * S.Smooth(across) * S.Smooth(ends) + 0.02f * Mathf.Sin(x * 2.3f) * across;
                          });

            // Boards along its face, staked: what it is the targets hide behind.
            for (int k = 0; k < 9; k++)
            {
                float x = -Width * 0.5f + 0.9f + k * 1.15f;
                m.Box(B.Plank(k + 60), new Vector3(x, 0.25f, Z0 + 0.05f), new Vector3(1.12f, 0.42f, 0.06f), 0.015f, Quaternion.Euler(-14f, 0f, 0f), 2);
                m.Box(B.Walnut, new Vector3(x - 0.5f, 0.25f, Z0 - 0.02f), new Vector3(0.07f, 0.52f, 0.07f), 0.015f, 2);
            }
        }

        /// <summary>Each lane's runner: two steel rails on sleepers, with buffers at both ends.</summary>
        static void Runners(B.Model m)
        {
            foreach (Vector3 rail in Rails)
            {
                for (float x = -RailHalf - 0.15f; x <= RailHalf + 0.16f; x += 0.42f)
                    m.Box(B.WalnutDark, rail + new Vector3(x, 0.03f, 0f), new Vector3(0.12f, 0.06f, 0.5f), 0.015f, 2);

                foreach (float z in new[] { -0.14f, 0.14f })
                    m.Tube(D.Steel, rail + new Vector3(-RailHalf - 0.25f, 0.08f, z), rail + new Vector3(RailHalf + 0.25f, 0.08f, z), 0.02f, 8);

                foreach (float x in new[] { -RailHalf - 0.3f, RailHalf + 0.3f })
                    m.Box(D.Mustard, rail + new Vector3(x, 0.11f, 0f), new Vector3(0.08f, 0.14f, 0.42f), 0.025f, 2);
            }
        }

        /// <summary>The backstop: a tall bank of earth, faced with railway sleepers in courses and tyres stacked at both ends.</summary>
        static void Backstop(B.Model m)
        {
            const float Z0 = 4.8f, Z1 = 6.5f, Height = 2.3f;
            m.Heightfield((p, n) => Color.Lerp(D.EarthDark, B.Leaf * 0.9f, Mathf.Clamp01((p.y - 1.7f) / 0.5f) * Mathf.Clamp01(n.y * 1.3f)) *
                                    (0.9f + 0.1f * K.Noise(p * 2.5f)),
                          Rect.MinMaxRect(-Width * 0.5f - 0.3f, Z0, Width * 0.5f + 0.3f, Z1 + 0.6f), 40, (x, z) =>
                          {
                              float up = S.Smooth(Mathf.Clamp01((z - Z0) / 0.5f));
                              float down = S.Smooth(Mathf.Clamp01((Z1 + 0.6f - z) / 1.2f));
                              float ends = S.Smooth(Mathf.Clamp01((Width * 0.5f + 0.3f - Mathf.Abs(x)) / 0.8f));
                              return Height * up * down * ends + 0.05f * Mathf.Sin(x * 1.7f + z * 2.1f) * up * down;
                          });

            // Sleepers stacked against its face, each course set a little further back.
            for (int course = 0; course < 7; course++)
            {
                float y = 0.12f + course * 0.23f, z = Z0 - 0.15f + course * 0.06f;
                float offset = course % 2 == 0 ? 0f : 0.6f;
                for (float x = -Width * 0.5f + 0.4f + offset; x < Width * 0.5f - 0.6f; x += 1.22f)
                {
                    Color c = B.WalnutDark * (1f + 0.07f * B.Jitter(course * 31 + (int)(x * 3f)));
                    m.Box(c, new Vector3(x + 0.6f, y, z), new Vector3(1.18f, 0.22f, 0.26f), 0.03f, Quaternion.Euler(-8f, 0f, 0f), 2);
                }
            }

            foreach (float x in new[] { -Width * 0.5f + 0.2f, Width * 0.5f - 0.2f })
                for (int k = 0; k < 4; k++)
                    D.Tyre(m, new Vector3(x, 0.11f + k * 0.21f, Z0 - 0.55f), Vector3.up, 0.38f, 0.22f);
        }

        /// <summary>The scoreboard: a black board on two posts with a red header, the score and the clock to be laid on its face.</summary>
        static void Scoreboard(B.Model m)
        {
            D.PostSign(m, BoardAt, 0f, BoardSize, BoardLift, B.Walnut, B.Hex("23292A"), D.Red);
            Vector3 face = BoardAt + new Vector3(0f, BoardLift + BoardSize.y * 0.5f - 0.04f, -0.07f);

            // A divider between the score and the clock, and little brass lamps along the top.
            m.Box(B.Walnut, face + new Vector3(0.1f, -0.06f, 0f), new Vector3(0.04f, BoardSize.y - 0.36f, 0.03f), 0.01f, 2);
            for (int k = -2; k <= 2; k++)
                m.Lathe((p, n) => D.Mustard, face + new Vector3(k * 0.3f, BoardSize.y * 0.5f - 0.02f, 0f), new List<Vector2>
                {
                    new Vector2(0f, 0f), new Vector2(0.03f, 0f), new Vector2(0.035f, 0.03f), new Vector2(0f, 0.045f),
                }, 8);
        }

        // --- How it was found, and back at work ----------------------------------------------------------

        public static B.Model Derelict()
        {
            var m = new B.Model { Shade = Shade };

            Roof(m, torn: true);

            // Targets lying where they fell, faded and bent.
            CutOut(m, new Vector3(-3.0f, 0.05f, 3.9f), Quaternion.Euler(-88f, 30f, 0f), faded: true);
            CutOut(m, new Vector3(2.4f, 0.05f, 1.2f), Quaternion.Euler(-86f, -60f, 8f), faded: true);
            CutOut(m, new Vector3(4.1f, 0.4f, 4.4f), Quaternion.Euler(-35f, 160f, 12f), faded: true);

            // KEEP OUT, on a board knocked askew, and weeds over everything.
            D.PostSign(m, new Vector3(-4.6f, 0f, -2.6f), 18f, new Vector2(0.9f, 0.6f), 0.7f, B.Walnut, D.CreamLight, D.Red);
            for (int k = 0; k < 16; k++)
            {
                float x = -Width * 0.5f + 0.6f + (k * 3.7f) % (Width - 1.2f);
                float z = -4.5f + (k * 5.3f) % 10f;
                D.WeedClump(m, new Vector3(x, z > 2f && z < 3.2f ? 0.55f : 0f, z), 0.3f, 10, 120 + k);
            }

            D.Tyre(m, new Vector3(1.4f, 0.11f, -1.5f), Vector3.up, 0.38f, 0.22f);
            D.Cone(m, new Vector3(-1.2f, 0f, -2.6f), 70f, true);
            D.RubbishSack(m, new Vector3(2.9f, 0f, -4.6f), 0.9f, 7);
            return m;
        }

        public static B.Model Restored()
        {
            var m = new B.Model { Shade = Shade };

            Roof(m, torn: false);

            // The red flag up: a sheet with a ripple through it, two-sided, so it never vanishes edge-on.
            var top = new Vector3(-5.0f, 4.1f, -4.2f);
            Func<float, float, Vector3> flag = (u, v) =>
                top + new Vector3(0.05f + u * 1.1f, -v * 0.7f + 0.05f * Mathf.Sin(u * 5f), 0.09f * Mathf.Sin(u * 6f + v * 1.5f) * u);
            m.Surface((p, n) => D.Red, 16, 6, flag);
            m.Surface((p, n) => D.Red * 0.8f, 6, 16, (v, u) => flag(u, v) + new Vector3(0f, 0f, 0.008f));

            // Lamps on the shelter's front posts, lit.
            foreach (float x in new[] { -ShelterHalf, ShelterHalf })
                m.Lantern(new Vector3(x, FrontHeight - 0.2f, ShelterFront - 0.09f), Vector3.back);

            // The scoreboard's header lit, and a loudspeaker on the shelter for calling the rounds.
            Vector3 header = BoardAt + new Vector3(0f, BoardLift + BoardSize.y - 0.08f, -0.08f);
            D.GlowBox(m, header, new Vector3(BoardSize.x - 0.2f, 0.06f, 0.012f), Quaternion.identity);
            SecurityHutModel.Horn(m, new Vector3(ShelterHalf - 0.1f, FrontHeight + 0.05f, ShelterFront), new Vector3(0.3f, -0.15f, 1f), 0.32f, 0.13f);

            // A fresh row of sandbags along the front of the pad, and a crate of practice rounds by it.
            for (int k = 0; k < 4; k++)
                D.Sandbag(m, new Vector3(Mount.x - 1.0f + k * 0.66f, 0.32f, Mount.z + 1.5f), 4f * B.Jitter(k + 20), 0.95f, 90 + k);
            B.Crate(m, new Vector3(1.4f, 0f, -3.4f), new Vector3(0.5f, 0.34f, 0.4f), -12f, 9);
            return m;
        }

        // --- The cardboard zombie ------------------------------------------------------------------------

        /// <summary>Height of the trolley's top, where the cut-out's hinge sits.</summary>
        public const float HingeHeight = 0.2f;

        /// <summary>
        /// The trolley a target rides its runner on: a steel box on four flanged wheels sitting on the
        /// rails, a hinge bracket on top. Origin on the ground between the rails.
        /// </summary>
        public static B.Model Trolley()
        {
            var m = new B.Model();
            m.Box(D.SteelDark, new Vector3(0f, 0.15f, 0f), new Vector3(0.46f, 0.08f, 0.38f), 0.02f, 2);
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var hub = new Vector3(sx * 0.16f, 0.11f, sz * 0.14f);
                m.Tube(D.Steel, hub + Vector3.back * 0.02f * sz, hub + Vector3.forward * 0.02f * sz, 0.04f, 10);
                m.Tube(D.SteelDark, hub, hub + Vector3.forward * 0.03f * sz, 0.048f, 10);
            }

            m.Box(D.Mustard, new Vector3(0f, 0.19f, -0.02f), new Vector3(0.34f, 0.04f, 0.08f), 0.01f, 2);
            m.Tube(B.Iron, new Vector3(-0.16f, HingeHeight, -0.02f), new Vector3(0.16f, HingeHeight, -0.02f), 0.02f, 8);
            return m;
        }

        /// <summary>The cut-out on its own, about its hinge, upright and facing -Z: for the live targets.</summary>
        public static B.Model CardboardZombie()
        {
            var m = new B.Model();
            CutOut(m, Vector3.zero, Quaternion.identity, faded: false);
            return m;
        }

        /// <summary>
        /// A cardboard zombie: legs, a ragged shirt, arms reaching up and out, a round head with hair
        /// tufts, a face of goggling eyes and teeth - every piece a slab of card four centimetres
        /// thick with its corrugated edge showing darker, the face and the bullseye on its chest raised
        /// off it - braced from behind by two struts down to the hinge. Built at <paramref name="foot"/>,
        /// turned by <paramref name="turn"/>.
        /// </summary>
        static void CutOut(B.Model m, Vector3 foot, Quaternion turn, bool faded)
        {
            const float T = 0.04f;
            float fade = faded ? 0.82f : 1f;
            Color skin = D.Zombie * fade, shirt = B.Hex("4F6A86") * fade, trousers = B.Hex("6B5440") * fade;

            Func<Vector3, Vector3, Color> Card(Color face) => (p, n) => Mathf.Abs(n.z) < 0.6f ? D.CardboardDark * fade : face;

            Matrix4x4 was = m.Transform;
            m.Transform = was * Matrix4x4.TRS(foot, turn, Vector3.one);
            try
            {
                void Slab(Color colour, Vector3 centre, Vector3 size, float roll, float radius = 0.03f)
                {
                    var shape = new SmoothMesh(1);
                    shape.RoundedBox(0, centre, new Vector3(size.x, size.y, T), radius, Quaternion.Euler(0f, 0f, roll), 2);
                    m.Add(shape, Card(colour));
                }

                // Legs, apart, one a little bent; shoes.
                Slab(trousers, new Vector3(-0.12f, 0.33f, 0f), new Vector3(0.15f, 0.56f, T), -6f);
                Slab(trousers, new Vector3(0.13f, 0.33f, 0f), new Vector3(0.15f, 0.56f, T), 9f);
                Slab(B.Hex("2E2A26") * fade, new Vector3(-0.15f, 0.07f, -0.005f), new Vector3(0.2f, 0.1f, T), 0f);
                Slab(B.Hex("2E2A26") * fade, new Vector3(0.17f, 0.07f, -0.005f), new Vector3(0.2f, 0.1f, T), 0f);

                // The torso in a torn shirt: its hem ragged in teeth along the bottom.
                Slab(shirt, new Vector3(0f, 0.86f, 0f), new Vector3(0.48f, 0.56f, T), 2f, 0.06f);
                for (int k = 0; k < 5; k++)
                    Slab(shirt, new Vector3(-0.19f + k * 0.095f, 0.56f, 0f), new Vector3(0.07f, 0.1f, T), 45f, 0.01f);

                // Arms up and out, hands hooked.
                Slab(shirt, new Vector3(-0.36f, 1.07f, 0f), new Vector3(0.12f, 0.42f, T), 58f);
                Slab(shirt, new Vector3(0.37f, 1.13f, 0f), new Vector3(0.12f, 0.42f, T), -66f);
                Slab(skin, new Vector3(-0.56f, 1.2f, 0f), new Vector3(0.13f, 0.12f, T), 20f, 0.04f);
                Slab(skin, new Vector3(0.58f, 1.3f, 0f), new Vector3(0.13f, 0.12f, T), -30f, 0.04f);

                // The head: a disc of card, tufts of hair, a neck.
                Slab(skin, new Vector3(0f, 1.18f, 0f), new Vector3(0.1f, 0.12f, T), 0f);
                m.Tube((p, n) => Mathf.Abs(n.z) < 0.6f ? D.CardboardDark * fade : skin, new Vector3(0.02f, 1.38f, T * 0.5f), new Vector3(0.02f, 1.38f, -T * 0.5f),
                       0.18f, 0.18f, 18);
                for (int k = 0; k < 3; k++)
                    Slab(B.Hex("3A3029") * fade, new Vector3(-0.08f + k * 0.08f, 1.55f, 0f), new Vector3(0.06f, 0.1f, T), -20f + k * 20f, 0.015f);

                // The face, standing off the card: goggling eyes, one bigger, and a mouth of teeth.
                Vector3 face = new Vector3(0.02f, 1.38f, -T * 0.5f);
                foreach ((float x, float y, float r) in new[] { (-0.07f, 0.04f, 0.055f), (0.07f, 0.05f, 0.042f) })
                {
                    m.Tube(B.Hex("D9D6CC") * fade, face + new Vector3(x, y, 0f), face + new Vector3(x, y, -0.01f), r, 12);
                    m.Tube(B.Hex("1A1A1C"), face + new Vector3(x + 0.01f, y - 0.008f, -0.01f), face + new Vector3(x + 0.01f, y - 0.008f, -0.016f), r * 0.4f, 8);
                }

                m.Box(B.Hex("3A1F1C") * fade, face + new Vector3(0.01f, -0.08f, -0.006f), new Vector3(0.16f, 0.06f, 0.012f), 0.012f, Quaternion.Euler(0f, 0f, -6f));
                for (int k = 0; k < 3; k++)
                    m.Box(B.Hex("D3CCBA") * fade, face + new Vector3(-0.04f + k * 0.045f, -0.065f, -0.013f), new Vector3(0.025f, 0.025f, 0.006f), 0.004f);

                // The bullseye on its chest, rings raised in steps.
                Vector3 chest = new Vector3(0f, 0.9f, -T * 0.5f);
                foreach ((float r, Color c, float lift) in new[] { (0.15f, B.Hex("D9D6CC"), 0.004f), (0.11f, D.Red, 0.007f), (0.07f, B.Hex("D9D6CC"), 0.01f), (0.03f, D.Red, 0.013f) })
                    m.Tube(c * fade, chest, chest + Vector3.back * lift, r, 20);

                // Tape where it was mended.
                m.Box(B.Hex("C9C1A3") * fade, new Vector3(0.12f, 0.62f, -T * 0.5f - 0.002f), new Vector3(0.2f, 0.05f, 0.006f), 0.002f, Quaternion.Euler(0f, 0f, 30f));

                // Struts behind, from the shoulders down to the hinge.
                foreach (float x in new[] { -0.15f, 0.15f })
                    m.Tube(B.Walnut, new Vector3(x, 1.0f, T * 0.5f), new Vector3(x * 0.8f, 0.02f, 0.22f), 0.018f, 6);
                m.Box(B.Walnut, new Vector3(0f, 0.02f, 0.2f), new Vector3(0.34f, 0.04f, 0.06f), 0.012f);
            }
            finally
            {
                m.Transform = was;
            }
        }
    }
}
