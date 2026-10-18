using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using K = FoodStreet.Visitors.EditorTools.VehicleKit;
using S = LastOrders.EditorTools.SoupKitchenModel;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// What the four discovery sites share: their palette - a roadside diner's teal, cream, red and
    /// mustard, worn concrete and old tin - and the props every abandoned place out there is littered
    /// with, each modelled with some body to it: weeds in clumps of tapering blades, planks nailed on
    /// with their nail heads standing proud, sandbags slumped on each other, tyres, traffic cones,
    /// spray paint raised off the wall rather than painted onto it.
    /// </summary>
    public static class DiscoveryKit
    {
        public const string Folder = "Assets/Game Assets/Content/LastOrders/Discovery";
        public const string MeshFolder = Folder + "/" + EnvKit.Style;
        public const string PrefabFolder = Folder + "/Prefabs";

        // --- Palette (painted a shade darker than it shows, as the rest of the level is) ------------

        public static readonly Color Teal = B.Hex("4E7D79");
        public static readonly Color TealDark = B.Hex("3E6461");
        public static readonly Color Cream = B.Hex("B6AD97");
        public static readonly Color CreamLight = B.Hex("C2BAA4");
        public static readonly Color Red = B.Hex("8E3B33");
        public static readonly Color Mustard = B.Hex("B08A3E");
        public static readonly Color Concrete = B.Hex("8E8C86");
        public static readonly Color ConcreteDark = B.Hex("74726D");
        public static readonly Color Asphalt = B.Hex("47474B");
        public static readonly Color RoadPaint = B.Hex("C9C3B0");
        public static readonly Color Steel = B.Hex("8F969B");
        public static readonly Color SteelDark = B.Hex("5E6367");
        public static readonly Color Earth = B.Hex("6E5A40");
        public static readonly Color EarthDark = B.Hex("594833");
        public static readonly Color Burlap = B.Hex("9C8A66");
        public static readonly Color Rubber = B.Hex("2C2C2E");
        public static readonly Color Graffiti1 = B.Hex("7E4C86");
        public static readonly Color Graffiti2 = B.Hex("3F7A9A");
        public static readonly Color Cardboard = B.Hex("A0845E");
        public static readonly Color CardboardDark = B.Hex("7D6646");
        public static readonly Color Zombie = B.Hex("7F9663");
        public static readonly Color ZombieDark = B.Hex("62774C");

        public static readonly Color[] Weeds = { B.Hex("5D7B2F"), B.Hex("6C8A35"), B.Hex("4F6B2A"), B.Hex("7F9A3A") };

        static float Smooth(float t) => S.Smooth(Mathf.Clamp01(t));

        /// <summary>Darker where a thing meets the ground and underneath: the shade that plants a model on the grass.</summary>
        public static Func<Vector3, Vector3, float> GroundShade(float reach = 0.45f) =>
            (p, n) => Mathf.Lerp(0.76f, 1f, Smooth(p.y / reach)) * (n.y < -0.4f ? 0.84f : 1f);

        // --- Saving and showing ---------------------------------------------------------------------

        public static Mesh Save(B.Model model, string name) => model.Save($"{MeshFolder}/{name}.asset", name);

        /// <summary>A child holding <paramref name="mesh"/>, with the painted material and the glow if it has one.</summary>
        public static GameObject Part(Transform parent, string name, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            B.Show(go, mesh);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        // --- Props ----------------------------------------------------------------------------------

        /// <summary>A clump of weeds: blades tapering from the ground, leaning out from the middle, shades varied.</summary>
        public static void WeedClump(B.Model m, Vector3 centre, float radius, int blades, int key)
        {
            for (int i = 0; i < blades; i++)
            {
                float a = (i * 2.399f + key) % (Mathf.PI * 2f);
                float d = radius * Mathf.Sqrt((B.Hash(key * 31 + i) % 1000) / 1000f);
                Vector3 foot = centre + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float height = 0.18f + 0.22f * ((B.Hash(key * 17 + i) % 1000) / 1000f);
                Vector3 lean = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.06f + d * 0.4f);
                Color c = Weeds[B.Hash(key + i * 7) % Weeds.Length];

                Vector3 mid = foot + Vector3.up * height * 0.55f + lean * 0.5f;
                Vector3 tip = foot + Vector3.up * height + lean * 1.3f;
                m.Tube((p, n) => c * Mathf.Lerp(0.8f, 1.05f, Mathf.Clamp01((p.y - foot.y) / height)), foot, mid, 0.018f, 0.012f, 5);
                m.Tube((p, n) => c * 1.05f, mid, tip, 0.012f, 0.002f, 5);
            }
        }

        /// <summary>A board nailed on from <paramref name="from"/> to <paramref name="to"/>, its face toward <paramref name="outward"/>, nail heads proud at each end.</summary>
        public static void NailedPlank(B.Model m, Vector3 from, Vector3 to, Vector3 outward, float width, int key)
        {
            Vector3 along = to - from;
            float length = along.magnitude;
            if (length < 0.01f) return;

            Quaternion turn = Quaternion.LookRotation(outward, Vector3.Cross(along / length, outward).normalized);
            Color c = B.Plank(key) * (1f + 0.05f * B.Jitter(key + 3));
            Vector3 middle = (from + to) * 0.5f + outward.normalized * 0.025f;
            m.Box(c, middle, new Vector3(length, width, 0.035f), 0.01f, turn, 2);

            foreach (float t in new[] { 0.06f, 0.94f })
            {
                Vector3 nail = Vector3.Lerp(from, to, t) + outward.normalized * 0.045f;
                m.Tube(B.Iron, nail - outward.normalized * 0.004f, nail + outward.normalized * 0.006f, 0.012f, 6);
            }
        }

        /// <summary>A filled sandbag lying along <paramref name="yaw"/>: a fat pillow pinched at both ends, a seam along the top.</summary>
        public static void Sandbag(B.Model m, Vector3 centre, float yaw, float scale, int key)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 6f * B.Jitter(key));
            Color c = Burlap * (1f + 0.06f * B.Jitter(key + 1));
            S.Ball(m, (p, n) => c * (0.9f + 0.1f * K.Noise(p * 7f + Vector3.one * key)) * (n.y < -0.3f ? 0.8f : 1f), centre,
                   new Vector3(0.32f, 0.12f, 0.2f) * scale, turn, 16, 10);

            for (int s = -1; s <= 1; s += 2)
                S.Ball(m, (p, n) => c * 0.88f, centre + turn * new Vector3(s * 0.31f * scale, 0f, 0f), new Vector3(0.05f, 0.07f, 0.09f) * scale, turn, 8, 6);
        }

        /// <summary>An old tyre, its tread blocked round it, lying flat or stood up: <paramref name="axis"/> is the way the hub faces.</summary>
        public static void Tyre(B.Model m, Vector3 centre, Vector3 axis, float radius, float width)
        {
            Quaternion turn = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            Matrix4x4 was = m.Transform;
            m.Transform = was * Matrix4x4.TRS(centre, turn, Vector3.one);
            try
            {
                float inner = radius * 0.58f, h = width * 0.5f;
                m.Lathe((p, n) =>
                {
                    float r = new Vector2(p.x, p.z).magnitude;
                    float tread = Mathf.Sin(Mathf.Atan2(p.z, p.x) * 18f) > 0.3f && r > radius * 0.93f ? 0.82f : 1f;
                    return Rubber * tread * (Mathf.Abs(p.y) < h * 0.7f ? 1f : 1.15f);
                }, Vector3.zero, new List<Vector2>
                {
                    new Vector2(inner, -h * 0.6f), new Vector2(radius * 0.85f, -h), new Vector2(radius, -h * 0.55f), new Vector2(radius, h * 0.55f),
                    new Vector2(radius * 0.85f, h), new Vector2(inner, h * 0.6f), new Vector2(inner * 0.97f, 0f), new Vector2(inner, -h * 0.6f),
                }, 22);
            }
            finally
            {
                m.Transform = was;
            }
        }

        /// <summary>A traffic cone: square base, orange body with a white band. <paramref name="tipped"/> lays it on its side.</summary>
        public static void Cone(B.Model m, Vector3 foot, float yaw, bool tipped)
        {
            Color orange = B.Hex("C1612F"), white = B.Hex("C9C5B8");
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f) * (tipped ? Quaternion.Euler(0f, 0f, 76f) : Quaternion.identity);
            Vector3 lift = tipped ? Vector3.up * 0.13f : Vector3.zero;

            Matrix4x4 was = m.Transform;
            m.Transform = was * Matrix4x4.TRS(foot + lift, turn, Vector3.one);
            try
            {
                m.Box(B.Hex("2E2E30"), new Vector3(0f, 0.02f, 0f), new Vector3(0.34f, 0.04f, 0.34f), 0.02f, 2);
                m.Lathe((p, n) => p.y > 0.2f && p.y < 0.3f ? white : orange, new Vector3(0f, 0.04f, 0f), new List<Vector2>
                {
                    new Vector2(0f, 0f), new Vector2(0.13f, 0f), new Vector2(0.11f, 0.12f), new Vector2(0.09f, 0.2f), new Vector2(0.09f, 0.2f),
                    new Vector2(0.075f, 0.3f), new Vector2(0.075f, 0.3f), new Vector2(0.03f, 0.48f), new Vector2(0f, 0.49f),
                }, 14);
            }
            finally
            {
                m.Transform = was;
            }
        }

        /// <summary>
        /// Spray paint on a wall: loops and a tag, as raised strokes a few millimetres off it, so they
        /// catch the light rather than lying in it. <paramref name="right"/> and <paramref name="up"/> span the wall.
        /// </summary>
        public static void Graffiti(B.Model m, Vector3 centre, Vector3 right, Vector3 up, Vector3 outward, float size, Color colour, int key)
        {
            outward = outward.normalized;
            int strokes = 3 + B.Hash(key) % 2;
            for (int s = 0; s < strokes; s++)
            {
                var points = new List<Vector3>();
                float phase = B.Jitter(key * 7 + s) * 3f;
                float x0 = -0.5f + s / (float)strokes;
                for (int k = 0; k <= 8; k++)
                {
                    float t = k / 8f;
                    float x = (x0 + t / strokes * 0.9f) * size;
                    float y = Mathf.Sin(t * Mathf.PI * 2f + phase) * size * 0.22f;
                    points.Add(centre + right.normalized * x + up.normalized * y + outward * 0.012f);
                }

                K.Sweep(m, (p, n) => colour, points, 0.02f * size, 6, 3, false, new Vector2(1f, 0.35f));
            }
        }

        /// <summary>A lumpy black rubbish sack, tied at the top.</summary>
        public static void RubbishSack(B.Model m, Vector3 foot, float scale, int key)
        {
            Color black = B.Hex("2D2F31");
            S.Ball(m, (p, n) => black * (1f + 0.15f * Mathf.Max(0f, n.y)) * (0.9f + 0.1f * K.Noise(p * 9f + Vector3.one * key)),
                   foot + Vector3.up * 0.2f * scale, new Vector3(0.24f, 0.21f, 0.22f) * scale, Quaternion.Euler(0f, key * 37f, 8f * B.Jitter(key)), 14, 10);
            m.Tube(black, foot + Vector3.up * 0.38f * scale, foot + Vector3.up * 0.48f * scale, 0.04f * scale, 6);
            S.Ball(m, (p, n) => black, foot + Vector3.up * 0.5f * scale, new Vector3(0.07f, 0.04f, 0.05f) * scale, Quaternion.identity, 8, 6);
        }

        /// <summary>A little glowing ball - a bulb - in the glow submesh.</summary>
        public static void GlowBall(B.Model m, Vector3 centre, float r)
        {
            var profile = new List<Vector2>();
            for (int i = 0; i <= 6; i++)
            {
                float a = -Mathf.PI * 0.5f + i * Mathf.PI / 6f;
                profile.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
            }

            m.Lathe((p, n) => Color.white, centre, profile, 10, B.Model.Glow);
        }

        /// <summary>A rounded box in the glow submesh: a lit panel.</summary>
        public static void GlowBox(B.Model m, Vector3 centre, Vector3 size, Quaternion turn)
        {
            var shape = new SmoothMesh(1);
            shape.RoundedBox(0, centre, size, Mathf.Min(0.01f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.4f), turn, 2);
            m.Add(shape, Color.white, B.Model.Glow);
        }

        /// <summary>A swag of bulbs on a wire from <paramref name="from"/> to <paramref name="to"/>, sagging between hooks.</summary>
        public static void BulbString(B.Model m, Vector3 from, Vector3 to, int bulbs, float sag)
        {
            var wire = new List<Vector3>();
            for (int k = 0; k <= bulbs * 2; k++)
            {
                float t = k / (float)(bulbs * 2);
                float dip = Mathf.Abs(Mathf.Sin(t * Mathf.PI * bulbs)) * sag;
                wire.Add(Vector3.Lerp(from, to, t) + Vector3.down * dip);
            }

            K.Sweep(m, (p, n) => K.Trim, wire, 0.006f, 5, 2);
            for (int k = 0; k < bulbs; k++)
            {
                Vector3 at = wire[k * 2 + 1];
                m.Tube(K.Trim, at, at + Vector3.down * 0.03f, 0.011f, 6);
                GlowBall(m, at + Vector3.down * 0.055f, 0.03f);
            }
        }

        /// <summary>A sign board on two posts: a framed panel, a header in another colour, posts planted on concrete feet.</summary>
        public static void PostSign(B.Model m, Vector3 foot, float yaw, Vector2 board, float height, Color frame, Color face, Color header)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 centre = foot + Vector3.up * (height + board.y * 0.5f);

            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 post = foot + turn * new Vector3(s * (board.x * 0.5f - 0.08f), 0f, 0.06f);
                m.Box(Concrete, post + Vector3.up * 0.05f, new Vector3(0.2f, 0.1f, 0.2f), 0.03f, turn, 2);
                m.Box(B.Walnut, post + Vector3.up * (height + board.y) * 0.5f, new Vector3(0.08f, height + board.y, 0.08f), 0.02f, turn, 2);
            }

            m.Box(frame, centre, new Vector3(board.x, board.y, 0.08f), 0.03f, turn, 2);
            m.Box(face, centre + turn * new Vector3(0f, -0.04f, -0.035f), new Vector3(board.x - 0.12f, board.y - 0.2f, 0.03f), 0.012f, turn, 2);
            m.Box(header, centre + turn * new Vector3(0f, board.y * 0.5f - 0.08f, -0.036f), new Vector3(board.x - 0.12f, 0.1f, 0.032f), 0.012f, turn, 2);
        }
    }
}
