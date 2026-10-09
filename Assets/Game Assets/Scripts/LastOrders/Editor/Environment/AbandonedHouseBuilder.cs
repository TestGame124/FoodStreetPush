using System.Collections.Generic;
using LastOrders.Core;
using UnityEngine;
using E = LastOrders.EditorTools.EnvKit;
using K = LastOrders.EditorTools.PrimitiveKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// One abandoned suburban house on its lot: clapboard walls, a shingled roof, windows boarded
    /// or dark, the yard gone to seed.
    ///
    /// Seen from above a house is mostly its roof, so that is where the variety goes: gabled or
    /// hipped, turned with the house, one of several colours, with a wing or a garage roofed
    /// separately, and the damage a year of weather does - a hole, a tarp somebody nailed over one,
    /// bare boards where shingles blew off, moss. The walls show on the two sides the camera sees,
    /// so only those get windows and a door; the other two are plain.
    ///
    /// Each house is two renderers - walls and roof - built as meshes UV'd in metres, so the siding
    /// and the shingles are the same size on every house whatever its shape.
    ///
    /// Since 2026-10-07 this is the planner and the yard: the house that stands on the plan is
    /// <see cref="ToonHouseBuilder"/>'s unless <see cref="Look"/> says Classic.
    ///
    /// Everything is in the lot's own space: X along the street, +Z toward it, the front edge at
    /// +depth/2. The caller positions and turns the lot.
    /// </summary>
    public static class AbandonedHouseBuilder
    {
        public struct Lot
        {
            public Vector2 Centre;

            /// <summary>Along the street.</summary>
            public float Width;

            /// <summary>Away from the street.</summary>
            public float Depth;

            /// <summary>Turns local +Z to face the street.</summary>
            public float Yaw;

            /// <summary>Full detail - yard clutter, bushes, junk. Off for lots the fog mostly hides.</summary>
            public bool Detailed;

            public Rect WorldRect
            {
                get
                {
                    bool turned = Mathf.Abs(Mathf.DeltaAngle(Yaw, 90f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(Yaw, -90f)) < 1f;
                    var size = turned ? new Vector2(Depth, Width) : new Vector2(Width, Depth);
                    return new Rect(Centre - size * 0.5f, size);
                }
            }
        }

        public enum HouseLook
        {
            /// <summary>The toon houses of <see cref="ToonHouseBuilder"/>, one saved mesh per lot.</summary>
            Toon,

            /// <summary>The textured clapboard houses built here, two meshes kept in the scene.</summary>
            Classic,
        }

        /// <summary>
        /// Which house goes on a lot. Either way the plan - and every random number the yard and the
        /// lots after it draw - is the same, so switching changes only the house.
        /// </summary>
        public static HouseLook Look = HouseLook.Toon;

        /// <summary>When set, every house's plan is added to it as the house is built.</summary>
        public static List<ToonHouseBuilder.Plan> Recorder;

        // Submeshes of the walls.
        const int Siding = 0, Trim = 1, Glass = 2, Boards = 3, Door = 4, Base = 5, Dark = 6;
        const int BodyParts = 7;

        // Submeshes of the roof.
        const int Covering = 0, Gable = 1, RoofTrim = 2, Hole = 3, Tarp = 4, Brick = 5, Bare = 6, Moss = 7;
        const int RoofParts = 8;

        const float WallThickness = 0.22f;
        const float FloorTop = 0.35f;

        /// <summary>Which way is toward the camera, flat on the ground. It sits south-east of what it looks at.</summary>
        static readonly Vector3 TowardCamera = new Vector3(0.6f, 0f, -0.8f);

        internal static readonly Color[] TrimColours = { K.Hex("EEEAE0"), K.Hex("D9D4C7"), K.Hex("5C656C"), K.Hex("F2EDE2") };
        internal static readonly Color[] DoorColours ={ K.Hex("7A3B32"), K.Hex("3F5B6E"), K.Hex("4A5A3A"), K.Hex("6B4A2E"), K.Hex("2F3438") };
        /// <summary>Slate blues and greys mostly, the odd rust-red or moss-brown: the painted look's roofs.</summary>
        public static readonly Color[] RoofColours =
        {
            K.Hex("4A5363"), K.Hex("5B5045"), K.Hex("7A4B3F"), K.Hex("4E5A62"), K.Hex("55604C"), K.Hex("565E6B"), K.Hex("3F4757"),
        };

        static readonly Color GlassColour = K.Hex("2C3338");
        static readonly Color HoleColour = K.Hex("1C1A18");
        static readonly Color TarpColour = K.Hex("3F6FA8");
        static readonly Color BrickColour = K.Hex("8B5B4A");
        static readonly Color BareColour = K.Hex("7C6B55");
        static readonly Color MossColour = K.Hex("4E5E33");
        static readonly Color CharredRoof = K.Hex("2B2926");

        sealed class Palette
        {
            public Color Siding;
            public Color Trim;
            public Color Door;
            public Color Roof;
        }

        public static Transform Build(Transform parent, string name, Lot lot, ref DeterministicRandom rng)
        {
            Transform root = K.Group(name, parent);
            root.localPosition = new Vector3(lot.Centre.x, 0f, lot.Centre.y);
            root.localRotation = Quaternion.Euler(0f, lot.Yaw, 0f);
            Quaternion lotTurn = root.localRotation;

            float w = lot.Width;
            float d = lot.Depth;
            float front = d * 0.5f;

            // No lawn slab: the grass is the lawn, a year uncut and dying back in patches.
            int dead = rng.Range(1, 3);
            for (int i = 0; i < dead; i++)
            {
                E.Blob($"DeadGrass_{i}", root, new Vector2(rng.Range(-w * 0.3f, w * 0.3f), rng.Range(-d * 0.3f, d * 0.3f)),
                       rng.Range(4f, 8f), E.Pick(E.SunGrass, ref rng), 1, ref rng, 0.5f);
            }

            // --- The plan -----------------------------------------------------------------------------

            float houseW = Mathf.Max(5f, Mathf.Min(rng.Range(7.5f, 11f), w - 3.2f));
            float houseD = Mathf.Max(4.5f, Mathf.Min(rng.Range(6.5f, 9.5f), d - 5.5f));
            float setback = Mathf.Clamp(rng.Range(2.8f, 4.5f), 2.2f, d - houseD - 1f);
            float slack = Mathf.Max(0f, (w - houseW) * 0.5f - 1.1f);
            float houseX = rng.Range(-slack, slack);
            var main = new Rect(houseX - houseW * 0.5f, front - setback - houseD, houseW, houseD);

            bool collapsed = rng.Chance(0.1f);
            bool burnt = !collapsed && rng.Chance(0.15f);

            var palette = new Palette
            {
                Siding = burnt ? E.Pick(E.Burnt, ref rng) : E.Pick(E.HouseWall, ref rng),
                Trim = burnt ? E.Burnt[2] : TrimColours[rng.Range(0, TrimColours.Length)],
                Door = DoorColours[rng.Range(0, DoorColours.Length)],
                Roof = burnt ? CharredRoof : RoofColours[rng.Range(0, RoofColours.Length)],
            };

            var body = new EnvMesh(BodyParts);
            var roof = new EnvMesh(RoofParts);

            var plan = new ToonHouseBuilder.Plan
            {
                Name = name,
                Lot = lot,
                Main = main,
                Collapsed = collapsed,
                Burnt = burnt,
                Siding = palette.Siding,
                Trim = palette.Trim,
                Door = palette.Door,
                Roof = palette.Roof,
            };

            if (collapsed)
            {
                Collapsed(root, body, roof, main, ref rng);
                Present(root, body, roof, palette, plan);
                Yard(root, lot, main, main.center.x, null, ref rng);
                return root;
            }

            float height = rng.Range(2.5f, 2.85f);
            float wallTop = FloorTop + height;

            Rect? wing = null;
            if (rng.Chance(0.45f))
            {
                float ww = Mathf.Min(rng.Range(3.4f, 5f), houseW - 1f);
                float wd = rng.Range(3f, 4.6f);
                if (main.yMin - wd > -d * 0.5f + 0.8f)
                {
                    float wx = rng.Chance(0.5f) ? main.xMin : main.xMax - ww;
                    wing = new Rect(wx, main.yMin - wd, ww, wd);
                }
            }

            Rect? garage = null;
            float roomLeft = main.xMin + w * 0.5f;
            float roomRight = w * 0.5f - main.xMax;
            if (rng.Chance(0.3f) && Mathf.Max(roomLeft, roomRight) > 4.8f)
            {
                float gd = Mathf.Min(5.8f, houseD + 0.6f);
                float gx = roomRight >= roomLeft ? main.xMax : main.xMin - 3.8f;
                garage = new Rect(gx, main.yMax - 0.4f - gd, 3.8f, gd);
            }

            bool Visible(Vector2 outward) => Vector3.Dot(lotTurn * new Vector3(outward.x, 0f, outward.y), TowardCamera) > -0.3f;

            // --- Foundations and walls -------------------------------------------------------------------

            Foundation(body, main);
            if (wing.HasValue) Foundation(body, wing.Value);
            if (garage.HasValue) Foundation(body, garage.Value);

            float doorX = Mathf.Clamp(main.center.x + rng.Range(-houseW * 0.25f, houseW * 0.25f), main.xMin + 1.2f, main.xMax - 1.2f);
            float damage = burnt ? 0.55f : 0.2f;

            Wall(body, new Vector2(main.xMin, main.yMax), new Vector2(main.xMax, main.yMax), Vector2.up, height,
                 Visible(Vector2.up), doorX - main.xMin, -1f, damage, ref rng);
            Wall(body, new Vector2(main.xMin, main.yMin), new Vector2(main.xMax, main.yMin), Vector2.down, height,
                 Visible(Vector2.down), -1f, -1f, damage, ref rng);
            Wall(body, new Vector2(main.xMin, main.yMin), new Vector2(main.xMin, main.yMax), Vector2.left, height,
                 Visible(Vector2.left), -1f, -1f, damage, ref rng);
            Wall(body, new Vector2(main.xMax, main.yMin), new Vector2(main.xMax, main.yMax), Vector2.right, height,
                 Visible(Vector2.right), -1f, -1f, damage, ref rng);
            Corners(body, main, height);

            float wingHeight = height - 0.25f;
            if (wing.HasValue)
            {
                Rect r = wing.Value;
                Wall(body, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), Vector2.down, wingHeight, Visible(Vector2.down), -1f, -1f, damage, ref rng);
                Wall(body, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), Vector2.left, wingHeight, Visible(Vector2.left), -1f, -1f, damage, ref rng);
                Wall(body, new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), Vector2.right, wingHeight, Visible(Vector2.right), -1f, -1f, damage, ref rng);
            }

            float garageHeight = 2.35f;
            if (garage.HasValue)
            {
                Rect g = garage.Value;
                bool right = g.center.x > main.center.x;
                Vector2 outer = right ? Vector2.right : Vector2.left;
                float outerX = right ? g.xMax : g.xMin;

                Wall(body, new Vector2(g.xMin, g.yMin), new Vector2(g.xMax, g.yMin), Vector2.down, garageHeight, Visible(Vector2.down), -1f, -1f, damage, ref rng);
                Wall(body, new Vector2(outerX, g.yMin), new Vector2(outerX, g.yMax), outer, garageHeight, Visible(outer), -1f, -1f, damage, ref rng);
                Wall(body, new Vector2(g.xMin, g.yMax), new Vector2(g.xMax, g.yMax), Vector2.up, garageHeight, Visible(Vector2.up), -1f,
                     g.width * 0.5f, damage, ref rng);
            }

            // --- The porch ---------------------------------------------------------------------------------

            plan.Height = height;
            plan.Wing = wing;
            plan.Garage = garage;
            plan.DoorX = doorX;

            var porchParts = new EnvMesh.RoofParts { Covering = Covering, Gable = RoofTrim, Trim = RoofTrim };
            float stepX = doorX;
            plan.Porch = rng.Chance(0.4f);
            if (plan.Porch)
            {
                // Just over the door and under the main eaves, which come down lower than it looks.
                const float porchHeight = 1.9f;
                roof.ShedRoof(porchParts, new Vector3(doorX, FloorTop + porchHeight, main.yMax + 0.8f), Quaternion.Euler(0f, 180f, 0f),
                              2.8f, 1.6f, 0.3f, 0.1f);

                foreach (float side in new[] { -1f, 1f })
                {
                    body.Box(Trim, new Vector3(doorX + side * 1.25f, FloorTop + porchHeight * 0.5f, main.yMax + 1.45f),
                             new Vector3(0.14f, porchHeight, 0.14f), Quaternion.identity);
                }
            }

            body.Box(Base, new Vector3(stepX, FloorTop * 0.5f, main.yMax + 0.5f), new Vector3(1.6f, FloorTop, 1f), Quaternion.identity);

            // --- Roofs --------------------------------------------------------------------------------------

            var parts = new EnvMesh.RoofParts { Covering = Covering, Gable = Gable, Trim = RoofTrim };

            plan.Pitch = rng.Range(26f, 36f);
            plan.Hipped = rng.Chance(0.35f);
            Slope visible = Roof(roof, parts, main, wallTop, plan.Pitch, plan.Hipped, lotTurn);

            if (wing.HasValue) Roof(roof, parts, wing.Value, FloorTop + wingHeight, rng.Range(24f, 32f), false, lotTurn);

            if (garage.HasValue)
            {
                Rect g = garage.Value;
                bool right = g.center.x > main.center.x;
                roof.ShedRoof(parts, new Vector3(g.center.x, FloorTop + garageHeight, g.center.y),
                              Quaternion.Euler(0f, right ? -90f : 90f, 0f), g.height + WallThickness, g.width + WallThickness, 0.55f, 0.3f);
            }

            // What a year of weather does to a roof nobody fixes.
            int patches = burnt ? rng.Range(2, 4) : rng.Chance(0.5f) ? rng.Range(1, 3) : 0;
            plan.Patches = patches;
            for (int i = 0; i < patches; i++)
            {
                int kind;
                float roll = rng.NextFloat();
                if (burnt) kind = roll < 0.65f ? Hole : Bare;
                else kind = roll < 0.38f ? Hole : roll < 0.62f ? Tarp : roll < 0.82f ? Bare : Moss;

                var size = kind == Tarp ? new Vector2(rng.Range(1.6f, 2.6f), rng.Range(1.3f, 2f))
                         : kind == Moss ? new Vector2(rng.Range(1.2f, 2.8f), rng.Range(0.8f, 1.6f))
                         : new Vector2(rng.Range(0.8f, burnt ? 2.6f : 1.6f), rng.Range(0.6f, burnt ? 2f : 1.2f));

                // A hipped slope narrows toward the top, so its patches keep to the middle and lower down.
                float spread = visible.Tapers ? 0.1f : 0.25f;
                float reach = visible.Tapers ? 0.5f : 0.7f;
                var at = new Vector2(rng.Range(visible.Length * (0.5f - spread), visible.Length * (0.5f + spread)),
                                     rng.Range(visible.Run * 0.25f, visible.Run * reach));
                size = Vector2.Min(size, new Vector2(visible.Length * 0.3f, visible.Run * 0.4f));
                roof.SlopePatch(kind, visible.EaveStart, visible.Along, visible.Up, visible.Normal, at, size, rng.Range(-25f, 25f));
            }

            if (rng.Chance(0.3f))
            {
                float cx = rng.Chance(0.5f) ? main.xMin + 0.9f : main.xMax - 0.9f;
                float top = wallTop + visible.Rise + 0.6f;
                float cz = main.center.y + rng.Range(-0.5f, 0.5f);
                plan.Chimney = new Vector2(cx, cz);
                roof.Box(Brick, new Vector3(cx, (wallTop - 0.4f + top) * 0.5f, cz),
                         new Vector3(0.7f, top - wallTop + 0.4f, 0.7f), Quaternion.identity);
            }

            // A shed at the bottom of the garden, sometimes.
            if (rng.Chance(0.22f) && main.yMin - (-d * 0.5f) > 4.2f)
            {
                var at = new Vector2(rng.Range(-w * 0.3f, w * 0.3f), (main.yMin + -d * 0.5f) * 0.5f);
                if (!wing.HasValue || !wing.Value.Contains(at))
                {
                    float yaw = rng.Range(-10f, 10f);
                    Color planks = E.Pick(E.Weathered, ref rng);
                    plan.Shed = new ToonHouseBuilder.ShedPlan { At = at, Yaw = yaw, Size = new Vector2(2.4f, 2f), Height = 2f };
                    if (Look == HouseLook.Classic) Shed(root, "Shed", at, yaw, plan.Shed.Size, plan.Shed.Height, planks, false, ref rng);
                }
            }

            Present(root, body, roof, palette, plan);

            if (burnt) E.Blob("Scorch", root, main.center, Mathf.Max(main.width, main.height) * 1.1f, E.Scorch, 2, ref rng, 0.45f);

            E.Slab("Path", root, new Vector2(stepX, (main.yMax + front) * 0.5f + 0.5f), new Vector2(1.1f, front - main.yMax - 0.9f),
                   0f, 0.03f, 0.02f, E.Pick(E.Concrete, ref rng));

            Yard(root, lot, main, stepX, garage, ref rng);
            return root;
        }

        // --- Walls ---------------------------------------------------------------------------------------

        static void Foundation(EnvMesh body, Rect r)
        {
            body.Box(Base, new Vector3(r.center.x, FloorTop * 0.5f, r.center.y), new Vector3(r.width + 0.3f, FloorTop, r.height + 0.3f),
                     Quaternion.identity);
        }

        /// <summary>
        /// One wall: a solid run of siding and, if it faces the camera, its windows and door laid on
        /// its face. <paramref name="doorAt"/> and <paramref name="garageDoorAt"/> are metres from
        /// <paramref name="a"/>, negative for none.
        /// </summary>
        static void Wall(EnvMesh body, Vector2 a, Vector2 b, Vector2 outward, float height, bool visible,
                         float doorAt, float garageDoorAt, float damage, ref DeterministicRandom rng)
        {
            float length = Vector2.Distance(a, b);
            Vector2 dir = (b - a) / Mathf.Max(0.01f, length);
            Quaternion turn = Quaternion.Euler(0f, Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg, 0f);
            Vector2 mid = (a + b) * 0.5f;

            body.Box(Siding, new Vector3(mid.x, FloorTop + height * 0.5f, mid.y), new Vector3(length + WallThickness, height, WallThickness), turn);

            if (!visible) return;

            var normal = new Vector3(outward.x, 0f, outward.y);

            Vector3 OnFace(float along, float y, float depth) =>
                new Vector3(a.x + dir.x * along, y, a.y + dir.y * along) + normal * (WallThickness * 0.5f + depth * 0.5f);

            if (doorAt >= 0f)
            {
                body.Box(Trim, OnFace(doorAt, FloorTop + 1.12f, 0.05f), new Vector3(1.25f, 2.25f, 0.05f), turn);
                body.Box(rng.Chance(0.2f) ? Dark : Door, OnFace(doorAt, FloorTop + 1.05f, 0.09f), new Vector3(1f, 2.1f, 0.09f), turn);
            }

            if (garageDoorAt >= 0f)
            {
                body.Box(Trim, OnFace(garageDoorAt, FloorTop + 1.05f, 0.05f), new Vector3(3.1f, 2.15f, 0.05f), turn);
                body.Box(rng.Chance(0.3f) ? Dark : Siding, OnFace(garageDoorAt, FloorTop + 1f, 0.09f), new Vector3(2.8f, 2f, 0.09f), turn);
            }

            int windows = Mathf.FloorToInt((length - 0.8f) / 2.7f);
            if (windows < 1 && length > 2.4f) windows = 1;

            for (int i = 0; i < windows; i++)
            {
                float t = (i + 0.5f) / windows * length + rng.Range(-0.3f, 0.3f);
                if (doorAt >= 0f && Mathf.Abs(t - doorAt) < 1.4f) continue;
                if (garageDoorAt >= 0f && Mathf.Abs(t - garageDoorAt) < 2.2f) continue;
                if (t < 0.9f || t > length - 0.9f) continue;

                float y = FloorTop + Mathf.Min(1.5f, height - 0.9f);
                body.Box(Trim, OnFace(t, y, 0.05f), new Vector3(1.2f, 1.3f, 0.05f), turn);

                float roll = rng.NextFloat();
                if (roll < damage + 0.3f)
                {
                    // Boarded: a sheet over the frame and two planks nailed across it.
                    body.Box(Boards, OnFace(t, y, 0.08f), new Vector3(1.05f, 1.12f, 0.08f), turn);
                    foreach (float tilt in new[] { 28f, -28f })
                        body.Box(Boards, OnFace(t, y, 0.13f), new Vector3(1.3f, 0.14f, 0.05f), turn * Quaternion.Euler(0f, 0f, tilt));
                }
                else
                {
                    body.Box(roll < damage + 0.45f ? Dark : Glass, OnFace(t, y, 0.07f), new Vector3(1f, 1.1f, 0.07f), turn);
                }
            }
        }

        /// <summary>Corner boards: thin posts of trim up each corner, which is what makes clapboard read as a house.</summary>
        static void Corners(EnvMesh body, Rect r, float height)
        {
            foreach (float x in new[] { r.xMin, r.xMax })
                foreach (float z in new[] { r.yMin, r.yMax })
                    body.Box(Trim, new Vector3(x, FloorTop + height * 0.5f, z), new Vector3(0.3f, height, 0.3f), Quaternion.identity);
        }

        // --- Roofs ---------------------------------------------------------------------------------------

        /// <summary>One side of a roof, in the house's space, for laying patches on it.</summary>
        struct Slope
        {
            public Vector3 EaveStart;
            public Vector3 Along;
            public Vector3 Up;
            public Vector3 Normal;
            public float Length;
            public float Run;
            public float Rise;

            /// <summary>A hipped roof's long side, which narrows to the ridge.</summary>
            public bool Tapers;
        }

        /// <summary>
        /// A roof over a rectangle, ridge along its longer side. Returns the slope the camera sees
        /// more of, which is where the damage goes.
        /// </summary>
        static Slope Roof(EnvMesh roof, EnvMesh.RoofParts parts, Rect r, float wallTop, float pitch, bool hipped, Quaternion lotTurn)
        {
            // Measured to the walls' outer faces: the slope then clears the wall tops instead of
            // having their outer edge poke through it, and the gable ends sit flush with the siding.
            bool alongX = r.width >= r.height;
            float length = (alongX ? r.width : r.height) + WallThickness;
            float width = (alongX ? r.height : r.width) + WallThickness;
            float rise = width * 0.5f * Mathf.Tan(pitch * Mathf.Deg2Rad);
            const float overhang = 0.45f;
            const float endOverhang = 0.3f;

            Quaternion frame = Quaternion.Euler(0f, alongX ? 0f : 90f, 0f);
            var origin = new Vector3(r.center.x, wallTop, r.center.y);

            if (hipped) roof.HipRoof(parts, origin, frame, length, width, rise, overhang);
            else roof.GableRoof(parts, origin, frame, length, width, rise, overhang, endOverhang);

            // Of the two long slopes, the one facing the camera more.
            float slope = rise / (width * 0.5f);
            float best = float.MinValue;
            var chosen = new Slope();

            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 normal = frame * new Vector3(0f, 1f, side * slope).normalized;
                float facing = Vector3.Dot(lotTurn * normal, TowardCamera) + normal.y;
                if (facing <= best) continue;
                best = facing;

                float x = length * 0.5f + (hipped ? overhang : endOverhang);
                float ze = width * 0.5f + overhang;
                float ye = -overhang * slope;

                Vector3 eave = origin + frame * new Vector3(-x, ye, side * ze);
                Vector3 ridge = origin + frame * new Vector3(-x, rise, 0f);

                chosen = new Slope
                {
                    EaveStart = eave,
                    Along = frame * Vector3.right,
                    Up = (ridge - eave).normalized,
                    Normal = normal,
                    Length = x * 2f,
                    Run = Vector3.Distance(eave, ridge),
                    Rise = rise,
                    Tapers = hipped,
                };
            }

            return chosen;
        }

        // --- Putting it together -----------------------------------------------------------------------------

        /// <summary>
        /// The house the plan describes, in the current <see cref="Look"/>: the toon house and its
        /// shed, or the classic walls and roof just modelled.
        /// </summary>
        static void Present(Transform root, EnvMesh body, EnvMesh roof, Palette palette, ToonHouseBuilder.Plan plan)
        {
            Recorder?.Add(plan);

            if (Look == HouseLook.Classic)
            {
                Finish(root, body, roof, palette);
                return;
            }

            ToonHouseBuilder.Build(root, plan);
            ToonHouseBuilder.BuildShed(root, plan);
        }

        static void Finish(Transform root, EnvMesh body, EnvMesh roof, Palette palette)
        {
            Material siding = E.Surface("Siding", TextureGenerator.EnsureSiding(), palette.Siding, 1.2f);
            Material shingles = E.Surface("Shingles", TextureGenerator.EnsureShingles(), palette.Roof, 1.8f);

            Material[] bodyMaterials =
            {
                siding, K.MaterialFor(palette.Trim), K.MaterialFor(GlassColour), K.MaterialFor(E.Weathered[0]),
                K.MaterialFor(palette.Door), K.MaterialFor(E.Foundation), K.MaterialFor(HoleColour),
            };

            Material[] roofMaterials =
            {
                shingles, siding, K.MaterialFor(palette.Trim), K.MaterialFor(HoleColour), K.MaterialFor(TarpColour),
                K.MaterialFor(BrickColour), K.MaterialFor(BareColour), K.MaterialFor(MossColour),
            };

            Attach(root, "Walls", body, bodyMaterials);
            Attach(root, "Roof", roof, roofMaterials);
        }

        /// <summary>A mesh on its own child, with the materials of the submeshes it actually has.</summary>
        public static GameObject Attach(Transform root, string name, EnvMesh mesh, Material[] byPart)
        {
            if (mesh.IsEmpty) return null;

            Mesh built = mesh.Build($"{root.name}_{name}", out List<int> used);

            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = built;

            var materials = new Material[used.Count];
            for (int i = 0; i < used.Count; i++) materials[i] = byPart[used[i]];
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;

            return go;
        }

        /// <summary>
        /// A garden shed or a shack: plank walls, a tin roof sloping to the front, a door, and - for the
        /// settlement's - a lit window, which at night is the most "somebody lives here" thing there is.
        /// </summary>
        public static Transform Shed(Transform parent, string name, Vector2 at, float yaw, Vector2 size, float height,
                                     Color planks, bool lit, ref DeterministicRandom rng)
        {
            Transform root = K.Group(name, parent);
            root.localPosition = new Vector3(at.x, 0f, at.y);
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);

            const int Walls = 0, Tin = 1, Frame = 2, DoorPart = 3, Window = 4;
            var mesh = new EnvMesh(5);

            mesh.Box(Walls, new Vector3(0f, height * 0.5f, 0f), new Vector3(size.x, height, size.y), Quaternion.identity);
            mesh.ShedRoof(new EnvMesh.RoofParts { Covering = Tin, Gable = Walls, Trim = Frame },
                          new Vector3(0f, height, 0f), Quaternion.Euler(0f, 180f, 0f), size.x, size.y, 0.45f, 0.3f);

            float face = -size.y * 0.5f - 0.04f;
            mesh.Box(DoorPart, new Vector3(-size.x * 0.22f, 0.95f, face), new Vector3(0.85f, 1.9f, 0.06f), Quaternion.identity);
            if (size.x > 2.6f || lit)
                mesh.Box(Window, new Vector3(size.x * 0.25f, 1.35f, face), new Vector3(0.7f, 0.55f, 0.06f), Quaternion.identity);

            Material[] materials =
            {
                E.Surface("Siding", TextureGenerator.EnsureSiding(), planks, 1.2f),
                E.Surface("Metal", TextureGenerator.EnsureMetal(), Color.white, 0.8f),
                K.MaterialFor(K.WoodDark),
                K.MaterialFor(K.Dark),
                lit ? E.Glow(E.LampGlow) : K.MaterialFor(GlassColour),
            };

            Attach(root, "Shed", mesh, materials);
            return root;
        }

        /// <summary>A house that has come down: the slab, stubs of wall, heaps, and a piece of the roof lying across them.</summary>
        static void Collapsed(Transform root, EnvMesh body, EnvMesh roof, Rect main, ref DeterministicRandom rng)
        {
            Foundation(body, main);
            E.Blob("Scorch", root, main.center, Mathf.Min(main.width, main.height) * 0.9f, E.Scorch, 2, ref rng, 0.5f);

            int stubs = rng.Range(2, 5);
            for (int i = 0; i < stubs; i++)
            {
                bool alongX = rng.Chance(0.5f);
                float len = rng.Range(1.4f, 3.4f);
                float h = rng.Range(0.6f, 1.8f);
                Vector3 centre = alongX
                    ? new Vector3(rng.Range(main.xMin + len * 0.5f, main.xMax - len * 0.5f), FloorTop + h * 0.5f, rng.Chance(0.5f) ? main.yMin : main.yMax)
                    : new Vector3(rng.Chance(0.5f) ? main.xMin : main.xMax, FloorTop + h * 0.5f, rng.Range(main.yMin + len * 0.5f, main.yMax - len * 0.5f));
                body.Box(Siding, centre, alongX ? new Vector3(len, h, WallThickness) : new Vector3(WallThickness, h, len), Quaternion.identity);
            }

            int heaps = rng.Range(2, 4);
            for (int i = 0; i < heaps; i++)
            {
                var at = new Vector2(rng.Range(main.xMin + 1f, main.xMax - 1f), rng.Range(main.yMin + 1f, main.yMax - 1f));
                ObstacleKit.JunkPile(root, $"Heap_{i}", at, ref rng);
            }

            // Part of the roof, down in one piece and leaning on the rubble.
            var slab = new Vector3(Mathf.Min(main.width, 5f), 0.14f, Mathf.Min(main.height, 3.6f) * 0.8f);
            roof.Box(Covering, new Vector3(main.center.x + rng.Range(-0.8f, 0.8f), FloorTop + 0.7f, main.center.y + rng.Range(-0.6f, 0.6f)),
                     slab, Quaternion.Euler(rng.Range(12f, 22f), rng.Range(-20f, 20f), rng.Range(-8f, 8f)), bottom: true);
        }

        // --- The yard -------------------------------------------------------------------------------------

        static void Yard(Transform root, Lot lot, Rect main, float pathX, Rect? garage, ref DeterministicRandom rng)
        {
            float w = lot.Width;
            float d = lot.Depth;
            float front = d * 0.5f;

            // The drive: to the garage when there is one, otherwise up the side of the house.
            float? driveX = null;
            if (garage.HasValue)
            {
                driveX = garage.Value.center.x;
            }
            else if (rng.Chance(0.55f))
            {
                float left = main.xMin + w * 0.5f;
                float right = w * 0.5f - main.xMax;
                if (Mathf.Max(left, right) > 3.4f)
                    driveX = right >= left ? main.xMax + right * 0.5f : main.xMin - left * 0.5f;
            }

            if (driveX.HasValue)
            {
                float driveEnd = garage.HasValue ? garage.Value.yMax : main.yMax - 1.5f;
                float length = front - driveEnd;
                E.Slab("Drive", root, new Vector2(driveX.Value, front - length * 0.5f), new Vector2(3f, length), 0f, 0.035f, 0.025f,
                       E.Pick(E.Concrete, ref rng));

                if (rng.Chance(0.35f))
                    ObstacleKit.Car(root, "Car_OnDrive", new Vector2(driveX.Value, front - Mathf.Min(length, 5.5f) * 0.5f - 0.4f),
                                    90f + rng.Range(-8f, 8f), ref rng);
            }

            // A broken fence along the front, gapped at the path and the drive.
            if (rng.Chance(0.55f))
            {
                string[] style = rng.Chance(0.5f) ? E.PaintedFences : E.WoodFences;
                bool painted = style == E.PaintedFences;

                for (float x = -w * 0.5f + 0.2f; x + 2.5f <= w * 0.5f - 0.2f; x += 2.5f)
                {
                    float mid = x + 1.25f;
                    if (Mathf.Abs(mid - pathX) < 1.8f) continue;
                    if (driveX.HasValue && Mathf.Abs(mid - driveX.Value) < 2.6f) continue;
                    if (rng.Chance(0.2f)) continue;

                    var tilt = new Vector3(rng.Chance(0.15f) ? rng.Range(55f, 84f) : rng.Range(-12f, 12f), 0f, 0f);
                    E.Pack(style[rng.Range(0, style.Length)], root, new Vector3(x + 2.5f, painted ? 0.54f : 0f, front - 0.35f),
                           0f, 1f, null, tilt);
                }
            }

            if (rng.Chance(0.6f))
                E.PackOf(E.Mailboxes, root, new Vector3(pathX + 1.1f, 0f, front - 0.6f), 180f + rng.Range(-15f, 15f), 1f, ref rng);

            if (rng.Chance(0.6f))
            {
                var at = new Vector3(rng.Range(-w * 0.4f, w * 0.4f), 0f, rng.Range(-d * 0.45f, main.yMin - 0.8f));
                if (at.z < main.yMin - 1.5f)
                {
                    if (rng.Chance(0.3f)) ObstacleKit.DeadTree(root, "YardTree", new Vector2(at.x, at.z), ref rng);
                    else E.PackOf(E.Trees, root, at, rng.Range(0f, 360f), rng.Range(1.4f, 2.1f), ref rng);
                }
            }

            if (!lot.Detailed) return;

            int bushes = rng.Range(0, 4);
            for (int i = 0; i < bushes; i++)
            {
                var at = new Vector3(rng.Range(main.xMin, main.xMax), 0f, main.yMax + rng.Range(0.6f, 1.4f));
                if (Mathf.Abs(at.x - pathX) < 1.2f) continue;
                E.PackOf(E.Bushes, root, at, rng.Range(0f, 360f), rng.Range(1.2f, 2f), ref rng);
            }

            if (rng.Chance(0.3f))
                ObstacleKit.Trash(root, "Trash", new Vector2(driveX ?? (pathX + 2.5f), front - 1.2f), ref rng);

            if (rng.Chance(0.25f))
                ObstacleKit.JunkPile(root, "BackyardJunk",
                                     new Vector2(rng.Range(-w * 0.35f, w * 0.35f), Mathf.Min(main.yMin - 1.2f, -d * 0.3f)), ref rng);
        }
    }
}
