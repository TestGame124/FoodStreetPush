using FoodStreet.Navigation;
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

    [Header("UI")]
    [SerializeField]UIGame uiGame;

    public static bool IsDragging;
    private void Start()
    {
        instance = this;

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

        inventorySystem.Initialize();

        orderSystem.Initialize(inventorySystem);
        uiGame.Initialize();
    }


    public void ChangeGameState(GameState state, Action onStateChange = null)
    {
        currentState = state;
        if(gridBuildingSystem.gridVisualization != null){
        switch (currentState)
        {
            case GameState.EditState:
                gridBuildingSystem.gridVisualization.SetActive(true); 
                break;
            case GameState.PlayState:
                gridBuildingSystem.gridVisualization.SetActive(false);
                break;

        }
        }
        onStateChange?.Invoke();

    }
    public static GameHandler GetHandler() { return instance; }
}
