namespace LastOrders.Night
{
    /// <summary>
    /// Whatever draws an enemy: the project's people bent into a shamble (<see cref="ZombieBody"/>),
    /// or a model made for one archetype (<see cref="MascotZombieBody"/>).
    ///
    /// The agent tells it the things only the agent knows - a blow landing on a wall, a hit taken,
    /// the killing blow - and the body works out its own walk from how it moves. A new kind of
    /// zombie is a new body behind this, with no change to the agent.
    /// </summary>
    public interface IEnemyBody
    {
        /// <summary>One blow on whatever is in front of it.</summary>
        void Pound();

        /// <summary>It was hit for <paramref name="amount"/>. Not called for the killing blow.</summary>
        void Hurt(float amount);

        /// <summary>
        /// The killing blow landed: the body falls down by itself. Returns the seconds until it lies
        /// still, or 0 for a body with no fall of its own - the agent then topples it whole.
        /// </summary>
        float Die();
    }
}
