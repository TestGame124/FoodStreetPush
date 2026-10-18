using System.IO;
using LastOrders.Livestock;
using LastOrders.UI.Hud;
using LastOrders.UI.World;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static LastOrders.EditorTools.Screens.UiKit;

namespace LastOrders.EditorTools.Screens
{
    /// <summary>
    /// The UI that stands over things in the world - the hammer, expand and repair buttons, the work
    /// and expansion timers, health bars, the floating "+XP", the coop and cow shed's card and
    /// bubble - rebuilt in the screens' look: round buttons from the HUD atlas with real icons in
    /// them, rounded bars with the HUD's type on them, captions on chips.
    ///
    /// Same scripts, same sizes and anchors as the prefabs they replace, so they sit where the old
    /// ones sat. The UI catalog and the two livestock prefabs are pointed at the new prefabs; the old
    /// prefabs are kept, and 'Restore Previous World UI' points everything back.
    /// </summary>
    public static class DinerWorldUiBuilder
    {
        public const string Folder = DinerScreensBuilder.PrefabFolder + "/World";

        const string OldFolder = WorldUiPrefabGenerator.Folder;
        const string CoopPath = "Assets/Game Assets/Content/LastOrders/Prefabs/Livestock/Livestock_ChickenCoop.prefab";
        const string ShedPath = "Assets/Game Assets/Content/LastOrders/Prefabs/Livestock/Livestock_CowShelter.prefab";

        /// <summary>As the old world UI: a shade over a centimetre per unit.</summary>
        const float CanvasScale = 0.014f;

        [MenuItem("Last Orders/UI/Build Diner World UI", priority = 214)]
        static void BuildFromMenu() => Debug.Log($"[Last Orders] {Build()}");

        [MenuItem("Last Orders/UI/Restore Previous World UI", priority = 215)]
        static void RestoreFromMenu() => Debug.Log($"[Last Orders] {RestorePrevious()}");

        [MenuItem("Last Orders/UI/Build Crop Card UI", priority = 216)]
        static void BuildCropUiFromMenu() => Debug.Log($"[Last Orders] {BuildCropUi()}");

        /// <summary>The card a tap on a growing crop opens - progress bar, time left, gem button - into the UI catalog.</summary>
        public static string BuildCropUi()
        {
            Load();
            Directory.CreateDirectory(Folder);

            var catalog = AssetDatabase.LoadAssetAtPath<WorldUiCatalog>(WorldUiPrefabGenerator.CatalogPath);
            if (catalog == null) return $"No UI catalog at {WorldUiPrefabGenerator.CatalogPath} - run the world UI setup first.";

            Undo.RecordObject(catalog, "Build Crop Card UI");
            catalog.cropCard = Save<CropCardView>(CropCard(), "WorldUI_CropCard");
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);

            return $"Crop card built in {Folder}; the UI catalog uses it.";
        }

        public static string Build()
        {
            Load();
            Directory.CreateDirectory(Folder);

            var catalog = AssetDatabase.LoadAssetAtPath<WorldUiCatalog>(WorldUiPrefabGenerator.CatalogPath);
            if (catalog == null) return $"No UI catalog at {WorldUiPrefabGenerator.CatalogPath} - run the world UI setup first.";

            Undo.RecordObject(catalog, "Build Diner World UI");
            catalog.actionPrompt = Save<ActionPromptView>(Prompt("WorldUI_HammerPrompt", HudSprites.RoundCream, HudSprites.IconHammer, null,
                                                                 HudPalette.Teal, standing: false), "WorldUI_HammerPrompt");
            catalog.expandPrompt = Save<ActionPromptView>(Prompt("WorldUI_ExpandPrompt", HudSprites.RoundTeal, HudSprites.IconFlag, "EXPAND",
                                                                 HudPalette.TealDark, standing: true), "WorldUI_ExpandPrompt");
            catalog.repairPrompt = Save<ActionPromptView>(Prompt("WorldUI_RepairPrompt", HudSprites.RoundCream, HudSprites.IconWrench, "REPAIR",
                                                                 HudPalette.RedDark, standing: true, releaseOnPress: true), "WorldUI_RepairPrompt");
            catalog.workProgress = Save<WorkProgressView>(Progress("WorldUI_WorkProgress", new Vector2(190f, 50f), null, HudPalette.Mint),
                                                         "WorldUI_WorkProgress");
            catalog.expansionProgress = Save<WorkProgressView>(Progress("WorldUI_ExpansionProgress", new Vector2(330f, 120f), "EXPANDING",
                                                                        HudPalette.Amber), "WorldUI_ExpansionProgress");
            catalog.healthBar = Save<HealthBarView>(HealthBar(), "WorldUI_HealthBar");
            catalog.floatingText = Save<FloatingTextView>(FloatingText(), "WorldUI_FloatingText");
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);

            var card = Save<ProduceProgressView>(ProduceCard(), "WorldUI_ProduceProgress");
            var bubble = Save<ProduceBubbleView>(ProduceBubble(), "WorldUI_ProduceBubble");
            int pens = PointPens(card, bubble);

            BuildCropUi();

            return $"Diner world UI built in {Folder} (10 prefabs); the UI catalog and {pens} livestock prefabs use them. " +
                   $"The old ones are kept in {OldFolder}.";
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

            Undo.RecordObject(catalog, "Restore Previous World UI");
            catalog.actionPrompt = Old("WorldUI_HammerPrompt", catalog.actionPrompt);
            catalog.expandPrompt = Old("WorldUI_ExpandPrompt", catalog.expandPrompt);
            catalog.repairPrompt = Old("WorldUI_RepairPrompt", catalog.repairPrompt);
            catalog.workProgress = Old("WorldUI_WorkProgress", catalog.workProgress);
            catalog.expansionProgress = Old("WorldUI_ExpansionProgress", catalog.expansionProgress);
            catalog.healthBar = Old("WorldUI_HealthBar", catalog.healthBar);
            catalog.floatingText = Old("WorldUI_FloatingText", catalog.floatingText);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);

            ProduceProgressView card = Old<ProduceProgressView>("WorldUI_ProduceProgress", null);
            ProduceBubbleView bubble = Old<ProduceBubbleView>("WorldUI_ProduceBubble", null);
            int pens = card != null && bubble != null ? PointPens(card, bubble) : 0;
            return $"The UI catalog and {pens} livestock prefabs point at the previous world UI again; the diner world UI is left in place.";
        }

        /// <summary>The coop and the cow shed carry their card and bubble prefabs themselves.</summary>
        static int PointPens(ProduceProgressView card, ProduceBubbleView bubble)
        {
            int changed = 0;
            foreach (string path in new[] { CoopPath, ShedPath })
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;

                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                foreach (LivestockPen pen in contents.GetComponentsInChildren<LivestockPen>(true))
                {
                    Set(pen, "cardPrefab", card);
                    Set(pen, "bubblePrefab", bubble);
                }

                PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
                changed++;
            }

            return changed;
        }

        static T Save<T>(GameObject built, string name) where T : Component
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(built, $"{Folder}/{name}.prefab");
            Object.DestroyImmediate(built);
            return prefab.GetComponent<T>();
        }

        // --- Pieces -------------------------------------------------------------------------------

        internal static GameObject CanvasRoot(string name, Vector2 size, bool raycaster)
        {
            var root = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rt = (RectTransform)root.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * CanvasScale;

            if (raycaster) root.AddComponent<GraphicRaycaster>();
            return root;
        }

        /// <summary>The child every animation runs on, so the pooled root's scale never changes.</summary>
        internal static RectTransform Body(GameObject root) => Fill("Body", root.transform);

        /// <summary>A rounded track with a fill clipped to its shape, for a script that sets the fill's amount and colour.</summary>
        static Image Track(RectTransform parent, string name, Vector2 at, Vector2 size, out RectTransform track)
        {
            track = Center(name, parent, at, size);
            Paint(track, HudSprites.BarTrack, Color.white, size: size);

            float inset = size.y >= 30f ? 4f : 3f;
            RectTransform area = Fill("Area", track, inset, inset, inset, inset);
            Paint(area, HudSprites.BarFill, Color.white, size: size - Vector2.one * inset * 2f);
            area.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            Image fill = Paint(Fill("Fill", area), HudSprites.White, HudPalette.Mint);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0.5f;
            Paint(Row("Shine", area, 1f, Mathf.Max(3f, size.y * 0.3f), 4f, 4f), HudSprites.White, new Color(1f, 1f, 1f, 0.24f));
            return fill;
        }

        static void NoRaycasts(GameObject root)
        {
            foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        }

        // --- Buttons over the world ---------------------------------------------------------------

        /// <summary>
        /// A round button over something in the world: the icon in a chunky disc with a little beak
        /// pointing down at the thing, a caption on a chip under it, breathing to be noticed.
        /// </summary>
        internal static GameObject Prompt(string name, string round, string icon, string caption, Color chip, bool standing,
                                 bool releaseOnPress = false)
        {
            bool captioned = !string.IsNullOrEmpty(caption);
            var size = captioned ? new Vector2(170f, 210f) : new Vector2(130f, 150f);
            GameObject root = CanvasRoot(name, size, raycaster: true);
            RectTransform body = Body(root);

            float disc = captioned ? 132f : 118f;
            float y = captioned ? 26f : 8f;

            RectTransform beak = Center("Beak", body, new Vector2(0f, y - disc * 0.5f + 4f), new Vector2(54f, 54f));
            beak.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Paint(beak, HudSprites.AlertPointer, round == HudSprites.RoundTeal ? HudPalette.Teal : round == HudSprites.RoundRed ? HudPalette.Red : HudPalette.Cream);

            RectTransform buttonRect = Center("Button", body, new Vector2(0f, y), new Vector2(disc, disc));
            Image face = Paint(buttonRect, round, Color.white, raycast: true);
            Button button = MakeButton(buttonRect, face);

            Image iconImage = Paint(Center("Icon", buttonRect, new Vector2(0f, disc * 0.04f), new Vector2(disc * 0.64f, disc * 0.64f)), icon,
                                    Color.white);

            if (captioned)
                Chip(body, "Caption", caption, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(150f, 40f), chip, 24f);

            var view = root.AddComponent<ActionPromptView>();
            Set(view, "body", body);
            Set(view, "button", button);
            Set(view, "icon", iconImage);
            Set(view, "pulse", buttonRect);

            var so = new SerializedObject(view);
            so.FindProperty("dismissOnWorldTap").boolValue = !standing;
            so.FindProperty("releaseOnPress").boolValue = !standing || releaseOnPress;
            so.FindProperty("pulseScale").floatValue = 1.07f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // --- Timers and bars ----------------------------------------------------------------------

        static GameObject Progress(string name, Vector2 size, string caption, Color working)
        {
            GameObject root = CanvasRoot(name, size, raycaster: false);
            RectTransform body = Body(root);

            bool captioned = !string.IsNullOrEmpty(caption);
            var barSize = captioned ? new Vector2(size.x - 20f, 54f) : new Vector2(size.x - 30f, 40f);
            float barY = captioned ? -18f : 0f;

            if (captioned)
                Chip(body, "Caption", caption, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(210f, 42f),
                     HudPalette.Ink.WithAlpha(0.9f), 26f);

            Image fill = Track(body, "Bar", new Vector2(captioned ? 0f : 14f, barY), barSize, out RectTransform track);
            TextMeshProUGUI label = Label(Fill("Label", track, 6f, 2f, 6f, 0f), "...", Face.Display, captioned ? 30f : 24f, Color.white,
                                          TextAlignmentOptions.Center, Style.Outline);

            if (!captioned)
                Paint(At("Clock", body, new Vector2(0f, 0.5f), new Vector2(-4f, 0f), new Vector2(50f, 50f)), HudSprites.IconClock, Color.white);

            NoRaycasts(root);
            var view = root.AddComponent<WorkProgressView>();
            Set(view, "body", body);
            Set(view, "fill", fill);
            Set(view, "label", label);
            Set(view, "workingColour", working);
            Set(view, "waitingColour", new Color(0.80f, 0.78f, 0.74f));
            return root;
        }

        static GameObject HealthBar()
        {
            GameObject root = CanvasRoot("WorldUI_HealthBar", new Vector2(150f, 34f), raycaster: false);
            RectTransform body = Body(root);
            var group = body.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            Image fill = Track(body, "Bar", Vector2.zero, new Vector2(150f, 28f), out RectTransform track);
            TextMeshProUGUI label = Label(Fill("Label", track, 4f, 1f, 4f, 0f), "", Face.Display, 18f, Color.white,
                                          TextAlignmentOptions.Center, Style.Outline);

            NoRaycasts(root);
            var view = root.AddComponent<HealthBarView>();
            Set(view, "body", body);
            Set(view, "fill", fill);
            Set(view, "label", label);
            Set(view, "group", group);
            Set(view, "healthyColour", HudPalette.Mint);
            Set(view, "hurtColour", HudPalette.Amber);
            Set(view, "dangerColour", HudPalette.Red);

            // Up at once: a bar that grows in over a fight is a bar that shows the wrong number.
            Set(view, "popInSeconds", 0f);
            return root;
        }

        static GameObject FloatingText()
        {
            GameObject root = CanvasRoot("WorldUI_FloatingText", new Vector2(360f, 80f), raycaster: false);
            RectTransform body = Body(root);
            var group = body.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            TextMeshProUGUI label = Label(Fill("Label", body), "+2 XP", Face.Display, 48f, Color.white, TextAlignmentOptions.Center,
                                          Style.Outline);

            NoRaycasts(root);
            var view = root.AddComponent<FloatingTextView>();
            Set(view, "body", body);
            Set(view, "label", label);
            Set(view, "group", group);
            return root;
        }

        // --- The coop and cow shed ----------------------------------------------------------------

        /// <summary>
        /// The card a tap on a coop or shed brings up: what it makes in a round badge with how many
        /// are waiting, a line, the feed left, and the time to the next one on a bar - on dark glass,
        /// because the card writes its feed count in white.
        /// </summary>
        static GameObject ProduceCard()
        {
            GameObject root = CanvasRoot("WorldUI_ProduceProgress", new Vector2(320f, 120f), raycaster: false);
            RectTransform body = Body(root);

            var panelSize = new Vector2(240f, 88f);
            RectTransform panel = Center("Panel", body, new Vector2(34f, 0f), panelSize);
            Paint(panel, HudSprites.GlassCard, Color.white, size: panelSize);

            RectTransform badge = Center("Badge", body, new Vector2(-110f, 2f), new Vector2(104f, 104f));
            Paint(badge, HudSprites.RoundCream, Color.white);
            Image icon = Paint(Center("Icon", badge, new Vector2(0f, 4f), new Vector2(70f, 70f)), HudSprites.IconSeeds, Color.white);
            TextMeshProUGUI stock = Chip(badge, "StockPill", "0/6", new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(70f, 32f), null, 20f);

            TextMeshProUGUI caption = Label(Center("Caption", body, new Vector2(26f, 20f), new Vector2(148f, 34f)), "Next egg", Face.Display,
                                            24f, Color.white, TextAlignmentOptions.MidlineLeft, Style.Shadow).Fit(15f);

            Image feed = Paint(Center("FeedIcon", body, new Vector2(116f, 21f), new Vector2(36f, 36f)), HudSprites.IconSeeds, Color.white);
            TextMeshProUGUI feedCount = Label(Center("FeedCount", body, new Vector2(146f, 19f), new Vector2(40f, 30f)), "x3", Face.Display,
                                              22f, Color.white, TextAlignmentOptions.MidlineLeft, Style.Shadow);

            Image fill = Track(body, "Track", new Vector2(36f, -18f), new Vector2(216f, 32f), out RectTransform track);
            TextMeshProUGUI time = Label(Fill("Time", track, 4f, 1f, 4f, 0f), "24s", Face.Display, 22f, Color.white,
                                         TextAlignmentOptions.Center, Style.Outline);

            NoRaycasts(root);
            var view = root.AddComponent<ProduceProgressView>();
            view.Configure(body, icon, badge, fill, caption, time, stock, feed, feedCount);
            Set(view, "workingColour", HudPalette.Mint);
            Set(view, "hungryColour", HudPalette.Red);
            Set(view, "asleepColour", HudPalette.Night);
            Set(view, "fullColour", HudPalette.Gold);
            return root;
        }

        // --- The plant beds ---------------------------------------------------------------------------

        /// <summary>
        /// The card a tap on a growing crop opens: the crop in a round badge, its name and the timer on
        /// dark glass, and the teal gem button on the right with the price on it - FINISH under it.
        /// A little beak points down at the pot.
        /// </summary>
        static GameObject CropCard()
        {
            GameObject root = CanvasRoot("WorldUI_CropCard", new Vector2(360f, 160f), raycaster: true);
            RectTransform body = Body(root);

            var panelSize = new Vector2(300f, 100f);
            RectTransform beak = Center("Beak", body, new Vector2(-6f, -58f), new Vector2(40f, 40f));
            beak.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Paint(beak, HudSprites.AlertPointer, HudPalette.Glass);

            RectTransform panel = Center("Panel", body, new Vector2(10f, 0f), panelSize);
            Paint(panel, HudSprites.GlassCard, Color.white, raycast: true, size: panelSize);

            RectTransform badge = Center("Badge", body, new Vector2(-128f, 4f), new Vector2(104f, 104f));
            Paint(badge, HudSprites.RoundCream, Color.white);
            Image icon = Paint(Center("Icon", badge, new Vector2(0f, 4f), new Vector2(70f, 70f)), HudSprites.IconSprout, Color.white);

            TextMeshProUGUI caption = Label(Center("Caption", body, new Vector2(-6f, 24f), new Vector2(150f, 34f)), "Tomato", Face.Display,
                                            26f, Color.white, TextAlignmentOptions.MidlineLeft, Style.Shadow).Fit(15f);

            Image fill = Track(body, "Track", new Vector2(-6f, -18f), new Vector2(150f, 30f), out RectTransform track);
            TextMeshProUGUI time = Label(Fill("Time", track, 4f, 1f, 4f, 0f), "45s", Face.Display, 21f, Color.white,
                                         TextAlignmentOptions.Center, Style.Outline);

            // The gem button: the price on top, FINISH under it.
            var buttonSize = new Vector2(98f, 90f);
            RectTransform buttonRect = Center("Finish", body, new Vector2(118f, 0f), buttonSize);
            Image face = Paint(buttonRect, HudSprites.ButtonTeal, Color.white, raycast: true, size: buttonSize);
            Button button = MakeButton(buttonRect, face);
            HudMotion motion = Motion(buttonRect);

            Image gem = Paint(Center("Gem", buttonRect, new Vector2(-17f, 16f), new Vector2(42f, 42f)), HudSprites.IconGem, Color.white);
            TextMeshProUGUI cost = Label(Center("Cost", buttonRect, new Vector2(20f, 16f), new Vector2(44f, 40f)), "2", Face.Display, 32f,
                                         Color.white, TextAlignmentOptions.MidlineLeft, Style.Shadow);
            Label(Center("Label", buttonRect, new Vector2(0f, -22f), new Vector2(90f, 26f)), "FINISH", Face.Display, 19f, Color.white,
                  TextAlignmentOptions.Center, Style.Shadow).Fit(12f);

            // Over everything, for the sparkles: takes no raycasts.
            RectTransform fx = Fill("Fx", body);
            fx.SetAsLastSibling();

            NoRaycasts(root);
            panel.GetComponent<Image>().raycastTarget = true;
            face.raycastTarget = true;

            var view = root.AddComponent<CropCardView>();
            view.Configure(body, icon, caption, fill, time, button, face, motion, cost, gem, fx);
            Set(view, "growingColour", HudPalette.Mint);
            Set(view, "stoppedColour", HudPalette.Disabled);
            return root;
        }

        /// <summary>The bubble over a building with eggs waiting or nothing to eat: the item in a coloured ring, a count on a badge.</summary>
        static GameObject ProduceBubble()
        {
            GameObject root = CanvasRoot("WorldUI_ProduceBubble", new Vector2(140f, 160f), raycaster: false);
            RectTransform body = Body(root);
            RectTransform bob = Center("Bob", body, Vector2.zero, new Vector2(140f, 160f));

            RectTransform beak = Center("Point", bob, new Vector2(0f, -46f), new Vector2(50f, 50f));
            beak.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Paint(beak, HudSprites.AlertPointer, HudPalette.Cream);

            Image ring = Paint(Center("Ring", bob, new Vector2(0f, 8f), new Vector2(122f, 122f)), HudSprites.RoundTint, HudPalette.Mint);
            Paint(Center("Disc", bob, new Vector2(0f, 12f), new Vector2(92f, 92f)), HudSprites.Disc, HudPalette.Cream);
            Image icon = Paint(Center("Icon", bob, new Vector2(0f, 12f), new Vector2(72f, 72f)), HudSprites.IconSeeds, Color.white);

            RectTransform badge = Center("Badge", bob, new Vector2(42f, 48f), new Vector2(48f, 48f));
            Paint(badge, HudSprites.Badge, Color.white);
            TextMeshProUGUI count = Label(Fill("Count", badge, 0f, 2f, 0f, 0f), "2", Face.Display, 26f, Color.white,
                                          TextAlignmentOptions.Center, Style.Outline);

            NoRaycasts(root);
            var view = root.AddComponent<ProduceBubbleView>();
            view.Configure(body, bob, icon, ring, badge.gameObject, count);
            return root;
        }
    }
}
