using System.Collections.Generic;
using LastOrders.Ambience;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LastOrders.Lighting
{
    /// <summary>
    /// Lights the safe zone at night: the yard inside the walls, the restaurant's room, the lamps
    /// by the gates and in the car park - every <see cref="NightLight"/> in the scene.
    ///
    /// All of them are painted into the sun's cookie, the texture URP multiplies the sun's light by
    /// wherever it lands. At night the sun is the moon, dim and blue; where a light is painted the
    /// cookie goes far above 1 - it is a half-float texture - and the moon there becomes lamplight
    /// on everything the sun lights: URP's Lit, and the level's Stylized, Toon and CropSprite
    /// shaders, the characters with them, shadows and all. Outside the lights the cookie is 1, and
    /// the night is as dark as it always was.
    ///
    /// Built for phones. No Unity light is added, so no surface pays for another light, and the
    /// shaders already sample this cookie for the cloud shadows by day. The cookie (512x512 at 8
    /// bytes a texel, 2 MB, released at dawn) is painted with a few quads only when something
    /// changes - a light moves or goes out, the sun turns or dims at dusk - never on a steady night.
    ///
    /// The lights are divided by the sun's colour as they are painted, so a lamp is the same
    /// brightness whatever the moon is doing. The cloud shadows hand the cookie over from dusk to
    /// dawn and are faded out of it as the lights come on, so neither pops.
    /// </summary>
    [DefaultExecutionOrder(1210)] // After DayNightLighting has set the sun and CloudShadows has slid its clouds.
    [DisallowMultipleComponent]
    public sealed class NightLights : MonoBehaviour
    {
        [Tooltip("The sun. Found automatically when left empty.")]
        [SerializeField] Light sun;

        [Tooltip("Hidden/LastOrders/NightLightCookie. Referenced here so builds include it.")]
        [SerializeField] Shader shader;

        [Tooltip("The cloud shadows, which hand the sun's cookie over at dusk. Found automatically when left empty.")]
        [SerializeField] CloudShadows clouds;

        [Tooltip("The ground the cookie covers (world X, Z) while there are still clouds in it at dusk. " +
                 "Once they have gone it covers only the lights, much more sharply.")]
        [SerializeField] Rect world = Rect.MinMaxRect(-80f, -55f, 116f, 106f);

        [Tooltip("World height of the ground the lights fall on.")]
        [SerializeField] float ground;

        [Tooltip("Texels along each side of the cookie.")]
        [SerializeField, Range(128, 1024)] int resolution = 512;

        [Tooltip("Darkness (0 day, 1 night) over which the lights come on. Dawn is 0.25, dusk 0.6.")]
        [SerializeField] Vector2 switchOn = new Vector2(0.3f, 0.75f);

        [Tooltip("Darkness over which the cloud shadows fade out of the cookie as the lights take it over.")]
        [SerializeField] Vector2 cloudsFade = new Vector2(0.25f, 0.6f);

        [Tooltip("Seconds between checks on whether a light has moved, grown, come on or gone out.")]
        [SerializeField, Min(0.05f)] float recheckSeconds = 0.25f;

        [Tooltip("Metres of plain moonlight kept round the lights in the cookie, so its clamped edge is unlit.")]
        [SerializeField, Min(0.5f)] float margin = 3f;

        static readonly int NightGlowId = Shader.PropertyToID("_EnvNightGlow");
        static readonly int WorldToLightId = Shader.PropertyToID("_NightWorldToLight");
        static readonly int CookieRectId = Shader.PropertyToID("_NightCookieRect");
        static readonly int GroundId = Shader.PropertyToID("_NightGround");
        static readonly int CentreId = Shader.PropertyToID("_Centre");
        static readonly int ShapeId = Shader.PropertyToID("_Shape");
        static readonly int ColourId = Shader.PropertyToID("_Colour");
        static readonly int PoolsId = Shader.PropertyToID("_Pools");
        static readonly int CloudTexId = Shader.PropertyToID("_CloudTex");
        static readonly int CloudUVId = Shader.PropertyToID("_CloudUV");

        const int FillPass = 0, LampPass = 1, BasePass = 2;

        /// <summary>The dimmest the sun is taken to be when the lights are divided by it.</summary>
        const float DimmestSun = 0.02f;

        readonly List<Footprint> drawn = new List<Footprint>();
        readonly List<Footprint> gathered = new List<Footprint>();

        UniversalAdditionalLightData lightData;
        Material material;
        Mesh quad;
        CommandBuffer buffer;
        MaterialPropertyBlock block;
        RenderTexture cookie;
        GraphicsFormat format = GraphicsFormat.None;

        /// <summary>The sun's cookie is ours: the lights are on.</summary>
        bool owning;

        Texture authoredCookie;
        Vector2 authoredSize, authoredOffset;

        float nextCheck;
        bool lightsChanged = true;
        bool painted;
        Look paintedLook;

        /// <summary>Everything the cookie was painted with, bar the lights themselves.</summary>
        struct Look
        {
            public float On, Clouds;
            public Color Sun;
            public Quaternion Turn;
            public Rect Area;
            public Vector2 CloudOffset;

            public bool Same(Look other) =>
                Mathf.Abs(On - other.On) < 0.002f && Mathf.Abs(Clouds - other.Clouds) < 0.002f &&
                Mathf.Abs(Sun.r - other.Sun.r) < 0.001f && Mathf.Abs(Sun.g - other.Sun.g) < 0.001f &&
                Mathf.Abs(Sun.b - other.Sun.b) < 0.001f && Quaternion.Angle(Turn, other.Turn) < 0.05f &&
                Area == other.Area && (CloudOffset - other.CloudOffset).sqrMagnitude < 1e-4f;
        }

        /// <summary>How many times the cookie has been painted since the game started: a handful a
        /// night, plus every frame of a dusk or dawn blend - never every frame of a steady night.</summary>
        public static int Paints { get; private set; }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(Light directional, Shader cookieShader, CloudShadows cloudShadows, Rect ground)
        {
            sun = directional;
            shader = cookieShader;
            clouds = cloudShadows;
            world = ground;
        }

        void OnEnable()
        {
            if (sun == null) sun = RenderSettings.sun;
            if (sun == null) sun = FindSun();
            if (clouds == null) clouds = FindFirstObjectByType<CloudShadows>();
            if (shader == null) shader = Shader.Find("Hidden/LastOrders/NightLightCookie");

            if (sun == null || shader == null || !shader.isSupported)
            {
                Debug.LogWarning("[NightLights] No sun or no cookie shader, so the safe zone stays unlit at night.", this);
                enabled = false;
                return;
            }

            format = PickFormat();
            if (format == GraphicsFormat.None)
            {
                Debug.LogWarning("[NightLights] This device can't render a half-float cookie, so the safe zone stays unlit at night.", this);
                enabled = false;
                return;
            }

            if (!sun.TryGetComponent(out lightData)) lightData = sun.gameObject.AddComponent<UniversalAdditionalLightData>();

            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            quad = Quad();
            buffer = new CommandBuffer { name = "Night lights" };
            block = new MaterialPropertyBlock();
            lightsChanged = true;
        }

        void OnDisable()
        {
            Release();

            if (material != null) Destroy(material);
            if (quad != null) Destroy(quad);
            buffer?.Release();

            material = null;
            quad = null;
            buffer = null;
            drawn.Clear();
        }

        void LateUpdate()
        {
            float darkness = Mathf.Clamp01(Shader.GetGlobalFloat(NightGlowId));
            float on = Ramp(switchOn, darkness);

            if (on <= 0f)
            {
                Release();
                return;
            }

            Acquire();

            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + recheckSeconds;
                if (Gather()) lightsChanged = true;
            }

            bool cloudsShown = clouds != null && clouds.isActiveAndEnabled && clouds.Shade != null;
            float cloudShade = cloudsShown ? 1f - Ramp(cloudsFade, darkness) : 0f;

            var look = new Look
            {
                On = on,
                Clouds = cloudShade,
                Sun = SunLight(sun),
                Turn = sun.transform.rotation,
                Area = cloudShade > 0f ? world : LitArea(),
                CloudOffset = cloudShade > 0f ? clouds.Offset : Vector2.zero,
            };

            // A phone app sent to the background can lose its render textures' contents.
            bool lost = !cookie.IsCreated();
            if (lost) cookie.Create();

            if (painted && !lost && !lightsChanged && look.Same(paintedLook) && sun.cookie == cookie) return;

            Paint(look);
            paintedLook = look;
            painted = true;
            lightsChanged = false;
        }

        // --- The cookie --------------------------------------------------------------------------

        void Paint(Look look)
        {
            Matrix4x4 worldToLight = WorldToLight(sun.transform);
            Rect area = LightSpaceRect(worldToLight, look.Area, ground);
            Vector3 gain = Gain(look.Sun, look.On);

            material.SetMatrix(WorldToLightId, worldToLight);
            material.SetVector(CookieRectId, new Vector4(area.xMin, area.yMin, 1f / area.width, 1f / area.height));
            material.SetFloat(GroundId, ground);

            if (look.Clouds > 0f)
            {
                material.SetTexture(CloudTexId, clouds.Shade);
                material.SetVector(CloudUVId, new Vector4(look.CloudOffset.x, look.CloudOffset.y, 1f / clouds.TileMetres, look.Clouds));
            }
            else
            {
                material.SetVector(CloudUVId, Vector4.zero);
            }

            buffer.Clear();
            buffer.SetRenderTarget(cookie);
            buffer.ClearRenderTarget(false, true, Color.clear);

            // Fills first, the brightest winning where they overlap, then the lamps on top of them.
            foreach (Footprint light in drawn)
                if (light.IsFill) Draw(light, gain, FillPass);

            foreach (Footprint light in drawn)
                if (!light.IsFill) Draw(light, gain, LampPass);

            buffer.DrawMesh(quad, Matrix4x4.identity, material, 0, BasePass);
            Graphics.ExecuteCommandBuffer(buffer);
            Paints++;

            sun.cookie = cookie;
            lightData.lightCookieSize = area.size;
            lightData.lightCookieOffset = area.center;
        }

        void Draw(Footprint light, Vector3 gain, int pass)
        {
            Color colour = QualitySettings.activeColorSpace == ColorSpace.Linear ? light.Light.linear : light.Light;
            Vector2 pools = light.FittedPoolSpacing;

            block.SetVector(CentreId, new Vector4(light.Centre.x, light.Centre.y, light.Axis.x, light.Axis.y));
            block.SetVector(ShapeId, new Vector4(light.Half.x, light.Half.y, light.Edge, light.IsRound ? 1f : 0f));
            block.SetVector(ColourId, new Vector4(colour.r * gain.x, colour.g * gain.y, colour.b * gain.z, 0f));
            block.SetVector(PoolsId, new Vector4(pools.x, pools.y, light.PoolStrength, 0f));

            // The buffer copies the block's values as the draw is recorded, so one block serves every light.
            buffer.DrawMesh(quad, Matrix4x4.identity, material, 0, pass, block);
        }

        /// <summary>The ground the lights cover, with a margin of plain moonlight round them.</summary>
        Rect LitArea()
        {
            if (drawn.Count == 0) return world;

            Rect area = drawn[0].Bounds;
            for (int i = 1; i < drawn.Count; i++)
            {
                Rect b = drawn[i].Bounds;
                area = Rect.MinMaxRect(Mathf.Min(area.xMin, b.xMin), Mathf.Min(area.yMin, b.yMin),
                                       Mathf.Max(area.xMax, b.xMax), Mathf.Max(area.yMax, b.yMax));
            }

            // Rounded out to whole metres, so a light nudged a little doesn't move the whole cookie.
            return Rect.MinMaxRect(Mathf.Floor(area.xMin - margin), Mathf.Floor(area.yMin - margin),
                                   Mathf.Ceil(area.xMax + margin), Mathf.Ceil(area.yMax + margin));
        }

        /// <summary>True when the lights giving light are not the ones last painted.</summary>
        bool Gather()
        {
            gathered.Clear();
            foreach (NightLight light in NightLight.Active)
                if (light.TryGetFootprint(out Footprint footprint)) gathered.Add(footprint);

            bool same = gathered.Count == drawn.Count;
            for (int i = 0; same && i < gathered.Count; i++)
                same = gathered[i].Equals(drawn[i]);

            if (same) return false;

            drawn.Clear();
            drawn.AddRange(gathered);
            return true;
        }

        // --- Taking the sun's cookie, and giving it back -----------------------------------------

        void Acquire()
        {
            if (owning) return;
            owning = true;

            cookie = new RenderTexture(new RenderTextureDescriptor(resolution, resolution, format, 0))
            {
                name = "NightLightsCookie",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            cookie.Create();

            if (clouds != null && clouds.isActiveAndEnabled && !clouds.Yielded)
            {
                // The clouds hold the level's own cookie; it is theirs to put back.
                clouds.GetAuthored(out authoredCookie, out authoredSize, out authoredOffset);
                clouds.Yield(true);
            }
            else
            {
                authoredCookie = sun.cookie;
                authoredSize = lightData.lightCookieSize;
                authoredOffset = lightData.lightCookieOffset;
            }

            painted = false;
            lightsChanged = true;
            nextCheck = 0f;
        }

        void Release()
        {
            if (!owning) return;
            owning = false;
            painted = false;

            bool cloudsBack = false;
            if (clouds != null && clouds.Yielded)
            {
                cloudsBack = clouds.isActiveAndEnabled;
                clouds.Yield(false);
            }

            if (!cloudsBack && sun != null)
            {
                sun.cookie = authoredCookie;
                lightData.lightCookieSize = authoredSize;
                lightData.lightCookieOffset = authoredOffset;
            }

            if (cookie != null)
            {
                cookie.Release();
                Destroy(cookie);
                cookie = null;
            }
        }

        // --- Pure workings, public for the tests -------------------------------------------------

        /// <summary>The shader's smoothstep of <paramref name="t"/> across the range's x..y.</summary>
        public static float Ramp(Vector2 range, float t)
        {
            float x = Mathf.Clamp01((t - range.x) / Mathf.Max(range.y - range.x, 1e-5f));
            return x * x * (3f - 2f * x);
        }

        /// <summary>
        /// What each colour channel of a light is multiplied by as it goes into the cookie: the
        /// sun's own light is multiplied by the cookie again in every shader, so a light divided by
        /// it comes out as itself, at <paramref name="on"/> of full.
        /// </summary>
        public static Vector3 Gain(Color sunLight, float on) => new Vector3(
            on / Mathf.Max(sunLight.r, DimmestSun),
            on / Mathf.Max(sunLight.g, DimmestSun),
            on / Mathf.Max(sunLight.b, DimmestSun));

        /// <summary>The sun's light as URP hands it to the shaders: colour, temperature and intensity.</summary>
        public static Color SunLight(Light light)
        {
            Color colour = QualitySettings.activeColorSpace == ColorSpace.Linear ? light.color.linear : light.color;
            if (GraphicsSettings.lightsUseColorTemperature && light.useColorTemperature)
                colour *= Mathf.CorrelatedColorTemperatureToRGB(light.colorTemperature);

            colour *= light.intensity;
            colour.a = 1f;
            return colour;
        }

        /// <summary>World to the sun's own space, as URP builds its cookie lookup: position and turn, no scale.</summary>
        public static Matrix4x4 WorldToLight(Transform light) =>
            Matrix4x4.TRS(light.position, light.rotation, Vector3.one).inverse;

        /// <summary>
        /// The rectangle, in the sun's own right and up metres, that holds a world rectangle of
        /// ground at the given height: what the cookie has to cover to reach all of it.
        /// </summary>
        public static Rect LightSpaceRect(Matrix4x4 worldToLight, Rect area, float height)
        {
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;

            for (int corner = 0; corner < 4; corner++)
            {
                float x = (corner & 1) == 0 ? area.xMin : area.xMax;
                float z = (corner & 2) == 0 ? area.yMin : area.yMax;
                Vector3 p = worldToLight.MultiplyPoint3x4(new Vector3(x, height, z));

                xMin = Mathf.Min(xMin, p.x);
                yMin = Mathf.Min(yMin, p.y);
                xMax = Mathf.Max(xMax, p.x);
                yMax = Mathf.Max(yMax, p.y);
            }

            // Never thinner than a metre, so the shader's 1 / size stays finite.
            float cx = (xMin + xMax) * 0.5f, cy = (yMin + yMax) * 0.5f;
            float w = Mathf.Max(xMax - xMin, 1f), h = Mathf.Max(yMax - yMin, 1f);
            return new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);
        }

        static GraphicsFormat PickFormat()
        {
            foreach (GraphicsFormat candidate in new[] { GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormat.B10G11R11_UFloatPack32 })
            {
                if (SystemInfo.IsFormatSupported(candidate, GraphicsFormatUsage.Render) &&
                    SystemInfo.IsFormatSupported(candidate, GraphicsFormatUsage.Blend) &&
                    SystemInfo.IsFormatSupported(candidate, GraphicsFormatUsage.Linear))
                    return candidate;
            }

            return GraphicsFormat.None;
        }

        static Mesh Quad()
        {
            var mesh = new Mesh { name = "NightLightQuad", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(-1f, 1f, 0f) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);

            // Drawn straight into the cookie, never culled against a camera.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            return mesh;
        }

        /// <summary>The brightest directional light in the scene, which is what a sun is.</summary>
        static Light FindSun()
        {
            Light best = null;
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                if (best == null || light.intensity > best.intensity) best = light;
            }

            return best;
        }
    }
}
