using Unobtanium.Web.Proxy;
using Unobtanium.Web.Proxy.Events;
using Unobtanium.Web.Proxy.Examples.WorkerTemplate;
using Unobtanium.Web.Proxy.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHttpClient(CustomProxyHttpClientFactory.CLIENT_NAME, client =>
{
    // Configure the HttpClient as needed, e.g., set base address, default headers, etc.
    // client.BaseAddress = new Uri("https://graph.microsoft.com/");
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

var events = new ProxyServerEvents();
events.ShouldDecryptNewConnection = async ( host, client, cts ) =>
{
    // Log the new connection details
    return host.Equals("graph.microsoft.com");
};
events.OnRequest += async ( s, e, cancellationToken ) =>
{
    Console.WriteLine($"Request to: {e.Request.RequestUri}");
    //if (e.Request.Content is not null)
    //{
    //    var body = await e.Request.Content!.ReadAsStringAsync(cancellationToken);
    //    Console.WriteLine(body);
    //}
    if (e.Request.RequestUri.ToString().StartsWith("https://graph.microsoft.com/v1.0/"))
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
        var response = new HttpResponseMessage
        {
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

events.OnResponse += async ( s, e, cancellationToken ) =>
{
    Console.WriteLine($"Response from: {e.Request.RequestUri}");
    //if (e.Response.Content is not null)
    //{
    //    var body = await e.Response.Content!.ReadAsStringAsync(cancellationToken);
    //    Console.WriteLine(body);
    //}
    return ResponseEventResponse.ContinueResponse();
};

builder.Services.AddProxyEvents(events);

// Register the custom HttpClient factory for the proxy server to use
builder.Services.AddSingleton<IProxyHttpClientFactory, CustomProxyHttpClientFactory>();

builder.Services.Configure<ProxyServerOptions>(options =>
{
    options.Port = ProxyServerDefaults.DEFAULT_PORT; // Set the port for the proxy server
    options.HttpsPort = ProxyServerDefaults.DEFAULT_HTTPS_PORT;
});

builder.Services.Configure<CertificateManagerConfiguration>(options =>
{
    options.CachePath = "c:\\temp\\certs"; // Set the path where certificates will be cached
});

builder.Services.AddProxyServices();

var host = builder.Build();
host.Run();
