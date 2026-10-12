using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastOrders.UI.Hud;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using static LastOrders.EditorTools.Hud.Shape;

namespace LastOrders.EditorTools.Hud
{
    /// <summary>
    /// Bakes every sprite the gameplay HUD draws - cards, chunky buttons, pills, bars, rings,
    /// ribbons and its icon set - into one atlas, and the text materials it uses, then fills the
    /// <see cref="HudSkin"/> the HUD reads them from.
    ///
    /// One atlas so the whole HUD batches into a draw call or two instead of one per image. Drawn
    /// in code like the rest of Last Orders' art: every icon is a handful of distance-field shapes,
    /// lit by <see cref="HudPainter.Shade"/> to sit next to the rendered item icons - the same warm
    /// dark outline, the same soft top-left light, the same soft shadow underneath.
    ///
    /// Re-baking keeps each sprite's id, so a HUD already wired to the atlas keeps its references.
    /// </summary>
    public static class HudArt
    {
        public const string Folder = "Assets/Game Assets/Content/LastOrders/UI/Hud";
        public const string AtlasPath = Folder + "/HudAtlas.png";
        public const string SkinPath = Folder + "/HudSkin.asset";

        const string DisplayFontPath = "Assets/Game Assets/UI/FinalGameUI/CookingPanelUI/junegull SDF.asset";
        const string BodyFontPath = "Assets/Game Assets/UI/FinalGameUI/CookingPanelUI/Montserrat-Regular SDF.asset";

        const int AtlasWidth = 2048;
        const int Padding = 4;

        /// <summary>Icons bake at this many pixels a side.</summary>
        const int IconSize = 160;
        const int GlyphSize = 96;

        /// <summary>Icon outline width and shadow, in the 100-unit icon design space.</summary>
        const float OutlineWidth = 3f;

        static readonly Color OutlineSoft = new Color(0.33f, 0.24f, 0.16f);
        static readonly Color IconShadow = new Color(0.10f, 0.06f, 0.03f, 0.32f);

        static readonly Color GoldBody = new Color(0.98f, 0.76f, 0.27f);
        static readonly Color SteelBody = new Color(0.74f, 0.78f, 0.82f);
        static readonly Color WoodBody = new Color(0.66f, 0.44f, 0.26f);
        static readonly Color WoodEnd = new Color(0.93f, 0.78f, 0.54f);

        sealed class Entry
        {
            public string Name;
            public int Width, Height;
            public float Units;
            public Vector4 Border;

            /// <summary>Pixels trimmed off each side of the painted area for the sprite rect - for
            /// solid fills, so bilinear filtering never reaches the transparent padding.</summary>
            public int Inset;

            public Action<HudPainter> Draw;
            public RectInt Placed;
        }

        [MenuItem("Last Orders/UI/Bake HUD Art", priority = 200)]
        static void BakeFromMenu() => Debug.Log($"[Last Orders] {Bake()}");

        public static string Bake()
        {
            Directory.CreateDirectory(Folder);

            List<Entry> entries = Entries();
            int height = Pack(entries);

            var atlas = new Color[AtlasWidth * height];
            foreach (Entry e in entries)
            {
                var painter = new HudPainter(e.Width, e.Height, e.Units);
                e.Draw(painter);
                painter.Blit(atlas, AtlasWidth, e.Placed.x, e.Placed.y);
            }

            var texture = new Texture2D(AtlasWidth, height, TextureFormat.RGBA32, false);
            texture.SetPixels(atlas);
            texture.Apply();
            File.WriteAllBytes(AtlasPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporter(entries);

            HudSkin skin = WriteSkin();
            return $"HUD art baked: {entries.Count} sprites in {AtlasPath} ({AtlasWidth}x{height}), skin at {SkinPath} " +
                   $"({skin.sprites.Length} sprites, fonts {(skin.display ? skin.display.name : "missing")} / " +
                   $"{(skin.body ? skin.body.name : "missing")}).";
        }

        // --- Packing and import ----------------------------------------------------------------------

        /// <summary>Shelf-packs the entries, tallest first, and returns the atlas height (a power of two).</summary>
        static int Pack(List<Entry> entries)
        {
            int x = Padding, y = Padding, shelf = 0;
            foreach (Entry e in entries.OrderByDescending(e => e.Height).ThenByDescending(e => e.Width))
            {
                if (x + e.Width + Padding > AtlasWidth)
                {
                    x = Padding;
                    y += shelf + Padding;
                    shelf = 0;
                }

                e.Placed = new RectInt(x, y, e.Width, e.Height);
                x += e.Width + Padding;
                shelf = Mathf.Max(shelf, e.Height);
            }

            int used = y + shelf + Padding;
            int height = 256;
            while (height < used) height *= 2;
            return height;
        }

        static void ConfigureImporter(List<Entry> entries)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 2048;
            importer.spritePixelsPerUnit = 100f;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            // Same name, same id: the scene's Image references survive a re-bake.
            Dictionary<string, GUID> ids = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);

            var rects = new List<SpriteRect>();
            foreach (Entry e in entries)
            {
                var r = new Rect(e.Placed.x + e.Inset, e.Placed.y + e.Inset, e.Width - e.Inset * 2, e.Height - e.Inset * 2);
                rects.Add(new SpriteRect
                {
                    name = e.Name,
                    rect = r,
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = e.Border,
                    spriteID = ids.TryGetValue(e.Name, out GUID id) ? id : GUID.Generate(),
                });
            }

            provider.SetSpriteRects(rects.ToArray());

            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            provider.Apply();
            importer.SaveAndReimport();
        }

        static HudSkin WriteSkin()
        {
            var skin = AssetDatabase.LoadAssetAtPath<HudSkin>(SkinPath);
            if (skin == null)
            {
                skin = ScriptableObject.CreateInstance<HudSkin>();
                AssetDatabase.CreateAsset(skin, SkinPath);
            }

            skin.sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            skin.display = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFontPath);
            skin.body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

            if (skin.display != null)
            {
                skin.displayOutline = TextMaterial(skin.display, "HudDisplay_Outline", outline: 0.2f, underlay: 0.55f);
                skin.displayShadow = TextMaterial(skin.display, "HudDisplay_Shadow", outline: 0f, underlay: 0.35f);
            }

            if (skin.body != null)
            {
                skin.bodyShadow = TextMaterial(skin.body, "HudBody_Shadow", outline: 0f, underlay: 0.5f);
            }

            skin.Rebuild();
            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssetIfDirty(skin);
            return skin;
        }

        /// <summary>
        /// A copy of the font's own material with an outline and a soft drop under the letters, so
        /// white numbers stay readable over grass, tiles and tarmac alike. Kept beside the atlas rather
        /// than next to the font, which other screens use as it is.
        /// </summary>
        static Material TextMaterial(TMP_FontAsset font, string name, float outline, float underlay)
        {
            string path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(font.material);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = font.material.shader;
                material.CopyPropertiesFromMaterial(font.material);
            }

            material.name = name;
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
            material.SetColor(ShaderUtilities.ID_OutlineColor, HudPalette.Outline);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, outline > 0f ? outline * 0.6f : 0f);

            if (underlay > 0f)
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                material.SetColor(ShaderUtilities.ID_UnderlayColor, HudPalette.Outline.WithAlpha(underlay));
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.75f);
                material.SetFloat(ShaderUtilities.ID_UnderlayDilate, outline > 0f ? 0.35f : 0.1f);
                material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
            }
            else
            {
                material.DisableKeyword(ShaderUtilities.Keyword_Underlay);
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        // --- What is in the atlas --------------------------------------------------------------------

        static List<Entry> Entries()
        {
            var list = new List<Entry>();

            void Surface(string name, int w, int h, Vector4 border, Action<HudPainter> draw, int inset = 0) =>
                list.Add(new Entry { Name = name, Width = w, Height = h, Units = w, Border = border, Draw = draw, Inset = inset });

            void Icon(string name, Action<HudPainter> draw) =>
                list.Add(new Entry { Name = name, Width = IconSize, Height = IconSize, Units = 100f, Draw = draw });

            void Glyph(string name, Sdf shape) =>
                list.Add(new Entry { Name = name, Width = GlyphSize, Height = GlyphSize, Units = 100f, Draw = p => DrawGlyph(p, shape) });

            // Surfaces: pixel-space, nine-sliced.
            Surface(HudSprites.Card, 112, 112, new Vector4(34, 40, 34, 34), DrawCard);
            Surface(HudSprites.CardShadow, 128, 128, new Vector4(52, 52, 52, 52), DrawShadow);
            Surface(HudSprites.Glass, 72, 72, new Vector4(35, 35, 35, 35), p => DrawGlass(p, 72, 34));
            Surface(HudSprites.GlassCard, 96, 96, new Vector4(30, 30, 30, 30), p => DrawGlass(p, 96, 24));
            Surface(HudSprites.CreamPill, 72, 76, new Vector4(35, 40, 35, 35), DrawCreamPill);

            Surface(HudSprites.ButtonTeal, 112, 112, new Vector4(36, 46, 36, 36),
                    p => DrawButton(p, new Color(0.40f, 0.76f, 0.70f), HudPalette.Teal, HudPalette.TealDark));
            Surface(HudSprites.ButtonRed, 112, 112, new Vector4(36, 46, 36, 36),
                    p => DrawButton(p, new Color(0.89f, 0.45f, 0.37f), HudPalette.Red, HudPalette.RedDark));
            Surface(HudSprites.ButtonGold, 112, 112, new Vector4(36, 46, 36, 36),
                    p => DrawButton(p, new Color(1f, 0.85f, 0.42f), HudPalette.Gold, HudPalette.GoldDark));
            Surface(HudSprites.ButtonCream, 112, 112, new Vector4(36, 46, 36, 36),
                    p => DrawButton(p, new Color(1f, 0.99f, 0.96f), HudPalette.CreamShade, HudPalette.CreamLip));
            Surface(HudSprites.ButtonGrey, 112, 112, new Vector4(36, 46, 36, 36),
                    p => DrawButton(p, new Color(0.80f, 0.78f, 0.74f), HudPalette.Disabled, new Color(0.45f, 0.42f, 0.39f)));

            Surface(HudSprites.RoundCream, 192, 192, Vector4.zero,
                    p => DrawRound(p, new Color(1f, 0.99f, 0.96f), HudPalette.CreamShade, HudPalette.CreamLip));
            Surface(HudSprites.RoundTeal, 192, 192, Vector4.zero,
                    p => DrawRound(p, new Color(0.40f, 0.76f, 0.70f), HudPalette.Teal, HudPalette.TealDark));
            Surface(HudSprites.RoundRed, 192, 192, Vector4.zero,
                    p => DrawRound(p, new Color(0.89f, 0.45f, 0.37f), HudPalette.Red, HudPalette.RedDark));
            Surface(HudSprites.RoundGold, 192, 192, Vector4.zero,
                    p => DrawRound(p, new Color(1f, 0.85f, 0.42f), HudPalette.Gold, HudPalette.GoldDark));
            Surface(HudSprites.RoundGlass, 192, 192, Vector4.zero, DrawRoundGlass);

            Surface(HudSprites.Badge, 64, 64, Vector4.zero, DrawBadge);
            Surface(HudSprites.Disc, 96, 96, Vector4.zero, p => p.Fill(Circle(48, 48, 45), Color.white));
            Surface(HudSprites.DiscShaded, 96, 96, Vector4.zero, DrawShadedDisc);
            Surface(HudSprites.BarTrack, 48, 48, new Vector4(23, 23, 23, 23), DrawBarTrack);
            Surface(HudSprites.BarFill, 48, 48, new Vector4(23, 23, 23, 23), DrawBarFill);
            Surface(HudSprites.Ring, 192, 192, Vector4.zero, p => p.Fill(Edge(Circle(96, 96, 84), 16), Color.white));
            Surface(HudSprites.Glow, 128, 128, Vector4.zero, DrawGlow);
            Surface(HudSprites.Vignette, 256, 256, new Vector4(100, 100, 100, 100), DrawVignette);
            Surface(HudSprites.Ribbon, 320, 112, new Vector4(70, 40, 70, 40), DrawRibbon);
            Surface(HudSprites.Chip, 48, 48, new Vector4(23, 23, 23, 23), DrawChip);
            Surface(HudSprites.Segment, 32, 32, new Vector4(11, 11, 11, 11), p => p.Fill(Box(2, 2, 30, 30, 9), Color.white));
            Surface(HudSprites.White, 16, 16, Vector4.zero, p => p.Fill(Box(0, 0, 16, 16), Color.white), inset: 4);
            Surface(HudSprites.AlertPointer, 160, 160, Vector4.zero, DrawAlertPointer);

            // Icons: the 100-unit design space, outlined and shadowed like the item icons.
            Icon(HudSprites.IconCoin, DrawCoin);
            Icon(HudSprites.IconGem, DrawGem);
            Icon(HudSprites.IconStar, DrawStar);
            Icon(HudSprites.IconWood, DrawWood);
            Icon(HudSprites.IconScrap, DrawScrap);
            Icon(HudSprites.IconKit, DrawToolbox);
            Icon(HudSprites.IconWrench, DrawWrench);
            Icon(HudSprites.IconShield, DrawShield);
            Icon(HudSprites.IconDiner, DrawDiner);
            Icon(HudSprites.IconZombie, DrawZombieHand);
            Icon(HudSprites.IconSun, DrawSun);
            Icon(HudSprites.IconMoon, DrawMoon);
            Icon(HudSprites.IconDawn, DrawDawn);
            Icon(HudSprites.IconClock, DrawClock);
            Icon(HudSprites.IconShop, DrawShop);
            Icon(HudSprites.IconOrders, DrawClipboard);
            Icon(HudSprites.IconGear, DrawGear);
            Icon(HudSprites.IconTrophy, DrawTrophy);
            Icon(HudSprites.IconCook, DrawPan);
            Icon(HudSprites.IconServe, DrawCloche);
            Icon(HudSprites.IconClear, DrawBroom);
            Icon(HudSprites.IconVisitors, DrawPeople);
            Icon(HudSprites.IconMedic, DrawMedic);
            Icon(HudSprites.IconFlag, DrawFlag);
            Icon(HudSprites.IconHammer, DrawHammer);

            // Glyphs: white, for colour discs and buttons.
            Glyph(HudSprites.GlyphPlus, Union(Box(41, 16, 59, 84, 7), Box(16, 41, 84, 59, 7)));
            Glyph(HudSprites.GlyphCheck, Union(Capsule(20, 52, 41, 73, 9), Capsule(41, 73, 80, 30, 9)));
            Glyph(HudSprites.GlyphCross, Union(Capsule(26, 26, 74, 74, 9), Capsule(74, 26, 26, 74, 9)));
            Glyph(HudSprites.GlyphPlay, Grow(Poly(32, 22, 78, 50, 32, 78), 7));
            Glyph(HudSprites.GlyphInfo, Union(Circle(50, 22, 9), Box(41, 38, 59, 84, 7)));
            Glyph(HudSprites.GlyphAlert, Union(Box(41, 12, 59, 62, 8), Circle(50, 79, 9.5f)));
            Glyph(HudSprites.GlyphUp, Grow(Poly(50, 22, 78, 62, 22, 62), 6));
            Glyph(HudSprites.GlyphDown, Grow(Poly(22, 38, 78, 38, 50, 78), 6));

            return list;
        }

        // --- Surfaces ----------------------------------------------------------------------------------

        /// <summary>The cream card: a warm outline, a lip along the bottom, a lit top edge.</summary>
        static void DrawCard(HudPainter p)
        {
            p.Fill(Box(2, 2, 110, 110, 30), OutlineSoft);
            p.Fill(Box(5, 5, 107, 107, 27), HudPalette.CreamLip);
            p.Gradient(Box(5, 5, 107, 101, 27), 5, HudPalette.Cream, 101, HudPalette.CreamShade);
            p.Fill(Edge(Box(7.5f, 7.5f, 104.5f, 98.5f, 24.5f), 1.6f), new Color(1f, 1f, 1f, 0.75f), Above(26));
        }

        static void DrawShadow(HudPainter p) =>
            p.Shadow(Box(30, 30, 98, 98, 26), new Color(0.12f, 0.08f, 0.05f, 0.42f), 15f, Vector2.zero);

        static void DrawGlass(HudPainter p, int size, float radius)
        {
            p.Fill(Box(2, 2, size - 2, size - 2, radius + 2), new Color(0.08f, 0.06f, 0.05f, 0.55f));
            p.Fill(Box(4, 4, size - 4, size - 4, radius), HudPalette.Glass);
            p.Fill(Edge(Box(6, 6, size - 6, size - 6, radius - 2), 1.4f), new Color(1f, 1f, 1f, 0.16f), Above(size * 0.36f));
        }

        static void DrawCreamPill(HudPainter p)
        {
            p.Fill(Box(2, 2, 70, 74, 35), OutlineSoft);
            p.Fill(Box(5, 5, 67, 71, 32), HudPalette.CreamLip);
            p.Gradient(Box(5, 5, 67, 66, 31), 5, HudPalette.Cream, 66, HudPalette.CreamShade);
            p.Fill(Edge(Box(7.5f, 7.5f, 64.5f, 63.5f, 28.5f), 1.4f), new Color(1f, 1f, 1f, 0.7f), Above(22));
        }

        /// <summary>A pressable button: dark outline, deep lip, lighter top, a soft gloss band.</summary>
        static void DrawButton(HudPainter p, Color light, Color body, Color lip)
        {
            p.Fill(Box(2, 2, 110, 110, 31), HudPalette.Outline);
            p.Fill(Box(5.5f, 5.5f, 106.5f, 106.5f, 27.5f), lip);
            Sdf face = Box(5.5f, 5.5f, 106.5f, 97f, 27.5f);
            p.Gradient(face, 5.5f, light, 97f, body);
            p.Fill(Box(14, 10, 98, 42, 15), new Color(1f, 1f, 1f, 0.2f), face);
            p.Fill(Edge(Box(8, 8, 104, 94.5f, 25), 1.6f), new Color(1f, 1f, 1f, 0.35f), Above(30));
        }

        static void DrawRound(HudPainter p, Color light, Color body, Color lip)
        {
            Sdf top = Circle(96, 88, 82), bottom = Circle(96, 100, 82);
            p.Shadow(Union(top, bottom), new Color(0.10f, 0.06f, 0.03f, 0.3f), 5f, new Vector2(0f, 3f));
            p.Fill(Grow(Union(top, bottom), 4f), HudPalette.Outline);
            p.Fill(bottom, lip);
            p.Gradient(top, 6, light, 170, body);
            p.Fill(Ellipse(new Vector2(96, 46), new Vector2(54, 24)), new Color(1f, 1f, 1f, 0.24f), top);
            p.Fill(Edge(Grow(top, -3f), 2f), new Color(1f, 1f, 1f, 0.3f), Above(70));
        }

        static void DrawRoundGlass(HudPainter p)
        {
            Sdf disc = Circle(96, 96, 84);
            p.Fill(Grow(disc, 4f), new Color(0.08f, 0.06f, 0.05f, 0.6f));
            p.Fill(disc, HudPalette.Glass);
            p.Fill(Edge(Grow(disc, -4f), 2.4f), new Color(1f, 1f, 1f, 0.15f), Above(80));
        }

        static void DrawBadge(HudPainter p)
        {
            p.Fill(Circle(32, 32, 30), HudPalette.Outline);
            p.Fill(Circle(32, 32, 27.5f), Color.white);
            p.Shade(Circle(32, 32, 23), Shading.Of(new Color(0.88f, 0.30f, 0.24f), 8f, 0.8f), 9, 55);
        }

        static void DrawShadedDisc(HudPainter p)
        {
            p.Fill(Circle(48, 48, 46), new Color(0.6f, 0.6f, 0.6f));
            p.Shade(Circle(48, 48, 42), Shading.Of(new Color(0.9f, 0.9f, 0.9f), 14f, 0.7f, 1.1f, 0.92f, 0.12f), 6, 90);
        }

        static void DrawBarTrack(HudPainter p)
        {
            Sdf track = Box(1, 1, 47, 47, 23);
            p.Fill(track, new Color(0.09f, 0.07f, 0.06f, 0.6f));
            p.Fill(track, new Color(0f, 0f, 0f, 0.2f), Above(12));
            p.Fill(Edge(Box(1.8f, 1.8f, 46.2f, 46.2f, 22.2f), 1.4f), new Color(1f, 1f, 1f, 0.16f), Below(34));
        }

        static void DrawBarFill(HudPainter p)
        {
            p.Fill(Box(3, 3, 45, 45, 21), new Color(0.78f, 0.78f, 0.78f));
            p.Fill(Box(3, 3, 45, 39, 21), new Color(0.94f, 0.94f, 0.94f));
            p.Fill(Box(9, 7, 39, 17, 5), new Color(1f, 1f, 1f, 0.6f));
        }

        static void DrawGlow(HudPainter p) => p.Paint(q =>
        {
            float t = Mathf.Clamp01((q - new Vector2(64, 64)).magnitude / 62f);
            float a = (1f - t) * (1f - t);
            return new Color(1f, 1f, 1f, a);
        });

        /// <summary>An edge vignette: clear in the middle, rising towards every edge.</summary>
        static void DrawVignette(HudPainter p) => p.Paint(q =>
        {
            float dx = Mathf.Max(0f, Mathf.Abs(q.x - 128f) - 28f) / 100f;
            float dy = Mathf.Max(0f, Mathf.Abs(q.y - 128f) - 28f) / 100f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float t = Mathf.Clamp01((r - 0.25f) / 0.75f);
            return new Color(1f, 1f, 1f, Mathf.Pow(t * t * (3f - 2f * t), 1.4f));
        });

        static void DrawRibbon(HudPainter p)
        {
            Sdf band = Box(44, 10, 276, 82, 12);
            Sdf left = Poly(8, 32, 62, 32, 62, 100, 8, 100, 26, 66);
            Sdf right = Poly(258, 32, 312, 32, 294, 66, 312, 100, 258, 100);

            p.Shadow(Union(band, left, right), new Color(0.10f, 0.06f, 0.03f, 0.3f), 4f, new Vector2(0f, 3f));
            p.Fill(Grow(Union(band, left, right), 3f), HudPalette.Outline);
            p.Gradient(left, 32, new Color(0.78f, 0.78f, 0.78f), 100, new Color(0.62f, 0.62f, 0.62f));
            p.Gradient(right, 32, new Color(0.78f, 0.78f, 0.78f), 100, new Color(0.62f, 0.62f, 0.62f));
            p.Fill(Poly(44, 82, 62, 82, 62, 100), new Color(0.42f, 0.42f, 0.42f));
            p.Fill(Poly(276, 82, 258, 82, 258, 100), new Color(0.42f, 0.42f, 0.42f));
            p.Fill(Grow(band, 3f), HudPalette.Outline);
            p.Gradient(band, 10, Color.white, 82, new Color(0.84f, 0.84f, 0.84f));
            p.Fill(Box(56, 16, 264, 40, 10), new Color(1f, 1f, 1f, 0.28f), band);
        }

        /// <summary>
        /// The beak an approach alert points with: a rounded wedge from under its badge out past the
        /// rim, tip up, white so it takes the alert's colour. Square and centred on the badge, so
        /// turning the image swings the beak round the badge. The shadow has no offset, because the
        /// beak turns and a cast shadow would turn with it.
        /// </summary>
        static void DrawAlertPointer(HudPainter p)
        {
            Sdf beak = Grow(Poly(80, 16, 100, 60, 60, 60), 6f);
            p.Shadow(Grow(beak, 4f), new Color(0.10f, 0.06f, 0.03f, 0.32f), 4f, Vector2.zero);
            p.Fill(Grow(beak, 4f), HudPalette.Outline);
            p.Gradient(beak, 10, Color.white, 66, new Color(0.80f, 0.80f, 0.80f));
            p.Fill(Edge(Grow(beak, -3f), 1.6f), new Color(1f, 1f, 1f, 0.5f), Above(40));
        }

        static void DrawChip(HudPainter p)
        {
            p.Fill(Box(1, 1, 47, 47, 23), new Color(0.12f, 0.08f, 0.05f, 0.45f));
            p.Gradient(Box(3, 3, 45, 45, 21), 3, Color.white, 45, new Color(0.86f, 0.86f, 0.86f));
            p.Fill(Box(9, 6, 39, 16, 5), new Color(1f, 1f, 1f, 0.35f));
        }

        // --- Icons -------------------------------------------------------------------------------------

        /// <summary>The frame every icon shares: a soft shadow and the warm outline round its silhouette.</summary>
        static void Frame(HudPainter p, Sdf silhouette)
        {
            Sdf outline = Grow(silhouette, OutlineWidth);
            p.Shadow(outline, IconShadow, 2.6f, new Vector2(0.8f, 2.6f));
            p.Fill(outline, HudPalette.Outline);
        }

        static void Gloss(HudPainter p, Sdf clip, Vector2 at, Vector2 size, float degrees = -30f, float alpha = 0.42f) =>
            p.Fill(Ellipse(at, size, degrees), new Color(1f, 1f, 1f, alpha), clip);

        static void DrawGlyph(HudPainter p, Sdf shape)
        {
            p.Shadow(shape, new Color(0.10f, 0.06f, 0.03f, 0.38f), 3f, new Vector2(0f, 4f));
            p.Fill(shape, Color.white);
        }

        static void DrawCoin(HudPainter p)
        {
            Sdf coin = Circle(50, 50, 38);
            Frame(p, coin);
            p.Shade(coin, Shading.Of(GoldBody, 12f, 0.95f), 12, 88);
            p.Fill(Edge(Circle(50, 50, 29.5f), 2.4f), new Color(0.72f, 0.48f, 0.12f, 0.85f));
            p.Shade(Circle(50, 50, 28.2f), Shading.Of(new Color(0.95f, 0.70f, 0.22f), 6f, -0.6f, 0.96f, 1.06f, 0f), 22, 78);
            p.Shade(Grow(Star(new Vector2(50, 52), 14.5f, 6.6f), 1.8f), Shading.Of(new Color(1f, 0.86f, 0.40f), 4f, 0.9f), 36, 66);
            Gloss(p, coin, new Vector2(36, 30), new Vector2(13, 6.5f), -35f);
        }

        static void DrawGem(HudPainter p)
        {
            Sdf gem = Poly(16, 40, 32, 20, 68, 20, 84, 40, 50, 88);
            Frame(p, Grow(gem, 1.5f));

            p.Fill(Grow(gem, 1.5f), new Color(0.78f, 0.26f, 0.39f));
            void Facet(Sdf f, Color top, Color bottom, float y0, float y1) => p.Gradient(f, y0, top, y1, bottom);

            Facet(Poly(16, 40, 32, 20, 40, 40), new Color(0.98f, 0.52f, 0.60f), new Color(0.92f, 0.43f, 0.53f), 20, 40);
            Facet(Poly(32, 20, 68, 20, 60, 40, 40, 40), new Color(1f, 0.74f, 0.79f), new Color(0.98f, 0.60f, 0.67f), 20, 40);
            Facet(Poly(68, 20, 84, 40, 60, 40), new Color(0.86f, 0.34f, 0.46f), new Color(0.78f, 0.28f, 0.40f), 20, 40);
            Facet(Poly(16, 40, 40, 40, 50, 88), new Color(0.92f, 0.40f, 0.52f), new Color(0.80f, 0.28f, 0.41f), 40, 88);
            Facet(Poly(40, 40, 60, 40, 50, 88), new Color(0.84f, 0.30f, 0.43f), new Color(0.70f, 0.21f, 0.34f), 40, 88);
            Facet(Poly(60, 40, 84, 40, 50, 88), new Color(0.66f, 0.19f, 0.31f), new Color(0.54f, 0.13f, 0.25f), 40, 88);

            var line = new Color(1f, 0.86f, 0.89f, 0.55f);
            p.Fill(Capsule(17, 40, 83, 40, 0.7f), line, gem);
            p.Fill(Capsule(40, 40, 50, 87, 0.6f), line, gem);
            p.Fill(Capsule(60, 40, 50, 87, 0.6f), line, gem);
            p.Fill(Capsule(32, 21, 40, 40, 0.6f), line, gem);
            p.Fill(Capsule(68, 21, 60, 40, 0.6f), line, gem);

            p.Fill(Star(new Vector2(36, 30), 8f, 1.8f, 4), Color.white);
            p.Fill(Circle(70, 54, 2.2f), new Color(1f, 1f, 1f, 0.8f));
        }

        static void DrawStar(HudPainter p)
        {
            Sdf star = Grow(Star(new Vector2(50, 54), 38f, 17.5f), 5f);
            Frame(p, star);
            p.Shade(star, Shading.Of(GoldBody, 14f, 1f, 1.1f, 0.88f, 0.3f), 10, 92);
            Gloss(p, star, new Vector2(40, 34), new Vector2(10, 5), -30f, 0.4f);
        }

        static void DrawWood(HudPainter p)
        {
            Sdf lowBody = Capsule(20, 66, 66, 66, 14);
            Sdf lowEnd = Ellipse(new Vector2(66, 66), new Vector2(10, 14));
            Sdf upBody = Capsule(34, 40, 74, 40, 14);
            Sdf upEnd = Ellipse(new Vector2(74, 40), new Vector2(10, 14));

            Frame(p, Union(lowBody, lowEnd, upBody, upEnd));

            void Log(Sdf body, Sdf end, Vector2 c, float x0, float x1)
            {
                p.Shade(body, Shading.Of(WoodBody, 9f, 0.9f), c.y - 14, c.y + 14);
                var bark = new Color(0.36f, 0.22f, 0.12f, 0.45f);
                p.Fill(Capsule(x0, c.y - 5, x1, c.y - 5, 0.9f), bark, body);
                p.Fill(Capsule(x0 + 6, c.y + 5, x1 - 8, c.y + 5, 0.9f), bark, body);
                p.Shade(end, Shading.Of(WoodEnd, 4f, 0.6f), c.y - 14, c.y + 14);
                var ring = new Color(0.70f, 0.50f, 0.28f, 0.85f);
                p.Fill(Edge(Ellipse(c, new Vector2(6.2f, 8.8f)), 1.4f), ring);
                p.Fill(Edge(Ellipse(c, new Vector2(2.6f, 3.6f)), 1.3f), ring);
            }

            Log(lowBody, lowEnd, new Vector2(66, 66), 14, 58);
            p.Fill(Grow(Union(upBody, upEnd), OutlineWidth), HudPalette.Outline);
            Log(upBody, upEnd, new Vector2(74, 40), 28, 66);
        }

        static void DrawScrap(HudPainter p)
        {
            Sdf nut = Subtract(Grow(Regular(new Vector2(50, 50), 33f, 6, 30f), 4f), Circle(50, 50, 13f));
            Frame(p, nut);
            p.Shade(nut, Shading.Of(SteelBody, 10f, 1f, 1.08f, 0.86f, 0.35f), 12, 88);
            p.Fill(Edge(Circle(50, 50, 26f), 1.6f), new Color(1f, 1f, 1f, 0.3f), nut);
            p.Fill(Edge(Circle(50, 50, 14.5f), 3f), new Color(0.30f, 0.32f, 0.35f, 0.7f), nut);
            Gloss(p, nut, new Vector2(34, 30), new Vector2(10, 5), -30f, 0.35f);
        }

        static void DrawToolbox(HudPainter p)
        {
            Sdf handle = Subtract(Box(32, 16, 68, 42, 9), Box(39, 23, 61, 44, 3));
            Sdf body = Box(12, 34, 88, 86, 10);
            Frame(p, Union(handle, body));

            p.Shade(handle, Shading.Of(SteelBody, 4f, 0.9f), 16, 42);
            p.Shade(body, Shading.Of(HudPalette.Red, 10f, 0.85f), 34, 86);
            p.Fill(Box(12, 49, 88, 52.5f), new Color(0.30f, 0.10f, 0.07f, 0.5f), body);
            p.Fill(Box(12, 52.5f, 88, 54), new Color(1f, 1f, 1f, 0.18f), body);
            Sdf latch = Box(43, 44, 57, 60, 3);
            p.Fill(Grow(latch, 2f), HudPalette.Outline);
            p.Shade(latch, Shading.Of(new Color(0.95f, 0.80f, 0.40f), 3f, 0.9f), 44, 60);
            Gloss(p, body, new Vector2(30, 42), new Vector2(12, 4), -10f, 0.3f);
        }

        static void DrawWrench(HudPainter p)
        {
            Sdf head = Subtract(Circle(66, 34, 18), Capsule(66, 34, 88, 12, 7.5f));
            Sdf handle = Capsule(60, 40, 29, 71, 8);
            Sdf tail = Subtract(Circle(26, 74, 11.5f), Circle(26, 74, 4.6f));
            Sdf wrench = Union(head, handle, tail);
            Frame(p, wrench);
            p.Shade(wrench, Shading.Of(SteelBody, 7f, 1f, 1.08f, 0.88f, 0.35f), 14, 86);
            Gloss(p, wrench, new Vector2(58, 26), new Vector2(7, 3.5f), -40f, 0.4f);
        }

        static readonly Vector2[] ShieldOutline =
        {
            new Vector2(50, 9), new Vector2(66, 15), new Vector2(84, 19), new Vector2(84, 44), new Vector2(79, 61),
            new Vector2(67, 76), new Vector2(50, 90), new Vector2(33, 76), new Vector2(21, 61), new Vector2(16, 44),
            new Vector2(16, 19), new Vector2(34, 15),
        };

        static void DrawShield(HudPainter p)
        {
            Sdf shield = Grow(Polygon(ShieldOutline), 2f);
            Frame(p, shield);
            p.Shade(shield, Shading.Of(new Color(0.34f, 0.70f, 0.65f), 11f, 0.9f), 8, 92, LeftOf(50));
            p.Shade(shield, Shading.Of(new Color(0.24f, 0.56f, 0.53f), 11f, 0.9f), 8, 92, RightOf(50));
            p.Fill(Edge(Grow(Polygon(ShieldOutline), -4.5f), 1.8f), new Color(0.82f, 0.97f, 0.92f, 0.5f));
            Gloss(p, shield, new Vector2(33, 26), new Vector2(10, 5), -25f, 0.32f);
        }

        static void DrawDiner(HudPainter p)
        {
            Sdf sign = Box(30, 12, 70, 30, 6);
            Sdf awning = Box(13, 30, 87, 46, 3);
            var scallops = new List<Sdf>();
            for (int i = 0; i < 8; i++) scallops.Add(Circle(17.6f + i * 9.25f, 46, 4.7f));
            Sdf scallop = Union(scallops.ToArray());
            Sdf body = Box(19, 44, 81, 88, 4);
            Frame(p, Union(sign, awning, scallop, body));

            p.Shade(body, Shading.Of(HudPalette.Cream, 6f, 0.6f), 44, 88);
            p.Fill(Box(19, 78, 81, 88, 4), HudPalette.Teal, body);
            p.Shade(Box(42, 58, 58, 88, 2.5f), Shading.Of(HudPalette.TealDark, 3f, 0.8f), 58, 88);
            Color glass = new Color(0.70f, 0.85f, 0.90f);
            p.Shade(Box(24, 55, 38, 70, 2.5f), Shading.Of(glass, 3f, 0.8f), 55, 70);
            p.Shade(Box(62, 55, 76, 70, 2.5f), Shading.Of(glass, 3f, 0.8f), 55, 70);
            p.Fill(Circle(55, 74, 1.4f), new Color(0.95f, 0.85f, 0.5f));

            p.Fill(Union(awning, scallop), HudPalette.Cream);
            for (int i = 0; i < 8; i += 2)
            {
                float x = 13 + i * 9.25f;
                p.Fill(Union(Box(x, 30, x + 9.25f, 46), Circle(17.6f + i * 9.25f, 46, 4.7f)), HudPalette.Red, Union(awning, scallop));
            }

            p.Fill(Union(awning, scallop), new Color(0f, 0f, 0f, 0.12f), Below(41));
            p.Shade(sign, Shading.Of(HudPalette.Teal, 5f, 0.9f), 12, 30);
            p.Fill(Box(36, 19, 64, 23, 2), new Color(0.98f, 0.95f, 0.86f));
        }

        static void DrawZombieHand(HudPainter p)
        {
            var skin = new Color(0.56f, 0.67f, 0.46f);
            Sdf palm = Box(new Vector2(51, 58), new Vector2(16, 15), 8, 0);
            Sdf index = Capsule(41, 50, 37, 20, 6.4f);
            Sdf middle = Capsule(51, 48, 51, 13, 6.6f);
            Sdf ring = Capsule(61, 50, 65, 20, 6.2f);
            Sdf pinky = Capsule(67, 56, 77, 34, 5.4f);
            Sdf thumb = Capsule(37, 62, 22, 47, 6.4f);
            Sdf hand = Union(palm, index, middle, ring, pinky, thumb);
            Sdf sleeve = Poly(30, 72, 36, 67, 42, 74, 49, 66, 55, 73, 61, 66, 67, 72, 72, 69, 74, 92, 28, 92);
            Frame(p, Union(hand, sleeve));

            p.Shade(hand, Shading.Of(skin, 7f, 0.9f), 12, 74);
            var crease = new Color(0.25f, 0.33f, 0.20f, 0.55f);
            p.Fill(Capsule(44, 60, 57, 55, 0.9f), crease);
            p.Fill(Capsule(47, 54, 47, 61, 0.7f), crease);
            p.Fill(Capsule(51, 53, 51, 60, 0.7f), crease);
            p.Fill(Capsule(55, 52, 55, 59, 0.7f), crease);
            p.Fill(Circle(51, 17, 2.8f), new Color(0.40f, 0.48f, 0.34f));
            p.Fill(Circle(37.6f, 24, 2.6f), new Color(0.40f, 0.48f, 0.34f));
            p.Fill(Circle(64.5f, 24, 2.5f), new Color(0.40f, 0.48f, 0.34f));

            p.Fill(Grow(sleeve, 2.4f), HudPalette.Outline);
            p.Shade(sleeve, Shading.Of(new Color(0.46f, 0.37f, 0.30f), 6f, 0.8f), 66, 92);
            p.Fill(Capsule(36, 80, 66, 80, 0.9f), new Color(0.25f, 0.18f, 0.12f, 0.5f), sleeve);
        }

        static void DrawSun(HudPainter p)
        {
            var rays = new List<Sdf>();
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                rays.Add(Capsule(new Vector2(50, 50) + dir * 27f, new Vector2(50, 50) + dir * 36f, 4.6f));
            }

            Sdf ray = Union(rays.ToArray());
            Sdf core = Circle(50, 50, 19);
            Frame(p, Union(ray, core));
            p.Shade(ray, Shading.Of(new Color(0.99f, 0.70f, 0.26f), 4f, 0.8f), 10, 90);
            p.Fill(Grow(core, 2.6f), HudPalette.Outline);
            p.Shade(core, Shading.Of(new Color(1f, 0.83f, 0.33f), 9f, 0.9f), 31, 69);
            Gloss(p, core, new Vector2(43, 42), new Vector2(6, 3.5f), -30f, 0.5f);
        }

        static void DrawMoon(HudPainter p)
        {
            Sdf moon = Subtract(Circle(46, 52, 34), Circle(63, 39, 27));
            Sdf starA = Grow(Star(new Vector2(74, 66), 8f, 3.2f, 4), 0.8f);
            Sdf starB = Grow(Star(new Vector2(82, 34), 5.5f, 2.2f, 4), 0.6f);
            Frame(p, Union(moon, starA, starB));
            p.Shade(moon, Shading.Of(new Color(0.90f, 0.92f, 0.98f), 10f, 0.9f, 1.06f, 0.86f), 18, 86);
            var crater = new Color(0.55f, 0.60f, 0.74f, 0.35f);
            p.Fill(Circle(30, 60, 4.5f), crater, moon);
            p.Fill(Circle(42, 74, 3.2f), crater, moon);
            p.Fill(Circle(24, 44, 2.6f), crater, moon);
            p.Shade(starA, Shading.Of(new Color(1f, 0.85f, 0.40f), 2f, 0.6f), 58, 74);
            p.Shade(starB, Shading.Of(new Color(1f, 0.85f, 0.40f), 2f, 0.6f), 28, 40);
        }

        static void DrawDawn(HudPainter p)
        {
            var rays = new List<Sdf>();
            foreach (float deg in new[] { -60f, -30f, 0f, 30f, 60f })
            {
                float a = deg * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                rays.Add(Capsule(new Vector2(50, 62) + dir * 29f, new Vector2(50, 62) + dir * 39f, 4.4f));
            }

            Sdf ray = Union(rays.ToArray());
            Sdf sun = Intersect(Circle(50, 62, 22), Above(64));
            Sdf ground = Box(10, 62, 90, 76, 7);
            Frame(p, Union(ray, sun, ground));
            p.Shade(ray, Shading.Of(new Color(0.99f, 0.70f, 0.26f), 4f, 0.8f), 20, 62);
            p.Shade(sun, Shading.Of(new Color(1f, 0.82f, 0.33f), 8f, 0.9f), 40, 64);
            p.Fill(Grow(ground, 2.4f), HudPalette.Outline);
            p.Shade(ground, Shading.Of(new Color(0.42f, 0.64f, 0.56f), 5f, 0.8f), 62, 76);
        }

        static void DrawClock(HudPainter p)
        {
            Sdf rim = Circle(50, 50, 38);
            Frame(p, rim);
            p.Shade(rim, Shading.Of(HudPalette.Teal, 8f, 0.9f), 12, 88);
            Sdf face = Circle(50, 50, 29);
            p.Fill(Grow(face, 1.6f), new Color(0.12f, 0.30f, 0.28f, 0.6f));
            p.Shade(face, Shading.Of(HudPalette.Cream, 6f, -0.4f, 0.98f, 1.04f, 0f), 21, 79);
            var tick = HudPalette.InkSoft;
            p.Fill(Capsule(50, 25, 50, 29, 1.6f), tick);
            p.Fill(Capsule(75, 50, 71, 50, 1.6f), tick);
            p.Fill(Capsule(50, 75, 50, 71, 1.6f), tick);
            p.Fill(Capsule(25, 50, 29, 50, 1.6f), tick);
            p.Fill(Capsule(50, 50, 50, 31, 3f), HudPalette.Ink);
            p.Fill(Capsule(50, 50, 63, 58, 3f), HudPalette.Ink);
            p.Fill(Circle(50, 50, 4.2f), HudPalette.Red);
            Gloss(p, rim, new Vector2(34, 26), new Vector2(9, 4f), -35f, 0.35f);
        }

        static void DrawShop(HudPainter p)
        {
            Sdf roof = Box(9, 14, 91, 27, 6);
            Sdf awning = Box(13, 25, 87, 43, 2);
            var scallops = new List<Sdf>();
            for (int i = 0; i < 6; i++) scallops.Add(Circle(19.2f + i * 12.33f, 43, 6.2f));
            Sdf scallop = Union(scallops.ToArray());
            Sdf posts = Union(Box(17, 44, 24, 80, 2), Box(76, 44, 83, 80, 2));
            Sdf produce = Union(Circle(32, 60, 7.5f), Circle(49, 58.5f, 8f), Circle(66, 60, 7.5f));
            Sdf counter = Box(9, 62, 91, 88, 6);
            Frame(p, Union(roof, awning, scallop, posts, produce, counter));

            p.Shade(posts, Shading.Of(WoodBody, 3f, 0.8f), 44, 80);
            p.Fill(Grow(produce, 2.2f), HudPalette.Outline);
            p.Shade(Circle(32, 60, 7.5f), Shading.Of(HudPalette.Red, 5f, 0.9f), 52, 68);
            p.Shade(Circle(49, 58.5f, 8f), Shading.Of(new Color(0.52f, 0.72f, 0.36f), 5f, 0.9f), 50, 67);
            p.Shade(Circle(66, 60, 7.5f), Shading.Of(GoldBody, 5f, 0.9f), 52, 68);

            p.Fill(Grow(counter, 2.4f), HudPalette.Outline);
            p.Shade(counter, Shading.Of(new Color(0.72f, 0.49f, 0.29f), 6f, 0.85f), 62, 88);
            p.Fill(Box(9, 74, 91, 76.2f), new Color(0.30f, 0.18f, 0.10f, 0.4f), counter);

            Sdf canopy = Union(awning, scallop);
            p.Fill(canopy, HudPalette.Cream);
            for (int i = 0; i < 6; i += 2)
            {
                float x = 13 + i * 12.33f;
                p.Fill(Union(Box(x, 25, x + 12.33f, 43), Circle(19.2f + i * 12.33f, 43, 6.2f)), HudPalette.Teal, canopy);
            }

            p.Fill(canopy, new Color(0f, 0f, 0f, 0.12f), Below(38));
            p.Fill(Grow(roof, 2.4f), HudPalette.Outline);
            p.Shade(roof, Shading.Of(HudPalette.Red, 5f, 0.9f), 14, 27);
        }

        static void DrawClipboard(HudPainter p)
        {
            Sdf board = Box(19, 15, 81, 90, 9);
            Sdf clip = Box(35, 8, 65, 25, 6);
            Frame(p, Union(board, clip));
            p.Shade(board, Shading.Of(new Color(0.66f, 0.45f, 0.27f), 7f, 0.85f), 15, 90);
            Sdf paper = Box(26, 24, 74, 83, 3);
            p.Fill(Grow(paper, 1.4f), new Color(0.30f, 0.18f, 0.10f, 0.45f));
            p.Gradient(paper, 24, new Color(1f, 0.99f, 0.96f), 83, new Color(0.94f, 0.91f, 0.85f));

            var line = new Color(0.58f, 0.62f, 0.68f);
            for (int i = 0; i < 3; i++)
            {
                float y = 44 + i * 13;
                p.Fill(Capsule(43, y, 66, y, 2.2f), line);
                p.Fill(Union(Capsule(31, y - 1, 34, y + 2.5f, 1.9f), Capsule(34, y + 2.5f, 39, y - 4, 1.9f)), HudPalette.Teal);
            }

            p.Fill(Grow(clip, 2.2f), HudPalette.Outline);
            p.Shade(clip, Shading.Of(SteelBody, 4f, 1f), 8, 25);
            p.Fill(Circle(50, 13, 2.6f), new Color(0.32f, 0.35f, 0.38f));
        }

        static void DrawGear(HudPainter p)
        {
            var parts = new List<Sdf> { Circle(50, 50, 27) };
            for (int i = 0; i < 8; i++)
            {
                float deg = i * 45f;
                float a = deg * Mathf.Deg2Rad;
                var c = new Vector2(50, 50) + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * 31f;
                parts.Add(Box(c, new Vector2(6.5f, 7.5f), 2.5f, deg));
            }

            Sdf gear = Subtract(Union(parts.ToArray()), Circle(50, 50, 11));
            Frame(p, gear);
            p.Shade(gear, Shading.Of(SteelBody, 7f, 1f, 1.08f, 0.86f, 0.3f), 10, 90);
            p.Fill(Edge(Circle(50, 50, 19), 1.4f), new Color(1f, 1f, 1f, 0.28f), gear);
        }

        static void DrawTrophy(HudPainter p)
        {
            Sdf cup = Grow(Poly(28, 16, 72, 16, 71, 33, 64, 46, 56, 52, 44, 52, 36, 46, 29, 33), 2.5f);
            Sdf handles = Union(Intersect(Edge(Circle(26, 30, 10), 4.4f), LeftOf(31)),
                                Intersect(Edge(Circle(74, 30, 10), 4.4f), RightOf(69)));
            Sdf stem = Box(45, 52, 55, 68, 1.5f);
            Sdf foot = Box(32, 66, 68, 78, 4);
            Sdf plinth = Box(26, 76, 74, 90, 4);
            Frame(p, Union(cup, handles, stem, foot, plinth));

            p.Shade(handles, Shading.Of(GoldBody, 3f, 0.8f), 18, 42);
            p.Shade(stem, Shading.Of(new Color(0.90f, 0.66f, 0.22f), 3f, 0.8f), 52, 68);
            p.Shade(cup, Shading.Of(GoldBody, 11f, 1f, 1.1f, 0.88f, 0.35f), 14, 55);
            p.Shade(foot, Shading.Of(GoldBody, 4f, 0.9f), 66, 78);
            p.Fill(Grow(plinth, 2.4f), HudPalette.Outline);
            p.Shade(plinth, Shading.Of(new Color(0.55f, 0.36f, 0.22f), 4f, 0.85f), 76, 90);
            p.Shade(Grow(Star(new Vector2(50, 32), 8f, 3.6f), 1f), Shading.Of(new Color(1f, 0.92f, 0.62f), 2f, 0.6f), 24, 40);
            Gloss(p, cup, new Vector2(36, 26), new Vector2(4.5f, 10), 10f, 0.42f);
        }

        static void DrawPan(HudPainter p)
        {
            Sdf pan = Circle(44, 56, 30);
            Sdf handle = Capsule(68, 36, 90, 16, 6);
            Frame(p, Union(pan, handle));
            p.Shade(handle, Shading.Of(WoodBody, 4f, 0.9f), 10, 42);
            p.Fill(Circle(70.5f, 33.5f, 2.2f), new Color(0.30f, 0.32f, 0.35f));
            p.Shade(pan, Shading.Of(new Color(0.36f, 0.38f, 0.41f), 9f, 1f, 1.1f, 0.85f, 0.4f), 26, 86);
            p.Shade(Circle(44, 56, 23), Shading.Of(new Color(0.44f, 0.46f, 0.49f), 5f, -0.6f, 0.95f, 1.05f, 0f), 33, 79);
            Sdf white = Union(Circle(42, 56, 14), Circle(52, 50, 9), Circle(34, 62, 8.5f), Circle(47, 66, 8));
            p.Shade(white, Shading.Of(new Color(0.99f, 0.98f, 0.95f), 6f, 0.6f), 40, 74);
            p.Fill(Grow(Circle(44, 56, 7.5f), 1.6f), new Color(0.75f, 0.45f, 0.10f, 0.55f));
            p.Shade(Circle(44, 56, 7.5f), Shading.Of(new Color(1f, 0.72f, 0.20f), 5f, 1f, 1.1f, 0.9f, 0.5f), 48, 64);
        }

        static void DrawCloche(HudPainter p)
        {
            Sdf dome = Intersect(Ellipse(new Vector2(50, 66), new Vector2(34, 31)), Above(66));
            Sdf knob = Circle(50, 31, 6.5f);
            Sdf plate = Box(9, 64, 91, 76, 6);
            Frame(p, Union(dome, knob, plate));
            p.Shade(knob, Shading.Of(SteelBody, 4f, 1f), 24, 38);
            p.Shade(dome, Shading.Of(new Color(0.82f, 0.85f, 0.88f), 12f, 1f, 1.1f, 0.84f, 0.45f), 35, 66);
            p.Fill(Grow(plate, 2.4f), HudPalette.Outline);
            p.Shade(plate, Shading.Of(HudPalette.Cream, 4f, 0.8f), 64, 76);
            p.Fill(Ellipse(new Vector2(34, 50), new Vector2(4, 11), 32f), new Color(1f, 1f, 1f, 0.55f), dome);
        }

        static void DrawBroom(HudPainter p)
        {
            Sdf handle = Capsule(78, 10, 52, 56, 4.4f);
            Sdf band = Box(new Vector2(50, 59), new Vector2(13, 5), 2, 30f);
            Sdf bristles = Grow(Poly(36, 60, 60, 70, 56, 92, 14, 84), 3f);
            Frame(p, Union(handle, band, bristles));
            p.Shade(handle, Shading.Of(WoodBody, 3.5f, 0.9f), 10, 56);
            p.Shade(bristles, Shading.Of(new Color(0.88f, 0.73f, 0.40f), 6f, 0.8f), 60, 92);
            var straw = new Color(0.56f, 0.40f, 0.18f, 0.6f);
            for (int i = 0; i < 4; i++)
            {
                float t = i / 3f;
                var a = Vector2.Lerp(new Vector2(40, 66), new Vector2(58, 74), t);
                var b = Vector2.Lerp(new Vector2(22, 86), new Vector2(52, 92), t);
                p.Fill(Capsule(a, b, 0.9f), straw, bristles);
            }

            p.Fill(Grow(band, 2.2f), HudPalette.Outline);
            p.Shade(band, Shading.Of(HudPalette.Red, 3f, 0.9f), 52, 66);
        }

        static void DrawPeople(HudPainter p)
        {
            Sdf backHead = Circle(66, 32, 12);
            Sdf backBody = Intersect(Box(48, 48, 84, 92, 17), Above(84));
            Sdf frontHead = Circle(38, 40, 13);
            Sdf frontBody = Intersect(Box(17, 58, 59, 100, 19), Above(90));
            Frame(p, Union(backHead, backBody, frontHead, frontBody));

            var skin = new Color(0.93f, 0.76f, 0.60f);
            p.Shade(backBody, Shading.Of(HudPalette.Teal, 7f, 0.9f), 48, 84);
            p.Shade(backHead, Shading.Of(skin, 6f, 0.8f), 20, 44);
            p.Fill(Grow(Union(frontHead, frontBody), OutlineWidth), HudPalette.Outline);
            p.Shade(frontBody, Shading.Of(HudPalette.Red, 8f, 0.9f), 58, 90);
            p.Shade(frontHead, Shading.Of(skin, 6f, 0.8f), 27, 53);
            p.Fill(Intersect(Circle(38, 40, 13), Above(34)), new Color(0.40f, 0.26f, 0.16f), frontHead);
        }

        static void DrawMedic(HudPainter p)
        {
            Sdf card = Box(14, 14, 86, 86, 15);
            Frame(p, card);
            p.Shade(card, Shading.Of(HudPalette.Cream, 9f, 0.8f), 14, 86);
            Sdf cross = Union(Box(40, 25, 60, 75, 4), Box(25, 40, 75, 60, 4));
            p.Fill(Grow(cross, 2f), new Color(0.40f, 0.10f, 0.08f, 0.5f));
            p.Shade(cross, Shading.Of(new Color(0.84f, 0.29f, 0.25f), 6f, 0.9f), 25, 75);
        }

        static void DrawFlag(HudPainter p)
        {
            Sdf pole = Capsule(27, 12, 27, 86, 3.6f);
            Sdf flag = Grow(Poly(31, 17, 80, 22, 69, 36, 81, 51, 31, 47), 2f);
            Sdf foot = Box(14, 82, 40, 90, 4);
            Frame(p, Union(pole, flag, foot));
            p.Shade(pole, Shading.Of(WoodBody, 3f, 0.9f), 12, 86);
            p.Shade(flag, Shading.Of(HudPalette.Red, 7f, 0.9f), 17, 51);
            p.Fill(Capsule(38, 30, 62, 32, 1.1f), new Color(1f, 1f, 1f, 0.25f), flag);
            p.Shade(foot, Shading.Of(SteelBody, 3f, 0.8f), 82, 90);
            p.Shade(Circle(27, 11, 4.6f), Shading.Of(GoldBody, 3f, 0.8f), 6, 16);
        }

        static void DrawHammer(HudPainter p)
        {
            Sdf handle = Capsule(52, 44, 24, 86, 5.8f);
            Sdf head = Box(new Vector2(60, 32), new Vector2(26, 11), 4, 34f);
            Frame(p, Union(handle, head));
            p.Shade(handle, Shading.Of(WoodBody, 4f, 0.9f), 40, 90);
            p.Fill(Grow(head, 2.4f), HudPalette.Outline);
            p.Shade(head, Shading.Of(SteelBody, 7f, 1f, 1.08f, 0.86f, 0.35f), 14, 52);
            Gloss(p, head, new Vector2(56, 24), new Vector2(12, 3), 34f, 0.35f);
        }
    }
}
