using UnityEngine;

namespace LastOrders.Field
{
    /// <summary>
    /// A zombie standing on land the restaurant wants back. It has to be dealt with before the
    /// debris around it can be - nobody clears rubble with something shambling up behind them.
    ///
    /// The field service keeps them by area and hands them to the worker as targets; whatever
    /// draws one in the scene answers these.
    /// </summary>
    public interface IAreaZombie
    {
        /// <summary>Stable identity. Recorded when it is killed, so it stays dead.</summary>
        string ZombieId { get; }

        /// <summary>The expansion area it lurks on.</summary>
        string AreaId { get; }

        Vector3 Position { get; }

        bool IsAlive { get; }

        float Health { get; }

        float MaxHealth { get; }

        void ApplyDamage(float amount);
    }
}
