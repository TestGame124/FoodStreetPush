using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class ShopItemDataContainer : MonoBehaviour
{
    public List<PlacedObject_Done> allPlacedObjects = new();
    private static Dictionary<KeyValuePair<ObjectCategory,ObjectType>, ShopItem> shopItemDic = new();

    public ObjectDatabaseHandler.ObjectDatabases[] objectDatabases;
    public void Initialize(ObjectDatabaseHandler.ObjectDatabases[] objectDatabases, UiItemsWindow uiItems)
    {

        this.objectDatabases = objectDatabases;

        uiItems.Initialize(objectDatabases);


        for (int i = 0; i < objectDatabases.Count(); i++)
        {
            ObjectDatabaseHandler.ObjectDatabases item = objectDatabases[i];
            for (int y = 0; y < item.placedObjectTypeSOList.Count(); y++)
            {
                int index = y;
                PlacedObjectTypeSO shopItemData = item.placedObjectTypeSOList[index];


                ItemUiButton itemBtn = uiItems.InitializeShopItemUI(index, item);
                ShopItem shopItem = new ShopItem(index,shopItemData,0, itemBtn);
                
                RegisterShopItem(shopItemData.type, shopItemData.categoryType, shopItem);

            }
        }
    }

   public static void IncreaseItemCount(ShopItem item)
    {
        if(item.CanPurchase())
            item.itemsAmount++;
    }
    
    private void RegisterShopItem(ObjectType type, ObjectCategory otype, ShopItem item)
    {
        KeyValuePair<ObjectCategory, ObjectType> keyValuePair = KeyValuePair.Create(otype, type);
        

        if (!shopItemDic.ContainsKey(keyValuePair))
        {
            shopItemDic.Add(keyValuePair,item);
        }
    }
    public static ShopItem GetShopItem(ObjectType type ,ObjectCategory otype)
    {
        KeyValuePair<ObjectCategory, ObjectType> keyValuePair = KeyValuePair.Create(otype, type);
        if(shopItemDic.ContainsKey(keyValuePair))
            return shopItemDic[keyValuePair];

        return null;
    }
}
