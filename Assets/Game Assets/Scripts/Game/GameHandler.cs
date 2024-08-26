using System;
using System.Collections;
using System.Collections.Generic;
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
    public const string FirstTimeData = "FirstTime";

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

        FirstTime = PlayerPrefs.GetInt(FirstTimeData, 0) == 0;
        if (FirstTime)
        {
            PlayerPrefs.SetInt(FirstTimeData, 1);
        }


        TutorialController.Initialize();

        currenciesController.Initialise();
        levelUpSystem.Initialize();
        gridBuildingSystem.Initialize();
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
