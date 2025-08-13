using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Events;
using Unobtanium.Web.Proxy.KestrelTests.TestHelpers;

namespace Unobtanium.Web.Proxy.KestrelTests;
[TestClass]
public class ProxyTests
{
    private static ProxyRunner? _proxyRunner;
    private static TestContext? _testContext;

    [ClassInitialize]
    public static async Task InitializeAsync( TestContext testContext)
    {
        _testContext = testContext;
        _proxyRunner = new ProxyRunner(8888, 8889);
        await _proxyRunner.StartAsync(testContext.CancellationTokenSource.Token);
    }

    [ClassCleanup]
    public static async Task CleanupAsync()
    {
        if (_proxyRunner != null)
        {
            await _proxyRunner.StopAsync();
            _proxyRunner.Dispose();
            _proxyRunner = null;
        }
    }

    [TestMethod]
    public async Task ProxyServer_Should_Intercept_HttpRequest()
    {
        // Arrange
        var client = _proxyRunner!.CreateHttpClient();
        var interceptUri = "http://fake.svrooij.io/intercepted";
        // The proxy server is shared across tests, so we clear any previous events
        _proxyRunner.ProxyServerEvents.ClearEvents();
        _proxyRunner.ProxyServerEvents.OnRequest += async (sender, args, cts) =>
        {
            // Log the response details
            if (args.Request.RequestUri!.ToString() == interceptUri )
            {
                return RequestEventResponse.EarlyResponse(new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = new StringContent("<html><body><h1>Intercepted Response</h1></body></html>", Encoding.UTF8, "text/html")
                });
            }
            return RequestEventResponse.ContinueResponse();
        };
        // Act
        var response = await client.GetAsync(interceptUri);
        // Assert
        Assert.IsTrue(response.IsSuccessStatusCode, "Expected a successful response from the proxy server.");
        Assert.AreEqual("text/html", response.Content.Headers.ContentType?.MediaType, "Expected HTML content type.");
        var content = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(content.Contains("<h1>Intercepted Response</h1>"), "Expected the response to contain the intercepted content.");
    }
}
