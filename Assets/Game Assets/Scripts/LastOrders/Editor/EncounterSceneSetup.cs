using LastOrders.Blockout;
using LastOrders.Core;
using LastOrders.Effects;
using LastOrders.Restaurant;
using LastOrders.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Puts the zombie attack cycle into the open scene: the Safety Reputation meter, the attack
    /// status line, the restaurant's health, the morning card and the repair button on the HUD,
    /// and the world view that draws the restaurant's health over its door, sends the crew out in
    /// the morning and puts a wrench over every wreck.
    ///
    /// Idempotent: run it again and it rebuilds both in place. It never saves the scene.
    /// </summary>
    public static class EncounterSceneSetup
    {
        const string HudName = "EncounterHUD";
        const string WorldName = "Encounters";
        const string DustPath = "Assets/Game Assets/Content/LastOrders/Prefabs/Effects/Fx_BuildDust.prefab";

        static readonly Color Panel = new Color(0.99f, 0.97f, 0.93f, 0.97f);
        static readonly Color Ink = new Color(0.18f, 0.16f, 0.14f, 1f);
        static readonly Color Dark = new Color(0.16f, 0.15f, 0.17f, 0.82f);
        static readonly Color Track = new Color(0.08f, 0.08f, 0.09f, 0.85f);
        static readonly Color Repair = new Color(0.40f, 0.76f, 0.66f, 1f);

        [MenuItem("Last Orders/Encounters/Set Up Zombie Attacks (Current Scene)")]
        static void SetUpFromMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Zombie attacks", "Leave play mode first.", "OK");
                return;
            }

            string report = SetUp();
            Debug.Log($"[Last Orders] {report}");
        }

        /// <summary>Builds both halves. Returns what it did.</summary>
        public static string SetUp()
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            if (scope == null) return "No GameLifetimeScope in the open scene: nothing to set up.";

            CompoundHud hud = Object.FindFirstObjectByType<CompoundHud>(FindObjectsInactive.Include);
            if (hud == null) return "No CompoundHUD in the open scene: run the compound HUD builder first.";

            BuildHud(hud.transform, scope);
            EncounterWorldView view = BuildWorldView(scope);

            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            Selection.activeGameObject = view.gameObject;

            return "Zombie attacks set up: HUD meter, status, health, result card and repair button under " +
                   $"'{hud.name}', and the '{WorldName}' world view. Scene left unsaved.";
        }

        // --- The world view ---------------------------------------------------------------------

        static EncounterWorldView BuildWorldView(GameLifetimeScope scope)
        {
            GameObject root = GameObject.Find(WorldName);
            if (root == null)
            {
                root = new GameObject(WorldName);
                Undo.RegisterCreatedObjectUndo(root, "Zombie attacks");
            }

            EncounterWorldView view = root.GetComponent<EncounterWorldView>();
            if (view == null) view = Undo.AddComponent<EncounterWorldView>(root);

            var dust = AssetDatabase.LoadAssetAtPath<GameObject>(DustPath);
            PooledEffect effect = dust != null ? dust.GetComponent<PooledEffect>() : null;
            if (effect == null) Debug.LogWarning($"[Last Orders] No dust effect at {DustPath}: wrecks will have no puff.");

            Undo.RecordObject(view, "Zombie attacks");
            view.Configure(scope, effect, effect);
            EditorUtility.SetDirty(view);

            return view;
        }

        // --- The HUD ------------------------------------------------------------------------------

        static void BuildHud(Transform compoundHud, GameLifetimeScope scope)
        {
            Transform old = compoundHud.Find(HudName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var root = new GameObject(HudName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Zombie attacks");
            root.transform.SetParent(compoundHud, false);
            Stretch(root.GetComponent<RectTransform>());

            // Drawn over the rest of the HUD, so the morning card sits on top of everything.
            root.transform.SetAsLastSibling();

            // The meter: top right, under the level's own wallet - the top centre belongs to the day
            // pill and the level's XP bar.
            RectTransform meter = Rect("SafetyMeter", root.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                                       new Vector2(-28f, -222f), new Vector2(420f, 54f));
            meter.pivot = new Vector2(1f, 1f);
            Pill(meter, Dark);

            RectTransform caption = Rect("Caption", meter, new Vector2(0f, 0f), new Vector2(0f, 1f),
                                         new Vector2(18f, 0f), new Vector2(110f, 0f));
            caption.pivot = new Vector2(0f, 0.5f);
            Label(caption, "SAFETY", 22, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.75f));

            Image meterFill = Bar(meter, new Vector2(122f, 0f), new Vector2(-74f, 0f), 18f);

            RectTransform value = Rect("Value", meter, new Vector2(1f, 0f), new Vector2(1f, 1f),
                                       new Vector2(-16f, 0f), new Vector2(54f, 0f));
            value.pivot = new Vector2(1f, 0.5f);
            Text meterValue = Label(value, "30", 30, TextAnchor.MiddleRight, Color.white);

            // The line under it: next attack, the countdown, the wave.
            RectTransform status = Rect("Status", root.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                                        new Vector2(-28f, -284f), new Vector2(560f, 46f));
            status.pivot = new Vector2(1f, 1f);
            Image statusPanel = Pill(status, Dark);
            Text statusText = Label(status, "Next zombie attack in 3 days  ·  Low threat", 22, TextAnchor.MiddleCenter, Color.white);

            // The restaurant's health, while an attack is on.
            RectTransform health = Rect("RestaurantHealth", root.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                                        new Vector2(-28f, -340f), new Vector2(420f, 42f));
            health.pivot = new Vector2(1f, 1f);
            Pill(health, Dark);
            Image healthFill = Bar(health, new Vector2(12f, 0f), new Vector2(-12f, 0f), 26f);
            Text healthText = Label(health, "RESTAURANT  400/400", 20, TextAnchor.MiddleCenter, Color.white);
            var shadow = healthText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            health.gameObject.SetActive(false);

            // The morning card.
            RectTransform card = Rect("Result", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                      new Vector2(0f, 90f), new Vector2(640f, 260f));
            Pill(card, Panel);
            Button close = card.gameObject.AddComponent<Button>();

            RectTransform header = Rect("Header", card, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                        Vector2.zero, new Vector2(0f, 78f));
            header.pivot = new Vector2(0.5f, 1f);
            Image headerImage = Pill(header, new Color(0.30f, 0.62f, 0.32f, 1f));
            Text title = Label(header, "VICTORY", 46, TextAnchor.MiddleCenter, Color.white);
            title.fontStyle = FontStyle.Bold;

            RectTransform body = Rect("Body", card, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            body.offsetMin = new Vector2(28f, 44f);
            body.offsetMax = new Vector2(-28f, -92f);
            Text bodyText = Label(body, "Not one zombie got inside the walls.\n+40 Coins   +20 XP   +1 Repair Kit\nSafety +8  (38/100)",
                                  26, TextAnchor.MiddleCenter, Ink);
            bodyText.lineSpacing = 1.15f;

            RectTransform hint = Rect("Hint", card, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 12f), new Vector2(0f, 28f));
            hint.pivot = new Vector2(0.5f, 0f);
            Label(hint, "Tap to close", 18, TextAnchor.MiddleCenter, new Color(Ink.r, Ink.g, Ink.b, 0.5f));
            card.gameObject.SetActive(false);

            // The one repair, just above the objective card, clear of the level's own buttons along the
            // bottom of the screen.
            RectTransform repair = Rect("RepairWrecks", root.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                                        new Vector2(28f, 150f), new Vector2(360f, 86f));
            repair.pivot = new Vector2(0f, 0f);
            Pill(repair, Repair);
            Text repairText = Label(repair, "Repair 2 wrecks\n<size=22>1 Repair Kit  ·  1 kit held</size>", 28, TextAnchor.MiddleCenter, Ink);
            Button repairButton = repair.gameObject.AddComponent<Button>();
            repair.gameObject.SetActive(false);

            var encounterHud = root.AddComponent<EncounterHud>();
            encounterHud.Configure(scope, meterFill, meterValue, statusPanel, statusText,
                                   health.gameObject, healthFill, healthText, card.gameObject, headerImage,
                                   title, bodyText, close, repairButton, repairText);
            EditorUtility.SetDirty(encounterHud);
        }

        /// <summary>A dark track with a horizontal fill inside it, the fill returned.</summary>
        static Image Bar(RectTransform parent, Vector2 left, Vector2 right, float height)
        {
            RectTransform track = Rect("Track", parent, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero,
                                       new Vector2(0f, height));
            track.offsetMin = new Vector2(left.x, -height * 0.5f);
            track.offsetMax = new Vector2(right.x, height * 0.5f);
            Pill(track, Track);

            RectTransform fill = Rect("Fill", track, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fill.offsetMin = new Vector2(3f, 3f);
            fill.offsetMax = new Vector2(-3f, -3f);

            Image image = Pill(fill, new Color(0.96f, 0.76f, 0.28f));
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 0.3f;
            image.raycastTarget = false;
            return image;
        }

        // --- Primitives, as the compound HUD's builder makes them ---------------------------------

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

        static Image Pill(RectTransform rt, Color colour)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.color = colour;
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
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
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return text;
        }
    }
}
