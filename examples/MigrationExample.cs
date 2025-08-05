using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy;
using Unobtanium.Web.Proxy.EventArguments;
using Unobtanium.Web.Proxy.Events;
using Unobtanium.Web.Proxy.Models;

namespace Unobtanium.Web.Proxy.Examples.Migration;

/// <summary>
/// Example showing how to migrate from the old BeforeRequest event system 
/// to the new Events system in ProxyServerConfiguration
/// </summary>
public class MigrationExample
{
    /// <summary>
    /// OLD WAY - Using deprecated BeforeRequest event
    /// </summary>
    public void OldEventSystemExample()
    {
        var proxyServer = new ProxyServer(new ProxyServerConfiguration());
        
        // THIS IS DEPRECATED - Don't use this approach
        #pragma warning disable CS0618 // Type or member is obsolete
        proxyServer.BeforeRequest += async (sender, e) =>
        {
            // Log the request
            Console.WriteLine($"OLD: Request to {e.HttpClient.Request.Url}");
            
            // Block requests to google.com
            if (e.HttpClient.Request.RequestUri.Host.Contains("google.com"))
            {
                e.Ok("<!DOCTYPE html><html><body><h1>Blocked</h1></body></html>");
                return;
            }
            
            // Modify headers
            e.HttpClient.Request.Headers.AddHeader("X-Custom-Header", "OldValue");
            
            // Redirect requests
            if (e.HttpClient.Request.RequestUri.Host.Contains("example.com"))
            {
                e.Redirect("https://microsoft.com");
            }
        };
        #pragma warning restore CS0618 // Type or member is obsolete
    }

    /// <summary>
    /// NEW WAY - Using the new Events system with HttpRequestMessage/HttpResponseMessage
    /// </summary>
    public void NewEventSystemExample()
    {
        var config = new ProxyServerConfiguration();
        
        // NEW APPROACH - Use the new event system
        config.Events.OnRequest += async (sender, e, cancellationToken) =>
        {
            // Log the request
            Console.WriteLine($"NEW: Request to {e.Request.RequestUri}");
            
            // Block requests to google.com with early response
            if (e.Request.RequestUri?.Host.Contains("google.com") == true)
            {
                var blockedResponse = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<!DOCTYPE html><html><body><h1>Blocked</h1></body></html>", 
                                              System.Text.Encoding.UTF8, "text/html")
                };
                return RequestEventResponse.EarlyResponse(blockedResponse);
            }
            
            // Modify the request
            var modifiedRequest = e.Request;
            
            // Add custom headers
            modifiedRequest.Headers.Add("X-Custom-Header", "NewValue");
            
            // Redirect requests by modifying the URI
            if (e.Request.RequestUri?.Host.Contains("example.com") == true)
            {
                var redirectRequest = new HttpRequestMessage(e.Request.Method, "https://microsoft.com")
                {
                    Content = e.Request.Content,
                    Version = e.Request.Version
                };
                
                // Copy headers from original request
                foreach (var header in e.Request.Headers)
                {
                    redirectRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                
                return RequestEventResponse.ModifyRequest(redirectRequest);
            }
            
            // Continue with normal processing
            return RequestEventResponse.ContinueResponse();
        };

        var proxyServer = new ProxyServer(config);
    }

    /// <summary>
    /// Advanced example showing request/response body modification
    /// </summary>
    public void AdvancedMigrationExample()
    {
        var config = new ProxyServerConfiguration();
        
        config.Events.OnRequest += async (sender, e, cancellationToken) =>
        {
            // Modify request body if present
            if (e.Request.Content != null)
            {
                var body = await e.Request.Content.ReadAsStringAsync(cancellationToken);
                if (body.Contains("oldValue"))
                {
                    var newBody = body.Replace("oldValue", "newValue");
                    var modifiedRequest = new HttpRequestMessage(e.Request.Method, e.Request.RequestUri)
                    {
                        Content = new StringContent(newBody, System.Text.Encoding.UTF8, "application/json"),
                        Version = e.Request.Version
                    };
                    
                    // Copy headers
                    foreach (var header in e.Request.Headers)
                    {
                        modifiedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                    
                    return RequestEventResponse.ModifyRequest(modifiedRequest);
                }
            }
            
            return RequestEventResponse.ContinueResponse();
        };
        
        config.Events.OnResponse += async (sender, e, cancellationToken) =>
        {
            // Modify response (for logging, caching, etc.)
            Console.WriteLine($"Response: {e.Response.StatusCode} from {e.Request.RequestUri}");
            
            // In the new system, response modification returns a modified response
            if (e.Response.Content != null)
            {
                var body = await e.Response.Content.ReadAsStringAsync(cancellationToken);
                if (body.Contains("error"))
                {
                    var modifiedResponse = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(body.Replace("error", "success"), 
                                                  System.Text.Encoding.UTF8, "application/json"),
                        Version = e.Response.Version
                    };
                    
                    // Copy headers
                    foreach (var header in e.Response.Headers)
                    {
                        modifiedResponse.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                    
                    return ResponseEventResponse.ModifyResponse(modifiedResponse);
                }
            }
            
            return ResponseEventResponse.ContinueResponse();
        };

        var proxyServer = new ProxyServer(config);
    }

    /// <summary>
    /// Complete example with HTTPS endpoint setup
    /// </summary>
    public async Task CompleteExampleAsync()
    {
        var config = new ProxyServerConfiguration();
        
        // Configure request handling
        config.Events.OnRequest += async (sender, e, cancellationToken) =>
        {
            Console.WriteLine($"Request: {e.Request.Method} {e.Request.RequestUri}");
            return RequestEventResponse.ContinueResponse();
        };
        
        // Configure response handling  
        config.Events.OnResponse += async (sender, e, cancellationToken) =>
        {
            Console.WriteLine($"Response: {e.Response.StatusCode}");
            return ResponseEventResponse.ContinueResponse();
        };

        var proxyServer = new ProxyServer(config);
        
        // Set up endpoints
        var explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Any, 8000, true);
        proxyServer.AddEndPoint(explicitEndPoint);
        
        // Start the proxy
        await proxyServer.StartAsync();
        
        Console.WriteLine($"Proxy listening on {explicitEndPoint.IpAddress}:{explicitEndPoint.Port}");
        
        // Wait for shutdown signal
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            proxyServer.Stop();
        };
    }
}

/// <summary>
/// Migration mapping guide for common scenarios
/// </summary>
public static class MigrationGuide
{
    /// <summary>
    /// Maps old SessionEventArgs operations to new event responses
    /// </summary>
    public static class CommonMigrations
    {
        /*
         * OLD: e.Ok(content)
         * NEW: return RequestEventResponse.EarlyResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) });
         * 
         * OLD: e.Redirect(url)  
         * NEW: return RequestEventResponse.EarlyResponse(new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri(url) } });
         * 
         * OLD: e.HttpClient.Request.Url = newUrl
         * NEW: return RequestEventResponse.ModifyRequest(new HttpRequestMessage(e.Request.Method, newUrl) { ... });
         * 
         * OLD: e.HttpClient.Request.Headers.AddHeader(name, value)
         * NEW: modifiedRequest.Headers.Add(name, value); return RequestEventResponse.ModifyRequest(modifiedRequest);
         * 
         * OLD: var body = await e.GetRequestBody()
         * NEW: var body = await e.Request.Content.ReadAsByteArrayAsync()
         * 
         * OLD: e.SetRequestBody(body)
         * NEW: modifiedRequest.Content = new ByteArrayContent(body); return RequestEventResponse.ModifyRequest(modifiedRequest);
         */
    }
}
