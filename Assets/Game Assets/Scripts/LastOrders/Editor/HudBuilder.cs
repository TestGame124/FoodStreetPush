using System;
using LastOrders.Data;
using LastOrders.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Builds the Last Orders HUD into an existing Canvas.
    ///
    /// Constructed in code rather than authored as a prefab so that it can be regenerated
    /// after any change, and so the layout is reviewable as a diff. The GDD's top bar is a
    /// short list - day, clock arc, Reputation, Heat bar with forecast marker - and that list
    /// reads more clearly here than it would as serialised RectTransform values.
    ///
    /// Legacy uGUI Text rather than TextMeshPro, deliberately: the project's existing UI uses
    /// it (UIGame holds a UnityEngine.UI.Text), and the builtin font needs no imported TMP
    /// essentials to render. Swapping to TMP later is a mechanical change confined to here.
    /// </summary>
    public static class HudBuilder
    {
        static readonly Color Warm = new Color(1f, 0.93f, 0.82f);
        static readonly Color Dim = new Color(1f, 1f, 1f, 0.6f);
        static readonly Color PanelBg = new Color(0.09f, 0.10f, 0.13f, 0.88f);
        static readonly Color HeatColour = new Color(0.95f, 0.45f, 0.2f);

        static Font font;

        static Font Font =>
            font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>Builds the HUD under <paramref name="canvas"/> and returns the bound component.</summary>
        public static LastOrdersHUD Build(Canvas canvas, GameLifetimeScopeRef scopeRef)
        {
            // Rebuilt from scratch each run so re-running the setup does not stack two HUDs.
            Transform existing = canvas.transform.Find("LastOrdersHUD");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            GameObject root = CreateRect("LastOrdersHUD", canvas.transform);
            Stretch(root.GetComponent<RectTransform>());

            var hud = root.AddComponent<LastOrdersHUD>();

            var so = new SerializedObject(hud);
            so.FindProperty("scope").objectReferenceValue = scopeRef.Scope;

            BuildTopBar(root.transform, so);
            BuildBanner(root.transform, so);
            BuildDawnPanel(root.transform, so, hud);
            BuildCrisisBar(root.transform, so, hud);

            so.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        // --- Top bar (GDD 23) ---------------------------------------------------

        static void BuildTopBar(Transform parent, SerializedObject so)
        {
            GameObject bar = CreatePanel("TopBar", parent, PanelBg);
            RectTransform rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 150f);

            Text day = CreateText("DayText", bar.transform, "DAY 1", 34, TextAnchor.UpperLeft, Warm);
            Place(day, new Vector2(24f, -16f), new Vector2(260f, 44f), new Vector2(0f, 1f));

            Text phase = CreateText("PhaseText", bar.transform, "DAWN", 20, TextAnchor.UpperLeft, Dim);
            Place(phase, new Vector2(24f, -60f), new Vector2(260f, 28f), new Vector2(0f, 1f));

            // The clock is an arc, not digits: it has to communicate "time remaining" at a
            // glance, because every build cost in the game is priced against it.
            Image clock = CreateImage("ClockRing", bar.transform, Warm);
            clock.type = Image.Type.Filled;
            clock.fillMethod = Image.FillMethod.Radial360;
            clock.fillOrigin = (int)Image.Origin360.Top;
            clock.fillClockwise = true;
            clock.fillAmount = 1f;
            Place(clock, new Vector2(320f, -20f), new Vector2(72f, 72f), new Vector2(0f, 1f));

            Text clockText = CreateText("ClockText", bar.transform, "Paused", 16, TextAnchor.UpperLeft, Dim);
            Place(clockText, new Vector2(404f, -38f), new Vector2(200f, 26f), new Vector2(0f, 1f));

            BuildHeatBar(bar.transform, so);

            Text rep = CreateText("ReputationText", bar.transform, "REP 0", 18, TextAnchor.UpperRight, Warm);
            Place(rep, new Vector2(-24f, -16f), new Vector2(320f, 26f), new Vector2(1f, 1f));

            Text resources = CreateText("ResourcesText", bar.transform, "", 16, TextAnchor.UpperRight, Dim);
            Place(resources, new Vector2(-24f, -46f), new Vector2(520f, 26f), new Vector2(1f, 1f));

            so.FindProperty("dayText").objectReferenceValue = day;
            so.FindProperty("phaseText").objectReferenceValue = phase;
            so.FindProperty("clockFill").objectReferenceValue = clock;
            so.FindProperty("clockText").objectReferenceValue = clockText;
            so.FindProperty("reputationText").objectReferenceValue = rep;
            so.FindProperty("resourcesText").objectReferenceValue = resources;
        }

        /// <summary>
        /// The Heat bar and its forecast marker - the single most important readout in the
        /// game. The marker shows which wave tier tonight lands in if the player stops
        /// trading now, which is what puts a visible price on "one more table before dusk".
        /// </summary>
        static void BuildHeatBar(Transform parent, SerializedObject so)
        {
            GameObject track = CreatePanel("HeatBar", parent, new Color(1f, 1f, 1f, 0.12f));
            RectTransform trackRect = track.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 1f);
            trackRect.anchorMax = new Vector2(0f, 1f);
            trackRect.pivot = new Vector2(0f, 1f);
            trackRect.anchoredPosition = new Vector2(24f, -100f);
            trackRect.sizeDelta = new Vector2(560f, 22f);

            Image fill = CreateImage("HeatFill", track.transform, HeatColour);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            Stretch(fill.rectTransform);

            GameObject marker = CreateRect("ForecastMarker", track.transform);
            var markerImage = marker.AddComponent<Image>();
            markerImage.color = Warm;
            RectTransform markerRect = marker.GetComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(0f, 0f);
            markerRect.anchorMax = new Vector2(0f, 1f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(3f, 10f);
            markerRect.anchoredPosition = Vector2.zero;

            Text heatText = CreateText("HeatText", parent, "HEAT 0", 16, TextAnchor.UpperLeft, HeatColour);
            Place(heatText, new Vector2(24f, -126f), new Vector2(420f, 24f), new Vector2(0f, 1f));

            so.FindProperty("heatFill").objectReferenceValue = fill;
            so.FindProperty("heatText").objectReferenceValue = heatText;
            so.FindProperty("forecastMarker").objectReferenceValue = markerRect;
            so.FindProperty("heatBarRect").objectReferenceValue = trackRect;
        }

        // --- Banner --------------------------------------------------------------

        static void BuildBanner(Transform parent, SerializedObject so)
        {
            GameObject banner = CreatePanel("PhaseBanner", parent, new Color(0f, 0f, 0f, 0.55f));
            RectTransform rect = banner.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 120f);

            Text text = CreateText("BannerText", banner.transform, "DAWN", 52, TextAnchor.MiddleCenter, Warm);
            Stretch(text.rectTransform);

            banner.SetActive(false);

            so.FindProperty("phaseBanner").objectReferenceValue = banner;
            so.FindProperty("bannerText").objectReferenceValue = text;
        }

        // --- Dawn (GDD 23, 15.3, 15.10) ------------------------------------------

        static void BuildDawnPanel(Transform parent, SerializedObject so, LastOrdersHUD hud)
        {
            GameObject panel = CreatePanel("DawnPanel", parent, PanelBg);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(760f, 460f);

            Text title = CreateText("Title", panel.transform, "DAWN", 32, TextAnchor.UpperCenter, Warm);
            Place(title, new Vector2(0f, -24f), new Vector2(700f, 40f), new Vector2(0.5f, 1f));

            // The radio broadcast. 16.1 makes this the player's whole basis for building
            // against tonight, so it gets real estate rather than a corner.
            Text broadcast = CreateText("Broadcast", panel.transform,
                "\"Static...\"", 20, TextAnchor.UpperCenter, new Color(0.7f, 0.85f, 1f));
            Place(broadcast, new Vector2(0f, -76f), new Vector2(700f, 90f), new Vector2(0.5f, 1f));

            Text report = CreateText("Report", panel.transform, "", 18, TextAnchor.UpperLeft, Dim);
            Place(report, new Vector2(0f, -180f), new Vector2(700f, 150f), new Vector2(0.5f, 1f));

            CreateButton("OpenButton", panel.transform, "OPEN FOR BUSINESS",
                         new Vector2(0f, 36f), new Vector2(320f, 56f), hud.OnOpenForBusiness);

            CreateButton("RebuildButton", panel.transform, "REBUILD ALL",
                         new Vector2(-170f, -36f), new Vector2(300f, 48f), hud.OnRebuildAll);

            CreateButton("RepairsButton", panel.transform, "CLOSED FOR REPAIRS",
                         new Vector2(170f, -36f), new Vector2(300f, 48f), hud.OnClosedForRepairs);

            so.FindProperty("dawnPanel").objectReferenceValue = panel;
            so.FindProperty("dawnReportText").objectReferenceValue = report;
            so.FindProperty("broadcastText").objectReferenceValue = broadcast;
        }

        // --- Crisis Bar (GDD 13.3) ------------------------------------------------

        static void BuildCrisisBar(Transform parent, SerializedObject so, LastOrdersHUD hud)
        {
            GameObject bar = CreatePanel("CrisisBar", parent, PanelBg);
            RectTransform rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 150f);

            Text status = CreateText("NightStatus", bar.transform, "", 18, TextAnchor.UpperCenter, Dim);
            Place(status, new Vector2(0f, -10f), new Vector2(600f, 26f), new Vector2(0.5f, 1f));

            // Seven verbs, one grammar. Laid out in a single row because the bar has to be
            // operable one-handed in portrait.
            var verbs = (CrisisVerb[])Enum.GetValues(typeof(CrisisVerb));
            float spacing = 130f;
            float start = -(verbs.Length - 1) * spacing * 0.5f;

            for (int i = 0; i < verbs.Length; i++)
            {
                Button button = CreateButton($"Crisis_{verbs[i]}", bar.transform,
                                             verbs[i].ToString().ToUpperInvariant(),
                                             new Vector2(start + i * spacing, 46f),
                                             new Vector2(120f, 60f), null, 16);

                // Bound with the int overload rather than a lambda. A persistent listener is
                // serialised as a target plus a method name, so a closure cannot be one - it
                // would save as an empty entry and the button would silently do nothing.
                UnityEventTools.AddIntPersistentListener(button.onClick, hud.OnCrisisVerb, i);
            }

            CreateButton("SkipDusk", bar.transform, "SKIP",
                         new Vector2(0f, -110f), new Vector2(160f, 40f), hud.OnSkipDusk, 16);

            bar.SetActive(false);

            so.FindProperty("crisisBar").objectReferenceValue = bar;
            so.FindProperty("nightStatusText").objectReferenceValue = status;
        }

        // --- Primitives ------------------------------------------------------------

        static GameObject CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static GameObject CreatePanel(string name, Transform parent, Color colour)
        {
            GameObject go = CreateRect(name, parent);
            var image = go.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return go;
        }

        static Image CreateImage(string name, Transform parent, Color colour)
        {
            GameObject go = CreateRect(name, parent);
            var image = go.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        static Text CreateText(string name, Transform parent, string content, int size,
                               TextAnchor anchor, Color colour)
        {
            GameObject go = CreateRect(name, parent);

            var text = go.AddComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.text = content;
            text.alignment = anchor;
            text.color = colour;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;

            return text;
        }

        static Button CreateButton(string name, Transform parent, string label, Vector2 position,
                                   Vector2 size, UnityEngine.Events.UnityAction action, int fontSize = 20)
        {
            GameObject go = CreateRect(name, parent);

            var image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.16f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = CreateText("Label", go.transform, label, fontSize, TextAnchor.MiddleCenter, Warm);
            Stretch(text.rectTransform);

            // Persistent rather than a runtime AddListener, so the wiring is visible in the
            // inspector and survives being saved into the scene.
            if (action != null) UnityEventTools.AddVoidPersistentListener(button.onClick, action);

            return button;
        }

        static void Place(Graphic graphic, Vector2 position, Vector2 size, Vector2 anchor)
        {
            RectTransform rect = graphic.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Small carrier so the builder can take the scope without the scene builder
    /// having to pass a half-constructed object graph around.</summary>
    public sealed class GameLifetimeScopeRef
    {
        public LastOrders.Core.GameLifetimeScope Scope;
    }
}
