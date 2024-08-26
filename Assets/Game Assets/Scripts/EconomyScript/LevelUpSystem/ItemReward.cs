using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

[System.Serializable]
public class ItemReward : IReward
{
    public ShopItem shopItem;

    public ItemReward(ShopItem shopItem)
    {
        this.shopItem = shopItem;
        previewImage = shopItem.itemData.previewImage;
    }

    private Sprite previewImage;
    public Sprite PreviewImage { get { return previewImage; } set { previewImage = value; } }
    public int Amount { get { return 0; } set { Amount = 0; } }

    public void GiveReward()
    {
        shopItem.Unlock();
    }
}