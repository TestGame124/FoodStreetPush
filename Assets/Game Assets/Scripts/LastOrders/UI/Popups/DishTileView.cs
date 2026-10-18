using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Popups
{
    /// <summary>
    /// A dish as a button: its picture, a count, a name under it, and a tick when it is done - a
    /// plate on the pass to pack, or a slot in a takeaway bag. The popup that owns it decides what
    /// pressing it means.
    /// </summary>
    public sealed class DishTileView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image icon;

        [Tooltip("Shown when there is no picture for the dish.")]
        [SerializeField] TMP_Text initial;

        [SerializeField] TMP_Text count;
        [SerializeField] GameObject countChip;
        [SerializeField] TMP_Text label;

        [Tooltip("The tick: packed, or chosen.")]
        [SerializeField] GameObject mark;

        [Tooltip("A ring round the tile: the one to press next.")]
        [SerializeField] GameObject glow;

        [Tooltip("Faded when the tile is waiting on something that is not there yet.")]
        [SerializeField] CanvasGroup group;

        public Button Button => button;

        public void Show(Sprite picture, string name, string number, bool interactable, bool done, bool highlighted, bool faded)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            if (icon != null)
            {
                icon.sprite = picture;
                icon.enabled = picture != null;
            }

            if (initial != null)
            {
                initial.gameObject.SetActive(picture == null);
                initial.text = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
            }

            if (count != null) count.text = number ?? "";
            if (countChip != null) countChip.SetActive(!string.IsNullOrEmpty(number));
            if (label != null) label.text = name ?? "";
            if (mark != null) mark.SetActive(done);
            if (glow != null) glow.SetActive(highlighted);
            if (group != null) group.alpha = faded ? 0.45f : 1f;
            if (button != null) button.interactable = interactable;
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
