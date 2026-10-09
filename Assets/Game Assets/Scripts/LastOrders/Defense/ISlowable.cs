namespace LastOrders.Defense
{
    /// <summary>
    /// Something a slowing device can hold back: wire it wades through, a trap that pins it.
    ///
    /// Asked for on whatever a ground defense catches, alongside <see cref="IDefenseTarget"/>, so
    /// the defenses know nothing about what an enemy is - only that it can be made slower.
    /// </summary>
    public interface ISlowable
    {
        /// <summary>
        /// Moves at <paramref name="multiplier"/> of its own speed for the next
        /// <paramref name="seconds"/>. Asked again every frame something is caught: the strongest
        /// hold wins, and it lasts until the longest has run out.
        /// </summary>
        void Slow(float multiplier, float seconds);
    }
}
