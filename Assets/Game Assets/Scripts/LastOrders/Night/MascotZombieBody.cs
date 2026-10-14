using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Night
{
    /// <summary>
    /// Brings a zombie made of parts to life - one with a model of its own rather than one of the
    /// project's people: the Hot Dog Zombie in its mascot costume.
    ///
    /// No animator and no clips. It watches how far the agent moved this frame and steps to match:
    /// legs swinging, rolling onto each foot, bobbing up between steps, arms flapping out in front,
    /// whatever is loose on top swinging back against the roll. Standing, it sways. A hit squashes
    /// it flat and it boings back up wobbling; a blow on a wall swings both arms up and brings them
    /// down. Googly pupils rattle about on a spring. Killed, it squashes, keels over onto its side
    /// with a bounce, arms and sausage flopping, and lies there. Every number comes from a
    /// <see cref="ZombieGaitProfile"/>; every part is optional.
    ///
    /// Pooled like every enemy, so it starts clean each time it is enabled.
    /// </summary>
    [DefaultExecutionOrder(1300)]
    public sealed class MascotZombieBody : MonoBehaviour, IEnemyBody
    {
        [SerializeField] ZombieGaitProfile gait;

        [Tooltip("The whole figure, pivoting at its feet: rolls, hops and squashes.")]
        [SerializeField] Transform model;

        [Tooltip("Optional. Loose on top, swinging back against the roll: the sausage in its bun.")]
        [SerializeField] Transform top;

        [Tooltip("Optional. Pivoting at the shoulder, pointing forward at rest.")]
        [SerializeField] Transform armLeft, armRight;

        [Tooltip("Optional. Pivoting at the hip, hanging straight down at rest.")]
        [SerializeField] Transform legLeft, legRight;

        [Tooltip("Optional. Googly pupils, rattling about in their own X and Y.")]
        [SerializeField] Transform[] googly;

        Vector3 modelPosition, modelScale;
        Quaternion modelRotation, topRotation, armLeftRotation, armRightRotation, legLeftRotation, legRightRotation;
        Vector3[] googlyRest;
        Vector2[] googlyOffset, googlyVelocity;

        Vector3 lastPosition;
        float speed, moving, phase;
        float sinceHit = float.MaxValue, sinceBlow = float.MaxValue;
        bool captured;

        /// <summary>Seconds since the killing blow; below zero while alive.</summary>
        float sinceDeath = -1f;

        /// <summary>Which way it keels over: 1 onto its left side, -1 onto its right.</summary>
        float fallSide = 1f;

        /// <summary>Seconds from the killing blow until it lies still.</summary>
        const float DeathSeconds = 0.95f;

        /// <summary>Metres the figure is lifted as it goes over, so it lies on its side rather than half in the ground.</summary>
        const float LyingLift = 0.2f;

        /// <summary>Metres per second over the last few frames.</summary>
        public float Speed => speed;

        public void Pound()
        {
            if (sinceDeath < 0f) sinceBlow = 0f;
        }

        public void Hurt(float amount)
        {
            if (sinceDeath >= 0f) return;

            sinceHit = 0f;
            Kick(gait != null ? gait.googlyKick * 2f : 1f);
        }

        /// <summary>Squashes, keels over onto its side with a bounce, and lies there.</summary>
        public float Die()
        {
            if (gait == null || model == null) return 0f;
            if (sinceDeath >= 0f) return DeathSeconds;

            sinceDeath = 0f;
            sinceHit = 0f;
            fallSide = Random.value < 0.5f ? 1f : -1f;
            Kick(gait.googlyKick * 3f);
            return DeathSeconds;
        }

        void Awake() => Capture();

        /// <summary>The rest pose, read once from the prefab as built.</summary>
        void Capture()
        {
            if (captured) return;
            captured = true;

            if (model != null)
            {
                modelPosition = model.localPosition;
                modelRotation = model.localRotation;
                modelScale = model.localScale;
            }

            if (top != null) topRotation = top.localRotation;
            if (armLeft != null) armLeftRotation = armLeft.localRotation;
            if (armRight != null) armRightRotation = armRight.localRotation;
            if (legLeft != null) legLeftRotation = legLeft.localRotation;
            if (legRight != null) legRightRotation = legRight.localRotation;

            int count = googly != null ? googly.Length : 0;
            googlyRest = new Vector3[count];
            googlyOffset = new Vector2[count];
            googlyVelocity = new Vector2[count];
            for (int i = 0; i < count; i++)
                if (googly[i] != null) googlyRest[i] = googly[i].localPosition;
        }

        void OnEnable()
        {
            Capture();

            lastPosition = transform.position;
            speed = 0f;
            moving = 0f;
            sinceHit = float.MaxValue;
            sinceBlow = float.MaxValue;
            sinceDeath = -1f;

            // Out of step with each other, so a crowd of them does not waddle as one.
            phase = Random.value * Mathf.PI * 2f;

            for (int i = 0; i < googlyOffset.Length; i++)
            {
                googlyOffset[i] = Vector2.zero;
                googlyVelocity[i] = Vector2.zero;
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || gait == null) return;

            Vector3 position = transform.position;
            Vector3 moved = position - lastPosition;
            moved.y = 0f;
            lastPosition = position;

            // Smoothed: steering jitters, and a stride that changes pace every frame reads as a stutter.
            float distance = moved.magnitude;
            speed = Mathf.Lerp(speed, distance / dt, 1f - Mathf.Exp(-8f * dt));
            moving = Mathf.MoveTowards(moving, speed > 0.08f ? 1f : 0f, dt * 3f);

            // Half a turn per step: one foot, then the other. A footfall is where the sine crosses zero.
            float before = Mathf.Sin(phase);
            phase += distance / gait.stepMetres * Mathf.PI;
            if (moving > 0.5f && Mathf.Sign(before) != Mathf.Sign(Mathf.Sin(phase))) Kick(gait.googlyKick);

            if (sinceHit < float.MaxValue) sinceHit += dt;
            if (sinceBlow < float.MaxValue) sinceBlow += dt;

            if (sinceDeath >= 0f)
            {
                float was = sinceDeath;
                sinceDeath += dt;

                // The thud on the ground knocks the eyes about once more.
                if (was < 0.42f && sinceDeath >= 0.42f) Kick(gait.googlyKick * 2.5f);

                Fallen(dt);
                return;
            }

            Pose(dt);
        }

        /// <summary>
        /// Dying: squashed by the blow for a moment, then over onto its side - quick at first, a
        /// bounce where it lands - arms flung up and flopping down, the sausage lolling out of the bun.
        /// </summary>
        void Fallen(float dt)
        {
            float t = sinceDeath;

            // Over by 0.42 s, a bounce up off the ground and down again by 0.7 s.
            float over = t < 0.42f ? EaseIn(Mathf.Clamp01((t - 0.08f) / 0.34f)) : 1f;
            float bounce = t > 0.42f && t < 0.7f ? Mathf.Sin((t - 0.42f) / 0.28f * Mathf.PI) * 0.12f : 0f;
            float roll = (over - bounce) * 86f * fallSide;

            // Squashed by the blow, stretched as it goes over, flattened where it lands, settling.
            float squash = t < 0.1f ? -0.22f * Mathf.Sin(t / 0.1f * Mathf.PI * 0.5f)
                         : t < 0.42f ? Mathf.Lerp(-0.22f, 0.08f, (t - 0.1f) / 0.32f)
                         : 0.08f - 0.24f * Mathf.Exp(-(t - 0.42f) * 9f) * Mathf.Cos((t - 0.42f) * 22f);
            squash = Mathf.Clamp(squash, -0.3f, 0.2f);

            if (model != null)
            {
                model.localPosition = modelPosition + Vector3.up * (LyingLift * Mathf.Abs(Mathf.Sin(roll * Mathf.Deg2Rad)));
                model.localRotation = modelRotation * Quaternion.Euler(-8f * over, 0f, roll);

                float wide = 1f - squash * 0.5f;
                model.localScale = Vector3.Scale(modelScale, new Vector3(wide, 1f + squash, wide));
            }

            // The arms fly up as it goes and flop back down after the bounce.
            float flail = t < 0.42f ? Mathf.Sin(Mathf.Clamp01(t / 0.42f) * Mathf.PI * 0.5f)
                                    : Mathf.Lerp(1f, 0.35f, Mathf.Clamp01((t - 0.42f) / 0.4f));
            if (armLeft != null) armLeft.localRotation = armLeftRotation * Quaternion.Euler(-70f * flail, 0f, 25f * flail);
            if (armRight != null) armRight.localRotation = armRightRotation * Quaternion.Euler(-55f * flail, 0f, -30f * flail);

            float kick = Mathf.Sin(Mathf.Clamp01(t / 0.6f) * Mathf.PI) * 28f;
            if (legLeft != null) legLeft.localRotation = legLeftRotation * Quaternion.Euler(kick, 0f, 0f);
            if (legRight != null) legRight.localRotation = legRightRotation * Quaternion.Euler(-kick * 0.6f, 0f, 0f);

            // The sausage keeps going over a little further than the bun, and wobbles back.
            float loll = 18f * over + 14f * Mathf.Exp(-Mathf.Max(0f, t - 0.42f) * 6f) * Mathf.Sin(Mathf.Max(0f, t - 0.42f) * 18f);
            if (top != null) top.localRotation = topRotation * Quaternion.Euler(0f, 0f, loll * fallSide);

            Rattle(roll * 0.2f, dt);
        }

        static float EaseIn(float t) => t * t * (2.2f - 1.2f * t);

        void Pose(float dt)
        {
            float roll = gait.Roll(phase, moving, Time.time);
            float stretch = gait.HurtStretch(sinceHit);
            float lift = gait.PoundLift(sinceBlow);

            if (model != null)
            {
                // Thrown forward as the blow comes down, rocked back as the arms go up.
                bool dropping = sinceBlow < gait.poundSeconds && sinceBlow > gait.poundSeconds * 0.66f;
                float lean = dropping ? 14f * lift : -6f * lift;

                model.localPosition = modelPosition + Vector3.up * gait.Hop(phase, moving);
                model.localRotation = modelRotation * Quaternion.Euler(lean, 0f, roll);

                float wide = 1f - stretch * 0.5f;
                model.localScale = Vector3.Scale(modelScale, new Vector3(wide, 1f + stretch, wide));
            }

            if (top != null)
                top.localRotation = topRotation * Quaternion.Euler(0f, 0f, -roll * gait.topWobble + gait.HurtWobble(sinceHit));

            float swing = gait.LegSwing(phase, moving);
            if (legLeft != null) legLeft.localRotation = legLeftRotation * Quaternion.Euler(swing, 0f, 0f);
            if (legRight != null) legRight.localRotation = legRightRotation * Quaternion.Euler(-swing, 0f, 0f);

            // A negative turn about X lifts an arm that points forward. Flapping out of step, and both up for a blow.
            float flap = Mathf.Sin(phase * 2f) * gait.armFlapDegrees * Mathf.Max(moving, 0.3f);
            float up = lift * gait.poundLiftDegrees;
            if (armLeft != null) armLeft.localRotation = armLeftRotation * Quaternion.Euler(-(flap + up), 0f, 0f);
            if (armRight != null) armRight.localRotation = armRightRotation * Quaternion.Euler(-(-flap + up), 0f, 0f);

            Rattle(roll, dt);
        }

        /// <summary>Each googly pupil on a spring, hanging a little low and thrown about by the roll and every knock.</summary>
        void Rattle(float roll, float dt)
        {
            if (googly == null) return;

            float range = gait.googlyRange;
            var hang = new Vector2(-roll * Mathf.Deg2Rad * range * 3f, -range * 0.45f);

            for (int i = 0; i < googly.Length && i < googlyOffset.Length; i++)
            {
                if (googly[i] == null) continue;

                Vector2 v = googlyVelocity[i];
                Vector2 o = googlyOffset[i];
                v += ((hang - o) * gait.googlyStiffness - v * gait.googlyDamping) * dt;
                o += v * dt;

                // Stops at the rim of the eye and loses most of its speed there.
                if (o.magnitude > range)
                {
                    o = o.normalized * range;
                    v *= -0.3f;
                }

                googlyVelocity[i] = v;
                googlyOffset[i] = o;
                googly[i].localPosition = googlyRest[i] + new Vector3(o.x, o.y, 0f);
            }
        }

        void Kick(float strength)
        {
            if (googlyVelocity == null) return;

            for (int i = 0; i < googlyVelocity.Length; i++)
                googlyVelocity[i] += Random.insideUnitCircle * strength;
        }

        /// <summary>Editor wiring, for the prefab generator.</summary>
        public void Configure(ZombieGaitProfile profile, Transform figure, Transform loose, Transform leftArm, Transform rightArm,
                              Transform leftLeg, Transform rightLeg, Transform[] pupils)
        {
            gait = profile;
            model = figure;
            top = loose;
            armLeft = leftArm;
            armRight = rightArm;
            legLeft = leftLeg;
            legRight = rightLeg;
            googly = pupils;
        }
    }
}
