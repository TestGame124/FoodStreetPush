using System;
using System.Collections.Generic;
using DG.Tweening;
using FoodStreet.Saving;
using UnityEngine;

/// <summary>
/// Player level and experience.
///
/// The numbers live in the encrypted save payload - level gates which shop
/// items exist, so it is worth as much to a cheater as the coin balance is.
/// </summary>
public class LevelUpSystem : MonoBehaviour
{
    public static LevelUpSystem Instance { get; private set; }

    public static Action<int, float, float> OnXpChanged;
    public static Action<int> OnLevelUp;

    public List<CurrencyReward> rewardConfigs;

    private static Dictionary<int, CurrencyReward> rewardConfigDictionary;
    private static RewardManager rewardManager;

    private static ProgressSaveData Progress
    {
        get
        {
            if (!SaveSystem.IsReady)
            {
                Debug.LogError("[LevelUp] Progress read before SaveSystem.Initialize.");
                return null;
            }
            return SaveSystem.Data.progress;
        }
    }

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

        // The save file already carries level and XP. All that is left is to
        // let the UI catch up - deliberately without running the level-up
        // check, which would replay the level-up popup on every boot.
        Broadcast();
    }

    public static int GetLevel()
    {
        ProgressSaveData progress = Progress;
        return progress == null ? 1 : progress.level;
    }

    public static int GetCurrentXP()
    {
        ProgressSaveData progress = Progress;
        return progress == null ? 0 : progress.currentXp;
    }

    public static int GetXPToNextLevel()
    {
        ProgressSaveData progress = Progress;
        return progress == null ? 3 : progress.xpToNextLevel;
    }

    public static void AddXP(int amount)
    {
        ProgressSaveData progress = Progress;
        if (progress == null || amount <= 0)
            return;

        progress.currentXp += amount;
        CheckLevelUp();

        SaveSystem.MarkDirty();
        Broadcast();
    }

    public static void CheckLevelUp()
    {
        ProgressSaveData progress = Progress;
        if (progress == null)
            return;

        while (progress.currentXp >= progress.xpToNextLevel)
        {
            progress.currentXp -= progress.xpToNextLevel;
            progress.level++;

            if (TutorialController.TutorialCompleted() && UIGame.GetUI() != null)
            {
                UIGame.GetUI().levelUpUi.gameObject.SetActive(true);
                UIGame.GetUI().levelUpUi.GetComponent<CanvasGroup>().DOFade(1, 0.5f).From(0);
            }

            ApplyRewards(progress.level);
            progress.xpToNextLevel = CalculateXPToNextLevel(progress.level);
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

    private static int CalculateXPToNextLevel(int level)
    {
        return 3 + (level - 1) * 2;
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
        ProgressSaveData progress = Progress;
        if (progress == null)
            return;

        OnXpChanged?.Invoke(progress.level, progress.currentXp, progress.xpToNextLevel);
    }

    public static void ResetXpData()
    {
        ProgressSaveData progress = Progress;
        if (progress == null)
            return;

        progress.level = 1;
        progress.currentXp = 0;
        progress.xpToNextLevel = 3;

        SaveSystem.MarkDirty();
        Broadcast();
        OnLevelUp?.Invoke(progress.level);
    }
}
