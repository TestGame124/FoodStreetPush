using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// Shows what an older screen script writes into a legacy <see cref="Text"/> in a styled
    /// TextMeshPro label instead. The old scripts keep writing to the field they always had - the
    /// station's name, an order's reward, a recipe's time - and the redesigned screen keeps that
    /// <see cref="Text"/> hidden and shows its words in the new type.
    /// </summary>
    public sealed class UiTextMirror : MonoBehaviour
    {
        [SerializeField] Text source;
        [SerializeField] TMP_Text target;

        [Tooltip("How the copied words are shown; {0} is the copy.")]
        [SerializeField] string format = "{0}";

        [Tooltip("Copy only the first whole number in the text - the 3 out of 'Level Up To 3'.")]
        [SerializeField] bool numberOnly;

        string last;

        public void Configure(Text from, TMP_Text to, string pattern = "{0}", bool number = false)
        {
            source = from;
            target = to;
            format = pattern;
            numberOnly = number;
        }

        void OnEnable()
        {
            last = null;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (source == null || target == null) return;

            string text = source.text;
            if (text == last) return;
            last = text;

            if (numberOnly)
            {
                Match number = Regex.Match(text ?? string.Empty, @"\d+");
                text = number.Success ? number.Value : string.Empty;
            }

            target.text = string.Format(format, text);
        }
    }
}
