using LastOrders.Core;
using LastOrders.DayCycle;
using UnityEngine;

namespace LastOrders.Audio
{
    /// <summary>
    /// A sound that belongs to a thing in the scene rather than to an event: the soup pot's boil,
    /// the fridge's hum, the dining room's chatter while it is open.
    ///
    /// A looping cue sounds from here for as long as this is enabled and the day is in one of its
    /// <see cref="phases"/>, fading in and out with both - so the diner's chatter goes as the
    /// shutters come down at dusk, and a station switched off goes quiet with it. A one-shot cue
    /// plays once each time this is enabled, which is what a thing that appears wants.
    ///
    /// <see cref="Play"/> is public for UnityEvents and animation events: a door's swing, a
    /// footstep. Several emitters may sit on one object.
    /// </summary>
    public sealed class SoundEmitter : MonoBehaviour
    {
        [Tooltip("What it plays. Wins over the id when both are set.")]
        [SerializeField] SoundCue cue;

        [Tooltip("What it plays, looked up in the scene's sound library, when no cue is set.")]
        [SerializeField] SoundId id = SoundId.None;

        [Tooltip("A loop sounds while this is enabled; a one-shot plays each time it is enabled. " +
                 "Off, it plays only when told to.")]
        [SerializeField] bool playOnEnable = true;

        [Tooltip("For a loop: the phases of the day it sounds in.")]
        [SerializeField] SoundPhases phases = SoundPhases.All;

        [Tooltip("On top of the cue's own level.")]
        [SerializeField, Range(0f, 1f)] float volume = 1f;

        [Tooltip("Seconds a loop takes to fade in and out.")]
        [SerializeField, Min(0f)] float fadeSeconds = 1.5f;

        GameLifetimeScope scope;
        GameComposition composition;
        SoundHandle loop;
        bool started;

        public SoundCue Cue => cue != null ? cue : SoundController.CueFor(id);

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(SoundId soundId, SoundPhases inPhases, float level = 1f)
        {
            cue = null;
            id = soundId;
            phases = inPhases;
            volume = level;
            playOnEnable = true;
        }

        /// <summary>Plays the cue once from here.</summary>
        public void Play()
        {
            SoundCue sound = Cue;
            if (sound == null) return;

            if (sound.loop) StartLoop();
            else SoundController.Play(sound, transform.position, volume);
        }

        public void StartLoop()
        {
            if (loop.IsPlaying) return;
            loop = SoundController.PlayAttached(Cue, transform, volume, fadeSeconds);
        }

        public void Stop() => loop.Stop(fadeSeconds);

        void Start()
        {
            scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;
            if (composition != null) composition.Machine.OnPhaseChanged += HandlePhaseChanged;

            started = true;
            Refresh(enabling: true);
        }

        void OnEnable()
        {
            if (started) Refresh(enabling: true);
        }

        void OnDisable() => loop.Stop(fadeSeconds);

        void OnDestroy()
        {
            if (composition != null) composition.Machine.OnPhaseChanged -= HandlePhaseChanged;
        }

        void HandlePhaseChanged(DayPhase previous, DayPhase current) => Refresh(enabling: false);

        void Refresh(bool enabling)
        {
            if (!playOnEnable) return;

            SoundCue sound = Cue;
            if (sound == null) return;

            if (!sound.loop)
            {
                if (enabling) SoundController.Play(sound, transform.position, volume);
                return;
            }

            bool wanted = isActiveAndEnabled && (composition == null || phases.Includes(composition.Machine.CurrentPhase));
            if (wanted) StartLoop();
            else Stop();
        }
    }
}
