using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy;
/// <summary>
/// Defines a factory for creating <see cref="HttpClient"/> instances configured for outbound internet connectivity
/// through a proxy server.
/// </summary>
/// <remarks>This interface is intended to provide a mechanism for generating <see cref="HttpClient"/> objects
/// that are pre-configured to route requests through a proxy server. Implementations may apply additional
/// configurations specific to the provided host.</remarks>
public interface IProxyHttpClientFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="HttpClient"/> configured for outbound internet connectivity from the proxy server.
    /// </summary>
    /// <param name="host">The host to which the <see cref="HttpClient"/> will connect. This may be used to configure the proxy settings or other client options.</param>
    /// <returns>A new <see cref="HttpClient"/> instance.</returns>
    HttpClient CreateHttpClient (string host);
}
