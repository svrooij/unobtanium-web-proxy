using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections.Features;
using System.Buffers;
using Unobtanium.Web.Proxy.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;
using Unobtanium.Web.Proxy.Internal;
using System;
using System.Net.Http;
using System.Linq;
using Microsoft.Extensions.Options;
using System.Net;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Unobtanium.Web.Proxy.Services.Proxy;

internal static class ProxyEndpoints
{
    internal static readonly ActivitySource activitySource = ProxyServerDefaults.ProxyActivitySource;
    internal static void MapProxyEndpoints ( this WebApplication app )
    {
        // Map CONNECT method for HTTPS tunneling
        app.MapMethods("{**path}", [HttpMethods.Connect], async ( HttpContext context, ProxyServerEvents serverEvents, ICertificateManager certManager, IOptions<ProxyServerOptions> options, ConnectionMapper connectionMapper, IProxyEndpointResolver endpoints ) =>
        {
            await HandleConnectMethod(context, app.Logger, serverEvents, certManager, options.Value, connectionMapper, endpoints);
        });

        // Map a proxy endpoint that handles all requests
        app.Map("{**path}", async ( HttpContext context, ProxyServerEvents serverEvents, IProxyHttpClientFactory clientFactory, IOptions<ProxyServerOptions> options, ConnectionMapper connectionMapper ) =>
        {
            // Handle all other requests, proxying http traffic if just sending the same http request to the proxy server
            await HandleProxyRequest(context, app.Logger, serverEvents, clientFactory, connectionMapper);
        });
    }

    /// <summary>
    /// Handle the CONNECT method for HTTPS tunneling.
    /// </summary>
    /// <param name="context">Incoming <see cref="HttpContext"/></param>
    /// <param name="_logger">Proxy logger</param>
    /// <param name="serverEvents">Event handler that decides to proxy the request</param>
    /// <param name="certManager">Certificate manager to pre-load certificates</param>
    /// <param name="options"><see cref="ProxyServerOptions"/> to find out which port to proxy to</param>
    /// <param name="connectionMapper">Singleton dictionary for storing remote addresses and ports, this is used to keep record of incoming clients for HTTPS proxying</param>
    /// <param name="endpointResolver"></param>
    /// <returns></returns>
    private static async Task HandleConnectMethod ( HttpContext context, ILogger _logger, ProxyServerEvents serverEvents, ICertificateManager certManager, ProxyServerOptions options, ConnectionMapper connectionMapper, IProxyEndpointResolver endpointResolver )
    {
        using var activity = activitySource.StartActivity(nameof(HandleConnectMethod), ActivityKind.Consumer);
        // Check if we should decrypt this connection

        // Handle CONNECT method for tunneling
        // Extract the original target server and port from the request
        string originalHost = context.Request.Host.Host;
        int originalPort = context.Request.Host.Port ?? 443; // Default to 443 if no port is specified


        // Fire and forget the certificate retrieval (background task)
        _ = Task.Run(async () => await certManager.GetCertificateAsync(originalHost, CancellationToken.None), CancellationToken.None);

        var hostWithPort = originalPort != 443 ? $"{originalHost}:{originalPort}" : originalHost;
        var clientInfo = new ClientDetails(context.Connection.RemoteIpAddress!.ToString(), context.Connection.RemotePort, activity?.TraceId, activity?.SpanId);

        var shouldDecrypt = await serverEvents.InvokeShouldDecryptNewConnection(hostWithPort, clientInfo, context.RequestAborted);
        if (activity is not null)
        {
            activity.SetTag("server.host", originalHost);
            activity.SetTag("server.post", originalPort);
            activity.SetTag("proxy.intercept", shouldDecrypt.HasValue ? shouldDecrypt.Value : "blocked");
            activity.SetTag("client.address", context.Connection.RemoteIpAddress);
            activity.SetTag("client.port", context.Connection.RemotePort);

        }
        if (shouldDecrypt is null)
        {
            _logger.LogWarning("Connection decryption decision was canceled, terminating connection for {HostWithPort}", hostWithPort);
            context.Response.StatusCode = 503; // Service Unavailable
            await context.Response.WriteAsync("Request was blocked by application");
            return;
        }
        var targetHost = shouldDecrypt == true ? "localhost" : originalHost;
        int targetPort = shouldDecrypt == true ? endpointResolver.HttpsPort ?? options.HttpsPort : originalPort;
        _logger.LogInformation("CONNECT request received for {HostWithPort}, will intercept {Intercepting} {RemoteIp} {RemotePort}", hostWithPort, shouldDecrypt, clientInfo.Address, clientInfo.Port);

        // Get the connection feature
        var connectionFeature = context.Features.Get<IConnectionLifetimeFeature>();
        var connectionTransportFeature = context.Features.Get<IConnectionTransportFeature>();

        if (connectionTransportFeature == null)
        {
            _logger.LogError("Connection transport feature not available");
            context.Response.StatusCode = 500;
            activity?.SetStatus(ActivityStatusCode.Error, "Connection transport feature");
            await context.Response.WriteAsync("Failed to access transport connection");
            return;
        }

        try
        {
            using var client = new TcpClient();

            // Connect to our local Kestrel server instead of the original destination
            _logger.LogDebug("Establishing connection to {TargetHost}:{TargetPort} for {HostWithPort}", targetHost, targetPort, hostWithPort);
            await client.ConnectAsync(targetHost, targetPort);
            _logger.LogDebug("TCP Connection Established");

            if (shouldDecrypt == true)
            {
                // Save the connection in the mapper for later use
                // Saving the ClientInfo of the incoming connection in the dictionary under the outbound port
                // For the proxy it looks like a new connection, but we want to map it to the original client

                var outboundPort = ((IPEndPoint)client!.Client.LocalEndPoint!)?.Port ?? 0;
                if (outboundPort > 0)
                {
                    connectionMapper.Connections.AddOrUpdate($"{outboundPort}", clientInfo, ( key, oldValue ) => clientInfo);
                    _logger.LogDebug("Connection mapped for {HostWithPort} to {ClientInfo} on port {OutboundPort}", hostWithPort, clientInfo, outboundPort);
                }
            }

            // Get the connection's transport
            var transport = connectionTransportFeature.Transport;

            // Signal to the client that the tunnel is established
            context.Response.StatusCode = 200;
            await context.Response.CompleteAsync();

            // Get network stream for the local Kestrel server
            using var stream = client.GetStream();

            // We need to forward the SNI (Server Name Indication) information to our local Kestrel server
            // This is done implicitly by the client when establishing the TLS connection

            // Create cancellation token that is canceled when either connection is closed
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                context.RequestAborted,
                connectionFeature?.ConnectionClosed ?? CancellationToken.None);

            // Create tasks for copying data in both directions
            var serverToClientTask = stream.CopyDataAsync(transport.Output, "server -> client", _logger, cts.Token);
            var clientToServerTask = transport.Input.CopyDataAsync(stream, "client -> server", _logger, cts.Token);

            // Wait for any of the tasks to complete (or error)
            await Task.WhenAny(serverToClientTask, clientToServerTask);

            // Cancel the token to stop the other task
            cts.Cancel();

            // Wait for both tasks to finish (they should end quickly due to cancellation)
            await Task.WhenAll(serverToClientTask, clientToServerTask);

            _logger.LogDebug("Tunnel closed for {HostWithPort}", hostWithPort);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling CONNECT tunnel to {HostWithPort}", hostWithPort);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // If we haven't sent a response yet, send an error
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 502;
                await context.Response.WriteAsync($"Error establishing connection: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Handle all the incoming HTTP requests that are not using the <see cref="HttpMethod.Connect"/>
    /// </summary>
    /// <param name="context">Incoming <see cref="HttpContext"/></param>
    /// <param name="_logger">An instance of a logger</param>
    /// <param name="serverEvents">Event handlers that control the proxy</param>
    /// <param name="clientFactory">Client factory to create a new HttpClient for backchannel requests Proxy -> Remote Server</param>
    /// <param name="connectionMapper">Singleton dictionary for storing remote addresses and ports, this is used to keep record of incoming clients for HTTPS proxying</param>
    /// <returns></returns>
    private static async Task HandleProxyRequest ( HttpContext context, ILogger _logger, ProxyServerEvents serverEvents, IProxyHttpClientFactory clientFactory, ConnectionMapper connectionMapper )
    {
        ClientDetails clientDetails = connectionMapper.Connections
            .FirstOrDefault(x => x.Key == context.Connection.RemotePort.ToString()).Value
            ?? new ClientDetails(context.Connection.RemoteIpAddress!.ToString(), context.Connection.RemotePort);
        Activity? activity = null;
        if (activitySource.HasListeners())
        {
            // Create link to ConnectActivity using the connection trace ID if available
            if (clientDetails.ConnectionSpanId is not null && clientDetails.ConnectionTraceId is not null)
            {
                activity = activitySource.StartActivity(nameof(HandleProxyRequest), ActivityKind.Consumer, null, links: [
                    new ActivityLink(new ActivityContext(clientDetails.ConnectionTraceId.Value, clientDetails.ConnectionSpanId.Value, ActivityTraceFlags.Recorded))
                    ]);
            } else
            {
                activity = activitySource.StartActivity(nameof(HandleProxyRequest), ActivityKind.Consumer);
            }
                // Start a new activity for distributed tracing if there are listeners
                
        }

        using (activity)
        {
            string? requestId = null;
            try
            {
                // Extract target URL
                var targetUrl = ExtractTargetUrl(context);
                // Create HttpRequestMessage from the incoming request
                var requestMessage = CreateProxyRequest(context, targetUrl);

                _logger.LogInformation("Proxy request {Method} {TargetUrl} {RemoteIp} {RemotePort}", requestMessage.Method.Method, targetUrl, clientDetails.Address, clientDetails.Port);

                using var arguments = new RequestEventArguments(requestMessage, clientDetails, activity, context.TraceIdentifier);
                requestId = arguments.RequestId;
                if (activity is not null)
                {
                    activity.SetTag("http.request.method", requestMessage.Method.Method);
                    activity.SetTag("url.full", requestMessage.RequestUri?.ToString());
                    activity.SetTag("proxy.requestId", requestId);
                    if (requestMessage.RequestUri?.Host != null)
                    {
                        activity.SetTag("server.host", requestMessage.RequestUri.Host);
                        activity.SetTag("server.port", requestMessage.RequestUri.Port);
                    }
                    activity.SetTag("client.address", clientDetails.Address);
                    activity.SetTag("client.port", clientDetails.Port);
                }
                var responseEvent = await serverEvents.InvokeOnRequest(context, arguments, _logger, context.RequestAborted);
                if (responseEvent.Response != null)
                {
                    // If the event handler returned a response, send it back to the client
                    _logger.LogInformation("Proxy early response {Method} {TargetUrl} {RequestId}", requestMessage.Method.Method, targetUrl, arguments.RequestId); ;
                    activity?.SetTag("proxy.response.source", "OnRequest");
                    await CopyResponseToClient(context, responseEvent.Response);
                    return;
                }
                if (responseEvent.ModifiedRequest != null)
                {
                    // If the request was modified, use the modified request
                    requestMessage = responseEvent.ModifiedRequest;
                    activity?.SetTag("proxy.request.source", "OnRequest");
                    _logger.LogInformation("Proxy modified request {Method} {TargetUrl} {RequestId}", requestMessage.Method.Method, targetUrl, arguments.RequestId);
                }

                // Send request to target server
                var _httpClient = clientFactory.CreateHttpClient(requestMessage.RequestUri!.Host);
                var responseMessage = await _httpClient.SendAsync(requestMessage);
                using var onResponseArguments = new ResponseEventArguments(requestMessage, responseMessage, clientDetails, activity, arguments.RequestId);
                var eventResponse = await serverEvents.InvokeOnResponse(context, onResponseArguments , _logger, context.RequestAborted);

                if (eventResponse.ModifiedResponse is not null)
                {
                    // If the event handler modified the response, use the modified response
                    _logger.LogInformation("Proxy modified response {Method} {TargetUrl} {RequestId}", requestMessage.Method.Method, targetUrl, arguments.RequestId); ;
                    activity?.SetTag("proxy.response.source", "OnResponse");
                    responseMessage = eventResponse.ModifiedResponse;
                }
                else
                {
                    activity?.SetTag("proxy.response.source", "Remote");
                }

                // Return the response to the client
                await CopyResponseToClient(context, responseMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling proxy request {RequestId}", requestId);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Proxy error: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Create full url from the incoming <see cref="HttpContext"/>.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    private static string ExtractTargetUrl ( HttpContext context )
    {
        return $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}";
    }

    /// <summary>
    /// Create a new <see cref="HttpRequestMessage"/> based on the incoming HTTP request."/>
    /// </summary>
    /// <param name="context">Incoming <see cref="HttpContent"/></param>
    /// <param name="targetUrl">Extracted full target url</param>
    /// <returns></returns>
    private static HttpRequestMessage CreateProxyRequest ( HttpContext context, string targetUrl )
    {
        var requestMessage = new HttpRequestMessage
        {
            Method = new HttpMethod(context.Request.Method),
            RequestUri = new Uri(targetUrl)
        };

        // Copy headers from the incoming request to the outgoing request
        foreach (var header in context.Request.Headers)
        {
            if (!header.Key.StartsWith("Host", StringComparison.OrdinalIgnoreCase) &&
                !header.Key.StartsWith("Connection", StringComparison.OrdinalIgnoreCase) &&
                !header.Key.StartsWith("Proxy-Connection", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        // Copy the request body
        if (context.Request.ContentLength > 0)
        {
            requestMessage.Content = new StreamContent(context.Request.Body);

            // Copy content-type
            if (context.Request.ContentType != null)
            {
                requestMessage.Content.Headers.ContentType =
                    System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Request.ContentType);
            }
        }

        return requestMessage;
    }

    /// <summary>
    /// Write the proxy response back to the <see cref="HttpContext.Response"/>.
    /// </summary>
    /// <param name="context">Incoming <see cref="HttpContent"/></param>
    /// <param name="responseMessage">The proxied <see cref="HttpResponseMessage"/> either from HttpClient or generated</param>
    /// <returns></returns>
    private static async Task CopyResponseToClient ( HttpContext context, HttpResponseMessage responseMessage )
    {
        // Copy status code
        context.Response.StatusCode = (int)responseMessage.StatusCode;

        // Copy response headers
        foreach (var header in responseMessage.Headers)
        {
            if (header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Upgrade", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Proxy-Connection", StringComparison.OrdinalIgnoreCase))
            {
                // Skip some forbidden headers that should not be forwarded
                continue;
            }
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        // Copy content headers
        if (responseMessage.Content != null)
        {
            foreach (var header in responseMessage.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            // Copy response body
            await responseMessage.Content.CopyToAsync(context.Response.Body);
        }
    }
}
