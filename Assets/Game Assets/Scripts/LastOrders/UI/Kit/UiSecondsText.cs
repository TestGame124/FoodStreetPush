using System.Globalization;
using TMPro;
using UnityEngine;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// Turns a bare count of seconds written into a label ("74") into a time a player reads at a
    /// glance ("1:14", "45s"). The kitchen writes its cook timers as plain numbers; this rewrites
    /// each one as it lands and leaves anything that is not a number alone.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class UiSecondsText : MonoBehaviour
    {
        TMP_Text text;
        string written;

        void Awake() => text = GetComponent<TMP_Text>();

        void LateUpdate()
        {
            string value = text.text;
            if (value == written || string.IsNullOrEmpty(value)) return;
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds)) return;

            written = Format(seconds);
            text.text = written;
        }

        public static string Format(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return s < 60 ? $"{s}s" : $"{s / 60}:{s % 60:00}";
        }
    }
}
