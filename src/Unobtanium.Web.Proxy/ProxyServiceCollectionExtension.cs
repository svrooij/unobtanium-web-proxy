using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using Unobtanium.Web.Proxy.Events;
using Unobtanium.Web.Proxy.Services;
namespace Unobtanium.Web.Proxy;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to add proxy services.
/// </summary>
public static class ProxyServiceCollectionExtension
{
    /// <summary>
    /// Registers the <see cref="ProxyServerEvents"/> as a singleton in the service collection.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="events"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static IServiceCollection AddProxyEvents ( this IServiceCollection services, ProxyServerEvents events )
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        if (events == null)
        {
            throw new ArgumentNullException(nameof(events));
        }
        // Register the ProxyServerEvents as a singleton
        services.AddSingleton(events);
        // Return the updated service collection
        return services;
    }

    /// <summary>
    /// Adds the proxy services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddProxyServices ( this IServiceCollection services )
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        // Check existence of ProxyServerEvents and throw if not registered
        if (!services.Any(s => s.ServiceType == typeof(ProxyServerEvents) && s.Lifetime == ServiceLifetime.Singleton))
        {
            throw new InvalidOperationException(
                "ProxyServerEvents must be registered as Singleton before adding proxy services. " +
                "Use services.AddProxyEvents() to register the events.");
        }

        // Add TimeProvider.System if no other TimeProvider is registered
        services.AddSingletonIfMissing<TimeProvider, TimeProvider>(TimeProvider.System);

        // Add certificate mananger if not already registered
        services.AddSingletonIfMissing<ICertificateManager, DefaultCertificateManager>();


        // Add the proxy server configuration if not already registered
        // This is an IOptions<T> implementation how does this work?
        // Check if the configuration is already registered


        // Add the proxy server!
        services.AddHostedService<ProxyBackgroundService>();
        return services;
    }

    /// <summary>
    /// Register a service as a singleton if it is not already registered.
    /// </summary>
    /// <typeparam name="TService">Service it is implementing</typeparam>
    /// <typeparam name="TImplementation">Implementing service</typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    private static IServiceCollection AddSingletonIfMissing<TService, TImplementation> ( this IServiceCollection services )
        where TService : class
        where TImplementation : class, TService
    {
        if (!services.Any(s => s.ServiceType == typeof(TService) && s.ImplementationType == typeof(TImplementation)))
        {
            services.AddSingleton<TService, TImplementation>();
        }
        return services;
    }

    /// <summary>
    /// Register a service as a singleton if it is not already registered, with a specific implementation instance.
    /// </summary>
    /// <typeparam name="TService">Service it is implementing</typeparam>
    /// <typeparam name="TImplementation">Implementing service</typeparam>
    /// <param name="services"></param>
    /// <param name="implementation">Exact implementation</param>
    private static IServiceCollection AddSingletonIfMissing<TService, TImplementation> ( this IServiceCollection services, TImplementation implementation )
        where TService : class
        where TImplementation : class, TService
    {
        if (!services.Any(s => s.ServiceType == typeof(TService)))
        {
            services.AddSingleton<TService>(implementation);
        }
        return services;
    }
}
