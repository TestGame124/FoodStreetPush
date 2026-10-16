using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// A notice that goes away by itself: switched on by the game ("not enough ingredients"), it
    /// stays a moment, fades and switches off - no OK button to hunt for, nothing blocking the
    /// screen. A tap on it dismisses it early.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UiAutoHide : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] float seconds = 2.2f;
        [SerializeField] float fade = 0.25f;

        CanvasGroup group;
        float shownAt;
        bool hiding;

        void Awake() => group = GetComponent<CanvasGroup>();

        void OnEnable()
        {
            DOTween.Kill(this);
            group.alpha = 1f;
            shownAt = Time.unscaledTime;
            hiding = false;
        }

        void OnDisable() => DOTween.Kill(this);

        void Update()
        {
            if (!hiding && Time.unscaledTime - shownAt >= seconds) Hide();
        }

        public void OnPointerClick(PointerEventData eventData) => Hide();

        public void Hide()
        {
            if (hiding) return;
            hiding = true;
            group.DOFade(0f, fade).SetUpdate(true).SetTarget(this).OnComplete(() => gameObject.SetActive(false));
        }
    }
}
