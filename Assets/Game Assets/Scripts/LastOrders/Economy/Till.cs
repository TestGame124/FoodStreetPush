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
        /// <summary>The first thing the player is short of, in words. Null when they have it all.</summary>
        public static string Shortfall(Price price, ResourceLedger ledger, IWallet coins)
        {
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
