using JetBrains.Annotations;
using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class CookRecipeData
{
    public string managerName;
    public Vector3 position;
    public string recipeImageName;
    public float timer;
    public ItemAmount results;
    public string inventoryName;
    public int totalTime;
    public bool active;

    [NonSerialized]
    public CookeryStation manager;
    [NonSerialized]
    public InventorySystem inventory;

    public Action<float, CookRecipeData> UpdateTimer;

    public CookRecipeData(CookeryStation manager, CookingRecipe recipe, InventorySystem inventory)
    {
        this.managerName = manager.name;
        this.position = manager.transform.position;
        this.recipeImageName = recipe.RecipeImage.name;
        this.totalTime = recipe.TotalTimeToCook;
        this.results = recipe.Results;
        this.inventoryName = inventory.name;
        this.manager = manager;
        this.inventory = inventory;
    }

    /// <summary>
    /// Runs the cook down to zero and hands over the results.
    /// </summary>
    /// <param name="resume">
    /// True when the cook is being restored from a save. The timer then keeps
    /// the value it was loaded with instead of restarting at the full duration -
    /// reloading used to hand the player back a full-length cook.
    /// </param>
    public IEnumerator StartCookingTimer(CookeryStation manager, bool resume = false)
    {
        if (!resume)
            timer = totalTime;

        while (timer > 0)
        {
            UpdateTimer?.Invoke(timer, this);
            yield return new WaitForSeconds(1);
            timer--;
        }
        UpdateTimer?.Invoke(timer, this);

        int servings = 0;

        for (int i = 0; i <= results.Amount; i++)
        {
            Vector3 offset = new Vector3(UnityEngine.Random.Range(-1, 1), 2, UnityEngine.Random.Range(-1, 1));

            Item newItem = new(ItemController.GetItem(results.item.type), results.item.itemsAmount);

            results.item.itemDataSO.GenerateItem(ItemController.instance.itemReceiver, newItem, manager.transform.position + offset, InventorySystem.instance);

            servings++;
        }

        // GDD 11.1: every dish cooked adds Heat, and grilled or smoked dishes add double.
        // Until the recipe assets carry that multiplier through this path, everything is
        // charged the base rate.
        //
        // The serving count is what reaches the customer system: each plate produced here is a
        // meal on the pass, and the pass is what travellers on the road are deciding about.
        LastOrders.Core.LastOrdersBridge.NotifyDishCooked(servings: servings);

        manager.RemoveCookRecipeData(this);
    }

    public void FinishRecipe()
    {
        timer = 0;
    }
}