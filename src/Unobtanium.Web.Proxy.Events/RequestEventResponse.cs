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
    
    /// <summary>
    /// Creates a response with the specified HTTP status code, optional reason phrase, and optional content.
    /// </summary>
    /// <remarks>The response content, if provided, is encoded as UTF-8 with a MIME type of
    /// "text/plain".</remarks>
    /// <param name="statusCode">The HTTP status code to set for the response.</param>
    /// <param name="reasonPhrase">An optional reason phrase that provides additional context for the status code.  If null, no reason phrase is
    /// set.</param>
    /// <param name="content">An optional string representing the response content.  If null, the response will have no content.</param>
    /// <returns>A <see cref="RequestEventResponse"/> containing the configured HTTP response.</returns>
    public static RequestEventResponse StatusCodeResponse(System.Net.HttpStatusCode statusCode, string? reasonPhrase = null, string? content = null)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            ReasonPhrase = reasonPhrase,
            Content = content != null ? new StringContent(content, Encoding.UTF8, "text/plain") : null
        };
        return EarlyResponse(response);
    }

    /// <inheritdoc />
    public void Dispose ()
    {
        // TODO: Do we need to dispose these?
        //_modifiedRequest?.Dispose();
        //_earlyResponse?.Dispose();
    }
}
