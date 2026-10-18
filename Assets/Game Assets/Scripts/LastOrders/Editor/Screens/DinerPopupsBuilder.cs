using System.IO;
using LastOrders.UI.Hud;
using LastOrders.UI.Popups;
using LastOrders.UI.World;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static LastOrders.EditorTools.Screens.UiKit;

namespace LastOrders.EditorTools.Screens
{
    /// <summary>
    /// The game's screen popups - expanding land, a guard tower or wall gun, a shelter, a worker,
    /// expanding the restaurant, feeding the coop or the cow shed - rebuilt in the screens' look:
    /// the cream window with its teal title band, chunky buttons that take the colour the popup gives
    /// them, sunken wells for details and requirement rows, the HUD's type.
    ///
    /// The popups' scripts are unchanged; each new prefab carries the same script with every field
    /// wired as before, and its button colours set to the diner's. The UI catalog is pointed at the
    /// new prefabs; the old ones are left where they were, and 'Restore Previous Popups' points the
    /// catalog back at them.
    /// </summary>
    public static class DinerPopupsBuilder
    {
        public const string Folder = DinerScreensBuilder.PrefabFolder + "/Popups";

        const string OldFolder = WorldUiPrefabGenerator.Folder;

        // Colours the popups tint their buttons with. The tint button is grey, so these are the
        // colour the top of a button comes out.
        internal static readonly Color Ready = new Color(0.42f, 0.78f, 0.72f);
        internal static readonly Color Blocked = new Color(0.80f, 0.78f, 0.75f);
        static readonly Color Premium = new Color(0.93f, 0.50f, 0.62f);
        internal static readonly Color Met = new Color(0.20f, 0.52f, 0.28f);
        internal static readonly Color Short = new Color(0.78f, 0.31f, 0.25f);

        [MenuItem("Last Orders/UI/Build Diner Popups", priority = 212)]
        static void BuildFromMenu() => Debug.Log($"[Last Orders] {Build()}");

        [MenuItem("Last Orders/UI/Restore Previous Popups", priority = 213)]
        static void RestoreFromMenu() => Debug.Log($"[Last Orders] {RestorePrevious()}");

        public static string Build()
        {
            Load();
            Directory.CreateDirectory(Folder);

            var catalog = AssetDatabase.LoadAssetAtPath<WorldUiCatalog>(WorldUiPrefabGenerator.CatalogPath);
            if (catalog == null) return $"No UI catalog at {WorldUiPrefabGenerator.CatalogPath} - run the world UI setup first.";

            Undo.RecordObject(catalog, "Build Diner Popups");
            catalog.expansionPopup = Save<ExpansionPopupView>(BuildExpansion(), "UI_ExpansionPopup");
            catalog.defensePopup = Save<DefensePopupView>(BuildDefense(), "UI_DefensePopup");
            catalog.shelterPopup = Save<ShelterPopupView>(BuildShelter(), "UI_ShelterPopup");
            catalog.workerPanel = Save<WorkerPanelView>(BuildWorker(), "UI_WorkerPanel");
            catalog.restaurantExpansionPopup = Save<RestaurantExpansionPopupView>(BuildRestaurant(), "UI_RestaurantExpansionPopup");
            catalog.feedPopup = Save<FeedPopupView>(BuildFeed(), "UI_FeedPopup");

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            return $"Diner popups built in {Folder} (6 popups) and put in the UI catalog. The old ones are kept in {OldFolder}.";
        }

        public static string RestorePrevious()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WorldUiCatalog>(WorldUiPrefabGenerator.CatalogPath);
            if (catalog == null) return "No UI catalog - nothing to restore.";

            T Old<T>(string name, T current) where T : Component
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{OldFolder}/{name}.prefab");
                T old = prefab != null ? prefab.GetComponent<T>() : null;
                return old != null ? old : current;
            }

            Undo.RecordObject(catalog, "Restore Previous Popups");
            catalog.expansionPopup = Old("UI_ExpansionPopup", catalog.expansionPopup);
            catalog.defensePopup = Old("UI_DefensePopup", catalog.defensePopup);
            catalog.shelterPopup = Old("UI_ShelterPopup", catalog.shelterPopup);
            catalog.workerPanel = Old("UI_WorkerPanel", catalog.workerPanel);
            catalog.restaurantExpansionPopup = Old("UI_RestaurantExpansionPopup", catalog.restaurantExpansionPopup);
            catalog.feedPopup = Old("UI_FeedPopup", catalog.feedPopup);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            return "The UI catalog points at the previous popups again; the diner popups are left in place.";
        }

        static T Save<T>(GameObject built, string name) where T : Component
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(built, $"{Folder}/{name}.prefab");
            Object.DestroyImmediate(built);
            return prefab.GetComponent<T>();
        }

        // --- The frame every popup shares ---------------------------------------------------------

        internal sealed class Frame
        {
            public GameObject root;
            public RectTransform panel;
            public CanvasGroup group;
            public Button backdrop;
            public Button close;
            public TextMeshProUGUI title;
        }

        /// <summary>The dim, the window with its title band and icon, and the close button.</summary>
        internal static Frame Popup(string name, Vector2 size, string title, string icon)
        {
            var f = new Frame();
            f.root = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var root = (RectTransform)f.root.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            f.group = f.root.AddComponent<CanvasGroup>();

            RectTransform dim = Fill("Backdrop", root);
            Image shade = Paint(dim, HudSprites.White, new Color(0.10f, 0.07f, 0.05f, 0.55f), raycast: true);
            f.backdrop = MakeButton(dim, shade);

            WindowParts w = Window(root, "Panel", size, title, null, Vector2.zero);
            f.panel = w.root;
            f.close = w.close;

            // The title is written by the popup at runtime and can be long; it shrinks to the band.
            f.title = w.title;
            var heading = w.title.transform.parent.GetComponent<HorizontalLayoutGroup>();
            heading.childControlWidth = false;
            ((RectTransform)w.title.transform).sizeDelta = new Vector2(size.x - (string.IsNullOrEmpty(icon) ? 240f : 330f), 70f);
            w.title.Fit(24f);

            if (!string.IsNullOrEmpty(icon))
                Paint(At("Icon", w.band, new Vector2(0f, 0.5f), new Vector2(4f, 2f), new Vector2(70f, 70f)), icon, Color.white);

            return f;
        }

        internal static TextMeshProUGUI Para(RectTransform panel, string name, Vector2 at, Vector2 size, Face face, float fontSize, Color colour,
                                    TextAlignmentOptions align = TextAlignmentOptions.Center, float min = 16f, bool wrap = true)
        {
            TextMeshProUGUI t = Label(Center(name, panel, at, size), "", face, fontSize, colour, align).Fit(min, wrap);
            return t;
        }

        /// <summary>A chunky button the popup tints: grey art, so the colour it is given is the colour it shows.</summary>
        internal static (Button, Image, TextMeshProUGUI) TintButton(RectTransform panel, string name, Vector2 at, Vector2 size, string text,
                                                           string sprite = null)
        {
            RectTransform rt = Center(name, panel, at, size);
            Image face = Paint(rt, sprite ?? HudSprites.ButtonTint, sprite == null ? Ready : Color.white, raycast: true, size: size);
            Button button = MakeButton(rt, face);
            Motion(rt);

            bool light = sprite == HudSprites.ButtonCream;
            TextMeshProUGUI label = Label(Fill("Label", rt, 18f, 12f, 18f, 4f), text, Face.Display, 34f,
                                          light ? HudPalette.Ink : Color.white, TextAlignmentOptions.Center,
                                          light ? Style.Plain : Style.Shadow).Fit(18f);
            return (button, face, label);
        }

        static RequirementRowView RequirementRow(RectTransform panel, int index, Vector2 at)
        {
            var size = new Vector2(680f, 56f);
            RectTransform rt = Center($"Requirement_{index}", panel, at, size);
            Well(rt, size);

            Image status = Paint(At("Status", rt, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(26f, 26f)), HudSprites.Disc, Met);
            TextMeshProUGUI label = Label(At("Label", rt, new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(380f, 44f)), "Coins",
                                          Face.Body, 26f, HudPalette.Ink, TextAlignmentOptions.Left).Fit(16f);
            TextMeshProUGUI amount = Label(At("Amount", rt, new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(220f, 44f)), "0 / 0",
                                           Face.Display, 28f, HudPalette.Ink, TextAlignmentOptions.Right).Fit(16f);

            var row = rt.gameObject.AddComponent<RequirementRowView>();
            Set(row, "label", label);
            Set(row, "amount", amount);
            Set(row, "status", status);
            Set(row, "metColour", Met);
            Set(row, "shortColour", Short);
            return row;
        }

        /// <summary>A sunken block with a paragraph of details in it.</summary>
        internal static TextMeshProUGUI Details(RectTransform panel, Vector2 at, Vector2 size)
        {
            RectTransform back = Center("DetailsBack", panel, at, size);
            Well(back, size);
            TextMeshProUGUI text = Label(Fill("Details", back, 26f, 14f, 26f, 14f), "", Face.Body, 26f, HudPalette.Ink,
                                         TextAlignmentOptions.MidlineLeft).Fit(16f, wrap: true);
            text.lineSpacing = 6f;
            return text;
        }

        /// <summary>A rounded track with a fill the popup sets the amount and colour of.</summary>
        internal static Image FilledBar(RectTransform panel, string name, Vector2 at, Vector2 size, out RectTransform track)
        {
            track = Center(name, panel, at, size);
            Paint(track, HudSprites.BarTrack, Color.white, size: size);

            RectTransform area = Fill("Area", track, 3f, 3f, 3f, 3f);
            Image shape = Paint(area, HudSprites.BarFill, Color.white, size: size - new Vector2(6f, 6f));
            area.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            Image fill = Paint(Fill("Fill", area), HudSprites.White, HudPalette.Mint);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0.6f;
            Paint(Row("Shine", area, 2f, Mathf.Max(4f, size.y * 0.28f), 6f, 6f), HudSprites.White, new Color(1f, 1f, 1f, 0.22f));
            return fill;
        }

        internal static void WireFrame(Object view, Frame f)
        {
            Set(view, "panel", f.panel);
            Set(view, "group", f.group);
            Set(view, "backdrop", f.backdrop);
            Set(view, "closeButton", f.close);
            Set(view, "title", f.title);
        }

        internal static void Colours(Object view, Color ready, Color blocked)
        {
            Set(view, "readyColour", ready);
            Set(view, "blockedColour", blocked);
        }

        // --- The popups ---------------------------------------------------------------------------

        static GameObject BuildExpansion()
        {
            Frame f = Popup("UI_ExpansionPopup", new Vector2(820f, 740f), "Expand", HudSprites.IconFlag);
            RectTransform p = f.panel;

            TextMeshProUGUI benefit = Para(p, "Benefit", new Vector2(0f, 214f), new Vector2(740f, 56f), Face.Body, 26f, HudPalette.InkSoft);
            benefit.fontStyle = FontStyles.Italic;
            Label(Center("RequirementsCaption", p, new Vector2(0f, 158f), new Vector2(680f, 36f)), "NEEDS", Face.Display, 24f,
                  HudPalette.InkSoft, TextAlignmentOptions.Left);

            var rows = new RequirementRowView[4];
            for (int i = 0; i < rows.Length; i++) rows[i] = RequirementRow(p, i, new Vector2(0f, 108f - i * 64f));

            RectTransform timeRow = Center("BuildTimeRow", p, new Vector2(0f, -150f), new Vector2(400f, 44f));
            Paint(At("Clock", timeRow, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(44f, 44f)), HudSprites.IconClock, Color.white);
            TextMeshProUGUI time = Label(At("BuildTime", timeRow, new Vector2(0f, 0.5f), new Vector2(92f, 0f), new Vector2(300f, 40f)),
                                         "Build time: 12s", Face.Display, 26f, HudPalette.Ink, TextAlignmentOptions.Left);

            TextMeshProUGUI reason = Para(p, "Reason", new Vector2(0f, -196f), new Vector2(740f, 40f), Face.Body, 24f, Short);
            (Button expand, Image expandImage, _) = TintButton(p, "ExpandButton", new Vector2(0f, -276f), new Vector2(400f, 100f), "EXPAND");

            var view = f.root.AddComponent<ExpansionPopupView>();
            WireFrame(view, f);
            Set(view, "benefit", benefit);
            Set(view, "buildTime", time);
            Set(view, "reason", reason);
            Set(view, "expandButton", expand);
            Set(view, "expandButtonImage", expandImage);
            SetArray(view, "rows", rows);
            Colours(view, Ready, Blocked);
            return f.root;
        }

        static GameObject BuildDefense()
        {
            Frame f = Popup("UI_DefensePopup", new Vector2(880f, 740f), "Guard Tower", HudSprites.IconShield);
            RectTransform p = f.panel;

            TextMeshProUGUI description = Para(p, "Description", new Vector2(0f, 210f), new Vector2(780f, 70f), Face.Body, 25f,
                                               HudPalette.InkSoft);
            TextMeshProUGUI status = Para(p, "Status", new Vector2(0f, 148f), new Vector2(780f, 44f), Face.Display, 30f, HudPalette.Ink,
                                          min: 18f, wrap: false);
            TextMeshProUGUI details = Details(p, new Vector2(0f, 0f), new Vector2(760f, 230f));
            TextMeshProUGUI reason = Para(p, "Reason", new Vector2(0f, -150f), new Vector2(780f, 40f), Face.Body, 24f, Short);

            (Button stores, Image storesImage, TextMeshProUGUI storesLabel) =
                TintButton(p, "PayResources", new Vector2(-200f, -262f), new Vector2(370f, 100f), "60 Coins");
            (Button gems, Image gemsImage, TextMeshProUGUI gemsLabel) =
                TintButton(p, "PayPremium", new Vector2(200f, -262f), new Vector2(370f, 100f), "20 Gems");
            gemsImage.color = Premium;

            var view = f.root.AddComponent<DefensePopupView>();
            WireFrame(view, f);
            Set(view, "description", description);
            Set(view, "status", status);
            Set(view, "details", details);
            Set(view, "reason", reason);
            Set(view, "resourcesButton", stores);
            Set(view, "resourcesButtonImage", storesImage);
            Set(view, "resourcesLabel", storesLabel);
            Set(view, "premiumButton", gems);
            Set(view, "premiumButtonImage", gemsImage);
            Set(view, "premiumLabel", gemsLabel);
            Colours(view, Ready, Blocked);
            Set(view, "premiumColour", Premium);
            return f.root;
        }

        static GameObject BuildShelter()
        {
            Frame f = Popup("UI_ShelterPopup", new Vector2(920f, 760f), "Shelter", HudSprites.IconVisitors);
            RectTransform p = f.panel;

            TextMeshProUGUI description = Para(p, "Description", new Vector2(0f, 220f), new Vector2(820f, 70f), Face.Body, 25f,
                                               HudPalette.InkSoft);
            TextMeshProUGUI status = Para(p, "Status", new Vector2(0f, 158f), new Vector2(820f, 44f), Face.Display, 30f, HudPalette.Ink,
                                          min: 18f, wrap: false);
            TextMeshProUGUI details = Details(p, new Vector2(0f, 8f), new Vector2(820f, 236f));
            TextMeshProUGUI reason = Para(p, "Reason", new Vector2(0f, -150f), new Vector2(820f, 40f), Face.Body, 24f, Short);

            var buttons = new Button[3];
            var images = new Image[3];
            var labels = new TextMeshProUGUI[3];
            for (int i = 0; i < buttons.Length; i++)
                (buttons[i], images[i], labels[i]) = TintButton(p, $"Option_{i}", new Vector2((i - 1) * 280f, -262f), new Vector2(264f, 104f), "Hire");

            var view = f.root.AddComponent<ShelterPopupView>();
            WireFrame(view, f);
            Set(view, "description", description);
            Set(view, "status", status);
            Set(view, "details", details);
            Set(view, "reason", reason);
            SetArray(view, "optionButtons", buttons);
            SetArray(view, "optionImages", images);
            SetArray(view, "optionLabels", labels);
            Colours(view, Ready, Blocked);
            return f.root;
        }

        static GameObject BuildWorker()
        {
            Frame f = Popup("UI_WorkerPanel", new Vector2(920f, 860f), "Worker", HudSprites.IconVisitors);
            RectTransform p = f.panel;

            TextMeshProUGUI subtitle = Para(p, "Subtitle", new Vector2(0f, 290f), new Vector2(820f, 44f), Face.Body, 26f, HudPalette.InkSoft,
                                            min: 18f, wrap: false);
            subtitle.fontStyle = FontStyles.Italic;

            Image healthFill = FilledBar(p, "Health", new Vector2(0f, 234f), new Vector2(660f, 46f), out RectTransform health);
            Paint(At("Heart", health, new Vector2(0f, 0.5f), new Vector2(-30f, 0f), new Vector2(60f, 60f)), HudSprites.IconMedic, Color.white);
            TextMeshProUGUI healthLabel = Label(Fill("Label", health, 10f, 2f, 10f, 0f), "100/100 HP", Face.Display, 26f, Color.white,
                                                TextAlignmentOptions.Center, Style.Outline);

            TextMeshProUGUI status = Para(p, "Status", new Vector2(0f, 168f), new Vector2(820f, 52f), Face.Display, 30f, HudPalette.Ink,
                                          min: 18f);
            TextMeshProUGUI details = Details(p, new Vector2(0f, 34f), new Vector2(820f, 200f));
            TextMeshProUGUI reason = Para(p, "Reason", new Vector2(0f, -100f), new Vector2(820f, 44f), Face.Body, 24f, Short);

            var buttons = new Button[3];
            var images = new Image[3];
            var labels = new TextMeshProUGUI[3];
            for (int i = 0; i < buttons.Length; i++)
                (buttons[i], images[i], labels[i]) = TintButton(p, $"Option_{i}", new Vector2((i - 1) * 280f, -196f), new Vector2(264f, 100f), "Send");

            (Button recall, _, TextMeshProUGUI recallLabel) =
                TintButton(p, "Recall", new Vector2(0f, -320f), new Vector2(470f, 100f), "RECALL WORKER", HudSprites.ButtonRed);

            var view = f.root.AddComponent<WorkerPanelView>();
            WireFrame(view, f);
            Set(view, "subtitle", subtitle);
            Set(view, "healthRoot", health.gameObject);
            Set(view, "healthFill", healthFill);
            Set(view, "healthLabel", healthLabel);
            Set(view, "healthyColour", HudPalette.Mint);
            Set(view, "lowColour", HudPalette.Red);
            Set(view, "status", status);
            Set(view, "details", details);
            Set(view, "reason", reason);
            Set(view, "recallButton", recall);
            Set(view, "recallLabel", recallLabel);
            SetArray(view, "optionButtons", buttons);
            SetArray(view, "optionImages", images);
            SetArray(view, "optionLabels", labels);
            Colours(view, Ready, Blocked);
            return f.root;
        }

        static GameObject BuildRestaurant()
        {
            Frame f = Popup("UI_RestaurantExpansionPopup", new Vector2(840f, 780f), "Expand the restaurant?", HudSprites.IconDiner);
            RectTransform p = f.panel;

            TextMeshProUGUI body = Para(p, "Body", new Vector2(0f, 214f), new Vector2(760f, 110f), Face.Body, 26f, HudPalette.InkSoft);
            Label(Center("RequirementsCaption", p, new Vector2(0f, 132f), new Vector2(680f, 36f)), "NEEDS", Face.Display, 24f,
                  HudPalette.InkSoft, TextAlignmentOptions.Left);

            var rows = new RequirementRowView[4];
            for (int i = 0; i < rows.Length; i++) rows[i] = RequirementRow(p, i, new Vector2(0f, 82f - i * 64f));

            TextMeshProUGUI reason = Para(p, "Reason", new Vector2(0f, -212f), new Vector2(760f, 40f), Face.Body, 24f, Short);

            (Button yes, Image yesImage, _) = TintButton(p, "YesButton", new Vector2(-180f, -296f), new Vector2(320f, 100f), "YES");
            (Button no, _, _) = TintButton(p, "NoButton", new Vector2(180f, -296f), new Vector2(320f, 100f), "NOT NOW", HudSprites.ButtonCream);

            var view = f.root.AddComponent<RestaurantExpansionPopupView>();
            WireFrame(view, f);
            Set(view, "body", body);
            Set(view, "reason", reason);
            Set(view, "yesButton", yes);
            Set(view, "yesButtonImage", yesImage);
            Set(view, "noButton", no);
            SetArray(view, "rows", rows);
            Colours(view, Ready, Blocked);
            return f.root;
        }

        /// <summary>
        /// Feeding a coop or a cow shed: what the building is doing now, then how many to make - the
        /// feed it costs and what it makes, a minus and a plus, the most it can take - and the button.
        /// </summary>
        static GameObject BuildFeed()
        {
            Frame f = Popup("UI_FeedPopup", new Vector2(920f, 840f), "Chicken Coop", HudSprites.IconSprout);
            RectTransform p = f.panel;

            TextMeshProUGUI description = Para(p, "Description", new Vector2(0f, 276f), new Vector2(820f, 44f), Face.Body, 24f,
                                               HudPalette.InkSoft, min: 16f, wrap: false);
            description.fontStyle = FontStyles.Italic;

            // What it is doing now.
            var nowSize = new Vector2(840f, 140f);
            RectTransform now = Center("Now", p, new Vector2(0f, 172f), nowSize);
            Well(now, nowSize);
            RectTransform disc = At("NowDisc", now, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(112f, 112f));
            Paint(disc, HudSprites.RoundCream, Color.white);
            Image nowIcon = Paint(Center("Icon", disc, new Vector2(0f, 5f), new Vector2(78f, 78f)), HudSprites.IconSeeds, Color.white);
            TextMeshProUGUI status = Label(At("Status", now, new Vector2(0f, 0.5f), new Vector2(150f, 30f), new Vector2(520f, 42f)),
                                           "Next egg in", Face.Display, 28f, HudPalette.Ink, TextAlignmentOptions.Left).Fit(18f);
            TextMeshProUGUI stock = Chip(now, "StockPill", "0/6", new Vector2(1f, 0.5f), new Vector2(-82f, 30f), new Vector2(112f, 44f), null, 24f);
            Image bar = FilledBar(now, "Track", new Vector2(70f, -26f), new Vector2(640f, 42f), out RectTransform track);
            TextMeshProUGUI barTime = Label(Fill("Time", track, 8f, 2f, 8f, 0f), "24s", Face.Display, 24f, Color.white,
                                            TextAlignmentOptions.Center, Style.Outline);

            // How many to make.
            var pickSize = new Vector2(840f, 310f);
            RectTransform pick = Center("Pick", p, new Vector2(0f, -60f), pickSize);
            Well(pick, pickSize);
            Label(At("Ask", pick, new Vector2(0f, 1f), new Vector2(24f, -12f), new Vector2(300f, 40f)), "HOW MANY?", Face.Display, 24f,
                  HudPalette.InkSoft, TextAlignmentOptions.Left);

            Image feedIcon = Paint(Center("FeedIcon", pick, new Vector2(-160f, 70f), new Vector2(92f, 92f)), HudSprites.IconSeeds, Color.white);
            TextMeshProUGUI feedCount = Label(Center("FeedCount", pick, new Vector2(-82f, 58f), new Vector2(80f, 44f)), "x1", Face.Display, 32f,
                                              HudPalette.Ink, TextAlignmentOptions.Left);
            Paint(Center("Makes", pick, new Vector2(-6f, 70f), new Vector2(50f, 50f)), HudSprites.GlyphNext, HudPalette.Teal);
            Image produceIcon = Paint(Center("ProduceIcon", pick, new Vector2(80f, 70f), new Vector2(92f, 92f)), HudSprites.IconSeeds, Color.white);
            TextMeshProUGUI produceCount = Label(Center("ProduceCount", pick, new Vector2(158f, 58f), new Vector2(80f, 44f)), "x2", Face.Display,
                                                 32f, HudPalette.Ink, TextAlignmentOptions.Left);

            Button less = Stepper(pick, "Less", new Vector2(-250f, -32f), HudSprites.GlyphMinus);
            Button more = Stepper(pick, "More", new Vector2(250f, -32f), HudSprites.GlyphPlus);
            TextMeshProUGUI amount = Label(Center("Amount", pick, new Vector2(0f, -32f), new Vector2(360f, 64f)), "2 eggs", Face.Display, 44f,
                                           HudPalette.Ink).Fit(24f);
            (Button most, Image mostFace, _) = TintButton(pick, "Max", new Vector2(352f, -32f), new Vector2(112f, 68f), "MAX", HudSprites.ButtonGold);
            mostFace.raycastTarget = true;
            Dimming(most, mostFace);

            TextMeshProUGUI summary = Label(Center("Summary", pick, new Vector2(0f, -112f), new Vector2(780f, 64f)), "", Face.Body, 22f,
                                            HudPalette.InkSoft).Fit(15f, wrap: true);

            TextMeshProUGUI reason = Para(p, "Reason", new Vector2(0f, -240f), new Vector2(820f, 40f), Face.Body, 24f, Short);
            (Button feed, Image feedImage, TextMeshProUGUI feedLabel) =
                TintButton(p, "FeedButton", new Vector2(0f, -320f), new Vector2(460f, 104f), "FEED 1 CORN");

            var view = f.root.AddComponent<FeedPopupView>();
            WireFrame(view, f);
            Set(view, "description", description);
            Set(view, "nowIcon", nowIcon);
            Set(view, "status", status);
            Set(view, "bar", bar);
            Set(view, "barTime", barTime);
            Set(view, "stock", stock);
            Set(view, "feedIcon", feedIcon);
            Set(view, "feedCount", feedCount);
            Set(view, "produceIcon", produceIcon);
            Set(view, "produceCount", produceCount);
            Set(view, "amount", amount);
            Set(view, "summary", summary);
            Set(view, "less", less);
            Set(view, "more", more);
            Set(view, "most", most);
            Set(view, "reason", reason);
            Set(view, "feedButton", feed);
            Set(view, "feedButtonImage", feedImage);
            Set(view, "feedButtonLabel", feedLabel);
            Colours(view, Ready, Blocked);
            Set(view, "workingColour", HudPalette.Mint);
            Set(view, "hungryColour", HudPalette.Red);
            Set(view, "asleepColour", HudPalette.Night);
            Set(view, "fullColour", HudPalette.Gold);
            return f.root;
        }

        /// <summary>A round minus or plus button that greys out when it cannot be pressed.</summary>
        internal static Button Stepper(RectTransform parent, string name, Vector2 at, string glyph)
        {
            ButtonParts parts = RoundButton(parent, name, HudSprites.RoundCream, glyph, 92f, at);
            Dimming(parts.button, parts.face);
            return parts.button;
        }

        /// <summary>The popup switches some buttons off and on; a switched-off one fades back.</summary>
        internal static void Dimming(Button button, Graphic face)
        {
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = face;
            ColorBlock colours = ColorBlock.defaultColorBlock;
            colours.normalColor = Color.white;
            colours.highlightedColor = Color.white;
            colours.pressedColor = new Color(0.92f, 0.92f, 0.92f);
            colours.selectedColor = Color.white;
            colours.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            colours.fadeDuration = 0.08f;
            button.colors = colours;
        }
    }
}
