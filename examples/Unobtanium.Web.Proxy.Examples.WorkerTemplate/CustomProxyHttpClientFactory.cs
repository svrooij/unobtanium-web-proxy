using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.Examples.WorkerTemplate;
internal class CustomProxyHttpClientFactory : IProxyServerHttpClientFactory
{
    private readonly IHttpClientFactory httpClientFactory;

    public CustomProxyHttpClientFactory ( IHttpClientFactory httpClientFactory )
    {
        this.httpClientFactory = httpClientFactory;
    }

    public HttpClient CreateHttpClient ()
    {
        return httpClientFactory.CreateClient("Unobtanium.Web.Proxy.Examples.WorkerTemplate");
    }
}
