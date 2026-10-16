using System;
using System.Collections.Generic;
using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Economy
{
    /// <summary>
    /// The player's stores (19). One ledger for all six resource families rather than a
    /// field per currency, so a cost is a list and every system spends the same way.
    ///
    /// Two rules from 15.6 are enforced here rather than at the call sites that would
    /// otherwise have to remember them: theft is capped per resource per night, and
    /// anything in the cellar cannot be stolen at all. Protecting stores is then a build
    /// decision the player controls, rather than something the game does to them.
    /// </summary>
    public sealed class ResourceLedger
    {
        readonly Dictionary<ResourceType, int> balances = new Dictionary<ResourceType, int>();
        readonly Dictionary<ResourceType, int> cellarProtected = new Dictionary<ResourceType, int>();
        readonly IResourceWallet coins;

        public ResourceLedger(IResourceWallet coins = null) { this.coins = coins; }

        /// <summary>Raised on any balance change, with the type and its new amount.</summary>
        public event Action<ResourceType, int> OnBalanceChanged;

        public int Get(ResourceType type)
        {
            type = type.Canonical();
            if (type == ResourceType.Coin && coins != null) return coins.Balance;
            return balances.TryGetValue(type, out int value) ? value : 0;
        }

        /// <summary>Portion of a resource stored in a cellar and therefore unstealable.</summary>
        public int GetProtected(ResourceType type) =>
            cellarProtected.TryGetValue(type.Canonical(), out int value) ? value : 0;

        public void Add(ResourceType type, int amount)
        {
            if (amount == 0) return;
            type = type.Canonical();
            int next = (int)Math.Max(0L, Math.Min(int.MaxValue, (long)Get(type) + amount));
            if (type == ResourceType.Coin && coins != null)
            {
                int delta = next - coins.Balance;
                if (delta > 0) coins.Add(delta);
                else if (delta < 0) coins.TrySpend(-delta);
                next = coins.Balance;
            }
            balances[type] = next;
            OnBalanceChanged?.Invoke(type, next);
        }

        public void Add(ResourceAmount[] amounts, int multiplier = 1)
        {
            if (amounts == null) return;
            for (int i = 0; i < amounts.Length; i++)
                Add(amounts[i].type, amounts[i].amount * multiplier);
        }

        public bool CanAfford(ResourceAmount[] cost)
        {
            if (cost == null) return true;

            // Summed rather than checked per entry, so a cost that lists the same resource
            // twice is still validated correctly.
            var required = new Dictionary<ResourceType, int>();
            for (int i = 0; i < cost.Length; i++)
            {
                if (cost[i].amount < 0) return false;
                ResourceType type = cost[i].type.Canonical();
                required.TryGetValue(type, out int existing);
                if ((long)existing + cost[i].amount > int.MaxValue) return false;
                required[type] = existing + cost[i].amount;
            }

            foreach (KeyValuePair<ResourceType, int> entry in required)
                if (Get(entry.Key) < entry.Value) return false;

            return true;
        }

        /// <summary>
        /// All-or-nothing. A partial spend would let a player start a build they cannot
        /// finish and leave the ledger in a state no undo could describe.
        /// </summary>
        public bool TrySpend(ResourceAmount[] cost)
        {
            if (!CanAfford(cost)) return false;

            if (cost != null)
                for (int i = 0; i < cost.Length; i++)
                    Add(cost[i].type, -cost[i].amount);

            return true;
        }

        /// <summary>Puts resources beyond the reach of theft. Capacity is the caller's
        /// concern - the cellar buildable decides how much it holds.</summary>
        public void SetCellarProtection(ResourceType type, int amount)
        {
            cellarProtected[type.Canonical()] = Mathf.Max(0, amount);
        }

        /// <summary>
        /// Resolves a night's theft. Returns what was actually taken, which is what the dawn
        /// damage report shows.
        ///
        /// 15.6 caps this at a fraction of any single resource so a bad night never zeroes
        /// the player's ability to act, and exempts the cellar entirely.
        /// </summary>
        public ResourceAmount[] ApplyTheft(ResourceAmount[] attempted, float maxFractionPerResource)
        {
            if (attempted == null || attempted.Length == 0) return Array.Empty<ResourceAmount>();

            var taken = new List<ResourceAmount>(attempted.Length);

            for (int i = 0; i < attempted.Length; i++)
            {
                ResourceType type = attempted[i].type;

                int stealable = Mathf.Max(0, Get(type) - GetProtected(type));
                if (stealable <= 0) continue;

                int cap = Mathf.FloorToInt(Get(type) * Mathf.Clamp01(maxFractionPerResource));
                int amount = Mathf.Min(attempted[i].amount, Mathf.Min(stealable, cap));
                if (amount <= 0) continue;

                Add(type, -amount);
                taken.Add(new ResourceAmount(type, amount));
            }

            return taken.ToArray();
        }

        public IReadOnlyDictionary<ResourceType, int> All
        {
            get
            {
                if (coins != null) balances[ResourceType.Coin] = coins.Balance;
                return balances;
            }
        }
    }
}
