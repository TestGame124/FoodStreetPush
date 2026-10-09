using LastOrders.Livestock;
using LastOrders.UI.Popups;
using LastOrders.UI.World;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The farm buildings' UI, in the look of the game's other UI. Over the building, like the
    /// hammer button and the work timer: the card a tap brings up - the item in a cream disc ringed
    /// in orange, a caption, a dark bar filling with the time on it, how many are waiting, the feed
    /// left - and the bubble that stands over a building with something waiting or nothing to eat.
    /// On screen, like the shelter popup: the feed popup, where the player picks how many eggs or
    /// churns to make and sees the feed it costs. And the icon thrown into the feeder for each item
    /// fed, drawn like the harvest pickups. Rebuilt in place; the popup is put in the UI catalog.
    /// </summary>
    internal static class LivestockUi
    {
        const string Folder = WorldUiPrefabGenerator.Folder;
        internal const string CardPath = Folder + "/WorldUI_ProduceProgress.prefab";
        internal const string BubblePath = Folder + "/WorldUI_ProduceBubble.prefab";
        internal const string FeedPopupPath = Folder + "/UI_FeedPopup.prefab";
        internal const string DropPath = LivestockKit.PrefabFolder + "/FeedDrop.prefab";

        const string PickupFolder = "Assets/Game Assets/Content/LastOrders/Farming/Effects";
        const string OutlineMaterialPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat";

        // As the game's other world UI: a shade over a centimetre per unit.
        const float CanvasScale = 0.014f;

        static readonly Color Panel = new Color(0.12f, 0.12f, 0.14f, 0.86f);
        static readonly Color Track = new Color(0.05f, 0.05f, 0.06f, 0.75f);
        static readonly Color Cream = new Color(1f, 0.97f, 0.9f);
        static readonly Color Orange = new Color(0.95f, 0.62f, 0.2f);
        static readonly Color Pill = new Color(0.12f, 0.12f, 0.14f, 0.9f);
        static readonly Color Badge = new Color(0.85f, 0.32f, 0.25f);

        // The screen popups' colours, as WorldUiPrefabGenerator has them.
        static readonly Color PopupCream = new Color(0.99f, 0.96f, 0.9f);
        static readonly Color PanelText = new Color(0.24f, 0.2f, 0.17f);
        static readonly Color Section = new Color(0.93f, 0.88f, 0.78f);
        static readonly Color Green = new Color(0.32f, 0.7f, 0.3f);
        static readonly Color Red = new Color(0.85f, 0.3f, 0.25f);

        internal static (ProduceProgressView card, ProduceBubbleView bubble, FeedDrop drop) Apply()
        {
            ContentGenerator.EnsureFolder(Folder);
            ContentGenerator.EnsureFolder(LivestockKit.PrefabFolder);
            var card = Save(BuildCard(), CardPath).GetComponent<ProduceProgressView>();
            var bubble = Save(BuildBubble(), BubblePath).GetComponent<ProduceBubbleView>();
            var popup = Save(BuildFeedPopup(), FeedPopupPath).GetComponent<FeedPopupView>();
            var drop = Save(BuildDrop(), DropPath).GetComponent<FeedDrop>();
            Register(popup);
            return (card, bubble, drop);
        }

        /// <summary>Puts the feed popup in the UI catalog, where the buildings look for it.</summary>
        static void Register(FeedPopupView popup)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WorldUiCatalog>(WorldUiPrefabGenerator.CatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning($"[Last Orders] No UI catalog at {WorldUiPrefabGenerator.CatalogPath}; the feed popup is not registered.");
                return;
            }

            if (catalog.feedPopup == popup) return;
            Undo.RecordObject(catalog, "Feed popup");
            catalog.feedPopup = popup;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        static GameObject Save(GameObject root, string path)
        {
            try { return PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { Object.DestroyImmediate(root); }
        }

        // --- The card ------------------------------------------------------------------------------

        static GameObject BuildCard()
        {
            GameObject root = CanvasRoot("WorldUI_ProduceProgress", new Vector2(320f, 120f));
            RectTransform body = Body(root.transform);

            Image panel = Img("Panel", body, Rounded(), Panel, new Vector2(236f, 84f));
            panel.type = Image.Type.Sliced;
            panel.rectTransform.anchoredPosition = new Vector2(34f, 0f);

            // What it gives, in the disc the action buttons use.
            var badge = new GameObject("Badge", typeof(RectTransform)).GetComponent<RectTransform>();
            badge.SetParent(body, false);
            badge.anchoredPosition = new Vector2(-104f, 2f);
            badge.sizeDelta = new Vector2(104f, 104f);

            Image shadow = Img("Shadow", badge, Knob(), new Color(0f, 0f, 0f, 0.35f), new Vector2(104f, 104f));
            shadow.rectTransform.anchoredPosition = new Vector2(4f, -6f);
            Img("Disc", badge, Knob(), Cream, new Vector2(100f, 100f));
            Img("Ring", badge, Knob(), Orange, new Vector2(90f, 90f));
            Img("Inner", badge, Knob(), Cream, new Vector2(78f, 78f));
            Image icon = Img("Icon", badge, null, Color.white, new Vector2(66f, 66f));
            icon.preserveAspect = true;

            // How many are waiting, on a pill under the disc.
            Image pill = Img("StockPill", badge, Rounded(), Pill, new Vector2(64f, 30f));
            pill.type = Image.Type.Sliced;
            pill.rectTransform.anchoredPosition = new Vector2(0f, -50f);
            TextMeshProUGUI stock = Text("Stock", pill.rectTransform, 22f, new Vector2(64f, 30f));
            stock.text = "0/6";

            TextMeshProUGUI caption = Text("Caption", body, 25f, new Vector2(160f, 34f));
            caption.rectTransform.anchoredPosition = new Vector2(-2f, 20f);
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            caption.fontStyle = FontStyles.Bold;
            caption.text = "Next egg";

            // The feed left: its icon and how many.
            Image feed = Img("FeedIcon", body, null, Color.white, new Vector2(36f, 36f));
            feed.preserveAspect = true;
            feed.rectTransform.anchoredPosition = new Vector2(104f, 21f);
            TextMeshProUGUI feedCount = Text("FeedCount", body, 22f, new Vector2(44f, 30f));
            feedCount.rectTransform.anchoredPosition = new Vector2(134f, 19f);
            feedCount.alignment = TextAlignmentOptions.MidlineLeft;
            feedCount.text = "x3";

            Image track = Img("Track", body, Rounded(), Track, new Vector2(216f, 32f));
            track.type = Image.Type.Sliced;
            track.rectTransform.anchoredPosition = new Vector2(34f, -16f);

            Image fill = Img("Fill", track.rectTransform, Rounded(), new Color(0.45f, 0.82f, 0.35f), new Vector2(208f, 24f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0.4f;

            TextMeshProUGUI time = Text("Time", track.rectTransform, 24f, new Vector2(216f, 32f));
            time.fontStyle = FontStyles.Bold;
            time.text = "24s";

            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

            var view = root.AddComponent<ProduceProgressView>();
            view.Configure(body, icon, badge, fill, caption, time, stock, feed, feedCount);
            return root;
        }

        // --- The bubble ----------------------------------------------------------------------------

        static GameObject BuildBubble()
        {
            GameObject root = CanvasRoot("WorldUI_ProduceBubble", new Vector2(140f, 160f));
            RectTransform body = Body(root.transform);

            var bob = new GameObject("Bob", typeof(RectTransform)).GetComponent<RectTransform>();
            bob.SetParent(body, false);
            bob.sizeDelta = new Vector2(140f, 160f);

            // A speech-bubble point under the disc, toward the building.
            Image point = Img("Point", bob, Rounded(), Cream, new Vector2(30f, 30f));
            point.rectTransform.anchoredPosition = new Vector2(0f, -46f);
            point.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            Image shadow = Img("Shadow", bob, Knob(), new Color(0f, 0f, 0f, 0.3f), new Vector2(108f, 108f));
            shadow.rectTransform.anchoredPosition = new Vector2(4f, 2f);
            Image ring = Img("Ring", bob, Knob(), new Color(0.45f, 0.82f, 0.35f), new Vector2(112f, 112f));
            ring.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            Image disc = Img("Disc", bob, Knob(), Cream, new Vector2(94f, 94f));
            disc.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            Image icon = Img("Icon", bob, null, Color.white, new Vector2(74f, 74f));
            icon.preserveAspect = true;
            icon.rectTransform.anchoredPosition = new Vector2(0f, 8f);

            var badge = new GameObject("Badge", typeof(RectTransform)).GetComponent<RectTransform>();
            badge.SetParent(bob, false);
            badge.anchoredPosition = new Vector2(40f, 46f);
            badge.sizeDelta = new Vector2(46f, 46f);
            Img("BadgeRim", badge, Knob(), Cream, new Vector2(46f, 46f));
            Img("BadgeFill", badge, Knob(), Badge, new Vector2(40f, 40f));
            TextMeshProUGUI count = Text("Count", badge, 26f, new Vector2(46f, 46f));
            count.fontStyle = FontStyles.Bold;
            count.text = "2";

            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

            var view = root.AddComponent<ProduceBubbleView>();
            view.Configure(body, bob, icon, ring, badge.gameObject, count);
            return root;
        }

        // --- The feed popup (screen space) -----------------------------------------------------------

        /// <summary>
        /// The shelter popup's panel - orange header, red close disc, green button - with, under the
        /// title, what the building is doing now (the item in its disc, a line, the bar with the time,
        /// how many are waiting) and how many to make: the feed and what it makes side by side, the
        /// number between minus and plus with MAX by them, and how long, the feeder and the storehouse.
        /// </summary>
        static GameObject BuildFeedPopup()
        {
            var root = new GameObject("UI_FeedPopup", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            Stretch(rootRect);
            var group = root.AddComponent<CanvasGroup>();

            Image dim = Img("Backdrop", rootRect, null, new Color(0f, 0f, 0f, 0.55f), Vector2.zero);
            Stretch(dim.rectTransform);
            var backdrop = dim.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;

            Image panel = Img("Panel", rootRect, Rounded(), PopupCream, new Vector2(900f, 800f));
            panel.type = Image.Type.Sliced;
            RectTransform panelRect = panel.rectTransform;

            Image header = Img("Header", panelRect, Rounded(), Orange, new Vector2(900f, 110f));
            header.type = Image.Type.Sliced;
            header.rectTransform.anchoredPosition = new Vector2(0f, 345f);
            header.raycastTarget = false;

            TextMeshProUGUI title = Text("Title", header.rectTransform, 44f, new Vector2(700f, 100f));
            title.rectTransform.anchoredPosition = new Vector2(-30f, 0f);
            title.fontStyle = FontStyles.Bold;
            title.enableAutoSizing = true;
            title.fontSizeMin = 26f;
            title.fontSizeMax = 44f;
            title.text = "Chicken Coop";

            Image closeDisc = Img("Close", panelRect, Knob(), Red, new Vector2(84f, 84f));
            closeDisc.rectTransform.anchoredPosition = new Vector2(412f, 362f);
            var close = closeDisc.gameObject.AddComponent<Button>();
            close.targetGraphic = closeDisc;
            TextMeshProUGUI cross = Text("X", closeDisc.rectTransform, 44f, new Vector2(84f, 84f));
            cross.fontStyle = FontStyles.Bold;
            cross.text = "X";

            TextMeshProUGUI description = Text("Description", panelRect, 26f, new Vector2(800f, 56f), outline: false);
            description.rectTransform.anchoredPosition = new Vector2(0f, 258f);
            description.fontStyle = FontStyles.Italic;
            description.enableAutoSizing = true;
            description.fontSizeMin = 18f;
            description.fontSizeMax = 26f;
            description.text = "Each corn makes 2 eggs, one every 30s of daytime.";

            // What it is doing now.
            Image nowBack = Img("NowBack", panelRect, Rounded(), Section, new Vector2(820f, 128f));
            nowBack.type = Image.Type.Sliced;
            nowBack.raycastTarget = false;
            nowBack.rectTransform.anchoredPosition = new Vector2(0f, 162f);
            RectTransform now = nowBack.rectTransform;

            Image nowIcon = Disc("NowDisc", now, new Vector2(-338f, 0f), 108f);

            TextMeshProUGUI status = Text("Status", now, 28f, new Vector2(470f, 44f), outline: false);
            status.rectTransform.anchoredPosition = new Vector2(-35f, 30f);
            status.alignment = TextAlignmentOptions.MidlineLeft;
            status.fontStyle = FontStyles.Bold;
            status.enableAutoSizing = true;
            status.fontSizeMin = 20f;
            status.fontSizeMax = 28f;
            status.text = "Next egg in";

            Image stockPill = Img("StockPill", now, Rounded(), Pill, new Vector2(110f, 42f));
            stockPill.type = Image.Type.Sliced;
            stockPill.raycastTarget = false;
            stockPill.rectTransform.anchoredPosition = new Vector2(320f, 30f);
            TextMeshProUGUI stock = Text("Stock", stockPill.rectTransform, 26f, new Vector2(110f, 42f));
            stock.fontStyle = FontStyles.Bold;
            stock.text = "0/6";

            Image track = Img("Track", now, Rounded(), Track, new Vector2(640f, 42f));
            track.type = Image.Type.Sliced;
            track.raycastTarget = false;
            track.rectTransform.anchoredPosition = new Vector2(55f, -26f);

            Image bar = Img("Fill", track.rectTransform, Rounded(), new Color(0.45f, 0.82f, 0.35f), new Vector2(630f, 32f));
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.fillOrigin = (int)Image.OriginHorizontal.Left;
            bar.fillAmount = 0.4f;
            bar.raycastTarget = false;

            TextMeshProUGUI barTime = Text("Time", track.rectTransform, 26f, new Vector2(640f, 42f));
            barTime.fontStyle = FontStyles.Bold;
            barTime.text = "24s";

            // How many to make.
            Image pickBack = Img("PickBack", panelRect, Rounded(), Section, new Vector2(820f, 300f));
            pickBack.type = Image.Type.Sliced;
            pickBack.raycastTarget = false;
            pickBack.rectTransform.anchoredPosition = new Vector2(0f, -70f);
            RectTransform pick = pickBack.rectTransform;

            TextMeshProUGUI ask = Text("Ask", pick, 26f, new Vector2(300f, 40f), outline: false);
            ask.rectTransform.anchoredPosition = new Vector2(-250f, 118f);
            ask.alignment = TextAlignmentOptions.MidlineLeft;
            ask.fontStyle = FontStyles.Bold;
            ask.text = "How many?";

            Image feedIcon = Icon("FeedIcon", pick, new Vector2(-150f, 78f), 92f);
            TextMeshProUGUI feedCount = Count("FeedCount", pick, new Vector2(-62f, 70f));
            Chevron(pick, new Vector2(10f, 78f));
            Image produceIcon = Icon("ProduceIcon", pick, new Vector2(110f, 78f), 92f);
            TextMeshProUGUI produceCount = Count("ProduceCount", pick, new Vector2(198f, 70f));

            Button less = Stepper("Less", pick, new Vector2(-230f, -22f), plus: false);
            Button more = Stepper("More", pick, new Vector2(230f, -22f), plus: true);

            TextMeshProUGUI amount = Text("Amount", pick, 42f, new Vector2(360f, 64f), outline: false);
            amount.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            amount.fontStyle = FontStyles.Bold;
            amount.enableAutoSizing = true;
            amount.fontSizeMin = 26f;
            amount.fontSizeMax = 42f;
            amount.text = "2 eggs";

            Image maxPill = Img("Max", pick, Rounded(), Orange, new Vector2(104f, 64f));
            maxPill.type = Image.Type.Sliced;
            maxPill.rectTransform.anchoredPosition = new Vector2(346f, -22f);
            var most = maxPill.gameObject.AddComponent<Button>();
            most.targetGraphic = maxPill;
            TextMeshProUGUI maxLabel = Text("Label", maxPill.rectTransform, 28f, new Vector2(96f, 60f));
            maxLabel.fontStyle = FontStyles.Bold;
            maxLabel.text = "MAX";

            TextMeshProUGUI summary = Text("Summary", pick, 24f, new Vector2(780f, 72f), outline: false);
            summary.rectTransform.anchoredPosition = new Vector2(0f, -106f);
            summary.textWrappingMode = TextWrappingModes.Normal;
            summary.enableAutoSizing = true;
            summary.fontSizeMin = 16f;
            summary.fontSizeMax = 24f;
            summary.lineSpacing = 4f;
            summary.text = "Takes 30s of daytime\nFeeder 0/3  -  Storehouse 5 corn (1 kept for sowing)";

            TextMeshProUGUI reason = Text("Reason", panelRect, 26f, new Vector2(820f, 40f), outline: false);
            reason.rectTransform.anchoredPosition = new Vector2(0f, -248f);
            reason.color = Red;
            reason.enableAutoSizing = true;
            reason.fontSizeMin = 18f;
            reason.fontSizeMax = 26f;
            reason.text = string.Empty;

            Image feedImage = Img("FeedButton", panelRect, Rounded(), Green, new Vector2(440f, 100f));
            feedImage.type = Image.Type.Sliced;
            feedImage.rectTransform.anchoredPosition = new Vector2(0f, -328f);
            var feedButton = feedImage.gameObject.AddComponent<Button>();
            feedButton.targetGraphic = feedImage;
            TextMeshProUGUI feedLabel = Text("Label", feedImage.rectTransform, 38f, new Vector2(420f, 94f));
            feedLabel.fontStyle = FontStyles.Bold;
            feedLabel.enableAutoSizing = true;
            feedLabel.fontSizeMin = 22f;
            feedLabel.fontSizeMax = 38f;
            feedLabel.text = "FEED 1 CORN";

            var view = root.AddComponent<FeedPopupView>();
            Wire(view, ("panel", panelRect), ("group", group), ("backdrop", backdrop), ("closeButton", close),
                 ("title", title), ("description", description),
                 ("nowIcon", nowIcon), ("status", status), ("bar", bar), ("barTime", barTime), ("stock", stock),
                 ("feedIcon", feedIcon), ("feedCount", feedCount), ("produceIcon", produceIcon), ("produceCount", produceCount),
                 ("amount", amount), ("summary", summary), ("less", less), ("more", more), ("most", most),
                 ("reason", reason), ("feedButton", feedButton), ("feedButtonImage", feedImage), ("feedButtonLabel", feedLabel));

            return root;
        }

        /// <summary>The item in the cream disc ringed in orange, as on the card.</summary>
        static Image Disc(string name, RectTransform parent, Vector2 at, float size)
        {
            var badge = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            badge.SetParent(parent, false);
            badge.anchoredPosition = at;
            badge.sizeDelta = new Vector2(size, size);

            Image shadow = Img("Shadow", badge, Knob(), new Color(0f, 0f, 0f, 0.25f), Vector2.one * size);
            shadow.rectTransform.anchoredPosition = new Vector2(3f, -5f);
            Img("Disc", badge, Knob(), Cream, Vector2.one * (size - 4f));
            Img("Ring", badge, Knob(), Orange, Vector2.one * (size - 14f));
            Img("Inner", badge, Knob(), Cream, Vector2.one * (size - 26f));
            Image icon = Img("Icon", badge, null, Color.white, Vector2.one * (size - 36f));
            icon.preserveAspect = true;

            foreach (Graphic graphic in badge.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            return icon;
        }

        static Image Icon(string name, RectTransform parent, Vector2 at, float size)
        {
            Image icon = Img(name, parent, null, Color.white, Vector2.one * size);
            icon.rectTransform.anchoredPosition = at;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return icon;
        }

        static TextMeshProUGUI Count(string name, RectTransform parent, Vector2 at)
        {
            TextMeshProUGUI count = Text(name, parent, 34f, new Vector2(90f, 50f), outline: false);
            count.rectTransform.anchoredPosition = at;
            count.alignment = TextAlignmentOptions.MidlineLeft;
            count.fontStyle = FontStyles.Bold;
            count.text = "x1";
            return count;
        }

        /// <summary>A "&gt;" of two rounded bars: the feed turns into this.</summary>
        static void Chevron(RectTransform parent, Vector2 at)
        {
            var chevron = new GameObject("Chevron", typeof(RectTransform)).GetComponent<RectTransform>();
            chevron.SetParent(parent, false);
            chevron.anchoredPosition = at;
            chevron.sizeDelta = new Vector2(44f, 52f);

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1f : -1f;
                Image bar = Img(i == 0 ? "Upper" : "Lower", chevron, Rounded(), Orange, new Vector2(38f, 12f));
                bar.type = Image.Type.Sliced;
                bar.raycastTarget = false;
                bar.rectTransform.anchoredPosition = new Vector2(0f, 11f * side);
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -40f * side);
            }
        }

        /// <summary>An orange disc with a minus or a plus on it, drawn in bars.</summary>
        static Button Stepper(string name, RectTransform parent, Vector2 at, bool plus)
        {
            Image disc = Img(name, parent, Knob(), Orange, new Vector2(84f, 84f));
            disc.rectTransform.anchoredPosition = at;
            var button = disc.gameObject.AddComponent<Button>();
            button.targetGraphic = disc;

            Image across = Img("Across", disc.rectTransform, Rounded(), Color.white, new Vector2(40f, 11f));
            across.type = Image.Type.Sliced;
            across.raycastTarget = false;

            if (plus)
            {
                Image down = Img("Down", disc.rectTransform, Rounded(), Color.white, new Vector2(11f, 40f));
                down.type = Image.Type.Sliced;
                down.raycastTarget = false;
            }

            return button;
        }

        // --- The feed thrown into the feeder ---------------------------------------------------------

        /// <summary>The harvest pickup's icon and glow, with their materials, on a <see cref="FeedDrop"/>.</summary>
        static GameObject BuildDrop()
        {
            var root = new GameObject("FeedDrop");

            var glowMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{PickupFolder}/Pickup_Glow.mat");
            var iconMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{PickupFolder}/Pickup_Icon.mat");
            var glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PickupFolder}/Pickup_Glow.png");
            if (glowMaterial == null || iconMaterial == null)
                Debug.LogWarning("[Last Orders] The harvest pickup materials are missing (Last Orders/Farming/Build Item Pickups); the feed icons use the default sprite material.");

            var glow = new GameObject("Glow").AddComponent<SpriteRenderer>();
            glow.transform.SetParent(root.transform, false);
            glow.sprite = glowSprite;
            if (glowMaterial != null) glow.sharedMaterial = glowMaterial;
            glow.color = new Color(1f, 0.85f, 0.45f, 0.85f);
            glow.sortingOrder = 0;

            var icon = new GameObject("Icon").AddComponent<SpriteRenderer>();
            icon.transform.SetParent(root.transform, false);
            if (iconMaterial != null) icon.sharedMaterial = iconMaterial;
            icon.sortingOrder = 1;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            root.AddComponent<FeedDrop>().Configure(icon, glow);
            root.SetActive(false);
            return root;
        }

        // --- Pieces ----------------------------------------------------------------------------------

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void Wire(Object target, params (string field, Object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach ((string field, Object value) in fields)
            {
                SerializedProperty property = so.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"[Last Orders] {target.GetType().Name} has no field '{field}' to wire.");
                    continue;
                }

                property.objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject CanvasRoot(string name, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * CanvasScale;
            return root;
        }

        static RectTransform Body(Transform root)
        {
            var rt = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(root, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        static Image Img(string name, RectTransform parent, Sprite sprite, Color colour, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            return image;
        }

        /// <summary>White with an outline, to read over the world; <paramref name="outline"/> off for dark text on a popup panel.</summary>
        static TextMeshProUGUI Text(string name, RectTransform parent, float size, Vector2 box, bool outline = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = box;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = outline ? Color.white : PanelText;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;

            if (!outline) return text;
            var material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (material != null) text.fontSharedMaterial = material;
            return text;
        }

        static Sprite Knob() => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        static Sprite Rounded() => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }
}
