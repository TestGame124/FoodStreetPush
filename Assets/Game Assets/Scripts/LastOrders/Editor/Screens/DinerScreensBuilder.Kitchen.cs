using LastOrders.UI.Hud;
using LastOrders.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static LastOrders.EditorTools.Screens.UiKit;

namespace LastOrders.EditorTools.Screens
{
    public static partial class DinerScreensBuilder
    {
        // --- Kitchen ----------------------------------------------------------------------------------

        /// <summary>
        /// Cooking at a station, out in the world: the camera frames the station in the middle of the
        /// screen, its cooking slots hang just above it, and the station's recipes sit in a tray along
        /// the bottom - a dish is cooked by dragging it up onto the free slot. Nothing dims the
        /// station; a tap anywhere off the tray and the slots puts it all away.
        /// </summary>
        static GameObject BuildKitchen(Transform legacy, RectTransform canvas, Swap swap)
        {
            GameObject old = Old(legacy, "CookingPanel");
            if (old == null) return null;

            const int slotCount = 2;

            RectTransform root = ScreenRoot(canvas, "KitchenStation");
            root.gameObject.AddComponent<CanvasGroup>();

            // A tap off the tray and the slots closes the kitchen, as the seed tray does.
            RectTransform away = Fill("TapAway", root);
            Image catcher = Paint(away, HudSprites.White, new Color(1f, 1f, 1f, 0f), raycast: true);
            Button tapAway = MakeButton(away, catcher);

            RectTransform safe = Fill("Safe", root);
            safe.gameObject.AddComponent<HudSafeArea>();

            // The slots: a row that the kitchen keeps hanging over the station as the camera moves.
            const float slotWidth = 196f, slotHeight = 236f, gap = 30f;
            RectTransform rack = Node("Rack", safe, Middle, Middle, new Vector2(0.5f, 0f), new Vector2(0f, 120f),
                                      new Vector2(slotCount * slotWidth + (slotCount - 1) * gap, slotHeight));
            Layout<HorizontalLayoutGroup>(rack, gap, TextAnchor.LowerCenter);

            var views = new CookSlotView[slotCount];
            for (int i = 0; i < slotCount; i++) views[i] = CookSlot(rack, i, new Vector2(slotWidth, slotHeight));

            // The tray of recipes along the bottom.
            const float trayHeight = 330f;
            RectTransform tray = Node("Tray", safe, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f),
                                      new Vector2(-48f, trayHeight));
            DropShadow(tray, 24f);
            Paint(Fill("Face", tray), HudSprites.Card, Color.white, raycast: true, size: new Vector2(1872f, trayHeight));

            RectTransform header = Row("Header", tray, 14f, 50f, 28f, 120f);
            Paint(At("Icon", header, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(56f, 56f)), HudSprites.IconCook, Color.white);
            TextMeshProUGUI title = Label(At("Title", header, new Vector2(0f, 0.5f), new Vector2(66f, 0f), new Vector2(380f, 46f)),
                                          "Kitchen", Face.Display, 34f, HudPalette.Ink, TextAlignmentOptions.Left).Fit(22f);
            Text legacyTitle = HiddenText(header, "LegacyTitle", title, "{0}");
            Paint(At("Hand", header, new Vector2(0f, 0.5f), new Vector2(460f, 0f), new Vector2(42f, 42f)), HudSprites.IconHand, Color.white);
            RectTransform hintRect = At("Hint", header, new Vector2(0f, 0.5f), new Vector2(510f, 0f), new Vector2(1100f, 40f));
            TextMeshProUGUI hint = Label(hintRect, "Drag a dish up onto the free slot to cook it", Face.Body, 25f, HudPalette.InkSoft,
                                         TextAlignmentOptions.Left);
            HudMotion hintMotion = Motion(hintRect, pressable: false);

            ButtonParts close = RoundButton(tray, "Close", HudSprites.RoundRed, HudSprites.GlyphCross, 84f, new Vector2(-30f, -22f),
                                            Vector2.one);

            RectTransform shelf = Fill("Shelf", tray, 20f, 10f, 20f, 70f);
            ScrollRect scroll = Scroll(shelf, horizontal: true, out RectTransform content);
            Layout<HorizontalLayoutGroup>(content, 18f, TextAnchor.MiddleLeft, new RectOffset(10, 10, 4, 4));
            FitContent(content, horizontal: true);

            // Over everything: where a dragged dish is drawn.
            RectTransform dragLayer = Fill("DragLayer", root);

            // The old panel script fades and scales slots of its own as it cooks; the slots above
            // read the station's queue directly, so its slots are kept out of sight.
            RectTransform legacySlots = Fill("LegacySlots", root);
            var dummies = new ItemSlotUI[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                RectTransform d = Center($"Slot{i + 1}", legacySlots, Vector2.zero, new Vector2(10f, 10f));
                var slot = d.gameObject.AddComponent<ItemSlotUI>();
                slot.previewImage = Center("Picture", d, Vector2.zero, new Vector2(10f, 10f)).gameObject.AddComponent<Image>();
                slot.amountText = Label(Center("Time", d, Vector2.zero, new Vector2(10f, 10f)), "", Face.Display, 10f, Color.white);
                dummies[i] = slot;
            }

            legacySlots.gameObject.SetActive(false);

            RectTransform legacyClose = At("LegacyClose", root, Middle, Vector2.zero, new Vector2(10f, 10f));
            var unused = legacyClose.gameObject.AddComponent<Button>();
            legacyClose.gameObject.SetActive(false);

            var panel = root.gameObject.AddComponent<CookingUIPanel>();
            SetArray(panel, "itemSlots", dummies);
            panel.buildingNameText = legacyTitle;
            panel.recipiesBoxContainer = content;
            panel.recipeBoxPrefab = BuildRecipeCard();
            panel.closeButton = unused;

            var view = root.gameObject.AddComponent<KitchenStationView>();
            Set(view, "panel", panel);
            Set(view, "rack", rack);
            Set(view, "tray", tray);
            SetArray(view, "slots", views);
            Set(view, "dragLayer", dragLayer);
            Set(view, "hint", hint);
            Set(view, "hintMotion", hintMotion);
            foreach (CookSlotView slot in views) Set(slot, "kitchen", view);

            UiWindow window = WindowBehaviour(root.gameObject, tray, null, close.button);
            var so = new UnityEditor.SerializedObject(window);
            so.FindProperty("popFrom").floatValue = 0.97f;
            so.ApplyModifiedPropertiesWithoutUndo();
            OnClick(tapAway, window.Close);

            root.gameObject.SetActive(false);
            swap.Panel(old, panel);
            return root.gameObject;
        }

        /// <summary>One cooking slot: a plate with a ring that fills as the dish cooks, and its time under it.</summary>
        static CookSlotView CookSlot(RectTransform rack, int index, Vector2 size)
        {
            RectTransform root = Node($"Slot{index + 1}", rack, Middle, Middle, Middle, Vector2.zero, size);
            RectTransform plate = Center("Plate", root, new Vector2(0f, 28f), new Vector2(176f, 176f));

            RectTransform glowRect = Center("Glow", plate, Vector2.zero, new Vector2(250f, 250f));
            Image glow = Paint(glowRect, HudSprites.Glow, new Color(1f, 0.86f, 0.40f, 0.9f));
            glowRect.gameObject.SetActive(false);

            // Catches taps, so a tap on a slot does not count as a tap away from the kitchen.
            Paint(Fill("Base", plate), HudSprites.RoundCream, Color.white, raycast: true);
            Paint(Center("Well", plate, new Vector2(0f, 7f), new Vector2(134f, 134f)), HudSprites.Disc, new Color(0.88f, 0.83f, 0.74f));

            RectTransform ringRect = Center("Progress", plate, new Vector2(0f, 7f), new Vector2(156f, 156f));
            Image ring = Paint(ringRect, HudSprites.Ring, HudPalette.Teal);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 0.3f;
            ringRect.gameObject.SetActive(false);

            Image dish = Paint(Center("Dish", plate, new Vector2(0f, 9f), new Vector2(112f, 112f)), HudSprites.IconCook, Color.white);
            dish.gameObject.SetActive(false);

            RectTransform freeRect = Center("Free", plate, new Vector2(0f, 7f), new Vector2(130f, 130f));
            var free = freeRect.gameObject.AddComponent<CanvasGroup>();
            free.blocksRaycasts = false;
            Paint(Center("Plus", freeRect, new Vector2(0f, 18f), new Vector2(54f, 54f)), HudSprites.GlyphPlus, HudPalette.Teal);
            TextMeshProUGUI freeLabel = Label(Center("Label", freeRect, new Vector2(0f, -32f), new Vector2(130f, 32f)), "Drop here",
                                              Face.Display, 21f, HudPalette.TealDark).Fit(14f);

            RectTransform chip = Node("Timer", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Middle, new Vector2(0f, 22f),
                                      new Vector2(124f, 44f));
            Paint(chip, HudSprites.Chip, HudPalette.Ink.WithAlpha(0.92f), size: new Vector2(124f, 44f));
            TextMeshProUGUI time = Label(Fill("Text", chip, 6f, 2f, 6f, 0f), "0:30", Face.Display, 26f, Color.white);
            chip.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<CookSlotView>();
            var so = new UnityEditor.SerializedObject(view);
            so.FindProperty("index").intValue = index;
            so.ApplyModifiedPropertiesWithoutUndo();
            Set(view, "plate", plate);
            Set(view, "dish", dish);
            Set(view, "progress", ring);
            Set(view, "timerChip", chip.gameObject);
            Set(view, "timer", time);
            Set(view, "freeMark", free);
            Set(view, "freeLabel", freeLabel);
            Set(view, "glow", glow);
            Set(view, "sparkle", Skin[HudSprites.IconStar]);
            Set(view, "ring", Skin[HudSprites.Ring]);
            return view;
        }

        /// <summary>
        /// A recipe in the tray: the dish to drag, its name and cooking time, what it needs (green when
        /// there is enough, red when not) and a tick when it can be cooked now. The card's old Cook
        /// button is kept, out of sight, for the drag to press.
        /// </summary>
        static RecipeUiBox BuildRecipeCard()
        {
            var size = new Vector2(244f, 236f);
            RectTransform root = TemplateRoot("RecipeTile", size);
            RectTransform body = Fill("Body", root);
            Tile(body, size, raycast: true);

            Image dish = Paint(Center("Dish", body, new Vector2(0f, 52f), new Vector2(132f, 104f)), HudSprites.IconCook, Color.white);
            TextMeshProUGUI title = Label(Center("Name", body, new Vector2(0f, -14f), new Vector2(222f, 32f)), "Bread", Face.Display, 24f,
                                          HudPalette.Ink).Fit(15f);

            RectTransform time = At("Time", body, new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(96f, 36f));
            Paint(time, HudSprites.Chip, HudPalette.Ink.WithAlpha(0.88f), size: new Vector2(96f, 36f));
            Paint(At("Clock", time, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(38f, 38f)), HudSprites.IconClock, Color.white);
            TextMeshProUGUI timeLabel = Label(At("Text", time, new Vector2(0f, 0.5f), new Vector2(38f, 1f), new Vector2(54f, 30f)), "30s",
                                              Face.Display, 20f, Color.white, TextAlignmentOptions.Center).Fit(13f);
            Text timer = HiddenText(time, "LegacyTime", timeLabel);

            RectTransform ready = Node("Ready", body, Vector2.one, Vector2.one, Middle, new Vector2(-28f, -28f), new Vector2(44f, 44f));
            Paint(ready, HudSprites.RoundTeal, Color.white);
            Paint(Center("Glyph", ready, new Vector2(0f, 2f), new Vector2(24f, 24f)), HudSprites.GlyphCheck, Color.white);
            ready.gameObject.SetActive(false);

            RectTransform needs = Center("Needs", body, new Vector2(0f, -70f), new Vector2(236f, 72f));
            Layout<HorizontalLayoutGroup>(needs, 4f, TextAnchor.MiddleCenter);

            // Pressed by the drag when a dish is dropped on a free slot.
            RectTransform cookRect = Center("Cook", root, Vector2.zero, new Vector2(10f, 10f));
            var cook = cookRect.gameObject.AddComponent<Button>();
            cookRect.gameObject.SetActive(false);

            var box = root.gameObject.AddComponent<RecipeUiBox>();
            box.CookBtn = cook;
            box.CookingItemImage = dish;
            box.TimerText = timer;
            box.ingredientsArea = needs;
            Set(box, "itemSlotPrefab", BuildIngredientSlot());
            Set(box, "slotSize", 1f);

            var view = root.gameObject.AddComponent<RecipeCardView>();
            Set(view, "box", box);
            Set(view, "title", title);
            Set(view, "picture", dish);
            Set(view, "readyMark", ready.gameObject);

            var drag = root.gameObject.AddComponent<RecipeDragSource>();
            Set(drag, "box", box);
            Set(drag, "body", body);
            Set(drag, "picture", dish);

            return SavePrefab<RecipeUiBox>(root.gameObject, "RecipeTile");
        }

        /// <summary>One ingredient a recipe needs: its picture in a well, and how many there are of how many.</summary>
        static ItemSlotUI BuildIngredientSlot()
        {
            RectTransform root = TemplateRoot("IngredientSlot", new Vector2(56f, 70f));
            RectTransform well = At("Well", root, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(54f, 48f));
            Well(well, new Vector2(54f, 48f));
            Image icon = Paint(Center("Icon", well, new Vector2(0f, 1f), new Vector2(40f, 40f)), HudSprites.IconCrate, Color.white);
            TextMeshProUGUI amount = Label(Node("Amount", root, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero,
                                                new Vector2(8f, 22f)), "0/1", Face.Display, 18f, HudPalette.Ink).Fit(11f);

            var slot = root.gameObject.AddComponent<ItemSlotUI>();
            slot.previewImage = icon;
            slot.amountText = amount;
            return SavePrefab<ItemSlotUI>(root.gameObject, "IngredientSlot");
        }

        // --- Seed tray --------------------------------------------------------------------------------

        /// <summary>
        /// The seeds a tapped pot can take, in a tray along the top of the screen with the one thing
        /// to know written on it: drag a seed across the empty pots.
        /// </summary>
        static void BuildSeedTray(Transform legacy, RectTransform canvas, Swap swap)
        {
            GameObject old = Old(legacy, "SeedsInventory");
            if (old == null) return;

            RectTransform root = ScreenRoot(canvas, "SeedTray");

            // A tap anywhere off the tray puts it away, as before.
            Image catcher = Paint(root, HudSprites.White, new Color(1f, 1f, 1f, 0f), raycast: true);
            Button away = MakeButton(root, catcher);
            var panel = root.gameObject.AddComponent<SeedsInventoryUI>();
            OnClick(away, panel.DeActivatePanel);

            RectTransform safe = Fill("Safe", root);
            safe.gameObject.AddComponent<HudSafeArea>();

            var size = new Vector2(1180f, 250f);
            RectTransform tray = At("Tray", safe, new Vector2(0.5f, 1f), new Vector2(0f, -22f), size);
            DropShadow(tray, 24f);
            Paint(Fill("Face", tray), HudSprites.Card, Color.white, raycast: true, size: size);

            RectTransform header = Row("Header", tray, 14f, 48f, 26f, 90f);
            Paint(At("Icon", header, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(54f, 54f)), HudSprites.IconSeeds, Color.white);
            Label(At("Title", header, new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(160f, 44f)), "SEEDS", Face.Display, 32f,
                  HudPalette.Ink, TextAlignmentOptions.Left);
            Paint(At("Hand", header, new Vector2(0f, 0.5f), new Vector2(222f, 0f), new Vector2(40f, 40f)), HudSprites.IconHand, Color.white);
            Label(At("Hint", header, new Vector2(0f, 0.5f), new Vector2(270f, 0f), new Vector2(760f, 40f)),
                  "Drag a seed across the empty pots to plant them", Face.Body, 23f, HudPalette.InkSoft, TextAlignmentOptions.Left);

            RectTransform seeds = Fill("Seeds", tray, 24f, 16f, 24f, 70f);
            Layout<HorizontalLayoutGroup>(seeds, 16f, TextAnchor.MiddleCenter);
            seeds.gameObject.AddComponent<UiStaggerIn>();

            ButtonParts close = RoundButton(tray, "Close", HudSprites.RoundRed, HudSprites.GlyphCross, 70f, new Vector2(-14f, -14f),
                                            Vector2.one);
            OnClick(close.button, panel.DeActivatePanel);

            Set(panel, "seedsContainer", seeds);
            Set(panel, "seedTray", tray);
            Set(panel, "itemSlotTemplate", BuildSeedSlot());

            root.gameObject.SetActive(false);
            swap.Panel(old, panel);
        }

        static ItemSlotUI BuildSeedSlot()
        {
            var size = new Vector2(140f, 152f);
            RectTransform root = TemplateRoot("SeedSlot", size);
            Tile(root, size, raycast: true);
            Image icon = Paint(Center("Icon", root, new Vector2(0f, 16f), new Vector2(92f, 92f)), HudSprites.IconSeeds, Color.white);
            TextMeshProUGUI count = Chip(root, "Count", "3", Vector2.one, new Vector2(-30f, -22f), new Vector2(54f, 34f), null, 21f);
            AutoHideChip(count);
            ItemName(root, icon, 19f);

            var slot = root.gameObject.AddComponent<ItemSlotUI>();
            slot.previewImage = icon;
            slot.amountText = count;
            return SavePrefab<ItemSlotUI>(root.gameObject, "SeedSlot");
        }
    }
}
