using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemUiButton : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
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

    // In a shelf that scrolls sideways, a sideways drag scrolls the shelf and only a drag up and
    // out of it picks the item up; the drag is handed on to the scroll view for as long as it lasts.
    ScrollRect sidewaysShelf;
    bool scrollingShelf;

    public PlacedObjectTypeSO Data => data;
    public ShopItem Item => shopItem;
    public bool Interactable => button == null || button.interactable;

    public void Initialize(PlacedObjectTypeSO data/*, Action onclick*/)
    {
        //button.onClick.AddListener(() => {
        //    if (shopItem.IsUnlocked)
        //        OnClickOnButton?.Invoke();
        //});
        

        this.data = data;
        
        // Enable It If you want To Add Image Through Scriptable Object 
        //image.sprite = data.previewImage;

        // An item with an icon of its own for the shop - the defenses - shows it.
        if (data.shopIcon != null && image != null)
        {
            image.sprite = data.shopIcon;
            image.preserveAspect = true;
        }

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
        
        requiredLevelText.SetText($"Level {data.requiredLevel}");
    }

    public void ButtonState(bool state)
    {
        button.interactable= state;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (sidewaysShelf == null) sidewaysShelf = GetComponentInParent<ScrollRect>();
        if (sidewaysShelf != null && sidewaysShelf.horizontal && !sidewaysShelf.vertical &&
            Mathf.Abs(eventData.delta.x) > Mathf.Abs(eventData.delta.y))
        {
            scrollingShelf = true;
            sidewaysShelf.OnBeginDrag(eventData);
            return;
        }

        if (shopItem.IsUnlocked && shopItem.CanPurchase())
        {
            CameraPanZoomRig.InputBlocked = true;

            OnClickOnButton?.Invoke();

        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (scrollingShelf && sidewaysShelf != null) sidewaysShelf.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (scrollingShelf)
        {
            scrollingShelf = false;
            if (sidewaysShelf != null) sidewaysShelf.OnEndDrag(eventData);
            return;
        }

        CameraPanZoomRig.InputBlocked = false;
    }

    
}

