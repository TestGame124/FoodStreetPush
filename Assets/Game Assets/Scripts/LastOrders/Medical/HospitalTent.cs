using System;
using LastOrders.Core;
using LastOrders.Interaction;
using LastOrders.UI.World;
using UnityEngine;

namespace LastOrders.Medical
{
    /// <summary>
    /// The hospital tent in the scene: two cots beside the guarded entrance, drawn as the
    /// hospital service says they are.
    ///
    /// A cot with somebody in it shows a blanket and a bar filling over it as they recover. A
    /// guard has no body walking about the compound, so the tent draws one in the cot for them;
    /// a visitor lies down in it themselves, and the tent only covers them up. When a medic is
    /// posted they stand between the cots - the one visible sign that recovery is running fast.
    ///
    /// Everything about who goes in and when is the service's. This owns the positions -
    /// where the cots are, where the way in is, where the healed wait for their shuttle - and
    /// the drawing.
    /// </summary>
    public sealed class HospitalTent : MonoBehaviour, ITappable, IInteractableObject
    {
        [Serializable]
        public struct Cot
        {
            [Tooltip("The foot of the mattress. Forward points up the cot toward the pillow.")]
            public Transform bed;

            [Tooltip("Shown over whoever is lying here.")]
            public GameObject blanket;

            [Tooltip("The patient drawn for a survivor, who has no body of their own in the scene.")]
            public GameObject patientFigure;

            [Tooltip("Where the recovery bar floats.")]
            public Transform barAnchor;
        }

        [SerializeField] GameLifetimeScope scope;

        [SerializeField] Cot[] cots;

        [Tooltip("Shown while a medic is posted to the tent.")]
        [SerializeField] GameObject medicFigure;

        [Tooltip("Just outside the way in.")]
        [SerializeField] Transform entrance;

        [Tooltip("Where visitors who are well again wait for the shuttle. One per spot.")]
        [SerializeField] Transform[] benches;

        [Tooltip("The line over the tent when somebody is well again.")]
        [SerializeField] Color recoveredColour = new Color(0.55f, 0.95f, 0.55f);

        [SerializeField] Vector3 calloutOffset = new Vector3(0f, 3.2f, 0f);

        GameComposition composition;
        HospitalService hospital;
        WorkProgressView[] bars;

        public string DisplayName => hospital != null ? hospital.DisplayName : "Hospital Tent";

        public Vector3 Entrance => entrance != null ? entrance.position : transform.position;

        public int BenchCount => benches != null ? benches.Length : 0;

        /// <summary>
        /// Where someone carried in lies while every cot is taken: on the ground beside the way
        /// in, a body's width apart, until a cot comes free.
        /// </summary>
        public Pose WaitingPose(int index)
        {
            Vector3 side = transform.right * (1.2f + Mathf.Max(0, index) * 0.9f);
            Vector3 at = Entrance + side + Vector3.up * 0.15f;

            return new Pose(at, Quaternion.LookRotation(Vector3.up, -transform.forward));
        }

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                Debug.LogError($"[HospitalTent] {name} found no GameLifetimeScope, so nobody can be treated.", this);
                enabled = false;
                return;
            }

            hospital = composition.Hospital;

            if (cots == null || cots.Length < hospital.Cots.Count)
            {
                Debug.LogWarning($"[HospitalTent] The tent has {(cots != null ? cots.Length : 0)} cots drawn " +
                                 $"but the hospital has {hospital.Cots.Count}. The extra patients are treated " +
                                 "but not shown.", this);
            }

            bars = new WorkProgressView[cots != null ? cots.Length : 0];
            hospital.OnDischarged += HandleDischarged;

            Redraw();
        }

        void OnDestroy()
        {
            if (hospital != null) hospital.OnDischarged -= HandleDischarged;

            if (bars == null) return;
            for (int i = 0; i < bars.Length; i++) HideBar(i);
        }

        void Update()
        {
            if (hospital != null) Redraw();
        }

        // --- Drawing ---------------------------------------------------------------------------

        void Redraw()
        {
            int drawn = Mathf.Min(cots != null ? cots.Length : 0, hospital.Cots.Count);

            for (int i = 0; i < drawn; i++)
            {
                HospitalCot cot = hospital.Cots[i];
                bool inBed = !cot.IsFree && cot.InBed;

                SetActive(cots[i].blanket, inBed);
                SetActive(cots[i].patientFigure, inBed && cot.Patient.Kind == PatientKind.Survivor);

                if (cot.IsFree)
                {
                    HideBar(i);
                    continue;
                }

                WorkProgressView bar = ShowBar(i);
                if (bar == null) continue;

                if (inBed) bar.SetProgress(cot.Progress, cot.SecondsLeft(hospital.TreatmentSpeed));
                else bar.SetWaiting();
            }

            SetActive(medicFigure, hospital.Medic != null);
        }

        WorkProgressView ShowBar(int index)
        {
            if (bars[index] != null && bars[index].IsLive) return bars[index];

            Transform anchor = cots[index].barAnchor != null ? cots[index].barAnchor : cots[index].bed;
            if (anchor == null) return null;

            bars[index] = composition.WorldUi.ShowProgress(anchor);
            return bars[index];
        }

        void HideBar(int index)
        {
            if (bars[index] != null && bars[index].IsLive) bars[index].Release();
            bars[index] = null;
        }

        void HandleDischarged(IPatient patient)
        {
            // Survivors by name. A visitor is only ever known by what they are.
            string line;

            if (patient is SurvivorPatient survivor)
            {
                bool guard = survivor.Survivor.Role != null && survivor.Survivor.Role.role == Data.SurvivorRole.Guard;
                line = guard ? $"{patient.DisplayName} is back on watch" : $"{patient.DisplayName} is back at work";
            }
            else if (patient.Kind == PatientKind.Worker)
            {
                line = $"{patient.DisplayName} is back on their feet";
            }
            else
            {
                line = $"The {patient.DisplayName.ToLowerInvariant()} is well again";
            }

            composition.WorldUi.ShowFloatingText(transform.position + calloutOffset, line, recoveredColour);
            TapFeedback.Report($"{line}.");
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }

        // --- Where people go ---------------------------------------------------------------------

        /// <summary>
        /// Where a body lies in a cot, and how: feet at the foot of the mattress, face up, head
        /// toward the pillow. Works for any of the project's characters, whose pivot is at the feet.
        /// </summary>
        public Pose CotPose(int index)
        {
            Transform bed = cots != null && index >= 0 && index < cots.Length ? cots[index].bed : null;
            if (bed == null) return new Pose(Entrance, transform.rotation);

            return new Pose(bed.position, Quaternion.LookRotation(Vector3.up, bed.forward));
        }

        /// <summary>A spot to wait for the shuttle. Wraps, so a third visitor shares the first spot.</summary>
        public Vector3 Bench(int index)
        {
            if (benches == null || benches.Length == 0) return Entrance;

            Transform bench = benches[((index % benches.Length) + benches.Length) % benches.Length];
            return bench != null ? bench.position : Entrance;
        }

        // --- Tapping -------------------------------------------------------------------------------

        public bool OnTouch() => TapFeedback.Report(OnTapped());

        public string OnTapped() =>
            hospital != null ? hospital.Describe() : $"The {DisplayName} is not connected to the game.";

        /// <summary>Editor wiring, for the prefab generator.</summary>
        public void Configure(Cot[] beds, GameObject medic, Transform way, Transform[] waiting)
        {
            cots = beds;
            medicFigure = medic;
            entrance = way;
            benches = waiting;
        }

        /// <summary>Editor wiring, for the scene setup.</summary>
        public void Configure(GameLifetimeScope lifetimeScope) => scope = lifetimeScope;
    }
}
