namespace LastOrders.Livestock
{
    /// <summary>
    /// An animal's body as its herd sees it. The herd (<see cref="AnimalHerd"/>) only walks it
    /// about its pen, sends it to its work and puts it to bed; what it does when it stops - a hen
    /// pecking and scratching, a cow grazing - is the body's own business, as is how it walks: it
    /// measures how far it has been moved and steps to match.
    /// </summary>
    public interface IAnimalBody
    {
        /// <summary>In the middle of something, or still getting up: the herd leaves it standing until it is done.</summary>
        bool Busy { get; }

        /// <summary>Does something where it stands, as an animal does between strolls. Returns about how long it takes.</summary>
        float Idle();

        /// <summary>Makes itself heard: a hen that has just laid, a cow just milked, either of them wanting feeding.</summary>
        void Call();

        /// <summary>Settles down for the night, or gets up again.</summary>
        void Sleep(bool asleep);

        /// <summary>Settles to the pen's work where the herd has put it - a hen brooding in the nest box, the cow eating at the rack - or stops.</summary>
        void Work(bool working);

        /// <summary>How far on the next egg or churn is, 0..1 (1 full): the cow's udder fills with it.</summary>
        void Filling(float amount);
    }
}
