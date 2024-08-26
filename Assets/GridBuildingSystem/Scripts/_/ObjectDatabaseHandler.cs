using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectDatabaseHandler : MonoBehaviour
{
    [SerializeField] private ObjectDatabases[] objectsDatabases;
    public ObjectDatabases[] ObjectsDatabases => objectsDatabases;
    private static Dictionary<ObjectCategory, PlacedObjectTypeSO[]> AllObjectsCategoryType = new();
    private static Dictionary<ObjectType, PlacedObjectTypeSO> allObjects = new();

    [Serializable]
    public struct ObjectDatabases
    {
        public ObjectCategory type;
        public PlacedObjectTypeSO[] placedObjectTypeSOList;
    }

    public void Initialize()
    {
        RegisterDatabases();
    }

    public void RegisterDatabases()
    {
        foreach (ObjectDatabases od in objectsDatabases)
        {
            if (!AllObjectsCategoryType.ContainsKey(od.type))
            {
                AllObjectsCategoryType.Add(od.type, od.placedObjectTypeSOList);
                foreach(PlacedObjectTypeSO ot in od.placedObjectTypeSOList)
                {
                    if(!allObjects.ContainsKey(ot.type))
                    {
                        //ObjectPoolingManager.CreatePool(ot.prefab, 3, 5);
                        ot.Initialize();
                        allObjects.Add(ot.type, ot);
                    }
                }
            }
        }
    }

    public static PlacedObjectTypeSO GetPlacedObject(ObjectType type) 
    {
        if(allObjects.ContainsKey(type))
            return allObjects[type];
        return null;
    }

    public static PlacedObjectTypeSO[] GetObjectDataBase(ObjectCategory type)
    {
        if(AllObjectsCategoryType.ContainsKey(type))
            return AllObjectsCategoryType[type];
        return null;
    }
    
    
}