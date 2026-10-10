using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Hud
{
    /// <summary>
    /// A rounded fill bar that eases to its value, with an optional "ghost" that lingers where the
    /// value was and then drains after it - the familiar health-bar trail that makes a hit readable.
    ///
    /// The fill is sized rather than masked, so its rounded right end stays rounded at every value,
    /// and never drawn narrower than it is tall, so a nearly empty bar is a pill and not a sliver.
    /// </summary>
    [ExecuteAlways]
    public sealed class HudBar : MonoBehaviour
    {
        [SerializeField] RectTransform fill;
        [SerializeField] Image fillImage;
        [SerializeField] RectTransform ghost;

        [Tooltip("How fast the fill eases to its value, per second.")]
        [SerializeField] float speed = 7f;

        [Tooltip("Seconds the ghost waits after a drop before it drains.")]
        [SerializeField] float ghostHold = 0.45f;

        [SerializeField] [Range(0f, 1f)] float value = 1f;

        float shown = -1f;
        float ghostShown;
        float ghostWait;

        public float Value => value;

        public void Configure(RectTransform fillRect, Image image, RectTransform ghostRect)
        {
            fill = fillRect;
            fillImage = image;
            ghost = ghostRect;
        }

        public void Set(float t, bool instant = false)
        {
            t = Mathf.Clamp01(t);

            if (t < value - 0.0001f) ghostWait = ghostHold;
            value = t;

            if (instant || !Application.isPlaying || shown < 0f)
            {
                shown = ghostShown = t;
                Apply();
            }
        }

        public void SetColour(Color colour)
        {
            if (fillImage != null) fillImage.color = colour;
        }

        void OnEnable()
        {
            shown = value;
            ghostShown = value;
            Apply();
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                shown = ghostShown = value;
                Apply();
                return;
            }

            float dt = Time.unscaledDeltaTime;
            shown = Mathf.Lerp(shown, value, 1f - Mathf.Exp(-speed * dt));
            if (Mathf.Abs(shown - value) < 0.0005f) shown = value;

            if (ghostShown < shown) ghostShown = shown;
            else if ((ghostWait -= dt) <= 0f) ghostShown = Mathf.MoveTowards(ghostShown, shown, dt * 0.9f);

            Apply();
        }

        void Apply()
        {
            Size(fill, shown);
            Size(ghost, ghostShown);
        }

        void Size(RectTransform rect, float t)
        {
            if (rect == null) return;

            var parent = (RectTransform)rect.parent;
            float width = parent.rect.width;
            float height = parent.rect.height;

            bool visible = t > 0.001f;
            if (rect.gameObject.activeSelf != visible) rect.gameObject.SetActive(visible);
            if (!visible) return;

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(Mathf.Lerp(Mathf.Min(height, width), width, t), 0f);
        }
    }
}
