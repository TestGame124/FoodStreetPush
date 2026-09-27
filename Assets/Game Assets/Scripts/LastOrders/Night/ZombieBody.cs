using UnityEngine;

namespace LastOrders.Night
{
    /// <summary>
    /// Makes one of the project's people read as a zombie: arms held out in front, a forward
    /// stoop, a lolling head, and a walk slowed to a shamble.
    ///
    /// All of it is done on top of the ordinary walk cycle, in LateUpdate, after the animator
    /// has posed the skeleton. There is no zombie clip in the project; bending the bones the walk
    /// has already placed is what turns a farmer walking into something the player knows to
    /// shoot from a single glance - which is the only glance they get at forty pixels tall.
    ///
    /// The arms are aimed in world space rather than by fixed local angles, so the pose holds on
    /// any of the characters whatever their bones' own axes happen to be.
    ///
    /// Several bodies can sit under one prefab and one is picked each time it is enabled, so a
    /// pool of one prefab still turns out a crowd of different people.
    /// </summary>
    [DefaultExecutionOrder(1300)]
    public sealed class ZombieBody : MonoBehaviour
    {
        static readonly int MoveHash = Animator.StringToHash("Move");

        [Tooltip("Alternative bodies. One is shown per life; the rest stay hidden.")]
        [SerializeField] GameObject[] variants;

        [Tooltip("Metres per second the walk clip was made for. The clip is slowed to match how " +
                 "fast the body actually moves, which is most of what a shamble is.")]
        [SerializeField] float clipSpeed = 1.6f;

        [Tooltip("Slowest the clip plays while moving, so a very slow zombie still visibly steps.")]
        [SerializeField] float minimumClipRate = 0.45f;

        [Tooltip("How far the arms are pulled from the walk's swing to straight ahead. 1 is rigid.")]
        [Range(0f, 1f)] [SerializeField] float armReach = 0.85f;

        [Tooltip("How far below level the arms point.")]
        [SerializeField] float armDroop = 0.2f;

        [Tooltip("Degrees the upper body stoops forward.")]
        [SerializeField] float stoop = 14f;

        [Tooltip("Degrees the head lolls side to side.")]
        [SerializeField] float headLoll = 12f;

        [Tooltip("Seconds one blow on a wall takes: arms up, and down on it.")]
        [SerializeField] float poundSeconds = 0.45f;

        [Tooltip("How high the arms go before they come down, above their usual reach.")]
        [SerializeField] float poundLift = 1.3f;

        [Tooltip("Degrees the body lunges forward as the arms come down.")]
        [SerializeField] float poundLunge = 16f;

        Animator animator;
        Transform spine;
        Transform head;
        Transform shoulderL, elbowL, handL;
        Transform shoulderR, elbowR, handR;

        Vector3 lastPosition;
        float phase;
        float speed;
        float poundLeft;

        /// <summary>Metres per second over the last frame. What the walk is matched to.</summary>
        public float Speed => speed;

        /// <summary>One blow on whatever is in front of it: both arms raised and brought down.</summary>
        public void Pound() => poundLeft = poundSeconds;

        void OnEnable()
        {
            ChooseVariant();

            lastPosition = transform.position;
            speed = 0f;
            poundLeft = 0f;

            // Out of step with each other, so a group does not sway as one.
            phase = Random.value * 10f;
        }

        void ChooseVariant()
        {
            GameObject shown = null;

            if (variants != null && variants.Length > 0)
            {
                int pick = Random.Range(0, variants.Length);
                for (int i = 0; i < variants.Length; i++)
                {
                    if (variants[i] == null) continue;

                    bool active = i == pick;
                    if (variants[i].activeSelf != active) variants[i].SetActive(active);
                    if (active) shown = variants[i];
                }
            }

            Transform body = shown != null ? shown.transform : transform;

            animator = body.GetComponentInChildren<Animator>();

            // The pose below bends bones the animator has just written. A culled animator stops
            // writing them, and every frame's bend would then land on the last one - a stoop that
            // keeps stooping until the spine is spinning. Always animating keeps the base fresh.
            if (animator != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            spine = Find(body, "Spine_03", "Spine_02", "Spine");
            head = Find(body, "Head");
            shoulderL = Find(body, "Shoulder_L", "LeftArm", "UpperArm_L");
            elbowL = Find(body, "Elbow_L", "LeftForeArm", "LowerArm_L");
            handL = Find(body, "Hand_L", "LeftHand");
            shoulderR = Find(body, "Shoulder_R", "RightArm", "UpperArm_R");
            elbowR = Find(body, "Elbow_R", "RightForeArm", "LowerArm_R");
            handR = Find(body, "Hand_R", "RightHand");
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 position = transform.position;
            Vector3 moved = position - lastPosition;
            moved.y = 0f;
            lastPosition = position;

            // Smoothed, because steering jitters and a clip that changes pace every frame reads
            // as a stutter rather than a gait.
            speed = Mathf.Lerp(speed, moved.magnitude / dt, 1f - Mathf.Exp(-8f * dt));
            phase += dt * (1.2f + speed);

            if (poundLeft > 0f) poundLeft = Mathf.Max(0f, poundLeft - dt);

            Animate();
            Pose();
        }

        /// <summary>
        /// 0 to 1 and back over a blow: a slow lift, then a fast drop. Zero between blows.
        /// </summary>
        float PoundCurve()
        {
            if (poundLeft <= 0f || poundSeconds <= 0f) return 0f;

            float t = 1f - poundLeft / poundSeconds;

            // Two-thirds of the time going up, a third coming down on it.
            return t < 0.66f ? Mathf.SmoothStep(0f, 1f, t / 0.66f) : 1f - (t - 0.66f) / 0.34f;
        }

        void Animate()
        {
            if (animator == null) return;

            bool moving = speed > 0.05f;
            animator.SetFloat(MoveHash, moving ? 1f : 0f);
            animator.speed = moving ? Mathf.Clamp(speed / Mathf.Max(0.1f, clipSpeed), minimumClipRate, 1.2f) : 1f;
        }

        void Pose()
        {
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            // Mid-blow the arms are up and the body leans back; on the way down it throws itself in.
            float pound = PoundCurve();
            bool dropping = poundLeft > 0f && poundLeft < poundSeconds * 0.34f;
            float lunge = dropping
                ? poundLunge * Mathf.Sin(Mathf.PI * (1f - pound))
                : -poundLunge * 0.35f * Mathf.Sin(Mathf.PI * pound);

            if (spine != null)
                spine.rotation = Quaternion.AngleAxis(stoop + lunge, right) * spine.rotation;

            if (head != null)
            {
                float loll = Mathf.Sin(phase * 0.9f) * headLoll;
                head.rotation = Quaternion.AngleAxis(loll, forward) * Quaternion.AngleAxis(8f, right) * head.rotation;
            }

            // Each arm reaches a little across and bobs out of time with the other.
            float bob = Mathf.Sin(phase * 1.7f) * 0.08f;
            Vector3 lift = Vector3.up * (pound * poundLift);

            Reach(shoulderL, elbowL, handL, forward - right * 0.08f + Vector3.down * (armDroop + bob) + lift);
            Reach(shoulderR, elbowR, handR, forward + right * 0.08f + Vector3.down * (armDroop - bob) + lift);
        }

        /// <summary>Turns the upper arm, then the forearm, to point along <paramref name="aim"/>.</summary>
        void Reach(Transform shoulder, Transform elbow, Transform hand, Vector3 aim)
        {
            if (shoulder == null || elbow == null) return;

            aim.Normalize();

            Vector3 upper = elbow.position - shoulder.position;
            if (upper.sqrMagnitude > 1e-6f)
            {
                Quaternion turn = Quaternion.FromToRotation(upper, aim);
                shoulder.rotation = Quaternion.Slerp(Quaternion.identity, turn, armReach) * shoulder.rotation;
            }

            if (hand == null) return;

            Vector3 lower = hand.position - elbow.position;
            if (lower.sqrMagnitude > 1e-6f)
            {
                Quaternion turn = Quaternion.FromToRotation(lower, aim);
                elbow.rotation = Quaternion.Slerp(Quaternion.identity, turn, armReach) * elbow.rotation;
            }
        }

        static Transform Find(Transform root, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform found = FindDeep(root, names[i]);
                if (found != null) return found;
            }

            return null;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>Editor wiring, for the prefab generator.</summary>
        public void Configure(GameObject[] bodies) => variants = bodies;
    }
}
