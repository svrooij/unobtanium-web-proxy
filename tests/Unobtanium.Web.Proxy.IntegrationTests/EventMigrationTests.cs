using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Events;

namespace Unobtanium.Web.Proxy.IntegrationTests;

/// <summary>
/// Tests to verify that the new event system works correctly
/// and both old and new event systems can coexist during migration
/// </summary>
[TestClass]
public class EventMigrationTests
{
    [TestMethod, Timeout(10000)]
    public async Task NewEventSystem_CanBlockRequests()
    {
        var testSuite = new TestSuite();
        var server = testSuite.GetServer();
        server.HandleRequest(context =>
        {
            return context.Response.WriteAsync("Server response");
        });

        // Use the new event system to block requests
        var config = new ProxyServerConfiguration();
        config.Events.OnRequest += (sender, e, cancellationToken) =>
        {
            if (e.Request.RequestUri?.Host.Contains("blocked.com") == true)
            {
                var blockedResponse = new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("Access Denied by New Event System")
                };
                return Task.FromResult(RequestEventResponse.EarlyResponse(blockedResponse));
            }
            return Task.FromResult(RequestEventResponse.ContinueResponse());
        };

        var proxy = new ProxyServer(config);
        proxy.AddEndPoint(new Models.ExplicitProxyEndPoint(IPAddress.Any, 0));
        await proxy.StartAsync();

        var client = testSuite.GetClient(proxy);

        // This request should be blocked
        var blockedResponse = await client.GetAsync("http://blocked.com/test");
        Assert.AreEqual(HttpStatusCode.Forbidden, blockedResponse.StatusCode);
        var blockedContent = await blockedResponse.Content.ReadAsStringAsync();
        Assert.AreEqual("Access Denied by New Event System", blockedContent);

        // This request should go through
        var allowedResponse = await client.GetAsync(server.ListeningHttpUrl);
        Assert.AreEqual(HttpStatusCode.OK, allowedResponse.StatusCode);
        var allowedContent = await allowedResponse.Content.ReadAsStringAsync();
        Assert.AreEqual("Server response", allowedContent);

        proxy.Stop();
    }

    [TestMethod, Timeout(10000)]
    public async Task NewEventSystem_CanModifyRequests()
    {
        var testSuite = new TestSuite();
        var server = testSuite.GetServer();
        server.HandleRequest(context =>
        {
            // Echo back the custom header
            var customHeader = context.Request.Headers["X-Modified-By"];
            return context.Response.WriteAsync($"Header: {customHeader}");
        });

        // Use the new event system to modify requests
        var config = new ProxyServerConfiguration();
        config.Events.OnRequest += (sender, e, cancellationToken) =>
        {
            // Add a custom header to show the request was modified
            e.Request.Headers.Add("X-Modified-By", "NewEventSystem");
            return Task.FromResult(RequestEventResponse.ContinueResponse());
        };

        var proxy = new ProxyServer(config);
        proxy.AddEndPoint(new Models.ExplicitProxyEndPoint(IPAddress.Any, 0));
        await proxy.StartAsync();

        var client = testSuite.GetClient(proxy);

        var response = await client.GetAsync(server.ListeningHttpUrl);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(content.Contains("NewEventSystem"), "Request should have been modified by new event system");

        proxy.Stop();
    }

    [TestMethod, Timeout(10000)] 
    public async Task NewEventSystem_CanRedirectRequests()
    {
        var testSuite = new TestSuite();
        var targetServer = testSuite.GetServer();
        targetServer.HandleRequest(context =>
        {
            return context.Response.WriteAsync("Redirected Server Response");
        });

        // Use the new event system to redirect requests
        var config = new ProxyServerConfiguration();
        config.Events.OnRequest += (sender, e, cancellationToken) =>
        {
            if (e.Request.RequestUri?.Host.Contains("redirect.com") == true)
            {
                // Create a new request to the target server
                var redirectedRequest = new HttpRequestMessage(e.Request.Method, targetServer.ListeningHttpUrl)
                {
                    Version = e.Request.Version
                };
                
                // Copy headers
                foreach (var header in e.Request.Headers)
                {
                    redirectedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                
                return Task.FromResult(RequestEventResponse.ModifyRequest(redirectedRequest));
            }
            return Task.FromResult(RequestEventResponse.ContinueResponse());
        };

        var proxy = new ProxyServer(config);
        proxy.AddEndPoint(new Models.ExplicitProxyEndPoint(IPAddress.Any, 0));
        await proxy.StartAsync();

        var client = testSuite.GetClient(proxy);

        // Request to redirect.com should be redirected to our target server
        var response = await client.GetAsync("http://redirect.com/test");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("Redirected Server Response", content);

        proxy.Stop();
    }

    [TestMethod, Timeout(10000)]
    public async Task NewEventSystem_CanHandleResponseModification()
    {
        var testSuite = new TestSuite();
        var server = testSuite.GetServer();
        server.HandleRequest(context =>
        {
            return context.Response.WriteAsync("Original Server Response");
        });

        var config = new ProxyServerConfiguration();
        bool responseEventCalled = false;

        // Set up response modification
        config.Events.OnResponse += (sender, e, cancellationToken) =>
        {
            responseEventCalled = true;
            
            // Modify the response
            var modifiedResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Modified by New Event System"),
                Version = e.Response.Version
            };
            
            // Copy headers
            foreach (var header in e.Response.Headers)
            {
                modifiedResponse.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            
            return Task.FromResult(Events.ResponseEventResponse.ModifyResponse(modifiedResponse));
        };

        var proxy = new ProxyServer(config);
        proxy.AddEndPoint(new Models.ExplicitProxyEndPoint(IPAddress.Any, 0));
        await proxy.StartAsync();

        var client = testSuite.GetClient(proxy);

        var response = await client.GetAsync(server.ListeningHttpUrl);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        
        Assert.IsTrue(responseEventCalled, "Response event should have been called");
        Assert.AreEqual("Modified by New Event System", content);

        proxy.Stop();
    }

    [TestMethod, Timeout(10000)]
    public async Task ModernEventSystem_PerformanceAndCleanAPI()
    {
        var testSuite = new TestSuite();
        var server = testSuite.GetServer();
        server.HandleRequest(context =>
        {
            var customHeader = context.Request.Headers["X-Custom"];
            return context.Response.WriteAsync($"Custom: {customHeader}");
        });

        var config = new ProxyServerConfiguration();
        var requestCount = 0;

        // Demonstrate clean, modern API
        config.Events.OnRequest += (sender, e, cancellationToken) =>
        {
            requestCount++;
            
            // Standard HttpRequestMessage API
            e.Request.Headers.Add("X-Custom", $"Request-{requestCount}");
            
            return Task.FromResult(RequestEventResponse.ContinueResponse());
        };

        var proxy = new ProxyServer(config);
        proxy.AddEndPoint(new Models.ExplicitProxyEndPoint(IPAddress.Any, 0));
        await proxy.StartAsync();

        var client = testSuite.GetClient(proxy);

        var response = await client.GetAsync(server.ListeningHttpUrl);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        
        Assert.IsTrue(content.Contains("Request-1"), "Custom header should contain request number");
        Assert.AreEqual(1, requestCount, "Request should have been processed by modern event system");

        proxy.Stop();
    }
}
