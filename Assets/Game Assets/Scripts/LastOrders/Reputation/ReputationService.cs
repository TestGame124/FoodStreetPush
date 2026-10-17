using System;
using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Reputation
{
    /// <summary>How an attack ended, as the Safety Reputation counts it.</summary>
    public enum EncounterResultKind
    {
        /// <summary>Held, and no zombie ever got inside the walls.</summary>
        CleanVictory = 0,

        /// <summary>Held, but zombies got inside the walls. Nothing in the yard was wrecked.</summary>
        BreachedVictory = 1,

        /// <summary>Held, but something in the yard was wrecked.</summary>
        WreckedVictory = 2,

        /// <summary>The restaurant's health reached zero.</summary>
        Defeat = 3,
    }

    /// <summary>
    /// The Safety Reputation: one 0-100 meter for how safe survivors believe the restaurant is.
    ///
    /// It moves once per zombie attack, by how that attack went, and nothing else nudges it from
    /// day to day. That is what makes it readable: the player can always say why it changed. In
    /// return it decides two things - how often customer cars turn up, and which threat band the
    /// next attack is drawn from. Safety draws trade, and trade draws the dead. The player's level
    /// adds to both: a restaurant that has grown is better known on the road.
    ///
    /// It can fall all the way to zero. That is not a dead end: at zero, cars still come now and
    /// then with one or two customers, and the next attacks are the lightest there are, so a
    /// restaurant that lost everything can always earn its way back.
    /// </summary>
    public sealed class ReputationService
    {
        public const float Max = 100f;

        readonly GameBalanceConfig balance;

        float current;

        /// <summary>The highest tier ever reached. Kept for the celebration when a new band is
        /// entered; it is not a floor - the meter can drop back below it.</summary>
        public int HighestTierReached { get; private set; }

        public float Current => current;

        /// <summary>0 to 1 across the meter.</summary>
        public float Normalized => Mathf.Clamp01(current / Max);

        public int Tier => balance.ReputationTier(current);

        SafetyReputationTuning Tuning => balance.safety ?? (balance.safety = new SafetyReputationTuning());

        /// <summary>Raised on any change, with the new value. Nothing polls this.</summary>
        public event Action<float> OnReputationChanged;

        /// <summary>Raised when the tier steps up past anything reached before, with the new tier.</summary>
        public event Action<int> OnTierReached;

        public ReputationService(GameBalanceConfig balance)
        {
            this.balance = balance != null
                ? balance
                : throw new ArgumentNullException(nameof(balance));

            current = Mathf.Clamp(Tuning.startingReputation, 0f, Max);
            HighestTierReached = Tier;
        }

        /// <summary>
        /// The single mutation point. Clamps to 0-100 and raises the tier event when a band is
        /// entered for the first time.
        /// </summary>
        public void Add(float delta)
        {
            if (Mathf.Approximately(delta, 0f)) return;

            float before = current;
            current = Mathf.Clamp(current + delta, 0f, Max);
            if (Mathf.Approximately(before, current)) return;

            // The tier event goes first so that a subscriber handling the value change already
            // sees the unlock it implies, rather than reacting to a value whose tier has not been
            // announced yet.
            int tier = Tier;
            if (tier > HighestTierReached)
            {
                HighestTierReached = tier;
                OnTierReached?.Invoke(tier);
            }

            OnReputationChanged?.Invoke(current);
        }

        /// <summary>Puts the meter at an exact value. For a new game and for tests.</summary>
        public void Set(float value) => Add(Mathf.Clamp(value, 0f, Max) - current);

        // --- One result per attack ----------------------------------------------------------------

        /// <summary>
        /// What an attack that ended this way is worth, before it is applied. A victory earns its
        /// row of the table, less a share for how far the restaurant's own health ran down - the
        /// closer the call, the less the survivors on the road hear about it. A defeat is one fixed
        /// penalty, whatever else broke.
        /// </summary>
        public float ResultDelta(EncounterResultKind kind, float restaurantDamageFraction)
        {
            SafetyReputationTuning tuning = Tuning;

            if (kind == EncounterResultKind.Defeat) return -Mathf.Abs(tuning.defeatPenalty);

            float gain;
            switch (kind)
            {
                case EncounterResultKind.CleanVictory: gain = tuning.cleanVictory; break;
                case EncounterResultKind.BreachedVictory: gain = tuning.breachedVictory; break;
                default: gain = tuning.wreckedVictory; break;
            }

            if (gain <= 0f) return gain;

            float kept = 1f - Mathf.Clamp01(tuning.restaurantDamageWeight) * Mathf.Clamp01(restaurantDamageFraction);
            return Mathf.Round(gain * kept * 10f) / 10f;
        }

        /// <summary>Applies an attack's result and returns how far the meter actually moved, which
        /// can be less than the delta at either end of it.</summary>
        public float ApplyEncounterResult(EncounterResultKind kind, float restaurantDamageFraction) =>
            ApplyEncounterResult(kind, restaurantDamageFraction, 1f);

        /// <summary>As above, with the delta scaled by <paramref name="share"/>: a small nightly attack
        /// moves the meter by less than a big wave does.</summary>
        public float ApplyEncounterResult(EncounterResultKind kind, float restaurantDamageFraction, float share)
        {
            float before = current;
            Add(Mathf.Round(ResultDelta(kind, restaurantDamageFraction) * Mathf.Max(0f, share) * 10f) / 10f);
            return current - before;
        }

        /// <summary>Which row of the table an attack lands in.</summary>
        public static EncounterResultKind Classify(bool defeated, bool breached, int wrecks)
        {
            if (defeated) return EncounterResultKind.Defeat;
            if (wrecks > 0) return EncounterResultKind.WreckedVictory;
            return breached ? EncounterResultKind.BreachedVictory : EncounterResultKind.CleanVictory;
        }

        // --- Customers ----------------------------------------------------------------------------

        /// <summary>
        /// Multiplier on the time between customer cars: slow at zero, the traffic profile's own
        /// pace at fifty, nearly twice as often at a hundred. Applied within the car park and
        /// the chairs, which still gate every arrival.
        /// </summary>
        public float ArrivalIntervalScale
        {
            get
            {
                SafetyReputationTuning tuning = Tuning;
                float t = Normalized;

                return t <= 0.5f
                    ? Mathf.Lerp(tuning.arrivalIntervalAtZero, tuning.arrivalIntervalAtHalf, t * 2f)
                    : Mathf.Lerp(tuning.arrivalIntervalAtHalf, tuning.arrivalIntervalAtFull, (t - 0.5f) * 2f);
            }
        }

        /// <summary>The smallest and largest party one car brings at today's reputation.</summary>
        public Vector2Int PartySizeRange
        {
            get
            {
                SafetyReputationTuning tuning = Tuning;
                float t = Normalized;

                int low = Mathf.RoundToInt(Mathf.Lerp(tuning.partySizeAtZero.x, tuning.partySizeAtFull.x, t));
                int high = Mathf.RoundToInt(Mathf.Lerp(tuning.partySizeAtZero.y, tuning.partySizeAtFull.y, t));

                low = Mathf.Max(1, low);
                return new Vector2Int(low, Mathf.Max(low, high));
            }
        }

        /// <summary>How many customers the next car brings, never more than
        /// <paramref name="room"/> - the free chairs - and always at least one.</summary>
        public int RollPartySize(int room, float roll01) => RollPartySize(room, roll01, 1);

        /// <summary>As above, with the biggest party a little bigger at a higher <paramref name="level"/>.</summary>
        public int RollPartySize(int room, float roll01, int level)
        {
            Vector2Int range = PartySizeRangeAt(level);
            int size = range.x + Mathf.FloorToInt(Mathf.Clamp01(roll01) * (range.y - range.x + 1) * 0.9999f);
            return Mathf.Clamp(size, 1, Mathf.Max(1, room));
        }

        // --- Customers and the player's level ------------------------------------------------------

        /// <summary>
        /// Multiplier on the time between cars from the level alone: one at level 1, a little less
        /// for every level above it, never under the tuning's floor.
        /// </summary>
        public float LevelArrivalScale(int level)
        {
            SafetyReputationTuning tuning = Tuning;
            float scale = 1f - Mathf.Max(0f, tuning.arrivalFasterPerLevel) * Mathf.Max(0, level - 1);
            return Mathf.Max(Mathf.Clamp(tuning.minLevelArrivalScale, 0.1f, 1f), scale);
        }

        /// <summary>The time between cars from Safety and the level together. Lower is more often.</summary>
        public float ArrivalIntervalScaleAt(int level) => ArrivalIntervalScale * LevelArrivalScale(level);

        /// <summary>Extra customers the biggest party may bring at <paramref name="level"/>.</summary>
        public int ExtraGuestsAt(int level)
        {
            SafetyReputationTuning tuning = Tuning;
            return Mathf.Min(Mathf.Max(0, tuning.maxExtraGuestsFromLevel), Mathf.Max(0, level - 1) / Mathf.Max(1, tuning.levelsPerExtraGuest));
        }

        /// <summary>The smallest and largest party at today's Safety and <paramref name="level"/>.</summary>
        public Vector2Int PartySizeRangeAt(int level)
        {
            Vector2Int range = PartySizeRange;
            return new Vector2Int(range.x, range.y + ExtraGuestsAt(level));
        }
    }
}
