using UnityEngine;

namespace LastOrders.Audio
{
    /// <summary>
    /// One thing the game can sound like: a set of interchangeable takes, and how to play them.
    ///
    /// A cue rather than a clip, because almost nothing in the pack is meant to be heard as a single
    /// recording. Chopping is six takes and a zombie is twenty-four, and the same take twice running
    /// is what makes a game sound like a machine. The cue picks a different one each time - never the
    /// one it just played - and nudges the pitch and the level, so even a cue with one take does not
    /// stutter.
    ///
    /// The limits live here too, how often and how many at once, because they belong to the sound
    /// rather than to whoever plays it. Twenty zombies falling in the same second is twenty calls;
    /// the cue is what decides the player hears three.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Audio/Sound Cue", fileName = "SoundCue")]
    public sealed class SoundCue : ScriptableObject
    {
        [Tooltip("The takes. A different one each play, never the last one twice running.")]
        public AudioClip[] clips = new AudioClip[0];

        [Tooltip("Which volume slider this cue answers to.")]
        public SoundBus bus = SoundBus.Sfx;

        [Tooltip("Level, picked anew each play between these two.")]
        public Vector2 volume = new Vector2(0.85f, 1f);

        [Tooltip("Pitch, picked anew each play between these two. 1 is as recorded.")]
        public Vector2 pitch = new Vector2(0.95f, 1.05f);

        [Header("Space")]
        [Tooltip("Heard from where it happens, loudest in the middle of the screen. Off for the UI, " +
                 "stingers and beds, which are heard the same wherever the camera is.")]
        public bool spatial = true;

        [Tooltip("Metres from the listener inside which it plays at full level. The listener rides " +
                 "above the ground the camera looks at, so this is roughly 'on screen'.")]
        [Min(0.1f)] public float minDistance = 14f;

        [Tooltip("Metres from the listener at which it has faded out. A one-shot further away than " +
                 "this is not played at all, which keeps the voices for what can be heard.")]
        [Min(0.2f)] public float maxDistance = 50f;

        [Header("Playback")]
        [Tooltip("Plays until stopped. Loops are started and stopped by whoever owns them, through " +
                 "the handle the controller returns.")]
        public bool loop;

        [Tooltip("Seconds before this cue will play again. Calls inside the window are dropped.")]
        [Min(0f)] public float cooldown;

        [Tooltip("Most copies of this cue sounding at once. A call past it is dropped. 0 for no limit.")]
        [Min(0)] public int maxVoices = 4;

        [Tooltip("0 is the most important. With every voice busy, a cue may take one from a less " +
                 "important one-shot; it never takes one from a loop.")]
        [Range(0, 256)] public int priority = 128;

        [System.NonSerialized] int lastPick = -1;

        public bool HasClips
        {
            get
            {
                if (clips == null) return false;
                for (int i = 0; i < clips.Length; i++)
                    if (clips[i] != null) return true;
                return false;
            }
        }

        /// <summary>A take to play: random, and not the one played last.</summary>
        public AudioClip Pick()
        {
            if (clips == null || clips.Length == 0) return null;

            int index = PickIndex(clips.Length, lastPick, Random.value);
            AudioClip clip = clips[index];

            // A hole left in the list costs that take, not the sound.
            for (int step = 1; clip == null && step < clips.Length; step++)
            {
                index = (index + 1) % clips.Length;
                clip = clips[index];
            }

            lastPick = index;
            return clip;
        }

        public float PickVolume() => Random.Range(Mathf.Min(volume.x, volume.y), Mathf.Max(volume.x, volume.y));

        public float PickPitch() => Random.Range(Mathf.Min(pitch.x, pitch.y), Mathf.Max(pitch.x, pitch.y));

        /// <summary>
        /// Which of <paramref name="count"/> takes a roll of 0 to 1 lands on, never
        /// <paramref name="previous"/>: the roll is spread over the others, so each of them is as
        /// likely as the rest. Pure, for the tests.
        /// </summary>
        public static int PickIndex(int count, int previous, float roll)
        {
            if (count <= 1) return 0;

            roll = Mathf.Clamp(roll, 0f, 0.9999f);
            if (previous < 0 || previous >= count) return Mathf.Min((int)(roll * count), count - 1);

            int index = Mathf.Min((int)(roll * (count - 1)), count - 2);
            return index >= previous ? index + 1 : index;
        }

        void OnValidate()
        {
            maxDistance = Mathf.Max(maxDistance, minDistance + 0.1f);
        }
    }
}
