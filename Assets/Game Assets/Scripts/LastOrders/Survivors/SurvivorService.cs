using System;
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.Economy;
using UnityEngine;

namespace LastOrders.Survivors
{
    /// <summary>
    /// One person. Named, portrait, one Role, one Trait, persistent and injurable.
    ///
    /// Deliberately a plain class rather than a MonoBehaviour: a Survivor away on Dispatch
    /// has no body in the scene but very much still exists, and their absence from the
    /// kitchen and the night watch is the entire cost of sending them.
    /// </summary>
    [Serializable]
    public sealed class Survivor
    {
        public string Id;
        public string Name;
        public SurvivorRoleDefinition Role;
        public SurvivorTraitDefinition Trait;

        /// <summary>Days remaining before they can work again. Injuries, never deaths (15.5).</summary>
        public int InjuryDaysRemaining;

        /// <summary>Days remaining on a Dispatch. Away is not injured - they are fine, they
        /// are simply not here.</summary>
        public int AwayDaysRemaining;

        public bool IsInjured => InjuryDaysRemaining > 0;

        public bool IsAway => AwayDaysRemaining > 0;

        /// <summary>The only question most systems ask.</summary>
        public bool IsAvailable => !IsInjured && !IsAway;

        /// <summary>Whether this person will work tonight at all. Nervous Survivors flee when
        /// the siege starts; Superstitious ones refuse Named Nights - a trait that costs
        /// nothing on forty-three days and a great deal on the seven that matter.</summary>
        public bool WillWorkTonight(bool isNamedNight)
        {
            if (!IsAvailable) return false;
            if (Trait == null) return true;
            if (Trait.fleesDuringSiege) return false;
            if (isNamedNight && Trait.refusesNamedNights) return false;
            return true;
        }

        public float Effectiveness => Trait != null ? Trait.roleEffectivenessMultiplier : 1f;

        public int DailyWage
        {
            get
            {
                if (Role == null) return 0;
                float multiplier = Trait != null ? Trait.wageMultiplier : 1f;
                return Mathf.RoundToInt(Role.dailyWage * multiplier);
            }
        }
    }

    /// <summary>
    /// The roster (17).
    ///
    /// Survivors are what make the game affordable to play. With no staff the player does
    /// everything themselves and the clock destroys them; a full roster runs the restaurant
    /// semi-autonomously and frees them to build. Staffing is therefore the progression
    /// system that unlocks the rest of the game, and recruitment comes from good service -
    /// which is what ties the day loop directly to the night loop.
    /// </summary>
    public sealed class SurvivorService
    {
        readonly List<Survivor> roster = new List<Survivor>();
        readonly GameBalanceConfig balance;
        readonly ResourceLedger ledger;

        public SurvivorService(GameBalanceConfig balance, ResourceLedger ledger)
        {
            this.balance = balance;
            this.ledger = ledger;
        }

        public IReadOnlyList<Survivor> Roster => roster;

        /// <summary>Raised when someone joins. Worth celebrating - it is the main progression
        /// beat outside of expansion.</summary>
        public event Action<Survivor> OnRecruited;

        public event Action<Survivor> OnInjured;

        /// <summary>Raised when wages could not be paid in full, with the shortfall. The
        /// economy is supposed to squeeze here, not silently absorb it.</summary>
        public event Action<int> OnWagesUnpaid;

        public void Add(Survivor survivor)
        {
            if (survivor == null) return;

            roster.Add(survivor);
            OnRecruited?.Invoke(survivor);
        }

        public IEnumerable<Survivor> AvailableWithRole(SurvivorRole role)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                Survivor survivor = roster[i];
                if (survivor.IsAvailable && survivor.Role != null && survivor.Role.role == role)
                    yield return survivor;
            }
        }

        /// <summary>Summed multiplier for a role across everyone available, which is what the
        /// kitchen and the build layer actually consume.</summary>
        public float TotalEffectiveness(SurvivorRole role)
        {
            float total = 0f;
            foreach (Survivor survivor in AvailableWithRole(role)) total += survivor.Effectiveness;
            return total;
        }

        /// <summary>
        /// Injures someone for 1 to 3 days, halved by a Medic on the roster.
        ///
        /// It hurts - the kitchen is slower, a defense post is empty - and it hurts in time,
        /// which is the only currency 15 allows punishment to be denominated in.
        /// </summary>
        public void Injure(Survivor survivor, int days = -1)
        {
            if (survivor == null || survivor.IsInjured) return;

            if (days < 0)
                days = UnityEngine.Random.Range(balance.injuryDaysRange.x, balance.injuryDaysRange.y + 1);

            float multiplier = survivor.Trait != null ? survivor.Trait.injuryDurationMultiplier : 1f;

            // A Medic halves recovery for everyone, including themselves - which makes the
            // first Medic a disproportionately good recruit, as intended.
            if (HasAvailableMedic()) multiplier *= MedicRecoveryMultiplier();

            survivor.InjuryDaysRemaining = Mathf.Max(1, Mathf.RoundToInt(days * multiplier));
            OnInjured?.Invoke(survivor);
        }

        bool HasAvailableMedic()
        {
            foreach (Survivor _ in AvailableWithRole(SurvivorRole.Medic)) return true;
            return false;
        }

        float MedicRecoveryMultiplier()
        {
            foreach (Survivor medic in AvailableWithRole(SurvivorRole.Medic))
                return medic.Role != null ? medic.Role.injuryRecoveryMultiplier : 0.5f;

            return 1f;
        }

        /// <summary>
        /// Pays the roster. Called at Resolution.
        ///
        /// 19 is emphatic that wages are the pressure valve: a large roster forces the
        /// restaurant to stay open and busy, which raises Heat, which raises threat. The
        /// economy has to push players toward danger rather than permit a safe idle
        /// equilibrium, and this method is where that pressure is actually applied. Preserve
        /// it through every rebalance.
        /// </summary>
        public void PayDailyWages()
        {
            int total = 0;
            for (int i = 0; i < roster.Count; i++) total += roster[i].DailyWage;
            if (total <= 0) return;

            int available = ledger.Get(ResourceType.Coin);

            if (available >= total)
            {
                ledger.Add(ResourceType.Coin, -total);
                return;
            }

            // Paid as far as it goes. Unpaid wages are a Reputation problem rather than a
            // hard failure, because nothing in this game is allowed to take a person away.
            ledger.Add(ResourceType.Coin, -available);
            OnWagesUnpaid?.Invoke(total - available);
        }

        /// <summary>Advances injury and away counters. Called once at Resolution.</summary>
        public void AdvanceDay()
        {
            for (int i = 0; i < roster.Count; i++)
            {
                Survivor survivor = roster[i];
                if (survivor.InjuryDaysRemaining > 0) survivor.InjuryDaysRemaining--;
                if (survivor.AwayDaysRemaining > 0) survivor.AwayDaysRemaining--;
            }
        }

        /// <summary>Passive Reputation from traits, applied at Resolution. The Regular's
        /// contribution simply by being on the roster.</summary>
        public float PassiveReputationPerDay()
        {
            float total = 0f;
            for (int i = 0; i < roster.Count; i++)
            {
                Survivor survivor = roster[i];
                if (survivor.Trait != null && survivor.IsAvailable)
                    total += survivor.Trait.passiveReputationPerDay;
            }
            return total;
        }

        public Survivor FindById(string id)
        {
            for (int i = 0; i < roster.Count; i++)
                if (roster[i].Id == id) return roster[i];

            return null;
        }

        public void RestoreFromSave(IEnumerable<Survivor> saved)
        {
            roster.Clear();
            if (saved == null) return;

            foreach (Survivor survivor in saved) roster.Add(survivor);
        }
    }
}
