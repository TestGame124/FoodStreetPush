using System.Collections.Generic;

namespace LastOrders.Kitchen
{
    /// <summary>
    /// Cooked dishes that can leave the kitchen without passing through the dining room: packed
    /// into a takeaway bag, loaded onto the food truck.
    ///
    /// A dish taken is gone from the pass at once rather than held for later, because the
    /// waiters and the walk-in customers count the same plates and know nothing of anybody
    /// else's claim. One put back - a car that gave up waiting, a truck unloaded before it left -
    /// is on the pass again for anyone.
    /// </summary>
    public interface IDishStore
    {
        /// <summary>How many of <paramref name="dish"/> are on the pass and not already promised to a table.</summary>
        int Spare(ItemType dish);

        /// <summary>Takes one off the pass. False when there is none spare.</summary>
        bool TryTake(ItemType dish);

        /// <summary>Puts one taken earlier back on the pass.</summary>
        void PutBack(ItemType dish);

        /// <summary>Every dish the level's stations can cook, whether or not any is ready.</summary>
        IReadOnlyCollection<ItemType> Menu { get; }
    }
}
