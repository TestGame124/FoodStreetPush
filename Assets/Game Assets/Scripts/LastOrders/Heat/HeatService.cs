using System;
using System.Collections.Generic;
using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Heat
{
    /// <summary>Where a day's Heat came from. Kept per source so the dawn report can tell the
    /// player which of their choices cost them the night, rather than showing one number.</summary>
    public enum HeatSource
    {
        CustomersServed,
        Cooking,
        Lighting,
        Generators,
        Music,
        Footprint,
        Reputation,
        Corpses,
        Walls,
        Enemies,
        Reductions,
    }

    /// <summary>
    /// The central system (11). If only one thing survives contact with production, it is
    /// this one: Heat is what binds the builder to the defense game, and without it they
    /// are two games sharing a currency.
    ///
    /// The player watches themselves cook their way into tonight's problem, in real time,
    /// on a bar they cannot ignore. That is the whole design, and it works with grey boxes,
    /// which is why it is the Phase 1 prototype target.
    ///
    /// Two invariants worth stating because breaking either collapses the tension model:
    ///
    /// Every reduction costs revenue. There is never a free safety. A free Heat sink would
    /// let the player buy their way out of the central decision, which is why 20 also bans
    /// monetising Heat reduction.
    ///
    /// Heat locks at dusk. What the forecast marker promised during the day is what the
    /// WaveDirector consumes, so the bar the player was watching is the bar that mattered.
    /// </summary>
    public sealed class HeatService
    {
        readonly HeatWeightsConfig weights;
        readonly Dictionary<HeatSource, float> bySource = new Dictionary<HeatSource, float>();

        /// <summary>Heat accrued so far today. Reset at dawn.</summary>
        public float CurrentHeat { get; private set; }

        /// <summary>The value locked in at dusk and handed to the WaveDirector. Distinct from
        /// <see cref="CurrentHeat"/> so that anything still emitting Heat during the siege
        /// (a Screecher, a fire) cannot retroactively change the wave it is part of.</summary>
        public float LockedHeat { get; private set; }

        public bool IsLocked { get; private set; }

        /// <summary>Raised whenever Heat changes, with the new total. The bar subscribes;
        /// nothing polls.</summary>
        public event Action<float> OnHeatChanged;

        /// <summary>Raised when the projected wave tier changes. This is the forecast marker
        /// stepping up, and it should be loud - it is the game telling the player they have
        /// just crossed a line.</summary>
        public event Action<int> OnForecastTierChanged;

        int lastForecastTier;

        public HeatService(HeatWeightsConfig weights)
        {
            this.weights = weights != null
                ? weights
                : throw new ArgumentNullException(nameof(weights));
        }

        /// <summary>The tier tonight currently projects to. Drawn on the Heat bar all day.
        /// Reads the same function the WaveDirector uses, so the forecast cannot lie.</summary>
        public int ForecastTier => weights.TierForHeat(IsLocked ? LockedHeat : CurrentHeat);

        public float HeatFrom(HeatSource source) =>
            bySource.TryGetValue(source, out float value) ? value : 0f;

        public IReadOnlyDictionary<HeatSource, float> Breakdown => bySource;

        /// <summary>
        /// The single mutation point. Everything that adds or removes Heat comes through
        /// here, which is what makes the dawn breakdown trustworthy and what gives the
        /// debug overlay one place to watch.
        /// </summary>
        public void Add(float amount, HeatSource source)
        {
            if (IsLocked)
            {
                // Not an error: a Screecher raising Heat during the siege is legitimate
                // gameplay. It just must not change tonight's already-committed wave, so it
                // is recorded for tomorrow's report and nothing else.
                Accumulate(amount, source);
                return;
            }

            if (Mathf.Approximately(amount, 0f)) return;

            CurrentHeat = Mathf.Max(0f, CurrentHeat + amount);
            Accumulate(amount, source);

            OnHeatChanged?.Invoke(CurrentHeat);

            int tier = ForecastTier;
            if (tier != lastForecastTier)
            {
                lastForecastTier = tier;
                OnForecastTierChanged?.Invoke(tier);
            }
        }

        void Accumulate(float amount, HeatSource source)
        {
            bySource.TryGetValue(source, out float existing);
            bySource[source] = existing + amount;
        }

        // --- Sources (11.1) ---------------------------------------------------

        public void AddCustomerServed(float multiplier = 1f) =>
            Add(weights.perCustomerServed * multiplier, HeatSource.CustomersServed);

        /// <summary>Grilled and smoked dishes pass a multiplier of 2. The menu is a risk
        /// decision, and this is the line that makes it one.</summary>
        public void AddDishCooked(float multiplier = 1f) =>
            Add(weights.perDishCooked * multiplier, HeatSource.Cooking);

        public void AddPoweredLights(int count) =>
            Add(weights.perPoweredLightAfterDusk * count, HeatSource.Lighting);

        public void AddGenerator(int count = 1) =>
            Add(weights.perGeneratorRunning * count, HeatSource.Generators);

        public void AddJukebox(int count = 1) =>
            Add(weights.perJukeboxPlaying * count, HeatSource.Music);

        /// <summary>Expansion permanently raises Heat via footprint. Growth is never free.</summary>
        public void AddFootprint(int tiles) =>
            Add(weights.perStructureTile * tiles, HeatSource.Footprint);

        /// <summary>Success itself is dangerous: the better the restaurant's reputation, the
        /// worse the night. Applied once at dawn from the current tier.</summary>
        public void AddReputationTier(int tier) =>
            Add(weights.ReputationTierHeat(tier), HeatSource.Reputation);

        public void AddUnburiedCorpses(int count) =>
            Add(weights.perUnburiedCorpse * count, HeatSource.Corpses);

        public void AddCorrugatedSegments(int count) =>
            Add(weights.perCorrugatedSegment * count, HeatSource.Walls);

        /// <summary>Screechers raise Heat continuously while alive, which is how passive play
        /// gets punished during the siege itself.</summary>
        public void AddFromEnemy(float amount) => Add(amount, HeatSource.Enemies);

        // --- Reductions (11.2) - each one costs revenue ------------------------

        public void ApplyBlackout() => Add(weights.blackoutReduction, HeatSource.Reductions);

        public void ApplySmokeScrubber()
        {
            // Capped at the cooking Heat actually accrued: a scrubber cannot take the day
            // negative on a day the player never cooked.
            float cooking = HeatFrom(HeatSource.Cooking);
            float reduction = Mathf.Max(weights.smokeScrubberCookingReduction, -cooking);
            Add(reduction, HeatSource.Reductions);
        }

        public void ApplyCloseEarly() => Add(weights.closeEarlyReduction, HeatSource.Reductions);

        public void ApplyBuriedCorpses(int count) =>
            Add(weights.buryCorpseReduction * count, HeatSource.Reductions);

        public void ApplyDecoyFire() => Add(weights.decoyFireReduction, HeatSource.Reductions);

        public void ApplyRefusedCustomer() =>
            Add(weights.refuseCustomerReduction, HeatSource.Reductions);

        // --- Lifecycle --------------------------------------------------------

        /// <summary>Called by DuskState. After this, tonight's wave is decided and the bar
        /// stops moving. 28 question 2 keeps retroactive reduction out for now, because a
        /// late panic button risks becoming the only strategy.</summary>
        public float LockForNight()
        {
            IsLocked = true;
            LockedHeat = CurrentHeat;
            return LockedHeat;
        }

        /// <summary>Called by DawnState. Heat is per-day and does not carry over, which is
        /// also the recovery mechanism: a wrecked restaurant serves four customers instead of
        /// sixteen, so Heat is low, so the next night is survivable. The same system that
        /// punishes success protects failure, and no separate catch-up mechanic is needed.</summary>
        public void ResetForNewDay()
        {
            CurrentHeat = 0f;
            LockedHeat = 0f;
            IsLocked = false;
            bySource.Clear();
            lastForecastTier = 0;

            OnHeatChanged?.Invoke(CurrentHeat);
        }
    }
}
