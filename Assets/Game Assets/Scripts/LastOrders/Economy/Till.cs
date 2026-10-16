using LastOrders.Data;

namespace LastOrders.Economy
{
    /// <summary>
    /// Takes a <see cref="Price"/>: the stores from the ledger, the coins from the level's wallet.
    ///
    /// All or nothing, like the ledger's own spend. The stores are handed back if the coins then
    /// cannot be taken, so a price is never left half paid.
    /// </summary>
    public static class Till
    {
        static Price Normalize(Price price)
        {
            long totalCoins = System.Math.Max(0, price.coins);
            var resources = new System.Collections.Generic.Dictionary<ResourceType, int>();
            if (price.resources != null)
                foreach (ResourceAmount entry in price.resources)
                {
                    ResourceType type = entry.type.Canonical();
                    if (entry.amount <= 0) continue;
                    if (type == ResourceType.Coin) totalCoins += entry.amount;
                    else
                    {
                        resources.TryGetValue(type, out int amount);
                        resources[type] = (int)System.Math.Min(int.MaxValue, (long)amount + entry.amount);
                    }
                }
            var result = new System.Collections.Generic.List<ResourceAmount>();
            foreach (var entry in resources) result.Add(new ResourceAmount(entry.Key, entry.Value));
            return new Price((int)System.Math.Min(int.MaxValue, totalCoins), result.ToArray());
        }

        /// <summary>The first thing the player is short of, in words. Null when they have it all.</summary>
        public static string Shortfall(Price price, ResourceLedger ledger, IWallet coins)
        {
            price = Normalize(price);
            if (price.coins > 0)
            {
                int have = coins != null ? coins.Balance : 0;
                if (have < price.coins) return $"Need {price.coins - have} more {CoinName(coins)}.";
            }

            if (price.resources == null) return null;

            for (int i = 0; i < price.resources.Length; i++)
            {
                int have = ledger != null ? ledger.Get(price.resources[i].type) : 0;
                int shortBy = price.resources[i].amount - have;
                if (shortBy > 0) return $"Need {shortBy} more {price.resources[i].type.DisplayName()}.";
            }

            return null;
        }

        public static bool CanAfford(Price price, ResourceLedger ledger, IWallet coins) =>
            Shortfall(price, ledger, coins) == null;

        public static bool TryPay(Price price, ResourceLedger ledger, IWallet coins, out string reason)
        {
            price = Normalize(price);
            reason = Shortfall(price, ledger, coins);
            if (reason != null) return false;
            if (price.IsFree) return true;

            if (price.HasResources && (ledger == null || !ledger.TrySpend(price.resources)))
            {
                reason = "Could not pay for it.";
                return false;
            }

            if (price.coins > 0 && (coins == null || !coins.TrySpend(price.coins)))
            {
                if (price.HasResources) ledger.Add(price.resources);

                reason = $"Need {price.coins} {CoinName(coins)}.";
                return false;
            }

            return true;
        }

        public static string CoinName(IWallet coins) => coins != null ? coins.DisplayName : "Coins";
    }
}
