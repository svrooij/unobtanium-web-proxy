# Titanium Web Proxy

A lightweight HTTP(S) proxy server written in C# `NET8.0`.

[![Titanium web proxy][badge_twp-repo]][link_twp-repo]
[![github issues][badge_issues]][link_issues]
[![Build status][badge_twp_build]][link_build]
[![Support me on Github][badge_sponsor]][link_sponsor]

Report bugs or raise issues here.

## Project reboot

[![Titanium web proxy][badge_twp-repo]][link_twp-repo]

This project is a reboot of the original [Titanium-Web-Proxy](https://github.com/justcoding121/titanium-web-proxy) project. The original project was last updated two years ago, has been archived by the author on July 9th 2023 and has been inactive since then. This project aims to continue the development of the original project and provide a stable and reliable proxy server library for .NET developers.

Documentation and nuget packages will be released soon.

### Reboot focus

* `net8.0` only (no support for older versions of .NET!)
* Support for `ILogger`
* Support for diagnostic events using `ActivitySource` and `Activity`
* Using the latest .NET features like `Span<T>` and `Memory<T>` to improve performance
* Update dependencies to the latest versions
* `TLS 1.2` and `TLS 1.3` only support
* Event-handlers with `HttpRequestMessage` and `HttpResponseMessage`, to greatly improve the portability of the library
* `HttpClient` as the default client, and using the IHttpClientFactory to handle pooling of the clients
* Testing, testing, testing!

## Features

* ~~[API Documentation](https://justcoding121.github.io/titanium-web-proxy/docs/api/Titanium.Web.Proxy.ProxyServer.html)~~
* ~~[Wiki & Contribution guidelines](https://github.com/justcoding121/Titanium-Web-Proxy/wiki)~~
* Multithreaded and asynchronous proxy employing server connection pooling, certificate cache, and buffer pooling
* View, modify, redirect and block requests or responses
* Supports mutual SSL authentication, proxy authentication & automatic upstream proxy detection
* Supports kerberos, NTLM authentication over HTTP protocols on windows domain controlled networks
* SOCKS4/5 Proxy support

## Installation

Package not yet available on NuGet.

Supports

* `.NET 8.0` and above

## Note to contributors

### Road map

* Fix [outstanding bugs](https://github.com/svrooij/Titanium-Web-Proxy/issues?q=is%3Aopen+is%3Aissue+label%3Abug)
* Support reading request and response body as stream [#823](https://github.com/justcoding121/Titanium-Web-Proxy/issues/823)
* Support HTTP 2.0

### Collaborators

The owner of this project, [justcoding121](https://github.com/justcoding121), is considered to be inactive from this project due to his busy work schedule. See [project reboot](#project-reboot) for more information.

Previous contributors:

* [justcoding121](https://github.com/justcoding121) *owner*
* [honfika](https://github.com/honfika)

Current contributors:

* [svrooij](https://github.com/svrooij)

You contributions are more then welcome! Let's make this project great again!

## Development environment

### Windows

* Visual Studio Code as IDE
* Visual Studio 2022 as IDE

### Mac OS

* Visual Studio Code as IDE

### Linux

* Visual Studio Code as IDE

## Usage

Refer the HTTP Proxy Server library in your project and look up the test project to learn usage.

Setup HTTP proxy:

```csharp
var proxyServer = new ProxyServer();

// locally trust root certificate used by this proxy 
proxyServer.CertificateManager.TrustRootCertificate(true);

// optionally set the Certificate Engine
// Under Mono only BouncyCastle will be supported
//proxyServer.CertificateManager.CertificateEngine = Network.CertificateEngine.BouncyCastle;

proxyServer.BeforeRequest += OnRequest;
proxyServer.BeforeResponse += OnResponse;
proxyServer.ServerCertificateValidationCallback += OnCertificateValidation;
proxyServer.ClientCertificateSelectionCallback += OnCertificateSelection;


var explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Any, 8000, true)
{
    // Use self-issued generic certificate on all https requests
    // Optimizes performance by not creating a certificate for each https-enabled domain
    // Useful when certificate trust is not required by proxy clients
   //GenericCertificate = new X509Certificate2(Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "genericcert.pfx"), "password")
};

// Fired when a CONNECT request is received
explicitEndPoint.BeforeTunnelConnectRequest += OnBeforeTunnelConnectRequest;

// An explicit endpoint is where the client knows about the existence of a proxy
// So client sends request in a proxy friendly manner
proxyServer.AddEndPoint(explicitEndPoint);
proxyServer.Start();

// Transparent endpoint is useful for reverse proxy (client is not aware of the existence of proxy)
// A transparent endpoint usually requires a network router port forwarding HTTP(S) packets or DNS
// to send data to this endPoint
var transparentEndPoint = new TransparentProxyEndPoint(IPAddress.Any, 8001, true)
{
    // Generic Certificate hostname to use
    // when SNI is disabled by client
    GenericCertificateName = "google.com"
};

proxyServer.AddEndPoint(transparentEndPoint);

//proxyServer.UpStreamHttpProxy = new ExternalProxy() { HostName = "localhost", Port = 8888 };
//proxyServer.UpStreamHttpsProxy = new ExternalProxy() { HostName = "localhost", Port = 8888 };

foreach (var endPoint in proxyServer.ProxyEndPoints)
Console.WriteLine("Listening on '{0}' endpoint at Ip {1} and port: {2} ",
    endPoint.GetType().Name, endPoint.IpAddress, endPoint.Port);

// Only explicit proxies can be set as system proxy!
proxyServer.SetAsSystemHttpProxy(explicitEndPoint);
proxyServer.SetAsSystemHttpsProxy(explicitEndPoint);

// wait here (You can use something else as a wait function, I am using this as a demo)
Console.Read();

// Unsubscribe & Quit
explicitEndPoint.BeforeTunnelConnectRequest -= OnBeforeTunnelConnectRequest;
proxyServer.BeforeRequest -= OnRequest;
proxyServer.BeforeResponse -= OnResponse;
proxyServer.ServerCertificateValidationCallback -= OnCertificateValidation;
proxyServer.ClientCertificateSelectionCallback -= OnCertificateSelection;

proxyServer.Stop();
    
```

Sample request and response event handlers

```csharp
private async Task OnBeforeTunnelConnectRequest(object sender, TunnelConnectSessionEventArgs e)
{
    string hostname = e.HttpClient.Request.RequestUri.Host;

    if (hostname.Contains("dropbox.com"))
    {
         // Exclude Https addresses you don't want to proxy
         // Useful for clients that use certificate pinning
         // for example dropbox.com
         e.DecryptSsl = false;
    }
}

public async Task OnRequest(object sender, SessionEventArgs e)
{
    Console.WriteLine(e.HttpClient.Request.Url);

    // read request headers
    var requestHeaders = e.HttpClient.Request.Headers;

    var method = e.HttpClient.Request.Method.ToUpper();
    if ((method == "POST" || method == "PUT" || method == "PATCH"))
    {
        // Get/Set request body bytes
        byte[] bodyBytes = await e.GetRequestBody();
        e.SetRequestBody(bodyBytes);

        // Get/Set request body as string
        string bodyString = await e.GetRequestBodyAsString();
        e.SetRequestBodyString(bodyString);
    
        // store request 
        // so that you can find it from response handler 
        e.UserData = e.HttpClient.Request;
    }

    // To cancel a request with a custom HTML content
    // Filter URL
    if (e.HttpClient.Request.RequestUri.AbsoluteUri.Contains("google.com"))
    {
        e.Ok("<!DOCTYPE html>" +
            "<html><body><h1>" +
            "Website Blocked" +
            "</h1>" +
            "<p>Blocked by titanium web proxy.</p>" +
            "</body>" +
            "</html>");
    }

    // Redirect example
    if (e.HttpClient.Request.RequestUri.AbsoluteUri.Contains("wikipedia.org"))
    {
        e.Redirect("https://www.paypal.com");
    }
}

// Modify response
public async Task OnResponse(object sender, SessionEventArgs e)
{
    // read response headers
    var responseHeaders = e.HttpClient.Response.Headers;

    //if (!e.ProxySession.Request.Host.Equals("medeczane.sgk.gov.tr")) return;
    if (e.HttpClient.Request.Method == "GET" || e.HttpClient.Request.Method == "POST")
    {
        if (e.HttpClient.Response.StatusCode == 200)
        {
            if (e.HttpClient.Response.ContentType != null && e.HttpClient.Response.ContentType.Trim().ToLower().Contains("text/html"))
            {
                byte[] bodyBytes = await e.GetResponseBody();
                e.SetResponseBody(bodyBytes);

                string body = await e.GetResponseBodyAsString();
                e.SetResponseBodyString(body);
            }
        }
    }
    
    if (e.UserData != null)
    {
        // access request from UserData property where we stored it in RequestHandler
        var request = (Request)e.UserData;
    }
}

// Allows overriding default certificate validation logic
public Task OnCertificateValidation(object sender, CertificateValidationEventArgs e)
{
    // set IsValid to true/false based on Certificate Errors
    if (e.SslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
        e.IsValid = true;

    return Task.CompletedTask;
}

// Allows overriding default client certificate selection logic during mutual authentication
public Task OnCertificateSelection(object sender, CertificateSelectionEventArgs e)
{
    // set e.clientCertificate to override
    return Task.CompletedTask;
}
```

### Console example application screenshot

![alt tag](https://raw.githubusercontent.com/svrooij/Titanium-Web-Proxy/develop/examples/Titanium.Web.Proxy.Examples.Basic/Capture.PNG)

### GUI example application screenshot

![alt tag](https://raw.githubusercontent.com/svrooij/Titanium-Web-Proxy/develop/examples/Titanium.Web.Proxy.Examples.Wpf/Capture.PNG)

[badge_issues]: https://img.shields.io/github/issues/svrooij/titanium-web-proxy?style=for-the-badge
[badge_sponsor]: https://img.shields.io/github/sponsors/svrooij?style=for-the-badge&logo=github
[badge_twp-repo]: https://img.shields.io/badge/Titanium--Web--Proxy-Reboot-blue?style=for-the-badge
[badge_twp_build]: https://img.shields.io/github/check-runs/svrooij/titanium-web-proxy/develop?style=for-the-badge

[link_build]: https://github.com/svrooij/titanium-web-proxy/actions/workflows/dotnetcore.yml
[link_issues]: https://github.com/svrooij/titanium-web-proxy/issues
[link_twp-repo]: https://github.com/svrooij/titanium-web-proxy
[link_sponsor]: https://github.com/sponsors/svrooij
