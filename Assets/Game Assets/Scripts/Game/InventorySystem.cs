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

    /// <summary>
    /// Takes a whole amount of one item, or nothing at all.
    ///
    /// RemoveItem decrements by exactly one per call and drops the entry when it reaches one,
    /// so spending three of something is three calls with an off-by-one waiting in it. Worse,
    /// a caller that checks the count and then removes in a loop can leave a visitor half paid
    /// for if the count was wrong. This checks first and moves once, so a purchase either
    /// happens or does not.
    /// </summary>
    public bool TryConsume(ItemType type, int amount)
    {
        if (amount <= 0) return true;

        Item stored = GetItem(type);
        if (stored == null || stored.itemsAmount < amount) return false;

        stored.itemsAmount -= amount;

        if (stored.itemsAmount <= 0)
        {
            items.Remove(stored);
            itemDict.Remove(type);
        }

        onInventoryChangedCallback?.Invoke();
        return true;
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
}
