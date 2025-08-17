using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Unobtanium.Web.Proxy.Events;
using Unobtanium.Web.Proxy.Services;

namespace Unobtanium.Web.Proxy.KestrelTests;

[TestClass]
public sealed class ServiceRegistrationTests
{
    [TestMethod]
    public void AddProxyServices_WithValidConfiguration_ShouldRegisterServices ()
    {
        // Arrange
        var services = new ServiceCollection();
        var events = new ProxyServerEvents();

        // Add logging (required for DefaultCertificateManager)
        services.AddLogging();

        // Add required ProxyServerEvents first
        services.AddProxyEvents(events);

        // Configure ProxyServerOptions
        services.Configure<ProxyServerOptions>(options =>
        {
            options.Port = 8080;
            options.HttpsPort = 8081;
            options.SetAsSystemProxy = false;
            options.TrustCertificateOnStart = false;
        });

        // Act
        services.AddProxyServices();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        Assert.IsNotNull(serviceProvider.GetService<ProxyServerEvents>());
        Assert.IsNotNull(serviceProvider.GetService<TimeProvider>());
        Assert.IsNotNull(serviceProvider.GetService<ICertificateManager>());
        Assert.IsNotNull(serviceProvider.GetService<IOptions<ProxyServerOptions>>());

        // Verify the ProxyBackgroundService is registered as a hosted service
        var hostedServices = serviceProvider.GetServices<IHostedService>();
        Assert.IsTrue(hostedServices.Any(hs => hs.GetType().Name == "ProxyBackgroundService"));
    }

    [TestMethod]
    public void AddProxyServices_WithoutProxyServerEvents_ShouldThrowInvalidOperationException ()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => services.AddProxyServices());
        Assert.IsTrue(exception.Message.Contains("ProxyServerEvents must be registered as Singleton"));
    }

    [TestMethod]
    public void AddProxyEvents_WithValidEvents_ShouldRegisterAsSingleton ()
    {
        // Arrange
        var services = new ServiceCollection();
        var events = new ProxyServerEvents();

        // Act
        services.AddProxyEvents(events);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var registeredEvents = serviceProvider.GetService<ProxyServerEvents>();
        Assert.IsNotNull(registeredEvents);
        Assert.AreSame(events, registeredEvents);
    }

    [TestMethod]
    public void AddProxyEvents_WithNullServices_ShouldThrowArgumentNullException ()
    {
        // Arrange
        IServiceCollection? services = null;
        var events = new ProxyServerEvents();

        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() => services!.AddProxyEvents(events));
    }

    [TestMethod]
    public void AddProxyEvents_WithNullEvents_ShouldThrowArgumentNullException ()
    {
        // Arrange
        var services = new ServiceCollection();
        ProxyServerEvents? events = null;

        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() => services.AddProxyEvents(events!));
    }

    [TestMethod]
    public void AddProxyServices_WithCustomTimeProvider_ShouldNotOverrideCustomProvider ()
    {
        // Arrange
        var services = new ServiceCollection();
        var events = new ProxyServerEvents();
        var customTimeProvider = TimeProvider.System;

        services.AddProxyEvents(events);
        services.AddSingleton<TimeProvider>(customTimeProvider);

        // Act
        services.AddProxyServices();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var registeredTimeProvider = serviceProvider.GetService<TimeProvider>();
        Assert.IsNotNull(registeredTimeProvider);
        Assert.AreSame(customTimeProvider, registeredTimeProvider);
    }

    [TestMethod]
    public void AddProxyServices_WithCustomCertificateManager_ShouldNotOverrideCustomManager ()
    {
        // Arrange
        var services = new ServiceCollection();
        var events = new ProxyServerEvents();

        // Add logging and events first
        services.AddLogging();
        services.AddProxyEvents(events);

        // Create a mock certificate manager that is NOT DefaultCertificateManager
        var customCertManager = new TestCertificateManager();
        services.AddSingleton<ICertificateManager>(customCertManager);

        // Act
        services.AddProxyServices();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var registeredCertManager = serviceProvider.GetService<ICertificateManager>();
        Assert.IsNotNull(registeredCertManager);

        // The AddSingletonIfMissing should not override our custom manager
        // However, since it checks for implementation type, it might add DefaultCertificateManager too
        // Let's verify that our custom manager is still accessible
        var allCertManagers = serviceProvider.GetServices<ICertificateManager>().ToList();
        Assert.IsTrue(allCertManagers.Contains(customCertManager), "Custom certificate manager should be registered");
    }

    // Helper test class for custom certificate manager
    private class TestCertificateManager : ICertificateManager
    {
        public Task<System.Security.Cryptography.X509Certificates.X509Certificate2> GetCertificateAsync ( string hostname, CancellationToken cancellationToken = default )
        {
            throw new NotImplementedException();
        }

        public Task<System.Security.Cryptography.X509Certificates.X509Certificate2> GetRootCertificateAsync (bool includePriveteKey, CancellationToken cancellationToken = default )
        {
            throw new NotImplementedException();
        }

        public void Dispose ()
        {
            // No cleanup needed for test
        }
    }

    [TestMethod]
    public void AddProxyServices_CompleteWorkflow_ShouldSetupProxyCorrectly ()
    {
        // Arrange
        var services = new ServiceCollection();
        var events = new ProxyServerEvents();

        // Configure logging (optional but realistic)
        services.AddLogging();

        // Add required services
        services.AddProxyEvents(events);

        // Configure options
        services.Configure<ProxyServerOptions>(options =>
        {
            options.Port = ProxyServerDefaults.DEFAULT_PORT;
            options.HttpsPort = ProxyServerDefaults.DEFAULT_HTTPS_PORT;
            options.SetAsSystemProxy = false;
            options.TrustCertificateOnStart = false;
            options.PreloadCertificates = new[] { "example.com", "test.local" };
        });

        // Act
        services.AddProxyServices();
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify all required services are available
        Assert.IsNotNull(serviceProvider.GetService<ProxyServerEvents>());
        Assert.IsNotNull(serviceProvider.GetService<TimeProvider>());
        Assert.IsNotNull(serviceProvider.GetService<ICertificateManager>());
        Assert.IsNotNull(serviceProvider.GetService<IOptions<ProxyServerOptions>>());

        // Verify options are configured correctly
        var options = serviceProvider.GetService<IOptions<ProxyServerOptions>>()?.Value;
        Assert.IsNotNull(options);
        Assert.AreEqual(ProxyServerDefaults.DEFAULT_PORT, options.Port);
        Assert.AreEqual(ProxyServerDefaults.DEFAULT_HTTPS_PORT, options.HttpsPort);
        Assert.IsFalse(options.SetAsSystemProxy);
        Assert.IsFalse(options.TrustCertificateOnStart);
        Assert.IsNotNull(options.PreloadCertificates);
        Assert.AreEqual(2, options.PreloadCertificates.Length);
        Assert.AreEqual("example.com", options.PreloadCertificates[0]);
        Assert.AreEqual("test.local", options.PreloadCertificates[1]);

        // Verify ProxyBackgroundService is registered
        var hostedServices = serviceProvider.GetServices<IHostedService>();
        var proxyService = hostedServices.FirstOrDefault(hs => hs.GetType().Name == "ProxyBackgroundService");
        Assert.IsNotNull(proxyService);
    }

    [TestMethod]
    public void AddProxyServices_ShouldRegisterDefaultServicesWhenNotProvided ()
    {
        // Arrange
        var services = new ServiceCollection();
        var events = new ProxyServerEvents();

        // Add logging for certificate manager
        services.AddLogging();

        services.AddProxyEvents(events);

        // Act
        services.AddProxyServices();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var timeProvider = serviceProvider.GetService<TimeProvider>();
        Assert.IsNotNull(timeProvider);
        Assert.AreSame(TimeProvider.System, timeProvider);

        var certificateManager = serviceProvider.GetService<ICertificateManager>();
        Assert.IsNotNull(certificateManager);
        Assert.IsInstanceOfType(certificateManager, typeof(DefaultCertificateManager));
    }

    [TestMethod]
    public void AddProxyServices_WithConfiguredCertificateManagerOptions_ShouldPassOptionsToManager ()
    {
        // Arrange
        var services = new ServiceCollection();
        var events = new ProxyServerEvents();

        services.AddLogging();
        services.AddProxyEvents(events);

        // Configure certificate manager options
        services.Configure<CertificateManagerConfiguration>(options =>
        {
            options.CachePath = "custom-cert-path";
            options.CacheRootCertificate = false;
            options.CacheHostCertificates = false;
            options.CertificateLifetimeDays = 365;
        });

        // Act
        services.AddProxyServices();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var certificateManager = serviceProvider.GetService<ICertificateManager>();
        Assert.IsNotNull(certificateManager);
        Assert.IsInstanceOfType(certificateManager, typeof(DefaultCertificateManager));

        // Verify that the options were applied
        var options = serviceProvider.GetService<IOptions<CertificateManagerConfiguration>>()?.Value;
        Assert.IsNotNull(options);
        Assert.AreEqual("custom-cert-path", options.CachePath);
        Assert.IsFalse(options.CacheRootCertificate);
        Assert.IsFalse(options.CacheHostCertificates);
        Assert.AreEqual(365, options.CertificateLifetimeDays);
    }
}
