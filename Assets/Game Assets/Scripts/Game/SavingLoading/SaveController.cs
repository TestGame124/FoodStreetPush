
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using BayatGames.SaveGameFree;
using System.Collections;
using DG.Tweening;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using BayatGames.SaveGameFree.Serializers;

#region Old save FIle
public class SaveController : MonoBehaviour
{
    public readonly static string persistentDataPath = Application.persistentDataPath;
    private readonly static string cookSaveFilePath = Application.persistentDataPath + "/cookRecipes.json";

    public const string PlacedObjectDataID = "placedObjectList";

    public static bool SaveIsRequired;

    public static int uID;

    public const int SAVE_DELAY = 2;

    public static readonly object fileLock = new object();

    static ISaveGameSerializer serializer;
    public static void SavePlacedObjects(List<PlacedObjectData> placedObjectDataList)
    {
        //lock (fileLock)
        //{
        if (serializer == null)
            serializer = new SaveGameBinarySerializer();
        PlacedObjectDataListWrapper placedObj = new PlacedObjectDataListWrapper { placedObjectDataList = placedObjectDataList };

            SaveGame.Save(PlacedObjectDataID, placedObj,serializer);
        //}
    }
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Saving/Clear Data")]
    public static void RemoveAllSave()
    {
        PlayerPrefs.DeleteAll();
        SaveGame.Clear();
    }
#endif

    public static bool isSavingPlacedObject;
   
    //static List<PlacedObjectData> placedObjectDataList;
    public static void SavePlacedObject(PlacedObjectData placedObjectData)
    {
        //if (!TutorialController.TutorialCompleted())
        //{
        //    SaveGame.Delete(PlacedObjectDataID);
        //}

        //if(placedObjectDataList.Count <= 0)
        //    placedObjectDataList = LoadPlacedObjects();
        List<PlacedObjectData> placedObjectDataList = LoadPlacedObjects();

        //List<PlacedObjectData> placedObjectDataList  = LoadPlacedObjects();

        //bool alreadExist = placedObjectDataList.Any(x => x.position.x == placedObjectData.position.x
        //&& x.position.y == placedObjectData.position.y
        //&& x.position.z == placedObjectData.position.z && x.objectType == placedObjectData.objectType);
        bool alreadyExist = false;
        for (int i = 0; i < placedObjectDataList.Count; i++)
        {
            var existingObject = placedObjectDataList[i];

            if (existingObject.position.x == placedObjectData.position.x &&
                existingObject.position.y == placedObjectData.position.y &&
                existingObject.position.z == placedObjectData.position.z &&
                existingObject.objectType == placedObjectData.objectType)
            {
                alreadyExist = true;
                break;
            }
        }
        if (!alreadyExist)
        {

            placedObjectDataList.Add(placedObjectData);
            SavePlacedObjects(placedObjectDataList);
        }
        


    }
    static List<PlacedObjectData> placedObjectDataList = new();
    public static void BatchSavePlacedObjects(List<PlacedObjectData> placedObjectsBatch, List<PlacedObjectData> removedObjectsBatch)
    {
       
        if(placedObjectDataList.Count <= 0)
        {
        placedObjectDataList = LoadPlacedObjects();
        }

        foreach (var removedObjectData in removedObjectsBatch)
        {
            for (int i = 0; i < placedObjectDataList.Count; i++)
            {
                var existingObject = placedObjectDataList[i];

                if (existingObject.position.x == removedObjectData.position.x &&
                    existingObject.position.y == removedObjectData.position.y &&
                    existingObject.position.z == removedObjectData.position.z &&
                    existingObject.objectType == removedObjectData.objectType)
                {
                    placedObjectDataList.RemoveAt(i);
                    break; 
                }
            }
        }

        foreach (var placedObjectData in placedObjectsBatch)
        {
            bool alreadyExist = false;
            for (int i = 0; i < placedObjectDataList.Count; i++)
            {
                var existingObject = placedObjectDataList[i];

                if (existingObject.position.x == placedObjectData.position.x &&
                    existingObject.position.y == placedObjectData.position.y &&
                    existingObject.position.z == placedObjectData.position.z &&
                    existingObject.objectType == placedObjectData.objectType)
                {
                    alreadyExist = true;
                    break;
                }
            }

            if (!alreadyExist)
            {
                placedObjectDataList.Add(placedObjectData);
            }
        }


        SavePlacedObjects(placedObjectDataList);
    }


    public static void RemoveObject(PlacedObjectData placedObjectData, Vector3 oldPos)
    {
        //lock (fileLock)
        //{
        List<PlacedObjectData> placedObjectDataList = LoadPlacedObjects();
        //if (placedObjectDataList.Count <= 0)
        //{
        //    placedObjectDataList = LoadPlacedObjects();
        //}

        bool objectFound = false;
            for (int i = 0; i < placedObjectDataList.Count; i++)
            {
            Debug.Log($"OBject Removed Save X : {placedObjectDataList[i].position.x} , Z : {placedObjectDataList[i].position.z}");

            if (placedObjectDataList[i].objectType == placedObjectData.objectType &&
                    placedObjectDataList[i].position.x == oldPos.x &&
                    placedObjectDataList[i].position.z == oldPos.z)
                {
                    placedObjectDataList.RemoveAt(i);
                    objectFound = true;
                    break;
                }
            }
            if (objectFound)
            {
                SavePlacedObjects(placedObjectDataList);
            }

        //}
    }

    


    public static List<PlacedObjectData> LoadPlacedObjects()
    {

        //PlacedObjectDataListWrapper wrapper = SaveObject<PlacedObjectDataListWrapper>(saveFilePath);
        if (serializer == null)
            serializer = new SaveGameBinarySerializer();
        PlacedObjectDataListWrapper wrapper = SaveGame.Load<PlacedObjectDataListWrapper>(PlacedObjectDataID, serializer);
        
        if (wrapper != null)
        {
            return wrapper.placedObjectDataList;
        }
        return new List<PlacedObjectData>();

    }
    public static void UpdatePlacedObject(PlacedObjectData placedObjectData, Vector3 oldPos)
    {
        List<PlacedObjectData> placedObjectDataList = LoadPlacedObjects();
        bool objectFound = false;
        for (int i = 0; i < placedObjectDataList.Count; i++)
        {

            if (placedObjectDataList[i].objectType == placedObjectData.objectType &&
                placedObjectDataList[i].position.x == oldPos.x &&
                placedObjectDataList[i].position.z == oldPos.z)
            {
                Debug.Log("OBject Placed Save");

                placedObjectDataList[i] = placedObjectData;
                
                objectFound = true;
                break;
            }
        }
        if (objectFound)
            SavePlacedObjects(placedObjectDataList);
        else
            SavePlacedObject(placedObjectData);
    }
    
    [System.Serializable]
    public class PlacedObjectDataListWrapper
    {
        public List<PlacedObjectData> placedObjectDataList;
    }


    public static void MarkAsSaveIsRequired()
    {
        SaveIsRequired = true;
    }

    public static int GiveUniqueID()
    {
        uID++;
        return uID;
    }

  
}
#endregion


[System.Serializable]
public struct SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public SerializableVector3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public SerializableVector3(Vector3 vector3)
    {
        this.x = vector3.x;
        this.y = vector3.y;
        this.z = vector3.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}