using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using LastOrders.Audio;
using LastOrders.Core;
using LastOrders.Defense;
using LastOrders.Restaurant;
using LastOrders.UI.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Gives the current scene its sound (see <see cref="SoundController"/>), from the pack in
    /// Assets/Game Assets/Audio:
    ///
    /// - a <see cref="SoundCue"/> for every <see cref="SoundId"/>, made from <see cref="SoundCatalog"/>
    ///   the first time and left alone after, and the <see cref="SoundLibrary"/> that names them;
    /// - import settings fitted to how each take is used: long beds streamed, 3D sounds mono, short
    ///   ones decompressed up front, and 22 kHz on phones for everything but the beds;
    /// - one root, <c>Audio</c>: the controller, the ambience, the game's events, the horde's voice,
    ///   the button clicks, and the ears that ride over the middle of the screen;
    /// - the add-ons on the prefabs that make a noise of their own - every cooking station and every
    ///   defense - and a click on each button that has no <see cref="HudMotion"/> to sound it;
    /// - the dining room's chatter while it is open, and the fridge's hum.
    ///
    /// Re-runnable and additive: a cue tuned by hand, a library entry pointed elsewhere, an add-on
    /// set differently on a prefab are all kept. Never shows a dialog, so it can be run unattended.
    /// </summary>
    internal static class SoundSetup
    {
        const string PackRoot = "Assets/Game Assets/Audio";
        const string ContentRoot = "Assets/Game Assets/Content/LastOrders/Audio";
        const string CueFolder = ContentRoot + "/Cues";
        const string LibraryPath = ContentRoot + "/SoundLibrary.asset";
        const string RootName = "Audio";
        const string PackPrefix = "Cooking_Game_";

        static readonly string[] PrefabFolders = { "Assets/Game Assets", "Assets/GridBuildingSystem" };

        /// <summary>Full-screen catchers behind a window: a tap on them closes it, and says nothing.</summary>
        static readonly string[] SilentButtons = { "scrim", "backdrop", "tapaway", "unmask" };

        /// <summary>Past this, a take is streamed from disk rather than held in memory.</summary>
        const float StreamFromSeconds = 30f;

        /// <summary>Under this many bytes as 22 kHz mono PCM, a take is decompressed when it loads.</summary>
        const int DecompressUnderBytes = 200 * 1024;

        const int PhoneSampleRate = 22050;

        [MenuItem("Last Orders/Audio/Set Up Sound (Current Scene)", priority = 120)]
        static void Run() => Debug.Log("[Last Orders] " + Apply());

        [MenuItem("Last Orders/Audio/Remove Sound (Current Scene)", priority = 121)]
        static void RunRemove() => Debug.Log("[Last Orders] " + Remove());

        [MenuItem("Last Orders/Audio/Reapply Import Settings", priority = 122)]
        static void RunImport()
        {
            SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            var report = new StringBuilder("Sound import settings:\n");
            if (library == null) report.Append("  No library yet; run Set Up Sound first.\n");
            else ApplyImportSettings(library, report);
            Debug.Log("[Last Orders] " + report);
        }

        internal static string Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            var report = new StringBuilder("Sound:\n");

            Dictionary<string, string> takes = IndexPack();
            if (takes.Count == 0) return $"Sound: no audio found under {PackRoot}; nothing was changed.";

            SoundLibrary library = BuildLibrary(takes, report);
            ApplyImportSettings(library, report);

            SetUpRoot(scene, library, report);
            AddStationSounds(report);
            AddDefenseSounds(report);
            AddButtonSounds(report);
            AddSceneEmitters(report);

            EditorSceneManager.MarkSceneDirty(scene);
            report.Append("Scene not saved.");
            return report.ToString();
        }

        internal static string Remove()
        {
            Scene scene = SceneManager.GetActiveScene();
            int roots = 0, emitters = 0, buttons = 0;

            foreach (GameObject existing in scene.GetRootGameObjects())
            {
                if (existing.name != RootName || existing.GetComponent<SoundController>() == null) continue;
                Object.DestroyImmediate(existing);
                roots++;
            }

            foreach (SoundEmitter emitter in Object.FindObjectsByType<SoundEmitter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(emitter)) continue;
                if (emitter.gameObject.name.StartsWith("Sound_")) Object.DestroyImmediate(emitter.gameObject);
                else Object.DestroyImmediate(emitter);
                emitters++;
            }

            foreach (UiButtonSound sound in Object.FindObjectsByType<UiButtonSound>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(sound)) continue;
                Object.DestroyImmediate(sound);
                buttons++;
            }

            int prefabs = 0;
            foreach (string path in Prefabs())
                if (EditPrefab(path, root => StripAll(root) > 0)) prefabs++;

            EditorSceneManager.MarkSceneDirty(scene);
            return $"Sound removed ({roots} root, {emitters} emitters, {buttons} button sounds in the scene; " +
                   $"add-ons taken off {prefabs} prefabs). Cues, library and import settings kept. Scene not saved.";
        }

        // --- Cues and the library -----------------------------------------------------------------

        /// <summary>Every take in the pack, by its folder and its name less the pack's prefix.</summary>
        static Dictionary<string, string> IndexPack()
        {
            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { PackRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                index[Key(path)] = path;
            }

            return index;
        }

        static string Key(string path)
        {
            string relative = path.Substring(PackRoot.Length + 1);
            string folder = Path.GetDirectoryName(relative)?.Replace('\\', '/') ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(relative);
            if (name.StartsWith(PackPrefix, StringComparison.OrdinalIgnoreCase)) name = name.Substring(PackPrefix.Length);
            return folder.Length > 0 ? folder + "/" + name : name;
        }

        /// <summary>
        /// The takes a pattern names: the end of a take's key, with * for anything. Public for the tests.
        /// </summary>
        internal static List<string> Match(IEnumerable<string> keys, string pattern)
        {
            var regex = new Regex("(^|/)" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase);
            var found = new List<string>();
            foreach (string key in keys)
                if (regex.IsMatch(key)) found.Add(key);

            found.Sort(StringComparer.OrdinalIgnoreCase);
            return found;
        }

        static SoundLibrary BuildLibrary(Dictionary<string, string> takes, StringBuilder report)
        {
            EnsureFolder(CueFolder);

            SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var kept = new Dictionary<SoundId, SoundCue>();
            foreach (SoundLibrary.Entry entry in library.Cues)
                if (entry.cue != null) kept[entry.id] = entry.cue;

            var entries = new List<SoundLibrary.Entry>();
            var missing = new List<string>();
            int made = 0, filled = 0;

            foreach (SoundCatalog.Spec spec in SoundCatalog.All)
            {
                if (!kept.TryGetValue(spec.Id, out SoundCue cue))
                {
                    string path = $"{CueFolder}/{spec.Id}.asset";
                    cue = AssetDatabase.LoadAssetAtPath<SoundCue>(path);

                    if (cue == null)
                    {
                        cue = ScriptableObject.CreateInstance<SoundCue>();
                        Fill(cue, spec);
                        AssetDatabase.CreateAsset(cue, path);
                        made++;
                    }
                }

                // A cue whose takes have all gone is filled again; one with takes is the designer's.
                if (!cue.HasClips)
                {
                    cue.clips = Clips(spec, takes, missing);
                    EditorUtility.SetDirty(cue);
                    if (cue.HasClips) filled++;
                }

                entries.Add(new SoundLibrary.Entry { id = spec.Id, cue = cue });
            }

            library.SetCues(entries.ToArray());
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            report.Append($"  {entries.Count} sounds in '{LibraryPath}': {made} cues made, {filled} filled with takes, " +
                          $"the rest kept as they were.\n");

            foreach (string gap in missing)
                report.Append($"  WARNING: no take in the pack matches '{gap}'.\n");

            return library;
        }

        static void Fill(SoundCue cue, SoundCatalog.Spec spec)
        {
            cue.bus = spec.Bus;
            cue.volume = new Vector2(Mathf.Clamp01(spec.Level - spec.LevelSpread), Mathf.Clamp01(spec.Level));
            cue.pitch = spec.Pitch;
            cue.spatial = spec.Spatial;
            cue.minDistance = spec.MinDistance;
            cue.maxDistance = spec.MaxDistance;
            cue.loop = spec.Loop;
            cue.cooldown = spec.Cooldown;
            cue.maxVoices = spec.MaxVoices;
            cue.priority = spec.Priority;
        }

        static AudioClip[] Clips(SoundCatalog.Spec spec, Dictionary<string, string> takes, List<string> missing)
        {
            var clips = new List<AudioClip>();
            foreach (string pattern in spec.Takes)
            {
                List<string> keys = Match(takes.Keys, pattern);
                if (keys.Count == 0) missing.Add($"{pattern}' (for {spec.Id}");

                foreach (string key in keys)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(takes[key]);
                    if (clip != null && !clips.Contains(clip)) clips.Add(clip);
                }
            }

            return clips.ToArray();
        }

        // --- Import settings ----------------------------------------------------------------------

        [Flags]
        enum Use { None = 0, Spatial = 1, Bed = 2, Loop = 4, Voice = 8, Flat = 16 }

        static void ApplyImportSettings(SoundLibrary library, StringBuilder report)
        {
            var uses = new Dictionary<string, Use>();
            foreach (SoundLibrary.Entry entry in library.Cues)
            {
                SoundCue cue = entry.cue;
                if (cue == null || cue.clips == null) continue;

                Use use = (cue.spatial ? Use.Spatial : Use.Flat)
                        | (cue.bus == SoundBus.Ambience && !cue.spatial ? Use.Bed : Use.None)
                        | (cue.loop ? Use.Loop : Use.None)
                        | (cue.bus == SoundBus.Voice ? Use.Voice : Use.None);

                foreach (AudioClip clip in cue.clips)
                {
                    if (clip == null) continue;
                    string path = AssetDatabase.GetAssetPath(clip);
                    uses[path] = uses.TryGetValue(path, out Use had) ? had | use : use;
                }
            }

            int streamed = 0, compressed = 0, decompressed = 0, changed = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (KeyValuePair<string, Use> pair in uses)
                {
                    if (!(AssetImporter.GetAtPath(pair.Key) is AudioImporter importer)) continue;

                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(pair.Key);
                    float seconds = clip != null ? clip.length : 0f;
                    Use use = pair.Value;

                    // A bed only ever plays flat and as recorded, so it keeps its stereo; anything
                    // heard from a place is mixed to mono, or a 3D voice plays only its left channel.
                    bool bedOnly = (use & Use.Bed) != 0 && (use & Use.Spatial) == 0;
                    bool mono = !bedOnly;

                    AudioClipLoadType load = seconds > StreamFromSeconds ? AudioClipLoadType.Streaming
                                           : (use & Use.Loop) == 0 && seconds * PhoneSampleRate * 2f < DecompressUnderBytes
                                               ? AudioClipLoadType.DecompressOnLoad
                                               : AudioClipLoadType.CompressedInMemory;

                    float quality = bedOnly ? 0.5f : (use & Use.Voice) != 0 ? 0.7f : 0.6f;
                    bool streaming = load == AudioClipLoadType.Streaming;

                    if (streaming) streamed++;
                    else if (load == AudioClipLoadType.CompressedInMemory) compressed++;
                    else decompressed++;

                    if (Configure(importer, mono, load, quality, phoneRate: !bedOnly)) changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            report.Append($"  Import settings on {uses.Count} takes ({changed} changed): {streamed} streamed, " +
                          $"{compressed} compressed in memory, {decompressed} decompressed on load; " +
                          $"3D takes mono, {PhoneSampleRate} Hz on Android and iOS.\n");
        }

        /// <summary>Sets an importer to these settings and reimports it, if it is not set so already.</summary>
        static bool Configure(AudioImporter importer, bool mono, AudioClipLoadType load, float quality, bool phoneRate)
        {
            bool streaming = load == AudioClipLoadType.Streaming;

            AudioImporterSampleSettings wanted = importer.defaultSampleSettings;
            wanted.loadType = load;
            wanted.compressionFormat = AudioCompressionFormat.Vorbis;
            wanted.quality = quality;
            wanted.preloadAudioData = !streaming;
            wanted.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;

            AudioImporterSampleSettings phone = wanted;
            phone.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            phone.sampleRateOverride = PhoneSampleRate;

            bool same = importer.forceToMono == mono && importer.loadInBackground == streaming &&
                        Same(importer.defaultSampleSettings, wanted) &&
                        SamePlatform(importer, "Android", phoneRate, phone) &&
                        SamePlatform(importer, "iOS", phoneRate, phone);
            if (same) return false;

            importer.forceToMono = mono;
            importer.loadInBackground = streaming;
            importer.defaultSampleSettings = wanted;

            foreach (string platform in new[] { "Android", "iOS" })
            {
                if (phoneRate) importer.SetOverrideSampleSettings(platform, phone);
                else importer.ClearSampleSettingOverride(platform);
            }

            importer.SaveAndReimport();
            return true;
        }

        static bool SamePlatform(AudioImporter importer, string platform, bool wantOverride, AudioImporterSampleSettings phone)
        {
            bool has = importer.ContainsSampleSettingsOverride(platform);
            if (has != wantOverride) return false;
            return !has || Same(importer.GetOverrideSampleSettings(platform), phone);
        }

        static bool Same(AudioImporterSampleSettings a, AudioImporterSampleSettings b) =>
            a.loadType == b.loadType && a.compressionFormat == b.compressionFormat &&
            Mathf.Approximately(a.quality, b.quality) && a.preloadAudioData == b.preloadAudioData &&
            a.sampleRateSetting == b.sampleRateSetting &&
            (a.sampleRateSetting != AudioSampleRateSetting.OverrideSampleRate || a.sampleRateOverride == b.sampleRateOverride);

        // --- The scene's root ---------------------------------------------------------------------

        static void SetUpRoot(Scene scene, SoundLibrary library, StringBuilder report)
        {
            GameObject root = null;
            foreach (GameObject existing in scene.GetRootGameObjects())
                if (existing.name == RootName && existing.GetComponent<SoundController>() != null) root = existing;

            bool made = root == null;
            if (made)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();

            Ensure<SoundController>(root).Configure(library);
            Ensure<AmbienceDirector>(root).Configure(scope);
            Ensure<GameSoundEvents>(root).Configure(scope);
            Ensure<ZombieVoices>(root).Configure(scope);
            Ensure<UiClickSounds>(root);

            Transform ears = root.transform.Find("Listener");
            if (ears == null)
            {
                ears = new GameObject("Listener").transform;
                ears.SetParent(root.transform, false);
            }

            Ensure<SoundListener>(ears.gameObject).Configure(Camera.main, 0f);

            report.Append($"  Root '{RootName}' {(made ? "made" : "updated")}: controller, ambience by phase, the game's events, " +
                          $"the horde's voice and the button clicks{(scope == null ? " (WARNING: no GameLifetimeScope, so no phases or events)" : "")}.\n");
            report.Append(Camera.main != null
                ? $"  Ears over the middle of '{Camera.main.name}''s view; its own AudioListener stands down while they are on.\n"
                : "  WARNING: no Main Camera, so the ears stay where they are.\n");
        }

        // --- What makes a noise of its own ---------------------------------------------------------

        static void AddStationSounds(StringBuilder report)
        {
            var added = new List<string>();

            foreach (string path in Prefabs())
            {
                bool changed = EditPrefab(path, root =>
                {
                    bool any = false;
                    foreach (CookeryStation station in root.GetComponentsInChildren<CookeryStation>(true))
                    {
                        if (station.GetComponent<StationSound>() != null || FromNestedPrefab(station)) continue;

                        station.gameObject.AddComponent<StationSound>().Configure(station, LoopFor(root.name));
                        any = true;
                    }

                    return any;
                });

                if (changed) added.Add(Path.GetFileNameWithoutExtension(path));
            }

            report.Append(added.Count > 0 ? $"  Station sounds on {string.Join(", ", added)}.\n"
                                          : "  Station sounds: already on every station prefab.\n");
        }

        static SoundId LoopFor(string station)
        {
            string name = station.ToLowerInvariant();
            if (name.Contains("grill")) return SoundId.GrillSizzle;
            if (name.Contains("griddle")) return SoundId.GriddleSizzle;
            if (name.Contains("soup")) return SoundId.SoupBoil;
            if (name.Contains("bread") || name.Contains("oven")) return SoundId.OvenRoar;
            return SoundId.None;
        }

        static void AddDefenseSounds(StringBuilder report)
        {
            var added = new List<string>();

            foreach (string path in Prefabs())
            {
                bool changed = EditPrefab(path, root =>
                {
                    bool any = false;
                    foreach (DefensePost post in root.GetComponentsInChildren<DefensePost>(true))
                    {
                        if (post.GetComponent<DefenseSound>() != null || FromNestedPrefab(post)) continue;

                        (SoundId fire, SoundId impact) = ShotsFor(post.Definition != null ? post.Definition.name : root.name);
                        post.gameObject.AddComponent<DefenseSound>().Configure(post, fire, impact);
                        any = true;
                    }

                    return any;
                });

                if (changed) added.Add(Path.GetFileNameWithoutExtension(path));
            }

            report.Append(added.Count > 0 ? $"  Defense sounds on {string.Join(", ", added)}.\n"
                                          : "  Defense sounds: already on every defense prefab.\n");
        }

        static (SoundId fire, SoundId impact) ShotsFor(string defense)
        {
            string name = defense.ToLowerInvariant();
            if (name.Contains("toast")) return (SoundId.ShotThrow, SoundId.ImpactToast);
            if (name.Contains("plate")) return (SoundId.ShotThrow, SoundId.ImpactSmash);
            if (name.Contains("scrap") || name.Contains("turret")) return (SoundId.ShotHeavy, SoundId.None);
            if (name.Contains("guard") || name.Contains("tower")) return (SoundId.ShotRifle, SoundId.None);
            return (SoundId.ShotFood, SoundId.ImpactSplat);
        }

        static void AddButtonSounds(StringBuilder report)
        {
            int inPrefabs = 0;
            foreach (string path in Prefabs())
            {
                int count = 0;
                EditPrefab(path, root =>
                {
                    foreach (Button button in root.GetComponentsInChildren<Button>(true))
                        if (!FromNestedPrefab(button) && AddButtonSound(button, root.name, inPrefab: true)) count++;
                    return count > 0;
                });

                inPrefabs += count;
            }

            // Prefabs first, so a button in a prefab's copy in the scene already has it.
            int inScene = 0;
            foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (AddButtonSound(button, button.transform.root.name, inPrefab: false)) inScene++;

            report.Append($"  Click sounds on {inPrefabs} buttons in prefabs and {inScene} in the scene that have no " +
                          "pressable HudMotion; the rest click through the root.\n");
        }

        static bool AddButtonSound(Button button, string context, bool inPrefab)
        {
            if (button.GetComponent<UiButtonSound>() != null) return false;
            if (button.TryGetComponent(out HudMotion motion) && motion.Pressable) return false;

            string name = button.name.ToLowerInvariant();
            foreach (string silent in SilentButtons)
                if (name.Replace(" ", "").Contains(silent)) return false;

            // A button called only "Button" is a world prompt: what it does is in its prefab's name.
            SoundId id = UiClickSounds.ForName(button.name);
            if (id == SoundId.UiTap && name == "button") id = UiClickSounds.ForName(context);

            UiButtonSound sound = inPrefab ? button.gameObject.AddComponent<UiButtonSound>()
                                           : Undo.AddComponent<UiButtonSound>(button.gameObject);
            sound.Configure(id);
            if (!inPrefab) EditorUtility.SetDirty(sound);
            return true;
        }

        /// <summary>Part of another prefab placed inside this one: that prefab's own pass sounds it.</summary>
        static bool FromNestedPrefab(Component part) => PrefabUtility.IsPartOfPrefabInstance(part);

        static void AddSceneEmitters(StringBuilder report)
        {
            RestaurantFloor floor = Object.FindFirstObjectByType<RestaurantFloor>(FindObjectsInactive.Include);
            if (floor != null)
            {
                Vector3 centre = floor.TryGetComponent(out Renderer area) ? area.bounds.center : floor.transform.position;
                Emitter(floor.transform, "Sound_DinerChatter", centre + Vector3.up * 1.5f, SoundId.DinerChatter, SoundPhases.Day);
                report.Append("  The dining room's chatter, while it is open for service.\n");
            }
            else
            {
                report.Append("  WARNING: no RestaurantFloor found; the dining room is quiet.\n");
            }

            int fridges = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!t.name.Equals("Fridge", StringComparison.OrdinalIgnoreCase)) continue;
                Emitter(t, "Sound_FridgeHum", t.position + Vector3.up, SoundId.FridgeHum, SoundPhases.All);
                fridges++;
            }

            report.Append($"  A hum on {fridges} fridge(s).\n");
        }

        static void Emitter(Transform parent, string name, Vector3 at, SoundId id, SoundPhases phases)
        {
            Transform host = parent.Find(name);
            if (host == null)
            {
                host = new GameObject(name).transform;
                Undo.RegisterCreatedObjectUndo(host.gameObject, "Sound emitter");
                host.SetParent(parent, false);
            }

            host.position = at;

            SoundEmitter emitter = Ensure<SoundEmitter>(host.gameObject);
            emitter.Configure(id, phases);
            EditorUtility.SetDirty(emitter);
        }

        // --- Plumbing -------------------------------------------------------------------------------

        static int StripAll(GameObject root)
        {
            var parts = new List<Component>();
            parts.AddRange(root.GetComponentsInChildren<StationSound>(true));
            parts.AddRange(root.GetComponentsInChildren<DefenseSound>(true));
            parts.AddRange(root.GetComponentsInChildren<UiButtonSound>(true));

            int removed = 0;
            foreach (Component part in parts)
            {
                // A nested prefab's add-on comes off when that prefab's own turn comes.
                if (FromNestedPrefab(part)) continue;
                Object.DestroyImmediate(part, true);
                removed++;
            }

            return removed;
        }

        /// <summary>Every prefab the setup looks in, plain ones before their variants.</summary>
        static List<string> Prefabs()
        {
            var plain = new List<string>();
            var variants = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", PrefabFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;

                // Only prefabs with something here to sound; loading the rest would cost minutes.
                if (asset.GetComponentInChildren<CookeryStation>(true) == null &&
                    asset.GetComponentInChildren<DefensePost>(true) == null &&
                    asset.GetComponentInChildren<Button>(true) == null &&
                    asset.GetComponentInChildren<StationSound>(true) == null &&
                    asset.GetComponentInChildren<DefenseSound>(true) == null &&
                    asset.GetComponentInChildren<UiButtonSound>(true) == null)
                {
                    continue;
                }

                (PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant ? variants : plain).Add(path);
            }

            plain.Sort(StringComparer.Ordinal);
            variants.Sort(StringComparer.Ordinal);
            plain.AddRange(variants);
            return plain;
        }

        static bool EditPrefab(string path, Func<GameObject, bool> edit)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = edit(root);
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                return changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component == null) component = Undo.AddComponent<T>(go);
            EditorUtility.SetDirty(component);
            return component;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
