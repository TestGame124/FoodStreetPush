using UnityEngine;

namespace LastOrders.Defense
{
    /// <summary>
    /// A throwing arm - the cabbage catapult's - that whips forward on every shot and is wound back
    /// down to cocked before the next.
    ///
    /// An add-on to a <see cref="DefensePost"/>, like <see cref="DefenseRecoil"/>: it listens to the
    /// post's shots and nothing else. The arm's pivot turns about its X, upright at zero. At rest it lies cocked at
    /// <see cref="cockedDegrees"/>; a shot throws it to <see cref="thrownDegrees"/> in
    /// <see cref="throwSeconds"/>, overshooting a little against its stop; it then winds back down,
    /// easing in, and is home by the time the post's reload shows (the weapon's time between shots
    /// times <see cref="Data.DefenseMotionProfile.reloadShowsAt"/>) - the moment the next round
    /// appears in its cup.
    /// </summary>
    public sealed class DefenseSwingArm : MonoBehaviour
    {
        [SerializeField] DefensePost post;

        [Tooltip("Turned about its own X.")]
        [SerializeField] Transform arm;

        [Tooltip("Degrees about X at rest, loaded and wound down.")]
        [SerializeField] float cockedDegrees = 60f;

        [Tooltip("Degrees about X at the top of the throw, against the crossbar.")]
        [SerializeField] float thrownDegrees = -20f;

        [Tooltip("Seconds from cocked to thrown.")]
        [Min(0.02f)] [SerializeField] float throwSeconds = 0.12f;

        [Tooltip("Degrees it bounces back off the crossbar before it is wound down.")]
        [SerializeField] float bounceDegrees = 9f;

        float sinceShot = float.MaxValue;

        void Awake()
        {
            if (post == null) post = GetComponentInParent<DefensePost>();
            Pose(cockedDegrees);
        }

        void OnEnable()
        {
            if (post != null) post.Fired += HandleFired;
        }

        void OnDisable()
        {
            if (post != null) post.Fired -= HandleFired;

            sinceShot = float.MaxValue;
            Pose(cockedDegrees);
        }

        void HandleFired(Vector3 from, Vector3 to, DefenseShot shot) => sinceShot = 0f;

        void Update()
        {
            if (arm == null || sinceShot == float.MaxValue) return;

            sinceShot += Time.deltaTime;
            Pose(Angle(sinceShot));
        }

        /// <summary>The arm's angle <paramref name="t"/> seconds after a shot.</summary>
        float Angle(float t)
        {
            if (t < throwSeconds)
            {
                float k = t / throwSeconds;
                return Mathf.LerpUnclamped(cockedDegrees, thrownDegrees, k * k);
            }

            float between = post != null && post.Unit != null && post.Unit.Weapon != null ? post.Unit.Weapon.secondsBetweenShots : 1f;
            float share = post != null && post.Motion != null ? post.Motion.reloadShowsAt : 0.5f;
            float windEnd = Mathf.Max(throwSeconds + 0.1f, between * share);

            // A quick bounce off the crossbar, then wound down steadily.
            float bounce = throwSeconds * 1.6f;
            if (t < throwSeconds + bounce)
            {
                float k = (t - throwSeconds) / bounce;
                return thrownDegrees + Mathf.Sin(k * Mathf.PI) * bounceDegrees * Mathf.Sign(cockedDegrees - thrownDegrees);
            }

            float wind = Mathf.Clamp01((t - throwSeconds - bounce) / Mathf.Max(0.05f, windEnd - throwSeconds - bounce));
            if (wind >= 1f) sinceShot = float.MaxValue;

            float eased = wind * wind * (3f - 2f * wind);
            return Mathf.Lerp(thrownDegrees, cockedDegrees, eased);
        }

        void Pose(float degrees)
        {
            if (arm != null) arm.localRotation = Quaternion.Euler(degrees, 0f, 0f);
        }

        /// <summary>
        /// Editor wiring, for the prefab generator: <paramref name="swinging"/> is a pivot whose X at
        /// zero stands the arm upright. It is left posed cocked.
        /// </summary>
        public void Configure(DefensePost owner, Transform swinging, float cocked, float thrown, float seconds)
        {
            post = owner;
            arm = swinging;
            cockedDegrees = cocked;
            thrownDegrees = thrown;
            throwSeconds = seconds;
            Pose(cockedDegrees);
        }
    }
}
