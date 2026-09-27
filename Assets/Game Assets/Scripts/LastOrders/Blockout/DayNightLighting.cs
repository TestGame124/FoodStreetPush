using LastOrders.Core;
using LastOrders.DayCycle;
using UnityEngine;

namespace LastOrders.Blockout
{
    /// <summary>
    /// Makes the phase visible: the sun swings and dims, and the ambient cools.
    ///
    /// Without this, "switch to night" changes a label on the HUD and nothing else, which
    /// makes the whole cycle impossible to judge by eye - and judging by eye is the entire
    /// reason to have a day/night toggle. Two lerps on the scene's own directional light is
    /// the cheapest honest version of that.
    ///
    /// Deliberately drives the light the LEVEL already has rather than creating one. The
    /// level's lighting is authored, its shadows are set up, and replacing it to show a phase
    /// would trade a real look for a debuggable one.
    /// </summary>
    [DefaultExecutionOrder(1195)]
    public sealed class DayNightLighting : MonoBehaviour
    {
        [Tooltip("The scene's sun. Found automatically when left empty.")]
        [SerializeField] Light sun;

        [Header("Day")]
        [SerializeField] Color dayColour = new Color(1f, 0.96f, 0.88f);
        [SerializeField] float dayIntensity = 1.1f;
        [SerializeField] Vector3 dayAngles = new Vector3(50f, 30f, 0f);
        [SerializeField] Color dayAmbient = new Color(0.55f, 0.57f, 0.60f);

        [Header("Night")]
        [SerializeField] Color nightColour = new Color(0.44f, 0.54f, 0.78f);
        [SerializeField] float nightIntensity = 0.22f;
        [SerializeField] Vector3 nightAngles = new Vector3(14f, 200f, 0f);
        [SerializeField] Color nightAmbient = new Color(0.12f, 0.14f, 0.22f);

        [Tooltip("Seconds to cross between the two. Long enough to read as dusk falling, " +
                 "short enough that a test toggle is not a wait.")]
        [SerializeField] float blendSeconds = 1.5f;

        [SerializeField] GameLifetimeScope scope;

        GameComposition composition;

        /// <summary>0 is full day, 1 is full night. Eased toward the target every frame so a
        /// phase change reads as the light changing rather than as a cut.</summary>
        float darkness;

        float targetDarkness;

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            if (sun == null) sun = FindSun();

            composition = scope != null ? scope.Composition : null;

            if (composition != null)
            {
                composition.Machine.OnPhaseChanged += HandlePhaseChanged;
                targetDarkness = DarknessFor(composition.Machine.CurrentPhase);
            }

            // Starts already at the target rather than blending in from day, so the first
            // frame of a scene that opens at night is night.
            darkness = targetDarkness;
            Apply(darkness);
        }

        void OnDestroy()
        {
            if (composition != null) composition.Machine.OnPhaseChanged -= HandlePhaseChanged;
        }

        void Update()
        {
            if (Mathf.Approximately(darkness, targetDarkness)) return;

            float step = blendSeconds > 0f ? Time.deltaTime / blendSeconds : 1f;
            darkness = Mathf.MoveTowards(darkness, targetDarkness, step);

            Apply(darkness);
        }

        void HandlePhaseChanged(DayPhase previous, DayPhase current) =>
            targetDarkness = DarknessFor(current);

        /// <summary>
        /// How dark each phase is.
        ///
        /// Dusk sits between the two rather than at night, because dusk is the forty-five
        /// seconds the player spends repositioning and it should look like the light going
        /// rather than like the light gone.
        /// </summary>
        static float DarknessFor(DayPhase phase) => phase switch
        {
            DayPhase.Dawn => 0.25f,
            DayPhase.Day => 0f,
            DayPhase.Dusk => 0.6f,
            DayPhase.Night => 1f,
            DayPhase.Resolution => 0.75f,
            _ => 0f,
        };

        void Apply(float t)
        {
            RenderSettings.ambientLight = Color.Lerp(dayAmbient, nightAmbient, t);

            if (sun == null) return;

            sun.color = Color.Lerp(dayColour, nightColour, t);
            sun.intensity = Mathf.Lerp(dayIntensity, nightIntensity, t);
            sun.transform.rotation = Quaternion.Euler(Vector3.Lerp(dayAngles, nightAngles, t));
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

        /// <summary>Captures the level's authored lighting as the DAY end of the blend, so the
        /// scene looks exactly as it did before this component was added.</summary>
        public void Configure(GameLifetimeScope lifetimeScope, Light directional)
        {
            scope = lifetimeScope;
            sun = directional;

            if (sun != null)
            {
                dayColour = sun.color;
                dayIntensity = sun.intensity;
                dayAngles = sun.transform.rotation.eulerAngles;
            }

            dayAmbient = RenderSettings.ambientLight;
        }
    }
}
