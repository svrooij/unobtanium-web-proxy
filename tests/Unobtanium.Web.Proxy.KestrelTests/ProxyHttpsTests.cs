using System.Text;

namespace Unobtanium.Web.Proxy.KestrelTests;
[TestClass]
public class ProxyHttpsTests
{
    private static ProxyRunner? _proxyRunner;
    public TestContext? TestContext { get; set; }

    private static string _tempCacheDirectory = null!;

    [ClassInitialize]
    public static async Task InitializeAsync ( TestContext testContext )
    {
        TestContext = testContext;
        _tempCacheDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempCacheDirectory);
        _proxyRunner = new ProxyRunner(8898, 8899, _tempCacheDirectory);
        await _proxyRunner.StartAsync(testContext.CancellationTokenSource.Token);
        _proxyRunner.ProxyServerEvents.ShouldDecryptNewConnection = ( host, ct ) =>
        {
            // Accept all connections for testing purposes
            return Task.FromResult(true);
        };
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
        if (Directory.Exists(_tempCacheDirectory))
        {
            Directory.Delete(_tempCacheDirectory, true);
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
    public async Task Https_request_should_be_intercepted ()
    {
        // Arrange
        var client = _proxyRunner!.CreateHttpClient(ignoreAllCertificateErrors: true);
        var requestUri = "https://fake.svrooij.io/intercepted";
        // The proxy server is shared across tests, so we clear any previous events
        _proxyRunner.ProxyServerEvents.ClearEvents();
        _proxyRunner.ProxyServerEvents.OnRequest += async ( sender, args, cts ) =>
        {
            // Log the response details
            if (args.Request.RequestUri!.ToString() == requestUri)
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
        var response = await client.GetAsync(requestUri, TestContext!.CancellationTokenSource.Token);

        // Assert
        response.Should().NotBeNull("expected a response from the proxy server.");
        response.IsSuccessStatusCode.Should().BeTrue("expected a successful response from the proxy server.");
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html", "expected HTML content type.");

        // Read the content and verify it contains the expected text
        var content = await response.Content.ReadAsStringAsync(TestContext!.CancellationTokenSource.Token);
        content.Should().NotBeNullOrEmpty("expected the response content to be non-empty.");
        content.Should().Contain("<h1>Intercepted Response</h1>", "expect the intercepted content");
    }

    [TestMethod]
    public async Task Https_request_should_return_server_response_if_no_interceptions_are_configured ()
    {
        // Arrange
        var client = _proxyRunner!.CreateHttpClient(ignoreAllCertificateErrors: true);
        var requestUri = "https://svrooij.io/";
        _proxyRunner.ProxyServerEvents.ClearEvents();

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

    [TestMethod]
    public async Task Https_request_should_fail_if_certificate_is_not_accepted ()
    {
        // Arrange
        var client = _proxyRunner!.CreateHttpClient(acceptFakeRootAndNormalTrusted: true);
        var requestUri = "https://svrooij.io/";
        _proxyRunner.ProxyServerEvents.ClearEvents();

        // Act
        var act = () => client.GetAsync(requestUri, TestContext!.CancellationTokenSource.Token);

        // Assert
        // We will not accept the certificate for svrooij.io, so we expect a certificate error
        await act.Should().ThrowAsync<HttpRequestException>("expected an exception due to certificate validation failure.")
            .WithMessage("*The SSL connection could not be established, see inner exception.*", "expected a specific error message indicating SSL connection failure.");
    }
}
