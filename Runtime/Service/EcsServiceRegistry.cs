using System;
using System.Collections.Generic;
using FFS.Libraries.StaticEcs;

namespace UniGame.StaticEcs.Unity
{
    /// <summary>Locates live ECS services by their exact Static ECS world type.</summary>
    public static class EcsServiceRegistry
    {
        private static readonly Dictionary<Type, IEcsService> Services = new();
        private static readonly List<IEcsService> RegistrationOrder = new();

        /// <summary>Gets the Main service, or the most recently registered live service when Main is absent.</summary>
        public static IEcsService Active
        {
            get
            {
                if (Services.TryGetValue(typeof(Main), out var main))
                    return main;

                return RegistrationOrder.Count == 0
                    ? null
                    : RegistrationOrder[RegistrationOrder.Count - 1];
            }
        }

        /// <summary>Gets the report from the most recent successful registration.</summary>
        public static EcsStartupReport LastReport { get; private set; }

        /// <summary>Gets the service registered for the exact requested world type.</summary>
        public static IEcsService Get<TWorld>()
            where TWorld : struct, IWorldType
        {
            Services.TryGetValue(typeof(TWorld), out var service);
            return service;
        }

        /// <summary>Tries to get the service registered for the exact requested world type.</summary>
        public static bool TryGet<TWorld>(out IEcsService service)
            where TWorld : struct, IWorldType
        {
            return Services.TryGetValue(typeof(TWorld), out service);
        }

        /// <summary>Registers a service as the sole owner of its exact world type.</summary>
        public static void Register(IEcsService service)
        {
            if (service == null)
                return;

            var worldType = service.WorldType;
            if (Services.TryGetValue(worldType, out var current))
            {
                if (ReferenceEquals(current, service))
                    return;

                throw new InvalidOperationException(
                    $"Static ECS world `{worldType.FullName}` is already owned by " +
                    $"service `{current.GetType().FullName}`.");
            }

            Services.Add(worldType, service);
            RegistrationOrder.Add(service);
            LastReport = service.Report;
        }

        /// <summary>Unregisters only the exact service instance currently owning its world type.</summary>
        public static void Unregister(IEcsService service)
        {
            if (service == null ||
                !Services.TryGetValue(service.WorldType, out var current) ||
                !ReferenceEquals(current, service))
                return;

            Services.Remove(service.WorldType);
            RegistrationOrder.Remove(service);
        }
    }
}
