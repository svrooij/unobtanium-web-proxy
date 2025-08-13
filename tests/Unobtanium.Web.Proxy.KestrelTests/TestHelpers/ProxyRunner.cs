using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Events;

namespace Unobtanium.Web.Proxy.KestrelTests.TestHelpers;
internal class ProxyRunner : IDisposable
{
    private readonly int _port;
    private readonly int _httpsPort;
    private IServiceProvider? _serviceProvider;
    private ProxyServerEvents? _proxyServerEvents;
    private bool _isRunning;


    public ProxyRunner(int port, int httpsPort)
    {
        _port = port;
        _httpsPort = httpsPort;
        BuildProxyServiceProvider();
    }

    private void BuildProxyServiceProvider()
    {
        var services = new ServiceCollection();
        _proxyServerEvents = new ProxyServerEvents();
        _proxyServerEvents.ShouldDecryptNewConnection = async (host, cts) =>
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
        services.AddProxyServices();
        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task StartAsync ( CancellationToken cancellationToken = default )
    {
        if (_serviceProvider == null)
            throw new InvalidOperationException("Service provider is not initialized.");
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

    public HttpClient CreateHttpClient(bool ignoreCertificateErrors = false)
    {
        if (_serviceProvider == null)
           throw new InvalidOperationException("Service provider is not initialized.");

        // Configure the HttpClientHandler to ignore certificate errors
        var handler = new HttpClientHandler
        { 
            UseProxy = true,
            Proxy = new WebProxy($"http://localhost:{_port}")
        };
        if (ignoreCertificateErrors)
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
