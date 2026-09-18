using System;
using FoodStreet.Saving;
using UnityEngine;

/// <summary>
/// Coins and gems.
///
/// Balances live inside the encrypted save payload rather than in a plaintext
/// json file next to the game, and every change goes through here so nothing
/// can move a balance without the save being marked dirty.
/// </summary>
public class CurrenciesController : MonoBehaviour
{
    public static CurrenciesController Instance { get; private set; }

    [SerializeField] CurrencyDatabaseSO currencyDatabase;

    public static Action<CurrencyType, int> OnCurrencyAmountChanged;

    private static EconomySaveData Economy
    {
        get
        {
            if (!SaveSystem.IsReady)
            {
                Debug.LogError("[Currencies] Balance read before SaveSystem.Initialize.");
                return null;
            }
            return SaveSystem.Data.economy;
        }
    }

    public void Initialise()
    {
        Instance = this;
        currencyDatabase.Initialize();

        // Balances were restored with the save file; just let the UI catch up.
        Broadcast(CurrencyType.Coins);
        Broadcast(CurrencyType.Gems);
    }

    public static bool HasCurrency(CurrencyType type, int amount)
    {
        return GetCurrencyAmount(type) >= amount;
    }

    public static void AddCurrency(CurrencyType type, int amount)
    {
        if (amount <= 0)
            return;

        // A balance that has overflowed reads as a negative number of coins,
        // which every "can I afford this" check then fails. Clamp instead.
        long total = (long)GetCurrencyAmount(type) + amount;
        SetCurrencyAmount(type, (int)Math.Min(total, int.MaxValue));

        SaveSystem.MarkDirty();
        Broadcast(type);
    }

    public static bool SubtractCurrency(CurrencyType type, int amount)
    {
        if (amount <= 0)
            return true;
        if (!HasCurrency(type, amount))
            return false;

        SetCurrencyAmount(type, GetCurrencyAmount(type) - amount);

        SaveSystem.MarkDirty();
        Broadcast(type);
        return true;
    }

    public static int GetCurrencyAmount(CurrencyType type)
    {
        EconomySaveData economy = Economy;
        if (economy == null)
            return 0;

        switch (type)
        {
            case CurrencyType.Coins: return economy.coins;
            case CurrencyType.Gems: return economy.gems;
            default: return 0;
        }
    }

    private static void SetCurrencyAmount(CurrencyType type, int amount)
    {
        EconomySaveData economy = Economy;
        if (economy == null)
            return;

        amount = Mathf.Max(0, amount);

        switch (type)
        {
            case CurrencyType.Coins:
                economy.coins = amount;
                break;
            case CurrencyType.Gems:
                economy.gems = amount;
                break;
        }
    }

    private static void Broadcast(CurrencyType type)
    {
        OnCurrencyAmountChanged?.Invoke(type, GetCurrencyAmount(type));
    }

    public static void ResetCurrencies()
    {
        EconomySaveData economy = Economy;
        if (economy == null)
            return;

        economy.coins = 0;
        economy.gems = 0;

        SaveSystem.MarkDirty();
        Broadcast(CurrencyType.Coins);
        Broadcast(CurrencyType.Gems);
    }

    [ContextMenu("Add 500 Coins")]
    public void AddCoins500()
    {
        AddCurrency(CurrencyType.Coins, 500);
    }
}
