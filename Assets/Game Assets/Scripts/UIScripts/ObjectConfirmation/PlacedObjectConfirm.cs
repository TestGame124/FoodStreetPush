using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlacedObjectConfirm : ConfirmationUiPanel
{
    public override void Initialize(GridBuildingSystem3D gridBuildingSystem, bool canRotate = true, bool canSell = false)
    {
        base.Initialize(gridBuildingSystem, canRotate, canSell);
        ConfirmButton.gameObject.SetActive(canSell);
        
    }

    public override void Cancel() // PlaceObject
    {
        Debug.Log("Placed Object Confirmation");

        if (!gridBuildingSystem.PlaceObject(placedObject))
        { return; }
        gameObject.SetActive(false);
        UIGame.GetUI().uiItemsWindow.ActiveState(true);

        gridBuildingSystem.DeselectObjectType();
        
    }

    // Sell Button
    public override void Confirm()
    {
        Debug.Log("Sold !!");
        CurrenciesController.AddCurrency(CurrencyType.Coins, gridBuildingSystem.selectedObject.sellingPrice);
        
        gridBuildingSystem.RemoveObject(gridBuildingSystem.selectedObject.transform.position);
        gridBuildingSystem.DeselectObjectType();
        gameObject.SetActive(false);
    }
}
