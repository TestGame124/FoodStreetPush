using TMPro;
using UnityEngine;

namespace LastOrders.UI.Hud
{
    /// <summary>
    /// A number that counts to its new value instead of jumping, and kicks when it goes up - so a
    /// payment is something the player sees arrive, not a digit that changed while they looked away.
    /// </summary>
    public sealed class HudCounter : MonoBehaviour
    {
        [SerializeField] TMP_Text text;
        [SerializeField] HudMotion motion;
        [SerializeField] float seconds = 0.5f;

        [Tooltip("Shortens large numbers: 12,400 stays as it is, 125,000 becomes 125K.")]
        [SerializeField] bool compact = true;

        [Tooltip("Written before the number, e.g. '+' on a reward row.")]
        [SerializeField] string prefix = "";

        float shown;
        float from;
        int target;
        float t = 1f;
        bool drawn;

        public int Value => target;

        public void Configure(TMP_Text label, HudMotion kick, string before = "")
        {
            text = label;
            motion = kick;
            prefix = before;
        }

        public void Set(int value, bool animate = true)
        {
            if (!animate || !isActiveAndEnabled || !drawn)
            {
                shown = from = target = value;
                t = 1f;
                drawn = true;
                Draw();
                return;
            }

            if (value == target) return;

            if (motion != null) motion.Punch(value > target ? 0.16f : -0.07f);

            from = shown;
            target = value;
            t = 0f;
        }

        void Update()
        {
            if (t >= 1f) return;

            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds));
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            shown = Mathf.Lerp(from, target, eased);
            Draw();
        }

        void Draw()
        {
            if (text == null) return;
            int value = t >= 1f ? target : Mathf.RoundToInt(shown);
            text.text = prefix + (compact ? Compact(value) : value.ToString("N0"));
        }

        /// <summary>"950", "12,400", "125K", "1.2M".</summary>
        public static string Compact(long value)
        {
            long a = value < 0 ? -value : value;
            string sign = value < 0 ? "-" : "";

            if (a < 100_000) return sign + a.ToString("N0");
            if (a < 1_000_000) return sign + (a / 1000) + "K";
            if (a < 100_000_000) return sign + (a / 1_000_000f).ToString("0.#") + "M";
            return sign + (a / 1_000_000) + "M";
        }
    }
}
