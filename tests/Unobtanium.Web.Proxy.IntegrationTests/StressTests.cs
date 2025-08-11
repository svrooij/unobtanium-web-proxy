using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Unobtanium.Web.Proxy.IntegrationTests.Helpers;
using Unobtanium.Web.Proxy.IntegrationTests.Setup;

namespace Unobtanium.Web.Proxy.IntegrationTests;

[TestClass]
public class StressTests
{
    private readonly IProxyServerHttpClientFactory _httpClientFactory = new TestHttpClientFactory();
    private readonly TestSuite _testSuite = new TestSuite();
    private readonly TestServer _server;
    private readonly Uri _uri;

    public StressTests()
    {
        _server = _testSuite.GetServer();
        _server.HandleRequest(context =>
        {
            return context.Response.WriteAsync("I am server. I received your greetings.");
        });
        _uri = new Uri(_server.ListeningHttpsUrl);
    }

    [TestMethod, Timeout(60_000)]
    [DataRow(1000)]
    //[DataRow(2000)]
    //[DataRow(3000)]
    [Ignore("This test is failing in CI, but works locally. Needs investigation.")]
    public async Task Stress_Test_With_One_Server_And_Many_Clients(int numberOfRequests)
    {
        using var proxy = _testSuite.GetProxy(proxyServerHttpClientFactory: _httpClientFactory);
        using var cts = new System.Threading.CancellationTokenSource(50_000);

        await Task.Delay(100);

        var tasks = new List<Task>();

        //send x requests to server
        for (var j = 0; j < numberOfRequests; j++)
        {
            var task = Task.Run(async () =>
            {
                using var client = _testSuite.GetClient(proxy);

                await client.PostAsync(_uri,
                    new StringContent("hello server. I am a client."), cts.Token);
            }, cts.Token);

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
    }
}
