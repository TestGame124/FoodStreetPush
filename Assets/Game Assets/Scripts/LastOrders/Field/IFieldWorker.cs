using LastOrders.Jobs;
using UnityEngine;

namespace LastOrders.Field
{
    /// <summary>
    /// One of the restaurant's people with a body in the scene, as the field service sees them:
    /// who they are, how hurt they are, where they stand, and what they are good for.
    ///
    /// Small on purpose. The service decides who goes where and what happens when somebody is
    /// knocked down; the worker in the scene does the walking, the fighting and the carrying.
    /// Keeping the service on this side of an interface is what lets its rules be tested with a
    /// stand-in instead of a character.
    /// </summary>
    public interface IFieldWorker
    {
        /// <summary>Stable for the session. Also the owner ID the job queue knows them by.</summary>
        string WorkerId { get; }

        string DisplayName { get; }

        /// <summary>"Helper". What they do when they are not out clearing.</summary>
        string RoleName { get; }

        WorkerRole Role { get; }

        WorkerHealth Health { get; }

        Vector3 Position { get; }

        /// <summary>Can be sent out past the walls. The Chef never leaves the kitchen.</summary>
        bool CanClearAreas { get; }

        /// <summary>Restaurant staff: they carry a downed worker to the hospital tent.</summary>
        bool CanCarry { get; }

        /// <summary>Indoors for the night, or on the way in or out. Nobody is sent anywhere then.</summary>
        bool IsSheltering { get; }

        /// <summary>What they are doing when the field service has nothing to say about it:
        /// "Idle", "Clear table". For the info panel.</summary>
        string Activity { get; }
    }
}
