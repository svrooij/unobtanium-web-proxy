using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Events;
using Unobtanium.Web.Proxy.Services;

namespace Unobtanium.Web.Proxy.KestrelTests.TestHelpers;
internal class ProxyRunner : IDisposable
{
    private readonly int _port;
    private readonly int _httpsPort;
    private IServiceProvider? _serviceProvider;
    private ProxyServerEvents? _proxyServerEvents;
    private bool _isRunning;
    private X509Certificate2? _rootCertificate;


    public ProxyRunner ( int port, int httpsPort, string? cachePath = null )
    {
        _port = port;
        _httpsPort = httpsPort;
        BuildProxyServiceProvider(cachePath);
    }

    private void BuildProxyServiceProvider ( string? cachePath = null )
    {
        var services = new ServiceCollection();
        _proxyServerEvents = new ProxyServerEvents();
        _proxyServerEvents.ShouldDecryptNewConnection = async ( host, cts ) =>
        {
            // Log the new connection details
            return host.Equals("graph.microsoft.com");
        };

        services.AddProxyEvents(_proxyServerEvents);
        services.Configure<ProxyServerOptions>(options =>
        {
            options.Port = _port; // Set the port for the proxy server
            options.HttpsPort = _httpsPort;
        });
        if (!string.IsNullOrEmpty(cachePath))
        {
            services.Configure<CertificateManagerConfiguration>(options =>
            {
                options.CachePath = cachePath; // Set the cache path if provided
                options.CacheRootCertificate = true;
            });
        }

        services.AddProxyServices();
        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task StartAsync ( CancellationToken cancellationToken = default )
    {
        if (_serviceProvider == null)
            throw new InvalidOperationException("Service provider is not initialized.");
        _rootCertificate = await _serviceProvider.GetRequiredService<ICertificateManager>().GetRootCertificateAsync(cancellationToken);

        var proxyServer = _serviceProvider.GetRequiredService<IHostedService>();
        await proxyServer.StartAsync(cancellationToken);
        _isRunning = true;
    }

    public async Task StopAsync ( CancellationToken cancellationToken = default )
    {
        if (!_isRunning)
            return; // Already stopped, no need to stop again.
        if (_serviceProvider == null)
            throw new InvalidOperationException("Service provider is not initialized.");
        var proxyServer = _serviceProvider.GetRequiredService<IHostedService>();
        await proxyServer.StopAsync(cancellationToken);
    }

    public HttpClient CreateHttpClient ( bool ignoreAllCertificateErrors = false, bool acceptFakeRootAndNormalTrusted = false )
    {
        if (_serviceProvider == null)
            throw new InvalidOperationException("Service provider is not initialized.");

        // Configure the HttpClientHandler to ignore certificate errors
        var handler = new HttpClientHandler
        {
            UseProxy = true,
            Proxy = new WebProxy($"http://localhost:{_port}")
        };
        if (acceptFakeRootAndNormalTrusted)
        {
            handler.ServerCertificateCustomValidationCallback = ( message, cert, chain, errors ) =>
            {
                // Accept the certificate if it's the root certificate or a normal trusted certificate
                return errors == System.Net.Security.SslPolicyErrors.None || chain!.ChainElements.Any(c => c.Certificate.Thumbprint == _rootCertificate!.Thumbprint);
            };
        }
        else
        if (ignoreAllCertificateErrors)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
        return new HttpClient(handler);

    }

    public ProxyServerEvents ProxyServerEvents => _proxyServerEvents ?? throw new InvalidOperationException("Proxy server events are not initialized.");
    public void Dispose ()
    {
        if (_serviceProvider != null)
        {
            if (_isRunning)
            {
                using var cts = new CancellationTokenSource(5_000);
                var proxyServer = _serviceProvider.GetRequiredService<IHostedService>();
                proxyServer.StopAsync(cts.Token).GetAwaiter().GetResult();
            }
            _serviceProvider = null;
        }
    }
}
