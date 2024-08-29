using System.Net.Http;
using Unobtanium.Web.Proxy.IntegrationTests.Helpers;
using Unobtanium.Web.Proxy.IntegrationTests.Setup;

namespace Unobtanium.Web.Proxy.IntegrationTests;

public class TestSuite
{
    private readonly TestServer server;

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

    public ProxyServer GetProxy(ProxyServer upStreamProxy = null, ProxyServerConfiguration? proxyServerConfiguration = null)
    {
        if (upStreamProxy != null)
        {
            return new TestProxyServer(false, upStreamProxy, proxyServerConfiguration).ProxyServer;
        }

        return new TestProxyServer(false, proxyServerConfiguration: proxyServerConfiguration).ProxyServer;
    }

    public ProxyServer GetReverseProxy(ProxyServer upStreamProxy = null, ProxyServerConfiguration? proxyServerConfiguration = null)
    {
        if (upStreamProxy != null)
        {
            return new TestProxyServer(true, upStreamProxy, proxyServerConfiguration).ProxyServer;
        }

        return new TestProxyServer(true, proxyServerConfiguration: proxyServerConfiguration).ProxyServer;
    }

    public HttpClient GetClient(ProxyServer proxyServer, bool enableBasicProxyAuthorization = false)
    {
        return TestHelper.GetHttpClient(proxyServer.ProxyEndPoints[0].Port, enableBasicProxyAuthorization);
    }

    public HttpClient GetReverseProxyClient()
    {
        return TestHelper.GetHttpClient();
    }
}
