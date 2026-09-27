using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class Zone : MonoBehaviour
{

    [SerializeField] InventorySystem inventory;
    public InventoryObject InventoryPosition;


    [Header("Sign Board")]
    [SerializeField] SignBoard signBoard;

    // The visitor spawner that used to live here is gone. Visitors drive in off the road and
    // park inside the zone, so who arrives and when belongs to
    // FoodStreet.Visitors.VisitorManager, which watches the parking and the chairs - neither
    // of which a zone can see. These transforms stay because the legacy level still uses them
    // to mark its own entry and exit points.
    [Header("Entry and exit")]
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
        

        // Index-based IDs, so scene-authored objects keep the same identity
        // every session. Runtime-placed objects allocate above RUNTIME_ID_BASE.
        for (int i = 0; i < placedObject.Count; i++)
        {
            placedObject[i].InitializeObject(i + 1);
        }

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
            
            if (tableBehaviours[i] && !tableBehaviours[i].IsFull())
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
}