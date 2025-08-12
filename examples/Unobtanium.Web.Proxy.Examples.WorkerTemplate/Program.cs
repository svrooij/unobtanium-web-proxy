using Unobtanium.Proxy;
using Unobtanium.Proxy.Interfaces;
using Unobtanium.Web.Proxy.Events;
using Unobtanium.Web.Proxy.Examples.WorkerTemplate;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHttpClient("Unobtanium.Web.Proxy.Examples.WorkerTemplate", client =>
{
    // Configure the HttpClient as needed, e.g., set base address, default headers, etc.
    client.BaseAddress = new Uri("https://graph.microsoft.com/");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    // Configure HttpClientHandler to explicitly bypass system proxy settings
    return new HttpClientHandler()
    {
        UseProxy = false, // Explicitly disable proxy usage
        Proxy = null      // Ensure no proxy is set
    };
});
builder.AddSensibleDefault();

var config = new ProxyServerConfiguration()
{
    DefaultPort = 8000,
    TryDecryptHttps = async (host, _) =>
    {
        // Implement your logic to determine if HTTPS decryption should be attempted
        // For example, you might check if the URL is in a whitelist
        return host.StartsWith("graph.microsoft.com");
    },
};
config.Events.OnRequest += async (s, e, cancellationToken) =>
{
    Console.WriteLine($"Request to: {e.Request.RequestUri}");
    //if (e.Request.Content is not null)
    //{
    //    var body = await e.Request.Content!.ReadAsStringAsync(cancellationToken);
    //    Console.WriteLine(body);
    //}
    if (e.Request.RequestUri.ToString().Contains("graph.microsoft.com/v1.0/"))
    {
        var content = @"{
	""error"": {
		""code"": ""InvalidAuthenticationToken"",
		""message"": ""Access token is empty."",
		""innerError"": {
			""date"": ""2025-08-04T17:51:33"",
			""request-id"": ""aec30f44-185a-49fc-9cff-03471de2af3d"",
			""client-request-id"": ""aec30f44-185a-49fc-9cff-03471de2af3d""
		}
	}
}";
        var response = new HttpResponseMessage {
            StatusCode = System.Net.HttpStatusCode.Unauthorized,
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };
        return Unobtanium.Web.Proxy.Events.RequestEventResponse.EarlyResponse(response);
    }

    if (e.Request.RequestUri.ToString().Contains("openai.azure.com"))
    {
        return Unobtanium.Web.Proxy.Events.RequestEventResponse.ContinueResponse();
        var response = new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.Unauthorized,
            //Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };
        return Unobtanium.Web.Proxy.Events.RequestEventResponse.EarlyResponse(response);
    }
    return Unobtanium.Web.Proxy.Events.RequestEventResponse.ContinueResponse();
};

config.Events.OnResponse += async (s, e, cancellationToken) =>
{
    Console.WriteLine($"Response from: {e.Request.RequestUri}");
    //if (e.Response.Content is not null)
    //{
    //    var body = await e.Response.Content!.ReadAsStringAsync(cancellationToken);
    //    Console.WriteLine(body);
    //}
    return ResponseEventResponse.ContinueResponse();
};

//config.Endpoints = [new ExplicitProxyEndPoint(System.Net.IPAddress.Any, 8000)];
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<ProxyServer>();

// Register the custom HttpClient factory for the proxy server to use
builder.Services.AddSingleton<IProxyHttpClientFactory, CustomProxyHttpClientFactory>();

// Register HttpClientService to handle outbound requests without using system proxy
//builder.Services.AddSingleton<HttpClientService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
