using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.EventArguments;
using Unobtanium.Web.Proxy.Exceptions;
using Unobtanium.Web.Proxy.Extensions;
using Unobtanium.Web.Proxy.Helpers;
using Unobtanium.Web.Proxy.Http;
using Unobtanium.Web.Proxy.Models;
using Unobtanium.Web.Proxy.Network;
using Unobtanium.Web.Proxy.Network.Tcp;
using Unobtanium.Web.Proxy.Shared;

namespace Unobtanium.Web.Proxy;

/// <summary>
///     Handle the request
/// </summary>
public partial class ProxyServer
{
    /// <summary>
    ///     This is the core request handler method for a particular connection from client.
    ///     Will create new session (request/response) sequence until
    ///     client/server abruptly terminates connection or by normal HTTP termination.
    /// </summary>
    /// <param name="endPoint">The proxy endpoint.</param>
    /// <param name="clientStream">The client stream.</param>
    /// <param name="cancellationTokenSource">The cancellation token source for this async task.</param>
    /// <param name="connectArgs">The Connect request if this is a HTTPS request from explicit endpoint.</param>
    /// <param name="prefetchConnectionTask">Prefetched server connection for current client using Connect/SNI headers.</param>
    /// <param name="isHttps">Is HTTPS</param>
    private async Task HandleHttpSessionRequest ( ProxyEndPoint endPoint, HttpClientStream clientStream,
        CancellationTokenSource cancellationTokenSource, TunnelConnectSessionEventArgs? connectArgs = null,
        Task<TcpServerConnection?>? prefetchConnectionTask = null, bool isHttps = false )
    {
        var connectRequest = connectArgs?.HttpClient.ConnectRequest;

        var prefetchTask = prefetchConnectionTask;
        TcpServerConnection? connection = null;
        var closeServerConnection = false;

        try
        {
            var cancellationToken = cancellationTokenSource.Token;

            // Loop through each subsequent request on this particular client connection
            // (assuming HTTP connection is kept alive by client)
            while (true)
            {
                if (clientStream.IsClosed) return;

                // Create request activity as child of current activity (which should be the ClientConnection activity)
                using var requestActivity = activitySource?.StartActivity("HttpRequest", ActivityKind.Server);
                requestActivity?.SetTag("proxy.server", "Unobtanium.Web.Proxy");

                // read the request line
                var requestLine = await clientStream.ReadRequestLine(cancellationToken);
                if (requestLine.IsEmpty()) return;

                var args = new SessionEventArgs(this, endPoint, clientStream, connectRequest, cancellationTokenSource)
                {
                    UserData = connectArgs?.UserData
                };

                var request = args.HttpClient.Request;
                if (isHttps) request.IsHttps = true;

                try
                {
                    try
                    {
                        // Read the request headers in to unique and non-unique header collections
                        await HeaderParser.ReadHeaders(clientStream, args.HttpClient.Request.Headers,
                            cancellationToken);

                        if (connectRequest != null)
                        {
                            request.IsHttps = connectRequest.IsHttps;
                            request.Authority = connectRequest.Authority;
                        }

                        request.RequestUriString8 = requestLine.RequestUri;

                        request.Method = requestLine.Method;
                        request.HttpVersion = requestLine.Version;

                        // Set activity tags with request information
                        requestActivity?.SetTag("http.method", request.Method);
                        requestActivity?.SetTag("http.url", request.Url);
                        requestActivity?.SetTag("http.scheme", request.IsHttps ? "https" : "http");
                        requestActivity?.SetTag("http.target", request.RequestUri.PathAndQuery);
                        if (request.RequestUri.Host != null)
                        {
                            requestActivity?.SetTag("http.host", request.RequestUri.Host);
                        }

                        // we need this to syphon out data from connection if API user changes them.
                        request.SetOriginalHeaders();

                        // If user requested interception do it
                        await OnBeforeRequest(args, requestActivity, cancellationToken);

                        if (!args.IsTransparent && !args.IsSocks)
                        {
                            // proxy authorization check
                            if (connectRequest == null && await CheckAuthorization(args) == false)
                            {
                                await OnBeforeResponse(args);

                                // send the response
                                await clientStream.WriteResponseAsync(args.HttpClient.Response, cancellationToken);
                                return;
                            }

                            PrepareRequestHeaders(request.Headers);
                            request.Host = request.RequestUri.Authority;
                        }

                        // Add distributed tracing headers if they don't exist and we have an active activity
                        AddDistributedTracingHeaders(request.Headers, requestActivity);

                        // if win auth is enabled
                        // we need a cache of request body
                        // so that we can send it after authentication in WinAuthHandler.cs
                        if (args.EnableWinAuth && request.HasBody) await args.GetRequestBody(cancellationToken);

                        var response = args.HttpClient.Response;

                        if (request.CancelRequest)
                        {
                            if (!(Enable100ContinueBehaviour && request.ExpectContinue))
                                // syphon out the request body from client before setting the new body
                                await args.SyphonOutBodyAsync(true, cancellationToken);

                            await HandleHttpSessionResponse(args);

                            if (!response.KeepAlive) return;

                            continue;
                        }

                        // If prefetch task is available.
                        if (connection == null && prefetchTask != null)
                        {
                            try
                            {
                                connection = await prefetchTask;
                            }
                            catch (SocketException e)
                            {
                                if (e.SocketErrorCode != SocketError.HostNotFound) throw;
                            }

                            prefetchTask = null;
                        }

                        if (connection != null)
                        {
                            var socket = connection.TcpSocket;
                            var part1 = socket.Poll(1000, SelectMode.SelectRead);
                            var part2 = socket.Available == 0;
                            if (part1 & part2)
                            {
                                //connection is closed
                                await TcpConnectionFactory.Release(connection, true);
                                connection = null;
                            }
                        }

                        // create a new connection if cache key changes.
                        // only gets hit when connection pool is disabled.
                        // or when prefetch task has a unexpectedly different connection.
                        if (connection != null
                            && await TcpConnectionFactory.GetConnectionCacheKey(this, args,
                                clientStream.Connection.NegotiatedApplicationProtocol)
                            != connection.CacheKey)
                        {
                            await TcpConnectionFactory.Release(connection);
                            connection = null;
                        }

                        var result = await HandleHttpSessionRequest(args, connection,
                            clientStream.Connection.NegotiatedApplicationProtocol,
                            cancellationToken, cancellationTokenSource);

                        var newConnection = result.LatestConnection;
                        if (connection != newConnection && connection != null)
                            await TcpConnectionFactory.Release(connection);

                        // update connection to latest used
                        connection = result.LatestConnection;

                        closeServerConnection = !result.Continue;

                        // throw if exception happened
                        if (result.Exception != null) throw result.Exception;

                        if (!result.Continue) return;

                        // user requested
                        if (args.HttpClient.CloseServerConnection)
                        {
                            closeServerConnection = true;
                            return;
                        }

                        // if connection is closing exit
                        if (!response.KeepAlive)
                        {
                            closeServerConnection = true;
                            return;
                        }

                        if (cancellationTokenSource.IsCancellationRequested)
                            throw new Exception("Session was terminated by user.");

                        // Release server connection for each HTTP session instead of per client connection.
                        // This will be more efficient especially when client is idly holding server connection 
                        // between sessions without using it.
                        // Do not release authenticated connections for performance reasons.
                        // Otherwise it will keep authenticating per session.
                        if (EnableConnectionPool && connection != null
                                                 && !connection.IsWinAuthenticated)
                        {
                            await TcpConnectionFactory.Release(connection);
                            connection = null;
                        }
                    }
                    catch (Exception e) when (e is not ProxyHttpException)
                    {
                        throw new ProxyHttpException("Error occured whilst handling session request", e, args);
                    }
                }
                catch (Exception e)
                {
                    args.Exception = e;
                    closeServerConnection = true;
                    throw;
                }
                finally
                {
                    await OnAfterResponse(args);
                    args.Dispose();
                }
            }
        }
        finally
        {
            if (connection != null) await TcpConnectionFactory.Release(connection, closeServerConnection);

            await TcpConnectionFactory.Release(prefetchTask, closeServerConnection);
        }
    }

    private async Task<RetryResult> HandleHttpSessionRequest ( SessionEventArgs args,
        TcpServerConnection? serverConnection, SslApplicationProtocol sslApplicationProtocol,
        CancellationToken cancellationToken, CancellationTokenSource cancellationTokenSource )
    {
        args.HttpClient.Request.Locked = true;

        // do not cache server connections for WebSockets
        var noCache = args.HttpClient.Request.UpgradeToWebSocket;

        if (noCache) serverConnection = null;

        // a connection generator task with captured parameters via closure.
        var generator = () =>
            TcpConnectionFactory.GetServerConnection(this,
                args,
                false,
                sslApplicationProtocol,
                noCache,
                cancellationToken);

        // Retry with new connection if the initial stream.WriteAsync call to server fails.
        // i.e if request line and headers failed to get send.
        // Do not retry after reading data from client stream, 
        // because subsequent try will not have data to read from client 
        // and will hang at clientStream.ReadAsync call.
        // So, throw RetryableServerConnectionException only when we are sure we can retry safely.
        return await RetryPolicy<RetryableServerConnectionException>().ExecuteAsync(async connection =>
        {
            // set the connection and send request headers
            args.HttpClient.SetConnection(connection);

            args.TimeLine["Connection Ready"] = DateTime.UtcNow;

            if (args.HttpClient.Request.UpgradeToWebSocket)
            {
                // connectRequest can be null for SOCKS connection
                if (args.HttpClient.ConnectRequest != null)
                    args.HttpClient.ConnectRequest!.TunnelType = TunnelType.Websocket;

                // if upgrading to websocket then relay the request without reading the contents
                await HandleWebSocketUpgrade(args, args.ClientStream, connection, cancellationTokenSource,
                    cancellationToken);
                return false;
            }

            // construct the web request that we are going to issue on behalf of the client.
            await HandleHttpSessionRequest(args);
            return true;
        }, generator, serverConnection);
    }

    private async Task HandleHttpSessionRequest ( SessionEventArgs args )
    {
        var cancellationToken = args.CancellationTokenSource.Token;
        var request = args.HttpClient.Request;

        var body = request.CompressBodyAndUpdateContentLength();

        await args.HttpClient.SendRequest(Enable100ContinueBehaviour, args.IsTransparent,
            cancellationToken);

        // If a successful 100 continue request was made, inform that to the client and reset response
        if (request.ExpectationSucceeded)
        {
            var writer = args.ClientStream;
            var response = args.HttpClient.Response;

            var headerBuilder = new HeaderBuilder();
            headerBuilder.WriteResponseLine(response.HttpVersion, response.StatusCode, response.StatusDescription);
            headerBuilder.WriteHeaders(response.Headers);
            await writer.WriteHeadersAsync(headerBuilder, cancellationToken);

            await args.ClearResponse(cancellationToken);
        }

        // send body to server if available
        if (request.HasBody)
        {
            if (request.IsBodyRead)
                await args.HttpClient.Connection.Stream.WriteBodyAsync(body!, request.IsChunked, cancellationToken);
            else if (!request.ExpectationFailed)
                // get the request body unless an unsuccessful 100 continue request was made
                await args.CopyRequestBodyAsync(args.HttpClient.Connection.Stream, TransformationMode.None,
                    cancellationToken);
        }

        args.TimeLine["Request Sent"] = DateTime.UtcNow;

        // parse and send response
        await HandleHttpSessionResponse(args);
    }

    /// <summary>
    ///     Prepare the request headers so that we can avoid encodings not parseable by this proxy.
    ///     This method removes the Accept-Encoding header completely to prevent compressed responses,
    ///     making it easier for the proxy to inspect, modify, and process the response content.
    /// </summary>
    /// <param name="requestHeaders">The request headers collection to modify</param>
    /// <remarks>
    ///     Removing Accept-Encoding ensures that:
    ///     - Responses are received uncompressed and are human-readable
    ///     - The proxy can easily inspect and modify response content
    ///     - No decompression/recompression is needed when modifying responses
    ///     - Better compatibility with response modification scenarios
    /// </remarks>
    private void PrepareRequestHeaders ( HeaderCollection requestHeaders )
    {
        // Remove Accept-Encoding header completely to prevent compression
        // This ensures the proxy receives uncompressed responses that are easier to process and modify
        requestHeaders.RemoveHeader(KnownHeaders.AcceptEncoding);

        requestHeaders.FixProxyHeaders();
    }

    /// <summary>
    ///     Add distributed tracing headers to the outgoing request if they don't already exist.
    ///     This ensures that trace context is propagated even if the original client request 
    ///     didn't include tracing headers.
    /// </summary>
    /// <param name="requestHeaders">The request headers collection to modify</param>
    /// <param name="activity">The current activity context</param>
    private void AddDistributedTracingHeaders(HeaderCollection requestHeaders, Activity? activity)
    {
        if (activity == null) return;

        // Check if traceparent header already exists from the client
        var existingTraceparent = requestHeaders.GetHeaderValueOrNull("traceparent");
        var existingTracestate = requestHeaders.GetHeaderValueOrNull("tracestate");

        // If no existing trace headers, add them from current activity
        if (string.IsNullOrEmpty(existingTraceparent))
        {
            var traceparent = activity.Id;
            if (!string.IsNullOrEmpty(traceparent))
            {
                requestHeaders.AddHeader("traceparent", traceparent);
                activity.SetTag("http.traceparent_injected", "true");
            }
        }
        else
        {
            activity.SetTag("http.traceparent_preserved", "true");
        }

        // Add tracestate if it exists in the activity and wasn't already present
        if (string.IsNullOrEmpty(existingTracestate) && !string.IsNullOrEmpty(activity.TraceStateString))
        {
            requestHeaders.AddHeader("tracestate", activity.TraceStateString);
            activity.SetTag("http.tracestate_injected", "true");
        }
        else if (!string.IsNullOrEmpty(existingTracestate))
        {
            activity.SetTag("http.tracestate_preserved", "true");
        }

        // Add correlation ID for easier debugging
        requestHeaders.AddHeader("X-Correlation-ID", activity.RootId ?? activity.Id ?? Guid.NewGuid().ToString());
    }

    /// <summary>
    ///     Invoke before request handler if it is set.
    /// </summary>
    /// <param name="args">The session event arguments.</param>
    /// <param name="requestActivity">Activity where this requests belongs to.</param>
    /// <param name="cancellationToken">Cancellation token for this request</param>
    /// <returns></returns>
    private async Task OnBeforeRequest ( SessionEventArgs args, Activity? requestActivity = null, CancellationToken cancellationToken = default)
    {
        args.TimeLine["Request Received"] = DateTime.UtcNow;

        // Support legacy BeforeRequest event for backward compatibility (DEPRECATED)
        #pragma warning disable CS0618 // Type or member is obsolete
        if (BeforeRequest != null) 
            await BeforeRequest.InvokeAsync(this, args, ExceptionFunc);
        #pragma warning restore CS0618

        // Use the new event system for request handling (PREFERRED)
        if (configuration.Events.HasOnRequest) 
        {
            using var activity = activitySource?.StartActivity(nameof(OnBeforeRequest), ActivityKind.Internal, requestActivity?.Context ?? default);
            
            // Create HttpRequestMessage from the custom Request
            var httpRequest = CreateHttpRequestMessageFromCustomRequest(args.HttpClient.Request);
            
            requestActivity?.SetTag("requestUri", args.HttpClient.Request.Url);
            requestActivity?.SetTag("requestMethod", httpRequest.Method.ToString());

            // If the request has a body and it's been read, add it to the HttpRequestMessage
            if (args.HttpClient.Request.HasBody && args.HttpClient.Request.IsBodyRead)
            {
                httpRequest.Content = new ByteArrayContent(args.HttpClient.Request.Body);
                
                // Set the content type if available
                if (!string.IsNullOrEmpty(args.HttpClient.Request.ContentType))
                {
                    httpRequest.Content.Headers.TryAddWithoutValidation("Content-Type", args.HttpClient.Request.ContentType);
                }
            }
            else if (args.HttpClient.Request.HasBody)
            {
                // If body hasn't been read yet, read it now
                var body = await args.GetRequestBody(cancellationToken);
                httpRequest.Content = new ByteArrayContent(body);
                
                // Set the content type if available
                if (!string.IsNullOrEmpty(args.HttpClient.Request.ContentType))
                {
                    httpRequest.Content.Headers.TryAddWithoutValidation("Content-Type", args.HttpClient.Request.ContentType);
                }
            }

            var requestArguments = new Events.RequestEventArguments(
                httpRequest,
                activity
            );
            
            try
            {
                var response = await configuration.Events.InvokeOnRequest(this, requestArguments, logger, cancellationToken);
                
                // Handle the response from the new event system
                if (response.Response != null)
                {
                    // Early response - convert HttpResponseMessage back to custom Response
                    var customResponse = await ConvertHttpResponseMessageToCustomResponse(response.Response);
                    args.HttpClient.Response = customResponse;
                    args.HttpClient.Response.Locked = true; // Mark as custom response
                }
                else if (response.ModifiedRequest != null)
                {
                    // Modified request - update the custom Request object
                    await UpdateCustomRequestFromHttpRequestMessage(args.HttpClient.Request, response.ModifiedRequest);
                }
                // For ContinueResponse, no action needed - processing continues normally
            }
            finally
            {
                requestArguments.Dispose();
            }
        }
    }

    /// <summary>
    /// Create HttpRequestMessage from custom Request object
    /// </summary>
    private HttpRequestMessage CreateHttpRequestMessageFromCustomRequest(Request customRequest)
    {
        var httpMethod = new HttpMethod(customRequest.Method);
        var httpRequest = new HttpRequestMessage(httpMethod, customRequest.RequestUri)
        {
            Version = customRequest.HttpVersion
        };

        // Copy headers from custom Request to HttpRequestMessage
        // We need to separate content headers from request headers
        var contentHeaders = new List<HttpHeader>();
        foreach (var header in customRequest.Headers.GetAllHeaders())
        {
            if (IsContentHeader(header.Name))
            {
                contentHeaders.Add(header);
            }
            else
            {
                httpRequest.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }
        }

        // Handle body if present
        if (customRequest.HasBody && customRequest.IsBodyRead)
        {
            httpRequest.Content = new ByteArrayContent(customRequest.Body);
            
            // Add content headers
            foreach (var header in contentHeaders)
            {
                httpRequest.Content.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }
        }

        return httpRequest;
    }

    /// <summary>
    /// Determines if a header is a content header
    /// </summary>
    private static bool IsContentHeader(string headerName)
    {
        return headerName.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Range", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Language", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Location", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-MD5", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Expires", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Last-Modified", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Convert HttpResponseMessage to custom Response object for backward compatibility
    /// </summary>
    private async Task<Response> ConvertHttpResponseMessageToCustomResponse(HttpResponseMessage httpResponse)
    {
        var response = new Response
        {
            StatusCode = (int)httpResponse.StatusCode,
            StatusDescription = httpResponse.ReasonPhrase ?? string.Empty,
            HttpVersion = httpResponse.Version
        };

        // Copy headers from HttpResponseMessage to custom Response
        foreach (var header in httpResponse.Headers)
        {
            response.Headers.AddHeader(new HttpHeader(header.Key, string.Join(", ", header.Value)));
        }

        // Handle content if present
        if (httpResponse.Content != null)
        {
            // Copy content headers
            foreach (var header in httpResponse.Content.Headers)
            {
                response.Headers.AddHeader(new HttpHeader(header.Key, string.Join(", ", header.Value)));
            }

            // Read and set body
            var responseBody = await httpResponse.Content.ReadAsByteArrayAsync();
            response.Body = responseBody;
            response.IsBodyRead = true;
        }

        return response;
    }

    /// <summary>
    /// Update custom Request object from HttpRequestMessage for backward compatibility
    /// </summary>
    private async Task UpdateCustomRequestFromHttpRequestMessage(Request customRequest, HttpRequestMessage httpRequest)
    {
        // Update basic properties
        customRequest.Method = httpRequest.Method.Method;
        customRequest.Url = httpRequest.RequestUri?.ToString() ?? customRequest.Url;
        customRequest.HttpVersion = httpRequest.Version;

        // Clear existing headers and copy from HttpRequestMessage
        customRequest.Headers.Clear();
        foreach (var header in httpRequest.Headers)
        {
            customRequest.Headers.AddHeader(new HttpHeader(header.Key, string.Join(", ", header.Value)));
        }

        // Handle content if present
        if (httpRequest.Content != null)
        {
            // Copy content headers
            foreach (var header in httpRequest.Content.Headers)
            {
                customRequest.Headers.AddHeader(new HttpHeader(header.Key, string.Join(", ", header.Value)));
            }

            // Read and set body
            var requestBody = await httpRequest.Content.ReadAsByteArrayAsync();
            customRequest.Body = requestBody;
            customRequest.IsBodyRead = true;
        }
    }

    /// <summary>
    ///     Invoke before request handler if it is set.
    /// </summary>
    /// <param name="request">The COONECT request.</param>
    /// <returns></returns>
    internal async Task OnBeforeUpStreamConnectRequest ( ConnectRequest request )
    {
        if (BeforeUpStreamConnectRequest != null)
            await BeforeUpStreamConnectRequest.InvokeAsync(this, request, ExceptionFunc);
    }

#if DEBUG
    internal bool ShouldCallBeforeRequestBodyWrite ()
    {
        if (OnRequestBodyWrite != null)
        {
            return true;
        }

        return false;
    }

    internal async Task OnBeforeRequestBodyWrite ( BeforeBodyWriteEventArgs args )
    {
        if (OnRequestBodyWrite != null)
        {
            await OnRequestBodyWrite.InvokeAsync(this, args, ExceptionFunc);
        }
    }
#endif
}
