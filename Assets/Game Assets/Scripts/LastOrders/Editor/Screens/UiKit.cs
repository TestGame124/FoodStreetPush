using LastOrders.EditorTools.Hud;
using LastOrders.UI.Hud;
using LastOrders.UI.Kit;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LastOrders.EditorTools.Screens
{
    /// <summary>
    /// The building blocks every redesigned screen is put together from - windows, buttons, tiles,
    /// wells, chips, bars and labels - all drawn from the HUD atlas (<see cref="HudSkin"/>) in the
    /// HUD's two typefaces, so the shop, the kitchen, the popups and the HUD read as one game.
    ///
    /// Positions are in the 1920x1080 reference space the screens' canvases scale from.
    /// </summary>
    public static class UiKit
    {
        public enum Face
        {
            Display,
            Body,
        }

        public enum Style
        {
            Plain,
            Outline,
            Shadow,
        }

        /// <summary>What a window's insides are laid out in: below the title band, inside the frame.</summary>
        public const float BandHeight = 96f;

        public static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);

        static HudSkin skin;

        public static HudSkin Skin
        {
            get
            {
                if (skin == null) Load();
                return skin;
            }
        }

        /// <summary>Loads the skin, baking the atlas first when it predates the screens' sprites.</summary>
        public static void Load()
        {
            skin = AssetDatabase.LoadAssetAtPath<HudSkin>(HudArt.SkinPath);
            if (skin == null || !System.Array.Exists(skin.sprites, s => s != null && s.name == HudSprites.Window))
            {
                HudArt.Bake();
                skin = AssetDatabase.LoadAssetAtPath<HudSkin>(HudArt.SkinPath);
            }

            skin.Rebuild();
        }

        // --- Layout ----------------------------------------------------------------------------------

        public static RectTransform Node(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                                         Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Anchored and pivoted at the same point of the parent, so positions read as offsets from it.</summary>
        public static RectTransform At(string name, Transform parent, Vector2 corner, Vector2 position, Vector2 size) =>
            Node(name, parent, corner, corner, corner, position, size);

        /// <summary>Centred in the parent.</summary>
        public static RectTransform Center(string name, Transform parent, Vector2 position, Vector2 size) =>
            Node(name, parent, Middle, Middle, Middle, position, size);

        /// <summary>Stretched over the parent, inset by the given margins.</summary>
        public static RectTransform Fill(string name, Transform parent, float left = 0f, float bottom = 0f, float right = 0f,
                                         float top = 0f)
        {
            RectTransform rt = Node(name, parent, Vector2.zero, Vector2.one, Middle, Vector2.zero, Vector2.zero);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>A strip across the top of the parent, <paramref name="height"/> tall, <paramref name="top"/> down.</summary>
        public static RectTransform Row(string name, Transform parent, float top, float height, float left = 0f, float right = 0f)
        {
            RectTransform rt = Node(name, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                                    new Vector2((left - right) * 0.5f, -top), new Vector2(-(left + right), height));
            return rt;
        }

        // --- Paint -----------------------------------------------------------------------------------

        /// <summary>
        /// An image from the atlas. A nine-sliced sprite shrinks its corners to fit a rect smaller than
        /// its borders (<paramref name="size"/>, or the rect's own size when it has one).
        /// </summary>
        public static Image Paint(RectTransform rt, string sprite, Color colour, bool raycast = false, Vector2? size = null,
                                  float scale = 0f)
        {
            var image = rt.gameObject.GetComponent<Image>();
            if (image == null) image = rt.gameObject.AddComponent<Image>();
            image.sprite = Skin[sprite];
            image.color = colour;
            image.raycastTarget = raycast;

            bool sliced = image.sprite != null && image.sprite.border != Vector4.zero;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            // Icons keep their shape; a plain white fill (dims, lines) stretches to its rect.
            image.preserveAspect = !sliced && sprite != HudSprites.White;

            if (sliced)
            {
                if (scale <= 0f)
                {
                    Vector2 s = size ?? rt.rect.size;
                    if (s.x <= 0f || s.y <= 0f) s = rt.sizeDelta;
                    Vector4 b = image.sprite.border;
                    scale = 1f;
                    if (s.y > 0f) scale = Mathf.Max(scale, (b.y + b.w) / (s.y * 0.98f));
                    if (s.x > 0f) scale = Mathf.Max(scale, (b.x + b.z) / (s.x * 0.98f));
                }

                image.pixelsPerUnitMultiplier = scale;
            }

            return image;
        }

        public static TextMeshProUGUI Label(RectTransform rt, string value, Face face, float size, Color colour,
                                            TextAlignmentOptions alignment = TextAlignmentOptions.Center, Style style = Style.Plain)
        {
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = face == Face.Display ? Skin.display : Skin.body;

            Material material = style switch
            {
                Style.Outline when face == Face.Display => Skin.displayOutline,
                Style.Outline => Skin.bodyShadow,
                Style.Shadow when face == Face.Display => Skin.displayShadow,
                Style.Shadow => Skin.bodyShadow,
                _ => null,
            };
            if (material != null) text.fontSharedMaterial = material;

            text.text = value;
            text.fontSize = size;
            text.color = colour;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.margin = Vector4.zero;
            return text;
        }

        /// <summary>Shrinks the text to fit its box instead of spilling out of it.</summary>
        public static TextMeshProUGUI Fit(this TextMeshProUGUI text, float min, bool wrap = false)
        {
            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = min;
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        public static Button MakeButton(RectTransform rt, Graphic target)
        {
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        public static HudMotion Motion(RectTransform rt, bool pressable = true)
        {
            var motion = rt.gameObject.AddComponent<HudMotion>();
            var so = new SerializedObject(motion);
            so.FindProperty("pressable").boolValue = pressable;
            so.ApplyModifiedPropertiesWithoutUndo();
            return motion;
        }

        /// <summary>A track with a fill inset into it.</summary>
        public static HudBar Bar(RectTransform track, Color fill, float height)
        {
            Paint(track, HudSprites.BarTrack, Color.white, size: new Vector2(200f, height));

            float inset = height >= 16f ? 3f : 1.5f;
            RectTransform area = Fill("Area", track, inset, inset, inset, inset);
            RectTransform fillRect = Fill("Fill", area);
            Image image = Paint(fillRect, HudSprites.BarFill, fill, size: new Vector2(200f, height - inset * 2f));

            var bar = track.gameObject.AddComponent<HudBar>();
            bar.Configure(fillRect, image, null);
            return bar;
        }

        // --- Composites ------------------------------------------------------------------------------

        /// <summary>The soft shadow a card or window casts, behind <paramref name="rt"/>'s face.</summary>
        public static Image DropShadow(RectTransform rt, float spread = 26f)
        {
            RectTransform shadow = Fill("Shadow", rt, -spread, -spread - 10f, -spread, -spread + 8f);
            shadow.SetAsFirstSibling();
            return Paint(shadow, HudSprites.CardShadow, Color.white);
        }

        /// <summary>A full-screen dim that closes <paramref name="window"/> when tapped.</summary>
        public static CanvasGroup Scrim(RectTransform parent, float alpha = 0.55f)
        {
            RectTransform scrim = Fill("Scrim", parent);
            Paint(scrim, HudSprites.White, new Color(0.10f, 0.07f, 0.05f, alpha), raycast: true);
            var group = scrim.gameObject.AddComponent<CanvasGroup>();
            return group;
        }

        public sealed class WindowParts
        {
            public RectTransform root;
            public RectTransform body;
            public RectTransform band;
            public Image face;
            public TextMeshProUGUI title;
            public Image icon;
            public Button close;
        }

        /// <summary>
        /// A window: the cream frame with its teal title band, an icon and the title in the band, the
        /// red round close button on its corner and a body area below the band for the contents.
        /// </summary>
        public static WindowParts Window(RectTransform parent, string name, Vector2 size, string title, string icon,
                                         Vector2? position = null)
        {
            var parts = new WindowParts();
            parts.root = Center(name, parent, position ?? new Vector2(0f, -10f), size);

            DropShadow(parts.root, 30f);
            parts.face = Paint(Fill("Face", parts.root), HudSprites.Window, Color.white, raycast: true, size: size, scale: 1f);

            // Diner trim under the band.
            RectTransform trim = Row("Trim", parts.root, BandHeight - 6f, 14f, 8f, 8f);
            Image checker = Paint(trim, HudSprites.Checker, new Color(1f, 1f, 1f, 0.9f));
            checker.type = Image.Type.Tiled;
            checker.pixelsPerUnitMultiplier = 2.3f;

            parts.band = Row("Band", parts.root, 4f, BandHeight - 12f, 40f, 40f);
            RectTransform heading = Center("Heading", parts.band, new Vector2(0f, 2f), new Vector2(size.x - 220f, 70f));
            var layout = heading.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            if (!string.IsNullOrEmpty(icon))
            {
                RectTransform iconRect = Node("Icon", heading, Middle, Middle, Middle, Vector2.zero, new Vector2(72f, 72f));
                parts.icon = Paint(iconRect, icon, Color.white);
                var element = iconRect.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = element.minWidth = 72f;
                element.preferredHeight = element.minHeight = 72f;
            }

            RectTransform titleRect = Node("Title", heading, Middle, Middle, Middle, Vector2.zero, new Vector2(400f, 70f));
            parts.title = Label(titleRect, title, Face.Display, 48f, Color.white, TextAlignmentOptions.Center, Style.Outline);
            var titleSize = titleRect.gameObject.AddComponent<LayoutElement>();
            titleSize.preferredHeight = 70f;
            titleSize.flexibleWidth = 0f;

            RectTransform close = Node("Close", parts.root, Vector2.one, Vector2.one, Middle, new Vector2(-18f, -22f),
                                       new Vector2(92f, 92f));
            Image closeFace = Paint(close, HudSprites.RoundRed, Color.white, raycast: true);
            parts.close = MakeButton(close, closeFace);
            Motion(close);
            Paint(Center("Glyph", close, new Vector2(0f, 5f), new Vector2(46f, 46f)), HudSprites.GlyphCross, Color.white);

            parts.body = Fill("Body", parts.root, 34f, 40f, 34f, BandHeight + 18f);
            return parts;
        }

        public sealed class ButtonParts
        {
            public Button button;
            public Image face;
            public TextMeshProUGUI label;
            public Image icon;
            public HudMotion motion;
        }

        /// <summary>A chunky button with a label and, optionally, an icon to its left.</summary>
        public static ButtonParts TextButton(RectTransform parent, string name, string sprite, string label, Vector2 size,
                                             Vector2 position, Vector2? anchor = null, string icon = null, float labelSize = 34f)
        {
            var parts = new ButtonParts();
            Vector2 a = anchor ?? Middle;
            RectTransform rt = Node(name, parent, a, a, Middle, position, size);
            parts.face = Paint(rt, sprite, Color.white, raycast: true, size: size);
            parts.button = MakeButton(rt, parts.face);
            parts.motion = Motion(rt);

            bool light = sprite == HudSprites.ButtonCream;
            RectTransform content = Fill("Content", rt, 18f, 10f, 18f, 4f);
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            if (!string.IsNullOrEmpty(icon))
            {
                float s = Mathf.Min(size.y - 12f, 64f);
                RectTransform iconRect = Node("Icon", content, Middle, Middle, Middle, Vector2.zero, new Vector2(s, s));
                parts.icon = Paint(iconRect, icon, Color.white);
                var element = iconRect.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = element.minWidth = s;
                element.preferredHeight = element.minHeight = s;
            }

            RectTransform labelRect = Node("Label", content, Middle, Middle, Middle, Vector2.zero, new Vector2(size.x - 40f, size.y));
            parts.label = Label(labelRect, label, Face.Display, labelSize, light ? HudPalette.Ink : Color.white,
                                TextAlignmentOptions.Center, light ? Style.Plain : Style.Shadow);
            return parts;
        }

        /// <summary>A round button with a white glyph (or a full-colour icon) in it.</summary>
        public static ButtonParts RoundButton(RectTransform parent, string name, string round, string glyph, float size,
                                              Vector2 position, Vector2? anchor = null, bool iconNotGlyph = false)
        {
            var parts = new ButtonParts();
            Vector2 a = anchor ?? Middle;
            RectTransform rt = Node(name, parent, a, a, Middle, position, new Vector2(size, size));
            parts.face = Paint(rt, round, Color.white, raycast: true);
            parts.button = MakeButton(rt, parts.face);
            parts.motion = Motion(rt);

            float inner = iconNotGlyph ? size * 0.7f : size * 0.5f;
            Color tint = round == HudSprites.RoundCream && !iconNotGlyph ? HudPalette.Ink : Color.white;
            parts.icon = Paint(Center("Glyph", rt, new Vector2(0f, size * 0.05f), new Vector2(inner, inner)), glyph, tint);
            return parts;
        }

        /// <summary>A small dark pill with white text in it: counts, timers, prices on pictures.</summary>
        public static TextMeshProUGUI Chip(RectTransform parent, string name, string text, Vector2 anchor, Vector2 position,
                                           Vector2 size, Color? colour = null, float fontSize = 24f)
        {
            RectTransform chip = Node(name, parent, anchor, anchor, Middle, position, size);
            Paint(chip, HudSprites.Chip, colour ?? new Color(0.27f, 0.19f, 0.13f, 0.92f), size: size);
            return Label(Fill("Text", chip, 6f, 2f, 6f, 0f), text, Face.Display, fontSize, Color.white,
                         TextAlignmentOptions.Center, Style.Plain);
        }

        /// <summary>The well's sunken look, painted onto <paramref name="rt"/>.</summary>
        public static Image Well(RectTransform rt, Vector2? size = null) => Paint(rt, HudSprites.Well, Color.white, size: size);

        /// <summary>A light raised tile with its soft shadow; returns the face.</summary>
        public static Image Tile(RectTransform rt, Vector2 size, bool raycast = false, bool shadow = true)
        {
            if (shadow)
            {
                RectTransform s = Fill("Shadow", rt, -12f, -16f, -12f, -6f);
                Paint(s, HudSprites.CardShadow, new Color(1f, 1f, 1f, 0.55f));
            }

            return Paint(Fill("Face", rt), HudSprites.Tile, Color.white, raycast, size: size);
        }

        public static void Layout<T>(RectTransform rt, float spacing, TextAnchor align, RectOffset padding = null)
            where T : HorizontalOrVerticalLayoutGroup
        {
            var layout = rt.gameObject.AddComponent<T>();
            layout.spacing = spacing;
            layout.childAlignment = align;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            if (padding != null) layout.padding = padding;
        }

        // --- Events ----------------------------------------------------------------------------------

        /// <summary>Wires <paramref name="action"/> into a button's saved click, replacing what it had.</summary>
        public static void OnClick(Button button, UnityAction action)
        {
            while (button.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(button.onClick, 0);
            UnityEventTools.AddPersistentListener(button.onClick, action);
        }

        /// <summary>Wires a saved "switch this off" into an event.</summary>
        public static void SwitchOff(UnityEvent e, GameObject target) =>
            UnityEventTools.AddBoolPersistentListener(e, target.SetActive, false);

        /// <summary>Closing the window - close button, a tap on the dim, the back button - all go through it.</summary>
        public static UiWindow WindowBehaviour(GameObject root, RectTransform panel, CanvasGroup scrim, Button close,
                                               UnityAction closed = null)
        {
            var window = root.AddComponent<UiWindow>();
            var so = new SerializedObject(window);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("scrim").objectReferenceValue = scrim;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (closed != null) UnityEventTools.AddPersistentListener(window.Closed, closed);
            if (close != null) OnClick(close, window.Close);

            if (scrim != null)
            {
                Button tap = scrim.gameObject.GetComponent<Button>();
                if (tap == null) tap = MakeButton((RectTransform)scrim.transform, scrim.GetComponent<Image>());
                OnClick(tap, window.Close);
            }

            return window;
        }

        /// <summary>Sets a serialized field by name; for wiring the screens' own scripts.</summary>
        public static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning($"[Screens] {target.GetType().Name} has no field '{field}'.", target);
                return;
            }

            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Set(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning($"[Screens] {target.GetType().Name} has no field '{field}'.", target);
                return;
            }

            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Set(Object target, string field, Color value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning($"[Screens] {target.GetType().Name} has no field '{field}'.", target);
                return;
            }

            p.colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Set(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning($"[Screens] {target.GetType().Name} has no field '{field}'.", target);
                return;
            }

            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning($"[Screens] {target.GetType().Name} has no field '{field}'.", target);
                return;
            }

            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
