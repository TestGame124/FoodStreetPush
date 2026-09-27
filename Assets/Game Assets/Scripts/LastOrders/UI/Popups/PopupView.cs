using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Popups
{
    /// <summary>
    /// The base of every screen popup: a dimmed backdrop, a panel that pops in, and the two
    /// ways out - the close button and a tap on the backdrop.
    ///
    /// The backdrop covers the whole screen and takes raycasts, which is what makes the popup
    /// modal: Mouse3D and the camera rig both stand down while the pointer is over UI, so the
    /// world cannot be tapped or panned behind an open popup.
    /// </summary>
    public abstract class PopupView : MonoBehaviour
    {
        [Tooltip("The part that pops in. The backdrop stays put.")]
        [SerializeField] RectTransform panel;

        [Tooltip("Fades the whole popup in.")]
        [SerializeField] CanvasGroup group;

        [Tooltip("The full-screen dim. Tapping it closes the popup.")]
        [SerializeField] Button backdrop;

        [SerializeField] Button closeButton;

        PopupService owner;

        public bool IsOpen => owner != null;

        protected virtual void Awake()
        {
            if (backdrop != null) backdrop.onClick.AddListener(Close);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        internal void Open(PopupService service)
        {
            owner = service;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            DOTween.Kill(this);

            if (panel != null)
            {
                panel.localScale = Vector3.one * 0.85f;
                panel.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetUpdate(true).SetTarget(this);
            }

            if (group != null)
            {
                group.alpha = 0f;
                DOTween.To(() => group.alpha, a => group.alpha = a, 1f, 0.15f).SetUpdate(true).SetTarget(this);
            }

            OnOpened();
        }

        /// <summary>Closes the popup. Safe to call when it is already closed.</summary>
        public void Close()
        {
            if (owner != null) owner.Hide(this);
        }

        internal void Closed()
        {
            DOTween.Kill(this);

            if (panel != null) panel.localScale = Vector3.one;
            if (group != null) group.alpha = 1f;

            OnClosed();

            owner = null;
            gameObject.SetActive(false);
        }

        protected virtual void OnOpened() { }

        /// <summary>Drop every callback and reference here, so a reused popup carries nothing over.</summary>
        protected virtual void OnClosed() { }
    }
}
