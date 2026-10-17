using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// What a seated visitor wants, and how many of it.
    ///
    /// The only place the player can see that somebody is waiting, which makes it the whole
    /// interface of this system: an icon, a number, and a disc that goes orange when the
    /// storeroom cannot cover it. A visitor never gives up and leaves, so a bubble the player
    /// cannot yet fill is a standing obligation they can act on rather than a timer running out
    /// behind their back.
    ///
    /// Built from SPRITE RENDERERS and a TextMesh rather than a world-space Canvas, matching
    /// the rest of this project, which has none. That is a deliberate choice and not an
    /// oversight: when a world-space canvas does not show up there are six plausible reasons
    /// and no way to tell them apart without a running editor, where two sprites have one
    /// failure mode - they are in the wrong place - and it is visible in the scene view.
    ///
    /// Faces the camera the way the rest of the world UI does, by taking the camera's own
    /// rotation: under this level's pitched camera the disc then sits flat to the screen
    /// instead of leaning back, and every bubble in view reads the same.
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed class OrderBubble : OrderBubbleBase
    {
        [Tooltip("The whole bubble. Hidden until the visitor sits down.")]
        [SerializeField] GameObject panel;

        [Tooltip("The disc behind the icon. Tinted to say whether the order can be filled.")]
        [SerializeField] SpriteRenderer backing;

        [Tooltip("The item being asked for.")]
        [SerializeField] SpriteRenderer icon;

        [Tooltip("How many. Hidden for an order of one, where the number is noise.")]
        [SerializeField] TextMesh amount;

        [Tooltip("Metres across the disc is drawn. Sprites arrive at whatever pixels-per-unit " +
                 "they were imported at, so the size is normalised rather than assumed.")]
        [SerializeField] float discSize = 0.66f;

        [SerializeField] float iconSize = 0.4f;

        static readonly Color Affordable = new Color(0.99f, 0.97f, 0.93f);
        static readonly Color Short = new Color(0.95f, 0.55f, 0.32f);

        Camera view;
        float flashTimer;

        public override bool IsShowing => panel != null && panel.activeSelf;

        void Awake() => Hide();

        void Start() => Fit(backing, discSize);

        void LateUpdate()
        {
            if (panel == null || !panel.activeSelf) return;

            if (view == null) view = Camera.main;
            if (view == null) return;

            // Local +Z points the way the camera looks, which is the side a TextMesh reads
            // correctly from and puts the count, a little toward -Z, in front of the disc.
            // Both faces of a sprite draw, so the icon would look the same either way - but a
            // TextMesh seen from behind is legible and backwards, which reads as a font problem
            // rather than a rotation problem.
            transform.rotation = view.transform.rotation;

            TickFlash();
        }

        /// <summary>
        /// Shows an order.
        ///
        /// <paramref name="affordable"/> is re-asserted every time the inventory changes rather
        /// than only when the order is placed - the interesting state is the moment a dish comes
        /// out of the kitchen and a waiting visitor becomes servable, and a disc that only
        /// updates on a tap would hide it.
        /// </summary>
        public override void Show(Sprite item, int count, bool affordable)
        {
            if (panel != null) panel.SetActive(true);

            SetAffordable(affordable);

            if (icon != null)
            {
                icon.sprite = item;
                icon.enabled = item != null;
                Fit(icon, iconSize);
            }

            if (amount == null) return;

            amount.text = $"x{count}";

            // One of something needs no number. The count is there to warn the player that this
            // one is expensive, and a row of "x1" over every table teaches them to stop reading
            // it.
            amount.gameObject.SetActive(count > 1);
        }

        public override void SetAffordable(bool affordable)
        {
            if (backing == null) return;

            backing.color = affordable ? Affordable : Short;
        }

        public override void Hide()
        {
            flashTimer = 0f;
            if (panel != null) panel.SetActive(false);

            // The count hangs beside the panel rather than inside it, so it goes separately - or
            // an "x3" stays floating over somebody already eating it.
            if (amount != null) amount.gameObject.SetActive(false);
        }

        /// <summary>
        /// A short orange pulse, for a tap that could not be filled.
        ///
        /// 4 is explicit that a tap is never silently accepted or silently refused. The message
        /// on the HUD says what is missing; this says which bubble it was about, which the
        /// message alone cannot once there are four of them.
        /// </summary>
        public override void FlashShort()
        {
            SetAffordable(false);
            flashTimer = 0.6f;
        }

        void TickFlash()
        {
            if (flashTimer <= 0f) return;

            flashTimer -= Time.deltaTime;

            if (backing == null) return;

            // A pulse rather than a flash: something that changes size for half a second reads
            // at this camera distance, where a colour blink does not.
            float pulse = 1f + Mathf.Sin(flashTimer * 24f) * 0.09f;
            backing.transform.localScale = BaseScale(backing) * pulse;

            if (flashTimer > 0f) return;

            backing.transform.localScale = BaseScale(backing);
        }

        /// <summary>
        /// Scales a sprite to a size in metres.
        ///
        /// Item sprites come from the project's item database and were imported at whatever
        /// pixels-per-unit somebody chose, which has no reason to match a builtin UI knob.
        /// Without this, one of them is a postage stamp and the other fills the room.
        /// </summary>
        static void Fit(SpriteRenderer renderer, float metres)
        {
            if (renderer == null || renderer.sprite == null) return;

            Vector3 size = renderer.sprite.bounds.size;
            float largest = Mathf.Max(size.x, size.y);

            renderer.transform.localScale = largest > 1e-4f
                ? Vector3.one * (metres / largest)
                : Vector3.one;
        }

        Vector3 BaseScale(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.sprite == null) return Vector3.one;

            Vector3 size = renderer.sprite.bounds.size;
            float largest = Mathf.Max(size.x, size.y);

            return largest > 1e-4f ? Vector3.one * (discSize / largest) : Vector3.one;
        }

        public void Configure(GameObject root, SpriteRenderer disc, SpriteRenderer item,
                              TextMesh count)
        {
            panel = root;
            backing = disc;
            icon = item;
            amount = count;
        }
    }
}
