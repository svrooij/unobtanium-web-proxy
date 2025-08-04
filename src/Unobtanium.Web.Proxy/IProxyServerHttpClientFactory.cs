using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy;

/// <summary>
/// Defines a factory for creating <see cref="HttpClient"/> instances configured to communicate with a proxy server.
/// </summary>
/// <remarks>Implementations of this interface are responsible for providing <see cref="HttpClient"/> instances
/// that are properly configured to act as an outbound HttpClient, be sure to explicitly state that it should not use the system defined proxy or you'll end up in a loop.
/// Preferably you would create you're own implementation that uses the IHttpClientFactory in Microsoft.Extensions.Http to manage the lifetime of the HttpClient instances.
/// </remarks>
public interface IProxyServerHttpClientFactory
{
    /// <summary>
    /// Returns a new instance of <see cref="HttpClient"/> configured for outbound internet connectivity from the proxy server.
    /// </summary>
    HttpClient CreateHttpClient ();
}

internal class DefaultProxyServerHttpClientFactory : IProxyServerHttpClientFactory
{
    /// <inheritdoc />
    public HttpClient CreateHttpClient ()
    {
        // Create a new HttpClient instance with a handler that does not use any proxy
        var handler = new HttpClientHandler
        {
            UseProxy = false, // Explicitly disable proxy usage
            Proxy = null,      // Ensure no proxy is set
            // Do we really want to bypass certificate validation?
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true, // Bypass certificate
            
        };
        return new HttpClient(handler);
    }
}
