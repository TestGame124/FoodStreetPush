using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public abstract class ConfirmationUiPanel : MonoBehaviour
{
    [SerializeField] public Button ConfirmButton, RotateButton, CancelButton;
    public Action OnRotation;
    public Action OnConfirmation;

    protected GridBuildingSystem3D gridBuildingSystem;
    protected PlacedObject_Done placedObject;

    private void Awake()
    {
        ConfirmButton.onClick.AddListener(Confirm);
        RotateButton.onClick.AddListener(Rotate);
        CancelButton.onClick.AddListener(Cancel);
    }

    public virtual void Initialize(GridBuildingSystem3D gridBuildingSystem, bool canRotate = true, bool canSell = false)
    {
        this.gridBuildingSystem = gridBuildingSystem;
       
        RotateButton.gameObject.SetActive(canRotate);
        
    }

    public void SetPlacedObjectData(PlacedObject_Done placedObject)
    {
        this.placedObject = placedObject; 
    }    

    public abstract void Confirm();

    public void Rotate() { 

        gridBuildingSystem.RotateObject();
        OnRotation?.Invoke();
    }
    public abstract void Cancel();


}
