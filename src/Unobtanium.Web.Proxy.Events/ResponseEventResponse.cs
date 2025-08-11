namespace Unobtanium.Web.Proxy.Events;

/// <summary>
/// Holds the response for the <see cref="ProxyServerEvents.OnResponse"/> event.
/// </summary>
public class ResponseEventResponse
{
    /// <summary>
    /// Modified response that can be returned to the client.
    /// </summary>
    public readonly HttpResponseMessage? ModifiedResponse;
    private ResponseEventResponse(HttpResponseMessage? modifiedResponse)
    {
        ModifiedResponse = modifiedResponse;
    }

    /// <summary>
    /// Send the original response to the client, or continue processing the response as normal.
    /// </summary>
    public static ResponseEventResponse ContinueResponse() => new ResponseEventResponse(null);

    /// <summary>
    /// Modify the response before it is sent to the client.
    /// </summary>
    /// <param name="modifiedResponse"></param>
    public static ResponseEventResponse ModifyResponse(HttpResponseMessage modifiedResponse) => new ResponseEventResponse(modifiedResponse);
}
