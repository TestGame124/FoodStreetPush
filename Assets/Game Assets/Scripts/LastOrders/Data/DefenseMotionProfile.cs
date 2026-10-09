using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// How a defense moves when it watches and shoots: how fast its aim swings round, the sweep it
    /// makes while nothing is in sight, and how hard each shot kicks it back.
    ///
    /// None of this is a rule - the shot lands on the tick the weapon is ready, whichever way the
    /// gun happens to be pointing - so it lives apart from <see cref="DefenseDefinition"/>, on the
    /// model that moves. A post without one keeps its own turn speed and stands still at rest; a
    /// turret with one sweeps the approach and bucks on every shot. Several turret models can share
    /// one profile, and tuning it retunes them all.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Defense/Motion Profile", fileName = "DefenseMotion_")]
    public sealed class DefenseMotionProfile : ScriptableObject
    {
        [Header("Aim")]
        [Tooltip("Degrees per second the aim swings round to follow what it is tracking.")]
        [Min(1f)] public float turnSpeed = 240f;

        [Tooltip("Where on an enemy a shot lands, metres above its feet.")]
        [Min(0f)] public float targetHeight = 0.9f;

        [Header("Watching")]
        [Tooltip("Degrees either side of the resting direction the aim sweeps while nothing is in " +
                 "sight. Zero: it holds still at rest.")]
        [Range(0f, 180f)] public float idleScanDegrees = 40f;

        [Tooltip("Seconds for one full sweep, out to one side, across to the other and back.")]
        [Min(0.5f)] public float idleScanSeconds = 7f;

        [Header("Firing")]
        [Tooltip("Seconds the tracer and the muzzle flash show for, per shot.")]
        [Min(0.01f)] public float flashSeconds = 0.05f;

        [Tooltip("Metres the barrels kick back on a shot.")]
        [Min(0f)] public float recoilDistance = 0.09f;

        [Tooltip("Seconds the barrels take to run forward again. Keep it under the weapon's time " +
                 "between shots, or the barrels never get home before the next one.")]
        [Min(0.01f)] public float recoilReturnSeconds = 0.12f;

        [Tooltip("Spent cases thrown out on each shot. Zero for a weapon that throws none.")]
        [Min(0)] public int casingsPerShot = 1;

        [Header("Thrown shots")]
        [Tooltip("Metres a thrown shot rises above the straight line to its target, at the top of " +
                 "its arc. Only for a weapon with a projectile speed.")]
        [Min(0f)] public float lobHeight = 2.2f;

        [Tooltip("Degrees per second a thrown shot tumbles end over end on the way.")]
        public float projectileSpin = 540f;

        [Tooltip("The axis, in the shot's own space, it spins about: X tumbles it end over end (a " +
                 "slice of toast); Z spirals it along its flight (a cob, nose first).")]
        public Vector3 projectileSpinAxis = Vector3.right;

        [Tooltip("Points the shot along its arc - nose up as it climbs, down as it drops - rather " +
                 "than level the whole way. For something long thrown point first.")]
        public bool projectileFacesFlight;

        [Header("Living")]
        [Tooltip("How much it swells and settles as it breathes, as a fraction of its height. Zero " +
                 "for anything that is not alive.")]
        [Range(0f, 0.2f)] public float breatheAmount;

        [Tooltip("Seconds for one breath.")]
        [Min(0.2f)] public float breatheSeconds = 1.4f;

        [Tooltip("How far it squashes down as it fires, as a fraction of its height, before it " +
                 "springs back up past its size and settles.")]
        [Range(0f, 0.5f)] public float shotSquash;

        [Tooltip("Seconds from the squash on a shot to settled again.")]
        [Min(0.05f)] public float shotSettleSeconds = 0.45f;

        [Tooltip("Seconds between blinks, on average. Zero: it never blinks.")]
        [Min(0f)] public float blinkSeconds = 3.5f;

        [Tooltip("Degrees its leaves and tendrils sway either way.")]
        [Range(0f, 45f)] public float swayDegrees = 7f;

        [Tooltip("The fraction of the weapon's time between shots after which the next round shows " +
                 "loaded again - the toast back in the slots.")]
        [Range(0f, 1f)] public float reloadShowsAt = 0.45f;

        /// <summary>
        /// Degrees off the resting direction the idle sweep is at <paramref name="seconds"/>: an
        /// even swing from one side to the other, slowing at each end as a traversing gun does.
        /// <paramref name="offset"/> is a fraction of a sweep, so two turrets side by side are not
        /// in step.
        /// </summary>
        public float ScanYaw(float seconds, float offset = 0f)
        {
            if (idleScanDegrees <= 0f) return 0f;

            float phase = (seconds / idleScanSeconds + offset) * Mathf.PI * 2f;
            return Mathf.Sin(phase) * idleScanDegrees;
        }

        /// <summary>
        /// How far back the barrels sit, 0 to <see cref="recoilDistance"/>, <paramref name="sinceShot"/>
        /// seconds after a shot: thrown fully back at once, then run forward, easing in to rest.
        /// </summary>
        public float RecoilOffset(float sinceShot)
        {
            if (recoilDistance <= 0f || sinceShot < 0f || sinceShot >= recoilReturnSeconds) return 0f;

            float left = 1f - sinceShot / recoilReturnSeconds;
            return recoilDistance * left * left;
        }

        /// <summary>
        /// How much taller than at rest it stands, as a fraction (negative is squashed),
        /// <paramref name="sinceShot"/> seconds after a shot: down to <see cref="shotSquash"/> in the
        /// first fifth, then a spring back up past rest that dies away. Zero once settled.
        /// </summary>
        public float ShotStretch(float sinceShot)
        {
            if (shotSquash <= 0f || sinceShot < 0f || sinceShot >= shotSettleSeconds) return 0f;

            float t = sinceShot / shotSettleSeconds;
            const float Down = 0.2f;
            if (t < Down) return -shotSquash * Mathf.Sin(t / Down * Mathf.PI * 0.5f);

            float u = (t - Down) / (1f - Down);
            return -shotSquash * Mathf.Cos(u * Mathf.PI * 2.5f) * (1f - u) * (1f - u);
        }

        /// <summary>The breath at <paramref name="seconds"/>, as a fraction of its height: a gentle swell and settle.</summary>
        public float Breath(float seconds, float offset = 0f) =>
            breatheAmount <= 0f ? 0f : Mathf.Sin((seconds / breatheSeconds + offset) * Mathf.PI * 2f) * breatheAmount;

        /// <summary>
        /// Where a thrown shot is, a fraction <paramref name="t"/> of the way from <paramref name="from"/>
        /// to <paramref name="to"/>: along the straight line between them, lifted by an arc that
        /// peaks at <see cref="lobHeight"/> halfway.
        /// </summary>
        public Vector3 LobPoint(Vector3 from, Vector3 to, float t)
        {
            t = Mathf.Clamp01(t);
            return Vector3.Lerp(from, to, t) + Vector3.up * (4f * lobHeight * t * (1f - t));
        }
    }
}
