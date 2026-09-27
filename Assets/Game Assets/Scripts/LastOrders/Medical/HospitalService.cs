using System;
using System.Collections.Generic;
using System.Text;
using LastOrders.Data;
using LastOrders.Survivors;
using UnityEngine;

namespace LastOrders.Medical
{
    /// <summary>One bed, and whoever is in it.</summary>
    public sealed class HospitalCot
    {
        public readonly int Index;

        public HospitalCot(int index) => Index = index;

        /// <summary>Who the cot is held for. Null when it is free.</summary>
        public IPatient Patient { get; internal set; }

        /// <summary>
        /// The patient is actually lying in it. Treatment runs only from here: a visitor is
        /// given the cot the moment they are hurt, and the walk to it is not rest.
        /// </summary>
        public bool InBed { get; internal set; }

        /// <summary>Seconds of treatment received, at the normal pace.</summary>
        public float Treated { get; internal set; }

        /// <summary>Seconds of treatment this patient needs in all.</summary>
        public float Required { get; internal set; }

        public bool IsFree => Patient == null;

        public float Progress => Required <= 0f ? 1f : Mathf.Clamp01(Treated / Required);

        /// <summary>Real seconds until they are well, at the given treatment speed.</summary>
        public float SecondsLeft(float speed) => Mathf.Max(0f, Required - Treated) / Mathf.Max(0.01f, speed);
    }

    /// <summary>A roster member in a cot. Wraps the survivor so the tent can treat them like
    /// any other patient.</summary>
    public sealed class SurvivorPatient : IPatient
    {
        public readonly Survivor Survivor;

        public SurvivorPatient(Survivor survivor) => Survivor = survivor;

        public string PatientId => $"survivor:{Survivor.Id}";

        public string DisplayName => Survivor.Name;

        public PatientKind Kind => PatientKind.Survivor;

        /// <summary>"Mahmoud (Guard)". The role is what the player needs to hear: it is why
        /// the tower is empty.</summary>
        public string Label => Survivor.Role != null ? $"{Survivor.Name} ({Survivor.Role.displayName})" : Survivor.Name;
    }

    /// <summary>
    /// The hospital tent, as rules: who is in which cot, how far along they are, who is waiting
    /// for a bed, and who - if anyone - is posted there as medic.
    ///
    /// Recovery is automatic. Anyone admitted takes the first free cot and is treated until
    /// they are well; with both cots taken they wait, and the next cot to come free is theirs.
    /// Nobody has to be told to go and nobody has to be told to leave.
    ///
    /// A medic makes it faster, and is posted the same way a guard is: the first available
    /// Medic on the roster stands in the tent unless the player has chosen one. With no medic
    /// the tent still works, at its normal pace - the medic is a later improvement to a
    /// facility that already does its job, not a key to it.
    ///
    /// Owns nothing visual. The tent in the scene draws what this says, and the visitor system
    /// walks its own customers in and out; this only decides when.
    /// </summary>
    public sealed class HospitalService
    {
        readonly List<HospitalCot> cots = new List<HospitalCot>();
        readonly List<IPatient> waiting = new List<IPatient>();

        readonly HospitalDefinition definition;
        readonly SurvivorService survivors;
        readonly Func<bool> isPaused;

        Survivor chosenMedic;

        /// <param name="definition">Cots and stay lengths. Null uses the defaults on a fresh
        /// definition, so a scene with no hospital content still has a working tent.</param>
        /// <param name="isPaused">True while the game clock is stopped - the dawn screen - when
        /// nobody recovers.</param>
        public HospitalService(HospitalDefinition definition, SurvivorService survivors, Func<bool> isPaused)
        {
            this.definition = definition;
            this.survivors = survivors;
            this.isPaused = isPaused;

            int count = definition != null ? Mathf.Max(1, definition.cots) : 2;
            for (int i = 0; i < count; i++) cots.Add(new HospitalCot(i));
        }

        public string DisplayName => definition != null ? definition.displayName : "Hospital Tent";

        public IReadOnlyList<HospitalCot> Cots => cots;

        /// <summary>Patients hurt while every cot was taken, oldest first.</summary>
        public IReadOnlyList<IPatient> Waiting => waiting;

        /// <summary>The medic on duty in the tent. Null when nobody is.</summary>
        public Survivor Medic { get; private set; }

        /// <summary>How fast treatment runs: 1 on its own, faster with a medic.</summary>
        public float TreatmentSpeed
        {
            get
            {
                if (Medic == null) return 1f;
                return definition != null ? definition.MedicSpeed(Medic.Role) : 2f;
            }
        }

        public int OccupiedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < cots.Count; i++) if (!cots[i].IsFree) n++;
                return n;
            }
        }

        /// <summary>Given a cot. Raised the moment it is theirs, before they are in it.</summary>
        public event Action<IPatient, HospitalCot> OnAdmitted;

        /// <summary>Hurt, but every cot was taken. They will be admitted when one frees.</summary>
        public event Action<IPatient> OnQueued;

        /// <summary>Well again, and out of the cot.</summary>
        public event Action<IPatient> OnDischarged;

        /// <summary>A medic started or stopped standing in the tent (null).</summary>
        public event Action<Survivor> OnMedicChanged;

        // --- Admission ------------------------------------------------------------------------

        /// <summary>
        /// Takes someone in. The first free cot is theirs; with none free they wait for one.
        /// False only when they are already here, so hurting somebody twice is one stay.
        /// </summary>
        public bool Admit(IPatient patient)
        {
            if (patient == null || Contains(patient)) return false;

            if (patient is SurvivorPatient survivorPatient) survivorPatient.Survivor.InHospital = true;

            HospitalCot cot = FirstFreeCot();
            if (cot == null)
            {
                waiting.Add(patient);
                OnQueued?.Invoke(patient);
                return true;
            }

            Occupy(cot, patient);
            return true;
        }

        /// <summary>Takes a roster member in. They stop being available the moment they are
        /// hurt, whether or not a cot is free, so a hurt guard never stands a watch.</summary>
        public bool AdmitSurvivor(Survivor survivor) =>
            survivor != null && Admit(new SurvivorPatient(survivor));

        /// <summary>
        /// Says the patient is lying in their cot, which is when treatment starts. A survivor
        /// is in bed from the moment they are admitted; a visitor says so once they get there.
        /// </summary>
        public void MarkInBed(IPatient patient)
        {
            HospitalCot cot = CotOf(patient);
            if (cot != null) cot.InBed = true;
        }

        public bool Contains(IPatient patient) => patient != null && (CotOf(patient) != null || IndexInWaiting(patient) >= 0);

        public bool Contains(Survivor survivor)
        {
            if (survivor == null) return false;

            string id = $"survivor:{survivor.Id}";
            for (int i = 0; i < cots.Count; i++)
                if (cots[i].Patient != null && cots[i].Patient.PatientId == id) return true;

            for (int i = 0; i < waiting.Count; i++)
                if (waiting[i].PatientId == id) return true;

            return false;
        }

        /// <summary>The cot a patient holds. Null when they are waiting or not here.</summary>
        public HospitalCot CotOf(IPatient patient)
        {
            if (patient == null) return null;

            for (int i = 0; i < cots.Count; i++)
                if (cots[i].Patient != null && cots[i].Patient.PatientId == patient.PatientId) return cots[i];

            return null;
        }

        // --- Treatment ------------------------------------------------------------------------

        /// <summary>Posts the medic, then treats everyone in bed. Called once per frame.</summary>
        public void Tick(float deltaSeconds)
        {
            StaffMedic();

            if (deltaSeconds <= 0f || (isPaused != null && isPaused())) return;

            float treated = deltaSeconds * TreatmentSpeed;

            for (int i = 0; i < cots.Count; i++)
            {
                HospitalCot cot = cots[i];
                if (cot.IsFree || !cot.InBed) continue;

                cot.Treated += treated;
                if (cot.Treated >= cot.Required) Discharge(cot);
            }
        }

        void Discharge(HospitalCot cot)
        {
            IPatient patient = cot.Patient;

            cot.Patient = null;
            cot.InBed = false;
            cot.Treated = 0f;
            cot.Required = 0f;

            // Back at work the moment they are out of the cot. The guard post re-crews on its
            // own next tick, so nothing here has to know which tower was empty.
            if (patient is SurvivorPatient survivorPatient) survivorPatient.Survivor.InHospital = false;

            OnDischarged?.Invoke(patient);

            // The freed cot goes to whoever has waited longest, before anyone new can take it.
            if (waiting.Count > 0)
            {
                IPatient next = waiting[0];
                waiting.RemoveAt(0);
                Occupy(cot, next);
            }
        }

        void Occupy(HospitalCot cot, IPatient patient)
        {
            cot.Patient = patient;
            cot.Treated = 0f;
            cot.Required = RecoverySecondsFor(patient);

            // A survivor has no body to walk over, so they are in bed from the start.
            cot.InBed = patient.Kind == PatientKind.Survivor;

            OnAdmitted?.Invoke(patient, cot);
        }

        float RecoverySecondsFor(IPatient patient)
        {
            if (patient.Kind == PatientKind.Visitor)
                return definition != null ? definition.visitorRecoverySeconds : 90f;

            return definition != null ? definition.survivorRecoverySeconds : 150f;
        }

        HospitalCot FirstFreeCot()
        {
            for (int i = 0; i < cots.Count; i++)
                if (cots[i].IsFree) return cots[i];

            return null;
        }

        int IndexInWaiting(IPatient patient)
        {
            for (int i = 0; i < waiting.Count; i++)
                if (waiting[i].PatientId == patient.PatientId) return i;

            return -1;
        }

        // --- The medic ------------------------------------------------------------------------

        SurvivorRole MedicRole => definition != null ? definition.medicRole : SurvivorRole.Medic;

        /// <summary>
        /// Posts a particular survivor as the tent's medic. They stay while they are available;
        /// the automatic choice only covers for them when they are not.
        /// </summary>
        public bool AssignMedic(Survivor survivor, out string message)
        {
            if (!IsMedic(survivor))
            {
                message = $"Only a {MedicRole} can look after the patients in the {DisplayName}.";
                return false;
            }

            if (!survivor.IsAvailable)
            {
                message = $"{survivor.Name} is {(survivor.IsInjured ? "injured" : "away")} and cannot look after anyone.";
                return false;
            }

            chosenMedic = survivor;
            StaffMedic();

            message = $"{survivor.Name} is looking after the {DisplayName}. Patients recover {TreatmentSpeed:0.#}x as fast.";
            return true;
        }

        /// <summary>Clears the player's choice. The first available Medic stands in again.</summary>
        public void UnassignMedic()
        {
            chosenMedic = null;
            StaffMedic();
        }

        void StaffMedic()
        {
            Survivor next = chosenMedic != null && chosenMedic.IsAvailable ? chosenMedic : FirstAvailableMedic();
            if (ReferenceEquals(next, Medic)) return;

            Medic = next;
            OnMedicChanged?.Invoke(next);
        }

        Survivor FirstAvailableMedic()
        {
            if (survivors == null) return null;

            IReadOnlyList<Survivor> roster = survivors.Roster;
            for (int i = 0; i < roster.Count; i++)
                if (IsMedic(roster[i]) && roster[i].IsAvailable) return roster[i];

            return null;
        }

        bool IsMedic(Survivor survivor) =>
            survivor != null && survivor.Role != null && survivor.Role.role == MedicRole;

        // --- Describing -----------------------------------------------------------------------

        /// <summary>The tap reply for the tent: who is in each cot, how long they have left, and
        /// what a medic would change.</summary>
        public string Describe()
        {
            var text = new StringBuilder();
            text.Append($"{DisplayName}: ");

            float speed = TreatmentSpeed;
            int occupied = 0;

            for (int i = 0; i < cots.Count; i++)
            {
                HospitalCot cot = cots[i];
                if (cot.IsFree) continue;

                if (occupied++ > 0) text.Append(", ");
                text.Append(LabelOf(cot.Patient));
                text.Append(cot.InBed
                    ? $" {Mathf.RoundToInt(cot.Progress * 100f)}% ({Mathf.CeilToInt(cot.SecondsLeft(speed))}s)"
                    : " on the way");
            }

            if (occupied == 0) text.Append($"{cots.Count} empty cots");
            if (waiting.Count > 0) text.Append($". {waiting.Count} waiting for a cot");

            text.Append(Medic != null
                ? $". {Medic.Name} is the medic - recovery is {speed:0.#}x as fast."
                : $". No medic, so recovery runs at the normal pace. Hire a {MedicRole} at a shelter to speed it up.");

            return text.ToString();
        }

        static string LabelOf(IPatient patient) =>
            patient is SurvivorPatient survivor ? survivor.Label : patient.DisplayName;
    }
}
