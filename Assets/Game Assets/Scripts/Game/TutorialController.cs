using System;
using FoodStreet.Saving;
using UnityEngine;

/// <summary>
/// Where the player is in the tutorial.
///
/// Stage lives in the save payload with everything else, so it can no longer
/// disagree with the world it is meant to describe - the old system wrote it to
/// its own file, which meant a crash between the two writes could put a
/// finished tutorial next to a world that had never been built.
/// </summary>
public class TutorialController
{
    public static Action<TutorialStage> onStageActivation;

    public static Action OnPickingFirstPlant;
    public static Action OnPickingSecondPlant;

    public static Action OnPlanting;
    public static Action OnTappingFirstCookingStation;

    public static Action OnCookButtonClick;
    public static Action OnOrderButtonClick;

    public static Action ObjectPlacingStageEvent;

    public static Action OnRestaurantNameSubmit;

    /// <summary>
    /// Reads through to the save file so there is one copy of this value, not a
    /// static mirror that can drift out of step with it.
    /// </summary>
    public static TutorialStage currentStage
    {
        get
        {
            return SaveSystem.IsReady
                ? SaveSystem.Data.progress.tutorialStage
                : TutorialStage.FirstStage;
        }
        set
        {
            if (!SaveSystem.IsReady)
            {
                Debug.LogError("[Tutorial] Stage set before SaveSystem.Initialize.");
                return;
            }

            if (SaveSystem.Data.progress.tutorialStage == value)
                return;

            SaveSystem.Data.progress.tutorialStage = value;
            SaveSystem.MarkDirty();
        }
    }

    /// <summary>
    /// Nothing to load any more - stage and step counter are restored with the
    /// save file itself. Kept as the explicit point where the tutorial is
    /// considered live, and to catch a bad boot order early.
    /// </summary>
    public static void Initialize()
    {
        if (!SaveSystem.IsReady)
            Debug.LogError("[Tutorial] Initialize called before SaveSystem.Initialize.");
    }

    public static void EnableTutorial(TutorialStage stage)
    {
        currentStage = stage;
        onStageActivation?.Invoke(stage);
    }

    public static bool TutorialCompleted()
    {
        return currentStage == TutorialStage.Done;
    }

    /// <summary>Kept for existing call sites; the values write themselves now.</summary>
    public static void Save()
    {
        SaveSystem.MarkDirty();
    }
}

public enum TutorialStage
{
    None,
    FirstStage,
    PlantingStage,
    BreadCookingStage,
    OrdersStage,
    GuideStage,
    PLacingObjectStage,
    NamingRestaurantStage,
    EighthStage,
    ninthStage,

    Done = 999
}
