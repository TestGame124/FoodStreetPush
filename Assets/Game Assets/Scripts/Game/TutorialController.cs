using BayatGames.SaveGameFree;
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TutorialController
{
    private const string TutorialSave = "tutorialSave";

    public static TutorialStage currentStage = TutorialStage.FirstStage;

    public static Action<TutorialStage> onStageActivation;

    public static Action OnPickingFirstPlant;
    public static Action OnPickingSecondPlant;

    public static Action OnPlanting;
    public static Action OnTappingFirstCookingStation;

    public static Action OnCookButtonClick;
    public static Action OnOrderButtonClick;

    public static Action ObjectPlacingStageEvent;

    public static Action OnRestaurantNameSubmit;

    public static void Initialize()
    {
        Load();
    }
    public static void EnableTutorial(TutorialStage stage)
    {
        currentStage = stage;
        Save();
        onStageActivation?.Invoke(stage);
    }

    public static bool TutorialCompleted()
    {
        return currentStage == TutorialStage.Done;
    }
    public static void Save()
    {
        
        TutorialStageSaveData data = new TutorialStageSaveData();
        data.TutorialStage = currentStage;
        data.numOfTimesEventWillRun = LevelTutorialBehaviour.numOfTimesEventWillRun;
        SaveGame.Save(TutorialSave, data);

    }

    private static void Load()
    {
        TutorialStageSaveData data = SaveGame.Load<TutorialStageSaveData>(TutorialSave);



        if (data == null)
        {
            Save();
            return;
        }else if(data.TutorialStage != TutorialStage.Done)
        {
            return;
        }
        currentStage = data.TutorialStage;
        LevelTutorialBehaviour.numOfTimesEventWillRun = data.numOfTimesEventWillRun;
    }
}

[Serializable]
public class TutorialStageSaveData
{
    public TutorialStage TutorialStage;
    public int numOfTimesEventWillRun = 0;

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
