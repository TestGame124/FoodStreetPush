using UnityEngine;

namespace LastOrders.Night
{
    /// <summary>
    /// Makes one of the project's people read as a zombie.
    ///
    /// With the zombie controller on the rig (built by 'Build Zombie Animations') it plays the
    /// zombies' own clips: a hunched idle that sways and twitches, a limping shamble - arms out in
    /// front, or hanging and dragging - a lurching lope for the fast ones, a battering loop whose
    /// blows land on the beat the agent hits on, a flinch for every hit and a fall to the ground
    /// when it dies. The walk is played at the pace the body actually moves, so the feet do not
    /// slide, and each life picks its own style and starts out of step with the rest of the crowd.
    ///
    /// Without that controller it falls back to the old way: the ordinary walk cycle, slowed to a
    /// shamble and bent in LateUpdate - arms held out, a stoop, a lolling head.
    ///
    /// Several bodies can sit under one prefab and one is picked each time it is enabled, so a
    /// pool of one prefab still turns out a crowd of different people.
    /// </summary>
    [DefaultExecutionOrder(1300)]
    public sealed class ZombieBody : MonoBehaviour, IEnemyBody
    {
        static readonly int MoveHash = Animator.StringToHash(ZombieMotion.MoveParam);
        static readonly int StyleHash = Animator.StringToHash(ZombieMotion.StyleParam);
        static readonly int WalkRateHash = Animator.StringToHash(ZombieMotion.WalkRateParam);
        static readonly int BatterRateHash = Animator.StringToHash(ZombieMotion.BatterRateParam);

        static readonly int LocomotionHash = Animator.StringToHash(ZombieMotion.LocomotionState);
        static readonly int EmptyHash = Animator.StringToHash(ZombieMotion.EmptyState);
        static readonly int SlamHash = Animator.StringToHash(ZombieMotion.SlamState);
        static readonly int ClawHash = Animator.StringToHash(ZombieMotion.ClawState);
        static readonly int DeathBackHash = Animator.StringToHash(ZombieMotion.DeathBackState);
        static readonly int DeathForwardHash = Animator.StringToHash(ZombieMotion.DeathForwardState);

        static readonly int[] HitHashes =
        {
            Animator.StringToHash(ZombieMotion.HitFrontState),
            Animator.StringToHash(ZombieMotion.HitLeftState),
            Animator.StringToHash(ZombieMotion.HitRightState),
        };

        [Tooltip("Alternative bodies. One is shown per life; the rest stay hidden.")]
        [SerializeField] GameObject[] variants;

        [Tooltip("Seconds the walk/idle blend takes to follow the speed.")]
        [SerializeField] float moveDamping = 0.12f;

        [Tooltip("Shortest gap between two flinches. Hits closer together than this ride the one already playing.")]
        [SerializeField] float flinchGap = 0.2f;

        [Header("Without the zombie controller")]
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

        /// <summary>The rig has the zombie controller: clips, not bent bones.</summary>
        bool clips;

        /// <summary>Waiting for the animator to come up before the clips can be started.</summary>
        bool starting;

        bool dead;
        float style;
        float walkRate = 1f;
        bool battering;
        int batterHash;
        float lastBlow;
        float blowInterval = 1f;
        float lastFlinch = -10f;

        /// <summary>Metres per second over the last frame. What the walk is matched to.</summary>
        public float Speed => speed;

        /// <summary>Playing the zombie clips rather than bending the ordinary walk.</summary>
        public bool PlaysClips => clips;

        /// <summary>One blow on whatever is in front of it: the arms come down on it, on the beat.</summary>
        public void Pound()
        {
            if (dead) return;

            if (!clips)
            {
                poundLeft = poundSeconds;
                return;
            }

            if (!Ready()) return;

            float now = Time.time;
            if (battering) blowInterval = Mathf.Lerp(blowInterval, Mathf.Clamp(now - lastBlow, 0.4f, 4f), 0.6f);
            lastBlow = now;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(ZombieMotion.ActionLayer);
            bool inLoop = battering && state.shortNameHash == batterHash && !animator.IsInTransition(ZombieMotion.ActionLayer);

            if (!inLoop)
            {
                // Into the loop just before the blow comes down, so it lands now rather than a beat late.
                battering = true;
                animator.SetFloat(BatterRateHash, ZombieMotion.BatterSeconds / Mathf.Max(0.4f, blowInterval));
                animator.CrossFadeInFixedTime(batterHash, 0.1f, ZombieMotion.ActionLayer,
                                              ZombieMotion.BatterSeconds * (ZombieMotion.ImpactPhase - 0.08f));
                return;
            }

            animator.SetFloat(BatterRateHash, ZombieMotion.BatterRate(Mathf.Repeat(state.normalizedTime, 1f), blowInterval));
        }

        /// <summary>A hit: the body jolts with it, over whatever it is doing.</summary>
        public void Hurt(float amount)
        {
            if (dead || !clips || !Ready()) return;

            float now = Time.time;
            if (now - lastFlinch < flinchGap) return;
            lastFlinch = now;

            int pick = HitHashes[Random.Range(0, HitHashes.Length)];
            animator.CrossFadeInFixedTime(pick, 0.04f, ZombieMotion.FlinchLayer, 0f);
        }

        /// <summary>Drops: backwards or forwards on its face, limbs flung out, a bounce and still.</summary>
        public float Die()
        {
            if (dead) return ZombieMotion.LyingAfterSeconds;
            if (!clips || !Ready()) return 0f;

            dead = true;
            battering = false;

            animator.SetFloat(MoveHash, 0f);
            animator.SetFloat(WalkRateHash, 1f);
            animator.CrossFadeInFixedTime(EmptyHash, 0.12f, ZombieMotion.ActionLayer);
            animator.CrossFadeInFixedTime(EmptyHash, 0.2f, ZombieMotion.FlinchLayer);
            animator.CrossFadeInFixedTime(Random.value < 0.55f ? DeathBackHash : DeathForwardHash, 0.08f, ZombieMotion.BaseLayer, 0f);
            return ZombieMotion.LyingAfterSeconds;
        }

        void OnEnable()
        {
            ChooseVariant();

            lastPosition = transform.position;
            speed = 0f;
            poundLeft = 0f;
            dead = false;
            battering = false;
            walkRate = 1f;
            blowInterval = 1f;
            lastFlinch = -10f;

            // Out of step with each other, so a group does not sway as one.
            phase = Random.value * 10f;

            // Half reach out in front of them, half hang and drag; each a little its own.
            style = Random.value < 0.5f ? Random.Range(0f, 0.2f) : Random.Range(0.8f, 1f);
            batterHash = Random.value < 0.5f ? SlamHash : ClawHash;

            starting = clips;
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
            clips = animator != null && HasZombieController(animator);

            // The old pose bends bones the animator has just written. A culled animator stops
            // writing them, and every frame's bend would then land on the last one - a stoop that
            // keeps stooping until the spine is spinning. Always animating keeps the base fresh.
            // The clips need it too: a body falling out of its own bounds must not freeze mid-fall.
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

        /// <summary>The controller has the zombie clips' parameters, not just the visitors' walk.</summary>
        static bool HasZombieController(Animator animator)
        {
            if (animator.runtimeAnimatorController == null) return false;

            // Parameters can only be read from an animator that is up; until then, ask the controller.
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name.StartsWith("Zombie_")) return true;

            return false;
        }

        /// <summary>The animator is up and started on the walk, so the clips can be cued.</summary>
        bool Ready()
        {
            if (animator == null || !animator.isActiveAndEnabled || !animator.isInitialized) return false;
            if (starting) Begin();
            return true;
        }

        /// <summary>A fresh life: standing, its own style, somewhere in the loop no neighbour is.</summary>
        void Begin()
        {
            starting = false;

            animator.Rebind();
            animator.SetFloat(StyleHash, style);
            animator.SetFloat(MoveHash, 0f);
            animator.SetFloat(WalkRateHash, 1f);
            animator.SetFloat(BatterRateHash, 1f);
            animator.Play(LocomotionHash, ZombieMotion.BaseLayer, Random.value);
            animator.Play(EmptyHash, ZombieMotion.ActionLayer, 0f);
            animator.Play(EmptyHash, ZombieMotion.FlinchLayer, 0f);
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

            if (clips)
            {
                Drive(dt);
                return;
            }

            phase += dt * (1.2f + speed);
            if (poundLeft > 0f) poundLeft = Mathf.Max(0f, poundLeft - dt);

            Animate();
            Pose();
        }

        /// <summary>The walk to the pace, and the battering put away once the blows stop or it moves on.</summary>
        void Drive(float dt)
        {
            if (dead || !Ready()) return;

            float scale = Mathf.Max(0.1f, animator.humanScale);
            float move = ZombieMotion.Move(speed, scale);
            animator.SetFloat(MoveHash, move, moveDamping, dt);

            float blended = animator.GetFloat(MoveHash);
            float rate = ZombieMotion.WalkRate(speed, Mathf.Max(blended, move > 0f ? ZombieMotion.ShambleSpeed : 0f), scale);
            walkRate = Mathf.Lerp(walkRate, rate, 1f - Mathf.Exp(-6f * dt));
            animator.SetFloat(WalkRateHash, walkRate);

            if (battering && (speed > 0.3f || Time.time - lastBlow > blowInterval * 1.6f + 0.25f))
            {
                battering = false;
                animator.CrossFadeInFixedTime(EmptyHash, 0.3f, ZombieMotion.ActionLayer);
            }
        }

        // --- Without the zombie controller -----------------------------------------------------------

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
