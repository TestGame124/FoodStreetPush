using System;
using System.Collections;
using System.Collections.Generic;
using FoodStreet.Saving;
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

    public void OnTouch()
    {
        cookPanel = UIGame.GetUI().cookingUIPanel;
        cookPanel.gameObject.SetActive(true);
        cookPanel.Setup(recipies,this);
        MaxCookRecipies = cookPanel.GetSlotsCount();

        if (LevelTutorialBehaviour.numOfTimesEventWillRun < 1)
        {
            if (!TutorialController.TutorialCompleted())
                TutorialController.OnTappingFirstCookingStation?.Invoke();
        }
        Initialize();
    }
    
    
    public bool StartCooking(CookingRecipe recipe, InventorySystem inventory)
    {
        if (recipiesInProgress.Count >= MaxCookRecipies)
        {
            return false;
        }
        CookRecipeData newRecipe = new CookRecipeData(this, recipe, inventory);
        
        if(!TutorialController.TutorialCompleted())
            TutorialController.OnCookButtonClick?.Invoke();

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

    #region Saving Loading

    private PlacedObject_Done owner;

    /// <summary>
    /// The ID of the placed object this station is part of. Cooks are keyed by
    /// it, so a station that is built or sold cannot shuffle everybody else's
    /// pans onto the wrong stove.
    /// </summary>
    public int OwnerId
    {
        get
        {
            if (owner == null)
                owner = GetComponentInParent<PlacedObject_Done>();

            return owner != null ? owner.ID : 0;
        }
    }

    public CookStationSaveData Save()
    {
        CookStationSaveData data = new CookStationSaveData { ownerId = OwnerId };

        for (int i = 0; i < recipiesInProgress.Count; i++)
        {
            CookRecipeData recipe = recipiesInProgress[i];
            if (recipe == null)
                continue;

            data.recipes.Add(new CookRecipeSaveData
            {
                // The result item identifies the recipe. The old format stored
                // the sprite's asset name, which broke the moment art was
                // renamed.
                resultType = recipe.results.item.type,
                remainingSeconds = recipe.timer,
                totalSeconds = recipe.totalTime,
                active = recipe.active
            });
        }

        return data;
    }

    public void Load(CookStationSaveData data)
    {
        if (data == null)
            return;

        for (int i = 0; i < data.recipes.Count; i++)
        {
            CookRecipeSaveData saved = data.recipes[i];

            CookingRecipe recipe = FindRecipe(saved.resultType);
            if (recipe == null)
            {
                // The station no longer offers what was cooking - a recipe was
                // removed or moved since this save. Drop it rather than crash.
                Debug.LogWarning($"[CookeryStation] Dropping saved cook for '{saved.resultType}': this station no longer makes it.");
                continue;
            }

            CookRecipeData restored = new CookRecipeData(this, recipe, InventorySystem.instance)
            {
                timer = Mathf.Clamp(saved.remainingSeconds, 0, saved.totalSeconds),
                active = saved.active
            };

            recipiesInProgress.Add(restored);
            StartCoroutine(restored.StartCookingTimer(this, true));
        }
    }

    private CookingRecipe FindRecipe(ItemType resultType)
    {
        for (int i = 0; i < recipies.Length; i++)
        {
            if (recipies[i] != null && recipies[i].Results.item != null && recipies[i].Results.item.type == resultType)
                return recipies[i];
        }
        return null;
    }

    #endregion

}

