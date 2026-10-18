using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace LastOrders.Audio
{
    /// <summary>
    /// Which cue each <see cref="SoundId"/> is, and the mix: how loud each bus sits against the
    /// others, and where it goes if the project grows an AudioMixer.
    ///
    /// Code names a sound by id and never by asset, so "the zombie died" is written once at the
    /// place it happens and what it sounds like is changed here without touching that code. An id
    /// with no cue is silence, not an error - a sound not yet chosen should not stop the game.
    ///
    /// Built by 'Last Orders / Audio / Set Up Sound', which fills it from the pack; edits made by
    /// hand to a cue survive a re-run, because the tool only adds what is missing.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Audio/Sound Library", fileName = "SoundLibrary")]
    public sealed class SoundLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public SoundId id;
            public SoundCue cue;
        }

        [Serializable]
        public sealed class BusMix
        {
            public SoundBus bus;

            [Tooltip("This bus's level in the mix, under the player's own slider for it.")]
            [Range(0f, 1f)] public float level = 1f;

            [Tooltip("Optional. Routes every cue on this bus through a mixer group, for effects or snapshots.")]
            public AudioMixerGroup output;
        }

        [SerializeField] Entry[] cues = new Entry[0];

        [SerializeField] BusMix[] buses =
        {
            new BusMix { bus = SoundBus.Sfx, level = 0.9f },
            new BusMix { bus = SoundBus.Ui, level = 0.7f },
            new BusMix { bus = SoundBus.Ambience, level = 0.45f },
            new BusMix { bus = SoundBus.Music, level = 0.6f },
            new BusMix { bus = SoundBus.Voice, level = 0.8f },
        };

        [NonSerialized] Dictionary<SoundId, SoundCue> lookup;

        public IReadOnlyList<Entry> Cues => cues;

        public SoundCue Get(SoundId id)
        {
            if (lookup == null) Build();
            return lookup.TryGetValue(id, out SoundCue cue) ? cue : null;
        }

        public float Level(SoundBus bus)
        {
            BusMix mix = Mix(bus);
            return mix != null ? mix.level : 1f;
        }

        public AudioMixerGroup Output(SoundBus bus) => Mix(bus)?.output;

        /// <summary>Editor wiring, for the setup tool: the whole id table at once.</summary>
        public void SetCues(Entry[] entries)
        {
            cues = entries ?? new Entry[0];
            lookup = null;
        }

        BusMix Mix(SoundBus bus)
        {
            if (buses == null) return null;
            for (int i = 0; i < buses.Length; i++)
                if (buses[i] != null && buses[i].bus == bus) return buses[i];
            return null;
        }

        void Build()
        {
            lookup = new Dictionary<SoundId, SoundCue>(cues != null ? cues.Length : 0);
            if (cues == null) return;

            for (int i = 0; i < cues.Length; i++)
                if (cues[i].cue != null) lookup[cues[i].id] = cues[i].cue;
        }

        void OnValidate() => lookup = null;
    }
}
