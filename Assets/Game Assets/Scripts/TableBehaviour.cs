using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class TableBehaviour : MonoBehaviour
{
    [SerializeField] Chair[] chairs;
    Zone zone;

    [Serializable]
    public class Chair
    {
        public Transform sitPos;
        public bool isBusy;
        public bool foodServed;
        public bool gettingServed;
        public bool isSitted;
        public Transform foodPlatePos;

        public void ResetStates()
        {
            isBusy= false;
            foodServed= false;
            gettingServed = false;
            isSitted= false;
        }
    }
    private void Start()
    {
        zone = LevelController.Instance.zone;
        Initialize(zone);

    }
    private void OnEnable()
    {
        if(LevelController.Instance != null)
        {
            Initialize(LevelController.Instance.zone);
        }
    }
    public void Initialize(Zone zone)
    {
        zone.RegisterTable(this);
    }


    /// <summary>
    /// The chairs, so FoodStreet.Visitors.SeatRegistry can hand each one out.
    ///
    /// Exposed rather than duplicated. These chairs are already authored into the level with
    /// their sit points and plate points, and they are what StaffBehaviour serves - rebuilding
    /// a parallel set of seats beside them would mean the visitor sits in one chair and the
    /// waiter delivers to another.
    /// </summary>
    public Chair[] Chairs => chairs;

    public Chair ChairAt(int index) =>
        chairs != null && index >= 0 && index < chairs.Length ? chairs[index] : null;

    public Chair GetChair()
    {
        foreach (Chair chair in chairs)
        {
            if (!chair.isBusy)
            {
                return chair;
            }
        }
        return null;
    }

    public bool IsFull()
    {
        foreach (Chair chair in chairs)
        {
            if (!chair.isBusy)
            {
                return false;
            }
        }
        return true;
    }

    private void OnDisable()
    {

        // OnDisable also runs while the scene is being torn down, by which point the
        // singleton may already be destroyed. Unregistering is pointless then anyway.
        if (LevelController.Instance != null && LevelController.Instance.zone != null)
            LevelController.Instance.zone.UnRegisterTable(this);

        // A chair no longer holds a reference to whoever is in it. Seat occupancy belongs to
        // FoodStreet.Visitors.SeatRegistry, which releases its own reservations when a table
        // goes away - so this only has to clear the chair's own state and return the plate.
        foreach (Chair chair in chairs)
        {
            if (chair.isBusy && chair.isSitted && chair.foodPlatePos != null &&
                chair.foodPlatePos.childCount > 0 && zone != null)
            {
                zone.InventoryPosition.ReturnFoodPlate(chair.foodPlatePos.GetChild(0).gameObject);
            }

            chair.ResetStates();
        }
    }
}
