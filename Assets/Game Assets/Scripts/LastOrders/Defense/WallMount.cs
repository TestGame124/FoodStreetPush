using DG.Tweening;
using FoodStreet.Placement;
using LastOrders.Core;
using LastOrders.Perimeter;
using UnityEngine;

namespace LastOrders.Defense
{
    /// <summary>
    /// Lets a defense bought from the shop go up on the compound wall: set down over a stretch of
    /// wall, it is mounted on a bastion - the wall thickened to the size of its footprint, as a wall
    /// site's is - instead of standing in the wall at its foot.
    ///
    /// On the placed defense, the bastion is a real <see cref="CompoundWall"/> of its own: zombies
    /// batter it, it cracks and falls to rubble and a worker rebuilds it, and while it is down the
    /// defense on it is knocked over and out of action, exactly as on a wall site. On open ground
    /// there is no bastion and the defense stands on the grass.
    ///
    /// The same component on the ghost being dragged shows where it would go - up on its bastion
    /// over a wall, on the ground anywhere else - so the player sees it before buying. It keeps
    /// running there (<see cref="IPlacementPreview"/>) but only moves things about.
    ///
    /// What it is standing on is <see cref="WallFooting"/>'s answer, the same one the shop's placement
    /// rule gives, worked out again whenever the defense is put down somewhere new.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class WallMount : MonoBehaviour, IPlacementPreview
    {
        [Tooltip("What goes up on the wall: the defense, or the ghost's model of it. Stands at the footprint's middle.")]
        [SerializeField] Transform lifted;

        [Tooltip("The bastion: shown, and turned to the wall's line, only while mounted.")]
        [SerializeField] GameObject bastion;

        [Tooltip("The bastion's wall piece. Empty on the ghost, which has only its look.")]
        [SerializeField] CompoundWall bastionWall;

        [Tooltip("The defense itself, knocked over while the bastion is down. Empty on the ghost.")]
        [SerializeField] DefensePost post;

        [Tooltip("Cells, along the placed object's own X and Z. The footprint's middle is half of it.")]
        [SerializeField] Vector2 footprint = new Vector2(2f, 2f);

        [Tooltip("Metres up the defense stands on a bastion: its top.")]
        [SerializeField] float mountHeight = 2.3f;

        [Header("Knocked over")]
        [SerializeField] float toppleDegrees = 24f;
        [SerializeField] float toppleDrop = 1.7f;

        GameComposition composition;
        bool lookedForComposition;

        Vector3 lastPosition;
        Quaternion lastRotation;
        bool evaluated;

        bool mounted;
        bool toppled;
        Vector3 standAt;

        /// <summary>Up on a bastion over the wall, rather than on the ground.</summary>
        public bool IsMounted => mounted;

        /// <summary>The wall piece it stands on while mounted: what a zombie has to bring down to get at it.</summary>
        public CompoundWall Bastion => mounted ? bastionWall : null;

        public WallFooting.Result Footing { get; private set; }

        Vector3 FootprintMiddle => transform.TransformPoint(new Vector3(footprint.x * 0.5f, 0f, footprint.y * 0.5f));

        void OnEnable() => evaluated = false;

        void OnDisable()
        {
            if (lifted != null) lifted.DOKill();
            if (toppled) Untopple(instant: true);
            evaluated = false;
        }

        void LateUpdate()
        {
            if (!evaluated || transform.position != lastPosition || transform.rotation != lastRotation)
            {
                lastPosition = transform.position;
                lastRotation = transform.rotation;
                evaluated = true;
                Evaluate();
            }

            KnockOver();
        }

        // --- Where it stands ----------------------------------------------------------------------------

        void Evaluate()
        {
            Vector3 middle = FootprintMiddle;
            Footing = WallFooting.Find(middle, footprint, transform);
            bool wall = Footing.Kind == WallFooting.Kind.Wall;

            if (toppled) Untopple(instant: true);

            if (bastion != null)
            {
                if (wall)
                {
                    bastion.transform.SetPositionAndRotation(middle, Quaternion.Euler(0f, OutwardYaw(Footing.Wall, middle), 0f));

                    // A new spot on the wall gets a sound bastion: one put away battered comes back mended.
                    if (!mounted && bastionWall != null && bastionWall.Health < bastionWall.MaxHealth) bastionWall.Repair();
                }

                ShowBastion(wall);
            }

            mounted = wall;
            standAt = middle + (wall ? Vector3.up * mountHeight : Vector3.zero);

            if (lifted == null) return;
            lifted.DOKill();
            lifted.position = standAt;
        }

        /// <summary>
        /// The real bastion comes and goes with its wall piece. The ghost's stays active and only shows or
        /// hides its renderers: the ghost tints the renderers it found when it was made, active ones only.
        /// </summary>
        void ShowBastion(bool on)
        {
            if (bastionWall != null)
            {
                if (bastion.activeSelf != on) bastion.SetActive(on);
                return;
            }

            if (!bastion.activeSelf) bastion.SetActive(true);
            foreach (Renderer r in bastion.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        }

        /// <summary>
        /// The wall's own yaw, turned round if need be so the bastion's +Z - its parapet side - faces out
        /// of the compound. Away from the restaurant when the compound has not said where inside is.
        /// </summary>
        float OutwardYaw(CompoundWall wall, Vector3 middle)
        {
            float yaw = WallFooting.Yaw(wall);
            Vector3 normal = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

            GameComposition game = Composition();
            Vector3 ahead = wall.transform.position + normal * 2f, behind = wall.transform.position - normal * 2f;

            if (game != null && game.Compound.HasInterior)
            {
                bool aheadInside = game.Compound.IsInside(ahead);
                if (aheadInside != game.Compound.IsInside(behind)) return aheadInside ? yaw + 180f : yaw;
            }

            if (game != null && game.Restaurant.HasFootprint)
            {
                Vector2 c = game.Restaurant.Footprint.center;
                var centre = new Vector3(c.x, middle.y, c.y);
                return (ahead - centre).sqrMagnitude < (behind - centre).sqrMagnitude ? yaw + 180f : yaw;
            }

            return yaw;
        }

        GameComposition Composition()
        {
            if (composition != null || lookedForComposition) return composition;

            lookedForComposition = true;
            var scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;
            return composition;
        }

        // --- Knocked over -------------------------------------------------------------------------------

        /// <summary>The bastion under it fell, or was rebuilt: the defense goes over with it, and is set up again with it.</summary>
        void KnockOver()
        {
            if (post == null || post.Unit == null || lifted == null) return;

            bool down = mounted && bastionWall != null && bastionWall.IsDestroyed;
            if (down == toppled) return;

            if (down) Topple();
            else Untopple(instant: false);
        }

        void Topple()
        {
            toppled = true;
            Composition()?.Defense.SetOutOfAction(post.PostId, true);

            lifted.DOKill();
            lifted.DOMove(standAt + Vector3.down * toppleDrop, 0.35f).SetEase(Ease.InQuad);
            lifted.DOLocalRotate(new Vector3(0f, 0f, toppleDegrees), 0.35f).SetEase(Ease.InQuad);
            Composition()?.WorldUi.ShowWarning(standAt + Vector3.up * 1.5f, $"{post.DisplayName} knocked over!");
        }

        void Untopple(bool instant)
        {
            toppled = false;
            if (post != null && post.Unit != null) Composition()?.Defense.SetOutOfAction(post.PostId, false);
            if (lifted == null) return;

            lifted.DOKill();
            if (instant)
            {
                lifted.position = standAt;
                lifted.localRotation = Quaternion.identity;
                return;
            }

            lifted.DOMove(standAt, 0.4f).SetEase(Ease.OutBack);
            lifted.DOLocalRotate(Vector3.zero, 0.4f).SetEase(Ease.OutBack);
        }

        /// <summary>Editor wiring, for the shop's prefab generator.</summary>
        public void Configure(Transform raised, GameObject block, CompoundWall blockWall, DefensePost defense, Vector2 cells, float height)
        {
            lifted = raised;
            bastion = block;
            bastionWall = blockWall;
            post = defense;
            footprint = cells;
            mountHeight = height;
        }
    }
}
