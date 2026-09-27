using System;
using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// One thing a visitor might ask for, and how many of it.
    ///
    /// A range rather than a number, because "two breads" and "one bread" asked by the same
    /// kind of person on different days is the cheapest variety this system has. The weight is
    /// relative within a visitor's own list, so adding a dish to the menu is one more line
    /// rather than a rebalance of every other line.
    /// </summary>
    [Serializable]
    public struct ItemRequest
    {
        public ItemType item;

        [Tooltip("Inclusive. x and y equal is a fixed amount.")]
        public Vector2Int amountRange;

        [Min(0f)] public float weight;
    }

    /// <summary>
    /// What one seated visitor is waiting for.
    ///
    /// A value rather than a reference to the request it came from: the order is rolled once,
    /// when they sit down, and must not change afterwards. A visitor whose order re-rolls while
    /// the player is walking back with the food is the worst bug this system could have, and
    /// making the order immutable is what makes it impossible rather than merely unlikely.
    /// </summary>
    public readonly struct VisitorOrder
    {
        public readonly ItemType Item;
        public readonly int Amount;

        public VisitorOrder(ItemType item, int amount)
        {
            Item = item;
            Amount = amount;
        }

        public bool IsEmpty => Amount <= 0;

        /// <summary>The item's database record, or null when the level has no such item.</summary>
        public ItemDataSO Data => ItemController.GetItem(Item);

        public override string ToString() => $"{Amount}x {Item}";
    }
}
