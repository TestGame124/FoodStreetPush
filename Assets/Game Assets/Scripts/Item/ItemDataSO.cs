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
    public Mesh[] plantGrowMeshes;
    public Material[] plantMat;

    public void GenerateItem(ItemReceiver receiverPrefab,Item item,Vector3 position, InventorySystem inventory, int amount = 1)
    {
        for (int i = 0; i < amount; i++)
        {
            ItemReceiver itemObj = Instantiate(receiverPrefab, position, Quaternion.identity);
            itemObj.transform.DOScale(1, 0.4f).From(0);
            itemObj.transform.DOJump(position, 3, 1, 1).SetEase(Ease.OutBack);
            itemObj.Initialize(item, inventory);
        }
    }

}
public enum ItemType
{
    Flour = 0,
    Corn = 1,
    Tomato = 2,
    Cabbage = 3,

    // Recipies
    Bread = 10,
    CornBread = 11,

    TomatoSoup = 12,
}

