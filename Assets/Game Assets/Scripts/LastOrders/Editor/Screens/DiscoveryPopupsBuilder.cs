using System.IO;
using LastOrders.UI.Hud;
using LastOrders.UI.Popups;
using LastOrders.UI.World;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static LastOrders.EditorTools.Screens.UiKit;
using P = LastOrders.EditorTools.Screens.DinerPopupsBuilder;

namespace LastOrders.EditorTools.Screens
{
    /// <summary>
    /// The discovery sites' screens and buttons, in the diner popups' look: the drive-through window
    /// - the plates on the pass along the top, a card for each car's bag below - and the food truck -
    /// the loading list on the left, a little map of the road on the right - plus the round buttons
    /// that float over a site: one for a site at work, a question mark over one still to be found,
    /// and the red alarm over the noise maker in an attack.
    ///
    /// Built from scratch every run and put in the UI catalog. The range, the noise maker and the
    /// "Discovered!" card use the game's existing pick-one popup, so they need nothing here.
    /// </summary>
    public static class DiscoveryPopupsBuilder
    {
        public const string Folder = DinerScreensBuilder.PrefabFolder + "/Discovery";

        static readonly Color Pale = new Color(0.72f, 0.8f, 0.58f);
        static readonly Color Road = new Color(0.33f, 0.33f, 0.35f);

        [MenuItem("Last Orders/UI/Build Discovery Popups", priority = 214)]
        static void BuildFromMenu() => Debug.Log($"[Last Orders] {Build()}");

        public static string Build()
        {
            Load();
            Directory.CreateDirectory(Folder);

            var catalog = AssetDatabase.LoadAssetAtPath<WorldUiCatalog>(WorldUiPrefabGenerator.CatalogPath);
            if (catalog == null) return $"No UI catalog at {WorldUiPrefabGenerator.CatalogPath} - run the world UI setup first.";

            Undo.RecordObject(catalog, "Build Discovery Popups");
            catalog.takeawayPopup = Save<TakeawayPopupView>(BuildTakeaway(), "UI_TakeawayPopup");
            catalog.foodTruckPopup = Save<FoodTruckPopupView>(BuildFoodTruck(), "UI_FoodTruckPopup");

            catalog.sitePrompt = Save<ActionPromptView>(
                DinerWorldUiBuilder.Prompt("WorldUI_SitePrompt", HudSprites.RoundCream, HudSprites.IconServe, null, HudPalette.Teal, standing: true),
                "WorldUI_SitePrompt");
            catalog.mysteryPrompt = Save<ActionPromptView>(
                DinerWorldUiBuilder.Prompt("WorldUI_MysteryPrompt", HudSprites.RoundTeal, HudSprites.GlyphInfo, "???", HudPalette.TealDark, standing: true),
                "WorldUI_MysteryPrompt");
            catalog.alarmPrompt = Save<ActionPromptView>(
                DinerWorldUiBuilder.Prompt("WorldUI_AlarmPrompt", HudSprites.RoundRed, HudSprites.IconZombie, "SOUND IT", HudPalette.RedDark, standing: true,
                                           releaseOnPress: true),
                "WorldUI_AlarmPrompt");

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            return $"Discovery popups and prompts built in {Folder} and put in the UI catalog.";
        }

        static T Save<T>(GameObject built, string name) where T : Component
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(built, $"{Folder}/{name}.prefab");
            Object.DestroyImmediate(built);
            return prefab.GetComponent<T>();
        }

        /// <summary>A caption over a block of the popup, small capitals in the soft ink, left aligned.</summary>
        static void Caption(RectTransform panel, string name, Vector2 at, float width, string text) =>
            Label(Center(name, panel, at, new Vector2(width, 36f)), text, Face.Display, 24f, HudPalette.InkSoft, TextAlignmentOptions.Left);

        // --- The drive-through -------------------------------------------------------------------------

        static GameObject BuildTakeaway()
        {
            P.Frame f = P.Popup("UI_TakeawayPopup", new Vector2(1400f, 900f), "Drive-through", HudSprites.IconServe);
            RectTransform p = f.panel;

            Caption(p, "PassCaption", new Vector2(-390f, 318f), 600f, "ON THE PASS");
            var tray = new DishTileView[8];
            for (int i = 0; i < tray.Length; i++) tray[i] = DishTile(p, $"Pass_{i}", new Vector2(-560f + i * 160f, 222f), new Vector2(140f, 150f), true);
            TextMeshProUGUI trayEmpty = P.Para(p, "PassEmpty", new Vector2(0f, 222f), new Vector2(1100f, 60f), Face.Body, 26f, HudPalette.InkSoft);
            trayEmpty.text = "Nothing cooked on the pass yet. Cook something for the cars.";

            Caption(p, "LaneCaption", new Vector2(-390f, 118f), 600f, "IN THE LANE");
            var bags = new TakeawayBagView[3];
            for (int i = 0; i < bags.Length; i++) bags[i] = Bag(p, i, new Vector2(-440f + i * 440f, -118f));
            TextMeshProUGUI laneEmpty = P.Para(p, "LaneEmpty", new Vector2(0f, -118f), new Vector2(1100f, 60f), Face.Body, 26f, HudPalette.InkSoft);
            laneEmpty.text = "No cars in the lane yet.";

            TextMeshProUGUI status = P.Para(p, "Status", new Vector2(0f, -398f), new Vector2(1300f, 44f), Face.Body, 24f, HudPalette.InkSoft);

            var view = f.root.AddComponent<TakeawayPopupView>();
            P.WireFrame(view, f);
            SetArray(view, "tray", tray);
            Set(view, "trayEmpty", trayEmpty);
            SetArray(view, "bags", bags);
            Set(view, "laneEmpty", laneEmpty);
            Set(view, "status", status);
            P.Colours(view, P.Ready, P.Blocked);
            Set(view, "calmColour", HudPalette.Mint);
            Set(view, "hurryColour", HudPalette.Amber);
            Set(view, "leavingColour", HudPalette.Red);
            return f.root;
        }

        /// <summary>
        /// A dish as a tile: its picture over a sunken tile, a count on a chip in the corner, its name
        /// under it, a tick when it is done and a teal ring when it is the one to press.
        /// </summary>
        static DishTileView DishTile(RectTransform parent, string name, Vector2 at, Vector2 size, bool named)
        {
            RectTransform rt = Center(name, parent, at, size);
            var group = rt.gameObject.AddComponent<CanvasGroup>();

            RectTransform glowRect = Center("Glow", rt, Vector2.zero, size + new Vector2(14f, 14f));
            Paint(glowRect, HudSprites.Tile, HudPalette.Teal, size: size + new Vector2(14f, 14f));

            Image face = Tile(Center("Face", rt, Vector2.zero, size), size, raycast: true);
            Button button = MakeButton((RectTransform)face.transform, face);
            Motion((RectTransform)face.transform);
            P.Dimming(button, face);

            float iconSize = Mathf.Min(size.x, size.y) * 0.62f;
            Vector2 iconAt = named ? new Vector2(0f, size.y * 0.12f) : Vector2.zero;
            Image icon = Paint(Center("Icon", rt, iconAt, new Vector2(iconSize, iconSize)), HudSprites.IconCrate, Color.white);
            icon.preserveAspect = true;
            TextMeshProUGUI initial = Label(Center("Initial", rt, iconAt, new Vector2(iconSize, iconSize)), "?", Face.Display, iconSize * 0.6f, HudPalette.Ink);

            TextMeshProUGUI count = Chip(rt, "Count", "x2", new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(64f, 36f), HudPalette.TealDark, 22f);
            GameObject chip = count.transform.parent != rt ? count.transform.parent.gameObject : count.gameObject;

            TextMeshProUGUI label = null;
            if (named)
                label = Label(Center("Name", rt, new Vector2(0f, -size.y * 0.36f), new Vector2(size.x - 10f, 30f)), "", Face.Body, 18f, HudPalette.Ink).Fit(12f);

            RectTransform tick = At("Tick", rt, new Vector2(0f, 1f), new Vector2(6f, -6f), new Vector2(44f, 44f));
            Paint(tick, HudSprites.Disc, HudPalette.Mint);
            Paint(Fill("Glyph", tick, 8f, 8f, 8f, 8f), HudSprites.GlyphCheck, Color.white);

            var view = rt.gameObject.AddComponent<DishTileView>();
            Set(view, "button", button);
            Set(view, "icon", icon);
            Set(view, "initial", initial);
            Set(view, "count", count);
            Set(view, "countChip", chip);
            if (label != null) Set(view, "label", label);
            Set(view, "mark", tick.gameObject);
            Set(view, "glow", glowRect.gameObject);
            Set(view, "group", group);
            return view;
        }

        /// <summary>A car's bag: a card with its place and number, a patience bar, three dish slots, what it pays and the hand-over button.</summary>
        static TakeawayBagView Bag(RectTransform parent, int index, Vector2 at)
        {
            var size = new Vector2(420f, 400f);
            RectTransform rt = Center($"Bag_{index}", parent, at, size);

            RectTransform frame = Center("Selected", rt, Vector2.zero, size + new Vector2(16f, 16f));
            Paint(frame, HudSprites.Tile, HudPalette.Gold, size: size + new Vector2(16f, 16f));

            Image card = Tile(Center("Card", rt, Vector2.zero, size), size, raycast: true);
            Button select = MakeButton((RectTransform)card.transform, card);

            TextMeshProUGUI heading = Label(Center("Heading", rt, new Vector2(0f, 160f), new Vector2(380f, 40f)), "AT THE WINDOW  #1", Face.Display, 24f,
                                            HudPalette.Ink).Fit(16f);
            Image patience = P.FilledBar(rt, "Patience", new Vector2(0f, 118f), new Vector2(360f, 22f), out _);

            var slots = new DishTileView[3];
            for (int s = 0; s < slots.Length; s++) slots[s] = DishTile(rt, $"Slot_{s}", new Vector2(-125f + s * 125f, 22f), new Vector2(112f, 124f), true);

            TextMeshProUGUI bill = Chip(rt, "Bill", "24 coins", new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(230f, 44f), HudPalette.GoldDark, 24f);

            (Button hand, Image handFace, TextMeshProUGUI handLabel) = P.TintButton(rt, "HandOver", new Vector2(0f, -148f), new Vector2(330f, 84f), "HAND OVER");
            P.Dimming(hand, handFace);

            var view = rt.gameObject.AddComponent<TakeawayBagView>();
            Set(view, "select", select);
            Set(view, "card", card);
            Set(view, "selectedFrame", frame.gameObject);
            Set(view, "heading", heading);
            Set(view, "bill", bill);
            Set(view, "patience", patience);
            SetArray(view, "slots", slots);
            Set(view, "handOver", hand);
            Set(view, "handOverFace", handFace);
            Set(view, "handOverLabel", handLabel);
            return view;
        }

        // --- The food truck ----------------------------------------------------------------------------

        static GameObject BuildFoodTruck()
        {
            P.Frame f = P.Popup("UI_FoodTruckPopup", new Vector2(1500f, 920f), "Food truck", HudSprites.IconCrate);
            RectTransform p = f.panel;

            // The loading list, on the left.
            Caption(p, "LoadCaption", new Vector2(-470f, 334f), 480f, "LOAD FROM THE PASS");
            TextMeshProUGUI aboard = Chip(p, "Aboard", "0 / 6 ABOARD", new Vector2(0.5f, 0.5f), new Vector2(-110f, 334f), new Vector2(250f, 44f),
                                          HudPalette.TealDark, 22f);
            var rows = new TruckLoadRowView[6];
            for (int i = 0; i < rows.Length; i++) rows[i] = LoadRow(p, i, new Vector2(-370f, 258f - i * 96f));
            TextMeshProUGUI listHint = P.Para(p, "ListHint", new Vector2(-370f, -322f), new Vector2(680f, 40f), Face.Body, 21f, HudPalette.InkSoft);

            // The map, on the right.
            var mapSize = new Vector2(640f, 380f);
            RectTransform mapBack = Center("MapBack", p, new Vector2(380f, 152f), mapSize + new Vector2(20f, 20f));
            Well(mapBack, mapSize + new Vector2(20f, 20f));
            RectTransform map = Center("Map", p, new Vector2(380f, 152f), mapSize);
            Paint(map, HudSprites.Tile, Pale, size: mapSize);
            MapDressing(map, mapSize);

            RectTransform route = Center("Route", map, Vector2.zero, new Vector2(100f, 8f));
            Paint(route, HudSprites.White, HudPalette.Gold);

            RectTransform home = At("Home", map, new Vector2(0.5f, 0.3f), Vector2.zero, new Vector2(78f, 78f));
            home.pivot = new Vector2(0.5f, 0.5f);
            Paint(home, HudSprites.RoundCream, Color.white);
            Paint(Center("Icon", home, new Vector2(0f, 3f), new Vector2(50f, 50f)), HudSprites.IconDiner, Color.white);

            var pins = new MapPinView[3];
            for (int i = 0; i < pins.Length; i++) pins[i] = Pin(map, i);

            TextMeshProUGUI placeName = P.Para(p, "PlaceName", new Vector2(380f, -66f), new Vector2(620f, 42f), Face.Display, 30f, HudPalette.Ink, wrap: false);
            TextMeshProUGUI placeBlurb = P.Para(p, "PlaceBlurb", new Vector2(380f, -108f), new Vector2(620f, 40f), Face.Body, 22f, HudPalette.InkSoft);
            placeBlurb.fontStyle = FontStyles.Italic;

            Caption(p, "FavouritesCaption", new Vector2(250f, -160f), 360f, "PAYS EXTRA FOR");
            var favourites = new Image[4];
            for (int i = 0; i < favourites.Length; i++)
            {
                RectTransform tile = Center($"Favourite_{i}", p, new Vector2(420f + i * 74f, -160f), new Vector2(64f, 64f));
                Tile(tile, new Vector2(64f, 64f));
                favourites[i] = Paint(Center("Icon", tile, Vector2.zero, new Vector2(52f, 52f)), HudSprites.IconCrate, Color.white);
                favourites[i].preserveAspect = true;
            }

            TextMeshProUGUI estimate = P.Para(p, "Estimate", new Vector2(380f, -214f), new Vector2(620f, 40f), Face.Body, 23f, HudPalette.Ink);
            TextMeshProUGUI reason = P.Para(p, "Reason", new Vector2(380f, -268f), new Vector2(620f, 36f), Face.Body, 22f, P.Short);
            (Button send, Image sendFace, TextMeshProUGUI sendLabel) = P.TintButton(p, "Send", new Vector2(380f, -340f), new Vector2(480f, 96f), "SEND");
            P.Dimming(send, sendFace);

            // While the truck is out: a panel over everything saying where, with the trip's bar.
            RectTransform outPanel = Center("OutOnTheRoad", p, new Vector2(0f, -30f), new Vector2(1420f, 760f));
            Image outBack = Paint(outPanel, HudSprites.Tile, new Color(1f, 0.98f, 0.94f, 0.97f), raycast: true, size: new Vector2(1420f, 760f));
            outBack.raycastTarget = true;
            Paint(Center("Truck", outPanel, new Vector2(0f, 150f), new Vector2(150f, 150f)), HudSprites.IconCrate, Color.white);
            TextMeshProUGUI outText = Label(Center("Text", outPanel, new Vector2(0f, -10f), new Vector2(1200f, 130f)), "", Face.Display, 40f, HudPalette.Ink)
                .Fit(22f, wrap: true);
            Image outBar = P.FilledBar(outPanel, "Trip", new Vector2(0f, -130f), new Vector2(800f, 40f), out _);
            outPanel.gameObject.SetActive(false);

            var view = f.root.AddComponent<FoodTruckPopupView>();
            P.WireFrame(view, f);
            SetArray(view, "rows", rows);
            Set(view, "aboard", aboard);
            Set(view, "listHint", listHint);
            Set(view, "map", map);
            Set(view, "home", home);
            SetArray(view, "pins", pins);
            Set(view, "route", route);
            Set(view, "placeName", placeName);
            Set(view, "placeBlurb", placeBlurb);
            SetArray(view, "favourites", favourites);
            Set(view, "estimate", estimate);
            Set(view, "reason", reason);
            Set(view, "sendButton", send);
            Set(view, "sendButtonImage", sendFace);
            Set(view, "sendButtonLabel", sendLabel);
            Set(view, "outPanel", outPanel.gameObject);
            Set(view, "outText", outText);
            Set(view, "outBar", outBar);
            P.Colours(view, P.Ready, P.Blocked);
            Set(view, "pinColour", HudPalette.Red);
            Set(view, "pickedColour", HudPalette.Gold);
            return f.root;
        }

        /// <summary>One line of the loading list: picture, name, what is on the pass and its price there, minus, count, plus.</summary>
        static TruckLoadRowView LoadRow(RectTransform parent, int index, Vector2 at)
        {
            var size = new Vector2(680f, 88f);
            RectTransform rt = Center($"Load_{index}", parent, at, size);
            Well(rt, size);

            RectTransform iconRect = At("Icon", rt, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(70f, 70f));
            Image icon = Paint(iconRect, HudSprites.IconCrate, Color.white);
            icon.preserveAspect = true;
            TextMeshProUGUI initial = Label(At("Initial", rt, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(70f, 70f)), "?", Face.Display, 40f,
                                            HudPalette.Ink);

            RectTransform star = At("Favourite", rt, new Vector2(0f, 0.5f), new Vector2(62f, 26f), new Vector2(34f, 34f));
            Paint(star, HudSprites.IconStar, Color.white);

            TextMeshProUGUI label = Label(At("Name", rt, new Vector2(0f, 0.5f), new Vector2(100f, 14f), new Vector2(300f, 36f)), "Tomato soup", Face.Display,
                                          26f, HudPalette.Ink, TextAlignmentOptions.Left).Fit(16f);
            TextMeshProUGUI onPass = Label(At("OnPass", rt, new Vector2(0f, 0.5f), new Vector2(100f, -18f), new Vector2(220f, 28f)), "3 on the pass", Face.Body,
                                           20f, HudPalette.InkSoft, TextAlignmentOptions.Left).Fit(14f);
            TextMeshProUGUI price = Label(At("Price", rt, new Vector2(0f, 0.5f), new Vector2(320f, -18f), new Vector2(110f, 28f)), "22c", Face.Body, 20f,
                                          HudPalette.GoldDark, TextAlignmentOptions.Left);

            Button less = P.Stepper(rt, "Less", new Vector2(150f, 0f), HudSprites.GlyphMinus);
            ((RectTransform)less.transform).localScale = Vector3.one * 0.72f;
            TextMeshProUGUI aboard = Label(Center("Aboard", rt, new Vector2(218f, 0f), new Vector2(60f, 50f)), "0", Face.Display, 34f, HudPalette.Ink);
            Button more = P.Stepper(rt, "More", new Vector2(286f, 0f), HudSprites.GlyphPlus);
            ((RectTransform)more.transform).localScale = Vector3.one * 0.72f;

            var view = rt.gameObject.AddComponent<TruckLoadRowView>();
            Set(view, "icon", icon);
            Set(view, "initial", initial);
            Set(view, "label", label);
            Set(view, "onPass", onPass);
            Set(view, "aboard", aboard);
            Set(view, "less", less);
            Set(view, "more", more);
            Set(view, "favourite", star.gameObject);
            Set(view, "price", price);
            return view;
        }

        /// <summary>The map's ground: the road across it with its dashes, and dark green copses scattered about.</summary>
        static void MapDressing(RectTransform map, Vector2 size)
        {
            RectTransform road = At("Road", map, new Vector2(0f, 0.3f), Vector2.zero, new Vector2(size.x, 30f));
            road.anchorMax = new Vector2(1f, 0.3f);
            road.pivot = new Vector2(0.5f, 0.5f);
            road.sizeDelta = new Vector2(0f, 30f);
            Paint(road, HudSprites.White, Road);
            for (int k = 0; k < 9; k++)
                Paint(At($"Dash_{k}", road, new Vector2(0f, 0.5f), new Vector2(20f + k * 70f, 0f), new Vector2(34f, 5f)), HudSprites.White,
                      new Color(0.9f, 0.88f, 0.8f));

            var copses = new[] { new Vector2(0.1f, 0.75f), new Vector2(0.45f, 0.86f), new Vector2(0.62f, 0.55f), new Vector2(0.9f, 0.4f), new Vector2(0.15f, 0.12f),
                                 new Vector2(0.7f, 0.12f), new Vector2(0.55f, 0.72f) };
            for (int k = 0; k < copses.Length; k++)
            {
                RectTransform copse = At($"Copse_{k}", map, copses[k], Vector2.zero, new Vector2(46f + k % 3 * 12f, 40f + k % 2 * 10f));
                copse.pivot = new Vector2(0.5f, 0.5f);
                Paint(copse, HudSprites.Disc, new Color(0.36f, 0.5f, 0.28f));
            }
        }

        /// <summary>A pin on the map: a round button with a flag on it, a ring for the one picked, its name and trip time on a chip under it.</summary>
        static MapPinView Pin(RectTransform map, int index)
        {
            RectTransform rt = At($"Pin_{index}", map, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f));
            rt.pivot = new Vector2(0.5f, 0.5f);

            RectTransform ring = Center("Picked", rt, Vector2.zero, new Vector2(108f, 108f));
            Paint(ring, HudSprites.Ring, HudPalette.Gold);

            Image disc = Paint(Center("Disc", rt, Vector2.zero, new Vector2(84f, 84f)), HudSprites.Disc, HudPalette.Red, raycast: true);
            Button button = MakeButton((RectTransform)disc.transform, disc);
            Motion((RectTransform)disc.transform);
            Paint(Center("Flag", rt, new Vector2(2f, 2f), new Vector2(52f, 52f)), HudSprites.IconFlag, Color.white);

            TextMeshProUGUI label = Chip(rt, "Name", "Farm", new Vector2(0.5f, 0f), new Vector2(0f, -30f), new Vector2(230f, 36f), HudPalette.Ink, 20f);
            TextMeshProUGUI trip = Label(Center("Trip", rt, new Vector2(0f, -86f), new Vector2(120f, 28f)), "1:00", Face.Body, 20f, HudPalette.Ink);

            var view = rt.gameObject.AddComponent<MapPinView>();
            Set(view, "button", button);
            Set(view, "disc", disc);
            Set(view, "label", label);
            Set(view, "trip", trip);
            Set(view, "picked", ring.gameObject);
            return view;
        }
    }
}
