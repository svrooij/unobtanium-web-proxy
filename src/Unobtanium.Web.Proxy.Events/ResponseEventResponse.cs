namespace Unobtanium.Web.Proxy.Events;
public class ResponseEventResponse
{
    public readonly HttpResponseMessage? ModifiedResponse;
    private ResponseEventResponse(HttpResponseMessage? modifiedResponse)
    {
        ModifiedResponse = modifiedResponse;
    }

    public static ResponseEventResponse ContinueResponse() => new ResponseEventResponse(null);
    public static ResponseEventResponse ModifyResponse(HttpResponseMessage modifiedResponse) => new ResponseEventResponse(modifiedResponse);
}
