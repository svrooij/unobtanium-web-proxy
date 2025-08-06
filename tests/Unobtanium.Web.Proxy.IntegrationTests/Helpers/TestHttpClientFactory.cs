using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Unobtanium.Web.Proxy.IntegrationTests.Helpers;

internal class TestHttpClientFactory : IProxyServerHttpClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    public TestHttpClientFactory()
    {
        // Trick the platform into creating a default HttpClientFactory
        // https://stackoverflow.com/a/75057730/639153
        _httpClientFactory = new ServiceCollection()
            .AddHttpClient()
            .BuildServiceProvider()
            .GetRequiredService<IHttpClientFactory>();
    }
    public HttpClient CreateHttpClient() => _httpClientFactory.CreateClient();
}
