using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopItemConfirm : ConfirmationUiPanel
{

    public override void Initialize(GridBuildingSystem3D gridBuildingSystem, bool canRotate = false, bool canSell = false)
    {
        base.Initialize(gridBuildingSystem);
    }
    public override void Cancel()
    {
        if(gridBuildingSystem.GetPlacedObjectTypeSO().categoryType == ObjectCategory.Floor)
        {
            gridBuildingSystem.ClearPreviewTiles();
        }
        gridBuildingSystem.DeselectObjectType();
        
        gameObject.SetActive(false);
    }

    public override void Confirm()
    {
        if (gridBuildingSystem.GetPlacedObjectTypeSO().categoryType != ObjectCategory.Floor)
        {
            if (!gridBuildingSystem.PlaceObject())
                return;
            if(gridBuildingSystem.GetPlacedObjectTypeSO().PriceAmount > 0)
            {
                CurrenciesController.SubtractCurrency(CurrencyType.Coins, gridBuildingSystem.GetPlacedObjectTypeSO().PriceAmount);
            }
        gridBuildingSystem.CurrentShopItem.IncreaseItemCount();

            if (!TutorialController.TutorialCompleted() && gridBuildingSystem.GetPlacedObjectTypeSO().type == ObjectType.SoupStove)
            {
                TutorialController.ObjectPlacingStageEvent?.Invoke();
            }
        }
        else
        {
            gridBuildingSystem.EndDrag();

        }
        
        gameObject.SetActive(false);
        Cancel();

    }

    

}
