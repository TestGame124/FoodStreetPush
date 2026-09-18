using System;
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.DayCycle;

namespace LastOrders.Save
{
    /// <summary>
    /// Everything Last Orders adds to the save file.
    ///
    /// Hung off the existing <see cref="FoodStreet.Saving.GameSaveData"/> as one section
    /// rather than spread across it, so the whole feature can be versioned and migrated as a
    /// unit, and so a file written before this existed simply has a null here.
    ///
    /// 24.5 requires a full write at every day boundary plus a mid-night snapshot, because
    /// mobile OSes kill backgrounded apps constantly and a siege that cannot be resumed is a
    /// one-star review. That is why <see cref="phase"/> and <see cref="nightElapsedSeconds"/>
    /// are here: the file has to be able to come back up in the middle of a wave.
    /// </summary>
    [Serializable]
    public sealed class LastOrdersSaveData
    {
        [Serializable]
        public sealed class SurvivorRecord
        {
            public string id;
            public string name;
            public string roleAssetName;
            public string traitAssetName;
            public int injuryDaysRemaining;
            public int awayDaysRemaining;
        }

        [Serializable]
        public sealed class GhostRecord
        {
            public string typeAssetName;
            public int originX;
            public int originZ;
            public int dir;
            public int dayLost;
        }

        [Serializable]
        public sealed class ResourceRecord
        {
            public ResourceType type;
            public int amount;
        }

        // --- Cycle ---
        public int day = 1;
        public DayPhase phase = DayPhase.Dawn;
        public float clockMinutes;

        /// <summary>Seconds into the night. Restoring mid-siege resumes from here rather than
        /// restarting the wave, which would hand the player a second full night.</summary>
        public float nightElapsedSeconds;

        /// <summary>How many of the day's scheduled parties have already walked in, so a
        /// restored day does not re-spawn the lunch rush.</summary>
        public int partiesReleased;

        // --- Heat ---
        public float heatCurrent;
        public float heatLocked;
        public bool heatIsLocked;

        // --- Reputation ---
        public float reputation;

        /// <summary>The permanent floor. Stored explicitly rather than recomputed, because it
        /// is the guarantee that progress can never be lost (15.1) and must survive even a
        /// rebalance of the tier thresholds.</summary>
        public int highestReputationTier;

        // --- Economy ---
        public List<ResourceRecord> resources = new List<ResourceRecord>();

        // --- People ---
        public List<SurvivorRecord> survivors = new List<SurvivorRecord>();

        // --- Consequence ---
        public List<GhostRecord> ghosts = new List<GhostRecord>();

        public int plotTier = 1;

        public void EnsureComplete()
        {
            if (resources == null) resources = new List<ResourceRecord>();
            if (survivors == null) survivors = new List<SurvivorRecord>();
            if (ghosts == null) ghosts = new List<GhostRecord>();
            if (day < 1) day = 1;
            if (plotTier < 1) plotTier = 1;
        }
    }
}
