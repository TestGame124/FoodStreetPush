using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.World
{
    /// <summary>
    /// The timer over work in progress: a bar that fills and the seconds left.
    ///
    /// Shown from the moment the work is asked for, not from the moment it starts. While the
    /// worker is still walking over, it sits empty and says so; a tap that produces nothing on
    /// screen until somebody arrives reads as a tap that did not register.
    ///
    /// Takes no raycasts. It floats over the thing being worked on, and a timer that swallowed
    /// the taps meant for what is beneath it would be the one piece of UI the player has to
    /// learn to tap around.
    /// </summary>
    public sealed class WorkProgressView : WorldUiElement
    {
        [Tooltip("An Image set to Filled. Its fill amount is the progress.")]
        [SerializeField] Image fill;

        [SerializeField] TMP_Text label;

        [SerializeField] Color workingColour = new Color(0.45f, 0.85f, 0.35f);
        [SerializeField] Color waitingColour = new Color(0.85f, 0.85f, 0.85f);

        [Tooltip("Shown while the worker is on the way.")]
        [SerializeField] string waitingText = "...";

        /// <summary>Empty bar: the work is asked for and the worker has not arrived yet.</summary>
        public void SetWaiting()
        {
            if (fill != null)
            {
                fill.fillAmount = 0f;
                fill.color = waitingColour;
            }

            if (label != null) label.text = waitingText;
        }

        public void SetProgress(float normalized, float secondsLeft)
        {
            if (fill != null)
            {
                fill.fillAmount = Mathf.Clamp01(normalized);
                fill.color = workingColour;
            }

            if (label != null) label.text = FormatSeconds(secondsLeft);
        }

        public override void OnReturnedToPool()
        {
            SetWaiting();
            base.OnReturnedToPool();
        }

        /// <summary>"8s" under a minute, "1:05" from a minute up.</summary>
        static string FormatSeconds(float seconds)
        {
            int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return total >= 60 ? $"{total / 60}:{total % 60:00}" : $"{total}s";
        }
    }
}
