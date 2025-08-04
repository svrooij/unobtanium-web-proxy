using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Events;

namespace Unobtanium.Web.Proxy.Events;
/// <summary>
/// All events you can configure on the proxy server.
/// </summary>
public class ProxyServerEvents
{
    /// <summary>
    ///    You'll get this event for each request that the proxy server is decrypting and processing.
    /// </summary>
    /// <remarks>
    /// You're responsible for returning a <see cref="RequestEventResponse"/>.<br/>
    /// And you have tree options:<br/>
    /// Use <see cref="RequestEventResponse.ContinueResponse()"/> to continue processing the request as normal. <br/>
    /// Use <see cref="RequestEventResponse.ModifyRequest(HttpRequestMessage)"/> to modify the request before it is sent to the server. <br/>
    /// Use <see cref="RequestEventResponse.EarlyResponse(HttpResponseMessage)"/> to return an early response to the client, without sending the request to the server. <br/>
    /// </remarks>
    public event AsyncEventHandler<RequestEventArguments, RequestEventResponse>? OnRequest;

    public bool HasOnRequest => OnRequest != null;

    public async Task<RequestEventResponse> InvokeOnRequest ( object sender, RequestEventArguments requestEventArguments, ILogger? logger, CancellationToken cancellationToken)
    {
        if(OnRequest == null)
            return RequestEventResponse.ContinueResponse();

        // Invoke on all registered handlers in a foreach loop
        // if the result of InternalInvokeWithLoggerAsync is null, continue to the next handler.
        // if the result has a modified request, use the modifier request for the next handler.
        // if the result has an early response, return that response.
        RequestEventResponse? response = null;
        foreach (var handler in OnRequest.GetInvocationList())
        {
            response = await AsyncEventHandlerExtensions.InternalInvokeWithLoggerAsync((AsyncEventHandler<RequestEventArguments, RequestEventResponse>)handler, sender, requestEventArguments, logger, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                break;
            if (response?.ModifiedRequest != null)
            {
                requestEventArguments.Request = response.ModifiedRequest;
            }
            else if (response?.Response != null)
            {
                return response;
            }
        }

        return response ?? RequestEventResponse.ContinueResponse();

    }

    /// <summary>
    /// This event is triggered when the proxy server has a response from the server. (This is not triggered when an <see cref="OnRequest"/> event returned a new HttpResponseMessage).
    /// </summary>
    /// <remarks>Use this event for logging or caching purpuses</remarks>
    public event AsyncEventHandler<ResponseEventArguments, ResponseEventResponse>? OnResponse;

    public bool HasOnResponse => OnResponse != null;

    public async Task<ResponseEventResponse> InvokeOnResponse ( object sender, ResponseEventArguments responseEventArguments, ILogger? logger, CancellationToken cancellationToken )
    {
        if (OnResponse == null)
            return ResponseEventResponse.ContinueResponse();
        // Invoke on all registered handlers in a foreach loop
        // if the result of InternalInvokeWithLoggerAsync is null, continue to the next handler.
        ResponseEventResponse? response = null;
        foreach ( var handler in OnResponse.GetInvocationList() )
        {
            response = await AsyncEventHandlerExtensions.InternalInvokeWithLoggerAsync((AsyncEventHandler<ResponseEventArguments, ResponseEventResponse>)handler, sender, responseEventArguments, logger, cancellationToken);
            if ( response?.ModifiedResponse != null )
            {
                // If the response is not null, we can return early.
                // This means that the user has modified the response and we should not continue processing.
                return response;
            }
            if ( cancellationToken.IsCancellationRequested )
                break;
        }
        return ResponseEventResponse.ContinueResponse();
    }
}
