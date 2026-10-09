using System;
using System.Collections.Generic;
using FoodStreet.Navigation;
using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Defense
{
    /// <summary>
    /// One slot in the strip outside the walls, as the rules see it: where it is, what has been
    /// built on it, and how worn that is.
    ///
    /// A plain class with no scene in it, like <see cref="DefenseUnit"/>. The slot in the scene
    /// registers one and draws what it says - the spikes coming up, the wire shaking, the
    /// barricade falling - so every ground defense can be run and tested without a model.
    ///
    /// The three kinds share one loop. A barricade only answers the steering - a step into it is
    /// refused, and the zombie that took it starts hitting it. A trap and a snare look each tick
    /// for what is standing on them: a trap springs on everything there at once and resets, a
    /// snare holds back whatever is caught and is torn a little by it.
    /// </summary>
    public sealed class GroundDefenseUnit
    {
        /// <summary>Seconds between a snare's cuts. Once a second, so a zombie in wire flinches
        /// as it is cut rather than every frame.</summary>
        const float BiteSeconds = 1f;

        /// <summary>How much of a zombie's body has to be over a trap or in wire for it to count:
        /// its feet, more or less, not the edge of its shoulder.</summary>
        const float CatchRadius = 0.1f;

        public readonly string SlotId;

        float strikeCooldown;
        float biteCooldown;

        public GroundDefenseUnit(string slotId, Vector3 position, float yaw)
        {
            SlotId = slotId;
            Position = position;
            Yaw = yaw;
        }

        /// <summary>The middle of the slot, on the ground. Kept up to date by the slot.</summary>
        public Vector3 Position { get; set; }

        /// <summary>Degrees about Y: the slot's +Z faces out from the wall.</summary>
        public float Yaw { get; set; }

        /// <summary>What stands here. Null on an empty slot.</summary>
        public GroundDefenseDefinition Definition { get; private set; }

        public float Health { get; private set; }

        public bool IsBuilt => Definition != null;

        /// <summary>Battered down, jammed or torn, and waiting for a worker.</summary>
        public bool IsWorn => IsBuilt && Health <= 0f;

        /// <summary>Built and still doing its job.</summary>
        public bool IsWorking => IsBuilt && Health > 0f;

        public float MaxHealth => IsBuilt ? Definition.health : 0f;

        public float HealthFraction => MaxHealth > 0f ? Mathf.Clamp01(Health / MaxHealth) : 0f;

        public GroundDefenseKind Kind => IsBuilt ? Definition.kind : GroundDefenseKind.Barricade;

        public bool IsBarricade => IsBuilt && Definition.kind == GroundDefenseKind.Barricade;

        /// <summary>How many of the night's zombies were on it at the last tick: what makes wire shake.</summary>
        public int Caught { get; private set; }

        public int Strikes { get; private set; }

        /// <summary>A trap sprang.</summary>
        public event Action OnStrike;

        /// <summary>A barricade took a blow.</summary>
        public event Action OnHit;

        /// <summary>It was battered down, jammed or torn.</summary>
        public event Action OnWorn;

        /// <summary>Built, or put right again. What the slot redraws on.</summary>
        public event Action OnChanged;

        Vector2 HalfSize => IsBuilt ? Definition.footprint * 0.5f : Vector2.zero;

        internal void Build(GroundDefenseDefinition definition)
        {
            if (definition == null || IsBuilt) return;

            Definition = definition;
            Health = definition.health;
            strikeCooldown = 0f;
            biteCooldown = 0f;
            OnChanged?.Invoke();
        }

        /// <summary>Back to full, standing and armed again.</summary>
        public void Repair()
        {
            if (!IsBuilt) return;

            Health = MaxHealth;
            strikeCooldown = 0f;
            biteCooldown = 0f;
            OnChanged?.Invoke();
        }

        /// <summary>True when a body of <paramref name="radius"/> at <paramref name="point"/> is on it.</summary>
        public bool Covers(Vector3 point, float radius = CatchRadius) =>
            IsBuilt && MovementBarriers.Penetration(point, Position, Yaw, HalfSize, radius) > 0f;

        // --- The loop -------------------------------------------------------------------------------

        /// <summary>
        /// One step for a trap or a snare: what is on it now, and what it does to them. A barricade
        /// has nothing to do here - the steering asks it.
        /// </summary>
        internal void Tick(float deltaSeconds, IReadOnlyList<IDefenseTarget> hostiles)
        {
            Caught = 0;

            if (!IsWorking || Definition.kind == GroundDefenseKind.Barricade)
                return;

            if (strikeCooldown > 0f) strikeCooldown -= deltaSeconds;
            if (biteCooldown > 0f) biteCooldown -= deltaSeconds;

            if (hostiles == null) return;

            bool trap = Definition.kind == GroundDefenseKind.Trap;
            bool strike = trap && strikeCooldown <= 0f;
            bool bite = !trap && biteCooldown <= 0f && Definition.damagePerSecond > 0f;

            for (int i = 0; i < hostiles.Count; i++)
            {
                IDefenseTarget target = hostiles[i];
                if (target == null || (target is UnityEngine.Object o && o == null) || !target.IsAlive) continue;
                if (!Covers(target.Position)) continue;

                Caught++;

                if (trap)
                {
                    if (!strike) continue;
                    if (target is ISlowable pinned) pinned.Slow(Definition.slowMultiplier, Definition.slowSeconds);
                    target.ApplyDamage(Definition.strikeDamage);
                    continue;
                }

                if (target is ISlowable held) held.Slow(Definition.slowMultiplier, Definition.slowSeconds);
                if (bite) target.ApplyDamage(Definition.damagePerSecond * BiteSeconds);
            }

            if (Caught == 0) return;

            if (trap)
            {
                if (!strike) return;

                strikeCooldown = Definition.strikeSeconds;
                Strikes++;
                OnStrike?.Invoke();
                Wear(Definition.wearPerStrike);
                return;
            }

            if (bite) biteCooldown = BiteSeconds;
            Wear(Definition.wearPerSecond * Caught * deltaSeconds);
        }

        // --- A barricade --------------------------------------------------------------------------

        /// <summary>The steering's question: does this step take a zombie into it? Only a standing
        /// barricade stops anything, and only the night's: workers step over it.</summary>
        public bool Blocks(Vector3 from, Vector3 to, float radius, MoverKind mover) =>
            mover == MoverKind.Hostile && IsWorking && Definition.kind == GroundDefenseKind.Barricade &&
            MovementBarriers.StepEnters(from, to, Position, Yaw, HalfSize, radius);

        /// <summary>One blow on a barricade. True when it brought it down.</summary>
        public bool TakeHit(float damage)
        {
            if (damage <= 0f || !IsBarricade || !IsWorking) return false;

            OnHit?.Invoke();
            return Wear(damage);
        }

        /// <summary>The nearest point of its footprint to <paramref name="from"/>, for a zombie to face.</summary>
        public Vector3 ClosestPoint(Vector3 from)
        {
            Quaternion turn = Quaternion.Euler(0f, Yaw, 0f);
            Vector3 local = Quaternion.Inverse(turn) * (from - Position);

            Vector2 half = HalfSize;
            local.x = Mathf.Clamp(local.x, -half.x, half.x);
            local.z = Mathf.Clamp(local.z, -half.y, half.y);
            local.y = 0f;

            return Position + turn * local;
        }

        /// <summary>Takes <paramref name="amount"/> off it. True when that wore it out.</summary>
        bool Wear(float amount)
        {
            if (amount <= 0f || Health <= 0f) return false;

            Health = Mathf.Max(0f, Health - amount);
            if (Health > 0f) return false;

            OnWorn?.Invoke();
            return true;
        }
    }
}
