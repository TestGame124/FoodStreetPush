using System;
using System.Collections.Generic;
using LastOrders.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using B = LastOrders.EditorTools.BuildingKit;
using E = LastOrders.EditorTools.EnvKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The suburbs' houses in the game's toon shapes, modelled in code like the compound's
    /// buildings: smooth low-poly parts rounded on every edge, colour painted into the vertices, one
    /// material for the whole house.
    ///
    /// The proportions are pushed the way a toon house is: a steep, thick roof with deep eaves, laid
    /// in fat rows of shingles with a light trim edge round it and a fat ridge capped with balls;
    /// chunky lapped siding between heavy corner boards; big windows in thick frames under a fat
    /// lintel; a stacked brick chimney that leans. Abandoned shows through the house's own parts -
    /// the whole thing leaning a little, boards hanging off a nail, windows boarded,
    /// a shutter hanging, a hole with the rafters showing, a tarp held down by a tyre, a gutter come
    /// away, moss - never through a global tint.
    ///
    /// Each house is planned by <see cref="AbandonedHouseBuilder"/> (its footprint, wing, garage,
    /// door, porch, roof shape, colours), so the yard it planned - path, drive, fence, car - meets the
    /// house where it expects to. Details are picked from the lot's own name, so a house comes out
    /// the same every build. Meshes are saved as assets, one per lot, in a folder per scene under
    /// <see cref="Folder"/>.
    ///
    /// Lot space as the planner's: X along the street, +Z toward it.
    /// </summary>
    public static class ToonHouseBuilder
    {
        public const string Folder = B.Folder + "/Houses";
        public const string HouseChild = "House";
        public const string ShedChild = "GardenShed";

        // The old builder's floor: the path and the step meet the house at this height.
        const float Sill = 0.35f;
        const float WallT = 0.24f;
        const float Over = 0.55f;
        const float GableOver = 0.42f;
        const float Deck = 0.16f;
        const float Tile = 0.075f;
        const float RowStep = 0.5f;
        const float TileTilt = 7f;
        const float GarageSill = 0.12f;
        const float GarageHeight = 2.35f;

        /// <summary>Which way is toward the camera, flat on the ground. It sits south-east of what it looks at.</summary>
        static readonly Vector3 TowardCamera = new Vector3(0.6f, 0f, -0.8f);

        // --- The palette ---------------------------------------------------------------------------
        //
        // The planner's colours, each with its toon paint, so a house keeps its colour: a shade cleaner
        // and mid-tone, since a smooth vertex-painted face reads lighter than a textured one and the
        // whites must not clip.

        static readonly Color[] WallPaint = { Hex("8D7967"), Hex("AEAB9F"), Hex("7592A7"), Hex("A99A80"), Hex("8D9C85"), Hex("955D52") };

        // No green: from above a green roof is lost in the grass. The planner's moss-green roof is a grey teal here.
        static readonly Color[] RoofPaint = { Hex("586478"), Hex("6E5E52"), Hex("7A544E"), Hex("56656F"), Hex("586868"), Hex("60656F"), Hex("4B5265") };
        static readonly Color[] TrimPaint = { Hex("BDB8AB"), Hex("AEA99C"), Hex("5C656C"), Hex("C0BBAE") };
        static readonly Color[] DoorPaint = { Hex("7E3D33"), Hex("3F5D72"), Hex("4E5F3C"), Hex("6E4C30"), Hex("33393E") };
        static readonly Color[] CharWall = { Hex("4A4440"), Hex("57504B"), Hex("3E3935") };
        static readonly Color CharTrim = Hex("3B3632");
        static readonly Color CharRoof = Hex("34302D");
        static readonly Color CharDeck = Hex("2C2826");
        static readonly Color Sarking = Hex("54473C");
        static readonly Color Moss = Hex("4A5734");
        static readonly Color[] Brick = { Hex("8C5747"), Hex("7C4B3F"), Hex("965F4E") };
        static readonly Color Stone = Hex("918D86");
        static readonly Color CapStone = Hex("8F8D87");
        static readonly Color Hole = Hex("1E1C1A");
        static readonly Color Pot = Hex("5A5550");
        static readonly Color TarpBlue = Hex("56697A");
        static readonly Color Rubber = Hex("2D2D30");
        static readonly Color GarageWhite = Hex("A7A399");
        static readonly Color GutterGrey = Hex("7E8286");

        static Color Hex(string rgb) => PrimitiveKit.Hex(rgb);

        /// <summary>The entry of <paramref name="to"/> standing where <paramref name="c"/>'s nearest match stands in <paramref name="from"/>.</summary>
        static Color Recolour(Color[] from, Color[] to, Color c)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < from.Length; i++)
            {
                float d = Mathf.Abs(from[i].r - c.r) + Mathf.Abs(from[i].g - c.g) + Mathf.Abs(from[i].b - c.b);
                if (d < bestDistance) { bestDistance = d; best = i; }
            }

            return to[best % to.Length];
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // --- The plan ------------------------------------------------------------------------------

        /// <summary>What the planner decided about one lot's house.</summary>
        public sealed class Plan
        {
            public string Name;
            public AbandonedHouseBuilder.Lot Lot;
            public Rect Main;
            public Rect? Wing;
            public Rect? Garage;

            /// <summary>The main walls, floor to eaves.</summary>
            public float Height = 2.6f;

            public float DoorX;
            public bool Collapsed;
            public bool Burnt;
            public bool Porch;
            public bool Hipped;

            /// <summary>The planner's roof pitch, 26-36 degrees; a toon roof is a good deal steeper.</summary>
            public float Pitch = 30f;

            /// <summary>How many patches of damage the roof has.</summary>
            public int Patches;

            public Vector2? Chimney;
            public Color Siding;
            public Color Trim;
            public Color Door;
            public Color Roof;
            public ShedPlan Shed;
        }

        /// <summary>A shed at the bottom of the garden.</summary>
        public sealed class ShedPlan
        {
            public Vector2 At;
            public float Yaw;
            public Vector2 Size;
            public float Height;
        }

        // --- Building ------------------------------------------------------------------------------

        /// <summary>
        /// Where a lot's meshes are saved: a folder per scene, since every level's suburbs number their
        /// lots from Lot_000 and one level's houses must not be saved over another's.
        /// </summary>
        public static string FolderFor(Transform lot)
        {
            string scene = lot.gameObject.scene.name;
            return $"{Folder}/{(string.IsNullOrEmpty(scene) ? "Untitled" : scene)}";
        }

        /// <summary>Builds the house on <paramref name="lot"/> as its child <see cref="HouseChild"/>, replacing one already there.</summary>
        public static GameObject Build(Transform lot, Plan plan)
        {
            var builder = new Builder(plan, "house");
            if (plan.Collapsed) builder.Ruin();
            else builder.House();

            Mesh mesh = builder.M.Save($"{FolderFor(lot)}/House_{plan.Name}.asset", $"House_{plan.Name}");
            return Attach(lot, HouseChild, mesh, Vector3.zero, Quaternion.identity);
        }

        /// <summary>Builds the plan's garden shed on <paramref name="lot"/> as its child <see cref="ShedChild"/>.</summary>
        public static GameObject BuildShed(Transform lot, Plan plan)
        {
            if (plan.Shed == null) return null;

            var builder = new Builder(plan, "shed");
            float turn = builder.Shed(plan.Shed);
            Mesh mesh = builder.M.Save($"{FolderFor(lot)}/Shed_{plan.Name}.asset", $"Shed_{plan.Name}");
            return Attach(lot, ShedChild, mesh, new Vector3(plan.Shed.At.x, 0f, plan.Shed.At.y),
                          Quaternion.Euler(0f, plan.Shed.Yaw + turn, 0f));
        }

        static GameObject Attach(Transform lot, string name, Mesh mesh, Vector3 position, Quaternion rotation)
        {
            Transform old = lot.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = new GameObject(name);
            go.transform.SetParent(lot, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            B.Show(go, mesh);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        static Matrix4x4 Frame(Vector3 origin, Vector3 x, Vector3 y, Vector3 z)
        {
            var f = Matrix4x4.identity;
            f.SetColumn(0, x);
            f.SetColumn(1, y);
            f.SetColumn(2, z);
            f.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1f));
            return f;
        }

        /// <summary>A wall's own space: X along it, Y up, Z out of it, the origin on its outer face at the ground.</summary>
        static Matrix4x4 Face(Vector3 origin, Vector3 outward) => Frame(origin, Vector3.Cross(Vector3.up, outward), Vector3.up, outward);

        static readonly Vector3[] Sides = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        enum Damage { Hole, Tarp, Bare, Moss }

        struct Patch
        {
            public Damage Kind;
            public float X, Z, HalfX, HalfZ;
        }

        /// <summary>
        /// One roof slope: a thick deck from the eave up to its top edge, in its own space - X along
        /// the eave, Y out of the slope, Z up it - laid out by where its eave starts and which ways are up
        /// and out. <see cref="HalfTop"/> below <see cref="HalfBottom"/> narrows it to a hipped side.
        /// </summary>
        struct SlopeSpec
        {
            public Vector3 Eave, Up, Normal;
            public float HalfBottom, HalfTop, Run, Deck, Tile;
        }

        sealed class RoofInfo
        {
            public Matrix4x4 Frame;
            public Quaternion Turn;
            public float L, W, P, Rise, Top;
            public bool Hipped, AlongX;
        }

        sealed class Builder
        {
            public readonly B.Model M = new B.Model();

            readonly Plan plan;
            readonly Quaternion lotTurn;
            readonly bool burnt;
            readonly bool detailed;
            readonly Color paint, trim, door, roof, deckTop;
            readonly List<(Rect r, float top)> blocks = new List<(Rect, float)>();
            DeterministicRandom rng;
            Matrix4x4 lean = Matrix4x4.identity;

            public Builder(Plan plan, string salt)
            {
                this.plan = plan;
                lotTurn = Quaternion.Euler(0f, plan.Lot.Yaw, 0f);
                burnt = plan.Burnt;
                detailed = plan.Lot.Detailed;
                rng = new DeterministicRandom(DeterministicRandom.StableHash(plan.Name + "/" + salt));

                paint = burnt ? Recolour(E.Burnt, CharWall, plan.Siding) : Recolour(E.HouseWall, WallPaint, plan.Siding);
                trim = burnt ? CharTrim : Recolour(AbandonedHouseBuilder.TrimColours, TrimPaint, plan.Trim);
                door = Recolour(AbandonedHouseBuilder.DoorColours, DoorPaint, plan.Door);
                roof = burnt ? CharRoof : Recolour(AbandonedHouseBuilder.RoofColours, RoofPaint, plan.Roof);
                deckTop = burnt ? CharDeck : Sarking;
                M.Shade = Shade;
            }

            // --- Plumbing ------------------------------------------------------------------------------

            void At(Matrix4x4 local, Func<Vector3, Vector3> warp = null)
            {
                M.Transform = lean * local;
                M.Warp = warp;
            }

            void AtHouse() => At(Matrix4x4.identity);

            void Box(Color c, Vector3 at, Vector3 size, float radius, Quaternion rotation) => M.Box(c, at, size, radius, rotation, 1);

            void Box(Color c, Vector3 at, Vector3 size, float radius) => M.Box(c, at, size, radius, Quaternion.identity, 1);

            float Jit(float amount) => 1f + amount * rng.Range(-1f, 1f);

            bool Visible(Vector3 outward) => Vector3.Dot(lotTurn * outward, TowardCamera) > -0.3f;

            /// <summary>Darker at the foot of a wall and up under the eaves, as a painter would shade it; sooty on a burnt house.</summary>
            float Shade(Vector3 p, Vector3 n)
            {
                float k = Mathf.Lerp(0.8f, 1f, Smooth(p.y / 0.7f));
                if (Mathf.Abs(n.y) > 0.5f) return k;

                foreach (var (r, top) in blocks)
                {
                    if (p.x < r.xMin - 0.4f || p.x > r.xMax + 0.4f || p.z < r.yMin - 0.4f || p.z > r.yMax + 0.4f || p.y > top + 0.05f)
                        continue;

                    k *= Mathf.Lerp(1f, 0.84f, Smooth((p.y - (top - 0.55f)) / 0.55f));
                    if (burnt) k *= Mathf.Lerp(1f, 0.7f, Smooth((p.y - 1f) / Mathf.Max(0.5f, top - 1f)));
                    break;
                }

                return k;
            }

            /// <summary>
            /// A long piece only ever seen from the front and above - a shingle, a siding board: a
            /// <paramref name="profile"/> of (y, z) points swept along X, its ends left open where they are
            /// buried or covered. A tenth of the vertices of a rounded box, with the same soft edge.
            /// </summary>
            void Strip(Func<Vector3, Vector3, Color> paint, Vector3 centre, Quaternion rotation, float length, Vector2[] profile)
            {
                int count = profile.Length;
                M.Surface(paint, 1, count - 1, (u, v) =>
                {
                    float f = v * (count - 1);
                    int i = Mathf.Min((int)f, count - 2);
                    Vector2 a = Vector2.Lerp(profile[i], profile[i + 1], f - i);
                    return centre + rotation * new Vector3(length * 0.5f - u * length, a.x, a.y);
                });
            }

            /// <summary>A shingle row's section: across its top from the back, round the lip, down its front.</summary>
            static Vector2[] TileProfile(float length, float thickness)
            {
                float r = Mathf.Min(0.035f, thickness * 0.45f);
                float front = -length * 0.5f, top = thickness * 0.5f;
                return new[]
                {
                    new Vector2(top, length * 0.5f), new Vector2(top, front + r), new Vector2(top - r * 0.29f, front + r * 0.29f),
                    new Vector2(top - r, front), new Vector2(-top, front),
                };
            }

            /// <summary>A board's section: in under its bottom edge, up its rounded face, back over its top.</summary>
            static Vector2[] BoardProfile(float height, float depth)
            {
                float r = Mathf.Min(0.022f, depth * 0.45f);
                float b = -height * 0.5f, t = height * 0.5f, back = -depth * 0.5f, face = depth * 0.5f;
                return new[]
                {
                    new Vector2(b, back), new Vector2(b, face - r), new Vector2(b + r * 0.29f, face - r * 0.29f), new Vector2(b + r, face),
                    new Vector2(t - r, face), new Vector2(t - r * 0.29f, face - r * 0.29f), new Vector2(t, face - r), new Vector2(t, back),
                };
            }

            void Blob(Func<Vector3, Vector3, Color> colour, Vector3 centre, Vector3 radii)
            {
                M.Surface(colour, 10, 7, (u, v) =>
                {
                    float th = v * Mathf.PI, ph = u * Mathf.PI * 2f;
                    return centre + Vector3.Scale(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), -Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)), radii);
                }, true);
            }

            // --- The house -----------------------------------------------------------------------------

            public void House()
            {
                Rect main = plan.Main;
                float h = plan.Height;
                float top = Sill + h;

                // Nothing stands quite straight after a year of nobody looking after it.
                if (rng.Chance(burnt ? 0.8f : 0.5f))
                {
                    lean = Matrix4x4.identity;
                    lean.m01 = rng.Range(-0.022f, 0.022f);
                    lean.m21 = rng.Range(-0.018f, 0.018f);
                }

                AtHouse();

                float wingTop = top - 0.25f;
                float garageTop = GarageSill + GarageHeight;
                blocks.Add((main, top));
                if (plan.Wing.HasValue) blocks.Add((plan.Wing.Value, wingTop));
                if (plan.Garage.HasValue) blocks.Add((plan.Garage.Value, garageTop));

                Plinth(main, Sill);
                if (plan.Wing.HasValue) Plinth(plan.Wing.Value, Sill);
                if (plan.Garage.HasValue) Plinth(plan.Garage.Value, GarageSill);

                Walls(main, Sill, h, -1, plan.DoorX - main.center.x, false);

                // The wing's front and the garage's inside are the main house's walls.
                if (plan.Wing.HasValue) Walls(plan.Wing.Value, Sill, h - 0.25f, 0, null, false);
                if (plan.Garage.HasValue)
                {
                    Rect g = plan.Garage.Value;
                    Walls(g, GarageSill, GarageHeight, g.center.x > main.center.x ? 2 : 3, null, true);
                }

                float pitch = Mathf.Clamp(plan.Pitch + 12f, 36f, 48f);
                RoofInfo mainRoof = Roof(main, top, pitch, plan.Hipped, plan.Patches, true);
                if (plan.Wing.HasValue) Roof(plan.Wing.Value, wingTop, pitch - 4f, false, 0, false);
                if (plan.Garage.HasValue) Roof(plan.Garage.Value, garageTop, 30f, false, 0, false);

                Entrance(main, mainRoof);

                float chimneyEnd = 0f;
                if (plan.Chimney.HasValue)
                {
                    chimneyEnd = Mathf.Sign(Vector3.Dot(Quaternion.Inverse(mainRoof.Turn) *
                                                        new Vector3(plan.Chimney.Value.x - main.center.x, 0f, plan.Chimney.Value.y - main.center.y),
                                                        Vector3.right) + 1e-3f);
                    Chimney(mainRoof, chimneyEnd);
                }

                if (!burnt && detailed && !plan.Hipped && rng.Chance(0.3f))
                    Antenna(mainRoof, chimneyEnd != 0f ? -chimneyEnd : (rng.Chance(0.5f) ? 1f : -1f));
            }

            /// <summary>A stone plinth under a block, a few chunky stones standing proud of it on the sides the camera sees.</summary>
            void Plinth(Rect r, float height)
            {
                AtHouse();
                Box(Stone * 0.95f, new Vector3(r.center.x, height * 0.5f, r.center.y),
                    new Vector3(r.width + WallT + 0.18f, height, r.height + WallT + 0.18f), 0.06f);

                if (!detailed || height < 0.2f) return;

                int stones = rng.Range(3, 7);
                for (int i = 0; i < stones; i++)
                {
                    int side = rng.Range(0, 4);
                    Vector3 n = Sides[side];
                    if (!Visible(n)) continue;

                    float along = rng.Range(-0.45f, 0.45f) * (side < 2 ? r.width : r.height);
                    float out_ = (side < 2 ? r.height : r.width) * 0.5f + WallT * 0.5f + 0.1f;
                    Vector3 at = new Vector3(r.center.x, 0f, r.center.y) + n * out_ + Vector3.Cross(Vector3.up, n) * along;
                    at.y = height * rng.Range(0.3f, 0.45f);
                    Box(Stone * Jit(0.08f), at, new Vector3(rng.Range(0.4f, 0.7f), height * rng.Range(0.6f, 0.85f), 0.2f), 0.06f,
                        Quaternion.LookRotation(n) * Quaternion.Euler(0f, rng.Range(-8f, 8f), rng.Range(-6f, 6f)));
                }
            }

            /// <summary>
            /// A block's walls: a solid core, and on each side the camera sees lapped boards, a skirting,
            /// a frieze, windows - and the door, at <paramref name="doorT"/> along the front - with heavy
            /// corner boards up every corner. <paramref name="joined"/> is the side another block covers.
            /// </summary>
            void Walls(Rect r, float sill, float h, int joined, float? doorT, bool garageDoor)
            {
                AtHouse();
                Box(paint * 0.88f, new Vector3(r.center.x, sill + h * 0.5f, r.center.y), new Vector3(r.width + WallT, h, r.height + WallT), 0.05f);

                for (int i = 0; i < 4; i++)
                {
                    if (i == joined) continue;
                    Vector3 n = Sides[i];
                    if (!Visible(n)) continue;

                    bool front = i == 0;
                    float length = (i < 2 ? r.width : r.height) + WallT;
                    var origin = new Vector3(r.center.x + n.x * (r.width + WallT) * 0.5f, 0f, r.center.y + n.z * (r.height + WallT) * 0.5f);
                    Matrix4x4 face = Face(origin, n);

                    At(face);
                    Boards(length, sill, h);
                    Box(trim * 0.86f, new Vector3(0f, sill + 0.085f, 0.045f), new Vector3(length, 0.17f, 0.09f), 0.03f);
                    Box(trim, new Vector3(0f, sill + h - 0.1f, 0.05f), new Vector3(length + 0.02f, 0.21f, 0.1f), 0.035f);

                    float? doorAt = front ? doorT : null;
                    bool bigDoor = front && garageDoor;
                    if (doorAt.HasValue) Door(face, doorAt.Value, sill);
                    if (bigDoor) GarageDoor(face, sill);

                    int windows = Mathf.FloorToInt((length - 0.8f) / 2.7f);
                    if (windows < 1 && length > 2.4f) windows = 1;
                    for (int k = 0; k < windows; k++)
                    {
                        float t = -length * 0.5f + (k + 0.5f) / windows * length + rng.Range(-0.25f, 0.25f);
                        if (doorAt.HasValue && Mathf.Abs(t - doorAt.Value) < 1.45f) continue;
                        if (bigDoor && Mathf.Abs(t) < 2.15f) continue;
                        if (Mathf.Abs(t) > length * 0.5f - 0.95f) continue;
                        Window(face, t, sill + Mathf.Min(1.55f, h - 0.85f));
                    }
                }

                AtHouse();
                foreach (float x in new[] { r.xMin - WallT * 0.5f, r.xMax + WallT * 0.5f })
                    foreach (float z in new[] { r.yMin - WallT * 0.5f, r.yMax + WallT * 0.5f })
                        Box(trim, new Vector3(x, sill + h * 0.5f + 0.02f, z), new Vector3(0.27f, h + 0.04f, 0.27f), 0.06f);
            }

            /// <summary>Lapped boards in the house paint, every other row a shade darker, the odd one missing or hanging off one nail.</summary>
            void Boards(float length, float sill, float h)
            {
                float y0 = sill + 0.17f, y1 = sill + h - 0.2f;
                int rows = Mathf.Max(3, Mathf.RoundToInt((y1 - y0) / (detailed ? 0.34f : 0.5f)));
                float rh = (y1 - y0) / rows;
                float from = -length * 0.5f + 0.13f, to = length * 0.5f - 0.13f;
                float missing = burnt ? 0.12f : 0.035f, hanging = burnt ? 0.05f : 0.025f;
                Quaternion lap = Quaternion.Euler(-6f, 0f, 0f);

                for (int row = 0; row < rows; row++)
                {
                    float y = y0 + rh * (row + 0.5f);
                    float cut = to - from > 6.5f ? Mathf.Lerp(from, to, 0.5f + rng.Range(-0.2f, 0.2f)) : to;

                    for (int piece = 0; piece < 2; piece++)
                    {
                        float a = piece == 0 ? from : cut, b = piece == 0 ? cut : to;
                        if (b - a < 0.2f) continue;
                        if (rng.Chance(missing)) continue;

                        Color c = paint * ((row & 1) == 0 ? 1f : 0.95f) * Jit(0.035f);
                        var centre = new Vector3((a + b) * 0.5f, y, 0.036f);
                        Quaternion q = lap;

                        if (b - a < 4.5f && rng.Chance(hanging))
                        {
                            // Lost the nail at one end and swung down.
                            bool left = rng.Chance(0.5f);
                            var pivot = new Vector3(left ? a + 0.1f : b - 0.1f, y, 0f);
                            Quaternion swing = Quaternion.Euler(0f, 0f, (left ? -1f : 1f) * rng.Range(7f, 14f));
                            centre = pivot + swing * (centre - pivot) + Vector3.forward * 0.03f;
                            q = swing * lap;
                        }

                        Strip((p, n) => c, centre, q, b - a - 0.025f, BoardProfile(rh + 0.03f, 0.062f));
                    }
                }
            }

            void Window(Matrix4x4 face, float t, float y)
            {
                At(face * Matrix4x4.TRS(new Vector3(t, y, 0f), Quaternion.Euler(0f, 0f, rng.Range(-2.5f, 2.5f)), Vector3.one));

                const float w = 1.1f, hgt = 1.25f, bar = 0.14f;
                float damage = burnt ? 0.55f : 0.2f;
                float roll = rng.NextFloat();
                bool boarded = roll < damage + 0.3f;
                bool dark = !boarded && roll < damage + 0.45f;

                // A thick frame, the pane - or the dark behind a broken one - laid on its face.
                Box(trim, new Vector3(0f, 0f, 0.075f), new Vector3(w + bar * 2f, hgt + bar * 2f, 0.1f), 0.04f);
                if (boarded || dark) Box(Hole, new Vector3(0f, 0f, 0.13f), new Vector3(w, hgt, 0.02f), 0.008f);
                else Glass(w, hgt, 0.128f);

                // A sill well out from the wall and a fat lintel over it.
                Box(trim * 0.92f, new Vector3(0f, -hgt * 0.5f - bar - 0.04f, 0.13f), new Vector3(w + 0.42f, 0.09f, 0.22f), 0.035f);
                Box(trim, new Vector3(0f, hgt * 0.5f + bar + 0.06f, 0.12f), new Vector3(w + 0.46f, 0.13f, 0.17f), 0.04f);

                if (boarded)
                {
                    int planks = rng.Range(2, 4);
                    for (int k = 0; k < planks; k++)
                    {
                        float py = Mathf.Lerp(-hgt * 0.3f, hgt * 0.3f, k / (float)(planks - 1)) + rng.Range(-0.06f, 0.06f);
                        Box(B.Plank(rng.Range(0, 99)) * Jit(0.05f), new Vector3(rng.Range(-0.05f, 0.05f), py, 0.17f + k * 0.012f),
                            new Vector3(w + 0.38f, 0.19f, 0.05f), 0.02f, Quaternion.Euler(0f, 0f, rng.Range(-22f, 22f)));
                    }
                }
                else if (!dark)
                {
                    Box(trim, new Vector3(0f, 0f, 0.14f), new Vector3(0.07f, hgt, 0.04f), 0.015f);
                    Box(trim, new Vector3(0f, 0.08f, 0.14f), new Vector3(w, 0.07f, 0.04f), 0.015f);
                }

                if (detailed && !boarded && rng.Chance(0.3f)) Shutters(w, hgt);
            }

            /// <summary>The pane, a sky-coloured gleam slanting across it.</summary>
            void Glass(float w, float h, float z)
            {
                M.Surface((p, n) =>
                {
                    float gleam = Mathf.Exp(-Mathf.Pow(p.x * 0.8f + p.y - 0.1f, 2f) / 0.03f);
                    return Color.Lerp(B.Glass, B.GlassSky, 0.2f + 0.5f * gleam + 0.2f * (p.y / h + 0.5f));
                }, 4, 4, (u, v) => new Vector3(w * 0.5f - u * w, -h * 0.5f + v * h, z));
            }

            void Shutters(float w, float h)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var centre = new Vector3(side * (w * 0.5f + 0.38f), 0f, 0.07f);
                    Quaternion q = Quaternion.identity;

                    if (rng.Chance(0.25f))
                    {
                        // Hanging from its top hinge, the outer edge dropped.
                        var pivot = new Vector3(side * (w * 0.5f + 0.18f), h * 0.5f, 0.07f);
                        q = Quaternion.Euler(0f, 0f, -side * rng.Range(12f, 25f));
                        centre = pivot + q * (centre - pivot) + Vector3.forward * 0.02f;
                    }

                    Box(door * Jit(0.04f), centre, new Vector3(0.42f, h + 0.12f, 0.05f), 0.02f, q);
                    Box(door * 0.84f, centre + q * new Vector3(0f, 0f, 0.03f), new Vector3(0.34f, 0.09f, 0.02f), 0.01f, q);
                }
            }

            void Door(Matrix4x4 face, float t, float sill)
            {
                At(face * Matrix4x4.Translate(new Vector3(t, sill, 0f)));
                const float w = 1f, hgt = 2.1f;

                Box(trim, new Vector3(-w * 0.5f - 0.08f, hgt * 0.5f + 0.04f, 0.09f), new Vector3(0.16f, hgt + 0.08f, 0.1f), 0.035f);
                Box(trim, new Vector3(w * 0.5f + 0.08f, hgt * 0.5f + 0.04f, 0.09f), new Vector3(0.16f, hgt + 0.08f, 0.1f), 0.035f);
                Box(trim, new Vector3(0f, hgt + 0.13f, 0.1f), new Vector3(w + 0.5f, 0.17f, 0.14f), 0.045f);

                float roll = rng.NextFloat();
                if (roll < 0.18f)
                {
                    // Off its hinges, standing on the ground and leaning on the wall beside the doorway.
                    Box(Hole, new Vector3(0f, hgt * 0.5f, 0.075f), new Vector3(w, hgt, 0.02f), 0.008f);
                    float side = rng.Chance(0.5f) ? 1f : -1f;
                    DoorLeaf(new Vector3(side * (w + 0.32f), -sill + hgt * 0.494f, 0.3f), Quaternion.Euler(-9f, rng.Range(-6f, 6f), side * 3f), w, hgt);
                }
                else if (roll < 0.36f)
                {
                    // Ajar: a dark gap and the door swung in on its hinges.
                    Box(Hole, new Vector3(0f, hgt * 0.5f, 0.075f), new Vector3(w, hgt, 0.02f), 0.008f);
                    Quaternion swing = Quaternion.Euler(0f, rng.Range(25f, 45f), 0f);
                    var hinge = new Vector3(-w * 0.5f, hgt * 0.5f, 0.06f);
                    DoorLeaf(hinge + swing * new Vector3(w * 0.5f, 0f, 0f), swing, w, hgt);
                }
                else
                {
                    DoorLeaf(new Vector3(0f, hgt * 0.5f, 0.06f), Quaternion.identity, w, hgt);
                }
            }

            void DoorLeaf(Vector3 centre, Quaternion q, float w, float hgt)
            {
                Box(door * Jit(0.04f), centre, new Vector3(w - 0.02f, hgt - 0.02f, 0.08f), 0.03f, q);
                Box(door * 0.86f, centre + q * new Vector3(0f, hgt * 0.2f, 0.045f), new Vector3(w * 0.66f, hgt * 0.34f, 0.025f), 0.02f, q);
                Box(door * 0.86f, centre + q * new Vector3(0f, -hgt * 0.22f, 0.045f), new Vector3(w * 0.66f, hgt * 0.3f, 0.025f), 0.02f, q);
                Box(B.Iron, centre + q * new Vector3(w * 0.36f, -0.02f, 0.07f), new Vector3(0.08f, 0.08f, 0.07f), 0.03f, q);
            }

            /// <summary>A roll-up door in fat ribs, sometimes stuck part-way up over the dark inside, the odd rib dented.</summary>
            void GarageDoor(Matrix4x4 face, float sill)
            {
                At(face * Matrix4x4.Translate(new Vector3(0f, sill, 0f)));
                const float w = 2.8f, hgt = 2.05f;

                Box(trim, new Vector3(-w * 0.5f - 0.08f, hgt * 0.5f + 0.03f, 0.09f), new Vector3(0.16f, hgt + 0.06f, 0.1f), 0.035f);
                Box(trim, new Vector3(w * 0.5f + 0.08f, hgt * 0.5f + 0.03f, 0.09f), new Vector3(0.16f, hgt + 0.06f, 0.1f), 0.035f);
                Box(trim, new Vector3(0f, hgt + 0.1f, 0.1f), new Vector3(w + 0.36f, 0.16f, 0.13f), 0.04f);

                float open = rng.Chance(0.3f) ? rng.Range(0.35f, 0.9f) : 0f;
                if (open > 0f) Box(Hole, new Vector3(0f, open * 0.5f, 0.075f), new Vector3(w, open, 0.02f), 0.008f);

                int ribs = Mathf.Max(2, Mathf.RoundToInt((hgt - open) / 0.41f));
                float rib = (hgt - open) / ribs;
                for (int k = 0; k < ribs; k++)
                {
                    bool dent = rng.Chance(0.15f);
                    Quaternion q = dent ? Quaternion.Euler(rng.Range(-6f, 6f), rng.Range(-3f, 3f), rng.Range(-2f, 2f)) : Quaternion.identity;
                    Color c = (burnt ? CharTrim : GarageWhite) * ((k & 1) == 0 ? 1f : 0.95f) * Jit(0.03f);
                    Box(c, new Vector3(0f, open + rib * (k + 0.5f), dent ? 0.06f : 0.075f), new Vector3(w - 0.04f, rib - 0.035f, 0.07f), 0.03f, q);
                }
            }

            // --- Roofs -----------------------------------------------------------------------------------

            /// <summary>
            /// A roof over a block, its ridge along the longer side: gabled, or hipped on all four sides.
            /// The decks are measured to the walls' outer faces, so their undersides clear the wall
            /// tops and the boards' tops are buried in them.
            /// </summary>
            RoofInfo Roof(Rect r, float top, float pitchDeg, bool hipped, int patchCount, bool main)
            {
                bool alongX = r.width >= r.height;
                float l = (alongX ? r.width : r.height) + WallT;
                float w = (alongX ? r.height : r.width) + WallT;
                float p = Mathf.Min(pitchDeg, Mathf.Atan(3.4f / (w * 0.5f)) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(p), sin = Mathf.Sin(p), tan = Mathf.Tan(p);

                var info = new RoofInfo
                {
                    Turn = Quaternion.Euler(0f, alongX ? 0f : 90f, 0f),
                    L = l,
                    W = w,
                    P = p,
                    Rise = w * 0.5f * tan,
                    Top = top,
                    Hipped = hipped,
                    AlongX = alongX,
                };
                info.Frame = Matrix4x4.TRS(new Vector3(r.center.x, top, r.center.y), info.Turn, Vector3.one);

                float g = hipped ? Over : GableOver;
                float run = (w * 0.5f + Over) / cos;
                var slopes = new List<SlopeSpec>();
                for (int s = -1; s <= 1; s += 2)
                {
                    slopes.Add(new SlopeSpec
                    {
                        Eave = new Vector3(0f, -Over * tan, s * (w * 0.5f + Over)),
                        Normal = new Vector3(0f, cos, s * sin),
                        Up = new Vector3(0f, sin, -s * cos),
                        HalfBottom = l * 0.5f + g,
                        HalfTop = hipped ? Mathf.Max(0.06f, (l - w) * 0.5f) : l * 0.5f + g,
                        Run = run,
                        Deck = Deck,
                        Tile = Tile,
                    });
                }

                if (hipped)
                {
                    for (int e = -1; e <= 1; e += 2)
                    {
                        slopes.Add(new SlopeSpec
                        {
                            Eave = new Vector3(e * (l * 0.5f + Over), -Over * tan, 0f),
                            Normal = new Vector3(e * sin, cos, 0f),
                            Up = new Vector3(-e * cos, sin, 0f),
                            HalfBottom = w * 0.5f + Over,
                            HalfTop = 0.06f,
                            Run = run,
                            Deck = Deck,
                            Tile = Tile,
                        });
                    }
                }

                // The damage goes on the slope the camera sees most of.
                int best = 0;
                float bestFacing = float.MinValue;
                for (int i = 0; i < slopes.Count; i++)
                {
                    Vector3 n = lotTurn * (info.Turn * slopes[i].Normal);
                    float facing = Vector3.Dot(n, TowardCamera) + n.y;
                    if (facing > bestFacing) { bestFacing = facing; best = i; }
                }

                List<Patch> patches = main ? Patches(slopes[best], patchCount, hipped) : null;
                for (int i = 0; i < slopes.Count; i++) Slope(info.Frame, slopes[i], i == best ? patches : null);

                float capY = info.Rise + (Deck + Tile * 0.6f) / cos;
                if (!hipped)
                {
                    Ridge(info.Frame, -(l * 0.5f + g) - 0.03f, l * 0.5f + g + 0.03f, capY, 0.15f);
                    for (int e = -1; e <= 1; e += 2) GableEnd(info, e, main);

                    if (main)
                        for (int s = -1; s <= 1; s += 2)
                            if (Visible(info.Turn * new Vector3(0f, 0f, s)) && rng.Chance(0.75f))
                                Gutter(info, s);
                }
                else
                {
                    float half = Mathf.Max(0f, (l - w) * 0.5f);
                    if (half > 0.05f) Ridge(info.Frame, -half - 0.05f, half + 0.05f, capY, 0.14f);

                    At(info.Frame);
                    Color cap = roof * 0.78f;
                    float eaveY = -Over * tan + (Deck + Tile * 0.6f) / cos;
                    for (int e = -1; e <= 1; e += 2)
                    {
                        for (int s = -1; s <= 1; s += 2)
                        {
                            var corner = new Vector3(e * (l * 0.5f + Over - 0.06f), eaveY, s * (w * 0.5f + Over - 0.06f));
                            M.Tube((q, n) => cap, corner, new Vector3(e * half, capY, 0f), 0.12f, 0.12f, 10);
                            Blob((q, n) => cap, corner, Vector3.one * 0.15f);
                        }
                    }
                }

                return info;
            }

            List<Patch> Patches(SlopeSpec s, int count, bool tapered)
            {
                var list = new List<Patch>();
                for (int i = 0; i < count; i++)
                {
                    float roll = rng.NextFloat();
                    Damage kind = burnt ? (roll < 0.65f ? Damage.Hole : Damage.Bare)
                                : roll < 0.36f ? Damage.Hole : roll < 0.6f ? Damage.Tarp : roll < 0.8f ? Damage.Bare : Damage.Moss;

                    float spread = tapered ? 0.22f : 0.42f;
                    var patch = new Patch
                    {
                        Kind = kind,
                        X = rng.Range(-spread, spread) * s.HalfBottom,
                        Z = rng.Range(0.25f, tapered ? 0.5f : 0.62f) * s.Run,
                    };

                    switch (kind)
                    {
                        case Damage.Tarp:
                            patch.HalfX = rng.Range(0.75f, 1.2f);
                            patch.HalfZ = rng.Range(0.55f, 0.9f);
                            break;
                        case Damage.Moss:
                            patch.HalfX = rng.Range(0.6f, 1.3f);
                            patch.HalfZ = rng.Range(0.4f, 0.8f);
                            break;
                        default:
                            patch.HalfX = rng.Range(0.4f, burnt ? 1.2f : 0.85f);
                            patch.HalfZ = rng.Range(0.35f, burnt ? 0.9f : 0.65f);
                            break;
                    }

                    patch.HalfX = Mathf.Min(patch.HalfX, s.HalfBottom * 0.3f);
                    patch.HalfZ = Mathf.Min(patch.HalfZ, s.Run * 0.25f);
                    list.Add(patch);
                }

                // Moss along the bottom of an old roof, where nothing else is wrong with it.
                if (!burnt && rng.Chance(0.3f))
                {
                    list.Add(new Patch
                    {
                        Kind = Damage.Moss,
                        X = rng.Range(-0.4f, 0.4f) * s.HalfBottom,
                        Z = rng.Range(0.1f, 0.25f) * s.Run,
                        HalfX = 0.8f,
                        HalfZ = 0.35f,
                    });
                }

                return list;
            }

            /// <summary>
            /// One slope: the deck, its edges painted as trim, then rows of fat shingles from the eave up,
            /// each row tipped so its lower edge stands proud of the one below - the stepped look of a toon
            /// roof - with gaps where the damage is and the damage's dressing on top.
            /// </summary>
            void Slope(Matrix4x4 frame, SlopeSpec s, List<Patch> patches)
            {
                Vector3 along = Vector3.Cross(s.Normal, s.Up);
                Matrix4x4 local = Frame(s.Eave, along, s.Normal, s.Up);

                // A hipped side narrows toward its top: everything on it is squeezed in along the eave.
                // Linear across any one piece, so boxes stay straight-edged.
                float taper = s.HalfTop / s.HalfBottom;
                Func<Vector3, Vector3> warp = null;
                if (Mathf.Abs(taper - 1f) > 1e-3f)
                {
                    float run = s.Run;
                    warp = p => new Vector3(p.x * Mathf.Lerp(1f, taper, Mathf.Clamp01(p.z / run)), p.y, p.z);
                }

                At(frame * local, warp);

                Color fascia = trim * 0.94f, soffit = trim * 0.66f;
                float length = s.Run + 0.03f;
                var deck = new SmoothMesh(1);
                deck.RoundedBox(0, new Vector3(0f, s.Deck * 0.5f, length * 0.5f - 0.02f), new Vector3(s.HalfBottom * 2f, s.Deck, length + 0.04f),
                                0.05f, Quaternion.identity, 1);
                M.Add(deck, (p, n) => n.y > 0.6f ? deckTop : n.y < -0.6f ? soffit : fascia);

                int rows = Mathf.CeilToInt((s.Run + 0.05f) / RowStep);
                float a0 = -s.HalfBottom - 0.05f, a1 = s.HalfBottom + 0.05f;
                int pieces = Mathf.Max(1, Mathf.RoundToInt((a1 - a0) / 3.4f));

                for (int row = 0; row < rows; row++)
                {
                    float z0 = row * RowStep - 0.06f;
                    float z1 = Mathf.Min(z0 + RowStep * 1.3f, s.Run + 0.02f);
                    if (z1 - z0 < 0.15f) continue;
                    float zc = (z0 + z1) * 0.5f;
                    float shade = Mathf.Lerp(0.9f, 1.05f, Mathf.Clamp01(zc / s.Run));

                    var spans = new List<Vector2>();
                    float prev = a0;
                    for (int k = 1; k <= pieces; k++)
                    {
                        float cut = k == pieces ? a1 : Mathf.Lerp(a0, a1, (k + rng.Range(-0.3f, 0.3f)) / pieces);
                        spans.Add(new Vector2(prev, cut));
                        prev = cut;
                    }

                    if (patches != null)
                    {
                        foreach (Patch patch in patches)
                        {
                            if (patch.Kind != Damage.Hole && patch.Kind != Damage.Bare) continue;
                            if (zc < patch.Z - patch.HalfZ || zc > patch.Z + patch.HalfZ) continue;
                            float ragged = rng.Range(0.75f, 1.15f);
                            spans = Cut(spans, patch.X - patch.HalfX * ragged, patch.X + patch.HalfX * ragged);
                        }
                    }

                    foreach (Vector2 span in spans)
                    {
                        float w = span.y - span.x - 0.035f;
                        if (w < 0.15f) continue;

                        // Light along the row's exposed lower edge, darkening up under the lip of the
                        // row above: the soft band that makes rows of shingles read from above.
                        Quaternion q = Quaternion.Euler(TileTilt + rng.Range(-1.5f, 1.5f), rng.Range(-0.8f, 0.8f), 0f);
                        Color c = roof * shade * Jit(0.07f);
                        float low = z0, reach = (z1 - z0) * 0.8f;
                        Strip((p, n) => c * Mathf.Lerp(1.08f, 0.8f, Mathf.Clamp01((p.z - low) / reach)),
                              new Vector3((span.x + span.y) * 0.5f, s.Deck + s.Tile * 0.5f, zc), q, w, TileProfile(z1 - z0, s.Tile));
                    }
                }

                if (patches == null) return;
                foreach (Patch patch in patches) Dress(patch, s);
            }

            static List<Vector2> Cut(List<Vector2> spans, float from, float to)
            {
                var result = new List<Vector2>();
                foreach (Vector2 s in spans)
                {
                    if (to <= s.x || from >= s.y) { result.Add(s); continue; }
                    if (from > s.x) result.Add(new Vector2(s.x, from));
                    if (to < s.y) result.Add(new Vector2(to, s.y));
                }

                return result;
            }

            /// <summary>What a patch of damage looks like, laid on the slope in its own space.</summary>
            void Dress(Patch patch, SlopeSpec s)
            {
                float tileTop = s.Deck + s.Tile;
                switch (patch.Kind)
                {
                    case Damage.Hole:
                    {
                        Box(Hole, new Vector3(patch.X, s.Deck + 0.006f, patch.Z), new Vector3(patch.HalfX * 1.85f, 0.014f, patch.HalfZ * 1.85f), 0.006f);
                        for (int k = -1; k <= 1; k += 2)
                        {
                            bool broken = k == 1 && rng.Chance(0.6f);
                            float x = patch.X + k * patch.HalfX * 0.45f;
                            Box(B.WalnutDark, new Vector3(x, s.Deck + 0.05f, patch.Z + (broken ? patch.HalfZ * 0.4f : 0f)),
                                new Vector3(0.1f, 0.09f, broken ? patch.HalfZ * 1.2f : patch.HalfZ * 2f + 0.3f), 0.02f,
                                broken ? Quaternion.Euler(-14f, rng.Range(-8f, 8f), 0f) : Quaternion.identity);
                        }

                        break;
                    }

                    case Damage.Bare:
                    {
                        int loose = rng.Range(1, 3);
                        for (int k = 0; k < loose; k++)
                        {
                            Box(roof * 0.85f * Jit(0.05f),
                                new Vector3(patch.X + rng.Range(-0.5f, 0.5f) * patch.HalfX, s.Deck + s.Tile * 0.5f + 0.02f,
                                            patch.Z + rng.Range(-0.5f, 0.5f) * patch.HalfZ),
                                new Vector3(rng.Range(0.5f, 0.9f), s.Tile, rng.Range(0.4f, 0.6f)), 0.028f,
                                Quaternion.Euler(rng.Range(-6f, 10f), rng.Range(-35f, 35f), rng.Range(-6f, 6f)));
                        }

                        break;
                    }

                    case Damage.Tarp:
                    {
                        // Clear of the shingles' lips everywhere, puffed up in the middle, its edges a shade darker where they fold over.
                        // Nailed on crooked, puffed up and creased, clear of the shingles' lips everywhere,
                        // its edges a shade darker where they fold over.
                        float y = tileTop + 0.06f;
                        float x0 = patch.X, z0 = patch.Z, hx = patch.HalfX, hz = patch.HalfZ;
                        float turn = rng.Range(-25f, 25f) * Mathf.Deg2Rad, cosT = Mathf.Cos(turn), sinT = Mathf.Sin(turn);
                        float phase = rng.Range(0f, 6f);
                        M.Surface((p, n) =>
                        {
                            float crease = 0.92f + 0.08f * Mathf.Sin(p.x * 7f + p.z * 3f + phase);
                            return TarpBlue * crease * Mathf.Lerp(1.04f, 0.86f, Mathf.Clamp01(1f - n.y * 1.2f + 0.2f));
                        }, 10, 8, (u, v) =>
                        {
                            float bulge = Mathf.Sin(u * Mathf.PI) * Mathf.Sin(v * Mathf.PI);
                            float creases = 0.03f * Mathf.Sin(u * Mathf.PI * 5f + v * 2.5f + phase) * bulge;
                            float dx = (u - 0.5f) * 2f * hx, dz = (v - 0.5f) * 2f * hz;
                            return new Vector3(x0 + dx * cosT - dz * sinT, y + 0.11f * bulge + creases, z0 + dx * sinT + dz * cosT);
                        });

                        // Held down by an old tyre.
                        var tyre = new Vector3(x0 + rng.Range(-0.3f, 0.3f) * hx, y + 0.13f, z0 + rng.Range(-0.2f, 0.3f) * hz);
                        var ring = new List<Vector2>();
                        for (int k = 0; k <= 10; k++)
                        {
                            float a = -Mathf.PI * 0.5f + k / 10f * Mathf.PI * 2f;
                            ring.Add(new Vector2(0.26f + Mathf.Cos(a) * 0.09f, Mathf.Sin(a) * 0.09f));
                        }

                        M.Lathe((p, n) => Rubber, tyre, ring, 12);
                        break;
                    }

                    case Damage.Moss:
                    {
                        // Low cushions of it along the rows, not one ball.
                        int clumps = rng.Range(4, 8);
                        for (int k = 0; k < clumps; k++)
                        {
                            float rx = rng.Range(0.16f, 0.32f), rz = rng.Range(0.12f, 0.22f);
                            var at = new Vector3(patch.X + rng.Range(-0.8f, 0.8f) * patch.HalfX, tileTop + 0.025f,
                                                 patch.Z + rng.Range(-0.8f, 0.8f) * patch.HalfZ);
                            // Grown into the roof's colour, so it reads as weathering rather than as green dots.
                            Color tuft = Color.Lerp(roof, Moss, 0.55f) * Jit(0.08f);
                            Blob((p, n) => tuft * Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(n.y)), at, new Vector3(rx, 0.04f, rz));
                        }

                        break;
                    }
                }
            }

            /// <summary>A fat round ridge capping, a ball on each end.</summary>
            void Ridge(Matrix4x4 frame, float from, float to, float y, float radius)
            {
                At(frame);
                Color c = roof * 0.78f;
                M.Surface((p, n) => c * (0.94f + 0.06f * n.y), 10, 14, (u, v) =>
                {
                    float a = u * Mathf.PI * 2f;
                    return new Vector3(Mathf.Lerp(from, to, v), y + Mathf.Cos(a) * radius, -Mathf.Sin(a) * radius);
                }, true);

                Blob((p, n) => c, new Vector3(from, y, 0f), Vector3.one * radius * 1.18f);
                Blob((p, n) => c, new Vector3(to, y, 0f), Vector3.one * radius * 1.18f);
            }

            /// <summary>The triangle under a gable: lapped boards stepping in under the roof, a round vent in the one the camera sees.</summary>
            void GableEnd(RoofInfo r, int e, bool vent)
            {
                At(r.Frame);
                float tan = Mathf.Tan(r.P);
                bool visible = Visible(r.Turn * new Vector3(e, 0f, 0f));
                float rowH = visible && detailed ? 0.42f : 0.8f;
                float x = e * (r.L * 0.5f - 0.07f);

                int row = 0;
                for (float y = 0f; y < r.Rise - 0.05f; y += rowH, row++)
                {
                    float y1 = Mathf.Min(y + rowH, r.Rise);
                    float hw = r.W * 0.5f - y1 / tan - 0.03f;
                    if (hw < 0.08f)
                    {
                        hw = (r.W * 0.5f - y / tan) * 0.45f;
                        if (hw < 0.08f) break;
                    }

                    Color c = paint * ((row & 1) == 0 ? 1f : 0.95f) * Jit(0.03f);
                    Box(c, new Vector3(x, (y + y1) * 0.5f, 0f), new Vector3(0.26f, y1 - y + 0.03f, hw * 2f), 0.025f, Quaternion.Euler(0f, 0f, e * 5f));
                }

                if (vent && visible && r.Rise * 0.42f > 0.6f) Vent(r, e);
            }

            void Vent(RoofInfo r, int e)
            {
                var c = new Vector3(e * (r.L * 0.5f + 0.1f), r.Rise * 0.42f, 0f);
                var outward = new Vector3(e, 0f, 0f);

                // The ring is turned round its own axis, so it is drawn on its side: Y along the roof's X.
                At(r.Frame * Matrix4x4.TRS(c, Quaternion.Euler(0f, 0f, 90f), Vector3.one));
                const float ri = 0.24f, ro = 0.36f, t = 0.05f;
                M.Lathe((p, n) => trim, Vector3.zero, new List<Vector2>
                {
                    new Vector2(ri, -t), new Vector2(ro, -t), new Vector2(ro, -t), new Vector2(ro, t), new Vector2(ro, t),
                    new Vector2(ri, t), new Vector2(ri, t), new Vector2(ri, -t),
                }, 14);

                // The dark behind it, and louvres across it in front of the dark.
                At(r.Frame);
                M.Tube((p, n) => Hole, c - outward * 0.05f, c, ri + 0.01f, ri + 0.01f, 12);
                for (int k = -1; k <= 1; k++)
                {
                    float yk = k * 0.12f;
                    float half = Mathf.Sqrt(ri * ri - yk * yk);
                    Box(trim * 0.85f, c + outward * 0.02f + Vector3.up * yk, new Vector3(0.03f, 0.05f, half * 2f), 0.01f);
                }
            }

            void Gutter(RoofInfo r, int s)
            {
                At(r.Frame);
                float tan = Mathf.Tan(r.P);
                float half = r.L * 0.5f + GableOver - 0.05f;
                float y = -Over * tan - 0.02f, z = s * (r.W * 0.5f + Over + 0.08f);

                // Come away at one end and hanging.
                float dropA = 0f, dropB = 0f;
                if (rng.Chance(burnt ? 0.6f : 0.3f))
                {
                    if (rng.Chance(0.5f)) dropA = rng.Range(0.3f, 0.7f);
                    else dropB = rng.Range(0.3f, 0.7f);
                }

                M.Tube((p, n) => GutterGrey, new Vector3(-half, y - dropA, z), new Vector3(half, y - dropB, z), 0.075f, 0.075f, 8);
            }

            /// <summary>A stack of brick courses near one end of the ridge, each a little off, the whole leaning; a cap and pots on top.</summary>
            void Chimney(RoofInfo r, float end)
            {
                float tan = Mathf.Tan(r.P), cos = Mathf.Cos(r.P);
                float lx = end * Mathf.Max(0f, r.L * 0.5f - 1.05f);
                float lz = (rng.Chance(0.5f) ? 1f : -1f) * 0.45f;

                float under = (r.W * 0.5f - Mathf.Abs(lz)) * tan;
                if (r.Hipped) under = Mathf.Min(under, (r.L * 0.5f - Mathf.Abs(lx)) * tan);
                float surface = under + (Deck + Tile) / cos;
                float baseY = surface - 0.4f;
                float topY = Mathf.Max(r.Rise + 0.75f, surface + 1.2f);

                At(r.Frame * Matrix4x4.TRS(new Vector3(lx, baseY, lz), Quaternion.Euler(rng.Range(-4f, 4f), rng.Range(-4f, 4f), rng.Range(-4f, 4f)),
                                           Vector3.one));

                const float size = 0.82f;
                int courses = Mathf.Max(3, Mathf.CeilToInt((topY - baseY) / 0.3f));
                float ch = (topY - baseY) / courses;
                for (int k = 0; k < courses; k++)
                {
                    float s = size * (1f - 0.012f * k);
                    Color c = Brick[(k + rng.Range(0, 2)) % Brick.Length] * Jit(0.04f);
                    if (burnt) c *= Mathf.Lerp(1f, 0.55f, k / (float)courses);
                    Box(c, new Vector3(rng.Range(-0.02f, 0.02f), ch * (k + 0.5f), rng.Range(-0.02f, 0.02f)), new Vector3(s, ch + 0.012f, s), 0.05f,
                        Quaternion.Euler(0f, rng.Range(-3f, 3f), 0f));
                }

                float y = courses * ch;
                Box(CapStone, new Vector3(0f, y + 0.06f, 0f), new Vector3(size + 0.24f, 0.13f, size + 0.24f), 0.045f);

                M.Tube((p, n) => Pot, new Vector3(-0.17f, y + 0.1f, 0f), new Vector3(-0.17f, y + 0.44f, 0f), 0.13f, 0.11f, 10);
                M.Tube((p, n) => Hole, new Vector3(-0.17f, y + 0.43f, 0f), new Vector3(-0.17f, y + 0.445f, 0f), 0.085f, 0.085f, 10);
                if (rng.Chance(0.6f))
                {
                    var foot = new Vector3(0.18f, y + 0.1f, 0.05f);
                    Vector3 tip = foot + Quaternion.Euler(rng.Range(-12f, 12f), 0f, rng.Range(-12f, 12f)) * Vector3.up * 0.3f;
                    M.Tube((p, n) => Pot * 0.92f, foot, tip, 0.12f, 0.1f, 10);
                }
            }

            /// <summary>An old TV aerial on the ridge, one of its arms bent.</summary>
            void Antenna(RoofInfo r, float end)
            {
                float lx = end * r.L * Mathf.Lerp(0.15f, 0.28f, rng.NextFloat());
                float y = r.Rise + (Deck + Tile) / Mathf.Cos(r.P);
                At(r.Frame * Matrix4x4.TRS(new Vector3(lx, y, 0f), Quaternion.Euler(rng.Range(-8f, 8f), 0f, rng.Range(-8f, 8f)), Vector3.one));

                M.Tube((p, n) => B.Iron, Vector3.zero, Vector3.up * 1.7f, 0.03f, 0.025f, 6);
                for (int k = 0; k < 3; k++)
                {
                    float half = (1.1f - k * 0.25f) * 0.5f;
                    var at = new Vector3(0f, 1f + k * 0.3f, 0f);
                    Quaternion bend = k == 1 && rng.Chance(0.6f) ? Quaternion.Euler(0f, 0f, rng.Range(18f, 30f)) : Quaternion.identity;
                    Vector3 d = bend * Vector3.forward * half;
                    M.Tube((p, n) => B.Iron, at - d, at + d, 0.014f, 0.014f, 5);
                }
            }

            // --- The way in ----------------------------------------------------------------------------

            void Entrance(Rect main, RoofInfo r)
            {
                float x = plan.DoorX;
                float face = main.yMax + WallT * 0.5f;

                // With the ridge along the front the door is under the eaves, which shelter it already.
                bool eaveFront = r.AlongX;

                if (plan.Porch) Porch(x, face, r, eaveFront);
                else
                {
                    if (!eaveFront && Visible(Vector3.forward)) Hood(x, face);
                    Steps(x, face);
                }
            }

            void Steps(float x, float z0)
            {
                AtHouse();
                Box(Stone * Jit(0.04f), new Vector3(x, 0.17f, z0 + 0.22f), new Vector3(1.35f, 0.34f, 0.46f), 0.05f);
                Box(Stone * 0.95f * Jit(0.04f), new Vector3(x + rng.Range(-0.05f, 0.05f), 0.085f, z0 + 0.66f), new Vector3(1.55f, 0.17f, 0.44f), 0.05f,
                    Quaternion.Euler(0f, rng.Range(-4f, 4f), 0f));
            }

            /// <summary>
            /// A porch on fat turned posts, its floor of boards, its roof a shallow slope: tucked under the
            /// main eave when the front is an eave side, against the gable wall over the door when not.
            /// </summary>
            void Porch(float x, float face, RoofInfo r, bool eaveFront)
            {
                const float half = 1.5f, depth = 1.9f, fall = 0.18f;
                float tan = Mathf.Tan(r.P);
                float zFront = face + depth;
                float zBack = face + 0.02f;
                float yBack = eaveFront
                    ? r.Top - Over * tan - 0.24f + (Over - 0.02f) * fall
                    : Sill + 2.55f;
                float yFront = yBack - (zFront - zBack) * fall;

                AtHouse();
                float floorTop = Sill + 0.01f;
                const int boards = 5;
                for (int k = 0; k < boards; k++)
                {
                    float z = face + depth * (k + 0.5f) / boards;
                    Box(B.Plank(k + 31) * 0.92f * Jit(0.04f), new Vector3(x, floorTop - 0.06f, z), new Vector3(half * 2f, 0.12f, depth / boards - 0.02f), 0.025f);
                }

                Box(Stone * 0.78f, new Vector3(x, (floorTop - 0.12f) * 0.5f, face + depth * 0.5f), new Vector3(half * 2f - 0.14f, floorTop - 0.12f, depth - 0.14f), 0.03f);

                float postTop = yFront - 0.14f;
                for (int side = -1; side <= 1; side += 2)
                {
                    var foot = new Vector3(x + side * (half - 0.2f), floorTop, zFront - 0.2f);
                    Box(trim * 0.9f, foot + Vector3.up * 0.08f, new Vector3(0.26f, 0.16f, 0.26f), 0.04f);
                    M.Tube((p, n) => trim, foot, new Vector3(foot.x, postTop, foot.z), 0.1f, 0.075f, 10);
                    Box(trim * 0.9f, new Vector3(foot.x, postTop - 0.04f, foot.z), new Vector3(0.22f, 0.1f, 0.22f), 0.03f);
                }

                Box(trim, new Vector3(x, yFront - 0.08f, zFront - 0.2f), new Vector3(half * 2f, 0.16f, 0.17f), 0.04f);

                float zEdge = zFront + 0.25f, yEdge = yFront - 0.25f * fall;
                Vector3 up = new Vector3(0f, yBack - yEdge, zBack - zEdge).normalized;
                Vector3 normal = new Vector3(0f, zEdge - zBack, yBack - yEdge).normalized;
                Slope(Matrix4x4.identity, new SlopeSpec
                {
                    Eave = new Vector3(x, yEdge, zEdge),
                    Up = up,
                    Normal = normal,
                    HalfBottom = half + 0.2f,
                    HalfTop = half + 0.2f,
                    Run = Vector2.Distance(new Vector2(zBack, yBack), new Vector2(zEdge, yEdge)),
                    Deck = 0.12f,
                    Tile = 0.065f,
                }, null);

                Steps(x, zFront);
            }

            /// <summary>A little tiled hood over a door in a gable wall, on two brackets.</summary>
            void Hood(float x, float face)
            {
                float y = Sill + 2.62f;
                const float reach = 0.75f, drop = 0.2f;
                Vector3 up = new Vector3(0f, drop, -reach).normalized;
                Vector3 normal = new Vector3(0f, reach, drop).normalized;
                Slope(Matrix4x4.identity, new SlopeSpec
                {
                    Eave = new Vector3(x, y - drop, face + reach),
                    Up = up,
                    Normal = normal,
                    HalfBottom = 0.85f,
                    HalfTop = 0.85f,
                    Run = Mathf.Sqrt(reach * reach + drop * drop),
                    Deck = 0.1f,
                    Tile = 0.06f,
                }, null);

                AtHouse();
                for (int side = -1; side <= 1; side += 2)
                    Box(trim, new Vector3(x + side * 0.7f, y - 0.4f, face + 0.27f), new Vector3(0.09f, 0.65f, 0.09f), 0.02f, Quaternion.Euler(46f, 0f, 0f));
            }

            // --- A house that has come down ---------------------------------------------------------------

            public void Ruin()
            {
                Rect main = plan.Main;
                blocks.Add((main, Sill + 1.6f));
                Plinth(main, Sill);

                int stubs = rng.Range(3, 6);
                Quaternion lap = Quaternion.Euler(-6f, 0f, 0f);
                for (int i = 0; i < stubs; i++)
                {
                    int side = rng.Range(0, 4);
                    Vector3 n = Sides[side];
                    float wallLength = (side < 2 ? main.width : main.height) + WallT;
                    float len = Mathf.Min(rng.Range(1.4f, 3.4f), wallLength - 0.4f);
                    float t = rng.Range(-wallLength * 0.5f + len * 0.5f, wallLength * 0.5f - len * 0.5f);
                    float hs = rng.Range(0.6f, 1.9f);

                    var origin = new Vector3(main.center.x + n.x * (main.width + WallT) * 0.5f, 0f, main.center.y + n.z * (main.height + WallT) * 0.5f);
                    At(Face(origin, n));
                    Box(paint * 0.85f, new Vector3(t, Sill + hs * 0.35f, -WallT * 0.5f), new Vector3(len, hs * 0.7f, WallT), 0.04f);

                    // Boards to a ragged top.
                    int rows = Mathf.Max(2, Mathf.CeilToInt(hs / 0.3f));
                    for (int row = 0; row < rows; row++)
                    {
                        float l = len * (1f - row / (float)rows * rng.Range(0.1f, 0.75f));
                        float off = rng.Range(-0.5f, 0.5f) * (len - l);
                        Box(paint * ((row & 1) == 0 ? 1f : 0.95f) * Jit(0.04f), new Vector3(t + off, Sill + 0.15f + row * 0.3f, 0.036f),
                            new Vector3(l, 0.33f, 0.062f), 0.022f, lap);
                    }

                    float end = rng.Chance(0.5f) ? -1f : 1f;
                    float postH = hs * rng.Range(0.7f, 1.25f);
                    Box(trim, new Vector3(t + end * len * 0.5f, Sill + postH * 0.5f, 0f), new Vector3(0.25f, postH, 0.25f), 0.05f,
                        Quaternion.Euler(0f, 0f, rng.Range(-8f, 8f)));
                }

                // A piece of the roof, down in one and leaning on the rubble.
                float halfLength = Mathf.Min(main.width * 0.5f, rng.Range(2f, 2.8f));
                float run = Mathf.Min(main.height * 0.8f, rng.Range(2.6f, 3.4f));
                Quaternion q = Quaternion.Euler(0f, rng.Range(-20f, 20f) + (rng.Chance(0.5f) ? 180f : 0f), 0f) * Quaternion.Euler(-rng.Range(16f, 26f), 0f, 0f);
                Vector3 up = q * Vector3.forward, normal = q * Vector3.up;
                var centre = new Vector3(main.center.x + rng.Range(-0.8f, 0.8f), 0f, main.center.y + rng.Range(-0.6f, 0.6f));
                var spec = new SlopeSpec
                {
                    Eave = centre - up * run * 0.5f + Vector3.up * (Sill + 0.05f + run * 0.5f * up.y),
                    Up = up,
                    Normal = normal,
                    HalfBottom = halfLength,
                    HalfTop = halfLength,
                    Run = run,
                    Deck = 0.14f,
                    Tile = 0.07f,
                };

                var holes = new List<Patch>
                {
                    new Patch { Kind = Damage.Hole, X = rng.Range(-0.3f, 0.3f) * halfLength, Z = run * rng.Range(0.35f, 0.65f), HalfX = 0.6f, HalfZ = 0.45f },
                };
                Slope(Matrix4x4.identity, spec, holes);

                AtHouse();
                int beams = rng.Range(3, 5);
                for (int i = 0; i < beams; i++)
                {
                    var at = new Vector3(main.center.x + rng.Range(-0.4f, 0.4f) * main.width, Sill + rng.Range(0.25f, 0.7f),
                                         main.center.y + rng.Range(-0.4f, 0.4f) * main.height);
                    Box(B.WalnutDark * Jit(0.06f), at, new Vector3(0.14f, 0.14f, rng.Range(1.6f, 3.2f)), 0.03f,
                        Quaternion.Euler(rng.Range(-35f, 35f), rng.Range(0f, 360f), 0f));
                }

                int tiles = rng.Range(6, 11);
                for (int i = 0; i < tiles; i++)
                {
                    var at = new Vector2(main.center.x + rng.Range(-0.65f, 0.65f) * main.width, main.center.y + rng.Range(-0.65f, 0.65f) * main.height);
                    float y = main.Contains(at) ? Sill + 0.04f : 0.04f;
                    Box(roof * Jit(0.07f), new Vector3(at.x, y, at.y), new Vector3(0.6f, 0.06f, 0.45f), 0.025f,
                        Quaternion.Euler(rng.Range(-10f, 10f), rng.Range(0f, 360f), rng.Range(-10f, 10f)));
                }

                // Loose boards in a heap.
                int boards = rng.Range(4, 8);
                var heap = new Vector3(main.center.x + rng.Range(-0.3f, 0.3f) * main.width, Sill, main.center.y + rng.Range(-0.3f, 0.3f) * main.height);
                for (int i = 0; i < boards; i++)
                {
                    Box(paint * Jit(0.06f), heap + new Vector3(rng.Range(-0.5f, 0.5f), 0.04f + i * 0.05f, rng.Range(-0.5f, 0.5f)),
                        new Vector3(rng.Range(1.2f, 2.2f), 0.05f, 0.22f), 0.015f, Quaternion.Euler(rng.Range(-6f, 6f), rng.Range(0f, 180f), rng.Range(-6f, 6f)));
                }

                // The chimney, come down in one line and broken into its courses, across the edge of the slab.
                float fall = rng.Chance(0.5f) ? -1f : 1f;
                var start = new Vector3(fall > 0 ? main.xMax - 0.8f : main.xMin + 0.8f, 0f, main.center.y + rng.Range(-0.3f, 0.3f) * main.height);
                Vector3 dir = Quaternion.Euler(0f, rng.Range(-35f, 35f), 0f) * new Vector3(fall, 0f, 0f);
                Quaternion lying = Quaternion.LookRotation(dir);
                int courses = rng.Range(4, 7);
                for (int k = 0; k < courses; k++)
                {
                    Vector3 at = start + dir * (k * 0.36f + rng.Range(0f, 0.08f) * k);
                    float ground = main.Contains(new Vector2(at.x, at.z)) ? Sill : 0f;
                    at.y = ground + 0.39f;
                    Box(Brick[k % Brick.Length] * Jit(0.05f), at, new Vector3(0.78f, 0.78f, 0.3f), 0.05f,
                        lying * Quaternion.Euler(rng.Range(-6f, 6f), rng.Range(-8f, 8f), rng.Range(-10f, 10f)));
                }
            }

            // --- The garden shed -------------------------------------------------------------------------

            /// <summary>
            /// A plank shed under a sloping tin roof, its door on the side the camera sees. Returns the
            /// quarter turn that puts the door there, added to the shed's own yaw.
            /// </summary>
            public float Shed(ShedPlan s)
            {
                float turn = 0f, best = float.MinValue;
                for (int k = 0; k < 4; k++)
                {
                    float facing = Vector3.Dot(lotTurn * Quaternion.Euler(0f, s.Yaw + k * 90f, 0f) * Vector3.forward, TowardCamera);
                    if (facing > best) { best = facing; turn = k * 90f; }
                }

                float w = s.Size.x, d = s.Size.y;
                float hFront = s.Height - 0.1f, hBack = s.Height + 0.4f;
                float TopAt(float z) => Mathf.Lerp(hBack, hFront, (z + d * 0.5f) / d);
                M.Shade = B.TimberShade(new Vector2(w * 0.5f, d * 0.5f), hFront);
                AtHouse();

                Box(B.WalnutDark, new Vector3(0f, 0.07f, 0f), new Vector3(w + 0.14f, 0.14f, d + 0.14f), 0.03f);

                float doorX = -w * 0.18f;
                const float doorW = 0.86f, doorH = 1.76f;
                var doorway = new[] { new Vector3(doorX - doorW * 0.5f - 0.04f, doorX + doorW * 0.5f + 0.04f, 0.14f + doorH + 0.04f) };
                M.BoardWall(new Vector3(0f, 0f, d * 0.5f), Vector3.right, Vector3.forward, -w * 0.5f, w * 0.5f, 0.14f, x => hFront, 0.22f, 11, doorway);
                M.BoardWall(new Vector3(0f, 0f, -d * 0.5f), Vector3.right, Vector3.back, -w * 0.5f, w * 0.5f, 0.14f, x => hBack, 0.22f, 12);
                M.BoardWall(new Vector3(-w * 0.5f, 0f, 0f), Vector3.forward, Vector3.left, -d * 0.5f, d * 0.5f, 0.14f, z => TopAt(z) - 0.02f, 0.22f, 13);
                M.BoardWall(new Vector3(w * 0.5f, 0f, 0f), Vector3.forward, Vector3.right, -d * 0.5f, d * 0.5f, 0.14f, z => TopAt(z) - 0.02f, 0.22f, 14);

                foreach (float x in new[] { -w * 0.5f, w * 0.5f })
                    foreach (float z in new[] { -d * 0.5f, d * 0.5f })
                        Box(B.Walnut, new Vector3(x, (0.14f + TopAt(z)) * 0.5f, z), new Vector3(0.12f, TopAt(z) - 0.14f, 0.12f), 0.025f);

                // The doorway's dark, and the door: ledged and braced, sometimes hanging open.
                Box(Hole, new Vector3(doorX, 0.14f + doorH * 0.5f, d * 0.5f - 0.05f), new Vector3(doorW, doorH, 0.04f), 0.01f);
                Quaternion swing = rng.Chance(0.35f) ? Quaternion.Euler(0f, -rng.Range(30f, 75f), 0f) : Quaternion.identity;
                var hinge = new Vector3(doorX - doorW * 0.5f, 0.14f + doorH * 0.5f, d * 0.5f + 0.04f);
                Vector3 leaf = hinge + swing * new Vector3(doorW * 0.5f, 0f, 0f);
                Box(B.DoorWood * Jit(0.04f), leaf, new Vector3(doorW, doorH, 0.05f), 0.015f, swing);
                foreach (float ly in new[] { -0.55f, 0.55f })
                    Box(B.DoorWood * 0.85f, leaf + swing * new Vector3(0f, ly, 0.035f), new Vector3(doorW - 0.08f, 0.1f, 0.03f), 0.01f, swing);
                Box(B.DoorWood * 0.85f, leaf + swing * new Vector3(0f, 0f, 0.035f), new Vector3(0.08f, 1.25f, 0.03f), 0.01f,
                    swing * Quaternion.Euler(0f, 0f, -36f));

                // A small window in the side the camera sees.
                float windowSide = Vector3.Dot(lotTurn * Quaternion.Euler(0f, s.Yaw + turn, 0f) * Vector3.right, TowardCamera) >= 0f ? 1f : -1f;
                var pane = new Vector3(windowSide * (w * 0.5f + 0.04f), 1.35f, -0.1f);
                Box(B.Walnut, pane, new Vector3(0.05f, 0.56f, 0.66f), 0.015f);
                Box(B.Glass, pane + Vector3.right * windowSide * 0.02f, new Vector3(0.03f, 0.44f, 0.54f), 0.01f);
                Box(B.Walnut, pane + Vector3.right * windowSide * 0.035f, new Vector3(0.025f, 0.44f, 0.04f), 0.008f);

                // The roof: corrugated tin sloping to the front, rust run down it.
                float slope = (hBack - hFront) / d;
                float cos = 1f / Mathf.Sqrt(1f + slope * slope), sin = slope * cos;
                var down = new Vector3(0f, -sin, cos);
                const float overhang = 0.22f;
                var corner = new Vector3(-w * 0.5f - 0.16f, hBack + 0.05f + overhang * slope, -d * 0.5f - overhang);
                float length = (d + overhang * 2f) / cos;
                Color[] tins = rng.Chance(0.5f) ? B.Tin : B.RoofTin;
                Color tin = tins[rng.Range(0, tins.Length)];
                float phase = rng.Range(0f, 10f);
                M.Corrugated(corner, Vector3.right, down, w + 0.32f, length, 0.15f, 0.035f, 0.03f, (u, v) =>
                {
                    float streak = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u * 7.3f + phase)), 10f);
                    return Color.Lerp(tin * Mathf.Lerp(1.04f, 0.95f, v / length), B.Rust, Mathf.Clamp01(streak) * Mathf.Clamp01(v / length) * 0.35f);
                });

                for (int side = -1; side <= 1; side += 2)
                {
                    var mid = new Vector3(side * (w * 0.5f + 0.14f), (hBack + hFront) * 0.5f, 0f);
                    Box(B.Walnut, mid, new Vector3(0.05f, 0.12f, length), 0.015f, Quaternion.Euler(Mathf.Atan(slope) * Mathf.Rad2Deg, 0f, 0f));
                }

                if (detailed)
                {
                    if (rng.Chance(0.5f)) B.Barrel(M, new Vector3(w * 0.5f - 0.25f, 0f, d * 0.5f + 0.35f), 0.22f, 0.6f, B.DoorWood, B.WalnutDark);
                    else B.Crate(M, new Vector3(w * 0.5f - 0.3f, 0f, d * 0.5f + 0.38f), new Vector3(0.5f, 0.42f, 0.42f), rng.Range(-20f, 20f), 7);
                }

                return turn;
            }
        }
    }
}
