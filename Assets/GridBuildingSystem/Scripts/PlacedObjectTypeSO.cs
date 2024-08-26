using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

[CreateAssetMenu()]
public class PlacedObjectTypeSO : ScriptableObject {


    public string nameString;
    public int requiredLevel;

    public bool isUnlocked;

    private ObjectPool<Transform> objectPool;
    private ObjectPool<Transform> visualPool;

    public ObjectCategory categoryType;
    public ObjectType type;

    [Space]
    public int PriceAmount;
    public bool Sellable = true;
    [Space]


    public Transform prefab;
    public Transform visual;

    public int width;
    public int height;

    public int maxItems;
    [Header("UI Settings")]
    public Sprite previewImage;

    public void Initialize()
    {
        int poolCapacity = maxItems == 0 ?
            type == ObjectType.Floor1 ? 100 : 20 
            : maxItems;  

        objectPool = new ObjectPool<Transform>(() => { return Instantiate(prefab); },
            placedObject => { placedObject.gameObject.SetActive(true); }
            ,placedObject => { placedObject.gameObject.SetActive(false); }
            ,placedObject => { Destroy(placedObject.gameObject); },
            false, poolCapacity);
        
        visualPool = new ObjectPool<Transform>(() => { return Instantiate(visual); },
            placedObject => { placedObject.gameObject.SetActive(true); }
            ,placedObject => { placedObject.gameObject.SetActive(false); }
            ,placedObject => { Destroy(placedObject.gameObject); },
            false, poolCapacity);
    }

    public Transform GetObject() { 
        return objectPool.Get();
    }
    public Transform GetVisual() { 
        return visualPool.Get();
    }
    public void ReleaseObject(Transform placedObject) { objectPool.Release(placedObject); }
    public void ReleaseVisual(Transform placedObject) { visualPool.Release(placedObject); }

    public bool CanPurchase()
    {
        
        if(PriceAmount > 0 )
        {
            return CurrenciesController.GetCurrencyAmount(CurrencyType.Coins) >= PriceAmount;
        }
        return true;
    }
    public static Dir GetNextDir(Dir dir) {
        switch (dir) {
            default:
            case Dir.Down:      return Dir.Left;
            case Dir.Left:      return Dir.Up;
            case Dir.Up:        return Dir.Right;
            case Dir.Right:     return Dir.Down;
        }
    }

    public enum Dir {
        Down,
        Left,
        Up,
        Right,
    }
    public int GetRotationAngle(Dir dir) {
        switch (dir) {
            default:
            case Dir.Down:  return 0;
            case Dir.Left:  return 90;
            case Dir.Up:    return 180;
            case Dir.Right: return 270;
        }
    }

    public Dir SetRotationAngle(int angle)
    {
        switch (angle)
        {
            default:
            case 0: return Dir.Down;
            case 90: return Dir.Left;
            case 180: return Dir.Up;
            case 270: return Dir.Right;
        }
    }

    public Vector2Int GetRotationOffset(Dir dir) {
        switch (dir) {
            default:
            case Dir.Down:  return new Vector2Int(0, 0);
            case Dir.Left:  return new Vector2Int(0, width);
            case Dir.Up:    return new Vector2Int(width, height);
            case Dir.Right: return new Vector2Int(height, 0);
        }
    }

    public List<Vector2Int> GetGridPositionList(Vector2Int offset, Dir dir) {
        List<Vector2Int> gridPositionList = new List<Vector2Int>();
        switch (dir) {
            default:
            case Dir.Down:
            case Dir.Up:
                for (int x = 0; x < width; x++) {
                    for (int y = 0; y < height; y++) {
                        gridPositionList.Add(offset + new Vector2Int(x, y));
                    }
                }
                break;
            case Dir.Left:
            case Dir.Right:
                for (int x = 0; x < height; x++) {
                    for (int y = 0; y < width; y++) {
                        gridPositionList.Add(offset + new Vector2Int(x, y));
                    }
                }
                break;
        }
        return gridPositionList;
    }

   
}

public enum ObjectCategory
{
    Stoves,
    Farming,
    Decoration,
    Floor,
    Props,
}
public enum ObjectType
{
    Inventory = -1,
    BreadStove,
    SoupStove,
    
    // Farming 
    PlantPot,

    // Decoration
    Table1,
    BigWall,
    Wall,
    Door = 6,
    // Floors
    Floor1 = 50,
    RemoveFloor = 51,
    WoodenFloor = 52,

    // Props
    ________Props_________ = 100, // Should not be set to any Object
    Barrel = 101,
    Well = 102,
    Letterbox = 103,
    Cafe = 104
}