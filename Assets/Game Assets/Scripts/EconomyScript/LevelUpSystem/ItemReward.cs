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
    private int amount;
    public Sprite PreviewImage { get { return previewImage; } set { previewImage = value; } }

    // An item reward is an unlock, not a quantity, so this stays 0 and the
    // level-up UI shows "Unlocked" instead of a number.
    public int Amount { get { return amount; } set { amount = value; } }

    public void GiveReward()
    {
        shopItem.Unlock();
    }
}