using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Events;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Unobtanium.Web.Proxy")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Unobtanium.Web.Proxy.KestrelTests")]

namespace Unobtanium.Web.Proxy.Events;
/// <summary>
/// All events you can configure on the proxy server.
/// </summary>
public class ProxyServerEvents
{
    /// <summary>
    /// Each new connection to the proxy server will call this function.
    /// You can use this event to decide wheter or not this connection should be decrypted or forwarded as is.<br/><br/>
    /// returning <see langword="false"/> will result in the connection being forwarded as is, without decryption.<br/>
    /// returning <see langword="true"/> will result in the connection being decrypted and processed by the proxy server.<br/>
    /// </summary>
    /// <remarks>You will get the hostname and and a cancellation token source</remarks>
    public Func<string,CancellationTokenSource, Task<bool>>? ShouldDecryptNewConnection;

    internal async Task<bool> InvokeShouldDecryptNewConnection (string hostname, CancellationTokenSource cancellationTokenSource )
    {
        if (ShouldDecryptNewConnection == null)
            return true; // Default to true if no handler is registered.
        return await ShouldDecryptNewConnection.Invoke(hostname, cancellationTokenSource);
    }

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

    internal bool HasOnRequest => OnRequest != null;

    internal async Task<RequestEventResponse> InvokeOnRequest ( object sender, RequestEventArguments requestEventArguments, ILogger? logger, CancellationToken cancellationToken)
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

    internal bool HasOnResponse => OnResponse != null;

    internal async Task<ResponseEventResponse> InvokeOnResponse ( object sender, ResponseEventArguments responseEventArguments, ILogger? logger, CancellationToken cancellationToken )
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

    internal void ClearEvents()
    {
        OnRequest = null;
        OnResponse = null;
        ShouldDecryptNewConnection = null;
    }
}
