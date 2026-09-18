using System.Collections.Generic;
using LastOrders.Build;
using LastOrders.Consequence;
using LastOrders.Core;
using LastOrders.Customers;
using LastOrders.Data;
using LastOrders.Dispatch;
using LastOrders.Economy;
using LastOrders.Heat;
using LastOrders.Night;
using LastOrders.Perimeter;
using LastOrders.Reputation;
using LastOrders.Survivors;

namespace LastOrders.DayCycle
{
    /// <summary>
    /// What one day produced. Accumulated during the cycle and read by the dawn damage
    /// report, which 23 requires be a single readable screen rather than a wall of numbers.
    /// </summary>
    public sealed class DayReport
    {
        public int Day;
        public int CustomersServed;
        public int CustomersTurnedAway;
        public int CustomersLost;
        public int CoinEarned;
        public float HeatAtDusk;
        public int EnemiesKilled;

        public int StructuresDamaged;
        public int StructuresWrecked;
        public int StructuresDestroyed;

        public readonly List<string> InjuredSurvivors = new List<string>();
        public ResourceAmount[] Stolen;

        public float ReputationDelta;

        /// <summary>
        /// The lowest health any perimeter structure reached, as a fraction.
        ///
        /// 15.7 calls near-miss tuning the most important instruction in the document: a wall
        /// at 4 HP when dawn breaks is more memorable than a wall that fell, and it costs the
        /// player nothing. Players tell their friends about those nights. Surfacing the
        /// margin here is what makes that tunable rather than hoped for.
        /// </summary>
        public float ClosestCall = 1f;

        /// <summary>True when the night landed in "under-prepared or worse". 15.8 targets
        /// 15 to 20 percent of nights here - below 10 means no stakes, above 35 means players
        /// feel punished for playing.</summary>
        public bool WasBadNight;

        public void Reset(int day)
        {
            Day = day;
            CustomersServed = 0;
            CustomersTurnedAway = 0;
            CustomersLost = 0;
            CoinEarned = 0;
            HeatAtDusk = 0f;
            EnemiesKilled = 0;
            StructuresDamaged = 0;
            StructuresWrecked = 0;
            StructuresDestroyed = 0;
            InjuredSurvivors.Clear();
            Stolen = null;
            ReputationDelta = 0f;
            ClosestCall = 1f;
            WasBadNight = false;
        }
    }

    /// <summary>
    /// The services every phase state needs, in one object.
    ///
    /// Passed as an aggregate rather than constructor-injecting ten dependencies into each of
    /// five states. The states are not independent units with narrow interests - each one
    /// legitimately touches most of the game for a moment - so listing the same ten
    /// parameters five times would document nothing and obscure the one or two dependencies
    /// that are actually interesting per state.
    /// </summary>
    public sealed class DayCycleContext
    {
        public readonly GameBalanceConfig Balance;
        public readonly TimeService Time;
        public readonly HeatService Heat;
        public readonly ReputationService Reputation;
        public readonly ResourceLedger Ledger;
        public readonly PerimeterService Perimeter;
        public readonly CustomerDirector Customers;
        public readonly WaveDirector Waves;
        public readonly ThreatProfileService ThreatProfile;
        public readonly CrisisBarService CrisisBar;
        public readonly SurvivorService Survivors;
        public readonly DispatchService Dispatch;
        public readonly GhostRegistry Ghosts;
        public readonly BuildCommandStack Commands;
        public readonly BuildContext BuildContext;
        public readonly PoolService Pools;

        public readonly DayReport Report = new DayReport();

        /// <summary>Tonight's wave, produced at dusk and consumed by Night.</summary>
        public WavePlan TonightsWave;

        /// <summary>Set at dawn when the player chooses Closed for Repairs (15.10): no
        /// serving, faster building, and a Reputation cost they accepted knowingly.</summary>
        public bool ClosedForRepairs;

        public DayCycleContext(
            GameBalanceConfig balance,
            TimeService time,
            HeatService heat,
            ReputationService reputation,
            ResourceLedger ledger,
            PerimeterService perimeter,
            CustomerDirector customers,
            WaveDirector waves,
            ThreatProfileService threatProfile,
            CrisisBarService crisisBar,
            SurvivorService survivors,
            DispatchService dispatch,
            GhostRegistry ghosts,
            BuildCommandStack commands,
            BuildContext buildContext,
            PoolService pools)
        {
            Balance = balance;
            Time = time;
            Heat = heat;
            Reputation = reputation;
            Ledger = ledger;
            Perimeter = perimeter;
            Customers = customers;
            Waves = waves;
            ThreatProfile = threatProfile;
            CrisisBar = crisisBar;
            Survivors = survivors;
            Dispatch = dispatch;
            Ghosts = ghosts;
            Commands = commands;
            BuildContext = buildContext;
            Pools = pools;
        }
    }
}
