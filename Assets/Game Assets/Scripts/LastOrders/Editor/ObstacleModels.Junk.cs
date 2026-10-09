using System.Collections.Generic;
using LastOrders.Core;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// What people left behind: car wrecks, scrap heaps, rubble and old barricades. Same style and
    /// rules as the stone and wood in <see cref="ObstacleModels"/>.
    /// </summary>
    public static partial class ObstacleModels
    {
        // --- The palette ---------------------------------------------------------------------------

        /// <summary>Faded paint, one per car: teal, red and sage. None of them a warm cream.</summary>
        static readonly Color[] CarPaint = { B.Hex("4F8A86"), B.Hex("A04A40"), B.Hex("7E9270") };

        static readonly Color Rust = B.Hex("85614C");
        static readonly Color RustDark = B.Hex("6E4A38");
        static readonly Color Rubber = B.Hex("2F2F31");
        static readonly Color Hub = B.Hex("8D9295");
        static readonly Color Trim = B.Hex("6F7478");
        static readonly Color Grille = B.Hex("3A3B3E");
        static readonly Color Headlight = B.Hex("D2CDBC");
        static readonly Color TailLight = B.Hex("8E3A33");
        static readonly Color BedFloor = B.Hex("3D3833");
        /// <summary>Mid grey, as the stone is: a lump of concrete in the sun reads white at anything lighter.</summary>
        static readonly Color[] Concrete = { B.Hex("86847D"), B.Hex("7D7B75"), B.Hex("8E8B84"), B.Hex("83807A") };
        static readonly Color ConcreteInside = B.Hex("4A4843");
        static readonly Color[] Brick = { B.Hex("9A5544"), B.Hex("8C4A3B"), B.Hex("A35E4A") };
        static readonly Color Rebar = B.Hex("5A4A40");
        static readonly Color Gravel = B.Hex("74726C");
        static readonly Color GravelFleck = B.Hex("86847E");
        static readonly Color EarthFleck = B.Hex("7E6A52");
        static readonly Color[] DrumPaint = { B.Hex("4D6E8F"), B.Hex("9E463C"), B.Hex("5E7A4A") };
        static readonly Color[] Sandbag = { B.Hex("9C8A63"), B.Hex("8A7A56"), B.Hex("A39170") };
        static readonly Color Enamel = B.Hex("B9B6AC");
        static readonly Color StripeRed = B.Hex("B5463C");
        static readonly Color StripeWhite = B.Hex("CBC6B6");

        // --- Parts ------------------------------------------------------------------------------------

        /// <summary>
        /// A tyre, its axis along local Y through the origin of <paramref name="local"/>: a ring of
        /// rubber round a grey hub, the sidewalls a shade lighter than the tread.
        /// </summary>
        static void Tyre(B.Model m, Matrix4x4 local, float outer, float width, bool hub)
        {
            float tube = width * 0.5f;
            float ring = outer - tube;

            var profile = new List<Vector2>();
            for (int i = 0; i <= 16; i++)
            {
                float a = -Mathf.PI * 0.5f + i / 16f * Mathf.PI * 2f;
                profile.Add(new Vector2(ring + tube * Mathf.Cos(a), tube * Mathf.Sin(a) * 0.92f));
            }

            Placed(m, local, () =>
            {
                m.Lathe((p, n) => Mathf.Abs(n.y) > 0.6f ? Rubber * 1.18f : Rubber, Vector3.zero, profile, 18);

                if (!hub) return;

                float r = ring * 0.8f, h = tube * 0.55f;
                var disc = new List<Vector2>
                {
                    new Vector2(0f, -h), new Vector2(r, -h), new Vector2(r, -h), new Vector2(r, h), new Vector2(r, h),
                    new Vector2(r * 0.4f, h + 0.01f), new Vector2(0f, h + 0.012f),
                };
                m.Lathe((p, n) => new Vector2(p.x, p.z).magnitude < r * 0.45f ? Hub * 0.75f : Hub, Vector3.zero, disc, 14);
            });
        }

        /// <summary>A steel drum standing on the origin of <paramref name="local"/>: two rolled ribs, a lid, rust at the rims.</summary>
        static void Drum(B.Model m, Matrix4x4 local, float radius, float height, Color colour, int key)
        {
            var profile = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(radius * 0.96f, 0f), new Vector2(radius * 0.96f, 0f), new Vector2(radius, 0.03f),
                new Vector2(radius, height * 0.3f), new Vector2(radius * 1.04f, height * 0.33f), new Vector2(radius, height * 0.36f),
                new Vector2(radius, height * 0.64f), new Vector2(radius * 1.04f, height * 0.67f), new Vector2(radius, height * 0.7f),
                new Vector2(radius, height - 0.03f), new Vector2(radius * 0.96f, height), new Vector2(radius * 0.96f, height),
                new Vector2(radius * 0.88f, height - 0.012f), new Vector2(0f, height - 0.012f),
            };

            Vector3 seed = Seed(key);

            Color Paint(Vector3 p, Vector3 n)
            {
                if (n.y > 0.7f && p.y > height - 0.05f) return colour * 0.78f;

                float rim = Mathf.Max(1f - Band(p.y, 0f, 0.12f), Band(p.y, height - 0.1f, height));
                float rust = Mathf.Max(rim * 0.75f, Band(Noise(p * 3f + seed), 0.57f, 0.67f));
                return Color.Lerp(colour * (0.95f + 0.08f * Noise(p * 5f + seed)), Color.Lerp(Rust, RustDark, Noise(p * 7f)), rust);
            }

            Placed(m, local, () => m.Lathe(Paint, Vector3.zero, profile, 16));
        }

        /// <summary>A drum lying on its side at <paramref name="foot"/>, its axis turned to <paramref name="yaw"/>.</summary>
        static void DrumLying(B.Model m, Vector3 foot, float yaw, float radius, float height, Color colour, int key) =>
            Drum(m, Matrix4x4.TRS(foot + Vector3.up * radius, Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f), Vector3.one)
                     * Matrix4x4.Translate(Vector3.down * height * 0.5f), radius, height, colour, key);

        /// <summary>
        /// A sheet of corrugated tin from the middle of its top edge at <paramref name="top"/> down to
        /// <paramref name="bottom"/>, <paramref name="width"/> across along <paramref name="across"/>:
        /// leaning on something, or lying flat. Rust gathers toward its foot.
        /// </summary>
        static void Sheet(B.Model m, Vector3 top, Vector3 bottom, Vector3 across, float width, Color[] tin, int key)
        {
            Vector3 down = bottom - top;
            float length = down.magnitude;
            across.Normalize();

            // Ridges up, toward the sky.
            if (Vector3.Cross(down, across).y < 0f) across = -across;
            Vector3 corner = top - across * (width * 0.5f);
            Vector3 seed = Seed(key);

            m.Corrugated(corner, across, down, width, length, 0.13f, 0.035f, 0.012f, (u, v) =>
            {
                Color c = tin[B.Hash(key + (int)(u / 0.65f)) % tin.Length];
                float rust = Mathf.Max(Band(v / length, 0.62f, 1f) * 0.65f,
                                       Band(Noise(new Vector3(u, v, 0f) * 1.6f + seed), 0.6f, 0.7f));
                return Color.Lerp(c, Rust, rust);
            });
        }

        static void Plank(B.Model m, Vector3 centre, Vector3 size, Quaternion turn, int key) =>
            m.Box(B.Plank(key) * (1f + 0.04f * B.Jitter(key)), centre, size, 0.015f, turn, 1);

        /// <summary>A broken lump of concrete: a box with its edges knocked round.</summary>
        static void Chunk(B.Model m, Vector3 centre, Vector3 size, Quaternion turn, int key)
        {
            float r = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.2f;
            m.Box(Pick(Concrete, key) * (0.94f + 0.1f * Hash01(key)), centre, size, r, turn, 2);
        }

        static void BrickAt(B.Model m, Vector3 centre, Quaternion turn, int key) =>
            m.Box(Pick(Brick, key) * (0.93f + 0.12f * Hash01(key)), centre, new Vector3(0.23f, 0.075f, 0.11f), 0.014f, turn, 1);

        /// <summary>Bent reinforcing bar out of broken concrete: two straight lengths with a kink.</summary>
        static void RebarAt(B.Model m, Vector3 from, Vector3 heading, float length, int key)
        {
            Vector3 kink = from + heading.normalized * (length * 0.55f);
            Vector3 bend = Quaternion.Euler(25f * (Hash01(key) - 0.5f) * 2f, 40f * (Hash01(key + 1) - 0.5f) * 2f, 0f) * heading.normalized;
            m.Tube(Rebar, from, kink, 0.018f, 5);
            m.Tube(Rebar * 0.9f, kink, kink + bend * (length * 0.45f), 0.018f, 5);
        }

        static void SandbagAt(B.Model m, Vector3 centre, Quaternion turn, int key) =>
            m.Box(Pick(Sandbag, key) * (0.94f + 0.1f * Hash01(key)), centre, new Vector3(0.56f, 0.2f, 0.32f), 0.085f, turn, 2);

        /// <summary>A heap of loose stuff on the ground - gravel, earth - for the rest to lie on.</summary>
        static void Mound(B.Model m, Vector3 foot, float radius, float height, Color colour, Color fleck, int key) =>
            Bush(m, foot, radius, key, colour, height, fleck);

        // --- Cars --------------------------------------------------------------------------------------

        /// <summary>
        /// A car left where it stopped: a rounded toy-like body in faded paint with rust coming through,
        /// a cabin of dark glass on body-coloured pillars, flat or missing tyres, and the grass growing up
        /// round it. A sedan, a hatchback down on one corner, and a pickup with junk in the bed.
        /// </summary>
        static void CarWreck(B.Model m, int variant, int key, ref DeterministicRandom rng)
        {
            const float Width = 1.74f;
            const float BodyHeight = 0.56f;
            const float Clearance = 0.2f;
            const float CabinHeight = 0.56f;

            float length, cabinLength, cabinX, windscreen, rearScreen;
            switch (variant)
            {
                case 0: length = 4.0f; cabinLength = 2.05f; cabinX = -0.2f; windscreen = 0.5f; rearScreen = 0.36f; break;
                case 1: length = 3.5f; cabinLength = 2.0f; cabinX = -0.42f; windscreen = 0.46f; rearScreen = 0.1f; break;
                default: length = 4.5f; cabinLength = 1.45f; cabinX = 0.6f; windscreen = 0.4f; rearScreen = 0.05f; break;
            }

            Color paint = CarPaint[variant % CarPaint.Length];
            Vector3 seed = Seed(key);
            float top = Clearance + BodyHeight;

            Color BodyPaint(Vector3 p, Vector3 n)
            {
                Color c = paint * (0.92f + 0.1f * Noise(p * 2f + seed));
                if (n.y < -0.4f) return c * 0.6f;

                float rust = Band(Noise(p * 1.3f + seed * 2f), 0.56f, 0.66f) * (1f - Band(p.y, 0.45f, 0.85f) * 0.6f);
                c = Color.Lerp(c, Color.Lerp(Rust, RustDark, Noise(p * 4f)), rust);

                // Moss and fallen leaves on the flat tops.
                if (n.y > 0.75f) c = Color.Lerp(c, Moss[0], Band(Noise(p * 1.8f + seed * 3f), 0.6f, 0.7f) * 0.8f);
                return c;
            }

            Vector3 cabinCentre = new Vector3(cabinX, top + CabinHeight * 0.5f - 0.04f, 0f);
            Vector3 cabinSize = new Vector3(cabinLength, CabinHeight, Width - 0.18f);

            Color CabinPaint(Vector3 p, Vector3 n)
            {
                if (n.y > 0.72f || n.y < -0.4f) return BodyPaint(p, n);
                if (Mathf.Abs(n.x) > 0.3f && Mathf.Abs(n.z) > 0.3f) return BodyPaint(p, n);

                float h = (p.y - (cabinCentre.y - cabinSize.y * 0.5f)) / cabinSize.y;
                if (h < 0.12f) return BodyPaint(p, n);
                return Color.Lerp(B.Glass, B.GlassSky, Band(h, 0.45f, 0.95f) * 0.8f);
            }

            // The hatchback has lost its front left wheel and sits down on that corner.
            bool sagging = variant == 1;
            Quaternion sag = sagging ? Quaternion.Euler(-3.2f, 0f, 2.6f) : Quaternion.identity;

            Placed(m, sagging ? Vector3.down * 0.04f : Vector3.zero, sag, () =>
            {
                var body = new SmoothMesh(1);
                body.RoundedBox(0, new Vector3(0f, Clearance + BodyHeight * 0.5f, 0f), new Vector3(length, BodyHeight, Width), 0.2f,
                                Quaternion.identity, 3);
                m.Add(body, BodyPaint);

                // The cabin: a rounded box leant in at the top, front and back, into a windscreen and a rear window.
                var cabin = new SmoothMesh(1);
                cabin.RoundedBox(0, cabinCentre, cabinSize, 0.15f, Quaternion.identity, 3);
                m.Warp = q =>
                {
                    float t = Mathf.Clamp01((q.y - (cabinCentre.y - cabinSize.y * 0.5f)) / cabinSize.y);
                    float s = (q.x - cabinCentre.x) / (cabinSize.x * 0.5f);
                    float x = q.x - t * (windscreen * Mathf.Max(0f, s) - rearScreen * Mathf.Max(0f, -s));
                    return new Vector3(x, q.y, q.z * (1f - 0.1f * t));
                };
                m.Add(cabin, CabinPaint);
                m.Warp = null;

                // The pillar between the doors, leaning in with the glass.
                float tilt = Mathf.Atan2(0.1f * cabinSize.z * 0.5f, cabinSize.y) * Mathf.Rad2Deg;
                for (int side = -1; side <= 1; side += 2)
                {
                    float z = side * (cabinSize.z * 0.5f * 0.95f + 0.008f);
                    m.Box(paint * 0.95f, new Vector3(cabinX + cabinLength * 0.06f, cabinCentre.y + 0.02f, z),
                          new Vector3(0.1f, cabinSize.y * 0.8f, 0.03f), 0.012f, Quaternion.Euler(-side * tilt, 0f, 0f));
                }

                // Front and back.
                for (int end = -1; end <= 1; end += 2)
                {
                    float x = end * (length * 0.5f + 0.02f);
                    m.Box(Trim, new Vector3(x, Clearance + 0.13f, 0f), new Vector3(0.16f, 0.17f, Width - 0.06f), 0.06f, 2);

                    Color lamp = end > 0 ? Headlight : TailLight;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        m.Box(lamp, new Vector3(end * (length * 0.5f + 0.005f), Clearance + BodyHeight * 0.62f, side * (Width * 0.5f - 0.3f)),
                              new Vector3(0.05f, 0.12f, 0.28f), 0.025f, 2);
                    }
                }

                m.Box(Grille, new Vector3(length * 0.5f + 0.008f, Clearance + BodyHeight * 0.58f, 0f), new Vector3(0.04f, 0.15f, Width * 0.4f), 0.02f);

                if (variant == 2)
                {
                    // The pickup's bed: low walls round the back, a dark floor, and what was being carried.
                    float bedFrom = -length * 0.5f + 0.05f, bedTo = cabinX - cabinLength * 0.5f - 0.06f;
                    float bedMid = (bedFrom + bedTo) * 0.5f, bedLength = bedTo - bedFrom;

                    m.Box(BedFloor, new Vector3(bedMid, top - 0.005f, 0f), new Vector3(bedLength, 0.03f, Width - 0.16f), 0.01f);
                    for (int side = -1; side <= 1; side += 2)
                        m.Box(paint * 0.94f, new Vector3(bedMid, top + 0.14f, side * (Width * 0.5f - 0.04f)), new Vector3(bedLength, 0.3f, 0.07f), 0.03f, 2);
                    m.Box(paint * 0.94f, new Vector3(bedFrom + 0.035f, top + 0.14f, 0f), new Vector3(0.07f, 0.3f, Width - 0.08f), 0.03f, 2);

                    B.Crate(m, new Vector3(bedMid - 0.25f, top + 0.01f, 0.28f), new Vector3(0.6f, 0.42f, 0.55f), 14f, key + 5);
                    Tyre(m, Matrix4x4.Translate(new Vector3(bedMid + 0.45f, top + 0.12f, -0.3f)), 0.32f, 0.22f, hub: true);
                    Plank(m, new Vector3(bedMid + 0.1f, top + 0.46f, -0.05f), new Vector3(1.5f, 0.04f, 0.19f), Quaternion.Euler(0f, 8f, 12f), key + 6);
                }

                // Wheels: one flat on every car, and none at all at the hatchback's sagging corner.
                for (int i = 0; i < 4; i++)
                {
                    float x = (i < 2 ? 1f : -1f) * length * 0.32f;
                    float z = (i % 2 == 0 ? 1f : -1f) * (Width * 0.5f - 0.08f);
                    if (sagging && i == 0) continue;

                    bool flat = i == 2 + variant % 2;
                    float squash = flat ? 0.78f : 1f;
                    Matrix4x4 local = Matrix4x4.Translate(new Vector3(x, 0.32f * squash, z)) * Matrix4x4.Scale(new Vector3(1f, squash, 1f)) *
                                      Matrix4x4.Rotate(Quaternion.Euler(90f, 0f, 0f));
                    Tyre(m, local, 0.32f, 0.22f, hub: true);
                }
            });

            if (sagging)
            {
                // Propped on bricks where the wheel was.
                for (int k = 0; k < 3; k++)
                    BrickAt(m, new Vector3(length * 0.32f + (k - 1) * 0.12f, 0.04f + (k == 1 ? 0.075f : 0f), Width * 0.5f - 0.25f),
                            Quaternion.Euler(0f, 90f + k * 12f, 0f), key + 70 + k);
            }

            // The grass and a bush coming up round it.
            Bush(m, new Vector3(-length * 0.5f + 0.25f, 0f, (variant == 0 ? 1f : -1f) * (Width * 0.5f + 0.15f)), 0.62f, key + 30, Pick(Leaves, key), 0.9f);
            for (int i = 0; i < 4; i++)
            {
                float x = (i < 2 ? 1f : -1f) * length * 0.32f + rng.Range(-0.3f, 0.3f);
                float z = (i % 2 == 0 ? 1f : -1f) * (Width * 0.5f + rng.Range(0.05f, 0.25f));
                Tuft(m, new Vector3(x, 0f, z), rng.Range(0.3f, 0.45f), key + 40 + i);
            }
        }

        // --- Scrap -------------------------------------------------------------------------------------

        static void ScrapHeap(B.Model m, int variant, int key, ref DeterministicRandom rng)
        {
            Mound(m, Vector3.zero, 1.0f, 0.34f, EarthDark, EarthFleck, key);

            switch (variant)
            {
                case 0:
                    Drum(m, Matrix4x4.Translate(new Vector3(0.45f, 0.05f, -0.3f)), 0.3f, 0.88f, DrumPaint[0], key + 1);
                    Drum(m, Matrix4x4.TRS(new Vector3(0.95f, 0f, 0.3f), Quaternion.Euler(0f, 0f, -6f), Vector3.one), 0.3f, 0.88f, DrumPaint[1], key + 2);
                    DrumLying(m, new Vector3(-0.5f, 0.1f, 0.6f), 75f, 0.3f, 0.88f, DrumPaint[2], key + 3);
                    B.Crate(m, new Vector3(0.62f, 0.93f, -0.05f), new Vector3(0.6f, 0.45f, 0.55f), 28f, key + 6);
                    Sheet(m, new Vector3(0.1f, 1.3f, -0.1f), new Vector3(-0.75f, 0.03f, -0.35f), Vector3.forward, 1.35f, B.Tin, key + 4);
                    for (int i = 0; i < 4; i++)
                        Tyre(m, Matrix4x4.Translate(new Vector3(-0.95f + i * 0.03f, 0.11f + i * 0.2f, -0.75f)), 0.34f, 0.2f, hub: false);
                    Plank(m, new Vector3(0.2f, 0.42f, 0.75f), new Vector3(1.8f, 0.04f, 0.2f), Quaternion.Euler(0f, 20f, 10f), key + 5);
                    break;

                case 1:
                    B.Crate(m, new Vector3(0.3f, 0.12f, 0.25f), new Vector3(0.85f, 0.62f, 0.75f), 18f, key + 1);
                    B.Crate(m, new Vector3(0.45f, 0.76f, 0.3f), new Vector3(0.55f, 0.42f, 0.5f), -12f, key + 2);

                    // A pallet lying askew.
                    Placed(m, new Vector3(-0.7f, 0.22f, -0.35f), Quaternion.Euler(-8f, 30f, 6f), () =>
                    {
                        for (int i = 0; i < 4; i++) Plank(m, new Vector3(0f, 0.08f, -0.45f + i * 0.3f), new Vector3(1.2f, 0.03f, 0.16f), Quaternion.identity, key + 10 + i);
                        for (int i = 0; i < 3; i++) Plank(m, new Vector3(-0.5f + i * 0.5f, 0.02f, 0f), new Vector3(0.12f, 0.08f, 1.05f), Quaternion.identity, key + 20 + i);
                    });

                    Tyre(m, Matrix4x4.TRS(new Vector3(1.05f, 0.34f, -0.45f), Quaternion.Euler(0f, 40f, 0f) * Quaternion.Euler(90f, 0f, -14f), Vector3.one),
                         0.34f, 0.2f, hub: true);
                    Sheet(m, new Vector3(-0.12f, 0.7f, 0.75f), new Vector3(-0.85f, 0.03f, 1.0f), Vector3.right, 1.0f, B.RoofTin, key + 4);
                    m.Tube((p, n) => Trim * (0.9f + 0.1f * Noise(p * 3f)), new Vector3(-1.2f, 0.07f, 0.2f), new Vector3(0.1f, 0.12f, -1.0f), 0.07f, 0.07f, 10);
                    break;

                default:
                {
                    // An old fridge on its back, door to the sky.
                    Quaternion turn = Quaternion.Euler(0f, 25f, 0f);
                    Vector3 at = new Vector3(0.05f, 0.32f, 0.05f);
                    Vector3 seed = Seed(key);

                    var fridge = new SmoothMesh(1);
                    fridge.RoundedBox(0, at, new Vector3(0.72f, 0.62f, 1.4f), 0.07f, turn, 2);
                    m.Add(fridge, (p, n) => Color.Lerp(Enamel * (0.95f + 0.06f * Noise(p * 4f)), Rust, Band(Noise(p * 2.5f + seed), 0.6f, 0.68f) * 0.8f));
                    m.Box(Grille, at + turn * new Vector3(0f, 0.31f, -0.2f), new Vector3(0.66f, 0.012f, 0.02f), 0.004f, turn);
                    m.Box(Trim, at + turn * new Vector3(0.28f, 0.33f, 0.15f), new Vector3(0.04f, 0.03f, 0.3f), 0.012f, turn);

                    Drum(m, Matrix4x4.Translate(new Vector3(-0.85f, 0.03f, 0.35f)), 0.3f, 0.88f, DrumPaint[1], key + 2);
                    for (int i = 0; i < 2; i++)
                        Tyre(m, Matrix4x4.Translate(new Vector3(0.95f, 0.11f + i * 0.2f, -0.65f)), 0.34f, 0.2f, hub: false);
                    Sheet(m, new Vector3(-0.5f, 0.75f, -0.3f), new Vector3(-0.95f, 0.03f, -0.95f), Vector3.right, 0.95f, B.Tin, key + 4);
                    Plank(m, new Vector3(0.3f, 0.7f, 0.65f), new Vector3(1.4f, 0.04f, 0.18f), Quaternion.Euler(0f, -30f, 18f), key + 5);
                    break;
                }
            }

            Tufts(m, 1.25f, 4, key + 50, ref rng);
        }

        // --- Rubble ------------------------------------------------------------------------------------

        static void Rubble(B.Model m, int variant, int key, ref DeterministicRandom rng)
        {
            switch (variant)
            {
                case 0:
                {
                    Mound(m, Vector3.zero, 0.95f, 0.55f, Gravel, GravelFleck, key);

                    // A slab of floor leant on the heap, its reinforcing torn out of the top edge.
                    Quaternion slabTurn = Quaternion.Euler(0f, 30f, 24f);
                    Vector3 slab = new Vector3(0.25f, 0.58f, 0.1f);
                    Chunk(m, slab, new Vector3(1.3f, 0.16f, 0.95f), slabTurn, key + 1);
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 edge = slab + slabTurn * new Vector3(-0.62f, 0.02f, -0.3f + i * 0.3f);
                        RebarAt(m, edge, slabTurn * new Vector3(-1f, 0.6f, 0.1f * (i - 1)), 0.55f, key + 10 + i);
                    }

                    ScatterChunks(m, 10, 1.25f, 0.42f, key + 20, ref rng);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = rng.Range(0f, Mathf.PI * 2f), r = rng.Range(0.7f, 1.5f);
                        BrickAt(m, new Vector3(Mathf.Cos(a) * r, 0.04f, Mathf.Sin(a) * r), Quaternion.Euler(rng.Range(-10f, 10f), rng.Range(0f, 180f), 0f), key + 40 + i);
                    }

                    break;
                }

                case 1:
                {
                    // What is left of a brick wall: a footing, a few courses broken off unevenly, the rest at its foot.
                    Chunk(m, new Vector3(0f, 0.09f, 0f), new Vector3(2.3f, 0.18f, 0.34f), Quaternion.identity, key);

                    const int Rows = 8;
                    for (int row = 0; row < Rows; row++)
                    {
                        float stagger = row % 2 == 0 ? 0f : 0.12f;
                        float reach = 2.1f * (1f - row / (float)Rows * rng.Range(0.35f, 0.95f));
                        float start = -1.05f + stagger + (row > 3 ? rng.Range(0f, 0.5f) : 0f);
                        for (float x = start; x < -1.05f + reach - 0.1f; x += 0.245f)
                        {
                            if (row > 2 && rng.Chance(0.08f)) continue;
                            BrickAt(m, new Vector3(x + 0.115f, 0.18f + 0.04f + row * 0.085f, 0f),
                                    Quaternion.Euler(0f, rng.Range(-2f, 2f), rng.Range(-1.5f, 1.5f)), key + row * 31 + (int)(x * 10f));
                        }
                    }

                    Mound(m, new Vector3(0.2f, 0f, 0.55f), 0.7f, 0.32f, Gravel, GravelFleck, key + 3);
                    for (int i = 0; i < 14; i++)
                    {
                        Vector3 at = new Vector3(rng.Range(-1f, 1.2f), 0.05f + rng.Range(0f, 0.12f), rng.Range(0.3f, 1.2f));
                        BrickAt(m, at, Quaternion.Euler(rng.Range(-25f, 25f), rng.Range(0f, 180f), rng.Range(-25f, 25f)), key + 200 + i);
                    }

                    ScatterChunks(m, 4, 1.1f, 0.2f, key + 20, ref rng);
                    break;
                }

                default:
                {
                    // Slabs fallen on each other, and a length of concrete pipe beside them.
                    Chunk(m, new Vector3(0f, 0.09f, 0f), new Vector3(1.45f, 0.18f, 1.05f), Quaternion.Euler(0f, 10f, 0f), key);
                    Chunk(m, new Vector3(0.15f, 0.3f, 0.1f), new Vector3(1.2f, 0.16f, 0.9f), Quaternion.Euler(6f, 38f, -9f), key + 1);
                    Chunk(m, new Vector3(-0.1f, 0.52f, -0.05f), new Vector3(0.95f, 0.15f, 0.75f), Quaternion.Euler(-12f, -15f, 14f), key + 2);
                    RebarAt(m, new Vector3(0.6f, 0.55f, 0.35f), new Vector3(0.8f, 0.9f, 0.3f), 0.5f, key + 5);

                    Pipe(m, new Vector3(1.25f, 0.33f, -0.75f), Quaternion.Euler(0f, 70f, 0f) * Quaternion.Euler(0f, 0f, 90f), 0.33f, 1.6f, key + 6);
                    ScatterChunks(m, 7, 1.35f, 0.15f, key + 20, ref rng);
                    break;
                }
            }

            Tufts(m, 1.35f, 4, key + 50, ref rng);
        }

        /// <summary>Loose lumps of concrete scattered round a heap, lying on it where they fell on it.</summary>
        static void ScatterChunks(B.Model m, int count, float reach, float heapHeight, int key, ref DeterministicRandom rng)
        {
            for (int i = 0; i < count; i++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f), r = rng.Range(0.2f, reach);
                var size = new Vector3(rng.Range(0.3f, 0.7f), rng.Range(0.16f, 0.36f), rng.Range(0.26f, 0.55f));
                float onHeap = heapHeight * Mathf.Max(0f, 1f - r * r / (reach * reach));
                Chunk(m, new Vector3(Mathf.Cos(a) * r, onHeap + size.y * 0.35f, Mathf.Sin(a) * r), size,
                      Quaternion.Euler(rng.Range(-25f, 25f), rng.Range(0f, 360f), rng.Range(-25f, 25f)), key + i);
            }
        }

        /// <summary>A hollow concrete pipe, its axis local Y turned by <paramref name="turn"/>, centred at <paramref name="centre"/>.</summary>
        static void Pipe(B.Model m, Vector3 centre, Quaternion turn, float radius, float length, int key)
        {
            float inner = radius * 0.74f;
            var profile = new List<Vector2>
            {
                new Vector2(inner, 0f), new Vector2(radius, 0f), new Vector2(radius, 0f), new Vector2(radius * 1.02f, length * 0.5f),
                new Vector2(radius, length), new Vector2(radius, length), new Vector2(inner, length), new Vector2(inner, length),
                new Vector2(inner, 0f),
            };

            Color concrete = Pick(Concrete, key);
            Placed(m, Matrix4x4.TRS(centre, turn, Vector3.one) * Matrix4x4.Translate(Vector3.down * length * 0.5f), () =>
                m.Lathe((p, n) =>
                {
                    Vector2 radial = new Vector2(p.x, p.z);
                    bool inside = Vector2.Dot(new Vector2(n.x, n.z), radial) < -0.01f;
                    return inside ? ConcreteInside : concrete * (0.93f + 0.1f * Noise(p * 3f));
                }, Vector3.zero, profile, 18));
        }

        // --- Barricades --------------------------------------------------------------------------------

        static void Barricade(B.Model m, int variant, int key, ref DeterministicRandom rng)
        {
            switch (variant)
            {
                case 0:
                {
                    // A curved wall of sandbags, three courses, the top one gone in places.
                    const float Radius = 2.1f;
                    const float Arc = 80f;
                    for (int row = 0; row < 3; row++)
                    {
                        int bags = Mathf.RoundToInt(Arc * Mathf.Deg2Rad * Radius / 0.54f) - row;
                        for (int i = 0; i < bags; i++)
                        {
                            if (row == 2 && rng.Chance(0.3f)) continue;

                            float t = (i + row * 0.5f) / Mathf.Max(1, bags - 1 + row * 0.5f);
                            float a = (-Arc * 0.5f + t * Arc) * Mathf.Deg2Rad;
                            Vector3 at = new Vector3(Mathf.Sin(a) * Radius, 0.1f + row * 0.19f, Mathf.Cos(a) * Radius - Radius + 0.4f);
                            SandbagAt(m, at, Quaternion.Euler(rng.Range(-4f, 4f), a * Mathf.Rad2Deg + 90f + rng.Range(-6f, 6f), rng.Range(-3f, 3f)),
                                      key + row * 50 + i);
                        }
                    }

                    DrumLying(m, new Vector3(-0.9f, 0f, -0.6f), 20f, 0.3f, 0.88f, DrumPaint[0], key + 2);
                    Plank(m, new Vector3(0.6f, 0.45f, -0.25f), new Vector3(1.7f, 0.04f, 0.2f), Quaternion.Euler(0f, -12f, 28f), key + 3);
                    break;
                }

                case 1:
                {
                    // Two striped road barriers, one knocked flat, and sandbags heaped at their feet.
                    Sawhorse(m, new Vector3(-0.55f, 0f, 0f), Quaternion.Euler(0f, 8f, 0f), key);
                    Placed(m, new Vector3(0.95f, 0.12f, -0.6f), Quaternion.Euler(0f, -35f, 0f) * Quaternion.Euler(84f, 0f, 0f), () =>
                        Sawhorse(m, Vector3.zero, Quaternion.identity, key + 9));

                    for (int i = 0; i < 7; i++)
                    {
                        Vector3 at = new Vector3(rng.Range(-1.2f, 0.4f), 0.1f + (i > 4 ? 0.19f : 0f), rng.Range(0.35f, 0.8f));
                        SandbagAt(m, at, Quaternion.Euler(rng.Range(-6f, 6f), rng.Range(-30f, 30f), rng.Range(-4f, 4f)), key + 30 + i);
                    }

                    break;
                }

                default:
                {
                    // Drums with planks laid across them and sandbags weighting the planks down.
                    Drum(m, Matrix4x4.Translate(new Vector3(-0.95f, 0f, 0f)), 0.3f, 0.88f, DrumPaint[2], key + 1);
                    Drum(m, Matrix4x4.Translate(new Vector3(0.95f, 0f, 0.08f)), 0.3f, 0.88f, DrumPaint[1], key + 2);
                    for (int i = 0; i < 3; i++)
                        Plank(m, new Vector3(0f, 0.91f + i * 0.045f, -0.18f + i * 0.17f), new Vector3(2.5f, 0.04f, 0.19f),
                              Quaternion.Euler(0f, rng.Range(-3f, 3f), rng.Range(-1.5f, 1.5f)), key + 10 + i);

                    SandbagAt(m, new Vector3(-0.6f, 1.12f, 0f), Quaternion.Euler(0f, 84f, 0f), key + 20);
                    SandbagAt(m, new Vector3(0.55f, 1.12f, -0.05f), Quaternion.Euler(0f, 96f, 0f), key + 21);
                    for (int i = 0; i < 5; i++)
                        SandbagAt(m, new Vector3(-0.9f + i * 0.45f, 0.1f, -0.55f), Quaternion.Euler(0f, rng.Range(-8f, 8f), 0f), key + 30 + i);
                    SandbagAt(m, new Vector3(0.4f, 0.12f, 0.75f), Quaternion.Euler(8f, 40f, -6f), key + 40);
                    break;
                }
            }

            Tufts(m, 1.4f, 4, key + 50, ref rng);
        }

        /// <summary>A road barrier: an A-frame of planks each end and a board across the top in red and white.</summary>
        static void Sawhorse(B.Model m, Vector3 foot, Quaternion turn, int key)
        {
            Placed(m, foot, turn, () =>
            {
                for (int end = -1; end <= 1; end += 2)
                {
                    for (int leg = -1; leg <= 1; leg += 2)
                        Plank(m, new Vector3(end * 0.72f, 0.42f, leg * 0.16f), new Vector3(0.07f, 0.9f, 0.1f), Quaternion.Euler(leg * -12f, 0f, 0f), key + end + leg * 3);
                }

                const int Stripes = 6;
                for (int i = 0; i < Stripes; i++)
                {
                    float x = -0.85f + (i + 0.5f) * 1.7f / Stripes;
                    m.Box(i % 2 == 0 ? StripeRed : StripeWhite, new Vector3(x, 0.8f, 0f), new Vector3(1.7f / Stripes + 0.002f, 0.2f, 0.04f), 0.008f);
                }

                Plank(m, new Vector3(0f, 0.45f, 0f), new Vector3(1.55f, 0.1f, 0.035f), Quaternion.identity, key + 7);
            });
        }
    }
}
