using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;


public class InventoryUIBehaviour : MonoBehaviour
{
    private InventorySystem _inventorySystem;
    [SerializeField]private Transform itemSlotContainer;
    [SerializeField]private ItemSlotUI itemSLotTemplate;

    private List<ItemSlotUI> allItemsSlot = new();

   
    private void OnDestroy()
    {
        InventorySystem.onInventoryChangedCallback -= RefreshInventoryItems;
    }
    private void OnEnable()
    {
        itemSlotContainer.GetComponentInParent<UnityEngine.UI.ScrollRect>().transform.DOScale(1, 0.3f).From(0).SetEase(Ease.OutBack);

        
        for (int i = 0; i < allItemsSlot.Count; i++)
        {
            allItemsSlot[i].transform.DOScale(1, 0.3f).From(0).SetEase(Ease.OutBack).SetDelay(i * 0.15f);
        }
    }
    public void Initialise(InventorySystem inventorySystem)
    {
        _inventorySystem= inventorySystem;
        InventorySystem.onInventoryChangedCallback += RefreshInventoryItems;

        RefreshInventoryItems();
    }

    private void RefreshInventoryItems()
    {
        foreach(Transform child in itemSlotContainer)
        {
            //if (child == itemSLotTemplate) continue;
            Destroy(child.gameObject);
        }

        allItemsSlot.Clear();

        foreach(Item item in _inventorySystem.GetItemList())
        {
            ItemSlotUI itemSlotRectTransform = Instantiate(itemSLotTemplate, itemSlotContainer);

            if (item.itemsAmount > 1) {

                itemSlotRectTransform.amountText.SetText(item.itemsAmount.ToString());
            }
            else
            {
                itemSlotRectTransform.amountText.SetText("");
            }
            allItemsSlot.Add(itemSlotRectTransform);
            itemSlotRectTransform.previewImage.sprite = item.itemDataSO.itemImage;

            itemSlotRectTransform.gameObject.SetActive(true);

            
        }
    }
}