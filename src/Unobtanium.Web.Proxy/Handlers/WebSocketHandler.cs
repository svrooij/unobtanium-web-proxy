﻿using System.Threading;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.EventArguments;
using Unobtanium.Web.Proxy.Helpers;
using Unobtanium.Web.Proxy.Http;
using Unobtanium.Web.Proxy.Network.Tcp;

namespace Unobtanium.Web.Proxy;

public partial class ProxyServer
{
    /// <summary>
    ///     Handle upgrade to websocket
    /// </summary>
    private async Task HandleWebSocketUpgrade ( SessionEventArgs args,
        HttpClientStream clientStream, TcpServerConnection serverConnection,
        CancellationTokenSource cancellationTokenSource, CancellationToken cancellationToken )
    {
        await serverConnection.Stream.WriteRequestAsync(args.HttpClient.Request, cancellationToken);

        var httpStatus = await serverConnection.Stream.ReadResponseStatus(cancellationToken);

        var response = args.HttpClient.Response;
        response.HttpVersion = httpStatus.Version;
        response.StatusCode = httpStatus.StatusCode;
        response.StatusDescription = httpStatus.Description;

        await HeaderParser.ReadHeaders(serverConnection.Stream, response.Headers,
            cancellationToken);

        await clientStream.WriteResponseAsync(response, cancellationToken);

        // If user requested call back then do it
        if (!args.HttpClient.Response.Locked) await OnBeforeResponse(args);

        await TcpHelper.SendRawWithCallbacks(clientStream, serverConnection.Stream, BufferPool,
            args.OnDataSent, args.OnDataReceived, cancellationTokenSource, ExceptionFunc);
    }
}
