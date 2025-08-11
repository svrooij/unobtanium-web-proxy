using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Threading;
using Unobtanium.Web.Proxy.Helpers;
using Unobtanium.Web.Proxy.Http;
using Unobtanium.Web.Proxy.Models;
using Unobtanium.Web.Proxy.StreamExtended.Network;

namespace Unobtanium.Web.Proxy.EventArguments;

/// <summary>
///     A class that wraps the state when a tunnel connect event happen for Explicit endpoints.
/// </summary>
[Obsolete("This will be removed")]
public class TunnelConnectSessionEventArgs : SessionEventArgsBase
{
    private bool? isHttpsConnect;

    internal TunnelConnectSessionEventArgs ( ProxyServer server, ProxyEndPoint endPoint, ConnectRequest connectRequest,
        HttpClientStream clientStream, CancellationToken cancellationToken)
        : base(server, endPoint, clientStream, connectRequest, connectRequest, cancellationToken)
    {
    }

    /// <summary>
    ///     Should we decrypt the Ssl or relay it to server?
    ///     Default is true.
    /// </summary>
    public bool DecryptSsl { get; set; } = true;

    /// <summary>
    ///     When set to true it denies the connect request with a Forbidden status.
    /// </summary>
    public bool DenyConnect { get; set; }

    /// <summary>
    ///     Is this a connect request to secure HTTP server? Or is it to some other protocol.
    /// </summary>
    public bool IsHttpsConnect
    {
        get => isHttpsConnect ??
               throw new Exception("The value of this property is known in the BeforeTunnelConnectResponse event");

        internal set => isHttpsConnect = value;
    }

    /// <summary>
    ///     Fired when decrypted data is sent within this session to server/client.
    /// </summary>
    [Obsolete("You won't receive any data here, were you using it? If so, please open an issue on GitHub to discuss.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public event EventHandler<DataEventArgs>? DecryptedDataSent;

    /// <summary>
    ///     Fired when decrypted data is received within this session from client/server.
    /// </summary>
    [Obsolete("You won't receive any data here, were you using it? If so, please open an issue on GitHub to discuss.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public event EventHandler<DataEventArgs>? DecryptedDataReceived;



    /// <summary>
    /// Dispose the object.
    /// </summary>
    ~TunnelConnectSessionEventArgs ()
    {
        Dispose(false);
    }
}
