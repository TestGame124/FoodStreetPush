using DG.Tweening;
using LastOrders.UI.Hud;
using UnityEngine;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// Pops a list's items in one after another whenever the list is shown - a shop shelf when its
    /// tab is picked, the seeds when the tray opens, the rewards on a level-up - and any item added
    /// to it later. An item with a <see cref="HudMotion"/> springs in through it (the motion owns
    /// its scale); anything else scales up with a little overshoot.
    /// </summary>
    public sealed class UiStaggerIn : MonoBehaviour
    {
        [SerializeField] float step = 0.045f;
        [SerializeField] float maxDelay = 0.45f;
        [SerializeField] [Range(0.1f, 1f)] float from = 0.5f;

        int seen;

        void OnEnable()
        {
            seen = 0;
            Play(0);
        }

        void OnDisable()
        {
            DOTween.Kill(this);
            foreach (Transform child in transform)
            {
                var group = child.GetComponent<CanvasGroup>();
                if (group != null) group.alpha = 1f;
            }
        }

        void LateUpdate()
        {
            int count = transform.childCount;
            if (count < seen) seen = count;
            if (count > seen) Play(seen);
        }

        void Play(int start)
        {
            int count = transform.childCount;
            int order = 0;
            for (int i = start; i < count; i++)
            {
                Transform child = transform.GetChild(i);
                if (!child.gameObject.activeSelf) continue;

                var group = child.GetComponent<CanvasGroup>();
                if (group == null) group = child.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;

                var motion = child.GetComponent<HudMotion>();
                float delay = Mathf.Min(order++ * step, maxDelay);
                DOVirtual.DelayedCall(delay, () => Show(child, group, motion), true).SetTarget(this);
            }

            seen = count;
        }

        void Show(Transform child, CanvasGroup group, HudMotion motion)
        {
            if (child == null) return;

            group.DOFade(1f, 0.16f).SetUpdate(true);
            if (motion != null && motion.isActiveAndEnabled)
            {
                motion.Appear(from);
                return;
            }

            DOTween.Kill(child);
            child.localScale = Vector3.one * from;
            child.DOScale(1f, 0.34f).SetEase(Ease.OutBack, 2.2f).SetUpdate(true).SetTarget(child);
        }
    }
}
