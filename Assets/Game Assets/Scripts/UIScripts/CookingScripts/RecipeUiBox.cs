using CodeMonkey.Utils;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RecipeUiBox : MonoBehaviour
{

    InventorySystem inventorySystem;

    [SerializeField] public Button CookBtn;
    [SerializeField] public Image CookingItemImage;
    [SerializeField] public Text TimerText;

    private List<ItemSlotUI> itemSlotUI = new();

    [SerializeField] float slotSize = 1.5f;

    [SerializeField] public Transform ingredientsArea;
    [SerializeField] ItemSlotUI itemSlotPrefab;

    private CookingRecipe cookingRecipe;
    private CookingRecipe CookingRecipe
    {
        get { return cookingRecipe; }
        set { SetCookingRecipe(value); }
    }
    CookeryStation cookeryBuilding;
    private void OnEnable()
    {
        Vector3 scale = transform.localScale;
        transform.DOScale(scale, 0.3f).From(0).SetEase(Ease.OutBack);
    }
    public void Initialze(InventorySystem inventorysystem, CookingRecipe recipe, CookeryStation cookeryBuilding)
    {
        this.inventorySystem= inventorysystem;
        cookingRecipe= recipe;
        this.cookeryBuilding = cookeryBuilding;
        
        itemSlotUI.Clear();

        CookingItemImage.sprite = recipe.RecipeImage;

        TimerText.text = TimeSpan.FromSeconds(recipe.TotalTimeToCook).TotalSeconds + "s";
        for(int i = 0; i < recipe.Ingrediants.Count; i++)
        {
            ItemSlotUI itemSlot = Instantiate(itemSlotPrefab, ingredientsArea);
            itemSlot.transform.localScale = Vector3.one * slotSize;
            itemSlot.previewImage.sprite = recipe.Ingrediants[i].item.itemDataSO.itemImage;
            itemSlot.amountText.text = $" {inventorysystem.ItemCount(recipe.Ingrediants[i].item.type)} / {recipe.Ingrediants[i].Amount}";
            itemSlotUI.Add(itemSlot);
        }

       
        CookingRecipe.OnStartCooking += Redraw;
        CookBtn.onClick.AddListener(StartCookingButton);
    }
    private void OnDisable()
    {
        CookingRecipe.OnStartCooking = null;
    }
    private void StartCookingButton()
    {
        if (cookingRecipe != null && inventorySystem != null)
        {

            if (cookingRecipe.CanCook(inventorySystem))
            {
                if (!inventorySystem.IsFull())
                {
                    if (!cookeryBuilding.StartCooking(cookingRecipe, inventorySystem))
                        return;
                    

                    cookingRecipe.Cook(inventorySystem);
                    //cookeryBuilding.StartCooking();
                }
                else
                {
                    Debug.LogError("Inventory Is Full");
                }
            }
            else
            {
                UtilsClass.CreateWorldTextPopup("Not Enough Ingredient!!", transform.localPosition);
                //Debug.LogError("Not Enough Ingredients");
                UIGame.GetUI().notEnoughIng.SetActive(true);
                for (int i = 0; i < UIGame.GetUI().notEnoughIng.transform.childCount; i++)
                {
                    Transform child = UIGame.GetUI().notEnoughIng.transform.GetChild(i);
                    child.DOLocalMoveY(0, 0.5f).From(-500).SetEase(Ease.OutBack);
                    child.DOScale(1, 0.5f).From(0.5f).SetEase(Ease.OutBack);
                    
                }

            }

        }
    }
        
    private void Redraw()
    {
        for (int i = 0; i < CookingRecipe.Ingrediants.Count; i++)
        {
            itemSlotUI[i].amountText.text = $" {inventorySystem.ItemCount(CookingRecipe.Ingrediants[i].item.type)} / {CookingRecipe.Ingrediants[i].Amount}";
        }

    }
    private void SetCookingRecipe(CookingRecipe recipe)
    {
        cookingRecipe = recipe;

        if (cookingRecipe != null)
        {

            int slotIndex = 0;
            //slotIndex = SetSlots(cookingRecipe.Ingrediants, slotIndex);


        }
    }

}
