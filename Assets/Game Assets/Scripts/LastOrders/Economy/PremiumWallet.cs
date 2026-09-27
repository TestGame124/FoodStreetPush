namespace LastOrders.Economy
{
    /// <summary>
    /// A balance held outside the Last Orders ledger, as far as the systems that charge it need
    /// to know: a name, a balance and an all-or-nothing spend.
    ///
    /// An interface rather than a direct call into the level's wallet, so the services that
    /// charge it can be tested without that wallet, and so a real store integration later
    /// replaces one class rather than every place that charges.
    /// </summary>
    public interface IWallet
    {
        /// <summary>What the player calls it. "Gems", "Coins".</summary>
        string DisplayName { get; }

        int Balance { get; }

        /// <summary>Spends the whole amount or nothing, and says which.</summary>
        bool TrySpend(int amount);
    }

    /// <summary>The premium currency. A wallet like any other; the name says which one a price
    /// is asking for.</summary>
    public interface IPremiumWallet : IWallet
    {
    }

    /// <summary>
    /// The level's own Gems. The Last Orders ledger has no premium currency of its own, and a
    /// second one beside the wallet the HUD already shows would be two balances for one thing.
    /// </summary>
    public sealed class GemWallet : IPremiumWallet
    {
        public string DisplayName => "Gems";

        public int Balance => CurrenciesController.GetCurrencyAmount(CurrencyType.Gems);

        public bool TrySpend(int amount) => CurrenciesController.SubtractCurrency(CurrencyType.Gems, amount);
    }

    /// <summary>
    /// The level's own Coins - what visitors pay into and what the HUD's counter shows.
    ///
    /// Not the ledger's Coin. In the working level nothing credits that one after the starting
    /// kit, so a price charged there would be charged against a balance the player can neither
    /// see nor earn.
    /// </summary>
    public sealed class CoinWallet : IWallet
    {
        public string DisplayName => "Coins";

        public int Balance => CurrenciesController.GetCurrencyAmount(CurrencyType.Coins);

        public bool TrySpend(int amount) => CurrenciesController.SubtractCurrency(CurrencyType.Coins, amount);
    }
}
