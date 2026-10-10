using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// A body at a table: hips on the cushion, hands on the top, a fork going from the plate to
    /// the mouth while eating, and a lean forward to sit down and to get up again.
    ///
    /// The project has one seated clip, and it rests the hands on the knees - which, at any table
    /// tall enough to eat at, is under the top, so arms went into the table. Rather than a clip
    /// per table height the hands are put on the top with IK, wherever the top actually is, and
    /// eating is the right hand travelling plate to mouth on top of that. The clip also seats
    /// every model at its own leg length, so short-legged ones sank into the cushion; the body is
    /// lifted (or lowered) until the hips sit on it.
    ///
    /// Driven a frame at a time by <see cref="Visitor"/>, which knows the table. Lives on the
    /// character's Animator, because that is the only place OnAnimatorIK is sent, and needs the
    /// IK pass on the controller's base layer - 'Set Up Seated Visitors' turns it on.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class SeatedPose : MonoBehaviour
    {
        /// <summary>What the visitor is doing at the table this frame.</summary>
        public struct Frame
        {
            /// <summary>0 standing, 1 sat on the cushion.</summary>
            public float seated;

            /// <summary>0 upright, 1 the deepest point of the lean that sitting down and getting up take.</summary>
            public float lean;

            public bool handsOnTable;
            public bool eating;

            /// <summary>The middle of the table's edge in front of them, at the top's height.</summary>
            public Vector3 tableEdge;

            /// <summary>Into the table.</summary>
            public Vector3 inward;

            /// <summary>World height of the cushion's top.</summary>
            public float seatTop;

            public Vector3 plate;
            public bool hasPlate;
        }

        static readonly int SittingState = Animator.StringToHash("Sitting");
        static readonly int EatingState = Animator.StringToHash("Eating");

        [Header("Hands on the table")]
        [Tooltip("Metres in from the table's edge the hands rest.")]
        [SerializeField] float restDepth = 0.14f;

        [Tooltip("Metres either side of the middle the hands rest.")]
        [SerializeField] float restSpread = 0.19f;

        [Tooltip("Height of the wrist above the top: a hand's thickness.")]
        [SerializeField] float palm = 0.035f;

        [Tooltip("Seconds for the hands to go onto the table, or come off it.")]
        [SerializeField, Min(0.05f)] float handsSeconds = 0.4f;

        [Header("Eating")]
        [Tooltip("Seconds from one forkful to the next, rolled per bite.")]
        [SerializeField] Vector2 biteSeconds = new Vector2(2.1f, 2.8f);

        [Header("Sitting")]
        [Tooltip("Height of the hips above the cushion once sat on it.")]
        [SerializeField] float hipsAboveSeat = 0.1f;

        [Tooltip("Degrees the back bends forward at the deepest point of sitting down or getting up.")]
        [SerializeField, Range(0f, 45f)] float lean = 24f;

        Animator animator;
        Transform hips, spine, chest, head, leftFoot, rightFoot;
        Frame frame;

        Vector3 restLocal;
        float hands;
        float lift;
        float standingHips;
        float ankle;
        float seatedHips = -1f;
        float biteClock, bitePeriod;

        void Awake()
        {
            animator = GetComponent<Animator>();
            restLocal = transform.localPosition;

            if (animator.isHuman)
            {
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                head = animator.GetBoneTransform(HumanBodyBones.Head);
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }

            // Until the clip has been seen fully sat, guess its hip height from the standing one:
            // across this project's models the seated clip puts the hips at 0.55-0.56 of it.
            standingHips = hips != null ? Mathf.Max(0.3f, hips.position.y - transform.position.y) : 1f;
            ankle = leftFoot != null ? Mathf.Clamp(leftFoot.position.y - transform.position.y, 0.03f, 0.2f) : 0.08f;

            bitePeriod = Roll();
        }

        public void Drive(in Frame next)
        {
            // Every meal starts from the hand at rest, not halfway to the mouth from the last one.
            if (!next.eating) biteClock = 0f;

            frame = next;
        }

        /// <summary>Back to standing, at once. For the pool.</summary>
        public void ResetPose()
        {
            frame = default;
            hands = 0f;
            lift = 0f;
            biteClock = 0f;
            transform.localPosition = restLocal;
        }

        void OnAnimatorIK(int layer)
        {
            if (layer != 0 || animator == null) return;

            PlantFoot(AvatarIKGoal.LeftFoot);
            PlantFoot(AvatarIKGoal.RightFoot);

            hands = Mathf.MoveTowards(hands, frame.handsOnTable ? 1f : 0f, Time.deltaTime / handsSeconds);
            float weight = hands * hands * (3f - 2f * hands);

            if (weight <= 0.001f)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
                animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
                animator.SetLookAtWeight(0f);
                return;
            }

            Vector3 forward = Flat(frame.inward, transform.forward);
            Vector3 side = Vector3.Cross(Vector3.up, forward);

            Vector3 middle = frame.tableEdge + forward * restDepth + Vector3.up * palm;
            Vector3 left = middle - side * restSpread;
            Vector3 right = middle + side * restSpread;

            Vector3 lookAt = middle;
            float look = 0f;

            if (frame.eating)
            {
                Vector3 dish = frame.hasPlate ? frame.plate : middle;
                dish.y = middle.y + 0.03f;

                // The other hand steadies the plate.
                left = Vector3.Lerp(left, dish - side * 0.13f, 0.6f);
                left.y = middle.y;

                right = Bite(Vector3.Lerp(right, dish + side * 0.12f, 0.4f), dish + side * 0.03f, forward, ref lookAt, ref look);
            }

            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, left);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, right);

            // Elbows out and down, as they are with forearms on a table, rather than wherever the
            // solver happens to swing them.
            Vector3 waist = hips != null ? hips.position : transform.position + Vector3.up * 0.6f;
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, weight * 0.7f);
            animator.SetIKHintPosition(AvatarIKHint.LeftElbow, waist + Vector3.up * 0.22f - side * 0.4f + forward * 0.05f);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, weight * 0.7f);
            animator.SetIKHintPosition(AvatarIKHint.RightElbow, waist + Vector3.up * 0.22f + side * 0.4f + forward * 0.05f);

            animator.SetLookAtWeight(look * weight, 0.05f, 0.6f, 0f, 0.5f);
            animator.SetLookAtPosition(lookAt);
        }

        /// <summary>
        /// Feet flat on the floor wherever the clip put them. Lifting the hips onto a cushion
        /// higher than the clip's own seat lifts the feet with them, and a diner with their feet
        /// hovering under the table reads as one floating in the chair.
        /// </summary>
        void PlantFoot(AvatarIKGoal goal)
        {
            float weight = Mathf.Clamp01(frame.seated);
            animator.SetIKPositionWeight(goal, weight);
            if (weight <= 0f) return;

            Vector3 at = animator.GetIKPosition(goal);
            at.y = Floor + ankle;
            animator.SetIKPosition(goal, at);
        }

        /// <summary>The floor under them: the visitor's own height, which the model is lifted off.</summary>
        float Floor => transform.parent != null ? transform.parent.position.y : transform.position.y - lift;

        /// <summary>
        /// Where the eating hand is: down to the plate, a scoop, up to the mouth, a moment there,
        /// and back to rest. The eyes follow the fork down and come up with it.
        /// </summary>
        Vector3 Bite(Vector3 rest, Vector3 dish, Vector3 forward, ref Vector3 lookAt, ref float look)
        {
            biteClock += Time.deltaTime;
            if (biteClock >= bitePeriod)
            {
                biteClock -= bitePeriod;
                bitePeriod = Roll();
            }

            Vector3 mouth = head != null
                ? head.position + forward * 0.11f - Vector3.up * 0.07f
                : transform.position + Vector3.up * 1.1f + forward * 0.15f;

            float p = biteClock / bitePeriod;

            lookAt = dish;
            look = p < 0.45f ? 0.8f : Mathf.Lerp(0.8f, 0f, Smooth((p - 0.45f) / 0.2f));

            if (p < 0.22f) return Vector3.Lerp(rest, dish, Smooth(p / 0.22f));
            if (p < 0.34f) return dish + forward * (Mathf.Sin((p - 0.22f) / 0.12f * Mathf.PI) * 0.04f);
            if (p < 0.56f) return Vector3.Lerp(dish, mouth, Smooth((p - 0.34f) / 0.22f));
            if (p < 0.72f) return mouth;
            return Vector3.Lerp(mouth, rest, Smooth((p - 0.72f) / 0.28f));
        }

        void LateUpdate()
        {
            if (animator == null || hips == null) return;

            float rootY = Floor;

            // Standing, one foot or the other is always flat on the floor: the lower ankle is how
            // high an ankle sits. Watched rather than read once, because this is added mid-stride -
            // and only while upright, since lying in a cot puts both ankles at the root's height.
            if (frame.seated <= 0f && leftFoot != null && rightFoot != null && transform.up.y > 0.95f)
                ankle = Mathf.Max(0.03f, Mathf.Min(ankle, Mathf.Min(leftFoot.position.y, rightFoot.position.y) - rootY));

            // The clip's own seated hip height, measured once it is fully sat - the body as it is
            // drawn, minus the lift already on it.
            if (frame.seated >= 1f && !animator.IsInTransition(0))
            {
                int state = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
                if (state == SittingState || state == EatingState)
                {
                    float measured = hips.position.y - lift - rootY;
                    seatedHips = seatedHips < 0f ? measured : Mathf.Lerp(seatedHips, measured, 0.1f);
                }
            }

            float clipHips = seatedHips >= 0f ? seatedHips : standingHips * 0.555f;
            float wanted = frame.seatTop + hipsAboveSeat - (rootY + clipHips);

            lift = frame.seated > 0f ? wanted * Mathf.Clamp01(frame.seated) : 0f;
            transform.localPosition = restLocal + Vector3.up * lift;

            if (frame.lean <= 0.001f || spine == null) return;

            // Hips back, chest forward over the knees - then the head tipped back up, so the eyes
            // stay on where they are going.
            Vector3 across = Vector3.Cross(Vector3.up, transform.forward);
            float bend = lean * frame.lean;

            spine.rotation = Quaternion.AngleAxis(bend * 0.6f, across) * spine.rotation;
            if (chest != null) chest.rotation = Quaternion.AngleAxis(bend * 0.4f, across) * chest.rotation;
            if (head != null) head.rotation = Quaternion.AngleAxis(-bend * 0.7f, across) * head.rotation;
        }

        float Roll() => Random.Range(Mathf.Min(biteSeconds.x, biteSeconds.y), Mathf.Max(biteSeconds.x, biteSeconds.y, 0.5f));

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static Vector3 Flat(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f)
            {
                direction = fallback;
                direction.y = 0f;
            }

            return direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.forward;
        }
    }
}
