using DG.Tweening;
using FoodStreet.Navigation;
using LastOrders.Audio;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Effects;
using LastOrders.Interaction;
using LastOrders.Jobs;
using LastOrders.Perimeter;
using LastOrders.Repair;
using LastOrders.UI.World;
using UnityEngine;

namespace LastOrders.Defense
{
    /// <summary>
    /// A build slot in the strip outside the walls: a reinforced patch of ground that takes one
    /// barricade, trap or roll of wire.
    ///
    /// Everything it does is drawing and answering. It registers a <see cref="GroundDefenseUnit"/>
    /// with the ground defense service and shows what that unit says: an empty slot's survey pegs,
    /// or the model of whatever was built, standing, damaged or wrecked. Tapping an empty slot
    /// opens the pick-one popup the shelters use, with a button per kind; tapping a built one says
    /// how it is holding up.
    ///
    /// It also stands in for its unit wherever the rest of the game asks: the steering asks it
    /// whether a step is blocked (only a standing barricade blocks), a zombie that walked into it
    /// hits it, and the repair service sends a worker to anything worn - from the wall side, so
    /// nobody has to go round the front.
    /// </summary>
    public sealed class StripSlot : MonoBehaviour, ITappable, IInteractableObject, IMovementBarrier, IBreakable, IRepairable,
                                    IWorkStrikeReceiver
    {
        [SerializeField] GameLifetimeScope scope;

        [Tooltip("Stable identity. Recorded in progression flags, so renaming it forgets what was built here.")]
        [SerializeField] string slotId = "strip";

        [Tooltip("What can be built here, in the order the popup lists them.")]
        [SerializeField] GroundDefenseDefinition[] options;

        [Tooltip("Optional. Stands here from the start, never charged for.")]
        [SerializeField] GroundDefenseDefinition startsWith;

        [Header("Parts")]
        [Tooltip("What an empty slot shows: the pegs it is marked out with. Hidden once something is built.")]
        [SerializeField] GameObject emptyMarker;

        [Tooltip("Resized to the slot while it is empty and to what stands on it once built.")]
        [SerializeField] BoxCollider tapCollider;

        [Tooltip("Metres along the strip and across it: the patch an empty slot covers.")]
        [SerializeField] Vector2 slotSize = new Vector2(2.4f, 1.5f);

        [Tooltip("Played where something here is battered down, jams or is torn.")]
        [SerializeField] PooledEffect breakEffect;

        [Tooltip("The model standing here, when the scene was set up with one. Kept if it is what is built; replaced otherwise.")]
        [SerializeField] GameObject model;

        [SerializeField] GroundDefenseDefinition modelOf;

        [Header("Feedback")]
        [Tooltip("Seconds the health bar stays up after it was last hit, sprang or held something.")]
        [SerializeField] float healthBarLinger = 3f;

        GameComposition composition;
        GroundDefenseUnit unit;
        GroundDefenseView view;
        HealthBarView healthBar;
        Transform repairAnchor;
        float lastHit = float.NegativeInfinity;
        bool listedForRepair;
        bool shownBuilt;
        bool shownWorn;

        public string SlotId => slotId;
        public GroundDefenseUnit Unit => unit;

        public string DisplayName => unit != null && unit.IsBuilt ? unit.Definition.displayName : "Strip slot";

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                Debug.LogError($"[StripSlot] {name} found no GameLifetimeScope.", this);
                return;
            }

            unit = composition.GroundDefenses.Register(slotId, transform.position, transform.eulerAngles.y, options, startsWith);
            if (unit == null) return;

            unit.OnChanged += HandleChanged;
            unit.OnHit += HandleHit;
            unit.OnStrike += HandleStrike;
            unit.OnWorn += HandleWorn;

            MovementBarriers.Register(this);
            Show(animate: false);
        }

        void OnDestroy()
        {
            MovementBarriers.Unregister(this);
            if (unit == null) return;

            unit.OnChanged -= HandleChanged;
            unit.OnHit -= HandleHit;
            unit.OnStrike -= HandleStrike;
            unit.OnWorn -= HandleWorn;

            if (listedForRepair) composition.Repairs.Unregister(this);
            composition.GroundDefenses.Unregister(slotId);

            // Not while the scene is coming down: the pool would re-parent the bar under a root being destroyed.
            if (gameObject.scene.isLoaded) ReleaseHealthBar();
            unit = null;
        }

        void Update()
        {
            if (unit == null) return;

            unit.Position = transform.position;
            unit.Yaw = transform.eulerAngles.y;

            if (view != null) view.Hold(unit.Caught);
            if (unit.Caught > 0) lastHit = Time.time;

            DrawHealthBar();
        }

        // --- Drawing the unit -------------------------------------------------------------------------

        void Show(bool animate)
        {
            bool built = unit.IsBuilt;
            SetActive(emptyMarker, !built);

            if (built) EnsureModel(unit.Definition);
            else ClearModel();

            if (view != null && built) view.Show(unit.HealthFraction, unit.IsWorn, unit.Definition.damagedBelow);

            FitCollider();
            ListForRepair(built);

            shownBuilt = built;
            shownWorn = unit.IsWorn;

            if (!animate || !built || model == null) return;

            // Rises into place rather than blinking in, so the player sees what they bought.
            Transform t = model.transform;
            t.DOKill(true);
            t.localScale = new Vector3(1f, 0.05f, 1f);
            t.DOScale(Vector3.one, 0.45f).SetEase(Ease.OutBack);
        }

        void EnsureModel(GroundDefenseDefinition definition)
        {
            if (model != null && modelOf == definition)
            {
                view = model.GetComponent<GroundDefenseView>();
                return;
            }

            ClearModel();

            if (definition.prefab == null)
            {
                Debug.LogWarning($"[StripSlot] '{definition.displayName}' has no prefab, so {name} shows nothing.", this);
                return;
            }

            model = Instantiate(definition.prefab, transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            modelOf = definition;
            view = model.GetComponent<GroundDefenseView>();
        }

        void ClearModel()
        {
            if (model != null) Destroy(model);
            model = null;
            modelOf = null;
            view = null;
        }

        void FitCollider()
        {
            if (tapCollider == null) return;

            Vector3 size = unit.IsBuilt
                ? new Vector3(unit.Definition.footprint.x + 0.2f, ModelHeight(), unit.Definition.footprint.y + 0.2f)
                : new Vector3(slotSize.x, 0.4f, slotSize.y);

            tapCollider.size = size;
            tapCollider.center = new Vector3(0f, size.y * 0.5f, 0f);
        }

        float ModelHeight()
        {
            if (model == null) return 0.6f;

            float top = 0.3f;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                top = Mathf.Max(top, r.bounds.max.y - transform.position.y);

            return top;
        }

        void ListForRepair(bool built)
        {
            if (built == listedForRepair) return;

            if (built) composition.Repairs.Register(this);
            else composition.Repairs.Unregister(this);

            listedForRepair = built;
        }

        /// <summary>Built, or put right. Rises into place when it is new or was down; a patched one just shows patched.</summary>
        void HandleChanged() => Show(animate: !shownBuilt || shownWorn);

        void HandleHit()
        {
            lastHit = Time.time;
            if (view != null) view.Hit();
            if (view != null) view.Show(unit.HealthFraction, unit.IsWorn, unit.Definition.damagedBelow);
        }

        void HandleStrike()
        {
            lastHit = Time.time;
            if (view != null) view.Strike();
            SoundController.Play(SoundId.TrapSnap, transform.position);
        }

        void HandleWorn()
        {
            shownWorn = true;
            ReleaseHealthBar();
            if (view != null) view.Show(unit.HealthFraction, true, unit.Definition.damagedBelow);

            string what = unit.Definition.displayName;
            string state = unit.Kind == GroundDefenseKind.Barricade ? "down" : unit.Kind == GroundDefenseKind.Trap ? "jammed" : "torn";

            Vector3 at = transform.position + Vector3.up * 0.6f;
            composition.Effects.Play(breakEffect, at);
            SoundController.Play(SoundId.StructureBreak, at);
            composition.WorldUi.ShowWarning(at + Vector3.up * 1.2f, $"{what} {state}!");
        }

        void DrawHealthBar()
        {
            bool show = unit.IsWorking && Time.time - lastHit < healthBarLinger && unit.Health < unit.MaxHealth;
            if (!show)
            {
                ReleaseHealthBar();
                return;
            }

            Transform anchor = RepairAnchor;
            if (healthBar == null || !healthBar.IsLive || healthBar.Anchor != anchor) healthBar = composition.WorldUi.ShowHealthBar(anchor);
            if (healthBar != null) healthBar.Set(unit.HealthFraction, unit.Health, unit.MaxHealth, unit.HealthFraction < 0.3f);
        }

        void ReleaseHealthBar()
        {
            if (healthBar == null) return;

            if (healthBar.IsLive && healthBar.Anchor == repairAnchor) healthBar.Release();
            healthBar = null;
        }

        // --- Tapping and building ---------------------------------------------------------------------

        public bool OnTouch() => TapFeedback.Report(OnTapped());

        public string OnTapped()
        {
            if (unit == null) return "This slot is not connected to the game.";
            if (unit.IsBuilt) return composition.GroundDefenses.Describe(slotId);

            return OpenPopup() ? null : composition.GroundDefenses.Describe(slotId);
        }

        bool OpenPopup() => BuildChoicePopup.Open(composition, this, () => composition.GroundDefenses.GetBuildOffer(slotId), Take);

        void Take(int index)
        {
            bool done = composition.GroundDefenses.TryBuild(slotId, index, out string message);
            TapFeedback.Report(message);

            // A refusal leaves the popup open with the reason on it.
            if (done) BuildChoicePopup.Close(composition, this);
        }

        // --- Standing in for the unit -------------------------------------------------------------------

        public bool Blocks(Vector3 from, Vector3 to, float radius, MoverKind mover) =>
            unit != null && unit.Blocks(from, to, radius, mover);

        public bool IsInTheWay => unit != null && unit.IsBarricade && unit.IsWorking;

        public Vector3 ClosestPoint(Vector3 from) => unit != null ? unit.ClosestPoint(from) : transform.position;

        public bool TakeHit(float damage, Vector3 from) => unit != null && unit.TakeHit(damage);

        public string RepairId => $"strip:{slotId}";

        public float Health => unit != null ? unit.Health : 0f;

        public float MaxHealth => unit != null ? unit.MaxHealth : 0f;

        public bool IsDestroyed => unit != null && unit.IsWorn;

        public bool IsRepairableNow => unit != null && unit.IsBuilt;

        public Transform RepairTarget => transform;

        /// <summary>Just over the top of what stands here, made the first time it is asked for.</summary>
        public Transform RepairAnchor
        {
            get
            {
                if (repairAnchor != null) return repairAnchor;

                repairAnchor = new GameObject("RepairAnchor").transform;
                repairAnchor.SetParent(transform, false);
                repairAnchor.localPosition = Vector3.up * Mathf.Max(0.8f, ModelHeight() + 0.1f);
                return repairAnchor;
            }
        }

        /// <summary>On the wall side of it: a worker comes out to it but never has to go round the front.</summary>
        public Vector3 RepairPoint
        {
            get
            {
                float depth = unit != null && unit.IsBuilt ? unit.Definition.footprint.y : slotSize.y;
                return transform.position - transform.forward * (depth * 0.5f + 0.6f);
            }
        }

        public void Repair() => unit?.Repair();

        /// <summary>A blow of the worker's hammer landed.</summary>
        public void OnStrike()
        {
            if (view != null) view.Hit();
            SoundController.Play(SoundId.WorkHammer, transform.position);
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(GameLifetimeScope lifetimeScope, string id, GroundDefenseDefinition[] choices,
                              GroundDefenseDefinition starting, GameObject marker, BoxCollider tap, Vector2 size, PooledEffect fall)
        {
            scope = lifetimeScope;
            slotId = id;
            options = choices;
            startsWith = starting;
            emptyMarker = marker;
            tapCollider = tap;
            slotSize = size;
            breakEffect = fall;
        }

        /// <summary>Editor wiring for the model shown in the scene before the game runs.</summary>
        public void ConfigureModel(GameObject standing, GroundDefenseDefinition of)
        {
            model = standing;
            modelOf = of;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.95f, 0.7f, 0.2f, 0.9f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.up * 0.05f, new Vector3(slotSize.x, 0.1f, slotSize.y));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
