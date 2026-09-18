using System;
using System.Collections.Generic;

namespace FoodStreet.Saving
{
    /// <summary>
    /// The restaurant itself: what is built, what is growing, what is cooking,
    /// and what is in the bag.
    ///
    /// Pots and stations are keyed by the ID of the placed object they sit on,
    /// never by list position. Position-keyed loading was the source of the old
    /// system's index-out-of-range crash the moment a player built or sold a pot.
    /// </summary>
    [Serializable]
    public class WorldSaveData
    {
        public List<PlacedObjectData> placedObjects = new List<PlacedObjectData>();
        public List<PotSaveData> pots = new List<PotSaveData>();
        public List<CookStationSaveData> stations = new List<CookStationSaveData>();
        public InventorySaveData inventory = new InventorySaveData();

        public void EnsureComplete()
        {
            if (placedObjects == null) placedObjects = new List<PlacedObjectData>();
            if (pots == null) pots = new List<PotSaveData>();
            if (stations == null) stations = new List<CookStationSaveData>();
            if (inventory == null) inventory = new InventorySaveData();

            inventory.EnsureComplete();
        }

        public PotSaveData FindPot(int ownerId)
        {
            for (int i = 0; i < pots.Count; i++)
            {
                if (pots[i] != null && pots[i].ownerId == ownerId)
                    return pots[i];
            }
            return null;
        }

        public CookStationSaveData FindStation(int ownerId)
        {
            for (int i = 0; i < stations.Count; i++)
            {
                if (stations[i] != null && stations[i].ownerId == ownerId)
                    return stations[i];
            }
            return null;
        }
    }

    [Serializable]
    public class PotSaveData
    {
        public int ownerId;
        public PlantPot.PotState state;

        /// <summary>
        /// Only the seed's type is stored. The old format serialised the whole
        /// ItemDataSO asset reference into the save, which bloated the file and
        /// could not survive the asset being edited.
        /// </summary>
        public bool hasSeed;
        public ItemType seedType;
        public int seedAmount = 1;

        public float remainingSeconds;
    }

    [Serializable]
    public class CookStationSaveData
    {
        public int ownerId;
        public List<CookRecipeSaveData> recipes = new List<CookRecipeSaveData>();
    }

    [Serializable]
    public class CookRecipeSaveData
    {
        /// <summary>
        /// Recipes are identified by what they produce, which is unique per
        /// station and survives renaming the asset or its sprite.
        /// </summary>
        public ItemType resultType;

        public float remainingSeconds;
        public int totalSeconds;
        public bool active;
    }

    [Serializable]
    public class InventorySaveData
    {
        public int size = 20;
        public List<InventoryEntrySaveData> items = new List<InventoryEntrySaveData>();

        public void EnsureComplete()
        {
            if (items == null)
                items = new List<InventoryEntrySaveData>();
            if (size < 1)
                size = 20;
        }
    }

    [Serializable]
    public class InventoryEntrySaveData
    {
        public ItemType type;
        public int amount;
    }
}
