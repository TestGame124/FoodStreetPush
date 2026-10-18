using System;
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.Kitchen;
using UnityEngine;

namespace LastOrders.Discovery
{
    public enum FoodTruckState
    {
        /// <summary>On its pad by the road, being loaded.</summary>
        Parked,

        /// <summary>Out on a trip, selling; back when the trip's time is up.</summary>
        Out,
    }

    /// <summary>
    /// The food truck's rules: one truck, loaded with dishes off the pass, sent to one place on a
    /// small map, and back again with whatever they paid. Each place pays extra for its own
    /// favourites and less for the rest, so what to load and where to send it is one decision.
    ///
    /// Dishes loaded leave the pass at once; unloading before the truck goes puts them back. The
    /// truck in the scene only drives what this says - out along the road when it leaves, home in
    /// time for the takings.
    /// </summary>
    public sealed class FoodTruckService
    {
        readonly FoodTruckTuning tuning;
        readonly IDishStore kitchen;
        readonly DishCatalogue dishes;
        readonly Func<bool> canSend;
        readonly Func<bool> isUnlocked;
        readonly Action<int> pay;
        readonly Action<int> addExperience;

        readonly Dictionary<ItemType, int> cargo = new Dictionary<ItemType, int>();
        readonly List<ItemType> loadOrder = new List<ItemType>();

        float secondsLeft;
        int takings;

        /// <summary>The truck has set off for <see cref="Heading"/>.</summary>
        public event Action<FoodTruckDestination> OnSent;

        /// <summary>The truck is back: where it went, what it took and how many dishes it sold.</summary>
        public event Action<FoodTruckDestination, int, int> OnReturned;

        /// <summary>The load or the state changed.</summary>
        public event Action OnChanged;

        public FoodTruckService(FoodTruckTuning tuning, IDishStore kitchen, DishCatalogue dishes, Func<bool> canSend, Func<bool> isUnlocked,
                                Action<int> pay, Action<int> addExperience)
        {
            this.tuning = tuning ?? new FoodTruckTuning();
            this.kitchen = kitchen;
            this.dishes = dishes ?? new DishCatalogue();
            this.canSend = canSend;
            this.isUnlocked = isUnlocked;
            this.pay = pay;
            this.addExperience = addExperience;
        }

        public FoodTruckState State { get; private set; }

        public FoodTruckDestination Heading { get; private set; }

        public IReadOnlyList<FoodTruckDestination> Destinations => tuning.destinations ?? new FoodTruckDestination[0];

        public int Capacity => Mathf.Max(1, tuning.capacity);

        public int Loaded
        {
            get
            {
                int n = 0;
                foreach (int count in cargo.Values) n += count;
                return n;
            }
        }

        /// <summary>The kinds of dish aboard, in the order they were first loaded.</summary>
        public IReadOnlyList<ItemType> LoadOrder => loadOrder;

        public int CountOf(ItemType dish) => cargo.TryGetValue(dish, out int n) ? n : 0;

        public float SecondsLeft => State == FoodTruckState.Out ? Mathf.Max(0f, secondsLeft) : 0f;

        /// <summary>How far through the trip it is, 0 to 1.</summary>
        public float TripProgress =>
            State == FoodTruckState.Out && Heading != null ? 1f - Mathf.Clamp01(secondsLeft / Mathf.Max(0.1f, Heading.tripSeconds)) : 0f;

        public bool IsUnlocked => isUnlocked == null || isUnlocked();

        public string DishName(ItemType dish) => dishes.Name(dish);

        public int Spare(ItemType dish) => kitchen != null ? kitchen.Spare(dish) : 0;

        /// <summary>Dishes the kitchen makes that the player can cook now, in a fixed order: what the loading list shows.</summary>
        public List<ItemType> Menu()
        {
            var menu = new List<ItemType>();
            if (kitchen?.Menu == null) return menu;

            foreach (ItemType dish in kitchen.Menu)
                if (dishes.CanCook(dish)) menu.Add(dish);

            menu.Sort((a, b) => ((int)a).CompareTo((int)b));
            return menu;
        }

        public FoodTruckDestination Find(string id)
        {
            foreach (FoodTruckDestination place in Destinations)
                if (place != null && place.id == id) return place;
            return null;
        }

        /// <summary>What one <paramref name="dish"/> sells for at <paramref name="place"/>.</summary>
        public int PriceAt(FoodTruckDestination place, ItemType dish)
        {
            float multiplier = place == null ? 1f : place.IsFavourite(dish) ? place.favouriteMultiplier : place.otherMultiplier;
            return Mathf.Max(1, Mathf.RoundToInt(dishes.Price(dish) * multiplier));
        }

        /// <summary>What the load aboard now would fetch at <paramref name="place"/>.</summary>
        public int Estimate(FoodTruckDestination place)
        {
            int total = 0;
            foreach (KeyValuePair<ItemType, int> entry in cargo) total += PriceAt(place, entry.Key) * entry.Value;
            return total;
        }

        /// <summary>How many of the load are <paramref name="place"/>'s favourites.</summary>
        public int FavouritesAboard(FoodTruckDestination place)
        {
            int n = 0;
            foreach (KeyValuePair<ItemType, int> entry in cargo)
                if (place != null && place.IsFavourite(entry.Key)) n += entry.Value;
            return n;
        }

        // --- Loading --------------------------------------------------------------------------------

        public bool TryLoad(ItemType dish, out string message)
        {
            if (!CanTouchLoad(out message)) return false;

            if (Loaded >= Capacity)
            {
                message = $"The truck is full: {Capacity} dishes.";
                return false;
            }

            if (kitchen == null || !kitchen.TryTake(dish))
            {
                message = $"No {DishName(dish)} on the pass. Cook some first.";
                return false;
            }

            if (!cargo.ContainsKey(dish)) loadOrder.Add(dish);
            cargo[dish] = CountOf(dish) + 1;
            OnChanged?.Invoke();

            message = null;
            return true;
        }

        public bool TryUnload(ItemType dish, out string message)
        {
            if (!CanTouchLoad(out message)) return false;

            int count = CountOf(dish);
            if (count <= 0)
            {
                message = null;
                return false;
            }

            kitchen?.PutBack(dish);
            if (count == 1)
            {
                cargo.Remove(dish);
                loadOrder.Remove(dish);
            }
            else
            {
                cargo[dish] = count - 1;
            }

            OnChanged?.Invoke();
            message = null;
            return true;
        }

        /// <summary>Takes everything off the truck and puts it back on the pass.</summary>
        public void UnloadAll()
        {
            if (State != FoodTruckState.Parked || cargo.Count == 0) return;

            foreach (KeyValuePair<ItemType, int> entry in cargo)
                for (int i = 0; i < entry.Value; i++) kitchen?.PutBack(entry.Key);

            cargo.Clear();
            loadOrder.Clear();
            OnChanged?.Invoke();
        }

        bool CanTouchLoad(out string reason)
        {
            if (!IsUnlocked)
            {
                reason = "The food truck has not been found yet.";
                return false;
            }

            if (State == FoodTruckState.Out)
            {
                reason = "The truck is out on the road.";
                return false;
            }

            reason = null;
            return true;
        }

        // --- Trips ----------------------------------------------------------------------------------

        public bool CanSend(FoodTruckDestination place, out string reason)
        {
            if (!CanTouchLoad(out reason)) return false;

            if (place == null)
            {
                reason = "Pick somewhere to go on the map.";
                return false;
            }

            if (Loaded == 0)
            {
                reason = "Load some dishes first.";
                return false;
            }

            if (canSend != null && !canSend())
            {
                reason = "Too dangerous on the road after dark. Wait for morning.";
                return false;
            }

            reason = null;
            return true;
        }

        public bool TrySend(FoodTruckDestination place, out string message)
        {
            if (!CanSend(place, out message)) return false;

            Heading = place;
            takings = Estimate(place);
            secondsLeft = Mathf.Max(1f, place.tripSeconds);
            State = FoodTruckState.Out;

            OnSent?.Invoke(place);
            OnChanged?.Invoke();

            message = $"The truck is off to {place.displayName} with {Loaded} dishes.";
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (State != FoodTruckState.Out || deltaSeconds <= 0f) return;

            secondsLeft -= deltaSeconds;
            if (secondsLeft <= 0f) Return();
        }

        void Return()
        {
            FoodTruckDestination place = Heading;
            int sold = Loaded;
            int coins = takings;

            cargo.Clear();
            loadOrder.Clear();
            takings = 0;
            secondsLeft = 0f;
            State = FoodTruckState.Parked;

            if (coins > 0) pay?.Invoke(coins);
            if (sold > 0) addExperience?.Invoke(tuning.experiencePerTrip);

            OnReturned?.Invoke(place, coins, sold);
            Heading = null;
            OnChanged?.Invoke();
        }
    }
}
