using FoodStreet.Placement;
using LastOrders.Core;
using UnityEngine;

namespace LastOrders.Effects
{
    /// <summary>
    /// Makes putting something down feel like it. Whatever the player places on the build grid
    /// drops in, stretched, and lands with a squash and a wobble (<see cref="PlacementPop"/>); where
    /// it touches down a puff of dust rolls out round it, a ring spreads across the ground and a
    /// twinkle goes up, all sized to its cells. Something moved lands the same way from lower down.
    /// A run of floor tiles pops up one after another, rippling out from where the drag began.
    ///
    /// Listens to <see cref="PlacementService.Placed"/>, so it covers the shop, moving things in
    /// build mode, the build commands and the floor brush alike, and nothing the scene puts on the
    /// grid as it starts. Other builders can ask for the landing burst through <see cref="Burst"/>.
    /// </summary>
    public sealed class PlacementJuice : MonoBehaviour
    {
        [SerializeField] GameLifetimeScope scope;

        [Tooltip("Played where something lands, scaled to the size of its cells.")]
        [SerializeField] PooledEffect landing;

        [Header("Something new")]
        [SerializeField] PlacementPop.Shape placed = new PlacementPop.Shape(1.3f, 0.24f, 0.45f, 0.24f, 0.7f);

        [Tooltip("Extra metres it drops from for each metre across its cells, so a big station falls further.")]
        [SerializeField] float dropPerMetre = 0.12f;

        [Header("Something moved")]
        [SerializeField] PlacementPop.Shape moved = new PlacementPop.Shape(0.5f, 0.16f, 1f, 0.15f, 0.5f);

        [Header("Floor tiles")]
        [SerializeField] PlacementPop.Shape tile = new PlacementPop.Shape(0f, 0.2f, 0.15f, 0.12f, 0.35f);

        [Tooltip("Seconds a tile waits for each metre it is from the first one put down with it.")]
        [SerializeField] float tileRipple = 0.045f;

        [Tooltip("The longest any tile in a run waits.")]
        [SerializeField] float tileRippleLimit = 0.6f;

        static PlacementJuice active;

        EffectsService effects;
        int runFrame = -1;
        Vector3 runStart;

        /// <summary>The landing puff at <paramref name="at"/>, sized for something <paramref name="size"/>
        /// metres across. Nothing when there is no juice in the scene.</summary>
        public static void Burst(Vector3 at, float size)
        {
            if (active != null) active.PlayBurst(at, size);
        }

        void OnEnable()
        {
            active = this;
            PlacementService.Placed -= HandlePlaced;
            PlacementService.Placed += HandlePlaced;
        }

        void OnDisable()
        {
            PlacementService.Placed -= HandlePlaced;
            if (active == this) active = null;
        }

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
        }

        void HandlePlaced(PlacedObject_Done target, bool wasMoved)
        {
            if (target == null || target.isRemoved) return;

            PlacementService grid = GridBuildingSystem3D.Instance != null ? GridBuildingSystem3D.Instance.Placement : null;
            if (grid == null) return;

            Rect cells = grid.Footprint(target);
            var centre = new Vector3(cells.center.x, target.transform.position.y, cells.center.y);

            if (target.IsFloor())
            {
                // A run of tiles comes in one frame; each waits by how far it is from the first.
                if (Time.frameCount != runFrame)
                {
                    runFrame = Time.frameCount;
                    runStart = centre;
                }

                PlacementPop.Shape shape = tile;
                shape.delay = Mathf.Min(tileRippleLimit, Vector3.Distance(centre, runStart) * tileRipple);
                PlacementPop.Play(target.transform, centre, shape);
                return;
            }

            float across = Mathf.Max(cells.width, cells.height);

            PlacementPop.Shape landingShape = wasMoved ? moved : placed;
            if (!wasMoved) landingShape.drop += dropPerMetre * across;

            PlacementPop.Play(target.transform, centre, landingShape,
                              pop => PlayBurst(pop.Centre, across * (wasMoved ? 0.8f : 1f)));
        }

        void PlayBurst(Vector3 at, float size)
        {
            if (landing == null) return;

            if (effects == null)
            {
                GameComposition composition = scope != null ? scope.Composition : null;
                if (composition == null) return;
                effects = composition.Effects;
            }

            PooledEffect burst = effects.Play(landing, at + Vector3.up * 0.02f);
            if (burst != null) burst.transform.localScale = Vector3.one * Mathf.Clamp(size, 0.6f, 5f);
        }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(GameLifetimeScope lifetimeScope, PooledEffect landingBurst)
        {
            scope = lifetimeScope;
            landing = landingBurst;
        }
    }
}
