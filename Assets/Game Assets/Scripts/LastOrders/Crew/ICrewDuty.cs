namespace LastOrders.Crew
{
    /// <summary>
    /// One thing a crew member can be busy with: serving tables, helping at a station, keeping the
    /// diner clean, being carried by the player's finger. A <see cref="CrewBrain"/> gives the body to
    /// the duty with the highest <see cref="Priority"/> that <see cref="HasWork"/>, and takes it away
    /// again the moment a more important one does; with nothing to do anywhere, the least important
    /// duty keeps the body as the fallback.
    ///
    /// Duties are components beside the brain. They walk, gesture and hold things through the brain's
    /// shared body parts rather than their own, so serving and sweeping move the same legs the same way.
    /// </summary>
    public interface ICrewDuty
    {
        /// <summary>Higher goes first.</summary>
        int Priority { get; }

        /// <summary>True while there is something to do. Asked every frame, so it must be cheap.</summary>
        bool HasWork { get; }

        /// <summary>What it is doing, for the tap and the panels. Empty while it is doing nothing.</summary>
        string Activity { get; }

        /// <summary>Given the body (true), or had it taken away (false) - in which case drop everything
        /// taken on, so nothing is left promised.</summary>
        void SetActive(bool active);

        /// <summary>One frame of the duty, while it has the body.</summary>
        void Tick(float deltaTime);
    }
}
