using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Popups
{
    /// <summary>
    /// Opens screen popups, one at a time, each from its own prefab.
    ///
    /// Each prefab is instantiated once, the first time it is asked for, and the same
    /// instance is shown and hidden from then on - a popup is opened far too rarely for a
    /// real pool to pay for itself, but it should never be instantiated twice either.
    ///
    /// The popups sit on a screen-space canvas of their own, built on first use, sorted
    /// above the HUD and scaled the way the HUD is, so a popup authored at the reference
    /// resolution looks the same on every screen.
    /// </summary>
    public sealed class PopupService
    {
        /// <summary>Above the HUD canvases, which sit at 100 and below.</summary>
        const int SortingOrder = 500;

        static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        readonly Transform root;
        readonly Dictionary<PopupView, PopupView> instances = new Dictionary<PopupView, PopupView>();

        RectTransform canvas;
        PopupView current;

        public PopupService(Transform root)
        {
            this.root = root;
        }

        public bool IsOpen => current != null;
        public PopupView Current => current;

        /// <summary>Opens a popup, closing whichever one was open.</summary>
        public T Show<T>(T prefab) where T : PopupView
        {
            if (prefab == null)
            {
                Debug.LogError("[Popups] Asked to show a popup with no prefab. Check the UI catalog.");
                return null;
            }

            Hide();

            T instance = InstanceOf(prefab);
            current = instance;
            instance.Open(this);
            return instance;
        }

        /// <summary>Closes <paramref name="view"/>, or whichever popup is open when null.</summary>
        public void Hide(PopupView view = null)
        {
            if (current == null) return;
            if (view != null && view != current) return;

            PopupView closing = current;
            current = null;
            closing.Closed();
        }

        T InstanceOf<T>(T prefab) where T : PopupView
        {
            if (instances.TryGetValue(prefab, out PopupView existing) && existing != null) return (T)existing;

            T created = Object.Instantiate(prefab, Canvas(), false);
            created.gameObject.SetActive(false);
            instances[prefab] = created;
            return created;
        }

        RectTransform Canvas()
        {
            if (canvas != null) return canvas;

            var host = new GameObject("PopupCanvas", typeof(RectTransform));
            host.transform.SetParent(root, false);

            var c = host.AddComponent<UnityEngine.Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = SortingOrder;

            var scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 1f;

            host.AddComponent<GraphicRaycaster>();

            canvas = (RectTransform)host.transform;
            return canvas;
        }
    }
}
