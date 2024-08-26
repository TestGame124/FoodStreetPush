using BayatGames.SaveGameFree;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Level : MonoBehaviour
{

    public static Action OnLevelLoaded;
    public Zone zone;


    private const string ZONE_SAVE_ID = "zoneData";

    private void Awake()
    {
        OnLevelLoaded?.Invoke();
    }
    public void Initialize()
    {
        
        zone.Initialize();
    }

    IEnumerator SaveOnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(SaveController.SAVE_DELAY);

            Debug.Log("Zone Game Saved !!");

            Saving();

            //Thread saveThread = new Thread(() => Saving());
            //saveThread.Start();

        }

    }

    public void LoadData()
    {
        if (!TutorialController.TutorialCompleted())
            return;
        ZoneData zoneData = SaveGame.Load<ZoneData>(ZONE_SAVE_ID);

        zone.Load(zoneData);
    }

    private void Saving()
    {
        //lock (SaveController.fileLock)
        //{
        Debug.Log("Saving!");
       
        SaveGame.Save(ZONE_SAVE_ID, zone.Save());
        //}
    }
}
