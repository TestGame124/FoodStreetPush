using UnityEngine;

namespace LastOrders.Livestock
{
    /// <summary>
    /// What the feed popup shows for one farm building, read fresh while it is open: what it is
    /// doing now, what its feed turns into, how much feed there is to give and how much fits.
    /// The player picks how many to make; <see cref="Most"/> is the most feed that can go in.
    /// </summary>
    public readonly struct FeedOffer
    {
        public FeedOffer(string title, string description, Sprite produceIcon, Sprite feedIcon, string unitName, string unitsName,
                         string feedName, int perFeed, float secondsEach, int have, int spare, bool keepsSeed, int inTrough,
                         int troughCapacity, int stored, int capacity, ProduceProgressView.State state, string status,
                         float progress, float secondsToNext)
        {
            Title = title;
            Description = description;
            ProduceIcon = produceIcon;
            FeedIcon = feedIcon;
            UnitName = unitName;
            UnitsName = unitsName;
            FeedName = feedName;
            PerFeed = Mathf.Max(1, perFeed);
            SecondsEach = secondsEach;
            Have = have;
            Spare = Mathf.Max(0, spare);
            KeepsSeed = keepsSeed;
            InTrough = inTrough;
            TroughCapacity = troughCapacity;
            Stored = stored;
            Capacity = capacity;
            State = state;
            Status = status;
            Progress = progress;
            SecondsToNext = secondsToNext;
        }

        public string Title { get; }
        public string Description { get; }
        public Sprite ProduceIcon { get; }
        public Sprite FeedIcon { get; }

        /// <summary>"egg" / "eggs", "churn of milk" / "churns of milk".</summary>
        public string UnitName { get; }
        public string UnitsName { get; }

        /// <summary>"corn", "wheat".</summary>
        public string FeedName { get; }

        /// <summary>Eggs or churns each item of feed is good for.</summary>
        public int PerFeed { get; }

        /// <summary>Seconds of the working day for each egg or churn.</summary>
        public float SecondsEach { get; }

        /// <summary>Of the feed, in the storehouse.</summary>
        public int Have { get; }

        /// <summary>Of the feed, what can be given: all of it, or all but the last of a crop, kept for sowing.</summary>
        public int Spare { get; }

        /// <summary>The feed is a crop, so its last one stays in the storehouse.</summary>
        public bool KeepsSeed { get; }

        public int InTrough { get; }
        public int TroughCapacity { get; }

        /// <summary>Eggs or churns waiting to be collected, and the most it keeps.</summary>
        public int Stored { get; }
        public int Capacity { get; }

        public ProduceProgressView.State State { get; }

        /// <summary>What is happening now, in a few words: "Next egg in", "Asleep until morning".</summary>
        public string Status { get; }

        public float Progress { get; }
        public float SecondsToNext { get; }

        /// <summary>The most items of feed that can go in now.</summary>
        public int Most => Mathf.Max(0, Mathf.Min(TroughCapacity - InTrough, Spare));

        /// <summary>Why nothing can be fed, or null when something can.</summary>
        public string Blocked
        {
            get
            {
                if (TroughCapacity - InTrough <= 0) return $"The feeder is full - they have {FeedName} for now.";
                if (Spare > 0) return null;
                return Have > 0
                    ? $"Grow more {FeedName} - the last one is kept for sowing."
                    : $"No {FeedName} in the storehouse - grow some in the plant beds.";
            }
        }

        /// <summary>The eggs or churns <paramref name="items"/> of feed make.</summary>
        public int Makes(int items) => items * PerFeed;

        /// <summary>Seconds of daytime <paramref name="items"/> of feed keep them busy.</summary>
        public float Takes(int items) => Makes(items) * SecondsEach;
    }
}
