using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CookeryStation : MonoBehaviour, IInteractableObject
{

    private InventorySystem inventorySystem;
    [SerializeField] CookingRecipe[] recipies = Array.Empty<CookingRecipe>();
    CookingUIPanel cookPanel;

    [SerializeField] List<Coroutine> recipiesCoroutines;

    [SerializeField]
    public List<CookRecipeData> recipiesInProgress = new List<CookRecipeData>();

    PlacedObjectTypeSO placedObjectData;

    public static Action OnStartCooking;

    [SerializeField, Min(1)] int MaxCookRecipies = 3;
    public Transform centerPoint;

    [Tooltip("What the cooking panel calls this station when it has no placed-object type to name it - a station set down by hand in the level rather than bought.")]
    [SerializeField] string displayName;

    /// <summary>
    /// What this station can cook.
    ///
    /// Exposed so the restaurant's menu can be built from the kitchen that exists rather than
    /// from a second list somebody has to keep in step with it. A dish the player can cook and
    /// a dish customers can order should never be two different answers.
    /// </summary>
    public CookingRecipe[] Recipes => recipies;

    /// <summary>Raised when a station's cooking speed changes - somebody came to help, or left.</summary>
    public static Action<CookeryStation> OnBoostChanged;

    // Who is speeding this station up, and by how much: 0.15 is 15% faster.
    readonly Dictionary<object, float> boosts = new Dictionary<object, float>();

    /// <summary>How fast this station cooks: 1, plus every boost on it. A cook's seconds pass this much quicker.</summary>
    public float CookSpeed
    {
        get
        {
            float speed = 1f;
            foreach (float boost in boosts.Values) speed += boost;
            return Mathf.Max(0.1f, speed);
        }
    }

    public bool IsBoosted => boosts.Count > 0;

    /// <summary>Speeds the station up by <paramref name="amount"/> (0.15 = 15% faster) for as long as
    /// <paramref name="source"/> keeps it - a helper standing at it. Setting it again replaces it.</summary>
    public void AddBoost(object source, float amount)
    {
        if (source == null) return;

        boosts[source] = Mathf.Max(0f, amount);
        OnBoostChanged?.Invoke(this);
    }

    public void RemoveBoost(object source)
    {
        if (source != null && boosts.Remove(source)) OnBoostChanged?.Invoke(this);
    }

    private void Start()
    {
        foreach (CookingRecipe recipe in recipies)
        {
            OrderSystem.AddAvailableItems(recipe.Results.item);

            // What this station makes is a meal: it goes out on the pass and customers can be served it.
            if (recipe != null && recipe.Results.item != null)
                LastOrders.Kitchen.MealStock.RegisterCooked(recipe.Results.item.type, recipe.Results.item.itemDataSO);
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
        {
            var placed = GetComponent<PlacedObject_Done>();
            if (placed != null) placedObjectData = placed.PlacedObjectTypeSO;
        }

        // A station placed by hand has no type, and reading its name threw before the tap was done.
        string title = placedObjectData != null ? placedObjectData.name : displayName;
        if (!string.IsNullOrEmpty(title)) cookPanel.buildingNameText.text = title;

       
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
        if (recipe == null || Array.IndexOf(recipies, recipe) < 0 ||
            recipiesInProgress.Count >= Mathf.Max(1, MaxCookRecipies) ||
            LastOrders.Consequence.OutdoorWreck.IsWreckedOn(this) || !recipe.TryCook(inventory))
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
        if (recipiesInProgress.Count > 0) recipiesInProgress[0].active = true;
        OnStartCooking?.Invoke();
    }
}

