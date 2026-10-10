using System;
using FoodStreet.Visitors.EditorTools;
using LastOrders.Ambience;
using UnityEditor;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using S = LastOrders.EditorTools.SoupKitchenModel;
using V = FoodStreet.Visitors.EditorTools.VehicleKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The level's wild birds, modelled in code like the hens: a round body, a neck and head that move
    /// on their own, each wing in two pieces - the arm from the shoulder and the hand from the wrist -
    /// so it can fold flat along the side or open out and beat, a tail, and legs that tuck up in
    /// flight. A little bigger than life so they read from the game camera; muted, in the feathers'
    /// own shades, no toy eyes.
    ///
    /// Three pigeons - blue-bar, dark chequer, a pale one - a house sparrow, a carrion crow, and a bat
    /// for the night. Every one is the same build with its proportions and paint changed, facing +Z,
    /// right +X, its feet on y 0 (the bat, which never lands, is centred on its body).
    /// <see cref="BirdBody"/> moves the parts and <see cref="AmbientBird"/> flies them.
    /// </summary>
    internal static class BirdModel
    {
        internal enum Breed { BlueBar, Chequer, Pale, Sparrow, Crow, Bat }

        internal static readonly Breed[] Pigeons = { Breed.BlueBar, Breed.Chequer, Breed.Pale };

        internal static string PrefabPath(Breed breed) => $"{AmbienceKit.PrefabFolder}/Bird_{breed}.prefab";

        const string Folder = AmbienceKit.BirdFolder;

        // --- The pigeon's measurements (metres); the others are this, reshaped and scaled ------------------

        static readonly Vector3 NeckBase = new Vector3(0f, 0.19f, 0.075f);
        // Tucked in over the back, so the folded wing lies along the body rather than standing off it.
        static readonly Vector3 Shoulder = new Vector3(0.048f, 0.198f, 0.05f);
        static readonly Vector3 TailRoot = new Vector3(0f, 0.165f, -0.125f);
        static readonly Vector3 Hips = new Vector3(0f, 0.09f, 0.015f);
        const float ArmSpan = 0.152f;

        sealed class Design
        {
            public AmbientBird.Kind Kind;
            public float Scale = 1f;

            // Reshaping, 1 = pigeon.
            public float Plump = 1f, Head = 1f, Beak = 1f, BeakThick = 1f, Neck = 1f, Tail = 1f, Span = 1f, TipTaper = 0.6f;

            public Color Back, Belly, HeadTop, Throat, Sheen, SheenAlt, Wing, WingUnder, Bar, Primaries, TailTop, TailBand;
            public Color Bill, Cere, Eye, Shank, Cheek, Nape;
            public float Bars, Chequer, Streaks, Glint;

            public BirdBody.Gait Gait = BirdBody.Gait.Walk;
            public float Step = 0.07f, Bob = 0.028f, Caws, Pecks = 0.5f, FlapDegrees = 50f;
            public float Cruise = 7.5f, Turn = 140f, FlapHz = 6.5f, Glide = 0.35f, Walk = 0.4f, Hop, Fright = 3.8f;
            public bool Bounding;
        }

        static Design Of(Breed breed)
        {
            switch (breed)
            {
                case Breed.Chequer:
                    return Pigeon(new Design
                    {
                        Back = B.Hex("6E7178"), Belly = B.Hex("7C8087"), HeadTop = B.Hex("5D6168"), Wing = B.Hex("777A81"),
                        Bar = B.Hex("44464B"), Chequer = 0.8f, Bars = 1f, Primaries = B.Hex("45474C"), TailTop = B.Hex("6A6D74"),
                    });
                case Breed.Pale:
                    return Pigeon(new Design
                    {
                        Back = B.Hex("ADA9A1"), Belly = B.Hex("BAB6AE"), HeadTop = B.Hex("8F8D89"), Wing = B.Hex("B3AEA5"),
                        Bar = B.Hex("8A7867"), Chequer = 0.45f, Bars = 0.5f, Primaries = B.Hex("8C8780"), TailTop = B.Hex("A29E97"),
                    });
                case Breed.Sparrow:
                    return new Design
                    {
                        Kind = AmbientBird.Kind.Sparrow, Scale = 0.68f,
                        Plump = 1.15f, Head = 1.28f, Beak = 0.5f, BeakThick = 1.7f, Neck = 0.55f, Tail = 0.95f, Span = 0.85f, TipTaper = 0.5f,
                        Back = B.Hex("8A6B4E"), Belly = B.Hex("B3AB9C"), HeadTop = B.Hex("7D7A75"), Throat = B.Hex("3C3631"),
                        Wing = B.Hex("7F6348"), WingUnder = B.Hex("A99E8E"), Bar = B.Hex("CFC6B4"), Bars = 0.6f,
                        Primaries = B.Hex("54443A"), TailTop = B.Hex("5E4B3B"), TailBand = B.Hex("5E4B3B"),
                        Bill = B.Hex("3E3833"), Cere = B.Hex("3E3833"), Eye = B.Hex("221D1A"), Shank = B.Hex("9A8571"),
                        Cheek = B.Hex("C6BDAC"), Nape = B.Hex("7B5538"), Streaks = 1f,
                        Gait = BirdBody.Gait.Hop, Step = 0.05f, Bob = 0f, Pecks = 0.55f, FlapDegrees = 62f,
                        Cruise = 6.5f, Turn = 260f, FlapHz = 13f, Glide = 0f, Bounding = true, Walk = 0f, Hop = 0.15f, Fright = 4.5f,
                    };
                case Breed.Crow:
                    return new Design
                    {
                        Kind = AmbientBird.Kind.Crow, Scale = 1.32f,
                        Plump = 0.95f, Head = 1.02f, Beak = 1.9f, BeakThick = 1.75f, Neck = 0.9f, Tail = 1.15f, Span = 1.12f, TipTaper = 0.3f,
                        Back = B.Hex("36383D"), Belly = B.Hex("2C2D31"), HeadTop = B.Hex("313237"), Throat = B.Hex("2C2D31"),
                        Sheen = B.Hex("3D4250"), SheenAlt = B.Hex("3A3F48"), Glint = 0.5f,
                        Wing = B.Hex("393B41"), WingUnder = B.Hex("2E2F33"), Bar = B.Hex("393B41"), Primaries = B.Hex("26272B"),
                        TailTop = B.Hex("2F3035"), TailBand = B.Hex("2A2B2F"),
                        Bill = B.Hex("2A2A2D"), Cere = B.Hex("2A2A2D"), Eye = B.Hex("17171A"), Shank = B.Hex("2E2E31"),
                        Step = 0.1f, Bob = 0.012f, Caws = 0.3f, Pecks = 0.35f, FlapDegrees = 40f,
                        Cruise = 6.5f, Turn = 110f, FlapHz = 4f, Glide = 0.3f, Walk = 0.45f, Fright = 6f,
                    };
                case Breed.Bat:
                    return new Design
                    {
                        Kind = AmbientBird.Kind.Bat, Scale = 1f,
                        Back = B.Hex("57483F"), Belly = B.Hex("4C3F37"), Wing = B.Hex("4A3E38"), WingUnder = B.Hex("41362F"),
                        Primaries = B.Hex("3E332D"), Bar = B.Hex("2E2724"), HeadTop = B.Hex("4F4139"), Eye = B.Hex("161312"),
                        FlapDegrees = 62f, Cruise = 5.5f, Turn = 280f, FlapHz = 9f, Glide = 0f, Fright = 2f,
                    };
                default:
                    return Pigeon(new Design
                    {
                        Back = B.Hex("8C9198"), Belly = B.Hex("9DA1A7"), HeadTop = B.Hex("757A83"), Wing = B.Hex("9EA3A9"),
                        Bar = B.Hex("3F4246"), Bars = 1f, Primaries = B.Hex("4D5056"), TailTop = B.Hex("878B92"),
                    });
            }
        }

        /// <summary>The pigeons' shared parts of the paint and the way they move.</summary>
        static Design Pigeon(Design d)
        {
            d.Kind = AmbientBird.Kind.Pigeon;
            d.Throat = d.HeadTop;
            d.Sheen = B.Hex("62786A");
            d.SheenAlt = B.Hex("74667C");
            d.Glint = 1f;
            d.WingUnder = Color.Lerp(d.Belly, Color.white, 0.15f);
            d.TailBand = B.Hex("3A3C40");
            d.Bill = B.Hex("3C3A39");
            d.Cere = B.Hex("CDC6BC");
            d.Eye = B.Hex("8F5536");
            d.Shank = B.Hex("A27268");
            return d;
        }

        // --- Building ------------------------------------------------------------------------------------

        /// <summary>The meshes and a prefab per breed. Returns a line for the log.</summary>
        internal static string Apply()
        {
            ContentGenerator.EnsureFolder(Folder);
            ContentGenerator.EnsureFolder(AmbienceKit.PrefabFolder);
            int built = 0, vertices = 0;

            foreach (Breed breed in (Breed[])Enum.GetValues(typeof(Breed)))
            {
                Design d = Of(breed);
                bool bat = d.Kind == AmbientBird.Kind.Bat;

                Mesh body = LivestockKit.Save(bat ? BatBody(d) : Body(d), Folder, $"Bird_{breed}_Body");
                Mesh head = LivestockKit.Save(bat ? BatHead(d) : Head(d), Folder, $"Bird_{breed}_Head");
                Mesh armL = LivestockKit.Save(Arm(d, -1f), Folder, $"Bird_{breed}_Arm_L");
                Mesh armR = LivestockKit.Save(Arm(d, 1f), Folder, $"Bird_{breed}_Arm_R");
                Mesh handL = LivestockKit.Save(Hand(d, -1f), Folder, $"Bird_{breed}_Hand_L");
                Mesh handR = LivestockKit.Save(Hand(d, 1f), Folder, $"Bird_{breed}_Hand_R");
                Mesh tail = bat ? null : LivestockKit.Save(Tail(d), Folder, $"Bird_{breed}_Tail");
                Mesh legs = bat ? null : LivestockKit.Save(Legs(d), Folder, $"Bird_{breed}_Legs");

                LivestockKit.Prefab(PrefabPath(breed), $"Bird_{breed}", (root, created) => Rig(root, d, body, head, armL, armR, handL, handR, tail, legs));
                built++;
                vertices = Mathf.Max(vertices, body.vertexCount + head.vertexCount + 2 * (armL.vertexCount + handL.vertexCount) +
                                               (tail != null ? tail.vertexCount : 0) + (legs != null ? legs.vertexCount : 0));
            }

            return $"Birds: {built} breeds, up to {vertices} vertices each.";
        }

        static void Rig(GameObject root, Design d, Mesh body, Mesh head, Mesh armL, Mesh armR, Mesh handL, Mesh handR, Mesh tail, Mesh legs)
        {
            float s = d.Scale;
            bool bat = d.Kind == AmbientBird.Kind.Bat;
            Vector3 shoulder = bat ? BatShoulder : Shoulder;
            Vector3 wrist = new Vector3(WristReach(d), 0f, 0f);

            Transform model = LivestockKit.Part(root.transform, "Model", Vector3.zero, body);
            Transform headPart = LivestockKit.Part(model, "Head", (bat ? BatNeck : NeckBase) * s, head);
            Transform left = LivestockKit.Part(model, "Wing_L", new Vector3(-shoulder.x, shoulder.y, shoulder.z) * s, armL);
            Transform right = LivestockKit.Part(model, "Wing_R", shoulder * s, armR);
            Transform tipLeft = LivestockKit.Part(left, "Tip_L", new Vector3(-wrist.x, 0f, 0f) * s, handL);
            Transform tipRight = LivestockKit.Part(right, "Tip_R", wrist * s, handR);
            Transform tailPart = tail != null ? LivestockKit.Part(model, "Tail", TailRoot * s, tail) : null;
            Transform legsPart = legs != null ? LivestockKit.Part(model, "Legs", Hips * s, legs, false) : null;

            var bodyPart = root.GetComponent<BirdBody>();
            if (bodyPart == null) bodyPart = root.AddComponent<BirdBody>();
            bodyPart.Configure(model, headPart, left, right, tipLeft, tipRight, tailPart, legsPart, d.Gait, d.Step * s, d.Bob * s,
                               d.Caws, d.Pecks, d.FlapDegrees);

            var bird = root.GetComponent<AmbientBird>();
            if (bird == null) bird = root.AddComponent<AmbientBird>();
            bird.Configure(d.Kind, bodyPart, d.Cruise, d.Turn, d.FlapHz, d.Glide, d.Bounding, d.Walk, d.Hop, d.Fright);
        }

        // --- Paint ---------------------------------------------------------------------------------------

        /// <summary>Lighter on top where the light falls, the under colour beneath, a soft mottle over all.</summary>
        static Func<Vector3, Vector3, Color> Feathers(Color top, Color under, int key, float mottle = 0.07f)
        {
            Vector3 seed = V.Seed(key);
            return (p, n) =>
            {
                Color c = Color.Lerp(under, top, LivestockKit.Smooth((n.y + 0.45f) / 0.9f));
                return c * (1f - mottle * 0.5f + mottle * V.Noise(p * 60f + seed));
            };
        }

        static B.Model Scaled(Design d, Vector3 pivot) =>
            new B.Model { Transform = Matrix4x4.Scale(Vector3.one * d.Scale) * Matrix4x4.Translate(-pivot) };

        // --- Bird parts ----------------------------------------------------------------------------------

        static B.Model Body(Design d)
        {
            B.Model m = Scaled(d, Vector3.zero);
            Func<Vector3, Vector3, Color> feathers = Feathers(d.Back, d.Belly, 11 + (int)d.Kind);
            Vector3 seed = V.Seed(23 + (int)d.Kind);
            Func<Vector3, Vector3, Color> paint = (p, n) =>
            {
                Color c = feathers(p, n);

                // A sparrow's back: dark streaks running fore and aft.
                if (d.Streaks > 0f && n.y > 0.2f)
                {
                    float streak = V.Band(V.Noise(new Vector3(p.x * 90f, p.y * 20f, p.z * 14f) + seed), 0.58f, 0.66f);
                    c = Color.Lerp(c, d.Back * 0.62f, streak * d.Streaks * LivestockKit.Smooth((n.y - 0.2f) / 0.4f));
                }

                return c;
            };

            // One smooth body: the breast full and low in front, tapering behind to the rump.
            var body = new Loft
            {
                Back = -0.135f, Front = 0.105f, CapBack = 0.075f, CapFront = 0.08f, Round = 2.1f, CapRound = 2.05f,
                Section = z =>
                {
                    float t = Mathf.InverseLerp(-0.1f, 0.075f, z);
                    float bottom = Mathf.Lerp(0.115f, 0.078f, LivestockKit.Smooth(t * 1.2f));
                    float top = Mathf.Lerp(0.19f, 0.212f, t);
                    float half = Mathf.Lerp(0.045f, 0.07f, LivestockKit.Smooth(t * 1.4f)) * d.Plump;
                    return new Vector3(bottom, top + (d.Plump - 1f) * 0.03f, half);
                },
                Tuck = y => 1f - 0.1f * Mathf.Max(0f, y) * Mathf.Max(0f, y),
                Warp = p =>
                {
                    // The back sloping down into the tail.
                    float back = Mathf.Max(0f, -p.z - 0.05f);
                    return p - new Vector3(0f, back * 0.25f, 0f);
                },
            };
            body.Build(m, paint, 22, 18);
            return m;
        }

        /// <summary>Neck, head, bill and eyes, about the base of the neck.</summary>
        static B.Model Head(Design d)
        {
            B.Model m = Scaled(d, NeckBase);
            Func<Vector3, Vector3, Color> neckPaint = Feathers(d.HeadTop, d.Throat, 31 + (int)d.Kind);
            Func<Vector3, Vector3, Color> neck = (p, n) =>
            {
                Color c = neckPaint(p, n);

                // The pigeon's neck: green and mauve sheen shifting round it.
                if (d.Glint > 0f)
                {
                    float shift = 0.5f + 0.5f * Mathf.Sin(n.x * 3.2f + p.y * 55f);
                    Color sheen = Color.Lerp(d.Sheen, d.SheenAlt, shift);
                    float band = LivestockKit.Smooth((p.y - 0.18f) / 0.03f) * (1f - LivestockKit.Smooth((p.y - 0.235f) / 0.02f));
                    c = Color.Lerp(c, sheen, band * 0.75f * d.Glint);
                }

                return c;
            };

            Vector3 headCentre = new Vector3(0f, 0.258f, 0.125f + (d.Neck - 1f) * 0.02f);
            if (d.Neck > 0.7f)
                S.Ball(m, neck, new Vector3(0f, 0.212f, 0.098f), new Vector3(0.04f, 0.05f, 0.043f) * d.Neck, Quaternion.Euler(18f, 0f, 0f), 14, 10);
            else
                S.Ball(m, neck, new Vector3(0f, 0.214f, 0.095f), new Vector3(0.05f, 0.045f, 0.045f), Quaternion.identity, 14, 10);

            Vector3 seed = V.Seed(41 + (int)d.Kind);
            float r = 0.036f * d.Head;
            S.Ball(m, (p, n) =>
            {
                Color c = Color.Lerp(d.Throat, d.HeadTop, LivestockKit.Smooth((n.y + 0.5f) / 0.9f));
                if (d.Cheek.a > 0f && d.Kind == AmbientBird.Kind.Sparrow)
                {
                    // A cock sparrow: grey crown, chestnut behind the eye, pale cheek, black bib.
                    float side = Mathf.Abs(n.x);
                    c = Color.Lerp(c, d.Nape, LivestockKit.Smooth((side - 0.45f) / 0.25f) * LivestockKit.Smooth((-n.z + 0.1f) / 0.4f) * 0.9f);
                    c = Color.Lerp(c, d.Cheek, LivestockKit.Smooth((side - 0.55f) / 0.2f) * LivestockKit.Smooth((n.z + 0.1f) / 0.4f) * LivestockKit.Smooth((-n.y + 0.1f) / 0.4f));
                    c = Color.Lerp(c, d.Throat, LivestockKit.Smooth((-n.y - 0.2f) / 0.3f) * LivestockKit.Smooth((n.z - 0.1f) / 0.4f));
                }

                return c * (0.97f + 0.06f * V.Noise(p * 70f + seed));
            }, headCentre, new Vector3(r, r * 0.95f, r * 1.12f), Quaternion.identity, 16, 12);

            // The bill: a tapered cone, longer and heavier on the crow, a stubby seed-cracker on the sparrow.
            float front = headCentre.z + r * 1.0f;
            Vector3 root = new Vector3(0f, headCentre.y - r * 0.12f, front - 0.004f);
            Vector3 tip = root + new Vector3(0f, -0.008f * d.Beak, 0.03f * d.Beak);
            m.Tube((p, n) => n.y < -0.3f ? d.Bill * 0.85f : d.Bill, root, tip, 0.0085f * d.BeakThick, 0.0022f * d.BeakThick, 10);
            if (d.Kind == AmbientBird.Kind.Pigeon)
                S.Ball(m, (p, n) => d.Cere, root + new Vector3(0f, 0.004f, 0.002f), new Vector3(0.0075f, 0.0055f, 0.008f), Quaternion.identity, 8, 6);

            for (int side = -1; side <= 1; side += 2)
                S.Ball(m, (p, n) => d.Eye, new Vector3(side * r * 0.74f, headCentre.y + r * 0.22f, headCentre.z + r * 0.42f),
                       new Vector3(0.0055f, 0.0065f, 0.0065f) * Mathf.Sqrt(d.Head), Quaternion.identity, 8, 6);
            return m;
        }

        static float WristReach(Design d) => (d.Kind == AmbientBird.Kind.Bat ? BatArm : ArmSpan) * d.Span;

        /// <summary>The arm: shoulder to wrist, spread flat out to the side. About the shoulder.</summary>
        static B.Model Arm(Design d, float side)
        {
            var m = new B.Model { Transform = Matrix4x4.Scale(Vector3.one * d.Scale) };
            float span = WristReach(d);
            bool bat = d.Kind == AmbientBird.Kind.Bat;
            Vector3 seed = V.Seed(51 + (int)d.Kind);

            Func<Vector3, Vector3, Color> paint = (p, n) =>
            {
                Color c = n.y > 0f ? d.Wing : d.WingUnder;
                c *= 0.95f + 0.08f * V.Noise(p * 70f + seed);
                if (n.y > 0f && !bat)
                {
                    // Pigeons' two dark bars across the coverts; a sparrow's one pale one.
                    float bar1 = 1f - LivestockKit.Smooth(Mathf.Abs(p.z + 0.03f) / 0.006f);
                    float bar2 = 1f - LivestockKit.Smooth(Mathf.Abs(p.z + 0.054f) / 0.006f);
                    float bars = Mathf.Max(bar1, d.Kind == AmbientBird.Kind.Sparrow ? 0f : bar2) * LivestockKit.Smooth(Mathf.Abs(p.x) / 0.05f);
                    c = Color.Lerp(c, d.Bar, bars * d.Bars);

                    if (d.Chequer > 0f)
                    {
                        float spot = V.Band(V.Noise(p * 160f + seed), 0.62f, 0.7f);
                        c = Color.Lerp(c, d.Bar, spot * d.Chequer * 0.8f);
                    }

                    if (d.Streaks > 0f)
                    {
                        float streak = V.Band(V.Noise(new Vector3(p.x * 30f, 0f, p.z * 120f) + seed), 0.55f, 0.65f);
                        c = Color.Lerp(c, d.Wing * 0.6f, streak * 0.7f);
                    }
                }

                // Bats: the arm bones darker through the membrane.
                if (bat) c = Color.Lerp(c, d.Bar, 1f - LivestockKit.Smooth(Mathf.Abs(p.z - 0.004f) / 0.006f));
                return c;
            };

            if (bat)
            {
                Membrane(m, paint, side, 0f, span, 0.07f, 0.06f, 0f);
            }
            else
            {
                S.Ball(m, paint, new Vector3(side * span * 0.5f, 0f, -0.018f), new Vector3(span * 0.54f, 0.012f, 0.056f),
                       Quaternion.identity, 16, 10);
            }

            return m;
        }

        /// <summary>The hand: wrist to tip, the long flight feathers, swept back and tapering. About the wrist.</summary>
        static B.Model Hand(Design d, float side)
        {
            var m = new B.Model { Transform = Matrix4x4.Scale(Vector3.one * d.Scale) };
            bool bat = d.Kind == AmbientBird.Kind.Bat;
            float span = (bat ? 0.12f : 0.2f) * d.Span;
            Vector3 seed = V.Seed(61 + (int)d.Kind);

            Func<Vector3, Vector3, Color> paint = (p, n) =>
            {
                float t = Mathf.Clamp01(Mathf.Abs(p.x) / span);
                Color top = Color.Lerp(d.Wing, d.Primaries, LivestockKit.Smooth((t - 0.15f) / 0.5f));
                Color c = n.y > 0f ? top : Color.Lerp(d.WingUnder, d.Primaries, t * 0.6f);

                // The primaries in rows, a feather wide.
                if (!bat) c *= Mathf.Repeat((p.z + p.x * 0.4f) * 70f, 1f) < 0.5f ? 0.93f : 1f;
                else c = Color.Lerp(c, d.Bar, 1f - LivestockKit.Smooth(Mathf.Abs(Mathf.Repeat(p.z * 28f + t * 2f, 1f) - 0.5f) / 0.08f));
                return c * (0.96f + 0.06f * V.Noise(p * 80f + seed));
            };

            if (bat)
            {
                Membrane(m, paint, side, 0f, span, 0.06f, 0.012f, 0.03f);
                return m;
            }

            float taper = d.TipTaper;
            m.Warp = p =>
            {
                float t = Mathf.Clamp01(Mathf.Abs(p.x) / span);
                return new Vector3(p.x, p.y * (1f - 0.5f * t), p.z * (1f - taper * t) - 0.05f * t * t);
            };
            S.Ball(m, paint, new Vector3(side * span * 0.5f, 0f, -0.03f), new Vector3(span * 0.53f, 0.009f, 0.047f),
                   Quaternion.identity, 16, 10);
            m.Warp = null;
            return m;
        }

        /// <summary>The tail: a flattened fan off the rump, a dark band across the end on a pigeon. About the rump.</summary>
        static B.Model Tail(Design d)
        {
            B.Model m = Scaled(d, TailRoot);
            float length = 0.085f * d.Tail;
            Vector3 centre = TailRoot + new Vector3(0f, -0.012f, -length);
            S.Ball(m, (p, n) =>
            {
                Color c = n.y > 0f ? d.TailTop : d.TailTop * 0.85f;
                float end = LivestockKit.Smooth((-(p.z - centre.z) - length * 0.55f) / (length * 0.2f));
                return Color.Lerp(c, d.TailBand, end);
            }, centre, new Vector3(0.042f * (d.Kind == AmbientBird.Kind.Crow ? 1.15f : 1f), 0.012f, length), Quaternion.Euler(-8f, 0f, 0f), 14, 10);
            return m;
        }

        /// <summary>Two short legs and their toes. About the hips.</summary>
        static B.Model Legs(Design d)
        {
            B.Model m = Scaled(d, Hips);
            // Always down to the ground: the feet stand on y 0 whatever the bird.
            float drop = Hips.y - 0.008f;
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = new Vector3(side * 0.022f, Hips.y - 0.005f, Hips.z);
                var ankle = new Vector3(side * 0.026f, Hips.y - drop, Hips.z + 0.012f);
                m.Tube((p, n) => d.Shank, hip, ankle, 0.0075f, 0.0055f, 6);
                foreach (float splay in new[] { -0.012f, 0f, 0.012f })
                    m.Tube((p, n) => d.Shank * 0.92f, ankle, ankle + new Vector3(splay + side * 0.004f, -0.004f, 0.03f), 0.004f, 0.003f, 5);
                m.Tube((p, n) => d.Shank * 0.92f, ankle, ankle + new Vector3(0f, -0.004f, -0.018f), 0.0035f, 0.0028f, 5);
            }

            return m;
        }

        // --- The bat ------------------------------------------------------------------------------------

        static readonly Vector3 BatShoulder = new Vector3(0.022f, 0.008f, 0.012f);
        static readonly Vector3 BatNeck = new Vector3(0f, 0.004f, 0.035f);
        const float BatArm = 0.1f;

        static B.Model BatBody(Design d)
        {
            var m = new B.Model();
            Func<Vector3, Vector3, Color> fur = Feathers(d.Back, d.Belly, 71, 0.12f);
            S.Ball(m, fur, new Vector3(0f, 0f, 0f), new Vector3(0.028f, 0.024f, 0.048f), Quaternion.identity, 14, 10);
            // A little tail membrane between the feet.
            m.Warp = p => new Vector3(p.x, p.y, p.z - Mathf.Abs(p.x) * 0.3f);
            S.Ball(m, (p, n) => d.Wing, new Vector3(0f, -0.004f, -0.055f), new Vector3(0.03f, 0.003f, 0.022f), Quaternion.identity, 10, 6);
            m.Warp = null;
            return m;
        }

        static B.Model BatHead(Design d)
        {
            var m = new B.Model { Transform = Matrix4x4.Translate(-BatNeck) };
            Func<Vector3, Vector3, Color> fur = Feathers(d.HeadTop, d.Belly, 73, 0.1f);
            S.Ball(m, fur, new Vector3(0f, 0.006f, 0.05f), new Vector3(0.019f, 0.018f, 0.02f), Quaternion.identity, 12, 8);
            m.Tube((p, n) => d.HeadTop * 0.9f, new Vector3(0f, 0.004f, 0.062f), new Vector3(0f, 0.001f, 0.078f), 0.009f, 0.005f, 8);
            for (int side = -1; side <= 1; side += 2)
            {
                // Big upright ears, the bat's outline.
                S.Ball(m, (p, n) => d.Bar, new Vector3(side * 0.011f, 0.03f, 0.045f), new Vector3(0.0065f, 0.016f, 0.004f),
                       Quaternion.Euler(-15f, 0f, side * -18f), 8, 6);
                S.Ball(m, (p, n) => d.Eye, new Vector3(side * 0.011f, 0.011f, 0.066f), new Vector3(0.003f, 0.003f, 0.003f), Quaternion.identity, 6, 4);
            }

            return m;
        }

        /// <summary>A bat's wing membrane between <paramref name="from"/> and <paramref name="to"/> out from the
        /// pivot: thin, the trailing edge scalloped between the fingers.</summary>
        static void Membrane(B.Model m, Func<Vector3, Vector3, Color> paint, float side, float from, float to, float chordRoot,
                             float chordTip, float sweep)
        {
            float span = to - from;
            m.Warp = p =>
            {
                float t = Mathf.Clamp01((Mathf.Abs(p.x) - from) / span);
                float chord = Mathf.Lerp(chordRoot, chordTip, t) / chordRoot;
                float z = p.z * chord - sweep * t * t;
                if (p.z < 0f) z += 0.008f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f)) * chord;
                return new Vector3(p.x, p.y, z);
            };
            S.Ball(m, paint, new Vector3(side * (from + span * 0.5f), 0f, -chordRoot * 0.25f), new Vector3(span * 0.52f, 0.004f, chordRoot * 0.5f),
                   Quaternion.identity, 16, 8);
            m.Warp = null;
        }

        [MenuItem("Last Orders/Ambience/Build Birds (Models & Prefabs)", priority = 120)]
        static void Run() => Debug.Log("[Last Orders] " + Apply());
    }
}
