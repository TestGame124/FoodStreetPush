using System.Collections.Generic;
using UnityEngine;
using Model = LastOrders.EditorTools.BuildingKit.Model;

namespace LastOrders.EditorTools
{
    public static partial class DecorModels
    {
        /// <summary>
        /// A standard lamp: a heavy teal foot, a chrome pole with a honey-wood collar, and a cream drum
        /// shade trimmed in cushion red, open at the top so the lit bulb shows from above at night.
        /// </summary>
        public static Model FloorLamp()
        {
            Model m = DecorKit.New();
            var foot = new Vector3(0f, F, 0f);

            DecorKit.Turned(m, DecorKit.Teal, foot, new List<Vector2>
            {
                V(0f, 0f), V(0.165f, 0f), V(0.175f, 0.014f), V(0.165f, 0.038f), V(0.1f, 0.064f), V(0.042f, 0.08f), V(0.03f, 0.092f), V(0f, 0.092f),
            }, 26);
            DecorKit.Rod(m, DecorKit.Chrome, foot + Vector3.up * 0.08f, foot + Vector3.up * 1.33f, 0.018f, 0.015f, 10);
            DecorKit.Turned(m, DecorKit.Honey, foot + Vector3.up * 0.58f, new List<Vector2>
            {
                V(0f, 0f), V(0.026f, 0f), V(0.04f, 0.018f), V(0.042f, 0.05f), V(0.03f, 0.075f), V(0f, 0.078f),
            }, 16);
            DecorKit.Turned(m, DecorKit.ChromeDark, foot + Vector3.up * 1.3f, new List<Vector2>
            {
                V(0f, 0f), V(0.028f, 0f), V(0.034f, 0.035f), V(0.026f, 0.06f), V(0f, 0.062f),
            }, 12);
            DecorKit.Ball(m, Color.white, foot + Vector3.up * 1.4f, 0.055f, 14, 7, Model.Glow);

            // The shade: up the outside, over the top lip, down the inside, round the bottom lip.
            const float Low = 1.2f, High = 1.53f, BandLow = 1.236f, BandHigh = 1.492f;
            float Outer(float y) => Mathf.Lerp(0.28f, 0.18f, (y - Low) / (High - Low));
            float Inner(float y) => Outer(y) - 0.012f;
            var shade = new List<Vector2> { V(Inner(Low + 0.008f), Low + 0.004f), V(Outer(Low) - 0.004f, Low - 0.003f), V(Outer(Low) + 0.004f, Low + 0.008f) };
            foreach (float y in new[] { BandLow - 0.002f, BandLow + 0.002f, BandHigh - 0.002f, BandHigh + 0.002f, High - 0.01f })
                shade.Add(V(Outer(y) + 0.004f, y));
            shade.Add(V(Outer(High) - 0.002f, High + 0.002f));
            shade.Add(V(Inner(High) - 0.002f, High - 0.002f));
            shade.Add(V(Inner(High - 0.012f), High - 0.012f));
            shade.Add(V(Inner(Low + 0.012f), Low + 0.012f));

            m.Lathe((p, n) =>
            {
                float y = p.y - F;
                bool inside = n.x * p.x + n.z * p.z < 0f;
                if (inside) return DecorKit.Tone(DecorKit.CreamLight, 1.04f);
                Color c = y < BandLow || y > BandHigh ? DecorKit.Red : DecorKit.Cream;
                return DecorKit.Tone(c, DecorKit.Light(y, n, Low, High));
            }, foot, shade, 30);

            return m;
        }

        /// <summary>
        /// A dresser of open honey-wood shelves in a teal case with a cream back, like the dining
        /// chairs' teal frames and cream shells, set to the back of its two cells so it can stand
        /// against a wall: stacks of the diner's plates, jars of honey, jam and pickles, mugs, a
        /// teapot, plates stood on show and potted plants, one trailing over the top.
        /// </summary>
        public static Model DisplayShelf()
        {
            Model m = DecorKit.New();
            const float W = 1.66f, D = 0.4f, H = 1.24f, Z = 0.27f;
            float front = Z - D * 0.5f;

            for (int s = -1; s <= 1; s += 2)
                DecorKit.Box(m, DecorKit.Teal, new Vector3(s * (W * 0.5f - 0.025f), F + H * 0.5f, Z), new Vector3(0.05f, H, D), 0.016f);
            DecorKit.Box(m, DecorKit.TealLight, new Vector3(0f, F + H + 0.024f, Z - 0.005f), new Vector3(W + 0.06f, 0.048f, D + 0.04f), 0.018f);
            DecorKit.Box(m, DecorKit.Cream, new Vector3(0f, F + H * 0.5f, Z + D * 0.5f - 0.012f), new Vector3(W - 0.05f, H - 0.02f, 0.022f), 0.006f);
            DecorKit.Box(m, DecorKit.TealDark, new Vector3(0f, F + 0.045f, front + 0.02f), new Vector3(W - 0.05f, 0.09f, 0.03f), 0.01f);

            float[] shelves = { 0.1f, 0.5f, 0.88f };
            foreach (float y in shelves)
                DecorKit.Box(m, DecorKit.Tone(DecorKit.Honey, 1.02f), new Vector3(0f, F + y, Z), new Vector3(W - 0.05f, 0.034f, D - 0.02f), 0.012f);

            float Top(int shelf) => F + shelves[shelf] + 0.017f;
            float mid = Z - 0.02f;

            // Bottom: stacks of plates and a big jar of honey.
            for (int k = 0; k < 6; k++) Plate(m, new Vector3(-0.56f, Top(0) + k * 0.017f, mid), 0.11f);
            for (int k = 0; k < 4; k++) Plate(m, new Vector3(-0.3f, Top(0) + k * 0.017f, mid), 0.1f);
            Jar(m, new Vector3(0.05f, Top(0), mid), 0.085f, 0.26f, DecorKit.Butter, DecorKit.Red);
            for (int k = 0; k < 5; k++) Plate(m, new Vector3(0.4f, Top(0) + k * 0.017f, mid + 0.01f), 0.1f);
            Mug(m, new Vector3(0.62f, Top(0), mid - 0.04f), DecorKit.Teal, 0f);

            // Middle: jars and mugs.
            Jar(m, new Vector3(-0.6f, Top(1), mid), 0.055f, 0.16f, DecorKit.Red, DecorKit.CreamLight);
            Jar(m, new Vector3(-0.47f, Top(1), mid + 0.03f), 0.06f, 0.2f, DecorKit.Butter, DecorKit.Teal);
            Jar(m, new Vector3(-0.33f, Top(1), mid - 0.01f), 0.05f, 0.14f, DecorKit.LeafLight, DecorKit.Red);
            for (int k = 0; k < 3; k++) Mug(m, new Vector3(-0.08f + k * 0.13f, Top(1), mid - 0.03f), DecorKit.Red, 20f * k);
            Teapot(m, new Vector3(0.52f, Top(1), mid));

            // Top: plates stood up on show, a plant.
            for (int k = 0; k < 3; k++)
            {
                var at = new Vector3(-0.5f + k * 0.21f, Top(2) + 0.11f, Z + D * 0.5f - 0.07f);
                m.Transform = Matrix4x4.TRS(at, Quaternion.Euler(-78f, 0f, 0f), Vector3.one);
                Plate(m, Vector3.zero, 0.1f);
                m.Transform = Matrix4x4.identity;
            }

            PotPlant(m, new Vector3(0.3f, Top(2), mid), 0.75f, false, 1);
            Jar(m, new Vector3(0.58f, Top(2), mid), 0.06f, 0.18f, DecorKit.Coral, DecorKit.Teal);

            // On top: a plant trailing down over the edge.
            PotPlant(m, new Vector3(-0.45f, F + H + 0.048f, Z - 0.04f), 0.8f, true, 2);
            PotPlant(m, new Vector3(0.5f, F + H + 0.048f, Z), 0.65f, false, 3);

            return m;
        }

        /// <summary>
        /// An arcade cabinet: cushion-red sides edged in cream moulding round a teal body, a lit
        /// marquee with a red title, a dark screen of bright sprites, a control deck with a red-ball
        /// stick and three buttons, a chrome coin door and a dark kick plate.
        /// </summary>
        public static Model ArcadeCabinet()
        {
            Model m = DecorKit.New();

            // The side's outline (z, y): two halves, each visible whole from its own middle.
            var lower = new List<Vector2> { V(-0.3f, 0f), V(-0.3f, 0.78f), V(-0.4f, 0.88f), V(-0.38f, 0.93f), V(-0.18f, 1f), V(0.32f, 1f), V(0.32f, 0f) };
            var upper = new List<Vector2> { V(-0.18f, 1f), V(-0.08f, 1.44f), V(-0.13f, 1.72f), V(0.32f, 1.72f), V(0.32f, 1f) };
            List<Vector2> Up(List<Vector2> outline) => outline.ConvertAll(p => p + new Vector2(0f, F));

            const float Body = 0.27f, Side = 0.04f;
            DecorKit.Prism(m, DecorKit.Teal, Up(lower), -Body, Body);
            DecorKit.Prism(m, DecorKit.Teal, Up(upper), -Body, Body);
            for (int s = -1; s <= 1; s += 2)
            {
                float x0 = s < 0 ? -Body - Side : Body, x1 = x0 + Side;
                DecorKit.Prism(m, DecorKit.Red, Up(lower), x0, x1);
                DecorKit.Prism(m, DecorKit.Red, Up(upper), x0, x1);

                // Cream T-moulding round the edge of each side, not along the floor.
                var edge = new List<Vector2> { V(-0.3f, 0f), V(-0.3f, 0.78f), V(-0.4f, 0.88f), V(-0.38f, 0.93f), V(-0.18f, 1f), V(-0.08f, 1.44f), V(-0.13f, 1.72f), V(0.32f, 1.72f), V(0.32f, 0f) };
                float x = s * (Body + Side);
                for (int i = 0; i < edge.Count; i++)
                {
                    var a = new Vector3(x, edge[i].y + F, edge[i].x);
                    if (i > 0 && i < edge.Count - 1) DecorKit.Ball(m, DecorKit.Cream, a, 0.017f, 10, 5);
                    if (i < edge.Count - 1)
                        DecorKit.Rod(m, DecorKit.Cream, a, new Vector3(x, edge[i + 1].y + F, edge[i + 1].x), 0.017f, 0.017f, 8);
                }
            }

            // Kick plate, front panel with stripes, coin door.
            DecorKit.Box(m, DecorKit.TealDark, new Vector3(0f, F + 0.05f, -0.305f), new Vector3(0.54f, 0.1f, 0.02f), 0.008f);
            DecorKit.Box(m, DecorKit.Cream, new Vector3(0f, F + 0.66f, -0.305f), new Vector3(0.5f, 0.2f, 0.016f), 0.01f);
            DecorKit.Box(m, DecorKit.Red, new Vector3(0f, F + 0.69f, -0.315f), new Vector3(0.5f, 0.03f, 0.006f), 0.003f, 1);
            DecorKit.Box(m, DecorKit.Butter, new Vector3(0f, F + 0.64f, -0.315f), new Vector3(0.5f, 0.03f, 0.006f), 0.003f, 1);
            DecorKit.Box(m, DecorKit.Chrome, new Vector3(0f, F + 0.36f, -0.31f), new Vector3(0.24f, 0.26f, 0.03f), 0.016f);
            for (int s = -1; s <= 1; s += 2)
            {
                DecorKit.Box(m, DecorKit.Red, new Vector3(s * 0.05f, F + 0.41f, -0.328f), new Vector3(0.04f, 0.07f, 0.012f), 0.006f, 1);
                DecorKit.Box(m, DecorKit.Ink, new Vector3(s * 0.05f, F + 0.415f, -0.335f), new Vector3(0.006f, 0.04f, 0.004f), 0.002f, 1);
            }

            // The control deck along the slope from the panel's lip to the screen.
            Vector3 deckFrom = new Vector3(0f, F + 0.93f, -0.38f), deckTo = new Vector3(0f, F + 1f, -0.18f);
            Quaternion deck = Quaternion.LookRotation(deckTo - deckFrom, Vector3.up);
            Vector3 deckUp = deck * Vector3.up;
            Vector3 DeckAt(float x, float t) => Vector3.Lerp(deckFrom, deckTo, t) + Vector3.right * x + deckUp * 0.008f;
            DecorKit.Box(m, DecorKit.Ink, DeckAt(0f, 0.5f), new Vector3(0.52f, 0.012f, (deckTo - deckFrom).magnitude - 0.02f), 0.005f, deck, 1);
            Vector3 stick = DeckAt(-0.12f, 0.45f);
            DecorKit.Ball(m, DecorKit.ChromeDark, stick, 0.026f, 10, 5);
            DecorKit.Rod(m, DecorKit.Ink, stick, stick + deckUp * 0.075f, 0.008f, 0.008f, 6);
            DecorKit.Ball(m, DecorKit.RedLight, stick + deckUp * 0.085f, 0.03f, 12, 6);
            Color[] buttons = { DecorKit.Butter, DecorKit.TealLight, DecorKit.RedLight };
            for (int k = 0; k < 3; k++)
            {
                m.Transform = Matrix4x4.TRS(DeckAt(0.02f + k * 0.075f, 0.5f), deck, Vector3.one);
                DecorKit.Turned(m, buttons[k], Vector3.zero, new List<Vector2> { V(0f, 0f), V(0.024f, 0f), V(0.024f, 0.008f), V(0.018f, 0.016f), V(0f, 0.018f) }, 12);
                m.Transform = Matrix4x4.identity;
            }

            // The screen, leaning back between the deck and the marquee, with its sprites.
            Vector3 screenLow = new Vector3(0f, F + 1f, -0.18f), screenHigh = new Vector3(0f, F + 1.44f, -0.08f);
            Quaternion screen = Quaternion.LookRotation(Vector3.Cross(Vector3.right, screenHigh - screenLow).normalized, (screenHigh - screenLow).normalized);
            Vector3 screenMid = (screenLow + screenHigh) * 0.5f;
            m.Transform = Matrix4x4.TRS(screenMid, screen, Vector3.one);
            DecorKit.Box(m, DecorKit.Ink, new Vector3(0f, 0f, 0.006f), new Vector3(0.52f, 0.43f, 0.014f), 0.012f);
            DecorKit.Box(m, DecorKit.Hex("1F3B3E"), new Vector3(0f, 0.005f, 0.0f), new Vector3(0.42f, 0.33f, 0.008f), 0.02f);
            for (int k = 0; k < 5; k++)
                Sprite(m, new Vector2(-0.14f + k * 0.07f, 0.09f), DecorKit.Butter);
            for (int k = 0; k < 4; k++)
                Sprite(m, new Vector2(-0.105f + k * 0.07f, 0.035f), DecorKit.Coral);
            Sprite(m, new Vector2(0.02f, -0.12f), DecorKit.LeafLight);
            DecorKit.Box(m, DecorKit.LeafLight, new Vector3(0.02f, -0.05f, -0.007f), new Vector3(0.006f, 0.03f, 0.004f), 0.002f, 1);
            m.Transform = Matrix4x4.identity;

            // The marquee, lit, with a red frame and a red title.
            Vector3 marqueeLow = screenHigh, marqueeHigh = new Vector3(0f, F + 1.72f, -0.13f);
            Quaternion marquee = Quaternion.LookRotation(Vector3.Cross(Vector3.right, marqueeHigh - marqueeLow).normalized, (marqueeHigh - marqueeLow).normalized);
            m.Transform = Matrix4x4.TRS((marqueeLow + marqueeHigh) * 0.5f + Vector3.up * 0.005f, marquee, Vector3.one);
            DecorKit.Box(m, DecorKit.RedDark, new Vector3(0f, 0f, 0.004f), new Vector3(0.53f, 0.27f, 0.016f), 0.01f);
            var sign = new SmoothMesh(1);
            sign.RoundedBox(0, new Vector3(0f, 0f, -0.002f), new Vector3(0.47f, 0.21f, 0.01f), 0.004f, Quaternion.identity, 1);
            m.Add(sign, Color.white, Model.Glow);
            for (int k = 0; k < 5; k++)
            {
                float h = k % 2 == 0 ? 0.09f : 0.075f;
                DecorKit.Box(m, DecorKit.Red, new Vector3(-0.15f + k * 0.075f, 0.005f, -0.01f), new Vector3(0.055f, h, 0.006f), 0.008f, 1);
            }

            m.Transform = Matrix4x4.identity;
            return m;
        }

        /// <summary>A little blocky screen sprite at (x, y) on a screen face, a body with two feet.</summary>
        static void Sprite(Model m, Vector2 at, Color colour)
        {
            DecorKit.Box(m, colour, new Vector3(at.x, at.y + 0.006f, -0.007f), new Vector3(0.04f, 0.022f, 0.004f), 0.003f, 1);
            for (int s = -1; s <= 1; s += 2)
                DecorKit.Box(m, colour, new Vector3(at.x + s * 0.013f, at.y - 0.012f, -0.007f), new Vector3(0.01f, 0.014f, 0.004f), 0.002f, 1);
        }

        /// <summary>One of the diner's plates lying at <paramref name="at"/>: cream with a red rim.</summary>
        static void Plate(Model m, Vector3 at, float radius)
        {
            // Rings a hair either side of the rim's edge, so the red stops there instead of fading inward.
            m.Lathe((p, n) =>
            {
                float r = new Vector2(p.x - at.x, p.z - at.z).magnitude;
                Color c = r > radius * 0.845f ? DecorKit.Red : DecorKit.CreamLight;
                return DecorKit.Tone(c, n.y > 0.5f ? 1.02f : 0.86f);
            }, at, new List<Vector2>
            {
                V(0f, 0f), V(radius * 0.6f, 0f), V(radius * 0.64f, 0.004f), V(radius * 0.83f, 0.008f), V(radius * 0.86f, 0.0085f),
                V(radius, 0.015f), V(radius * 0.97f, 0.017f), V(radius * 0.86f, 0.0108f), V(radius * 0.83f, 0.0102f),
                V(radius * 0.6f, 0.009f), V(0f, 0.009f),
            }, 14);
        }

        /// <summary>A glass jar <paramref name="height"/> tall, filled three parts with <paramref name="contents"/>, its lid <paramref name="lid"/>.</summary>
        static void Jar(Model m, Vector3 foot, float radius, float height, Color contents, Color lid)
        {
            Color glass = DecorKit.Hex("C9DCD5");
            float fill = height * 0.72f;
            var body = new List<Vector2>
            {
                V(0f, 0f), V(radius * 0.9f, 0f), V(radius, radius * 0.12f), V(radius, fill - 0.002f), V(radius, fill + 0.002f),
                V(radius, height * 0.86f), V(radius * 0.86f, height * 0.92f), V(radius * 0.86f, height * 0.93f), V(0f, height * 0.93f),
            };
            DecorKit.Turned(m, y => y < fill ? contents : glass, foot, body, 16);
            DecorKit.Turned(m, lid, foot + Vector3.up * (height * 0.9f), new List<Vector2>
            {
                V(0f, 0f), V(radius * 0.92f, 0f), V(radius * 0.94f, height * 0.05f), V(radius * 0.88f, height * 0.1f), V(0f, height * 0.1f),
            }, 16);
        }

        /// <summary>A mug, its handle turned <paramref name="yaw"/> from facing +X.</summary>
        static void Mug(Model m, Vector3 foot, Color colour, float yaw)
        {
            const float R = 0.04f, H = 0.09f;
            DecorKit.Turned(m, y => y > H - 0.012f ? DecorKit.CreamLight : colour, foot, new List<Vector2>
            {
                V(0f, 0f), V(R * 0.9f, 0f), V(R, 0.008f), V(R, H - 0.014f), V(R, H - 0.01f), V(R, H), V(R - 0.007f, H),
                V(R - 0.007f, H - 0.01f), V(R - 0.007f, H - 0.014f), V(0f, H - 0.02f),
            }, 14);
            m.Transform = Matrix4x4.TRS(foot + Vector3.up * (H * 0.5f), Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            DecorKit.Arch(m, DecorKit.Painted(colour, -0.03f, 0.03f), new Vector3(R, 0f, 0f), 0.024f, 0.011f, 0.012f, -80f, 80f, 10);
            m.Transform = Matrix4x4.identity;
        }

        /// <summary>A round cream teapot with a red lid and a spout and handle.</summary>
        static void Teapot(Model m, Vector3 foot)
        {
            DecorKit.Turned(m, DecorKit.CreamLight, foot, new List<Vector2>
            {
                V(0f, 0f), V(0.06f, 0f), V(0.09f, 0.03f), V(0.1f, 0.07f), V(0.088f, 0.11f), V(0.055f, 0.13f), V(0f, 0.132f),
            }, 20);
            DecorKit.Turned(m, DecorKit.Red, foot + Vector3.up * 0.125f, new List<Vector2>
            {
                V(0f, 0f), V(0.056f, 0f), V(0.05f, 0.02f), V(0.02f, 0.03f), V(0.015f, 0.045f), V(0f, 0.05f),
            }, 16);
            DecorKit.Rod(m, DecorKit.CreamLight, foot + new Vector3(-0.08f, 0.05f, 0f), foot + new Vector3(-0.145f, 0.12f, 0f), 0.018f, 0.009f, 8);
            DecorKit.Arch(m, DecorKit.Painted(DecorKit.Red, 0f, 1f), foot + new Vector3(0.095f, 0.07f, 0f), 0.04f, 0.014f, 0.016f, -80f, 80f, 10);
        }

        /// <summary>A small teal pot with a clump of leaves; <paramref name="trailing"/> ones hang down over a shelf's edge toward -Z.</summary>
        static void PotPlant(Model m, Vector3 foot, float scale, bool trailing, int key)
        {
            float r = 0.075f * scale, h = 0.1f * scale;
            DecorKit.Turned(m, y => y > h * 0.78f ? DecorKit.Cream : DecorKit.Teal, foot, new List<Vector2>
            {
                V(0f, 0f), V(r * 0.78f, 0f), V(r, h * 0.75f), V(r * 1.04f, h * 0.78f - 0.001f), V(r * 1.06f, h * 0.8f),
                V(r * 1.08f, h), V(r * 0.92f, h), V(r * 0.9f, h * 0.92f), V(0f, h * 0.94f),
            }, 16);

            Vector3 crown = foot + Vector3.up * (h * 0.92f);
            int leaves = trailing ? 9 : 7;
            for (int k = 0; k < leaves; k++)
            {
                Vector3 heading = Quaternion.Euler(0f, k * (360f / leaves) + 20f * Jitter(key * 13 + k), 0f) * Vector3.forward;
                if (trailing && k < 4)
                {
                    // Long leaves out over the front, dropping steeply.
                    Vector3 down = Quaternion.Euler(0f, -40f + k * 27f, 0f) * Vector3.back;
                    DecorKit.LeafBlade(m, crown + down * 0.02f, down, 0.25f, 1.6f, 0.2f * scale + 0.05f * k * scale, 0.07f * scale, 0.15f,
                                       DecorKit.Tone(DecorKit.Leaf, 1.02f + 0.05f * Jitter(key * 7 + k)), 8);
                    continue;
                }

                float length = (0.12f + 0.03f * Jitter(key * 17 + k)) * scale;
                DecorKit.LeafBlade(m, crown + heading * 0.01f, heading, 1.1f, 1.1f, length, length * 0.5f, 0.2f,
                                   DecorKit.Tone(DecorKit.LeafLight, 0.96f + 0.06f * Jitter(key * 19 + k)), 6);
            }
        }
    }
}
