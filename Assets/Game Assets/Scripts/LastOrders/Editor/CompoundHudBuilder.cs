using LastOrders.Blockout;
using LastOrders.Core;
using LastOrders.Debugging;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Builds the base HUD of section 20, laid out for landscape.
    ///
    /// Separate from HudBuilder rather than a change to it. That one authors the Heat bar,
    /// forecast marker and Crisis bar of the other design against a portrait 1080x1920
    /// reference; this one is the HUD 20 actually describes - level and coins top left, day and
    /// phase top centre, settings top right, one primary objective bottom left, Build and World
    /// Map bottom right, and the centre of the screen kept clear around the entrance and the
    /// dining activity.
    ///
    /// Built in code for the same reason HudBuilder is: a layout that comes from a table of
    /// anchors diffs, and a layout dragged in the inspector does not.
    /// </summary>
    public static class CompoundHudBuilder
    {
        // 18 asks for verification at narrow landscape ratios as well as 16:9, so the canvas
        // matches on height and every element anchors to a corner rather than to the centre.
        static readonly Vector2 Reference = new Vector2(1920f, 1080f);

        const float Margin = 28f;

        static readonly Color Panel = new Color(0.99f, 0.97f, 0.93f, 0.96f);
        static readonly Color Ink = new Color(0.18f, 0.16f, 0.14f, 1f);
        static readonly Color Dim = new Color(0.36f, 0.34f, 0.31f, 1f);
        static readonly Color Dark = new Color(0.16f, 0.15f, 0.17f, 0.82f);
        static readonly Color Accent = new Color(0.25f, 0.68f, 0.93f, 1f);
        static readonly Color Locked = new Color(0.45f, 0.45f, 0.48f, 0.85f);

        /// <summary>
        /// Builds the HUD.
        ///
        /// <paramref name="minimal"/> is for a level that already has a UI. Main Level has its
        /// own wallet, shop and build buttons, and drawing a second set on top of them at a
        /// higher sorting order is not a HUD, it is a lid. What that level is missing is only
        /// the things the day cycle added: which phase it is, the button that starts the day,
        /// and a line to answer taps on - so that is all it gets.
        /// </summary>
        public static int Build(GameLifetimeScope scope, bool minimal = false)
        {
            Canvas canvas = CreateCanvas();

            var root = new GameObject("CompoundHUD", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            Stretch(root.GetComponent<RectTransform>());

            var hud = root.AddComponent<CompoundHud>();

            if (!minimal)
            {
                hud.coinsText = BuildWallet(root.transform, out hud.levelText);
                BuildSettings(root.transform);
                hud.buildButton = BuildActions(root.transform, out hud.worldMapButton);
            }

            hud.dayText = BuildDayPill(root.transform);
            hud.objectiveTitle = BuildObjective(root.transform, out hud.objectiveBody);
            hud.phaseButton = BuildPhaseButton(root.transform, out hud.phaseButtonLabel);

            BuildTimeControls(root.transform, scope);

            var so = new SerializedObject(hud);
            so.FindProperty("scope").objectReferenceValue = scope;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root.GetComponentsInChildren<Graphic>(true).Length;
        }

        /// <summary>
        /// Builds the HUD its own canvas.
        ///
        /// It used to adopt the scene's largest existing canvas, which was right while the old
        /// bootstrap was running the scene. It is wrong now: that canvas lives under GameHandler,
        /// which this scene parks, so the HUD would be built onto a deactivated object and never
        /// appear. Its own canvas also means the legacy shop and order panels cannot render on
        /// top of it.
        /// </summary>
        static Canvas CreateCanvas()
        {
            const string Name = "LastOrdersCanvas";

            foreach (Canvas existing in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (existing.name == Name) Object.DestroyImmediate(existing.gameObject);
            }

            var go = new GameObject(Name);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Above whatever the old UI would draw, if any of it is ever switched back on.
            canvas.sortingOrder = 100;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;

            // 18 asks for verification at narrow landscape ratios as well as 16:9, so the
            // layout matches on height and every element anchors to a corner.
            scaler.matchWidthOrHeight = 1f;

            go.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var events = new GameObject("EventSystem");
                events.AddComponent<UnityEngine.EventSystems.EventSystem>();
                events.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            return canvas;
        }

        // --- Top left: level and coins ------------------------------------------------

        static Text BuildWallet(Transform parent, out Text levelText)
        {
            RectTransform bar = Rect("Wallet", parent, new Vector2(0f, 1f), new Vector2(0f, 1f),
                                     new Vector2(Margin, -Margin), new Vector2(330f, 74f));
            bar.pivot = new Vector2(0f, 1f);

            Pill(bar, Dark);

            RectTransform star = Rect("Level", bar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                      new Vector2(6f, 0f), new Vector2(62f, 62f));
            star.pivot = new Vector2(0f, 0.5f);
            Circle(star, new Color(0.98f, 0.78f, 0.22f));

            levelText = Label(star, "1", 30, TextAnchor.MiddleCenter, Ink);

            RectTransform coin = Rect("Coin", bar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                      new Vector2(84f, 0f), new Vector2(44f, 44f));
            coin.pivot = new Vector2(0f, 0.5f);
            Circle(coin, new Color(0.96f, 0.74f, 0.20f));

            RectTransform amount = Rect("Coins", bar, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                                        new Vector2(0f, 0f), Vector2.zero);
            amount.offsetMin = new Vector2(140f, -26f);
            amount.offsetMax = new Vector2(-16f, 26f);

            // 9: coins and day are the two things on the opening HUD; everything else waits for
            // the Inventory and cost panels.
            return Label(amount, "200", 34, TextAnchor.MiddleLeft, Color.white);
        }

        // --- Top centre: day and phase --------------------------------------------------

        static Text BuildDayPill(Transform parent)
        {
            RectTransform pill = Rect("DayPhase", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                      new Vector2(0f, -Margin), new Vector2(420f, 66f));
            pill.pivot = new Vector2(0.5f, 1f);

            Pill(pill, Dark);

            RectTransform icon = Rect("Phase", pill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                      new Vector2(18f, 0f), new Vector2(40f, 40f));
            icon.pivot = new Vector2(0f, 0.5f);
            Circle(icon, new Color(1f, 0.85f, 0.35f));

            RectTransform label = Rect("Label", pill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.offsetMin = new Vector2(66f, 0f);
            label.offsetMax = new Vector2(-18f, 0f);

            return Label(label, "DAY 1  •  MORNING", 30, TextAnchor.MiddleCenter, Color.white);
        }

        static void BuildSettings(Transform parent)
        {
            RectTransform gear = Rect("Settings", parent, new Vector2(1f, 1f), new Vector2(1f, 1f),
                                      new Vector2(-Margin, -Margin), new Vector2(72f, 72f));
            gear.pivot = new Vector2(1f, 1f);

            Circle(gear, Dark);
            Label(gear, "*", 40, TextAnchor.MiddleCenter, Color.white);

            gear.gameObject.AddComponent<Button>();
        }

        // --- Bottom left: one objective ---------------------------------------------------

        static Text BuildObjective(Transform parent, out Text body)
        {
            RectTransform card = Rect("Objective", parent, Vector2.zero, Vector2.zero,
                                      new Vector2(Margin, Margin), new Vector2(470f, 108f));
            card.pivot = Vector2.zero;

            Pill(card, Panel);

            RectTransform icon = Rect("Icon", card, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                      new Vector2(22f, 0f), new Vector2(60f, 60f));
            icon.pivot = new Vector2(0f, 0.5f);
            Circle(icon, new Color(0.72f, 0.55f, 0.35f));

            RectTransform title = Rect("Title", card, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                       Vector2.zero, Vector2.zero);
            title.pivot = new Vector2(0f, 1f);
            title.offsetMin = new Vector2(100f, -58f);
            title.offsetMax = new Vector2(-18f, -16f);

            RectTransform sub = Rect("Body", card, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
            sub.pivot = Vector2.zero;
            sub.offsetMin = new Vector2(100f, 18f);
            sub.offsetMax = new Vector2(-18f, 52f);

            // 13 step 1, and 20's rule that the objective shows one thing with its progress.
            body = Label(sub, "Clear the diner entrance", 26, TextAnchor.UpperLeft, Dim);
            return Label(title, "A Fresh Start", 32, TextAnchor.LowerLeft, Ink);
        }

        // --- Bottom right: Build and the locked World Map -------------------------------------

        static Button BuildActions(Transform parent, out Button worldMap)
        {
            RectTransform map = Rect("WorldMap", parent, new Vector2(1f, 0f), new Vector2(1f, 0f),
                                     new Vector2(-Margin, Margin), new Vector2(104f, 104f));
            map.pivot = new Vector2(1f, 0f);

            Pill(map, Locked, 24f);
            Label(map, "MAP", 22, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.75f));

            worldMap = map.gameObject.AddComponent<Button>();

            // 20: the World Map shows a lock until the first night is won.
            worldMap.interactable = false;

            RectTransform build = Rect("Build", parent, new Vector2(1f, 0f), new Vector2(1f, 0f),
                                       new Vector2(-(Margin + 120f), Margin), new Vector2(188f, 104f));
            build.pivot = new Vector2(1f, 0f);

            Pill(build, Accent, 24f);
            Label(build, "Build", 32, TextAnchor.MiddleCenter, Color.white);

            return build.gameObject.AddComponent<Button>();
        }

        // --- Bottom centre: the one action that moves the day on ------------------------------

        /// <summary>
        /// Start the day, and skip the rest of the dusk warning.
        ///
        /// Bottom centre rather than with the other actions, because it is the only control on
        /// screen that advances time and it should not be one tab over from Build. Without it
        /// the compound sits in a paused dawn forever: dawn is paused by design and nothing
        /// else in this scene asks the machine to leave it, so nobody ever walks the road.
        /// </summary>
        static Button BuildPhaseButton(Transform parent, out Text label)
        {
            RectTransform pill = Rect("PhaseAction", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                      new Vector2(0f, Margin), new Vector2(300f, 92f));
            pill.pivot = new Vector2(0.5f, 0f);

            Pill(pill, new Color(0.96f, 0.72f, 0.28f), 24f);
            label = Label(pill, "Start the day", 32, TextAnchor.MiddleCenter, Ink);

            return pill.gameObject.AddComponent<Button>();
        }

        // --- Test controls --------------------------------------------------------------------

        /// <summary>
        /// Day/night controls, for testing.
        ///
        /// Almost everything in this game is gated on the phase - customers only during the
        /// day, the siege only at night - and the working day is six real minutes long. A
        /// toggle and two hold modes turn "wait and see" into "look now", which is the
        /// difference between a bug that takes a minute to find and one that takes an evening.
        ///
        /// Deliberately plain and deliberately labelled TEST. It is not a player feature, and
        /// it should be obvious in a screenshot that it is not.
        /// </summary>
        static void BuildTimeControls(Transform parent, GameLifetimeScope scope)
        {
            RectTransform bar = Rect("TimeControls", parent, new Vector2(0f, 1f), new Vector2(0f, 1f),
                                     new Vector2(Margin, -(Margin + 90f)), new Vector2(430f, 120f));
            bar.pivot = new Vector2(0f, 1f);

            Pill(bar, Dark, 18f);

            RectTransform caption = Rect("Caption", bar, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                         Vector2.zero, Vector2.zero);
            caption.pivot = new Vector2(0f, 1f);
            caption.offsetMin = new Vector2(16f, -38f);
            caption.offsetMax = new Vector2(-16f, -8f);

            Label(caption, "TEST - TIME", 20, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.6f));

            Button toggle = TimeButton(bar, "Day / Night", 16f, 150f);
            Button normal = TimeButton(bar, "Normal", 172f, 78f);
            Button day = TimeButton(bar, "All Day", 256f, 78f);
            Button night = TimeButton(bar, "All Night", 340f, 78f);

            // Handed the buttons rather than wired to them here. A listener added from an
            // editor script is never serialised, so buttons wired at build time come back from
            // a scene load looking perfect and doing nothing.
            var controls = bar.gameObject.AddComponent<TimeControls>();
            controls.Configure(scope, toggle, normal, day, night);
        }

        static Button TimeButton(RectTransform parent, string text, float x, float width)
        {
            RectTransform rect = Rect(text, parent, new Vector2(0f, 0f), new Vector2(0f, 0f),
                                      new Vector2(x, 14f), new Vector2(width, 56f));
            rect.pivot = Vector2.zero;

            Pill(rect, new Color(0.30f, 0.34f, 0.40f), 14f);
            Label(rect, text, 20, TextAnchor.MiddleCenter, Color.white);

            return rect.gameObject.AddComponent<Button>();
        }

        // --- Primitives ---------------------------------------------------------------------

        static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                                  Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Image Pill(RectTransform rt, Color colour, float radius = 0f)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.color = colour;

            // Unity's built-in rounded sprite when it is available; a flat rect otherwise.
            // Deliberately not shipping a texture for a greybox HUD.
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;

            return image;
        }

        static Image Circle(RectTransform rt, Color colour)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.color = colour;
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            return image;
        }

        static Text Label(RectTransform parent, string value, int size, TextAnchor anchor, Color colour)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Stretch(go.GetComponent<RectTransform>());

            var text = go.AddComponent<Text>();
            text.text = value;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = colour;
            text.raycastTarget = false;

            // Same choice HudBuilder makes: the built-in font, so the HUD needs no font asset
            // and no TMP dependency to render.
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return text;
        }
    }
}
