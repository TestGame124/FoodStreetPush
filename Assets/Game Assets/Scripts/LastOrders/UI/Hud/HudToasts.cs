using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.UI.Hud
{
    /// <summary>
    /// The answers to taps and the news of the night, as a short stack of toasts under the day card.
    ///
    /// Replaces the old habit of writing every message over the objective's second line, which hid
    /// the objective exactly when the player most needed it and lost a message whenever two arrived
    /// together. A repeated message refreshes the toast already showing it instead of stacking a copy.
    /// </summary>
    public sealed class HudToasts : MonoBehaviour
    {
        [SerializeField] HudSkin skin;
        [SerializeField] HudToast template;

        [SerializeField] int maxShown = 3;
        [SerializeField] float spacing = 12f;
        [SerializeField] float seconds = 3.4f;
        [SerializeField] float maxTextWidth = 720f;

        readonly List<HudToast> live = new List<HudToast>();
        readonly Stack<HudToast> pool = new Stack<HudToast>();

        public void Configure(HudSkin hudSkin, HudToast toastTemplate)
        {
            skin = hudSkin;
            template = toastTemplate;
        }

        public void Show(string message, HudTone tone)
        {
            if (string.IsNullOrEmpty(message) || template == null) return;

            foreach (HudToast existing in live)
            {
                if (existing.leaving || existing.message != message) continue;

                existing.age = Mathf.Min(existing.age, 0.3f);
                existing.life = seconds + message.Length * 0.02f;
                existing.GetComponent<HudMotion>()?.Punch(0.08f);
                return;
            }

            HudToast toast = pool.Count > 0 ? pool.Pop() : Instantiate(template, transform);
            toast.gameObject.SetActive(true);
            toast.transform.SetAsLastSibling();

            toast.message = message;
            toast.text.text = message;
            toast.age = 0f;
            toast.life = seconds + message.Length * 0.02f;
            toast.leaving = false;
            toast.group.alpha = 0f;

            Paint(toast, tone);
            Fit(toast);

            live.Insert(0, toast);
            Restack();

            toast.y = -toast.rect.sizeDelta.y * 0.8f;
            toast.rect.anchoredPosition = new Vector2(0f, toast.y);
            toast.rect.localScale = Vector3.one * 0.9f;
        }

        void Paint(HudToast toast, HudTone tone)
        {
            if (skin == null) return;

            (Color colour, string glyph) = tone switch
            {
                HudTone.Good => (HudPalette.Mint, HudSprites.GlyphCheck),
                HudTone.Warning => (HudPalette.Amber, HudSprites.GlyphAlert),
                HudTone.Danger => (HudPalette.Red, HudSprites.GlyphAlert),
                _ => (HudPalette.Teal, HudSprites.GlyphInfo),
            };

            toast.disc.color = colour;
            toast.glyph.sprite = skin[glyph];
        }

        /// <summary>Hugs the text, up to a width, wrapping onto a second line beyond it.</summary>
        void Fit(HudToast toast)
        {
            float w = Mathf.Min(toast.text.GetPreferredValues(toast.message).x, maxTextWidth);
            float h = Mathf.Max(64f, toast.text.GetPreferredValues(toast.message, w + 1f, 0f).y + 26f);

            var textRect = (RectTransform)toast.text.transform;
            textRect.sizeDelta = new Vector2(w + 2f, h - 16f);

            toast.rect.sizeDelta = new Vector2(w + 98f, h);
        }

        void Restack()
        {
            int shown = 0;
            foreach (HudToast toast in live)
            {
                if (toast.leaving) continue;
                if (shown >= maxShown) toast.leaving = true;
                else toast.slot = shown++;
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float top = 0f;

            for (int i = 0; i < live.Count; i++)
            {
                HudToast toast = live[i];
                toast.age += dt;

                if (!toast.leaving && toast.age >= toast.life)
                {
                    toast.leaving = true;
                    Restack();
                }

                float alpha = toast.leaving
                    ? Mathf.MoveTowards(toast.group.alpha, 0f, dt * 4f)
                    : Mathf.MoveTowards(toast.group.alpha, 1f, dt * 6f);
                toast.group.alpha = alpha;

                if (!toast.leaving)
                {
                    float goal = -top;
                    toast.y = Mathf.Lerp(toast.y, goal, 1f - Mathf.Exp(-14f * dt));
                    top += toast.rect.sizeDelta.y + spacing;
                }

                toast.rect.anchoredPosition = new Vector2(0f, toast.y);
                float s = Mathf.Lerp(toast.rect.localScale.x, toast.leaving ? 0.94f : 1f, 1f - Mathf.Exp(-16f * dt));
                toast.rect.localScale = new Vector3(s, s, 1f);

                if (toast.leaving && alpha <= 0f)
                {
                    toast.gameObject.SetActive(false);
                    live.RemoveAt(i--);
                    pool.Push(toast);
                }
            }
        }
    }
}
