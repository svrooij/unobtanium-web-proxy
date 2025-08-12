using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Security;
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
            clientConnectionActivity = ProxyActivitySource.StartActivity("ProxyRequest", ActivityKind.Server);
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
            tcpClientSocket.Close();
        }
    }

    private async Task HandleClientConnectionAsync( TcpClientConnection clientConnection, CancellationTokenSource cts)
    {
        // At this point we have a client connection ready to be processed.
        // It can either be a CONNECT message for HTTPS or a regular HTTP request, and those need to be handled differently.

        var firstLine = await clientConnection.GetStream.ReadFirstLineAsync(cts.Token).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            _logger.LogDebug("Received empty first line from {RemoteEndPoint}", clientConnection.RemoteEndpoint);
            return; // No valid request, just return
        }
        if (firstLine.StartsWith("CONNECT ", StringComparison.OrdinalIgnoreCase))
        {
            // Handle CONNECT request for HTTPS
            _logger.LogDebug("Handling CONNECT request from {RemoteEndPoint}: {FirstLine}",
                clientConnection.RemoteEndpoint, firstLine);
            await HandleConnectRequestAsync(firstLine, clientConnection, cts.Token).ConfigureAwait(false);
        }
        else
        {
            // Handle regular HTTP request
            _logger.LogDebug("Handling HTTP request from {RemoteEndPoint}: {FirstLine}",
                clientConnection.RemoteEndpoint, firstLine);
            await HandleHttpRequest(firstLine, clientConnection, cts.Token).ConfigureAwait(false);
        }
    }

    private async Task HandleConnectRequestAsync ( string firstLine, TcpClientConnection clientConnection, CancellationToken cancellationToken )
    {
        // Extract the target host, port and http version from the CONNECT request
        var parts = firstLine.Split(' ');
        if (parts.Length != 3)
        {
            _logger.LogWarning("Invalid CONNECT request from {RemoteEndPoint}: {FirstLine}",
                clientConnection.RemoteEndpoint, firstLine);
            return; // Invalid CONNECT request, just return
        }
        var targetHost = parts[1];
        var targetPort = 443; // Default HTTPS port
        if (targetHost.Contains(':'))
        {
            // If the host contains a port, split it
            var hostParts = targetHost.Split(':');
            targetHost = hostParts[0];
            if (hostParts.Length > 1 && int.TryParse(hostParts[1], out var port))
            {
                targetPort = port;
            }
        }
        if (clientConnection.Activity is not null)
        {
            clientConnection.Activity.SetTag("server.address", targetHost);
            clientConnection.Activity.SetTag("server.port", targetPort);
        }

        // Send 200 Connection Established response
        await SendConnectionEstablishedResponseAsync(clientConnection, cancellationToken).ConfigureAwait(false);

        bool tryToDecrypt = await _configuration.TryDecryptHttps(targetHost, cancellationToken).ConfigureAwait(false);
        if (!tryToDecrypt)
        {
            // This will just proxy the request as is without decryption, is the first line needed?
            await ProxyRequestAsIsAsync(clientConnection, targetHost, targetPort, cancellationToken).ConfigureAwait(false);
            return;
        }

        await ProxyRequestWithInspectionAsync(clientConnection, targetHost, targetPort, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProxyRequestWithInspectionAsync(TcpClientConnection clientConnection, string targetHost, int targetPort, CancellationToken cancellationToken)
    {
        using var activity = ProxyActivitySource.StartActivity("ProxyRequestWithInspection", ActivityKind.Internal, clientConnection.Activity?.Context ?? default);
        var cert = await _certificateManager.GetCertificateAsync(targetHost, cancellationToken).ConfigureAwait(false);
        try
        {
            // Read the client request stream using the certificate
            using var sslStream = new SslStream(clientConnection.GetStream, true);
            await sslStream.AuthenticateAsServerAsync(cert, false, System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13, false).ConfigureAwait(false);
            _logger.LogDebug("SSL stream established for {RemoteEndPoint} to {TargetHost}:{TargetPort}",
                clientConnection.RemoteEndpoint, targetHost, targetPort);

            using var reader = new StreamReader(sslStream, Encoding.ASCII, leaveOpen: true);
            var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create SSL stream for {RemoteEndPoint} to {TargetHost}:{TargetPort}",
                clientConnection.RemoteEndpoint, targetHost, targetPort);
            throw;
        }




    }

    private async Task ProxyRequestAsIsAsync( TcpClientConnection clientConnection, string targetHost, int targetPort, CancellationToken cancellationToken )
    {
        using var activity = ProxyActivitySource.StartActivity("ProxyRequestAsIs", ActivityKind.Internal, clientConnection.Activity?.Context ?? default);
        using var timeoutCts = new CancellationTokenSource(5000);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        try
        {
            // Create a new TCP connection to the target host
            using var targetSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            await targetSocket.ConnectAsync(targetHost, targetPort, cancellationToken).ConfigureAwait(false);
            // Configure the socket for performance
            ConfigureSocketForPerformance(targetSocket);
            // Get the network stream for both client and target
            using var clientStream = clientConnection.GetStream;
            using var targetStream = new NetworkStream(targetSocket, true);
            _logger.LogDebug("Proxying request from {RemoteEndPoint} to {TargetHost}:{TargetPort}",
                clientConnection.RemoteEndpoint, targetHost, targetPort);
            // While there is data on either side, forward it and close the connection when done
            await Task.WhenAny(
                clientStream.CopyToAsync(targetStream, cts.Token),
                targetStream.CopyToAsync(clientStream, cts.Token)
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying request from {RemoteEndPoint} to {TargetHost}:{TargetPort}",
                clientConnection.RemoteEndpoint, targetHost, targetPort);
            throw;
        }
        finally
        {
            clientConnection.Dispose();
        }
    }

    private async Task SendConnectionEstablishedResponseAsync ( TcpClientConnection clientConnection, CancellationToken cancellationToken )
    {
        try
        {
            var response = "HTTP/1.1 200 Connection Established\r\nConnection: keep-alive\r\n\r\n";
            var responseBytes = Encoding.ASCII.GetBytes(response);
            var stream = clientConnection.GetStream;
            await stream.WriteAsync(responseBytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send connection established response to {RemoteEndPoint}",
                clientConnection.RemoteEndpoint);
            throw;
        }
    }

    private async Task HandleHttpRequest(string firstLine, TcpClientConnection clientConnection, CancellationToken cancellationToken)
    {
        using var activity = ProxyActivitySource.StartActivity("HandleHttpRequest", ActivityKind.Internal, clientConnection.Activity?.Context ?? default);
        
        try
        {
            _logger.LogInformation("Processing HTTP request: {FirstLine}", firstLine);
            
            // Parse the complete HTTP request message from the client's stream
            var httpRequestMessage = await clientConnection.GetStream.ParseHttpRequestMessageAsync(firstLine, cancellationToken).ConfigureAwait(false);
            if (httpRequestMessage == null)
            {
                _logger.LogWarning("Failed to parse HTTP request from client {RemoteEndPoint}", clientConnection.RemoteEndpoint);
                return;
            }
            
            // Add tracing information to the request
            activity?.SetTag("http.request.method", httpRequestMessage.Method.Method);
            activity?.SetTag("url.full", httpRequestMessage.RequestUri?.ToString());
            
            if (httpRequestMessage.RequestUri?.Host != null)
            {
                activity?.SetTag("server.address", httpRequestMessage.RequestUri.Host);
                activity?.SetTag("server.port", httpRequestMessage.RequestUri.Port);
            }
            
            // Create request arguments for the event handler
            var requestEventArgs = new Unobtanium.Web.Proxy.Events.RequestEventArguments(httpRequestMessage, activity);
            
            // Pass the request to the event handlers in configuration
            var eventHandlerResult = await _configuration.Events.InvokeOnRequest(this, requestEventArgs, _logger, cancellationToken).ConfigureAwait(false);
            
            HttpResponseMessage? responseMessage = eventHandlerResult.Response;
            
            // Check if the event handler provided an early response
            if (eventHandlerResult.Response != null)
            {
                activity?.SetTag("response.source", "event_handler");
            }
            // Check if the event handler modified the request
            else if (eventHandlerResult.ModifiedRequest != null)
            {
                // Use the modified request to send to the server
                httpRequestMessage = eventHandlerResult.ModifiedRequest;
                activity?.SetTag("request.modified", "true");
                
                // Forward the modified request to the server
                responseMessage = await SendRequestToServerAsync(httpRequestMessage, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // No modifications, forward the original request
                responseMessage = await SendRequestToServerAsync(httpRequestMessage, cancellationToken).ConfigureAwait(false);
            }
            
            // Send the response back to the client
            await SendResponseToClientAsync(responseMessage!, clientConnection, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling HTTP request from {RemoteEndPoint}", clientConnection.RemoteEndpoint);
            activity?.RecordException(ex);
            
            try
            {
                // Try to send a 500 error response
                await SendErrorResponseAsync(clientConnection, 500, "Internal Server Error", cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors in sending error response
            }
        }
    }
    
    private async Task<HttpResponseMessage> SendRequestToServerAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateHttpClient(request.RequestUri!.Host);

        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }
    
    private async Task SendResponseToClientAsync(HttpResponseMessage response, TcpClientConnection clientConnection, CancellationToken cancellationToken)
    {
        var stream = clientConnection.GetStream;
        
        // Create the status line
        var statusLine = $"HTTP/{response.Version.Major}.{response.Version.Minor} {(int)response.StatusCode} {response.ReasonPhrase ?? GetDefaultReasonPhrase((int)response.StatusCode)}\r\n";
        var headerBuilder = new StringBuilder(statusLine);
        
        // Add headers
        foreach (var header in response.Headers)
        {
            foreach (var value in header.Value)
            {
                headerBuilder.Append($"{header.Key}: {value}\r\n");
            }
        }
        
        // Add content headers if content exists
        if (response.Content != null)
        {
            foreach (var header in response.Content.Headers)
            {
                foreach (var value in header.Value)
                {
                    headerBuilder.Append($"{header.Key}: {value}\r\n");
                }
            }
        }
        
        // End of headers
        headerBuilder.Append("\r\n");
        
        // Write headers
        var headerBytes = Encoding.ASCII.GetBytes(headerBuilder.ToString());
        await stream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
        
        // Write content if exists
        if (response.Content != null)
        {
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await contentStream.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
    
    private async Task SendErrorResponseAsync(TcpClientConnection clientConnection, int statusCode, string reasonPhrase, CancellationToken cancellationToken)
    {
        var errorContent = $"<html><body><h1>{statusCode} {reasonPhrase}</h1><p>The proxy server encountered an error.</p></body></html>";
        var contentBytes = Encoding.UTF8.GetBytes(errorContent);
        
        var response = new StringBuilder();
        response.AppendLine($"HTTP/1.1 {statusCode} {reasonPhrase}");
        response.AppendLine("Content-Type: text/html; charset=utf-8");
        response.AppendLine($"Content-Length: {contentBytes.Length}");
        response.AppendLine("Connection: close");
        response.AppendLine();
        
        var responseBytes = Encoding.ASCII.GetBytes(response.ToString());
        
        var stream = clientConnection.GetStream;
        await stream.WriteAsync(responseBytes, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(contentBytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
    
    private string GetDefaultReasonPhrase(int statusCode)
    {
        return statusCode switch
        {
            200 => "OK",
            201 => "Created",
            202 => "Accepted",
            204 => "No Content",
            301 => "Moved Permanently",
            302 => "Found",
            304 => "Not Modified",
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            405 => "Method Not Allowed",
            500 => "Internal Server Error",
            501 => "Not Implemented",
            502 => "Bad Gateway",
            503 => "Service Unavailable",
            _ => "Unknown Status Code"
        };
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
