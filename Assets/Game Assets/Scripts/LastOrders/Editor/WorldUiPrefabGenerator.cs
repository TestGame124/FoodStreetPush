using LastOrders.Data;
using LastOrders.UI.Popups;
using LastOrders.UI.World;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Builds the world-space UI prefabs - one per kind of element - and the catalog that
    /// lists them.
    ///
    /// A prefab is created only when it does not exist. These are meant to be restyled by
    /// hand: swap the placeholder hammer for real art, change the colours, reshape the bar.
    /// Regenerating over that work would make the generator something people are afraid to
    /// run.
    ///
    /// Every element is a world-space Canvas at a fixed small scale, so sizes below are in
    /// canvas units of a little over a centimetre each. Only the action prompt has a
    /// GraphicRaycaster; the timer and the floating text must never take a tap.
    /// </summary>
    public static class WorldUiPrefabGenerator
    {
        public const string Folder = "Assets/Game Assets/Content/LastOrders/Prefabs/UI";
        public const string CatalogPath = "Assets/Game Assets/Content/LastOrders/WorldUiCatalog.asset";

        const string PromptPath = Folder + "/WorldUI_HammerPrompt.prefab";
        const string ProgressPath = Folder + "/WorldUI_WorkProgress.prefab";
        const string FloatingPath = Folder + "/WorldUI_FloatingText.prefab";
        const string ExpandPromptPath = Folder + "/WorldUI_ExpandPrompt.prefab";
        const string ExpansionProgressPath = Folder + "/WorldUI_ExpansionProgress.prefab";
        const string ExpansionPopupPath = Folder + "/UI_ExpansionPopup.prefab";
        const string DefensePopupPath = Folder + "/UI_DefensePopup.prefab";
        const string ShelterPopupPath = Folder + "/UI_ShelterPopup.prefab";
        const string HealthBarPath = Folder + "/WorldUI_HealthBar.prefab";
        const string WorkerPanelPath = Folder + "/UI_WorkerPanel.prefab";
        const string RepairPromptPath = Folder + "/WorldUI_RepairPrompt.prefab";
        const string RestaurantPopupPath = Folder + "/UI_RestaurantExpansionPopup.prefab";

        const string OutlineMaterialPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat";

        // A shade over a centimetre per unit: at the level's default zoom the timer then spans
        // about an eighth of a phone screen, which is readable without covering the pile.
        const float CanvasScale = 0.014f;

        /// <summary>Creates whatever is missing and assigns the catalog to the balance asset.</summary>
        public static WorldUiCatalog Ensure(GameBalanceConfig balance)
        {
            ContentGenerator.EnsureFolder(Folder);

            WorldUiCatalog catalog = AssetDatabase.LoadAssetAtPath<WorldUiCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WorldUiCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            if (catalog.actionPrompt == null) catalog.actionPrompt = LoadOrBuild<ActionPromptView>(PromptPath, BuildPrompt);
            if (catalog.workProgress == null) catalog.workProgress = LoadOrBuild<WorkProgressView>(ProgressPath, BuildProgress);
            if (catalog.floatingText == null) catalog.floatingText = LoadOrBuild<FloatingTextView>(FloatingPath, BuildFloatingText);
            if (catalog.expandPrompt == null) catalog.expandPrompt = LoadOrBuild<ActionPromptView>(ExpandPromptPath, BuildExpandPrompt);
            if (catalog.expansionProgress == null) catalog.expansionProgress = LoadOrBuild<WorkProgressView>(ExpansionProgressPath, BuildExpansionProgress);
            if (catalog.expansionPopup == null) catalog.expansionPopup = LoadOrBuild<ExpansionPopupView>(ExpansionPopupPath, BuildExpansionPopup);
            if (catalog.defensePopup == null) catalog.defensePopup = LoadOrBuild<DefensePopupView>(DefensePopupPath, BuildDefensePopup);
            if (catalog.shelterPopup == null) catalog.shelterPopup = LoadOrBuild<ShelterPopupView>(ShelterPopupPath, BuildShelterPopup);
            if (catalog.healthBar == null) catalog.healthBar = LoadOrBuild<HealthBarView>(HealthBarPath, BuildHealthBar);
            if (catalog.workerPanel == null) catalog.workerPanel = LoadOrBuild<WorkerPanelView>(WorkerPanelPath, BuildWorkerPanel);
            if (catalog.repairPrompt == null) catalog.repairPrompt = LoadOrBuild<ActionPromptView>(RepairPromptPath, BuildRepairPrompt);
            if (catalog.restaurantExpansionPopup == null)
                catalog.restaurantExpansionPopup = LoadOrBuild<RestaurantExpansionPopupView>(RestaurantPopupPath, BuildRestaurantPopup);

            EditorUtility.SetDirty(catalog);

            if (balance != null && balance.worldUi == null)
            {
                balance.worldUi = catalog;
                EditorUtility.SetDirty(balance);
            }

            AssetDatabase.SaveAssets();
            return catalog;
        }

        static T LoadOrBuild<T>(string path, System.Func<GameObject> build) where T : Component
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<T>();

            GameObject root = build();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            Debug.Log($"[Last Orders] Created world UI prefab {path}.");
            return prefab.GetComponent<T>();
        }

        // --- The hammer button ----------------------------------------------------------------

        static GameObject BuildPrompt()
        {
            GameObject root = CanvasRoot("WorldUI_HammerPrompt", new Vector2(120f, 120f), raycaster: true);
            RectTransform body = Body(root.transform);

            // A soft shadow under the disc, then the disc itself - which is the button.
            Image shadow = Img("Shadow", body, Knob(), new Color(0f, 0f, 0f, 0.35f), new Vector2(112f, 112f));
            shadow.rectTransform.anchoredPosition = new Vector2(4f, -6f);
            shadow.raycastTarget = false;

            Image disc = Img("Button", body, Knob(), new Color(1f, 0.97f, 0.9f), new Vector2(108f, 108f));
            var button = disc.gameObject.AddComponent<Button>();
            button.targetGraphic = disc;

            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(1f, 1f, 0.85f);
            colours.pressedColor = new Color(0.85f, 0.8f, 0.7f);
            button.colors = colours;

            Image ring = Img("Ring", disc.rectTransform, Knob(), new Color(0.95f, 0.62f, 0.2f), new Vector2(96f, 96f));
            ring.raycastTarget = false;

            Image inner = Img("Inner", disc.rectTransform, Knob(), new Color(1f, 0.97f, 0.9f), new Vector2(84f, 84f));
            inner.raycastTarget = false;

            // The icon slot: empty until a definition or an artist gives it a sprite.
            Image icon = Img("Icon", disc.rectTransform, null, Color.white, new Vector2(64f, 64f));
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.enabled = false;

            GameObject hammer = PlaceholderHammer(disc.rectTransform);

            var view = root.AddComponent<ActionPromptView>();
            Wire(view, ("body", body), ("button", button), ("icon", icon), ("placeholderIcon", hammer));

            return root;
        }

        /// <summary>A hammer drawn from three rectangles. Replace with a sprite on the Icon image.</summary>
        static GameObject PlaceholderHammer(RectTransform parent)
        {
            var group = new GameObject("PlaceholderHammer", typeof(RectTransform));
            var rt = (RectTransform)group.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(64f, 64f);
            rt.localRotation = Quaternion.Euler(0f, 0f, 35f);

            Image handle = Img("Handle", rt, null, new Color(0.55f, 0.35f, 0.18f), new Vector2(11f, 56f));
            handle.rectTransform.anchoredPosition = new Vector2(0f, -6f);

            Image head = Img("Head", rt, null, new Color(0.36f, 0.39f, 0.44f), new Vector2(40f, 17f));
            head.rectTransform.anchoredPosition = new Vector2(0f, 24f);

            Image face = Img("Face", rt, null, new Color(0.62f, 0.66f, 0.7f), new Vector2(10f, 17f));
            face.rectTransform.anchoredPosition = new Vector2(-15f, 24f);

            foreach (Image image in group.GetComponentsInChildren<Image>()) image.raycastTarget = false;

            return group;
        }

        // --- The work timer -------------------------------------------------------------------

        static GameObject BuildProgress()
        {
            GameObject root = CanvasRoot("WorldUI_WorkProgress", new Vector2(170f, 44f), raycaster: false);
            RectTransform body = Body(root.transform);

            Image back = Img("Background", body, Rounded(), new Color(0.12f, 0.12f, 0.14f, 0.85f), new Vector2(170f, 38f));
            back.type = Image.Type.Sliced;

            Image fill = Img("Fill", back.rectTransform, Rounded(), new Color(0.45f, 0.85f, 0.35f), new Vector2(160f, 28f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;

            TextMeshProUGUI label = Text("Label", body, 26f, new Vector2(170f, 38f));
            label.text = "...";

            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

            var view = root.AddComponent<WorkProgressView>();
            Wire(view, ("body", body), ("fill", fill), ("label", label));

            return root;
        }

        // --- The floating "+2 XP" --------------------------------------------------------------

        static GameObject BuildFloatingText()
        {
            GameObject root = CanvasRoot("WorldUI_FloatingText", new Vector2(360f, 80f), raycaster: false);
            RectTransform body = Body(root.transform);
            var group = body.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            TextMeshProUGUI label = Text("Label", body, 44f, new Vector2(360f, 80f));
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;

            var view = root.AddComponent<FloatingTextView>();
            Wire(view, ("body", body), ("label", label), ("group", group));

            return root;
        }

        // --- The expand button over cleared land -------------------------------------------------

        static GameObject BuildExpandPrompt()
        {
            GameObject root = CanvasRoot("WorldUI_ExpandPrompt", new Vector2(160f, 200f), raycaster: true);
            RectTransform body = Body(root.transform);

            Image shadow = Img("Shadow", body, Knob(), new Color(0f, 0f, 0f, 0.35f), new Vector2(142f, 142f));
            shadow.rectTransform.anchoredPosition = new Vector2(4f, 14f);
            shadow.raycastTarget = false;

            Image disc = Img("Button", body, Knob(), new Color(0.26f, 0.6f, 0.26f), new Vector2(136f, 136f));
            disc.rectTransform.anchoredPosition = new Vector2(0f, 20f);
            var button = disc.gameObject.AddComponent<Button>();
            button.targetGraphic = disc;

            Image inner = Img("Inner", disc.rectTransform, Knob(), Green, new Vector2(118f, 118f));
            inner.raycastTarget = false;

            Image icon = Img("Icon", disc.rectTransform, null, Color.white, new Vector2(80f, 80f));
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.enabled = false;

            GameObject glyph = ExpandGlyph(disc.rectTransform);

            TextMeshProUGUI caption = Text("Caption", body, 30f, new Vector2(220f, 44f));
            caption.rectTransform.anchoredPosition = new Vector2(0f, -70f);
            caption.fontStyle = FontStyles.Bold;
            caption.text = "EXPAND";

            var view = root.AddComponent<ActionPromptView>();
            Wire(view, ("body", body), ("button", button), ("icon", icon), ("placeholderIcon", glyph),
                 ("pulse", disc.rectTransform));

            // A standing button: it stays while the popup it opens is up, and until the build starts.
            SetBool(view, "dismissOnWorldTap", false);
            SetBool(view, "releaseOnPress", false);

            return root;
        }

        /// <summary>Four corner brackets round a plus: the usual "make bigger" mark, from rectangles.</summary>
        static GameObject ExpandGlyph(RectTransform parent)
        {
            var group = new GameObject("PlaceholderExpand", typeof(RectTransform));
            var rt = (RectTransform)group.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(80f, 80f);

            const float half = 36f, arm = 26f, thick = 9f;

            foreach (Vector2 corner in new[] { new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f) })
            {
                Image across = Img("Across", rt, null, Color.white, new Vector2(arm, thick));
                across.rectTransform.anchoredPosition = new Vector2(corner.x * (half - arm * 0.5f), corner.y * (half - thick * 0.5f));

                Image down = Img("Down", rt, null, Color.white, new Vector2(thick, arm));
                down.rectTransform.anchoredPosition = new Vector2(corner.x * (half - thick * 0.5f), corner.y * (half - arm * 0.5f));
            }

            Img("PlusAcross", rt, null, Color.white, new Vector2(30f, 8f));
            Img("PlusDown", rt, null, Color.white, new Vector2(8f, 30f));

            foreach (Image image in group.GetComponentsInChildren<Image>()) image.raycastTarget = false;
            return group;
        }

        // --- The wrench over anything broken ------------------------------------------------------

        /// <summary>
        /// A standing button: it stays over the broken thing until it is pressed or the thing is
        /// fixed, however many other taps come and go - unlike the hammer, which a tap elsewhere
        /// closes. Pressing it sends the worker, so it goes away then.
        /// </summary>
        static GameObject BuildRepairPrompt()
        {
            GameObject root = CanvasRoot("WorldUI_RepairPrompt", new Vector2(140f, 180f), raycaster: true);
            RectTransform body = Body(root.transform);

            Image shadow = Img("Shadow", body, Knob(), new Color(0f, 0f, 0f, 0.35f), new Vector2(112f, 112f));
            shadow.rectTransform.anchoredPosition = new Vector2(4f, 14f);
            shadow.raycastTarget = false;

            Image disc = Img("Button", body, Knob(), new Color(1f, 0.97f, 0.9f), new Vector2(108f, 108f));
            disc.rectTransform.anchoredPosition = new Vector2(0f, 20f);
            var button = disc.gameObject.AddComponent<Button>();
            button.targetGraphic = disc;

            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(1f, 1f, 0.85f);
            colours.pressedColor = new Color(0.85f, 0.8f, 0.7f);
            button.colors = colours;

            Image ring = Img("Ring", disc.rectTransform, Knob(), Red, new Vector2(96f, 96f));
            ring.raycastTarget = false;

            Image inner = Img("Inner", disc.rectTransform, Knob(), new Color(1f, 0.97f, 0.9f), new Vector2(84f, 84f));
            inner.raycastTarget = false;

            Image icon = Img("Icon", disc.rectTransform, null, Color.white, new Vector2(64f, 64f));
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.enabled = false;

            GameObject wrench = PlaceholderWrench(disc.rectTransform, new Color(1f, 0.97f, 0.9f));

            TextMeshProUGUI caption = Text("Caption", body, 28f, new Vector2(200f, 40f));
            caption.rectTransform.anchoredPosition = new Vector2(0f, -58f);
            caption.fontStyle = FontStyles.Bold;
            caption.text = "REPAIR";

            var view = root.AddComponent<ActionPromptView>();
            Wire(view, ("body", body), ("button", button), ("icon", icon), ("placeholderIcon", wrench),
                 ("pulse", disc.rectTransform));

            SetBool(view, "dismissOnWorldTap", false);
            SetBool(view, "releaseOnPress", true);

            return root;
        }

        /// <summary>A spanner drawn from a handle and an open jaw. Replace with a sprite on the Icon image.</summary>
        static GameObject PlaceholderWrench(RectTransform parent, Color cutout)
        {
            var group = new GameObject("PlaceholderWrench", typeof(RectTransform));
            var rt = (RectTransform)group.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(64f, 64f);
            rt.localRotation = Quaternion.Euler(0f, 0f, -40f);

            var steel = new Color(0.36f, 0.39f, 0.44f);

            Image handle = Img("Handle", rt, null, steel, new Vector2(12f, 46f));
            handle.rectTransform.anchoredPosition = new Vector2(0f, -8f);

            Image head = Img("Head", rt, Knob(), steel, new Vector2(34f, 34f));
            head.rectTransform.anchoredPosition = new Vector2(0f, 20f);

            // The open jaw, cut out of the head in the button's own colour.
            Image jaw = Img("Jaw", rt, null, cutout, new Vector2(12f, 18f));
            jaw.rectTransform.anchoredPosition = new Vector2(0f, 30f);

            Image end = Img("End", rt, Knob(), steel, new Vector2(18f, 18f));
            end.rectTransform.anchoredPosition = new Vector2(0f, -31f);

            foreach (Image image in group.GetComponentsInChildren<Image>()) image.raycastTarget = false;

            return group;
        }

        // --- "Expand the restaurant?" (screen space) -----------------------------------------------

        /// <summary>
        /// The expand popup's panel, with the build time swapped for a line on how many times each
        /// side can still grow, and the one button for two: yes, which lights up the ground to pick
        /// from, and no.
        /// </summary>
        static GameObject BuildRestaurantPopup()
        {
            var root = new GameObject("UI_RestaurantExpansionPopup", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            Stretch(rootRect);
            var group = root.AddComponent<CanvasGroup>();

            Image dim = Img("Backdrop", rootRect, null, new Color(0f, 0f, 0f, 0.55f), Vector2.zero);
            Stretch(dim.rectTransform);
            var backdrop = dim.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;

            Image panel = Img("Panel", rootRect, Rounded(), Cream, new Vector2(820f, 740f));
            panel.type = Image.Type.Sliced;
            RectTransform panelRect = panel.rectTransform;

            Image header = Img("Header", panelRect, Rounded(), Orange, new Vector2(820f, 110f));
            header.type = Image.Type.Sliced;
            header.rectTransform.anchoredPosition = new Vector2(0f, 315f);
            header.raycastTarget = false;

            TextMeshProUGUI title = Text("Title", header.rectTransform, 44f, new Vector2(620f, 100f));
            title.rectTransform.anchoredPosition = new Vector2(-30f, 0f);
            title.fontStyle = FontStyles.Bold;
            title.enableAutoSizing = true;
            title.fontSizeMin = 26f;
            title.fontSizeMax = 44f;
            title.text = "Expand the restaurant?";

            Image closeDisc = Img("Close", panelRect, Knob(), Red, new Vector2(84f, 84f));
            closeDisc.rectTransform.anchoredPosition = new Vector2(372f, 332f);
            var close = closeDisc.gameObject.AddComponent<Button>();
            close.targetGraphic = closeDisc;

            TextMeshProUGUI cross = Text("X", closeDisc.rectTransform, 44f, new Vector2(84f, 84f));
            cross.fontStyle = FontStyles.Bold;
            cross.text = "X";

            TextMeshProUGUI body = Text("Body", panelRect, 28f, new Vector2(740f, 130f), outline: false);
            body.rectTransform.anchoredPosition = new Vector2(0f, 190f);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.enableAutoSizing = true;
            body.fontSizeMin = 20f;
            body.fontSizeMax = 28f;

            TextMeshProUGUI caption = Text("RequirementsCaption", panelRect, 30f, new Vector2(660f, 44f), outline: false);
            caption.rectTransform.anchoredPosition = new Vector2(0f, 100f);
            caption.fontStyle = FontStyles.Bold;
            caption.alignment = TextAlignmentOptions.Left;
            caption.text = "Needs";

            var rows = new RequirementRowView[4];
            for (int i = 0; i < rows.Length; i++) rows[i] = Row(panelRect, i, new Vector2(0f, 44f - i * 66f));

            TextMeshProUGUI reason = Text("Reason", panelRect, 28f, new Vector2(740f, 40f), outline: false);
            reason.rectTransform.anchoredPosition = new Vector2(0f, -218f);
            reason.color = Red;
            reason.enableAutoSizing = true;
            reason.fontSizeMin = 18f;
            reason.fontSizeMax = 28f;
            reason.text = string.Empty;

            (Button yes, Image yesImage, TextMeshProUGUI _) =
                PayButton("YesButton", panelRect, new Vector2(-180f, -300f), Green, "YES", new Vector2(320f, 96f));

            (Button no, Image _, TextMeshProUGUI _) =
                PayButton("NoButton", panelRect, new Vector2(180f, -300f), Red, "NO", new Vector2(320f, 96f));

            var view = root.AddComponent<RestaurantExpansionPopupView>();
            Wire(view, ("panel", panelRect), ("group", group), ("backdrop", backdrop), ("closeButton", close),
                 ("title", title), ("body", body), ("reason", reason),
                 ("yesButton", yes), ("yesButtonImage", yesImage), ("noButton", no));
            WireArray(view, "rows", rows);

            return root;
        }

        // --- The build timer over expanding land -------------------------------------------------

        static GameObject BuildExpansionProgress()
        {
            GameObject root = CanvasRoot("WorldUI_ExpansionProgress", new Vector2(320f, 110f), raycaster: false);
            RectTransform body = Body(root.transform);

            TextMeshProUGUI caption = Text("Caption", body, 32f, new Vector2(320f, 44f));
            caption.rectTransform.anchoredPosition = new Vector2(0f, 32f);
            caption.fontStyle = FontStyles.Bold;
            caption.text = "EXPANDING";

            Image back = Img("Background", body, Rounded(), new Color(0.12f, 0.12f, 0.14f, 0.85f), new Vector2(320f, 54f));
            back.type = Image.Type.Sliced;
            back.rectTransform.anchoredPosition = new Vector2(0f, -14f);

            Image fill = Img("Fill", back.rectTransform, Rounded(), Orange, new Vector2(308f, 42f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;

            TextMeshProUGUI label = Text("Label", body, 32f, new Vector2(320f, 54f));
            label.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            label.text = "...";

            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

            var view = root.AddComponent<WorkProgressView>();
            Wire(view, ("body", body), ("fill", fill), ("label", label));
            SetColour(view, "workingColour", Orange);

            return root;
        }

        // --- The expand popup (screen space) ----------------------------------------------------

        /// <summary>
        /// Laid out for the HUD's 1920x1080 reference, centred. Not a canvas of its own: the
        /// popup service parents it under the popup canvas, which does the scaling.
        /// </summary>
        static GameObject BuildExpansionPopup()
        {
            var root = new GameObject("UI_ExpansionPopup", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            Stretch(rootRect);
            var group = root.AddComponent<CanvasGroup>();

            // The dim behind the panel is what makes the popup modal, and tapping it closes it.
            Image dim = Img("Backdrop", rootRect, null, new Color(0f, 0f, 0f, 0.55f), Vector2.zero);
            Stretch(dim.rectTransform);
            var backdrop = dim.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;

            Image panel = Img("Panel", rootRect, Rounded(), Cream, new Vector2(820f, 700f));
            panel.type = Image.Type.Sliced;
            RectTransform panelRect = panel.rectTransform;

            Image header = Img("Header", panelRect, Rounded(), Orange, new Vector2(820f, 110f));
            header.type = Image.Type.Sliced;
            header.rectTransform.anchoredPosition = new Vector2(0f, 295f);
            header.raycastTarget = false;

            // Narrower than the header and nudged left, clear of the close button; shrinks to fit
            // a long area name rather than running under it.
            TextMeshProUGUI title = Text("Title", header.rectTransform, 44f, new Vector2(620f, 100f));
            title.rectTransform.anchoredPosition = new Vector2(-30f, 0f);
            title.fontStyle = FontStyles.Bold;
            title.enableAutoSizing = true;
            title.fontSizeMin = 26f;
            title.fontSizeMax = 44f;
            title.text = "Expand";

            Image closeDisc = Img("Close", panelRect, Knob(), Red, new Vector2(84f, 84f));
            closeDisc.rectTransform.anchoredPosition = new Vector2(372f, 312f);
            var close = closeDisc.gameObject.AddComponent<Button>();
            close.targetGraphic = closeDisc;

            TextMeshProUGUI cross = Text("X", closeDisc.rectTransform, 44f, new Vector2(84f, 84f));
            cross.fontStyle = FontStyles.Bold;
            cross.text = "X";

            TextMeshProUGUI benefit = Text("Benefit", panelRect, 32f, new Vector2(740f, 60f), outline: false);
            benefit.rectTransform.anchoredPosition = new Vector2(0f, 195f);
            benefit.fontStyle = FontStyles.Italic;

            TextMeshProUGUI caption = Text("RequirementsCaption", panelRect, 30f, new Vector2(660f, 44f), outline: false);
            caption.rectTransform.anchoredPosition = new Vector2(0f, 128f);
            caption.fontStyle = FontStyles.Bold;
            caption.alignment = TextAlignmentOptions.Left;
            caption.text = "Needs";

            var rows = new RequirementRowView[4];
            for (int i = 0; i < rows.Length; i++) rows[i] = Row(panelRect, i, new Vector2(0f, 70f - i * 66f));

            TextMeshProUGUI time = Text("BuildTime", panelRect, 30f, new Vector2(660f, 44f), outline: false);
            time.rectTransform.anchoredPosition = new Vector2(0f, -196f);
            time.text = "Build time: 12s";

            TextMeshProUGUI reason = Text("Reason", panelRect, 28f, new Vector2(740f, 40f), outline: false);
            reason.rectTransform.anchoredPosition = new Vector2(0f, -238f);
            reason.color = Red;
            reason.text = string.Empty;

            Image expandImage = Img("ExpandButton", panelRect, Rounded(), Green, new Vector2(380f, 96f));
            expandImage.type = Image.Type.Sliced;
            expandImage.rectTransform.anchoredPosition = new Vector2(0f, -294f);
            var expand = expandImage.gameObject.AddComponent<Button>();
            expand.targetGraphic = expandImage;

            TextMeshProUGUI expandLabel = Text("Label", expandImage.rectTransform, 44f, new Vector2(380f, 96f));
            expandLabel.fontStyle = FontStyles.Bold;
            expandLabel.text = "EXPAND";

            var view = root.AddComponent<ExpansionPopupView>();
            Wire(view, ("panel", panelRect), ("group", group), ("backdrop", backdrop), ("closeButton", close),
                 ("title", title), ("benefit", benefit), ("buildTime", time), ("reason", reason),
                 ("expandButton", expand), ("expandButtonImage", expandImage));
            WireArray(view, "rows", rows);

            return root;
        }

        // --- The defense popup (screen space) ----------------------------------------------------

        /// <summary>
        /// The same panel as the expand popup, with the requirement rows swapped for a block of
        /// detail lines and the one button for two: pay from the stores, or pay premium.
        /// </summary>
        static GameObject BuildDefensePopup()
        {
            var root = new GameObject("UI_DefensePopup", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            Stretch(rootRect);
            var group = root.AddComponent<CanvasGroup>();

            Image dim = Img("Backdrop", rootRect, null, new Color(0f, 0f, 0f, 0.55f), Vector2.zero);
            Stretch(dim.rectTransform);
            var backdrop = dim.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;

            Image panel = Img("Panel", rootRect, Rounded(), Cream, new Vector2(860f, 720f));
            panel.type = Image.Type.Sliced;
            RectTransform panelRect = panel.rectTransform;

            Image header = Img("Header", panelRect, Rounded(), Orange, new Vector2(860f, 110f));
            header.type = Image.Type.Sliced;
            header.rectTransform.anchoredPosition = new Vector2(0f, 305f);
            header.raycastTarget = false;

            TextMeshProUGUI title = Text("Title", header.rectTransform, 44f, new Vector2(660f, 100f));
            title.rectTransform.anchoredPosition = new Vector2(-30f, 0f);
            title.fontStyle = FontStyles.Bold;
            title.enableAutoSizing = true;
            title.fontSizeMin = 26f;
            title.fontSizeMax = 44f;
            title.text = "Guard Tower";

            Image closeDisc = Img("Close", panelRect, Knob(), Red, new Vector2(84f, 84f));
            closeDisc.rectTransform.anchoredPosition = new Vector2(392f, 322f);
            var close = closeDisc.gameObject.AddComponent<Button>();
            close.targetGraphic = closeDisc;

            TextMeshProUGUI cross = Text("X", closeDisc.rectTransform, 44f, new Vector2(84f, 84f));
            cross.fontStyle = FontStyles.Bold;
            cross.text = "X";

            TextMeshProUGUI description = Text("Description", panelRect, 28f, new Vector2(760f, 84f), outline: false);
            description.rectTransform.anchoredPosition = new Vector2(0f, 198f);
            description.fontStyle = FontStyles.Italic;
            description.textWrappingMode = TextWrappingModes.Normal;

            TextMeshProUGUI status = Text("Status", panelRect, 30f, new Vector2(760f, 44f), outline: false);
            status.rectTransform.anchoredPosition = new Vector2(0f, 132f);
            status.fontStyle = FontStyles.Bold;

            Image detailBack = Img("DetailsBack", panelRect, Rounded(), new Color(0.93f, 0.88f, 0.78f), new Vector2(740f, 230f));
            detailBack.type = Image.Type.Sliced;
            detailBack.raycastTarget = false;
            detailBack.rectTransform.anchoredPosition = new Vector2(0f, -8f);

            TextMeshProUGUI details = Text("Details", detailBack.rectTransform, 29f, new Vector2(690f, 210f), outline: false);
            details.alignment = TextAlignmentOptions.MidlineLeft;
            details.textWrappingMode = TextWrappingModes.Normal;
            details.lineSpacing = 8f;

            TextMeshProUGUI reason = Text("Reason", panelRect, 26f, new Vector2(780f, 40f), outline: false);
            reason.rectTransform.anchoredPosition = new Vector2(0f, -160f);
            reason.color = Red;
            reason.enableAutoSizing = true;
            reason.fontSizeMin = 18f;
            reason.fontSizeMax = 26f;
            reason.text = string.Empty;

            (Button stores, Image storesImage, TextMeshProUGUI storesLabel) =
                PayButton("PayResources", panelRect, new Vector2(-200f, -262f), Green, "60 Trade Tokens");
            (Button gems, Image gemsImage, TextMeshProUGUI gemsLabel) =
                PayButton("PayPremium", panelRect, new Vector2(200f, -262f), new Color(0.55f, 0.36f, 0.85f), "20 Gems");

            var view = root.AddComponent<DefensePopupView>();
            Wire(view, ("panel", panelRect), ("group", group), ("backdrop", backdrop), ("closeButton", close),
                 ("title", title), ("description", description), ("status", status), ("details", details),
                 ("reason", reason),
                 ("resourcesButton", stores), ("resourcesButtonImage", storesImage), ("resourcesLabel", storesLabel),
                 ("premiumButton", gems), ("premiumButtonImage", gemsImage), ("premiumLabel", gemsLabel));

            return root;
        }

        // --- The shelter popup (screen space) ----------------------------------------------------

        /// <summary>
        /// The defense popup's panel with a row of three option buttons in place of its two: one
        /// BUILD button over an empty site, and a hire button per role in a built shelter. The
        /// view centres however many the offer uses.
        /// </summary>
        static GameObject BuildShelterPopup()
        {
            var root = new GameObject("UI_ShelterPopup", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            Stretch(rootRect);
            var group = root.AddComponent<CanvasGroup>();

            Image dim = Img("Backdrop", rootRect, null, new Color(0f, 0f, 0f, 0.55f), Vector2.zero);
            Stretch(dim.rectTransform);
            var backdrop = dim.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;

            Image panel = Img("Panel", rootRect, Rounded(), Cream, new Vector2(900f, 740f));
            panel.type = Image.Type.Sliced;
            RectTransform panelRect = panel.rectTransform;

            Image header = Img("Header", panelRect, Rounded(), Orange, new Vector2(900f, 110f));
            header.type = Image.Type.Sliced;
            header.rectTransform.anchoredPosition = new Vector2(0f, 315f);
            header.raycastTarget = false;

            TextMeshProUGUI title = Text("Title", header.rectTransform, 44f, new Vector2(700f, 100f));
            title.rectTransform.anchoredPosition = new Vector2(-30f, 0f);
            title.fontStyle = FontStyles.Bold;
            title.enableAutoSizing = true;
            title.fontSizeMin = 26f;
            title.fontSizeMax = 44f;
            title.text = "Shelter";

            Image closeDisc = Img("Close", panelRect, Knob(), Red, new Vector2(84f, 84f));
            closeDisc.rectTransform.anchoredPosition = new Vector2(412f, 332f);
            var close = closeDisc.gameObject.AddComponent<Button>();
            close.targetGraphic = closeDisc;

            TextMeshProUGUI cross = Text("X", closeDisc.rectTransform, 44f, new Vector2(84f, 84f));
            cross.fontStyle = FontStyles.Bold;
            cross.text = "X";

            TextMeshProUGUI description = Text("Description", panelRect, 28f, new Vector2(800f, 84f), outline: false);
            description.rectTransform.anchoredPosition = new Vector2(0f, 208f);
            description.fontStyle = FontStyles.Italic;
            description.textWrappingMode = TextWrappingModes.Normal;

            TextMeshProUGUI status = Text("Status", panelRect, 30f, new Vector2(800f, 44f), outline: false);
            status.rectTransform.anchoredPosition = new Vector2(0f, 142f);
            status.fontStyle = FontStyles.Bold;

            Image detailBack = Img("DetailsBack", panelRect, Rounded(), new Color(0.93f, 0.88f, 0.78f), new Vector2(800f, 240f));
            detailBack.type = Image.Type.Sliced;
            detailBack.raycastTarget = false;
            detailBack.rectTransform.anchoredPosition = new Vector2(0f, 0f);

            TextMeshProUGUI details = Text("Details", detailBack.rectTransform, 28f, new Vector2(750f, 220f), outline: false);
            details.alignment = TextAlignmentOptions.MidlineLeft;
            details.textWrappingMode = TextWrappingModes.Normal;
            details.enableAutoSizing = true;
            details.fontSizeMin = 20f;
            details.fontSizeMax = 28f;
            details.lineSpacing = 6f;

            TextMeshProUGUI reason = Text("Reason", panelRect, 26f, new Vector2(820f, 40f), outline: false);
            reason.rectTransform.anchoredPosition = new Vector2(0f, -158f);
            reason.color = Red;
            reason.enableAutoSizing = true;
            reason.fontSizeMin = 18f;
            reason.fontSizeMax = 26f;
            reason.text = string.Empty;

            var buttons = new Button[3];
            var images = new Image[3];
            var labels = new TextMeshProUGUI[3];

            for (int i = 0; i < buttons.Length; i++)
            {
                (buttons[i], images[i], labels[i]) =
                    PayButton($"Option_{i}", panelRect, new Vector2((i - 1) * 280f, -266f), Green, "Hire", new Vector2(260f, 104f));
            }

            var view = root.AddComponent<ShelterPopupView>();
            Wire(view, ("panel", panelRect), ("group", group), ("backdrop", backdrop), ("closeButton", close),
                 ("title", title), ("description", description), ("status", status), ("details", details),
                 ("reason", reason));
            WireArray(view, "optionButtons", buttons);
            WireArray(view, "optionImages", images);
            WireArray(view, "optionLabels", labels);

            return root;
        }

        // --- The health bar over a worker ---------------------------------------------------------

        static GameObject BuildHealthBar()
        {
            GameObject root = CanvasRoot("WorldUI_HealthBar", new Vector2(150f, 34f), raycaster: false);
            RectTransform body = Body(root.transform);

            var group = body.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            Image back = Img("Background", body, Rounded(), new Color(0.1f, 0.1f, 0.12f, 0.88f), new Vector2(150f, 26f));
            back.type = Image.Type.Sliced;

            Image fill = Img("Fill", back.rectTransform, Rounded(), Green, new Vector2(142f, 18f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            TextMeshProUGUI label = Text("Label", body, 18f, new Vector2(150f, 26f));
            label.fontStyle = FontStyles.Bold;

            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

            var view = root.AddComponent<HealthBarView>();
            Wire(view, ("body", body), ("fill", fill), ("label", label), ("group", group));

            // Up at once: a bar that grows in over a fight is a bar that shows the wrong number.
            var so = new SerializedObject(view);
            so.FindProperty("popInSeconds").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // --- The worker panel (screen space) ------------------------------------------------------

        /// <summary>
        /// The shelter popup's panel, taller, with a health bar under the name and a red Recall
        /// Worker button under the row of options.
        /// </summary>
        static GameObject BuildWorkerPanel()
        {
            var root = new GameObject("UI_WorkerPanel", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            Stretch(rootRect);
            var group = root.AddComponent<CanvasGroup>();

            Image dim = Img("Backdrop", rootRect, null, new Color(0f, 0f, 0f, 0.55f), Vector2.zero);
            Stretch(dim.rectTransform);
            var backdrop = dim.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;

            Image panel = Img("Panel", rootRect, Rounded(), Cream, new Vector2(900f, 840f));
            panel.type = Image.Type.Sliced;
            RectTransform panelRect = panel.rectTransform;

            Image header = Img("Header", panelRect, Rounded(), Orange, new Vector2(900f, 110f));
            header.type = Image.Type.Sliced;
            header.rectTransform.anchoredPosition = new Vector2(0f, 365f);
            header.raycastTarget = false;

            TextMeshProUGUI title = Text("Title", header.rectTransform, 44f, new Vector2(700f, 100f));
            title.rectTransform.anchoredPosition = new Vector2(-30f, 0f);
            title.fontStyle = FontStyles.Bold;
            title.enableAutoSizing = true;
            title.fontSizeMin = 26f;
            title.fontSizeMax = 44f;
            title.text = "Worker";

            Image closeDisc = Img("Close", panelRect, Knob(), Red, new Vector2(84f, 84f));
            closeDisc.rectTransform.anchoredPosition = new Vector2(412f, 382f);
            var close = closeDisc.gameObject.AddComponent<Button>();
            close.targetGraphic = closeDisc;

            TextMeshProUGUI cross = Text("X", closeDisc.rectTransform, 44f, new Vector2(84f, 84f));
            cross.fontStyle = FontStyles.Bold;
            cross.text = "X";

            TextMeshProUGUI subtitle = Text("Subtitle", panelRect, 28f, new Vector2(800f, 44f), outline: false);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, 276f);
            subtitle.fontStyle = FontStyles.Italic;
            subtitle.enableAutoSizing = true;
            subtitle.fontSizeMin = 20f;
            subtitle.fontSizeMax = 28f;

            // The health bar, with its numbers written across it.
            var healthRoot = new GameObject("Health", typeof(RectTransform));
            var healthRect = (RectTransform)healthRoot.transform;
            healthRect.SetParent(panelRect, false);
            healthRect.sizeDelta = new Vector2(640f, 52f);
            healthRect.anchoredPosition = new Vector2(0f, 218f);

            Image healthBack = Img("Background", healthRect, Rounded(), new Color(0.25f, 0.22f, 0.2f), new Vector2(640f, 52f));
            healthBack.type = Image.Type.Sliced;
            healthBack.raycastTarget = false;

            Image healthFill = Img("Fill", healthBack.rectTransform, Rounded(), Green, new Vector2(626f, 38f));
            healthFill.type = Image.Type.Filled;
            healthFill.fillMethod = Image.FillMethod.Horizontal;
            healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            healthFill.fillAmount = 1f;
            healthFill.raycastTarget = false;

            TextMeshProUGUI healthLabel = Text("Label", healthRect, 28f, new Vector2(620f, 48f));
            healthLabel.fontStyle = FontStyles.Bold;
            healthLabel.text = "100/100 HP";

            TextMeshProUGUI status = Text("Status", panelRect, 30f, new Vector2(820f, 56f), outline: false);
            status.rectTransform.anchoredPosition = new Vector2(0f, 154f);
            status.fontStyle = FontStyles.Bold;
            status.enableAutoSizing = true;
            status.fontSizeMin = 20f;
            status.fontSizeMax = 30f;
            status.textWrappingMode = TextWrappingModes.Normal;

            Image detailBack = Img("DetailsBack", panelRect, Rounded(), new Color(0.93f, 0.88f, 0.78f), new Vector2(800f, 200f));
            detailBack.type = Image.Type.Sliced;
            detailBack.raycastTarget = false;
            detailBack.rectTransform.anchoredPosition = new Vector2(0f, 20f);

            TextMeshProUGUI details = Text("Details", detailBack.rectTransform, 26f, new Vector2(750f, 184f), outline: false);
            details.alignment = TextAlignmentOptions.MidlineLeft;
            details.textWrappingMode = TextWrappingModes.Normal;
            details.enableAutoSizing = true;
            details.fontSizeMin = 18f;
            details.fontSizeMax = 26f;
            details.lineSpacing = 6f;

            TextMeshProUGUI reason = Text("Reason", panelRect, 26f, new Vector2(820f, 44f), outline: false);
            reason.rectTransform.anchoredPosition = new Vector2(0f, -106f);
            reason.color = Red;
            reason.enableAutoSizing = true;
            reason.fontSizeMin = 18f;
            reason.fontSizeMax = 26f;
            reason.textWrappingMode = TextWrappingModes.Normal;
            reason.text = string.Empty;

            var buttons = new Button[3];
            var images = new Image[3];
            var labels = new TextMeshProUGUI[3];

            for (int i = 0; i < buttons.Length; i++)
            {
                (buttons[i], images[i], labels[i]) =
                    PayButton($"Option_{i}", panelRect, new Vector2((i - 1) * 280f, -196f), Green, "Send", new Vector2(260f, 100f));
            }

            (Button recall, Image _, TextMeshProUGUI recallLabel) =
                PayButton("Recall", panelRect, new Vector2(0f, -330f), Red, "RECALL WORKER", new Vector2(460f, 96f));

            var view = root.AddComponent<WorkerPanelView>();
            Wire(view, ("panel", panelRect), ("group", group), ("backdrop", backdrop), ("closeButton", close),
                 ("title", title), ("subtitle", subtitle), ("healthRoot", healthRoot), ("healthFill", healthFill),
                 ("healthLabel", healthLabel), ("status", status), ("details", details), ("reason", reason),
                 ("recallButton", recall), ("recallLabel", recallLabel));
            WireArray(view, "optionButtons", buttons);
            WireArray(view, "optionImages", images);
            WireArray(view, "optionLabels", labels);

            return root;
        }

        static (Button, Image, TextMeshProUGUI) PayButton(string name, RectTransform parent, Vector2 at, Color colour, string text)
            => PayButton(name, parent, at, colour, text, new Vector2(360f, 96f));

        static (Button, Image, TextMeshProUGUI) PayButton(string name, RectTransform parent, Vector2 at, Color colour,
                                                          string text, Vector2 size)
        {
            Image image = Img(name, parent, Rounded(), colour, size);
            image.type = Image.Type.Sliced;
            image.rectTransform.anchoredPosition = at;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            TextMeshProUGUI label = Text("Label", image.rectTransform, 36f, size - new Vector2(20f, 6f));
            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = true;
            label.fontSizeMin = 22f;
            label.fontSizeMax = 36f;
            label.text = text;

            return (button, image, label);
        }

        static RequirementRowView Row(RectTransform parent, int index, Vector2 at)
        {
            Image back = Img($"Requirement_{index}", parent, Rounded(), new Color(0.93f, 0.88f, 0.78f), new Vector2(660f, 58f));
            back.type = Image.Type.Sliced;
            back.raycastTarget = false;
            back.rectTransform.anchoredPosition = at;
            RectTransform rt = back.rectTransform;

            Image status = Img("Status", rt, Knob(), Green, new Vector2(30f, 30f));
            status.rectTransform.anchoredPosition = new Vector2(-298f, 0f);
            status.raycastTarget = false;

            TextMeshProUGUI label = Text("Label", rt, 30f, new Vector2(300f, 50f), outline: false);
            label.rectTransform.anchoredPosition = new Vector2(-110f, 0f);
            label.alignment = TextAlignmentOptions.Left;

            TextMeshProUGUI amount = Text("Amount", rt, 30f, new Vector2(240f, 50f), outline: false);
            amount.rectTransform.anchoredPosition = new Vector2(190f, 0f);
            amount.alignment = TextAlignmentOptions.Right;
            amount.fontStyle = FontStyles.Bold;

            var row = back.gameObject.AddComponent<RequirementRowView>();
            Wire(row, ("label", label), ("amount", amount), ("status", status));
            return row;
        }

        // --- Pieces -----------------------------------------------------------------------------

        static GameObject CanvasRoot(string name, Vector2 size, bool raycaster)
        {
            var root = new GameObject(name, typeof(RectTransform));

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rt = (RectTransform)root.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * CanvasScale;

            if (raycaster) root.AddComponent<GraphicRaycaster>();

            return root;
        }

        /// <summary>The child every animation runs on, so the pooled root's scale never changes.</summary>
        static RectTransform Body(Transform root)
        {
            var body = new GameObject("Body", typeof(RectTransform));
            var rt = (RectTransform)body.transform;
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

        /// <summary>
        /// A TextMeshPro label. White with an outline by default, so it reads over sky and sand
        /// in the world; <paramref name="outline"/> off for dark text on a light popup panel.
        /// </summary>
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

            if (outline)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
                if (material != null) text.fontSharedMaterial = material;
            }

            return text;
        }

        static readonly Color PanelText = new Color(0.24f, 0.2f, 0.17f);
        static readonly Color Cream = new Color(0.99f, 0.96f, 0.9f);
        static readonly Color Orange = new Color(0.95f, 0.62f, 0.2f);
        static readonly Color Green = new Color(0.32f, 0.7f, 0.3f);
        static readonly Color Red = new Color(0.85f, 0.3f, 0.25f);

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void WireArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetColour(Object target, string field, Color value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Sprite Knob() => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        static Sprite Rounded() => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

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
    }
}
