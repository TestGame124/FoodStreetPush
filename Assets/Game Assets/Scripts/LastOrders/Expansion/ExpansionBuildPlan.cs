using System;
using DG.Tweening;
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
    /// </summary>
    public sealed class ExpansionBuildPlan : MonoBehaviour
    {
        [Tooltip("The sections, in build order. Each is shown when the build reaches it.")]
        [SerializeField] Transform[] pieces;

        [Tooltip("Seconds a section takes to rise into place.")]
        [SerializeField] float riseSeconds = 0.45f;

        Vector3[] basePositions;
        Vector3[] baseScales;
        int shown;

        public int Count => pieces != null ? pieces.Length : 0;

        /// <summary>Takes every section down. The state before an expansion starts.</summary>
        public void HideAll()
        {
            Cache();

            for (int i = 0; i < Count; i++)
            {
                Transform piece = pieces[i];
                if (piece == null) continue;

                piece.DOKill();
                piece.localPosition = basePositions[i];
                piece.localScale = baseScales[i];
                piece.gameObject.SetActive(false);
            }

            shown = 0;
        }

        /// <summary>
        /// Raises every section up to <paramref name="progress"/> (0 to 1) that is not up yet,
        /// calling <paramref name="placed"/> with the foot of each as it goes up.
        /// </summary>
        public void SetProgress(float progress, Action<Vector3> placed)
        {
            Cache();

            int target = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(progress) * Count), 0, Count);

            while (shown < target)
            {
                Raise(shown, animate: true);
                if (pieces[shown] != null) placed?.Invoke(FootOf(shown));
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
            for (int i = 0; i < Count; i++) Raise(i, animate: false);
            shown = Count;
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
        public void Configure(Transform[] sections) => pieces = sections;
    }
}
