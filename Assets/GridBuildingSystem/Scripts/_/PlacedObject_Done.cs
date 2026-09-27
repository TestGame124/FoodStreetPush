using System.Collections.Generic;
using UnityEngine;

/// <summary>Placed instance identity and pose; grid occupancy is owned by PlacementService.</summary>
public class PlacedObject_Done : MonoBehaviour, IInteractableObjectEditorState
{
    [SerializeField] PlacedObjectTypeSO placedObjectTypeSO;
    [SerializeField] Vector2Int origin;
    [SerializeField] PlacedObjectTypeSO.Dir dir;
    int id;
    static int transientId = -1;
    Transform objParent;
    bool countedInShop;
    public int ID => id;
    public Vector2Int Origin => origin;
    public PlacedObjectTypeSO PlacedObjectTypeSO => placedObjectTypeSO;
    public PlacedObjectTypeSO.Dir Dir => dir;
    public bool IsPlaced;
    public ObjectType shopItemType;
    [HideInInspector] public int sellingPrice;
    public Vector3 placedPosition;
    public bool isRemoved;

    public static PlacedObject_Done Create(Vector3 worldPosition, Vector2Int origin,
        PlacedObjectTypeSO.Dir dir, PlacedObjectTypeSO type)
    {
        var instance = type.GetObject();
        var placed = instance.GetComponent<PlacedObject_Done>();
        placed.objParent = null;
        instance.SetParent(null);
        placed.ConfigurePlacement(type, origin, dir);
        placed.id = transientId--;
        placed.isRemoved = false;
        placed.countedInShop = false;
        placed.RestorePlacementPose(worldPosition);
        placed.RegisterShopCount();
        return placed;
    }

    public void InitializeObject(int identity)
    {
        objParent = transform.parent;
        id = identity;
        if (placedObjectTypeSO == null) placedObjectTypeSO = ObjectDatabaseHandler.GetPlacedObject(shopItemType);
        var service = GridBuildingSystem3D.Instance != null ? GridBuildingSystem3D.Instance.Placement : null;
        if (placedObjectTypeSO == null || service == null) return;
        if (!IsPlaced) origin = service.Layout.PivotToCell(placedObjectTypeSO, transform.position, dir);
        ConfigurePlacement(placedObjectTypeSO, origin, dir);
        placedPosition = transform.position;
        service.Register(this);
        RegisterShopCount();
    }

    public void ConfigurePlacement(PlacedObjectTypeSO type, Vector2Int cell, PlacedObjectTypeSO.Dir direction)
    {
        placedObjectTypeSO = type; origin = cell; dir = direction;
        shopItemType = type.type;
        sellingPrice = type.Sellable ? Mathf.RoundToInt(type.PriceAmount * .3f) : 0;
        IsPlaced = true; isRemoved = false;
    }
    public void RestoreIdentity(int identity) => id = identity;
    void RegisterShopCount()
    {
        if (countedInShop || IsFloor()) return;
        var shop = ShopItemDataContainer.GetShopItem(placedObjectTypeSO.type, placedObjectTypeSO.categoryType);
        if (shop == null) return;
        // Count existing instances even if their asset is currently locked or at its purchase limit.
        shop.itemsAmount++; shop.RedrawUI(); countedInShop = true;
    }
    public void RestorePlacementPose(Vector3 position)
    {
        transform.SetParent(objParent, true);
        transform.SetPositionAndRotation(position, Quaternion.Euler(0, placedObjectTypeSO.GetRotationAngle(dir), 0));
        placedPosition = position;
    }
    public void PlaceSelectedObject(Vector3 worldPosition, Vector2Int cell, PlacedObjectTypeSO.Dir direction)
    {
        origin = cell; dir = direction;
        RestorePlacementPose(worldPosition);
    }
    public List<Vector2Int> GetGridPositionList() => placedObjectTypeSO.GetGridPositionList(origin, dir);
    public bool IsFloor() => placedObjectTypeSO != null && placedObjectTypeSO.categoryType == ObjectCategory.Floor;

    public void DestroySelf()
    {
        if (isRemoved) return;
        GridBuildingSystem3D.Instance?.Placement?.Unregister(this);
        isRemoved = true; IsPlaced = false;
        if (countedInShop)
        {
            ShopItemDataContainer.GetShopItem(placedObjectTypeSO.type, placedObjectTypeSO.categoryType)?.DecreaseItemCount();
            countedInShop = false;
        }
        transform.SetParent(null, true);
        placedObjectTypeSO.ReleaseObject(transform);
    }
    public bool OnTouch()
    {
        var system = GridBuildingSystem3D.Instance;

        // Declining rather than swallowing the tap is what keeps placement working: a floor tile,
        // or anything tapped while a building is already following the pointer, has to let the
        // same release reach BuildingGhost so it can drop that building.
        if (system == null || system.GhostObject == null || system.GhostObject.selectedState || IsFloor()) return false;

        objParent = transform.parent;
        system.GhostObject.SelectObject(this);
        var ui = UIGame.GetUI();
        if (ui != null) ui.placeObjectPanel.SetPlacedObjectData(this);
        return true;
    }
    internal PlacedObjectTypeSO GetPlacedObjectTypeSO() => placedObjectTypeSO;
    internal PlacedObjectTypeSO.Dir GetDir() => dir;
}
