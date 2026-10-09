using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using LastOrders.Blockout;
using LastOrders.Perimeter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using E = LastOrders.EditorTools.EnvKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Moves the open scene between the level's looks: the current one (<see cref="EnvKit.Style"/>,
    /// the clean low-poly look), the painted look before it, and the muted look before that.
    ///
    /// The low-poly look is the game's own: smooth, stylised 3D under plain daylight - a white sun,
    /// a soft blue sky light, no colour grade beyond a touch more contrast and colour - fresh green
    /// grass, neutral earth and stone, and colour that comes from the things themselves rather than
    /// from a tint over everything. The painted look was a warm, golden late-afternoon grade over
    /// lime-gold grass and honey timber; it is kept so it can be switched back to.
    ///
    /// Each look's textures and materials are their own assets, in its own style folder; the muted
    /// look's are at the folders' top level. Moving a scene points every material reference in it -
    /// renderers, and components such as the restaurant floor that keep their own - at its
    /// counterpart in the target look: the same name in that look's folder, or for the stains and
    /// houses, whose materials are named by colour, the material of the same palette entry in that
    /// look. It sets that look's light and grade too. Nothing is rebuilt, so hand edits to the scene
    /// survive any move.
    /// </summary>
    public static class PaintedLookSetup
    {
        /// <summary>Every style folder there has been, oldest first. The muted look has none: it is the top level.</summary>
        static readonly string[] Styles = { "Painted", "LowPoly" };

        const string VolumeName = "Look_Volume";
        const string LegacyVolumeName = "PaintedLook_Volume";

        /// <summary>A look's light and grade.</summary>
        sealed class Look
        {
            public string Style;
            public Color Sun;
            public float Intensity, Shadows;
            public Color Sky, Equator, Ground;
            /// <summary>Its grade's volume profile, or null for no post-processing.</summary>
            public string Profile;
        }

        /// <summary>
        /// Plain daylight: a sun only just off white and a soft blue sky light, so a white wall is
        /// white, grass is green and shadows go a little cool - nothing golden laid over the colours.
        /// </summary>
        static readonly Look LowPoly = new Look
        {
            Style = "LowPoly",
            Sun = new Color(1f, 0.975f, 0.94f), Intensity = 1f, Shadows = 0.68f,
            // A neutral sky, as set by hand in the level: any blue in it turned the white floors and walls pale blue.
            Sky = Color.white, Equator = new Color(0.6603774f, 0.6603774f, 0.6603774f), Ground = new Color(0.3f, 0.3f, 0.29f),
            Profile = "Assets/Game Assets/Content/LastOrders/Environment/LowPolyLook_Volume.asset",
        };

        /// <summary>A warm sun and a cool, bluish sky: lit faces golden, shaded ones cool, as a painter would split them.</summary>
        static readonly Look Painted = new Look
        {
            Style = "Painted",
            Sun = new Color(1f, 0.91f, 0.78f), Intensity = 1.05f, Shadows = 0.7f,
            Sky = new Color(0.78f, 0.84f, 0.92f), Equator = new Color(0.68f, 0.7f, 0.66f), Ground = new Color(0.3f, 0.27f, 0.22f),
            Profile = "Assets/Game Assets/Content/LastOrders/Environment/PaintedLook_Volume.asset",
        };

        /// <summary>The level's light before either, with no grade.</summary>
        static readonly Look Muted = new Look
        {
            Style = "",
            Sun = Color.white, Intensity = 1f, Shadows = 0.553f,
            Sky = Color.white, Equator = new Color(0.726f, 0.726f, 0.726f), Ground = new Color(0.047f, 0.043f, 0.035f),
            Profile = null,
        };

        /// <summary>
        /// The palettes whose materials are named by colour - stains, siding, shingles - entry for
        /// entry in each look: the muted look's colours, the painted look's, and the current look's
        /// (EnvKit's own). This is how a stain's counterpart is found in another look.
        /// </summary>
        internal static (string[] muted, string[] painted, Color[] current)[] Palettes => new[]
        {
            (new[] { "7D6446", "6E5840", "8A7253" }, new[] { "8A6B40", "7D5F38", "957548" }, E.Dirt),
            (new[] { "5A4633", "4E3D2E" }, new[] { "6B4A2D", "5E4027" }, E.Mud),
            (new[] { "9C9656", "8F914C", "A69A5E" }, new[] { "727C3A", "7A843E", "6C7737" }, E.SunGrass),
            (new[] { "466A33", "3F6030", "4D6A36", "3B5A2E" }, new[] { "46692B", "3F6027", "4C6E2E", "3A5A28" }, E.ForestFloor),
            (new[] { "9FAF93", "D9CFB5", "93A5B5", "C99A86", "A8A49C", "D6C27E" },
             new[] { "A8724A", "D6CAB0", "8E9DA8", "C29A6E", "9DAA8A", "B9805A" }, E.HouseWall),
            (new[] { "5B5E63", "6E5E55", "7A4B3F", "4F5A5E", "5E6650", "8A7F72", "464A4F" },
             new[] { "4A5363", "5B5045", "7A4B3F", "4E5A62", "55604C", "565E6B", "3F4757" }, AbandonedHouseBuilder.RoofColours),
        };

        /// <summary>Materials named by their colour: "Splat_{colour}_{opacity}", "Siding_{colour}", "Shingles_{colour}".</summary>
        static readonly Regex ByColour = new Regex(@"^(Splat|Siding|Shingles)_([0-9A-F]{6})(_\d+)?$");

        [MenuItem("Last Orders/Environment/Apply Low-Poly Look (Current Scene)", priority = 83)]
        public static void RunApply() => Debug.Log(Apply(SceneManager.GetActiveScene()));

        [MenuItem("Last Orders/Environment/Switch To Painted Look (Current Scene)", priority = 84)]
        public static void RunPainted() => Debug.Log(MoveTo(SceneManager.GetActiveScene(), Painted));

        [MenuItem("Last Orders/Environment/Restore Muted Look (Current Scene)", priority = 85)]
        public static void RunRestore() => Debug.Log(Restore(SceneManager.GetActiveScene()));

        /// <summary>Paints whatever of the current look is out of date, then moves the scene onto it.</summary>
        public static string Apply(Scene scene)
        {
            var report = new StringBuilder($"[Last Orders] {E.Style} look:\n");

            report.Append(SurfaceRepaint.Apply());
            report.Append($"  {FoliageKit.Restyle()}\n");
            report.Append(MoveTo(scene, Current(), afterRepaint: true));
            return report.ToString();
        }

        public static string Restore(Scene scene) => MoveTo(scene, Muted);

        static Look Current() => E.Style == LowPoly.Style ? LowPoly : Painted;

        /// <summary>Points the scene at <paramref name="look"/>'s materials and sets its light and grade. Paints nothing.</summary>
        static string MoveTo(Scene scene, Look look, bool afterRepaint = false)
        {
            var report = new StringBuilder(afterRepaint ? "" : $"[Last Orders] {(look.Style == "" ? "Muted" : look.Style)} look:\n");

            var colours = ColoursIn(look);
            int swapped = Swap(scene, m => Counterpart(m, look.Style, colours));
            report.Append($"  {swapped} material references moved onto the {(look.Style == "" ? "muted" : look.Style)} look's materials.\n");

            if (look.Style == E.Style) report.Append($"  {PanelWalls(scene)}\n");
            report.Append($"  {SetLight(scene, look)}\n");
            report.Append($"  {PostProcessing(scene, look)}\n");

            EditorSceneManager.MarkSceneDirty(scene);
            report.Append("  The scene was not saved. Every look's textures and materials are kept; the menus move the scene " +
                          "between them.");
            return report.ToString();
        }

        // --- Finding counterparts ------------------------------------------------------------------

        /// <summary>Every palette colour of every look, as a hex, to the same entry's colour in <paramref name="look"/>.</summary>
        static Dictionary<string, Color> ColoursIn(Look look)
        {
            var map = new Dictionary<string, Color>();
            foreach (var (muted, painted, current) in Palettes)
            {
                for (int i = 0; i < current.Length; i++)
                {
                    Color target = look.Style == "" ? PrimitiveKit.Hex(muted[i])
                        : look.Style == Painted.Style ? PrimitiveKit.Hex(painted[i])
                        : current[i];

                    map[muted[i]] = target;
                    map[painted[i]] = target;
                    map[ColorUtility.ToHtmlStringRGB(current[i])] = target;
                }
            }

            return map;
        }

        /// <summary>The folder a material's look lives under: its own, or the one above a style folder.</summary>
        static string BaseFolder(string folder)
        {
            foreach (string style in Styles)
                if (folder.EndsWith("/" + style)) return folder.Substring(0, folder.Length - style.Length - 1);
            return folder;
        }

        /// <summary>
        /// <paramref name="material"/>'s counterpart in the look of <paramref name="style"/> ("" the
        /// muted look), or null if it is already that look's or has none and stays as it is.
        /// </summary>
        static Material Counterpart(Material material, string style, Dictionary<string, Color> colours)
        {
            string path = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrEmpty(path)) return null;

            string folder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string root = BaseFolder(folder);
            string target = style == "" ? root : $"{root}/{style}";
            if (folder == target) return null;

            string name = material.name;
            Match named = ByColour.Match(name);
            if (named.Success && root == E.PreviousMaterialFolder)
            {
                if (!colours.TryGetValue(named.Groups[2].Value, out Color colour)) return null;

                string kind = named.Groups[1].Value, suffix = named.Groups[3].Value;

                // The current look makes what it is missing; another look has only what it made.
                if (style == E.Style)
                {
                    if (kind == "Splat") return GroundSplats.MaterialAt(colour, int.Parse(suffix.Substring(1)));

                    Texture2D texture = kind == "Siding" ? TextureGenerator.EnsureSiding() : TextureGenerator.EnsureShingles();
                    float scale = material.GetTextureScale("_BaseMap").x;
                    return E.Surface(kind, texture, colour, scale > 0f ? 1f / scale : 1f);
                }

                name = $"{kind}_{ColorUtility.ToHtmlStringRGB(colour)}{suffix}";
            }

            // The muted wall was concrete blocks all round: the timber and tin patches came later.
            if (style == "" && (name == "Wall_Planks" || name == "Wall_Tin")) name = "Wall_ConcreteBlock";

            return AssetDatabase.LoadAssetAtPath<Material>($"{target}/{name}.mat");
        }

        /// <summary>
        /// Points every material reference in the scene that has a counterpart at it: each
        /// renderer's materials, and every material a script keeps for itself.
        /// </summary>
        static int Swap(Scene scene, System.Func<Material, Material> counterpart)
        {
            var cache = new Dictionary<Material, Material>();
            Material Find(Material m)
            {
                if (m == null) return null;
                if (!cache.TryGetValue(m, out Material found)) cache[m] = found = counterpart(m);
                return found;
            }

            int swapped = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] shared = renderer.sharedMaterials;
                    bool any = false;
                    for (int i = 0; i < shared.Length; i++)
                    {
                        Material other = Find(shared[i]);
                        if (other == null) continue;
                        shared[i] = other;
                        any = true;
                        swapped++;
                    }

                    if (!any) continue;
                    Undo.RecordObject(renderer, "Change look");
                    renderer.sharedMaterials = shared;
                }

                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) continue;

                    var so = new SerializedObject(behaviour);
                    SerializedProperty p = so.GetIterator();
                    bool changed = false;

                    // Into everything but long arrays - baked grids and the like, which hold no materials.
                    bool enter = true;
                    while (p.Next(enter))
                    {
                        enter = p.propertyType == SerializedPropertyType.Generic && !(p.isArray && p.arraySize > 512);
                        if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                        Material other = Find(p.objectReferenceValue as Material);
                        if (other == null) continue;
                        p.objectReferenceValue = other;
                        changed = true;
                        swapped++;
                    }

                    if (changed) so.ApplyModifiedProperties();
                }
            }

            return swapped;
        }

        // --- Walls ------------------------------------------------------------------------------------

        /// <summary>
        /// Patches the compound wall sections the wall builder would patch - timber here, tin there,
        /// chosen by each section's name - so a scene built before the patches came in has them too.
        /// </summary>
        static string PanelWalls(Scene scene)
        {
            Material concrete = SurfaceKit.CompoundWall();
            int planks = 0, tin = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (CompoundWall wall in root.GetComponentsInChildren<CompoundWall>(true))
                {
                    TextureGenerator.WallPanel panel = CompoundWallSceneSetup.PanelFor(wall.name);
                    if (panel == TextureGenerator.WallPanel.Concrete) continue;

                    Material patch = SurfaceKit.CompoundWall(panel);
                    foreach (Renderer renderer in wall.GetComponentsInChildren<Renderer>(true))
                    {
                        Material[] shared = renderer.sharedMaterials;
                        int at = System.Array.IndexOf(shared, concrete);
                        if (at < 0) continue;

                        Undo.RecordObject(renderer, "Change look");
                        shared[at] = patch;
                        renderer.sharedMaterials = shared;
                    }

                    if (panel == TextureGenerator.WallPanel.Planks) planks++;
                    else tin++;
                }
            }

            return $"Walls: {planks} sections patched with timber, {tin} with tin, the rest concrete.";
        }

        // --- Light ------------------------------------------------------------------------------------

        /// <summary>
        /// The sun and the sky, and the day end of the day/night blend to match, so play mode shows
        /// the same light. Its day ambient had once been captured black, which turned the sky's light
        /// off the moment play began; it is always the look's own sky colour.
        /// </summary>
        static string SetLight(Scene scene, Look look)
        {
            Light sun = RenderSettings.sun;
            if (sun == null)
                foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (light.type == LightType.Directional && light.gameObject.scene == scene && (sun == null || light.intensity > sun.intensity))
                        sun = light;

            if (sun != null)
            {
                Undo.RecordObject(sun, "Change look");
                sun.color = look.Sun;
                sun.intensity = look.Intensity;
                sun.shadowStrength = look.Shadows;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = look.Sky;
            RenderSettings.ambientEquatorColor = look.Equator;
            RenderSettings.ambientGroundColor = look.Ground;

            int cycles = 0;
            foreach (DayNightLighting cycle in Object.FindObjectsByType<DayNightLighting>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cycle.gameObject.scene != scene) continue;

                var so = new SerializedObject(cycle);
                so.FindProperty("dayColour").colorValue = look.Sun;
                so.FindProperty("dayIntensity").floatValue = look.Intensity;
                so.FindProperty("dayAmbient").colorValue = look.Sky;
                so.ApplyModifiedProperties();
                cycles++;
            }

            return $"Light: sun {(sun != null ? sun.name : "MISSING")} at {look.Intensity:0.00}, sky ambient {look.Sky}; " +
                   $"{cycles} day/night cycle(s) given the same day light.";
        }

        /// <summary>The look's grade on the scene's one global volume, or post-processing off for a look with none.</summary>
        static string PostProcessing(Scene scene, Look look)
        {
            bool on = look.Profile != null;

            GameObject go = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == VolumeName || root.name == LegacyVolumeName) go = root;

            if (on && go == null)
            {
                go = new GameObject(VolumeName);
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "Change look");
            }

            if (go != null)
            {
                Undo.RecordObject(go, "Change look");
                go.name = VolumeName;
                go.SetActive(on);
            }

            if (on)
            {
                var volume = go.GetComponent<Volume>();
                if (volume == null) volume = Undo.AddComponent<Volume>(go);
                Undo.RecordObject(volume, "Change look");
                volume.isGlobal = true;
                volume.priority = 1f;
                volume.sharedProfile = look == LowPoly ? LowPolyProfile() : PaintedProfile();
            }

            int cameras = 0;
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (camera.gameObject.scene != scene || !camera.CompareTag("MainCamera")) continue;

                var data = camera.GetUniversalAdditionalCameraData();
                Undo.RecordObject(data, "Change look");
                data.renderPostProcessing = on;
                cameras++;
            }

            return on
                ? $"Post-processing: '{VolumeName}' on {System.IO.Path.GetFileNameWithoutExtension(look.Profile)}, {cameras} main camera(s) rendering it."
                : $"Post-processing: off, on {cameras} main camera(s).";
        }

        /// <summary>
        /// The low-poly grade: a little more contrast and colour, a faint glow on the brightest
        /// faces, a light vignette. No colour filter and no split toning: the colours are the models'.
        /// </summary>
        static VolumeProfile LowPolyProfile()
        {
            VolumeProfile profile = LoadProfile(LowPoly.Profile);

            var bloom = Component<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.2f);
            bloom.scatter.Override(0.6f);
            bloom.tint.Override(Color.white);

            var grade = Component<ColorAdjustments>(profile);
            grade.contrast.Override(8f);
            grade.saturation.Override(4f);
            grade.colorFilter.Override(Color.white);

            var vignette = Component<Vignette>(profile);
            vignette.intensity.Override(0.15f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }

        /// <summary>The painted grade, as it was: a warm glow, warm highlights, teal shadows.</summary>
        static VolumeProfile PaintedProfile()
        {
            VolumeProfile profile = LoadProfile(Painted.Profile);

            var bloom = Component<Bloom>(profile);
            bloom.threshold.Override(0.82f);
            bloom.intensity.Override(0.4f);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(new Color(1f, 0.9f, 0.76f));

            var grade = Component<ColorAdjustments>(profile);
            grade.contrast.Override(8f);
            grade.saturation.Override(6f);
            grade.colorFilter.Override(new Color(1f, 0.98f, 0.95f));

            var split = Component<SplitToning>(profile);
            split.shadows.Override(new Color(0.42f, 0.55f, 0.6f));
            split.highlights.Override(new Color(0.62f, 0.56f, 0.46f));
            split.balance.Override(-10f);

            var vignette = Component<Vignette>(profile);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }

        static VolumeProfile LoadProfile(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        static T Component<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;

            component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
