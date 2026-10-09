using System;
using DG.Tweening;
using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Expansion
{
    /// <summary>
    /// The new boundary of an area, raised a section at a time while the area expands.
    ///
    /// The sections are listed in the order they go up, and progress maps straight onto that
    /// list: at a third of the way through the build, a third of the wall stands. Each section
    /// rises out of the ground as it is placed and reports where, so the area can put a puff of
    /// dust there. Nothing here knows about timers or costs; it is told how far along it is.
    ///
    /// A section can stand between this land and another piece of land that can be claimed:
    /// the plot next door. It is a wall only while the land beyond it is not the restaurant's
    /// too. Beyond land that is already claimed it is never raised, and once the land beyond
    /// is claimed it comes down - two claimed plots side by side are one piece of ground, with
    /// no wall down the middle. The other plot's own section on the same line follows the same
    /// rule from its side, so between them exactly one stands while exactly one is claimed.
    /// </summary>
    public sealed class ExpansionBuildPlan : MonoBehaviour
    {
        [Tooltip("The sections, in build order. Each is shown when the build reaches it.")]
        [SerializeField] Transform[] pieces;

        [Tooltip("The land on the far side of each section, by index, where that land can be " +
                 "claimed. Empty: open ground. A section stands only while the land beyond it is " +
                 "not claimed.")]
        [SerializeField] ExpansionAreaDefinition[] beyond;

        [Tooltip("Seconds a section takes to rise into place.")]
        [SerializeField] float riseSeconds = 0.45f;

        Vector3[] basePositions;
        Vector3[] baseScales;
        int shown;
        Func<string, bool> isClaimed;

        public int Count => pieces != null ? pieces.Length : 0;

        /// <summary>The land on the far side of section <paramref name="index"/>, or null for open ground.</summary>
        public ExpansionAreaDefinition Beyond(int index) =>
            beyond != null && index >= 0 && index < beyond.Length ? beyond[index] : null;

        /// <summary>The section at <paramref name="index"/>, for tools that lay walls along them.</summary>
        public Transform Piece(int index) => pieces[index];

        /// <summary>
        /// How to tell whether a piece of land is claimed, by area id. Until this is given every
        /// section counts as standing on open ground.
        /// </summary>
        public void SetClaimedTest(Func<string, bool> test) => isClaimed = test;

        /// <summary>Takes every section down. The state before an expansion starts.</summary>
        public void HideAll()
        {
            Cache();

            for (int i = 0; i < Count; i++) Lower(i);

            shown = 0;
        }

        /// <summary>
        /// Raises every section up to <paramref name="progress"/> (0 to 1) that is not up yet,
        /// calling <paramref name="placed"/> with the foot of each as it goes up. A section facing
        /// claimed land is passed over: its place in the order still counts, so the rest of the
        /// wall goes up on time.
        /// </summary>
        public void SetProgress(float progress, Action<Vector3> placed)
        {
            Cache();

            int target = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(progress) * Count), 0, Count);

            while (shown < target)
            {
                if (Stands(shown))
                {
                    Raise(shown, animate: true);
                    if (pieces[shown] != null) placed?.Invoke(FootOf(shown));
                }

                shown++;
            }
        }

        /// <summary>Everything up. Instantly, with no dust, when restoring land claimed before.</summary>
        public void Complete(bool animate, Action<Vector3> placed)
        {
            if (animate)
            {
                SetProgress(1f, placed);
                return;
            }

            Cache();
            for (int i = 0; i < Count; i++)
            {
                if (Stands(i)) Raise(i, animate: false);
                else Lower(i);
            }

            shown = Count;
        }

        /// <summary>
        /// Takes down whatever is up facing land that has since been claimed. Called when any
        /// piece of land changes hands.
        /// </summary>
        public void Refresh()
        {
            if (pieces == null) return;

            Cache();
            for (int i = 0; i < shown && i < Count; i++)
            {
                if (!Stands(i) && pieces[i] != null && pieces[i].gameObject.activeSelf) Lower(i);
            }
        }

        bool Stands(int index)
        {
            ExpansionAreaDefinition far = Beyond(index);
            return far == null || isClaimed == null || !isClaimed(far.id);
        }

        void Raise(int index, bool animate)
        {
            Transform piece = pieces[index];
            if (piece == null) return;

            piece.DOKill();
            piece.gameObject.SetActive(true);

            Vector3 position = basePositions[index];
            Vector3 scale = baseScales[index];

            if (!animate)
            {
                piece.localPosition = position;
                piece.localScale = scale;
                return;
            }

            // Out of the ground: flattened and sunk by half its height, then up to full size.
            piece.localScale = new Vector3(scale.x, scale.y * 0.02f, scale.z);
            piece.localPosition = position + Vector3.down * (scale.y * 0.5f);

            piece.DOScale(scale, riseSeconds).SetEase(Ease.OutBack);
            piece.DOLocalMove(position, riseSeconds).SetEase(Ease.OutQuad);
        }

        void Lower(int index)
        {
            Transform piece = pieces[index];
            if (piece == null) return;

            piece.DOKill();
            piece.localPosition = basePositions[index];
            piece.localScale = baseScales[index];
            piece.gameObject.SetActive(false);
        }

        Vector3 FootOf(int index)
        {
            Transform piece = pieces[index];
            Vector3 centre = piece.parent != null ? piece.parent.TransformPoint(basePositions[index]) : basePositions[index];
            return new Vector3(centre.x, centre.y - baseScales[index].y * 0.5f, centre.z);
        }

        /// <summary>Records each section's authored pose once, before anything animates it.</summary>
        void Cache()
        {
            if (basePositions != null && basePositions.Length == Count) return;

            basePositions = new Vector3[Count];
            baseScales = new Vector3[Count];

            for (int i = 0; i < Count; i++)
            {
                if (pieces[i] == null) continue;

                basePositions[i] = pieces[i].localPosition;
                baseScales[i] = pieces[i].localScale;
            }
        }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(Transform[] sections) => Configure(sections, null);

        /// <summary>Editor wiring, for the setup tool: the sections and the land beyond each.</summary>
        public void Configure(Transform[] sections, ExpansionAreaDefinition[] landBeyond)
        {
            pieces = sections;
            beyond = landBeyond;
        }
    }
}
