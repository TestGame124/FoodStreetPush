using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.World
{
    /// <summary>
    /// A button over something in the world that says what tapping it will do - the hammer
    /// over debris.
    ///
    /// The only world UI element with a GraphicRaycaster, and that is what keeps its taps its
    /// own: a press that starts on it is "over UI", so Mouse3D leaves the release alone and the
    /// pile behind the button never sees it.
    ///
    /// A transient prompt - the hammer - closes itself when the player taps anywhere else in
    /// the world, read from Mouse3D's own notion of a tap so that panning the camera does not
    /// count; the tap that opened it arrives in the same frame and is ignored. A standing
    /// button - the expand icon over cleared land - stays until its owner takes it down.
    /// </summary>
    public sealed class ActionPromptView : WorldUiElement
    {
        [SerializeField] Button button;

        [Tooltip("Shows an icon sprite: the definition's own, or the one authored here.")]
        [SerializeField] Image icon;

        [Tooltip("Shown when there is no sprite at all. The generated prefab's icon, drawn from shapes.")]
        [SerializeField] GameObject placeholderIcon;

        [Tooltip("Close when the player taps somewhere else in the world. Off for standing buttons.")]
        [SerializeField] bool dismissOnWorldTap = true;

        [Tooltip("Go away once pressed. Off for a button that opens something and stays.")]
        [SerializeField] bool releaseOnPress = true;

        [Tooltip("Optional. Breathes gently while shown, to draw the eye to a standing button.")]
        [SerializeField] RectTransform pulse;

        [SerializeField] float pulseScale = 1.08f;

        Sprite authoredIcon;
        Action onConfirm;
        int shownFrame = -1;

        protected override void Awake()
        {
            base.Awake();

            authoredIcon = icon != null ? icon.sprite : null;

            if (button != null) button.onClick.AddListener(HandleClick);
            else Debug.LogError($"[WorldUI] '{name}' has no Button assigned, so it cannot be pressed.", this);
        }

        /// <summary>Sets the icon and what pressing does. A null sprite keeps the prefab's own.</summary>
        public void Configure(Sprite iconOverride, Action confirm)
        {
            onConfirm = confirm;
            ShowIcon(iconOverride != null ? iconOverride : authoredIcon);
        }

        public override void OnTakenFromPool()
        {
            shownFrame = Time.frameCount;
            if (dismissOnWorldTap) Mouse3D.WorldTapResolved += HandleWorldTap;

            if (pulse != null)
            {
                pulse.localScale = Vector3.one;
                pulse.DOScale(pulseScale, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetTarget(this);
            }
        }

        public override void OnReturnedToPool()
        {
            Mouse3D.WorldTapResolved -= HandleWorldTap;
            onConfirm = null;
            shownFrame = -1;
            ShowIcon(authoredIcon);

            // Base kills every tween targeting this, the pulse included; the scale is ours to reset.
            base.OnReturnedToPool();
            if (pulse != null) pulse.localScale = Vector3.one;
        }

        void HandleWorldTap(bool handled)
        {
            if (Time.frameCount == shownFrame) return;
            Release();
        }

        void HandleClick()
        {
            if (!releaseOnPress)
            {
                onConfirm?.Invoke();
                return;
            }

            // Released before acting, so whatever the action shows next - a timer over the same
            // spot - is not competing with a button that is about to vanish.
            Action confirm = onConfirm;
            onConfirm = null;

            Release();
            confirm?.Invoke();
        }

        void ShowIcon(Sprite sprite)
        {
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (placeholderIcon != null) placeholderIcon.SetActive(sprite == null);
        }
    }
}
