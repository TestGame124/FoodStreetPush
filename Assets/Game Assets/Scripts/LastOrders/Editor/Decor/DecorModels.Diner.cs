using System.Collections.Generic;
using UnityEngine;
using Model = LastOrders.EditorTools.BuildingKit.Model;

namespace LastOrders.EditorTools
{
    public static partial class DecorModels
    {
        /// <summary>
        /// A diner jukebox: honey-wood pillars bent over into an arch, a cushion-red crown with a glass
        /// dome, a lit tube following the arch and down both sides, a cream cabinet with the title
        /// cards, a row of buttons and a chrome-barred speaker grille, on a dark teal plinth.
        /// </summary>
        public static Model Jukebox()
        {
            Model m = DecorKit.New();
            const float Depth = 0.52f, PillarX = 0.335f, PillarW = 0.13f, Spring = 1.02f, Front = -0.22f;
            float b = F;

            DecorKit.Box(m, DecorKit.TealDark, new Vector3(0f, b + 0.05f, 0.01f), new Vector3(0.84f, 0.1f, Depth + 0.05f), 0.03f);

            // The cabinet between the pillars, set back from their faces.
            float inner = PillarX - PillarW * 0.5f;
            DecorKit.Box(m, DecorKit.Cream, new Vector3(0f, b + (0.1f + Spring) * 0.5f, 0.01f),
                         new Vector3(inner * 2f + 0.02f, Spring - 0.1f, -Front * 2f + 0.02f), 0.02f);

            // The crown: a red half drum filling the arch, its lower half inside the cabinet.
            float crownR = inner + 0.005f;
            m.Transform = Matrix4x4.TRS(new Vector3(0f, b + Spring, 0.01f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            float half = -Front;
            m.Lathe((p, n) => DecorKit.Tone(DecorKit.Red, DecorKit.Light(-p.z, new Vector3(n.x, -n.z, n.y), -crownR, crownR)), Vector3.zero,
                    new List<Vector2>
                    {
                        V(0f, -half), V(crownR - 0.02f, -half), V(crownR, -half + 0.02f), V(crownR, half - 0.02f), V(crownR - 0.02f, half), V(0f, half),
                    }, 28);
            m.Transform = Matrix4x4.identity;

            // The pillars and the arch they bend into, the arch starting a little below the pillars'
            // tops so it covers their rounded edges; a cushion-red band runs up one pillar, over and
            // down the other.
            const float Band = 0.16f, Overlap = 8f;
            float archLow = b + Spring - 0.1f, archHigh = b + Spring + PillarX + 0.06f;
            for (int s = -1; s <= 1; s += 2)
            {
                var pillar = new Vector3(s * PillarX, b + 0.09f + (Spring - 0.09f) * 0.5f, 0f);
                DecorKit.Box(m, DecorKit.Honey, pillar, new Vector3(PillarW, Spring - 0.09f, Depth), 0.045f);
                DecorKit.Box(m, DecorKit.Red, pillar + Vector3.up * 0.02f, new Vector3(PillarW + 0.012f, Spring - 0.13f, Band), 0.01f);
            }

            DecorKit.Arch(m, DecorKit.Painted(DecorKit.Honey, archLow, archHigh), new Vector3(0f, b + Spring, 0f),
                          PillarX, PillarW, Depth, -Overlap, 180f + Overlap, 30);
            DecorKit.Arch(m, DecorKit.Painted(DecorKit.Red, archLow, archHigh), new Vector3(0f, b + Spring, 0f),
                          PillarX, PillarW + 0.012f, Band, -Overlap * 0.5f, 180f + Overlap * 0.5f, 30);

            // The dome's glass on the crown's face, a chrome rim round it and a streak of reflection across it.
            var dome = new Vector3(0f, b + Spring, Front - 0.004f);
            const float DomeR = 0.2f;
            m.Surface((p, n) =>
            {
                float streak = Mathf.Abs((p.x - dome.x) * 0.8f + (p.y - dome.y) - 0.09f);
                float shine = 1f - Mathf.Clamp01((streak - 0.012f) / 0.03f);
                return Color.Lerp(DecorKit.Tone(DecorKit.TealDark, 0.85f + 0.3f * (p.y - dome.y) / DomeR), DecorKit.TealLight, shine * 0.65f);
            }, 10, 18, (u, v) =>
            {
                // Not quite to the middle: the fan's centre has no direction to take a normal from.
                float a = v * Mathf.PI;
                return dome + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (Mathf.Lerp(0.03f, 1f, u) * DomeR);
            });
            DecorKit.Arch(m, DecorKit.Painted(DecorKit.Chrome, b + Spring, b + Spring + DomeR), dome + Vector3.back * 0.004f, DomeR + 0.008f,
                          0.022f, 0.02f, 0f, 180f, 20);

            // The lit tube: round the arch and down both sides of the cabinet face.
            const float TubeR = 0.24f;
            DecorKit.Arch(m, (p, n) => Color.white, new Vector3(0f, b + Spring, Front - 0.014f), TubeR, 0.036f, 0.03f, 0f, 180f, 24, Model.Glow);
            for (int s = -1; s <= 1; s += 2)
                m.Tube((p, n) => Color.white, new Vector3(s * TubeR, b + 0.16f, Front - 0.014f), new Vector3(s * TubeR, b + Spring + 0.002f, Front - 0.014f),
                       0.017f, 0.017f, 10, Model.Glow);

            // The title cards: two rows of four on a red backing.
            float cards = b + 0.79f;
            DecorKit.Box(m, DecorKit.RedDark, new Vector3(0f, cards, Front - 0.008f), new Vector3(0.4f, 0.16f, 0.02f), 0.012f);
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < 4; col++)
            {
                var at = new Vector3(-0.135f + col * 0.09f, cards + 0.035f - row * 0.07f, Front - 0.019f);
                DecorKit.Box(m, DecorKit.Tone(DecorKit.CreamLight, 1f + 0.03f * Jitter(row * 4 + col)), at, new Vector3(0.078f, 0.052f, 0.008f), 0.006f, 1);
                DecorKit.Box(m, DecorKit.Red, at + new Vector3(0f, 0.014f, -0.004f), new Vector3(0.06f, 0.008f, 0.003f), 0.002f, 1);
            }

            // The buttons.
            float keys = b + 0.665f;
            DecorKit.Box(m, DecorKit.Chrome, new Vector3(0f, keys, Front - 0.01f), new Vector3(0.4f, 0.045f, 0.03f), 0.014f);
            for (int k = 0; k < 6; k++)
            {
                var at = new Vector3(-0.15f + k * 0.06f, keys + 0.002f, Front - 0.03f);
                DecorKit.Box(m, k % 2 == 0 ? DecorKit.RedLight : DecorKit.CreamLight, at, new Vector3(0.036f, 0.026f, 0.022f), 0.008f, 1);
            }

            // The speaker grille: chrome frame, dark red cloth, chrome bars.
            float grille = b + 0.38f;
            DecorKit.Box(m, DecorKit.Chrome, new Vector3(0f, grille, Front - 0.01f), new Vector3(0.42f, 0.4f, 0.025f), 0.03f);
            DecorKit.Box(m, DecorKit.RedDark, new Vector3(0f, grille, Front - 0.018f), new Vector3(0.36f, 0.34f, 0.02f), 0.02f);
            for (int k = 0; k < 7; k++)
            {
                float x = -0.15f + k * 0.05f;
                DecorKit.Rod(m, DecorKit.Chrome, new Vector3(x, grille - 0.165f, Front - 0.03f), new Vector3(x, grille + 0.165f, Front - 0.03f), 0.008f, 0.008f, 6);
            }

            return m;
        }

        /// <summary>
        /// An A-frame menu board: two honey-wood framed chalkboards hinged on a teal rod and held apart
        /// by chrome stays. The front is chalked up - a red heading tag, five lines of dishes with their
        /// prices in butter, a little cup in the corner; the back is wiped clean.
        /// </summary>
        public static Model MenuBoard()
        {
            Model m = DecorKit.New();
            const float Tilt = 13f, Length = 0.95f;
            float hinge = F + Length * Mathf.Cos(Tilt * Mathf.Deg2Rad);

            Quaternion front = Quaternion.Euler(Tilt, 0f, 0f);
            Quaternion back = Quaternion.Euler(0f, 180f, 0f) * front;
            var top = new Vector3(0f, hinge, 0f);

            m.Transform = Matrix4x4.TRS(top, front, Vector3.one);
            BoardPanel(m, Length, true);
            m.Transform = Matrix4x4.TRS(top, back, Vector3.one);
            BoardPanel(m, Length, false);
            m.Transform = Matrix4x4.identity;

            DecorKit.Rod(m, DecorKit.Teal, top + new Vector3(-0.31f, 0.012f, 0f), top + new Vector3(0.31f, 0.012f, 0f), 0.02f, 0.02f, 10);
            for (int s = -1; s <= 1; s += 2)
            {
                var local = new Vector3(s * 0.27f, -0.6f, 0.02f);
                DecorKit.Rod(m, DecorKit.Chrome, top + front * local, top + back * local, 0.006f, 0.006f, 6);
            }

            return m;
        }

        /// <summary>One leaf of the A-frame, hanging from its hinge at the origin, its face toward -Z.</summary>
        static void BoardPanel(Model m, float length, bool chalked)
        {
            const float Width = 0.58f;
            for (int s = -1; s <= 1; s += 2)
                DecorKit.Box(m, DecorKit.Honey, new Vector3(s * (Width * 0.5f - 0.025f), -length * 0.5f, 0f), new Vector3(0.05f, length, 0.036f), 0.014f);
            DecorKit.Box(m, DecorKit.HoneyEdge, new Vector3(0f, -0.032f, 0f), new Vector3(Width + 0.01f, 0.064f, 0.042f), 0.016f);
            DecorKit.Box(m, DecorKit.Honey, new Vector3(0f, -0.8f, 0f), new Vector3(Width - 0.06f, 0.05f, 0.034f), 0.012f);

            // The slate, recessed in the frame.
            DecorKit.Box(m, DecorKit.Slate, new Vector3(0f, -0.42f, 0.003f), new Vector3(Width - 0.08f, 0.72f, 0.018f), 0.006f);
            if (!chalked)
            {
                DecorKit.Box(m, DecorKit.Tone(DecorKit.Slate, 1.15f), new Vector3(0.06f, -0.36f, -0.0075f), new Vector3(0.22f, 0.012f, 0.002f), 0.001f, 1);
                return;
            }

            const float Face = -0.008f;

            // The heading: a red tag with chalk letters.
            DecorKit.Box(m, DecorKit.Red, new Vector3(0f, -0.145f, Face), new Vector3(0.32f, 0.08f, 0.006f), 0.012f, 1);
            for (int k = 0; k < 4; k++)
                DecorKit.Box(m, DecorKit.Chalk, new Vector3(-0.09f + k * 0.06f, -0.145f, Face - 0.004f), new Vector3(0.038f, 0.042f, 0.003f), 0.004f, 1);
            DecorKit.Box(m, DecorKit.Chalk, new Vector3(0f, -0.205f, Face), new Vector3(0.24f, 0.008f, 0.003f), 0.002f, 1);

            // Five dishes and their prices.
            for (int row = 0; row < 5; row++)
            {
                float y = -0.27f - row * 0.075f;
                float words = 0.15f + 0.07f * (0.5f + 0.5f * Jitter(row * 11 + 3));
                DecorKit.Box(m, DecorKit.Chalk, new Vector3(-0.2f + words * 0.5f, y, Face), new Vector3(words, 0.02f, 0.003f), 0.004f, 1);
                DecorKit.Box(m, DecorKit.Butter, new Vector3(0.17f, y, Face), new Vector3(0.06f, 0.022f, 0.003f), 0.004f, 1);
            }

            // A cup of coffee drawn in the corner, steam over it.
            var cup = new Vector3(-0.14f, -0.7f, Face);
            DecorKit.Box(m, DecorKit.Chalk, cup, new Vector3(0.07f, 0.05f, 0.003f), 0.01f, 1);
            DecorKit.Box(m, DecorKit.Chalk, cup + new Vector3(0f, -0.032f, 0f), new Vector3(0.1f, 0.008f, 0.003f), 0.003f, 1);
            DecorKit.Arch(m, DecorKit.Painted(DecorKit.Chalk, -1f, 0f), cup + new Vector3(0.038f, 0f, 0f), 0.016f, 0.008f, 0.003f, -90f, 90f, 8);
            for (int k = 0; k < 2; k++)
                DecorKit.Box(m, DecorKit.Chalk, cup + new Vector3(-0.012f + k * 0.024f, 0.055f, 0f), new Vector3(0.007f, 0.04f, 0.003f), 0.003f,
                             Quaternion.Euler(0f, 0f, 12f - k * 24f), 1);
            DecorKit.Box(m, DecorKit.Coral, new Vector3(0.12f, -0.69f, Face), new Vector3(0.11f, 0.06f, 0.004f), 0.03f, 1);
        }

        /// <summary>
        /// A gumball machine on a teal pedestal: a heavy round foot, a cream collar, a cushion-red body
        /// with a chrome coin plate, turning knob and chute, and a globe packed with gumballs in the
        /// diner's colours under a red cap.
        /// </summary>
        public static Model GumballMachine()
        {
            Model m = DecorKit.New();
            var foot = new Vector3(0f, F, 0f);

            DecorKit.Turned(m, DecorKit.Teal, foot, new List<Vector2>
            {
                V(0f, 0f), V(0.18f, 0f), V(0.19f, 0.012f), V(0.185f, 0.034f), V(0.14f, 0.058f), V(0.07f, 0.08f), V(0.045f, 0.095f), V(0f, 0.097f),
            }, 26);
            DecorKit.Rod(m, DecorKit.Teal, foot + Vector3.up * 0.09f, foot + Vector3.up * 0.52f, 0.038f, 0.03f, 14);
            DecorKit.Turned(m, DecorKit.Cream, foot + Vector3.up * 0.27f, new List<Vector2>
            {
                V(0f, 0f), V(0.04f, 0f), V(0.052f, 0.014f), V(0.052f, 0.036f), V(0.04f, 0.05f), V(0f, 0.05f),
            }, 16);

            // The body, and on its face the coin plate, knob and chute.
            const float Body = 0.5f;
            DecorKit.Turned(m, DecorKit.Red, foot, new List<Vector2>
            {
                V(0f, Body), V(0.08f, Body), V(0.115f, Body + 0.014f), V(0.138f, Body + 0.05f), V(0.143f, Body + 0.13f),
                V(0.138f, Body + 0.22f), V(0.125f, Body + 0.262f), V(0.105f, Body + 0.276f), V(0f, Body + 0.28f),
            }, 26);

            float plate = F + Body + 0.15f;
            DecorKit.Box(m, DecorKit.Chrome, new Vector3(0f, plate, -0.13f), new Vector3(0.13f, 0.15f, 0.04f), 0.016f);
            m.Transform = Matrix4x4.TRS(new Vector3(0f, plate + 0.01f, -0.148f), Quaternion.Euler(-90f, 0f, 0f), Vector3.one);
            DecorKit.Turned(m, DecorKit.Chrome, Vector3.zero, new List<Vector2>
            {
                V(0f, 0f), V(0.044f, 0f), V(0.05f, 0.01f), V(0.05f, 0.024f), V(0.04f, 0.034f), V(0f, 0.036f),
            }, 18);
            m.Transform = Matrix4x4.identity;
            DecorKit.Box(m, DecorKit.ChromeDark, new Vector3(0f, plate + 0.01f, -0.19f), new Vector3(0.075f, 0.018f, 0.018f), 0.007f, 1);
            DecorKit.Box(m, DecorKit.ChromeDark, new Vector3(0f, F + Body + 0.06f, -0.125f), new Vector3(0.075f, 0.05f, 0.04f), 0.012f);
            DecorKit.Box(m, DecorKit.Ink, new Vector3(0f, F + Body + 0.055f, -0.146f), new Vector3(0.052f, 0.028f, 0.006f), 0.004f, 1);

            // The globe: a seat ring, pale glass, and gumballs pressed against it all round.
            float ring = F + Body + 0.275f;
            DecorKit.Turned(m, DecorKit.Chrome, new Vector3(0f, ring, 0f), new List<Vector2>
            {
                V(0f, 0f), V(0.14f, 0f), V(0.152f, 0.012f), V(0.15f, 0.03f), V(0.13f, 0.036f), V(0f, 0.036f),
            }, 26);

            const float Globe = 0.175f, Gum = 0.034f;
            var centre = new Vector3(0f, ring + Globe - 0.002f, 0f);
            DecorKit.Ball(m, DecorKit.Hex("BFD6CF"), centre, Globe - Gum * 0.75f, 24, 12);

            Color[] gums = { DecorKit.Red, DecorKit.Butter, DecorKit.CreamLight, DecorKit.TealLight, DecorKit.LeafLight, DecorKit.Coral };
            const int Count = 72;
            for (int i = 0; i < Count; i++)
            {
                // Spread evenly over the sphere: a Fibonacci spiral.
                float y = 1f - 2f * (i + 0.5f) / Count;
                float r = Mathf.Sqrt(1f - y * y);
                float a = i * 2.39996f;
                var dir = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                if (dir.y < -0.55f) continue;
                DecorKit.Ball(m, gums[BuildingKit.Hash(i * 7 + 3) % gums.Length], centre + dir * (Globe - Gum), Gum, 8, 4);
            }

            DecorKit.Turned(m, DecorKit.Red, centre + Vector3.up * (Globe - 0.03f), new List<Vector2>
            {
                V(0f, 0f), V(0.09f, 0f), V(0.1f, 0.014f), V(0.088f, 0.04f), V(0.05f, 0.058f), V(0f, 0.062f),
            }, 22);
            DecorKit.Ball(m, DecorKit.Chrome, centre + Vector3.up * (Globe + 0.05f), 0.022f, 12, 6);

            return m;
        }
    }
}
