using LastOrders.UI.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.Livestock
{
    /// <summary>
    /// The bubble that stands over a farm building whenever it wants the player: an egg or a bottle
    /// of milk with how many are waiting, bobbing over the coop or the shelter, its ring glowing -
    /// green while there is room, orange and quicker once it is full and the animals have stopped.
    /// Hungry, it shows the corn or the wheat they are waiting for, in red.
    ///
    /// Takes no raycasts: tapping the building collects or feeds.
    /// </summary>
    public sealed class ProduceBubbleView : WorldUiElement
    {
        [Tooltip("The part that bobs: everything but the root and the pop-in body.")]
        [SerializeField] RectTransform bob;

        [SerializeField] Image icon;

        [Tooltip("The glowing ring round the bubble: its colour says ready, full or hungry.")]
        [SerializeField] Image ring;

        [SerializeField] GameObject badge;
        [SerializeField] TMP_Text badgeText;

        float pulse = 1f;
        float phase;

        /// <summary>Shows <paramref name="sprite"/> with <paramref name="label"/> on its badge (none when empty), the ring in <paramref name="colour"/>.</summary>
        public void Show(Sprite sprite, string label, Color colour, float pulseRate)
        {
            if (icon != null)
            {
                if (icon.sprite != sprite) icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (ring != null) ring.color = colour;

            bool hasLabel = !string.IsNullOrEmpty(label);
            if (badge != null && badge.activeSelf != hasLabel) badge.SetActive(hasLabel);
            if (badgeText != null && hasLabel) badgeText.text = label;
            pulse = pulseRate;
        }

        public override void OnTakenFromPool()
        {
            base.OnTakenFromPool();
            phase = Random.value * 10f;
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            phase += Time.unscaledDeltaTime * pulse;
            if (bob != null) bob.anchoredPosition = new Vector2(0f, Mathf.Sin(phase * 2.6f) * 7f);
            if (ring != null)
            {
                float s = 1f + 0.07f * Mathf.Max(0f, Mathf.Sin(phase * 4f));
                ring.rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }

        public override void OnReturnedToPool()
        {
            if (bob != null) bob.anchoredPosition = Vector2.zero;
            if (ring != null) ring.rectTransform.localScale = Vector3.one;
            base.OnReturnedToPool();
        }

        /// <summary>Editor wiring, for the prefab builder.</summary>
        public void Configure(RectTransform animated, RectTransform bobbing, Image itemIcon, Image glowRing, GameObject countBadge, TMP_Text countLabel)
        {
            body = animated;
            bob = bobbing;
            icon = itemIcon;
            ring = glowRing;
            badge = countBadge;
            badgeText = countLabel;
        }
    }
}
