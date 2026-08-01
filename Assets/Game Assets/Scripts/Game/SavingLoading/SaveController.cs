
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

    private const string IdCounterKey = "placedObjectIdCounter";

    /// <summary>
    /// Objects authored into the scene take IDs 1..N from their index in the
    /// zone's list, which is stable across sessions. Runtime-placed objects
    /// allocate above this base from a persisted counter, so a runtime ID is
    /// never reused and never collides with a scene object's ID.
    /// </summary>
    public const int RUNTIME_ID_BASE = 100000;

    public static int uID = RUNTIME_ID_BASE;

    public const int SAVE_DELAY = 2;

    public static readonly object fileLock = new object();

    static ISaveGameSerializer serializer;

    // Authoritative in-memory copy of the placed-object list. The save file is
    // deserialized once per session; every mutation edits this list and writes
    // it back, instead of doing a full load + scan + save on every placement.
    private static List<PlacedObjectData> cachedPlacedObjects;

    private static List<PlacedObjectData> GetCache()
    {
        if (cachedPlacedObjects == null)
        {
            if (serializer == null)
                serializer = new SaveGameBinarySerializer();

            PlacedObjectDataListWrapper wrapper = SaveGame.Load<PlacedObjectDataListWrapper>(PlacedObjectDataID, serializer);
            cachedPlacedObjects = wrapper != null && wrapper.placedObjectDataList != null
                ? wrapper.placedObjectDataList
                : new List<PlacedObjectData>();
        }
        return cachedPlacedObjects;
    }

    private static void Persist()
    {
        if (serializer == null)
            serializer = new SaveGameBinarySerializer();

        PlacedObjectDataListWrapper placedObj = new PlacedObjectDataListWrapper { placedObjectDataList = GetCache() };
        SaveGame.Save(PlacedObjectDataID, placedObj, serializer);
    }

    public static void SavePlacedObjects(List<PlacedObjectData> placedObjectDataList)
    {
        cachedPlacedObjects = placedObjectDataList ?? new List<PlacedObjectData>();
        Persist();
    }
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Saving/Clear Data")]
    public static void RemoveAllSave()
    {
        PlayerPrefs.DeleteAll();
        SaveGame.Clear();
        cachedPlacedObjects = null;
    }
#endif

    public static bool isSavingPlacedObject;

    public static void SavePlacedObject(PlacedObjectData placedObjectData)
    {
        List<PlacedObjectData> placedObjectDataList = GetCache();

        if (IndexOfId(placedObjectDataList, placedObjectData.Id) < 0)
        {
            placedObjectDataList.Add(placedObjectData);
            Persist();
        }
    }

    private static int IndexOfId(List<PlacedObjectData> list, int id)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Id == id)
                return i;
        }
        return -1;
    }

    public static void BatchSavePlacedObjects(List<PlacedObjectData> placedObjectsBatch, List<PlacedObjectData> removedObjectsBatch)
    {
        List<PlacedObjectData> placedObjectDataList = GetCache();

        foreach (var removedObjectData in removedObjectsBatch)
        {
            int index = IndexOfId(placedObjectDataList, removedObjectData.Id);
            if (index >= 0)
                placedObjectDataList.RemoveAt(index);
        }

        foreach (var placedObjectData in placedObjectsBatch)
        {
            if (IndexOfId(placedObjectDataList, placedObjectData.Id) < 0)
                placedObjectDataList.Add(placedObjectData);
        }

        Persist();
    }


    public static void RemoveObject(PlacedObjectData placedObjectData)
    {
        List<PlacedObjectData> placedObjectDataList = GetCache();

        int index = IndexOfId(placedObjectDataList, placedObjectData.Id);
        if (index >= 0)
        {
            placedObjectDataList.RemoveAt(index);
            Persist();
        }
    }

    /// <summary>
    /// Returns a snapshot of the saved placed objects. Callers get their own
    /// list so they can iterate it while placement code mutates the cache.
    /// </summary>
    public static List<PlacedObjectData> LoadPlacedObjects()
    {
        return new List<PlacedObjectData>(GetCache());
    }

    public static void UpdatePlacedObject(PlacedObjectData placedObjectData)
    {
        List<PlacedObjectData> placedObjectDataList = GetCache();

        int index = IndexOfId(placedObjectDataList, placedObjectData.Id);
        if (index >= 0)
        {
            placedObjectDataList[index] = placedObjectData;
            Persist();
        }
        else
        {
            SavePlacedObject(placedObjectData);
        }
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

    /// <summary>
    /// Restores the runtime ID counter. Must run before any runtime object is
    /// placed, otherwise fresh objects reuse IDs from a previous session.
    /// </summary>
    public static void InitializeIds()
    {
        uID = PlayerPrefs.GetInt(IdCounterKey, RUNTIME_ID_BASE);
        if (uID < RUNTIME_ID_BASE)
            uID = RUNTIME_ID_BASE;
    }

    public static int GiveUniqueID()
    {
        if (uID < RUNTIME_ID_BASE)
            uID = RUNTIME_ID_BASE;

        uID++;
        PlayerPrefs.SetInt(IdCounterKey, uID);
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