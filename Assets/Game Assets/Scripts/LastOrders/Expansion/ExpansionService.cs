using System;
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Economy;
using LastOrders.Progression;
using UnityEngine;

namespace LastOrders.Expansion
{
    /// <summary>Where a piece of land stands in the expansion loop, in the order it moves.</summary>
    public enum AreaState
    {
        /// <summary>Not touching the restaurant's land yet. Its debris cannot be worked.</summary>
        Locked,

        /// <summary>Touching the boundary. Its debris can be cleared.</summary>
        Available,

        /// <summary>Every piece of debris is gone. The player can pay to expand into it.</summary>
        ReadyToExpand,

        /// <summary>Paid for; the new boundary is going up.</summary>
        Expanding,

        /// <summary>The boundary is built. The land is the restaurant's, permanently.</summary>
        Claimed,
    }

    /// <summary>One line of what an expansion needs, with what the player has towards it.</summary>
    public readonly struct ExpansionRequirement
    {
        public readonly string Label;
        public readonly int Have;
        public readonly int Need;

        public ExpansionRequirement(string label, int have, int need)
        {
            Label = label;
            Have = have;
            Need = need;
        }

        public bool Met => Have >= Need;
    }

    /// <summary>
    /// Everything the expand popup shows, gathered in one place so the popup has nothing to
    /// work out for itself: what the land is, what it needs, how long it takes, and whether
    /// the button should work - and if not, the one sentence saying why.
    /// </summary>
    public sealed class ExpansionOffer
    {
        public string Title;
        public string Benefit;
        public float Seconds;
        public readonly List<ExpansionRequirement> Requirements = new List<ExpansionRequirement>();
        public bool CanExpand;
        public string Reason;
    }

    /// <summary>
    /// Which land is locked, workable, ready, going up or claimed, and the rules that move it
    /// along.
    ///
    /// Knows nothing about jobs, workers or scene objects - only that an area has a set of
    /// debris IDs, a price and a build time. The clearing service reports clearances; this
    /// decides what they mean. The build timer is ticked from the game loop like the day
    /// cycle, so it stops when the game does and never runs a clock of its own.
    ///
    /// Progress is recorded as progression flags - debris cleared, expansion paid for, land
    /// claimed - so a reload puts every area back where it was and never charges twice.
    /// </summary>
    public sealed class ExpansionService
    {
        sealed class Area
        {
            public ExpansionAreaDefinition Definition;
            public readonly HashSet<string> Remaining = new HashSet<string>();
            public int DebrisCount;
            public AreaState State;
            public float BuildRemaining;
        }

        readonly ProgressionService progression;
        readonly RewardService rewards;
        readonly ResourceLedger ledger;
        readonly DayCycleMachine machine;
        readonly Func<int> playerLevel;
        readonly Dictionary<string, Area> areas = new Dictionary<string, Area>();
        readonly List<Area> finished = new List<Area>();

        public event Action<string, AreaState> OnAreaStateChanged;
        public event Action<string> OnAreaClaimed;

        public ExpansionService(ProgressionService progression, RewardService rewards, ResourceLedger ledger,
                                DayCycleMachine machine, Func<int> playerLevel)
        {
            this.progression = progression;
            this.rewards = rewards;
            this.ledger = ledger;
            this.machine = machine;
            this.playerLevel = playerLevel;
        }

        /// <summary>
        /// Adds an area and the debris standing on it.
        ///
        /// Debris already recorded as cleared is left out of what remains, so an area comes back
        /// in the state it was left in - ready, still building, or claimed - rather than asking
        /// to be cleared or paid for again.
        /// </summary>
        public void RegisterArea(ExpansionAreaDefinition definition, IReadOnlyList<string> debrisIds)
        {
            if (definition == null || string.IsNullOrEmpty(definition.id))
            {
                Debug.LogError("[Expansion] An area was registered with no definition or no id.");
                return;
            }

            if (areas.ContainsKey(definition.id))
            {
                Debug.LogError($"[Expansion] Two areas share the id '{definition.id}'. The second " +
                               "was ignored; give each area definition its own id.");
                return;
            }

            var area = new Area { Definition = definition };

            if (debrisIds != null)
            {
                for (int i = 0; i < debrisIds.Count; i++)
                {
                    if (string.IsNullOrEmpty(debrisIds[i])) continue;

                    area.DebrisCount++;
                    if (!progression.Has(ExpansionIds.DebrisCleared(debrisIds[i])))
                        area.Remaining.Add(debrisIds[i]);
                }
            }

            if (area.DebrisCount == 0)
            {
                Debug.LogWarning($"[Expansion] Area '{definition.id}' has no debris, so it can never be " +
                                 "expanded into. Put Debris objects under its ExpansionArea.");
            }

            areas.Add(definition.id, area);

            area.State = Evaluate(area);

            // The time left on a build is not recorded, only that it was paid for: a build
            // interrupted by a reload starts its timer again rather than charging again.
            if (area.State == AreaState.Expanding) area.BuildRemaining = BuildSeconds(area);

            OnAreaStateChanged?.Invoke(definition.id, area.State);

            // A new claimed area may be what another area was waiting on.
            if (area.State == AreaState.Claimed) ReevaluateAll();
        }

        public bool IsRegistered(string areaId) => !string.IsNullOrEmpty(areaId) && areas.ContainsKey(areaId);

        /// <summary>Every registered area, whatever its state.</summary>
        public IEnumerable<string> AreaIds => areas.Keys;

        public AreaState StateOf(string areaId) =>
            TryGet(areaId, out Area area) ? area.State : AreaState.Locked;

        public ExpansionAreaDefinition DefinitionOf(string areaId) =>
            TryGet(areaId, out Area area) ? area.Definition : null;

        /// <summary>Debris still standing in the area.</summary>
        public int RemainingIn(string areaId) =>
            TryGet(areaId, out Area area) ? area.Remaining.Count : 0;

        /// <summary>How far the build has got, 0 to 1. Zero unless the area is expanding.</summary>
        public float ExpansionProgress(string areaId)
        {
            if (!TryGet(areaId, out Area area)) return 0f;
            if (area.State == AreaState.Claimed) return 1f;
            if (area.State != AreaState.Expanding) return 0f;

            float total = BuildSeconds(area);
            return 1f - Mathf.Clamp01(area.BuildRemaining / total);
        }

        public float ExpansionSecondsLeft(string areaId) =>
            TryGet(areaId, out Area area) && area.State == AreaState.Expanding ? Mathf.Max(0f, area.BuildRemaining) : 0f;

        // --- Clearing -------------------------------------------------------------------------

        /// <summary>
        /// A piece of debris in the area was cleared. The area becomes ready to expand only when
        /// this is the one that empties it - never merely because it happens to be empty, which
        /// is what an area whose debris had not registered yet would look like.
        /// </summary>
        public void HandleDebrisCleared(string areaId, string debrisId)
        {
            if (!TryGet(areaId, out Area area)) return;
            if (!area.Remaining.Remove(debrisId)) return;

            if (area.Remaining.Count == 0 && area.State == AreaState.Available)
                SetState(area, AreaState.ReadyToExpand);
        }

        // --- Expanding --------------------------------------------------------------------------

        /// <summary>What expanding into the area needs, what the player has, and whether they can.</summary>
        public ExpansionOffer GetOffer(string areaId)
        {
            var offer = new ExpansionOffer();
            if (!TryGet(areaId, out Area area)) return offer;

            ExpansionAreaDefinition definition = area.Definition;
            offer.Title = definition.displayName;
            offer.Benefit = definition.benefit;
            offer.Seconds = BuildSeconds(area);

            if (definition.requiredLevel > 0)
                offer.Requirements.Add(new ExpansionRequirement("Level", Level(), definition.requiredLevel));

            if (definition.expandCost != null)
            {
                foreach (ResourceAmount cost in definition.expandCost)
                {
                    if (cost.amount <= 0) continue;
                    offer.Requirements.Add(new ExpansionRequirement(cost.type.ToString(), ledger.Get(cost.type), cost.amount));
                }
            }

            offer.CanExpand = CanExpand(areaId, out offer.Reason);
            return offer;
        }

        /// <summary>Whether the expansion could start now, and if not, why - in words the player can act on.</summary>
        public bool CanExpand(string areaId, out string reason)
        {
            if (!TryGet(areaId, out Area area))
            {
                reason = "There is no land here to expand into.";
                return false;
            }

            ExpansionAreaDefinition definition = area.Definition;

            switch (area.State)
            {
                case AreaState.Locked:
                    reason = WhyLocked(areaId);
                    return false;

                case AreaState.Available:
                    reason = $"Clear the debris from the {definition.displayName} first.";
                    return false;

                case AreaState.Expanding:
                    reason = $"The {definition.displayName} is already being built.";
                    return false;

                case AreaState.Claimed:
                    reason = $"The {definition.displayName} is already part of the restaurant.";
                    return false;
            }

            if (machine != null && !machine.CurrentPhase.AllowsBuilding())
            {
                reason = "Too dangerous to build outside after dark. Wait for dawn.";
                return false;
            }

            if (definition.requiredLevel > 0 && Level() < definition.requiredLevel)
            {
                reason = $"Reach level {definition.requiredLevel} to expand.";
                return false;
            }

            if (definition.expandCost != null)
            {
                foreach (ResourceAmount cost in definition.expandCost)
                {
                    int shortBy = cost.amount - ledger.Get(cost.type);
                    if (shortBy <= 0) continue;

                    reason = $"Need {shortBy} more {cost.type}.";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// Pays for the expansion and starts building. Returns false, with the reason in
        /// <paramref name="message"/>, when it cannot start.
        /// </summary>
        public bool StartExpansion(string areaId, out string message)
        {
            if (!CanExpand(areaId, out message)) return false;

            Area area = areas[areaId];
            ExpansionAreaDefinition definition = area.Definition;

            if (definition.expandCost != null && definition.expandCost.Length > 0 && !ledger.TrySpend(definition.expandCost))
            {
                message = "Could not pay for the expansion.";
                return false;
            }

            progression.Set(ExpansionIds.ExpansionStarted(areaId));
            area.BuildRemaining = BuildSeconds(area);
            SetState(area, AreaState.Expanding);

            message = $"Expanding into the {definition.displayName}.";
            return true;
        }

        /// <summary>Advances every build under way. Called once per frame by the game loop.</summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            finished.Clear();

            foreach (Area area in areas.Values)
            {
                if (area.State != AreaState.Expanding) continue;

                area.BuildRemaining -= deltaSeconds;
                if (area.BuildRemaining <= 0f) finished.Add(area);
            }

            // Claimed outside the loop: claiming re-evaluates every area, which changes states
            // in the dictionary being walked.
            for (int i = 0; i < finished.Count; i++) Claim(finished[i]);
        }

        /// <summary>
        /// A readable reason the area cannot be worked yet, naming the land that has to be
        /// claimed first. Null when it can be worked.
        /// </summary>
        public string WhyLocked(string areaId)
        {
            if (!TryGet(areaId, out Area area) || area.State != AreaState.Locked) return null;

            ExpansionAreaDefinition[] gates = area.Definition.unlockedBy;
            string first = gates != null && gates.Length > 0 && gates[0] != null
                ? gates[0].displayName
                : "the land next to it";

            return $"{area.Definition.displayName} is out of reach. Claim {first} first.";
        }

        void Claim(Area area)
        {
            string id = area.Definition.id;

            area.BuildRemaining = 0f;
            SetState(area, AreaState.Claimed);
            progression.Set(ExpansionIds.AreaClaimed(id));
            rewards.Grant(ExpansionIds.AreaReward(id), area.Definition.claimReward);

            OnAreaClaimed?.Invoke(id);

            ReevaluateAll();
        }

        void ReevaluateAll()
        {
            foreach (Area area in areas.Values)
            {
                if (area.State == AreaState.Claimed || area.State == AreaState.Expanding) continue;
                SetState(area, Evaluate(area));
            }
        }

        AreaState Evaluate(Area area)
        {
            string id = area.Definition.id;

            if (progression.Has(ExpansionIds.AreaClaimed(id))) return AreaState.Claimed;
            if (progression.Has(ExpansionIds.ExpansionStarted(id))) return AreaState.Expanding;

            if (!IsReachable(area.Definition)) return AreaState.Locked;

            return area.DebrisCount > 0 && area.Remaining.Count == 0 ? AreaState.ReadyToExpand : AreaState.Available;
        }

        bool IsReachable(ExpansionAreaDefinition definition)
        {
            if (definition.BordersStartingLand) return true;

            for (int i = 0; i < definition.unlockedBy.Length; i++)
            {
                ExpansionAreaDefinition gate = definition.unlockedBy[i];
                if (gate != null && progression.Has(ExpansionIds.AreaClaimed(gate.id))) return true;
            }

            return false;
        }

        void SetState(Area area, AreaState next)
        {
            if (area.State == next) return;

            area.State = next;
            OnAreaStateChanged?.Invoke(area.Definition.id, next);
        }

        int Level() => playerLevel != null ? playerLevel() : 1;

        static float BuildSeconds(Area area) => Mathf.Max(0.1f, area.Definition.expandSeconds);

        bool TryGet(string areaId, out Area area)
        {
            area = null;
            return !string.IsNullOrEmpty(areaId) && areas.TryGetValue(areaId, out area);
        }
    }
}
