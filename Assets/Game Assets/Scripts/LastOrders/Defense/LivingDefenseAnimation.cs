using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Defense
{
    /// <summary>
    /// A defense that is alive: it breathes, it blinks, its leaves sway, and every shot is a
    /// whole-body effort - squashed down, sprung back up past its size and wobbling still - with
    /// the round it fired gone from where it sat until it reloads, and a lever pumped down and
    /// let back up.
    ///
    /// An add-on to a <see cref="DefensePost"/>, like <see cref="DefenseRecoil"/>: it listens to
    /// the post's shots and takes every number from the post's <see cref="DefenseMotionProfile"/>,
    /// so nothing about the rules is in here and nothing about any one model either. Every part
    /// is optional; a living toaster uses all of them.
    /// </summary>
    public sealed class LivingDefenseAnimation : MonoBehaviour
    {
        [SerializeField] DefensePost post;

        [Tooltip("Squashes and stretches from its base. Everything that should squash with it - " +
                 "eyes, mouth, the loaded round - sits under it.")]
        [SerializeField] Transform body;

        [Tooltip("Shut and opened on a blink: flattened in their own Y.")]
        [SerializeField] Transform[] eyes;

        [Tooltip("Optional. The round ready to fire: gone the moment it is, back rising into place as it reloads.")]
        [SerializeField] Transform loaded;

        [Tooltip("Optional. Pushed down on a shot and let back up: a toaster's lever.")]
        [SerializeField] Transform lever;

        [Tooltip("How far the lever travels down, metres.")]
        [SerializeField] float leverTravel = 0.14f;

        [Tooltip("Optional. Sway about their own X, out of step with each other: leaves, tendrils.")]
        [SerializeField] Transform[] swaying;

        const float BlinkSeconds = 0.14f;
        const float ReloadRiseSeconds = 0.16f;
        const float LoadedDrop = 0.18f;

        Vector3 bodyScale;
        Vector3[] eyeScales;
        Vector3 loadedRest, leverRest;
        Quaternion[] swayRest;

        float sinceShot = float.MaxValue;
        float breathOffset;
        float nextBlink, blinkLeft;

        void Awake()
        {
            if (post == null) post = GetComponentInParent<DefensePost>();

            if (body != null) bodyScale = body.localScale;
            if (loaded != null) loadedRest = loaded.localPosition;
            if (lever != null) leverRest = lever.localPosition;

            eyeScales = new Vector3[eyes != null ? eyes.Length : 0];
            for (int i = 0; i < eyeScales.Length; i++)
                if (eyes[i] != null) eyeScales[i] = eyes[i].localScale;

            swayRest = new Quaternion[swaying != null ? swaying.Length : 0];
            for (int i = 0; i < swayRest.Length; i++)
                if (swaying[i] != null) swayRest[i] = swaying[i].localRotation;

            // From where it stands, so two side by side do not breathe as one.
            Vector3 p = transform.position;
            breathOffset = Mathf.Repeat(p.x * 0.31f + p.z * 0.17f, 1f);
            nextBlink = 1f + breathOffset * 2f;
        }

        void OnEnable()
        {
            if (post != null) post.Fired += HandleFired;
        }

        void OnDisable()
        {
            if (post != null) post.Fired -= HandleFired;
            sinceShot = float.MaxValue;
            blinkLeft = 0f;
            Pose(0f, 1f);
        }

        void HandleFired(Vector3 from, Vector3 to, DefenseShot shot) => sinceShot = 0f;

        void Update()
        {
            DefenseMotionProfile motion = post != null ? post.Motion : null;
            if (motion == null) return;

            float dt = Time.deltaTime;
            if (sinceShot < float.MaxValue) sinceShot += dt;

            float stretch = motion.Breath(Time.time, breathOffset) + motion.ShotStretch(sinceShot);
            Pose(stretch, Blink(motion, dt));

            Reload(motion);
            Pump(motion);
            Sway(motion);
        }

        /// <summary>Taller by <paramref name="stretch"/> (a fraction; negative is squashed), thinner to keep its bulk; eyes open by <paramref name="open"/>.</summary>
        void Pose(float stretch, float open)
        {
            if (body != null)
            {
                float wide = 1f - stretch * 0.5f;
                body.localScale = Vector3.Scale(bodyScale, new Vector3(wide, 1f + stretch, wide));
            }

            if (eyes == null) return;

            for (int i = 0; i < eyes.Length && i < eyeScales.Length; i++)
                if (eyes[i] != null) eyes[i].localScale = Vector3.Scale(eyeScales[i], new Vector3(1f, Mathf.Max(0.08f, open), 1f));
        }

        /// <summary>How open the eyes are, 0 to 1: a quick shut and open every few seconds, never on a beat.</summary>
        float Blink(DefenseMotionProfile motion, float dt)
        {
            if (motion.blinkSeconds <= 0f) return 1f;

            if (blinkLeft > 0f)
            {
                blinkLeft = Mathf.Max(0f, blinkLeft - dt);
                return 1f - Mathf.Sin((1f - blinkLeft / BlinkSeconds) * Mathf.PI);
            }

            nextBlink -= dt;
            if (nextBlink > 0f) return 1f;

            blinkLeft = BlinkSeconds;
            nextBlink = motion.blinkSeconds * Random.Range(0.5f, 1.5f);
            return 1f;
        }

        /// <summary>The fired round is gone until the weapon is part way to its next shot, then rises back into its slot.</summary>
        void Reload(DefenseMotionProfile motion)
        {
            if (loaded == null) return;

            WeaponDefinition weapon = post.Unit?.Weapon;
            float showAt = weapon != null ? weapon.secondsBetweenShots * motion.reloadShowsAt : 0f;

            float since = sinceShot - showAt;
            bool shown = sinceShot == float.MaxValue || since >= 0f;
            if (loaded.gameObject.activeSelf != shown) loaded.gameObject.SetActive(shown);
            if (!shown) return;

            float rise = sinceShot == float.MaxValue ? 1f : Mathf.Clamp01(since / ReloadRiseSeconds);
            loaded.localPosition = loadedRest + Vector3.down * (LoadedDrop * (1f - Mathf.SmoothStep(0f, 1f, rise)));
        }

        /// <summary>The lever slammed down on the shot and let back up over the settle.</summary>
        void Pump(DefenseMotionProfile motion)
        {
            if (lever == null) return;

            float t = sinceShot == float.MaxValue ? 1f : Mathf.Clamp01(sinceShot / motion.shotSettleSeconds);
            lever.localPosition = leverRest + Vector3.down * (leverTravel * (1f - t) * (1f - t));
        }

        void Sway(DefenseMotionProfile motion)
        {
            if (swaying == null || motion.swayDegrees <= 0f) return;

            float time = Time.time;
            for (int i = 0; i < swaying.Length && i < swayRest.Length; i++)
            {
                if (swaying[i] == null) continue;

                float angle = Mathf.Sin(time * 1.7f + i * 2.1f + breathOffset * 6f) * motion.swayDegrees;
                swaying[i].localRotation = swayRest[i] * Quaternion.Euler(angle, 0f, angle * 0.5f);
            }
        }

        /// <summary>Editor wiring, for the prefab generator.</summary>
        public void Configure(DefensePost owner, Transform squash, Transform[] blinkers, Transform round, Transform pump, float travel,
                              Transform[] leaves)
        {
            post = owner;
            body = squash;
            eyes = blinkers;
            loaded = round;
            lever = pump;
            leverTravel = travel;
            swaying = leaves;
        }
    }
}
