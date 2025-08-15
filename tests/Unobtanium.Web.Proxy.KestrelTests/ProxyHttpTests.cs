using System.Text;

namespace Unobtanium.Web.Proxy.KestrelTests;
[TestClass]
public class ProxyHttpTests
{
    private static ProxyRunner? _proxyRunner;
    public TestContext TestContext { get; set; }

    [ClassInitialize]
    public static async Task InitializeAsync ( TestContext testContext )
    {
        _proxyRunner = new ProxyRunner(8888, 8889);
        await _proxyRunner.StartAsync(testContext.CancellationTokenSource.Token);
    }

    [ClassCleanup]
    public static async Task CleanupAsync ()
    {
        if (_proxyRunner != null)
        {
            await _proxyRunner.StopAsync();
            _proxyRunner.Dispose();
            _proxyRunner = null;
        }
    }

    [TestMethod]
    public async Task Http_request_should_be_intercepted ()
    {
        // Arrange
        var client = _proxyRunner!.CreateHttpClient();
        var interceptUri = "http://fake.svrooij.io/intercepted";
        // The proxy server is shared across tests, so we clear any previous events
        _proxyRunner.ProxyServerEvents.ClearEvents();
        _proxyRunner.ProxyServerEvents.OnRequest += async ( sender, args, cts ) =>
        {
            // Log the response details
            if (args.Request.RequestUri!.ToString() == interceptUri)
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
        var response = await client.GetAsync(interceptUri, TestContext!.CancellationTokenSource.Token);
        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, "expected a successful response from the proxy server.");
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html", "expected HTML content type.");

        // Read the content and verify it contains the intercepted response
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("<h1>Intercepted Response</h1>", "expected the response to contain the intercepted content.");
    }

    [TestMethod]
    public async Task Http_request_should_be_modified ()
    {
        // Arrange
        var client = _proxyRunner!.CreateHttpClient();
        var interceptUri = "http://fake.svrooij.io/modify";
        var modifiedUri = "https://github.com/svrooij/unobtanium-web-proxy/";
        // The proxy server is shared across tests, so we clear any previous events
        _proxyRunner.ProxyServerEvents.ClearEvents();
        _proxyRunner.ProxyServerEvents.OnRequest += async ( sender, args, cts ) =>
        {
            // Log the response details
            if (args.Request.RequestUri!.ToString() == interceptUri)
            {
                return RequestEventResponse.ModifyRequest(new HttpRequestMessage(HttpMethod.Get, modifiedUri));
            }
            return RequestEventResponse.ContinueResponse();
        };
        // Act
        var response = await client.GetAsync(interceptUri, TestContext!.CancellationTokenSource.Token);
        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, "expected a successful response from the proxy server.");
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html", "expected HTML content type.");

        // Read the content and verify it contains the intercepted response
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("unobtanium-web-proxy", "expected the response to contain result for modified request.");
    }

    [TestMethod]
    public async Task Https_request_should_passthrough_without_intercepting ()
    {
        // Arrange
        var client = _proxyRunner!.CreateHttpClient();
        var requestUri = "https://svrooij.io/";
        // The proxy server is shared across tests, so we clear any previous events
        _proxyRunner.ProxyServerEvents.ClearEvents();
        _proxyRunner.ProxyServerEvents.ShouldDecryptNewConnection = ( host, details, ct ) =>
        {
            // Do not decrypt HTTPS traffic
            return Task.FromResult(false);
        };
        // Act
        var response = await client.GetAsync(requestUri, TestContext!.CancellationTokenSource.Token);
        // Assert
        response.Should().NotBeNull("expected a response from the proxy server.");
        response.IsSuccessStatusCode.Should().BeTrue("expected a successful response from the proxy server.");
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html", "expected HTML content type.");

        // Read the content and verify it contains the expected text
        var content = await response.Content.ReadAsStringAsync(TestContext!.CancellationTokenSource.Token);
        content.Should().NotBeNullOrEmpty("expected the response content to be non-empty.");
        content.Should().Contain("Stephan van Rooij", "expected the response to contain the author's name.");
    }

}
