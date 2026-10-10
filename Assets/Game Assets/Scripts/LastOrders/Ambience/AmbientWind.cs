using UnityEngine;

namespace LastOrders.Ambience
{
    /// <summary>
    /// The level's wind: one steady breeze from a heading that veers slowly back and forth, with
    /// gusts on top that roll across the ground as fronts rather than switching on everywhere at once.
    ///
    /// The trees read it on the GPU (Environment/Shaders/AmbientWind.hlsl) from two globals set here
    /// every frame; everything else - the leaves and pollen it carries, the clouds' drift, the smoke
    /// plumes' lean - asks <see cref="StrengthAt"/>, which is the same sum as the shader's, so a gust
    /// the forest leans into is the gust that throws the leaves.
    ///
    /// Without one in the level the globals are zero and nothing moves.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    [DisallowMultipleComponent]
    public sealed class AmbientWind : MonoBehaviour
    {
        [Tooltip("Compass heading the wind blows TOWARD, degrees: 0 is north (+Z), 90 is east (+X).")]
        [SerializeField] float heading = 65f;

        [Tooltip("How far the heading swings either side, degrees.")]
        [SerializeField] float veer = 18f;

        [Tooltip("Seconds for one full swing of the heading.")]
        [SerializeField, Min(10f)] float veerSeconds = 150f;

        [Tooltip("The steady breeze, 0 to 1. Even a still day stirs the leaves.")]
        [SerializeField, Range(0f, 1f)] float breeze = 0.3f;

        [Tooltip("How much a gust adds to the breeze at its peak.")]
        [SerializeField, Range(0f, 1f)] float gusts = 0.6f;

        [Tooltip("Metres a second a gust front travels across the level.")]
        [SerializeField, Min(0.5f)] float gustFrontSpeed = 7f;

        static readonly int WindId = Shader.PropertyToID("_AmbWind");
        static readonly int ClockId = Shader.PropertyToID("_AmbWindClock");

        static float breezeNow, gustsNow, frontSpeed = 7f;

        /// <summary>True while a wind is blowing in the level.</summary>
        public static bool Active { get; private set; }

        /// <summary>Where the wind blows toward: flat and unit length.</summary>
        public static Vector3 Heading { get; private set; } = Vector3.forward;

        /// <summary>The wind's clock, seconds. The shader's gusts run on the same one.</summary>
        public static float Clock { get; private set; }

        /// <summary>
        /// The gust envelope at clock <paramref name="x"/>, 0 to 1: three slow sines summed and eased
        /// into separate swells. Must match AmbGustAt in AmbientWind.hlsl.
        /// </summary>
        public static float GustAt(float x)
        {
            float n = Mathf.Sin(x * 0.37f) * 0.5f + Mathf.Sin(x * 0.23f + 1.7f) * 0.35f + Mathf.Sin(x * 0.61f + 4.1f) * 0.15f;
            return Smoothstep(0.2f, 0.8f, n);
        }

        /// <summary>0 to about 1: the breeze plus whatever gust is passing <paramref name="point"/> now.</summary>
        public static float StrengthAt(Vector3 point)
        {
            if (!Active) return 0f;

            float along = point.x * Heading.x + point.z * Heading.z;
            return breezeNow + gustsNow * GustAt(Clock - along / frontSpeed);
        }

        /// <summary>
        /// The gust alone at <paramref name="point"/>, 0 to 1, <paramref name="ahead"/> seconds from now:
        /// what reaches a point upwind first.
        /// </summary>
        public static float GustAt(Vector3 point, float ahead = 0f)
        {
            if (!Active) return 0f;

            float along = point.x * Heading.x + point.z * Heading.z;
            return GustAt(Clock + ahead - along / frontSpeed);
        }

        /// <summary>The air's velocity at <paramref name="point"/>, metres a second, for something that
        /// rides it at <paramref name="atFull"/> m/s in a full wind.</summary>
        public static Vector3 VelocityAt(Vector3 point, float atFull) => Heading * (StrengthAt(point) * atFull);

        void OnEnable()
        {
            Active = true;
            Publish();
        }

        void OnDisable()
        {
            Active = false;

            // Globals outlive play mode in the editor: without this the trees go on leaning in edit mode.
            Shader.SetGlobalVector(WindId, Vector4.zero);
            Shader.SetGlobalVector(ClockId, Vector4.zero);
        }

        void Update() => Publish();

        void Publish()
        {
            Clock = Time.timeSinceLevelLoad;

            float yaw = (heading + veer * Mathf.Sin(Clock * Mathf.PI * 2f / veerSeconds)) * Mathf.Deg2Rad;
            Heading = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            breezeNow = breeze;
            gustsNow = gusts;
            frontSpeed = gustFrontSpeed;

            Shader.SetGlobalVector(WindId, new Vector4(Heading.x, Heading.z, breezeNow, gustsNow));
            Shader.SetGlobalVector(ClockId, new Vector4(Clock, frontSpeed, 0f, 0f));
        }

        /// <summary>The shader's smoothstep. Not <see cref="Mathf.SmoothStep"/>, which is an eased lerp.</summary>
        public static float Smoothstep(float from, float to, float x)
        {
            float t = Mathf.Clamp01((x - from) / (to - from));
            return t * t * (3f - 2f * t);
        }
    }
}
