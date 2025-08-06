using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Unobtanium.Web.Proxy.IntegrationTests.Helpers;
using Unobtanium.Web.Proxy.IntegrationTests.Setup;

namespace Unobtanium.Web.Proxy.IntegrationTests;

public class TestSuite : IDisposable
{
    private readonly TestServer server;
    private IHttpClientFactory? httpClientFactory;

    public TestSuite(bool requireMutualTls = false, ProxyServerConfiguration? proxyServerConfiguration = null)
    {
        var dummyProxy = new ProxyServer(proxyServerConfiguration);
        var serverCertificate = dummyProxy.CertificateManager.GetOrGenerateCertificateAsync("localhost").Result;
        server = new TestServer(serverCertificate, requireMutualTls);
    }

    public TestServer GetServer()
    {
        return server;
    }

    public ProxyServer GetProxy(ProxyServer upStreamProxy = null, ProxyServerConfiguration? proxyServerConfiguration = null, IProxyServerHttpClientFactory? proxyServerHttpClientFactory = null)
    {
        TestProxyServer testProxy = upStreamProxy != null
            ? new TestProxyServer(false, upStreamProxy, proxyServerConfiguration, proxyServerHttpClientFactory)
            : new TestProxyServer(false, proxyServerConfiguration: proxyServerConfiguration, proxyServerHttpClientFactory: proxyServerHttpClientFactory);

        if (httpClientFactory == null)
        {
            httpClientFactory = new ServiceCollection()
                .AddHttpClient()
                .ConfigureHttpClientDefaults(http =>
                {
                    http.ConfigurePrimaryHttpMessageHandler(() =>
                    {
                        return new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                            Proxy = new TestHelper.TestProxy($"http://localhost:{testProxy.ProxyServer.ProxyEndPoints[0].Port}", false),
                        };
                    });
                })
                .BuildServiceProvider()
                .GetRequiredService<IHttpClientFactory>();
        }
        
        return testProxy.ProxyServer;
    }

    public ProxyServer GetProxyWithHandler(ProxyServer upStreamProxy = null, Events.AsyncEventHandler<Events.RequestEventArguments, Events.RequestEventResponse>? OnRequest = null, IProxyServerHttpClientFactory? proxyServerHttpClientFactory = null)
    {
        var config = new ProxyServerConfiguration();
        config.Events.OnRequest += OnRequest;
        return GetProxy(upStreamProxy, config, proxyServerHttpClientFactory);
    }

    public ProxyServer GetReverseProxy(ProxyServer upStreamProxy = null, ProxyServerConfiguration? proxyServerConfiguration = null, Events.AsyncEventHandler<Events.RequestEventArguments, Events.RequestEventResponse> ? onRequest = null)
    {
        var config = proxyServerConfiguration ?? new ProxyServerConfiguration();
        if (onRequest != null)
        {
            config.Events.OnRequest += onRequest;
        }
        if (upStreamProxy != null)
        {
            return new TestProxyServer(true, upStreamProxy, config).ProxyServer;
        }

        return new TestProxyServer(true, proxyServerConfiguration: config).ProxyServer;
    }

    public HttpClient GetClient(ProxyServer proxyServer, bool? enableBasicProxyAuthorization = false)
    {
        if (httpClientFactory == null || enableBasicProxyAuthorization == true)
        {
            TestHelper.GetHttpClient(proxyServer.ProxyEndPoints[0].Port, enableBasicProxyAuthorization == true);
        }
        // Most efficient way to create a client with the factory, no support for basic auth here
        return httpClientFactory.CreateClient();
    }

    public HttpClient GetReverseProxyClient()
    {
        return TestHelper.GetHttpClient();
    }

    public void Dispose()
    {
        httpClientFactory = null;
    }
}
