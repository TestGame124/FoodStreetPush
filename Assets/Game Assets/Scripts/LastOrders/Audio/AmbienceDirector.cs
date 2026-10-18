using System;
using LastOrders.Core;
using LastOrders.DayCycle;
using UnityEngine;

namespace LastOrders.Audio
{
    /// <summary>
    /// Keeps the right bed under the day: birds over the fields in the morning, the farm through
    /// the working day, the evening thinning out at dusk and crickets in the dark.
    ///
    /// Follows the day cycle's phase rather than the clock, so the bed changes when the light does
    /// (<see cref="Blockout.DayNightLighting"/> reads the same phase), and crossfades over a few
    /// seconds so the change is the world going quiet rather than a recording stopping.
    ///
    /// A scene with no Last Orders layer has no phases; it gets the day's bed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbienceDirector : MonoBehaviour
    {
        [Serializable]
        public struct PhaseBed
        {
            public DayPhase phase;
            public SoundId bed;
        }

        [SerializeField] GameLifetimeScope scope;

        [SerializeField] PhaseBed[] beds =
        {
            new PhaseBed { phase = DayPhase.Dawn, bed = SoundId.AmbienceDawn },
            new PhaseBed { phase = DayPhase.Day, bed = SoundId.AmbienceDay },
            new PhaseBed { phase = DayPhase.Dusk, bed = SoundId.AmbienceDusk },
            new PhaseBed { phase = DayPhase.Night, bed = SoundId.AmbienceNight },
            new PhaseBed { phase = DayPhase.Resolution, bed = SoundId.AmbienceNight },
        };

        [Tooltip("The bed when there is no day cycle in the scene.")]
        [SerializeField] SoundId withoutCycle = SoundId.AmbienceDay;

        [Tooltip("Seconds one bed takes to become the next.")]
        [SerializeField, Min(0f)] float crossfadeSeconds = 5f;

        [Tooltip("Seconds the first bed takes to come up when the scene starts.")]
        [SerializeField, Min(0f)] float openingFadeSeconds = 2.5f;

        GameComposition composition;

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(GameLifetimeScope lifetimeScope) => scope = lifetimeScope;

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                SoundController.SetAmbience(withoutCycle, openingFadeSeconds);
                return;
            }

            composition.Machine.OnPhaseChanged += HandlePhaseChanged;
            SoundController.SetAmbience(BedFor(composition.Machine.CurrentPhase), openingFadeSeconds);
        }

        void OnDestroy()
        {
            if (composition != null) composition.Machine.OnPhaseChanged -= HandlePhaseChanged;
        }

        void HandlePhaseChanged(DayPhase previous, DayPhase current) =>
            SoundController.SetAmbience(BedFor(current), previous == current ? openingFadeSeconds : crossfadeSeconds);

        SoundId BedFor(DayPhase phase)
        {
            if (beds != null)
                for (int i = 0; i < beds.Length; i++)
                    if (beds[i].phase == phase) return beds[i].bed;

            return withoutCycle;
        }
    }
}
