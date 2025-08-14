using System.Diagnostics;
using System.Net.Sockets;
using System.IO.Pipelines;
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

namespace Unobtanium.Web.Proxy.Services.Proxy;

internal static class ProxyEndpoints
{
    internal static readonly ActivitySource activitySource = ProxyServerDefaults.ProxyActivitySource;
    internal static void MapProxyEndpoints ( this WebApplication app )
    {
        // Map a proxy endpoint that handles all requests
        app.Map("{**path}", async ( HttpContext context, ProxyServerEvents serverEvents, ICertificateManager certManager, IProxyHttpClientFactory clientFactory, IOptions<ProxyServerOptions> options ) =>
        {
            //using var activity = activitySource.StartActivity("ProxyRequest", ActivityKind.Consumer);
            // Handle requests to the root path and any sub-paths
            if (context.Request.Method == HttpMethods.Connect)
            {
                // Handle CONNECT method for tunneling
                // When the client asks to tunnel https traffic over an http proxy connection
                await HandleConnectMethod(context, app.Logger, serverEvents, certManager, options.Value);
            }
            else
            {
                // Handle all other requests, proxying http traffic if just sending the same http request to the proxy server
                await HandleProxyRequest(context, app.Logger, serverEvents, clientFactory);
            }
        });
    }

    private static async Task HandleConnectMethod ( HttpContext context, ILogger _logger, ProxyServerEvents serverEvents, ICertificateManager certManager, ProxyServerOptions options )
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
        using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        var shouldDecrypt = await serverEvents.InvokeShouldDecryptNewConnection(hostWithPort, cts2);
        if (activity is not null)
        {
            activity.SetTag("server.host", originalHost);
            activity.SetTag("server.post", originalPort);
            activity.SetTag("proxy.intercept", shouldDecrypt);
        }
        var targetHost = shouldDecrypt ? "localhost" : originalHost;
        var targetPort = shouldDecrypt ? options.HttpsPort : originalPort; // Forward to local Kestrel if decrypting, otherwise use original port
        _logger.LogInformation("CONNECT request received for {HostWithPort}, will intercept {Intercepting}", hostWithPort, shouldDecrypt);

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

    private static async Task HandleProxyRequest ( HttpContext context, ILogger _logger, ProxyServerEvents serverEvents, IProxyHttpClientFactory clientFactory )
    {
        using var activity = activitySource.StartActivity(nameof(HandleProxyRequest), ActivityKind.Consumer);
        string? requestId = null;
        try
        {
            // Extract target URL
            var targetUrl = ExtractTargetUrl(context);
            // Create HttpRequestMessage from the incoming request
            var requestMessage = CreateProxyRequest(context, targetUrl);

            _logger.LogInformation("Proxy request {Method} {TargetUrl}", requestMessage.Method.Method, targetUrl);

            var arguments = new RequestEventArguments(requestMessage, activity);
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

            var eventResponse = await serverEvents.InvokeOnResponse(context, new ResponseEventArguments(requestMessage, responseMessage, activity, arguments.RequestId), _logger, context.RequestAborted);

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
            context.Response.StatusCode = 500;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            await context.Response.WriteAsync("Proxy error: " + ex.Message);
        }
    }

    private static string ExtractTargetUrl ( HttpContext context )
    {
        return $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}";
    }

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
