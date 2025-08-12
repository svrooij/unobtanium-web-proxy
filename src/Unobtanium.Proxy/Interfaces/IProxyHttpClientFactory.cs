using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Proxy.Interfaces;
public interface IProxyHttpClientFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="HttpClient"/> configured for outbound internet connectivity from the proxy server.
    /// </summary>
    /// <returns>A new <see cref="HttpClient"/> instance.</returns>
    HttpClient CreateHttpClient(string host);
}
