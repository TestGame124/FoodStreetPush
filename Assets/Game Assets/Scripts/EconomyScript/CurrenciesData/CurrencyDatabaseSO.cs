using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "CurrencyDatabase",menuName = "Currency/Database")]
public class CurrencyDatabaseSO : ScriptableObject
{
    [SerializeField] CurrencyInfo[] currencies;
    private static Dictionary<CurrencyType,CurrencyInfo> currencyDict = new();


    public void Initialize()
    {
        RegisterCurrencies();
    }
    
    private void RegisterCurrencies()
    {
        foreach(CurrencyInfo currency in currencies) {

            if (!currencyDict.ContainsKey(currency.type))
            {
                currencyDict.Add(currency.type, currency);
            }
        
        }
    }

    public static CurrencyInfo GetCurrency(CurrencyType type)
    {
        return currencyDict[type];
    }

}

[System.Serializable]
public class CurrencyInfo
{
    
    public Sprite image;
    public CurrencyType type;
}



public enum CurrencyType
{
    Coins,
    Gems
}


