using UnityEngine;

namespace FoodStreet.Navigation
{
    /// <summary>
    /// Walks a <see cref="FlowField"/> instead of a path.
    ///
    /// The trade against <see cref="PathFollower"/> is the usual one: no per-agent search,
    /// so a crowd all heading for the same objective costs one field between them, but the
    /// destination is fixed by whoever seeded the field. Use this for the many, and
    /// <see cref="PathFollower"/> for the few with somewhere specific to be.
    ///
    /// Both feed the same <see cref="SteeringAgent"/>, so they move and avoid identically.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SteeringAgent))]
    public sealed class FlowFieldFollower : MonoBehaviour
    {
        [SerializeField] FlowFieldId field = FlowFieldId.Doors;

        [Tooltip("Blend between the cell's direction and its neighbour's, so agents do not " +
                 "visibly snap between the eight field directions when crossing a cell edge.")]
        [SerializeField] bool smoothDirection = true;

        [Tooltip("Stop once integrated cost drops below this. Zero means walk right onto " +
                 "the seed tile.")]
        [SerializeField] int arrivalCost = 0;

        public FlowFieldId Field
        {
            get => field;
            set => field = value;
        }

        /// <summary>Set false to hold position without giving up the field.</summary>
        public bool active = true;

        /// <summary>True when the field has led this agent as far as it goes — either onto
        /// the objective, or into a pocket the field never reached.</summary>
        public bool HasArrived { get; private set; }

        SteeringAgent steering;

        bool hasFallback;
        Vector3 fallback;

        /// <summary>
        /// Where to head whenever the field cannot say: when it has no objective at all, or this
        /// agent stands somewhere it never reached. Without one the agent stands still there.
        /// </summary>
        public void SetFallback(Vector3 point)
        {
            fallback = point;
            hasFallback = true;
        }

        public void ClearFallback() => hasFallback = false;

        void Awake()
        {
            steering = GetComponent<SteeringAgent>();
        }

        void Update()
        {
            if (!active) return;

            NavigationSystem nav = NavigationSystem.Instance;
            if (nav == null || nav.Grid == null) return;

            FlowField flow = nav.GetFlowField(field);
            bool valid = flow != null && flow.IsValid;

            NavGrid grid = nav.Grid.Grid;
            Vector3 position = transform.position;
            Vector2Int cell = grid.WorldToCell(position);

            // Clearance treats off-grid as blocked, so the field never covers the outermost
            // cells: its reach starts this many cells in from every edge.
            int inset = nav.Grid.ClearanceForRadius(valid ? flow.Settings.AgentRadius : steering.Radius);

            if (!grid.InBounds(cell) || (IsOnRim(grid, cell, inset) && (!valid || flow.GetIntegratedCost(cell) == FlowField.Unreachable)))
            {
                // Off the grid, or on the rim the field does not reach - which is where every
                // enemy starts, because lanes are placed beyond the plot's edge so that they
                // walk in. The field knows nothing out here, so head straight in to the nearest
                // cell it does cover and pick it up from there. Doing nothing instead left every
                // agent standing where it spawned for the whole night. Needs no field at all, so
                // it is asked before the field is.
                HasArrived = false;
                steering.SetDesiredDirection(grid.CellToWorld(Inset(grid, cell, inset)) - position);
                return;
            }

            if (!valid)
            {
                // A field with no objective - nothing seeded it. Head for the fallback rather than
                // stand there all night, which is what every zombie did when the restaurant's door
                // stopped being a grid object.
                HasArrived = false;
                if (hasFallback) steering.SetDesiredDirection(fallback - position);
                return;
            }

            int cost = flow.GetIntegratedCost(cell);
            if (cost == FlowField.Unreachable)
            {
                // Standing somewhere the field never reached. Rather than guess a direction - how
                // agents end up jittering against a wall - head straight for the fallback when there
                // is one: whatever stands in the way is then what a zombie starts hitting.
                HasArrived = false;
                if (hasFallback) steering.SetDesiredDirection(fallback - position);
                return;
            }

            if (cost <= arrivalCost)
            {
                HasArrived = true;
                return;
            }

            HasArrived = false;

            Vector3 direction = flow.GetDirection(cell);
            if (direction == Vector3.zero) return;

            if (smoothDirection)
            {
                direction = Blend(flow, grid, cell, position, direction);
            }

            steering.SetDesiredDirection(direction);
        }

        static bool IsOnRim(NavGrid grid, Vector2Int cell, int inset) =>
            cell.x < inset || cell.y < inset || cell.x >= grid.Width - inset || cell.y >= grid.Height - inset;

        /// <summary>The nearest cell at least <paramref name="inset"/> cells in from every edge.</summary>
        static Vector2Int Inset(NavGrid grid, Vector2Int cell, int inset) => new Vector2Int(
            Mathf.Clamp(cell.x, inset, grid.Width - 1 - inset),
            Mathf.Clamp(cell.y, inset, grid.Height - 1 - inset));

        /// <summary>
        /// Mixes in the direction of the cell being approached, weighted by how far across
        /// the current cell the agent has travelled. Costs one extra lookup and removes the
        /// eight-way staircase the raw field would otherwise produce.
        /// </summary>
        static Vector3 Blend(FlowField flow, NavGrid grid, Vector2Int cell, Vector3 position, Vector3 direction)
        {
            Vector3 centre = grid.CellToWorld(cell);
            Vector3 offset = position - centre;

            float t = Mathf.Clamp01(
                new Vector2(offset.x, offset.z).magnitude / (grid.CellSize * 0.5f));
            if (t <= 0.001f) return direction;

            Vector2Int ahead = cell + new Vector2Int(
                Mathf.RoundToInt(direction.x),
                Mathf.RoundToInt(direction.z));

            Vector3 aheadDirection = flow.GetDirection(ahead);
            if (aheadDirection == Vector3.zero) return direction;

            Vector3 blended = Vector3.Lerp(direction, aheadDirection, t * 0.5f);
            return blended.sqrMagnitude > 0.0001f ? blended.normalized : direction;
        }
    }
}
