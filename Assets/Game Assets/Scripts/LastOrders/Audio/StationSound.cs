using UnityEngine;

namespace LastOrders.Audio
{
    /// <summary>
    /// What a cooking station sounds like: the grill sizzling, the soup on the boil, the oven's
    /// roar - for as long as something is on it - and a click as a dish goes on and a ding as one
    /// comes off.
    ///
    /// An add-on, so the cooking code knows nothing of it. It watches the station's own queue: one
    /// more in it is a dish started, one fewer is a dish done, any at all is a station at work. The
    /// loop fades in and out with that rather than cutting, and sounds from the station's middle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StationSound : MonoBehaviour
    {
        [SerializeField] CookeryStation station;

        [Tooltip("Loops while anything is cooking. None for a station that only clicks and dings.")]
        [SerializeField] SoundId working = SoundId.None;

        [SerializeField] SoundId started = SoundId.CookStart;
        [SerializeField] SoundId finished = SoundId.CookDone;

        [Tooltip("On top of the working loop's own level.")]
        [SerializeField, Range(0f, 1f)] float workingVolume = 1f;

        [Tooltip("Seconds the working loop takes to fade in and out.")]
        [SerializeField, Min(0f)] float fadeSeconds = 0.8f;

        SoundHandle loop;
        int lastCount;

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(CookeryStation cookery, SoundId loopWhileCooking, float level = 1f)
        {
            station = cookery;
            working = loopWhileCooking;
            workingVolume = level;
        }

        void Awake()
        {
            if (station == null) station = GetComponentInParent<CookeryStation>();
        }

        void OnEnable() => lastCount = Count;

        void OnDisable() => loop.Stop(fadeSeconds);

        int Count => station != null && station.recipiesInProgress != null ? station.recipiesInProgress.Count : 0;

        Transform Middle => station != null && station.centerPoint != null ? station.centerPoint : transform;

        void Update()
        {
            int count = Count;

            if (count > lastCount) SoundController.Play(started, Middle.position);
            else if (count < lastCount) SoundController.Play(finished, Middle.position);
            lastCount = count;

            bool busy = count > 0 && working != SoundId.None;
            if (busy && !loop.IsPlaying) loop = SoundController.PlayAttached(working, Middle, workingVolume, fadeSeconds);
            else if (!busy && loop.IsPlaying) loop.Stop(fadeSeconds);
        }
    }
}
