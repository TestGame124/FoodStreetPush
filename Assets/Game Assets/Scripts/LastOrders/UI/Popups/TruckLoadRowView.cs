using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Popups
{
    /// <summary>
    /// One dish in the food truck's loading list: its picture and name, how many are on the pass,
    /// how many are aboard with a minus and a plus, and a star when the place picked on the map
    /// pays extra for it.
    /// </summary>
    public sealed class TruckLoadRowView : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] TMP_Text initial;
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text onPass;
        [SerializeField] TMP_Text aboard;
        [SerializeField] Button less;
        [SerializeField] Button more;
        [SerializeField] GameObject favourite;
        [SerializeField] TMP_Text price;

        public Button Less => less;
        public Button More => more;

        public void Show(Sprite picture, string name, int spare, int loaded, bool canLoad, bool canUnload, bool isFavourite, int priceEach)
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

            if (label != null) label.text = name;
            if (onPass != null) onPass.text = spare == 1 ? "1 on the pass" : $"{spare} on the pass";
            if (aboard != null) aboard.text = loaded.ToString();
            if (less != null) less.interactable = canUnload;
            if (more != null) more.interactable = canLoad;
            if (favourite != null) favourite.SetActive(isFavourite);
            if (price != null) price.text = priceEach > 0 ? $"{priceEach}c" : "";
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
