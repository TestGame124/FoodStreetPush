using System.Collections.Generic;
using UnityEngine;
using LastOrders.Data;
using FoodStreet.Placement;

[CreateAssetMenu()]
public class PlacedObjectTypeSO : ScriptableObject {


    public string nameString;
    public int requiredLevel;

    public bool isUnlocked;


    public ObjectCategory categoryType;
    public ObjectType type;

    [Space]
    public int PriceAmount;
    public bool Sellable = true;
    [Space]


    public Transform prefab;
    public Transform visual;

    [Min(1)] public int width;
    [Min(1)] public int height;

    [Header("Placement")]
    public FoodStreet.Placement.PlacementProfile placementProfile;

    // Original floor assets omitted dimensions; interpret those as unit tiles without modifying assets.
    public int WidthInCells => categoryType == ObjectCategory.Floor && width == 0 ? 1 : width;
    public int HeightInCells => categoryType == ObjectCategory.Floor && height == 0 ? 1 : height;
    public Vector2Int GetFootprintSize(Dir direction) =>
        GridFootprint.Size(WidthInCells, HeightInCells, (int)direction);

    public int maxItems;
    [Header("UI Settings")]
    public Sprite previewImage;

    [Tooltip("Shown on this item's shop button. Empty: the button keeps its own art.")]
    public Sprite shopIcon;

    public void Initialize() => PlacementPools.Initialize(this);
    public Transform GetObject() => PlacementPools.Get(this, false);
    public Transform GetVisual() => PlacementPools.Get(this, true);
    public void ReleaseObject(Transform instance) => PlacementPools.Release(this, instance, false);
    public void ReleaseVisual(Transform instance) => PlacementPools.Release(this, instance, true);

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
        return GridFootprint.PivotOffset(WidthInCells, HeightInCells, (int)dir);
    }

    public List<Vector2Int> GetGridPositionList(Vector2Int offset, Dir direction)
    {
        var size = GetFootprintSize(direction);
        var result = new List<Vector2Int>(Mathf.Max(0, size.x * size.y));
        for (int x = 0; x < size.x; x++)
            for (int z = 0; z < size.y; z++)
                result.Add(offset + new Vector2Int(x, z));
        return result;
    }

    // ------------------------------------------------------------------
    // Last Orders (GDD 6.1) - the buildable's role in the day/night loop.
    //
    // Added to this existing asset rather than to a parallel type so that every
    // buildable already authored keeps working and the build system needs no
    // changes to place them. Unset values mean "not a Last Orders buildable yet",
    // which is exactly what the legacy assets are.
    //
    // Pillar 2 is the reason these live together: a table and a barricade compete
    // for the same tile, so one asset type has to answer both the economic and the
    // tactical question about any given object.
    // ------------------------------------------------------------------

    [Header("Last Orders - Category")]
    public BuildCategory buildCategory = BuildCategory.Service;

    [Header("Last Orders - Cost")]
    [Tooltip("Materials consumed. Every build costs materials AND clock-minutes (6.3).")]
    public ResourceAmount[] buildCost;

    [Tooltip("Clock-minutes to build. GDD: wall 2, table 1, turret 10, generator 15. " +
             "Every minute building is a minute not earning, and every minute earning is Heat.")]
    public float buildClockMinutes = 1f;

    [Header("Last Orders - Durability (15.4)")]
    public float maxHealth = 100f;

    [Tooltip("Output while Damaged, as a fraction. GDD says 60 to 90 percent.")]
    [Range(0f, 1f)] public float damagedEfficiency = 0.75f;

    [Tooltip("Perimeter objects can be Destroyed and leave a ghost outline. Interior objects " +
             "can only ever be Wrecked, never removed - a wrecked restaurant still looks " +
             "like the player's restaurant at dawn, which matters more than it sounds.")]
    public bool isPerimeter;

    [Tooltip("The starting stove. Indestructible: it can be Damaged but never disabled. " +
             "One flag solves the entire softlock problem (15.1).")]
    public bool isHearth;

    [Header("Last Orders - Heat (11.1)")]
    [Tooltip("Heat contributed per occupied tile, per day, simply by existing.")]
    public float heatPerTile = 0.5f;

    [Tooltip("A powered light. Adds 3 Heat each if still burning after dusk.")]
    public bool isPoweredLight;

    [Tooltip("Adds 5 Heat while running, and powers turrets and lights.")]
    public bool isGenerator;

    [Tooltip("Adds 4 Heat while playing, and changes the soundtrack. Good music is a risk.")]
    public bool isJukebox;

    [Header("Last Orders - Role")]
    [Tooltip("Seats this many customers. Table sizes are 2, 4 and 6.")]
    public int seatCapacity;

    [Tooltip("Customers enter ONLY through doors, and enemies path to doors first. " +
             "Revenue capacity and defensive integrity are the same variable, expressed " +
             "in doors (7).")]
    public bool isDoor;

    [Tooltip("Part of the perimeter shell for integrity and breach calculations.")]
    public bool isWall;

    [Tooltip("Shelters customers caught by dusk, and its contents cannot be stolen (15.6). " +
             "One of the most valuable buildables in the game.")]
    public bool isCellar;

    [Tooltip("Stores ingredients. What the Glutton beelines for.")]
    public bool isPantry;

    [Tooltip("Lets the player feed the Last Party at the door for half payment (9.4).")]
    public bool isTakeawayWindow;

    [Header("Last Orders - Defense")]
    [Tooltip("Damage per second to enemies in range. Turrets resolve combat automatically; " +
             "the player's build IS the strategy (13.2).")]
    public float defenseDps;

    public float defenseRangeTiles;

    [Tooltip("Consumes Ammo per shot and must be reloaded via the Crisis Bar.")]
    public bool consumesAmmo;

    [Header("Last Orders - Production")]
    [Tooltip("Recipes this station can cook. Stations cook automatically when staffed.")]
    public RecipeDefinition[] recipes;

    [Tooltip("Wall material, when this is a wall segment (6.4).")]
    public WallMaterialDefinition wallMaterial;

    [Header("Last Orders - Comfort")]
    [Tooltip("Multiplies the daily customer pool. Comfort ranges 0.9 to 1.2 (9.2).")]
    public float comfortMultiplier = 1f;

    [Header("Last Orders - Unlock")]
    [Tooltip("Reputation gate. Plot tiers and Blueprints are the other two gates.")]
    public int requiredReputation;

    [Tooltip("Unlocked by a Blueprint from Dispatch, a Named Night, or a faction.")]
    public bool requiresBlueprint;

    /// <summary>Total Heat this object contributes per day at rest, before anything it does.</summary>
    public float PassiveHeat => heatPerTile * Mathf.Max(1, width * height);

   
}

public enum ObjectCategory
{
    Stoves,
    Farming,
    Decoration,
    Floor,
    Props,
    Defense,
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
    Cafe = 104,

    // Defense: the shooters, bought from the shop's Defense tab and set down on the grid.
    ________Defense_________ = 200, // Should not be set to any Object
    CornShooter = 201,
    TomatoShooter = 202,
    CabbageShooter = 203,
    PlateShooter = 204,

    // Farm animals: their houses, bought from the shop's Farming tab and set down on the grid.
    ________Livestock_________ = 300, // Should not be set to any Object
    ChickenCoop = 301,
    CowShelter = 302,
}
