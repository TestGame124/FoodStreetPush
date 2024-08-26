using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class CookingUIPanel : MonoBehaviour
{

    InventorySystem inventorySystem;
    [SerializeField] ItemSlotUI[] itemSlots;

    [SerializeField] List<TextMeshProUGUI> itemSlotsTimerList;

    [SerializeField] public Text buildingNameText;
    [Header("Recipe Area")]
    public Transform recipiesBoxContainer;
    [Space]
    public RecipeUiBox recipeBoxPrefab;
    public List<RecipeUiBox> recipeBoxesUI;
    private List<CookRecipeData> recipiesData = new();

    [SerializeField] public GameObject skipButton;
     public Button closeButton;

    CookeryStation cookBuilding;

    CanvasGroup canvasGroup;
    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }
    private void OnEnable()
    {
        CookeryStation.OnStartCooking += UpdateSlots;
        canvasGroup.DOFade(1, 0.2f).From(0);
    }
    private void OnDisable()
    {
        CookeryStation.OnStartCooking-= UpdateSlots;
        CloseButton();
    }
    public void Initialize(InventorySystem inventory)
    {
        inventorySystem= inventory;
        closeButton.onClick.AddListener(() => CloseButton(true));
    }

    public void Setup(CookingRecipe[] recipies, CookeryStation cookBuilding)
    {
        this.cookBuilding= cookBuilding;
        recipeBoxesUI.Clear();

        for (int i = 0; i< recipies.Length; i++)
        {
            RecipeUiBox recipeBox = Instantiate(recipeBoxPrefab, recipiesBoxContainer);
            recipeBox.Initialze(inventorySystem, recipies[i],cookBuilding);
            recipeBoxesUI.Add(recipeBox);
        }

        for (int i = 0; i < itemSlots.Length; i++)
        {
            ItemSlotUI slot = itemSlots[i];

            slot.previewImage.CrossFadeAlpha(0, 0, true);
            slot.amountText.alpha = 0;
            slot.transform.localPosition = Vector3.zero;
            slot.transform.localScale = Vector3.one * 3;
        }

        itemSlotsTimerList.Clear();
        for (int i = 0; i < cookBuilding.recipiesInProgress.Count; i++)
        {

            ItemSlotUI slot = itemSlots[i];
            slot.previewImage.sprite = ItemController.GetItem(cookBuilding.recipiesInProgress[i].results.item.type).itemImage;
            slot.transform.localPosition = Vector3.zero;
            slot.transform.localScale = Vector3.one * 3;
            slot.previewImage.CrossFadeAlpha(1, .1f, true);
            slot.amountText.alpha = 1;
            itemSlotsTimerList.Add(slot.amountText);
            cookBuilding.recipiesInProgress[i].UpdateTimer -= UpdateTimerUI;
            recipiesData.Add(cookBuilding.recipiesInProgress[i]);
            cookBuilding.recipiesInProgress[i].UpdateTimer += UpdateTimerUI;
        }

    }

    void UpdateTimerUI(float timer, CookRecipeData recipeData)
    {
        //for (int i = 0; i < cookBuilding.recipiesInProgress.Count; i++)
        //{
        if (itemSlotsTimerList.Count == 0)
            return;


        Debug.Log($"Recipe Data Name : {recipeData.managerName}");
        int recipeIndex = cookBuilding.recipiesInProgress.IndexOf(recipeData);
        if (timer <= 0)
        {
            for (int i = 0; i < itemSlots.Length; i++)
            {
                itemSlots[i].previewImage.CrossFadeAlpha(0, 0, true);
                itemSlots[i].amountText.alpha = 0;
            }
            return;
        }
        itemSlots[recipeIndex].previewImage.CrossFadeAlpha(1, .05f, true);
        itemSlots[recipeIndex].amountText.alpha = 1;
        itemSlotsTimerList[recipeIndex].text = timer.ToString();
        //}
    }
    
    public int GetSlotsCount()
    {
        return itemSlots.Length;
    } 
    public ItemSlotUI[] GetSlots()
    {
        return itemSlots;
    }

    void UpdateSlots()
    {
        itemSlotsTimerList.Clear();
        for (int i = 0; i < cookBuilding.recipiesInProgress.Count; i++)
        {
            ItemSlotUI slot = itemSlots[i];
            slot.previewImage.sprite = ItemController.GetItem(cookBuilding.recipiesInProgress[i].results.item.type).itemImage;
            slot.transform.localPosition = Vector3.zero;
            slot.transform.localScale = Vector3.one * 3;

            itemSlotsTimerList.Add(slot.amountText);
            cookBuilding.recipiesInProgress[i].UpdateTimer -= UpdateTimerUI;
            cookBuilding.recipiesInProgress[i].UpdateTimer += UpdateTimerUI;
        }
    }
       
    public void CloseButton(bool tutorial = false)
    {
        itemSlotsTimerList.Clear();
        foreach(Transform go in recipiesBoxContainer)
        {
            Destroy(go.gameObject);
        }
        for (int i = 0; i < itemSlots.Length; i++)
        {
            itemSlots[i].previewImage.sprite = null;
            itemSlots[i].amountText.SetText(string.Empty);
        }
        foreach (CookRecipeData item in cookBuilding.recipiesInProgress)
        {
            item.UpdateTimer -= UpdateTimerUI;
        }
        if(tutorial)
            if (!TutorialController.TutorialCompleted())
                TutorialController.OnCookButtonClick?.Invoke();
    }
   
    public void SkipButton()
    {

        if(cookBuilding.recipiesInProgress.Count > 0)
        {
            Debug.Log($"RecepiesData : {cookBuilding.recipiesInProgress[0].timer}");
            cookBuilding.recipiesInProgress[0].FinishRecipe();
            skipButton.SetActive(false);
        }
        if (!TutorialController.TutorialCompleted())
            TutorialController.OnCookButtonClick?.Invoke();
    }
  
}


