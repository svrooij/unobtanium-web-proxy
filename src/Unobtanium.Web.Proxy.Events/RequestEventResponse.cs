using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.Events;

/// <summary>
/// Response to the <see cref="ProxyServerEvents.OnRequest"/> event.
/// </summary>
public class RequestEventResponse : IDisposable
{

    internal readonly HttpRequestMessage? ModifiedRequest;
    internal readonly HttpResponseMessage? Response;

    /// <summary>
    /// Constructs a new instance of <see cref="RequestEventResponse"/>.
    /// </summary>
    /// <param name="modifiedRequest"></param>
    /// <param name="earlyResponse"></param>
    private RequestEventResponse ( HttpRequestMessage? modifiedRequest, HttpResponseMessage? earlyResponse )
    {
        ModifiedRequest = modifiedRequest;
        Response = earlyResponse;
    }

    /// <summary>
    /// The request was not modified, and the request should continue as normal.
    /// </summary>
    public static RequestEventResponse ContinueResponse() => new RequestEventResponse(null, null);

    /// <summary>
    /// The request was modified, and the modified request should be used instead of the original request.
    /// </summary>
    /// <param name="modifiedRequest"></param>
    public static RequestEventResponse ModifyRequest(HttpRequestMessage modifiedRequest) => new RequestEventResponse(modifiedRequest, null);

    /// <summary>
    /// The request was not modified, but an early response should be returned to the client without sending the request to the server.
    /// </summary>
    /// <param name="earlyResponse"></param>
    public static RequestEventResponse EarlyResponse(HttpResponseMessage earlyResponse) => new RequestEventResponse(null, earlyResponse);
    
    /// <inheritdoc />
    public void Dispose ()
    {
        // TODO: Do we need to dispose these?
        //_modifiedRequest?.Dispose();
        //_earlyResponse?.Dispose();
    }
}
