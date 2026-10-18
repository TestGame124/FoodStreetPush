using System.Collections.Generic;
using DG.Tweening;
using LastOrders.Audio;
using UnityEngine;
using UnityEngine.Events;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// How every screen window opens and closes: the dim behind it fades in, the window pops up
    /// from a little smaller, and closing plays the same thing backwards before the window switches
    /// off. The phone's back button (Escape) closes the window on top.
    ///
    /// Sits on the object the game switches on and off, so a window opened by the older UI code
    /// (<c>SetActive(true)</c>) animates all the same. What closing actually does - a screen's own
    /// close method, or just switching off - is the <see cref="closed"/> event, so the close button,
    /// a tap on the dim and the back button all go through one path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiWindow : MonoBehaviour
    {
        [Tooltip("The part that pops in. The dim behind it only fades.")]
        [SerializeField] RectTransform panel;

        [Tooltip("The full-screen dim. Optional.")]
        [SerializeField] CanvasGroup scrim;

        [Tooltip("Runs once the window has animated out, before it switches off: the screen's own close " +
                 "method when it has one. The window switches itself off afterwards either way.")]
        [SerializeField] UnityEvent closed = new UnityEvent();

        [SerializeField] bool closeOnBack = true;

        [SerializeField] [Range(0.5f, 1f)] float popFrom = 0.88f;

        static readonly List<UiWindow> open = new List<UiWindow>();

        bool closing;

        /// <summary>True while any window is up.</summary>
        public static bool AnyOpen => open.Count > 0;

        public UnityEvent Closed => closed;

        void OnEnable()
        {
            closing = false;
            open.Remove(this);
            open.Add(this);
            SoundController.Play(SoundId.UiPopupOpen);

            DOTween.Kill(this);
            if (panel != null)
            {
                panel.localScale = Vector3.one * popFrom;
                panel.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 1.7f).SetUpdate(true).SetTarget(this);
            }

            if (scrim != null)
            {
                scrim.alpha = 0f;
                scrim.DOFade(1f, 0.18f).SetUpdate(true).SetTarget(this);
            }
        }

        void OnDisable()
        {
            open.Remove(this);
            DOTween.Kill(this);
            if (panel != null) panel.localScale = Vector3.one;
            if (scrim != null) scrim.alpha = 1f;
            closing = false;
        }

        void Update()
        {
            if (!closeOnBack || closing || open.Count == 0 || open[open.Count - 1] != this) return;
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        /// <summary>Animates the window out, runs <see cref="closed"/>, and switches it off.</summary>
        public void Close()
        {
            if (closing || !isActiveAndEnabled) return;
            closing = true;
            SoundController.Play(SoundId.UiPopupClose);

            DOTween.Kill(this);
            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            if (panel != null) sequence.Join(panel.DOScale(popFrom + (1f - popFrom) * 0.3f, 0.12f).SetEase(Ease.InQuad));
            if (scrim != null) sequence.Join(scrim.DOFade(0f, 0.14f));
            sequence.OnComplete(Finish);
        }

        void Finish()
        {
            closed.Invoke();
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
