using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.Audio
{
    /// <summary>
    /// Plays every sound in the game: one-shots and loops on a pool of voices, and the ambience and
    /// music beds on their own crossfading layers.
    ///
    /// Reached through statics - <c>SoundController.Play(SoundId.ZombieDeath, position)</c> - for the
    /// same reason <see cref="Core.LastOrdersBridge"/> is: a sound is one line at the place the thing
    /// happens, in a zombie, a turret, a button, and threading a service reference into each of them
    /// would touch every constructor in the project for the sake of a click. Every call is a no-op
    /// when there is no controller, no cue for the id, or the cue is cooling down, so a scene with no
    /// sound set up is simply quiet, and no caller ever checks.
    ///
    /// What a sound is, how often it may play and how many may overlap all live on its
    /// <see cref="SoundCue"/>; which cue an id means lives in the <see cref="SoundLibrary"/>. This
    /// class only decides which voice plays it and how loud the sliders let it be.
    ///
    /// One per scene, on the Audio root that 'Last Orders / Audio / Set Up Sound' adds. A second one
    /// in the same scene stands down rather than doubling every sound.
    /// </summary>
    [DefaultExecutionOrder(-900)] // Awake before anything that might play a sound in its own.
    [DisallowMultipleComponent]
    public sealed class SoundController : MonoBehaviour
    {
        [Tooltip("Which cue each sound id is, and the mix between the buses.")]
        [SerializeField] SoundLibrary library;

        [Tooltip("Voices made up front. More are made as they are needed, up to the most below.")]
        [SerializeField, Range(1, 64)] int prewarmVoices = 12;

        [Tooltip("Most one-shots and loops sounding at once. Keep under the project's Max Real " +
                 "Voices (Project Settings > Audio), which is 32: past it Unity silences voices itself, " +
                 "with no regard for which matter.")]
        [SerializeField, Range(4, 64)] int maxVoices = 26;

        [Tooltip("Degrees a 3D voice is spread across the speakers. 0 pans it hard to one side, " +
                 "which on a phone held in two hands sounds like a fault; 90 still says where.")]
        [SerializeField, Range(0f, 360f)] float spread = 90f;

        [Tooltip("Seconds the ambience takes to change bed.")]
        [SerializeField, Min(0f)] float defaultBedFade = 3f;

        static readonly int BusCount = Enum.GetValues(typeof(SoundBus)).Length;

        static SoundController instance;

        readonly List<Voice> voices = new List<Voice>();
        readonly Dictionary<SoundCue, float> lastPlayed = new Dictionary<SoundCue, float>();
        readonly Dictionary<SoundCue, int> sounding = new Dictionary<SoundCue, int>();
        float[] busVolume;

        Transform voiceRoot;
        SoundLayer ambience;
        SoundLayer music;

        AudioListener listener;
        float nextListenerSearch;

        sealed class Voice
        {
            public AudioSource Source;
            public SoundCue Cue;
            public Transform Follow;
            public bool Following;
            public float Level;
            public float Scale = 1f;
            public float Fade = 1f;
            public float FadeTarget = 1f;
            public float FadeSpeed;
            public bool Stopping;
            public int Generation;
            public float StartedAt;

            public bool Busy => Cue != null;
        }

        // --- The calls the rest of the game makes ----------------------------------------------------

        public static SoundController Instance => instance;

        public static SoundLibrary Library => instance != null ? instance.library : null;

        /// <summary>Voices sounding now, for the debug overlay.</summary>
        public static int ActiveVoices
        {
            get
            {
                if (instance == null) return 0;
                int busy = 0;
                foreach (Voice voice in instance.voices)
                    if (voice.Busy) busy++;
                return busy;
            }
        }

        /// <summary>Where the scene is being heard from, when there is a listener.</summary>
        public static bool TryGetListenerPosition(out Vector3 position)
        {
            AudioListener ears = instance != null ? instance.Listener() : null;
            position = ears != null ? ears.transform.position : default;
            return ears != null;
        }

        /// <summary>The cue an id stands for here, or null.</summary>
        public static SoundCue CueFor(SoundId id) => Library != null ? Library.Get(id) : null;

        /// <summary>Plays a sound that is heard the same wherever the camera is: the UI, a stinger.</summary>
        public static SoundHandle Play(SoundId id, float volume = 1f) =>
            instance != null ? instance.Begin(CueFor(id), null, null, volume, 0f) : default;

        /// <summary>Plays a sound from a point in the world. A cue that is not spatial ignores the point.</summary>
        public static SoundHandle Play(SoundId id, Vector3 at, float volume = 1f) =>
            instance != null ? instance.Begin(CueFor(id), at, null, volume, 0f) : default;

        public static SoundHandle Play(SoundCue cue, float volume = 1f) =>
            instance != null ? instance.Begin(cue, null, null, volume, 0f) : default;

        public static SoundHandle Play(SoundCue cue, Vector3 at, float volume = 1f) =>
            instance != null ? instance.Begin(cue, at, null, volume, 0f) : default;

        /// <summary>
        /// Plays a sound that goes where <paramref name="follow"/> goes - a loop on something that
        /// moves, or a long one-shot on a walking zombie. A loop whose object is destroyed stops with
        /// it; keep the handle to stop it sooner.
        /// </summary>
        public static SoundHandle PlayAttached(SoundId id, Transform follow, float volume = 1f, float fadeIn = 0f) =>
            instance != null ? instance.Begin(CueFor(id), null, follow, volume, fadeIn) : default;

        public static SoundHandle PlayAttached(SoundCue cue, Transform follow, float volume = 1f, float fadeIn = 0f) =>
            instance != null ? instance.Begin(cue, null, follow, volume, fadeIn) : default;

        /// <summary>Crossfades the ambience to <paramref name="bed"/>; null fades it out.</summary>
        public static void SetAmbience(SoundCue bed, float fadeSeconds = -1f)
        {
            if (instance == null) return;
            instance.ambience.Play(bed, fadeSeconds < 0f ? instance.defaultBedFade : fadeSeconds,
                                   instance.library != null ? instance.library.Output(SoundBus.Ambience) : null);
        }

        public static void SetAmbience(SoundId id, float fadeSeconds = -1f) => SetAmbience(CueFor(id), fadeSeconds);

        /// <summary>Crossfades the music to <paramref name="track"/>; null fades it out.</summary>
        public static void SetMusic(SoundCue track, float fadeSeconds = -1f)
        {
            if (instance == null) return;
            instance.music.Play(track, fadeSeconds < 0f ? instance.defaultBedFade : fadeSeconds,
                                instance.library != null ? instance.library.Output(SoundBus.Music) : null);
        }

        /// <summary>Stops every one-shot and loop, fading over <paramref name="fadeSeconds"/>. The beds carry on.</summary>
        public static void StopAll(float fadeSeconds = 0.2f)
        {
            if (instance == null) return;
            for (int i = 0; i < instance.voices.Count; i++)
                if (instance.voices[i].Busy) instance.StopVoice(i, fadeSeconds);
        }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(SoundLibrary soundLibrary) => library = soundLibrary;

        // --- Life -----------------------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning($"[SoundController] '{instance.name}' already plays this scene's sound, so " +
                                 $"the one on '{name}' stands down.", this);
                enabled = false;
                return;
            }

            instance = this;

            if (library == null)
                Debug.LogWarning("[SoundController] No SoundLibrary assigned, so every sound id is silent. " +
                                 "Run 'Last Orders / Audio / Set Up Sound (Current Scene)'.", this);

            voiceRoot = new GameObject("Voices").transform;
            voiceRoot.SetParent(transform, false);
            for (int i = 0; i < prewarmVoices; i++) CreateVoice();

            ambience = new SoundLayer(transform, "Ambience");
            music = new SoundLayer(transform, "Music");

            busVolume = new float[BusCount];
            RefreshBusVolumes();
            SoundSettings.Changed += HandleSettingsChanged;
        }

        void OnDestroy()
        {
            if (instance != this) return;

            instance = null;
            SoundSettings.Changed -= HandleSettingsChanged;
        }

        void OnDisable()
        {
            if (instance != this) return;

            for (int i = 0; i < voices.Count; i++)
                if (voices[i].Busy) Release(voices[i]);
        }

        void OnValidate()
        {
            maxVoices = Mathf.Max(maxVoices, prewarmVoices);
            if (busVolume != null) RefreshBusVolumes();
        }

        void Update()
        {
            float delta = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;

            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];
                if (!voice.Busy) continue;

                if (voice.Following)
                {
                    if (voice.Follow == null)
                    {
                        // What it was attached to has gone. A loop goes with it; a one-shot finishes where it was.
                        if (voice.Cue.loop)
                        {
                            Release(voice);
                            continue;
                        }

                        voice.Following = false;
                    }
                    else
                    {
                        voice.Source.transform.position = voice.Follow.position;
                    }
                }

                if (voice.Fade != voice.FadeTarget)
                    voice.Fade = Mathf.MoveTowards(voice.Fade, voice.FadeTarget, voice.FadeSpeed * delta);

                if (voice.Stopping && voice.Fade <= 0f)
                {
                    Release(voice);
                    continue;
                }

                Apply(voice);

                // Finished. Given a moment after starting, and not while the clip is still loading, so
                // a voice is never taken back before it has begun.
                if (!voice.Source.isPlaying && !AudioListener.pause && now - voice.StartedAt > 0.1f &&
                    (voice.Source.clip == null || voice.Source.clip.loadState != AudioDataLoadState.Loading))
                {
                    Release(voice);
                }
            }

            ambience.Tick(delta, busVolume[(int)SoundBus.Ambience]);
            music.Tick(delta, busVolume[(int)SoundBus.Music]);
        }

        // --- Playing --------------------------------------------------------------------------------

        SoundHandle Begin(SoundCue cue, Vector3? at, Transform follow, float scale, float fadeIn)
        {
            if (cue == null || !isActiveAndEnabled) return default;

            float now = Time.unscaledTime;
            if (cue.cooldown > 0f && lastPlayed.TryGetValue(cue, out float last) && now - last < cue.cooldown)
                return default;

            if (cue.maxVoices > 0 && Sounding(cue) >= cue.maxVoices) return default;

            bool placed = follow != null || at.HasValue;
            bool spatial = cue.spatial && placed;
            Vector3 position = follow != null ? follow.position : at.GetValueOrDefault();

            // A one-shot nobody could hear is not worth a voice. A loop is started anyway: the camera
            // may pan to it while it plays.
            if (spatial && !cue.loop && OutOfEarshot(position, cue.maxDistance)) return default;

            AudioClip clip = cue.Pick();
            if (clip == null) return default;

            int index = Acquire(cue.priority);
            if (index < 0) return default;

            Voice voice = voices[index];
            AudioSource source = voice.Source;

            source.clip = clip;
            source.loop = cue.loop;
            source.pitch = cue.PickPitch();
            source.priority = cue.priority;
            source.spatialBlend = spatial ? 1f : 0f;
            source.spread = spatial ? spread : 0f;
            source.minDistance = cue.minDistance;
            source.maxDistance = cue.maxDistance;
            source.outputAudioMixerGroup = library != null ? library.Output(cue.bus) : null;
            source.transform.position = spatial ? position : transform.position;

            voice.Cue = cue;
            voice.Follow = spatial ? follow : null;
            voice.Following = voice.Follow != null;
            voice.Level = cue.PickVolume();
            voice.Scale = Mathf.Max(0f, scale);
            voice.Fade = fadeIn > 0f ? 0f : 1f;
            voice.FadeTarget = 1f;
            voice.FadeSpeed = fadeIn > 0f ? 1f / fadeIn : 0f;
            voice.Stopping = false;
            voice.StartedAt = now;
            voice.Generation++;

            Apply(voice);

            // From somewhere into it, so two of the same loop side by side do not phase.
            if (cue.loop && clip.length > 1f) source.time = UnityEngine.Random.Range(0f, clip.length * 0.9f);

            source.Play();

            lastPlayed[cue] = now;
            sounding[cue] = Sounding(cue) + 1;

            return new SoundHandle(this, index, voice.Generation);
        }

        /// <summary>
        /// A free voice; failing that a new one, up to the most; failing that the least important
        /// one-shot that is no more important than this, oldest first. Never a loop: a loop that
        /// vanished would never come back.
        /// </summary>
        int Acquire(int priority)
        {
            for (int i = 0; i < voices.Count; i++)
                if (!voices[i].Busy) return i;

            if (voices.Count < maxVoices)
            {
                CreateVoice();
                return voices.Count - 1;
            }

            int victim = -1;
            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];
                if (voice.Cue.loop || voice.Cue.priority < priority) continue;

                if (victim < 0 ||
                    voice.Cue.priority > voices[victim].Cue.priority ||
                    (voice.Cue.priority == voices[victim].Cue.priority && voice.StartedAt < voices[victim].StartedAt))
                {
                    victim = i;
                }
            }

            if (victim >= 0) Release(voices[victim]);
            return victim;
        }

        void CreateVoice()
        {
            var host = new GameObject($"Voice {voices.Count:00}");
            host.transform.SetParent(voiceRoot, false);

            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;

            voices.Add(new Voice { Source = source });
        }

        void Release(Voice voice)
        {
            if (!voice.Busy) return;

            voice.Source.Stop();
            voice.Source.clip = null;

            int count = Sounding(voice.Cue) - 1;
            if (count > 0) sounding[voice.Cue] = count;
            else sounding.Remove(voice.Cue);

            voice.Cue = null;
            voice.Follow = null;
            voice.Following = false;
            voice.Stopping = false;
        }

        void Apply(Voice voice) =>
            voice.Source.volume = voice.Level * voice.Scale * voice.Fade * busVolume[(int)voice.Cue.bus];

        int Sounding(SoundCue cue) => sounding.TryGetValue(cue, out int count) ? count : 0;

        // --- What handles ask -------------------------------------------------------------------

        internal bool IsCurrent(int index, int generation) =>
            this != null && index >= 0 && index < voices.Count && voices[index].Busy && voices[index].Generation == generation;

        internal void StopVoice(int index, float fadeSeconds)
        {
            Voice voice = voices[index];
            if (fadeSeconds <= 0f)
            {
                Release(voice);
                return;
            }

            voice.Stopping = true;
            voice.FadeTarget = 0f;
            voice.FadeSpeed = Mathf.Max(voice.FadeSpeed, 1f / fadeSeconds);
        }

        internal void ScaleVoice(int index, float scale)
        {
            Voice voice = voices[index];
            voice.Scale = Mathf.Max(0f, scale);
            Apply(voice);
        }

        // --- The sliders ----------------------------------------------------------------------------

        void HandleSettingsChanged(SoundBus? _) => RefreshBusVolumes();

        void RefreshBusVolumes()
        {
            float master = SoundSettings.Master;
            for (int i = 0; i < BusCount; i++)
            {
                var bus = (SoundBus)i;
                float mix = library != null ? library.Level(bus) : 1f;
                busVolume[i] = master * SoundSettings.Effective(bus) * mix;
            }
        }

        // --- Earshot --------------------------------------------------------------------------------

        bool OutOfEarshot(Vector3 position, float maxDistance)
        {
            AudioListener ears = Listener();
            if (ears == null) return false;

            float reach = maxDistance * 1.05f;
            return (ears.transform.position - position).sqrMagnitude > reach * reach;
        }

        AudioListener Listener()
        {
            if (listener != null && listener.isActiveAndEnabled) return listener;
            if (Time.unscaledTime < nextListenerSearch) return null;

            nextListenerSearch = Time.unscaledTime + 1f;
            listener = null;

            foreach (AudioListener candidate in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (!candidate.isActiveAndEnabled) continue;
                listener = candidate;
                break;
            }

            return listener;
        }
    }
}
