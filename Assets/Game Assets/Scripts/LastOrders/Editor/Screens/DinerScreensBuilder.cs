using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastOrders.UI.Hud;
using LastOrders.UI.Kit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static LastOrders.EditorTools.Screens.UiKit;

namespace LastOrders.EditorTools.Screens
{
    /// <summary>
    /// Builds the game's screens again in the HUD's look - the shop drawer, the build and move
    /// buttons, the kitchen, the seed tray, the storehouse, the orders, the level-up, the guide, the
    /// naming window, the store and the little notices - on a canvas of their own, and points every
    /// reference the game holds to an old screen at its new one.
    ///
    /// The screens' own scripts are reused as they are (<see cref="UiItemsWindow"/>,
    /// <see cref="CookingUIPanel"/>, <see cref="OrdersBoardUI"/> ...), so the game plays exactly as it
    /// did; only what is drawn, how it is laid out and how it answers a tap change. The templates the
    /// scripts instantiate (shop cards, recipe cards, order tickets, item slots) are written as new
    /// prefabs beside the old ones.
    ///
    /// Nothing old is deleted. The old panels stay in the scene, switched off as they always were;
    /// 'Restore Previous Screens' points everything back at them.
    /// </summary>
    public static partial class DinerScreensBuilder
    {
        const string RootName = "DinerScreens";
        const int SortingOrder = 20;
        public const string PrefabFolder = "Assets/Game Assets/Content/LastOrders/Prefabs/UI/Screens";

        [MenuItem("Last Orders/UI/Build Diner Screens (Current Scene)", priority = 210)]
        static void BuildFromMenu() => Debug.Log($"[Last Orders] {Build()}");

        [MenuItem("Last Orders/UI/Restore Previous Screens (Current Scene)", priority = 211)]
        static void RestoreFromMenu() => Debug.Log($"[Last Orders] {RestorePrevious()}");

        /// <summary>Old object -> its replacement, gathered while building.</summary>
        sealed class Swap
        {
            public readonly List<Object> previous = new List<Object>();
            public readonly List<Object> replacements = new List<Object>();

            public void Add(Object old, Object replacement)
            {
                if (old == null || replacement == null) return;
                previous.Add(old);
                replacements.Add(replacement);
            }

            /// <summary>The old panel and its script, both pointed at the new ones.</summary>
            public void Panel<T>(GameObject old, T replacement) where T : Component
            {
                if (old == null || replacement == null) return;
                Add(old, replacement.gameObject);
                T oldScript = old.GetComponent<T>();
                if (oldScript != null) Add(oldScript, replacement);
                Add(old.transform, replacement.transform);
            }
        }

        public static string Build()
        {
            Load();

            UIGame ui = Object.FindFirstObjectByType<UIGame>(FindObjectsInactive.Include);
            if (ui == null) return "No UIGame in the open scene - nothing to rebuild.";

            // A rebuild starts from the old screens: undo the last swap first.
            foreach (ScreensReplaced previousBuild in Object.FindObjectsByType<ScreensReplaced>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                PointBack(previousBuild);
                UnityEditor.Undo.DestroyObjectImmediate(previousBuild.gameObject);
            }

            Directory.CreateDirectory(PrefabFolder);

            Canvas canvas = CreateCanvas();
            var root = (RectTransform)canvas.transform;
            UnityEditor.Undo.RegisterCreatedObjectUndo(canvas.gameObject, "Build Diner Screens");

            var swap = new Swap();
            Transform legacy = ui.transform;

            BuildShop(ui, legacy, root, swap);
            BuildPlacement(legacy, root, swap);
            BuildSeedTray(legacy, root, swap);
            GameObject kitchen = BuildKitchen(legacy, root, swap);
            GameObject storehouse = BuildStorehouse(legacy, root, swap);
            GameObject orders = BuildOrders(legacy, root, swap);
            GameObject levelUp = BuildLevelUp(legacy, root, swap);
            GameObject guide = BuildGuide(legacy, root, swap);
            GameObject naming = BuildNaming(legacy, root, swap);
            GameObject store = BuildStore(ui, legacy, root, swap);
            BuildNotices(legacy, root, swap);

            var record = canvas.gameObject.AddComponent<ScreensReplaced>();
            record.previous = swap.previous.ToArray();
            record.replacements = swap.replacements.ToArray();

            // What the HUD stepped aside for before the swap, for restoring.
            GameplayHud hud = Object.FindFirstObjectByType<GameplayHud>(FindObjectsInactive.Include);
            if (hud != null) record.hudCovering = (GameObject[])hud.coveringPanels.Clone();

            int pointed = Remap(swap.previous, swap.replacements, canvas.transform, legacy);

            // The HUD steps aside for the windows; the notices are small enough to share the screen.
            if (hud != null)
            {
                UnityEditor.Undo.RecordObject(hud, "Build Diner Screens");
                hud.coveringPanels = new[] { kitchen, storehouse, orders, levelUp, guide, naming, store }.Where(g => g != null).ToArray();
                EditorUtility.SetDirty(hud);
            }

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = canvas.gameObject;

            return $"Diner screens built on '{RootName}' (sort {SortingOrder}): {swap.previous.Count / 3} screens, " +
                   $"{pointed} references moved over; templates in {PrefabFolder}. Old panels left in place. Scene left unsaved.";
        }

        public static string RestorePrevious()
        {
            ScreensReplaced[] builds = Object.FindObjectsByType<ScreensReplaced>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (builds.Length == 0) return "No diner screens in this scene - nothing to restore.";

            int pointed = 0;
            foreach (ScreensReplaced build in builds)
            {
                pointed += PointBack(build);
                UnityEditor.Undo.RecordObject(build.gameObject, "Restore Previous Screens");
                build.gameObject.SetActive(false);
            }

            EditorSceneManager.MarkSceneDirty(builds[0].gameObject.scene);
            return $"Previous screens restored ({pointed} references pointed back); '{RootName}' switched off, not deleted. " +
                   "Re-run Build Diner Screens to bring it back. Scene left unsaved.";
        }

        /// <summary>Points everything back at the old screens.</summary>
        static int PointBack(ScreensReplaced build)
        {
            UIGame ui = Object.FindFirstObjectByType<UIGame>(FindObjectsInactive.Include);
            int pointed = Remap(build.replacements.ToList(), build.previous.ToList(), build.transform, ui != null ? ui.transform : null);

            if (ui != null) Set(ui, "storeCards", (Object)null);

            GameplayHud hud = Object.FindFirstObjectByType<GameplayHud>(FindObjectsInactive.Include);
            if (hud != null)
            {
                GameObject[] covering = build.hudCovering;
                if (covering == null || covering.Length == 0 || covering.Any(g => g == null || g.transform.IsChildOf(build.transform)))
                    covering = LegacyCovering(ui != null ? ui.transform : null);

                UnityEditor.Undo.RecordObject(hud, "Restore Previous Screens");
                hud.coveringPanels = covering;
                EditorUtility.SetDirty(hud);
            }

            return pointed;
        }

        /// <summary>The old full-screen panels the HUD stepped aside for, found by name as the HUD builder found them.</summary>
        static GameObject[] LegacyCovering(Transform legacy) =>
            new[] { "CookingPanel", "Inventory", "OrdersPanel", "NotEnoughPanel", "LevelUpPanel", "GuidePanel", "SignBoardPanel", "Buy Ingredients", "InApp" }
                .Select(n => Old(legacy, n)).Where(g => g != null).ToArray();

        /// <summary>
        /// Every object reference in the scene's behaviours that points at something in
        /// <paramref name="from"/> is pointed at the matching entry of <paramref name="to"/>. The
        /// screens themselves are skipped - their insides refer to their own parts.
        /// </summary>
        static int Remap(List<Object> from, List<Object> to, Transform skipA, Transform skipB)
        {
            var map = new Dictionary<Object, Object>();
            for (int i = 0; i < from.Count && i < to.Count; i++)
                if (from[i] != null && to[i] != null) map[from[i]] = to[i];

            // Inside the old panels and the new canvas, references stay as they are.
            var skip = new HashSet<Transform>();
            foreach (Object o in from.Concat(to))
            {
                if (o is GameObject go) skip.Add(go.transform);
            }

            bool Skipped(Transform t)
            {
                for (Transform p = t; p != null; p = p.parent)
                {
                    if (p == skipA) return true;
                    if (skip.Contains(p) && p != skipB) return true;
                }

                return false;
            }

            int count = 0;
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null || Skipped(behaviour.transform)) continue;
                if (EditorUtility.IsPersistent(behaviour)) continue;

                var so = new SerializedObject(behaviour);
                SerializedProperty it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object value = it.objectReferenceValue;
                    if (value == null || !map.TryGetValue(value, out Object replacement)) continue;

                    it.objectReferenceValue = replacement;
                    changed = true;
                    count++;
                }

                if (changed) so.ApplyModifiedProperties();
            }

            return count;
        }

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
            return canvas;
        }

        // --- Shared pieces -------------------------------------------------------------------------

        static GameObject Old(Transform legacy, string name)
        {
            Transform t = legacy != null ? legacy.Find(name) : null;
            return t != null ? t.gameObject : null;
        }

        /// <summary>A full-screen screen root, switched off like the panel it replaces.</summary>
        static RectTransform ScreenRoot(RectTransform canvas, string name) => Fill(name, canvas);

        /// <summary>
        /// A hidden legacy <see cref="Text"/> for an old script to keep writing into, shown through a
        /// styled label by <see cref="UiTextMirror"/>.
        /// </summary>
        static Text HiddenText(RectTransform parent, string name, TMPro.TMP_Text shownIn, string format = "{0}", bool numberOnly = false)
        {
            RectTransform rt = At(name, parent, Middle, Vector2.zero, new Vector2(10f, 10f));
            var text = rt.gameObject.AddComponent<Text>();
            text.enabled = false;
            text.raycastTarget = false;
            var mirror = rt.gameObject.AddComponent<UiTextMirror>();
            mirror.Configure(text, shownIn, format, numberOnly);
            return text;
        }

        /// <summary>Writes a template built in the scene as a prefab and removes the scene copy.</summary>
        static T SavePrefab<T>(GameObject built, string fileName) where T : Component
        {
            string path = $"{PrefabFolder}/{fileName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(built, path);
            Object.DestroyImmediate(built);
            return prefab.GetComponent<T>();
        }

        /// <summary>A scratch parent for building a template, with a canvas so layouts resolve.</summary>
        static RectTransform TemplateRoot(string name, Vector2 size)
        {
            RectTransform rt = Node(name, null, Middle, Middle, Middle, Vector2.zero, size);
            return rt;
        }

        static ScrollRect Scroll(RectTransform area, bool horizontal, out RectTransform content)
        {
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = horizontal;
            scroll.vertical = !horizontal;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.12f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 30f;

            RectTransform viewport = Fill("Viewport", area);
            Paint(viewport, HudSprites.White, new Color(1f, 1f, 1f, 0f), raycast: true);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            content = horizontal
                ? Node("Content", viewport, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero)
                : Node("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            scroll.content = content;
            return scroll;
        }

        static void FitContent(RectTransform content, bool horizontal)
        {
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = horizontal ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = horizontal ? ContentSizeFitter.FitMode.Unconstrained : ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>A centred message for an empty list: an icon, a heading and a line under it.</summary>
        static GameObject EmptyMessage(RectTransform parent, string icon, string heading, string line, float y = 0f)
        {
            RectTransform rt = Center("Empty", parent, new Vector2(0f, y), new Vector2(760f, 260f));
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            if (!string.IsNullOrEmpty(icon))
                Paint(Center("Icon", rt, new Vector2(0f, 64f), new Vector2(120f, 120f)), icon, new Color(1f, 1f, 1f, 0.75f));
            Label(Center("Heading", rt, new Vector2(0f, -22f), new Vector2(760f, 50f)), heading, Face.Display, 36f, HudPalette.Ink);
            Label(Center("Line", rt, new Vector2(0f, -66f), new Vector2(760f, 40f)), line, Face.Body, 24f, HudPalette.InkSoft)
                .Fit(16f, wrap: true);
            return rt.gameObject;
        }
    }
}
