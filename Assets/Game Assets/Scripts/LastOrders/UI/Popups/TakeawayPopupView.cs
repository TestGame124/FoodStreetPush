using System;
using System.Collections.Generic;
using LastOrders.Discovery;
using LastOrders.Interaction;
using TMPro;
using UnityEngine;

namespace LastOrders.UI.Popups
{
    /// <summary>
    /// The drive-through window: what is cooked and on the pass along the top, the bags of the
    /// cars in the lane below. Tapping a dish on the pass packs it into the bag that wants it -
    /// the one picked, or the car nearest the window - and tapping an empty slot in a bag packs
    /// that dish if there is one. A full bag at the window is handed over with its button.
    /// </summary>
    public sealed class TakeawayPopupView : PopupView
    {
        [SerializeField] TMP_Text title;

        [Tooltip("The plates on the pass, pre-built; as many shown as there are kinds of dish ready.")]
        [SerializeField] DishTileView[] tray;

        [Tooltip("Shown when nothing at all is on the pass.")]
        [SerializeField] TMP_Text trayEmpty;

        [Tooltip("The cars' bags, the one at the window first.")]
        [SerializeField] TakeawayBagView[] bags;

        [Tooltip("Shown when the lane is empty.")]
        [SerializeField] TMP_Text laneEmpty;

        [Tooltip("A hint, or why the last tap did nothing.")]
        [SerializeField] TMP_Text status;

        [SerializeField] Color readyColour = new Color(0.42f, 0.78f, 0.72f);
        [SerializeField] Color blockedColour = new Color(0.80f, 0.78f, 0.75f);
        [SerializeField] Color cardColour = Color.white;
        [SerializeField] Color windowCardColour = new Color(1f, 0.97f, 0.86f);
        [SerializeField] Color calmColour = new Color(0.42f, 0.70f, 0.42f);
        [SerializeField] Color hurryColour = new Color(0.92f, 0.60f, 0.24f);
        [SerializeField] Color leavingColour = new Color(0.78f, 0.31f, 0.25f);

        [SerializeField] float refreshInterval = 0.15f;

        readonly List<ItemType> shown = new List<ItemType>();
        readonly List<ItemType> scratch = new List<ItemType>();

        TakeawayService service;
        Func<TakeawayOrder, bool> handOver;
        TakeawayOrder selected;
        float nextRefresh;
        string lastMessage;
        float messageUntil;

        public object Owner { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            for (int i = 0; i < tray.Length; i++)
            {
                int index = i;
                if (tray[i] != null && tray[i].Button != null) tray[i].Button.onClick.AddListener(() => HandleTray(index));
            }

            for (int b = 0; b < bags.Length; b++)
            {
                int bag = b;
                TakeawayBagView view = bags[b];
                if (view == null) continue;

                if (view.Select != null) view.Select.onClick.AddListener(() => HandleSelect(bag));
                if (view.HandOverButton != null) view.HandOverButton.onClick.AddListener(() => HandleHandOver(bag));

                for (int s = 0; s < view.SlotCount; s++)
                {
                    int slot = s;
                    DishTileView tile = view.Slot(s);
                    if (tile != null && tile.Button != null) tile.Button.onClick.AddListener(() => HandleSlot(bag, slot));
                }
            }
        }

        public void Bind(object owner, TakeawayService takeaway, Func<TakeawayOrder, bool> handOverBag)
        {
            Owner = owner;
            service = takeaway;
            handOver = handOverBag;
            selected = null;
            lastMessage = null;
            Refresh();
        }

        void Update()
        {
            if (service == null) return;

            nextRefresh -= Time.unscaledDeltaTime;
            if (nextRefresh > 0f) return;

            Refresh();
        }

        void Refresh()
        {
            nextRefresh = refreshInterval;
            if (service == null) return;

            // The window shut while the popup was up: there is nothing left to do here.
            if (!service.IsOpen)
            {
                Close();
                return;
            }

            IReadOnlyList<TakeawayOrder> lane = service.Lane;
            if (selected != null && service.PlaceOf(selected) < 0) selected = null;

            if (title != null) title.text = lane.Count == 1 ? "Drive-through: 1 car" : $"Drive-through: {lane.Count} cars";

            DrawTray(lane);
            DrawBags(lane);

            if (status != null)
            {
                if (Time.unscaledTime < messageUntil && !string.IsNullOrEmpty(lastMessage)) status.text = lastMessage;
                else status.text = Hint(lane);
            }
        }

        /// <summary>What is on the pass: the dishes a car is waiting for first, then the rest.</summary>
        void DrawTray(IReadOnlyList<TakeawayOrder> lane)
        {
            shown.Clear();
            scratch.Clear();

            foreach (TakeawayOrder order in lane)
                for (int i = 0; i < order.Count; i++)
                    if (!order.IsPacked(i) && !scratch.Contains(order.Dishes[i])) scratch.Add(order.Dishes[i]);

            foreach (ItemType dish in scratch)
                if (service.Spare(dish) > 0) shown.Add(dish);

            foreach (ItemType dish in service.MenuOnPass())
                if (!shown.Contains(dish)) shown.Add(dish);

            for (int i = 0; i < tray.Length; i++)
            {
                if (tray[i] == null) continue;
                if (i >= shown.Count)
                {
                    tray[i].Hide();
                    continue;
                }

                ItemType dish = shown[i];
                bool wanted = scratch.Contains(dish);
                tray[i].Show(DishCatalogue.Icon(dish), service.DishName(dish), $"x{service.Spare(dish)}", true, false, wanted, !wanted);
            }

            if (trayEmpty != null) trayEmpty.gameObject.SetActive(shown.Count == 0);
        }

        void DrawBags(IReadOnlyList<TakeawayOrder> lane)
        {
            for (int b = 0; b < bags.Length; b++)
            {
                TakeawayBagView view = bags[b];
                if (view == null) continue;

                if (b >= lane.Count)
                {
                    view.Hide();
                    continue;
                }

                TakeawayOrder order = lane[b];
                bool atWindow = b == 0;
                bool here = order.State == TakeawayCarState.Waiting;
                int bill = service.Bill(order, out int tip);

                string where = atWindow ? "AT THE WINDOW" : b == 1 ? "NEXT" : "IN THE LANE";
                if (!here) where = "PULLING IN";

                float patience = order.PatienceLeft;
                Color mood = patience > 0.5f ? calmColour : patience > 0.2f ? hurryColour : leavingColour;
                view.Show($"{where}  #{order.Number}", tip > 0 ? $"{bill} coins (tip!)" : $"{bill} coins", patience, mood,
                          order == selected, atWindow ? windowCardColour : cardColour);

                for (int s = 0; s < view.SlotCount; s++)
                {
                    DishTileView tile = view.Slot(s);
                    if (tile == null) continue;

                    if (s >= order.Count)
                    {
                        tile.Hide();
                        continue;
                    }

                    ItemType dish = order.Dishes[s];
                    bool packed = order.IsPacked(s);
                    bool available = service.Spare(dish) > 0;
                    tile.Show(DishCatalogue.Icon(dish), service.DishName(dish), null, !packed, packed, !packed && available, !packed && !available);
                }

                bool ready = atWindow && here && order.IsComplete;
                string label = order.IsComplete ? "HAND OVER" : $"{order.PackedCount}/{order.Count} PACKED";
                view.ShowHandOver(atWindow, ready, label, ready ? readyColour : blockedColour);
            }

            if (laneEmpty != null) laneEmpty.gameObject.SetActive(lane.Count == 0);
        }

        string Hint(IReadOnlyList<TakeawayOrder> lane)
        {
            if (lane.Count == 0) return "Cars turn in off the road while the dining room is open.";

            TakeawayOrder front = lane[0];
            if (front.IsComplete && front.State == TakeawayCarState.Waiting) return "The bag at the window is full - hand it over!";

            for (int i = 0; i < front.Count; i++)
                if (!front.IsPacked(i) && service.Spare(front.Dishes[i]) <= 0)
                    return $"No {service.DishName(front.Dishes[i])} on the pass - cook some, quick.";

            return "Tap a dish on the pass to pack it into the bag that wants it.";
        }

        // --- Taps ----------------------------------------------------------------------------------

        void HandleTray(int index)
        {
            if (service == null || index >= shown.Count) return;

            bool packed = service.TryPackDish(shown[index], selected, out string message);
            Say(packed ? null : message);
            if (IsOpen) Refresh();
        }

        void HandleSlot(int bag, int slot)
        {
            if (service == null || bag >= service.Lane.Count) return;

            TakeawayOrder order = service.Lane[bag];
            selected = order;

            bool packed = service.TryPack(order, slot, out string message);
            Say(packed ? null : message);
            if (IsOpen) Refresh();
        }

        void HandleSelect(int bag)
        {
            if (service == null || bag >= service.Lane.Count) return;

            TakeawayOrder order = service.Lane[bag];
            selected = selected == order ? null : order;
            if (IsOpen) Refresh();
        }

        void HandleHandOver(int bag)
        {
            if (service == null || bag >= service.Lane.Count || handOver == null) return;

            TakeawayOrder order = service.Lane[bag];
            if (!handOver(order))
            {
                Say(null);
                if (IsOpen) Refresh();
                return;
            }

            if (selected == order) selected = null;
            if (IsOpen) Refresh();
        }

        void Say(string message)
        {
            lastMessage = message;
            messageUntil = string.IsNullOrEmpty(message) ? 0f : Time.unscaledTime + 2.5f;
            if (!string.IsNullOrEmpty(message)) TapFeedback.Report(message);
        }

        protected override void OnClosed()
        {
            service = null;
            handOver = null;
            selected = null;
            Owner = null;
        }
    }
}
