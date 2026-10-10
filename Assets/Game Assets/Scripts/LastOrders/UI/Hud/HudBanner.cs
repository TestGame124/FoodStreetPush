using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Hud
{
    /// <summary>
    /// The big ribbon that marks a turn in the day - DINER OPEN, ZOMBIES INCOMING, WAVE 2 OF 3 -
    /// pops in under the top bar, holds, and floats away.
    ///
    /// Kept in the upper third and short-lived, so it announces without covering the entrance and
    /// the dining room, which the HUD keeps clear. A banner asked for while one is up waits its turn.
    /// </summary>
    public sealed class HudBanner : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform body;
        [SerializeField] Image ribbon;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text subtitle;

        [SerializeField] float inSeconds = 0.32f;
        [SerializeField] float holdSeconds = 1.9f;
        [SerializeField] float outSeconds = 0.35f;

        float clock = -1f;
        bool pending;
        string pendingTitle, pendingSubtitle;
        Color pendingColour;

        public void Configure(CanvasGroup canvasGroup, RectTransform content, Image ribbonImage, TMP_Text heading, TMP_Text line)
        {
            group = canvasGroup;
            body = content;
            ribbon = ribbonImage;
            title = heading;
            subtitle = line;
        }

        public void Show(string heading, string line, Color colour)
        {
            // Mid-show: replace a banner that is still popping in, queue behind one that has settled.
            if (clock >= 0f && clock > inSeconds)
            {
                pending = true;
                pendingTitle = heading;
                pendingSubtitle = line;
                pendingColour = colour;
                return;
            }

            Begin(heading, line, colour);
        }

        void Begin(string heading, string line, Color colour)
        {
            title.text = heading;
            subtitle.text = line ?? "";
            subtitle.gameObject.SetActive(!string.IsNullOrEmpty(line));
            ribbon.color = colour;

            clock = 0f;
            group.alpha = 0f;
            gameObject.SetActive(true);
        }

        void Awake()
        {
            if (group != null) group.alpha = 0f;
        }

        void Update()
        {
            if (clock < 0f) return;

            clock += Time.unscaledDeltaTime;
            float hold = holdSeconds + (pending ? -0.6f : 0f);

            float scale, alpha, rise;
            if (clock < inSeconds)
            {
                float t = clock / inSeconds;
                scale = Mathf.LerpUnclamped(0.55f, 1f, BackOut(t));
                alpha = Mathf.Clamp01(t * 2.2f);
                rise = 0f;
            }
            else if (clock < inSeconds + hold)
            {
                scale = 1f;
                alpha = 1f;
                rise = 0f;
            }
            else
            {
                float t = Mathf.Clamp01((clock - inSeconds - hold) / outSeconds);
                scale = Mathf.Lerp(1f, 0.96f, t);
                alpha = 1f - t;
                rise = t * 26f;

                if (t >= 1f)
                {
                    clock = -1f;
                    group.alpha = 0f;

                    if (pending)
                    {
                        pending = false;
                        Begin(pendingTitle, pendingSubtitle, pendingColour);
                    }

                    return;
                }
            }

            group.alpha = alpha;
            body.localScale = new Vector3(scale, scale, 1f);
            body.anchoredPosition = new Vector2(0f, rise);
        }

        static float BackOut(float t)
        {
            const float s = 1.9f;
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }
    }
}
