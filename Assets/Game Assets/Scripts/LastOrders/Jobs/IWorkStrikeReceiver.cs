namespace LastOrders.Jobs
{
    /// <summary>
    /// Something that reacts to each blow of a worker's tool.
    ///
    /// One method, and the reason the smashing reads as smashing: a hammer swinging at a pile
    /// that never moves looks like mime. The worker says when a blow lands; the thing being
    /// worked on decides what that looks like.
    /// </summary>
    public interface IWorkStrikeReceiver
    {
        void OnStrike();
    }
}
