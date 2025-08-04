using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.Events;
public class RequestEventResponse : IDisposable
{
    public readonly HttpRequestMessage? ModifiedRequest;
    public readonly HttpResponseMessage? Response;

    private RequestEventResponse ( HttpRequestMessage? modifiedRequest, HttpResponseMessage? earlyResponse )
    {
        ModifiedRequest = modifiedRequest;
        Response = earlyResponse;
    }

    public static RequestEventResponse ContinueResponse() => new RequestEventResponse(null, null);
    public static RequestEventResponse ModifyRequest(HttpRequestMessage modifiedRequest) => new RequestEventResponse(modifiedRequest, null);
    public static RequestEventResponse EarlyResponse(HttpResponseMessage earlyResponse) => new RequestEventResponse(null, earlyResponse);
    public void Dispose ()
    {
        // TODO: Do we need to dispose these?
        //_modifiedRequest?.Dispose();
        //_earlyResponse?.Dispose();
    }
}
