using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Popups
{
    /// <summary>
    /// One car's bag in the drive-through popup: which car it is, how long it will wait, a slot
    /// for each dish it asked for, what it pays, and the button that hands it out of the window.
    /// </summary>
    public sealed class TakeawayBagView : MonoBehaviour
    {
        [SerializeField] Button select;
        [SerializeField] Image card;

        [Tooltip("The frame round the bag a dish from the pass goes into.")]
        [SerializeField] GameObject selectedFrame;

        [SerializeField] TMP_Text heading;
        [SerializeField] TMP_Text bill;
        [SerializeField] Image patience;
        [SerializeField] DishTileView[] slots;
        [SerializeField] Button handOver;
        [SerializeField] Image handOverFace;
        [SerializeField] TMP_Text handOverLabel;

        public Button Select => select;
        public Button HandOverButton => handOver;
        public int SlotCount => slots != null ? slots.Length : 0;
        public DishTileView Slot(int index) => slots[index];

        public void Show(string title, string pays, float patienceLeft, Color patienceColour, bool selected, Color cardColour)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            if (heading != null) heading.text = title;
            if (bill != null) bill.text = pays;
            if (patience != null)
            {
                patience.fillAmount = Mathf.Clamp01(patienceLeft);
                patience.color = patienceColour;
            }

            if (selectedFrame != null) selectedFrame.SetActive(selected);
            if (card != null) card.color = cardColour;
        }

        public void ShowHandOver(bool visible, bool ready, string label, Color colour)
        {
            if (handOver == null) return;

            if (handOver.gameObject.activeSelf != visible) handOver.gameObject.SetActive(visible);
            handOver.interactable = ready;
            if (handOverFace != null) handOverFace.color = colour;
            if (handOverLabel != null) handOverLabel.text = label;
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
