using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemsDatabase", menuName = "Items/Database")]
public class ItemsDatabase : ScriptableObject
{

    public ItemDataSO[] AllItems;
    public ItemDataSO[] AllRecipies;

}
