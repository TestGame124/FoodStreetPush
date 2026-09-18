using System;

namespace FoodStreet.Saving
{
    /// <summary>
    /// The whole save file, in one object. Everything the game persists hangs
    /// off here, so a write is always internally consistent: coins can never
    /// land on disk without the world state that earned them.
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        /// <summary>
        /// Bumped whenever the shape of the data changes in a way older files
        /// cannot be read as-is. <see cref="SaveMigrations"/> upgrades on load.
        /// </summary>
        public int schemaVersion = SaveMigrations.CurrentSchemaVersion;

        public long savedAtUtcTicks;

        public ProfileSaveData profile = new ProfileSaveData();
        public EconomySaveData economy = new EconomySaveData();
        public ProgressSaveData progress = new ProgressSaveData();
        public WorldSaveData world = new WorldSaveData();

        /// <summary>
        /// The Last Orders section: day cycle, Heat, Reputation, the roster and the
        /// ghost outlines awaiting rebuild. Null in any file written before it existed,
        /// which EnsureComplete fills in rather than migrating.
        /// </summary>
        public LastOrders.Save.LastOrdersSaveData lastOrders = new LastOrders.Save.LastOrdersSaveData();

        /// <summary>
        /// A file that has been through migration can still be missing whole
        /// sections if it was hand-truncated. Fill in the gaps so no caller has
        /// to null-check its way down the tree.
        /// </summary>
        public void EnsureComplete()
        {
            if (profile == null) profile = new ProfileSaveData();
            if (economy == null) economy = new EconomySaveData();
            if (progress == null) progress = new ProgressSaveData();
            if (world == null) world = new WorldSaveData();
            if (lastOrders == null) lastOrders = new LastOrders.Save.LastOrdersSaveData();

            profile.EnsureComplete();
            progress.EnsureComplete();
            world.EnsureComplete();
            lastOrders.EnsureComplete();
        }
    }

    [Serializable]
    public class ProfileSaveData
    {
        /// <summary>False only until the very first session finishes booting.</summary>
        public bool hasPlayedBefore;

        public string restaurantName = string.Empty;

        /// <summary>
        /// High-water mark for runtime-placed object IDs. Lives in the save file
        /// rather than PlayerPrefs so it can never drift out of step with the
        /// object list it hands out IDs for.
        /// </summary>
        public int placedObjectIdCounter = SaveIds.RuntimeIdBase;

        public void EnsureComplete()
        {
            if (restaurantName == null)
                restaurantName = string.Empty;
            if (placedObjectIdCounter < SaveIds.RuntimeIdBase)
                placedObjectIdCounter = SaveIds.RuntimeIdBase;
        }
    }

    /// <summary>
    /// Currency balances. Encrypted along with the rest of the file - this is
    /// the section a save editor goes looking for first.
    /// </summary>
    [Serializable]
    public class EconomySaveData
    {
        public int coins;
        public int gems;
    }

    public static class SaveIds
    {
        /// <summary>
        /// Scene-authored objects take IDs 1..N from their index in the zone's
        /// list, which is stable across sessions. Runtime placements allocate
        /// above this base, so a runtime ID never collides with a scene one.
        /// </summary>
        public const int RuntimeIdBase = 100000;
    }
}
