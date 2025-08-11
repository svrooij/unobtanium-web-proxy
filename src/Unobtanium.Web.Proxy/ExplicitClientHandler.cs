using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.EventArguments;
using Unobtanium.Web.Proxy.Exceptions;
using Unobtanium.Web.Proxy.Extensions;
using Unobtanium.Web.Proxy.Helpers;
using Unobtanium.Web.Proxy.Http;
using Unobtanium.Web.Proxy.Http2;
using Unobtanium.Web.Proxy.Models;
using Unobtanium.Web.Proxy.Network.Tcp;
using Unobtanium.Web.Proxy.Services;
using Unobtanium.Web.Proxy.StreamExtended;
using SslExtensions = Unobtanium.Web.Proxy.Extensions.SslExtensions;

namespace Unobtanium.Web.Proxy;

public partial class ProxyServer
{
    /// <summary>
    ///     Parse incoming HTTP request stream directly to HttpRequestMessage
    /// </summary>
    /// <param name="clientStream">The client stream to read from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HttpRequestMessage or null if invalid</returns>
    private async Task<HttpRequestMessage?> ParseHttpRequestMessage ( HttpClientStream clientStream, CancellationToken cancellationToken )
    {
        try
        {
            // Read the request line
            var requestLine = await clientStream.ReadRequestLine(cancellationToken);
            if (requestLine.IsEmpty()) return null;

            // Parse method
            var httpMethod = Native.HttpMethodParser.ParseMethodFromString(requestLine.Method);

            // Create the request URI
            var requestUri = requestLine.RequestUri.GetString();

            // If it's not a full URI, we'll need to construct it later with Host header
            Uri? uri = null;
            if (Uri.IsWellFormedUriString(requestUri, UriKind.Absolute))
            {
                uri = new Uri(requestUri);
            }

            // Create HttpRequestMessage
            var httpRequest = new HttpRequestMessage(httpMethod, uri?.ToString() ?? requestUri);
            httpRequest.Version = requestLine.Version;

            // Read headers
            var headers = new HeaderCollection();
            await HeaderParser.ReadHeaders(clientStream, headers, cancellationToken);

            // Set headers on HttpRequestMessage
            var contentHeadersKey = new HttpRequestOptionsKey<HeaderCollection>("ContentHeaders");
            foreach (var header in headers.GetAllHeaders())
            {
                try
                {
                    // Skip compression-related headers to prevent compressed responses
                    // This ensures the proxy can easily process and modify response content
                    if (header.Name.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Skip this header completely
                    }

                    // Try to add to request headers first
                    if (!httpRequest.Headers.TryAddWithoutValidation(header.Name, header.Value))
                    {
                        // If it fails, it might be a content header, we'll handle it when we create content
                        // For now, store it in Options for later processing
                        if (!httpRequest.Options.TryGetValue(contentHeadersKey, out _))
                        {
                            httpRequest.Options.Set(contentHeadersKey, new HeaderCollection());
                        }

                        if (httpRequest.Options.TryGetValue(contentHeadersKey, out var contentHeaders))
                        {
                            contentHeaders?.AddHeader(header);
                        }
                    }
                }
                catch
                {
                    // Some headers might not be valid, skip them
                }
            }

            // Construct full URI if we only have a relative path
            if (uri == null)
            {
                var hostHeader = headers.GetHeaderValueOrNull(KnownHeaders.Host);
                if (hostHeader != null)
                {
                    var scheme = "http"; // Will be updated to https if needed
                    var fullUri = $"{scheme}://{hostHeader}{requestUri}";
                    if (Uri.IsWellFormedUriString(fullUri, UriKind.Absolute))
                    {
                        httpRequest.RequestUri = new Uri(fullUri);
                    }
                }
            }

            // Handle request body
            var contentLengthHeader = headers.GetHeaderValueOrNull(KnownHeaders.ContentLength);
            var transferEncodingHeader = headers.GetHeaderValueOrNull(KnownHeaders.TransferEncoding);
            var isChunked = transferEncodingHeader?.Contains("chunked", StringComparison.OrdinalIgnoreCase) == true;

            long contentLength = 0;
            var hasContentLength = contentLengthHeader != null && long.TryParse(contentLengthHeader, out contentLength);

            if ((hasContentLength && contentLength > 0) || isChunked)
            {
                // Read the body
                using var bodyStream = new MemoryStream();

                if (isChunked)
                {
                    // Handle chunked encoding
                    await ReadChunkedBody(clientStream, bodyStream, cancellationToken);
                }
                else if (contentLength > 0)
                {
                    // Handle content-length body
                    await ReadBodyWithContentLength(clientStream, bodyStream, contentLength, cancellationToken);
                }

                if (bodyStream.Length > 0)
                {
                    var bodyBytes = bodyStream.ToArray();
                    httpRequest.Content = new ByteArrayContent(bodyBytes);

                    // Apply content headers that were stored earlier
                    if (httpRequest.Options.TryGetValue(contentHeadersKey, out var contentHeaders) && contentHeaders != null)
                    {
                        foreach (var contentHeader in contentHeaders.GetAllHeaders())
                        {
                            httpRequest.Content.Headers.TryAddWithoutValidation(contentHeader.Name, contentHeader.Value);
                        }
                    }
                }
            }

            return httpRequest;
        }
        catch (Exception)
        {
            // If parsing fails, return null
            return null;
        }
    }

    /// <summary>
    ///     Read request body with content-length
    /// </summary>
    private async Task ReadBodyWithContentLength ( HttpClientStream clientStream, MemoryStream bodyStream, long contentLength, CancellationToken cancellationToken )
    {
        var buffer = new byte[4096];
        long totalRead = 0;

        while (totalRead < contentLength)
        {
            var toRead = (int)Math.Min(buffer.Length, contentLength - totalRead);
            var read = await clientStream.ReadAsync(buffer, 0, toRead, cancellationToken);
            if (read == 0) break;

            await bodyStream.WriteAsync(buffer, 0, read, cancellationToken);
            totalRead += read;
        }
    }

    /// <summary>
    ///     Read chunked request body
    /// </summary>
    private async Task ReadChunkedBody ( HttpClientStream clientStream, MemoryStream bodyStream, CancellationToken cancellationToken )
    {
        while (true)
        {
            // Read chunk size line
            var chunkSizeLine = await clientStream.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(chunkSizeLine)) break;

            // Parse chunk size (hex)
            var chunkSize = Convert.ToInt32(chunkSizeLine.Split(';')[0], 16);
            if (chunkSize == 0) break; // End of chunks

            // Read chunk data
            var buffer = new byte[chunkSize];
            var totalRead = 0;
            while (totalRead < chunkSize)
            {
                var read = await clientStream.ReadAsync(buffer, totalRead, chunkSize - totalRead, cancellationToken);
                if (read == 0) break;
                totalRead += read;
            }

            await bodyStream.WriteAsync(buffer, 0, totalRead, cancellationToken);

            // Read trailing CRLF after chunk data
            await clientStream.ReadLineAsync(cancellationToken);
        }

        // Read trailing headers (if any)
        var trailerHeaders = new HeaderCollection();
        await HeaderParser.ReadHeaders(clientStream, trailerHeaders, cancellationToken);
    }

    /// <summary>
    ///     This is called when client is aware of proxy
    ///     So for HTTPS requests client would send CONNECT header to negotiate a secure tcp tunnel via proxy
    /// </summary>
    /// <param name="endPoint">The explicit endpoint.</param>
    /// <param name="clientConnection">The client connection.</param>
    /// <param name="activityContext"></param>
    /// <param name="cancellationTokenSource"></param>
    /// <returns>The task.</returns>
    private async Task HandleClientExplicitEndpoint ( ExplicitProxyEndPoint endPoint, TcpClientConnection clientConnection, ActivityContext? activityContext, CancellationTokenSource cancellationTokenSource )
    {
        logger.LogDebug("HandleClientExplicitEndpoint called for {EndPoint}", endPoint);
        using var handleActivity = ProxyActivitySource.StartActivity(nameof(HandleClientExplicitEndpoint), ActivityKind.Consumer, activityContext ?? default);
        

        var clientStream = new HttpClientStream(this, clientConnection, clientConnection.GetStream(), BufferPool,
            cancellationTokenSource.Token);

        Task<TcpServerConnection?>? prefetchConnectionTask = null;
        var closeServerConnection = false;
        bool decryptSsl = false;

        //TunnelConnectSessionEventArgs? connectArgs = null;

        try
        {
            //using var methodActivity = ProxyActivitySource.StartActivity($"{nameof(HandleClientExplicitEndpoint)}_Method", ActivityKind.Consumer);
            var method = await HttpHelper.GetMethod(clientStream, BufferPool, cancellationTokenSource.Token);
            //methodActivity?.Stop();
            if (clientStream.IsClosed || cancellationTokenSource.IsCancellationRequested) return;

            // Client wants to create a secure tcp tunnel (probably its a HTTPS or Websocket request)
            if (method == KnownMethod.Connect)
            {
                using var connectActivity = ProxyActivitySource.StartActivity($"{nameof(HandleClientExplicitEndpoint)}_Connect", ActivityKind.Consumer, handleActivity?.Context ?? default);
                // read the first line HTTP command
                //var nativeConnectRequest = await ParseHttpRequestMessage(clientStream, cancellationTokenSource.Token);
                var requestLine = await clientStream.ReadRequestLine(cancellationTokenSource.Token);
                if (requestLine.IsEmpty()) return;

                var connectRequest = new ConnectRequest(requestLine.RequestUri)
                {
                    RequestUriString8 = requestLine.RequestUri,
                    HttpVersion = requestLine.Version
                };

                await HeaderParser.ReadHeaders(clientStream, connectRequest.Headers, cancellationTokenSource.Token);

                // Extract hostname once and reuse
                var connectHostnameSpan = requestLine.RequestUri.GetString().AsSpan();
                var colonIndex = connectHostnameSpan.IndexOf(':');
                string connectHostname = colonIndex >= 0
                    ? connectHostnameSpan[..colonIndex].ToString()
                    : connectHostnameSpan.ToString();

                //connectArgs = new TunnelConnectSessionEventArgs(this, endPoint, connectRequest, clientStream,
                //    cancellationTokenSource.Token);
                //using var decryptSslActivity = ProxyActivitySource.StartActivity($"{nameof(HandleClientExplicitEndpoint)}_ShouldDecryptSsl", ActivityKind.Internal);
                decryptSsl = await configuration.Events.InvokeShouldDecryptNewConnection(connectHostname, cancellationTokenSource).ConfigureAwait(false);
                var sendRawData = !decryptSsl;
                //decryptSslActivity?.Stop();
                // write back successful CONNECT response
                var nativeConnectResponse = NativeHttpMessagesHelper.ConnectOkResponse(requestLine.Version);
                await clientStream.WriteAsync(nativeConnectResponse, null, cancellationTokenSource.Token);

                // Set ContentLength explicitly to properly handle HTTP 1.0
                //var response = ConnectResponse.CreateSuccessfulConnectResponse(connectRequest.HttpVersion);
                //response.ContentLength = 0;
                //response.Headers.FixProxyHeaders();
                //connectArgs.HttpClient.Response = response;

                //await clientStream.WriteResponseAsync(response, cancellationTokenSource.Token);

                var clientHelloInfo = await SslTools.PeekClientHello(clientStream, BufferPool, connectActivity?.Context ?? default, cancellationTokenSource.Token);
                if (clientStream.IsClosed) return;

                var isClientHello = clientHelloInfo != null;
                if (clientHelloInfo != null)
                {
                    connectRequest.TunnelType = TunnelType.Https;
                    connectRequest.ClientHelloInfo = clientHelloInfo;
                }

                if (decryptSsl && clientHelloInfo != null)
                {
                    connectRequest.IsHttps = true; // todo: move this line to the previous "if"

                    var sslProtocol = clientHelloInfo.SslProtocol & SupportedSslProtocols;
                    if (sslProtocol == SslProtocols.None)
                    {
                        throw new Exception("Unsupported client SSL version.");
                    }

                    clientStream.Connection.SslProtocol = sslProtocol;

                    // Start parallel operations for performance optimization
                    Task<bool> http2SupportTask = null!;
                    Task<X509Certificate2?> certificateTask = null!;
                    
                    // Start certificate generation/retrieval in parallel
                    certificateTask = Task.Run(() => CertificateManager.GetOrGenerateCertificateAsync(connectHostname, cancellationTokenSource.Token));

                    // Start HTTP/2 support detection in parallel if enabled
                    if (EnableHttp2 && false)
                    {
                        var alpn = clientHelloInfo.GetAlpn();
                        if (alpn != null && alpn.Contains(SslApplicationProtocol.Http2))
                        {
                            //using var http2Activity = ProxyActivitySource.StartActivity($"{nameof(HandleClientExplicitEndpoint)}_Http2Detection", ActivityKind.Internal, handleActivity?.Context ?? default);
                            http2SupportTask = Task.Run(async () =>
                            {
                                try
                                {
                                    // Test server HTTP/2 support
                                    // TODO: fix this to not require connectArgs
                                    throw new NotImplementedException("TcpConnectionFactory.GetServerConnection with prefetch is not implemented yet.");
                                    var connection = await TcpConnectionFactory.GetServerConnection(this, null,
                                        true, SslExtensions.Http2ProtocolAsList,
                                        true, true, cancellationTokenSource.Token);

                                    if (connection != null)
                                    {
                                        var supported = connection.NegotiatedApplicationProtocol == SslApplicationProtocol.Http2;
                                        // release connection back to pool instead of closing when connection pool is enabled.
                                        await TcpConnectionFactory.Release(connection, true);
                                        return supported;
                                    }
                                    return false;
                                }
                                catch (Exception)
                                {
                                    // ignore
                                    return false;
                                }
                            });
                        }
                        else
                        {
                            http2SupportTask = Task.FromResult(false);
                        }
                    }
                    else
                    {
                        http2SupportTask = Task.FromResult(false);
                    }

                    // Start server connection prefetch early and in parallel
                    if (EnableTcpServerConnectionPrefetch)
                    {
                        // don't pass cancellation token here
                        // it could cause floating server connections when client exits
                        // TODO: fix this to not require connectArgs
                        throw new NotImplementedException("TcpConnectionFactory.GetServerConnection with prefetch is not implemented yet.");
                        prefetchConnectionTask = TcpConnectionFactory.GetServerConnection(this, null,
                            true, null, false, true,
                            CancellationToken.None);
                    }

                    await Task.WhenAll(http2SupportTask, certificateTask).ConfigureAwait(false);

                    X509Certificate2? certToUse = certificateTask.Result;
                    SslStream? sslStream = null;
                    try
                    {
                        sslStream = new SslStream(clientStream, false);

                        // Prepare SSL authentication options
                        var options = new SslServerAuthenticationOptions
                        {
                            ServerCertificate = certToUse,
                            ClientCertificateRequired = false,
                            EnabledSslProtocols = SupportedSslProtocols,
                            CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                        };
                        
                        if (EnableHttp2 && http2SupportTask.Result)
                        {
                            options.ApplicationProtocols = clientHelloInfo.GetAlpn();
                            if (options.ApplicationProtocols == null || options.ApplicationProtocols.Count == 0)
                                options.ApplicationProtocols = SslExtensions.Http11ProtocolAsList;
                        }

                        await sslStream.AuthenticateAsServerAsync(options, cancellationTokenSource.Token);

                        clientStream.Connection.NegotiatedApplicationProtocol = sslStream.NegotiatedApplicationProtocol;

                        // HTTPS server created - we can now decrypt the client's traffic
                        clientStream = new HttpClientStream(this, clientStream.Connection, sslStream, BufferPool,
                            cancellationTokenSource.Token);
                        sslStream = null; // clientStream was created, no need to keep SSL stream reference
                    }
                    catch (Exception e)
                    {
                        sslStream?.Dispose();

                        var certName = certToUse?.GetNameInfo(X509NameType.SimpleName, false);
                        throw new ProxyConnectException(
                            $"Couldn't authenticate host '{connectHostname}' with certificate '{certName}'.", e, null);
                    }

                    method = await HttpHelper.GetMethod(clientStream, BufferPool, cancellationTokenSource.Token);
                    if (clientStream.IsClosed) return;

                    if (method == KnownMethod.Invalid)
                    {
                        sendRawData = true;
                        await TcpConnectionFactory.Release(prefetchConnectionTask, true);
                        prefetchConnectionTask = null;
                    }
                }
                else if (clientHelloInfo == null)
                {
                    method = await HttpHelper.GetMethod(clientStream, BufferPool, cancellationTokenSource.Token);
                    if (clientStream.IsClosed) return;
                }

                if (cancellationTokenSource.IsCancellationRequested)
                    throw new Exception("Session was terminated by user.");

                if (method == KnownMethod.Invalid)
                    sendRawData = true;

                // Forward the connection as is to the server
                // TODO: Maybe this part can be optimized even further, but for now I don't care
                if (sendRawData)
                {
                    logger.LogInformation("Sending raw request to {Hostname}", connectHostname);

                    // create new connection to server.
                    // If we detected that client tunnel CONNECTs without SSL by checking for empty client hello then 
                    // this connection should not be HTTPS.
                    // TODO: What do I fix for this to not require connectArgs?
                    var connection = (await TcpConnectionFactory.GetServerConnection(this, null, //connectArgs,
                        true, null,
                        true, false, cancellationTokenSource.Token))!;

                    try
                    {
                        if (isClientHello)
                        {
                            var available = clientStream.Available;
                            if (available > 0)
                            {
                                // send the buffered data
                                var data = BufferPool.GetBuffer();

                                try
                                {
                                    // clientStream.Available should be at most BufferSize because it is using the same buffer size
                                    var read = await clientStream.ReadAsync(data, 0, available, cancellationTokenSource.Token);
                                    if (read != available) throw new IOException("Raw stream has wrong number of bytes red");

                                    await connection.Stream.WriteAsync(data, 0, available, true, cancellationTokenSource.Token);
                                }
                                finally
                                {
                                    BufferPool.ReturnBuffer(data);
                                }
                            }

                            var serverHelloInfo =
                                await SslTools.PeekServerHello(connection.Stream, BufferPool, cancellationTokenSource.Token);
                            // TODO: What do I fix for this to not require connectArgs?

                            //((ConnectResponse)connectArgs.HttpClient.Response).ServerHelloInfo = serverHelloInfo;
                        }

                        if (!clientStream.IsClosed && !connection.Stream.IsClosed)
                            await TcpHelper.SendRaw(clientStream, connection.Stream, cancellationTokenSource.Token);
                    }
                    finally
                    {
                        await TcpConnectionFactory.Release(connection, true);
                    }

                    return;
                }
                connectActivity?.Stop();
            }

            //if (connectArgs != null && method == KnownMethod.Pri)
            //{
            //    using var prefaceActivity = ProxyActivitySource.StartActivity($"{nameof(HandleClientExplicitEndpoint)}_Http2ConnectionPreface", ActivityKind.Consumer, handleActivity?.Context ?? default);
            //    // todo
            //    var httpCmd = await clientStream.ReadLineAsync(cancellationTokenSource.Token);
            //    if (httpCmd == "PRI * HTTP/2.0")
            //    {
            //        connectArgs.HttpClient.ConnectRequest!.TunnelType = TunnelType.Http2;

            //        // HTTP/2 Connection Preface
            //        var line = await clientStream.ReadLineAsync(cancellationTokenSource.Token);
            //        if (line != string.Empty)
            //            throw new Exception($"HTTP/2 Protocol violation. Empty string expected, '{line}' received");

            //        line = await clientStream.ReadLineAsync(cancellationTokenSource.Token);
            //        if (line != "SM")
            //            throw new Exception($"HTTP/2 Protocol violation. 'SM' expected, '{line}' received");

            //        line = await clientStream.ReadLineAsync(cancellationTokenSource.Token);
            //        if (line != string.Empty)
            //            throw new Exception($"HTTP/2 Protocol violation. Empty string expected, '{line}' received");

            //        var connection = (await TcpConnectionFactory.GetServerConnection(this, connectArgs,
            //            true, SslExtensions.Http2ProtocolAsList,
            //            true, false, cancellationTokenSource.Token))!;
            //        try
            //        {
            //            var connectionPreface = new ReadOnlyMemory<byte>(Http2Helper.ConnectionPreface);
            //            await connection.Stream.WriteAsync(connectionPreface, cancellationTokenSource.Token);
            //            await Http2Helper.SendHttp2(clientStream, connection.Stream,
            //                () => new SessionEventArgs(this, endPoint, clientStream, connectArgs?.HttpClient.ConnectRequest, cancellationTokenSource.Token)
            //                {
            //                    UserData = connectArgs?.UserData
            //                },
            //                async args => { await OnBeforeRequest(args, prefaceActivity, cancellationToken: cancellationTokenSource.Token); },
            //                async args => { await OnBeforeResponse(args); },
            //                connectArgs.CancellationToken, clientStream.Connection.Id, null);
            //        }
            //        finally
            //        {
            //            await TcpConnectionFactory.Release(connection, true);
            //            prefaceActivity?.Stop();
            //        }
            //    }
            //}

            // NEW: Handle regular HTTP requests using HttpRequestMessage
            if (method != KnownMethod.Connect && method != KnownMethod.Pri && method != KnownMethod.Invalid)
            {
                using var requestActivity = ProxyActivitySource.StartActivity($"{nameof(HandleClientExplicitEndpoint)}_HandleHttpMessage", ActivityKind.Consumer, handleActivity?.Context ?? default);
                // Parse the incoming request into HttpRequestMessage
                var httpRequestMessage = await ParseHttpRequestMessage(clientStream, cancellationTokenSource.Token);
                if (httpRequestMessage != null)
                {
                    try
                    {
                        // Update the request URI to be HTTPS if this is a decrypted SSL connection
                        if (httpRequestMessage.RequestUri != null && decryptSsl) //connectArgs?.HttpClient.ConnectRequest?.IsHttps == true && httpRequestMessage.RequestUri != null
                        {
                            var builder = new UriBuilder(httpRequestMessage.RequestUri)
                            {
                                Scheme = "https",
                                Port = httpRequestMessage.RequestUri.IsDefaultPort? 443 : httpRequestMessage.RequestUri.Port
                                //Port = -1 // Use default port for HTTPS
                            };
                            httpRequestMessage.RequestUri = builder.Uri;
                        }

                        HttpResponseMessage? httpResponseMessage = null;                        
                        
                        // Set activity tags for better telemetry
                        requestActivity?.SetTag("http.request.method", httpRequestMessage.Method.Method);
                        requestActivity?.SetTag("url.full", httpRequestMessage.RequestUri?.ToString());
                        if (httpRequestMessage.RequestUri?.Host != null)
                        {
                            requestActivity?.SetTag("server.host", httpRequestMessage.RequestUri.Host);
                            requestActivity?.SetTag("server.port", httpRequestMessage.RequestUri.Port);
                        }

                        // Add distributed tracing headers to the outgoing request
                        AddDistributedTracingHeadersToHttpRequestMessage(httpRequestMessage, requestActivity);

                        var requestArguments = new Events.RequestEventArguments(httpRequestMessage, requestActivity);
                        var handlerResponse = await configuration.Events.InvokeOnRequest(this, requestArguments, logger, cancellationTokenSource.Token);
                        if (handlerResponse.Response is not null)
                        {
                            httpResponseMessage = handlerResponse.Response;
                        }
                        else if (handlerResponse.ModifiedRequest is not null)
                        {
                            httpRequestMessage = handlerResponse.ModifiedRequest;
                        }
                        requestActivity?.Stop();


                        // Proxy server did not modify the request, use HttpClient to send the request to remote server
                        if (httpResponseMessage is null)
                        {
                            using (var httpClient = this.httpClientFactory.CreateHttpClient())
                            {
                                httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseContentRead, cancellationTokenSource.Token);
                            }


                            using (var responseHandlerActivity = ProxyActivitySource.StartActivity($"{nameof(HandleClientExplicitEndpoint)}_OnResponse", ActivityKind.Producer, handleActivity?.Context ?? default))
                            {
                                var responseArguments = new Events.ResponseEventArguments(httpRequestMessage, httpResponseMessage, responseHandlerActivity, requestArguments.RequestId);
                                var eventResponse = await configuration.Events.InvokeOnResponse(this, responseArguments, logger, cancellationTokenSource.Token);
                                if (eventResponse.ModifiedResponse is not null)
                                {
                                    httpResponseMessage = eventResponse.ModifiedResponse;
                                }
                            }

                            using (var responseActivity = ProxyActivitySource.StartActivity($"{nameof(HandleClientExplicitEndpoint)}_TransmitRemoteResponse", ActivityKind.Producer, handleActivity?.Context ?? default))
                            {
                                // Convert HttpResponseMessage to custom Response object
                                var response = await ConvertHttpResponseMessage(httpResponseMessage); // Convert to custom Response object
                                await clientStream.WriteResponseAsync(response, cancellationTokenSource.Token);
                                responseActivity?.SetTag("http.response.status_code", httpResponseMessage.StatusCode);

                                //httpResponseMessage = httpResponseMessage.Clone();
                                
                                //if (httpResponseMessage.Content is not null)
                                //{
                                //    //var content = await httpResponseMessage.Content.ReadAsByteArrayAsync(cancellationTokenSource.Token);
                                //    //await clientStream.WriteAsync(httpResponseMessage, content, cancellationTokenSource.Token);
                                //    await httpResponseMessage.Content.LoadIntoBufferAsync();
                                //}
                                //else
                                //{
                                //    //await clientStream.WriteAsync(httpResponseMessage, null, cancellationTokenSource.Token);
                                //}
                                //await clientStream.WriteAsync(httpResponseMessage, null, cancellationTokenSource.Token);

                            }
                            return;
                        }


                        using (var responseActivity = ProxyActivitySource.StartActivity($"{nameof(HandleClientExplicitEndpoint)}_TransmitProxyResponse", ActivityKind.Producer, handleActivity?.Context ?? default))
                        {
                            // No freaking idea why I need to call this, but otherwise it won't work
                            // Maybe this calculates the Content-Length?
                            if (httpResponseMessage.Content is not null)
                            {
                                var content = await httpResponseMessage.Content.ReadAsByteArrayAsync(cancellationTokenSource.Token);
                                await clientStream.WriteAsync(httpResponseMessage, content, cancellationTokenSource.Token);
                            }
                            else
                            {
                                await clientStream.WriteAsync(httpResponseMessage, null, cancellationTokenSource.Token);
                            }
                        }

                        return;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error sending request using HttpClient");
                        // Fall back to original method
                    }
                    finally
                    {
                        httpRequestMessage.Dispose();
                    }
                } else
                {
                    logger.LogWarning("HttpRequestMessage is null, that cannot be good");
                    clientStream.Close();
                    cancellationTokenSource.Cancel();
                }
            }

            var prefetchTask = prefetchConnectionTask;
            prefetchConnectionTask = null;

            // Now create the request using original method (fallback)
            await HandleHttpSessionRequest(endPoint, clientStream, cancellationTokenSource.Token, null, prefetchTask);
        }
        catch (Exception e)
        {
            closeServerConnection = true;
            logger.LogError(e, "Error handling client request");
        }
        finally
        {
            logger.LogDebug("HandleClientExplicitEndpoint finished for {EndPoint}", endPoint);
            handleActivity?.Stop();
            if (!cancellationTokenSource.IsCancellationRequested) cancellationTokenSource.Cancel();

            await TcpConnectionFactory.Release(prefetchConnectionTask, closeServerConnection);

            clientStream.Dispose();
            //connectArgs?.Dispose();
            
        }
    }

    /// <summary>
    ///     Convert HttpResponseMessage to custom Response object
    /// </summary>
    private async Task<Response> ConvertHttpResponseMessage ( HttpResponseMessage httpResponseMessage )
    {
        var response = new Response
        {
            StatusCode = (int)httpResponseMessage.StatusCode,
            StatusDescription = httpResponseMessage.ReasonPhrase ?? string.Empty,
            HttpVersion = httpResponseMessage.Version
        };

        // Copy headers
        foreach (var header in httpResponseMessage.Headers)
        {
            response.Headers.AddHeader(new HttpHeader(header.Key, string.Join(", ", header.Value)));
        }

        if (httpResponseMessage.Content != null)
        {
            // Copy content headers
            foreach (var header in httpResponseMessage.Content.Headers)
            {
                response.Headers.AddHeader(new HttpHeader(header.Key, string.Join(", ", header.Value)));
            }

            // Read and set body
            var responseBody = await httpResponseMessage.Content.ReadAsByteArrayAsync();
            response.Body = responseBody;
            response.IsBodyRead = true;
        }

        return response;
    }

    /// <summary>
    ///     Add distributed tracing headers to HttpRequestMessage if they don't already exist.
    ///     This ensures that trace context is propagated across service boundaries.
    /// </summary>
    private void AddDistributedTracingHeadersToHttpRequestMessage(HttpRequestMessage httpRequestMessage, Activity? activity)
    {
        if (activity == null) return;

        // Check if traceparent header already exists from the client
        var existingTraceparent = httpRequestMessage.Headers.Contains("traceparent");
        var existingTracestate = httpRequestMessage.Headers.Contains("tracestate");

        // If no existing trace headers, add them from current activity
        if (!existingTraceparent)
        {
            var traceparent = activity.Id;
            if (!string.IsNullOrEmpty(traceparent))
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("traceparent", traceparent);
                activity.SetTag("http.traceparent_injected", "true");
            }
        }
        else
        {
            activity.SetTag("http.traceparent_preserved", "true");
            // If traceparent already exists, we assume it was set by the client
            // Add a link to the request activity

        }

        // Add tracestate if it exists in the activity and wasn't already present
        if (!existingTracestate && !string.IsNullOrEmpty(activity.TraceStateString))
        {
            httpRequestMessage.Headers.TryAddWithoutValidation("tracestate", activity.TraceStateString);
            activity.SetTag("http.tracestate_injected", "true");
        }
        else if (existingTracestate)
        {
            activity.SetTag("http.tracestate_preserved", "true");
        }

        // Add correlation ID for easier debugging
        httpRequestMessage.Headers.TryAddWithoutValidation("X-Correlation-ID", activity.RootId ?? activity.Id ?? Guid.NewGuid().ToString());
    }
}
