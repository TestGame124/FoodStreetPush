using DG.Tweening;
using LastOrders.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// The dish a seated visitor is waiting for, in the HUD's look: the dish on a cream disc in a
    /// coloured ring, a beak pointing down at them, "x2" on a badge when they want two, and what
    /// serving them pays on a coin chip.
    ///
    /// The ring says what to do. Amber: the kitchen has not got it yet. Green, with a tick, breathing
    /// and glowing: it can be served now - tap them. Teal, with a waiter: somebody is bringing it.
    ///
    /// It pops in when they sit down, bobs while they wait, springs and sparkles the moment the dish
    /// becomes servable, shakes red at a tap that could not be filled, and bursts up and away when
    /// the plate goes down.
    ///
    /// A world-space canvas that takes no raycasts; the tap target is the collider on the body, so a
    /// tap on the bubble is a tap on the visitor.
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed class DishBubble : OrderBubbleBase
    {
        [Tooltip("Everything that shows, and carries the tap collider. Off while hidden.")]
        [SerializeField] RectTransform body;

        [Tooltip("The part that bobs and springs, inside the body.")]
        [SerializeField] RectTransform bob;

        [SerializeField] Image ring;
        [SerializeField] Image glow;
        [SerializeField] Image icon;

        [SerializeField] GameObject countBadge;
        [SerializeField] TMP_Text countLabel;

        [Tooltip("The round badge on the other shoulder: a tick when it can be served, a waiter when one is coming.")]
        [SerializeField] Image stateBadge;
        [SerializeField] Image stateGlyph;
        [SerializeField] Sprite readySprite;
        [SerializeField] Sprite waiterSprite;

        [SerializeField] GameObject coinChip;
        [SerializeField] TMP_Text coinLabel;

        [Tooltip("Where the sparkles go, over everything; takes no raycasts.")]
        [SerializeField] RectTransform fx;
        [SerializeField] Sprite sparkle;

        [SerializeField] Color readyColour = new Color(0.42f, 0.70f, 0.42f);
        [SerializeField] Color shortColour = new Color(0.92f, 0.60f, 0.24f);
        [SerializeField] Color waiterColour = new Color(0.22f, 0.60f, 0.57f);
        [SerializeField] Color flashColour = new Color(0.78f, 0.31f, 0.25f);

        Canvas canvas;
        Camera view;
        bool affordable;
        bool waiterComing;
        bool leaving;
        float shake;
        float flashLeft;
        float bobPhase;

        public override bool IsShowing => body != null && body.gameObject.activeSelf && !leaving;

        void Awake()
        {
            canvas = GetComponent<Canvas>();
            bobPhase = Random.value * 10f;
            Hide();
        }

        void LateUpdate()
        {
            if (body == null || !body.gameObject.activeSelf) return;

            if (view == null) view = Camera.main;
            if (view == null) return;

            if (canvas != null && canvas.worldCamera != view) canvas.worldCamera = view;
            transform.rotation = view.transform.rotation;

            float t = Time.time + bobPhase;
            float dt = Time.deltaTime;

            if (bob != null && !leaving)
            {
                // Waiting bobs gently; servable breathes bigger, to be noticed across the room.
                float y = Mathf.Sin(t * 2.3f) * 5f;
                float breathe = Ready ? 1f + 0.06f * (0.5f + 0.5f * Mathf.Sin(t * 5.5f)) : 1f;

                shake = Mathf.MoveTowards(shake, 0f, dt * (shake * 6f + 8f));
                Vector2 jolt = shake > 0.05f ? Random.insideUnitCircle * shake : Vector2.zero;

                bob.anchoredPosition = new Vector2(jolt.x, y + jolt.y);
                if (!DOTween.IsTweening(bob)) bob.localScale = Vector3.one * breathe;
            }

            if (glow != null)
            {
                float a = Ready ? 0.35f + 0.25f * Mathf.Sin(t * 5.5f) : 0f;
                glow.color = new Color(readyColour.r, readyColour.g, readyColour.b, Mathf.MoveTowards(glow.color.a, a, dt * 3f));
            }

            if (flashLeft > 0f)
            {
                flashLeft -= dt;
                if (ring != null) ring.color = Color.Lerp(RingColour, flashColour, Mathf.Clamp01(flashLeft / 0.25f));
            }
        }

        bool Ready => affordable && !waiterComing;

        Color RingColour => waiterComing ? waiterColour : affordable ? readyColour : shortColour;

        public override void Show(Sprite item, int count, bool canServe) => Show(item, count, canServe, 0);

        public override void Show(Sprite item, int count, bool canServe, int coins)
        {
            if (body == null) return;

            bool appearing = !body.gameObject.activeSelf || leaving;
            leaving = false;
            body.gameObject.SetActive(true);

            if (icon != null)
            {
                icon.sprite = item;
                icon.enabled = item != null;
            }

            // One of something needs no number: a row of "x1" over every table teaches the player to stop reading it.
            if (countBadge != null) countBadge.SetActive(count > 1);
            if (countLabel != null) countLabel.text = $"x{count}";

            if (coinChip != null) coinChip.SetActive(coins > 0);
            if (coinLabel != null) coinLabel.text = coins.ToString();

            affordable = canServe;
            ApplyState(animate: false);

            if (appearing) PopIn();
        }

        public override void SetAffordable(bool canServe)
        {
            if (canServe == affordable) return;

            bool becameReady = canServe && !affordable;
            affordable = canServe;
            ApplyState(animate: true);

            // The moment a dish comes out of the kitchen and somebody becomes servable is the
            // interesting one: a spring and a burst say so from across the room.
            if (becameReady && IsShowing && !waiterComing) Celebrate(8);
        }

        public override void SetWaiterComing(bool coming)
        {
            if (coming == waiterComing) return;

            waiterComing = coming;
            ApplyState(animate: true);
        }

        public override void FlashShort()
        {
            if (!IsShowing) return;

            shake = 9f;
            flashLeft = 0.6f;
            Punch(0.12f);
        }

        public override void Served()
        {
            if (body == null || !body.gameObject.activeSelf || leaving)
            {
                Hide();
                return;
            }

            leaving = true;
            Celebrate(10);

            if (bob == null)
            {
                Hide();
                return;
            }

            DOTween.Kill(bob);
            DOTween.Sequence().SetTarget(bob)
                   .Append(bob.DOScale(1.25f, 0.12f).SetEase(Ease.OutQuad))
                   .Join(bob.DOAnchorPosY(bob.anchoredPosition.y + 30f, 0.34f).SetEase(Ease.OutQuad))
                   .Append(bob.DOScale(0f, 0.2f).SetEase(Ease.InBack))
                   .OnComplete(Hide);
        }

        public override void Hide()
        {
            if (bob != null)
            {
                DOTween.Kill(bob);
                bob.localScale = Vector3.one;
                bob.anchoredPosition = Vector2.zero;
                bob.localRotation = Quaternion.identity;
            }

            leaving = false;
            shake = 0f;
            flashLeft = 0f;
            waiterComing = false;
            if (glow != null) glow.color = new Color(readyColour.r, readyColour.g, readyColour.b, 0f);
            if (body != null) body.gameObject.SetActive(false);
        }

        void ApplyState(bool animate)
        {
            Color colour = RingColour;
            if (ring != null)
            {
                DOTween.Kill(ring);
                if (animate && isActiveAndEnabled) ring.DOColor(colour, 0.25f).SetTarget(ring);
                else ring.color = colour;
            }

            Sprite glyph = waiterComing ? waiterSprite : affordable ? readySprite : null;
            bool badge = glyph != null;

            if (stateGlyph != null) stateGlyph.sprite = glyph;
            if (stateBadge != null)
            {
                stateBadge.color = waiterComing ? waiterColour : readyColour;
                bool was = stateBadge.gameObject.activeSelf;
                stateBadge.gameObject.SetActive(badge);

                if (badge && (!was || animate))
                {
                    RectTransform rt = stateBadge.rectTransform;
                    DOTween.Kill(rt);
                    rt.localScale = Vector3.one * 0.3f;
                    rt.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 2.4f).SetTarget(rt);
                }
            }
        }

        void PopIn()
        {
            if (bob == null) return;

            DOTween.Kill(bob);
            bob.localScale = Vector3.one * 0.2f;
            bob.localRotation = Quaternion.Euler(0f, 0f, -12f);
            DOTween.Sequence().SetTarget(bob)
                   .Append(bob.DOScale(1f, 0.38f).SetEase(Ease.OutBack, 2.2f))
                   .Join(bob.DOLocalRotate(Vector3.zero, 0.5f).SetEase(Ease.OutElastic, 1.1f, 0.35f));
        }

        void Punch(float amount)
        {
            if (bob == null) return;

            DOTween.Kill(bob);
            bob.localScale = Vector3.one;
            bob.DOPunchScale(Vector3.one * amount, 0.35f, 7, 0.6f).SetTarget(bob);
        }

        void Celebrate(int sparkles)
        {
            Punch(0.22f);

            if (fx == null || sparkle == null || icon == null) return;

            Vector2 at = UiFx.ScreenOf(icon.transform);
            UiFx.Burst(fx, at, sparkle, Color.white, count: sparkles, distance: 120f, size: 30f);
            if (ring != null) UiFx.Ring(fx, at, ring.sprite, new Color(readyColour.r, readyColour.g, readyColour.b, 0.8f), 90f, 200f);
        }

        /// <summary>Editor wiring, for the bubble builder.</summary>
        public void Configure(RectTransform bodyRect, RectTransform bobRect, Image ringImage, Image glowImage, Image iconImage,
                              GameObject count, TMP_Text countText, Image badge, Image glyph, Sprite ready, Sprite waiter,
                              GameObject coins, TMP_Text coinText, RectTransform sparkleLayer, Sprite sparkleSprite)
        {
            body = bodyRect;
            bob = bobRect;
            ring = ringImage;
            glow = glowImage;
            icon = iconImage;
            countBadge = count;
            countLabel = countText;
            stateBadge = badge;
            stateGlyph = glyph;
            readySprite = ready;
            waiterSprite = waiter;
            coinChip = coins;
            coinLabel = coinText;
            fx = sparkleLayer;
            sparkle = sparkleSprite;
        }
    }
}
