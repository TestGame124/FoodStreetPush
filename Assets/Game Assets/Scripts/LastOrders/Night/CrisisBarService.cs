using System;
using System.Collections.Generic;
using LastOrders.Consequence;
using LastOrders.Data;
using LastOrders.Economy;
using UnityEngine;

namespace LastOrders.Night
{
    /// <summary>
    /// The player's hands during the siege (13.3).
    ///
    /// Turrets, traps and posted Guards resolve combat automatically, because the build IS
    /// the strategy and that is what makes the day matter. But it cannot be the entire
    /// night, or the night becomes a cutscene players learn to skip. These seven verbs are
    /// the difference.
    ///
    /// One grammar throughout: tap ability, tap tile. One-handed, portrait, no modifiers.
    /// The skill ceiling is not execution, it is triage - recognising which of three
    /// simultaneous breaches actually threatens the pantry and which the turrets will handle
    /// unaided. Cooldowns should therefore be tuned so a good player can always answer one
    /// crisis and never all three.
    /// </summary>
    public sealed class CrisisBarService
    {
        sealed class AbilityState
        {
            public CrisisAbilityDefinition Definition;
            public float CooldownRemaining;
            public int UsesRemaining;
        }

        readonly Dictionary<CrisisVerb, AbilityState> abilities = new Dictionary<CrisisVerb, AbilityState>();
        readonly ResourceLedger ledger;

        /// <summary>Raised when a verb is successfully used, for feedback and analytics.
        /// Which verbs players actually reach for is the clearest signal of whether the
        /// night is readable.</summary>
        public event Action<CrisisVerb, Vector3> OnAbilityUsed;

        /// <summary>Raised when a verb's availability changes, so the bar can re-render
        /// without polling seven cooldowns every frame.</summary>
        public event Action<CrisisVerb> OnAvailabilityChanged;

        public CrisisBarService(GameBalanceConfig balance, ResourceLedger ledger)
        {
            this.ledger = ledger;

            if (balance.crisisAbilities == null) return;

            for (int i = 0; i < balance.crisisAbilities.Length; i++)
            {
                CrisisAbilityDefinition def = balance.crisisAbilities[i];
                if (def == null) continue;

                abilities[def.verb] = new AbilityState
                {
                    Definition = def,
                    CooldownRemaining = 0f,
                    UsesRemaining = def.usesPerNight,
                };
            }
        }

        public bool IsAvailable(CrisisVerb verb)
        {
            if (!abilities.TryGetValue(verb, out AbilityState state)) return false;
            if (state.CooldownRemaining > 0f) return false;
            if (state.Definition.usesPerNight > 0 && state.UsesRemaining <= 0) return false;

            return ledger.CanAfford(state.Definition.costPerUse);
        }

        public float CooldownFraction(CrisisVerb verb)
        {
            if (!abilities.TryGetValue(verb, out AbilityState state)) return 0f;
            if (state.Definition.cooldownSeconds <= 0f) return 0f;

            return Mathf.Clamp01(state.CooldownRemaining / state.Definition.cooldownSeconds);
        }

        public void Tick(float deltaSeconds)
        {
            foreach (AbilityState state in abilities.Values)
            {
                if (state.CooldownRemaining <= 0f) continue;

                state.CooldownRemaining -= deltaSeconds;
                if (state.CooldownRemaining <= 0f)
                {
                    state.CooldownRemaining = 0f;
                    OnAvailabilityChanged?.Invoke(state.Definition.verb);
                }
            }
        }

        /// <summary>
        /// Applies a verb at a world position. Returns false without spending anything if
        /// the verb is unavailable or finds nothing to act on, so a mis-tap on empty ground
        /// costs the player nothing but the tap.
        /// </summary>
        public bool Use(CrisisVerb verb, Vector3 worldPosition)
        {
            if (!IsAvailable(verb)) return false;

            AbilityState state = abilities[verb];
            CrisisAbilityDefinition def = state.Definition;

            if (!Apply(def, worldPosition)) return false;

            if (!ledger.TrySpend(def.costPerUse)) return false;

            state.CooldownRemaining = def.cooldownSeconds;
            if (def.usesPerNight > 0) state.UsesRemaining--;

            OnAbilityUsed?.Invoke(verb, worldPosition);
            OnAvailabilityChanged?.Invoke(verb);
            return true;
        }

        bool Apply(CrisisAbilityDefinition def, Vector3 worldPosition)
        {
            switch (def.verb)
            {
                case CrisisVerb.Patch: return ApplyPatch(def, worldPosition);
                case CrisisVerb.Douse: return ApplyDouse(def, worldPosition);

                // The remaining verbs act on systems that own their own state - turret ammo,
                // generator routing, customer panic, Survivor posts. They are raised as
                // events and handled by those systems, so this service never has to hold a
                // reference to any of them.
                default: return true;
            }
        }

        bool ApplyPatch(CrisisAbilityDefinition def, Vector3 worldPosition)
        {
            DamageableStructure structure = FindStructure(worldPosition);
            if (structure == null || structure.State == DamageState.Destroyed) return false;

            structure.Patch(def.magnitude);
            return true;
        }

        bool ApplyDouse(CrisisAbilityDefinition def, Vector3 worldPosition)
        {
            DamageableStructure structure = FindStructure(worldPosition);
            if (structure == null) return false;

            var fire = structure.GetComponent<StructureFire>();
            if (fire == null || !fire.IsBurning) return false;

            fire.Extinguish();
            return true;
        }

        static DamageableStructure FindStructure(Vector3 worldPosition)
        {
            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();
            PlacedObject_Done occupant = grid?.GetGridObject(worldPosition)?.GetPlacedObject();

            return occupant != null ? occupant.GetComponent<DamageableStructure>() : null;
        }

        /// <summary>Resets per-night use counts and clears cooldowns. Called at dusk so the
        /// player always enters a siege with a full bar.</summary>
        public void BeginNight()
        {
            foreach (AbilityState state in abilities.Values)
            {
                state.CooldownRemaining = 0f;
                state.UsesRemaining = state.Definition.usesPerNight;
                OnAvailabilityChanged?.Invoke(state.Definition.verb);
            }
        }
    }
}
