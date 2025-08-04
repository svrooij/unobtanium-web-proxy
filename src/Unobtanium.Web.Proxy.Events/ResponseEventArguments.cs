using System.Diagnostics;

namespace Unobtanium.Web.Proxy.Events;

/// <summary>
/// Event arguments for the OnResponse event.
/// </summary>
public class ResponseEventArguments: RequestEventArguments
{
    public ResponseEventArguments (HttpRequestMessage request, HttpResponseMessage response, Activity? requestActivity ) : base(request, requestActivity)
    {
        Response = response;
    }
    public HttpResponseMessage Response { get; internal set; }
}
