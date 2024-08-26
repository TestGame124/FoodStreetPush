using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlacedObject_Done : MonoBehaviour, IInteractableObjectEditorState {



    public static PlacedObject_Done Create(Vector3 worldPosition, Vector2Int origin, PlacedObjectTypeSO.Dir dir, PlacedObjectTypeSO placedObjectTypeSO) {
        //Transform placedObjectTransform = Instantiate(placedObjectTypeSO.prefab, worldPosition, Quaternion.Euler(0, placedObjectTypeSO.GetRotationAngle(dir), 0));
        Transform placedObjectTransform = placedObjectTypeSO.GetObject();
        placedObjectTransform.position = worldPosition;
        placedObjectTransform.rotation = Quaternion.Euler(0, placedObjectTypeSO.GetRotationAngle(dir), 0);
        //Transform placedObjectTransform = ObjectPoolingManager.GetFromPool(placedObjectTypeSO.prefab, worldPosition, Quaternion.Euler(0, placedObjectTypeSO.GetRotationAngle(dir), 0));
        

        PlacedObject_Done placedObject = placedObjectTransform.GetComponent<PlacedObject_Done>();
        placedObject.Setup(placedObjectTypeSO, origin, dir);
        

        return placedObject; 
    }

    private int id;
    public int ID => id;

   


    public bool IsPlaced;

    private Transform objParent;

    private PlacedObjectTypeSO placedObjectTypeSO;
    public PlacedObjectTypeSO PlacedObjectTypeSO => placedObjectTypeSO;
    private Vector2Int origin;
    [SerializeField]private PlacedObjectTypeSO.Dir dir;
    public PlacedObjectTypeSO.Dir Dir => dir;

    //[Range(0,270)]
    [SerializeField] int directionVal => PlacedObjectTypeSO.GetRotationAngle(dir);

    public ObjectType shopItemType;

    [HideInInspector]public int sellingPrice;

    public Vector3 placedPosition;

    bool alreadyInitialized;

    public bool isRemoved;
    public void InitializeObject(int id)
    {
        objParent = transform.parent;
        if (IsPlaced)
            return;
        IsPlaced = true;
        this.id = id;
        alreadyInitialized= true;

        
        GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();
        placedObjectTypeSO = ObjectDatabaseHandler.GetPlacedObject(shopItemType);

        //GridBuildingSystem3D.Instance.SetPlacedObjectTypeSO(placedObjectTypeSO);
        placedPosition = transform.position;

        grid.GetXZ(transform.position, out int x, out int z);

        Vector2Int placedObjectOrigin = new Vector2Int(x, z);
        placedObjectOrigin = grid.ValidateGridPosition(placedObjectOrigin);
        
        // Test Can Build

        if (placedObjectTypeSO.Sellable)
        {
            sellingPrice = Mathf.RoundToInt(placedObjectTypeSO.PriceAmount * .3f);
        }


        


        Vector2Int rotationOffset = placedObjectTypeSO.GetRotationOffset(dir);
        Vector3 placedObjectWorldPosition = grid.GetWorldPosition(placedObjectOrigin.x, placedObjectOrigin.y) + new Vector3(rotationOffset.x, 0, rotationOffset.y) * grid.GetCellSize();



        PlaceSelectedObject(placedObjectWorldPosition, placedObjectOrigin,dir);

        List<Vector2Int> gridPositionList = placedObjectTypeSO.GetGridPositionList(placedObjectOrigin, dir);

        if (/*GameHandler.FirstTime &&*/ placedObjectTypeSO.categoryType != ObjectCategory.Floor)
        {
            foreach (Vector2Int gridPosition in gridPositionList)
            {
                grid.GetGridObject(gridPosition.x, gridPosition.y).SetPlacedObject(this);

            }
           
        }

        if (GameHandler.FirstTime)
        {
            GridBuildingSystem3D.Instance.SavePlacedObject(this);

        }
       
    }

    private void Setup(PlacedObjectTypeSO placedObjectTypeSO, Vector2Int origin, PlacedObjectTypeSO.Dir dir) {
        this.placedObjectTypeSO = placedObjectTypeSO;
        this.origin = origin;
        this.dir = dir;

        if (!alreadyInitialized)
        {

            alreadyInitialized= true;
            id = SaveController.GiveUniqueID();
        }
        if (placedObjectTypeSO.categoryType != ObjectCategory.Floor)
        {
            ShopItem shopItem = ShopItemDataContainer.GetShopItem(placedObjectTypeSO.type, placedObjectTypeSO.categoryType);
            shopItem.IncreaseItemCount();
        }
        if (placedObjectTypeSO.Sellable)
        {
            sellingPrice = Mathf.RoundToInt(placedObjectTypeSO.PriceAmount * .3f);
        }


        shopItemType = placedObjectTypeSO.type;
       
        placedPosition = transform.position;

        if (placedObjectTypeSO.categoryType != ObjectCategory.Floor)
            transform.DOScale(1, 0.3f).From(1.2f).SetEase(Ease.OutBack);
        else
            GridBuildingSystem3D.AddPlacedTiles(origin, this);


        IsPlaced = true;
    }

    public List<Vector2Int> GetGridPositionList() {
        return placedObjectTypeSO.GetGridPositionList(origin, dir);
    }

    public void DestroySelf() {
        //Destroy(gameObject);
        isRemoved = true;

        if (placedObjectTypeSO.type == ObjectType.Floor1)
        {
            GridBuildingSystem3D.RemovePlacedTiles(origin);
        }
        else
        {
            ShopItem shopItem = ShopItemDataContainer.GetShopItem(placedObjectTypeSO.type, placedObjectTypeSO.categoryType);
            shopItem.DecreaseItemCount();
            
        }
       
        placedObjectTypeSO.ReleaseObject(transform);
        if (placedObjectTypeSO.categoryType != ObjectCategory.Floor)
        {
            GridBuildingSystem3D.Instance.SavePlacedObject(this, true);
        }
        transform.SetParent(null);

    }

    

    public bool IsFloor()
    {
        return placedObjectTypeSO.categoryType == ObjectCategory.Floor;
    }

    
    public void PlaceSelectedObject(Vector3 worldPosition, Vector2Int origin, PlacedObjectTypeSO.Dir dir)
    {
        transform.SetParent(objParent);
        transform.position = worldPosition;
        transform.rotation = Quaternion.Euler(0, placedObjectTypeSO.GetRotationAngle(dir), 0);
        Setup(placedObjectTypeSO, origin, dir);
    }
    public void OnTouch()
    {
        if (UIGame.IsPointerOverUI())
            return;
        if (GridBuildingSystem3D.Instance.GhostObject.selectedState)
            return;
        Debug.Log($"Item Name : {name}");
        foreach (Vector2Int gridPosition in GetGridPositionList())
        {
            GridBuildingSystem3D.GetGrid().GetGridObject(gridPosition.x, gridPosition.y).ClearPlacedObject();
        }
        
        GridBuildingSystem3D.Instance.GhostObject.SelectObject(this);
        UIGame.GetUI().placeObjectPanel.SetPlacedObjectData(this);
    }

    public PlacedObjectData GetPlacedObjectData()
    {
        PlacedObjectData data = new PlacedObjectData
        {
            Id = ID,
            objectType = GetPlacedObjectTypeSO().type,
            position = new BayatGames.SaveGameFree.Types.Vector3Save(transform.position),
            direction = GetDir(),
            isRemoved = isRemoved

        };
       
        return data;
    }
    internal PlacedObjectTypeSO GetPlacedObjectTypeSO()
    {
        return placedObjectTypeSO;
    }

    internal PlacedObjectTypeSO.Dir GetDir()
    {
        return dir;
    }
}
