using System;
using LastOrders.Economy;
using LastOrders.Kitchen;
using UnityEngine;

namespace LastOrders.Discovery
{
    /// <summary>
    /// What the window and the truck need to know about a dish besides how many are cooked: what
    /// it is called, what it sells for, and whether the player can cook it yet.
    ///
    /// Three delegates rather than calls into the item database, so the rules can be tested in a
    /// scene with no database - where looking a dish up logs an error.
    /// </summary>
    public sealed class DishCatalogue
    {
        public Func<ItemType, int> Price = dish => 10;
        public Func<ItemType, string> Name = dish => TakeawayService.Spaced(dish.ToString());
        public Func<ItemType, bool> CanCook = dish => true;

        /// <summary>
        /// The game's own: the dining-room price and the recipe's level from the economy table, the
        /// name from the dish's data.
        /// </summary>
        public static DishCatalogue ForGame(Func<int> playerLevel) => new DishCatalogue
        {
            Price = dish =>
            {
                if (EconomyRules.TryDish(dish, out EconomyRules.Dish row)) return row.Coins;
                ItemDataSO data = SafeData(dish);
                return data != null && data.coinsReward > 0 ? data.coinsReward : 10;
            },
            Name = dish =>
            {
                ItemDataSO data = SafeData(dish);
                return data != null && !string.IsNullOrEmpty(data.Name) ? data.Name : TakeawayService.Spaced(dish.ToString());
            },
            CanCook = dish => !EconomyRules.TryDish(dish, out EconomyRules.Dish row) || playerLevel == null || row.Level <= playerLevel(),
        };

        /// <summary>The dish's data, or null - without the error the database logs for an item it has never heard of.</summary>
        public static ItemDataSO SafeData(ItemType dish) => ItemController.instance != null ? MealStock.DishData(dish) : null;

        /// <summary>The dish's picture for the popups, or null.</summary>
        public static Sprite Icon(ItemType dish)
        {
            ItemDataSO data = SafeData(dish);
            return data != null ? data.itemImage : null;
        }
    }
}
