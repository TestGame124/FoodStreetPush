using LastOrders.Core;
using UnityEngine;

namespace LastOrders.Perimeter
{
    /// <summary>
    /// A rectangle of ground behind the compound wall. Together they are "inside": a zombie
    /// standing on any of them means the compound is breached.
    ///
    /// Authored rather than worked out from the wall sections, because a wall only says where
    /// the line is, not which side of it is ours. Tied to another object with
    /// <see cref="presentWhile"/>, a zone counts only while that object is active - the yard
    /// beyond the north wall is inside once it has been claimed and not before.
    /// </summary>
    [DefaultExecutionOrder(1110)]
    public sealed class CompoundZone : MonoBehaviour
    {
        [SerializeField] GameLifetimeScope scope;

        [Tooltip("The ground behind the wall, world metres: X and Z of one corner, then width and depth.")]
        [SerializeField] Rect area = new Rect(0f, 0f, 10f, 10f);

        [Tooltip("Optional. Counts as inside only while that object is active.")]
        [SerializeField] GameObject presentWhile;

        GameComposition composition;
        bool registered;

        public Rect Area => area;

        /// <summary>True while this ground counts as inside: switched on, and claimed if it is tied to a claim.</summary>
        public bool IsPresent => isActiveAndEnabled && (presentWhile == null || presentWhile.activeInHierarchy);

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
                Debug.LogWarning($"[CompoundZone] {name} found no GameLifetimeScope, so nothing can breach it.", this);
        }

        void Update()
        {
            bool should = IsPresent;
            if (should != registered) SetRegistered(should);
        }

        void OnDisable() => SetRegistered(false);

        void SetRegistered(bool on)
        {
            if (composition == null)
            {
                registered = false;
                return;
            }

            registered = on;

            if (on) composition.Compound.RegisterZone(this, area);
            else composition.Compound.UnregisterZone(this);
        }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(GameLifetimeScope lifetimeScope, Rect ground, GameObject tiedTo)
        {
            scope = lifetimeScope;
            area = ground;
            presentWhile = tiedTo;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.6f);
            Gizmos.DrawWireCube(new Vector3(area.center.x, 0.2f, area.center.y), new Vector3(area.width, 0.4f, area.height));
        }
    }
}
