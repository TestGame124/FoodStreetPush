using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Item 
{
    public ItemDataSO itemDataSO;

    public ItemType type => itemDataSO.Type;

    public bool canBePlanted => itemDataSO.canBePlanted;
    public int itemsAmount;

    public Item(ItemDataSO itemDataSO, int itemsAmount)
    {
        this.itemDataSO = itemDataSO;
        this.itemsAmount = itemsAmount;


    }
}
