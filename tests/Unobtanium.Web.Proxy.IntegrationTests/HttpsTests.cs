using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Unobtanium.Web.Proxy.IntegrationTests;

[TestClass]
public class HttpsTests
{
    [TestMethod, Timeout(120_000)]
    public async Task Can_Handle_Https_Request()
    {
        var testSuite = new TestSuite();

        var server = testSuite.GetServer();
        server.HandleRequest(context =>
        {
            return context.Response.WriteAsync("I am server. I received your greetings.");
        });

        var proxy = testSuite.GetProxy();
        var client = testSuite.GetClient(proxy);

        var response = await client.PostAsync(new Uri(server.ListeningHttpsUrl),
            new StringContent("hello server. I am a client."));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        Assert.AreEqual("I am server. I received your greetings.", body);
    }

    [TestMethod, Timeout(10000)]
    public async Task Can_Handle_Https_Fake_Tunnel_Request()
    {
        var testSuite = new TestSuite();

        var server = testSuite.GetServer();
        server.HandleRequest(context =>
        {
            return context.Response.WriteAsync("I am server. I received your greetings.");
        });
        var config = new ProxyServerConfiguration();
        config.Events.OnRequest += async (sender, e, cancellationToken) =>
        {
            // This is a fake tunnel request, we need to set the URL to the server's listening URL
            e.Request.RequestUri = new Uri(server.ListeningHttpsUrl);
            var newRequest = new HttpRequestMessage(e.Request.Method, server.ListeningHttpsUrl)
            {
                Content = e.Request.Content
            };
            return Events.RequestEventResponse.ModifyRequest(newRequest);
        };

        var proxy = testSuite.GetProxy(proxyServerConfiguration: config);

        var client = testSuite.GetClient(proxy);

        var response = await client.PostAsync(new Uri($"https://{Guid.NewGuid()}.com"),
            new StringContent("hello server. I am a client."));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        Assert.AreEqual("I am server. I received your greetings.", body);
    }

    [TestMethod, Timeout(30_000)]
    [Ignore("Using client certificates is quite hard with the new HttpClient, have to look into this.")]
    public async Task Can_Handle_Https_Mutual_Tls_Request()
    {
        var testSuite = new TestSuite(true);

        var server = testSuite.GetServer();
        server.HandleRequest(context =>
        {
            return context.Response.WriteAsync("I am server. I received your greetings.");
        });
        var certs = new CertificateProxyHttpClientFactory();
        var proxy = testSuite.GetProxy(proxyServerHttpClientFactory: certs);

        var clientCert = await proxy.CertificateManager.GetCertificateFromDiskOrGenerateAsync("client.com", false);

        certs.AddClientCertificate(clientCert);

        var client = testSuite.GetClient(proxy);

        var response = await client.PostAsync(new Uri(server.ListeningHttpsUrl),
            new StringContent("hello server. I am a client."));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        Assert.AreEqual("I am server. I received your greetings.", body);
    }

    internal class CertificateProxyHttpClientFactory : IProxyServerHttpClientFactory
    {
        private readonly X509CertificateCollection _clientCertificates;
#nullable enable
        public CertificateProxyHttpClientFactory(X509CertificateCollection? clientCertificates = null)
        {
            _clientCertificates = clientCertificates ?? new X509CertificateCollection();
        }

        public void AddClientCertificate(X509Certificate certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));
            _clientCertificates.Add(certificate);
        }

        public HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                UseProxy = false, // Explicitly disable proxy usage
                Proxy = null,
            };

            // If client certificates are provided, add them to the handler
            if (_clientCertificates != null && _clientCertificates.Count > 0)
            {
                handler.ClientCertificates.AddRange(_clientCertificates);
            }
            return new HttpClient(handler);
        }
    }
}
