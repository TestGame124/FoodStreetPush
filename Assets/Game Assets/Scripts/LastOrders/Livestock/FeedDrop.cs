using System;
using UnityEngine;

namespace LastOrders.Livestock
{
    /// <summary>
    /// One item of feed on its way into a farm building, shown as its icon: it pops up where the
    /// popup's FEED button was, arcs over and drops into the feeder - or the front of the hay
    /// rack - shrinking as it goes in, and says so when it lands. The other half of the harvest
    /// pickups, which fly out of the plant beds into the storehouse; drawn with their material,
    /// so it reads as the same kind of thing going the other way.
    ///
    /// Pooled by the building that throws it: hidden between throws, never destroyed.
    /// </summary>
    public sealed class FeedDrop : MonoBehaviour
    {
        [SerializeField] SpriteRenderer icon;

        [Tooltip("A soft glow behind the icon, turning slowly. Optional.")]
        [SerializeField] SpriteRenderer glow;

        [Tooltip("How big the icon is drawn across its longer side, in metres.")]
        [SerializeField] float iconSize = 0.55f;

        [Tooltip("How wide the glow is, against the icon.")]
        [SerializeField] float glowSize = 1.6f;

        [Tooltip("The least share of the screen's height the icon takes, so it reads with the camera zoomed out.")]
        [SerializeField] float minScreenHeight = 0.07f;

        [Tooltip("Seconds from setting off to landing.")]
        [SerializeField] float flyTime = 0.75f;

        [Tooltip("How far above the higher end the arc goes, in metres, before it grows with the distance.")]
        [SerializeField] float arc = 0.8f;

        static readonly int FlashId = Shader.PropertyToID("_Flash");

        Camera view;
        MaterialPropertyBlock block;
        Vector3 from, to, over;
        float time, delay, zoom = 1f, roll;
        Action<FeedDrop> landed;

        public bool Flying { get; private set; }

        /// <summary>
        /// Throws <paramref name="sprite"/> from <paramref name="start"/> into <paramref name="end"/>
        /// after <paramref name="wait"/> seconds; <paramref name="onLanded"/> is told when it is in.
        /// </summary>
        public void Fly(Sprite sprite, Vector3 start, Vector3 end, float wait, Action<FeedDrop> onLanded)
        {
            view = Camera.main;
            from = start;
            to = end;
            delay = Mathf.Max(0f, wait);
            time = 0f;
            landed = onLanded;
            roll = UnityEngine.Random.Range(14f, 24f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);

            float distance = Vector3.Distance(from, to);
            over = Vector3.Lerp(from, to, 0.5f);
            over.y = Mathf.Max(from.y, to.y) + arc + distance * 0.18f;

            zoom = ZoomScale(to);
            Fit(sprite);

            Flying = true;
            gameObject.SetActive(true);
            Pose(0f, 0f);
        }

        /// <summary>Puts it away, landed or not.</summary>
        public void Stop()
        {
            Cancel();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Drops it mid-flight without landing and hides it where it is - for the building going away,
        /// when its children are already being switched off and cannot be switched off again.
        /// </summary>
        public void Cancel()
        {
            Flying = false;
            landed = null;
            transform.localScale = Vector3.zero;
        }

        void Fit(Sprite sprite)
        {
            if (icon == null) return;

            icon.sprite = sprite;
            icon.drawMode = SpriteDrawMode.Simple;
            if (sprite == null) return;

            // Centred on the body, so it turns and shrinks about its middle.
            Bounds bounds = sprite.bounds;
            float scale = iconSize / Mathf.Max(bounds.size.x, bounds.size.y, 1e-4f);
            icon.transform.localPosition = -bounds.center * scale;
            icon.transform.localScale = Vector3.one * scale;

            if (glow != null)
            {
                glow.transform.localPosition = new Vector3(0f, 0f, 0.02f);
                glow.transform.localScale = Vector3.one * (iconSize * glowSize / Mathf.Max(glow.sprite != null ? glow.sprite.bounds.size.x : 1f, 1e-4f));
            }
        }

        /// <summary>How much bigger than Icon Size it must be drawn at <paramref name="at"/> to fill Min Screen Height.</summary>
        float ZoomScale(Vector3 at)
        {
            if (view == null || iconSize <= 0f) return 1f;

            float distance = Vector3.Dot(at - view.transform.position, view.transform.forward);
            if (distance <= 0f) return 1f;

            float screenHeight = 2f * distance * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return Mathf.Max(1f, minScreenHeight * screenHeight / iconSize);
        }

        void Update()
        {
            if (!Flying) return;

            float dt = Time.deltaTime;
            if (delay > 0f)
            {
                delay -= dt;
                Pose(0f, 0f);
                return;
            }

            time += dt;
            float t = Mathf.Clamp01(time / Mathf.Max(0.05f, flyTime));
            Pose(t, 1f);

            if (t < 1f) return;

            Action<FeedDrop> told = landed;
            Stop();
            told?.Invoke(this);
        }

        void Pose(float t, float shown)
        {
            // Slow over the top, quicker into the feeder.
            float along = t * (0.7f + 0.3f * t);
            Vector3 a = Vector3.Lerp(from, over, along);
            Vector3 b = Vector3.Lerp(over, to, along);
            transform.position = Vector3.Lerp(a, b, along);

            // Pops up, holds, and shrinks away as it goes in.
            float pop = t < 0.16f ? OutBack(t / 0.16f) : 1f;
            float shrink = t > 0.72f ? Mathf.Lerp(1f, 0.25f, (t - 0.72f) / 0.28f) : 1f;
            transform.localScale = Vector3.one * (zoom * pop * shrink * shown);

            Quaternion facing = view != null ? view.transform.rotation : Quaternion.identity;
            transform.rotation = facing * Quaternion.Euler(0f, 0f, roll * Mathf.Sin(t * Mathf.PI * 1.5f));

            if (glow != null) glow.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 90f);

            // A white blink as it appears, as the pickups have.
            if (icon != null)
            {
                block ??= new MaterialPropertyBlock();
                icon.GetPropertyBlock(block);
                block.SetFloat(FlashId, t < 0.12f ? 1f - t / 0.12f : 0f);
                icon.SetPropertyBlock(block);
            }
        }

        static float OutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            x = Mathf.Clamp01(x);
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        /// <summary>Editor wiring, for the prefab builder.</summary>
        public void Configure(SpriteRenderer iconRenderer, SpriteRenderer glowRenderer)
        {
            icon = iconRenderer;
            glow = glowRenderer;
        }
    }
}
