using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Player level and experience.
///
/// The numbers live in memory for the length of the session; nothing is
/// persisted, so every launch starts at level one.
/// </summary>
public class LevelUpSystem : MonoBehaviour
{
    public static LevelUpSystem Instance { get; private set; }

    public static Action<int, float, float> OnXpChanged;
    public static Action<int> OnLevelUp;

    public List<CurrencyReward> rewardConfigs;

    private static Dictionary<int, CurrencyReward> rewardConfigDictionary;
    private static RewardManager rewardManager;

    private const int StartingXpToNextLevel = 3;

    private static int level = 1;
    private static int currentXp;
    private static int xpToNextLevel = StartingXpToNextLevel;

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

        // Let the UI catch up - deliberately without running the level-up
        // check, which would replay the level-up popup on every boot.
        Broadcast();
    }

    public static int GetLevel() => level;

    public static int GetCurrentXP() => currentXp;

    public static int GetXPToNextLevel() => xpToNextLevel;

    public static void AddXP(int amount)
    {
        if (amount <= 0)
            return;

        currentXp += amount;
        CheckLevelUp();

        Broadcast();
    }

    public static void CheckLevelUp()
    {
        while (currentXp >= xpToNextLevel)
        {
            currentXp -= xpToNextLevel;
            level++;

            if (UIGame.GetUI() != null)
            {
                UIGame.GetUI().levelUpUi.gameObject.SetActive(true);
                UIGame.GetUI().levelUpUi.GetComponent<CanvasGroup>().DOFade(1, 0.5f).From(0);
            }

            ApplyRewards(level);
            xpToNextLevel = CalculateXPToNextLevel(level);
        }
    }

    public static void GetReward()
    {
        OnLevelUp?.Invoke(GetLevel());
    }

    public static void OnLevelUpFunc()
    {
        OnLevelUp?.Invoke(GetLevel());
    }

    private static int CalculateXPToNextLevel(int reachedLevel)
    {
        return StartingXpToNextLevel + (reachedLevel - 1) * 2;
    }

    public static void ApplyRewards(int level)
    {
        CurrencyReward reward = new CurrencyReward(CurrencyType.Coins, 50 * level);

        if (UIGame.GetUI() != null)
            UIGame.GetUI().levelUpUi.OnLevelUp(reward);

        rewardManager.AddReward(reward);
        rewardManager.GiveAllRewards();
    }

    public static void AddReward(ShopItem shopItem)
    {
        ItemReward reward = new ItemReward(shopItem);

        if (UIGame.GetUI() != null)
            UIGame.GetUI().levelUpUi.OnLevelUp(reward);

        rewardManager.AddReward(reward);
    }

    private static void Broadcast()
    {
        OnXpChanged?.Invoke(level, currentXp, xpToNextLevel);
    }

    public static void ResetXpData()
    {
        level = 1;
        currentXp = 0;
        xpToNextLevel = StartingXpToNextLevel;

        Broadcast();
        OnLevelUp?.Invoke(level);
    }
}
