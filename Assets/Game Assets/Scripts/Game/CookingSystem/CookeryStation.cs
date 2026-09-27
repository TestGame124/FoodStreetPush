using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CookeryStation : MonoBehaviour, IInteractableObject
{

    private InventorySystem inventorySystem;
    [SerializeField] CookingRecipe[] recipies;
    CookingUIPanel cookPanel;

    [SerializeField] List<Coroutine> recipiesCoroutines;

    [SerializeField]
    public List<CookRecipeData> recipiesInProgress;

    PlacedObjectTypeSO placedObjectData;

    public static Action OnStartCooking;

    [SerializeField] int MaxCookRecipies;
    public Transform centerPoint;

    /// <summary>
    /// What this station can cook.
    ///
    /// Exposed so the restaurant's menu can be built from the kitchen that exists rather than
    /// from a second list somebody has to keep in step with it. A dish the player can cook and
    /// a dish customers can order should never be two different answers.
    /// </summary>
    public CookingRecipe[] Recipes => recipies;

    private void Start()
    {
        foreach (CookingRecipe recipe in recipies)
        {
            OrderSystem.AddAvailableItems(recipe.Results.item);
        }

    }

    private void OnEnable()
    {
        if (LevelController.Instance)
            LevelController.Instance.zone.RegisterStation(this);
    }

    private void OnDisable()
    {
        foreach (CookingRecipe recipe in recipies)
        {
            OrderSystem.RemoveAvailableItem(recipe.Results.item);
        }


        if (LevelController.Instance)
            LevelController.Instance.zone.UnRegisterStation(this);
    }
    public void Initialize()
    {
        if (placedObjectData == null)
            placedObjectData = GetComponent<PlacedObject_Done>().PlacedObjectTypeSO;
        cookPanel.buildingNameText.text = placedObjectData.name;

       
    }

    public bool OnTouch()
    {
        UIGame ui = UIGame.GetUI();
        if (ui == null) return false;

        cookPanel = ui.cookingUIPanel;
        cookPanel.gameObject.SetActive(true);
        cookPanel.Setup(recipies,this);
        MaxCookRecipies = cookPanel.GetSlotsCount();

        Initialize();
        return true;
    }
    
    
    public bool StartCooking(CookingRecipe recipe, InventorySystem inventory)
    {
        if (recipiesInProgress.Count >= MaxCookRecipies)
        {
            return false;
        }
        CookRecipeData newRecipe = new CookRecipeData(this, recipe, inventory);

        if (recipiesInProgress.Count == 0)
            newRecipe.active = true;
        else
            newRecipe.active = false;

        recipiesInProgress.Add(newRecipe);
        OnStartCooking?.Invoke();

        StartCoroutine(newRecipe.StartCookingTimer(this));
        return true;

    }

  
    public void RemoveCookRecipeData(CookRecipeData recipeData)
    {
        recipiesInProgress.Remove(recipeData);
    }
}

