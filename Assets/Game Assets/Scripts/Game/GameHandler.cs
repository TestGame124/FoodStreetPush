using FoodStreet.Navigation;
using FoodStreet.Saving;
using System;
using UnityEngine;

public class GameHandler : MonoBehaviour
{
    private static GameHandler instance;
    public enum GameState
    {
        EditState,
        PlayState
    }

    public static GameState currentState = GameState.PlayState;

    public static bool FirstTime;

    [Space]

    [SerializeField] ObjectDatabaseHandler objectDatabaseHandler;
    [SerializeField] InventorySystem inventorySystem;
    [SerializeField] ItemController itemController;
    [SerializeField] ShopItemDataContainer shopItemDataContainer;
    [SerializeField] CurrenciesController currenciesController;
    [SerializeField] LevelUpSystem levelUpSystem;
    [SerializeField] OrderSystem orderSystem;

    [SerializeField] LevelController levelController;

    [Space]
    [SerializeField]GridBuildingSystem3D gridBuildingSystem;

    [Space]
    [SerializeField] LevelTutorialBehaviour levelTutorialBehaviour;
    
    [Header("UI")]
    [SerializeField]UIGame uiGame;

    public static bool IsDragging;
    private void Start()
    {
        instance = this;

        // Before anything else reads or writes progress: this loads, verifies and
        // decrypts the save file, and takes over any data the old save system
        // left behind.
        SaveSystem.Initialize();

        // Must run before anything places a runtime object, or fresh objects
        // reuse IDs handed out in a previous session.
        SaveController.InitializeIds();

        FirstTime = !SaveSystem.Data.profile.hasPlayedBefore;
        if (FirstTime)
        {
            SaveSystem.Data.profile.hasPlayedBefore = true;
            SaveSystem.MarkDirty();
        }


        TutorialController.Initialize();

        currenciesController.Initialise();
        levelUpSystem.Initialize();
        gridBuildingSystem.Initialize();

        // Straight after the build grid and before anything is placed on it: the nav grid
        // mirrors the build grid's dimensions, and it listens for placements from here on.
        NavigationSystem.Bootstrap();

        objectDatabaseHandler.Initialize();
        itemController.Initialize();
        shopItemDataContainer.Initialize(objectDatabaseHandler.ObjectsDatabases, uiGame.uiItemsWindow);

        levelController.Initialize();
        gridBuildingSystem.LoadPlacedObjects();
        levelController.LoadData(gridBuildingSystem.isLoadingData);

        inventorySystem.Initialize();



        //Loading Data


        orderSystem.Initialize(inventorySystem);
        uiGame.Initialize();


        levelTutorialBehaviour.Initialize();
        
    }


    public void ChangeGameState(GameState state, Action onStateChange = null)
    {
        currentState = state;
        switch (currentState)
        {
            case GameState.EditState:
                gridBuildingSystem.gridVisualization.SetActive(true); 
                break;
            case GameState.PlayState:
                gridBuildingSystem.gridVisualization.SetActive(false);
                break;

        }
        onStateChange?.Invoke();

    }
    public static GameHandler GetHandler() { return instance; }
}
