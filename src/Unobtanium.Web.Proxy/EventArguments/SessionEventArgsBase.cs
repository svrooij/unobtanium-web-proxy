﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Unobtanium.Web.Proxy.Helpers;
using Unobtanium.Web.Proxy.Http;
using Unobtanium.Web.Proxy.Models;
using Unobtanium.Web.Proxy.Network.Tcp;
using Unobtanium.Web.Proxy.StreamExtended.BufferPool;
using Unobtanium.Web.Proxy.StreamExtended.Network;

namespace Unobtanium.Web.Proxy.EventArguments;

/// <summary>
/// Holds info related to a single proxy session (single request/response sequence).
/// A proxy session is bounded to a single connection from client.
/// A proxy session ends when client terminates connection to proxy
/// or when server terminates connection from proxy.
/// </summary>
public abstract class SessionEventArgsBase : ProxyEventArgsBase, IDisposable
{
    /// <summary>
    /// Buffer pool used for the session.
    /// </summary>
    protected readonly IBufferPool BufferPool;

    /// <summary>
    /// Cancellation token source for the session.
    /// </summary>
    internal readonly CancellationTokenSource CancellationTokenSource;

    /// <summary>
    /// Exception handler function for the session.
    /// </summary>
    protected readonly ExceptionHandler? ExceptionFunc;

    /// <summary>
    /// Logger instance for the session.
    /// </summary>
    internal readonly ILogger logger;

    private bool disposed;
    private bool enableWinAuth;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionEventArgsBase" /> class.
    /// </summary>
    private protected SessionEventArgsBase ( ProxyServer server, ProxyEndPoint endPoint,
        HttpClientStream clientStream, ConnectRequest? connectRequest, Request request,
        CancellationTokenSource cancellationTokenSource ) : base(server, clientStream.Connection)
    {
        BufferPool = server.BufferPool;
        ExceptionFunc = server.ExceptionFunc;
        TimeLine["Session Created"] = DateTime.UtcNow;

        CancellationTokenSource = cancellationTokenSource;

        ClientStream = clientStream;
        HttpClient = new HttpWebClient(connectRequest, request,
            new Lazy<int>(() => clientStream.Connection.GetProcessId(endPoint)));
        ProxyEndPoint = endPoint;
        EnableWinAuth = server.EnableWinAuth && IsWindowsAuthenticationSupported;
        logger = server.loggerFactory.CreateLogger<SessionEventArgsBase>();
    }

    private static bool IsWindowsAuthenticationSupported => RunTime.IsWindows;

    internal TcpServerConnection ServerConnection => HttpClient.Connection;

    /// <summary>
    ///     Holds a reference to client
    /// </summary>
    internal TcpClientConnection ClientConnection => ClientStream.Connection;

    internal HttpClientStream ClientStream { get; }

    /// <summary>
    /// Unique identifier of this session, on the client side.
    /// </summary>
    public Guid ClientConnectionId => ClientConnection.Id;

    /// <summary>
    /// Unique identifier of this session, on the server side.
    /// </summary>
    public Guid ServerConnectionId => HttpClient.HasConnection ? ServerConnection.Id : Guid.Empty;

    /// <summary>
    ///     Relative milliseconds for various events.
    /// </summary>
    public Dictionary<string, DateTime> TimeLine { get; } = [];

    /// <summary>
    ///     Returns a user data for this request/response session which is
    ///     same as the user data of HttpClient.
    /// </summary>
    public object? UserData
    {
        get => HttpClient.UserData;
        set => HttpClient.UserData = value;
    }

    /// <summary>
    ///     Enable/disable Windows Authentication (NTLM/Kerberos) for the current session.
    /// </summary>
    /// <exception cref="NotSupportedException">Windows Authentication is not supported</exception>
    public bool EnableWinAuth
    {
        get => enableWinAuth;
        set
        {
            if (value && !IsWindowsAuthenticationSupported)
            {
                var ex = new NotSupportedException("Windows Authentication is not supported");
                logger.LogWarning(ex, "You cannot enable Windows Authentication on this session");
                throw ex;
            }


            enableWinAuth = value;
        }
    }

    /// <summary>
    ///     Does this session uses SSL?
    /// </summary>
    public bool IsHttps => HttpClient.Request.IsHttps;

    /// <summary>
    ///     Client Local End Point.
    /// </summary>
    public IPEndPoint? ClientLocalEndPoint => ClientConnection.LocalEndPoint as IPEndPoint;

    /// <summary>
    ///     Client Remote End Point.
    /// </summary>
    public IPEndPoint? ClientRemoteEndPoint => ClientConnection.RemoteEndPoint as IPEndPoint;

    /// <summary>
    ///     The web client used to communicate with server for this session.
    /// </summary>
    public HttpWebClient HttpClient { get; }

    /// <summary>
    ///     Gets or sets the custom up stream proxy.
    /// </summary>
    /// <value>
    ///     The custom up stream proxy.
    /// </value>
    public IExternalProxy? CustomUpStreamProxy { get; set; }

    /// <summary>
    ///     Are we using a custom upstream HTTP(S) proxy?
    /// </summary>
    public IExternalProxy? CustomUpStreamProxyUsed { get; internal set; }

    /// <summary>
    ///     Local endpoint via which we make the request.
    /// </summary>
    public ProxyEndPoint ProxyEndPoint { get; }

    /// <summary>
    ///     Is this a transparent endpoint?
    /// </summary>
    public bool IsTransparent => ProxyEndPoint is TransparentProxyEndPoint;

    /// <summary>
    ///     Is this a SOCKS endpoint?
    /// </summary>
    public bool IsSocks => ProxyEndPoint is SocksProxyEndPoint;

    /// <summary>
    ///     The last exception that happened.
    /// </summary>
    public Exception? Exception { get; internal set; }

    /// <summary>
    /// Dispose this instance.
    /// </summary>
    public void Dispose ()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Called when an exception is thrown in user event.
    /// </summary>
    protected void OnException ( Exception exception )
    {
        ExceptionFunc?.Invoke(exception);
    }

    /// <summary>
    ///     Dispose this instance.
    /// </summary>
    protected virtual void Dispose ( bool disposing )
    {
        if (disposed) return;

        if (disposing)
        {
            CustomUpStreamProxyUsed = null;

            HttpClient.FinishSession();
        }

        DataSent = null;
        DataReceived = null;
        Exception = null;

        disposed = true;
    }

    /// <summary>
    ///     Finalizer.
    /// </summary>
    ~SessionEventArgsBase ()
    {
#if DEBUG
        // Finalizer should not be called
        System.Diagnostics.Debugger.Break();
#endif

        Dispose(false);
    }

    /// <summary>
    ///     Fired when data is sent within this session to server/client.
    /// </summary>
    public event EventHandler<DataEventArgs>? DataSent;

    /// <summary>
    ///     Fired when data is received within this session from client/server.
    /// </summary>
    public event EventHandler<DataEventArgs>? DataReceived;

    internal void OnDataSent ( byte[] buffer, int offset, int count )
    {
        try
        {
            DataSent?.Invoke(this, new DataEventArgs(buffer, offset, count));
        }
        catch (Exception ex)
        {
            OnException(new Exception("Exception thrown in user event", ex));
        }
    }

    internal void OnDataReceived ( byte[] buffer, int offset, int count )
    {
        try
        {
            DataReceived?.Invoke(this, new DataEventArgs(buffer, offset, count));
        }
        catch (Exception ex)
        {
            OnException(new Exception("Exception thrown in user event", ex));
        }
    }

    /// <summary>
    ///     Terminates the session abruptly by terminating client/server connections.
    /// </summary>
    public void TerminateSession ()
    {
        CancellationTokenSource.Cancel();
    }
}
