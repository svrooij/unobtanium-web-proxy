using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.Services;
internal class ProxyHttpClientFactory : IProxyHttpClientFactory
{
    public HttpClient CreateHttpClient ( string host )
    {
        // Create a new HttpClient instance with a handler that does not use any proxy
        var handler = new HttpClientHandler
        {
            UseProxy = false, // Explicitly disable proxy usage
            Proxy = null,      // Ensure no proxy is set
            // Do we really want to bypass certificate validation?
            ServerCertificateCustomValidationCallback = ( message, cert, chain, errors ) => true, // Bypass certificate

        };
        return new HttpClient(handler);
    }
}
