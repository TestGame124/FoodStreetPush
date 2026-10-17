using UnityEngine;

namespace LastOrders.Crew
{
    /// <summary>What a crew member has in hand.</summary>
    public enum CrewProp
    {
        None,

        /// <summary>A rag, flat in the right palm.</summary>
        Cloth,

        /// <summary>A broom, held across both hands; posed by whoever is sweeping, not by the hand.</summary>
        Broom,

        /// <summary>A feather duster in the right hand.</summary>
        Duster,
    }

    /// <summary>
    /// The things a crew member picks up for a chore - a cloth to wipe with, a broom, a feather duster
    /// - shown in hand while the chore lasts and put away after. One of each, made the first time it is
    /// needed from <see cref="CrewMeshes"/>.
    ///
    /// The cloth and the duster ride on the right hand bone. The broom does not: its head has to stay
    /// on the floor however the arms move, so whoever sweeps poses it with <see cref="PlaceBroom"/> and
    /// puts the hands on its handle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrewProps : MonoBehaviour
    {
        [Tooltip("Vertex-painted toon material the props are drawn with. A runtime copy of the toon shader when empty.")]
        [SerializeField] Material paint;

        [Header("In the right hand")]
        [Tooltip("In the hand bone's space. On these rigs the fingers run along +Y, the thumb is to -X, and the palm faces +Z.")]
        [SerializeField] Vector3 clothOffset = new Vector3(0f, 0.075f, 0.05f);
        [SerializeField] Vector3 clothTurn = new Vector3(90f, 0f, 0f);
        [SerializeField] Vector3 dusterOffset = new Vector3(0f, 0.08f, 0.02f);
        [SerializeField] Vector3 dusterTurn = new Vector3(0f, 0f, 90f);

        [Tooltip("Seconds for a prop to grow into the hand, or shrink away.")]
        [SerializeField] float popSeconds = 0.18f;

        Transform hand;
        Transform cloth, broom, duster;
        CrewProp shown = CrewProp.None;
        Transform popping;
        float pop = 1f;

        public CrewProp Shown => shown;

        /// <summary>The broom while it is out, to be posed.</summary>
        public Transform Broom => shown == CrewProp.Broom ? broom : null;

        void Awake()
        {
            CrewMeshes.UsePaint(paint);

            var animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman) hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        /// <summary>Puts away whatever is in hand and takes out <paramref name="prop"/>.</summary>
        public void Hold(CrewProp prop)
        {
            if (prop == shown) return;

            Transform was = Of(shown);
            if (was != null) was.gameObject.SetActive(false);

            shown = prop;
            Transform now = Of(prop, create: true);
            if (now == null) return;

            now.gameObject.SetActive(true);
            popping = now;
            pop = 0f;
            now.localScale = Vector3.one * 0.01f;
        }

        /// <summary>Stands the broom with its head at <paramref name="head"/> and its handle leaning to <paramref name="top"/>.</summary>
        public void PlaceBroom(Vector3 head, Vector3 top, Vector3 facing)
        {
            if (broom == null || shown != CrewProp.Broom) return;

            Vector3 up = (top - head).normalized;
            Vector3 across = Vector3.Cross(up, facing);
            if (across.sqrMagnitude < 1e-4f) across = Vector3.Cross(up, Vector3.forward);
            Vector3 forward = Vector3.Cross(across.normalized, up);
            broom.SetPositionAndRotation(head, Quaternion.LookRotation(forward, up));
        }

        /// <summary>A point on the broom's handle, <paramref name="height"/> metres up it.</summary>
        public Vector3 BroomGrip(float height) => broom != null ? broom.TransformPoint(0f, height, 0f) : transform.position;

        /// <summary>
        /// Lays the cloth flat at <paramref name="at"/>, turned to <paramref name="turn"/>, for this frame: while
        /// wiping it lies on the surface under the palm rather than tilting with the wrist. Called after
        /// the hands are posed; next frame it rides in the hand again unless laid again.
        /// </summary>
        public void LayCloth(Vector3 at, Quaternion turn)
        {
            if (cloth == null || shown != CrewProp.Cloth || popping == cloth) return;
            cloth.SetPositionAndRotation(at, turn);
        }

        /// <summary>The middle of the palm of the right hand, a little out from the wrist toward the fingers.</summary>
        public Vector3 Palm
        {
            get
            {
                if (hand == null) return transform.position + Vector3.up;

                var animator = hand.GetComponentInParent<Animator>();
                Transform finger = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal) : null;
                return finger != null ? Vector3.Lerp(hand.position, finger.position, 0.6f) : hand.position + hand.up * 0.07f;
            }
        }

        void LateUpdate()
        {
            if (popping == null) return;

            pop = Mathf.MoveTowards(pop, 1f, Time.deltaTime / Mathf.Max(0.01f, popSeconds));
            float s = 1f + 0.25f * Mathf.Sin(pop * Mathf.PI) - (1f - pop);
            popping.localScale = Vector3.one * Mathf.Max(0.01f, s);
            if (pop >= 1f)
            {
                popping.localScale = Vector3.one;
                popping = null;
            }
        }

        Transform Of(CrewProp prop, bool create = false)
        {
            switch (prop)
            {
                case CrewProp.Cloth:
                    if (cloth == null && create) cloth = Make("Cloth", CrewMeshes.Cloth, hand, clothOffset, clothTurn);
                    return cloth;
                case CrewProp.Broom:
                    // Free-standing: posed in the world, not by the hand.
                    if (broom == null && create) broom = Make("Broom", CrewMeshes.Broom, transform, new Vector3(0.35f, 0f, 0.3f), Vector3.zero, world: true);
                    return broom;
                case CrewProp.Duster:
                    if (duster == null && create) duster = Make("Duster", CrewMeshes.Duster, hand, dusterOffset, dusterTurn);
                    return duster;
                default:
                    return null;
            }
        }

        /// <summary>
        /// A child of the hand bone at world scale: imported rigs often carry a scale on their bones,
        /// and a prop parented straight to one would come out a hundred times too big, or too small.
        /// </summary>
        Transform Socket()
        {
            if (hand == null) return transform;

            Transform found = hand.Find("PropSocket");
            if (found != null) return found;

            var socket = new GameObject("PropSocket").transform;
            socket.SetParent(hand, false);
            Vector3 lossy = hand.lossyScale;
            socket.localScale = new Vector3(1f / Mathf.Max(1e-4f, lossy.x), 1f / Mathf.Max(1e-4f, lossy.y), 1f / Mathf.Max(1e-4f, lossy.z));
            return socket;
        }

        Transform Make(string name, Mesh mesh, Transform parent, Vector3 offset, Vector3 turn, bool world = false)
        {
            if (parent == null) parent = transform;
            else if (parent == hand) parent = Socket();

            var go = new GameObject("Prop_" + name);
            Transform t = go.transform;
            t.SetParent(world ? null : parent, false);
            if (world) t.position = parent.TransformPoint(offset);
            else
            {
                t.localPosition = offset;
                t.localRotation = Quaternion.Euler(turn);
            }

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = paint != null ? paint : CrewMeshes.Paint;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            go.SetActive(false);
            return t;
        }

        void OnDisable()
        {
            if (broom != null) broom.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            // The broom lives at the scene root while posed, so it goes with its owner.
            if (broom != null) Destroy(broom.gameObject);
        }
    }
}
