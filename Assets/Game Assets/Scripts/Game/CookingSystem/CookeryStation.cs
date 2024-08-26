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
    public CookStationData Save()
    {

        CookRecipeData[] recipiesData = new CookRecipeData[recipiesInProgress.Count];

        for (int i = 0; i < recipiesData.Length; i++)
        {
            recipiesData[i] = recipiesInProgress[i];
        }

        return new CookStationData(recipiesData);
    }

    public void Load(CookStationData data)
    {
        if (data == null)
        {
            data = Save();
        }


        for (int i = 0; i < data.recipiesInProgress.Length; i++)
        {
            recipiesInProgress.Add(data.recipiesInProgress[i]);
            StartCoroutine(data.recipiesInProgress[i].StartCookingTimer(this));
        }

    }
    #endregion

}
[System.Serializable]
public class CookStationData
{
    public CookRecipeData[] recipiesInProgress;

    public CookStationData(CookRecipeData[] recipiesInProgress)
    {
        this.recipiesInProgress = recipiesInProgress;
    }
}

