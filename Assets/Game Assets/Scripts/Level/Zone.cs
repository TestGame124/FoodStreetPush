using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class Zone : MonoBehaviour
{

    [SerializeField] InventorySystem inventory;
    public InventoryObject InventoryPosition;


    [Header("Sign Board")]
    [SerializeField] SignBoard signBoard;
    [Header("Visitor Settings")]
    [SerializeField] VisitorSpawner visitorSpawner;
    public Transform EntryPosition;
    public Transform[] exitPositions;
    [Space]
    [SerializeField] StaffBehaviour[] staffs;
    [Space]
    [SerializeField] List<PlacedObject_Done> placedObject;

    [Header("Tables")]
    [SerializeField] List<TableBehaviour> tableBehaviours = new();

    private List<TableBehaviour.Chair> activeChairs = new();

    [Header("Cook Shop")]
    [SerializeField] List<CookeryStation> cookingBuildings = new();
    public List<CookeryStation> CookingBuildings => cookingBuildings;

    [Header("Pots")]
    [SerializeField] List<PlantPot> plantPots = new();
    public List<PlantPot> PlantPots => plantPots;


    public static Action CleanPlacedObjects;
    public void Initialize()
    {
        

        for (int i = 0; i < placedObject.Count; i++)
        {
            placedObject[i].InitializeObject(SaveController.GiveUniqueID());
            
        }

        visitorSpawner.Initialize(this);

        foreach (StaffBehaviour staff in staffs)
        {
            staff.Initialize(this);
        }
       
        signBoard.Initialize();
    }

    public PlacedObject_Done GetAlreadyPlacedObject(int id)
    {
        return this.placedObject.Find(x => x.ID == id);
    }
    public List<PlacedObject_Done> GetAlreadyObjectsList()
    {
        return placedObject;
    }
    public void RemoveAlreadyPlacedObject(PlacedObject_Done placed)
    {
        PlacedObject_Done temp = placedObject.Find(x =>x.ID == placed.ID);
        if (placedObject.Contains(temp))
        {
            placedObject.Remove(temp);
            //placedObject.Remove(placed);
        }
    }

    public TableBehaviour GetTable()
    {
        for (int i = 0; i < tableBehaviours.Count; i++)
        {
            if (!tableBehaviours[i].IsFull())
                return tableBehaviours[i];
        }
        return null;
    }

    #region Table Behaviour
    public void RegisterTable(TableBehaviour table)
    {
        if (tableBehaviours.Contains(table))
            return;
        tableBehaviours.Add(table);
        //table.Initialize(this);
    }
    public void UnRegisterTable(TableBehaviour table)
    {
        if (!tableBehaviours.Contains(table))
            return;
        tableBehaviours.Remove(table);

    }

    public void AddActiveChairs(TableBehaviour.Chair activeChair)
    {
        if (activeChairs.Contains(activeChair)) return;
        activeChairs.Add(activeChair);
    }
    public void Remove(TableBehaviour.Chair activeChair)
    {
        if (!activeChairs.Contains(activeChair)) return;
        activeChairs.Remove(activeChair);
    }

    public List<TableBehaviour.Chair> ActiveChairs()
    {
        return activeChairs;
    }
    #endregion

    #region Plant Pots

    public void RegisterPot(PlantPot pot)
    {
        if(plantPots.Contains(pot))
            return;
        plantPots.Add(pot);
    }
    public void UnRegisterPot(PlantPot pot)
    {
        if(!plantPots.Contains(pot))
            return;
        plantPots.Remove(pot);
    }

    #endregion

    #region Shops

    public void RegisterStation(CookeryStation station)
    {
        if(cookingBuildings.Contains(station))
            return;
        cookingBuildings.Add(station);
    }public void UnRegisterStation(CookeryStation station)
    {
        if(!cookingBuildings.Contains(station))
            return;
        cookingBuildings.Remove(station);
    }

    #endregion

    #region Saving Loading
    public ZoneData Save()
    {
       
        InventoryData inventoryData = inventory.Save();
        PotData[] potData = new PotData[plantPots.Count];

        for (int i = 0; i < potData.Length; i++)
        {
            potData[i] = plantPots[i].Save();
        }

        CookStationData[] cookData = new CookStationData[cookingBuildings.Count];

        for(int i = 0; i < cookData.Length; i++)
        {
            cookData[i] = cookingBuildings[i].Save();
        }

        return new ZoneData(inventoryData, potData, cookData);
    }

    public void Load(ZoneData data)
    {
        if (!TutorialController.TutorialCompleted())
            return;
        if (data == null)
        {
            data = Save();
        }

        inventory.Load(data.inventory);
        for (int i = 0; i < plantPots.Count; i++)
        {
            plantPots[i].Load(data.potsData[i]);
        }

        for (int i = 0; i < cookingBuildings.Count; i++)
        {

            cookingBuildings[i].Load(data.cookData[i]);
        }
    }

    
    #endregion
}

[System.Serializable]
public class ZoneData
{


    public InventoryData inventory;
    public PotData[] potsData;
    public CookStationData[] cookData;
    public ZoneData(InventoryData inventory, PotData[] potsData, CookStationData[] cookData)
    {
        this.inventory = inventory;
        this.potsData = potsData;
        this.cookData = cookData;
    }
   

}