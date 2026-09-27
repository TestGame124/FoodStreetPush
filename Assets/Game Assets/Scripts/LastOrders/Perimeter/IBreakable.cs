using UnityEngine;

namespace LastOrders.Perimeter
{
    /// <summary>
    /// Something a zombie can smash its way through: a section of the compound wall, a gate.
    ///
    /// A zombie does not choose one. It walks the way the night sends it, and whatever stops it
    /// is what it starts hitting - the same rule the grid's walls follow, for the barriers that
    /// are not on the grid.
    /// </summary>
    public interface IBreakable
    {
        /// <summary>Still standing, and still shut. A gate standing open for a car is standing
        /// but not in the way, and a zombie at it simply walks on.</summary>
        bool IsInTheWay { get; }

        /// <summary>The nearest point of it to <paramref name="from"/>, for the zombie to face.</summary>
        Vector3 ClosestPoint(Vector3 from);

        /// <summary>One blow. True when it brought the thing down.</summary>
        bool TakeHit(float damage, Vector3 from);
    }

    /// <summary>A part of the compound's perimeter: what the dawn repair and the HUD count.</summary>
    public interface ICompoundStructure
    {
        string DisplayName { get; }

        float Health { get; }

        float MaxHealth { get; }

        /// <summary>Knocked down and not yet rebuilt.</summary>
        bool IsDestroyed { get; }

        /// <summary>Back to full health, and standing again if it had fallen.</summary>
        void Repair();
    }
}
