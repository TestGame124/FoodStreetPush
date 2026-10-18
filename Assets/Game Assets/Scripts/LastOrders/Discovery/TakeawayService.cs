using System;
using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Kitchen;
using UnityEngine;

namespace LastOrders.Discovery
{
    /// <summary>Where a drive-through car is: driving in to its place in the lane, waiting in it, or gone.</summary>
    public enum TakeawayCarState
    {
        Arriving,
        Waiting,
        Leaving,
    }

    /// <summary>
    /// One car in the drive-through lane and the bag it wants: a dish per slot, each slot filled
    /// from the pass when the player packs it.
    /// </summary>
    public sealed class TakeawayOrder
    {
        readonly List<ItemType> dishes;
        readonly bool[] packed;
        float patience;

        internal TakeawayOrder(int number, int look, List<ItemType> dishes, float patienceSeconds)
        {
            Number = number;
            Look = look;
            this.dishes = dishes;
            packed = new bool[dishes.Count];
            PatienceSeconds = Mathf.Max(1f, patienceSeconds);
            patience = PatienceSeconds;
        }

        /// <summary>The ticket number, counting up from one each day: what the popup calls the car.</summary>
        public int Number { get; }

        /// <summary>Which of the window's cars this one is, so a car looks the same every time it is drawn.</summary>
        public int Look { get; }

        public TakeawayCarState State { get; internal set; }

        public IReadOnlyList<ItemType> Dishes => dishes;

        public int Count => dishes.Count;

        public bool IsPacked(int slot) => slot >= 0 && slot < packed.Length && packed[slot];

        public int PackedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < packed.Length; i++)
                    if (packed[i]) n++;
                return n;
            }
        }

        public bool IsComplete => PackedCount == packed.Length;

        public float PatienceSeconds { get; }

        public float SecondsLeft => Mathf.Max(0f, patience);

        /// <summary>1 when the car has just pulled up, 0 when it is about to give up.</summary>
        public float PatienceLeft => Mathf.Clamp01(patience / PatienceSeconds);

        /// <summary>The first slot still waiting for <paramref name="dish"/>, or -1.</summary>
        public int SlotNeeding(ItemType dish)
        {
            for (int i = 0; i < dishes.Count; i++)
                if (!packed[i] && dishes[i] == dish) return i;
            return -1;
        }

        internal void Pack(int slot) => packed[slot] = true;

        internal void Unpack(int slot) => packed[slot] = false;

        internal void Wait(float deltaSeconds) => patience -= deltaSeconds;
    }

    /// <summary>
    /// The drive-through window's rules: cars turning in off the road, each wanting a bag of dishes
    /// the kitchen makes; the player packing each bag from what is already cooked on the pass and
    /// handing it out of the window to the car at the front; a car that waits too long giving up.
    ///
    /// It uses the kitchen's own food - nothing is cooked here - so it competes with the dining
    /// room for every plate, and a car may ask for something there is none of yet. It knows
    /// nothing about the cars themselves: the booth in the scene drives them, and says when one
    /// has pulled up in its place.
    /// </summary>
    public sealed class TakeawayService
    {
        readonly TakeawayTuning tuning;
        readonly IDishStore kitchen;
        readonly DishCatalogue dishes;
        readonly Func<bool> isOpen;
        readonly Func<bool> isUnlocked;
        readonly Action<int> pay;
        readonly Action<int> addExperience;

        readonly List<TakeawayOrder> lane = new List<TakeawayOrder>();
        readonly List<ItemType> scratch = new List<ItemType>();

        DeterministicRandom random;
        float nextArrival;
        int nextNumber = 1;
        bool wasOpen;

        /// <summary>A car has turned in and is driving to its place in the lane.</summary>
        public event Action<TakeawayOrder> OnArriving;

        /// <summary>A car is leaving: true with its bag, false because it gave up or the window shut.</summary>
        public event Action<TakeawayOrder, bool> OnLeaving;

        /// <summary>A car left the lane and those behind it move up.</summary>
        public event Action OnLaneMoved;

        /// <summary>Anything in the lane changed: a car, a packed dish, the order at the front.</summary>
        public event Action OnChanged;

        public TakeawayService(TakeawayTuning tuning, IDishStore kitchen, DishCatalogue dishes, Func<bool> isOpen, Func<bool> isUnlocked,
                               Action<int> pay, Action<int> addExperience, int seed)
        {
            this.tuning = tuning ?? new TakeawayTuning();
            this.kitchen = kitchen;
            this.dishes = dishes ?? new DishCatalogue();
            this.isOpen = isOpen;
            this.isUnlocked = isUnlocked;
            this.pay = pay;
            this.addExperience = addExperience;

            random = new DeterministicRandom(seed);

            // The first car comes soon after opening: the window is worth seeing work straight away.
            nextArrival = Mathf.Min(8f, this.tuning.arrivalSeconds.x);
        }

        /// <summary>
        /// There is a booth in the scene to drive the cars. Without one, cars would turn in and never
        /// arrive anywhere, so none are sent.
        /// </summary>
        public bool HasWindow { get; set; }

        /// <summary>Cars in the lane, the one at the window first.</summary>
        public IReadOnlyList<TakeawayOrder> Lane => lane;

        public int LaneLength => Mathf.Max(1, tuning.laneLength);

        public TakeawayOrder AtWindow => lane.Count > 0 ? lane[0] : null;

        public bool IsOpen => HasWindow && (isUnlocked == null || isUnlocked()) && (isOpen == null || isOpen());

        /// <summary>Where <paramref name="order"/> is in the lane: 0 at the window, -1 once it has gone.</summary>
        public int PlaceOf(TakeawayOrder order) => lane.IndexOf(order);

        public int Price(ItemType dish) => Mathf.Max(1, Mathf.RoundToInt(dishes.Price(dish) * tuning.priceMultiplier));

        /// <summary>What a bag pays handed over now: its dishes, and the tip if the car is still patient.</summary>
        public int Bill(TakeawayOrder order, out int tip)
        {
            int total = 0;
            for (int i = 0; i < order.Count; i++) total += Price(order.Dishes[i]);

            tip = order.PatienceLeft > 0.5f ? Mathf.RoundToInt(total * tuning.quickTip) : 0;
            return total + tip;
        }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            bool open = IsOpen;
            if (!open)
            {
                // Closing time - or the window lost: everyone still waiting drives off, and their
                // packed dishes go back on the pass for the morning.
                if (wasOpen || lane.Count > 0) SendAllAway();
                wasOpen = false;
                return;
            }

            if (!wasOpen)
            {
                wasOpen = true;
                nextArrival = Mathf.Min(nextArrival, Mathf.Min(8f, tuning.arrivalSeconds.x));
            }

            for (int i = lane.Count - 1; i >= 0; i--)
            {
                TakeawayOrder order = lane[i];
                if (order.State != TakeawayCarState.Waiting) continue;

                order.Wait(deltaSeconds);
                if (order.SecondsLeft <= 0f) Leave(order, served: false);
            }

            nextArrival -= deltaSeconds;
            if (nextArrival > 0f) return;

            if (lane.Count < LaneLength) TrySendCar();

            Vector2 gap = tuning.arrivalSeconds;
            nextArrival = random.Range(Mathf.Max(1f, gap.x), Mathf.Max(gap.x + 0.01f, gap.y));
        }

        /// <summary>The car has pulled up in its place in the lane: it starts to wait.</summary>
        public void MarkArrived(TakeawayOrder order)
        {
            if (order == null || order.State != TakeawayCarState.Arriving || !lane.Contains(order)) return;

            order.State = TakeawayCarState.Waiting;
            OnChanged?.Invoke();
        }

        /// <summary>Packs <paramref name="slot"/> of <paramref name="order"/>'s bag from the pass.</summary>
        public bool TryPack(TakeawayOrder order, int slot, out string message)
        {
            if (order == null || !lane.Contains(order) || order.State == TakeawayCarState.Leaving)
            {
                message = "That car has gone.";
                return false;
            }

            if (slot < 0 || slot >= order.Count)
            {
                message = null;
                return false;
            }

            if (order.IsPacked(slot))
            {
                message = "That one is already in the bag.";
                return false;
            }

            ItemType dish = order.Dishes[slot];
            if (kitchen == null || !kitchen.TryTake(dish))
            {
                message = $"No {DishName(dish)} on the pass. Cook some first.";
                return false;
            }

            order.Pack(slot);
            OnChanged?.Invoke();

            message = null;
            return true;
        }

        /// <summary>
        /// Packs one <paramref name="dish"/> into the bag that needs it, the car nearest the window
        /// first - or into <paramref name="preferred"/>'s if that one needs it.
        /// </summary>
        public bool TryPackDish(ItemType dish, TakeawayOrder preferred, out string message)
        {
            if (preferred != null && lane.Contains(preferred))
            {
                int slot = preferred.SlotNeeding(dish);
                if (slot >= 0) return TryPack(preferred, slot, out message);
            }

            for (int i = 0; i < lane.Count; i++)
            {
                int slot = lane[i].SlotNeeding(dish);
                if (slot >= 0) return TryPack(lane[i], slot, out message);
            }

            message = $"Nobody in the lane ordered {DishName(dish)}.";
            return false;
        }

        /// <summary>
        /// Hands <paramref name="order"/>'s bag out of the window and takes the money. Only the car at
        /// the window can be served, and only with everything it asked for in the bag.
        /// </summary>
        public bool TryHandOver(TakeawayOrder order, out string message)
        {
            if (order == null || !lane.Contains(order))
            {
                message = "That car has gone.";
                return false;
            }

            if (lane[0] != order || order.State != TakeawayCarState.Waiting)
            {
                message = "Serve the car at the window first.";
                return false;
            }

            if (!order.IsComplete)
            {
                int missing = order.Count - order.PackedCount;
                message = missing == 1 ? "One dish still to pack." : $"{missing} dishes still to pack.";
                return false;
            }

            int coins = Bill(order, out int tip);
            pay?.Invoke(coins);
            addExperience?.Invoke(tuning.experiencePerBag);

            Leave(order, served: true);

            message = tip > 0 ? $"Order {order.Number} handed over: {coins} coins, tip included!" : $"Order {order.Number} handed over: {coins} coins.";
            return true;
        }

        /// <summary>Every car drives off, and whatever was packed for them goes back on the pass.</summary>
        public void SendAllAway()
        {
            for (int i = lane.Count - 1; i >= 0; i--) Leave(lane[i], served: false);
            nextNumber = 1;
        }

        void TrySendCar()
        {
            List<ItemType> menu = Menu();
            if (menu.Count == 0) return;

            Vector2Int size = tuning.dishesPerBag;
            int count = random.Range(Mathf.Max(1, size.x), Mathf.Max(size.x, size.y) + 1);

            // What is on the pass now is what most bags ask for; some ask for what still has to be
            // cooked, so the window is a reason to cook as well as somewhere to sell.
            scratch.Clear();
            foreach (ItemType dish in menu)
                if (kitchen != null && kitchen.Spare(dish) > 0) scratch.Add(dish);

            var bag = new List<ItemType>(count);
            for (int i = 0; i < count; i++)
            {
                bool fromPass = scratch.Count > 0 && !random.Chance(tuning.cookToOrderChance);
                List<ItemType> from = fromPass ? scratch : menu;
                bag.Add(from[random.Range(0, from.Count)]);
            }

            // Grouped, so a bag of two soups and a bread reads that way round.
            bag.Sort((a, b) => ((int)a).CompareTo((int)b));

            var order = new TakeawayOrder(nextNumber++, random.Range(0, 1 << 16), bag, tuning.patienceSeconds)
            {
                State = TakeawayCarState.Arriving,
            };

            lane.Add(order);
            OnArriving?.Invoke(order);
            OnChanged?.Invoke();
        }

        List<ItemType> Menu()
        {
            var menu = new List<ItemType>();
            if (kitchen == null || kitchen.Menu == null) return menu;

            foreach (ItemType dish in kitchen.Menu)
                if (dishes.CanCook(dish)) menu.Add(dish);

            // In a fixed order, so the same seed asks for the same dishes however the kitchen listed them.
            menu.Sort((a, b) => ((int)a).CompareTo((int)b));
            return menu;
        }

        void Leave(TakeawayOrder order, bool served)
        {
            int at = lane.IndexOf(order);
            if (at < 0) return;

            lane.RemoveAt(at);

            if (!served)
            {
                for (int i = 0; i < order.Count; i++)
                {
                    if (!order.IsPacked(i)) continue;

                    kitchen?.PutBack(order.Dishes[i]);
                    order.Unpack(i);
                }
            }

            order.State = TakeawayCarState.Leaving;
            OnLeaving?.Invoke(order, served);
            if (at < lane.Count) OnLaneMoved?.Invoke();
            OnChanged?.Invoke();
        }

        public string DishName(ItemType dish) => dishes.Name(dish);

        /// <summary>How many of <paramref name="dish"/> could go out of the window now.</summary>
        public int Spare(ItemType dish) => kitchen != null ? kitchen.Spare(dish) : 0;

        /// <summary>Dishes the kitchen makes that have at least one on the pass now.</summary>
        public List<ItemType> MenuOnPass()
        {
            List<ItemType> menu = Menu();
            menu.RemoveAll(dish => Spare(dish) <= 0);
            return menu;
        }

        /// <summary>"TomatoSoup" as "tomato soup", for a dish with no data to name it.</summary>
        internal static string Spaced(string name)
        {
            var text = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i])) text.Append(' ');
                text.Append(char.ToLowerInvariant(name[i]));
            }

            return text.ToString();
        }
    }
}
