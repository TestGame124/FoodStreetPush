using System;
using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Economy;
using LastOrders.Reputation;
using LastOrders.Survivors;
using UnityEngine;

namespace LastOrders.Dispatch
{
    /// <summary>A Survivor currently out in the world.</summary>
    public sealed class ActiveDispatch
    {
        public DispatchDestinationDefinition Destination;
        public Survivor Assigned;
        public int DaysRemaining;

        /// <summary>The seed this dispatch will resolve with, fixed at assignment. Rolling it
        /// now rather than on return means the outcome cannot be changed by save-scumming the
        /// intervening nights.</summary>
        public int ResolutionSeed;
    }

    /// <summary>
    /// The world beyond the walls (12). It exists, it matters, and it supplies what the
    /// player cannot build - but it is narrated, not played.
    ///
    /// 12.2 is the whole argument for this shape. A destination is a data row plus two to
    /// four vignettes, so fifty of them is a week of writing rather than six months of level
    /// design, and it scales infinitely in LiveOps as pure content patches. It preserves
    /// everything the cut expedition mode was for: risk, resource inflow, blueprints that
    /// visibly change the restaurant, and attachment to people.
    ///
    /// The real cost is never the risk roll. It is that the Survivor is gone: their labour
    /// absent from the kitchen, their body absent from the night watch, felt immediately.
    /// </summary>
    public sealed class DispatchService
    {
        readonly GameBalanceConfig balance;
        readonly ResourceLedger ledger;
        readonly ReputationService reputation;
        readonly SurvivorService survivors;

        readonly List<DispatchDestinationDefinition> board = new List<DispatchDestinationDefinition>();
        readonly List<ActiveDispatch> active = new List<ActiveDispatch>();

        /// <summary>Raised when a dispatch returns and a vignette should be shown.</summary>
        public event Action<ActiveDispatch, DispatchDestinationDefinition.Outcome> OnResolved;

        /// <summary>Raised when a dispatch returns with a choice to present. The player picks,
        /// then calls <see cref="ApplyChoice"/>.</summary>
        public event Action<ActiveDispatch, DispatchDestinationDefinition.Choice> OnChoicePresented;

        public DispatchService(GameBalanceConfig balance, ResourceLedger ledger,
                               ReputationService reputation, SurvivorService survivors)
        {
            this.balance = balance;
            this.ledger = ledger;
            this.reputation = reputation;
            this.survivors = survivors;
        }

        public IReadOnlyList<DispatchDestinationDefinition> Board => board;

        public IReadOnlyList<ActiveDispatch> Active => active;

        /// <summary>Rebuilds the dawn board with 3 to 5 rotating destination cards.</summary>
        public void RefreshBoard(int day)
        {
            board.Clear();
            if (balance.dispatchDestinations == null) return;

            var eligible = new List<DispatchDestinationDefinition>();
            for (int i = 0; i < balance.dispatchDestinations.Length; i++)
            {
                DispatchDestinationDefinition destination = balance.dispatchDestinations[i];
                if (destination == null) continue;
                if (day < destination.earliestDay) continue;
                if (reputation.Current < destination.requiredReputation) continue;

                eligible.Add(destination);
            }

            if (eligible.Count == 0) return;

            var rng = DeterministicRandom.ForNight(day, 0, balance.campaignSeed, 9001);
            int count = Mathf.Min(rng.Range(3, 6), eligible.Count);

            for (int i = 0; i < count; i++)
            {
                int index = rng.Range(0, eligible.Count);
                board.Add(eligible[index]);
                eligible.RemoveAt(index);
            }
        }

        /// <summary>
        /// Sends someone. Returns false if they are unavailable, which is the only failure
        /// mode - there is no way to lose a person here.
        /// </summary>
        public bool Assign(DispatchDestinationDefinition destination, Survivor survivor, int day)
        {
            if (destination == null || survivor == null || !survivor.IsAvailable) return false;

            survivor.AwayDaysRemaining = destination.durationDays;

            active.Add(new ActiveDispatch
            {
                Destination = destination,
                Assigned = survivor,
                DaysRemaining = destination.durationDays,
                // Seeded from a stable hash, not string.GetHashCode: this value has to survive
                // the app being closed and reopened, or a dispatch saved today would resolve
                // differently tomorrow.
                ResolutionSeed = unchecked(balance.campaignSeed * 31 + day * 17 +
                                           DeterministicRandom.StableHash(survivor.Id)),
            });

            board.Remove(destination);
            return true;
        }

        /// <summary>Ticks every active dispatch. Called once at Resolution.</summary>
        public void AdvanceDay()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                active[i].DaysRemaining--;
                if (active[i].DaysRemaining > 0) continue;

                Resolve(active[i]);
                active.RemoveAt(i);
            }
        }

        void Resolve(ActiveDispatch dispatch)
        {
            DispatchDestinationDefinition destination = dispatch.Destination;
            var rng = new DeterministicRandom(dispatch.ResolutionSeed);

            // A choice, where one exists, beats a plain roll. The vignette with a decision in
            // it is the content players remember; the bare outcome is the fallback.
            if (destination.choices != null && destination.choices.Length > 0)
            {
                int index = rng.Range(0, destination.choices.Length);
                OnChoicePresented?.Invoke(dispatch, destination.choices[index]);
                return;
            }

            DispatchDestinationDefinition.Outcome outcome = RollOutcome(destination, ref rng);
            ApplyOutcome(dispatch, outcome);
        }

        DispatchDestinationDefinition.Outcome RollOutcome(
            DispatchDestinationDefinition destination, ref DeterministicRandom rng)
        {
            var legal = new List<DispatchDestinationDefinition.Outcome>();

            for (int i = 0; i < destination.outcomes.Length; i++)
            {
                DispatchDestinationDefinition.Outcome outcome = destination.outcomes[i];

                // 12.3's guarantee, enforced at the roll rather than trusted to the data: a
                // cautious player is never blindsided, so Stranded cannot come up on Low risk
                // even if someone authored it there by mistake.
                if (outcome.tier == DispatchOutcomeTier.Stranded && !destination.AllowsStranded)
                    continue;

                legal.Add(outcome);
            }

            if (legal.Count == 0) return default;

            var weights = new OutcomeWeights(legal);
            int index = rng.WeightedIndex(weights);
            return index >= 0 ? legal[index] : legal[0];
        }

        /// <summary>Applies the player's pick from a presented vignette.</summary>
        public void ApplyChoice(ActiveDispatch dispatch, DispatchDestinationDefinition.Choice choice, bool tookOptionA)
        {
            ApplyOutcome(dispatch, tookOptionA ? choice.optionA : choice.optionB);
        }

        void ApplyOutcome(ActiveDispatch dispatch, DispatchDestinationDefinition.Outcome outcome)
        {
            ledger.Add(outcome.haul);

            if (outcome.reputationDelta != 0) reputation.Add(outcome.reputationDelta);

            if (outcome.extraDaysAway > 0)
            {
                // Setback and Stranded both cost time and nothing else. The Survivor always
                // comes home - 12.3 is explicit that there is no permanent loss.
                if (outcome.tier == DispatchOutcomeTier.Setback)
                    survivors.Injure(dispatch.Assigned, outcome.extraDaysAway);
                else
                    dispatch.Assigned.AwayDaysRemaining += outcome.extraDaysAway;
            }

            OnResolved?.Invoke(dispatch, outcome);
        }

        sealed class OutcomeWeights : IReadOnlyWeights
        {
            readonly List<DispatchDestinationDefinition.Outcome> outcomes;

            public OutcomeWeights(List<DispatchDestinationDefinition.Outcome> outcomes)
            {
                this.outcomes = outcomes;
            }

            public int Count => outcomes.Count;

            public float WeightAt(int index) => Mathf.Max(0f, outcomes[index].weight);
        }
    }
}
