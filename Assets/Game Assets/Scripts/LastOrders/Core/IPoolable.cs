namespace LastOrders.Core
{
    /// <summary>
    /// The reset contract for anything that goes through <see cref="PrefabPool"/>.
    ///
    /// Read <see cref="OnReturnedToPool"/> as the important half. The instinct is to
    /// write the spawn path first and clear state in <see cref="OnTakenFromPool"/>,
    /// but state cleared on take is state that was still live while the object sat in
    /// the pool: a coroutine still running, a subscription still firing, a trail
    /// renderer still drawing across the map to wherever the object was parked. Those
    /// are the bugs that only show up at a hundred and twenty agents, and they are
    /// invisible in the inspector because the object is inactive.
    ///
    /// So: <see cref="OnReturnedToPool"/> is where an object becomes indistinguishable
    /// from a freshly instantiated one. <see cref="OnTakenFromPool"/> only applies the
    /// new identity. <see cref="PoolIntegrity"/> checks the former in development builds.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>Called after the object is activated, before the caller configures it.</summary>
        void OnTakenFromPool();

        /// <summary>
        /// Called before the object is deactivated. Must leave the object in the state a
        /// brand new instance of the prefab would be in: no running coroutines, no event
        /// subscriptions, no residual velocity, timers at zero, visuals reset, and every
        /// reference to another pooled object dropped.
        /// </summary>
        void OnReturnedToPool();
    }
}
