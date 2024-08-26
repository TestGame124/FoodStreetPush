using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemUiButton : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    PlacedObjectTypeSO data;
    ShopItem shopItem;

    [SerializeField] private Image image;

    [SerializeField] private GameObject lockedObject;
    [Space]
    [SerializeField] public TextMeshProUGUI limitationText;
    [SerializeField] public TextMeshProUGUI requiredLevelText;
    [SerializeField] public TextMeshProUGUI itemNameText;

    [SerializeField] public TextMeshProUGUI priceText;

    [SerializeField] private Button button;
   

    public Action OnClickOnButton;

    private int itemPlacedCount;

    public void Initialize(PlacedObjectTypeSO data/*, Action onclick*/)
    {
        //button.onClick.AddListener(() => {
        //    if (shopItem.IsUnlocked)
        //        OnClickOnButton?.Invoke();
        //});
        

        this.data = data;
        
        // Enable It If you want To Add Image Through Scriptable Object 
        //image.sprite = data.previewImage;

        if(data.maxItems> 0)
            limitationText.text = $"{itemPlacedCount}/{data.maxItems}";
        else
            limitationText.gameObject.SetActive(false);

        itemNameText.text = data.name;
        if(data.PriceAmount> 0)
        {
            priceText.SetText(data.PriceAmount.ToString());
        }
        else
        {
            priceText.transform.parent.gameObject.SetActive(false);
        }
        //Redraw();
        //LevelUpSystem.OnLevelUp += Redraw;
    }

    public void AssignShopItem(ShopItem shopItem)
    {
        this.shopItem = shopItem;
    }

    
    public void Redraw(int level = 0)
    {
        if (shopItem.IsUnlocked)
        {
            lockedObject.SetActive(false);
            return;
        }
        lockedObject.SetActive(true);
        
        requiredLevelText.SetText($"Need Level {data.requiredLevel} To Unlock");
    }

    public void ButtonState(bool state)
    {
        button.interactable= state;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (shopItem.IsUnlocked && shopItem.CanPurchase())
        {
            CameraTarget.stopDrag = true;

            OnClickOnButton?.Invoke();

        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        CameraTarget.stopDrag = false;
    }

    
}

