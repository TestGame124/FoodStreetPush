using UnityEngine;

namespace LastOrders.Defense
{
    /// <summary>
    /// Something on a gun that turns as it works - a cluster of barrels revolving as a gatling's do,
    /// the corn shooter's flywheel: it winds round while the gun has something in its sights, and
    /// turns on by a step with every shot.
    ///
    /// An add-on to a <see cref="DefensePost"/>, like <see cref="DefenseRecoil"/> - it listens to the
    /// post's shots and reads whether it is tracking, and nothing else. The drum turns about its own
    /// forward axis, so it can sit on the barrels the recoil kicks back.
    /// </summary>
    public sealed class DefenseBarrelSpin : MonoBehaviour
    {
        [SerializeField] DefensePost post;

        [Tooltip("Turned about its own Z on every shot.")]
        [SerializeField] Transform drum;

        [Tooltip("Barrels round the drum: a shot turns it on by one.")]
        [Min(1)] [SerializeField] int barrels = 4;

        [Tooltip("Degrees a second it winds round while it has something in its sights.")]
        [SerializeField] float trackingSpin = 90f;

        [Tooltip("How quickly the drum catches up with where it should be. Higher is snappier.")]
        [SerializeField] float catchUp = 16f;

        [Tooltip("Which way it turns: 1, or -1 for the other of a pair of wheels turning against each other.")]
        [SerializeField] float sense = 1f;

        Quaternion rest;
        float angle;
        float goal;

        void Awake()
        {
            if (post == null) post = GetComponentInParent<DefensePost>();
            if (drum != null) rest = drum.localRotation;
        }

        void OnEnable()
        {
            if (post != null) post.Fired += HandleFired;
        }

        void OnDisable()
        {
            if (post != null) post.Fired -= HandleFired;
        }

        void HandleFired(Vector3 from, Vector3 to, DefenseShot shot) => goal += 360f / barrels;

        void Update()
        {
            if (drum == null) return;

            DefenseUnit unit = post != null ? post.Unit : null;
            if (unit != null && unit.Target != null) goal += trackingSpin * Time.deltaTime;

            angle = Mathf.Lerp(angle, goal, 1f - Mathf.Exp(-catchUp * Time.deltaTime));

            // Both wound back together a turn at a time, so neither grows without end.
            if (angle > 360f && goal > 360f)
            {
                angle -= 360f;
                goal -= 360f;
            }

            drum.localRotation = rest * Quaternion.Euler(0f, 0f, angle * sense);
        }

        /// <summary>Editor wiring, for the prefab builder.</summary>
        public void Configure(DefensePost owner, Transform spinning, int count)
        {
            post = owner;
            drum = spinning;
            barrels = Mathf.Max(1, count);
        }

        /// <summary>Editor wiring for a wheel that spins on while it tracks - <paramref name="tracking"/>
        /// degrees a second - turning <paramref name="direction"/> 1 or -1.</summary>
        public void Configure(DefensePost owner, Transform spinning, int count, float tracking, float direction)
        {
            Configure(owner, spinning, count);
            trackingSpin = tracking;
            sense = direction < 0f ? -1f : 1f;
        }
    }
}
