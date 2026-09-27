using System;
using System.Collections.Generic;

namespace LastOrders.Progression
{
    /// <summary>
    /// Unlock flags and one-time reward grants (21).
    ///
    /// The second service the audit found missing. Gating fields are already authored across
    /// the definition assets - required reputation, required plot tier, unlocked-from-start -
    /// and nothing enforced any of them, because there was nowhere to record that a thing had
    /// happened.
    ///
    /// The important property is idempotence. 13 is explicit that if the app closes after a
    /// payment or a reward grant, the recorded transaction prevents a duplicate grant, and 22
    /// makes it an acceptance test: closing during payment, harvest, placement or reward cannot
    /// duplicate or lose value. So a grant is keyed by ID and asking twice is free.
    /// </summary>
    public sealed class ProgressionService
    {
        readonly HashSet<string> flags = new HashSet<string>();
        readonly HashSet<string> granted = new HashSet<string>();

        public event Action<string> OnFlagSet;
        public event Action<string> OnRewardGranted;

        public IReadOnlyCollection<string> Flags => flags;
        public IReadOnlyCollection<string> GrantedRewards => granted;

        public bool Has(string flag) => !string.IsNullOrEmpty(flag) && flags.Contains(flag);

        /// <summary>Sets a flag. Returns true only the first time, so callers can react once.</summary>
        public bool Set(string flag)
        {
            if (string.IsNullOrEmpty(flag) || !flags.Add(flag)) return false;

            OnFlagSet?.Invoke(flag);
            return true;
        }

        /// <summary>
        /// Runs a grant exactly once for a given reward ID.
        ///
        /// The ID is the whole mechanism: 12 grants guaranteed quest rewards "once using mission
        /// reward IDs", and the same rule covers one-off payouts and night victories. Callers
        /// do not have to remember whether they already paid.
        /// </summary>
        public bool GrantOnce(string rewardId, Action grant)
        {
            if (string.IsNullOrEmpty(rewardId) || !granted.Add(rewardId)) return false;

            grant?.Invoke();
            OnRewardGranted?.Invoke(rewardId);
            return true;
        }

        public bool HasGranted(string rewardId) => granted.Contains(rewardId);
    }

    /// <summary>
    /// The flag and reward names the opening sequence uses.
    ///
    /// Constants rather than loose strings: a typo in a flag name is an unlock that never
    /// fires and never says why, which is among the harder things to notice.
    /// </summary>
    public static class ProgressionFlags
    {
        /// <summary>13 step 1: the clearing job completed and the diner entrance is open.</summary>
        public const string EntranceCleared = "entrance_cleared";

        /// <summary>13 step 2: the stove is Active and the soup recipe is unlocked.</summary>
        public const string StoveRepaired = "stove_repaired";

        /// <summary>The first bowl of soup is on the pass. Half of what the sign waits on -
        /// an open sign over an empty kitchen is worse than a closed one.</summary>
        public const string FirstMealCooked = "first_meal_cooked";

        /// <summary>The Chef has turned the sign round and switched on the porch light.
        /// Until this is set, travellers walk past without a reason to stop.</summary>
        public const string RestaurantOpen = "restaurant_open";

        /// <summary>13 step 4: two payments taken; planting unlocks.</summary>
        public const string FirstServiceComplete = "first_service_complete";

        public const string PlantingUnlocked = "planting_unlocked";
        public const string SecondTableRepaired = "second_table_repaired";
        public const string NightOneSurvived = "night_one_survived";

        // --- What the specialised visitors wait on -------------------------------------
        //
        // Each of these gates one archetype. The opening of the game is three variants of one
        // traveller with one behaviour, and every specialist arrives only once the system that
        // gives them a reason to exist does - a Mechanic before there is a workshop is a
        // costume, not a character, and the player learns nothing from meeting them.

        /// <summary>Scavenging routes are running. Brings the Scavenger, back from the ruins.</summary>
        public const string ScavengingUnlocked = "scavenging_unlocked";

        /// <summary>The world map is open. Brings the Courier, stopping between deliveries.</summary>
        public const string WorldMapUnlocked = "world_map_unlocked";

        /// <summary>The workshop is running. Brings the Mechanic, travelling to a repair job.</summary>
        public const string WorkshopUnlocked = "workshop_unlocked";

        /// <summary>The radio works. Word reaches further settlements, and their residents
        /// start making the trip for an evening out rather than for supplies.</summary>
        public const string RadioRepaired = "radio_repaired";
    }

    public static class ProgressionRewards
    {
        /// <summary>13 step 1 and the 14 ledger both put the entrance debris at 8 wood.</summary>
        public const string ClearEntranceWood = "reward_clear_entrance_wood";

        public const string FirstServiceCoins = "reward_first_service_coins";
        public const string NightOneVictory = "reward_night_one_victory";
    }

    /// <summary>
    /// The flag and reward IDs of the expansion loop, built from the stable IDs authored on
    /// each area and piece of debris.
    ///
    /// Flags are the record of what has been cleared and claimed - the one thing a save has to
    /// keep for reclaimed land to stay reclaimed. Built in one place so the service that sets a
    /// flag and the one that reads it cannot spell it differently.
    /// </summary>
    public static class ExpansionIds
    {
        public static string DebrisCleared(string debrisId) => $"debris_cleared:{debrisId}";
        public static string AreaClaimed(string areaId) => $"area_claimed:{areaId}";

        /// <summary>The expansion was paid for and its build started. Recorded so a reload
        /// resumes the build rather than asking for the price again.</summary>
        public static string ExpansionStarted(string areaId) => $"area_expansion_started:{areaId}";
        public static string DebrisReward(string debrisId) => $"reward_debris:{debrisId}";
        public static string AreaReward(string areaId) => $"reward_area:{areaId}";
    }
}
