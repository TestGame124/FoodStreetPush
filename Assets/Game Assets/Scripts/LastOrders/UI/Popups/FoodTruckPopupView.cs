using System;
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.Discovery;
using LastOrders.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Popups
{
    /// <summary>
    /// The food truck: on the left what is on the pass to load, a minus and a plus each; on the
    /// right a little map of the road with the restaurant on it and the few places the truck can
    /// go. Picking a place shows who is there, what they pay extra for - starred in the list - and
    /// what the load aboard would fetch, so what to load and where to go are weighed together.
    /// While the truck is out the popup only says where, and how long until it is back.
    /// </summary>
    public sealed class FoodTruckPopupView : PopupView
    {
        [SerializeField] TMP_Text title;

        [Header("Loading")]
        [SerializeField] TruckLoadRowView[] rows;
        [SerializeField] TMP_Text aboard;
        [SerializeField] TMP_Text listHint;

        [Header("The map")]
        [SerializeField] RectTransform map;
        [SerializeField] RectTransform home;
        [SerializeField] MapPinView[] pins;

        [Tooltip("The dotted road from the restaurant to the place picked, stretched between them.")]
        [SerializeField] RectTransform route;

        [SerializeField] TMP_Text placeName;
        [SerializeField] TMP_Text placeBlurb;
        [SerializeField] Image[] favourites;
        [SerializeField] TMP_Text estimate;

        [Header("Sending")]
        [SerializeField] TMP_Text reason;
        [SerializeField] Button sendButton;
        [SerializeField] Image sendButtonImage;
        [SerializeField] TMP_Text sendButtonLabel;

        [Header("Out on the road")]
        [SerializeField] GameObject outPanel;
        [SerializeField] TMP_Text outText;
        [SerializeField] Image outBar;

        [SerializeField] Color readyColour = new Color(0.42f, 0.78f, 0.72f);
        [SerializeField] Color blockedColour = new Color(0.80f, 0.78f, 0.75f);
        [SerializeField] Color pinColour = new Color(0.78f, 0.31f, 0.25f);
        [SerializeField] Color pickedColour = new Color(0.96f, 0.75f, 0.27f);

        [SerializeField] float refreshInterval = 0.2f;

        readonly List<ItemType> listed = new List<ItemType>();

        FoodTruckService service;
        Func<FoodTruckDestination, bool> send;
        FoodTruckDestination picked;
        float nextRefresh;

        public object Owner { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            for (int i = 0; i < rows.Length; i++)
            {
                int index = i;
                if (rows[i] == null) continue;
                if (rows[i].Less != null) rows[i].Less.onClick.AddListener(() => HandleLoad(index, false));
                if (rows[i].More != null) rows[i].More.onClick.AddListener(() => HandleLoad(index, true));
            }

            for (int i = 0; i < pins.Length; i++)
            {
                int index = i;
                if (pins[i] != null && pins[i].Button != null) pins[i].Button.onClick.AddListener(() => HandlePick(index));
            }

            if (sendButton != null) sendButton.onClick.AddListener(HandleSend);
        }

        public void Bind(object owner, FoodTruckService truck, Func<FoodTruckDestination, bool> sendTo)
        {
            Owner = owner;
            service = truck;
            send = sendTo;

            if (picked == null || service.Find(picked.id) == null)
                picked = service.Destinations.Count > 0 ? service.Destinations[0] : null;

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

            bool isOut = service.State == FoodTruckState.Out;
            if (outPanel != null) outPanel.SetActive(isOut);

            if (isOut)
            {
                FoodTruckDestination heading = service.Heading;
                int left = Mathf.CeilToInt(service.SecondsLeft);
                if (title != null) title.text = "Food truck: out on the road";
                if (outText != null)
                    outText.text = $"Selling at {(heading != null ? heading.displayName : "the road")}.\nBack in {left / 60}:{left % 60:00}.";
                if (outBar != null) outBar.fillAmount = service.TripProgress;
            }
            else if (title != null)
            {
                title.text = "Food truck";
            }

            DrawList(isOut);
            DrawMap();
            DrawSend(isOut);
        }

        /// <summary>
        /// The dishes worth listing: everything aboard, then what is on the pass - the picked place's
        /// favourites first - as many as there are rows.
        /// </summary>
        void DrawList(bool isOut)
        {
            listed.Clear();
            foreach (ItemType dish in service.LoadOrder) listed.Add(dish);

            var ready = new List<ItemType>();
            foreach (ItemType dish in service.Menu())
                if (!listed.Contains(dish) && service.Spare(dish) > 0) ready.Add(dish);

            ready.Sort((a, b) =>
            {
                bool fa = picked != null && picked.IsFavourite(a), fb = picked != null && picked.IsFavourite(b);
                if (fa != fb) return fa ? -1 : 1;
                return service.Spare(b).CompareTo(service.Spare(a));
            });
            listed.AddRange(ready);

            bool full = service.Loaded >= service.Capacity;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) continue;
                if (i >= listed.Count)
                {
                    rows[i].Hide();
                    continue;
                }

                ItemType dish = listed[i];
                int spare = service.Spare(dish);
                int loaded = service.CountOf(dish);
                rows[i].Show(DishCatalogue.Icon(dish), service.DishName(dish), spare, loaded, !isOut && !full && spare > 0, !isOut && loaded > 0,
                             picked != null && picked.IsFavourite(dish), picked != null ? service.PriceAt(picked, dish) : 0);
            }

            if (aboard != null) aboard.text = $"{service.Loaded} / {service.Capacity} ABOARD";

            if (listHint == null) return;
            int hidden = listed.Count - rows.Length;
            listHint.text = listed.Count == 0 ? "Nothing on the pass. Cook something to sell."
                : hidden > 0 ? $"+{hidden} more on the pass"
                : "Starred dishes sell for extra where the truck is going.";
        }

        void DrawMap()
        {
            IReadOnlyList<FoodTruckDestination> places = service.Destinations;

            for (int i = 0; i < pins.Length; i++)
            {
                if (pins[i] == null) continue;
                if (i >= places.Count || places[i] == null)
                {
                    pins[i].Hide();
                    continue;
                }

                FoodTruckDestination place = places[i];
                int seconds = Mathf.RoundToInt(place.tripSeconds);
                bool isPicked = place == picked;
                pins[i].Show(place.mapPosition, place.displayName, $"{seconds / 60}:{seconds % 60:00}", isPicked, isPicked ? pickedColour : pinColour);
            }

            DrawRoute();

            if (picked == null) return;

            if (placeName != null) placeName.text = picked.displayName;
            if (placeBlurb != null) placeBlurb.text = picked.blurb;

            for (int i = 0; i < favourites.Length; i++)
            {
                if (favourites[i] == null) continue;

                bool has = picked.favourites != null && i < picked.favourites.Length;
                Sprite icon = has ? DishCatalogue.Icon(picked.favourites[i]) : null;
                favourites[i].gameObject.SetActive(has && icon != null);
                if (icon != null) favourites[i].sprite = icon;
            }

            if (estimate != null)
            {
                int coins = service.Estimate(picked);
                int stars = service.FavouritesAboard(picked);
                estimate.text = service.Loaded == 0 ? "Load some dishes to see what they would fetch."
                    : $"This load would fetch about {coins} coins" + (stars > 0 ? $" ({stars} favourites)." : ".");
            }
        }

        /// <summary>The line from the restaurant to the picked pin, stretched and turned to join them.</summary>
        void DrawRoute()
        {
            if (route == null || home == null || map == null) return;

            MapPinView pin = null;
            IReadOnlyList<FoodTruckDestination> places = service.Destinations;
            for (int i = 0; i < pins.Length && i < places.Count; i++)
                if (places[i] == picked) pin = pins[i];

            route.gameObject.SetActive(pin != null);
            if (pin == null) return;

            Vector2 size = map.rect.size;
            Vector2 from = Local(home, size);
            Vector2 to = Local(pin.Rect, size);
            Vector2 d = to - from;

            route.anchorMin = route.anchorMax = new Vector2(0.5f, 0.5f);
            route.anchoredPosition = (from + to) * 0.5f;
            route.sizeDelta = new Vector2(d.magnitude, route.sizeDelta.y);
            route.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        /// <summary>A pin's place on the map, measured from the map's middle.</summary>
        static Vector2 Local(RectTransform pin, Vector2 mapSize) =>
            new Vector2((pin.anchorMin.x - 0.5f) * mapSize.x, (pin.anchorMin.y - 0.5f) * mapSize.y) + pin.anchoredPosition;

        void DrawSend(bool isOut)
        {
            bool can = service.CanSend(picked, out string why);

            if (reason != null) reason.text = isOut || can ? "" : why;
            if (sendButton != null) sendButton.interactable = can;
            if (sendButtonImage != null) sendButtonImage.color = can ? readyColour : blockedColour;
            if (sendButtonLabel != null) sendButtonLabel.text = picked != null ? $"SEND TO {ShortName(picked)}" : "SEND";
        }

        static string ShortName(FoodTruckDestination place)
        {
            string name = place.displayName ?? "";
            int space = name.LastIndexOf(' ');
            return (space > 0 ? name.Substring(space + 1) : name).ToUpperInvariant();
        }

        // --- Taps ----------------------------------------------------------------------------------

        void HandleLoad(int row, bool load)
        {
            if (service == null || row >= listed.Count) return;

            ItemType dish = listed[row];
            string message;
            bool done = load ? service.TryLoad(dish, out message) : service.TryUnload(dish, out message);
            if (!done && !string.IsNullOrEmpty(message)) TapFeedback.Report(message);

            if (IsOpen) Refresh();
        }

        void HandlePick(int index)
        {
            if (service == null || index >= service.Destinations.Count) return;

            picked = service.Destinations[index];
            if (IsOpen) Refresh();
        }

        void HandleSend()
        {
            if (service == null || send == null) return;

            // Closed first when it goes: what happens next is out on the road.
            if (send(picked))
            {
                Close();
                return;
            }

            if (IsOpen) Refresh();
        }

        protected override void OnClosed()
        {
            service = null;
            send = null;
            Owner = null;
        }
    }
}
