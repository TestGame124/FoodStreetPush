using System;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Night;
using LastOrders.Repair;
using UnityEngine;

namespace LastOrders.Consequence
{
    /// <summary>
    /// The three damage states of 15.4, on one placed object.
    ///
    /// The rule that carries the most emotional weight in the whole consequence model is the
    /// one distinguishing perimeter from interior. Perimeter objects can be Destroyed and
    /// leave a ghost outline. Interior objects can only ever be Wrecked - never removed - so
    /// a wrecked restaurant still looks like the player's restaurant at dawn. That matters
    /// more than it sounds: nobody minds paying to repair, everybody minds re-placing twelve
    /// tables.
    ///
    /// The Hearth is exempt from everything. It can be Damaged but never disabled, which
    /// guarantees the player can always cook tomorrow and closes the softlock hole with one
    /// flag (15.1).
    ///
    /// Anything below full health is on the repair list while it is in the world, and comes back
    /// whole once a worker has been over and fixed it.
    /// </summary>
    [RequireComponent(typeof(PlacedObject_Done))]
    public sealed class DamageableStructure : MonoBehaviour, IRepairable
    {
        PlacedObject_Done placed;
        PlacedObjectTypeSO type;
        GameComposition composition;
        Transform repairAnchor;
        string repairId;

        public DamageState State { get; private set; } = DamageState.Intact;

        public float Health { get; private set; }

        public float MaxHealth => type != null ? Mathf.Max(1f, type.maxHealth) : 1f;

        public float HealthFraction => Mathf.Clamp01(Health / MaxHealth);

        /// <summary>Raised whenever the state changes, with the new state. The visual swap and
        /// the dawn damage report both subscribe.</summary>
        public event Action<DamageState> OnStateChanged;

        /// <summary>Raised on any damage, with remaining health. Drives hit feedback, which
        /// 21 requires be exaggerated because subtlety is invisible at this zoom.</summary>
        public event Action<float> OnDamaged;

        /// <summary>The lowest health reached tonight. 15.7 calls near-miss tuning the most
        /// important instruction in the document - a wall at 4 HP when dawn breaks is more
        /// memorable than a wall that fell - so the margin has to be measurable, not
        /// just felt.</summary>
        public float LowestHealthTonight { get; private set; }

        void Awake()
        {
            placed = GetComponent<PlacedObject_Done>();
            type = placed != null ? placed.PlacedObjectTypeSO : null;
            ResetToIntact();
        }

        void Start()
        {
            GameLifetimeScope scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;
            composition?.Repairs.Register(this);
        }

        // Pooled with the placed object: back on the list when it is placed again.
        void OnEnable() => composition?.Repairs.Register(this);

        void OnDisable() => composition?.Repairs.Unregister(this);

        // --- Being repaired ----------------------------------------------------------------

        public string RepairId => repairId ??= $"placed:{name}:{GetInstanceID()}";

        public string DisplayName =>
            type != null && !string.IsNullOrEmpty(type.nameString) ? type.nameString : name;

        public bool IsDestroyed => State == DamageState.Destroyed;

        public bool IsRepairableNow => isActiveAndEnabled && placed != null && !placed.isRemoved;

        public Transform RepairTarget => transform;

        /// <summary>Just over the top of it, made the first time it is asked for.</summary>
        public Transform RepairAnchor
        {
            get
            {
                if (repairAnchor != null) return repairAnchor;

                Bounds bounds = VisualBounds();
                repairAnchor = new GameObject("RepairAnchor").transform;
                repairAnchor.SetParent(transform, false);
                repairAnchor.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                return repairAnchor;
            }
        }

        /// <summary>Beside it, on whichever side faces the middle of the restaurant.</summary>
        public Vector3 RepairPoint
        {
            get
            {
                Bounds bounds = VisualBounds();
                Vector3 centre = new Vector3(bounds.center.x, transform.position.y, bounds.center.z);

                NightShelter shelter = FindFirstObjectByType<NightShelter>();
                Vector3 room = shelter != null ? shelter.Footprint.center : centre + Vector3.back;
                room.y = centre.y;

                Vector3 toward = room - centre;
                if (toward.sqrMagnitude < 1e-4f) toward = Vector3.back;

                // Out along whichever of its own faces points most toward the room.
                Vector3 face = Mathf.Abs(toward.x) > Mathf.Abs(toward.z)
                    ? new Vector3(Mathf.Sign(toward.x), 0f, 0f)
                    : new Vector3(0f, 0f, Mathf.Sign(toward.z));

                float reach = Mathf.Abs(face.x) > 0f ? bounds.extents.x : bounds.extents.z;
                return centre + face * (reach + 0.7f);
            }
        }

        Bounds VisualBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(transform.position + Vector3.up, Vector3.one);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        public void ResetToIntact()
        {
            type = placed != null ? placed.PlacedObjectTypeSO : type;
            Health = MaxHealth;
            LowestHealthTonight = Health;
            SetState(DamageState.Intact);
        }

        /// <summary>Output multiplier for whatever this object does. A Damaged station cooks
        /// slower; a Wrecked one does nothing at all.</summary>
        public float EfficiencyMultiplier
        {
            get
            {
                switch (State)
                {
                    case DamageState.Damaged:
                        return type != null ? type.damagedEfficiency : 0.75f;
                    case DamageState.Wrecked:
                    case DamageState.Destroyed:
                        return 0f;
                    default:
                        return 1f;
                }
            }
        }

        public bool IsFunctional => State == DamageState.Intact || State == DamageState.Damaged;

        /// <summary>
        /// Applies damage and steps the state down. Returns true if the object reached its
        /// terminal state this call, which is what the wave resolution counts as a loss.
        /// </summary>
        public bool ApplyDamage(float amount)
        {
            if (amount <= 0f) return false;
            if (State == DamageState.Destroyed) return false;

            // The Hearth absorbs damage down to Damaged and stops. The fire never goes out.
            bool isHearth = type != null && type.isHearth;

            Health = Mathf.Max(0f, Health - amount);
            LowestHealthTonight = Mathf.Min(LowestHealthTonight, Health);
            OnDamaged?.Invoke(Health);

            if (isHearth)
            {
                if (Health <= 0f) Health = 1f;
                if (State != DamageState.Damaged && HealthFraction < 1f)
                    SetState(DamageState.Damaged);
                return false;
            }

            if (Health <= 0f)
            {
                bool canBeDestroyed = type != null && type.isPerimeter;
                SetState(canBeDestroyed ? DamageState.Destroyed : DamageState.Wrecked);
                return true;
            }

            if (HealthFraction < 0.5f && State == DamageState.Intact)
                SetState(DamageState.Damaged);

            return false;
        }

        /// <summary>Restores a fraction of max health without changing state past Damaged.
        /// What the Crisis Bar's Patch verb does mid-fight (13.3).</summary>
        public void Patch(float fractionOfMax)
        {
            if (State == DamageState.Destroyed) return;

            Health = Mathf.Min(MaxHealth, Health + MaxHealth * fractionOfMax);

            if (State == DamageState.Wrecked && Health > 0f)
                SetState(DamageState.Damaged);
            else if (State == DamageState.Damaged && HealthFraction >= 1f)
                SetState(DamageState.Intact);
        }

        /// <summary>Full repair, paid for at dawn.</summary>
        public void Repair()
        {
            Health = MaxHealth;
            SetState(DamageState.Intact);
        }

        /// <summary>Fraction of the original build cost to restore this object, per 15.2
        /// and 15.4. Destroyed and Wrecked both cost 40 percent; Damaged costs 15.</summary>
        public float RestoreCostFraction(GameBalanceConfig balance)
        {
            switch (State)
            {
                case DamageState.Damaged: return balance.repairCostFraction;
                case DamageState.Wrecked:
                case DamageState.Destroyed: return balance.rebuildCostFraction;
                default: return 0f;
            }
        }

        /// <summary>Called at dusk so the night's near-miss margin is measured per night
        /// rather than accumulating across the campaign.</summary>
        public void BeginNight() => LowestHealthTonight = Health;

        void SetState(DamageState next)
        {
            if (State == next) return;
            State = next;
            OnStateChanged?.Invoke(next);
        }
    }
}
