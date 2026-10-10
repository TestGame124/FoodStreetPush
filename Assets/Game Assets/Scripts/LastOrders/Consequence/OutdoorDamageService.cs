using System;
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Defense;
using LastOrders.Economy;
using UnityEngine;

namespace LastOrders.Consequence
{
    /// <summary>
    /// What a zombie inside the walls asks before it goes after something in the yard. Kept narrow
    /// so the agent never needs the whole service, and a test can stand in for it.
    /// </summary>
    public interface IOutdoorRaids
    {
        /// <summary>Finds something for <paramref name="raider"/>, standing at <paramref name="from"/>,
        /// to smash. False when it should head for the restaurant instead.</summary>
        bool TryAssign(object raider, Vector3 from, out OutdoorWreck target);

        /// <summary>True while it is still worth smashing - not yet a wreck, and the night's cap on
        /// wrecks not yet reached.</summary>
        bool StillWanted(object raider, OutdoorWreck target);

        /// <summary>Lands a blow. True when the raider is done with it, either way.</summary>
        bool Strike(object raider, OutdoorWreck target, float damage, Vector3 from);

        void Release(object raider);
    }

    /// <summary>
    /// The yard during an attack, and the morning after it.
    ///
    /// During an attack it hands each zombie that gets inside the walls something nearby to smash,
    /// and it holds the line on how much one attack may wreck: never more than the cap, and never
    /// more things under attack at once than the cap has room for, so the last two raiders cannot
    /// each finish off a different object in the same second. Once the cap is reached nothing more
    /// is handed out and every raider lets go - the rest of the night is the restaurant's.
    ///
    /// Afterwards it owns the repair: one action, one repair kit, and every wreck in the yard is
    /// back. A player who has no kit can buy one on the spot, so a defeat - which pays no kit -
    /// never leaves the yard broken for good.
    /// </summary>
    public sealed class OutdoorDamageService : IOutdoorRaids
    {
        readonly EncounterTuning tuning;
        readonly ResourceLedger ledger;
        readonly IWallet coins;
        readonly DayCycleMachine machine;
        readonly Func<Vector3, bool> isInside;
        readonly DefenseService defense;

        readonly List<OutdoorWreck> objects = new List<OutdoorWreck>();
        readonly Dictionary<object, OutdoorWreck> assignments = new Dictionary<object, OutdoorWreck>();
        readonly HashSet<OutdoorWreck> underAttack = new HashSet<OutdoorWreck>();

        public OutdoorDamageService(EncounterTuning tuning, ResourceLedger ledger, IWallet coins, DayCycleMachine machine,
                                    Func<Vector3, bool> isInside, DefenseService defense)
        {
            this.tuning = tuning ?? new EncounterTuning();
            this.ledger = ledger;
            this.coins = coins;
            this.machine = machine;
            this.isInside = isInside;
            this.defense = defense;
        }

        /// <summary>Finds the yard's objects and gives each a wreck, as an attack is locked in. The
        /// scene's to supply; without one only what has been registered by hand is in play.</summary>
        public Func<IEnumerable<OutdoorWreck>> Scanner { get; set; }

        /// <summary>True from the moment an attack is locked in until it is settled.</summary>
        public bool IsActive { get; private set; }

        public int Cap { get; private set; }

        public int WrecksThisEncounter { get; private set; }

        /// <summary>More wrecks tonight's attack may still cause.</summary>
        public int RemainingCap => Mathf.Max(0, Cap - WrecksThisEncounter);

        public IReadOnlyList<OutdoorWreck> All => objects;

        public int RepairKits => ledger != null ? ledger.Get(ResourceType.RepairKit) : 0;

        public int KitPrice => Mathf.Max(0, tuning.repairKitCoinPrice);

        /// <summary>Something in the yard was just wrecked.</summary>
        public event Action<OutdoorWreck> OnWrecked;

        /// <summary>The night's cap on wrecks has been reached: the rest go for the restaurant.</summary>
        public event Action OnCapReached;

        /// <summary>A repair put every wreck back, with how many there were.</summary>
        public event Action<int> OnRepaired;

        // --- What is out there -----------------------------------------------------------------

        public void Register(OutdoorWreck wreck)
        {
            if (wreck == null || objects.Contains(wreck)) return;

            objects.Add(wreck);

            // A wreck brought back into the world - a turret re-registered under a new id - still
            // does not fire.
            if (wreck.IsWrecked) SetDefenseWrecked(wreck, true);
        }

        public void Unregister(OutdoorWreck wreck)
        {
            objects.Remove(wreck);
            underAttack.Remove(wreck);
        }

        void Prune()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] == null) objects.RemoveAt(i);
        }

        /// <summary>Wrecks standing in the world right now.</summary>
        public int WreckedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < objects.Count; i++)
                    if (objects[i] != null && objects[i].IsWrecked && objects[i].gameObject.activeInHierarchy) count++;

                return count;
            }
        }

        // --- The attack ------------------------------------------------------------------------

        /// <summary>The attack is locked in: everything in the yard is found and made whole, and the
        /// night's cap is set.</summary>
        public void BeginEncounter(int cap)
        {
            Prune();

            if (Scanner != null)
            {
                foreach (OutdoorWreck found in Scanner())
                {
                    if (found == null) continue;

                    Register(found);
                    found.Heal();
                }
            }

            IsActive = true;
            Cap = Mathf.Max(0, cap);
            WrecksThisEncounter = 0;
            assignments.Clear();
            underAttack.Clear();
        }

        public void EndEncounter()
        {
            IsActive = false;
            assignments.Clear();
            underAttack.Clear();
        }

        /// <summary>Morning: anything hit but not wrecked is mended overnight. Wrecks wait for a kit.</summary>
        public void MendDents()
        {
            for (int i = 0; i < objects.Count; i++)
                if (objects[i] != null) objects[i].Heal();
        }

        public bool TryAssign(object raider, Vector3 from, out OutdoorWreck target)
        {
            target = null;
            if (raider == null || !IsActive || RemainingCap <= 0) return false;
            if (isInside != null && !isInside(from)) return false;

            Release(raider);
            RefreshUnderAttack();

            // With as many objects already under attack as the cap has room for, only those are on
            // offer: the night can never wreck more than it is allowed.
            bool onlyThoseUnderAttack = underAttack.Count >= RemainingCap;

            float reach = Mathf.Max(1f, tuning.raidSearchRadius);
            float best = reach;

            for (int i = 0; i < objects.Count; i++)
            {
                OutdoorWreck candidate = objects[i];
                if (!InTheWorld(candidate) || candidate.IsWrecked) continue;
                if (onlyThoseUnderAttack && !underAttack.Contains(candidate)) continue;
                if (AttackersOf(candidate) >= Mathf.Max(1, tuning.maxRaidersPerObject)) continue;

                float distance = candidate.FlatDistance(from);
                if (distance >= best) continue;

                best = distance;
                target = candidate;
            }

            if (target == null) return false;

            assignments[raider] = target;
            underAttack.Add(target);
            return true;
        }

        public bool StillWanted(object raider, OutdoorWreck target) =>
            IsActive && RemainingCap > 0 && InTheWorld(target) && !target.IsWrecked &&
            raider != null && assignments.TryGetValue(raider, out OutdoorWreck held) && held == target;

        public bool Strike(object raider, OutdoorWreck target, float damage, Vector3 from)
        {
            if (!StillWanted(raider, target)) return true;

            if (!target.TakeHit(damage, from)) return false;

            HandleWrecked(target);
            return true;
        }

        public void Release(object raider)
        {
            if (raider == null || !assignments.TryGetValue(raider, out OutdoorWreck held)) return;

            assignments.Remove(raider);
            if (held != null && AttackersOf(held) == 0) underAttack.Remove(held);
        }

        void HandleWrecked(OutdoorWreck wreck)
        {
            WrecksThisEncounter++;
            SetDefenseWrecked(wreck, true);

            // Nobody goes on smashing a wreck.
            var finished = new List<object>();
            foreach (KeyValuePair<object, OutdoorWreck> entry in assignments)
                if (entry.Value == wreck) finished.Add(entry.Key);

            for (int i = 0; i < finished.Count; i++) assignments.Remove(finished[i]);
            underAttack.Remove(wreck);

            Debug.Log($"[Last Orders] Zombies wrecked the {wreck.DisplayName.ToLowerInvariant()} " +
                      $"({WrecksThisEncounter} of at most {Cap} tonight).");

            OnWrecked?.Invoke(wreck);

            if (RemainingCap > 0) return;

            // That was the last thing the night may take from the yard. Everyone still out there
            // turns on the restaurant.
            assignments.Clear();
            underAttack.Clear();
            OnCapReached?.Invoke();
        }

        /// <summary>Standing in the world, switched on: not destroyed, hidden or pooled away.</summary>
        static bool InTheWorld(OutdoorWreck wreck) => wreck != null && wreck.enabled && wreck.gameObject.activeInHierarchy;

        int AttackersOf(OutdoorWreck target)
        {
            int count = 0;
            foreach (OutdoorWreck held in assignments.Values)
                if (held == target) count++;

            return count;
        }

        void RefreshUnderAttack()
        {
            underAttack.Clear();
            foreach (OutdoorWreck held in assignments.Values)
                if (held != null && !held.IsWrecked) underAttack.Add(held);
        }

        void SetDefenseWrecked(OutdoorWreck wreck, bool wrecked)
        {
            if (defense == null || wreck == null) return;

            var post = wreck.GetComponent<DefensePost>();
            if (post != null && post.Unit != null) defense.SetWrecked(post.Unit.Id, wrecked);
        }

        // --- The repair -------------------------------------------------------------------------

        /// <summary>True in daylight and through the warning before an attack: never while one is on.</summary>
        public bool IsRepairTime(out string reason)
        {
            DayPhase phase = machine != null ? machine.CurrentPhase : DayPhase.Day;

            if ((IsActive && phase != DayPhase.Dusk) || phase == DayPhase.Night || phase == DayPhase.Resolution)
            {
                reason = "Not while the zombies are here. Repair the wrecks in the morning.";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>What the one repair would cost right now: a kit from the stores, or the coins to
        /// buy one, or nothing it can be done with.</summary>
        public string DescribeCost()
        {
            if (RepairKits > 0) return "1 Repair Kit";
            return KitPrice > 0 ? $"{KitPrice} {Till.CoinName(coins)}" : "a Repair Kit";
        }

        public bool CanRepairAll(out string reason)
        {
            int wrecked = WreckedCount;
            if (wrecked == 0)
            {
                reason = "Nothing in the yard is wrecked.";
                return false;
            }

            if (!IsRepairTime(out reason)) return false;

            if (RepairKits > 0) return true;

            if (KitPrice > 0 && coins != null && coins.Balance >= KitPrice) return true;

            reason = KitPrice > 0
                ? $"You need a Repair Kit, or {KitPrice} {Till.CoinName(coins)} to buy one. Hold off an attack to earn a kit."
                : "You need a Repair Kit. Hold off an attack to earn one.";
            return false;
        }

        /// <summary>
        /// One action, one kit, every wreck back together. With no kit in the stores and coins
        /// enough, the kit is bought and used in the same breath. The message is the tap's reply.
        /// </summary>
        public bool TryRepairAll(out string message)
        {
            if (!CanRepairAll(out message)) return false;

            string paidWith;
            if (RepairKits > 0)
            {
                if (!ledger.TrySpend(new[] { new ResourceAmount(ResourceType.RepairKit, 1) }))
                {
                    message = "The repair kit could not be taken from the stores.";
                    return false;
                }

                paidWith = "a Repair Kit";
            }
            else
            {
                if (coins == null || !coins.TrySpend(KitPrice))
                {
                    message = $"Short of the {KitPrice} {Till.CoinName(coins)} a repair kit costs.";
                    return false;
                }

                paidWith = $"a Repair Kit bought for {KitPrice} {Till.CoinName(coins)}";
            }

            int repaired = 0;
            for (int i = 0; i < objects.Count; i++)
            {
                OutdoorWreck wreck = objects[i];
                if (wreck == null || !wreck.IsWrecked) continue;

                wreck.Restore();
                SetDefenseWrecked(wreck, false);
                repaired++;
            }

            message = repaired == 1
                ? $"The crew put the wreck back together with {paidWith}."
                : $"The crew put all {repaired} wrecks back together with {paidWith}.";

            Debug.Log($"[Last Orders] {message}");
            OnRepaired?.Invoke(repaired);
            return true;
        }
    }
}
