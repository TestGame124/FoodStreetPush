using UnityEngine;

namespace LastOrders.Jobs
{
    /// <summary>
    /// Where a character is looking: the head turns to it, the chest a little with it, the eyes lead.
    /// Nothing a person does reads as alive so much as their eyes going to things - someone walking
    /// past, a car pulling in, the hens, whoever they are talking to.
    ///
    /// Humanoid look-at IK, on the base layer's IK pass, so it rides on whatever the body is
    /// playing. The point is eased rather than snapped to, and so is the weight, so a glance turns
    /// the head over a moment and lets it go again. <see cref="WorkerLife"/> says what to look at;
    /// with nothing said the head is left to the animation.
    ///
    /// Sits on the GameObject with the Animator: Unity only calls OnAnimatorIK there.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public sealed class CharacterLookAt : MonoBehaviour
    {
        [Tooltip("How much the head turns to the point, at full weight.")]
        [SerializeField, Range(0f, 1f)] float head = 0.9f;

        [Tooltip("How much the chest turns with it.")]
        [SerializeField, Range(0f, 1f)] float body = 0.22f;

        [Tooltip("How much the eyes lead.")]
        [SerializeField, Range(0f, 1f)] float eyes = 0.6f;

        [Tooltip("0 lets the head turn anywhere; 1 hardly at all. Keeps a look over the shoulder human.")]
        [SerializeField, Range(0f, 1f)] float clamp = 0.55f;

        [Tooltip("How fast the gaze swings to a new point, and the weight comes and goes.")]
        [SerializeField] float ease = 4f;

        Animator animator;
        Transform follow;
        Vector3 followOffset;
        Vector3 point, gaze;
        float wanted, weight;
        bool hasGaze;

        /// <summary>True while looking at something rather than where the animation points the head.</summary>
        public bool Looking => wanted > 0f;

        void Awake() => animator = GetComponent<Animator>();

        /// <summary>Looks at <paramref name="target"/>, <paramref name="offset"/> above its origin, as it moves.</summary>
        public void Watch(Transform target, Vector3 offset, float strength = 1f)
        {
            follow = target;
            followOffset = offset;
            wanted = Mathf.Clamp01(strength);
            if (target != null) point = target.position + offset;
        }

        /// <summary>Looks at a fixed point.</summary>
        public void Watch(Vector3 at, float strength = 1f)
        {
            follow = null;
            point = at;
            wanted = Mathf.Clamp01(strength);
        }

        /// <summary>Lets the head go back to the animation.</summary>
        public void Release()
        {
            follow = null;
            wanted = 0f;
        }

        void OnAnimatorIK(int layer)
        {
            if (animator == null || layer != 0) return;

            float dt = Time.deltaTime;
            if (follow != null)
            {
                point = follow.position + followOffset;
            }
            else if (!ReferenceEquals(follow, null))
            {
                // What it was watching has been destroyed: look away.
                follow = null;
                wanted = 0f;
            }

            Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            Vector3 eye = headBone != null ? headBone.position : transform.position + Vector3.up * 1.5f;
            if (!hasGaze)
            {
                gaze = eye + transform.forward * 3f;
                hasGaze = true;
            }

            // Swing the gaze toward the point as an angle about the eye, not a straight line through
            // the head - that would duck the head as it crossed in front of the face.
            Vector3 from = gaze - eye, to = point - eye;
            if (to.sqrMagnitude > 1e-4f && from.sqrMagnitude > 1e-4f)
            {
                Vector3 dir = Vector3.Slerp(from.normalized, to.normalized, 1f - Mathf.Exp(-dt * ease * 1.4f));
                float reach = Mathf.Lerp(from.magnitude, to.magnitude, 1f - Mathf.Exp(-dt * ease));
                gaze = eye + dir * reach;
            }

            weight = Mathf.MoveTowards(weight, wanted, dt * ease * 0.5f);
            if (weight <= 0.001f) return;

            animator.SetLookAtWeight(weight, body, head, eyes, clamp);
            animator.SetLookAtPosition(gaze);
        }
    }
}
