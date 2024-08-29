using System;
using System.Net;
using Unobtanium.Web.Proxy.Certificates;
using Unobtanium.Web.Proxy.Models;

namespace Unobtanium.Web.Proxy.IntegrationTests.Setup;

public class TestProxyServer : IDisposable
{
    public TestProxyServer(bool isReverseProxy, ProxyServer upStreamProxy = null, ProxyServerConfiguration? proxyServerConfiguration = null)
    {
        ProxyServer = new ProxyServer(proxyServerConfiguration);

        var explicitEndPoint = isReverseProxy
            ? (ProxyEndPoint)new TransparentProxyEndPoint(IPAddress.Any, 0)
            : new ExplicitProxyEndPoint(IPAddress.Any, 0);

        ProxyServer.AddEndPoint(explicitEndPoint);

        if (upStreamProxy != null)
        {
            ProxyServer.UpStreamHttpProxy = new ExternalProxy("localhost", upStreamProxy.ProxyEndPoints[0].Port);
            ProxyServer.UpStreamHttpsProxy = new ExternalProxy("localhost", upStreamProxy.ProxyEndPoints[0].Port);
        }

        ProxyServer.Start();
    }

    public ProxyServer ProxyServer { get; }

    public int ListeningPort => ProxyServer.ProxyEndPoints[0].Port;

    public CertificateManager CertificateManager => ProxyServer.CertificateManager;

    public void Dispose()
    {
        ProxyServer.Stop();
        ProxyServer.Dispose();
    }
}
