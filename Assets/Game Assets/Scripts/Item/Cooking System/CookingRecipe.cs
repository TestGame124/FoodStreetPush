using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Recipe", menuName = "Cooking/Recipe")]
public class CookingRecipe : ScriptableObject
{
    public Sprite RecipeImage;
    public int TotalTimeToCook = 30;
    [Min(1)] public int requiredLevel = 1;


    public List<ItemAmount> Ingrediants;
    public ItemAmount Results;

   
    public int startRewards;

    public static Action OnStartCooking;
    public bool CanCook(InventorySystem inventory)
    {
        return BlockReason(inventory) == null;
    }

    Dictionary<ItemType, int> IngredientsByType()
    {
        var required = new Dictionary<ItemType, int>();
        if (Ingrediants == null) return required;
        foreach (ItemAmount entry in Ingrediants)
        {
            if (entry.item == null || entry.Amount <= 0) continue;
            required.TryGetValue(entry.item.type, out int amount);
            required[entry.item.type] = amount + entry.Amount;
        }
        return required;
    }

    public int SupplyCost(InventorySystem inventory)
    {
        if (inventory == null) return 0;
        int cost = 0;
        foreach (var entry in IngredientsByType())
            cost += Mathf.Max(0, entry.Value - inventory.ItemCount(entry.Key)) * LastOrders.Economy.EconomyRules.SupplyPrice(entry.Key);
        return cost;
    }

    public string BlockReason(InventorySystem inventory)
    {
        if (LevelUpSystem.GetLevel() < requiredLevel) return $"Requires level {requiredLevel}.";
        if (inventory == null || Results.item == null || Results.Amount < 1) return "Recipe is not ready.";
        if (!inventory.CanAdd(Results.item.type)) return "Make room in the storehouse first.";
        foreach (var entry in IngredientsByType())
        {
            ItemDataSO data = Ingrediants.Find(ingredient => ingredient.item != null && ingredient.item.type == entry.Key).item.itemDataSO;
            int reserve = data != null && data.canBePlanted ? 1 : 0;
            if (inventory.ItemCount(entry.Key) >= entry.Value + reserve) continue;
            if (LastOrders.Economy.EconomyRules.SupplyPrice(entry.Key) > 0) continue;
            return reserve > 0 ? $"Grow more {data.Name}; keep one seed for replanting." : $"Need {entry.Value} {entry.Key}.";
        }
        int cost = SupplyCost(inventory);
        if (!CurrenciesController.HasCurrency(CurrencyType.Coins, cost)) return $"Need {cost} Coins for missing ingredients.";
        return null;
    }

    /// <summary>Pay once when queued. Purchased ingredients go directly into this batch.</summary>
    public bool TryCook(InventorySystem inventory)
    {
        if (!CanCook(inventory)) return false;
        int cost = SupplyCost(inventory);
        if (!CurrenciesController.SubtractCurrency(CurrencyType.Coins, cost)) return false;
        foreach (var entry in IngredientsByType())
            inventory.TryConsume(entry.Key, Mathf.Min(entry.Value, inventory.ItemCount(entry.Key)));
        OnStartCooking?.Invoke();
        return true;
    }

    public void Cook(InventorySystem inventory) => TryCook(inventory);
}

[Serializable]
public struct ItemAmount
{
    public Item item;

    [Range(1,99)]
    public int Amount;
}
