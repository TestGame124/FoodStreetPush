using UnityEngine;

[System.Serializable]
public class CurrencyReward : IReward
{
    public int level;
    public CurrencyType type;
    public int amount;
    public int Amount { get { return amount; } set { amount = value; } }
    private Sprite previewImage;
    public Sprite PreviewImage { get { return previewImage; } set { previewImage = value; } }

    public CurrencyReward (CurrencyType type, int amount)
    {
        this.type = type;
        this.amount = amount;
        previewImage = CurrencyDatabaseSO.GetCurrency(type).image;
    }


    public void GiveReward()
    {
        CurrenciesController.AddCurrency(type,amount);
    }
}