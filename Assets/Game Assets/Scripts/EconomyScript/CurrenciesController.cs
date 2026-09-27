using System;
using UnityEngine;

/// <summary>
/// Coins and gems.
///
/// Balances live in memory for the length of the session and are not persisted -
/// every launch starts from zero. Every change still goes through here so the UI
/// is told about it exactly once.
/// </summary>
public class CurrenciesController : MonoBehaviour
{
    public static CurrenciesController Instance { get; private set; }

    [SerializeField] CurrencyDatabaseSO currencyDatabase;

    public static Action<CurrencyType, int> OnCurrencyAmountChanged;

    private static int coins;
    private static int gems;

    public void Initialise()
    {
        Instance = this;
        currencyDatabase.Initialize();

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

        Broadcast(type);
    }

    public static bool SubtractCurrency(CurrencyType type, int amount)
    {
        if (amount <= 0)
            return true;
        if (!HasCurrency(type, amount))
            return false;

        SetCurrencyAmount(type, GetCurrencyAmount(type) - amount);

        Broadcast(type);
        return true;
    }

    public static int GetCurrencyAmount(CurrencyType type)
    {
        switch (type)
        {
            case CurrencyType.Coins: return coins;
            case CurrencyType.Gems: return gems;
            default: return 0;
        }
    }

    private static void SetCurrencyAmount(CurrencyType type, int amount)
    {
        amount = Mathf.Max(0, amount);

        switch (type)
        {
            case CurrencyType.Coins:
                coins = amount;
                break;
            case CurrencyType.Gems:
                gems = amount;
                break;
        }
    }

    private static void Broadcast(CurrencyType type)
    {
        OnCurrencyAmountChanged?.Invoke(type, GetCurrencyAmount(type));
    }

    public static void ResetCurrencies()
    {
        coins = 0;
        gems = 0;

        Broadcast(CurrencyType.Coins);
        Broadcast(CurrencyType.Gems);
    }

    [ContextMenu("Add 500 Coins")]
    public void AddCoins500()
    {
        AddCurrency(CurrencyType.Coins, 500);
    }
}
