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
        // --- Storehouse -------------------------------------------------------------------------------

        /// <summary>
        /// The storehouse: everything held, as a grid of tiles with counts, under a bar saying how
        /// many of its slots are taken - and a word of explanation when there is nothing in it yet.
        /// </summary>
        static GameObject BuildStorehouse(Transform legacy, RectTransform canvas, Swap swap)
        {
            GameObject old = Old(legacy, "Inventory");
            if (old == null) return null;

            RectTransform root = ScreenRoot(canvas, "StorehouseWindow");
            CanvasGroup scrim = Scrim(root);
            WindowParts w = Window(root, "Window", new Vector2(1320f, 800f), "Storehouse", HudSprites.IconCrate);

            RectTransform top = Row("Usage", w.body, 0f, 48f);
            Label(At("Line", top, new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(700f, 40f)), "Everything you have grown and cooked",
                  Face.Body, 24f, HudPalette.InkSoft, TextAlignmentOptions.Left);
            TextMeshProUGUI count = Label(At("Count", top, new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(190f, 40f)), "0/20 slots",
                                          Face.Display, 26f, HudPalette.Ink, TextAlignmentOptions.Right);
            RectTransform track = At("Bar", top, new Vector2(1f, 0.5f), new Vector2(-206f, 0f), new Vector2(300f, 26f));
            HudBar bar = Bar(track, HudPalette.Mint, 26f);
            var usage = top.gameObject.AddComponent<StorehouseUsage>();
            Set(usage, "bar", bar);
            Set(usage, "label", count);

            RectTransform area = Fill("Items", w.body, 0f, 0f, 0f, 60f);
            Well(area, new Vector2(1252f, 580f));
            ScrollRect scroll = Scroll(area, horizontal: false, out RectTransform content);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(150f, 166f);
            grid.spacing = new Vector2(16f, 18f);
            grid.padding = new RectOffset(20, 20, 20, 20);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 7;
            FitContent(content, horizontal: false);

            GameObject empty = EmptyMessage(area, HudSprites.IconCrate, "Your storehouse is empty",
                                            "Harvest crops and cook dishes to fill it up.");
            area.gameObject.AddComponent<UiEmptyState>().Configure(content, null, empty);

            var panel = root.gameObject.AddComponent<InventoryUIBehaviour>();
            Set(panel, "itemSlotContainer", content);
            Set(panel, "itemSLotTemplate", BuildStoreSlot());

            WindowBehaviour(root.gameObject, w.root, scrim, w.close);

            root.gameObject.SetActive(false);
            swap.Panel(old, panel);
            return root.gameObject;
        }

        static ItemSlotUI BuildStoreSlot()
        {
            var size = new Vector2(150f, 166f);
            RectTransform root = TemplateRoot("StoreSlot", size);
            Tile(root, size);
            Image icon = Paint(Center("Icon", root, new Vector2(0f, 18f), new Vector2(100f, 100f)), HudSprites.IconCrate, Color.white);
            TextMeshProUGUI count = Chip(root, "Count", "12", Vector2.one, new Vector2(-34f, -24f), new Vector2(60f, 34f), null, 22f);
            AutoHideChip(count);
            ItemName(root, icon, 20f);

            var slot = root.gameObject.AddComponent<ItemSlotUI>();
            slot.previewImage = icon;
            slot.amountText = count;
            return SavePrefab<ItemSlotUI>(root.gameObject, "StoreSlot");
        }

        // --- Orders -----------------------------------------------------------------------------------

        /// <summary>
        /// The orders as a row of diner tickets: the dishes each wants with how many are ready, a tick
        /// on every dish that is covered, the reward, and a Deliver button that lights up only when the
        /// whole ticket can go out.
        /// </summary>
        static GameObject BuildOrders(Transform legacy, RectTransform canvas, Swap swap)
        {
            GameObject old = Old(legacy, "OrdersPanel");
            if (old == null) return null;

            RectTransform root = ScreenRoot(canvas, "OrdersWindow");
            root.gameObject.AddComponent<CanvasGroup>();
            CanvasGroup scrim = Scrim(root);
            WindowParts w = Window(root, "Window", new Vector2(1580f, 830f), "Orders", HudSprites.IconOrders);

            Label(Row("Line", w.body, 0f, 40f, 6f, 6f), "Deliver a ticket once the storehouse holds every dish on it",
                  Face.Body, 24f, HudPalette.InkSoft, TextAlignmentOptions.Left).Fit(16f);

            RectTransform area = Fill("Tickets", w.body, 0f, 0f, 0f, 52f);
            Well(area, new Vector2(1512f, 620f));
            ScrollRect scroll = Scroll(area, horizontal: true, out RectTransform content);
            Layout<HorizontalLayoutGroup>(content, 26f, TextAnchor.MiddleLeft, new RectOffset(24, 24, 10, 10));
            FitContent(content, horizontal: true);

            GameObject empty = EmptyMessage(area, HudSprites.IconOrders, "No orders right now", "Open the diner and orders will come in.");
            area.gameObject.AddComponent<UiEmptyState>().Configure(content, null, empty);

            var panel = root.gameObject.AddComponent<OrdersUiBehaviour>();
            Set(panel, "ordersBoardUIPrefab", BuildOrderTicket());
            Set(panel, "boardsContainer", content);

            WindowBehaviour(root.gameObject, w.root, scrim, w.close, panel.OrderCloseButton);

            root.gameObject.SetActive(false);
            swap.Panel(old, panel);
            Transform oldClose = old.transform.Find("CloseBtn");
            if (oldClose != null) swap.Add(oldClose.gameObject, w.close.gameObject);
            return root.gameObject;
        }

        static OrdersBoardUI BuildOrderTicket()
        {
            var size = new Vector2(340f, 576f);
            RectTransform root = TemplateRoot("OrderTicket", size);
            Tile(root, size);

            // A red band across the top, like the diner's rail.
            RectTransform head = Row("Head", root, 10f, 66f, 10f, 10f);
            Paint(head, HudSprites.ButtonRed, Color.white, size: new Vector2(320f, 66f));
            Paint(At("Icon", head, new Vector2(0f, 0.5f), new Vector2(14f, 4f), new Vector2(58f, 58f)), HudSprites.IconServe, Color.white);
            Label(At("Title", head, new Vector2(0f, 0.5f), new Vector2(80f, 4f), new Vector2(220f, 46f)), "ORDER", Face.Display, 32f,
                  Color.white, TextAlignmentOptions.Left, Style.Shadow);

            // Up to six dishes. The order script scales each dish slot up twice, so the slots are
            // drawn at half size; the wells and ticks are drawn at full size.
            RectTransform dishes = Row("Dishes", root, 96f, 236f, 20f, 20f);
            var grid = dishes.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(92f, 104f);
            grid.spacing = new Vector2(12f, 16f);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            var wells = new Image[6];
            var parents = new Transform[6];
            var ticks = new GameObject[6];
            for (int i = 0; i < wells.Length; i++)
            {
                RectTransform well = Node($"Dish{i + 1}", dishes, Middle, Middle, Middle, Vector2.zero, new Vector2(92f, 104f));
                wells[i] = Well(well, new Vector2(92f, 104f));
                parents[i] = well;

                RectTransform tick = Node("Tick", well, Vector2.one, Vector2.one, Middle, new Vector2(-6f, -6f), new Vector2(40f, 40f));
                Paint(tick, HudSprites.RoundTeal, Color.white);
                Paint(Center("Glyph", tick, new Vector2(0f, 2f), new Vector2(22f, 22f)), HudSprites.GlyphCheck, Color.white);
                tick.gameObject.SetActive(false);
                ticks[i] = tick.gameObject;
            }

            Paint(Row("Divider", root, 346f, 4f, 30f, 30f), HudSprites.White, HudPalette.CreamLip);
            Label(Row("Caption", root, 356f, 28f), "REWARD", Face.Display, 22f, HudPalette.InkSoft);

            RectTransform reward = Row("Reward", root, 384f, 56f, 20f, 20f);
            var row = reward.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 8f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            TextMeshProUGUI coins = RewardPart(reward, HudSprites.IconCoin, "Coins", "20");
            RectTransform gap = Node("Gap", reward, Middle, Middle, Middle, Vector2.zero, new Vector2(16f, 10f));
            gap.gameObject.AddComponent<LayoutElement>().preferredWidth = 16f;
            TextMeshProUGUI stars = RewardPart(reward, HudSprites.IconStar, "Stars", "5");

            Text coinsText = HiddenText(root, "LegacyCoins", coins);
            Text starsText = HiddenText(root, "LegacyStars", stars);

            ButtonParts deliver = TextButton(root, "Deliver", HudSprites.ButtonGold, "DELIVER", new Vector2(200f, 82f), new Vector2(122f, 58f),
                                             Vector2.zero, HudSprites.GlyphCheck, 30f);
            ButtonParts trash = RoundButton(root, "Trash", HudSprites.RoundCream, HudSprites.GlyphTrash, 82f, new Vector2(-62f, 60f),
                                            new Vector2(1f, 0f));

            var board = root.gameObject.AddComponent<OrdersBoardUI>();
            Set(board, "CoinsReward", coinsText);
            Set(board, "StarRewards", starsText);
            board.ClaimButton = deliver.button;
            board.TrashButton = trash.button;
            SetArray(board, "ItemSlotsParents", parents);
            Set(board, "itemslotPrefab", BuildOrderDish());

            var view = root.gameObject.AddComponent<OrderCardView>();
            Set(view, "board", board);
            SetArray(view, "wells", wells);
            SetArray(view, "ticks", ticks);
            Set(view, "deliverFace", deliver.face);
            Set(view, "deliverMotion", deliver.motion);

            var fx = deliver.button.gameObject.AddComponent<OrderDeliverFx>();
            Set(fx, "coin", Skin[HudSprites.IconCoin]);
            Set(fx, "star", Skin[HudSprites.IconStar]);
            Set(fx, "sparkle", Skin[HudSprites.IconStar]);
            Set(view, "readySprite", Skin[HudSprites.ButtonGold]);
            Set(view, "blockedSprite", Skin[HudSprites.ButtonGrey]);

            return SavePrefab<OrdersBoardUI>(root.gameObject, "OrderTicket");
        }

        /// <summary>The item's name along the bottom of a tile, looked up from its picture.</summary>
        static void ItemName(RectTransform tile, Image picture, float size)
        {
            TextMeshProUGUI label = Label(Node("Name", tile, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f),
                                               new Vector2(-16f, 28f)), "", Face.Display, size, HudPalette.Ink).Fit(12f);
            var naming = tile.gameObject.AddComponent<UiItemName>();
            Set(naming, "picture", picture);
            Set(naming, "label", label);
        }

        static TextMeshProUGUI RewardPart(RectTransform row, string icon, string name, string sample)
        {
            RectTransform iconRect = Node($"{name}Icon", row, Middle, Middle, Middle, Vector2.zero, new Vector2(48f, 48f));
            Paint(iconRect, icon, Color.white);
            var size = iconRect.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = size.minWidth = 48f;
            size.preferredHeight = size.minHeight = 48f;

            return Label(Node(name, row, Middle, Middle, Middle, Vector2.zero, new Vector2(70f, 44f)), sample, Face.Display, 32f,
                         HudPalette.Ink, TextAlignmentOptions.Left);
        }

        /// <summary>One dish on a ticket, at half the size it shows at: its picture and how many of how many are ready.</summary>
        static ItemSlotUI BuildOrderDish()
        {
            RectTransform root = TemplateRoot("OrderDish", new Vector2(46f, 52f));
            Image icon = Paint(Center("Icon", root, new Vector2(0f, 5f), new Vector2(37f, 37f)), HudSprites.IconServe, Color.white);
            TextMeshProUGUI amount = Label(Node("Amount", root, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                                                new Vector2(0f, 0.5f), new Vector2(0f, 13f)), "0/1", Face.Display, 10.5f, HudPalette.Ink);

            var slot = root.gameObject.AddComponent<ItemSlotUI>();
            slot.previewImage = icon;
            slot.amountText = amount;
            return SavePrefab<ItemSlotUI>(root.gameObject, "OrderDish");
        }
    }
}
