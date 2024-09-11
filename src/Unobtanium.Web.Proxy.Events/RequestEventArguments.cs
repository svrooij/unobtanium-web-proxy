using System.Diagnostics;

namespace Unobtanium.Web.Proxy.Events;

/// <summary>
/// Event arguments for the request event.
/// </summary>
public class RequestEventArguments: IDisposable
{
    public RequestEventArguments ( HttpRequestMessage request, Activity? requestActivity )
    {
        Request = request;
        RequestActivity = requestActivity;
    }
    public HttpRequestMessage Request { get; private set; }
    public Activity? RequestActivity { get; private set; }
    
    public void Dispose ()
    {
        RequestActivity?.Dispose();
    }
}
