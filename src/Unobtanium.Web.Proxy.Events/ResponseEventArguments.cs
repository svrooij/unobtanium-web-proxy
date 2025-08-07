using System.Diagnostics;

namespace Unobtanium.Web.Proxy.Events;

/// <summary>
/// Event arguments for the <see cref="ProxyServerEvents.OnResponse"/> event.
/// </summary>
public class ResponseEventArguments: RequestEventArguments
{
    /// <summary>
    /// Constructor for the <see cref="ResponseEventArguments"/> class, which is used to pass the response and request information to the event handlers.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="response"></param>
    /// <param name="requestActivity"></param>
    internal ResponseEventArguments (HttpRequestMessage request, HttpResponseMessage response, Activity? requestActivity ) : base(request, requestActivity)
    {
        Response = response;
    }

    /// <summary>
    /// Http response message that is received from the server after processing the request.
    /// </summary>
    public HttpResponseMessage Response { get; internal set; }
}
