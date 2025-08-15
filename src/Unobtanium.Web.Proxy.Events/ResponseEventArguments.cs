using System.Diagnostics;

namespace Unobtanium.Web.Proxy.Events;

/// <summary>
/// Event arguments for the <see cref="ProxyServerEvents.OnResponse"/> event.
/// </summary>
public class ResponseEventArguments : RequestEventArguments, IDisposable
{
    /// <summary>
    /// Constructor for the <see cref="ResponseEventArguments"/> class, which is used to pass the response and request information to the event handlers.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="response"></param>
    /// <param name="clientDetails"></param>
    /// <param name="requestActivity"></param>
    /// <param name="requestId"></param>
    internal ResponseEventArguments ( HttpRequestMessage request, HttpResponseMessage response, ClientDetails clientDetails, Activity? requestActivity, string? requestId = null ) : base(request, clientDetails, requestActivity, requestId)
    {
        Response = response;
    }

    /// <summary>
    /// Http response message that is received from the server after processing the request.
    /// </summary>
    public HttpResponseMessage Response { get; internal set; }

    /// <summary>
    /// Releases the resources used by the current instance of the class.
    /// </summary>
    /// <remarks>This method disposes of both managed and unmanaged resources.  It should be called when the
    /// instance is no longer needed to free up resources.</remarks>
    public new void Dispose ()
    {
        base.Dispose();
        Response?.Dispose();
    }

}
