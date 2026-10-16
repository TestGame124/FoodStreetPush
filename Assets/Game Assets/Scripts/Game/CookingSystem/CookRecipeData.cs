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
    public int completionExperience;

    [NonSerialized]
    public CookeryStation manager;
    [NonSerialized]
    public InventorySystem inventory;

    public Action<float, CookRecipeData> UpdateTimer;

    public CookRecipeData(CookeryStation manager, CookingRecipe recipe, InventorySystem inventory)
    {
        this.managerName = manager.name;
        this.position = manager.transform.position;
        this.recipeImageName = recipe.RecipeImage != null ? recipe.RecipeImage.name : string.Empty;
        this.totalTime = recipe.TotalTimeToCook;
        this.results = recipe.Results;
        this.inventoryName = inventory.name;
        this.manager = manager;
        this.inventory = inventory;
        this.completionExperience = Mathf.Max(1, recipe.startRewards);
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
            while (!active || LastOrders.Consequence.OutdoorWreck.IsWreckedOn(manager)) yield return null;
            UpdateTimer?.Invoke(timer, this);
            yield return new WaitForSeconds(1);
            if (active && !LastOrders.Consequence.OutdoorWreck.IsWreckedOn(manager)) timer--;
        }
        UpdateTimer?.Invoke(timer, this);

        int servings = 0;

        for (int i = 0; i < Mathf.Max(1, results.Amount); i++)
        {
            while (!inventory.CanAdd(results.item.type)) yield return null;
            Vector3 offset = new Vector3(UnityEngine.Random.Range(-1, 1), 2, UnityEngine.Random.Range(-1, 1));

            Item newItem = new(ItemController.GetItem(results.item.type), 1);

            // A dish goes out onto the serving pass for the waiters, its plate hopping from the
            // station to the counter, rather than flying off to the storehouse as an icon. Into the
            // inventory either way: the pass is a view of it.
            if (LastOrders.Kitchen.ServingPassBoard.Receives(newItem.type))
            {
                LastOrders.Kitchen.ServingPassBoard.Instance.Incoming(newItem.type, PassOrigin(manager));
                InventorySystem.instance.AddItem(newItem);
            }
            else
            {
                results.item.itemDataSO.GenerateItem(ItemController.instance.itemReceiver, newItem, manager.transform.position + offset, InventorySystem.instance);
            }

            servings++;
        }

        // GDD 11.1: every dish cooked adds Heat, and grilled or smoked dishes add double.
        // Until the recipe assets carry that multiplier through this path, everything is
        // charged the base rate.
        //
        // The serving count is what reaches the customer system: each plate produced here is a
        // meal on the pass, and the pass is what travellers on the road are deciding about.
        LastOrders.Core.LastOrdersBridge.NotifyDishCooked(servings: servings);
        LastOrders.Core.LastOrdersBridge.AddExperience(completionExperience);

        manager.RemoveCookRecipeData(this);
    }

    public void FinishRecipe()
    {
        timer = 0;
    }

    /// <summary>Where a finished dish leaves the station: its front, where the cook works, at counter height.</summary>
    static Vector3 PassOrigin(CookeryStation station)
    {
        Vector3 front = station.centerPoint != null ? station.centerPoint.position : station.transform.position;
        front.y = station.transform.position.y + 0.95f;
        return front;
    }
}
