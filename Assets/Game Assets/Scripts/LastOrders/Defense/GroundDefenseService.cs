using System;
using System.Collections.Generic;
using LastOrders.DayCycle;
using LastOrders.Data;
using LastOrders.Economy;
using LastOrders.Progression;
using UnityEngine;

namespace LastOrders.Defense
{
    /// <summary>
    /// The strip outside the walls: every build slot in it, what stands on each, and the one loop
    /// that lets the traps and the wire act on whatever reaches them.
    ///
    /// A slot holds one ground defense, of whichever kind the player picks. Building one is paying
    /// for it, in daylight, with no worker - like a turret - and the choice is recorded in
    /// progression under the slot's id, so it is still there when the slot registers again. A
    /// slot the scene starts with something on is never charged for it.
    /// </summary>
    public sealed class GroundDefenseService
    {
        readonly Dictionary<string, GroundDefenseUnit> units = new Dictionary<string, GroundDefenseUnit>();
        readonly Dictionary<string, GroundDefenseDefinition[]> choices = new Dictionary<string, GroundDefenseDefinition[]>();
        readonly List<GroundDefenseUnit> order = new List<GroundDefenseUnit>();

        readonly ResourceLedger ledger;
        readonly ProgressionService progression;
        readonly DayCycleMachine machine;
        readonly Func<IReadOnlyList<IDefenseTarget>> hostiles;

        public event Action<GroundDefenseUnit> OnBuilt;

        /// <param name="hostiles">What walks into the strip: the night's zombies. Read every tick.</param>
        public GroundDefenseService(ResourceLedger ledger, ProgressionService progression, DayCycleMachine machine,
                                    Func<IReadOnlyList<IDefenseTarget>> hostiles)
        {
            this.ledger = ledger;
            this.progression = progression;
            this.machine = machine;
            this.hostiles = hostiles;
        }

        public IReadOnlyList<GroundDefenseUnit> All => order;

        public int BuiltCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < order.Count; i++) if (order[i].IsBuilt) count++;
                return count;
            }
        }

        public GroundDefenseUnit Find(string slotId) =>
            !string.IsNullOrEmpty(slotId) && units.TryGetValue(slotId, out GroundDefenseUnit unit) ? unit : null;

        // --- Registration ---------------------------------------------------------------------------

        /// <summary>
        /// Adds a slot. It comes back built with whatever was bought for it before, or with
        /// <paramref name="startsWith"/> - free - when nothing was, or empty.
        /// </summary>
        public GroundDefenseUnit Register(string slotId, Vector3 position, float yaw, GroundDefenseDefinition[] options,
                                          GroundDefenseDefinition startsWith)
        {
            if (string.IsNullOrEmpty(slotId))
            {
                Debug.LogError("[GroundDefense] A strip slot was registered with no id.");
                return null;
            }

            if (units.ContainsKey(slotId))
            {
                Debug.LogError($"[GroundDefense] Two strip slots share the id '{slotId}'. The second was ignored; " +
                               "give each slot its own id.");
                return null;
            }

            var unit = new GroundDefenseUnit(slotId, position, yaw);
            GroundDefenseDefinition built = Recorded(slotId, options) ?? startsWith;
            if (built != null) unit.Build(built);

            units.Add(slotId, unit);
            choices[slotId] = options ?? Array.Empty<GroundDefenseDefinition>();
            order.Add(unit);
            return unit;
        }

        public void Unregister(string slotId)
        {
            GroundDefenseUnit unit = Find(slotId);
            if (unit == null) return;

            units.Remove(slotId);
            choices.Remove(slotId);
            order.Remove(unit);
        }

        // --- The loop -------------------------------------------------------------------------------

        /// <summary>Lets every trap and snare act on what is standing on it. Called once per frame.</summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            IReadOnlyList<IDefenseTarget> targets = hostiles?.Invoke();
            for (int i = 0; i < order.Count; i++) order[i].Tick(deltaSeconds, targets);
        }

        // --- Building -------------------------------------------------------------------------------

        public BuildChoiceOffer GetBuildOffer(string slotId)
        {
            var offer = new BuildChoiceOffer
            {
                Title = "Build in the strip",
                Description = "Slow them down out here, where the wall guns can reach them.",
                Status = string.Empty,
            };

            GroundDefenseUnit unit = Find(slotId);
            if (unit == null)
            {
                offer.Blocked = "There is nowhere to build here.";
                return offer;
            }

            GroundDefenseDefinition[] options = Options(slotId);
            foreach (GroundDefenseDefinition option in options)
            {
                if (option == null) continue;

                string shortBy = ShortfallIn(option.buildCost);
                offer.Details.Add(option.Summary());
                offer.Options.Add(new BuildChoice($"{option.displayName}\n{Label(option.buildCost)}", shortBy == null, shortBy));
            }

            offer.Details.Add("No worker needed - ready once it is paid for");
            offer.Blocked = WhyCannotBuild(unit, options);
            return offer;
        }

        /// <summary>Builds option <paramref name="index"/> of the slot's offer on it, paying from the stores.</summary>
        public bool TryBuild(string slotId, int index, out string message)
        {
            GroundDefenseUnit unit = Find(slotId);
            if (unit == null)
            {
                message = "There is nowhere to build here.";
                return false;
            }

            GroundDefenseDefinition[] options = Options(slotId);
            message = WhyCannotBuild(unit, options);
            if (message != null) return false;

            GroundDefenseDefinition chosen = BuildChoices.Pick(options, index);
            if (chosen == null)
            {
                message = "That cannot be built here.";
                return false;
            }

            if (!TryPay(chosen.buildCost, out message)) return false;

            progression.Set(GroundDefenseIds.Built(slotId, chosen.id));
            unit.Build(chosen);
            OnBuilt?.Invoke(unit);

            message = $"{chosen.displayName} built.";
            return true;
        }

        string WhyCannotBuild(GroundDefenseUnit unit, GroundDefenseDefinition[] options)
        {
            if (unit.IsBuilt) return $"There is already a {unit.Definition.displayName} here.";
            if (BuildChoices.Pick(options, 0) == null) return "Nothing can be built here yet.";
            if (machine != null && !machine.CurrentPhase.AllowsBuilding()) return "Too dangerous to build after dark. Wait for dawn.";

            return null;
        }

        GroundDefenseDefinition[] Options(string slotId) =>
            choices.TryGetValue(slotId, out GroundDefenseDefinition[] options) ? options : Array.Empty<GroundDefenseDefinition>();

        GroundDefenseDefinition Recorded(string slotId, GroundDefenseDefinition[] options)
        {
            if (options == null) return null;

            foreach (GroundDefenseDefinition option in options)
                if (option != null && progression.Has(GroundDefenseIds.Built(slotId, option.id))) return option;

            return null;
        }

        // --- Paying ---------------------------------------------------------------------------------

        bool TryPay(ResourceAmount[] cost, out string reason)
        {
            reason = null;
            if (IsFree(cost)) return true;
            if (ledger.TrySpend(cost)) return true;

            reason = ShortfallIn(cost) ?? "Could not pay for it.";
            return false;
        }

        /// <summary>The first thing the stores are short of, in words. Null when they have it all.</summary>
        string ShortfallIn(ResourceAmount[] cost)
        {
            if (cost == null) return null;

            for (int i = 0; i < cost.Length; i++)
            {
                int shortBy = cost[i].amount - ledger.Get(cost[i].type);
                if (shortBy > 0) return $"Need {shortBy} more {cost[i].type.DisplayName()}.";
            }

            return null;
        }

        static bool IsFree(ResourceAmount[] cost)
        {
            if (cost == null) return true;
            for (int i = 0; i < cost.Length; i++) if (cost[i].amount > 0) return false;
            return true;
        }

        static string Label(ResourceAmount[] cost)
        {
            if (IsFree(cost)) return "Free";

            var parts = new List<string>(cost.Length);
            for (int i = 0; i < cost.Length; i++)
                if (cost[i].amount > 0) parts.Add($"{cost[i].amount} {cost[i].type.DisplayName()}");

            return string.Join(" + ", parts);
        }

        // --- Describing -----------------------------------------------------------------------------

        /// <summary>The tap reply for a built slot: what stands there and how it is holding up.</summary>
        public string Describe(string slotId)
        {
            GroundDefenseUnit unit = Find(slotId);
            if (unit == null) return null;
            if (!unit.IsBuilt) return "An empty slot in the strip. Build a barricade, a trap or wire here.";

            GroundDefenseDefinition definition = unit.Definition;
            if (unit.IsWorn)
            {
                string state = definition.kind == GroundDefenseKind.Barricade ? "is down"
                             : definition.kind == GroundDefenseKind.Trap ? "has jammed" : "is torn";
                return $"The {definition.displayName} {state}. Send a worker to fix it.";
            }

            return $"{definition.Summary()}. {Mathf.CeilToInt(unit.Health)}/{Mathf.CeilToInt(unit.MaxHealth)}.";
        }
    }

    /// <summary>The progression flag a built slot is recorded under, in one place so the setter and the reader agree.</summary>
    public static class GroundDefenseIds
    {
        public static string Built(string slotId, string defenseId) => $"ground_built:{slotId}:{defenseId}";
    }
}
