using System.Collections.Generic;
using FoodStreet.Saving;
using UnityEngine;

/// <summary>
/// The build grid's view of persistence.
///
/// All of the real work now lives in <see cref="SaveSystem"/>; this type stays
/// because the grid building package calls into it from a dozen places, and
/// because placed objects deserve an API that speaks in objects rather than in
/// files. Every method here edits the in-memory model and marks it dirty - none
/// of them touch the disk, so placing a hundred tiles costs a hundred list
/// operations and one write.
/// </summary>
public static class SaveController
{
    /// <inheritdoc cref="SaveIds.RuntimeIdBase"/>
    public const int RUNTIME_ID_BASE = SaveIds.RuntimeIdBase;

    private static List<PlacedObjectData> PlacedObjects
    {
        get
        {
            if (!SaveSystem.IsReady)
            {
                Debug.LogError("[SaveController] Placed objects touched before SaveSystem.Initialize.");
                return null;
            }
            return SaveSystem.Data.world.placedObjects;
        }
    }

    #region Placed objects

    /// <summary>
    /// A copy, so callers can iterate while placement code mutates the model.
    /// </summary>
    public static List<PlacedObjectData> LoadPlacedObjects()
    {
        List<PlacedObjectData> stored = PlacedObjects;
        return stored == null
            ? new List<PlacedObjectData>()
            : new List<PlacedObjectData>(stored);
    }

    public static void SavePlacedObjects(List<PlacedObjectData> placedObjectDataList)
    {
        List<PlacedObjectData> stored = PlacedObjects;
        if (stored == null)
            return;

        stored.Clear();
        if (placedObjectDataList != null)
            stored.AddRange(placedObjectDataList);

        SaveSystem.MarkDirty();
    }

    public static void SavePlacedObject(PlacedObjectData placedObjectData)
    {
        List<PlacedObjectData> stored = PlacedObjects;
        if (stored == null || IndexOfId(stored, placedObjectData.Id) >= 0)
            return;

        stored.Add(placedObjectData);
        SaveSystem.MarkDirty();
    }

    public static void UpdatePlacedObject(PlacedObjectData placedObjectData)
    {
        List<PlacedObjectData> stored = PlacedObjects;
        if (stored == null)
            return;

        int index = IndexOfId(stored, placedObjectData.Id);
        if (index >= 0)
        {
            if (stored[index].Equals(placedObjectData))
                return;

            stored[index] = placedObjectData;
        }
        else
        {
            stored.Add(placedObjectData);
        }

        SaveSystem.MarkDirty();
    }

    public static void RemoveObject(PlacedObjectData placedObjectData)
    {
        List<PlacedObjectData> stored = PlacedObjects;
        if (stored == null)
            return;

        int index = IndexOfId(stored, placedObjectData.Id);
        if (index < 0)
            return;

        stored.RemoveAt(index);
        SaveSystem.MarkDirty();
    }

    public static void BatchSavePlacedObjects(List<PlacedObjectData> placedObjectsBatch, List<PlacedObjectData> removedObjectsBatch)
    {
        List<PlacedObjectData> stored = PlacedObjects;
        if (stored == null)
            return;

        bool changed = false;

        if (removedObjectsBatch != null)
        {
            for (int i = 0; i < removedObjectsBatch.Count; i++)
            {
                int index = IndexOfId(stored, removedObjectsBatch[i].Id);
                if (index < 0)
                    continue;

                stored.RemoveAt(index);
                changed = true;
            }
        }

        if (placedObjectsBatch != null)
        {
            for (int i = 0; i < placedObjectsBatch.Count; i++)
            {
                PlacedObjectData placed = placedObjectsBatch[i];
                if (IndexOfId(stored, placed.Id) >= 0)
                    continue;

                stored.Add(placed);
                changed = true;
            }
        }

        if (changed)
            SaveSystem.MarkDirty();
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

    #endregion

    #region Runtime IDs

    /// <summary>
    /// Raises the counter above every ID already in the file. Must run before
    /// anything places a runtime object.
    ///
    /// The counter itself travels inside the save file, so it can no longer
    /// drift out of step with the object list the way the old PlayerPrefs copy
    /// could - whichever write lands last carries both. This scan only matters
    /// for a file restored from a backup whose counter is behind its contents.
    /// </summary>
    public static void InitializeIds()
    {
        if (!SaveSystem.IsReady)
        {
            Debug.LogError("[SaveController] InitializeIds called before SaveSystem.Initialize.");
            return;
        }

        ProfileSaveData profile = SaveSystem.Data.profile;
        int highest = Mathf.Max(profile.placedObjectIdCounter, SaveIds.RuntimeIdBase);

        List<PlacedObjectData> stored = SaveSystem.Data.world.placedObjects;
        for (int i = 0; i < stored.Count; i++)
        {
            if (stored[i].Id > highest)
                highest = stored[i].Id;
        }

        if (highest == profile.placedObjectIdCounter)
            return;

        profile.placedObjectIdCounter = highest;
        SaveSystem.MarkDirty();
    }

    /// <summary>Hands out an ID no live or saved object is using.</summary>
    public static int GiveUniqueID()
    {
        if (!SaveSystem.IsReady)
        {
            Debug.LogError("[SaveController] GiveUniqueID called before SaveSystem.Initialize.");
            return SaveIds.RuntimeIdBase;
        }

        ProfileSaveData profile = SaveSystem.Data.profile;
        if (profile.placedObjectIdCounter < SaveIds.RuntimeIdBase)
            profile.placedObjectIdCounter = SaveIds.RuntimeIdBase;

        profile.placedObjectIdCounter++;
        SaveSystem.MarkDirty();
        return profile.placedObjectIdCounter;
    }

    #endregion

    /// <summary>Kept so existing call sites keep compiling; requesting a save is now free.</summary>
    public static void MarkAsSaveIsRequired()
    {
        SaveSystem.MarkDirty();
    }

    /// <summary>
    /// The legacy binary payload. Only <see cref="SaveMigrations"/> reads it -
    /// the type has to keep this exact name and shape or BinaryFormatter cannot
    /// resolve the old file.
    /// </summary>
    [System.Serializable]
    public class PlacedObjectDataListWrapper
    {
        public List<PlacedObjectData> placedObjectDataList;
    }
}
