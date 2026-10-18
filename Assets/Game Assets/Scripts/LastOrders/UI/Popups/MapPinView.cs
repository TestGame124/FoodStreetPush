using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Popups
{
    /// <summary>A place on the food truck's little map: a pin to tap, its name, and how long the trip is.</summary>
    public sealed class MapPinView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image disc;
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text trip;

        [Tooltip("The ring round the pin that is picked.")]
        [SerializeField] GameObject picked;

        public Button Button => button;

        public RectTransform Rect => (RectTransform)transform;

        public void Show(Vector2 at, string name, string tripText, bool isPicked, Color colour)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            var rect = Rect;
            rect.anchorMin = rect.anchorMax = at;
            rect.anchoredPosition = Vector2.zero;

            if (label != null) label.text = name;
            if (trip != null) trip.text = tripText;
            if (picked != null) picked.SetActive(isPicked);
            if (disc != null) disc.color = colour;
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
