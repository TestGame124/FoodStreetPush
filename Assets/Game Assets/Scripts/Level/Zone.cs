using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FoodStreet.Saving;
using UnityEngine;
using UnityEngine.UI;

public class Zone : MonoBehaviour, ISaveParticipant
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
        

        // Index-based IDs, so scene-authored objects keep the same identity
        // every session. Runtime-placed objects allocate above RUNTIME_ID_BASE.
        for (int i = 0; i < placedObject.Count; i++)
        {
            placedObject[i].InitializeObject(i + 1);
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

    // Runs after the build grid, so every pot and station has been spawned and
    // has an ID by the time state is pushed into it.
    public int SaveOrder => 100;

    private bool restored;

    /// <summary>
    /// Copies the live restaurant into the save model. Called on the main thread
    /// immediately before each write.
    /// </summary>
    public void CaptureState(GameSaveData data)
    {
        // Until the scene has been filled in from the file, the live zone is an
        // empty authored shell. Capturing it would write that emptiness over the
        // player's restaurant - the exact failure the old two-second save loop
        // could hit when it fired mid-load. Leave the saved sections untouched.
        if (!restored)
            return;

        data.world.inventory = inventory.Save();

        data.world.pots.Clear();
        for (int i = 0; i < plantPots.Count; i++)
        {
            PlantPot pot = plantPots[i];
            if (pot == null || pot.OwnerId == 0)
                continue;

            data.world.pots.Add(pot.Save());
        }

        data.world.stations.Clear();
        for (int i = 0; i < cookingBuildings.Count; i++)
        {
            CookeryStation station = cookingBuildings[i];
            if (station == null || station.OwnerId == 0)
                continue;

            data.world.stations.Add(station.Save());
        }
    }

    /// <summary>
    /// Pushes saved state back into the scene, matching by owner ID. Anything
    /// with no entry in the file is simply left in its authored state, and an
    /// entry with no matching object is skipped - neither is an error, because
    /// players build and sell things between sessions.
    /// </summary>
    public void RestoreState(GameSaveData data)
    {
        if (!TutorialController.TutorialCompleted())
        {
            // Nothing to restore during the tutorial, but the zone is now live
            // and its state is the truth - start capturing it.
            restored = true;
            return;
        }

        inventory.Load(data.world.inventory);

        for (int i = 0; i < plantPots.Count; i++)
        {
            PlantPot pot = plantPots[i];
            if (pot == null)
                continue;

            PotSaveData saved = data.world.FindPot(pot.OwnerId);
            if (saved != null)
                pot.Load(saved);
        }

        for (int i = 0; i < cookingBuildings.Count; i++)
        {
            CookeryStation station = cookingBuildings[i];
            if (station == null)
                continue;

            CookStationSaveData saved = data.world.FindStation(station.OwnerId);
            if (saved != null)
                station.Load(saved);
        }

        restored = true;
    }

    #endregion
}