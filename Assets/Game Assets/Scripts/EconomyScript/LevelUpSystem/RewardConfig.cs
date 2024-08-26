using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RewardConfig", menuName = "Rewards/RewardConfig")]
public class RewardConfig : ScriptableObject
{
   
    public List<CurrencyReward> currencyRewards;
    //public List<ItemReward> itemRewards;

    private void OnValidate()
    {
        for (int i = 0; i < currencyRewards.Count; i++)
        {
            currencyRewards[i].level = i + 2;
        }
    }

}
