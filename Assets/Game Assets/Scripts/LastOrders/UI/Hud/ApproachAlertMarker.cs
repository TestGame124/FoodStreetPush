using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Hud
{
    /// <summary>
    /// One approach alert's parts: the round badge with its "!", the beak that swings round the badge
    /// to point at the zombies, the glow behind them and the tag saying how many and from which side.
    /// <see cref="ApproachAlerts"/> places it and says what it shows; this only holds the references.
    /// </summary>
    public sealed class ApproachAlertMarker : MonoBehaviour
    {
        public CanvasGroup group;

        [Tooltip("Everything, scaled down while the alert is pinned over zombies on screen.")]
        public RectTransform body;

        [Tooltip("Punched when a group sets off up this side.")]
        public HudMotion motion;

        public Image glow;
        public RectTransform pointer;
        public Image pointerImage;
        public Image badge;
        public Image glyph;
        public Button button;

        [Tooltip("Kept on the far side of the badge from the beak.")]
        public RectTransform tag;
        public TMP_Text label;
    }
}
