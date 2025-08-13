using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.Examples.WorkerTemplate;
internal class CustomProxyHttpClientFactory : IProxyHttpClientFactory
{
    internal const string CLIENT_NAME = "Unobtanium.Web.Proxy.Examples.WorkerTemplate";
    private readonly IHttpClientFactory httpClientFactory;

    public CustomProxyHttpClientFactory ( IHttpClientFactory httpClientFactory )
    {
        this.httpClientFactory = httpClientFactory;
    }

    public HttpClient CreateHttpClient ( string host )
    {
        return httpClientFactory.CreateClient(CLIENT_NAME);
    }
}
