using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Net;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Events;
using Unobtanium.Web.Proxy.Services;
using Unobtanium.Web.Proxy.Services.Proxy;

namespace Unobtanium.Web.Proxy;

internal class ProxyBackgroundService : BackgroundService
{
    private readonly ILogger<ProxyBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ProxyServerOptions _options;
    private readonly ICertificateManager _certificateManager;
    private readonly ProxyServerEvents _events;
    private readonly IProxyHttpClientFactory _proxyHttpClientFactory;
    private IHost? _proxyHost;

    public ProxyBackgroundService (

        IServiceProvider serviceProvider,
        IOptions<ProxyServerOptions> options,
        ProxyServerEvents events,
        ICertificateManager certificateManager,
        ILogger<ProxyBackgroundService>? logger = null,
        IProxyHttpClientFactory? proxyHttpClientFactory = null,
        TimeProvider? timeProvider = null
        )
    {
        _logger = logger ?? new NullLogger<ProxyBackgroundService>();
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _certificateManager = certificateManager;
        _events = events;
        _proxyHttpClientFactory = proxyHttpClientFactory ?? new ProxyHttpClientFactory();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async Task StartAsync ( CancellationToken cancellationToken )
    {
        _logger.LogDebug($"{nameof(ProxyBackgroundService)}.{nameof(StartAsync)} called");
        // Ensure the certificate manager is initialized
        var rootCert = await _certificateManager.GetRootCertificateAsync(cancellationToken);
        if (_options.PreloadCertificates != null)
        {
            foreach (var cert in _options.PreloadCertificates)
            {
                try
                {
                    var certificate = await _certificateManager.GetCertificateAsync(cert, cancellationToken);
                    _logger.LogInformation("Preloaded certificate: {Certificate}", certificate.Subject);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to preload certificate: {Certificate}", cert);
                }
            }
        }
        await base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async Task StopAsync ( CancellationToken cancellationToken )
    {
        _logger.LogDebug($"{nameof(ProxyBackgroundService)}.{nameof(StopAsync)} called");

        if (_proxyHost != null)
        {
            await _proxyHost.StopAsync(cancellationToken);
            _proxyHost.Dispose();
            _proxyHost = null;
        }

        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync ( CancellationToken stoppingToken )
    {
        _logger.LogDebug($"{nameof(ProxyBackgroundService)}.{nameof(ExecuteAsync)} called");
        try
        {
            _logger.LogInformation(
                "Proxy service starting on Port: {Port}, HTTPS Port: {HttpsPort}",
                _options.Port, _options.HttpsPort);

            // Create a new WebApplicationBuilder for our proxy server
            var proxyBuilder = WebApplication.CreateBuilder();

            // Configure services by copying from the main application
            ConfigureProxyServices(proxyBuilder);

            // Configure Kestrel
            proxyBuilder.WebHost.ConfigureKestrel(serverOptions =>
            {
                // Configure HTTPS endpoint for intercepting
                serverOptions.Listen(IPAddress.Loopback, _options.HttpsPort, listenOptions =>
                {
                    listenOptions.UseHttps(httpsOptions =>
                    {
                        httpsOptions.ServerCertificateSelector = ( context, dnsName ) =>
                        {
                            var cert = _certificateManager
                                .GetCertificateAsync(dnsName!, context?.ConnectionClosed ?? CancellationToken.None)
                                .GetAwaiter()
                                .GetResult();
                            var rootCert = _certificateManager.GetRootCertificateAsync(context?.ConnectionClosed ?? default).GetAwaiter().GetResult();
                            var combined = CertificateCombiner.CreateChainedCertificate(cert, rootCert);
                            return combined;
                        };
                    });
                });

                // HTTP endpoint
                serverOptions.Listen(IPAddress.Loopback, _options.Port);
            });

            // Create and start the proxy web app
            var proxyApp = proxyBuilder.Build();

            // Configure the request pipeline
            ConfigureProxyApp(proxyApp);

            // Start the proxy host
            _proxyHost = proxyApp;
            await _proxyHost.StartAsync(stoppingToken);

            // Log the addresses the proxy is listening on
            var addresses = _proxyHost.Services.GetService<IServer>()?.Features?.Get<IServerAddressesFeature>()?.Addresses;
            if (addresses != null)
            {
                foreach (var address in addresses)
                {
                    _logger.LogInformation("Proxy listening on: {Address}", address);
                }
            }

            // Wait until the hosting application is stopping
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown, don't log error
            _logger.LogInformation("Proxy service shutting down");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running proxy background service");
        }
    }

    private void ConfigureProxyServices ( WebApplicationBuilder proxyBuilder )
    {
        // Add the certificate manager from the main application
        proxyBuilder.Services.AddSingleton(_certificateManager);

        // Add the proxy events from the main application
        proxyBuilder.Services.AddSingleton(_events);

        // Add the proxy HTTP client factory
        proxyBuilder.Services.AddSingleton(_proxyHttpClientFactory);

        // Add other services needed for the proxy
        proxyBuilder.Services.AddSingleton(TimeProvider.System);

        proxyBuilder.Services.Configure<ProxyServerOptions>(options =>
        {
            options.Port = _options.Port;
            options.HttpsPort = _options.HttpsPort;
            //options.PreloadCertificates = _options.PreloadCertificates;
        });

        // Transfer OpenTelemetry services from the main application
        TransferOpenTelemetryServices(proxyBuilder);

        // Configure logging to use the same logger as the main app
        proxyBuilder.Logging.ClearProviders();
        proxyBuilder.Logging.AddProvider(new ForwardingLoggerProvider(_serviceProvider));
    }

    /// <summary>
    /// Transfers OpenTelemetry services and configuration from the main application 
    /// to the proxy host to ensure distributed tracing and metrics work correctly.
    /// </summary>
    /// <param name="proxyBuilder">The WebApplicationBuilder for the proxy host</param>
    private void TransferOpenTelemetryServices ( WebApplicationBuilder proxyBuilder )
    {
        try
        {
            // Use a more comprehensive approach to transfer OpenTelemetry and related services
            var servicesToTransfer = new[]
            {
                // OpenTelemetry core services
                "OpenTelemetry.Trace.TracerProvider",
                "OpenTelemetry.Metrics.MeterProvider",
                "OpenTelemetry.OpenTelemetryLoggerProvider",
                "OpenTelemetry.Logs.OpenTelemetryLoggerProvider",
                
                // .NET Activity and diagnostic services
                "System.Diagnostics.DiagnosticSource",
                "System.Diagnostics.ActivitySource"
            };

            foreach (var serviceTypeName in servicesToTransfer)
            {
                var serviceType = Type.GetType($"{serviceTypeName}, OpenTelemetry")
                                 ?? Type.GetType($"{serviceTypeName}, System.Diagnostics.DiagnosticSource");

                if (serviceType != null)
                {
                    var service = _serviceProvider.GetService(serviceType);
                    if (service != null)
                    {
                        proxyBuilder.Services.AddSingleton(serviceType, service);
                        _logger.LogDebug("Transferred {ServiceType} to proxy host", serviceType.Name);
                    }
                }
            }

            // Transfer OpenTelemetry-related ILoggerProvider services
            var loggerProviders = _serviceProvider.GetServices<ILoggerProvider>();
            foreach (var loggerProvider in loggerProviders)
            {
                var typeName = loggerProvider.GetType().FullName ?? string.Empty;
                if (typeName.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase))
                {
                    proxyBuilder.Services.AddSingleton<ILoggerProvider>(loggerProvider);
                    _logger.LogDebug("Transferred OpenTelemetry LoggerProvider {TypeName} to proxy host", typeName);
                }
            }

            // Register the proxy ActivitySource so it can be used for tracing
            proxyBuilder.Services.AddSingleton(ProxyServerDefaults.ProxyActivitySource);
            _logger.LogDebug("Registered ProxyActivitySource in proxy host");

            // Transfer any additional OpenTelemetry instrumentation services
            TransferInstrumentationServices(proxyBuilder);

            // Try to transfer configuration options that might be OpenTelemetry related
            TransferOpenTelemetryOptions(proxyBuilder);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to transfer some OpenTelemetry services to proxy host");
        }
    }

    /// <summary>
    /// Transfers OpenTelemetry instrumentation services that may be registered in the main application.
    /// </summary>
    /// <param name="proxyBuilder">The WebApplicationBuilder for the proxy host</param>
    private void TransferInstrumentationServices ( WebApplicationBuilder proxyBuilder )
    {
        try
        {
            // Look for common OpenTelemetry instrumentation services
            var instrumentationTypes = new[]
            {
                "OpenTelemetry.Instrumentation.Http.HttpClientInstrumentation",
                "OpenTelemetry.Instrumentation.AspNetCore.AspNetCoreInstrumentation",
                "OpenTelemetry.Instrumentation.Runtime.RuntimeInstrumentation"
            };

            foreach (var instrumentationTypeName in instrumentationTypes)
            {
                var instrumentationType = Type.GetType($"{instrumentationTypeName}, OpenTelemetry.Instrumentation.Http")
                                         ?? Type.GetType($"{instrumentationTypeName}, OpenTelemetry.Instrumentation.AspNetCore")
                                         ?? Type.GetType($"{instrumentationTypeName}, OpenTelemetry.Instrumentation.Runtime");

                if (instrumentationType != null)
                {
                    var service = _serviceProvider.GetService(instrumentationType);
                    if (service != null)
                    {
                        proxyBuilder.Services.AddSingleton(instrumentationType, service);
                        _logger.LogDebug("Transferred instrumentation service {ServiceType} to proxy host", instrumentationType.Name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to transfer some OpenTelemetry instrumentation services to proxy host");
        }
    }

    /// <summary>
    /// Transfers OpenTelemetry configuration options from the main application to the proxy host.
    /// </summary>
    /// <param name="proxyBuilder">The WebApplicationBuilder for the proxy host</param>
    private void TransferOpenTelemetryOptions ( WebApplicationBuilder proxyBuilder )
    {
        try
        {
            // Get all configured options and transfer those that might be OpenTelemetry related
            var optionsTypes = new[]
            {
                "OpenTelemetry.Trace.TracerProviderBuilderOptions",
                "OpenTelemetry.Metrics.MeterProviderOptions",
                "OpenTelemetry.Logs.OpenTelemetryLoggerOptions"
            };

            foreach (var optionsTypeName in optionsTypes)
            {
                var optionsType = Type.GetType($"{optionsTypeName}, OpenTelemetry");
                if (optionsType != null)
                {
                    var optionsServiceType = typeof(IOptions<>).MakeGenericType(optionsType);
                    var options = _serviceProvider.GetService(optionsServiceType);
                    if (options != null)
                    {
                        proxyBuilder.Services.AddSingleton(optionsServiceType, options);
                        _logger.LogDebug("Transferred {OptionsType} to proxy host", optionsType.Name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to transfer OpenTelemetry options to proxy host");
        }
    }

    private void ConfigureProxyApp ( WebApplication proxyApp )
    {
        // Configure middleware and endpoints
        proxyApp.MapProxyEndpoints();
    }

    // Forwarding logger provider to use the main app's loggers
    private class ForwardingLoggerProvider : ILoggerProvider
    {
        private readonly IServiceProvider _serviceProvider;

        public ForwardingLoggerProvider ( IServiceProvider serviceProvider )
        {
            _serviceProvider = serviceProvider;
        }

        public ILogger CreateLogger ( string categoryName )
        {
            // Get ILoggerFactory from the main service provider
            var loggerFactory = _serviceProvider.GetService<ILoggerFactory>() ?? new NullLoggerFactory();
            return loggerFactory.CreateLogger(categoryName);
        }

        public void Dispose ()
        {
        }
    }
}
