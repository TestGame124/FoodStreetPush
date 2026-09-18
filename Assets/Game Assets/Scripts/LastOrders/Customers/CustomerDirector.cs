using System;
using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Reputation;
using UnityEngine;

namespace LastOrders.Customers
{
    /// <summary>One scheduled party arrival.</summary>
    public readonly struct ScheduledParty
    {
        public readonly CustomerArchetypeDefinition Archetype;
        public readonly int Size;

        /// <summary>Clock-minute of the working day this party walks in at.</summary>
        public readonly float ClockMinute;

        /// <summary>The guaranteed high-value party of 9.4. Flagged so the UI can make the
        /// moment legible - this is the beat the store page screenshots.</summary>
        public readonly bool IsLastParty;

        public ScheduledParty(CustomerArchetypeDefinition archetype, int size, float clockMinute, bool isLastParty)
        {
            Archetype = archetype;
            Size = size;
            ClockMinute = clockMinute;
            IsLastParty = isLastParty;
        }
    }

    /// <summary>
    /// Decides who arrives and when (9).
    ///
    /// The day's rhythm is entirely in the distribution, not in the total. Arrivals are not
    /// uniform: a lunch rush at clock-minute 20 to 30 takes 30 percent of the day's parties,
    /// a lull follows for repairs and repositioning, a dinner rush at 40 to 50 takes another
    /// 20, and then comes the dangerous tail. Every party arriving after minute 50 is a
    /// decision, because serving them is money and Reputation and Heat, and they will not be
    /// finished by dusk.
    ///
    /// The whole schedule is built at dawn rather than rolled minute by minute. That is what
    /// lets the game promise the Last Party (9.4) exists tonight before the day has started,
    /// and it means a save written at minute 30 restores a day whose remaining arrivals are
    /// exactly the ones it was going to get.
    /// </summary>
    public sealed class CustomerDirector
    {
        readonly GameBalanceConfig balance;
        readonly ReputationService reputation;

        readonly List<ScheduledParty> schedule = new List<ScheduledParty>();
        int nextIndex;

        /// <summary>Raised when a party reaches the road tile. The spawner subscribes.</summary>
        public event Action<ScheduledParty> OnPartyArrived;

        public CustomerDirector(GameBalanceConfig balance, ReputationService reputation)
        {
            this.balance = balance;
            this.reputation = reputation;
        }

        public IReadOnlyList<ScheduledParty> Schedule => schedule;

        /// <summary>Parties that have not yet walked in. Shown at dawn as the day's forecast.</summary>
        public int Remaining => Mathf.Max(0, schedule.Count - nextIndex);

        /// <summary>
        /// Builds the day's arrivals. Called once at dawn.
        /// </summary>
        public void PlanDay(int day, float menuVariety, float comfort, float damageState, float factionEvent)
        {
            schedule.Clear();
            nextIndex = 0;

            int pool = reputation.DailyPartyPool(menuVariety, comfort, damageState, factionEvent);
            if (pool <= 0) return;

            var rng = DeterministicRandom.ForNight(day, 0, balance.campaignSeed, 4242);
            int tier = reputation.Tier;

            var archetypes = EligibleArchetypes(tier);
            if (archetypes.Count == 0) return;

            var weights = new ArchetypeWeights(archetypes, tier);

            for (int i = 0; i < pool; i++)
            {
                int index = rng.WeightedIndex(weights);
                if (index < 0) break;

                CustomerArchetypeDefinition archetype = archetypes[index];

                float clockMinute = DrawArrivalMinute(ref rng);
                int size = DrawPartySize(archetype, ref rng);

                schedule.Add(new ScheduledParty(archetype, size, clockMinute, false));
            }

            InsertLastParty(archetypes, ref rng);

            schedule.Sort((a, b) => a.ClockMinute.CompareTo(b.ClockMinute));
        }

        /// <summary>
        /// Draws an arrival time from the 9.3 block distribution. Two rushes, with deliberate
        /// breathing room between them where the player repairs and repositions.
        /// </summary>
        float DrawArrivalMinute(ref DeterministicRandom rng)
        {
            float[] curve = balance.arrivalCurveByBlock;
            if (curve == null || curve.Length == 0)
                return rng.Range(0f, DayCycle.TimeService.MinutesPerDay);

            float total = 0f;
            for (int i = 0; i < curve.Length; i++) total += curve[i];

            float roll = rng.NextFloat() * total;
            float blockMinutes = DayCycle.TimeService.MinutesPerDay / curve.Length;

            for (int i = 0; i < curve.Length; i++)
            {
                roll -= curve[i];
                if (roll > 0f) continue;

                // Uniform within the chosen block, so arrivals do not visibly clump on the
                // block boundaries.
                return i * blockMinutes + rng.Range(0f, blockMinutes);
            }

            return DayCycle.TimeService.MinutesPerDay;
        }

        int DrawPartySize(CustomerArchetypeDefinition archetype, ref DeterministicRandom rng)
        {
            // The archetype's own range wins where it is narrower than the global
            // distribution: a Family is never a party of one regardless of the weights.
            if (archetype.minPartySize == archetype.maxPartySize) return archetype.minPartySize;

            float[] weights = balance.partySizeWeights;
            if (weights == null || weights.Length == 0)
                return rng.Range(archetype.minPartySize, archetype.maxPartySize + 1);

            float total = 0f;
            for (int i = 0; i < weights.Length; i++) total += weights[i];

            float roll = rng.NextFloat() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll <= 0f)
                    return Mathf.Clamp(i + 1, archetype.minPartySize, archetype.maxPartySize);
            }

            return archetype.maxPartySize;
        }

        /// <summary>
        /// Guarantees the 9.4 beat: one high-value party lands between clock-minute 48 and 52,
        /// with no chance of finishing before dusk.
        ///
        /// The GDD is explicit that this must be designed rather than emergent, because it is
        /// the moment that sells the game: seat them for the full reward and four civilians to
        /// evacuate, turn them away and lose Reputation, or feed them at the door for half.
        /// </summary>
        void InsertLastParty(List<CustomerArchetypeDefinition> archetypes, ref DeterministicRandom rng)
        {
            CustomerArchetypeDefinition best = null;
            int bestValue = int.MinValue;

            for (int i = 0; i < archetypes.Count; i++)
            {
                // Largest party wins ties on payment, because the drama is in the number of
                // people the player has to decide about.
                int value = archetypes[i].baseCoinPayment * archetypes[i].maxPartySize;
                if (value <= bestValue) continue;

                bestValue = value;
                best = archetypes[i];
            }

            if (best == null) return;

            float minute = rng.Range(balance.lastPartyWindow.x, balance.lastPartyWindow.y);
            schedule.Add(new ScheduledParty(best, best.maxPartySize, minute, true));
        }

        /// <summary>
        /// Releases every party due by the given clock-minute. Driven by the TimeService
        /// minute tick, so a long frame that crosses several minutes still emits all of them.
        /// </summary>
        public void ReleaseDue(float clockMinute)
        {
            while (nextIndex < schedule.Count && schedule[nextIndex].ClockMinute <= clockMinute)
            {
                OnPartyArrived?.Invoke(schedule[nextIndex]);
                nextIndex++;
            }
        }

        List<CustomerArchetypeDefinition> EligibleArchetypes(int tier)
        {
            var result = new List<CustomerArchetypeDefinition>();
            if (balance.customerArchetypes == null) return result;

            for (int i = 0; i < balance.customerArchetypes.Length; i++)
            {
                CustomerArchetypeDefinition archetype = balance.customerArchetypes[i];
                if (archetype != null && archetype.WeightForTier(tier) > 0f) result.Add(archetype);
            }

            return result;
        }

        public void RestoreFromSave(int consumed) => nextIndex = Mathf.Clamp(consumed, 0, schedule.Count);

        public int Consumed => nextIndex;

        sealed class ArchetypeWeights : IReadOnlyWeights
        {
            readonly List<CustomerArchetypeDefinition> archetypes;
            readonly int tier;

            public ArchetypeWeights(List<CustomerArchetypeDefinition> archetypes, int tier)
            {
                this.archetypes = archetypes;
                this.tier = tier;
            }

            public int Count => archetypes.Count;

            public float WeightAt(int index) => archetypes[index].WeightForTier(tier);
        }
    }
}
