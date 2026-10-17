using System;
using LastOrders.Jobs;
using UnityEngine;

namespace LastOrders.Crew
{
    /// <summary>
    /// Helping in the kitchen: sent to a station, a crew member stands at it and lends a hand, and
    /// the station cooks a little faster for it (<see cref="Boost"/>) - only once they are there, and
    /// only while they stay. Hands busy while it is cooking; between cooks they wait at it, watching it.
    ///
    /// The most important of the everyday duties: while helping they do not serve or clean, and the
    /// player's taps serve customers straight from the pass. Sent back (<see cref="Release"/>), or the
    /// station taken away, and they are back on the floor.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CrewBrain))]
    public sealed class StationAssist : MonoBehaviour, ICrewDuty
    {
        [Tooltip("How much faster a station cooks with them helping at it: 0.15 is 15% faster.")]
        [SerializeField, Range(0f, 1f)] float boost = 0.15f;

        [SerializeField] int priority = 30;

        [Tooltip("Metres out from the station's working edge they stand.")]
        [SerializeField] float standOff = 0.45f;

        [Tooltip("A share of walking speed on the way to the kitchen: brisk.")]
        [SerializeField, Range(0.3f, 1.5f)] float pace = 1f;

        CrewBrain brain;
        CookeryStation station;
        bool atStation;
        Pose spot;
        Vector3 top;
        float idleClock;

        /// <summary>Raised when somebody is sent to a station, starts helping at it, or leaves it.</summary>
        public static event Action<StationAssist> OnChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => OnChanged = null;

        public CrewBrain Brain => brain != null ? brain : brain = GetComponent<CrewBrain>();

        /// <summary>The station they are sent to, or null while they are on the floor.</summary>
        public CookeryStation Station => station;

        /// <summary>At the station and speeding it up.</summary>
        public bool IsHelping => station != null && atStation;

        public float Boost => boost;

        public int Priority => priority;

        public bool HasWork => station != null;

        public string Activity
        {
            get
            {
                if (station == null) return "";
                string name = FixtureChores.StationName(station);
                return atStation ? $"Helping at the {name} (+{Mathf.RoundToInt(boost * 100f)}% speed)" : $"On the way to help at the {name}";
            }
        }

        void Awake() => brain = GetComponent<CrewBrain>();

        void OnDisable() => Release();

        /// <summary>Sends them to help at <paramref name="target"/>; null sends them back to the floor.</summary>
        public void AssignTo(CookeryStation target)
        {
            if (target == station) return;

            Leave();
            station = target;
            if (station != null)
            {
                spot = FixtureChores.StationSpot(station, standOff, out top);

                // Moved from one station to another: straight over, without going back to the floor first.
                if (Brain.Active == (ICrewDuty)this)
                {
                    brain.Rest();
                    brain.Legs.WalkTo(spot.position, spot.rotation * Vector3.forward, pace);
                }
            }

            OnChanged?.Invoke(this);
        }

        /// <summary>Back to serving and cleaning.</summary>
        public void Release() => AssignTo(null);

        public void SetActive(bool active)
        {
            if (active)
            {
                if (station == null) return;
                atStation = false;
                spot = FixtureChores.StationSpot(station, standOff, out top);
                Brain.Legs.WalkTo(spot.position, spot.rotation * Vector3.forward, pace);
                return;
            }

            Leave();
        }

        public void Tick(float dt)
        {
            // The station was sold, moved into storage or wrecked away.
            if (station == null || !station.isActiveAndEnabled)
            {
                Release();
                return;
            }

            if (!atStation)
            {
                if (!brain.Legs.Arrive()) return;

                atStation = true;
                station.AddBoost(this, boost);
                OnChanged?.Invoke(this);
            }

            bool cooking = station.recipiesInProgress != null && station.recipiesInProgress.Count > 0;
            brain.SetWorking(cooking, WorkerGestures.WorkHands);

            if (brain.Look != null) brain.Look.Watch(top + Vector3.up * (cooking ? 0f : 0.2f), cooking ? 0.85f : 0.5f);

            // Between cooks, waiting at it rather than standing frozen.
            if (cooking || brain.Gestures.Busy) return;
            idleClock += dt;
            if (idleClock < 4f) return;

            idleClock = 0f;
            float roll = UnityEngine.Random.value;
            if (roll < 0.35f) brain.Gestures.Play(Gesture.ArmsCrossed, UnityEngine.Random.Range(3f, 5f));
            else if (roll < 0.55f) brain.Gestures.Play(Gesture.HandsOnHips, UnityEngine.Random.Range(2f, 4f));
            else if (roll < 0.65f) brain.Gestures.Play(Gesture.WipeBrow);
        }

        /// <summary>Takes the boost off the station; they are no longer at it.</summary>
        void Leave()
        {
            if (station != null) station.RemoveBoost(this);
            bool was = atStation;
            atStation = false;
            idleClock = 0f;
            if (was) OnChanged?.Invoke(this);
        }
    }
}
