using System;
using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Reputation
{
    /// <summary>
    /// The single 0-100 stat of 10. Drives walk-in volume and quality, Survivor recruitment
    /// tier, trader inventory, expansion gates and story beats.
    ///
    /// Two rules do most of the design work here.
    ///
    /// It decays when the restaurant is closed, badly damaged, or serving poorly. That makes
    /// turtling a losing strategy: a player who bricks up every door slowly loses by
    /// attrition, which is correct for a game whose thesis is that safety is unaffordable.
    ///
    /// It never drops below the highest tier milestone reached. Tiers are permanent unlocks,
    /// and this is one of the guarantees 15.1 makes absolute - a player cannot be demoted
    /// out of content they have already earned, no matter how bad a run of nights they have.
    /// </summary>
    public sealed class ReputationService
    {
        readonly GameBalanceConfig balance;

        float current;

        /// <summary>The highest tier ever reached. The floor under <see cref="Current"/>,
        /// and the thing that makes progress impossible to lose.</summary>
        public int HighestTierReached { get; private set; }

        public float Current => current;

        public int Tier => balance.ReputationTier(current);

        /// <summary>Raised on any change, with the new value. Nothing polls this.</summary>
        public event Action<float> OnReputationChanged;

        /// <summary>Raised when the tier steps up, with the new tier. Traders restock, better
        /// clientele start arriving, and an expansion may have just unlocked - so this is a
        /// loud, celebratory moment, not a quiet stat change.</summary>
        public event Action<int> OnTierReached;

        public ReputationService(GameBalanceConfig balance)
        {
            this.balance = balance != null
                ? balance
                : throw new ArgumentNullException(nameof(balance));
        }

        /// <summary>The Reputation value below which the player can never fall, given the
        /// tiers they have already unlocked.</summary>
        public float Floor
        {
            get
            {
                if (balance.reputationTiers == null || balance.reputationTiers.Length == 0)
                    return 0f;

                int index = Mathf.Clamp(HighestTierReached, 0, balance.reputationTiers.Length - 1);
                return balance.reputationTiers[index];
            }
        }

        /// <summary>
        /// The single mutation point. Clamps to the permanent floor and to 100, and raises
        /// the tier event when a new band is entered.
        /// </summary>
        public void Add(float delta)
        {
            if (Mathf.Approximately(delta, 0f)) return;

            current = Mathf.Clamp(current + delta, Floor, 100f);

            // The tier event goes first so that a subscriber handling the value change
            // already sees the unlock it implies, rather than reacting to a value whose
            // tier has not been announced yet.
            int tier = Tier;
            if (tier > HighestTierReached)
            {
                HighestTierReached = tier;
                OnTierReached?.Invoke(tier);
            }

            OnReputationChanged?.Invoke(current);
        }

        /// <summary>The daily walk-in pool of 9.2: BasePool = 4 + floor(Reputation x 0.24),
        /// before the menu-variety, comfort, damage and faction multipliers are applied.</summary>
        public int BasePartyPool() =>
            balance.basePartyCount + Mathf.FloorToInt(current * balance.reputationToParties);

        /// <summary>
        /// Applies the full 9.2 multiplier chain. Kept here rather than in the customer
        /// director so that the dawn projection the player is shown and the number actually
        /// spawned come from one function.
        /// </summary>
        public int DailyPartyPool(float menuVariety, float comfort, float damageState, float factionEvent)
        {
            float pool = BasePartyPool()
                         * Mathf.Clamp(menuVariety, 0.8f, 1.3f)
                         * Mathf.Clamp(comfort, 0.9f, 1.2f)
                         * Mathf.Clamp(damageState, 0.5f, 1.0f)
                         * Mathf.Clamp(factionEvent, 1.0f, 1.5f);

            return Mathf.Max(0, Mathf.RoundToInt(pool));
        }

        /// <summary>
        /// Applied at Resolution. Turtling has to lose slowly, so a day spent closed or
        /// badly damaged costs Reputation even though nothing was destroyed.
        /// </summary>
        public void ApplyDailyDecay(bool wasClosed, bool badlyDamaged, bool servedPoorly)
        {
            if (!wasClosed && !badlyDamaged && !servedPoorly) return;

            int reasons = (wasClosed ? 1 : 0) + (badlyDamaged ? 1 : 0) + (servedPoorly ? 1 : 0);
            Add(-balance.reputationDecayPerDay * reasons);
        }

        public void RestoreFromSave(float value, int highestTier)
        {
            HighestTierReached = Mathf.Max(0, highestTier);
            current = Mathf.Clamp(value, Floor, 100f);
            OnReputationChanged?.Invoke(current);
        }
    }
}
