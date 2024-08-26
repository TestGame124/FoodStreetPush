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

    public IEnumerator StartCookingTimer(CookeryStation manager)
    {
        timer = totalTime;
        while (timer > 0)
        {
            UpdateTimer?.Invoke(timer, this);
            yield return new WaitForSeconds(1);
            timer--;
        }
        UpdateTimer?.Invoke(timer, this);

        for (int i = 0; i <= results.Amount; i++)
        {
            Vector3 offset = new Vector3(UnityEngine.Random.Range(-1, 1), 2, UnityEngine.Random.Range(-1, 1));

            Item newItem = new(ItemController.GetItem(results.item.type), results.item.itemsAmount);

            results.item.itemDataSO.GenerateItem(ItemController.instance.itemReceiver, newItem, manager.transform.position + offset, InventorySystem.instance);
        }
        manager.RemoveCookRecipeData(this);
    }

    public void FinishRecipe()
    {
        timer = 0;
    }
}