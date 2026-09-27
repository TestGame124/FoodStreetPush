using LastOrders.Expansion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Popups
{
    /// <summary>One line of a requirements list: what is needed, how much the player has, and
    /// a mark that goes green once it is covered.</summary>
    public sealed class RequirementRowView : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text amount;
        [SerializeField] Image status;

        [SerializeField] Color metColour = new Color(0.3f, 0.66f, 0.28f);
        [SerializeField] Color shortColour = new Color(0.85f, 0.3f, 0.25f);

        public void Show(ExpansionRequirement requirement)
        {
            gameObject.SetActive(true);

            Color colour = requirement.Met ? metColour : shortColour;

            if (label != null) label.text = requirement.Label;

            if (amount != null)
            {
                amount.text = $"{requirement.Have} / {requirement.Need}";
                amount.color = colour;
            }

            if (status != null) status.color = colour;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
