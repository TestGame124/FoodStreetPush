using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Defense
{
    /// <summary>
    /// What a gun does on each shot besides the tracer: the barrels buck back and run forward
    /// again, the flash at the muzzle is rolled to a new angle so no two look stamped, and a
    /// spent case is thrown out of the side.
    ///
    /// An add-on to a <see cref="DefensePost"/>. It listens to the post's shots and reads its
    /// numbers from the post's <see cref="DefenseMotionProfile"/>, so it knows nothing about the
    /// rules and the post knows nothing about it - leave it off a model and the model still
    /// shoots, put it on another gun and that gun kicks. Every part is optional.
    /// </summary>
    public sealed class DefenseRecoil : MonoBehaviour
    {
        [SerializeField] DefensePost post;

        [Tooltip("Thrown back along their own parent's -Z on a shot, then eased home: the barrels.")]
        [SerializeField] Transform[] kickers;

        [Tooltip("Optional. Each rolled round its own forward axis to a new angle on every shot: a " +
                 "barrel's muzzle flash.")]
        [SerializeField] Transform[] flashRolls;

        [Tooltip("Optional. Spent cases, emitted from where it stands on every shot.")]
        [SerializeField] ParticleSystem casings;

        Vector3[] rest;
        float sinceShot = float.MaxValue;

        void Awake()
        {
            if (post == null) post = GetComponentInParent<DefensePost>();

            rest = new Vector3[kickers != null ? kickers.Length : 0];
            for (int i = 0; i < rest.Length; i++)
                if (kickers[i] != null) rest[i] = kickers[i].localPosition;
        }

        void OnEnable()
        {
            if (post != null) post.Fired += HandleFired;
        }

        void OnDisable()
        {
            if (post != null) post.Fired -= HandleFired;

            // Never left stuck halfway back if the gun is switched off mid-kick.
            sinceShot = float.MaxValue;
            Settle(0f);
        }

        void Update()
        {
            if (sinceShot == float.MaxValue) return;

            sinceShot += Time.deltaTime;

            DefenseMotionProfile motion = post != null ? post.Motion : null;
            float back = motion != null ? motion.RecoilOffset(sinceShot) : 0f;
            Settle(back);

            if (back <= 0f) sinceShot = float.MaxValue;
        }

        void HandleFired(Vector3 from, Vector3 to, DefenseShot shot)
        {
            DefenseMotionProfile motion = post.Motion;
            if (motion == null) return;

            sinceShot = 0f;
            Settle(motion.RecoilOffset(0f));

            if (flashRolls != null)
                foreach (Transform flash in flashRolls)
                    if (flash != null) flash.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            if (casings != null && motion.casingsPerShot > 0) casings.Emit(motion.casingsPerShot);
        }

        void Settle(float back)
        {
            if (kickers == null || rest == null) return;

            for (int i = 0; i < kickers.Length && i < rest.Length; i++)
                if (kickers[i] != null) kickers[i].localPosition = rest[i] + Vector3.back * back;
        }

        /// <summary>Editor wiring, for the prefab generator.</summary>
        public void Configure(DefensePost owner, Transform[] barrels, Transform[] flashes, ParticleSystem spentCases)
        {
            post = owner;
            kickers = barrels;
            flashRolls = flashes;
            casings = spentCases;
        }
    }
}
