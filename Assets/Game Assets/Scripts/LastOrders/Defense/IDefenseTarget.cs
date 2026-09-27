using UnityEngine;

namespace LastOrders.Defense
{
    /// <summary>
    /// Anything a defense can shoot at.
    ///
    /// The night's enemies today. Whatever else turns up on the road later only has to answer
    /// these to be shot at by every tower and turret already standing, with no change to any
    /// of them.
    /// </summary>
    public interface IDefenseTarget
    {
        Vector3 Position { get; }

        /// <summary>False once it is dying. A defense never spends a shot on it after that.</summary>
        bool IsAlive { get; }

        /// <summary>What the guard calls it, for the spotting call-out.</summary>
        string DisplayName { get; }

        void ApplyDamage(float amount);
    }
}
