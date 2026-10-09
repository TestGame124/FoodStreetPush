using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIGame : MonoBehaviour
{
    private static UIGame instance;
    [SerializeField] GameObject scriptHolder;
    [SerializeField] ObjectDatabaseHandler objectDatabaseHandler;

    [Header("Shop Item UI")]
    [SerializeField] public UiItemsWindow uiItemsWindow;
    [SerializeField] public Transform shopBtn;

    [Header("Item Confirmation Popup")]

    [SerializeField] public ConfirmationUiPanel confirmationPopup;
    [SerializeField] public ConfirmationUiPanel placeObjectPanel;


    [Header("Planting System")]
    [SerializeField] public SeedsInventoryUI seedInventory;

    [Header("Inventory System")]
    [SerializeField] public InventoryUIBehaviour inventoryUIBehaviour;

    [Header("Cooking UI")]
    [SerializeField] public CookingUIPanel cookingUIPanel;
    [SerializeField] public GameObject notEnoughIng;

    [Header("Orders UI")]
    [SerializeField] public OrdersUiBehaviour ordersUIBehaviour;
    [SerializeField] public GameObject orderButton;
    [SerializeField] public Text ordersNeededText;
    [SerializeField] public GameObject orderCloseBtton;
    [Space]
    [SerializeField] public GameObject notEnoughIngPanel;
   

    [Header("Guide Panel")]
    [SerializeField] public GameObject guidePanel;
   
    [Header("Level UP UI")]
    [SerializeField] public LevelUpUIPanel levelUpUi;

    [Header("Sign Board UI Panel")]
    public SignBoardUIPanel signBoardUI;

    [Header("In APP Store")]
    [SerializeField] GameObject storeUI;


    private void Awake()
    {
        instance = this;
    }
    public void Initialize()
    {

        //instance = this;

        InventorySystem inventSystem = scriptHolder.GetComponent<InventorySystem>();
        inventoryUIBehaviour.Initialise(inventSystem);
        seedInventory.Initialise();
        cookingUIPanel.Initialize(inventSystem);

        CurrenciesController.AddCurrency(CurrencyType.Coins, 0);
        CurrenciesController.AddCurrency(CurrencyType.Gems, 0);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            LevelUpSystem.AddXP(2);
        }
        if (Input.GetKeyDown(KeyCode.C)){
        CurrenciesController.AddCurrency(CurrencyType.Coins, 1000);
        CurrenciesController.AddCurrency(CurrencyType.Gems, 2);
        }
    }
    /// <summary>
    /// Kept as the name the UI code already calls, but the answer now comes from PointerInput so
    /// there is one graphic raycast per frame for the whole game instead of a fresh
    /// PointerEventData and result list per caller per call.
    /// </summary>
    public static bool IsPointerOverUI() => FoodStreet.Interaction.PointerInput.OverUI;

    public static Vector3 GetScreenPostion(Vector3 pos)
    {
        return Camera.main.WorldToScreenPoint(pos);

    }

    public void StoreButton()
    {
        if (storeUI.activeInHierarchy)
            return;
        storeUI.GetComponent<CanvasGroup>().DOFade(1, 0.2f).From(0);
       
        for (int i = 0; i < storeUI.transform.GetChild(0).childCount; i++)
        {
            storeUI.transform.GetChild(0).GetChild(i).DOLocalMoveY(0, 0.5f).SetDelay(i * 0.15f).From(1000).SetEase(Ease.OutBack);
        }
        storeUI.gameObject.SetActive(true);
    }

    public void ShopButton()
    {
        GameHandler.GetHandler().ChangeGameState(GameHandler.GameState.EditState);
        uiItemsWindow.ActiveState(true);
    }
    public void ExitEditMode()
    {
        GameHandler.GetHandler().ChangeGameState(GameHandler.GameState.PlayState);
        uiItemsWindow.ActiveState(false);
    }

    public void OrderButtonClick()
    {
        ordersUIBehaviour.gameObject.SetActive(true);
        ordersUIBehaviour.CanvasGroupComp.DOFade(1, 0.2f).From(0);
    }

   
    public static UIGame GetUI()
    {
        return instance;
    }
}
