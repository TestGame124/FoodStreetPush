using UnityEngine;

namespace LastOrders.Repair
{
    /// <summary>
    /// Something the night can break and a worker can put right: a section of the compound wall,
    /// a gate, a table or a door on the grid.
    ///
    /// Knocked down or only hit, the answer is the same - somebody has to walk over and fix it.
    /// Nothing mends by itself.
    /// </summary>
    public interface IRepairable
    {
        /// <summary>Stable for the session: the job queue knows the work by it, so tapping the
        /// same wall twice is one job and not two.</summary>
        string RepairId { get; }

        string DisplayName { get; }

        float Health { get; }

        float MaxHealth { get; }

        /// <summary>Knocked down, as opposed to only hit.</summary>
        bool IsDestroyed { get; }

        /// <summary>In the world and reachable right now. A wall tied to land that has not been
        /// claimed yet is not, whatever its health says.</summary>
        bool IsRepairableNow { get; }

        /// <summary>What the worker faces while they work, and what their blows land on.</summary>
        Transform RepairTarget { get; }

        /// <summary>Where the repair button and the timer float.</summary>
        Transform RepairAnchor { get; }

        /// <summary>Where the worker stands to do it.</summary>
        Vector3 RepairPoint { get; }

        /// <summary>Back to full health, and standing again if it had fallen.</summary>
        void Repair();
    }
}
