using System;
using System.Collections;
using System.Collections.Generic;
using FoodStreet.Saving;
using UnityEngine;

public class InventorySystem : MonoBehaviour
{
    public static InventorySystem instance;

    private List<Item> items = new List<Item>();
    private Dictionary<ItemType, Item> itemDict = new();

    public int inventorySize = 20;

    public delegate void OnInventoryChanged();
    public static event OnInventoryChanged onInventoryChangedCallback;

    private void Awake()
    {
        instance = this;
    }
    public void Initialize()
    {

        // initialize If Needed
        //ForTesting();
        
    }

    void ForTesting()
    {
        Item itemTemp = new Item(ItemController.GetItem(ItemType.Flour), 99);
        Item itemTemp2 = new Item(ItemController.GetItem(ItemType.Tomato), 1);
        Item itemTemp3 = new Item(ItemController.GetItem(ItemType.CornBread), 10);
        Item itemTemp4 = new Item(ItemController.GetItem(ItemType.TomatoSoup), 10);
        Item itemTemp5 = new Item(ItemController.GetItem(ItemType.Bread), 20);
        AddItem(itemTemp);
        AddItem(itemTemp2);
        AddItem(itemTemp3);
        AddItem(itemTemp4);
        AddItem(itemTemp5);

    }
    public bool AddItem(Item item)
    {
        if (IsFull())
        {
            Debug.Log("Inventory is full!");
            return false;
        }

        bool itemAlreadyExist = false;
        if (ContainsItem(item.type))
        {
        Item inventoryItem = GetItem(item.type);
            inventoryItem.itemsAmount += 1;
            itemAlreadyExist = true;

        }

       
        if (!itemAlreadyExist)
        {
            items.Add(item);
            itemDict.Add(item.type,item);
        }

        onInventoryChangedCallback?.Invoke();
        return true;
    }

    public bool RemoveItem(Item item)
    {
        if (itemDict.ContainsKey(item.type))
        {

            //if(item.itemsAmount> 1) {
            //    item.itemsAmount -= 1;
            //}
            Item inventoryItem = GetItem(item.type);
            if (inventoryItem.itemsAmount > 1)
            {
                inventoryItem.itemsAmount -= 1;
            }
            else
            {

                items.Remove(inventoryItem);
                itemDict.Remove(inventoryItem.type);

            }
            onInventoryChangedCallback?.Invoke();
            return true;
        }
        Debug.Log(item.type + " not found in inventory.");
        return false;
    }

    public int ItemCount(ItemType itemtype)
    {
        if(GetItem(itemtype) != null)
            return GetItem(itemtype).itemsAmount;
        return 0;
    }

    public bool IsFull()
    {
        return items.Count >= inventorySize;
    }

    public Item GetItem(ItemType type)
    {
        if(itemDict.ContainsKey(type))
            return itemDict[type];
        return null;
    }
    public bool ContainsItem(ItemType item)
    {
        return itemDict.ContainsKey(item);
    }

    public List<Item> GetItemList()
    {
        return items;
    }

    #region Saving Loading

    public InventorySaveData Save()
    {
        InventorySaveData data = new InventorySaveData { size = inventorySize };

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null || item.itemDataSO == null)
                continue;

            // Type and count only. The old format wrote the whole ItemDataSO
            // asset into the save file for every stack.
            data.items.Add(new InventoryEntrySaveData
            {
                type = item.type,
                amount = item.itemsAmount
            });
        }

        return data;
    }

    public void Load(InventorySaveData data)
    {
        if (data == null)
            return;

        items.Clear();
        itemDict.Clear();

        inventorySize = data.size > 0 ? data.size : inventorySize;

        for (int i = 0; i < data.items.Count; i++)
        {
            InventoryEntrySaveData entry = data.items[i];
            if (entry.amount <= 0)
                continue;

            ItemDataSO itemData = ItemController.GetItem(entry.type);
            if (itemData == null)
                continue;

            // Rebuilt directly rather than through AddItem: that path adds one
            // at a time and would restore a stack of 40 as a stack of 1.
            Item item = new Item(itemData, entry.amount);
            items.Add(item);
            itemDict[item.type] = item;
        }

        onInventoryChangedCallback?.Invoke();
    }

    #endregion
}
