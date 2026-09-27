using System;
using System.Collections.Generic;
using LastOrders.DayCycle;
using LastOrders.Defense;
using UnityEngine;

namespace LastOrders.Perimeter
{
    /// <summary>
    /// The compound's outer wall as the rules see it: where inside is, which sections and gates
    /// stand, and whether anything has got in.
    ///
    /// A plain class with no scene in it. The wall sections, the gates and the interior areas in
    /// the scene register here and do their own drawing.
    ///
    /// "Inside" is authored, not worked out from the wall: a set of rectangles that together are
    /// the ground behind the wall. The moment a zombie is standing on any of them the compound is
    /// breached - the staff drop what they are doing and take cover in the shelters, and the
    /// visitors at the tables stop eating. It stays breached until nothing has been inside for a
    /// few seconds, so a zombie stepping back and forth over the gate line does not send everyone
    /// in and out again with every step.
    ///
    /// What the night knocked down stays down until a worker is sent to fix it - see
    /// <see cref="Repair.RepairService"/>. <see cref="RepairsAtDawn"/> brings back the old rule,
    /// where everything was rebuilt by dawn and the next night started from the same line.
    /// </summary>
    public sealed class CompoundPerimeterService
    {
        readonly Func<IReadOnlyList<IDefenseTarget>> hostiles;
        readonly DayCycleMachine machine;

        readonly Dictionary<object, Rect> zones = new Dictionary<object, Rect>();
        readonly List<ICompoundStructure> structures = new List<ICompoundStructure>();

        float clearTimer;

        public CompoundPerimeterService(Func<IReadOnlyList<IDefenseTarget>> hostiles, DayCycleMachine machine)
        {
            this.hostiles = hostiles;
            this.machine = machine;

            if (machine != null) machine.OnPhaseChanged += HandlePhaseChanged;
        }

        /// <summary>Seconds with nothing inside before the compound counts as clear again.</summary>
        public float ClearDelaySeconds { get; set; } = 3f;

        /// <summary>
        /// Rebuild everything by magic at dawn. Off in the game, where a worker has to go out and
        /// fix each piece; on by default so a service built on its own behaves as it always has.
        /// </summary>
        public bool RepairsAtDawn { get; set; } = true;

        /// <summary>True from the moment a zombie is inside the walls until the compound has been
        /// clear for <see cref="ClearDelaySeconds"/>.</summary>
        public bool IsBreached { get; private set; }

        /// <summary>How many zombies were inside at the last check.</summary>
        public int HostilesInside { get; private set; }

        /// <summary>True once the scene has said where inside is. Without it nothing can breach.</summary>
        public bool HasInterior => zones.Count > 0;

        public IReadOnlyList<ICompoundStructure> Structures => structures;

        /// <summary>Something got in. Raised once per breach.</summary>
        public event Action OnBreached;

        /// <summary>The last of them is gone, or dead.</summary>
        public event Action OnCleared;

        /// <summary>Raised at dawn with how many wall sections and gates were rebuilt or patched.</summary>
        public event Action<int> OnRepaired;

        /// <summary>The first blow on the wall since dawn, with what was hit. Once a night: the
        /// moment the player should look at the wall, not every blow after it.</summary>
        public event Action<ICompoundStructure> OnUnderAttack;

        /// <summary>A wall section or a gate has been knocked down.</summary>
        public event Action<ICompoundStructure> OnStructureDown;

        bool attackReported;

        // --- The scene registers ------------------------------------------------------------------

        /// <summary>Adds, or moves, a piece of the interior. <paramref name="owner"/> is whatever
        /// stands for it in the scene; registering the same owner again replaces its rectangle.</summary>
        public void RegisterZone(object owner, Rect area)
        {
            if (owner != null) zones[owner] = area;
        }

        public void UnregisterZone(object owner)
        {
            if (owner != null) zones.Remove(owner);
        }

        public void Register(ICompoundStructure structure)
        {
            if (structure != null && !structures.Contains(structure)) structures.Add(structure);
        }

        public void Unregister(ICompoundStructure structure) => structures.Remove(structure);

        /// <summary>Called by a piece on every blow it takes.</summary>
        public void ReportHit(ICompoundStructure structure)
        {
            if (attackReported) return;

            attackReported = true;
            OnUnderAttack?.Invoke(structure);
        }

        /// <summary>Called by a piece the moment it falls.</summary>
        public void ReportDestroyed(ICompoundStructure structure) => OnStructureDown?.Invoke(structure);

        // --- Questions ----------------------------------------------------------------------------

        /// <summary>True when the point is on the ground behind the wall.</summary>
        public bool IsInside(Vector3 point)
        {
            var flat = new Vector2(point.x, point.z);

            foreach (Rect area in zones.Values)
                if (area.Contains(flat)) return true;

            return false;
        }

        /// <summary>True when a living zombie is within <paramref name="radius"/> of the point,
        /// measured flat. What keeps a gate shut while one is at it.</summary>
        public bool HostileWithin(Vector3 point, float radius)
        {
            IReadOnlyList<IDefenseTarget> targets = hostiles?.Invoke();
            if (targets == null) return false;

            float radiusSquared = radius * radius;

            for (int i = 0; i < targets.Count; i++)
            {
                IDefenseTarget target = targets[i];
                if (!IsLive(target)) continue;

                if (DefenseUnit.FlatDistanceSquared(target.Position, point) <= radiusSquared) return true;
            }

            return false;
        }

        // --- The breach ---------------------------------------------------------------------------

        public void Tick(float deltaSeconds)
        {
            HostilesInside = CountInside();

            if (HostilesInside > 0)
            {
                clearTimer = ClearDelaySeconds;

                if (IsBreached) return;

                IsBreached = true;
                Debug.Log($"[Last Orders] Zombies are inside the walls ({HostilesInside}). The staff are taking cover.");
                OnBreached?.Invoke();
                return;
            }

            if (!IsBreached) return;

            clearTimer -= deltaSeconds;
            if (clearTimer > 0f) return;

            EndBreach();
        }

        int CountInside()
        {
            if (zones.Count == 0) return 0;

            IReadOnlyList<IDefenseTarget> targets = hostiles?.Invoke();
            if (targets == null) return 0;

            int inside = 0;
            for (int i = 0; i < targets.Count; i++)
                if (IsLive(targets[i]) && IsInside(targets[i].Position)) inside++;

            return inside;
        }

        void EndBreach()
        {
            if (!IsBreached) return;

            IsBreached = false;
            clearTimer = 0f;
            Debug.Log("[Last Orders] The compound is clear again.");
            OnCleared?.Invoke();
        }

        static bool IsLive(IDefenseTarget target) =>
            target != null && !(target is UnityEngine.Object o && o == null) && target.IsAlive;

        // --- Dawn ---------------------------------------------------------------------------------

        void HandlePhaseChanged(DayPhase previous, DayPhase current)
        {
            if (current != DayPhase.Dawn || previous == current) return;

            // The night is over and whatever was left of it has been dismissed.
            EndBreach();
            if (RepairsAtDawn) RepairAll();
            attackReported = false;
        }

        /// <summary>Rebuilds everything that fell and patches everything that was hit. The count
        /// of pieces that needed it.</summary>
        public int RepairAll()
        {
            int repaired = 0;

            for (int i = 0; i < structures.Count; i++)
            {
                ICompoundStructure structure = structures[i];
                if (structure == null || (structure is UnityEngine.Object o && o == null)) continue;
                if (structure.Health >= structure.MaxHealth && !structure.IsDestroyed) continue;

                structure.Repair();
                repaired++;
            }

            if (repaired > 0)
            {
                Debug.Log($"[Last Orders] Overnight the walls were patched up: {repaired} section" +
                          $"{(repaired == 1 ? "" : "s")} repaired.");
                OnRepaired?.Invoke(repaired);
            }

            return repaired;
        }
    }
}
