using DG.Tweening;
using FoodStreet.Navigation;
using LastOrders.Core;
using LastOrders.Effects;
using LastOrders.Jobs;
using LastOrders.Repair;
using LastOrders.UI.World;
using UnityEngine;

namespace LastOrders.Perimeter
{
    /// <summary>
    /// What a section of the compound wall and a gate have in common: a straight run of wall
    /// with a health bar, that zombies walk into, hit, and eventually knock down.
    ///
    /// Everything here is about the piece standing in the world - its footprint, its health, the
    /// shake and the bar when it is hit, the dust when it falls. What stops who, and how it looks
    /// at each stage, is the subclass's.
    ///
    /// A piece can be tied to another object with <see cref="presentWhile"/> and then exists only
    /// while that object is active: the stretch of wall across the yard's opening is gone once
    /// the yard is claimed, and the yard's own wall is there only once it has been built.
    ///
    /// Whatever the night does to it stays done until a worker comes to fix it: a damaged piece
    /// is on the repair list for as long as it is present, and the worker works from the inside.
    /// </summary>
    [DefaultExecutionOrder(1110)]
    public abstract class CompoundStructure : MonoBehaviour, IMovementBarrier, IBreakable, ICompoundStructure,
                                              IRepairable, IWorkStrikeReceiver
    {
        [SerializeField] protected GameLifetimeScope scope;

        [SerializeField] string displayName = "Wall";

        [Tooltip("Length along local X, in metres. The footprint zombies are stopped by.")]
        [SerializeField] protected float length = 4f;

        [Tooltip("Thickness along local Z, in metres.")]
        [SerializeField] protected float thickness = 0.45f;

        [Tooltip("Optional. This piece only exists while that object is active - the wall across " +
                 "the yard's opening, the yard's own new wall.")]
        [SerializeField] GameObject presentWhile;

        [Header("Parts")]
        [Tooltip("Everything drawn. Hidden while the piece is not present, and shaken when it is hit.")]
        [SerializeField] protected GameObject visualRoot;

        [Tooltip("Where the health bar floats from. The bar sits a little over two metres above it.")]
        [SerializeField] Transform healthBarAnchor;

        [Tooltip("Played where it falls.")]
        [SerializeField] PooledEffect breakEffect;

        [Header("Feedback")]
        [Tooltip("Seconds the health bar stays up after the last blow.")]
        [SerializeField] float healthBarLinger = 3f;

        [Tooltip("Metres each blow shakes it by.")]
        [SerializeField] float shakeStrength = 0.05f;

        protected GameComposition composition;

        float health;
        bool present;
        bool started;
        float lastHit = float.NegativeInfinity;
        HealthBarView healthBar;

        public string DisplayName => displayName;

        public float Health => health;

        public abstract float MaxHealth { get; }

        public float HealthFraction => MaxHealth > 0f ? Mathf.Clamp01(health / MaxHealth) : 0f;

        public bool IsDestroyed => health <= 0f;

        /// <summary>In the world right now: active, and whatever it is tied to active too.</summary>
        public bool IsPresent => present;

        public virtual bool IsInTheWay => present && !IsDestroyed;

        public float Length => length;

        public float Thickness => thickness;

        protected float Yaw => transform.eulerAngles.y;

        protected virtual void Awake()
        {
            health = MaxHealth;
        }

        protected virtual void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                Debug.LogWarning($"[{GetType().Name}] {name} found no GameLifetimeScope. It still stops " +
                                 "zombies, but is never repaired and never counted.", this);
            }

            started = true;
            SetPresent(ShouldBePresent);
            ApplyVisuals();
        }

        protected virtual void OnDisable()
        {
            SetPresent(false);

            // Not while the scene is coming down: the pool would re-parent the bar under a root
            // that is being destroyed in the same pass.
            if (gameObject.scene.isLoaded) ReleaseHealthBar();
        }

        protected virtual void Update()
        {
            bool should = ShouldBePresent;
            if (started && should != present) SetPresent(should);

            DrawHealthBar();
        }

        bool ShouldBePresent => isActiveAndEnabled && (presentWhile == null || presentWhile.activeInHierarchy);

        void SetPresent(bool on)
        {
            present = on;

            if (visualRoot != null && visualRoot.activeSelf != on) visualRoot.SetActive(on);

            if (on)
            {
                MovementBarriers.Register(this);
                composition?.Compound.Register(this);
                composition?.Repairs.Register(this);
            }
            else
            {
                MovementBarriers.Unregister(this);
                composition?.Compound.Unregister(this);
                composition?.Repairs.Unregister(this);
            }
        }

        // --- Being hit ----------------------------------------------------------------------------

        public bool TakeHit(float damage, Vector3 from)
        {
            if (damage <= 0f || !IsInTheWay) return false;

            health = Mathf.Max(0f, health - damage);
            lastHit = Time.time;

            composition?.Compound.ReportHit(this);
            Shake();

            if (health > 0f)
            {
                ApplyVisuals();
                return false;
            }

            Fall();
            return true;
        }

        void Fall()
        {
            ReleaseHealthBar();
            OnFallen();
            ApplyVisuals();

            Vector3 at = ClosestPoint(transform.position) + Vector3.up * 0.6f;

            if (composition != null)
            {
                composition.Effects.Play(breakEffect, at);
                composition.WorldUi.ShowWarning(at + Vector3.up * 1.4f, $"{DisplayName} down!");
                composition.Compound.ReportDestroyed(this);
            }
        }

        /// <summary>Full health, and standing again if it had fallen.</summary>
        public void Repair()
        {
            bool wasDown = IsDestroyed;

            health = MaxHealth;
            lastHit = float.NegativeInfinity;
            ReleaseHealthBar();

            OnRepaired(wasDown);
            ApplyVisuals();

            if (!wasDown || visualRoot == null) return;

            // Rises back into place, so the morning shows what was rebuilt.
            Transform t = visualRoot.transform;
            Vector3 scale = t.localScale;

            t.DOKill(true);
            t.localScale = new Vector3(scale.x, scale.y * 0.05f, scale.z);
            t.DOScale(scale, 0.5f).SetEase(Ease.OutBack);
        }

        // --- Being repaired ----------------------------------------------------------------------

        string repairId;
        Transform repairAnchor;

        public string RepairId => repairId ??= $"structure:{name}:{GetInstanceID()}";

        public bool IsRepairableNow => present;

        public Transform RepairTarget => transform;

        /// <summary>Just over the top of the piece as it stands now, made the first time it is asked for.</summary>
        public Transform RepairAnchor
        {
            get
            {
                if (repairAnchor != null) return repairAnchor;

                repairAnchor = new GameObject("RepairAnchor").transform;
                repairAnchor.SetParent(transform, false);
                repairAnchor.position = transform.position + Vector3.up * (VisualTop() - transform.position.y);
                return repairAnchor;
            }
        }

        /// <summary>
        /// On the inside, a step back from the middle of the run: the worker never has to go out
        /// past the wall to mend it. Whichever side is nearer the restaurant when the compound has
        /// not said where inside is.
        /// </summary>
        public Vector3 RepairPoint
        {
            get
            {
                Vector3 normal = Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward;
                float stand = thickness * 0.5f + 0.9f;

                Vector3 front = transform.position + normal * stand;
                Vector3 back = transform.position - normal * stand;

                if (composition != null && composition.Compound.HasInterior)
                {
                    bool frontInside = composition.Compound.IsInside(front);
                    if (frontInside != composition.Compound.IsInside(back)) return frontInside ? front : back;
                }

                if (composition != null && composition.Restaurant.HasFootprint)
                {
                    Vector2 middle = composition.Restaurant.Footprint.center;
                    var centre = new Vector3(middle.x, transform.position.y, middle.y);
                    return (front - centre).sqrMagnitude <= (back - centre).sqrMagnitude ? front : back;
                }

                return front;
            }
        }

        /// <summary>A blow of the worker's hammer landed.</summary>
        public void OnStrike() => Shake();

        float VisualTop()
        {
            float top = transform.position.y + 2f;
            if (visualRoot == null) return top;

            bool any = false;
            foreach (Renderer r in visualRoot.GetComponentsInChildren<Renderer>())
            {
                top = any ? Mathf.Max(top, r.bounds.max.y) : r.bounds.max.y;
                any = true;
            }

            return any ? top : transform.position.y + 2f;
        }

        void Shake()
        {
            if (visualRoot == null || shakeStrength <= 0f) return;

            Transform t = visualRoot.transform;
            t.DOKill(true);
            t.DOShakePosition(0.22f, new Vector3(shakeStrength, 0f, shakeStrength), 18, 90f, false, true);
        }

        void DrawHealthBar()
        {
            bool show = present && !IsDestroyed && Time.time - lastHit < healthBarLinger && composition != null;

            if (!show)
            {
                ReleaseHealthBar();
                return;
            }

            Transform anchor = healthBarAnchor != null ? healthBarAnchor : transform;

            if (healthBar == null || !healthBar.IsLive || healthBar.Anchor != anchor)
                healthBar = composition.WorldUi.ShowHealthBar(anchor);

            if (healthBar != null) healthBar.Set(HealthFraction, health, MaxHealth, HealthFraction < 0.3f);
        }

        void ReleaseHealthBar()
        {
            if (healthBar == null) return;

            Transform anchor = healthBarAnchor != null ? healthBarAnchor : transform;
            if (healthBar.IsLive && healthBar.Anchor == anchor) healthBar.Release();
            healthBar = null;
        }

        // --- Geometry -----------------------------------------------------------------------------

        /// <summary>The footprint's half-sizes along local X and Z.</summary>
        protected Vector2 HalfSize => new Vector2(length * 0.5f, thickness * 0.5f);

        public Vector3 ClosestPoint(Vector3 from)
        {
            Quaternion turn = Quaternion.Euler(0f, Yaw, 0f);
            Vector3 local = Quaternion.Inverse(turn) * (from - transform.position);

            Vector2 half = HalfSize;
            local.x = Mathf.Clamp(local.x, -half.x, half.x);
            local.z = Mathf.Clamp(local.z, -half.y, half.y);
            local.y = 0f;

            return transform.position + turn * local;
        }

        public abstract bool Blocks(Vector3 from, Vector3 to, float radius, MoverKind mover);

        // --- For the subclass ---------------------------------------------------------------------

        /// <summary>Shows whichever state it is in now.</summary>
        protected abstract void ApplyVisuals();

        protected virtual void OnFallen() { }

        protected virtual void OnRepaired(bool wasDown) { }

        /// <summary>Editor wiring shared by every piece, for the setup tool.</summary>
        public void ConfigureStructure(GameLifetimeScope lifetimeScope, string label, float runLength,
                                       float runThickness, GameObject tiedTo, GameObject visuals,
                                       Transform barAnchor, PooledEffect fallEffect)
        {
            scope = lifetimeScope;
            displayName = label;
            length = runLength;
            thickness = runThickness;
            presentWhile = tiedTo;
            visualRoot = visuals;
            healthBarAnchor = barAnchor;
            breakEffect = fallEffect;
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.3f, 0.8f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, Yaw, 0f), Vector3.one);
            Gizmos.DrawWireCube(Vector3.up * 1f, new Vector3(length, 2f, thickness));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
