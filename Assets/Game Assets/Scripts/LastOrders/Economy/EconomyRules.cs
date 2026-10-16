using UnityEngine;

namespace LastOrders.Economy
{
    /// <summary>Shared progression and production targets. Editor setup writes these into content assets.</summary>
    public static class EconomyRules
    {
        public readonly struct Dish
        {
            public readonly ItemType Type;
            public readonly int Level, Seconds, Coins, Experience;
            public Dish(ItemType type, int level, int seconds, int coins, int experience)
            { Type = type; Level = level; Seconds = seconds; Coins = coins; Experience = experience; }
        }

        public static readonly Dish[] Dishes =
        {
            new Dish(ItemType.Bread, 1, 30, 12, 2),
            new Dish(ItemType.TomatoSoup, 1, 40, 14, 2),
            new Dish(ItemType.CornBread, 2, 50, 22, 3),
            new Dish(ItemType.GardenSalad, 2, 20, 14, 2),
            new Dish(ItemType.SweetcornSalad, 3, 30, 20, 3),
            new Dish(ItemType.CarrotSlaw, 3, 30, 18, 3),
            new Dish(ItemType.FriedEggs, 3, 35, 20, 3),
            new Dish(ItemType.HashBrowns, 3, 40, 18, 3),
            new Dish(ItemType.TomatoCheeseSalad, 4, 40, 24, 4),
            new Dish(ItemType.GrilledCorn, 4, 45, 24, 4),
            new Dish(ItemType.BeanSalad, 4, 45, 24, 4),
            new Dish(ItemType.CornChowder, 5, 60, 28, 4),
            new Dish(ItemType.FrenchToast, 5, 60, 30, 4),
            new Dish(ItemType.ChickenSalad, 5, 50, 26, 4),
            new Dish(ItemType.ChickenSkewers, 6, 75, 32, 5),
            new Dish(ItemType.CustardTart, 6, 75, 34, 5),
            new Dish(ItemType.GrilledFish, 7, 90, 38, 6),
        };

        public static bool TryDish(ItemType type, out Dish dish)
        {
            foreach (Dish entry in Dishes) if (entry.Type == type) { dish = entry; return true; }
            dish = default;
            return false;
        }

        public static void ApplyItem(ItemDataSO item)
        {
            if (item == null) return;
            int grow = CropSeconds(item.Type);
            if (grow > 0) { item.totalTimeInSeconds = grow; item.harvestYield = 3; }
            if (TryDish(item.Type, out Dish dish))
            { item.coinsReward = dish.Coins; item.starsReward = dish.Experience; item.totalTimeInSeconds = dish.Seconds; }
        }

        public static void ApplyRecipe(CookingRecipe recipe)
        {
            if (recipe == null || recipe.Results.item == null || !TryDish(recipe.Results.item.type, out Dish dish)) return;
            recipe.TotalTimeToCook = dish.Seconds;
            recipe.requiredLevel = dish.Level;
            recipe.startRewards = 2 + (dish.Level - 1) / 2;
            var result = recipe.Results;
            result.Amount = 2;
            result.item = new Item(result.item.itemDataSO, 1);
            recipe.Results = result;
        }

        public static int XpForLevel(int level)
        {
            long step = Mathf.Clamp(level, 1, 10000) - 1L;
            return (int)System.Math.Min(int.MaxValue, 20L + 15L * step + 5L * step * step);
        }

        public static int LevelReward(int level) => 20 + 10 * Mathf.Clamp(level, 1, 1000);
        public static int OrderKinds(int level) => Mathf.Clamp(1 + (level - 1) / 3, 1, 3);
        public static int OrderQuantity(int level) => level < 4 ? 2 : 3;

        // Only ingredients with no farm/pen production can be supplied while cooking.
        public static int SupplyPrice(ItemType type)
        {
            switch (type)
            {
                case ItemType.Lettuce: return 2;
                case ItemType.Carrot: case ItemType.Potato: case ItemType.Onion: return 3;
                case ItemType.Cheese: return 6;
                case ItemType.CannedBeans: return 5;
                case ItemType.Chicken: return 10;
                case ItemType.Fish: return 12;
                default: return 0;
            }
        }

        public static int CropSeconds(ItemType type)
        {
            switch (type)
            {
                case ItemType.Flour: return 30;
                case ItemType.Corn: return 45;
                case ItemType.Tomato: return 60;
                case ItemType.Cabbage: return 90;
                default: return 0;
            }
        }
    }
}
