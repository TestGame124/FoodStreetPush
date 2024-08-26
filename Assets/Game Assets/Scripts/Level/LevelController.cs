using BayatGames.SaveGameFree;
using BayatGames.SaveGameFree.Serializers;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class LevelController : MonoBehaviour
{

    private const string ZONE_SAVE_ID = "zoneData";

    public static LevelController Instance;
    [SerializeField] public Zone zone;

    private void Awake()
    {
        Instance = this;
    }
    public void Initialize()
    {
     

        //LoadData();
        zone.Initialize();



        StartCoroutine(SaveOnLoop());
    }


    IEnumerator SaveOnLoop()
    {
        while (true)
        {

            //if(!SaveController.SaveIsRequired)
            //    yield return null;
            if (isLoadingData)
                yield return null;
            yield return new WaitForSeconds(SaveController.SAVE_DELAY);



            //Saving();
            //SaveController.SaveIsRequired = false;
            //Thread saveThread = new Thread(() => Saving());
            //saveThread.Start();

            Saving();

        }

    }
    bool isLoadingData;
    public void LoadData(bool isLoading) {

        if (GameHandler.FirstTime)
            return;
        isLoadingData = true;
        //foreach(PlacedObject_Done alreadyPlaced in zone.GetAlreadyObjectsList())
        //{
        //    alreadyPlaced.DestroySelf();
        //}
        //ZoneData zoneData = SaveGame.Load<ZoneData>(ZONE_SAVE_ID);
        
        //zone.Load(zoneData);
        StartCoroutine(WaitUntilDataLoad(isLoading));
    }

    IEnumerator WaitUntilDataLoad(bool isLoading)
    {
        while (isLoading)
        {
            yield return null;
        }
        if (TutorialController.TutorialCompleted())
        {
            foreach (PlacedObject_Done alreadyPlaced in zone.GetAlreadyObjectsList())
            {
                if(alreadyPlaced.GetPlacedObjectTypeSO().categoryType == ObjectCategory.Floor)
                {
                   
                    alreadyPlaced.DestroySelf();
                }
                else
                {

                GridBuildingSystem3D.Instance.RemoveObject(alreadyPlaced.transform.position);
                }
            }
        }
        ZoneData zoneData = SaveGame.Load<ZoneData>(ZONE_SAVE_ID);

        zone.Load(zoneData);
        isLoadingData = false;
    }
    // private void Saving()
    //{

    //        SaveGame.Save(ZONE_SAVE_ID, zone.Save());

    //}
    ISaveGameSerializer serializer;
    private  void Saving()
    {
        // await Task.Run(() => SaveGame.Save(ZONE_SAVE_ID, zone.Save()));
        if(serializer == null)
            serializer = new SaveGameBinarySerializer();
        SaveGame.Save(ZONE_SAVE_ID, zone.Save());
    }

}
