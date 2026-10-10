using DG.Tweening;
using UnityEngine;



[CreateAssetMenu(fileName = "ItemData" ,menuName = "Items/Item")]
public class ItemDataSO : ScriptableObject
{
    public string Name;

    public ItemType Type;

    [Range(0,9999)]
    public int coinsReward;
    [Range(0, 99)]
    public int starsReward;

    public bool canBePlanted;
    public int totalTimeInSeconds;

    

    [Space]
    public Sprite itemImage;

    [Space]
    [Tooltip("The crop in a plant pot as it grows, seedling first. The growing time is shared evenly " +
             "between all but the last, which is the ripe crop, shown once it can be harvested. " +
             "Built by Last Orders / Farming / Build Crop Sprites.")]
    public Sprite[] growStages;

    [Tooltip("How the crop looks in a pot: a plant on each of several spots, each stage in a few variants. " +
             "Shown instead of the grow stages when set. Built by Last Orders / Farming / Build Crop Sprites.")]
    public CropLook cropLook;

    /// <summary>How many grow stages the crop has, from its look or else its grow stages.</summary>
    public int GrowStageCount => cropLook != null && cropLook.StageCount > 0 ? cropLook.StageCount : growStages?.Length ?? 0;

    /// <summary>
    /// Adds <paramref name="item"/> to <paramref name="inventory"/> <paramref name="amount"/> times,
    /// throwing a pickup out from <paramref name="position"/> for each; a batch fans out together.
    /// </summary>
    public void GenerateItem(ItemReceiver receiverPrefab,Item item,Vector3 position, InventorySystem inventory, int amount = 1)
    {
        for (int i = 0; i < amount; i++)
        {
            ItemReceiver itemObj = Instantiate(receiverPrefab, position, Quaternion.identity);
            itemObj.Initialize(item, inventory, i, amount);
        }
    }

}
public enum ItemType
{
    Flour = 0,
    Corn = 1,
    Tomato = 2,
    Cabbage = 3,

    // Salad ingredients
    Lettuce = 4,
    Carrot = 5,
    Cheese = 6,
    CannedBeans = 7,
    Chicken = 8,

    // Recipies
    Bread = 10,
    CornBread = 11,

    TomatoSoup = 12,

    // Salad station
    GardenSalad = 13,
    SweetcornSalad = 14,
    CarrotSlaw = 15,
    TomatoCheeseSalad = 16,
    BeanSalad = 17,
    ChickenSalad = 18,

    // Farmyard produce: the chicken coop lays eggs, the cow shelter gives milk
    Egg = 19,
    Milk = 20,

    // Dishes made with them
    CustardTart = 21,
    CornChowder = 22,

    // What the griddle and the grill need that nothing grows or keeps yet
    Potato = 23,
    Onion = 24,
    Fish = 25,

    // Griddle station
    FriedEggs = 26,
    HashBrowns = 27,
    FrenchToast = 28,

    // Grill station
    GrilledCorn = 29,
    ChickenSkewers = 30,
    GrilledFish = 31,
}

