using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ShopItem
{
    public int id;
    public PlacedObjectTypeSO itemData;

    public ObjectCategory type => itemData.categoryType;

    public int itemsAmount;
    public ItemUiButton itemUI;

    private bool isUnlocked;
    public bool IsUnlocked => isUnlocked;

    public ShopItem(int id,PlacedObjectTypeSO itemData, int itemsAmount, ItemUiButton itemUI)
    {
        this.id = id;
        this.itemData = itemData;
        this.itemsAmount = itemsAmount;
        this.itemUI = itemUI;
        this.isUnlocked = itemData.isUnlocked;
        itemUI.AssignShopItem(this);

        Unlock();

        if (!isUnlocked) {

            // Catch the case where the player is already one level away from
            // unlocking this item, then keep watching for future level-ups.
            AddReward(LevelUpSystem.GetLevel());
            LevelUpSystem.OnLevelUp += AddReward;
        }
    }

    public bool CanPurchase()
    {
        if (!isUnlocked)
            return false;
        if (itemData.maxItems > 0)
        {
            if (itemsAmount >= itemData.maxItems)
                return false;
        }
        return true ;
    }

    public void IncreaseItemCount()
    {
        if (CanPurchase())
        {
            itemsAmount++;
            RedrawUI();
        }
    }

    public void DecreaseItemCount()
    {
        itemsAmount--;
        RedrawUI();
    }

    public void RedrawUI()
    {
        if (itemUI != null)
        {
            itemUI.limitationText.SetText($"{itemsAmount}/{itemData.maxItems}");
            if (!CanPurchase())
            {
                itemUI.ButtonState(false);
            }
            else
            {
                itemUI.ButtonState(true);
            }
        }
    }

    public void Unlock()
    {
        if (itemData.isUnlocked || isUnlocked)
            return;

        Debug.Log("Before Unlock Item");
        if(LevelUpSystem.GetLevel() >= itemData.requiredLevel)
        {
            Debug.Log("After Unlock Item");
            isUnlocked = true ;
        }
            itemUI.Redraw();
    }

    private void AddReward(int level)
    {
        if (IsUnlocked)
            return;

        if (level == itemData.requiredLevel - 1)
        {
            LevelUpSystem.AddReward(this);
        }
    }

    
}
