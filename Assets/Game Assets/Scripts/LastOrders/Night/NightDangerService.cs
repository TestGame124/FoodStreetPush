using System;
using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Defense;
using LastOrders.Medical;
using LastOrders.Survivors;
using UnityEngine;

namespace LastOrders.Night
{
    /// <summary>What a night did to people. Empty on the nights it did nothing.</summary>
    public readonly struct NightInjury
    {
        public readonly IPatient Patient;

        /// <summary>The roster member hurt, when it was one. Null for a visitor.</summary>
        public readonly Survivor Survivor;

        /// <summary>Zombie-seconds at the diner door, which is what made the night difficult.</summary>
        public readonly float Pressure;

        public NightInjury(IPatient patient, Survivor survivor, float pressure)
        {
            Patient = patient;
            Survivor = survivor;
            Pressure = pressure;
        }

        public bool Happened => Patient != null;
    }

    /// <summary>
    /// Decides whether a night was difficult, and whether it hurt somebody.
    ///
    /// Difficult is measured, not declared: every second a zombie spends at the diner door is a
    /// zombie-second of pressure, and the door is where they end up once they are past the
    /// guard. A night the gate tower handled scores nothing. A night where one got through and
    /// hammered on the door until dawn scores a great deal - and that is the night somebody gets
    /// hurt.
    ///
    /// Only the people who were exposed can be hurt: the guards standing watch, and any visitor
    /// who was still at a table when night fell and sheltered in the diner. The Chef and the
    /// Helper stay inside and are not at risk, which is the point of sending them in.
    ///
    /// The outcome is a pure function of the night's pressure, its day and the campaign seed, so
    /// replaying a night gives the same result. At most one person is hurt per night: this is
    /// the starting threat, and one guard in a cot is already a bad morning.
    /// </summary>
    public sealed class NightDangerService
    {
        readonly GameBalanceConfig balance;
        readonly HospitalService hospital;
        readonly DefenseService defense;
        readonly Func<IReadOnlyList<IDefenseTarget>> hostiles;

        readonly List<IPatient> civilians = new List<IPatient>();
        readonly HashSet<IDefenseTarget> reachedDoor = new HashSet<IDefenseTarget>();

        Vector3 door;
        bool hasDoor;

        public NightDangerService(GameBalanceConfig balance, HospitalService hospital, DefenseService defense,
                                  Func<IReadOnlyList<IDefenseTarget>> hostiles)
        {
            this.balance = balance;
            this.hospital = hospital;
            this.defense = defense;
            this.hostiles = hostiles;
        }

        /// <summary>Zombie-seconds at the diner door so far tonight.</summary>
        public float Pressure { get; private set; }

        /// <summary>How many different zombies reached the door tonight.</summary>
        public int ReachedDoorCount => reachedDoor.Count;

        /// <summary>Visitors sheltering in the diner tonight.</summary>
        public IReadOnlyList<IPatient> Civilians => civilians;

        public bool HasDoor => hasDoor;

        /// <summary>Raised once, the first time a zombie reaches the door on a night. The moment
        /// a night stops being fine, which the player should hear about while it is happening.</summary>
        public event Action OnDoorReached;

        /// <summary>Where the people are. The scene's shelter sets it: the diner's door.</summary>
        public void SetShelterPoint(Vector3 point)
        {
            door = point;
            hasDoor = true;
        }

        public void RegisterCivilian(IPatient civilian)
        {
            if (civilian != null && !civilians.Contains(civilian)) civilians.Add(civilian);
        }

        public void UnregisterCivilian(IPatient civilian) => civilians.Remove(civilian);

        /// <summary>Starts a new night's measurement.</summary>
        public void BeginNight()
        {
            Pressure = 0f;
            reachedDoor.Clear();
        }

        /// <summary>Adds this frame's pressure. Called during the night only.</summary>
        public void Tick(float deltaSeconds)
        {
            if (!hasDoor || deltaSeconds <= 0f) return;

            IReadOnlyList<IDefenseTarget> targets = hostiles?.Invoke();
            if (targets == null) return;

            float radius = balance != null ? balance.dinerDangerRadius : 3.5f;
            float radiusSquared = radius * radius;

            for (int i = 0; i < targets.Count; i++)
            {
                IDefenseTarget target = targets[i];
                if (target == null || (target is UnityEngine.Object o && o == null) || !target.IsAlive) continue;
                if (DefenseUnit.FlatDistanceSquared(target.Position, door) > radiusSquared) continue;

                Pressure += deltaSeconds;

                if (reachedDoor.Add(target) && reachedDoor.Count == 1) OnDoorReached?.Invoke();
            }
        }

        /// <summary>
        /// The chance a night with this much pressure hurts someone. Zero below the threshold:
        /// a zombie that touched the door and was dealt with is a story, not an injury.
        /// </summary>
        public static float InjuryChance(float pressure, GameBalanceConfig tuning)
        {
            float threshold = tuning != null ? tuning.injuryPressureThreshold : 5f;
            if (pressure < threshold || pressure <= 0f) return 0f;

            float atThreshold = tuning != null ? tuning.injuryChanceAtThreshold : 0.35f;
            float perSecond = tuning != null ? tuning.injuryChancePerPressureSecond : 0.03f;

            return Mathf.Clamp01(atThreshold + (pressure - threshold) * perSecond);
        }

        /// <summary>
        /// Settles the night: rolls for an injury, picks who among the exposed was hurt, and
        /// sends them to the hospital tent. Called once, as the night ends.
        /// </summary>
        public NightInjury Resolve(int day)
        {
            float pressure = Pressure;
            float chance = InjuryChance(pressure, balance);
            if (chance <= 0f) return default;

            var exposed = new List<IPatient>(civilians.Count + 2);
            exposed.AddRange(civilians);
            AddGuardsOnWatch(exposed);

            if (exposed.Count == 0) return default;

            // Seeded from the pressure as well as the day, so the same night always ends the
            // same way - and a worse version of the same night is a different roll.
            int seed = balance != null ? balance.campaignSeed : 0;
            var rng = DeterministicRandom.ForNight(day, Mathf.RoundToInt(pressure * 10f), seed, salt: 5151);

            if (!rng.Chance(chance)) return default;

            IPatient hurt = exposed[rng.Range(0, exposed.Count)];
            Survivor survivor = (hurt as SurvivorPatient)?.Survivor;

            // Out of the diner and into a cot. Nobody sheltering is still sheltering once they
            // are a patient.
            civilians.Remove(hurt);
            hospital?.Admit(hurt);

            return new NightInjury(hurt, survivor, pressure);
        }

        void AddGuardsOnWatch(List<IPatient> into)
        {
            if (defense == null) return;

            IReadOnlyList<DefenseUnit> units = defense.All;
            for (int i = 0; i < units.Count; i++)
            {
                DefenseUnit unit = units[i];
                if (!unit.IsBuilt || !unit.NeedsCrew || unit.Crew == null || !unit.CrewOnDuty) continue;

                into.Add(new SurvivorPatient(unit.Crew));
            }
        }
    }
}
