using UnityEngine;

namespace LastOrders.Livestock
{
    /// <summary>
    /// What a pen's animals have been given to eat, and so how much more they can give: the hens'
    /// corn, the cow's wheat. Each item fed keeps them going for <see cref="UnitsPerFeed"/> eggs or
    /// churns; the trough holds <see cref="Capacity"/> items at most. Nothing is laid or milked on an
    /// empty trough.
    ///
    /// Kept in portions - one per egg or churn - so an item part eaten is never lost or rounded away.
    /// Plain C#, so the rule can be tested without a scene.
    /// </summary>
    public sealed class FeedTrough
    {
        public FeedTrough(int capacity, int unitsPerFeed)
        {
            Capacity = Mathf.Max(1, capacity);
            UnitsPerFeed = Mathf.Max(1, unitsPerFeed);
        }

        /// <summary>The most items the trough holds.</summary>
        public int Capacity { get; }

        /// <summary>Eggs or churns one item of feed is good for.</summary>
        public int UnitsPerFeed { get; }

        /// <summary>Eggs or churns still paid for.</summary>
        public int Portions { get; private set; }

        public int MaxPortions => Capacity * UnitsPerFeed;

        /// <summary>Nothing left to eat: production stops.</summary>
        public bool Hungry => Portions <= 0;

        /// <summary>Whole items that would still fit.</summary>
        public int Room => (MaxPortions - Portions) / UnitsPerFeed;

        /// <summary>Items in the trough, counting one part eaten as one.</summary>
        public int ItemsLeft => (Portions + UnitsPerFeed - 1) / UnitsPerFeed;

        /// <summary>Puts up to <paramref name="items"/> in, as many as fit. Returns how many went in.</summary>
        public int Fill(int items)
        {
            int taken = Mathf.Clamp(items, 0, Room);
            Portions += taken * UnitsPerFeed;
            return taken;
        }

        /// <summary>Eats one portion's worth, for one egg or churn. False when there was nothing to eat.</summary>
        public bool Eat()
        {
            if (Portions <= 0) return false;
            Portions--;
            return true;
        }

        /// <summary>Starts over with <paramref name="items"/> already in the trough.</summary>
        public void Reset(int items)
        {
            Portions = Mathf.Clamp(items, 0, Capacity) * UnitsPerFeed;
        }
    }
}
