using System.Diagnostics;

namespace Unobtanium.Web.Proxy.Events;

/// <summary>
/// Event arguments for the request event.
/// </summary>
public class RequestEventArguments: IDisposable
{
    public RequestEventArguments ( HttpRequestMessage request, Activity? requestActivity, string? requestId = null )
    {
        Request = request;
        RequestActivity = requestActivity;
        RequestId = requestActivity?.Id ?? requestId ?? Guid.NewGuid().ToString();
    }
    public HttpRequestMessage Request { get; internal set; }
    public Activity? RequestActivity { get; internal set; }

    public string RequestId { get; internal set; }

    public void Dispose ()
    {
        RequestActivity?.Dispose();
    }
}
