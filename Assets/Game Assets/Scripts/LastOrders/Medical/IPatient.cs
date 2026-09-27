namespace LastOrders.Medical
{
    /// <summary>Who is in the cot. Decides how long they stay and who looks after their body
    /// while they are there.</summary>
    public enum PatientKind
    {
        /// <summary>A customer caught inside when the night went badly. Walks, lies down, and
        /// leaves on the shuttle - their own body does all of it.</summary>
        Visitor = 0,

        /// <summary>One of the restaurant's own people. Has no body in the scene, so the tent
        /// draws them in the cot, and they go back to work when they are well.</summary>
        Survivor = 1,

        /// <summary>A worker knocked down out in the field. Has a body: the staff carry it in
        /// and lay it in the cot, which is when treatment starts, and it gets up and walks
        /// back to work when they are well.</summary>
        Worker = 2,
    }

    /// <summary>
    /// Anyone the hospital tent can look after.
    ///
    /// Deliberately small. The tent needs to know who somebody is and how long they stay, and
    /// nothing else: whether they walk in or are drawn in, and where they go afterwards, belong
    /// to whoever owns them - the visitor system for a customer, the roster for a guard.
    /// </summary>
    public interface IPatient
    {
        /// <summary>Stable for the patient's whole stay. Two patients never share one.</summary>
        string PatientId { get; }

        string DisplayName { get; }

        PatientKind Kind { get; }
    }
}
