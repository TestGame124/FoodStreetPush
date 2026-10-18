using UnityEngine;
using UnityEngine.Audio;

namespace LastOrders.Audio
{
    /// <summary>
    /// A bed that is always there and changes by crossfading: the ambience, and the music when there
    /// is some.
    ///
    /// Two sources, because a bed never cuts. The new one comes up on the free source while the old
    /// one goes down on the other, so dusk falling is the countryside thinning into crickets rather
    /// than one recording stopping and another starting. Asked for the bed that is already fading
    /// out, it brings that one back up from where it is instead of starting it again.
    /// </summary>
    internal sealed class SoundLayer
    {
        readonly AudioSource[] sources = new AudioSource[2];
        readonly SoundCue[] cues = new SoundCue[2];
        readonly float[] fade = new float[2];
        readonly float[] target = new float[2];
        readonly float[] speed = new float[2];
        readonly float[] level = new float[2];

        int active = -1;

        public SoundLayer(Transform parent, string name)
        {
            for (int i = 0; i < 2; i++)
            {
                var host = new GameObject($"{name} {(char)('A' + i)}");
                host.transform.SetParent(parent, false);

                AudioSource source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.priority = 0;
                sources[i] = source;
            }
        }

        public SoundCue Current => active >= 0 ? cues[active] : null;

        public void Play(SoundCue cue, float fadeSeconds, AudioMixerGroup output)
        {
            if (cue != null && !cue.HasClips) cue = null;
            if (cue == Current) return;

            float rate = 1f / Mathf.Max(fadeSeconds, 0.01f);

            if (active >= 0)
            {
                target[active] = 0f;
                speed[active] = rate;
            }

            if (cue == null)
            {
                active = -1;
                return;
            }

            int other = active >= 0 ? 1 - active
                      : cues[0] == cue ? 0
                      : cues[1] == cue ? 1
                      : fade[0] <= fade[1] ? 0 : 1;

            if (cues[other] == cue)
            {
                // Still fading out from a moment ago: bring it back rather than restart it.
                target[other] = 1f;
                speed[other] = rate;
                active = other;
                return;
            }

            AudioSource source = sources[other];
            source.Stop();
            source.clip = cue.Pick();
            source.outputAudioMixerGroup = output;
            source.pitch = 1f;
            source.volume = 0f;

            // From somewhere into it, so a bed that comes back every dusk is not the same opening each time.
            if (source.clip != null && source.clip.length > 4f)
                source.time = Random.Range(0f, source.clip.length * 0.8f);

            cues[other] = cue;
            level[other] = Mathf.Max(cue.volume.x, cue.volume.y);
            fade[other] = 0f;
            target[other] = 1f;
            speed[other] = rate;
            active = other;

            source.Play();
        }

        public void Tick(float deltaSeconds, float busVolume)
        {
            for (int i = 0; i < 2; i++)
            {
                if (cues[i] == null) continue;

                fade[i] = Mathf.MoveTowards(fade[i], target[i], speed[i] * deltaSeconds);
                sources[i].volume = fade[i] * level[i] * busVolume;

                if (target[i] > 0f || fade[i] > 0f) continue;

                // Faded right out: let the clip go, so a streamed bed stops reading from disk.
                sources[i].Stop();
                sources[i].clip = null;
                cues[i] = null;
            }
        }

        public void StopNow()
        {
            for (int i = 0; i < 2; i++)
            {
                sources[i].Stop();
                sources[i].clip = null;
                cues[i] = null;
                fade[i] = target[i] = 0f;
            }

            active = -1;
        }
    }
}
