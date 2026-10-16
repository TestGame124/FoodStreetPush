using System.Collections;
using System.Collections.Generic;
using FoodStreet.Navigation;
using FoodStreet.Placement;
using UnityEngine;

namespace LastOrders.Kitchen
{
    /// <summary>
    /// One length of the serving pass: the counter between the kitchen and the dining room where the
    /// cooks set finished dishes down and the waiters pick them up. A module - the diner's pass is as
    /// many of these as fit, each the same prefab (pfServingPass), with an opening between them.
    ///
    /// It knows only its own shape: where plates stand on its top (<see cref="Slots"/>), where a waiter
    /// stands to reach them (<see cref="ServicePoints"/>) and the floor it covers. Which dish is on
    /// which plate, and who is coming for it, is the <see cref="ServingPassBoard"/>'s business; every
    /// module joins it as it comes up.
    ///
    /// The counter is solid on both grids: walkers go round it, and nothing can be built on top of it.
    /// It has no collider - nothing taps it - so taps on a customer beyond it are never caught by it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ServingPass : MonoBehaviour
    {
        [Tooltip("Where plates stand on the counter top, in order along it.")]
        [SerializeField] Transform[] slots;

        [Tooltip("Where a waiter stands to take a plate: on the dining side, facing the counter.")]
        [SerializeField] Transform[] servicePoints;

        [Tooltip("The floor the counter covers, in its own space: centre and size, flat (x across, y deep).")]
        [SerializeField] Rect footprint = new Rect(-1.5f, -0.31f, 3f, 0.61f);

        [Tooltip("What the dishes look like. The board takes it from the first pass that has one.")]
        [SerializeField] PassDishSet dishes;

        readonly List<Rect> navArea = new List<Rect>(1);
        bool onNavGrid;

        public IReadOnlyList<Transform> Slots => slots;
        public IReadOnlyList<Transform> ServicePoints => servicePoints;
        public PassDishSet Dishes => dishes;

        /// <summary>The counter's footprint in the world, flat: x across, y along the world's Z.</summary>
        public Rect WorldFootprint
        {
            get
            {
                Vector3 a = transform.TransformPoint(new Vector3(footprint.xMin, 0f, footprint.yMin));
                Vector3 b = transform.TransformPoint(new Vector3(footprint.xMax, 0f, footprint.yMax));
                Vector3 c = transform.TransformPoint(new Vector3(footprint.xMin, 0f, footprint.yMax));
                Vector3 d = transform.TransformPoint(new Vector3(footprint.xMax, 0f, footprint.yMin));
                return Rect.MinMaxRect(Mathf.Min(a.x, b.x, c.x, d.x), Mathf.Min(a.z, b.z, c.z, d.z),
                                       Mathf.Max(a.x, b.x, c.x, d.x), Mathf.Max(a.z, b.z, c.z, d.z));
            }
        }

        /// <summary>The service point nearest <paramref name="from"/>.</summary>
        public Transform NearestServicePoint(Vector3 from)
        {
            Transform best = null;
            float bestDistance = float.MaxValue;
            if (servicePoints == null) return transform;

            foreach (Transform point in servicePoints)
            {
                if (point == null) continue;

                float d = (point.position - from).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = point;
                }
            }

            return best != null ? best : transform;
        }

        void OnEnable()
        {
            ServingPassBoard.Join(this);
            PlacementService.SetFixture(this, WorldFootprint);
            StartCoroutine(PutOnNavGrid());
        }

        void OnDisable()
        {
            ServingPassBoard.Leave(this);
            PlacementService.SetFixture(this, null);

            if (onNavGrid)
            {
                NavigationSystem nav = NavigationSystem.Instance;
                if (nav != null && nav.Grid != null) nav.Grid.SetStaticWalls(this, null);
                onNavGrid = false;
            }
        }

        /// <summary>
        /// The counter was carried somewhere else - back with the kitchen when the restaurant grows
        /// out the back: what it keeps clear of building and walking moves with it.
        /// </summary>
        public void Refit()
        {
            if (!isActiveAndEnabled) return;

            PlacementService.SetFixture(this, WorldFootprint);
            if (!onNavGrid) return;

            NavigationSystem nav = NavigationSystem.Instance;
            if (nav == null || nav.Grid == null) return;

            navArea.Clear();
            navArea.Add(WorldFootprint);
            nav.Grid.SetStaticWalls(this, navArea);
        }

        /// <summary>The nav grid can come up after the counter does: asked again until it is there.</summary>
        IEnumerator PutOnNavGrid()
        {
            var wait = new WaitForSeconds(0.25f);
            while (!onNavGrid)
            {
                NavigationSystem nav = NavigationSystem.Instance;
                if (nav != null && nav.Grid != null)
                {
                    navArea.Clear();
                    navArea.Add(WorldFootprint);
                    nav.Grid.SetStaticWalls(this, navArea);
                    onNavGrid = true;
                    yield break;
                }

                yield return wait;
            }
        }

        /// <summary>Set by the builder (<c>ServingPassModel</c>).</summary>
        public void Configure(Transform[] dishSlots, Transform[] standPoints, Rect covers, PassDishSet set)
        {
            slots = dishSlots;
            servicePoints = standPoints;
            footprint = covers;
            dishes = set;
        }

        void OnDrawGizmosSelected()
        {
            Rect r = WorldFootprint;
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(new Vector3(r.center.x, transform.position.y + 0.02f, r.center.y), new Vector3(r.width, 0.04f, r.height));

            if (servicePoints == null) return;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
            foreach (Transform point in servicePoints)
                if (point != null) Gizmos.DrawWireSphere(point.position + Vector3.up * 0.05f, 0.25f);
        }
    }
}
