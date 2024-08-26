using BayatGames.SaveGameFree;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class LevelUpSystem : MonoBehaviour
{

    public static LevelUpSystem Instance { get; private set; }

    private const string xpSaveID = "XpData";

    private static ExperienceData XpController;
    private static string savePath;


    public static Action<int,float,float> OnXpChanged;
    public static Action<int> OnLevelUp;

    public List<CurrencyReward> rewardConfigs;

    private static Dictionary<int, CurrencyReward> rewardConfigDictionary;
    private static RewardManager rewardManager;

    void Awake()
    {
        rewardManager = new RewardManager();
        rewardConfigDictionary = new Dictionary<int, CurrencyReward>();

        foreach (var config in rewardConfigs)
        {
            rewardConfigDictionary[config.level] = config;
        }
    }

    public void Initialize()
    {
        Instance = this;
        savePath = Path.Combine(Application.persistentDataPath, "level.json");
        LoadXpData();

        //if (XpController.Level == 0)
        //{
        //    InitializePlayerData();
        //}

        AddXP(0);

    }

    private static void InitializePlayerData()
    {
        XpController = new ExperienceData();
        XpController.Level = 1;
        XpController.CurrentXP = 0;
        XpController.XPToNextLevel = 3;
        OnXpChanged?.Invoke(XpController.Level,XpController.CurrentXP,XpController.XPToNextLevel);
        OnLevelUp?.Invoke(XpController.Level);

        SaveXpData();
    }

    public static int GetLevel()
    {
        return XpController.Level;
    }

    public static int GetCurrentXP()
    {
        return XpController.CurrentXP;
    }

    public static int GetXPToNextLevel()
    {
        return XpController.XPToNextLevel;
    }

    public static void AddXP(int amount)
    {
        XpController.CurrentXP += amount;
       
            CheckLevelUp();

            OnXpChanged?.Invoke(XpController.Level, XpController.CurrentXP, XpController.XPToNextLevel);
        SaveXpData();
        
    }

    public static void CheckLevelUp()
    {
        while (XpController.CurrentXP >= XpController.XPToNextLevel)
        {
            XpController.CurrentXP -= XpController.XPToNextLevel;
            XpController.Level++;
            if (TutorialController.TutorialCompleted())
            {
                UIGame.GetUI().levelUpUi.gameObject.SetActive(true);
                UIGame.GetUI().levelUpUi.GetComponent<CanvasGroup>().DOFade(1, 0.5f).From(0);
            }
            ApplyRewards(XpController.Level);
            //OnLevelUp?.Invoke(XpController.Level);
            XpController.XPToNextLevel = CalculateXPToNextLevel(XpController.Level);
        }
       
    }

    public static void GetReward()
    {
        OnLevelUp?.Invoke(XpController.Level);
    }

    public static void OnLevelUpFunc()
    {
        OnLevelUp?.Invoke(XpController.Level);

    }

    private static int CalculateXPToNextLevel(int level)
    {
        return 3 + (level - 1) * 2; // Example formula: increase XP requirement with level
    }
    public static void ApplyRewards(int level)
    {
        //if (rewardConfigDictionary.TryGetValue(level, out var config))
        //{
        //foreach (var currencyReward in config)
        //{
        //rewardManager.AddReward(config);
        CurrencyReward reward = new CurrencyReward(CurrencyType.Coins, 50 * level);
        UIGame.GetUI().levelUpUi.OnLevelUp(reward);
        rewardManager.AddReward(reward);
            //}
            
            
        //}
            rewardManager.GiveAllRewards();
    }

    public static void AddReward(ShopItem shopItem)
    {
        Debug.Log($"Item Reward Added : {shopItem}");

        ItemReward reward = new ItemReward(shopItem);
        UIGame.GetUI().levelUpUi.OnLevelUp(reward);
        rewardManager.AddReward(reward);
    }

    private static void SaveXpData()
    {
        //if (!TutorialController.TutorialCompleted())
            //return;
        SaveGame.Save(xpSaveID, XpController);

    }

    private void LoadXpData()
    {
        if (!TutorialController.TutorialCompleted())
        {
            InitializePlayerData();
            return;
        }
        XpController = SaveGame.Load<ExperienceData>(xpSaveID);
        if(XpController == null)
        {
            InitializePlayerData();
        }
     
    }

    public static void ResetXpData()
    {
        InitializePlayerData();
        SaveXpData();
    }
}
