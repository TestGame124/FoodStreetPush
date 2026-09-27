using System;
using System.Text;

namespace LastOrders.Data
{
    /// <summary>
    /// What finishing something pays: experience plus a small bag of resources.
    ///
    /// One type for every payout in the expansion loop - a cleared pile and a claimed area
    /// pay the same shape - so the grant path, the idempotency and the "+2 XP" text are
    /// written once.
    /// </summary>
    [Serializable]
    public struct RewardBundle
    {
        public int experience;
        public ResourceAmount[] resources;

        public RewardBundle(int experience, params ResourceAmount[] resources)
        {
            this.experience = experience;
            this.resources = resources;
        }

        public bool IsEmpty => experience <= 0 && (resources == null || resources.Length == 0);

        /// <summary>"+2 XP  +3 Wood", for the floating text and the tap reply.</summary>
        public string Describe()
        {
            var text = new StringBuilder();

            if (experience > 0) text.Append($"+{experience} XP");

            if (resources != null)
            {
                for (int i = 0; i < resources.Length; i++)
                {
                    if (resources[i].amount <= 0) continue;
                    if (text.Length > 0) text.Append("  ");
                    text.Append($"+{resources[i].amount} {resources[i].type}");
                }
            }

            return text.ToString();
        }
    }
}
