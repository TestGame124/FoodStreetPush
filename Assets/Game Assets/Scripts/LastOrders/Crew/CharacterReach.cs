using UnityEngine;

namespace LastOrders.Crew
{
    /// <summary>
    /// Where a character's hands go: a cloth going round on a table top, both hands on a broom handle,
    /// a duster flicking over a shelf. The work clip gives the body its lean and rhythm; this puts the
    /// hands exactly on whatever is being worked on, whatever its height.
    ///
    /// Humanoid hand IK, eased in and out so a hand travels to its goal rather than snapping to it.
    /// Applied on every layer with an IK pass, so it lands on top of an upper-body work layer as well as
    /// the base one. Sits on the GameObject with the Animator: Unity only calls OnAnimatorIK there.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public sealed class CharacterReach : MonoBehaviour
    {
        [Tooltip("Seconds for a hand to go to its goal, or come back to the animation.")]
        [SerializeField, Min(0.05f)] float easeSeconds = 0.3f;

        struct Hand
        {
            public Vector3 position;
            public Quaternion rotation;
            public bool rotate;
            public float wanted, weight;
            public Vector3 hint;
            public bool hasHint;
        }

        Animator animator;
        Hand left, right;

        void Awake() => animator = GetComponent<Animator>();

        /// <summary>Puts a hand on <paramref name="position"/>, turned to <paramref name="rotation"/> if given.</summary>
        public void Hold(AvatarIKGoal goal, Vector3 position, Quaternion? rotation = null, float strength = 1f, Vector3? elbow = null)
        {
            ref Hand hand = ref goal == AvatarIKGoal.LeftHand ? ref left : ref right;
            hand.position = position;
            hand.rotate = rotation.HasValue;
            if (rotation.HasValue) hand.rotation = rotation.Value;
            hand.wanted = Mathf.Clamp01(strength);
            hand.hasHint = elbow.HasValue;
            if (elbow.HasValue) hand.hint = elbow.Value;
        }

        /// <summary>Lets one hand go back to the animation.</summary>
        public void Release(AvatarIKGoal goal)
        {
            if (goal == AvatarIKGoal.LeftHand) left.wanted = 0f;
            else right.wanted = 0f;
        }

        /// <summary>Lets both hands go back to the animation.</summary>
        public void Release()
        {
            left.wanted = 0f;
            right.wanted = 0f;
        }

        public bool Reaching => left.weight > 0.001f || right.weight > 0.001f;

        int easedFrame = -1;

        void OnAnimatorIK(int layer)
        {
            if (animator == null) return;

            // Eased once a frame, however many layers ask.
            if (easedFrame != Time.frameCount)
            {
                easedFrame = Time.frameCount;
                float step = Time.deltaTime / easeSeconds;
                left.weight = Mathf.MoveTowards(left.weight, left.wanted, step);
                right.weight = Mathf.MoveTowards(right.weight, right.wanted, step);
            }

            Apply(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, left);
            Apply(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, right);
        }

        void Apply(AvatarIKGoal goal, AvatarIKHint elbow, in Hand hand)
        {
            float w = hand.weight * hand.weight * (3f - 2f * hand.weight);
            if (w <= 0.001f) return;

            animator.SetIKPositionWeight(goal, w);
            animator.SetIKPosition(goal, hand.position);

            animator.SetIKRotationWeight(goal, hand.rotate ? w : 0f);
            if (hand.rotate) animator.SetIKRotation(goal, hand.rotation);

            animator.SetIKHintPositionWeight(elbow, hand.hasHint ? w * 0.6f : 0f);
            if (hand.hasHint) animator.SetIKHintPosition(elbow, hand.hint);
        }
    }
}
