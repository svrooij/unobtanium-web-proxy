using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Unobtanium.Proxy.BaseImplementations;
using Unobtanium.Proxy.Interfaces;

namespace Unobtanium.Proxy;
public partial class ProxyServer : IDisposable
{
    
    internal static readonly ActivitySource ProxyActivitySource = new("Unobtanium.Proxy");

    private readonly ProxyServerConfiguration _configuration;
    private readonly ICertificateManager _certificateManager;
    private readonly ILogger<ProxyServer> _logger;
    private readonly IProxyHttpClientFactory _httpClientFactory;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<Task> _listenerTasks = new();

    private bool _isListening;

    public ProxyServer(
        ProxyServerConfiguration configuration,
        ICertificateManager? certificateManager,
        ILogger<ProxyServer>? logger,
        IProxyHttpClientFactory? httpClientFactory)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _certificateManager = certificateManager ?? new DefaultCertificateManager(null, null, null);
        _logger = logger ?? new NullLogger<ProxyServer>();
        _httpClientFactory = httpClientFactory ?? new ProxyHttpClientFactory();

        if (_configuration.Endpoints.Count == 0)
        {
            _configuration.Endpoints.Add(new ProxyEndpoint
            {
                Port = _configuration.DefaultPort,
                Address = IPAddress.Any,
            });
        }

        OptimizeThreadPoolForProxy();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isListening)
        {
            _logger.LogWarning("Proxy server is already running. Ignoring start request.");
            return;
        }
        _isListening = true;
        _logger.LogInformation("Starting Proxy Server with configuration: {@Configuration}", _configuration);
        // Ensure it has the root certificate ready before starting listeners
        await _certificateManager.GetRootCertificateAsync(cancellationToken).ConfigureAwait(false);
        
        // Start listening on all configured endpoints
        foreach (var endpoint in _configuration.Endpoints)
        {
            StartListeningForRequests(endpoint);
        }
    }

    private void StartListeningForRequests ( ProxyEndpoint endpoint )
    {
        endpoint!.Listener = new System.Net.Sockets.TcpListener(endpoint.Address, endpoint.Port);

        // Validate if this is available on all platforms
        endpoint.Listener.Server.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket, System.Net.Sockets.SocketOptionName.ReuseAddress, true);

        try
        {
            endpoint.Listener.Start();
            // Write the port back to the endpoint
            endpoint._port = ((IPEndPoint)endpoint.Listener.LocalEndpoint).Port;

            var listenerTask = AcceptConnectionsAsync(endpoint, _cancellationTokenSource!.Token);
            _listenerTasks.Add(listenerTask);

            _logger.LogInformation("Started listening on {EndPoint} with async accept pattern", endpoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start listening on {EndPoint}", endpoint);
            throw new InvalidOperationException($"Failed to start listening on {endpoint.Address} {endpoint.Port}", ex);
        }
    }

    

    private void OptimizeThreadPoolForProxy ()
    {
        var processorCount = Environment.ProcessorCount;

        // Set minimum threads to avoid thread starvation under load
        var minWorkerThreads = Math.Max(processorCount * 2, _configuration.ThreadPoolWorkerThread);
        var minCompletionPortThreads = processorCount * 2;

        ThreadPool.SetMinThreads(minWorkerThreads, minCompletionPortThreads);

        // Set maximum threads for very high concurrency scenarios
        var maxWorkerThreads = processorCount * 32; // Aggressive for proxy workloads
        var maxCompletionPortThreads = processorCount * 32;

        ThreadPool.SetMaxThreads(maxWorkerThreads, maxCompletionPortThreads);

        _logger.LogDebug("Optimized ThreadPool: MinWorkers={MinWorkers}, MinIOCP={MinIOCP}, MaxWorkers={MaxWorkers}, MaxIOCP={MaxIOCP}",
            minWorkerThreads, minCompletionPortThreads, maxWorkerThreads, maxCompletionPortThreads);
    }

    public void Dispose ()
    {
        if (!_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }
    }
}
