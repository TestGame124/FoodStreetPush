using UnityEngine;

namespace LastOrders.UI.Hud
{
    /// <summary>
    /// Keeps its rect inside the screen's safe area, so nothing on the HUD sits under a notch, a
    /// rounded corner or a home indicator.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class HudSafeArea : MonoBehaviour
    {
        Rect applied;
        Vector2Int screen;

        void OnEnable() => Apply(true);

        void Update() => Apply(false);

        void Apply(bool force)
        {
            Rect safe = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (!force && safe == applied && size == screen) return;
            if (size.x <= 0 || size.y <= 0) return;

            applied = safe;
            screen = size;

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(safe.xMin / size.x, safe.yMin / size.y);
            rect.anchorMax = new Vector2(safe.xMax / size.x, safe.yMax / size.y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
