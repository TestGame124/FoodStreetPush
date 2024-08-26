using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Recipe", menuName = "Cooking/Recipe")]
public class CookingRecipe : ScriptableObject
{
    public Sprite RecipeImage;
    public int TotalTimeToCook = 30;


    public List<ItemAmount> Ingrediants;
    public ItemAmount Results;

   
    public int startRewards;

    public static Action OnStartCooking;
    public bool CanCook(InventorySystem inventory)
    {
       foreach(ItemAmount itemAmount in Ingrediants)
        {
            if(inventory.ItemCount(itemAmount.item.type) < itemAmount.Amount || !inventory.ContainsItem(itemAmount.item.type))
            {
                return false;
            }
        }
       return true;
    }

    public void Cook(InventorySystem inventory)
    {
        if(CanCook(inventory))
        {
            foreach(ItemAmount itemAmount in Ingrediants) {
            
                for(int i = 0; i < itemAmount.Amount; i++)
                {
                    inventory.RemoveItem(itemAmount.item);
                }
            

            }
            
            OnStartCooking?.Invoke();
        }
    }
}

[Serializable]
public struct ItemAmount
{
    public Item item;

    [Range(1,99)]
    public int Amount;
}
