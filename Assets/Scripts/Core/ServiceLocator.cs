using System;
using System.Collections.Generic;

namespace AlipiriAR.Core
{
    /// <summary>Process-wide service registry. Populated once by AppBootstrap at startup.</summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new();

        public static void Register<T>(T service) where T : class
        {
            Services[typeof(T)] = service ?? throw new ArgumentNullException(nameof(service));
        }

        public static T Get<T>() where T : class
        {
            if (Services.TryGetValue(typeof(T), out var service))
                return (T)service;
            throw new InvalidOperationException($"Service {typeof(T).Name} is not registered. AppBootstrap must register it before first use.");
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out var raw))
            {
                service = (T)raw;
                return true;
            }
            service = null;
            return false;
        }

        public static bool IsRegistered<T>() where T : class => Services.ContainsKey(typeof(T));

        /// <summary>Editor/domain-reload safety — AppBootstrap calls this before re-registering.</summary>
        public static void Clear() => Services.Clear();
    }
}
