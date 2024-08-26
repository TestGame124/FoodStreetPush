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
        public VisitorBehaviour visitor;
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

        LevelController.Instance.zone.UnRegisterTable(this);
        foreach(Chair chair in chairs)
        {
            if(chair.isBusy && chair.isSitted)
            {
                chair.visitor.ResetVisitor(true);
                if(chair.foodPlatePos.childCount > 0)
                    zone.InventoryPosition.ReturnFoodPlate(chair.foodPlatePos.GetChild(0).gameObject);
            }
            else if(chair.isBusy && !chair.isSitted)
            {
                chair.visitor.ResetVisitor();
                chair.visitor.ChangeState(VisitorBehaviour.States.GoToTable);
            }
                chair.ResetStates();
        }
    }
}
