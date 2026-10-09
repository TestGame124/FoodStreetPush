using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.Expansion;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Tells every expansion's new wall which land lies on the far side of each section.
    ///
    /// Plots claimed side by side must not keep a wall down the middle, and a plot claimed beside
    /// one that is already the restaurant's must not raise one there. The build plan handles that
    /// at runtime once each section knows what is beyond it; this works that out from where the
    /// sections stand, so the courtyard's walls learn about the plots laid out next to it without
    /// either setup knowing about the other. Run by both, after either has rebuilt a boundary.
    /// </summary>
    public static class ExpansionNeighbours
    {
        /// <summary>How far past a section's line the land beyond is looked for.</summary>
        const float Probe = 1f;

        /// <summary>Fills in the land beyond every section of every build plan in the scene. Returns how many face other land.</summary>
        public static int Assign(Scene scene)
        {
            var plans = new List<(ExpansionArea area, ExpansionBuildPlan plan, Rect land)>();

            foreach (ExpansionArea area in Object.FindObjectsByType<ExpansionArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (area.gameObject.scene != scene || area.Definition == null) continue;

                var plan = new SerializedObject(area).FindProperty("buildPlan").objectReferenceValue as ExpansionBuildPlan;
                if (plan == null || plan.Count == 0) continue;

                if (TryLand(plan, out Rect land)) plans.Add((area, plan, land));
            }

            int facing = 0;

            foreach ((ExpansionArea area, ExpansionBuildPlan plan, Rect land) in plans)
            {
                var pieces = new Transform[plan.Count];
                var beyond = new ExpansionAreaDefinition[plan.Count];

                for (int i = 0; i < plan.Count; i++)
                {
                    pieces[i] = plan.Piece(i);
                    if (pieces[i] == null || !TryLine(pieces[i], out bool alongX, out Vector3 centre, out _)) continue;

                    // Out of this land, across the line.
                    Vector2 p = new Vector2(centre.x, centre.z);
                    Vector2 outward = alongX
                        ? new Vector2(0f, Mathf.Sign(p.y - land.center.y))
                        : new Vector2(Mathf.Sign(p.x - land.center.x), 0f);
                    Vector2 probe = p + outward * Probe;

                    foreach ((ExpansionArea other, _, Rect otherLand) in plans)
                    {
                        if (other == area || !otherLand.Contains(probe)) continue;

                        beyond[i] = other.Definition;
                        facing++;
                        break;
                    }
                }

                Undo.RecordObject(plan, "Land beyond the wall");
                plan.Configure(pieces, beyond);
                EditorUtility.SetDirty(plan);
            }

            return facing;
        }

        /// <summary>The land a plan walls in: the rectangle its sections span.</summary>
        public static bool TryLand(ExpansionBuildPlan plan, out Rect land)
        {
            float xMin = float.MaxValue, xMax = float.MinValue, zMin = float.MaxValue, zMax = float.MinValue;
            bool any = false;

            for (int i = 0; i < plan.Count; i++)
            {
                Transform piece = plan.Piece(i);
                if (piece == null || !TryLine(piece, out bool alongX, out Vector3 centre, out float length)) continue;

                Vector3 half = (alongX ? Vector3.right : Vector3.forward) * (length * 0.5f);
                foreach (Vector3 end in new[] { centre - half, centre + half })
                {
                    xMin = Mathf.Min(xMin, end.x);
                    xMax = Mathf.Max(xMax, end.x);
                    zMin = Mathf.Min(zMin, end.z);
                    zMax = Mathf.Max(zMax, end.z);
                }

                any = true;
            }

            land = any ? Rect.MinMaxRect(xMin, zMin, xMax, zMax) : default;
            return any && land.width > 0.5f && land.height > 0.5f;
        }

        /// <summary>
        /// A boundary piece - a cube stretched along one axis, as the compound's own are - read as a
        /// line: which way it runs, its middle on the ground and its length.
        /// </summary>
        public static bool TryLine(Transform piece, out bool alongX, out Vector3 centre, out float length)
        {
            alongX = false;
            centre = piece.position;
            length = 0f;

            var filter = piece.GetComponent<MeshFilter>();
            Bounds local = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);

            Vector3 x = piece.TransformVector(Vector3.right * local.size.x);
            Vector3 z = piece.TransformVector(Vector3.forward * local.size.z);
            x.y = 0f;
            z.y = 0f;

            Vector3 along = x.sqrMagnitude >= z.sqrMagnitude ? x : z;
            length = along.magnitude;
            if (length < 0.5f) return false;

            alongX = Mathf.Abs(along.x) >= Mathf.Abs(along.z);
            centre = piece.TransformPoint(local.center);
            centre.y = 0f;
            return true;
        }
    }
}
