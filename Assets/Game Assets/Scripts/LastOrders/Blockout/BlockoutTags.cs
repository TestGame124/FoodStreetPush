using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.Blockout
{
    /// <summary>
    /// Names a point from the measured plan so the validator can find it by identity rather
    /// than by transform search.
    ///
    /// The gizmo matters more than it looks: an empty GameObject with no renderer is invisible
    /// in the scene view, and a plan made of invisible nodes cannot be checked by eye.
    /// </summary>
    public sealed class BlockoutMarker : MonoBehaviour
    {
        [Tooltip("Matches an entry in BlockoutSchedule.")]
        public string id;

        [TextArea] public string note;

        [Tooltip("Drawn at the node so route and approach markers read differently.")]
        public Color gizmoColour = new Color(0.3f, 0.9f, 1f);

        public float gizmoRadius = 0.25f;

        void OnDrawGizmos()
        {
            Gizmos.color = gizmoColour;
            Gizmos.DrawSphere(transform.position, gizmoRadius);

            Gizmos.color = new Color(gizmoColour.r, gizmoColour.g, gizmoColour.b, 0.35f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.5f);
        }
    }

    /// <summary>
    /// The entrance debris of 13 step 1: the first thing the player clears.
    ///
    /// Tagged rather than merely placed because every route check has to know about it. B1
    /// fills the whole 2 m door throat, so a reachability pass run against the day 1 state will
    /// correctly report the diner unreachable - the validator needs to be able to lift exactly
    /// this object and re-run, which means finding it without matching on a name.
    /// </summary>
    public sealed class BlockoutClearable : MonoBehaviour
    {
        [Tooltip("Wood granted when cleared. 13 step 1 and the 14 ledger both say 8.")]
        public int woodReward = 8;

        [Tooltip("Seconds of work after travel. 5 sets the tutorial repair band from 5.")]
        public float workSeconds = 5f;

        public bool IsCleared { get; private set; }

        /// <summary>Hides the obstruction. The real clearing job, when it exists, calls this.</summary>
        public void Clear()
        {
            IsCleared = true;
            gameObject.SetActive(false);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(transform.position, transform.localScale * 1.05f);
        }
    }

    /// <summary>
    /// Holds the diner's roof and its camera-facing wall, per 17's "hide its roof and fade
    /// camera-facing walls in normal gameplay".
    ///
    /// This only wires the references. The fade itself belongs to a runtime that knows when an
    /// interaction is selected (20: "tall trees and roof pieces fade if they obscure selected
    /// interactions"), and inventing that here would be guessing at a system that does not
    /// exist yet. Building the scene so it is one component away is the useful part.
    /// </summary>
    public sealed class DinerCutaway : MonoBehaviour
    {
        [Tooltip("Hidden by default so the interior is visible in the blockout.")]
        public GameObject roof;

        [Tooltip("The south wall: at yaw 0 and a 60 degree pitch, this is the wall between the " +
                 "camera and the dining room.")]
        public List<Renderer> cameraFacingWalls = new List<Renderer>();

        public void SetRoofVisible(bool visible)
        {
            if (roof != null) roof.SetActive(visible);
        }
    }
}
