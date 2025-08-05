# Unobtanium Web Proxy

A lightweight HTTP(S) proxy server written in C# `NET8.0`.

[![Unobtanium web proxy][badge_twp-repo]][link_twp-repo]
[![nuget][badge_nuget]][link_nuget]
[![github issues][badge_issues]][link_issues]
[![License][badle_license]][link_license]
[![Build status][badge_twp_build]][link_build]
[![Support me on Github][badge_sponsor]][link_sponsor]

Report bugs or raise issues here.

## Project reboot

[![Unobtanium web proxy][badge_twp-repo]][link_twp-repo]

This project is a reboot of the original [Titanium-Web-Proxy](https://github.com/justcoding121/titanium-web-proxy) project. The original project was last updated two years ago, has been archived by the author on July 9th 2023 and has been inactive since then. This project aims to continue the development of the original project and provide a stable and reliable proxy server library for .NET developers.

[Announcement](https://github.com/svrooij/titanium-web-proxy/discussions/2) [Reboot discussion](https://github.com/svrooij/titanium-web-proxy/discussions/7) [Issues](https://github.com/svrooij/titanium-web-proxy/issues?q=is%3Aissue+is%3Aopen+label%3Areboot)

### Reboot focus

* `net8.0` only (no support for older versions of .NET!)
* Support for `ILogger` [See #4](https://github.com/svrooij/titanium-web-proxy/issues/4)
* Support for diagnostic events using `ActivitySource` and `Activity` [See #3](https://github.com/svrooij/titanium-web-proxy/issues/3)
* Using the latest .NET features like `Span<T>` and `Memory<T>` to improve performance
* Update dependencies to the latest versions
* `TLS 1.2` and `TLS 1.3` only support
* **Modern Event System:** Event-handlers with `HttpRequestMessage` and `HttpResponseMessage`, to greatly improve the portability of the library [See #6](https://github.com/svrooij/titanium-web-proxy/issues/6)
* `HttpClient` as the default client, and using the IHttpClientFactory to handle pooling of the clients
* Testing, testing, testing!

## Modern Event System

This proxy server uses a modern, clean event system based on standard `HttpRequestMessage` and `HttpResponseMessage` objects, making it easier to integrate with existing .NET HTTP libraries and patterns.

### Request Interception

```csharp
var config = new ProxyServerConfiguration();
config.Events.OnRequest += async (sender, e, cancellationToken) =>
{
    Console.WriteLine($"Request: {e.Request.Method} {e.Request.RequestUri}");
    
    // Block specific domains
    if (e.Request.RequestUri?.Host.Contains("blocked.com") == true)
    {
        var blockedResponse = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("Access Denied")
        };
        return RequestEventResponse.EarlyResponse(blockedResponse);
    }
    
    // Modify request headers
    e.Request.Headers.Add("X-Proxy-Agent", "Unobtanium");
    
    // Redirect requests
    if (e.Request.RequestUri?.Host.Contains("example.com") == true)
    {
        var redirectedRequest = new HttpRequestMessage(e.Request.Method, "https://microsoft.com")
        {
            Content = e.Request.Content,
            Version = e.Request.Version
        };
        
        // Copy headers
        foreach (var header in e.Request.Headers)
        {
            redirectedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        
        return RequestEventResponse.ModifyRequest(redirectedRequest);
    }
    
    return RequestEventResponse.ContinueResponse();
};
```

### Response Interception

```csharp
config.Events.OnResponse += async (sender, e, cancellationToken) =>
{
    Console.WriteLine($"Response: {e.Response.StatusCode} from {e.Request.RequestUri}");
    
    // Modify responses
    if (e.Response.Content != null)
    {
        var content = await e.Response.Content.ReadAsStringAsync(cancellationToken);
        if (content.Contains("error"))
        {
            var modifiedResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content.Replace("error", "success")),
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
```

## Features

* ~~[API Documentation](https://justcoding121.github.io/titanium-web-proxy/docs/api/Titanium.Web.Proxy.ProxyServer.html)~~
* ~~[Wiki & Contribution guidelines](https://github.com/justcoding121/Titanium-Web-Proxy/wiki)~~
* Multithreaded and asynchronous proxy employing server connection pooling, certificate cache, and buffer pooling
* View, modify, redirect and block requests or responses
* Modern event system based on `HttpRequestMessage` and `HttpResponseMessage`
* Supports mutual SSL authentication, proxy authentication & automatic upstream proxy detection
* Supports kerberos, NTLM authentication over HTTP protocols on windows domain controlled networks
* SOCKS4/5 Proxy support

## Installation

Package on [NuGet][link_nuget], `Unobtanium.Web.Proxy` will be a partial drop-in replacement for `Titanium.Web.Proxy`, if you're on `NET8.0 or higher`.

```bash
dotnet add package Unobtanium.Web.Proxy
```

Supports

* `.NET 8.0` and above

As stated [above](#project-reboot), this project is a reboot of the original project. Expect things to change, everything marked as `obsolete` in the original project will be removed in this project. And until this is `v1.0.0`, expect [breaking changes](#reboot-focus).

## Usage

```csharp
using Unobtanium.Web.Proxy;
using Microsoft.Extensions.DependencyInjection;

// Use dependency injection to create the proxy server (this will be the preferred way to create the proxy server in the future)
// ActivitySource (for tracing) and ILogger are optional, but recommended
// Since the ProxyServer has to be a singleton, you have to register the configuration as a singleton as well
services.AddSingleton<ProxyServerConfiguration>(...);
services.AddSingleton<ProxyServer>();

// Get the proxy server from the service provider
var proxyServer = provider.GetRequiredService<ProxyServer>();
```

### Complete Example

```csharp
var config = new ProxyServerConfiguration();

// Handle requests with HttpRequestMessage
config.Events.OnRequest += async (sender, e, cancellationToken) =>
{
    Console.WriteLine($"Request: {e.Request.Method} {e.Request.RequestUri}");
    
    // Block specific domains
    if (e.Request.RequestUri?.Host.Contains("blocked.com") == true)
    {
        var blockedResponse = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("Access Denied")
        };
        return RequestEventResponse.EarlyResponse(blockedResponse);
    }
    
    // Modify request headers
    e.Request.Headers.Add("X-Proxy-Agent", "Unobtanium");
    
    return RequestEventResponse.ContinueResponse();
};

// Handle responses with HttpResponseMessage  
config.Events.OnResponse += async (sender, e, cancellationToken) =>
{
    Console.WriteLine($"Response: {e.Response.StatusCode} from {e.Request.RequestUri}");
    return ResponseEventResponse.ContinueResponse();
};

var proxyServer = new ProxyServer(config);

// Add endpoints
var explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Any, 8000, true);
proxyServer.AddEndPoint(explicitEndPoint);

// Start the proxy
await proxyServer.StartAsync();

Console.WriteLine($"Proxy listening on {explicitEndPoint.IpAddress}:{explicitEndPoint.Port}");

// Set as system proxy
proxyServer.SetAsSystemProxy(explicitEndPoint, ProxyProtocolType.AllHttp);

// Stop when done
proxyServer.Stop();
```

## Collaborators

The owner of this project, [justcoding121](https://github.com/justcoding121), is considered to be inactive from this project due to his busy work schedule. See [project reboot](#project-reboot) for more information.

Previous contributors:

* [justcoding121](https://github.com/justcoding121) *owner*
* [honfika](https://github.com/honfika)

Current contributors:

* [svrooij](https://github.com/svrooij)

You contributions are more then welcome! Let's make this project great again!

## Development environment

Since this is a `dotnet` project I would suggest to use `Visual Studio 2022` or `Visual Studio Code` as your development environment. The project is set up to use the `dotnet` CLI, so you can also use that to build and run the project.

### Console example application screenshot

![alt tag](https://raw.githubusercontent.com/svrooij/Titanium-Web-Proxy/develop/examples/Titanium.Web.Proxy.Examples.Basic/Capture.PNG)

### GUI example application screenshot

![alt tag](https://raw.githubusercontent.com/svrooij/Titanium-Web-Proxy/develop/examples/Titanium.Web.Proxy.Examples.Wpf/Capture.PNG)

[badge_issues]: https://img.shields.io/github/issues/svrooij/titanium-web-proxy?style=for-the-badge
[badle_license]: https://img.shields.io/github/license/svrooij/titanium-web-proxy?style=for-the-badge
[badge_nuget]: https://img.shields.io/nuget/v/Unobtanium.Web.Proxy?style=for-the-badge
[badge_sponsor]: https://img.shields.io/github/sponsors/svrooij?style=for-the-badge&logo=github
[badge_twp-repo]: https://img.shields.io/badge/Unobtanium--Web--Proxy-Reboot-blue?style=for-the-badge
[badge_twp_build]: https://img.shields.io/github/check-runs/svrooij/titanium-web-proxy/develop?style=for-the-badge

[link_build]: https://github.com/svrooij/titanium-web-proxy/actions/workflows/dotnetcore.yml
[link_issues]: https://github.com/svrooij/titanium-web-proxy/issues
[link_license]: https://github.com/svrooij/titanium-web-proxy?tab=MIT-1-ov-file
[link_nuget]: https://www.nuget.org/packages/Unobtanium.Web.Proxy
[link_twp-repo]: https://github.com/svrooij/titanium-web-proxy
[link_sponsor]: https://github.com/sponsors/svrooij
