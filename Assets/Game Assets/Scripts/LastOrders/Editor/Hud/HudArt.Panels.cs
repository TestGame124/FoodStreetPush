using System;
using System.Collections.Generic;
using LastOrders.UI.Hud;
using UnityEngine;
using static LastOrders.EditorTools.Hud.Shape;

namespace LastOrders.EditorTools.Hud
{
    /// <summary>
    /// The screens' half of the atlas: the window every screen sits in, the wells, tiles and tabs
    /// inside it, tint-able buttons for the popups that colour their own, a diner checker strip, sun
    /// rays for celebrations, and the icons the shop tabs, storehouse and hints need.
    ///
    /// Same rules as the HUD art: the warm dark outline, the soft top-left light, colours taken from
    /// the diner (cream, teal, red rail, honey oak) so a window opened over the world looks like it
    /// belongs to it.
    /// </summary>
    public static partial class HudArt
    {
        static readonly Color HoneyWood = new Color(0.86f, 0.60f, 0.30f);
        static readonly Color CrateWood = new Color(0.80f, 0.58f, 0.34f);
        static readonly Color LeafGreen = new Color(0.52f, 0.76f, 0.38f);
        static readonly Color StemGreen = new Color(0.36f, 0.58f, 0.30f);
        static readonly Color Soil = new Color(0.58f, 0.40f, 0.25f);
        static readonly Color Burlap = new Color(0.88f, 0.74f, 0.50f);
        static readonly Color Glove = new Color(1f, 0.98f, 0.94f);

        /// <summary>Where the window's title band ends, in pixels from its top.</summary>
        const float WindowBand = 88f;

        static void AddPanelEntries(List<Entry> list)
        {
            void Surface(string name, int w, int h, Vector4 border, Action<HudPainter> draw, int inset = 0) =>
                list.Add(new Entry { Name = name, Width = w, Height = h, Units = w, Border = border, Draw = draw, Inset = inset });

            void Icon(string name, Action<HudPainter> draw) =>
                list.Add(new Entry { Name = name, Width = IconSize, Height = IconSize, Units = 100f, Draw = draw });

            void Glyph(string name, Sdf shape) =>
                list.Add(new Entry { Name = name, Width = GlyphSize, Height = GlyphSize, Units = 100f, Draw = p => DrawGlyph(p, shape) });

            // Unity's border order is left, bottom, right, top.
            Surface(HudSprites.Window, 200, 240, new Vector4(48, 52, 48, 106), DrawWindow);
            Surface(HudSprites.Well, 64, 64, new Vector4(22, 22, 22, 22), DrawWell);
            Surface(HudSprites.Tile, 112, 112, new Vector4(34, 40, 34, 34), DrawTile);
            Surface(HudSprites.Tab, 112, 96, new Vector4(34, 8, 34, 40), DrawTab);
            Surface(HudSprites.ButtonTint, 112, 112, new Vector4(36, 46, 36, 36),
                    p => DrawButton(p, Color.white, new Color(0.87f, 0.87f, 0.87f), new Color(0.60f, 0.60f, 0.60f)));
            Surface(HudSprites.RoundTint, 192, 192, Vector4.zero,
                    p => DrawRound(p, Color.white, new Color(0.87f, 0.87f, 0.87f), new Color(0.60f, 0.60f, 0.60f)));

            // Painted a pixel past the rect on every side, so bilinear filtering at the sprite's edge
            // reads more checker rather than the atlas padding - otherwise a tiled strip shows seams.
            Surface(HudSprites.Checker, 34, 34, Vector4.zero, DrawChecker, inset: 1);
            Surface(HudSprites.Rays, 256, 256, Vector4.zero, DrawRays);

            Icon(HudSprites.IconLock, DrawLock);
            Icon(HudSprites.IconCrate, DrawCrate);
            Icon(HudSprites.IconSeeds, DrawSeedSack);
            Icon(HudSprites.IconSprout, DrawSprout);
            Icon(HudSprites.IconTiles, DrawTiles);
            Icon(HudSprites.IconFrame, DrawPicture);
            Icon(HudSprites.IconBarrel, DrawBarrel);
            Icon(HudSprites.IconHand, DrawHand);

            Glyph(HudSprites.GlyphRotate,
                  Union(Subtract(Edge(Circle(50, 55, 25), 12), Poly(50, 55, 50, 0, 100, 0, 100, 34)),
                        Grow(Poly(45, 16, 66, 30, 45, 44), 3)));
            Glyph(HudSprites.GlyphTrash,
                  Union(Box(20, 22, 80, 31, 3),
                        Subtract(Box(38, 11, 62, 26, 4), Box(43, 16, 57, 30, 1)),
                        Subtract(Grow(Poly(28, 37, 72, 37, 66, 86, 34, 86), 3),
                                 Union(Box(41, 46, 45.5f, 76, 2), Box(54.5f, 46, 59, 76, 2)))));
            Glyph(HudSprites.GlyphMinus, Box(16, 41, 84, 59, 7));
            Glyph(HudSprites.GlyphNext, Union(Capsule(38, 22, 64, 50, 9), Capsule(64, 50, 38, 78, 9)));
            Glyph(HudSprites.GlyphLock,
                  Union(Subtract(Box(31, 12, 69, 52, 19), Box(40.5f, 21.5f, 59.5f, 56, 9.5f)),
                        Subtract(Box(20, 42, 80, 88, 10), Union(Circle(50, 60, 6), Box(47, 60, 53, 75, 2)))));
            Glyph(HudSprites.GlyphSkip, Union(Grow(Poly(16, 27, 45, 50, 16, 73), 5), Grow(Poly(52, 27, 81, 50, 52, 73), 5)));
        }

        // --- Surfaces ----------------------------------------------------------------------------------

        /// <summary>
        /// The window: the warm outline, a teal title band across the top, cream below it fading a
        /// touch darker to the floor, and the deeper lip along the bottom that the cards have.
        /// </summary>
        static void DrawWindow(HudPainter p)
        {
            const float w = 200f, h = 240f, r = 40f;

            p.Fill(Box(2, 2, w - 2, h - 2, r), HudPalette.Outline);
            p.Fill(Box(6, 6, w - 6, h - 6, r - 4), HudPalette.CreamLip);

            Sdf face = Box(6, 6, w - 6, h - 13, r - 4);
            p.Gradient(face, WindowBand, HudPalette.Cream, h - 13, HudPalette.CreamShade);

            Sdf band = Intersect(face, Above(WindowBand));
            p.Gradient(band, 6, new Color(0.40f, 0.76f, 0.70f), WindowBand, HudPalette.Teal);
            p.Fill(Box(6, 14, w - 6, 40, 13), new Color(1f, 1f, 1f, 0.16f), Intersect(band, Box(16, 10, w - 16, 44, 14)));
            p.Fill(Box(6, WindowBand - 4, w - 6, WindowBand + 1), HudPalette.TealDark, face);
            p.Fill(Box(6, WindowBand + 1, w - 6, WindowBand + 5), new Color(0.35f, 0.24f, 0.15f, 0.16f), face);
            p.Fill(Edge(Box(9, 9, w - 9, h - 16, r - 7), 1.6f), new Color(1f, 1f, 1f, 0.32f), Above(WindowBand - 14));
            p.Fill(Edge(Box(9, 9, w - 9, h - 16, r - 7), 1.4f), new Color(1f, 1f, 1f, 0.6f),
                   Intersect(Below(WindowBand + 6), Above(WindowBand + 40)));
        }

        /// <summary>A sunken area: a darker cream with a shadow along its top edge, lit along its bottom.</summary>
        static void DrawWell(HudPainter p)
        {
            p.Fill(Box(1, 1, 63, 63, 20), new Color(0.62f, 0.50f, 0.38f, 0.5f));
            Sdf face = Box(2.5f, 2.5f, 61.5f, 61.5f, 18.5f);
            p.Fill(face, new Color(0.90f, 0.85f, 0.76f));
            p.Fill(face, q => new Color(0.42f, 0.30f, 0.20f, 0.24f * (1f - Mathf.SmoothStep(2.5f, 14f, q.y))));
            p.Fill(Edge(Box(4, 4, 60, 60, 17), 1.2f), new Color(1f, 1f, 1f, 0.55f), Below(46));
        }

        /// <summary>A raised light card: a soft brown edge, a cream lip, a white face lit along its top.</summary>
        static void DrawTile(HudPainter p)
        {
            p.Fill(Box(2, 2, 110, 110, 28), new Color(0.55f, 0.42f, 0.30f));
            p.Fill(Box(4.5f, 4.5f, 107.5f, 107.5f, 25.5f), HudPalette.CreamLip);
            Sdf face = Box(4.5f, 4.5f, 107.5f, 100.5f, 25.5f);
            p.Gradient(face, 4.5f, Color.white, 100.5f, new Color(0.98f, 0.95f, 0.89f));
            p.Fill(Edge(Box(7, 7, 105, 98, 23), 1.4f), new Color(1f, 1f, 1f, 0.85f), Above(30));
        }

        /// <summary>A tab: rounded on top, flat at the bottom where it meets the panel under it.</summary>
        static void DrawTab(HudPainter p)
        {
            Sdf outline = Intersect(Box(2, 2, 110, 140, 28), Above(96));
            p.Fill(outline, HudPalette.Outline);
            Sdf face = Intersect(Box(5.5f, 5.5f, 106.5f, 140, 24.5f), Above(96));
            p.Gradient(face, 5.5f, Color.white, 96, new Color(0.88f, 0.88f, 0.88f));
            p.Fill(Box(14, 10, 98, 34, 12), new Color(1f, 1f, 1f, 0.22f), face);
            p.Fill(Edge(Box(8, 8, 104, 140, 22), 1.6f), new Color(1f, 1f, 1f, 0.4f), Above(34));
        }

        /// <summary>A 2x2 checker in the diner's teal and cream; tiles seamlessly.</summary>
        static void DrawChecker(HudPainter p) => p.Paint(q =>
        {
            int cx = Mathf.FloorToInt((q.x - 1f) / 16f), cy = Mathf.FloorToInt((q.y - 1f) / 16f);
            bool dark = ((cx + cy) & 1) == 0;
            return dark ? new Color(0.20f, 0.47f, 0.45f) : HudPalette.Cream;
        });

        /// <summary>Twelve soft rays fading out from the centre, white so they take a tint.</summary>
        static void DrawRays(HudPainter p) => p.Paint(q =>
        {
            Vector2 d = q - new Vector2(128f, 128f);
            float r = d.magnitude / 126f;
            if (r >= 1f) return Color.clear;

            float angle = Mathf.Atan2(d.y, d.x);
            float ray = Mathf.SmoothStep(0.30f, 0.80f, Mathf.Cos(angle * 12f) * 0.5f + 0.5f);
            float fall = (1f - r) * (1f - r);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(ray * fall * 0.95f + fall * (1f - r) * 0.3f));
        });

        // --- Icons -------------------------------------------------------------------------------------

        static void DrawLock(HudPainter p)
        {
            Sdf shackle = Subtract(Box(29, 10, 71, 58, 21), Box(38.5f, 19.5f, 61.5f, 62, 11.5f));
            Sdf body = Box(17, 41, 83, 88, 11);
            Frame(p, Union(shackle, body));

            p.Shade(shackle, Shading.Of(SteelBody, 4f, 1f, 1.08f, 0.86f, 0.35f), 10, 50);
            p.Fill(Grow(body, 2.4f), HudPalette.Outline);
            p.Shade(body, Shading.Of(GoldBody, 11f, 0.95f), 41, 88);

            Sdf hole = Union(Circle(50, 60, 6.5f), Box(46.5f, 60, 53.5f, 76, 2.5f));
            p.Fill(Grow(hole, 1.4f), new Color(0.62f, 0.40f, 0.10f, 0.6f));
            p.Fill(hole, new Color(0.30f, 0.19f, 0.08f));
            Gloss(p, body, new Vector2(31, 51), new Vector2(9, 4), -15f, 0.4f);
        }

        static void DrawCrate(HudPainter p)
        {
            Sdf crate = Box(13, 20, 87, 87, 7);
            Frame(p, crate);
            p.Shade(crate, Shading.Of(CrateWood, 6f, 0.85f), 20, 87);

            Sdf panel = Box(24, 31, 76, 76, 3);
            p.Fill(Grow(panel, 1.8f), new Color(0.36f, 0.22f, 0.12f, 0.6f));
            p.Shade(panel, Shading.Of(new Color(0.70f, 0.48f, 0.27f), 3f, -0.5f, 0.95f, 1.05f, 0f), 31, 76);

            var seam = new Color(0.36f, 0.22f, 0.12f, 0.45f);
            p.Fill(Box(24, 45.5f, 76, 47), seam, panel);
            p.Fill(Box(24, 60.5f, 76, 62), seam, panel);

            Sdf brace = Intersect(Capsule(23, 77, 77, 30, 5.5f), Grow(panel, -0.5f));
            p.Fill(Grow(brace, 1.6f), new Color(0.36f, 0.22f, 0.12f, 0.6f), panel);
            p.Shade(brace, Shading.Of(CrateWood, 3f, 0.9f), 31, 76);

            var nail = new Color(0.35f, 0.36f, 0.38f);
            foreach (var at in new[] { new Vector2(18.5f, 25.5f), new Vector2(81.5f, 25.5f), new Vector2(18.5f, 81.5f), new Vector2(81.5f, 81.5f) })
                p.Fill(Circle(at, 2.3f), nail);

            Gloss(p, crate, new Vector2(32, 25), new Vector2(13, 3), -6f, 0.3f);
        }

        static void DrawSeedSack(HudPainter p)
        {
            Sdf body = Union(Ellipse(new Vector2(50, 67), new Vector2(31, 23)), Box(25, 38, 75, 68, 13));
            Sdf neck = Box(38, 28, 62, 42, 4);
            Sdf flare = Grow(Poly(36, 30, 64, 30, 73, 13, 27, 13), 3f);
            Frame(p, Union(body, neck, flare));

            p.Shade(flare, Shading.Of(Burlap, 4f, 0.8f), 10, 33);
            p.Shade(Union(body, neck), Shading.Of(Burlap, 10f, 0.85f), 30, 90);

            var weave = new Color(0.62f, 0.48f, 0.28f, 0.35f);
            p.Fill(Capsule(30, 80, 38, 79, 0.9f), weave, body);
            p.Fill(Capsule(64, 79, 71, 76, 0.9f), weave, body);
            p.Fill(Capsule(29, 50, 34, 47, 0.9f), weave, body);

            Sdf rope = Box(34, 29, 66, 36.5f, 3.5f);
            p.Fill(Grow(rope, 1.8f), HudPalette.Outline);
            p.Shade(rope, Shading.Of(new Color(0.66f, 0.42f, 0.22f), 2.5f, 0.9f), 29, 37);

            p.Fill(Capsule(50, 77, 50, 61, 2.2f), StemGreen);
            p.Shade(Ellipse(new Vector2(42, 59), new Vector2(9, 5), 22f), Shading.Of(LeafGreen, 4f, 0.8f), 54, 64);
            p.Shade(Ellipse(new Vector2(58.5f, 56), new Vector2(10, 5.5f), -22f), Shading.Of(LeafGreen, 4f, 0.8f), 50, 62);
        }

        static void DrawSprout(HudPainter p)
        {
            Sdf mound = Intersect(Ellipse(new Vector2(50, 88), new Vector2(37, 18)), Above(88));
            Sdf stem = Capsule(50, 76, 50, 44, 4f);
            Sdf leafL = Ellipse(new Vector2(32, 42), new Vector2(19, 10.5f), 22f);
            Sdf leafR = Ellipse(new Vector2(68, 34), new Vector2(21, 11.5f), -22f);
            Frame(p, Union(mound, stem, leafL, leafR));

            p.Shade(stem, Shading.Of(StemGreen, 3f, 0.8f), 40, 76);
            p.Shade(leafL, Shading.Of(LeafGreen, 8f, 0.9f), 30, 54);
            p.Shade(leafR, Shading.Of(new Color(0.46f, 0.72f, 0.34f), 8f, 0.9f), 22, 46);
            var vein = new Color(0.30f, 0.50f, 0.24f, 0.55f);
            p.Fill(Capsule(46, 47, 20, 37, 0.9f), vein, leafL);
            p.Fill(Capsule(54, 41, 84, 29, 0.9f), vein, leafR);

            p.Fill(Grow(mound, 2.4f), HudPalette.Outline);
            p.Shade(mound, Shading.Of(Soil, 6f, 0.85f), 70, 88);
            p.Fill(Circle(38, 80, 1.6f), new Color(0.40f, 0.26f, 0.15f));
            p.Fill(Circle(62, 82, 1.4f), new Color(0.40f, 0.26f, 0.15f));
        }

        /// <summary>A floor tile seen at an angle: a teal-and-cream checker top with a thick edge.</summary>
        static void DrawTiles(HudPainter p)
        {
            Sdf top = Grow(Poly(50, 16, 89, 41, 50, 66, 11, 41), 2f);
            Sdf side = Grow(Poly(11, 41, 50, 66, 89, 41, 89, 55, 50, 80, 11, 55), 2f);
            Frame(p, Union(top, side));

            p.Fill(side, new Color(0.80f, 0.72f, 0.60f), LeftOf(50));
            p.Fill(side, new Color(0.16f, 0.38f, 0.36f), RightOf(50));

            p.Fill(top, HudPalette.Cream);
            Sdf Diamond(float x, float y) => Poly(x, y - 12.5f, x + 19.5f, y, x, y + 12.5f, x - 19.5f, y);
            p.Fill(Union(Diamond(50, 28.5f), Diamond(50, 53.5f)), HudPalette.Teal, top);
            p.Fill(Union(Capsule(30.5f, 28.5f, 69.5f, 53.5f, 0.7f), Capsule(69.5f, 28.5f, 30.5f, 53.5f, 0.7f)),
                   new Color(0.25f, 0.20f, 0.15f, 0.35f), top);
            Gloss(p, top, new Vector2(40, 30), new Vector2(10, 4), -32f, 0.35f);
        }

        static void DrawPicture(HudPainter p)
        {
            Sdf wire = Union(Capsule(50, 9, 31, 22, 2f), Capsule(50, 9, 69, 22, 2f));
            Sdf frame = Box(13, 20, 87, 85, 6);
            Frame(p, Union(frame, wire, Circle(50, 9, 4f)));

            p.Shade(wire, Shading.Of(SteelBody, 1.5f, 0.6f), 6, 22);
            p.Shade(Circle(50, 9, 3.4f), Shading.Of(GoldBody, 2f, 0.8f), 5, 13);
            p.Shade(frame, Shading.Of(HoneyWood, 6f, 0.95f), 20, 85);

            Sdf art = Box(23, 30, 77, 75, 2);
            p.Fill(Grow(art, 1.8f), new Color(0.36f, 0.22f, 0.12f, 0.6f));
            p.Gradient(art, 30, new Color(0.62f, 0.83f, 0.91f), 75, new Color(0.88f, 0.95f, 0.96f));
            p.Fill(Circle(63, 45, 6.5f), new Color(1f, 0.82f, 0.36f), art);
            p.Fill(Ellipse(new Vector2(37, 80), new Vector2(25, 17)), new Color(0.46f, 0.70f, 0.40f), art);
            p.Fill(Ellipse(new Vector2(67, 82), new Vector2(23, 14)), new Color(0.36f, 0.60f, 0.36f), art);
            p.Fill(Edge(Box(15.5f, 22.5f, 84.5f, 82.5f, 4), 1.2f), new Color(1f, 1f, 1f, 0.3f), Above(40));
        }

        static void DrawBarrel(HudPainter p)
        {
            Sdf barrel = Intersect(Ellipse(new Vector2(50, 51), new Vector2(32, 45)), Box(10, 12, 90, 90));
            Frame(p, barrel);
            p.Shade(barrel, Shading.Of(new Color(0.76f, 0.52f, 0.30f), 13f, 0.9f), 12, 90);

            var stave = new Color(0.36f, 0.22f, 0.12f, 0.4f);
            p.Fill(Capsule(36, 13, 34, 89, 0.8f), stave, barrel);
            p.Fill(Capsule(50, 12, 50, 90, 0.8f), stave, barrel);
            p.Fill(Capsule(64, 13, 66, 89, 0.8f), stave, barrel);

            foreach (float y in new[] { 24f, 70f })
            {
                Sdf hoop = Intersect(Box(0, y, 100, y + 7), Grow(barrel, 0.5f));
                p.Fill(Grow(hoop, 1.2f), HudPalette.Outline, Grow(barrel, 1.5f));
                p.Shade(hoop, Shading.Of(new Color(0.45f, 0.47f, 0.50f), 2.5f, 0.9f), y, y + 7);
            }

            Gloss(p, barrel, new Vector2(34, 42), new Vector2(4, 13), 4f, 0.32f);
        }

        /// <summary>A pointing glove, for "drag this there" hints.</summary>
        static void DrawHand(HudPainter p)
        {
            Sdf finger = Capsule(43, 17, 43, 54, 8f);
            Sdf fist = Box(30, 45, 75, 82, 13);
            Sdf thumb = Capsule(31, 61, 19, 48, 6.5f);
            Sdf cuff = Box(34, 78, 71, 93, 4);
            Sdf glove = Union(finger, fist, thumb);
            Frame(p, Union(glove, cuff));

            p.Shade(glove, Shading.Of(Glove, 8f, 0.7f), 13, 82);
            var crease = new Color(0.47f, 0.39f, 0.32f, 0.55f);
            p.Fill(Capsule(53.5f, 48, 53.5f, 58, 1f), crease);
            p.Fill(Capsule(63, 48, 63, 58, 1f), crease);
            p.Fill(Capsule(29, 62, 37, 66, 1f), crease);

            p.Fill(Grow(cuff, 2.2f), HudPalette.Outline);
            p.Shade(cuff, Shading.Of(HudPalette.Teal, 4f, 0.9f), 78, 93);
        }
    }
}
