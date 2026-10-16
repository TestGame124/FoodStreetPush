using System.Collections.Generic;
using LastOrders.UI.Hud;
using LastOrders.UI.Kit;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static LastOrders.EditorTools.Screens.UiKit;

namespace LastOrders.EditorTools.Screens
{
    public static partial class DinerScreensBuilder
    {
        // --- Level up ---------------------------------------------------------------------------------

        /// <summary>
        /// A level-up worth stopping for: the new level in a big star over slowly turning sun rays, a
        /// red ribbon, what has just been unlocked laid out on a card, and one gold button to carry on.
        /// </summary>
        static GameObject BuildLevelUp(Transform legacy, RectTransform canvas, Swap swap)
        {
            GameObject old = Old(legacy, "LevelUpPanel");
            if (old == null) return null;

            RectTransform root = ScreenRoot(canvas, "LevelUpWindow");
            root.gameObject.AddComponent<CanvasGroup>();

            // No tap-to-close: the rewards are handed over by the Continue button.
            RectTransform dim = Fill("Scrim", root);
            Paint(dim, HudSprites.White, new Color(0.08f, 0.05f, 0.03f, 0.7f), raycast: true);
            var scrim = dim.gameObject.AddComponent<CanvasGroup>();

            RectTransform panel = Center("Panel", root, new Vector2(0f, 10f), new Vector2(960f, 880f));

            RectTransform rays = Center("Rays", panel, new Vector2(0f, 214f), new Vector2(860f, 860f));
            Paint(rays, HudSprites.Rays, new Color(1f, 0.86f, 0.45f, 0.62f));
            rays.gameObject.AddComponent<UiSpin>();

            RectTransform star = Center("Star", panel, new Vector2(0f, 226f), new Vector2(280f, 280f));
            Paint(star, HudSprites.IconStar, Color.white);
            TextMeshProUGUI level = Label(Center("Level", star, new Vector2(0f, -10f), new Vector2(200f, 140f)), "5", Face.Display, 112f,
                                          Color.white, TextAlignmentOptions.Center, Style.Outline);

            RectTransform ribbon = Center("Ribbon", panel, new Vector2(0f, 52f), new Vector2(620f, 134f));
            Paint(ribbon, HudSprites.Ribbon, HudPalette.Red);
            Label(Center("Text", ribbon, new Vector2(0f, 14f), new Vector2(440f, 72f)), "LEVEL UP!", Face.Display, 58f, Color.white,
                  TextAlignmentOptions.Center, Style.Shadow);

            var cardSize = new Vector2(840f, 300f);
            RectTransform card = Center("Unlocks", panel, new Vector2(0f, -190f), cardSize);
            DropShadow(card, 24f);
            Paint(Fill("Face", card), HudSprites.Card, Color.white, size: cardSize);
            Label(Row("Caption", card, 18f, 38f), "NEW FOR YOUR DINER", Face.Display, 28f, HudPalette.InkSoft);
            // A row that sits centred while it fits and scrolls sideways when a big jump unlocks a lot.
            RectTransform shelf = Fill("Shelf", card, 20f, 18f, 20f, 60f);
            ScrollRect scroll = Scroll(shelf, horizontal: true, out RectTransform rewards);
            rewards.anchorMin = rewards.anchorMax = rewards.pivot = Middle;
            rewards.sizeDelta = new Vector2(0f, 170f);
            rewards.name = "Rewards";
            Layout<HorizontalLayoutGroup>(rewards, 18f, TextAnchor.MiddleCenter, new RectOffset(8, 8, 4, 4));
            FitContent(rewards, horizontal: true);
            rewards.gameObject.AddComponent<UiStaggerIn>();
            scroll.movementType = ScrollRect.MovementType.Clamped;
            GameObject none = Label(Center("None", card, new Vector2(0f, -18f), new Vector2(760f, 50f)),
                                    "Keep serving - more unlocks are on the way", Face.Body, 26f, HudPalette.InkSoft).gameObject;
            card.gameObject.AddComponent<UiEmptyState>().Configure(rewards, null, none);

            ButtonParts carryOn = TextButton(panel, "Continue", HudSprites.ButtonGold, "CONTINUE", new Vector2(390f, 102f),
                                             new Vector2(0f, -402f), null, HudSprites.GlyphPlay, 42f);

            Text legacyTitle = HiddenText(panel, "LegacyTitle", level, "{0}", numberOnly: true);

            // Confetti over everything else in the celebration.
            RectTransform burst = Fill("Confetti", panel);
            var confetti = burst.gameObject.AddComponent<UiConfetti>();
            Set(confetti, "piece", Skin[HudSprites.Segment]);
            var colours = new SerializedObject(confetti);
            SerializedProperty list = colours.FindProperty("colours");
            Color[] palette = { HudPalette.Red, HudPalette.Teal, HudPalette.Gold, HudPalette.Cream, HudPalette.Mint, HudPalette.Rose };
            list.arraySize = palette.Length;
            for (int i = 0; i < palette.Length; i++) list.GetArrayElementAtIndex(i).colorValue = palette[i];
            colours.ApplyModifiedPropertiesWithoutUndo();

            var show = root.gameObject.AddComponent<LevelUpCelebration>();
            Set(show, "star", star);
            Set(show, "ribbon", ribbon);
            Set(show, "card", card);
            Set(show, "cardGroup", card.gameObject.AddComponent<CanvasGroup>());
            Set(show, "carryOn", carryOn.button.transform);
            Set(show, "carryOnGroup", carryOn.button.gameObject.AddComponent<CanvasGroup>());
            Set(show, "carryOnMotion", carryOn.motion);
            Set(show, "confetti", confetti);

            var view = root.gameObject.AddComponent<LevelUpUIPanel>();
            Set(view, "rewardsPanel", rewards);
            Set(view, "rewardItemSlotPrefab", BuildRewardTile());
            Set(view, "confirmButton", carryOn.button);
            Set(view, "levelUpText", legacyTitle);

            var window = root.gameObject.AddComponent<UiWindow>();
            var so = new SerializedObject(window);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("scrim").objectReferenceValue = scrim;
            so.FindProperty("closeOnBack").boolValue = false;
            so.FindProperty("popFrom").floatValue = 0.6f;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            swap.Panel(old, view);
            return root.gameObject;
        }

        static ItemSlotUI BuildRewardTile()
        {
            var size = new Vector2(132f, 156f);
            RectTransform root = TemplateRoot("RewardTile", size);
            Tile(root, size);
            Image icon = Paint(Center("Icon", root, new Vector2(0f, 16f), new Vector2(98f, 98f)), HudSprites.IconStar, Color.white);
            TextMeshProUGUI amount = Label(Node("Amount", root, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f),
                                                new Vector2(-16f, 32f)), "Unlocked", Face.Display, 22f, HudPalette.TealDark).Fit(14f);

            var slot = root.gameObject.AddComponent<ItemSlotUI>();
            slot.previewImage = icon;
            slot.amountText = amount;
            return SavePrefab<ItemSlotUI>(root.gameObject, "RewardTile");
        }

        // --- Guide ------------------------------------------------------------------------------------

        /// <summary>How the diner works, in three steps a player can take in at a glance.</summary>
        static GameObject BuildGuide(Transform legacy, RectTransform canvas, Swap swap)
        {
            GameObject old = Old(legacy, "GuidePanel");
            if (old == null) return null;

            RectTransform root = ScreenRoot(canvas, "GuideWindow");
            CanvasGroup scrim = Scrim(root);
            WindowParts w = Window(root, "Window", new Vector2(1280f, 720f), "How it works", HudSprites.IconDiner);

            RectTransform steps = Row("Steps", w.body, 14f, 380f);
            steps.gameObject.AddComponent<UiStaggerIn>();
            var data = new[]
            {
                (HudSprites.IconSprout, "Grow", "Plant seeds in your pots and harvest the crops"),
                (HudSprites.IconCook, "Cook", "Turn crops into dishes at your kitchen stations"),
                (HudSprites.IconServe, "Serve", "Deliver orders to earn coins and stars"),
            };

            for (int i = 0; i < data.Length; i++)
            {
                (string icon, string title, string line) = data[i];
                var size = new Vector2(340f, 360f);
                RectTransform step = Center($"Step{i + 1}", steps, new Vector2((i - 1) * 410f, 0f), size);
                Tile(step, size);
                Paint(Center("Icon", step, new Vector2(0f, 74f), new Vector2(156f, 156f)), icon, Color.white);
                Label(Center("Title", step, new Vector2(0f, -40f), new Vector2(300f, 54f)), title, Face.Display, 42f, HudPalette.Ink);
                Label(Center("Line", step, new Vector2(0f, -106f), new Vector2(290f, 74f)), line, Face.Body, 22f, HudPalette.InkSoft)
                    .Fit(16f, wrap: true);

                RectTransform number = Node("Number", step, new Vector2(0f, 1f), new Vector2(0f, 1f), Middle, new Vector2(30f, -30f),
                                            new Vector2(70f, 70f));
                Paint(number, HudSprites.RoundTeal, Color.white);
                Label(Center("Digit", number, new Vector2(0f, 3f), new Vector2(60f, 60f)), (i + 1).ToString(), Face.Display, 38f,
                      Color.white, TextAlignmentOptions.Center, Style.Shadow);

                if (i < data.Length - 1)
                {
                    Paint(Center($"Then{i + 1}", steps, new Vector2((i - 0.5f) * 410f, 10f), new Vector2(54f, 54f)), HudSprites.GlyphNext,
                          HudPalette.Teal);
                }
            }

            ButtonParts gotIt = TextButton(w.body, "GotIt", HudSprites.ButtonTeal, "GOT IT", new Vector2(310f, 92f), new Vector2(0f, 52f),
                                           new Vector2(0.5f, 0f), HudSprites.GlyphCheck, 36f);
            UiWindow window = WindowBehaviour(root.gameObject, w.root, scrim, w.close);
            OnClick(gotIt.button, window.Close);

            root.gameObject.SetActive(false);
            swap.Add(old, root.gameObject);
            swap.Add(old.transform, root.transform);
            return root.gameObject;
        }

        // --- Naming the diner -------------------------------------------------------------------------

        static GameObject BuildNaming(Transform legacy, RectTransform canvas, Swap swap)
        {
            GameObject old = Old(legacy, "SignBoardPanel");
            if (old == null) return null;

            RectTransform root = ScreenRoot(canvas, "NameWindow");
            root.gameObject.AddComponent<CanvasGroup>();
            CanvasGroup scrim = Scrim(root);
            WindowParts w = Window(root, "Window", new Vector2(880f, 520f), "Name your diner", HudSprites.IconDiner);

            Label(Row("Line", w.body, 8f, 40f), "It goes up on the sign out front", Face.Body, 26f, HudPalette.InkSoft);

            // The old script pops the field's parent in; the field sits in a holder of its own so the
            // window itself is left alone.
            RectTransform holder = Center("FieldHolder", w.body, new Vector2(0f, 28f), new Vector2(720f, 104f));
            RectTransform field = Fill("Field", holder);
            Image well = Well(field, new Vector2(720f, 104f));
            well.raycastTarget = true;

            RectTransform area = Fill("Text Area", field, 28f, 12f, 28f, 12f);
            area.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI placeholder = Label(Fill("Placeholder", area), "Type a name...", Face.Body, 34f,
                                                HudPalette.InkSoft.WithAlpha(0.7f), TextAlignmentOptions.Left);
            placeholder.fontStyle = FontStyles.Italic;
            TextMeshProUGUI text = Label(Fill("Text", area), "", Face.Display, 42f, HudPalette.Ink, TextAlignmentOptions.Left);

            var input = field.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = well;
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = 22;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.customCaretColor = true;
            input.caretColor = HudPalette.Teal;
            input.caretWidth = 3;
            input.selectionColor = HudPalette.TealLight.WithAlpha(0.45f);
            input.transition = Selectable.Transition.None;

            ButtonParts save = TextButton(w.body, "Save", HudSprites.ButtonTeal, "SAVE", new Vector2(300f, 92f), new Vector2(0f, 52f),
                                          new Vector2(0.5f, 0f), HudSprites.GlyphCheck, 36f);

            var panel = root.gameObject.AddComponent<SignBoardUIPanel>();
            panel.inputField = input;
            OnClick(save.button, panel.Submit);
            WindowBehaviour(root.gameObject, w.root, scrim, w.close);

            root.gameObject.SetActive(false);
            swap.Panel(old, panel);
            return root.gameObject;
        }

        // --- Store ------------------------------------------------------------------------------------

        /// <summary>
        /// The store's offers on cream cards in the diner's colours, keeping the old offers' pictures
        /// and prices. Buying is not built yet, so a price button says so instead of doing nothing.
        /// </summary>
        static GameObject BuildStore(UIGame ui, Transform legacy, RectTransform canvas, Swap swap)
        {
            GameObject old = Old(legacy, "InApp");
            if (old == null) return null;

            RectTransform root = ScreenRoot(canvas, "StoreWindow");
            root.gameObject.AddComponent<CanvasGroup>();
            CanvasGroup scrim = Scrim(root);
            WindowParts w = Window(root, "Window", new Vector2(1420f, 820f), "Store", HudSprites.IconGem);

            RectTransform offers = Fill("Offers", w.body);
            Layout<HorizontalLayoutGroup>(offers, 40f, TextAnchor.MiddleCenter);

            GameObject soon = Notice(canvas, "StoreSoonNotice", HudSprites.RoundTeal, HudSprites.GlyphInfo, "Coming soon",
                                     "Purchases are not open yet - keep an eye on the store");

            Transform oldOffers = old.transform.Find("MidArea");
            var tints = new Dictionary<string, Color>
            {
                { "Coins", HudPalette.Amber },
                { "Gems", HudPalette.Rose },
                { "RemoveAds", HudPalette.Teal },
            };

            if (oldOffers != null)
            {
                foreach (Transform offer in oldOffers)
                {
                    var size = new Vector2(388f, 610f);
                    RectTransform card = Node(offer.name, offers, Middle, Middle, Middle, Vector2.zero, size);
                    Tile(card, size);

                    string banner = TextAt(offer, "banner/Text (Legacy)") ?? offer.name;
                    string price = TextAt(offer, "Button (Legacy)/Text (Legacy)") ?? "Buy";
                    Transform pictureSource = offer.Find("Image");
                    Image oldPicture = pictureSource != null ? pictureSource.GetComponent<Image>() : null;
                    Sprite picture = oldPicture != null ? oldPicture.sprite : null;

                    RectTransform ribbon = Center("Ribbon", card, new Vector2(0f, 276f), new Vector2(410f, 104f));
                    Paint(ribbon, HudSprites.Ribbon, tints.TryGetValue(offer.name, out Color tint) ? tint : HudPalette.Teal);
                    Label(Center("Text", ribbon, new Vector2(0f, 11f), new Vector2(300f, 56f)), banner.ToUpperInvariant(), Face.Display, 36f,
                          Color.white, TextAlignmentOptions.Center, Style.Shadow).Fit(22f);

                    Color backdrop = Color.Lerp(tints.TryGetValue(offer.name, out Color shade) ? shade : HudPalette.Teal, HudPalette.Ink, 0.12f);
                    Paint(Center("Backdrop", card, new Vector2(0f, 40f), new Vector2(300f, 300f)), HudSprites.RoundTint, backdrop);
                    RectTransform pictureRect = Center("Picture", card, new Vector2(0f, 46f), new Vector2(286f, 286f));
                    Image image = pictureRect.gameObject.AddComponent<Image>();
                    image.sprite = picture;
                    image.preserveAspect = true;
                    image.raycastTarget = false;

                    ButtonParts buy = TextButton(card, "Buy", HudSprites.ButtonGold, price, new Vector2(250f, 88f), new Vector2(0f, 74f),
                                                 new Vector2(0.5f, 0f), null, 38f);
                    UnityEventTools.AddBoolPersistentListener(buy.button.onClick, soon.SetActive, true);
                }
            }

            Set(ui, "storeCards", offers);
            WindowBehaviour(root.gameObject, w.root, scrim, w.close);

            root.gameObject.SetActive(false);
            swap.Add(old, root.gameObject);
            swap.Add(old.transform, root.transform);
            return root.gameObject;
        }

        static string TextAt(Transform parent, string path)
        {
            Transform t = parent.Find(path);
            Text text = t != null ? t.GetComponent<Text>() : null;
            return text != null && !string.IsNullOrWhiteSpace(text.text) ? text.text : null;
        }

        // --- Notices ----------------------------------------------------------------------------------

        /// <summary>
        /// "Not enough ingredients" and "order not ready" as notices that rise up, say it, and go -
        /// rather than full-screen panels with an OK button to dismiss.
        /// </summary>
        static void BuildNotices(Transform legacy, RectTransform canvas, Swap swap)
        {
            GameObject oldShort = Old(legacy, "NotEnoughPanel");
            if (oldShort != null)
            {
                GameObject notice = Notice(canvas, "NotEnoughNotice", HudSprites.RoundRed, HudSprites.GlyphAlert, "Not enough ingredients",
                                           "Grow or harvest what this recipe needs first");
                swap.Add(oldShort, notice);
                swap.Add(oldShort.transform, notice.transform);
            }

            GameObject oldOrder = Old(legacy, "Buy Ingredients");
            if (oldOrder != null)
            {
                GameObject notice = Notice(canvas, "OrderNotReadyNotice", HudSprites.RoundGold, HudSprites.GlyphAlert, "Not ready to deliver",
                                           "Cook every dish on the ticket, then deliver it");
                swap.Add(oldOrder, notice);
                swap.Add(oldOrder.transform, notice.transform);
            }
        }

        /// <summary>
        /// A notice card low on the screen. The old code slides each child of the notice up from
        /// below to rest at zero, so the card is its only child and rests at the notice's centre.
        /// </summary>
        static GameObject Notice(RectTransform canvas, string name, string disc, string glyph, string title, string line)
        {
            var size = new Vector2(780f, 136f);
            RectTransform root = Node(name, canvas, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Middle, new Vector2(0f, 330f), size);
            root.gameObject.AddComponent<CanvasGroup>();
            root.gameObject.AddComponent<UiAutoHide>();

            RectTransform card = Center("Card", root, Vector2.zero, size);
            DropShadow(card, 22f);
            Paint(Fill("Face", card), HudSprites.Card, Color.white, raycast: true, size: size);

            RectTransform round = At("Disc", card, new Vector2(0f, 0.5f), new Vector2(22f, 4f), new Vector2(92f, 92f));
            Paint(round, disc, Color.white);
            Paint(Center("Glyph", round, new Vector2(0f, 5f), new Vector2(46f, 46f)), glyph, Color.white);

            Label(At("Title", card, new Vector2(0f, 0.5f), new Vector2(132f, 22f), new Vector2(620f, 48f)), title, Face.Display, 34f,
                  HudPalette.Ink, TextAlignmentOptions.Left).Fit(22f);
            Label(At("Line", card, new Vector2(0f, 0.5f), new Vector2(132f, -24f), new Vector2(620f, 36f)), line, Face.Body, 23f,
                  HudPalette.InkSoft, TextAlignmentOptions.Left).Fit(16f);

            root.gameObject.SetActive(false);
            return root.gameObject;
        }
    }
}
