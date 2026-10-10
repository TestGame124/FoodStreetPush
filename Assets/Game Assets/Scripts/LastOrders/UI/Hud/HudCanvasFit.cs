using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Hud
{
    /// <summary>
    /// Scales the HUD by height on wide screens and by width on narrow ones. Every cluster hangs off
    /// a corner, so matching on height keeps them the same size from 16:10 to 21:9 - but on a 3:2
    /// or 4:3 screen the top row would run out of width, so there the canvas matches on width instead.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class HudCanvasFit : MonoBehaviour
    {
        [Tooltip("At or above this width-to-height ratio the HUD scales by height.")]
        [SerializeField] float wideAspect = 1.6f;

        CanvasScaler scaler;

        void OnEnable() => scaler = GetComponent<CanvasScaler>();

        void Update()
        {
            if (scaler == null || Screen.height <= 0) return;

            float aspect = (float)Screen.width / Screen.height;
            float match = aspect >= wideAspect ? 1f : 0f;
            if (!Mathf.Approximately(scaler.matchWidthOrHeight, match)) scaler.matchWidthOrHeight = match;
        }
    }
}
