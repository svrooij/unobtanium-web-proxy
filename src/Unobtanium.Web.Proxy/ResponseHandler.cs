using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.EventArguments;
using Unobtanium.Web.Proxy.Extensions;
using Unobtanium.Web.Proxy.Network.WinAuth.Security;
using System.Collections.Generic;
using Unobtanium.Web.Proxy.Http;
using System.Diagnostics;

namespace Unobtanium.Web.Proxy;

/// <summary>
///     Handle the response from server.
/// </summary>
public partial class ProxyServer
{
    /// <summary>
    ///     Called asynchronously when a request was successful and we received the response.
    /// </summary>
    /// <param name="args">The session event arguments.</param>
    /// <returns> The task.</returns>
    private async Task HandleHttpSessionResponse ( SessionEventArgs args )
    {
        var cancellationToken = args.CancellationTokenSource.Token;

        // read response & headers from server
        await args.HttpClient.ReceiveResponse(cancellationToken);

        // Server may send expect-continue even if not asked for it in request.
        // According to spec "the client can simply discard this interim response."
        if (args.HttpClient.Response.StatusCode == (int)HttpStatusCode.Continue)
        {
            await args.ClearResponse(cancellationToken);
            await args.HttpClient.ReceiveResponse(cancellationToken);
        }

        args.TimeLine["Response Received"] = DateTime.UtcNow;

        var response = args.HttpClient.Response;
        args.ReRequest = false;

        // check for windows authentication
        if (args.EnableWinAuth)
        {
            if (response.StatusCode == (int)HttpStatusCode.Unauthorized)
                await Handle401UnAuthorized(args);
            else
                WinAuthEndPoint.AuthenticatedResponse(args.HttpClient.Data);
        }

        // save original values so that if user changes them
        // we can still use original values when syphoning out data from attached tcp connection.
        response.SetOriginalHeaders();

        // if user requested call back then do it
        if (!response.Locked) await OnBeforeResponse(args);

        // it may changed in the user event
        response = args.HttpClient.Response;

        var clientStream = args.ClientStream;

        // user set custom response by ignoring original response from server.
        if (response.Locked)
        {
            // write custom user response with body and return.
            await clientStream.WriteResponseAsync(response, cancellationToken);

            if (args.HttpClient.HasConnection && !args.HttpClient.CloseServerConnection)
                // syphon out the original response body from server connection
                // so that connection will be good to be reused.
                await args.SyphonOutBodyAsync(false, cancellationToken);

            return;
        }

        // if user requested to send request again
        // likely after making modifications from User Response Handler
        if (args.ReRequest)
        {
            if (args.HttpClient.HasConnection) await TcpConnectionFactory.Release(args.HttpClient.Connection);

            // clear current response
            await args.ClearResponse(cancellationToken);
            var result = await HandleHttpSessionRequest(args, null, args.ClientConnection.NegotiatedApplicationProtocol,
                cancellationToken, args.CancellationTokenSource);
            if (result.LatestConnection != null) args.HttpClient.SetConnection(result.LatestConnection);

            return;
        }

        response.Locked = true;

        if (!args.IsTransparent && !args.IsSocks) response.Headers.FixProxyHeaders();

        await clientStream.WriteResponseAsync(response, cancellationToken);

        if (response.OriginalHasBody)
        {
            if (response.IsBodySent)
            {
                // syphon out body
                await args.SyphonOutBodyAsync(false, cancellationToken);
            }
            else
            {
                // Copy body if exists
                var serverStream = args.HttpClient.Connection.Stream;
                await serverStream.CopyBodyAsync(response, false, clientStream, TransformationMode.None,
                    false, args, cancellationToken);
            }

            response.IsBodyReceived = true;
        }

        args.TimeLine["Response Sent"] = DateTime.UtcNow;
    }

    /// <summary>
    ///     Invoke before response if it is set.
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    private async Task OnBeforeResponse ( SessionEventArgs args )
    {
        // Support legacy BeforeResponse event for backward compatibility (DEPRECATED)
        #pragma warning disable CS0618 // Type or member is obsolete
        if (BeforeResponse != null) 
            await BeforeResponse.InvokeAsync(this, args, logger);
        #pragma warning restore CS0618

        // Use the new response event system (PREFERRED)
        if (configuration.Events.HasOnResponse)
        {
            using var activity = activitySource?.StartActivity(nameof(OnBeforeResponse), ActivityKind.Internal);
            
            // Create HttpRequestMessage and HttpResponseMessage from the session
            var httpRequest = CreateHttpRequestMessageFromCustomRequest(args.HttpClient.Request);
            var httpResponse = await ConvertCustomResponseToHttpResponseMessage(args.HttpClient.Response);
            
            var responseArguments = new Events.ResponseEventArguments(httpRequest, httpResponse, activity);
            
            try
            {
                var response = await configuration.Events.InvokeOnResponse(this, responseArguments, logger, args.CancellationTokenSource.Token);
                
                // Handle the response from the new event system
                if (response.ModifiedResponse != null)
                {
                    // Modified response - convert back to custom Response object
                    var customResponse = await ConvertHttpResponseMessageToCustomResponse(response.ModifiedResponse);
                    args.HttpClient.Response = customResponse;
                }
                // For ContinueResponse, no action needed - processing continues normally
            }
            finally
            {
                responseArguments.Dispose();
            }
        }
    }

    /// <summary>
    /// Convert custom Response to HttpResponseMessage for new event system
    /// </summary>
    private async Task<HttpResponseMessage> ConvertCustomResponseToHttpResponseMessage(Response customResponse)
    {
        var httpResponse = new HttpResponseMessage((HttpStatusCode)customResponse.StatusCode)
        {
            ReasonPhrase = customResponse.StatusDescription,
            Version = customResponse.HttpVersion
        };

        // Copy headers (excluding content headers which will be set with content)
        var contentHeaders = new List<Models.HttpHeader>();
        foreach (var header in customResponse.Headers.GetAllHeaders())
        {
            if (IsContentHeader(header.Name))
            {
                contentHeaders.Add(header);
            }
            else
            {
                httpResponse.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }
        }

        // Handle body if present
        if (customResponse.HasBody && customResponse.IsBodyRead)
        {
            httpResponse.Content = new ByteArrayContent(customResponse.Body);
            
            // Add content headers
            foreach (var header in contentHeaders)
            {
                httpResponse.Content.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }
        }

        return httpResponse;
    }

    /// <summary>
    ///     Invoke after response if it is set (legacy support).
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    private async Task OnAfterResponse ( SessionEventArgs args )
    {
        // Support legacy AfterResponse event for backward compatibility (DEPRECATED)
        #pragma warning disable CS0618 // Type or member is obsolete
        if (AfterResponse != null) 
            await AfterResponse.InvokeAsync(this, args, logger);
        #pragma warning restore CS0618
    }

#if DEBUG
    internal bool ShouldCallBeforeResponseBodyWrite ()
    {
        if (OnResponseBodyWrite != null)
        {
            return true;
        }

        return false;
    }

    internal async Task OnBeforeResponseBodyWrite ( BeforeBodyWriteEventArgs args )
    {
        if (OnResponseBodyWrite != null)
        {
            await OnResponseBodyWrite.InvokeAsync(this, args, logger);
        }
    }
#endif
}
