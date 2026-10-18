using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastOrders.UI.Hud
{
    /// <summary>
    /// The feel of one HUD element: it gives under a finger and springs back, it can breathe to ask
    /// for attention, it can be punched when its number changes, and it can shake when it is hit.
    ///
    /// One component rather than four because all of them drive the same scale; separate ones would
    /// fight over it. Runs on unscaled time, so a slowed-down game still has a lively HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudMotion : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] bool pressable = true;
        [SerializeField] [Range(0.7f, 1f)] float pressedScale = 0.92f;

        [Tooltip("Breathes in and out gently while on - the HUD's way of pointing at the next thing to do.")]
        [SerializeField] bool breathe;
        [SerializeField] float breatheAmount = 0.04f;
        [SerializeField] float breatheSpeed = 3f;

        RectTransform rect;
        Selectable selectable;
        Vector3 baseScale = Vector3.one;
        Vector2 basePosition;

        float press = 1f, pressGoal = 1f, pressVelocity;
        float punch, punchVelocity;
        float breath;
        float shake;
        bool shaking;

        public bool Breathe
        {
            get => breathe;
            set => breathe = value;
        }

        /// <summary>Gives under a finger: a button, rather than a panel that only punches and breathes.</summary>
        public bool Pressable => pressable;

        /// <summary>
        /// A pressable element let go of with the finger still on it: a tap, as the player feels
        /// it. Not raised when the press slid off, or turned into a drag or a scroll. For whatever
        /// answers every tap the same way - the click sound - without each button wiring it.
        /// </summary>
        public static event System.Action<HudMotion> Pressed;

        void Awake()
        {
            rect = (RectTransform)transform;
            selectable = GetComponent<Selectable>();
            baseScale = rect.localScale;
        }

        void OnDisable()
        {
            press = pressGoal = 1f;
            pressVelocity = punch = punchVelocity = 0f;
            if (shaking) rect.anchoredPosition = basePosition;
            shaking = false;
            shake = 0f;
            if (rect != null) rect.localScale = baseScale;
        }

        /// <summary>A springy kick: positive grows then settles, negative dips.</summary>
        public void Punch(float amount = 0.15f) => punchVelocity += amount * 30f;

        /// <summary>Pops in: starts at <paramref name="from"/> of its size and springs up past full size and back.</summary>
        public void Appear(float from = 0.45f)
        {
            punch = -(1f - Mathf.Clamp01(from));
            punchVelocity = 0f;
            if (rect != null) rect.localScale = baseScale * (1f + punch);
        }

        /// <summary>A short jolt, for a hit.</summary>
        public void Shake(float pixels = 10f)
        {
            if (!shaking) basePosition = rect.anchoredPosition;
            shaking = true;
            shake = Mathf.Max(shake, pixels);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!pressable || (selectable != null && !selectable.IsInteractable())) return;
            pressGoal = pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            bool tapped = pressGoal < 1f && !eventData.dragging;
            Release(true);
            if (tapped) Pressed?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData) => Release(false);

        void Release(bool kick)
        {
            if (pressGoal >= 1f) return;
            pressGoal = 1f;
            if (kick) pressVelocity += 3.5f;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

            Spring(ref press, ref pressVelocity, pressGoal, 700f, 30f, dt);
            Spring(ref punch, ref punchVelocity, 0f, 320f, 13f, dt);

            breath = Mathf.MoveTowards(breath, breathe ? 1f : 0f, dt * 3f);
            float b = 1f + breath * breatheAmount * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * breatheSpeed));

            rect.localScale = baseScale * (press * (1f + punch) * b);

            if (!shaking) return;

            shake = Mathf.MoveTowards(shake, 0f, dt * (shake * 5f + 6f));
            if (shake <= 0.05f)
            {
                shaking = false;
                rect.anchoredPosition = basePosition;
                return;
            }

            rect.anchoredPosition = basePosition + Random.insideUnitCircle * shake;
        }

        static void Spring(ref float value, ref float velocity, float goal, float stiffness, float damping, float dt)
        {
            velocity += ((goal - value) * stiffness - velocity * damping) * dt;
            value += velocity * dt;
        }
    }
}
