using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic;

public class CurrenciesController : MonoBehaviour
{
    public static CurrenciesController Instance { get; private set; }

    

    [SerializeField] CurrencyDatabaseSO currencyDatabase;

    private static CurrencyData currencyData;

    public static Action<CurrencyType, int> OnCurrencyAmountChanged;

    private static string savePath;

    //private void Awake()
    //{
    //    if (Instance == null)
    //    {
    //        Instance = this;
    //        DontDestroyOnLoad(gameObject);
    //        //Initialise();
    //    }
    //    else
    //    {
    //        Destroy(gameObject);
    //    }
    //}


    public void Initialise()
    {
        savePath = Path.Combine(Application.persistentDataPath, "currency.json");
        LoadCurrencies();

        currencyDatabase.Initialize();

    }

    // Check if user has a specified amount of currency
    public static bool HasCurrency(CurrencyType type, int amount)
    {
        return GetCurrencyAmount(type) >= amount;
    }

    // Add currency
    public static void AddCurrency(CurrencyType type, int amount)
    {
        SetCurrencyAmount(type, GetCurrencyAmount(type) + amount);
        OnCurrencyAmountChanged?.Invoke(type, GetCurrencyAmount(type));

        SaveCurrencies();
    }

    [ContextMenu("Add 500 Coins")]
    public void AddCoins500()
    {
        AddCurrency(CurrencyType.Coins, 500);
    }


    // Subtract currency
    public static bool SubtractCurrency(CurrencyType type, int amount)
    {
        if (HasCurrency(type, amount))
        {
            SetCurrencyAmount(type, GetCurrencyAmount(type) - amount);
            OnCurrencyAmountChanged?.Invoke(type, GetCurrencyAmount(type));


            SaveCurrencies();
            return true;
        }
        return false;
    }

    // Get current currency amount
    public static int GetCurrencyAmount(CurrencyType type)
    {
        switch (type)
        {
            case CurrencyType.Coins:
                return currencyData.Coins;
            case CurrencyType.Gems:
                return currencyData.Gems;
            default:
                return 0;
        }
    }

    // Set current currency amount
    private static void SetCurrencyAmount(CurrencyType type, int amount)
    {
        switch (type)
        {
            case CurrencyType.Coins:
                currencyData.Coins = amount;
                break;
            case CurrencyType.Gems:
                currencyData.Gems = amount;
                break;
        }

    }

    // Save currency data to file
    private static void SaveCurrencies()
    {
        //if (!TutorialController.TutorialCompleted())
        //    return;
        if (savePath == null)
            return;
        string json = JsonUtility.ToJson(currencyData);
        File.WriteAllText(savePath, json);
    }

    // Load currency data from file
    private static void LoadCurrencies()
    {
        if (!TutorialController.TutorialCompleted())
            return;
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            currencyData = JsonUtility.FromJson<CurrencyData>(json);
        }
        else
        {
            currencyData = new CurrencyData();
        }
    }

    // Reset currency data (for debugging or new user)
    public static void ResetCurrencies()
    {
        currencyData = new CurrencyData();
        SaveCurrencies();
    }

    public struct CurrencyData
    {
        public int Coins;
        public int Gems;
    }
}
