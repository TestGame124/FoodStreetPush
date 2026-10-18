using System.Collections.Generic;
using LastOrders.Defense;
using UnityEngine;

namespace LastOrders.Audio
{
    /// <summary>
    /// What a defense sounds like: the pop of a crop cannon, the whump of the toastapult's arm, the
    /// clatter of the scrap turret - and, for anything thrown, the splat where it comes down.
    ///
    /// An add-on to a <see cref="DefensePost"/>, like <see cref="DefenseRecoil"/>: it listens to the
    /// post's <see cref="DefensePost.Fired"/> and needs nothing else. A thrown shot lands its
    /// flight time later, which the shot itself says, so the landing is heard when
    /// <see cref="DefenseProjectiles"/> shows it. A hitscan shot's hit is the zombie's own to sound.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DefenseSound : MonoBehaviour
    {
        [SerializeField] DefensePost post;

        [SerializeField] SoundId fire = SoundId.ShotFood;

        [Tooltip("Where a thrown shot comes down. None for hitscan.")]
        [SerializeField] SoundId impact = SoundId.ImpactSplat;

        readonly List<Landing> landings = new List<Landing>();

        struct Landing
        {
            public float At;
            public Vector3 Where;
        }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(DefensePost owner, SoundId shot, SoundId landing)
        {
            post = owner;
            fire = shot;
            impact = landing;
        }

        void OnEnable()
        {
            if (post == null) post = GetComponentInParent<DefensePost>();
            if (post != null) post.Fired += HandleFired;
        }

        void OnDisable()
        {
            if (post != null) post.Fired -= HandleFired;
            landings.Clear();
        }

        void HandleFired(Vector3 from, Vector3 to, DefenseShot shot)
        {
            SoundController.Play(fire, from);

            if (!shot.InFlight || impact == SoundId.None) return;
            landings.Add(new Landing { At = Time.time + shot.FlightSeconds, Where = to });
        }

        void Update()
        {
            for (int i = landings.Count - 1; i >= 0; i--)
            {
                if (Time.time < landings[i].At) continue;

                SoundController.Play(impact, landings[i].Where);
                landings.RemoveAt(i);
            }
        }
    }
}
