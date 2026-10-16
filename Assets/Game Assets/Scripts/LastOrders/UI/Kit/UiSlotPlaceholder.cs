using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// Shows a placeholder ("Empty burner") in a slot while its picture is empty or faded out, and
    /// the slot's extras (its timer chip) only while something is in it. The kitchen fades its
    /// cooking slots in and out rather than switching them, so this reads the picture's own fade.
    /// </summary>
    public sealed class UiSlotPlaceholder : MonoBehaviour
    {
        [SerializeField] Image picture;
        [SerializeField] GameObject placeholder;
        [SerializeField] GameObject[] filledOnly = new GameObject[0];

        bool? shownEmpty;

        void LateUpdate()
        {
            if (picture == null) return;

            bool empty = picture.sprite == null || !picture.isActiveAndEnabled || picture.canvasRenderer.GetAlpha() < 0.1f;
            if (shownEmpty == empty) return;
            shownEmpty = empty;

            if (placeholder != null) placeholder.SetActive(empty);
            foreach (GameObject go in filledOnly)
                if (go != null) go.SetActive(!empty);
        }
    }
}
