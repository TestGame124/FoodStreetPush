using System.Collections.Generic;
using System.Text;
using LastOrders.Ambience;
using LastOrders.Blockout;
using LastOrders.Lighting;
using LastOrders.Perimeter;
using LastOrders.Restaurant;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Lights the safe zone at night (see <see cref="NightLights"/>): one root, <c>NightLights</c>,
    /// that paints every <see cref="NightLight"/> into the sun's cookie after dark, and the lights
    /// themselves on what is already in the scene -
    ///
    /// - every compound zone: a cool, even yard light over the ground inside the walls, spilling a
    ///   few metres past them onto the defense ring; a plot's zone lights up once it is claimed;
    /// - the restaurant's floor: a warm room light fitted over it as it grows, with a soft pool
    ///   under each ceiling light;
    /// - the lamps on the compound's gates and in the car park: warm pools under them.
    ///
    /// The moon is turned high and toward the camera, which the cookie needs (see DayNightLighting).
    /// Re-runnable: everything is set to the numbers here again. The wall and car park builders add
    /// the same lights when they rebuild (<see cref="Yard"/>, <see cref="GateLamp"/>, <see cref="CarParkLamp"/>).
    /// </summary>
    internal static class NightLightsSetup
    {
        const string RootName = "NightLights";
        const string ShaderPath = "Assets/Game Assets/Content/LastOrders/Environment/Shaders/NightLightCookie.shader";

        // The yard: a cool, moonlit white, about a third of a clear day's sun - plainly lit, still night.
        static readonly Color YardColour = new Color(0.7f, 0.8f, 1f);
        const float YardIntensity = 0.36f;
        const float YardSpill = 4f;

        // The restaurant: warm indoor light, brighter than the yard - the room is where the eye goes.
        static readonly Color RoomColour = new Color(1f, 0.85f, 0.64f);
        const float RoomIntensity = 0.75f;
        const float RoomSpill = 1.6f;
        const float CeilingSpacing = 3.4f;
        const float CeilingStrength = 0.9f;

        // The lamps: sodium-warm pools. The car park's lamps already throw an added glow pool of
        // their own (CarParkModel), so their light is kept lower or the asphalt burns out.
        static readonly Color GateLampColour = new Color(1f, 0.76f, 0.44f);
        const float GateLampIntensity = 0.65f;
        const float GateLampReach = 6f;

        static readonly Color CarParkLampColour = new Color(1f, 0.82f, 0.55f);
        const float CarParkLampIntensity = 0.45f;
        const float CarParkLampReach = 7.5f;

        // The moon, for the cookie: high and from the camera's side. Before: 14 degrees up, from behind.
        const float NightIntensity = 0.13f;
        static readonly Vector3 NightAngles = new Vector3(60f, 0f, 0f);
        const float PreviousNightIntensity = 0.22f;
        static readonly Vector3 PreviousNightAngles = new Vector3(14f, 200f, 0f);

        [MenuItem("Last Orders/Lighting/Set Up Night Lights (Current Scene)", priority = 100)]
        static void Run() => Debug.Log("[Last Orders] " + Apply());

        [MenuItem("Last Orders/Lighting/Remove Night Lights (Current Scene)", priority = 101)]
        static void RunRemove() => Debug.Log("[Last Orders] " + Remove());

        internal static string Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            var report = new StringBuilder("Night lights:\n");

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) return $"Night lights: no shader at {ShaderPath}; nothing was changed.";

            Light sun = Sun();
            if (sun == null) return "Night lights: the scene has no directional light; nothing was changed.";

            foreach (GameObject existing in scene.GetRootGameObjects())
                if (existing.name == RootName) Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Rect world = NeighborhoodBuilder.Inflate(EnvironmentLayout.Clear, 4f);
            root.AddComponent<NightLights>().Configure(sun, shader, Object.FindFirstObjectByType<CloudShadows>(), world);
            report.Append($"  Root '{RootName}' painting into '{sun.name}''s cookie, {world.width:0} x {world.height:0} m at dusk.\n");

            int zones = 0;
            foreach (CompoundZone zone in Object.FindObjectsByType<CompoundZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Yard(zone.gameObject);
                zones++;
            }

            report.Append($"  Yard light on {zones} compound zones (the compound now, each plot once claimed).\n");

            int rooms = 0;
            foreach (RestaurantFloor floor in Object.FindObjectsByType<RestaurantFloor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Room(floor);
                rooms++;
            }

            report.Append(rooms > 0 ? "  Room light over the restaurant's floor, following it as it grows.\n"
                                    : "  WARNING: no RestaurantFloor found; the restaurant has no room light.\n");

            int gateLamps = 0;
            foreach (GameObject wallRoot in RootsNamed(scene, "CompoundWalls"))
            foreach (Transform t in wallRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Lamp_L" && t.name != "Lamp_R") continue;
                GateLamp(t.gameObject);
                gateLamps++;
            }

            report.Append($"  Lamp pools on {gateLamps} gate lamps (lit only where their gate stands).\n");

            int carParkLamps = 0;
            foreach (GameObject visitors in RootsNamed(scene, "VisitorSystem"))
            {
                Transform carPark = visitors.transform.Find("Parking/CarPark");
                if (carPark == null) continue;

                foreach (Transform lamp in carPark)
                {
                    Transform pool = lamp.name.StartsWith("Lamp_") ? lamp.Find("Pool") : null;
                    if (pool == null) continue;
                    CarParkLamp(pool.gameObject);
                    carParkLamps++;
                }
            }

            report.Append($"  Lamp pools on {carParkLamps} car park lamps.\n");
            report.Append("  " + SetMoon(NightIntensity, NightAngles) + "\n");

            EditorSceneManager.MarkSceneDirty(scene);
            report.Append("Scene not saved.");
            return report.ToString();
        }

        internal static string Remove()
        {
            Scene scene = SceneManager.GetActiveScene();
            int roots = 0, lights = 0;

            foreach (GameObject existing in scene.GetRootGameObjects())
            {
                if (existing.name != RootName) continue;
                Object.DestroyImmediate(existing);
                roots++;
            }

            foreach (NightLight light in Object.FindObjectsByType<NightLight>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(light);
                lights++;
            }

            string moon = SetMoon(PreviousNightIntensity, PreviousNightAngles);
            EditorSceneManager.MarkSceneDirty(scene);
            return $"Night lights removed ({roots} root, {lights} lights). {moon} Scene not saved.";
        }

        // --- The lights, shared with the builders that make what they hang on ------------------------

        /// <summary>The even yard light over a compound zone on this object.</summary>
        internal static NightLight Yard(GameObject zone)
        {
            NightLight light = Ensure(zone);
            light.ConfigureArea(YardColour, YardIntensity, YardSpill, isFill: true, fitOver: null);
            return light;
        }

        /// <summary>The restaurant's warm room light, fitted over its floor.</summary>
        internal static NightLight Room(RestaurantFloor floor)
        {
            NightLight light = Ensure(floor.gameObject);
            light.ConfigureArea(RoomColour, RoomIntensity, RoomSpill, isFill: true, fitOver: floor.GetComponent<MeshRenderer>(),
                                CeilingSpacing, CeilingStrength);
            return light;
        }

        /// <summary>The pool under a gate's lamp.</summary>
        internal static NightLight GateLamp(GameObject lamp)
        {
            NightLight light = Ensure(lamp);
            light.ConfigureLamp(GateLampColour, GateLampIntensity, GateLampReach);
            return light;
        }

        /// <summary>The pool under a car park lamp: on its ground spot, which sits under the head.</summary>
        internal static NightLight CarParkLamp(GameObject pool)
        {
            NightLight light = Ensure(pool);
            light.ConfigureLamp(CarParkLampColour, CarParkLampIntensity, CarParkLampReach);
            return light;
        }

        static NightLight Ensure(GameObject go)
        {
            NightLight light = go.GetComponent<NightLight>();
            if (light == null) light = go.AddComponent<NightLight>();
            EditorUtility.SetDirty(light);
            return light;
        }

        // --- Plumbing ------------------------------------------------------------------------------

        static string SetMoon(float intensity, Vector3 angles)
        {
            int set = 0;
            foreach (DayNightLighting cycle in Object.FindObjectsByType<DayNightLighting>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(cycle);
                so.FindProperty("nightIntensity").floatValue = intensity;
                so.FindProperty("nightAngles").vector3Value = angles;
                so.ApplyModifiedProperties();
                set++;
            }

            return $"Moon set to {intensity} at {angles} on {set} day/night cycle(s).";
        }

        static IEnumerable<GameObject> RootsNamed(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) yield return root;
        }

        static Light Sun()
        {
            foreach (DayNightLighting cycle in Object.FindObjectsByType<DayNightLighting>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var sun = new SerializedObject(cycle).FindProperty("sun").objectReferenceValue as Light;
                if (sun != null) return sun;
            }

            if (RenderSettings.sun != null) return RenderSettings.sun;

            Light best = null;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional && (best == null || light.intensity > best.intensity)) best = light;
            return best;
        }
    }
}
