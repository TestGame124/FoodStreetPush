using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// Remembers how big the compound was before the car park needed it bigger.
    ///
    /// The setup command widens the boundary by a set of deltas, and a delta applied twice is
    /// twice the expansion. Without this, running the command a second time - which is the
    /// whole point of it being re-runnable - would push the walls another seven metres out, and
    /// a third run another seven. Nothing would look obviously broken until the compound
    /// reached the edge of the terrain.
    ///
    /// So the original rectangle is recorded the first time and the expansion is computed from
    /// it every time after. The component lives on the boundary itself rather than in the tool,
    /// because it is a fact about this scene and has to survive the editor being closed.
    ///
    /// Clearing <see cref="recorded"/> in the inspector is the supported way to say "the walls
    /// are where I want them now, treat this as the new original".
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ZoneBoundary : MonoBehaviour
    {
        [Tooltip("The boundary as it stood before any expansion, in world metres: min X, min Z, " +
                 "width, depth.")]
        [SerializeField] Rect original;

        [Tooltip("False until the original has been measured. Uncheck to re-measure from " +
                 "wherever the walls stand now.")]
        [SerializeField] bool recorded;

        public bool HasOriginal => recorded;

        public Rect Original => original;

        public void Record(Rect rectangle)
        {
            original = rectangle;
            recorded = true;
        }

        void OnDrawGizmosSelected()
        {
            if (!recorded) return;

            Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.8f);

            var centre = new Vector3(original.center.x, 0.2f, original.center.y);
            Gizmos.DrawWireCube(centre, new Vector3(original.width, 0.4f, original.height));
        }
    }
}
