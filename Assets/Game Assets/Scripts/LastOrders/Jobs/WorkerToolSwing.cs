using UnityEngine;

namespace LastOrders.Jobs
{
    /// <summary>
    /// A hammer in the worker's hand while they work, swung in code.
    ///
    /// Stands in for an animation clip the project does not have yet. The worker already sets
    /// an Animator bool "Working", so when a real clip arrives the controller plays it and this
    /// component's swing can be switched off - the strike timing is the only part worth
    /// keeping, and the clip can raise that from an animation event instead.
    ///
    /// The prop is built from primitives on first use. With no work clip it swings from the
    /// shoulder, overhead and down into whatever is in front: the arm hangs still, and a hammer
    /// parented to a still hand only twitches at hip height. On the worker controller, which has
    /// the swing clip, it is gripped in the right hand instead and the clip's arms carry it -
    /// swinging it again on top made the hammer and the hands go their separate ways.
    /// </summary>
    [RequireComponent(typeof(WorkerAgent))]
    public sealed class WorkerToolSwing : MonoBehaviour
    {
        [Tooltip("Swings per second.")]
        [SerializeField] float swingRate = 1.6f;

        [Tooltip("Parent the tool to the hand bone rather than swinging it from the shoulder. " +
                 "For a rig with a real work clip.")]
        [SerializeField] bool attachToHand;

        [Tooltip("The swing's pivot, relative to the worker, when not attached to the hand.")]
        [SerializeField] Vector3 shoulder = new Vector3(0.28f, 1.3f, 0.1f);

        [Tooltip("Pitch of the tool at the top of the swing: overhead and a little behind.")]
        [SerializeField] float raisedPitch = -110f;

        [Tooltip("Pitch of the tool as the blow lands: forward and down into the pile.")]
        [SerializeField] float struckPitch = 60f;

        [Tooltip("Degrees the tool travels when attached to the hand.")]
        [SerializeField] float swingArc = 95f;

        [Tooltip("Bones tried in order when attached to the hand.")]
        [SerializeField] string[] handBones = { "Hand_R", "RightHand", "hand_r", "mixamorig:RightHand" };

        [Tooltip("Optional. A hand-authored tool prop; built from primitives when left empty.")]
        [SerializeField] Transform tool;

        WorkerAgent agent;
        Transform pivot;
        float phase;
        bool struckThisSwing;

        void Awake()
        {
            agent = GetComponent<WorkerAgent>();
        }

        /// <summary>The worker controller swings the arms with its own clip: the tool rides in the hand, and
        /// is not swung again on top of it.</summary>
        bool clipSwings;

        void Start()
        {
            Animator animator = GetComponentInChildren<Animator>();
            clipSwings = WorkerGestures.CanGesture(animator) && animator.isHuman;
            if (clipSwings) attachToHand = true;

            pivot = BuildPivot();
            pivot.gameObject.SetActive(false);
        }

        void Update()
        {
            if (pivot == null) return;

            bool working = agent.IsWorking;
            if (pivot.gameObject.activeSelf != working)
            {
                pivot.gameObject.SetActive(working);
                phase = 0f;
                struckThisSwing = false;
            }

            if (!working) return;

            phase += Time.deltaTime * swingRate;
            float t = phase - Mathf.Floor(phase);

            // A slow lift and a fast drop. A symmetric sine reads as waving, not hammering.
            float lift = t < 0.7f ? Mathf.SmoothStep(0f, 1f, t / 0.7f) : 1f - (t - 0.7f) / 0.3f;
            if (!clipSwings)
            {
                float pitch = attachToHand ? -lift * swingArc : Mathf.Lerp(struckPitch, raisedPitch, lift);
                pivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            if (t < 0.7f)
            {
                struckThisSwing = false;
                return;
            }

            // The blow lands at the bottom of the drop, once per swing.
            if (struckThisSwing || t < 0.95f) return;

            struckThisSwing = true;
            Strike();
        }

        void Strike()
        {
            // A zombie in front of them before the job's pile: fighting comes first out there.
            Transform target = agent.StrikeTarget;
            if (target == null) target = agent.Current?.Target;
            if (target == null) return;

            var receiver = target.GetComponentInParent<IWorkStrikeReceiver>();
            receiver?.OnStrike();
        }

        Transform BuildPivot()
        {
            Animator animator = GetComponentInChildren<Animator>();
            Transform hand = null;
            if (attachToHand && animator != null && animator.isHuman) hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (attachToHand && hand == null) hand = FindBone(transform);

            var host = new GameObject("ToolPivot").transform;
            host.SetParent(hand != null ? hand : transform, false);

            if (hand == null) host.localPosition = shoulder;
            else if (clipSwings) Grip(host, hand, animator);

            // Bone scale differs by rig; the prop is sized in world metres regardless.
            Vector3 lossy = host.lossyScale;
            if (lossy.x > 1e-4f) host.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);

            if (tool != null)
            {
                tool.SetParent(host, false);
                return host;
            }

            BuildHammer(host);
            return host;
        }

        /// <summary>
        /// The handle through the fist, held as a hammer is: across the palm, out past the thumb, the head
        /// beyond it. Found from where the fingers and the thumb are, so it fits any rig's hand bone.
        /// </summary>
        static void Grip(Transform host, Transform hand, Animator animator)
        {
            Transform middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            Transform thumb = animator.GetBoneTransform(HumanBodyBones.RightThumbProximal);
            if (middle == null || thumb == null) return;

            Vector3 fingers = middle.position - hand.position;
            Vector3 across = Vector3.ProjectOnPlane(thumb.position - hand.position, fingers.normalized);
            if (across.sqrMagnitude < 1e-6f) return;
            across.Normalize();

            // The fist a little way up the handle from its end.
            host.SetPositionAndRotation(hand.position + fingers * 0.6f - across * 0.08f, Quaternion.LookRotation(across, fingers.normalized));
        }

        Transform FindBone(Transform root)
        {
            for (int i = 0; i < handBones.Length; i++)
            {
                Transform found = FindDeep(root, handBones[i]);
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

        static void BuildHammer(Transform parent)
        {
            // Larger than life on purpose: at this camera distance a true-to-scale hammer is a
            // few pixels, and the swing is only worth having if it can be seen.
            // Handle forward along the hand, head across its end.
            Part(parent, "Handle", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.34f),
                 new Vector3(0.08f, 0.36f, 0.08f), Quaternion.Euler(90f, 0f, 0f),
                 new Color(0.55f, 0.36f, 0.2f));

            Part(parent, "Head", PrimitiveType.Cube, new Vector3(0f, 0f, 0.7f),
                 new Vector3(0.16f, 0.38f, 0.16f), Quaternion.identity,
                 new Color(0.45f, 0.47f, 0.5f));
        }

        static void Part(Transform parent, string name, PrimitiveType type, Vector3 position,
                         Vector3 scale, Quaternion rotation, Color colour)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());

            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                renderer.material = new Material(shader) { color = colour };
            }
            else
            {
                renderer.material.color = colour;
            }
        }
    }
}
