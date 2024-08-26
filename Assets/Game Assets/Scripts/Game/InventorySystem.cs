using System;
using System.Collections;
using System.Collections.Generic;
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

    public InventoryData Save()
    {
       
        return new InventoryData(items.ToArray(), inventorySize);
    }

    public void Load(InventoryData data)
    {
        //if(data == null)
        //{
        //    data = Save(true);
        //}
       
        for (int i = 0; i < data.items.Length; i++)
        {
            Item item = data.items[i];
            Item newItem = new(ItemController.GetItem(item.type),item.itemsAmount);
            AddItem(newItem);
        }
        inventorySize = data.inventorySize;
    }

    #endregion
}


[Serializable]
public class InventoryData
{
    public Item[] items;
    public int inventorySize;


    public InventoryData(Item[]items, int inventorySize)
    {
        this.items = items;
        this.inventorySize = inventorySize;
    }
    
}
