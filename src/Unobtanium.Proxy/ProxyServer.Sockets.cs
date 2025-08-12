using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Unobtanium.Proxy.Extensions;
using Unobtanium.Proxy.Tcp;

namespace Unobtanium.Proxy;
public partial class ProxyServer
{
    /// <summary>
    ///     Modern high-performance async accept loop for .NET 8+
    /// </summary>
    /// <param name="endPoint">The proxy endpoint to accept connections for</param>
    /// <param name="cancellationToken">Cancellation token to stop the accept loop</param>
    private async Task AcceptConnectionsAsync ( ProxyEndpoint endPoint, CancellationToken cancellationToken )
    {
        var listener = endPoint.Listener!;
        var endPointInfo = $"{endPoint.Address}:{endPoint.Port}";

        _logger.LogDebug("Started async accept loop for endpoint {EndPoint}", endPointInfo);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Socket? tcpClient = null;

                try
                {
                    // Modern async accept - much more efficient than the old callback pattern
                    tcpClient = await listener.AcceptSocketAsync(cancellationToken).ConfigureAwait(false);

                    // Configure socket for maximum performance immediately
                    ConfigureSocketForPerformance(tcpClient);

                    // Fire and forget client handling
                    _ = SetupClientConnectionAsync(tcpClient, endPoint, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Expected when shutting down
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // Listener was disposed - exit gracefully
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error accepting connection on endpoint {EndPoint}", endPointInfo);

                    // Close the problematic socket if we got one
                    try
                    {
                        tcpClient?.Close();
                    }
                    catch { }

                    // Brief pause before retry to avoid tight error loops
                    try
                    {
                        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            _logger.LogDebug("Async accept loop ended for endpoint {EndPoint}", endPointInfo);
        }
    }

    /// <summary>
    ///     Handle client connection with optimized async pattern
    /// </summary>
    /// <param name="tcpClientSocket">Client socket</param>
    /// <param name="endPoint">Proxy endpoint</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task SetupClientConnectionAsync ( Socket tcpClientSocket, ProxyEndpoint endPoint, CancellationToken cancellationToken )
    {
        using var connectionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Only create activity if tracing is enabled (performance optimization)
        Activity? clientConnectionActivity = null;
        if (ProxyActivitySource.HasListeners())
        {
            clientConnectionActivity = ProxyActivitySource.StartActivity(nameof(SetupClientConnectionAsync), ActivityKind.Server);
            clientConnectionActivity?.SetTag("client.endpoint", tcpClientSocket.RemoteEndPoint?.ToString());
            clientConnectionActivity?.SetTag("proxy.endpoint", endPoint.ToString());
            clientConnectionActivity?.SetTag("connection.type", endPoint.GetType().Name);
        }

        using var clientConnection = new TcpClientConnection(tcpClientSocket, clientConnectionActivity);

        try
        {
            await HandleClientConnectionAsync(clientConnection, connectionCancellationTokenSource);
        }
        catch (Exception ex)
        {
            clientConnectionActivity?.RecordException(ex);
            _logger.LogDebug(ex, "Error handling client connection from {RemoteEndPoint}",
                tcpClientSocket.RemoteEndPoint);
            if (connectionCancellationTokenSource.IsCancellationRequested == false)
            {
                connectionCancellationTokenSource.Cancel();
            }
        }
    }

    private async Task HandleClientConnectionAsync( TcpClientConnection clientConnection, CancellationTokenSource cts)
    {

    }

    /// <summary>
    ///     Configure socket for maximum performance
    /// </summary>
    /// <param name="socket">Socket to configure</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ConfigureSocketForPerformance ( Socket socket )
    {
        socket.NoDelay = false;
        socket.ReceiveTimeout = _configuration.ConnectionTimeout * 1000;
        socket.SendTimeout = _configuration.ConnectionTimeout * 1000;
        socket.LingerState = new LingerOption(true, 10);

        // Optimize buffer sizes for high throughput
        socket.ReceiveBufferSize = 65536; // 64KB
        socket.SendBufferSize = 65536;    // 64KB

        // Platform-specific optimizations
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                // Windows-specific TCP optimizations
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 30);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 1);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to set Windows-specific socket options");
            }
        }
    }
}
