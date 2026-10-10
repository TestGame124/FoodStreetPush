using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.Debugging;
using LastOrders.UI.Hud;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.EditorTools.Hud
{
    /// <summary>
    /// Builds the gameplay HUD into the open scene (<see cref="GameplayHud"/>) on its own canvas, wires
    /// every reference, and switches off the pieces of the old HUD it replaces - the wallet and level
    /// bar, the shop and orders buttons, the grey-box CompoundHUD with its test-time buttons - and
    /// hides the debug overlay's text panel (F1 or the dev drawer brings it back).
    ///
    /// Nothing old is deleted: 'Restore Previous HUD' switches it all back on. The layout is a table
    /// of anchors in code, laid out for landscape against 1920x1080, every cluster hanging off a
    /// corner of the safe area.
    ///
    /// The canvas sorts below the older UI canvas, so the cooking, inventory, orders and store
    /// screens draw over it as they always did; the HUD fades itself out while one is open.
    /// </summary>
    public static class GameplayHudBuilder
    {
        const string RootName = "GameplayHUD";
        const int SortingOrder = -10;
        const float Margin = 28f;

        static HudSkin skin;

        enum Face
        {
            Display,
            Body,
        }

        enum Style
        {
            Plain,
            Outline,
            Shadow,
        }

        [MenuItem("Last Orders/UI/Build Gameplay HUD (Current Scene)", priority = 201)]
        static void BuildFromMenu() => Debug.Log($"[Last Orders] {Build()}");

        [MenuItem("Last Orders/UI/Restore Previous HUD (Current Scene)", priority = 202)]
        static void RestoreFromMenu() => Debug.Log($"[Last Orders] {RestorePrevious()}");

        public static string Build()
        {
            skin = AssetDatabase.LoadAssetAtPath<HudSkin>(HudArt.SkinPath);
            if (skin == null || skin.sprites.Length == 0)
            {
                HudArt.Bake();
                skin = AssetDatabase.LoadAssetAtPath<HudSkin>(HudArt.SkinPath);
            }

            skin.Rebuild();

            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();

            var previous = new List<GameObject>();
            GameObject old = GameObject.Find(RootName);
            if (old != null)
            {
                HudReplaced kept = old.GetComponent<HudReplaced>();
                if (kept != null) previous.AddRange(kept.objects);
                Undo.DestroyObjectImmediate(old);
            }

            Canvas canvas = CreateCanvas();
            GameObject root = canvas.gameObject;
            Undo.RegisterCreatedObjectUndo(root, "Build Gameplay HUD");

            var hud = root.AddComponent<GameplayHud>();
            hud.Configure(scope, skin);

            // Behind everything: the attack vignette covers the whole screen, notch and all.
            RectTransform vignette = Fill("AttackVignette", root.transform);
            hud.health.vignette = Paint(vignette, HudSprites.Vignette, HudPalette.Red.WithAlpha(0f), 0.4f);
            hud.health.vignette.enabled = false;

            RectTransform safe = Fill("SafeArea", root.transform);
            safe.gameObject.AddComponent<HudSafeArea>();
            hud.groups.everything = safe.gameObject.AddComponent<CanvasGroup>();

            BuildTopLeft(hud, safe);
            BuildTopCenter(hud, safe);
            BuildTopRight(hud, safe, scope);
            BuildBottomLeft(hud, safe);
            BuildBottomRight(hud, safe);
            BuildBottomCenter(hud, safe);
            BuildBanner(hud, safe);

            // Toasts over the banner, so a message is never hidden behind an announcement.
            hud.toastRoot.SetAsLastSibling();

            BuildResult(hud, safe);

            WireLegacyPanels(hud);
            List<GameObject> replaced = SwitchOffOldHud(previous);

            var record = root.AddComponent<HudReplaced>();
            record.objects = replaced.ToArray();

            HideDebugPanel();

            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;

            return $"Gameplay HUD built: {root.GetComponentsInChildren<Graphic>(true).Length} graphics on '{RootName}' " +
                   $"(sort {SortingOrder}), {replaced.Count} old HUD objects switched off" +
                   $"{(scope == null ? " - WARNING: no GameLifetimeScope in the scene, so it will only show placeholders" : "")}. " +
                   "Scene left unsaved.";
        }

        public static string RestorePrevious()
        {
            GameObject root = GameObject.Find(RootName);
            if (root == null) return $"No '{RootName}' in the open scene: nothing to restore.";

            HudReplaced record = root.GetComponent<HudReplaced>();
            int restored = 0;
            if (record != null)
            {
                foreach (GameObject go in record.objects)
                {
                    if (go == null) continue;
                    Undo.RecordObject(go, "Restore Previous HUD");
                    go.SetActive(true);
                    restored++;
                }
            }

            Undo.RecordObject(root, "Restore Previous HUD");
            root.SetActive(false);
            EditorSceneManager.MarkSceneDirty(root.scene);

            return $"Previous HUD restored ({restored} objects switched back on); '{RootName}' switched off, not deleted. " +
                   "Re-run Build Gameplay HUD to bring it back. Scene left unsaved.";
        }

        // --- Canvas and the old HUD ---------------------------------------------------------------

        static Canvas CreateCanvas()
        {
            var go = new GameObject(RootName, typeof(RectTransform)) { layer = 5 };

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            canvas.pixelPerfect = false;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            scaler.referencePixelsPerUnit = 100f;
            go.AddComponent<HudCanvasFit>();

            go.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var events = new GameObject("EventSystem");
                events.AddComponent<UnityEngine.EventSystems.EventSystem>();
                events.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(events, "Build Gameplay HUD");
            }

            return canvas;
        }

        /// <summary>The old wallet, level bar, shop and orders buttons, and the grey-box HUD.</summary>
        static List<GameObject> SwitchOffOldHud(List<GameObject> previous)
        {
            var off = new List<GameObject>();

            void Off(GameObject go)
            {
                if (go == null) return;
                if (!off.Contains(go)) off.Add(go);
                if (!go.activeSelf) return;

                Undo.RecordObject(go, "Build Gameplay HUD");
                go.SetActive(false);
            }

            foreach (GameObject go in previous) Off(go);

            UIGame legacy = Object.FindFirstObjectByType<UIGame>(FindObjectsInactive.Include);
            if (legacy != null)
            {
                Off(Child(legacy.transform, "ShopButton"));
                Off(Child(legacy.transform, "OrdersBtn"));
                Off(Child(legacy.transform.parent, "Currencies"));
            }

            foreach (var compound in Object.FindObjectsByType<Blockout.CompoundHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Off(compound.gameObject);

            return off;
        }

        static GameObject Child(Transform parent, string name)
        {
            if (parent == null) return null;
            Transform t = parent.Find(name);
            return t != null ? t.gameObject : null;
        }

        /// <summary>The older full-screen panels the HUD steps aside for, found by name under UIGame.</summary>
        static void WireLegacyPanels(GameplayHud hud)
        {
            UIGame legacy = Object.FindFirstObjectByType<UIGame>(FindObjectsInactive.Include);
            if (legacy == null) return;

            var covering = new List<GameObject>();
            foreach (string name in new[]
                     {
                         "CookingPanel", "Inventory", "OrdersPanel", "NotEnoughPanel", "LevelUpPanel", "GuidePanel",
                         "SignBoardPanel", "Buy Ingredients", "InApp",
                     })
            {
                GameObject go = Child(legacy.transform, name);
                if (go != null) covering.Add(go);
            }

            hud.coveringPanels = covering.ToArray();

            GameObject seeds = Child(legacy.transform, "SeedsInventory");
            hud.topPanels = seeds != null ? new[] { seeds } : new GameObject[0];
        }

        static void HideDebugPanel()
        {
            foreach (var overlay in Object.FindObjectsByType<LastOrdersDebugOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(overlay);
                SerializedProperty show = so.FindProperty("showPanel");
                if (show == null || !show.boolValue) continue;

                show.boolValue = false;
                so.ApplyModifiedProperties();
            }
        }

        // --- Top left: level, experience, building resources ----------------------------------------

        static void BuildTopLeft(GameplayHud hud, RectTransform safe)
        {
            RectTransform cluster = At("TopLeft", safe, new Vector2(0f, 1f), new Vector2(Margin, -22f), new Vector2(460f, 190f));
            hud.groups.topLeft = cluster.gameObject.AddComponent<CanvasGroup>();

            RectTransform pill = At("XpPill", cluster, new Vector2(0f, 1f), new Vector2(66f, -26f), new Vector2(300f, 58f));
            Paint(pill, HudSprites.Glass, Color.white, 72f / 58f);

            RectTransform track = Fill("XpTrack", pill, 58f, 15f, 18f, 15f);
            hud.profile.xp = Bar(track, HudPalette.Teal, withGhost: false);

            hud.profile.xpText = Label(Fill("XpText", track), "120 / 300 XP", Face.Body, 18f, Color.white,
                                       TextAlignmentOptions.Center, Style.Shadow);

            RectTransform star = At("Level", cluster, new Vector2(0f, 1f), new Vector2(-6f, 0f), new Vector2(112f, 112f));
            Paint(star, HudSprites.IconStar, Color.white);
            hud.profile.levelMotion = Motion(star, pressable: false);
            RectTransform levelText = Node("LevelText", star, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                           new Vector2(0f, -6f), new Vector2(80f, 60f));
            hud.profile.level = Label(levelText, "1", Face.Display, 40f, Color.white, TextAlignmentOptions.Center, Style.Outline);

            hud.profile.wood = ResourceChip(cluster, "Wood", HudSprites.IconWood, new Vector2(10f, -122f), "20");
            hud.profile.scrap = ResourceChip(cluster, "Scrap", HudSprites.IconScrap, new Vector2(146f, -122f), "60");
            hud.profile.kits = ResourceChip(cluster, "RepairKits", HudSprites.IconKit, new Vector2(282f, -122f), "1");
        }

        static HudCounter ResourceChip(RectTransform parent, string name, string icon, Vector2 at, string sample)
        {
            RectTransform chip = At(name, parent, new Vector2(0f, 1f), at, new Vector2(124f, 50f));
            Paint(chip, HudSprites.Glass, Color.white, 72f / 50f);
            HudMotion motion = Motion(chip, pressable: false);

            RectTransform image = Node("Icon", chip, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                       new Vector2(-14f, 2f), new Vector2(62f, 62f));
            Paint(image, icon, Color.white);

            TMP_Text text = Label(Fill("Count", chip, 50f, 2f, 14f, 2f), sample, Face.Display, 26f, Color.white,
                                  TextAlignmentOptions.Center, Style.Outline);

            var counter = chip.gameObject.AddComponent<HudCounter>();
            counter.Configure(text, motion);
            return counter;
        }

        // --- Top centre: the day, the restaurant's health, the toasts ---------------------------------

        static void BuildTopCenter(GameplayHud hud, RectTransform safe)
        {
            RectTransform cluster = At("TopCenter", safe, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(520f, 200f));
            hud.groups.topCenter = cluster.gameObject.AddComponent<CanvasGroup>();

            RectTransform dayRoot = At("DayCard", cluster, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(480f, 100f));
            Image face = Card(dayRoot);
            hud.day.card = MakeButton(dayRoot, face);
            hud.day.motion = Motion(dayRoot, pressable: true);

            RectTransform disc = Node("Disc", dayRoot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                      new Vector2(14f, 3f), new Vector2(78f, 78f));
            hud.day.disc = Paint(disc, HudSprites.DiscShaded, new Color(1f, 0.93f, 0.76f));

            RectTransform ring = Node("Countdown", disc, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                      Vector2.zero, new Vector2(92f, 92f));
            hud.day.ring = Paint(ring, HudSprites.Ring, HudPalette.Red);
            hud.day.ring.type = Image.Type.Filled;
            hud.day.ring.fillMethod = Image.FillMethod.Radial360;
            hud.day.ring.fillOrigin = (int)Image.Origin360.Top;
            hud.day.ring.fillClockwise = false;
            hud.day.ring.preserveAspect = true;
            ring.gameObject.SetActive(false);

            RectTransform icon = Node("Icon", disc, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                      new Vector2(0f, 1f), new Vector2(66f, 66f));
            hud.day.icon = Paint(icon, HudSprites.IconDawn, Color.white);

            hud.day.title = Label(At("Title", dayRoot, new Vector2(0f, 1f), new Vector2(106f, -9f), new Vector2(190f, 42f)),
                                  "DAY 1", Face.Display, 38f, HudPalette.Ink, TextAlignmentOptions.Left, Style.Plain);

            RectTransform chip = At("PhaseChip", dayRoot, new Vector2(1f, 1f), new Vector2(-18f, -14f), new Vector2(150f, 34f));
            hud.day.chip = Paint(chip, HudSprites.Chip, HudPalette.Gold, 48f / 34f);
            hud.day.phase = Label(Fill("Label", chip, 4f, 1f, 4f, 0f), "MORNING", Face.Display, 20f, Color.white,
                                  TextAlignmentOptions.Center, Style.Shadow);

            hud.day.time = Label(At("Time", dayRoot, new Vector2(0f, 1f), new Vector2(108f, -51f), new Vector2(354f, 24f)),
                                 "Closed - open when ready", Face.Body, 19f, HudPalette.InkSoft, TextAlignmentOptions.Left, Style.Plain);

            RectTransform bar = At("TimeBar", dayRoot, new Vector2(0f, 1f), new Vector2(108f, -79f), new Vector2(352f, 10f));
            hud.day.bar = Bar(bar, HudPalette.Teal, withGhost: false);
            bar.gameObject.SetActive(false);

            // The restaurant's health, under the day card while it is under attack.
            RectTransform healthRoot = At("RestaurantHealth", cluster, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(440f, 60f));
            hud.health.group = healthRoot.gameObject.AddComponent<CanvasGroup>();
            hud.health.group.alpha = 0f;
            hud.health.group.blocksRaycasts = false;
            hud.health.group.interactable = false;
            Paint(healthRoot, HudSprites.Glass, Color.white, 72f / 60f);
            hud.health.motion = Motion(healthRoot, pressable: false);

            RectTransform healthTrack = Fill("Track", healthRoot, 62f, 15f, 20f, 15f);
            hud.health.bar = Bar(healthTrack, HudPalette.Mint, withGhost: true);
            hud.health.text = Label(Fill("Value", healthTrack), "400 / 400", Face.Display, 22f, Color.white,
                                    TextAlignmentOptions.Center, Style.Outline);

            RectTransform diner = Node("Icon", healthRoot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                       new Vector2(-16f, 3f), new Vector2(78f, 78f));
            Paint(diner, HudSprites.IconDiner, Color.white);

            // The toasts hang under it all.
            RectTransform toastRoot = At("Toasts", safe, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(900f, 400f));
            hud.toastRoot = toastRoot;
            hud.toasts = toastRoot.gameObject.AddComponent<HudToasts>();
            hud.toasts.Configure(skin, ToastTemplate(toastRoot));
        }

        static HudToast ToastTemplate(RectTransform parent)
        {
            RectTransform rt = At("ToastTemplate", parent, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(420f, 64f));
            Paint(rt, HudSprites.GlassCard, Color.white);
            var toast = rt.gameObject.AddComponent<HudToast>();
            toast.rect = rt;
            toast.group = rt.gameObject.AddComponent<CanvasGroup>();
            toast.group.blocksRaycasts = false;
            toast.group.interactable = false;
            Motion(rt, pressable: false);

            RectTransform disc = Node("Disc", rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                      new Vector2(14f, 0f), new Vector2(44f, 44f));
            toast.disc = Paint(disc, HudSprites.Disc, HudPalette.Teal);

            RectTransform glyph = Node("Glyph", disc, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                       Vector2.zero, new Vector2(30f, 30f));
            toast.glyph = Paint(glyph, HudSprites.GlyphInfo, Color.white);

            RectTransform text = Node("Text", rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                      new Vector2(72f, 0f), new Vector2(320f, 48f));
            toast.text = Label(text, "Message", Face.Body, 22f, Color.white, TextAlignmentOptions.MidlineLeft, Style.Shadow);
            toast.text.textWrappingMode = TextWrappingModes.Normal;

            rt.gameObject.SetActive(false);
            return toast;
        }

        // --- Top right: wallet, dev tools, safety ------------------------------------------------------

        static void BuildTopRight(GameplayHud hud, RectTransform safe, GameLifetimeScope scope)
        {
            RectTransform cluster = At("TopRight", safe, new Vector2(1f, 1f), new Vector2(-Margin, -22f), new Vector2(600f, 240f));
            hud.groups.topRight = cluster.gameObject.AddComponent<CanvasGroup>();

            RectTransform row = At("Wallet", cluster, new Vector2(1f, 1f), Vector2.zero, new Vector2(600f, 62f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperRight;
            layout.spacing = 26f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            hud.wallet.coins = WalletPill(row, "Coins", HudSprites.IconCoin, 240f, "1,250", out hud.wallet.coinsPlus);
            hud.wallet.gems = WalletPill(row, "Gems", HudSprites.IconGem, 190f, "12", out hud.wallet.gemsPlus);

            // Dev tools: a gear in development builds only, with the time controls behind it.
            RectTransform gear = Node("DevTools", row, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                                      Vector2.zero, new Vector2(62f, 62f));
            Image gearFace = Paint(gear, HudSprites.RoundGlass, Color.white, raycast: true);
            hud.dev.toggle = MakeButton(gear, gearFace);
            Motion(gear, pressable: true);
            Paint(Node("Icon", gear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                       new Vector2(44f, 44f)), HudSprites.IconGear, Color.white);

            BuildSafety(hud, cluster);
            BuildDevDrawer(hud, cluster, scope);
        }

        static HudCounter WalletPill(RectTransform row, string name, string icon, float width, string sample, out Button plus)
        {
            RectTransform pill = Node(name, row, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                                      Vector2.zero, new Vector2(width, 60f));
            Paint(pill, HudSprites.Glass, Color.white, 72f / 60f);
            HudMotion motion = Motion(pill, pressable: false);

            RectTransform image = Node("Icon", pill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                       new Vector2(-22f, 2f), new Vector2(76f, 76f));
            Paint(image, icon, Color.white);

            TMP_Text text = Label(Fill("Amount", pill, 50f, 2f, 52f, 2f), sample, Face.Display, 36f, Color.white,
                                  TextAlignmentOptions.Center, Style.Outline);

            RectTransform plusRoot = Node("Add", pill, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                                          new Vector2(-7f, 1f), new Vector2(46f, 46f));
            Image plusFace = Paint(plusRoot, HudSprites.RoundGold, Color.white, raycast: true);
            plus = MakeButton(plusRoot, plusFace);
            Motion(plusRoot, pressable: true);
            Paint(Node("Glyph", plusRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                       new Vector2(0f, 2f), new Vector2(28f, 28f)), HudSprites.GlyphPlus, Color.white);

            var counter = pill.gameObject.AddComponent<HudCounter>();
            counter.Configure(text, motion);
            return counter;
        }

        static void BuildSafety(GameplayHud hud, RectTransform cluster)
        {
            RectTransform root = At("Safety", cluster, new Vector2(1f, 1f), new Vector2(0f, -80f), new Vector2(410f, 130f));
            hud.groups.safety = root.gameObject.AddComponent<CanvasGroup>();
            Image face = Card(root);
            hud.safety.card = MakeButton(root, face);
            hud.safety.motion = Motion(root, pressable: true);

            RectTransform shield = Node("Shield", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                                        new Vector2(8f, -4f), new Vector2(96f, 96f));
            Paint(shield, HudSprites.IconShield, Color.white);
            hud.safety.value = Label(Node("Value", shield, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                          new Vector2(0f, 3f), new Vector2(80f, 50f)),
                                     "30", Face.Display, 32f, Color.white, TextAlignmentOptions.Center, Style.Outline);

            Label(At("Title", root, new Vector2(0f, 1f), new Vector2(112f, -12f), new Vector2(200f, 30f)),
                  "SAFETY", Face.Display, 24f, HudPalette.Ink, TextAlignmentOptions.Left, Style.Plain);

            RectTransform bar = At("Meter", root, new Vector2(0f, 1f), new Vector2(112f, -46f), new Vector2(278f, 18f));
            hud.safety.bar = Bar(bar, HudPalette.Amber, withGhost: false);

            RectTransform strip = Node("Status", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                                       new Vector2(0f, 16f), new Vector2(-24f, 40f));
            hud.safety.strip = Paint(strip, HudSprites.Chip, new Color(0.92f, 0.87f, 0.78f), 48f / 40f);

            RectTransform statusIcon = Node("Icon", strip, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                            new Vector2(2f, 1f), new Vector2(44f, 44f));
            hud.safety.statusIcon = Paint(statusIcon, HudSprites.IconZombie, Color.white);

            hud.safety.status = Label(Fill("Line", strip, 48f, 2f, 92f, 2f), "Next zombie attack in 2 days", Face.Body, 17f,
                                      HudPalette.Ink, TextAlignmentOptions.MidlineLeft, Style.Plain);
            hud.safety.status.overflowMode = TextOverflowModes.Ellipsis;
            hud.safety.status.enableAutoSizing = true;
            hud.safety.status.fontSizeMin = 13f;
            hud.safety.status.fontSizeMax = 17f;

            RectTransform threat = Node("Threat", strip, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                                        new Vector2(-5f, 0f), new Vector2(84f, 30f));
            hud.safety.threatChip = Paint(threat, HudSprites.Chip, HudPalette.Mint, 48f / 30f);
            hud.safety.threat = Label(Fill("Label", threat, 2f, 0f, 2f, 0f), "LOW", Face.Display, 16f, Color.white,
                                      TextAlignmentOptions.Center, Style.Shadow);
        }

        static void BuildDevDrawer(GameplayHud hud, RectTransform cluster, GameLifetimeScope scope)
        {
            // TimeControls lives on the drawer's root, which stays active, so a held mode keeps
            // holding after the panel is closed.
            RectTransform root = At("DevDrawer", cluster, new Vector2(1f, 1f), new Vector2(0f, -76f), new Vector2(350f, 250f));
            var controls = root.gameObject.AddComponent<TimeControls>();

            RectTransform panel = Fill("Panel", root);
            Paint(panel, HudSprites.GlassCard, Color.white, raycast: true);
            hud.dev.panel = panel.gameObject;

            Label(At("Title", panel, new Vector2(0f, 1f), new Vector2(20f, -14f), new Vector2(300f, 30f)),
                  "DEV TOOLS", Face.Display, 22f, Color.white, TextAlignmentOptions.Left, Style.Outline);
            Label(At("Hint", panel, new Vector2(0f, 1f), new Vector2(20f, -44f), new Vector2(310f, 22f)),
                  "Time of day (testing only)", Face.Body, 15f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Left, Style.Shadow);

            Button toggle = DevButton(panel, "Day / Night", new Vector2(18f, -74f), 150f);
            Button normal = DevButton(panel, "Normal", new Vector2(180f, -74f), 150f);
            Button day = DevButton(panel, "All Day", new Vector2(18f, -132f), 150f);
            Button night = DevButton(panel, "All Night", new Vector2(180f, -132f), 150f);
            hud.dev.overlay = DevButton(panel, "Debug panel: OFF", new Vector2(18f, -190f), 312f);
            hud.dev.overlayLabel = hud.dev.overlay.GetComponentInChildren<TMP_Text>();

            controls.Configure(scope, toggle, normal, day, night);
            panel.gameObject.SetActive(false);
        }

        static Button DevButton(RectTransform parent, string label, Vector2 at, float width)
        {
            RectTransform rt = At(label, parent, new Vector2(0f, 1f), at, new Vector2(width, 50f));
            Image face = Paint(rt, HudSprites.ButtonCream, Color.white, 112f / 50f, raycast: true);
            Button button = MakeButton(rt, face);
            Motion(rt, pressable: true);
            Label(Fill("Label", rt, 6f, 6f, 6f, 2f), label, Face.Display, 19f, HudPalette.Ink, TextAlignmentOptions.Center, Style.Plain);
            return button;
        }

        // --- Bottom left: objective, shop, repair -------------------------------------------------------

        static void BuildBottomLeft(GameplayHud hud, RectTransform safe)
        {
            RectTransform cluster = At("BottomLeft", safe, new Vector2(0f, 0f), new Vector2(Margin, 22f), new Vector2(900f, 340f));
            hud.groups.bottomLeft = cluster.gameObject.AddComponent<CanvasGroup>();

            RectTransform objective = At("Objective", cluster, new Vector2(0f, 0f), new Vector2(0f, 206f), new Vector2(480f, 122f));
            hud.objective.panel = objective;
            Image face = Card(objective);
            hud.objective.card = MakeButton(objective, face);
            hud.objective.motion = Motion(objective, pressable: true);

            RectTransform disc = Node("Disc", objective, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                      new Vector2(15f, 3f), new Vector2(84f, 84f));
            hud.objective.disc = Paint(disc, HudSprites.DiscShaded, new Color(0.80f, 0.92f, 0.90f));
            RectTransform icon = Node("Icon", disc, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                      new Vector2(0f, 1f), new Vector2(68f, 68f));
            hud.objective.icon = Paint(icon, HudSprites.IconClear, Color.white);

            RectTransform dot = Node("New", disc, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                                     new Vector2(-8f, -8f), new Vector2(30f, 30f));
            Paint(dot, HudSprites.Badge, Color.white);
            hud.objective.newDot = dot.gameObject;
            dot.gameObject.SetActive(false);

            RectTransform text = Fill("Text", objective, 112f, 18f, 18f, 12f);
            text.gameObject.AddComponent<RectMask2D>();
            hud.objective.text = text.gameObject.AddComponent<CanvasGroup>();
            hud.objective.text.blocksRaycasts = false;

            hud.objective.title = Label(Node("Title", text, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                                             Vector2.zero, new Vector2(0f, 36f)),
                                        "A Fresh Start", Face.Display, 28f, HudPalette.Ink, TextAlignmentOptions.Left, Style.Plain);
            hud.objective.title.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform bodyRect = Fill("Body", text, 0f, 0f, 0f, 38f);
            hud.objective.body = Label(bodyRect, "Clear the debris from the diner entrance", Face.Body, 20f, HudPalette.Ink,
                                       TextAlignmentOptions.TopLeft, Style.Plain);
            hud.objective.body.textWrappingMode = TextWrappingModes.Normal;
            hud.objective.body.overflowMode = TextOverflowModes.Ellipsis;
            hud.objective.body.enableAutoSizing = true;
            hud.objective.body.fontSizeMin = 15f;
            hud.objective.body.fontSizeMax = 20f;

            hud.actions.shop = RoundButton(cluster, "Shop", HudSprites.IconShop, "SHOP", HudPalette.Teal,
                                           new Vector2(0f, 0f), new Vector2(0f, 0f), out _);

            // Repair: beside the shop, only while something in the yard is wrecked.
            RectTransform repair = At("Repair", cluster, new Vector2(0f, 0f), new Vector2(176f, 40f), new Vector2(300f, 96f));
            hud.actions.repairFace = Paint(repair, HudSprites.ButtonRed, Color.white, raycast: true);
            hud.actions.repair = MakeButton(repair, hud.actions.repairFace);
            hud.actions.repairMotion = Motion(repair, pressable: true);
            Paint(Node("Icon", repair, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-10f, 6f),
                       new Vector2(78f, 78f)), HudSprites.IconWrench, Color.white);
            hud.actions.repairLabel = Label(At("Label", repair, new Vector2(0f, 1f), new Vector2(76f, -10f), new Vector2(210f, 40f)),
                                            "REPAIR 2", Face.Display, 28f, Color.white, TextAlignmentOptions.Left, Style.Shadow);
            hud.actions.repairSub = Label(At("Cost", repair, new Vector2(0f, 1f), new Vector2(78f, -50f), new Vector2(210f, 24f)),
                                          "40 coins  ·  1 kit", Face.Body, 16f, Color.white, TextAlignmentOptions.Left, Style.Shadow);
            repair.gameObject.SetActive(false);
        }

        // --- Bottom right: orders ----------------------------------------------------------------------

        static void BuildBottomRight(GameplayHud hud, RectTransform safe)
        {
            RectTransform cluster = At("BottomRight", safe, new Vector2(1f, 0f), new Vector2(-Margin, 22f), new Vector2(220f, 220f));
            hud.groups.bottomRight = cluster.gameObject.AddComponent<CanvasGroup>();

            hud.actions.orders = RoundButton(cluster, "Orders", HudSprites.IconOrders, "ORDERS", HudPalette.Red,
                                             new Vector2(1f, 0f), new Vector2(0f, 0f), out HudMotion motion);
            hud.actions.ordersMotion = motion;

            RectTransform badge = Node("Badge", hud.actions.orders.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                                       new Vector2(0.5f, 0.5f), new Vector2(-18f, -18f), new Vector2(54f, 54f));
            Paint(badge, HudSprites.Badge, Color.white);
            hud.actions.ordersCount = Label(Fill("Count", badge, 0f, 2f, 0f, 0f), "2", Face.Display, 26f, Color.white,
                                            TextAlignmentOptions.Center, Style.Outline);
            hud.actions.ordersBadge = badge.gameObject;
            badge.gameObject.SetActive(false);
        }

        /// <summary>A big round cream button with an icon and a coloured name tag under it.</summary>
        static Button RoundButton(RectTransform parent, string name, string icon, string label, Color tag, Vector2 anchor,
                                  Vector2 at, out HudMotion motion)
        {
            RectTransform root = At(name, parent, anchor, at + new Vector2(0f, 18f), new Vector2(150f, 150f));
            Image face = Paint(root, HudSprites.RoundCream, Color.white, raycast: true);
            Button button = MakeButton(root, face);
            motion = Motion(root, pressable: true);

            Paint(Node("Icon", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f),
                       new Vector2(106f, 106f)), icon, Color.white);

            RectTransform chip = Node("Tag", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                                      new Vector2(0f, 4f), new Vector2(124f, 38f));
            Paint(chip, HudSprites.Chip, tag, 48f / 38f);
            Label(Fill("Label", chip, 4f, 1f, 4f, 0f), label, Face.Display, 21f, Color.white, TextAlignmentOptions.Center, Style.Outline);

            return button;
        }

        // --- Bottom centre: the one action that moves the day on --------------------------------------

        static void BuildBottomCenter(GameplayHud hud, RectTransform safe)
        {
            RectTransform cluster = At("BottomCenter", safe, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(420f, 130f));
            hud.groups.bottomCenter = cluster.gameObject.AddComponent<CanvasGroup>();

            RectTransform button = At("PhaseAction", cluster, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(390f, 110f));
            hud.actions.phaseFace = Paint(button, HudSprites.ButtonTeal, Color.white, raycast: true);
            hud.actions.phase = MakeButton(button, hud.actions.phaseFace);
            hud.actions.phaseMotion = Motion(button, pressable: true);

            RectTransform glyph = Node("Glyph", button, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                       new Vector2(30f, 6f), new Vector2(54f, 54f));
            hud.actions.phaseGlyph = Paint(glyph, HudSprites.GlyphPlay, Color.white);

            hud.actions.phaseLabel = Label(At("Label", button, new Vector2(0f, 1f), new Vector2(98f, -10f), new Vector2(270f, 48f)),
                                           "OPEN UP", Face.Display, 40f, Color.white, TextAlignmentOptions.Left, Style.Shadow);
            hud.actions.phaseSub = Label(At("Sub", button, new Vector2(0f, 1f), new Vector2(100f, -56f), new Vector2(270f, 24f)),
                                         "Start day 1", Face.Body, 18f, Color.white, TextAlignmentOptions.Left, Style.Shadow);

            RectTransform timer = At("Timer", button, new Vector2(0f, 1f), new Vector2(30f, -84f), new Vector2(330f, 9f));
            hud.actions.phaseTimer = Bar(timer, new Color(1f, 1f, 1f, 0.92f), withGhost: false);
            timer.gameObject.SetActive(false);
        }

        // --- The banner -------------------------------------------------------------------------------

        static void BuildBanner(GameplayHud hud, RectTransform safe)
        {
            RectTransform root = At("Banner", safe, new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(760f, 170f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            RectTransform body = Node("Body", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                      Vector2.zero, new Vector2(720f, 150f));
            Image ribbon = Paint(Fill("Ribbon", body), HudSprites.Ribbon, HudPalette.Teal);

            TMP_Text title = Label(Node("Title", body, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                                        new Vector2(0f, 30f), new Vector2(-150f, 64f)),
                                   "DAY 1", Face.Display, 54f, Color.white, TextAlignmentOptions.Center, Style.Outline);
            title.enableAutoSizing = true;
            title.fontSizeMin = 30f;
            title.fontSizeMax = 54f;

            TMP_Text subtitle = Label(Node("Subtitle", body, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                                           new Vector2(0f, -14f), new Vector2(-170f, 30f)),
                                      "Welcome to the diner", Face.Body, 22f, Color.white, TextAlignmentOptions.Center, Style.Shadow);

            hud.banner = root.gameObject.AddComponent<HudBanner>();
            hud.banner.Configure(group, body, ribbon, title, subtitle);
        }

        // --- The morning card -------------------------------------------------------------------------

        static void BuildResult(GameplayHud hud, RectTransform safe)
        {
            RectTransform root = Fill("Result", safe);
            hud.result.group = root.gameObject.AddComponent<CanvasGroup>();

            Paint(Fill("Dim", root, -400f, -400f, -400f, -400f), HudSprites.White, new Color(0.08f, 0.05f, 0.03f, 0.4f), raycast: true);

            RectTransform card = Node("Card", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                      new Vector2(0f, -10f), new Vector2(660f, 470f));
            hud.result.card = card;
            Card(card);
            hud.result.cardMotion = Motion(card, pressable: false);

            RectTransform ribbonRoot = Node("Ribbon", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                                            new Vector2(0f, -6f), new Vector2(620f, 132f));
            hud.result.ribbon = Paint(ribbonRoot, HudSprites.Ribbon, HudPalette.Mint);
            hud.result.title = Label(Node("Title", ribbonRoot, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                                          new Vector2(0f, 10f), new Vector2(-150f, 70f)),
                                     "VICTORY", Face.Display, 56f, Color.white, TextAlignmentOptions.Center, Style.Outline);
            hud.result.title.enableAutoSizing = true;
            hud.result.title.fontSizeMin = 34f;
            hud.result.title.fontSizeMax = 56f;

            hud.result.summary = Label(At("Summary", card, new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(560f, 64f)),
                                       "Not one zombie got inside the walls.", Face.Body, 23f, HudPalette.Ink,
                                       TextAlignmentOptions.Center, Style.Plain);
            hud.result.summary.textWrappingMode = TextWrappingModes.Normal;

            RectTransform rewards = At("Rewards", card, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(580f, 90f));
            var layout = rewards.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 22f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            hud.result.coins = RewardRow(rewards, "Coins", HudSprites.IconCoin, out hud.result.coinsRow);
            hud.result.xp = RewardRow(rewards, "Experience", HudSprites.IconStar, out hud.result.xpRow);
            hud.result.kits = RewardRow(rewards, "RepairKits", HudSprites.IconKit, out hud.result.kitsRow);

            hud.result.noReward = Label(At("NoReward", card, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(560f, 44f)),
                                        "No reward this time", Face.Body, 22f, HudPalette.InkSoft, TextAlignmentOptions.Center, Style.Plain);
            hud.result.noReward.gameObject.SetActive(false);

            RectTransform safety = At("Safety", card, new Vector2(0.5f, 1f), new Vector2(0f, -266f), new Vector2(500f, 64f));
            Paint(safety, HudSprites.CreamPill, new Color(0.95f, 0.92f, 0.86f), 76f / 64f);
            Paint(Node("Shield", safety, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 2f),
                       new Vector2(64f, 64f)), HudSprites.IconShield, Color.white);
            hud.result.safety = Label(Fill("Line", safety, 76f, 6f, 60f, 4f), "Safety  30  →  38   (+8)", Face.Body, 24f,
                                      HudPalette.Ink, TextAlignmentOptions.Center, Style.Plain);
            RectTransform arrow = Node("Arrow", safety, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                                       new Vector2(-14f, 2f), new Vector2(38f, 38f));
            hud.result.safetyArrow = Paint(arrow, HudSprites.GlyphUp, HudPalette.Mint);

            RectTransform close = At("Continue", card, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 92f));
            Image closeFace = Paint(close, HudSprites.ButtonTeal, Color.white, raycast: true);
            hud.result.close = MakeButton(close, closeFace);
            Motion(close, pressable: true);
            Label(At("Label", close, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(260f, 46f)), "CONTINUE", Face.Display,
                  34f, Color.white, TextAlignmentOptions.Center, Style.Shadow);
            RectTransform timer = At("Timer", close, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(230f, 8f));
            hud.result.timer = Bar(timer, new Color(1f, 1f, 1f, 0.85f), withGhost: false);

            root.gameObject.SetActive(false);
        }

        static HudCounter RewardRow(RectTransform parent, string name, string icon, out GameObject row)
        {
            RectTransform rt = Node(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                    Vector2.zero, new Vector2(170f, 84f));
            row = rt.gameObject;
            HudMotion motion = Motion(rt, pressable: false);

            Paint(Node("Icon", rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                       new Vector2(76f, 76f)), icon, Color.white);
            TMP_Text text = Label(Fill("Amount", rt, 80f, 0f, 0f, 0f), "+40", Face.Display, 36f, HudPalette.Ink,
                                  TextAlignmentOptions.MidlineLeft, Style.Plain);

            var counter = rt.gameObject.AddComponent<HudCounter>();
            counter.Configure(text, motion, "+");
            return counter;
        }

        // --- Building blocks ---------------------------------------------------------------------------

        static RectTransform Node(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
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

        /// <summary>Anchored and pivoted at the same corner, so positions read as offsets from it.</summary>
        static RectTransform At(string name, Transform parent, Vector2 corner, Vector2 position, Vector2 size) =>
            Node(name, parent, corner, corner, corner, position, size);

        /// <summary>Stretched over the parent, inset by the given margins.</summary>
        static RectTransform Fill(string name, Transform parent, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            RectTransform rt = Node(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>
        /// An image from the atlas. Sliced sprites take <paramref name="scale"/> as their pixels-per-unit
        /// multiplier, which shrinks the rounded ends to fit a rect shorter than the sprite.
        /// </summary>
        static Image Paint(RectTransform rt, string sprite, Color colour, float scale = 1f, bool raycast = false)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = skin[sprite];
            image.color = colour;
            image.raycastTarget = raycast;

            bool sliced = image.sprite != null && image.sprite.border != Vector4.zero;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = scale;
            image.preserveAspect = !sliced;
            return image;
        }

        /// <summary>A cream card with its soft shadow; returns the card face.</summary>
        static Image Card(RectTransform root)
        {
            Paint(Fill("Shadow", root, -26f, -34f, -26f, -18f), HudSprites.CardShadow, Color.white);
            return Paint(Fill("Face", root), HudSprites.Card, Color.white, raycast: true);
        }

        static Button MakeButton(RectTransform rt, Graphic target)
        {
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        static HudMotion Motion(RectTransform rt, bool pressable)
        {
            var motion = rt.gameObject.AddComponent<HudMotion>();
            var so = new SerializedObject(motion);
            so.FindProperty("pressable").boolValue = pressable;
            so.ApplyModifiedPropertiesWithoutUndo();
            return motion;
        }

        /// <summary>A track with a fill (and optionally a ghost behind it) inset by three pixels.</summary>
        static HudBar Bar(RectTransform track, Color fill, bool withGhost)
        {
            float height = Mathf.Max(1f, track.rect.height > 0f ? track.rect.height : track.sizeDelta.y);
            Paint(track, HudSprites.BarTrack, Color.white, 48f / height);

            float inset = height >= 16f ? 3f : 1.5f;
            RectTransform area = Fill("Area", track, inset, inset, inset, inset);
            float inner = Mathf.Max(1f, height - inset * 2f);

            RectTransform ghost = null;
            if (withGhost)
            {
                ghost = Fill("Ghost", area);
                Paint(ghost, HudSprites.BarFill, new Color(1f, 0.93f, 0.85f, 0.9f), 48f / inner);
            }

            RectTransform fillRect = Fill("Fill", area);
            Image image = Paint(fillRect, HudSprites.BarFill, fill, 48f / inner);

            var bar = track.gameObject.AddComponent<HudBar>();
            bar.Configure(fillRect, image, ghost);
            return bar;
        }

        static TextMeshProUGUI Label(RectTransform rt, string value, Face face, float size, Color colour,
                                     TextAlignmentOptions alignment, Style style)
        {
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = face == Face.Display ? skin.display : skin.body;

            Material material = style switch
            {
                Style.Outline when face == Face.Display => skin.displayOutline,
                Style.Outline => skin.bodyShadow,
                Style.Shadow when face == Face.Display => skin.displayShadow,
                Style.Shadow => skin.bodyShadow,
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
    }
}
