using System.Collections;
using System.Collections.Generic;
using LastOrders.Audio;
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
        {
            SoundController.Play(SoundId.PlaceRefused);
            return;
        }
        gameObject.SetActive(false);
        UIGame.GetUI().uiItemsWindow.ActiveState(true);

        gridBuildingSystem.DeselectObjectType();
        
    }

    // Sell Button
    public override void Confirm()
    {
        if (gridBuildingSystem.selectedObject == null || gridBuildingSystem.Placement == null ||
            !gridBuildingSystem.Placement.CanRemove(gridBuildingSystem.selectedObject))
        {
            SoundController.Play(SoundId.PlaceRefused);
            return;
        }
        Debug.Log("Sold !!");
        SoundController.Play(SoundId.Sell);
        LastOrders.Economy.RewardSource.At(gridBuildingSystem.selectedObject.transform.position + Vector3.up);
        CurrenciesController.AddCurrency(CurrencyType.Coins, gridBuildingSystem.selectedObject.sellingPrice);
        
        gridBuildingSystem.RemoveObject(gridBuildingSystem.selectedObject.transform.position);
        gridBuildingSystem.DeselectObjectType();
        gameObject.SetActive(false);
    }
}
