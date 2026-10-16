using System.Collections.Generic;
using LastOrders.UI.Hud;
using LastOrders.UI.Kit;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static LastOrders.EditorTools.Screens.UiKit;

namespace LastOrders.EditorTools.Screens
{
    public static partial class DinerScreensBuilder
    {
        /// <summary>Each shop category's tab: an icon from the atlas and a short name that fits the tab.</summary>
        static (string icon, string label) TabFor(string category) => category switch
        {
            "Stoves" => (HudSprites.IconCook, "KITCHEN"),
            "Farming" => (HudSprites.IconSprout, "FARM"),
            "Decoration" => (HudSprites.IconFrame, "DECOR"),
            "Floor" => (HudSprites.IconTiles, "FLOOR"),
            "Props" => (HudSprites.IconBarrel, "PROPS"),
            "Defense" => (HudSprites.IconShield, "DEFENSE"),
            _ => (HudSprites.IconShop, category.ToUpperInvariant()),
        };

        // --- Shop drawer ------------------------------------------------------------------------------

        /// <summary>
        /// The shop as a drawer along the bottom of the screen instead of a column over the left
        /// third of it: the land stays in view to drag things onto, the categories are tabs with
        /// pictures, and the shelf scrolls sideways under a line that says how to build.
        /// </summary>
        static void BuildShop(UIGame ui, Transform legacy, RectTransform canvas, Swap swap)
        {
            UiItemsWindow oldWindow = ui.uiItemsWindow != null ? ui.uiItemsWindow : Old(legacy, "AllObjectsPanel")?.GetComponent<UiItemsWindow>();
            if (oldWindow == null)
            {
                Debug.LogWarning("[Screens] No old shop window found - shop drawer not built.");
                return;
            }

            // The categories, in the old window's order.
            var categories = new List<(int value, string name, bool active)>();
            var oldSo = new SerializedObject(oldWindow);
            SerializedProperty oldContainers = oldSo.FindProperty("itemContainers");
            for (int i = 0; i < oldContainers.arraySize; i++)
            {
                SerializedProperty e = oldContainers.GetArrayElementAtIndex(i);
                SerializedProperty type = e.FindPropertyRelative("type");
                categories.Add((type.intValue, type.enumNames[type.enumValueIndex], e.FindPropertyRelative("isActive").boolValue));
            }

            RectTransform root = ScreenRoot(canvas, "ShopDrawer");
            var window = root.gameObject.AddComponent<UiItemsWindow>();
            RectTransform safe = Fill("Safe", root);
            safe.gameObject.AddComponent<HudSafeArea>();

            const float height = 330f;
            RectTransform drawer = Node("Drawer", safe, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                                        new Vector2(0f, 14f), new Vector2(-48f, height));
            DropShadow(drawer, 24f);

            // Tabs first, so the drawer's top edge runs across the ones that are not open.
            RectTransform tabRow = Node("Tabs", drawer, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                                        new Vector2(26f, -18f), new Vector2(1300f, 92f));
            var tabs = new List<(Button button, RectTransform rect, Image face, Image icon, TMP_Text label)>();
            for (int i = 0; i < categories.Count; i++)
            {
                (string icon, string label) = TabFor(categories[i].name);
                RectTransform tab = Node(label, tabRow, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(i * 206f, 0f),
                                         new Vector2(198f, 92f));
                Image face = Paint(tab, HudSprites.Tab, HudPalette.Teal, raycast: true, size: new Vector2(198f, 92f));
                Button button = MakeButton(tab, face);
                Motion(tab);
                Image iconImage = Paint(At("Icon", tab, new Vector2(0f, 0.5f), new Vector2(12f, 8f), new Vector2(60f, 60f)), icon, Color.white);
                TMP_Text text = Label(At("Label", tab, new Vector2(0f, 0.5f), new Vector2(76f, 8f), new Vector2(112f, 44f)), label,
                                      Face.Display, 26f, Color.white, TextAlignmentOptions.Left).Fit(15f);
                tabs.Add((button, tab, face, iconImage, text));
            }

            Paint(Fill("Face", drawer), HudSprites.Card, Color.white, raycast: true, size: new Vector2(1872f, height));

            ButtonParts done = TextButton(drawer, "Done", HudSprites.ButtonTeal, "DONE", new Vector2(210f, 84f), new Vector2(-129f, 30f),
                                          new Vector2(1f, 1f), HudSprites.GlyphCheck, 32f);
            OnClick(done.button, ui.ExitEditMode);

            // How to build, and the answer when a card is tapped.
            RectTransform hint = Node("Hint", drawer, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                                      new Vector2(30f, -16f), new Vector2(-300f, 44f));
            Paint(At("Hand", hint, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(44f, 44f)), HudSprites.IconHand, Color.white);
            TMP_Text hintText = Label(At("Text", hint, new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(1100f, 40f)),
                                      "Drag an item up onto your land to build it", Face.Body, 24f, HudPalette.InkSoft,
                                      TextAlignmentOptions.Left);
            var hintView = hint.gameObject.AddComponent<ShopDrawerHint>();
            Set(hintView, "label", hintText);
            Set(hintView, "motion", Motion(hint, pressable: false));

            // The shelf: one content per category, all in one sideways scroll view.
            RectTransform shelf = Fill("Shelf", drawer, 20f, 10f, 20f, 58f);
            ScrollRect scroll = Scroll(shelf, horizontal: true, out RectTransform first);
            var contents = new List<RectTransform>();
            for (int i = 0; i < categories.Count; i++)
            {
                RectTransform content = i == 0
                    ? first
                    : Node("Content", scroll.viewport, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
                content.name = $"{categories[i].name}Content";
                Layout<HorizontalLayoutGroup>(content, 18f, TextAnchor.MiddleLeft, new RectOffset(10, 10, 6, 6));
                FitContent(content, horizontal: true);
                content.gameObject.AddComponent<UiStaggerIn>();
                content.gameObject.SetActive(i == 0);
                contents.Add(content);
            }

            RectTransform empty = Center("Empty", shelf, Vector2.zero, new Vector2(1000f, 60f));
            Label(empty, "Nothing to build here yet - level up to unlock more", Face.Body, 26f, HudPalette.InkSoft);
            shelf.gameObject.AddComponent<UiEmptyState>().Configure(null, scroll, empty.gameObject);

            // The old window's script, wired to the drawer.
            var so = new SerializedObject(window);
            SerializedProperty containers = so.FindProperty("itemContainers");
            containers.arraySize = categories.Count;
            for (int i = 0; i < categories.Count; i++)
            {
                SerializedProperty e = containers.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("type").intValue = categories[i].value;
                e.FindPropertyRelative("isActive").boolValue = categories[i].active;
                e.FindPropertyRelative("container").objectReferenceValue = contents[i];
            }

            so.FindProperty("itemUiPrefab").objectReferenceValue = BuildShopCard();
            so.FindProperty("closeButton").objectReferenceValue = done.button.gameObject;
            so.FindProperty("scrollRect").objectReferenceValue = scroll;
            so.FindProperty("drawer").objectReferenceValue = drawer;
            so.FindProperty("drawerDrop").floatValue = height + 140f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var group = drawer.gameObject.AddComponent<UiTabGroup>();
            var tabSo = new SerializedObject(group);
            tabSo.FindProperty("window").objectReferenceValue = window;
            tabSo.FindProperty("scroll").objectReferenceValue = scroll;
            SerializedProperty tabArray = tabSo.FindProperty("tabs");
            tabArray.arraySize = tabs.Count;
            for (int i = 0; i < tabs.Count; i++)
            {
                SerializedProperty t = tabArray.GetArrayElementAtIndex(i);
                t.FindPropertyRelative("button").objectReferenceValue = tabs[i].button;
                t.FindPropertyRelative("rect").objectReferenceValue = tabs[i].rect;
                t.FindPropertyRelative("face").objectReferenceValue = tabs[i].face;
                t.FindPropertyRelative("icon").objectReferenceValue = tabs[i].icon;
                t.FindPropertyRelative("label").objectReferenceValue = tabs[i].label;
                t.FindPropertyRelative("content").objectReferenceValue = contents[i].gameObject;
            }

            tabSo.FindProperty("onFace").colorValue = HudPalette.Cream;
            tabSo.FindProperty("offFace").colorValue = new Color(0.30f, 0.66f, 0.62f);
            tabSo.FindProperty("onLabel").colorValue = HudPalette.Ink;
            tabSo.FindProperty("offLabel").colorValue = Color.white;
            tabSo.FindProperty("raise").floatValue = 10f;
            tabSo.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            swap.Panel(oldWindow.gameObject, window);
            swap.Add(oldWindow.closeButton, done.button.gameObject);
        }

        /// <summary>One item on the shelf: its picture, name, how many are built, and a price tag.</summary>
        static ItemUiButton BuildShopCard()
        {
            var size = new Vector2(220f, 236f);
            RectTransform root = TemplateRoot("ShopCard", size);
            Image face = Tile(root, size, raycast: true);
            Button button = MakeButton(root, face);
            HudMotion motion = Motion(root);

            Image picture = Paint(Center("Picture", root, new Vector2(0f, 36f), new Vector2(178f, 126f)), HudSprites.IconCrate, Color.white);

            TextMeshProUGUI count = Chip(root, "Count", "0/2", Vector2.one, new Vector2(-42f, -26f), new Vector2(70f, 34f), null, 20f);
            AutoHideChip(count);

            TextMeshProUGUI title = Label(Node("Name", root, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                                               new Vector2(0f, 72f), new Vector2(-24f, 34f)),
                                          "Bakery", Face.Display, 25f, HudPalette.Ink).Fit(15f);

            // The price tag holds the amount directly: the old script hides the amount's parent for free items.
            RectTransform tag = Node("Price", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Middle, new Vector2(0f, 40f),
                                     new Vector2(150f, 52f));
            Image tagFace = Paint(tag, HudSprites.ButtonGold, Color.white, size: new Vector2(150f, 52f));
            var row = tag.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 6f;
            row.padding = new RectOffset(12, 12, 2, 8);
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            RectTransform coin = Node("Coin", tag, Middle, Middle, Middle, Vector2.zero, new Vector2(38f, 38f));
            Paint(coin, HudSprites.IconCoin, Color.white);
            var coinSize = coin.gameObject.AddComponent<LayoutElement>();
            coinSize.preferredWidth = coinSize.minWidth = 38f;
            coinSize.preferredHeight = coinSize.minHeight = 38f;
            TextMeshProUGUI price = Label(Node("Amount", tag, Middle, Middle, Middle, Vector2.zero, new Vector2(80f, 40f)), "150",
                                          Face.Display, 28f, Color.white, TextAlignmentOptions.Center, Style.Shadow);

            RectTransform max = Center("Max", root, new Vector2(0f, 40f), new Vector2(124f, 50f));
            max.localRotation = Quaternion.Euler(0f, 0f, 9f);
            Paint(max, HudSprites.ButtonRed, Color.white, size: new Vector2(124f, 50f));
            Label(Fill("Label", max, 6f, 6f, 6f, 0f), "MAX", Face.Display, 28f, Color.white, TextAlignmentOptions.Center, Style.Shadow);
            max.gameObject.SetActive(false);

            RectTransform locked = Fill("Locked", root);
            Paint(locked, HudSprites.Tile, new Color(0.22f, 0.16f, 0.12f, 0.74f), raycast: true, size: size);
            Paint(Center("Lock", locked, new Vector2(0f, 34f), new Vector2(96f, 96f)), HudSprites.IconLock, Color.white);
            TextMeshProUGUI level = Label(Center("Level", locked, new Vector2(0f, -44f), new Vector2(190f, 40f)), "Level 3", Face.Display,
                                          30f, Color.white, TextAlignmentOptions.Center, Style.Outline);
            locked.gameObject.SetActive(false);

            var item = root.gameObject.AddComponent<ItemUiButton>();
            Set(item, "image", picture);
            Set(item, "lockedObject", locked.gameObject);
            Set(item, "limitationText", count);
            Set(item, "requiredLevelText", level);
            Set(item, "itemNameText", title);
            Set(item, "priceText", price);
            Set(item, "button", button);

            var view = root.gameObject.AddComponent<ShopCardView>();
            Set(view, "item", item);
            Set(view, "picture", picture);
            Set(view, "title", title);
            Set(view, "priceTag", tagFace);
            Set(view, "price", price);
            Set(view, "maxStamp", max.gameObject);
            Set(view, "motion", motion);
            Set(view, "affordableTag", Skin[HudSprites.ButtonGold]);
            Set(view, "shortTag", Skin[HudSprites.ButtonCream]);
            Set(view, "shortText", HudPalette.Red);
            Set(view, "affordableMaterial", Skin.displayShadow);
            Set(view, "shortMaterial", Skin.display != null ? Skin.display.material : null);

            return SavePrefab<ItemUiButton>(root.gameObject, "ShopCard");
        }

        static void AutoHideChip(TMP_Text label)
        {
            Transform chip = label.transform.parent;
            var hide = chip.gameObject.AddComponent<UiChipAutoHide>();
            Set(hide, "text", label);
            Set(hide, "background", chip.GetComponent<Image>());
        }

        // --- Build / move buttons ---------------------------------------------------------------------

        /// <summary>
        /// The round buttons that float by a building being placed or moved, each with its name
        /// under it: cancel, turn, build - or sell, turn, put it down.
        /// </summary>
        static void BuildPlacement(Transform legacy, RectTransform canvas, Swap swap)
        {
            GameObject oldBuild = Old(legacy, "ConfirmationPanel");
            if (oldBuild != null)
            {
                RectTransform root = PlacementRoot(canvas, "BuildConfirm", oldBuild);
                Button cancel = PlacementAction(root, "Cancel", HudSprites.RoundRed, HudSprites.GlyphCross, false, "CANCEL", HudPalette.RedDark);
                Button rotate = PlacementAction(root, "Rotate", HudSprites.RoundCream, HudSprites.GlyphRotate, false, "TURN", HudPalette.InkSoft);
                Button confirm = PlacementAction(root, "Confirm", HudSprites.RoundTeal, HudSprites.GlyphCheck, false, "BUILD", HudPalette.TealDark);

                var panel = root.gameObject.AddComponent<ShopItemConfirm>();
                panel.ConfirmButton = confirm;
                panel.RotateButton = rotate;
                panel.CancelButton = cancel;
                root.gameObject.SetActive(false);
                swap.Panel(oldBuild, panel);
            }

            GameObject oldMove = Old(legacy, "PlacedObjectConfirm");
            if (oldMove != null)
            {
                RectTransform root = PlacementRoot(canvas, "MoveConfirm", oldMove);
                Button sell = PlacementAction(root, "Sell", HudSprites.RoundGold, HudSprites.IconCoin, true, "SELL", HudPalette.GoldDark);
                Button rotate = PlacementAction(root, "Rotate", HudSprites.RoundCream, HudSprites.GlyphRotate, false, "TURN", HudPalette.InkSoft);
                Button place = PlacementAction(root, "Place", HudSprites.RoundTeal, HudSprites.GlyphCheck, false, "PLACE", HudPalette.TealDark);

                // The old panel's naming: its "confirm" sells, its "cancel" puts the building down.
                var panel = root.gameObject.AddComponent<PlacedObjectConfirm>();
                panel.ConfirmButton = sell;
                panel.RotateButton = rotate;
                panel.CancelButton = place;
                root.gameObject.SetActive(false);
                swap.Panel(oldMove, panel);
            }
        }

        static RectTransform PlacementRoot(RectTransform canvas, string name, GameObject old)
        {
            var oldRect = (RectTransform)old.transform;
            RectTransform root = Node(name, canvas, oldRect.anchorMin, oldRect.anchorMax, oldRect.pivot, oldRect.anchoredPosition,
                                      new Vector2(400f, 160f));
            var row = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 14f;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            // The buttons pop in one after another each time they appear by a building.
            root.gameObject.AddComponent<UiStaggerIn>();
            return root;
        }

        static Button PlacementAction(RectTransform row, string name, string round, string glyph, bool icon, string label, Color tag)
        {
            RectTransform action = Node(name, row, Middle, Middle, Middle, Vector2.zero, new Vector2(116f, 150f));
            Image face = Paint(At("Round", action, new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(106f, 106f)), round,
                               Color.white, raycast: true);
            Paint(Center("Glyph", face.rectTransform, new Vector2(0f, 6f), icon ? new Vector2(74f, 74f) : new Vector2(54f, 54f)), glyph,
                  round == HudSprites.RoundCream ? HudPalette.Ink : Color.white);
            Chip(action, "Tag", label, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(108f, 34f), tag, 19f);

            Button button = MakeButton(action, face);
            Motion(action);
            return button;
        }
    }
}
