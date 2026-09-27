using DG.Tweening;
using LastOrders.Core;
using UnityEngine;

namespace LastOrders.UI.World
{
    /// <summary>
    /// The base of every pooled world-space UI element: follow something, face the camera,
    /// pop in, and come back to the pool clean.
    ///
    /// The root is a world-space Canvas and code only ever sets its position and rotation.
    /// Everything that animates - the pop-in, a fade, a rise - happens on the <see cref="body"/>
    /// child or through an offset, because <see cref="PoolIntegrity"/> compares the root's
    /// scale against the prefab's on every release and a killed scale tween on the root is
    /// exactly what it exists to catch. The root's rotation is overwritten every frame, so it
    /// is exempt from that check.
    ///
    /// Faces the camera by matching its rotation rather than looking at its position. Under a
    /// pitched perspective camera that keeps every element parallel to the screen, so text is
    /// never skewed, and it is the orientation a world-space canvas reads correctly from.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public abstract class WorldUiElement : MonoBehaviour, IPoolable, IPoolRotationExempt
    {
        [Tooltip("The child that animates. The root's scale is never touched.")]
        [SerializeField] protected RectTransform body;

        [Tooltip("Seconds of the pop-in. Zero shows it at full size immediately.")]
        [SerializeField] float popInSeconds = 0.18f;

        WorldUiService owner;
        Transform anchor;
        Vector3 anchorPosition;
        Vector3 offset;
        Camera view;
        Canvas canvas;

        /// <summary>Added to the placement each frame. For elements that drift, such as rising text.</summary>
        protected Vector3 extraOffset;

        public Transform Anchor => anchor;
        public bool IsLive => owner != null;

        protected virtual void Awake()
        {
            canvas = GetComponent<Canvas>();
        }

        /// <summary>
        /// Attaches the element to what it is about and puts it there immediately. Immediately
        /// matters: the pool activates an instance while it is still parked at the pool root,
        /// and waiting for LateUpdate would show it there for a frame.
        /// </summary>
        internal void Bind(WorldUiService service, Transform target, Vector3 position, Vector3 localOffset,
                           Camera camera)
        {
            owner = service;
            anchor = target;
            anchorPosition = target != null ? target.position : position;
            offset = localOffset;
            view = camera;

            if (canvas == null) canvas = GetComponent<Canvas>();
            canvas.worldCamera = camera;

            Place();
            PopIn();
        }

        /// <summary>Returns the element to its pool. Safe to call more than once.</summary>
        public void Release()
        {
            if (owner != null) owner.Release(this);
        }

        protected virtual void LateUpdate()
        {
            if (owner == null) return;
            Place();
        }

        void Place()
        {
            // An anchor that has gone - destroyed, or pooled away - leaves the element where it
            // last was rather than snapping to the origin. Its owner releases it.
            if (anchor != null) anchorPosition = anchor.position;

            transform.position = anchorPosition + offset + extraOffset;

            if (view == null) view = Camera.main;
            if (view != null) transform.rotation = view.transform.rotation;
        }

        void PopIn()
        {
            if (body == null) return;

            DOTween.Kill(body);

            if (popInSeconds <= 0f)
            {
                body.localScale = Vector3.one;
                return;
            }

            body.localScale = Vector3.one * 0.4f;
            body.DOScale(1f, popInSeconds).SetEase(Ease.OutBack).SetTarget(body);
        }

        public virtual void OnTakenFromPool() { }

        /// <summary>
        /// Leaves the element indistinguishable from a fresh instance: tweens killed, body at
        /// full size, nothing anchored, nobody listening. Subclasses reset their own parts and
        /// then call this.
        /// </summary>
        public virtual void OnReturnedToPool()
        {
            DOTween.Kill(this);

            if (body != null)
            {
                DOTween.Kill(body);
                body.localScale = Vector3.one;
                body.anchoredPosition3D = Vector3.zero;
            }

            owner = null;
            anchor = null;
            view = null;
            extraOffset = Vector3.zero;
        }
    }
}
