using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unobtanium.Proxy.Interfaces;

namespace Unobtanium.Web.Proxy.Examples.WorkerTemplate;
internal class CustomProxyHttpClientFactory : IProxyHttpClientFactory
{
    private readonly IHttpClientFactory httpClientFactory;

    public CustomProxyHttpClientFactory ( IHttpClientFactory httpClientFactory )
    {
        this.httpClientFactory = httpClientFactory;
    }

    public HttpClient CreateHttpClient (string host)
    {
        return httpClientFactory.CreateClient("Unobtanium.Web.Proxy.Examples.WorkerTemplate");
    }
}
