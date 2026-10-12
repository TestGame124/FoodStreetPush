using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LastOrders.Ambience
{
    /// <summary>
    /// Cloud shadows: soft patches of shade drifting over the whole level with the wind, the sun
    /// dimming and brightening on everything they cross - the ground, the walls, the diner, the people.
    ///
    /// It is the sun's own cookie, so every surface the sun lights is shaded by it with nothing
    /// extra drawn: URP's Lit and the level's Stylized, Toon and CropSprite shaders all read it. The
    /// texture tiles, and is slid along with the wind each frame. Put back as it was when this is
    /// switched off, so the level's lighting is never left with a cookie it did not author.
    ///
    /// From dusk to dawn it yields the cookie to the night lights (<c>LastOrders.Lighting.NightLights</c>),
    /// which fade these clouds out of their own cookie as the lamps come on.
    /// </summary>
    [DefaultExecutionOrder(1200)] // After DayNightLighting has turned the sun for the frame.
    [DisallowMultipleComponent]
    public sealed class CloudShadows : MonoBehaviour
    {
        [Tooltip("The sun. Found automatically when left empty.")]
        [SerializeField] Light sun;

        [Tooltip("Tiling cloud shade: white where the sun is clear, grey under a cloud.")]
        [SerializeField] Texture2D clouds;

        [Tooltip("Metres of ground one tile of the texture covers.")]
        [SerializeField, Min(10f)] float tileMetres = 170f;

        [Tooltip("Metres a second the clouds drift in a still breeze...")]
        [SerializeField, Min(0f)] float calmDrift = 0.9f;

        [Tooltip("...and in a full gust. Clouds are high up: they move steadily, not in the gusts' jerks.")]
        [SerializeField, Min(0f)] float windyDrift = 2.2f;

        Texture authoredCookie;
        Vector2 authoredSize, authoredOffset;
        UniversalAdditionalLightData lightData;
        Vector3 travelled;
        float drift;
        bool applied;

        /// <summary>
        /// True while something else is drawing the sun's cookie - the night lights, from dusk to
        /// dawn. The clouds keep drifting meanwhile and are left alone on the light, so they come
        /// back where the wind has taken them.
        /// </summary>
        public bool Yielded { get; private set; }

        /// <summary>The tiling shade texture, for whatever draws the cookie while the clouds have yielded.</summary>
        public Texture Shade => clouds;

        public float TileMetres => tileMetres;

        /// <summary>The cookie offset the clouds have now: how far they have drifted along the sun's own right and up.</summary>
        public Vector2 Offset
        {
            get
            {
                Transform light = sun.transform;
                return -new Vector2(Vector3.Dot(travelled, light.right), Vector3.Dot(travelled, light.up));
            }
        }

        /// <summary>The sun's cookie as the level had it before the clouds - what to put back if they go.</summary>
        public void GetAuthored(out Texture cookie, out Vector2 size, out Vector2 offset)
        {
            cookie = authoredCookie;
            size = authoredSize;
            offset = authoredOffset;
        }

        /// <summary>Hands the sun's cookie over (true) or takes it back with the clouds where they have drifted to (false).</summary>
        public void Yield(bool yield)
        {
            if (yield == Yielded) return;
            Yielded = yield;

            if (yield || !applied) return;

            sun.cookie = clouds;
            lightData.lightCookieSize = new Vector2(tileMetres, tileMetres);
            Slide();
        }

        void OnEnable()
        {
            if (sun == null) sun = RenderSettings.sun;
            if (sun == null || clouds == null) return;

            lightData = sun.GetComponent<UniversalAdditionalLightData>();
            if (lightData == null) return;

            applied = true;

            // While yielded the light carries someone else's cookie, which is not the level's own.
            if (Yielded) return;

            authoredCookie = sun.cookie;
            authoredSize = lightData.lightCookieSize;
            authoredOffset = lightData.lightCookieOffset;

            sun.cookie = clouds;
            lightData.lightCookieSize = new Vector2(tileMetres, tileMetres);
            Slide();
        }

        void OnDisable()
        {
            if (!applied) return;
            applied = false;

            // Whoever the clouds yielded to owns the light now and will put it back itself.
            if (Yielded) return;

            if (sun != null) sun.cookie = authoredCookie;
            if (lightData != null)
            {
                lightData.lightCookieSize = authoredSize;
                lightData.lightCookieOffset = authoredOffset;
            }
        }

        void LateUpdate()
        {
            if (!applied) return;

            // Eased toward the wind, so the clouds speed up and slow down over seconds.
            float target = Mathf.Lerp(calmDrift, windyDrift, AmbientWind.StrengthAt(AmbientView.Focus));
            drift = Mathf.MoveTowards(drift, target, Time.deltaTime * 0.3f);
            travelled += AmbientWind.Heading * (drift * Time.deltaTime);

            // Keep the numbers small: the pattern repeats every tile anyway.
            travelled.x = Mathf.Repeat(travelled.x, tileMetres * 64f);
            travelled.z = Mathf.Repeat(travelled.z, tileMetres * 64f);
            if (!Yielded) Slide();
        }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(Light light, Texture2D shade)
        {
            sun = light;
            clouds = shade;
        }

        /// <summary>
        /// The cookie is laid across the sun's own right and up axes, so the world distance the clouds
        /// have travelled is measured along those to give the offset. Re-done every frame, as the sun
        /// turns between day and night.
        /// </summary>
        void Slide()
        {
            Transform light = sun.transform;
            lightData.lightCookieOffset = -new Vector2(Vector3.Dot(travelled, light.right), Vector3.Dot(travelled, light.up));
        }
    }
}
