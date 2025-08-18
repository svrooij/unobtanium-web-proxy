using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;

namespace Unobtanium.Web.Proxy.KestrelTests;
[TestClass]
public class ProxyHttpTests
{
    private static ProxyRunner? _proxyRunner;
    public TestContext TestContext { get; set; }

    [ClassInitialize]
    public static async Task InitializeAsync ( TestContext testContext )
    {
        _proxyRunner = new ProxyRunner(testContext, 0, 0);
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
        AsyncEventHandler<RequestEventArguments, RequestEventResponse> handler = async ( sender, args, cancellationToken ) =>
        {
            if (args.Request.RequestUri?.ToString() == interceptUri)
            {
                return RequestEventResponse.EarlyResponse(new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = new StringContent("<html><body><h1>Intercepted Response</h1></body></html>", Encoding.UTF8, "text/html")
                });
            }
            return RequestEventResponse.ContinueResponse();
        };
        _proxyRunner.ProxyServerEvents.OnRequest += handler;
        try
        {
            // Act
            var response = await client.GetAsync(interceptUri, TestContext!.CancellationTokenSource.Token);
            // Assert
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, "expected a successful response from the proxy server.");
            response.Content.Headers.ContentType?.MediaType.Should().Be("text/html", "expected HTML content type.");
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("<h1>Intercepted Response</h1>", "expected the response to contain the intercepted content.");
        }
        finally
        {
            _proxyRunner.ProxyServerEvents.OnRequest -= handler;
        }
    }

    [TestMethod]
    [Ignore("Need to clean up this test")]
    public async Task Http_request_should_be_modified ()
    {
        // Arrange: Start a local test server
        var builder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.Configure(app =>
                {
                    app.Run(async context =>
                    {
                        if (context.Request.Path == "/test-response")
                        {
                            context.Response.ContentType = "text/html";
                            await context.Response.WriteAsync("<html><body>TestServer Content</body></html>");
                        }
                        else
                        {
                            context.Response.StatusCode = 404;
                        }
                    });
                });
            });
        using var host = await builder.StartAsync();
        var testServer = host.GetTestServer();
        var testServerUri = testServer.BaseAddress + "test-response";

        var client = _proxyRunner!.CreateHttpClient();
        var interceptUri = "http://fake.svrooij.io/modify";
        AsyncEventHandler<RequestEventArguments, RequestEventResponse> handler = async ( sender, args, cancellationToken ) =>
        {
            if (args.Request.RequestUri!.ToString() == interceptUri)
            {
                return RequestEventResponse.ModifyRequest(new HttpRequestMessage(HttpMethod.Get, testServerUri));
            }
            return RequestEventResponse.ContinueResponse();
        };
        _proxyRunner.ProxyServerEvents.OnRequest += handler;
        try
        {
            // Act
            var response = await client.GetAsync(interceptUri, TestContext!.CancellationTokenSource.Token);
            // Assert
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, "expected a successful response from the proxy server.");
            response.Content.Headers.ContentType?.MediaType.Should().Be("text/html", "expected HTML content type.");
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("TestServer Content", "expected the response to contain result for modified request.");
        }
        finally
        {
            _proxyRunner.ProxyServerEvents.OnRequest -= handler;
        }
    }

    [TestMethod]
    public async Task Https_request_should_passthrough_without_intercepting ()
    {
        // Arrange
        var client = _proxyRunner!.CreateHttpClient();
        var requestUri = "https://svrooij.io/";
        _proxyRunner.ProxyServerEvents.ClearEvents();
        _proxyRunner.ProxyServerEvents.ShouldDecryptNewConnection = ( host, details, ct ) =>
        {
            return Task.FromResult(false);
        };
        // Act
        var response = await client.GetAsync(requestUri, TestContext!.CancellationTokenSource.Token);
        // Assert
        response.Should().NotBeNull("expected a response from the proxy server.");
        response.IsSuccessStatusCode.Should().BeTrue("expected a successful response from the proxy server.");
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html", "expected HTML content type.");
        var content = await response.Content.ReadAsStringAsync(TestContext!.CancellationTokenSource.Token);
        content.Should().NotBeNullOrEmpty("expected the response to be non-empty.");
        content.Should().Contain("Stephan van Rooij", "expected the response to contain the author's name.");
    }

}
