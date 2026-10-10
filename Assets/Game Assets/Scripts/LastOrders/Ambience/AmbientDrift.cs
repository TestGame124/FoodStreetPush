using UnityEngine;
using Random = UnityEngine.Random;

namespace LastOrders.Ambience
{
    /// <summary>
    /// What the wind carries: seed fluff and pollen drifting through the sunlight by day; a scatter of
    /// dead leaves and scraps of paper thrown across the ground by each gust, tumbling and skittering
    /// to a stop; and the smoke of the fires burning out in the suburbs, leaning with the wind.
    ///
    /// The pollen's cloud and the leaves' throw follow the view. Each gust is seen coming: when the
    /// front about to cross the view rises past a swell, leaves are thrown in from the upwind side,
    /// so they arrive with the trees' lean. The particle systems are built by the Last Orders /
    /// Ambience setup; this only steers them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbientDrift : MonoBehaviour
    {
        [Header("Pollen")]
        [Tooltip("Kept playing in world space; follows the view.")]
        [SerializeField] ParticleSystem motes;

        [Tooltip("Motes a second by day.")]
        [SerializeField] float moteRate = 10f;

        [Header("Leaves")]
        [Tooltip("Emitted into by hand, a handful each gust.")]
        [SerializeField] ParticleSystem leaves;

        [SerializeField] Vector2Int leavesPerGust = new Vector2Int(6, 12);

        [Tooltip("The gust strength that throws leaves.")]
        [SerializeField, Range(0f, 1f)] float gustThreshold = 0.55f;

        [Tooltip("Metres a second a leaf is carried at full wind.")]
        [SerializeField] float leafSpeed = 4.5f;

        [Header("Smoke")]
        [SerializeField] ParticleSystem[] plumes;

        [Tooltip("What each plume is burning: a wreck, a fallen house. When it is cleared away the fire goes out.")]
        [SerializeField] Renderer[] burning;

        [Tooltip("Metres a second the smoke drifts downwind at full wind: against its climb, how far it leans.")]
        [SerializeField] float smokeLean = 1f;

        [Tooltip("Glow puffs a second at a fire's foot at night; a few by day, when they barely show.")]
        [SerializeField] float glowRate = 6f;

        float lastGust;
        float nextSteer;

        void Start()
        {
            if (motes != null) motes.Play();
            if (leaves != null) leaves.Play();
            if (plumes != null)
                foreach (ParticleSystem plume in plumes)
                    if (plume != null) plume.Play();
        }

        void Update()
        {
            Vector3 focus = AmbientView.Focus;
            float day = AmbientDaylight.Day;

            if (motes != null)
            {
                motes.transform.position = focus;
                var emission = motes.emission;
                emission.rateOverTime = moteRate * day;
            }

            // Steering the systems by wind is a few module writes: not every frame.
            if ((nextSteer -= Time.deltaTime) <= 0f)
            {
                nextSteer = 0.25f;
                // The leaves are only thrown: the gust's push is in the speed they are emitted at, and
                // they slow to a stop skidding along the ground.
                Steer(motes, AmbientWind.VelocityAt(focus, 1.4f));

                if (plumes != null)
                    for (int i = 0; i < plumes.Length; i++)
                    {
                        ParticleSystem plume = plumes[i];
                        if (plume == null) continue;

                        Steer(plume, AmbientWind.VelocityAt(plume.transform.position, smokeLean));
                        Glow(plume);
                        bool alight = burning == null || i >= burning.Length || Burning(burning[i]);
                        if (alight != plume.isEmitting)
                        {
                            if (alight) plume.Play(true);
                            else plume.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                        }
                    }
            }

            Gusts(focus, day);
        }

        /// <summary>A handful of leaves as each gust front reaches the upwind edge of the view.</summary>
        void Gusts(Vector3 focus, float day)
        {
            if (leaves == null) return;

            float reach = AmbientView.Reach;
            Vector3 heading = AmbientWind.Heading;
            Vector3 upwind = focus - heading * (reach * 0.9f);
            float gust = AmbientWind.GustAt(upwind);

            if (gust > gustThreshold && lastGust <= gustThreshold && day > 0.3f)
            {
                Vector3 across = new Vector3(-heading.z, 0f, heading.x);
                int count = Random.Range(leavesPerGust.x, leavesPerGust.y + 1);
                var emit = new ParticleSystem.EmitParams();
                for (int i = 0; i < count; i++)
                {
                    emit.position = upwind + across * Random.Range(-reach * 0.8f, reach * 0.8f) - heading * Random.Range(0f, 6f) +
                                    Vector3.up * Random.Range(0.3f, 2.2f);
                    emit.velocity = heading * leafSpeed * Random.Range(0.7f, 1.2f) + across * Random.Range(-0.6f, 0.6f) +
                                    Vector3.up * Random.Range(0.3f, 1.4f);
                    emit.applyShapeToPosition = false;
                    leaves.Emit(emit, 1);
                }
            }

            lastGust = gust;
        }

        /// <summary>Carries a system's particles with the wind: its velocity, in world space.</summary>
        static void Steer(ParticleSystem system, Vector3 wind)
        {
            if (system == null) return;

            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(wind.x * 0.8f, wind.x * 1.2f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(wind.z * 0.8f, wind.z * 1.2f);
        }

        /// <summary>The fire's glow at the plume's foot: strong in the dark, faint by day.</summary>
        void Glow(ParticleSystem plume)
        {
            Transform glow = plume.transform.Find("Glow");
            if (glow == null || !glow.TryGetComponent(out ParticleSystem system)) return;

            var emission = system.emission;
            emission.rateOverTime = glowRate * Mathf.Lerp(0.15f, 1f, AmbientDaylight.Night);
        }

        /// <summary>A fire burns while what it is burning is still there: an obstacle cleared off a plot is gone.</summary>
        static bool Burning(Renderer fuel) => fuel != null && fuel.enabled && fuel.gameObject.activeInHierarchy;

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(ParticleSystem pollen, ParticleSystem gustLeaves, ParticleSystem[] smoke, Renderer[] fuel)
        {
            motes = pollen;
            leaves = gustLeaves;
            plumes = smoke;
            burning = fuel;
        }
    }
}
