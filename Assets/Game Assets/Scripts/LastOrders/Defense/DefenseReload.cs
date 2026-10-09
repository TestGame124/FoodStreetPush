using UnityEngine;

namespace LastOrders.Defense
{
    /// <summary>
    /// The round a defense shows waiting in its breech - a cob between the corn shooter's wheels -
    /// gone the moment it is fired and back once the next is loaded.
    ///
    /// An add-on to a <see cref="DefensePost"/>, like <see cref="DefenseRecoil"/>: it listens to the
    /// post's shots, and takes how long reloading looks from the weapon's time between shots and the
    /// post's <see cref="Data.DefenseMotionProfile.reloadShowsAt"/>.
    /// </summary>
    public sealed class DefenseReload : MonoBehaviour
    {
        [SerializeField] DefensePost post;

        [Tooltip("The round waiting to be fired: hidden as it goes, shown again once reloaded.")]
        [SerializeField] GameObject loaded;

        float sinceShot = float.MaxValue;

        void Awake()
        {
            if (post == null) post = GetComponentInParent<DefensePost>();
        }

        void OnEnable()
        {
            if (post != null) post.Fired += HandleFired;
        }

        void OnDisable()
        {
            if (post != null) post.Fired -= HandleFired;

            sinceShot = float.MaxValue;
            if (loaded != null) loaded.SetActive(true);
        }

        void HandleFired(Vector3 from, Vector3 to, DefenseShot shot)
        {
            if (loaded == null) return;

            loaded.SetActive(false);
            sinceShot = 0f;
        }

        void Update()
        {
            if (sinceShot == float.MaxValue) return;

            sinceShot += Time.deltaTime;

            float between = post != null && post.Unit != null && post.Unit.Weapon != null ? post.Unit.Weapon.secondsBetweenShots : 1f;
            float share = post != null && post.Motion != null ? post.Motion.reloadShowsAt : 0.5f;
            if (sinceShot < between * share) return;

            loaded.SetActive(true);
            sinceShot = float.MaxValue;
        }

        /// <summary>Editor wiring, for the prefab builder.</summary>
        public void Configure(DefensePost owner, GameObject round)
        {
            post = owner;
            loaded = round;
        }
    }
}
