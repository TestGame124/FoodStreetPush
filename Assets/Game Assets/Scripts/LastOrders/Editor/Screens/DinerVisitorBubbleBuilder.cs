using FoodStreet.Visitors;
using LastOrders.UI.Hud;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static LastOrders.EditorTools.Screens.UiKit;

namespace LastOrders.EditorTools.Screens
{
    /// <summary>
    /// The dish a visitor is waiting for, rebuilt in the screens' look: <see cref="DishBubble"/> - the
    /// dish on a cream disc in a ring that says amber / green / teal, a beak pointing at the visitor,
    /// "x2" on a badge, the pay on a coin chip.
    ///
    /// Added beside the original sprite bubble in every visitor prefab rather than over it: the
    /// original is switched off and kept, and 'Restore Previous Visitor Bubbles' points every visitor
    /// back at it.
    /// </summary>
    public static class DinerVisitorBubbleBuilder
    {
        const string PrefabFolder = "Assets/Game Assets/Content/Visitors/Prefabs";
        const string BubbleName = "DishBubble";
        const string LegacyName = "OrderBubble";

        /// <summary>Metres per canvas unit: a bubble a little under a metre across over the head.</summary>
        const float CanvasScale = 0.008f;

        static readonly Vector3 OverHead = new Vector3(0f, 2.2f, 0f);

        [MenuItem("Last Orders/UI/Build Visitor Dish Bubbles", priority = 217)]
        static void BuildFromMenu() => Debug.Log($"[Last Orders] {Build()}");

        [MenuItem("Last Orders/UI/Restore Previous Visitor Bubbles", priority = 218)]
        static void RestoreFromMenu() => Debug.Log($"[Last Orders] {RestorePrevious()}");

        public static string Build()
        {
            Load();
            int changed = 0;

            foreach (string path in VisitorPrefabs())
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                Visitor visitor = contents.GetComponentInChildren<Visitor>(true);
                if (visitor == null)
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                    continue;
                }

                Transform old = visitor.transform.Find(BubbleName);
                if (old != null) Object.DestroyImmediate(old.gameObject);

                Transform legacy = visitor.transform.Find(LegacyName);
                if (legacy != null) legacy.gameObject.SetActive(false);

                GameObject bubble = Bubble();
                bubble.transform.SetParent(visitor.transform, false);
                bubble.transform.localPosition = OverHead;

                Set(visitor, "bubble", bubble.GetComponent<DishBubble>());

                PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
                changed++;
            }

            return $"Dish bubbles built into {changed} visitor prefabs; the original bubbles are switched off and kept.";
        }

        public static string RestorePrevious()
        {
            int changed = 0;

            foreach (string path in VisitorPrefabs())
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                Visitor visitor = contents.GetComponentInChildren<Visitor>(true);
                Transform legacy = visitor != null ? visitor.transform.Find(LegacyName) : null;
                OrderBubble original = legacy != null ? legacy.GetComponent<OrderBubble>() : null;

                if (original == null)
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                    continue;
                }

                legacy.gameObject.SetActive(true);
                Transform built = visitor.transform.Find(BubbleName);
                if (built != null) built.gameObject.SetActive(false);

                Set(visitor, "bubble", original);

                PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
                changed++;
            }

            return $"{changed} visitor prefabs use their original order bubble again; the dish bubbles are left in place, switched off.";
        }

        static string[] VisitorPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
            var paths = new System.Collections.Generic.List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponentInChildren<Visitor>(true) != null) paths.Add(path);
            }

            return paths.ToArray();
        }

        /// <summary>
        /// The bubble: a world-space canvas that takes no raycasts, with the tap collider on its body.
        /// The ring, the badge and the glow are tinted by <see cref="DishBubble"/>; everything else is
        /// drawn here.
        /// </summary>
        static GameObject Bubble()
        {
            var root = new GameObject(BubbleName, typeof(RectTransform)) { layer = 5 };
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 5;

            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(180f, 200f);
            rt.localScale = Vector3.one * CanvasScale;

            RectTransform body = Fill("Body", rt);
            RectTransform bob = Center("Bob", body, Vector2.zero, new Vector2(180f, 200f));

            Image glow = Paint(Center("Glow", bob, new Vector2(0f, 12f), new Vector2(190f, 190f)), HudSprites.Glow,
                               HudPalette.Mint.WithAlpha(0f));

            // Tucked up under the ring, so it reads as the bubble's own point rather than a separate arrow.
            RectTransform beak = Center("Beak", bob, new Vector2(0f, -40f), new Vector2(50f, 50f));
            beak.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Paint(beak, HudSprites.AlertPointer, HudPalette.Cream);

            Image ring = Paint(Center("Ring", bob, new Vector2(0f, 12f), new Vector2(128f, 128f)), HudSprites.RoundTint, HudPalette.Amber);
            Paint(Center("Disc", bob, new Vector2(0f, 15f), new Vector2(100f, 100f)), HudSprites.Disc, HudPalette.Cream);
            Image icon = Paint(Center("Icon", bob, new Vector2(0f, 16f), new Vector2(80f, 80f)), HudSprites.IconCook, Color.white);

            // "x2", top right.
            RectTransform count = Center("Count", bob, new Vector2(50f, 60f), new Vector2(54f, 54f));
            Paint(count, HudSprites.Badge, Color.white);
            TextMeshProUGUI countText = Label(Fill("Text", count, 0f, 2f, 0f, 0f), "x2", Face.Display, 25f, Color.white,
                                              TextAlignmentOptions.Center, Style.Outline).Fit(16f);

            // The tick or the waiter, top left.
            RectTransform state = Center("State", bob, new Vector2(-50f, 60f), new Vector2(50f, 50f));
            Image badge = Paint(state, HudSprites.RoundTint, HudPalette.Mint);
            Image glyph = Paint(Center("Glyph", state, new Vector2(0f, 2f), new Vector2(30f, 30f)), HudSprites.GlyphCheck, Color.white);

            // What it pays, on a chip at the bottom right.
            RectTransform coins = Center("Coins", bob, new Vector2(46f, -30f), new Vector2(84f, 34f));
            Paint(coins, HudSprites.Chip, HudPalette.Ink.WithAlpha(0.92f), size: new Vector2(84f, 34f));
            Paint(Center("Coin", coins, new Vector2(-26f, 0f), new Vector2(28f, 28f)), HudSprites.IconCoin, Color.white);
            TextMeshProUGUI coinText = Label(Center("Text", coins, new Vector2(12f, 1f), new Vector2(50f, 30f)), "24", Face.Display, 22f,
                                             Color.white, TextAlignmentOptions.MidlineLeft, Style.Plain).Fit(14f);

            RectTransform fx = Fill("Fx", bob);
            fx.SetAsLastSibling();

            foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

            // The tap target: the bubble is what the player is looking at, so it is a tap on the visitor.
            var hit = body.gameObject.AddComponent<BoxCollider>();
            hit.size = new Vector3(130f, 150f, 10f);
            hit.center = new Vector3(0f, 10f, 0f);
            hit.isTrigger = true;

            var view = root.AddComponent<DishBubble>();
            view.Configure(body, bob, ring, glow, icon, count.gameObject, countText, badge, glyph, Skin[HudSprites.GlyphCheck],
                           Skin[HudSprites.IconServe], coins.gameObject, coinText, fx, Skin[HudSprites.IconStar]);
            Set(view, "readyColour", HudPalette.Mint);
            Set(view, "shortColour", HudPalette.Amber);
            Set(view, "waiterColour", HudPalette.Teal);
            Set(view, "flashColour", HudPalette.Red);

            body.gameObject.SetActive(false);
            return root;
        }
    }
}
