using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.Kitchen
{
    /// <summary>
    /// Prepared meals, per dish, and who has claimed one.
    ///
    /// Backed by the level's own inventory wherever there is one. That is deliberate and it is
    /// the important decision in this file: cooked dishes are already items the player owns,
    /// and keeping a second private tally beside them would mean two numbers that disagree the
    /// first time anything else touches the inventory. A level with no inventory - a grey-box
    /// scene with a pot and nothing else - gets an internal count instead, so the same customer
    /// code runs in both.
    ///
    /// Reservation is what makes it correct with more than one customer. A visitor who orders
    /// the last bowl of soup holds it from the moment they order until they eat it; without
    /// that, two people order the same bowl, one of them is served, and the other waits at a
    /// table forever for food that was never theirs.
    /// </summary>
    public sealed class MealStock
    {
        /// <summary>
        /// Which item types are MEALS rather than ingredients.
        ///
        /// The player's inventory holds both, and without this the pass would happily serve a
        /// customer a raw tomato - the one thing a restaurant must not do. The project already
        /// answers this question in its item database, so this reads that rather than guessing
        /// from the enum's numbering, which is a convention rather than a rule.
        ///
        /// Static because the answer is the same for every pass in the process and the
        /// database does not change at runtime.
        /// </summary>
        static Dictionary<ItemType, ItemDataSO> dishIndex;

        static bool warnedAboutMissingDatabase;

        readonly Dictionary<ItemType, int> reserved = new Dictionary<ItemType, int>();

        /// <summary>Counts for a level with no InventorySystem to read.</summary>
        readonly Dictionary<ItemType, int> standalone = new Dictionary<ItemType, int>();

        InventorySystem inventory;

        /// <summary>Total ever cooked. Drives the one-off beats - the first dish is what makes
        /// opening the restaurant possible - so it must not fall when one is eaten.</summary>
        public int TotalCooked { get; private set; }

        /// <summary>Raised on any change to what is on the pass or what is spoken for.</summary>
        public event Action OnPassChanged;

        /// <summary>Raised the first time anything is cooked at all.</summary>
        public event Action OnFirstMealCooked;

        /// <summary>
        /// The level's inventory.
        ///
        /// Looked up lazily rather than injected: this service is built at composition time,
        /// well before the legacy bootstrap has created the inventory it is going to read.
        ///
        /// Retried every time rather than latched on the first miss. A single failed lookup
        /// used to mean the pass fell back to its private counters for the rest of the session
        /// - and since the level's kitchen fills the inventory rather than those counters, the
        /// result was a restaurant where every dish read as zero, every customer waited for
        /// food that was already cooked, and nobody could work out why. Reading a static field
        /// costs nothing, so there is nothing to cache against.
        /// </summary>
        InventorySystem Inventory
        {
            get
            {
                if (inventory == null) inventory = InventorySystem.instance;
                return inventory;
            }
        }

        /// <summary>
        /// The dish's data: its name, its icon for the bubble, and what it is worth.
        ///
        /// Looked up in the recipe list first. ItemController only registers AllItems, so a
        /// cooked dish that lives in AllRecipies would log an error and come back null through
        /// the usual path - which is how a served meal ends up nameless and free.
        /// </summary>
        public static ItemDataSO DishData(ItemType dish)
        {
            BuildDishIndex();

            if (dishIndex != null && dishIndex.TryGetValue(dish, out ItemDataSO data)) return data;

            return ItemController.GetItem(dish);
        }

        /// <summary>True when this item is something a customer can be served.</summary>
        public static bool IsDish(ItemType type)
        {
            BuildDishIndex();

            // Permissive when the database is missing: a level with no item database is a
            // grey-box whose pass only ever holds what its own stove put there, and refusing
            // everything would leave every customer waiting forever.
            return dishIndex == null || dishIndex.ContainsKey(type);
        }

        static void BuildDishIndex()
        {
            if (dishIndex != null) return;

            ItemsDatabase database = ItemController.instance != null
                ? ItemController.instance.itemsDatabase
                : null;

            if (database == null || database.AllRecipies == null)
            {
                if (!warnedAboutMissingDatabase)
                {
                    warnedAboutMissingDatabase = true;
                    Debug.LogWarning("[MealStock] No item database yet, so anything in the " +
                                     "inventory counts as a meal. Ingredients may be served.");
                }

                // Not latched: the database arrives with the level bootstrap, which may not
                // have run yet.
                return;
            }

            dishIndex = new Dictionary<ItemType, ItemDataSO>();

            for (int i = 0; i < database.AllRecipies.Length; i++)
            {
                ItemDataSO data = database.AllRecipies[i];
                if (data != null) dishIndex[data.Type] = data;
            }
        }

        /// <summary>How many of this dish are cooked and not already spoken for.</summary>
        public int Ready(ItemType dish) => Mathf.Max(0, Cooked(dish) - Held(dish));

        /// <summary>Total cooked of this dish, reserved or not.</summary>
        public int Cooked(ItemType dish)
        {
            InventorySystem store = Inventory;
            if (store != null) return store.ItemCount(dish);

            return standalone.TryGetValue(dish, out int count) ? count : 0;
        }

        /// <summary>Everything on the pass that nobody has claimed, across all dishes. What
        /// the counter draws and the HUD counts.</summary>
        public int TotalReady
        {
            get
            {
                int total = 0;

                InventorySystem store = Inventory;
                if (store != null)
                {
                    List<Item> items = store.GetItemList();
                    for (int i = 0; i < items.Count; i++)
                        if (items[i] != null && IsDish(items[i].type)) total += Ready(items[i].type);

                    return total;
                }

                foreach (KeyValuePair<ItemType, int> pair in standalone) total += Ready(pair.Key);
                return total;
            }
        }

        /// <summary>Anything at all on the pass, of any dish. What the door checks.</summary>
        public bool HasAnyReady
        {
            get
            {
                InventorySystem store = Inventory;
                if (store != null)
                {
                    List<Item> items = store.GetItemList();
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (items[i] == null || !IsDish(items[i].type)) continue;
                        if (Ready(items[i].type) > 0) return true;
                    }

                    return false;
                }

                foreach (KeyValuePair<ItemType, int> pair in standalone)
                    if (Ready(pair.Key) > 0) return true;

                return false;
            }
        }

        /// <summary>
        /// Claims whatever is ready, for a customer who asked for "a meal" rather than for a
        /// dish by name.
        ///
        /// This is the ordering model the prototype uses: there is no menu, so a visitor wants
        /// feeding and the kitchen decides what that means. Which dish they got still matters
        /// afterwards - it is what they pay for - so it is reported back rather than discarded.
        /// </summary>
        public bool TryReserveAny(out ItemType dish)
        {
            dish = default;

            InventorySystem store = Inventory;
            if (store != null)
            {
                List<Item> items = store.GetItemList();
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] == null || !IsDish(items[i].type)) continue;
                    if (Ready(items[i].type) <= 0) continue;

                    dish = items[i].type;
                    return TryReserve(dish);
                }

                return false;
            }

            foreach (KeyValuePair<ItemType, int> pair in standalone)
            {
                if (Ready(pair.Key) <= 0) continue;

                dish = pair.Key;
                return TryReserve(dish);
            }

            return false;
        }

        /// <summary>
        /// Claims one of this dish for a customer. Returns false when there is none free,
        /// which is the honest answer - the caller then waits rather than being handed a
        /// bowl that does not exist.
        /// </summary>
        public bool TryReserve(ItemType dish)
        {
            if (Ready(dish) <= 0) return false;

            reserved[dish] = Held(dish) + 1;
            OnPassChanged?.Invoke();
            return true;
        }

        /// <summary>Gives a claim back without eating it. A customer who leaves before being
        /// served must not take the dish with them.</summary>
        public void Release(ItemType dish)
        {
            int held = Held(dish);
            if (held <= 0) return;

            reserved[dish] = held - 1;
            OnPassChanged?.Invoke();
        }

        /// <summary>
        /// Eats a reserved dish: drops the claim and takes the item off the pass.
        ///
        /// Both halves in one call, because doing them separately is how a dish gets eaten
        /// twice or vanishes while still reserved.
        /// </summary>
        public void Consume(ItemType dish)
        {
            Release(dish);

            InventorySystem store = Inventory;
            if (store != null)
            {
                Item item = store.GetItem(dish);
                if (item != null) store.RemoveItem(item);
            }
            else if (standalone.TryGetValue(dish, out int count) && count > 0)
            {
                standalone[dish] = count - 1;
            }

            OnPassChanged?.Invoke();
        }

        /// <summary>
        /// Puts a cooked dish on the pass.
        ///
        /// A level whose kitchen already adds the item to the inventory does not call this for
        /// the stock - the item is there the moment it is cooked. It calls
        /// <see cref="NoteCooked"/> instead, for the counters the one-off beats read.
        /// </summary>
        public void Add(ItemType dish, int count = 1)
        {
            if (count <= 0) return;

            if (Inventory == null)
            {
                standalone.TryGetValue(dish, out int existing);
                standalone[dish] = existing + count;
            }

            NoteCooked(count);
            OnPassChanged?.Invoke();
        }

        /// <summary>Records that dishes were cooked, without claiming to have stored them.</summary>
        public void NoteCooked(int count)
        {
            if (count <= 0) return;

            bool wasFirst = TotalCooked == 0;
            TotalCooked += count;

            OnPassChanged?.Invoke();
            if (wasFirst) OnFirstMealCooked?.Invoke();
        }

        /// <summary>
        /// Drops every outstanding claim.
        ///
        /// Run when the dining room empties. The food itself is left alone: it belongs to the
        /// player and it is still in their inventory in the morning. Only the claims are a
        /// per-service thing, and a claim held by a customer who is no longer in the building
        /// is a dish nobody can ever order again.
        /// </summary>
        public void ReleaseAllClaims()
        {
            if (reserved.Count == 0) return;

            reserved.Clear();
            OnPassChanged?.Invoke();
        }

        int Held(ItemType dish) => reserved.TryGetValue(dish, out int count) ? count : 0;
    }
}
