using System;
using LastOrders.Data;
using LastOrders.Economy;
using LastOrders.Progression;

namespace LastOrders.Expansion
{
    /// <summary>
    /// Pays a <see cref="RewardBundle"/> exactly once per reward ID.
    ///
    /// The once-only rule is the progression service's; this adds the two places a bundle
    /// goes. Experience is handed to a delegate rather than to the legacy level system
    /// directly, so the rules can be tested without a scene and the XP can move to a Last
    /// Orders system later without anything here changing.
    /// </summary>
    public sealed class RewardService
    {
        readonly ProgressionService progression;
        readonly ResourceLedger ledger;
        readonly Action<int> addExperience;

        public RewardService(ProgressionService progression, ResourceLedger ledger,
                             Action<int> addExperience)
        {
            this.progression = progression;
            this.ledger = ledger;
            this.addExperience = addExperience;
        }

        /// <summary>Pays the bundle. Returns false, paying nothing, if this ID already paid.</summary>
        public bool Grant(string rewardId, RewardBundle bundle)
        {
            return progression.GrantOnce(rewardId, () =>
            {
                if (bundle.resources != null) ledger.Add(bundle.resources);
                if (bundle.experience > 0) addExperience?.Invoke(bundle.experience);
            });
        }
    }
}
