using System;
using System.Collections.Generic;

namespace LastOrders.Core
{
    /// <summary>
    /// The composition root's lookup table. Services register here during boot and
    /// find each other through it, so no service needs a singleton of its own and
    /// none of them reach into the scene to find a collaborator.
    ///
    /// This is deliberately the smallest thing that does the job. The GDD calls for
    /// VContainer; that package is not in the project, and the shape here — register
    /// a concrete instance against an interface, resolve by type — is the subset of
    /// VContainer this game actually uses. Swapping it out later is a mechanical
    /// change at <see cref="GameBootstrap"/> and nowhere else, because nothing but
    /// the bootstrap ever calls <see cref="Register"/>.
    /// </summary>
    public static class ServiceRegistry
    {
        static readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

        /// <summary>Raised after <see cref="Clear"/>, so long-lived listeners can drop
        /// cached service references instead of holding a dead object across a scene load.</summary>
        public static event Action OnCleared;

        public static void Register<T>(T service) where T : class
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            services[typeof(T)] = service;
        }

        /// <summary>
        /// Throws when the service is missing. That is the intended behaviour: a missing
        /// service is always a boot-order bug, and it is far cheaper to find at the point
        /// of resolution than three systems later as a NullReferenceException.
        /// </summary>
        public static T Resolve<T>() where T : class
        {
            if (services.TryGetValue(typeof(T), out object service))
                return (T)service;

            throw new InvalidOperationException(
                $"[ServiceRegistry] {typeof(T).Name} was never registered. " +
                "Check the registration order in GameBootstrap.");
        }

        public static bool TryResolve<T>(out T service) where T : class
        {
            if (services.TryGetValue(typeof(T), out object found))
            {
                service = (T)found;
                return true;
            }

            service = null;
            return false;
        }

        public static bool IsRegistered<T>() where T : class => services.ContainsKey(typeof(T));

        /// <summary>
        /// Drops every registration. Called when tearing the game down so that a second
        /// boot cannot resolve a service belonging to the previous session.
        /// </summary>
        public static void Clear()
        {
            services.Clear();
            OnCleared?.Invoke();
        }
    }
}
