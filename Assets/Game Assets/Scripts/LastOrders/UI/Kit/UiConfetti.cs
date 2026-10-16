using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// A shower of paper confetti in the diner's colours: thrown up from a point, tumbling and
    /// fluttering as it falls, fading out at the end. Simple physics on unscaled time, so it still
    /// falls while the game is paused behind a celebration.
    /// </summary>
    public sealed class UiConfetti : MonoBehaviour
    {
        [SerializeField] Sprite piece;
        [SerializeField] Color[] colours = { Color.white };
        [SerializeField] int count = 70;
        [SerializeField] float gravity = 1500f;
        [SerializeField] float life = 2.8f;
        [SerializeField] Vector2 speed = new Vector2(700f, 1350f);
        [SerializeField] float spread = 70f;

        sealed class Bit
        {
            public RectTransform rect;
            public Image image;
            public Vector2 velocity;
            public float spin, flutter, phase, age;
        }

        readonly List<Bit> bits = new List<Bit>();

        void OnDisable() => Clear();

        /// <summary>Throws a handful up from <paramref name="origin"/>, in this object's local space.</summary>
        public void Play(Vector2 origin)
        {
            Clear();
            if (piece == null) return;

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Confetti", typeof(RectTransform)) { layer = gameObject.layer };
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.anchoredPosition = origin + Random.insideUnitCircle * 30f;
                rt.sizeDelta = new Vector2(Random.Range(12f, 18f), Random.Range(20f, 30f));

                var image = go.AddComponent<Image>();
                image.sprite = piece;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 3f;
                image.color = colours.Length > 0 ? colours[Random.Range(0, colours.Length)] : Color.white;
                image.raycastTarget = false;

                float angle = (90f + Random.Range(-spread, spread)) * Mathf.Deg2Rad;
                bits.Add(new Bit
                {
                    rect = rt,
                    image = image,
                    velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(speed.x, speed.y),
                    spin = Random.Range(-540f, 540f),
                    flutter = Random.Range(6f, 12f),
                    phase = Random.Range(0f, 6.28f),
                });
            }
        }

        void Update()
        {
            if (bits.Count == 0) return;

            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                Bit b = bits[i];
                b.age += dt;
                if (b.rect == null || b.age >= life)
                {
                    if (b.rect != null) Destroy(b.rect.gameObject);
                    bits.RemoveAt(i);
                    continue;
                }

                // Air drag once it is falling, so it drifts down rather than drops.
                b.velocity.y -= gravity * dt;
                if (b.velocity.y < -380f) b.velocity.y = Mathf.Lerp(b.velocity.y, -380f, dt * 6f);
                b.velocity.x = Mathf.Lerp(b.velocity.x, Mathf.Sin(b.age * 2f + b.phase) * 120f, dt * 1.5f);

                b.rect.anchoredPosition += b.velocity * dt;
                b.rect.localRotation = Quaternion.Euler(0f, 0f, b.rect.localEulerAngles.z + b.spin * dt);
                b.rect.localScale = new Vector3(Mathf.Cos(b.age * b.flutter + b.phase), 1f, 1f);

                float fade = Mathf.Clamp01((life - b.age) / 0.6f);
                Color c = b.image.color;
                c.a = fade;
                b.image.color = c;
            }
        }

        void Clear()
        {
            foreach (Bit b in bits)
                if (b.rect != null) Destroy(b.rect.gameObject);
            bits.Clear();
        }
    }
}
