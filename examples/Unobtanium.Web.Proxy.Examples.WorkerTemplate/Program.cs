using Unobtanium.Web.Proxy;
using Unobtanium.Web.Proxy.Examples.WorkerTemplate;
using Unobtanium.Web.Proxy.Models;

var builder = Host.CreateApplicationBuilder(args);
builder.AddSensibleDefault();
var config = new ProxyServerConfiguration()
{
    TcpTimeWaitSeconds = 10,
    ConnectionTimeOutSeconds = 15,
    ReuseSocket = false,
    EnableConnectionPool = true,
    //EnableHttp2 = true,
    ForwardToUpstreamGateway = true,
    CertificateTrustMode = ProxyCertificateTrustMode.UserTrust,
    ShouldProxyRequest = async ( uri, cancellationToken ) => {
        return uri.Host.Contains("graph.microsoft.com");
        //return !uri.Host.Contains("localhost");
    }
};
config.Events.OnRequest += async (s, e, cancellationToken) =>
{
    Console.WriteLine($"Request to: {e.Request.RequestUri}");
    if (e.Request.Content is not null)
    {
        var body = await e.Request.Content!.ReadAsStringAsync(cancellationToken);
        Console.WriteLine(body);
    }
};
config.EndPoints = [new ExplicitProxyEndPoint(System.Net.IPAddress.Any, 8000)];
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<ProxyServer>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
