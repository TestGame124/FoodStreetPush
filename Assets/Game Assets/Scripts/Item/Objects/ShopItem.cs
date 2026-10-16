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

    public ShopItem(int id, PlacedObjectTypeSO itemData, int itemsAmount, ItemUiButton itemUI)
    {
        this.id = id;
        this.itemData = itemData;
        this.itemsAmount = itemsAmount;
        this.itemUI = itemUI;

        // An unlock recorded in the save file always wins. Levelling is only one
        // of the ways an item can open up, and a granted unlock must survive a
        // rebalance that raises the required level.
        this.isUnlocked = itemData.isUnlocked || LevelUpSystem.GetLevel() >= itemData.requiredLevel;

        itemUI.AssignShopItem(this);

        Unlock();

        if (!isUnlocked)
        {
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
        return true;
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
            itemUI.ButtonState(CanPurchase());
        }
    }

    public void Unlock()
    {
        if (isUnlocked)
        {
            itemUI.Redraw();
            return;
        }

        if (itemData.isUnlocked || LevelUpSystem.GetLevel() >= itemData.requiredLevel)
        {
            isUnlocked = true;

            // Stop listening once it can never be relevant again - these are
            // static events, and a ShopItem is rebuilt on every scene load.
            LevelUpSystem.OnLevelUp -= AddReward;
        }

        itemUI.Redraw();
    }

    private void AddReward(int level)
    {
        if (IsUnlocked)
            return;

        if (level >= itemData.requiredLevel) Unlock();
    }
}
