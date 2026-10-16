using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// What something costs: the level's Coins, the stores, or both. Every part of it is paid,
    /// so "60 Coins + 15 Wood" costs sixty coins and fifteen wood. A price with neither is free.
    ///
    /// Coins here and Coin resource entries use the same wallet in the working level.
    /// </summary>
    [Serializable]
    public struct Price
    {
        [Tooltip("The level's own Coins - what visitors pay and the HUD counts.")]
        [Min(0)] public int coins;

        [Tooltip("Taken from the stores, on top of the coins.")]
        public ResourceAmount[] resources;

        public Price(int coins, params ResourceAmount[] resources)
        {
            this.coins = coins;
            this.resources = resources;
        }

        public bool HasResources
        {
            get
            {
                if (resources == null) return false;
                for (int i = 0; i < resources.Length; i++)
                    if (resources[i].amount > 0) return true;
                return false;
            }
        }

        public bool IsFree => coins <= 0 && !HasResources;

        /// <summary>
        /// Every part multiplied and rounded up, so a price that is not free never scales down
        /// to nothing: a quarter of a 1-Wood repair still costs a Wood.
        /// </summary>
        public Price Scaled(float multiplier)
        {
            multiplier = Mathf.Max(0f, multiplier);

            int scaledCoins = coins > 0 ? Mathf.Max(multiplier > 0f ? 1 : 0, Mathf.CeilToInt(coins * multiplier)) : 0;
            ResourceAmount[] scaledResources = multiplier > 0f ? resources.Scaled(multiplier) : Array.Empty<ResourceAmount>();

            return new Price(scaledCoins, scaledResources);
        }

        /// <summary>"60 Coins + 15 Wood". Empty when it is free.</summary>
        public string Describe(string coinName = "Coins")
        {
            var parts = new List<string>();
            if (coins > 0) parts.Add($"{coins} {coinName}");

            if (resources != null)
            {
                for (int i = 0; i < resources.Length; i++)
                    if (resources[i].amount > 0) parts.Add($"{resources[i].amount} {resources[i].type.DisplayName()}");
            }

            return string.Join(" + ", parts);
        }
    }
}
