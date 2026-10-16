using LastOrders.DayCycle;
using UnityEngine;

namespace LastOrders.Core
{
    /// <summary>
    /// The seam between the existing restaurant code and the Last Orders systems.
    ///
    /// The old visitor and cooking code predates all of this and knows nothing about Heat.
    /// Rather than thread a service reference through it - which would mean touching the
    /// constructor of every one of those components - it calls a static notifier, and this
    /// resolves the composition the first time it is asked.
    ///
    /// A static is the right shape here precisely because it is temporary. As each old
    /// system is properly refactored into its Last Orders equivalent it stops calling this,
    /// and when the last caller goes, so does this file. Being able to grep for the callers
    /// is the point: they are the remaining migration work, listed.
    ///
    /// Every method is a no-op when the composition is absent, so the old scenes that have
    /// no Last Orders layer keep working untouched.
    /// </summary>
    public static class LastOrdersBridge
    {
        static GameLifetimeScope cachedScope;

        /// <summary>
        /// The composition, found through the scene's scope.
        ///
        /// The SCOPE is cached rather than the composition, and the search is retried whenever
        /// that scope has gone. Caching a null after one miss is how this quietly stops working
        /// for the rest of the session - and with domain reloading off, for the rest of the
        /// editor session too.
        /// </summary>
        static GameComposition Composition
        {
            get
            {
                if (cachedScope == null) cachedScope = Object.FindFirstObjectByType<GameLifetimeScope>();
                return cachedScope != null ? cachedScope.Composition : null;
            }
        }

        /// <summary>Clears the cache on scene change, so a reloaded scene does not hold a
        /// reference to the previous session's composition.</summary>
        public static void Reset()
        {
            cachedScope = null;
            warnedNoLevelSystem = false;
        }

        /// <summary>
        /// A customer finished a meal and left satisfied.
        ///
        /// 11.1: every customer served is +2 Heat. This is the line that makes the central
        /// tension real - the player's own success is what builds tonight's wave.
        /// </summary>
        public static void NotifyCustomerServed(int partySize = 1)
        {
            GameComposition composition = Composition;
            if (composition == null) return;

            composition.Heat.AddCustomerServed(Mathf.Max(1, partySize));
            composition.Context.Report.CustomersServed += Mathf.Max(1, partySize);
        }

        /// <summary>
        /// A dish came off a station. Grilled and smoked pass 2, which is what makes the menu
        /// itself a risk decision (8).
        ///
        /// <paramref name="servings"/> is what puts the dish on the pass, and it is the join
        /// between the level's own kitchen and the customer system's food gate. Travellers only
        /// turn off the road when there is something to sell them; without this a level with a
        /// perfectly good working kitchen would starve its own door.
        /// </summary>
        public static void NotifyDishCooked(float heatMultiplier = 1f, int servings = 0)
        {
            GameComposition composition = Composition;
            if (composition == null) return;

            composition.Heat.AddDishCooked(heatMultiplier);

            if (servings <= 0) return;

            // Noted, not added. The level's own kitchen has already put the dish in the
            // player's inventory, and the pass reads that inventory - adding it again here
            // would be a second tally of the same bowl.
            composition.Meals.NoteCooked(servings);

            // The first dish is half of what the sign waits on. Set here rather than only in
            // the grey-box stove, so opening up works in whichever kitchen the level has.
            composition.Progression.Set(Progression.ProgressionFlags.FirstMealCooked);
        }

        /// <summary>
        /// A customer paid, and the level's own wallet should show it.
        ///
        /// Credits the shared coin wallet. Ledger Coin lookups read the same balance.
        /// </summary>
        public static void NotifyCoinsPaid(int amount)
        {
            if (amount <= 0) return;

            CurrenciesController.AddCurrency(CurrencyType.Coins, amount);
        }

        static bool warnedNoLevelSystem;

        /// <summary>
        /// Experience earned, into the level's own XP bar.
        ///
        /// The player level still lives in the legacy LevelUpSystem, which is what the XP bar
        /// and the level-up popup read. Guarded on its instance: a level-up pays through a
        /// static reward manager that only exists once that component has woken, and without
        /// the guard a scene without it throws on the first level gained.
        /// </summary>
        public static void AddExperience(int amount)
        {
            if (amount <= 0) return;

            if (LevelUpSystem.Instance == null)
            {
                if (!warnedNoLevelSystem)
                {
                    Debug.LogWarning("[Last Orders] No initialised LevelUpSystem in this scene, so " +
                                     $"{amount} XP (and any after it) had nowhere to go.");
                    warnedNoLevelSystem = true;
                }

                return;
            }

            LevelUpSystem.AddXP(amount);
        }

        /// <summary>The player's level, which still lives in the legacy LevelUpSystem.</summary>
        public static int PlayerLevel => LevelUpSystem.GetLevel();

        /// <summary>Coin earned, for the dawn report's takings line.</summary>
        public static void NotifyCoinEarned(int amount)
        {
            GameComposition composition = Composition;
            if (composition == null || amount <= 0) return;

            composition.Context.Report.CoinEarned += amount;
        }

        /// <summary>
        /// Whether the level's waiting staff may start a delivery.
        ///
        /// The legacy restaurant gated this on a claimed order, because an order being claimed
        /// was what put food in the kitchen. Under Last Orders the food is already off the pass
        /// before a server is involved - the customer takes it when they sit down - so the
        /// thing that authorises the trip is a seated customer, not a counter. Leaving the old
        /// gate in place would have servers stand at the counter while people wait at tables.
        ///
        /// A scene with no Last Orders layer keeps the old rule exactly.
        /// </summary>
        public static bool ServiceGateSatisfied
        {
            get
            {
                if (Composition != null) return true;

                return OrderSystem.instance != null && OrderSystem.instance.TotalOrdersClaimed > 0;
            }
        }

        /// <summary>True while the restaurant is open for service. The old spawner can ask
        /// this instead of spawning visitors through the night.</summary>
        public static bool IsServing
        {
            get
            {
                GameComposition composition = Composition;
                return composition == null || composition.Machine.CurrentPhase.AllowsService();
            }
        }
    }
}
