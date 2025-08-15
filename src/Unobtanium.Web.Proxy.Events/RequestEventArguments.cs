using System.Diagnostics;

namespace Unobtanium.Web.Proxy.Events;

/// <summary>
/// Event arguments for the request event.
/// </summary>
public class RequestEventArguments : IDisposable
{
    /// <summary>
    /// Event arguments for the <see cref="ProxyServerEvents.OnRequest"/> event."/>
    /// </summary>
    /// <param name="request">Incoming http request</param>
    /// <param name="clientDetails">Information about the client that is connecting</param>
    /// <param name="requestActivity">Activity for distributed tracing</param>
    /// <param name="requestId">ID that stays the same for this request</param>
    internal RequestEventArguments ( HttpRequestMessage request, ClientDetails clientDetails, Activity? requestActivity, string? requestId = null )
    {
        Request = request;
        ClientDetails = clientDetails;
        RequestActivity = requestActivity;
        RequestId = requestActivity?.TraceId.ToString() ?? requestId ?? Guid.NewGuid().ToString();
    }
    /// <summary>
    /// Incoming HTTP request message that is being processed by the proxy server.
    /// </summary>
    public HttpRequestMessage Request { get; internal set; }

    /// <summary>
    /// Information about the client that is connecting to the proxy server.
    /// </summary>
    public ClientDetails ClientDetails { get; internal set; }

    /// <summary>
    /// Activity for distributed tracing, which can be used to track the request across different services.
    /// </summary>
    public Activity? RequestActivity { get; internal set; }

    /// <summary>
    /// A random ID to keep track of everything related to this request
    /// </summary>
    public string RequestId { get; internal set; }


    /// <inheritdoc/>
    public void Dispose ()
    {
        RequestActivity?.Dispose();
    }
}
