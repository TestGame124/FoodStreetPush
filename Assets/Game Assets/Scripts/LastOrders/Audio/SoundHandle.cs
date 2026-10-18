namespace LastOrders.Audio
{
    /// <summary>
    /// A sound that has started, for whoever needs to stop it or turn it down - a grill's sizzle,
    /// the noise maker's wail.
    ///
    /// A ticket rather than the voice itself. Voices are pooled and handed on the moment a sound
    /// ends, so a reference kept to one would, a second later, stop someone else's sound. The
    /// handle carries the generation it was issued at and does nothing once that voice has moved on,
    /// which makes holding a stale one, or stopping one twice, harmless.
    ///
    /// The default handle is a sound that never started - a missing cue, a cooldown, no controller -
    /// and every method on it is a no-op, so callers never have to check.
    /// </summary>
    public readonly struct SoundHandle
    {
        readonly SoundController owner;
        readonly int voice;
        readonly int generation;

        internal SoundHandle(SoundController owner, int voice, int generation)
        {
            this.owner = owner;
            this.voice = voice;
            this.generation = generation;
        }

        /// <summary>True while the sound this was issued for is still the one on its voice.</summary>
        public bool IsPlaying => owner != null && owner.IsCurrent(voice, generation);

        /// <summary>Stops it, fading out over <paramref name="fadeSeconds"/> (0 is a cut).</summary>
        public void Stop(float fadeSeconds = 0f)
        {
            if (IsPlaying) owner.StopVoice(voice, fadeSeconds);
        }

        /// <summary>Scales its level, 0 to 1, on top of the cue's own and the sliders.</summary>
        public void SetVolume(float scale)
        {
            if (IsPlaying) owner.ScaleVoice(voice, scale);
        }
    }
}
