using System;
using System.Collections.Generic;
using System.IO;
using BayatGames.SaveGameFree;
using BayatGames.SaveGameFree.Serializers;
using UnityEngine;

namespace FoodStreet.Saving
{
    /// <summary>
    /// Two jobs: bring an older file of ours up to the current schema, and take
    /// over from the save system this one replaced.
    ///
    /// The legacy import runs exactly once - the first boot where no file of
    /// ours exists but the old ones do. Afterwards the old files are deleted, so
    /// the plaintext currency.json cannot sit around as an alternative source of
    /// truth an editor could point the game back at.
    /// </summary>
    internal static class SaveMigrations
    {
        /// <summary>Bump when the shape of <see cref="GameSaveData"/> changes incompatibly.</summary>
        internal const int CurrentSchemaVersion = 1;

        private const string LegacyPlacedObjectsId = "placedObjectList";
        private const string LegacyZoneId = "zoneData";
        private const string LegacyXpId = "XpData";
        private const string LegacyTutorialId = "tutorialSave";

        private const string LegacyCurrencyFile = "currency.json";
        private const string LegacyCookRecipeFile = "cookRecipes.json";

        private const string LegacyIdCounterKey = "placedObjectIdCounter";
        private const string LegacyFirstTimeKey = "FirstTime";
        private const string LegacyFirstGameKey = "FirstGame";
        private const string LegacyRestaurantNameKey = "nameSave";

        #region Schema upgrades

        internal static void Upgrade(GameSaveData data)
        {
            if (data == null)
                return;

            if (data.schemaVersion > CurrentSchemaVersion)
            {
                // A build older than the save file. Reading it as-is is the best
                // we can do; unknown fields are ignored by the serializer.
                Debug.LogWarning(
                    $"[SaveSystem] Save was written by a newer build (schema {data.schemaVersion} > {CurrentSchemaVersion}). " +
                    "Loading it anyway - anything this build does not understand will be dropped on the next save.");
                return;
            }

            while (data.schemaVersion < CurrentSchemaVersion)
            {
                switch (data.schemaVersion)
                {
                    // Add a case per version step as the schema evolves, e.g.
                    // case 1: UpgradeFrom1To2(data); break;
                    default:
                        Debug.LogWarning($"[SaveSystem] No upgrade path from schema {data.schemaVersion}; treating it as current.");
                        data.schemaVersion = CurrentSchemaVersion;
                        return;
                }
            }
        }

        #endregion

        #region Legacy import

        /// <summary>
        /// Builds the starting save for a player who has no file in the new
        /// format, pulling across whatever the old system left behind.
        /// </summary>
        internal static GameSaveData ImportLegacy()
        {
            GameSaveData data = new GameSaveData();
            data.EnsureComplete();

            if (!HasAnyLegacyData())
                return data;

            Debug.Log("[SaveSystem] Old save data found - importing it into the new format.");

            ImportPlayerPrefs(data);
            ImportCurrencies(data);
            ImportExperience(data);
            ImportTutorial(data);
            ImportPlacedObjects(data);

            // Everything worth keeping is now inside the encrypted file. Clearing
            // the old files keeps a single source of truth.
            DeleteLegacyData();

            return data;
        }

        private static bool HasAnyLegacyData()
        {
            try
            {
                if (PlayerPrefs.HasKey(LegacyFirstTimeKey) ||
                    PlayerPrefs.HasKey(LegacyIdCounterKey) ||
                    PlayerPrefs.HasKey(LegacyRestaurantNameKey))
                    return true;

                if (File.Exists(Path.Combine(Application.persistentDataPath, LegacyCurrencyFile)))
                    return true;

                return SaveGame.Exists(LegacyPlacedObjectsId)
                    || SaveGame.Exists(LegacyXpId)
                    || SaveGame.Exists(LegacyTutorialId)
                    || SaveGame.Exists(LegacyZoneId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveSystem] Could not probe for old save data: {exception.Message}");
                return false;
            }
        }

        private static void ImportPlayerPrefs(GameSaveData data)
        {
            try
            {
                data.profile.hasPlayedBefore = PlayerPrefs.GetInt(LegacyFirstTimeKey, 0) == 1;
                data.profile.restaurantName = PlayerPrefs.GetString(LegacyRestaurantNameKey, string.Empty);

                int counter = PlayerPrefs.GetInt(LegacyIdCounterKey, SaveIds.RuntimeIdBase);
                data.profile.placedObjectIdCounter = Mathf.Max(counter, SaveIds.RuntimeIdBase);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveSystem] Could not import player prefs: {exception.Message}");
            }
        }

        private static void ImportCurrencies(GameSaveData data)
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, LegacyCurrencyFile);
                if (!File.Exists(path))
                    return;

                LegacyCurrencyData legacy = JsonUtility.FromJson<LegacyCurrencyData>(File.ReadAllText(path));
                if (legacy == null)
                    return;

                data.economy.coins = Mathf.Max(0, legacy.Coins);
                data.economy.gems = Mathf.Max(0, legacy.Gems);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveSystem] Could not import currencies: {exception.Message}");
            }
        }

        private static void ImportExperience(GameSaveData data)
        {
            try
            {
                if (!SaveGame.Exists(LegacyXpId))
                    return;

                LegacyExperienceData legacy = SaveGame.Load<LegacyExperienceData>(LegacyXpId);
                if (legacy == null)
                    return;

                data.progress.level = Mathf.Max(1, legacy.Level);
                data.progress.currentXp = Mathf.Max(0, legacy.CurrentXP);
                data.progress.xpToNextLevel = legacy.XPToNextLevel > 0
                    ? legacy.XPToNextLevel
                    : 3 + (data.progress.level - 1) * 2;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveSystem] Could not import experience: {exception.Message}");
            }
        }

        private static void ImportTutorial(GameSaveData data)
        {
            try
            {
                if (!SaveGame.Exists(LegacyTutorialId))
                    return;

                LegacyTutorialData legacy = SaveGame.Load<LegacyTutorialData>(LegacyTutorialId);
                if (legacy == null)
                    return;

                data.progress.tutorialStage = legacy.TutorialStage;
                data.progress.tutorialEventRuns = Mathf.Max(0, legacy.numOfTimesEventWillRun);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveSystem] Could not import tutorial progress: {exception.Message}");
            }
        }

        private static void ImportPlacedObjects(GameSaveData data)
        {
            try
            {
                if (!SaveGame.Exists(LegacyPlacedObjectsId))
                    return;

                SaveController.PlacedObjectDataListWrapper wrapper =
                    SaveGame.Load<SaveController.PlacedObjectDataListWrapper>(
                        LegacyPlacedObjectsId, new SaveGameBinarySerializer());

                List<PlacedObjectData> legacyObjects = wrapper?.placedObjectDataList;
                if (legacyObjects == null)
                    return;

                int highestId = data.profile.placedObjectIdCounter;
                for (int i = 0; i < legacyObjects.Count; i++)
                {
                    PlacedObjectData placed = legacyObjects[i];
                    if (placed.isRemoved)
                        continue;

                    data.world.placedObjects.Add(placed);

                    if (placed.Id > highestId)
                        highestId = placed.Id;
                }

                // Guard against a PlayerPrefs counter that was lost when the app
                // was killed: never hand out an ID an existing object already uses.
                data.profile.placedObjectIdCounter = Mathf.Max(highestId, SaveIds.RuntimeIdBase);

                Debug.Log($"[SaveSystem] Imported {data.world.placedObjects.Count} placed objects.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveSystem] Could not import placed objects: {exception.Message}");
            }
        }

        /// <summary>
        /// In-flight timers - growing plants, cooking in progress - are
        /// deliberately not imported. The old format stored them as serialised
        /// ScriptableObject references that no longer resolve reliably, and they
        /// are worth seconds of play. Everything durable is carried across.
        /// </summary>
        internal static void DeleteLegacyData()
        {
            TryDeleteSaveGame(LegacyPlacedObjectsId);
            TryDeleteSaveGame(LegacyZoneId);
            TryDeleteSaveGame(LegacyXpId);
            TryDeleteSaveGame(LegacyTutorialId);

            TryDeleteFile(LegacyCurrencyFile);
            TryDeleteFile(LegacyCookRecipeFile);

            TryDeletePref(LegacyIdCounterKey);
            TryDeletePref(LegacyFirstTimeKey);
            TryDeletePref(LegacyFirstGameKey);
            TryDeletePref(LegacyRestaurantNameKey);

            PlayerPrefs.Save();
        }

        private static void TryDeleteSaveGame(string identifier)
        {
            try
            {
                if (SaveGame.Exists(identifier))
                    SaveGame.Delete(identifier);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveSystem] Could not delete old save '{identifier}': {exception.Message}");
            }
        }

        private static void TryDeleteFile(string fileName)
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, fileName);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveSystem] Could not delete old file '{fileName}': {exception.Message}");
            }
        }

        private static void TryDeletePref(string key)
        {
            try
            {
                if (PlayerPrefs.HasKey(key))
                    PlayerPrefs.DeleteKey(key);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveSystem] Could not delete pref '{key}': {exception.Message}");
            }
        }

        #endregion

        #region Legacy shapes

        // Local mirrors of the old payloads. Keeping them here means the live
        // gameplay types are free to change without breaking the import.

        [Serializable]
        private class LegacyCurrencyData
        {
            public int Coins;
            public int Gems;
        }

        [Serializable]
        private class LegacyExperienceData
        {
            public int Level = 1;
            public int CurrentXP;
            public int XPToNextLevel;
        }

        [Serializable]
        private class LegacyTutorialData
        {
            public TutorialStage TutorialStage = TutorialStage.FirstStage;
            public int numOfTimesEventWillRun;
        }

        #endregion
    }
}
