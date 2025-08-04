using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.Services;

/// <summary>
/// Service responsible for managing HttpClient instances and sending HTTP requests.
/// This service explicitly bypasses any system-defined proxy settings to ensure
/// direct internet connectivity for outbound requests from the proxy server.
/// </summary>
/// <remarks>
/// The HttpClient is configured with UseProxy = false to prevent infinite loops
/// when the proxy server needs to make outbound requests to external servers.
/// This ensures that the proxy server itself doesn't route its outbound traffic
/// through any system-configured proxy, including itself.
/// </remarks>
public class HttpClientService : IDisposable
{
    private readonly ILogger<HttpClientService> logger;
    private readonly HttpClient httpClient;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the HttpClientService with proxy bypass enabled.
    /// </summary>
    /// <param name="logger">Logger instance for debugging and monitoring</param>
    public HttpClientService(ILogger<HttpClientService> logger)
    {
        this.logger = logger;
        
        // Create HttpClientHandler that explicitly bypasses any system proxy settings
        // This is crucial to prevent infinite loops where the proxy server would
        // try to route its own outbound requests through a system proxy (potentially itself)
        var handler = new HttpClientHandler()
        {
            UseProxy = false, // Explicitly disable proxy usage
            Proxy = null      // Ensure no proxy is set
        };

        // Create HttpClient with the configured handler to bypass system proxy
        httpClient = new HttpClient(handler);
        
        logger.LogDebug("HttpClientService initialized with proxy bypass enabled for direct internet connectivity");
    }

    /// <summary>
    /// Send an HTTP request and get the response
    /// </summary>
    /// <param name="request">The HTTP request to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTTP response message</returns>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(HttpClientService));

        try
        {
            logger.LogDebug("Sending HTTP request directly to internet (bypassing proxy): {Method} {Uri}", request.Method, request.RequestUri);
            
            var response = await httpClient.SendAsync(request, cancellationToken);
            
            logger.LogDebug("Received HTTP response: {StatusCode} for {Method} {Uri}", 
                response.StatusCode, request.Method, request.RequestUri);
            
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending HTTP request: {Method} {Uri}", request.Method, request.RequestUri);
            throw;
        }
    }

    /// <summary>
    /// Send an HTTP request and get the response with completion option
    /// </summary>
    /// <param name="request">The HTTP request to send</param>
    /// <param name="completionOption">Http completion option</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTTP response message</returns>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken = default)
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(HttpClientService));

        try
        {
            logger.LogDebug("Sending HTTP request directly to internet (bypassing proxy): {Method} {Uri}", request.Method, request.RequestUri);
            
            var response = await httpClient.SendAsync(request, completionOption, cancellationToken);
            
            logger.LogDebug("Received HTTP response: {StatusCode} for {Method} {Uri}", 
                response.StatusCode, request.Method, request.RequestUri);
            
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending HTTP request: {Method} {Uri}", request.Method, request.RequestUri);
            throw;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed && disposing)
        {
            httpClient?.Dispose();
            disposed = true;
        }
    }
}
