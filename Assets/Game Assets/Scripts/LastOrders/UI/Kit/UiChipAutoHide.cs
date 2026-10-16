using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// Hides a chip's background while its text is blank, so a count that the game leaves empty
    /// ("1" is never written), or switches off, does not leave a little empty pill on the card.
    /// </summary>
    public sealed class UiChipAutoHide : MonoBehaviour
    {
        [SerializeField] TMP_Text text;
        [SerializeField] Graphic background;

        void LateUpdate()
        {
            if (text == null || background == null) return;

            bool show = text.isActiveAndEnabled && !string.IsNullOrWhiteSpace(text.text) && text.alpha > 0.05f;
            if (background.enabled != show) background.enabled = show;
        }
    }
}
