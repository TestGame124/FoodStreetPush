using System;
using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>The economy of §19. One enum rather than a class per resource, because every
    /// cost in the game is a small bag of these and they need to serialise flat.</summary>
    public enum ResourceType
    {
        Coin = 0,
        Scrap = 1,
        Parts = 2,
        Ammo = 3,
        Medicine = 4,

        // The five ingredients of §8. Kept in the same enum as the hard resources so a
        // recipe cost and a build cost are the same type and share one wallet UI.
        Meat = 10,
        Grain = 11,
        Veg = 12,
        Spice = 13,
        Luxury = 14,
    }

    /// <summary>One line of a cost or a reward. A serialisable pair, so costs author as a
    /// list in the inspector and diff cleanly in version control.</summary>
    [Serializable]
    public struct ResourceAmount
    {
        public ResourceType type;
        public int amount;

        public ResourceAmount(ResourceType type, int amount)
        {
            this.type = type;
            this.amount = amount;
        }

        public override string ToString() => $"{amount} {type}";
    }

    /// <summary>The buildable categories of the §6.1 table. Drives the build menu tabs and
    /// nothing else, so adding one is a data change plus a tab.</summary>
    public enum BuildCategory
    {
        Service = 0,
        Production = 1,
        Structure = 2,
        Defense = 3,
        Utility = 4,
        Comfort = 5,
    }

    /// <summary>
    /// §15.4. The three damage states, and the rule that separates them: perimeter objects
    /// can be Destroyed, interior objects can only ever be Wrecked. A wrecked restaurant
    /// still looks like the player's restaurant at dawn.
    /// </summary>
    public enum DamageState
    {
        /// <summary>Undamaged, full efficiency.</summary>
        Intact = 0,

        /// <summary>Works at 60 to 90 percent. Repair costs 15 percent and one clock-minute.</summary>
        Damaged = 1,

        /// <summary>Non-functional but still present. The tile stays occupied.</summary>
        Wrecked = 2,

        /// <summary>Gone, leaving a ghost outline. The tile is free and one tap rebuilds it.</summary>
        Destroyed = 3,
    }

    /// <summary>
    /// Which flow field an enemy samples. Mirrors <see cref="FoodStreet.Navigation.FlowFieldId"/>
    /// one-for-one and exists so the data layer does not have to reference the navigation
    /// assembly's enum directly in every enemy asset.
    ///
    /// This is the whole of enemy AI. A Brute is not a Shambler with different branching, it
    /// is a Shambler reading a different field.
    /// </summary>
    public enum ThreatNavigation
    {
        /// <summary>Route to a door, walls solid. Shambler, Runner.</summary>
        Doors = 0,

        /// <summary>Walls passable but expensive. Use a door if there is one, otherwise come
        /// through the shell. Brute.</summary>
        Breach = 1,

        /// <summary>Walls barely pricier than open ground, so the field pools at the thinnest
        /// or least defended stretch. Flanker.</summary>
        WeakestPoint = 2,

        /// <summary>Route to the stores. Glutton.</summary>
        Pantry = 3,
    }

    /// <summary>Utility for turning a designer-authored cost list into lookups without
    /// allocating a dictionary per query.</summary>
    public static class ResourceCostExtensions
    {
        public static int AmountOf(this ResourceAmount[] costs, ResourceType type)
        {
            if (costs == null) return 0;
            for (int i = 0; i < costs.Length; i++)
                if (costs[i].type == type) return costs[i].amount;
            return 0;
        }

        /// <summary>Scales a cost list, rounding up so a 40 percent rebuild of a 1-Scrap item
        /// still costs something. Free rebuilds would make the consequence model vacuous.</summary>
        public static ResourceAmount[] Scaled(this ResourceAmount[] costs, float multiplier)
        {
            if (costs == null || costs.Length == 0) return Array.Empty<ResourceAmount>();

            var result = new ResourceAmount[costs.Length];
            for (int i = 0; i < costs.Length; i++)
            {
                result[i] = new ResourceAmount(
                    costs[i].type,
                    Mathf.Max(costs[i].amount > 0 ? 1 : 0, Mathf.CeilToInt(costs[i].amount * multiplier)));
            }
            return result;
        }
    }
}
