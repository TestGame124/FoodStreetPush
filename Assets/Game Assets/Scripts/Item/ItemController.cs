using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemController : MonoBehaviour
{
    public static ItemController instance;

    [SerializeField] public ItemsDatabase itemsDatabase;
    private static Dictionary<ItemType, ItemDataSO> itemsDict = new();
    public static int TotalItems;
    public ItemReceiver itemReceiver;

    public void Initialize()
    {
        instance = this;
        RegisterItems();

    }
    private void RegisterItems()
    {
        for (int i = 0; i < itemsDatabase.AllItems.Length; i++)
        {
            ItemDataSO tempItem = itemsDatabase.AllItems[i];
            if (!itemsDict.ContainsKey(tempItem.Type))
            {
                itemsDict.Add(tempItem.Type, tempItem);
                TotalItems++;
            }
        }
    }

    public static ItemDataSO GetItem(ItemType type)
    {
        if (!itemsDict.ContainsKey(type))
        {
            Debug.LogError("No Item Type Of : " + type + " Found!");
            return null;
        }
        return itemsDict[type];
    }
}
