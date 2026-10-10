using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Hud
{
    /// <summary>One toast: dark glass, a coloured disc with a glyph, a line of text.</summary>
    public sealed class HudToast : MonoBehaviour
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Image disc;
        public Image glyph;
        public TMP_Text text;

        [System.NonSerialized] public float age;
        [System.NonSerialized] public float life;
        [System.NonSerialized] public float y;
        [System.NonSerialized] public int slot;
        [System.NonSerialized] public bool leaving;
        [System.NonSerialized] public string message;
    }
}
